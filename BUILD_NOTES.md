# Build Notes — MaintenanceSandbox

Running record of architecture decisions, assumptions, constraints, and things to keep in mind
during development. Add to this as decisions are made.

---

## Pilot Deployment — Stabilization Pass (current)

**Date:** 2026  
**Goal:** Get the app running on Azure App Service for mobile pilot testing.

**Changes made this pass:**
- Ollama `HttpClient` timeout reduced from 2 min → 12 s when `BaseUrl` is not localhost. Prevents
  AI Assist requests hanging silently on Azure (no local Ollama). Outer `try/catch` in
  `AiOrchestrator` returns a graceful "temporarily unavailable" message.
- `Properties/PublishProfiles/AzureWebApp.pubxml` created (fill in the four TODOs before first use).
- `DEPLOYMENT.md` created — step-by-step Azure CLI + Visual Studio publish guide.
- `CONFIG_CHECKLIST.md` created — all required env vars and pre/post deploy checklist.

**Known pilot gaps (not blocking):**
- AI Assist (`✦` modal) returns "temporarily unavailable" on Azure — Ollama not deployed.
- No staging slot — rollback = full redeploy.
- No `/health` endpoint for App Service probing.
- Email invites silently dropped until SMTP is configured.

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

### AI Integration — Claude (Onboarding & Help Panel)
**Decision:** Claude (Anthropic) handles the guided onboarding flow and the contextual `?` help
panel (`_AiHelpLauncher`). `IChatModel` abstracts the HTTP call. `ClaudeChatModel` is the
production impl; `NullChatModel` is registered as the fallback when `Ai:ApiKey` is empty.  
**Key config:** `appsettings.json` → `Ai:` section (`Provider`, `Model`, `MaxTokens`). API key
comes from user secrets in dev; `Ai__ApiKey` env var in production.  
**Separate interfaces:** `IOnboardingAiService` and `IMaintenanceSuggestionService` are kept
separate even though both hit Claude — isolates prompts and context windows per domain.

### AI Assist — Ollama Orchestrator (Incident Intelligence)
**Decision:** The full AI Assist chat panel (Ask / Command / Troubleshoot modes) runs against a
local Ollama instance, not Claude. This keeps operational AI free, offline-capable, and
key-free for demos and dev.  
**Model:** `llama3.2` (3b / ~2 GB by default). Configured in `appsettings.json` → `Ollama:BaseUrl`
and `Ollama:ChatModel`.  
**2-turn orchestration (`AiOrchestrator`):**
- Turn 1 — Intent extraction: temp 0.05, 250 tokens → structured `AiParsedIntent` JSON.
- Turn 2 — Response composition: temp 0.2, 400 tokens — skipped for action-only intents
  (e.g. `CreateIncidentDraft`).

**Three read-only + draft tools (`IncidentAiTools`):**
- `SearchIncidentsAsync` — full-text search across recent maintenance requests.
- `GetEquipmentStatusAsync` — live equipment + open-request status lookup.
- `CreateIncidentDraftAsync` — async DB lookup (`Equipment.Include(WorkCenter)`) resolves
  `equipmentId`, `workCenterId`, `areaId`; serialises a JSON payload for the Create form.

**Intent fallback:** Small LLMs sometimes put the issue description in `symptom` instead of
`issueSummary`. Orchestrator always uses `intent.IssueSummary ?? intent.Symptom`.  
**Null payload rule:** `issueSummary` in the `CreateIncidentDraft` payload is `null` when the
LLM didn't extract a value — never `"Not specified"`. The display fallback string only appears
in the human-readable AI summary text, never in the action payload JSON.  
**Audit trail:** Every session, message, and tool invocation is written to three tables:
`AiConversationSession`, `AiConversationMessage`, `AiToolAudit`. Migration: `AddAiAuditTables`.  
**UI — Floating modal:** `Views/Shared/_AiAssistModal.cshtml` is included from `_Layout.cshtml`
for all authenticated users — no per-page wiring required. A fixed ✦ FAB (bottom-right,
`z-index: 1040`) opens a Bootstrap modal. JS maintains `sessionId` for multi-turn continuity.
`handleAction` routes `create_incident` payloads to `GET /Maintenance/Create?description=...`
with all five query params (description, priority, areaId, workCenterId, equipmentId).  
**Endpoint:** `POST /api/ai/query` → `AiController.Query`. Tenant and username resolved from
HTTP context claims — no explicit params from client.  
**Create form pre-fill:** `GET /Maintenance/Create` accepts `description?` and `priority?` query
params. The cascade dropdown `nav()` function preserves them through Area→WorkCenter→Equipment
reloads. The form uses `method="post"` with `asp-action`/`asp-controller` tag helpers
(antiforgery token auto-injected).

### Demo AI Rate Limiting
**Decision:** Demo sessions are capped at **20 Claude API calls per hour per tenant**. All three
demo personas (Supervisor, Operator, Tech) sharing the same tenant share the same pool.  
**Implementation:** `IDemoAiRateLimiter` / `DemoAiRateLimiter` (`Demo\DemoAiRateLimiter.cs`).
Registered as a **singleton** in `Program.cs`. Uses `IMemoryCache` with a fixed-window counter.  
**Cache key:** `demo:ai:{tenantId}` — one window per tenant GUID.  
**Window mechanics:** First call in a window sets `Count = 1` and `Expiry = now + 1 hour`. Each
subsequent call increments `Count`. When `Count >= 20`, `TryConsume` returns `false`. The entry
expires naturally — no sliding reset.  
**Guarded endpoints:**
- `POST /api/ai/help` — `AiController.GetHelp` (AI help panel)
- `POST /Maintenance/SuggestFix` — `MaintenanceController.SuggestFix` (inline fix suggestions)  

Both return **HTTP 429** when the limit is hit. The UIs check `res.status === 429` and render a
styled warning in place of the AI result — no generic error, no misleading content.  
**Non-demo users:** The guard only fires when the `is_demo = "true"` claim is present. Production
tenants are completely unaffected.  
**Gotcha:** `IMemoryCache.Set()` does not preserve an existing expiry — the expiry must be stored
inside the cached value itself (the `Window(Count, Expiry)` record) and re-applied on every write.


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
| Claude connection | `ClaudeChatModel` is wired and live. `NullChatModel` is registered as the fallback when `Ai:ApiKey` is empty (dev without a key). Key comes from user secrets in dev; `Ai__ApiKey` machine env var in production. |
| Ollama must be running | `/api/ai/query` returns a user-visible error if Ollama is not reachable at `Ollama:BaseUrl` (default `http://localhost:11434`). No health-check gate — the error surfaces directly in the AI Assist modal. Run `ollama serve` and `ollama pull llama3.2` before testing AI Assist locally. |
| AI Assist modal (layout) | `_AiAssistModal.cshtml` is rendered from `_Layout.cshtml` for all authenticated users. No per-page wiring is needed when adding new pages. FAB sits at `z-index: 1040` — below Bootstrap modal backdrop (1050) — so it disappears naturally when any other modal is open. |
| CreateIncidentDraft pre-fill | Description and priority travel as query-string params to `GET /Maintenance/Create`. The cascade `nav()` JS re-attaches them on every dropdown reload. If the LLM omits them entirely the form still loads blank — no error. |

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
