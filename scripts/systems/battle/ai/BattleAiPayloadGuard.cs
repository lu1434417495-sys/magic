using System;
using System.Collections.Generic;
using Godot;

internal static class BattleAiPayloadGuard
{
    private const int MaxPayloadDepth = 12;

    private static bool _failLoudProcessAbortEnabled = false;

    internal static bool FailLoudProcessAbortEnabled
    {
        get => _failLoudProcessAbortEnabled;
        set => _failLoudProcessAbortEnabled = value;
    }

    internal static bool ValidateNoForbiddenObject(
        IReadOnlyDictionary<string, object> value,
        string context
    )
    {
        string error = FindForbiddenInTypedMap(value, context, 0);
        return string.IsNullOrEmpty(error) || FailLoud(error, FailureContext(context));
    }

    internal static bool ValidateNoForbiddenObject<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue> value,
        string context
    )
    {
        string error = FindForbiddenInTypedObject(value, context, 0);
        return string.IsNullOrEmpty(error) || FailLoud(error, FailureContext(context));
    }

    internal static bool ValidateNoForbiddenObject(
        Godot.Collections.Array<StringName> value,
        string context
    )
    {
        string error = FindForbiddenInTypedObject(value, context, 0);
        return string.IsNullOrEmpty(error) || FailLoud(error, FailureContext(context));
    }

    internal static bool ValidateNoForbiddenObject(
        Godot.Collections.Array<Vector2I> value,
        string context
    )
    {
        string error = FindForbiddenInTypedObject(value, context, 0);
        return string.IsNullOrEmpty(error) || FailLoud(error, FailureContext(context));
    }

    internal static bool ValidateNoForbiddenObject(AttackPreviewData value, string context)
    {
        if (value == null)
            return true;

        string error = FindForbiddenInTypedObject(value.SummaryText, $"{context}.SummaryText", 0);
        if (!string.IsNullOrEmpty(error))
            return FailLoud(error, FailureContext(context));

        error = FindForbiddenInTypedObject(value.Stages, $"{context}.Stages", 0);
        if (!string.IsNullOrEmpty(error))
            return FailLoud(error, FailureContext(context));

        error = FindForbiddenInTypedObject(value.Source, $"{context}.Source", 0);
        if (!string.IsNullOrEmpty(error))
            return FailLoud(error, FailureContext(context));

        error = FindForbiddenInTypedObject(
            value.AttackRollModifierBreakdownTyped,
            $"{context}.AttackRollModifierBreakdownTyped",
            0
        );
        return string.IsNullOrEmpty(error) || FailLoud(error, FailureContext(context));
    }

    internal static bool ValidateNoForbiddenObject(
        BattleAiScoreRuntimeMetadata value,
        string context
    )
    {
        string error = FindForbiddenInTypedObject(value, context, 0);
        return string.IsNullOrEmpty(error) || FailLoud(error, FailureContext(context));
    }

    internal static bool ValidateNoForbiddenObject(
        BattleSpecialProfilePreviewFacts value,
        string context
    )
    {
        string error = FindForbiddenInTypedObject(value, context, 0);
        return string.IsNullOrEmpty(error) || FailLoud(error, FailureContext(context));
    }

    internal static bool ValidateNoForbiddenObject(
        IEnumerable<MeteorSwarmNumericSummary> value,
        string context
    )
    {
        string error = FindForbiddenInTypedObject(value, context, 0);
        return string.IsNullOrEmpty(error) || FailLoud(error, FailureContext(context));
    }

    internal static bool ValidateNoForbiddenObject(
        IEnumerable<BattleAttackRollModifierSpec> value,
        string context
    )
    {
        string error = FindForbiddenInTypedObject(value, context, 0);
        return string.IsNullOrEmpty(error) || FailLoud(error, FailureContext(context));
    }

    internal static bool ValidateNoForbiddenObject(EquipmentInstanceState value, string context)
    {
        string error = FindForbiddenInTypedObject(value, context, 0);
        return string.IsNullOrEmpty(error) || FailLoud(error, FailureContext(context));
    }

    internal static bool ValidateNoForbiddenObject(
        BattleDamagePreviewRangeService.SkillDamagePreview? value,
        string context
    )
    {
        string error = value == null ? null : FindForbiddenInTypedObject(value.Value, context, 0);
        return string.IsNullOrEmpty(error) || FailLoud(error, FailureContext(context));
    }

    internal static bool ValidateNoForbiddenObject(IEnumerable<StringName> value, string context)
    {
        string error = FindForbiddenInTypedObject(value, context, 0);
        return string.IsNullOrEmpty(error) || FailLoud(error, FailureContext(context));
    }

    internal static bool ValidateNoForbiddenObject(IEnumerable<string> value, string context)
    {
        string error = FindForbiddenInTypedObject(value, context, 0);
        return string.IsNullOrEmpty(error) || FailLoud(error, FailureContext(context));
    }

    internal static bool ValidateNoForbiddenObject(IEnumerable<Vector2I> value, string context)
    {
        string error = FindForbiddenInTypedObject(value, context, 0);
        return string.IsNullOrEmpty(error) || FailLoud(error, FailureContext(context));
    }

    internal static void AbortFailLoudProcessIfRequested()
    {
        if (FailLoudProcessAbortEnabled)
            BattleAiFailurePolicy.StrictProcessAbortEnabled = true;

        if (BattleAiFailurePolicy.ShouldAbortProcess())
            BattleAiFailurePolicy.AbortProcessNow();
    }

    internal static bool FailLoud(
        string message,
        IReadOnlyDictionary<string, string> metadata = null
    )
    {
        if (FailLoudProcessAbortEnabled)
            BattleAiFailurePolicy.StrictProcessAbortEnabled = true;

        return BattleAiFailurePolicy.ReportContractError(message, CopyFailureMetadata(metadata));
    }

    internal static bool ActionError(
        string message,
        IReadOnlyDictionary<string, string> metadata = null
    )
    {
        if (FailLoudProcessAbortEnabled)
            BattleAiFailurePolicy.StrictProcessAbortEnabled = true;

        return BattleAiFailurePolicy.ReportActionError(message, CopyFailureMetadata(metadata));
    }

    internal static bool MutationViolation(
        string message,
        IReadOnlyDictionary<string, string> metadata = null
    )
    {
        if (FailLoudProcessAbortEnabled)
            BattleAiFailurePolicy.StrictProcessAbortEnabled = true;

        return BattleAiFailurePolicy.ReportMutationViolation(message, CopyFailureMetadata(metadata));
    }

    internal static bool CommandIsValueObject(BattleCommand command)
    {
        if (command == null)
            return false;

        return ValidateNoForbiddenObject(command.TargetUnitIdsTyped, "command.target_unit_ids")
            && ValidateNoForbiddenObject(command.TargetCoordsTyped, "command.target_coords")
            && ValidateNoForbiddenObject(
                command.EquipmentOccupiedSlotIdsTyped,
                "command.equipment_occupied_slot_ids"
            )
            && ValidateNoForbiddenObject(command.equipment_instance, "command.equipment_instance");
    }

    internal static bool PreviewHasNoLiveState(BattlePreview preview)
    {
        if (preview == null)
            return true;

        if (!ValidateNoForbiddenObject(preview.LogLinesTyped, "preview.log_lines"))
            return false;
        if (!ValidateNoForbiddenObject(preview.TargetUnitIdsTyped, "preview.target_unit_ids"))
            return false;
        if (!ValidateNoForbiddenObject(preview.TargetCoordsTyped, "preview.target_coords"))
            return false;
        if (
            !ValidateNoForbiddenObject(
                preview.RandomChainCandidateUnitIdsTyped,
                "preview.random_chain_candidate_unit_ids"
            )
        )
            return false;
        if (!ValidateNoForbiddenObject(preview.hit_preview, "preview.hit_preview"))
            return false;
        if (!ValidateNoForbiddenObject(preview.DamagePreviewTyped, "preview.damage_preview"))
            return false;

        BattleSpecialProfileGateResult gateResult = preview.special_profile_gate_result;
        if (gateResult != null)
        {
            if (
                !ValidateNoForbiddenObject(
                    gateResult.DebugDetails,
                    "preview.special_profile_gate_result.debug_details"
                )
            )
                return false;
        }

        BattleSpecialProfilePreviewFacts previewFacts = preview.special_profile_preview_facts;
        return previewFacts == null
            || ValidateNoForbiddenObject(previewFacts, "preview.special_profile_preview_facts");
    }

    internal static bool ScoreInputHasNoLiveState(BattleAiScoreInput scoreInput)
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
                FailureContext("score_input.skill_def")
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

    private static string FindForbiddenInTypedMap(
        IReadOnlyDictionary<string, object> payload,
        string path,
        int depth
    )
    {
        if (payload == null)
            return "";
        if (depth > MaxPayloadDepth)
            return $"{path} exceeds typed payload depth.";

        foreach (KeyValuePair<string, object> entry in payload)
        {
            string keyText = entry.Key ?? "";
            if (LooksLikeRuntimePayload(keyText))
                return $"{path}.key contains unsupported runtime payload.";

            object value = entry.Value;
            string valueText = value?.ToString() ?? "";
            if (LooksLikeRuntimePayload(valueText))
                return $"{path}.{keyText} contains unsupported runtime payload.";

            if (value is IReadOnlyDictionary<string, object> childMap)
            {
                string childError = FindForbiddenInTypedMap(
                    childMap,
                    $"{path}.{keyText}",
                    depth + 1
                );
                if (!string.IsNullOrEmpty(childError))
                    return childError;
            }
            else if (value is IEnumerable<string> strings)
            {
                int index = 0;
                foreach (string item in strings)
                {
                    if (LooksLikeRuntimePayload(item ?? ""))
                        return $"{path}.{keyText}[{index}] contains unsupported runtime payload.";
                    index++;
                }
            }
        }

        return "";
    }

    private static string FindForbiddenInTypedObject(object value, string path, int depth)
    {
        if (value == null)
            return "";
        if (depth > MaxPayloadDepth)
            return $"{path} exceeds typed payload depth.";

        switch (value)
        {
            case string text:
                return LooksLikeRuntimePayload(text) ? $"{path} contains unsupported runtime payload." : "";
            case StringName stringName:
                return LooksLikeRuntimePayload(stringName.ToString())
                    ? $"{path} contains unsupported runtime payload."
                    : "";
            case System.Collections.IDictionary dictionary:
                return FindForbiddenInTypedDictionary(dictionary, path, depth + 1);
            case System.Collections.IEnumerable enumerable when value is not string:
                return FindForbiddenInTypedEnumerable(enumerable, path, depth + 1);
        }

        Type type = value.GetType();
        if (
            type.IsPrimitive
            || value is decimal
            || value is Vector2I
            || value is Vector2I?
            || value is int?
            || value is bool?
        )
        {
            string text = value.ToString();
            return LooksLikeRuntimePayload(text)
                ? $"{path} contains unsupported runtime payload."
                : "";
        }

        return FindForbiddenInReflectedMembers(value, type, path, depth + 1);
    }

    private static string FindForbiddenInTypedDictionary(
        System.Collections.IDictionary payload,
        string path,
        int depth
    )
    {
        if (payload == null)
            return "";
        if (depth > MaxPayloadDepth)
            return $"{path} exceeds typed payload depth.";

        foreach (System.Collections.DictionaryEntry entry in payload)
        {
            string keyText = entry.Key?.ToString() ?? "";
            if (LooksLikeRuntimePayload(keyText))
                return $"{path}.key contains unsupported runtime payload.";

            string childError = FindForbiddenInTypedObject(
                entry.Value,
                $"{path}.{keyText}",
                depth + 1
            );
            if (!string.IsNullOrEmpty(childError))
                return childError;
        }

        return "";
    }

    private static string FindForbiddenInTypedEnumerable(
        System.Collections.IEnumerable payload,
        string path,
        int depth
    )
    {
        if (payload == null)
            return "";
        if (depth > MaxPayloadDepth)
            return $"{path} exceeds typed payload depth.";

        int index = 0;
        foreach (object item in payload)
        {
            string childError = FindForbiddenInTypedObject(item, $"{path}[{index}]", depth + 1);
            if (!string.IsNullOrEmpty(childError))
                return childError;
            index++;
        }

        return "";
    }

    private static string FindForbiddenInReflectedMembers(
        object payload,
        Type type,
        string path,
        int depth
    )
    {
        if (payload == null)
            return "";
        if (depth > MaxPayloadDepth)
            return $"{path} exceeds typed payload depth.";

        foreach (
            System.Reflection.PropertyInfo property in type.GetProperties(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
            )
        )
        {
            if (!property.CanRead || property.GetIndexParameters().Length != 0)
                continue;
            string childError = FindForbiddenInTypedObject(
                property.GetValue(payload),
                $"{path}.{property.Name}",
                depth + 1
            );
            if (!string.IsNullOrEmpty(childError))
                return childError;
        }

        foreach (
            System.Reflection.FieldInfo field in type.GetFields(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
            )
        )
        {
            string childError = FindForbiddenInTypedObject(
                field.GetValue(payload),
                $"{path}.{field.Name}",
                depth + 1
            );
            if (!string.IsNullOrEmpty(childError))
                return childError;
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

    private static Dictionary<string, string> CopyFailureMetadata(
        IReadOnlyDictionary<string, string> metadata
    )
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        if (metadata == null)
            return result;

        foreach (KeyValuePair<string, string> entry in metadata)
        {
            if (string.IsNullOrEmpty(entry.Key))
                continue;

            result[entry.Key] = entry.Value ?? "";
        }

        return result;
    }

    private static Dictionary<string, string> FailureContext(string context)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["context"] = context ?? "",
        };
    }
}
