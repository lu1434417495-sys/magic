using Godot;

public sealed class WorldMapFootprintState
{
    public readonly Vector2I Origin;
    public readonly Vector2I Size;

    public WorldMapFootprintState(Vector2I origin, Vector2I size)
    {
        Origin = origin;
        Size = size;
    }
}
