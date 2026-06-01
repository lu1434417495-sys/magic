using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_battle_shield_service_typed_context_regression : SceneTree
{
    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        GodotSharpCleanup.collect_pending_finalizers();
        Quit(exitCode);
    }

    private int Run()
    {
        TestTypedRollContextCachesShieldHp();
        TestGodotBoundaryRollContextRoundTrips();
        TestTypedApplyPathUsesSharedContext();

        if (_failures.Count == 0)
        {
            GD.Print("Battle shield service typed context regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle shield service typed context regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestTypedRollContextCachesShieldHp()
    {
        var service = new BattleShieldService();
        BattleUnitState source = BuildUnit("typed_context_source");
        CombatEffectDef effect = BuildAttributeScaledShieldEffect();
        long cacheKey = service._get_shield_roll_cache_key(effect);
        var rollContext = new Dictionary<long, int>();

        int shieldHp = service.ResolveShieldHp(source, effect, rollContext);

        AssertEq(shieldHp, 7, "typed context 首次 roll 应使用属性缩放骰和 dice_bonus。");
        AssertTrue(rollContext.ContainsKey(cacheKey), "typed context 应写入 shield roll cache。");
        AssertEq(rollContext[cacheKey], 7, "typed context cache 值应等于已解析护盾。");

        rollContext[cacheKey] = 19;
        AssertEq(
            service.ResolveShieldHp(source, effect, rollContext),
            19,
            "typed context 命中 cache 时不应重新 roll。"
        );
    }

    private void TestGodotBoundaryRollContextRoundTrips()
    {
        var service = new BattleShieldService();
        BattleUnitState source = BuildUnit("godot_context_source");
        CombatEffectDef effect = BuildAttributeScaledShieldEffect();
        long cacheKey = service._get_shield_roll_cache_key(effect);
        var godotContext = new GDictionary();

        int shieldHp = service._resolve_shield_hp(source, effect, godotContext);

        AssertEq(shieldHp, 7, "Godot 边界首次 roll 应保持现有行为。");
        AssertTrue(godotContext.ContainsKey(cacheKey), "Godot 边界应写回 roll context。");
        AssertEq(ReadInt(godotContext, cacheKey), 7, "Godot 边界 context 值应写回已解析护盾。");

        godotContext[cacheKey] = 23;
        AssertEq(
            service._resolve_shield_hp(source, effect, godotContext),
            23,
            "Godot 边界传入已有 cache 时应桥接到 typed context。"
        );
    }

    private void TestTypedApplyPathUsesSharedContext()
    {
        var service = new BattleShieldService();
        BattleUnitState source = BuildUnit("typed_apply_source");
        BattleUnitState target = BuildUnit("typed_apply_target");
        var skillDef = new SkillDef { skill_id = "typed_shield_skill" };
        CombatEffectDef effect = BuildAttributeScaledShieldEffect();
        var rollContext = new Dictionary<long, int>();

        BattleShieldApplyResult result = service.ApplyUnitShieldEffectsResult(
            source,
            target,
            skillDef,
            new[] { effect },
            rollContext
        );

        AssertTrue(result.Applied, "typed apply path 应成功应用 shield。");
        AssertEq(target.current_shield_hp, 7, "typed apply path 应写入解析后的 shield hp。");
        AssertEq(result.CurrentShieldHp, 7, "typed apply result 应返回当前 shield hp。");
        AssertTrue(
            rollContext.ContainsKey(service._get_shield_roll_cache_key(effect)),
            "typed apply path 应复用传入的 roll context。"
        );
    }

    private static BattleUnitState BuildUnit(string unitId)
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            faction_id = "player",
            is_alive = true,
            current_hp = 20,
        };
        unit.attribute_snapshot.set_value(UnitBaseAttributes.CONSTITUTION(), 14);
        unit.attribute_snapshot.set_value(UnitBaseAttributes.WILLPOWER(), 12);
        return unit;
    }

    private static CombatEffectDef BuildAttributeScaledShieldEffect()
    {
        return new CombatEffectDef
        {
            effect_type = "shield",
            dice_count = 2,
            dice_sides_base = 4,
            dice_sides_per_constitution_mod = 1,
            dice_sides_per_willpower_mod = 1,
            dice_bonus = 5,
            duration_tu = 60,
        };
    }

    private static int ReadInt(GDictionary source, long key)
    {
        return source[Variant.From(key)].AsInt32();
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
        {
            _failures.Add($"{message} expected={expected} actual={actual}");
        }
    }

    private void AssertTrue(bool value, string message)
    {
        if (!value)
        {
            _failures.Add(message);
        }
    }
}
