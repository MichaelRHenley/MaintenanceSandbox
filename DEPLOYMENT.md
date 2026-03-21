# Deployment Guide — Sentinel Maintenance Suite
> Generated during pilot stabilization pass. Update this file after each deployment.

---

## Prerequisites

| Tool | Purpose | Install |
|---|---|---|
| Azure CLI | Resource management, app deploy | `winget install Microsoft.AzureCLI` |
| .NET 8 SDK | Build | [dotnet.microsoft.com](https://dotnet.microsoft.com/download) |
| Git | Source control | already installed |

---

## Step 1 — Verify local build is clean

```powershell
cd C:\Dev\repos\MaintenanceSandbox
dotnet build -c Release
```

Expected: `Build succeeded. 0 Error(s)`

---

## Step 2 — Set production secrets (one-time per machine)

Never commit real secrets. Use Azure App Service → Configuration → Application Settings.

Required settings (set in Portal or via CLI):

```powershell
$rg  = "your-resource-group"
$app = "your-app-service-name"

az webapp config appsettings set -g $rg -n $app --settings `
  "Ai__ApiKey=sk-ant-..." `
  "Demo__EmailLinkSecret=$(New-Guid)" `
  "Email__SmtpHost=smtp.sendgrid.net" `
  "Email__SmtpPort=587" `
  "Email__SmtpUser=apikey" `
  "Email__SmtpPassword=SG.xxx" `
  "Email__FromAddress=noreply@yourdomain.com"
```

Connection strings go under **Connection Strings** tab (not App Settings):

```powershell
az webapp config connection-string set -g $rg -n $app --connection-string-type SQLAzure --settings `
  DefaultConnection="Server=tcp:sentinaldb.database.windows.net,1433;Initial Catalog=SentinelMfgSuite_Core;Authentication=Active Directory Default;Encrypt=Mandatory;MultipleActiveResultSets=True;" `
  DirectoryConnection="Server=tcp:sentinaldb.database.windows.net,1433;Initial Catalog=SentinelMfgSuite_Identity;Authentication=Active Directory Default;Encrypt=Mandatory;MultipleActiveResultSets=True;"
```

---

## Step 3 — Publish

### Option A — Visual Studio (quickest)

1. Right-click project → **Publish**
2. Select **AzureWebApp** profile (or create one targeting your App Service)
3. Click **Publish**
4. Watch Output window for success

### Option B — CLI (repeatable / CI-friendly)

```powershell
# 1. Produce a release build into ./publish
dotnet publish -c Release -o ./publish

# 2. Zip it
Compress-Archive -Path ./publish/* -DestinationPath ./publish.zip -Force

# 3. Deploy to Azure App Service
az webapp deploy `
  --resource-group $rg `
  --name $app `
  --src-path ./publish.zip `
  --type zip

# 4. Confirm the app is running
az webapp show -g $rg -n $app --query state -o tsv
```

Expected output: `Running`

---

## Step 4 — Verify deployment

Open in browser:

```
https://<your-app-name>.azurewebsites.net
```

Quick smoke-test checklist:
- [ ] Home page loads without errors
- [ ] Login page renders (`/Account/Login`)
- [ ] Demo login works (operator@sentinel-demo.local / demo)
- [ ] Maintenance index loads with seeded data
- [ ] Live indicator shows "Live" (green) within 5 seconds
- [ ] Language dropdown in Settings shows all 7 languages
- [ ] AI Help `?` button opens panel and returns a response (Claude)
- [ ] AI Assist `✦` button opens modal (Ollama — will show "unavailable" on Azure, expected)

---

## Step 5 — Mobile test checklist

Open on phone: `https://<your-app-name>.azurewebsites.net`

| Test | Expected |
|---|---|
| Home page layout | No horizontal scroll, text readable |
| Navbar burger menu | Opens / closes correctly |
| Maintenance index — cards | Full width, labels legible |
| Details page — two-column layout | Stacks to single column below lg breakpoint |
| Comment form | Text area and button visible without zooming |
| Settings page — language dropdown | Touch-friendly, full width |
| Login form | Keyboard doesn't push form off screen |

---

## Step 6 — Database migrations (if schema changed)

> The app runs `DbInitializer.SeedAsync` on startup. New migrations must be applied manually before deploying.

```powershell
# Against Azure SQL (requires firewall to allow your IP)
dotnet ef database update --connection "Server=tcp:sentinaldb.database.windows.net,1433;..." --context AppDbContext
dotnet ef database update --connection "Server=tcp:sentinaldb.database.windows.net,1433;..." --context DirectoryDbContext
```

---

## Known behaviours in production

| Behaviour | Reason | Action |
|---|---|---|
| AI Assist modal returns "temporarily unavailable" | Ollama requires a local GPU process — not deployed to Azure | Expected. AI Help `?` panel still works via Claude. |
| Demo tenants auto-purge after 2 hours | By design | None |
| First request after cold start is slow (~5 s) | App Service free/shared tier cold start | Upgrade to Basic or higher, or use Always On |

---

## Rollback

```powershell
# List recent deployments
az webapp deployment list -g $rg -n $app -o table

# Roll back one slot (if staging slot is configured)
az webapp deployment slot swap -g $rg -n $app --slot staging --target-slot production
```

---

## Useful commands

```powershell
# Stream live logs
az webapp log tail -g $rg -n $app

# Restart app
az webapp restart -g $rg -n $app

# Check environment variables are set
az webapp config appsettings list -g $rg -n $app -o table
```
