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
}
