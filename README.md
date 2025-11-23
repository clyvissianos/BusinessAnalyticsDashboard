# 📊 Business Analytics Dashboard

A **modular full-stack analytics platform** built with **ASP.NET Core 8**, **Entity Framework Core**, and a flexible **data-ingestion engine**.  
Designed for secure data management, automated processing, and enterprise-grade analytics.

Key capabilities:
- 🔐 Secure authentication & authorization (Identity + JWT)  
- 📁 Upload & manage CSV/Excel data sources  
- 🔎 Automatic schema detection & column mapping  
- ⚙️ ETL pipeline (`RawImports → DataSourceMappings → Fact tables`)  
- 📊 Analytics endpoints for KPIs and aggregated sales metrics  
- 🧪 Integration & unit tests with SQLite  
- 🧩 Extensible foundation for ML-driven insights

---

## 🧠 Tech Stack

| Layer | Technologies |
|-------|-------------|
| **Backend** | ASP.NET Core 8 Web API, EF Core 8, Identity, AutoMapper, FluentValidation, Swagger |
| **Database** | SQL Server (primary), SQLite (integration tests) |
| **Authentication** | JWT Bearer, Role-based Authorization, Swagger UI Token Integration |
| **ETL / Engine** | CSV/Excel Parsers, DataSource → RawImport → Fact Loaders |
| **Frontend (next)** | React + Chart.js *or* Blazor Unified |
| **BI / Export** | Power BI integration (planned), PDF export (planned) |
| **AI Module (future)** | Python or ML.NET microservice (anomaly detection, trend analysis) |

---

## ✔️ Current Project Status

### ✅ Completed
- Full authentication system: Identity + JWT + Swagger authorize  
- Domain model (Dims, Facts, DataSources, RawImports)  
- CSV ingestion pipeline (stable, validated)  
- Sales analytics endpoints (summary, aggregations)  
- Error-handling middleware  
- SQLite integration test harness  
- Login and protected-route tests

---

## 🚀 Run Locally

```bash
dotnet restore

# Apply database migrations
dotnet ef database update \
  --project src/BusinessAnalytics.Infrastructure \
  --startup-project src/BusinessAnalytics.Api

# Run the API
dotnet run --project src/BusinessAnalytics.Api
