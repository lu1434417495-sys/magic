using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

[Tool]
[GlobalClass]
public partial class EnemyAiGenerationSlotDef : Resource
{
	[Export] public StringName slot_id = "";
	[Export] public StringName slot_role = "offense";
	[Export] public int order = 0;
	[Export] public GStringNameArray allowed_affordances = new();
	[Export] public GStringNameArray action_families = new();
	[Export] public StringName style_template_action_id = "";
	[Export] public StringName score_bucket_id = "";
	[Export] public StringName target_selector = "";
	[Export] public int desired_min_distance = -1;
	[Export] public int desired_max_distance = -1;
	[Export] public StringName distance_reference = "";
	[Export] public StringName suppression_policy = "suppress_matching_family";

	public static GDictionary VALID_AFFORDANCES() => new()
	{
		["unit_hostile.damage"] = true,
		["unit_hostile.control"] = true,
		["ground_hostile.aoe"] = true,
		["ground_control"] = true,
		["terrain_control"] = true,
		["displacement_control"] = true,
		["charge_engage"] = true,
		["charge_path_aoe"] = true,
		["multi_unit"] = true,
		["random_chain"] = true,
		["special_ground"] = true,
		["ally_heal"] = true,
		["self_or_ally_buff"] = true,
		["reposition"] = true,
		["escape"] = true,
		["utility"] = true,
		["breaker"] = true,
	};

	public static GDictionary VALID_ACTION_FAMILIES() => new()
	{
		["use_unit_skill"] = true,
		["use_ground_skill"] = true,
		["use_multi_unit_skill"] = true,
		["use_random_chain_skill"] = true,
		["use_charge"] = true,
		["use_charge_path_aoe"] = true,
		["move_to_range"] = true,
		["move_to_multi_unit_skill_position"] = true,
	};

	public static GDictionary VALID_SLOT_ROLES() => new()
	{
		["offense"] = true,
		["control"] = true,
		["support"] = true,
		["positioning"] = true,
		["survival"] = true,
		["engage"] = true,
	};

	public static GDictionary VALID_TARGET_SELECTORS() => new()
	{
		[""] = true,
		["nearest_enemy"] = true,
		["lowest_hp_enemy"] = true,
		["nearest_role_threat_enemy"] = true,
		["nearest_ally"] = true,
		["lowest_hp_ally"] = true,
		["self"] = true,
	};

	public static GDictionary VALID_DISTANCE_REFERENCES() => new()
	{
		[""] = true,
		["target_unit"] = true,
		["target_coord"] = true,
		["candidate_pool"] = true,
		["enemy_frontline"] = true,
	};

	public static GDictionary VALID_SUPPRESSION_POLICIES() => new()
	{
		["suppress_matching_family"] = true,
		["allow_companion"] = true,
		["manual_only"] = true,
	};

	public bool matches_affordance(GDictionary record, StringName action_family)
	{
		if (!action_families.Contains(action_family))
			return false;
		if (allowed_affordances.Count == 0)
			return false;
		var record_affordances = GdInterop.GetArray(record, "affordances");
		if (record_affordances.Count == 0)
			return false;
		foreach (Variant affordance in record_affordances)
		{
			if (allowed_affordances.Contains(GdInterop.ToStringName(affordance)))
				return true;
		}
		return false;
	}

	public GDictionary to_signature()
	{
		return new GDictionary
		{
			["slot_id"] = slot_id.ToString(),
			["slot_role"] = slot_role.ToString(),
			["order"] = order,
			["allowed_affordances"] = _stringify_array(allowed_affordances),
			["action_families"] = _stringify_array(action_families),
			["style_template_action_id"] = style_template_action_id.ToString(),
			["score_bucket_id"] = score_bucket_id.ToString(),
			["target_selector"] = target_selector.ToString(),
			["desired_min_distance"] = desired_min_distance,
			["desired_max_distance"] = desired_max_distance,
			["distance_reference"] = distance_reference.ToString(),
			["suppression_policy"] = suppression_policy.ToString(),
		};
	}

	public GArray validate_schema(string context_label = "Enemy AI generation slot", GArray state_actions = null)
	{
		var errors = new GArray();
		var actions = state_actions ?? new GArray();
		var label = $"{context_label} generation slot {slot_id}";
		if (slot_id == (StringName)"")
			errors.Add($"{context_label} is missing slot_id.");
		if (!VALID_SLOT_ROLES().ContainsKey(slot_role.ToString()))
			errors.Add($"{label} declares unsupported slot_role {slot_role}.");
		if (order < 0)
			errors.Add($"{label} order must be >= 0.");
		if (allowed_affordances.Count == 0)
			errors.Add($"{label} must declare at least one allowed_affordance.");
		foreach (var affordance in allowed_affordances)
		{
			if (!VALID_AFFORDANCES().ContainsKey(affordance.ToString()))
				errors.Add($"{label} declares unsupported affordance {affordance}.");
		}
		if (action_families.Count == 0)
			errors.Add($"{label} must declare at least one action_family.");
		foreach (var family in action_families)
		{
			if (!VALID_ACTION_FAMILIES().ContainsKey(family.ToString()))
				errors.Add($"{label} declares unsupported action_family {family}.");
		}
		if (style_template_action_id != (StringName)"" && _find_action_by_id(actions, style_template_action_id) == null)
			errors.Add($"{label} style_template_action_id {style_template_action_id} does not exist in the same state.");
		if (!VALID_TARGET_SELECTORS().ContainsKey(target_selector.ToString()))
			errors.Add($"{label} declares unsupported target_selector {target_selector}.");
		if (desired_min_distance < -1)
			errors.Add($"{label} desired_min_distance must be >= -1.");
		if (desired_max_distance < -1)
			errors.Add($"{label} desired_max_distance must be >= -1.");
		if (desired_min_distance >= 0 && desired_max_distance >= 0 && desired_min_distance > desired_max_distance)
			errors.Add($"{label} desired_min_distance cannot exceed desired_max_distance.");
		if (!VALID_DISTANCE_REFERENCES().ContainsKey(distance_reference.ToString()))
			errors.Add($"{label} declares unsupported distance_reference {distance_reference}.");
		if (!VALID_SUPPRESSION_POLICIES().ContainsKey(suppression_policy.ToString()))
			errors.Add($"{label} declares unsupported suppression_policy {suppression_policy}.");
		return errors;
	}

	private static GodotObject _find_action_by_id(GArray state_actions, StringName expected_action_id)
	{
		foreach (Variant actionVariant in state_actions)
		{
			var action = actionVariant.AsGodotObject();
			if (action != null && GdInterop.GetStringName(action, "action_id") == expected_action_id)
				return action;
		}
		return null;
	}

	private static Godot.Collections.Array<string> _stringify_array(GStringNameArray values)
	{
		var result = new Godot.Collections.Array<string>();
		foreach (var value in values)
			result.Add(value.ToString());
		result.Sort();
		return result;
	}
}
