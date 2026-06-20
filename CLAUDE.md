# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Is

A .NET 10 real estate crawler/parser pipeline. It monitors Belgian real estate agency websites, extracts property listings, and serves them via a web dashboard. Three separate hosted services share a single SQLite database.

## Build & Run

```bash
# Build everything
dotnet build

# Run the web app (also runs migrations on startup)
dotnet run --project Immo.Web

# Docker (all three services together)
docker-compose up -d

# Add a new EF migration
dotnet ef migrations add <Name> --project Immo.Data --startup-project Immo.Web

# Apply migrations manually
dotnet ef database update --project Immo.Data --startup-project Immo.Web
```

There are no automated tests in this project.

## Architecture

Four projects sharing one SQLite database (`immo.db`, path overridable via `DB_PATH` env var):

- **Immo.Data** — EF Core entities, `ImmoContext`, migrations
- **Immo.Crawler** — Background service that fetches raw HTML/JSON from agency sites
- **Immo.Parser** — Background service that extracts structured property data from raw pages
- **Immo.Web** — ASP.NET Core MVC dashboard for config, search, and logs

### Pipeline Flow

```
AgencyDomain/ApiListingUrl
        ↓
  CrawlerWorker (every 4h, or on-demand within 1 min)
        ↓  fetches HTML or JSON
  RawPage (stored with SHA256 hash, IsParsed=false)
        ↓
  ParserWorker (every 30s)
        ↓  picks strategy by agency DataSourceType
  Property + PropertyHistory (updated or created)
```

### Parser Strategies

Two strategies, both resolved via DI and selected at parse time:

- **ConfigurableParserStrategy** — HTML via XPath selectors stored in `ParserConfig`. Handles spec tables (label/value pairs), keyword-based sold detection, address regex, og:image fallback. Implements `IParserStrategy` (one page → one property).
- **JsonApiParserStrategy** — JSON APIs using dot-notation paths (e.g. `price.amount`, `photos[0].url`). Implements `IMultiPropertyParserStrategy` (one `RawPage` → many `Property` records). Uses `JsonPathHelper` for navigation.

### Crawler Behaviors

- **HTML mode**: Crawls `AgencyDomain`, extracts property links via `GeneralLinkExtractor` matching URL patterns in `AgencyListingChecks`, follows pagination via `PaginationSelector` XPath.
- **JSON API mode**: Fetches `ApiListingUrl`, stores content with `json-api://` URL prefix.
- **Change detection**: SHA256 of content compared to stored hash. If unchanged, only `CrawledAt` is updated. If changed, `IsParsed` is reset to trigger re-parsing.
- **404 detection**: Pages that return 404 have their associated `Property` marked `Sold=true`.
- **Rate limiting**: Random 2–5s delay between requests.

## Key Entities

| Entity | Purpose |
|--------|---------|
| `Agency` | One agency website. Has `DataSourceType` ("html"/"json_api"), `IsSuspended`, `CrawlRequestedAt` for on-demand triggers |
| `ParserConfig` | XPath selectors (HTML) and dot-notation paths (JSON) for extracting fields. One-to-one with Agency |
| `AgencyListingCheck` | URL patterns (JSON array `UrlPosibilities`) to match property links on listing pages |
| `RawPage` | Fetched content. `IsParsed=false` means parser hasn't processed it yet |
| `Property` | Extracted listing: price, location, bedrooms, area, EPC, sold/under-option status |
| `PropertyHistory` | Field-level change log (field name, old value, new value, timestamp) |
| `AppSettings` | Singleton (always ID=1): crawl intervals, keywords, SMTP config, timezone |
| `LogEntry` | Serilog SQLite sink output — queried directly for the logs UI |

## Configuration

All runtime behavior is configured through the web UI (`AppSettings` entity), not appsettings.json:
- `RecrawlAfterDays`, `CrawlIntervalHours` — timing controls
- `SoldKeywords`, `UnderOptionKeywords` — comma-separated strings for HTML parsing
- `PreferredTimezone`, `NewOrUpdatedThresholdDays` — display settings
- SMTP fields for email notifications

Environment variables: `DB_PATH` (database file path), `ASPNETCORE_HTTP_PORTS` (web port, default 8080).

## Immo.Web Controllers

- `HomeController` — Property search with filters (price, zip, bedrooms, area, EPC, status, recency); change history; bulk email export
- `AgenciesController` — Agency + parser config CRUD; JSON import/export; debug tools (test URL parsing/pagination); on-demand crawl trigger; reparse action
- `SettingsController` — Global `AppSettings` management
- `LogsController` — Log viewer filtering by level and source context

## Logging

Serilog in all three projects: console + SQLite sinks. Minimum level Warning, with Information for the `Immo.*` namespace. SQLite retention is 30 days. The `LogEntry` entity has a computed `LocalTimestamp` property that applies the configured timezone.
