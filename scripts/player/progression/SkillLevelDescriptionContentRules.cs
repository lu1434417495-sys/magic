using System;
using System.Collections.Generic;
using Godot;

public static class SkillLevelDescriptionContentRules
{
    public static List<string> CollectValidationErrors(StringName skillId, SkillDef skillDef)
    {
        var errors = new List<string>();
        if (skillDef == null)
            return errors;

        string template = skillDef.level_description_template.StripEdges();
        IReadOnlyList<SkillDef.LevelDescriptionConfigEntryData> configs =
            skillDef.LevelDescriptionConfigEntriesTyped;
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
        if (!hasTemplate && hasConfigs)
        {
            errors.Add(
                $"Skill {skillId} level_description_template must be non-empty when level_description_configs is set."
            );
            return errors;
        }

        AppendExpressionValidationErrors(skillId, template, errors);

        var validLevels = new List<int>();
        int lowestDeclaredLevel = -1;
        int highestDeclaredLevel = -1;
        bool hasDynamicMaxLevel = skillDef.dynamic_max_level_stat_id != "";
        int maxLevel = skillDef.max_level;

        foreach (SkillDef.LevelDescriptionConfigEntryData configEntry in configs)
        {
            if (!configEntry.HasParsedLevelKey)
            {
                errors.Add(
                    $"Skill {skillId} level_description_configs key {configEntry.DisplayKey} must be a non-negative integer string."
                );
                continue;
            }
            int parsedLevel = configEntry.Level;
            lowestDeclaredLevel =
                lowestDeclaredLevel < 0 ? parsedLevel : Mathf.Min(lowestDeclaredLevel, parsedLevel);
            highestDeclaredLevel = Mathf.Max(highestDeclaredLevel, parsedLevel);
            validLevels.Add(parsedLevel);
            if (!configEntry.ValueIsDictionary)
                errors.Add(
                    $"Skill {skillId} level_description_configs[{parsedLevel}] must be a Dictionary."
                );
            if (!hasDynamicMaxLevel && maxLevel >= 0 && parsedLevel > maxLevel)
                errors.Add(
                    $"Skill {skillId} level_description_configs[{parsedLevel}] must be <= max_level {maxLevel}."
                );
        }

        if (validLevels.Count == 0)
            return errors;

        var declaredLevels = new HashSet<int>();
        foreach (int level in validLevels)
            declaredLevels.Add(level);
        for (
            int expectedLevel = lowestDeclaredLevel;
            expectedLevel <= highestDeclaredLevel;
            expectedLevel++
        )
        {
            if (!declaredLevels.Contains(expectedLevel))
                errors.Add(
                    $"Skill {skillId} level_description_configs must include level {expectedLevel}."
                );
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

            string expressionText = template.Substring(
                tokenStart + 2,
                tokenEnd - tokenStart - 2
            ).Trim();
            if (expressionText.Length == 0)
            {
                errors.Add(
                    $"Skill {skillId} level_description_template expression at index {tokenStart} must not be empty."
                );
                searchIndex = tokenEnd + 1;
                continue;
            }

            using var expressionScope = new NativeLeaseScope(
                "skill-level-description-content-expression",
                LifetimeDomain.Request
            );
            Expression expression = expressionScope.Own(
                new Expression(),
                "SkillLevelDescriptionContentRules.expression"
            );
            if (expression.Parse(expressionText, Array.Empty<string>()) != Error.Ok)
            {
                errors.Add(
                    $"Skill {skillId} level_description_template expression '{{={expressionText}}}' is invalid: {expression.GetErrorText()}"
                );
            }
            searchIndex = tokenEnd + 1;
        }
    }
}
