namespace MaintenanceSandbox.Services.Ai;

public class AiOptions
{
    public string Provider { get; set; } = "Claude";
    public string BaseUrl { get; set; } = "https://api.anthropic.com";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "claude-sonnet-4-5";
    public int MaxTokens { get; set; } = 1024;
    public double Temperature { get; set; } = 0.2;
}

