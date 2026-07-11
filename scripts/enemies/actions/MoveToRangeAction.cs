using Godot;

[GlobalClass]
public partial class MoveToRangeAction : EnemyAiAction
{
    private static readonly StringName ScreeningNoneName = "none";
    private static readonly StringName ScreeningRangedAllyName = "ranged_ally";
    private static readonly StringName AiEvaluationInlineDecideName = "inline_decide";
    private static readonly StringName AiEvaluationCandidateRequestName = "candidate_request";
    private const int HpBasisPointsDenominator = 10000;

    [Export]
    public StringName ai_evaluation_mode { get; set; } = AiEvaluationInlineDecideName;

    [Export]
    public StringName target_selector { get; set; } = "nearest_enemy";

    [Export]
    public int desired_min_distance { get; set; } = 1;

    [Export]
    public int desired_max_distance { get; set; } = 1;

    [Export]
    public Godot.Collections.Array<StringName> range_skill_ids { get; set; } = new();

    [Export]
    public StringName screening_mode { get; set; } = ScreeningNoneName;

    [Export]
    public bool enable_aoe_setup_positioning { get; set; } = true;

    [Export]
    public int aoe_setup_min_target_count { get; set; } = 2;

    [Export]
    public int aoe_setup_target_count_weight { get; set; } = 140;

    [Export]
    public int aoe_setup_improvement_weight { get; set; } = 220;

    [Export]
    public int aoe_setup_friendly_fire_penalty { get; set; } = 1000;

    [Export]
    public int screening_min_hp_basis_points { get; set; } = 4000;

    [Export]
    public int screening_ally_min_attack_range { get; set; } = 4;

    [Export]
    public int screening_enemy_max_contact_range { get; set; } = 2;

    [Export]
    public int screening_threat_distance_buffer { get; set; } = 2;

    [Export]
    public int screening_path_bonus { get; set; } = 45;

    public override Godot.Collections.Array<string> ValidateSchema()
    {
        Godot.Collections.Array<string> errors = _collect_base_validation_errors();
        if (target_selector == "")
            errors.Add($"MoveToRangeAction {action_id} is missing target_selector.");
        _append_enemy_focus_target_selector_errors(errors, "MoveToRangeAction", target_selector);
        bool knownScreeningMode =
            screening_mode == ScreeningNoneName || screening_mode == ScreeningRangedAllyName;
        if (!knownScreeningMode)
        {
            errors.Add(
                $"MoveToRangeAction {action_id} screening_mode must be none or ranged_ally."
            );
        }
        if (desired_min_distance < 0)
            errors.Add($"MoveToRangeAction {action_id} desired_min_distance must be >= 0.");
        if (desired_max_distance < desired_min_distance)
        {
            errors.Add(
                $"MoveToRangeAction {action_id} desired_max_distance must be >= desired_min_distance."
            );
        }
        if (
            screening_min_hp_basis_points < 0
            || screening_min_hp_basis_points > HpBasisPointsDenominator
        )
        {
            errors.Add(
                $"MoveToRangeAction {action_id} screening_min_hp_basis_points must be between 0 and 10000."
            );
        }
        if (screening_ally_min_attack_range < 1)
        {
            errors.Add(
                $"MoveToRangeAction {action_id} screening_ally_min_attack_range must be >= 1."
            );
        }
        if (screening_enemy_max_contact_range < 1)
        {
            errors.Add(
                $"MoveToRangeAction {action_id} screening_enemy_max_contact_range must be >= 1."
            );
        }
        if (screening_threat_distance_buffer < 0)
        {
            errors.Add(
                $"MoveToRangeAction {action_id} screening_threat_distance_buffer must be >= 0."
            );
        }
        if (screening_path_bonus < 0)
            errors.Add($"MoveToRangeAction {action_id} screening_path_bonus must be >= 0.");
        if (aoe_setup_min_target_count < 1)
            errors.Add($"MoveToRangeAction {action_id} aoe_setup_min_target_count must be >= 1.");
        if (aoe_setup_target_count_weight < 0)
        {
            errors.Add(
                $"MoveToRangeAction {action_id} aoe_setup_target_count_weight must be >= 0."
            );
        }
        if (aoe_setup_improvement_weight < 0)
        {
            errors.Add(
                $"MoveToRangeAction {action_id} aoe_setup_improvement_weight must be >= 0."
            );
        }
        if (aoe_setup_friendly_fire_penalty < 0)
        {
            errors.Add(
                $"MoveToRangeAction {action_id} aoe_setup_friendly_fire_penalty must be >= 0."
            );
        }
        bool knownEvaluationMode =
            ai_evaluation_mode == AiEvaluationInlineDecideName
            || ai_evaluation_mode == AiEvaluationCandidateRequestName;
        if (!knownEvaluationMode)
        {
            errors.Add(
                $"MoveToRangeAction {action_id} ai_evaluation_mode must be inline_decide or candidate_request."
            );
        }
        if (
            ai_evaluation_mode == AiEvaluationCandidateRequestName
            && screening_mode != ScreeningNoneName
        )
        {
            errors.Add(
                $"MoveToRangeAction {action_id} candidate_request mode does not support screening_mode {screening_mode}."
            );
        }
        return errors;
    }
}
