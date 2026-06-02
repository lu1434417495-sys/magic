using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

public partial class run_battle_metrics_collector_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        try
        {
            TestMetricsCollectorIsPlainTypedService();
            TestRuntimeMetricsUseTypedStateAndStableProjection();
        }
        catch (Exception exception)
        {
            _failures.Add($"Unhandled exception: {exception}");
        }

        if (_failures.Count == 0)
        {
            GD.Print("Battle metrics collector regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle metrics collector regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestMetricsCollectorIsPlainTypedService()
    {
        AssertPlainType(typeof(BattleMetricsCollector), "BattleMetricsCollector");
        AssertPlainType(typeof(BattleMetricsState), "BattleMetricsState");
        AssertPlainType(typeof(BattleMetricEntry), "BattleMetricEntry");
        AssertTrue(
            typeof(BattleMetricsCollector).GetMethod("setup") == null
                && typeof(BattleMetricsCollector).GetMethod("dispose") == null
                && typeof(BattleMetricsCollector).GetMethod("_initialize_battle_metrics") == null,
            "BattleMetricsCollector 不应保留 GDScript-style snake_case API。"
        );
        AssertPublicApiDoesNotExposeGodotCollections(
            typeof(BattleMetricsCollector),
            "BattleMetricsCollector"
        );
        AssertPublicApiDoesNotExposeGodotCollections(
            typeof(BattleMetricsState),
            "BattleMetricsState"
        );
        AssertPublicApiDoesNotExposeGodotCollections(
            typeof(BattleMetricEntry),
            "BattleMetricEntry"
        );
    }

    private void TestRuntimeMetricsUseTypedStateAndStableProjection()
    {
        var runtime = new BattleRuntimeModule();
        var source = BuildUnit("metrics_source", "player", "hero_member");
        var target = BuildUnit("metrics_target", "hostile", "");
        runtime._state = new BattleState
        {
            battle_id = "metrics_regression",
            seed = 2701,
        };
        runtime._state.units[source.unit_id] = source;
        runtime._state.units[target.unit_id] = target;

        runtime._initialize_battle_metrics();
        AssertTrue(
            runtime._battle_metrics.Units.ContainsKey("metrics_source"),
            "runtime 内部 metrics 应使用 typed unit map。"
        );

        runtime._record_turn_started(source);
        runtime._record_action_issued(source, BattleCommand.TYPE_SKILL(), 1);
        runtime._record_skill_attempt(source, "bolt");
        runtime._record_skill_success(source, "bolt");
        runtime._record_effect_metrics(source, target, 7, 0, 1);
        runtime.record_skill_effect_result(source, 3, 2, 1);
        runtime._record_unit_defeated(target);

        BattleMetricEntry sourceMetrics = runtime._battle_metrics.Units["metrics_source"];
        AssertEq(sourceMetrics.TurnCount, 1, "typed unit metrics 应记录 turn_count。");
        AssertEq(sourceMetrics.ActionCounts["skill"], 1, "typed unit metrics 应记录 skill action。");
        AssertEq(sourceMetrics.SkillAttemptCounts["bolt"], 1, "typed unit metrics 应记录 skill attempt。");
        AssertEq(sourceMetrics.SkillSuccessCounts["bolt"], 1, "typed unit metrics 应记录 skill success。");
        AssertEq(sourceMetrics.TotalDamageDone, 10, "typed unit metrics 应聚合 total_damage_done。");
        AssertEq(sourceMetrics.TotalHealingDone, 2, "typed unit metrics 应聚合 total_healing_done。");
        AssertEq(sourceMetrics.KillCount, 2, "typed unit metrics 应聚合 kill_count。");

        BattleMetricEntry targetMetrics = runtime._battle_metrics.Units["metrics_target"];
        AssertEq(targetMetrics.TotalDamageTaken, 7, "typed target metrics 应记录 total_damage_taken。");
        AssertEq(targetMetrics.DeathCount, 1, "typed target metrics 应记录 death_count。");

        Godot.Collections.Dictionary payload = runtime.get_battle_metrics();
        AssertEq(payload["battle_id"].AsString(), "metrics_regression", "投影应保留 battle_id。");
        AssertEq(payload["seed"].AsInt32(), 2701, "投影应保留 seed。");
        Godot.Collections.Dictionary units = payload["units"].AsGodotDictionary();
        Godot.Collections.Dictionary sourcePayload = units["metrics_source"].AsGodotDictionary();
        AssertEq(
            sourcePayload["total_damage_done"].AsInt32(),
            10,
            "投影应保留 total_damage_done。"
        );
        AssertEq(
            sourcePayload["total_healing_done"].AsInt32(),
            2,
            "投影应保留 total_healing_done。"
        );
        Godot.Collections.Dictionary factions = payload["factions"].AsGodotDictionary();
        Godot.Collections.Dictionary playerFaction = factions["player"].AsGodotDictionary();
        AssertEq(playerFaction["unit_count"].AsInt32(), 1, "faction 投影应保留 unit_count。");
        AssertEq(
            playerFaction["total_damage_done"].AsInt32(),
            10,
            "faction 投影应聚合 total_damage_done。"
        );

        sourcePayload["total_damage_done"] = 999;
        AssertEq(
            runtime._battle_metrics.Units["metrics_source"].TotalDamageDone,
            10,
            "修改公开 metrics Dictionary 投影不应反向污染 typed metrics state。"
        );
        Godot.Collections.Dictionary freshPayload = runtime.get_battle_metrics();
        Godot.Collections.Dictionary freshSource = freshPayload["units"]
            .AsGodotDictionary()["metrics_source"]
            .AsGodotDictionary();
        AssertEq(
            freshSource["total_damage_done"].AsInt32(),
            10,
            "重新投影应仍来自 typed metrics state。"
        );
    }

    private static BattleUnitState BuildUnit(StringName unitId, StringName factionId, StringName memberId)
    {
        return new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            control_mode = factionId == "player" ? "manual" : "ai",
            source_member_id = memberId,
            current_hp = 20,
            is_alive = true,
        };
    }

    private void AssertPlainType(Type type, string label)
    {
        AssertTrue(type.IsSealed, $"{label} 应为 sealed plain C# type。");
        AssertTrue(
            !typeof(GodotObject).IsAssignableFrom(type),
            $"{label} 不应继承 GodotObject/RefCounted。"
        );
        AssertTrue(
            type.GetCustomAttribute<GlobalClassAttribute>() == null,
            $"{label} 不应注册 GlobalClass。"
        );
        AssertEq(
            type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Length,
            0,
            $"{label} 应暴露 typed property/map，而不是 public mutable fields。"
        );
    }

    private void AssertPublicApiDoesNotExposeGodotCollections(Type type, string label)
    {
        foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            AssertTrue(
                !IsForbiddenGodotBoundaryType(method.ReturnType),
                $"{label}.{method.Name} 不应返回 Godot Dictionary/Array/Variant。"
            );
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                AssertTrue(
                    !IsForbiddenGodotBoundaryType(parameter.ParameterType),
                    $"{label}.{method.Name}({parameter.Name}) 不应接收 Godot Dictionary/Array/Variant。"
                );
            }
        }
    }

    private static bool IsForbiddenGodotBoundaryType(Type type) =>
        type == typeof(Variant)
        || type.FullName == "Godot.Collections.Dictionary"
        || type.FullName == "Godot.Collections.Array";

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
        {
            _failures.Add($"{message} Expected {expected}, got {actual}.");
        }
    }
}
