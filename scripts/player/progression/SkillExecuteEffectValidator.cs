using System.Collections.Generic;
using Godot;
using Godot.Collections;
using VT = Godot.Variant.Type;

internal sealed class SkillExecuteEffectValidator
{
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

    internal void AppendExecuteCombatProfileValidationErrors(
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
        SkillContentRegistry.RequireStringName(errors, skillId, "combat_profile.target_mode", combatProfile.target_mode, "unit");
        SkillContentRegistry.RequireStringName(
            errors,
            skillId,
            "combat_profile.target_team_filter",
            combatProfile.target_team_filter,
            "enemy"
        );
        SkillContentRegistry.RequireStringName(
            errors,
            skillId,
            "combat_profile.target_selection_mode",
            combatProfile.target_selection_mode,
            "single_unit"
        );
        SkillContentRegistry.RequireInt(errors, skillId, "combat_profile.min_target_count", combatProfile.min_target_count, 1);
        SkillContentRegistry.RequireInt(errors, skillId, "combat_profile.max_target_count", combatProfile.max_target_count, 1);
        SkillContentRegistry.RequireBool(
            errors,
            skillId,
            "combat_profile.allow_repeat_target",
            combatProfile.allow_repeat_target,
            false
        );
        SkillContentRegistry.RequireStringName(errors, skillId, "combat_profile.area_pattern", combatProfile.area_pattern, "single");
        SkillContentRegistry.RequireInt(errors, skillId, "combat_profile.area_value", combatProfile.area_value, 0);
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

    internal void AppendExecuteEffectValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatEffectDef effectDef,
        string contextLabel
    )
    {
        SkillContentRegistry.RequireStringName(
            errors,
            skillId,
            $"{contextLabel}.effect_target_team_filter",
            effectDef.effect_target_team_filter,
            "enemy"
        );
        BattleSaveDcMode saveDcMode = effectDef.SaveDcModeKind;
        if (
            saveDcMode != BattleSaveDcMode.Static
            && saveDcMode != BattleSaveDcMode.CasterSpell
        )
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel}.save_dc_mode must be static or caster_spell."
            );
        }
        if (saveDcMode == BattleSaveDcMode.Static && effectDef.save_dc <= 0)
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel}.save_dc must be > 0 for static execute saves."
            );
        }
        if (saveDcMode == BattleSaveDcMode.CasterSpell && effectDef.save_dc != 0)
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel}.save_dc must be 0 for caster_spell execute saves."
            );
        }
        StringName saveDcSourceAbility = ProgressionDataUtils.to_string_name(
            effectDef.save_dc_source_ability
        );
        if (saveDcMode == BattleSaveDcMode.CasterSpell)
        {
            if (!BattleSaveContentRules.IsValidSaveAbility(saveDcSourceAbility))
            {
                errors.Add(
                    $"Skill {skillId} effect {contextLabel}.save_dc_source_ability must be a valid base ability for caster_spell execute saves."
                );
            }
        }
        else if (saveDcSourceAbility != "")
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel}.save_dc_source_ability must be empty unless save_dc_mode is caster_spell."
            );
        }
        if (!BattleSaveContentRules.IsValidSaveAbility(effectDef.save_ability))
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel}.save_ability must be a valid base ability."
            );
        }
        SkillContentRegistry.RequireStringName(
            errors,
            skillId,
            $"{contextLabel}.save_tag",
            effectDef.save_tag,
            BattleSaveContentRules.ToStringName(BattleSaveTagKind.Execute)
        );
        if (DamageTagContentRules.ToDamageTagKind(effectDef.damage_tag) == DamageTagKind.Unknown)
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel}.damage_tag must be one of {DamageTagContentRules.ValidDamageTagLabel()}."
            );
        }
        SkillContentRegistry.RequireBool(
            errors,
            skillId,
            $"{contextLabel}.save_partial_on_success",
            effectDef.save_partial_on_success,
            false
        );
        SkillContentRegistry.RequireStringName(errors, skillId, $"{contextLabel}.trigger_event", effectDef.trigger_event, "");
        SkillContentRegistry.RequireStringName(
            errors,
            skillId,
            $"{contextLabel}.trigger_condition",
            effectDef.trigger_condition,
            ""
        );
        SkillContentRegistry.RequireRange(
            errors,
            skillId,
            $"{contextLabel}.threshold_max_hp_ratio_percent",
            effectDef.threshold_max_hp_ratio_percent,
            0,
            100
        );
        SkillContentRegistry.RequireRange(
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
        SkillContentRegistry.RequireRange(
            errors,
            skillId,
            $"{contextLabel}.heal_multiplier_percent",
            effectDef.heal_multiplier_percent,
            0,
            100
        );
        SkillContentRegistry.RequireRange(
            errors,
            skillId,
            $"{contextLabel}.shield_gain_multiplier_percent",
            effectDef.shield_gain_multiplier_percent,
            0,
            100
        );
        if (
            effectDef.soul_fracture_duration_tu < 0
            || (
                effectDef.soul_fracture_duration_tu > 0
                && !SkillContentRegistry.IsValidTuValue(effectDef.soul_fracture_duration_tu)
            )
        )
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel}.soul_fracture_duration_tu must be 0 or a positive value divisible by {SkillContentRegistry.TuGranularity}."
            );
        }
        if (effectDef.@params != null && effectDef.@params.Count > 0)
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel} execute must not use params payload."
            );
        }
    }

    internal void AppendGradedSaveExecuteValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatEffectDef effectDef,
        string contextLabel
    )
    {
        SkillContentRegistry.RequireStringName(
            errors,
            skillId,
            $"{contextLabel}.effect_target_team_filter",
            effectDef.effect_target_team_filter,
            "any"
        );
        SkillContentRegistry.RequireStringName(
            errors,
            skillId,
            $"{contextLabel}.damage_tag",
            effectDef.damage_tag,
            "psychic"
        );
        SkillContentRegistry.RequireStringName(
            errors,
            skillId,
            $"{contextLabel}.save_dc_mode",
            effectDef.save_dc_mode,
            BattleSaveContentRules.ToStringName(BattleSaveDcMode.CasterSpell)
        );
        SkillContentRegistry.RequireInt(errors, skillId, $"{contextLabel}.save_dc", effectDef.save_dc, 0);
        SkillContentRegistry.RequireStringName(
            errors,
            skillId,
            $"{contextLabel}.save_dc_source_ability",
            effectDef.save_dc_source_ability,
            UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Intelligence)
        );
        SkillContentRegistry.RequireStringName(
            errors,
            skillId,
            $"{contextLabel}.save_ability",
            effectDef.save_ability,
            UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Willpower)
        );
        SkillContentRegistry.RequireStringName(
            errors,
            skillId,
            $"{contextLabel}.save_tag",
            effectDef.save_tag,
            BattleSaveContentRules.ToStringName(BattleSaveTagKind.Illusion)
        );
        SkillContentRegistry.RequireBool(
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
        SkillContentRegistry.RequireStringNameParam(
            errors,
            skillId,
            parameters,
            contextLabel,
            "profile_id",
            "phantasmal_kill"
        );
        SkillContentRegistry.RequireNonNegativeIntParam(
            errors,
            skillId,
            parameters,
            contextLabel,
            "failure_execute_threshold_fixed"
        );
        SkillContentRegistry.RequireIntRangeParam(
            errors,
            skillId,
            parameters,
            contextLabel,
            "failure_execute_threshold_max_hp_percent",
            1,
            100
        );
        SkillContentRegistry.RequirePositiveIntParam(
            errors,
            skillId,
            parameters,
            contextLabel,
            "failure_damage_dice_count"
        );
        SkillContentRegistry.RequirePositiveIntParam(
            errors,
            skillId,
            parameters,
            contextLabel,
            "failure_damage_dice_sides"
        );
        SkillContentRegistry.RequirePositiveTuParam(
            errors,
            skillId,
            parameters,
            contextLabel,
            "failure_frightened_duration_tu"
        );
        SkillContentRegistry.RequirePositiveTuParam(
            errors,
            skillId,
            parameters,
            contextLabel,
            "failure_reaction_lock_duration_tu"
        );
        SkillContentRegistry.RequireIntRangeParam(
            errors,
            skillId,
            parameters,
            contextLabel,
            "critical_failure_execute_threshold_max_hp_percent",
            1,
            100
        );
        SkillContentRegistry.RequirePositiveIntParam(
            errors,
            skillId,
            parameters,
            contextLabel,
            "critical_failure_damage_dice_count"
        );
        SkillContentRegistry.RequirePositiveIntParam(
            errors,
            skillId,
            parameters,
            contextLabel,
            "critical_failure_damage_dice_sides"
        );
        SkillContentRegistry.RequirePositiveTuParam(
            errors,
            skillId,
            parameters,
            contextLabel,
            "critical_failure_frightened_duration_tu"
        );
        SkillContentRegistry.RequirePositiveTuParam(
            errors,
            skillId,
            parameters,
            contextLabel,
            "critical_failure_stunned_duration_tu"
        );
        SkillContentRegistry.RequirePositiveTuParam(
            errors,
            skillId,
            parameters,
            contextLabel,
            "success_aftershock_duration_tu"
        );
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
            string keyLabel = SkillContentRegistry.ParameterKeyLabel(rawKey);
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

    internal void AppendSaveBonusByTagValidationErrors(
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

    internal void AppendTemporalStatusEffectValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatEffectDef effectDef,
        string contextLabel
    )
    {
        BattleEffectKind effectKind = effectDef.EffectKind;
        StringName statusId = ProgressionDataUtils.to_string_name(effectDef.status_id);
        StringName temporalTag = TemporalStatusContentRules.TemporalStatusTag;
        bool isStatusKind =
            effectKind == BattleEffectKind.Status || effectKind == BattleEffectKind.ApplyStatus;
        if (isStatusKind && statusId == TemporalStatusContentRules.TimeReverberationStatusId)
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel} cannot apply time_reverberation directly; it is runtime-applied on temporal release."
            );
            return;
        }
        bool isTemporalControlStatus =
            statusId == TemporalStatusContentRules.TimeStasisStatusId
            || statusId == TemporalStatusContentRules.TimeSlowStatusId;
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
                TemporalStatusContentRules.IsTemporalReleaseTargetStatusId(statusId);
            bool erasesTemporal =
                erasesTemporalControl
                || statusId == TemporalStatusContentRules.TimeReverberationStatusId;
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
        CombatEffectDefinition effectDefinition = CombatEffectDefinition.FromResource(
            effectDef,
            "skill_content_validation.temporal_release_effect"
        );
        return TemporalStatusContentRules.IsTemporalReleaseEffect(effectDefinition);
    }
}
