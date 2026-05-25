using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GArray = Godot.Collections.Array;

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
    private static readonly string ParamDoesNotStackWithStatusIds = "does_not_stack_with_status_ids";
    private const int TuGranularity = 5;

    private WeakReference<GodotObject> _runtimeRef = null;

    private GodotObject _ResolveRuntime()
    {
        if (_runtimeRef == null)
            return null;
        _runtimeRef.TryGetTarget(out var runtime);
        return runtime;
    }

    public void Setup(GodotObject runtime)
    {
        _runtimeRef = runtime != null ? new WeakReference<GodotObject>(runtime) : null;
    }

    public new void Dispose()
    {
        _runtimeRef = null;
        base.Dispose();
    }

    public int GetMoveCostDeltaForUnitTarget(GodotObject unitStateObj, Vector2I targetCoord)
    {
        var runtime = _ResolveRuntime();
        var unitState = unitStateObj as BattleUnitState;
        if (runtime == null || unitState == null)
            return 0;

        var state = runtime.Call("get_state").AsGodotObject();
        var gridService = runtime.Call("get_grid_service").AsGodotObject();
        if (state == null || gridService == null)
            return 0;

        int maxDelta = 0;
        var targetCoords = gridService.Call("get_unit_target_coords", unitStateObj, targetCoord).AsGodotArray();
        foreach (var coordVariant in targetCoords)
        {
            var coord = coordVariant.AsVector2I();
            var cell = gridService.Call("get_cell", state, coord).As<BattleCellState>();
            if (cell == null || cell.timed_terrain_effects.Count == 0)
                continue;

            foreach (var effectState in cell.timed_terrain_effects)
            {
                int moveCostDelta = _GetTimedTerrainMoveCostDelta(effectState);
                if (moveCostDelta <= 0)
                    continue;

                var sourceUnit = effectState.source_unit_id != ""
                    ? (state.Get("units").AsGodotDictionary()[effectState.source_unit_id].AsGodotObject() as BattleUnitState)
                    : null;
                if (!BattleTargetTeamRules.is_unit_valid_for_filter(sourceUnit, unitState, effectState.target_team_filter))
                    continue;
                if (_IsBlockedByNonstackingStatus(unitState, effectState))
                    continue;

                maxDelta = Math.Max(maxDelta, moveCostDelta);
            }
        }
        return maxDelta;
    }

    public bool UpsertTimedTerrainEffect(Vector2I effectCoord, GodotObject sourceUnit, GodotObject skillDef, GodotObject effectDefObj, StringName fieldInstanceId)
    {
        var runtime = _ResolveRuntime();
        if (runtime == null)
            return false;

        var state = runtime.Call("get_state").AsGodotObject();
        var gridService = runtime.Call("get_grid_service").AsGodotObject();
        var effectDef = effectDefObj as CombatEffectDef;
        if (state == null || gridService == null || effectDef == null || effectDef.terrain_effect_id == "")
            return false;

        var cell = gridService.Call("get_cell", state, effectCoord).As<BattleCellState>();
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
                var refreshedEffect = _BuildTimedTerrainEffect(sourceUnit, skillDef, effectDef, fieldInstanceId);
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

    public void ProcessTimedTerrainEffects(GodotObject batch)
    {
        var runtime = _ResolveRuntime();
        if (runtime == null)
            return;

        var state = runtime.Call("get_state").AsGodotObject();
        var gridService = runtime.Call("get_grid_service").AsGodotObject();
        var timeline = state.Get("timeline").AsGodotObject();
        if (state == null || timeline == null || gridService == null)
            return;

        var processedTickKeys = new GDictionary();
        var cells = state.Get("cells").AsGodotDictionary();
        foreach (var coordKey in cells.Keys)
        {
            var coord = coordKey.AsVector2I();
            var cell = cells[coordKey].As<BattleCellState>();
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

                int currentTu = timeline.Get("current_tu").AsInt32();
                while (effectState.remaining_tu > 0 && effectState.tick_interval_tu > 0 && currentTu >= effectState.next_tick_at_tu)
                {
                    ApplyTimedTerrainEffectTick(coord, effectState, processedTickKeys, batch);
                    effectState.remaining_tu = Math.Max(effectState.remaining_tu - effectState.tick_interval_tu, 0);
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
                runtime.Call("append_changed_coord", batch, coord);
            }
        }
    }

    public void ApplyTimedTerrainEffectTick(Vector2I targetCoord, GodotObject effectStateObj, GDictionary processedTickKeys, GodotObject batch)
    {
        var runtime = _ResolveRuntime();
        if (runtime == null)
            return;

        var effectState = effectStateObj as BattleTerrainEffectState;
        if (effectState != null && (effectState.effect_type == TerrainEffectMovementCost || effectState.effect_type == TerrainEffectNone))
            return;

        var state = runtime.Call("get_state").AsGodotObject();
        var gridService = runtime.Call("get_grid_service").AsGodotObject();
        var damageResolver = runtime.Call("get_damage_resolver").AsGodotObject();
        if (state == null || effectState == null || processedTickKeys == null || gridService == null || damageResolver == null)
            return;

        var cell = gridService.Call("get_cell", state, targetCoord).As<BattleCellState>();
        if (cell == null || cell.occupant_unit_id == "")
            return;

        var targetUnit = (state.Get("units").AsGodotDictionary()[cell.occupant_unit_id].AsGodotObject() as BattleUnitState);
        if (targetUnit == null || !targetUnit.is_alive)
            return;

        var sourceUnit = effectState.source_unit_id != ""
            ? (state.Get("units").AsGodotDictionary().GetValueOrDefault(effectState.source_unit_id, default).AsGodotObject() as BattleUnitState)
            : null;
        if (!BattleTargetTeamRules.is_unit_valid_for_filter(sourceUnit, targetUnit, effectState.target_team_filter))
            return;

        var tickKey = $"{effectState.field_instance_id}|{targetUnit.unit_id}|{effectState.next_tick_at_tu}";
        if (processedTickKeys.ContainsKey(tickKey))
            return;
        processedTickKeys[tickKey] = true;

        var tempEffect = new CombatEffectDef();
        tempEffect.effect_type = effectState.effect_type;
        tempEffect.power = effectState.power;
        tempEffect.damage_tag = effectState.damage_tag;
        tempEffect.status_id = ProgressionDataUtils.to_string_name(effectState.@params.GetValueOrDefault("status_id", default));
        tempEffect.@params = (GDictionary)effectState.@params.Duplicate(true);

        var result = damageResolver.Call("resolve_effects", sourceUnit, targetUnit, new GArray { tempEffect }).AsGodotDictionary();
        if (!result.GetValueOrDefault("applied", default).AsBool())
            return;

        var statusEffectIds = result.GetValueOrDefault("status_effect_ids", default).AsGodotArray();
        runtime.Call("mark_applied_statuses_for_turn_timing", targetUnit, statusEffectIds);
        runtime.Call("append_result_source_status_effects", batch, sourceUnit, result);
        runtime.Call("append_changed_unit_id", batch, targetUnit.unit_id);
        runtime.Call("append_changed_unit_coords", batch, targetUnit);

        int damage = result.GetValueOrDefault("damage", default).AsInt32();
        int healing = result.GetValueOrDefault("healing", default).AsInt32();
        var damageSummary = runtime.Call("summarize_damage_result", result).AsGodotDictionary();
        int killCount = 0;

        if (damageSummary.GetValueOrDefault("has_damage_event", default).AsBool())
        {
            if (damage > 0)
            {
                var damageLine = $"{targetUnit.display_name} 受到 {_GetTimedTerrainEffectDisplayName(effectState)} 的 {damage} 点伤害";
                if (damageSummary.GetValueOrDefault("any_double", default).AsBool())
                    damageLine += "（触发易伤）";
                else if (damageSummary.GetValueOrDefault("any_half", default).AsBool())
                    damageLine += "（减半后结算）";
                runtime.Call("append_batch_log", batch, $"{damageLine}。");
                if (damageSummary.GetValueOrDefault("shield_absorbed", default).AsInt32() > 0)
                {
                    runtime.Call("append_batch_log", batch, $"{targetUnit.display_name} 的护盾吸收了 {damageSummary.GetValueOrDefault("shield_absorbed", default).AsInt32()} 点伤害。");
                }
            }
            else
            {
                if (damageSummary.GetValueOrDefault("any_immune", default).AsBool())
                {
                    runtime.Call("append_batch_log", batch, $"{_GetTimedTerrainEffectDisplayName(effectState)} 命中，但 {targetUnit.display_name} 免疫该伤害。");
                }
                else if (damageSummary.GetValueOrDefault("shield_absorbed", default).AsInt32() > 0)
                {
                    runtime.Call("append_batch_log", batch, $"{_GetTimedTerrainEffectDisplayName(effectState)} 命中，但被 {targetUnit.display_name} 的护盾吸收了 {damageSummary.GetValueOrDefault("shield_absorbed", default).AsInt32()} 点伤害。");
                }
                else
                {
                    runtime.Call("append_batch_log", batch, $"{_GetTimedTerrainEffectDisplayName(effectState)} 命中，但 {targetUnit.display_name} 的伤害被{damageSummary.GetValueOrDefault("absorb_reason_text", default)}完全吸收。");
                }
            }
            if (damageSummary.GetValueOrDefault("shield_broken", default).AsBool())
            {
                runtime.Call("append_batch_log", batch, $"{targetUnit.display_name} 的护盾被击碎。");
            }
        }
        if (healing > 0)
        {
            runtime.Call("append_batch_log", batch, $"{targetUnit.display_name} 受到 {_GetTimedTerrainEffectDisplayName(effectState)} 影响，恢复 {healing} 点生命。");
        }
        foreach (var statusId in statusEffectIds)
        {
            runtime.Call("append_batch_log", batch, $"{targetUnit.display_name} 获得状态 {statusId}。");
        }

        if (!targetUnit.is_alive)
        {
            killCount = 1;
            runtime.Call("clear_defeated_unit", targetUnit, batch);
            runtime.Call("append_batch_log", batch, $"{targetUnit.display_name} 被击倒。");
            runtime.Call("record_enemy_defeated_achievement", sourceUnit, targetUnit);
        }

        if (sourceUnit != null)
        {
            runtime.Call("record_battle_contribution_result", sourceUnit, targetUnit, damage, healing, killCount > 0, new StringName("terrain"), effectState.source_skill_id);
        }
    }

    private int _GetTimedTerrainMoveCostDelta(BattleTerrainEffectState effectState)
    {
        if (effectState == null)
            return 0;
        if (effectState.remaining_tu <= 0 && !_IsBattleLifetimeEffect(effectState))
            return 0;
        return Math.Max(effectState.@params.GetValueOrDefault(ParamMoveCostDelta, default).AsInt32(), 0);
    }

    private bool _IsBlockedByNonstackingStatus(BattleUnitState unitState, BattleTerrainEffectState effectState)
    {
        if (unitState == null || effectState == null)
            return false;
        if (_UnitHasStatusFromParam(unitState, effectState.@params.GetValueOrDefault(ParamDoesNotStackWithStatusId, default)))
            return true;
        return _UnitHasStatusFromParam(unitState, effectState.@params.GetValueOrDefault(ParamDoesNotStackWithStatusIds, default));
    }

    private bool _UnitHasStatusFromParam(BattleUnitState unitState, Variant value)
    {
        if (unitState == null)
            return false;
        if (value.VariantType == Variant.Type.String || value.VariantType == Variant.Type.StringName)
        {
            var statusId = ProgressionDataUtils.to_string_name(value);
            return statusId != "" && unitState.HasMethod("has_status_effect") && unitState.Call("has_status_effect", statusId).AsBool();
        }
        if (value.VariantType == Variant.Type.Array)
        {
            foreach (var statusVariant in value.AsGodotArray())
            {
                var statusId = ProgressionDataUtils.to_string_name(statusVariant);
                if (statusId != "" && unitState.HasMethod("has_status_effect") && unitState.Call("has_status_effect", statusId).AsBool())
                    return true;
            }
        }
        return false;
    }

    private BattleTerrainEffectState _BuildTimedTerrainEffect(GodotObject sourceUnit, GodotObject skillDef, CombatEffectDef effectDef, StringName fieldInstanceId)
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
            tickIntervalTu = _NormalizePositiveTuValue(effectDef.tick_interval_tu, "terrain effect tick_interval_tu");
            durationTu = _NormalizePositiveTuValue(effectDef.duration_tu, "terrain effect duration_tu");
            if (tickIntervalTu <= 0 || durationTu <= 0)
                return null;
        }

        var effectState = new BattleTerrainEffectState();
        effectState.field_instance_id = fieldInstanceId;
        effectState.effect_id = effectDef.terrain_effect_id;
        effectState.effect_type = effectDef.tick_effect_type != ""
            ? effectDef.tick_effect_type
            : (lifetimePolicy == LifetimePolicyBattle ? TerrainEffectNone : TerrainEffectDamage);
        effectState.source_unit_id = sourceUnit != null ? sourceUnit.Get("unit_id").AsStringName() : "";
        effectState.source_skill_id = skillDef != null ? skillDef.Get("skill_id").AsStringName() : "";
        effectState.target_team_filter = BattleTargetTeamRules.resolve_effect_target_filter(skillDef, effectDef);
        effectState.power = effectDef.power;
        effectState.damage_tag = effectDef.damage_tag;
        effectState.tick_interval_tu = tickIntervalTu;
        effectState.remaining_tu = lifetimePolicy == LifetimePolicyBattle ? 0 : Math.Max(durationTu, tickIntervalTu);

        var runtime = _ResolveRuntime();
        if (lifetimePolicy == LifetimePolicyBattle)
        {
            effectState.next_tick_at_tu = 0;
        }
        else if (runtime != null)
        {
            var state = runtime.Call("get_state").AsGodotObject();
            var timeline = state?.Get("timeline").AsGodotObject();
            effectState.next_tick_at_tu = timeline != null ? timeline.Get("current_tu").AsInt32() + tickIntervalTu : tickIntervalTu;
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

    public static bool IsTerrainEffectActive(GodotObject effectStateObj)
    {
        if (effectStateObj == null)
            return false;
        var effectState = effectStateObj as BattleTerrainEffectState;
        if (effectState == null)
            return false;
        if (_IsBattleLifetimeEffectStatic(effectState))
            return true;
        return effectState.remaining_tu > 0;
    }

    private static bool _IsBattleLifetimeEffectStatic(BattleTerrainEffectState effectState)
    {
        if (effectState == null || effectState.@params == null)
            return false;
        var policyValue = effectState.@params.GetValueOrDefault(ParamLifetimePolicy, effectState.@params.GetValueOrDefault(ParamLifetimePolicy, default));
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
        var value = effectDef.@params.GetValueOrDefault(ParamLifetimePolicy, effectDef.@params.GetValueOrDefault(ParamLifetimePolicy, LifetimePolicyTimed));
        if (value.VariantType == Variant.Type.StringName)
            return value.AsStringName() == LifetimePolicyBattle ? LifetimePolicyBattle : LifetimePolicyTimed;
        if (value.VariantType == Variant.Type.String)
            return new StringName(value.AsString()) == LifetimePolicyBattle ? LifetimePolicyBattle : LifetimePolicyTimed;
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
            GD.PushError($"{fieldLabel} must be positive and use {TuGranularity} TU steps, got {value}; skipping effect.");
            return -1;
        }
        if (value % TuGranularity != 0)
        {
            GD.PushError($"{fieldLabel} must use {TuGranularity} TU steps, got {value}; skipping effect.");
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
