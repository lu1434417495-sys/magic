using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_skill_description_consistency_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    private ContentSnapshot _contentSnapshot;

    public override void _Initialize()
    {
        ProcessFrame += RunOnFirstProcessFrame;
    }

    private void RunOnFirstProcessFrame()
    {
        ProcessFrame -= RunOnFirstProcessFrame;
        _contentSnapshot = GameSessionTestFactory.GetProcessSnapshot();
        Run();
    }

    private void Run()
    {
        TestChainLightningDescriptionInputsMatchSaveEnabledEffects();

        RequestTestExit(_test.Finish("Skill description consistency regression"));
    }

    private void TestChainLightningDescriptionInputsMatchSaveEnabledEffects()
    {
        SkillDefinition chainLightningDefinition = GetSkillDefinition(
            _contentSnapshot.Skills,
            "mage_chain_lightning"
        );
        _test.True(chainLightningDefinition != null, "链式闪击技能应存在。");
        if (chainLightningDefinition == null)
        {
            return;
        }

        string level0Description = SkillLevelDescriptionFormatter.BuildLevelDescription(
            chainLightningDefinition,
            0,
            new GDictionary()
        );
        string level7Description = SkillLevelDescriptionFormatter.BuildLevelDescription(
            chainLightningDefinition,
            7,
            new GDictionary()
        );
        _test.True(!string.IsNullOrWhiteSpace(level0Description), "链式闪击 0 级描述应能从 typed 数据生成。");
        _test.True(!string.IsNullOrWhiteSpace(level7Description), "链式闪击 7 级描述应能从 typed 数据生成。");

        CombatSkillDefinition combat = chainLightningDefinition.CombatProfile;
        _test.True(combat != null, "链式闪击应有 combat_profile。");
        if (combat == null)
            return;

        _test.Eq(combat.RangeValue, 5, "链式闪击描述输入应保留射程。");
        _test.Eq(combat.ApCost, 1, "链式闪击描述输入应保留 AP 消耗。");
        _test.Eq(combat.MpCost, 120, "链式闪击描述输入应保留 MP 消耗。");
        _test.Eq(combat.CooldownTu, 60, "链式闪击描述输入应保留冷却。");

        CombatEffectDefinition level0Damage = FindEffect(combat, "damage", 0);
        CombatEffectDefinition level7Damage = FindEffect(combat, "damage", 7);
        _test.True(level0Damage != null, "链式闪击应存在 0 级伤害 effect。");
        _test.True(level7Damage != null, "链式闪击应存在 7 级伤害 effect。");
        if (level0Damage != null)
        {
            _test.Eq(level0Damage.DiceCount, 4, "链式闪击 0 级伤害骰数量应来自 typed effect。");
            _test.Eq(level0Damage.DiceSides, 6, "链式闪击 0 级伤害骰面应来自 typed effect。");
            _test.Eq(level0Damage.SaveAbility, new StringName("agility"), "链式闪击伤害豁免属性应来自 typed effect。");
            _test.True(level0Damage.SavePartialOnSuccess, "链式闪击伤害 effect 应标记成功豁免减半。");
        }
        if (level7Damage != null)
        {
            _test.Eq(level7Damage.DiceCount, 8, "链式闪击 7 级伤害骰数量应来自 typed effect。");
            _test.Eq(level7Damage.DiceSides, 6, "链式闪击 7 级伤害骰面应来自 typed effect。");
        }

        CombatEffectDefinition shock = FindEffect(combat, "status", 0);
        _test.True(shock != null, "链式闪击应存在感电 status effect。");
        if (shock != null)
        {
            _test.Eq(shock.StatusId, new StringName("shocked"), "链式闪击 status effect 应保留正式状态 id。");
            _test.Eq(shock.SaveAbility, new StringName("constitution"), "链式闪击感电豁免属性应来自 typed effect。");
            _test.Eq(shock.DurationTu, 60, "链式闪击感电持续时间应来自 typed effect。");
            _test.Eq(shock.Power, 1, "链式闪击感电强度应来自 typed effect。");
        }

        CombatEffectDefinition chain = FindEffect(combat, "chain_damage", 0);
        _test.True(chain != null, "链式闪击应存在 chain_damage effect。");
        if (chain != null)
        {
            _test.Eq(chain.GetStringNameParamTyped("bonus_terrain_effect_id"), new StringName("wet"), "链式闪击连锁地形加成应来自 typed params。");
            _test.Eq(chain.GetIntParamTyped("base_chain_radius"), 1, "链式闪击基础连锁范围应来自 typed params。");
            _test.Eq(chain.GetIntParamTyped("wet_chain_radius"), 2, "链式闪击湿地连锁范围应来自 typed params。");
        }
    }

    private static SkillDefinition GetSkillDefinition(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        StringName skillId
    )
    {
        if (
            skillDefinitions == null
            || !skillDefinitions.TryGetValue(skillId, out SkillDefinition skillDefinition)
        )
        {
            return null;
        }
        return skillDefinition;
    }

    private static CombatEffectDefinition FindEffect(
        CombatSkillDefinition combat,
        StringName effectType,
        int skillLevel
    )
    {
        if (combat == null)
            return null;
        foreach (CombatEffectDefinition effect in combat.EffectDefinitions)
        {
            if (effect == null || effect.EffectType != effectType)
                continue;
            if (effect.MinSkillLevel > skillLevel)
                continue;
            if (effect.MaxSkillLevel >= 0 && effect.MaxSkillLevel < skillLevel)
                continue;
            return effect;
        }
        return null;
    }


}
