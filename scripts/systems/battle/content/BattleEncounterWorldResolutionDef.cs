using Godot;

[GlobalClass]
public partial class BattleEncounterWorldResolutionDef : Resource
{
    [Export]
    public BattleWorldResolutionMode player_success_mode { get; set; } =
        BattleWorldResolutionMode.Clear;

    [Export]
    public BattleWorldResolutionMode player_failure_mode { get; set; } =
        BattleWorldResolutionMode.Preserve;

    [Export]
    public BattleWorldResolutionMode draw_mode { get; set; } =
        BattleWorldResolutionMode.Preserve;

    [Export]
    public int suppression_steps { get; set; }

    internal BattleEncounterWorldResolutionDefinition ToDefinition() =>
        new(
            player_success_mode,
            player_failure_mode,
            draw_mode,
            suppression_steps
        );
}
