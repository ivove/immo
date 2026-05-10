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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<ImmoContext>();
                    var crawler = scope.ServiceProvider.GetRequiredService<CrawlerService>();

                    var agencies = await context.Agencies.ToListAsync(stoppingToken);
                    _logger.LogInformation("Starting crawl for {Count} agencies...", agencies.Count);

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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during crawling cycle.");
            }

            _logger.LogInformation("Crawl cycle finished. Waiting 4 hours for next cycle...");
            await Task.Delay(TimeSpan.FromHours(4), stoppingToken);
        }

        _logger.LogInformation("Crawler Worker stopping...");
    }
}
