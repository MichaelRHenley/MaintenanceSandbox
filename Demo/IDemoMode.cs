namespace MaintenanceSandbox.Demo;

public interface IDemoMode
{
    bool IsEnabled { get; }
    bool IsDemoRequest();
}
