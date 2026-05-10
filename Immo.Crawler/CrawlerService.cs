using System.Security.Cryptography;
using System.Text;
using Immo.Data;
using Immo.Data.Entities;
using Immo.Crawler.Extractors;
using Microsoft.Extensions.Logging;
using HtmlAgilityPack;

namespace Immo.Crawler;

public class CrawlerService
{

    private readonly HttpClient _httpClient;
    private readonly ImmoContext _context;
    private readonly IEnumerable<ILinkExtractorStrategy> _extractors;
    private readonly ILogger<CrawlerService> _logger;

    public CrawlerService(HttpClient httpClient, ImmoContext context, IEnumerable<ILinkExtractorStrategy> extractors, ILogger<CrawlerService> logger)
    {
        _httpClient = httpClient;
        _context = context;
        _extractors = extractors;
        _logger = logger;
    }

    private static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
    }

    private readonly HashSet<string> _crawledPropertyUrls = new();

    public async Task CrawlPageAsync(string url)
    {
        _crawledPropertyUrls.Add(url);
        try
        {
            var existing = _context.RawPages.FirstOrDefault(p => p.Url == url);

            if (existing != null)
            {
                var settings = _context.AppSettings.FirstOrDefault() ?? new AppSettings();
                var age = DateTime.UtcNow - existing.CrawledAt;
                if (age.TotalDays < settings.RecrawlAfterDays)
                {
                    _logger.LogInformation("Skipping {Url} — crawled {Hours:F0}h ago (recrawl after {Days}d)", url, age.TotalHours, settings.RecrawlAfterDays);
                    return;
                }

                _logger.LogInformation("Recrawling {Url} — last crawled {Days:F1} days ago", url, age.TotalDays);
                await FetchAndRefreshAsync(url, existing);
            }
            else
            {
                _logger.LogInformation("Crawling new page {Url}...", url);
                await FetchAndSaveAsync(url);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crawling {Url}", url);
        }
    }

    private async Task<HttpResponseMessage> FetchResponseAsync(string url)
    {
        var currentUrl = url;
        var maxRedirects = 10;
        var redirectCount = 0;

        while (true)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, currentUrl);
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            if ((int)response.StatusCode >= 300 && (int)response.StatusCode <= 399)
            {
                redirectCount++;
                if (redirectCount > maxRedirects)
                    throw new Exception($"Too many redirects for {url}");

                var location = response.Headers.Location;
                if (location == null) return response; // No location header, return as is

                if (!location.IsAbsoluteUri)
                {
                    location = new Uri(new Uri(currentUrl), location);
                }

                _logger.LogInformation("Manual redirect follow: {From} -> {To}", currentUrl, location);
                currentUrl = location.ToString();
                continue;
            }

            return response;
        }
    }

    private async Task FetchAndSaveAsync(string url)
    {
        await ApplyRateLimit();

        using var response = await FetchResponseAsync(url);
        response.EnsureSuccessStatusCode();

        if (response.RequestMessage?.RequestUri != null && response.RequestMessage.RequestUri.ToString() != url)
        {
            _logger.LogInformation("Redirect followed: {OriginalUrl} -> {FinalUrl}", url, response.RequestMessage.RequestUri);
        }

        var htmlContent = await response.Content.ReadAsStringAsync();
        var hash = ComputeHash(htmlContent);

        var rawPage = new RawPage
        {
            Url = url,
            HtmlContent = htmlContent,
            ContentHash = hash,
            CrawledAt = DateTime.UtcNow,
            IsParsed = false
        };

        _context.RawPages.Add(rawPage);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Saved new page {Url} (hash: {Hash})", url, hash[..8]);
    }

    private async Task FetchAndRefreshAsync(string url, RawPage existing)
    {
        await ApplyRateLimit();

        using var response = await FetchResponseAsync(url);
        
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogInformation("Page {Url} returned 404 Not Found. Marking associated property as sold.", url);
            var property = _context.Properties.FirstOrDefault(p => p.RawPageId == existing.Id);
            if (property != null && !property.Sold)
            {
                property.Sold = true;
                await _context.SaveChangesAsync();
            }
            return;
        }

        response.EnsureSuccessStatusCode();

        if (response.RequestMessage?.RequestUri != null && response.RequestMessage.RequestUri.ToString() != url)
        {
            _logger.LogInformation("Redirect followed during refresh: {OriginalUrl} -> {FinalUrl}", url, response.RequestMessage.RequestUri);
        }

        var htmlContent = await response.Content.ReadAsStringAsync();
        var newHash = ComputeHash(htmlContent);

        if (newHash == existing.ContentHash)
        {
            // Content unchanged — just bump the crawl timestamp
            existing.CrawledAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Content unchanged for {Url} — timestamp updated", url);
        }
        else
        {
            // Content changed — refresh content and queue for re-parsing
            _logger.LogInformation("Content changed for {Url} — refreshing and marking for re-parse", url);
            existing.HtmlContent = htmlContent;
            existing.ContentHash = newHash;
            existing.CrawledAt = DateTime.UtcNow;
            existing.IsParsed = false;
            await _context.SaveChangesAsync();
        }
    }

    private async Task ApplyRateLimit()
    {
        var delayMs = Random.Shared.Next(2000, 5000);
        _logger.LogInformation("Rate limiting: waiting {Delay}ms...", delayMs);
        await Task.Delay(delayMs);
    }

    private readonly HashSet<string> _visitedListingUrls = new();

    public async Task CrawlListingPageAsync(string listingUrl)
    {
        if (_visitedListingUrls.Contains(listingUrl)) return;
        _visitedListingUrls.Add(listingUrl);

        try
        {
            _logger.LogInformation("Crawling LISTING page: {Url}...", listingUrl);

            await ApplyRateLimit();

            using var response = await FetchResponseAsync(listingUrl);
            response.EnsureSuccessStatusCode();

            var htmlContent = await response.Content.ReadAsStringAsync();

            var extractor = _extractors.FirstOrDefault(e => e.CanExtract(listingUrl));
            if (extractor == null)
            {
                _logger.LogWarning("No link extractor found for domain: {Url}", listingUrl);
                return;
            }

            var propertyLinks = extractor.ExtractLinks(htmlContent, listingUrl).ToList();
            _logger.LogInformation("Found {Count} property links on {Url}", propertyLinks.Count, listingUrl);

            var count = 0;
            foreach (var link in propertyLinks)
            {
                if (count > 15) break; // Limit for testing/safety
                await CrawlPageAsync(link);
                count++;
            }

            // Pagination logic
            var uri = new Uri(listingUrl);
            var domain = uri.Host.Replace("www.", "");
            var agency = _context.Agencies.FirstOrDefault(a => a.AgencyDomain.Contains(domain));

            if (agency?.PaginationSelector != null)
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(htmlContent);
                var paginationNodes = doc.DocumentNode.SelectNodes(agency.PaginationSelector);
                
                if (paginationNodes != null)
                {
                    var paginationLinks = paginationNodes
                        .Select(n => n.GetAttributeValue("href", ""))
                        .Where(href => !string.IsNullOrEmpty(href))
                        .Select(href => new Uri(new Uri(listingUrl), href).ToString())
                        .Distinct()
                        .ToList();

                    _logger.LogInformation("Found {Count} pagination links on {Url}", paginationLinks.Count, listingUrl);

                    foreach (var pLink in paginationLinks)
                    {
                        await CrawlListingPageAsync(pLink);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crawling listing page {Url}", listingUrl);
        }
    }

    public async Task CheckUnseenPagesAsync()
    {
        _logger.LogInformation("Checking existing pages that were not seen during this crawl...");
        
        // Fetch only URLs to minimize memory usage, but we need the RawPageId for Properties
        var allPages = _context.RawPages.Select(p => new { p.Id, p.Url }).ToList();
        var unseenPages = allPages.Where(p => !_crawledPropertyUrls.Contains(p.Url)).ToList();
        
        _logger.LogInformation("Found {Count} unseen pages to verify.", unseenPages.Count);
        
        foreach (var page in unseenPages)
        {
            try
            {
                var property = _context.Properties.FirstOrDefault(p => p.RawPageId == page.Id);
                if (property != null && property.Sold)
                {
                    continue; // Already sold
                }

                await ApplyRateLimit();
                using var request = new HttpRequestMessage(HttpMethod.Get, page.Url);
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("Unseen page {Url} returned 404. Marking as sold.", page.Url);
                    if (property != null)
                    {
                        property.Sold = true;
                        await _context.SaveChangesAsync();
                    }
                }
                else
                {
                    _logger.LogInformation("Unseen page {Url} still exists (Status: {StatusCode}).", page.Url, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking unseen page {Url}", page.Url);
            }
        }
    }
}
