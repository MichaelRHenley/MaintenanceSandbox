# Maintenance Sandbox - Multi-Tenant SaaS Application

ASP.NET Core 8 Razor Pages application for managing maintenance requests with multi-tenant support, Azure SQL integration, and AI-powered assistance.

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

## Prerequisites
- .NET 8 SDK
- Azure SQL Database access
- Azure Entra ID authentication
- Visual Studio 2022 or VS Code
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

**Required for Azure AD Authentication:**
```bash
dotnet add package Azure.Identity
```

### 4. Database Setup

#### Azure SQL Firewall
1. Go to Azure Portal → SQL Server → **Networking**
2. Click **Add your client IPv4 address**
3. Save changes

#### Sync Tenant Across Databases
The demo data uses tenant `5EFA6386-80F0-4565-87D2-5170079B6BE0`. Ensure this exists in both databases:

**On SentinelMfgSuite_Identity:**
```sql
-- Add Sandbox Tenant
INSERT INTO dbo.Tenants (Id, Name, Status, CreatedUtc)
VALUES ('5EFA6386-80F0-4565-87D2-5170079B6BE0', 'Sandbox Tenant', 1, GETUTCDATE());
```

**On SentinelMfgSuite_Core:**
The tenant is auto-created by `DbInitializer.SeedAsync()` on first run.

### 5. Run Application
```bash
dotnet run
```

Or press **F5** in Visual Studio.

## Demo Data Access

### Create a User
1. Navigate to `/Identity/Account/Register`
2. Create an account with any email/password

### Link User to Demo Tenant
Run this SQL on **SentinelMfgSuite_Identity**:

```sql
UPDATE AspNetUsers 
SET TenantId = '5EFA6386-80F0-4565-87D2-5170079B6BE0'
WHERE Email = 'your-email@example.com';
```

### Login
- Go to `/Identity/Account/Login`
- Use your credentials
- You'll now see demo data:
  - 2 Sites (Site Alpha, Site Beta)
  - 2 Work Centers (WC-001, WC-002)
  - 4 Equipment items
  - 40 Bogus maintenance requests with messages

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

### Demo Users (Hardcoded - Dev Only)
Located in `Services/DemoUserProvider.cs`:
- **supervisor@sentinel-demo.local** / `demo`
- **operator@sentinel-demo.local** / `demo`

*Note: These do NOT use Identity database.*

## Project Structure

```
MaintenanceSandbox/
├── Areas/
│   └── Identity/          # Scaffolded Identity pages
├── Controllers/           # MVC controllers
├── Data/                  # Business DbContext
│   ├── AppDbContext.cs
│   └── DbInitializer.cs   # Seeds demo data
├── Directory/
│   ├── Data/              # DirectoryDbContext
│   ├── Models/            # ApplicationUser, Tenant
│   └── Services/          # Tenant provisioning
├── Middleware/            # Subscription gate, etc.
├── Models/                # Business entities
│   ├── MasterData/        # Sites, Equipment, WorkCenters
│   └── MaintenanceRequest.cs
├── Pages/                 # Razor Pages
├── Security/              # Tenant claims transformation
├── Services/              # AI, Demo providers
└── Program.cs             # App configuration
```

## Key Technologies
- ASP.NET Core 8 (Razor Pages)
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

### "No data visible after login"
**Cause:** User's `TenantId` doesn't match the demo tenant.

**Fix:** Run SQL update command in "Link User to Demo Tenant" section.

### "Migrations conflict" on startup
**Cause:** EF Core migration history out of sync with existing Azure SQL tables.

**Fix:** In `Program.cs`, migrations are commented out:
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
