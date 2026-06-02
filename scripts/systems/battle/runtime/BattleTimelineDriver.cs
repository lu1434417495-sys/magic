using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public sealed class BattleTimelineDriver : IDisposable
{
    private const int TuGranularity = 5;
    private const int StaminaRecoveryProgressBase = 11;
    private const int StaminaRecoveryProgressDenominator = 10;
    private const int StaminaRestingRecoveryMultiplier = 2;
    private static readonly StringName StaminaRecoveryPercentBonus =
        "stamina_recovery_percent_bonus";
    private static readonly StringName CalamityReasonLowHpEndTurn = "low_hp_end_turn";

    private WeakReference<BattleRuntimeModule> _runtimeRef;

    public void Setup(BattleRuntimeModule runtime)
    {
        _runtimeRef = runtime != null ? new WeakReference<BattleRuntimeModule>(runtime) : null;
    }

    public void Dispose()
    {
        _runtimeRef = null;
    }

    public void AdvanceTimeline(int tickCount, BattleEventBatch batch)
    {
        var runtime = _ResolveRuntime();
        var state = _ResolveState();
        if (state == null || state.timeline == null || tickCount <= 0)
            return;
        var resolvedTickCount = Mathf.Max(tickCount, 0);
        for (int i = 0; i < resolvedTickCount; i++)
        {
            ApplyTimelineStep(batch, state.timeline.tu_per_tick);
            if (CheckBattleEnd(batch))
                return;
        }
    }

    private void _RecordTurnStarted(BattleUnitState unitState)
    {
        var runtime = _ResolveRuntime();
        if (runtime == null)
            return;
        runtime._record_turn_started(unitState);
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
        BattleUnitState killerUnit = null
    )
    {
        var runtime = _ResolveRuntime();
        if (runtime == null)
            return;
        runtime._collect_defeated_unit_loot(unitState, killerUnit);
    }

    private void _ClearDefeatedUnit(BattleUnitState unitState, BattleEventBatch batch = null)
    {
        var runtime = _ResolveRuntime();
        if (runtime == null)
            return;
        runtime._clear_defeated_unit(unitState, batch);
    }

    private void _AdvanceUnitTurnTimers(BattleUnitState unitState, BattleEventBatch batch)
    {
        var runtime = _ResolveRuntime();
        if (runtime == null)
            return;
        runtime._advance_unit_turn_timers(unitState, batch);
    }

    private BattleStatusTickResult _ApplyTurnStartStatuses(
        BattleUnitState unitState,
        BattleEventBatch batch
    )
    {
        var runtime = _ResolveRuntime();
        if (runtime == null)
            return BattleStatusTickResult.Empty();
        return runtime._apply_turn_start_statuses_result(unitState, batch);
    }

    private BattleStatusTickResult _ApplyUnitStatusPeriodicTicks(
        BattleUnitState unitState,
        int elapsedTu,
        BattleEventBatch batch
    )
    {
        var runtime = _ResolveRuntime();
        if (runtime == null)
            return BattleStatusTickResult.Empty();
        return runtime._apply_unit_status_periodic_ticks_result(unitState, elapsedTu, batch);
    }

    private bool _AdvanceUnitStatusDurations(
        BattleUnitState unitState,
        int elapsedTu,
        BattleEventBatch batch = null
    )
    {
        var runtime = _ResolveRuntime();
        if (runtime == null)
            return false;
        return runtime._advance_unit_status_durations(unitState, elapsedTu, batch);
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

    private BattleResolutionResult _BuildBattleResolutionResult()
    {
        var runtime = _ResolveRuntime();
        if (runtime == null)
            return null;
        return runtime._build_battle_resolution_result();
    }

    public bool UseDiscreteTimelineTicks()
    {
        var state = _ResolveState();
        return state != null && state.timeline != null && state.timeline.tu_per_tick > 0;
    }

    public void ApplyTimelineStep(BattleEventBatch batch, int tuDelta)
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
        if (tuDelta > 0)
        {
            state.timeline.current_tu += tuDelta;
            ResolveTimelineStatusPhase(batch, tuDelta);
        }
        runtime?._terrain_effect_system?.process_timed_terrain_effects(batch);
        runtime?._layered_barrier_service?.AdvanceBarrierDurations(tuDelta, batch);
        if (tuDelta > 0)
        {
            CollectTimelineReadyUnits(batch, tuDelta);
        }
        SortReadyUnitIdsByActionPriority();
    }

    public void ResolveTimelineStatusPhase(BattleEventBatch batch, int tuDelta)
    {
        var state = _ResolveState();
        if (state == null || state.timeline == null || tuDelta <= 0)
            return;
        foreach (StringName unitId in GetUnitsInOrder())
        {
            var unitState = state.units.ContainsKey(unitId)
                ? state.units[unitId].As<BattleUnitState>()
                : null;
            if (unitState == null || !unitState.is_alive)
                continue;
            var statusTickResult = _ApplyUnitStatusPeriodicTicks(unitState, tuDelta, batch);
            if (statusTickResult.Changed)
                _AppendChangedUnitId(batch, unitState.unit_id);
            if (!unitState.is_alive)
            {
                var defeatSourceUnitId = statusTickResult.DefeatSourceUnitId;
                var defeatSourceUnit =
                    (
                        !string.IsNullOrEmpty(defeatSourceUnitId.ToString())
                        && state.units.ContainsKey(defeatSourceUnitId)
                    )
                        ? state.units[defeatSourceUnitId].As<BattleUnitState>()
                        : null;
                var runtime = _ResolveRuntime();
                runtime?.handle_unit_defeated_by_runtime_effect(
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

    public void CollectTimelineReadyUnits(BattleEventBatch batch, int tuDelta)
    {
        var runtime = _ResolveRuntime();
        var state = _ResolveState();
        if (state == null || state.timeline == null || tuDelta <= 0)
            return;
        foreach (StringName unitId in GetUnitsInOrder())
        {
            var unitState = state.units.ContainsKey(unitId)
                ? state.units[unitId].As<BattleUnitState>()
                : null;
            if (unitState == null || !unitState.is_alive)
                continue;
            if (ApplyStaminaRecovery(unitState, tuDelta))
                _AppendChangedUnitId(batch, unitState.unit_id);
            if (!unitState.is_alive)
                continue;
            unitState.action_progress += tuDelta;
            var actionThreshold = ResolveUnitActionThreshold(unitState);
            while (unitState.action_progress >= actionThreshold)
            {
                unitState.action_progress -= actionThreshold;
                if (!state.timeline.ready_unit_ids.Contains(unitId))
                    state.timeline.ready_unit_ids.Add(unitId);
            }
        }
    }

    public bool ApplyStaminaRecovery(BattleUnitState unitState, int tuDelta)
    {
        if (unitState == null || tuDelta <= 0)
            return false;
        var tickCount = tuDelta / TuGranularity;
        if (tickCount <= 0)
            return false;
        var staminaMax = _GetUnitStaminaMax(unitState);
        if (staminaMax <= 0)
        {
            if (unitState.current_stamina != 0 || unitState.stamina_recovery_progress != 0)
            {
                unitState.current_stamina = 0;
                unitState.stamina_recovery_progress = 0;
                return true;
            }
            return false;
        }

        bool changed = false;
        if (unitState.current_stamina >= staminaMax)
        {
            if (unitState.current_stamina != staminaMax || unitState.stamina_recovery_progress != 0)
            {
                unitState.current_stamina = staminaMax;
                unitState.stamina_recovery_progress = 0;
                changed = true;
            }
            return changed;
        }

        var constitution = _GetUnitConstitution(unitState);
        var progressGainPerTick = StaminaRecoveryProgressBase + constitution;
        progressGainPerTick = _ApplyStaminaRecoveryPercentBonus(unitState, progressGainPerTick);
        if (unitState.is_resting)
            progressGainPerTick *= StaminaRestingRecoveryMultiplier;

        for (int i = 0; i < tickCount; i++)
        {
            unitState.stamina_recovery_progress += progressGainPerTick;
            var recovered =
                unitState.stamina_recovery_progress / StaminaRecoveryProgressDenominator;
            if (recovered <= 0)
                continue;
            unitState.current_stamina = Mathf.Min(
                unitState.current_stamina + recovered,
                staminaMax
            );
            unitState.stamina_recovery_progress %= StaminaRecoveryProgressDenominator;
            changed = true;
            if (unitState.current_stamina >= staminaMax)
            {
                unitState.current_stamina = staminaMax;
                unitState.stamina_recovery_progress = 0;
                break;
            }
        }

        return changed;
    }

    public int _GetUnitConstitution(BattleUnitState unitState)
    {
        if (unitState == null || unitState.attribute_snapshot == null)
            return 0;
        var snapshot = unitState.attribute_snapshot;
        return Mathf.Max(snapshot.get_value("constitution"), 0);
    }

    public int _ApplyStaminaRecoveryPercentBonus(BattleUnitState unitState, int baseProgressGain)
    {
        if (unitState == null || unitState.attribute_snapshot == null)
            return baseProgressGain;
        var snapshot = unitState.attribute_snapshot;
        var percentBonus = Mathf.Max(snapshot.get_value(StaminaRecoveryPercentBonus), 0);
        if (percentBonus <= 0)
            return baseProgressGain;
        return (baseProgressGain * (100 + percentBonus)) / 100;
    }

    public int NormalizeUnitActionThreshold(int actionThreshold)
    {
        if (actionThreshold <= 0)
        {
            GameLog.Error($"Battle unit action_threshold must be positive, got {actionThreshold}.", "battle.timeline.invalid_threshold", "battle");
            return BattleUnitState.DEFAULT_ACTION_THRESHOLD();
        }
        if (actionThreshold % TuGranularity != 0)
        {
            GameLog.Error(
                $"Battle unit action_threshold must be a multiple of {TuGranularity}, got {actionThreshold}.",
                "battle.timeline.invalid_threshold_multiple",
                "battle"
            );
            return BattleUnitState.DEFAULT_ACTION_THRESHOLD();
        }
        return actionThreshold;
    }

    public void InitializeUnitActionThresholds()
    {
        var state = _ResolveState();
        if (state == null || state.units == null)
            return;
        foreach (var unitValue in state.units.Values)
        {
            ResolveUnitActionThreshold(unitValue.As<BattleUnitState>());
        }
    }

    public void InitializeUnitTraitHooks()
    {
        var runtime = _ResolveRuntime();
        var state = _ResolveState();
        var traitTriggerHooks = runtime?._trait_trigger_hooks;
        if (state == null || state.units == null || traitTriggerHooks == null)
            return;
        foreach (string unitIdStr in ProgressionDataUtils.sorted_string_keys(state.units))
        {
            var unitState = state.units.ContainsKey(unitIdStr)
                ? state.units[unitIdStr].As<BattleUnitState>()
                : null;
            if (unitState == null)
                continue;
            traitTriggerHooks.on_battle_start(
                unitState,
                new GDictionary { ["battle_state"] = state }
            );
        }
    }

    public int ResolveUnitActionThreshold(BattleUnitState unitState)
    {
        if (unitState == null)
            return BattleUnitState.DEFAULT_ACTION_THRESHOLD();
        var threshold = unitState.action_threshold;
        if (threshold <= 0)
        {
            threshold = BattleUnitState.DEFAULT_ACTION_THRESHOLD();
            unitState.action_threshold = threshold;
        }
        var normalizedThreshold = NormalizeUnitActionThreshold(threshold);
        if (normalizedThreshold != threshold)
            unitState.action_threshold = normalizedThreshold;
        return normalizedThreshold;
    }

    public int ResolveTimelineTuPerTick(GDictionary context)
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

    public bool CheckBattleEnd(BattleEventBatch batch)
    {
        var runtime = _ResolveRuntime();
        var state = _ResolveState();
        if (state == null || batch == null)
            return false;
        if (state.phase == (StringName)"battle_ended")
            return true;
        state.normalize_unit_id_arrays();
        var allyUnitIds = state.get_ally_unit_ids_typed();
        var enemyUnitIds = state.get_enemy_unit_ids_typed();
        var livingAllies = CountLivingUnits(allyUnitIds);
        var livingEnemies = CountLivingUnits(enemyUnitIds);
        if (livingAllies > 0 && livingEnemies > 0)
            return false;

        state.phase = "battle_ended";
        if (livingAllies <= 0 && livingEnemies <= 0)
            state.winner_faction_id = "draw";
        else if (livingAllies > 0)
            state.winner_faction_id = "player";
        else
            state.winner_faction_id = "hostile";
        state.active_unit_id = "";
        state.timeline.ready_unit_ids.Clear();
        state.timeline.frozen = true;
        runtime?._battle_rating_system?.record_battle_won_achievements();
        runtime?._battle_rating_system?.finalize_battle_rating_rewards();
        if (runtime?._battle_resolution_result == null)
            runtime._battle_resolution_result = _BuildBattleResolutionResult();
        if (runtime != null)
            runtime._battle_resolution_result_consumed = false;
        batch.phase_changed = true;
        batch.battle_ended = true;
        var line = $"战斗结束，胜利方：{state.winner_faction_id}。";
        batch.log_lines.Add(line);
        state.append_log_entry(line);
        return true;
    }

    public int CountLivingUnits(GStringNameArray unitIds)
    {
        var state = _ResolveState();
        int count = 0;
        foreach (StringName unitId in unitIds)
        {
            var unitState =
                state?.units.ContainsKey(unitId) == true
                    ? state.units[unitId].As<BattleUnitState>()
                    : null;
            if (unitState != null && unitState.is_alive)
                count++;
        }
        return count;
    }

    public void EndActiveTurn(BattleEventBatch batch)
    {
        var runtime = _ResolveRuntime();
        var state = _ResolveState();
        if (state == null || batch == null)
            return;
        var activeUnit = state.units.ContainsKey(state.active_unit_id)
            ? state.units[state.active_unit_id].As<BattleUnitState>()
            : null;
        if (activeUnit != null && activeUnit.is_alive && !activeUnit.has_taken_action_this_turn)
        {
            activeUnit.is_resting = true;
            _AppendChangedUnitId(batch, activeUnit.unit_id);
        }
        if (activeUnit != null && runtime != null)
        {
            runtime.handle_misfortune_trigger(
                CalamityReasonLowHpEndTurn,
                new GDictionary { ["unit_state"] = activeUnit }
            );
        }
        if (activeUnit != null && activeUnit.control_mode != "manual")
            _CleanupAiTurn(activeUnit);
        else if (activeUnit != null)
        {
            var isAiOverride =
                runtime?._skill_turn_resolver?.is_turn_ai_override_active(activeUnit) == true;
            if (isAiOverride)
                _CleanupAiTurn(activeUnit);
        }
        state.phase = "timeline_running";
        state.active_unit_id = "";
        batch.phase_changed = true;
    }

    public void ActivateNextReadyUnit(BattleEventBatch batch)
    {
        var runtime = _ResolveRuntime();
        var state = _ResolveState();
        if (state == null || state.timeline == null)
            return;
        while (state.timeline.ready_unit_ids.Count > 0)
        {
            var nextUnitId = state.timeline.ready_unit_ids[0];
            state.timeline.ready_unit_ids.RemoveAt(0);
            var unitState = state.units.ContainsKey(nextUnitId)
                ? state.units[nextUnitId].As<BattleUnitState>()
                : null;
            if (unitState == null || !unitState.is_alive)
                continue;
            state.phase = "unit_acting";
            state.active_unit_id = nextUnitId;
            unitState.has_taken_action_this_turn = false;
            unitState.has_moved_this_turn = false;
            unitState.can_use_locked_move_points_this_turn = false;
            unitState.reset_per_turn_charges();
            var traitTriggerHooks = runtime?._trait_trigger_hooks;
            TraitDispatchResult traitTurnStartResult = default;
            if (traitTriggerHooks != null)
                traitTurnStartResult = traitTriggerHooks.OnTurnStartResult(
                    unitState,
                    new GDictionary { ["battle_state"] = state }
                );
            if (traitTurnStartResult.Changed)
                _AppendChangedUnitId(batch, unitState.unit_id);
            _AdvanceUnitTurnTimers(unitState, batch);
            _RecordTurnStarted(unitState);
            var actionPoints = 1;
            if (unitState.attribute_snapshot != null)
            {
                actionPoints = Mathf.Max(unitState.attribute_snapshot.get_value("action_points"), 1);
            }
            unitState.current_ap = actionPoints;
            unitState.current_move_points = BattleUnitState.DEFAULT_MOVE_POINTS_PER_TURN();
            var turnStartResult = _ApplyTurnStartStatuses(unitState, batch);
            if (!unitState.is_alive)
            {
                var defeatSourceUnitId = turnStartResult.DefeatSourceUnitId;
                var defeatSourceUnit =
                    (
                        !string.IsNullOrEmpty(defeatSourceUnitId.ToString())
                        && state.units.ContainsKey(defeatSourceUnitId)
                    )
                        ? state.units[defeatSourceUnitId].As<BattleUnitState>()
                        : null;
                runtime?.handle_unit_defeated_by_runtime_effect(
                    unitState,
                    defeatSourceUnit,
                    batch,
                    $"{unitState.display_name} 因持续效果倒下。",
                    new BattleDefeatHandlingOptions(
                        recordEnemyDefeatedAchievement: defeatSourceUnit != null,
                        checkBattleEnd: false
                    )
                );
                state.phase = "timeline_running";
                state.active_unit_id = "";
                batch.phase_changed = true;
                batch.changed_unit_ids.Add(nextUnitId);
                state.append_log_entry(batch.log_lines[batch.log_lines.Count - 1]);
                if (CheckBattleEnd(batch))
                    return;
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
                state.phase = "timeline_running";
                state.active_unit_id = "";
                batch.phase_changed = true;
                batch.changed_unit_ids.Add(nextUnitId);
                continue;
            }
            if (
                unitState.control_mode != "manual"
                || controlStatusResult.AiControlled
            )
                _PrepareAiTurn(unitState);
            batch.phase_changed = true;
            batch.changed_unit_ids.Add(nextUnitId);
            var logLine = $"轮到 {unitState.display_name} 行动。";
            batch.log_lines.Add(logLine);
            state.append_log_entry(logLine);
            return;
        }
    }

    public void SortReadyUnitIdsByActionPriority()
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
            var unitState = state.units.ContainsKey(unitId)
                ? state.units[unitId].As<BattleUnitState>()
                : null;
            if (unitState == null || !unitState.is_alive)
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

    public bool IsLeftReadyUnitHigherPriority(StringName leftUnitId, StringName rightUnitId)
    {
        var state = _ResolveState();
        var leftUnit =
            state?.units.ContainsKey(leftUnitId) == true
                ? state.units[leftUnitId].As<BattleUnitState>()
                : null;
        var rightUnit =
            state?.units.ContainsKey(rightUnitId) == true
                ? state.units[rightUnitId].As<BattleUnitState>()
                : null;
        if (leftUnit == null || !leftUnit.is_alive)
            return false;
        if (rightUnit == null || !rightUnit.is_alive)
            return true;
        var leftAgility = _GetUnitTurnOrderAttribute(leftUnit, "agility");
        var rightAgility = _GetUnitTurnOrderAttribute(rightUnit, "agility");
        if (leftAgility != rightAgility)
            return leftAgility > rightAgility;
        var leftActionPoints = _GetUnitTurnOrderActionPoints(leftUnit);
        var rightActionPoints = _GetUnitTurnOrderActionPoints(rightUnit);
        if (leftActionPoints != rightActionPoints)
            return leftActionPoints > rightActionPoints;
        var leftMovePoints = Mathf.Max(leftUnit.current_move_points, 0);
        var rightMovePoints = Mathf.Max(rightUnit.current_move_points, 0);
        if (leftMovePoints != rightMovePoints)
            return leftMovePoints > rightMovePoints;
        return string.Compare(
                leftUnitId.ToString(),
                rightUnitId.ToString(),
                StringComparison.Ordinal
            ) < 0;
    }

    public int _GetUnitTurnOrderAttribute(BattleUnitState unitState, StringName attributeId)
    {
        if (unitState == null || unitState.attribute_snapshot == null)
            return 0;
        return unitState.attribute_snapshot.get_value(attributeId);
    }

    public int _GetUnitTurnOrderActionPoints(BattleUnitState unitState)
    {
        var snapshotActionPoints = _GetUnitTurnOrderAttribute(unitState, "action_points");
        if (snapshotActionPoints > 0)
            return snapshotActionPoints;
        return Mathf.Max(unitState.current_ap, 0);
    }

    public GStringNameArray GetUnitsInOrder()
    {
        var state = _ResolveState();
        var orderedIds = new GStringNameArray();
        foreach (string unitIdStr in ProgressionDataUtils.sorted_string_keys(state.units))
            orderedIds.Add(new StringName(unitIdStr));
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
