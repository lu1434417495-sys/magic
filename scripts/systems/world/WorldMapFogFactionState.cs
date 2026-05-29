using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class WorldMapFogFactionState : RefCounted
{
    public GDictionary visible_now = new();
    public GDictionary explored = new();

    public void clear_visible()
    {
        visible_now.Clear();
    }

    public void mark_visible(Vector2I coord)
    {
        visible_now[coord] = true;
        explored[coord] = true;
    }

    public bool is_visible(Vector2I coord)
    {
        return visible_now.ContainsKey(coord);
    }

    public bool is_explored(Vector2I coord)
    {
        return explored.ContainsKey(coord);
    }
}
