using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Immo.Data;
using Microsoft.EntityFrameworkCore;

namespace Immo.Crawler;

public class CrawlerWorker : BackgroundService
{
    private readonly ILogger<CrawlerWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public CrawlerWorker(ILogger<CrawlerWorker> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Crawler Worker starting...");

        // Track when the last full cycle ran so we can schedule the next one
        var nextFullCycleAt = DateTime.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // --- On-demand crawl requests ---
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<ImmoContext>();
                    var crawler = scope.ServiceProvider.GetRequiredService<CrawlerService>();

                    var pendingAgencies = await context.Agencies
                        .Where(a => a.CrawlRequestedAt != null)
                        .ToListAsync(stoppingToken);

                    foreach (var agency in pendingAgencies)
                    {
                        if (stoppingToken.IsCancellationRequested) break;

                        _logger.LogInformation("On-demand crawl requested for agency: {Domain}", agency.AgencyDomain);

                        // Clear the request flag immediately so it isn't picked up again
                        agency.CrawlRequestedAt = null;
                        await context.SaveChangesAsync(stoppingToken);

                        await crawler.CrawlListingPageAsync(agency.AgencyDomain);
                    }
                }

                // --- Regular scheduled full cycle ---
                if (DateTime.UtcNow >= nextFullCycleAt)
                {
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<ImmoContext>();
                        var crawler = scope.ServiceProvider.GetRequiredService<CrawlerService>();

                        var agencies = await context.Agencies.ToListAsync(stoppingToken);
                        _logger.LogInformation("Starting scheduled crawl for {Count} agencies...", agencies.Count);

                        foreach (var agency in agencies)
                        {
                            if (stoppingToken.IsCancellationRequested) break;

                            _logger.LogInformation("Processing agency: {Domain}", agency.AgencyDomain);
                            await crawler.CrawlListingPageAsync(agency.AgencyDomain);
                        }

                        if (!stoppingToken.IsCancellationRequested)
                        {
                            await crawler.CheckUnseenPagesAsync();
                        }
                    }

                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<ImmoContext>();
                        var settings = await context.AppSettings.FirstOrDefaultAsync(stoppingToken);
                        var waitHours = settings?.CrawlIntervalHours ?? 4;

                        nextFullCycleAt = DateTime.UtcNow.AddHours(waitHours);
                        _logger.LogInformation("Crawl cycle finished. Next scheduled cycle at {NextCycle}.", nextFullCycleAt);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during crawling cycle.");
            }

            // Poll every minute for on-demand requests
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }

        _logger.LogInformation("Crawler Worker stopping...");
    }
}
