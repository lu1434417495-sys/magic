using System;
using Godot;

public sealed class FacilitySlotDefinition
{
    public FacilitySlotDefinition(
        string slotId,
        Vector2I localCoord,
        string slotTag,
        bool required
    )
    {
        SlotId = slotId ?? throw new ArgumentNullException(nameof(slotId));
        LocalCoord = localCoord;
        SlotTag = slotTag ?? throw new ArgumentNullException(nameof(slotTag));
        Required = required;
    }

    public string SlotId { get; }
    public Vector2I LocalCoord { get; }
    public string SlotTag { get; }
    public bool Required { get; }

    internal static FacilitySlotDefinition FromResource(
        FacilitySlotConfig source,
        string path
    )
    {
        if (source == null)
            throw WorldDefinitionProjection.Invalid(path, "resource is null");
        return new FacilitySlotDefinition(
            WorldDefinitionProjection.RequireString(source.slot_id, path + ".slot_id").Trim(),
            source.local_coord,
            WorldDefinitionProjection.RequireString(source.slot_tag, path + ".slot_tag"),
            source.required
        );
    }
}
