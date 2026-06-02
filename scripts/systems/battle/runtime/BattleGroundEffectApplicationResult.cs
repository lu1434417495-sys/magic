using System.Collections.Generic;
using Godot;

public readonly record struct BattleGroundUnitEffectsResult(
    bool Applied,
    int AffectedUnitCount,
    int Damage,
    int Healing,
    int KillCount
)
{
    internal Godot.Collections.Dictionary ToDictionary() =>
        new()
        {
            ["applied"] = Applied,
            ["affected_unit_count"] = AffectedUnitCount,
            ["damage"] = Damage,
            ["healing"] = Healing,
            ["kill_count"] = KillCount,
        };
}

public readonly record struct BattleGroundTerrainEffectsResult(bool Applied)
{
    internal Godot.Collections.Dictionary ToDictionary() => new() { ["applied"] = Applied };
}

public readonly record struct BattleGroundWindPushResult(
    bool Applied,
    IReadOnlyList<StringName> AffectedUnitIds
)
{
    internal Godot.Collections.Dictionary ToDictionary()
    {
        var affectedUnitIds = new Godot.Collections.Array();
        foreach (StringName affectedUnitId in AffectedUnitIds ?? System.Array.Empty<StringName>())
        {
            affectedUnitIds.Add(affectedUnitId);
        }
        return new() { ["applied"] = Applied, ["affected_unit_ids"] = affectedUnitIds };
    }
}
