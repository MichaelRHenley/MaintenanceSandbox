namespace MaintenanceSandbox.Services.Ai;

public interface IEquipmentContextProvider
{
    Task<AiEquipmentSnapshot?> GetSnapshotAsync(AiParsedIntent intent, CancellationToken ct);
    Task<List<string>> GetKnownNamesAsync(CancellationToken ct);
}
