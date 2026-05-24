using Immo.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Immo.Data;

public class ImmoContext : DbContext
{
    public ImmoContext(DbContextOptions<ImmoContext> options) : base(options)
    {
    }

    public ImmoContext()
    {
    }

    public DbSet<RawPage> RawPages { get; set; }
    public DbSet<Agency> Agencies { get; set; }
    public DbSet<AgencyListingCheck> AgencyListingChecks { get; set; }
    public DbSet<Property> Properties { get; set; }
    public DbSet<PropertyHistory> PropertyHistories { get; set; }
    public DbSet<ParserConfig> ParserConfigs { get; set; }
    public DbSet<AppSettings> AppSettings { get; set; }
    public DbSet<LogEntry> Logs { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite("Data Source=immo.db");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AgencyListingCheck>()   
            .Property(p => p.UrlPosibilities)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>()
            );

        modelBuilder.Entity<Agency>()
            .HasOne(a => a.ParserConfig)
            .WithOne(c => c.Agency)
            .HasForeignKey<ParserConfig>(c => c.AgencyId);

        modelBuilder.Entity<Agency>()
            .HasMany(a => a.RawPages)
            .WithOne(p => p.Agency)
            .HasForeignKey(p => p.AgencyId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Agency>()
            .HasMany(a => a.Properties)
            .WithOne(p => p.Agency)
            .HasForeignKey(p => p.AgencyId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Property>()
            .HasMany<PropertyHistory>()
            .WithOne(h => h.Property)
            .HasForeignKey(h => h.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        // Seed default settings row
        modelBuilder.Entity<AppSettings>().HasData(new AppSettings
        {
            Id = 1,
            RecrawlAfterDays = 3,
            CrawlIntervalHours = 4,
            SoldKeywords = "verkocht,sold",
            UnderOptionKeywords = "onder optie,optie",
            PreferredTimezone = "UTC",
            NewOrUpdatedThresholdDays = 3
            ,
            SmtpHost = "",
            SmtpPort = 25,
            SmtpUsername = "",
            SmtpPassword = "",
            FromEmail = "",
            SmtpUseSsl = true
        });
    }
}
