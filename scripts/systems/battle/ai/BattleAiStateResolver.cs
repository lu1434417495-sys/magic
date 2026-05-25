using System.Collections.Generic;
using Godot;
using static GdInterop;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleAiStateResolver : RefCounted
{
    private const int HpBasisPointsDenominator = 10000;

    public GDictionary resolve(GodotObject context, GodotObject brain)
    {
        StringName previousStateId = GetPreviousStateId(context);
        StringName currentStateId = ResolveCurrentStateId(context, brain);
        if (brain == null)
        {
            return Result(previousStateId, currentStateId, "", "missing_brain", new GArray());
        }

        GArray rules = GetSortedRules(brain);
        if (rules.Count == 0)
        {
            return Result(previousStateId, currentStateId, "", "no_transition_rules", new GArray());
        }

        foreach (Variant ruleVariant in rules)
        {
            GodotObject rule = ruleVariant.AsGodotObject();
            if (rule == null)
            {
                continue;
            }
            if (!RuleAppliesToState(rule, currentStateId))
            {
                continue;
            }

            var matchedConditions = new GArray();
            if (RuleMatches(context, currentStateId, rule, matchedConditions))
            {
                return Result(
                    previousStateId,
                    ProgressionDataUtils.to_string_name(rule.Get("target_state_id")),
                    ProgressionDataUtils.to_string_name(rule.Get("rule_id")),
                    "matched_rule",
                    matchedConditions);
            }
        }

        return Result(previousStateId, currentStateId, "", "no_matching_rule", new GArray());
    }

    private static StringName GetPreviousStateId(GodotObject context)
    {
        BattleUnitState unitState = GetUnitState(context);
        return unitState == null ? new StringName("") : ProgressionDataUtils.to_string_name(unitState.ai_state_id);
    }

    private static StringName ResolveCurrentStateId(GodotObject context, GodotObject brain)
    {
        if (brain == null)
        {
            return GetPreviousStateId(context);
        }
        StringName currentStateId = GetPreviousStateId(context);
        if (currentStateId != (StringName)"" && brain.HasMethod("has_state") && brain.Call("has_state", currentStateId).AsBool())
        {
            return currentStateId;
        }
        StringName defaultStateId = ProgressionDataUtils.to_string_name(brain.Get("default_state_id"));
        if (defaultStateId != (StringName)"" && brain.HasMethod("has_state") && brain.Call("has_state", defaultStateId).AsBool())
        {
            return defaultStateId;
        }
        return defaultStateId;
    }

    private static GArray GetSortedRules(GodotObject brain)
    {
        if (brain == null)
        {
            return new GArray();
        }

        GArray rawRules = new();
        if (brain.HasMethod("get_transition_rules"))
        {
            rawRules = brain.Call("get_transition_rules").AsGodotArray();
        }
        else
        {
            Variant transitionRules = brain.Get("transition_rules");
            if (transitionRules.VariantType == Variant.Type.Array)
            {
                rawRules = transitionRules.AsGodotArray();
            }
        }

        var rules = new List<GodotObject>();
        foreach (Variant ruleVariant in rawRules)
        {
            GodotObject rule = ruleVariant.AsGodotObject();
            if (rule != null)
            {
                rules.Add(rule);
            }
        }
        rules.Sort((left, right) =>
        {
            int leftOrder = GetInt(left, "order");
            int rightOrder = GetInt(right, "order");
            if (leftOrder != rightOrder)
            {
                return leftOrder.CompareTo(rightOrder);
            }
            string leftId = GetString(left, "rule_id");
            string rightId = GetString(right, "rule_id");
            int idCompare = string.CompareOrdinal(leftId, rightId);
            if (idCompare != 0)
            {
                return idCompare;
            }
            return string.CompareOrdinal(GetString(left, "target_state_id"), GetString(right, "target_state_id"));
        });

        var sorted = new GArray();
        foreach (GodotObject rule in rules)
        {
            sorted.Add(rule);
        }
        return sorted;
    }

    private static bool RuleAppliesToState(GodotObject rule, StringName stateId)
    {
        if (rule == null)
        {
            return false;
        }
        if (rule.HasMethod("applies_to_state"))
        {
            return rule.Call("applies_to_state", stateId).AsBool();
        }
        GArray fromStateIds = GetArray(rule, "from_state_ids");
        return fromStateIds.Count == 0 || fromStateIds.Contains(stateId);
    }

    private static bool RuleMatches(GodotObject context, StringName currentStateId, GodotObject rule, GArray matchedConditions)
    {
        if (rule == null)
        {
            return false;
        }

        GArray conditions = rule.HasMethod("get_conditions")
            ? rule.Call("get_conditions").AsGodotArray()
            : GetArray(rule, "conditions");
        foreach (Variant conditionVariant in conditions)
        {
            GodotObject condition = conditionVariant.AsGodotObject();
            if (condition == null)
            {
                return false;
            }
            if (!ConditionMatches(context, currentStateId, condition))
            {
                return false;
            }
            matchedConditions.Add(condition.HasMethod("to_trace_dict")
                ? condition.Call("to_trace_dict")
                : new GDictionary());
        }
        return true;
    }

    private static bool ConditionMatches(GodotObject context, StringName currentStateId, GodotObject condition)
    {
        StringName predicate = ProgressionDataUtils.to_string_name(condition.Get("predicate"));
        if (predicate == EnemyAiTransitionConditionDef.PREDICATE_ALWAYS())
        {
            return true;
        }
        if (predicate == EnemyAiTransitionConditionDef.PREDICATE_CURRENT_STATE_IS())
        {
            return GetArray(condition, "state_ids").Contains(currentStateId);
        }
        if (predicate == EnemyAiTransitionConditionDef.PREDICATE_SELF_HP_AT_OR_BELOW())
        {
            return IsUnitAtOrBelowHpBasisPoints(GetUnitState(context), GetInt(condition, "basis_points"));
        }
        if (predicate == EnemyAiTransitionConditionDef.PREDICATE_ALLY_HP_AT_OR_BELOW())
        {
            return HasAllyAtOrBelowHpBasisPoints(context, GetInt(condition, "basis_points"));
        }
        if (predicate == EnemyAiTransitionConditionDef.PREDICATE_NEAREST_ENEMY_DISTANCE_AT_OR_BELOW())
        {
            return NearestEnemyDistanceAtOrBelow(context, GetInt(condition, "max_distance"));
        }
        if (predicate == EnemyAiTransitionConditionDef.PREDICATE_HAS_SKILL_AFFORDANCE())
        {
            return context != null
                && context.HasMethod("has_skill_affordance")
                && context.Call("has_skill_affordance", GetArray(condition, "affordances")).AsBool();
        }
        return false;
    }

    private static BattleUnitState GetUnitState(GodotObject context)
    {
        if (context == null)
        {
            return null;
        }
        return context.Get("unit_state").AsGodotObject() as BattleUnitState;
    }

    private static BattleState GetBattleState(GodotObject context)
    {
        if (context == null)
        {
            return null;
        }
        return context.Get("state").AsGodotObject() as BattleState;
    }

    private static bool HasAllyAtOrBelowHpBasisPoints(GodotObject context, int thresholdBasisPoints)
    {
        BattleUnitState unitState = GetUnitState(context);
        BattleState state = GetBattleState(context);
        if (unitState == null || state == null)
        {
            return false;
        }

        foreach (Variant unitVariant in state.units.Values)
        {
            BattleUnitState allyUnit = unitVariant.AsGodotObject() as BattleUnitState;
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

    private static bool NearestEnemyDistanceAtOrBelow(GodotObject context, int maxDistance)
    {
        if (maxDistance < 0)
        {
            return false;
        }

        BattleUnitState unitState = GetUnitState(context);
        BattleState state = GetBattleState(context);
        GodotObject gridService = GetObject(context, "grid_service");
        if (unitState == null || state == null || gridService == null)
        {
            return false;
        }

        Godot.Collections.Array<StringName> candidateIds = unitState.faction_id == (StringName)"player"
            ? state.enemy_unit_ids
            : state.ally_unit_ids;
        int bestDistance = 999999;
        foreach (StringName unitIdVariant in candidateIds)
        {
            StringName unitId = ProgressionDataUtils.to_string_name(unitIdVariant);
            if (!state.units.ContainsKey(unitId))
            {
                continue;
            }
            BattleUnitState candidate = state.units[unitId].AsGodotObject() as BattleUnitState;
            if (candidate == null || !candidate.is_alive)
            {
                continue;
            }
            int distance = gridService.Call("get_distance_between_units", unitState, candidate).AsInt32();
            if (distance < bestDistance)
            {
                bestDistance = distance;
            }
        }
        return bestDistance <= maxDistance;
    }

    private static bool IsUnitAtOrBelowHpBasisPoints(BattleUnitState unitState, int thresholdBasisPoints)
    {
        if (unitState == null || unitState.attribute_snapshot == null)
        {
            return false;
        }
        int hpMax = Mathf.Max(unitState.attribute_snapshot.Call("get_value", new StringName("hp_max")).AsInt32(), 1);
        int clampedThreshold = Mathf.Clamp(thresholdBasisPoints, 0, HpBasisPointsDenominator);
        int currentHp = Mathf.Clamp(unitState.current_hp, 0, hpMax);
        return currentHp * HpBasisPointsDenominator <= hpMax * clampedThreshold;
    }

    private static GDictionary Result(
        StringName previousStateId,
        StringName stateId,
        StringName ruleId,
        StringName reason,
        GArray matchedConditions)
    {
        return new GDictionary
        {
            ["previous_state_id"] = previousStateId,
            ["state_id"] = stateId,
            ["rule_id"] = ruleId,
            ["reason"] = reason,
            ["matched_conditions"] = (matchedConditions ?? new GArray()).Duplicate(true),
        };
    }
}
