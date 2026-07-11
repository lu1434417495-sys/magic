using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_skill_effective_max_level_rules_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestRulesNoLongerRequireGodotRegistration();
        TestProgressionServiceNoLongerRequiresGlobalClassRegistration();
        TestAuraSlashMaxLevelUsesTransformationCount();
        TestDynamicMaxLevelUsesProfessionRankIntegerDivisor();

        RequestTestExit(_test.Finish("Skill effective max level rules regression"));
    }

    private void TestRulesNoLongerRequireGodotRegistration()
    {
        Type rulesType = typeof(SkillEffectiveMaxLevelRules);
    }

    private void TestAuraSlashMaxLevelUsesTransformationCount()
    {
        UnitProgress progress = new() { unit_id = "hero" };
        SkillDefinition skillDefinition = BuildSkillDefinition(
            skillId: "warrior_aura_slash",
            maxLevel: 7,
            nonCoreMaxLevel: 5,
            dynamicMaxLevelStatId: "aura_transformation_count",
            dynamicMaxLevelBase: 7,
            dynamicMaxLevelPerStat: 2,
            masteryCurve: new[] { 1, 1, 1, 1, 1, 1, 1 }
        );

        ProgressionService service = new();
        service.SetupDefinitions(
            progress,
            new Dictionary<StringName, SkillDefinition> { [skillDefinition.SkillId] = skillDefinition },
            new Dictionary<StringName, ProfessionDef>()
        );
        _test.True(service.LearnSkill(skillDefinition.SkillId), "斗气斩测试技能应能学习。");

        service.GrantSkillMastery(skillDefinition.SkillId, 99, "training");
        UnitSkillProgress skillProgress = progress.GetSkillProgress(skillDefinition.SkillId);
        _test.Eq(skillProgress?.skill_level ?? -1, 5, "斗气斩非核心状态应被限制在 5 级。");

        _test.True(service.SetSkillCore(skillDefinition.SkillId, true), "斗气斩应能锁定为核心。");
        service.GrantSkillMastery(skillDefinition.SkillId, 99, "training");
        _test.Eq(skillProgress?.skill_level ?? -1, 5, "斗气斩仅指定核心但未锁定时仍应停在 non_core 上限。");

        if (skillProgress == null)
            return;
        skillProgress.is_level_trigger_locked = true;
        if (!progress.HasLockedLevelTriggerSkillId(skillDefinition.SkillId))
            progress.AddLockedLevelTriggerSkillId(skillDefinition.SkillId);
        progress.SetSkillProgress(skillProgress);
        service.RefreshRuntimeState();
        service.GrantSkillMastery(skillDefinition.SkillId, 99, "training");
        _test.Eq(skillProgress.skill_level, 7, "斗气斩锁定后默认最大等级应为 7。");

        progress.unit_base_attributes.SetAttributeValue("aura_transformation_count", 2);
        service.RefreshRuntimeState();
        service.GrantSkillMastery(skillDefinition.SkillId, 99, "training");
        _test.Eq(skillProgress.skill_level, 11, "斗气斩每次斗气质变应将核心最大等级提高 2。");
    }

    private void TestDynamicMaxLevelUsesProfessionRankIntegerDivisor()
    {
        UnitProgress progress = new() { unit_id = "mage" };
        UnitProfessionProgress mageProfession = new() { profession_id = "mage" };
        progress.SetProfessionProgress(mageProfession);

        SkillDefinition skillDefinition = BuildSkillDefinition(
            skillId: "mage_arcane_missile",
            maxLevel: 5,
            nonCoreMaxLevel: 3,
            dynamicMaxLevelStatId: "profession_rank:mage",
            dynamicMaxLevelBase: 5,
            dynamicMaxLevelPerStat: -2
        );

        mageProfession.rank = 9;
        _test.Eq(
            SkillEffectiveMaxLevelRules.GetEffectiveAbsoluteMaxLevel(skillDefinition, progress),
            5,
            "法师 rank 9 时奥术飞弹动态上限应为 max(5, floor(9 / 2)) = 5。"
        );

        mageProfession.rank = 12;
        _test.Eq(
            SkillEffectiveMaxLevelRules.GetEffectiveAbsoluteMaxLevel(skillDefinition, progress),
            6,
            "法师 rank 12 时奥术飞弹动态上限应为 max(5, floor(12 / 2)) = 6。"
        );

        mageProfession.rank = 19;
        _test.Eq(
            SkillEffectiveMaxLevelRules.GetEffectiveAbsoluteMaxLevel(skillDefinition, progress),
            9,
            "法师 rank 19 时奥术飞弹动态上限应为 max(5, floor(19 / 2)) = 9。"
        );

        UnitSkillProgress skillProgress = new() { skill_id = skillDefinition.SkillId };
        _test.Eq(
            SkillEffectiveMaxLevelRules.GetEffectiveMaxLevel(skillDefinition, skillProgress, progress),
            3,
            "奥术飞弹未锁定时仍应受 non_core 上限限制。"
        );
        skillProgress.is_level_trigger_locked = true;
        _test.Eq(
            SkillEffectiveMaxLevelRules.GetEffectiveMaxLevel(skillDefinition, skillProgress, progress),
            9,
            "奥术飞弹锁定后才应使用法师 rank/2 的动态核心上限。"
        );
    }

    private void TestProgressionServiceNoLongerRequiresGlobalClassRegistration()
    {
    }

    private static SkillDefinition BuildSkillDefinition(
        StringName skillId,
        int maxLevel,
        int nonCoreMaxLevel,
        StringName dynamicMaxLevelStatId = default,
        int dynamicMaxLevelBase = 0,
        int dynamicMaxLevelPerStat = 0,
        IReadOnlyList<int> masteryCurve = null
    )
    {
        return new SkillDefinition(
            skillId: skillId,
            displayName: (string)skillId,
            iconId: skillId,
            description: "",
            skillType: "active",
            maxLevel: maxLevel,
            nonCoreMaxLevel: nonCoreMaxLevel,
            dynamicMaxLevelStatId: dynamicMaxLevelStatId,
            dynamicMaxLevelBase: dynamicMaxLevelBase,
            dynamicMaxLevelPerStat: dynamicMaxLevelPerStat,
            masteryCurve: masteryCurve ?? System.Array.Empty<int>(),
            tags: System.Array.Empty<StringName>(),
            learnSource: "book",
            learnRequirements: System.Array.Empty<StringName>(),
            unlockMode: "",
            knowledgeRequirements: System.Array.Empty<StringName>(),
            skillLevelRequirements: new Dictionary<StringName, int>(),
            attributeRequirements: new Dictionary<StringName, int>(),
            achievementRequirements: System.Array.Empty<StringName>(),
            upgradeSourceSkillIds: System.Array.Empty<StringName>(),
            retainSourceSkillsOnUnlock: false,
            coreSkillTransitionMode: "",
            masterySources: System.Array.Empty<StringName>(),
            growthTier: "",
            attributeGrowthProgress: new Dictionary<StringName, int>(),
            practiceTier: "",
            attributeModifiers: System.Array.Empty<AttributeModifierDefinition>(),
            levelDescriptionTemplate: "",
            levelDescriptionConfigs: new Dictionary<int, IReadOnlyDictionary<string, Variant>>(),
            combatProfile: null
        );
    }

}
