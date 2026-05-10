namespace Immo.Data.Entities;

public class Agency
{
    public int Id { get; set; }
    public required string AgencyDomain { get; set; }
    public List<AgencyListingCheck> AgencyListingChecks { get; set; } = [];
    public string? PaginationSelector { get; set; }
    public ParserConfig? ParserConfig { get; set; }
}