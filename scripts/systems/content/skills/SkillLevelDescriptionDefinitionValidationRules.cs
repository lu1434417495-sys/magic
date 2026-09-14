#nullable enable

using System;
using System.Collections.Generic;
using Godot;

internal static class SkillLevelDescriptionDefinitionValidationRules
{
    internal static IReadOnlyList<string> CollectValidationErrors(
        StringName skillId,
        SkillDefinition skill
    )
    {
        var errors = new List<string>();
        if (skill == null)
            return errors;

        string template = (skill.LevelDescriptionTemplate ?? "").StripEdges();
        IReadOnlyDictionary<int, SkillDescriptionVariables> configs =
            skill.LevelDescriptionConfigs;
        bool hasTemplate = template.Length > 0;
        bool hasConfigs = configs.Count > 0;
        if (!hasTemplate && !hasConfigs)
            return errors;
        if (hasTemplate && !hasConfigs)
        {
            errors.Add(
                $"Skill {skillId} level_description_configs must be non-empty when level_description_template is set."
            );
            return errors;
        }
        if (!hasTemplate)
        {
            errors.Add(
                $"Skill {skillId} level_description_template must be non-empty when level_description_configs is set."
            );
            return errors;
        }

        AppendExpressionValidationErrors(skillId, template, errors);

        int lowestDeclaredLevel = int.MaxValue;
        int highestDeclaredLevel = int.MinValue;
        bool hasDynamicMaxLevel = skill.DynamicMaxLevelStatId != "";
        foreach (int level in configs.Keys)
        {
            lowestDeclaredLevel = Math.Min(lowestDeclaredLevel, level);
            highestDeclaredLevel = Math.Max(highestDeclaredLevel, level);
            if (level < 0)
            {
                errors.Add(
                    $"Skill {skillId} level_description_configs key {level} must be a non-negative integer string."
                );
                continue;
            }
            if (!hasDynamicMaxLevel && skill.MaxLevel >= 0 && level > skill.MaxLevel)
            {
                errors.Add(
                    $"Skill {skillId} level_description_configs[{level}] must be <= max_level {skill.MaxLevel}."
                );
            }
        }

        if (lowestDeclaredLevel == int.MaxValue)
            return errors;
        for (int level = lowestDeclaredLevel; level <= highestDeclaredLevel; level++)
        {
            if (!configs.ContainsKey(level))
            {
                errors.Add(
                    $"Skill {skillId} level_description_configs must include level {level}."
                );
            }
        }
        return errors;
    }

    private static void AppendExpressionValidationErrors(
        StringName skillId,
        string template,
        List<string> errors
    )
    {
        int searchIndex = 0;
        while (true)
        {
            int tokenStart = template.IndexOf("{=", searchIndex, StringComparison.Ordinal);
            if (tokenStart < 0)
                return;
            int tokenEnd = template.IndexOf('}', tokenStart + 2);
            if (tokenEnd < 0)
            {
                errors.Add(
                    $"Skill {skillId} level_description_template expression starting at index {tokenStart} is missing a closing brace."
                );
                return;
            }
            string expressionText = template[
                (tokenStart + 2)..tokenEnd
            ].Trim();
            if (expressionText.Length == 0)
            {
                errors.Add(
                    $"Skill {skillId} level_description_template expression at index {tokenStart} must not be empty."
                );
                searchIndex = tokenEnd + 1;
                continue;
            }

            using var expressionScope = new NativeLeaseScope(
                "skill-level-description-definition-validation",
                LifetimeDomain.Request
            );
            Expression expression = expressionScope.Own(
                new Expression(),
                "SkillLevelDescriptionDefinitionValidationRules.expression"
            );
            if (expression.Parse(expressionText, System.Array.Empty<string>()) != Error.Ok)
            {
                errors.Add(
                    $"Skill {skillId} level_description_template expression '{{={expressionText}}}' is invalid: {expression.GetErrorText()}"
                );
            }
            searchIndex = tokenEnd + 1;
        }
    }
}
