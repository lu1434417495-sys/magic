using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class BattleBarrierOutcomeResolver : RefCounted
{
    private const int DEFAULT_FATAL_DAMAGE = 99999;
    private const int TELEPORT_RANDOM_ATTEMPTS = 64;

    private WeakReference<GodotObject> _runtimeRef;

    public void Setup(GodotObject runtime)
    {
        _runtimeRef = runtime != null ? new WeakReference<GodotObject>(runtime) : null;
    }

    public new void Dispose()
    {
        _runtimeRef = null;
    }

    public Dictionary ApplyPassageOutcomes(
        BattleUnitState unitState,
        Dictionary barrier,
        Dictionary layer,
        BattleEventBatch batch
    )
    {
        var result = new Dictionary
        {
            ["applied"] = false,
            ["stopped"] = false,
        };
        if (unitState == null || barrier == null || barrier.Count == 0 || layer == null || layer.Count == 0)
            return result;
        var outcomes = DictionaryGet(layer, "passage_outcomes", new Godot.Collections.Array()).AsGodotArray();
        if (outcomes.Count == 0 && layer.ContainsKey("passage"))
        {
            outcomes = new Godot.Collections.Array { DictionaryGet(layer, "passage", new Dictionary()) };
        }
        foreach (Variant outcomeVariant in outcomes)
        {
            var outcome = outcomeVariant.VariantType == Variant.Type.Dictionary ? outcomeVariant.AsGodotDictionary() : new Dictionary();
            if (outcome.Count == 0)
                continue;
            var outcomeResult = _ApplyOutcome(unitState, barrier, layer, outcome, batch);
            result["applied"] = true;
            if (DictionaryGet(outcomeResult, "stopped", false).AsBool() || !unitState.is_alive)
            {
                result["stopped"] = true;
                return result;
            }
        }
        return result;
    }

    private Dictionary _ApplyOutcome(
        BattleUnitState unitState,
        Dictionary barrier,
        Dictionary layer,
        Dictionary outcome,
        BattleEventBatch batch
    )
    {
        var outcomeType = ProgressionDataUtils.to_string_name(DictionaryGet(outcome, "outcome_type", DictionaryGet(outcome, "outcome", "")));
        switch (outcomeType)
        {
            case "damage":
                return _ApplyDamageOutcome(unitState, barrier, layer, outcome, batch);
            case "poison_death":
                return _ApplyPoisonDeathOutcome(unitState, barrier, layer, outcome, batch);
            case "status":
                var statusId = ProgressionDataUtils.to_string_name(DictionaryGet(outcome, "status_id", ""));
                return _ApplyStatusOutcome(unitState, barrier, layer, outcome, statusId, batch);
            case "banish":
                return _ApplyBanishOutcome(unitState, barrier, layer, outcome, batch);
            default:
                return new Dictionary { ["stopped"] = false };
        }
    }

    private Dictionary _ApplyDamageOutcome(
        BattleUnitState unitState,
        Dictionary barrier,
        Dictionary layer,
        Dictionary outcome,
        BattleEventBatch batch
    )
    {
        var amount = Mathf.Max((int)DictionaryGet(outcome, "amount", 0), 0);
        if (amount <= 0)
            return new Dictionary { ["stopped"] = false };
        var saveResult = _ResolveOutcomeSave(unitState, barrier, layer, outcome);
        var finalAmount = amount;
        if (DictionaryGet(saveResult, "success", false).AsBool() && DictionaryGet(outcome, "half_on_success", false).AsBool())
            finalAmount = Mathf.Max((int)Mathf.Ceil(amount / 2.0f), 1);
        var damageTag = ProgressionDataUtils.to_string_name(DictionaryGet(outcome, "damage_tag", "force"));
        var damageResult = _ApplyDirectDamage(unitState, barrier, finalAmount, damageTag);
        _AppendChangedUnit(batch, unitState);
        _AppendLog(batch, $"{unitState.display_name} 触碰 {_GetLayerLabel(layer)}，受到 {DictionaryGet(damageResult, "damage", finalAmount)} 点伤害。");
        if (!unitState.is_alive)
        {
            _HandleDefeatedByBarrier(unitState, barrier, batch);
            return new Dictionary { ["stopped"] = true };
        }
        return new Dictionary { ["stopped"] = false };
    }

    private Dictionary _ApplyPoisonDeathOutcome(
        BattleUnitState unitState,
        Dictionary barrier,
        Dictionary layer,
        Dictionary outcome,
        BattleEventBatch batch
    )
    {
        var saveResult = _ResolveOutcomeSave(unitState, barrier, layer, outcome);
        if (DictionaryGet(saveResult, "success", false).AsBool())
        {
            var successAmount = Mathf.Max((int)DictionaryGet(outcome, "success_amount", 0), 0);
            if (successAmount <= 0)
                return new Dictionary { ["stopped"] = false };
            var damageTag = ProgressionDataUtils.to_string_name(DictionaryGet(outcome, "success_damage_tag", DictionaryGet(outcome, "damage_tag", "poison")));
            var damageResult = _ApplyDirectDamage(unitState, barrier, successAmount, damageTag);
            _AppendChangedUnit(batch, unitState);
            _AppendLog(batch, $"{unitState.display_name} 通过 {_GetLayerLabel(layer)} 的豁免，仍受到 {DictionaryGet(damageResult, "damage", successAmount)} 点伤害。");
            if (!unitState.is_alive)
            {
                _HandleDefeatedByBarrier(unitState, barrier, batch);
                return new Dictionary { ["stopped"] = true };
            }
            return new Dictionary { ["stopped"] = false };
        }
        var fatalDamage = Mathf.Max(
            unitState.current_hp + unitState.current_shield_hp + (int)DictionaryGet(outcome, "fatal_damage", DEFAULT_FATAL_DAMAGE),
            (int)DictionaryGet(outcome, "fatal_damage", DEFAULT_FATAL_DAMAGE)
        );
        var deathResult = _ApplyDirectDamage(unitState, barrier, fatalDamage, "poison");
        _AppendChangedUnit(batch, unitState);
        _AppendLog(batch, $"{unitState.display_name} 未通过 {_GetLayerLabel(layer)} 的豁免，触发即死效果。");
        if (!unitState.is_alive)
        {
            _HandleDefeatedByBarrier(unitState, barrier, batch);
            return new Dictionary { ["stopped"] = true };
        }
        if ((int)DictionaryGet(deathResult, "damage", 0) > 0)
            _AppendLog(batch, $"{unitState.display_name} 的免死效果抵消了即死。");
        return new Dictionary { ["stopped"] = false };
    }

    private Dictionary _ApplyStatusOutcome(
        BattleUnitState unitState,
        Dictionary barrier,
        Dictionary layer,
        Dictionary outcome,
        StringName statusId,
        BattleEventBatch batch
    )
    {
        if (statusId == "")
            return new Dictionary { ["stopped"] = false };
        var saveResult = _ResolveOutcomeSave(unitState, barrier, layer, outcome);
        if (DictionaryGet(saveResult, "success", false).AsBool())
        {
            _AppendLog(batch, $"{unitState.display_name} 通过 {_GetLayerLabel(layer)} 的豁免。");
            return new Dictionary { ["stopped"] = false };
        }
        _ApplyBarrierStatus(unitState, barrier, layer, outcome, statusId);
        _AppendChangedUnit(batch, unitState);
        _AppendLog(batch, $"{unitState.display_name} 未通过 {_GetLayerLabel(layer)} 的豁免，获得状态 {statusId}。");
        return new Dictionary { ["stopped"] = true };
    }

    private Dictionary _ApplyBanishOutcome(
        BattleUnitState unitState,
        Dictionary barrier,
        Dictionary layer,
        Dictionary outcome,
        BattleEventBatch batch
    )
    {
        var saveResult = _ResolveOutcomeSave(unitState, barrier, layer, outcome);
        if (DictionaryGet(saveResult, "success", false).AsBool())
        {
            _AppendLog(batch, $"{unitState.display_name} 通过 {_GetLayerLabel(layer)} 的豁免。");
            return new Dictionary { ["stopped"] = false };
        }
        if (_IsSummonedUnit(unitState))
        {
            _RemoveSummonedUnit(unitState, barrier, layer, batch);
            return new Dictionary { ["stopped"] = true };
        }
        var destination = _FindBanishTeleportCoord(unitState, barrier);
        if (destination == new Vector2I(-1, -1))
        {
            _AppendLog(batch, $"{unitState.display_name} 被 {_GetLayerLabel(layer)} 放逐，但没有找到可传送落点。");
            return new Dictionary { ["stopped"] = true };
        }
        var previousCoords = new Godot.Collections.Array();
        foreach (Vector2I coord in unitState.occupied_coords)
            previousCoords.Add(coord);
        var gridService = _ResolveRuntime().Get("_grid_service").AsGodotObject();
        var state = _ResolveRuntime().Get("_state").As<BattleState>();
        gridService.Call("clear_unit_occupancy", state, unitState);
        unitState.set_anchor_coord(destination);
        gridService.Call("set_occupants", state, unitState.occupied_coords, unitState.unit_id);
        _AppendChangedCoords(batch, previousCoords);
        _AppendChangedUnit(batch, unitState);
        _AppendLog(batch, $"{unitState.display_name} 被 {_GetLayerLabel(layer)} 随机传送到 ({destination.X}, {destination.Y})。");
        return new Dictionary { ["stopped"] = true };
    }

    private Dictionary _ResolveOutcomeSave(
        BattleUnitState unitState,
        Dictionary barrier,
        Dictionary layer,
        Dictionary outcome
    )
    {
        var effect = new CombatEffectDef();
        effect.effect_type = "status";
        effect.save_dc = Mathf.Max((int)DictionaryGet(outcome, "save_dc", DictionaryGet(barrier, "save_dc", 0)), 1);
        effect.save_dc_mode = BattleSaveResolver.SAVE_DC_MODE_STATIC();
        effect.save_ability = ProgressionDataUtils.to_string_name(DictionaryGet(outcome, "save_ability", "willpower"));
        effect.save_tag = ProgressionDataUtils.to_string_name(DictionaryGet(outcome, "save_tag", "magic"));
        var context = new Dictionary();
        if (layer.ContainsKey("save_roll_override"))
            context["save_roll_override"] = (int)DictionaryGet(layer, "save_roll_override", 0);
        return BattleSaveResolver.resolve_save_with_context(_GetBarrierSourceUnit(barrier), unitState, effect, context);
    }

    private void _ApplyBarrierStatus(
        BattleUnitState unitState,
        Dictionary barrier,
        Dictionary layer,
        Dictionary outcome,
        StringName statusId
    )
    {
        var statusEntry = new BattleStatusEffectState();
        statusEntry.status_id = statusId;
        statusEntry.source_unit_id = ProgressionDataUtils.to_string_name(DictionaryGet(barrier, "source_unit_id", ""));
        statusEntry.power = 1;
        statusEntry.stacks = 1;
        statusEntry.duration = -1;
        statusEntry.@params = new Dictionary
        {
            ["source"] = DictionaryGet(barrier, "profile_id", "").AsString(),
            ["layer_id"] = DictionaryGet(layer, "layer_id", "").AsString(),
            ["counts_as_debuff"] = true,
            ["self_save_dc"] = Mathf.Max((int)DictionaryGet(outcome, "save_dc", DictionaryGet(barrier, "save_dc", 0)), 1),
            ["self_save_ability"] = DictionaryGet(outcome, "save_ability", "willpower").AsString(),
            ["self_save_tag"] = DictionaryGet(outcome, "save_tag", "magic").AsString(),
        };
        unitState.set_status_effect(statusEntry);
    }

    private Dictionary _ApplyDirectDamage(
        BattleUnitState unitState,
        Dictionary barrier,
        int damageAmount,
        StringName damageTag
    )
    {
        var damageOutcome = new Dictionary
        {
            ["resolved_damage"] = Mathf.Max(damageAmount, 0),
            ["base_damage"] = Mathf.Max(damageAmount, 0),
            ["damage_tag"] = damageTag.ToString(),
            ["damage_kind"] = DictionaryGet(barrier, "profile_id", "barrier").AsString(),
        };
        var sourceUnit = _GetBarrierSourceUnit(barrier);
        var damageResolver = _ResolveRuntime().Get("_damage_resolver").AsGodotObject();
        var damageResult = damageResolver.Call("apply_direct_damage_to_target", unitState, damageOutcome, sourceUnit).AsGodotDictionary();
        unitState.is_alive = unitState.current_hp > 0;
        return damageResult;
    }

    private void _HandleDefeatedByBarrier(BattleUnitState unitState, Dictionary barrier, BattleEventBatch batch)
    {
        var sourceUnit = _GetBarrierSourceUnit(barrier);
        _ResolveRuntime().Call("handle_unit_defeated_by_runtime_effect",
            unitState,
            sourceUnit,
            batch,
            $"{unitState.display_name} 被 {_GetBarrierLabel(barrier)} 击倒。"
        );
    }

    private void _RemoveSummonedUnit(BattleUnitState unitState, Dictionary barrier, Dictionary layer, BattleEventBatch batch)
    {
        _ResolveRuntime().Call("remove_summoned_unit_from_battle",
            unitState,
            batch,
            $"{unitState.display_name} 是召唤物，被 {_GetLayerLabel(layer)} 直接放逐消失。"
        );
    }

    private Vector2I _FindBanishTeleportCoord(BattleUnitState unitState, Dictionary barrier)
    {
        var runtime = _ResolveRuntime();
        if (runtime == null)
            return new Vector2I(-1, -1);
        var state = runtime.Get("_state").As<BattleState>();
        var gridService = runtime.Get("_grid_service").AsGodotObject();
        if (state == null || gridService == null || unitState == null)
            return new Vector2I(-1, -1);
        var candidates = new List<Vector2I>();
        foreach (Variant coordVariant in state.cells.Keys)
        {
            if (coordVariant.VariantType != Variant.Type.Vector2I)
                continue;
            var coord = coordVariant.AsVector2I();
            if (_IsCoordInsideBarrier(coord, barrier))
                continue;
            if (!gridService.Call("can_place_footprint", state, coord, unitState.footprint_size, unitState.unit_id, unitState).AsBool())
                continue;
            candidates.Add(coord);
        }
        if (candidates.Count == 0)
            return new Vector2I(-1, -1);
        for (int attempt = 0; attempt < TELEPORT_RANDOM_ATTEMPTS; attempt++)
        {
            var index = TrueRandomSeedService.randi_range(0, candidates.Count - 1);
            return candidates[index];
        }
        candidates.Sort((left, right) =>
        {
            var leftDistance = (int)gridService.Call("get_distance", unitState.coord, left);
            var rightDistance = (int)gridService.Call("get_distance", unitState.coord, right);
            if (leftDistance != rightDistance)
                return leftDistance.CompareTo(rightDistance);
            if (left.Y != right.Y)
                return left.Y.CompareTo(right.Y);
            return left.X.CompareTo(right.X);
        });
        return candidates[0];
    }

    private bool _IsCoordInsideBarrier(Vector2I coord, Dictionary barrier)
    {
        var anchor = DictionaryGet(barrier, "anchor_coord", new Vector2I(-999999, -999999)).AsVector2I();
        var radius = Mathf.Max((int)DictionaryGet(barrier, "radius_cells", 0), 0);
        var pattern = ProgressionDataUtils.to_string_name(DictionaryGet(barrier, "area_pattern", "diamond"));
        var dx = Mathf.Abs(coord.X - anchor.X);
        var dy = Mathf.Abs(coord.Y - anchor.Y);
        switch (pattern)
        {
            case "square":
            case "radius":
                return Mathf.Max(dx, dy) <= radius;
            default:
                return dx + dy <= radius;
        }
    }

    private bool _IsSummonedUnit(BattleUnitState unitState)
    {
        if (unitState == null)
            return false;
        if (unitState.has_status_effect("summoned"))
            return true;
        if (DictionaryGet(unitState.ai_blackboard, "summoned", false).AsBool())
            return true;
        if (DictionaryGet(unitState.ai_blackboard, "temporary_unit", false).AsBool())
            return true;
        return !string.IsNullOrEmpty(DictionaryGet(unitState.ai_blackboard, "summon_source_unit_id", "").AsString());
    }

    private BattleUnitState _GetBarrierSourceUnit(Dictionary barrier)
    {
        var runtime = _ResolveRuntime();
        if (runtime == null)
            return null;
        var state = runtime.Get("_state").As<BattleState>();
        if (state == null)
            return null;
        var sourceUnitId = ProgressionDataUtils.to_string_name(DictionaryGet(barrier, "source_unit_id", ""));
        if (sourceUnitId == "")
            return null;
        if (!state.units.ContainsKey(sourceUnitId))
            return null;
        return state.units[sourceUnitId].As<BattleUnitState>();
    }

    private string _GetLayerLabel(Dictionary layer)
    {
        return DictionaryGet(layer, "display_name", DictionaryGet(layer, "layer_id", "屏障层")).AsString();
    }

    private string _GetBarrierLabel(Dictionary barrier)
    {
        return DictionaryGet(barrier, "display_name", DictionaryGet(barrier, "profile_id", "屏障")).AsString();
    }

    private void _AppendChangedUnit(BattleEventBatch batch, BattleUnitState unitState)
    {
        var runtime = _ResolveRuntime();
        if (runtime == null || batch == null || unitState == null)
            return;
        runtime.Call("_append_changed_unit_id", batch, unitState.unit_id);
        runtime.Call("_append_changed_unit_coords", batch, unitState);
    }

    private void _AppendChangedCoords(BattleEventBatch batch, Godot.Collections.Array coords)
    {
        var runtime = _ResolveRuntime();
        if (runtime == null || batch == null)
            return;
        runtime.Call("_append_changed_coords", batch, coords);
    }

    private void _AppendLog(BattleEventBatch batch, string line)
    {
        if (batch == null || string.IsNullOrEmpty(line))
            return;
        batch.log_lines.Add(line);
    }

    private GodotObject _ResolveRuntime()
    {
        if (_runtimeRef == null || !_runtimeRef.TryGetTarget(out GodotObject target) || !GodotObject.IsInstanceValid(target))
            return null;
        return target;
    }

    private static Variant DictionaryGet(Dictionary dictionary, Variant key, Variant fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        return dictionary[key];
    }
}
