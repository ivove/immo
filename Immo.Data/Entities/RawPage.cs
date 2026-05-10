namespace Immo.Data.Entities;

public class RawPage
{
    public int Id { get; set; }
    public required string Url { get; set; }
    public required string HtmlContent { get; set; }
    public string? ContentHash { get; set; }
    public DateTime CrawledAt { get; set; }
    public bool IsParsed { get; set; }
}
