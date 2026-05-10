namespace Immo.Data.Entities;

/// <summary>
/// Singleton settings row — always Id = 1. Use AppSettingsService to read/write.
/// </summary>
public class AppSettings
{
    public int Id { get; set; } = 1;

    /// <summary>Number of days before a crawled page is eligible for re-crawling.</summary>
    public int RecrawlAfterDays { get; set; } = 3;

    /// <summary>
    /// Comma-separated list of words/phrases that indicate a property is sold.
    /// E.g. "verkocht,sold"
    /// </summary>
    public string SoldKeywords { get; set; } = "verkocht,sold";

    /// <summary>
    /// Comma-separated list of words/phrases that indicate a property is under option.
    /// E.g. "onder optie,optie"
    /// </summary>
    public string UnderOptionKeywords { get; set; } = "onder optie,optie";

    // --------------- Helpers ---------------

    public IEnumerable<string> GetSoldKeywords() =>
        SoldKeywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public IEnumerable<string> GetUnderOptionKeywords() =>
        UnderOptionKeywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
