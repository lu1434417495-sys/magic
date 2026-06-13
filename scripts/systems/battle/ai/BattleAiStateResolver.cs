using System.Collections.Generic;
using Godot;

internal sealed class BattleAiStateResolver
{
    private const int HpBasisPointsDenominator = 10000;

    internal TransitionResult ResolveTyped(BattleAiContext context, EnemyAiBrainDef brain)
    {
        StringName previousStateId = GetPreviousStateId(context);
        StringName currentStateId = ResolveCurrentStateId(context, brain);
        if (brain == null)
        {
            return Result(
                previousStateId,
                currentStateId,
                "",
                "missing_brain",
                new List<TransitionConditionTrace>()
            );
        }

        var rules = GetSortedRules(brain);
        if (rules.Count == 0)
        {
            return Result(
                previousStateId,
                currentStateId,
                "",
                "no_transition_rules",
                new List<TransitionConditionTrace>()
            );
        }

        foreach (EnemyAiTransitionRuleDef rule in rules)
        {
            if (rule == null)
            {
                continue;
            }
            if (!RuleAppliesToState(rule, currentStateId))
            {
                continue;
            }

            var matchedConditions = new List<TransitionConditionTrace>();
            if (RuleMatches(context, currentStateId, rule, matchedConditions))
            {
                return Result(
                    previousStateId,
                    ProgressionDataUtils.to_string_name(rule.target_state_id),
                    ProgressionDataUtils.to_string_name(rule.rule_id),
                    "matched_rule",
                    matchedConditions
                );
            }
        }

        return Result(
            previousStateId,
            currentStateId,
            "",
            "no_matching_rule",
            new List<TransitionConditionTrace>()
        );
    }

    private static StringName GetPreviousStateId(BattleAiContext context)
    {
        BattleUnitState unitState = GetUnitState(context);
        return unitState == null
            ? new StringName("")
            : ProgressionDataUtils.to_string_name(unitState.ai_state_id);
    }

    private static StringName ResolveCurrentStateId(BattleAiContext context, EnemyAiBrainDef brain)
    {
        if (brain == null)
        {
            return GetPreviousStateId(context);
        }
        StringName currentStateId = GetPreviousStateId(context);
        if (currentStateId != (StringName)"" && brain.HasState(currentStateId))
        {
            return currentStateId;
        }
        StringName defaultStateId = brain.default_state_id;
        if (defaultStateId != (StringName)"" && brain.HasState(defaultStateId))
        {
            return defaultStateId;
        }
        return defaultStateId;
    }

    private static List<EnemyAiTransitionRuleDef> GetSortedRules(EnemyAiBrainDef brain)
    {
        if (brain == null)
        {
            return new List<EnemyAiTransitionRuleDef>();
        }

        var rules = new List<EnemyAiTransitionRuleDef>();
        foreach (EnemyAiTransitionRuleDef rule in brain.transition_rules)
        {
            if (rule != null)
            {
                rules.Add(rule);
            }
        }
        rules.Sort(
            (left, right) =>
            {
                int leftOrder = left.order;
                int rightOrder = right.order;
                if (leftOrder != rightOrder)
                {
                    return leftOrder.CompareTo(rightOrder);
                }
                string leftId = left.rule_id.ToString();
                string rightId = right.rule_id.ToString();
                int idCompare = string.CompareOrdinal(leftId, rightId);
                if (idCompare != 0)
                {
                    return idCompare;
                }
                return string.CompareOrdinal(
                    left.target_state_id.ToString(),
                    right.target_state_id.ToString()
                );
            }
        );
        return rules;
    }

    private static bool RuleAppliesToState(EnemyAiTransitionRuleDef rule, StringName stateId)
    {
        if (rule == null)
        {
            return false;
        }
        return rule.AppliesToState(stateId);
    }

    private static bool RuleMatches(
        BattleAiContext context,
        StringName currentStateId,
        EnemyAiTransitionRuleDef rule,
        List<TransitionConditionTrace> matchedConditions
    )
    {
        if (rule == null)
        {
            return false;
        }

        foreach (EnemyAiTransitionConditionDef condition in rule.GetTypedConditions())
        {
            if (condition == null)
            {
                return false;
            }
            if (!ConditionMatches(context, currentStateId, condition))
            {
                return false;
            }
            matchedConditions.Add(TransitionConditionTrace.FromCondition(condition));
        }
        return true;
    }

    private static bool ConditionMatches(
        BattleAiContext context,
        StringName currentStateId,
        EnemyAiTransitionConditionDef condition
    )
    {
        EnemyAiTransitionPredicate predicateKind = condition.PredicateKind;
        if (predicateKind == EnemyAiTransitionPredicate.Always)
        {
            return true;
        }
        if (predicateKind == EnemyAiTransitionPredicate.CurrentStateIs)
        {
            return condition.state_ids.Contains(currentStateId);
        }
        if (predicateKind == EnemyAiTransitionPredicate.SelfHpAtOrBelowBasisPoints)
        {
            return IsUnitAtOrBelowHpBasisPoints(
                GetUnitState(context),
                condition.basis_points
            );
        }
        if (predicateKind == EnemyAiTransitionPredicate.AllyHpAtOrBelowBasisPoints)
        {
            return HasAllyAtOrBelowHpBasisPoints(context, condition.basis_points);
        }
        if (predicateKind == EnemyAiTransitionPredicate.NearestEnemyDistanceAtOrBelow)
        {
            return NearestEnemyDistanceAtOrBelow(context, condition.max_distance);
        }
        if (predicateKind == EnemyAiTransitionPredicate.HasSkillAffordance)
        {
            return context != null && context.HasSkillAffordanceValues(condition.affordances);
        }
        return false;
    }

    private static BattleUnitState GetUnitState(BattleAiContext context)
    {
        return context?.unit_state;
    }

    private static BattleState GetBattleState(BattleAiContext context)
    {
        return context?.state;
    }

    private static bool HasAllyAtOrBelowHpBasisPoints(
        BattleAiContext context,
        int thresholdBasisPoints
    )
    {
        BattleUnitState unitState = GetUnitState(context);
        BattleState state = GetBattleState(context);
        if (unitState == null || state == null)
        {
            return false;
        }

        foreach (BattleUnitState allyUnit in CollectUnits(state))
        {
            if (allyUnit == null || !allyUnit.is_alive)
            {
                continue;
            }
            if (allyUnit == unitState || allyUnit.unit_id == unitState.unit_id)
            {
                continue;
            }
            if (allyUnit.faction_id != unitState.faction_id)
            {
                continue;
            }
            if (IsUnitAtOrBelowHpBasisPoints(allyUnit, thresholdBasisPoints))
            {
                return true;
            }
        }
        return false;
    }

    private static bool NearestEnemyDistanceAtOrBelow(BattleAiContext context, int maxDistance)
    {
        if (maxDistance < 0)
        {
            return false;
        }

        BattleUnitState unitState = GetUnitState(context);
        BattleState state = GetBattleState(context);
        BattleGridService gridService = context?.grid_service;
        if (unitState == null || state == null || gridService == null)
        {
            return false;
        }

        Godot.Collections.Array<StringName> candidateIds =
            unitState.faction_id == (StringName)"player"
                ? state.enemy_unit_ids
                : state.ally_unit_ids;
        int bestDistance = 999999;
        foreach (StringName unitIdValue in candidateIds)
        {
            StringName unitId = ProgressionDataUtils.to_string_name(unitIdValue);
            if (!TryGetUnit(state, unitId, out BattleUnitState candidate) || !candidate.is_alive)
            {
                continue;
            }
            int distance = gridService.GetDistanceBetweenUnits(unitState, candidate);
            if (distance < bestDistance)
            {
                bestDistance = distance;
            }
        }
        return bestDistance <= maxDistance;
    }

    private static bool IsUnitAtOrBelowHpBasisPoints(
        BattleUnitState unitState,
        int thresholdBasisPoints
    )
    {
        if (unitState == null || unitState.attribute_snapshot == null)
        {
            return false;
        }
        int hpMax = Mathf.Max(
            unitState.attribute_snapshot.GetValue(new StringName("hp_max")),
            1
        );
        int clampedThreshold = Mathf.Clamp(thresholdBasisPoints, 0, HpBasisPointsDenominator);
        int currentHp = Mathf.Clamp(unitState.current_hp, 0, hpMax);
        return currentHp * HpBasisPointsDenominator <= hpMax * clampedThreshold;
    }

    private static TransitionResult Result(
        StringName previousStateId,
        StringName stateId,
        StringName ruleId,
        StringName reason,
        List<TransitionConditionTrace> matchedConditions
    )
    {
        return new TransitionResult(
            previousStateId,
            stateId,
            ruleId,
            reason,
            matchedConditions
        );
    }

    internal sealed class TransitionResult
    {
        public TransitionResult(
            StringName previousStateId,
            StringName stateId,
            StringName ruleId,
            StringName reason,
            List<TransitionConditionTrace> matchedConditions
        )
        {
            PreviousStateId = previousStateId;
            StateId = stateId;
            RuleId = ruleId;
            Reason = reason;
            MatchedConditions =
                matchedConditions != null
                    ? new List<TransitionConditionTrace>(matchedConditions)
                    : new List<TransitionConditionTrace>();
        }

        public StringName PreviousStateId { get; }
        public StringName StateId { get; }
        public StringName RuleId { get; }
        public StringName Reason { get; }
        public IReadOnlyList<TransitionConditionTrace> MatchedConditions { get; }

        public static TransitionResult Empty() =>
            new("", "", "", "", new List<TransitionConditionTrace>());

    }

    internal sealed class TransitionConditionTrace
    {
        private TransitionConditionTrace(
            StringName predicate,
            int basisPoints,
            int maxDistance,
            List<StringName> stateIds,
            List<StringName> affordances
        )
        {
            Predicate = predicate;
            BasisPoints = basisPoints;
            MaxDistance = maxDistance;
            StateIds = stateIds ?? new List<StringName>();
            Affordances = affordances ?? new List<StringName>();
        }

        public StringName Predicate { get; }
        public int BasisPoints { get; }
        public int MaxDistance { get; }
        public IReadOnlyList<StringName> StateIds { get; }
        public IReadOnlyList<StringName> Affordances { get; }

        public static TransitionConditionTrace FromCondition(EnemyAiTransitionConditionDef condition)
        {
            if (condition == null)
            {
                return new TransitionConditionTrace(
                    "",
                    -1,
                    -1,
                    new List<StringName>(),
                    new List<StringName>()
                );
            }

            return new TransitionConditionTrace(
                condition.predicate,
                condition.basis_points,
                condition.max_distance,
                CopyStringNames(condition.state_ids),
                CopyStringNames(condition.affordances)
            );
        }

        private static List<StringName> CopyStringNames(
            Godot.Collections.Array<StringName> values
        )
        {
            List<StringName> result = new();
            if (values == null)
            {
                return result;
            }
            foreach (StringName value in values)
            {
                result.Add(value);
            }
            return result;
        }
    }

    private static List<BattleUnitState> CollectUnits(BattleState state)
    {
        var result = new List<BattleUnitState>();
        if (state?.units == null)
        {
            return result;
        }
        foreach (BattleUnitState unitState in state.GetUnitsTyped())
        {
            result.Add(unitState);
        }
        return result;
    }

    private static bool TryGetUnit(
        BattleState state,
        StringName unitId,
        out BattleUnitState unitState
    )
    {
        unitState = null;
        StringName normalized = ProgressionDataUtils.to_string_name(unitId);
        if (state == null || normalized == "")
        {
            return false;
        }
        return state.TryGetUnitTyped(normalized, out unitState);
    }
}
