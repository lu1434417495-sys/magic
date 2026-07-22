using Godot;

[GlobalClass]
public partial class BattleEncounterContentSeed : Resource
{
    [Export]
    public Godot.Collections.Array<Resource> battle_encounters { get; set; } = new();
}
