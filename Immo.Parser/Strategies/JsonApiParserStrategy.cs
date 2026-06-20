using System.Text.Json;
using HtmlAgilityPack;
using Immo.Data;
using Immo.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Immo.Parser.Strategies;

/// <summary>
/// Parses a <see cref="RawPage"/> whose <c>HtmlContent</c> field contains a raw JSON payload
/// returned by an agency's listing API.
/// <para>
/// Each element of the configured JSON array is mapped to a <see cref="Property"/> using
/// dot-notation paths stored in <see cref="ParserConfig"/>. A single <see cref="RawPage"/>
/// therefore yields <em>N</em> properties (one per array element).
/// </para>
/// </summary>
public class JsonApiParserStrategy : IMultiPropertyParserStrategy
{
    private readonly ImmoContext _context;
    private readonly ILogger<JsonApiParserStrategy> _logger;

    public JsonApiParserStrategy(ImmoContext context, ILogger<JsonApiParserStrategy> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Recognises pages that were crawled from a JSON API endpoint.
    /// The crawler stores these with a <c>json-api://</c> URL scheme prefix.
    /// </summary>
    public bool CanParse(string url) => url.StartsWith("json-api://", StringComparison.OrdinalIgnoreCase);

    /// <summary>Not used for JSON API pages; call <see cref="ParseMany"/> instead.</summary>
    public Property Parse(RawPage page, HtmlDocument? document) =>
        throw new NotSupportedException("JsonApiParserStrategy does not support single-item parsing. Use ParseMany.");

    public IEnumerable<Property> ParseMany(RawPage page)
    {
        var config = ResolveConfig(page);
        if (config is null) yield break;

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(page.HtmlContent);
        }
        catch (JsonException)
        {
            yield break;
        }

        var typeFilter = string.IsNullOrWhiteSpace(config.JsonTypeFilterValues)
            ? null
            : config.JsonTypeFilterValues
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        using (doc)
        {
            foreach (var item in JsonPathHelper.GetArray(doc.RootElement, config.JsonArrayPath))
            {
                if (typeFilter is not null && !string.IsNullOrEmpty(config.JsonTypeFilterPath))
                {
                    var itemType = JsonPathHelper.GetString(item, config.JsonTypeFilterPath);
                    if (itemType is null || !typeFilter.Contains(itemType))
                        continue;
                }

                var property = MapItem(item, config, page);
                if (property is not null) yield return property;
            }
        }
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private ParserConfig? ResolveConfig(RawPage page)
    {
        if (page.AgencyId.HasValue)
        {
            return _context.ParserConfigs
                .Include(c => c.Agency)
                .FirstOrDefault(c => c.AgencyId == page.AgencyId.Value);
        }

        // Fallback: strip the json-api:// prefix and match by domain
        var actualUrl = page.Url["json-api://".Length..];
        if (Uri.TryCreate(actualUrl, UriKind.Absolute, out var uri))
        {
            var domain = uri.Host.Replace("www.", "");
            return _context.ParserConfigs
                .Include(c => c.Agency)
                .FirstOrDefault(c => c.Agency != null && c.Agency.AgencyDomain.Contains(domain));
        }

        return null;
    }

    private Property? MapItem(JsonElement item, ParserConfig config, RawPage page)
    {
        var externalId = JsonPathHelper.GetString(item, config.JsonExternalIdPath);
        var title      = JsonPathHelper.GetString(item, config.JsonTitlePath);
        var price      = JsonPathHelper.GetDecimal(item, config.JsonPricePath);
        var desc       = JsonPathHelper.GetString(item, config.JsonDescriptionPath);
        var image      = JsonPathHelper.GetString(item, config.JsonImagePath);
        var bedrooms   = JsonPathHelper.GetInt(item, config.JsonBedroomsPath);
        var living     = JsonPathHelper.GetDouble(item, config.JsonLivingAreaPath);
        var plot       = JsonPathHelper.GetDouble(item, config.JsonPlotAreaPath);
        var epc        = JsonPathHelper.GetString(item, config.JsonEpcPath);
        var zip        = JsonPathHelper.GetString(item, config.JsonZipCodePath);
        var city       = JsonPathHelper.GetString(item, config.JsonCityPath);
        string? sourceUrl;
        if (!string.IsNullOrEmpty(config.JsonDetailUrlTemplate))
        {
            if (string.IsNullOrEmpty(externalId))
            {
                _logger.LogWarning("Skipping item: JsonDetailUrlTemplate is set but no external ID was found at '{Path}'", config.JsonExternalIdPath);
                return null;
            }
            sourceUrl = config.JsonDetailUrlTemplate
                .Replace("{id}", externalId)
                .Replace("{externalId}", externalId)
                .Replace("{title}", title)
                .Replace("{city}", city)
                .Replace("{postalCode}", zip)
                .Replace("{zip}", zip);
        }
        else
            sourceUrl = JsonPathHelper.GetString(item, config.JsonUrlPath) ?? page.Url;

        bool isSold        = false;
        bool isUnderOption = false;
        if (!string.IsNullOrEmpty(config.JsonStatusPath))
        {
            var status = JsonPathHelper.GetString(item, config.JsonStatusPath) ?? "";
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
