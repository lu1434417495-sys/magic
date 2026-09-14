using Godot;

internal enum EnemyAiTransitionPredicate
{
    Unknown,
    Always,
    CurrentStateIs,
    SelfHpAtOrBelowBasisPoints,
    AllyHpAtOrBelowBasisPoints,
    NearestEnemyDistanceAtOrBelow,
    HasSkillAffordance,
}

internal static class EnemyAiTransitionKinds
{
    internal static EnemyAiTransitionPredicate ToPredicate(StringName value) =>
        value.ToString() switch
        {
            "always" => EnemyAiTransitionPredicate.Always,
            "current_state_is" => EnemyAiTransitionPredicate.CurrentStateIs,
            "self_hp_at_or_below_basis_points" =>
                EnemyAiTransitionPredicate.SelfHpAtOrBelowBasisPoints,
            "ally_hp_at_or_below_basis_points" =>
                EnemyAiTransitionPredicate.AllyHpAtOrBelowBasisPoints,
            "nearest_enemy_distance_at_or_below" =>
                EnemyAiTransitionPredicate.NearestEnemyDistanceAtOrBelow,
            "has_skill_affordance" => EnemyAiTransitionPredicate.HasSkillAffordance,
            _ => EnemyAiTransitionPredicate.Unknown,
        };

    internal static StringName ToStringName(EnemyAiTransitionPredicate value) =>
        value switch
        {
            EnemyAiTransitionPredicate.Always => "always",
            EnemyAiTransitionPredicate.CurrentStateIs => "current_state_is",
            EnemyAiTransitionPredicate.SelfHpAtOrBelowBasisPoints =>
                "self_hp_at_or_below_basis_points",
            EnemyAiTransitionPredicate.AllyHpAtOrBelowBasisPoints =>
                "ally_hp_at_or_below_basis_points",
            EnemyAiTransitionPredicate.NearestEnemyDistanceAtOrBelow =>
                "nearest_enemy_distance_at_or_below",
            EnemyAiTransitionPredicate.HasSkillAffordance => "has_skill_affordance",
            _ => "",
        };
}
