using System.Linq;
using System.Collections.Generic;
using Godot;
using Godot.Collections;
using VT = Godot.Variant.Type;

internal sealed class SkillDefinitionExecuteEffectValidator
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
        SkillDefinition skillDef,
        CombatSkillDefinition combatProfile
    )
    {
        if (combatProfile == null)
            return;

        bool hasExecute = ValidateExecuteEffectSet(
            errors,
            skillId,
            combatProfile.EffectDefinitions,
            null,
            "combat_profile.effect_defs"
        );
        for (int optionIndex = 0; optionIndex < combatProfile.CastVariants.Count; optionIndex++)
        {
            CombatCastVariantDefinition castVariant = combatProfile.CastVariants[optionIndex];
            hasExecute |= ValidateExecuteEffectSet(
                errors,
                skillId,
                combatProfile.EffectDefinitions,
                castVariant,
                $"combat_profile.cast_variants[{optionIndex}] merged effect_defs"
            );
        }
        if (!hasExecute)
            return;

        if (combatProfile.SpecialResolutionProfileId != "")
        {
            errors.Add(
                $"Skill {skillId} combat_profile.special_resolution_profile_id must be empty when execute is present."
            );
        }
        SkillDefinitionValidationRules.RequireStringName(errors, skillId, "combat_profile.target_mode", combatProfile.TargetMode, "unit");
        SkillDefinitionValidationRules.RequireStringName(
            errors,
            skillId,
            "combat_profile.target_team_filter",
            combatProfile.TargetTeamFilter,
            "enemy"
        );
        SkillDefinitionValidationRules.RequireStringName(
            errors,
            skillId,
            "combat_profile.target_selection_mode",
            combatProfile.TargetSelectionMode,
            "single_unit"
        );
        SkillDefinitionValidationRules.RequireInt(errors, skillId, "combat_profile.min_target_count", combatProfile.MinTargetCount, 1);
        SkillDefinitionValidationRules.RequireInt(errors, skillId, "combat_profile.max_target_count", combatProfile.MaxTargetCount, 1);
        SkillDefinitionValidationRules.RequireBool(
            errors,
            skillId,
            "combat_profile.allow_repeat_target",
            combatProfile.AllowRepeatTarget,
            false
        );
        SkillDefinitionValidationRules.RequireStringName(errors, skillId, "combat_profile.area_pattern", combatProfile.AreaPattern, "single");
        SkillDefinitionValidationRules.RequireInt(errors, skillId, "combat_profile.area_value", combatProfile.AreaValue, 0);
    }

    private bool ValidateExecuteEffectSet(
        Array<string> errors,
        StringName skillId,
IReadOnlyList<CombatEffectDefinition> baseEffects,
        CombatCastVariantDefinition castVariant,
        string contextLabel
    )
    {
        var mergedEffects = new List<CombatEffectDefinition>();
        if (baseEffects != null)
        {
            foreach (CombatEffectDefinition effectDef in baseEffects)
            {
                mergedEffects.Add(effectDef);
            }
        }
        if (castVariant?.EffectDefinitions != null)
        {
            foreach (CombatEffectDefinition effectDef in castVariant.EffectDefinitions)
            {
                mergedEffects.Add(effectDef);
            }
        }

        bool hasExecute = false;
        foreach (CombatEffectDefinition effectDef in mergedEffects)
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
        CombatEffectDefinition effectDef,
        string contextLabel
    )
    {
        SkillDefinitionValidationRules.RequireStringName(
            errors,
            skillId,
            $"{contextLabel}.effect_target_team_filter",
            effectDef.EffectTargetTeamFilter,
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
        if (saveDcMode == BattleSaveDcMode.Static && effectDef.SaveDc <= 0)
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel}.save_dc must be > 0 for static execute saves."
            );
        }
        if (saveDcMode == BattleSaveDcMode.CasterSpell && effectDef.SaveDc != 0)
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel}.save_dc must be 0 for caster_spell execute saves."
            );
        }
        StringName saveDcSourceAbility = ProgressionDataUtils.to_string_name(
            effectDef.SaveDcSourceAbility
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
        if (!BattleSaveContentRules.IsValidSaveAbility(effectDef.SaveAbility))
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel}.save_ability must be a valid base ability."
            );
        }
        SkillDefinitionValidationRules.RequireStringName(
            errors,
            skillId,
            $"{contextLabel}.save_tag",
            effectDef.SaveTag,
            BattleSaveContentRules.ToStringName(BattleSaveTagKind.Execute)
        );
        if (DamageTagContentRules.ToDamageTagKind(effectDef.DamageTag) == DamageTagKind.Unknown)
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel}.damage_tag must be one of {DamageTagContentRules.ValidDamageTagLabel()}."
            );
        }
        SkillDefinitionValidationRules.RequireBool(
            errors,
            skillId,
            $"{contextLabel}.save_partial_on_success",
            effectDef.SavePartialOnSuccess,
            false
        );
        SkillDefinitionValidationRules.RequireStringName(errors, skillId, $"{contextLabel}.trigger_event", effectDef.TriggerEvent, "");
        SkillDefinitionValidationRules.RequireStringName(
            errors,
            skillId,
            $"{contextLabel}.trigger_condition",
            effectDef.TriggerCondition,
            ""
        );
        SkillDefinitionValidationRules.RequireRange(
            errors,
            skillId,
            $"{contextLabel}.threshold_max_hp_ratio_percent",
            effectDef.ThresholdMaxHpRatioPercent,
            0,
            100
        );
        SkillDefinitionValidationRules.RequireRange(
            errors,
            skillId,
            $"{contextLabel}.threshold_cap_max_hp_ratio_percent",
            effectDef.ThresholdCapMaxHpRatioPercent,
            0,
            100
        );
        if (effectDef.ThresholdCapMaxHpRatioPercent < effectDef.ThresholdMaxHpRatioPercent)
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel}.threshold_cap_max_hp_ratio_percent must be >= threshold_max_hp_ratio_percent."
            );
        }
        SkillDefinitionValidationRules.RequireRange(
            errors,
            skillId,
            $"{contextLabel}.heal_multiplier_percent",
            effectDef.HealMultiplierPercent,
            0,
            100
        );
        SkillDefinitionValidationRules.RequireRange(
            errors,
            skillId,
            $"{contextLabel}.shield_gain_multiplier_percent",
            effectDef.ShieldGainMultiplierPercent,
            0,
            100
        );
        if (
            effectDef.SoulFractureDurationTu < 0
            || (
                effectDef.SoulFractureDurationTu > 0
                && !SkillDefinitionValidationRules.IsValidTuValue(effectDef.SoulFractureDurationTu)
            )
        )
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel}.soul_fracture_duration_tu must be 0 or a positive value divisible by {SkillDefinitionValidationRules.TuGranularity}."
            );
        }
        if (effectDef.Parameters != null && effectDef.Parameters.Count > 0)
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel} execute must not use params payload."
            );
        }
    }

    internal void AppendGradedSaveExecuteValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatEffectDefinition effectDef,
        string contextLabel
    )
    {
        SkillDefinitionValidationRules.RequireStringName(
            errors,
            skillId,
            $"{contextLabel}.effect_target_team_filter",
            effectDef.EffectTargetTeamFilter,
            "any"
        );
        SkillDefinitionValidationRules.RequireStringName(
            errors,
            skillId,
            $"{contextLabel}.damage_tag",
            effectDef.DamageTag,
            "psychic"
        );
        SkillDefinitionValidationRules.RequireStringName(
            errors,
            skillId,
            $"{contextLabel}.save_dc_mode",
            effectDef.SaveDcMode,
            BattleSaveContentRules.ToStringName(BattleSaveDcMode.CasterSpell)
        );
        SkillDefinitionValidationRules.RequireInt(errors, skillId, $"{contextLabel}.save_dc", effectDef.SaveDc, 0);
        SkillDefinitionValidationRules.RequireStringName(
            errors,
            skillId,
            $"{contextLabel}.save_dc_source_ability",
            effectDef.SaveDcSourceAbility,
            UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Intelligence)
        );
        SkillDefinitionValidationRules.RequireStringName(
            errors,
            skillId,
            $"{contextLabel}.save_ability",
            effectDef.SaveAbility,
            UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Willpower)
        );
        SkillDefinitionValidationRules.RequireStringName(
            errors,
            skillId,
            $"{contextLabel}.save_tag",
            effectDef.SaveTag,
            BattleSaveContentRules.ToStringName(BattleSaveTagKind.Illusion)
        );
        SkillDefinitionValidationRules.RequireBool(
            errors,
            skillId,
            $"{contextLabel}.save_partial_on_success",
            effectDef.SavePartialOnSuccess,
            false
        );

        IReadOnlyDictionary<string, object> parameters = effectDef.Parameters ?? new System.Collections.Generic.Dictionary<string, object>();
        AppendGradedSaveExecuteParamKeyValidationErrors(
            errors,
            skillId,
            parameters,
            contextLabel
        );
        SkillDefinitionValidationRules.RequireStringNameParam(
            errors,
            skillId,
            parameters,
            contextLabel,
            "profile_id",
            "phantasmal_kill"
        );
        SkillDefinitionValidationRules.RequireNonNegativeIntParam(
            errors,
            skillId,
            parameters,
            contextLabel,
            "failure_execute_threshold_fixed"
        );
        SkillDefinitionValidationRules.RequireIntRangeParam(
            errors,
            skillId,
            parameters,
            contextLabel,
            "failure_execute_threshold_max_hp_percent",
            1,
            100
        );
        SkillDefinitionValidationRules.RequirePositiveIntParam(
            errors,
            skillId,
            parameters,
            contextLabel,
            "failure_damage_dice_count"
        );
        SkillDefinitionValidationRules.RequirePositiveIntParam(
            errors,
            skillId,
            parameters,
            contextLabel,
            "failure_damage_dice_sides"
        );
        SkillDefinitionValidationRules.RequirePositiveTuParam(
            errors,
            skillId,
            parameters,
            contextLabel,
            "failure_frightened_duration_tu"
        );
        SkillDefinitionValidationRules.RequirePositiveTuParam(
            errors,
            skillId,
            parameters,
            contextLabel,
            "failure_reaction_lock_duration_tu"
        );
        SkillDefinitionValidationRules.RequireIntRangeParam(
            errors,
            skillId,
            parameters,
            contextLabel,
            "critical_failure_execute_threshold_max_hp_percent",
            1,
            100
        );
        SkillDefinitionValidationRules.RequirePositiveIntParam(
            errors,
            skillId,
            parameters,
            contextLabel,
            "critical_failure_damage_dice_count"
        );
        SkillDefinitionValidationRules.RequirePositiveIntParam(
            errors,
            skillId,
            parameters,
            contextLabel,
            "critical_failure_damage_dice_sides"
        );
        SkillDefinitionValidationRules.RequirePositiveTuParam(
            errors,
            skillId,
            parameters,
            contextLabel,
            "critical_failure_frightened_duration_tu"
        );
        SkillDefinitionValidationRules.RequirePositiveTuParam(
            errors,
            skillId,
            parameters,
            contextLabel,
            "critical_failure_stunned_duration_tu"
        );
        SkillDefinitionValidationRules.RequirePositiveTuParam(
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
        IReadOnlyDictionary<string, object> parameters,
        string contextLabel
    )
    {
        parameters ??= new System.Collections.Generic.Dictionary<string, object>();
        foreach (string rawKey in parameters.Keys)
        {
            string keyLabel = SkillDefinitionValidationRules.ParameterKeyLabel(rawKey);
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
        CombatEffectDefinition effectDef,
        string contextLabel
    )
    {
        IReadOnlyDictionary<string, object> parameters = effectDef.Parameters ?? new System.Collections.Generic.Dictionary<string, object>();
        if (!parameters.ContainsKey("save_bonus_by_tag"))
            return;
        object rawMap = parameters["save_bonus_by_tag"];
        if (
            rawMap
            is not IReadOnlyDictionary<string, object> bonusMap
        )
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel} params.save_bonus_by_tag must be a Dictionary."
            );
            return;
        }
        foreach (string rawKey in bonusMap.Keys)
        {
            StringName saveTag = rawKey;
            if (!BattleSaveContentRules.IsValidSaveTag(saveTag))
                errors.Add(
                    $"Skill {skillId} effect {contextLabel} params.save_bonus_by_tag uses unsupported save tag {saveTag}."
                );
            object rawValue = bonusMap[rawKey];
            if (
                !SkillDefinitionValidationRules.TryStrictInt(rawValue, out int value)
                || value < 1
            )
                errors.Add(
                    $"Skill {skillId} effect {contextLabel} params.save_bonus_by_tag.{saveTag} must be an int >= 1."
                );
        }
    }

    internal void AppendTemporalStatusEffectValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatEffectDefinition effectDef,
        string contextLabel
    )
    {
        BattleEffectKind effectKind = effectDef.EffectKind;
        StringName statusId = ProgressionDataUtils.to_string_name(effectDef.StatusId);
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
            if (ProgressionDataUtils.to_string_name(effectDef.SaveTag) != temporalTag)
                errors.Add(
                    $"Skill {skillId} effect {contextLabel} applying {statusId} must use save_tag temporal."
                );
            if (
                effectDef.SaveDc <= 0
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
        CombatSkillDefinition combatProfile
    )
    {
        if (combatProfile == null)
            return;
        var labeledEffects = new List<(CombatEffectDefinition Effect, string Label)>();
        for (int effectIndex = 0; effectIndex < combatProfile.EffectDefinitions.Count; effectIndex++)
        {
            labeledEffects.Add(
                (
                    combatProfile.EffectDefinitions[effectIndex],
                    $"combat_profile.effect_defs[{effectIndex}]"
                )
            );
        }
        for (int optionIndex = 0; optionIndex < combatProfile.CastVariants.Count; optionIndex++)
        {
            CombatCastVariantDefinition castVariant = combatProfile.CastVariants[optionIndex];
            if (castVariant?.EffectDefinitions == null)
                continue;
            for (int effectIndex = 0; effectIndex < castVariant.EffectDefinitions.Count; effectIndex++)
            {
                labeledEffects.Add(
                    (
                        castVariant.EffectDefinitions[effectIndex],
                        $"combat_profile.cast_variants[{optionIndex}].effect_defs[{effectIndex}]"
                    )
                );
            }
        }
        bool hasTemporalRelease = false;
        foreach ((CombatEffectDefinition effect, string _) in labeledEffects)
        {
            if (IsTemporalReleaseEffectResource(effect))
            {
                hasTemporalRelease = true;
                break;
            }
        }
        if (!hasTemporalRelease)
            return;
        foreach ((CombatEffectDefinition effect, string label) in labeledEffects)
        {
            if (effect == null || IsTemporalReleaseEffectResource(effect))
                continue;
            errors.Add(
                $"Skill {skillId} {label} cannot mix {effect.EffectType} with temporal release effects; temporal release skills must stay temporal-only."
            );
        }
    }

    private static bool IsTemporalReleaseEffectResource(CombatEffectDefinition effectDef)
    {
        return TemporalStatusContentRules.IsTemporalReleaseEffect(effectDef);
    }
}
