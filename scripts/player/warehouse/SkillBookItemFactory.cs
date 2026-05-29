using Godot;

[GlobalClass]
public partial class SkillBookItemFactory : RefCounted
{
    private const string DEFAULT_ICON_PATH = "res://icon.svg";
    private const int DEFAULT_MAX_STACK = 20;

    public static StringName build_item_id_for_skill(StringName skillId) =>
        ProgressionDataUtils.to_string_name($"skill_book_{skillId}");

    public Godot.Collections.Dictionary build_generated_item_defs(
        Godot.Collections.Dictionary skillDefs
    ) => build_generated_item_defs(skillDefs, new Godot.Collections.Dictionary());

    public Godot.Collections.Dictionary build_generated_item_defs(
        Godot.Collections.Dictionary skillDefs,
        Godot.Collections.Dictionary existingItemDefs
    )
    {
        var generatedDefs = new Godot.Collections.Dictionary();
        existingItemDefs ??= new Godot.Collections.Dictionary();

        foreach (var skillKey in skillDefs.Keys)
        {
            var skillDef = skillDefs[skillKey].AsGodotObject() as SkillDef;
            if (skillDef == null)
                continue;
            if (skillDef.skill_id == "" || skillDef.learn_source != "book")
                continue;
            if (skillDef.display_name.StripEdges().Length == 0)
                continue;

            var itemId = build_item_id_for_skill(skillDef.skill_id);
            if (existingItemDefs.ContainsKey(itemId))
                continue;

            generatedDefs[itemId] = new ItemDef
            {
                item_id = itemId,
                display_name = _build_display_name(skillDef),
                description = _build_description(skillDef),
                icon = DEFAULT_ICON_PATH,
                is_stackable = true,
                max_stack = DEFAULT_MAX_STACK,
                item_category = ItemDef.ITEM_CATEGORY_SKILL_BOOK(),
                granted_skill_id = skillDef.skill_id,
            };
        }

        return generatedDefs;
    }

    private static string _build_display_name(SkillDef skillDef) =>
        $"{skillDef.display_name.StripEdges()} 技能书";

    private static string _build_description(SkillDef skillDef)
    {
        var skillName = skillDef.display_name.StripEdges();
        var result = $"阅读后使一名队员学会技能：{skillName}。";
        if (skillDef.description.Length > 0)
            result += $"\n{skillDef.description}";
        return result;
    }
}
