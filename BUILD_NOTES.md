# Build Notes — MaintenanceSandbox

Running record of architecture decisions, assumptions, constraints, and things to keep in mind
during development. Add to this as decisions are made.

---

## Architecture Decisions

### Two-Database Split
**Decision:** Business data (`AppDbContext`) lives in `SentinelMfgSuite_Core`; identity and
directory data (`DirectoryDbContext`) lives in `SentinelMfgSuite_Identity`.  
**Reason:** Allows identity/tenancy infrastructure to be shared across multiple products in the
CoreSuite family without coupling product schemas to user management.  
**Implication:** Cross-database joins are not possible via EF. Any user-facing query that needs
both (e.g. resolving a username from a request) must do two separate lookups and join in code.

### Row-Level Multi-Tenancy via `TenantId`
**Decision:** Every business entity implements `ITenantScoped` and carries a `TenantId` GUID.
EF global query filters enforce isolation automatically.  
**Assumption:** All data access goes through `AppDbContext` — no raw SQL or Dapper queries that
bypass the filter.  
**Gotcha:** `IgnoreQueryFilters()` must be used deliberately in seeding and admin operations.
Forgetting it in seed code silently produces empty result sets for cross-tenant reads.

### Demo Tenant Isolation
**Decision:** Each demo login creates a brand-new tenant via `DbInitializer.SeedDemoSessionAsync`.
The tenant `Name` is encoded as `"demo-{epochSeconds}-{tenantGuid:N}"` so age can be calculated
from the name alone — no schema change required.  
**Reason:** Allows unlimited concurrent demo sessions with no shared state.  
**Auto-purge:** `PurgeExpiredDemoTenantsAsync` runs at startup and after every `Switch()` call.
TTL is 2 hours, inferred from the epoch in the tenant name.  
**PlanTier constant:** `DbInitializer.DemoPlanTier = "Demo"` — used by both the purge logic and
`BlockDemoFilter`. Change in one place only.

### Demo Role Toggle — No Logout
**Decision:** Demo users see a Supervisor / Operator toggle in the navbar instead of a logout
button. Switching roles calls `DemoUserController.SwitchRole` which re-issues claims against the
**same** tenant (no reseed).  
**Reason:** Keeps demo data consistent across role switches so a visitor can see the same requests
from both perspectives.  
**Claim:** `is_demo = "true"` is added to the cookie on every demo sign-in. Layout checks this
claim to render the toggle branch.

### BlockDemoFilter
**Decision:** `Filters\BlockDemoFilter.cs` is an `IAsyncActionFilter` registered as scoped.
Applied via `[ServiceFilter(typeof(BlockDemoFilter))]` on individual action methods, not globally.  
**Covered actions:** `UsersAdmin/Create` (GET + POST), `TenantUserInvites/Create` (POST),
`UserInvitesAdmin/Invite` (GET + POST).  
**Assumption:** Any new user-creation or invite endpoint added in future must also be decorated.

### AI Integration — Claude
**Decision:** Claude (Anthropic) is the AI backend. `IChatModel` abstracts the HTTP call.
`ClaudeChatModel` is the real impl; `StubAiAssistantClient` is the fallback for local dev without
a key.  
**Status:** Connection currently broken / stubbed — see WEEKEND_PLAN.md.  
**AiOptions:** API key and model config come from `appsettings.json` → `Ai:` section, overridden
by user secrets in dev.

### Onboarding AI Service Split
**Decision:** `IOnboardingAiService` and `IMaintenanceSuggestionService` are separate interfaces
even though both hit Claude. Keeps prompts and context windows isolated per domain.

---

## Assumptions

- **Sandbox tenant GUID is fixed:** `5EFA6386-80F0-4565-87D2-5170079B6BE0`. Hardcoded in
  `DbInitializer.SeedAsync`. Do not change without also updating any seed references.
- **Azure SQL with Entra ID auth:** Local SQL Server / LocalDB is not supported. Dev machines must
  have Azure CLI logged in (`az login`) or use a service principal in the connection string.
- **Bogus for fake data:** Seeding uses the Bogus library with a deterministic seed derived from
  `Math.Abs(tenantId.GetHashCode())` so demo data is reproducible per tenant but varies across
  tenants.
- **Single site per sandbox tenant:** `SeedAsync` creates one site. Multi-site paths exist in the
  model but are untested at scale.
- **Subscription/Stripe is in pilot mode:** `SubscriptionGateMiddleware` and `SubscribeController`
  exist but Stripe webhooks are not fully wired. Pilot tenants bypass the gate.

---

## Known Constraints & Gotchas

| Area | Note |
|------|------|
| EF Migrations | Both contexts have separate migration histories. Run `dotnet ef migrations add` with `--context AppDbContext` or `--context DirectoryDbContext` as appropriate. |
| Demo purge timing | Purge runs at startup and on each `Switch()`. Long-running dev sessions don't purge mid-session. Old demo tenants accumulate in Azure SQL until next restart. |
| `sentinel-card` padding | The CSS class has no padding — inner content needs its own wrapper (e.g. `p-3 p-md-4`) or a `glass-card m-3` child. Don't add padding directly to `.sentinel-card` in CSS; it breaks the overflow/glow effect. |
| `IgnoreQueryFilters` in seeding | Required any time seed code reads across tenants. Easy to forget on new seed methods. |
| Demo user claims | `DemoUserProvider` bypasses ASP.NET Identity entirely. Demo users have no `ApplicationUser` record. Any code that calls `UserManager.GetUserAsync(User)` will return `null` for demo sessions. |
| Localization | All user-facing strings go through `IStringLocalizer<SharedResource>`. Keys follow the pattern `Section_Page_Element`. Don't hardcode English strings in views. |
| Claude connection | Currently stubbed (`StubAiAssistantClient`). Real `ClaudeChatModel` wiring is pending — see WEEKEND_PLAN.md. |

---

## CSS / Design System Notes

- **`.sentinel-card`** — outer card shell, dark glass effect. No padding. `overflow: hidden` clips
  the glow border. Content inside needs its own padding wrapper.
- **`.glass-card`** — inner content card, `padding: 1.25rem`. Use `m-3` when placing directly
  inside a `sentinel-card`.
- **`.glass-card--accent`** — adds a cyan left-border accent, used for workflow/status edit sections.
- **`.sentinel-page-header`** — flex row for page title + action buttons. Used on Create/Index pages.
- **`.badge-status` / `.badge-priority`** — pill badges driven by CSS classes set in the controller
  (e.g. `badge-status--open`, `badge-priority--high`).
- **`.sentinel-signout-btn`** — ghost button for sign-out / role toggle. `.active` subclass applies
  cyan fill to indicate the currently selected demo role.

---

## Demo Mobile Share / SMS Join Link

**Decision:** A demo session's isolated `tenantId` GUID is the natural share token — it already
uniquely identifies the tenant and has a TTL baked into the tenant name (epoch prefix).  
**Planned endpoint:** `GET /DemoUser/Join?tenantId=<guid>&role=Operator` — validates the tenantId
exists in `DemoTenant` / has not been purged, then signs the caller in via `SignInDemoUser` against
that existing tenant. **No** `SeedDemoSessionAsync` call — the tenant is not reseeded.  
**QR code:** Rendered client-side via `qrcode.js` (CDN) on the demo dashboard pointing at the
join URL. No server-side dependency.  
**SMS (optional):** Azure Communication Services (ACS) sends the join URL to a supplied phone
number. ACS connection string stored in `appsettings.json` → `AzureCommunicationServices:ConnectionString`,
overridden by user secrets in dev. Requires an ACS resource with an allocated phone number.  
**Security constraints:**
- Join link is only valid while the demo tenant is alive (≤2 h TTL, same `PurgeExpiredDemoTenantsAsync` logic).
- Unknown or expired `tenantId` values redirect to `/DemoUser/Index` with a friendly message.
- Join role is scoped to `Operator` or `Tech` — callers cannot self-elevate to `Supervisor` via the link.
- `is_demo = "true"` claim is added to the joined session identically to a normal `Switch()` sign-in.  
**Gotcha:** `DemoUserProvider` bypasses ASP.NET Identity — the same null-`ApplicationUser` caveat
applies to joined sessions. Any code calling `UserManager.GetUserAsync(User)` returns `null`.  
**Primary demo use case — Operator/Supervisor interaction:** The join-link feature unlocks the
core two-role demo story. A presenter logs in on a laptop as Supervisor (normal demo login), then
scans the QR code on a phone which joins as Operator on the **same tenant**. The phone creates a
maintenance request; the laptop sees it arrive live via SignalR. The Supervisor triages, updates
status, and adds comments — the Operator's phone reflects changes in real time. This is the key
product differentiator (real-time cross-role workflow) demonstrated with zero setup, credentials,
or IT involvement.  
**Demo script shorthand:** Phone A (Operator) creates request → Phone B (Tech) picks it up &
updates status → Laptop (Supervisor) triages, assigns priority & closes.

---

## Pending / Deferred

See `WEEKEND_PLAN.md` for the tracked backlog.
