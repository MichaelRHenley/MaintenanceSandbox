// Demo/DemoOptions.cs
namespace MaintenanceSandbox.Demo;

public sealed class DemoOptions
{
    public bool Enabled { get; set; }
    public bool SeedOnStartup { get; set; }
    public string? EmailLinkSecret { get; set; }
    public int EmailLinkExpiryMinutes { get; set; } = 30;
}