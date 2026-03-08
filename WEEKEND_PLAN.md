# Weekend Plan

## TODO

- [ ] Add a public demo link on the website (marketing/landing page) that deep-links directly into the app as the demo user — e.g. a "Try the Demo" button that hits `/DemoUser/Switch?email=supervisor@sentinel-demo.local` so visitors land on the Maintenance dashboard already logged in as Supervisor without typing credentials.
- [ ] Add a "Try Me" / "Try the Demo" button on the marketing website that links to the app demo login.
- [ ] **Demo mobile share link / SMS invite** — After a demo session is created, show a shareable link (and QR code) on the demo dashboard so a mobile device can join the *same* demo tenant without starting a fresh one.
  - Add `/DemoUser/Join?tenantId=<guid>&role=Operator` endpoint: validates the tenantId belongs to an active demo tenant, then signs the caller in as Operator on that tenant (no new `SeedDemoSessionAsync` call).
  - Render a QR code on the demo index/dashboard page using `qrcode.js` (CDN, no install) pointing at the join URL.
  - Optional: add an "Send to my phone" form that POSTs a phone number and fires the join link via Azure Communication Services (ACS) SMS — ACS SDK already available in the ecosystem. Requires an ACS resource + phone number configured in `appsettings`.
  - Security note: join links should only work while the demo tenant is alive (≤2 h TTL already enforced by `PurgeExpiredDemoTenantsAsync`) and should reject unknown/expired tenantIds with a redirect to the demo login page.
- [ ] Add screenshots to the CoreSuite section of the marketing website.
- [ ] Fix Claude AI connection (investigate broken/stub state and wire up real API).
- [ ] Add Sentinel Copilot feature.
