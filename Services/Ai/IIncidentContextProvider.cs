namespace MaintenanceSandbox.Services.Ai;

public interface IIncidentContextProvider
{
    Task<List<AiIncidentSummary>> GetOpenIncidentsAsync(AiParsedIntent intent, CancellationToken ct);
    Task<List<AiIncidentSummary>> GetSimilarIncidentsAsync(AiParsedIntent intent, string userQuery, CancellationToken ct);
}
