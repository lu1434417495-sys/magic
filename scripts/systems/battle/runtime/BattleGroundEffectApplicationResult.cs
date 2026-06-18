using System.Collections.Generic;
using Godot;

public readonly record struct BattleGroundUnitEffectsResult(
    bool Applied,
    int AffectedUnitCount,
    int Damage,
    int Healing,
    int KillCount
);

public readonly record struct BattleGroundTerrainEffectsResult(bool Applied);

public readonly record struct BattleGroundWindPushResult(
    bool Applied,
    IReadOnlyList<StringName> AffectedUnitIds
);
