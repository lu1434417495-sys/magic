using Godot;

[GlobalClass]
public partial class AscensionStageDef : Resource
{
    [Export] public StringName stage_id = "";
    [Export] public StringName ascension_id = "";
    [Export] public string display_name = "";
    [Export(PropertyHint.MultilineText)] public string description = "";
    [Export] public Godot.Collections.Array<Resource> attribute_modifiers = new();
    [Export] public Godot.Collections.Array<StringName> trait_ids = new();
    [Export] public Godot.Collections.Array<Resource> racial_granted_skills = new();
    [Export] public StringName body_size_category_override = "";
    [Export] public Godot.Collections.Array<string> trait_summary = new();
}
