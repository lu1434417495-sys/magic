using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

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
    bool SetApToZeroAtTurnStart,
    string DisplayLabel,
    string TurnStartLogReasonId
);

public static class BattleStatusSemanticTable
{
    private static readonly StringName[] HardControlStatusIds =
    {
        "prone",
        "stunned",
        "paralyzed",
        "frozen",
        "petrified",
    };

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
        STATUS_NIGHT_PRESSURE = "night_pressure",
        STATUS_MAGIC_SHIELD = "magic_shield",
        STATUS_MARKED = "marked",
        STATUS_METEOR_CONCUSSED = "meteor_concussed",
        STATUS_PINNED = "pinned",
        STATUS_PARALYZED = "paralyzed",
        STATUS_PRISMATIC_BARRIER = "prismatic_barrier",
        STATUS_PETRIFIED = "petrified",
        STATUS_MADNESS = "madness",
        STATUS_ROOTED = "rooted",
        STATUS_POISONED = "poisoned",
        STATUS_SHOCKED = "shocked",
        STATUS_SLOW = "slow",
        STATUS_SPELLWARD = "spellward",
        STATUS_SOUL_FRACTURE = "soul_fracture",
        STATUS_STAGGERED = "staggered",
        STATUS_AFTERSHOCK = "aftershock",
        STATUS_REACTION_LOCK = "reaction_lock",
        STATUS_FRIGHTENED = "frightened",
        STATUS_STUNNED = "stunned",
        STATUS_KNOCKDOWN_IMMUNITY = StatusContentRules.KnockdownImmunity,
        STATUS_TAUNTED = "taunted",
        STATUS_TENDON_CUT = "tendon_cut",
        STATUS_CROWN_BREAK_BROKEN_FANG = "crown_break_broken_fang",
        STATUS_CROWN_BREAK_BROKEN_HAND = "crown_break_broken_hand",
        STATUS_CROWN_BREAK_BLINDED_EYE = "crown_break_blinded_eye",
        STATUS_DOOM_SENTENCE_VERDICT = "doom_sentence_verdict",
        STATUS_LAST_STAND_ACTIVE = "last_stand_active",
        STATUS_WILLPOWER_SAVE_BONUS_UP = "willpower_save_bonus_up",
        STATUS_TIME_STASIS = TemporalStatusContentRules.TimeStasisStatusId,
        STATUS_TIME_SLOW = TemporalStatusContentRules.TimeSlowStatusId,
        STATUS_TIME_REVERBERATION = TemporalStatusContentRules.TimeReverberationStatusId,
        STATUS_TEMPORAL_AP_STOLEN = "temporal_ap_stolen",
        STATUS_BLACK_STAR_BRAND_NORMAL = "black_star_brand_normal",
        STATUS_BLACK_STAR_BRAND_ELITE = "black_star_brand_elite";

    private const int DEFAULT_DISPEL_PRIORITY = 50;

    // One row owns everything the table knows about a status: semantic, harm
    // classification, cast blocking, dispel/cleanse eligibility, dispel priority.
    // Adding a status means adding exactly one row here.
    private sealed record BattleStatusDescriptor
    {
        internal BattleStatusSemantic Semantic { get; init; }
        internal bool Harmful { get; init; }

        // Harmful but immune to普通净化（temporal-only release / dispel 专用路径）。
        internal bool CleanseProtected { get; init; }
        internal bool BlocksPendingCast { get; init; }
        internal bool DispellableHarmful { get; init; }
        internal bool DispellableBeneficial { get; init; }
        internal int DispelPriority { get; init; } = DEFAULT_DISPEL_PRIORITY;
    }

    private static readonly Dictionary<StringName, BattleStatusDescriptor> StatusTable = new()
    {
        // —— 增益（可驱散增益）——
        [STATUS_ATTACK_UP] = new() { Semantic = RefreshSemantic(), DispellableBeneficial = true, DispelPriority = 80 },
        [STATUS_ATTACK_ROLL_BONUS_UP] = new() { Semantic = RefreshSemantic(), DispellableBeneficial = true, DispelPriority = 80 },
        [STATUS_DAMAGE_REDUCTION_UP] = new() { Semantic = RefreshSemantic(), DispellableBeneficial = true, DispelPriority = 80 },
        [STATUS_DODGE_BONUS_UP] = new() { Semantic = RefreshSemantic(), DispellableBeneficial = true, DispelPriority = 80 },
        [STATUS_WILLPOWER_SAVE_BONUS_UP] = new() { Semantic = RefreshSemantic(), DispellableBeneficial = true, DispelPriority = 80 },
        [STATUS_DEATH_WARD] = new() { Semantic = RefreshSemantic(), DispellableBeneficial = true, DispelPriority = 100 },
        [STATUS_MAGIC_SHIELD] = new() { Semantic = RefreshSemantic(), DispellableBeneficial = true, DispelPriority = 100 },
        [STATUS_PRISMATIC_BARRIER] = new() { Semantic = RefreshSemantic(), DispellableBeneficial = true, DispelPriority = 100 },
        [STATUS_SPELLWARD] = new() { Semantic = RefreshSemantic(), DispellableBeneficial = true, DispelPriority = 100 },

        // —— 中性/自持（只有语义）——
        [STATUS_ARCHER_PRE_AIM] = new() { Semantic = RefreshSemantic() },
        [STATUS_ARCHER_RANGE_UP] = new() { Semantic = RefreshSemantic() },
        [STATUS_ARCHER_SHOOTING_SPECIALIZATION] = new() { Semantic = RefreshSemantic() },
        [STATUS_GUARDING] = new() { Semantic = RefreshSemantic() },
        [STATUS_KNOCKDOWN_IMMUNITY] = new()
        {
            Semantic = RefreshSemantic(displayLabel: "击倒免疫"),
        },
        [STATUS_LAST_STAND_ACTIVE] = new() { Semantic = RefreshSemantic() },
        [STATUS_TIME_REVERBERATION] = new() { Semantic = RefreshSemantic() },

        // —— 减益 ——
        [STATUS_ARMOR_BREAK] = new() { Semantic = RefreshSemantic(), Harmful = true },
        [STATUS_TENDON_CUT] = new() { Semantic = RefreshSemantic(), Harmful = true },
        [STATUS_CROWN_BREAK_BROKEN_FANG] = new() { Semantic = RefreshSemantic(), Harmful = true },
        [STATUS_CROWN_BREAK_BROKEN_HAND] = new() { Semantic = RefreshSemantic(), Harmful = true },
        [STATUS_CROWN_BREAK_BLINDED_EYE] = new() { Semantic = RefreshSemantic(), Harmful = true },
        [STATUS_BLIND] = new() { Semantic = RefreshSemantic(attackRollPenalty: DEFAULT_BLIND_ATTACK_ROLL_PENALTY), Harmful = true, DispellableHarmful = true, DispelPriority = 90 },
        [STATUS_FROZEN] = new() { Semantic = RefreshSemantic(), Harmful = true, DispellableHarmful = true, BlocksPendingCast = true, DispelPriority = 90 },
        [STATUS_MARKED] = new() { Semantic = RefreshSemantic(), Harmful = true, DispellableHarmful = true },
        [STATUS_PARALYZED] = new() { Semantic = RefreshSemantic(displayLabel: "麻痹"), Harmful = true, DispellableHarmful = true, BlocksPendingCast = true, DispelPriority = 90 },
        [STATUS_PINNED] = new() { Semantic = RefreshSemantic(), Harmful = true, DispellableHarmful = true, DispelPriority = 70 },
        [STATUS_ROOTED] = new() { Semantic = RefreshSemantic(), Harmful = true, DispellableHarmful = true, DispelPriority = 90 },
        [STATUS_POISONED] = new() { Semantic = RefreshSemantic(attackRollPenalty: 2, displayLabel: "中毒"), Harmful = true, DispellableHarmful = true, DispelPriority = 70 },
        [STATUS_SHOCKED] = new() { Semantic = RefreshSemantic(), Harmful = true, DispellableHarmful = true, DispelPriority = 70 },
        [STATUS_TAUNTED] = new() { Semantic = RefreshSemantic(), Harmful = true, DispellableHarmful = true, DispelPriority = 70 },
        [STATUS_HEX_OF_FRAILTY] = new() { Semantic = RefreshSemantic(), Harmful = true, DispellableHarmful = true, DispelPriority = 70 },
        [STATUS_NIGHT_PRESSURE] = new() { Semantic = RefreshSemantic(displayLabel: "夜幕压迫"), Harmful = true, DispellableHarmful = true, DispelPriority = 70 },
        [STATUS_DOOM_SENTENCE_VERDICT] = new() { Semantic = RefreshSemantic(), Harmful = true, DispellableHarmful = true },
        [STATUS_BURNING] = new() { Semantic = BuildSemantic(STACK_ADD, 3, TICK_TIMELINE_DAMAGE), Harmful = true, DispellableHarmful = true, DispelPriority = 70 },
        [STATUS_SLOW] = new() { Semantic = RefreshSemantic(moveCostDelta: 1), Harmful = true, DispellableHarmful = true, DispelPriority = 70 },
        [STATUS_SOUL_FRACTURE] = new() { Semantic = RefreshSemantic(displayLabel: "灵魂裂解"), Harmful = true, DispellableHarmful = true },
        [STATUS_AFTERSHOCK] = new() { Semantic = RefreshSemantic(displayLabel: "余悸"), Harmful = true, DispellableHarmful = true },
        [STATUS_REACTION_LOCK] = new() { Semantic = RefreshSemantic(displayLabel: "反应封锁"), Harmful = true, DispellableHarmful = true },
        [STATUS_FRIGHTENED] = new() { Semantic = RefreshSemantic(displayLabel: "恐惧"), Harmful = true, DispellableHarmful = true },
        [STATUS_STUNNED] = new() { Semantic = RefreshSemantic(displayLabel: "震慑"), Harmful = true, DispellableHarmful = true },
        [STATUS_MADNESS] = new() { Semantic = RefreshSemantic(), Harmful = true, DispellableHarmful = true, BlocksPendingCast = true, DispelPriority = 90 },
        [STATUS_PETRIFIED] = new() { Semantic = RefreshSemantic(), Harmful = true, CleanseProtected = true, BlocksPendingCast = true },
        [STATUS_TIME_STASIS] = new() { Semantic = RefreshSemantic(), Harmful = true, CleanseProtected = true, DispellableHarmful = true, DispelPriority = 90 },
        [STATUS_TIME_SLOW] = new() { Semantic = RefreshSemantic(), Harmful = true, DispellableHarmful = true, DispelPriority = 70 },
        [STATUS_TEMPORAL_AP_STOLEN] = new()
        {
            Semantic = RefreshSemantic(
                tickMode: TICK_TURN_START_AP_PENALTY,
                apPenaltyGroup: STATUS_TEMPORAL_AP_STOLEN,
                consumeAfterApPenalty: true,
                setApToZeroAtTurnStart: true,
                displayLabel: "时间剥夺",
                turnStartLogReasonId: "temporal_ap_stolen_consumed"
            ),
            Harmful = true,
            DispellableHarmful = true,
            DispelPriority = 70,
        },
        [STATUS_STAGGERED] = new()
        {
            Semantic = RefreshSemantic(
                tickMode: TICK_TURN_START_AP_PENALTY,
                apPenaltyGroup: STATUS_STAGGERED,
                displayLabel: "踉跄"
            ),
            Harmful = true,
            DispellableHarmful = true,
            BlocksPendingCast = true,
            DispelPriority = 70,
        },
        [STATUS_METEOR_CONCUSSED] = new()
        {
            Semantic = RefreshSemantic(
                tickMode: TICK_TURN_START_AP_PENALTY,
                attackRollPenalty: 2,
                apPenaltyGroup: STATUS_STAGGERED,
                consumeAfterApPenalty: true,
                displayLabel: "震眩",
                turnStartLogReasonId: "meteor_concussed_ap_consumed"
            ),
            Harmful = true,
            DispellableHarmful = true,
            BlocksPendingCast = true,
            DispelPriority = 70,
        },

        // —— 命运烙印：只参与减益判定，语义留空走 effect 自配置路径 ——
        [STATUS_BLACK_STAR_BRAND_NORMAL] = new() { Harmful = true },
        [STATUS_BLACK_STAR_BRAND_ELITE] = new() { Harmful = true },
    };

    private static BattleStatusDescriptor GetDescriptor(StringName statusId)
    {
        var normalizedStatusId = ProgressionDataUtils.to_string_name(statusId);
        return normalizedStatusId != ""
            && StatusTable.TryGetValue(normalizedStatusId, out BattleStatusDescriptor descriptor)
            ? descriptor
            : null;
    }

    public static bool HasSemantic(StringName statusId) => GetSemantic(statusId).Defined;

    public static bool IsHarmfulStatus(StringName statusId) =>
        GetDescriptor(statusId)?.Harmful ?? false;

    public static bool IsCleansableHarmfulStatus(StringName statusId)
    {
        BattleStatusDescriptor descriptor = GetDescriptor(statusId);
        // CleanseProtected（petrified / time_stasis）解除属于 temporal-only release /
        // dispel 路径，普通净化不移除。
        return descriptor != null && descriptor.Harmful && !descriptor.CleanseProtected;
    }

    public static bool IsHarmfulStatusEntry(BattleStatusEffectState statusEntry)
    {
        if (statusEntry == null)
            return false;
        if (statusEntry.counts_as_debuff_override)
            return statusEntry.counts_as_debuff;
        return IsHarmfulStatus(statusEntry.status_id);
    }

    public static bool IsCleansableHarmfulStatusEntry(BattleStatusEffectState statusEntry)
    {
        if (statusEntry == null || statusEntry.undispellable)
            return false;
        if (GetDescriptor(statusEntry.status_id)?.CleanseProtected == true)
            return false;
        return IsHarmfulStatusEntry(statusEntry);
    }

    public static bool BlocksPendingCast(StringName statusId) =>
        GetDescriptor(statusId)?.BlocksPendingCast ?? false;

    public static string GetDisplayLabel(StringName statusId)
    {
        BattleStatusSemantic semantic = GetSemantic(statusId);
        return string.IsNullOrWhiteSpace(semantic.DisplayLabel)
            ? ProgressionDataUtils.to_string_name(statusId).ToString()
            : semantic.DisplayLabel;
    }

    public static string GetDisplayLabel(BattleStatusEffectState statusEntry)
    {
        if (statusEntry == null)
            return "";
        string configuredLabel = statusEntry.display_label ?? "";
        return string.IsNullOrWhiteSpace(configuredLabel)
            ? GetDisplayLabel(statusEntry.status_id)
            : configuredLabel;
    }

    public static bool IsDispellableHarmfulStatus(StringName statusId) =>
        GetDescriptor(statusId)?.DispellableHarmful ?? false;

    public static bool IsDispellableBeneficialStatus(StringName statusId) =>
        GetDescriptor(statusId)?.DispellableBeneficial ?? false;

    public static bool IsDispellableHarmfulStatusEntry(BattleStatusEffectState statusEntry)
    {
        if (statusEntry == null)
            return false;
        if (statusEntry.undispellable)
            return false;
        if (statusEntry.dispellable_harmful_magic)
            return true;
        if (statusEntry.dispellable_magic)
            return IsHarmfulStatusEntry(statusEntry);
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
            return !IsHarmfulStatusEntry(statusEntry);
        return IsDispellableBeneficialStatus(statusEntry.status_id);
    }

    public static bool IsHardControlled(BattleUnitState unitState)
    {
        if (unitState == null)
            return false;
        foreach (StringName statusId in HardControlStatusIds)
        {
            BattleStatusEffectState status = unitState.GetStatusEffect(statusId);
            if (status != null && status.stacks > 0)
                return true;
        }
        return false;
    }

    internal static bool IsHardControlled(BattleUnitReadView unitView)
    {
        if (!unitView.IsValid)
            return false;
        foreach (StringName statusId in HardControlStatusIds)
        {
            if (unitView.HasStatusEffect(statusId) && unitView.GetStatusStacks(statusId) > 0)
                return true;
        }
        return false;
    }

    public static int GetDispelPriority(StringName statusId) =>
        GetDescriptor(statusId)?.DispelPriority ?? DEFAULT_DISPEL_PRIORITY;

    public static BattleStatusSemantic GetSemantic(StringName statusId) =>
        GetDescriptor(statusId)?.Semantic ?? default;

    public static BattleStatusEffectState MergeStatus(
        CombatEffectDefinition effectDefinition,
        StringName sourceUnitId,
        BattleStatusEffectState existingEntry = null,
        StringName statusIdOverride = default
    )
    {
        if (effectDefinition == null)
            return null;
        StringName resolvedStatusId = ProgressionDataUtils.to_string_name(
            statusIdOverride == default || statusIdOverride == ""
                ? effectDefinition.StatusId
                : statusIdOverride
        );
        if (resolvedStatusId == "")
            return null;
        BattleStatusSemantic semantic = GetSemantic(resolvedStatusId);
        var statusEntry = BuildMergedStatusEffectState(
            effectDefinition,
            sourceUnitId,
            existingEntry,
            resolvedStatusId
        );
        int incomingPower = Mathf.Max(effectDefinition.Power, 1);
        int previousPower = Mathf.Max(statusEntry.power, 0);
        int previousStacks = Mathf.Max(statusEntry.stacks, 0);
        if (!semantic.Defined)
        {
            StringName configuredStackMode =
                ProgressionDataUtils.to_string_name(effectDefinition.StackBehavior) == ""
                    ? STACK_REFRESH
                    : effectDefinition.StackBehavior;
            int configuredStackLimit = Mathf.Max(effectDefinition.StackLimit, 0);
            statusEntry.stack_behavior = configuredStackMode;
            statusEntry.stack_limit = configuredStackLimit;
            statusEntry.power = Mathf.Max(previousPower, incomingPower);
            statusEntry.stacks =
                configuredStackMode == STACK_ADD
                    ? (
                        configuredStackLimit > 0
                            ? Mathf.Min(
                                Mathf.Max(previousStacks + incomingPower, 1),
                                configuredStackLimit
                            )
                            : Mathf.Max(previousStacks + incomingPower, 1)
                    )
                    : 1;
            int durationTu = ResolveDurationTu(effectDefinition);
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
        int semanticDurationTu = ResolveDurationTu(effectDefinition);
        if (semanticDurationTu >= 0)
        {
            int previousDuration = statusEntry.duration;
            statusEntry.duration = Mathf.Max(semanticDurationTu, previousDuration);
        }
        int tickIntervalTu = ResolveTickIntervalTu(effectDefinition);
        if (tickIntervalTu > 0)
        {
            statusEntry.tick_interval_tu = tickIntervalTu;
            if (statusEntry.next_tick_at_tu <= 0)
                statusEntry.next_tick_at_tu = tickIntervalTu;
        }
        return statusEntry;
    }

    private static BattleStatusEffectState BuildMergedStatusEffectState(
        CombatEffectDefinition effectDefinition,
        StringName sourceUnitId,
        BattleStatusEffectState existingEntry,
        StringName resolvedStatusId
    )
    {
        var statusEntry = BattleStatusEffectState.CreateOrDuplicate(existingEntry);
        statusEntry.status_id = resolvedStatusId;
        statusEntry.source_unit_id = sourceUnitId;
        AssignResidualParams(statusEntry, effectDefinition.Parameters);
        statusEntry.display_label = effectDefinition.DisplayName ?? "";
        statusEntry.counts_as_debuff_override = effectDefinition.CountsAsDebuffOverride;
        statusEntry.counts_as_debuff = effectDefinition.CountsAsDebuff;
        statusEntry.lock_counterattack = effectDefinition.LockCounterattack;
        statusEntry.lock_guard = effectDefinition.LockGuard;
        statusEntry.lock_dodge_bonus = effectDefinition.LockDodgeBonus;
        statusEntry.lock_crit = effectDefinition.LockCrit;
        statusEntry.save_bonus = effectDefinition.SaveBonus;
        statusEntry.control_save_bonus = effectDefinition.ControlSaveBonus;
        statusEntry.heal_multiplier_percent = effectDefinition.HealMultiplierPercent;
        statusEntry.shield_gain_multiplier_percent = effectDefinition.ShieldGainMultiplierPercent;
        statusEntry.passive_reduction = effectDefinition.PassiveReduction;
        statusEntry.content_dr = effectDefinition.ContentDr;
        statusEntry.guard_block = effectDefinition.GuardBlock;
        statusEntry.range_bonus = effectDefinition.RangeBonus;
        statusEntry.death_prevention_priority = Mathf.Max(
            effectDefinition.DeathPreventionPriority,
            0
        );
        statusEntry.save_advantage_tags = BuildStringNameList(effectDefinition.SaveAdvantageTags);
        statusEntry.save_disadvantage_tags = BuildStringNameList(
            effectDefinition.SaveDisadvantageTags
        );
        statusEntry.save_immunity_tags = BuildStringNameList(effectDefinition.SaveImmunityTags);
        statusEntry.status_tags = BuildStringNameList(effectDefinition.EffectTags);
        statusEntry.save_bonus_by_tag = BuildStringNameIntMap(
            effectDefinition.GetStringNameIntMapParamTyped("save_bonus_by_tag")
        );
        statusEntry.attack_roll_penalty = Math.Max(
            statusEntry.attack_roll_penalty,
            effectDefinition.AttackRollPenalty
        );
        statusEntry.attack_roll_bonus = effectDefinition.AttackRollBonus;
        statusEntry.melee_combo_stack_gain_bonus =
            effectDefinition.MeleeComboStackGainBonus;
        statusEntry.combo_attack_bonus_status_id =
            effectDefinition.ComboAttackBonusStatusId;
        statusEntry.combo_attack_bonus_stack_divisor =
            effectDefinition.ComboAttackBonusStackDivisor;
        statusEntry.upkeep_resource = effectDefinition.UpkeepResource;
        statusEntry.upkeep_interval_tu = effectDefinition.UpkeepIntervalTu;
        statusEntry.upkeep_base_cost = effectDefinition.UpkeepBaseCost;
        statusEntry.upkeep_escalation_interval_tu =
            effectDefinition.UpkeepEscalationIntervalTu;
        statusEntry.upkeep_cost_multiplier = effectDefinition.UpkeepCostMultiplier;
        statusEntry.break_on_hard_control = effectDefinition.BreakOnHardControl;
        statusEntry.termination_status_id = effectDefinition.TerminationStatusId;
        statusEntry.termination_status_duration_tu =
            effectDefinition.TerminationStatusDurationTu;
        statusEntry.termination_attack_roll_penalty =
            effectDefinition.TerminationAttackRollPenalty;
        statusEntry.termination_cooldown_tu =
            effectDefinition.TerminationCooldownTu;
        if (statusEntry.upkeep_interval_tu > 0)
        {
            statusEntry.tick_interval_tu = statusEntry.upkeep_interval_tu;
        }
        statusEntry.attack_roll_advantage = effectDefinition.AttackRollAdvantage;
        statusEntry.consume_on_next_attack_check = effectDefinition.ConsumeOnNextAttackCheck;
        statusEntry.consume_on_next_save = effectDefinition.ConsumeOnNextSave;
        statusEntry.source_bound_attack_roll_penalty =
            effectDefinition.GetIntParamTyped("source_bound_attack_roll_penalty", 0);
        statusEntry.source_bound_attack_roll_penalty_min_stacks = Math.Max(
            effectDefinition.GetIntParamTyped("source_bound_attack_roll_penalty_min_stacks", 1),
            1
        );
        statusEntry.source_bound_incoming_attack_roll_bonus_per_stack =
            effectDefinition.GetIntParamTyped(
                "source_bound_incoming_attack_roll_bonus_per_stack",
                0
            );
        statusEntry.source_bound_incoming_attack_roll_bonus_min_stacks = Math.Max(
            effectDefinition.GetIntParamTyped(
                "source_bound_incoming_attack_roll_bonus_min_stacks",
                1
            ),
            1
        );
        statusEntry.source_bound_weapon_bonus_damage_dice_count = Math.Max(
            effectDefinition.SourceBoundWeaponBonusDamageDiceCount,
            0
        );
        statusEntry.source_bound_weapon_bonus_damage_dice_sides = Math.Max(
            effectDefinition.SourceBoundWeaponBonusDamageDiceSides,
            0
        );
        statusEntry.source_bound_weapon_bonus_damage_dice_bonus =
            effectDefinition.SourceBoundWeaponBonusDamageDiceBonus;
        statusEntry.undispellable = effectDefinition.Undispellable;
        statusEntry.dispellable_magic = effectDefinition.DispellableMagic;
        statusEntry.dispellable_harmful_magic = effectDefinition.DispellableHarmfulMagic;
        statusEntry.dispellable_beneficial_magic = effectDefinition.DispellableBeneficialMagic;
        statusEntry.damage_tag = effectDefinition.DamageTag;
        statusEntry.damage_tags = BuildStringNameList(effectDefinition.DamageTags);
        statusEntry.damage_category = effectDefinition.DamageCategory;
        statusEntry.mitigation_tier = effectDefinition.MitigationTier;
        statusEntry.dr_bypass_tag = effectDefinition.DrBypassTag;
        statusEntry.main_skill_lock_other_debuff_count = Mathf.Max(
            effectDefinition.MainSkillLockOtherDebuffCount,
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

    public static bool ShouldSetApToZeroAtTurnStart(BattleStatusEffectState statusEntry)
    {
        if (statusEntry == null)
            return false;
        BattleStatusSemantic semantic = GetSemantic(statusEntry.status_id);
        return semantic.TickMode == TICK_TURN_START_AP_PENALTY
            && semantic.SetApToZeroAtTurnStart;
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
        return semantic.TickMode == TICK_TIMELINE_DAMAGE || HasTimelineDamagePayload(statusEntry)
            ? GetEffectIntensity(statusEntry)
            : 0;
    }

    public static int RollTimelineTickDamage(
        BattleStatusEffectState statusEntry,
        Func<int, int> rollDamageDie = null
    )
    {
        if (statusEntry == null || statusEntry.tick_interval_tu <= 0)
            return 0;
        BattleStatusSemantic semantic = GetSemantic(statusEntry.status_id);
        if (semantic.TickMode != TICK_TIMELINE_DAMAGE && !HasTimelineDamagePayload(statusEntry))
            return 0;
        if (statusEntry.timeline_damage_dice_count <= 0 || statusEntry.timeline_damage_dice_sides <= 0)
            return GetEffectIntensity(statusEntry);

        int total = Math.Max(statusEntry.timeline_damage_flat_bonus, 0);
        int diceCount = Math.Max(statusEntry.timeline_damage_dice_count, 0);
        int diceSides = Math.Max(statusEntry.timeline_damage_dice_sides, 1);
        Func<int, int> roller = rollDamageDie ?? DefaultRollDamageDie;
        for (int index = 0; index < diceCount; index++)
            total += Math.Clamp(roller(diceSides), 1, diceSides);
        return Math.Max(total, 0);
    }

    private static bool HasTimelineDamagePayload(BattleStatusEffectState statusEntry) =>
        statusEntry != null
        && (
            statusEntry.timeline_damage_dice_count > 0
            || statusEntry.timeline_damage_dice_sides > 0
            || statusEntry.timeline_damage_flat_bonus > 0
        );

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

    // 攻击惩罚默认跨状态累加;需要"同类来源不叠加、取最大"语义的状态把
    // status_id 加入此集合(整组只生效最大值,结果再与累加池求和)。当前
    // 没有取大来源,集合为空——这是设计预留的配置点,不是死代码。
    private static readonly HashSet<StringName> AttackRollPenaltyTakeMaxStatusIds = new();

    public static bool IsAttackRollPenaltyTakeMax(StringName statusId) =>
        statusId != null && !statusId.IsEmpty && AttackRollPenaltyTakeMaxStatusIds.Contains(statusId);

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
        bool setApToZeroAtTurnStart = false,
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
            setApToZeroAtTurnStart,
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
        bool setApToZeroAtTurnStart = false,
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
            setApToZeroAtTurnStart,
            displayLabel ?? "",
            turnStartLogReasonId ?? ""
        );

    private static int ResolveDurationTu(CombatEffectDefinition effectDefinition)
    {
        if (effectDefinition == null)
            return -1;
        if (effectDefinition.DurationTu > 0)
            return NormalizePositiveTu(effectDefinition.DurationTu, "status duration_tu");
        return -1;
    }

    private static int ResolveTickIntervalTu(CombatEffectDefinition effectDefinition)
    {
        if (effectDefinition == null)
            return 0;
        if (effectDefinition.TickIntervalTu > 0)
            return NormalizePositiveTu(
                effectDefinition.TickIntervalTu,
                "status tick_interval_tu"
            );
        return 0;
    }

    private static int GetEffectIntensity(BattleStatusEffectState statusEntry) =>
        statusEntry == null ? 0 : Mathf.Max(Mathf.Max(statusEntry.power, statusEntry.stacks), 1);

    private static int DefaultRollDamageDie(int diceSides) =>
        TrueRandomSeedService.RandiRange(1, Math.Max(diceSides, 1));

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

    private static void AssignResidualParams(
        BattleStatusEffectState statusEntry,
        IReadOnlyDictionary<string, object> parameters
    )
    {
        if (statusEntry == null)
            return;
        statusEntry.SetParamsTyped(
            BattleStatusEffectState.CopyResidualParamsPlain(parameters)
        );
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
