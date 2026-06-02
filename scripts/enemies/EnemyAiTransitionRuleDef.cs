using System.Collections.Generic;
using Godot;
using GConditionArray = Godot.Collections.Array<EnemyAiTransitionConditionDef>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

[Tool]
[GlobalClass]
public partial class EnemyAiTransitionRuleDef : Resource
{
    [Export]
    public StringName rule_id = "";

    [Export]
    public int order = 0;

    [Export]
    public GStringNameArray from_state_ids = new();

    [Export]
    public StringName target_state_id = "";

    [Export]
    public GConditionArray conditions = new();

    [Export(PropertyHint.MultilineText)]
    public string designer_note = "";

    public Godot.Collections.Array get_conditions()
    {
        var result = new Godot.Collections.Array();
        foreach (EnemyAiTransitionConditionDef condition in conditions)
        {
            if (condition != null)
                result.Add(condition);
        }
        return result;
    }

    internal List<EnemyAiTransitionConditionDef> GetTypedConditions()
    {
        var result = new List<EnemyAiTransitionConditionDef>();
        foreach (EnemyAiTransitionConditionDef condition in conditions)
        {
            if (condition != null)
            {
                result.Add(condition);
            }
        }
        return result;
    }

    internal bool AppliesToState(StringName stateId)
    {
        return from_state_ids.Count == 0 || from_state_ids.Contains(stateId);
    }

    internal List<string> ValidateSchema(
        StringName brainId,
        IReadOnlySet<StringName> declaredStateIds
    )
    {
        var errors = new List<string>();
        var ownerLabel = $"Enemy brain {brainId} transition rule {rule_id}";
        if (rule_id == (StringName)"")
            errors.Add($"Enemy brain {brainId} contains a transition rule without rule_id.");
        if (target_state_id == (StringName)"")
            errors.Add($"{ownerLabel} is missing target_state_id.");
        else if (declaredStateIds == null || !declaredStateIds.Contains(target_state_id))
            errors.Add(
                $"{ownerLabel} target_state_id {target_state_id} is not declared in states."
            );
        foreach (var from_state_id in from_state_ids)
        {
            if (from_state_id == (StringName)"")
                errors.Add($"{ownerLabel} contains empty from_state_id.");
            else if (declaredStateIds == null || !declaredStateIds.Contains(from_state_id))
                errors.Add(
                    $"{ownerLabel} from_state_id {from_state_id} is not declared in states."
                );
        }
        if (conditions.Count == 0)
            errors.Add($"{ownerLabel} must declare at least one condition.");
        foreach (EnemyAiTransitionConditionDef condition in conditions)
        {
            if (condition == null)
            {
                errors.Add($"{ownerLabel} contains a null condition resource.");
                continue;
            }
            errors.AddRange(condition.ValidateSchema(ownerLabel, declaredStateIds));
        }
        return errors;
    }

    internal string ToSignature()
    {
        var conditionEntries = new List<string>();
        foreach (EnemyAiTransitionConditionDef condition in GetTypedConditions())
        {
            conditionEntries.Add(condition.ToSignature());
        }
        return $"{order}:{rule_id}:{target_state_id}:from=[{string.Join(",", StringNameArrayToStrings(from_state_ids))}]:conditions=[{string.Join(";", conditionEntries)}]";
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
