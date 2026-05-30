using System;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

// 翻译自 battle_special_skill_resolver.gd（2026-05-25，战斗特殊技能 C# 迁移）。
// runtime 耦合：战斗实体统一按 GodotObject + GdInterop 访问；保留 snake_case 公开接口供 GDScript 调用。
[GlobalClass]
public partial class BattleSpecialSkillResolver : RefCounted
{
    private static readonly StringName BODY_SIZE_CATEGORY_OVERRIDE_EFFECT_TYPE =
        "body_size_category_override";
    private static readonly StringName LAYERED_BARRIER_EFFECT_TYPE = "layered_barrier";
    private static readonly StringName STATUS_MARKED = "marked";
    private static readonly StringName STATUS_GUARDING = "guarding";
    private static readonly StringName STATUS_VAJRA_BODY = "vajra_body";
    private static readonly StringName STATUS_BLACK_STAR_BRAND_NORMAL = "black_star_brand_normal";
    private static readonly StringName STATUS_BLACK_STAR_BRAND_ELITE = "black_star_brand_elite";
    private static readonly StringName STATUS_BLACK_STAR_BRAND_ELITE_GUARD_WINDOW =
        "black_star_brand_elite_guard_window";
    private static readonly StringName STATUS_CROWN_BREAK_BROKEN_FANG = "crown_break_broken_fang";
    private static readonly StringName STATUS_CROWN_BREAK_BROKEN_HAND = "crown_break_broken_hand";
    private static readonly StringName STATUS_CROWN_BREAK_BLINDED_EYE = "crown_break_blinded_eye";
    private static readonly StringName BLACK_CONTRACT_PUSH_SKILL_ID = "black_contract_push";
    private static readonly StringName DOOM_SHIFT_SKILL_ID = "doom_shift";

    private static readonly StringName BLACK_CROWN_SEAL_SKILL_ID =
        MisfortuneService.BLACK_CROWN_SEAL_SKILL_ID;
    private static readonly StringName BLACK_STAR_BRAND_SKILL_ID =
        MisfortuneService.BLACK_STAR_BRAND_SKILL_ID;
    private static readonly StringName CROWN_BREAK_SKILL_ID =
        MisfortuneService.CROWN_BREAK_SKILL_ID;
    private static readonly StringName DOOM_SENTENCE_SKILL_ID =
        MisfortuneService.DOOM_SENTENCE_SKILL_ID;
    private static readonly StringName CALAMITY_REASON_ADJACENT_ALLY_DEFEATED =
        "adjacent_ally_defeated";

    private const int BLACK_STAR_BRAND_DURATION_TU = 60;
    private const int DOOM_SHIFT_SELF_DEBUFF_DURATION_TU = 60;

    private const string STATUS_PARAM_BODY_SIZE_CATEGORY_OVERRIDE = "body_size_category_override";
    private const string STATUS_PARAM_PREVIOUS_BODY_SIZE_CATEGORY = "previous_body_size_category";

    private const int FORCED_MOVE_INVALID_SCORE = -999999;

    private WeakReference<GodotObject> _runtimeRef;

    private BattleRuntimeModule _runtime
    {
        get => ResolveWeakRef(_runtimeRef) as BattleRuntimeModule;
        set => _runtimeRef = value != null ? new WeakReference<GodotObject>(value) : null;
    }

    public void setup(GodotObject runtime)
    {
        _runtime = runtime as BattleRuntimeModule;
    }

    public void dispose()
    {
        _runtime = null;
    }

    public bool _is_unit_valid_for_effect(
        GodotObject source_unit,
        GodotObject target_unit,
        StringName target_team_filter
    )
    {
        if (_runtime == null)
        {
            return false;
        }
        return _runtime._is_unit_valid_for_effect(
            source_unit as BattleUnitState,
            target_unit as BattleUnitState,
            target_team_filter
        );
    }

    public void _apply_skill_mastery_grant(
        GodotObject unit_state,
        GDictionary grant,
        GodotObject batch
    )
    {
        if (_runtime == null)
        {
            return;
        }
        _runtime._apply_skill_mastery_grant(unit_state as BattleUnitState, grant, batch as BattleEventBatch);
    }

    public void _append_changed_coords(GodotObject batch, GArray coords)
    {
        if (_runtime == null)
        {
            return;
        }
        _runtime._append_changed_coords(batch as BattleEventBatch, coords);
    }

    public void _append_changed_unit_id(GodotObject batch, StringName unit_id)
    {
        if (_runtime == null)
        {
            return;
        }
        _runtime._append_changed_unit_id(batch as BattleEventBatch, unit_id);
    }

    public void _append_changed_unit_coords(GodotObject batch, GodotObject unit_state)
    {
        if (_runtime == null)
        {
            return;
        }
        _runtime._append_changed_unit_coords(batch as BattleEventBatch, unit_state as BattleUnitState);
    }

    public void _apply_on_kill_gain_resources_effects(
        GodotObject source_unit,
        GodotObject defeated_unit,
        GodotObject skill_def,
        GArray effect_defs,
        GodotObject batch
    )
    {
        if (source_unit == null || defeated_unit == null || skill_def == null || batch == null)
        {
            return;
        }
        if (GdInterop.GetBool(defeated_unit, "is_alive"))
        {
            return;
        }
        foreach (var effectDefValue in effect_defs)
        {
            GodotObject effectDef = effectDefValue.AsGodotObject();
            if (
                effectDef == null
                || GdInterop.GetStringName(effectDef, "effect_type") != "on_kill_gain_resources"
            )
            {
                continue;
            }
            GDictionary parameters = GdInterop.GetDictionary(effectDef, "params");
            int apGain = Math.Max(GdInterop.GetInt(parameters, "ap_gain", 0), 0);
            int freeMovePointsGain = Math.Max(
                GdInterop.GetInt(parameters, "free_move_points_gain", 0),
                0
            );
            if (apGain <= 0 && freeMovePointsGain <= 0)
            {
                continue;
            }
            if (apGain > 0)
            {
                source_unit.Set("current_ap", GdInterop.GetInt(source_unit, "current_ap") + apGain);
            }
            if (freeMovePointsGain > 0)
            {
                source_unit.Set(
                    "current_move_points",
                    GdInterop.GetInt(source_unit, "current_move_points") + freeMovePointsGain
                );
                source_unit.Set("can_use_locked_move_points_this_turn", true);
            }
            _append_changed_unit_id(batch, GdInterop.GetStringName(source_unit, "unit_id"));
            var gainParts = new System.Collections.Generic.List<string>();
            if (apGain > 0)
            {
                gainParts.Add($"恢复 {apGain} AP");
            }
            if (freeMovePointsGain > 0)
            {
                gainParts.Add($"获得 {freeMovePointsGain} 点普通移动力并可在行动后移动");
            }
            string skillName = !string.IsNullOrEmpty(GdInterop.GetString(skill_def, "display_name"))
                ? GdInterop.GetString(skill_def, "display_name")
                : GdInterop.GetStringName(skill_def, "skill_id").ToString();
            GdInterop
                .GetArray(batch, "log_lines")
                .Add(
                    $"{GdInterop.GetString(source_unit, "display_name")} 击倒 {GdInterop.GetString(defeated_unit, "display_name")}，触发 {skillName}：{string.Join("，", gainParts)}。"
                );
        }
    }

    public GDictionary _apply_unit_skill_special_effects(
        GodotObject active_unit,
        GodotObject target_unit,
        GodotObject skill_def,
        GodotObject cast_variant,
        GArray effect_defs,
        GodotObject batch,
        GDictionary forced_move_context = null
    )
    {
        forced_move_context ??= new GDictionary();
        var result = new GDictionary
        {
            ["applied"] = false,
            ["moved_steps"] = 0,
            ["status_effect_ids"] = new GArray(),
            ["log_lines"] = new GArray(),
        };
        if (active_unit == null || skill_def == null)
        {
            return result;
        }
        if (_is_black_star_brand_skill(GdInterop.GetStringName(skill_def, "skill_id")))
        {
            return _apply_black_star_brand_effect(active_unit, target_unit);
        }
        if (_is_doom_shift_skill(GdInterop.GetStringName(skill_def, "skill_id")))
        {
            return _apply_doom_shift_effect(active_unit, target_unit, batch);
        }
        if (effect_defs == null || effect_defs.Count == 0)
        {
            return result;
        }

        BattleLayeredBarrierService layeredBarrierService = _runtime._layered_barrier_service;
        var seenForcedMoveEffects = new GDictionary();
        foreach (var effectDefValue in effect_defs)
        {
            GodotObject effectDef = effectDefValue.AsGodotObject();
            if (effectDef == null)
            {
                continue;
            }
            StringName effectType = GdInterop.GetStringName(effectDef, "effect_type");
            if (effectType == LAYERED_BARRIER_EFFECT_TYPE)
            {
                GDictionary barrierResult =
                    layeredBarrierService != null
                        ? layeredBarrierService.ApplyLayeredBarrierEffect(
                            active_unit as BattleUnitState,
                            (target_unit ?? active_unit) as BattleUnitState,
                            skill_def as SkillDef,
                            effectDef as CombatEffectDef,
                            batch as BattleEventBatch
                        )
                        : new GDictionary();
                if (GdInterop.GetBool(barrierResult, "applied", false))
                {
                    result["applied"] = true;
                }
                continue;
            }
            if (effectType == BODY_SIZE_CATEGORY_OVERRIDE_EFFECT_TYPE)
            {
                GDictionary bodySizeResult = _apply_body_size_category_override_effect(
                    active_unit,
                    target_unit ?? active_unit,
                    effectDef,
                    batch
                );
                if (GdInterop.GetBool(bodySizeResult, "applied", false))
                {
                    result["applied"] = true;
                    foreach (
                        Variant statusId in GdInterop.GetArray(bodySizeResult, "status_effect_ids")
                    )
                    {
                        if (!GdInterop.GetArray(result, "status_effect_ids").Contains(statusId))
                        {
                            GdInterop.GetArray(result, "status_effect_ids").Add(statusId);
                        }
                    }
                    foreach (var logLine in GdInterop.GetArray(bodySizeResult, "log_lines"))
                    {
                        GdInterop.GetArray(result, "log_lines").Add(logLine.ToString());
                    }
                }
                continue;
            }
            if (effectType != "forced_move")
            {
                continue;
            }
            ulong forcedMoveInstanceId = effectDef.GetInstanceId();
            if (seenForcedMoveEffects.ContainsKey(forcedMoveInstanceId))
            {
                continue;
            }
            seenForcedMoveEffects[forcedMoveInstanceId] = true;
            GodotObject moveTarget = target_unit ?? active_unit;
            int movedSteps = _apply_forced_move_effect(
                active_unit,
                moveTarget,
                effectDef,
                batch,
                forced_move_context
            );
            if (movedSteps > 0)
            {
                result["applied"] = true;
                result["moved_steps"] = Math.Max(
                    GdInterop.GetInt(result, "moved_steps", 0),
                    movedSteps
                );
            }
        }
        return result;
    }

    public GDictionary _apply_doom_shift_effect(
        GodotObject active_unit,
        GodotObject target_unit,
        GodotObject batch
    )
    {
        var result = new GDictionary
        {
            ["applied"] = false,
            ["moved_steps"] = 0,
            ["status_effect_ids"] = new GArray(),
            ["log_lines"] = new GArray(),
        };
        if (RtState() == null || active_unit == null || target_unit == null)
        {
            return result;
        }
        if (
            GdInterop.GetStringName(target_unit, "unit_id")
            == GdInterop.GetStringName(active_unit, "unit_id")
        )
        {
            return result;
        }
        if (!_swap_unit_positions(active_unit, target_unit, batch))
        {
            return result;
        }
        _set_runtime_status_effect(
            active_unit,
            STATUS_MARKED,
            DOOM_SHIFT_SELF_DEBUFF_DURATION_TU,
            GdInterop.GetStringName(active_unit, "unit_id"),
            1,
            new GDictionary { ["counts_as_debuff"] = true }
        );
        _append_changed_unit_id(batch, GdInterop.GetStringName(active_unit, "unit_id"));
        result["applied"] = true;
        result["log_lines"] = new GArray
        {
            $"{GdInterop.GetString(active_unit, "display_name")} 先承受 marked，再与 {GdInterop.GetString(target_unit, "display_name")} 交换位置。",
        };
        return result;
    }

    public bool _swap_unit_positions(
        GodotObject first_unit,
        GodotObject second_unit,
        GodotObject batch
    )
    {
        GodotObject state = RtState();
        BattleGridService gridService = _runtime.get_grid_service();
        if (state == null || first_unit == null || second_unit == null)
        {
            return false;
        }
        if (
            GdInterop.GetStringName(first_unit, "unit_id")
            == GdInterop.GetStringName(second_unit, "unit_id")
        )
        {
            return false;
        }
        GArray firstPreviousCoords = (GArray)
            GdInterop.GetArray(first_unit, "occupied_coords").Duplicate();
        GArray secondPreviousCoords = (GArray)
            GdInterop.GetArray(second_unit, "occupied_coords").Duplicate();
        Vector2I firstCoord = GdInterop.GetVector2I(first_unit, "coord");
        Vector2I secondCoord = GdInterop.GetVector2I(second_unit, "coord");
        if (!_resolve_swap_barrier_passage(first_unit, firstCoord, secondCoord, batch))
        {
            return false;
        }
        if (!_resolve_swap_barrier_passage(second_unit, secondCoord, firstCoord, batch))
        {
            return false;
        }
        gridService.clear_unit_occupancy(state, first_unit as BattleUnitState);
        gridService.clear_unit_occupancy(state, second_unit as BattleUnitState);
        bool canSwap =
            gridService.can_place_unit(state, first_unit as BattleUnitState, secondCoord, true)
            && gridService.can_place_unit(state, second_unit as BattleUnitState, firstCoord, true);
        if (!canSwap)
        {
            gridService.set_occupants(
                state,
                firstPreviousCoords,
                GdInterop.GetStringName(first_unit, "unit_id")
            );
            gridService.set_occupants(
                state,
                secondPreviousCoords,
                GdInterop.GetStringName(second_unit, "unit_id")
            );
            return false;
        }
        gridService.place_unit(state, first_unit as BattleUnitState, secondCoord, true);
        gridService.place_unit(state, second_unit as BattleUnitState, firstCoord, true);
        _append_changed_coords(batch, firstPreviousCoords);
        _append_changed_coords(batch, secondPreviousCoords);
        _append_changed_unit_coords(batch, first_unit);
        _append_changed_unit_coords(batch, second_unit);
        _append_changed_unit_id(batch, GdInterop.GetStringName(first_unit, "unit_id"));
        _append_changed_unit_id(batch, GdInterop.GetStringName(second_unit, "unit_id"));
        return true;
    }

    public bool _resolve_swap_barrier_passage(
        GodotObject unit_state,
        Vector2I from_coord,
        Vector2I to_coord,
        GodotObject batch
    )
    {
        if (unit_state == null)
        {
            return false;
        }
        BattleLayeredBarrierService layeredBarrierService = _runtime._layered_barrier_service;
        if (layeredBarrierService == null)
        {
            return true;
        }
        GDictionary barrierResult = layeredBarrierService.ResolveUnitBoundaryCrossing(
            unit_state as BattleUnitState,
            from_coord,
            to_coord,
            batch as BattleEventBatch
        );
        return !GdInterop.GetBool(barrierResult, "blocked", false)
            && GdInterop.GetBool(unit_state, "is_alive")
            && GdInterop.GetVector2I(unit_state, "coord") == from_coord;
    }

    public GDictionary _apply_black_star_brand_effect(
        GodotObject active_unit,
        GodotObject target_unit
    )
    {
        var result = new GDictionary
        {
            ["applied"] = false,
            ["moved_steps"] = 0,
            ["status_effect_ids"] = new GArray(),
            ["log_lines"] = new GArray(),
        };
        if (active_unit == null || target_unit == null)
        {
            return result;
        }
        _clear_black_star_brand_statuses(target_unit);
        if (_is_black_star_brand_elite_target(target_unit))
        {
            _set_runtime_status_effect(
                target_unit,
                STATUS_BLACK_STAR_BRAND_ELITE,
                BLACK_STAR_BRAND_DURATION_TU,
                GdInterop.GetStringName(active_unit, "unit_id")
            );
            _set_runtime_status_effect(
                target_unit,
                STATUS_BLACK_STAR_BRAND_ELITE_GUARD_WINDOW,
                BLACK_STAR_BRAND_DURATION_TU,
                GdInterop.GetStringName(active_unit, "unit_id")
            );
            result["status_effect_ids"] = new GArray
            {
                STATUS_BLACK_STAR_BRAND_ELITE,
                STATUS_BLACK_STAR_BRAND_ELITE_GUARD_WINDOW,
            };
            result["log_lines"] = new GArray
            {
                $"{GdInterop.GetString(target_unit, "display_name")} 被施加黑星烙印：暴击失效、命中下降，且第一次受击会被穿透部分格挡。",
            };
        }
        else
        {
            _set_runtime_status_effect(
                target_unit,
                STATUS_BLACK_STAR_BRAND_NORMAL,
                BLACK_STAR_BRAND_DURATION_TU,
                GdInterop.GetStringName(active_unit, "unit_id")
            );
            (target_unit as BattleUnitState)?.erase_status_effect(STATUS_GUARDING);
            result["status_effect_ids"] = new GArray { STATUS_BLACK_STAR_BRAND_NORMAL };
            result["log_lines"] = new GArray
            {
                $"{GdInterop.GetString(target_unit, "display_name")} 被施加黑星烙印：无法反击，且无法进入格挡。",
            };
        }
        result["applied"] = true;
        return result;
    }

    public void _set_runtime_status_effect(
        GodotObject unit_state,
        StringName status_id,
        int duration_tu,
        StringName source_unit_id = null,
        int power = 1,
        GDictionary status_params = null
    )
    {
        source_unit_id ??= new StringName("");
        status_params ??= new GDictionary();
        if (unit_state == null || GdInterop.IsEmpty(status_id))
        {
            return;
        }
        var statusEntry = new BattleStatusEffectState
        {
            status_id = status_id,
            source_unit_id = source_unit_id,
            power = Math.Max(power, 1),
            stacks = 1,
            duration = Math.Max(duration_tu, -1),
            @params = (GDictionary)status_params.Duplicate(true),
        };
        (unit_state as BattleUnitState)?.set_status_effect(statusEntry);
    }

    public void _clear_black_star_brand_statuses(GodotObject unit_state)
    {
        if (unit_state == null)
        {
            return;
        }
        (unit_state as BattleUnitState)?.erase_status_effect(STATUS_BLACK_STAR_BRAND_NORMAL);
        (unit_state as BattleUnitState)?.erase_status_effect(STATUS_BLACK_STAR_BRAND_ELITE);
        (unit_state as BattleUnitState)?.erase_status_effect(STATUS_BLACK_STAR_BRAND_ELITE_GUARD_WINDOW);
    }

    public bool _is_black_star_brand_elite_target(GodotObject unit_state)
    {
        return _is_elite_or_boss_target(unit_state);
    }

    public bool _is_elite_or_boss_target(GodotObject unit_state)
    {
        return BattleExecutionRules.is_elite_or_boss_target(unit_state);
    }

    public bool _is_boss_target(GodotObject unit_state)
    {
        return BattleExecutionRules.is_boss_target(unit_state);
    }

    public bool _is_black_star_brand_skill(StringName skill_id)
    {
        return ProgressionDataUtils.to_string_name(skill_id) == BLACK_STAR_BRAND_SKILL_ID;
    }

    public bool _is_black_contract_push_skill(StringName skill_id)
    {
        return ProgressionDataUtils.to_string_name(skill_id) == BLACK_CONTRACT_PUSH_SKILL_ID;
    }

    public bool _is_doom_shift_skill(StringName skill_id)
    {
        return ProgressionDataUtils.to_string_name(skill_id) == DOOM_SHIFT_SKILL_ID;
    }

    public bool _is_black_crown_seal_skill(StringName skill_id)
    {
        return ProgressionDataUtils.to_string_name(skill_id) == BLACK_CROWN_SEAL_SKILL_ID;
    }

    public void _clear_crown_break_seal_statuses(GodotObject unit_state)
    {
        if (unit_state == null)
        {
            return;
        }
        (unit_state as BattleUnitState)?.erase_status_effect(STATUS_CROWN_BREAK_BROKEN_FANG);
        (unit_state as BattleUnitState)?.erase_status_effect(STATUS_CROWN_BREAK_BROKEN_HAND);
        (unit_state as BattleUnitState)?.erase_status_effect(STATUS_CROWN_BREAK_BLINDED_EYE);
    }

    public bool _is_crown_break_target_eligible(GodotObject active_unit, GodotObject target_unit)
    {
        return target_unit != null
            && _is_unit_valid_for_effect(active_unit, target_unit, "enemy")
            && (target_unit as BattleUnitState)?.has_status_effect(STATUS_BLACK_STAR_BRAND_ELITE) == true;
    }

    public bool _is_crown_break_skill(StringName skill_id)
    {
        return ProgressionDataUtils.to_string_name(skill_id) == CROWN_BREAK_SKILL_ID;
    }

    public bool _is_doom_sentence_target_eligible(GodotObject active_unit, GodotObject target_unit)
    {
        return target_unit != null
            && _is_unit_valid_for_effect(active_unit, target_unit, "enemy")
            && _is_elite_or_boss_target(target_unit);
    }

    public bool _is_black_crown_seal_target_eligible(
        GodotObject active_unit,
        GodotObject target_unit
    )
    {
        return target_unit != null
            && _is_unit_valid_for_effect(active_unit, target_unit, "enemy")
            && _is_boss_target(target_unit);
    }

    public bool _is_doom_sentence_skill(StringName skill_id)
    {
        return ProgressionDataUtils.to_string_name(skill_id) == DOOM_SENTENCE_SKILL_ID;
    }

    public int _apply_forced_move_effect(
        GodotObject source_unit,
        GodotObject unit_state,
        GodotObject effect_def,
        GodotObject batch,
        GDictionary forced_move_context = null
    )
    {
        forced_move_context ??= new GDictionary();
        GodotObject state = RtState();
        BattleGridService gridService = _runtime.get_grid_service();
        if (state == null || unit_state == null || effect_def == null)
        {
            return 0;
        }
        int moveDistance = Math.Max(GdInterop.GetInt(effect_def, "forced_move_distance"), 0);
        if (moveDistance <= 0)
        {
            return 0;
        }
        if (_blocks_enemy_forced_move(source_unit, unit_state))
        {
            if (batch != null)
            {
                GdInterop
                    .GetArray(batch, "log_lines")
                    .Add(
                        $"{GdInterop.GetString(unit_state, "display_name")} 稳如金刚，未被强制位移。"
                    );
            }
            return 0;
        }

        StringName mode = GdInterop.GetStringName(effect_def, "forced_move_mode");
        if (GdInterop.IsEmpty(mode))
        {
            return 0;
        }
        if (mode == "jump" || mode == "blink")
        {
            return 0;
        }

        BattleLayeredBarrierService layeredBarrierService = _runtime._layered_barrier_service;
        int movedSteps = 0;
        for (int step = 0; step < moveDistance; step++)
        {
            Vector2I nextCoord = _pick_forced_move_coord(
                unit_state,
                mode,
                source_unit,
                forced_move_context
            );
            if (
                nextCoord == new Vector2I(-1, -1)
                || nextCoord == GdInterop.GetVector2I(unit_state, "coord")
            )
            {
                break;
            }
            if (
                !gridService.can_traverse(
                    state,
                    GdInterop.GetVector2I(unit_state, "coord"),
                    nextCoord,
                    unit_state as BattleUnitState
                )
            )
            {
                break;
            }
            GDictionary barrierResult =
                layeredBarrierService != null
                    ? layeredBarrierService.ResolveUnitBoundaryCrossing(
                        unit_state as BattleUnitState,
                        GdInterop.GetVector2I(unit_state, "coord"),
                        nextCoord,
                        batch as BattleEventBatch
                    )
                    : new GDictionary();
            if (
                GdInterop.GetBool(barrierResult, "blocked", false)
                || !GdInterop.GetBool(unit_state, "is_alive")
            )
            {
                break;
            }
            GArray previousCoords = (GArray)
                GdInterop.GetArray(unit_state, "occupied_coords").Duplicate();
            if (!gridService.move_unit(state, unit_state as BattleUnitState, nextCoord))
            {
                break;
            }
            movedSteps += 1;
            _append_changed_coords(batch, previousCoords);
            _append_changed_unit_coords(batch, unit_state);
            _append_changed_unit_id(batch, GdInterop.GetStringName(unit_state, "unit_id"));
        }
        return movedSteps;
    }

    public GDictionary _apply_body_size_category_override_effect(
        GodotObject source_unit,
        GodotObject target_unit,
        GodotObject effect_def,
        GodotObject batch
    )
    {
        var result = new GDictionary
        {
            ["applied"] = false,
            ["status_effect_ids"] = new GArray(),
            ["log_lines"] = new GArray(),
        };
        GodotObject state = RtState();
        BattleGridService gridService = _runtime.get_grid_service();
        if (state == null || target_unit == null || effect_def == null)
        {
            return result;
        }
        StringName statusId = ProgressionDataUtils.to_string_name(
            GdInterop.GetStringName(effect_def, "status_id")
        );
        StringName targetCategory = ProgressionDataUtils.to_string_name(
            GdInterop.GetStringName(effect_def, "body_size_category")
        );
        if (
            GdInterop.IsEmpty(statusId)
            || !BodySizeRules.is_valid_body_size_category(targetCategory)
        )
        {
            return result;
        }
        int durationTu = Math.Max(GdInterop.GetInt(effect_def, "duration_tu"), 0);
        if (durationTu <= 0)
        {
            return result;
        }

        var existingEntry = (target_unit as BattleUnitState)?.get_status_effect(statusId);
        StringName restoreCategory = GdInterop.GetStringName(target_unit, "body_size_category");
        if (existingEntry != null && existingEntry.@params != null)
        {
            StringName existingRestoreCategory = ProgressionDataUtils.to_string_name(
                existingEntry.@params.GetValueOrDefault(
                    STATUS_PARAM_PREVIOUS_BODY_SIZE_CATEGORY,
                    ""
                )
            );
            if (BodySizeRules.is_valid_body_size_category(existingRestoreCategory))
            {
                restoreCategory = existingRestoreCategory;
            }
        }

        StringName previousCategory = GdInterop.GetStringName(target_unit, "body_size_category");
        int previousBodySize = GdInterop.GetInt(target_unit, "body_size");
        Vector2I previousFootprint = GdInterop.GetVector2I(target_unit, "footprint_size");
        GArray previousCoords = (GArray)
            GdInterop.GetArray(target_unit, "occupied_coords").Duplicate();
        gridService.clear_unit_occupancy(state, target_unit as BattleUnitState);
        (target_unit as BattleUnitState)?.set_body_size_category(targetCategory);
        if (
            !gridService.can_place_footprint(
                state,
                GdInterop.GetVector2I(target_unit, "coord"),
                GdInterop.GetVector2I(target_unit, "footprint_size"),
                GdInterop.GetStringName(target_unit, "unit_id"),
                target_unit as BattleUnitState
            )
        )
        {
            target_unit.Set("body_size_category", previousCategory);
            target_unit.Set("body_size", previousBodySize);
            target_unit.Set("footprint_size", previousFootprint);
            target_unit.Set("occupied_coords", previousCoords);
            gridService.set_occupants(
                state,
                GdInterop.GetArray(target_unit, "occupied_coords"),
                GdInterop.GetStringName(target_unit, "unit_id")
            );
            GdInterop
                .GetArray(result, "log_lines")
                .Add(
                    $"{GdInterop.GetString(target_unit, "display_name")} 周围空间不足，无法改变体型。"
                );
            return result;
        }
        gridService.set_occupants(
            state,
            GdInterop.GetArray(target_unit, "occupied_coords"),
            GdInterop.GetStringName(target_unit, "unit_id")
        );

        GDictionary statusParams = (GDictionary)
            GdInterop.GetDictionary(effect_def, "params").Duplicate(true);
        statusParams[STATUS_PARAM_BODY_SIZE_CATEGORY_OVERRIDE] = targetCategory.ToString();
        statusParams[STATUS_PARAM_PREVIOUS_BODY_SIZE_CATEGORY] = restoreCategory.ToString();
        _set_runtime_status_effect(
            target_unit,
            statusId,
            durationTu,
            source_unit != null
                ? GdInterop.GetStringName(source_unit, "unit_id")
                : new StringName(""),
            Math.Max(GdInterop.GetInt(effect_def, "power"), 1),
            statusParams
        );
        _append_changed_coords(batch, previousCoords);
        _append_changed_unit_coords(batch, target_unit);
        _append_changed_unit_id(batch, GdInterop.GetStringName(target_unit, "unit_id"));
        result["applied"] = true;
        result["status_effect_ids"] = new GArray { statusId };
        GdInterop
            .GetArray(result, "log_lines")
            .Add(
                $"{GdInterop.GetString(target_unit, "display_name")} 的体型临时变为 {targetCategory}。"
            );
        return result;
    }

    public bool _blocks_enemy_forced_move(GodotObject source_unit, GodotObject target_unit)
    {
        if (source_unit == null || target_unit == null)
        {
            return false;
        }
        if (
            GdInterop.GetStringName(source_unit, "unit_id")
            == GdInterop.GetStringName(target_unit, "unit_id")
        )
        {
            return false;
        }
        if (
            GdInterop.GetString(source_unit, "faction_id")
            == GdInterop.GetString(target_unit, "faction_id")
        )
        {
            return false;
        }
        BattleStatusEffectState statusEntry = (target_unit as BattleUnitState)?.get_status_effect(
            STATUS_VAJRA_BODY
        );
        if (statusEntry == null || GdInterop.GetObject(statusEntry, "params") == null)
        {
            return false;
        }
        return GdInterop.GetBool(
            GdInterop.GetDictionary(statusEntry, "params"),
            "forced_move_immune",
            false
        );
    }

    public void _record_vajra_body_mastery_from_incoming_damage(
        GodotObject source_unit,
        GodotObject target_unit,
        GodotObject skill_def,
        GDictionary result,
        GodotObject batch = null
    )
    {
        BattleSkillMasteryService skillMasteryService = _runtime._skill_mastery_service;
        var grant = skillMasteryService.build_vajra_body_mastery_grant(
            source_unit as BattleUnitState,
            target_unit as BattleUnitState,
            skill_def as SkillDef,
            result,
            _runtime.Get("_skill_defs").AsGodotDictionary()
        );
        _apply_skill_mastery_grant(target_unit, grant, batch);
    }

    public Vector2I _pick_forced_move_coord(
        GodotObject unit_state,
        StringName mode,
        GodotObject source_unit = null,
        GDictionary forced_move_context = null
    )
    {
        forced_move_context ??= new GDictionary();
        GodotObject state = RtState();
        BattleGridService gridService = _runtime.get_grid_service();
        if (state == null || unit_state == null)
        {
            return new Vector2I(-1, -1);
        }
        (unit_state as BattleUnitState)?.refresh_footprint();
        var bestCoord = new Vector2I(-1, -1);
        int bestScore = FORCED_MOVE_INVALID_SCORE;
        foreach (
            Vector2I direction in new[]
            {
                Vector2I.Up,
                Vector2I.Left,
                Vector2I.Right,
                Vector2I.Down,
            }
        )
        {
            Vector2I candidateCoord = GdInterop.GetVector2I(unit_state, "coord") + direction;
            if (
                !gridService.can_traverse(
                    state,
                    GdInterop.GetVector2I(unit_state, "coord"),
                    candidateCoord,
                    unit_state as BattleUnitState
                )
            )
            {
                continue;
            }
            int candidateScore = _score_forced_move_coord(
                unit_state,
                candidateCoord,
                mode,
                source_unit,
                forced_move_context
            );
            if (candidateScore <= FORCED_MOVE_INVALID_SCORE)
            {
                continue;
            }
            if (
                candidateScore > bestScore
                || (
                    candidateScore == bestScore
                    && (
                        bestCoord == new Vector2I(-1, -1)
                        || candidateCoord.Y < bestCoord.Y
                        || (candidateCoord.Y == bestCoord.Y && candidateCoord.X < bestCoord.X)
                    )
                )
            )
            {
                bestScore = candidateScore;
                bestCoord = candidateCoord;
            }
        }
        return bestCoord;
    }

    public int _score_forced_move_coord(
        GodotObject unit_state,
        Vector2I candidate_coord,
        StringName mode,
        GodotObject source_unit = null,
        GDictionary forced_move_context = null
    )
    {
        forced_move_context ??= new GDictionary();
        GodotObject state = RtState();
        BattleGridService gridService = _runtime.get_grid_service();
        if (state == null || unit_state == null)
        {
            return FORCED_MOVE_INVALID_SCORE;
        }
        if (mode == "wind_push")
        {
            return _score_wind_push_coord(
                unit_state,
                candidate_coord,
                source_unit,
                forced_move_context
            );
        }
        GArray hostileUnits = _collect_hostile_units_for(unit_state);
        int closestHostileDistance = 0;
        if (hostileUnits.Count != 0)
        {
            closestHostileDistance = 999999;
            foreach (var hostileUnitValue in hostileUnits)
            {
                GodotObject hostileUnit = hostileUnitValue.AsGodotObject();
                closestHostileDistance = Math.Min(
                    closestHostileDistance,
                    gridService.get_distance(
                        candidate_coord,
                        GdInterop.GetVector2I(hostileUnit, "coord")
                    )
                );
            }
        }
        int score = closestHostileDistance * 100;
        score -=
            gridService.get_distance(GdInterop.GetVector2I(unit_state, "coord"), candidate_coord)
            * 10;
        score -= candidate_coord.Y * 2 + candidate_coord.X;
        if (mode == "evasive")
        {
            score += 5;
        }
        return score;
    }

    public int _score_wind_push_coord(
        GodotObject unit_state,
        Vector2I candidate_coord,
        GodotObject source_unit,
        GDictionary forced_move_context = null
    )
    {
        forced_move_context ??= new GDictionary();
        Vector2I pushDirection = _resolve_forced_move_direction(
            unit_state,
            source_unit,
            forced_move_context
        );
        if (pushDirection == Vector2I.Zero)
        {
            return FORCED_MOVE_INVALID_SCORE;
        }
        Vector2I stepDelta = candidate_coord - GdInterop.GetVector2I(unit_state, "coord");
        if (_dot_vector2i(stepDelta, pushDirection) <= 0)
        {
            return FORCED_MOVE_INVALID_SCORE;
        }
        int currentProjection = _dot_vector2i(
            GdInterop.GetVector2I(unit_state, "coord"),
            pushDirection
        );
        int candidateProjection = _dot_vector2i(candidate_coord, pushDirection);
        return (candidateProjection - currentProjection) * 1000
            - candidate_coord.Y * 2
            - candidate_coord.X;
    }

    public int _dot_vector2i(Vector2I first, Vector2I second)
    {
        return first.X * second.X + first.Y * second.Y;
    }

    public Vector2I _resolve_forced_move_direction(
        GodotObject unit_state,
        GodotObject source_unit,
        GDictionary forced_move_context = null
    )
    {
        forced_move_context ??= new GDictionary();
        Vector2I contextDirection = GdInterop.GetVector2I(
            forced_move_context,
            "direction",
            Vector2I.Zero
        );
        if (contextDirection != Vector2I.Zero)
        {
            contextDirection = _normalize_axis_direction(contextDirection);
            if (contextDirection != Vector2I.Zero)
            {
                return contextDirection;
            }
        }
        if (source_unit != null && unit_state != null)
        {
            return _normalize_axis_direction(
                GdInterop.GetVector2I(unit_state, "coord")
                    - GdInterop.GetVector2I(source_unit, "coord")
            );
        }
        return Vector2I.Zero;
    }

    public Vector2I _normalize_axis_direction(Vector2I direction)
    {
        if (direction == Vector2I.Zero)
        {
            return Vector2I.Zero;
        }
        int absX = Math.Abs(direction.X);
        int absY = Math.Abs(direction.Y);
        if (absX >= absY && absX > 0)
        {
            return new Vector2I(direction.X > 0 ? 1 : -1, 0);
        }
        if (absY > 0)
        {
            return new Vector2I(0, direction.Y > 0 ? 1 : -1);
        }
        return Vector2I.Zero;
    }

    public GArray _collect_hostile_units_for(GodotObject unit_state)
    {
        var hostileUnits = new GArray();
        GodotObject state = RtState();
        if (state == null || unit_state == null)
        {
            return hostileUnits;
        }
        foreach (var otherUnitValue in GdInterop.GetDictionary(state, "units").Values)
        {
            GodotObject otherUnit = otherUnitValue.AsGodotObject();
            if (
                otherUnit == null
                || GdInterop.GetStringName(otherUnit, "unit_id")
                    == GdInterop.GetStringName(unit_state, "unit_id")
                || !GdInterop.GetBool(otherUnit, "is_alive")
            )
            {
                continue;
            }
            if (
                GdInterop.GetString(otherUnit, "faction_id")
                == GdInterop.GetString(unit_state, "faction_id")
            )
            {
                continue;
            }
            hostileUnits.Add(otherUnit);
        }
        return hostileUnits;
    }

    public void _handle_adjacent_ally_defeat(GodotObject defeated_unit)
    {
        if (RtState() == null || defeated_unit == null)
        {
            return;
        }
        if (
            GdInterop.GetBool(defeated_unit, "is_alive")
            || GdInterop.IsEmpty(GdInterop.GetStringName(defeated_unit, "source_member_id"))
        )
        {
            return;
        }
        if (_runtime == null || !_runtime.HasMethod("handle_misfortune_trigger"))
        {
            return;
        }
        GArray adjacentAllies = _collect_adjacent_living_allies(defeated_unit);
        if (adjacentAllies.Count == 0)
        {
            return;
        }
        _runtime.handle_misfortune_trigger(
            CALAMITY_REASON_ADJACENT_ALLY_DEFEATED,
            new GDictionary
            {
                ["defeated_unit"] = defeated_unit,
                ["adjacent_units"] = adjacentAllies,
            }
        );
    }

    public void _handle_low_luck_relic_ally_defeat(
        GodotObject defeated_unit,
        GodotObject batch = null
    )
    {
        GodotObject state = RtState();
        if (state == null || defeated_unit == null || GdInterop.GetBool(defeated_unit, "is_alive"))
        {
            return;
        }
        foreach (var unitValue in GdInterop.GetDictionary(state, "units").Values)
        {
            GodotObject candidate = unitValue.AsGodotObject();
            if (candidate == null || !GdInterop.GetBool(candidate, "is_alive"))
            {
                continue;
            }
            if (
                GdInterop.GetStringName(candidate, "unit_id")
                == GdInterop.GetStringName(defeated_unit, "unit_id")
            )
            {
                continue;
            }
            if (
                GdInterop.GetString(candidate, "faction_id")
                != GdInterop.GetString(defeated_unit, "faction_id")
            )
            {
                continue;
            }
            if (
                !LowLuckRelicRules.unit_has_flag(candidate, LowLuckRelicRules.ATTR_BLOOD_DEBT_SHAWL)
            )
            {
                continue;
            }
            candidate.Set(
                "current_ap",
                GdInterop.GetInt(candidate, "current_ap")
                    + LowLuckRelicRules.BLOOD_DEBT_ALLY_DOWN_AP_GAIN
            );
            _append_changed_unit_id(batch, GdInterop.GetStringName(candidate, "unit_id"));
            if (batch != null)
            {
                GdInterop
                    .GetArray(batch, "log_lines")
                    .Add(
                        $"{GdInterop.GetString(candidate, "display_name")} 目睹队友倒地，血债披肩返还 {LowLuckRelicRules.BLOOD_DEBT_ALLY_DOWN_AP_GAIN} 点行动点。"
                    );
            }
        }
    }

    public GArray _collect_adjacent_living_allies(GodotObject defeated_unit)
    {
        var adjacentAllies = new GArray();
        if (defeated_unit == null)
        {
            return adjacentAllies;
        }
        (defeated_unit as BattleUnitState)?.refresh_footprint();
        foreach (var unitValue in GdInterop.GetDictionary(RtState(), "units").Values)
        {
            GodotObject candidate = unitValue.AsGodotObject();
            if (candidate == null || !GdInterop.GetBool(candidate, "is_alive"))
            {
                continue;
            }
            if (
                GdInterop.GetStringName(candidate, "unit_id")
                == GdInterop.GetStringName(defeated_unit, "unit_id")
            )
            {
                continue;
            }
            if (
                GdInterop.GetString(candidate, "faction_id")
                    != GdInterop.GetString(defeated_unit, "faction_id")
                || GdInterop.IsEmpty(GdInterop.GetStringName(candidate, "source_member_id"))
            )
            {
                continue;
            }
            (candidate as BattleUnitState)?.refresh_footprint();
            if (_are_units_adjacent(candidate, defeated_unit))
            {
                adjacentAllies.Add(candidate);
            }
        }
        return adjacentAllies;
    }

    public bool _are_units_adjacent(GodotObject first_unit, GodotObject second_unit)
    {
        if (first_unit == null || second_unit == null)
        {
            return false;
        }
        foreach (var firstCoordValue in GdInterop.GetArray(first_unit, "occupied_coords"))
        {
            Vector2I firstCoord = firstCoordValue.AsVector2I();
            foreach (var secondCoordValue in GdInterop.GetArray(second_unit, "occupied_coords"))
            {
                Vector2I secondCoord = secondCoordValue.AsVector2I();
                if (
                    Math.Abs(firstCoord.X - secondCoord.X) + Math.Abs(firstCoord.Y - secondCoord.Y)
                    == 1
                )
                {
                    return true;
                }
            }
        }
        return false;
    }

    private GodotObject RtState()
    {
        return GdInterop.GetObject(_runtime, "_state");
    }

    private static GodotObject ResolveWeakRef(WeakReference<GodotObject> weakRef)
    {
        if (
            weakRef == null
            || !weakRef.TryGetTarget(out GodotObject target)
            || !GodotObject.IsInstanceValid(target)
        )
        {
            return null;
        }
        return target;
    }
}
