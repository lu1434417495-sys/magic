using System;
using Godot;

public partial class run_battle_report_formatter_contract_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestTypedAttackMetadataBuildsReportEntry();
            TestTypedDamageResultBuildsLogLines();
            TestMeteorSummaryProjectionStillFormatsEntry();

            RequestTestExit(_test.Finish("Battle report formatter contract regression"));
        }
        catch (Exception exception)
        {
            ConsoleProcessOutput.WriteFailure($"Battle report formatter contract regression crashed: {exception}");
            RequestTestExit(_test.Finish("Battle report formatter contract regression", 1));
        }
    }

    private void TestTypedAttackMetadataBuildsReportEntry()
    {
        var formatter = new BattleReportFormatter();
        var attacker = BuildUnit("attacker", "player", "施术者");
        var defender = BuildUnit("defender", "enemy", "目标");
        var metadata = new AttackResolutionMetadata
        {
            AttackResolution = "critical_hit",
            HitRoll = 20,
            CritGateDie = 20,
            CritGateRoll = 20,
            RequiredRoll = 12,
            DisplayRequiredRoll = 12,
            CritThreshold = 19,
        };

        Godot.Collections.Dictionary entry = formatter.BuildAttackReportEntry(
            attacker,
            defender,
            metadata,
            "gate_die",
            new Godot.Collections.Array<StringName> { "doom_sentence" }
        );

        _test.Eq(
            EntryString(entry, "entry_type"),
            "fate_attack_resolution",
            "typed attack metadata 应构建 fate attack report entry。"
        );
        _test.Eq(
            EntryString(entry, "reason_id"),
            "critical_success_gate_die",
            "gate die critical hit 应生成对应 reason_id。"
        );
        _test.False(
            string.IsNullOrWhiteSpace(EntryString(entry, "text")),
            "typed attack metadata 应生成非空 report text。"
        );
    }

    private void TestTypedDamageResultBuildsLogLines()
    {
        var formatter = new BattleReportFormatter();
        var batch = new BattleEventBatch();
        var result = new AttackEffectResolutionResult
        {
            Damage = 18,
            ShieldAbsorbed = 3,
            ShieldBroken = true,
            HasDamageEvent = true,
            AnyHalf = true,
            HalfSourceLabels = new[] { "冰霜抗性" },
        };

        formatter.AppendDamageResultLogLines(batch, "施术者", "目标", result);

        _test.Eq(batch.log_lines.Count, 3, "typed damage result 应生成伤害、护盾吸收和护盾破碎日志。");
        _test.Eq(
            batch.log_lines[0],
            "施术者 对 目标 造成 18 点伤害（因 冰霜抗性 减半后结算）。",
            "typed damage log 应准确投影伤害值与减半来源。"
        );
        _test.Eq(
            batch.log_lines[1],
            "目标 的护盾吸收了 3 点伤害。",
            "typed damage log 应准确投影护盾吸收值。"
        );
        _test.Eq(
            batch.log_lines[2],
            "目标 的护盾被击碎。",
            "typed damage log 应准确投影破盾结果。"
        );
    }

    private void TestMeteorSummaryProjectionStillFormatsEntry()
    {
        var formatter = new BattleReportFormatter();
        var entry = new Godot.Collections.Dictionary
        {
            ["entry_type"] = "meteor_swarm_impact_summary",
            ["target_count"] = 2,
            ["total_damage"] = 42,
            ["terrain_summary"] = new Godot.Collections.Dictionary
            {
                ["affected_coord_count"] = 9,
                ["crater_count"] = 3,
                ["rubble_count"] = 2,
                ["dust_count"] = 1,
            },
        };

        Godot.Collections.Array<string> lines = formatter.FormatMeteorSwarmSummary(entry);

        _test.Eq(lines.Count, 1, "meteor summary projection 应生成一行摘要。");
        _test.Eq(
            lines[0],
            "陨星雨覆盖 9 格，波及 2 个单位，造成 42 点总伤害；留下陨坑 3 格、碎石 2 格、尘土 1 格。",
            "meteor summary 应准确投影目标数、总伤害与每类 terrain 统计。"
        );
    }

    private static BattleUnitState BuildUnit(StringName unitId, StringName team, string displayName)
    {
        return new BattleUnitState
        {
            unit_id = unitId,
            source_member_id = unitId,
            faction_id = team,
            display_name = displayName,
        };
    }

    private static string EntryString(Godot.Collections.Dictionary entry, string key)
    {
        return entry.GetValueOrDefault(key, "").AsString();
    }

}
