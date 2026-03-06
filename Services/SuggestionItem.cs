namespace MaintenanceSandbox.Services;

public class SuggestionItem
{
    public string Text { get; set; } = string.Empty;

    // If true, operators are allowed to see this suggestion.
    public bool ForOperators { get; set; } = false;
}

