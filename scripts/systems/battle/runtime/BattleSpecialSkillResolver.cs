using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Godot;
using GArray = Godot.Collections.Array;
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
}

// 翻译自 battle_special_skill_resolver.gd（2026-05-25，战斗特殊技能 C# 迁移）。
public class BattleSpecialSkillResolver
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
    }

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
        MisfortuneService.ToStringName(MisfortuneSkillKind.BlackCrownSeal);
    private static readonly StringName BLACK_STAR_BRAND_SKILL_ID =
        MisfortuneService.ToStringName(MisfortuneSkillKind.BlackStarBrand);
    private static readonly StringName CROWN_BREAK_SKILL_ID =
        MisfortuneService.ToStringName(MisfortuneSkillKind.CrownBreak);
    private static readonly StringName DOOM_SENTENCE_SKILL_ID =
        MisfortuneService.ToStringName(MisfortuneSkillKind.DoomSentence);
    private const int BLACK_STAR_BRAND_DURATION_TU = 60;
    private const int DOOM_SHIFT_SELF_DEBUFF_DURATION_TU = 60;

    private const int FORCED_MOVE_INVALID_SCORE = -999999;

    private WeakReference<BattleRuntimeModule> _runtimeRef;

    private BattleRuntimeModule _runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<BattleRuntimeModule>(value) : null;
    }

    internal void Setup(BattleRuntimeModule runtime)
    {
        _runtime = runtime;
    }

    internal void Dispose()
    {
        _runtime = null;
    }

    private bool IsUnitValidForEffect(
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

    private void ApplySkillMasteryGrant(
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

    private void AppendChangedCoords(BattleEventBatch batch, IEnumerable<Vector2I> coords)
    {
        if (_runtime == null)
        {
            return;
        }
        _runtime._append_changed_coords_typed(batch, coords);
    }

    private void AppendChangedUnitId(BattleEventBatch batch, StringName unit_id)
    {
        if (_runtime == null)
        {
            return;
        }
        _runtime._append_changed_unit_id(batch, unit_id);
    }

    private void AppendChangedUnitCoords(BattleEventBatch batch, BattleUnitState unit_state)
    {
        if (_runtime == null)
        {
            return;
        }
        _runtime._append_changed_unit_coords(batch, unit_state);
    }

    public void ApplyOnKillGainResourcesEffects(
        BattleUnitState source_unit,
        BattleUnitState defeated_unit,
        SkillDefinition skillDefinition,
        IEnumerable<CombatEffectDefinition> effectDefinitions,
        BattleEventBatch batch
    )
    {
        if (source_unit == null || defeated_unit == null || skillDefinition == null || batch == null)
        {
            return;
        }
        if (defeated_unit.is_alive)
        {
            return;
        }
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effectDefinition == null
                || effectDefinition.EffectKind != BattleEffectKind.OnKillGainResources
            )
            {
                continue;
            }
            int apGain = Math.Max(effectDefinition.ApGain, 0);
            int freeMovePointsGain = Math.Max(effectDefinition.FreeMovePointsGain, 0);
            if (apGain <= 0 && freeMovePointsGain <= 0)
            {
                continue;
            }
            if (apGain > 0)
            {
                source_unit.SetCurrentAp(source_unit.current_ap + apGain);
            }
            if (freeMovePointsGain > 0)
            {
                source_unit.SetCurrentMovePoints(
                    source_unit.current_move_points + freeMovePointsGain
                );
                source_unit.can_use_locked_move_points_this_turn = true;
            }
            AppendChangedUnitId(batch, source_unit.unit_id);
            var gainParts = new List<string>();
            if (apGain > 0)
            {
                gainParts.Add($"恢复 {apGain} AP");
            }
            if (freeMovePointsGain > 0)
            {
                gainParts.Add($"获得 {freeMovePointsGain} 点普通移动力并可在行动后移动");
            }
            string skillName = !string.IsNullOrEmpty(skillDefinition.DisplayName)
                ? skillDefinition.DisplayName
                : skillDefinition.SkillId.ToString();
            batch.AddLogLine(
                $"{source_unit.display_name} 击倒 {defeated_unit.display_name}，触发 {skillName}：{string.Join("，", gainParts)}。"
            );
        }
    }

    public BattleSpecialSkillResult ApplyUnitSkillSpecialEffectsResult(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        IEnumerable<CombatEffectDefinition> effectDefinitions,
        BattleEventBatch batch,
        BattleForcedMoveContext forced_move_context
    )
    {
        if (active_unit == null || skillDefinition == null)
        {
            return BattleSpecialSkillResult.Empty();
        }
        if (IsBlackStarBrandSkill(skillDefinition.SkillId))
        {
            return ApplyBlackStarBrandEffectResult(active_unit, target_unit);
        }
        if (IsDoomShiftSkill(skillDefinition.SkillId))
        {
            return ApplyDoomShiftEffectResult(active_unit, target_unit, batch);
        }
        if (effectDefinitions == null)
        {
            return BattleSpecialSkillResult.Empty();
        }

        BattleLayeredBarrierService layeredBarrierService = _runtime._layered_barrier_service;
        var seenForcedMoveEffects = new HashSet<long>();
        bool applied = false;
        int maxMovedSteps = 0;
        var statusEffectIds = new List<StringName>();
        var logLines = new List<string>();
        foreach (CombatEffectDefinition effectDefinition in effectDefinitions)
        {
            if (effectDefinition == null)
            {
                continue;
            }
            BattleEffectKind effectKind = effectDefinition.EffectKind;
            if (effectKind == BattleEffectKind.LayeredBarrier)
            {
                BattleLayeredBarrierApplyResult barrierResult =
                    layeredBarrierService != null
                        ? layeredBarrierService.ApplyLayeredBarrierEffectResult(
                            active_unit,
                            target_unit ?? active_unit,
                            skillDefinition,
                            effectDefinition,
                            batch
                        )
                        : BattleLayeredBarrierApplyResult.Empty();
                if (barrierResult.Applied)
                {
                    applied = true;
                }
                continue;
            }
            if (effectKind == BattleEffectKind.BodySizeCategoryOverride)
            {
                BodySizeOverrideResult bodySizeResult = ApplyBodySizeCategoryOverrideEffectResult(
                    active_unit,
                    target_unit ?? active_unit,
                    effectDefinition,
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
            if (effectKind != BattleEffectKind.ForcedMove)
            {
                continue;
            }
            long forcedMoveInstanceId = RuntimeHelpers.GetHashCode(effectDefinition);
            if (!seenForcedMoveEffects.Add(forcedMoveInstanceId))
            {
                continue;
            }
            int movedSteps = ApplyForcedMoveEffect(
                active_unit,
                target_unit ?? active_unit,
                effectDefinition,
                batch,
                forced_move_context,
                BattleSaveContext.ForSkill(skillDefinition.SkillId)
            );
            if (movedSteps > 0)
            {
                applied = true;
                maxMovedSteps = Math.Max(maxMovedSteps, movedSteps);
            }
        }
        return new BattleSpecialSkillResult(applied, maxMovedSteps, statusEffectIds, logLines);
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
        if (!SwapUnitPositions(active_unit, target_unit, batch))
        {
            return BattleSpecialSkillResult.Empty();
        }
        SetRuntimeDebuffStatusEffect(
            active_unit,
            STATUS_MARKED,
            DOOM_SHIFT_SELF_DEBUFF_DURATION_TU,
            active_unit.unit_id,
            1
        );
        AppendChangedUnitId(batch, active_unit.unit_id);
        return new BattleSpecialSkillResult(
            true,
            0,
            Array.Empty<StringName>(),
            new[] { $"{active_unit.display_name} 先承受 marked，再与 {target_unit.display_name} 交换位置。" }
        );
    }

    public bool SwapUnitPositions(
        BattleUnitState first_unit,
        BattleUnitState second_unit,
        BattleEventBatch batch
    )
    {
        BattleState state = RtState();
        BattleGridService gridService = _runtime.GetGridService();
        if (state == null || first_unit == null || second_unit == null)
        {
            return false;
        }
        if (first_unit.unit_id == second_unit.unit_id)
        {
            return false;
        }
        var firstPreviousCoords = DuplicateVector2IArray(first_unit.occupied_coords);
        var secondPreviousCoords = DuplicateVector2IArray(second_unit.occupied_coords);
        Vector2I firstCoord = first_unit.coord;
        Vector2I secondCoord = second_unit.coord;
        if (!ResolveSwapBarrierPassage(first_unit, firstCoord, secondCoord, batch))
        {
            return false;
        }
        if (!ResolveSwapBarrierPassage(second_unit, secondCoord, firstCoord, batch))
        {
            return false;
        }
        gridService.ClearUnitOccupancy(state, first_unit);
        gridService.ClearUnitOccupancy(state, second_unit);
        bool canSwap =
            gridService.CanPlaceUnit(state, first_unit, secondCoord, true)
            && gridService.CanPlaceUnit(state, second_unit, firstCoord, true);
        if (!canSwap)
        {
            gridService.SetOccupantsTyped(state, firstPreviousCoords, first_unit.unit_id);
            gridService.SetOccupantsTyped(state, secondPreviousCoords, second_unit.unit_id);
            return false;
        }
        gridService.PlaceUnit(state, first_unit, secondCoord, true);
        gridService.PlaceUnit(state, second_unit, firstCoord, true);
        AppendChangedCoords(batch, firstPreviousCoords);
        AppendChangedCoords(batch, secondPreviousCoords);
        AppendChangedUnitCoords(batch, first_unit);
        AppendChangedUnitCoords(batch, second_unit);
        AppendChangedUnitId(batch, first_unit.unit_id);
        AppendChangedUnitId(batch, second_unit.unit_id);
        return true;
    }

    private bool ResolveSwapBarrierPassage(
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

    public BattleSpecialSkillResult ApplyBlackStarBrandEffectResult(
        BattleUnitState active_unit,
        BattleUnitState target_unit
    )
    {
        if (active_unit == null || target_unit == null)
        {
            return BattleSpecialSkillResult.Empty();
        }
        ClearBlackStarBrandStatuses(target_unit);
        var statusEffectIds = new List<StringName>();
        var logLines = new List<string>();
        if (IsBlackStarBrandEliteTarget(target_unit))
        {
            SetRuntimeSourceStatusEffect(
                target_unit,
                STATUS_BLACK_STAR_BRAND_ELITE,
                BLACK_STAR_BRAND_DURATION_TU,
                active_unit.unit_id
            );
            SetRuntimeSourceStatusEffect(
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
            SetRuntimeSourceStatusEffect(
                target_unit,
                STATUS_BLACK_STAR_BRAND_NORMAL,
                BLACK_STAR_BRAND_DURATION_TU,
                active_unit.unit_id
            );
            target_unit.EraseStatusEffect(STATUS_GUARDING);
            statusEffectIds.Add(STATUS_BLACK_STAR_BRAND_NORMAL);
            logLines.Add($"{target_unit.display_name} 被施加黑星烙印：无法反击，且无法进入格挡。");
        }
        return new BattleSpecialSkillResult(true, 0, statusEffectIds, logLines);
    }

    public void SetRuntimeDebuffStatusEffect(
        BattleUnitState unit_state,
        StringName status_id,
        int duration_tu,
        StringName source_unit_id = null,
        int power = 1,
        GDictionary status_params = null
    )
    {
        SetRuntimeStatusEffect(
            unit_state,
            status_id,
            duration_tu,
            source_unit_id,
            power,
            status_params,
            counts_as_debuff_override: true,
            counts_as_debuff: true
        );
    }

    public void SetRuntimeBodySizeOverrideStatusEffect(
        BattleUnitState unit_state,
        StringName status_id,
        int duration_tu,
        StringName source_unit_id = null,
        int power = 1,
        GDictionary status_params = null,
        StringName body_size_category_override = default,
        StringName previous_body_size_category = default
    )
    {
        SetRuntimeStatusEffect(
            unit_state,
            status_id,
            duration_tu,
            source_unit_id,
            power,
            status_params,
            body_size_category_override: body_size_category_override,
            previous_body_size_category: previous_body_size_category
        );
    }

    public void SetRuntimeSourceStatusEffect(
        BattleUnitState unit_state,
        StringName status_id,
        int duration_tu,
        StringName source_unit_id,
        int power = 1,
        GDictionary status_params = null
    )
    {
        SetRuntimeStatusEffect(
            unit_state,
            status_id,
            duration_tu,
            source_unit_id,
            power,
            status_params
        );
    }

    public void SetRuntimeBarrierStatusEffect(
        BattleUnitState unit_state,
        StringName status_id,
        StringName source_unit_id,
        StringName source_profile_id,
        StringName source_layer_id,
        int self_save_dc,
        StringName self_save_ability,
        StringName self_save_tag,
        int power = 1,
        GDictionary status_params = null
    )
    {
        SetRuntimeStatusEffect(
            unit_state,
            status_id,
            -1,
            source_unit_id,
            power,
            status_params,
            source_profile_id: source_profile_id,
            source_layer_id: source_layer_id,
            self_save_dc: self_save_dc,
            self_save_ability: self_save_ability,
            self_save_tag: self_save_tag,
            counts_as_debuff_override: true,
            counts_as_debuff: true
        );
    }

    public void SetRuntimeStatusEffect(
        BattleUnitState unit_state,
        StringName status_id,
        int duration_tu,
        StringName source_unit_id = null,
        int power = 1,
        GDictionary status_params = null,
        StringName source_profile_id = default,
        StringName source_layer_id = default,
        StringName source_skill_id = default,
        int? source_skill_level = null,
        int self_save_dc = 0,
        StringName self_save_ability = default,
        StringName self_save_tag = default,
        int? self_save_roll_override = null,
        int save_bonus = 0,
        int control_save_bonus = 0,
        int range_bonus = 0,
        int passive_reduction = 0,
        int content_dr = 0,
        int guard_block = 0,
        IReadOnlyList<StringName> save_advantage_tags = null,
        IReadOnlyList<StringName> save_disadvantage_tags = null,
        IReadOnlyList<StringName> save_immunity_tags = null,
        int? heal_multiplier_percent = null,
        int? shield_gain_multiplier_percent = null,
        double? incoming_damage_multiplier = null,
        double? outgoing_damage_multiplier = null,
        StringName damage_tag = default,
        IReadOnlyList<StringName> damage_tags = null,
        StringName damage_category = default,
        int attack_roll_penalty = -1,
        bool undispellable = false,
        bool dispellable_magic = false,
        bool dispellable_harmful_magic = false,
        bool dispellable_beneficial_magic = false,
        StringName mitigation_tier = default,
        StringName dr_bypass_tag = default,
        bool counts_as_debuff_override = false,
        bool counts_as_debuff = false,
        bool forced_move_immune = false,
        bool lock_counterattack = false,
        bool lock_guard = false,
        bool lock_dodge_bonus = false,
        bool lock_crit = false,
        int main_skill_lock_other_debuff_count = 0,
        StringName stack_behavior = default,
        int stack_limit = 0,
        StringName body_size_category_override = default,
        StringName previous_body_size_category = default
    )
    {
        source_unit_id ??= new StringName("");
        status_params ??= new GDictionary();
        if (unit_state == null || IsEmpty(status_id))
        {
            return;
        }
        BattleStatusEffectParams typedStatusParams =
            BattleStatusEffectParams.FromDictionary(status_params);
        var statusEntry = new BattleStatusEffectState
        {
            status_id = status_id,
            source_unit_id = source_unit_id,
            source_profile_id = source_profile_id ?? new StringName(""),
            source_layer_id = source_layer_id ?? new StringName(""),
            source_skill_id = source_skill_id ?? new StringName(""),
            source_skill_level = source_skill_level,
            self_save_dc = Math.Max(self_save_dc, 0),
            self_save_ability = self_save_ability ?? new StringName(""),
            self_save_tag = self_save_tag ?? new StringName(""),
            self_save_roll_override = self_save_roll_override,
            save_bonus = save_bonus,
            control_save_bonus = control_save_bonus,
            range_bonus = range_bonus,
            passive_reduction = passive_reduction,
            content_dr = content_dr,
            guard_block = guard_block,
            save_advantage_tags =
                save_advantage_tags != null
                    ? new List<StringName>(save_advantage_tags)
                    : new List<StringName>(),
            save_disadvantage_tags =
                save_disadvantage_tags != null
                    ? new List<StringName>(save_disadvantage_tags)
                    : new List<StringName>(),
            save_immunity_tags =
                save_immunity_tags != null
                    ? new List<StringName>(save_immunity_tags)
                    : new List<StringName>(),
            heal_multiplier_percent = heal_multiplier_percent,
            shield_gain_multiplier_percent = shield_gain_multiplier_percent,
            incoming_damage_multiplier = incoming_damage_multiplier,
            outgoing_damage_multiplier = outgoing_damage_multiplier,
            damage_tag = damage_tag ?? new StringName(""),
            damage_tags =
                damage_tags != null ? new List<StringName>(damage_tags) : new List<StringName>(),
            damage_category = damage_category ?? new StringName(""),
            attack_roll_penalty = attack_roll_penalty,
            undispellable = undispellable,
            dispellable_magic = dispellable_magic,
            dispellable_harmful_magic = dispellable_harmful_magic,
            dispellable_beneficial_magic = dispellable_beneficial_magic,
            mitigation_tier = mitigation_tier ?? new StringName(""),
            dr_bypass_tag = dr_bypass_tag ?? new StringName(""),
            power = Math.Max(power, 1),
            stacks = 1,
            duration = Math.Max(duration_tu, -1),
            counts_as_debuff_override = counts_as_debuff_override,
            counts_as_debuff = counts_as_debuff,
            forced_move_immune = forced_move_immune,
            lock_counterattack = lock_counterattack,
            lock_guard = lock_guard,
            lock_dodge_bonus = lock_dodge_bonus,
            lock_crit = lock_crit,
            main_skill_lock_other_debuff_count = Math.Max(main_skill_lock_other_debuff_count, 0),
            stack_behavior = stack_behavior ?? new StringName(""),
            stack_limit = Math.Max(stack_limit, 0),
            body_size_category_override = body_size_category_override ?? new StringName(""),
            previous_body_size_category = previous_body_size_category ?? new StringName(""),
        };
        statusEntry.SetParamsTyped(typedStatusParams.ResidualSavePayload);
        typedStatusParams.ApplyTo(statusEntry);
        unit_state.SetStatusEffect(statusEntry);
    }

    public void ClearBlackStarBrandStatuses(BattleUnitState unit_state)
    {
        if (unit_state == null)
        {
            return;
        }
        unit_state.EraseStatusEffect(STATUS_BLACK_STAR_BRAND_NORMAL);
        unit_state.EraseStatusEffect(STATUS_BLACK_STAR_BRAND_ELITE);
        unit_state.EraseStatusEffect(STATUS_BLACK_STAR_BRAND_ELITE_GUARD_WINDOW);
    }

    public bool IsBlackStarBrandEliteTarget(BattleUnitState unit_state)
    {
        return IsEliteOrBossTarget(unit_state);
    }

    public bool IsEliteOrBossTarget(BattleUnitState unit_state)
    {
        return BattleExecutionRules.IsEliteOrBossTarget(unit_state);
    }

    public bool IsBossTarget(BattleUnitState unit_state)
    {
        return BattleExecutionRules.IsBossTarget(unit_state);
    }

    public bool IsBlackStarBrandSkill(StringName skill_id)
    {
        return ProgressionDataUtils.to_string_name(skill_id) == BLACK_STAR_BRAND_SKILL_ID;
    }

    public bool IsBlackContractPushSkill(StringName skill_id)
    {
        return ProgressionDataUtils.to_string_name(skill_id) == BLACK_CONTRACT_PUSH_SKILL_ID;
    }

    public bool IsDoomShiftSkill(StringName skill_id)
    {
        return ProgressionDataUtils.to_string_name(skill_id) == DOOM_SHIFT_SKILL_ID;
    }

    public bool IsBlackCrownSealSkill(StringName skill_id)
    {
        return ProgressionDataUtils.to_string_name(skill_id) == BLACK_CROWN_SEAL_SKILL_ID;
    }

    public void ClearCrownBreakSealStatuses(BattleUnitState unit_state)
    {
        if (unit_state == null)
        {
            return;
        }
        unit_state.EraseStatusEffect(STATUS_CROWN_BREAK_BROKEN_FANG);
        unit_state.EraseStatusEffect(STATUS_CROWN_BREAK_BROKEN_HAND);
        unit_state.EraseStatusEffect(STATUS_CROWN_BREAK_BLINDED_EYE);
    }

    public bool IsCrownBreakTargetEligible(
        BattleUnitState active_unit,
        BattleUnitState target_unit
    )
    {
        return target_unit != null
            && IsUnitValidForEffect(active_unit, target_unit, "enemy")
            && target_unit.HasStatusEffect(STATUS_BLACK_STAR_BRAND_ELITE);
    }

    public bool IsCrownBreakSkill(StringName skill_id)
    {
        return ProgressionDataUtils.to_string_name(skill_id) == CROWN_BREAK_SKILL_ID;
    }

    public bool IsDoomSentenceTargetEligible(
        BattleUnitState active_unit,
        BattleUnitState target_unit
    )
    {
        return target_unit != null
            && IsUnitValidForEffect(active_unit, target_unit, "enemy")
            && IsEliteOrBossTarget(target_unit);
    }

    public bool IsBlackCrownSealTargetEligible(
        BattleUnitState active_unit,
        BattleUnitState target_unit
    )
    {
        return target_unit != null
            && IsUnitValidForEffect(active_unit, target_unit, "enemy")
            && IsBossTarget(target_unit);
    }

    public bool IsDoomSentenceSkill(StringName skill_id)
    {
        return ProgressionDataUtils.to_string_name(skill_id) == DOOM_SENTENCE_SKILL_ID;
    }

    public int ApplyForcedMoveEffect(
        BattleUnitState sourceUnit,
        BattleUnitState unitState,
        CombatEffectDefinition effectDefinition,
        BattleEventBatch eventBatch,
        BattleForcedMoveContext forcedMoveContext,
        BattleSaveContext saveContext = default
    )
    {
        BattleState state = RtState();
        BattleGridService gridService = _runtime.GetGridService();
        if (state == null || unitState == null || effectDefinition == null)
        {
            return 0;
        }
        int moveDistance = Math.Max(effectDefinition.ForcedMoveDistance, 0);
        if (moveDistance <= 0)
        {
            return 0;
        }
        if (ForcedMoveSaveBlocksEffect(sourceUnit, unitState, effectDefinition, saveContext, eventBatch))
        {
            return 0;
        }
        if (BattleTemporalStatusService.HasTimeStasis(unitState))
        {
            if (eventBatch != null)
            {
                eventBatch.AddLogLine($"{unitState.display_name} 处于时间静滞，强制位移无效。");
            }
            return 0;
        }
        if (BlocksEnemyForcedMove(sourceUnit, unitState))
        {
            if (eventBatch != null)
            {
                eventBatch.AddLogLine($"{unitState.display_name} 稳如金刚，未被强制位移。");
            }
            return 0;
        }

        BattleForcedMoveMode mode = effectDefinition.ForcedMoveModeKind;
        if (mode == BattleForcedMoveMode.Unknown)
        {
            return 0;
        }
        if (mode is BattleForcedMoveMode.Jump or BattleForcedMoveMode.Blink)
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
                !gridService.CanTraverse(
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
            var previousCoords = DuplicateVector2IArray(unitState.occupied_coords);
            if (!gridService.MoveUnit(state, unitState, nextCoord))
            {
                break;
            }
            movedSteps += 1;
            AppendChangedCoords(eventBatch, previousCoords);
            AppendChangedUnitCoords(eventBatch, unitState);
            AppendChangedUnitId(eventBatch, unitState.unit_id);
        }
        return movedSteps;
    }

    private static bool ForcedMoveSaveBlocksEffect(
        BattleUnitState sourceUnit,
        BattleUnitState unitState,
        CombatEffectDefinition effectDefinition,
        BattleSaveContext saveContext,
        BattleEventBatch eventBatch
    )
    {
        BattleSaveResult saveResult = BattleSaveResolver.ResolveSaveResult(
            sourceUnit,
            unitState,
            effectDefinition,
            saveContext
        );
        if (!saveResult.HasSave || !saveResult.Success)
        {
            return false;
        }
        eventBatch?.AddLogLine(
            saveResult.Immune
                ? $"{unitState.display_name} 免疫本次强制位移。"
                : $"{unitState.display_name} 抵抗了本次强制位移。"
        );
        return true;
    }

    private BodySizeOverrideResult ApplyBodySizeCategoryOverrideEffectResult(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        CombatEffectDefinition effect_definition,
        BattleEventBatch batch
    )
    {
        BodySizeOverrideResult result = new(
            false,
            Array.Empty<StringName>(),
            Array.Empty<string>()
        );
        BattleState state = RtState();
        BattleGridService gridService = _runtime.GetGridService();
        if (state == null || target_unit == null || effect_definition == null)
        {
            return result;
        }
        StringName statusId = ProgressionDataUtils.to_string_name(effect_definition.StatusId);
        StringName targetCategory = ProgressionDataUtils.to_string_name(
            effect_definition.BodySizeCategory
        );
        if (
            IsEmpty(statusId)
            || !BodySizeContentRules.IsValidBodySizeCategory(targetCategory)
        )
        {
            return result;
        }
        int durationTu = Math.Max(effect_definition.DurationTu, 0);
        if (durationTu <= 0)
        {
            return result;
        }

        var existingEntry = target_unit.GetStatusEffect(statusId);
        StringName restoreCategory = target_unit.body_size_category;
        if (existingEntry != null)
        {
            StringName existingRestoreCategory = existingEntry.previous_body_size_category;
            if (BodySizeContentRules.IsValidBodySizeCategory(existingRestoreCategory))
            {
                restoreCategory = existingRestoreCategory;
            }
        }

        StringName previousCategory = target_unit.body_size_category;
        int previousBodySize = target_unit.body_size;
        Vector2I previousFootprint = target_unit.footprint_size;
        List<Vector2I> previousOccupiedCoords = DuplicateVector2IArray(target_unit.occupied_coords);
        gridService.ClearUnitOccupancy(state, target_unit);
        target_unit.SetBodySizeCategory(targetCategory);
        if (
            !gridService.CanPlaceFootprint(
                state,
                target_unit.coord,
                target_unit.footprint_size,
                target_unit.unit_id,
                target_unit
            )
        )
        {
            target_unit.RestoreBodyShapeProjection(
                previousCategory,
                previousBodySize,
                previousFootprint,
                previousOccupiedCoords
            );
            gridService.SetOccupantsTyped(state, previousOccupiedCoords, target_unit.unit_id);
            return result with
            {
                LogLines = new[]
                {
                    $"{target_unit.display_name} 周围空间不足，无法改变体型。",
                },
            };
        }
        gridService.SetOccupantsTyped(state, target_unit.occupied_coords, target_unit.unit_id);

        using (
            GodotProjectionLease<GDictionary> parametersProjection =
                RuntimePlainPayload.ProjectDictionaryLease(
                    effect_definition.Parameters,
                    "battle-special-skill-effect-parameters",
                    LifetimeDomain.Battle,
                    "BattleSpecialSkillResolver.body_size_override_parameters"
                )
        )
        {
            SetRuntimeBodySizeOverrideStatusEffect(
                target_unit,
                statusId,
                durationTu,
                source_unit != null ? source_unit.unit_id : new StringName(""),
                Math.Max(effect_definition.Power, 1),
                parametersProjection.Value,
                targetCategory,
                restoreCategory
            );
        }
        AppendChangedCoords(batch, previousOccupiedCoords);
        AppendChangedUnitCoords(batch, target_unit);
        AppendChangedUnitId(batch, target_unit.unit_id);
        return new BodySizeOverrideResult(
            true,
            new[] { statusId },
            new[] { $"{target_unit.display_name} 的体型临时变为 {targetCategory}。" }
        );
    }

    public bool BlocksEnemyForcedMove(BattleUnitState source_unit, BattleUnitState target_unit)
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
        foreach (BattleStatusEffectState statusEntry in target_unit.GetStatusEffectsTyped())
        {
            if (statusEntry == null || statusEntry.stacks <= 0)
                continue;
            if (ForcedMoveStatusParameters.FromStatus(statusEntry).ForcedMoveImmune)
                return true;
        }
        return false;
    }

    internal void RecordVajraBodyMasteryFromIncomingDamageTyped(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
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
            skillDefinition,
            result,
            _runtime.GetSkillDefinitionIndexTyped()
        );
        ApplySkillMasteryGrantTyped(targetUnit, grant, batch);
    }

    internal Vector2I PickForcedMoveCoord(
        BattleUnitState unit_state,
        BattleForcedMoveMode mode,
        BattleUnitState source_unit,
        BattleForcedMoveContext forced_move_context
    )
    {
        BattleState state = RtState();
        BattleGridService gridService = _runtime.GetGridService();
        if (state == null || unit_state == null)
        {
            return new Vector2I(-1, -1);
        }
        unit_state.RefreshFootprint();
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
                !gridService.CanTraverse(
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

    internal int ScoreForcedMoveCoord(
        BattleUnitState unit_state,
        Vector2I candidate_coord,
        BattleForcedMoveMode mode,
        BattleUnitState source_unit,
        BattleForcedMoveContext forced_move_context
    )
    {
        BattleState state = RtState();
        BattleGridService gridService = _runtime.GetGridService();
        if (state == null || unit_state == null)
        {
            return FORCED_MOVE_INVALID_SCORE;
        }
        if (mode is BattleForcedMoveMode.WindPush or BattleForcedMoveMode.Knockback)
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
                    gridService.GetDistance(candidate_coord, hostileUnit.coord)
                );
            }
        }
        int score = closestHostileDistance * 100;
        score -= gridService.GetDistance(unit_state.coord, candidate_coord) * 10;
        score -= candidate_coord.Y * 2 + candidate_coord.X;
        if (mode == BattleForcedMoveMode.Evasive)
        {
            score += 5;
        }
        return score;
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
        if (DotVector2I(stepDelta, pushDirection) <= 0)
        {
            return FORCED_MOVE_INVALID_SCORE;
        }
        int currentProjection = DotVector2I(unit_state.coord, pushDirection);
        int candidateProjection = DotVector2I(candidate_coord, pushDirection);
        return (candidateProjection - currentProjection) * 1000
            - candidate_coord.Y * 2
            - candidate_coord.X;
    }

    public int DotVector2I(Vector2I first, Vector2I second)
    {
        return first.X * second.X + first.Y * second.Y;
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
            contextDirection = NormalizeAxisDirection(contextDirection);
            if (contextDirection != Vector2I.Zero)
            {
                return contextDirection;
            }
        }
        if (source_unit != null && unit_state != null)
        {
            return NormalizeAxisDirection(
                unit_state.coord - source_unit.coord
            );
        }
        return Vector2I.Zero;
    }

    public Vector2I NormalizeAxisDirection(Vector2I direction)
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

    public void HandleAdjacentAllyDefeat(BattleUnitState defeated_unit)
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
        _runtime.GetFateRuntime()
            ?.HandleMisfortuneTrigger(
                MisfortuneTriggerRequest.AdjacentAllyDefeated(defeated_unit, adjacentAllies)
            );
    }

    public void HandleLowLuckRelicAllyDefeat(
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
                    LowLuckRelicRules.ToStringName(LowLuckRelicAttributeKind.BloodDebtShawl)
                )
            )
            {
                continue;
            }
            candidate.SetCurrentAp(
                candidate.current_ap + LowLuckRelicRules.BloodDebtAllyDownApGain
            );
            AppendChangedUnitId(batch, candidate.unit_id);
            if (batch != null)
            {
                batch.AddLogLine(
                    $"{candidate.display_name} 目睹队友倒地，血债披肩返还 {LowLuckRelicRules.BloodDebtAllyDownApGain} 点行动点。"
                );
            }
        }
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
        defeated_unit.RefreshFootprint();
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
            candidate.RefreshFootprint();
            if (AreUnitsAdjacent(candidate, defeated_unit))
            {
                adjacentAllies.Add(candidate);
            }
        }
        return adjacentAllies;
    }

    public bool AreUnitsAdjacent(BattleUnitState first_unit, BattleUnitState second_unit)
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

    private static List<Vector2I> DuplicateVector2IArray(IEnumerable<Vector2I> coords)
    {
        var result = new List<Vector2I>();
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
