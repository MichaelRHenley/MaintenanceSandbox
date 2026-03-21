namespace MaintenanceSandbox.Services.Ai;

public interface IAiPromptComposer
{
    string Compose(AiContextPacket context, AiParsedIntent intent);
}
