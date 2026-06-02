using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

public partial class run_battle_report_formatter_contract_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        try
        {
            int exitCode = Run();
            Quit(exitCode);
        }
        catch (Exception exception)
        {
            GD.PushError($"Battle report formatter contract regression crashed: {exception}");
            Quit(1);
        }
    }

    private int Run()
    {
        TestFormatterTypeIsPlainCSharp();
        TestTypedAttackMetadataBuildsReportEntry();
        TestTypedDamageResultBuildsLogLines();
        TestMeteorSummaryProjectionStillFormatsEntry();

        if (_failures.Count == 0)
        {
            GD.Print("Battle report formatter contract regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle report formatter contract regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestFormatterTypeIsPlainCSharp()
    {
        Type formatterType = typeof(BattleReportFormatter);
        AssertTrue(formatterType.IsSealed, "BattleReportFormatter 应为 sealed plain C# formatter。");
        AssertFalse(
            typeof(GodotObject).IsAssignableFrom(formatterType),
            "BattleReportFormatter 不应继承 GodotObject/RefCounted。"
        );
        AssertFalse(
            HasAttributeNamed(formatterType, "GlobalClassAttribute"),
            "BattleReportFormatter 不应注册 GlobalClass。"
        );
        AssertNull(
            formatterType.GetMethod("build_attack_report_entry"),
            "BattleReportFormatter 不应保留 build_attack_report_entry snake_case API。"
        );
        AssertNull(
            formatterType.GetMethod("build_skill_event_entry"),
            "BattleReportFormatter 不应保留 build_skill_event_entry snake_case API。"
        );
        AssertNull(
            formatterType.GetMethod("format_meteor_swarm_summary"),
            "BattleReportFormatter 不应保留 format_meteor_swarm_summary snake_case API。"
        );
        AssertNull(
            formatterType.GetMethod("summarize_damage_result"),
            "BattleReportFormatter 不应保留 summarize_damage_result snake_case API。"
        );
        AssertNull(
            formatterType.GetMethod("build_damage_absorb_reason_text"),
            "BattleReportFormatter 不应保留 build_damage_absorb_reason_text snake_case API。"
        );
        AssertNull(
            formatterType.GetMethod("append_damage_result_log_lines"),
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

        AssertEq(
            EntryString(entry, "entry_type"),
            "fate_attack_resolution",
            "typed attack metadata 应构建 fate attack report entry。"
        );
        AssertEq(
            EntryString(entry, "reason_id"),
            "critical_success_gate_die",
            "gate die critical hit 应生成对应 reason_id。"
        );
        AssertTrue(
            EntryString(entry, "text").Contains("门骰", StringComparison.Ordinal),
            "report text 应保留大成功门骰说明。"
        );
        AssertTrue(
            EntryString(entry, "text").Contains("doom_sentence", StringComparison.Ordinal),
            "report text 应保留事件标签后缀。"
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

        AssertEq(batch.log_lines.Count, 3, "typed damage result 应生成伤害、护盾吸收和护盾破碎日志。");
        AssertTrue(
            batch.log_lines[0].Contains("减半后结算", StringComparison.Ordinal),
            "伤害日志应包含 typed mitigation suffix。"
        );
        AssertTrue(
            batch.log_lines[1].Contains("护盾吸收了 3 点伤害", StringComparison.Ordinal),
            "伤害日志应包含护盾吸收文本。"
        );
        AssertTrue(
            batch.log_lines[2].Contains("护盾被击碎", StringComparison.Ordinal),
            "伤害日志应包含护盾破碎文本。"
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

        AssertEq(lines.Count, 1, "meteor summary projection 应生成一行摘要。");
        AssertTrue(lines[0].Contains("覆盖 9 格", StringComparison.Ordinal), "摘要应包含覆盖格数。");
        AssertTrue(lines[0].Contains("波及 2 个单位", StringComparison.Ordinal), "摘要应包含目标数。");
        AssertTrue(lines[0].Contains("造成 42 点总伤害", StringComparison.Ordinal), "摘要应包含总伤害。");
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

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
            _failures.Add(message);
    }

    private void AssertFalse(bool condition, string message)
    {
        if (condition)
            _failures.Add(message);
    }

    private void AssertNull(object value, string message)
    {
        if (value != null)
            _failures.Add(message);
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
            _failures.Add($"{message} expected={expected} actual={actual}");
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

    private void AssertPublicApiDoesNotExposeGodotPayload(Type type, string label)
    {
        foreach (MethodInfo method in type.GetMethods(
                     BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly
                 ))
        {
            AssertFalse(
                IsGodotPayloadType(method.ReturnType),
                $"{label}.{method.Name}() 不应公开返回 Godot Dictionary/Array/Variant。"
            );
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                AssertFalse(
                    IsGodotPayloadType(parameter.ParameterType),
                    $"{label}.{method.Name}({parameter.Name}) 不应公开接收 Godot Dictionary/Array/Variant。"
                );
            }
        }

        foreach (PropertyInfo property in type.GetProperties(
                     BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly
                 ))
        {
            AssertFalse(
                IsGodotPayloadType(property.PropertyType),
                $"{label}.{property.Name} 不应公开 Godot Dictionary/Array/Variant 属性。"
            );
        }

        foreach (FieldInfo field in type.GetFields(
                     BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly
                 ))
        {
            AssertFalse(
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
}
