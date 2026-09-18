using System.Collections.Generic;
using Godot;

internal readonly record struct BattleGroundEffectBarrierClipContext(
    IReadOnlyList<CombatEffectDefinition> UnitEffectDefinitions,
    IReadOnlyList<CombatEffectDefinition> TerrainEffectDefinitions,
    IReadOnlyList<Vector2I> RawEffectCoords,
    IReadOnlyList<Vector2I> UnitEffectCoords,
    IReadOnlyList<Vector2I> TerrainEffectCoords,
    IReadOnlyList<Vector2I> VisibleEffectCoords,
    bool BarrierApplied
)
{
}


internal readonly record struct BattleBarrierCoordClipResult(
    IReadOnlyList<Vector2I> AllowedCoords,
    IReadOnlyList<Vector2I> BlockedCoords
)
{
}

internal readonly record struct BattleGroundEffectBarrierClipResult(
    BattleBarrierCoordClipResult UnitEffects,
    BattleBarrierCoordClipResult TerrainEffects,
    IReadOnlyList<Vector2I> VisibleCoords,
    bool Applied
)
{
}
