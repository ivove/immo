using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Immo.Data;
using Immo.Data.Entities;

namespace Immo.Web.Controllers;

public class AgenciesController : Controller
{
    private readonly ImmoContext _context;

    public AgenciesController(ImmoContext context)
    {
        _context = context;
    }

    // GET: Agencies
    public async Task<IActionResult> Index()
    {
        return View(await _context.Agencies
            .Include(a => a.AgencyListingChecks)
            .Include(a => a.ParserConfig)
            .ToListAsync());
    }

    // GET: Agencies/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var agency = await _context.Agencies
            .Include(a => a.AgencyListingChecks)
            .Include(a => a.ParserConfig)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (agency == null) return NotFound();

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
    public async Task<IActionResult> Create([Bind("Id,AgencyDomain,PaginationSelector,IsSuspended,Notes")] Agency agency)
    {
        if (ModelState.IsValid)
        {
            _context.Add(agency);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
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
    public async Task<IActionResult> Edit(int id, [Bind("Id,AgencyDomain,PaginationSelector,IsSuspended,Notes")] Agency agency)
    {
        if (id != agency.Id) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(agency);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!AgencyExists(agency.Id)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Index));
        }
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
        if (agency == null) return NotFound();

        await PurgeAgencyDataAsync(agency.Id);

        _context.Agencies.Remove(agency);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Agency \u0022{agency.AgencyDomain}\u0022 and all its crawled data have been deleted.";
        return RedirectToAction(nameof(Index));
    }

    // POST: Agencies/PurgeData/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PurgeData(int id)
    {
        var agency = await _context.Agencies.FindAsync(id);
        if (agency == null) return NotFound();

        var (pages, properties) = await PurgeAgencyDataAsync(agency.Id);

        TempData["SuccessMessage"] = $"Purged {properties} propert{(properties == 1 ? "y" : "ies")} and {pages} raw page{(pages == 1 ? "" : "s")} for \u0022{agency.AgencyDomain}\u0022.";
        return RedirectToAction(nameof(Index));
    }

    // POST: Agencies/ReparseData/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReparseData(int id)
    {
        var agency = await _context.Agencies.FindAsync(id);
        if (agency == null) return NotFound();

        var count = await ReparseAgencyDataAsync(agency.Id);

        TempData["SuccessMessage"] = $"Queued {count} page{(count == 1 ? "" : "s")} for reparsing for \u0022{agency.AgencyDomain}\u0022. The parser will process them shortly.";
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
            var html = await response.Content.ReadAsStringAsync();

            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);

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
            var propertyLinks = linkExtractor.ExtractLinks(html, urlToTest, agency.AgencyDomain).ToList();
            ViewBag.PropertyLinks = propertyLinks;

            // 3. Parser Strategy
            var rawPage = new Immo.Data.Entities.RawPage { Url = urlToTest, HtmlContent = html, AgencyId = agency.Id, CrawledAt = DateTime.UtcNow };
            var parser = new Immo.Parser.Strategies.ConfigurableParserStrategy(_context);
            var parsedProperty = parser.Parse(rawPage, doc);
            ViewBag.ParsedProperty = parsedProperty;
            
            ViewBag.RawHtmlLength = html.Length;
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
        return View(config);
    }

    // POST: Agencies/EditParserConfig
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditParserConfig(ParserConfig config)
    {
        if (ModelState.IsValid)
        {
            if (config.Id == 0)
            {
                _context.ParserConfigs.Add(config);
            }
            else
            {
                _context.Update(config);
            }
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Edit), new { id = config.AgencyId });
        }
        return View(config);
    }

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
            using var stream = importFile.OpenReadStream();
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var importedAgencies = await System.Text.Json.JsonSerializer.DeserializeAsync<List<Agency>>(stream, options);

            if (importedAgencies != null)
            {
                foreach (var imported in importedAgencies)
                {
                    // Find existing by domain
                    var existing = await _context.Agencies
                        .Include(a => a.AgencyListingChecks)
                        .Include(a => a.ParserConfig)
                        .FirstOrDefaultAsync(a => a.AgencyDomain == imported.AgencyDomain);

                    if (existing == null)
                    {
                        // Insert new, reset IDs
                        imported.Id = 0;
                        if (imported.ParserConfig != null) imported.ParserConfig.Id = 0;
                        foreach (var check in imported.AgencyListingChecks) check.Id = 0;
                        _context.Agencies.Add(imported);
                    }
                    else
                    {
                        // Update existing config
                        if (imported.ParserConfig != null)
                        {
                            if (existing.ParserConfig == null)
                            {
                                imported.ParserConfig.Id = 0;
                                existing.ParserConfig = imported.ParserConfig;
                            }
                            else
                            {
                                var existingConfigId = existing.ParserConfig.Id;
                                _context.Entry(existing.ParserConfig).CurrentValues.SetValues(imported.ParserConfig);
                                existing.ParserConfig.Id = existingConfigId;
                            }
                        }

                        // Just append new listing checks or overwrite? Let's overwrite for simplicity
                        _context.AgencyListingChecks.RemoveRange(existing.AgencyListingChecks);
                        foreach (var check in imported.AgencyListingChecks)
                        {
                            check.Id = 0;
                            check.AgencyId = existing.Id;
                            existing.AgencyListingChecks.Add(check);
                        }

                        existing.PaginationSelector = imported.PaginationSelector;
                        _context.Agencies.Update(existing);
                    }
                }
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Agencies imported successfully.";
            }
        }
        catch (Exception ex)
        {
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
