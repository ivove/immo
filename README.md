# Immo Crawler & Parser

A production-ready, containerized real estate crawler and parser solution built with .NET 10. This system monitors real estate agency websites, extracts property data using configurable XPath strategies, and provides a modern web dashboard for searching and analysis.

## 🚀 Features

- **Automated Crawling**: Periodic background workers crawl configured agency websites on a configurable schedule.
- **On-demand Crawling**: Trigger an immediate crawl for any individual agency directly from the web UI, without waiting for the next scheduled cycle.
- **Dynamic Parsing**: Configure XPath selectors via the web UI to support new websites without code changes.
- **Advanced Filtering**: Filter properties by status (Available, Sold, Under Option), price, bedrooms, living area, and multi-select postal codes.
- **Centralized Logging**: Structured logging with Serilog, persisted to a shared SQLite database, displayed in the browser's local timezone.
- **Docker Orchestration**: Fully containerized with Docker Compose for easy deployment.
- **CI/CD**: GitHub Actions workflow for automated Docker Hub image publishing with versioning.

## 🛠 Tech Stack

- **Core**: .NET 10 (Web, Worker Services, Class Libraries)
- **Database**: SQLite with Entity Framework Core
- **Frontend**: ASP.NET Core MVC, Bootstrap 5, Bi-icons
- **Logging**: Serilog (SQLite & Console sinks)
- **Infrastructure**: Docker, Docker Compose, GitHub Actions

## 📦 Getting Started

### Prerequisites
- Docker & Docker Compose
- .NET 10 SDK (for local development)

### Running with Docker
1. Clone the repository.
2. Run the orchestration:
   ```bash
   docker-compose up -d
   ```
3. Access the web dashboard at `http://localhost:8080`.

### Local Development
1. Update `appsettings.json` or set environment variables for database paths.
2. Run the migrations:
   ```bash
   dotnet ef database update --project Immo.Data --startup-project Immo.Web
   ```
3. Start the services:
   ```bash
   dotnet run --project Immo.Web
   ```

## ⚙️ Configuration

### Agency Management
Navigate to the **Agencies** tab to:
- Add/Edit agency domains.
- Configure crawler "Listing Checks" (URL patterns).
- Define **Parser Configs**: Map XPath selectors for Title, Price, Description, Images, and Specifications (Bedrooms, EPC, Area).
- **Crawl Now**: Click the ▶ button next to any agency to queue an immediate on-demand crawl. The crawler worker picks it up within one minute. A *Pending* badge is shown while the request is queued.

### Import/Export
The system supports exporting and importing your entire agency configuration (rules and parsers) via JSON, making it easy to share or backup your crawling logic.

## 📈 Observability
The **System Logs** dashboard provides real-time access to logs across all services. Timestamps are automatically converted from UTC (as stored by Serilog) to the browser server's local timezone. You can filter by:
- Log Level (Info, Warning, Error, Fatal)
- Source Class (e.g., `Immo.Crawler.CrawlerWorker`)

## 📝 License
This project is licensed under the MIT License.
