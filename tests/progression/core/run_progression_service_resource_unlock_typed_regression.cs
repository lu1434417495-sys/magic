using System;
using System.Collections.Generic;
using Godot;

public partial class run_progression_service_resource_unlock_typed_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestLearnedSkillsUnlockCombatResources();

        RequestTestExit(_test.Finish("Progression service resource unlock typed regression"));
    }

    private void TestLearnedSkillsUnlockCombatResources()
    {
        UnitProgress progress = new() { unit_id = "hero", display_name = "Hero" };
        SkillDefinition mpSkill = MakeCombatResourceSkill("test_mp_spell", 3, 0);
        SkillDefinition auraSkill = MakeCombatResourceSkill("test_aura_slash", 0, 2);

        ProgressionService service = new();
        service.SetupDefinitions(
            progress,
            new Dictionary<StringName, SkillDefinition>
            {
                [mpSkill.SkillId] = mpSkill,
                [auraSkill.SkillId] = auraSkill,
            },
            new Dictionary<StringName, ProfessionDefinition>()
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

        _test.True(service.LearnSkill(mpSkill.SkillId), "测试耗蓝技能应能学习。");
        _test.True(
            progress.HasCombatResourceUnlocked(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp)),
            "学习耗蓝技能后应正式解锁 MP 资源。"
        );
        _test.False(
            progress.HasCombatResourceUnlocked(CombatResourceIds.ToStringName(CombatResourceIdKind.Aura)),
            "只学习耗蓝技能不应解锁斗气资源。"
        );

        _test.True(service.LearnSkill(auraSkill.SkillId), "测试耗斗气技能应能学习。");
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

    private static SkillDefinition MakeCombatResourceSkill(StringName skillId, int mpCost, int auraCost)
    {
        return new SkillDefinition(
            skillId,
            skillId.ToString(),
            skillId,
            "",
            "active",
            1,
            0,
            "",
            0,
            0,
            System.Array.Empty<int>(),
            System.Array.Empty<StringName>(),
            "book",
            System.Array.Empty<StringName>(),
            "standard",
            System.Array.Empty<StringName>(),
            new Dictionary<StringName, int>(),
            new Dictionary<StringName, int>(),
            System.Array.Empty<StringName>(),
            System.Array.Empty<StringName>(),
            false,
            "",
            System.Array.Empty<StringName>(),
            "",
            new Dictionary<StringName, int>(),
            "",
            System.Array.Empty<AttributeModifierDefinition>(),
            "",
            new Dictionary<int, IReadOnlyDictionary<string, object>>(),
            new CombatSkillDefinition(
                skillId,
                "single",
                "enemy",
                "single",
                1,
                "single",
                0,
                true,
                0,
                mpCost,
                0,
                0,
                0,
                0,
                0,
                "",
                0,
                "",
                auraCost,
                new Dictionary<int, IReadOnlyDictionary<string, object>>(),
                "",
                "",
                "",
                "",
                0,
                System.Array.Empty<int>(),
                0,
                "",
                "",
                0,
                "",
                "",
                System.Array.Empty<StringName>(),
                System.Array.Empty<StringName>(),
                "",
                "",
                0,
                0,
                false,
                0,
                "",
                System.Array.Empty<CombatEffectDefinition>(),
                System.Array.Empty<CombatEffectDefinition>(),
                System.Array.Empty<CombatCastVariantDefinition>(),
                System.Array.Empty<StringName>(),
                System.Array.Empty<StringName>(),
                System.Array.Empty<StringName>(),
                false,
                0,
                0
            )
        );
    }
}
