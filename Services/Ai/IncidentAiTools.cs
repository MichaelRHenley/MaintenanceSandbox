using MaintenanceSandbox.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace MaintenanceSandbox.Services.Ai;

public sealed class IncidentAiTools : IIncidentAiTools
{
    private readonly AppDbContext _db;

    public IncidentAiTools(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AiToolResult> SearchOpenIncidentsAsync(
        string? equipment, string? area, int? sinceHours, CancellationToken ct = default)
    {
        var query = _db.MaintenanceRequests
            .Include(r => r.Equipment)
            .Include(r => r.WorkCenter)
            .Where(r => r.Status != "Resolved" && r.Status != "Closed");

        if (!string.IsNullOrWhiteSpace(equipment))
        {
            var eq = equipment.ToLower();
            query = query.Where(r =>
                r.Equipment != null && (
                    (r.Equipment.DisplayName != null && r.Equipment.DisplayName.ToLower().Contains(eq)) ||
                    r.Equipment.Code.ToLower().Contains(eq)));
        }

        if (!string.IsNullOrWhiteSpace(area))
        {
            var ar = area.ToLower();
            query = query.Where(r => r.Area.ToLower().Contains(ar));
        }

        if (sinceHours.HasValue && sinceHours.Value > 0)
        {
            var cutoff = DateTime.UtcNow.AddHours(-sinceHours.Value);
            query = query.Where(r => r.CreatedAt >= cutoff);
        }

        var results = await query
            .OrderByDescending(r => r.CreatedAt)
            .Take(10)
            .ToListAsync(ct);

        if (!results.Any())
        {
            return new AiToolResult
            {
                Success = true,
                ToolName = "search_open_incidents",
                Summary = "No open incidents found matching the criteria."
            };
        }

        var citations = results
            .Select(r =>
            {
                var name = r.Equipment?.DisplayName ?? r.Equipment?.Code ?? "Unknown";
                var desc = r.Description.Length > 60 ? r.Description[..60] + "…" : r.Description;
                return $"#{r.Id} — {name} — {r.Status} — {desc}";
            })
            .ToList();

        var actions = results
            .Select(r => new AiSuggestedAction
            {
                Label = $"Open #{r.Id}",
                Action = "view_incident",
                Payload = r.Id.ToString()
            })
            .ToList();

        var summary = $"Found {results.Count} open incident(s): " +
            string.Join(", ", results.Select(r =>
                $"#{r.Id} {r.Equipment?.DisplayName ?? r.Equipment?.Code ?? "?"} ({r.Status})"));

        return new AiToolResult
        {
            Success = true,
            ToolName = "search_open_incidents",
            Summary = summary,
            Citations = citations,
            SuggestedActions = actions
        };
    }

    public async Task<AiToolResult> SearchIncidentHistoryAsync(
        string? equipment, string? symptom, CancellationToken ct = default)
    {
        var since = DateTime.UtcNow.AddDays(-90);
        var query = _db.MaintenanceRequests
            .Include(r => r.Equipment)
            .Include(r => r.WorkCenter)
            .Where(r => r.CreatedAt >= since);

        if (!string.IsNullOrWhiteSpace(equipment))
        {
            var eq = equipment.ToLower();
            query = query.Where(r =>
                r.Equipment != null && (
                    (r.Equipment.DisplayName != null && r.Equipment.DisplayName.ToLower().Contains(eq)) ||
                    r.Equipment.Code.ToLower().Contains(eq)));
        }

        if (!string.IsNullOrWhiteSpace(symptom))
        {
            var sym = symptom.ToLower();
            query = query.Where(r => r.Description.ToLower().Contains(sym));
        }

        var results = await query
            .OrderByDescending(r => r.CreatedAt)
            .Take(10)
            .ToListAsync(ct);

        if (!results.Any())
        {
            return new AiToolResult
            {
                Success = true,
                ToolName = "search_incident_history",
                Summary = "No matching incident history found in the last 90 days."
            };
        }

        var citations = results
            .Select(r =>
            {
                var name = r.Equipment?.DisplayName ?? r.Equipment?.Code ?? "Unknown";
                var desc = r.Description.Length > 60 ? r.Description[..60] + "…" : r.Description;
                return $"#{r.Id} — {name} — {r.Status} — {r.CreatedAt:yyyy-MM-dd} — {desc}";
            })
            .ToList();

        var actions = results
            .Select(r => new AiSuggestedAction
            {
                Label = $"View #{r.Id}",
                Action = "view_incident",
                Payload = r.Id.ToString()
            })
            .ToList();

        var summary = $"Found {results.Count} incident(s) in the last 90 days: " +
            string.Join(", ", results.Select(r =>
                $"#{r.Id} {r.CreatedAt:MMM d} {r.Equipment?.DisplayName ?? r.Equipment?.Code ?? "?"} ({r.Status})"));

        return new AiToolResult
        {
            Success = true,
            ToolName = "search_incident_history",
            Summary = summary,
            Citations = citations,
            SuggestedActions = actions
        };
    }

    public async Task<AiToolResult> CreateIncidentDraftAsync(
        string? equipment, string? issueSummary, string? priority, CancellationToken ct = default)
    {
        var eq = equipment ?? "Unknown";
        var issue = issueSummary ?? "Not specified";
        var pri = priority ?? "Medium";

        int? equipmentId = null;
        int? workCenterId = null;
        int? areaId = null;

        if (!string.IsNullOrWhiteSpace(equipment))
        {
            var eqLower = equipment.ToLower();
            var found = await _db.Equipment
                .Include(e => e.WorkCenter)
                .FirstOrDefaultAsync(e =>
                    (e.DisplayName != null && e.DisplayName.ToLower().Contains(eqLower)) ||
                    e.Code.ToLower().Contains(eqLower), ct);

            if (found != null)
            {
                eq = found.DisplayName ?? found.Code;
                equipmentId = found.Id;
                workCenterId = found.WorkCenterId;
                areaId = found.WorkCenter?.AreaId;
            }
        }

        // Pass the raw issueSummary (may be null) — form field stays blank if not extracted.
        // Use the display fallback only in the Summary text shown in the AI panel.
        var payload = JsonSerializer.Serialize(new
        {
            equipment = eq,
            issueSummary = string.IsNullOrWhiteSpace(issueSummary) ? null : issueSummary.Trim(),
            priority = pri,
            equipmentId,
            workCenterId,
            areaId
        });

        return new AiToolResult
        {
            Success = true,
            ToolName = "create_incident_draft",
            Summary = $"Draft ready — Equipment: {eq} | Issue: {issue} | Priority: {pri}",
            SuggestedActions = new List<AiSuggestedAction>
            {
                new() { Label = "✓ Create Incident", Action = "create_incident", Payload = payload },
                new() { Label = "✗ Cancel", Action = "cancel" }
            }
        };
    }
}
