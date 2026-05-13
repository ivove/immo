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
    
    public string? Notes { get; set; }
}