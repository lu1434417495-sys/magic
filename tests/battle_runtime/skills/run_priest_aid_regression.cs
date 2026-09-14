using System;
using System.Collections.Generic;
using Godot;

public partial class run_priest_aid_regression : LifecycleTestSceneTree
{
    private static readonly StringName SkillId = "priest_aid";
    private static readonly StringName ShieldFamily = "holy_barrier";
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            SkillDefinition skill = TestSkillDefinitionProjection.LoadSkillDefinition(
                "priest_aid",
                "priest_aid_regression"
            );
            TestAuthoredContract(skill);
            TestLevelCurve(skill);
            TestPreviewExecutionAndPerTargetMastery(skill);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Priest aid regression"));
    }

    private void TestAuthoredContract(SkillDefinition skill)
    {
        _test.True(skill?.CombatProfile != null, "援助术正式资源及 combat_profile 应可加载。");
        if (skill?.CombatProfile == null)
        {
            return;
        }
        CombatSkillDefinition combat = skill.CombatProfile;
        _test.Eq(skill.SkillId, SkillId, "援助术 skill_id 应稳定。");
        _test.Eq(skill.MaxLevel, 5, "援助术核心等级上限应为5级。");
        _test.Eq(skill.NonCoreMaxLevel, 3, "援助术非核心等级上限应为3级。");
        _test.Eq(skill.GrowthTier, new StringName("intermediate"), "援助术应使用中级成长档。");
        _test.Eq(ReadGrowth(skill, "willpower"), 120, "援助术应提供120点意志成长进度。");
        _test.Eq(combat.TargetMode, new StringName("ground"), "援助术应选择地格。");
        _test.Eq(combat.TargetTeamFilter, new StringName("ally"), "援助术只应影响友军。");
        _test.Eq(combat.MasteryTriggerMode, new StringName("effect_applied"), "熟练度只应在护盾实际改善时触发。");
        _test.Eq(combat.MasteryAmountMode, new StringName("per_target_rank"), "熟练度应按成功改善的单位逐个结算。");
        _test.Eq(combat.RequiredWeaponFamilies.Count, 0, "援助术不应有武器限制。");
        _test.True(skill.Description.Contains("每个目标独立投骰"), "玩家描述应明确逐目标投骰。");

        _test.Eq(combat.EffectDefinitions.Count, 3, "援助术应以三段互斥效果表达持续时间成长。");
        AssertEffectLevelWindow(combat.EffectDefinitions[0], 0, 2, "基础护盾段");
        AssertEffectLevelWindow(combat.EffectDefinitions[1], 3, 4, "中间护盾段");
        AssertEffectLevelWindow(combat.EffectDefinitions[2], 5, 5, "满级护盾段");
        foreach (CombatEffectDefinition effect in combat.EffectDefinitions)
        {
            _test.Eq(effect.EffectKind, BattleEffectKind.Shield, "援助术每段效果都应为护盾。");
            _test.Eq(effect.DiceCount, 1, "援助术应投1枚护盾骰。");
            _test.Eq(effect.DiceSides, 8, "援助术护盾骰应为D8。");
            _test.Eq(effect.DiceBonus, 3, "援助术护盾应有+3基础加值。");
            _test.Eq(effect.ShieldFamily, ShieldFamily, "援助术应使用 typed 神圣护盾族。");
            _test.Eq(effect.ShieldAttributeModifierId, new StringName("willpower_modifier"), "援助术应读取施法者意志调整值。");
            _test.True(effect.ShieldRollPerTarget, "援助术每个目标必须独立投骰。");
            _test.True(
                effect.Payload is EmptyCombatEffectPayloadDefinition,
                "护盾效果不得携带额外 payload。"
            );
        }
    }

    private void AssertEffectLevelWindow(
        CombatEffectDefinition effect,
        int minLevel,
        int maxLevel,
        string label
    )
    {
        _test.Eq(effect.MinSkillLevel, minLevel, $"{label}最低技能等级应正确。");
        _test.Eq(effect.MaxSkillLevel, maxLevel, $"{label}最高技能等级应正确。");
    }

    private void TestLevelCurve(SkillDefinition skill)
    {
        int[] expectedMastery = { 150, 375, 825, 1500, 2400 };
        for (int level = 0; level < expectedMastery.Length; level++)
        {
            _test.Eq(skill.GetMasteryRequiredForLevel(level), expectedMastery[level], $"援助术{level}升{level + 1}熟练度需求应为批准后的50%提高曲线。");
        }

        AssertLevel(skill, 0, "cross", 1, 0, 18, 120, 40);
        AssertLevel(skill, 1, "radius", 1, 0, 18, 120, 40);
        AssertLevel(skill, 2, "radius", 1, 0, 16, 120, 40);
        AssertLevel(skill, 3, "radius", 1, 0, 16, 120, 50);
        AssertLevel(skill, 4, "radius", 1, 1, 14, 120, 50);
        AssertLevel(skill, 5, "radius", 1, 2, 14, 100, 60);
    }

    private void AssertLevel(
        SkillDefinition skill,
        int level,
        StringName areaPattern,
        int areaValue,
        int range,
        int stamina,
        int cooldownTu,
        int durationTu
    )
    {
        CombatSkillDefinition combat = skill.CombatProfile;
        CombatSkillResourceCosts costs = combat.GetEffectiveResourceCostValues(level);
        _test.Eq(costs.ApCost, 1, $"援助术{level}级应消耗1AP。");
        _test.Eq(costs.MpCost, 40, $"援助术{level}级应消耗40MP。");
        _test.Eq(costs.StaminaCost, stamina, $"援助术{level}级体力消耗应正确。");
        _test.Eq(costs.CooldownTu, cooldownTu, $"援助术{level}级冷却应正确。");
        _test.Eq(combat.GetEffectiveAreaPattern(level), areaPattern, $"援助术{level}级范围形状应正确。");
        _test.Eq(combat.GetEffectiveAreaValue(level), areaValue, $"援助术{level}级范围值应正确。");
        _test.Eq(combat.GetEffectiveRangeValue(level), range, $"援助术{level}级选点距离应正确。");
        List<CombatEffectDefinition> active = ActiveEffects(combat.EffectDefinitions, level);
        _test.Eq(active.Count, 1, $"援助术{level}级应只有一段护盾生效。");
        if (active.Count == 1)
        {
            _test.Eq(active[0].DurationTu, durationTu, $"援助术{level}级持续时间应正确。");
        }
    }

    private void TestPreviewExecutionAndPerTargetMastery(SkillDefinition skill)
    {
        using MasteryFixture fixture = BuildMasteryFixture(skill, 1);
        BattleCommand command = BuildCommand(fixture.Caster, fixture.Caster.GetAnchorCoord());
        BattlePreview preview = null;
        BattleEventBatch batch = null;
        try
        {
            preview = fixture.Runtime.PreviewCommand(command);
            _test.True(preview?.allowed == true, $"1级援助术应允许以自身为中心施放。logs={FormatLogs(preview)}");
            BattleShieldPreviewData shieldPreview = preview?.ShieldPreviewTyped;
            _test.True(shieldPreview?.HasShield == true, "正式预览应暴露 typed 护盾信息。");
            _test.Eq(shieldPreview?.MinShieldHp ?? -1, 6, "意志14时护盾下限应为1+3+2=6。");
            _test.Eq(shieldPreview?.MaxShieldHp ?? -1, 13, "意志14时护盾上限应为8+3+2=13。");
            _test.Eq(shieldPreview?.AttributeModifier ?? -99, 2, "预览应显示施法者意志调整值+2。");
            _test.Eq(shieldPreview?.TargetCount ?? -1, 3, "预览应去重统计施法者、友军与友方召唤物三个单位。");
            _test.True(shieldPreview?.RollPerTarget == true, "预览应声明每目标独立投骰。");

            int masteryBefore = fixture.SkillProgress.current_mastery;
            batch = fixture.Runtime.IssueCommand(command);
            _test.True(batch != null, "援助术应通过正式命令结算。");
            AssertShield(fixture.Caster, 40, "施法者");
            AssertShield(fixture.CenterAlly, 40, "范围内友军");
            AssertShield(fixture.Summon, 40, "范围内友方召唤物");
            _test.False(fixture.OutsideAlly.HasShield(), "范围外友军不得获得护盾。");
            _test.Eq(fixture.SkillProgress.current_mastery, masteryBefore + 3, "三个实际获得改善的单位应各增加1熟练度。");
            _test.Eq(fixture.Caster.GetCurrentAp(), 1, "1级援助术应消耗1AP。");
            _test.Eq(fixture.Caster.GetCurrentMp(), 60, "1级援助术应消耗40MP。");
            _test.Eq(fixture.Caster.GetCurrentStamina(), 82, "1级援助术应消耗18体力。");
            _test.Eq(fixture.Caster.GetCooldownTyped(SkillId), 120, "1级援助术应进入120TU冷却。");

            batch.Dispose();
            batch = null;
            SetStrongShield(fixture.Caster);
            SetStrongShield(fixture.CenterAlly);
            SetStrongShield(fixture.Summon);
            fixture.Caster.SetCurrentAp(2);
            fixture.Caster.SetCurrentMp(100);
            fixture.Caster.SetCurrentStamina(100);
            fixture.Caster.SetCooldownTyped(SkillId, 0);
            int masteryBeforeNoOp = fixture.SkillProgress.current_mastery;
            batch = fixture.Runtime.IssueCommand(command);
            _test.Eq(fixture.SkillProgress.current_mastery, masteryBeforeNoOp, "较弱且更短的护盾未改善任何单位时不得增加熟练度。");
        }
        finally
        {
            batch?.Dispose();
            BattleTestFixture.DisposeBattlePreview(preview);
            BattleTestFixture.DisposeBattleCommand(command);
        }
    }

    private void AssertShield(BattleUnitState unit, int durationTu, string label)
    {
        BattleUnitShieldSnapshot shield = unit.GetShieldStateTyped();
        _test.True(shield.CurrentHp >= 6 && shield.CurrentHp <= 13, $"{label}护盾应落在1D8+3+意志调整值范围内。");
        _test.Eq(shield.Duration, durationTu, $"{label}护盾持续时间应正确。");
        _test.Eq(shield.Family, ShieldFamily, $"{label}应获得神圣护盾族。");
    }

    private static void SetStrongShield(BattleUnitState unit)
    {
        unit.ReplaceShieldStateTyped(20, 20, 200, ShieldFamily, "old_source", "old_skill");
    }

    private static MasteryFixture BuildMasteryFixture(SkillDefinition skill, int level)
    {
        var definitions = new Dictionary<StringName, SkillDefinition> { [SkillId] = skill };
        var progress = new UnitProgress { unit_id = "hero", display_name = "援助术施法者" };
        var skillProgress = new UnitSkillProgress
        {
            skill_id = SkillId,
            is_learned = true,
            skill_level = level,
            current_mastery = 0,
            total_mastery_earned = 0,
            granted_source_type = "player",
        };
        progress.SetSkillProgress(skillProgress);
        var member = new PartyMemberState
        {
            member_id = "hero",
            display_name = "援助术施法者",
            progression = progress,
            current_hp = 100,
            current_mp = 100,
        };
        var party = new PartyState
        {
            leader_member_id = member.member_id,
            main_character_member_id = member.member_id,
            active_member_ids = new StringNameList { member.member_id },
        };
        party.SetMemberState(member);
        var characterManagement = new CharacterManagementModule();
        characterManagement.setup(party, definitions);

        BattleUnitState caster = BuildUnit("aid_caster", "player", new Vector2I(2, 2));
        caster.source_member_id = member.member_id;
        caster.AddKnownActiveSkill(SkillId);
        caster.SetKnownSkillLevelTyped(SkillId, level, preserveZero: true);
        caster.attribute_snapshot.SetValue("willpower", 14);
        BattleUnitState centerAlly = BuildUnit("aid_center_ally", "player", new Vector2I(3, 2));
        BattleUnitState summon = BuildUnit("aid_summon", "player", new Vector2I(3, 3));
        summon.source_member_id = "";
        BattleUnitState outsideAlly = BuildUnit("aid_outside", "player", new Vector2I(6, 4));
        BattleUnitState enemy = BuildUnit("aid_enemy", "enemy", new Vector2I(3, 1));
        BattleTestFixture battle = BattleTestFixture.CreateFlatBattle(
            "priest_aid_runtime",
            new Vector2I(8, 6),
            new[] { caster, centerAlly, summon, outsideAlly },
            new[] { enemy }
        );
        battle.Runtime.setup(characterManagement, definitions);
        battle.State.active_unit_id = caster.unit_id;
        battle.Runtime.SetupStateForTests(battle.State);
        return new MasteryFixture(
            battle,
            characterManagement,
            caster,
            centerAlly,
            summon,
            outsideAlly,
            skillProgress
        );
    }

    private static BattleUnitState BuildUnit(StringName id, StringName faction, Vector2I coord)
    {
        BattleUnitState unit = new BattleUnitState
        {
            unit_id = id,
            source_member_id = id,
            display_name = id.ToString(),
            faction_id = faction,
            control_mode = "manual",
        }.WithCombatResourcesForTest(hp: 100, mp: 100, stamina: 100, ap: 2, isAlive: true);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        unit.attribute_snapshot.SetValue(AttributeService.MP_MAX, 100);
        unit.attribute_snapshot.SetValue(AttributeService.STAMINA_MAX, 100);
        unit.attribute_snapshot.SetValue("willpower", 10);
        unit.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp));
        unit.SetAnchorCoord(coord);
        return unit;
    }

    private static BattleCommand BuildCommand(BattleUnitState caster, Vector2I targetCoord)
    {
        var command = new BattleCommand
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = caster.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(SkillId),
            skill_id = SkillId,
            target_coord = targetCoord,
        };
        command.AddTargetCoord(targetCoord);
        return command;
    }

    private static List<CombatEffectDefinition> ActiveEffects(
        IReadOnlyList<CombatEffectDefinition> effects,
        int level
    )
    {
        var result = new List<CombatEffectDefinition>();
        foreach (CombatEffectDefinition effect in effects ?? Array.Empty<CombatEffectDefinition>())
        {
            if (
                effect != null
                && level >= Math.Max(effect.MinSkillLevel, 0)
                && (effect.MaxSkillLevel < 0 || level <= effect.MaxSkillLevel)
            )
            {
                result.Add(effect);
            }
        }
        return result;
    }

    private static int ReadGrowth(SkillDefinition skill, StringName attributeId) =>
        skill.AttributeGrowthProgress.TryGetValue(attributeId, out int value) ? value : 0;

    private static string FormatLogs(BattlePreview preview) =>
        preview == null ? "" : string.Join(" | ", preview.LogLinesTyped);

    private sealed class MasteryFixture : IDisposable
    {
        internal BattleRuntimeModule Runtime => Battle.Runtime;
        internal BattleTestFixture Battle { get; }
        internal CharacterManagementModule CharacterManagement { get; }
        internal BattleUnitState Caster { get; }
        internal BattleUnitState CenterAlly { get; }
        internal BattleUnitState Summon { get; }
        internal BattleUnitState OutsideAlly { get; }
        internal UnitSkillProgress SkillProgress { get; }

        internal MasteryFixture(
            BattleTestFixture battle,
            CharacterManagementModule characterManagement,
            BattleUnitState caster,
            BattleUnitState centerAlly,
            BattleUnitState summon,
            BattleUnitState outsideAlly,
            UnitSkillProgress skillProgress
        )
        {
            Battle = battle;
            CharacterManagement = characterManagement;
            Caster = caster;
            CenterAlly = centerAlly;
            Summon = summon;
            OutsideAlly = outsideAlly;
            SkillProgress = skillProgress;
        }

        public void Dispose()
        {
            Battle?.Dispose();
            CharacterManagement?.Dispose();
        }
    }
}
