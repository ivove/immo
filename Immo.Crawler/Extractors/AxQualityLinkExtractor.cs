using HtmlAgilityPack;
using System.Text.RegularExpressions;

namespace Immo.Crawler.Extractors;

public class AxQualityLinkExtractor : ILinkExtractorStrategy
{
    public bool CanExtract(string url)
    {
        return url.Contains("ax-quality.be");
    }

    public IEnumerable<string> ExtractLinks(string htmlContent, string baseUrl)
    {
        var document = new HtmlDocument();
        document.LoadHtml(htmlContent);

        var links = new List<string>();

        // This is a generic approach to find links that might be properties.
        // A real implementation would use specific CSS selectors, e.g., "//a[contains(@class, 'property-link')]"
        var anchorNodes = document.DocumentNode.SelectNodes("//a[@href]");
        if (anchorNodes == null) return links;

        foreach (var node in anchorNodes)
        {
            var href = node.GetAttributeValue("href", "");
            
            // Basic filtering to find property links. Adjust based on actual site structure.
            // Assuming property pages look like /nl/pand/12345 or similar.
            if (!string.IsNullOrWhiteSpace(href) && href.Contains("/tekoop/")) 
            {
                // Handle relative vs absolute URLs
                if (!href.StartsWith("http"))
                {
                    var baseUri = new Uri(baseUrl);
                    href = new Uri(baseUri, href).ToString();
                }
                
                links.Add(href);
            }
        }

        // Return unique links
        return links.Distinct();
    }
}
