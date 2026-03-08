namespace MaintenanceSandbox.Services.Ai;

/// <summary>
/// No-op IChatModel used when no API key is configured (dev / demo).
/// All calls return an empty string — callers treat empty as "no suggestion".
/// </summary>
public sealed class NullChatModel : IChatModel
{
    public Task<string> CompleteAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken ct = default) => Task.FromResult(string.Empty);
}
