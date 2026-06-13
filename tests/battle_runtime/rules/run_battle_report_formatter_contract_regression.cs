using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

public partial class run_battle_report_formatter_contract_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestFormatterTypeIsPlainCSharp();
            TestTypedAttackMetadataBuildsReportEntry();
            TestTypedDamageResultBuildsLogLines();
            TestMeteorSummaryProjectionStillFormatsEntry();

            Quit(_test.Finish("Battle report formatter contract regression"));
        }
        catch (Exception exception)
        {
            GD.PushError($"Battle report formatter contract regression crashed: {exception}");
            Quit(1);
        }
    }

    private void TestFormatterTypeIsPlainCSharp()
    {
        Type formatterType = typeof(BattleReportFormatter);
        _test.True(formatterType.IsSealed, "BattleReportFormatter 应为 sealed plain C# formatter。");
        _test.True(
            formatterType.GetMethod("build_attack_report_entry") == null,
            "BattleReportFormatter 不应保留 build_attack_report_entry snake_case API。"
        );
        _test.True(
            formatterType.GetMethod("build_skill_event_entry") == null,
            "BattleReportFormatter 不应保留 build_skill_event_entry snake_case API。"
        );
        _test.True(
            formatterType.GetMethod("format_meteor_swarm_summary") == null,
            "BattleReportFormatter 不应保留 format_meteor_swarm_summary snake_case API。"
        );
        _test.True(
            formatterType.GetMethod("summarize_damage_result") == null,
            "BattleReportFormatter 不应保留 summarize_damage_result snake_case API。"
        );
        _test.True(
            formatterType.GetMethod("build_damage_absorb_reason_text") == null,
            "BattleReportFormatter 不应保留 build_damage_absorb_reason_text snake_case API。"
        );
        _test.True(
            formatterType.GetMethod("append_damage_result_log_lines") == null,
            "BattleReportFormatter 不应保留 append_damage_result_log_lines snake_case API。"
        );
        AssertPublicApiDoesNotExposeGodotPayload(formatterType, "BattleReportFormatter");
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

    private void AssertAllLinesNonEmpty(Godot.Collections.Array<string> lines, string message)
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

    private void AssertPublicApiDoesNotExposeGodotPayload(Type type, string label)
    {
        foreach (MethodInfo method in type.GetMethods(
                     BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly
                 ))
        {
            _test.False(
                IsGodotPayloadType(method.ReturnType),
                $"{label}.{method.Name}() 不应公开返回 Godot Dictionary/Array/Variant。"
            );
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                _test.False(
                    IsGodotPayloadType(parameter.ParameterType),
                    $"{label}.{method.Name}({parameter.Name}) 不应公开接收 Godot Dictionary/Array/Variant。"
                );
            }
        }

        foreach (PropertyInfo property in type.GetProperties(
                     BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly
                 ))
        {
            _test.False(
                IsGodotPayloadType(property.PropertyType),
                $"{label}.{property.Name} 不应公开 Godot Dictionary/Array/Variant 属性。"
            );
        }

        foreach (FieldInfo field in type.GetFields(
                     BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly
                 ))
        {
            _test.False(
                IsGodotPayloadType(field.FieldType),
                $"{label}.{field.Name} 不应公开 Godot Dictionary/Array/Variant 字段。"
            );
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

    private static bool HasAttributeNamed(Type type, string attributeName)
    {
        foreach (object attribute in type.GetCustomAttributes(false))
        {
            if (attribute.GetType().Name == attributeName)
                return true;
        }
        return false;
    }
}
