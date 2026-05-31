using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_profession_assignment_service_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestAssignLearnedCoreSkillToProfession();
        TestPromoteMatchingLearnedSkillToCore();

        if (_failures.Count == 0)
        {
            GD.Print("Profession assignment service regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Profession assignment service regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestAssignLearnedCoreSkillToProfession()
    {
        UnitProgress progress = MakeProgress("hero");
        SkillDef heavyStrike = MakeSkill("heavy_strike", "martial", maxLevel: 2);
        UnitSkillProgress skillProgress = MakeSkillProgress("heavy_strike", learned: true, isCore: true, level: 2);
        UnitProfessionProgress warriorProgress = MakeProfessionProgress("warrior", rank: 1);
        progress.set_skill_progress(skillProgress);
        progress.set_profession_progress(warriorProgress);

        ProfessionAssignmentService service = MakeService(
            progress,
            new[] { heavyStrike },
            new[] { MakeProfession("warrior", "martial") }
        );

        AssertTrue(
            service.can_assign_core_skill_to_profession("heavy_strike", "warrior"),
            "已学会且到有效上限的核心技能应可分配到职业。"
        );
        AssertTrue(
            service.assign_core_skill_to_profession("heavy_strike", "warrior"),
            "核心技能分配应成功。"
        );
        AssertEq(
            skillProgress.assigned_profession_id,
            new StringName("warrior"),
            "核心技能应记录 assigned_profession_id。"
        );
        AssertTrue(
            warriorProgress.core_skill_ids.Contains("heavy_strike"),
            "职业进度应记录核心技能。"
        );
        AssertTrue(
            progress.active_core_skill_ids.Contains("heavy_strike"),
            "分配后应同步 active_core_skill_ids。"
        );
    }

    private void TestPromoteMatchingLearnedSkillToCore()
    {
        UnitProgress progress = MakeProgress("hero");
        SkillDef guardBreak = MakeSkill("guard_break", "martial", maxLevel: 1);
        UnitSkillProgress skillProgress = MakeSkillProgress("guard_break", learned: true, isCore: false, level: 1);
        UnitProfessionProgress warriorProgress = MakeProfessionProgress("warrior", rank: 1);
        progress.set_skill_progress(skillProgress);
        progress.set_profession_progress(warriorProgress);

        ProfessionAssignmentService service = MakeService(
            progress,
            new[] { guardBreak },
            new[] { MakeProfession("warrior", "martial") }
        );

        AssertTrue(
            service.can_promote_non_core_to_core("guard_break", "warrior"),
            "满足职业 tag 且到有效上限的非核心技能应可晋升。"
        );
        AssertTrue(
            service.promote_non_core_to_core("guard_break", "warrior"),
            "非核心技能晋升应成功。"
        );
        AssertTrue(skillProgress.is_core, "晋升后技能应变为核心技能。");
        AssertEq(
            skillProgress.assigned_profession_id,
            new StringName("warrior"),
            "晋升后技能应绑定目标职业。"
        );
        AssertTrue(
            warriorProgress.core_skill_ids.Contains("guard_break"),
            "晋升后职业应包含该核心技能。"
        );
    }

    private static ProfessionAssignmentService MakeService(
        UnitProgress progress,
        IEnumerable<SkillDef> skillDefs,
        IEnumerable<ProfessionDef> professionDefs
    )
    {
        GDictionary indexedSkillDefs = new();
        foreach (SkillDef skillDef in skillDefs)
        {
            indexedSkillDefs[skillDef.skill_id] = skillDef;
        }

        GDictionary indexedProfessionDefs = new();
        foreach (ProfessionDef professionDef in professionDefs)
        {
            indexedProfessionDefs[professionDef.profession_id] = professionDef;
        }

        ProfessionAssignmentService service = new();
        service.setup(progress, indexedSkillDefs, indexedProfessionDefs);
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

    private static SkillDef MakeSkill(StringName skillId, StringName tag, int maxLevel)
    {
        return new SkillDef
        {
            skill_id = skillId,
            display_name = skillId.ToString(),
            max_level = maxLevel,
            tags = new Godot.Collections.Array<StringName> { tag },
        };
    }

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

    private static ProfessionDef MakeProfession(StringName professionId, StringName acceptedTag)
    {
        return new ProfessionDef
        {
            profession_id = professionId,
            display_name = professionId.ToString(),
            max_rank = 20,
            unlock_requirement = new ProfessionPromotionRequirement
            {
                required_tag_rules = new Godot.Collections.Array<TagRequirement>
                {
                    new() { tag = acceptedTag },
                },
            },
        };
    }

    private static UnitProfessionProgress MakeProfessionProgress(StringName professionId, int rank)
    {
        return new UnitProfessionProgress
        {
            profession_id = professionId,
            rank = rank,
        };
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
        {
            _failures.Add($"{message} | actual={actual} expected={expected}");
        }
    }
}
