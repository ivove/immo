using HtmlAgilityPack;
using Immo.Data;
using Immo.Data.Entities;
using System.Text.RegularExpressions;

namespace Immo.Crawler.Extractors;

public class GeneralLinkExtractor : ILinkExtractorStrategy
{
    private readonly ImmoContext _context;

    public GeneralLinkExtractor(ImmoContext context)
    {
        _context = context;
    }

    public bool CanExtract(string url)
    {
        return true;
    }

    public IEnumerable<string> ExtractLinks(string htmlContent, string baseUrl,string agencyUrl="")
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
            if (!string.IsNullOrWhiteSpace(href) && CheckUrl(href,agencyUrl)) 
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

    private bool CheckUrl(string href,string baseUrl){

        var agency = _context.Agencies.FirstOrDefault(a => a.AgencyDomain == baseUrl);
        if (agency == null) return false;

        var agencyListingChecks = _context.AgencyListingChecks.Where(a => a.AgencyId == agency.Id).ToList();
        if (agencyListingChecks.Count == 0) return false;

        bool result = false;

        foreach (var agencyListingCheck in agencyListingChecks)
        {
            result = false;
            foreach (var urlPossibility in agencyListingCheck.UrlPosibilities)
            {
                if (href.Contains(urlPossibility))
                {
                    result = true;
                    break;
                }
            }
            if (!result){
                break;
            }
        }

        return result;
    }
}
