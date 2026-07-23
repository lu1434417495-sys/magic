using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class WildEncounterRosterUnitEntryDef : Resource
{
    [Export]
    public StringName template_id { get; set; } = "";

    [Export]
    public StringName actor_id { get; set; } = "";

    [Export]
    public int count { get; set; } = 1;

    [Export]
    public string display_name { get; set; } = "";

    internal WildEncounterRosterUnitEntryDefinition ToDefinition() =>
        new(template_id, count, display_name, actor_id);
}
