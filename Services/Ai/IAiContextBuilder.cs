namespace MaintenanceSandbox.Services.Ai;

public interface IAiContextBuilder
{
    Task<AiContextPacket> BuildAsync(AiQueryRequest request, AiParsedIntent intent, CancellationToken ct);
}
