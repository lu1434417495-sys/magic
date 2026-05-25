using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

[Tool]
[GlobalClass]
public partial class EnemyAiTransitionConditionDef : Resource
{
	private const int HpBasisPointsDenominator = 10000;
	private static readonly StringName PredicateAlways = "always";
	private static readonly StringName PredicateCurrentStateIs = "current_state_is";
	private static readonly StringName PredicateSelfHpAtOrBelow = "self_hp_at_or_below_basis_points";
	private static readonly StringName PredicateAllyHpAtOrBelow = "ally_hp_at_or_below_basis_points";
	private static readonly StringName PredicateNearestEnemyDistanceAtOrBelow = "nearest_enemy_distance_at_or_below";
	private static readonly StringName PredicateHasSkillAffordance = "has_skill_affordance";

	[Export] public StringName predicate = "";
	[Export] public int basis_points = -1;
	[Export] public int max_distance = -1;
	[Export] public GStringNameArray state_ids = new();
	[Export] public GStringNameArray affordances = new();

	public static int HP_BASIS_POINTS_DENOMINATOR() => HpBasisPointsDenominator;
	public static StringName PREDICATE_ALWAYS() => PredicateAlways;
	public static StringName PREDICATE_CURRENT_STATE_IS() => PredicateCurrentStateIs;
	public static StringName PREDICATE_SELF_HP_AT_OR_BELOW() => PredicateSelfHpAtOrBelow;
	public static StringName PREDICATE_ALLY_HP_AT_OR_BELOW() => PredicateAllyHpAtOrBelow;
	public static StringName PREDICATE_NEAREST_ENEMY_DISTANCE_AT_OR_BELOW() => PredicateNearestEnemyDistanceAtOrBelow;
	public static StringName PREDICATE_HAS_SKILL_AFFORDANCE() => PredicateHasSkillAffordance;
	public static GDictionary VALID_PREDICATES() => new()
	{
		[PredicateAlways] = true,
		[PredicateCurrentStateIs] = true,
		[PredicateSelfHpAtOrBelow] = true,
		[PredicateAllyHpAtOrBelow] = true,
		[PredicateNearestEnemyDistanceAtOrBelow] = true,
		[PredicateHasSkillAffordance] = true,
	};

	public GArray validate_schema(string owner_label, GDictionary declared_state_ids = null)
	{
		var errors = new GArray();
		var states = declared_state_ids ?? new GDictionary();
		if (predicate == (StringName)"")
		{
			errors.Add($"{owner_label} transition condition is missing predicate.");
			return errors;
		}
		if (!VALID_PREDICATES().ContainsKey(predicate))
		{
			errors.Add($"{owner_label} transition condition uses unsupported predicate {predicate}.");
			return errors;
		}
		if (predicate == PredicateAlways)
			return errors;
		if (predicate == PredicateCurrentStateIs)
		{
			if (state_ids.Count == 0)
				errors.Add($"{owner_label} current_state_is condition must declare state_ids.");
			foreach (var state_id in state_ids)
			{
				if (state_id == (StringName)"")
					errors.Add($"{owner_label} current_state_is condition contains empty state_id.");
				else if (states.Count > 0 && !states.ContainsKey(state_id))
					errors.Add($"{owner_label} current_state_is condition references undeclared state_id {state_id}.");
			}
		}
		else if (predicate == PredicateSelfHpAtOrBelow || predicate == PredicateAllyHpAtOrBelow)
		{
			if (basis_points < 0 || basis_points > HpBasisPointsDenominator)
				errors.Add($"{owner_label} {predicate} condition basis_points must be within [0, 10000].");
		}
		else if (predicate == PredicateNearestEnemyDistanceAtOrBelow)
		{
			if (max_distance < 0)
				errors.Add($"{owner_label} nearest_enemy_distance_at_or_below condition max_distance must be >= 0.");
		}
		else if (predicate == PredicateHasSkillAffordance)
		{
			if (affordances.Count == 0)
				errors.Add($"{owner_label} has_skill_affordance condition must declare affordances.");
			foreach (var affordance in affordances)
			{
				if (affordance == (StringName)"")
				{
					errors.Add($"{owner_label} has_skill_affordance condition contains empty affordance.");
					continue;
				}
				if (!EnemyAiGenerationSlotDef.VALID_AFFORDANCES().ContainsKey(affordance.ToString()))
					errors.Add($"{owner_label} has_skill_affordance condition uses unsupported affordance {affordance}.");
			}
		}
		return errors;
	}

	public GDictionary to_trace_dict()
	{
		return new GDictionary
		{
			["predicate"] = predicate.ToString(),
			["basis_points"] = basis_points,
			["max_distance"] = max_distance,
			["state_ids"] = _string_name_array_to_strings(state_ids),
			["affordances"] = _string_name_array_to_strings(affordances),
		};
	}

	public string to_signature()
	{
		return $"{predicate}(bp={basis_points},dist={max_distance},states={string.Join(",", _string_name_array_to_strings(state_ids))},affordances={string.Join(",", _string_name_array_to_strings(affordances))})";
	}

	private static Godot.Collections.Array<string> _string_name_array_to_strings(GStringNameArray values)
	{
		var results = new Godot.Collections.Array<string>();
		foreach (var value in values)
			results.Add(value.ToString());
		return results;
	}
}
