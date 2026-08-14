using System;
using System.Collections.Generic;
using Godot;

/// 把 <see cref="IBattleChargeRuntimePort"/> 实现到 <see cref="BattleRuntimeModule"/> 上。
///
/// 作为 <see cref="BattleRuntimeModuleBorrower"/>，它经基类的 <c>_runtime</c> 访问 hub；
/// 穿透 hub 的行为被限制在本文件内，<see cref="BattleChargeResolver"/> 自身不再引用
/// BattleRuntimeModule。与 <see cref="BattleTimelineBridgeService"/> 同一套模式。
///
/// 全部成员为显式接口实现：本类不提供 hub 之外的额外能力面。
internal sealed class BattleChargeBridgeService
    : BattleRuntimeModuleBorrower,
        IBattleChargeRuntimePort
{
    BattleState IBattleChargeRuntimePort.GetBattleState() => _runtime?._state;

    BattleGridService IBattleChargeRuntimePort.GetGridService() => _runtime?._grid_service;

    BattleDamageResolver IBattleChargeRuntimePort.GetDamageResolver() => _runtime?._damage_resolver;

    BattleAttackCheckPolicyService IBattleChargeRuntimePort.GetAttackCheckPolicyService() =>
        _runtime?.GetAttackCheckPolicyService();

    BattleTerrainMovementContactResult IBattleChargeRuntimePort.ResolveMovementContact(
        BattleUnitState unitState,
        BattleEventBatch batch,
        HashSet<string> processedContactKeys,
        bool startingInsideCheck
    ) =>
        _runtime
            ?._terrain_effect_system
            ?.ResolveMovementContactForUnit(
                unitState,
                BattleSaveContext.Empty,
                batch,
                processedContactKeys,
                startingInsideCheck
            ) ?? BattleTerrainMovementContactResult.None;

    void IBattleChargeRuntimePort.ApplyTerrainContactEffects(
        BattleUnitState unitState,
        BattleEventBatch batch,
        HashSet<string> processedContactKeys
    ) =>
        _runtime
            ?._terrain_effect_system
            ?.ApplyContactEffectsForUnit(
                unitState,
                BattleSaveContext.Empty,
                batch,
                processedContactKeys
            );

    BattleBarrierInteractionResult IBattleChargeRuntimePort.ResolveUnitBoundaryCrossing(
        BattleUnitState unitState,
        Vector2I fromAnchor,
        Vector2I toAnchor,
        BattleEventBatch batch
    ) =>
        _runtime
            ?._layered_barrier_service
            ?.ResolveUnitBoundaryCrossingResult(unitState, fromAnchor, toAnchor, batch)
        ?? new BattleBarrierInteractionResult(false, false);

    IReadOnlyList<Vector2I> IBattleChargeRuntimePort.PreviewGroundEffectAllowedCoords(
        BattleUnitState sourceUnit,
        Vector2I effectOriginCoord,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> unitEffectDefinitions,
        IReadOnlyList<Vector2I> effectCoords,
        CombatCastVariantDefinition castVariantDefinition
    )
    {
        BattleLayeredBarrierService barrierService = _runtime?._layered_barrier_service;
        if (barrierService == null)
        {
            return effectCoords;
        }
        return barrierService
            .PreviewGroundEffectBarrierClipResultAtCoord(
                sourceUnit,
                effectOriginCoord,
                skillDefinition,
                unitEffectDefinitions,
                Array.Empty<CombatEffectDefinition>(),
                effectCoords,
                castVariantDefinition
            )
            .UnitEffects.AllowedCoords;
    }

    IReadOnlyList<Vector2I> IBattleChargeRuntimePort.PreviewGroundEffectAllowedCoords(
        BattleUnitReadView sourceUnit,
        Vector2I effectOriginCoord,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> unitEffectDefinitions,
        IReadOnlyList<Vector2I> effectCoords,
        CombatCastVariantDefinition castVariantDefinition
    )
    {
        BattleLayeredBarrierService barrierService = _runtime?._layered_barrier_service;
        if (barrierService == null)
        {
            return effectCoords;
        }
        return barrierService
            .PreviewGroundEffectBarrierClipResultAtCoord(
                sourceUnit,
                effectOriginCoord,
                skillDefinition,
                unitEffectDefinitions,
                Array.Empty<CombatEffectDefinition>(),
                effectCoords,
                castVariantDefinition
            )
            .UnitEffects.AllowedCoords;
    }

    IReadOnlyList<Vector2I> IBattleChargeRuntimePort.ResolveGroundEffectAllowedCoords(
        BattleUnitState sourceUnit,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> unitEffectDefinitions,
        IReadOnlyList<Vector2I> effectCoords,
        BattleEventBatch batch,
        CombatCastVariantDefinition castVariantDefinition
    )
    {
        BattleLayeredBarrierService barrierService = _runtime?._layered_barrier_service;
        if (barrierService == null)
        {
            return effectCoords;
        }
        return barrierService
            .ResolveGroundEffectBarrierClipResult(
                sourceUnit,
                skillDefinition,
                unitEffectDefinitions,
                Array.Empty<CombatEffectDefinition>(),
                effectCoords,
                batch,
                castVariantDefinition
            )
            .UnitEffects.AllowedCoords;
    }

    void IBattleChargeRuntimePort.ApplyMovementTrails(
        BattleUnitState sourceUnit,
        IReadOnlyList<Vector2I> executedPath,
        StringName skillId,
        BattleEventBatch batch
    ) =>
        _runtime
            ?._equipment_ability_runtime_service
            ?.ApplyMovementTrails(sourceUnit, executedPath, skillId, batch);

    StringName IBattleChargeRuntimePort.ResolveEffectTargetFilter(
        SkillDefinition skillDefinition,
        CombatEffectDefinition effectDefinition
    ) =>
        _runtime
            ?._skill_resolution_rules
            ?.ResolveEffectTargetFilter(skillDefinition, effectDefinition)
        ?? new StringName("");

    bool IBattleChargeRuntimePort.IsUnitValidForEffect(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        StringName targetFilter
    ) => _runtime?.IsUnitValidForEffect(sourceUnit, targetUnit, targetFilter) == true;

    IReadOnlyList<BattleUnitState> IBattleChargeRuntimePort.CollectUnitsInCoords(
        IReadOnlyList<Vector2I> effectCoords
    )
    {
        BattleRuntimeModule runtime = _runtime;
        if (runtime == null)
        {
            return Array.Empty<BattleUnitState>();
        }
        runtime._ensure_sidecars_ready();
        return runtime._skill_orchestrator.CollectUnitsInCoords(effectCoords);
    }

    int IBattleChargeRuntimePort.GetUnitSkillLevel(BattleUnitState unitState, StringName skillId) =>
        _runtime?.GetUnitSkillLevel(unitState, skillId) ?? 0;

    void IBattleChargeRuntimePort.ApplySourceBoundWeaponBonusMasteryGrants(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        AttackEffectResolutionResult result,
        BattleEventBatch batch
    ) =>
        _runtime?._apply_source_bound_weapon_bonus_mastery_grants(
            sourceUnit,
            targetUnit,
            result,
            batch
        );

    void IBattleChargeRuntimePort.MarkAppliedStatusesForTurnTiming(
        BattleUnitState targetUnit,
        IReadOnlyList<StringName> statusEffectIds
    ) => _runtime?.MarkAppliedStatusesForTurnTiming(targetUnit, statusEffectIds);

    void IBattleChargeRuntimePort.AppendResultSourceStatusEffects(
        BattleEventBatch batch,
        BattleUnitState sourceUnit,
        AttackEffectResolutionResult result
    ) => _runtime?.AppendResultSourceStatusEffects(batch, sourceUnit, result);

    void IBattleChargeRuntimePort.AppendDamageResultLogLines(
        BattleEventBatch batch,
        string subjectLabel,
        string targetDisplayName,
        AttackEffectResolutionResult result
    ) => _runtime?.AppendDamageResultLogLines(batch, subjectLabel, targetDisplayName, result);

    void IBattleChargeRuntimePort.RecordSkillEffectResult(
        BattleUnitState sourceUnit,
        int damage,
        int healing,
        int killCount
    ) => _runtime?.RecordSkillEffectResult(sourceUnit, damage, healing, killCount);

    void IBattleChargeRuntimePort.HandleUnitDefeatedByRuntimeEffect(
        BattleUnitState unitState,
        BattleUnitState sourceUnit,
        BattleEventBatch batch,
        string logLine,
        BattleDefeatHandlingOptions options
    ) =>
        _runtime?.HandleUnitDefeatedByRuntimeEffect(
            unitState,
            sourceUnit,
            batch,
            logLine,
            options
        );
}
