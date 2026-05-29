using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class BattleBarrierOutcomeResolver : RefCounted
{
    private const int DEFAULT_FATAL_DAMAGE = 99999;
    private const int TELEPORT_RANDOM_ATTEMPTS = 64;

    private WeakReference<BattleRuntimeModule> _runtimeRef;
    private bool _disposed;

    public void Setup(BattleRuntimeModule runtime)
    {
        _runtimeRef = runtime != null ? new WeakReference<BattleRuntimeModule>(runtime) : null;
    }

    public new void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _runtimeRef = null;
        base.Dispose();
    }

    public Dictionary ApplyPassageOutcomes(
        BattleUnitState unitState,
        BattleBarrierInstanceState barrier,
        BattleBarrierLayerState layer,
        BattleEventBatch batch
    )
    {
        var result = new Dictionary { ["applied"] = false, ["stopped"] = false };
        if (
            unitState == null
            || barrier == null
            || barrier.IsEmpty
            || layer == null
            || IsLayerEmpty(layer)
        )
            return result;

        foreach (BattleBarrierOutcomeState outcome in layer.GetPassageOutcomesTyped())
        {
            if (outcome == null || outcome.IsEmpty)
                continue;
            var outcomeResult = _ApplyOutcome(unitState, barrier, layer, outcome, batch);
            result["applied"] = true;
            if (outcomeResult.GetValueOrDefault("stopped", false).AsBool() || !unitState.is_alive)
            {
                result["stopped"] = true;
                return result;
            }
        }
        return result;
    }

    public Dictionary ApplyPassageOutcomes(
        BattleUnitState unitState,
        Dictionary barrier,
        Dictionary layer,
        BattleEventBatch batch
    )
    {
        return ApplyPassageOutcomes(
            unitState,
            BattleBarrierInstanceState.from_runtime_dict(barrier),
            BattleBarrierLayerState.from_runtime_dict(layer),
            batch
        );
    }

    private Dictionary _ApplyOutcome(
        BattleUnitState unitState,
        BattleBarrierInstanceState barrier,
        BattleBarrierLayerState layer,
        BattleBarrierOutcomeState outcome,
        BattleEventBatch batch
    )
    {
        switch (outcome?.outcome_type ?? new StringName(""))
        {
            case "damage":
                return _ApplyDamageOutcome(unitState, barrier, layer, outcome, batch);
            case "poison_death":
                return _ApplyPoisonDeathOutcome(unitState, barrier, layer, outcome, batch);
            case "status":
                return _ApplyStatusOutcome(unitState, barrier, layer, outcome, batch);
            case "banish":
                return _ApplyBanishOutcome(unitState, barrier, layer, outcome, batch);
            default:
                return new Dictionary { ["stopped"] = false };
        }
    }

    private Dictionary _ApplyDamageOutcome(
        BattleUnitState unitState,
        BattleBarrierInstanceState barrier,
        BattleBarrierLayerState layer,
        BattleBarrierOutcomeState outcome,
        BattleEventBatch batch
    )
    {
        var amount = Mathf.Max(outcome?.amount ?? 0, 0);
        if (amount <= 0)
            return new Dictionary { ["stopped"] = false };
        var saveResult = _ResolveOutcomeSave(unitState, barrier, layer, outcome);
        var finalAmount = amount;
        if (
            saveResult.GetValueOrDefault("success", false).AsBool()
            && outcome.half_on_success
        )
            finalAmount = Mathf.Max((int)Mathf.Ceil(amount / 2.0f), 1);
        var damageTag = ResolveDamageTag(outcome.damage_tag, "force");
        var damageResult = _ApplyDirectDamage(unitState, barrier, finalAmount, damageTag);
        _AppendChangedUnit(batch, unitState);
        _AppendLog(
            batch,
            $"{unitState.display_name} 触碰 {_GetLayerLabel(layer)}，受到 {damageResult.GetValueOrDefault("damage", finalAmount)} 点伤害。"
        );
        if (!unitState.is_alive)
        {
            _HandleDefeatedByBarrier(unitState, barrier, batch);
            return new Dictionary { ["stopped"] = true };
        }
        return new Dictionary { ["stopped"] = false };
    }

    private Dictionary _ApplyPoisonDeathOutcome(
        BattleUnitState unitState,
        BattleBarrierInstanceState barrier,
        BattleBarrierLayerState layer,
        BattleBarrierOutcomeState outcome,
        BattleEventBatch batch
    )
    {
        var saveResult = _ResolveOutcomeSave(unitState, barrier, layer, outcome);
        if (saveResult.GetValueOrDefault("success", false).AsBool())
        {
            var successAmount = Mathf.Max(outcome?.success_amount ?? 0, 0);
            if (successAmount <= 0)
                return new Dictionary { ["stopped"] = false };
            var damageTag = ResolveDamageTag(
                outcome.success_damage_tag != "" ? outcome.success_damage_tag : outcome.damage_tag,
                "poison"
            );
            var damageResult = _ApplyDirectDamage(unitState, barrier, successAmount, damageTag);
            _AppendChangedUnit(batch, unitState);
            _AppendLog(
                batch,
                $"{unitState.display_name} 通过 {_GetLayerLabel(layer)} 的豁免，仍受到 {damageResult.GetValueOrDefault("damage", successAmount)} 点伤害。"
            );
            if (!unitState.is_alive)
            {
                _HandleDefeatedByBarrier(unitState, barrier, batch);
                return new Dictionary { ["stopped"] = true };
            }
            return new Dictionary { ["stopped"] = false };
        }
        var fatalDamage = Mathf.Max(
            unitState.current_hp
                + unitState.current_shield_hp
                + Mathf.Max(outcome?.fatal_damage ?? DEFAULT_FATAL_DAMAGE, 1),
            Mathf.Max(outcome?.fatal_damage ?? DEFAULT_FATAL_DAMAGE, 1)
        );
        var deathResult = _ApplyDirectDamage(unitState, barrier, fatalDamage, "poison");
        _AppendChangedUnit(batch, unitState);
        _AppendLog(
            batch,
            $"{unitState.display_name} 未通过 {_GetLayerLabel(layer)} 的豁免，触发即死效果。"
        );
        if (!unitState.is_alive)
        {
            _HandleDefeatedByBarrier(unitState, barrier, batch);
            return new Dictionary { ["stopped"] = true };
        }
        if ((int)deathResult.GetValueOrDefault("damage", 0) > 0)
            _AppendLog(batch, $"{unitState.display_name} 的免死效果抵消了即死。");
        return new Dictionary { ["stopped"] = false };
    }

    private Dictionary _ApplyStatusOutcome(
        BattleUnitState unitState,
        BattleBarrierInstanceState barrier,
        BattleBarrierLayerState layer,
        BattleBarrierOutcomeState outcome,
        BattleEventBatch batch
    )
    {
        StringName statusId = outcome?.status_id ?? new StringName("");
        if (statusId == "")
            return new Dictionary { ["stopped"] = false };
        var saveResult = _ResolveOutcomeSave(unitState, barrier, layer, outcome);
        if (saveResult.GetValueOrDefault("success", false).AsBool())
        {
            _AppendLog(batch, $"{unitState.display_name} 通过 {_GetLayerLabel(layer)} 的豁免。");
            return new Dictionary { ["stopped"] = false };
        }
        _ApplyBarrierStatus(unitState, barrier, layer, outcome, statusId);
        _AppendChangedUnit(batch, unitState);
        _AppendLog(
            batch,
            $"{unitState.display_name} 未通过 {_GetLayerLabel(layer)} 的豁免，获得状态 {statusId}。"
        );
        return new Dictionary { ["stopped"] = true };
    }

    private Dictionary _ApplyBanishOutcome(
        BattleUnitState unitState,
        BattleBarrierInstanceState barrier,
        BattleBarrierLayerState layer,
        BattleBarrierOutcomeState outcome,
        BattleEventBatch batch
    )
    {
        var saveResult = _ResolveOutcomeSave(unitState, barrier, layer, outcome);
        if (saveResult.GetValueOrDefault("success", false).AsBool())
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
            _AppendLog(
                batch,
                $"{unitState.display_name} 被 {_GetLayerLabel(layer)} 放逐，但没有找到可传送落点。"
            );
            return new Dictionary { ["stopped"] = true };
        }
        var previousCoords = new Godot.Collections.Array();
        foreach (Vector2I coord in unitState.occupied_coords)
            previousCoords.Add(coord);
        var runtime = _ResolveRuntime();
        var state = runtime._state;
        runtime._grid_service.clear_unit_occupancy(state, unitState);
        unitState.set_anchor_coord(destination);
        runtime._grid_service.set_occupants(
            state,
            (Godot.Collections.Array)unitState.occupied_coords,
            unitState.unit_id
        );
        _AppendChangedCoords(batch, previousCoords);
        _AppendChangedUnit(batch, unitState);
        _AppendLog(
            batch,
            $"{unitState.display_name} 被 {_GetLayerLabel(layer)} 随机传送到 ({destination.X}, {destination.Y})。"
        );
        return new Dictionary { ["stopped"] = true };
    }

    private Dictionary _ResolveOutcomeSave(
        BattleUnitState unitState,
        BattleBarrierInstanceState barrier,
        BattleBarrierLayerState layer,
        BattleBarrierOutcomeState outcome
    )
    {
        var effect = new CombatEffectDef();
        effect.effect_type = "status";
        int outcomeSaveDc = outcome?.save_dc ?? 0;
        int barrierSaveDc = barrier?.save_dc ?? 0;
        effect.save_dc = Mathf.Max(outcomeSaveDc > 0 ? outcomeSaveDc : barrierSaveDc, 1);
        effect.save_dc_mode = BattleSaveResolver.SAVE_DC_MODE_STATIC();
        effect.save_ability = ResolveStringName(outcome?.save_ability ?? new StringName(""), "willpower");
        effect.save_tag = ResolveStringName(outcome?.save_tag ?? new StringName(""), "magic");
        var context = new Dictionary();
        if (layer != null && layer.has_save_roll_override)
            context["save_roll_override"] = layer.save_roll_override;
        return BattleSaveResolver.resolve_save_with_context(
            _GetBarrierSourceUnit(barrier),
            unitState,
            effect,
            context
        );
    }

    private void _ApplyBarrierStatus(
        BattleUnitState unitState,
        BattleBarrierInstanceState barrier,
        BattleBarrierLayerState layer,
        BattleBarrierOutcomeState outcome,
        StringName statusId
    )
    {
        var statusEntry = new BattleStatusEffectState();
        statusEntry.status_id = statusId;
        statusEntry.source_unit_id = barrier?.source_unit_id ?? new StringName("");
        statusEntry.power = 1;
        statusEntry.stacks = 1;
        statusEntry.duration = -1;
        int rawStatusSaveDc = outcome != null && outcome.save_dc > 0
            ? outcome.save_dc
            : barrier?.save_dc ?? 0;
        int statusSaveDc = Mathf.Max(rawStatusSaveDc, 1);
        StringName statusSaveAbility = ResolveStringName(
            outcome?.save_ability ?? new StringName(""),
            "willpower"
        );
        StringName statusSaveTag = ResolveStringName(outcome?.save_tag ?? new StringName(""), "magic");
        statusEntry.@params = new Dictionary
        {
            ["source"] = barrier?.profile_id.ToString() ?? "",
            ["layer_id"] = layer?.layer_id.ToString() ?? "",
            ["counts_as_debuff"] = true,
            ["self_save_dc"] = statusSaveDc,
            ["self_save_ability"] = statusSaveAbility.ToString(),
            ["self_save_tag"] = statusSaveTag.ToString(),
        };
        unitState.set_status_effect(statusEntry);
    }

    private Dictionary _ApplyDirectDamage(
        BattleUnitState unitState,
        BattleBarrierInstanceState barrier,
        int damageAmount,
        StringName damageTag
    )
    {
        var damageOutcome = new Dictionary
        {
            ["resolved_damage"] = Mathf.Max(damageAmount, 0),
            ["base_damage"] = Mathf.Max(damageAmount, 0),
            ["damage_tag"] = damageTag.ToString(),
            ["damage_kind"] = ResolveStringName(barrier?.profile_id ?? new StringName(""), "barrier").ToString(),
        };
        var sourceUnit = _GetBarrierSourceUnit(barrier);
        var damageResult = _ResolveRuntime()
            ._damage_resolver
            .apply_direct_damage_to_target(unitState, damageOutcome, sourceUnit);
        unitState.is_alive = unitState.current_hp > 0;
        return damageResult;
    }

    private void _HandleDefeatedByBarrier(
        BattleUnitState unitState,
        BattleBarrierInstanceState barrier,
        BattleEventBatch batch
    )
    {
        var sourceUnit = _GetBarrierSourceUnit(barrier);
        _ResolveRuntime()
            .handle_unit_defeated_by_runtime_effect(
                unitState,
                sourceUnit,
                batch,
                $"{unitState.display_name} 被 {_GetBarrierLabel(barrier)} 击倒。"
            );
    }

    private void _RemoveSummonedUnit(
        BattleUnitState unitState,
        BattleBarrierInstanceState barrier,
        BattleBarrierLayerState layer,
        BattleEventBatch batch
    )
    {
        _ResolveRuntime()
            .remove_summoned_unit_from_battle(
                unitState,
                batch,
                $"{unitState.display_name} 是召唤物，被 {_GetLayerLabel(layer)} 直接放逐消失。"
            );
    }

    private Vector2I _FindBanishTeleportCoord(
        BattleUnitState unitState,
        BattleBarrierInstanceState barrier
    )
    {
        var runtime = _ResolveRuntime();
        if (runtime == null)
            return new Vector2I(-1, -1);
        var state = runtime._state;
        var gridService = runtime._grid_service;
        if (state == null || gridService == null || unitState == null)
            return new Vector2I(-1, -1);
        var candidates = new List<Vector2I>();
        foreach (BattleState.BattleCellEntry cellEntry in state.GetCellEntriesTyped())
        {
            Vector2I coord = cellEntry.Coord;
            if (_IsCoordInsideBarrier(coord, barrier))
                continue;
            if (
                !gridService.can_place_footprint(
                        state,
                        coord,
                        unitState.footprint_size,
                        unitState.unit_id,
                        unitState
                    )
            )
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
        candidates.Sort(
            (left, right) =>
            {
                var leftDistance = gridService.get_distance(unitState.coord, left);
                var rightDistance = gridService.get_distance(unitState.coord, right);
                if (leftDistance != rightDistance)
                    return leftDistance.CompareTo(rightDistance);
                if (left.Y != right.Y)
                    return left.Y.CompareTo(right.Y);
                return left.X.CompareTo(right.X);
            }
        );
        return candidates[0];
    }

    private bool _IsCoordInsideBarrier(Vector2I coord, BattleBarrierInstanceState barrier)
    {
        if (barrier == null)
        {
            return false;
        }
        var anchor = barrier.anchor_coord;
        var radius = Mathf.Max(barrier.radius_cells, 0);
        var pattern = BattleTypedNames.ToAreaPattern(barrier.area_pattern);
        var dx = Mathf.Abs(coord.X - anchor.X);
        var dy = Mathf.Abs(coord.Y - anchor.Y);
        switch (pattern)
        {
            case BattleAreaPattern.Square:
            case BattleAreaPattern.Radius:
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
        if (unitState.ai_blackboard.GetValueOrDefault("summoned", false).AsBool())
            return true;
        if (unitState.ai_blackboard.GetValueOrDefault("temporary_unit", false).AsBool())
            return true;
        return !string.IsNullOrEmpty(
            unitState.ai_blackboard.GetValueOrDefault("summon_source_unit_id", "").AsString()
        );
    }

    private BattleUnitState _GetBarrierSourceUnit(BattleBarrierInstanceState barrier)
    {
        var runtime = _ResolveRuntime();
        if (runtime == null)
            return null;
        var state = runtime._state;
        if (state == null)
            return null;
        var sourceUnitId = barrier?.source_unit_id ?? new StringName("");
        if (sourceUnitId == "")
            return null;
        return state.TryGetUnitTyped(sourceUnitId, out BattleUnitState sourceUnit)
            ? sourceUnit
            : null;
    }

    private string _GetLayerLabel(BattleBarrierLayerState layer)
    {
        if (layer == null)
            return "屏障层";
        if (!string.IsNullOrEmpty(layer.display_name))
            return layer.display_name;
        string layerId = layer.layer_id.ToString();
        return !string.IsNullOrEmpty(layerId) ? layerId : "屏障层";
    }

    private string _GetBarrierLabel(BattleBarrierInstanceState barrier)
    {
        if (barrier == null)
            return "屏障";
        if (!string.IsNullOrEmpty(barrier.display_name))
            return barrier.display_name;
        string profileId = barrier.profile_id.ToString();
        return !string.IsNullOrEmpty(profileId) ? profileId : "屏障";
    }

    private static bool IsLayerEmpty(BattleBarrierLayerState layer)
    {
        return layer == null || (layer.layer_id == "" && layer.passage_outcomes.Count == 0);
    }

    private static StringName ResolveDamageTag(StringName value, StringName fallback)
    {
        return value != "" ? value : fallback;
    }

    private static StringName ResolveStringName(StringName value, StringName fallback)
    {
        return value != "" ? value : fallback;
    }

    private void _AppendChangedUnit(BattleEventBatch batch, BattleUnitState unitState)
    {
        var runtime = _ResolveRuntime();
        if (runtime == null || batch == null || unitState == null)
            return;
        runtime._append_changed_unit_id(batch, unitState.unit_id);
        runtime._append_changed_unit_coords(batch, unitState);
    }

    private void _AppendChangedCoords(BattleEventBatch batch, Godot.Collections.Array coords)
    {
        var runtime = _ResolveRuntime();
        if (runtime == null || batch == null)
            return;
        runtime._append_changed_coords(batch, coords);
    }

    private void _AppendLog(BattleEventBatch batch, string line)
    {
        if (batch == null || string.IsNullOrEmpty(line))
            return;
        batch.log_lines.Add(line);
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

}
