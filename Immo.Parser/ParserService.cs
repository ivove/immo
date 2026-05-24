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
        var unparsedPages = _context.RawPages.Where(p => !p.IsParsed).ToList();
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

                var document = new HtmlDocument();
                document.LoadHtml(page.HtmlContent);

                var property = strategy.Parse(page, document);
                if (property != null)
                {
                    var existingProperty = await _context.Properties.FirstOrDefaultAsync(p => p.RawPageId == page.Id);
                    if (existingProperty != null)
                    {
                        // Track changes before updating fields
                        var now = DateTime.UtcNow;
                        var changes = new List<PropertyHistory>();

                        void CheckChange<T>(string fieldName, T oldValue, T newValue)
                        {
                            if (!EqualityComparer<T>.Default.Equals(oldValue, newValue))
                            {
                                changes.Add(new PropertyHistory
                                {
                                    PropertyId = existingProperty.Id,
                                    Field = fieldName,
                                    OldValue = oldValue?.ToString(),
                                    NewValue = newValue?.ToString(),
                                    ChangedAt = now
                                });
                            }
                        }

                        CheckChange("Title", existingProperty.Title, property.Title);
                        CheckChange("Description", existingProperty.Description, property.Description);
                        CheckChange("Price", existingProperty.Price, property.Price);
                        CheckChange("ZipCode", existingProperty.ZipCode, property.ZipCode);
                        CheckChange("City", existingProperty.City, property.City);
                        CheckChange("Bedrooms", existingProperty.Bedrooms, property.Bedrooms);
                        CheckChange("LivingArea", existingProperty.LivingArea, property.LivingArea);
                        CheckChange("PlotArea", existingProperty.PlotArea, property.PlotArea);
                        CheckChange("ImageUrl", existingProperty.ImageUrl, property.ImageUrl);
                        CheckChange("EpcScore", existingProperty.EpcScore, property.EpcScore);
                        CheckChange("Sold", existingProperty.Sold, property.Sold);
                        CheckChange("UnderOption", existingProperty.UnderOption, property.UnderOption);

                        if (changes.Any())
                        {
                            _context.PropertyHistories.AddRange(changes);
                            _logger.LogInformation("Recorded {Count} changes for property {PropertyId}", changes.Count, existingProperty.Id);
                        }

                        // Update existing property fields
                        existingProperty.Title = property.Title;
                        existingProperty.Description = property.Description;
                        existingProperty.Price = property.Price;
                        existingProperty.ZipCode = property.ZipCode;
                        existingProperty.City = property.City;
                        existingProperty.Bedrooms = property.Bedrooms;
                        existingProperty.LivingArea = property.LivingArea;
                        existingProperty.PlotArea = property.PlotArea;
                        existingProperty.ImageUrl = property.ImageUrl;
                        existingProperty.EpcScore = property.EpcScore;
                        existingProperty.ExternalId = property.ExternalId;
                        existingProperty.RawPageId = page.Id;
                        existingProperty.Sold = property.Sold;
                        existingProperty.UnderOption = property.UnderOption;
                        existingProperty.AgencyId = page.AgencyId;
                        existingProperty.LastUpdatedAt = DateTime.UtcNow;
                        _logger.LogInformation("Successfully updated property from {Url}", page.Url);
                    }
                    else
                    {
                        property.RawPageId = page.Id;
                        property.AgencyId = page.AgencyId;
                        property.CreatedAt = DateTime.UtcNow;
                        property.LastUpdatedAt = DateTime.UtcNow;
                        _context.Properties.Add(property);
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
}
