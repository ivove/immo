using System.Text.Json;
using HtmlAgilityPack;
using Immo.Data;
using Immo.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Immo.Parser.Strategies;

/// <summary>
/// Parses a <see cref="RawPage"/> whose content is a JSON detail response for a single property.
/// Used by <c>html_json</c> agencies where the listing page is HTML but each property has a JSON API endpoint.
/// The page URL is stored as <c>json-detail://&lt;originalHtmlPropertyUrl&gt;</c> so the original
/// human-readable URL is recovered by stripping the prefix.
/// </summary>
public class JsonDetailParserStrategy : IParserStrategy
{
    private readonly ImmoContext _context;
    private readonly ILogger<JsonDetailParserStrategy> _logger;

    public JsonDetailParserStrategy(ImmoContext context, ILogger<JsonDetailParserStrategy> logger)
    {
        _context = context;
        _logger = logger;
    }

    public bool CanParse(string url) =>
        url.StartsWith("json-detail://", StringComparison.OrdinalIgnoreCase);

    public Property? Parse(RawPage page, HtmlDocument? _)
    {
        var config = ResolveConfig(page);
        if (config is null)
        {
            _logger.LogWarning("No parser config found for json-detail page {Url}", page.Url);
            return null;
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(page.HtmlContent);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse JSON for {Url}", page.Url);
            return null;
        }

        using (doc)
        {
            return MapItem(doc.RootElement, config, page);
        }
    }

    private ParserConfig? ResolveConfig(RawPage page)
    {
        if (page.AgencyId.HasValue)
        {
            return _context.ParserConfigs
                .Include(c => c.Agency)
                .FirstOrDefault(c => c.AgencyId == page.AgencyId.Value);
        }

        var htmlUrl = page.Url["json-detail://".Length..];
        if (Uri.TryCreate(htmlUrl, UriKind.Absolute, out var uri))
        {
            var domain = uri.Host.Replace("www.", "");
            return _context.ParserConfigs
                .Include(c => c.Agency)
                .FirstOrDefault(c => c.Agency != null && c.Agency.AgencyDomain.Contains(domain));
        }

        return null;
    }

    private Property? MapItem(JsonElement root, ParserConfig config, RawPage page)
    {
        var externalId = JsonPathHelper.GetString(root, config.JsonExternalIdPath);
        var title      = JsonPathHelper.GetString(root, config.JsonTitlePath);
        var price      = JsonPathHelper.GetDecimal(root, config.JsonPricePath);
        var desc       = JsonPathHelper.GetString(root, config.JsonDescriptionPath);
        var image      = JsonPathHelper.GetString(root, config.JsonImagePath);
        var bedrooms   = JsonPathHelper.GetInt(root, config.JsonBedroomsPath);
        var living     = JsonPathHelper.GetDouble(root, config.JsonLivingAreaPath);
        var plot       = JsonPathHelper.GetDouble(root, config.JsonPlotAreaPath);
        var epc        = JsonPathHelper.GetString(root, config.JsonEpcPath);
        var zip        = JsonPathHelper.GetString(root, config.JsonZipCodePath);
        var city       = JsonPathHelper.GetString(root, config.JsonCityPath);

        // SourceUrl is the original HTML property page (strip the json-detail:// prefix)
        var sourceUrl = page.Url["json-detail://".Length..];

        bool isSold        = false;
        bool isUnderOption = false;
        if (!string.IsNullOrEmpty(config.JsonStatusPath))
        {
            var status = JsonPathHelper.GetString(root, config.JsonStatusPath) ?? "";
            if (!string.IsNullOrEmpty(config.JsonSoldValue))
                isSold = status.Equals(config.JsonSoldValue, StringComparison.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(config.JsonUnderOptionValue))
                isUnderOption = status.Equals(config.JsonUnderOptionValue, StringComparison.OrdinalIgnoreCase);
        }

        return new Property
        {
            SourceUrl    = sourceUrl,
            SourceDomain = config.Agency?.AgencyDomain ?? string.Empty,
            ExternalId   = externalId,
            Title        = title,
            Description  = desc,
            Price        = price,
            ZipCode      = zip,
            City         = city,
            Bedrooms     = bedrooms,
            LivingArea   = living,
            PlotArea     = plot,
            ImageUrl     = image,
            EpcScore     = epc,
            Sold         = isSold,
            UnderOption  = isUnderOption
        };
    }
}
