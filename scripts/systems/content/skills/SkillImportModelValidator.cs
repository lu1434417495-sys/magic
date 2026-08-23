#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Godot;
using GdStringArray = Godot.Collections.Array<string>;

/// <summary>
/// Domain-local skill validation entry point for the normalized plain import graph.
/// Production JSON and diagnostic fixtures share this exact validator. The immutable
/// definition used by rule evaluators is produced only by the canonical import-model projector.
/// </summary>
internal sealed class SkillImportModelValidator
{
    internal const string DomainRuleId = "skill.validation.domain_rule";
    internal const string AttributeGrowthTotalRuleId =
        "skill.validation.attribute_growth_total";
    private static readonly StringName[] PracticeTrackTags =
    {
        "meditation",
        "cultivation",
    };

    private readonly SkillDefinitionDamageEffectValidator _damage = new();
    private readonly SkillDefinitionExecuteEffectValidator _execute = new();
    private readonly SkillDefinitionCombatProfileValidator _combat;

    internal SkillImportModelValidator()
    {
        _combat = new SkillDefinitionCombatProfileValidator(_damage, _execute);
    }

    internal IReadOnlyList<ContentJsonDiagnostic> ValidateDomainLocal(
        JsonContentEntryContext context,
        SkillImportModel import
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(import);
        IReadOnlyList<string> messages = ValidateMessages(import);
        return new ReadOnlyCollection<ContentJsonDiagnostic>(
            messages.Select(message => ProjectDiagnostic(context, import, message))
                .ToList()
        );
    }

    private static ContentJsonDiagnostic ProjectDiagnostic(
        JsonContentEntryContext context,
        SkillImportModel import,
        string message
    )
    {
        long actualGrowthTotal = import.AttributeGrowthProgress.Values.Sum(
            static amount => (long)amount
        );
        string growthTierWire = SkillRootCombatImportValueRules.GetWireValue(
            import.GrowthTier
        );
        int expectedGrowthTotal = AttributeGrowthContentRules.GetTierBudget(
            new StringName(growthTierWire)
        );
        if (
            expectedGrowthTotal > 0
            && actualGrowthTotal != expectedGrowthTotal
            && message.EndsWith(
                $"attribute_growth_progress total must equal {expectedGrowthTotal} "
                    + $"for growth_tier {growthTierWire}.",
                StringComparison.Ordinal
            )
        )
        {
            return new ContentJsonDiagnostic(
                AttributeGrowthTotalRuleId,
                message,
                context.SourceLabel,
                context.JsonPointer + "/attribute_growth_progress",
                $"sum equal to {expectedGrowthTotal} for growth_tier "
                    + growthTierWire,
                $"sum={actualGrowthTotal}"
            );
        }
        return new ContentJsonDiagnostic(
            DomainRuleId,
            message,
            context.SourceLabel,
            context.JsonPointer
        );
    }

    internal IReadOnlyList<string> ValidateMessages(SkillImportModel import)
    {
        ArgumentNullException.ThrowIfNull(import);
        SkillDefinition skill = SkillDefinitionProjector.Project(import);
        var errors = new GdStringArray();
        AppendSkillValidationErrors(errors, skill);
        return System.Array.AsReadOnly(errors.ToArray());
    }

    internal IReadOnlyList<string> ValidateBatchMessages(
        IReadOnlyDictionary<StringName, SkillImportModel> imports
    )
    {
        ArgumentNullException.ThrowIfNull(imports);
        var errors = new List<string>();
        var definitions = new Dictionary<StringName, SkillDefinition>();
        foreach ((StringName skillId, SkillImportModel import) in imports)
        {
            foreach (string message in ValidateMessages(import))
                errors.Add(message);
            definitions.Add(skillId, SkillDefinitionProjector.Project(import));
        }
        foreach ((StringName skillId, SkillDefinition skill) in definitions)
            AppendSpellReactionReferenceValidationErrors(errors, skillId, skill, definitions);
        return errors.AsReadOnly();
    }

    private void AppendSkillValidationErrors(
        GdStringArray errors,
        SkillDefinition skill
    )
    {
        StringName skillId = skill.SkillId;
        if ((skill.DisplayName ?? "").StripEdges().Length == 0)
            errors.Add($"Skill {skillId} is missing display_name.");
        if (skill.MaxLevel < 0 && skill.DynamicMaxLevelStatId == "")
            errors.Add($"Skill {skillId} must have max_level >= 0.");
        if (skill.NonCoreMaxLevel < 0)
            errors.Add($"Skill {skillId} non_core_max_level must be >= 0.");
        if (
            skill.NonCoreMaxLevel > skill.MaxLevel
            && skill.MaxLevel >= 0
            && skill.DynamicMaxLevelStatId == ""
        )
        {
            errors.Add($"Skill {skillId} non_core_max_level must be <= max_level.");
        }
        if (
            skill.MasteryCurve.Count != skill.MaxLevel
            && skill.MaxLevel >= 0
            && skill.DynamicMaxLevelStatId == ""
        )
        {
            errors.Add($"Skill {skillId} mastery_curve size must match max_level.");
        }
        AppendDynamicMaxLevelValidationErrors(errors, skillId, skill);
        if (skill.MasteryCurve.Any(value => value <= 0))
            errors.Add($"Skill {skillId} has a non-positive mastery threshold.");

        if (skill.SkillTypeKind == SkillTypeKind.Unknown)
            errors.Add($"Skill {skillId} uses unsupported skill_type {skill.SkillType}.");
        if (skill.SkillTypeKind == SkillTypeKind.Active && skill.CombatProfile == null)
            errors.Add($"Skill {skillId} is active but missing combat_profile.");

        AppendPracticeSkillValidationErrors(errors, skillId, skill);
        AppendAttributeGrowthValidationErrors(errors, skillId, skill);
        foreach (
            string message in SkillLevelDescriptionDefinitionValidationRules
                .CollectValidationErrors(skillId, skill)
        )
        {
            errors.Add(message);
        }
        _combat.AppendPhantasmalKillLevelDescriptionValidationErrors(
            errors,
            skillId,
            skill
        );
        _combat.AppendPhantasmalKillCombatProfileValidationErrors(
            errors,
            skillId,
            skill
        );
        if (skill.CombatProfile != null)
        {
            _combat.AppendCombatProfileValidationErrors(
                errors,
                skillId,
                skill.CombatProfile,
                skill
            );
        }
    }

    private static void AppendDynamicMaxLevelValidationErrors(
        GdStringArray errors,
        StringName skillId,
        SkillDefinition skill
    )
    {
        if (skill.DynamicMaxLevelStatId == "")
        {
            if (skill.DynamicMaxLevelBase != 0)
            {
                errors.Add(
                    $"Skill {skillId} dynamic_max_level_base requires dynamic_max_level_stat_id."
                );
            }
            if (skill.DynamicMaxLevelPerStat != 0)
            {
                errors.Add(
                    $"Skill {skillId} dynamic_max_level_per_stat requires dynamic_max_level_stat_id."
                );
            }
            return;
        }
        if (skill.DynamicMaxLevelBase <= 0)
            errors.Add($"Skill {skillId} dynamic_max_level_base must be >= 1.");
        if (skill.DynamicMaxLevelPerStat == 0)
        {
            errors.Add(
                $"Skill {skillId} dynamic_max_level_per_stat must not be 0 when dynamic_max_level_stat_id is set."
            );
        }
    }

    private static void AppendPracticeSkillValidationErrors(
        GdStringArray errors,
        StringName skillId,
        SkillDefinition skill
    )
    {
        int trackCount = PracticeTrackTags.Count(skill.HasTag);
        if (trackCount == 0)
        {
            if (skill.PracticeTierKind != SkillPracticeTierKind.None)
            {
                errors.Add(
                    $"Skill {skillId} practice_tier requires meditation or cultivation tag."
                );
            }
            return;
        }
        if (trackCount != 1)
            errors.Add($"Skill {skillId} must use exactly one practice track tag.");
        if (skill.Tags.Count != 1)
        {
            errors.Add(
                $"Skill {skillId} practice tags must be exclusive; tags must contain only meditation or cultivation."
            );
        }
        if (
            skill.PracticeTierKind
            is SkillPracticeTierKind.None or SkillPracticeTierKind.Unknown
        )
        {
            errors.Add(
                $"Skill {skillId} practice_tier must be one of basic, intermediate, advanced, ultimate."
            );
        }
    }

    private static void AppendAttributeGrowthValidationErrors(
        GdStringArray errors,
        StringName skillId,
        SkillDefinition skill
    )
    {
        if (skill.AttributeGrowthProgress.Count == 0 && skill.GrowthTier == "")
            return;
        if (!AttributeGrowthContentRules.IsValidGrowthTier(skill.GrowthTier))
        {
            errors.Add(
                $"Skill {skillId} uses unsupported growth_tier {skill.GrowthTier}."
            );
            return;
        }
        int progressTotal = 0;
        foreach ((StringName attributeId, int amount) in skill.AttributeGrowthProgress)
        {
            if (!AttributeGrowthContentRules.IsValidAttributeId(attributeId))
            {
                errors.Add(
                    $"Skill {skillId} attribute_growth_progress references invalid attribute {attributeId}."
                );
            }
            if (amount <= 0)
            {
                errors.Add(
                    $"Skill {skillId} attribute_growth_progress for {attributeId} must be a positive int."
                );
            }
            progressTotal += amount;
        }
        int expectedTotal = AttributeGrowthContentRules.GetTierBudget(skill.GrowthTier);
        if (progressTotal != expectedTotal)
        {
            errors.Add(
                $"Skill {skillId} attribute_growth_progress total must equal {expectedTotal} for growth_tier {skill.GrowthTier}."
            );
        }
    }

    private static void AppendSpellReactionReferenceValidationErrors(
        List<string> errors,
        StringName skillId,
        SkillDefinition skill,
        IReadOnlyDictionary<StringName, SkillDefinition> definitions
    )
    {
        StringName reactionSkillId =
            skill.CombatProfile?.SpellReaction?.ReactionSkillId
            ?? new StringName("");
        if (reactionSkillId == "")
            return;
        if (!definitions.TryGetValue(reactionSkillId, out SkillDefinition? reactionSkill))
        {
            errors.Add(
                $"Skill {skillId} combat_profile.spell_reaction_profile references missing reaction skill {reactionSkillId}."
            );
            return;
        }
        bool hasWeaponDamage = reactionSkill.CombatProfile?.EffectDefinitions.Any(
            effect => effect?.EffectKind == BattleEffectKind.Damage && effect.AddWeaponDice
        ) == true;
        if (
            reactionSkill.SkillTypeKind != SkillTypeKind.Active
            || reactionSkill.CombatProfile == null
            || reactionSkill.CombatProfile.TargetModeKind != BattleTargetMode.Unit
            || !hasWeaponDamage
        )
        {
            errors.Add(
                $"Skill {skillId} combat_profile.spell_reaction_profile reaction skill {reactionSkillId} must be an active unit skill with weapon-dice damage."
            );
        }
    }
}
