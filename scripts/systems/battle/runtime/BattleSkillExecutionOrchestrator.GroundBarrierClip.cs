using System;
using System.Collections.Generic;
using Godot;

internal sealed partial class BattleSkillExecutionOrchestrator
{
    internal readonly record struct GroundEffectBarrierClipContext(
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

    private GroundEffectBarrierClipContext ResolveGroundEffectBarrierClipContext(
        BattleUnitState activeUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        IReadOnlyList<Vector2I> targetCoords,
        BattleEventBatch batch,
        IReadOnlyList<Vector2I> rawEffectCoords = null
    )
    {
        IReadOnlyList<CombatEffectDefinition> unitEffectDefinitions =
            Runtime?.CollectGroundUnitEffectDefinitionsTyped(
                skillDefinition,
                castVariantDefinition,
                activeUnit
            ) ?? Array.Empty<CombatEffectDefinition>();
        IReadOnlyList<CombatEffectDefinition> terrainEffectDefinitions =
            Runtime?.CollectGroundTerrainEffectDefinitionsTyped(
                skillDefinition,
                castVariantDefinition,
                activeUnit
            ) ?? Array.Empty<CombatEffectDefinition>();
        IReadOnlyList<Vector2I> normalizedRawEffectCoords =
            rawEffectCoords
            ?? Runtime?.BuildGroundEffectCoordsTyped(
                skillDefinition,
                targetCoords ?? Array.Empty<Vector2I>(),
                activeUnit != null ? activeUnit.coord : new Vector2I(-1, -1),
                activeUnit,
                castVariantDefinition
            )
            ?? Array.Empty<Vector2I>();
        BattleGroundEffectBarrierClipResult clipResult = Runtime?._layered_barrier_service
            ?.ResolveGroundEffectBarrierClipResult(
                activeUnit,
                skillDefinition,
                unitEffectDefinitions,
                terrainEffectDefinitions,
                normalizedRawEffectCoords,
                batch
            ) ?? BuildUnclippedGroundEffectBarrierResult(
                unitEffectDefinitions,
                terrainEffectDefinitions,
                normalizedRawEffectCoords
            );
        return BuildGroundEffectBarrierClipContext(
            unitEffectDefinitions,
            terrainEffectDefinitions,
            normalizedRawEffectCoords,
            clipResult
        );
    }

    internal GroundEffectBarrierClipContext PreviewGroundEffectBarrierClipContext(
        BattleUnitReadView activeUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        IReadOnlyList<Vector2I> targetCoords,
        IReadOnlyList<Vector2I> rawEffectCoords = null
    )
    {
        IReadOnlyList<CombatEffectDefinition> unitEffectDefinitions =
            Runtime?.CollectGroundUnitEffectDefinitionsTyped(
                skillDefinition,
                castVariantDefinition,
                activeUnit
            ) ?? Array.Empty<CombatEffectDefinition>();
        IReadOnlyList<CombatEffectDefinition> terrainEffectDefinitions =
            Runtime?.CollectGroundTerrainEffectDefinitionsTyped(
                skillDefinition,
                castVariantDefinition,
                activeUnit
            ) ?? Array.Empty<CombatEffectDefinition>();
        IReadOnlyList<Vector2I> normalizedRawEffectCoords =
            rawEffectCoords
            ?? Runtime?.BuildGroundEffectCoordsTyped(
                skillDefinition,
                targetCoords ?? Array.Empty<Vector2I>(),
                activeUnit.IsValid ? activeUnit.Coord : new Vector2I(-1, -1),
                activeUnit,
                castVariantDefinition
            )
            ?? Array.Empty<Vector2I>();
        BattleGroundEffectBarrierClipResult clipResult = Runtime?._layered_barrier_service
            ?.PreviewGroundEffectBarrierClipResult(
                activeUnit,
                skillDefinition,
                unitEffectDefinitions,
                terrainEffectDefinitions,
                normalizedRawEffectCoords
            ) ?? BuildUnclippedGroundEffectBarrierResult(
                unitEffectDefinitions,
                terrainEffectDefinitions,
                normalizedRawEffectCoords
            );
        return BuildGroundEffectBarrierClipContext(
            unitEffectDefinitions,
            terrainEffectDefinitions,
            normalizedRawEffectCoords,
            clipResult
        );
    }

    private static GroundEffectBarrierClipContext BuildGroundEffectBarrierClipContext(
        IReadOnlyList<CombatEffectDefinition> unitEffectDefinitions,
        IReadOnlyList<CombatEffectDefinition> terrainEffectDefinitions,
        IReadOnlyList<Vector2I> rawEffectCoords,
        BattleGroundEffectBarrierClipResult clipResult
    )
    {
        return new GroundEffectBarrierClipContext(
            unitEffectDefinitions ?? Array.Empty<CombatEffectDefinition>(),
            terrainEffectDefinitions ?? Array.Empty<CombatEffectDefinition>(),
            rawEffectCoords ?? Array.Empty<Vector2I>(),
            clipResult.UnitEffects.AllowedCoords ?? Array.Empty<Vector2I>(),
            clipResult.TerrainEffects.AllowedCoords ?? Array.Empty<Vector2I>(),
            clipResult.VisibleCoords ?? Array.Empty<Vector2I>(),
            clipResult.Applied
        );
    }

    private static BattleGroundEffectBarrierClipResult BuildUnclippedGroundEffectBarrierResult(
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
