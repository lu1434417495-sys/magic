using System;
using Godot;
using GArray = Godot.Collections.Array;

public partial class run_extra_damage_segment_critical_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestValidatorRequiresDiceForCriticalDoubling();
            TestConfiguredSegmentAddsOneCriticalDicePoolWithoutRepeatingBonus();
            TestDefaultSegmentDoesNotInheritCriticalDoubling();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(
            _test.Finish("Extra damage segment critical regression")
        );
    }

    private void TestValidatorRequiresDiceForCriticalDoubling()
    {
        var segment = new CombatDamageSegmentDef
        {
            damage_tag = "fire",
            power = 3,
            double_dice_on_critical = true,
        };
        var effect = new CombatEffectDef
        {
            effect_type = "damage",
            damage_tag = "physical_blunt",
        };
        effect.extra_damage_segments.Add(segment);
        var errors = new Godot.Collections.Array<string>();

        new SkillDamageEffectValidator().AppendDamageEffectValidationErrors(
            errors,
            "test_segment_critical_schema",
            effect,
            "effect[0]"
        );

        _test.True(
            ContainsError(errors, "double_dice_on_critical requires dice_count and dice_sides"),
            "开启附段暴击骰但没有合法骰池时，正式 validator 必须拒绝。"
        );
        effect.Dispose();
        segment.Dispose();
    }

    private void TestConfiguredSegmentAddsOneCriticalDicePoolWithoutRepeatingBonus()
    {
        CombatEffectDefinition effect = BuildDamageEffect(
            doubleDiceOnCritical: true
        );
        var resolver = new FixedRollDamageResolver(Rolls(4, 5));

        AttackEffectResolutionResult result = resolver.ResolveEffects(
            BuildUnit("configured_segment_source"),
            BuildUnit("configured_segment_target"),
            new[] { effect },
            DamageResolutionContext.Create(
                criticalHit: true,
                attackSuccess: true,
                secondaryHitSuccess: false
            )
        );

        _test.Eq(result.DamageEvents.Length, 2, "主段与附段应产生两个正式伤害事件。");
        if (result.DamageEvents.Length != 2)
            return;
        DamageEventResult segmentEvent = result.DamageEvents[1];
        _test.Eq(segmentEvent.DamageTag, new StringName("fire"), "第二个事件应是fire附段。");
        _test.True(segmentEvent.CriticalHit, "显式开启的附段应记录为暴击事件。");
        _test.Eq(segmentEvent.DamageDice.Total, 4, "附段普通1D10应使用第一枚固定骰。");
        _test.Eq(
            segmentEvent.CriticalExtraDamageDice.Total,
            5,
            "附段暴击应额外掷且只掷一组同规格骰池。"
        );
        _test.Eq(segmentEvent.DamageDice.Bonus, 2, "附段静态dice_bonus应保留在普通骰池。");
        _test.Eq(
            segmentEvent.CriticalExtraDamageDice.Bonus,
            0,
            "附段暴击追加骰不得重复计算dice_bonus。"
        );
        _test.Eq(segmentEvent.BaseDamage, 11, "附段应为4+2+5，不翻倍power或bonus。");
        _test.Eq(result.Damage, 12, "主段1点与附段11点应汇总为12点伤害。");
    }

    private void TestDefaultSegmentDoesNotInheritCriticalDoubling()
    {
        CombatEffectDefinition effect = BuildDamageEffect(
            doubleDiceOnCritical: false
        );
        var resolver = new FixedRollDamageResolver(Rolls(4, 9));

        AttackEffectResolutionResult result = resolver.ResolveEffects(
            BuildUnit("default_segment_source"),
            BuildUnit("default_segment_target"),
            new[] { effect },
            DamageResolutionContext.Create(
                criticalHit: true,
                attackSuccess: true,
                secondaryHitSuccess: false
            )
        );

        _test.Eq(result.DamageEvents.Length, 2, "默认附段仍应独立生成伤害事件。");
        if (result.DamageEvents.Length != 2)
            return;
        DamageEventResult segmentEvent = result.DamageEvents[1];
        _test.False(segmentEvent.CriticalHit, "未配置的附段不得全局继承主攻击暴击翻倍。");
        _test.Eq(
            segmentEvent.CriticalExtraDamageDice.Count,
            0,
            "默认附段不得消耗或记录暴击追加骰。"
        );
        _test.Eq(segmentEvent.BaseDamage, 6, "默认附段只应结算1D10结果4与bonus2。");
        _test.Eq(result.Damage, 7, "默认附段不能误用队列中的第二枚固定骰。");
    }

    private static CombatEffectDefinition BuildDamageEffect(bool doubleDiceOnCritical)
    {
        var segment = new CombatDamageSegmentDef
        {
            damage_tag = "fire",
            dice_count = 1,
            dice_sides = 10,
            dice_bonus = 2,
            double_dice_on_critical = doubleDiceOnCritical,
        };
        segment.damage_tags.Add("fire");
        var resource = new CombatEffectDef
        {
            effect_type = "damage",
            damage_tag = "physical_blunt",
            power = 1,
        };
        resource.extra_damage_segments.Add(segment);
        CombatEffectDefinition definition = CombatEffectDefinition.FromResource(
            resource,
            "test://extra_damage_segment_critical"
        );
        resource.Dispose();
        segment.Dispose();
        return definition;
    }

    private static BattleUnitState BuildUnit(StringName unitId) =>
        new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "player",
        }.WithCombatResourcesForTest(hp: 100, isAlive: true);

    private static GArray Rolls(params int[] values)
    {
        var rolls = new GArray();
        foreach (int value in values ?? Array.Empty<int>())
            rolls.Add(value);
        return rolls;
    }

    private static bool ContainsError(
        Godot.Collections.Array<string> errors,
        string fragment
    )
    {
        foreach (string error in errors ?? new Godot.Collections.Array<string>())
        {
            if (error?.Contains(fragment, StringComparison.Ordinal) == true)
                return true;
        }
        return false;
    }
}
