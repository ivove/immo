namespace Immo.Crawler.Extractors;

public interface ILinkExtractorStrategy
{
    bool CanExtract(string url);
    IEnumerable<string> ExtractLinks(string htmlContent, string baseUrl,string agencyUrl="");
}
