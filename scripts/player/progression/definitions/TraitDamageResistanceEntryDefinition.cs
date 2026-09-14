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
}
