using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using Godot;
using GArray = Godot.Collections.Array;
using GBattleUnitArray = System.Collections.Generic.List<BattleUnitState>;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

internal sealed class BattleTimelineStatusBridgeService : BattleRuntimeModuleBorrower
{

    internal bool _use_discrete_timeline_ticks()
    {
        _runtime._ensure_sidecars_ready();
        return _runtime._timeline_driver.UseDiscreteTimelineTicks();
    }

    internal void _apply_timeline_step(BattleEventBatch batch, int tu_delta)
    {
        _runtime._ensure_sidecars_ready();
        _runtime._timeline_driver.ApplyTimelineStep(batch, tu_delta);
    }

    internal void _resolve_timeline_status_phase(BattleEventBatch batch, int tu_delta)
    {
        _runtime._ensure_sidecars_ready();
        _runtime._timeline_driver.ResolveTimelineStatusPhase(batch, tu_delta);
    }

    internal void _collect_timeline_ready_units(BattleEventBatch batch, int tu_delta)
    {
        _runtime._ensure_sidecars_ready();
        _runtime._timeline_driver.CollectTimelineReadyUnits(batch, tu_delta);
    }

    internal bool _apply_stamina_recovery(BattleUnitState unit_state, int tu_delta)
    {
        _runtime._ensure_sidecars_ready();
        return _runtime._timeline_driver.ApplyStaminaRecovery(unit_state, tu_delta);
    }

    internal int _get_unit_constitution(BattleUnitState unit_state)
    {
        _runtime._ensure_sidecars_ready();
        return _runtime._timeline_driver.GetUnitConstitution(unit_state);
    }

    internal int _apply_stamina_recovery_percent_bonus(
        BattleUnitState unit_state,
        int base_progress_gain
    )
    {
        _runtime._ensure_sidecars_ready();
        return _runtime._timeline_driver.ApplyStaminaRecoveryPercentBonus(unit_state, base_progress_gain);
    }

    internal void MarkAppliedStatusesForTurnTiming(
        BattleUnitState target_unit,
        GArray status_effect_ids
    )
    {
        _initialize_applied_status_timeline_ticks(target_unit, status_effect_ids);
        _runtime._fate_runtime?.HandleAppliedStatuses(target_unit, status_effect_ids);
    }

    internal void MarkAppliedStatusesForTurnTiming(
        BattleUnitState target_unit,
        GStringNameArray status_effect_ids
    )
    {
        GStringNameArray normalizedStatusIds = NormalizeStatusIdArray(status_effect_ids);
        _initialize_applied_status_timeline_ticks(target_unit, normalizedStatusIds);
        _runtime._fate_runtime?.HandleAppliedStatuses(target_unit, normalizedStatusIds);
    }

    internal void MarkAppliedStatusesForTurnTiming(
        BattleUnitState target_unit,
        IReadOnlyList<StringName> status_effect_ids
    )
    {
        _initialize_applied_status_timeline_ticks(target_unit, status_effect_ids);
        _runtime._fate_runtime?.HandleAppliedStatuses(target_unit, status_effect_ids);
    }

    internal void _initialize_applied_status_timeline_ticks(
        BattleUnitState target_unit,
        GArray status_effect_ids
    )
    {
        _initialize_applied_status_timeline_ticks(
            target_unit,
            NormalizeStatusIdArray(status_effect_ids)
        );
    }

    internal void _initialize_applied_status_timeline_ticks(
        BattleUnitState target_unit,
        GStringNameArray status_effect_ids
    )
    {
        if (target_unit == null)
            return;
        GStringNameArray normalizedStatusIds = NormalizeStatusIdArray(status_effect_ids);
        if (normalizedStatusIds.Count == 0)
            return;
        int currentTu = _runtime._state?.timeline != null ? _runtime._state.timeline.current_tu : 0;
        foreach (StringName statusId in normalizedStatusIds)
        {
            BattleStatusEffectState statusEntry = target_unit.GetStatusEffect(statusId);
            if (statusEntry == null || statusEntry.tick_interval_tu <= 0)
                continue;
            if (statusEntry.next_tick_at_tu <= currentTu)
            {
                statusEntry.next_tick_at_tu = currentTu + statusEntry.tick_interval_tu;
                target_unit.SetStatusEffect(statusEntry);
            }
        }
    }

    internal void _initialize_applied_status_timeline_ticks(
        BattleUnitState target_unit,
        IReadOnlyList<StringName> status_effect_ids
    )
    {
        if (target_unit == null || status_effect_ids == null || status_effect_ids.Count == 0)
            return;
        int currentTu = _runtime._state?.timeline != null ? _runtime._state.timeline.current_tu : 0;
        var seenStatusIds = new HashSet<StringName>();
        foreach (StringName rawStatusId in status_effect_ids)
        {
            StringName statusId = ProgressionDataUtils.to_string_name(rawStatusId);
            if (statusId == "" || !seenStatusIds.Add(statusId))
                continue;
            BattleStatusEffectState statusEntry = target_unit.GetStatusEffect(statusId);
            if (statusEntry == null || statusEntry.tick_interval_tu <= 0)
                continue;
            if (statusEntry.next_tick_at_tu <= currentTu)
            {
                statusEntry.next_tick_at_tu = currentTu + statusEntry.tick_interval_tu;
                target_unit.SetStatusEffect(statusEntry);
            }
        }
    }

    internal static GStringNameArray NormalizeStatusIdArray(GArray statusEffectIds)
    {
        GStringNameArray normalized = new();
        if (statusEffectIds == null)
            return normalized;
        foreach (var statusIdValue in statusEffectIds)
        {
            StringName statusId = ProgressionDataUtils.to_string_name(statusIdValue);
            if (statusId == "" || normalized.Contains(statusId))
                continue;
            normalized.Add(statusId);
        }
        return normalized;
    }

    internal static GStringNameArray NormalizeStatusIdArray(GStringNameArray statusEffectIds)
    {
        GStringNameArray normalized = new();
        if (statusEffectIds == null)
            return normalized;
        foreach (StringName statusIdValue in statusEffectIds)
        {
            StringName statusId = ProgressionDataUtils.to_string_name(statusIdValue);
            if (statusId == "" || normalized.Contains(statusId))
                continue;
            normalized.Add(statusId);
        }
        return normalized;
    }

    internal int _normalize_unit_action_threshold(int action_threshold)
    {
        _runtime._ensure_sidecars_ready();
        return _runtime._timeline_driver.NormalizeUnitActionThreshold(action_threshold);
    }

    internal void _initialize_unit_action_thresholds()
    {
        _runtime._ensure_sidecars_ready();
        _runtime._timeline_driver.InitializeUnitActionThresholds();
    }

    internal int _resolve_unit_action_threshold(BattleUnitState unit_state)
    {
        _runtime._ensure_sidecars_ready();
        return _runtime._timeline_driver.ResolveUnitActionThreshold(unit_state);
    }

    internal int _resolve_timeline_tu_per_tick(GDictionary context)
    {
        _runtime._ensure_sidecars_ready();
        return _runtime._timeline_driver.ResolveTimelineTuPerTick(context);
    }

    internal void _ensure_unit_turn_anchor(BattleUnitState unit_state) =>
        _runtime._skill_turn_resolver.EnsureUnitTurnAnchor(unit_state);

    internal bool _advance_unit_cooldowns(BattleUnitState unit_state, int cooldown_delta) =>
        _runtime._skill_turn_resolver.AdvanceUnitCooldowns(unit_state, cooldown_delta);

    internal bool _consume_turn_cooldown_delta(BattleUnitState unit_state) =>
        _runtime._skill_turn_resolver.ConsumeTurnCooldownDelta(unit_state);

    internal void _advance_unit_turn_timers(BattleUnitState unit_state, BattleEventBatch batch) =>
        _runtime._skill_turn_resolver.AdvanceUnitTurnTimers(unit_state, batch);

    internal BattleStatusTickResult _apply_turn_start_statuses_result(
        BattleUnitState unit_state,
        BattleEventBatch batch
    ) => _runtime._skill_turn_resolver.ApplyTurnStartStatusesResult(unit_state, batch);

    internal BattleStatusTickResult _apply_unit_status_periodic_ticks_result(
        BattleUnitState unit_state,
        int elapsed_tu,
        BattleEventBatch batch
    ) => _runtime._skill_turn_resolver.ApplyUnitStatusPeriodicTicksResult(unit_state, elapsed_tu, batch);

    internal bool _advance_unit_status_durations(
        BattleUnitState unit_state,
        int elapsed_tu,
        BattleEventBatch batch = null
    ) => _runtime._skill_turn_resolver.AdvanceUnitStatusDurations(unit_state, elapsed_tu, batch);
}
