# Maintenance Sandbox - Multi-Tenant SaaS Application

ASP.NET Core 8 MVC application for managing maintenance requests with multi-tenant support, Azure SQL integration, AI-powered assistance, and a fully isolated demo mode.

## Architecture

### Two-Database Design
- **SentinelMfgSuite_Core** (Business Database)
  - Tenant data, sites, work centers, equipment
  - Maintenance requests and messages
  - Multi-tenant filtered via `TenantId`

- **SentinelMfgSuite_Identity** (Directory Database)
  - ASP.NET Identity (users, roles)
  - Tenant subscriptions and billing
  - User-tenant associations

### Key Features
- **Multi-tenancy**: Row-level data isolation using `TenantId`
- **Identity**: ASP.NET Core Identity with custom `ApplicationUser`
- **Azure SQL**: Microsoft Entra ID (Azure AD) authentication
- **AI Integration**: Claude AI for maintenance suggestions
- **Onboarding Flow**: Guided tenant provisioning and user setup
- **Subscription Management**: Stripe integration (pilot mode available)
- **Demo Mode**: Per-session isolated tenants with auto-seeded data and auto-purge

## Prerequisites
- .NET 8 SDK
- Azure SQL Database access
- Azure Entra ID authentication
- Visual Studio 2022+ or VS Code
- Azure CLI (optional, for management)

## Setup

### 1. Clone Repository
```bash
git clone <your-repo-url>
cd MaintenanceSandbox
```

### 2. Configure User Secrets
Replace with your actual Azure SQL server and database names:

```bash
# Business Database
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=tcp:sentinaldb.database.windows.net,1433;Initial Catalog=SentinelMfgSuite_Core;Authentication=Active Directory Default;Encrypt=Mandatory;MultipleActiveResultSets=True;"

# Directory/Identity Database
dotnet user-secrets set "ConnectionStrings:DirectoryConnection" "Server=tcp:sentinaldb.database.windows.net,1433;Initial Catalog=SentinelMfgSuite_Identity;Authentication=Active Directory Default;Encrypt=Mandatory;MultipleActiveResultSets=True;"

# AI Configuration (optional)
dotnet user-secrets set "Ai:ApiKey" "<your-anthropic-api-key>"
```

### 3. Install NuGet Packages
```bash
dotnet restore
```

### 4. Database Setup

#### Azure SQL Firewall
1. Go to Azure Portal → SQL Server → **Networking**
2. Click **Add your client IPv4 address**
3. Save changes

#### Sandbox Tenant
The sandbox tenant (`5EFA6386-80F0-4565-87D2-5170079B6BE0`) is auto-created and seeded with demo data by `DbInitializer.SeedAsync()` on every app startup. No manual SQL required.

### 5. Run Application
```bash
dotnet run
```

Or press **F5** in Visual Studio.

## Demo Mode

### How It Works
The app ships with a demo mode designed for public "Try Me" flows from a marketing site.

- Every demo login creates a **brand-new isolated tenant** with its own fresh copy of seed data (40 maintenance requests, messages, master data).
- No two demo sessions share data — concurrent visitors cannot interfere with each other.
- Demo tenants are **auto-purged after 2 hours** (triggered on the next demo login and on app startup).

### Marketing Site Integration
Point a "Try the Demo" button on your marketing site at:

```
POST https://<app-url>/DemoUser/Switch
email=supervisor@sentinel-demo.local
```

This endpoint is `[AllowAnonymous]` and signs the visitor directly into the Maintenance dashboard as a Supervisor with a fresh isolated tenant. No registration or password required.

### Demo Accounts
Both accounts are defined in `Services/DemoUserProvider.cs` and bypass Identity completely.

| Email | Password | Role |
|-------|----------|------|
| `supervisor@sentinel-demo.local` | `sentineldemo` | Supervisor |
| `operator@sentinel-demo.local` | `sentineldemo` | Operator |

### Demo Restrictions
Demo tenants are blocked from mutating user management via `Filters/BlockDemoFilter`:
- `UsersAdmin/Create` (GET + POST)
- `TenantUserInvites/Create` (POST)
- `UserInvitesAdmin/Invite` (GET + POST)

Any attempt redirects back to the Index with a `TempData["err"]` message.

### Seeded Demo Data (per session)
- 2 Sites (Site Alpha, Site Beta)
- 2 Work Centers (WC-001, WC-002) under Area 1
- 4 Equipment items
- 40 randomised maintenance requests with 0–3 messages each

## Configuration

### appsettings.json
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "<fallback-local-db>",
    "DirectoryConnection": "<fallback-local-db>"
  },
  "Ai": {
    "Provider": "Claude",
    "Model": "claude-sonnet-4-5",
    "MaxTokens": 1024
  },
  "Billing": {
    "Mode": "Pilot",
    "DefaultTier": "Tier1"
  },
  "Demo": {
    "Enabled": true,
    "SeedOnStartup": true
  }
}
```

## Project Structure

```
MaintenanceSandbox/
├── Areas/
│   └── Identity/          # Scaffolded Identity pages
├── Controllers/           # MVC controllers
│   ├── DemoUserController.cs   # Demo login + session switching
│   └── UsersAdminController.cs # Blocked for demo tenants
├── Data/                  # Business DbContext
│   ├── AppDbContext.cs
│   └── DbInitializer.cs   # Seeds sandbox + per-session demo tenants
├── Directory/
│   ├── Data/              # DirectoryDbContext
│   ├── Models/            # ApplicationUser, Tenant
│   └── Services/          # Tenant provisioning
├── Filters/
│   └── BlockDemoFilter.cs # Blocks mutating actions for demo tenants
├── Middleware/            # Subscription gate, etc.
├── Models/                # Business entities
│   ├── MasterData/        # Sites, Equipment, WorkCenters
│   └── MaintenanceRequest.cs
├── Security/              # Tenant claims transformation
├── Services/              # AI, Demo providers, TenantProvider
└── Program.cs             # App configuration
```

## Key Technologies
- ASP.NET Core 8 (MVC + Razor Views)
- Entity Framework Core 9
- ASP.NET Core Identity
- Azure SQL Database
- Microsoft Entra ID (Azure AD) Authentication
- Bogus (fake data generation)
- SignalR (real-time updates)
- Localization (en-CA, fr-CA, es-MX)

## Troubleshooting

### "Login failed for user '<token-identified principal>'"
**Cause:** Azure SQL configured for Entra ID only, but connection string uses SQL auth.

**Fix:** Use `Authentication=Active Directory Default` in connection strings (see Setup step 2).

### "Migrations conflict" on startup
**Cause:** EF Core migration history out of sync with existing Azure SQL tables.

**Fix:** In `Program.cs`, migrations are intentionally commented out — the Azure databases already exist:
```csharp
// businessDb.Database.Migrate(); // Commented out - Azure DB already exists
```

### "Tenant mismatch between databases"
**Cause:** Business database has tenant `5EFA6386...` but Identity database doesn't.

**Fix:** Run the INSERT statement in "Sync Tenant Across Databases" section above.

## Demo Tenant ID
The sandbox demo data uses this fixed tenant ID across both databases:
```
5EFA6386-80F0-4565-87D2-5170079B6BE0
```

## Development Notes
- Migrations are disabled on startup (databases already exist on Azure)
- Demo data seeds on every app start
- Tenant filtering is enforced via `ITenantProvider` and query filters
- Onboarding flow automatically assigns users to tenants

## Contributing
1. Fork the repository
2. Create a feature branch
3. Commit changes
4. Push to the branch
5. Create a Pull Request

## License
[Your License Here]

## Contact
[Your Contact Info]
