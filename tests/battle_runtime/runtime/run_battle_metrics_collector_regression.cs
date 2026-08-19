using System;
using System.Collections.Generic;
using Godot;

public partial class run_battle_metrics_collector_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestCollectorUsesTypedStateAndStableProjection();
            TestIssueCommandPopulatesActionDamageAndKillMetrics();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        RequestTestExit(_test.Finish("Battle metrics collector regression"));
    }

    private void TestCollectorUsesTypedStateAndStableProjection()
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

        var collector = new BattleMetricsCollector();
        collector.Setup(runtime);
        collector.InitializeBattleMetrics();
        _test.True(
            runtime._battle_metrics.Units.ContainsKey("metrics_source"),
            "runtime 内部 metrics 应使用 typed unit map。"
        );

        collector.RecordTurnStarted(source);
        collector.RecordActionIssued(
            source,
            BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            1
        );
        collector.RecordSkillAttempt(source, "bolt");
        collector.RecordSkillSuccess(source, "bolt");
        collector.RecordEffectMetrics(source, target, 7, 0, 1);
        collector.RecordSkillEffectResult(source, 3, 2, 1);
        collector.RecordUnitDefeated(target);

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
        collector.Dispose();
    }

    private void TestIssueCommandPopulatesActionDamageAndKillMetrics()
    {
        const string resourcePath = "mage_death_reap";
        SkillDefinition skillDefinition =
            TestSkillDefinitionProjection.LoadSkillDefinition(resourcePath, resourcePath);
        BattleUnitState caster = BattleTestFixture.BuildUnit(
            "metrics_dispatch_caster",
            "player",
            Vector2I.Zero,
            currentAp: 2,
            currentHp: 80
        );
        caster.AddKnownActiveSkill(skillDefinition.SkillId);
        caster.SetKnownSkillLevelTyped(skillDefinition.SkillId, 1);
        caster.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp));
        caster.SetCurrentMp(150);
        caster.attribute_snapshot.SetValue(
            AttributeService.ToStringName(AttributeIdKind.MpMax),
            200
        );
        caster.attribute_snapshot.SetValue(
            AttributeService.ToStringName(AttributeIdKind.ActionPoints),
            2
        );
        caster.attribute_snapshot.SetValue(
            AttributeService.ToStringName(AttributeIdKind.AttackBonus),
            12
        );
        caster.attribute_snapshot.SetValue(
            AttributeService.ToStringName(AttributeIdKind.ArmorClass),
            10
        );
        caster.attribute_snapshot.SetValue("willpower", 10);

        BattleUnitState target = BattleTestFixture.BuildUnit(
            "metrics_dispatch_target",
            "enemy",
            new Vector2I(1, 0),
            currentAp: 1,
            currentHp: 5
        );
        target.attribute_snapshot.SetValue(
            AttributeService.ToStringName(AttributeIdKind.ArmorClass),
            10
        );

        BattleCommand command = null;
        BattleEventBatch batch = null;
        using BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "metrics_dispatch_regression",
            new Vector2I(4, 2),
            new[] { caster },
            new[] { target }
        );
        try
        {
            fixture.Runtime.setup(
                skill_definitions: new Dictionary<StringName, SkillDefinition>
                {
                    [skillDefinition.SkillId] = skillDefinition,
                }
            );
            fixture.Runtime.SetupStateForTests(fixture.State);
            BattleTestFixture.ConfigureDamageResolverForTests(
                fixture.Runtime,
                new FixedHitMaxDamageResolver()
            );
            BattleTestFixture.ConfigureHitResolverForTests(
                fixture.Runtime,
                new FixedHitResolver(10)
            );
            var initializer = new BattleMetricsCollector();
            initializer.Setup(fixture.Runtime);
            initializer.InitializeBattleMetrics();
            initializer.Dispose();

            command = new BattleCommand
            {
                CommandKind = BattleCommandKind.Skill,
                unit_id = caster.unit_id,
                skill_entry_id = BattleSkillEntryIds.KnownSkill(skillDefinition.SkillId),
                skill_id = skillDefinition.SkillId,
                target_unit_id = target.unit_id,
                target_coord = target.GetAnchorCoord(),
            };
            command.AddTargetUnitId(target.unit_id);

            BattlePreview preview = fixture.Runtime.PreviewCommand(command);
            _test.True(
                preview?.allowed == true,
                $"正式指标链路的技能 preview 应允许执行。logs={JoinLogs(preview?.LogLinesTyped)}"
            );
            batch = fixture.Runtime.IssueCommand(command);
            string issueLogs = JoinLogs(batch?.LogLinesTyped);
            _test.False(
                target.IsAlive(),
                $"正式指标链路应实际击杀目标。target_hp={target.GetCurrentHp()} logs={issueLogs}"
            );

            BattleMetricEntry casterMetrics =
                fixture.Runtime.GetBattleMetricsTyped().Units[caster.unit_id.ToString()];
            BattleMetricEntry targetMetrics =
                fixture.Runtime.GetBattleMetricsTyped().Units[target.unit_id.ToString()];
            _test.Eq(
                casterMetrics.ActionCounts["skill"],
                1,
                "IssueCommand 应通过正式分发记录一次 skill action。"
            );
            _test.Eq(
                casterMetrics.SkillAttemptCounts[skillDefinition.SkillId.ToString()],
                1,
                "IssueCommand 应通过正式分发记录一次 skill attempt。"
            );
            _test.Eq(
                casterMetrics.SkillSuccessCounts[skillDefinition.SkillId.ToString()],
                1,
                "成功执行的 IssueCommand 应记录一次 skill success。"
            );
            _test.True(
                casterMetrics.TotalDamageDone > 0,
                "正式伤害结算应向施法者指标写入正伤害。"
            );
            _test.Eq(
                casterMetrics.TotalDamageDone,
                targetMetrics.TotalDamageTaken,
                "同一次正式伤害结算的造成伤害与承受伤害指标应一致。"
            );
            _test.Eq(casterMetrics.KillCount, 1, "正式击杀结算应记录一次 kill。");
            _test.Eq(targetMetrics.DeathCount, 1, "正式击杀结算应记录目标一次 death。");
        }
        finally
        {
            batch?.Dispose();
            BattleTestFixture.DisposeBattleCommand(command);
        }
    }

    private static string JoinLogs(IEnumerable<string> values) =>
        values == null ? "" : string.Join(" | ", values);

    private static BattleUnitState BuildUnit(StringName unitId, StringName factionId, StringName memberId)
    {
        return new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            control_mode = factionId == "player" ? "manual" : "ai",
            source_member_id = memberId,
        }.WithCombatResourcesForTest(
            hp: 20,
            isAlive: true
        );
    }

}
