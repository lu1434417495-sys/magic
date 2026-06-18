using Godot;

internal static class BattleShieldApplyResultProjection
{
    internal static Godot.Collections.Dictionary Project(BattleShieldApplyResult result) =>
        new()
        {
            ["applied"] = result.Applied,
            ["current_shield_hp"] = result.CurrentShieldHp,
            ["shield_max_hp"] = result.ShieldMaxHp,
            ["shield_duration"] = result.ShieldDuration,
            ["shield_family"] = result.ShieldFamily,
        };
}
