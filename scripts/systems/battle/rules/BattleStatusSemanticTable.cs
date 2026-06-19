using System.Collections.Generic;
using Godot;

public readonly record struct BattleStatusDurationAdvanceResult(bool Expired, bool Changed);

public readonly record struct BattleStatusSemantic(
    bool Defined,
    StringName StackMode,
    int MaxStacks,
    StringName TickMode,
    int MoveCostDelta,
    int AttackRollPenalty,
    StringName ApPenaltyGroup,
    bool ConsumeAfterApPenalty,
    string DisplayLabel,
    string TurnStartLogReasonId
);

public static class BattleStatusSemanticTable
{
    internal static readonly StringName STACK_REFRESH = "refresh",
        STACK_ADD = "add";
    internal static readonly StringName TICK_NONE = "none",
        TICK_TURN_START_AP_PENALTY = "turn_start_ap_penalty",
        TICK_TURN_START_DAMAGE = "turn_start_damage",
        TICK_TIMELINE_DAMAGE = "timeline_damage";
    internal const int TU_GRANULARITY = 5,
        DEFAULT_BLIND_ATTACK_ROLL_PENALTY = 4;
    internal static readonly StringName STATUS_ARMOR_BREAK = "armor_break",
        STATUS_ARCHER_PRE_AIM = "archer_pre_aim",
        STATUS_ARCHER_RANGE_UP = "archer_range_up",
        STATUS_ARCHER_SHOOTING_SPECIALIZATION = "archer_shooting_specialization",
        STATUS_ATTACK_UP = "attack_up",
        STATUS_ATTACK_ROLL_BONUS_UP = "attack_roll_bonus_up",
        STATUS_BURNING = "burning",
        STATUS_BLIND = "blind",
        STATUS_DEATH_WARD = "death_ward",
        STATUS_DAMAGE_REDUCTION_UP = "damage_reduction_up",
        STATUS_DODGE_BONUS_UP = "dodge_bonus_up",
        STATUS_FROZEN = "frozen",
        STATUS_GUARDING = "guarding",
        STATUS_HEX_OF_FRAILTY = "hex_of_frailty",
        STATUS_MAGIC_SHIELD = "magic_shield",
        STATUS_MARKED = "marked",
        STATUS_METEOR_CONCUSSED = "meteor_concussed",
        STATUS_PINNED = "pinned",
        STATUS_PRISMATIC_BARRIER = "prismatic_barrier",
        STATUS_PETRIFIED = "petrified",
        STATUS_MADNESS = "madness",
        STATUS_ROOTED = "rooted",
        STATUS_SHOCKED = "shocked",
        STATUS_SLOW = "slow",
        STATUS_SPELLWARD = "spellward",
        STATUS_SOUL_FRACTURE = "soul_fracture",
        STATUS_STAGGERED = "staggered",
        STATUS_TAUNTED = "taunted",
        STATUS_TENDON_CUT = "tendon_cut",
        STATUS_CROWN_BREAK_BROKEN_FANG = "crown_break_broken_fang",
        STATUS_CROWN_BREAK_BROKEN_HAND = "crown_break_broken_hand",
        STATUS_CROWN_BREAK_BLINDED_EYE = "crown_break_blinded_eye",
        STATUS_DOOM_SENTENCE_VERDICT = "doom_sentence_verdict",
        STATUS_LAST_STAND_ACTIVE = "last_stand_active",
        STATUS_WILLPOWER_SAVE_BONUS_UP = "willpower_save_bonus_up",
        STATUS_TIME_STASIS = "time_stasis",
        STATUS_TIME_SLOW = "time_slow",
        STATUS_TIME_REVERBERATION = "time_reverberation";

    public static bool HasSemantic(StringName statusId) => GetSemantic(statusId).Defined;

    public static bool IsHarmfulStatus(StringName statusId)
    {
        var normalizedStatusId = ProgressionDataUtils.to_string_name(statusId);
        return normalizedStatusId == STATUS_ARMOR_BREAK
            || normalizedStatusId == STATUS_BLIND
            || normalizedStatusId == STATUS_FROZEN
            || normalizedStatusId == STATUS_MARKED
            || normalizedStatusId == STATUS_METEOR_CONCUSSED
            || normalizedStatusId == STATUS_PINNED
            || normalizedStatusId == STATUS_ROOTED
            || normalizedStatusId == STATUS_SHOCKED
            || normalizedStatusId == STATUS_TAUNTED
            || normalizedStatusId == STATUS_TENDON_CUT
            || normalizedStatusId == STATUS_BURNING
            || normalizedStatusId == STATUS_SLOW
            || normalizedStatusId == STATUS_SOUL_FRACTURE
            || normalizedStatusId == STATUS_STAGGERED
            || normalizedStatusId == STATUS_HEX_OF_FRAILTY
            || normalizedStatusId == STATUS_CROWN_BREAK_BROKEN_FANG
            || normalizedStatusId == STATUS_CROWN_BREAK_BROKEN_HAND
            || normalizedStatusId == STATUS_CROWN_BREAK_BLINDED_EYE
            || normalizedStatusId == STATUS_DOOM_SENTENCE_VERDICT
            || normalizedStatusId == STATUS_PETRIFIED
            || normalizedStatusId == STATUS_MADNESS
            || normalizedStatusId == STATUS_TIME_STASIS
            || normalizedStatusId == STATUS_TIME_SLOW
            || normalizedStatusId == "black_star_brand_normal"
            || normalizedStatusId == "black_star_brand_elite";
    }

    public static bool IsCleansableHarmfulStatus(StringName statusId)
    {
        var normalizedStatusId = ProgressionDataUtils.to_string_name(statusId);
        // time_stasis 解除属于 temporal-only release / dispel 路径，普通净化不移除。
        if (normalizedStatusId == STATUS_PETRIFIED || normalizedStatusId == STATUS_TIME_STASIS)
            return false;
        return IsHarmfulStatus(normalizedStatusId);
    }

    public static bool BlocksPendingCast(StringName statusId)
    {
        var normalizedStatusId = ProgressionDataUtils.to_string_name(statusId);
        return normalizedStatusId == STATUS_PETRIFIED
            || normalizedStatusId == STATUS_MADNESS
            || normalizedStatusId == STATUS_FROZEN
            || normalizedStatusId == STATUS_STAGGERED
            || normalizedStatusId == STATUS_METEOR_CONCUSSED;
    }

    public static bool IsDispellableHarmfulStatus(StringName statusId)
    {
        var normalizedStatusId = ProgressionDataUtils.to_string_name(statusId);
        return normalizedStatusId == STATUS_BLIND
            || normalizedStatusId == STATUS_BURNING
            || normalizedStatusId == STATUS_DOOM_SENTENCE_VERDICT
            || normalizedStatusId == STATUS_FROZEN
            || normalizedStatusId == STATUS_HEX_OF_FRAILTY
            || normalizedStatusId == STATUS_MADNESS
            || normalizedStatusId == STATUS_MARKED
            || normalizedStatusId == STATUS_METEOR_CONCUSSED
            || normalizedStatusId == STATUS_PINNED
            || normalizedStatusId == STATUS_ROOTED
            || normalizedStatusId == STATUS_SHOCKED
            || normalizedStatusId == STATUS_SLOW
            || normalizedStatusId == STATUS_SOUL_FRACTURE
            || normalizedStatusId == STATUS_STAGGERED
            || normalizedStatusId == STATUS_TAUNTED
            || normalizedStatusId == STATUS_TIME_STASIS
            || normalizedStatusId == STATUS_TIME_SLOW;
    }

    public static bool IsDispellableBeneficialStatus(StringName statusId)
    {
        var normalizedStatusId = ProgressionDataUtils.to_string_name(statusId);
        return normalizedStatusId == STATUS_ATTACK_UP
            || normalizedStatusId == STATUS_ATTACK_ROLL_BONUS_UP
            || normalizedStatusId == STATUS_DAMAGE_REDUCTION_UP
            || normalizedStatusId == STATUS_DEATH_WARD
            || normalizedStatusId == STATUS_DODGE_BONUS_UP
            || normalizedStatusId == STATUS_MAGIC_SHIELD
            || normalizedStatusId == STATUS_PRISMATIC_BARRIER
            || normalizedStatusId == STATUS_SPELLWARD
            || normalizedStatusId == STATUS_WILLPOWER_SAVE_BONUS_UP;
    }

    public static bool IsDispellableHarmfulStatusEntry(BattleStatusEffectState statusEntry)
    {
        if (statusEntry == null)
            return false;
        if (statusEntry.undispellable)
            return false;
        if (statusEntry.dispellable_harmful_magic)
            return true;
        if (statusEntry.dispellable_magic)
            return IsHarmfulStatus(statusEntry.status_id);
        return IsDispellableHarmfulStatus(statusEntry.status_id);
    }

    public static bool IsDispellableBeneficialStatusEntry(BattleStatusEffectState statusEntry)
    {
        if (statusEntry == null)
            return false;
        if (statusEntry.undispellable)
            return false;
        if (statusEntry.dispellable_beneficial_magic)
            return true;
        if (statusEntry.dispellable_magic)
            return !IsHarmfulStatus(statusEntry.status_id);
        return IsDispellableBeneficialStatus(statusEntry.status_id);
    }

    public static int GetDispelPriority(StringName statusId)
    {
        var normalizedStatusId = ProgressionDataUtils.to_string_name(statusId);
        if (
            normalizedStatusId == STATUS_DEATH_WARD
            || normalizedStatusId == STATUS_MAGIC_SHIELD
            || normalizedStatusId == STATUS_PRISMATIC_BARRIER
            || normalizedStatusId == STATUS_SPELLWARD
        )
            return 100;
        if (
            normalizedStatusId == STATUS_BLIND
            || normalizedStatusId == STATUS_FROZEN
            || normalizedStatusId == STATUS_MADNESS
            || normalizedStatusId == STATUS_ROOTED
            || normalizedStatusId == STATUS_TIME_STASIS
        )
            return 90;
        if (
            normalizedStatusId == STATUS_ATTACK_UP
            || normalizedStatusId == STATUS_ATTACK_ROLL_BONUS_UP
            || normalizedStatusId == STATUS_DAMAGE_REDUCTION_UP
            || normalizedStatusId == STATUS_DODGE_BONUS_UP
            || normalizedStatusId == STATUS_WILLPOWER_SAVE_BONUS_UP
        )
            return 80;
        if (
            normalizedStatusId == STATUS_BURNING
            || normalizedStatusId == STATUS_HEX_OF_FRAILTY
            || normalizedStatusId == STATUS_METEOR_CONCUSSED
            || normalizedStatusId == STATUS_PINNED
            || normalizedStatusId == STATUS_SHOCKED
            || normalizedStatusId == STATUS_SLOW
            || normalizedStatusId == STATUS_STAGGERED
            || normalizedStatusId == STATUS_TAUNTED
            || normalizedStatusId == STATUS_TIME_SLOW
        )
            return 70;
        return 50;
    }

    public static BattleStatusSemantic GetSemantic(StringName statusId)
    {
        var normalizedStatusId = ProgressionDataUtils.to_string_name(statusId);
        if (
            normalizedStatusId == STATUS_ARCHER_PRE_AIM
            || normalizedStatusId == STATUS_ARCHER_RANGE_UP
            || normalizedStatusId == STATUS_ARCHER_SHOOTING_SPECIALIZATION
            || normalizedStatusId == STATUS_ATTACK_UP
            || normalizedStatusId == STATUS_ATTACK_ROLL_BONUS_UP
            || normalizedStatusId == STATUS_DAMAGE_REDUCTION_UP
            || normalizedStatusId == STATUS_DEATH_WARD
            || normalizedStatusId == STATUS_DODGE_BONUS_UP
            || normalizedStatusId == STATUS_GUARDING
            || normalizedStatusId == STATUS_HEX_OF_FRAILTY
            || normalizedStatusId == STATUS_MAGIC_SHIELD
            || normalizedStatusId == STATUS_PRISMATIC_BARRIER
            || normalizedStatusId == STATUS_SPELLWARD
            || normalizedStatusId == STATUS_LAST_STAND_ACTIVE
            || normalizedStatusId == STATUS_WILLPOWER_SAVE_BONUS_UP
        )
            return RefreshSemantic();
        if (normalizedStatusId == STATUS_BLIND)
            return RefreshSemantic(attackRollPenalty: DEFAULT_BLIND_ATTACK_ROLL_PENALTY);
        if (
            normalizedStatusId == STATUS_ARMOR_BREAK
            || normalizedStatusId == STATUS_FROZEN
            || normalizedStatusId == STATUS_MARKED
            || normalizedStatusId == STATUS_PINNED
            || normalizedStatusId == STATUS_ROOTED
            || normalizedStatusId == STATUS_SHOCKED
            || normalizedStatusId == STATUS_TAUNTED
            || normalizedStatusId == STATUS_TENDON_CUT
            || normalizedStatusId == STATUS_CROWN_BREAK_BROKEN_FANG
            || normalizedStatusId == STATUS_CROWN_BREAK_BROKEN_HAND
            || normalizedStatusId == STATUS_CROWN_BREAK_BLINDED_EYE
            || normalizedStatusId == STATUS_DOOM_SENTENCE_VERDICT
            || normalizedStatusId == STATUS_PETRIFIED
            || normalizedStatusId == STATUS_MADNESS
            || normalizedStatusId == STATUS_TIME_STASIS
            || normalizedStatusId == STATUS_TIME_SLOW
            || normalizedStatusId == STATUS_TIME_REVERBERATION
        )
            return RefreshSemantic();
        if (normalizedStatusId == STATUS_BURNING)
            return BuildSemantic(STACK_ADD, 3, TICK_TIMELINE_DAMAGE);
        if (normalizedStatusId == STATUS_SLOW)
            return RefreshSemantic(moveCostDelta: 1);
        if (normalizedStatusId == STATUS_SOUL_FRACTURE)
            return RefreshSemantic(displayLabel: "灵魂裂解");
        if (normalizedStatusId == STATUS_METEOR_CONCUSSED)
        {
            return RefreshSemantic(
                tickMode: TICK_TURN_START_AP_PENALTY,
                attackRollPenalty: 2,
                apPenaltyGroup: STATUS_STAGGERED,
                consumeAfterApPenalty: true,
                displayLabel: "震眩",
                turnStartLogReasonId: "meteor_concussed_ap_consumed"
            );
        }
        if (normalizedStatusId == STATUS_STAGGERED)
        {
            return RefreshSemantic(
                tickMode: TICK_TURN_START_AP_PENALTY,
                apPenaltyGroup: STATUS_STAGGERED,
                displayLabel: "踉跄"
            );
        }
        return default;
    }

    public static BattleStatusEffectState MergeStatus(
        CombatEffectDef effectDef,
        StringName sourceUnitId,
        BattleStatusEffectState existingEntry = null
    )
    {
        if (effectDef == null || ProgressionDataUtils.to_string_name(effectDef.status_id) == "")
            return null;
        BattleStatusSemantic semantic = GetSemantic(effectDef.status_id);
        var statusEntry = BuildMergedStatusEffectState(effectDef, sourceUnitId, existingEntry);
        int incomingPower = Mathf.Max(effectDef.power, 1);
        int previousPower = Mathf.Max(statusEntry.power, 0);
        int previousStacks = Mathf.Max(statusEntry.stacks, 0);
        if (!semantic.Defined)
        {
            statusEntry.stack_behavior =
                ProgressionDataUtils.to_string_name(effectDef.stack_behavior) == ""
                    ? STACK_REFRESH
                    : effectDef.stack_behavior;
            statusEntry.stack_limit = Mathf.Max(effectDef.stack_limit, 0);
            statusEntry.power = effectDef.power;
            statusEntry.stacks = Mathf.Max(previousStacks + 1, 1);
            int durationTu = ResolveDurationTu(effectDef);
            if (durationTu >= 0)
                statusEntry.duration = durationTu;
            return statusEntry;
        }

        StringName stackMode =
            ProgressionDataUtils.to_string_name(semantic.StackMode) == ""
                ? STACK_REFRESH
                : semantic.StackMode;
        int maxStacks = semantic.MaxStacks;
        statusEntry.stack_behavior = stackMode;
        statusEntry.stack_limit = Mathf.Max(maxStacks, 0);
        statusEntry.power = Mathf.Max(previousPower, incomingPower);
        statusEntry.stacks =
            stackMode == STACK_ADD
                ? (
                    maxStacks > 0
                        ? Mathf.Min(Mathf.Max(previousStacks + 1, 1), maxStacks)
                        : Mathf.Max(previousStacks + 1, 1)
                )
                : 1;
        int semanticDurationTu = ResolveDurationTu(effectDef);
        if (semanticDurationTu >= 0)
        {
            int previousDuration = statusEntry.duration;
            statusEntry.duration = Mathf.Max(semanticDurationTu, previousDuration);
        }
        int tickIntervalTu = ResolveTickIntervalTu(effectDef);
        if (tickIntervalTu > 0)
        {
            statusEntry.tick_interval_tu = tickIntervalTu;
            if (statusEntry.next_tick_at_tu <= 0)
                statusEntry.next_tick_at_tu = tickIntervalTu;
        }
        return statusEntry;
    }

    private static BattleStatusEffectState BuildMergedStatusEffectState(
        CombatEffectDef effectDef,
        StringName sourceUnitId,
        BattleStatusEffectState existingEntry
    )
    {
        var statusEntry = BattleStatusEffectState.CreateOrDuplicate(existingEntry);
        statusEntry.status_id = ProgressionDataUtils.to_string_name(effectDef.status_id);
        statusEntry.source_unit_id = sourceUnitId;
        statusEntry.@params = BattleStatusEffectState.CopyResidualParams(effectDef.@params);
        statusEntry.counts_as_debuff_override = effectDef.counts_as_debuff_override;
        statusEntry.counts_as_debuff = effectDef.counts_as_debuff;
        statusEntry.lock_counterattack = effectDef.lock_counterattack;
        statusEntry.lock_dodge_bonus = effectDef.lock_dodge_bonus;
        statusEntry.lock_crit = effectDef.lock_crit;
        statusEntry.save_bonus = effectDef.save_bonus;
        statusEntry.control_save_bonus = effectDef.control_save_bonus;
        statusEntry.heal_multiplier_percent = effectDef.heal_multiplier_percent;
        statusEntry.shield_gain_multiplier_percent = effectDef.shield_gain_multiplier_percent;
        statusEntry.passive_reduction = effectDef.passive_reduction;
        statusEntry.content_dr = effectDef.content_dr;
        statusEntry.guard_block = effectDef.guard_block;
        statusEntry.range_bonus = effectDef.range_bonus;
        statusEntry.death_prevention_priority = Mathf.Max(effectDef.death_prevention_priority, 0);
        statusEntry.save_advantage_tags = BuildStringNameList(effectDef.save_advantage_tags);
        statusEntry.save_disadvantage_tags = BuildStringNameList(effectDef.save_disadvantage_tags);
        statusEntry.save_immunity_tags = BuildStringNameList(effectDef.save_immunity_tags);
        statusEntry.save_tags = BuildStringNameList(effectDef.save_tags);
        statusEntry.status_tags = BuildStringNameList(effectDef.effect_tags);
        statusEntry.save_bonus_by_tag = BuildStringNameIntMap(
            effectDef.GetStringNameIntMapParamTyped("save_bonus_by_tag")
        );
        statusEntry.attack_roll_penalty = effectDef.attack_roll_penalty;
        statusEntry.undispellable = effectDef.undispellable;
        statusEntry.dispellable_magic = effectDef.dispellable_magic;
        statusEntry.dispellable_harmful_magic = effectDef.dispellable_harmful_magic;
        statusEntry.dispellable_beneficial_magic = effectDef.dispellable_beneficial_magic;
        statusEntry.damage_tag = effectDef.damage_tag;
        statusEntry.damage_tags = BuildStringNameList(effectDef.damage_tags);
        statusEntry.damage_category = effectDef.damage_category;
        statusEntry.mitigation_tier = effectDef.mitigation_tier;
        statusEntry.dr_bypass_tag = effectDef.dr_bypass_tag;
        statusEntry.main_skill_lock_other_debuff_count = Mathf.Max(
            effectDef.main_skill_lock_other_debuff_count,
            0
        );
        return statusEntry;
    }

    public static int GetTurnStartApPenalty(BattleStatusEffectState statusEntry)
    {
        if (statusEntry == null)
            return 0;
        BattleStatusSemantic semantic = GetSemantic(statusEntry.status_id);
        return semantic.TickMode != TICK_TURN_START_AP_PENALTY
            ? 0
            : GetEffectIntensity(statusEntry);
    }

    public static StringName GetTurnStartApPenaltyGroup(BattleStatusEffectState statusEntry)
    {
        if (statusEntry == null)
            return "";
        BattleStatusSemantic semantic = GetSemantic(statusEntry.status_id);
        return semantic.TickMode != TICK_TURN_START_AP_PENALTY
            ? ""
            : (
                ProgressionDataUtils.to_string_name(semantic.ApPenaltyGroup) == ""
                    ? statusEntry.status_id
                    : semantic.ApPenaltyGroup
            );
    }

    public static bool ShouldConsumeAfterTurnStartApPenalty(BattleStatusEffectState statusEntry)
    {
        if (statusEntry == null)
            return false;
        BattleStatusSemantic semantic = GetSemantic(statusEntry.status_id);
        return semantic.TickMode == TICK_TURN_START_AP_PENALTY
            && semantic.ConsumeAfterApPenalty;
    }

    public static string GetTurnStartApPenaltyDisplayLabel(BattleStatusEffectState statusEntry)
    {
        if (statusEntry == null)
            return "";
        BattleStatusSemantic semantic = GetSemantic(statusEntry.status_id);
        if (semantic.TickMode != TICK_TURN_START_AP_PENALTY)
            return "";
        string label = semantic.DisplayLabel ?? "";
        return label.StripEdges().Length > 0 ? label : statusEntry.status_id.ToString();
    }

    public static int GetTurnStartDamage(BattleStatusEffectState statusEntry)
    {
        if (statusEntry == null)
            return 0;
        BattleStatusSemantic semantic = GetSemantic(statusEntry.status_id);
        return semantic.TickMode != TICK_TURN_START_DAMAGE ? 0 : GetEffectIntensity(statusEntry);
    }

    public static int GetTimelineTickDamage(BattleStatusEffectState statusEntry)
    {
        if (statusEntry == null || statusEntry.tick_interval_tu <= 0)
            return 0;
        BattleStatusSemantic semantic = GetSemantic(statusEntry.status_id);
        return semantic.TickMode != TICK_TIMELINE_DAMAGE ? 0 : GetEffectIntensity(statusEntry);
    }

    public static int GetMoveCostDelta(BattleStatusEffectState statusEntry)
    {
        if (statusEntry == null)
            return 0;
        BattleStatusSemantic semantic = GetSemantic(statusEntry.status_id);
        int baseDelta = Mathf.Max(semantic.MoveCostDelta, 0);
        return baseDelta <= 0 ? 0 : baseDelta * GetEffectIntensity(statusEntry);
    }

    public static int GetAttackRollPenalty(BattleStatusEffectState statusEntry)
    {
        if (statusEntry == null)
            return 0;
        BattleStatusSemantic semantic = GetSemantic(statusEntry.status_id);
        int defaultPenalty = Mathf.Max(semantic.AttackRollPenalty, 0);
        if (statusEntry.TryGetAttackRollPenaltyTyped(out int overridePenalty))
            return Mathf.Max(overridePenalty, 0);
        return defaultPenalty;
    }

    public static BattleStatusDurationAdvanceResult AdvanceTimelineDurationResult(
        BattleStatusEffectState statusEntry,
        int elapsedTu
    )
    {
        if (statusEntry == null || elapsedTu <= 0 || statusEntry.duration < 0)
            return new BattleStatusDurationAdvanceResult(false, false);
        int previousDuration = statusEntry.duration;
        int remainingDuration = Mathf.Max(previousDuration - elapsedTu, 0);
        if (remainingDuration <= 0)
            return new BattleStatusDurationAdvanceResult(true, true);
        statusEntry.duration = remainingDuration;
        return new BattleStatusDurationAdvanceResult(false, remainingDuration != previousDuration);
    }

    private static BattleStatusSemantic RefreshSemantic(
        StringName tickMode = default,
        int moveCostDelta = 0,
        int attackRollPenalty = 0,
        StringName apPenaltyGroup = default,
        bool consumeAfterApPenalty = false,
        string displayLabel = "",
        string turnStartLogReasonId = ""
    ) =>
        BuildSemantic(
            STACK_REFRESH,
            1,
            tickMode,
            moveCostDelta,
            attackRollPenalty,
            apPenaltyGroup,
            consumeAfterApPenalty,
            displayLabel,
            turnStartLogReasonId
        );

    private static BattleStatusSemantic BuildSemantic(
        StringName stackMode,
        int maxStacks,
        StringName tickMode = default,
        int moveCostDelta = 0,
        int attackRollPenalty = 0,
        StringName apPenaltyGroup = default,
        bool consumeAfterApPenalty = false,
        string displayLabel = "",
        string turnStartLogReasonId = ""
    ) =>
        new(
            true,
            ProgressionDataUtils.to_string_name(stackMode) == "" ? STACK_REFRESH : stackMode,
            Mathf.Max(maxStacks, 0),
            ProgressionDataUtils.to_string_name(tickMode) == "" ? TICK_NONE : tickMode,
            Mathf.Max(moveCostDelta, 0),
            Mathf.Max(attackRollPenalty, 0),
            ProgressionDataUtils.to_string_name(apPenaltyGroup),
            consumeAfterApPenalty,
            displayLabel ?? "",
            turnStartLogReasonId ?? ""
        );

    private static int ResolveDurationTu(CombatEffectDef effectDef)
    {
        if (effectDef == null)
            return -1;
        if (effectDef.duration_tu > 0)
            return NormalizePositiveTu(effectDef.duration_tu, "status duration_tu");
        return -1;
    }

    private static int ResolveTickIntervalTu(CombatEffectDef effectDef)
    {
        if (effectDef == null)
            return 0;
        if (effectDef.tick_interval_tu > 0)
            return NormalizePositiveTu(effectDef.tick_interval_tu, "status tick_interval_tu");
        return 0;
    }

    private static int GetEffectIntensity(BattleStatusEffectState statusEntry) =>
        statusEntry == null ? 0 : Mathf.Max(Mathf.Max(statusEntry.power, statusEntry.stacks), 1);

    private static List<StringName> BuildStringNameList(IEnumerable<StringName> values)
    {
        var result = new List<StringName>();
        if (values == null)
        {
            return result;
        }
        foreach (StringName value in values)
        {
            if (value != "")
            {
                result.Add(value);
            }
        }
        return result;
    }

    private static Dictionary<StringName, int> BuildStringNameIntMap(
        IReadOnlyDictionary<StringName, int> values
    )
    {
        var result = new Dictionary<StringName, int>();
        if (values == null)
        {
            return result;
        }
        foreach (KeyValuePair<StringName, int> entry in values)
        {
            if (entry.Key != "")
            {
                result[entry.Key] = entry.Value;
            }
        }
        return result;
    }

    private static int NormalizePositiveTu(int value, string fieldLabel)
    {
        if (value <= 0)
            return -1;
        if (value % TU_GRANULARITY != 0)
        {
            int clampedValue = ((value + TU_GRANULARITY - 1) / TU_GRANULARITY) * TU_GRANULARITY;
            GameLog.Error(
                $"{fieldLabel} must use {TU_GRANULARITY} TU steps, got {value}; clamping up to {clampedValue}.",
                "battle.status.invalid_tu",
                "battle"
            );
            return clampedValue;
        }
        return value;
    }
}
