# Immo Crawler & Parser

A production-ready, containerized real estate crawler and parser pipeline built with .NET 10. It monitors Belgian real estate agency websites, extracts structured property data, and serves it through a web dashboard with search and analysis tools.

## 🚀 Features

- **Automated Crawling**: Background workers crawl configured agency websites on a configurable schedule (default every 4 hours).
- **On-demand Crawling**: Trigger an immediate crawl for any agency from the web UI; the worker picks it up within one minute.
- **HTML & JSON API Support**: Scrape traditional HTML pages via XPath selectors, or fetch structured JSON API endpoints — both configured without code changes.
- **Visual Config Builder**: Point-and-click XPath configuration — load any property page in a built-in iframe, click elements to capture their selectors, and map spec table rows to label fields. Includes a live parse preview to validate selectors before saving.
- **Advanced Filtering**: Filter properties by status (Available, Sold, Under Option), price range, bedrooms, living area, EPC score, and multi-select postal codes. Filters are persisted across sessions.
- **Change Tracking**: Field-level property history — every update to price, status, or specs is recorded and browsable.
- **Centralized Logging**: Structured logging with Serilog, stored in the shared SQLite database and displayed in the browser's local timezone.
- **Import/Export**: Full agency configuration (crawl rules, parser config) exportable and importable as JSON for backup or sharing.
- **Docker Orchestration**: All three services containerised and orchestrated with Docker Compose.
- **CI/CD**: GitHub Actions workflow for automated Docker Hub image publishing.

## 🛠 Tech Stack

- **Core**: .NET 10 (ASP.NET Core MVC, Worker Services, Class Libraries)
- **Database**: SQLite with Entity Framework Core
- **HTML Parsing**: HtmlAgilityPack (XPath)
- **Frontend**: Bootstrap 5, Bootstrap Icons
- **Logging**: Serilog (SQLite & Console sinks)
- **Infrastructure**: Docker, Docker Compose, GitHub Actions

## 📦 Getting Started

### Prerequisites
- Docker & Docker Compose
- .NET 10 SDK (for local development)

### Running with Docker
```bash
docker-compose up -d
```
Access the dashboard at `http://localhost:8080`.

### Local Development
```bash
# Apply migrations (run once, or after adding a new migration)
dotnet ef database update --project Immo.Data --startup-project Immo.Web

# Start the web app (also runs migrations on startup)
dotnet run --project Immo.Web
```

## 🏗 Architecture

Three hosted services share a single SQLite database (`immo.db`):

| Project | Role |
|---|---|
| `Immo.Data` | EF Core entities, `ImmoContext`, migrations |
| `Immo.Crawler` | Fetches raw HTML/JSON from agency sites every 4 h (or on demand) |
| `Immo.Parser` | Extracts structured `Property` records from raw pages every 30 s |
| `Immo.Web` | ASP.NET Core MVC dashboard |

**Pipeline:**
```
Agency domain / API URL
    ↓  CrawlerWorker  (SHA-256 change detection, 2–5 s rate limiting)
RawPage  (IsParsed = false)
    ↓  ParserWorker
Property + PropertyHistory
```

## ⚙️ Configuration

All runtime settings are managed through the web UI (`AppSettings` entity), not `appsettings.json`:

- `RecrawlAfterDays` / `CrawlIntervalHours` — timing controls
- `SoldKeywords` / `UnderOptionKeywords` — comma-separated strings for status detection
- `PreferredTimezone` — for timestamp display
- SMTP fields for email notifications

Environment variables: `DB_PATH` (database file path), `ASPNETCORE_HTTP_PORTS` (default `8080`).

## 🕸 Agency Setup

Navigate to **Agencies** to add an agency and choose its data source type:

### HTML Scraping
1. Set the **Agency Domain** (base URL for crawling).
2. Add **Crawling Rules** — URL patterns that identify property listing links.
3. Optionally set a **Pagination Selector** (XPath) to follow next-page links.
4. Open **Visual Config Builder** to configure the parser by clicking elements directly on the live page:
   - Click any element in the rendered page to capture its XPath selector.
   - Use **Map Labels** to parse the spec table and assign spec row labels (e.g. "Slaapkamers") to their corresponding label mapping fields.
   - Click **Parse** to run the full parser against the loaded URL and verify extracted values.
   - Save without leaving the page; the iframe stays loaded.
5. Alternatively, use the **Full Editor** (Parser Configuration) to enter XPath selectors and label mappings by hand.

### JSON API
1. Set the **API Listing URL** (endpoint returning a JSON array of properties).
2. Configure **Field Paths** using dot-notation (`price.amount`, `photos[0].url`) in the Parser Configuration.

## 📈 Observability

The **Logs** page shows structured Serilog output across all services. Filter by log level and source context. Timestamps are stored as UTC and converted to `PreferredTimezone` for display.

## 📝 License
This project is licensed under the MIT License.
