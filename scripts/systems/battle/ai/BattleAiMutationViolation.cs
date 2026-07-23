using System;
using System.Collections.Generic;
using System.Diagnostics;
using Godot;

internal sealed class BattleAiMutationViolationException : InvalidOperationException
{
    internal BattleAiMutationViolationException(
        BattleAiMutationViolationReport report,
        Exception innerException = null
    )
        : base(
            report?.Message ?? "AI mutation guard detected an implicit state mutation.",
            innerException
        )
    {
        Report = report;
    }

    internal BattleAiMutationViolationReport Report { get; }
}

internal sealed class BattleAiMutationViolationReport
{
    private readonly List<string> _violations;

    internal BattleAiMutationViolationReport(
        BattleAiContext context,
        IEnumerable<string> violations,
        string stage,
        BattleAiRuntimeActionEntry actionEntry,
        int actionIndex,
        string callSite
    )
    {
        _violations = new List<string>();
        foreach (string violation in violations ?? Array.Empty<string>())
        {
            if (!string.IsNullOrEmpty(violation))
            {
                _violations.Add(violation);
            }
        }

        Stage = string.IsNullOrEmpty(stage) ? "decision" : stage;
        UnitId = context?.unit_state?.unit_id.ToString() ?? "";
        UnitName = context?.unit_state?.display_name ?? "";
        BattleId = context?.state?.battle_id.ToString() ?? "";
        ActionIndex = actionIndex;
        ActionId = actionEntry?.Action?.ActionId.ToString() ?? "";
        ActionType = actionEntry?.Action?.GetType().FullName ?? "";
        SuspectedCallSite =
            !string.IsNullOrEmpty(callSite)
                ? callSite
                : BuildActionCallSite(actionEntry, actionIndex);
        DetectionStack = new StackTrace(1, true).ToString();
        Message = BuildMessage();
    }

    internal IReadOnlyList<string> Violations => _violations;

    internal string Stage { get; }

    internal string UnitId { get; }

    internal string UnitName { get; }

    internal string BattleId { get; }

    internal int ActionIndex { get; }

    internal string ActionId { get; }

    internal string ActionType { get; }

    internal string SuspectedCallSite { get; }

    internal string DetectionStack { get; }

    internal string Message { get; }

    internal static string BuildActionCallSite(
        BattleAiRuntimeActionEntry actionEntry,
        int actionIndex
    )
    {
        EnemyAiActionDefinition action = actionEntry?.Action;
        string actionType = action?.GetType().FullName ?? "unknown_action_type";
        string actionId = action?.ActionId.ToString() ?? "";
        string indexText = actionIndex >= 0 ? actionIndex.ToString() : "?";
        return $"action[{indexText}] {actionType}.Evaluate(action_id={actionId})";
    }

    internal Dictionary<string, string> ToMetadata()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["stage"] = Stage,
            ["battle_id"] = BattleId,
            ["unit_id"] = UnitId,
            ["unit_name"] = UnitName,
            ["action_index"] = ActionIndex.ToString(),
            ["action_id"] = ActionId,
            ["action_type"] = ActionType,
            ["suspected_call_site"] = SuspectedCallSite,
            ["first_violation"] = _violations.Count > 0 ? _violations[0] : "",
        };
    }

    private string BuildMessage()
    {
        string violationText =
            _violations.Count > 0 ? string.Join("; ", _violations) : "(no diff details)";
        string actionText = string.IsNullOrEmpty(ActionType)
            ? "unknown action"
            : $"{ActionType} action_id={ActionId}";
        return
            "AI mutation guard abort: implicit battle state mutation detected. "
            + $"stage={Stage}; battle_id={BattleId}; unit={UnitName}({UnitId}); "
            + $"action={actionText}; suspected_call_site={SuspectedCallSite}; "
            + $"violations={violationText}; detection_stack={DetectionStack}";
    }
}
