using System;
using System.Collections.Generic;
using Godot;

public partial class run_progression_service_resource_unlock_typed_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestLearnedSkillsUnlockCombatResources();

        Quit(_test.Finish("Progression service resource unlock typed regression"));
    }

    private void TestLearnedSkillsUnlockCombatResources()
    {
        UnitProgress progress = new() { unit_id = "hero", display_name = "Hero" };
        SkillDef mpSkill = MakeCombatResourceSkill("test_mp_spell", 3, 0);
        SkillDef auraSkill = MakeCombatResourceSkill("test_aura_slash", 0, 2);

        ProgressionService service = new();
        service.Setup(
            progress,
            new Dictionary<StringName, SkillDef>
            {
                [mpSkill.skill_id] = mpSkill,
                [auraSkill.skill_id] = auraSkill,
            },
            new Dictionary<StringName, ProfessionDef>()
        );

        _test.True(
            progress.HasCombatResourceUnlocked(CombatResourceIds.ToStringName(CombatResourceIdKind.Hp)),
            "角色初始应解锁 HP 资源。"
        );
        _test.True(
            progress.HasCombatResourceUnlocked(CombatResourceIds.ToStringName(CombatResourceIdKind.Stamina)),
            "角色初始应解锁体力资源。"
        );
        _test.False(
            progress.HasCombatResourceUnlocked(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp)),
            "学习耗蓝技能前不应显示 MP 资源。"
        );
        _test.False(
            progress.HasCombatResourceUnlocked(CombatResourceIds.ToStringName(CombatResourceIdKind.Aura)),
            "学习耗斗气技能前不应显示斗气资源。"
        );

        _test.True(service.LearnSkill(mpSkill.skill_id), "测试耗蓝技能应能学习。");
        _test.True(
            progress.HasCombatResourceUnlocked(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp)),
            "学习耗蓝技能后应正式解锁 MP 资源。"
        );
        _test.False(
            progress.HasCombatResourceUnlocked(CombatResourceIds.ToStringName(CombatResourceIdKind.Aura)),
            "只学习耗蓝技能不应解锁斗气资源。"
        );

        _test.True(service.LearnSkill(auraSkill.skill_id), "测试耗斗气技能应能学习。");
        _test.True(
            progress.HasCombatResourceUnlocked(CombatResourceIds.ToStringName(CombatResourceIdKind.Aura)),
            "学习耗斗气技能后应正式解锁斗气资源。"
        );

        UnitProgress restoredProgress = UnitProgress.FromDictionary(progress.ToDictionary());
        _test.True(
            restoredProgress.HasCombatResourceUnlocked(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp)),
            "MP 解锁状态应通过 UnitProgress 存档往返保留。"
        );
        _test.True(
            restoredProgress.HasCombatResourceUnlocked(CombatResourceIds.ToStringName(CombatResourceIdKind.Aura)),
            "斗气解锁状态应通过 UnitProgress 存档往返保留。"
        );
    }

    private static SkillDef MakeCombatResourceSkill(StringName skillId, int mpCost, int auraCost)
    {
        SkillDef skillDef = new()
        {
            skill_id = skillId,
            display_name = skillId.ToString(),
            icon_id = skillId,
            skill_type = "active",
            learn_source = "book",
            combat_profile = new CombatSkillDef
            {
                skill_id = skillId,
                mp_cost = mpCost,
                aura_cost = auraCost,
            },
        };
        return skillDef;
    }


}
