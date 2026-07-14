using System;
using Godot;

public sealed class TraitDamageResistanceEntryDefinition
{
    public TraitDamageResistanceEntryDefinition(StringName damageTag, StringName mitigationTier)
    {
        DamageTag = damageTag;
        MitigationTier = mitigationTier;
    }

    public StringName DamageTag { get; }
    public StringName MitigationTier { get; }

    internal static TraitDamageResistanceEntryDefinition FromResource(
        TraitDamageResistanceEntryDef source,
        string path
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        return new TraitDamageResistanceEntryDefinition(
            source.damage_tag,
            source.mitigation_tier
        );
    }
}
