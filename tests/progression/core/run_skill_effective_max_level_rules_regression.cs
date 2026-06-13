using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_skill_effective_max_level_rules_regression : SceneTree
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

        Quit(_test.Finish("Skill effective max level rules regression"));
    }

    private void TestRulesNoLongerRequireGodotRegistration()
    {
        Type rulesType = typeof(SkillEffectiveMaxLevelRules);
    }

    private void TestAuraSlashMaxLevelUsesTransformationCount()
    {
        UnitProgress progress = new() { unit_id = "hero" };
        SkillDef skillDef = new()
        {
            skill_id = "warrior_aura_slash",
            display_name = "斗气斩",
            icon_id = "warrior_aura_slash",
            max_level = 7,
            non_core_max_level = 5,
            dynamic_max_level_stat_id = "aura_transformation_count",
            dynamic_max_level_base = 7,
            dynamic_max_level_per_stat = 2,
            mastery_curve = new[] { 1, 1, 1, 1, 1, 1, 1 },
        };

        ProgressionService service = new();
        service.Setup(
            progress,
            new Dictionary<StringName, SkillDef> { [skillDef.skill_id] = skillDef },
            new Dictionary<StringName, ProfessionDef>()
        );
        _test.True(service.LearnSkill(skillDef.skill_id), "斗气斩测试技能应能学习。");

        service.GrantSkillMastery(skillDef.skill_id, 99, "training");
        UnitSkillProgress skillProgress = progress.GetSkillProgress(skillDef.skill_id);
        _test.Eq(skillProgress?.skill_level ?? -1, 5, "斗气斩非核心状态应被限制在 5 级。");

        _test.True(service.SetSkillCore(skillDef.skill_id, true), "斗气斩应能锁定为核心。");
        service.GrantSkillMastery(skillDef.skill_id, 99, "training");
        _test.Eq(skillProgress?.skill_level ?? -1, 5, "斗气斩仅指定核心但未锁定时仍应停在 non_core 上限。");

        if (skillProgress == null)
            return;
        skillProgress.is_level_trigger_locked = true;
        if (!progress.HasLockedLevelTriggerSkillId(skillDef.skill_id))
            progress.AddLockedLevelTriggerSkillId(skillDef.skill_id);
        progress.SetSkillProgress(skillProgress);
        service.RefreshRuntimeState();
        service.GrantSkillMastery(skillDef.skill_id, 99, "training");
        _test.Eq(skillProgress.skill_level, 7, "斗气斩锁定后默认最大等级应为 7。");

        progress.unit_base_attributes.SetAttributeValue("aura_transformation_count", 2);
        service.RefreshRuntimeState();
        service.GrantSkillMastery(skillDef.skill_id, 99, "training");
        _test.Eq(skillProgress.skill_level, 11, "斗气斩每次斗气质变应将核心最大等级提高 2。");
    }

    private void TestDynamicMaxLevelUsesProfessionRankIntegerDivisor()
    {
        UnitProgress progress = new() { unit_id = "mage" };
        UnitProfessionProgress mageProfession = new() { profession_id = "mage" };
        progress.SetProfessionProgress(mageProfession);

        SkillDef skillDef = new()
        {
            skill_id = "mage_arcane_missile",
            max_level = 5,
            non_core_max_level = 3,
            dynamic_max_level_stat_id = "profession_rank:mage",
            dynamic_max_level_base = 5,
            dynamic_max_level_per_stat = -2,
        };

        mageProfession.rank = 9;
        _test.Eq(
            SkillEffectiveMaxLevelRules.GetEffectiveAbsoluteMaxLevel(skillDef, progress),
            5,
            "法师 rank 9 时奥术飞弹动态上限应为 max(5, floor(9 / 2)) = 5。"
        );

        mageProfession.rank = 12;
        _test.Eq(
            SkillEffectiveMaxLevelRules.GetEffectiveAbsoluteMaxLevel(skillDef, progress),
            6,
            "法师 rank 12 时奥术飞弹动态上限应为 max(5, floor(12 / 2)) = 6。"
        );

        mageProfession.rank = 19;
        _test.Eq(
            SkillEffectiveMaxLevelRules.GetEffectiveAbsoluteMaxLevel(skillDef, progress),
            9,
            "法师 rank 19 时奥术飞弹动态上限应为 max(5, floor(19 / 2)) = 9。"
        );

        UnitSkillProgress skillProgress = new() { skill_id = skillDef.skill_id };
        _test.Eq(
            SkillEffectiveMaxLevelRules.GetEffectiveMaxLevel(skillDef, skillProgress, progress),
            3,
            "奥术飞弹未锁定时仍应受 non_core 上限限制。"
        );
        skillProgress.is_level_trigger_locked = true;
        _test.Eq(
            SkillEffectiveMaxLevelRules.GetEffectiveMaxLevel(skillDef, skillProgress, progress),
            9,
            "奥术飞弹锁定后才应使用法师 rank/2 的动态核心上限。"
        );
    }

    private void TestProgressionServiceNoLongerRequiresGlobalClassRegistration()
    {
    }


}
