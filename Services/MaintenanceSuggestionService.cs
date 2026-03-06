using MaintenanceSandbox.Models;
using Microsoft.Extensions.Localization;

namespace MaintenanceSandbox.Services
{
    public class MaintenanceSuggestionService : IMaintenanceSuggestionService
    {
        private readonly IStringLocalizer<SharedResource> _sr;

        public MaintenanceSuggestionService(IStringLocalizer<SharedResource> sr)
        {
            _sr = sr;
        }

        public List<SuggestionItem> GetSuggestions(MaintenanceRequest request)
        {
            var suggestions = new List<SuggestionItem>();

            // Operator-safe: very basic checks
            if (request.Priority == "High")
            {
                suggestions.Add(new SuggestionItem
                {
                    Text = _sr["Maint_Suggestion_StopIfUnsafe"],
                    ForOperators = true
                });
            }

            // Operator-safe: clarifying the problem
            if (request.Status == "New")
            {
                suggestions.Add(new SuggestionItem
                {
                    Text = _sr["Maint_Suggestion_ConfirmSymptoms"],
                    ForOperators = true
                });
            }

            // Technician-level: parts planning
            if (request.Status == "Waiting on Parts")
            {
                suggestions.Add(new SuggestionItem
                {
                    Text = _sr["Maint_Suggestion_VerifyParts"],
                    ForOperators = false
                });
            }

            // Technician-level: history review
            var wcCode = request.WorkCenter?.Code; // or .DisplayName / .Name if that's what you use
            if (!string.IsNullOrEmpty(wcCode) &&
                wcCode.StartsWith("WC-0", StringComparison.OrdinalIgnoreCase))
            {
                suggestions.Add(new SuggestionItem
                {
                    Text = _sr["Maint_Suggestion_ReviewHistory"],
                    ForOperators = false
                });
            }

            // Technician-level: electrical checks
            var eqCode = request.Equipment?.Code; // or .DisplayName / .Name depending on your model
            if (!string.IsNullOrEmpty(eqCode) &&
                eqCode.StartsWith("EQ-1", StringComparison.OrdinalIgnoreCase))
            {
                suggestions.Add(new SuggestionItem
                {
                    Text = _sr["Maint_Suggestion_CheckElectrical"],
                    ForOperators = false
                });
            }

            // Fallback operator-safe suggestion if nothing else fired
            if (!suggestions.Any())
            {
                suggestions.Add(new SuggestionItem
                {
                    Text = _sr["Maint_Suggestion_FallbackChecklist"],
                    ForOperators = true
                });
            }

            return suggestions;
        }
    }
}
