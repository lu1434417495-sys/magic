using Godot;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

internal sealed class BattleAiFailureEvent
{
    private readonly ReadOnlyDictionary<string, string> _metadata;

    public BattleAiFailureEvent(
        StringName severity,
        string message,
        IReadOnlyDictionary<string, string> metadata = null
    )
    {
        Severity = severity;
        Message = message ?? "";
        var metadataCopy = new Dictionary<string, string>(StringComparer.Ordinal);

        if (metadata != null)
        {
            foreach (KeyValuePair<string, string> entry in metadata)
            {
                if (string.IsNullOrEmpty(entry.Key))
                    continue;

                metadataCopy[entry.Key] = entry.Value ?? "";
            }
        }

        _metadata = new ReadOnlyDictionary<string, string>(metadataCopy);
    }

    public StringName Severity { get; }

    public string Message { get; }

    public IReadOnlyDictionary<string, string> Metadata => _metadata;
}

internal static class BattleAiFailurePolicy
{
    internal static readonly StringName ModeRuntimeFault = "runtime_fault";

    internal static readonly StringName ModeStrictAbort = "strict_abort";

    internal static readonly StringName SeverityActionError = "action_error";

    internal static readonly StringName SeverityContractError = "contract_error";

    internal static readonly StringName SeverityMutationViolation = "mutation_violation";

    private static readonly List<BattleAiFailureEvent> _events = new();

    internal static StringName Mode { get; private set; } = ModeRuntimeFault;

    internal static bool StrictProcessAbortEnabled = false;

    public static BattleAiFailureEvent LastEvent { get; private set; }

    internal static IReadOnlyList<BattleAiFailureEvent> Events => _events.AsReadOnly();

    internal static void Reset()
    {
        Mode = ModeRuntimeFault;

        StrictProcessAbortEnabled = false;

        LastEvent = null;

        _events.Clear();
    }

    internal static void SetMode(StringName newMode)
    {
        if (newMode == ModeStrictAbort)
            Mode = ModeStrictAbort;
        else
            Mode = ModeRuntimeFault;
    }

    internal static bool ReportActionError(
        string message,
        IReadOnlyDictionary<string, string> metadata = null
    )
    {
        return Report(SeverityActionError, message, metadata);
    }

    internal static bool ReportContractError(
        string message,
        IReadOnlyDictionary<string, string> metadata = null
    )
    {
        return Report(SeverityContractError, message, metadata);
    }

    internal static bool ReportMutationViolation(
        string message,
        IReadOnlyDictionary<string, string> metadata = null
    )
    {
        return Report(SeverityMutationViolation, message, metadata);
    }

    internal static bool Report(
        StringName severity,
        string message,
        IReadOnlyDictionary<string, string> metadata = null
    )
    {
        var failureEvent = new BattleAiFailureEvent(severity, message, metadata);

        LastEvent = failureEvent;
        _events.Add(failureEvent);

        GameLog.Error(message, "ai.failure.policy_triggered", "ai");

        if (ShouldAbortProcess())
            AbortProcessNow();

        return false;
    }

    internal static bool ShouldAbortProcess()
    {
        if (StrictProcessAbortEnabled)
            return true;

        if ((bool)ProjectSettings.GetSetting("battle_ai/fail_loud_abort_process", false))
            return true;

        return ConfiguredMode() == ModeStrictAbort;
    }

    private const int AbortProcessGracePeriodMsec = 5000;

    private const int AbortProcessGraceStepMsec = 100;

    internal static void AbortProcessNow()
    {
        if (OS.HasFeature("windows"))
        {
            OS.Execute(
                "taskkill",
                new[] { "/PID", OS.GetProcessId().ToString(), "/F" },
                null,
                false
            );

            int elapsedMsec = 0;

            while (elapsedMsec < AbortProcessGracePeriodMsec)
            {
                OS.DelayMsec(AbortProcessGraceStepMsec);

                elapsedMsec += AbortProcessGraceStepMsec;
            }
        }

        OS.Kill(OS.GetProcessId());
    }

    private static StringName ConfiguredMode()
    {
        var configured = ProjectSettings.GetSetting("battle_ai/failure_policy_mode", "");

        var configuredText = configured.ToString();

        if (configuredText == ModeStrictAbort.ToString())
            return ModeStrictAbort;

        if (configuredText == ModeRuntimeFault.ToString())
            return ModeRuntimeFault;

        return Mode;
    }
}
