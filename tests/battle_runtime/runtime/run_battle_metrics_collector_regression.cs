using System;
using System.Collections.Generic;
using Godot;

public partial class run_battle_metrics_collector_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestRuntimeMetricsUseTypedStateAndStableProjection();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        Quit(_test.Finish("Battle metrics collector regression"));
    }

    private void TestRuntimeMetricsUseTypedStateAndStableProjection()
    {
        var source = BuildUnit("metrics_source", "player", "hero_member");
        var target = BuildUnit("metrics_target", "hostile", "");
        source.SetAnchorCoord(Vector2I.Zero);
        target.SetAnchorCoord(new Vector2I(1, 0));
        using BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "metrics_regression",
            new Vector2I(2, 1),
            new[] { source },
            new[] { target }
        );
        BattleRuntimeModule runtime = fixture.Runtime;
        fixture.State.seed = 2701;

        runtime._initialize_battle_metrics();
        _test.True(
            runtime._battle_metrics.Units.ContainsKey("metrics_source"),
            "runtime 内部 metrics 应使用 typed unit map。"
        );

        runtime._record_turn_started(source);
        runtime._record_action_issued(source, BattleTypedNames.ToStringName(BattleCommandKind.Skill), 1);
        runtime._record_skill_attempt(source, "bolt");
        runtime._record_skill_success(source, "bolt");
        runtime._record_effect_metrics(source, target, 7, 0, 1);
        runtime.RecordSkillEffectResult(source, 3, 2, 1);
        runtime._record_unit_defeated(target);

        BattleMetricEntry sourceMetrics = runtime._battle_metrics.Units["metrics_source"];
        _test.Eq(sourceMetrics.TurnCount, 1, "typed unit metrics 应记录 turn_count。");
        _test.Eq(sourceMetrics.ActionCounts["skill"], 1, "typed unit metrics 应记录 skill action。");
        _test.Eq(sourceMetrics.SkillAttemptCounts["bolt"], 1, "typed unit metrics 应记录 skill attempt。");
        _test.Eq(sourceMetrics.SkillSuccessCounts["bolt"], 1, "typed unit metrics 应记录 skill success。");
        _test.Eq(sourceMetrics.TotalDamageDone, 10, "typed unit metrics 应聚合 total_damage_done。");
        _test.Eq(sourceMetrics.TotalHealingDone, 2, "typed unit metrics 应聚合 total_healing_done。");
        _test.Eq(sourceMetrics.KillCount, 2, "typed unit metrics 应聚合 kill_count。");

        BattleMetricEntry targetMetrics = runtime._battle_metrics.Units["metrics_target"];
        _test.Eq(targetMetrics.TotalDamageTaken, 7, "typed target metrics 应记录 total_damage_taken。");
        _test.Eq(targetMetrics.DeathCount, 1, "typed target metrics 应记录 death_count。");

        Godot.Collections.Dictionary payload =
            BattleMetricsProjection.Project(runtime.GetBattleMetricsTyped());
        _test.Eq(payload["battle_id"].AsString(), "metrics_regression", "投影应保留 battle_id。");
        _test.Eq(payload["seed"].AsInt32(), 2701, "投影应保留 seed。");
        Godot.Collections.Dictionary units = payload["units"].AsGodotDictionary();
        Godot.Collections.Dictionary sourcePayload = units["metrics_source"].AsGodotDictionary();
        _test.Eq(
            sourcePayload["total_damage_done"].AsInt32(),
            10,
            "投影应保留 total_damage_done。"
        );
        _test.Eq(
            sourcePayload["total_healing_done"].AsInt32(),
            2,
            "投影应保留 total_healing_done。"
        );
        Godot.Collections.Dictionary factions = payload["factions"].AsGodotDictionary();
        Godot.Collections.Dictionary playerFaction = factions["player"].AsGodotDictionary();
        _test.Eq(playerFaction["unit_count"].AsInt32(), 1, "faction 投影应保留 unit_count。");
        _test.Eq(
            playerFaction["total_damage_done"].AsInt32(),
            10,
            "faction 投影应聚合 total_damage_done。"
        );

        sourcePayload["total_damage_done"] = 999;
        _test.Eq(
            runtime._battle_metrics.Units["metrics_source"].TotalDamageDone,
            10,
            "修改公开 metrics Dictionary 投影不应反向污染 typed metrics state。"
        );
        Godot.Collections.Dictionary freshPayload =
            BattleMetricsProjection.Project(runtime.GetBattleMetricsTyped());
        Godot.Collections.Dictionary freshSource = freshPayload["units"]
            .AsGodotDictionary()["metrics_source"]
            .AsGodotDictionary();
        _test.Eq(
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

}
