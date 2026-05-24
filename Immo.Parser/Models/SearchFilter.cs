using System.Collections.Generic;

namespace Immo.Parser.Models;

public class SearchFilter
{
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public List<string>? ZipCodes { get; set; }
    public int? MinBedrooms { get; set; }
    public double? MinLivingArea { get; set; }
    public double? MinPlotArea { get; set; }
    public string? MaxEpc { get; set; }
    /// <summary>
    /// "available" | "sold" | "under_option" | "all"
    /// </summary>
    public string? Status { get; set; } = "available";
    /// <summary>
    /// "new" | "updated" | "new_or_updated" | null
    /// </summary>
    public string? Recency { get; set; }
}
