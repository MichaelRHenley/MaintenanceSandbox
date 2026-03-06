using System.Text.Json.Serialization;

namespace MaintenanceSandbox.Data
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AiHelpIntent
    {
        ExplainScreen,
        ExplainFields,
        ShowExamples,
        WhatFirst
    }

    public class AiHelpRequest
    {
        // e.g. "EM_Details", "Parts_Search", "Dashboard_WorkCenter"
        public string ModuleKey { get; set; } = "";

        public AiHelpIntent Intent { get; set; }

        // optional: current filters, selected ID, etc.
        public string? ExtraContext { get; set; }

        // NEW: UI culture code, e.g. "fr-CA", "es-MX", "en-CA"
        public string? Culture { get; set; }
    }
}
