using Immo.Data;
using Immo.Data.Entities;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Mail;

namespace Immo.Parser;

public class ParserService
{
    private readonly ImmoContext _context;
    private readonly IEnumerable<IParserStrategy> _strategies;
    private readonly ILogger<ParserService> _logger;

    public ParserService(ImmoContext context, IEnumerable<IParserStrategy> strategies, ILogger<ParserService> logger)
    {
        _context = context;
        _strategies = strategies;
        _logger = logger;
    }

    public async Task<bool> SendSearchResultsByEmailAsync(Immo.Parser.Models.SearchFilter filter, string recipientEmail)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail)) return false;

        var settings = await _context.AppSettings.FirstOrDefaultAsync() ?? new AppSettings();
        if (string.IsNullOrWhiteSpace(settings.SmtpHost) || string.IsNullOrWhiteSpace(settings.FromEmail))
        {
            _logger.LogWarning("SMTP settings not configured - cannot send email to {Recipient}", recipientEmail);
            return false;
        }

        var thresholdDate = DateTime.UtcNow.AddDays(-settings.NewOrUpdatedThresholdDays);
        var properties = await ApplyPropertyFilter(_context.Properties.AsQueryable(), filter, thresholdDate)
            .OrderByDescending(p => p.Id)
            .ToListAsync();

        if (!properties.Any())
        {
            _logger.LogInformation("No properties match the provided filter; email not sent.");
            return false;
        }

        return await SendEmailAsync(settings, recipientEmail, BuildEmailBody(properties), properties.Count);
    }

    private static IQueryable<Property> ApplyPropertyFilter(IQueryable<Property> query, Immo.Parser.Models.SearchFilter filter, DateTime thresholdDate)
    {
        if (filter.Status == "available")    query = query.Where(p => !p.Sold && !p.UnderOption);
        else if (filter.Status == "sold")    query = query.Where(p => p.Sold);
        else if (filter.Status == "under_option") query = query.Where(p => p.UnderOption);

        if (filter.Recency == "new")              query = query.Where(p => p.CreatedAt >= thresholdDate);
        else if (filter.Recency == "updated")     query = query.Where(p => p.LastUpdatedAt >= thresholdDate && p.CreatedAt < thresholdDate);
        else if (filter.Recency == "new_or_updated") query = query.Where(p => p.CreatedAt >= thresholdDate || p.LastUpdatedAt >= thresholdDate);

        if (filter.MinPrice.HasValue)    query = query.Where(p => p.Price >= filter.MinPrice.Value);
        if (filter.MaxPrice.HasValue)    query = query.Where(p => p.Price <= filter.MaxPrice.Value);
        if (filter.ZipCodes != null && filter.ZipCodes.Any())
            query = query.Where(p => p.ZipCode != null && filter.ZipCodes.Contains(p.ZipCode));
        if (filter.MinBedrooms.HasValue)  query = query.Where(p => p.Bedrooms >= filter.MinBedrooms.Value);
        if (filter.MinLivingArea.HasValue) query = query.Where(p => p.LivingArea >= filter.MinLivingArea.Value);
        if (filter.MinPlotArea.HasValue)  query = query.Where(p => p.PlotArea >= filter.MinPlotArea.Value);

        if (!string.IsNullOrEmpty(filter.MaxEpc))
        {
            var epcGrades = new List<string> { "A", "B", "C", "D", "E", "F", "G" };
            var maxIndex = epcGrades.IndexOf(filter.MaxEpc.ToUpper());
            if (maxIndex != -1)
            {
                var allowed = epcGrades.Take(maxIndex + 1).ToList();
                query = query.Where(p => p.EpcScore != null && allowed.Contains(p.EpcScore.Substring(0, 1).ToUpper()));
            }
        }

        return query;
    }

    private static string BuildEmailBody(List<Property> properties)
    {
        var sb = new System.Text.StringBuilder("<h3>Search results</h3><ul>");
        foreach (var p in properties)
        {
            var price = p.Price.HasValue ? p.Price.Value.ToString("N0") : "N/A";
            sb.Append($"<li><strong>{WebUtility.HtmlEncode(p.Title)}</strong> - {WebUtility.HtmlEncode(p.ZipCode + " " + p.City)} - € {price} - <a href='{WebUtility.HtmlEncode(p.SourceUrl)}'>details</a></li>");
        }
        sb.Append("</ul>");
        return sb.ToString();
    }

    private async Task<bool> SendEmailAsync(AppSettings settings, string recipient, string body, int propertyCount)
    {
        try
        {
            using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort) { EnableSsl = settings.SmtpUseSsl };
            if (!string.IsNullOrEmpty(settings.SmtpUsername))
                client.Credentials = new NetworkCredential(settings.SmtpUsername, settings.SmtpPassword);

            var mail = new MailMessage
            {
                From       = new MailAddress(settings.FromEmail),
                Subject    = $"Immo: {propertyCount} properties matching your filter",
                Body       = body,
                IsBodyHtml = true
            };
            mail.To.Add(recipient);

            await client.SendMailAsync(mail);
            _logger.LogInformation("Sent search results email to {Recipient} ({Count} properties)", recipient, propertyCount);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send search results email to {Recipient}", recipient);
            return false;
        }
    }

    public async Task ParsePendingPagesAsync()
    {
        var unparsedPages = await _context.RawPages.Where(p => !p.IsParsed).ToListAsync();
        _logger.LogInformation("Found {Count} unparsed pages.", unparsedPages.Count);

        foreach (var page in unparsedPages)
        {
            try
            {
                var strategy = _strategies.FirstOrDefault(s => s.CanParse(page.Url));
                if (strategy == null)
                {
                    _logger.LogWarning("No parsing strategy found for {Url}", page.Url);
                    continue;
                }

                if (strategy is IMultiPropertyParserStrategy multiStrategy)
                    await ProcessJsonApiPageAsync(page, multiStrategy);
                else
                    await ProcessHtmlPageAsync(page, strategy);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing page {Url}", page.Url);
            }
        }
    }

    private async Task ProcessJsonApiPageAsync(RawPage page, IMultiPropertyParserStrategy strategy)
    {
        var properties = strategy.ParseMany(page).ToList();
        _logger.LogInformation("JSON API page {Url} yielded {Count} properties.", page.Url, properties.Count);

        var now = DateTime.UtcNow;
        foreach (var property in properties)
        {
            // Match by ExternalId + AgencyId (RawPageId is shared across all items)
            var existing = page.AgencyId.HasValue && !string.IsNullOrEmpty(property.ExternalId)
                ? await _context.Properties.FirstOrDefaultAsync(
                    p => p.AgencyId == page.AgencyId.Value && p.ExternalId == property.ExternalId)
                : null;

            if (existing != null)
            {
                SaveChangesIfAny(RecordChanges(existing, property, now), property.ExternalId);
                CopyFields(existing, property, page.Id, page.AgencyId, now);
                _logger.LogInformation("Updated property ExternalId={ExternalId}", property.ExternalId);
            }
            else
            {
                property.RawPageId = page.Id; property.AgencyId = page.AgencyId;
                property.CreatedAt = now;     property.LastUpdatedAt = now;
                _context.Properties.Add(property);
                _logger.LogInformation("Added new property ExternalId={ExternalId} from {Url}", property.ExternalId, page.Url);
            }
        }

        page.IsParsed = true;
        await _context.SaveChangesAsync();
    }

    private async Task ProcessHtmlPageAsync(RawPage page, IParserStrategy strategy)
    {
        var document = new HtmlDocument();
        document.LoadHtml(page.HtmlContent);

        var parsed = strategy.Parse(page, document);
        if (parsed != null)
        {
            var now = DateTime.UtcNow;
            var existing = await _context.Properties.FirstOrDefaultAsync(p => p.RawPageId == page.Id);
            if (existing != null)
            {
                SaveChangesIfAny(RecordChanges(existing, parsed, now), existing.Id.ToString());
                CopyFields(existing, parsed, page.Id, page.AgencyId, now);
                _logger.LogInformation("Successfully updated property from {Url}", page.Url);
            }
            else
            {
                parsed.RawPageId = page.Id; parsed.AgencyId = page.AgencyId;
                parsed.CreatedAt = now;     parsed.LastUpdatedAt = now;
                _context.Properties.Add(parsed);
                _logger.LogInformation("Successfully parsed new property from {Url}", page.Url);
            }
        }

        page.IsParsed = true;
        await _context.SaveChangesAsync();
    }

    private void SaveChangesIfAny(List<PropertyHistory> changes, string? identifier)
    {
        if (!changes.Any()) return;
        _context.PropertyHistories.AddRange(changes);
        _logger.LogInformation("Recorded {Count} changes for property {Id}", changes.Count, identifier);
    }

    private static List<PropertyHistory> RecordChanges(Property existing, Property incoming, DateTime now)
    {
        var changes = new List<PropertyHistory>();
        void Check<T>(string field, T old, T current)
        {
            if (!EqualityComparer<T>.Default.Equals(old, current))
                changes.Add(new PropertyHistory
                {
                    PropertyId = existing.Id,
                    Field      = field,
                    OldValue   = old?.ToString(),
                    NewValue   = current?.ToString(),
                    ChangedAt  = now
                });
        }
        Check("Title",       existing.Title,       incoming.Title);
        Check("Description", existing.Description, incoming.Description);
        Check("Price",       existing.Price,       incoming.Price);
        Check("ZipCode",     existing.ZipCode,     incoming.ZipCode);
        Check("City",        existing.City,        incoming.City);
        Check("Bedrooms",    existing.Bedrooms,    incoming.Bedrooms);
        Check("LivingArea",  existing.LivingArea,  incoming.LivingArea);
        Check("PlotArea",    existing.PlotArea,    incoming.PlotArea);
        Check("ImageUrl",    existing.ImageUrl,    incoming.ImageUrl);
        Check("EpcScore",    existing.EpcScore,    incoming.EpcScore);
        Check("Sold",        existing.Sold,        incoming.Sold);
        Check("UnderOption", existing.UnderOption, incoming.UnderOption);
        return changes;
    }

    private static void CopyFields(Property existing, Property incoming, int rawPageId, int? agencyId, DateTime now)
    {
        existing.Title         = incoming.Title;
        existing.Description   = incoming.Description;
        existing.Price         = incoming.Price;
        existing.ZipCode       = incoming.ZipCode;
        existing.City          = incoming.City;
        existing.Bedrooms      = incoming.Bedrooms;
        existing.LivingArea    = incoming.LivingArea;
        existing.PlotArea      = incoming.PlotArea;
        existing.ImageUrl      = incoming.ImageUrl;
        existing.EpcScore      = incoming.EpcScore;
        existing.SourceUrl     = incoming.SourceUrl;
        existing.ExternalId    = incoming.ExternalId;
        existing.Sold          = incoming.Sold;
        existing.UnderOption   = incoming.UnderOption;
        existing.RawPageId     = rawPageId;
        existing.AgencyId      = agencyId;
        existing.LastUpdatedAt = now;
    }
}
