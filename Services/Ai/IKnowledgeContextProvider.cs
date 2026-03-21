namespace MaintenanceSandbox.Services.Ai;

public interface IKnowledgeContextProvider
{
    Task<List<AiKnowledgeHit>> SearchAsync(AiParsedIntent intent, string userQuery, CancellationToken ct);
}
