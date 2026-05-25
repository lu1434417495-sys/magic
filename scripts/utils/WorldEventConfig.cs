using Godot;

[GlobalClass]
public partial class WorldEventConfig : Resource
{
    public static readonly StringName EVENT_TYPE_ENTER_SUBMAP = "enter_submap";

    [Export] public StringName event_id = "";
    [Export] public string display_name = "";
    [Export] public Vector2I world_coord = Vector2I.Zero;
    [Export] public StringName event_type = EVENT_TYPE_ENTER_SUBMAP;
    [Export] public StringName target_submap_id = "";
    [Export] public StringName discovery_condition_id = "always_true";
    [Export] public string prompt_title = "";
    [Export(PropertyHint.MultilineText)] public string prompt_text = "";
}
