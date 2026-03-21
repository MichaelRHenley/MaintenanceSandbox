using System.Text;

namespace MaintenanceSandbox.Services.Ai;

public sealed class AiPromptComposer : IAiPromptComposer
{
    public string Compose(AiContextPacket context, AiParsedIntent intent)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"Response language: {intent.Language}");
        sb.AppendLine();
        sb.AppendLine("You are the Sentinel EM Incident Command Center AI.");
        sb.AppendLine("Use plant context first. Do not invent assets, incidents, or parts.");
        sb.AppendLine();

        sb.AppendLine($"Mode: {context.Mode}");
        sb.AppendLine($"User query: {context.UserQuery}");
        sb.AppendLine($"Intent: {intent.Intent}");
        sb.AppendLine($"Equipment: {context.Equipment ?? "Unknown"}");
        sb.AppendLine();

        if (context.KnownEquipmentNames.Count > 0)
        {
            sb.AppendLine("Plant equipment (only reference these names):");
            sb.AppendLine(string.Join(", ", context.KnownEquipmentNames));
            sb.AppendLine();
        }

        if (context.EquipmentSnapshot is not null)
        {
            sb.AppendLine("Equipment snapshot:");
            sb.AppendLine($"- Current status: {context.EquipmentSnapshot.CurrentStatus}");
            sb.AppendLine($"- Last issue: {context.EquipmentSnapshot.LastIssue}");
            sb.AppendLine($"- Last maintenance comment: {context.EquipmentSnapshot.LastMaintenanceComment}");
            sb.AppendLine();
        }

        if (context.OpenIncidents.Count > 0)
        {
            sb.AppendLine($"Open incidents ({context.OpenIncidents.Count} total):");
            foreach (var item in context.OpenIncidents.Take(5))
                sb.AppendLine($"- #{item.Id} | {item.Equipment} | {item.Status} | {item.Description}");
            sb.AppendLine();
        }

        if (context.SimilarIncidents.Count > 0)
        {
            sb.AppendLine("Similar past incidents:");
            foreach (var item in context.SimilarIncidents.Take(5))
                sb.AppendLine($"- #{item.Id} | {item.Equipment} | {item.Status} | {item.Description}");
            sb.AppendLine();
        }

        if (context.PartsSnapshot?.Parts.Count > 0)
        {
            sb.AppendLine("Relevant parts on hand:");
            foreach (var part in context.PartsSnapshot.Parts.Take(5))
                sb.AppendLine($"- {part.PartNumber} | {part.Description} | Qty: {part.QtyOnHand}");
            sb.AppendLine();
        }

        if (context.KnowledgeHits.Count > 0)
        {
            sb.AppendLine("Knowledge hits from plant history:");
            foreach (var hit in context.KnowledgeHits.Take(5))
                sb.AppendLine($"- [incident #{hit.SourceId}] {hit.Text}");
            sb.AppendLine();
        }

        sb.AppendLine("Constraints:");
        foreach (var constraint in context.Constraints)
            sb.AppendLine($"- {constraint}");
        sb.AppendLine();

        switch (context.Mode)
        {
            case "Troubleshoot":
                sb.AppendLine("Instructions:");
                sb.AppendLine("- List the most likely root causes based on plant history.");
                sb.AppendLine("- Give a numbered inspection checklist (3-5 items).");
                sb.AppendLine("- Mention parts availability when relevant.");
                sb.AppendLine("- Clearly state when plant evidence is limited.");
                sb.AppendLine("- End with a suggested next action.");
                sb.AppendLine("- Structure your response as:");
                sb.AppendLine("  Most likely causes:");
                sb.AppendLine("  Evidence from plant history:");
                sb.AppendLine("  Recommended checks:");
                sb.AppendLine("  Suggested next action:");
                sb.AppendLine();
                sb.AppendLine($"REMINDER: Write your entire response in {intent.Language}. Do not use English unless {intent.Language} is \"en\".");
                break;

            case "Command":
                sb.AppendLine("Instructions:");
                sb.AppendLine("- Prepare a draft action only. Do not execute anything.");
                sb.AppendLine("- State clearly what action would be taken and require explicit user confirmation.");
                break;

            default:
                sb.AppendLine("Instructions:");
                sb.AppendLine("- Give a concise operational answer using only provided plant context.");
                sb.AppendLine("- Mention when plant evidence is limited.");
                sb.AppendLine("- End with suggested next actions if appropriate.");
                break;
        }

        return sb.ToString();
    }
}
