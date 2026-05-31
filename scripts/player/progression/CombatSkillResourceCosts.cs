using Godot;
using GDictionary = Godot.Collections.Dictionary;

public readonly record struct CombatSkillResourceCosts(
    int ApCost,
    int MpCost,
    int StaminaCost,
    int AuraCost,
    int CooldownTu
)
{
    public static readonly CombatSkillResourceCosts Zero = new(0, 0, 0, 0, 0);

    public GDictionary ToDictionary() =>
        new()
        {
            ["ap_cost"] = ApCost,
            ["mp_cost"] = MpCost,
            ["stamina_cost"] = StaminaCost,
            ["aura_cost"] = AuraCost,
            ["cooldown_tu"] = CooldownTu,
        };
}
