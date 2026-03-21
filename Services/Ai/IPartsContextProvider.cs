namespace MaintenanceSandbox.Services.Ai;

public interface IPartsContextProvider
{
    Task<AiPartsSnapshot?> GetPartsAsync(AiParsedIntent intent, CancellationToken ct);
}
