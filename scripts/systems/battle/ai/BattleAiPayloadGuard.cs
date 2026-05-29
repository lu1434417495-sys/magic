using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

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

    public static void SetFailLoudProcessAbortEnabled(bool value) =>
        _failLoudProcessAbortEnabled = value;

    internal static bool IsAllowedValuePayload(object value)
    {
        return string.IsNullOrEmpty(FindForbiddenObject(value, "payload"));
    }

    internal static string FindForbiddenObject(object payload, string path = "payload")
    {
        if (IsForbiddenObject(payload))
        {
            return $"{path} contains forbidden object {ForbiddenObjectName(payload)}";
        }

        if (payload is Variant value)
        {
            if (value.VariantType == Variant.Type.Dictionary)
            {
                return FindForbiddenObject(value.AsGodotDictionary(), path);
            }

            if (value.VariantType == Variant.Type.Array)
            {
                return FindForbiddenObject(value.AsGodotArray(), path);
            }
        }

        if (payload is GDictionary dict)
        {
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

        if (payload is GArray arr)
        {
            for (int i = 0; i < arr.Count; i++)
            {
                var itemError = FindForbiddenObject(arr[i], $"{path}[{i}]");

                if (!string.IsNullOrEmpty(itemError))
                    return itemError;
            }
        }

        return "";
    }

    internal static string FindLiveStateObject(object payload, string path = "payload")
    {
        if (IsLiveStateObject(payload, out var className))
            return $"{path} contains live state object {className}";

        if (payload is Variant value)
        {
            if (value.VariantType == Variant.Type.Dictionary)
            {
                return FindLiveStateObject(value.AsGodotDictionary(), path);
            }

            if (value.VariantType == Variant.Type.Array)
            {
                return FindLiveStateObject(value.AsGodotArray(), path);
            }
        }

        if (payload is GDictionary dict)
        {
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

        if (payload is GArray arr)
        {
            for (int i = 0; i < arr.Count; i++)
            {
                var itemError = FindLiveStateObject(arr[i], $"{path}[{i}]");

                if (!string.IsNullOrEmpty(itemError))
                    return itemError;
            }
        }

        return "";
    }

    internal static bool ValidateNoForbiddenObject(object value, string context)
    {
        var error = FindForbiddenObject(value, context);

        if (string.IsNullOrEmpty(error))
            return true;

        return FailLoud(error, new GDictionary { ["context"] = context });
    }

    internal static bool ValidateNoLiveStateObject(object value, string context)
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

    public static bool CommandIsValueObject(BattleCommand command)
    {
        if (command == null)
            return false;

        return ValidateNoForbiddenObject(command.target_unit_ids, "command.target_unit_ids")
            && ValidateNoForbiddenObject(command.target_coords, "command.target_coords")
            && ValidateNoForbiddenObject(command.equipment_instance, "command.equipment_instance")
            && ValidateNoForbiddenObject(
                command.equipment_occupied_slot_ids,
                "command.equipment_occupied_slot_ids"
            );
    }

    public static bool PreviewHasNoLiveState(BattlePreview preview)
    {
        if (preview == null)
            return true;

        if (!ValidateNoForbiddenObject(preview.log_lines, "preview.log_lines"))
            return false;
        if (!ValidateNoForbiddenObject(preview.target_unit_ids, "preview.target_unit_ids"))
            return false;
        if (!ValidateNoForbiddenObject(preview.target_coords, "preview.target_coords"))
            return false;
        if (
            !ValidateNoForbiddenObject(
                preview.random_chain_candidate_unit_ids,
                "preview.random_chain_candidate_unit_ids"
            )
        )
            return false;
        if (!ValidateNoForbiddenObject(preview.hit_preview, "preview.hit_preview"))
            return false;
        if (!ValidateNoForbiddenObject(preview.damage_preview, "preview.damage_preview"))
            return false;

        BattleSpecialProfileGateResult gateResult = preview.special_profile_gate_result;

        if (gateResult != null)
        {
            if (
                !ValidateNoForbiddenObject(
                    gateResult.debug_details,
                    "preview.special_profile_gate_result.debug_details"
                )
            )
                return false;
        }

        BattleSpecialProfilePreviewFacts previewFacts = preview.special_profile_preview_facts;

        if (previewFacts != null)
        {
            return ValidateNoForbiddenObject(
                previewFacts.ToDict(),
                "preview.special_profile_preview_facts"
            );
        }

        return true;
    }

    public static bool ScoreInputHasNoLiveState(BattleAiScoreInput scoreInput)
    {
        if (scoreInput == null)
            return false;
        if (scoreInput.command != null && !CommandIsValueObject(scoreInput.command))
            return false;
        if (scoreInput.preview != null && !PreviewHasNoLiveState(scoreInput.preview))
            return false;
        if (scoreInput.skill_def != null)
        {
            return FailLoud(
                "BattleAiScoreInput.skill_def must be stripped before leaving score assembly.",
                new GDictionary { ["context"] = "score_input.skill_def" }
            );
        }

        return ValidateNoForbiddenObject(
                scoreInput.runtime_action_metadata,
                "score_input.runtime_action_metadata"
            )
            && ValidateNoForbiddenObject(
                scoreInput.random_chain_candidate_unit_ids,
                "score_input.random_chain_candidate_unit_ids"
            )
            && ValidateNoForbiddenObject(
                scoreInput.estimated_lethal_target_ids,
                "score_input.estimated_lethal_target_ids"
            )
            && ValidateNoForbiddenObject(
                scoreInput.estimated_lethal_threat_target_ids,
                "score_input.estimated_lethal_threat_target_ids"
            )
            && ValidateNoForbiddenObject(
                scoreInput.estimated_control_target_ids,
                "score_input.estimated_control_target_ids"
            )
            && ValidateNoForbiddenObject(
                scoreInput.estimated_control_threat_target_ids,
                "score_input.estimated_control_threat_target_ids"
            )
            && ValidateNoForbiddenObject(
                scoreInput.save_estimates_by_target_id,
                "score_input.save_estimates_by_target_id"
            )
            && ValidateNoForbiddenObject(
                scoreInput.damage_estimates_by_target_id,
                "score_input.damage_estimates_by_target_id"
            )
            && ValidateNoForbiddenObject(
                scoreInput.special_profile_preview_facts,
                "score_input.special_profile_preview_facts"
            )
            && ValidateNoForbiddenObject(
                scoreInput.target_numeric_summary,
                "score_input.target_numeric_summary"
            )
            && ValidateNoForbiddenObject(
                scoreInput.friendly_fire_numeric_summary,
                "score_input.friendly_fire_numeric_summary"
            )
            && ValidateNoForbiddenObject(
                scoreInput.high_priority_target_ids,
                "score_input.high_priority_target_ids"
            )
            && ValidateNoForbiddenObject(
                scoreInput.high_priority_reasons,
                "score_input.high_priority_reasons"
            )
            && ValidateNoForbiddenObject(
                scoreInput.attack_roll_modifier_breakdown,
                "score_input.attack_roll_modifier_breakdown"
            )
            && ValidateNoForbiddenObject(
                scoreInput.path_step_hit_counts_by_unit_id,
                "score_input.path_step_hit_counts_by_unit_id"
            )
            && ValidateNoForbiddenObject(
                scoreInput.pre_action_threat_unit_ids,
                "score_input.pre_action_threat_unit_ids"
            )
            && ValidateNoForbiddenObject(
                scoreInput.post_action_remaining_threat_unit_ids,
                "score_input.post_action_remaining_threat_unit_ids"
            );
    }

    private static bool IsForbiddenObject(object payload)
    {
        if (payload is Variant value)
        {
            if (value.VariantType == Variant.Type.Callable)
                return true;

            if (value.Obj is GodotObject)
                return true;

            return false;
        }

        return payload is Callable || payload is GodotObject;
    }

    private static string ForbiddenObjectName(object payload)
    {
        if (payload is Variant value)
        {
            if (value.VariantType == Variant.Type.Callable)
                return "Callable";

            GodotObject obj = value.AsGodotObject();
            return obj != null ? obj.GetClass() : value.VariantType.ToString();
        }

        if (payload is GodotObject go)
            return go.GetClass();

        return payload?.GetType().Name ?? "null";
    }

    private static bool IsLiveStateObject(object payload, out string className)
    {
        className = null;

        GodotObject go = null;
        if (payload is Variant value)
        {
            go = value.Obj as GodotObject;
        }
        else if (payload is GodotObject obj)
        {
            go = obj;
        }

        if (go == null)
            return false;

        className = go.GetClass();

        return LiveStateClassNames.Contains(className);
    }
}
