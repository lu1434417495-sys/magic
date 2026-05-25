using Godot;
using GArray = Godot.Collections.Array;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

[Tool]
[GlobalClass]
public partial class EnemyAiTransitionRuleDef : Resource
{
	[Export] public StringName rule_id = "";
	[Export] public int order = 0;
	[Export] public GStringNameArray from_state_ids = new();
	[Export] public StringName target_state_id = "";
	[Export] public GArray conditions = new();
	[Export(PropertyHint.MultilineText)] public string designer_note = "";

	public GArray get_conditions()
	{
		var result = new GArray();
		foreach (Variant conditionVariant in conditions)
		{
			if (conditionVariant.VariantType != Variant.Type.Nil && conditionVariant.AsGodotObject() != null)
				result.Add(conditionVariant);
		}
		return result;
	}

	public bool applies_to_state(StringName state_id)
	{
		return from_state_ids.Count == 0 || from_state_ids.Contains(state_id);
	}

	public GArray validate_schema(StringName brain_id, Godot.Collections.Dictionary declared_state_ids)
	{
		var errors = new GArray();
		var owner_label = $"Enemy brain {brain_id} transition rule {rule_id}";
		if (rule_id == (StringName)"")
			errors.Add($"Enemy brain {brain_id} contains a transition rule without rule_id.");
		if (target_state_id == (StringName)"")
			errors.Add($"{owner_label} is missing target_state_id.");
		else if (!declared_state_ids.ContainsKey(target_state_id))
			errors.Add($"{owner_label} target_state_id {target_state_id} is not declared in states.");
		foreach (var from_state_id in from_state_ids)
		{
			if (from_state_id == (StringName)"")
				errors.Add($"{owner_label} contains empty from_state_id.");
			else if (!declared_state_ids.ContainsKey(from_state_id))
				errors.Add($"{owner_label} from_state_id {from_state_id} is not declared in states.");
		}
		if (conditions.Count == 0)
			errors.Add($"{owner_label} must declare at least one condition.");
		foreach (Variant conditionVariant in conditions)
		{
			if (conditionVariant.VariantType == Variant.Type.Nil || conditionVariant.AsGodotObject() == null)
			{
				errors.Add($"{owner_label} contains a null condition resource.");
				continue;
			}
			if (conditionVariant.AsGodotObject() is not EnemyAiTransitionConditionDef condition)
			{
				errors.Add($"{owner_label} contains an invalid condition resource.");
				continue;
			}
			errors.AddRange(condition.validate_schema(owner_label, declared_state_ids));
		}
		return errors;
	}

	public string to_signature()
	{
		var condition_entries = new Godot.Collections.Array<string>();
		foreach (Variant conditionVariant in get_conditions())
		{
			if (conditionVariant.AsGodotObject() is EnemyAiTransitionConditionDef condition)
				condition_entries.Add(condition.to_signature());
		}
		return $"{order}:{rule_id}:{target_state_id}:from=[{string.Join(",", _string_name_array_to_strings(from_state_ids))}]:conditions=[{string.Join(";", condition_entries)}]";
	}

	private static Godot.Collections.Array<string> _string_name_array_to_strings(GStringNameArray values)
	{
		var results = new Godot.Collections.Array<string>();
		foreach (var value in values)
			results.Add(value.ToString());
		return results;
	}
}
