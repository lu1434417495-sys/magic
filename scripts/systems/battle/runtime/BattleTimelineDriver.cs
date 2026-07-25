using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

internal sealed class BattleTimelineDriver
{
    private const int TuGranularity = 5;
    private const int StaminaRecoveryProgressBase = 11;
    private const int StaminaRecoveryProgressDenominator = 10;
    private const int StaminaRestingRecoveryMultiplier = 2;
    private static readonly StringName StaminaRecoveryPercentBonus =
        "stamina_recovery_percent_bonus";
    private static readonly StringName CalamityReasonLowHpEndTurn = "low_hp_end_turn";

    private WeakReference<BattleRuntimeModule> _runtimeRef;

    internal void Setup(BattleRuntimeModule runtime)
    {
        _runtimeRef = runtime != null ? new WeakReference<BattleRuntimeModule>(runtime) : null;
    }

    internal void Dispose()
    {
        _runtimeRef = null;
    }

    internal void AdvanceTimeline(int tickCount, BattleEventBatch batch)
    {
        var runtime = _ResolveRuntime();
        var state = _ResolveState();
        if (state == null || state.timeline == null || tickCount <= 0)
            return;
        var resolvedTickCount = Mathf.Max(tickCount, 0);
        for (int i = 0; i < resolvedTickCount; i++)
        {
            runtime?.BeginObjectiveMutation();
            bool mutationCompleted = false;
            try
            {
                ApplyTimelineStep(batch, state.timeline.tu_per_tick);
                mutationCompleted = true;
            }
            finally
            {
                runtime?.EndObjectiveMutation(batch, mutationCompleted);
            }
            if (state.FinalDecision != null)
                return;
        }
    }

    private void _RecordTurnStarted(BattleUnitState unitState, BattleEventBatch batch)
    {
        var runtime = _ResolveRuntime();
        if (runtime == null)
            return;
        runtime._record_turn_started(unitState, batch);
    }

    private int _GetUnitStaminaMax(BattleUnitState unitState)
    {
        var runtime = _ResolveRuntime();
        if (runtime == null)
            return 0;
        return runtime._get_unit_stamina_max(unitState);
    }

    private void _AppendChangedUnitId(BattleEventBatch batch, StringName unitId)
    {
        var runtime = _ResolveRuntime();
        if (runtime == null)
            return;
        runtime._append_changed_unit_id(batch, unitId);
    }

    private void _CollectDefeatedUnitLoot(
        BattleUnitState unitState,
        BattleUnitState killerUnit = null,
        BattleEventBatch batch = null
    )
    {
        var runtime = _ResolveRuntime();
        if (runtime == null)
            return;
        runtime._collect_defeated_unit_loot(unitState, killerUnit, batch);
    }

    private void _AdvanceUnitTurnTimers(BattleUnitState unitState, BattleEventBatch batch)
    {
        _ResolveRuntime()?._skill_turn_resolver?.AdvanceUnitTurnTimers(unitState, batch);
    }

    private BattleStatusTickResult _ApplyTurnStartStatuses(
        BattleUnitState unitState,
        BattleEventBatch batch
    )
    {
        return _ResolveRuntime()
                ?._skill_turn_resolver
                ?.ApplyTurnStartStatusesResult(unitState, batch)
            ?? BattleStatusTickResult.Empty();
    }

    private BattleStatusTickResult _ApplyUnitStatusPeriodicTicks(
        BattleUnitState unitState,
        int elapsedTu,
        BattleEventBatch batch
    )
    {
        return _ResolveRuntime()
                ?._skill_turn_resolver
                ?.ApplyUnitStatusPeriodicTicksResult(unitState, elapsedTu, batch)
            ?? BattleStatusTickResult.Empty();
    }

    private bool _AdvanceUnitStatusDurations(
        BattleUnitState unitState,
        int elapsedTu,
        BattleEventBatch batch = null
    )
    {
        return _ResolveRuntime()
                ?._skill_turn_resolver
                ?.AdvanceUnitStatusDurations(unitState, elapsedTu, batch)
            == true;
    }

    private void _PrepareAiTurn(BattleUnitState unitState)
    {
        var runtime = _ResolveRuntime();
        if (runtime == null)
            return;
        runtime._prepare_ai_turn(unitState);
    }

    private void _CleanupAiTurn(BattleUnitState unitState)
    {
        var runtime = _ResolveRuntime();
        if (runtime == null)
            return;
        runtime._cleanup_ai_turn(unitState);
    }

    private void _ReconcilePendingCasts(BattleEventBatch batch)
    {
        var runtime = _ResolveRuntime();
        runtime?._casting_time_service?.ReconcilePendingCasts(batch);
    }

    private void _AdvancePendingCasts(
        int tuDelta,
        BattleEventBatch batch,
        ISet<StringName> stasisFrozenUnitIds
    )
    {
        var runtime = _ResolveRuntime();
        runtime?._casting_time_service?.AdvancePendingCasts(tuDelta, batch, stasisFrozenUnitIds);
    }

    private void _CompleteReadyPendingCasts(BattleEventBatch batch)
    {
        var runtime = _ResolveRuntime();
        runtime?._casting_time_service?.CompleteReadyPendingCasts(batch);
    }

    internal bool UseDiscreteTimelineTicks()
    {
        var state = _ResolveState();
        return state != null && state.timeline != null && state.timeline.tu_per_tick > 0;
    }

    internal void ApplyTimelineStep(BattleEventBatch batch, int tuDelta)
    {
        var runtime = _ResolveRuntime();
        var state = _ResolveState();
        if (state == null || state.timeline == null)
            return;
        if (tuDelta > 0 && tuDelta % TuGranularity != 0)
        {
            GameLog.Error(
                $"Battle timeline can only advance in {TuGranularity} TU steps, got {tuDelta}.",
                "battle.timeline.invalid_tu_delta",
                "battle"
            );
            return;
        }
        HashSet<StringName> stasisUnitIdsAtStepStart =
            tuDelta > 0 ? CollectTimeStasisUnitIds() : new HashSet<StringName>();
        HashSet<StringName> progressFrozenUnitIdsAtStepStart =
            tuDelta > 0 ? CollectCastingUnitIds() : new HashSet<StringName>();
        progressFrozenUnitIdsAtStepStart.UnionWith(stasisUnitIdsAtStepStart);
        if (tuDelta > 0)
        {
            state.timeline.current_tu += tuDelta;
            runtime?.AdvanceControlObjectiveProgress(tuDelta, batch);
            ResolveTimelineStatusPhase(batch, tuDelta);
        }
        state.RemoveExpiredTemporaryEdgeFeatures();
        runtime?._delayed_area_effect_system?.ProcessDueEffects(batch);
        runtime?._terrain_effect_system?.ProcessTimedTerrainEffects(batch);
        runtime?._layered_barrier_service?.AdvanceBarrierDurations(tuDelta, batch);
        if (tuDelta > 0)
        {
            _ReconcilePendingCasts(batch);
            _AdvancePendingCasts(tuDelta, batch, stasisUnitIdsAtStepStart);
            _ReconcilePendingCasts(batch);
            _CompleteReadyPendingCasts(batch);
            CollectTimelineReadyUnits(batch, tuDelta, progressFrozenUnitIdsAtStepStart);
        }
        SortReadyUnitIdsByActionPriority();
    }

    private HashSet<StringName> CollectCastingUnitIds()
    {
        var result = new HashSet<StringName>();
        var state = _ResolveState();
        if (state == null)
            return result;
        foreach (StringName unitId in GetUnitsInOrder())
        {
            var unitState = state.GetUnit(unitId);
            if (unitState?.IsCasting() == true)
                result.Add(unitId);
        }
        return result;
    }

    // step 开始时处于 time_stasis 的单位本 step 全程按冻结处理：
    // 静滞同 step 自然到期时，到期当 step 的 action/cast progress 仍不推进。
    private HashSet<StringName> CollectTimeStasisUnitIds()
    {
        var result = new HashSet<StringName>();
        var state = _ResolveState();
        if (state == null)
            return result;
        foreach (StringName unitId in GetUnitsInOrder())
        {
            var unitState = state.GetUnit(unitId);
            if (unitState != null && BattleTemporalStatusService.HasTimeStasis(unitState))
                result.Add(unitId);
        }
        return result;
    }

    internal void ResolveTimelineStatusPhase(BattleEventBatch batch, int tuDelta)
    {
        var state = _ResolveState();
        if (state == null || state.timeline == null || tuDelta <= 0)
            return;
        foreach (StringName unitId in GetUnitsInOrder())
        {
            var unitState = state.GetUnit(unitId);
            if (unitState == null || !unitState.IsAlive())
                continue;
            if (BattleTemporalStatusService.HasTimeStasis(unitState))
            {
                // 静滞冻结个人时间线：不结算 DOT/HOT、不推进其他状态 duration，
                // 只有 time_stasis 自身按战场时间减少，冷却 anchor 跟随时间前移。
                var stasisRuntime = _ResolveRuntime();
                if (
                    stasisRuntime?._skill_turn_resolver?.AdvanceTimeStasisFrozenTimers(
                        unitState,
                        tuDelta,
                        batch
                    ) == true
                )
                    _AppendChangedUnitId(batch, unitState.unit_id);
                continue;
            }
            var statusTickResult = _ApplyUnitStatusPeriodicTicks(unitState, tuDelta, batch);
            if (statusTickResult.Changed)
                _AppendChangedUnitId(batch, unitState.unit_id);
            if (!unitState.IsAlive())
            {
                var defeatSourceUnitId = statusTickResult.DefeatSourceUnitId;
                var defeatSourceUnit = state.GetUnit(defeatSourceUnitId);
                var runtime = _ResolveRuntime();
                runtime?.HandleUnitDefeatedByRuntimeEffect(
                    unitState,
                    defeatSourceUnit,
                    batch,
                    $"{unitState.display_name} 因持续效果倒下。",
                    new BattleDefeatHandlingOptions(
                        recordEnemyDefeatedAchievement: defeatSourceUnit != null
                    )
                );
                continue;
            }
            if (_AdvanceUnitStatusDurations(unitState, tuDelta, batch))
                _AppendChangedUnitId(batch, unitState.unit_id);
        }
    }

    internal void CollectTimelineReadyUnits(
        BattleEventBatch batch,
        int tuDelta,
        ISet<StringName> skipProgressUnitIds = null
    )
    {
        var runtime = _ResolveRuntime();
        var state = _ResolveState();
        if (state == null || state.timeline == null || tuDelta <= 0)
            return;
        foreach (StringName unitId in GetUnitsInOrder())
        {
            var unitState = state.GetUnit(unitId);
            if (unitState == null || !unitState.IsAlive())
                continue;
            if (skipProgressUnitIds != null && skipProgressUnitIds.Contains(unitId))
                continue;
            if (BattleTemporalStatusService.HasTimeStasis(unitState))
                continue;
            if (unitState.IsCasting())
                continue;
            if (ApplyStaminaRecovery(unitState, tuDelta))
                _AppendChangedUnitId(batch, unitState.unit_id);
            if (!unitState.IsAlive())
                continue;
            int actionProgressGain =
                BattleTemporalStatusService.ConsumeActionProgressGain(
                    unitState,
                    tuDelta
                );
            var actionThreshold = ResolveUnitActionThreshold(unitState);
            if (
                unitState.AdvanceActionClockTyped(
                    actionProgressGain,
                    actionThreshold
                )
                && !state.timeline.ready_unit_ids.Contains(unitId)
            )
            {
                state.timeline.ready_unit_ids.Add(unitId);
            }
        }
    }

    internal bool ApplyStaminaRecovery(BattleUnitState unitState, int tuDelta)
    {
        if (unitState == null || tuDelta <= 0)
            return false;
        var tickCount = tuDelta / TuGranularity;
        if (tickCount <= 0)
            return false;
        var staminaMax = _GetUnitStaminaMax(unitState);
        if (
            staminaMax <= 0
            || unitState.GetCurrentStamina() >= staminaMax
        )
        {
            return unitState.ApplyStaminaRecoveryTyped(
                tickCount,
                staminaMax,
                0,
                StaminaRecoveryProgressDenominator
            );
        }

        var constitution = GetUnitConstitution(unitState);
        var progressGainPerTick = StaminaRecoveryProgressBase + constitution;
        progressGainPerTick = ApplyStaminaRecoveryPercentBonus(unitState, progressGainPerTick);
        if (unitState.IsRestingTyped())
            progressGainPerTick *= StaminaRestingRecoveryMultiplier;
        return unitState.ApplyStaminaRecoveryTyped(
            tickCount,
            staminaMax,
            progressGainPerTick,
            StaminaRecoveryProgressDenominator
        );
    }

    internal int GetUnitConstitution(BattleUnitState unitState)
    {
        if (unitState == null || unitState.attribute_snapshot == null)
            return 0;
        var snapshot = unitState.attribute_snapshot;
        return Mathf.Max(snapshot.GetValue("constitution"), 0);
    }

    internal int ApplyStaminaRecoveryPercentBonus(BattleUnitState unitState, int baseProgressGain)
    {
        if (unitState == null || unitState.attribute_snapshot == null)
            return baseProgressGain;
        var snapshot = unitState.attribute_snapshot;
        var percentBonus = Mathf.Max(snapshot.GetValue(StaminaRecoveryPercentBonus), 0);
        if (percentBonus <= 0)
            return baseProgressGain;
        return (baseProgressGain * (100 + percentBonus)) / 100;
    }

    internal int NormalizeUnitActionThreshold(int actionThreshold)
    {
        if (actionThreshold <= 0)
        {
            GameLog.Error($"Battle unit action_threshold must be positive, got {actionThreshold}.", "battle.timeline.invalid_threshold", "battle");
            return BattleUnitState.DefaultActionThreshold;
        }
        if (actionThreshold % TuGranularity != 0)
        {
            GameLog.Error(
                $"Battle unit action_threshold must be a multiple of {TuGranularity}, got {actionThreshold}.",
                "battle.timeline.invalid_threshold_multiple",
                "battle"
            );
            return BattleUnitState.DefaultActionThreshold;
        }
        return actionThreshold;
    }

    internal void InitializeUnitActionThresholds()
    {
        var state = _ResolveState();
        if (state == null)
            return;
        foreach (BattleUnitState unitState in state.Units())
            ResolveUnitActionThreshold(unitState);
    }

    internal void InitializeUnitTraitHooks()
    {
        var runtime = _ResolveRuntime();
        var state = _ResolveState();
        var traitTriggerHooks = runtime?._trait_trigger_hooks;
        if (state == null || traitTriggerHooks == null)
            return;
        foreach (BattleState.BattleUnitEntry unitEntry in state.UnitEntries(sorted: true))
        {
            var unitState = unitEntry.Unit;
            if (unitState == null)
                continue;
            traitTriggerHooks.OnBattleStartResult(unitState);
        }
    }

    internal int ResolveUnitActionThreshold(BattleUnitState unitState)
    {
        if (unitState == null)
            return BattleUnitState.DefaultActionThreshold;
        var threshold = unitState.GetActionThresholdTyped();
        if (threshold <= 0)
        {
            threshold = BattleUnitState.DefaultActionThreshold;
            unitState.SetActionThresholdTyped(threshold);
        }
        var normalizedThreshold = NormalizeUnitActionThreshold(threshold);
        if (normalizedThreshold != threshold)
            unitState.SetActionThresholdTyped(normalizedThreshold);
        return normalizedThreshold;
    }

    internal int ResolveTimelineTuPerTick(GDictionary context)
    {
        var tuPerTick =
            context != null && context.ContainsKey("tu_per_tick")
                ? context["tu_per_tick"].AsInt32()
                : TuGranularity;
        if (tuPerTick <= 0)
            return TuGranularity;
        if (tuPerTick % TuGranularity != 0)
        {
            GameLog.Error(
                $"timeline.tu_per_tick must be a multiple of {TuGranularity}, got {tuPerTick}.",
                "battle.timeline.invalid_tu_per_tick",
                "battle"
            );
            return TuGranularity;
        }
        return tuPerTick;
    }

    internal void EndActiveTurn(BattleEventBatch batch)
    {
        var runtime = _ResolveRuntime();
        var state = _ResolveState();
        if (state == null || batch == null)
            return;
        var activeUnit = state.GetUnit(state.active_unit_id);
        if (
            activeUnit != null
            && activeUnit.IsAlive()
            && !activeUnit.HasTakenActionThisTurnTyped()
        )
        {
            activeUnit.MarkRestingTyped();
            _AppendChangedUnitId(batch, activeUnit.unit_id);
        }
        if (activeUnit != null && runtime != null)
        {
            runtime.GetFateRuntime()
                ?.HandleMisfortuneTrigger(
                    MisfortuneTriggerRequest.LowHpTurnEnd(activeUnit)
                );
        }
        if (
            activeUnit != null
            && runtime?.GetEquipmentAbilityRuntimeService()?.ResolveTurnEnd(
                new BattleEquipmentAbilityTurnEndContext
                {
                    SourceUnit = activeUnit,
                    BattleState = state,
                }
            ) == true
        )
        {
            _AppendChangedUnitId(batch, activeUnit.unit_id);
        }
        if (activeUnit != null && activeUnit.ControlModeKind != BattleUnitControlMode.Manual)
            _CleanupAiTurn(activeUnit);
        else if (activeUnit != null)
        {
            var isAiOverride =
                runtime?._skill_turn_resolver?.IsTurnAiOverrideActive(activeUnit) == true;
            if (isAiOverride)
                _CleanupAiTurn(activeUnit);
        }
        activeUnit?.ClearCastingTurnFlags();
        state.PhaseKind = BattlePhaseKind.TimelineRunning;
        state.active_unit_id = "";
        batch.phase_changed = true;
    }

    internal void ActivateNextReadyUnit(BattleEventBatch batch)
    {
        var runtime = _ResolveRuntime();
        var state = _ResolveState();
        if (state == null || state.timeline == null)
            return;
        while (state.timeline.ready_unit_ids.Count > 0)
        {
            var nextUnitId = state.timeline.ready_unit_ids[0];
            state.timeline.ready_unit_ids.RemoveAt(0);
            var unitState = state.GetUnit(nextUnitId);
            if (unitState == null || !unitState.IsAlive())
                continue;
            if (BattleTemporalStatusService.HasTimeStasis(unitState))
                continue;
            state.PhaseKind = BattlePhaseKind.UnitActing;
            state.active_unit_id = nextUnitId;
            unitState.ResetTurnStateForTurnStartTyped();
            unitState.ResetPerTurnCharges();
            var traitTriggerHooks = runtime?._trait_trigger_hooks;
            TraitDispatchResult traitTurnStartResult = default;
            if (traitTriggerHooks != null)
                traitTurnStartResult = traitTriggerHooks.OnTurnStartResult(unitState);
            if (traitTurnStartResult.Changed)
                _AppendChangedUnitId(batch, unitState.unit_id);
            _AdvanceUnitTurnTimers(unitState, batch);
            _RecordTurnStarted(unitState, batch);
            var actionPoints = 1;
            if (unitState.attribute_snapshot != null)
            {
                actionPoints = Mathf.Max(unitState.attribute_snapshot.GetValue("action_points"), 1);
            }
            unitState.SetCurrentAp(actionPoints);
            unitState.SetCurrentMovePoints(unitState.GetMovePointCapacity());
            var turnStartResult = _ApplyTurnStartStatuses(unitState, batch);
            if (!unitState.IsAlive())
            {
                var defeatSourceUnitId = turnStartResult.DefeatSourceUnitId;
                var defeatSourceUnit = state.GetUnit(defeatSourceUnitId);
                runtime?.HandleUnitDefeatedByRuntimeEffect(
                    unitState,
                    defeatSourceUnit,
                    batch,
                    $"{unitState.display_name} 因持续效果倒下。",
                    new BattleDefeatHandlingOptions(
                        recordEnemyDefeatedAchievement: defeatSourceUnit != null
                    )
                );
                state.PhaseKind = BattlePhaseKind.TimelineRunning;
                state.active_unit_id = "";
                batch.phase_changed = true;
                batch.AddChangedUnitId(nextUnitId);
                state.AppendLogEntry(batch.LogLinesTyped[batch.LogLinesTyped.Count - 1]);
                continue;
            }
            var skillTurnResolver = runtime?._skill_turn_resolver;
            BattleTurnControlStatusResult controlStatusResult =
                BattleTurnControlStatusResult.Empty();
            if (skillTurnResolver != null)
                controlStatusResult = skillTurnResolver.ResolveTurnControlStatusResult(
                    unitState,
                    batch
                );
            if (controlStatusResult.SkipTurn)
            {
                state.PhaseKind = BattlePhaseKind.TimelineRunning;
                state.active_unit_id = "";
                batch.phase_changed = true;
                batch.AddChangedUnitId(nextUnitId);
                continue;
            }
            if (
                unitState.ControlModeKind != BattleUnitControlMode.Manual
                || controlStatusResult.AiControlled
            )
                _PrepareAiTurn(unitState);
            batch.phase_changed = true;
            batch.AddChangedUnitId(nextUnitId);
            var logLine = $"轮到 {unitState.display_name} 行动。";
            batch.AddLogLine(logLine);
            state.AppendLogEntry(logLine);
            return;
        }
    }

    internal void SortReadyUnitIdsByActionPriority()
    {
        var state = _ResolveState();
        if (state == null || state.timeline == null)
            return;
        var orderedReadyIds = new GStringNameArray();
        var seenIds = new HashSet<StringName>();
        foreach (var unitIdValue in state.timeline.ready_unit_ids)
        {
            var unitId = ProgressionDataUtils.to_string_name(unitIdValue);
            if (unitId == "" || seenIds.Contains(unitId))
                continue;
            var unitState = state.GetUnit(unitId);
            if (unitState == null || !unitState.IsAlive())
                continue;
            seenIds.Add(unitId);
            orderedReadyIds.Add(unitId);
        }
        var list = new System.Collections.Generic.List<StringName>(orderedReadyIds);
        list.Sort(
            (a, b) =>
            {
                if (IsLeftReadyUnitHigherPriority(a, b))
                    return -1;
                if (IsLeftReadyUnitHigherPriority(b, a))
                    return 1;
                return 0;
            }
        );
        var sorted = new GStringNameArray();
        foreach (var id in list)
            sorted.Add(id);
        state.timeline.ready_unit_ids = sorted;
    }

    internal bool IsLeftReadyUnitHigherPriority(StringName leftUnitId, StringName rightUnitId)
    {
        var state = _ResolveState();
        var leftUnit = state?.GetUnit(leftUnitId);
        var rightUnit = state?.GetUnit(rightUnitId);
        if (leftUnit == null || !leftUnit.IsAlive())
            return false;
        if (rightUnit == null || !rightUnit.IsAlive())
            return true;
        var leftAgility = GetUnitTurnOrderAttribute(leftUnit, "agility");
        var rightAgility = GetUnitTurnOrderAttribute(rightUnit, "agility");
        if (leftAgility != rightAgility)
            return leftAgility > rightAgility;
        var leftActionPoints = GetUnitTurnOrderActionPoints(leftUnit);
        var rightActionPoints = GetUnitTurnOrderActionPoints(rightUnit);
        if (leftActionPoints != rightActionPoints)
            return leftActionPoints > rightActionPoints;
        var leftMovePoints = Mathf.Max(leftUnit.GetCurrentMovePoints(), 0);
        var rightMovePoints = Mathf.Max(rightUnit.GetCurrentMovePoints(), 0);
        if (leftMovePoints != rightMovePoints)
            return leftMovePoints > rightMovePoints;
        return string.Compare(
                leftUnitId.ToString(),
                rightUnitId.ToString(),
                StringComparison.Ordinal
            ) < 0;
    }

    internal int GetUnitTurnOrderAttribute(BattleUnitState unitState, StringName attributeId)
    {
        if (unitState == null || unitState.attribute_snapshot == null)
            return 0;
        return unitState.attribute_snapshot.GetValue(attributeId);
    }

    internal int GetUnitTurnOrderActionPoints(BattleUnitState unitState)
    {
        var snapshotActionPoints = GetUnitTurnOrderAttribute(unitState, "action_points");
        if (snapshotActionPoints > 0)
            return snapshotActionPoints;
        return Mathf.Max(unitState.GetCurrentAp(), 0);
    }

    internal GStringNameArray GetUnitsInOrder()
    {
        var state = _ResolveState();
        var orderedIds = new GStringNameArray();
        if (state == null)
            return orderedIds;
        foreach (StringName unitId in state.GetUnitIdsTyped(sorted: true))
            orderedIds.Add(unitId);
        return orderedIds;
    }

    private BattleRuntimeModule _ResolveRuntime()
    {
        if (
            _runtimeRef == null
            || !_runtimeRef.TryGetTarget(out BattleRuntimeModule target)
        )
            return null;
        return target;
    }

    private BattleState _ResolveState()
    {
        var runtime = _ResolveRuntime();
        if (runtime == null)
            return null;
        return runtime._state;
    }

}
