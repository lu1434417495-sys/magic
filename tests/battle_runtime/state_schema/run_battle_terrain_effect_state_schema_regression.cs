using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_terrain_effect_state_schema_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestParamsLifetimePolicyRoundtrip();
        TestTopLevelLifetimePolicyIsRejected();
        TestInvalidTargetTeamFilterIsRejected();

        if (_failures.Count == 0)
        {
            GD.Print("Battle terrain effect state schema regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle terrain effect state schema regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestParamsLifetimePolicyRoundtrip()
    {
        BattleTerrainEffectState effect = BuildEffect();
        BattleTerrainEffectState restored = BattleTerrainEffectState.from_dict(effect.to_dict());
        AssertTrue(restored != null, "terrain effect state roundtrip 应恢复对象。");
        AssertEq(
            restored != null ? DictString(restored.@params, "lifetime_policy", "") : "",
            "battle",
            "lifetime_policy 应只通过 params roundtrip。"
        );
        AssertEq(
            restored != null ? restored.remaining_tu : -1,
            0,
            "battle lifetime terrain effect 应允许 remaining_tu=0。"
        );
        AssertEq(
            restored != null ? restored.tick_interval_tu : -1,
            0,
            "battle lifetime terrain effect 应允许 tick_interval_tu=0。"
        );
    }

    private void TestTopLevelLifetimePolicyIsRejected()
    {
        GDictionary payload = BuildEffect().to_dict();
        payload["lifetime_policy"] = "battle";
        AssertTrue(
            BattleTerrainEffectState.from_dict(payload) == null,
            "terrain effect strict schema 应拒绝顶层 lifetime_policy 字段。"
        );
    }

    private void TestInvalidTargetTeamFilterIsRejected()
    {
        GDictionary payload = BuildEffect().to_dict();
        payload["target_team_filter"] = "hostile";
        AssertTrue(
            BattleTerrainEffectState.from_dict(payload) == null,
            "terrain effect state 不应接受 hostile 作为 target_team_filter。"
        );
    }

    private static BattleTerrainEffectState BuildEffect()
    {
        return new BattleTerrainEffectState
        {
            field_instance_id = "meteor_crater_core_1",
            effect_id = "meteor_swarm_crater_core",
            effect_type = "none",
            source_unit_id = "caster",
            source_skill_id = "mage_meteor_swarm",
            target_team_filter = "any",
            power = 0,
            damage_tag = "",
            remaining_tu = 0,
            tick_interval_tu = 0,
            next_tick_at_tu = 0,
            stack_behavior = "refresh",
            @params = new GDictionary
            {
                ["lifetime_policy"] = "battle",
                ["move_cost_delta"] = 3,
                ["render_overlay_id"] = "meteor_crater_core",
            },
        };
    }

    private static string DictString(GDictionary dictionary, string key, string defaultValue)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return defaultValue;
        }
        Variant value = dictionary[key];
        return value.VariantType is Variant.Type.String or Variant.Type.StringName
            ? value.AsString()
            : defaultValue;
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
        if (EqualityComparer<T>.Default.Equals(actual, expected))
        {
            return;
        }
        _failures.Add($"{message} actual={actual} expected={expected}");
    }
}
