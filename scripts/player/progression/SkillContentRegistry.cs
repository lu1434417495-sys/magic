using System.Collections.Generic;
using Godot;
using Godot.Collections;
using VT = Godot.Variant.Type;

public class SkillContentRegistry : System.IDisposable
{
    private const string SkillConfigDirectory = "res://data/configs/skills";
    private const int TuGranularity = 5;

    private static readonly System.Collections.Generic.Dictionary<string, string> TypedEffectParamTargets =
        new()
        {
            { "dice_count", "dice_count" },
            { "dice_sides", "dice_sides" },
            { "dice_bonus", "dice_bonus" },
            { "base_sides", "dice_sides_base" },
            { "con_mod_sides", "dice_sides_per_constitution_mod" },
            { "will_mod_sides", "dice_sides_per_willpower_mod" },
            { "dice_sides_base", "dice_sides_base" },
            { "dice_sides_per_constitution_mod", "dice_sides_per_constitution_mod" },
            { "dice_sides_per_willpower_mod", "dice_sides_per_willpower_mod" },
            { "runtime_pre_resistance_damage_multiplier", "pre_resistance_damage_multiplier" },
            { "dr_bypass_tag", "dr_bypass_tag" },
            { "hp_ratio_threshold_percent", "hp_ratio_threshold_percent" },
            { "bonus_damage_dice_count", "bonus_damage_dice_count" },
            { "bonus_damage_dice_sides", "bonus_damage_dice_sides" },
            { "bonus_damage_dice_bonus", "bonus_damage_dice_bonus" },
            { "add_weapon_dice", "add_weapon_dice" },
            { "requires_weapon", "requires_weapon" },
            { "use_weapon_physical_damage_tag", "use_weapon_physical_damage_tag" },
            { "resolve_as_weapon_attack", "resolve_as_weapon_attack" },
            { "allow_repeat_hits_across_steps", "allow_repeat_hits_across_steps" },
            { "prevent_repeat_target", "prevent_repeat_target" },
            { "stop_on_miss", "stop_on_miss" },
            { "stop_on_target_down", "stop_on_target_down" },
            { "remove_harmful", "remove_harmful" },
            { "remove_harmful_from_allies", "remove_harmful_from_allies" },
            { "remove_beneficial", "remove_beneficial" },
            { "remove_beneficial_from_enemies", "remove_beneficial_from_enemies" },
            { "require_damage_applied", "require_damage_applied" },
            { "lifetime_policy", "lifetime_policy" },
            { "move_cost_delta", "move_cost_delta" },
            { "min_hp_after_damage", "min_hp_after_damage" },
            { "threshold_base_value", "threshold_base_value" },
            { "threshold_level_anchor", "threshold_level_anchor" },
            { "threshold_level_bonus_per_delta", "threshold_level_bonus_per_delta" },
            { "threshold_max_hp_ratio_percent", "threshold_max_hp_ratio_percent" },
            { "threshold_cap_max_hp_ratio_percent", "threshold_cap_max_hp_ratio_percent" },
            { "soul_fracture_duration_tu", "soul_fracture_duration_tu" },
            { "heal_multiplier_percent", "heal_multiplier_percent" },
            { "shield_gain_multiplier_percent", "shield_gain_multiplier_percent" },
            { "attack_roll_penalty", "attack_roll_penalty" },
            { "undispellable", "undispellable" },
            { "dispellable_magic", "dispellable_magic" },
            { "dispellable_harmful_magic", "dispellable_harmful_magic" },
            { "dispellable_beneficial_magic", "dispellable_beneficial_magic" },
            { "mitigation_tier", "mitigation_tier" },
            { "counts_as_debuff_override", "counts_as_debuff_override" },
            { "counts_as_debuff", "counts_as_debuff" },
            { "lock_counterattack", "lock_counterattack" },
            { "lock_guard", "lock_guard" },
            { "lock_dodge_bonus", "lock_dodge_bonus" },
            { "lock_crit", "lock_crit" },
            { "save_bonus", "save_bonus" },
            { "control_save_bonus", "control_save_bonus" },
            { "save_advantage_tags", "save_advantage_tags" },
            { "save_disadvantage_tags", "save_disadvantage_tags" },
            { "save_immunity_tags", "save_immunity_tags" },
            { "save_tags", "save_tags" },
            { "secondary_hit_save_bonus", "control_save_bonus" },
            { "passive_reduction", "passive_reduction" },
            { "content_dr", "content_dr" },
            { "guard_block", "guard_block" },
            { "main_skill_lock_other_debuff_count", "main_skill_lock_other_debuff_count" },
            { "ap_gain", "ap_gain" },
            { "free_move_points_gain", "free_move_points_gain" },
        };

    private static readonly string[] GradedSaveExecuteParamKeys =
    {
        "profile_id",
        "failure_execute_threshold_fixed",
        "failure_execute_threshold_max_hp_percent",
        "failure_damage_dice_count",
        "failure_damage_dice_sides",
        "failure_frightened_duration_tu",
        "failure_reaction_lock_duration_tu",
        "critical_failure_execute_threshold_max_hp_percent",
        "critical_failure_damage_dice_count",
        "critical_failure_damage_dice_sides",
        "critical_failure_frightened_duration_tu",
        "critical_failure_stunned_duration_tu",
        "success_aftershock_duration_tu",
    };

    private static readonly HashSet<string> GradedSaveExecuteParamKeySet =
        new(GradedSaveExecuteParamKeys, System.StringComparer.Ordinal);

    private static readonly string GradedSaveExecuteParamKeyLabel = string.Join(
        ", ",
        GradedSaveExecuteParamKeys
    );

    private static readonly StringName[] PracticeTrackTags = { "meditation", "cultivation" };

    public Dictionary _skill_defs { get; set; } = new();
    private readonly List<string> _validationErrors = new();
    public Array<string> _validation_errors
    {
        get => ToGodotStringArray(_validationErrors);
        set
        {
            _validationErrors.Clear();
            if (value == null)
                return;
            foreach (string error in value)
                _validationErrors.Add(error);
        }
    }
    private bool _disposed;

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
        Rebuild();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        System.GC.SuppressFinalize(this);
        DisposeManagedRegistry();
    }

    private void DisposeManagedRegistry()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _skill_defs.Clear();
        _validationErrors.Clear();
    }

    public void Rebuild()
    {
        LoadFromDirectory(SkillConfigDirectory);
    }

    public void LoadFromDirectory(string directoryPath)
    {
        _skill_defs.Clear();
        _validationErrors.Clear();
        ScanDirectory(directoryPath);
        AppendArray(_validationErrors, CollectValidationErrors());
    }

    internal Dictionary DuplicateSkillResourceBucketForProgressionRegistry()
    {
        return _skill_defs.Duplicate();
    }

    private IReadOnlyDictionary<StringName, SkillDef> BuildSkillDefIndex()
    {
        var result = new System.Collections.Generic.Dictionary<StringName, SkillDef>();
        foreach (Variant key in _skill_defs.Keys)
        {
            if (key.VariantType != VT.StringName)
                continue;
            StringName skillId = key.AsStringName();
            if (skillId == default || skillId == (StringName)"")
                continue;
            SkillDef skillDef = GetTyped<SkillDef>(_skill_defs, skillId);
            if (skillDef == null)
                continue;
            result[skillId] = skillDef;
        }
        return result;
    }

    internal IReadOnlyDictionary<StringName, SkillDefinition> GetSkillDefinitionsTyped()
    {
        return SkillDefinition.ProjectIndex(BuildSkillDefIndex());
    }

    public Array<string> Validate()
    {
        var copy = new Array<string>();
        foreach (string error in _validationErrors)
            copy.Add(error);
        return copy;
    }

    private void ScanDirectory(string directoryPath)
    {
        if (!DirAccess.DirExistsAbsolute(ProjectSettings.GlobalizePath(directoryPath)))
        {
            _validationErrors.Add($"SkillContentRegistry could not find {directoryPath}.");
            return;
        }

        DirAccess directory = DirAccess.Open(directoryPath);
        if (directory == null)
        {
            _validationErrors.Add($"SkillContentRegistry could not open {directoryPath}.");
            return;
        }

        try
        {
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
                    ScanDirectory(entryPath);
                    continue;
                }
                if (!entryName.EndsWith(".tres") && !entryName.EndsWith(".res"))
                    continue;
                RegisterSkillResource(entryPath);
            }
            directory.ListDirEnd();
        }
        finally
        {
            GodotObjectLifecycle.DisposeGodotObject(directory);
        }
    }

    private void RegisterSkillResource(string resourcePath)
    {
        var resource = GD.Load<Resource>(resourcePath);
        if (resource == null)
        {
            _validationErrors.Add($"Failed to load skill config {resourcePath}.");
            return;
        }
        if (resource is not SkillDef skillDef)
        {
            _validationErrors.Add($"Skill config {resourcePath} is not a SkillDef.");
            return;
        }
        GodotContentOwnership.RegisterBorrowedContent(skillDef, resourcePath);

        NormalizeSkillDef(skillDef);

        if (skillDef.skill_id == "")
        {
            _validationErrors.Add($"Skill config {resourcePath} is missing skill_id.");
            return;
        }
        if (_skill_defs.ContainsKey(skillDef.skill_id))
        {
            _validationErrors.Add($"Duplicate skill_id registered: {skillDef.skill_id}");
            return;
        }

        _skill_defs[skillDef.skill_id] = skillDef;
    }

    private void NormalizeSkillDef(SkillDef skillDef)
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

    private Array<string> CollectValidationErrors()
    {
        var errors = new Array<string>();
        foreach (string skillKey in ProgressionDataUtils.sorted_string_keys(_skill_defs))
        {
            var skillId = new StringName(skillKey);
            var skillDef = GetTyped<SkillDef>(_skill_defs, skillId);
            if (skillDef == null)
                continue;
            AppendSkillValidationErrors(errors, skillId, skillDef);
        }
        return errors;
    }

    private void AppendSkillValidationErrors(
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
        AppendDynamicMaxLevelValidationErrors(errors, skillId, skillDef);
        foreach (int masteryThreshold in skillDef.mastery_curve)
        {
            if (masteryThreshold <= 0)
            {
                errors.Add($"Skill {skillId} has a non-positive mastery threshold.");
                break;
            }
        }

        if (skillDef.SkillTypeKind == SkillTypeKind.Unknown)
            errors.Add($"Skill {skillId} uses unsupported skill_type {skillDef.skill_type}.");
        if (skillDef.SkillTypeKind == SkillTypeKind.Active && skillDef.combat_profile == null)
            errors.Add($"Skill {skillId} is active but missing combat_profile.");
        AppendPracticeSkillValidationErrors(errors, skillId, skillDef);
        AppendAttributeGrowthValidationErrors(errors, skillId, skillDef);
        AppendRawIntRequirementEntryErrors(
            errors,
            skillId,
            skillDef.SkillLevelRequirementEntriesTyped,
            "skill_level_requirements",
            "skill_id"
        );
        AppendRawIntRequirementEntryErrors(
            errors,
            skillId,
            skillDef.AttributeRequirementEntriesTyped,
            "attribute_requirements",
            "attribute_id"
        );
        foreach (
            string error in SkillLevelDescriptionContentRules.CollectValidationErrors(
                skillId,
                skillDef
            )
        )
        {
            errors.Add(error);
        }
        AppendPhantasmalKillLevelDescriptionValidationErrors(errors, skillId, skillDef);
        AppendPhantasmalKillCombatProfileValidationErrors(errors, skillId, skillDef);

        if (skillDef.combat_profile != null)
            AppendCombatProfileValidationErrors(errors, skillId, skillDef.combat_profile, skillDef);
    }

    private void AppendPracticeSkillValidationErrors(
        Array<string> errors,
        StringName skillId,
        SkillDef skillDef
    )
    {
        int trackCount = 0;
        foreach (StringName trackTag in PracticeTrackTags)
        {
            if (skillDef.HasTag(trackTag))
                trackCount++;
        }

        if (trackCount == 0)
        {
            if (skillDef.PracticeTierKind != SkillPracticeTierKind.None)
                errors.Add(
                    $"Skill {skillId} practice_tier requires meditation or cultivation tag."
                );
            return;
        }

        if (trackCount != 1)
            errors.Add($"Skill {skillId} must use exactly one practice track tag.");
        if (skillDef.TagsTyped.Count != 1)
            errors.Add(
                $"Skill {skillId} practice tags must be exclusive; tags must contain only meditation or cultivation."
            );
        if (
            skillDef.PracticeTierKind
            is SkillPracticeTierKind.None
                or SkillPracticeTierKind.Unknown
        )
            errors.Add(
                $"Skill {skillId} practice_tier must be one of basic, intermediate, advanced, ultimate."
            );
    }

    private void AppendDynamicMaxLevelValidationErrors(
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

    private static void AppendRawIntRequirementEntryErrors(
        Array<string> errors,
        StringName skillId,
        IReadOnlyList<SkillDef.IntRequirementEntryData> entries,
        string contextLabel,
        string idLabel
    )
    {
        foreach (SkillDef.IntRequirementEntryData entry in entries ?? System.Array.Empty<SkillDef.IntRequirementEntryData>())
        {
            if (!entry.HasStringLikeKey)
            {
                errors.Add(
                    $"Skill {skillId} has a non-string {idLabel} key {entry.RawKeyLabel} in {contextLabel}."
                );
                continue;
            }
            if (!entry.HasNonEmptyKey)
            {
                errors.Add($"Skill {skillId} has an empty {idLabel} in {contextLabel}.");
                continue;
            }
            if (!entry.HasStrictIntAmount)
            {
                errors.Add(
                    $"Skill {skillId} requires integer value for {entry.RequirementId} in {contextLabel}."
                );
            }
        }
    }

    public void AppendAttributeGrowthValidationErrors(
        Array<string> errors,
        StringName skillId,
        SkillDef skillDef
    )
    {
        if (skillDef.AttributeGrowthProgressEntriesTyped.Count == 0 && skillDef.growth_tier == "")
            return;
        if (!AttributeGrowthContentRules.IsValidGrowthTier(skillDef.growth_tier))
        {
            errors.Add($"Skill {skillId} uses unsupported growth_tier {skillDef.growth_tier}.");
            return;
        }

        int progressTotal = 0;
        foreach (
            SkillDef.AttributeGrowthProgressEntryData entry in skillDef.AttributeGrowthProgressEntriesTyped
        )
        {
            if (!entry.HasStrictStringKey || !entry.HasNonEmptyKey)
            {
                errors.Add(
                    $"Skill {skillId} attribute_growth_progress key {entry.RawKeyLabel} must be a non-empty String."
                );
                continue;
            }
            var attributeId = entry.AttributeId;
            if (!entry.HasStrictIntAmount)
            {
                errors.Add(
                    $"Skill {skillId} attribute_growth_progress for {attributeId} must be a positive int."
                );
                continue;
            }
            int amount = entry.Amount;
            if (!AttributeGrowthContentRules.IsValidAttributeId(attributeId))
                errors.Add(
                    $"Skill {skillId} attribute_growth_progress references invalid attribute {attributeId}."
                );
            if (amount <= 0)
                errors.Add(
                    $"Skill {skillId} attribute_growth_progress for {attributeId} must be a positive int."
                );
            progressTotal += amount;
        }

        int expectedTotal = AttributeGrowthContentRules.GetTierBudget(skillDef.growth_tier);
        if (progressTotal != expectedTotal)
            errors.Add(
                $"Skill {skillId} attribute_growth_progress total must equal {expectedTotal} for growth_tier {skillDef.growth_tier}."
            );
    }

    private void AppendCombatProfileValidationErrors(
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
            !CombatSkillTargetingContentRules.IsValidCombatTargetMode(combatProfile.target_mode)
        )
            errors.Add(
                $"Skill {skillId} combat_profile uses unsupported target_mode {combatProfile.target_mode}; expected one of {CombatSkillTargetingContentRules.ValidCombatTargetModeLabel()}."
            );

        if (combatProfile.target_team_filter == "")
            errors.Add($"Skill {skillId} combat_profile is missing target_team_filter.");
        else if (
            !CombatTargetTeamContentRules.IsValidSkillTargetTeamFilter(
                combatProfile.target_team_filter
            )
        )
            errors.Add(
                $"Skill {skillId} combat_profile uses unsupported target_team_filter {combatProfile.target_team_filter}; expected one of {CombatTargetTeamContentRules.ValidSkillTargetTeamFilterLabel()}."
            );

        if (combatProfile.target_selection_mode == "")
            errors.Add($"Skill {skillId} combat_profile is missing target_selection_mode.");
        else if (
            !CombatSkillTargetingContentRules.IsValidTargetSelectionMode(
                combatProfile.target_selection_mode
            )
        )
            errors.Add(
                $"Skill {skillId} combat_profile uses unsupported target_selection_mode {combatProfile.target_selection_mode}; expected one of {CombatSkillTargetingContentRules.ValidTargetSelectionModeLabel()}."
            );

        if (combatProfile.selection_order_mode == "")
            errors.Add($"Skill {skillId} combat_profile is missing selection_order_mode.");
        else if (combatProfile.SelectionOrderModeKind == BattleTargetSelectionOrderMode.Unknown)
            errors.Add(
                $"Skill {skillId} combat_profile uses unsupported selection_order_mode {combatProfile.selection_order_mode}; expected one of {CombatSkillTargetingContentRules.ValidSelectionOrderModeLabel()}."
            );

        if (!CombatSkillTargetingContentRules.IsValidAreaPattern(combatProfile.area_pattern))
            errors.Add(
                $"Skill {skillId} combat_profile uses unsupported area_pattern {combatProfile.area_pattern}; expected one of {CombatSkillTargetingContentRules.ValidAreaPatternLabel()}."
            );
        if (combatProfile.MasteryTriggerModeKind == CombatSkillMasteryTriggerMode.Unknown)
            errors.Add(
                $"Skill {skillId} combat_profile uses unsupported mastery_trigger_mode {combatProfile.mastery_trigger_mode}."
            );
        if (combatProfile.MasteryAmountModeKind == CombatSkillMasteryAmountMode.Unknown)
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
        if (!IsValidTuValue(combatProfile.cooldown_tu))
            errors.Add(
                $"Skill {skillId} combat_profile cooldown_tu must be 0 or a multiple of {TuGranularity}."
            );
        if (!IsValidTuValue(combatProfile.casting_time_tu))
            errors.Add(
                $"Skill {skillId} combat_profile casting_time_tu must be 0 or a multiple of {TuGranularity}."
            );
        if (combatProfile.casting_maintenance_dc < 0)
            errors.Add($"Skill {skillId} combat_profile casting_maintenance_dc must be >= 0.");
        if (combatProfile.casting_spell_control_dc < 0)
            errors.Add($"Skill {skillId} combat_profile casting_spell_control_dc must be >= 0.");
        if (!IsValidPendingCastBindingMode(combatProfile.pending_cast_binding_mode))
            errors.Add(
                $"Skill {skillId} combat_profile pending_cast_binding_mode uses unsupported value {combatProfile.pending_cast_binding_mode}."
            );
        bool hasCastingTime = combatProfile.casting_time_tu > 0;
        if (hasCastingTime)
        {
            AppendCastingTimeCompatibilityErrors(errors, skillId, combatProfile, skillDef);
        }
        AppendTemporalReleaseSkillValidationErrors(errors, skillId, combatProfile);

        AppendSpellFateValidationErrors(errors, skillId, combatProfile);
        AppendStringNameArrayValidationErrors(
            errors,
            skillId,
            "combat_profile.required_weapon_families",
            combatProfile.required_weapon_families
        );
        AppendStringNameArrayValidationErrors(
            errors,
            skillId,
            "combat_profile.excluded_weapon_families",
            combatProfile.excluded_weapon_families
        );
        AppendStringNameArrayValidationErrors(
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
                if (
                    TryReadLevelOverrideInt(
                        errors,
                        skillId,
                        overrideLevelKey,
                        overrideDict,
                        costKey,
                        out int costValue
                    )
                    && costValue < 0
                )
                    errors.Add(
                        $"Skill {skillId} combat_profile level override {overrideLevelKey}.{costKey} must be >= 0."
                    );
            }
            if (
                TryReadLevelOverrideInt(
                    errors,
                    skillId,
                    overrideLevelKey,
                    overrideDict,
                    "cooldown_tu",
                    out int cooldownTu
                )
                && !IsValidTuValue(cooldownTu)
            )
                errors.Add(
                    $"Skill {skillId} combat_profile level override {overrideLevelKey}.cooldown_tu must be 0 or a multiple of {TuGranularity}."
                );
            if (
                TryReadLevelOverrideInt(
                    errors,
                    skillId,
                    overrideLevelKey,
                    overrideDict,
                    "casting_time_tu",
                    out int castingTimeTu
                )
                && !IsValidTuValue(castingTimeTu)
            )
                errors.Add(
                    $"Skill {skillId} combat_profile level override {overrideLevelKey}.casting_time_tu must be 0 or a multiple of {TuGranularity}."
                );
            if (
                TryReadLevelOverrideInt(
                    errors,
                    skillId,
                    overrideLevelKey,
                    overrideDict,
                    "casting_maintenance_dc",
                    out int castingMaintenanceDc
                )
                && castingMaintenanceDc < 0
            )
                errors.Add(
                    $"Skill {skillId} combat_profile level override {overrideLevelKey}.casting_maintenance_dc must be >= 0."
                );
            if (
                TryReadLevelOverrideInt(
                    errors,
                    skillId,
                    overrideLevelKey,
                    overrideDict,
                    "casting_spell_control_dc",
                    out int castingSpellControlDc
                )
                && castingSpellControlDc < 0
            )
                errors.Add(
                    $"Skill {skillId} combat_profile level override {overrideLevelKey}.casting_spell_control_dc must be >= 0."
                );
            if (overrideDict.ContainsKey("pending_cast_binding_mode"))
            {
                var overrideBindingMode = ProgressionDataUtils.to_string_name(
                    overrideDict["pending_cast_binding_mode"]
                );
                if (!IsValidPendingCastBindingMode(overrideBindingMode))
                    errors.Add(
                        $"Skill {skillId} combat_profile level override {overrideLevelKey}.pending_cast_binding_mode uses unsupported value {overrideBindingMode}."
                    );
            }
            TryReadLevelOverrideInt(
                errors,
                skillId,
                overrideLevelKey,
                overrideDict,
                "attack_roll_bonus",
                out _
            );
            if (
                TryReadLevelOverrideInt(
                    errors,
                    skillId,
                    overrideLevelKey,
                    overrideDict,
                    "area_value",
                    out int areaValue
                )
                && areaValue < 0
            )
                errors.Add(
                    $"Skill {skillId} combat_profile level override {overrideLevelKey}.area_value must be >= 0."
                );
            if (
                TryReadLevelOverrideInt(
                    errors,
                    skillId,
                    overrideLevelKey,
                    overrideDict,
                    "range_value",
                    out int rangeValue
                )
                && rangeValue < 0
            )
                errors.Add(
                    $"Skill {skillId} combat_profile level override {overrideLevelKey}.range_value must be >= 0."
                );
            if (overrideDict.ContainsKey("area_pattern"))
            {
                var overrideAreaPattern = ProgressionDataUtils.to_string_name(
                    overrideDict["area_pattern"]
                );
                if (!CombatSkillTargetingContentRules.IsValidAreaPattern(overrideAreaPattern))
                    errors.Add(
                        $"Skill {skillId} combat_profile level override {overrideLevelKey}.area_pattern uses unsupported area_pattern {overrideAreaPattern}; expected one of {CombatSkillTargetingContentRules.ValidAreaPatternLabel()}."
                    );
            }
            if (
                TryReadLevelOverrideInt(
                    errors,
                    skillId,
                    overrideLevelKey,
                    overrideDict,
                    "max_target_count",
                    out int maxTargetCount
                )
                && maxTargetCount < 1
            )
                errors.Add(
                    $"Skill {skillId} combat_profile level override {overrideLevelKey}.max_target_count must be >= 1."
                );
            if (castingTimeTu > 0)
            {
                AppendCastingTimeCompatibilityErrors(
                    errors,
                    skillId,
                    combatProfile,
                    skillDef,
                    $"combat_profile level override {overrideLevelKey}"
                );
            }
        }

        if (combatProfile.min_target_count <= 0)
            errors.Add($"Skill {skillId} combat_profile min_target_count must be >= 1.");
        if (combatProfile.max_target_count < combatProfile.min_target_count)
            errors.Add(
                $"Skill {skillId} combat_profile max_target_count must be >= min_target_count."
            );
        AppendExecuteCombatProfileValidationErrors(errors, skillId, skillDef, combatProfile);

        for (int effectIndex = 0; effectIndex < combatProfile.effect_defs.Count; effectIndex++)
            AppendEffectValidationErrors(
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
                if (
                    passiveEffect != null
                    && passiveEffect.EffectKind == BattleEffectKind.Execute
                )
                {
                    errors.Add(
                        $"Skill {skillId} passive_effect_defs[{passiveIndex}] uses effect_type 'execute', which is not allowed in passive effects."
                    );
                    continue;
                }
                AppendEffectValidationErrors(
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
                castVariant.TargetModeKind == BattleTargetMode.Unknown
            )
                errors.Add(
                    $"Skill {skillId} cast option {castVariant.variant_id} uses unsupported target_mode {castVariant.target_mode}; expected one of {CombatSkillTargetingContentRules.ValidCastVariantTargetModeLabel()}."
                );

            if (
                !CombatSkillTargetingContentRules.IsValidFootprintPattern(
                    castVariant.footprint_pattern
                )
            )
                errors.Add(
                    $"Skill {skillId} cast option {castVariant.variant_id} uses unsupported footprint_pattern {castVariant.footprint_pattern}; expected one of {CombatSkillTargetingContentRules.ValidFootprintPatternLabel()}."
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
                AppendEffectValidationErrors(
                    errors,
                    skillId,
                    castVariant.effect_defs[effectIndex],
                    $"combat_profile.cast_variants[{optionIndex}].effect_defs[{effectIndex}]"
                );
        }
    }

    private void AppendSpellFateValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatSkillDef combatProfile
    )
    {
        if (combatProfile == null)
            return;
        if (combatProfile.SpellFateModeKind == CombatSpellFateMode.Unknown)
            errors.Add(
                $"Skill {skillId} combat_profile uses unsupported spell_fate_mode {combatProfile.spell_fate_mode}."
            );
        if (combatProfile.SpellCriticalModeKind == CombatSpellCriticalMode.Unknown)
            errors.Add(
                $"Skill {skillId} combat_profile uses unsupported spell_critical_mode {combatProfile.spell_critical_mode}."
            );
        if (combatProfile.BacklashModeKind == CombatSkillBacklashMode.Unknown)
            errors.Add(
                $"Skill {skillId} combat_profile uses unsupported backlash_mode {combatProfile.backlash_mode}."
            );
        if (
            combatProfile.SpellCriticalModeKind != CombatSpellCriticalMode.None
            && combatProfile.SpellFateModeKind == CombatSpellFateMode.None
        )
            errors.Add(
                $"Skill {skillId} combat_profile spell_critical_mode requires spell_fate_mode."
            );
        if (
            combatProfile.BacklashModeKind != CombatSkillBacklashMode.None
            && combatProfile.SpellFateModeKind == CombatSpellFateMode.None
        )
            errors.Add($"Skill {skillId} combat_profile backlash_mode requires spell_fate_mode.");
        if (combatProfile.AreaOriginModeKind == CombatAreaOriginMode.Unknown)
            errors.Add(
                $"Skill {skillId} combat_profile uses unsupported area_origin_mode {combatProfile.area_origin_mode}."
            );
        if (combatProfile.AreaDirectionModeKind == CombatAreaDirectionMode.Unknown)
            errors.Add(
                $"Skill {skillId} combat_profile uses unsupported area_direction_mode {combatProfile.area_direction_mode}."
            );
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
        if (combatProfile.BacklashModeKind == CombatSkillBacklashMode.GroundAnchorDrift)
        {
            if (combatProfile.TargetModeKind != BattleTargetMode.Ground)
                errors.Add(
                    $"Skill {skillId} combat_profile ground_anchor_drift requires target_mode ground."
                );
            if (combatProfile.backlash_offset_radius <= 0)
                errors.Add(
                    $"Skill {skillId} combat_profile ground_anchor_drift requires backlash_offset_radius >= 1."
                );
        }
    }

    public void AppendEffectValidationErrors(
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
        BattleEffectKind effectKind = effectDef.EffectKind;
        if (effectKind == BattleEffectKind.Unknown)
            errors.Add(
                $"Skill {skillId} effect {contextLabel} uses unsupported effect_type {effectDef.effect_type}."
            );
        if (effectDef.min_skill_level < 0)
            errors.Add($"Skill {skillId} effect {contextLabel} min_skill_level must be >= 0.");
        if (effectDef.max_skill_level >= 0 && effectDef.max_skill_level < effectDef.min_skill_level)
            errors.Add(
                $"Skill {skillId} effect {contextLabel} max_skill_level must be >= min_skill_level or -1."
            );
        if (effectDef.TriggerEventKind == CombatEffectTriggerEvent.Unknown)
            errors.Add(
                $"Skill {skillId} effect {contextLabel} uses unsupported trigger_event {effectDef.trigger_event}."
            );
        if (effectDef.TriggerConditionKind == CombatEffectTriggerCondition.Unknown)
            errors.Add(
                $"Skill {skillId} effect {contextLabel} uses unsupported trigger_condition {effectDef.trigger_condition}."
            );
        if (
            !CombatTargetTeamContentRules.IsValidEffectTargetTeamFilter(
                effectDef.effect_target_team_filter
            )
        )
            errors.Add(
                $"Skill {skillId} effect {contextLabel} uses unsupported effect_target_team_filter {effectDef.effect_target_team_filter}; expected one of {CombatTargetTeamContentRules.ValidEffectTargetTeamFilterLabel()}."
            );
        if (!IsValidTuValue(effectDef.duration_tu))
            errors.Add(
                $"Skill {skillId} effect {contextLabel} duration_tu must be 0 or a multiple of {TuGranularity}."
            );
        if (!IsValidTuValue(effectDef.tick_interval_tu))
            errors.Add(
                $"Skill {skillId} effect {contextLabel} tick_interval_tu must be 0 or a multiple of {TuGranularity}."
            );

        AppendSaveValidationErrors(errors, skillId, effectDef, contextLabel);

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
                $"Skill {skillId} effect {contextLabel} params.duration is unsupported; use CombatEffectDef.duration_tu."
            );
        if (parameters.ContainsKey("effect_tags"))
            errors.Add(
                $"Skill {skillId} effect {contextLabel} params.effect_tags is unsupported; use CombatEffectDef.effect_tags."
            );
        if (parameters.ContainsKey("status_tags"))
            errors.Add(
                $"Skill {skillId} effect {contextLabel} params.status_tags is unsupported; status tags are projected from CombatEffectDef.effect_tags."
            );
        AppendStringNameArrayValidationErrors(
            errors,
            skillId,
            $"effect {contextLabel} effect_tags",
            effectDef.effect_tags
        );
        AppendSaveBonusByTagValidationErrors(errors, skillId, effectDef, contextLabel);
        AppendTemporalStatusEffectValidationErrors(errors, skillId, effectDef, contextLabel);
        AppendTypedEffectParamValidationErrors(errors, skillId, effectDef, contextLabel);
        AppendAttributeScaledDiceValidationErrors(errors, skillId, effectDef, contextLabel);

        if (effectKind == BattleEffectKind.Damage)
        {
            AppendDamageEffectValidationErrors(errors, skillId, effectDef, contextLabel);
        }
        else if (
            effectKind == BattleEffectKind.Status
            || effectKind == BattleEffectKind.ApplyStatus
        )
        {
            if (effectDef.status_id == "")
                errors.Add(
                    $"Skill {skillId} status effect in {contextLabel} is missing status_id."
                );
            if (effectDef.terrain_effect_id == "" && parameters.ContainsKey("duration_tu"))
                errors.Add(
                    $"Skill {skillId} effect {contextLabel} params.duration_tu is unsupported; use CombatEffectDef.duration_tu."
                );
            if (effectDef.terrain_effect_id == "" && parameters.ContainsKey("tick_interval_tu"))
                errors.Add(
                    $"Skill {skillId} effect {contextLabel} params.tick_interval_tu is unsupported; use CombatEffectDef.tick_interval_tu."
                );
            if (parameters.ContainsKey("range_bonus"))
                errors.Add(
                    $"Skill {skillId} status effect in {contextLabel} params.range_bonus is unsupported; use CombatEffectDef.range_bonus."
                );
            AppendStatusDamageFilterValidationErrors(
                errors,
                skillId,
                effectDef,
                contextLabel
            );
        }
        else if (effectKind == BattleEffectKind.Shield)
        {
            bool hasFixedDiceKeys = _has_fixed_dice_fields(effectDef);
            bool hasValidFixedDiceConfig = _has_valid_fixed_dice_config(effectDef);
            bool hasValidDynamicDiceConfig = _has_valid_attribute_scaled_dice_config(effectDef);
            if (effectDef.power <= 0 && !hasValidFixedDiceConfig && !hasValidDynamicDiceConfig)
                errors.Add(
                    $"Skill {skillId} shield effect in {contextLabel} must have power >= 1, a valid dice_count/dice_sides config, or a valid attribute-scaled dice config."
                );
            if (hasFixedDiceKeys && !hasValidFixedDiceConfig)
                errors.Add(
                    $"Skill {skillId} shield effect in {contextLabel} must set dice_count and dice_sides >= 1 together."
                );
            if (
                effectDef.duration_tu <= 0
            )
                errors.Add(
                    $"Skill {skillId} shield effect in {contextLabel} must have positive duration_tu in {TuGranularity} TU steps."
                );
        }
        else if (
            effectKind == BattleEffectKind.Heal
            || effectKind == BattleEffectKind.StaminaRestore
        )
        {
            bool hasFixedDiceKeys = _has_fixed_dice_fields(effectDef);
            if (hasFixedDiceKeys && !_has_valid_fixed_dice_config(effectDef))
                errors.Add(
                    $"Skill {skillId} {effectDef.effect_type} effect in {contextLabel} must set dice_count and dice_sides >= 1 together."
                );
            if (
                effectKind == BattleEffectKind.StaminaRestore
                && effectDef.power <= 0
                && !_has_valid_fixed_dice_config(effectDef)
                && !_has_valid_attribute_scaled_dice_config(effectDef)
            )
                errors.Add(
                    $"Skill {skillId} stamina_restore effect in {contextLabel} must have power >= 1, a valid dice_count/dice_sides config, or a valid attribute-scaled dice config."
                );
        }
        else if (effectKind == BattleEffectKind.TerrainEffect)
        {
            if (effectDef.terrain_effect_id == "")
                errors.Add(
                    $"Skill {skillId} terrain_effect in {contextLabel} is missing terrain_effect_id."
                );
            if (effectDef.LifetimePolicyKind == CombatEffectLifetimePolicy.Unknown)
                errors.Add(
                    $"Skill {skillId} terrain_effect in {contextLabel} lifetime_policy must be battle or timed."
                );
            if (effectDef.move_cost_delta < 0)
                errors.Add(
                    $"Skill {skillId} terrain_effect in {contextLabel} move_cost_delta must be >= 0."
                );
            if (effectDef.overlay_priority < 0)
                errors.Add(
                    $"Skill {skillId} terrain_effect in {contextLabel} overlay_priority must be >= 0."
                );
            if (parameters.ContainsKey("render_overlay_id"))
                errors.Add(
                    $"Skill {skillId} terrain_effect in {contextLabel} params.render_overlay_id is unsupported; use CombatEffectDef.render_overlay_id."
                );
            if (parameters.ContainsKey("overlay_priority"))
                errors.Add(
                    $"Skill {skillId} terrain_effect in {contextLabel} params.overlay_priority is unsupported; use CombatEffectDef.overlay_priority."
                );
            if (parameters.ContainsKey("display_name"))
                errors.Add(
                    $"Skill {skillId} terrain_effect in {contextLabel} params.display_name is unsupported; use CombatEffectDef.display_name."
                );
            if (parameters.ContainsKey("does_not_stack_with_status_id"))
                errors.Add(
                    $"Skill {skillId} terrain_effect in {contextLabel} params.does_not_stack_with_status_id is unsupported; use CombatEffectDef.does_not_stack_with_status_id."
                );
            if (parameters.ContainsKey("does_not_stack_with_status_ids"))
                errors.Add(
                    $"Skill {skillId} terrain_effect in {contextLabel} params.does_not_stack_with_status_ids is unsupported; use CombatEffectDef.does_not_stack_with_status_ids."
                );
            AppendStringNameArrayValidationErrors(
                errors,
                skillId,
                $"terrain_effect in {contextLabel} does_not_stack_with_status_ids",
                effectDef.does_not_stack_with_status_ids
            );
            if (effectDef.duration_tu > 0 && effectDef.tick_interval_tu <= 0)
                errors.Add(
                    $"Skill {skillId} terrain_effect in {contextLabel} must have positive tick_interval_tu in {TuGranularity} TU steps."
                );
            if (effectDef.TerrainTickEffectKind == BattleTerrainEffectRuntimeKind.Status)
            {
                if (effectDef.status_id == "")
                    errors.Add(
                        $"Skill {skillId} terrain_effect in {contextLabel} with tick_effect_type=status is missing status_id."
                    );
                if (parameters.ContainsKey("status_id"))
                    errors.Add(
                        $"Skill {skillId} terrain_effect in {contextLabel} params.status_id is unsupported; use CombatEffectDef.status_id."
                    );
                if (parameters.ContainsKey("duration_tu"))
                    errors.Add(
                        $"Skill {skillId} terrain_effect in {contextLabel} params.duration_tu is unsupported; use CombatEffectDef.applied_status_duration_tu."
                    );
                if (!IsValidTuValue(effectDef.applied_status_duration_tu) || effectDef.applied_status_duration_tu <= 0)
                    errors.Add(
                        $"Skill {skillId} terrain_effect in {contextLabel} with tick_effect_type=status must set positive applied_status_duration_tu in {TuGranularity} TU steps."
                    );
            }
        }
        else if (
            effectKind == BattleEffectKind.Terrain
            || effectKind == BattleEffectKind.TerrainReplace
            || effectKind == BattleEffectKind.TerrainReplaceTo
        )
        {
            if (effectDef.terrain_replace_to == "")
                errors.Add(
                    $"Skill {skillId} terrain_replace effect in {contextLabel} is missing terrain_replace_to."
                );
        }
        else if (
            effectKind == BattleEffectKind.Height
            || effectKind == BattleEffectKind.HeightDelta
        )
        {
            if (effectDef.height_delta == 0)
                errors.Add(
                    $"Skill {skillId} height effect in {contextLabel} must have non-zero height_delta."
                );
        }
        else if (effectKind == BattleEffectKind.BodySizeCategoryOverride)
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
                !BodySizeContentRules.IsValidBodySizeCategory(effectDef.body_size_category)
            )
                errors.Add(
                    $"Skill {skillId} body_size_category_override effect in {contextLabel} uses unsupported body_size_category {effectDef.body_size_category}."
                );
            if (effectDef.duration_tu <= 0)
                errors.Add(
                    $"Skill {skillId} body_size_category_override effect in {contextLabel} must have positive duration_tu."
                );
        }
        else if (effectKind == BattleEffectKind.ForcedMove)
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
            else if (effectDef.ForcedMoveModeKind == BattleForcedMoveMode.Unknown)
                errors.Add(
                    $"Skill {skillId} forced_move effect in {contextLabel} uses unsupported forced_move_mode {effectDef.forced_move_mode}."
                );
            else if (effectDef.ForcedMoveModeKind == BattleForcedMoveMode.Jump)
                AppendJumpEffectValidationErrors(errors, skillId, effectDef, contextLabel);
            else if (effectDef.forced_move_distance <= 0)
                errors.Add(
                    $"Skill {skillId} forced_move effect in {contextLabel} must have forced_move_distance >= 1."
                );
        }
        else if (effectKind == BattleEffectKind.Charge)
        {
            if (
                DictStringName(parameters, "skill_id").ToString().Length == 0
            )
                errors.Add(
                    $"Skill {skillId} charge effect in {contextLabel} is missing params.skill_id."
                );
        }
        else if (effectKind == BattleEffectKind.PathStepAoe)
        {
            AppendPathStepAoeValidationErrors(errors, skillId, effectDef, contextLabel);
        }
        else if (effectKind == BattleEffectKind.EquipmentDurabilityDamage)
        {
            AppendEquipmentDurabilityDamageValidationErrors(
                errors,
                skillId,
                effectDef,
                contextLabel
            );
        }
        else if (effectKind == BattleEffectKind.Execute)
        {
            AppendExecuteEffectValidationErrors(errors, skillId, effectDef, contextLabel);
        }
        else if (effectKind == BattleEffectKind.GradedSaveExecute)
        {
            AppendGradedSaveExecuteValidationErrors(errors, skillId, effectDef, contextLabel);
        }
    }

    private void AppendPhantasmalKillLevelDescriptionValidationErrors(
        Array<string> errors,
        StringName skillId,
        SkillDef skillDef
    )
    {
        if (skillId != "mage_phantasmal_kill" || skillDef == null)
            return;

        var coveredLevels = new HashSet<int>();
        foreach (
            SkillDef.LevelDescriptionConfigEntryData entry in skillDef.LevelDescriptionConfigEntriesTyped
        )
        {
            if (entry.KeyIsStrictString && entry.HasParsedLevelKey && entry.ValueIsDictionary)
                coveredLevels.Add(entry.Level);
        }

        for (int level = 0; level <= 9; level++)
        {
            if (!coveredLevels.Contains(level))
                errors.Add(
                    $"Skill {skillId} level_description_configs must include level {level}."
                );
        }
    }

    private void AppendPhantasmalKillCombatProfileValidationErrors(
        Array<string> errors,
        StringName skillId,
        SkillDef skillDef
    )
    {
        if (skillId != "mage_phantasmal_kill" || skillDef?.combat_profile == null)
            return;

        CombatSkillDef combatProfile = skillDef.combat_profile;
        RequireStringName(
            errors,
            skillId,
            "combat_profile.target_mode",
            combatProfile.target_mode,
            "ground"
        );
        RequireStringName(
            errors,
            skillId,
            "combat_profile.target_team_filter",
            combatProfile.target_team_filter,
            "any"
        );
        RequireStringName(
            errors,
            skillId,
            "combat_profile.target_selection_mode",
            combatProfile.target_selection_mode,
            "single_coord"
        );
        RequireStringName(
            errors,
            skillId,
            "combat_profile.area_pattern",
            combatProfile.area_pattern,
            "square"
        );
        RequireInt(errors, skillId, "combat_profile.area_value", combatProfile.area_value, 3);
        RequireStringName(
            errors,
            skillId,
            "combat_profile.special_resolution_profile_id",
            combatProfile.special_resolution_profile_id,
            ""
        );
    }

    private void AppendExecuteCombatProfileValidationErrors(
        Array<string> errors,
        StringName skillId,
        SkillDef skillDef,
        CombatSkillDef combatProfile
    )
    {
        if (combatProfile == null)
            return;

        bool hasExecute = ValidateExecuteEffectSet(
            errors,
            skillId,
            combatProfile.effect_defs,
            null,
            "combat_profile.effect_defs"
        );
        for (int optionIndex = 0; optionIndex < combatProfile.cast_variants.Count; optionIndex++)
        {
            CombatCastVariantDef castVariant = combatProfile.cast_variants[optionIndex];
            hasExecute |= ValidateExecuteEffectSet(
                errors,
                skillId,
                combatProfile.effect_defs,
                castVariant,
                $"combat_profile.cast_variants[{optionIndex}] merged effect_defs"
            );
        }
        if (!hasExecute)
            return;

        if (combatProfile.special_resolution_profile_id != "")
        {
            errors.Add(
                $"Skill {skillId} combat_profile.special_resolution_profile_id must be empty when execute is present."
            );
        }
        RequireStringName(errors, skillId, "combat_profile.target_mode", combatProfile.target_mode, "unit");
        RequireStringName(
            errors,
            skillId,
            "combat_profile.target_team_filter",
            combatProfile.target_team_filter,
            "enemy"
        );
        RequireStringName(
            errors,
            skillId,
            "combat_profile.target_selection_mode",
            combatProfile.target_selection_mode,
            "single_unit"
        );
        RequireInt(errors, skillId, "combat_profile.min_target_count", combatProfile.min_target_count, 1);
        RequireInt(errors, skillId, "combat_profile.max_target_count", combatProfile.max_target_count, 1);
        RequireBool(
            errors,
            skillId,
            "combat_profile.allow_repeat_target",
            combatProfile.allow_repeat_target,
            false
        );
        RequireStringName(errors, skillId, "combat_profile.area_pattern", combatProfile.area_pattern, "single");
        RequireInt(errors, skillId, "combat_profile.area_value", combatProfile.area_value, 0);
    }

    private bool ValidateExecuteEffectSet(
        Array<string> errors,
        StringName skillId,
        Array<CombatEffectDef> baseEffects,
        CombatCastVariantDef castVariant,
        string contextLabel
    )
    {
        var mergedEffects = new List<CombatEffectDef>();
        if (baseEffects != null)
        {
            foreach (CombatEffectDef effectDef in baseEffects)
            {
                mergedEffects.Add(effectDef);
            }
        }
        if (castVariant?.effect_defs != null)
        {
            foreach (CombatEffectDef effectDef in castVariant.effect_defs)
            {
                mergedEffects.Add(effectDef);
            }
        }

        bool hasExecute = false;
        foreach (CombatEffectDef effectDef in mergedEffects)
        {
            if (effectDef?.EffectKind == BattleEffectKind.Execute)
            {
                hasExecute = true;
                break;
            }
        }
        if (!hasExecute)
            return false;

        if (mergedEffects.Count != 1 || mergedEffects[0]?.EffectKind != BattleEffectKind.Execute)
        {
            errors.Add(
                $"Skill {skillId} {contextLabel} containing execute must contain exactly one execute effect and no sibling effects."
            );
        }
        return true;
    }

    private void AppendExecuteEffectValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatEffectDef effectDef,
        string contextLabel
    )
    {
        RequireStringName(
            errors,
            skillId,
            $"{contextLabel}.effect_target_team_filter",
            effectDef.effect_target_team_filter,
            "enemy"
        );
        RequireStringName(
            errors,
            skillId,
            $"{contextLabel}.save_dc_mode",
            effectDef.save_dc_mode,
            BattleSaveContentRules.ToStringName(BattleSaveDcMode.CasterSpell)
        );
        RequireInt(errors, skillId, $"{contextLabel}.save_dc", effectDef.save_dc, 0);
        RequireStringName(
            errors,
            skillId,
            $"{contextLabel}.save_dc_source_ability",
            effectDef.save_dc_source_ability,
            UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Intelligence)
        );
        RequireStringName(
            errors,
            skillId,
            $"{contextLabel}.save_ability",
            effectDef.save_ability,
            UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Willpower)
        );
        RequireStringName(
            errors,
            skillId,
            $"{contextLabel}.save_tag",
            effectDef.save_tag,
            BattleSaveContentRules.ToStringName(BattleSaveTagKind.Execute)
        );
        RequireStringName(errors, skillId, $"{contextLabel}.damage_tag", effectDef.damage_tag, "negative_energy");
        RequireBool(
            errors,
            skillId,
            $"{contextLabel}.save_partial_on_success",
            effectDef.save_partial_on_success,
            false
        );
        RequireStringName(errors, skillId, $"{contextLabel}.trigger_event", effectDef.trigger_event, "");
        RequireStringName(
            errors,
            skillId,
            $"{contextLabel}.trigger_condition",
            effectDef.trigger_condition,
            ""
        );
        RequireRange(
            errors,
            skillId,
            $"{contextLabel}.threshold_max_hp_ratio_percent",
            effectDef.threshold_max_hp_ratio_percent,
            0,
            100
        );
        RequireRange(
            errors,
            skillId,
            $"{contextLabel}.threshold_cap_max_hp_ratio_percent",
            effectDef.threshold_cap_max_hp_ratio_percent,
            0,
            100
        );
        if (effectDef.threshold_cap_max_hp_ratio_percent < effectDef.threshold_max_hp_ratio_percent)
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel}.threshold_cap_max_hp_ratio_percent must be >= threshold_max_hp_ratio_percent."
            );
        }
        RequireRange(
            errors,
            skillId,
            $"{contextLabel}.heal_multiplier_percent",
            effectDef.heal_multiplier_percent,
            0,
            100
        );
        RequireRange(
            errors,
            skillId,
            $"{contextLabel}.shield_gain_multiplier_percent",
            effectDef.shield_gain_multiplier_percent,
            0,
            100
        );
        if (effectDef.soul_fracture_duration_tu <= 0 || !IsValidTuValue(effectDef.soul_fracture_duration_tu))
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel}.soul_fracture_duration_tu must be > 0 and divisible by {TuGranularity}."
            );
        }
        if (effectDef.@params != null && effectDef.@params.Count > 0)
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel} execute must not use params payload."
            );
        }
    }

    private void AppendGradedSaveExecuteValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatEffectDef effectDef,
        string contextLabel
    )
    {
        RequireStringName(
            errors,
            skillId,
            $"{contextLabel}.effect_target_team_filter",
            effectDef.effect_target_team_filter,
            "any"
        );
        RequireStringName(
            errors,
            skillId,
            $"{contextLabel}.damage_tag",
            effectDef.damage_tag,
            "psychic"
        );
        RequireStringName(
            errors,
            skillId,
            $"{contextLabel}.save_dc_mode",
            effectDef.save_dc_mode,
            BattleSaveContentRules.ToStringName(BattleSaveDcMode.CasterSpell)
        );
        RequireInt(errors, skillId, $"{contextLabel}.save_dc", effectDef.save_dc, 0);
        RequireStringName(
            errors,
            skillId,
            $"{contextLabel}.save_dc_source_ability",
            effectDef.save_dc_source_ability,
            UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Intelligence)
        );
        RequireStringName(
            errors,
            skillId,
            $"{contextLabel}.save_ability",
            effectDef.save_ability,
            UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Willpower)
        );
        RequireStringName(
            errors,
            skillId,
            $"{contextLabel}.save_tag",
            effectDef.save_tag,
            BattleSaveContentRules.ToStringName(BattleSaveTagKind.Illusion)
        );
        RequireBool(
            errors,
            skillId,
            $"{contextLabel}.save_partial_on_success",
            effectDef.save_partial_on_success,
            false
        );

        Dictionary parameters = effectDef.@params ?? new Dictionary();
        AppendGradedSaveExecuteParamKeyValidationErrors(
            errors,
            skillId,
            parameters,
            contextLabel
        );
        RequireStringNameParam(
            errors,
            skillId,
            parameters,
            contextLabel,
            "profile_id",
            "phantasmal_kill"
        );
        RequireNonNegativeIntParam(
            errors,
            skillId,
            parameters,
            contextLabel,
            "failure_execute_threshold_fixed"
        );
        RequireIntRangeParam(
            errors,
            skillId,
            parameters,
            contextLabel,
            "failure_execute_threshold_max_hp_percent",
            1,
            100
        );
        RequirePositiveIntParam(
            errors,
            skillId,
            parameters,
            contextLabel,
            "failure_damage_dice_count"
        );
        RequirePositiveIntParam(
            errors,
            skillId,
            parameters,
            contextLabel,
            "failure_damage_dice_sides"
        );
        RequirePositiveTuParam(
            errors,
            skillId,
            parameters,
            contextLabel,
            "failure_frightened_duration_tu"
        );
        RequirePositiveTuParam(
            errors,
            skillId,
            parameters,
            contextLabel,
            "failure_reaction_lock_duration_tu"
        );
        RequireIntRangeParam(
            errors,
            skillId,
            parameters,
            contextLabel,
            "critical_failure_execute_threshold_max_hp_percent",
            1,
            100
        );
        RequirePositiveIntParam(
            errors,
            skillId,
            parameters,
            contextLabel,
            "critical_failure_damage_dice_count"
        );
        RequirePositiveIntParam(
            errors,
            skillId,
            parameters,
            contextLabel,
            "critical_failure_damage_dice_sides"
        );
        RequirePositiveTuParam(
            errors,
            skillId,
            parameters,
            contextLabel,
            "critical_failure_frightened_duration_tu"
        );
        RequirePositiveTuParam(
            errors,
            skillId,
            parameters,
            contextLabel,
            "critical_failure_stunned_duration_tu"
        );
        RequirePositiveTuParam(
            errors,
            skillId,
            parameters,
            contextLabel,
            "success_aftershock_duration_tu"
        );
    }

    private void AppendDamageEffectValidationErrors(
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
            else if (DamageTagContentRules.ToDamageTagKind(damageTag) == DamageTagKind.Unknown)
                errors.Add(
                    $"Skill {skillId} damage effect in {contextLabel} uses unsupported damage_tag {damageTag}; expected one of {DamageTagContentRules.ValidDamageTagLabel()}."
                );
        }

        if (effectDef.hp_ratio_threshold_percent < 0 || effectDef.hp_ratio_threshold_percent > 100)
            errors.Add(
                $"Skill {skillId} damage effect in {contextLabel} hp_ratio_threshold_percent must be 0 or from 1 to 100."
            );

        bool hasBonusDamageDice =
            effectDef.bonus_damage_dice_count > 0
            || effectDef.bonus_damage_dice_sides > 0
            || effectDef.bonus_damage_dice_bonus != 0;
        if (!hasBonusDamageDice)
            return;
        if (effectDef.bonus_condition == "")
            errors.Add(
                $"Skill {skillId} damage effect in {contextLabel} bonus_damage_dice requires bonus_condition."
            );
        if (effectDef.bonus_damage_dice_count < 1)
            errors.Add(
                $"Skill {skillId} damage effect in {contextLabel} bonus_damage_dice_count must be positive."
            );
        if (effectDef.bonus_damage_dice_sides < 1)
            errors.Add(
                $"Skill {skillId} damage effect in {contextLabel} bonus_damage_dice_sides must be positive."
            );
    }

    private void AppendStatusDamageFilterValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatEffectDef effectDef,
        string contextLabel
    )
    {
        if (effectDef == null)
            return;
        Dictionary parameters = effectDef.@params ?? new Dictionary();
        if (parameters.ContainsKey("damage_tag"))
        {
            errors.Add(
                $"Skill {skillId} status effect in {contextLabel} params.damage_tag is unsupported; use CombatEffectDef.damage_tag."
            );
        }
        if (parameters.ContainsKey("damage_tags"))
        {
            errors.Add(
                $"Skill {skillId} status effect in {contextLabel} params.damage_tags is unsupported; use CombatEffectDef.damage_tags."
            );
        }
        if (parameters.ContainsKey("damage_category"))
        {
            errors.Add(
                $"Skill {skillId} status effect in {contextLabel} params.damage_category is unsupported; use CombatEffectDef.damage_category."
            );
        }
        if (
            effectDef.damage_tag != ""
            && DamageTagContentRules.ToDamageTagKind(effectDef.damage_tag)
                == DamageTagKind.Unknown
        )
        {
            errors.Add(
                $"Skill {skillId} status effect in {contextLabel} damage_tag must be one of {DamageTagContentRules.ValidDamageTagLabel()}."
            );
        }
        for (int index = 0; index < effectDef.damage_tags.Count; index++)
        {
            StringName damageTag = ProgressionDataUtils.to_string_name(effectDef.damage_tags[index]);
            if (
                damageTag == ""
                || DamageTagContentRules.ToDamageTagKind(damageTag) == DamageTagKind.Unknown
            )
            {
                errors.Add(
                    $"Skill {skillId} status effect in {contextLabel} damage_tags[{index}] must be one of {DamageTagContentRules.ValidDamageTagLabel()}."
                );
            }
        }
        if (
            effectDef.damage_category != ""
            && DamageTagContentRules.ToDamageCategoryKind(effectDef.damage_category)
                == DamageCategoryKind.Unknown
        )
        {
            errors.Add(
                $"Skill {skillId} status effect in {contextLabel} damage_category must be one of {DamageTagContentRules.ValidDamageCategoryLabel()}."
            );
        }
        if (
            effectDef.mitigation_tier != ""
            && DamageTagContentRules.ToMitigationTierKind(effectDef.mitigation_tier)
                == DamageMitigationTierKind.Unknown
        )
        {
            errors.Add(
                $"Skill {skillId} status effect in {contextLabel} mitigation_tier must be one of {DamageTagContentRules.ValidMitigationTierLabel()}."
            );
        }
    }

    private void AppendEquipmentDurabilityDamageValidationErrors(
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
        bool hasDynamicSave = effectDef.SaveDcModeKind == BattleSaveDcMode.CasterSpell;
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
        if (parameters.ContainsKey("slot_weight_map"))
        {
            errors.Add(
                $"Skill {skillId} equipment_durability_damage effect in {contextLabel} params.slot_weight_map is unsupported; use equipment_durability_slot_weights."
            );
        }
        _append_equipment_slot_weight_validation_errors(
            errors,
            skillId,
            contextLabel,
            effectDef.equipment_durability_slot_weights
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
            if (!EquipmentRules.IsValidSlot(slotId))
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
        Godot.Collections.Array<CombatEffectSlotWeightDef> slotWeights
    )
    {
        if (slotWeights == null || slotWeights.Count == 0)
            return;
        var seenSlots = new HashSet<StringName>();
        for (int index = 0; index < slotWeights.Count; index++)
        {
            CombatEffectSlotWeightDef slotWeight = slotWeights[index];
            if (slotWeight == null)
            {
                errors.Add(
                    $"Skill {skillId} equipment_durability_damage effect in {contextLabel} equipment_durability_slot_weights[{index}] must be set."
                );
                continue;
            }
            var slotId = ProgressionDataUtils.to_string_name(slotWeight.slot_id);
            if (!EquipmentRules.IsValidSlot(slotId))
            {
                errors.Add(
                    $"Skill {skillId} equipment_durability_damage effect in {contextLabel} equipment_durability_slot_weights uses unsupported slot {slotId}."
                );
            }
            else if (!seenSlots.Add(slotId))
            {
                errors.Add(
                    $"Skill {skillId} equipment_durability_damage effect in {contextLabel} equipment_durability_slot_weights repeats slot {slotId}."
                );
            }
            if (slotWeight.weight <= 0)
            {
                errors.Add(
                    $"Skill {skillId} equipment_durability_damage effect in {contextLabel} equipment_durability_slot_weights[{slotId}] must be a positive int."
                );
            }
        }
    }

    private void AppendSaveValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatEffectDef effectDef,
        string contextLabel
    )
    {
        if (effectDef == null)
            return;
        int saveDc = effectDef.save_dc;
        BattleSaveDcMode saveDcMode = effectDef.SaveDcModeKind;
        bool dynamicSaveDc = saveDcMode == BattleSaveDcMode.CasterSpell;
        bool hasSaveDc = saveDc > 0 || dynamicSaveDc;
        var saveAbility = ProgressionDataUtils.to_string_name(effectDef.save_ability);
        var saveDcSourceAbility = ProgressionDataUtils.to_string_name(
            effectDef.save_dc_source_ability
        );
        var saveTag = ProgressionDataUtils.to_string_name(effectDef.save_tag);
        if (saveDcMode == BattleSaveDcMode.Unknown)
            errors.Add(
                $"Skill {skillId} effect {contextLabel} uses unsupported save_dc_mode {effectDef.save_dc_mode}."
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
        if (dynamicSaveDc && !BattleSaveContentRules.IsValidSaveAbility(saveDcSourceAbility))
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

        if (!BattleSaveContentRules.IsValidSaveAbility(saveAbility))
            errors.Add(
                $"Skill {skillId} effect {contextLabel} uses unsupported save_ability {saveAbility}."
            );
        if (!BattleSaveContentRules.IsValidSaveTag(saveTag))
            errors.Add(
                $"Skill {skillId} effect {contextLabel} uses unsupported save_tag {saveTag}."
            );
        BattleEffectKind effectKind = effectDef.EffectKind;
        if (effectDef.save_partial_on_success && effectKind != BattleEffectKind.Damage)
            errors.Add(
                $"Skill {skillId} effect {contextLabel} save_partial_on_success is only supported on damage effects."
            );
        if (
            effectDef.save_failure_status_id != ""
            && effectKind != BattleEffectKind.Status
            && effectKind != BattleEffectKind.ApplyStatus
        )
            errors.Add(
                $"Skill {skillId} effect {contextLabel} save_failure_status_id is only supported on status effects."
            );
    }

    private void AppendPathStepAoeValidationErrors(
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
        if (!HasRepeatHitStatusConfig(parameters))
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
            if (durationTu <= 0 || !IsValidTuValue(durationTu))
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

    private bool HasRepeatHitStatusConfig(Dictionary parameters)
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

    private void AppendJumpEffectValidationErrors(
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
        if (effectDef.jump_arc_ratio < CombatEffectContentRules.MinJumpArcRatio)
            errors.Add(
                $"Skill {skillId} jump effect in {contextLabel} requires jump_arc_ratio >= {CombatEffectContentRules.MinJumpArcRatio:0.00}; jump must lift the unit."
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

    private void AppendTypedEffectParamValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatEffectDef effectDef,
        string contextLabel
    )
    {
        Dictionary parameters = effectDef.@params ?? new Dictionary();
        foreach (var migratedParam in TypedEffectParamTargets)
        {
            if (parameters.ContainsKey(migratedParam.Key))
                errors.Add(
                    $"Skill {skillId} effect {contextLabel} params.{migratedParam.Key} is unsupported; use CombatEffectDef.{migratedParam.Value}."
                );
        }
    }

    private void AppendAttributeScaledDiceValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatEffectDef effectDef,
        string contextLabel
    )
    {
        if (effectDef == null || !_has_attribute_scaled_dice_fields(effectDef))
            return;
        if (effectDef.dice_count < 1)
            errors.Add(
                $"Skill {skillId} effect {contextLabel} attribute-scaled dice must set dice_count >= 1."
            );
        if (effectDef.dice_sides_base < 1)
            errors.Add(
                $"Skill {skillId} effect {contextLabel} attribute-scaled dice must set dice_sides_base >= 1."
            );
        if (effectDef.dice_sides_per_constitution_mod < 0)
            errors.Add(
                $"Skill {skillId} effect {contextLabel} dice_sides_per_constitution_mod must be >= 0."
            );
        if (effectDef.dice_sides_per_willpower_mod < 0)
            errors.Add(
                $"Skill {skillId} effect {contextLabel} dice_sides_per_willpower_mod must be >= 0."
            );
        if (effectDef.dice_sides > 0)
            errors.Add(
                $"Skill {skillId} effect {contextLabel} cannot combine dice_sides with attribute-scaled dice_sides_base."
            );
        if (
            effectDef.power > 0
            && (
                effectDef.EffectKind == BattleEffectKind.Heal
                || effectDef.EffectKind == BattleEffectKind.Shield
                || effectDef.EffectKind == BattleEffectKind.StaminaRestore
            )
        )
            errors.Add(
                $"Skill {skillId} effect {contextLabel} uses attribute-scaled dice; put the dice count in dice_count, not power."
            );
    }

    private void AppendStringNameArrayValidationErrors(
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

    private bool IsValidTuValue(int value)
    {
        if (value < 0)
            return false;
        if (value == 0)
            return true;
        return value % TuGranularity == 0;
    }

    private static void RequireStringName(
        Array<string> errors,
        StringName skillId,
        string fieldLabel,
        StringName actual,
        StringName expected
    )
    {
        if (actual != expected)
        {
            errors.Add($"Skill {skillId} {fieldLabel} must be {expected}.");
        }
    }

    private static void RequireInt(
        Array<string> errors,
        StringName skillId,
        string fieldLabel,
        int actual,
        int expected
    )
    {
        if (actual != expected)
        {
            errors.Add($"Skill {skillId} {fieldLabel} must be {expected}.");
        }
    }

    private static void RequireBool(
        Array<string> errors,
        StringName skillId,
        string fieldLabel,
        bool actual,
        bool expected
    )
    {
        if (actual != expected)
        {
            errors.Add($"Skill {skillId} {fieldLabel} must be {expected.ToString().ToLowerInvariant()}.");
        }
    }

    private static void RequireRange(
        Array<string> errors,
        StringName skillId,
        string fieldLabel,
        int actual,
        int minimum,
        int maximum
    )
    {
        if (actual < minimum || actual > maximum)
        {
            errors.Add($"Skill {skillId} {fieldLabel} must be between {minimum} and {maximum}.");
        }
    }

    private static void AppendGradedSaveExecuteParamKeyValidationErrors(
        Array<string> errors,
        StringName skillId,
        Dictionary parameters,
        string contextLabel
    )
    {
        parameters ??= new Dictionary();
        foreach (Variant rawKey in parameters.Keys)
        {
            string keyLabel = ParameterKeyLabel(rawKey);
            if (!GradedSaveExecuteParamKeySet.Contains(keyLabel))
            {
                errors.Add(
                    $"Skill {skillId} effect {contextLabel} params.{keyLabel} is unsupported; expected only {GradedSaveExecuteParamKeyLabel}."
                );
            }
        }

        foreach (string requiredKey in GradedSaveExecuteParamKeys)
        {
            if (!parameters.ContainsKey(requiredKey))
                errors.Add(
                    $"Skill {skillId} effect {contextLabel} params.{requiredKey} is required."
                );
        }
    }

    private static void RequireStringNameParam(
        Array<string> errors,
        StringName skillId,
        Dictionary parameters,
        string contextLabel,
        string paramName,
        StringName expected
    )
    {
        if (!TryGetParameter(parameters, paramName, out object rawValue))
            return;
        StringName actual = ProgressionDataUtils.to_string_name(rawValue);
        if (actual != expected)
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel} params.{paramName} must be {expected}."
            );
        }
    }

    private static void RequirePositiveIntParam(
        Array<string> errors,
        StringName skillId,
        Dictionary parameters,
        string contextLabel,
        string paramName
    )
    {
        if (
            TryReadStrictIntParam(errors, skillId, parameters, contextLabel, paramName, out int value)
            && value <= 0
        )
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel} params.{paramName} must be a positive int."
            );
        }
    }

    private static void RequireNonNegativeIntParam(
        Array<string> errors,
        StringName skillId,
        Dictionary parameters,
        string contextLabel,
        string paramName
    )
    {
        if (
            TryReadStrictIntParam(errors, skillId, parameters, contextLabel, paramName, out int value)
            && value < 0
        )
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel} params.{paramName} must be >= 0."
            );
        }
    }

    private static void RequireIntRangeParam(
        Array<string> errors,
        StringName skillId,
        Dictionary parameters,
        string contextLabel,
        string paramName,
        int minimum,
        int maximum
    )
    {
        if (
            TryReadStrictIntParam(errors, skillId, parameters, contextLabel, paramName, out int value)
            && (value < minimum || value > maximum)
        )
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel} params.{paramName} must be between {minimum} and {maximum}."
            );
        }
    }

    private void RequirePositiveTuParam(
        Array<string> errors,
        StringName skillId,
        Dictionary parameters,
        string contextLabel,
        string paramName
    )
    {
        if (
            TryReadStrictIntParam(errors, skillId, parameters, contextLabel, paramName, out int value)
            && (value <= 0 || !IsValidTuValue(value))
        )
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel} params.{paramName} must be a positive multiple of {TuGranularity}."
            );
        }
    }

    private static bool TryReadStrictIntParam(
        Array<string> errors,
        StringName skillId,
        Dictionary parameters,
        string contextLabel,
        string paramName,
        out int value
    )
    {
        if (!TryGetParameter(parameters, paramName, out object rawValue))
        {
            value = 0;
            return false;
        }
        if (TryStrictInt(rawValue, out value))
            return true;

        errors.Add(
            $"Skill {skillId} effect {contextLabel} params.{paramName} must be an int."
        );
        return false;
    }

    private static string ParameterKeyLabel(Variant rawKey)
    {
        return rawKey.VariantType switch
        {
            VT.String => rawKey.AsString(),
            VT.StringName => rawKey.AsStringName().ToString(),
            _ => rawKey.ToString(),
        };
    }

    private bool IsValidPendingCastBindingMode(StringName value)
    {
        return value == BattleTypedNames.PendingCastBindingSoftAnchor
            || value == BattleTypedNames.PendingCastBindingHardAnchor
            || value == BattleTypedNames.PendingCastBindingGroundBind;
    }

    private void AppendCastingTimeCompatibilityErrors(
        Array<string> errors,
        StringName skillId,
        CombatSkillDef combatProfile,
        SkillDef skillDef,
        string contextLabel = "combat_profile"
    )
    {
        if (combatProfile.special_resolution_profile_id != "")
            errors.Add(
                $"Skill {skillId} {contextLabel} cannot combine casting_time_tu with special_resolution_profile_id."
            );
        if (combatProfile.TargetSelectionModeKind == BattleTargetSelectionMode.RandomChain)
            errors.Add(
                $"Skill {skillId} {contextLabel} cannot combine casting_time_tu with random_chain target_selection_mode."
            );
        if (combatProfile.fumble_protection_curve != null && combatProfile.fumble_protection_curve.Length > 0)
            errors.Add(
                $"Skill {skillId} {contextLabel} cannot combine casting_time_tu with fumble_protection_curve."
            );
        if (skillDef != null)
        {
            if (IsIdentityLearnSource(skillDef.learn_source))
                errors.Add(
                    $"Skill {skillId} {contextLabel} cannot combine casting_time_tu with identity-granted learn_source {skillDef.learn_source}."
                );
            if (MisfortuneService.IsMisfortuneGatedSkill(skillDef.skill_id))
                errors.Add(
                    $"Skill {skillId} {contextLabel} cannot combine casting_time_tu with misfortune-gated skills."
                );
        }
        if (skillId == "black_contract_push")
            errors.Add(
                $"Skill {skillId} {contextLabel} cannot combine casting_time_tu with black-contract-push variants."
            );
        AppendCastingTimeEffectCompatibilityErrors(
            errors,
            skillId,
            combatProfile.effect_defs,
            $"{contextLabel}.effect_defs"
        );
        for (int optionIndex = 0; optionIndex < combatProfile.cast_variants.Count; optionIndex++)
        {
            CombatCastVariantDef castVariant = combatProfile.cast_variants[optionIndex];
            AppendCastingTimeEffectCompatibilityErrors(
                errors,
                skillId,
                castVariant?.effect_defs,
                $"{contextLabel}.cast_variants[{optionIndex}].effect_defs"
            );
        }
    }

    private void AppendCastingTimeEffectCompatibilityErrors(
        Array<string> errors,
        StringName skillId,
        Array<CombatEffectDef> effectDefs,
        string contextLabel
    )
    {
        if (effectDefs == null)
        {
            return;
        }
        for (int effectIndex = 0; effectIndex < effectDefs.Count; effectIndex++)
        {
            CombatEffectDef effectDef = effectDefs[effectIndex];
            if (effectDef == null)
            {
                continue;
            }
            if (
                effectDef.EffectKind == BattleEffectKind.Charge
                || effectDef.EffectKind == BattleEffectKind.PathStepAoe
            )
            {
                errors.Add(
                    $"Skill {skillId} {contextLabel}[{effectIndex}] cannot use {effectDef.effect_type} with casting_time_tu."
                );
            }
            if (
                effectDef.EffectKind == BattleEffectKind.ForcedMove
                && (
                    effectDef.ForcedMoveModeKind == BattleForcedMoveMode.Jump
                    || effectDef.ForcedMoveModeKind == BattleForcedMoveMode.Blink
                )
                && effectDef.effect_target_team_filter == "self"
            )
            {
                errors.Add(
                    $"Skill {skillId} {contextLabel}[{effectIndex}] cannot use self relocation with casting_time_tu."
                );
            }
        }
    }

    private void AppendSaveBonusByTagValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatEffectDef effectDef,
        string contextLabel
    )
    {
        Dictionary parameters = effectDef.@params ?? new Dictionary();
        if (!parameters.ContainsKey("save_bonus_by_tag"))
            return;
        Variant rawMap = parameters["save_bonus_by_tag"];
        if (rawMap.VariantType != VT.Dictionary)
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel} params.save_bonus_by_tag must be a Dictionary."
            );
            return;
        }
        Dictionary bonusMap = rawMap.AsGodotDictionary();
        foreach (Variant rawKey in bonusMap.Keys)
        {
            if (rawKey.VariantType != VT.StringName)
            {
                errors.Add(
                    $"Skill {skillId} effect {contextLabel} params.save_bonus_by_tag keys must be StringName."
                );
                continue;
            }
            StringName saveTag = rawKey.AsStringName();
            if (!BattleSaveContentRules.IsValidSaveTag(saveTag))
                errors.Add(
                    $"Skill {skillId} effect {contextLabel} params.save_bonus_by_tag uses unsupported save tag {saveTag}."
                );
            Variant rawValue = bonusMap[rawKey];
            if (rawValue.VariantType != VT.Int || rawValue.AsInt32() < 1)
                errors.Add(
                    $"Skill {skillId} effect {contextLabel} params.save_bonus_by_tag.{saveTag} must be an int >= 1."
                );
        }
    }

    private void AppendTemporalStatusEffectValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatEffectDef effectDef,
        string contextLabel
    )
    {
        BattleEffectKind effectKind = effectDef.EffectKind;
        StringName statusId = ProgressionDataUtils.to_string_name(effectDef.status_id);
        StringName temporalTag = BattleTemporalStatusService.TemporalStatusTag;
        bool isStatusKind =
            effectKind == BattleEffectKind.Status || effectKind == BattleEffectKind.ApplyStatus;
        if (isStatusKind && statusId == BattleStatusSemanticTable.STATUS_TIME_REVERBERATION)
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel} cannot apply time_reverberation directly; it is runtime-applied on temporal release."
            );
            return;
        }
        bool isTemporalControlStatus =
            statusId == BattleStatusSemanticTable.STATUS_TIME_STASIS
            || statusId == BattleStatusSemanticTable.STATUS_TIME_SLOW;
        if (isStatusKind && isTemporalControlStatus)
        {
            if (!effectDef.HasEffectTagTyped(temporalTag))
                errors.Add(
                    $"Skill {skillId} effect {contextLabel} applying {statusId} must declare effect_tags temporal."
                );
            if (ProgressionDataUtils.to_string_name(effectDef.save_tag) != temporalTag)
                errors.Add(
                    $"Skill {skillId} effect {contextLabel} applying {statusId} must use save_tag temporal."
                );
            if (
                effectDef.save_dc <= 0
                && effectDef.SaveDcModeKind != BattleSaveDcMode.CasterSpell
            )
                errors.Add(
                    $"Skill {skillId} effect {contextLabel} applying {statusId} must configure a save."
                );
        }
        if (effectKind == BattleEffectKind.EraseStatus)
        {
            bool hasTemporalTag = effectDef.HasEffectTagTyped(temporalTag);
            bool erasesTemporalControl =
                BattleTemporalStatusService.IsTemporalReleaseTargetStatusId(statusId);
            bool erasesTemporal =
                erasesTemporalControl
                || statusId == BattleStatusSemanticTable.STATUS_TIME_REVERBERATION;
            if (hasTemporalTag && !erasesTemporalControl)
                errors.Add(
                    $"Skill {skillId} effect {contextLabel} temporal erase_status must target time_stasis or time_slow."
                );
            if (!hasTemporalTag && erasesTemporal)
                errors.Add(
                    $"Skill {skillId} effect {contextLabel} erasing {statusId} must declare effect_tags temporal."
                );
        }
    }

    // temporal-only 解控技能：含 temporal release 效果的技能不得混入伤害、治疗、位移或普通状态。
    internal void AppendTemporalReleaseSkillValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatSkillDef combatProfile
    )
    {
        if (combatProfile == null)
            return;
        var labeledEffects = new List<(CombatEffectDef Effect, string Label)>();
        for (int effectIndex = 0; effectIndex < combatProfile.effect_defs.Count; effectIndex++)
        {
            labeledEffects.Add(
                (
                    combatProfile.effect_defs[effectIndex],
                    $"combat_profile.effect_defs[{effectIndex}]"
                )
            );
        }
        for (int optionIndex = 0; optionIndex < combatProfile.cast_variants.Count; optionIndex++)
        {
            CombatCastVariantDef castVariant = combatProfile.cast_variants[optionIndex];
            if (castVariant?.effect_defs == null)
                continue;
            for (int effectIndex = 0; effectIndex < castVariant.effect_defs.Count; effectIndex++)
            {
                labeledEffects.Add(
                    (
                        castVariant.effect_defs[effectIndex],
                        $"combat_profile.cast_variants[{optionIndex}].effect_defs[{effectIndex}]"
                    )
                );
            }
        }
        bool hasTemporalRelease = false;
        foreach ((CombatEffectDef effect, string _) in labeledEffects)
        {
            if (IsTemporalReleaseEffectResource(effect))
            {
                hasTemporalRelease = true;
                break;
            }
        }
        if (!hasTemporalRelease)
            return;
        foreach ((CombatEffectDef effect, string label) in labeledEffects)
        {
            if (effect == null || IsTemporalReleaseEffectResource(effect))
                continue;
            errors.Add(
                $"Skill {skillId} {label} cannot mix {effect.effect_type} with temporal release effects; temporal release skills must stay temporal-only."
            );
        }
    }

    private static bool IsTemporalReleaseEffectResource(CombatEffectDef effectDef)
    {
        CombatEffectDefinition effectDefinition = CombatEffectDefinition.FromResource(effectDef);
        return BattleTemporalStatusService.IsTemporalReleaseEffect(effectDefinition);
    }

    private static bool IsIdentityLearnSource(StringName learnSource)
    {
        return learnSource == "race"
            || learnSource == "subrace"
            || learnSource == "ascension"
            || learnSource == "bloodline";
    }

    private bool HasValidShieldDiceConfig(CombatEffectDef effectDef)
    {
        if (effectDef == null)
            return false;
        return _has_valid_fixed_dice_config(effectDef)
            || _has_valid_attribute_scaled_dice_config(effectDef);
    }

    private static bool _has_fixed_dice_fields(CombatEffectDef effectDef)
    {
        if (effectDef == null)
            return false;
        bool hasAttributeScaledDice = _has_attribute_scaled_dice_fields(effectDef);
        return effectDef.dice_sides > 0
            || (effectDef.dice_bonus != 0 && !hasAttributeScaledDice)
            || (effectDef.dice_count > 0 && !hasAttributeScaledDice);
    }

    private static bool _has_valid_fixed_dice_config(CombatEffectDef effectDef)
    {
        if (effectDef == null)
            return false;
        return effectDef.dice_count > 0 && effectDef.dice_sides > 0;
    }

    private static bool _has_attribute_scaled_dice_fields(CombatEffectDef effectDef)
    {
        if (effectDef == null)
            return false;
        return effectDef.dice_sides_base > 0
            || effectDef.dice_sides_per_constitution_mod != 0
            || effectDef.dice_sides_per_willpower_mod != 0;
    }

    private static bool _has_valid_attribute_scaled_dice_config(CombatEffectDef effectDef)
    {
        if (effectDef == null)
            return false;
        return effectDef.dice_count > 0 && effectDef.dice_sides_base > 0;
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

    private static bool TryReadLevelOverrideInt(
        Array<string> errors,
        StringName skillId,
        object overrideLevelKey,
        Dictionary overrideDict,
        string fieldName,
        out int value
    )
    {
        if (!TryGetParameter(overrideDict, fieldName, out object rawValue))
        {
            value = 0;
            return false;
        }
        if (TryStrictInt(rawValue, out value))
        {
            return true;
        }
        errors.Add(
            $"Skill {skillId} combat_profile level override {overrideLevelKey}.{fieldName} must be an int."
        );
        return false;
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
        return null;
    }

    private static void AppendArray(List<string> target, Array<string> source)
    {
        foreach (string value in source)
            target.Add(value);
    }

    private static Array<string> ToGodotStringArray(IEnumerable<string> values)
    {
        var result = new Array<string>();
        if (values == null)
            return result;
        foreach (string value in values)
            result.Add(value);
        return result;
    }
}
