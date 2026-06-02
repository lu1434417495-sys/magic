using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

public partial class run_battle_ai_failure_policy_regression : SceneTree
{
    private const string AbortProcessSetting = "battle_ai/fail_loud_abort_process";
    private const string FailureModeSetting = "battle_ai/failure_policy_mode";

    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        Variant previousAbortProcess = ProjectSettings.GetSetting(AbortProcessSetting, false);
        Variant previousFailureMode = ProjectSettings.GetSetting(FailureModeSetting, "");

        try
        {
            ConfigureRuntimeFaultMode();

            TestPolicyAndEventArePlainTypedCSharp();
            TestReportStoresTypedFailureEventSnapshots();
            TestPayloadGuardProjectsGodotMetadataAtBoundary();
            TestConfiguredModeControlsAbortDecisionWithoutReporting();
        }
        catch (Exception exception)
        {
            _failures.Add($"Unhandled exception: {exception}");
        }
        finally
        {
            BattleAiPayloadGuard.FailLoudProcessAbortEnabled = false;
            BattleAiFailurePolicy.Reset();
            ProjectSettings.SetSetting(AbortProcessSetting, previousAbortProcess);
            ProjectSettings.SetSetting(FailureModeSetting, previousFailureMode);
        }

        if (_failures.Count == 0)
        {
            GD.Print("Battle AI failure policy regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle AI failure policy regression: FAIL ({_failures.Count})");
        return 1;
    }

    private static void ConfigureRuntimeFaultMode()
    {
        BattleAiPayloadGuard.FailLoudProcessAbortEnabled = false;
        BattleAiFailurePolicy.Reset();
        ProjectSettings.SetSetting(AbortProcessSetting, false);
        ProjectSettings.SetSetting(FailureModeSetting, BattleAiFailurePolicy.ModeRuntimeFault.ToString());
    }

    private void TestPolicyAndEventArePlainTypedCSharp()
    {
        Type policyType = typeof(BattleAiFailurePolicy);
        AssertTrue(
            policyType.IsAbstract && policyType.IsSealed,
            "BattleAiFailurePolicy 应是 plain static C# helper。"
        );
        AssertTrue(
            !typeof(GodotObject).IsAssignableFrom(policyType),
            "BattleAiFailurePolicy 不应继承 GodotObject/RefCounted。"
        );
        AssertTrue(
            policyType.GetCustomAttribute<GlobalClassAttribute>() == null,
            "BattleAiFailurePolicy 不应注册 GlobalClass。"
        );
        AssertPublicApiDoesNotExposeGodotCollections(policyType, "BattleAiFailurePolicy");

        Type eventType = typeof(BattleAiFailureEvent);
        AssertTrue(!eventType.IsAbstract, "BattleAiFailureEvent 应是普通 typed event 快照。");
        AssertTrue(
            !typeof(GodotObject).IsAssignableFrom(eventType),
            "BattleAiFailureEvent 不应继承 GodotObject/RefCounted。"
        );
        AssertTrue(
            eventType.GetCustomAttribute<GlobalClassAttribute>() == null,
            "BattleAiFailureEvent 不应注册 GlobalClass。"
        );
        AssertPublicApiDoesNotExposeGodotCollections(eventType, "BattleAiFailureEvent");

        AssertEq(
            policyType.GetProperty("LastEvent")?.PropertyType,
            typeof(BattleAiFailureEvent),
            "LastEvent 应暴露 typed event，而不是 Godot Dictionary。"
        );

        Type guardType = typeof(BattleAiPayloadGuard);
        AssertPublicApiDoesNotExposeGodotCollections(guardType, "BattleAiPayloadGuard");
    }

    private void TestReportStoresTypedFailureEventSnapshots()
    {
        ConfigureRuntimeFaultMode();

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["context"] = "action.plan",
            ["reason"] = null,
        };

        bool result = BattleAiFailurePolicy.ReportActionError("action failed", metadata);

        AssertTrue(!result, "ReportActionError 应返回 false，保持 guard 调用习惯。");
        AssertEq(BattleAiFailurePolicy.Events.Count, 1, "policy 应记录一条 typed event。");

        BattleAiFailureEvent failureEvent = BattleAiFailurePolicy.LastEvent;
        AssertTrue(failureEvent != null, "LastEvent 应指向最近 event。");
        if (failureEvent == null)
            return;

        AssertEq(failureEvent.Severity, BattleAiFailurePolicy.SeverityActionError, "severity 应保留。");
        AssertEq(failureEvent.Message, "action failed", "message 应保留。");
        AssertEq(failureEvent.Metadata["context"], "action.plan", "metadata context 应复制。");
        AssertEq(failureEvent.Metadata["reason"], "", "null metadata value 应规整为空字符串。");

        metadata["context"] = "mutated";
        AssertEq(failureEvent.Metadata["context"], "action.plan", "event metadata 不应引用调用方字典。");

        BattleAiFailurePolicy.Reset();
        AssertEq(BattleAiFailurePolicy.Events.Count, 0, "Reset 应清空 typed event 列表。");
        AssertTrue(BattleAiFailurePolicy.LastEvent == null, "Reset 应清空 LastEvent。");
    }

    private void TestPayloadGuardProjectsGodotMetadataAtBoundary()
    {
        ConfigureRuntimeFaultMode();

        var metadata = new Godot.Collections.Dictionary
        {
            ["context"] = "score_input.runtime_action_metadata",
            ["attempt"] = 2,
            [new StringName("stage")] = new StringName("scoring"),
        };

        bool result = BattleAiPayloadGuard.FailLoud("contract failed", metadata);

        AssertTrue(!result, "PayloadGuard.FailLoud 应返回 false。");
        AssertEq(BattleAiFailurePolicy.Events.Count, 1, "PayloadGuard 应向 policy 记录 typed event。");

        BattleAiFailureEvent failureEvent = BattleAiFailurePolicy.LastEvent;
        AssertTrue(failureEvent != null, "PayloadGuard 应设置 LastEvent。");
        if (failureEvent == null)
            return;

        AssertEq(
            failureEvent.Severity,
            BattleAiFailurePolicy.SeverityContractError,
            "FailLoud 应记录 contract_error severity。"
        );
        AssertEq(
            failureEvent.Metadata["context"],
            "score_input.runtime_action_metadata",
            "PayloadGuard 应把 Godot metadata key/value 投影为 typed string metadata。"
        );
        AssertEq(failureEvent.Metadata["attempt"], "2", "int metadata value 应投影为 string。");
        AssertEq(failureEvent.Metadata["stage"], "scoring", "StringName metadata value 应投影为 string。");
    }

    private void TestConfiguredModeControlsAbortDecisionWithoutReporting()
    {
        ConfigureRuntimeFaultMode();

        AssertEq(BattleAiFailurePolicy.Mode, BattleAiFailurePolicy.ModeRuntimeFault, "默认 mode 应是 runtime_fault。");
        AssertTrue(!BattleAiFailurePolicy.ShouldAbortProcess(), "runtime_fault 且 abort setting=false 时不应请求终止进程。");

        ProjectSettings.SetSetting(FailureModeSetting, BattleAiFailurePolicy.ModeStrictAbort.ToString());
        AssertTrue(BattleAiFailurePolicy.ShouldAbortProcess(), "ProjectSettings strict_abort 应请求终止进程。");

        ProjectSettings.SetSetting(FailureModeSetting, "");
        BattleAiFailurePolicy.SetMode(BattleAiFailurePolicy.ModeStrictAbort);
        AssertEq(BattleAiFailurePolicy.Mode, BattleAiFailurePolicy.ModeStrictAbort, "SetMode 应接受 strict_abort。");
        AssertTrue(BattleAiFailurePolicy.ShouldAbortProcess(), "未配置 ProjectSettings 时 strict_abort mode 应请求终止进程。");

        BattleAiFailurePolicy.SetMode("unknown_mode");
        AssertEq(BattleAiFailurePolicy.Mode, BattleAiFailurePolicy.ModeRuntimeFault, "未知 mode 应规整为 runtime_fault。");
    }

    private void AssertPublicApiDoesNotExposeGodotCollections(Type type, string label)
    {
        const BindingFlags flags =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (FieldInfo field in type.GetFields(flags))
        {
            AssertTrue(
                !IsGodotDynamicBoundaryType(field.FieldType),
                $"{label}.{field.Name} 不应暴露 Godot Dictionary/Array/Variant。"
            );
        }

        foreach (PropertyInfo property in type.GetProperties(flags))
        {
            AssertTrue(
                !IsGodotDynamicBoundaryType(property.PropertyType),
                $"{label}.{property.Name} 不应暴露 Godot Dictionary/Array/Variant。"
            );
        }

        foreach (MethodInfo method in type.GetMethods(flags))
        {
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                AssertTrue(
                    !IsGodotDynamicBoundaryType(parameter.ParameterType),
                    $"{label}.{method.Name}({parameter.Name}) 不应接收 Godot Dictionary/Array/Variant。"
                );
            }
        }
    }

    private static bool IsGodotDynamicBoundaryType(Type type) =>
        type == typeof(Godot.Collections.Dictionary)
        || type == typeof(Godot.Collections.Array)
        || type == typeof(Variant)
        || type.FullName == "Godot.Collections.Dictionary"
        || type.FullName == "Godot.Collections.Array";

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertEq(Type actual, Type expected, string message)
    {
        if (actual != expected)
        {
            _failures.Add($"{message} expected={expected} actual={actual}");
        }
    }

    private void AssertEq(StringName actual, StringName expected, string message)
    {
        if (actual != expected)
        {
            _failures.Add($"{message} expected={expected} actual={actual}");
        }
    }

    private void AssertEq(string actual, string expected, string message)
    {
        if (actual != expected)
        {
            _failures.Add($"{message} expected={expected} actual={actual}");
        }
    }

    private void AssertEq(int actual, int expected, string message)
    {
        if (actual != expected)
        {
            _failures.Add($"{message} expected={expected} actual={actual}");
        }
    }
}
