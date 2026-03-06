namespace MaintenanceSandbox.Services;

public static class RequestStatusRules
{
    // Ordered list is the natural workflow
    public static readonly List<string> WorkflowOrder = new()
    {
        "New",
        "In Progress",
        "Waiting on Parts",
        "Resolved",
        "Closed"
    };

    // Allowed moves from each state
    private static readonly Dictionary<string, List<string>> AllowedTransitions = new()
    {
        ["New"] = new() { "In Progress", "Closed" },
        ["In Progress"] = new() { "Waiting on Parts", "Resolved", "Closed" },
        ["Waiting on Parts"] = new() { "Resolved", "Closed" },
        ["Resolved"] = new() { "Closed", "In Progress" },      // reopen
        ["Closed"] = new() { }                                // terminal
    };

    public static bool CanTransition(string from, string to)
    {
        if (from == to) return true; // no-op transitions allowed
        if (!AllowedTransitions.ContainsKey(from)) return false;
        return AllowedTransitions[from].Contains(to);
    }

    public static List<string> GetAllowedTargets(string from)
    {
        if (!AllowedTransitions.ContainsKey(from))
            return new List<string>();

        return AllowedTransitions[from];
    }
}

