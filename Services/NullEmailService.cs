namespace MaintenanceSandbox.Services;

public sealed class NullEmailService : IEmailService
{
    private readonly ILogger<NullEmailService> _logger;

    public NullEmailService(ILogger<NullEmailService> logger) => _logger = logger;

    public Task SendAsync(string toEmail, string subject, string body)
    {
        _logger.LogInformation("[Email not configured] To: {Email} | Subject: {Subject}", toEmail, subject);
        return Task.CompletedTask;
    }
}
