using System.Collections.Generic;
using Godot;

public partial class run_repeat_attack_decay_multiplier_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        AssertRepeatDamageScenario(
            multiplierPercent: 50,
            stageCount: 4,
            expectedDamage: 187,
            "50% 衰减应产生 100+50+25+12 的真实四段伤害。"
        );
        AssertRepeatDamageScenario(
            multiplierPercent: 200,
            stageCount: 3,
            expectedDamage: 700,
            "200% 放大应产生 100+200+400 的真实三段伤害。"
        );
        AssertRepeatDamageScenario(
            multiplierPercent: 100,
            stageCount: 3,
            expectedDamage: 300,
            "100% 身份倍率应让三段伤害保持等额。"
        );
        AssertRepeatDamageScenario(
            multiplierPercent: 0,
            stageCount: 3,
            expectedDamage: 300,
            "0% 非法倍率应在正式连击结算中回退到 100%。"
        );
        AssertRepeatDamageScenario(
            multiplierPercent: -50,
            stageCount: 3,
            expectedDamage: 300,
            "负倍率应在正式连击结算中回退到 100%。"
        );

        RequestTestExit(_test.Finish("Repeat attack decay multiplier regression"));
    }

    private void AssertRepeatDamageScenario(
        int multiplierPercent,
        int stageCount,
        int expectedDamage,
        string message
    )
    {
        StringName skillId = $"repeat_decay_{multiplierPercent}_{stageCount}";
        CombatEffectDefinition damageEffect = TestSkillDefinitionProjection.BuildEffect(
            "damage",
            effectTargetTeamFilter: "enemy",
            power: 100,
            damageTag: "force"
        );
        CombatEffectDefinition repeatEffect = TestSkillDefinitionProjection.BuildEffect(
            "repeat_attack_until_fail",
            effectTargetTeamFilter: "enemy",
            parameters: new Dictionary<string, object>
            {
                ["cost_resource"] = "aura",
                ["follow_up_fixed_cost"] = 1,
                ["follow_up_attack_penalty"] = 0,
                ["follow_up_damage_multiplier_percent"] = multiplierPercent,
            }
        );
        SkillDefinition skill = TestSkillDefinitionProjection.BuildSkill(
            skillId,
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                skillId,
                effects: new[] { damageEffect, repeatEffect }
            )
        );

        BattleUnitState source = BattleTestFixture.BuildUnit(
            $"repeat_decay_source_{multiplierPercent}_{stageCount}",
            "player",
            new Vector2I(1, 1),
            currentHp: 1000
        );
        source.AddKnownActiveSkill(skillId);
        source.SetKnownSkillLevelTyped(skillId, 1);
        source.SetCurrentAura(stageCount - 1);
        source.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 100);
        BattleUnitState target = BattleTestFixture.BuildUnit(
            $"repeat_decay_target_{multiplierPercent}_{stageCount}",
            "enemy",
            new Vector2I(2, 1),
            currentHp: 2000
        );
        target.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 1);

        using BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            $"repeat_decay_battle_{multiplierPercent}_{stageCount}",
            new Vector2I(4, 3),
            new[] { source },
            new[] { target }
        );
        BattleTestFixture.ConfigureDamageResolverForTests(
            fixture.Runtime,
            new FixedHitMaxDamageResolver()
        );
        BattleTestFixture.ConfigureHitResolverForTests(
            fixture.Runtime,
            new FixedHitResolver(10)
        );
        var resolver = new BattleRepeatAttackResolver();
        resolver.Setup(fixture.Runtime);
        try
        {
            int hpBefore = target.GetCurrentHp();
            using var batch = new BattleEventBatch();
            bool executed = resolver.ApplyRepeatAttackSkillResult(
                source,
                target,
                skill,
                skill.CombatProfile.EffectDefinitions,
                repeatEffect,
                batch
            );

            _test.True(executed, $"连击场景应至少命中第一段：{message}");
            _test.Eq(hpBefore - target.GetCurrentHp(), expectedDamage, message);
            _test.Eq(
                source.GetCurrentAura(),
                0,
                $"{stageCount} 段场景应消费 {stageCount - 1} 点追击 Aura 后停止。"
            );
        }
        finally
        {
            resolver.DisposeRuntime();
        }
    }
}
