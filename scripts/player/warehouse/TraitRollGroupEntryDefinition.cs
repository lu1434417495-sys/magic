using System;
using Godot;

public sealed class TraitRollGroupEntryDefinition
{
    public TraitRollGroupEntryDefinition(
        StringName traitId,
        int weight,
        StringName exclusiveGroup
    )
    {
        TraitId = traitId;
        Weight = weight;
        ExclusiveGroup = exclusiveGroup;
    }

    public StringName TraitId { get; }
    public int Weight { get; }
    public StringName ExclusiveGroup { get; }

}
