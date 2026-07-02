using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GArray = Godot.Collections.Array;
using Variant = Godot.Variant;

// 战斗状态效果数据 bag。
// 翻译自 battle_status_effect_state.gd（2026-05-24，数据层 C# 迁移）。
// 契约：docs/design/battle_csharp_migration.md
public class BattleStatusEffectState
{
    private static readonly string[] RequiredSchemaFields =
    {
        "status_id",
        "source_unit_id",
        "power",
        "params",
        "stacks",
    };

    private static readonly string[] OptionalSchemaFields =
    {
        "duration",
        "display_label",
        "tick_interval_tu",
        "next_tick_at_tu",
        "timeline_damage_dice_count",
        "timeline_damage_dice_sides",
        "timeline_damage_flat_bonus",
        "skip_next_turn_end_decay",
        "counts_as_debuff_override",
        "counts_as_debuff",
        "lock_counterattack",
        "lock_guard",
        "lock_dodge_bonus",
        "lock_crit",
        "main_skill_lock_other_debuff_count",
    };

    private static readonly string[] FormalParamKeys =
    {
        "stack_behavior",
        "stack_limit",
        "incoming_damage_multiplier",
        "outgoing_damage_multiplier",
        "source",
        "layer_id",
        "source_skill_id",
        "death_prevention_priority",
        "heal_multiplier_percent",
        "shield_gain_multiplier_percent",
        "attack_roll_penalty",
        "source_bound_attack_roll_penalty",
        "source_bound_attack_roll_penalty_min_stacks",
        "undispellable",
        "dispellable_magic",
        "dispellable_harmful_magic",
        "dispellable_beneficial_magic",
        "damage_tag",
        "damage_tags",
        "damage_category",
        "mitigation_tier",
        "dr_bypass_tag",
        "body_size_category_override",
        "previous_body_size_category",
        "self_save_dc",
        "self_save_ability",
        "self_save_tag",
        "self_save_roll_override",
        "skill_level",
        "save_bonus",
        "control_save_bonus",
        "passive_reduction",
        "content_dr",
        "guard_block",
        "range_bonus",
        "save_advantage_tags",
        "save_disadvantage_tags",
        "save_immunity_tags",
        "save_tags",
        "status_tags",
        "save_bonus_by_tag",
    };

    public StringName status_id { get; set; } = "";
    public StringName source_unit_id { get; set; } = "";
    public StringName source_profile_id { get; set; } = "";
    public StringName source_layer_id { get; set; } = "";
    public StringName source_skill_id { get; set; } = "";
    public StringName stack_behavior { get; set; } = "";
    public int stack_limit { get; set; }
    public int power { get; set; }
    private readonly Dictionary<string, object> _params = new(System.StringComparer.Ordinal);
    internal GDictionary @params
    {
        get => RuntimePlainPayload.ProjectDictionary(
            _params,
            "BattleStatusEffectState.@params"
        );
        set => ReplaceParams(value);
    }
    public double? incoming_damage_multiplier { get; set; }
    public double? outgoing_damage_multiplier { get; set; }
    public int? heal_multiplier_percent { get; set; }
    public int? shield_gain_multiplier_percent { get; set; }
    public int attack_roll_penalty { get; set; } = -1;
    public int source_bound_attack_roll_penalty { get; set; }
    public int source_bound_attack_roll_penalty_min_stacks { get; set; } = 1;
    public bool undispellable { get; set; }
    public bool dispellable_magic { get; set; }
    public bool dispellable_harmful_magic { get; set; }
    public bool dispellable_beneficial_magic { get; set; }
    public StringName damage_tag { get; set; } = "";
    public List<StringName> damage_tags { get; set; } = new();
    public StringName damage_category { get; set; } = "";
    public StringName mitigation_tier { get; set; } = "";
    public StringName dr_bypass_tag { get; set; } = "";
    public StringName body_size_category_override { get; set; } = "";
    public StringName previous_body_size_category { get; set; } = "";
    public int self_save_dc { get; set; }
    public StringName self_save_ability { get; set; } = "";
    public StringName self_save_tag { get; set; } = "";
    public int? self_save_roll_override { get; set; }
    public int? source_skill_level { get; set; }
    public int death_prevention_priority { get; set; }
    public int stacks { get; set; }
    public int duration { get; set; } = -1;
    public string display_label { get; set; } = "";
    public int tick_interval_tu { get; set; }
    public int next_tick_at_tu { get; set; }
    public int timeline_damage_dice_count { get; set; }
    public int timeline_damage_dice_sides { get; set; }
    public int timeline_damage_flat_bonus { get; set; }
    public bool skip_next_turn_end_decay { get; set; }
    public bool forced_move_immune { get; set; }
    public bool counts_as_debuff_override { get; set; }
    public bool counts_as_debuff { get; set; }
    public bool lock_counterattack { get; set; }
    public bool lock_guard { get; set; }
    public bool lock_dodge_bonus { get; set; }
    public bool lock_crit { get; set; }
    public int save_bonus { get; set; }
    public int control_save_bonus { get; set; }
    public int passive_reduction { get; set; }
    public int content_dr { get; set; }
    public int guard_block { get; set; }
    public int range_bonus { get; set; }
    public int main_skill_lock_other_debuff_count { get; set; }
    public List<StringName> save_advantage_tags { get; set; } = new();
    public List<StringName> save_disadvantage_tags { get; set; } = new();
    public List<StringName> save_immunity_tags { get; set; } = new();
    public List<StringName> save_tags { get; set; } = new();
    public List<StringName> status_tags { get; set; } = new();
    public Dictionary<StringName, int> save_bonus_by_tag { get; set; } = new();

    public bool IsEmpty()
    {
        return status_id == "";
    }

    public bool HasDuration()
    {
        return duration >= 0;
    }

    internal bool TryGetHealMultiplierPercentTyped(out int value)
    {
        if (heal_multiplier_percent.HasValue)
        {
            value = heal_multiplier_percent.Value;
            return true;
        }
        value = 0;
        return false;
    }

    internal bool TryGetShieldGainMultiplierPercentTyped(out int value)
    {
        if (shield_gain_multiplier_percent.HasValue)
        {
            value = shield_gain_multiplier_percent.Value;
            return true;
        }
        value = 0;
        return false;
    }

    internal bool TryGetAttackRollPenaltyTyped(out int value)
    {
        if (attack_roll_penalty >= 0)
        {
            value = attack_roll_penalty;
            return true;
        }
        value = 0;
        return false;
    }

    internal Dictionary<string, object> GetParamsTyped()
    {
        return RuntimePlainPayload.NormalizeDictionary(
            BuildParamsProjection(),
            "BattleStatusEffectState.GetParamsTyped"
        );
    }

    internal static BattleStatusEffectState CreateOrDuplicate(BattleStatusEffectState existingEntry)
    {
        return existingEntry?.DuplicateState() ?? new BattleStatusEffectState();
    }

    public BattleStatusEffectState DuplicateState()
    {
        return new BattleStatusEffectState
        {
            status_id = status_id,
            source_unit_id = source_unit_id,
            source_profile_id = source_profile_id,
            source_layer_id = source_layer_id,
            source_skill_id = source_skill_id,
            stack_behavior = stack_behavior,
            stack_limit = stack_limit,
            power = power,
            @params = CopyResidualParams(@params),
            incoming_damage_multiplier = incoming_damage_multiplier,
            outgoing_damage_multiplier = outgoing_damage_multiplier,
            heal_multiplier_percent = heal_multiplier_percent,
            shield_gain_multiplier_percent = shield_gain_multiplier_percent,
            attack_roll_penalty = attack_roll_penalty,
            source_bound_attack_roll_penalty = source_bound_attack_roll_penalty,
            source_bound_attack_roll_penalty_min_stacks =
                source_bound_attack_roll_penalty_min_stacks,
            undispellable = undispellable,
            dispellable_magic = dispellable_magic,
            dispellable_harmful_magic = dispellable_harmful_magic,
            dispellable_beneficial_magic = dispellable_beneficial_magic,
            damage_tag = damage_tag,
            damage_tags = BuildStringNameList(damage_tags),
            damage_category = damage_category,
            mitigation_tier = mitigation_tier,
            dr_bypass_tag = dr_bypass_tag,
            body_size_category_override = body_size_category_override,
            previous_body_size_category = previous_body_size_category,
            self_save_dc = self_save_dc,
            self_save_ability = self_save_ability,
            self_save_tag = self_save_tag,
            self_save_roll_override = self_save_roll_override,
            source_skill_level = source_skill_level,
            death_prevention_priority = death_prevention_priority,
            stacks = stacks,
            duration = duration,
            display_label = display_label,
            tick_interval_tu = tick_interval_tu,
            next_tick_at_tu = next_tick_at_tu,
            timeline_damage_dice_count = timeline_damage_dice_count,
            timeline_damage_dice_sides = timeline_damage_dice_sides,
            timeline_damage_flat_bonus = timeline_damage_flat_bonus,
            skip_next_turn_end_decay = skip_next_turn_end_decay,
            forced_move_immune = forced_move_immune,
            counts_as_debuff_override = counts_as_debuff_override,
            counts_as_debuff = counts_as_debuff,
            lock_counterattack = lock_counterattack,
            lock_guard = lock_guard,
            lock_dodge_bonus = lock_dodge_bonus,
            lock_crit = lock_crit,
            save_bonus = save_bonus,
            control_save_bonus = control_save_bonus,
            passive_reduction = passive_reduction,
            content_dr = content_dr,
            guard_block = guard_block,
            range_bonus = range_bonus,
            main_skill_lock_other_debuff_count = main_skill_lock_other_debuff_count,
            save_advantage_tags = BuildStringNameList(save_advantage_tags),
            save_disadvantage_tags = BuildStringNameList(save_disadvantage_tags),
            save_immunity_tags = BuildStringNameList(save_immunity_tags),
            save_tags = BuildStringNameList(save_tags),
            status_tags = BuildStringNameList(status_tags),
            save_bonus_by_tag = BuildStringNameIntMap(save_bonus_by_tag),
        };
    }

    internal GDictionary ToDictionary()
    {
        GDictionary projectedParams = BuildParamsProjection();
        GDictionary payload = new()
        {
            ["status_id"] = status_id.ToString(),
            ["source_unit_id"] = source_unit_id.ToString(),
            ["power"] = power,
            ["params"] = projectedParams,
            ["stacks"] = stacks,
        };
        if (HasDuration())
        {
            payload["duration"] = duration;
        }
        if (!string.IsNullOrWhiteSpace(display_label))
        {
            payload["display_label"] = display_label;
        }
        if (tick_interval_tu > 0)
        {
            payload["tick_interval_tu"] = tick_interval_tu;
        }
        if (next_tick_at_tu > 0)
        {
            payload["next_tick_at_tu"] = next_tick_at_tu;
        }
        if (timeline_damage_dice_count > 0 || timeline_damage_dice_sides > 0)
        {
            payload["timeline_damage_dice_count"] = timeline_damage_dice_count;
            payload["timeline_damage_dice_sides"] = timeline_damage_dice_sides;
        }
        if (timeline_damage_flat_bonus > 0)
        {
            payload["timeline_damage_flat_bonus"] = timeline_damage_flat_bonus;
        }
        if (skip_next_turn_end_decay)
        {
            payload["skip_next_turn_end_decay"] = true;
        }
        if (counts_as_debuff_override)
        {
            payload["counts_as_debuff_override"] = true;
            payload["counts_as_debuff"] = counts_as_debuff;
        }
        if (lock_counterattack)
        {
            payload["lock_counterattack"] = true;
        }
        if (lock_guard)
        {
            payload["lock_guard"] = true;
        }
        if (lock_dodge_bonus)
        {
            payload["lock_dodge_bonus"] = true;
        }
        if (lock_crit)
        {
            payload["lock_crit"] = true;
        }
        if (main_skill_lock_other_debuff_count > 0)
        {
            payload["main_skill_lock_other_debuff_count"] = main_skill_lock_other_debuff_count;
        }
        return payload;
    }

    internal static BattleStatusEffectState FromDictionary(GDictionary effectDict)
    {
        if (effectDict == null)
            return null;
        if (!HasCurrentSchemaFields(effectDict))
        {
            return null;
        }

        if (!TryGetStringLike(effectDict, "status_id", out string statusId)
            || string.IsNullOrEmpty(statusId))
        {
            return null;
        }
        if (!TryGetStringLike(effectDict, "source_unit_id", out string sourceUnitId))
        {
            return null;
        }
        if (!TryGetStrictInt(effectDict, "power", out int power))
        {
            return null;
        }
        if (!TryGetDictionary(effectDict, "params", out GDictionary parameters))
        {
            return null;
        }
        if (!TryGetStrictInt(effectDict, "stacks", out int stacks) || stacks < 0)
        {
            return null;
        }

        int durationValue = -1;
        if (effectDict.ContainsKey("duration"))
        {
            if (!TryGetStrictInt(effectDict, "duration", out durationValue) || durationValue < 0)
            {
                return null;
            }
        }

        string displayLabelValue = "";
        if (effectDict.ContainsKey("display_label"))
        {
            if (
                !TryGetStringLike(effectDict, "display_label", out displayLabelValue)
                || string.IsNullOrWhiteSpace(displayLabelValue)
            )
            {
                return null;
            }
        }

        int tickIntervalValue = 0;
        if (effectDict.ContainsKey("tick_interval_tu"))
        {
            if (
                !TryGetStrictInt(effectDict, "tick_interval_tu", out tickIntervalValue)
                || tickIntervalValue <= 0
            )
            {
                return null;
            }
        }

        int nextTickAtValue = 0;
        if (effectDict.ContainsKey("next_tick_at_tu"))
        {
            if (
                !TryGetStrictInt(effectDict, "next_tick_at_tu", out nextTickAtValue)
                || nextTickAtValue <= 0
            )
            {
                return null;
            }
        }

        int timelineDamageDiceCountValue = 0;
        int timelineDamageDiceSidesValue = 0;
        int timelineDamageFlatBonusValue = 0;
        bool hasTimelineDamageDiceCount = effectDict.ContainsKey("timeline_damage_dice_count");
        bool hasTimelineDamageDiceSides = effectDict.ContainsKey("timeline_damage_dice_sides");
        bool hasTimelineDamageFlatBonus = effectDict.ContainsKey("timeline_damage_flat_bonus");
        if (hasTimelineDamageDiceCount != hasTimelineDamageDiceSides)
        {
            return null;
        }
        if (hasTimelineDamageDiceCount)
        {
            if (
                !TryGetStrictInt(
                    effectDict,
                    "timeline_damage_dice_count",
                    out timelineDamageDiceCountValue
                )
                || timelineDamageDiceCountValue <= 0
                || !TryGetStrictInt(
                    effectDict,
                    "timeline_damage_dice_sides",
                    out timelineDamageDiceSidesValue
                )
                || timelineDamageDiceSidesValue <= 0
            )
            {
                return null;
            }
        }
        if (hasTimelineDamageFlatBonus)
        {
            if (
                !TryGetStrictInt(
                    effectDict,
                    "timeline_damage_flat_bonus",
                    out timelineDamageFlatBonusValue
                )
                || timelineDamageFlatBonusValue <= 0
                || !hasTimelineDamageDiceCount
            )
            {
                return null;
            }
        }

        bool skipDecayValue = false;
        if (effectDict.ContainsKey("skip_next_turn_end_decay"))
        {
            if (!TryReadBoolField(effectDict, "skip_next_turn_end_decay", out skipDecayValue)
                || !skipDecayValue)
            {
                return null;
            }
        }

        bool countsAsDebuffOverrideValue = false;
        if (effectDict.ContainsKey("counts_as_debuff_override"))
        {
            if (
                !TryReadBoolField(
                    effectDict,
                    "counts_as_debuff_override",
                    out countsAsDebuffOverrideValue
                )
                || !countsAsDebuffOverrideValue
            )
            {
                return null;
            }
        }

        bool countsAsDebuffValue = false;
        if (effectDict.ContainsKey("counts_as_debuff"))
        {
            if (
                !countsAsDebuffOverrideValue
                || !TryReadBoolField(effectDict, "counts_as_debuff", out countsAsDebuffValue)
            )
            {
                return null;
            }
        }
        else if (countsAsDebuffOverrideValue)
        {
            return null;
        }

        bool lockCounterattackValue = false;
        if (effectDict.ContainsKey("lock_counterattack"))
        {
            if (
                !TryReadBoolField(effectDict, "lock_counterattack", out lockCounterattackValue)
                || !lockCounterattackValue
            )
            {
                return null;
            }
        }

        bool lockGuardValue = false;
        if (effectDict.ContainsKey("lock_guard"))
        {
            if (!TryReadBoolField(effectDict, "lock_guard", out lockGuardValue) || !lockGuardValue)
            {
                return null;
            }
        }

        bool lockCritValue = false;
        if (effectDict.ContainsKey("lock_crit"))
        {
            if (
                !TryReadBoolField(effectDict, "lock_crit", out lockCritValue)
                || !lockCritValue
            )
            {
                return null;
            }
        }

        bool lockDodgeBonusValue = false;
        if (effectDict.ContainsKey("lock_dodge_bonus"))
        {
            if (
                !TryReadBoolField(effectDict, "lock_dodge_bonus", out lockDodgeBonusValue)
                || !lockDodgeBonusValue
            )
            {
                return null;
            }
        }

        int mainSkillLockOtherDebuffCountValue = 0;
        if (effectDict.ContainsKey("main_skill_lock_other_debuff_count"))
        {
            if (
                !TryGetStrictInt(
                    effectDict,
                    "main_skill_lock_other_debuff_count",
                    out mainSkillLockOtherDebuffCountValue
                )
                || mainSkillLockOtherDebuffCountValue <= 0
            )
            {
                return null;
            }
        }

        return new BattleStatusEffectState
        {
            status_id = new StringName(statusId),
            source_unit_id = new StringName(sourceUnitId),
            power = power,
            @params = CopyResidualParams(parameters),
            incoming_damage_multiplier = ReadOptionalDoubleParam(parameters, "incoming_damage_multiplier"),
            outgoing_damage_multiplier = ReadOptionalDoubleParam(parameters, "outgoing_damage_multiplier"),
            source_profile_id = ReadOptionalStringNameParam(parameters, "source"),
            source_layer_id = ReadOptionalStringNameParam(parameters, "layer_id"),
            source_skill_id = ReadOptionalStringNameParam(parameters, "source_skill_id"),
            stack_behavior = ReadOptionalStringNameParam(parameters, "stack_behavior"),
            stack_limit = ReadOptionalIntParam(parameters, "stack_limit") ?? 0,
            heal_multiplier_percent = ReadOptionalIntParam(parameters, "heal_multiplier_percent"),
            shield_gain_multiplier_percent = ReadOptionalIntParam(
                parameters,
                "shield_gain_multiplier_percent"
            ),
            attack_roll_penalty = ReadOptionalIntParam(parameters, "attack_roll_penalty") ?? -1,
            source_bound_attack_roll_penalty =
                ReadOptionalIntParam(parameters, "source_bound_attack_roll_penalty") ?? 0,
            source_bound_attack_roll_penalty_min_stacks =
                ReadOptionalIntParam(parameters, "source_bound_attack_roll_penalty_min_stacks") ?? 1,
            undispellable = ReadOptionalBoolParam(parameters, "undispellable"),
            dispellable_magic = ReadOptionalBoolParam(parameters, "dispellable_magic"),
            dispellable_harmful_magic = ReadOptionalBoolParam(parameters, "dispellable_harmful_magic"),
            dispellable_beneficial_magic = ReadOptionalBoolParam(parameters, "dispellable_beneficial_magic"),
            damage_tag = ReadOptionalStringNameParam(parameters, "damage_tag"),
            damage_tags = ReadStringNameListParam(parameters, "damage_tags"),
            damage_category = ReadOptionalStringNameParam(parameters, "damage_category"),
            mitigation_tier = ReadOptionalStringNameParam(parameters, "mitigation_tier"),
            dr_bypass_tag = ReadOptionalStringNameParam(parameters, "dr_bypass_tag"),
            body_size_category_override = ReadOptionalStringNameParam(
                parameters,
                "body_size_category_override"
            ),
            previous_body_size_category = ReadOptionalStringNameParam(
                parameters,
                "previous_body_size_category"
            ),
            self_save_dc = ReadOptionalIntParam(parameters, "self_save_dc") ?? 0,
            self_save_ability = ReadOptionalStringNameParam(parameters, "self_save_ability"),
            self_save_tag = ReadOptionalStringNameParam(parameters, "self_save_tag"),
            self_save_roll_override = ReadOptionalIntParam(parameters, "self_save_roll_override"),
            source_skill_level = ReadOptionalIntParam(parameters, "skill_level"),
            death_prevention_priority = ReadOptionalIntParam(parameters, "death_prevention_priority") ?? 0,
            save_bonus = ReadOptionalIntParam(parameters, "save_bonus") ?? 0,
            control_save_bonus = ReadOptionalIntParam(parameters, "control_save_bonus") ?? 0,
            passive_reduction = ReadOptionalIntParam(parameters, "passive_reduction") ?? 0,
            content_dr = ReadOptionalIntParam(parameters, "content_dr") ?? 0,
            guard_block = ReadOptionalIntParam(parameters, "guard_block") ?? 0,
            range_bonus = ReadOptionalIntParam(parameters, "range_bonus") ?? 0,
            save_advantage_tags = ReadStringNameListParam(parameters, "save_advantage_tags"),
            save_disadvantage_tags = ReadStringNameListParam(parameters, "save_disadvantage_tags"),
            save_immunity_tags = ReadStringNameListParam(parameters, "save_immunity_tags"),
            save_tags = ReadStringNameListParam(parameters, "save_tags"),
            status_tags = ReadStringNameListParam(parameters, "status_tags"),
            save_bonus_by_tag = ReadStringNameIntMapParam(parameters, "save_bonus_by_tag"),
            stacks = stacks,
            duration = durationValue,
            display_label = displayLabelValue,
            tick_interval_tu = tickIntervalValue,
            next_tick_at_tu = nextTickAtValue,
            timeline_damage_dice_count = timelineDamageDiceCountValue,
            timeline_damage_dice_sides = timelineDamageDiceSidesValue,
            timeline_damage_flat_bonus = timelineDamageFlatBonusValue,
            skip_next_turn_end_decay = skipDecayValue,
            counts_as_debuff_override = countsAsDebuffOverrideValue,
            counts_as_debuff = countsAsDebuffValue,
            lock_counterattack = lockCounterattackValue,
            lock_guard = lockGuardValue,
            lock_dodge_bonus = lockDodgeBonusValue,
            lock_crit = lockCritValue,
            main_skill_lock_other_debuff_count = mainSkillLockOtherDebuffCountValue,
        };
    }

    private GDictionary BuildParamsProjection()
    {
        GDictionary projected = RuntimePlainPayload.ProjectDictionary(
            _params,
            "BattleStatusEffectState.BuildParamsProjection"
        );
        if (incoming_damage_multiplier.HasValue)
        {
            projected["incoming_damage_multiplier"] = incoming_damage_multiplier.Value;
        }
        if (outgoing_damage_multiplier.HasValue)
        {
            projected["outgoing_damage_multiplier"] = outgoing_damage_multiplier.Value;
        }
        if (source_profile_id != "")
        {
            projected["source"] = source_profile_id;
        }
        if (source_layer_id != "")
        {
            projected["layer_id"] = source_layer_id;
        }
        if (source_skill_id != "")
        {
            projected["source_skill_id"] = source_skill_id;
        }
        if (stack_behavior != "")
        {
            projected["stack_behavior"] = stack_behavior;
        }
        if (stack_limit > 0)
        {
            projected["stack_limit"] = stack_limit;
        }
        if (heal_multiplier_percent.HasValue)
        {
            projected["heal_multiplier_percent"] = heal_multiplier_percent.Value;
        }
        if (shield_gain_multiplier_percent.HasValue)
        {
            projected["shield_gain_multiplier_percent"] = shield_gain_multiplier_percent.Value;
        }
        if (attack_roll_penalty >= 0)
        {
            projected["attack_roll_penalty"] = attack_roll_penalty;
        }
        if (source_bound_attack_roll_penalty > 0)
        {
            projected["source_bound_attack_roll_penalty"] = source_bound_attack_roll_penalty;
            projected["source_bound_attack_roll_penalty_min_stacks"] =
                Mathf.Max(source_bound_attack_roll_penalty_min_stacks, 1);
        }
        if (undispellable)
        {
            projected["undispellable"] = true;
        }
        if (dispellable_magic)
        {
            projected["dispellable_magic"] = true;
        }
        if (dispellable_harmful_magic)
        {
            projected["dispellable_harmful_magic"] = true;
        }
        if (dispellable_beneficial_magic)
        {
            projected["dispellable_beneficial_magic"] = true;
        }
        if (damage_tag != "")
        {
            projected["damage_tag"] = damage_tag;
        }
        if (damage_tags.Count > 0)
        {
            projected["damage_tags"] =
                ProgressionDataUtils.string_name_array_to_string_array(damage_tags);
        }
        if (damage_category != "")
        {
            projected["damage_category"] = damage_category;
        }
        if (body_size_category_override != "")
        {
            projected["body_size_category_override"] = body_size_category_override;
        }
        if (previous_body_size_category != "")
        {
            projected["previous_body_size_category"] = previous_body_size_category;
        }
        if (self_save_dc > 0)
        {
            projected["self_save_dc"] = self_save_dc;
        }
        if (self_save_ability != "")
        {
            projected["self_save_ability"] = self_save_ability;
        }
        if (self_save_tag != "")
        {
            projected["self_save_tag"] = self_save_tag;
        }
        if (self_save_roll_override.HasValue)
        {
            projected["self_save_roll_override"] = self_save_roll_override.Value;
        }
        if (source_skill_level.HasValue)
        {
            projected["skill_level"] = source_skill_level.Value;
        }
        if (death_prevention_priority > 0)
        {
            projected["death_prevention_priority"] = death_prevention_priority;
        }
        if (mitigation_tier != "")
        {
            projected["mitigation_tier"] = mitigation_tier;
        }
        if (dr_bypass_tag != "")
        {
            projected["dr_bypass_tag"] = dr_bypass_tag;
        }
        if (save_bonus != 0)
        {
            projected["save_bonus"] = save_bonus;
        }
        if (control_save_bonus != 0)
        {
            projected["control_save_bonus"] = control_save_bonus;
        }
        if (passive_reduction != 0)
        {
            projected["passive_reduction"] = passive_reduction;
        }
        if (content_dr != 0)
        {
            projected["content_dr"] = content_dr;
        }
        if (guard_block != 0)
        {
            projected["guard_block"] = guard_block;
        }
        if (range_bonus != 0)
        {
            projected["range_bonus"] = range_bonus;
        }
        if (save_advantage_tags.Count > 0)
        {
            projected["save_advantage_tags"] =
                ProgressionDataUtils.string_name_array_to_string_array(save_advantage_tags);
        }
        if (save_disadvantage_tags.Count > 0)
        {
            projected["save_disadvantage_tags"] =
                ProgressionDataUtils.string_name_array_to_string_array(save_disadvantage_tags);
        }
        if (save_immunity_tags.Count > 0)
        {
            projected["save_immunity_tags"] =
                ProgressionDataUtils.string_name_array_to_string_array(save_immunity_tags);
        }
        if (save_tags.Count > 0)
        {
            projected["save_tags"] =
                ProgressionDataUtils.string_name_array_to_string_array(save_tags);
        }
        if (status_tags.Count > 0)
        {
            projected["status_tags"] =
                ProgressionDataUtils.string_name_array_to_string_array(status_tags);
        }
        if (save_bonus_by_tag.Count > 0)
        {
            GDictionary projectedBonusByTag = new();
            foreach (KeyValuePair<StringName, int> entry in save_bonus_by_tag)
            {
                projectedBonusByTag[entry.Key] = entry.Value;
            }
            projected["save_bonus_by_tag"] = projectedBonusByTag;
        }
        return projected;
    }

    private void ReplaceParams(GDictionary values)
    {
        _params.Clear();
        foreach (
            KeyValuePair<string, object> entry in RuntimePlainPayload.NormalizeDictionary(
                values ?? new GDictionary(),
                "BattleStatusEffectState.@params"
            )
        )
        {
            if (!string.IsNullOrEmpty(entry.Key))
                _params[entry.Key] = entry.Value;
        }
    }

    internal static GDictionary CopyResidualParams(GDictionary parameters)
    {
        if (parameters == null || parameters.Count == 0)
        {
            return new GDictionary();
        }

        GDictionary residual = RuntimePayloadCopy.Dictionary(
            parameters,
            "BattleStatusEffectState.CopyResidualParams"
        );
        var keysToRemove = new List<Variant>();
        foreach (Variant key in parameters.Keys)
        {
            if (HasString(FormalParamKeys, key.ToString()))
            {
                keysToRemove.Add(key);
            }
        }

        foreach (Variant key in keysToRemove)
        {
            residual.Remove(key);
        }

        return residual;
    }

    private static bool HasCurrentSchemaFields(GDictionary effectDict)
    {
        foreach (string field in RequiredSchemaFields)
        {
            if (!effectDict.ContainsKey(field))
            {
                return false;
            }
        }
        foreach (var keyValue in effectDict.Keys)
        {
            string key = keyValue.ToString();
            if (!HasString(RequiredSchemaFields, key) && !HasString(OptionalSchemaFields, key))
            {
                return false;
            }
        }
        return true;
    }

    private static bool TryGetStringLike(GDictionary data, string key, out string value)
    {
        if (data != null && data.ContainsKey(key) && IsStringLikeField(data, key))
        {
            value = data[key].ToString();
            return true;
        }
        value = "";
        return false;
    }

    private static bool TryGetStrictInt(GDictionary data, string key, out int value)
    {
        if (data != null && data.ContainsKey(key) && IsFieldType(data, key, "Int"))
        {
            value = data[key].AsInt32();
            return true;
        }
        value = 0;
        return false;
    }

    private static bool TryGetDictionary(GDictionary data, string key, out GDictionary value)
    {
        if (data != null && data.ContainsKey(key) && IsFieldType(data, key, "Dictionary"))
        {
            value = data[key].AsGodotDictionary();
            return true;
        }
        value = new GDictionary();
        return false;
    }

    private static bool TryReadBoolField(GDictionary data, string key, out bool value)
    {
        if (data != null && data.ContainsKey(key) && IsFieldType(data, key, "Bool"))
        {
            value = data[key].AsBool();
            return true;
        }
        value = false;
        return false;
    }

    private static int? ReadOptionalIntParam(GDictionary parameters, string key)
    {
        if (parameters != null && parameters.ContainsKey(key) && IsFieldType(parameters, key, "Int"))
        {
            return parameters[key].AsInt32();
        }
        return null;
    }

    private static double? ReadOptionalDoubleParam(GDictionary parameters, string key)
    {
        if (parameters != null && parameters.ContainsKey(key))
        {
            Variant value = parameters[key];
            if (value.VariantType == Variant.Type.Float)
            {
                return value.AsDouble();
            }
            if (value.VariantType == Variant.Type.Int)
            {
                return value.AsInt32();
            }
        }
        return null;
    }

    private static bool ReadOptionalBoolParam(GDictionary parameters, string key)
    {
        if (parameters != null && parameters.ContainsKey(key) && IsFieldType(parameters, key, "Bool"))
        {
            return parameters[key].AsBool();
        }
        return false;
    }

    private static StringName ReadOptionalStringNameParam(GDictionary parameters, string key)
    {
        if (parameters != null && parameters.ContainsKey(key) && IsStringLikeField(parameters, key))
        {
            return ProgressionDataUtils.to_string_name(parameters[key]);
        }
        return "";
    }

    private static List<StringName> ReadStringNameListParam(GDictionary parameters, string key)
    {
        if (!TryReadArray(parameters, key, out GArray values))
        {
            return new List<StringName>();
        }
        return BuildStringNameList(ProgressionDataUtils.to_string_name_array(values));
    }

    private static Dictionary<StringName, int> ReadStringNameIntMapParam(
        GDictionary parameters,
        string key
    )
    {
        var result = new Dictionary<StringName, int>();
        if (
            parameters == null
            || string.IsNullOrEmpty(key)
            || !parameters.ContainsKey(key)
            || !IsFieldType(parameters, key, "Dictionary")
        )
        {
            return result;
        }
        GDictionary mapValue = parameters[key].AsGodotDictionary();
        foreach (Variant rawKey in mapValue.Keys)
        {
            if (rawKey.VariantType != Variant.Type.StringName)
            {
                continue;
            }
            StringName tag = rawKey.AsStringName();
            Variant rawValue = mapValue[rawKey];
            if (tag == "" || rawValue.VariantType != Variant.Type.Int)
            {
                continue;
            }
            result[tag] = rawValue.AsInt32();
        }
        return result;
    }

    private static bool TryReadArray(GDictionary parameters, string key, out GArray result)
    {
        result = new GArray();
        if (
            parameters == null
            || string.IsNullOrEmpty(key)
            || !parameters.ContainsKey(key)
            || !IsFieldType(parameters, key, "Array")
        )
        {
            return false;
        }
        result = parameters[key].AsGodotArray();
        return true;
    }

    private static bool IsStringLikeField(GDictionary data, string key)
    {
        string typeName = data[key].VariantType.ToString();
        return typeName == "String" || typeName == "StringName";
    }

    private static bool IsFieldType(GDictionary data, string key, string expectedTypeName)
    {
        return data[key].VariantType.ToString() == expectedTypeName;
    }

    private static bool HasString(string[] values, string value)
    {
        foreach (string entry in values)
        {
            if (entry == value)
            {
                return true;
            }
        }
        return false;
    }

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

    internal bool HasStatusTag(StringName tag)
    {
        if (tag == "")
        {
            return false;
        }
        foreach (StringName statusTag in status_tags)
        {
            if (statusTag == tag)
            {
                return true;
            }
        }
        return false;
    }

}
