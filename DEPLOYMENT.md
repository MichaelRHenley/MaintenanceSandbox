# Deployment Guide — Sentinel Maintenance Suite
> Target: Azure VM running IIS with .NET 8 Hosting Bundle.
> Update this file after each deployment.

---

## Prerequisites

| Tool / Component | Where |
|---|---|
| .NET 8 SDK (dev machine) | [dotnet.microsoft.com/download/dotnet/8.0](https://dotnet.microsoft.com/download/dotnet/8.0) |
| .NET 8 **Hosting Bundle** (VM) | Same page → "Hosting Bundle" installer — installs ANCM v2 + ASP.NET Core runtime |
| IIS with WebSocket Protocol (VM) | Server Manager → Add Roles → Web Server → Application Development → WebSocket Protocol |
| Git | Already installed |

Run `iisreset` on the VM after installing the Hosting Bundle.

---

## Step 1 — Verify local build is clean

```powershell
cd C:\Dev\repos\MaintenanceSandbox
dotnet build -c Release
```

Expected: `Build succeeded. 0 Error(s)`

---

## Step 2 — Apply pending migrations (if schema changed)

Migrations must be applied **before** deploying the new app binaries. The app does not
run `Database.Migrate()` on startup — migrations are manual.

```powershell
# Replace with your production connection strings
$bizConn = "Server=tcp:yourserver.database.windows.net,1433;Initial Catalog=SentinelMfgSuite_Core;..."
$dirConn = "Server=tcp:yourserver.database.windows.net,1433;Initial Catalog=SentinelMfgSuite_Identity;..."

dotnet ef database update --connection $bizConn --context AppDbContext
dotnet ef database update --connection $dirConn --context DirectoryDbContext
```

> Your dev machine IP must be in the Azure SQL Server firewall before running these.

---

## Step 3 — Publish to folder

### Option A — Visual Studio

1. Right-click project → **Publish**
2. Select **IISFolderPublish** profile
3. Click **Publish**
4. Output lands in `.\publish\`

### Option B — CLI

```powershell
dotnet publish -c Release -r win-x64 --no-self-contained -o .\publish
```

---

## Step 4 — Copy to VM

```powershell
# Replace with your VM name or IP and site root path
$vmSiteRoot = "\\YOUR-VM-NAME\c$\inetpub\wwwroot\MaintenanceSandbox"

# /MIR = mirror (delete removed files), /XD logs = preserve logs folder on server
robocopy .\publish\ $vmSiteRoot /MIR /XD logs /NFL /NDL
```

> If you don't have a UNC share set up, use RDP to copy the `publish` folder manually,
> or `scp` if OpenSSH is enabled on the VM.

---

## Step 5 — First-time VM setup (one-time only)

### a) Create required folders

On the VM (run as Administrator):

```powershell
# Site root logs folder (for stdout troubleshooting)
New-Item -ItemType Directory -Force "C:\inetpub\wwwroot\MaintenanceSandbox\logs"

# Data Protection key ring (must survive deployments — do NOT put inside site root)
New-Item -ItemType Directory -Force "C:\inetpub\dpkeys\MaintenanceSandbox"
```

### b) Grant app pool identity permissions

```powershell
$pool = "MaintenanceSandbox"   # your IIS app pool name

# Site root: Read + Execute
icacls "C:\inetpub\wwwroot\MaintenanceSandbox" /grant "IIS AppPool\${pool}:(OI)(CI)RX"

# Logs folder: Write (only needed when stdout logging is enabled for troubleshooting)
icacls "C:\inetpub\wwwroot\MaintenanceSandbox\logs" /grant "IIS AppPool\${pool}:(OI)(CI)W"

# Data Protection keys: Modify (read + write + delete old keys)
icacls "C:\inetpub\dpkeys\MaintenanceSandbox" /grant "IIS AppPool\${pool}:(OI)(CI)M"
```

### c) Create IIS app pool

| Setting | Value |
|---|---|
| Name | `MaintenanceSandbox` |
| .NET CLR Version | **No Managed Code** |
| Pipeline Mode | Integrated |
| Platform (Advanced Settings) | `64-bit` |
| Start Mode | `AlwaysRunning` |
| Idle Time-out | `0` |

### d) Create IIS site

- Physical path: `C:\inetpub\wwwroot\MaintenanceSandbox\`
- App pool: `MaintenanceSandbox`
- Binding: HTTPS, port 443, with your SSL certificate

### e) Set environment variables

In IIS Manager → Sites → `MaintenanceSandbox` → Configuration Editor →
`system.webServer/aspNetCore` → `environmentVariables`:

| Name | Value |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__DefaultConnection` | `Server=tcp:yourserver...` |
| `ConnectionStrings__DirectoryConnection` | `Server=tcp:yourserver...` |
| `Ai__ApiKey` | `sk-ant-...` |
| `Demo__EmailLinkSecret` | *(32+ char random string)* |
| `Email__SmtpHost` | `smtp.sendgrid.net` *(if sending email)* |

See `CONFIG_CHECKLIST.md` for the full variable list.

---

## Step 6 — Enable and assign SentinelAdmin role

The `SentinelAdmin` role is created automatically on first startup. Assign it to your operator
account directly in the `SentinelMfgSuite_Identity` database:

```sql
-- Find the role ID
SELECT Id FROM AspNetRoles WHERE Name = 'SentinelAdmin';

-- Find your user ID
SELECT Id FROM AspNetUsers WHERE Email = 'your-operator@yourdomain.com';

-- Assign
INSERT INTO AspNetUserRoles (UserId, RoleId) VALUES ('<userId>', '<roleId>');
```

---

## Step 7 — Verify deployment

```
https://<your-vm-hostname>/
```

Smoke-test checklist:
- [ ] Home page loads without errors
- [ ] Login page renders (`/Account/Login`)
- [ ] Demo login works (`operator@sentinel-demo.local` / `sentineldemo`)
- [ ] Maintenance index loads with seeded data
- [ ] SignalR live indicator turns green within 5 seconds
- [ ] `?` help button returns an AI response (requires `Ai__ApiKey`)
- [ ] Language dropdown in Settings shows all 7 languages
- [ ] Log out and log back in — session re-establishes (Data Protection keys working)
- [ ] `/SentinelAdmin/TenantHealth` — health dashboard loads for `SentinelAdmin` users
- [ ] Mobile: no horizontal scroll, hamburger menu opens

---

## Subsequent Deployments (no schema changes)

```powershell
# 1. Build
dotnet publish -c Release -r win-x64 --no-self-contained -o .\publish

# 2. Stop app pool (optional but cleaner — prevents file-lock errors mid-copy)
Invoke-Command -ComputerName YOUR-VM-NAME -ScriptBlock {
    Stop-WebAppPool -Name "MaintenanceSandbox"
}

# 3. Copy
robocopy .\publish\ "\\YOUR-VM-NAME\c$\inetpub\wwwroot\MaintenanceSandbox" /MIR /XD logs /NFL /NDL

# 4. Restart app pool
Invoke-Command -ComputerName YOUR-VM-NAME -ScriptBlock {
    Start-WebAppPool -Name "MaintenanceSandbox"
}
```

---

## Troubleshooting Startup Failures

### HTTP 500.30 — ANCM In-Process Handler Load Failure

1. Enable stdout logging — on the VM, edit `web.config` in the site root:
   ```xml
   stdoutLogEnabled="true"
   ```
2. Ensure `.\logs\` folder exists and app pool identity has Write access
3. Recycle app pool, reproduce error, read `.\logs\stdout_*.log`
4. **Disable stdout logging immediately after** — it is unbuffered and degrades performance

Common causes:
- App pool not set to **No Managed Code**
- .NET 8 Hosting Bundle not installed (run `dotnet --version` in a new cmd prompt on the VM)
- `ASPNETCORE_ENVIRONMENT` not set (app reads LocalDB connection strings → startup DB failure)

### DB Connection Failure at Startup

The startup seeder (`DbInitializer.SeedAsync`, `EnsureRagTablesAsync`, `PurgeExpiredDemoTenantsAsync`)
runs synchronously at boot. If Azure SQL is unreachable the process crashes with a 500.30.
Check:
- Environment variables for connection strings are set and correct
- VM's IP is in the Azure SQL Server firewall
- Azure SQL server is running

### Auth Cookies Break After Recycle

Data Protection keys are not persisted. Check:
- `C:\inetpub\dpkeys\MaintenanceSandbox\` exists
- App pool identity has Modify rights on that folder
- `DataProtection:KeysPath` config key resolves to the correct path

---

## Rollback

Rollback is a re-deploy of the previous publish output:

```powershell
# Keep the previous publish in a dated archive before each deploy
Copy-Item .\publish\ ".\publish-backup-$(Get-Date -Format yyyyMMdd-HHmm)\" -Recurse

# To rollback: stop app pool, robocopy previous archive, start app pool
```

If migrations were applied for the failed release, you may need to roll back the schema manually
(`dotnet ef database update <PreviousMigrationName>`).

---

## Useful Commands

```powershell
# Check .NET runtime on the VM
dotnet --info

# Recycle app pool remotely
Invoke-Command -ComputerName YOUR-VM-NAME -ScriptBlock {
    Restart-WebAppPool -Name "MaintenanceSandbox"
}

# Check IIS site state
Invoke-Command -ComputerName YOUR-VM-NAME -ScriptBlock {
    Get-Website -Name "MaintenanceSandbox"
}

# View Windows Application Event Log for ANCM errors
Get-EventLog -LogName Application -Source "IIS AspNetCore Module*" -Newest 20
```

