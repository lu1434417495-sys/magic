using Godot;

[GlobalClass]
public partial class BattleTerrainGenerator : RefCounted
{
    private RandomNumberGenerator _rng = new RandomNumberGenerator();
    public Godot.Collections.Dictionary generate(GodotObject encounterAnchorOrContext, int seed = 0, Godot.Collections.Dictionary context = null) { return new Godot.Collections.Dictionary(); }
}
