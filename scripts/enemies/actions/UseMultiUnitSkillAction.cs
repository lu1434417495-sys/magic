using Godot;

[GlobalClass]
public partial class UseMultiUnitSkillAction : EnemyAiAction
{
    [Export]
    public Godot.Collections.Array<StringName> skill_ids { get; set; } = new();

    [Export]
    public StringName target_selector { get; set; } = "nearest_enemy";

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

    [Export]
    public int candidate_pool_limit { get; set; } = 6;

    [Export]
    public int candidate_group_limit { get; set; } = 12;

    public override Godot.Collections.Array<string> ValidateSchema()
    {
        Godot.Collections.Array<string> errors = _collect_base_validation_errors();
        if (skill_ids.Count == 0)
            errors.Add($"UseMultiUnitSkillAction {action_id} must declare at least one skill_id.");
        if (target_selector == "")
            errors.Add($"UseMultiUnitSkillAction {action_id} is missing target_selector.");
        if (desired_min_distance < 0)
            errors.Add($"UseMultiUnitSkillAction {action_id} desired_min_distance must be >= 0.");
        if (desired_max_distance < desired_min_distance)
        {
            errors.Add(
                $"UseMultiUnitSkillAction {action_id} desired_max_distance must be >= desired_min_distance."
            );
        }
        if (
            DistanceReferenceKind != EnemyAiDistanceReference.TargetUnit
            && DistanceReferenceKind != EnemyAiDistanceReference.EnemyFrontline
        )
        {
            errors.Add(
                $"UseMultiUnitSkillAction {action_id} distance_reference must be target_unit or enemy_frontline."
            );
        }
        if (candidate_pool_limit <= 0)
            errors.Add($"UseMultiUnitSkillAction {action_id} candidate_pool_limit must be > 0.");
        if (candidate_group_limit <= 0)
            errors.Add($"UseMultiUnitSkillAction {action_id} candidate_group_limit must be > 0.");
        return errors;
    }
}
