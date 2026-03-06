using MaintenanceSandbox.Services;
using MaintenanceSandbox.Services.Ai;
using Microsoft.AspNetCore.Mvc;


[ServiceFilter(typeof(RequireTenantFilter))]
public class AiTestController : Controller
{
    private readonly MaintenanceAiService _ai;

    public AiTestController(MaintenanceAiService ai)
    {
        _ai = ai;
    }

    [HttpGet("/aitest")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var text = await _ai.RawTestAsync(ct);
        return Content(text);
    }
}

