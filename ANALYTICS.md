# Analytics Integration

The application integrates **Google Analytics 4 (GA4)** for event tracking and funnel analysis, and **Microsoft Clarity** for session recording, heatmaps, and UX insights.

Scripts are injected **conditionally** — if an ID is not configured the script tag is never emitted, so local/development environments produce zero analytics traffic by default.

---

## Configuration

IDs are stored in `appsettings.json` under the `Analytics` section. Leave them empty in development:

```json
"Analytics": {
  "GA4MeasurementId": "",
  "ClarityProjectId": ""
}
```

> ⚠️ Never commit real measurement IDs or project IDs to source control if your repo is public. Use environment variables or User Secrets instead.

**Production environment variables** (double-underscore maps to nested JSON):

| Setting              | Environment Variable              | Example value   |
|----------------------|-----------------------------------|-----------------|
| GA4 Measurement ID   | `Analytics__GA4MeasurementId`     | `G-XXXXXXXXXX`  |
| Clarity Project ID   | `Analytics__ClarityProjectId`     | `abcde12345`    |

Set these in Azure App Service → **Configuration → Application settings**, or via VM system environment variables.

---

## Global JavaScript Helper

`window.sentinelTrack(eventName, params)` fires an event to **both** GA4 and Clarity simultaneously. It is safe to call when either or both platforms are not loaded (the function guards with `typeof` checks).

```javascript
// anywhere in a view's @section Scripts block
window.sentinelTrack('my_custom_event', { key: 'value' });
```

---

## Google Analytics 4

### Automatic events (no extra code required)

| Event           | Description                          |
|-----------------|--------------------------------------|
| `page_view`     | Fires on every full page load        |
| `session_start` | New session begins                   |
| `first_visit`   | First time a browser visits the site |

### Custom events

| Event name                    | Status        | Where to call / fired by              | Parameters                            |
|-------------------------------|---------------|---------------------------------------|---------------------------------------|
| `sound_toggled`               | ✅ Implemented | Sound toggle button (`_Layout.cshtml`) | `{ enabled: true \| false }`         |
| `maintenance_request_created` | 📋 Planned    | `Views/Maintenance/Create.cshtml`     | `{ work_center, priority }`           |
| `maintenance_status_updated`  | 📋 Planned    | `Views/Maintenance/Details.cshtml`    | `{ from_status, to_status }`          |
| `em_dashboard_viewed`         | 📋 Planned    | `Views/Maintenance/EmDashboard.cshtml`| `{ active_filter }`                   |
| `ai_help_opened`              | 📋 Planned    | `Views/Shared/_AiHelpLauncher.cshtml` | `{ page, intent }`                    |
| `demo_role_switched`          | 📋 Planned    | Layout demo role buttons              | `{ role }`                            |
| `comment_added`               | 📋 Planned    | `Views/Maintenance/Details.cshtml`    | *(none)*                              |

---

## Microsoft Clarity

### Custom tags (set on every page load)

Tags are set via `clarity('set', key, value)` immediately after the Clarity snippet loads. They appear as filterable dimensions in the Clarity portal under **Filters → Custom tags**.

| Tag         | Possible values                              | Description                          |
|-------------|----------------------------------------------|--------------------------------------|
| `user_role` | `Supervisor`, `Operator`, `Tech`, `anonymous`| The authenticated user's role        |
| `is_demo`   | `true`, `false`                              | Whether the session is in demo mode  |
| `tier`      | `Standard`, `Enhanced`, `Premium`            | Current product tier                 |

### Custom events

Clarity receives the **same event name** as GA4 whenever `sentinelTrack()` is called. This lets you filter session recordings to replay exactly the sessions where a specific action occurred.

---

## Adding new events

Call `window.sentinelTrack` from any view's `@section Scripts` block:

```javascript
@section Scripts {
    <script>
        document.addEventListener('DOMContentLoaded', function () {
            // Fire once on page load
            window.sentinelTrack('em_dashboard_viewed', { active_filter: 'all' });

            // Fire on a button click
            document.getElementById('my-btn')?.addEventListener('click', function () {
                window.sentinelTrack('my_button_clicked', { context: 'example' });
            });
        });
    </script>
}
```

GA4 event names must be **snake_case**, max 40 characters. Parameter keys/values follow the same rules.

---

## Getting your IDs

| Platform          | Where to find your ID                                                                 |
|-------------------|---------------------------------------------------------------------------------------|
| Google Analytics 4 | GA4 → Admin → Data Streams → your stream → **Measurement ID** (starts with `G-`)    |
| Microsoft Clarity  | Clarity portal → your project → **Settings → Overview → Project ID**                |
