using MaintenanceSandbox.Models;

namespace MaintenanceSandbox.Services;

public interface IMaintenanceSuggestionService
{
    List<SuggestionItem> GetSuggestions(MaintenanceRequest request);
}


