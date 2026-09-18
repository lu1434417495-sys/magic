using static BattleGroundEffectClipRules;
using System;
using System.Collections.Generic;
using Godot;

internal sealed partial class BattleSkillExecutionOrchestrator
{
    private BattleGroundEffectBarrierClipContext ResolveGroundEffectBarrierClipContext(
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
                activeUnit != null
                    ? activeUnit.GetAnchorCoord()
                    : new Vector2I(-1, -1),
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
                batch,
                castVariantDefinition
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

    private void RecordBarrierOnlyGroundMastery(
        BattleUnitState sourceUnit,
        SkillDefinition skillDefinition,
        BattleGroundEffectBarrierClipContext barrierClip,
        BattleGroundUnitEffectsResult unitResult,
        BattleGroundTerrainEffectsResult terrainResult
    )
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (
            sourceUnit == null
            || sourceUnit.source_member_id == ""
            || combatProfile == null
            || combatProfile.MasteryTriggerModeKind
                != CombatSkillMasteryTriggerMode.EffectApplied
            || !barrierClip.BarrierApplied
            || unitResult.Applied
            || terrainResult.Applied
        )
        {
            return;
        }
        Runtime?._skill_mastery_service?.RecordMasteryAmount(
            skillDefinition.SkillId,
            Math.Max(combatProfile.MasteryBaseAmount, 1)
        );
    }
}
