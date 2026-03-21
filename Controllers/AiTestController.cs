using MaintenanceSandbox.Services;
using MaintenanceSandbox.Services.Ai;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;


[ServiceFilter(typeof(RequireTenantFilter))]
public class AiTestController : Controller
{
    private readonly MaintenanceAiService _ai;
    private readonly IIncidentVectorSearch _vectorSearch;

    public AiTestController(MaintenanceAiService ai, IIncidentVectorSearch vectorSearch)
    {
        _ai = ai;
        _vectorSearch = vectorSearch;
    }

    [HttpGet("/aitest")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var text = await _ai.RawTestAsync(ct);
        return Content(text);
    }

    /// <summary>
    /// Indexes any un-embedded incidents for the current tenant.
    /// GET /aitest/rag-index
    /// </summary>
    [HttpGet("/aitest/rag-index")]
    public async Task<IActionResult> RagIndex(CancellationToken ct)
    {
        var tenantId = GetTenantId();
        if (tenantId == Guid.Empty)
            return Content("No tenant in claims.");

        await _vectorSearch.IndexRecentAsync(tenantId, ct);
        return Content($"Indexing complete for tenant {tenantId}. Check logs for count.");
    }

    /// <summary>
    /// Runs a cosine-similarity search against indexed incidents.
    /// GET /aitest/rag-search?q=overheating+motor
    /// </summary>
    [HttpGet("/aitest/rag-search")]
    public async Task<IActionResult> RagSearch(string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Content("Provide a query: /aitest/rag-search?q=your+issue");

        var tenantId = GetTenantId();
        if (tenantId == Guid.Empty)
            return Content("No tenant in claims.");

        var matches = await _vectorSearch.SearchAsync(q, tenantId, topK: 5, ct);

        if (matches.Count == 0)
            return Content("No matches found. Try /aitest/rag-index first.");

        var lines = matches.Select((m, i) =>
            $"{i + 1}. [score={m.Score:F4}] #{m.IncidentId}\n   {m.TextChunk.Replace("\n", " | ")}");

        return Content(string.Join("\n\n", lines));
    }

    private Guid GetTenantId()
    {
        var raw = User.FindFirstValue("tenant_id");
        return Guid.TryParse(raw, out var id) ? id : Guid.Empty;
    }
}

