using Godot;

[GlobalClass]
public partial class CharacterProgressionDelta : RefCounted
{
    public StringName member_id = "";
    public Godot.Collections.Array<StringName> leveled_skill_ids = new();
    public Godot.Collections.Array<StringName> granted_skill_ids = new();
    public Godot.Collections.Array<StringName> changed_profession_ids = new();
    public int character_level_before;
    public int character_level_after;
    public Godot.Collections.Array pending_profession_choices = new();
    public bool needs_promotion_modal;
    public Godot.Collections.Array<Godot.Collections.Dictionary> mastery_changes = new();
    public Godot.Collections.Array<StringName> unlocked_achievement_ids = new();
    public Godot.Collections.Array<Godot.Collections.Dictionary> knowledge_changes = new();
    public Godot.Collections.Array<Godot.Collections.Dictionary> attribute_changes = new();
}
