using Godot;

[GlobalClass]
public partial class BattleEncounterDef : Resource
{
    [Export]
    public StringName encounter_id { get; set; } = "";

    [Export]
    public string display_name { get; set; } = "";

    [Export]
    public StringName roster_profile_id { get; set; } = "";

    [Export]
    public BattleObjectiveDef objective { get; set; }

    [Export]
    public BattleEncounterWorldResolutionDef world_resolution { get; set; }

    internal BattleEncounterDefinition ToDefinition() =>
        new(
            encounter_id,
            display_name,
            roster_profile_id,
            objective?.ToDefinition(),
            world_resolution?.ToDefinition()
        );
}
