using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Immo.Data;
using Immo.Data.Entities;

namespace Immo.Web.Controllers;

public class SettingsController : Controller
{
    private readonly ImmoContext _context;

    public SettingsController(ImmoContext context)
    {
        _context = context;
    }

    // GET: Settings
    public async Task<IActionResult> Index()
    {
        var settings = await _context.AppSettings.FirstOrDefaultAsync() 
                      ?? new AppSettings { Id = 1 };
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
            }
            else
            {
                existing.RecrawlAfterDays = settings.RecrawlAfterDays;
                existing.SoldKeywords = settings.SoldKeywords;
                existing.UnderOptionKeywords = settings.UnderOptionKeywords;
                _context.Update(existing);
            }
            await _context.SaveChangesAsync();
            TempData["Success"] = "Settings updated successfully!";
            return RedirectToAction(nameof(Index));
        }
        return View("Index", settings);
    }
}
