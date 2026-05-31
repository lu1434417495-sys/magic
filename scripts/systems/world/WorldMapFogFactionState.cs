using System.Collections.Generic;
using Godot;

public sealed class WorldMapFogFactionState
{
    private readonly HashSet<Vector2I> _visibleNow = new();
    private readonly HashSet<Vector2I> _explored = new();

    public IEnumerable<Vector2I> ExploredCoords => _explored;

    public void ClearVisible()
    {
        _visibleNow.Clear();
    }

    public void MarkVisible(Vector2I coord)
    {
        _visibleNow.Add(coord);
        _explored.Add(coord);
    }

    public void MarkExplored(Vector2I coord)
    {
        _explored.Add(coord);
    }

    public bool IsVisible(Vector2I coord)
    {
        return _visibleNow.Contains(coord);
    }

    public bool IsExplored(Vector2I coord)
    {
        return _explored.Contains(coord);
    }
}
