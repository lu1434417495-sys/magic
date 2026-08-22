using System;

public sealed class FacilityNpcDefinition
{
    public FacilityNpcDefinition(
        string templateId,
        string displayName,
        string serviceType,
        string interactionScriptId,
        string localSlotId
    )
    {
        TemplateId = templateId ?? throw new ArgumentNullException(nameof(templateId));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        ServiceType = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
        InteractionScriptId = interactionScriptId
            ?? throw new ArgumentNullException(nameof(interactionScriptId));
        LocalSlotId = localSlotId ?? throw new ArgumentNullException(nameof(localSlotId));
    }

    public string TemplateId { get; }
    public string DisplayName { get; }
    public string ServiceType { get; }
    public string InteractionScriptId { get; }
    public string LocalSlotId { get; }
}
