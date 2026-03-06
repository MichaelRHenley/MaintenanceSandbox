using Microsoft.Extensions.Options;

namespace MaintenanceSandbox.Demo;

public sealed class DemoMode : IDemoMode
{
    private readonly IHttpContextAccessor _http;
    private readonly DemoOptions _opts;

    public DemoMode(IHttpContextAccessor http, IOptions<DemoOptions> opts)
    {
        _http = http;
        _opts = opts.Value;
    }

    public bool IsEnabled => _opts.Enabled;

    // You control what constitutes “demo”. This is a clean default:
    // any route under /demo is demo-mode.
    public bool IsDemoRequest()
    {
        if (!IsEnabled) return false;
        var path = _http.HttpContext?.Request.Path ?? PathString.Empty;
        return path.StartsWithSegments("/demo");
    }
}