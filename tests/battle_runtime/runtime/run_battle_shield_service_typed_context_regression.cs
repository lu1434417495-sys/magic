using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;

public partial class run_battle_shield_service_typed_context_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestTypedRollContextCachesShieldHp();
        TestGodotBoundaryRollContextRoundTrips();
        TestTypedApplyPathUsesSharedContext();
        TestApplyResultPublicApiStaysTyped();
        TestApplyResultProjectsInternalBoundary();
        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(_test.Finish("Battle shield service typed context regression"));
    }

    private void TestTypedRollContextCachesShieldHp()
    {
        var service = new BattleShieldService();
        BattleUnitState source = BuildUnit("typed_context_source");
        CombatEffectDef effect = BuildAttributeScaledShieldEffect();
        long cacheKey = service._get_shield_roll_cache_key(effect);
        var rollContext = new Dictionary<long, int>();

        int shieldHp = service.ResolveShieldHp(source, effect, rollContext);

        _test.Eq(shieldHp, 7, "typed context 首次 roll 应使用属性缩放骰和 dice_bonus。");
        _test.True(rollContext.ContainsKey(cacheKey), "typed context 应写入 shield roll cache。");
        _test.Eq(rollContext[cacheKey], 7, "typed context cache 值应等于已解析护盾。");

        rollContext[cacheKey] = 19;
        _test.Eq(
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
        var godotContext = new Godot.Collections.Dictionary();

        int shieldHp = service._resolve_shield_hp(source, effect, godotContext);

        _test.Eq(shieldHp, 7, "Godot 边界首次 roll 应保持现有行为。");
        _test.True(HasKey(godotContext, cacheKey), "Godot 边界应写回 roll context。");
        _test.Eq(ReadInt(godotContext, cacheKey), 7, "Godot 边界 context 值应写回已解析护盾。");

        godotContext[cacheKey.ToString(CultureInfo.InvariantCulture)] = 23;
        _test.Eq(
            service._resolve_shield_hp(source, effect, godotContext),
            23,
            "Godot 边界传入已有 cache 时应桥接到 typed context。"
        );
    }

    private void TestApplyResultPublicApiStaysTyped()
    {
        Type type = typeof(BattleShieldApplyResult);
    }

    private void TestApplyResultProjectsInternalBoundary()
    {
        var result = new BattleShieldApplyResult(
            true,
            9,
            12,
            60,
            new StringName("test_shield_family")
        );

        Godot.Collections.Dictionary payload = result.ToDictionary();

        _test.True(payload["applied"].AsBool(), "shield apply result 应投影 applied。");
        _test.Eq(payload["current_shield_hp"].AsInt32(), 9, "shield apply result 应投影当前护盾。");
        _test.Eq(payload["shield_max_hp"].AsInt32(), 12, "shield apply result 应投影最大护盾。");
        _test.Eq(payload["shield_duration"].AsInt32(), 60, "shield apply result 应投影持续时间。");
        _test.Eq(
            payload["shield_family"].AsStringName(),
            new StringName("test_shield_family"),
            "shield apply result 应投影护盾族。"
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

        _test.True(result.Applied, "typed apply path 应成功应用 shield。");
        _test.Eq(target.current_shield_hp, 7, "typed apply path 应写入解析后的 shield hp。");
        _test.Eq(result.CurrentShieldHp, 7, "typed apply result 应返回当前 shield hp。");
        _test.True(
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
        unit.attribute_snapshot.SetValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Constitution), 14);
        unit.attribute_snapshot.SetValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Willpower), 12);
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

    private static int ReadInt(Godot.Collections.Dictionary source, long key)
    {
        return source[key.ToString(CultureInfo.InvariantCulture)].AsInt32();
    }

    private static bool HasKey(Godot.Collections.Dictionary source, long key)
    {
        return source.ContainsKey(key.ToString(CultureInfo.InvariantCulture));
    }

    private static bool IsGodotCollectionOrVariant(Type type)
    {
        if (type == typeof(Variant))
            return true;
        Type genericDefinition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
        return genericDefinition == typeof(Godot.Collections.Dictionary)
            || genericDefinition == typeof(Godot.Collections.Array)
            || type.Namespace == "Godot.Collections";
    }
}
