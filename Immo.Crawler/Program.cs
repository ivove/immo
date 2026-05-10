using Immo.Crawler;
using Immo.Crawler.Extractors;
using Immo.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Serilog;
using Serilog.Events;

var builder = Host.CreateApplicationBuilder(args);

// Determine database path
var dbPath = Environment.GetEnvironmentVariable("DB_PATH") 
    ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Immo.Data", "immo.db"));

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Warning()
    .MinimumLevel.Override("Immo", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.SQLite(sqliteDbPath: dbPath, tableName: "Logs")
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

// Configure SQLite Database
builder.Services.AddDbContext<ImmoContext>(options =>
{
    options.UseSqlite($"Data Source={dbPath}");
});

// Register strategies
builder.Services.AddTransient<ILinkExtractorStrategy, GeneralLinkExtractor>();

// Register CrawlerService
builder.Services.AddHttpClient<CrawlerService>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    AllowAutoRedirect = true,
    MaxAutomaticRedirections = 10
});

// Register Worker
builder.Services.AddHostedService<CrawlerWorker>();

var host = builder.Build();
host.Run();

