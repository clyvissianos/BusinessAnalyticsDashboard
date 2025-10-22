# 📊 Business Analytics Dashboard

**Full-stack analytics platform** built with **ASP.NET Core Web API**, **Entity Framework Core**, **JWT Authentication**, and a modular data processing engine.

It enables users to:
- 🔒 Register, login, and manage access roles (`Admin`, `Analyst`, `Viewer`)
- 📁 Upload CSV data sources (sales, satisfaction, etc.)
- ⚙️ Automatically profile and parse uploaded datasets
- 📊 Generate KPI dashboards and export to Power BI or PDF
- 🤖 (Upcoming) AI-powered insights: detect trends and anomalies automatically

---

## 🧠 Tech Stack
| Layer | Technologies |
|-------|---------------|
| Backend | ASP.NET Core 8, EF Core, Identity, AutoMapper, Swagger |
| Database | SQL Server |
| Auth | JWT Bearer, Role-based authorization |
| Frontend *(upcoming)* | React + Chart.js (or Blazor) |
| BI/Visualization | Power BI integration, PDF export |
| ML Module *(optional)* | Python/ML.NET microservice |

---

## 🚀 Run Locally
```bash
dotnet restore
dotnet ef database update --project src/BusinessAnalytics.Infrastructure --startup-project src/BusinessAnalytics.Api
dotnet run --project src/BusinessAnalytics.Api
