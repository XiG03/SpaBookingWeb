# GitHub Copilot / AI Agent Instructions for SpaBookingWeb

## Quick overview
- ASP.NET Core 8 MVC + Razor Pages app with Identity. Main app entry: `Program.cs`.
- DB: Entity Framework Core (SqlServer). DbContext: `Data/ApplicationDbContext.cs` (composite keys and explicit relationships configured there).
- Areas: `Areas/Manager` hosts admin functionality; `Areas/Identity` contains identity pages. Public controllers live in `Controllers/`.
- Domain note: Identity is extended via `Models/ApplicationUser.cs` and linked to an `Employee` record (via `IdentityUserId`).
- External integrations: Google OAuth (configured via `appsettings.json` or user secrets) and Momo payment (`Services/MomoService.cs`, currently uses hard-coded test keys).

## What to know first (important local facts)
- Default connection string is in `appsettings.json` under `ConnectionStrings:DefaultConnection` and points to `Server=MSI;Database=SpaBookingDB;Trusted_Connection=True;` — change for your environment.
- Secrets: Google OAuth client ID/secret exist in `appsettings.json`. Project also has `UserSecretsId` in the `.csproj` (preferred place for dev secrets). Avoid committing production secrets.
- Docker: `Dockerfile` builds and runs the app (exposes ports 8080/8081). Use `dotnet publish` or Docker as in the file to produce the container.
- No test project found in the repository (no automated tests present).

## Common developer tasks & exact commands
- Run locally (dev):
  - dotnet run --project SpaBookingWeb
  - or use Visual Studio / VS Code launch configuration.
- Run with file watching:
  - dotnet watch --project SpaBookingWeb run
- EF Core migrations (add/update):
  - Add migration: `dotnet ef migrations add <Name> --project SpaBookingWeb --startup-project SpaBookingWeb -o Data/Migrations`
  - Apply migrations: `dotnet ef database update --project SpaBookingWeb --startup-project SpaBookingWeb`
  - In Package Manager Console (Visual Studio): `Update-Database -Project SpaBookingWeb`
- Build Docker image (root of project):
  - docker build -t spabookingweb:latest .
  - docker run -p 8080:8080 spabookingweb:latest

## Architectural & code patterns to follow (examples)
- Identity customization: `ApplicationUser` extends `IdentityUser`. When creating or querying users, be careful to keep `ApplicationUser` and `Employee` associations in sync (see `Models/ApplicationUser.cs` and `Models/Employee`).
- Soft-delete: `Voucher` uses `IsDeleted` to keep history; follow this pattern if you add logical deletions elsewhere.
- Composite keys and relationships: configured centrally in `ApplicationDbContext.OnModelCreating` (examples: `ServiceConsumable`, `ComboDetail`, `TechnicianService`). When altering models, update the model builder accordingly.
- Controllers + Razor: UI is a standard MVC/Razor mix. Use ViewModels from `ViewModels/` for data transfer (e.g., `ViewModels/Manager/*`).
- Service registration: Add services and interfaces in `Program.cs` (eg. `builder.Services.AddScoped<ICustomerService, CustomerService>();`). When adding a new service, register it in the same place.

## Integration details & gotchas
- Google OAuth: configured via `Authentication:Google` in `appsettings.json`. On local dev, prefer `dotnet user-secrets` to store client secret (project has a `UserSecretsId`).
- Momo payment: `Services/MomoService.cs` calls a Momo test endpoint with hard-coded keys; update to use configuration and secure storage for keys before production.
- Database: Project references both SQL Server and SQLite providers but migrations are clearly intended for SQL Server. Use SQL Server for running existing migrations.
- Error handling: app uses `app.UseStatusCodePagesWithReExecute("/Error/{0}")` and an `ErrorController` with 404/500 views. Use that route for reproducing error pages.

## Files & locations worth checking when changing behavior
- App startup & DI: `Program.cs`
- Db schema & constraints: `Data/ApplicationDbContext.cs` and `Data/Migrations/`
- Identity UI: `Areas/Identity/Pages/` and `Views/Account/` (AccountController uses custom flows)
- Payments: `Services/MomoService.cs`
- Manager features: `Areas/Manager/Controllers`, `Services/Manager/`, `ViewModels/Manager/`

## Style / language notes
- Code comments and some identifiers contain Vietnamese; search the codebase with English and Vietnamese terms when investigating domain logic.
- Pay attention to numeric types (decimal(18,2) attributes used for money) and explicit DateTime initializations.

## Safety & security notes for AI agents
- Do not commit real secrets. If you see credentials in the code (e.g., Google client secret or Momo keys in `appsettings.json` or source files), flag them in a PR and suggest moving to `dotnet user-secrets` or environment variables.
- Avoid changing database `OnModelCreating` behavior without migration updates; provide a migration when schema changes are made.

---
If any part of this doc is unclear or you'd like more examples (e.g., a sample migration commit, or a step-by-step Docker compose for DB + app), tell me which area to expand and I will iterate. ✅