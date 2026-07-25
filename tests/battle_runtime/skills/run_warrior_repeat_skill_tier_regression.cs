using System;
using System.Collections.Generic;
using Godot;

public partial class run_warrior_repeat_skill_tier_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            SkillDefinition doubleStrike = LoadSkill(
                "warrior_double_strike",
                "res://data/configs/skills/warrior_double_strike.tres"
            );
            SkillDefinition comboStrike = LoadSkill(
                "warrior_combo_strike",
                "res://data/configs/skills/warrior_combo_strike.tres"
            );
            SkillDefinition saintBladeCombo = LoadSkill(
                "saint_blade_combo",
                "res://data/configs/skills/saint_blade_combo.tres"
            );
            TestDoubleStrikeTier(doubleStrike);
            TestComboStrikeTier(comboStrike);
            TestComboPenaltyFreeStages(comboStrike);
            TestLinearPenaltyRestartsAfterFreeStages(saintBladeCombo);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Warrior repeat skill tier regression"));
    }

    private void TestLinearPenaltyRestartsAfterFreeStages(SkillDefinition skill)
    {
        CombatEffectDefinition repeatEffect = FindRepeatEffect(skill);
        _test.True(repeatEffect != null, "圣剑连斩应声明命中前重复攻击效果。");
        if (repeatEffect == null)
            return;

        BattleRepeatAttackStageSpec firstPenalized =
            BattleRepeatAttackStageSpec.FromRepeatAttackEffect(
                repeatEffect,
                stage_index_value: 3,
                stage_count_value: 0,
                skill_level_value: 5
            );
        BattleRepeatAttackStageSpec secondPenalized =
            BattleRepeatAttackStageSpec.FromRepeatAttackEffect(
                repeatEffect,
                stage_index_value: 4,
                stage_count_value: 0,
                skill_level_value: 5
            );
        _test.Eq(
            firstPenalized.ResolveStageAttackPenalty(),
            2,
            "线性连击的第一段受罚攻击应从基础惩罚-2开始。"
        );
        _test.Eq(
            secondPenalized.ResolveStageAttackPenalty(),
            4,
            "线性连击的第二段受罚攻击应递增到-4。"
        );
    }

    private void TestDoubleStrikeTier(SkillDefinition skill)
    {
        _test.True(skill != null, "应能加载双重打击正式资源。");
        if (skill == null)
            return;

        _test.Eq(skill.NonCoreMaxLevel, 3, "双重打击非核心上限应压回3级。");
        _test.Eq(skill.MaxLevel, 5, "双重打击核心上限应压回5级。");
        _test.Eq(skill.MasteryCurve.Count, 5, "双重打击熟练度曲线应与5级上限一致。");
        _test.Eq(skill.GrowthTier, new StringName("basic"), "双重打击应属于基础成长档。");
        _test.Eq(SumGrowth(skill), 60, "双重打击基础成长预算应为60。");
    }

    private void TestComboStrikeTier(SkillDefinition skill)
    {
        _test.True(skill != null, "应能加载连击正式资源。");
        if (skill == null)
            return;

        _test.Eq(skill.NonCoreMaxLevel, 5, "连击非核心上限应提升到5级。");
        _test.Eq(skill.MaxLevel, 7, "连击核心上限应提升到7级。");
        _test.Eq(skill.MasteryCurve.Count, 7, "连击熟练度曲线应覆盖7级上限。");
        _test.Eq(skill.GetMasteryRequiredForLevel(5), 4700, "连击5升6应需要4700熟练度。");
        _test.Eq(skill.GetMasteryRequiredForLevel(6), 6500, "连击6升7应需要6500熟练度。");
        _test.Eq(skill.GrowthTier, new StringName("intermediate"), "连击应属于中阶成长档。");
        _test.Eq(SumGrowth(skill), 120, "连击中阶成长预算应为120。");
        _test.True(skill.LevelDescriptionConfigs.ContainsKey(6), "连击应提供6级说明数据。");
        _test.True(skill.LevelDescriptionConfigs.ContainsKey(7), "连击应提供7级说明数据。");
    }

    private void TestComboPenaltyFreeStages(SkillDefinition skill)
    {
        CombatEffectDefinition repeatEffect = FindRepeatEffect(skill);
        _test.True(repeatEffect != null, "连击应声明命中前重复攻击效果。");
        if (repeatEffect == null)
            return;

        AssertPenaltyProfile(repeatEffect, level: 5, freeStages: 3);
        AssertPenaltyProfile(repeatEffect, level: 6, freeStages: 4);
        AssertPenaltyProfile(repeatEffect, level: 7, freeStages: 4);
    }

    private void AssertPenaltyProfile(
        CombatEffectDefinition repeatEffect,
        int level,
        int freeStages
    )
    {
        for (int stageIndex = 0; stageIndex < freeStages; stageIndex++)
        {
            BattleRepeatAttackStageSpec freeSpec =
                BattleRepeatAttackStageSpec.FromRepeatAttackEffect(
                    repeatEffect,
                    stageIndex,
                    0,
                    level
                );
            _test.Eq(
                freeSpec.ResolveStageAttackPenalty(),
                0,
                $"连击{level}级第{stageIndex + 1}段应免除命中惩罚。"
            );
        }

        BattleRepeatAttackStageSpec penalizedSpec =
            BattleRepeatAttackStageSpec.FromRepeatAttackEffect(
                repeatEffect,
                freeStages,
                0,
                level
            );
        _test.Eq(
            penalizedSpec.ResolveStageAttackPenalty(),
            1,
            $"连击{level}级第一段受罚攻击应从-1开始。"
        );

        BattleRepeatAttackStageSpec secondPenalizedSpec =
            BattleRepeatAttackStageSpec.FromRepeatAttackEffect(
                repeatEffect,
                freeStages + 1,
                0,
                level
            );
        _test.Eq(
            secondPenalizedSpec.ResolveStageAttackPenalty(),
            2,
            $"连击{level}级第二段受罚攻击应递增到-2。"
        );
    }

    private static SkillDefinition LoadSkill(StringName skillId, string path) =>
        TestSkillDefinitionProjection.LoadSkillDefinition(
            path,
            $"{skillId}_tier_regression"
        );

    private static CombatEffectDefinition FindRepeatEffect(SkillDefinition skill)
    {
        foreach (
            CombatEffectDefinition effect in
                skill?.CombatProfile?.EffectDefinitions
                    ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (effect?.EffectKind == BattleEffectKind.RepeatAttackUntilFail)
                return effect;
        }
        return null;
    }

    private static int SumGrowth(SkillDefinition skill)
    {
        int total = 0;
        foreach (KeyValuePair<StringName, int> entry in skill.AttributeGrowthProgress)
            total += entry.Value;
        return total;
    }
}
