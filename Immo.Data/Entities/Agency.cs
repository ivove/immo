namespace Immo.Data.Entities;

public class Agency
{
    public int Id { get; set; }
    public required string AgencyDomain { get; set; }
    public List<AgencyListingCheck> AgencyListingChecks { get; set; } = [];
    public string? PaginationSelector { get; set; }
    public ParserConfig? ParserConfig { get; set; }

    /// <summary>
    /// When set, the crawler worker will run an immediate crawl for this agency on its next tick.
    /// Cleared by the worker after crawl is initiated.
    /// </summary>
    public DateTime? CrawlRequestedAt { get; set; }

    public bool IsSuspended { get; set; }

    /// <summary>
    /// "html" (default) or "json_api". When "json_api", the crawler fetches
    /// <see cref="ApiListingUrl"/> as a JSON endpoint instead of scraping HTML.
    /// </summary>
    public string? DataSourceType { get; set; }

    /// <summary>
    /// Full URL of the JSON listing API endpoint.
    /// Required when <see cref="DataSourceType"/> is "json_api".
    /// e.g. https://api.agency.com/v1/properties
    /// </summary>
    public string? ApiListingUrl { get; set; }

    public string? Notes { get; set; }

    public List<RawPage> RawPages { get; set; } = [];
    public List<Property> Properties { get; set; } = [];
}