using System;
using System.Collections.Generic;
using Godot;

public partial class run_warrior_night_pressure_regression : LifecycleTestSceneTree
{
    private static readonly StringName SkillId = "warrior_night_pressure";
    private static readonly StringName StatusId = "night_pressure";
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestFormalDefinitionAndLevelEffects();
            TestStrongestPenaltySurvivesRefresh();
            TestFormalCommandAppliesStatusAndPaysCost();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Warrior night pressure regression"));
    }

    private void TestFormalDefinitionAndLevelEffects()
    {
        SkillDefinition skill = LoadSkill();
        _test.True(skill?.CombatProfile != null, "夜幕压迫正式资源应可加载。");
        if (skill?.CombatProfile == null)
            return;

        _test.Eq(skill.NonCoreMaxLevel, 3, "夜幕压迫非核心上限应为3级。");
        _test.Eq(skill.MaxLevel, 5, "夜幕压迫核心上限应为5级。");
        _test.Eq(skill.MasteryCurve.Count, 5, "夜幕压迫熟练度曲线应覆盖5级上限。");
        _test.Eq(
            skill.CombatProfile.GetEffectiveResourceCostValues(0).AuraCost,
            30,
            "0级夜幕压迫应消耗30斗气。"
        );
        _test.Eq(
            skill.CombatProfile.GetEffectiveResourceCostValues(3).AuraCost,
            25,
            "3级夜幕压迫应消耗25斗气。"
        );
        _test.Eq(skill.CombatProfile.GetEffectiveAreaValue(4), 1, "4级作用半径应为1。");
        _test.Eq(skill.CombatProfile.GetEffectiveAreaValue(5), 2, "5级作用半径应为2。");

        BattleUnitState caster = BuildCaster();
        using var rules = new BattleSkillResolutionRules();
        AssertLevelEffect(rules, skill, caster, 0, penalty: 1, durationTu: 80);
        AssertLevelEffect(rules, skill, caster, 3, penalty: 2, durationTu: 100);
        AssertLevelEffect(rules, skill, caster, 5, penalty: 2, durationTu: 120);

        _test.Eq(
            BattleRangeService.GetEffectiveSkillRange(caster, skill),
            0,
            "夜幕压迫应只能以使用者自身为中心，不读取装备武器射程。"
        );
    }

    private void TestStrongestPenaltySurvivesRefresh()
    {
        SkillDefinition skill = LoadSkill();
        if (skill?.CombatProfile == null)
            return;
        CombatEffectDefinition low = FindEffect(skill, 0);
        CombatEffectDefinition high = FindEffect(skill, 5);
        _test.True(low != null && high != null, "夜幕压迫高低等级状态定义应存在。");
        if (low == null || high == null)
            return;

        BattleStatusEffectState status = BattleStatusSemanticTable.MergeStatus(
            high,
            "strong_source"
        );
        status = BattleStatusSemanticTable.MergeStatus(low, "weak_source", status);

        _test.Eq(status?.status_id ?? default, StatusId, "夜幕压迫应生成专属状态。");
        _test.Eq(status?.stacks ?? 0, 1, "夜幕压迫重复施加不应叠层。");
        _test.Eq(status?.attack_roll_penalty ?? 0, 2, "弱效果不应覆盖已有的-2攻击惩罚。");
        _test.Eq(status?.duration ?? 0, 120, "较短的弱效果不应缩短已有持续时间。");
        _test.True(
            BattleStatusSemanticTable.IsHarmfulStatusEntry(status),
            "夜幕压迫应是有害状态。"
        );
        _test.True(
            BattleStatusSemanticTable.IsDispellableHarmfulStatusEntry(status),
            "夜幕压迫应可作为有害状态驱散。"
        );

        BattleUnitState affectedUnit = BuildCaster();
        affectedUnit.SetStatusEffect(status);
        BattleUnitReadView affectedView = affectedUnit;
        _test.Eq(
            affectedView.GetAttackRollPenalty(),
            2,
            "夜幕压迫应实际进入攻击检定惩罚汇总。"
        );
    }

    private void TestFormalCommandAppliesStatusAndPaysCost()
    {
        SkillDefinition skill = LoadSkill();
        if (skill?.CombatProfile == null)
            return;
        BattleUnitState caster = BuildBattleUnit(
            "night_pressure_runtime_caster",
            "player",
            new Vector2I(0, 1)
        );
        BattleUnitState ally = BuildBattleUnit(
            "night_pressure_runtime_ally",
            "player",
            new Vector2I(1, 1)
        );
        BattleUnitState primaryTarget = BuildBattleUnit(
            "night_pressure_runtime_primary",
            "enemy",
            new Vector2I(2, 1)
        );
        BattleUnitState secondaryTarget = BuildBattleUnit(
            "night_pressure_runtime_secondary",
            "enemy",
            new Vector2I(1, 2)
        );
        caster.UnlockCombatResource("aura");
        caster.SetCurrentAura(100);
        caster.AddKnownActiveSkill(SkillId);
        caster.SetKnownSkillLevelTyped(SkillId, 5);

        using BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "night_pressure_formal_command",
            new Vector2I(6, 4),
            new[] { caster, ally },
            new[] { primaryTarget, secondaryTarget }
        );
        fixture.Runtime.setup(
            null,
            new Dictionary<StringName, SkillDefinition> { [SkillId] = skill }
        );
        fixture.Runtime.SetupStateForTests(fixture.State);
        var remoteCommand = new BattleCommand
        {
            command_type = "skill",
            unit_id = caster.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(SkillId),
            skill_id = SkillId,
            target_coord = new Vector2I(3, 1),
        };
        remoteCommand.AddTargetCoord(remoteCommand.target_coord);
        BattlePreview remotePreview = fixture.Runtime.PreviewCommand(remoteCommand);
        _test.False(
            remotePreview?.allowed == true,
            "夜幕压迫不应允许把作用中心投放到使用者以外的格子。"
        );
        BattleTestFixture.DisposeBattleCommand(remoteCommand);

        var command = new BattleCommand
        {
            command_type = "skill",
            unit_id = caster.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(SkillId),
            skill_id = SkillId,
            target_coord = caster.GetAnchorCoord(),
        };
        command.AddTargetCoord(caster.GetAnchorCoord());
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(
            preview?.allowed == true,
            $"夜幕压迫正式技能命令应通过预览。log={FormatLogs(preview?.log_lines)}"
        );
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);

        _test.True(batch != null, "夜幕压迫应通过正式技能命令完成结算。");
        AssertAppliedRuntimeStatus(primaryTarget, "主目标");
        AssertAppliedRuntimeStatus(secondaryTarget, "半径内次要目标");
        _test.False(ally.HasStatusEffect(StatusId), "夜幕压迫不应影响范围内友军。");
        _test.Eq(caster.GetCurrentAp(), 1, "夜幕压迫应消耗1 AP。");
        _test.Eq(caster.GetCurrentAura(), 75, "5级夜幕压迫应消耗25斗气。");
        _test.Eq(caster.GetCooldownTyped(SkillId), 120, "夜幕压迫应进入120TU冷却。");

        batch?.Dispose();
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private static string FormatLogs(
        System.Collections.ObjectModel.ReadOnlyCollection<string> logs
    ) => logs == null ? "" : string.Join(" | ", logs);

    private void AssertAppliedRuntimeStatus(BattleUnitState target, string label)
    {
        BattleStatusEffectState status = target.GetStatusEffect(StatusId);
        _test.True(status != null, $"{label}应获得night_pressure状态。");
        _test.Eq(status?.attack_roll_penalty ?? 0, 2, $"{label}攻击检定应-2。");
        _test.Eq(status?.duration ?? 0, 120, $"{label}状态应持续120TU。");
    }

    private void AssertLevelEffect(
        BattleSkillResolutionRules rules,
        SkillDefinition skill,
        BattleUnitState caster,
        int level,
        int penalty,
        int durationTu
    )
    {
        caster.SetKnownSkillLevelTyped(SkillId, level);
        List<CombatEffectDefinition> effects =
            rules.CollectGroundUnitEffectDefinitions(skill, null, caster);
        _test.Eq(effects.Count, 1, $"{level}级夜幕压迫应只启用一段状态效果。");
        if (effects.Count != 1)
            return;
        _test.Eq(effects[0].StatusId, StatusId, $"{level}级应使用night_pressure状态。");
        _test.Eq(effects[0].AttackRollPenalty, penalty, $"{level}级攻击检定惩罚不符。");
        _test.Eq(effects[0].DurationTu, durationTu, $"{level}级持续时间不符。");
    }

    private static CombatEffectDefinition FindEffect(SkillDefinition skill, int level)
    {
        foreach (CombatEffectDefinition effect in skill.CombatProfile.EffectDefinitions)
        {
            if (
                effect != null
                && level >= Math.Max(effect.MinSkillLevel, 0)
                && (effect.MaxSkillLevel < 0 || level <= effect.MaxSkillLevel)
            )
            {
                return effect;
            }
        }
        return null;
    }

    private static SkillDefinition LoadSkill() =>
        TestSkillDefinitionProjection.LoadSkillDefinition(
            "res://data/configs/skills/warrior_night_pressure.tres",
            "warrior_night_pressure_regression"
        );

    private static BattleUnitState BuildCaster()
    {
        BattleUnitState unit = new BattleUnitState()
        {
            unit_id = "night_pressure_caster",
            faction_id = "player",
        }.WithCombatResourcesForTest(
            hp: 100,
            aura: 100,
            ap: 2,
            isAlive: true
        );
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = "equipped",
                weapon_item_id = "night_pressure_test_polearm",
                weapon_profile_type_id = "glaive",
                weapon_range_type = "melee",
                weapon_family = "polearm",
                weapon_current_grip = "two_handed",
                weapon_attack_range = 6,
                weapon_one_handed_dice = new WeaponDice(),
                weapon_two_handed_dice = new WeaponDice
                {
                    dice_count = 1,
                    dice_sides = 10,
                },
                weapon_is_versatile = false,
                weapon_uses_two_hands = true,
                weapon_physical_damage_tag = "physical_pierce",
            }
        );
        unit.SetKnownActiveSkillIds(new[] { SkillId });
        return unit;
    }

    private static BattleUnitState BuildBattleUnit(
        StringName unitId,
        StringName factionId,
        Vector2I coord
    )
    {
        BattleUnitState unit = BattleTestFixture.BuildUnit(
            unitId,
            factionId,
            coord,
            currentAp: 2,
            currentHp: 100
        );
        unit.attribute_snapshot.SetValue(
            AttributeService.ToStringName(AttributeIdKind.ActionPoints),
            2
        );
        unit.attribute_snapshot.SetValue("aura_max", 100);
        return unit;
    }
}
