using System;
using Godot;

public sealed class TraitSaveBonusEntryDefinition
{
    public TraitSaveBonusEntryDefinition(StringName saveAbility, int bonus)
    {
        SaveAbility = saveAbility;
        Bonus = bonus;
    }

    public StringName SaveAbility { get; }
    public int Bonus { get; }

    internal static TraitSaveBonusEntryDefinition FromResource(
        TraitSaveBonusEntryDef source,
        string path
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        return new TraitSaveBonusEntryDefinition(source.save_ability, source.bonus);
    }
}
