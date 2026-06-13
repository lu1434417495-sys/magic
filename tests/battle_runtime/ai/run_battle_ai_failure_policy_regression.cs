using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

public partial class run_battle_ai_failure_policy_regression : SceneTree
{
    private const string AbortProcessSetting = "battle_ai/fail_loud_abort_process";
    private const string FailureModeSetting = "battle_ai/failure_policy_mode";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        Variant previousAbortProcess = ProjectSettings.GetSetting(AbortProcessSetting, false);
        Variant previousFailureMode = ProjectSettings.GetSetting(FailureModeSetting, "");

        try
        {
            ConfigureRuntimeFaultMode();

            TestPolicyAndEventArePlainTypedCSharp();
            TestReportStoresTypedFailureEventSnapshots();
            TestPayloadGuardUsesTypedMetadataOnly();
            TestConfiguredModeControlsAbortDecisionWithoutReporting();
            Quit(_test.Finish("Battle AI failure policy regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            Quit(1);
        }
        finally
        {
            BattleAiPayloadGuard.FailLoudProcessAbortEnabled = false;
            BattleAiFailurePolicy.Reset();
            ProjectSettings.SetSetting(AbortProcessSetting, previousAbortProcess);
            ProjectSettings.SetSetting(FailureModeSetting, previousFailureMode);
        }
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
        _test.True(
            policyType.IsAbstract && policyType.IsSealed,
            "BattleAiFailurePolicy 应是 plain static C# helper。"
        );
        AssertPublicApiDoesNotExposeGodotCollections(policyType, "BattleAiFailurePolicy");

        Type eventType = typeof(BattleAiFailureEvent);
        _test.True(!eventType.IsAbstract, "BattleAiFailureEvent 应是普通 typed event 快照。");
        AssertPublicApiDoesNotExposeGodotCollections(eventType, "BattleAiFailureEvent");

        _test.Eq(
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

        _test.True(!result, "ReportActionError 应返回 false，保持 guard 调用习惯。");
        _test.Eq(BattleAiFailurePolicy.Events.Count, 1, "policy 应记录一条 typed event。");

        BattleAiFailureEvent failureEvent = BattleAiFailurePolicy.LastEvent;
        _test.True(failureEvent != null, "LastEvent 应指向最近 event。");
        if (failureEvent == null)
            return;

        _test.Eq(failureEvent.Severity, BattleAiFailurePolicy.SeverityActionError, "severity 应保留。");
        _test.Eq(failureEvent.Message, "action failed", "message 应保留。");
        _test.Eq(failureEvent.Metadata["context"], "action.plan", "metadata context 应复制。");
        _test.Eq(failureEvent.Metadata["reason"], "", "null metadata value 应规整为空字符串。");

        metadata["context"] = "mutated";
        _test.Eq(failureEvent.Metadata["context"], "action.plan", "event metadata 不应引用调用方字典。");

        BattleAiFailurePolicy.Reset();
        _test.Eq(BattleAiFailurePolicy.Events.Count, 0, "Reset 应清空 typed event 列表。");
        _test.True(BattleAiFailurePolicy.LastEvent == null, "Reset 应清空 LastEvent。");
    }

    private void TestPayloadGuardUsesTypedMetadataOnly()
    {
        ConfigureRuntimeFaultMode();

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["context"] = "score_input.runtime_action_metadata",
            ["attempt"] = "2",
            ["stage"] = "scoring",
        };

        bool result = BattleAiPayloadGuard.FailLoud("contract failed", metadata);

        _test.True(!result, "PayloadGuard.FailLoud 应返回 false。");
        _test.Eq(BattleAiFailurePolicy.Events.Count, 1, "PayloadGuard 应向 policy 记录 typed event。");

        BattleAiFailureEvent failureEvent = BattleAiFailurePolicy.LastEvent;
        _test.True(failureEvent != null, "PayloadGuard 应设置 LastEvent。");
        if (failureEvent == null)
            return;

        _test.Eq(
            failureEvent.Severity,
            BattleAiFailurePolicy.SeverityContractError,
            "FailLoud 应记录 contract_error severity。"
        );
        _test.Eq(
            failureEvent.Metadata["context"],
            "score_input.runtime_action_metadata",
            "PayloadGuard 应保留 typed metadata context。"
        );
        _test.Eq(failureEvent.Metadata["attempt"], "2", "typed metadata value 应保留。");
        _test.Eq(failureEvent.Metadata["stage"], "scoring", "typed metadata stage 应保留。");
    }

    private void TestConfiguredModeControlsAbortDecisionWithoutReporting()
    {
        ConfigureRuntimeFaultMode();

        _test.Eq(BattleAiFailurePolicy.Mode, BattleAiFailurePolicy.ModeRuntimeFault, "默认 mode 应是 runtime_fault。");
        _test.True(!BattleAiFailurePolicy.ShouldAbortProcess(), "runtime_fault 且 abort setting=false 时不应请求终止进程。");

        ProjectSettings.SetSetting(FailureModeSetting, BattleAiFailurePolicy.ModeStrictAbort.ToString());
        _test.True(BattleAiFailurePolicy.ShouldAbortProcess(), "ProjectSettings strict_abort 应请求终止进程。");

        ProjectSettings.SetSetting(FailureModeSetting, "");
        BattleAiFailurePolicy.SetMode(BattleAiFailurePolicy.ModeStrictAbort);
        _test.Eq(BattleAiFailurePolicy.Mode, BattleAiFailurePolicy.ModeStrictAbort, "SetMode 应接受 strict_abort。");
        _test.True(BattleAiFailurePolicy.ShouldAbortProcess(), "未配置 ProjectSettings 时 strict_abort mode 应请求终止进程。");

        BattleAiFailurePolicy.SetMode("unknown_mode");
        _test.Eq(BattleAiFailurePolicy.Mode, BattleAiFailurePolicy.ModeRuntimeFault, "未知 mode 应规整为 runtime_fault。");
    }

    private void AssertPublicApiDoesNotExposeGodotCollections(Type type, string label)
    {
        const BindingFlags flags =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (FieldInfo field in type.GetFields(flags))
        {
            _test.True(
                !IsGodotDynamicBoundaryType(field.FieldType),
                $"{label}.{field.Name} 不应暴露 Godot Dictionary/Array/Variant。"
            );
        }

        foreach (PropertyInfo property in type.GetProperties(flags))
        {
            _test.True(
                !IsGodotDynamicBoundaryType(property.PropertyType),
                $"{label}.{property.Name} 不应暴露 Godot Dictionary/Array/Variant。"
            );
        }

        foreach (MethodInfo method in type.GetMethods(flags))
        {
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                _test.True(
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

}
