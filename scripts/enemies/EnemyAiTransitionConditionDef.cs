using System.Collections.Generic;
using Godot;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

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

[Tool]
[GlobalClass]
public partial class EnemyAiTransitionConditionDef : Resource
{
    private const int HpBasisPointsDenominator = 10000;
    private static readonly StringName PredicateAlways = "always";
    private static readonly StringName PredicateCurrentStateIs = "current_state_is";
    private static readonly StringName PredicateSelfHpAtOrBelow =
        "self_hp_at_or_below_basis_points";
    private static readonly StringName PredicateAllyHpAtOrBelow =
        "ally_hp_at_or_below_basis_points";
    private static readonly StringName PredicateNearestEnemyDistanceAtOrBelow =
        "nearest_enemy_distance_at_or_below";
    private static readonly StringName PredicateHasSkillAffordance = "has_skill_affordance";

    [Export]
    public StringName predicate = "";
    internal EnemyAiTransitionPredicate PredicateKind
    {
        get => ToPredicate(predicate);
        set => predicate = ToStringName(value);
    }

    [Export]
    public int basis_points = -1;

    [Export]
    public int max_distance = -1;

    [Export]
    public GStringNameArray state_ids = new();

    [Export]
    public GStringNameArray affordances = new();

    internal static int HpBasisPointsDenominatorValue => HpBasisPointsDenominator;

    internal static EnemyAiTransitionPredicate ToPredicate(StringName value)
    {
        return value.ToString() switch
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
    }

    internal static StringName ToStringName(EnemyAiTransitionPredicate value) =>
        value switch
        {
            EnemyAiTransitionPredicate.Always => PredicateAlways,
            EnemyAiTransitionPredicate.CurrentStateIs => PredicateCurrentStateIs,
            EnemyAiTransitionPredicate.SelfHpAtOrBelowBasisPoints =>
                PredicateSelfHpAtOrBelow,
            EnemyAiTransitionPredicate.AllyHpAtOrBelowBasisPoints =>
                PredicateAllyHpAtOrBelow,
            EnemyAiTransitionPredicate.NearestEnemyDistanceAtOrBelow =>
                PredicateNearestEnemyDistanceAtOrBelow,
            EnemyAiTransitionPredicate.HasSkillAffordance => PredicateHasSkillAffordance,
            _ => "",
        };

    internal List<string> ValidateSchema(
        string ownerLabel,
        IReadOnlySet<StringName> declaredStateIds = null
    )
    {
        var errors = new List<string>();
        if (predicate == (StringName)"")
        {
            errors.Add($"{ownerLabel} transition condition is missing predicate.");
            return errors;
        }

        EnemyAiTransitionPredicate predicateKind = PredicateKind;
        if (predicateKind == EnemyAiTransitionPredicate.Unknown)
        {
            errors.Add(
                $"{ownerLabel} transition condition uses unsupported predicate {predicate}."
            );
            return errors;
        }

        if (predicateKind == EnemyAiTransitionPredicate.Always)
            return errors;
        if (predicateKind == EnemyAiTransitionPredicate.CurrentStateIs)
        {
            if (state_ids.Count == 0)
                errors.Add($"{ownerLabel} current_state_is condition must declare state_ids.");
            foreach (var state_id in state_ids)
            {
                if (state_id == (StringName)"")
                    errors.Add(
                        $"{ownerLabel} current_state_is condition contains empty state_id."
                    );
                else if (
                    declaredStateIds != null
                    && declaredStateIds.Count > 0
                    && !declaredStateIds.Contains(state_id)
                )
                    errors.Add(
                        $"{ownerLabel} current_state_is condition references undeclared state_id {state_id}."
                    );
            }
        }
        else if (
            predicateKind == EnemyAiTransitionPredicate.SelfHpAtOrBelowBasisPoints
            || predicateKind == EnemyAiTransitionPredicate.AllyHpAtOrBelowBasisPoints
        )
        {
            if (basis_points < 0 || basis_points > HpBasisPointsDenominator)
                errors.Add(
                    $"{ownerLabel} {predicate} condition basis_points must be within [0, 10000]."
                );
        }
        else if (predicateKind == EnemyAiTransitionPredicate.NearestEnemyDistanceAtOrBelow)
        {
            if (max_distance < 0)
                errors.Add(
                    $"{ownerLabel} nearest_enemy_distance_at_or_below condition max_distance must be >= 0."
                );
        }
        else if (predicateKind == EnemyAiTransitionPredicate.HasSkillAffordance)
        {
            if (affordances.Count == 0)
                errors.Add(
                    $"{ownerLabel} has_skill_affordance condition must declare affordances."
                );
            foreach (var affordance in affordances)
            {
                if (affordance == (StringName)"")
                    errors.Add(
                        $"{ownerLabel} has_skill_affordance condition contains empty affordance."
                    );
            }
        }
        return errors;
    }

    internal string ToSignature()
    {
        return $"{predicate}(bp={basis_points},dist={max_distance},states={string.Join(",", StringNameArrayToStrings(state_ids))},affordances={string.Join(",", StringNameArrayToStrings(affordances))})";
    }

    private static List<string> StringNameArrayToStrings(GStringNameArray values)
    {
        var results = new List<string>();
        foreach (var value in values)
        {
            results.Add(value.ToString());
        }
        return results;
    }
}
