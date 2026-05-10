using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Immo.Parser;

public class ParserWorker : BackgroundService
{
    private readonly ILogger<ParserWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public ParserWorker(ILogger<ParserWorker> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Parser Worker starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var parserService = scope.ServiceProvider.GetRequiredService<ParserService>();
                    await parserService.ParsePendingPagesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during parsing cycle.");
            }

            _logger.LogInformation("Waiting for next cycle...");
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }

        _logger.LogInformation("Parser Worker stopping...");
    }
}
