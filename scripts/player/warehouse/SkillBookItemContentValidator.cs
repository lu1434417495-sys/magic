using Godot;

[GlobalClass]
public partial class SkillBookItemContentValidator : RefCounted
{
    private static readonly GDScript ItemDefScript = GD.Load<GDScript>("res://scripts/player/warehouse/item_def.gd");

    public static Godot.Collections.Array<string> validate(Godot.Collections.Dictionary itemDefs, Godot.Collections.Dictionary skillDefs)
    {
        var errors = new Godot.Collections.Array<string>();
        var normalizedItemDefs = _normalize_item_defs(itemDefs);
        var normalizedSkillDefs = _normalize_skill_defs(skillDefs);
        _append_skill_book_reference_errors(errors, normalizedItemDefs, normalizedSkillDefs);
        _append_canonical_id_collision_errors(errors, normalizedItemDefs, normalizedSkillDefs);
        return errors;
    }

    private static void _append_skill_book_reference_errors(Godot.Collections.Array<string> errors, Godot.Collections.Dictionary itemDefs, Godot.Collections.Dictionary skillDefs)
    {
        foreach (var itemKey in ProgressionDataUtils.sorted_string_keys(itemDefs))
        {
            var itemId = new StringName(itemKey);
            if (!itemDefs.ContainsKey(itemId)) continue;
            var itemDef = itemDefs[itemId].AsGodotObject();
            if (itemDef == null) continue;
            if (itemDef.Call("get_item_category_normalized").AsStringName() != ItemDefScript.Get("ITEM_CATEGORY_SKILL_BOOK").AsStringName()) continue;
            if (itemDef.Get("granted_skill_id").AsStringName() == "") continue;

            var skillDef = skillDefs.ContainsKey(itemDef.Get("granted_skill_id").AsStringName()) ? skillDefs[itemDef.Get("granted_skill_id").AsStringName()].AsGodotObject() : null;
            if (skillDef == null) { errors.Add($"Skill book item {itemDef.Get("item_id")} references missing skill {itemDef.Get("granted_skill_id")}."); continue; }
            if (skillDef.Get("learn_source").AsStringName() != "book")
                errors.Add($"Skill book item {itemDef.Get("item_id")} granted_skill_id {itemDef.Get("granted_skill_id")} learn_source must be book, got {skillDef.Get("learn_source")}.");
        }
    }

    private static void _append_canonical_id_collision_errors(Godot.Collections.Array<string> errors, Godot.Collections.Dictionary itemDefs, Godot.Collections.Dictionary skillDefs)
    {
        foreach (var skillKey in ProgressionDataUtils.sorted_string_keys(skillDefs))
        {
            var skillId = new StringName(skillKey);
            if (!skillDefs.ContainsKey(skillId)) continue;
            var skillDef = skillDefs[skillId].AsGodotObject();
            if (skillDef == null || skillDef.Get("skill_id").AsStringName() == "" || skillDef.Get("learn_source").AsStringName() != "book") continue;

            var canonicalItemId = SkillBookItemFactory.build_item_id_for_skill(skillDef.Get("skill_id").AsStringName());
            if (!itemDefs.ContainsKey(canonicalItemId)) continue;
            var occupyingItem = itemDefs[canonicalItemId].AsGodotObject();
            if (occupyingItem == null) continue;
            if (occupyingItem.Call("get_item_category_normalized").AsStringName() != ItemDefScript.Get("ITEM_CATEGORY_SKILL_BOOK").AsStringName())
            { errors.Add($"Item {canonicalItemId} occupies generated skill book id for skill {skillDef.Get("skill_id")} but item_category must be skill_book."); continue; }
            if (occupyingItem.Get("granted_skill_id").AsStringName() != skillDef.Get("skill_id").AsStringName())
                errors.Add($"Skill book item {canonicalItemId} occupies generated skill book id for skill {skillDef.Get("skill_id")} but grants {occupyingItem.Get("granted_skill_id")}.");
        }
    }

    private static Godot.Collections.Dictionary _normalize_item_defs(Godot.Collections.Dictionary itemDefs)
    {
        var normalized = new Godot.Collections.Dictionary();
        foreach (var key in itemDefs.Keys)
        {
            var itemDef = itemDefs[key].AsGodotObject();
            if (itemDef == null || itemDef.Get("item_id").AsStringName() == "") continue;
            normalized[itemDef.Get("item_id").AsStringName()] = itemDef;
        }
        return normalized;
    }

    private static Godot.Collections.Dictionary _normalize_skill_defs(Godot.Collections.Dictionary skillDefs)
    {
        var normalized = new Godot.Collections.Dictionary();
        foreach (var key in skillDefs.Keys)
        {
            var skillDef = skillDefs[key].AsGodotObject();
            if (skillDef == null || skillDef.Get("skill_id").AsStringName() == "") continue;
            normalized[skillDef.Get("skill_id").AsStringName()] = skillDef;
        }
        return normalized;
    }
}
