using Immo.Data;
using Immo.Data.Entities;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

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
