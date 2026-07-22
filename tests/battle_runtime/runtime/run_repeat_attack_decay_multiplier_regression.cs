using System;
using System.Collections.Generic;
using Godot;

public partial class run_repeat_attack_decay_multiplier_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestStagePercentCompoundsDecay();
        TestStagePercentTruncatesTowardZero();
        TestStagePercentCompoundsAmplify();
        TestInvalidPercentFallsBackToIdentity();
        TestStageEffectsCarryDecayMultiplier();
        TestIdentityPercentKeepsEffectUntouched();

        RequestTestExit(_test.Finish("Repeat attack decay multiplier regression"));
    }

    private static CombatEffectDefinition BuildRepeatEffect(int multiplierPercent)
    {
        return TestSkillDefinitionProjection.BuildEffect(
            "repeat_attack_until_fail",
            effectTargetTeamFilter: "enemy",
            parameters: new Dictionary<string, object>
            {
                ["follow_up_damage_multiplier_percent"] = multiplierPercent,
            }
        );
    }

    private static CombatEffectDefinition BuildDamageEffect()
    {
        return TestSkillDefinitionProjection.BuildEffect(
            "damage",
            effectTargetTeamFilter: "enemy",
            power: 5,
            damageTag: "force"
        );
    }

    private void TestStagePercentCompoundsDecay()
    {
        var resolver = new BattleRepeatAttackResolver();
        CombatEffectDefinition repeat = BuildRepeatEffect(50);
        _test.Eq(
            resolver._get_repeat_attack_stage_damage_percent(repeat, 0),
            100,
            "衰减连击第 1 段应保持全额。"
        );
        _test.Eq(
            resolver._get_repeat_attack_stage_damage_percent(repeat, 1),
            50,
            "50% 衰减的第 2 段应为 50%,不允许被钳回 100%。"
        );
        _test.Eq(
            resolver._get_repeat_attack_stage_damage_percent(repeat, 2),
            25,
            "衰减按段整数复合,第 3 段应为 25%。"
        );
    }

    private void TestStagePercentTruncatesTowardZero()
    {
        var resolver = new BattleRepeatAttackResolver();
        CombatEffectDefinition repeat = BuildRepeatEffect(50);
        _test.Eq(
            resolver._get_repeat_attack_stage_damage_percent(repeat, 3),
            12,
            "整数复合向下截断:第 4 段应为 12%(25×50/100),不是 12.5 的四舍五入 13。"
        );
    }

    private void TestStagePercentCompoundsAmplify()
    {
        var resolver = new BattleRepeatAttackResolver();
        CombatEffectDefinition repeat = BuildRepeatEffect(200);
        _test.Eq(
            resolver._get_repeat_attack_stage_damage_percent(repeat, 1),
            200,
            "放大连击第 2 段应为 200%。"
        );
        _test.Eq(
            resolver._get_repeat_attack_stage_damage_percent(repeat, 2),
            400,
            "放大连击按段整数复合,第 3 段应为 400%。"
        );
    }

    private void TestInvalidPercentFallsBackToIdentity()
    {
        var resolver = new BattleRepeatAttackResolver();
        _test.Eq(
            resolver._get_repeat_attack_stage_damage_percent(BuildRepeatEffect(0), 1),
            100,
            "百分比 0 不合法,应回退为等额 100%。"
        );
        _test.Eq(
            resolver._get_repeat_attack_stage_damage_percent(BuildRepeatEffect(-50), 1),
            100,
            "负百分比不合法,应回退为等额 100%。"
        );
    }

    private void TestStageEffectsCarryDecayMultiplier()
    {
        var resolver = new BattleRepeatAttackResolver();
        CombatEffectDefinition repeat = BuildRepeatEffect(50);
        List<CombatEffectDefinition> staged = resolver._build_repeat_attack_stage_effects(
            new[] { BuildDamageEffect() },
            repeat,
            50
        );
        _test.Eq(staged.Count, 1, "阶段效果列表应保留伤害效果。");
        _test.True(
            Math.Abs(staged[0].PreResistanceDamageMultiplier - 0.5) < 1e-9,
            "衰减百分比应换算进阶段伤害效果的 pre_resistance 倍率。"
        );
    }

    private void TestIdentityPercentKeepsEffectUntouched()
    {
        var resolver = new BattleRepeatAttackResolver();
        CombatEffectDefinition repeat = BuildRepeatEffect(100);
        CombatEffectDefinition damage = BuildDamageEffect();
        List<CombatEffectDefinition> staged = resolver._build_repeat_attack_stage_effects(
            new[] { damage },
            repeat,
            100
        );
        _test.True(
            ReferenceEquals(staged[0], damage),
            "等额百分比不应重建效果定义。"
        );
    }
}
