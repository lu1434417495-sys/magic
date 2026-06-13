using Godot;

[GlobalClass]
public partial class WildEncounterRosterStageDef : Resource
{
    [Export]
    public int stage { get; set; }

    [Export]
    public Godot.Collections.Array<WildEncounterRosterUnitEntryDef> unit_entries { get; set; } = new();
}
