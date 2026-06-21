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

        var nextFullCycleAt = DateTime.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // --- On-demand crawl requests ---
                await ProcessOnDemandRequestsAsync(stoppingToken);

                // --- Regular scheduled full cycle ---
                if (DateTime.UtcNow >= nextFullCycleAt)
                {
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<ImmoContext>();
                        var crawler = scope.ServiceProvider.GetRequiredService<CrawlerService>();

                        var agencies = await context.Agencies
                            .Where(a => !a.IsSuspended)
                            .ToListAsync(stoppingToken);
                        _logger.LogInformation("Starting scheduled crawl for {Count} agencies...", agencies.Count);

                        foreach (var agency in agencies)
                        {
                            if (stoppingToken.IsCancellationRequested) break;

                            // Service on-demand requests between agencies so they aren't blocked
                            // by a long-running scheduled cycle.
                            await ProcessOnDemandRequestsAsync(stoppingToken);

                            _logger.LogInformation("Processing agency: {Domain}", agency.AgencyDomain);

                            if (agency.DataSourceType == "json_api" && !string.IsNullOrEmpty(agency.ApiListingUrl))
                                await crawler.CrawlJsonApiAsync(agency.ApiListingUrl, agency.Id);
                            else
                                await crawler.CrawlListingPageAsync(agency.AgencyDomain, agencyId: agency.Id);
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

    private async Task ProcessOnDemandRequestsAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ImmoContext>();
        var crawler = scope.ServiceProvider.GetRequiredService<CrawlerService>();

        var pendingAgencies = await context.Agencies
            .Where(a => a.CrawlRequestedAt != null && !a.IsSuspended)
            .ToListAsync(stoppingToken);

        foreach (var agency in pendingAgencies)
        {
            if (stoppingToken.IsCancellationRequested) break;

            _logger.LogInformation("On-demand crawl requested for agency: {Domain}", agency.AgencyDomain);

            agency.CrawlRequestedAt = null;
            await context.SaveChangesAsync(stoppingToken);

            if (agency.DataSourceType == "json_api" && !string.IsNullOrEmpty(agency.ApiListingUrl))
                await crawler.CrawlJsonApiAsync(agency.ApiListingUrl, agency.Id);
            else
                await crawler.CrawlListingPageAsync(agency.AgencyDomain, agencyId: agency.Id);
        }
    }
}
