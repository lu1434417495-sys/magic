using Godot;

[GlobalClass]
public partial class UseUnitSkillAction : EnemyAiAction
{
    [Export]
    public Godot.Collections.Array<StringName> skill_ids = new();

    [Export]
    public StringName target_selector = "nearest_enemy";

    [Export]
    public int minimum_effective_target_count = 1;

    [Export]
    public int maximum_friendly_fire_target_count = 0;

    [Export]
    public bool allow_friendly_lethal = false;

    [Export]
    public int desired_min_distance = -1;

    [Export]
    public int desired_max_distance = -1;

    [Export]
    public StringName distance_reference = "";
    internal EnemyAiDistanceReference DistanceReferenceKind
    {
        get => EnemyAiDistanceReferences.ToKind(distance_reference);
        set => distance_reference = EnemyAiDistanceReferences.ToStringName(value);
    }

    public override Godot.Collections.Array<string> ValidateSchema()
    {
        var errors = _collect_base_validation_errors();
        if (skill_ids.Count == 0)
            errors.Add($"UseUnitSkillAction {action_id} must declare at least one skill_id.");
        if (target_selector == (StringName)"")
            errors.Add($"UseUnitSkillAction {action_id} is missing target_selector.");
        if (minimum_effective_target_count < 0)
            errors.Add(
                $"UseUnitSkillAction {action_id} minimum_effective_target_count must be >= 0."
            );
        if (maximum_friendly_fire_target_count < 0)
            errors.Add(
                $"UseUnitSkillAction {action_id} maximum_friendly_fire_target_count must be >= 0."
            );
        if (desired_min_distance < 0)
            errors.Add($"UseUnitSkillAction {action_id} desired_min_distance must be >= 0.");
        if (desired_max_distance < desired_min_distance)
            errors.Add(
                $"UseUnitSkillAction {action_id} desired_max_distance must be >= desired_min_distance."
            );
        if (
            DistanceReferenceKind != EnemyAiDistanceReference.TargetUnit
            && DistanceReferenceKind != EnemyAiDistanceReference.EnemyFrontline
        )
            errors.Add(
                $"UseUnitSkillAction {action_id} distance_reference must be target_unit or enemy_frontline."
            );
        return errors;
    }
}
