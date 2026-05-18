using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Immo.Data;
using Immo.Data.Entities;

namespace Immo.Web.Controllers;

public class SettingsController : Controller
{
    private readonly ImmoContext _context;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(ImmoContext context, ILogger<SettingsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: Settings
    public async Task<IActionResult> Index()
    {
        var settings = await _context.AppSettings.FirstOrDefaultAsync() 
                      ?? new AppSettings { Id = 1 };
        
        _logger.LogInformation("Loaded application settings. Preferred Timezone: {Timezone}, Recency Highlight Threshold: {ThresholdDays} days.", 
            settings.PreferredTimezone, settings.NewOrUpdatedThresholdDays);

        ViewBag.Timezones = TimeZoneInfo.GetSystemTimeZones()
            .OrderBy(tz => tz.DisplayName)
            .ToList();

        return View(settings);
    }

    // POST: Settings
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AppSettings settings)
    {
        if (ModelState.IsValid)
        {
            var existing = await _context.AppSettings.FirstOrDefaultAsync(s => s.Id == 1);
            if (existing == null)
            {
                settings.Id = 1;
                _context.AppSettings.Add(settings);
                _logger.LogInformation("Creating new application settings record. Timezone: {Timezone}, Recency: {ThresholdDays} days.",
                    settings.PreferredTimezone, settings.NewOrUpdatedThresholdDays);
            }
            else
            {
                _logger.LogInformation("Updating application settings. Old Timezone: {OldTimezone} -> New Timezone: {NewTimezone}. Recency Threshold: {OldRecency} -> {NewRecency} days.",
                    existing.PreferredTimezone, settings.PreferredTimezone, existing.NewOrUpdatedThresholdDays, settings.NewOrUpdatedThresholdDays);

                existing.RecrawlAfterDays = settings.RecrawlAfterDays;
                existing.CrawlIntervalHours = settings.CrawlIntervalHours;
                existing.SoldKeywords = settings.SoldKeywords;
                existing.UnderOptionKeywords = settings.UnderOptionKeywords;
                existing.PreferredTimezone = settings.PreferredTimezone;
                existing.NewOrUpdatedThresholdDays = settings.NewOrUpdatedThresholdDays;
                _context.Update(existing);
            }
            await _context.SaveChangesAsync();
            TempData["Success"] = "Settings updated successfully!";
            return RedirectToAction(nameof(Index));
        }

        _logger.LogWarning("Validation failed for settings model state. Changes not saved.");

        ViewBag.Timezones = TimeZoneInfo.GetSystemTimeZones()
            .OrderBy(tz => tz.DisplayName)
            .ToList();

        return View("Index", settings);
    }
}
