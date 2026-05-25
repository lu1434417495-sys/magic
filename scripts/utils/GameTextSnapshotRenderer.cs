using Godot;

[GlobalClass]
public partial class GameTextSnapshotRenderer : RefCounted
{
    public string render_snapshot(Godot.Collections.Dictionary runtimeState) { return ""; }
    public static string format_coord(Vector2I coord) => $"({coord.X}, {coord.Y})";
}
