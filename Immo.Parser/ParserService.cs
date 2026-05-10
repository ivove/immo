using Immo.Data;
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
                        _logger.LogInformation("Successfully updated property from {Url}", page.Url);
                    }
                    else
                    {
                        property.RawPageId = page.Id;
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
