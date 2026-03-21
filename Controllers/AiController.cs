using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using MaintenanceSandbox.Data;
using MaintenanceSandbox.Demo;
using MaintenanceSandbox.Services;
using MaintenanceSandbox.Services.Ai;
using System.Security.Claims;

namespace MaintenanceSandbox.Controllers.Api;

[ApiController]
[Route("api/ai")]
[ServiceFilter(typeof(RequireTenantFilter))]
public class AiController : ControllerBase
{
    private readonly IAiAssistantClient _aiClient;
    private readonly IAiOrchestrator _orchestrator;
    private readonly IAiIncidentInsightService _insightService;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly ILogger<AiController> _logger;
    private readonly IDemoAiRateLimiter _rateLimiter;

    public AiController(
        IAiAssistantClient aiClient,
        IAiOrchestrator orchestrator,
        IAiIncidentInsightService insightService,
        IStringLocalizer<SharedResource> localizer,
        ILogger<AiController> logger,
        IDemoAiRateLimiter rateLimiter)
    {
        _aiClient = aiClient;
        _orchestrator = orchestrator;
        _insightService = insightService;
        _localizer = localizer;
        _logger = logger;
        _rateLimiter = rateLimiter;
    }

    [HttpPost("help")]
    public async Task<IActionResult> GetHelp([FromBody] AiHelpRequestDto dto)
    {
        if (User.HasClaim("is_demo", "true"))
        {
            var tenantId = User.FindFirstValue("tenant_id") ?? "unknown";
            if (!_rateLimiter.TryConsume(tenantId))
                return StatusCode(429, "Demo AI limit reached for this session — please try again later.");
        }

        try
        {
            var request = new AiHelpRequest
            {
                ModuleKey = dto.ModuleKey ?? "Generic",
                Intent = ParseIntent(dto.Intent),
                ExtraContext = dto.ExtraContext,
                Culture = dto.Culture ?? "en"
            };

            var response = await _aiClient.GetHelpAsync(request);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI Help request failed");
            return StatusCode(500, _localizer["AiHelp_Error"].Value);
        }
    }

    [HttpPost("query")]
    public async Task<IActionResult> Query([FromBody] AiQueryRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.UserText))
            return BadRequest("UserText is required.");

        if (User.HasClaim("is_demo", "true"))
        {
            var tenantId = User.FindFirstValue("tenant_id") ?? "unknown";
            if (!_rateLimiter.TryConsume(tenantId))
                return StatusCode(429, "Demo AI limit reached — please try again later.");
        }

        var tenantIdStr = User.FindFirstValue("tenant_id");
        var parsedTenantId = Guid.TryParse(tenantIdStr, out var tid) ? tid : Guid.Empty;
        var userName = User.FindFirstValue(ClaimTypes.Name)
            ?? User.FindFirstValue(ClaimTypes.Email)
            ?? "unknown";

        var response = await _orchestrator.HandleAsync(request, parsedTenantId, userName, ct);

        if (!response.Success)
            return StatusCode(503, response);

        return Ok(response);
    }

    [HttpGet("insight/incident/{incidentId:int}")]
    public async Task<IActionResult> GetIncidentInsight(
        int incidentId,
        [FromQuery] bool force = false,
        [FromQuery] string? lang = null,
        CancellationToken ct = default)
    {
        var tenantIdStr = User.FindFirstValue("tenant_id");
        if (!Guid.TryParse(tenantIdStr, out var tenantId) || tenantId == Guid.Empty)
            return Unauthorized();

        if (User.HasClaim("is_demo", "true"))
        {
            if (!_rateLimiter.TryConsume(tenantIdStr!))
                return StatusCode(429, "Demo AI limit reached — please try again later.");
        }

        var language = lang
            ?? Request.GetTypedHeaders().AcceptLanguage
                .OrderByDescending(x => x.Quality.GetValueOrDefault(1))
                .Select(x => x.Value.Value?.Split('-')[0])
                .FirstOrDefault(x => !string.IsNullOrEmpty(x))
            ?? "en";

        try
        {
            var insight = await _insightService.GetOrGenerateAsync(incidentId, tenantId, force, language, ct);

            if (insight is null)
                return NotFound();

            return Ok(new
            {
                incidentId = insight.IncidentId,
                insightText = insight.InsightText,
                modelUsed = insight.ModelUsed,
                createdUtc = insight.CreatedUtc
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Incident insight failed for incident {IncidentId}", incidentId);
            return StatusCode(503, "AI insight is temporarily unavailable.");
        }
    }

    private static AiHelpIntent ParseIntent(string? intent)
    {
        return intent switch
        {
            "ExplainScreen" => AiHelpIntent.ExplainScreen,
            "ExplainFields" => AiHelpIntent.ExplainFields,
            "ShowExamples" => AiHelpIntent.ShowExamples,
            "WhatFirst" => AiHelpIntent.WhatFirst,
            _ => AiHelpIntent.ExplainScreen
        };
    }

    public class AiHelpRequestDto
    {
        public string? ModuleKey { get; set; }
        public string? Intent { get; set; }
        public string? ExtraContext { get; set; }
        public string? Culture { get; set; }
    }
}