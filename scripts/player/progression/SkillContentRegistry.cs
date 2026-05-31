using System.Collections.Generic;
using Godot;
using Godot.Collections;
using VT = Godot.Variant.Type;

[GlobalClass]
public partial class SkillContentRegistry : RefCounted
{
    private const string SkillConfigDirectory = "res://data/configs/skills";
    private const int TuGranularity = 5;

    private static readonly HashSet<StringName> ValidMasteryTriggerModes = new()
    {
        "skill_damage_dice_max",
        "weapon_attack_quality",
        "damage_dealt",
        "status_applied",
        "effect_applied",
        "incoming_physical_hit",
        "secondary_hit",
    };

    private static readonly HashSet<StringName> ValidMasteryAmountModes = new()
    {
        "per_target_rank",
        "per_cast_hp_ratio",
    };

    private static readonly HashSet<StringName> ValidSpellFateModes = new() { "", "control_roll" };
    private static readonly HashSet<StringName> ValidSpellCriticalModes = new() { "", "mp_refund" };
    private static readonly HashSet<StringName> ValidBacklashModes = new()
    {
        "",
        "ground_anchor_drift",
    };
    private static readonly HashSet<StringName> ValidSaveDcModes = new()
    {
        BattleSaveContentRules.SAVE_DC_MODE_STATIC,
        BattleSaveContentRules.SAVE_DC_MODE_CASTER_SPELL,
    };

    private static readonly HashSet<StringName> ValidSaveAbilities = new()
    {
        UnitBaseAttributes.STRENGTH(),
        UnitBaseAttributes.AGILITY(),
        UnitBaseAttributes.CONSTITUTION(),
        UnitBaseAttributes.PERCEPTION(),
        UnitBaseAttributes.INTELLIGENCE(),
        UnitBaseAttributes.WILLPOWER(),
    };

    private static readonly HashSet<StringName> ValidSaveTags = new()
    {
        BattleSaveContentRules.SAVE_TAG_SLEEP,
        BattleSaveContentRules.SAVE_TAG_PARALYSIS,
        BattleSaveContentRules.SAVE_TAG_CHARM,
        BattleSaveContentRules.SAVE_TAG_POISON,
        BattleSaveContentRules.SAVE_TAG_DRAGON_BREATH,
        BattleSaveContentRules.SAVE_TAG_FIREBALL,
        BattleSaveContentRules.SAVE_TAG_CHAIN_LIGHTNING,
        BattleSaveContentRules.SAVE_TAG_EQUIPMENT_DISJUNCTION,
        BattleSaveContentRules.SAVE_TAG_MAGIC,
        BattleSaveContentRules.SAVE_TAG_ILLUSION,
        BattleSaveContentRules.SAVE_TAG_FRIGHTENED,
        BattleSaveContentRules.SAVE_TAG_EXECUTE,
        UnitBaseAttributes.STRENGTH(),
        UnitBaseAttributes.AGILITY(),
        UnitBaseAttributes.CONSTITUTION(),
        UnitBaseAttributes.PERCEPTION(),
        UnitBaseAttributes.INTELLIGENCE(),
        UnitBaseAttributes.WILLPOWER(),
    };

    private static readonly HashSet<StringName> ValidEffectTriggerEvents = new()
    {
        "",
        "critical_hit",
        "ordinary_hit",
        "secondary_hit",
    };

    private static readonly HashSet<StringName> ValidTriggerConditions = new()
    {
        "",
        "battle_start",
        "on_fatal_damage",
    };

    private static readonly HashSet<StringName> ValidEffectTypes = new()
    {
        "body_size_category_override",
        "chain_damage",
        "charge",
        "cleanse_harmful",
        "damage",
        "dispel_magic",
        "edge_clear",
        "equipment_durability_damage",
        "erase_status",
        "forced_move",
        "heal",
        "heal_fatal",
        "height",
        "height_delta",
        "layered_barrier",
        "on_kill_gain_resources",
        "path_step_aoe",
        "repeat_attack_until_fail",
        "shield",
        "stamina_restore",
        "status",
        "apply_status",
        "terrain",
        "terrain_effect",
        "terrain_replace",
        "terrain_replace_to",
        "execute",
    };

    private static readonly string[] TypedEffectFlagParamNames =
    {
        "add_weapon_dice",
        "requires_weapon",
        "use_weapon_physical_damage_tag",
        "resolve_as_weapon_attack",
        "allow_repeat_hits_across_steps",
        "prevent_repeat_target",
        "stop_on_miss",
        "stop_on_target_down",
        "remove_harmful",
        "remove_harmful_from_allies",
        "remove_beneficial",
        "remove_beneficial_from_enemies",
        "require_damage_applied",
        "staged_execution",
    };

    private static readonly StringName[] PracticeTrackTags = { "meditation", "cultivation" };
    private static readonly HashSet<StringName> ValidPracticeTiers = new()
    {
        "basic",
        "intermediate",
        "advanced",
        "ultimate",
    };

    public Dictionary _skill_defs { get; set; } = new();
    public Array<string> _validation_errors { get; set; } = new();

    private readonly record struct EquipmentDurabilityDamageValidationParameters(
        int MaxDamagedItems,
        bool RequireDamageApplied,
        bool TargetSlotsMissingOrEmpty
    )
    {
        public static EquipmentDurabilityDamageValidationParameters FromEffect(
            CombatEffectDef effectDef
        )
        {
            Dictionary parameters = effectDef?.@params ?? new Dictionary();
            return new EquipmentDurabilityDamageValidationParameters(
                DictInt(parameters, "max_damaged_items", 1),
                effectDef?.require_damage_applied ?? false,
                ReadTargetSlotsMissingOrEmpty(parameters)
            );
        }

        private static bool ReadTargetSlotsMissingOrEmpty(Dictionary parameters)
        {
            if (!TryGetParameter(parameters, "target_slots", out object rawTargetSlots))
                return true;
            return TryAsArray(rawTargetSlots, out Array targetSlots) && targetSlots.Count == 0;
        }
    }

    public SkillContentRegistry()
    {
        System.GC.SuppressFinalize(this);
        rebuild();
    }

    public new void Dispose()
    {
        _skill_defs.Clear();
        _validation_errors.Clear();
        base.Dispose();
    }

    public void dispose() => Dispose();

    public static string skill_config_directory() => SkillConfigDirectory;

    public void rebuild()
    {
        _skill_defs.Clear();
        _validation_errors.Clear();
        _scan_directory(SkillConfigDirectory);
        AppendArray(_validation_errors, _collect_validation_errors());
    }

    public Dictionary get_skill_defs() => _skill_defs.Duplicate();

    public Array<string> validate()
    {
        var copy = new Array<string>();
        foreach (string error in _validation_errors)
            copy.Add(error);
        return copy;
    }

    public void _scan_directory(string directoryPath)
    {
        if (!DirAccess.DirExistsAbsolute(ProjectSettings.GlobalizePath(directoryPath)))
        {
            _validation_errors.Add($"SkillContentRegistry could not find {directoryPath}.");
            return;
        }

        using var directory = DirAccess.Open(directoryPath);
        if (directory == null)
        {
            _validation_errors.Add($"SkillContentRegistry could not open {directoryPath}.");
            return;
        }

        directory.ListDirBegin();
        while (true)
        {
            string entryName = directory.GetNext();
            if (string.IsNullOrEmpty(entryName))
                break;
            if (entryName == "." || entryName == "..")
                continue;

            string entryPath = $"{directoryPath}/{entryName}";
            if (directory.CurrentIsDir())
            {
                _scan_directory(entryPath);
                continue;
            }
            if (!entryName.EndsWith(".tres") && !entryName.EndsWith(".res"))
                continue;
            _register_skill_resource(entryPath);
        }
        directory.ListDirEnd();
    }

    public void _register_skill_resource(string resourcePath)
    {
        var resource = GodotContentResourceLifetime.Keep(GD.Load<Resource>(resourcePath));
        if (resource == null)
        {
            _validation_errors.Add($"Failed to load skill config {resourcePath}.");
            return;
        }
        if (resource is not SkillDef skillDef)
        {
            _validation_errors.Add($"Skill config {resourcePath} is not a SkillDef.");
            return;
        }

        _normalize_skill_def(skillDef);

        if (skillDef.skill_id == "")
        {
            _validation_errors.Add($"Skill config {resourcePath} is missing skill_id.");
            return;
        }
        if (_skill_defs.ContainsKey(skillDef.skill_id))
        {
            _validation_errors.Add($"Duplicate skill_id registered: {skillDef.skill_id}");
            return;
        }

        _skill_defs[skillDef.skill_id] = skillDef;
    }

    public void _normalize_skill_def(SkillDef skillDef)
    {
        if (skillDef == null)
            return;
        if (skillDef.skill_id != "" && skillDef.icon_id == "")
            skillDef.icon_id = skillDef.skill_id;
        if (
            skillDef.combat_profile != null
            && skillDef.skill_id != ""
            && skillDef.combat_profile.skill_id == ""
        )
            skillDef.combat_profile.skill_id = skillDef.skill_id;
    }

    public Array<string> _collect_validation_errors()
    {
        var errors = new Array<string>();
        foreach (string skillKey in ProgressionDataUtils.sorted_string_keys(_skill_defs))
        {
            var skillId = new StringName(skillKey);
            var skillDef = GetTyped<SkillDef>(_skill_defs, skillId);
            if (skillDef == null)
                continue;
            _append_skill_validation_errors(errors, skillId, skillDef);
        }
        return errors;
    }

    public void _append_skill_validation_errors(
        Array<string> errors,
        StringName skillId,
        SkillDef skillDef
    )
    {
        if (skillDef == null)
            return;

        if (skillDef.display_name.StripEdges().Length == 0)
            errors.Add($"Skill {skillId} is missing display_name.");
        if (skillDef.icon_id == "")
            errors.Add($"Skill {skillId} is missing icon_id.");
        if (skillDef.max_level < 0 && skillDef.dynamic_max_level_stat_id == "")
            errors.Add($"Skill {skillId} must have max_level >= 0.");
        if (skillDef.non_core_max_level < 0)
            errors.Add($"Skill {skillId} non_core_max_level must be >= 0.");
        if (
            skillDef.non_core_max_level > skillDef.max_level
            && skillDef.max_level >= 0
            && skillDef.dynamic_max_level_stat_id == ""
        )
            errors.Add($"Skill {skillId} non_core_max_level must be <= max_level.");
        if (
            skillDef.mastery_curve.Length != skillDef.max_level
            && skillDef.max_level >= 0
            && skillDef.dynamic_max_level_stat_id == ""
        )
            errors.Add($"Skill {skillId} mastery_curve size must match max_level.");
        _append_dynamic_max_level_validation_errors(errors, skillId, skillDef);
        foreach (int masteryThreshold in skillDef.mastery_curve)
        {
            if (masteryThreshold <= 0)
            {
                errors.Add($"Skill {skillId} has a non-positive mastery threshold.");
                break;
            }
        }

        if (skillDef.skill_type == "active" && skillDef.combat_profile == null)
            errors.Add($"Skill {skillId} is active but missing combat_profile.");
        _append_practice_skill_validation_errors(errors, skillId, skillDef);
        _append_attribute_growth_validation_errors(errors, skillId, skillDef);
        SkillLevelDescriptionContentRules.append_validation_errors(errors, skillId, skillDef);

        if (skillDef.combat_profile != null)
            AppendCombatProfileValidationErrors(errors, skillId, skillDef.combat_profile, skillDef);
    }

    public void _append_practice_skill_validation_errors(
        Array<string> errors,
        StringName skillId,
        SkillDef skillDef
    )
    {
        int trackCount = 0;
        foreach (StringName trackTag in PracticeTrackTags)
        {
            if (skillDef.tags.Contains(trackTag))
                trackCount++;
        }

        if (trackCount == 0)
        {
            if (skillDef.practice_tier != "")
                errors.Add(
                    $"Skill {skillId} practice_tier requires meditation or cultivation tag."
                );
            return;
        }

        if (trackCount != 1)
            errors.Add($"Skill {skillId} must use exactly one practice track tag.");
        if (skillDef.tags.Count != 1)
            errors.Add(
                $"Skill {skillId} practice tags must be exclusive; tags must contain only meditation or cultivation."
            );
        if (!ValidPracticeTiers.Contains(skillDef.practice_tier))
            errors.Add(
                $"Skill {skillId} practice_tier must be one of basic, intermediate, advanced, ultimate."
            );
    }

    public void _append_dynamic_max_level_validation_errors(
        Array<string> errors,
        StringName skillId,
        SkillDef skillDef
    )
    {
        bool hasDynamicStat = skillDef.dynamic_max_level_stat_id != "";
        if (!hasDynamicStat)
        {
            if (skillDef.dynamic_max_level_base != 0)
                errors.Add(
                    $"Skill {skillId} dynamic_max_level_base requires dynamic_max_level_stat_id."
                );
            if (skillDef.dynamic_max_level_per_stat != 0)
                errors.Add(
                    $"Skill {skillId} dynamic_max_level_per_stat requires dynamic_max_level_stat_id."
                );
            return;
        }

        if (skillDef.dynamic_max_level_base <= 0)
            errors.Add($"Skill {skillId} dynamic_max_level_base must be >= 1.");
        if (skillDef.dynamic_max_level_per_stat == 0)
            errors.Add(
                $"Skill {skillId} dynamic_max_level_per_stat must not be 0 when dynamic_max_level_stat_id is set."
            );
    }

    public void _append_attribute_growth_validation_errors(
        Array<string> errors,
        StringName skillId,
        SkillDef skillDef
    )
    {
        if (skillDef.attribute_growth_progress.Count == 0 && skillDef.growth_tier == "")
            return;
        if (!AttributeGrowthContentRules.is_valid_growth_tier(skillDef.growth_tier))
        {
            errors.Add($"Skill {skillId} uses unsupported growth_tier {skillDef.growth_tier}.");
            return;
        }

        int progressTotal = 0;
        foreach (object attributeKey in skillDef.attribute_growth_progress.Keys)
        {
            if (!TryStrictString(attributeKey, out string attributeKeyText) || attributeKeyText.StripEdges().Length == 0)
            {
                errors.Add(
                    $"Skill {skillId} attribute_growth_progress key {attributeKey} must be a non-empty String."
                );
                continue;
            }
            var attributeId = ProgressionDataUtils.to_string_name(attributeKey);
            TryGetDictionaryValue(skillDef.attribute_growth_progress, attributeKey, out object amountValue);
            if (!TryStrictInt(amountValue, out int amount))
            {
                errors.Add(
                    $"Skill {skillId} attribute_growth_progress for {attributeId} must be a positive int."
                );
                continue;
            }
            if (!AttributeGrowthContentRules.is_valid_attribute_id(attributeId))
                errors.Add(
                    $"Skill {skillId} attribute_growth_progress references invalid attribute {attributeId}."
                );
            if (amount <= 0)
                errors.Add(
                    $"Skill {skillId} attribute_growth_progress for {attributeId} must be a positive int."
                );
            progressTotal += amount;
        }

        int expectedTotal = AttributeGrowthContentRules.get_tier_budget(skillDef.growth_tier);
        if (progressTotal != expectedTotal)
            errors.Add(
                $"Skill {skillId} attribute_growth_progress total must equal {expectedTotal} for growth_tier {skillDef.growth_tier}."
            );
    }

    public void _append_combat_profile_validation_errors(
        Array<string> errors,
        StringName skillId,
        CombatSkillDef combatProfile
    )
    {
        AppendCombatProfileValidationErrors(errors, skillId, combatProfile, null);
    }

    private void AppendCombatProfileValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatSkillDef combatProfile,
        SkillDef skillDef
    )
    {
        if (combatProfile.skill_id != skillId)
            errors.Add($"Skill {skillId} combat_profile.skill_id must match skill_id.");
        if (combatProfile.target_mode == "")
            errors.Add($"Skill {skillId} combat_profile is missing target_mode.");
        else if (
            !CombatSkillTargetingContentRules.is_valid_combat_target_mode(combatProfile.target_mode)
        )
            errors.Add(
                $"Skill {skillId} combat_profile uses unsupported target_mode {combatProfile.target_mode}; expected one of {CombatSkillTargetingContentRules.valid_combat_target_mode_label()}."
            );

        if (combatProfile.target_team_filter == "")
            errors.Add($"Skill {skillId} combat_profile is missing target_team_filter.");
        else if (
            !CombatTargetTeamContentRules.is_valid_skill_target_team_filter(
                combatProfile.target_team_filter
            )
        )
            errors.Add(
                $"Skill {skillId} combat_profile uses unsupported target_team_filter {combatProfile.target_team_filter}; expected one of {CombatTargetTeamContentRules.valid_skill_target_team_filter_label()}."
            );

        if (combatProfile.target_selection_mode == "")
            errors.Add($"Skill {skillId} combat_profile is missing target_selection_mode.");
        else if (
            !CombatSkillTargetingContentRules.is_valid_target_selection_mode(
                combatProfile.target_selection_mode
            )
        )
            errors.Add(
                $"Skill {skillId} combat_profile uses unsupported target_selection_mode {combatProfile.target_selection_mode}; expected one of {CombatSkillTargetingContentRules.valid_target_selection_mode_label()}."
            );

        if (combatProfile.selection_order_mode == "")
            errors.Add($"Skill {skillId} combat_profile is missing selection_order_mode.");
        else if (
            !CombatSkillTargetingContentRules.is_valid_selection_order_mode(
                combatProfile.selection_order_mode
            )
        )
            errors.Add(
                $"Skill {skillId} combat_profile uses unsupported selection_order_mode {combatProfile.selection_order_mode}; expected one of {CombatSkillTargetingContentRules.valid_selection_order_mode_label()}."
            );

        if (!CombatSkillTargetingContentRules.is_valid_area_pattern(combatProfile.area_pattern))
            errors.Add(
                $"Skill {skillId} combat_profile uses unsupported area_pattern {combatProfile.area_pattern}; expected one of {CombatSkillTargetingContentRules.valid_area_pattern_label()}."
            );
        if (!ValidMasteryTriggerModes.Contains(combatProfile.mastery_trigger_mode))
            errors.Add(
                $"Skill {skillId} combat_profile uses unsupported mastery_trigger_mode {combatProfile.mastery_trigger_mode}."
            );
        if (!ValidMasteryAmountModes.Contains(combatProfile.mastery_amount_mode))
            errors.Add(
                $"Skill {skillId} combat_profile uses unsupported mastery_amount_mode {combatProfile.mastery_amount_mode}."
            );
        if (combatProfile.range_value < 0)
            errors.Add($"Skill {skillId} combat_profile range_value must be >= 0.");
        if (combatProfile.area_value < 0)
            errors.Add($"Skill {skillId} combat_profile area_value must be >= 0.");
        if (
            combatProfile.ap_cost < 0
            || combatProfile.mp_cost < 0
            || combatProfile.stamina_cost < 0
            || combatProfile.aura_cost < 0
        )
            errors.Add($"Skill {skillId} combat_profile costs must be >= 0.");
        if (!_is_valid_tu_value(combatProfile.cooldown_tu))
            errors.Add(
                $"Skill {skillId} combat_profile cooldown_tu must be 0 or a multiple of {TuGranularity}."
            );

        _append_spell_fate_validation_errors(errors, skillId, combatProfile);
        _append_string_name_array_validation_errors(
            errors,
            skillId,
            "combat_profile.required_weapon_families",
            combatProfile.required_weapon_families
        );
        _append_string_name_array_validation_errors(
            errors,
            skillId,
            "combat_profile.excluded_weapon_families",
            combatProfile.excluded_weapon_families
        );
        _append_string_name_array_validation_errors(
            errors,
            skillId,
            "combat_profile.excluded_weapon_type_ids",
            combatProfile.excluded_weapon_type_ids
        );

        foreach (object overrideLevelKey in combatProfile.level_overrides.Keys)
        {
            if (!TryStrictInt(overrideLevelKey, out int overrideLevel))
            {
                errors.Add(
                    $"Skill {skillId} combat_profile level override key {overrideLevelKey} must be an int."
                );
                continue;
            }
            TryGetDictionaryValue(combatProfile.level_overrides, overrideLevelKey, out object overrideData);
            if (!TryAsDictionary(overrideData, out Dictionary overrideDict))
            {
                errors.Add(
                    $"Skill {skillId} combat_profile level override {overrideLevelKey} must be a Dictionary."
                );
                continue;
            }
            if (overrideLevel < 0)
                errors.Add(
                    $"Skill {skillId} combat_profile level override {overrideLevelKey} must use a non-negative level."
                );
            foreach (string costKey in new[] { "ap_cost", "mp_cost", "stamina_cost", "aura_cost" })
            {
                if (overrideDict.ContainsKey(costKey) && DictInt(overrideDict, costKey) < 0)
                    errors.Add(
                        $"Skill {skillId} combat_profile level override {overrideLevelKey}.{costKey} must be >= 0."
                    );
            }
            if (
                overrideDict.ContainsKey("cooldown_tu")
                && !_is_valid_tu_value(DictInt(overrideDict, "cooldown_tu"))
            )
                errors.Add(
                    $"Skill {skillId} combat_profile level override {overrideLevelKey}.cooldown_tu must be 0 or a multiple of {TuGranularity}."
                );
            if (overrideDict.ContainsKey("area_value") && DictInt(overrideDict, "area_value") < 0)
                errors.Add(
                    $"Skill {skillId} combat_profile level override {overrideLevelKey}.area_value must be >= 0."
                );
            if (overrideDict.ContainsKey("area_pattern"))
            {
                var overrideAreaPattern = ProgressionDataUtils.to_string_name(
                    overrideDict["area_pattern"]
                );
                if (!CombatSkillTargetingContentRules.is_valid_area_pattern(overrideAreaPattern))
                    errors.Add(
                        $"Skill {skillId} combat_profile level override {overrideLevelKey}.area_pattern uses unsupported area_pattern {overrideAreaPattern}; expected one of {CombatSkillTargetingContentRules.valid_area_pattern_label()}."
                    );
            }
            if (
                overrideDict.ContainsKey("max_target_count")
                && DictInt(overrideDict, "max_target_count") < 1
            )
                errors.Add(
                    $"Skill {skillId} combat_profile level override {overrideLevelKey}.max_target_count must be >= 1."
                );
        }

        if (combatProfile.min_target_count <= 0)
            errors.Add($"Skill {skillId} combat_profile min_target_count must be >= 1.");
        if (combatProfile.max_target_count < combatProfile.min_target_count)
            errors.Add(
                $"Skill {skillId} combat_profile max_target_count must be >= min_target_count."
            );

        for (int effectIndex = 0; effectIndex < combatProfile.effect_defs.Count; effectIndex++)
            _append_effect_validation_errors(
                errors,
                skillId,
                combatProfile.effect_defs[effectIndex],
                $"combat_profile.effect_defs[{effectIndex}]"
            );

        if (
            combatProfile.passive_effect_defs != null
            && combatProfile.passive_effect_defs.Count > 0
        )
        {
            for (
                int passiveIndex = 0;
                passiveIndex < combatProfile.passive_effect_defs.Count;
                passiveIndex++
            )
            {
                CombatEffectDef passiveEffect = combatProfile.passive_effect_defs[passiveIndex];
                if (passiveEffect != null && passiveEffect.effect_type == "execute")
                {
                    errors.Add(
                        $"Skill {skillId} passive_effect_defs[{passiveIndex}] uses effect_type 'execute', which is not allowed in passive effects."
                    );
                    continue;
                }
                _append_effect_validation_errors(
                    errors,
                    skillId,
                    passiveEffect,
                    $"combat_profile.passive_effect_defs[{passiveIndex}]"
                );
            }
        }

        var seenOptionIds = new HashSet<StringName>();
        for (int optionIndex = 0; optionIndex < combatProfile.cast_variants.Count; optionIndex++)
        {
            CombatCastVariantDef castVariant = combatProfile.cast_variants[optionIndex];
            if (castVariant == null)
            {
                errors.Add(
                    $"Skill {skillId} combat_profile.cast_variants[{optionIndex}] failed to cast to CombatCastVariantDef."
                );
                continue;
            }
            if (castVariant.variant_id == "")
                errors.Add($"Skill {skillId} has a cast option without variant_id.");
            else if (!seenOptionIds.Add(castVariant.variant_id))
                errors.Add(
                    $"Skill {skillId} declares duplicate cast option {castVariant.variant_id}."
                );

            if (castVariant.target_mode == "")
                errors.Add(
                    $"Skill {skillId} cast option {castVariant.variant_id} is missing target_mode."
                );
            else if (
                !CombatSkillTargetingContentRules.is_valid_cast_variant_target_mode(
                    castVariant.target_mode
                )
            )
                errors.Add(
                    $"Skill {skillId} cast option {castVariant.variant_id} uses unsupported target_mode {castVariant.target_mode}; expected one of {CombatSkillTargetingContentRules.valid_cast_variant_target_mode_label()}."
                );

            if (
                !CombatSkillTargetingContentRules.is_valid_footprint_pattern(
                    castVariant.footprint_pattern
                )
            )
                errors.Add(
                    $"Skill {skillId} cast option {castVariant.variant_id} uses unsupported footprint_pattern {castVariant.footprint_pattern}; expected one of {CombatSkillTargetingContentRules.valid_footprint_pattern_label()}."
                );

            if (castVariant.min_skill_level < 0)
                errors.Add(
                    $"Skill {skillId} cast option {castVariant.variant_id} min_skill_level must be >= 0."
                );
            else if (
                skillDef != null
                && skillDef.dynamic_max_level_stat_id == ""
                && skillDef.max_level >= 0
                && castVariant.min_skill_level > skillDef.max_level
            )
                errors.Add(
                    $"Skill {skillId} cast option {castVariant.variant_id} min_skill_level must be <= max_level {skillDef.max_level}."
                );

            if (castVariant.required_coord_count <= 0)
                errors.Add(
                    $"Skill {skillId} cast option {castVariant.variant_id} must have required_coord_count >= 1."
                );

            for (int effectIndex = 0; effectIndex < castVariant.effect_defs.Count; effectIndex++)
                _append_effect_validation_errors(
                    errors,
                    skillId,
                    castVariant.effect_defs[effectIndex],
                    $"combat_profile.cast_variants[{optionIndex}].effect_defs[{effectIndex}]"
                );
        }
    }

    public void _append_spell_fate_validation_errors(
        Array<string> errors,
        StringName skillId,
        CombatSkillDef combatProfile
    )
    {
        if (combatProfile == null)
            return;
        if (!ValidSpellFateModes.Contains(combatProfile.spell_fate_mode))
            errors.Add(
                $"Skill {skillId} combat_profile uses unsupported spell_fate_mode {combatProfile.spell_fate_mode}."
            );
        if (!ValidSpellCriticalModes.Contains(combatProfile.spell_critical_mode))
            errors.Add(
                $"Skill {skillId} combat_profile uses unsupported spell_critical_mode {combatProfile.spell_critical_mode}."
            );
        if (!ValidBacklashModes.Contains(combatProfile.backlash_mode))
            errors.Add(
                $"Skill {skillId} combat_profile uses unsupported backlash_mode {combatProfile.backlash_mode}."
            );
        if (combatProfile.spell_critical_mode != "" && combatProfile.spell_fate_mode == "")
            errors.Add(
                $"Skill {skillId} combat_profile spell_critical_mode requires spell_fate_mode."
            );
        if (combatProfile.backlash_mode != "" && combatProfile.spell_fate_mode == "")
            errors.Add($"Skill {skillId} combat_profile backlash_mode requires spell_fate_mode.");
        if (
            combatProfile.spell_critical_mp_refund_percent < 0
            || combatProfile.spell_critical_mp_refund_percent > 100
        )
            errors.Add(
                $"Skill {skillId} combat_profile spell_critical_mp_refund_percent must be between 0 and 100."
            );
        if (combatProfile.fumble_protection_extra_mp_percent < 0)
            errors.Add(
                $"Skill {skillId} combat_profile fumble_protection_extra_mp_percent must be >= 0."
            );
        foreach (int protectionValue in combatProfile.fumble_protection_curve)
        {
            if (protectionValue < 0)
            {
                errors.Add(
                    $"Skill {skillId} combat_profile fumble_protection_curve values must be >= 0."
                );
                break;
            }
        }
        if (combatProfile.backlash_offset_radius < 0)
            errors.Add($"Skill {skillId} combat_profile backlash_offset_radius must be >= 0.");
        if (combatProfile.backlash_mode == "ground_anchor_drift")
        {
            if (combatProfile.target_mode != "ground")
                errors.Add(
                    $"Skill {skillId} combat_profile ground_anchor_drift requires target_mode ground."
                );
            if (combatProfile.backlash_offset_radius <= 0)
                errors.Add(
                    $"Skill {skillId} combat_profile ground_anchor_drift requires backlash_offset_radius >= 1."
                );
        }
    }

    public void _append_effect_validation_errors(
        Array<string> errors,
        StringName skillId,
        CombatEffectDef effectDef,
        string contextLabel
    )
    {
        if (effectDef == null)
        {
            errors.Add($"Skill {skillId} has a null effect in {contextLabel}.");
            return;
        }
        if (effectDef.effect_type == "")
        {
            errors.Add($"Skill {skillId} has an effect without effect_type in {contextLabel}.");
            return;
        }
        if (!ValidEffectTypes.Contains(effectDef.effect_type))
            errors.Add(
                $"Skill {skillId} effect {contextLabel} uses unsupported effect_type {effectDef.effect_type}."
            );
        if (effectDef.min_skill_level < 0)
            errors.Add($"Skill {skillId} effect {contextLabel} min_skill_level must be >= 0.");
        if (effectDef.max_skill_level >= 0 && effectDef.max_skill_level < effectDef.min_skill_level)
            errors.Add(
                $"Skill {skillId} effect {contextLabel} max_skill_level must be >= min_skill_level or -1."
            );
        if (!ValidEffectTriggerEvents.Contains(effectDef.trigger_event))
            errors.Add(
                $"Skill {skillId} effect {contextLabel} uses unsupported trigger_event {effectDef.trigger_event}."
            );
        if (!ValidTriggerConditions.Contains(effectDef.trigger_condition))
            errors.Add(
                $"Skill {skillId} effect {contextLabel} uses unsupported trigger_condition {effectDef.trigger_condition}."
            );
        if (
            !CombatTargetTeamContentRules.is_valid_effect_target_team_filter(
                effectDef.effect_target_team_filter
            )
        )
            errors.Add(
                $"Skill {skillId} effect {contextLabel} uses unsupported effect_target_team_filter {effectDef.effect_target_team_filter}; expected one of {CombatTargetTeamContentRules.valid_effect_target_team_filter_label()}."
            );
        if (!_is_valid_tu_value(effectDef.duration_tu))
            errors.Add(
                $"Skill {skillId} effect {contextLabel} duration_tu must be 0 or a multiple of {TuGranularity}."
            );
        if (!_is_valid_tu_value(effectDef.tick_interval_tu))
            errors.Add(
                $"Skill {skillId} effect {contextLabel} tick_interval_tu must be 0 or a multiple of {TuGranularity}."
            );

        _append_save_validation_errors(errors, skillId, effectDef, contextLabel);

        Dictionary parameters = effectDef.@params ?? new Dictionary();
        var unsupportedParamAliases = new System.Collections.Generic.Dictionary<string, string>
        {
            { "damage_dice_count", "dice_count" },
            { "damage_dice_sides", "dice_sides" },
            { "damage_dice_bonus", "dice_bonus" },
            { "tag", "damage_tag" },
            { "bypass_tag", "dr_bypass_tag" },
            { "low_hp_ratio", "hp_ratio_threshold_percent" },
        };
        foreach (var alias in unsupportedParamAliases)
        {
            if (parameters.ContainsKey(alias.Key))
                errors.Add(
                    $"Skill {skillId} effect {contextLabel} params.{alias.Key} is unsupported; use {alias.Value}."
                );
        }
        if (parameters.ContainsKey("duration"))
            errors.Add(
                $"Skill {skillId} effect {contextLabel} params.duration is unsupported; use duration_tu."
            );
        if (parameters.ContainsKey("duration_tu"))
        {
            int paramsDurationTu = DictInt(parameters, "duration_tu");
            if (!_is_valid_tu_value(paramsDurationTu))
                errors.Add(
                    $"Skill {skillId} effect {contextLabel} params.duration_tu must be 0 or a multiple of {TuGranularity}."
                );
        }
        _append_weapon_param_validation_errors(errors, skillId, effectDef, contextLabel);

        if (effectDef.effect_type == "damage")
        {
            _append_damage_effect_validation_errors(errors, skillId, effectDef, contextLabel);
        }
        else if (effectDef.effect_type == "status" || effectDef.effect_type == "apply_status")
        {
            if (effectDef.status_id == "")
                errors.Add(
                    $"Skill {skillId} status effect in {contextLabel} is missing status_id."
                );
            _append_status_damage_filter_validation_errors(
                errors,
                skillId,
                effectDef,
                contextLabel
            );
        }
        else if (effectDef.effect_type == "shield")
        {
            bool hasDiceKeys =
                parameters.ContainsKey("dice_count") || parameters.ContainsKey("dice_sides");
            bool hasValidDiceConfig = _has_valid_shield_dice_config(effectDef);
            if (effectDef.power <= 0 && !hasValidDiceConfig)
                errors.Add(
                    $"Skill {skillId} shield effect in {contextLabel} must have power >= 1 or a valid dice_count/dice_sides config."
                );
            if (hasDiceKeys && !hasValidDiceConfig)
                errors.Add(
                    $"Skill {skillId} shield effect in {contextLabel} must set dice_count and dice_sides >= 1 together."
                );
            if (
                effectDef.duration_tu <= 0
                && DictInt(parameters, "duration_tu") <= 0
            )
                errors.Add(
                    $"Skill {skillId} shield effect in {contextLabel} must have positive duration_tu in {TuGranularity} TU steps."
                );
        }
        else if (effectDef.effect_type == "terrain_effect")
        {
            if (effectDef.terrain_effect_id == "")
                errors.Add(
                    $"Skill {skillId} terrain_effect in {contextLabel} is missing terrain_effect_id."
                );
            if (effectDef.duration_tu > 0 && effectDef.tick_interval_tu <= 0)
                errors.Add(
                    $"Skill {skillId} terrain_effect in {contextLabel} must have positive tick_interval_tu in {TuGranularity} TU steps."
                );
        }
        else if (
            effectDef.effect_type == "terrain"
            || effectDef.effect_type == "terrain_replace"
            || effectDef.effect_type == "terrain_replace_to"
        )
        {
            if (effectDef.terrain_replace_to == "")
                errors.Add(
                    $"Skill {skillId} terrain_replace effect in {contextLabel} is missing terrain_replace_to."
                );
        }
        else if (effectDef.effect_type == "height" || effectDef.effect_type == "height_delta")
        {
            if (effectDef.height_delta == 0)
                errors.Add(
                    $"Skill {skillId} height effect in {contextLabel} must have non-zero height_delta."
                );
        }
        else if (effectDef.effect_type == "body_size_category_override")
        {
            if (effectDef.status_id == "")
                errors.Add(
                    $"Skill {skillId} body_size_category_override effect in {contextLabel} is missing status_id."
                );
            if (effectDef.body_size_category == "")
                errors.Add(
                    $"Skill {skillId} body_size_category_override effect in {contextLabel} is missing body_size_category."
                );
            else if (
                !BodySizeContentRules.is_valid_body_size_category(effectDef.body_size_category)
            )
                errors.Add(
                    $"Skill {skillId} body_size_category_override effect in {contextLabel} uses unsupported body_size_category {effectDef.body_size_category}."
                );
            if (effectDef.duration_tu <= 0)
                errors.Add(
                    $"Skill {skillId} body_size_category_override effect in {contextLabel} must have positive duration_tu."
                );
        }
        else if (effectDef.effect_type == "forced_move")
        {
            if (parameters.ContainsKey("mode"))
                errors.Add(
                    $"Skill {skillId} forced_move effect in {contextLabel} params.mode is unsupported; use forced_move_mode."
                );
            if (parameters.ContainsKey("distance"))
                errors.Add(
                    $"Skill {skillId} forced_move effect in {contextLabel} params.distance is unsupported; use forced_move_distance."
                );
            if (effectDef.forced_move_mode == "")
                errors.Add(
                    $"Skill {skillId} forced_move effect in {contextLabel} is missing forced_move_mode."
                );
            if (effectDef.forced_move_mode == "jump")
                _append_jump_effect_validation_errors(errors, skillId, effectDef, contextLabel);
            else if (effectDef.forced_move_distance <= 0)
                errors.Add(
                    $"Skill {skillId} forced_move effect in {contextLabel} must have forced_move_distance >= 1."
                );
        }
        else if (effectDef.effect_type == "charge")
        {
            if (
                DictStringName(parameters, "skill_id").ToString().Length == 0
            )
                errors.Add(
                    $"Skill {skillId} charge effect in {contextLabel} is missing params.skill_id."
                );
        }
        else if (effectDef.effect_type == "path_step_aoe")
        {
            _append_path_step_aoe_validation_errors(errors, skillId, effectDef, contextLabel);
        }
        else if (effectDef.effect_type == "equipment_durability_damage")
        {
            _append_equipment_durability_damage_validation_errors(
                errors,
                skillId,
                effectDef,
                contextLabel
            );
        }
    }

    public void _append_damage_effect_validation_errors(
        Array<string> errors,
        StringName skillId,
        CombatEffectDef effectDef,
        string contextLabel
    )
    {
        if (effectDef == null)
            return;
        Dictionary parameters = effectDef.@params ?? new Dictionary();
        var damageTag = effectDef.damage_tag;
        bool usesWeaponDamageTag = effectDef.use_weapon_physical_damage_tag;

        if (parameters.ContainsKey("damage_tag"))
            errors.Add(
                $"Skill {skillId} damage effect in {contextLabel} params.damage_tag is unsupported on damage effects; use damage_tag or use_weapon_physical_damage_tag."
            );
        if (usesWeaponDamageTag)
        {
            if (damageTag != "")
                errors.Add(
                    $"Skill {skillId} damage effect in {contextLabel} cannot combine damage_tag with use_weapon_physical_damage_tag."
                );
        }
        else
        {
            if (damageTag == "")
                errors.Add(
                    $"Skill {skillId} damage effect in {contextLabel} must declare damage_tag or set use_weapon_physical_damage_tag = true."
                );
            else if (!DamageTagContentRules.is_valid_damage_tag(damageTag))
                errors.Add(
                    $"Skill {skillId} damage effect in {contextLabel} uses unsupported damage_tag {damageTag}; expected one of {DamageTagContentRules.valid_damage_tag_label()}."
                );
        }

        if (parameters.ContainsKey("hp_ratio_threshold_percent"))
        {
            if (
                !TryStrictInt(parameters["hp_ratio_threshold_percent"], out int thresholdValue)
                || thresholdValue < 1
                || thresholdValue > 100
            )
                errors.Add(
                    $"Skill {skillId} damage effect in {contextLabel} params.hp_ratio_threshold_percent must be an int from 1 to 100."
                );
        }

        bool hasBonusDiceKey =
            parameters.ContainsKey("bonus_damage_dice_count")
            || parameters.ContainsKey("bonus_damage_dice_sides")
            || parameters.ContainsKey("bonus_damage_dice_bonus");
        if (!hasBonusDiceKey)
            return;
        if (effectDef.bonus_condition == "")
            errors.Add(
                $"Skill {skillId} damage effect in {contextLabel} bonus_damage_dice requires bonus_condition."
            );
        if (
            !parameters.ContainsKey("bonus_damage_dice_count")
            || !TryStrictInt(parameters["bonus_damage_dice_count"], out int countValue)
            || countValue < 1
        )
            errors.Add(
                $"Skill {skillId} damage effect in {contextLabel} params.bonus_damage_dice_count must be a positive int."
            );
        if (
            !parameters.ContainsKey("bonus_damage_dice_sides")
            || !TryStrictInt(parameters["bonus_damage_dice_sides"], out int sidesValue)
            || sidesValue < 1
        )
            errors.Add(
                $"Skill {skillId} damage effect in {contextLabel} params.bonus_damage_dice_sides must be a positive int."
            );
        if (
            parameters.ContainsKey("bonus_damage_dice_bonus")
            && !TryStrictInt(parameters["bonus_damage_dice_bonus"], out _)
        )
            errors.Add(
                $"Skill {skillId} damage effect in {contextLabel} params.bonus_damage_dice_bonus must be an int."
            );
    }

    public void _append_status_damage_filter_validation_errors(
        Array<string> errors,
        StringName skillId,
        CombatEffectDef effectDef,
        string contextLabel
    )
    {
        if (effectDef == null || effectDef.@params == null)
            return;
        Dictionary parameters = effectDef.@params;
        if (parameters.ContainsKey("damage_tag"))
        {
            var damageTag = ProgressionDataUtils.to_string_name(parameters["damage_tag"]);
            if (damageTag == "" || !DamageTagContentRules.is_valid_damage_tag(damageTag))
                errors.Add(
                    $"Skill {skillId} status effect in {contextLabel} params.damage_tag must be one of {DamageTagContentRules.valid_damage_tag_label()}."
                );
        }
        if (parameters.ContainsKey("damage_tags"))
        {
            if (!TryAsArray(parameters["damage_tags"], out Array damageTagArray))
            {
                errors.Add(
                    $"Skill {skillId} status effect in {contextLabel} params.damage_tags must be an Array."
                );
            }
            else
            {
                for (int index = 0; index < damageTagArray.Count; index++)
                {
                    var damageTag = ProgressionDataUtils.to_string_name(damageTagArray[index]);
                    if (damageTag == "" || !DamageTagContentRules.is_valid_damage_tag(damageTag))
                        errors.Add(
                            $"Skill {skillId} status effect in {contextLabel} params.damage_tags[{index}] must be one of {DamageTagContentRules.valid_damage_tag_label()}."
                        );
                }
            }
        }
        if (parameters.ContainsKey("damage_category"))
        {
            var damageCategory = ProgressionDataUtils.to_string_name(
                parameters["damage_category"]
            );
            if (
                damageCategory == ""
                || !DamageTagContentRules.is_valid_damage_category(damageCategory)
            )
                errors.Add(
                    $"Skill {skillId} status effect in {contextLabel} params.damage_category must be one of {DamageTagContentRules.valid_damage_category_label()}."
                );
        }
        if (parameters.ContainsKey("mitigation_tier"))
        {
            var mitigationTier = ProgressionDataUtils.to_string_name(
                parameters["mitigation_tier"]
            );
            if (
                mitigationTier == ""
                || !DamageTagContentRules.is_valid_mitigation_tier(mitigationTier)
            )
                errors.Add(
                    $"Skill {skillId} status effect in {contextLabel} params.mitigation_tier must be one of {DamageTagContentRules.valid_mitigation_tier_label()}."
                );
        }
    }

    public void _append_equipment_durability_damage_validation_errors(
        Array<string> errors,
        StringName skillId,
        CombatEffectDef effectDef,
        string contextLabel
    )
    {
        if (effectDef == null)
            return;
        if (effectDef.power <= 0)
            errors.Add(
                $"Skill {skillId} equipment_durability_damage effect in {contextLabel} must have power >= 1."
            );
        Dictionary parameters = effectDef.@params ?? new Dictionary();
        bool hasDynamicSave =
            ProgressionDataUtils.to_string_name(effectDef.save_dc_mode)
            == BattleSaveContentRules.SAVE_DC_MODE_CASTER_SPELL;
        if (effectDef.save_dc <= 0 && !hasDynamicSave)
            errors.Add(
                $"Skill {skillId} equipment_durability_damage effect in {contextLabel} must configure a save DC."
            );
        var validationParameters = EquipmentDurabilityDamageValidationParameters.FromEffect(
            effectDef
        );
        if (validationParameters.MaxDamagedItems != 1)
            errors.Add(
                $"Skill {skillId} equipment_durability_damage effect in {contextLabel} currently supports max_damaged_items = 1 only."
            );
        if (!validationParameters.RequireDamageApplied)
            errors.Add(
                $"Skill {skillId} equipment_durability_damage effect in {contextLabel} must set require_damage_applied = true."
            );

        if (validationParameters.TargetSlotsMissingOrEmpty)
            errors.Add(
                $"Skill {skillId} equipment_durability_damage effect in {contextLabel} params.target_slots must include at least one slot."
            );
        _append_equipment_slot_array_validation_errors(
            errors,
            skillId,
            contextLabel,
            parameters,
            "target_slots"
        );
        _append_equipment_slot_weight_validation_errors(
            errors,
            skillId,
            contextLabel,
            parameters,
            "slot_weight_map"
        );
    }

    private void _append_equipment_slot_array_validation_errors(
        Array<string> errors,
        StringName skillId,
        string contextLabel,
        Dictionary parameters,
        string paramName
    )
    {
        if (parameters == null || !parameters.ContainsKey(paramName))
            return;
        object value = parameters[paramName];
        if (!TryAsArray(value, out Array slotValues))
        {
            errors.Add(
                $"Skill {skillId} equipment_durability_damage effect in {contextLabel} params.{paramName} must be an Array."
            );
            return;
        }
        var seenSlots = new HashSet<StringName>();
        foreach (object rawSlotId in slotValues)
        {
            var slotId = ProgressionDataUtils.to_string_name(rawSlotId);
            if (!EquipmentRules.is_valid_slot(slotId))
            {
                errors.Add(
                    $"Skill {skillId} equipment_durability_damage effect in {contextLabel} params.{paramName} uses unsupported slot {slotId}."
                );
                continue;
            }
            if (!seenSlots.Add(slotId))
                errors.Add(
                    $"Skill {skillId} equipment_durability_damage effect in {contextLabel} params.{paramName} repeats slot {slotId}."
                );
        }
    }

    private void _append_equipment_slot_weight_validation_errors(
        Array<string> errors,
        StringName skillId,
        string contextLabel,
        Dictionary parameters,
        string paramName
    )
    {
        if (parameters == null || !parameters.ContainsKey(paramName))
            return;
        object value = parameters[paramName];
        if (!TryAsDictionary(value, out Dictionary weightMap))
        {
            errors.Add(
                $"Skill {skillId} equipment_durability_damage effect in {contextLabel} params.{paramName} must be a Dictionary."
            );
            return;
        }
        foreach (object key in weightMap.Keys)
        {
            var slotId = ProgressionDataUtils.to_string_name(key);
            if (!EquipmentRules.is_valid_slot(slotId))
                errors.Add(
                    $"Skill {skillId} equipment_durability_damage effect in {contextLabel} params.{paramName} uses unsupported slot {slotId}."
                );
            TryGetDictionaryValue(weightMap, key, out object weightVariant);
            if (!TryStrictInt(weightVariant, out int weight) || weight <= 0)
                errors.Add(
                    $"Skill {skillId} equipment_durability_damage effect in {contextLabel} params.{paramName}.{slotId} must be a positive int."
                );
        }
    }

    public void _append_save_validation_errors(
        Array<string> errors,
        StringName skillId,
        CombatEffectDef effectDef,
        string contextLabel
    )
    {
        if (effectDef == null)
            return;
        int saveDc = effectDef.save_dc;
        var saveDcMode = ProgressionDataUtils.to_string_name(effectDef.save_dc_mode);
        bool dynamicSaveDc = saveDcMode == BattleSaveContentRules.SAVE_DC_MODE_CASTER_SPELL;
        bool hasSaveDc = saveDc > 0 || dynamicSaveDc;
        var saveAbility = ProgressionDataUtils.to_string_name(effectDef.save_ability);
        var saveDcSourceAbility = ProgressionDataUtils.to_string_name(
            effectDef.save_dc_source_ability
        );
        var saveTag = ProgressionDataUtils.to_string_name(effectDef.save_tag);
        if (!ValidSaveDcModes.Contains(saveDcMode))
            errors.Add(
                $"Skill {skillId} effect {contextLabel} uses unsupported save_dc_mode {saveDcMode}."
            );
        if (saveDc < 0)
            errors.Add($"Skill {skillId} effect {contextLabel} save_dc must be >= 0.");
        if (dynamicSaveDc && saveDc > 0)
            errors.Add(
                $"Skill {skillId} effect {contextLabel} caster_spell save_dc_mode must leave static save_dc at 0."
            );
        if (!dynamicSaveDc && saveDcSourceAbility != "")
            errors.Add(
                $"Skill {skillId} effect {contextLabel} save_dc_source_ability requires caster_spell save_dc_mode."
            );
        if (dynamicSaveDc && !ValidSaveAbilities.Contains(saveDcSourceAbility))
            errors.Add(
                $"Skill {skillId} effect {contextLabel} uses unsupported save_dc_source_ability {saveDcSourceAbility}."
            );

        if (!hasSaveDc)
        {
            if (saveAbility != "")
                errors.Add(
                    $"Skill {skillId} effect {contextLabel} save_ability requires save_dc >= 1 or caster_spell save_dc_mode."
                );
            if (saveTag != "")
                errors.Add(
                    $"Skill {skillId} effect {contextLabel} save_tag requires save_dc >= 1 or caster_spell save_dc_mode."
                );
            if (effectDef.save_failure_status_id != "")
                errors.Add(
                    $"Skill {skillId} effect {contextLabel} save_failure_status_id requires save_dc >= 1 or caster_spell save_dc_mode."
                );
            if (effectDef.save_partial_on_success)
                errors.Add(
                    $"Skill {skillId} effect {contextLabel} save_partial_on_success requires save_dc >= 1 or caster_spell save_dc_mode."
                );
            return;
        }

        if (!ValidSaveAbilities.Contains(saveAbility))
            errors.Add(
                $"Skill {skillId} effect {contextLabel} uses unsupported save_ability {saveAbility}."
            );
        if (!ValidSaveTags.Contains(saveTag))
            errors.Add(
                $"Skill {skillId} effect {contextLabel} uses unsupported save_tag {saveTag}."
            );
        if (effectDef.save_partial_on_success && effectDef.effect_type != "damage")
            errors.Add(
                $"Skill {skillId} effect {contextLabel} save_partial_on_success is only supported on damage effects."
            );
        if (
            effectDef.save_failure_status_id != ""
            && effectDef.effect_type != "status"
            && effectDef.effect_type != "apply_status"
        )
            errors.Add(
                $"Skill {skillId} effect {contextLabel} save_failure_status_id is only supported on status effects."
            );
    }

    public void _append_path_step_aoe_validation_errors(
        Array<string> errors,
        StringName skillId,
        CombatEffectDef effectDef,
        string contextLabel
    )
    {
        if (effectDef == null || effectDef.@params == null)
            return;
        Dictionary parameters = effectDef.@params;
        if (
            parameters.ContainsKey("path_step_log_label")
            && DictString(parameters, "path_step_log_label").StripEdges().Length == 0
        )
            errors.Add(
                $"Skill {skillId} path_step_aoe effect in {contextLabel} params.path_step_log_label must be non-empty when set."
            );
        if (!_has_repeat_hit_status_config(parameters))
            return;

        var statusId = DictStringName(parameters, "repeat_hit_status_id");
        if (statusId == "")
            errors.Add(
                $"Skill {skillId} path_step_aoe effect in {contextLabel} repeat-hit status config requires params.repeat_hit_status_id."
            );
        if (DictInt(parameters, "repeat_hit_status_threshold") < 1)
            errors.Add(
                $"Skill {skillId} path_step_aoe effect in {contextLabel} params.repeat_hit_status_threshold must be >= 1."
            );
        if (DictInt(parameters, "repeat_hit_status_min_skill_level") < 0)
            errors.Add(
                $"Skill {skillId} path_step_aoe effect in {contextLabel} params.repeat_hit_status_min_skill_level must be >= 0."
            );
        if (DictInt(parameters, "repeat_hit_status_power", 1) < 1)
            errors.Add(
                $"Skill {skillId} path_step_aoe effect in {contextLabel} params.repeat_hit_status_power must be >= 1."
            );
        if (!parameters.ContainsKey("repeat_hit_status_duration_tu"))
        {
            errors.Add(
                $"Skill {skillId} path_step_aoe effect in {contextLabel} repeat-hit status config requires params.repeat_hit_status_duration_tu."
            );
        }
        else
        {
            int durationTu = DictInt(parameters, "repeat_hit_status_duration_tu");
            if (durationTu <= 0 || !_is_valid_tu_value(durationTu))
                errors.Add(
                    $"Skill {skillId} path_step_aoe effect in {contextLabel} params.repeat_hit_status_duration_tu must be a positive multiple of {TuGranularity}."
                );
        }
        if (
            parameters.ContainsKey("repeat_hit_status_params")
            && !TryAsDictionary(parameters["repeat_hit_status_params"], out _)
        )
            errors.Add(
                $"Skill {skillId} path_step_aoe effect in {contextLabel} params.repeat_hit_status_params must be a Dictionary."
            );
    }

    public bool _has_repeat_hit_status_config(Dictionary parameters)
    {
        foreach (
            string key in new[]
            {
                "repeat_hit_status_id",
                "repeat_hit_status_threshold",
                "repeat_hit_status_min_skill_level",
                "repeat_hit_status_power",
                "repeat_hit_status_duration_tu",
                "repeat_hit_status_params",
                "repeat_hit_status_log_template",
            }
        )
        {
            if (parameters.ContainsKey(key))
                return true;
        }
        return false;
    }

    public void _append_jump_effect_validation_errors(
        Array<string> errors,
        StringName skillId,
        CombatEffectDef effectDef,
        string contextLabel
    )
    {
        if (effectDef.forced_move_distance < 0)
            errors.Add(
                $"Skill {skillId} jump effect in {contextLabel} must have forced_move_distance >= 0 (0 = no max_range cap)."
            );
        if (effectDef.jump_arc_ratio < CombatEffectDef.MIN_JUMP_ARC_RATIO())
            errors.Add(
                $"Skill {skillId} jump effect in {contextLabel} requires jump_arc_ratio >= {CombatEffectDef.MIN_JUMP_ARC_RATIO():0.00}; jump must lift the unit."
            );
        if (effectDef.jump_arc_ratio > 1.0)
            errors.Add(
                $"Skill {skillId} jump effect in {contextLabel} requires jump_arc_ratio <= 1.0."
            );
        if (effectDef.jump_base_budget < 0)
            errors.Add(
                $"Skill {skillId} jump effect in {contextLabel} must have jump_base_budget >= 0."
            );
        if (effectDef.jump_str_scale < 0.0)
            errors.Add(
                $"Skill {skillId} jump effect in {contextLabel} must have jump_str_scale >= 0."
            );
        if (effectDef.jump_range_multiplier < 1)
            errors.Add(
                $"Skill {skillId} jump effect in {contextLabel} must have jump_range_multiplier >= 1."
            );
    }

    public void _append_weapon_param_validation_errors(
        Array<string> errors,
        StringName skillId,
        CombatEffectDef effectDef,
        string contextLabel
    )
    {
        Dictionary parameters = effectDef.@params ?? new Dictionary();
        foreach (string flagName in TypedEffectFlagParamNames)
        {
            if (parameters.ContainsKey(flagName))
                errors.Add(
                    $"Skill {skillId} effect {contextLabel} params.{flagName} is unsupported; use CombatEffectDef.{flagName}."
                );
        }
    }

    public void _append_string_name_array_validation_errors(
        Array<string> errors,
        StringName skillId,
        string fieldLabel,
        Array<StringName> values
    )
    {
        for (int index = 0; index < values.Count; index++)
        {
            var value = values[index];
            if (value == "")
                errors.Add($"Skill {skillId} {fieldLabel}[{index}] must be non-empty.");
        }
    }

    public bool _is_valid_tu_value(int value)
    {
        if (value < 0)
            return false;
        if (value == 0)
            return true;
        return value % TuGranularity == 0;
    }

    public bool _has_valid_shield_dice_config(CombatEffectDef effectDef)
    {
        if (effectDef == null || effectDef.@params == null)
            return false;
        return DictInt(effectDef.@params, "dice_count") > 0
            && DictInt(effectDef.@params, "dice_sides") > 0;
    }

    private static int DictInt(Dictionary dictionary, string key, int fallback = 0)
    {
        if (!TryGetParameter(dictionary, key, out object value))
            return fallback;
        if (value is Variant variant)
        {
            return variant.VariantType switch
            {
                Variant.Type.Int => variant.AsInt32(),
                Variant.Type.Float => (int)variant.AsDouble(),
                Variant.Type.Bool => variant.AsBool() ? 1 : 0,
                Variant.Type.String => int.TryParse(variant.AsString(), out int parsed)
                    ? parsed
                    : 0,
                Variant.Type.StringName => int.TryParse(
                    variant.AsStringName().ToString(),
                    out int parsed
                )
                    ? parsed
                    : 0,
                _ => 0,
            };
        }
        return value switch
        {
            int intValue => intValue,
            long longValue => (int)longValue,
            float floatValue => (int)floatValue,
            double doubleValue => (int)doubleValue,
            bool boolValue => boolValue ? 1 : 0,
            string stringValue => int.TryParse(stringValue, out int parsed) ? parsed : 0,
            StringName stringName => int.TryParse(stringName.ToString(), out int parsed)
                ? parsed
                : 0,
            _ => 0,
        };
    }

    private static string DictString(Dictionary dictionary, string key, string fallback = "")
    {
        if (!TryGetParameter(dictionary, key, out object value))
            return fallback;
        return value is Variant variant ? variant.AsString() : value?.ToString() ?? "";
    }

    private static StringName DictStringName(
        Dictionary dictionary,
        string key,
        StringName fallback = default
    )
    {
        return TryGetParameter(dictionary, key, out object value)
            ? ProgressionDataUtils.to_string_name(value)
            : fallback;
    }

    private static bool TryAsArray(object rawValue, out Array value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Array)
        {
            value = variant.AsGodotArray();
            return true;
        }
        if (rawValue is Array array)
        {
            value = array;
            return true;
        }
        value = new Array();
        return false;
    }

    private static bool TryAsDictionary(object rawValue, out Dictionary value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Dictionary)
        {
            value = variant.AsGodotDictionary();
            return true;
        }
        if (rawValue is Dictionary dictionary)
        {
            value = dictionary;
            return true;
        }
        value = new Dictionary();
        return false;
    }

    private static bool TryStrictBool(object rawValue, out bool value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Bool)
        {
            value = variant.AsBool();
            return true;
        }
        if (rawValue is bool boolValue)
        {
            value = boolValue;
            return true;
        }
        value = false;
        return false;
    }

    private static bool TryStrictInt(object rawValue, out int value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Int)
        {
            value = variant.AsInt32();
            return true;
        }
        if (rawValue is int intValue)
        {
            value = intValue;
            return true;
        }
        value = 0;
        return false;
    }

    private static bool TryStrictString(object rawValue, out string value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.String)
        {
            value = variant.AsString();
            return true;
        }
        if (rawValue is string stringValue)
        {
            value = stringValue;
            return true;
        }
        value = "";
        return false;
    }

    private static bool TryGetParameter(Dictionary dictionary, string key, out object value)
    {
        if (dictionary != null && dictionary.ContainsKey(key))
        {
            value = dictionary[key];
            return true;
        }
        value = null;
        return false;
    }

    private static bool TryGetDictionaryValue(Dictionary dictionary, object key, out object value)
    {
        Variant variantKey = ToVariantKey(key);
        if (dictionary != null && dictionary.ContainsKey(variantKey))
        {
            value = dictionary[variantKey];
            return true;
        }
        value = null;
        return false;
    }

    private static Variant ToVariantKey(object key)
    {
        return key switch
        {
            Variant variant => variant,
            StringName stringName => Variant.From(stringName),
            string text => Variant.From(text),
            int intValue => Variant.From(intValue),
            long longValue => Variant.From(longValue),
            float floatValue => Variant.From(floatValue),
            double doubleValue => Variant.From(doubleValue),
            bool boolValue => Variant.From(boolValue),
            Vector2I coord => Variant.From(coord),
            _ => Variant.From(key?.ToString() ?? ""),
        };
    }

    private static T GetTyped<T>(Dictionary dictionary, StringName key)
        where T : class
    {
        if (dictionary.ContainsKey(key))
            return dictionary[key].AsGodotObject() as T;
        string keyText = key.ToString();
        if (dictionary.ContainsKey(keyText))
            return dictionary[keyText].AsGodotObject() as T;
        var keyName = new StringName(keyText);
        if (dictionary.ContainsKey(keyName))
            return dictionary[keyName].AsGodotObject() as T;
        return null;
    }

    private static void AppendArray(Array<string> target, Array<string> source)
    {
        foreach (string value in source)
            target.Add(value);
    }
}
