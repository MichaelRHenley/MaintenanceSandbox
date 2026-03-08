# Weekend Plan

## ✅ Done

- [x] **Add Maintenance Tech demo role** — `DemoUserProvider`, `DemoUserController`, and navbar toggle updated with `tech@sentinel-demo.local` / role `"Tech"`.
- [x] **Fix Operator role blocked on MarkResolved** — `[Authorize]` on `MarkResolved` updated to `Operator,Supervisor,Tech`. `Views/Account/AccessDenied.cshtml` created.
- [x] **Fix Claude AI connection** — `ClaudeChatModel` now returns `string.Empty` on non-success instead of throwing. `NullChatModel` registered as fallback when `Ai:ApiKey` is empty. `appsettings.json` placeholder cleared so env var override works correctly.
- [x] **AI Suggest Fix — remove full-page reload** — `SuggestFix` converted to AJAX fetch with spinner and inline result injection. Controller returns `Json({ suggestion })`.
- [x] **AI help modal improvements** — `max-height` increased to `calc(85vh - 3rem)`. "Was this helpful?" feedback section removed.
- [x] **Demo AI rate limiting** — `IDemoAiRateLimiter` / `DemoAiRateLimiter` (20 calls/hr per tenant, `IMemoryCache` fixed window). Both `/api/ai/help` and `/Maintenance/SuggestFix` return HTTP 429 when limit hit. UIs detect 429 and show a styled warning instead of an error or fake AI content.

---

## TODO

- [ ] Add a public demo link on the website (marketing/landing page) that deep-links directly into the app as the demo user — e.g. a "Try the Demo" button that hits `/DemoUser/Switch?email=supervisor@sentinel-demo.local` so visitors land on the Maintenance dashboard already logged in as Supervisor without typing credentials.
- [ ] Add a "Try Me" / "Try the Demo" button on the marketing website that links to the app demo login.
- [ ] **Demo mobile share link / SMS invite** — After a demo session is created, show a shareable link (and QR code) on the demo dashboard so a mobile device can join the *same* demo tenant without starting a fresh one.
  - Add `/DemoUser/Join?tenantId=<guid>&role=Operator` endpoint: validates the tenantId belongs to an active demo tenant, then signs the caller in as Operator on that tenant (no new `SeedDemoSessionAsync` call).
  - Render a QR code on the demo index/dashboard page using `qrcode.js` (CDN, no install) pointing at the join URL.
  - Optional: add an "Send to my phone" form that POSTs a phone number and fires the join link via Azure Communication Services (ACS) SMS — ACS SDK already available in the ecosystem. Requires an ACS resource + phone number configured in `appsettings`.
  - Security note: join links should only work while the demo tenant is alive (≤2 h TTL already enforced by `PurgeExpiredDemoTenantsAsync`) and should reject unknown/expired tenantIds with a redirect to the demo login page.
- [ ] Add screenshots to the CoreSuite section of the marketing website.
- [ ] Add Sentinel Copilot feature.
- [ ] Set `Ai__ApiKey` machine env var on VM and confirm Claude is live in production.
- [ ] Azure Key Vault for API key management (longer-term — replace machine env var pattern).
