using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Immo.Data;
using Immo.Data.Entities;

namespace Immo.Web.Controllers;

public class AgenciesController : Controller
{
    private readonly ImmoContext _context;
    private readonly ILogger<AgenciesController> _logger;

    public AgenciesController(ImmoContext context, ILogger<AgenciesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: Agencies
    public async Task<IActionResult> Index()
    {
        _logger.LogInformation("Retrieving list of all agencies and statistics.");
        var agencies = await _context.Agencies
            .Include(a => a.AgencyListingChecks)
            .Include(a => a.ParserConfig)
            .ToListAsync();

        var stats = await _context.Agencies
            .Select(a => new {
                AgencyId = a.Id,
                PagesCount = a.RawPages.Count(),
                AvailablePropertiesCount = a.Properties.Count(p => !p.Sold)
            })
            .ToDictionaryAsync(
                x => x.AgencyId, 
                x => new AgencyStatsViewModel { PagesCount = x.PagesCount, AvailablePropertiesCount = x.AvailablePropertiesCount }
            );

        ViewBag.Stats = stats;

        return View(agencies);
    }

    public class AgencyStatsViewModel 
    {
        public int PagesCount { get; set; }
        public int AvailablePropertiesCount { get; set; }
    }

    // GET: Agencies/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        _logger.LogInformation("Displaying details for Agency ID: {Id}", id);
        var agency = await _context.Agencies
            .Include(a => a.AgencyListingChecks)
            .Include(a => a.ParserConfig)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (agency == null)
        {
            _logger.LogWarning("Agency details requested for non-existent Agency ID: {Id}", id);
            return NotFound();
        }

        return View(agency);
    }

    // GET: Agencies/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: Agencies/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,AgencyDomain,PaginationSelector,DataSourceType,ApiListingUrl,IsSuspended,Notes")] Agency agency)
    {
        if (ModelState.IsValid)
        {
            _context.Add(agency);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Successfully created new agency {AgencyDomain} (ID: {Id}). Notes: {Notes}", agency.AgencyDomain, agency.Id, agency.Notes);
            return RedirectToAction(nameof(Index));
        }
        _logger.LogWarning("Failed to create agency {AgencyDomain} due to validation errors.", agency.AgencyDomain);
        return View(agency);
    }

    // GET: Agencies/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var agency = await _context.Agencies
            .Include(a => a.AgencyListingChecks)
            .Include(a => a.ParserConfig)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (agency == null) return NotFound();
        return View(agency);
    }

    // POST: Agencies/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,AgencyDomain,PaginationSelector,DataSourceType,ApiListingUrl,IsSuspended,Notes")] Agency agency)
    {
        if (id != agency.Id) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                _logger.LogInformation("Saving updates for agency {AgencyDomain} (ID: {Id}). Notes: {Notes}, Suspended: {IsSuspended}", agency.AgencyDomain, agency.Id, agency.Notes, agency.IsSuspended);
                _context.Update(agency);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (!AgencyExists(agency.Id))
                {
                    _logger.LogWarning("Concurrency error: Agency ID {Id} no longer exists during update.", agency.Id);
                    return NotFound();
                }
                else
                {
                    _logger.LogError(ex, "Concurrency database update error occurred while editing Agency ID {Id}.", agency.Id);
                    throw;
                }
            }
            return RedirectToAction(nameof(Index));
        }
        _logger.LogWarning("Failed to save updates for agency {AgencyDomain} (ID: {Id}) due to validation errors.", agency.AgencyDomain, agency.Id);
        return View(agency);
    }

    // GET: Agencies/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var agency = await _context.Agencies
            .FirstOrDefaultAsync(m => m.Id == id);
        if (agency == null) return NotFound();

        return View(agency);
    }

    // POST: Agencies/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var agency = await _context.Agencies.FindAsync(id);
        if (agency == null)
        {
            _logger.LogWarning("Delete request received for non-existent Agency ID {Id}.", id);
            return NotFound();
        }

        _logger.LogInformation("Initiating full deletion of agency {AgencyDomain} (ID: {Id}) and all of its associated pages and properties.", agency.AgencyDomain, agency.Id);
        await PurgeAgencyDataAsync(agency.Id);

        _context.Agencies.Remove(agency);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Successfully deleted agency {AgencyDomain} (ID: {Id}) from database.", agency.AgencyDomain, agency.Id);
        TempData["SuccessMessage"] = $"Agency \u0022{agency.AgencyDomain}\u0022 and all its crawled data have been deleted.";
        return RedirectToAction(nameof(Index));
    }

    // POST: Agencies/PurgeData/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PurgeData(int id)
    {
        var agency = await _context.Agencies.FindAsync(id);
        if (agency == null)
        {
            _logger.LogWarning("Purge data request received for non-existent Agency ID {Id}.", id);
            return NotFound();
        }

        _logger.LogInformation("Purging crawled data (pages/properties) for agency {AgencyDomain} (ID: {Id}).", agency.AgencyDomain, agency.Id);
        var (pages, properties) = await PurgeAgencyDataAsync(agency.Id);

        _logger.LogInformation("Successfully purged {Properties} properties and {Pages} raw pages for agency {AgencyDomain} (ID: {Id}).", properties, pages, agency.AgencyDomain, agency.Id);
        TempData["SuccessMessage"] = $"Purged {properties} propert{(properties == 1 ? "y" : "ies")} and {pages} raw page{(pages == 1 ? "" : "s")} for \u0022{agency.AgencyDomain}\u0022.";
        return RedirectToAction(nameof(Index));
    }

    // POST: Agencies/ReparseData/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReparseData(int id)
    {
        var agency = await _context.Agencies.FindAsync(id);
        if (agency == null)
        {
            _logger.LogWarning("Reparse request received for non-existent Agency ID {Id}.", id);
            return NotFound();
        }

        _logger.LogInformation("Queuing parsed pages for reparsing for agency {AgencyDomain} (ID: {Id}).", agency.AgencyDomain, agency.Id);
        var count = await ReparseAgencyDataAsync(agency.Id);

        _logger.LogInformation("Successfully queued {Count} pages for reparsing for agency {AgencyDomain} (ID: {Id}).", count, agency.AgencyDomain, agency.Id);
        TempData["SuccessMessage"] = $"Queued {count} page{(count == 1 ? "" : "s")} for reparsing for \u0022{agency.AgencyDomain}\u0022. The parser will process them shortly.";
        return RedirectToAction(nameof(Index));
    }

    // POST: Agencies/PurgeOrphanedProperties
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PurgeOrphanedProperties()
    {
        var agencyIds = await _context.Agencies.Select(a => a.Id).ToListAsync();

        var orphanedProperties = await _context.Properties
            .Where(p => p.AgencyId == null || !agencyIds.Contains(p.AgencyId.Value))
            .ToListAsync();

        if (orphanedProperties.Count > 0)
        {
            _context.Properties.RemoveRange(orphanedProperties);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Removed {orphanedProperties.Count} orphaned propert{(orphanedProperties.Count == 1 ? "y" : "ies")}.";
        }
        else
        {
            TempData["SuccessMessage"] = "No orphaned properties found.";
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: Agencies/FixAgencyReferences
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FixAgencyReferences()
    {
        var agencies = await _context.Agencies.ToListAsync();
        int rawPagesFixed = 0;
        int propertiesFixed = 0;

        var rawPagesToFix = await _context.RawPages.Where(p => p.AgencyId == null).ToListAsync();
        foreach (var page in rawPagesToFix)
        {
            if (Uri.TryCreate(page.Url, UriKind.Absolute, out var uri))
            {
                var domain = uri.Host.Replace("www.", "");
                var agency = agencies.FirstOrDefault(a => a.AgencyDomain.Contains(domain));
                if (agency != null)
                {
                    page.AgencyId = agency.Id;
                    rawPagesFixed++;
                }
            }
        }

        var propertiesToFix = await _context.Properties.Where(p => p.AgencyId == null).ToListAsync();
        foreach (var property in propertiesToFix)
        {
            if (Uri.TryCreate(property.SourceUrl, UriKind.Absolute, out var uri))
            {
                var domain = uri.Host.Replace("www.", "");
                var agency = agencies.FirstOrDefault(a => a.AgencyDomain.Contains(domain));
                if (agency != null)
                {
                    property.AgencyId = agency.Id;
                    propertiesFixed++;
                }
            }
        }

        if (rawPagesFixed > 0 || propertiesFixed > 0)
        {
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Fixed agency references for {rawPagesFixed} raw pages and {propertiesFixed} properties.";
        }
        else
        {
            TempData["SuccessMessage"] = "All raw pages and properties already have their agency references set correctly.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<(int pages, int properties)> PurgeAgencyDataAsync(int agencyId)
    {
        var rawPages = await _context.RawPages
            .Where(p => p.AgencyId == agencyId)
            .ToListAsync();

        var properties = await _context.Properties
            .Where(p => p.AgencyId == agencyId)
            .ToListAsync();

        _context.Properties.RemoveRange(properties);
        _context.RawPages.RemoveRange(rawPages);
        await _context.SaveChangesAsync();

        return (rawPages.Count, properties.Count);
    }

    /// <summary>Resets IsParsed to false for all RawPages belonging to the agency.</summary>
    private async Task<int> ReparseAgencyDataAsync(int agencyId)
    {
        var rawPages = await _context.RawPages
            .Where(p => p.AgencyId == agencyId)
            .ToListAsync();

        foreach (var page in rawPages)
        {
            page.IsParsed = false;
        }

        await _context.SaveChangesAsync();
        return rawPages.Count;
    }

    // GET: Agencies/Debug/5
    public async Task<IActionResult> Debug(int? agencyId)
    {
        if (agencyId == null) return NotFound();

        var agency = await _context.Agencies.FindAsync(agencyId);
        if (agency == null) return NotFound();

        ViewBag.AgencyId = agency.Id;
        ViewBag.AgencyDomain = agency.AgencyDomain;
        return View();
    }

    // POST: Agencies/Debug
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Debug(int agencyId, string urlToTest)
    {
        var agency = await _context.Agencies
            .Include(a => a.AgencyListingChecks)
            .Include(a => a.ParserConfig)
            .FirstOrDefaultAsync(a => a.Id == agencyId);

        if (agency == null) return NotFound();

        ViewBag.AgencyId = agency.Id;
        ViewBag.AgencyDomain = agency.AgencyDomain;
        ViewBag.TestedUrl = urlToTest;
        ViewBag.DataSourceType = agency.DataSourceType;

        if (string.IsNullOrWhiteSpace(urlToTest))
        {
            ModelState.AddModelError("", "URL is required.");
            return View();
        }

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            var response = await client.GetAsync(urlToTest);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            ViewBag.RawHtmlLength = content.Length;

            if (agency.DataSourceType == "json_api")
            {
                var rawPage = new Immo.Data.Entities.RawPage
                {
                    Url = "json-api://" + urlToTest,
                    HtmlContent = content,
                    AgencyId = agency.Id,
                    CrawledAt = DateTime.UtcNow
                };
                var parser = new Immo.Parser.Strategies.JsonApiParserStrategy(_context);
                ViewBag.ParsedProperties = parser.ParseMany(rawPage).ToList();
            }
            else
            {
                var doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(content);

                // 1. Pagination Extraction
                var paginationLinks = new List<string>();
                if (!string.IsNullOrEmpty(agency.PaginationSelector))
                {
                    var nodes = doc.DocumentNode.SelectNodes(agency.PaginationSelector);
                    if (nodes != null)
                    {
                        paginationLinks = nodes.Select(n => n.GetAttributeValue("href", ""))
                                               .Where(href => !string.IsNullOrEmpty(href))
                                               .Select(href => new Uri(new Uri(urlToTest), href).ToString())
                                               .Distinct()
                                               .ToList();
                    }
                }
                ViewBag.PaginationLinks = paginationLinks;

                // 2. Property Link Extraction
                var linkExtractor = new Immo.Crawler.Extractors.GeneralLinkExtractor(_context);
                ViewBag.PropertyLinks = linkExtractor.ExtractLinks(content, urlToTest, agency.AgencyDomain).ToList();

                // 3. Parser Strategy
                var rawPage = new Immo.Data.Entities.RawPage { Url = urlToTest, HtmlContent = content, AgencyId = agency.Id, CrawledAt = DateTime.UtcNow };
                var parser = new Immo.Parser.Strategies.ConfigurableParserStrategy(_context);
                ViewBag.ParsedProperty = parser.Parse(rawPage, doc);
            }
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", "Error fetching or parsing URL: " + ex.Message);
        }

        return View();
    }

    // --- AgencyListingCheck Management ---

    // GET: Agencies/AddCheck/5
    public IActionResult AddCheck(int agencyId)
    {
        ViewBag.AgencyId = agencyId;
        return View();
    }

    // POST: Agencies/AddCheck
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCheck(int agencyId, string possibilitiesRaw)
    {
        if (string.IsNullOrWhiteSpace(possibilitiesRaw))
        {
            ModelState.AddModelError("", "Possibilities are required.");
            ViewBag.AgencyId = agencyId;
            return View();
        }

        var possibilities = possibilitiesRaw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                           .Select(s => s.Trim())
                                           .ToList();

        var check = new AgencyListingCheck
        {
            AgencyId = agencyId,
            UrlPosibilities = possibilities
        };

        _context.AgencyListingChecks.Add(check);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Edit), new { id = agencyId });
    }

    // POST: Agencies/DeleteCheck/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCheck(int id)
    {
        var check = await _context.AgencyListingChecks.FindAsync(id);
        if (check == null) return NotFound();

        var agencyId = check.AgencyId;
        _context.AgencyListingChecks.Remove(check);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Edit), new { id = agencyId });
    }

    // GET: Agencies/EditParserConfig/5
    public async Task<IActionResult> EditParserConfig(int agencyId)
    {
        var agency = await _context.Agencies
            .Include(a => a.ParserConfig)
            .FirstOrDefaultAsync(a => a.Id == agencyId);
            
        if (agency == null) return NotFound();

        var config = agency.ParserConfig ?? new ParserConfig { AgencyId = agencyId };
        ViewData["DataSourceType"] = agency.DataSourceType;
        return View(config);
    }

    // POST: Agencies/EditParserConfig
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditParserConfig(ParserConfig config)
    {
        var isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        if (ModelState.IsValid)
        {
            if (config.Id == 0)
                _context.ParserConfigs.Add(config);
            else
                _context.Update(config);
            await _context.SaveChangesAsync();

            if (isAjax) return Json(new { success = true, configId = config.Id });
            return RedirectToAction(nameof(Edit), new { id = config.AgencyId });
        }

        if (isAjax)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage);
            return Json(new { success = false, errors });
        }

        var agency = await _context.Agencies.FindAsync(config.AgencyId);
        ViewData["DataSourceType"] = agency?.DataSourceType;
        return View(config);
    }

    // GET: Agencies/ParsePreview?agencyId=5&url=...
    public async Task<IActionResult> ParsePreview(int agencyId, string url)
    {
        var agency = await _context.Agencies
            .Include(a => a.ParserConfig)
            .FirstOrDefaultAsync(a => a.Id == agencyId);
        if (agency == null) return NotFound();

        if (string.IsNullOrWhiteSpace(url))
            return BadRequest("URL required");

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            client.Timeout = TimeSpan.FromSeconds(30);
            var html = await (await client.GetAsync(url)).Content.ReadAsStringAsync();

            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);

            var rawPage = new Immo.Data.Entities.RawPage { Url = url, HtmlContent = html, AgencyId = agencyId, CrawledAt = DateTime.UtcNow };
            var parser = new Immo.Parser.Strategies.ConfigurableParserStrategy(_context);
            var prop = parser.Parse(rawPage, doc);

            if (prop == null)
                return Json(new { error = "Parser returned no result. Check that a parser config exists for this agency." });

            return Json(new
            {
                title       = prop.Title,
                price       = prop.Price,
                city        = prop.City,
                zipCode     = prop.ZipCode,
                bedrooms    = prop.Bedrooms,
                livingArea  = prop.LivingArea,
                plotArea    = prop.PlotArea,
                epcScore    = prop.EpcScore,
                description = prop.Description != null && prop.Description.Length > 300
                                ? prop.Description[..300] + "…"
                                : prop.Description,
                imageUrl    = prop.ImageUrl,
                externalId  = prop.ExternalId,
                sold        = prop.Sold,
                underOption = prop.UnderOption
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ParsePreview failed for agency {AgencyId}, URL {Url}", agencyId, url);
            return Json(new { error = ex.Message });
        }
    }

    // GET: Agencies/VisualConfig/5
    public async Task<IActionResult> VisualConfig(int agencyId)
    {
        var agency = await _context.Agencies
            .Include(a => a.ParserConfig)
            .FirstOrDefaultAsync(a => a.Id == agencyId);
        if (agency == null) return NotFound();

        if (agency.DataSourceType == "json_api")
        {
            TempData["ErrorMessage"] = "Visual Config Builder is only available for HTML scraping agencies.";
            return RedirectToAction(nameof(Edit), new { id = agencyId });
        }

        var config = agency.ParserConfig ?? new ParserConfig { AgencyId = agencyId };
        ViewBag.AgencyDomain = agency.AgencyDomain;
        return View(config);
    }

    // GET: Agencies/ProxyPage
    public async Task<IActionResult> ProxyPage(int agencyId, string url)
    {
        var agency = await _context.Agencies.FindAsync(agencyId);
        if (agency == null) return NotFound();

        if (string.IsNullOrWhiteSpace(url)
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != "http" && uri.Scheme != "https"))
            return BadRequest("Invalid URL");

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            client.Timeout = TimeSpan.FromSeconds(30);
            var html = await (await client.GetAsync(url)).Content.ReadAsStringAsync();
            return Content(InjectVisualBuilderScript(html, url), "text/html");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "VisualConfig proxy failed for URL {Url}", url);
            var msg = System.Net.WebUtility.HtmlEncode(ex.Message);
            return Content($"<html><body style='font-family:sans-serif;padding:2rem'><h3>Failed to load page</h3><p>{msg}</p></body></html>", "text/html");
        }
    }

    private static string InjectVisualBuilderScript(string html, string pageUrl)
    {
        var origin = new Uri(pageUrl).GetLeftPart(UriPartial.Authority);

        // Remove any existing <base> tags
        html = System.Text.RegularExpressions.Regex.Replace(html, @"<base[^>]*?>", "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5));

        // Inject <base> pointing to the original origin so all relative assets resolve correctly
        var baseTag = $"""<base href="{origin}/">""";
        html = System.Text.RegularExpressions.Regex.Replace(html, @"<head(\s[^>]*)?>",
            m => m.Value + baseTag,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5));

        // Inject click-capture script before </body>
        var script = $"\n<script>\n{VisualPickerScript}\n</script>\n";
        var bodyClose = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        html = bodyClose >= 0 ? html.Insert(bodyClose, script) : html + script;

        return html;
    }

    private const string VisualPickerScript = """
        (function () {
            var s = document.createElement('style');
            s.textContent = '* { cursor: crosshair !important; user-select: none !important; } ' +
                '.immo-hover { outline: 2px dashed #4361ee !important; background: rgba(67,97,238,0.06) !important; } ' +
                '.immo-selected { outline: 3px solid #e63946 !important; background: rgba(230,57,70,0.09) !important; }';
            document.head.appendChild(s);

            var hovered = null;

            document.addEventListener('mouseover', function (e) {
                if (hovered) hovered.classList.remove('immo-hover');
                hovered = e.target;
                if (!hovered.classList.contains('immo-selected')) hovered.classList.add('immo-hover');
            });
            document.addEventListener('mouseout', function (e) { e.target.classList.remove('immo-hover'); });

            document.addEventListener('click', function (e) {
                e.preventDefault();
                e.stopImmediatePropagation();
                var el = e.target;
                var xpath = getXPath(el);
                var text = (el.innerText || el.textContent || '').replace(/\s+/g, ' ').trim().substring(0, 150);
                window.parent.postMessage({ type: 'immo:pick', xpath: xpath, text: text }, '*');
            }, true);

            window.addEventListener('message', function (e) {
                if (!e.data || e.data.type !== 'immo:highlight') return;
                document.querySelectorAll('.immo-selected').forEach(function (x) { x.classList.remove('immo-selected'); });
                if (!e.data.xpath) return;
                try {
                    var res = document.evaluate(e.data.xpath, document, null, XPathResult.ORDERED_NODE_SNAPSHOT_TYPE, null);
                    for (var i = 0; i < res.snapshotLength; i++) res.snapshotItem(i).classList.add('immo-selected');
                } catch (_) {}
            });

            function getXPath(el) {
                if (el.id) return '//' + el.tagName.toLowerCase() + '[@id=\'' + el.id.replace(/'/g, "\\'") + '\']';
                if (el.className && typeof el.className === 'string') {
                    var cls = el.className.trim().split(/\s+/).filter(function (c) { return c && !c.startsWith('immo-'); });
                    if (cls.length) {
                        var best = cls.slice().sort(function (a, b) { return b.length - a.length; })[0];
                        return '//' + el.tagName.toLowerCase() + '[contains(@class,\'' + best.replace(/'/g, "\\'") + '\')]';
                    }
                }
                return buildPath(el);
            }

            function buildPath(el) {
                var parts = [], cur = el;
                while (cur && cur.nodeType === 1 && cur !== document.documentElement) {
                    var idx = 1, sib = cur.previousSibling;
                    while (sib) { if (sib.nodeType === 1 && sib.tagName === cur.tagName) idx++; sib = sib.previousSibling; }
                    parts.unshift(cur.tagName.toLowerCase() + (idx > 1 ? '[' + idx + ']' : ''));
                    cur = cur.parentNode;
                }
                return '//' + parts.join('/');
            }
        })();
        """;

    // GET: Agencies/Export
    public async Task<IActionResult> Export()
    {
        var agencies = await _context.Agencies
            .Include(a => a.AgencyListingChecks)
            .Include(a => a.ParserConfig)
            .AsNoTracking()
            .ToListAsync();

        var options = new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
        };

        var json = System.Text.Json.JsonSerializer.Serialize(agencies, options);
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);

        return File(bytes, "application/json", "agencies_export.json");
    }

    // POST: Agencies/Import
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(IFormFile importFile)
    {
        if (importFile == null || importFile.Length == 0)
        {
            TempData["ErrorMessage"] = "Please select a valid JSON file to import.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            _logger.LogInformation("Import preview requested. Analyzing file {FileName} ({Size} bytes).", importFile.FileName, importFile.Length);
            using var stream = importFile.OpenReadStream();
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var importedAgencies = await System.Text.Json.JsonSerializer.DeserializeAsync<List<Agency>>(stream, options);

            if (importedAgencies == null || importedAgencies.Count == 0)
            {
                _logger.LogWarning("Import file {FileName} contained no valid agency objects.", importFile.FileName);
                TempData["ErrorMessage"] = "No valid agencies found in the imported file.";
                return RedirectToAction(nameof(Index));
            }

            // Let's analyze new vs conflicting
            var existingDomains = await _context.Agencies
                .Select(a => a.AgencyDomain)
                .ToListAsync();

            var viewModel = new ImportPreviewViewModel();
            var serializeOptions = new System.Text.Json.JsonSerializerOptions
            {
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
            };
            viewModel.SerializedData = System.Text.Json.JsonSerializer.Serialize(importedAgencies, serializeOptions);

            foreach (var imported in importedAgencies)
            {
                var isExisting = existingDomains.Contains(imported.AgencyDomain, StringComparer.OrdinalIgnoreCase);
                var preview = new ImportAgencyPreview
                {
                    AgencyDomain = imported.AgencyDomain,
                    HasParserConfig = imported.ParserConfig != null,
                    ListingChecksCount = imported.AgencyListingChecks?.Count ?? 0,
                    Notes = imported.Notes,
                    IsSuspended = imported.IsSuspended
                };

                if (isExisting)
                {
                    viewModel.ConflictingAgencies.Add(preview);
                }
                else
                {
                    viewModel.NewAgencies.Add(preview);
                }
            }

            _logger.LogInformation("Import analysis complete. Found {NewCount} new domains and {ConflictCount} existing domains in imported file.", 
                viewModel.NewAgencies.Count, viewModel.ConflictingAgencies.Count);

            return View("ImportPreview", viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during import file analysis.");
            TempData["ErrorMessage"] = $"Error analyzing import file: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    // POST: Agencies/ConfirmImport
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmImport(string serializedData)
    {
        if (string.IsNullOrWhiteSpace(serializedData))
        {
            TempData["ErrorMessage"] = "Import data was lost or invalid. Please try again.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            _logger.LogInformation("ConfirmImport requested to persist parsed agencies.");
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var importedAgencies = System.Text.Json.JsonSerializer.Deserialize<List<Agency>>(serializedData, options);

            if (importedAgencies != null)
            {
                int newCount = 0;
                int mergedCount = 0;

                foreach (var imported in importedAgencies)
                {
                    // Find existing by domain
                    var existing = await _context.Agencies
                        .Include(a => a.AgencyListingChecks)
                        .Include(a => a.ParserConfig)
                        .FirstOrDefaultAsync(a => a.AgencyDomain == imported.AgencyDomain);

                    if (existing == null)
                    {
                        _logger.LogInformation("Import: Adding new agency domain {AgencyDomain}.", imported.AgencyDomain);
                        // Insert new, reset IDs
                        imported.Id = 0;
                        if (imported.ParserConfig != null) imported.ParserConfig.Id = 0;
                        if (imported.AgencyListingChecks != null)
                        {
                            foreach (var check in imported.AgencyListingChecks) check.Id = 0;
                        }
                        _context.Agencies.Add(imported);
                        newCount++;
                    }
                    else
                    {
                        _logger.LogInformation("Import: Merging configuration details into existing agency domain {AgencyDomain}.", imported.AgencyDomain);
                        // Update existing config
                        if (imported.ParserConfig != null)
                        {
                            if (existing.ParserConfig == null)
                            {
                                imported.ParserConfig.Id = 0;
                                imported.ParserConfig.AgencyId = existing.Id;
                                existing.ParserConfig = imported.ParserConfig;
                            }
                            else
                            {
                                var existingConfigId = existing.ParserConfig.Id;
                                var existingAgencyId = existing.ParserConfig.AgencyId;
                                _context.Entry(existing.ParserConfig).CurrentValues.SetValues(imported.ParserConfig);
                                existing.ParserConfig.Id = existingConfigId;
                                existing.ParserConfig.AgencyId = existingAgencyId;
                            }
                        }

                        // Overwrite listing checks
                        if (imported.AgencyListingChecks != null)
                        {
                            _context.AgencyListingChecks.RemoveRange(existing.AgencyListingChecks);
                            foreach (var check in imported.AgencyListingChecks)
                            {
                                check.Id = 0;
                                check.AgencyId = existing.Id;
                                existing.AgencyListingChecks.Add(check);
                            }
                        }

                        existing.PaginationSelector = imported.PaginationSelector;
                        existing.DataSourceType     = imported.DataSourceType;
                        existing.ApiListingUrl      = imported.ApiListingUrl;
                        existing.IsSuspended = imported.IsSuspended;
                        if (!string.IsNullOrEmpty(imported.Notes))
                        {
                            existing.Notes = imported.Notes;
                        }

                        _context.Agencies.Update(existing);
                        mergedCount++;
                    }
                }
                await _context.SaveChangesAsync();
                
                var message = "";
                if (newCount > 0 && mergedCount > 0)
                {
                    message = $"Import completed: {newCount} new agencies added, {mergedCount} existing agencies merged.";
                }
                else if (newCount > 0)
                {
                    message = $"Import completed: {newCount} new agencies added.";
                }
                else if (mergedCount > 0)
                {
                    message = $"Import completed: {mergedCount} existing agencies merged.";
                }
                else
                {
                    message = "Import completed, but no changes were made.";
                }

                _logger.LogInformation("Import confirmed and completed. {Message}", message);
                TempData["SuccessMessage"] = message;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during import confirmation save.");
            TempData["ErrorMessage"] = $"Error importing agencies: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: Agencies/RequestCrawl/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestCrawl(int id)
    {
        var agency = await _context.Agencies.FindAsync(id);
        if (agency == null) return NotFound();

        agency.CrawlRequestedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Crawl requested for \"{agency.AgencyDomain}\". The crawler will pick it up shortly.";
        return RedirectToAction(nameof(Index));
    }

    // POST: Agencies/ToggleSuspension/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleSuspension(int id)
    {
        var agency = await _context.Agencies.FindAsync(id);
        if (agency == null) return NotFound();

        agency.IsSuspended = !agency.IsSuspended;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Agency \"{agency.AgencyDomain}\" has been {(agency.IsSuspended ? "suspended" : "resumed")}.";
        return RedirectToAction(nameof(Index));
    }

    private bool AgencyExists(int id)
    {
        return _context.Agencies.Any(e => e.Id == id);
    }
}

public class ImportPreviewViewModel
{
    public List<ImportAgencyPreview> NewAgencies { get; set; } = [];
    public List<ImportAgencyPreview> ConflictingAgencies { get; set; } = [];
    public string SerializedData { get; set; } = string.Empty;
}

public class ImportAgencyPreview
{
    public string AgencyDomain { get; set; } = string.Empty;
    public bool HasParserConfig { get; set; }
    public int ListingChecksCount { get; set; }
    public string? Notes { get; set; }
    public bool IsSuspended { get; set; }
}

