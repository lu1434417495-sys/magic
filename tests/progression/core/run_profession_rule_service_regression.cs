using System;
using System.Collections.Generic;
using Godot;

public partial class run_profession_rule_service_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestServiceNoLongerRequiresGodotRegistration();
        TestEligibleSkillIdsUseTypedSetupAndPreviewAssignments();
        TestRefreshAllProfessionStatesUsesTypedDefIndex();

        RequestTestExit(_test.Finish("Profession rule service regression"));
    }

    private void TestServiceNoLongerRequiresGodotRegistration()
    {
        Type serviceType = typeof(ProfessionRuleService);
    }

    private void TestEligibleSkillIdsUseTypedSetupAndPreviewAssignments()
    {
        UnitProgress progress = MakeProgress("hero");
        SkillDefinition heavyStrike = MakeSkill("heavy_strike", "martial", maxLevel: 2);
        SkillDefinition lowLevelStrike = MakeSkill("low_level_strike", "martial", maxLevel: 2);
        SkillDefinition arcaneBolt = MakeSkill("arcane_bolt", "arcane", maxLevel: 2);
        SkillDefinition claimedStrike = MakeSkill("claimed_strike", "martial", maxLevel: 2);

        progress.SetSkillProgress(MakeSkillProgress("heavy_strike", level: 2));
        progress.SetSkillProgress(MakeSkillProgress("low_level_strike", level: 1));
        progress.SetSkillProgress(MakeSkillProgress("arcane_bolt", level: 2));
        UnitSkillProgress claimedProgress = MakeSkillProgress("claimed_strike", level: 2);
        claimedProgress.assigned_profession_id = "rogue";
        progress.SetSkillProgress(claimedProgress);

        ProfessionRuleService service = MakeService(
            progress,
            new[] { heavyStrike, lowLevelStrike, arcaneBolt, claimedStrike },
            new[] { MakeProfession("warrior") }
        );

        TagRequirement martialCoreMax = new() { tag = "martial" };
        IReadOnlyList<StringName> eligibleSkillIds = service.GetEligibleSkillIds(
            "warrior",
            new[] { martialCoreMax },
            allowUnassigned: true
        );

        _test.True(
            ContainsSkillId(eligibleSkillIds, "heavy_strike"),
            "typed eligible skill 列表应包含符合 tag、核心且已达有效上限的技能。"
        );
        _test.False(
            ContainsSkillId(eligibleSkillIds, "low_level_strike"),
            "未达有效上限的核心技能不应满足默认 core_max tag rule。"
        );
        _test.False(
            ContainsSkillId(eligibleSkillIds, "arcane_bolt"),
            "不同 tag 的技能不应进入 martial 候选列表。"
        );
        _test.False(
            ContainsSkillId(eligibleSkillIds, "claimed_strike"),
            "已分配给其他职业的技能不应进入目标职业候选列表。"
        );

        _test.True(
            service.SkillMatchesTagRequirement(
                "heavy_strike",
                "warrior",
                martialCoreMax,
                allowUnassigned: false,
                previewAssignedSkillIds: new[] { new StringName("heavy_strike") }
            ),
            "previewAssignedSkillIds 应允许未分配技能参与 rank-up 预览匹配。"
        );
    }

    private void TestRefreshAllProfessionStatesUsesTypedDefIndex()
    {
        UnitProgress progress = MakeProgress("hero");
        progress.unit_base_attributes.SetAttributeValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Strength), 8);
        UnitProfessionProgress professionProgress = new()
        {
            profession_id = "warrior",
            rank = 1,
            is_active = true,
        };
        progress.SetProfessionProgress(professionProgress);

        ProfessionDef warrior = MakeProfession("warrior");
        warrior.active_conditions = new Godot.Collections.Array<ProfessionActiveCondition>
        {
            new()
            {
                condition_type = "attribute_range",
                attribute_id = UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Strength),
                min_value = 10,
            },
        };

        ProfessionRuleService service = MakeService(
            progress,
            Array.Empty<SkillDefinition>(),
            new[] { warrior }
        );

        service.RefreshAllProfessionStates();
        _test.False(
            professionProgress.is_active,
            "不满足 active condition 时职业应被刷新为 inactive。"
        );
        _test.True(
            professionProgress.is_hidden,
            "不满足 active condition 时职业应隐藏。"
        );
        _test.Eq(
            professionProgress.inactive_reason,
            new StringName("active_conditions_not_met"),
            "不满足条件的 inactive reason 应稳定。"
        );

        progress.unit_base_attributes.SetAttributeValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Strength), 12);
        service.RefreshAllProfessionStates();
        _test.True(
            professionProgress.is_active,
            "满足 active condition 后 auto reactivation 职业应恢复 active。"
        );
        _test.False(professionProgress.is_hidden, "恢复 active 后职业不应继续隐藏。");
        _test.Eq(professionProgress.inactive_reason, new StringName(""), "恢复 active 后 reason 应清空。");
    }

    private static ProfessionRuleService MakeService(
        UnitProgress progress,
        IEnumerable<SkillDefinition> skillDefinitions,
        IEnumerable<ProfessionDef> professionDefs
    )
    {
        Dictionary<StringName, SkillDefinition> indexedSkillDefinitions = new();
        foreach (SkillDefinition skillDefinition in skillDefinitions)
            indexedSkillDefinitions[skillDefinition.SkillId] = skillDefinition;

        Dictionary<StringName, ProfessionDef> indexedProfessionDefs = new();
        foreach (ProfessionDef professionDef in professionDefs)
            indexedProfessionDefs[professionDef.profession_id] = professionDef;

        ProfessionRuleService service = new();
        service.Setup(progress, indexedSkillDefinitions, indexedProfessionDefs);
        return service;
    }

    private static UnitProgress MakeProgress(StringName unitId) =>
        new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            unit_base_attributes = new UnitBaseAttributes(),
        };

    private static SkillDefinition MakeSkill(StringName skillId, StringName tag, int maxLevel) =>
        TestSkillDefinitionProjection.BuildSkill(
            skillId,
            displayName: skillId.ToString(),
            maxLevel: maxLevel,
            tags: new[] { tag }
        );

    private static UnitSkillProgress MakeSkillProgress(StringName skillId, int level) =>
        new()
        {
            skill_id = skillId,
            is_learned = true,
            is_core = true,
            skill_level = level,
        };

    private static ProfessionDef MakeProfession(StringName professionId) =>
        new()
        {
            profession_id = professionId,
            display_name = professionId.ToString(),
            max_rank = 20,
            reactivation_mode = "auto",
        };

    private static bool ContainsSkillId(IEnumerable<StringName> skillIds, StringName targetSkillId)
    {
        foreach (StringName skillId in skillIds)
        {
            if (skillId == targetSkillId)
                return true;
        }
        return false;
    }


}
