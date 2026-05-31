using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public readonly record struct BattleGroundUnitEffectsResult(
    bool Applied,
    int AffectedUnitCount,
    int Damage,
    int Healing,
    int KillCount
)
{
    public GDictionary ToDictionary() =>
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
    public GDictionary ToDictionary() => new() { ["applied"] = Applied };
}

public readonly record struct BattleGroundWindPushResult(
    bool Applied,
    IReadOnlyList<StringName> AffectedUnitIds
)
{
    public GDictionary ToDictionary()
    {
        var affectedUnitIds = new GArray();
        foreach (StringName affectedUnitId in AffectedUnitIds ?? System.Array.Empty<StringName>())
        {
            affectedUnitIds.Add(affectedUnitId);
        }
        return new() { ["applied"] = Applied, ["affected_unit_ids"] = affectedUnitIds };
    }
}
