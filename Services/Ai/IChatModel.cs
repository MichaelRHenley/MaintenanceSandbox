namespace MaintenanceSandbox.Services.Ai;

public interface IChatModel
{
    Task<string> CompleteAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken ct = default);
}

