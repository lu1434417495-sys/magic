using Godot;

[GlobalClass]
public partial class MoveToAdvantagePositionAction : EnemyAiAction
{
    private static readonly StringName ModeAdvantage = "advantage";
    private static readonly StringName ModeSurvival = "survival";
    private static readonly StringName ModeHighGround = "high_ground";

    private enum PositioningMode
    {
        Unknown,
        Advantage,
        Survival,
        HighGround,
    }

    [Export]
    public StringName target_selector { get; set; } = "nearest_enemy";

    [Export]
    public int desired_min_distance { get; set; } = 3;

    [Export]
    public int desired_max_distance { get; set; } = 5;

    [Export]
    public Godot.Collections.Array<StringName> range_skill_ids { get; set; } = new();

    [Export]
    public int minimum_safe_distance { get; set; } = 3;

    [Export]
    public int safe_distance_margin { get; set; } = 1;

    [Export]
    public int min_survival_margin_gain_to_escape { get; set; } = 1;

    [Export]
    public int min_distance_progress_when_beyond_band { get; set; }

    [Export]
    public StringName positioning_mode { get; set; } = ModeAdvantage;

    [Export]
    public int high_ground_weight { get; set; } = 60;

    [Export]
    public int safety_weight { get; set; } = 50;

    [Export]
    public int distance_band_weight { get; set; } = 20;

    [Export]
    public int candidate_limit { get; set; } = 96;

    public override Godot.Collections.Array<string> ValidateSchema()
    {
        Godot.Collections.Array<string> errors = _collect_base_validation_errors();
        if (target_selector == "")
            errors.Add($"MoveToAdvantagePositionAction {action_id} is missing target_selector.");
        _append_enemy_focus_target_selector_errors(
            errors,
            "MoveToAdvantagePositionAction",
            target_selector
        );
        if (desired_min_distance < 0)
            errors.Add(
                $"MoveToAdvantagePositionAction {action_id} desired_min_distance must be >= 0."
            );
        if (desired_max_distance < desired_min_distance)
            errors.Add(
                $"MoveToAdvantagePositionAction {action_id} desired_max_distance must be >= desired_min_distance."
            );
        if (minimum_safe_distance < 0)
            errors.Add(
                $"MoveToAdvantagePositionAction {action_id} minimum_safe_distance must be >= 0."
            );
        if (safe_distance_margin < 0)
            errors.Add(
                $"MoveToAdvantagePositionAction {action_id} safe_distance_margin must be >= 0."
            );
        if (min_distance_progress_when_beyond_band < 0)
            errors.Add(
                $"MoveToAdvantagePositionAction {action_id} min_distance_progress_when_beyond_band must be >= 0."
            );
        if (ToPositioningMode(positioning_mode) == PositioningMode.Unknown)
            errors.Add(
                $"MoveToAdvantagePositionAction {action_id} positioning_mode must be advantage, survival, or high_ground."
            );
        return errors;
    }

    private static PositioningMode ToPositioningMode(StringName mode)
    {
        if (mode == ModeAdvantage)
            return PositioningMode.Advantage;
        if (mode == ModeSurvival)
            return PositioningMode.Survival;
        if (mode == ModeHighGround)
            return PositioningMode.HighGround;
        return PositioningMode.Unknown;
    }
}
