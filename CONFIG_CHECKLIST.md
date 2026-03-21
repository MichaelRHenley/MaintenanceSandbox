# Production Config Checklist — Sentinel Maintenance Suite
> Run through this before every deployment to the IIS staging VM.

---

## Environment Variables — Azure VM (IIS)

Set via one of two methods (both work; IIS Manager is scoped to the app, system env vars are
machine-wide):

**Option A — IIS Manager (recommended — scoped to this app):**
IIS Manager → Sites → `MaintenanceSandbox` → Configuration Editor →
`system.webServer/aspNetCore` → `environmentVariables` → Add

**Option B — System environment variables (machine-wide):**
Windows System Properties → Environment Variables → System Variables → New

Use **double-underscore (`__`)** to map nested JSON keys.

---

### Required — App will not start or function without these

| Environment Variable | Example / Notes |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` — enables `appsettings.Production.json` overrides, disables developer exception page |
| `ConnectionStrings__DefaultConnection` | Azure SQL business DB (`SentinelMfgSuite_Core`). Do not use LocalDB — it is inaccessible to IIS service accounts. |
| `ConnectionStrings__DirectoryConnection` | Azure SQL identity/directory DB (`SentinelMfgSuite_Identity`) |

---

### Required — Features broken without these

| Environment Variable | Example / Notes |
|---|---|
| `Ai__ApiKey` | `sk-ant-api03-...` — Anthropic Claude key. AI Help `?` panel is disabled (`NullChatModel`) if missing. |
| `Demo__EmailLinkSecret` | Any 32+ character random string. HMAC-signs demo share links. App starts without it but demo sharing is broken. |

---

### Data Protection — Critical for IIS

| Item | Action |
|---|---|
| `DataProtection:KeysPath` folder | Create `C:\inetpub\dpkeys\MaintenanceSandbox\` on the VM **before first launch** |
| App pool identity permissions | Grant `IIS AppPool\<pool-name>` **Modify** on the dpkeys folder: `icacls "C:\inetpub\dpkeys\MaintenanceSandbox" /grant "IIS AppPool\MaintenanceSandbox:(OI)(CI)M"` |
| Config override (optional) | Set env var `DataProtection__KeysPath` to override the path in `appsettings.Production.json` |

> **Why this matters:** Without persisted keys, every IIS app pool recycle invalidates all auth
> cookies, session tokens, and antiforgery tokens — logging every active user out silently.

---

### Email — Optional (NullEmailService if missing)

| Environment Variable | Example |
|---|---|
| `Email__SmtpHost` | `smtp.sendgrid.net` |
| `Email__SmtpPort` | `587` |
| `Email__SmtpUser` | `apikey` (SendGrid) |
| `Email__SmtpPassword` | `SG.xxxx...` |
| `Email__FromAddress` | `noreply@yourdomain.com` |

> Without these, `NullEmailService` is registered. App works fully except invite and demo-share
> emails are silently dropped.

---

### Analytics — Optional (omit to disable tracking)

| Environment Variable | Example |
|---|---|
| `Analytics__GA4MeasurementId` | `G-XXXXXXXXXX` |
| `Analytics__ClarityProjectId` | `abcde12345` |

---

## Azure SQL — Firewall

- [ ] Add the VM's outbound IP to the Azure SQL Server firewall:
      Portal → SQL Server → Networking → Firewall rules → Add your VM's IP
- [ ] If using SQL auth, verify username/password in the connection string
- [ ] If using Entra ID auth (`Authentication=Active Directory Default`), ensure the VM's
      managed identity or the app pool service account has access to the SQL server

---

## IIS — App Pool Settings

| Setting | Required value |
|---|---|
| .NET CLR Version | **No Managed Code** |
| Pipeline Mode | Integrated |
| Platform | 64-bit |
| Identity | `ApplicationPoolIdentity` or dedicated service account |
| Idle Time-out | `0` (disabled) |
| Start Mode | `AlwaysRunning` |

---

## IIS — Site Settings

| Setting | Required value |
|---|---|
| Physical path | `C:\inetpub\wwwroot\MaintenanceSandbox\` (or your site root) |
| App pool | The pool configured above (No Managed Code) |
| Bindings | HTTPS on port 443 with a valid SSL certificate |
| HTTPS redirect | Configured in IIS or handled by `app.UseHttpsRedirection()` |

---

## Pre-deploy Checklist

- [ ] `dotnet build -c Release` passes locally with 0 errors
- [ ] All 7 resource files present: `en-CA`, `fr-CA`, `es-MX`, `de-DE`, `it-IT`, `sv-SE`, `fi-FI`
- [ ] `ASPNETCORE_ENVIRONMENT=Production` is set on the VM
- [ ] `ConnectionStrings__DefaultConnection` points to Azure SQL (not LocalDB)
- [ ] `ConnectionStrings__DirectoryConnection` points to Azure SQL (not LocalDB)
- [ ] `Ai__ApiKey` is set (Claude features)
- [ ] `Demo__EmailLinkSecret` is set
- [ ] Azure SQL firewall allows the VM's IP
- [ ] Pending migrations applied manually before deploy (see `DEPLOYMENT.md` Step 3)
- [ ] `C:\inetpub\dpkeys\MaintenanceSandbox\` exists and app pool identity has Modify rights
- [ ] `C:\inetpub\wwwroot\MaintenanceSandbox\logs\` exists (for stdout troubleshooting — Write rights)
- [ ] .NET 8 Hosting Bundle installed on VM (`dotnet --version` → `8.x.x`)
- [ ] IIS WebSocket Protocol feature installed (Server Manager → Add Roles)
- [ ] App pool set to **No Managed Code**
- [ ] SSL certificate bound to the site in IIS

---

## Post-deploy Smoke Test

- [ ] `https://<vm-hostname>/` — Home page loads without errors
- [ ] `https://<vm-hostname>/Account/Login` — Login form renders
- [ ] Demo login: `operator@sentinel-demo.local` / `sentineldemo`
- [ ] `/Maintenance` — Index loads with seeded demo data
- [ ] SignalR live indicator turns green within 5 seconds (confirms WebSocket is working)
- [ ] `?` help button returns an AI response (Claude — requires `Ai__ApiKey`)
- [ ] Settings → Language dropdown shows all 7 languages
- [ ] Log out and log back in — session survives (confirms Data Protection keys are persisted)
- [ ] `/SentinelAdmin` — accessible with `SentinelAdmin` role, redirects to login otherwise
- [ ] `/SentinelAdmin/TenantHealth` — tenant health dashboard loads
- [ ] Mobile: navbar hamburger opens/closes, no horizontal scroll

---

## Known Gaps for Pilot (Not Blocking)

| Gap | Impact | Planned fix |
|---|---|---|
| AI Assist (Ollama) unavailable on VM | `✦` modal shows "temporarily unavailable" | Install Ollama on VM, or swap orchestrator to Claude |
| No `/health` endpoint | IIS has no structured health probe | Add `MapHealthChecks("/health")` to `Program.cs` |
| No staging slot / blue-green | Rollback = full folder redeploy | Set up second IIS site as staging, use `robocopy` swap |
| Email invite links not tested end-to-end | Invites silently dropped if SMTP not configured | Configure SendGrid, run invite flow smoke test |
| `SentinelAdmin` role must be manually assigned | No UI for role assignment | Assign via direct SQL or add a bootstrap step in `Program.cs` |

