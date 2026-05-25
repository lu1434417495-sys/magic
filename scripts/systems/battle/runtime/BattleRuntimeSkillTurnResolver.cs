using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

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
    public static readonly StringName BLACK_CONTRACT_PUSH_VARIANT_BLOOD = "blood_tithe";
    public static readonly StringName BLACK_CONTRACT_PUSH_VARIANT_GUARD = "guard_tithe";
    public static readonly StringName BLACK_CONTRACT_PUSH_VARIANT_ACTION = "action_tithe";
    public const int DOOM_SHIFT_SELF_DEBUFF_DURATION_TU = 60;
    public const int BLACK_CONTRACT_PUSH_HP_COST = 10;
    public const int TU_GRANULARITY = 5;

    private const string StatusParamBodySizeCategoryOverride = "body_size_category_override";
    private const string StatusParamPreviousBodySizeCategory = "previous_body_size_category";
    private const string AiBlackboardTurnOverride = "madness_ai_control";
    private const string AiBlackboardAnyUnitTargeting = "madness_target_any_team";

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

    private static readonly Script BattleCommandScript = GD.Load<Script>("res://scripts/systems/battle/core/BattleCommand.cs");
    private static readonly Script BattleStatusSemanticTableScript = GD.Load<Script>("res://scripts/systems/battle/rules/battle_status_semantic_table.gd");
    private static readonly Script BattleSaveResolverScript = GD.Load<Script>("res://scripts/systems/battle/rules/battle_save_resolver.gd");
    private static readonly Script BodySizeRulesScript = GD.Load<Script>("res://scripts/systems/progression/body_size_rules.gd");
    private static readonly Script MisfortuneServiceScript = GD.Load<Script>("res://scripts/systems/battle/fate/misfortune_service.gd");
    private static readonly Script CombatEffectDefScript = GD.Load<Script>("res://scripts/player/progression/combat_effect_def.gd");

    private WeakReference<GodotObject> _runtimeRef;

    private GodotObject _runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<GodotObject>(value) : null;
    }

    public void setup(GodotObject runtime)
    {
        _runtime = runtime;
    }

    public void dispose()
    {
        _runtime = null;
    }

    public GDictionary resolve_turn_control_status(GodotObject unit_state, GodotObject batch)
    {
        var result = new GDictionary
        {
            ["skip_turn"] = false,
            ["changed"] = false,
            ["ai_controlled"] = false,
            ["ai_target_policy"] = "",
            ["cleanup_on_turn_end"] = false,
            ["status_removed"] = false,
        };
        if (unit_state == null || !GdInterop.GetBool(unit_state, "is_alive"))
        {
            return result;
        }
        if (HasStatus(unit_state, STATUS_PETRIFIED))
        {
            GodotObject petrifiedEntry = GetStatusEffect(unit_state, STATUS_PETRIFIED);
            GDictionary petrifiedSave = _resolve_status_self_save(unit_state, petrifiedEntry, "constitution", "constitution");
            if (GdInterop.GetBool(petrifiedSave, "success", false))
            {
                EraseStatusEffect(unit_state, STATUS_PETRIFIED);
                _append_changed_unit(batch, unit_state);
                _append_log(batch, $"{DisplayName(unit_state)} 通过体质检定，解除石化并立刻恢复行动。");
                result["changed"] = true;
                result["status_removed"] = true;
                return result;
            }
            unit_state.Set("current_ap", 0);
            unit_state.Set("current_move_points", 0);
            _append_changed_unit(batch, unit_state);
            _append_log(batch, $"{DisplayName(unit_state)} 石化未解除，无法行动。");
            result["skip_turn"] = true;
            result["changed"] = true;
            return result;
        }
        if (HasStatus(unit_state, STATUS_MADNESS))
        {
            GodotObject madnessEntry = GetStatusEffect(unit_state, STATUS_MADNESS);
            GDictionary madnessSave = _resolve_status_self_save(unit_state, madnessEntry, "willpower", "willpower");
            if (GdInterop.GetBool(madnessSave, "success", false))
            {
                EraseStatusEffect(unit_state, STATUS_MADNESS);
                clear_turn_ai_override(unit_state);
                _append_changed_unit(batch, unit_state);
                _append_log(batch, $"{DisplayName(unit_state)} 通过意志检定，摆脱疯狂并立刻恢复行动。");
                result["changed"] = true;
                result["status_removed"] = true;
                return result;
            }
            GDictionary blackboard = GdInterop.GetDictionary(unit_state, "ai_blackboard");
            blackboard[AiBlackboardTurnOverride] = true;
            blackboard[AiBlackboardAnyUnitTargeting] = true;
            unit_state.Set("ai_blackboard", blackboard);
            _append_changed_unit(batch, unit_state);
            _append_log(batch, $"{DisplayName(unit_state)} 疯狂未解除，本次行动由 AI 接管且不区分敌我。");
            result["changed"] = true;
            result["ai_controlled"] = true;
            result["ai_target_policy"] = "any_unit";
            result["cleanup_on_turn_end"] = true;
        }
        return result;
    }

    public bool is_turn_ai_override_active(GodotObject unit_state)
    {
        return unit_state != null && GdInterop.GetBool(GdInterop.GetDictionary(unit_state, "ai_blackboard"), AiBlackboardTurnOverride, false);
    }

    public void clear_turn_ai_override(GodotObject unit_state)
    {
        if (unit_state == null)
        {
            return;
        }
        GDictionary blackboard = GdInterop.GetDictionary(unit_state, "ai_blackboard");
        blackboard.Remove(AiBlackboardTurnOverride);
        blackboard.Remove(AiBlackboardAnyUnitTargeting);
        unit_state.Set("ai_blackboard", blackboard);
    }

    public GodotObject build_madness_fallback_command(GodotObject unit_state)
    {
        GodotObject state = GdInterop.GetObject(_runtime, "_state");
        if (_runtime == null || state == null || unit_state == null)
        {
            return null;
        }
        GDictionary skillDefs = GdInterop.GetDictionary(_runtime, "_skill_defs");
        foreach (Variant rawSkillId in GdInterop.GetArray(unit_state, "known_active_skill_ids"))
        {
            StringName skillId = ToStringName(rawSkillId);
            GodotObject skillDef = GdInterop.GetObject(skillDefs, skillId);
            GodotObject combatProfile = GdInterop.GetObject(skillDef, "combat_profile");
            if (skillDef == null || combatProfile == null)
            {
                continue;
            }
            if (GdInterop.GetStringName(combatProfile, "target_mode") != UnitTargetMode)
            {
                continue;
            }
            if (!string.IsNullOrEmpty(get_skill_cast_block_reason(unit_state, skillDef)))
            {
                continue;
            }
            GodotObject targetUnit = _find_madness_unit_target(unit_state, skillDef);
            if (targetUnit == null)
            {
                continue;
            }
            GodotObject command = NewBattleCommand();
            command.Set("command_type", BattleCommand.TYPE_SKILL());
            command.Set("unit_id", GdInterop.GetStringName(unit_state, "unit_id"));
            command.Set("skill_id", skillId);
            command.Set("target_unit_id", GdInterop.GetStringName(targetUnit, "unit_id"));
            command.Set("target_coord", GdInterop.GetVector2I(targetUnit, "coord"));
            if (_skill_requires_variant(skillDef))
            {
                GodotObject firstVariant = _pick_first_valid_madness_variant(unit_state, skillDef);
                if (firstVariant != null)
                {
                    command.Set("skill_variant_id", new StringName(GdInterop.GetStringName(firstVariant, "variant_id").ToString()));
                }
            }
            return command;
        }
        GodotObject waitCommand = NewBattleCommand();
        waitCommand.Set("command_type", BattleCommand.TYPE_WAIT());
        waitCommand.Set("unit_id", GdInterop.GetStringName(unit_state, "unit_id"));
        return waitCommand;
    }

    public string get_skill_cast_block_reason(GodotObject active_unit, GodotObject skill_def)
    {
        GodotObject combatProfile = GdInterop.GetObject(skill_def, "combat_profile");
        if (active_unit == null || skill_def == null || combatProfile == null)
        {
            return "技能或目标无效。";
        }
        GDictionary costs = get_effective_skill_costs(active_unit, skill_def);
        int cooldown = GdInterop.GetInt(GdInterop.GetDictionary(active_unit, "cooldowns"), GdInterop.GetStringName(skill_def, "skill_id"), 0);
        if (cooldown > 0)
        {
            return $"{DisplayName(skill_def)} 仍在冷却中（{cooldown}）。";
        }
        string lockedResourceBlockReason = get_locked_combat_resource_block_reason(active_unit, costs);
        if (!string.IsNullOrEmpty(lockedResourceBlockReason))
        {
            return lockedResourceBlockReason;
        }
        if (GdInterop.GetInt(active_unit, "current_ap") < GdInterop.GetInt(costs, "ap_cost", GdInterop.GetInt(combatProfile, "ap_cost")))
        {
            return "AP不足，无法施放该技能。";
        }
        if (GdInterop.GetInt(active_unit, "current_mp") < GdInterop.GetInt(costs, "mp_cost", GdInterop.GetInt(combatProfile, "mp_cost")))
        {
            return "法力不足，无法施放该技能。";
        }
        if (GdInterop.GetInt(active_unit, "current_stamina") < GdInterop.GetInt(costs, "stamina_cost", GdInterop.GetInt(combatProfile, "stamina_cost")))
        {
            return "体力不足，无法施放该技能。";
        }
        if (has_status(active_unit, STATUS_PETRIFIED))
        {
            return "当前处于石化状态，无法施放技能。";
        }
        if (GdInterop.GetInt(active_unit, "current_aura") < GdInterop.GetInt(costs, "aura_cost", GdInterop.GetInt(combatProfile, "aura_cost")))
        {
            return "斗气不足，无法施放该技能。";
        }
        string racialChargeBlockReason = get_racial_skill_charge_block_reason(active_unit, skill_def);
        if (!string.IsNullOrEmpty(racialChargeBlockReason))
        {
            return racialChargeBlockReason;
        }
        if (GdInterop.GetArray(combatProfile, "required_weapon_families").Count > 0
            && !unit_matches_required_weapon_families(active_unit, GdInterop.GetArray(combatProfile, "required_weapon_families")))
        {
            return "需要装备指定武器家族，无法施放该技能。";
        }
        if (GdInterop.GetBool(combatProfile, "requires_equipped_shield") && !unit_has_equipped_shield(active_unit))
        {
            return "需要装备盾牌，无法施放该技能。";
        }
        if (requires_melee_weapon(skill_def) && !unit_has_melee_weapon(active_unit))
        {
            return "需要装备有效武器，无法施放该技能。";
        }
        if (GdInterop.GetArray(combatProfile, "excluded_weapon_families").Count > 0
            && GdInterop.GetArray(combatProfile, "excluded_weapon_families").Contains(GdInterop.GetStringName(active_unit, "weapon_family")))
        {
            return "当前武器类型无法施放该技能。";
        }
        if (GdInterop.GetArray(combatProfile, "excluded_weapon_type_ids").Count > 0
            && GdInterop.GetArray(combatProfile, "excluded_weapon_type_ids").Contains(GdInterop.GetStringName(active_unit, "weapon_profile_type_id")))
        {
            return "当前武器类型无法施放该技能。";
        }
        if (is_main_skill_locked_by_status(active_unit, skill_def))
        {
            return "厄命宣判压制了主技能，无法施放该技能。";
        }
        string misfortuneBlockReason = get_misfortune_skill_cast_block_reason(active_unit, skill_def);
        if (!string.IsNullOrEmpty(misfortuneBlockReason))
        {
            return misfortuneBlockReason;
        }
        if (has_status(active_unit, STATUS_BLACK_STAR_BRAND_NORMAL) && _runtime.Call("_skill_grants_guarding", skill_def).AsBool())
        {
            return "黑星烙印封锁了格挡，无法施放该技能。";
        }
        return "";
    }

    public bool unit_has_melee_weapon(GodotObject active_unit) => BattleRangeService.unit_has_melee_weapon(active_unit);

    public bool unit_matches_required_weapon_families(GodotObject active_unit, GArray required_weapon_families)
    {
        return BattleRangeService.unit_matches_required_weapon_families(active_unit, required_weapon_families);
    }

    public bool unit_has_equipped_shield(GodotObject active_unit)
    {
        GDictionary itemDefs = _runtime != null && _runtime.HasMethod("get_item_defs")
            ? _runtime.Call("get_item_defs").AsGodotDictionary()
            : new GDictionary();
        return BattleEquipmentRequirementRules.unit_has_equipped_shield(active_unit, itemDefs);
    }

    public bool _skill_requires_variant(GodotObject skill_def)
    {
        GodotObject combatProfile = GdInterop.GetObject(skill_def, "combat_profile");
        return skill_def != null && combatProfile != null && GdInterop.GetArray(combatProfile, "cast_variants").Count > 0;
    }

    public GodotObject _pick_first_valid_madness_variant(GodotObject unit_state, GodotObject skill_def)
    {
        GodotObject combatProfile = GdInterop.GetObject(skill_def, "combat_profile");
        if (skill_def == null || combatProfile == null)
        {
            return null;
        }
        foreach (Variant variant in GdInterop.GetArray(combatProfile, "cast_variants"))
        {
            GodotObject castVariant = variant.AsGodotObject();
            if (castVariant == null)
            {
                continue;
            }
            int minLevel = GdInterop.GetInt(castVariant, "min_skill_level");
            int skillLevel = GdInterop.GetInt(GdInterop.GetDictionary(unit_state, "known_skill_level_map"), GdInterop.GetStringName(skill_def, "skill_id"), 0);
            if (skillLevel < minLevel)
            {
                continue;
            }
            return castVariant;
        }
        return null;
    }

    public bool requires_melee_weapon(GodotObject skill_def) => BattleRangeService.requires_current_melee_weapon(skill_def);

    public bool effect_uses_weapon_physical_damage_tag(GodotObject effect_def)
    {
        return BattleRangeService.effect_uses_weapon_physical_damage_tag(effect_def);
    }

    public string get_skill_command_block_reason(GodotObject active_unit, GodotObject skill_def, GodotObject cast_variant)
    {
        string blockReason = get_skill_cast_block_reason(active_unit, skill_def);
        if (!string.IsNullOrEmpty(blockReason))
        {
            return blockReason;
        }
        if (_is_black_contract_push_skill(GdInterop.GetStringName(skill_def, "skill_id")))
        {
            return get_black_contract_push_variant_block_reason(active_unit, cast_variant);
        }
        return "";
    }

    public string get_misfortune_skill_cast_block_reason(GodotObject active_unit, GodotObject skill_def)
    {
        StringName skillId = GdInterop.GetStringName(skill_def, "skill_id");
        if (skill_def == null || !CallScriptBool(MisfortuneServiceScript, "is_misfortune_gated_skill", skillId))
        {
            return "";
        }
        if (_runtime == null || !_runtime.HasMethod("get_misfortune_skill_cast_block_reason"))
        {
            return CallScriptString(MisfortuneServiceScript, "get_skill_sidecar_missing_message", skillId);
        }
        return _runtime.Call("get_misfortune_skill_cast_block_reason", active_unit, skillId).AsString();
    }

    public bool consume_skill_costs(GodotObject active_unit, GodotObject skill_def, GodotObject cast_variant = null, GodotObject batch = null)
    {
        GodotObject combatProfile = GdInterop.GetObject(skill_def, "combat_profile");
        if (active_unit == null || skill_def == null || combatProfile == null)
        {
            return false;
        }
        GDictionary costs = get_effective_skill_costs(active_unit, skill_def);
        string lockedResourceBlockReason = get_locked_combat_resource_block_reason(active_unit, costs);
        if (!string.IsNullOrEmpty(lockedResourceBlockReason))
        {
            AppendLog(batch, lockedResourceBlockReason);
            return false;
        }
        if (_is_black_contract_push_skill(GdInterop.GetStringName(skill_def, "skill_id"))
            && !consume_black_contract_push_cast(active_unit, cast_variant, batch))
        {
            return false;
        }
        if (!consume_misfortune_skill_gate(active_unit, skill_def, batch))
        {
            return false;
        }
        if (!consume_racial_skill_charge(active_unit, skill_def, batch))
        {
            return false;
        }
        active_unit.Set("current_ap", Math.Max(GdInterop.GetInt(active_unit, "current_ap") - GdInterop.GetInt(costs, "ap_cost", GdInterop.GetInt(combatProfile, "ap_cost")), 0));
        active_unit.Set("current_mp", Math.Max(GdInterop.GetInt(active_unit, "current_mp") - GdInterop.GetInt(costs, "mp_cost", GdInterop.GetInt(combatProfile, "mp_cost")), 0));
        active_unit.Set("current_stamina", Math.Max(GdInterop.GetInt(active_unit, "current_stamina") - GdInterop.GetInt(costs, "stamina_cost", GdInterop.GetInt(combatProfile, "stamina_cost")), 0));
        active_unit.Set("current_aura", Math.Max(GdInterop.GetInt(active_unit, "current_aura") - GdInterop.GetInt(costs, "aura_cost", GdInterop.GetInt(combatProfile, "aura_cost")), 0));
        int cooldown = Math.Max(GdInterop.GetInt(costs, "cooldown_tu", GdInterop.GetInt(combatProfile, "cooldown_tu")), 0);
        if (cooldown > 0)
        {
            GDictionary cooldowns = GdInterop.GetDictionary(active_unit, "cooldowns");
            cooldowns[GdInterop.GetStringName(skill_def, "skill_id")] = cooldown;
            active_unit.Set("cooldowns", cooldowns);
        }
        return true;
    }

    public bool consume_misfortune_skill_gate(GodotObject active_unit, GodotObject skill_def, GodotObject batch = null)
    {
        StringName skillId = GdInterop.GetStringName(skill_def, "skill_id");
        if (skill_def == null || !CallScriptBool(MisfortuneServiceScript, "is_misfortune_gated_skill", skillId))
        {
            return true;
        }
        if (_runtime == null || !_runtime.HasMethod("consume_misfortune_skill_cast"))
        {
            AppendLog(batch, CallScriptString(MisfortuneServiceScript, "get_skill_sidecar_missing_message", skillId));
            return false;
        }
        GDictionary consumeResult = _runtime.Call("consume_misfortune_skill_cast", active_unit, skillId).AsGodotDictionary();
        if (!GdInterop.GetBool(consumeResult, "ok", false))
        {
            AppendLog(batch, GdInterop.GetString(consumeResult, "message", CallScriptString(MisfortuneServiceScript, "get_skill_default_block_message", skillId)));
            return false;
        }
        return true;
    }

    public string get_racial_skill_charge_block_reason(GodotObject active_unit, GodotObject skill_def)
    {
        if (active_unit == null || !_is_identity_granted_skill(skill_def))
        {
            return "";
        }
        StringName chargeKey = get_racial_skill_charge_key(GdInterop.GetStringName(skill_def, "skill_id"));
        GDictionary perBattleCharges = GdInterop.GetDictionary(active_unit, "per_battle_charges");
        GDictionary perTurnCharges = GdInterop.GetDictionary(active_unit, "per_turn_charges");
        if (perBattleCharges.ContainsKey(chargeKey))
        {
            if (GdInterop.GetInt(perBattleCharges, chargeKey, 0) <= 0)
            {
                return $"{_get_skill_display_name(skill_def)} 的身份技能次数已用尽。";
            }
        }
        else if (perTurnCharges.ContainsKey(chargeKey))
        {
            if (GdInterop.GetInt(perTurnCharges, chargeKey, 0) <= 0)
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

    public bool consume_racial_skill_charge(GodotObject active_unit, GodotObject skill_def, GodotObject batch = null)
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
        StringName chargeKey = get_racial_skill_charge_key(GdInterop.GetStringName(skill_def, "skill_id"));
        GDictionary perBattleCharges = GdInterop.GetDictionary(active_unit, "per_battle_charges");
        GDictionary perTurnCharges = GdInterop.GetDictionary(active_unit, "per_turn_charges");
        if (perBattleCharges.ContainsKey(chargeKey))
        {
            perBattleCharges[chargeKey] = Math.Max(GdInterop.GetInt(perBattleCharges, chargeKey, 0) - 1, 0);
            active_unit.Set("per_battle_charges", perBattleCharges);
        }
        if (perTurnCharges.ContainsKey(chargeKey))
        {
            perTurnCharges[chargeKey] = Math.Max(GdInterop.GetInt(perTurnCharges, chargeKey, 0) - 1, 0);
            active_unit.Set("per_turn_charges", perTurnCharges);
        }
        return true;
    }

    public StringName get_racial_skill_charge_key(StringName skill_id)
    {
        return GdInterop.IsEmpty(skill_id) ? Empty : new StringName($"racial_skill_{skill_id}");
    }

    public GDictionary get_effective_skill_costs(GodotObject active_unit, GodotObject skill_def)
    {
        GodotObject combatProfile = GdInterop.GetObject(skill_def, "combat_profile");
        if (skill_def == null || combatProfile == null)
        {
            return new GDictionary();
        }
        int skillLevel = _runtime.Call("_get_unit_skill_level", active_unit, GdInterop.GetStringName(skill_def, "skill_id")).AsInt32();
        Variant costs = combatProfile.Call("get_effective_resource_costs", skillLevel);
        return costs.VariantType == Variant.Type.Dictionary ? costs.AsGodotDictionary() : new GDictionary();
    }

    public string get_locked_combat_resource_block_reason(GodotObject active_unit, GDictionary costs)
    {
        if (active_unit == null)
        {
            return "技能施放者无效。";
        }
        if (GdInterop.GetInt(costs, "mp_cost", 0) > 0 && !HasCombatResourceUnlocked(active_unit, CombatResourceMp))
        {
            return "法力尚未解锁，无法施放该技能。";
        }
        if (GdInterop.GetInt(costs, "stamina_cost", 0) > 0 && !HasCombatResourceUnlocked(active_unit, CombatResourceStamina))
        {
            return "体力尚未解锁，无法施放该技能。";
        }
        if (GdInterop.GetInt(costs, "aura_cost", 0) > 0 && !HasCombatResourceUnlocked(active_unit, CombatResourceAura))
        {
            return "斗气尚未解锁，无法施放该技能。";
        }
        return "";
    }

    public bool _is_identity_granted_skill(GodotObject skill_def)
    {
        return skill_def != null && IdentitySkillLearnSources.Contains(GdInterop.GetStringName(skill_def, "learn_source"));
    }

    public string _get_skill_display_name(GodotObject skill_def)
    {
        if (skill_def == null)
        {
            return "身份技能";
        }
        string displayName = GdInterop.GetString(skill_def, "display_name").Trim();
        if (!string.IsNullOrEmpty(displayName))
        {
            return displayName;
        }
        StringName skillId = GdInterop.GetStringName(skill_def, "skill_id");
        return !GdInterop.IsEmpty(skillId) ? skillId.ToString() : "身份技能";
    }

    public string get_black_contract_push_variant_block_reason(GodotObject active_unit, GodotObject cast_variant)
    {
        if (active_unit == null)
        {
            return "技能施放者无效。";
        }
        if (cast_variant == null)
        {
            return "黑契推进需要先选择一个代价分支。";
        }
        StringName variantId = GdInterop.GetStringName(cast_variant, "variant_id");
        if (variantId == BLACK_CONTRACT_PUSH_VARIANT_BLOOD && GdInterop.GetInt(active_unit, "current_hp") <= BLACK_CONTRACT_PUSH_HP_COST)
        {
            return "当前生命不足，无法支付血契代价。";
        }
        if (variantId == BLACK_CONTRACT_PUSH_VARIANT_GUARD && !HasStatus(active_unit, STATUS_GUARDING))
        {
            return "当前没有 Guard，无法支付护契代价。";
        }
        if (variantId == BLACK_CONTRACT_PUSH_VARIANT_ACTION)
        {
            return "";
        }
        if (variantId != BLACK_CONTRACT_PUSH_VARIANT_BLOOD && variantId != BLACK_CONTRACT_PUSH_VARIANT_GUARD)
        {
            return "黑契推进的施法形态无效。";
        }
        return "";
    }

    public bool consume_black_contract_push_cast(GodotObject active_unit, GodotObject cast_variant, GodotObject batch = null)
    {
        string blockReason = get_black_contract_push_variant_block_reason(active_unit, cast_variant);
        if (!string.IsNullOrEmpty(blockReason))
        {
            AppendLog(batch, blockReason);
            return false;
        }
        if (active_unit == null || cast_variant == null)
        {
            return false;
        }
        StringName variantId = GdInterop.GetStringName(cast_variant, "variant_id");
        if (variantId == BLACK_CONTRACT_PUSH_VARIANT_BLOOD)
        {
            active_unit.Set("current_hp", Math.Max(GdInterop.GetInt(active_unit, "current_hp") - BLACK_CONTRACT_PUSH_HP_COST, 1));
            AppendLog(batch, $"{DisplayName(active_unit)} 以血契推进，先失去 {BLACK_CONTRACT_PUSH_HP_COST} 点生命。");
        }
        else if (variantId == BLACK_CONTRACT_PUSH_VARIANT_GUARD)
        {
            EraseStatusEffect(active_unit, STATUS_GUARDING);
            AppendLog(batch, $"{DisplayName(active_unit)} 拆解了自己的 Guard，换取这次黑契推进。");
        }
        else if (variantId == BLACK_CONTRACT_PUSH_VARIANT_ACTION)
        {
            _runtime.Call(
                "_set_runtime_status_effect",
                active_unit,
                STATUS_STAGGERED,
                DOOM_SHIFT_SELF_DEBUFF_DURATION_TU,
                GdInterop.GetStringName(active_unit, "unit_id"),
                1,
                new GDictionary { ["counts_as_debuff"] = true });
            AppendLog(batch, $"{DisplayName(active_unit)} 透支了下一回合的行动力，换取这次黑契推进。");
        }
        _runtime.Call("_append_changed_unit_id", batch, GdInterop.GetStringName(active_unit, "unit_id"));
        return true;
    }

    public void ensure_unit_turn_anchor(GodotObject unit_state)
    {
        if (unit_state == null || GdInterop.GetInt(unit_state, "last_turn_tu") >= 0)
        {
            return;
        }
        GodotObject state = GdInterop.GetObject(_runtime, "_state");
        GodotObject timeline = GdInterop.GetObject(state, "timeline");
        unit_state.Set("last_turn_tu", state != null && timeline != null ? GdInterop.GetInt(timeline, "current_tu") : 0);
    }

    public bool advance_unit_cooldowns(GodotObject unit_state, int cooldown_delta)
    {
        if (unit_state == null || cooldown_delta <= 0)
        {
            return false;
        }
        GDictionary previousCooldowns = GdInterop.GetDictionary(unit_state, "cooldowns").Duplicate(true);
        var retainedCooldowns = new GDictionary();
        foreach (Variant skillIdVariant in previousCooldowns.Keys)
        {
            StringName skillId = ToStringName(skillIdVariant);
            int previousRemaining = GdInterop.GetInt(previousCooldowns, skillIdVariant, 0);
            int remaining = Math.Max(previousRemaining - cooldown_delta, 0);
            if (remaining > 0)
            {
                retainedCooldowns[skillId] = remaining;
            }
        }
        unit_state.Set("cooldowns", retainedCooldowns);
        return !DictionariesEqual(previousCooldowns, retainedCooldowns);
    }

    public bool consume_turn_cooldown_delta(GodotObject unit_state)
    {
        if (unit_state == null)
        {
            return false;
        }
        GodotObject state = GdInterop.GetObject(_runtime, "_state");
        GodotObject timeline = GdInterop.GetObject(state, "timeline");
        int currentTu = state != null && timeline != null ? GdInterop.GetInt(timeline, "current_tu") : 0;
        if (GdInterop.GetInt(unit_state, "last_turn_tu") < 0)
        {
            unit_state.Set("last_turn_tu", currentTu);
            return false;
        }
        int elapsedTu = Math.Max(currentTu - GdInterop.GetInt(unit_state, "last_turn_tu"), 0);
        unit_state.Set("last_turn_tu", currentTu);
        if (elapsedTu <= 0)
        {
            return false;
        }
        if (elapsedTu % TU_GRANULARITY != 0)
        {
            GD.PushError($"Cooldown delta must use {TU_GRANULARITY} TU steps, got {elapsedTu}.");
            return false;
        }
        return advance_unit_cooldowns(unit_state, elapsedTu);
    }

    public void advance_unit_turn_timers(GodotObject unit_state, GodotObject batch)
    {
        if (unit_state == null)
        {
            return;
        }
        bool changed = consume_turn_cooldown_delta(unit_state);
        foreach (string statusIdString in SortedStringKeys(GdInterop.GetDictionary(unit_state, "status_effects")))
        {
            if (GetStatusEffect(unit_state, new StringName(statusIdString)) == null)
            {
                changed = true;
            }
        }
        if (changed)
        {
            _runtime.Call("_append_changed_unit_id", batch, GdInterop.GetStringName(unit_state, "unit_id"));
        }
    }

    public GDictionary apply_turn_start_statuses(GodotObject unit_state, GodotObject batch)
    {
        if (unit_state == null)
        {
            return new GDictionary { ["changed"] = false, ["defeat_source_unit_id"] = "" };
        }
        bool changed = false;
        var penaltyByGroup = new GDictionary();
        var labelByGroup = new GDictionary();
        var consumeStatusIds = new GArray();
        foreach (string statusIdString in SortedStringKeys(GdInterop.GetDictionary(unit_state, "status_effects")))
        {
            StringName statusId = new(statusIdString);
            GodotObject statusEntry = GetStatusEffect(unit_state, statusId);
            if (statusEntry == null)
            {
                continue;
            }
            int apPenalty = CallScriptInt(BattleStatusSemanticTableScript, "get_turn_start_ap_penalty", statusEntry);
            if (apPenalty <= 0)
            {
                continue;
            }
            StringName penaltyGroup = CallScriptStringName(BattleStatusSemanticTableScript, "get_turn_start_ap_penalty_group", statusEntry);
            if (GdInterop.IsEmpty(penaltyGroup))
            {
                penaltyGroup = GdInterop.GetStringName(statusEntry, "status_id");
            }
            if (apPenalty > GdInterop.GetInt(penaltyByGroup, penaltyGroup, 0))
            {
                penaltyByGroup[penaltyGroup] = apPenalty;
                labelByGroup[penaltyGroup] = CallScriptString(BattleStatusSemanticTableScript, "get_turn_start_ap_penalty_display_label", statusEntry);
            }
            if (CallScriptBool(BattleStatusSemanticTableScript, "should_consume_after_turn_start_ap_penalty", statusEntry))
            {
                consumeStatusIds.Add(GdInterop.GetStringName(statusEntry, "status_id"));
            }
        }
        foreach (string groupIdString in SortedStringKeys(penaltyByGroup))
        {
            StringName groupId = new(groupIdString);
            int groupPenalty = GdInterop.GetInt(penaltyByGroup, groupId, 0);
            if (groupPenalty <= 0)
            {
                continue;
            }
            int previousAp = GdInterop.GetInt(unit_state, "current_ap");
            unit_state.Set("current_ap", Math.Max(previousAp - groupPenalty, 0));
            int consumedAp = previousAp - GdInterop.GetInt(unit_state, "current_ap");
            if (consumedAp > 0)
            {
                changed = true;
                AppendLog(batch, $"{DisplayName(unit_state)} 受到{GdInterop.GetString(labelByGroup, groupId, "状态")}影响，本回合少 {consumedAp} 点 AP。");
            }
        }
        foreach (Variant statusIdValue in consumeStatusIds)
        {
            StringName statusId = ToStringName(statusIdValue);
            if (HasStatus(unit_state, statusId))
            {
                EraseStatusEffect(unit_state, statusId);
                changed = true;
            }
        }
        if (changed)
        {
            _runtime.Call("_append_changed_unit_id", batch, GdInterop.GetStringName(unit_state, "unit_id"));
        }
        return new GDictionary { ["changed"] = changed, ["defeat_source_unit_id"] = "" };
    }

    public GDictionary apply_unit_status_periodic_ticks(GodotObject unit_state, int elapsed_tu, GodotObject batch)
    {
        GodotObject state = GdInterop.GetObject(_runtime, "_state");
        GodotObject timeline = GdInterop.GetObject(state, "timeline");
        if (state == null || timeline == null || unit_state == null || elapsed_tu <= 0)
        {
            return new GDictionary { ["changed"] = false, ["defeat_source_unit_id"] = "" };
        }
        bool changed = false;
        StringName defeatSourceUnitId = Empty;
        int currentTu = GdInterop.GetInt(timeline, "current_tu");
        int previousTu = Math.Max(currentTu - elapsed_tu, 0);
        foreach (string statusIdString in SortedStringKeys(GdInterop.GetDictionary(unit_state, "status_effects")))
        {
            if (!GdInterop.GetBool(unit_state, "is_alive"))
            {
                break;
            }
            GodotObject statusEntry = GetStatusEffect(unit_state, new StringName(statusIdString));
            if (statusEntry == null)
            {
                continue;
            }
            int tickDamage = CallScriptInt(BattleStatusSemanticTableScript, "get_timeline_tick_damage", statusEntry);
            if (tickDamage <= 0)
            {
                continue;
            }
            if (GdInterop.GetInt(statusEntry, "next_tick_at_tu") <= previousTu)
            {
                statusEntry.Set("next_tick_at_tu", previousTu + GdInterop.GetInt(statusEntry, "tick_interval_tu"));
                changed = true;
            }
            int tickLimitTu = currentTu;
            if (statusEntry.Call("has_duration").AsBool())
            {
                tickLimitTu = Math.Min(tickLimitTu, previousTu + GdInterop.GetInt(statusEntry, "duration"));
            }
            while (GdInterop.GetBool(unit_state, "is_alive")
                && GdInterop.GetInt(statusEntry, "next_tick_at_tu") > 0
                && GdInterop.GetInt(statusEntry, "next_tick_at_tu") <= tickLimitTu)
            {
                int previousHp = GdInterop.GetInt(unit_state, "current_hp");
                unit_state.Set("current_hp", Math.Max(previousHp - tickDamage, 0));
                unit_state.Set("is_alive", GdInterop.GetInt(unit_state, "current_hp") > 0);
                statusEntry.Set("next_tick_at_tu", GdInterop.GetInt(statusEntry, "next_tick_at_tu") + GdInterop.GetInt(statusEntry, "tick_interval_tu"));
                if (GdInterop.GetInt(unit_state, "current_hp") != previousHp)
                {
                    changed = true;
                    AppendLog(batch, $"{DisplayName(unit_state)} 受到 {GdInterop.GetStringName(statusEntry, "status_id")} 持续影响，损失 {previousHp - GdInterop.GetInt(unit_state, "current_hp")} 点生命。");
                    if (!GdInterop.GetBool(unit_state, "is_alive") && !GdInterop.IsEmpty(GdInterop.GetStringName(statusEntry, "source_unit_id")))
                    {
                        defeatSourceUnitId = GdInterop.GetStringName(statusEntry, "source_unit_id");
                    }
                }
            }
            if (GdInterop.GetBool(unit_state, "is_alive"))
            {
                unit_state.Call("set_status_effect", statusEntry);
            }
        }
        return new GDictionary { ["changed"] = changed, ["defeat_source_unit_id"] = defeatSourceUnitId.ToString() };
    }

    public bool advance_unit_status_durations(GodotObject unit_state, int elapsed_tu, GodotObject batch = null)
    {
        if (unit_state == null)
        {
            return false;
        }
        bool changed = false;
        var expiredStatusIds = new List<StringName>();
        var expiredStatusEntries = new Dictionary<StringName, GodotObject>();
        foreach (string statusIdString in SortedStringKeys(GdInterop.GetDictionary(unit_state, "status_effects")))
        {
            StringName statusId = new(statusIdString);
            GodotObject statusEntry = GetStatusEffect(unit_state, statusId);
            if (statusEntry == null)
            {
                expiredStatusIds.Add(statusId);
                changed = true;
                continue;
            }
            GDictionary durationResult = CallScriptDictionary(BattleStatusSemanticTableScript, "advance_timeline_duration", statusEntry, elapsed_tu);
            if (GdInterop.GetBool(durationResult, "expired", false))
            {
                expiredStatusIds.Add(statusId);
                expiredStatusEntries[statusId] = statusEntry;
                changed = true;
                continue;
            }
            if (GdInterop.GetBool(durationResult, "changed", false))
            {
                unit_state.Call("set_status_effect", statusEntry);
                changed = true;
            }
        }
        foreach (StringName expiredStatusId in expiredStatusIds)
        {
            expiredStatusEntries.TryGetValue(expiredStatusId, out GodotObject expiredStatusEntry);
            bool shouldEraseStatus = true;
            if (_is_body_size_category_override_status(expiredStatusEntry))
            {
                shouldEraseStatus = false;
                if (_restore_body_size_category_override_if_needed(unit_state, expiredStatusEntry, batch))
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

    public bool _body_size_already_matches_previous(GodotObject unit_state, GodotObject status_entry)
    {
        if (unit_state == null || status_entry == null)
        {
            return false;
        }
        GDictionary parameters = GdInterop.GetDictionary(status_entry, "params");
        if (!parameters.ContainsKey(StatusParamBodySizeCategoryOverride))
        {
            return false;
        }
        StringName previousCategory = GdInterop.GetStringName(parameters, StatusParamPreviousBodySizeCategory, Empty);
        if (!CallScriptBool(BodySizeRulesScript, "is_valid_body_size_category", previousCategory))
        {
            return false;
        }
        return GdInterop.GetStringName(unit_state, "body_size_category") == previousCategory;
    }

    public bool _is_body_size_category_override_status(GodotObject status_entry)
    {
        return status_entry != null && GdInterop.GetDictionary(status_entry, "params").ContainsKey(StatusParamBodySizeCategoryOverride);
    }

    public bool _restore_body_size_category_override_if_needed(GodotObject unit_state, GodotObject status_entry, GodotObject batch = null)
    {
        if (unit_state == null || status_entry == null)
        {
            return false;
        }
        GDictionary parameters = GdInterop.GetDictionary(status_entry, "params");
        if (!parameters.ContainsKey(StatusParamBodySizeCategoryOverride))
        {
            return false;
        }
        StringName previousCategory = GdInterop.GetStringName(parameters, StatusParamPreviousBodySizeCategory, Empty);
        if (!CallScriptBool(BodySizeRulesScript, "is_valid_body_size_category", previousCategory))
        {
            return false;
        }
        if (GdInterop.GetStringName(unit_state, "body_size_category") == previousCategory)
        {
            return false;
        }
        GArray previousCoords = GdInterop.GetArray(unit_state, "occupied_coords").Duplicate();
        StringName currentCategory = GdInterop.GetStringName(unit_state, "body_size_category");
        GodotObject runtime = _runtime;
        GodotObject gridService = runtime != null && runtime.HasMethod("get_grid_service") ? runtime.Call("get_grid_service").AsGodotObject() : null;
        GodotObject state = runtime != null && runtime.HasMethod("get_state") ? runtime.Call("get_state").AsGodotObject() : null;
        if (gridService != null && state != null)
        {
            gridService.Call("clear_unit_occupancy", state, unit_state);
        }
        unit_state.Call("set_body_size_category", previousCategory);
        if (gridService != null && state != null)
        {
            if (!gridService.Call("can_place_unit", state, unit_state, GdInterop.GetVector2I(unit_state, "coord"), true).AsBool())
            {
                unit_state.Call("set_body_size_category", currentCategory);
                gridService.Call("set_occupants", state, previousCoords, GdInterop.GetStringName(unit_state, "unit_id"));
                return false;
            }
            gridService.Call("set_occupants", state, GdInterop.GetArray(unit_state, "occupied_coords"), GdInterop.GetStringName(unit_state, "unit_id"));
        }
        if (runtime != null && batch != null)
        {
            runtime.Call("_append_changed_coords", batch, previousCoords);
            runtime.Call("_append_changed_unit_coords", batch, unit_state);
            runtime.Call("_append_changed_unit_id", batch, GdInterop.GetStringName(unit_state, "unit_id"));
        }
        return true;
    }

    public int get_effective_skill_range(GodotObject active_unit, GodotObject skill_def) => BattleRangeService.get_effective_skill_range(active_unit, skill_def);
    public int resolve_base_skill_range(GodotObject active_unit, GodotObject skill_def) => BattleRangeService.resolve_base_skill_range(active_unit, skill_def);
    public bool is_weapon_range_skill(GodotObject skill_def) => BattleRangeService.is_weapon_range_skill(skill_def);
    public int get_weapon_attack_range(GodotObject active_unit) => BattleRangeService.get_weapon_attack_range(active_unit);

    public bool skill_has_tag(GodotObject skill_def, StringName expected_tag)
    {
        if (skill_def == null || GdInterop.IsEmpty(expected_tag))
        {
            return false;
        }
        foreach (Variant tag in GdInterop.GetArray(skill_def, "tags"))
        {
            if (ToStringName(tag) == expected_tag)
            {
                return true;
            }
        }
        return false;
    }

    public bool is_movement_blocked(GodotObject unit_state)
    {
        return has_status(unit_state, STATUS_PINNED)
            || has_status(unit_state, STATUS_ROOTED)
            || has_status(unit_state, STATUS_TENDON_CUT)
            || has_status(unit_state, STATUS_PETRIFIED);
    }

    public bool has_status(GodotObject unit_state, StringName status_id)
    {
        return unit_state != null && !GdInterop.IsEmpty(status_id) && HasStatus(unit_state, status_id);
    }

    public GDictionary _resolve_status_self_save(GodotObject unit_state, GodotObject status_entry, StringName fallback_ability, StringName fallback_tag)
    {
        GDictionary parameters = status_entry != null ? GdInterop.GetDictionary(status_entry, "params") : new GDictionary();
        GodotObject effect = NewScriptInstance(CombatEffectDefScript);
        effect.Set("effect_type", StatusEffectType);
        effect.Set("save_dc", Math.Max(GdInterop.GetInt(parameters, "self_save_dc", 16), 1));
        effect.Set("save_dc_mode", SaveDcModeStatic);
        effect.Set("save_ability", GdInterop.GetStringName(parameters, "self_save_ability", fallback_ability));
        effect.Set("save_tag", GdInterop.GetStringName(parameters, "self_save_tag", fallback_tag));
        var context = new GDictionary();
        if (parameters.ContainsKey("self_save_roll_override"))
        {
            context["save_roll_override"] = GdInterop.GetInt(parameters, "self_save_roll_override", 0);
        }
        GodotObject sourceUnit = null;
        GodotObject state = GdInterop.GetObject(_runtime, "_state");
        if (_runtime != null && state != null && status_entry != null && !GdInterop.IsEmpty(GdInterop.GetStringName(status_entry, "source_unit_id")))
        {
            sourceUnit = GdInterop.GetObject(GdInterop.GetDictionary(state, "units"), GdInterop.GetStringName(status_entry, "source_unit_id"));
        }
        return CallScriptDictionary(BattleSaveResolverScript, "resolve_save", sourceUnit, unit_state, effect, context);
    }

    public GodotObject _find_madness_unit_target(GodotObject unit_state, GodotObject skill_def)
    {
        GodotObject state = GdInterop.GetObject(_runtime, "_state");
        if (_runtime == null || state == null || unit_state == null)
        {
            return null;
        }
        GodotObject bestUnit = null;
        int bestDistance = 999999;
        int effectiveRange = _runtime.Call("_get_effective_skill_range", unit_state, skill_def).AsInt32();
        foreach (Variant unitValue in GdInterop.GetDictionary(state, "units").Values)
        {
            GodotObject candidate = unitValue.AsGodotObject();
            if (candidate == null
                || !GdInterop.GetBool(candidate, "is_alive")
                || GdInterop.GetStringName(candidate, "unit_id") == GdInterop.GetStringName(unit_state, "unit_id"))
            {
                continue;
            }
            int distance = GdInterop.GetObject(_runtime, "_grid_service").Call("get_distance_between_units", unit_state, candidate).AsInt32();
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

    public void _append_changed_unit(GodotObject batch, GodotObject unit_state)
    {
        if (_runtime == null || batch == null || unit_state == null)
        {
            return;
        }
        _runtime.Call("_append_changed_unit_id", batch, GdInterop.GetStringName(unit_state, "unit_id"));
        _runtime.Call("_append_changed_unit_coords", batch, unit_state);
    }

    public void _append_log(GodotObject batch, string line)
    {
        AppendLog(batch, line);
    }

    public void consume_status_if_present(GodotObject unit_state, StringName status_id, GodotObject batch = null)
    {
        if (unit_state == null || GdInterop.IsEmpty(status_id) || !HasStatus(unit_state, status_id))
        {
            return;
        }
        EraseStatusEffect(unit_state, status_id);
        if (batch != null)
        {
            _runtime.Call("_append_changed_unit_id", batch, GdInterop.GetStringName(unit_state, "unit_id"));
        }
    }

    public bool is_main_skill_locked_by_status(GodotObject active_unit, GodotObject skill_def)
    {
        if (active_unit == null || skill_def == null)
        {
            return false;
        }
        GArray knownActiveSkillIds = GdInterop.GetArray(active_unit, "known_active_skill_ids");
        if (knownActiveSkillIds.Count == 0)
        {
            return false;
        }
        if (ToStringName(knownActiveSkillIds[0]) != GdInterop.GetStringName(skill_def, "skill_id"))
        {
            return false;
        }
        int requiredDebuffCount = get_status_param_max_int(active_unit, "main_skill_lock_other_debuff_count");
        if (requiredDebuffCount <= 0)
        {
            return false;
        }
        return count_debuff_statuses(active_unit) >= requiredDebuffCount;
    }

    public int count_debuff_statuses(GodotObject unit_state)
    {
        if (unit_state == null)
        {
            return 0;
        }
        int debuffCount = 0;
        foreach (Variant statusIdValue in GdInterop.GetDictionary(unit_state, "status_effects").Keys)
        {
            StringName statusId = ToStringName(statusIdValue);
            GodotObject statusEntry = GetStatusEffect(unit_state, statusId);
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

    public bool status_counts_as_debuff(StringName status_id, GodotObject status_entry)
    {
        if (status_entry != null)
        {
            GDictionary parameters = GdInterop.GetDictionary(status_entry, "params");
            if (_status_params_has_formal_key(parameters, "counts_as_debuff"))
            {
                return GdInterop.GetBool(parameters, "counts_as_debuff", false);
            }
        }
        return DebuffStatusIds.Contains(status_id);
    }

    public bool has_status_param_bool(GodotObject unit_state, StringName param_key)
    {
        if (unit_state == null || GdInterop.IsEmpty(param_key))
        {
            return false;
        }
        foreach (Variant statusIdValue in GdInterop.GetDictionary(unit_state, "status_effects").Keys)
        {
            GodotObject statusEntry = GetStatusEffect(unit_state, ToStringName(statusIdValue));
            if (statusEntry == null)
            {
                continue;
            }
            Variant value = _status_params_get_formal_value(GdInterop.GetDictionary(statusEntry, "params"), param_key.ToString(), false);
            if (value.AsBool())
            {
                return true;
            }
        }
        return false;
    }

    public int get_status_param_max_int(GodotObject unit_state, StringName param_key)
    {
        if (unit_state == null || GdInterop.IsEmpty(param_key))
        {
            return 0;
        }
        int maxValue = 0;
        foreach (Variant statusIdValue in GdInterop.GetDictionary(unit_state, "status_effects").Keys)
        {
            GodotObject statusEntry = GetStatusEffect(unit_state, ToStringName(statusIdValue));
            if (statusEntry == null)
            {
                continue;
            }
            Variant value = _status_params_get_formal_value(GdInterop.GetDictionary(statusEntry, "params"), param_key.ToString(), 0);
            maxValue = Math.Max(value.AsInt32(), maxValue);
        }
        return maxValue;
    }

    public bool _status_params_has_formal_key(GDictionary parameters, string param_key)
    {
        return parameters.Keys.Any(key => key.VariantType == Variant.Type.String && key.AsString() == param_key);
    }

    public Variant _status_params_get_formal_value(GDictionary parameters, string param_key, Variant default_value)
    {
        foreach (Variant key in parameters.Keys)
        {
            if (key.VariantType == Variant.Type.String && key.AsString() == param_key)
            {
                return parameters[key];
            }
        }
        return default_value;
    }

    public bool _is_black_contract_push_skill(StringName skill_id)
    {
        return skill_id == BLACK_CONTRACT_PUSH_SKILL_ID;
    }

    private static GodotObject NewBattleCommand()
    {
        return new BattleCommand();
    }

    private static GodotObject NewScriptInstance(Script script)
    {
        return script.Call("new").AsGodotObject();
    }

    private static bool HasCombatResourceUnlocked(GodotObject unit, StringName resourceId)
    {
        return unit.Call("has_combat_resource_unlocked", resourceId).AsBool();
    }

    private static bool HasStatus(GodotObject unit, StringName statusId)
    {
        return unit.Call("has_status_effect", statusId).AsBool();
    }

    private static GodotObject GetStatusEffect(GodotObject unit, StringName statusId)
    {
        return unit.Call("get_status_effect", statusId).AsGodotObject();
    }

    private static void EraseStatusEffect(GodotObject unit, StringName statusId)
    {
        unit.Call("erase_status_effect", statusId);
    }

    private static void AppendLog(GodotObject batch, string line)
    {
        if (batch == null || string.IsNullOrEmpty(line))
        {
            return;
        }
        GdInterop.GetArray(batch, "log_lines").Add(line);
    }

    private static string DisplayName(GodotObject value)
    {
        return GdInterop.GetString(value, "display_name");
    }

    private static StringName ToStringName(Variant value)
    {
        return value.VariantType == Variant.Type.StringName ? value.AsStringName() : new StringName(value.ToString());
    }

    private static List<string> SortedStringKeys(GDictionary dictionary)
    {
        var keys = new List<string>();
        foreach (Variant key in dictionary.Keys)
        {
            keys.Add(key.ToString());
        }
        keys.Sort(StringComparer.Ordinal);
        return keys;
    }

    private static bool DictionariesEqual(GDictionary left, GDictionary right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }
        foreach (Variant key in left.Keys)
        {
            if (!GdInterop.TryGet(right, key, out Variant rightValue) || !left[key].Equals(rightValue))
            {
                return false;
            }
        }
        return true;
    }

    private static GDictionary CallScriptDictionary(Script script, string method, params Variant[] args)
    {
        Variant value = script.Call(method, args);
        return value.VariantType == Variant.Type.Dictionary ? value.AsGodotDictionary() : new GDictionary();
    }

    private static int CallScriptInt(Script script, string method, params Variant[] args)
    {
        return script.Call(method, args).AsInt32();
    }

    private static bool CallScriptBool(Script script, string method, params Variant[] args)
    {
        return script.Call(method, args).AsBool();
    }

    private static string CallScriptString(Script script, string method, params Variant[] args)
    {
        return script.Call(method, args).AsString();
    }

    private static StringName CallScriptStringName(Script script, string method, params Variant[] args)
    {
        Variant value = script.Call(method, args);
        return value.VariantType == Variant.Type.StringName ? value.AsStringName() : new StringName(value.ToString());
    }

    private static GodotObject ResolveWeakRef(WeakReference<GodotObject> weakRef)
    {
        if (weakRef == null || !weakRef.TryGetTarget(out GodotObject target) || !GodotObject.IsInstanceValid(target))
        {
            return null;
        }
        return target;
    }
}
