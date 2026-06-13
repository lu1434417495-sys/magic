using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_battle_timeline_state_schema_regression : SceneTree
{
    private readonly TestHarness _test = new();

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

        Quit(_test.Finish("Battle timeline state schema regression"));
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

        BattleTimelineState restored = BattleTimelineState.FromDictionary(state.ToDictionary());
        _test.True(restored != null, "合法 to_dict payload 应能恢复。");
        if (restored == null)
        {
            return;
        }

        _test.Eq(restored.current_tu, 15, "roundtrip 应保留 current_tu。");
        _test.Eq(restored.tu_per_tick, 5, "roundtrip 应保留 tu_per_tick。");
        _test.True(restored.frozen, "roundtrip 应保留 frozen。");
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
        _test.True(BattleTimelineState.FromDictionary(payload) == null, "缺少 current_tu 应返回 null。");
    }

    private void TestExtraFieldReturnsNull()
    {
        GDictionary payload = ValidPayload();
        payload["speed"] = 5;
        _test.True(BattleTimelineState.FromDictionary(payload) == null, "额外旧字段应返回 null。");
    }

    private void TestWrongTypesReturnNull()
    {
        _test.True(BattleTimelineState.FromDictionary(PayloadWith("current_tu", 1.0)) == null, "current_tu 必须是 int。");
        _test.True(BattleTimelineState.FromDictionary(PayloadWith("tu_per_tick", 5.0)) == null, "tu_per_tick 必须是 int。");
        _test.True(BattleTimelineState.FromDictionary(PayloadWith("frozen", 1)) == null, "frozen 必须是 bool。");
        _test.True(
            BattleTimelineState.FromDictionary(PayloadWith("ready_unit_ids", new GArray { 7 })) == null,
            "ready_unit_ids entry 只能是 String/StringName。"
        );
    }

    private void TestStringNumbersReturnNull()
    {
        _test.True(BattleTimelineState.FromDictionary(PayloadWith("current_tu", "1")) == null, "current_tu 不接受字符串数字。");
        _test.True(BattleTimelineState.FromDictionary(PayloadWith("tu_per_tick", "5")) == null, "tu_per_tick 不接受字符串数字。");
    }

    private void TestEmptyReadyIdReturnsNull()
    {
        _test.True(
            BattleTimelineState.FromDictionary(PayloadWith("ready_unit_ids", new GArray { "" })) == null,
            "空 String ready id 应返回 null。"
        );
        _test.True(
            BattleTimelineState.FromDictionary(
                PayloadWith("ready_unit_ids", new GArray { new StringName("") })
            ) == null,
            "空 StringName ready id 应返回 null。"
        );
    }

    private void TestNonArrayReadyUnitIdsReturnsNull()
    {
        _test.True(
            BattleTimelineState.FromDictionary(PayloadWith("ready_unit_ids", "hero")) == null,
            "ready_unit_ids 非 Array 应返回 null。"
        );
    }

    private void TestNumericBoundariesReturnNull()
    {
        _test.True(BattleTimelineState.FromDictionary(PayloadWith("current_tu", -1)) == null, "current_tu 不能为负数。");
        _test.True(BattleTimelineState.FromDictionary(PayloadWith("tu_per_tick", 0)) == null, "tu_per_tick 必须为正数。");
        _test.True(BattleTimelineState.FromDictionary(PayloadWith("tu_per_tick", -5)) == null, "tu_per_tick 不能为负数。");
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

    private void AssertStringNameArrayEq(GStringNameArray actual, IReadOnlyList<string> expected, string message)
    {
        if (actual == null || actual.Count != expected.Count)
        {
            _test.Fail($"{message} | actual={FormatStringNameArray(actual)} expected=[{string.Join(", ", expected)}]");
            return;
        }
        for (int index = 0; index < expected.Count; index++)
        {
            if (actual[index].ToString() != expected[index])
            {
                _test.Fail($"{message} | actual={FormatStringNameArray(actual)} expected=[{string.Join(", ", expected)}]");
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
