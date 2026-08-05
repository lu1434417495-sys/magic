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
        TestAimedShotDescriptionMatchesImplementedHighAccuracyContract();
        TestChargeUsesTypedRangeAndTerrainDamageContract();

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

    private void TestAimedShotDescriptionMatchesImplementedHighAccuracyContract()
    {
        SkillDefinition aimedShotDefinition = GetSkillDefinition(
            _contentSnapshot.Skills,
            "archer_aimed_shot"
        );
        _test.True(aimedShotDefinition != null, "精准射击技能应存在。");
        if (aimedShotDefinition == null)
            return;

        _test.False(
            aimedShotDefinition.Description.Contains("尚未行动")
                || aimedShotDefinition.Description.Contains("暴击"),
            "精准射击不应宣称尚未实现的先手暴击机制。"
        );

        CombatSkillDefinition combat = aimedShotDefinition.CombatProfile;
        _test.True(combat != null, "精准射击应有 combat_profile。");
        if (combat == null)
            return;

        _test.Eq(combat.RequiredWeaponFamilies.Count, 1, "精准射击应只要求一个武器家族。");
        if (combat.RequiredWeaponFamilies.Count == 1)
        {
            _test.Eq(
                combat.RequiredWeaponFamilies[0],
                new StringName("bow"),
                "精准射击应保持弓类武器门禁。"
            );
        }
        _test.True(
            combat.AllowsNaturalWeapon,
            "精准射击应显式允许天生武器作为弓之外的替代武器来源。"
        );
        _test.True(
            aimedShotDefinition.Description.Contains("天生武器"),
            "精准射击描述应明确弓与天生武器均可使用。"
        );
        _test.Eq(combat.RangeValue, 0, "精准射击应继续使用装备弓的武器射程。");
        _test.Eq(combat.ApCost, 1, "精准射击应消耗 1 AP。");
        _test.Eq(combat.StaminaCost, 20, "精准射击应消耗 20 体力。");
        _test.Eq(combat.GetEffectiveAttackRollBonus(0), 1, "精准射击 0 级攻击检定加值应为 +1。");
        _test.Eq(combat.GetEffectiveAttackRollBonus(2), 2, "精准射击 2 级攻击检定加值应为 +2。");
        _test.Eq(combat.GetEffectiveAttackRollBonus(4), 3, "精准射击 4 级攻击检定加值应为 +3。");

        AssertAimedShotDamageDice(combat, 0, 1, 4);
        AssertAimedShotDamageDice(combat, 1, 1, 6);
        AssertAimedShotDamageDice(combat, 3, 1, 8);
        AssertAimedShotDamageDice(combat, 5, 2, 4);
    }

    private void AssertAimedShotDamageDice(
        CombatSkillDefinition combat,
        int skillLevel,
        int expectedDiceCount,
        int expectedDiceSides
    )
    {
        CombatEffectDefinition damage = FindEffect(combat, "damage", skillLevel);
        _test.True(damage != null, $"精准射击 {skillLevel} 级应有伤害 effect。");
        if (damage == null)
            return;
        _test.True(damage.AddWeaponDice, $"精准射击 {skillLevel} 级应附加武器骰。");
        _test.Eq(
            damage.DiceCount,
            expectedDiceCount,
            $"精准射击 {skillLevel} 级技能骰数量应匹配。"
        );
        _test.Eq(
            damage.DiceSides,
            expectedDiceSides,
            $"精准射击 {skillLevel} 级技能骰面数应匹配。"
        );
    }

    private void TestChargeUsesTypedRangeAndTerrainDamageContract()
    {
        SkillDefinition chargeDefinition = GetSkillDefinition(
            _contentSnapshot.Skills,
            "charge"
        );
        _test.True(chargeDefinition != null, "冲锋技能应存在。");
        if (chargeDefinition == null)
            return;

        CombatSkillDefinition combat = chargeDefinition.CombatProfile;
        _test.True(combat != null, "冲锋应有 combat_profile。");
        if (combat == null)
            return;

        int[] expectedRanges = { 3, 4, 4, 5, 5, 6, 6, 7 };
        int[] expectedStaminaCosts = { 50, 50, 35, 35, 30, 30, 25, 25 };
        for (int level = 0; level < expectedRanges.Length; level++)
        {
            _test.Eq(
                combat.GetEffectiveRangeValue(level),
                expectedRanges[level],
                $"冲锋 {level} 级距离应来自 combat_profile 有效射程。"
            );
            _test.Eq(
                combat.GetEffectiveResourceCostValues(level).StaminaCost,
                expectedStaminaCosts[level],
                $"冲锋 {level} 级体力消耗应正确。"
            );
        }

        _test.Eq(combat.CastVariants.Count, 1, "冲锋应有一个直线冲锋变体。");
        if (combat.CastVariants.Count == 0)
            return;
        CombatCastVariantDefinition variant = combat.CastVariants[0];
        CombatEffectDefinition chargeEffect = null;
        bool hasDirectDamageEffect = false;
        foreach (CombatEffectDefinition effect in variant.EffectDefinitions)
        {
            if (effect?.EffectKind == BattleEffectKind.Charge)
                chargeEffect = effect;
            if (
                effect?.EffectKind == BattleEffectKind.Damage
                || effect?.EffectKind == BattleEffectKind.PathStepAoe
            )
            {
                hasDirectDamageEffect = true;
            }
        }
        _test.True(chargeEffect != null, "直线冲锋变体应包含 charge effect。");
        _test.False(hasDirectDamageEffect, "基础冲锋本身不应包含直接伤害或路径伤害 effect。");
        if (chargeEffect != null)
        {
            _test.Eq(
                chargeEffect.ChargeTrapImmunityMinSkillLevel,
                7,
                "冲锋陷阱免疫门槛应使用 typed 字段。"
            );
            foreach (
                string legacyKey in new[]
                {
                    "skill_id",
                    "base_distance",
                    "distance_by_level",
                    "trap_immunity_level",
                    "collision_base_damage",
                    "collision_size_gap_damage",
                }
            )
            {
                _test.False(
                    chargeEffect.Parameters.ContainsKey(legacyKey),
                    $"冲锋不应继续携带旧参数 {legacyKey}。"
                );
            }
        }

        string level6Description = SkillLevelDescriptionFormatter.BuildLevelDescription(
            chargeDefinition,
            6,
            new GDictionary()
        );
        string level7Description = SkillLevelDescriptionFormatter.BuildLevelDescription(
            chargeDefinition,
            7,
            new GDictionary()
        );
        _test.True(
            level6Description.Contains("6格") && !level6Description.Contains("免疫陷阱"),
            "冲锋 6 级说明应显示 6 格且尚未获得陷阱免疫。"
        );
        _test.True(
            level7Description.Contains("7格")
                && level7Description.Contains("免疫陷阱")
                && level7Description.Contains("技能本身不造成伤害"),
            "冲锋 7 级说明应匹配距离、陷阱免疫和地形伤害契约。"
        );

        SkillDefinition whirlwindDefinition = GetSkillDefinition(
            _contentSnapshot.Skills,
            "warrior_whirlwind_slash"
        );
        _test.True(whirlwindDefinition != null, "旋风斩技能应存在。");
        CombatSkillDefinition whirlwindCombat = whirlwindDefinition?.CombatProfile;
        if (whirlwindCombat == null)
            return;
        _test.Eq(whirlwindCombat.GetEffectiveRangeValue(0), 3, "旋风斩 0 级冲锋距离应为 3。");
        _test.Eq(whirlwindCombat.GetEffectiveRangeValue(1), 4, "旋风斩 1 级冲锋距离应为 4。");
        _test.Eq(whirlwindCombat.GetEffectiveRangeValue(3), 5, "旋风斩 3 级冲锋距离应为 5。");
        _test.Eq(whirlwindCombat.GetEffectiveRangeValue(5), 6, "旋风斩 5 级冲锋距离应为 6。");
        CombatEffectDefinition whirlwindCharge =
            whirlwindCombat.CastVariants.Count > 0
                ? FindVariantEffect(whirlwindCombat.CastVariants[0], BattleEffectKind.Charge)
                : null;
        _test.True(whirlwindCharge != null, "旋风斩应保留 charge effect。");
        _test.Eq(
            whirlwindCharge?.Parameters.Count ?? -1,
            0,
            "旋风斩 charge effect 不应继续携带无效碰撞伤害或距离参数。"
        );
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

    private static CombatEffectDefinition FindVariantEffect(
        CombatCastVariantDefinition variant,
        BattleEffectKind effectKind
    )
    {
        foreach (CombatEffectDefinition effect in variant?.EffectDefinitions)
        {
            if (effect?.EffectKind == effectKind)
                return effect;
        }
        return null;
    }


}
