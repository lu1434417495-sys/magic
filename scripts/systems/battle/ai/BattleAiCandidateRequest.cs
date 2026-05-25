using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GArray = Godot.Collections.Array;

[GlobalClass]
public partial class BattleAiCandidateRequest : RefCounted
{
	private static readonly StringName _familyMoveToRange = "move_to_range";
	public static StringName FamilyMoveToRange() => _familyMoveToRange;

	private static readonly HashSet<string> PathBudgetKeys = new()
	{
		"max_cost",
		"max_nodes",
		"max_destinations",
		"path_tree_min_destination_count",
		"include_origin",
		"prefer_progress",
	};

	private static readonly HashSet<string> MoveToRangeTacticalKeys = new()
	{
		"target_selector",
		"range_skill_ids",
		"position_objective_kind",
	};

	private static readonly HashSet<string> MoveToRangeRuntimeKeys = new()
	{
		"configured_desired_min_distance",
		"configured_desired_max_distance",
		"effective_attack_range",
	};

	public StringName FamilyId = "";
	public StringName ActionId = "";
	public string ActionLabel = "";
	public StringName ActionIntent = "";
	public StringName ScoreBucketId = "";
	public StringName ActorUnitId = "";
	public StringName FocusTargetUnitId = "";

	public int DesiredMinDistance = 0;
	public int DesiredMaxDistance = 0;
	public int MaxCandidateCount = 0;
	public GDictionary PathSearchBudget = new();
	public GDictionary TacticalParams = new();
	public GDictionary RuntimeMetadata = new();

	public bool RequireValidPayload()
	{
		if (FamilyId != FamilyMoveToRange())
			return Fail($"Unsupported candidate family_id {FamilyId}.");
		if (ActionId == "")
			return Fail("BattleAiCandidateRequest action_id must not be empty.");
		if (ActorUnitId == "")
			return Fail("BattleAiCandidateRequest actor_unit_id must not be empty.");
		if (ActionIntent == "" || !BattleAiActionIntent.is_valid(ActionIntent))
			return Fail($"BattleAiCandidateRequest action_intent is unsupported: {ActionIntent}.");
		if (DesiredMinDistance < 0 || DesiredMaxDistance < 0 || DesiredMinDistance > DesiredMaxDistance)
			return Fail("BattleAiCandidateRequest desired distance range is invalid.");
		if (MaxCandidateCount <= 0)
			return Fail("BattleAiCandidateRequest max_candidate_count must be > 0.");
		if (!ValidatePathBudget())
			return false;
		var maxDestinations = 0;
		if (PathSearchBudget.ContainsKey("max_destinations"))
			maxDestinations = PathSearchBudget["max_destinations"].AsInt32();
		if (maxDestinations > 0 && MaxCandidateCount > maxDestinations)
			return Fail("max_candidate_count must not exceed path_search_budget.max_destinations.");
		if (!ValidateMoveToRangeTacticalParams())
			return false;
		if (!ValidateRuntimeMetadata())
			return false;
		foreach (var payload in new[] { PathSearchBudget, TacticalParams, RuntimeMetadata })
		{
			if (!BattleAiPayloadGuard.ValidateNoForbiddenObject(payload, "BattleAiCandidateRequest"))
				return false;
		}
		return true;
	}

	private bool ValidatePathBudget()
	{
		foreach (var key in PathSearchBudget.Keys)
		{
			if (!PathBudgetKeys.Contains(key.ToString()))
				return Fail($"Unsupported path_search_budget key {key}.");
		}
		if (!PathSearchBudget.ContainsKey("max_cost") || PathSearchBudget["max_cost"].VariantType != Variant.Type.Int)
			return Fail("path_search_budget.max_cost must be int.");
		foreach (var key in new[] { "max_cost", "max_nodes", "max_destinations", "path_tree_min_destination_count" })
		{
			if (PathSearchBudget.ContainsKey(key))
			{
				var v = PathSearchBudget[key];
				if (v.VariantType != Variant.Type.Int || v.AsInt32() < 0)
					return Fail($"path_search_budget.{key} must be int >= 0.");
			}
		}
		foreach (var key in new[] { "include_origin", "prefer_progress" })
		{
			if (PathSearchBudget.ContainsKey(key) && PathSearchBudget[key].VariantType != Variant.Type.Bool)
				return Fail($"path_search_budget.{key} must be bool.");
		}
		return true;
	}

	private bool ValidateMoveToRangeTacticalParams()
	{
		foreach (var key in TacticalParams.Keys)
		{
			if (!MoveToRangeTacticalKeys.Contains(key.ToString()))
				return Fail($"Unsupported tactical_params key {key}.");
		}
		if (TacticalParams.ContainsKey("target_selector") && TacticalParams["target_selector"].VariantType != Variant.Type.StringName)
			return Fail("tactical_params.target_selector must be StringName.");
		if (TacticalParams.ContainsKey("position_objective_kind") && TacticalParams["position_objective_kind"].VariantType != Variant.Type.StringName)
			return Fail("tactical_params.position_objective_kind must be StringName.");
		if (TacticalParams.ContainsKey("range_skill_ids") && TacticalParams["range_skill_ids"].VariantType != Variant.Type.Array)
			return Fail("tactical_params.range_skill_ids must be Array.");
		if (TacticalParams.ContainsKey("range_skill_ids") && TacticalParams["range_skill_ids"].VariantType == Variant.Type.Array)
		{
			var arr = TacticalParams["range_skill_ids"].AsGodotArray();
			foreach (var rawSkillId in arr)
			{
				if (rawSkillId.VariantType != Variant.Type.String && rawSkillId.VariantType != Variant.Type.StringName)
					return Fail("tactical_params.range_skill_ids elements must be StringName/String.");
			}
		}
		return true;
	}

	private bool ValidateRuntimeMetadata()
	{
		foreach (var key in RuntimeMetadata.Keys)
		{
			if (!MoveToRangeRuntimeKeys.Contains(key.ToString()))
				return Fail($"Unsupported runtime_metadata key {key}.");
		}
		foreach (var key in MoveToRangeRuntimeKeys)
		{
			if (RuntimeMetadata.ContainsKey(key) && RuntimeMetadata[key].VariantType != Variant.Type.Int)
				return Fail($"runtime_metadata.{key} must be int.");
		}
		return true;
	}

	private bool Fail(string message)
	{
		return BattleAiPayloadGuard.FailLoud(message, new GDictionary { ["source"] = "BattleAiCandidateRequest" });
	}
}
