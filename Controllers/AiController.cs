using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using MaintenanceSandbox.Data;
using MaintenanceSandbox.Services;

namespace MaintenanceSandbox.Controllers.Api;

[ApiController]
[Route("api/ai")]
[ServiceFilter(typeof(RequireTenantFilter))]
public class AiController : ControllerBase
{
    private readonly IAiAssistantClient _aiClient;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly ILogger<AiController> _logger;

    public AiController(
        IAiAssistantClient aiClient,
        IStringLocalizer<SharedResource> localizer,
        ILogger<AiController> logger)
    {
        _aiClient = aiClient;
        _localizer = localizer;
        _logger = logger;
    }

    [HttpPost("help")]
    public async Task<IActionResult> GetHelp([FromBody] AiHelpRequestDto dto)
    {
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