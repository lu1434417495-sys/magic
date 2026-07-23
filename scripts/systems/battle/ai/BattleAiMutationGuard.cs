using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;

internal sealed class BattleAiMutationGuard
{
    internal const int MaxReportedViolations = 64;

    internal static readonly HashSet<string> AllowedActiveUnitFields =
        new(StringComparer.Ordinal) { "ai_brain_id", "ai_state_id" };

    internal static readonly HashSet<string> AllowedActiveBlackboardKeys =
        new(StringComparer.Ordinal)
        {
            "last_brain_id",
            "last_state_id",
            "last_action_id",
            "last_reason_text",
            "last_transition_previous_state_id",
            "last_transition_state_id",
            "last_transition_rule_id",
            "last_transition_reason",
        };

    private BattleAiMutationSnapshot _before_snapshot = BattleAiMutationSnapshot.Empty();
    private StringName _active_unit_id = "";

    internal bool Capture(BattleAiContext context)
    {
        if (!TryGetContextState(context, out _, out BattleUnitState unitState))
        {
            return false;
        }

        _active_unit_id = unitState.unit_id;
        _before_snapshot = BattleAiMutationSnapshot.Capture(context);
        return true;
    }

    internal List<string> ValidateTyped(BattleAiContext context)
    {
        if (_before_snapshot.IsEmpty || !TryGetContextState(context, out _, out _))
        {
            return new List<string>();
        }
        return _before_snapshot.Validate(context, _active_unit_id);
    }

    internal BattleAiMutationViolationReport ValidateReportTyped(
        BattleAiContext context,
        string stage,
        BattleAiRuntimeActionEntry actionEntry = null,
        int actionIndex = -1,
        string callSite = null
    )
    {
        List<string> violations = ValidateTyped(context);
        return violations.Count == 0
            ? null
            : new BattleAiMutationViolationReport(
                context,
                violations,
                stage,
                actionEntry,
                actionIndex,
                callSite
            );
    }

    internal static void CollectDiffs(
        StableMap expected,
        StableMap actual,
        string path,
        List<StableDiff> diffs
    )
    {
        CollectDictionaryDiffs(expected, actual, path, diffs);
    }

    internal static void CollectDiffs(
        StableValue expected,
        StableValue actual,
        string path,
        List<StableDiff> diffs
    )
    {
        if (diffs.Count >= MaxReportedViolations)
        {
            return;
        }
        if (expected.TryGetMap(out StableMap expectedMap) && actual.TryGetMap(out StableMap actualMap))
        {
            CollectDictionaryDiffs(expectedMap, actualMap, path, diffs);
            return;
        }
        if (
            expected.TryGetArray(out IReadOnlyList<StableValue> expectedArray)
            && actual.TryGetArray(out IReadOnlyList<StableValue> actualArray)
        )
        {
            CollectArrayDiffs(expectedArray, actualArray, path, diffs);
            return;
        }
        if (!expected.ScalarEquals(actual))
        {
            diffs.Add(StableDiff.Changed(path, expected, actual));
        }
    }

    private static void CollectDictionaryDiffs(
        StableMap expected,
        StableMap actual,
        string path,
        List<StableDiff> diffs
    )
    {
        foreach (KeyValuePair<string, StableValue> entry in expected.Entries)
        {
            if (diffs.Count >= MaxReportedViolations)
            {
                return;
            }
            string childPath = $"{path}.{entry.Key}";
            if (!actual.TryGet(entry.Key, out StableValue actualValue))
            {
                diffs.Add(StableDiff.Removed(childPath));
                continue;
            }
            CollectDiffs(entry.Value, actualValue, childPath, diffs);
        }

        foreach (KeyValuePair<string, StableValue> entry in actual.Entries)
        {
            if (diffs.Count >= MaxReportedViolations)
            {
                return;
            }
            if (expected.ContainsKey(entry.Key))
            {
                continue;
            }
            diffs.Add(StableDiff.Added($"{path}.{entry.Key}", entry.Value));
        }
    }

    private static void CollectArrayDiffs(
        IReadOnlyList<StableValue> expected,
        IReadOnlyList<StableValue> actual,
        string path,
        List<StableDiff> diffs
    )
    {
        if (expected.Count != actual.Count)
        {
            diffs.Add(StableDiff.SizeChanged(path, expected.Count, actual.Count));
            return;
        }
        for (int i = 0; i < expected.Count; i += 1)
        {
            if (diffs.Count >= MaxReportedViolations)
            {
                return;
            }
            CollectDiffs(expected[i], actual[i], $"{path}[{i}]", diffs);
        }
    }

    internal static List<string> FormatStableDiffs(IEnumerable<StableDiff> diffs)
    {
        List<string> result = new();
        foreach (StableDiff diff in diffs ?? System.Array.Empty<StableDiff>())
        {
            result.Add(diff.Format());
        }
        return result;
    }

    private static bool TryGetContextState(
        BattleAiContext context,
        out BattleState state,
        out BattleUnitState unitState
    )
    {
        state = context?.state;
        unitState = context?.unit_state;
        return context != null && state != null && unitState != null;
    }
}
