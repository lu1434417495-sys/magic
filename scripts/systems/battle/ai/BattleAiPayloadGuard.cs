using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GArray = Godot.Collections.Array;

[GlobalClass]
public partial class BattleAiPayloadGuard : RefCounted
{
	private static readonly HashSet<string> LiveStateClassNames = new()
	{
		"BattleState",
		"BattleUnitState",
		"BattleCellState",
		"BattleGridService",
	};

	private static bool _failLoudProcessAbortEnabled = false;
	public static bool FailLoudProcessAbortEnabled
	{
		get => _failLoudProcessAbortEnabled;
		set => _failLoudProcessAbortEnabled = value;
	}
	public static bool GetFailLoudProcessAbortEnabled() => _failLoudProcessAbortEnabled;
	public static void SetFailLoudProcessAbortEnabled(bool value) => _failLoudProcessAbortEnabled = value;

	public static bool IsAllowedValuePayload(Variant value)
	{
		return string.IsNullOrEmpty(FindForbiddenObject(value, "payload"));
	}

	public static string FindForbiddenObject(Variant value, string path = "payload")
	{
		if (IsForbiddenObject(value))
		{
			var obj = value.AsGodotObject();
			return $"{path} contains forbidden object {obj.GetClass()}";
		}

		if (value.VariantType == Variant.Type.Dictionary)
		{
			var dict = value.AsGodotDictionary();
			foreach (var key in dict.Keys)
			{
				var keyError = FindForbiddenObject(key, $"{path}.key");
				if (!string.IsNullOrEmpty(keyError))
					return keyError;
				var valueError = FindForbiddenObject(dict[key], $"{path}.{key}");
				if (!string.IsNullOrEmpty(valueError))
					return valueError;
			}
			return "";
		}
		if (value.VariantType == Variant.Type.Array)
		{
			var arr = value.AsGodotArray();
			for (int i = 0; i < arr.Count; i++)
			{
				var itemError = FindForbiddenObject(arr[i], $"{path}[{i}]");
				if (!string.IsNullOrEmpty(itemError))
					return itemError;
			}
		}
		return "";
	}

	public static string FindLiveStateObject(Variant value, string path = "payload")
	{
		if (IsLiveStateObject(value, out var className))
			return $"{path} contains live state object {className}";

		if (value.VariantType == Variant.Type.Dictionary)
		{
			var dict = value.AsGodotDictionary();
			foreach (var key in dict.Keys)
			{
				var keyError = FindLiveStateObject(key, $"{path}.key");
				if (!string.IsNullOrEmpty(keyError))
					return keyError;
				var valueError = FindLiveStateObject(dict[key], $"{path}.{key}");
				if (!string.IsNullOrEmpty(valueError))
					return valueError;
			}
			return "";
		}
		if (value.VariantType == Variant.Type.Array)
		{
			var arr = value.AsGodotArray();
			for (int i = 0; i < arr.Count; i++)
			{
				var itemError = FindLiveStateObject(arr[i], $"{path}[{i}]");
				if (!string.IsNullOrEmpty(itemError))
					return itemError;
			}
		}
		return "";
	}

	public static bool ValidateNoForbiddenObject(Variant value, string context)
	{
		var error = FindForbiddenObject(value, context);
		if (string.IsNullOrEmpty(error))
			return true;
		return FailLoud(error, new GDictionary { ["context"] = context });
	}

	public static bool ValidateNoLiveStateObject(Variant value, string context)
	{
		var error = FindLiveStateObject(value, context);
		if (string.IsNullOrEmpty(error))
			return true;
		return FailLoud(error, new GDictionary { ["context"] = context });
	}

	public static void AbortFailLoudProcessIfRequested()
	{
		if (FailLoudProcessAbortEnabled)
			BattleAiFailurePolicy.StrictProcessAbortEnabled = true;
		if (BattleAiFailurePolicy.ShouldAbortProcess())
			BattleAiFailurePolicy.AbortProcessNow();
	}

	public static bool FailLoud(string message, GDictionary metadata = null)
	{
		if (FailLoudProcessAbortEnabled)
			BattleAiFailurePolicy.StrictProcessAbortEnabled = true;
		return BattleAiFailurePolicy.ReportContractError(message, metadata);
	}

	public static bool ActionError(string message, GDictionary metadata = null)
	{
		if (FailLoudProcessAbortEnabled)
			BattleAiFailurePolicy.StrictProcessAbortEnabled = true;
		return BattleAiFailurePolicy.ReportActionError(message, metadata);
	}

	public static bool MutationViolation(string message, GDictionary metadata = null)
	{
		if (FailLoudProcessAbortEnabled)
			BattleAiFailurePolicy.StrictProcessAbortEnabled = true;
		return BattleAiFailurePolicy.ReportMutationViolation(message, metadata);
	}

	public static Variant DuplicateValue(Variant value)
	{
		if (value.VariantType == Variant.Type.Dictionary)
			return value.AsGodotDictionary().Duplicate(true);
		if (value.VariantType == Variant.Type.Array)
			return value.AsGodotArray().Duplicate(true);
		return value;
	}

	public static bool CommandIsValueObject(GodotObject command)
	{
		if (command == null)
			return false;

		var payload = new GDictionary
		{
			["command_type"] = command.Get("command_type"),
			["unit_id"] = command.Get("unit_id"),
			["skill_id"] = command.Get("skill_id"),
			["skill_variant_id"] = command.Get("skill_variant_id"),
			["target_unit_id"] = command.Get("target_unit_id"),
			["target_unit_ids"] = command.Get("target_unit_ids").AsGodotArray().Duplicate(),
			["target_coord"] = command.Get("target_coord"),
			["target_coords"] = command.Get("target_coords").AsGodotArray().Duplicate(),
			["equipment_operation"] = command.Get("equipment_operation"),
			["equipment_slot_id"] = command.Get("equipment_slot_id"),
			["equipment_item_id"] = command.Get("equipment_item_id"),
			["equipment_instance_id"] = command.Get("equipment_instance_id"),
			["equipment_instance"] = command.Get("equipment_instance").AsGodotDictionary().Duplicate(true),
			["equipment_occupied_slot_ids"] = command.Get("equipment_occupied_slot_ids").AsGodotArray().Duplicate(),
		};
		return ValidateNoForbiddenObject(payload, "command");
	}

	public static bool PreviewHasNoLiveState(GodotObject preview)
	{
		if (preview == null)
			return true;

		var previewPayload = new GDictionary
		{
			["allowed"] = preview.Get("allowed"),
			["log_lines"] = preview.Get("log_lines").AsGodotArray().Duplicate(),
			["target_unit_ids"] = preview.Get("target_unit_ids").AsGodotArray().Duplicate(),
			["target_coords"] = preview.Get("target_coords").AsGodotArray().Duplicate(),
			["random_chain_candidate_unit_ids"] = preview.Get("random_chain_candidate_unit_ids").AsGodotArray().Duplicate(),
			["resolved_anchor_coord"] = preview.Get("resolved_anchor_coord"),
			["move_cost"] = preview.Get("move_cost"),
			["hit_preview"] = preview.Get("hit_preview").AsGodotDictionary().Duplicate(true),
			["damage_preview"] = preview.Get("damage_preview").AsGodotDictionary().Duplicate(true),
		};
		if (!ValidateNoForbiddenObject(previewPayload, "preview"))
			return false;
		if (!ValidateNoForbiddenObject(preview.Get("hit_preview").AsGodotDictionary(), "preview.hit_preview"))
			return false;
		if (!ValidateNoForbiddenObject(preview.Get("damage_preview").AsGodotDictionary(), "preview.damage_preview"))
			return false;

		var gateResult = preview.Get("special_profile_gate_result").AsGodotObject();
		if (gateResult != null)
		{
			var gatePayload = new GDictionary
			{
				["allowed"] = gateResult.Get("allowed"),
				["profile_id"] = gateResult.Get("profile_id"),
				["skill_id"] = gateResult.Get("skill_id"),
				["block_code"] = gateResult.Get("block_code"),
				["player_message"] = gateResult.Get("player_message"),
				["debug_details"] = gateResult.Get("debug_details").AsGodotDictionary().Duplicate(true),
			};
			if (!ValidateNoForbiddenObject(gatePayload, "preview.special_profile_gate_result"))
				return false;
		}

		var previewFacts = preview.Get("special_profile_preview_facts").AsGodotObject();
		if (previewFacts != null)
		{
			if (previewFacts.HasMethod("to_dict"))
				return ValidateNoForbiddenObject(previewFacts.Call("to_dict"), "preview.special_profile_preview_facts");

			var message = "preview.special_profile_preview_facts must expose to_dict().";
			return FailLoud(message, new GDictionary { ["context"] = "preview.special_profile_preview_facts" });
		}
		return true;
	}

	private static bool IsForbiddenObject(Variant value)
	{
		if (value.VariantType == Variant.Type.Callable)
			return true;
		if (value.Obj is GodotObject)
			return true;
		return false;
	}

	private static bool IsLiveStateObject(Variant value, out string className)
	{
		className = null;
		if (value.Obj is not GodotObject go)
			return false;

		className = go.GetClass();
		return LiveStateClassNames.Contains(className);
	}
}
