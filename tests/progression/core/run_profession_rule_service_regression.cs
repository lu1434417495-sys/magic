using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

public partial class run_profession_rule_service_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestServiceNoLongerRequiresGodotRegistration();
        TestEligibleSkillIdsUseTypedSetupAndPreviewAssignments();
        TestRefreshAllProfessionStatesUsesTypedDefIndex();

        if (_failures.Count == 0)
        {
            GD.Print("Profession rule service regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
            GD.PushError(failure);
        GD.Print($"Profession rule service regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestServiceNoLongerRequiresGodotRegistration()
    {
        Type serviceType = typeof(ProfessionRuleService);
        AssertFalse(
            typeof(GodotObject).IsAssignableFrom(serviceType),
            "ProfessionRuleService 应是普通 C# service，不应继承 GodotObject/RefCounted。"
        );
        AssertFalse(
            serviceType.GetCustomAttributes(typeof(GlobalClassAttribute), inherit: false).Length
                > 0,
            "ProfessionRuleService 不应继续注册为 Godot GlobalClass。"
        );
    }

    private void TestEligibleSkillIdsUseTypedSetupAndPreviewAssignments()
    {
        UnitProgress progress = MakeProgress("hero");
        SkillDef heavyStrike = MakeSkill("heavy_strike", "martial", maxLevel: 2);
        SkillDef lowLevelStrike = MakeSkill("low_level_strike", "martial", maxLevel: 2);
        SkillDef arcaneBolt = MakeSkill("arcane_bolt", "arcane", maxLevel: 2);
        SkillDef claimedStrike = MakeSkill("claimed_strike", "martial", maxLevel: 2);

        progress.set_skill_progress(MakeSkillProgress("heavy_strike", level: 2));
        progress.set_skill_progress(MakeSkillProgress("low_level_strike", level: 1));
        progress.set_skill_progress(MakeSkillProgress("arcane_bolt", level: 2));
        UnitSkillProgress claimedProgress = MakeSkillProgress("claimed_strike", level: 2);
        claimedProgress.assigned_profession_id = "rogue";
        progress.set_skill_progress(claimedProgress);

        ProfessionRuleService service = MakeService(
            progress,
            new[] { heavyStrike, lowLevelStrike, arcaneBolt, claimedStrike },
            new[] { MakeProfession("warrior") }
        );

        TagRequirement martialCoreMax = new() { tag = "martial" };
        IReadOnlyList<StringName> eligibleSkillIds = service.get_eligible_skill_ids(
            "warrior",
            new[] { martialCoreMax },
            allowUnassigned: true
        );

        AssertTrue(
            ContainsSkillId(eligibleSkillIds, "heavy_strike"),
            "typed eligible skill 列表应包含符合 tag、核心且已达有效上限的技能。"
        );
        AssertFalse(
            ContainsSkillId(eligibleSkillIds, "low_level_strike"),
            "未达有效上限的核心技能不应满足默认 core_max tag rule。"
        );
        AssertFalse(
            ContainsSkillId(eligibleSkillIds, "arcane_bolt"),
            "不同 tag 的技能不应进入 martial 候选列表。"
        );
        AssertFalse(
            ContainsSkillId(eligibleSkillIds, "claimed_strike"),
            "已分配给其他职业的技能不应进入目标职业候选列表。"
        );

        AssertTrue(
            service.skill_matches_tag_requirement(
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
        progress.unit_base_attributes.set_attribute_value(UnitBaseAttributes.STRENGTH(), 8);
        UnitProfessionProgress professionProgress = new()
        {
            profession_id = "warrior",
            rank = 1,
            is_active = true,
        };
        progress.set_profession_progress(professionProgress);

        ProfessionDef warrior = MakeProfession("warrior");
        warrior.active_conditions = new Godot.Collections.Array<ProfessionActiveCondition>
        {
            new()
            {
                condition_type = "attribute_range",
                attribute_id = UnitBaseAttributes.STRENGTH(),
                min_value = 10,
            },
        };

        ProfessionRuleService service = MakeService(
            progress,
            Array.Empty<SkillDef>(),
            new[] { warrior }
        );

        service.refresh_all_profession_states();
        AssertFalse(
            professionProgress.is_active,
            "不满足 active condition 时职业应被刷新为 inactive。"
        );
        AssertTrue(
            professionProgress.is_hidden,
            "不满足 active condition 时职业应隐藏。"
        );
        AssertEq(
            professionProgress.inactive_reason,
            new StringName("active_conditions_not_met"),
            "不满足条件的 inactive reason 应稳定。"
        );

        progress.unit_base_attributes.set_attribute_value(UnitBaseAttributes.STRENGTH(), 12);
        service.refresh_all_profession_states();
        AssertTrue(
            professionProgress.is_active,
            "满足 active condition 后 auto reactivation 职业应恢复 active。"
        );
        AssertFalse(professionProgress.is_hidden, "恢复 active 后职业不应继续隐藏。");
        AssertEq(professionProgress.inactive_reason, new StringName(""), "恢复 active 后 reason 应清空。");
    }

    private static ProfessionRuleService MakeService(
        UnitProgress progress,
        IEnumerable<SkillDef> skillDefs,
        IEnumerable<ProfessionDef> professionDefs
    )
    {
        Dictionary<StringName, SkillDef> indexedSkillDefs = new();
        foreach (SkillDef skillDef in skillDefs)
            indexedSkillDefs[skillDef.skill_id] = skillDef;

        Dictionary<StringName, ProfessionDef> indexedProfessionDefs = new();
        foreach (ProfessionDef professionDef in professionDefs)
            indexedProfessionDefs[professionDef.profession_id] = professionDef;

        ProfessionRuleService service = new();
        service.setup(progress, indexedSkillDefs, indexedProfessionDefs);
        return service;
    }

    private static UnitProgress MakeProgress(StringName unitId) =>
        new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            unit_base_attributes = new UnitBaseAttributes(),
        };

    private static SkillDef MakeSkill(StringName skillId, StringName tag, int maxLevel) =>
        new()
        {
            skill_id = skillId,
            display_name = skillId.ToString(),
            max_level = maxLevel,
            tags = new Godot.Collections.Array<StringName> { tag },
        };

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

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
            _failures.Add(message);
    }

    private void AssertFalse(bool condition, string message)
    {
        if (condition)
            _failures.Add(message);
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
            _failures.Add($"{message} | actual={actual} expected={expected}");
    }
}
