using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using MaintenanceSandbox.Data;
using MaintenanceSandbox.Demo;
using MaintenanceSandbox.Services;
using System.Security.Claims;

namespace MaintenanceSandbox.Controllers.Api;

[ApiController]
[Route("api/ai")]
[ServiceFilter(typeof(RequireTenantFilter))]
public class AiController : ControllerBase
{
    private readonly IAiAssistantClient _aiClient;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly ILogger<AiController> _logger;
    private readonly IDemoAiRateLimiter _rateLimiter;

    public AiController(
        IAiAssistantClient aiClient,
        IStringLocalizer<SharedResource> localizer,
        ILogger<AiController> logger,
        IDemoAiRateLimiter rateLimiter)
    {
        _aiClient = aiClient;
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