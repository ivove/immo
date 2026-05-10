using System;

namespace Immo.Data.Entities;

public class LogEntry
{
    public int Id { get; set; }
    public string? Timestamp { get; set; }
    public string? Level { get; set; }
    public string? Exception { get; set; }
    public string? RenderedMessage { get; set; }
    public string? Properties { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? SourceContext 
    {
        get 
        {
            if (string.IsNullOrEmpty(Properties)) return null;
            try {
                using var doc = System.Text.Json.JsonDocument.Parse(Properties);
                if (doc.RootElement.TryGetProperty("SourceContext", out var sourceElement))
                    return sourceElement.GetString();
            } catch {}
            return null;
        }
    }
}
