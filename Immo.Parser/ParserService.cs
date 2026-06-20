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

    // ----- Email notification for search results -----
    public async Task<bool> SendSearchResultsByEmailAsync(Immo.Parser.Models.SearchFilter filter, string recipientEmail)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail)) return false;

        var settings = await _context.AppSettings.FirstOrDefaultAsync() ?? new AppSettings();
        if (string.IsNullOrWhiteSpace(settings.SmtpHost) || string.IsNullOrWhiteSpace(settings.FromEmail))
        {
            _logger.LogWarning("SMTP settings not configured - cannot send email to {Recipient}", recipientEmail);
            return false;
        }

        // Build base query
        var thresholdDays = settings.NewOrUpdatedThresholdDays;
        var thresholdDate = DateTime.UtcNow.AddDays(-thresholdDays);

        var query = _context.Properties.AsQueryable();

        // Status
        if (filter.Status == "available") query = query.Where(p => !p.Sold && !p.UnderOption);
        else if (filter.Status == "sold") query = query.Where(p => p.Sold);
        else if (filter.Status == "under_option") query = query.Where(p => p.UnderOption);

        // Recency
        if (filter.Recency == "new") query = query.Where(p => p.CreatedAt >= thresholdDate);
        else if (filter.Recency == "updated") query = query.Where(p => p.LastUpdatedAt >= thresholdDate && p.CreatedAt < thresholdDate);
        else if (filter.Recency == "new_or_updated") query = query.Where(p => p.CreatedAt >= thresholdDate || p.LastUpdatedAt >= thresholdDate);

        if (filter.MinPrice.HasValue) query = query.Where(p => p.Price >= filter.MinPrice.Value);
        if (filter.MaxPrice.HasValue) query = query.Where(p => p.Price <= filter.MaxPrice.Value);
        if (filter.ZipCodes != null && filter.ZipCodes.Any()) query = query.Where(p => p.ZipCode != null && filter.ZipCodes.Contains(p.ZipCode));
        if (filter.MinBedrooms.HasValue) query = query.Where(p => p.Bedrooms >= filter.MinBedrooms.Value);
        if (filter.MinLivingArea.HasValue) query = query.Where(p => p.LivingArea >= filter.MinLivingArea.Value);
        if (filter.MinPlotArea.HasValue) query = query.Where(p => p.PlotArea >= filter.MinPlotArea.Value);

        if (!string.IsNullOrEmpty(filter.MaxEpc))
        {
            var epcGrades = new List<string> { "A","B","C","D","E","F","G" };
            var maxIndex = epcGrades.IndexOf(filter.MaxEpc.ToUpper());
            if (maxIndex != -1)
            {
                var allowed = epcGrades.Take(maxIndex + 1).ToList();
                query = query.Where(p => p.EpcScore != null && allowed.Contains(p.EpcScore.Substring(0,1).ToUpper()));
            }
        }

        var properties = await query.OrderByDescending(p => p.Id).ToListAsync();
        if (!properties.Any())
        {
            _logger.LogInformation("No properties match the provided filter; email not sent.");
            return false;
        }

        // Build HTML body
        var html = "<h3>Search results</h3><ul>";
        foreach (var p in properties)
        {
            var price = p.Price.HasValue ? p.Price.Value.ToString("N0") : "N/A";
            html += $"<li><strong>{System.Net.WebUtility.HtmlEncode(p.Title)}</strong> - {System.Net.WebUtility.HtmlEncode(p.ZipCode + " " + p.City)} - € {price} - <a href='{System.Net.WebUtility.HtmlEncode(p.SourceUrl)}'>details</a></li>";
        }
        html += "</ul>";

        try
        {
            using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
            {
                EnableSsl = settings.SmtpUseSsl
            };

            if (!string.IsNullOrEmpty(settings.SmtpUsername))
            {
                client.Credentials = new NetworkCredential(settings.SmtpUsername, settings.SmtpPassword);
            }

            var mail = new MailMessage
            {
                From = new MailAddress(settings.FromEmail),
                Subject = $"Immo: {properties.Count} properties matching your filter",
                Body = html,
                IsBodyHtml = true
            };
            mail.To.Add(recipientEmail);

            await client.SendMailAsync(mail);
            _logger.LogInformation("Sent search results email to {Recipient} ({Count} properties)", recipientEmail, properties.Count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send search results email to {Recipient}", recipientEmail);
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

                // ── Multi-property path (JSON API) ────────────────────────────────────
                if (strategy is IMultiPropertyParserStrategy multiStrategy)
                {
                    var properties = multiStrategy.ParseMany(page).ToList();
                    _logger.LogInformation("JSON API page {Url} yielded {Count} properties.", page.Url, properties.Count);

                    var now = DateTime.UtcNow;
                    foreach (var property in properties)
                    {
                        // Match by ExternalId + AgencyId (RawPageId is shared across all items)
                        var existingProperty = page.AgencyId.HasValue && !string.IsNullOrEmpty(property.ExternalId)
                            ? await _context.Properties.FirstOrDefaultAsync(
                                p => p.AgencyId == page.AgencyId.Value && p.ExternalId == property.ExternalId)
                            : null;

                        if (existingProperty != null)
                        {
                            var changes = RecordChanges(existingProperty, property, now);
                            if (changes.Any())
                            {
                                _context.PropertyHistories.AddRange(changes);
                                _logger.LogInformation("Recorded {Count} changes for property ExternalId={ExternalId}", changes.Count, property.ExternalId);
                            }
                            CopyFields(existingProperty, property, page.Id, page.AgencyId, now);
                            _logger.LogInformation("Updated property ExternalId={ExternalId}", property.ExternalId);
                        }
                        else
                        {
                            property.RawPageId     = page.Id;
                            property.AgencyId      = page.AgencyId;
                            property.CreatedAt     = now;
                            property.LastUpdatedAt = now;
                            _context.Properties.Add(property);
                            _logger.LogInformation("Added new property ExternalId={ExternalId} from {Url}", property.ExternalId, page.Url);
                        }
                    }

                    page.IsParsed = true;
                    await _context.SaveChangesAsync();
                    continue;
                }

                // ── Single-property path (HTML) ───────────────────────────────────────
                var document = new HtmlDocument();
                document.LoadHtml(page.HtmlContent);

                var parsedProperty = strategy.Parse(page, document);
                if (parsedProperty != null)
                {
                    var now = DateTime.UtcNow;
                    var existingProperty = await _context.Properties.FirstOrDefaultAsync(p => p.RawPageId == page.Id);
                    if (existingProperty != null)
                    {
                        var changes = RecordChanges(existingProperty, parsedProperty, now);
                        if (changes.Any())
                        {
                            _context.PropertyHistories.AddRange(changes);
                            _logger.LogInformation("Recorded {Count} changes for property {PropertyId}", changes.Count, existingProperty.Id);
                        }
                        CopyFields(existingProperty, parsedProperty, page.Id, page.AgencyId, now);
                        _logger.LogInformation("Successfully updated property from {Url}", page.Url);
                    }
                    else
                    {
                        parsedProperty.RawPageId     = page.Id;
                        parsedProperty.AgencyId      = page.AgencyId;
                        parsedProperty.CreatedAt     = now;
                        parsedProperty.LastUpdatedAt = now;
                        _context.Properties.Add(parsedProperty);
                        _logger.LogInformation("Successfully parsed new property from {Url}", page.Url);
                    }
                }

                page.IsParsed = true;
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing page {Url}", page.Url);
            }
        }
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
