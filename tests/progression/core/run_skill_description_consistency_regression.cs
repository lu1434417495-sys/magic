using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_skill_description_consistency_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestChainLightningDescriptionInputsMatchSaveEnabledEffects();

        Quit(_test.Finish("Skill description consistency regression"));
    }

    private void TestChainLightningDescriptionInputsMatchSaveEnabledEffects()
    {
        ProgressionContentRegistry registry = new();
        SkillDef chainLightning = GetSkillDef(
            registry.GetSkillDefsTyped(),
            "mage_chain_lightning"
        );
        _test.True(chainLightning != null, "链式闪击技能应存在。");
        if (chainLightning == null)
        {
            return;
        }

        string level0Description = SkillLevelDescriptionFormatter.BuildLevelDescription(
            chainLightning,
            0,
            new GDictionary()
        );
        string level7Description = SkillLevelDescriptionFormatter.BuildLevelDescription(
            chainLightning,
            7,
            new GDictionary()
        );
        _test.True(!string.IsNullOrWhiteSpace(level0Description), "链式闪击 0 级描述应能从 typed 数据生成。");
        _test.True(!string.IsNullOrWhiteSpace(level7Description), "链式闪击 7 级描述应能从 typed 数据生成。");

        CombatSkillDef combat = chainLightning.combat_profile;
        _test.True(combat != null, "链式闪击应有 combat_profile。");
        if (combat == null)
            return;

        _test.Eq(combat.range_value, 5, "链式闪击描述输入应保留射程。");
        _test.Eq(combat.ap_cost, 1, "链式闪击描述输入应保留 AP 消耗。");
        _test.Eq(combat.mp_cost, 120, "链式闪击描述输入应保留 MP 消耗。");
        _test.Eq(combat.cooldown_tu, 60, "链式闪击描述输入应保留冷却。");

        CombatEffectDef level0Damage = FindEffect(combat, "damage", 0);
        CombatEffectDef level7Damage = FindEffect(combat, "damage", 7);
        _test.True(level0Damage != null, "链式闪击应存在 0 级伤害 effect。");
        _test.True(level7Damage != null, "链式闪击应存在 7 级伤害 effect。");
        if (level0Damage != null)
        {
            _test.Eq(level0Damage.dice_count, 4, "链式闪击 0 级伤害骰数量应来自 typed effect。");
            _test.Eq(level0Damage.dice_sides, 6, "链式闪击 0 级伤害骰面应来自 typed effect。");
            _test.Eq(level0Damage.save_ability, new StringName("agility"), "链式闪击伤害豁免属性应来自 typed effect。");
            _test.True(level0Damage.save_partial_on_success, "链式闪击伤害 effect 应标记成功豁免减半。");
        }
        if (level7Damage != null)
        {
            _test.Eq(level7Damage.dice_count, 8, "链式闪击 7 级伤害骰数量应来自 typed effect。");
            _test.Eq(level7Damage.dice_sides, 6, "链式闪击 7 级伤害骰面应来自 typed effect。");
        }

        CombatEffectDef shock = FindEffect(combat, "status", 0);
        _test.True(shock != null, "链式闪击应存在感电 status effect。");
        if (shock != null)
        {
            _test.Eq(shock.status_id, new StringName("shocked"), "链式闪击 status effect 应保留正式状态 id。");
            _test.Eq(shock.save_ability, new StringName("constitution"), "链式闪击感电豁免属性应来自 typed effect。");
            _test.Eq(shock.duration_tu, 60, "链式闪击感电持续时间应来自 typed effect。");
            _test.Eq(shock.power, 1, "链式闪击感电强度应来自 typed effect。");
        }

        CombatEffectDef chain = FindEffect(combat, "chain_damage", 0);
        _test.True(chain != null, "链式闪击应存在 chain_damage effect。");
        if (chain != null)
        {
            _test.Eq(chain.GetStringNameParamTyped("bonus_terrain_effect_id"), new StringName("wet"), "链式闪击连锁地形加成应来自 typed params。");
            _test.Eq(chain.GetIntParamTyped("base_chain_radius"), 1, "链式闪击基础连锁范围应来自 typed params。");
            _test.Eq(chain.GetIntParamTyped("wet_chain_radius"), 2, "链式闪击湿地连锁范围应来自 typed params。");
        }
    }

    private static SkillDef GetSkillDef(
        IReadOnlyDictionary<StringName, SkillDef> skillDefs,
        StringName skillId
    )
    {
        if (skillDefs == null || !skillDefs.TryGetValue(skillId, out SkillDef skillDef))
        {
            return null;
        }
        return skillDef;
    }

    private static CombatEffectDef FindEffect(CombatSkillDef combat, StringName effectType, int skillLevel)
    {
        if (combat == null)
            return null;
        foreach (CombatEffectDef effect in combat.effect_defs)
        {
            if (effect == null || effect.effect_type != effectType)
                continue;
            if (effect.min_skill_level > skillLevel)
                continue;
            if (effect.max_skill_level >= 0 && effect.max_skill_level < skillLevel)
                continue;
            return effect;
        }
        return null;
    }


}
