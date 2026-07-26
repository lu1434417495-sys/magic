using Godot;

[GlobalClass]
public partial class WorldMapWildSpawnBundle : Resource
{
    [Export]
    public Godot.Collections.Array<Resource> wild_monster_distribution { get; set; } = new();

    internal Godot.Collections.Array<Resource> WildMonsterDistributionProjectionBorrowed =>
        wild_monster_distribution;

    internal WorldMapWildSpawnBundleDefinition ToDefinition(string path) =>
        WorldMapWildSpawnBundleDefinition.FromResource(this, path);
}
