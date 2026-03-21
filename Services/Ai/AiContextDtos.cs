namespace MaintenanceSandbox.Services.Ai;

public sealed class AiContextPacket
{
    public string Mode { get; set; } = "";
    public string UserQuery { get; set; } = "";

    public string? Equipment { get; set; }
    public string? WorkCenter { get; set; }
    public string? Area { get; set; }

    public List<string> KnownEquipmentNames { get; set; } = new();

    public List<AiIncidentSummary> OpenIncidents { get; set; } = new();
    public List<AiIncidentSummary> SimilarIncidents { get; set; } = new();

    public AiEquipmentSnapshot? EquipmentSnapshot { get; set; }
    public AiPartsSnapshot? PartsSnapshot { get; set; }
    public AiWorkforceSnapshot? WorkforceSnapshot { get; set; }

    public List<AiKnowledgeHit> KnowledgeHits { get; set; } = new();

    public List<string> Constraints { get; set; } = new();
}

public sealed class AiIncidentSummary
{
    public int Id { get; set; }
    public string Equipment { get; set; } = "";
    public string Status { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public sealed class AiEquipmentSnapshot
{
    public string Equipment { get; set; } = "";
    public string? CurrentStatus { get; set; }
    public string? LastIssue { get; set; }
    public string? LastMaintenanceComment { get; set; }
}

public sealed class AiPartsSnapshot
{
    public string Equipment { get; set; } = "";
    public List<AiPartAvailability> Parts { get; set; } = new();
}

public sealed class AiPartAvailability
{
    public string PartNumber { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal? QtyOnHand { get; set; }
}

public sealed class AiWorkforceSnapshot
{
    public int AvailableResponders { get; set; }
    public List<string> ActiveResponders { get; set; } = new();
}

public sealed class AiKnowledgeHit
{
    public string SourceType { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string Text { get; set; } = "";
    public double Score { get; set; }
}
