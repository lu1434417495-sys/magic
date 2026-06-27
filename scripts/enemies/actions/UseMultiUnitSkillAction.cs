using System.Collections.Generic;
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

    internal override BattleAiDecision Decide(BattleAiContext context) =>
        new BattleAiMultiUnitSkillEvaluator().Evaluate(this, context);

    protected List<CombatCastVariantDefinition> _get_multi_unit_cast_variants(
        BattleAiContext context,
        SkillDefinition skillDefinition
    )
    {
        var result = new List<CombatCastVariantDefinition>();
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (combatProfile == null)
            return result;
        if (combatProfile.CastVariants.Count == 0)
        {
            result.Add(null);
            return result;
        }
        int skillLevel = context?.unit_state != null
            ? _get_skill_level(context.unit_state, skillDefinition.SkillId)
            : 0;
        SkillEffectiveCombatDefinition effectiveDefinition =
            context?.skill_catalog?.GetEffectiveCombatDefinition(skillDefinition.SkillId, skillLevel)
            ?? SkillEffectiveCombatDefinition.BuildUncached(skillDefinition, skillLevel);
        foreach (CombatCastVariantDefinition castVariant in effectiveDefinition.UnlockedCastVariants)
        {
            if (castVariant != null)
                result.Add(castVariant);
        }
        return result;
    }

    protected Dictionary<string, object> _build_position_metadata(
        BattleAiContext context,
        IReadOnlyList<BattleUnitState> targetGroup,
        SkillDefinition skillDefinition
    )
    {
        Dictionary<string, object> metadata = _resolve_desired_distance_contract_typed(
            context,
            skillDefinition
        );
        if (DistanceReferenceKind == EnemyAiDistanceReference.TargetUnit)
        {
            BattleUnitState primaryTarget = targetGroup.Count > 0 ? targetGroup[0] : null;
            if (primaryTarget != null)
                metadata["position_target_unit_id"] = primaryTarget.unit_id;
            else
                metadata["position_objective_kind"] = "none";
        }
        else if (DistanceReferenceKind == EnemyAiDistanceReference.EnemyFrontline)
        {
            BattleUnitState frontline = _resolve_enemy_frontline_unit(context);
            if (frontline != null)
                metadata["position_target_unit_id"] = frontline.unit_id;
            else
                metadata["position_objective_kind"] = "none";
        }
        else
        {
            metadata["position_objective_kind"] = "none";
        }
        return metadata;
    }

    private BattleUnitState _resolve_enemy_frontline_unit(BattleAiContext context)
    {
        List<BattleUnitState> targets = _sort_target_units_typed(
            context,
            "enemy",
            "nearest_enemy"
        );
        return targets.Count > 0 ? targets[0] : null;
    }

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
