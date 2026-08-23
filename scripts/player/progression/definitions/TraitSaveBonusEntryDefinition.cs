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
}
