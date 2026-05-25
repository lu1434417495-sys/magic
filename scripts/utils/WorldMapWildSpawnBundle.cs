using Godot;

[GlobalClass]
public partial class WorldMapWildSpawnBundle : Resource
{
    [Export] public Godot.Collections.Array<Resource> wild_monster_distribution { get; set; } = new();
}
