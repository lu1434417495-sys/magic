using Godot;

[GlobalClass]
public partial class WildEncounterRosterStageDef : Resource
{
    [Export]
    public int stage { get; set; }

    [Export]
    public Godot.Collections.Array<WildEncounterRosterUnitEntryDef> unit_entries { get; set; } = new();

    internal WildEncounterRosterStageDefinition ToDefinition()
    {
        var entries = new System.Collections.Generic.List<WildEncounterRosterUnitEntryDefinition>();
        foreach (WildEncounterRosterUnitEntryDef entry in unit_entries)
        {
            if (entry != null)
                entries.Add(entry.ToDefinition());
        }
        return new WildEncounterRosterStageDefinition(stage, entries);
    }
}
