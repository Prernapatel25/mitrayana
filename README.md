# Mitrayana — Senior Citizen Help Portal

This repository contains a full-stack ASP.NET Core Web API (C#) backend and a small static frontend served from `wwwroot`. The stack uses MySQL (via Pomelo) and JWT-based authentication.

Tech: ASP.NET Core 8, EF Core (Pomelo MySQL), JWT, BCrypt, Vanilla JS, HTML/CSS

Quick setup (Windows PowerShell):

1. Install .NET 8 SDK: https://dotnet.microsoft.com
2. Install MySQL and create a database `mitrayana_db` or update the connection string in `appsettings.json`.

Update connection string in `appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "server=localhost;database=mitrayana_db;user=root;password=yourpassword;"
}
```

3. Restore packages:

```powershell
dotnet restore
```

4. Add EF migrations and update database (if you have dotnet-ef installed):

```powershell
dotnet tool install --global dotnet-ef --version 8.0.0
dotnet ef migrations add InitialCreate
dotnet ef database update
```

If you prefer, the app will call `Database.Migrate()` at startup and create the schema if not present.

5. Run the app:

```powershell
dotnet run
```

By default the app will serve static files from `wwwroot`. The API endpoints are under `/api`.

Default seeded admin: email `admin@mitrayana.local`, password `Admin@123` (change after first login).

Notes and next steps:
- Add stronger secret management for JWT key (use environment variables or secret manager).
- Implement more robust validation and DTO mapping.
- Add client-side dashboards for each role (place in `wwwroot`) or build a separate SPA.

Security helper (development)
--------------------------------
To generate a strong JWT signing key for local development and store it using the .NET user-secrets tool, run:

```powershell
cd /workspace/Mitrayana.Api
.\scripts\generate-jwt-key.ps1
```

This script will generate a 512-bit (64-byte) random key, print it once, and store it as `Jwt:Key` in this project's user-secrets.

Important: do NOT commit secrets into source control. For production, use a secure secret store (Azure Key Vault, AWS Secrets Manager, etc.) and configure your hosting environment to provide the secret as `Jwt__Key`.
# Mitrayana — Senior Citizen Help Portal

This repository contains a full-stack ASP.NET Core Web API (C#) backend and a small static frontend served from `wwwroot`. The stack uses MySQL (via Pomelo) and JWT-based authentication.

Tech: ASP.NET Core 7, EF Core (Pomelo MySQL), JWT, BCrypt, Vanilla JS, HTML/CSS

Quick setup (Windows PowerShell):

1. Install .NET 7 SDK: https://dotnet.microsoft.com
2. Install MySQL and create a database `mitrayana_db` or update the connection string in `appsettings.json`.

Update connection string in `appsettings.json`:

  "ConnectionStrings": {
    "DefaultConnection": "server=localhost;database=mitrayana_db;user=root;password=yourpassword;"
  }

3. Restore packages:

```powershell
dotnet restore
```

4. Add EF migrations and update database (if you have dotnet-ef installed):

```powershell
dotnet tool install --global dotnet-ef --version 7.0.0; # if not installed
dotnet ef migrations add InitialCreate
dotnet ef database update
```

If you prefer, the app will call `Database.Migrate()` at startup and create the schema if not present.

5. Run the app:

```powershell
dotnet run
```

By default the app will serve static files from `wwwroot`. The API endpoints are under `/api`.

Default seeded admin: email `admin@mitrayana.local`, password `Admin@123` (change after first login).

Notes and next steps:
- Add stronger secret management for JWT key (use environment variables or secret manager).
- Implement more robust validation and DTO mapping.
- Add client-side dashboards for each role (place in `wwwroot`) or build a separate SPA.
