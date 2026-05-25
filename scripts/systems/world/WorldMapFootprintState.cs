using Godot;

[GlobalClass]
public partial class WorldMapFootprintState : RefCounted
{
    public Vector2I origin = Vector2I.Zero;
    public Vector2I size = Vector2I.Zero;

    public static WorldMapFootprintState create(Vector2I next_origin, Vector2I next_size)
    {
        var state = new WorldMapFootprintState();
        state.origin = next_origin;
        state.size = next_size;
        return state;
    }

    public bool is_empty() => size.X <= 0 || size.Y <= 0;
}
