using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_warrior_hamstring_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        try
        {
            ContentSnapshot snapshot = GameSessionTestFactory.GetProcessSnapshot();
            bool found = snapshot.Skills.TryGetValue(
                "warrior_hamstring",
                out SkillDefinition hamstring
            );
            _test.True(found && hamstring?.CombatProfile != null, "断筋斩正式技能配置应可加载。");

            if (hamstring?.CombatProfile != null)
            {
                TestProgressionContract(hamstring);
                TestLevelEffectsAndCosts(snapshot, hamstring);
                TestLevelDescriptions(hamstring);
            }
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        RequestTestExit(_test.Finish("Warrior hamstring regression"));
    }

    private void TestProgressionContract(SkillDefinition hamstring)
    {
        _test.Eq(hamstring.MaxLevel, 5, "断筋斩核心上限应为5级。");
        _test.Eq(hamstring.NonCoreMaxLevel, 3, "断筋斩非核心上限应为3级。");

        int[] expectedMasteryCurve = { 100, 250, 550, 1000, 1600 };
        _test.Eq(
            hamstring.MasteryCurve.Count,
            expectedMasteryCurve.Length,
            "断筋斩熟练度曲线应覆盖1至5级。"
        );
        for (
            int index = 0;
            index < expectedMasteryCurve.Length && index < hamstring.MasteryCurve.Count;
            index++
        )
        {
            _test.Eq(
                hamstring.MasteryCurve[index],
                expectedMasteryCurve[index],
                $"断筋斩熟练度阈值[{index}]应匹配批准方案。"
            );
        }
    }

    private void TestLevelEffectsAndCosts(ContentSnapshot snapshot, SkillDefinition hamstring)
    {
        _test.True(
            snapshot.Skills.TryGetValue("basic_attack", out SkillDefinition basicAttack)
                && basicAttack?.CombatProfile != null,
            "断筋斩体力基准回归需要正式普通攻击配置。"
        );
        int basicAttackStamina =
            basicAttack?.CombatProfile?.GetEffectiveResourceCostValues(0).StaminaCost ?? 0;

        int[] expectedAttackBonus = { 0, 1, 1, 1, 1, 1 };
        int[] expectedCooldownTu = { 80, 80, 80, 80, 80, 60 };
        int[] expectedSlowPower = { 1, 1, 2, 2, 2, 2 };
        int[] expectedSlowDurationTu = { 40, 40, 40, 60, 60, 60 };
        int[] expectedSkillDiceCount = { 0, 0, 0, 0, 1, 1 };
        int[] expectedSkillDiceSides = { 0, 0, 0, 0, 4, 4 };

        for (int level = 0; level <= 5; level++)
        {
            SkillEffectiveCombatDefinition effective =
                SkillEffectiveCombatDefinition.BuildUncached(hamstring, level);
            _test.Eq(
                effective.ResourceCosts.ApCost,
                1,
                $"断筋斩{level}级应消耗1AP。"
            );
            _test.Eq(
                effective.ResourceCosts.StaminaCost,
                20,
                $"断筋斩{level}级应固定消耗20体力。"
            );
            _test.True(
                effective.ResourceCosts.StaminaCost > basicAttackStamina,
                $"断筋斩{level}级体力消耗应高于普通攻击。"
            );
            _test.Eq(
                effective.ResourceCosts.CooldownTu,
                expectedCooldownTu[level],
                $"断筋斩{level}级冷却应匹配批准方案。"
            );
            _test.Eq(
                effective.AttackRollBonus,
                expectedAttackBonus[level],
                $"断筋斩{level}级攻击检定加值应匹配批准方案。"
            );

            List<CombatEffectDefinition> activeEffects = ActiveEffects(
                hamstring.CombatProfile.EffectDefinitions,
                level
            );
            CombatEffectDefinition damage = FindEffect(activeEffects, "damage");
            CombatEffectDefinition slow = FindEffect(activeEffects, "status", "slow");

            _test.Eq(
                activeEffects.Count,
                2,
                $"断筋斩{level}级应恰好激活一个伤害和一个减速效果。"
            );
            _test.True(damage != null, $"断筋斩{level}级应激活伤害效果。");
            _test.True(slow != null, $"断筋斩{level}级应激活slow效果。");
            if (damage != null)
            {
                _test.True(damage.AddWeaponDice, $"断筋斩{level}级应使用当前武器伤害骰。");
                _test.True(damage.RequiresWeapon, $"断筋斩{level}级应要求装备武器。");
                _test.Eq(
                    damage.DiceCount,
                    expectedSkillDiceCount[level],
                    $"断筋斩{level}级技能骰数量应匹配批准方案。"
                );
                _test.Eq(
                    damage.DiceSides,
                    expectedSkillDiceSides[level],
                    $"断筋斩{level}级技能骰面数应匹配批准方案。"
                );
            }
            if (slow != null)
            {
                _test.Eq(
                    slow.Power,
                    expectedSlowPower[level],
                    $"断筋斩{level}级移动成本增量应匹配批准方案。"
                );
                _test.Eq(
                    slow.DurationTu,
                    expectedSlowDurationTu[level],
                    $"断筋斩{level}级减速持续时间应匹配批准方案。"
                );
                _test.Eq(
                    BattleStatusSemanticTable.GetMoveCostDelta(
                        new BattleStatusEffectState
                        {
                            status_id = slow.StatusId,
                            power = slow.Power,
                        }
                    ),
                    expectedSlowPower[level],
                    $"断筋斩{level}级slow应按强度增加真实移动成本。"
                );
            }
        }
    }

    private void TestLevelDescriptions(SkillDefinition hamstring)
    {
        _test.True(
            hamstring.Description.Contains("移动成本")
                && !hamstring.Description.Contains("降低移动力"),
            "断筋斩总描述应使用真实的移动成本语义。"
        );

        string[] descriptions = new string[6];
        for (int level = 0; level <= 5; level++)
        {
            descriptions[level] = SkillLevelDescriptionFormatter.BuildLevelDescription(
                hamstring,
                level,
                new GDictionary()
            );
            _test.True(
                !string.IsNullOrWhiteSpace(descriptions[level]),
                $"断筋斩{level}级应有等级描述。"
            );
            _test.True(
                !descriptions[level].Contains("{") && !descriptions[level].Contains("}"),
                $"断筋斩{level}级描述不应残留模板占位符。"
            );
            _test.True(
                descriptions[level].Contains("20体力"),
                $"断筋斩{level}级描述应显示20体力。"
            );
        }

        _test.True(
            !descriptions[0].Contains("攻击检定"),
            "断筋斩0级不应显示无意义的攻击检定+0。"
        );
        _test.True(
            descriptions[1].Contains("攻击检定+1"),
            "断筋斩1级描述应显示攻击检定+1。"
        );
        _test.True(
            descriptions[2].Contains("移动成本+2"),
            "断筋斩2级描述应显示移动成本+2。"
        );
        _test.True(
            descriptions[3].Contains("持续60TU"),
            "断筋斩3级描述应显示60TU减速。"
        );
        _test.True(
            descriptions[4].Contains("1D4技能骰"),
            "断筋斩4级描述应显示追加1D4技能骰。"
        );
        _test.True(
            descriptions[5].Contains("冷却60TU"),
            "断筋斩5级描述应显示60TU冷却。"
        );
    }

    private static List<CombatEffectDefinition> ActiveEffects(
        IReadOnlyList<CombatEffectDefinition> effects,
        int skillLevel
    )
    {
        var result = new List<CombatEffectDefinition>();
        foreach (CombatEffectDefinition effect in effects)
        {
            if (
                effect != null
                && skillLevel >= Mathf.Max(effect.MinSkillLevel, 0)
                && (effect.MaxSkillLevel < 0 || skillLevel <= effect.MaxSkillLevel)
            )
            {
                result.Add(effect);
            }
        }
        return result;
    }

    private static CombatEffectDefinition FindEffect(
        IReadOnlyList<CombatEffectDefinition> effects,
        StringName effectType,
        StringName statusId = default
    )
    {
        foreach (CombatEffectDefinition effect in effects)
        {
            if (
                effect?.EffectType == effectType
                && (statusId == null || statusId.IsEmpty || effect.StatusId == statusId)
            )
            {
                return effect;
            }
        }
        return null;
    }
}
