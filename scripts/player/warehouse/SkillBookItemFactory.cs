using Godot;

[GlobalClass]
public partial class SkillBookItemFactory : RefCounted
{
    private static readonly GDScript ItemDefScript = GD.Load<GDScript>("res://scripts/player/warehouse/item_def.gd");

    private const string DEFAULT_ICON_PATH = "res://icon.svg";
    private const int DEFAULT_MAX_STACK = 20;

    public static StringName build_item_id_for_skill(StringName skillId)
    {
        return ProgressionDataUtils.to_string_name($"skill_book_{(string)skillId}");
    }

    public Godot.Collections.Dictionary build_generated_item_defs(Godot.Collections.Dictionary skillDefs, Godot.Collections.Dictionary existingItemDefs = null)
    {
        var generatedDefs = new Godot.Collections.Dictionary();
        var existingDefs = existingItemDefs ?? new Godot.Collections.Dictionary();
        foreach (var skillKey in skillDefs.Keys)
        {
            var skillDef = skillDefs[skillKey].AsGodotObject();
            if (skillDef == null) continue;
            if (skillDef.Get("skill_id").AsStringName() == "" || skillDef.Get("learn_source").AsStringName() != "book") continue;
            if (skillDef.Get("display_name").AsString().StripEdges() == "") continue;

            var itemId = build_item_id_for_skill(skillDef.Get("skill_id").AsStringName());
            if (existingDefs.ContainsKey(itemId)) continue;

            var itemDef = ItemDefScript.New().AsGodotObject();
            itemDef.Set("item_id", itemId);
            itemDef.Set("display_name", _build_display_name(skillDef));
            itemDef.Set("description", _build_description(skillDef));
            itemDef.Set("icon", DEFAULT_ICON_PATH);
            itemDef.Set("is_stackable", true);
            itemDef.Set("max_stack", DEFAULT_MAX_STACK);
            itemDef.Set("item_category", ItemDefScript.Get("ITEM_CATEGORY_SKILL_BOOK"));
            itemDef.Set("granted_skill_id", skillDef.Get("skill_id").AsStringName());
            generatedDefs[itemId] = itemDef;
        }
        return generatedDefs;
    }

    private static string _build_display_name(GodotObject skillDef) => $"{skillDef.Get("display_name").AsString().StripEdges()} 技能书";

    private static string _build_description(GodotObject skillDef)
    {
        var skillName = skillDef.Get("display_name").AsString().StripEdges();
        var desc = skillDef.Get("description").AsString();
        var result = $"阅读后使一名队员学会技能：{skillName}。";
        if (desc.Length > 0)
            result += $"\n{desc}";
        return result;
    }
}
