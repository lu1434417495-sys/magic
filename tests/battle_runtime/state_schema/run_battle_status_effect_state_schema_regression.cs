using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_status_effect_state_schema_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestValidRoundtripWithoutDuration();
        TestValidRoundtripWithDurationTickAndSkip();
        TestMissingRequiredFieldReturnsNull();
        TestExtraLegacyFieldReturnsNull();
        TestWrongTypesReturnNull();
        TestStringNumbersAndBoolsReturnNull();
        TestEmptyStatusIdReturnsNull();
        TestNegativeDurationReturnsNull();
        TestZeroTickOptionalReturnsNull();
        TestSkipFalseOptionalReturnsNull();
        TestDuplicateStateStillWorks();

        if (_failures.Count == 0)
        {
            GD.Print("Battle status effect state schema regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle status effect state schema regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestValidRoundtripWithoutDuration()
    {
        BattleStatusEffectState effect = new()
        {
            status_id = "guarded",
            source_unit_id = "",
            power = 2,
            @params = new GDictionary { ["damage_tag"] = "holy" },
            stacks = 0,
        };

        GDictionary payload = effect.to_dict();
        AssertFalse(payload.ContainsKey("duration"), "无 duration 状态的 to_dict 不应写 duration。");
        BattleStatusEffectState restored = BattleStatusEffectState.from_dict(payload);
        AssertTrue(restored != null, "无 duration 的当前 to_dict 形状应能恢复。");
        if (restored == null)
        {
            return;
        }

        AssertEq(restored.status_id, new StringName("guarded"), "roundtrip 应保留 status_id。");
        AssertEq(restored.source_unit_id, new StringName(""), "roundtrip 应允许空 source_unit_id。");
        AssertEq(restored.power, 2, "roundtrip 应保留 power。");
        AssertDictionaryEq(
            restored.@params,
            new GDictionary { ["damage_tag"] = "holy" },
            "roundtrip 应深拷贝并保留 params。"
        );
        AssertEq(restored.stacks, 0, "roundtrip 应允许新建状态写出的 0 stacks。");
        AssertEq(restored.duration, -1, "缺失 duration 应恢复为 -1。");
        AssertEq(restored.tick_interval_tu, 0, "缺失 tick_interval_tu 应恢复为 0。");
        AssertEq(restored.next_tick_at_tu, 0, "缺失 next_tick_at_tu 应恢复为 0。");
        AssertFalse(restored.skip_next_turn_end_decay, "缺失 skip_next_turn_end_decay 应恢复为 false。");
    }

    private void TestValidRoundtripWithDurationTickAndSkip()
    {
        BattleStatusEffectState effect = new()
        {
            status_id = "burning",
            source_unit_id = "caster",
            power = 3,
            @params = new GDictionary
            {
                ["damage_tag"] = "fire",
                ["nested"] = new GDictionary { ["value"] = 1 },
            },
            stacks = 2,
            duration = 20,
            tick_interval_tu = 10,
            next_tick_at_tu = 15,
            skip_next_turn_end_decay = true,
            lock_crit = true,
        };

        BattleStatusEffectState restored = BattleStatusEffectState.from_dict(effect.to_dict());
        AssertTrue(restored != null, "带 duration/tick/skip 的当前 to_dict 形状应能恢复。");
        if (restored == null)
        {
            return;
        }

        AssertEq(restored.status_id, new StringName("burning"), "roundtrip 应保留 status_id。");
        AssertEq(restored.source_unit_id, new StringName("caster"), "roundtrip 应保留 source_unit_id。");
        AssertEq(restored.power, 3, "roundtrip 应保留 power。");
        AssertDictionaryEq(
            restored.@params,
            new GDictionary
            {
                ["damage_tag"] = "fire",
                ["nested"] = new GDictionary { ["value"] = 1 },
            },
            "roundtrip 应保留 params。"
        );
        AssertEq(restored.stacks, 2, "roundtrip 应保留 stacks。");
        AssertEq(restored.duration, 20, "roundtrip 应保留 duration。");
        AssertEq(restored.tick_interval_tu, 10, "roundtrip 应保留 tick_interval_tu。");
        AssertEq(restored.next_tick_at_tu, 15, "roundtrip 应保留 next_tick_at_tu。");
        AssertTrue(restored.skip_next_turn_end_decay, "roundtrip 应保留 skip_next_turn_end_decay。");
        AssertTrue(restored.lock_crit, "roundtrip 应保留 lock_crit typed 字段。");
    }

    private void TestMissingRequiredFieldReturnsNull()
    {
        foreach (string field in new[] { "status_id", "source_unit_id", "power", "params", "stacks" })
        {
            GDictionary payload = ValidPayload();
            payload.Remove(field);
            AssertNull(BattleStatusEffectState.from_dict(payload), $"缺少必需字段 {field} 应返回 null。");
        }
    }

    private void TestExtraLegacyFieldReturnsNull()
    {
        GDictionary payload = ValidPayload();
        payload["remaining_turns"] = 2;
        AssertNull(BattleStatusEffectState.from_dict(payload), "额外旧字段应返回 null。");
    }

    private void TestWrongTypesReturnNull()
    {
        AssertNull(BattleStatusEffectState.from_dict(PayloadWith("status_id", 7)), "status_id 必须是 String/StringName。");
        AssertNull(
            BattleStatusEffectState.from_dict(PayloadWith("source_unit_id", 7)),
            "source_unit_id 必须是 String/StringName。"
        );
        AssertNull(BattleStatusEffectState.from_dict(PayloadWith("power", 1.5)), "power 必须是 int。");
        AssertNull(
            BattleStatusEffectState.from_dict(PayloadWith("params", new GArray())),
            "params 必须是 Dictionary。"
        );
        AssertNull(BattleStatusEffectState.from_dict(PayloadWith("stacks", 1.0)), "stacks 必须是 int。");
        AssertNull(BattleStatusEffectState.from_dict(PayloadWith("duration", 1.0)), "duration 必须是 int。");
        AssertNull(
            BattleStatusEffectState.from_dict(PayloadWith("tick_interval_tu", 1.0)),
            "tick_interval_tu 必须是 int。"
        );
        AssertNull(
            BattleStatusEffectState.from_dict(PayloadWith("next_tick_at_tu", 1.0)),
            "next_tick_at_tu 必须是 int。"
        );
        AssertNull(
            BattleStatusEffectState.from_dict(PayloadWith("skip_next_turn_end_decay", 1)),
            "skip_next_turn_end_decay 必须是 bool true。"
        );
        AssertNull(
            BattleStatusEffectState.from_dict(PayloadWith("lock_crit", 1)),
            "lock_crit 必须是 bool true。"
        );

        GDictionary stringNamePayload = ValidPayload();
        stringNamePayload["status_id"] = new StringName("slow");
        stringNamePayload["source_unit_id"] = new StringName("caster");
        AssertTrue(
            BattleStatusEffectState.from_dict(stringNamePayload) != null,
            "StringName status_id/source_unit_id 应继续可用。"
        );
    }

    private void TestStringNumbersAndBoolsReturnNull()
    {
        AssertNull(BattleStatusEffectState.from_dict(PayloadWith("power", "3")), "power 不接受字符串数字。");
        AssertNull(BattleStatusEffectState.from_dict(PayloadWith("stacks", "1")), "stacks 不接受字符串数字。");
        AssertNull(BattleStatusEffectState.from_dict(PayloadWith("duration", "10")), "duration 不接受字符串数字。");
        AssertNull(
            BattleStatusEffectState.from_dict(PayloadWith("tick_interval_tu", "10")),
            "tick_interval_tu 不接受字符串数字。"
        );
        AssertNull(
            BattleStatusEffectState.from_dict(PayloadWith("next_tick_at_tu", "15")),
            "next_tick_at_tu 不接受字符串数字。"
        );
        AssertNull(
            BattleStatusEffectState.from_dict(PayloadWith("skip_next_turn_end_decay", "true")),
            "skip_next_turn_end_decay 不接受字符串 bool。"
        );
        AssertNull(
            BattleStatusEffectState.from_dict(PayloadWith("lock_crit", "true")),
            "lock_crit 不接受字符串 bool。"
        );
    }

    private void TestEmptyStatusIdReturnsNull()
    {
        AssertNull(BattleStatusEffectState.from_dict(PayloadWith("status_id", "")), "空 String status_id 应返回 null。");
        AssertNull(
            BattleStatusEffectState.from_dict(PayloadWith("status_id", new StringName(""))),
            "空 StringName status_id 应返回 null。"
        );
    }

    private void TestNegativeDurationReturnsNull()
    {
        AssertNull(BattleStatusEffectState.from_dict(PayloadWith("duration", -1)), "显式负 duration 应返回 null。");
    }

    private void TestZeroTickOptionalReturnsNull()
    {
        AssertNull(
            BattleStatusEffectState.from_dict(PayloadWith("tick_interval_tu", 0)),
            "显式 0 tick_interval_tu 应返回 null。"
        );
        AssertNull(
            BattleStatusEffectState.from_dict(PayloadWith("next_tick_at_tu", 0)),
            "显式 0 next_tick_at_tu 应返回 null。"
        );
    }

    private void TestSkipFalseOptionalReturnsNull()
    {
        AssertNull(
            BattleStatusEffectState.from_dict(PayloadWith("skip_next_turn_end_decay", false)),
            "显式 false skip_next_turn_end_decay 应返回 null。"
        );
        AssertNull(
            BattleStatusEffectState.from_dict(PayloadWith("lock_crit", false)),
            "显式 false lock_crit 应返回 null。"
        );
    }

    private void TestDuplicateStateStillWorks()
    {
        BattleStatusEffectState effect = new()
        {
            status_id = "slow",
            source_unit_id = "caster",
            power = 1,
            @params = new GDictionary { ["move_cost_delta"] = 1 },
            stacks = 1,
            duration = 15,
            lock_crit = true,
        };

        BattleStatusEffectState duplicate = effect.duplicate_state();
        AssertTrue(duplicate != null, "duplicate_state 应继续返回有效对象。");
        if (duplicate == null)
        {
            return;
        }

        AssertTrue(duplicate != effect, "duplicate_state 应返回新对象。");
        AssertEq(duplicate.status_id, new StringName("slow"), "duplicate_state 应保留 status_id。");
        AssertEq(duplicate.source_unit_id, new StringName("caster"), "duplicate_state 应保留 source_unit_id。");
        AssertEq(duplicate.power, 1, "duplicate_state 应保留 power。");
        AssertDictionaryEq(
            duplicate.@params,
            new GDictionary { ["move_cost_delta"] = 1 },
            "duplicate_state 应保留 params。"
        );
        AssertEq(duplicate.stacks, 1, "duplicate_state 应保留 stacks。");
        AssertEq(duplicate.duration, 15, "duplicate_state 应保留 duration。");
        AssertTrue(duplicate.lock_crit, "duplicate_state 应保留 lock_crit。");
    }

    private static GDictionary ValidPayload()
    {
        return new GDictionary
        {
            ["status_id"] = "burning",
            ["source_unit_id"] = "caster",
            ["power"] = 3,
            ["params"] = new GDictionary { ["damage_tag"] = "fire" },
            ["stacks"] = 1,
        };
    }

    private static GDictionary PayloadWith(string fieldName, Variant value)
    {
        GDictionary payload = ValidPayload();
        payload[fieldName] = value;
        return payload;
    }

    private void AssertNull(object value, string message)
    {
        if (value != null)
        {
            _failures.Add($"{message} | actual={value}");
        }
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertFalse(bool condition, string message)
    {
        if (condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
        {
            _failures.Add($"{message} | actual={actual} expected={expected}");
        }
    }

    private void AssertDictionaryEq(GDictionary actual, GDictionary expected, string message)
    {
        if (!DictionaryEquals(actual, expected))
        {
            _failures.Add($"{message} | actual={Variant.From(actual)} expected={Variant.From(expected)}");
        }
    }

    private static bool DictionaryEquals(GDictionary actual, GDictionary expected)
    {
        if (actual == null || expected == null || actual.Count != expected.Count)
        {
            return false;
        }
        foreach (Variant key in expected.Keys)
        {
            string keyText = key.AsString();
            if (
                !TryGetDictionaryValue(expected, keyText, out Variant expectedValue)
                || !TryGetDictionaryValue(actual, keyText, out Variant actualValue)
            )
            {
                return false;
            }
            if (!VariantEquals(actualValue, expectedValue))
            {
                return false;
            }
        }
        return true;
    }

    private static bool TryGetDictionaryValue(GDictionary dictionary, string key, out Variant value)
    {
        foreach (Variant candidateKey in dictionary.Keys)
        {
            if (candidateKey.AsString() == key)
            {
                value = dictionary[candidateKey];
                return true;
            }
        }
        value = default;
        return false;
    }

    private static bool VariantEquals(Variant actual, Variant expected)
    {
        if (actual.VariantType == Variant.Type.Dictionary && expected.VariantType == Variant.Type.Dictionary)
        {
            return DictionaryEquals(actual.AsGodotDictionary(), expected.AsGodotDictionary());
        }
        if (IsStringLike(actual) && IsStringLike(expected))
        {
            return actual.AsString() == expected.AsString();
        }
        if (actual.VariantType == Variant.Type.Int && expected.VariantType == Variant.Type.Int)
        {
            return actual.AsInt64() == expected.AsInt64();
        }
        if (actual.VariantType == Variant.Type.Bool && expected.VariantType == Variant.Type.Bool)
        {
            return actual.AsBool() == expected.AsBool();
        }
        if (actual.VariantType == Variant.Type.Float && expected.VariantType == Variant.Type.Float)
        {
            return Mathf.IsEqualApprox(actual.AsDouble(), expected.AsDouble());
        }
        return actual.VariantType == expected.VariantType && actual.AsString() == expected.AsString();
    }

    private static bool IsStringLike(Variant value)
    {
        return value.VariantType is Variant.Type.String or Variant.Type.StringName;
    }
}
