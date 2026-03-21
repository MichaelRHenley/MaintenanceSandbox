# Sentinel Pilot Platform Runbook v1

---

## 1. Purpose

This runbook defines the repeatable procedures for deploying, configuring, validating, and recovering the Sentinel multi-tenant SaaS platform in a pilot or staging environment hosted on IIS (.NET 8).

It is intended for developers, platform operators, and anyone responsible for standing up or maintaining a Sentinel pilot instance. Follow these procedures in sequence for a clean first deployment. Individual sections may be referenced independently for ongoing operations.

**Scope covered by this document:**

- Application deployment to IIS on Azure VM
- Database migration sequencing
- Tenant provisioning lifecycle
- SentinelAdmin access and controls
- Failure recovery and retry workflows
- Pre-go-live validation checklist

---

## 2. Environment Overview

### Application

**Sentinel / MaintenanceSandbox** — .NET 8 ASP.NET Core MVC application with Razor views, SignalR hubs, and background services.

### Hosting

Azure Virtual Machine running Windows Server with IIS. The application is deployed as a framework-dependent publish targeting `win-x64`.

### Database

Azure SQL Database or SQL Server. Two logical databases are required:

| Database | Purpose |
|---|---|
| `DefaultConnection` | Application data — tenants, maintenance requests, AI insights, embeddings |
| `DirectoryConnection` | Identity and directory — users, roles, invites, subscriptions |

### Architecture Model

Shared-database multi-tenant SaaS. All tenant data coexists in a single database instance, isolated by `TenantId` enforced through a global EF Core query filter on all `TenantEntity` subclasses. The `Tenant` table itself is unfiltered and owned by the platform.

### Control Plane Components

| Component | Role |
|---|---|
| `TenantLifecycleService` | Orchestrates provisioning state transitions |
| `ProvisioningStatus` state machine | Tracks `Pending → Provisioning → Ready / Failed / Suspended` |
| `TenantProvisioningEvent` audit stream | Immutable record of every provisioning action |
| SentinelAdmin dashboard | Operator UI for tenant health, retry, and history |
| Retry provisioning workflow | Allows operators to re-trigger a failed provisioning attempt |

---

## 3. Deployment Steps

### 3.1 VM Prerequisites

Verify the following are installed and available on the target VM before deploying:

- **IIS** — Windows feature enabled; `Default Web Site` removed or repurposed
- **.NET 8 Hosting Bundle** — required for framework-dependent deployment; installs the ASP.NET Core Module for IIS
- **WebSocket Protocol** — IIS feature; required for SignalR hubs
- **URL Rewrite Module** — IIS extension; required if HTTPS redirect or canonical hostname rules are configured
- **SQL connectivity** — the VM must be able to reach the Azure SQL server on port 1433; verify firewall rules and VNet/NSG settings
- **Deployment folder** — e.g., `C:\inetpub\sentinel` — created and accessible by the app pool identity
- **DataProtection key folder** — e.g., `C:\keys\sentinel` — created with write permissions for the app pool identity; required for persistent auth cookie encryption keys across restarts

### 3.2 App Pool Configuration

Create a dedicated app pool (e.g., `SentinelPool`) with the following settings:

| Setting | Value | Reason |
|---|---|---|
| .NET CLR Version | No Managed Code | The .NET 8 runtime is self-managed; IIS should not attempt to load its own CLR |
| Pipeline Mode | Integrated | Required by ASP.NET Core Module |
| Enable 32-Bit Applications | False | Run as 64-bit to match the `win-x64` publish target |
| Start Mode | AlwaysRunning | Prevents cold-start delay for the first request after an idle period |
| Idle Time-out | 0 (disabled) | Keeps the process alive; prevents auth key rotation and SignalR disconnects on idle |
| Identity | ApplicationPoolIdentity | Least-privilege; grant this identity access to the key folder and deployment folder |

### 3.3 Publish Application

Run the following from the solution root on the build machine:

```powershell
dotnet publish -c Release -r win-x64 --no-self-contained -o .\publish
```

This produces a framework-dependent output in `.\publish`. The target VM must have the .NET 8 Hosting Bundle installed.

### 3.4 Copy Publish Output

Use `robocopy` to mirror the publish output to the deployment folder on the VM:

```powershell
robocopy .\publish C:\inetpub\sentinel /MIR /NP /NFL /NDL /LOG:deploy.log
```

- `/MIR` — mirrors source to destination, removing files no longer present
- `/NP` — suppresses progress percentage output
- `/LOG` — writes a deployment log for audit purposes

Stop the app pool before copying if the site is live to avoid file-lock errors:

```powershell
Stop-WebAppPool -Name SentinelPool
# ... robocopy ...
Start-WebAppPool -Name SentinelPool
```

### 3.5 IIS Site Configuration

Create the IIS site with the following properties:

| Property | Value |
|---|---|
| Site name | `Sentinel` |
| Physical path | `C:\inetpub\sentinel` |
| Binding | HTTPS, port 443, hostname as appropriate |
| App pool | `SentinelPool` |

Ensure the `web.config` in the publish output references `aspNetCore` with `processPath="dotnet"` and `hostingModel="inprocess"`.

### 3.6 DataProtection Key Folder Setup

The application persists Data Protection keys to the filesystem so that auth cookies survive app restarts. Without this, every restart invalidates all active sessions.

```powershell
# Create the key directory
New-Item -ItemType Directory -Path C:\keys\sentinel -Force

# Grant the app pool identity write access
$acl = Get-Acl "C:\keys\sentinel"
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
    "IIS AppPool\SentinelPool", "Modify", "ContainerInherit,ObjectInherit", "None", "Allow"
)
$acl.SetAccessRule($rule)
Set-Acl "C:\keys\sentinel" $acl
```

Verify the `appsettings.Production.json` or environment configuration points `DataProtection:KeyPath` to `C:\keys\sentinel`.

---

## 4. Migration Sequence

Database migrations must be applied manually before the first deployment. The application intentionally does **not** call `Database.Migrate()` at runtime.

```powershell
# Apply application database migrations
dotnet ef database update --context AppDbContext --connection "<DefaultConnection>"

# Apply identity/directory database migrations
dotnet ef database update --context DirectoryDbContext --connection "<DirectoryConnection>"
```

Run these from the solution root with the appropriate connection string for the target environment.

**Why runtime migration is disabled:**

Calling `Database.Migrate()` at startup on a shared-database multi-tenant system is unsafe in production. It can cause race conditions under multi-instance deployments, execute schema changes without an audit trail, and block application startup if the migration takes time. Schema changes must be reviewed, sequenced, and applied by a human operator before a new version goes live.

The `EnsureRagTablesAsync` method in `DbInitializer` applies supplemental DDL migrations (idempotent `IF NOT EXISTS` guards) for tables added after the initial Azure deployment. This is intentional for additive-only columns and tables that do not require EF migration tracking.

---

## 5. Connection String Strategy

All connection strings and environment-specific configuration are injected via environment variables set on the IIS site or app pool. No production credentials are stored in `appsettings.json` or source control.

### Required Variables

```
ConnectionStrings__DefaultConnection=Server=<host>;Database=<db>;User Id=<user>;Password=<pass>;TrustServerCertificate=True;
ConnectionStrings__DirectoryConnection=Server=<host>;Database=<dir_db>;User Id=<user>;Password=<pass>;TrustServerCertificate=True;
ASPNETCORE_ENVIRONMENT=Production
```

### Recommended Variables

```
ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
```

Enable this when the application sits behind an Azure Load Balancer or reverse proxy. Without it, `HttpContext.Request.Scheme` will return `http` even for HTTPS requests, causing incorrect redirect URLs and cookie security flags.

### LocalDB

**LocalDB must never be used in an IIS deployment.** LocalDB runs under the invoking user's context and is not accessible from service accounts or app pool identities. Any `(localdb)\MSSQLLocalDB` connection string in an IIS environment will fail at runtime with a network-related error.

---

## 6. Tenant Bootstrap Flow

### Lifecycle States

```
Pending → Provisioning → Ready
                      ↘ Failed
                      ↘ Suspended
```

| State | Meaning |
|---|---|
| `Pending` | Tenant record created; provisioning has not started |
| `Provisioning` | `TenantOperationalProvisioner` is actively running |
| `Ready` | All provisioning steps completed; tenant is operational |
| `Failed` | Provisioning encountered an unrecoverable error; `LastProvisioningError` is populated |
| `Suspended` | Tenant manually suspended by a platform operator |

Until a tenant reaches `Ready`, the `ProvisioningStatusGateMiddleware` intercepts all authenticated browser requests and redirects to `/TenantStatus`. This prevents users from accessing an incompletely provisioned workspace.

### What Provisioning Creates

A successful provisioning run creates the following operational data for the tenant:

- **Tenant** record with `ProvisioningStatus = Ready`, `ProvisionedAt`, and `ProvisioningCompletedAt` timestamps
- **Site** — at least one physical plant location (`Site Alpha`)
- **Area** — logical zone within the site (`Area 1`, `Area 2`)
- **WorkCenter** — operational unit within an area (`WC-001`, `WC-002`)
- **Equipment** placeholder records linked to each WorkCenter

### Post-Provisioning Validation

After provisioning completes, confirm the following before handing the tenant to the plant admin:

- [ ] `ProvisioningStatus` is `Ready` in the SentinelAdmin TenantHealth view
- [ ] At least one Site, Area, WorkCenter, and Equipment record exist under the tenant
- [ ] The tenant user can log in and is not redirected to `/TenantStatus`
- [ ] The Maintenance index page loads with no errors
- [ ] SignalR live indicator shows connected

---

## 7. SentinelAdmin Assignment

### Role Bootstrap

The `SentinelAdmin` role is created automatically during application startup if it does not exist. No manual migration or seed step is required for the role itself.

### Assigning an Operator

At least one user must be manually assigned the `SentinelAdmin` role before the dashboard is accessible. This must be done directly in the identity database or through the Users Admin interface (`/UsersAdmin`) while logged in as an already-privileged user.

There is no self-service SentinelAdmin registration by design.

### Access and Capabilities

Once assigned, a `SentinelAdmin` user has access to:

| Path | Purpose |
|---|---|
| `/SentinelAdmin` | Platform operator dashboard — tenant list, provisioning status summary |
| `/SentinelAdmin/TenantHealth` | Per-tenant health view — current status, error details, provisioning timeline |
| `/SentinelAdmin/TenantProvisioningHistory` | Full audit trail of provisioning events for a tenant |
| Retry controls | Re-trigger provisioning for a `Failed` or stale `Provisioning` tenant |

The `ProvisioningStatusGateMiddleware` explicitly bypasses `/SentinelAdmin` so operators can always reach the dashboard regardless of their own tenant's status.

---

## 8. Recovery from Provisioning Failure

### Failure Indicators

A tenant requires attention if any of the following are observed:

- `ProvisioningStatus = Failed` — provisioning completed with an error; `LastProvisioningError` contains the exception detail
- `ProvisioningStatus = Provisioning` for more than 5 minutes — indicates the provisioner process died mid-run (e.g., application restart during provisioning)
- Users redirected to `/TenantStatus` with no progress after an extended period

### Retry Workflow

1. Navigate to `/SentinelAdmin/TenantHealth`
2. Locate the affected tenant by name or ID
3. Inspect the provisioning timeline — note the last event, error message, and timestamp
4. Click **Retry Provisioning** to reset the status to `Pending` and re-enqueue the tenant
5. Monitor the status — it should transition `Pending → Provisioning → Ready` within the expected window
6. Confirm `Ready` state and that the tenant user can access the application

### Fallback Troubleshooting

If retry does not resolve the failure:

- Review `LastProvisioningError` for the root cause — common causes include missing master data, database permission errors, or external service timeouts
- Check application logs (IIS `stdout` or Application Insights) for the full stack trace at the time of failure
- If master data (Sites, Areas, WorkCenters) is partially created, the provisioner's `get-or-create` helpers are idempotent — a retry is safe and will not create duplicates
- For a `Provisioning` stale state with no error, manually reset `ProvisioningStatus` to `Pending` in the database, then trigger retry from SentinelAdmin
- If the tenant is in `Suspended` state, it must be re-activated by a platform operator before retry is possible

---

## 9. Startup Troubleshooting

### HTTP 500.30 — ANCM In-Process Start Failure

The application process failed to start. Check in this order:

- **Hosting Bundle** — confirm the .NET 8 ASP.NET Core Hosting Bundle is installed on the VM; a version mismatch between the published runtime and the installed bundle is the most common cause
- **App Pool mode** — confirm the app pool is set to `No Managed Code`; a managed CLR setting will prevent the ANCM from loading the .NET 8 runtime
- **stdout logs** — enable `stdoutLogEnabled="true"` in `web.config` temporarily, restart the site, and inspect the log file in the `logs\` folder under the deployment directory
- **Event Viewer** — check **Windows Logs → Application** for `IIS AspNetCore Module` errors with additional detail

### Database Connection Failure

Symptoms: application starts but crashes on first request, or startup throws a migration/connection error.

- Verify environment variable connection strings are set correctly on the IIS site (not app pool — site-level variables take precedence)
- Verify the Azure SQL firewall allows inbound connections from the VM's public or private IP
- Verify the SQL login credentials and that the user has `db_datareader`, `db_datawriter`, and `db_ddladmin` (for migration) permissions
- Verify migrations have been applied — an unapplied migration will cause EF to throw `Invalid column name` or `Invalid object name` errors at query time

### Authentication Reset After Restart

**Symptom:** all users are logged out after every IIS restart or app pool recycle.

- Verify the DataProtection key folder exists at the configured path
- Verify the app pool identity (`IIS AppPool\SentinelPool`) has **Modify** permission on the key folder
- Verify the application configuration (`DataProtection:KeyPath` or equivalent) points to a persistent path, not a temp directory
- Confirm at least one `.xml` key file exists in the folder after the first successful startup

### SignalR Connection Issues

**Symptom:** the live indicator in the nav shows `Reconnecting` or `Offline`; real-time updates do not arrive.

- Verify the **WebSocket Protocol** IIS feature is installed and enabled for the site
- Verify the app pool is not in 32-bit mode (WebSocket support requires 64-bit)
- Verify no load balancer or reverse proxy is stripping the `Upgrade: websocket` header; configure sticky sessions or WebSocket pass-through as required

---

## 10. Operational Validation Checklist

Run through this checklist after every deployment before declaring the environment ready:

- [ ] Application loads at the configured hostname without errors
- [ ] Login with a valid user account succeeds and redirects correctly
- [ ] `/SentinelAdmin` is accessible for a user with the `SentinelAdmin` role
- [ ] A new tenant can be provisioned through the full `Pending → Ready` lifecycle
- [ ] Tenant `ProvisioningStatus` is confirmed `Ready` in the TenantHealth view after provisioning
- [ ] A provisioned tenant user can log in and access the Maintenance index
- [ ] The TenantHealth dashboard loads and displays correct provisioning history
- [ ] The retry flow can be triggered for a `Failed` tenant and completes successfully
- [ ] The SignalR live indicator shows connected on the Maintenance and EM Dashboard views
- [ ] Auth cookies persist correctly across an IIS app pool recycle (user remains logged in)
- [ ] Application logs show no unhandled exceptions at startup
- [ ] Demo mode creates a fresh isolated session and reaches the Maintenance index

---

## 11. Pilot Success Criteria

The platform is considered ready for pilot plant onboarding when all of the following conditions are verified:

- **Deployment is repeatable** — a clean deploy from source to a running IIS site can be executed end-to-end from this runbook without undocumented steps
- **Migrations are controlled** — all schema changes are applied via `dotnet ef database update` before deployment; no runtime schema changes occur
- **Tenant provisioning is verified** — at least one real tenant has been provisioned through the full lifecycle and confirmed `Ready`
- **Failure recovery is verified** — at least one provisioning failure has been simulated and recovered using the SentinelAdmin retry workflow
- **One real workflow executed end-to-end** — a plant user has logged in, created a maintenance request, added a comment, and the request has been updated through at least one status transition

---

## 12. Ownership Model

### Platform Owner (Sentinel / Dev Team)

The platform owner is responsible for all infrastructure and lifecycle concerns:

- Deployment — publishing, file copy, IIS site and app pool configuration
- Migration — reviewing, sequencing, and applying all database schema changes
- Configuration — connection strings, environment variables, Data Protection keys, external service credentials
- Recovery — diagnosing provisioning failures, executing retries, escalating infrastructure issues

### Pilot Plant Admin

The pilot plant admin is responsible for operational configuration within their provisioned tenant:

- Master data — Sites, Areas, WorkCenters, Equipment records
- Users — inviting plant users, assigning roles
- Workflow validation — exercising the maintenance request lifecycle end-to-end
- Feedback — reporting unexpected behavior, data quality issues, and UX concerns

### Governance Rule

> **Tenants never modify the database schema directly.**
>
> All schema changes are owned by the Sentinel platform team and delivered through versioned EF Core migrations. No pilot plant admin or tenant user has DDL access to the application database. Sentinel owns the provisioning lifecycle; tenants operate within it.
