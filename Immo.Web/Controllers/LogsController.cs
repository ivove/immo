using Immo.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Immo.Web.Controllers;

public class LogsController : Controller
{
    private readonly ImmoContext _context;

    public LogsController(ImmoContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? level, string? source, int page = 1)
    {
        int pageSize = 50;
        
        var query = _context.Logs.AsQueryable();

        if (!string.IsNullOrEmpty(level))
        {
            query = query.Where(l => l.Level == level);
        }

        if (!string.IsNullOrEmpty(source))
        {
            query = query.Where(l => l.Properties != null && l.Properties.Contains($"\"SourceContext\":\"{source}\""));
        }

        var totalLogs = await query.CountAsync();
        var logs = await query
            .OrderByDescending(l => l.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalLogs / (double)pageSize);
        ViewBag.CurrentLevel = level;
        ViewBag.CurrentSource = source;

        return View(logs);
    }
}
