using Godot;

[GlobalClass]
public partial class AgeStageRule : Resource
{
    [Export] public StringName stage_id = "";
    [Export] public string display_name = "";
    [Export(PropertyHint.MultilineText)] public string description = "";
    [Export] public Godot.Collections.Array<Resource> attribute_modifiers = new();
    [Export] public Godot.Collections.Array<StringName> trait_ids = new();
    [Export] public Godot.Collections.Array<string> trait_summary = new();
    [Export] public bool selectable_in_creation = true;
    [Export] public bool reachable_by_aging = true;
}
