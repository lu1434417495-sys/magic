using System;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleAiPayloadGuard : RefCounted
{
    private const int MaxPayloadDepth = 12;

    private static bool _failLoudProcessAbortEnabled = false;

    public static bool FailLoudProcessAbortEnabled
    {
        get => _failLoudProcessAbortEnabled;
        set => _failLoudProcessAbortEnabled = value;
    }

    public static bool GetFailLoudProcessAbortEnabled() => _failLoudProcessAbortEnabled;

    public static void SetFailLoudProcessAbortEnabled(bool value) =>
        _failLoudProcessAbortEnabled = value;

    internal static bool IsAllowedValuePayload(GDictionary value)
    {
        return string.IsNullOrEmpty(FindForbiddenObject(value, "payload"));
    }

    internal static bool IsAllowedValuePayload(GArray value)
    {
        return string.IsNullOrEmpty(FindForbiddenObject(value, "payload"));
    }

    internal static string FindForbiddenObject(GDictionary payload, string path = "payload")
    {
        return FindForbiddenInDictionary(payload, path, 0);
    }

    internal static string FindForbiddenObject(GArray payload, string path = "payload")
    {
        return FindForbiddenInArray(payload, path, 0);
    }

    internal static bool ValidateNoForbiddenObject(GDictionary value, string context)
    {
        string error = FindForbiddenObject(value, context);
        return string.IsNullOrEmpty(error)
            || FailLoud(error, new GDictionary { ["context"] = context });
    }

    internal static bool ValidateNoForbiddenObject(GArray value, string context)
    {
        string error = FindForbiddenObject(value, context);
        return string.IsNullOrEmpty(error)
            || FailLoud(error, new GDictionary { ["context"] = context });
    }

    internal static bool ValidateNoForbiddenObject(
        Godot.Collections.Array<StringName> value,
        string context
    )
    {
        return true;
    }

    internal static bool ValidateNoForbiddenObject(
        Godot.Collections.Array<Vector2I> value,
        string context
    )
    {
        return true;
    }

    internal static bool ValidateNoForbiddenObject(AttackPreviewData value, string context)
    {
        return true;
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

        return ValidateNoForbiddenObject(command.equipment_instance, "command.equipment_instance");
    }

    public static bool PreviewHasNoLiveState(BattlePreview preview)
    {
        if (preview == null)
            return true;

        if (!ValidateNoForbiddenObject(preview.log_lines, "preview.log_lines"))
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
        return previewFacts == null
            || ValidateNoForbiddenObject(
                previewFacts.ToDict(),
                "preview.special_profile_preview_facts"
            );
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
            );
    }

    private static string FindForbiddenInDictionary(GDictionary payload, string path, int depth)
    {
        if (payload == null)
            return "";
        if (depth > MaxPayloadDepth)
            return $"{path} exceeds typed payload depth.";

        foreach (var key in payload.Keys)
        {
            string keyText = key.ToString();
            if (LooksLikeRuntimePayload(keyText))
                return $"{path}.key contains unsupported runtime payload.";

            var value = payload[key];
            string valueText = value.ToString();
            if (LooksLikeRuntimePayload(valueText))
                return $"{path}.{keyText} contains unsupported runtime payload.";

            try
            {
                GDictionary child = value.AsGodotDictionary();
                string childError = FindForbiddenInDictionary(child, $"{path}.{keyText}", depth + 1);
                if (!string.IsNullOrEmpty(childError))
                    return childError;
            }
            catch
            {
            }

            try
            {
                GArray child = value.AsGodotArray();
                string childError = FindForbiddenInArray(child, $"{path}.{keyText}", depth + 1);
                if (!string.IsNullOrEmpty(childError))
                    return childError;
            }
            catch
            {
            }
        }

        return "";
    }

    private static string FindForbiddenInArray(GArray payload, string path, int depth)
    {
        if (payload == null)
            return "";
        if (depth > MaxPayloadDepth)
            return $"{path} exceeds typed payload depth.";

        for (int i = 0; i < payload.Count; i++)
        {
            var value = payload[i];
            string valueText = value.ToString();
            if (LooksLikeRuntimePayload(valueText))
                return $"{path}[{i}] contains unsupported runtime payload.";

            try
            {
                GDictionary child = value.AsGodotDictionary();
                string childError = FindForbiddenInDictionary(child, $"{path}[{i}]", depth + 1);
                if (!string.IsNullOrEmpty(childError))
                    return childError;
            }
            catch
            {
            }

            try
            {
                GArray child = value.AsGodotArray();
                string childError = FindForbiddenInArray(child, $"{path}[{i}]", depth + 1);
                if (!string.IsNullOrEmpty(childError))
                    return childError;
            }
            catch
            {
            }
        }

        return "";
    }

    private static bool LooksLikeRuntimePayload(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;
        return text.StartsWith("<Object", StringComparison.Ordinal)
            || text.StartsWith("<Callable", StringComparison.Ordinal)
            || text.StartsWith("[Object", StringComparison.Ordinal)
            || text.StartsWith("[Callable", StringComparison.Ordinal);
    }
}
