using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public static class SkillLevelDescriptionContentRules
{
    public static List<string> CollectValidationErrors(StringName skillId, SkillDef skillDef)
    {
        var errors = new List<string>();
        if (skillDef == null)
            return errors;

        string template = skillDef.level_description_template.StripEdges();
        List<LevelDescriptionConfigEntry> configs = ReadConfigEntries(
            skillDef.level_description_configs
        );
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

        var validLevels = new List<int>();
        int lowestDeclaredLevel = -1;
        int highestDeclaredLevel = -1;
        bool hasDynamicMaxLevel = skillDef.dynamic_max_level_stat_id != "";
        int maxLevel = skillDef.max_level;

        foreach (LevelDescriptionConfigEntry configEntry in configs)
        {
            int parsedLevel = ParseLevelKey(configEntry);
            if (parsedLevel < 0)
            {
                errors.Add(
                    $"Skill {skillId} level_description_configs key {configEntry.DisplayKey} must be a non-negative integer string."
                );
                continue;
            }
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

    private static List<LevelDescriptionConfigEntry> ReadConfigEntries(GDictionary configs)
    {
        var result = new List<LevelDescriptionConfigEntry>();
        if (configs == null)
            return result;

        foreach (Variant rawLevelKey in configs.Keys)
        {
            bool keyIsString = rawLevelKey.VariantType == Variant.Type.String;
            string keyText = keyIsString ? rawLevelKey.AsString().StripEdges() : "";
            bool valueIsDictionary =
                configs[rawLevelKey].VariantType == Variant.Type.Dictionary;
            result.Add(
                new LevelDescriptionConfigEntry(
                    rawLevelKey.ToString(),
                    keyIsString,
                    keyText,
                    valueIsDictionary
                )
            );
        }
        return result;
    }

    private static int ParseLevelKey(LevelDescriptionConfigEntry configEntry)
    {
        if (!configEntry.KeyIsString)
            return -1;
        string text = configEntry.KeyText;
        if (text.Length == 0 || !int.TryParse(text, out int parsedLevel))
            return -1;
        if (parsedLevel < 0)
            return -1;
        if (parsedLevel.ToString() != text)
            return -1;
        return parsedLevel;
    }

    private readonly record struct LevelDescriptionConfigEntry(
        string DisplayKey,
        bool KeyIsString,
        string KeyText,
        bool ValueIsDictionary
    );
}
