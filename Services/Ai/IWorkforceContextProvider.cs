namespace MaintenanceSandbox.Services.Ai;

public interface IWorkforceContextProvider
{
    Task<AiWorkforceSnapshot> GetWorkforceAsync(CancellationToken ct);
}
