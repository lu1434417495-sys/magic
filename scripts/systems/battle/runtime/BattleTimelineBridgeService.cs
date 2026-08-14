using System.Collections.Generic;
using Godot;

/// 把 <see cref="IBattleTimelineRuntimePort"/> 实现到 <see cref="BattleRuntimeModule"/> 上。
///
/// 作为 <see cref="BattleRuntimeModuleBorrower"/>，它经基类的 <c>_runtime</c> 访问 hub；
/// 穿透 hub 的行为被限制在本文件内，<see cref="BattleTimelineDriver"/> 自身不再引用
/// BattleRuntimeModule。与 <see cref="BattleContingencyBridgeService"/> 同一套模式。
///
/// 全部成员为显式接口实现：本类不提供 hub 之外的额外能力面。
internal sealed class BattleTimelineBridgeService
    : BattleRuntimeModuleBorrower,
        IBattleTimelineRuntimePort
{
    BattleState IBattleTimelineRuntimePort.GetBattleState() => _runtime?.GetState();

    void IBattleTimelineRuntimePort.AdvanceUnitTurnTimers(
        BattleUnitState unitState,
        BattleEventBatch batch
    ) => _runtime?._skill_turn_resolver?.AdvanceUnitTurnTimers(unitState, batch);

    BattleStatusTickResult IBattleTimelineRuntimePort.ApplyTurnStartStatuses(
        BattleUnitState unitState,
        BattleEventBatch batch
    ) =>
        _runtime?._skill_turn_resolver?.ApplyTurnStartStatusesResult(unitState, batch)
        ?? BattleStatusTickResult.Empty();

    BattleStatusTickResult IBattleTimelineRuntimePort.ApplyUnitStatusPeriodicTicks(
        BattleUnitState unitState,
        int elapsedTu,
        BattleEventBatch batch
    ) =>
        _runtime
            ?._skill_turn_resolver
            ?.ApplyUnitStatusPeriodicTicksResult(unitState, elapsedTu, batch)
        ?? BattleStatusTickResult.Empty();

    bool IBattleTimelineRuntimePort.AdvanceUnitStatusDurations(
        BattleUnitState unitState,
        int elapsedTu,
        BattleEventBatch batch
    ) =>
        _runtime?._skill_turn_resolver?.AdvanceUnitStatusDurations(unitState, elapsedTu, batch)
        == true;

    bool IBattleTimelineRuntimePort.AdvanceTimeStasisFrozenTimers(
        BattleUnitState unitState,
        int tuDelta,
        BattleEventBatch batch
    ) =>
        _runtime?._skill_turn_resolver?.AdvanceTimeStasisFrozenTimers(unitState, tuDelta, batch)
        == true;

    bool IBattleTimelineRuntimePort.IsTurnAiOverrideActive(BattleUnitState unitState) =>
        _runtime?._skill_turn_resolver?.IsTurnAiOverrideActive(unitState) == true;

    BattleTurnControlStatusResult IBattleTimelineRuntimePort.ResolveTurnControlStatus(
        BattleUnitState unitState,
        BattleEventBatch batch
    ) =>
        _runtime?._skill_turn_resolver?.ResolveTurnControlStatusResult(unitState, batch)
        ?? BattleTurnControlStatusResult.Empty();

    void IBattleTimelineRuntimePort.ReconcilePendingCasts(BattleEventBatch batch) =>
        _runtime?._casting_time_service?.ReconcilePendingCasts(batch);

    void IBattleTimelineRuntimePort.AdvancePendingCasts(
        int elapsedTu,
        BattleEventBatch batch,
        ISet<StringName> stasisFrozenUnitIds
    ) => _runtime?._casting_time_service?.AdvancePendingCasts(elapsedTu, batch, stasisFrozenUnitIds);

    void IBattleTimelineRuntimePort.CompleteReadyPendingCasts(BattleEventBatch batch) =>
        _runtime?._casting_time_service?.CompleteReadyPendingCasts(batch);

    void IBattleTimelineRuntimePort.ProcessDueDelayedAreaEffects(BattleEventBatch batch) =>
        _runtime?._delayed_area_effect_system?.ProcessDueEffects(batch);

    void IBattleTimelineRuntimePort.ProcessTimedTerrainEffects(BattleEventBatch batch) =>
        _runtime?._terrain_effect_system?.ProcessTimedTerrainEffects(batch);

    void IBattleTimelineRuntimePort.AdvanceBarrierDurations(int tuDelta, BattleEventBatch batch) =>
        _runtime?._layered_barrier_service?.AdvanceBarrierDurations(tuDelta, batch);

    void IBattleTimelineRuntimePort.DispatchTraitBattleStart(BattleUnitState unitState) =>
        _runtime?._trait_trigger_hooks?.OnBattleStartResult(unitState);

    TraitDispatchResult IBattleTimelineRuntimePort.DispatchTraitTurnStart(
        BattleUnitState unitState
    ) => _runtime?._trait_trigger_hooks?.OnTurnStartResult(unitState) ?? default;

    void IBattleTimelineRuntimePort.BeginObjectiveMutation() => _runtime?.BeginObjectiveMutation();

    void IBattleTimelineRuntimePort.EndObjectiveMutation(
        BattleEventBatch batch,
        bool mutationCompleted
    ) => _runtime?.EndObjectiveMutation(batch, mutationCompleted);

    void IBattleTimelineRuntimePort.AdvanceControlObjectiveProgress(
        int tuDelta,
        BattleEventBatch batch
    ) => _runtime?.AdvanceControlObjectiveProgress(tuDelta, batch);

    void IBattleTimelineRuntimePort.RecordTurnStarted(
        BattleUnitState unitState,
        BattleEventBatch batch
    ) => _runtime?._record_turn_started(unitState, batch);

    int IBattleTimelineRuntimePort.GetUnitStaminaMax(BattleUnitState unitState) =>
        _runtime?._get_unit_stamina_max(unitState) ?? 0;

    void IBattleTimelineRuntimePort.AppendChangedUnitId(
        BattleEventBatch batch,
        StringName unitId
    ) => _runtime?._append_changed_unit_id(batch, unitId);

    void IBattleTimelineRuntimePort.CollectDefeatedUnitLoot(
        BattleUnitState unitState,
        BattleUnitState killerUnit,
        BattleEventBatch batch
    ) => _runtime?._collect_defeated_unit_loot(unitState, killerUnit, batch);

    void IBattleTimelineRuntimePort.HandleUnitDefeatedByRuntimeEffect(
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

    void IBattleTimelineRuntimePort.PrepareAiTurn(BattleUnitState unitState) =>
        _runtime?._prepare_ai_turn(unitState);

    void IBattleTimelineRuntimePort.CleanupAiTurn(BattleUnitState unitState) =>
        _runtime?._cleanup_ai_turn(unitState);

    void IBattleTimelineRuntimePort.HandleLowHpTurnEndMisfortune(BattleUnitState unitState) =>
        _runtime
            ?.GetFateRuntime()
            ?.HandleMisfortuneTrigger(MisfortuneTriggerRequest.LowHpTurnEnd(unitState));

    bool IBattleTimelineRuntimePort.ResolveEquipmentAbilityTurnEnd(
        BattleEquipmentAbilityTurnEndContext context
    ) => _runtime?.GetEquipmentAbilityRuntimeService()?.ResolveTurnEnd(context) == true;
}
