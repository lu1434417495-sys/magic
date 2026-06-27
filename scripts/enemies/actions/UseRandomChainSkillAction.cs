using Godot;

[GlobalClass]
public partial class UseRandomChainSkillAction : EnemyAiAction
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
    public StringName distance_reference { get; set; } =
        EnemyAiDistanceReferences.ToStringName(EnemyAiDistanceReference.CandidatePool);
    internal EnemyAiDistanceReference DistanceReferenceKind
    {
        get => EnemyAiDistanceReferences.ToKind(distance_reference);
        set => distance_reference = EnemyAiDistanceReferences.ToStringName(value);
    }

    [Export]
    public int minimum_candidate_count { get; set; } = 1;

    internal override BattleAiDecision Decide(BattleAiContext context) =>
        new BattleAiRandomChainSkillEvaluator().Evaluate(this, context);

    public override Godot.Collections.Array<string> ValidateSchema()
    {
        Godot.Collections.Array<string> errors = _collect_base_validation_errors();
        if (skill_ids.Count == 0)
            errors.Add($"UseRandomChainSkillAction {action_id} must declare at least one skill_id.");
        if (target_selector == "")
            errors.Add($"UseRandomChainSkillAction {action_id} is missing target_selector.");
        if (desired_min_distance < 0)
            errors.Add($"UseRandomChainSkillAction {action_id} desired_min_distance must be >= 0.");
        if (desired_max_distance < desired_min_distance)
        {
            errors.Add(
                $"UseRandomChainSkillAction {action_id} desired_max_distance must be >= desired_min_distance."
            );
        }
        if (
            DistanceReferenceKind != EnemyAiDistanceReference.CandidatePool
            && DistanceReferenceKind != EnemyAiDistanceReference.EnemyFrontline
        )
        {
            errors.Add(
                $"UseRandomChainSkillAction {action_id} distance_reference must be candidate_pool or enemy_frontline."
            );
        }
        if (minimum_candidate_count < 1)
        {
            errors.Add(
                $"UseRandomChainSkillAction {action_id} minimum_candidate_count must be >= 1."
            );
        }
        return errors;
    }
}
