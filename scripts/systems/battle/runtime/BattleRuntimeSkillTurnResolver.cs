using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

public readonly record struct BattleStatusTickResult(bool Changed, StringName DefeatSourceUnitId)
{
    public static BattleStatusTickResult Empty() => new(false, "");

    public GDictionary ToDictionary() =>
        new()
        {
            ["changed"] = Changed,
            ["defeat_source_unit_id"] = DefeatSourceUnitId.ToString(),
        };
}

public readonly record struct BattleTurnControlStatusResult(
    bool SkipTurn,
    bool Changed,
    bool AiControlled,
    string AiTargetPolicy,
    bool CleanupOnTurnEnd,
    bool StatusRemoved
)
{
    public static BattleTurnControlStatusResult Empty() =>
        new(false, false, false, "", false, false);

    public GDictionary ToDictionary() =>
        new()
        {
            ["skip_turn"] = SkipTurn,
            ["changed"] = Changed,
            ["ai_controlled"] = AiControlled,
            ["ai_target_policy"] = AiTargetPolicy ?? "",
            ["cleanup_on_turn_end"] = CleanupOnTurnEnd,
            ["status_removed"] = StatusRemoved,
        };
}

[GlobalClass]
public partial class BattleRuntimeSkillTurnResolver : RefCounted
{
    public static readonly StringName STATUS_PINNED = "pinned";
    public static readonly StringName STATUS_ROOTED = "rooted";
    public static readonly StringName STATUS_TENDON_CUT = "tendon_cut";
    public static readonly StringName STATUS_STAGGERED = "staggered";
    public static readonly StringName STATUS_METEOR_CONCUSSED = "meteor_concussed";
    public static readonly StringName STATUS_PETRIFIED = "petrified";
    public static readonly StringName STATUS_MADNESS = "madness";
    public static readonly StringName STATUS_GUARDING = "guarding";
    public static readonly StringName STATUS_BLACK_STAR_BRAND_NORMAL = "black_star_brand_normal";
    public static readonly StringName STATUS_CROWN_BREAK_BROKEN_HAND = "crown_break_broken_hand";
    public static readonly StringName BLACK_CONTRACT_PUSH_SKILL_ID = "black_contract_push";
    public static readonly StringName BLACK_CONTRACT_PUSH_OPTION_BLOOD = "blood_tithe";
    public static readonly StringName BLACK_CONTRACT_PUSH_OPTION_GUARD = "guard_tithe";
    public static readonly StringName BLACK_CONTRACT_PUSH_OPTION_ACTION = "action_tithe";
    public const int DOOM_SHIFT_SELF_DEBUFF_DURATION_TU = 60;
    public const int BLACK_CONTRACT_PUSH_HP_COST = 10;
    public const int TU_GRANULARITY = 5;

    private const string StatusParamBodySizeCategoryOverride = "body_size_category_override";
    private const string StatusParamPreviousBodySizeCategory = "previous_body_size_category";
    private static readonly StringName Empty = "";
    private static readonly StringName CombatResourceMp = "mp";
    private static readonly StringName CombatResourceStamina = "stamina";
    private static readonly StringName CombatResourceAura = "aura";
    private static readonly StringName UnitTargetMode = "unit";
    private static readonly StringName StatusEffectType = "status";
    private static readonly StringName SaveDcModeStatic = "static";

    private static readonly HashSet<StringName> IdentitySkillLearnSources = new()
    {
        "race",
        "subrace",
        "ascension",
        "bloodline",
    };

    private static readonly HashSet<StringName> DebuffStatusIds = new()
    {
        "armor_break",
        "black_star_brand_elite",
        "black_star_brand_normal",
        "burning",
        "crown_break_blinded_eye",
        "crown_break_broken_fang",
        "crown_break_broken_hand",
        "frozen",
        "hex_of_frailty",
        "marked",
        "meteor_concussed",
        "pinned",
        "petrified",
        "rooted",
        "shocked",
        "slow",
        "staggered",
        "taunted",
        "tendon_cut",
    };

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

    public GDictionary resolve_turn_control_status(BattleUnitState unit_state, BattleEventBatch batch)
    {
        return ResolveTurnControlStatusResult(unit_state, batch).ToDictionary();
    }

    public BattleTurnControlStatusResult ResolveTurnControlStatusResult(
        BattleUnitState unit_state,
        BattleEventBatch batch
    )
    {
        if (unit_state == null || !unit_state.is_alive)
        {
            return BattleTurnControlStatusResult.Empty();
        }
        if (HasStatus(unit_state, STATUS_PETRIFIED))
        {
            BattleStatusEffectState petrifiedEntry = GetStatusEffect(unit_state, STATUS_PETRIFIED);
            BattleSaveResult petrifiedSave = _resolve_status_self_save_result(
                unit_state,
                petrifiedEntry,
                "constitution",
                "constitution"
            );
            if (petrifiedSave.Success)
            {
                EraseStatusEffect(unit_state, STATUS_PETRIFIED);
                _append_changed_unit(batch, unit_state);
                _append_log(
                    batch,
                    $"{DisplayName(unit_state)} 通过体质检定，解除石化并立刻恢复行动。"
                );
                return new BattleTurnControlStatusResult(
                    false,
                    true,
                    false,
                    "",
                    false,
                    true
                );
            }
            unit_state.current_ap = 0;
            unit_state.current_move_points = 0;
            _append_changed_unit(batch, unit_state);
            _append_log(batch, $"{DisplayName(unit_state)} 石化未解除，无法行动。");
            return new BattleTurnControlStatusResult(true, true, false, "", false, false);
        }
        if (HasStatus(unit_state, STATUS_MADNESS))
        {
            BattleStatusEffectState madnessEntry = GetStatusEffect(unit_state, STATUS_MADNESS);
            BattleSaveResult madnessSave = _resolve_status_self_save_result(
                unit_state,
                madnessEntry,
                "willpower",
                "willpower"
            );
            if (madnessSave.Success)
            {
                EraseStatusEffect(unit_state, STATUS_MADNESS);
                clear_turn_ai_override(unit_state);
                _append_changed_unit(batch, unit_state);
                _append_log(
                    batch,
                    $"{DisplayName(unit_state)} 通过意志检定，摆脱疯狂并立刻恢复行动。"
                );
                return new BattleTurnControlStatusResult(
                    false,
                    true,
                    false,
                    "",
                    false,
                    true
                );
            }
            unit_state.ai_blackboard.madness_ai_control = true;
            unit_state.ai_blackboard.madness_target_any_team = true;
            _append_changed_unit(batch, unit_state);
            _append_log(
                batch,
                $"{DisplayName(unit_state)} 疯狂未解除，本次行动由 AI 接管且不区分敌我。"
            );
            return new BattleTurnControlStatusResult(
                false,
                true,
                true,
                "any_unit",
                true,
                false
            );
        }
        return BattleTurnControlStatusResult.Empty();
    }

    public bool is_turn_ai_override_active(BattleUnitState unit_state)
    {
        return unit_state?.ai_blackboard?.madness_ai_control ?? false;
    }

    public void clear_turn_ai_override(BattleUnitState unit_state)
    {
        if (unit_state == null)
        {
            return;
        }
        unit_state.ai_blackboard.madness_ai_control = false;
        unit_state.ai_blackboard.madness_target_any_team = false;
    }

    public GodotObject build_madness_fallback_command(GodotObject unit_state)
    {
        BattleUnitState activeUnit = unit_state as BattleUnitState;
        BattleState state = _runtime?._state;
        if (_runtime == null || state == null || activeUnit == null)
        {
            return null;
        }
        foreach (StringName skillId in activeUnit.known_active_skill_ids)
        {
            SkillDef skillDef = _runtime.get_skill_def_typed(skillId);
            CombatSkillDef combatProfile = skillDef?.combat_profile;
            if (skillDef == null || combatProfile == null)
            {
                continue;
            }
            if (combatProfile.target_mode != UnitTargetMode)
            {
                continue;
            }
            if (!string.IsNullOrEmpty(get_skill_cast_block_reason(activeUnit, skillDef)))
            {
                continue;
            }
            BattleUnitState targetUnit = _find_madness_unit_target(activeUnit, skillDef);
            if (targetUnit == null)
            {
                continue;
            }
            GodotObject command = NewBattleCommand();
            command.Set("command_type", BattleCommand.TYPE_SKILL());
            command.Set("unit_id", activeUnit.unit_id);
            command.Set("skill_id", skillId);
            command.Set("target_unit_id", targetUnit.unit_id);
            command.Set("target_coord", targetUnit.coord);
            if (_skill_requires_option(skillDef))
            {
                CombatCastVariantDef firstValue = _pick_first_valid_madness_option(activeUnit, skillDef);
                if (firstValue != null)
                {
                    command.Set(
                        "skill_variant_id",
                        new StringName(firstValue.variant_id.ToString())
                    );
                }
            }
            return command;
        }
        GodotObject waitCommand = NewBattleCommand();
        waitCommand.Set("command_type", BattleCommand.TYPE_WAIT());
        waitCommand.Set("unit_id", activeUnit.unit_id);
        return waitCommand;
    }

    public string get_skill_cast_block_reason(BattleUnitState active_unit, SkillDef skill_def)
    {
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        if (active_unit == null || skill_def == null || combatProfile == null)
        {
            return "技能或目标无效。";
        }
        CombatSkillResourceCosts costs = get_effective_skill_resource_costs(
            active_unit,
            skill_def
        );
        int cooldown = active_unit.cooldowns.ContainsKey(skill_def.skill_id)
            ? active_unit.cooldowns[skill_def.skill_id].AsInt32()
            : 0;
        if (cooldown > 0)
        {
            return $"{DisplayName(skill_def)} 仍在冷却中（{cooldown}）。";
        }
        string lockedResourceBlockReason = get_locked_combat_resource_block_reason(
            active_unit,
            costs
        );
        if (!string.IsNullOrEmpty(lockedResourceBlockReason))
        {
            return lockedResourceBlockReason;
        }
        if (
            active_unit.current_ap < costs.ApCost
        )
        {
            return "AP不足，无法施放该技能。";
        }
        if (
            active_unit.current_mp < costs.MpCost
        )
        {
            return "法力不足，无法施放该技能。";
        }
        if (
            active_unit.current_stamina < costs.StaminaCost
        )
        {
            return "体力不足，无法施放该技能。";
        }
        if (active_unit.has_status_effect(STATUS_PETRIFIED))
        {
            return "当前处于石化状态，无法施放技能。";
        }
        if (
            active_unit.current_aura < costs.AuraCost
        )
        {
            return "斗气不足，无法施放该技能。";
        }
        string racialChargeBlockReason = get_racial_skill_charge_block_reason(
            active_unit,
            skill_def
        );
        if (!string.IsNullOrEmpty(racialChargeBlockReason))
        {
            return racialChargeBlockReason;
        }
        if (
            combatProfile.required_weapon_families.Count > 0
            && !unit_matches_required_weapon_families(
                active_unit,
                combatProfile.required_weapon_families
            )
        )
        {
            return "需要装备指定武器家族，无法施放该技能。";
        }
        if (
            combatProfile.requires_equipped_shield
            && !unit_has_equipped_shield(active_unit)
        )
        {
            return "需要装备盾牌，无法施放该技能。";
        }
        if (requires_melee_weapon(skill_def) && !unit_has_melee_weapon(active_unit))
        {
            return "需要装备有效武器，无法施放该技能。";
        }
        if (
            combatProfile.excluded_weapon_families.Count > 0
            && combatProfile.excluded_weapon_families.Contains(active_unit.weapon_family)
        )
        {
            return "当前武器类型无法施放该技能。";
        }
        if (
            combatProfile.excluded_weapon_type_ids.Count > 0
            && combatProfile.excluded_weapon_type_ids.Contains(active_unit.weapon_profile_type_id)
        )
        {
            return "当前武器类型无法施放该技能。";
        }
        if (is_main_skill_locked_by_status(active_unit, skill_def))
        {
            return "厄命宣判压制了主技能，无法施放该技能。";
        }
        string misfortuneBlockReason = get_misfortune_skill_cast_block_reason(
            active_unit,
            skill_def
        );
        if (!string.IsNullOrEmpty(misfortuneBlockReason))
        {
            return misfortuneBlockReason;
        }
        if (
            active_unit.has_status_effect(STATUS_BLACK_STAR_BRAND_NORMAL)
            && _runtime._skill_grants_guarding(skill_def)
        )
        {
            return "黑星烙印封锁了格挡，无法施放该技能。";
        }
        return "";
    }

    public bool unit_has_melee_weapon(BattleUnitState active_unit) =>
        BattleRangeService.unit_has_melee_weapon(active_unit);

    public bool unit_matches_required_weapon_families(
        BattleUnitState active_unit,
        Godot.Collections.Array<StringName> required_weapon_families
    )
    {
        return BattleRangeService.unit_matches_required_weapon_families(
            active_unit,
            required_weapon_families
        );
    }

    public bool unit_has_equipped_shield(BattleUnitState active_unit)
    {
        GDictionary itemDefs = _runtime != null ? _runtime.get_item_defs() : new GDictionary();
        return BattleEquipmentRequirementRules.unit_has_equipped_shield(active_unit, itemDefs);
    }

    public bool _skill_requires_option(SkillDef skill_def)
    {
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        return skill_def != null
            && combatProfile != null
            && combatProfile.cast_variants.Count > 0;
    }

    public CombatCastVariantDef _pick_first_valid_madness_option(
        BattleUnitState unit_state,
        SkillDef skill_def
    )
    {
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        if (skill_def == null || combatProfile == null)
        {
            return null;
        }
        foreach (CombatCastVariantDef castVariant in combatProfile.cast_variants)
        {
            if (castVariant == null)
            {
                continue;
            }
            int skillLevel = _runtime._get_unit_skill_level(
                unit_state,
                skill_def.skill_id
            );
            if (skillLevel < castVariant.min_skill_level)
            {
                continue;
            }
            return castVariant;
        }
        return null;
    }

    public bool requires_melee_weapon(SkillDef skill_def) =>
        BattleRangeService.requires_current_melee_weapon(skill_def);

    public bool effect_uses_weapon_physical_damage_tag(GodotObject effect_def)
    {
        return BattleRangeService.effect_uses_weapon_physical_damage_tag(
            effect_def as CombatEffectDef
        );
    }

    public string get_skill_command_block_reason(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant
    )
    {
        string blockReason = get_skill_cast_block_reason(active_unit, skill_def);
        if (!string.IsNullOrEmpty(blockReason))
        {
            return blockReason;
        }
        if (_is_black_contract_push_skill(skill_def.skill_id))
        {
            return get_black_contract_push_variant_block_reason(active_unit, cast_variant);
        }
        return "";
    }

    public string get_misfortune_skill_cast_block_reason(
        BattleUnitState active_unit,
        SkillDef skill_def
    )
    {
        StringName skillId = skill_def?.skill_id ?? Empty;
        if (
            skill_def == null
            || !MisfortuneService.IsMisfortuneGatedSkill(skillId)
        )
        {
            return "";
        }
        if (_runtime == null)
        {
            return MisfortuneService.GetSkillSidecarMissingMessage(skillId);
        }
        return _runtime.get_misfortune_skill_cast_block_reason(active_unit, skillId);
    }

    public bool consume_skill_costs(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant = null,
        BattleEventBatch batch = null
    )
    {
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        if (active_unit == null || skill_def == null || combatProfile == null)
        {
            return false;
        }
        CombatSkillResourceCosts costs = get_effective_skill_resource_costs(
            active_unit,
            skill_def
        );
        string lockedResourceBlockReason = get_locked_combat_resource_block_reason(
            active_unit,
            costs
        );
        if (!string.IsNullOrEmpty(lockedResourceBlockReason))
        {
            AppendLog(batch, lockedResourceBlockReason);
            return false;
        }
        if (
            _is_black_contract_push_skill(skill_def.skill_id)
            && !consume_black_contract_push_cast(active_unit, cast_variant, batch)
        )
        {
            return false;
        }
        if (!consume_misfortune_skill_gate(active_unit, skill_def, batch))
        {
            return false;
        }
        if (
            !consume_racial_skill_charge(
                active_unit,
                skill_def,
                batch
            )
        )
        {
            return false;
        }
        active_unit.current_ap = Math.Max(active_unit.current_ap - costs.ApCost, 0);
        active_unit.current_mp = Math.Max(active_unit.current_mp - costs.MpCost, 0);
        active_unit.current_stamina = Math.Max(
            active_unit.current_stamina - costs.StaminaCost,
            0
        );
        active_unit.current_aura = Math.Max(active_unit.current_aura - costs.AuraCost, 0);
        int cooldown = Math.Max(costs.CooldownTu, 0);
        if (cooldown > 0)
        {
            active_unit.cooldowns[skill_def.skill_id] = cooldown;
        }
        return true;
    }

    public bool consume_misfortune_skill_gate(
        BattleUnitState active_unit,
        SkillDef skill_def,
        BattleEventBatch batch = null
    )
    {
        StringName skillId = skill_def?.skill_id ?? Empty;
        if (
            skill_def == null
            || !MisfortuneService.IsMisfortuneGatedSkill(skillId)
        )
        {
            return true;
        }
        if (_runtime == null)
        {
            AppendLog(
                batch,
                MisfortuneService.GetSkillSidecarMissingMessage(skillId)
            );
            return false;
        }
        MisfortuneSkillCastResult consumeResult = _runtime.consume_misfortune_skill_cast_result(
            active_unit,
            skillId
        );
        if (!consumeResult.Ok)
        {
            AppendLog(
                batch,
                !string.IsNullOrEmpty(consumeResult.Message)
                    ? consumeResult.Message
                    : MisfortuneService.GetSkillDefaultBlockMessage(skillId)
            );
            return false;
        }
        return true;
    }

    public string get_racial_skill_charge_block_reason(
        BattleUnitState active_unit,
        SkillDef skill_def
    )
    {
        if (active_unit == null || !_is_identity_granted_skill(skill_def))
        {
            return "";
        }
        StringName chargeKey = get_racial_skill_charge_key(skill_def.skill_id);
        GDictionary perBattleCharges = active_unit.per_battle_charges;
        GDictionary perTurnCharges = active_unit.per_turn_charges;
        if (perBattleCharges.ContainsKey(chargeKey))
        {
            if (perBattleCharges[chargeKey].AsInt32() <= 0)
            {
                return $"{_get_skill_display_name(skill_def)} 的身份技能次数已用尽。";
            }
        }
        else if (perTurnCharges.ContainsKey(chargeKey))
        {
            if (perTurnCharges[chargeKey].AsInt32() <= 0)
            {
                return $"{_get_skill_display_name(skill_def)} 本回合无法再次使用。";
            }
        }
        else
        {
            return $"{_get_skill_display_name(skill_def)} 的身份技能次数未初始化。";
        }
        return "";
    }

    public bool consume_racial_skill_charge(
        BattleUnitState active_unit,
        SkillDef skill_def,
        BattleEventBatch batch = null
    )
    {
        string blockReason = get_racial_skill_charge_block_reason(active_unit, skill_def);
        if (!string.IsNullOrEmpty(blockReason))
        {
            AppendLog(batch, blockReason);
            return false;
        }
        if (active_unit == null || !_is_identity_granted_skill(skill_def))
        {
            return true;
        }
        StringName chargeKey = get_racial_skill_charge_key(skill_def.skill_id);
        GDictionary perBattleCharges = active_unit.per_battle_charges;
        GDictionary perTurnCharges = active_unit.per_turn_charges;
        if (perBattleCharges.ContainsKey(chargeKey))
        {
            perBattleCharges[chargeKey] = Math.Max(
                perBattleCharges[chargeKey].AsInt32() - 1,
                0
            );
        }
        if (perTurnCharges.ContainsKey(chargeKey))
        {
            perTurnCharges[chargeKey] = Math.Max(
                perTurnCharges[chargeKey].AsInt32() - 1,
                0
            );
        }
        return true;
    }

    public StringName get_racial_skill_charge_key(StringName skill_id)
    {
        return skill_id == Empty ? Empty : new StringName($"racial_skill_{skill_id}");
    }

    public GDictionary get_effective_skill_costs(BattleUnitState active_unit, SkillDef skill_def)
    {
        return get_effective_skill_resource_costs(active_unit, skill_def).ToDictionary();
    }

    public CombatSkillResourceCosts get_effective_skill_resource_costs(
        BattleUnitState active_unit,
        SkillDef skill_def
    )
    {
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        if (skill_def == null || combatProfile == null)
        {
            return CombatSkillResourceCosts.Zero;
        }
        int skillLevel =
            _runtime != null ? _runtime._get_unit_skill_level(active_unit, skill_def.skill_id) : 0;
        return combatProfile.get_effective_resource_cost_values(skillLevel);
    }

    public string get_locked_combat_resource_block_reason(
        BattleUnitState active_unit,
        CombatSkillResourceCosts costs
    )
    {
        if (active_unit == null)
        {
            return "技能施放者无效。";
        }
        if (
            costs.MpCost > 0
            && !HasCombatResourceUnlocked(active_unit, CombatResourceMp)
        )
        {
            return "法力尚未解锁，无法施放该技能。";
        }
        if (
            costs.StaminaCost > 0
            && !HasCombatResourceUnlocked(active_unit, CombatResourceStamina)
        )
        {
            return "体力尚未解锁，无法施放该技能。";
        }
        if (
            costs.AuraCost > 0
            && !HasCombatResourceUnlocked(active_unit, CombatResourceAura)
        )
        {
            return "斗气尚未解锁，无法施放该技能。";
        }
        return "";
    }

    public bool _is_identity_granted_skill(SkillDef skill_def)
    {
        return skill_def != null
            && IdentitySkillLearnSources.Contains(skill_def.learn_source);
    }

    public string _get_skill_display_name(SkillDef skill_def)
    {
        if (skill_def == null)
        {
            return "身份技能";
        }
        string displayName = skill_def.display_name.Trim();
        if (!string.IsNullOrEmpty(displayName))
        {
            return displayName;
        }
        StringName skillId = skill_def.skill_id;
        return skillId != Empty ? skillId.ToString() : "身份技能";
    }

    public string get_black_contract_push_variant_block_reason(
        BattleUnitState active_unit,
        CombatCastVariantDef cast_variant
    )
    {
        if (active_unit == null)
        {
            return "技能施放者无效。";
        }
        if (cast_variant == null)
        {
            return "黑契推进需要先选择一个代价分支。";
        }
        StringName optionId = cast_variant.variant_id;
        if (
            optionId == BLACK_CONTRACT_PUSH_OPTION_BLOOD
            && active_unit.current_hp <= BLACK_CONTRACT_PUSH_HP_COST
        )
        {
            return "当前生命不足，无法支付血契代价。";
        }
        if (
            optionId == BLACK_CONTRACT_PUSH_OPTION_GUARD
            && !HasStatus(active_unit, STATUS_GUARDING)
        )
        {
            return "当前没有 Guard，无法支付护契代价。";
        }
        if (optionId == BLACK_CONTRACT_PUSH_OPTION_ACTION)
        {
            return "";
        }
        if (
            optionId != BLACK_CONTRACT_PUSH_OPTION_BLOOD
            && optionId != BLACK_CONTRACT_PUSH_OPTION_GUARD
        )
        {
            return "黑契推进的施法形态无效。";
        }
        return "";
    }

    public bool consume_black_contract_push_cast(
        BattleUnitState active_unit,
        CombatCastVariantDef cast_variant,
        BattleEventBatch batch = null
    )
    {
        string blockReason = get_black_contract_push_variant_block_reason(
            active_unit,
            cast_variant
        );
        if (!string.IsNullOrEmpty(blockReason))
        {
            AppendLog(batch, blockReason);
            return false;
        }
        if (active_unit == null || cast_variant == null)
        {
            return false;
        }
        StringName optionId = cast_variant.variant_id;
        if (optionId == BLACK_CONTRACT_PUSH_OPTION_BLOOD)
        {
            active_unit.current_hp = Math.Max(
                active_unit.current_hp - BLACK_CONTRACT_PUSH_HP_COST,
                1
            );
            AppendLog(
                batch,
                $"{DisplayName(active_unit)} 以血契推进，先失去 {BLACK_CONTRACT_PUSH_HP_COST} 点生命。"
            );
        }
        else if (optionId == BLACK_CONTRACT_PUSH_OPTION_GUARD)
        {
            EraseStatusEffect(active_unit, STATUS_GUARDING);
            AppendLog(batch, $"{DisplayName(active_unit)} 拆解了自己的 Guard，换取这次黑契推进。");
        }
        else if (optionId == BLACK_CONTRACT_PUSH_OPTION_ACTION)
        {
            _runtime._set_runtime_status_effect(
                active_unit,
                STATUS_STAGGERED,
                DOOM_SHIFT_SELF_DEBUFF_DURATION_TU,
                active_unit.unit_id,
                1,
                new GDictionary { ["counts_as_debuff"] = true }
            );
            AppendLog(
                batch,
                $"{DisplayName(active_unit)} 透支了下一回合的行动力，换取这次黑契推进。"
            );
        }
        _runtime._append_changed_unit_id(batch, active_unit.unit_id);
        return true;
    }

    public void ensure_unit_turn_anchor(BattleUnitState unit_state)
    {
        if (unit_state == null || unit_state.last_turn_tu >= 0)
        {
            return;
        }
        unit_state.last_turn_tu = _runtime?._state?.timeline?.current_tu ?? 0;
    }

    public bool advance_unit_cooldowns(BattleUnitState unit_state, int cooldown_delta)
    {
        if (unit_state == null || cooldown_delta <= 0)
        {
            return false;
        }
        GDictionary previousCooldowns = unit_state.cooldowns.Duplicate(true);
        var retainedCooldowns = new GDictionary();
        foreach (var skillIdValue in previousCooldowns.Keys)
        {
            StringName skillId = ToStringName(skillIdValue);
            int previousRemaining = previousCooldowns[skillIdValue].AsInt32();
            int remaining = Math.Max(previousRemaining - cooldown_delta, 0);
            if (remaining > 0)
            {
                retainedCooldowns[skillId] = remaining;
            }
        }
        unit_state.cooldowns = retainedCooldowns;
        return !DictionariesEqual(previousCooldowns, retainedCooldowns);
    }

    public bool consume_turn_cooldown_delta(BattleUnitState unit_state)
    {
        if (unit_state == null)
        {
            return false;
        }
        int currentTu = _runtime?._state?.timeline?.current_tu ?? 0;
        if (unit_state.last_turn_tu < 0)
        {
            unit_state.last_turn_tu = currentTu;
            return false;
        }
        int elapsedTu = Math.Max(currentTu - unit_state.last_turn_tu, 0);
        unit_state.last_turn_tu = currentTu;
        if (elapsedTu <= 0)
        {
            return false;
        }
        if (elapsedTu % TU_GRANULARITY != 0)
        {
            GameLog.Error($"Cooldown delta must use {TU_GRANULARITY} TU steps, got {elapsedTu}.", "battle.skill.invalid_cooldown_delta", "battle");
            return false;
        }
        return advance_unit_cooldowns(unit_state, elapsedTu);
    }

    public void advance_unit_turn_timers(BattleUnitState unit_state, BattleEventBatch batch)
    {
        if (unit_state == null)
        {
            return;
        }
        bool changed = consume_turn_cooldown_delta(unit_state);
        foreach (
            string statusIdString in SortedStringKeys(
                unit_state.status_effects
            )
        )
        {
            if (GetStatusEffect(unit_state, new StringName(statusIdString)) == null)
            {
                changed = true;
            }
        }
        if (changed)
        {
            _runtime._append_changed_unit_id(
                batch,
                unit_state.unit_id
            );
        }
    }

    public GDictionary apply_turn_start_statuses(BattleUnitState unit_state, BattleEventBatch batch)
    {
        return ApplyTurnStartStatusesResult(unit_state, batch).ToDictionary();
    }

    public BattleStatusTickResult ApplyTurnStartStatusesResult(
        BattleUnitState unit_state,
        BattleEventBatch batch
    )
    {
        if (unit_state == null)
        {
            return BattleStatusTickResult.Empty();
        }
        bool changed = false;
        var penaltyByGroup = new Dictionary<StringName, int>();
        var labelByGroup = new Dictionary<StringName, string>();
        var consumeStatusIds = new List<StringName>();
        foreach (
            string statusIdString in SortedStringKeys(
                unit_state.status_effects
            )
        )
        {
            StringName statusId = new(statusIdString);
            BattleStatusEffectState statusEntry = GetStatusEffect(unit_state, statusId);
            if (statusEntry == null)
            {
                continue;
            }
            int apPenalty = BattleStatusSemanticTable.get_turn_start_ap_penalty(statusEntry);
            if (apPenalty <= 0)
            {
                continue;
            }
            StringName penaltyGroup =
                BattleStatusSemanticTable.get_turn_start_ap_penalty_group(statusEntry);
            if (penaltyGroup == Empty)
            {
                penaltyGroup = statusEntry.status_id;
            }
            if (
                !penaltyByGroup.TryGetValue(penaltyGroup, out int previousPenalty)
                || apPenalty > previousPenalty
            )
            {
                penaltyByGroup[penaltyGroup] = apPenalty;
                labelByGroup[penaltyGroup] =
                    BattleStatusSemanticTable.get_turn_start_ap_penalty_display_label(
                        statusEntry
                    );
            }
            if (
                BattleStatusSemanticTable.should_consume_after_turn_start_ap_penalty(
                    statusEntry
                )
            )
            {
                consumeStatusIds.Add(statusEntry.status_id);
            }
        }
        var sortedGroupIds = new List<StringName>(penaltyByGroup.Keys);
        sortedGroupIds.Sort(
            (left, right) => StringComparer.Ordinal.Compare(left.ToString(), right.ToString())
        );
        foreach (StringName groupId in sortedGroupIds)
        {
            int groupPenalty = penaltyByGroup[groupId];
            if (groupPenalty <= 0)
            {
                continue;
            }
            int previousAp = unit_state.current_ap;
            unit_state.current_ap = Math.Max(previousAp - groupPenalty, 0);
            int consumedAp = previousAp - unit_state.current_ap;
            if (consumedAp > 0)
            {
                changed = true;
                AppendLog(
                    batch,
                    $"{DisplayName(unit_state)} 受到{labelByGroup.GetValueOrDefault(groupId, "状态")}影响，本回合少 {consumedAp} 点 AP。"
                );
            }
        }
        foreach (StringName statusId in consumeStatusIds)
        {
            if (HasStatus(unit_state, statusId))
            {
                EraseStatusEffect(unit_state, statusId);
                changed = true;
            }
        }
        if (changed)
        {
            _runtime._append_changed_unit_id(
                batch,
                unit_state.unit_id
            );
        }
        return new BattleStatusTickResult(changed, "");
    }

    public GDictionary apply_unit_status_periodic_ticks(
        BattleUnitState unit_state,
        int elapsed_tu,
        BattleEventBatch batch
    )
    {
        return ApplyUnitStatusPeriodicTicksResult(unit_state, elapsed_tu, batch).ToDictionary();
    }

    public BattleStatusTickResult ApplyUnitStatusPeriodicTicksResult(
        BattleUnitState unit_state,
        int elapsed_tu,
        BattleEventBatch batch
    )
    {
        BattleTimelineState timeline = _runtime?._state?.timeline;
        if (timeline == null || unit_state == null || elapsed_tu <= 0)
        {
            return BattleStatusTickResult.Empty();
        }
        bool changed = false;
        StringName defeatSourceUnitId = Empty;
        int currentTu = timeline.current_tu;
        int previousTu = Math.Max(currentTu - elapsed_tu, 0);
        foreach (
            string statusIdString in SortedStringKeys(
                unit_state.status_effects
            )
        )
        {
            if (!unit_state.is_alive)
            {
                break;
            }
            BattleStatusEffectState statusEntry = GetStatusEffect(
                unit_state,
                new StringName(statusIdString)
            );
            if (statusEntry == null)
            {
                continue;
            }
            int tickDamage = BattleStatusSemanticTable.get_timeline_tick_damage(statusEntry);
            if (tickDamage <= 0)
            {
                continue;
            }
            if (statusEntry.next_tick_at_tu <= previousTu)
            {
                statusEntry.next_tick_at_tu = previousTu + statusEntry.tick_interval_tu;
                changed = true;
            }
            int tickLimitTu = currentTu;
            if (statusEntry.has_duration())
            {
                tickLimitTu = Math.Min(
                    tickLimitTu,
                    previousTu + statusEntry.duration
                );
            }
            while (
                unit_state.is_alive
                && statusEntry.next_tick_at_tu > 0
                && statusEntry.next_tick_at_tu <= tickLimitTu
            )
            {
                int previousHp = unit_state.current_hp;
                unit_state.current_hp = Math.Max(previousHp - tickDamage, 0);
                unit_state.is_alive = unit_state.current_hp > 0;
                statusEntry.next_tick_at_tu += statusEntry.tick_interval_tu;
                if (unit_state.current_hp != previousHp)
                {
                    changed = true;
                    AppendLog(
                        batch,
                        $"{DisplayName(unit_state)} 受到 {statusEntry.status_id} 持续影响，损失 {previousHp - unit_state.current_hp} 点生命。"
                    );
                    if (
                        !unit_state.is_alive
                        && statusEntry.source_unit_id != Empty
                    )
                    {
                        defeatSourceUnitId = statusEntry.source_unit_id;
                    }
                }
            }
            if (unit_state.is_alive)
            {
                unit_state.set_status_effect(statusEntry);
            }
        }
        return new BattleStatusTickResult(changed, defeatSourceUnitId);
    }

    public bool advance_unit_status_durations(
        BattleUnitState unit_state,
        int elapsed_tu,
        BattleEventBatch batch = null
    )
    {
        if (unit_state == null)
        {
            return false;
        }
        bool changed = false;
        var expiredStatusIds = new List<StringName>();
        var expiredStatusEntries = new Dictionary<StringName, BattleStatusEffectState>();
        foreach (
            string statusIdString in SortedStringKeys(
                unit_state.status_effects
            )
        )
        {
            StringName statusId = new(statusIdString);
            BattleStatusEffectState statusEntry = GetStatusEffect(unit_state, statusId);
            if (statusEntry == null)
            {
                expiredStatusIds.Add(statusId);
                changed = true;
                continue;
            }
            BattleStatusDurationAdvanceResult durationResult =
                BattleStatusSemanticTable.advance_timeline_duration_result(
                statusEntry,
                elapsed_tu
            );
            if (durationResult.Expired)
            {
                expiredStatusIds.Add(statusId);
                expiredStatusEntries[statusId] = statusEntry;
                changed = true;
                continue;
            }
            if (durationResult.Changed)
            {
                unit_state.set_status_effect(statusEntry);
                changed = true;
            }
        }
        foreach (StringName expiredStatusId in expiredStatusIds)
        {
            expiredStatusEntries.TryGetValue(
                expiredStatusId,
                out BattleStatusEffectState expiredStatusEntry
            );
            bool shouldEraseStatus = true;
            if (_is_body_size_category_override_status(expiredStatusEntry))
            {
                shouldEraseStatus = false;
                if (
                    _restore_body_size_category_override_if_needed(
                        unit_state,
                        expiredStatusEntry,
                        batch
                    )
                )
                {
                    changed = true;
                    shouldEraseStatus = true;
                }
                else if (_body_size_already_matches_previous(unit_state, expiredStatusEntry))
                {
                    shouldEraseStatus = true;
                }
            }
            if (shouldEraseStatus)
            {
                EraseStatusEffect(unit_state, expiredStatusId);
            }
        }
        return changed;
    }

    public bool _body_size_already_matches_previous(
        BattleUnitState unit_state,
        BattleStatusEffectState status_entry
    )
    {
        if (unit_state == null || status_entry == null)
        {
            return false;
        }
        GDictionary parameters = status_entry.@params;
        if (!parameters.ContainsKey(StatusParamBodySizeCategoryOverride))
        {
            return false;
        }
        StringName previousCategory = parameters.ContainsKey(StatusParamPreviousBodySizeCategory)
            ? ProgressionDataUtils.to_string_name(parameters[StatusParamPreviousBodySizeCategory])
            : Empty;
        if (!BodySizeRules.is_valid_body_size_category(previousCategory))
        {
            return false;
        }
        return unit_state.body_size_category == previousCategory;
    }

    public bool _is_body_size_category_override_status(BattleStatusEffectState status_entry)
    {
        return status_entry != null
            && status_entry.@params.ContainsKey(StatusParamBodySizeCategoryOverride);
    }

    public bool _restore_body_size_category_override_if_needed(
        BattleUnitState unit_state,
        BattleStatusEffectState status_entry,
        BattleEventBatch batch = null
    )
    {
        if (unit_state == null || status_entry == null)
        {
            return false;
        }
        GDictionary parameters = status_entry.@params;
        if (!parameters.ContainsKey(StatusParamBodySizeCategoryOverride))
        {
            return false;
        }
        StringName previousCategory = parameters.ContainsKey(StatusParamPreviousBodySizeCategory)
            ? ProgressionDataUtils.to_string_name(parameters[StatusParamPreviousBodySizeCategory])
            : Empty;
        if (!BodySizeRules.is_valid_body_size_category(previousCategory))
        {
            return false;
        }
        if (unit_state.body_size_category == previousCategory)
        {
            return false;
        }
        GArray previousCoords = ToUntypedCoordArray(unit_state.occupied_coords);
        StringName currentCategory = unit_state.body_size_category;
        BattleRuntimeModule runtime = _runtime;
        BattleGridService gridService = runtime?.get_grid_service();
        BattleState state = runtime?._state;
        if (gridService != null && state != null)
        {
            gridService.clear_unit_occupancy(state, unit_state);
        }
        unit_state.set_body_size_category(previousCategory);
        if (gridService != null && state != null)
        {
            if (
                !gridService.can_place_unit(
                    state,
                    unit_state,
                    unit_state.coord,
                    true
                )
            )
            {
                unit_state.set_body_size_category(currentCategory);
                gridService.set_occupants(
                    state,
                    previousCoords,
                    unit_state.unit_id
                );
                return false;
            }
            gridService.set_occupants(
                state,
                ToUntypedCoordArray(unit_state.occupied_coords),
                unit_state.unit_id
            );
        }
        if (runtime != null && batch != null)
        {
            runtime._append_changed_coords(batch, previousCoords);
            runtime._append_changed_unit_coords(batch, unit_state);
            runtime._append_changed_unit_id(batch, unit_state.unit_id);
        }
        return true;
    }

    public int get_effective_skill_range(BattleUnitState active_unit, SkillDef skill_def) =>
        BattleRangeService.get_effective_skill_range(
            active_unit,
            skill_def
        );

    public int resolve_base_skill_range(BattleUnitState active_unit, SkillDef skill_def) =>
        BattleRangeService.resolve_base_skill_range(
            active_unit,
            skill_def
        );

    public bool is_weapon_range_skill(SkillDef skill_def) =>
        BattleRangeService.is_weapon_range_skill(skill_def);

    public int get_weapon_attack_range(BattleUnitState active_unit) =>
        BattleRangeService.get_weapon_attack_range(active_unit);

    public bool skill_has_tag(SkillDef skill_def, StringName expected_tag)
    {
        if (skill_def == null || expected_tag == Empty)
        {
            return false;
        }
        foreach (StringName tag in skill_def.tags)
        {
            if (tag == expected_tag)
            {
                return true;
            }
        }
        return false;
    }

    public bool is_movement_blocked(BattleUnitState unit_state)
    {
        return has_status(unit_state, STATUS_PINNED)
            || has_status(unit_state, STATUS_ROOTED)
            || has_status(unit_state, STATUS_TENDON_CUT)
            || has_status(unit_state, STATUS_PETRIFIED);
    }

    public bool has_status(BattleUnitState unit_state, StringName status_id)
    {
        return unit_state != null
            && status_id != Empty
            && HasStatus(unit_state, status_id);
    }

    public GDictionary _resolve_status_self_save(
        BattleUnitState unit_state,
        BattleStatusEffectState status_entry,
        StringName fallback_ability,
        StringName fallback_tag
    )
    {
        return _resolve_status_self_save_result(
                unit_state,
                status_entry,
                fallback_ability,
                fallback_tag
            )
            .ToDictionary();
    }

    public BattleSaveResult _resolve_status_self_save_result(
        BattleUnitState unit_state,
        BattleStatusEffectState status_entry,
        StringName fallback_ability,
        StringName fallback_tag
    )
    {
        GDictionary parameters =
            status_entry != null
                ? status_entry.@params
                : new GDictionary();
        CombatEffectDef effect = new CombatEffectDef();
        effect.effect_type = StatusEffectType;
        effect.save_dc = Math.Max(
            parameters.ContainsKey("self_save_dc")
                ? parameters["self_save_dc"].AsInt32()
                : 16,
            1
        );
        effect.save_dc_mode = SaveDcModeStatic;
        effect.save_ability = parameters.ContainsKey("self_save_ability")
            ? ProgressionDataUtils.to_string_name(parameters["self_save_ability"])
            : fallback_ability;
        effect.save_tag = parameters.ContainsKey("self_save_tag")
            ? ProgressionDataUtils.to_string_name(parameters["self_save_tag"])
            : fallback_tag;
        var context = new GDictionary();
        if (parameters.ContainsKey("self_save_roll_override"))
        {
            context["save_roll_override"] = parameters["self_save_roll_override"].AsInt32();
        }
        BattleUnitState sourceUnit = null;
        if (
            _runtime != null
            && _runtime._state != null
            && status_entry != null
            && status_entry.source_unit_id != Empty
        )
        {
            _runtime._state.TryGetUnitTyped(status_entry.source_unit_id, out sourceUnit);
        }
        return BattleSaveResolver.resolve_save_result(
            sourceUnit,
            unit_state,
            effect,
            context
        );
    }

    public BattleUnitState _find_madness_unit_target(BattleUnitState unit_state, SkillDef skill_def)
    {
        BattleState state = _runtime?._state;
        if (_runtime == null || state == null || unit_state == null)
        {
            return null;
        }
        BattleUnitState bestUnit = null;
        int bestDistance = 999999;
        int effectiveRange = _runtime._get_effective_skill_range(
            unit_state,
            skill_def
        );
        foreach (BattleUnitState candidate in state.GetUnitsTyped())
        {
            if (
                candidate == null
                || !candidate.is_alive
                || candidate.unit_id == unit_state.unit_id
            )
            {
                continue;
            }
            int distance = _runtime.get_grid_service().get_distance_between_units(
                unit_state,
                candidate
            );
            if (distance > effectiveRange)
            {
                continue;
            }
            if (bestUnit == null || distance < bestDistance)
            {
                bestUnit = candidate;
                bestDistance = distance;
            }
        }
        return bestUnit;
    }

    public void _append_changed_unit(BattleEventBatch batch, BattleUnitState unit_state)
    {
        if (_runtime == null || batch == null || unit_state == null)
        {
            return;
        }
        _runtime._append_changed_unit_id(batch, unit_state.unit_id);
        _runtime._append_changed_unit_coords(batch, unit_state);
    }

    public void _append_log(BattleEventBatch batch, string line)
    {
        AppendLog(batch, line);
    }

    public void consume_status_if_present(
        BattleUnitState unit_state,
        StringName status_id,
        BattleEventBatch batch = null
    )
    {
        if (unit_state == null || status_id == Empty || !HasStatus(unit_state, status_id))
        {
            return;
        }
        EraseStatusEffect(unit_state, status_id);
        if (batch != null)
        {
            _runtime._append_changed_unit_id(
                batch,
                unit_state.unit_id
            );
        }
    }

    public bool is_main_skill_locked_by_status(BattleUnitState active_unit, SkillDef skill_def)
    {
        if (active_unit == null || skill_def == null)
        {
            return false;
        }
        if (active_unit.known_active_skill_ids.Count == 0)
        {
            return false;
        }
        if (active_unit.known_active_skill_ids[0] != skill_def.skill_id)
        {
            return false;
        }
        int requiredDebuffCount = get_status_param_max_int(
            active_unit,
            "main_skill_lock_other_debuff_count"
        );
        if (requiredDebuffCount <= 0)
        {
            return false;
        }
        return count_debuff_statuses(active_unit) >= requiredDebuffCount;
    }

    public int count_debuff_statuses(BattleUnitState unit_state)
    {
        if (unit_state == null)
        {
            return 0;
        }
        int debuffCount = 0;
        foreach (string statusIdString in SortedStringKeys(unit_state.status_effects))
        {
            StringName statusId = new(statusIdString);
            BattleStatusEffectState statusEntry = GetStatusEffect(unit_state, statusId);
            if (statusEntry == null)
            {
                continue;
            }
            if (status_counts_as_debuff(statusId, statusEntry))
            {
                debuffCount += 1;
            }
        }
        return debuffCount;
    }

    public bool status_counts_as_debuff(
        StringName status_id,
        BattleStatusEffectState status_entry
    )
    {
        if (status_entry != null)
        {
            GDictionary parameters = status_entry.@params;
            if (_status_params_has_formal_key(parameters, "counts_as_debuff"))
            {
                return StatusParamsGetFormalBool(parameters, "counts_as_debuff", false);
            }
        }
        return DebuffStatusIds.Contains(status_id);
    }

    public bool has_status_param_bool(BattleUnitState unit_state, StringName param_key)
    {
        if (unit_state == null || param_key == Empty)
        {
            return false;
        }
        foreach (string statusIdString in SortedStringKeys(unit_state.status_effects))
        {
            BattleStatusEffectState statusEntry = GetStatusEffect(
                unit_state,
                new StringName(statusIdString)
            );
            if (statusEntry == null)
            {
                continue;
            }
            bool value = StatusParamsGetFormalBool(
                statusEntry.@params,
                param_key.ToString(),
                false
            );
            if (value)
            {
                return true;
            }
        }
        return false;
    }

    public int get_status_param_max_int(BattleUnitState unit_state, StringName param_key)
    {
        if (unit_state == null || param_key == Empty)
        {
            return 0;
        }
        int maxValue = 0;
        foreach (string statusIdString in SortedStringKeys(unit_state.status_effects))
        {
            BattleStatusEffectState statusEntry = GetStatusEffect(
                unit_state,
                new StringName(statusIdString)
            );
            if (statusEntry == null)
            {
                continue;
            }
            int value = StatusParamsGetFormalInt(
                statusEntry.@params,
                param_key.ToString(),
                0
            );
            maxValue = Math.Max(value, maxValue);
        }
        return maxValue;
    }

    private static bool _status_params_has_formal_key(GDictionary parameters, string param_key)
    {
        return TryGetFormalStatusParam(parameters, param_key, out _);
    }

    private static bool StatusParamsGetFormalBool(
        GDictionary parameters,
        string param_key,
        bool default_value = false
    )
    {
        if (!TryGetFormalStatusParam(parameters, param_key, out Variant value))
        {
            return default_value;
        }
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : default_value;
    }

    private static int StatusParamsGetFormalInt(
        GDictionary parameters,
        string param_key,
        int default_value = 0
    )
    {
        return TryGetFormalStatusParam(parameters, param_key, out Variant value)
            ? ValueAsInt(value)
            : default_value;
    }

    private static bool TryGetFormalStatusParam(
        GDictionary parameters,
        string param_key,
        out Variant value
    )
    {
        if (parameters != null && !string.IsNullOrEmpty(param_key))
        {
            foreach (var key in parameters.Keys)
            {
                if (key.VariantType == Variant.Type.String && key.AsString() == param_key)
                {
                    value = parameters[key];
                    return true;
                }
            }
        }
        value = default;
        return false;
    }

    public bool _is_black_contract_push_skill(StringName skill_id)
    {
        return skill_id == BLACK_CONTRACT_PUSH_SKILL_ID;
    }

    private static GodotObject NewBattleCommand()
    {
        return new BattleCommand();
    }

    private static bool HasCombatResourceUnlocked(BattleUnitState unit, StringName resourceId)
    {
        return unit?.has_combat_resource_unlocked(resourceId) ?? false;
    }

    private static bool HasStatus(GodotObject unit, StringName statusId)
    {
        return (unit as BattleUnitState)?.has_status_effect(statusId) ?? false;
    }

    private static BattleStatusEffectState GetStatusEffect(GodotObject unit, StringName statusId)
    {
        return (unit as BattleUnitState)?.get_status_effect(statusId);
    }

    private static void EraseStatusEffect(GodotObject unit, StringName statusId)
    {
        (unit as BattleUnitState)?.erase_status_effect(statusId);
    }

    private static void AppendLog(BattleEventBatch batch, string line)
    {
        if (batch == null || string.IsNullOrEmpty(line))
        {
            return;
        }
        batch.log_lines.Add(line);
    }

    private static string DisplayName(GodotObject value)
    {
        return value switch
        {
            BattleUnitState unitState => unitState.display_name,
            SkillDef skillDef => !string.IsNullOrEmpty(skillDef.display_name)
                ? skillDef.display_name
                : skillDef.skill_id.ToString(),
            _ => "",
        };
    }

    private static StringName ToStringName(Variant value)
    {
        return value.VariantType == Variant.Type.StringName
            ? value.AsStringName()
            : new StringName(value.ToString());
    }

    private static int ValueAsInt(Variant value)
    {
        return value.AsInt32();
    }

    private static List<string> SortedStringKeys(GDictionary dictionary)
    {
        var keys = new List<string>();
        foreach (var key in dictionary.Keys)
        {
            keys.Add(key.ToString());
        }
        keys.Sort(StringComparer.Ordinal);
        return keys;
    }

    private static GArray ToUntypedCoordArray(GVector2IArray source)
    {
        var result = new GArray();
        foreach (Vector2I coord in source ?? new GVector2IArray())
        {
            result.Add(coord);
        }
        return result;
    }

    private static bool DictionariesEqual(GDictionary left, GDictionary right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }
        foreach (var key in left.Keys)
        {
            if (
                !right.ContainsKey(key)
                || !left[key].Equals(right[key])
            )
            {
                return false;
            }
        }
        return true;
    }

    private static T ResolveWeakRef<T>(WeakReference<T> weakRef)
        where T : GodotObject
    {
        if (
            weakRef == null
            || !weakRef.TryGetTarget(out T target)
            || !GodotObject.IsInstanceValid(target)
        )
        {
            return null;
        }
        return target;
    }
}
