using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class UseUnitSkillAction : Resource
{
	public static StringName DISTANCE_REF_TARGET_UNIT() => "target_unit";
	public static StringName DISTANCE_REF_ENEMY_FRONTLINE() => "enemy_frontline";

	private static readonly StringName TargetSelectorNearestEnemy = "nearest_enemy";
	private static readonly StringName TargetSelectorLowestHpEnemy = "lowest_hp_enemy";
	private static readonly StringName TargetSelectorNearestRoleThreatEnemy = "nearest_role_threat_enemy";
	private static readonly StringName TargetSelectorNearestAlly = "nearest_ally";
	private static readonly StringName TargetSelectorLowestHpAlly = "lowest_hp_ally";
	private static readonly StringName TargetSelectorSelf = "self";

	[Export] public StringName action_id = "";
	[Export] public StringName score_bucket_id = "";
	[Export] public StringName action_intent = "positioning";
	[Export] public Godot.Collections.Array<StringName> skill_ids = new();
	[Export] public StringName target_selector = "nearest_enemy";
	[Export] public int minimum_effective_target_count = 1;
	[Export] public int maximum_friendly_fire_target_count = 0;
	[Export] public bool allow_friendly_lethal = false;
	[Export] public int desired_min_distance = -1;
	[Export] public int desired_max_distance = -1;
	[Export] public StringName distance_reference = "";

	private readonly BattleAiUnitSkillCandidateEvaluator _unitSkillCandidateEvaluator = new();

	public GodotObject decide(GodotObject context)
	{
		AiTraceRecorder.enter("decide:unit_skill");
		try
		{
			return _unitSkillCandidateEvaluator.evaluate(this, context);
		}
		finally
		{
			AiTraceRecorder.exit("decide:unit_skill");
		}
	}

	public Godot.Collections.Array<string> validate_schema()
	{
		var errors = CollectBaseValidationErrors();
		if (skill_ids.Count == 0)
			errors.Add($"UseUnitSkillAction {action_id} must declare at least one skill_id.");
		if (target_selector == (StringName)"")
			errors.Add($"UseUnitSkillAction {action_id} is missing target_selector.");
		if (minimum_effective_target_count < 0)
			errors.Add($"UseUnitSkillAction {action_id} minimum_effective_target_count must be >= 0.");
		if (maximum_friendly_fire_target_count < 0)
			errors.Add($"UseUnitSkillAction {action_id} maximum_friendly_fire_target_count must be >= 0.");
		if (desired_min_distance < 0)
			errors.Add($"UseUnitSkillAction {action_id} desired_min_distance must be >= 0.");
		if (desired_max_distance < desired_min_distance)
			errors.Add($"UseUnitSkillAction {action_id} desired_max_distance must be >= desired_min_distance.");
		if (distance_reference != DISTANCE_REF_TARGET_UNIT() && distance_reference != DISTANCE_REF_ENEMY_FRONTLINE())
			errors.Add($"UseUnitSkillAction {action_id} distance_reference must be target_unit or enemy_frontline.");
		return errors;
	}

	public Godot.Collections.Array<StringName> get_declared_skill_ids()
	{
		var results = new Godot.Collections.Array<StringName>();
		var seen = new GDictionary();
		AppendDeclaredSkillId(results, seen, skill_ids);
		return results;
	}

	public Godot.Collections.Array<string> validate_skill_references(GDictionary skill_defs)
	{
		var errors = new Godot.Collections.Array<string>();
		foreach (StringName skillId in get_declared_skill_ids())
		{
			if (skillId == (StringName)"")
			{
				errors.Add($"AI action {action_id} references an empty skill_id.");
				continue;
			}
			if (skill_defs == null || !skill_defs.ContainsKey(skillId))
				errors.Add($"AI action {action_id} references missing skill {skillId}.");
		}
		return errors;
	}

	private Godot.Collections.Array<string> CollectBaseValidationErrors()
	{
		var errors = new Godot.Collections.Array<string>();
		if (action_id == (StringName)"")
			errors.Add("AI action is missing action_id.");
		if (target_selector != (StringName)"" && !IsSupportedTargetSelector(target_selector))
			errors.Add($"AI action {action_id} has unsupported target_selector {target_selector}.");
		return errors;
	}

	private static bool IsSupportedTargetSelector(StringName selector)
	{
		return selector == TargetSelectorNearestEnemy
			|| selector == TargetSelectorLowestHpEnemy
			|| selector == TargetSelectorNearestRoleThreatEnemy
			|| selector == TargetSelectorNearestAlly
			|| selector == TargetSelectorLowestHpAlly
			|| selector == TargetSelectorSelf;
	}

	private static void AppendDeclaredSkillId(Godot.Collections.Array<StringName> results, GDictionary seen, Godot.Collections.Array<StringName> rawSkillIds)
	{
		foreach (StringName rawSkillId in rawSkillIds)
		{
			if (rawSkillId == (StringName)"" || seen.ContainsKey(rawSkillId))
				continue;
			seen[rawSkillId] = true;
			results.Add(rawSkillId);
		}
	}
}
