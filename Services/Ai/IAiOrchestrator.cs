namespace MaintenanceSandbox.Services.Ai;

public interface IAiOrchestrator
{
    Task<AiQueryResponse> HandleAsync(
        AiQueryRequest request,
        Guid tenantId,
        string userName,
        CancellationToken ct = default);
}
