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

    internal static FacilityNpcDefinition FromResource(
        FacilityNpcConfig source,
        string path
    )
    {
        if (source == null)
            throw WorldDefinitionProjection.Invalid(path, "resource is null");
        return new FacilityNpcDefinition(
            WorldDefinitionProjection.RequireString(source.npc_id, path + ".npc_id").Trim(),
            WorldDefinitionProjection.RequireString(
                source.display_name,
                path + ".display_name"
            ),
            WorldDefinitionProjection.RequireString(
                source.service_type,
                path + ".service_type"
            ),
            WorldDefinitionProjection.RequireString(
                source.interaction_script_id,
                path + ".interaction_script_id"
            ),
            WorldDefinitionProjection.RequireString(
                source.local_slot_id,
                path + ".local_slot_id"
            )
        );
    }
}
