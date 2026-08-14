using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

/// 把 <see cref="IBattleGroundEffectRuntimePort"/> 实现到 <see cref="BattleRuntimeModule"/> 上。
///
/// 作为 <see cref="BattleRuntimeModuleBorrower"/>，它经基类的 <c>_runtime</c> 访问 hub；
/// 穿透 hub 的行为被限制在本文件内，地面效果服务族的四个文件自身不再引用 BattleRuntimeModule。
/// 与 <see cref="BattleTimelineBridgeService"/> 同一套模式。
///
/// 全部成员为显式接口实现：本类不提供 hub 之外的额外能力面。
internal sealed class BattleGroundEffectBridgeService
    : BattleRuntimeModuleBorrower,
        IBattleGroundEffectRuntimePort
{
    BattleState IBattleGroundEffectRuntimePort.GetBattleState() => _runtime?._state;

    BattleGridService IBattleGroundEffectRuntimePort.GetGridService() => _runtime?._grid_service;

    BattleSkillResolutionRules IBattleGroundEffectRuntimePort.GetSkillResolutionRules() =>
        _runtime?._skill_resolution_rules;

    BattleLayeredBarrierService IBattleGroundEffectRuntimePort.GetLayeredBarrierService() =>
        _runtime?._layered_barrier_service;

    BattleTargetCollectionService IBattleGroundEffectRuntimePort.GetTargetCollectionService() =>
        _runtime?._target_collection_service;

    BattleDamageResolver IBattleGroundEffectRuntimePort.GetDamageResolver() =>
        _runtime?.GetDamageResolver();

    BattleAttackCheckPolicyService IBattleGroundEffectRuntimePort.GetAttackCheckPolicyService() =>
        _runtime?.GetAttackCheckPolicyService();

    BattleMagicBacklashResolver IBattleGroundEffectRuntimePort.GetMagicBacklashResolver() =>
        _runtime?._magic_backlash_resolver;

    BattleChargeResolver IBattleGroundEffectRuntimePort.GetChargeResolver() =>
        _runtime?._charge_resolver;

    BattleSkillMasteryService IBattleGroundEffectRuntimePort.GetSkillMasteryService() =>
        _runtime?._skill_mastery_service;

    BattleEffectOrigin IBattleGroundEffectRuntimePort.CurrentEffectOriginForContingency =>
        _runtime?.CurrentEffectOriginForContingency ?? BattleEffectOrigin.PlayerCommand();

    void IBattleGroundEffectRuntimePort.AppendResultReportEntry(
        BattleEventBatch batch,
        AttackEffectResolutionResult result
    ) => _runtime?.AppendResultReportEntry(batch, result);

    void IBattleGroundEffectRuntimePort.MarkAppliedStatusesForTurnTiming(
        BattleUnitState targetUnit,
        GArray statusEffectIds
    ) => _runtime?.MarkAppliedStatusesForTurnTiming(targetUnit, statusEffectIds);

    void IBattleGroundEffectRuntimePort.MarkAppliedStatusesForTurnTiming(
        BattleUnitState targetUnit,
        GStringNameArray statusEffectIds
    ) => _runtime?.MarkAppliedStatusesForTurnTiming(targetUnit, statusEffectIds);

    void IBattleGroundEffectRuntimePort.MarkAppliedStatusesForTurnTiming(
        BattleUnitState targetUnit,
        IReadOnlyList<StringName> statusEffectIds
    ) => _runtime?.MarkAppliedStatusesForTurnTiming(targetUnit, statusEffectIds);

    void IBattleGroundEffectRuntimePort.AppendResultSourceStatusEffects(
        BattleEventBatch batch,
        BattleUnitState sourceUnit,
        AttackEffectResolutionResult result
    ) => _runtime?.AppendResultSourceStatusEffects(batch, sourceUnit, result);

    void IBattleGroundEffectRuntimePort.RecordEffectMetrics(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        int damage,
        int healing,
        int killCount
    ) => _runtime?._record_effect_metrics(sourceUnit, targetUnit, damage, healing, killCount);

    void IBattleGroundEffectRuntimePort.RecordUnitDefeated(BattleUnitState unitState) =>
        _runtime?._record_unit_defeated(unitState);

    void IBattleGroundEffectRuntimePort.AppendDamageResultLogLines(
        BattleEventBatch batch,
        string subjectLabel,
        string targetDisplayName,
        AttackEffectResolutionResult result
    ) => _runtime?.AppendDamageResultLogLines(batch, subjectLabel, targetDisplayName, result);

    void IBattleGroundEffectRuntimePort.ApplyOnKillGainResourcesEffects(
        BattleUnitState sourceUnit,
        BattleUnitState defeatedUnit,
        SkillDefinition skillDefinition,
        IEnumerable<CombatEffectDefinition> effectDefinitions,
        BattleEventBatch batch
    ) =>
        _runtime?._apply_on_kill_gain_resources_effects(
            sourceUnit,
            defeatedUnit,
            skillDefinition,
            effectDefinitions,
            batch
        );

    bool IBattleGroundEffectRuntimePort.IsCrownBreakTargetEligible(
        BattleUnitState activeUnit,
        BattleUnitState targetUnit
    ) => _runtime?._is_crown_break_target_eligible(activeUnit, targetUnit) == true;

    bool IBattleGroundEffectRuntimePort.IsCrownBreakSkill(StringName skillId) =>
        _runtime?._is_crown_break_skill(skillId) == true;

    void IBattleGroundEffectRuntimePort.RecordVajraBodyMasteryFromIncomingDamage(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
        AttackEffectResolutionResult result,
        BattleEventBatch batch
    ) =>
        _runtime?.RecordVajraBodyMasteryFromIncomingDamageTyped(
            sourceUnit,
            targetUnit,
            skillDefinition,
            result,
            batch
        );

    BattleShieldApplyResult IBattleGroundEffectRuntimePort.ApplyUnitShieldEffectsResult(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
        IEnumerable<CombatEffectDefinition> effectDefinitions,
        Dictionary<long, int> shieldRollContext
    )
    {
        BattleRuntimeModule runtime = _runtime;
        if (runtime == null)
        {
            return new BattleShieldApplyResult(false, 0, 0, -1, new StringName(""));
        }
        return runtime.ApplyUnitShieldEffectsResult(
            sourceUnit,
            targetUnit,
            skillDefinition,
            effectDefinitions,
            shieldRollContext
        );
    }

    bool IBattleGroundEffectRuntimePort.IsUnitValidForEffect(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        StringName targetTeamFilter
    ) => _runtime?._is_unit_valid_for_effect(sourceUnit, targetUnit, targetTeamFilter) == true;

    bool IBattleGroundEffectRuntimePort.IsUnitValidForEffect(
        BattleUnitReadView sourceUnit,
        BattleUnitReadView targetUnit,
        StringName targetTeamFilter
    ) => _runtime?._is_unit_valid_for_effect(sourceUnit, targetUnit, targetTeamFilter) == true;

    void IBattleGroundEffectRuntimePort.FlushLastStandMasteryRecords(BattleEventBatch batch) =>
        _runtime?._flush_last_stand_mastery_records(batch);

    void IBattleGroundEffectRuntimePort.AppendChangedCoord(
        BattleEventBatch batch,
        Vector2I coord
    ) => _runtime?._append_changed_coord(batch, coord);

    void IBattleGroundEffectRuntimePort.AppendChangedUnitId(
        BattleEventBatch batch,
        StringName unitId
    ) => _runtime?._append_changed_unit_id(batch, unitId);

    void IBattleGroundEffectRuntimePort.AppendChangedUnitCoords(
        BattleEventBatch batch,
        BattleUnitState unitState
    ) => _runtime?._append_changed_unit_coords(batch, unitState);

    void IBattleGroundEffectRuntimePort.CollectDefeatedUnitLoot(
        BattleUnitState unitState,
        BattleUnitState killerUnit,
        BattleEventBatch batch
    ) => _runtime?._collect_defeated_unit_loot(unitState, killerUnit, batch);

    void IBattleGroundEffectRuntimePort.ClearDefeatedUnit(
        BattleUnitState unitState,
        BattleEventBatch batch
    ) => _runtime?._clear_defeated_unit(unitState, batch);

    int IBattleGroundEffectRuntimePort.GetUnitSkillLevel(
        BattleUnitState unitState,
        StringName skillId
    ) => _runtime?._get_unit_skill_level(unitState, skillId) ?? 0;

    BattleSkillCastBlockReasonKind IBattleGroundEffectRuntimePort.GetSkillCastBlockReason(
        BattleUnitState activeUnit,
        SkillDefinition skillDefinition
    ) =>
        _runtime?._get_skill_cast_block_reason(activeUnit, skillDefinition)
        ?? BattleSkillCastBlockReasonKind.SkillCastCheckUnbound;

    string IBattleGroundEffectRuntimePort.GetSkillCastBlockMessage(
        BattleUnitState activeUnit,
        SkillDefinition skillDefinition
    ) => _runtime?._get_skill_cast_block_message(activeUnit, skillDefinition);

    string IBattleGroundEffectRuntimePort.GetSkillCommandBlockReason(
        BattleUnitReadView activeUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition
    ) =>
        _runtime?._get_skill_command_block_reason(
            activeUnit,
            skillDefinition,
            castVariantDefinition
        );

    int IBattleGroundEffectRuntimePort.GetEffectiveSkillRange(
        BattleUnitState activeUnit,
        SkillDefinition skillDefinition
    ) => _runtime?._get_effective_skill_range(activeUnit, skillDefinition) ?? 0;

    int IBattleGroundEffectRuntimePort.GetEffectiveSkillRange(
        BattleUnitReadView activeUnit,
        SkillDefinition skillDefinition
    ) => _runtime?._get_effective_skill_range(activeUnit, skillDefinition) ?? 0;

    bool IBattleGroundEffectRuntimePort.IsMovementBlocked(BattleUnitState unitState) =>
        _runtime?._is_movement_blocked(unitState) == true;

    bool IBattleGroundEffectRuntimePort.IsMovementBlocked(BattleUnitReadView unitView) =>
        _runtime?._movement_service?.IsMovementBlocked(unitView) == true;

    StringName IBattleGroundEffectRuntimePort.AllocateContingencySourceEventId(StringName prefix) =>
        _runtime?.AllocateContingencySourceEventId(prefix) ?? new StringName("");

    void IBattleGroundEffectRuntimePort.EmitContingencySpellAffected(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        IReadOnlyList<StringName> affectedUnitIds,
        StringName sourceEventId,
        IReadOnlyList<Vector2I> affectedCoords
    ) =>
        _runtime?.EmitContingencySpellAffected(
            sourceUnit,
            targetUnit,
            affectedUnitIds,
            sourceEventId,
            affectedCoords
        );

    void IBattleGroundEffectRuntimePort.EmitContingencyHpAndStatusHooks(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        int previousTargetHp,
        IReadOnlyList<StringName> appliedStatusIds,
        StringName sourceEventId
    ) =>
        _runtime?.EmitContingencyHpAndStatusHooks(
            sourceUnit,
            targetUnit,
            previousTargetHp,
            appliedStatusIds,
            sourceEventId
        );

    BattleSpecialSkillResult IBattleGroundEffectRuntimePort.ApplyUnitSkillSpecialEffectsResult(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        BattleEventBatch batch,
        BattleForcedMoveContext forcedMoveContext
    ) =>
        _runtime?.ApplyUnitSkillSpecialEffectsResult(
            sourceUnit,
            targetUnit,
            skillDefinition,
            castVariantDefinition,
            effectDefinitions,
            batch,
            forcedMoveContext
        ) ?? BattleSpecialSkillResult.Empty();

    void IBattleGroundEffectRuntimePort.HandleUnitDefeatedByRuntimeEffect(
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

    void IBattleGroundEffectRuntimePort.RecordSkillMasteryTargetResult(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
        AttackEffectResolutionResult result,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        bool additionalEffectApplied
    ) =>
        _runtime?._skill_mastery_service?.RecordTargetResult(
            sourceUnit,
            targetUnit,
            skillDefinition,
            result,
            effectDefinitions,
            additionalEffectApplied
        );

    void IBattleGroundEffectRuntimePort.RecordRatingContributionFromUnits(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        int damage,
        int healing,
        bool defeated,
        StringName sourceKind,
        StringName sourceId
    ) =>
        _runtime?._battle_rating_system?.RecordContributionFromUnits(
            sourceUnit,
            targetUnit,
            damage,
            healing,
            defeated,
            sourceKind,
            sourceId
        );

    void IBattleGroundEffectRuntimePort.PrepareTimedTerrainFieldPlacement(
        BattleUnitState sourceUnit,
        CombatEffectDefinition effectDefinition,
        StringName fieldInstanceId,
        BattleEventBatch batch
    ) =>
        _runtime?._terrain_effect_system?.PrepareTimedTerrainFieldPlacement(
            sourceUnit,
            effectDefinition,
            fieldInstanceId,
            batch
        );

    bool IBattleGroundEffectRuntimePort.UpsertTimedTerrainEffectFromDefinition(
        Vector2I effectCoord,
        BattleUnitState sourceUnit,
        SkillDefinition skillDefinition,
        CombatEffectDefinition effectDefinition,
        StringName fieldInstanceId
    ) =>
        _runtime
            ?._terrain_effect_system
            ?.UpsertTimedTerrainEffectFromDefinition(
                effectCoord,
                sourceUnit,
                skillDefinition,
                effectDefinition,
                fieldInstanceId
            ) == true;

    IReadOnlyList<BattleTerrainTopologyChange> IBattleGroundEffectRuntimePort.ReclassifyWaterTerrainNearCoords(
        BattleState state,
        IReadOnlyList<Vector2I> effectCoords
    ) =>
        _runtime?._terrain_topology_service?.ReclassifyWaterTerrainNearCoords(state, effectCoords)
        ?? System.Array.Empty<BattleTerrainTopologyChange>();

    int IBattleGroundEffectRuntimePort.AllocateTerrainEffectNonce()
    {
        BattleRuntimeModule runtime = _runtime;
        if (runtime == null)
        {
            return 0;
        }
        int nonce = runtime._terrain_effect_nonce + 1;
        runtime._terrain_effect_nonce = nonce;
        return nonce;
    }

    IReadOnlyDictionary<
        CombatEffectDefinition,
        IReadOnlyList<BattleUnitState>
    > IBattleGroundEffectRuntimePort.BuildUnitEffectTargetPlan(
        BattleUnitState sourceUnit,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        IReadOnlyList<BattleUnitState> targetUnits
    ) =>
        _runtime?._skill_orchestrator?.BuildUnitEffectTargetPlan(
            sourceUnit,
            skillDefinition,
            effectDefinitions,
            targetUnits
        );

    IReadOnlyDictionary<
        CombatEffectDefinition,
        IReadOnlyList<BattleUnitReadView>
    > IBattleGroundEffectRuntimePort.BuildUnitEffectTargetPlan(
        BattleUnitReadView sourceUnit,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        IReadOnlyList<BattleUnitReadView> targetUnits
    ) =>
        _runtime?._skill_orchestrator?.BuildUnitEffectTargetPlan(
            sourceUnit,
            skillDefinition,
            effectDefinitions,
            targetUnits
        );

    IReadOnlyList<BattleUnitState> IBattleGroundEffectRuntimePort.CollectUnitsInCoords(
        IReadOnlyList<Vector2I> effectCoords
    ) =>
        _runtime?._skill_orchestrator?.CollectUnitsInCoords(effectCoords)
        ?? System.Array.Empty<BattleUnitState>();

    int IBattleGroundEffectRuntimePort.ApplyForcedMoveEffect(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDefinition effectDefinition,
        BattleEventBatch batch,
        BattleForcedMoveContext forcedMoveContext,
        BattleSaveContext saveContext
    ) =>
        _runtime
            ?._special_skill_resolver
            ?.ApplyForcedMoveEffect(
                sourceUnit,
                targetUnit,
                effectDefinition,
                batch,
                forcedMoveContext,
                saveContext
            ) ?? 0;

    IReadOnlyList<Vector2I> IBattleGroundEffectRuntimePort.BuildGroundEffectCoords(
        SkillDefinition skillDefinition,
        IReadOnlyList<Vector2I> targetCoords,
        Vector2I sourceCoord,
        BattleUnitReadView activeUnit,
        CombatCastVariantDefinition castVariantDefinition
    ) =>
        _runtime?.BuildGroundEffectCoordsTyped(
            skillDefinition,
            targetCoords,
            sourceCoord,
            activeUnit,
            castVariantDefinition
        );
}
