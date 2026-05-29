using Godot;

[GlobalClass]
public partial class EnemyAiBrainDef : Resource
{
    [Export]
    public StringName brain_id { get; set; } = "";

    [Export]
    public StringName default_state_id { get; set; } = "engage";

    [Export]
    public Godot.Collections.Array<EnemyAiStateDef> states { get; set; } = new();

    [Export]
    public Godot.Collections.Array<EnemyAiTransitionRuleDef> transition_rules { get; set; } = new();

    public Godot.Collections.Array<EnemyAiStateDef> get_resolved_states()
    {
        var result = new Godot.Collections.Array<EnemyAiStateDef>();

        foreach (var state in states)
        {
            if (state != null)
                result.Add(state);
        }

        return result;
    }

    public EnemyAiStateDef get_state(StringName stateId)
    {
        foreach (var s in get_resolved_states())
        {
            if (s != null && s.state_id == stateId)
                return s;
        }
        return null;
    }

    public bool has_state(StringName stateId) => get_state(stateId) != null;

    public Godot.Collections.Array<string> validate_schema(
        Godot.Collections.Dictionary skillDefs = null
    )
    {
        skillDefs ??= new Godot.Collections.Dictionary();

        var errors = new Godot.Collections.Array<string>();

        if (brain_id == "")
        {
            errors.Add("Enemy brain is missing brain_id.");
            return errors;
        }

        if (default_state_id == "")
            errors.Add($"Enemy brain {brain_id} is missing default_state_id.");

        if (states.Count == 0)
            errors.Add($"Enemy brain {brain_id} must declare at least one state.");

        var seenStateIds = new Godot.Collections.Dictionary();
        bool defaultStateFound = false;

        foreach (var state in states)
        {
            if (state == null)
            {
                errors.Add($"Enemy brain {brain_id} contains a null state resource.");
                continue;
            }

            if (state.state_id == "")
                errors.Add($"Enemy brain {brain_id} contains a state without state_id.");
            else if (seenStateIds.ContainsKey(state.state_id))
                errors.Add($"Enemy brain {brain_id} declares duplicate state_id {state.state_id}.");
            else
                seenStateIds[state.state_id] = true;

            if (state.state_id == default_state_id)
                defaultStateFound = true;

            foreach (var e in state.validate_schema(brain_id, skillDefs))
                errors.Add(e);
        }

        if (default_state_id != "" && !defaultStateFound)
            errors.Add(
                $"Enemy brain {brain_id} default_state_id {default_state_id} is not declared in states."
            );

        foreach (var e in _validate_transition_rules(seenStateIds))
            errors.Add(e);

        return errors;
    }

    private Godot.Collections.Array<string> _validate_transition_rules(
        Godot.Collections.Dictionary declaredStateIds
    )
    {
        var errors = new Godot.Collections.Array<string>();

        var seenRuleIds = new Godot.Collections.Dictionary();
        var seenOrders = new Godot.Collections.Dictionary();

        foreach (var rule in transition_rules)
        {
            if (rule == null)
            {
                errors.Add($"Enemy brain {brain_id} contains a null transition rule resource.");
                continue;
            }

            var ruleId = ProgressionDataUtils.to_string_name(rule.rule_id);

            if (ruleId != "" && seenRuleIds.ContainsKey(ruleId))
                errors.Add(
                    $"Enemy brain {brain_id} declares duplicate transition rule_id {ruleId}."
                );
            else if (ruleId != "")
                seenRuleIds[ruleId] = true;

            int order = rule.order;

            if (seenOrders.ContainsKey(order))
                errors.Add($"Enemy brain {brain_id} declares duplicate transition order {order}.");
            else
                seenOrders[order] = true;

            foreach (var e in rule.validate_schema(brain_id, declaredStateIds))
                errors.Add(e.AsString());
        }

        return errors;
    }
}
