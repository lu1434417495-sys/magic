using System;
using System.Collections.Generic;
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
            GD.PushError($"Battle report formatter contract regression crashed: {exception}");
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
        AssertAllLinesNonEmpty(batch.log_lines, "typed damage result 应生成非空战斗日志。");
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
        _test.False(string.IsNullOrWhiteSpace(lines[0]), "meteor summary projection 应生成非空摘要。");
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

    private void AssertAllLinesNonEmpty(IReadOnlyList<string> lines, string message)
    {
        if (lines == null || lines.Count == 0)
        {
            _test.Fail(message);
            return;
        }
        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                _test.Fail(message);
                return;
            }
        }
    }

    private static bool IsGodotPayloadType(Type type)
    {
        if (type.IsByRef || type.IsPointer || type.IsArray)
        {
            type = type.GetElementType() ?? type;
        }
        if (type == typeof(Variant))
        {
            return true;
        }
        string typeName = type.FullName ?? "";
        if (
            typeName.StartsWith("Godot.Collections.Dictionary", StringComparison.Ordinal)
            || typeName.StartsWith("Godot.Collections.Array", StringComparison.Ordinal)
        )
        {
            return true;
        }
        if (type.IsGenericType)
        {
            foreach (Type genericArgument in type.GetGenericArguments())
            {
                if (IsGodotPayloadType(genericArgument))
                {
                    return true;
                }
            }
        }
        return false;
    }

}
