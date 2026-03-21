# Maintenance Sandbox — Sentinel Multi-Tenant SaaS Platform

ASP.NET Core 8 MVC application for managing maintenance requests with full multi-tenant SaaS
architecture, a Sentinel operator control plane, AI-powered assistance, real-time SignalR
updates, and a fully isolated demo mode. Deployed to an Azure VM running IIS.

## Architecture

### Two-Database Design
- **SentinelMfgSuite_Core** (Business Database)
  - Tenant data, provisioning state, sites, work centers, equipment
  - Maintenance requests, messages, production logs
  - Multi-tenant filtered via `TenantId` EF global query filters
  - Append-only provisioning audit log (`TenantProvisioningEvent`)

- **SentinelMfgSuite_Identity** (Directory Database)
  - ASP.NET Identity (users, roles)
  - Tenant subscriptions and billing
  - User-tenant associations and invites

### Sentinel SaaS Control Plane

The `/SentinelAdmin` area is a Sentinel-operator–only control plane (`[Authorize(Roles = "SentinelAdmin")]`):

| Feature | Route | Description |
|---|---|---|
| Tenant list | `/SentinelAdmin` | All tenants with provisioning status |
| Health dashboard | `/SentinelAdmin/TenantHealth` | Color-coded health cards, stale detection, retry button |
| Provisioning history | `/SentinelAdmin/TenantProvisioningHistory?tenantId=...` | Grouped attempt cards with per-event detail |
| Suspend / Reactivate | POST actions | Lifecycle transitions via `ITenantLifecycleService` |

**Provisioning lifecycle:** `Pending → Provisioning → Ready | Failed | Suspended`
enforced by `ProvisioningStatusGateMiddleware`.

**Audit log:** Every provisioning action is written to `TenantProvisioningEvent` (append-only,
never updated or deleted). Events are grouped by `CorrelationId` on the history page so each
provisioning attempt or retry is visually distinct.

### Key Features
- **Multi-tenancy**: Row-level isolation via `TenantId` + EF Core global query filters
- **Identity**: ASP.NET Core Identity with custom `ApplicationUser`
- **Azure SQL**: Microsoft Entra ID (Azure AD) authentication
- **Data Protection**: Keys persisted to `C:\inetpub\dpkeys\MaintenanceSandbox\` — auth cookies
  survive IIS app pool recycles
- **AI Assist**: Floating ✦ modal (all pages) — Ask / Command / Troubleshoot modes backed by a
  local Ollama LLM with tool-calling for incident search, equipment status, and create-incident drafting
- **AI Integration**: Claude AI for onboarding guidance and the contextual `?` help panel
- **Onboarding Flow**: Guided tenant provisioning and user setup
- **Subscription Management**: Stripe integration (pilot mode active)
- **Demo Mode**: Per-session isolated tenants with auto-seeded data and 2-hour auto-purge
- **Localization**: 7 languages — `en-CA`, `fr-CA`, `es-MX`, `de-DE`, `it-IT`, `sv-SE`, `fi-FI`
- **Real-time**: SignalR hub at `/hubs/maintenance` (requires IIS WebSocket Protocol feature)

## Prerequisites
- .NET 8 SDK (dev machine)
- Azure SQL Database access
- Azure Entra ID authentication (or SQL auth connection strings)
- Visual Studio 2022+ or VS Code
- **IIS on target VM** with .NET 8 Hosting Bundle and WebSocket Protocol feature (see `DEPLOYMENT.md`)
- Ollama (optional — local AI Assist only)
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
role=Supervisor
```

This endpoint is `[AllowAnonymous]` and signs the visitor directly into the Maintenance dashboard with a fresh isolated tenant. No registration or password required. Use `role=Operator` for the operator perspective.

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

### Email Demo Sharing

The **📧 Share** button in the demo nav pill lets a demo user send a one-time login link to any email address. The recipient lands directly in the **same isolated tenant** — no password required.

#### How it works
1. Demo user clicks **📧 Share** in the top-right nav
2. Enters an email address and picks a role (Supervisor / Operator / Tech)
3. App generates a time-limited HMAC-signed token (default: 30 min)
4. An email is sent with a `/DemoUser/JoinDemo?token=…` link
5. Recipient clicks the link → signed in to the same demo tenant instantly

#### Fallback (no email configured — works out of the box)
If `Email:SmtpHost` is not set, the modal shows a **copyable link** instead of sending email. The share feature works immediately with no mail server setup required.

#### Activating real email delivery (SMTP)

Any SMTP provider works (SendGrid, Mailgun, AWS SES, Gmail, etc.).

**Step 1 — Gather your SMTP credentials**

For **SendGrid**:
1. Create a SendGrid account → Settings → API Keys → Create API Key (Mail Send permission)
2. Host: `smtp.sendgrid.net`, Port: `587`, User: `apikey`, Password: your API key

For **Gmail** (dev/testing only):
1. Enable 2-factor auth → Google Account → Security → App Passwords → generate one
2. Host: `smtp.gmail.com`, Port: `587`, User: your Gmail address, Password: the app password

**Step 2 — Set secrets locally**
```bash
dotnet user-secrets set "Email:SmtpHost"     "smtp.sendgrid.net"
dotnet user-secrets set "Email:SmtpPort"     "587"
dotnet user-secrets set "Email:SmtpUser"     "apikey"
dotnet user-secrets set "Email:SmtpPassword" "SG.xxxx..."
dotnet user-secrets set "Email:FromAddress"  "noreply@yourdomain.com"

# Any random string, 32+ characters
dotnet user-secrets set "Demo:EmailLinkSecret" "replace-with-a-long-random-secret-string"
```

**Step 3 — Set environment variables in production**
```
Email__SmtpHost        = smtp.sendgrid.net
Email__SmtpPort        = 587
Email__SmtpUser        = apikey
Email__SmtpPassword    = SG.xxxx...
Email__FromAddress     = noreply@yourdomain.com
Demo__EmailLinkSecret  = <32+ char secret>
Demo__EmailLinkExpiryMinutes = 30
```

> **Tip:** Generate a strong `EmailLinkSecret` with:
> ```bash
> node -e "console.log(require('crypto').randomBytes(32).toString('hex'))"
> ```

## AI Assist

The ✦ AI Assist panel is accessible from every page via a fixed floating button in the bottom-right corner. Clicking it opens a modal with three modes:

| Mode | What it does |
|------|--------------|
| **Ask** | Natural-language queries against your incident history and equipment data |
| **Command** | Direct tool invocations — search incidents, check equipment status |
| **Troubleshoot** | Guided diagnosis that can draft a pre-filled Create Incident form |

### Local Setup (Ollama)

AI Assist uses a local [Ollama](https://ollama.ai) instance — no cloud API key required.

```bash
# 1. Install Ollama (https://ollama.ai/download)
# 2. Pull the model
ollama pull llama3.2
# 3. Start the server (runs on http://localhost:11434 by default)
ollama serve
```

Configure the endpoint in `appsettings.json` (or override via user secrets):

```json
"Ollama": {
  "BaseUrl": "http://localhost:11434",
  "ChatModel": "llama3.2"
}
```

If Ollama is not running, the modal shows a friendly error — no other functionality is affected.

### Create Incident from AI

When the AI returns a `create_incident` suggested action, clicking **✔ Create Incident** navigates
to `GET /Maintenance/Create` with all available fields pre-filled (description, priority, area,
work centre, equipment). Cascade dropdowns preserve AI-filled values through selection changes.
The form submits via `POST` with antiforgery protection.

### Audit Trail

Every AI session, message, and tool call is recorded in three tables:
`AiConversationSession`, `AiConversationMessage`, `AiToolAudit`.

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
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "ChatModel": "llama3.2"
  },
  "Analytics": {
    "GA4MeasurementId": "",
    "ClarityProjectId": ""
  },
  "Billing": {
    "Mode": "Pilot",
    "DefaultTier": "Tier1"
  },
  "Demo": {
    "Enabled": true,
    "SeedOnStartup": true,
    "EmailLinkSecret": "",
    "EmailLinkExpiryMinutes": 30
  },
  "Email": {
    "SmtpHost": "",
    "SmtpPort": 587,
    "SmtpUser": "",
    "SmtpPassword": "",
    "FromAddress": ""
  }
}
```

Set secrets via `dotnet user-secrets` locally (see Setup section) or via environment variables in production using double-underscore notation (e.g. `Demo__SmsLinkSecret`). Leave `Analytics`, `Sms`, and `Demo:SmsLinkSecret` empty in development — the features degrade gracefully (no scripts injected, copyable link shown instead of SMS).

## Project Structure

```
MaintenanceSandbox/
├── Areas/
│   └── Identity/              # Scaffolded Identity pages
├── Controllers/               # MVC controllers
│   ├── DemoUserController.cs  # Demo login + session switching
│   ├── SentinelAdminController.cs  # Operator control plane (SentinelAdmin role)
│   └── UsersAdminController.cs     # Blocked for demo tenants
├── Data/                      # Business DbContext
│   ├── AppDbContext.cs        # TenantId global query filters, model configuration
│   └── DbInitializer.cs       # Seeds sandbox + per-session demo tenants
├── Directory/
│   ├── Data/                  # DirectoryDbContext (Identity + Tenants)
│   ├── Models/                # ApplicationUser, Tenant, TenantSubscription
│   └── Services/              # Tenant provisioning
├── Filters/
│   └── BlockDemoFilter.cs     # Blocks mutating actions for demo tenants
├── Middleware/                 # TenantContextMiddleware, ProvisioningStatusGateMiddleware,
│                              # SubscriptionGateMiddleware
├── Models/
│   ├── MasterData/            # Site, Area, WorkCenter, Equipment
│   ├── Base/                  # TenantEntity base class
│   ├── MaintenanceRequest.cs
│   └── TenantProvisioningEvent.cs  # Append-only provisioning audit log
├── Properties/
│   └── PublishProfiles/
│       ├── AzureWebApp.pubxml      # Azure App Service profile (reference only)
│       └── IISFolderPublish.pubxml # Active: folder publish for IIS VM deploy
├── Security/                  # TenantClaimsTransformation
├── Services/
│   ├── ITenantLifecycleService.cs  # Provisioning, health, history, suspend/reactivate
│   ├── TenantLifecycleService.cs
│   ├── ITenantOperationalProvisioner.cs
│   ├── TenantOperationalProvisioner.cs
│   ├── ITenantProvisioningAuditLogger.cs  # Append-only event logger
│   ├── TenantProvisioningAuditLogger.cs
│   └── Ai/                    # Ollama orchestrator, Claude models, AI tools
│       ├── AiOrchestrator.cs          # 2-turn intent → tool → response pipeline
│       ├── IncidentAiTools.cs         # DB-backed incident search & draft tools
│       ├── OllamaService.cs           # Direct HTTP to Ollama REST API
│       └── PromptLibrary.cs           # All system + user prompt templates
├── ViewModels/Admin/
│   ├── TenantHealthSummaryVm.cs
│   ├── TenantProvisioningEventVm.cs
│   ├── TenantProvisioningAttemptVm.cs  # Groups events by CorrelationId
│   └── TenantProvisioningHistoryVm.cs  # Top-level grouped history model
├── Views/SentinelAdmin/
│   ├── Index.cshtml
│   ├── TenantHealth.cshtml             # Health dashboard with retry button
│   └── TenantProvisioningHistory.cshtml # Grouped attempt cards
├── Views/Shared/
│   ├── _AiAssistModal.cshtml  # Floating FAB + modal (rendered in layout for all users)
│   └── _AiHelpLauncher.cshtml # Contextual ? help panel (per-page)
└── web.config                 # ANCM template — requestLimits override, stdout log docs
```

## Key Technologies
- ASP.NET Core 8 (MVC + Razor Views)
- Entity Framework Core 9
- ASP.NET Core Identity
- Azure SQL Database
- Microsoft Entra ID (Azure AD) Authentication
- ASP.NET Core Data Protection (file system key persistence for IIS)
- IIS + ANCM v2 (deployment target — Azure VM)
- Bogus (fake data generation)
- SignalR (real-time updates — requires IIS WebSocket Protocol)
- Localization (`en-CA`, `fr-CA`, `es-MX`, `de-DE`, `it-IT`, `sv-SE`, `fi-FI`)
- Ollama (local LLM runtime — `llama3.2` for AI Assist incident intelligence)
- Anthropic Claude (onboarding guidance & contextual help panel)

## Deployment

See `DEPLOYMENT.md` for the full step-by-step IIS deployment guide.
See `CONFIG_CHECKLIST.md` for the pre/post deploy checklist and all required environment variables.

**Quick summary:**
1. `dotnet publish -c Release -r win-x64 --no-self-contained -o .\publish`
2. `robocopy .\publish\ \\VM-NAME\c$\inetpub\wwwroot\MaintenanceSandbox\ /MIR /XD logs`
3. IIS app pool: **No Managed Code**, 64-bit, AlwaysRunning
4. Set env vars via IIS Manager → Configuration Editor → `system.webServer/aspNetCore`
5. Create `C:\inetpub\dpkeys\MaintenanceSandbox\` and grant app pool identity Modify rights

## Troubleshooting

### "HTTP 500.30 — ANCM In-Process Handler Load Failure" on IIS
**Cause:** Most commonly the app pool is set to a managed .NET CLR version instead of "No Managed Code", or the .NET 8 Hosting Bundle is not installed.

**Fix:**
1. Set app pool → .NET CLR Version = **No Managed Code**
2. Verify Hosting Bundle is installed: run `dotnet --version` in a new cmd prompt on the VM
3. Enable stdout logging in `web.config` (`stdoutLogEnabled="true"`) and read `.\logs\stdout_*.log`

### Everyone gets logged out after IIS recycle
**Cause:** Data Protection keys are ephemeral (not persisted to disk).

**Fix:** Create `C:\inetpub\dpkeys\MaintenanceSandbox\`, grant the app pool identity Modify
rights, and verify `DataProtection:KeysPath` in `appsettings.Production.json` points to it.
See `CONFIG_CHECKLIST.md` — Data Protection section.

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

**Fix:** Run the INSERT statement in the database setup section above.

### SentinelAdmin panel shows 403 / Access Denied
**Cause:** The `SentinelAdmin` role is not assigned to your user.

**Fix:** The role is created automatically on startup. Assign it via direct SQL on the Identity DB:
```sql
-- Get role ID
SELECT Id FROM AspNetRoles WHERE Name = 'SentinelAdmin';
-- Get user ID
SELECT Id FROM AspNetUsers WHERE Email = 'your@email.com';
-- Assign
INSERT INTO AspNetUserRoles (UserId, RoleId) VALUES ('<userId>', '<roleId>');
```

## Demo Tenant ID
The sandbox demo data uses this fixed tenant ID across both databases:
```
5EFA6386-80F0-4565-87D2-5170079B6BE0
```

## Development Notes
- Migrations are disabled on startup — run manually with `dotnet ef database update` before deploying
- Demo data seeds on every app start (`DbInitializer.SeedAsync`)
- Tenant filtering enforced via `ITenantProvider` and EF global query filters
- `TenantProvisioningEvent` has **no global query filter** — admin reads are intentionally cross-tenant
- Onboarding flow automatically assigns users to tenants
- All user-facing strings use `IStringLocalizer<SharedResource>` — 7 locale files under `Resources\`

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
