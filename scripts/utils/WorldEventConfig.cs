using Godot;

internal enum WorldEventTypeKind
{
    Unknown = 0,
    EnterSubmap,
}

[GlobalClass]
public partial class WorldEventConfig : Resource
{
    private static readonly StringName EventTypeEnterSubmap = "enter_submap";

    [Export]
    public StringName event_id = "";

    [Export]
    public string display_name = "";

    [Export]
    public Vector2I world_coord = Vector2I.Zero;

    [Export]
    public StringName event_type = EventTypeEnterSubmap;

    [Export]
    public StringName target_submap_id = "";

    [Export]
    public StringName discovery_condition_id = "always_true";

    [Export]
    public string prompt_title = "";

    [Export(PropertyHint.MultilineText)]
    public string prompt_text = "";

    internal static StringName ToStringName(WorldEventTypeKind kind) =>
        kind == WorldEventTypeKind.EnterSubmap ? EventTypeEnterSubmap : "";

    internal static WorldEventTypeKind ToEventTypeKind(StringName eventType) =>
        eventType == EventTypeEnterSubmap ? WorldEventTypeKind.EnterSubmap : WorldEventTypeKind.Unknown;
}
