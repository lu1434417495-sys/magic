using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleTerrainEffectSystem : RefCounted
{
    private static readonly StringName TerrainEffectDamage = "damage";
    private static readonly StringName TerrainEffectMovementCost = "movement_cost";
    private static readonly StringName TerrainEffectNone = "none";
    private static readonly StringName LifetimePolicyTimed = "timed";
    private static readonly StringName LifetimePolicyBattle = "battle";
    private static readonly StringName StackBehaviorRefresh = "refresh";
    private static readonly StringName StackBehaviorStack = "stack";
    private static readonly StringName StackBehaviorIgnoreExisting = "ignore_existing";
    private static readonly string ParamLifetimePolicy = "lifetime_policy";
    private static readonly string ParamMoveCostDelta = "move_cost_delta";
    private static readonly string ParamDoesNotStackWithStatusId = "does_not_stack_with_status_id";
    private static readonly string ParamDoesNotStackWithStatusIds =
        "does_not_stack_with_status_ids";
    private const int TuGranularity = 5;

    private WeakReference<BattleRuntimeModule> _runtimeRef = null;

    private BattleRuntimeModule _ResolveRuntime()
    {
        if (_runtimeRef == null)
            return null;
        _runtimeRef.TryGetTarget(out var runtime);
        return runtime;
    }

    public void Setup(BattleRuntimeModule runtime)
    {
        _runtimeRef = runtime != null ? new WeakReference<BattleRuntimeModule>(runtime) : null;
    }

    public void setup(BattleRuntimeModule runtime) => Setup(runtime);

    public new void Dispose()
    {
        _runtimeRef = null;
        base.Dispose();
    }

    public void dispose() => Dispose();

    public int GetMoveCostDeltaForUnitTarget(BattleUnitState unitState, Vector2I targetCoord)
    {
        var runtime = _ResolveRuntime();
        if (runtime == null || unitState == null)
            return 0;

        BattleState state = runtime.get_state();
        BattleGridService gridService = runtime.get_grid_service();
        if (state == null || gridService == null)
            return 0;

        int maxDelta = 0;
        var targetCoords = gridService.get_unit_target_coords(unitState, targetCoord);
        foreach (Vector2I coord in targetCoords)
        {
            BattleCellState cell = gridService.get_cell(state, coord);
            if (cell == null || cell.timed_terrain_effects.Count == 0)
                continue;

            foreach (var effectState in cell.timed_terrain_effects)
            {
                int moveCostDelta = _GetTimedTerrainMoveCostDelta(effectState);
                if (moveCostDelta <= 0)
                    continue;

                var sourceUnit =
                    effectState.source_unit_id != ""
                        ? GdInterop.GetObject(state.units, effectState.source_unit_id)
                            as BattleUnitState
                        : null;
                if (
                    !BattleTargetTeamRules.is_unit_valid_for_filter(
                        sourceUnit,
                        unitState,
                        effectState.target_team_filter
                    )
                )
                    continue;
                if (_IsBlockedByNonstackingStatus(unitState, effectState))
                    continue;

                maxDelta = Math.Max(maxDelta, moveCostDelta);
            }
        }
        return maxDelta;
    }

    public int get_move_cost_delta_for_unit_target(BattleUnitState unit_state, Vector2I target_coord)
    {
        return GetMoveCostDeltaForUnitTarget(unit_state, target_coord);
    }

    public bool UpsertTimedTerrainEffect(
        Vector2I effectCoord,
        BattleUnitState sourceUnit,
        SkillDef skillDef,
        CombatEffectDef effectDef,
        StringName fieldInstanceId
    )
    {
        var runtime = _ResolveRuntime();
        if (runtime == null)
            return false;

        BattleState state = runtime.get_state();
        BattleGridService gridService = runtime.get_grid_service();
        if (
            state == null
            || gridService == null
            || effectDef == null
            || effectDef.terrain_effect_id == ""
        )
            return false;

        BattleCellState cell = gridService.get_cell(state, effectCoord);
        if (cell == null)
            return false;

        var normalizedBehavior = _NormalizeStackBehavior(effectDef.stack_behavior);
        int existingIndex = -1;
        for (int i = 0; i < cell.timed_terrain_effects.Count; i++)
        {
            var existingEffect = cell.timed_terrain_effects[i];
            if (existingEffect != null && existingEffect.effect_id == effectDef.terrain_effect_id)
            {
                existingIndex = i;
                break;
            }
        }

        if (existingIndex >= 0)
        {
            if (normalizedBehavior == StackBehaviorIgnoreExisting)
                return false;
            if (normalizedBehavior == StackBehaviorRefresh)
            {
                var refreshedEffect = _BuildTimedTerrainEffect(
                    sourceUnit,
                    skillDef,
                    effectDef,
                    fieldInstanceId
                );
                if (refreshedEffect == null)
                    return false;
                cell.timed_terrain_effects[existingIndex] = refreshedEffect;
                return true;
            }
        }

        var newEffect = _BuildTimedTerrainEffect(sourceUnit, skillDef, effectDef, fieldInstanceId);
        if (newEffect == null)
            return false;
        cell.timed_terrain_effects.Add(newEffect);
        return true;
    }

    public bool upsert_timed_terrain_effect(
        Vector2I effect_coord,
        BattleUnitState source_unit,
        SkillDef skill_def,
        CombatEffectDef effect_def,
        StringName field_instance_id
    )
    {
        return UpsertTimedTerrainEffect(
            effect_coord,
            source_unit,
            skill_def,
            effect_def,
            field_instance_id
        );
    }

    public void ProcessTimedTerrainEffects(BattleEventBatch batch)
    {
        var runtime = _ResolveRuntime();
        if (runtime == null)
            return;

        BattleState state = runtime.get_state();
        BattleGridService gridService = runtime.get_grid_service();
        BattleTimelineState timeline = state?.timeline;
        if (state == null || timeline == null || gridService == null)
            return;

        var processedTickKeys = new GDictionary();
        foreach (var coordKey in state.cells.Keys)
        {
            var coord = coordKey.AsVector2I();
            var cell = state.cells[coordKey].As<BattleCellState>();
            if (cell == null || cell.timed_terrain_effects.Count == 0)
                continue;

            var retainedEffects = new Godot.Collections.Array<BattleTerrainEffectState>();
            bool cellChanged = false;
            foreach (var effectState in cell.timed_terrain_effects)
            {
                if (effectState == null)
                {
                    cellChanged = true;
                    continue;
                }
                if (_IsBattleLifetimeEffect(effectState))
                {
                    retainedEffects.Add(effectState);
                    continue;
                }

                int currentTu = timeline.current_tu;
                while (
                    effectState.remaining_tu > 0
                    && effectState.tick_interval_tu > 0
                    && currentTu >= effectState.next_tick_at_tu
                )
                {
                    ApplyTimedTerrainEffectTick(coord, effectState, processedTickKeys, batch);
                    effectState.remaining_tu = Math.Max(
                        effectState.remaining_tu - effectState.tick_interval_tu,
                        0
                    );
                    effectState.next_tick_at_tu += effectState.tick_interval_tu;
                    cellChanged = true;
                }

                if (effectState.remaining_tu > 0)
                    retainedEffects.Add(effectState);
                else
                    cellChanged = true;
            }

            if (cellChanged)
            {
                cell.timed_terrain_effects = retainedEffects;
                runtime.append_changed_coord(batch, coord);
            }
        }
    }

    public void process_timed_terrain_effects(BattleEventBatch batch) =>
        ProcessTimedTerrainEffects(batch);

    public void ApplyTimedTerrainEffectTick(
        Vector2I targetCoord,
        BattleTerrainEffectState effectState,
        GDictionary processedTickKeys,
        BattleEventBatch batch
    )
    {
        var runtime = _ResolveRuntime();
        if (runtime == null)
            return;

        if (
            effectState != null
            && (
                effectState.effect_type == TerrainEffectMovementCost
                || effectState.effect_type == TerrainEffectNone
            )
        )
            return;

        BattleState state = runtime.get_state();
        BattleGridService gridService = runtime.get_grid_service();
        BattleDamageResolver damageResolver = runtime.get_damage_resolver();
        if (
            state == null
            || effectState == null
            || processedTickKeys == null
            || gridService == null
            || damageResolver == null
        )
            return;

        BattleCellState cell = gridService.get_cell(state, targetCoord);
        if (cell == null || cell.occupant_unit_id == "")
            return;

        var targetUnit = GdInterop.GetObject(state.units, cell.occupant_unit_id) as BattleUnitState;
        if (targetUnit == null || !targetUnit.is_alive)
            return;

        var sourceUnit =
            effectState.source_unit_id != ""
                ? GdInterop.GetObject(state.units, effectState.source_unit_id) as BattleUnitState
                : null;
        if (
            !BattleTargetTeamRules.is_unit_valid_for_filter(
                sourceUnit,
                targetUnit,
                effectState.target_team_filter
            )
        )
            return;

        var tickKey =
            $"{effectState.field_instance_id}|{targetUnit.unit_id}|{effectState.next_tick_at_tu}";
        if (processedTickKeys.ContainsKey(tickKey))
            return;
        processedTickKeys[tickKey] = true;

        var tempEffect = new CombatEffectDef();
        tempEffect.effect_type = effectState.effect_type;
        tempEffect.power = effectState.power;
        tempEffect.damage_tag = effectState.damage_tag;
        tempEffect.status_id = ProgressionDataUtils.to_string_name(
            effectState.@params.GetValueOrDefault("status_id", default)
        );
        tempEffect.@params = (GDictionary)effectState.@params.Duplicate(true);

        var result = damageResolver.resolve_effects(sourceUnit, targetUnit, new GArray { tempEffect });
        if (!GdInterop.GetBool(result, "applied"))
            return;

        var statusEffectIds = GdInterop.GetArray(result, "status_effect_ids");
        runtime.mark_applied_statuses_for_turn_timing(targetUnit, statusEffectIds);
        runtime.append_result_source_status_effects(batch, sourceUnit, result);
        runtime.append_changed_unit_id(batch, targetUnit.unit_id);
        runtime.append_changed_unit_coords(batch, targetUnit);

        int damage = GdInterop.GetInt(result, "damage");
        int healing = GdInterop.GetInt(result, "healing");
        var damageSummary = runtime.summarize_damage_result(result);
        int killCount = 0;

        if (GdInterop.GetBool(damageSummary, "has_damage_event"))
        {
            if (damage > 0)
            {
                var damageLine =
                    $"{targetUnit.display_name} 受到 {_GetTimedTerrainEffectDisplayName(effectState)} 的 {damage} 点伤害";
                if (GdInterop.GetBool(damageSummary, "any_double"))
                    damageLine += "（触发易伤）";
                else if (GdInterop.GetBool(damageSummary, "any_half"))
                    damageLine += "（减半后结算）";
                runtime.append_batch_log(batch, $"{damageLine}。");
                if (GdInterop.GetInt(damageSummary, "shield_absorbed") > 0)
                {
                    runtime.append_batch_log(
                        batch,
                        $"{targetUnit.display_name} 的护盾吸收了 {GdInterop.GetInt(damageSummary, "shield_absorbed")} 点伤害。"
                    );
                }
            }
            else
            {
                if (GdInterop.GetBool(damageSummary, "any_immune"))
                {
                    runtime.append_batch_log(
                        batch,
                        $"{_GetTimedTerrainEffectDisplayName(effectState)} 命中，但 {targetUnit.display_name} 免疫该伤害。"
                    );
                }
                else if (GdInterop.GetInt(damageSummary, "shield_absorbed") > 0)
                {
                    runtime.append_batch_log(
                        batch,
                        $"{_GetTimedTerrainEffectDisplayName(effectState)} 命中，但被 {targetUnit.display_name} 的护盾吸收了 {GdInterop.GetInt(damageSummary, "shield_absorbed")} 点伤害。"
                    );
                }
                else
                {
                    runtime.append_batch_log(
                        batch,
                        $"{_GetTimedTerrainEffectDisplayName(effectState)} 命中，但 {targetUnit.display_name} 的伤害被{GdInterop.GetString(damageSummary, "absorb_reason_text")}完全吸收。"
                    );
                }
            }
            if (GdInterop.GetBool(damageSummary, "shield_broken"))
            {
                runtime.append_batch_log(
                    batch,
                    $"{targetUnit.display_name} 的护盾被击碎。"
                );
            }
        }
        if (healing > 0)
        {
            runtime.append_batch_log(
                batch,
                $"{targetUnit.display_name} 受到 {_GetTimedTerrainEffectDisplayName(effectState)} 影响，恢复 {healing} 点生命。"
            );
        }
        foreach (var statusId in statusEffectIds)
        {
            runtime.append_batch_log(
                batch,
                $"{targetUnit.display_name} 获得状态 {statusId}。"
            );
        }

        if (!targetUnit.is_alive)
        {
            killCount = 1;
            runtime.clear_defeated_unit(targetUnit, batch);
            runtime.append_batch_log(batch, $"{targetUnit.display_name} 被击倒。");
            runtime.record_enemy_defeated_achievement(sourceUnit, targetUnit);
        }

        if (sourceUnit != null)
        {
            runtime.record_battle_contribution_result(
                sourceUnit,
                targetUnit,
                damage,
                healing,
                killCount > 0,
                new StringName("terrain"),
                effectState.source_skill_id
            );
        }
    }

    private int _GetTimedTerrainMoveCostDelta(BattleTerrainEffectState effectState)
    {
        if (effectState == null)
            return 0;
        if (effectState.remaining_tu <= 0 && !_IsBattleLifetimeEffect(effectState))
            return 0;
        return Math.Max(
            effectState.@params.GetValueOrDefault(ParamMoveCostDelta, default).AsInt32(),
            0
        );
    }

    private bool _IsBlockedByNonstackingStatus(
        BattleUnitState unitState,
        BattleTerrainEffectState effectState
    )
    {
        if (unitState == null || effectState == null)
            return false;
        if (
            _UnitHasStatusFromParam(
                unitState,
                effectState.@params.GetValueOrDefault(ParamDoesNotStackWithStatusId, default)
            )
        )
            return true;
        return _UnitHasStatusFromParam(
            unitState,
            effectState.@params.GetValueOrDefault(ParamDoesNotStackWithStatusIds, default)
        );
    }

    private bool _UnitHasStatusFromParam(BattleUnitState unitState, object rawValue)
    {
        if (unitState == null)
            return false;
        if (rawValue is string || rawValue is StringName)
        {
            var statusId = ProgressionDataUtils.to_string_name(rawValue);
            return statusId != "" && unitState.has_status_effect(statusId);
        }
        if (rawValue is not Variant value)
        {
            return false;
        }
        if (
            value.VariantType == Variant.Type.String
            || value.VariantType == Variant.Type.StringName
        )
        {
            var statusId = ProgressionDataUtils.to_string_name(value);
            return statusId != "" && unitState.has_status_effect(statusId);
        }
        if (value.VariantType == Variant.Type.Array)
        {
            foreach (var statusValue in value.AsGodotArray())
            {
                var statusId = ProgressionDataUtils.to_string_name(statusValue);
                if (statusId != "" && unitState.has_status_effect(statusId))
                    return true;
            }
        }
        return false;
    }

    private BattleTerrainEffectState _BuildTimedTerrainEffect(
        BattleUnitState sourceUnit,
        SkillDef skillDef,
        CombatEffectDef effectDef,
        StringName fieldInstanceId
    )
    {
        var lifetimePolicy = _ResolveLifetimePolicy(effectDef);
        int tickIntervalTu = 0;
        int durationTu = 0;
        if (lifetimePolicy == LifetimePolicyBattle)
        {
            tickIntervalTu = 0;
            durationTu = 0;
        }
        else
        {
            tickIntervalTu = _NormalizePositiveTuValue(
                effectDef.tick_interval_tu,
                "terrain effect tick_interval_tu"
            );
            durationTu = _NormalizePositiveTuValue(
                effectDef.duration_tu,
                "terrain effect duration_tu"
            );
            if (tickIntervalTu <= 0 || durationTu <= 0)
                return null;
        }

        var effectState = new BattleTerrainEffectState();
        effectState.field_instance_id = fieldInstanceId;
        effectState.effect_id = effectDef.terrain_effect_id;
        effectState.effect_type =
            effectDef.tick_effect_type != ""
                ? effectDef.tick_effect_type
                : (
                    lifetimePolicy == LifetimePolicyBattle ? TerrainEffectNone : TerrainEffectDamage
                );
        effectState.source_unit_id =
            sourceUnit != null ? sourceUnit.unit_id : "";
        effectState.source_skill_id =
            skillDef?.skill_id ?? new StringName("");
        effectState.target_team_filter = BattleTargetTeamRules.resolve_effect_target_filter(
            skillDef,
            effectDef
        );
        effectState.power = effectDef.power;
        effectState.damage_tag = effectDef.damage_tag;
        effectState.tick_interval_tu = tickIntervalTu;
        effectState.remaining_tu =
            lifetimePolicy == LifetimePolicyBattle ? 0 : Math.Max(durationTu, tickIntervalTu);

        var runtime = _ResolveRuntime();
        if (lifetimePolicy == LifetimePolicyBattle)
        {
            effectState.next_tick_at_tu = 0;
        }
        else if (runtime != null)
        {
            BattleState state = runtime.get_state();
            BattleTimelineState timeline = state?.timeline;
            effectState.next_tick_at_tu =
                timeline != null
                    ? timeline.current_tu + tickIntervalTu
                    : tickIntervalTu;
        }
        else
        {
            effectState.next_tick_at_tu = tickIntervalTu;
        }

        effectState.stack_behavior = _NormalizeStackBehavior(effectDef.stack_behavior);
        effectState.@params = (GDictionary)effectDef.@params.Duplicate(true);
        effectState.@params[ParamLifetimePolicy] = lifetimePolicy.ToString();
        if (effectDef.status_id != "")
            effectState.@params["status_id"] = effectDef.status_id.ToString();
        return effectState;
    }

    public static bool IsTerrainEffectActive(BattleTerrainEffectState effectState)
    {
        if (effectState == null)
            return false;
        if (_IsBattleLifetimeEffectStatic(effectState))
            return true;
        return effectState.remaining_tu > 0;
    }

    public static bool is_terrain_effect_active(BattleTerrainEffectState effect_state) =>
        IsTerrainEffectActive(effect_state);

    private static bool _IsBattleLifetimeEffectStatic(BattleTerrainEffectState effectState)
    {
        if (effectState == null || effectState.@params == null)
            return false;
        var policyValue = effectState.@params.GetValueOrDefault(
            ParamLifetimePolicy,
            effectState.@params.GetValueOrDefault(ParamLifetimePolicy, default)
        );
        if (policyValue.VariantType == Variant.Type.StringName)
            return policyValue.AsStringName() == LifetimePolicyBattle;
        if (policyValue.VariantType == Variant.Type.String)
            return new StringName(policyValue.AsString()) == LifetimePolicyBattle;
        return false;
    }

    private bool _IsBattleLifetimeEffect(BattleTerrainEffectState effectState)
    {
        return _IsBattleLifetimeEffectStatic(effectState);
    }

    private StringName _ResolveLifetimePolicy(CombatEffectDef effectDef)
    {
        if (effectDef == null || effectDef.@params == null)
            return LifetimePolicyTimed;
        var value = effectDef.@params.GetValueOrDefault(
            ParamLifetimePolicy,
            effectDef.@params.GetValueOrDefault(ParamLifetimePolicy, LifetimePolicyTimed)
        );
        if (value.VariantType == Variant.Type.StringName)
            return value.AsStringName() == LifetimePolicyBattle
                ? LifetimePolicyBattle
                : LifetimePolicyTimed;
        if (value.VariantType == Variant.Type.String)
            return new StringName(value.AsString()) == LifetimePolicyBattle
                ? LifetimePolicyBattle
                : LifetimePolicyTimed;
        return LifetimePolicyTimed;
    }

    private StringName _NormalizeStackBehavior(StringName stackBehavior)
    {
        if (stackBehavior == StackBehaviorStack || stackBehavior == StackBehaviorIgnoreExisting)
            return stackBehavior;
        return StackBehaviorRefresh;
    }

    private int _NormalizePositiveTuValue(int value, string fieldLabel)
    {
        if (value <= 0)
        {
            GameLog.Error(
                $"{fieldLabel} must be positive and use {TuGranularity} TU steps, got {value}; skipping effect.",
                "battle.terrain.invalid_tu_positive",
                "battle"
            );
            return -1;
        }
        if (value % TuGranularity != 0)
        {
            GameLog.Error(
                $"{fieldLabel} must use {TuGranularity} TU steps, got {value}; skipping effect.",
                "battle.terrain.invalid_tu_granularity",
                "battle"
            );
            return -1;
        }
        return value;
    }

    private string _GetTimedTerrainEffectDisplayName(BattleTerrainEffectState effectState)
    {
        if (effectState != null && effectState.@params.ContainsKey("display_name"))
            return effectState.@params.GetValueOrDefault("display_name", default).AsString();
        return effectState != null ? effectState.effect_id.ToString() : "地格效果";
    }
}
