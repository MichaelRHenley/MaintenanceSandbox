namespace MaintenanceSandbox.Services.Ai;

public sealed class AiContextBuilder : IAiContextBuilder
{
    private readonly IIncidentContextProvider _incidentProvider;
    private readonly IEquipmentContextProvider _equipmentProvider;
    private readonly IPartsContextProvider _partsProvider;
    private readonly IWorkforceContextProvider _workforceProvider;
    private readonly IKnowledgeContextProvider _knowledgeProvider;

    public AiContextBuilder(
        IIncidentContextProvider incidentProvider,
        IEquipmentContextProvider equipmentProvider,
        IPartsContextProvider partsProvider,
        IWorkforceContextProvider workforceProvider,
        IKnowledgeContextProvider knowledgeProvider)
    {
        _incidentProvider = incidentProvider;
        _equipmentProvider = equipmentProvider;
        _partsProvider = partsProvider;
        _workforceProvider = workforceProvider;
        _knowledgeProvider = knowledgeProvider;
    }

    public async Task<AiContextPacket> BuildAsync(AiQueryRequest request, AiParsedIntent intent, CancellationToken ct)
    {
        var packet = new AiContextPacket
        {
            Mode = request.Mode,
            UserQuery = request.UserText,
            Equipment = intent.Equipment,
            WorkCenter = intent.WorkCenter,
            Area = intent.Area
        };

        // Sequential execution — AppDbContext is not thread-safe.
        packet.KnownEquipmentNames = await _equipmentProvider.GetKnownNamesAsync(ct);
        packet.OpenIncidents = await _incidentProvider.GetOpenIncidentsAsync(intent, ct);
        packet.SimilarIncidents = await _incidentProvider.GetSimilarIncidentsAsync(intent, request.UserText, ct);
        packet.EquipmentSnapshot = await _equipmentProvider.GetSnapshotAsync(intent, ct);
        packet.PartsSnapshot = await _partsProvider.GetPartsAsync(intent, ct);
        packet.WorkforceSnapshot = await _workforceProvider.GetWorkforceAsync(ct);

        // KnowledgeContextProvider uses its own DbContext factory, safe to call last.
        packet.KnowledgeHits = await _knowledgeProvider.SearchAsync(intent, request.UserText, ct);

        packet.Constraints.Add("Do not invent plant assets or incident records.");
        packet.Constraints.Add("Prefer cited plant history over generic maintenance advice.");
        packet.Constraints.Add("Any system-changing action requires user confirmation.");

        return packet;
    }
}
