using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GCombatEffectArray = Godot.Collections.Array<CombatEffectDef>;
using GDictionary = Godot.Collections.Dictionary;

public readonly record struct BattleSpecialSkillResult(
    bool Applied,
    int MovedSteps,
    IReadOnlyList<StringName> StatusEffectIds,
    IReadOnlyList<string> LogLines
)
{
    public static BattleSpecialSkillResult Empty() =>
        new(false, 0, Array.Empty<StringName>(), Array.Empty<string>());

    public GDictionary ToDictionary()
    {
        var statusEffectIds = new GArray();
        foreach (StringName statusEffectId in StatusEffectIds ?? Array.Empty<StringName>())
        {
            statusEffectIds.Add(statusEffectId);
        }
        var logLines = new GArray();
        foreach (string logLine in LogLines ?? Array.Empty<string>())
        {
            logLines.Add(logLine);
        }
        return new GDictionary
        {
            ["applied"] = Applied,
            ["moved_steps"] = MovedSteps,
            ["status_effect_ids"] = statusEffectIds,
            ["log_lines"] = logLines,
        };
    }
}

// 翻译自 battle_special_skill_resolver.gd（2026-05-25，战斗特殊技能 C# 迁移）。
// 保留 snake_case 公开接口供 GDScript 调用。
[GlobalClass]
public partial class BattleSpecialSkillResolver : RefCounted
{
    private readonly record struct ForcedMoveStatusParameters(bool ForcedMoveImmune)
    {
        public static ForcedMoveStatusParameters FromStatus(BattleStatusEffectState statusEntry)
        {
            return new ForcedMoveStatusParameters(statusEntry?.forced_move_immune == true);
        }
    }

    private readonly record struct BodySizeOverrideResult(
        bool Applied,
        IReadOnlyList<StringName> StatusEffectIds,
        IReadOnlyList<string> LogLines
    )
    {
        public GDictionary ToDictionary()
        {
            var statusEffectIds = new GArray();
            foreach (StringName statusEffectId in StatusEffectIds ?? Array.Empty<StringName>())
            {
                statusEffectIds.Add(statusEffectId);
            }
            var logLines = new GArray();
            foreach (string logLine in LogLines ?? Array.Empty<string>())
            {
                logLines.Add(logLine);
            }
            return new GDictionary
            {
                ["applied"] = Applied,
                ["status_effect_ids"] = statusEffectIds,
                ["log_lines"] = logLines,
            };
        }
    }

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

    private WeakReference<BattleRuntimeModule> _runtimeRef;

    private BattleRuntimeModule _runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<BattleRuntimeModule>(value) : null;
    }

    public void setup(BattleRuntimeModule runtime)
    {
        _runtime = runtime;
    }

    public void dispose()
    {
        _runtime = null;
    }

    public bool _is_unit_valid_for_effect(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        StringName target_team_filter
    )
    {
        if (_runtime == null)
        {
            return false;
        }
        return _runtime._is_unit_valid_for_effect(
            source_unit,
            target_unit,
            target_team_filter
        );
    }

    public void _apply_skill_mastery_grant(
        BattleUnitState unit_state,
        GDictionary grant,
        BattleEventBatch batch
    )
    {
        if (_runtime == null)
        {
            return;
        }
        _runtime._apply_skill_mastery_grant(unit_state, grant, batch);
    }

    internal void ApplySkillMasteryGrantTyped(
        BattleUnitState unitState,
        BattleSkillMasteryGrant grant,
        BattleEventBatch batch
    )
    {
        if (_runtime == null)
        {
            return;
        }
        _runtime.ApplySkillMasteryGrantTyped(unitState, grant, batch);
    }

    public void _append_changed_coords(BattleEventBatch batch, GArray coords)
    {
        if (_runtime == null)
        {
            return;
        }
        _runtime._append_changed_coords(batch, coords);
    }

    public void _append_changed_unit_id(BattleEventBatch batch, StringName unit_id)
    {
        if (_runtime == null)
        {
            return;
        }
        _runtime._append_changed_unit_id(batch, unit_id);
    }

    public void _append_changed_unit_coords(BattleEventBatch batch, BattleUnitState unit_state)
    {
        if (_runtime == null)
        {
            return;
        }
        _runtime._append_changed_unit_coords(batch, unit_state);
    }

    public void _apply_on_kill_gain_resources_effects(
        BattleUnitState source_unit,
        BattleUnitState defeated_unit,
        SkillDef skill_def,
        GCombatEffectArray effect_defs,
        BattleEventBatch batch
    )
    {
        if (source_unit == null || defeated_unit == null || skill_def == null || batch == null)
        {
            return;
        }
        if (defeated_unit.is_alive)
        {
            return;
        }
        foreach (CombatEffectDef effectDef in effect_defs ?? new GCombatEffectArray())
        {
            if (
                effectDef == null
                || effectDef.effect_type != "on_kill_gain_resources"
            )
            {
                continue;
            }
            int apGain = Math.Max(effectDef.ap_gain, 0);
            int freeMovePointsGain = Math.Max(effectDef.free_move_points_gain, 0);
            if (apGain <= 0 && freeMovePointsGain <= 0)
            {
                continue;
            }
            if (apGain > 0)
            {
                source_unit.current_ap += apGain;
            }
            if (freeMovePointsGain > 0)
            {
                source_unit.current_move_points += freeMovePointsGain;
                source_unit.can_use_locked_move_points_this_turn = true;
            }
            _append_changed_unit_id(batch, source_unit.unit_id);
            var gainParts = new System.Collections.Generic.List<string>();
            if (apGain > 0)
            {
                gainParts.Add($"恢复 {apGain} AP");
            }
            if (freeMovePointsGain > 0)
            {
                gainParts.Add($"获得 {freeMovePointsGain} 点普通移动力并可在行动后移动");
            }
            string skillName = !string.IsNullOrEmpty(skill_def.display_name)
                ? skill_def.display_name
                : skill_def.skill_id.ToString();
            batch.log_lines.Add(
                $"{source_unit.display_name} 击倒 {defeated_unit.display_name}，触发 {skillName}：{string.Join("，", gainParts)}。"
            );
        }
    }

    public GDictionary _apply_unit_skill_special_effects(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        GCombatEffectArray effect_defs,
        BattleEventBatch batch,
        BattleForcedMoveContext forced_move_context = default
    )
    {
        return ApplyUnitSkillSpecialEffectsResult(
                active_unit,
                target_unit,
                skill_def,
                cast_variant,
                effect_defs,
                batch,
                forced_move_context
            )
            .ToDictionary();
    }

    public BattleSpecialSkillResult ApplyUnitSkillSpecialEffectsResult(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        GCombatEffectArray effect_defs,
        BattleEventBatch batch,
        BattleForcedMoveContext forced_move_context
    )
    {
        if (active_unit == null || skill_def == null)
        {
            return BattleSpecialSkillResult.Empty();
        }
        if (_is_black_star_brand_skill(skill_def.skill_id))
        {
            return ApplyBlackStarBrandEffectResult(active_unit, target_unit);
        }
        if (_is_doom_shift_skill(skill_def.skill_id))
        {
            return ApplyDoomShiftEffectResult(active_unit, target_unit, batch);
        }
        if (effect_defs == null || effect_defs.Count == 0)
        {
            return BattleSpecialSkillResult.Empty();
        }

        BattleLayeredBarrierService layeredBarrierService = _runtime._layered_barrier_service;
        var seenForcedMoveEffects = new HashSet<ulong>();
        bool applied = false;
        int maxMovedSteps = 0;
        var statusEffectIds = new List<StringName>();
        var logLines = new List<string>();
        foreach (CombatEffectDef effectDef in effect_defs)
        {
            if (effectDef == null)
            {
                continue;
            }
            StringName effectType = effectDef.effect_type;
            if (effectType == LAYERED_BARRIER_EFFECT_TYPE)
            {
                BattleLayeredBarrierApplyResult barrierResult =
                    layeredBarrierService != null
                        ? layeredBarrierService.ApplyLayeredBarrierEffectResult(
                            active_unit,
                            target_unit ?? active_unit,
                            skill_def,
                            effectDef,
                            batch
                        )
                        : BattleLayeredBarrierApplyResult.Empty();
                if (barrierResult.Applied)
                {
                    applied = true;
                }
                continue;
            }
            if (effectType == BODY_SIZE_CATEGORY_OVERRIDE_EFFECT_TYPE)
            {
                BodySizeOverrideResult bodySizeResult = _apply_body_size_category_override_effect_result(
                    active_unit,
                    target_unit ?? active_unit,
                    effectDef,
                    batch
                );
                if (bodySizeResult.Applied)
                {
                    applied = true;
                    foreach (StringName statusId in bodySizeResult.StatusEffectIds)
                    {
                        if (!statusEffectIds.Contains(statusId))
                        {
                            statusEffectIds.Add(statusId);
                        }
                    }
                    foreach (string logLine in bodySizeResult.LogLines)
                    {
                        logLines.Add(logLine);
                    }
                }
                continue;
            }
            if (effectType != "forced_move")
            {
                continue;
            }
            ulong forcedMoveInstanceId = effectDef.GetInstanceId();
            if (!seenForcedMoveEffects.Add(forcedMoveInstanceId))
            {
                continue;
            }
            int movedSteps = ApplyForcedMoveEffect(
                active_unit,
                target_unit ?? active_unit,
                effectDef,
                batch,
                forced_move_context
            );
            if (movedSteps > 0)
            {
                applied = true;
                maxMovedSteps = Math.Max(maxMovedSteps, movedSteps);
            }
        }
        return new BattleSpecialSkillResult(applied, maxMovedSteps, statusEffectIds, logLines);
    }

    public GDictionary _apply_doom_shift_effect(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        BattleEventBatch batch
    )
    {
        return ApplyDoomShiftEffectResult(active_unit, target_unit, batch).ToDictionary();
    }

    public BattleSpecialSkillResult ApplyDoomShiftEffectResult(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        BattleEventBatch batch
    )
    {
        if (RtState() == null || active_unit == null || target_unit == null)
        {
            return BattleSpecialSkillResult.Empty();
        }
        if (target_unit.unit_id == active_unit.unit_id)
        {
            return BattleSpecialSkillResult.Empty();
        }
        if (!_swap_unit_positions(active_unit, target_unit, batch))
        {
            return BattleSpecialSkillResult.Empty();
        }
        _set_runtime_status_effect(
            active_unit,
            STATUS_MARKED,
            DOOM_SHIFT_SELF_DEBUFF_DURATION_TU,
            active_unit.unit_id,
            1,
            counts_as_debuff_override: true,
            counts_as_debuff: true
        );
        _append_changed_unit_id(batch, active_unit.unit_id);
        return new BattleSpecialSkillResult(
            true,
            0,
            Array.Empty<StringName>(),
            new[] { $"{active_unit.display_name} 先承受 marked，再与 {target_unit.display_name} 交换位置。" }
        );
    }

    public bool _swap_unit_positions(
        BattleUnitState first_unit,
        BattleUnitState second_unit,
        BattleEventBatch batch
    )
    {
        BattleState state = RtState();
        BattleGridService gridService = _runtime.get_grid_service();
        if (state == null || first_unit == null || second_unit == null)
        {
            return false;
        }
        if (first_unit.unit_id == second_unit.unit_id)
        {
            return false;
        }
        GArray firstPreviousCoords = ToUntypedVector2IArray(first_unit.occupied_coords);
        GArray secondPreviousCoords = ToUntypedVector2IArray(second_unit.occupied_coords);
        Vector2I firstCoord = first_unit.coord;
        Vector2I secondCoord = second_unit.coord;
        if (!_resolve_swap_barrier_passage(first_unit, firstCoord, secondCoord, batch))
        {
            return false;
        }
        if (!_resolve_swap_barrier_passage(second_unit, secondCoord, firstCoord, batch))
        {
            return false;
        }
        gridService.clear_unit_occupancy(state, first_unit);
        gridService.clear_unit_occupancy(state, second_unit);
        bool canSwap =
            gridService.can_place_unit(state, first_unit, secondCoord, true)
            && gridService.can_place_unit(state, second_unit, firstCoord, true);
        if (!canSwap)
        {
            gridService.set_occupants(
                state,
                firstPreviousCoords,
                first_unit.unit_id
            );
            gridService.set_occupants(
                state,
                secondPreviousCoords,
                second_unit.unit_id
            );
            return false;
        }
        gridService.place_unit(state, first_unit, secondCoord, true);
        gridService.place_unit(state, second_unit, firstCoord, true);
        _append_changed_coords(batch, firstPreviousCoords);
        _append_changed_coords(batch, secondPreviousCoords);
        _append_changed_unit_coords(batch, first_unit);
        _append_changed_unit_coords(batch, second_unit);
        _append_changed_unit_id(batch, first_unit.unit_id);
        _append_changed_unit_id(batch, second_unit.unit_id);
        return true;
    }

    public bool _resolve_swap_barrier_passage(
        BattleUnitState unit_state,
        Vector2I from_coord,
        Vector2I to_coord,
        BattleEventBatch batch
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
        BattleBarrierInteractionResult barrierResult =
            layeredBarrierService.ResolveUnitBoundaryCrossingResult(
            unit_state,
            from_coord,
            to_coord,
            batch
        );
        return !barrierResult.Blocked && unit_state.is_alive && unit_state.coord == from_coord;
    }

    public GDictionary _apply_black_star_brand_effect(
        BattleUnitState active_unit,
        BattleUnitState target_unit
    )
    {
        return ApplyBlackStarBrandEffectResult(active_unit, target_unit).ToDictionary();
    }

    public BattleSpecialSkillResult ApplyBlackStarBrandEffectResult(
        BattleUnitState active_unit,
        BattleUnitState target_unit
    )
    {
        if (active_unit == null || target_unit == null)
        {
            return BattleSpecialSkillResult.Empty();
        }
        _clear_black_star_brand_statuses(target_unit);
        var statusEffectIds = new List<StringName>();
        var logLines = new List<string>();
        if (_is_black_star_brand_elite_target(target_unit))
        {
            _set_runtime_status_effect(
                target_unit,
                STATUS_BLACK_STAR_BRAND_ELITE,
                BLACK_STAR_BRAND_DURATION_TU,
                active_unit.unit_id
            );
            _set_runtime_status_effect(
                target_unit,
                STATUS_BLACK_STAR_BRAND_ELITE_GUARD_WINDOW,
                BLACK_STAR_BRAND_DURATION_TU,
                active_unit.unit_id
            );
            statusEffectIds.Add(STATUS_BLACK_STAR_BRAND_ELITE);
            statusEffectIds.Add(STATUS_BLACK_STAR_BRAND_ELITE_GUARD_WINDOW);
            logLines.Add($"{target_unit.display_name} 被施加黑星烙印：暴击失效、命中下降，且第一次受击会被穿透部分格挡。");
        }
        else
        {
            _set_runtime_status_effect(
                target_unit,
                STATUS_BLACK_STAR_BRAND_NORMAL,
                BLACK_STAR_BRAND_DURATION_TU,
                active_unit.unit_id
            );
            target_unit.erase_status_effect(STATUS_GUARDING);
            statusEffectIds.Add(STATUS_BLACK_STAR_BRAND_NORMAL);
            logLines.Add($"{target_unit.display_name} 被施加黑星烙印：无法反击，且无法进入格挡。");
        }
        return new BattleSpecialSkillResult(true, 0, statusEffectIds, logLines);
    }

    public void _set_runtime_status_effect(
        BattleUnitState unit_state,
        StringName status_id,
        int duration_tu,
        StringName source_unit_id = null,
        int power = 1,
        GDictionary status_params = null,
        bool counts_as_debuff_override = false,
        bool counts_as_debuff = false,
        bool lock_counterattack = false,
        bool lock_crit = false,
        int main_skill_lock_other_debuff_count = 0
    )
    {
        source_unit_id ??= new StringName("");
        status_params ??= new GDictionary();
        if (unit_state == null || IsEmpty(status_id))
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
            counts_as_debuff_override = counts_as_debuff_override,
            counts_as_debuff = counts_as_debuff,
            lock_counterattack = lock_counterattack,
            lock_crit = lock_crit,
            main_skill_lock_other_debuff_count = Math.Max(main_skill_lock_other_debuff_count, 0),
        };
        unit_state.set_status_effect(statusEntry);
    }

    public void _clear_black_star_brand_statuses(BattleUnitState unit_state)
    {
        if (unit_state == null)
        {
            return;
        }
        unit_state.erase_status_effect(STATUS_BLACK_STAR_BRAND_NORMAL);
        unit_state.erase_status_effect(STATUS_BLACK_STAR_BRAND_ELITE);
        unit_state.erase_status_effect(STATUS_BLACK_STAR_BRAND_ELITE_GUARD_WINDOW);
    }

    public bool _is_black_star_brand_elite_target(BattleUnitState unit_state)
    {
        return _is_elite_or_boss_target(unit_state);
    }

    public bool _is_elite_or_boss_target(BattleUnitState unit_state)
    {
        return BattleExecutionRules.is_elite_or_boss_target(unit_state);
    }

    public bool _is_boss_target(BattleUnitState unit_state)
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

    public void _clear_crown_break_seal_statuses(BattleUnitState unit_state)
    {
        if (unit_state == null)
        {
            return;
        }
        unit_state.erase_status_effect(STATUS_CROWN_BREAK_BROKEN_FANG);
        unit_state.erase_status_effect(STATUS_CROWN_BREAK_BROKEN_HAND);
        unit_state.erase_status_effect(STATUS_CROWN_BREAK_BLINDED_EYE);
    }

    public bool _is_crown_break_target_eligible(
        BattleUnitState active_unit,
        BattleUnitState target_unit
    )
    {
        return target_unit != null
            && _is_unit_valid_for_effect(active_unit, target_unit, "enemy")
            && target_unit.has_status_effect(STATUS_BLACK_STAR_BRAND_ELITE);
    }

    public bool _is_crown_break_skill(StringName skill_id)
    {
        return ProgressionDataUtils.to_string_name(skill_id) == CROWN_BREAK_SKILL_ID;
    }

    public bool _is_doom_sentence_target_eligible(
        BattleUnitState active_unit,
        BattleUnitState target_unit
    )
    {
        return target_unit != null
            && _is_unit_valid_for_effect(active_unit, target_unit, "enemy")
            && _is_elite_or_boss_target(target_unit);
    }

    public bool _is_black_crown_seal_target_eligible(
        BattleUnitState active_unit,
        BattleUnitState target_unit
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
        BattleUnitState source_unit,
        BattleUnitState unit_state,
        CombatEffectDef effect_def,
        BattleEventBatch batch,
        BattleForcedMoveContext forced_move_context = default
    )
    {
        return ApplyForcedMoveEffect(
            source_unit,
            unit_state,
            effect_def,
            batch,
            forced_move_context
        );
    }

    public int ApplyForcedMoveEffect(
        BattleUnitState sourceUnit,
        BattleUnitState unitState,
        CombatEffectDef effectDef,
        BattleEventBatch eventBatch,
        BattleForcedMoveContext forcedMoveContext
    )
    {
        BattleState state = RtState();
        BattleGridService gridService = _runtime.get_grid_service();
        if (state == null || unitState == null || effectDef == null)
        {
            return 0;
        }
        int moveDistance = Math.Max(effectDef.forced_move_distance, 0);
        if (moveDistance <= 0)
        {
            return 0;
        }
        if (_blocks_enemy_forced_move(sourceUnit, unitState))
        {
            if (eventBatch != null)
            {
                eventBatch.log_lines.Add($"{unitState.display_name} 稳如金刚，未被强制位移。");
            }
            return 0;
        }

        StringName mode = effectDef.forced_move_mode;
        if (IsEmpty(mode))
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
            Vector2I nextCoord = PickForcedMoveCoord(
                unitState,
                mode,
                sourceUnit,
                forcedMoveContext
            );
            if (
                nextCoord == new Vector2I(-1, -1)
                || nextCoord == unitState.coord
            )
            {
                break;
            }
            if (
                !gridService.can_traverse(
                    state,
                    unitState.coord,
                    nextCoord,
                    unitState
                )
            )
            {
                break;
            }
            BattleBarrierInteractionResult barrierResult =
                layeredBarrierService != null
                    ? layeredBarrierService.ResolveUnitBoundaryCrossingResult(
                        unitState,
                        unitState.coord,
                        nextCoord,
                        eventBatch
                    )
                    : new BattleBarrierInteractionResult(false, false);
            if (
                barrierResult.Blocked
                || !unitState.is_alive
            )
            {
                break;
            }
            GArray previousCoords = ToUntypedVector2IArray(unitState.occupied_coords);
            if (!gridService.move_unit(state, unitState, nextCoord))
            {
                break;
            }
            movedSteps += 1;
            _append_changed_coords(eventBatch, previousCoords);
            _append_changed_unit_coords(eventBatch, unitState);
            _append_changed_unit_id(eventBatch, unitState.unit_id);
        }
        return movedSteps;
    }

    public GDictionary _apply_body_size_category_override_effect(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        CombatEffectDef effect_def,
        BattleEventBatch batch
    )
    {
        return _apply_body_size_category_override_effect_result(
            source_unit,
            target_unit,
            effect_def,
            batch
        ).ToDictionary();
    }

    private BodySizeOverrideResult _apply_body_size_category_override_effect_result(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        CombatEffectDef effect_def,
        BattleEventBatch batch
    )
    {
        BodySizeOverrideResult result = new(
            false,
            Array.Empty<StringName>(),
            Array.Empty<string>()
        );
        BattleState state = RtState();
        BattleGridService gridService = _runtime.get_grid_service();
        if (state == null || target_unit == null || effect_def == null)
        {
            return result;
        }
        StringName statusId = ProgressionDataUtils.to_string_name(effect_def.status_id);
        StringName targetCategory = ProgressionDataUtils.to_string_name(
            effect_def.body_size_category
        );
        if (
            IsEmpty(statusId)
            || !BodySizeContentRules.IsValidBodySizeCategory(targetCategory)
        )
        {
            return result;
        }
        int durationTu = Math.Max(effect_def.duration_tu, 0);
        if (durationTu <= 0)
        {
            return result;
        }

        var existingEntry = target_unit.get_status_effect(statusId);
        StringName restoreCategory = target_unit.body_size_category;
        if (existingEntry != null && existingEntry.@params != null)
        {
            StringName existingRestoreCategory = ProgressionDataUtils.to_string_name(
                existingEntry.@params.GetValueOrDefault(
                    STATUS_PARAM_PREVIOUS_BODY_SIZE_CATEGORY,
                    ""
                )
            );
            if (BodySizeContentRules.IsValidBodySizeCategory(existingRestoreCategory))
            {
                restoreCategory = existingRestoreCategory;
            }
        }

        StringName previousCategory = target_unit.body_size_category;
        int previousBodySize = target_unit.body_size;
        Vector2I previousFootprint = target_unit.footprint_size;
        Godot.Collections.Array<Vector2I> previousOccupiedCoords = DuplicateVector2IArray(
            target_unit.occupied_coords
        );
        GArray previousCoords = ToUntypedVector2IArray(target_unit.occupied_coords);
        gridService.clear_unit_occupancy(state, target_unit);
        target_unit.set_body_size_category(targetCategory);
        if (
            !gridService.can_place_footprint(
                state,
                target_unit.coord,
                target_unit.footprint_size,
                target_unit.unit_id,
                target_unit
            )
        )
        {
            target_unit.body_size_category = previousCategory;
            target_unit.body_size = previousBodySize;
            target_unit.footprint_size = previousFootprint;
            target_unit.occupied_coords = previousOccupiedCoords;
            gridService.set_occupants(
                state,
                previousCoords,
                target_unit.unit_id
            );
            return result with
            {
                LogLines = new[]
                {
                    $"{target_unit.display_name} 周围空间不足，无法改变体型。",
                },
            };
        }
        gridService.set_occupants(
            state,
            ToUntypedVector2IArray(target_unit.occupied_coords),
            target_unit.unit_id
        );

        GDictionary statusParams = (GDictionary)
            (effect_def.@params ?? new GDictionary()).Duplicate(true);
        statusParams[STATUS_PARAM_BODY_SIZE_CATEGORY_OVERRIDE] = targetCategory.ToString();
        statusParams[STATUS_PARAM_PREVIOUS_BODY_SIZE_CATEGORY] = restoreCategory.ToString();
        _set_runtime_status_effect(
            target_unit,
            statusId,
            durationTu,
            source_unit != null ? source_unit.unit_id : new StringName(""),
            Math.Max(effect_def.power, 1),
            statusParams
        );
        _append_changed_coords(batch, previousCoords);
        _append_changed_unit_coords(batch, target_unit);
        _append_changed_unit_id(batch, target_unit.unit_id);
        return new BodySizeOverrideResult(
            true,
            new[] { statusId },
            new[] { $"{target_unit.display_name} 的体型临时变为 {targetCategory}。" }
        );
    }

    public bool _blocks_enemy_forced_move(BattleUnitState source_unit, BattleUnitState target_unit)
    {
        if (source_unit == null || target_unit == null)
        {
            return false;
        }
        if (source_unit.unit_id == target_unit.unit_id)
        {
            return false;
        }
        if (source_unit.faction_id == target_unit.faction_id)
        {
            return false;
        }
        BattleStatusEffectState statusEntry = target_unit.get_status_effect(STATUS_VAJRA_BODY);
        if (statusEntry == null)
        {
            return false;
        }
        return ForcedMoveStatusParameters.FromStatus(statusEntry).ForcedMoveImmune;
    }

    public void _record_vajra_body_mastery_from_incoming_damage(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        GDictionary result,
        BattleEventBatch batch = null
    )
    {
        BattleSkillMasteryService skillMasteryService = _runtime._skill_mastery_service;
        var grant = skillMasteryService.build_vajra_body_mastery_grant(
            source_unit,
            target_unit,
            skill_def,
            result,
            _runtime.get_skill_defs()
        );
        _apply_skill_mastery_grant(target_unit, grant, batch);
    }

    internal void RecordVajraBodyMasteryFromIncomingDamageTyped(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDef skillDef,
        AttackEffectResolutionResult result,
        BattleEventBatch batch = null
    )
    {
        BattleSkillMasteryService skillMasteryService = _runtime?._skill_mastery_service;
        if (skillMasteryService == null)
            return;
        BattleSkillMasteryGrant grant = skillMasteryService.BuildVajraBodyMasteryGrantTyped(
            sourceUnit,
            targetUnit,
            skillDef,
            result,
            _runtime.get_skill_defs()
        );
        ApplySkillMasteryGrantTyped(targetUnit, grant, batch);
    }

    public Vector2I _pick_forced_move_coord(
        BattleUnitState unit_state,
        StringName mode,
        BattleUnitState source_unit = null,
        BattleForcedMoveContext forced_move_context = default
    )
    {
        return PickForcedMoveCoord(
            unit_state,
            mode,
            source_unit,
            forced_move_context
        );
    }

    public Vector2I PickForcedMoveCoord(
        BattleUnitState unit_state,
        StringName mode,
        BattleUnitState source_unit,
        BattleForcedMoveContext forced_move_context
    )
    {
        BattleState state = RtState();
        BattleGridService gridService = _runtime.get_grid_service();
        if (state == null || unit_state == null)
        {
            return new Vector2I(-1, -1);
        }
        unit_state.refresh_footprint();
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
            Vector2I candidateCoord = unit_state.coord + direction;
            if (
                !gridService.can_traverse(
                    state,
                    unit_state.coord,
                    candidateCoord,
                    unit_state
                )
            )
            {
                continue;
            }
            int candidateScore = ScoreForcedMoveCoord(
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
        BattleUnitState unit_state,
        Vector2I candidate_coord,
        StringName mode,
        BattleUnitState source_unit = null,
        BattleForcedMoveContext forced_move_context = default
    )
    {
        return ScoreForcedMoveCoord(
            unit_state,
            candidate_coord,
            mode,
            source_unit,
            forced_move_context
        );
    }

    public int ScoreForcedMoveCoord(
        BattleUnitState unit_state,
        Vector2I candidate_coord,
        StringName mode,
        BattleUnitState source_unit,
        BattleForcedMoveContext forced_move_context
    )
    {
        BattleState state = RtState();
        BattleGridService gridService = _runtime.get_grid_service();
        if (state == null || unit_state == null)
        {
            return FORCED_MOVE_INVALID_SCORE;
        }
        if (mode == "wind_push")
        {
            return ScoreWindPushCoord(
                unit_state,
                candidate_coord,
                source_unit,
                forced_move_context
            );
        }
        List<BattleUnitState> hostileUnits = CollectHostileUnitsFor(unit_state);
        int closestHostileDistance = 0;
        if (hostileUnits.Count != 0)
        {
            closestHostileDistance = 999999;
            foreach (BattleUnitState hostileUnit in hostileUnits)
            {
                closestHostileDistance = Math.Min(
                    closestHostileDistance,
                    gridService.get_distance(candidate_coord, hostileUnit.coord)
                );
            }
        }
        int score = closestHostileDistance * 100;
        score -= gridService.get_distance(unit_state.coord, candidate_coord) * 10;
        score -= candidate_coord.Y * 2 + candidate_coord.X;
        if (mode == "evasive")
        {
            score += 5;
        }
        return score;
    }

    public int _score_wind_push_coord(
        BattleUnitState unit_state,
        Vector2I candidate_coord,
        BattleUnitState source_unit,
        BattleForcedMoveContext forced_move_context = default
    )
    {
        return ScoreWindPushCoord(
            unit_state,
            candidate_coord,
            source_unit,
            forced_move_context
        );
    }

    public int ScoreWindPushCoord(
        BattleUnitState unit_state,
        Vector2I candidate_coord,
        BattleUnitState source_unit,
        BattleForcedMoveContext forced_move_context
    )
    {
        Vector2I pushDirection = ResolveForcedMoveDirection(
            unit_state,
            source_unit,
            forced_move_context
        );
        if (pushDirection == Vector2I.Zero)
        {
            return FORCED_MOVE_INVALID_SCORE;
        }
        Vector2I stepDelta = candidate_coord - unit_state.coord;
        if (_dot_vector2i(stepDelta, pushDirection) <= 0)
        {
            return FORCED_MOVE_INVALID_SCORE;
        }
        int currentProjection = _dot_vector2i(unit_state.coord, pushDirection);
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
        BattleUnitState unit_state,
        BattleUnitState source_unit,
        BattleForcedMoveContext forced_move_context = default
    )
    {
        return ResolveForcedMoveDirection(
            unit_state,
            source_unit,
            forced_move_context
        );
    }

    public Vector2I ResolveForcedMoveDirection(
        BattleUnitState unit_state,
        BattleUnitState source_unit,
        BattleForcedMoveContext forced_move_context
    )
    {
        Vector2I contextDirection = forced_move_context.Direction;
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
                unit_state.coord - source_unit.coord
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

    public GArray _collect_hostile_units_for(BattleUnitState unit_state)
    {
        var hostileUnits = new GArray();
        foreach (BattleUnitState unitState in CollectHostileUnitsFor(unit_state))
        {
            hostileUnits.Add(unitState);
        }
        return hostileUnits;
    }

    internal List<BattleUnitState> CollectHostileUnitsFor(BattleUnitState unit_state)
    {
        var hostileUnits = new List<BattleUnitState>();
        BattleState state = RtState();
        if (state == null || unit_state == null)
        {
            return hostileUnits;
        }
        foreach (BattleUnitState otherUnit in state.GetUnitsTyped())
        {
            if (
                otherUnit == null
                || otherUnit.unit_id == unit_state.unit_id
                || !otherUnit.is_alive
            )
            {
                continue;
            }
            if (otherUnit.faction_id == unit_state.faction_id)
            {
                continue;
            }
            hostileUnits.Add(otherUnit);
        }
        return hostileUnits;
    }

    public void _handle_adjacent_ally_defeat(BattleUnitState defeated_unit)
    {
        if (RtState() == null || defeated_unit == null)
        {
            return;
        }
        if (
            defeated_unit.is_alive
            || IsEmpty(defeated_unit.source_member_id)
        )
        {
            return;
        }
        if (_runtime == null)
        {
            return;
        }
        List<BattleUnitState> adjacentAllies = CollectAdjacentLivingAllies(defeated_unit);
        if (adjacentAllies.Count == 0)
        {
            return;
        }
        _runtime.handle_misfortune_trigger(
            CALAMITY_REASON_ADJACENT_ALLY_DEFEATED,
            new GDictionary
            {
                ["defeated_unit"] = defeated_unit,
                ["adjacent_units"] = ToUntypedUnitArray(adjacentAllies),
            }
        );
    }

    public void _handle_low_luck_relic_ally_defeat(
        BattleUnitState defeated_unit,
        BattleEventBatch batch = null
    )
    {
        BattleState state = RtState();
        if (state == null || defeated_unit == null || defeated_unit.is_alive)
        {
            return;
        }
        foreach (BattleUnitState candidate in state.GetUnitsTyped())
        {
            if (candidate == null || !candidate.is_alive)
            {
                continue;
            }
            if (candidate.unit_id == defeated_unit.unit_id)
            {
                continue;
            }
            if (candidate.faction_id != defeated_unit.faction_id)
            {
                continue;
            }
            if (
                !LowLuckRelicRules.SnapshotHasFlag(
                    candidate.attribute_snapshot,
                    LowLuckRelicRules.ATTR_BLOOD_DEBT_SHAWL
                )
            )
            {
                continue;
            }
            candidate.current_ap += LowLuckRelicRules.BLOOD_DEBT_ALLY_DOWN_AP_GAIN;
            _append_changed_unit_id(batch, candidate.unit_id);
            if (batch != null)
            {
                batch.log_lines.Add(
                    $"{candidate.display_name} 目睹队友倒地，血债披肩返还 {LowLuckRelicRules.BLOOD_DEBT_ALLY_DOWN_AP_GAIN} 点行动点。"
                );
            }
        }
    }

    public GArray _collect_adjacent_living_allies(BattleUnitState defeated_unit)
    {
        var adjacentAllies = new GArray();
        foreach (BattleUnitState unitState in CollectAdjacentLivingAllies(defeated_unit))
        {
            adjacentAllies.Add(unitState);
        }
        return adjacentAllies;
    }

    internal List<BattleUnitState> CollectAdjacentLivingAllies(BattleUnitState defeated_unit)
    {
        var adjacentAllies = new List<BattleUnitState>();
        if (defeated_unit == null)
        {
            return adjacentAllies;
        }
        BattleState state = RtState();
        if (state == null)
        {
            return adjacentAllies;
        }
        defeated_unit.refresh_footprint();
        foreach (BattleUnitState candidate in state.GetUnitsTyped())
        {
            if (candidate == null || !candidate.is_alive)
            {
                continue;
            }
            if (candidate.unit_id == defeated_unit.unit_id)
            {
                continue;
            }
            if (
                candidate.faction_id != defeated_unit.faction_id
                || IsEmpty(candidate.source_member_id)
            )
            {
                continue;
            }
            candidate.refresh_footprint();
            if (_are_units_adjacent(candidate, defeated_unit))
            {
                adjacentAllies.Add(candidate);
            }
        }
        return adjacentAllies;
    }

    public bool _are_units_adjacent(BattleUnitState first_unit, BattleUnitState second_unit)
    {
        if (first_unit == null || second_unit == null)
        {
            return false;
        }
        foreach (Vector2I firstCoord in first_unit.occupied_coords)
        {
            foreach (Vector2I secondCoord in second_unit.occupied_coords)
            {
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

    private static GArray ToUntypedVector2IArray(Godot.Collections.Array<Vector2I> coords)
    {
        var result = new GArray();
        if (coords == null)
        {
            return result;
        }
        foreach (Vector2I coord in coords)
        {
            result.Add(coord);
        }
        return result;
    }

    private static Godot.Collections.Array<Vector2I> DuplicateVector2IArray(
        Godot.Collections.Array<Vector2I> coords
    )
    {
        var result = new Godot.Collections.Array<Vector2I>();
        foreach (Vector2I coord in coords ?? new Godot.Collections.Array<Vector2I>())
        {
            result.Add(coord);
        }
        return result;
    }

    private static GArray ToUntypedUnitArray(IEnumerable<BattleUnitState> units)
    {
        var result = new GArray();
        foreach (BattleUnitState unit in units ?? Array.Empty<BattleUnitState>())
        {
            if (unit != null)
            {
                result.Add(unit);
            }
        }
        return result;
    }

    private static bool IsEmpty(StringName value) => value == null || value == "";

    private BattleState RtState()
    {
        return _runtime?._state;
    }

    private static BattleRuntimeModule ResolveWeakRef(WeakReference<BattleRuntimeModule> weakRef)
    {
        if (weakRef == null || !weakRef.TryGetTarget(out BattleRuntimeModule target))
        {
            return null;
        }
        return target;
    }
}
