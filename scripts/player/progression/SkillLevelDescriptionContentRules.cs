using Godot;

[GlobalClass]
public partial class SkillLevelDescriptionContentRules : RefCounted
{
    public static void append_validation_errors(Godot.Collections.Array<string> errors, StringName skillId, GodotObject skillDef)
    {
        if (skillDef == null) return;
        string template = skillDef.Get("level_description_template").AsString().StripEdges();
        var configs = skillDef.Get("level_description_configs").AsGodotDictionary();
        bool hasTemplate = template.Length > 0;
        bool hasConfigs = configs.Count > 0;

        if (!hasTemplate && !hasConfigs) return;
        if (hasTemplate && !hasConfigs) { errors.Add($"Skill {skillId} level_description_configs must be non-empty when level_description_template is set."); return; }
        if (!hasTemplate && hasConfigs) { errors.Add($"Skill {skillId} level_description_template must be non-empty when level_description_configs is set."); return; }

        var validLevels = new Godot.Collections.Array<int>();
        int lowestDeclaredLevel = -1;
        int highestDeclaredLevel = -1;
        bool hasDynamicMaxLevel = skillDef.Get("dynamic_max_level_stat_id").AsStringName() != "";
        int maxLevel = skillDef.Get("max_level").AsInt32();

        foreach (var levelKey in configs.Keys)
        {
            int parsedLevel = _parse_level_key(levelKey);
            if (parsedLevel < 0) { errors.Add($"Skill {skillId} level_description_configs key {levelKey} must be a non-negative integer string."); continue; }
            lowestDeclaredLevel = lowestDeclaredLevel < 0 ? parsedLevel : Mathf.Min(lowestDeclaredLevel, parsedLevel);
            highestDeclaredLevel = Mathf.Max(highestDeclaredLevel, parsedLevel);
            validLevels.Add(parsedLevel);
            if (configs[levelKey].VariantType != Variant.Type.Dictionary)
                errors.Add($"Skill {skillId} level_description_configs[{parsedLevel}] must be a Dictionary.");
            if (!hasDynamicMaxLevel && maxLevel >= 0 && parsedLevel > maxLevel)
                errors.Add($"Skill {skillId} level_description_configs[{parsedLevel}] must be <= max_level {maxLevel}.");
        }

        if (validLevels.Count == 0) return;

        var declaredLevels = new Godot.Collections.Dictionary();
        foreach (int level in validLevels)
            declaredLevels[level] = true;
        for (int expectedLevel = lowestDeclaredLevel; expectedLevel <= highestDeclaredLevel; expectedLevel++)
        {
            if (!declaredLevels.ContainsKey(expectedLevel))
                errors.Add($"Skill {skillId} level_description_configs must include level {expectedLevel}.");
        }
    }

    private static int _parse_level_key(Variant levelKey)
    {
        if (levelKey.VariantType != Variant.Type.String) return -1;
        string text = levelKey.AsString().StripEdges();
        if (text.Length == 0 || !int.TryParse(text, out int parsedLevel)) return -1;
        if (parsedLevel < 0) return -1;
        if (parsedLevel.ToString() != text) return -1;
        return parsedLevel;
    }
}
