namespace Immo.Data.Entities;

/// <summary>
/// Singleton settings row — always Id = 1. Use AppSettingsService to read/write.
/// </summary>
public class AppSettings
{
    public int Id { get; set; } = 1;

    /// <summary>Number of days before a crawled page is eligible for re-crawling.</summary>
    public int RecrawlAfterDays { get; set; } = 3;

    /// <summary>How many hours the crawler worker should wait between complete cycles.</summary>
    public int CrawlIntervalHours { get; set; } = 4;

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

    /// <summary>
    /// Preferred timezone for displaying dates and times across the system.
    /// Defaults to UTC.
    /// </summary>
    public string PreferredTimezone { get; set; } = "UTC";

    /// <summary>
    /// The threshold in days during which a property is highlighted as "New" or "Updated".
    /// </summary>
    public int NewOrUpdatedThresholdDays { get; set; } = 3;

    // --------------- Email / SMTP settings ---------------

    /// <summary>
    /// SMTP host used for sending email notifications.
    /// </summary>
    public string SmtpHost { get; set; } = string.Empty;

    /// <summary>
    /// SMTP port.
    /// </summary>
    public int SmtpPort { get; set; } = 25;

    /// <summary>
    /// Username for SMTP authentication (optional).
    /// </summary>
    public string SmtpUsername { get; set; } = string.Empty;

    /// <summary>
    /// Password for SMTP authentication (optional).
    /// </summary>
    public string SmtpPassword { get; set; } = string.Empty;

    /// <summary>
    /// From email address used when sending notifications.
    /// </summary>
    public string FromEmail { get; set; } = string.Empty;

    /// <summary>
    /// Whether to use SSL/TLS for SMTP connections.
    /// </summary>
    public bool SmtpUseSsl { get; set; } = true;

    // --------------- Helpers ---------------

    public IEnumerable<string> GetSoldKeywords() =>
        SoldKeywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public IEnumerable<string> GetUnderOptionKeywords() =>
        UnderOptionKeywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
