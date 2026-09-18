using static BattleSkillTargetPlanRules;
using static BattleGroundEffectClipRules;
using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

internal static class BattleGroundEffectClipRules
{
    internal static BattleGroundEffectBarrierClipContext BuildGroundEffectBarrierClipContext(
        IReadOnlyList<CombatEffectDefinition> unitEffectDefinitions,
        IReadOnlyList<CombatEffectDefinition> terrainEffectDefinitions,
        IReadOnlyList<Vector2I> rawEffectCoords,
        BattleGroundEffectBarrierClipResult clipResult
    )
    {
        return new BattleGroundEffectBarrierClipContext(
            unitEffectDefinitions ?? Array.Empty<CombatEffectDefinition>(),
            terrainEffectDefinitions ?? Array.Empty<CombatEffectDefinition>(),
            rawEffectCoords ?? Array.Empty<Vector2I>(),
            clipResult.UnitEffects.AllowedCoords ?? Array.Empty<Vector2I>(),
            clipResult.TerrainEffects.AllowedCoords ?? Array.Empty<Vector2I>(),
            clipResult.VisibleCoords ?? Array.Empty<Vector2I>(),
            clipResult.Applied
        );
    }

    internal static BattleGroundEffectBarrierClipResult BuildUnclippedGroundEffectBarrierResult(
        IReadOnlyList<CombatEffectDefinition> unitEffectDefinitions,
        IReadOnlyList<CombatEffectDefinition> terrainEffectDefinitions,
        IReadOnlyList<Vector2I> rawEffectCoords
    )
    {
        IReadOnlyList<Vector2I> normalizedRawEffectCoords =
            rawEffectCoords ?? Array.Empty<Vector2I>();
        IReadOnlyList<Vector2I> unitEffectCoords = unitEffectDefinitions?.Count > 0
            ? normalizedRawEffectCoords
            : Array.Empty<Vector2I>();
        IReadOnlyList<Vector2I> terrainEffectCoords = terrainEffectDefinitions?.Count > 0
            ? normalizedRawEffectCoords
            : Array.Empty<Vector2I>();
        return new BattleGroundEffectBarrierClipResult(
            new BattleBarrierCoordClipResult(unitEffectCoords, Array.Empty<Vector2I>()),
            new BattleBarrierCoordClipResult(terrainEffectCoords, Array.Empty<Vector2I>()),
            normalizedRawEffectCoords,
            false
        );
    }
}
