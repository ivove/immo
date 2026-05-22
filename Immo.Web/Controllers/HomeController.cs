using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Immo.Web.Models;
using Immo.Data;
using Immo.Data.Entities;

namespace Immo.Web.Controllers;

public class HomeController : Controller
{
    private readonly ImmoContext _context;
    private readonly ILogger<HomeController> _logger;

    public HomeController(ImmoContext context, ILogger<HomeController> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IActionResult> Index(
        decimal? minPrice, decimal? maxPrice, 
        List<string>? zipCodes, 
        int? minBedrooms, 
        double? minLivingArea, 
        double? minPlotArea,
        string? maxEpc,
        string? status = "available",
        string? recency = null)
    {
        _logger.LogInformation("Property search requested. Filters - Price: [{MinPrice} - {MaxPrice}], Postal Codes: [{ZipCodes}], Beds: >= {MinBedrooms}, Area: >= {MinLivingArea} m², EPC: <= {MaxEpc}, Status: {Status}, Recency: {Recency}",
            minPrice, maxPrice, zipCodes != null ? string.Join(", ", zipCodes) : "All", minBedrooms, minLivingArea, maxEpc, status, recency ?? "Anytime");
        var settings = await _context.AppSettings.FirstOrDefaultAsync() ?? new AppSettings();
        var thresholdDays = settings.NewOrUpdatedThresholdDays;
        var thresholdDate = DateTime.UtcNow.AddDays(-thresholdDays);

        var query = _context.Properties.AsQueryable();

        // Status Filtering
        if (status == "available")
        {
            query = query.Where(p => !p.Sold && !p.UnderOption);
        }
        else if (status == "sold")
        {
            query = query.Where(p => p.Sold);
        }
        else if (status == "under_option")
        {
            query = query.Where(p => p.UnderOption);
        }

        // Recency Filtering
        if (recency == "new")
        {
            query = query.Where(p => p.CreatedAt >= thresholdDate);
        }
        else if (recency == "updated")
        {
            query = query.Where(p => p.LastUpdatedAt >= thresholdDate && p.CreatedAt < thresholdDate);
        }
        else if (recency == "new_or_updated")
        {
            query = query.Where(p => p.CreatedAt >= thresholdDate || p.LastUpdatedAt >= thresholdDate);
        }

        if (minPrice.HasValue) query = query.Where(p => p.Price >= minPrice.Value);
        if (maxPrice.HasValue) query = query.Where(p => p.Price <= maxPrice.Value);
        
        if (zipCodes != null && zipCodes.Any())
        {
            query = query.Where(p => p.ZipCode != null && zipCodes.Contains(p.ZipCode));
        }

        if (minBedrooms.HasValue) query = query.Where(p => p.Bedrooms >= minBedrooms.Value);
        if (minLivingArea.HasValue) query = query.Where(p => p.LivingArea >= minLivingArea.Value);
        if (minPlotArea.HasValue) query = query.Where(p => p.PlotArea >= minPlotArea.Value);

        if (!string.IsNullOrEmpty(maxEpc))
        {
            var epcGrades = new List<string> { "A", "B", "C", "D", "E", "F", "G" };
            var maxIndex = epcGrades.IndexOf(maxEpc.ToUpper());
            if (maxIndex != -1)
            {
                var allowedGrades = epcGrades.Take(maxIndex + 1).ToList();
                query = query.Where(p => p.EpcScore != null && allowedGrades.Contains(p.EpcScore.Substring(0, 1).ToUpper()));
            }
        }

        var properties = await query.OrderByDescending(p => p.Id).ToListAsync();
        _logger.LogInformation("Found {Count} matching properties.", properties.Count);
        
        var rawLocations = await _context.Properties
            .Where(p => !string.IsNullOrEmpty(p.ZipCode))
            .Select(p => new { p.ZipCode, p.City })
            .ToListAsync();

        var availableZipCodes = rawLocations
            .GroupBy(p => p.ZipCode)
            .Select(g => new 
            { 
                ZipCode = g.Key, 
                City = g.FirstOrDefault(x => !string.IsNullOrEmpty(x.City))?.City ?? "" 
            })
            .OrderBy(l => l.ZipCode)
            .ToList();

        // Pass filter values back to the view
        ViewBag.MinPrice = minPrice;
        ViewBag.MaxPrice = maxPrice;
        ViewBag.SelectedZipCodes = zipCodes ?? new List<string>();
        ViewBag.AvailableZipCodes = availableZipCodes;
        ViewBag.MinBedrooms = minBedrooms;
        ViewBag.MinLivingArea = minLivingArea;
        ViewBag.MinPlotArea = minPlotArea;
        ViewBag.MaxEpc = maxEpc;
        ViewBag.Status = status;
        ViewBag.Recency = recency;
        ViewBag.ThresholdDays = thresholdDays;

        return View(properties);
    }

    public async Task<IActionResult> Changes(int id)
    {
        var property = await _context.Properties
            .Include(p => p.Agency)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (property == null)
        {
            return NotFound();
        }

        var history = await _context.PropertyHistories
            .Where(h => h.PropertyId == id)
            .OrderByDescending(h => h.ChangedAt)
            .ToListAsync();

        ViewBag.Property = property;
        return View(history);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
