using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using System;

[GlobalClass]
public partial class BattleAiFailurePolicy : RefCounted
{
    private static readonly StringName _modeRuntimeFault = "runtime_fault";

    private static readonly StringName _modeStrictAbort = "strict_abort";

    private static readonly StringName _severityActionError = "action_error";

    private static readonly StringName _severityContractError = "contract_error";

    private static readonly StringName _severityMutationViolation = "mutation_violation";

    public static StringName ModeRuntimeFault() => _modeRuntimeFault;

    public static StringName ModeStrictAbort() => _modeStrictAbort;

    public static StringName SeverityActionError() => _severityActionError;

    public static StringName SeverityContractError() => _severityContractError;

    public static StringName SeverityMutationViolation() => _severityMutationViolation;

    public static StringName Mode = ModeRuntimeFault();

    public static bool StrictProcessAbortEnabled = false;

    public static GDictionary LastEvent = new();

    public static GArray Events = new();

    public static void Reset()
    {
        Mode = ModeRuntimeFault();

        StrictProcessAbortEnabled = false;

        LastEvent = new GDictionary();

        Events.Clear();
    }

    public static void SetMode(StringName newMode)
    {
        if (newMode == _modeStrictAbort)
            Mode = _modeStrictAbort;
        else
            Mode = _modeRuntimeFault;
    }

    public static bool ReportActionError(string message, GDictionary metadata = null)
    {
        return Report(_severityActionError, message, metadata);
    }

    public static bool ReportContractError(string message, GDictionary metadata = null)
    {
        return Report(_severityContractError, message, metadata);
    }

    public static bool ReportMutationViolation(string message, GDictionary metadata = null)
    {
        return Report(_severityMutationViolation, message, metadata);
    }

    public static bool Report(StringName severity, string message, GDictionary metadata = null)
    {
        GDictionary sanitized = metadata != null
            ? (GDictionary)metadata.Duplicate(true)
            : new GDictionary();

        var eventDict = new GDictionary
        {
            ["severity"] = severity,

            ["message"] = message,

            ["metadata"] = sanitized,
        };

        LastEvent = (GDictionary)eventDict.Duplicate(true);

        Events.Add(eventDict);

        GameLog.Error(message, "ai.failure.policy_triggered", "ai");

        if (ShouldAbortProcess())
            AbortProcessNow();

        return false;
    }

    public static bool ShouldAbortProcess()
    {
        if (StrictProcessAbortEnabled)
            return true;

        if ((bool)ProjectSettings.GetSetting("battle_ai/fail_loud_abort_process", false))
            return true;

        return ConfiguredMode() == ModeStrictAbort();
    }

    private const int AbortProcessGracePeriodMsec = 5000;

    private const int AbortProcessGraceStepMsec = 100;

    public static void AbortProcessNow()
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

        if (configuredText == _modeStrictAbort.ToString())
            return _modeStrictAbort;

        if (configuredText == _modeRuntimeFault.ToString())
            return _modeRuntimeFault;

        return Mode;
    }
}
