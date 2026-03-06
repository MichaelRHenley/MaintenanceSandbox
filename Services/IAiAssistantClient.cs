using MaintenanceSandbox.Data;
using MaintenanceSandbox.Services.Ai;
using Microsoft.Extensions.Caching.Memory;
using System.Globalization;
using System.Text;
using System.Text.Json;


namespace MaintenanceSandbox.Services;

public interface IAiAssistantClient
{
    Task<string> GetHelpAsync(AiHelpRequest request, CancellationToken ct = default);
    Task<string> GetJsonAsync(string prompt, CancellationToken ct = default);
}


public sealed class AiAssistantClient : IAiAssistantClient
{
    private readonly IChatModel _chat;
    private readonly IMemoryCache _cache;

    // FIXED: Inject IMemoryCache in constructor
    public AiAssistantClient(IChatModel chat, IMemoryCache cache)
    {
        _chat = chat;
        _cache = cache;
    }

    public async Task<string> GetHelpAsync(AiHelpRequest request, CancellationToken ct = default)
    {
        // Cache key: module + intent + culture (ignore extraContext for now)
        var cacheKey = $"aihelp:{request.ModuleKey}:{request.Intent}:{request.Culture}";

        if (_cache.TryGetValue<string>(cacheKey, out var cached))
        {
            return cached!;
        }

        // 1) Culture → language hint
        var culture = string.IsNullOrWhiteSpace(request.Culture)
            ? CultureInfo.CurrentUICulture.Name
            : request.Culture;

        var languageHint = culture switch
        {
            var c when c.StartsWith("fr", StringComparison.OrdinalIgnoreCase)
                => "Réponds en français (Canada), de manière claire et professionnelle.",
            var c when c.StartsWith("es", StringComparison.OrdinalIgnoreCase)
                => "Responde en español de forma clara y profesional.",
            _ => "Answer in clear, professional Canadian English."
        };

        // 2) Get module-specific context
        var moduleContext = GetModuleContext(request.ModuleKey);

        // 3) System prompt
        var systemPrompt =
            $"{languageHint}\n\n" +
            $"You are the in-app assistant for the Sentinel Manufacturing Suite. " +
            $"You help operators and maintenance technicians understand how to use the system. " +
            $"Be concise, practical, and use bullet points where helpful.";

        // 4) Build user message with actual page state
        var userMessage = BuildUserMessage(request, moduleContext);

        // 5) Delegate to IChatModel
        var result = await _chat.CompleteAsync(systemPrompt, userMessage, ct);

        // Cache for 5 minutes
        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));

        return result;
    }

    private static string BuildUserMessage(AiHelpRequest request, string moduleContext)
    {
        var basePrompt = request.Intent switch
        {
            AiHelpIntent.ExplainScreen =>
                "Explain what this screen does and what the user can do here.",

            AiHelpIntent.ExplainFields =>
                "Explain each important field, filter, or section visible on this screen.",

            AiHelpIntent.ShowExamples =>
                "Give 2-3 realistic examples of how users would use this screen in their daily work.",

            AiHelpIntent.WhatFirst =>
                "The user just opened this screen and doesn't know where to start. Tell them the first steps to take.",

            _ => "Explain what this screen does."
        };

        var sb = new StringBuilder();
        sb.AppendLine(basePrompt);
        sb.AppendLine();
        sb.AppendLine($"Module: {request.ModuleKey}");
        sb.AppendLine($"General Purpose: {moduleContext}");

        // Include actual page state if available
        if (!string.IsNullOrWhiteSpace(request.ExtraContext))
        {
            sb.AppendLine();
            sb.AppendLine("Current Page State:");
            sb.AppendLine(request.ExtraContext);
        }

        return sb.ToString();
    }

    private static string GetModuleContext(string moduleKey)
    {
        return moduleKey switch
        {
            "Maintenance" =>
                "This page shows all maintenance requests (work orders). Users can see open requests, " +
                "requests waiting for parts, and resolved requests. Each request displays: equipment, " +
                "priority, status, description, and last comment. Filters available: site, zone, work center.",

            "EmergencyMaintenance" =>
                "This is for urgent issues requiring immediate attention. Users can submit new emergency " +
                "maintenance requests with photos/videos, chat in real-time with responders, and track status. " +
                "Real-time presence indicators show who's responding.",

            "Parts" =>
                "This manages spare parts inventory. Users can search parts by number or description, check " +
                "stock levels and bin locations, view BOMs (bill of materials), and see usage history. " +
                "AI helps clean up part descriptions.",

            "WorkCenter" =>
                "This shows live production status for a work center. Operators log hourly throughput, " +
                "downtime events, and status changes. Color-coded tiles show status (green=running, red=down). " +
                "Displays production vs. goal.",

            "Assets" =>
                "This is the equipment registry. Contains all assets with functional locations, technical specs, " +
                "maintenance history, and links to work orders. Used to track equipment lifecycle and reliability.",

            _ =>
                "This is part of the Sentinel Manufacturing Core Suite, helping manage operations, maintenance, " +
                "and parts inventory."
        };
    }
    public async Task<string> GetJsonAsync(string prompt, CancellationToken ct = default)
    {
        var systemPrompt =
            "You are a JSON generator. Return ONLY valid JSON. " +
            "No markdown, no commentary, no code fences, no trailing text.";

        var raw = await _chat.CompleteAsync(systemPrompt, prompt, ct);

        var json = ExtractJson(raw);

        // Validate JSON
        using var _ = JsonDocument.Parse(json);

        return json;
    }

    private static string ExtractJson(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "{}";

        s = s.Trim();

        // Strip ```json fences
        if (s.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = s.IndexOf('\n');
            if (firstNewline >= 0) s = s[(firstNewline + 1)..];

            var lastFence = s.LastIndexOf("```", StringComparison.Ordinal);
            if (lastFence >= 0) s = s[..lastFence];

            s = s.Trim();
        }

        // Prefer object
        var firstBrace = s.IndexOf('{');
        var lastBrace = s.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
            return s.Substring(firstBrace, lastBrace - firstBrace + 1).Trim();

        // Or array
        var firstBracket = s.IndexOf('[');
        var lastBracket = s.LastIndexOf(']');
        if (firstBracket >= 0 && lastBracket > firstBracket)
            return s.Substring(firstBracket, lastBracket - firstBracket + 1).Trim();

        return s.Trim();
    }
}