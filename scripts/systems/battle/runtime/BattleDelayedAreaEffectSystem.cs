using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleDelayedAreaEffectSystem : IDisposable
{
    private static readonly StringName StackBehaviorRefresh = "refresh";
    private static readonly StringName StackBehaviorStack = "stack";
    private static readonly StringName StackBehaviorIgnoreExisting = "ignore_existing";

    private readonly List<PendingAreaEffect> _pendingEffects = new();
    private WeakReference<BattleRuntimeModule> _runtimeRef;

    internal void Setup(BattleRuntimeModule runtime)
    {
        _runtimeRef = runtime != null ? new WeakReference<BattleRuntimeModule>(runtime) : null;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _runtimeRef = null;
        _pendingEffects.Clear();
    }

    internal void Clear()
    {
        _pendingEffects.Clear();
    }

    internal void ScheduleFromEquipmentAction(
        BattleUnitState sourceUnit,
        BattleUnitState anchorUnit,
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityActionDefinition action,
        ScheduleAreaEffectActionPayloadDefinition payload
    )
    {
        BattleRuntimeModule runtime = ResolveRuntime();
        BattleState state = runtime?.GetState();
        if (
            runtime == null
            || state?.timeline == null
            || sourceUnit == null
            || anchorUnit == null
            || payload == null
            || payload.DelayTu <= 0
            || payload.TerrainEffectId == ""
        )
        {
            return;
        }

        int nonce = runtime.IncrementTerrainEffectNonce();
        _pendingEffects.Add(
            new PendingAreaEffect
            {
                DueTu = state.timeline.current_tu + payload.DelayTu,
                AnchorCoord = anchorUnit.GetAnchorCoord(),
                SourceUnitId = sourceUnit.unit_id,
                BindingId = binding?.BindingId ?? new StringName(""),
                ActionId = action?.ActionId ?? new StringName(""),
                FieldInstanceId = new StringName($"equipment_area:{nonce}"),
                Payload = payload,
            }
        );
    }

    internal void ProcessDueEffects(BattleEventBatch batch)
    {
        BattleRuntimeModule runtime = ResolveRuntime();
        BattleState state = runtime?.GetState();
        BattleTimelineState timeline = state?.timeline;
        if (runtime == null || state == null || timeline == null || _pendingEffects.Count == 0)
            return;

        for (int index = _pendingEffects.Count - 1; index >= 0; index--)
        {
            PendingAreaEffect pending = _pendingEffects[index];
            if (pending == null || timeline.current_tu < pending.DueTu)
                continue;
            MaterializeAreaEffect(runtime, state, pending, batch);
            _pendingEffects.RemoveAt(index);
        }
    }

    private static void MaterializeAreaEffect(
        BattleRuntimeModule runtime,
        BattleState state,
        PendingAreaEffect pending,
        BattleEventBatch batch
    )
    {
        BattleGridService gridService = runtime.GetGridService();
        ScheduleAreaEffectActionPayloadDefinition payload = pending.Payload;
        if (gridService == null || payload == null)
            return;

        List<Vector2I> coords = gridService.GetAreaCoords(
            state,
            pending.AnchorCoord,
            payload.AreaPattern,
            Math.Max(payload.AreaValue, 0)
        );
        var occupantUnitIds = new HashSet<StringName>();
        foreach (Vector2I coord in coords)
        {
            BattleCellState cell = gridService.GetCellState(state, coord);
            if (cell == null)
                continue;
            BattleTerrainEffectState effectState = BuildTerrainEffectState(
                pending,
                payload,
                runtime.GetState()?.timeline
            );
            if (effectState == null)
                continue;
            if (!UpsertTerrainEffect(cell, effectState))
                continue;
            runtime.AppendChangedCoord(batch, coord);
            if (cell.occupant_unit_id != "")
                occupantUnitIds.Add(cell.occupant_unit_id);
        }
        foreach (StringName unitId in occupantUnitIds)
        {
            BattleUnitState unit = state.GetUnit(unitId);
            if (unit != null)
            {
                runtime._terrain_effect_system?.ApplyContactEffectsForUnit(
                    unit,
                    BattleSaveContext.Empty,
                    batch
                );
            }
        }
    }

    private static BattleTerrainEffectState BuildTerrainEffectState(
        PendingAreaEffect pending,
        ScheduleAreaEffectActionPayloadDefinition payload,
        BattleTimelineState timeline
    )
    {
        if (pending == null || payload == null)
            return null;
        int tickIntervalTu = Math.Max(payload.ContactTickIntervalTu, 0);
        return new BattleTerrainEffectState
        {
            field_instance_id = pending.FieldInstanceId,
            effect_id = payload.TerrainEffectId,
            effect_type = payload.EffectType == "" ? new StringName("none") : payload.EffectType,
            lifetime_policy =
                payload.LifetimePolicy == "" ? new StringName("timed") : payload.LifetimePolicy,
            render_overlay_id = payload.RenderOverlayId,
            overlay_priority = payload.OverlayPriority,
            display_name = payload.DisplayName ?? "",
            source_unit_id = pending.SourceUnitId,
            target_team_filter =
                payload.TargetTeamFilter == "" ? new StringName("any") : payload.TargetTeamFilter,
            stack_behavior =
                payload.StackBehavior == "" ? StackBehaviorRefresh : payload.StackBehavior,
            remaining_tu = 0,
            tick_interval_tu = 0,
            next_tick_at_tu = 0,
            contact_status_id = payload.ContactStatusId,
            contact_status_duration_tu = payload.ContactStatusDurationTu,
            contact_stack_behavior =
                payload.ContactStackBehavior == "" ? StackBehaviorRefresh : payload.ContactStackBehavior,
            contact_stack_limit = payload.ContactStackLimit,
            contact_status_display_label = payload.ContactStatusDisplayLabel ?? "",
            contact_counts_as_debuff_override = payload.ContactCountsAsDebuffOverride,
            contact_counts_as_debuff = payload.ContactCountsAsDebuff,
            contact_undispellable = payload.ContactUndispellable,
            contact_dispellable_magic = payload.ContactDispellableMagic,
            contact_dispellable_harmful_magic = payload.ContactDispellableHarmfulMagic,
            contact_dispellable_beneficial_magic = payload.ContactDispellableBeneficialMagic,
            contact_save_dc = payload.ContactSaveDc,
            contact_save_ability = payload.ContactSaveAbility,
            contact_save_tag = payload.ContactSaveTag,
            contact_apply_on_save_failure = payload.ContactApplyOnSaveFailure,
            contact_tick_interval_tu = tickIntervalTu,
            contact_timeline_damage_dice_count = payload.ContactTimelineDamageDiceCount,
            contact_timeline_damage_dice_sides = payload.ContactTimelineDamageDiceSides,
            contact_timeline_damage_flat_bonus = payload.ContactTimelineDamageFlatBonus,
            contact_blocked_by_trait_id = payload.ContactBlockedByTraitId,
        };
    }

    private static bool UpsertTerrainEffect(
        BattleCellState cell,
        BattleTerrainEffectState effectState
    )
    {
        if (cell == null || effectState == null || effectState.effect_id == "")
            return false;

        StringName stackBehavior = NormalizeStackBehavior(effectState.stack_behavior);
        int existingIndex = -1;
        for (int index = 0; index < cell.timed_terrain_effects.Count; index++)
        {
            BattleTerrainEffectState existing = cell.timed_terrain_effects[index];
            if (existing?.effect_id == effectState.effect_id)
            {
                existingIndex = index;
                break;
            }
        }

        if (existingIndex >= 0)
        {
            if (stackBehavior == StackBehaviorIgnoreExisting)
                return false;
            if (stackBehavior == StackBehaviorRefresh)
            {
                cell.timed_terrain_effects[existingIndex] = effectState;
                return true;
            }
        }

        cell.timed_terrain_effects.Add(effectState);
        return true;
    }

    private BattleRuntimeModule ResolveRuntime()
    {
        if (_runtimeRef == null)
            return null;
        _runtimeRef.TryGetTarget(out BattleRuntimeModule runtime);
        return runtime;
    }

    private static StringName NormalizeStackBehavior(StringName value)
    {
        if (value == StackBehaviorStack || value == StackBehaviorIgnoreExisting)
            return value;
        return StackBehaviorRefresh;
    }

    private sealed class PendingAreaEffect
    {
        internal int DueTu { get; init; }
        internal Vector2I AnchorCoord { get; init; }
        internal StringName SourceUnitId { get; init; } = "";
        internal StringName BindingId { get; init; } = "";
        internal StringName ActionId { get; init; } = "";
        internal StringName FieldInstanceId { get; init; } = "";
        internal ScheduleAreaEffectActionPayloadDefinition Payload { get; init; }
    }
}
