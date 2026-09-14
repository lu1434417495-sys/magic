using System;
using System.Collections.Generic;
using Godot;

public partial class run_weapon_training_promotion_policy_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try { TestWeaponTrainingCannotBecomeProfessionTrigger(); }
        catch (Exception error) { _test.Fail($"Unhandled exception: {error}"); }
        RequestTestExit(_test.Finish("Weapon training promotion policy regression"));
    }

    private void TestWeaponTrainingCannotBecomeProfessionTrigger()
    {
        StringName skillId = "test_sword_training";
        StringName ordinaryId = "test_ordinary_growth";
        StringName professionId = "weapon_training_profession";
        SkillDefinition weaponTraining = TestSkillDefinitionProjection.BuildSkill(
            skillId, maxLevel: 1,
            tags: new StringName[] { "weapon_training", "test_mastery" }
        );
        SkillDefinition ordinary = TestSkillDefinitionProjection.BuildSkill(
            ordinaryId, maxLevel: 1, tags: new StringName[] { "test_mastery" }
        );
        var skillDefinitions = new Dictionary<StringName, SkillDefinition>
        {
            [skillId] = weaponTraining, [ordinaryId] = ordinary,
        };
        var rankRequirement = new ProfessionRankRequirementDefinition(
            2,
            new[] { new TagRequirementDefinition("test_mastery", 1, "core_qualified", "any", "assigned_core") },
            Array.Empty<ProfessionRankGateDefinition>(),
            Array.Empty<AttributeRequirementDefinition>(),
            Array.Empty<ReputationRequirementDefinition>()
        );
        var profession = new ProfessionDefinition(
            professionId, "Weapon Training Profession", "Regression-only profession.",
            2, 6, "full", true, "", null, new[] { rankRequirement },
            Array.Empty<ProfessionGrantedSkillDefinition>(),
            Array.Empty<AttributeModifierDefinition>(),
            Array.Empty<ProfessionActiveConditionDefinition>(), "auto", "count_when_hidden"
        );
        var progress = new UnitProgress { unit_id = "weapon_training_member" };
        PromotionHistoryTestFixture.Record(progress, "prior_growth", professionId);
        progress.SetSkillProgress(new UnitSkillProgress
        {
            skill_id = skillId, is_learned = true, skill_level = 1,
        });
        var service = new ProgressionService();
        service.SetupDefinitions(progress, skillDefinitions,
            new Dictionary<StringName, ProfessionDefinition> { [professionId] = profession });
        _test.True(service.SetSkillCore(skillId, true), "武器训练仍可设为核心。");
        UnitSkillProgress trainingProgress = progress.GetSkillProgress(skillId);
        _test.True(PromotionEligibilityRules.HasReachedMilestone(weaponTraining, trainingProgress, progress),
            "负向控制：武器训练已经满足成长里程碑。");
        _test.True(PromotionEligibilityRules.HasCoreQualification(weaponTraining, trainingProgress, progress),
            "武器训练仍可作为核心资格。");
        _test.False(PromotionEligibilityRules.IsReadyTrigger(weaponTraining, trainingProgress, progress),
            "武器训练不得作为晋升触发技能。");
        _test.False(service.CanPromoteProfession(professionId), "武器训练不得生成晋升候选。");
        int rankBefore = progress.GetProfessionProgress(professionId).rank;
        int historyBefore = progress.GetProfessionProgress(professionId).promotion_history.Count;
        int hpBefore = progress.unit_base_attributes.GetAttributeValue("hp_max");
        PreparedPromotion rejected = service.PreparePromotion(professionId,
            new PromotionCommitRequest(skillId, 2, new[] { skillId }, Array.Empty<StringName>()));
        _test.Eq(rejected.Failure, PromotionFailureKind.NotEligible, "完整提交也必须拒绝武器训练触发。");
        _test.True(rejected.Candidate == null, "拒绝不得发布晋升副本。");
        _test.Eq(progress.GetProfessionProgress(professionId).rank, rankBefore, "拒绝不得改变职业等级。");
        _test.Eq(progress.GetProfessionProgress(professionId).promotion_history.Count, historyBefore, "拒绝不得追加历史。");
        _test.Eq(progress.unit_base_attributes.GetAttributeValue("hp_max"), hpBefore, "拒绝不得改变生命上限。");
        _test.False(progress.HasUsedGrowthTrigger(skillId), "拒绝不得消耗成长触发技能。");

        progress.SetSkillProgress(new UnitSkillProgress
        {
            skill_id = ordinaryId, is_learned = true, skill_level = 1,
        });
        _test.True(PromotionEligibilityRules.IsReadyTrigger(ordinary, progress.GetSkillProgress(ordinaryId), progress),
            "正向控制：同里程碑的普通技能仍可触发晋升。");
        _test.True(service.CanPromoteProfession(professionId), "普通技能应生成晋升候选。");
        PreparedPromotion accepted = service.PreparePromotion(professionId,
            new PromotionCommitRequest(ordinaryId, 2, new[] { ordinaryId }, Array.Empty<StringName>()));
        _test.True(accepted.Ok, $"普通技能完整提交应可准备：{accepted.Failure}");
        _test.Eq(progress.GetProfessionProgress(professionId).rank, rankBefore, "准备普通晋升仍不得提前修改原状态。");
    }
}
