using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class BattleUnitFactoryRuntime : RefCounted
{
    public GodotObject get_character_gateway() => null;

    public GDictionary get_skill_defs() => new();

    public GodotObject get_terrain_generator() => null;

    public GodotObject get_grid_service() => null;

    public int get_min_battle_surface_height() => 4;
}
