using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_battle_timeline_state_schema_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestValidToDictRoundtrip();
        TestMissingFieldReturnsNull();
        TestExtraFieldReturnsNull();
        TestWrongTypesReturnNull();
        TestStringNumbersReturnNull();
        TestEmptyReadyIdReturnsNull();
        TestNonArrayReadyUnitIdsReturnsNull();
        TestNumericBoundariesReturnNull();

        if (_failures.Count == 0)
        {
            GD.Print("Battle timeline state schema regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle timeline state schema regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestValidToDictRoundtrip()
    {
        BattleTimelineState state = new()
        {
            current_tu = 15,
            tu_per_tick = 5,
            frozen = true,
            ready_unit_ids = new GStringNameArray { new StringName("hero"), new StringName("enemy") },
        };

        BattleTimelineState restored = BattleTimelineState.from_dict(state.to_dict());
        AssertTrue(restored != null, "合法 to_dict payload 应能恢复。");
        if (restored == null)
        {
            return;
        }

        AssertEq(restored.current_tu, 15, "roundtrip 应保留 current_tu。");
        AssertEq(restored.tu_per_tick, 5, "roundtrip 应保留 tu_per_tick。");
        AssertTrue(restored.frozen, "roundtrip 应保留 frozen。");
        AssertStringNameArrayEq(
            restored.ready_unit_ids,
            new[] { "hero", "enemy" },
            "roundtrip 应保留 ready_unit_ids。"
        );
    }

    private void TestMissingFieldReturnsNull()
    {
        GDictionary payload = ValidPayload();
        payload.Remove("current_tu");
        AssertNull(BattleTimelineState.from_dict(payload), "缺少 current_tu 应返回 null。");
    }

    private void TestExtraFieldReturnsNull()
    {
        GDictionary payload = ValidPayload();
        payload["speed"] = 5;
        AssertNull(BattleTimelineState.from_dict(payload), "额外旧字段应返回 null。");
    }

    private void TestWrongTypesReturnNull()
    {
        AssertNull(BattleTimelineState.from_dict(PayloadWith("current_tu", 1.0)), "current_tu 必须是 int。");
        AssertNull(BattleTimelineState.from_dict(PayloadWith("tu_per_tick", 5.0)), "tu_per_tick 必须是 int。");
        AssertNull(BattleTimelineState.from_dict(PayloadWith("frozen", 1)), "frozen 必须是 bool。");
        AssertNull(
            BattleTimelineState.from_dict(PayloadWith("ready_unit_ids", new GArray { 7 })),
            "ready_unit_ids entry 只能是 String/StringName。"
        );
    }

    private void TestStringNumbersReturnNull()
    {
        AssertNull(BattleTimelineState.from_dict(PayloadWith("current_tu", "1")), "current_tu 不接受字符串数字。");
        AssertNull(BattleTimelineState.from_dict(PayloadWith("tu_per_tick", "5")), "tu_per_tick 不接受字符串数字。");
    }

    private void TestEmptyReadyIdReturnsNull()
    {
        AssertNull(
            BattleTimelineState.from_dict(PayloadWith("ready_unit_ids", new GArray { "" })),
            "空 String ready id 应返回 null。"
        );
        AssertNull(
            BattleTimelineState.from_dict(
                PayloadWith("ready_unit_ids", new GArray { new StringName("") })
            ),
            "空 StringName ready id 应返回 null。"
        );
    }

    private void TestNonArrayReadyUnitIdsReturnsNull()
    {
        AssertNull(
            BattleTimelineState.from_dict(PayloadWith("ready_unit_ids", "hero")),
            "ready_unit_ids 非 Array 应返回 null。"
        );
    }

    private void TestNumericBoundariesReturnNull()
    {
        AssertNull(BattleTimelineState.from_dict(PayloadWith("current_tu", -1)), "current_tu 不能为负数。");
        AssertNull(BattleTimelineState.from_dict(PayloadWith("tu_per_tick", 0)), "tu_per_tick 必须为正数。");
        AssertNull(BattleTimelineState.from_dict(PayloadWith("tu_per_tick", -5)), "tu_per_tick 不能为负数。");
    }

    private static GDictionary ValidPayload()
    {
        return new GDictionary
        {
            ["current_tu"] = 10,
            ["tu_per_tick"] = 5,
            ["frozen"] = false,
            ["ready_unit_ids"] = new GArray { "hero", new StringName("enemy") },
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

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
        {
            _failures.Add($"{message} | actual={actual} expected={expected}");
        }
    }

    private void AssertStringNameArrayEq(GStringNameArray actual, IReadOnlyList<string> expected, string message)
    {
        if (actual == null || actual.Count != expected.Count)
        {
            _failures.Add($"{message} | actual={FormatStringNameArray(actual)} expected=[{string.Join(", ", expected)}]");
            return;
        }
        for (int index = 0; index < expected.Count; index++)
        {
            if (actual[index].ToString() != expected[index])
            {
                _failures.Add($"{message} | actual={FormatStringNameArray(actual)} expected=[{string.Join(", ", expected)}]");
                return;
            }
        }
    }

    private static string FormatStringNameArray(GStringNameArray values)
    {
        if (values == null)
        {
            return "<null>";
        }
        List<string> parts = new();
        foreach (StringName value in values)
        {
            parts.Add(value.ToString());
        }
        return $"[{string.Join(", ", parts)}]";
    }
}
