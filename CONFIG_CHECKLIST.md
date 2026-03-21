# Production Config Checklist — Sentinel Maintenance Suite
> Run through this before every production deployment.

---

## Azure App Service — Application Settings

Set via: **Portal → App Service → Configuration → Application settings**  
Or CLI: `az webapp config appsettings set -g <rg> -n <app> --settings "Key=Value"`

| Setting | Required | Example / Notes |
|---|---|---|
| `Ai__ApiKey` | ✅ Yes | `sk-ant-api03-...` — Anthropic Claude key. AI Help panel is disabled (NullChatModel) if missing. |
| `Demo__EmailLinkSecret` | ✅ Yes | Any 32+ character random string. Used to HMAC-sign demo share links. |
| `ASPNETCORE_ENVIRONMENT` | ✅ Yes | Must be `Production` |

---

## Azure App Service — Connection Strings

Set via: **Portal → App Service → Configuration → Connection strings** (Type: `SQLAzure`)  
Or CLI: `az webapp config connection-string set ...`

| Name | Required | Notes |
|---|---|---|
| `DefaultConnection` | ✅ Yes | Azure SQL business DB (`SentinelMfgSuite_Core`) |
| `DirectoryConnection` | ✅ Yes | Azure SQL identity/directory DB (`SentinelMfgSuite_Identity`) |

---

## Email (optional — NullEmailService used if missing)

| Setting | Required | Example |
|---|---|---|
| `Email__SmtpHost` | ⚠️ For invite emails | `smtp.sendgrid.net` |
| `Email__SmtpPort` | ⚠️ For invite emails | `587` |
| `Email__SmtpUser` | ⚠️ For invite emails | `apikey` (SendGrid) |
| `Email__SmtpPassword` | ⚠️ For invite emails | `SG.xxxx...` |
| `Email__FromAddress` | ⚠️ For invite emails | `noreply@yourdomain.com` |

> Without these, `NullEmailService` is registered. App works fully except invite/share emails are silently dropped.

---

## Analytics (optional — omit to disable)

| Setting | Required | Example |
|---|---|---|
| `Analytics__GA4MeasurementId` | ❌ Optional | `G-XXXXXXXXXX` |
| `Analytics__ClarityProjectId` | ❌ Optional | `abcde12345` |

---

## Azure SQL — Firewall

Before running migrations or connecting from dev:

- [ ] Portal → SQL Server → **Networking** → Add your IP
- [ ] App Service outbound IPs added to SQL Server firewall (or use VNet integration)

To get App Service outbound IPs:
```powershell
az webapp show -g <rg> -n <app> --query outboundIpAddresses -o tsv
```

---

## Azure App Service — General Settings

| Setting | Recommended value |
|---|---|
| .NET version | `.NET 8 (LTS)` |
| Platform | `64-bit` |
| Always On | `On` (Basic tier or higher — prevents cold start) |
| HTTPS Only | `On` |
| Minimum TLS | `1.2` |
| ARR Affinity | `Off` (stateless app — no benefit, adds sticky-session overhead) |

---

## Pre-deploy checklist

- [ ] `dotnet build -c Release` passes locally with 0 errors
- [ ] All 7 resource files present: `en-CA`, `fr-CA`, `es-MX`, `de-DE`, `it-IT`, `sv-SE`, `fi-FI`
- [ ] `ASPNETCORE_ENVIRONMENT=Production` is set on App Service
- [ ] `Ai__ApiKey` is set (Claude features)
- [ ] Both connection strings are set and point to Azure SQL
- [ ] SQL Server firewall allows App Service outbound IPs
- [ ] `Demo__EmailLinkSecret` is set
- [ ] HTTPS Only is enabled
- [ ] Always On is enabled (Basic tier+)

---

## Post-deploy smoke test

- [ ] `/` — Home page loads
- [ ] `/Account/Login` — Login form renders
- [ ] Demo login: `operator@sentinel-demo.local` / `demo`
- [ ] `/Maintenance` — Index loads with seeded demo data
- [ ] SignalR live indicator turns green within 5 seconds
- [ ] `?` help button returns an AI response (Claude)
- [ ] Settings → Language dropdown shows 7 languages
- [ ] Mobile: navbar hamburger opens/closes, no horizontal scroll

---

## Known gaps for pilot (not blocking)

| Gap | Impact | Planned fix |
|---|---|---|
| AI Assist (Ollama) unavailable on Azure | `✦` modal shows "temporarily unavailable" | Deploy to Azure Container Instance with GPU SKU, or swap to Claude for Assist |
| No staging slot configured | Rollback requires full redeploy | Add staging slot in Portal |
| No health check endpoint | Azure can't probe app health | Add `/health` endpoint via `MapHealthChecks` |
| Email invite links not tested end-to-end | Invites silently dropped if SMTP not configured | Set up SendGrid and test invite flow |
