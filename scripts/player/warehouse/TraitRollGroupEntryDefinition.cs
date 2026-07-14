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

    internal static TraitRollGroupEntryDefinition FromResource(
        TraitRollGroupEntryDef source
    ) =>
        FromResource(source, "trait_roll_group_entry");

    internal static TraitRollGroupEntryDefinition FromResource(
        TraitRollGroupEntryDef source,
        string path
    )
    {
        if (source == null)
            throw WarehouseDefinitionProjection.Invalid(path, "resource is null");
        return new TraitRollGroupEntryDefinition(
            source.trait_id,
            source.weight,
            source.exclusive_group
        );
    }
}
