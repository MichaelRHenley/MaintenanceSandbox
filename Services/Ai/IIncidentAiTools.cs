namespace MaintenanceSandbox.Services.Ai;

public interface IIncidentAiTools
{
    Task<AiToolResult> SearchOpenIncidentsAsync(
        string? equipment, string? area, int? sinceHours, CancellationToken ct = default);

    Task<AiToolResult> SearchIncidentHistoryAsync(
        string? equipment, string? symptom, CancellationToken ct = default);

    Task<AiToolResult> CreateIncidentDraftAsync(
        string? equipment, string? issueSummary, string? priority, CancellationToken ct = default);
}
