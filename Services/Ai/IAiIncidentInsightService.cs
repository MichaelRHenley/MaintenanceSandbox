using MaintenanceSandbox.Models;

namespace MaintenanceSandbox.Services.Ai;

public interface IAiIncidentInsightService
{
    Task<IncidentAiInsight?> GetOrGenerateAsync(
        int incidentId,
        Guid tenantId,
        bool force,
        string language,
        CancellationToken ct);
}
