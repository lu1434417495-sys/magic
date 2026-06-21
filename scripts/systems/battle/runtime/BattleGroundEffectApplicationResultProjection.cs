using Godot;

internal static class BattleGroundEffectApplicationResultProjection
{
    internal static Godot.Collections.Dictionary ProjectUnitEffects(
        BattleGroundUnitEffectsResult result
    ) =>
        new()
        {
            ["applied"] = result.Applied,
            ["affected_unit_count"] = result.AffectedUnitCount,
            ["damage"] = result.Damage,
            ["healing"] = result.Healing,
            ["kill_count"] = result.KillCount,
        };

    internal static Godot.Collections.Dictionary ProjectTerrainEffects(
        BattleGroundTerrainEffectsResult result
    ) => new() { ["applied"] = result.Applied };

    internal static Godot.Collections.Dictionary ProjectWindPush(BattleGroundWindPushResult result)
    {
        var affectedUnitIds = new Godot.Collections.Array();
        try
        {
            foreach (StringName affectedUnitId in result.AffectedUnitIds ?? System.Array.Empty<StringName>())
            {
                affectedUnitIds.Add(affectedUnitId);
            }
            return new() { ["applied"] = result.Applied, ["affected_unit_ids"] = affectedUnitIds };
        }
        finally
        {
            affectedUnitIds.Dispose();
        }
    }
}
