using Godot;

[GlobalClass]
public partial class UseGroundSkillAction : EnemyAiAction
{
    [Export]
    public Godot.Collections.Array<StringName> skill_ids { get; set; } = new();

    [Export]
    public int minimum_hit_count { get; set; } = 1;

    [Export]
    public bool allow_empty_ground_control { get; set; } = false;

    [Export]
    public bool allow_ground_control_supplement_partial_hits { get; set; } = false;

    [Export]
    public int minimum_ground_control_score { get; set; } = 1;

    [Export]
    public int minimum_ally_threat_hit_count { get; set; } = 0;

    [Export]
    public int maximum_friendly_fire_target_count { get; set; } = 0;

    [Export]
    public bool allow_friendly_lethal { get; set; } = false;

    [Export]
    public int threat_minimum_safe_distance { get; set; } = 0;

    [Export]
    public int threat_safe_distance_margin { get; set; } = 0;

    [Export]
    public int desired_min_distance { get; set; } = -1;

    [Export]
    public int desired_max_distance { get; set; } = -1;

    [Export]
    public StringName distance_reference { get; set; } = "";

    internal EnemyAiDistanceReference DistanceReferenceKind
    {
        get => EnemyAiDistanceReferences.ToKind(distance_reference);
        set => distance_reference = EnemyAiDistanceReferences.ToStringName(value);
    }

    public override Godot.Collections.Array<string> ValidateSchema()
    {
        Godot.Collections.Array<string> errors = _collect_base_validation_errors();
        if (skill_ids.Count == 0)
        {
            errors.Add($"UseGroundSkillAction {action_id} must declare at least one skill_id.");
        }
        if (minimum_hit_count <= 0)
        {
            errors.Add($"UseGroundSkillAction {action_id} minimum_hit_count must be >= 1.");
        }
        if (minimum_ground_control_score <= 0)
        {
            errors.Add(
                $"UseGroundSkillAction {action_id} minimum_ground_control_score must be >= 1."
            );
        }
        if (minimum_ally_threat_hit_count < 0)
        {
            errors.Add(
                $"UseGroundSkillAction {action_id} minimum_ally_threat_hit_count must be >= 0."
            );
        }
        if (maximum_friendly_fire_target_count < 0)
        {
            errors.Add(
                $"UseGroundSkillAction {action_id} maximum_friendly_fire_target_count must be >= 0."
            );
        }
        if (threat_minimum_safe_distance < 0)
        {
            errors.Add(
                $"UseGroundSkillAction {action_id} threat_minimum_safe_distance must be >= 0."
            );
        }
        if (threat_safe_distance_margin < 0)
        {
            errors.Add(
                $"UseGroundSkillAction {action_id} threat_safe_distance_margin must be >= 0."
            );
        }
        if (desired_min_distance < 0)
        {
            errors.Add($"UseGroundSkillAction {action_id} desired_min_distance must be >= 0.");
        }
        if (desired_max_distance < desired_min_distance)
        {
            errors.Add(
                $"UseGroundSkillAction {action_id} desired_max_distance must be >= desired_min_distance."
            );
        }
        if (
            DistanceReferenceKind != EnemyAiDistanceReference.TargetCoord
            && DistanceReferenceKind != EnemyAiDistanceReference.EnemyFrontline
        )
        {
            errors.Add(
                $"UseGroundSkillAction {action_id} distance_reference must be target_coord or enemy_frontline."
            );
        }
        return errors;
    }
}
