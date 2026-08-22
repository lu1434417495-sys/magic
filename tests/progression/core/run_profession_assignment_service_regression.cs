using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class run_profession_assignment_service_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestAssignLearnedCoreSkillToProfession();
        TestPromoteMatchingLearnedSkillToCore();

        RequestTestExit(_test.Finish("Profession assignment service regression"));
    }

    private void TestAssignLearnedCoreSkillToProfession()
    {
        UnitProgress progress = MakeProgress("hero");
        SkillDefinition heavyStrike = MakeSkill("heavy_strike", "martial", maxLevel: 2);
        UnitSkillProgress skillProgress = MakeSkillProgress("heavy_strike", learned: true, isCore: true, level: 2);
        UnitProfessionProgress warriorProgress = MakeProfessionProgress("warrior", rank: 1);
        progress.SetSkillProgress(skillProgress);
        progress.SetProfessionProgress(warriorProgress);

        ProfessionAssignmentService service = MakeService(
            progress,
            new[] { heavyStrike },
            new[] { MakeProfession("warrior", "martial") }
        );

        _test.True(
            service.CanAssignCoreSkillToProfession("heavy_strike", "warrior"),
            "已学会且到有效上限的核心技能应可分配到职业。"
        );
        _test.True(
            service.AssignCoreSkillToProfession("heavy_strike", "warrior"),
            "核心技能分配应成功。"
        );
        _test.Eq(
            skillProgress.assigned_profession_id,
            new StringName("warrior"),
            "核心技能应记录 assigned_profession_id。"
        );
        _test.True(
            warriorProgress.core_skill_ids.Contains("heavy_strike"),
            "职业进度应记录核心技能。"
        );
        _test.True(
            progress.active_core_skill_ids.Contains("heavy_strike"),
            "分配后应同步 active_core_skill_ids。"
        );
        _test.True(
            service.GetProfessionCoreSkillIds("warrior").Contains(new StringName("heavy_strike")),
            "typed 职业核心技能查询应包含已分配技能。"
        );
    }

    private void TestPromoteMatchingLearnedSkillToCore()
    {
        UnitProgress progress = MakeProgress("hero");
        SkillDefinition guardBreak = MakeSkill("guard_break", "martial", maxLevel: 1);
        UnitSkillProgress skillProgress = MakeSkillProgress("guard_break", learned: true, isCore: false, level: 1);
        UnitProfessionProgress warriorProgress = MakeProfessionProgress("warrior", rank: 1);
        progress.SetSkillProgress(skillProgress);
        progress.SetProfessionProgress(warriorProgress);

        ProfessionAssignmentService service = MakeService(
            progress,
            new[] { guardBreak },
            new[] { MakeProfession("warrior", "martial") }
        );

        _test.True(
            service.CanPromoteNonCoreToCore("guard_break", "warrior"),
            "满足职业 tag 且到有效上限的非核心技能应可晋升。"
        );
        _test.True(
            service.PromoteNonCoreToCore("guard_break", "warrior"),
            "非核心技能晋升应成功。"
        );
        _test.True(skillProgress.is_core, "晋升后技能应变为核心技能。");
        _test.Eq(
            skillProgress.assigned_profession_id,
            new StringName("warrior"),
            "晋升后技能应绑定目标职业。"
        );
        _test.True(
            warriorProgress.core_skill_ids.Contains("guard_break"),
            "晋升后职业应包含该核心技能。"
        );
    }

    private static ProfessionAssignmentService MakeService(
        UnitProgress progress,
        IEnumerable<SkillDefinition> skillDefinitions,
        IEnumerable<ProfessionDefinition> professionDefs
    )
    {
        Dictionary<StringName, SkillDefinition> indexedSkillDefinitions = new();
        foreach (SkillDefinition skillDefinition in skillDefinitions)
        {
            indexedSkillDefinitions[skillDefinition.SkillId] = skillDefinition;
        }

        Dictionary<StringName, ProfessionDefinition> indexedProfessionDefs = new();
        foreach (ProfessionDefinition professionDef in professionDefs)
        {
            indexedProfessionDefs[professionDef.ProfessionId] = professionDef;
        }

        ProfessionAssignmentService service = new();
        service.Setup(
            progress,
            indexedSkillDefinitions,
            indexedProfessionDefs
        );
        return service;
    }

    private static UnitProgress MakeProgress(StringName unitId)
    {
        return new UnitProgress
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
        };
    }

    private static SkillDefinition MakeSkill(StringName skillId, StringName tag, int maxLevel) =>
        TestSkillDefinitionProjection.BuildSkill(
            skillId,
            displayName: skillId.ToString(),
            maxLevel: maxLevel,
            tags: new[] { tag }
        );

    private static UnitSkillProgress MakeSkillProgress(
        StringName skillId,
        bool learned,
        bool isCore,
        int level
    )
    {
        return new UnitSkillProgress
        {
            skill_id = skillId,
            is_learned = learned,
            is_core = isCore,
            skill_level = level,
        };
    }

    private static ProfessionDefinition MakeProfession(StringName professionId, StringName acceptedTag)
    {
        ProfessionPromotionRequirementDefinition requirement = new(
            System.Array.Empty<StringName>(),
            new[] { new TagRequirementDefinition(acceptedTag, 1, "core_max", "any", "assigned_core") },
            System.Array.Empty<ProfessionRankGateDefinition>(),
            System.Array.Empty<AttributeRequirementDefinition>(),
            System.Array.Empty<ReputationRequirementDefinition>(),
            false
        );
        return new ProfessionDefinition(
            professionId, professionId.ToString(), "Fixture profession.", 20, 8,
            "full", false, "", requirement,
            System.Array.Empty<ProfessionRankRequirementDefinition>(),
            System.Array.Empty<ProfessionGrantedSkillDefinition>(),
            System.Array.Empty<AttributeModifierDefinition>(),
            System.Array.Empty<ProfessionActiveConditionDefinition>(),
            "auto", "count_when_hidden"
        );
    }

    private static UnitProfessionProgress MakeProfessionProgress(StringName professionId, int rank)
    {
        return new UnitProfessionProgress
        {
            profession_id = professionId,
            rank = rank,
        };
    }


}
