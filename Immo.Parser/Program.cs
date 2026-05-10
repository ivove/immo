using Immo.Data;
using Immo.Parser;
using Immo.Parser.Strategies;
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

builder.Services.AddDbContext<ImmoContext>(options =>
{
    options.UseSqlite($"Data Source={dbPath}");
});

// Register strategies
builder.Services.AddTransient<IParserStrategy, ConfigurableParserStrategy>();
builder.Services.AddTransient<ParserService>();

// Register Worker
builder.Services.AddHostedService<ParserWorker>();

var host = builder.Build();
host.Run();

