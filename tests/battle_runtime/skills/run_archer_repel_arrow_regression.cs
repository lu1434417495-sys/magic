using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_archer_repel_arrow_regression : LifecycleTestSceneTree
{
    private static readonly StringName SkillId = "archer_repel_arrow";
    private const string SkillPath = "res://data/configs/skills/archer_repel_arrow.tres";
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            SkillDefinition skill = LoadSkill();
            TestAuthoredContract(skill);
            TestAttackHitTriggerSchema();
            TestBowGateAndWeaponRange(skill);
            TestAiAffordance(skill);
            TestHitDealsWeaponDamageAndRepels(skill);
            TestCriticalHitAlsoRepels(skill);
            TestMissDoesNotRepel(skill);
            TestLevelFiveRuntimeCosts(skill);
            TestBlockedDestinationKeepsDamageWithoutExtraEffect(skill);
            TestForcedMoveImmunityKeepsDamageWithoutMovement(skill);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Archer repel arrow regression"));
    }

    private void TestAuthoredContract(SkillDefinition skill)
    {
        _test.True(skill != null, "震退箭正式资源应可投影为技能定义。");
        CombatSkillDefinition combat = skill?.CombatProfile;
        _test.True(combat != null, "震退箭必须具有战斗配置。");
        if (skill == null || combat == null)
            return;

        _test.Eq(skill.SkillId, SkillId, "震退箭应使用新的技能ID。");
        _test.Eq(skill.DisplayName, "震退箭", "显示名称应与单体击退效果一致。");
        _test.Eq(skill.MaxLevel, 5, "核心技能上限应为5级。");
        _test.Eq(skill.NonCoreMaxLevel, 3, "非核心技能上限应为3级。");
        _test.Eq(combat.TargetModeKind, BattleTargetMode.Unit, "震退箭必须选择单位目标。");
        _test.Eq(combat.TargetFilterKind, BattleTargetFilter.Enemy, "震退箭只能选择敌方单位。");
        _test.Eq(combat.RangeValue, 0, "普通弓技能不应配置固定射程。");
        _test.True(combat.RequiresLos, "震退箭必须要求视线。");
        _test.True(
            combat.RequiredWeaponFamilies.Any(family => family == new StringName("bow")),
            "震退箭必须由弓装备门禁数据驱动。"
        );

        AssertCosts(combat, 0, 1, 36, 90, 0);
        AssertCosts(combat, 1, 1, 36, 90, 0);
        AssertCosts(combat, 2, 1, 34, 90, 0);
        AssertCosts(combat, 3, 1, 34, 90, 1);
        AssertCosts(combat, 4, 1, 34, 80, 1);
        AssertCosts(combat, 5, 1, 34, 80, 2);

        CombatEffectDefinition damage = null;
        CombatEffectDefinition repel = null;
        foreach (CombatEffectDefinition effect in combat.EffectDefinitions)
        {
            if (effect?.EffectKind == BattleEffectKind.Damage)
                damage = effect;
            else if (effect?.EffectKind == BattleEffectKind.ForcedMove)
                repel = effect;
        }
        _test.True(damage?.AddWeaponDice == true, "伤害必须使用当前弓武器骰。");
        _test.True(damage?.RequiresWeapon == true, "伤害结算必须要求实际武器。");
        _test.True(damage?.UseWeaponPhysicalDamageTag == true, "伤害类型必须来自当前弓。");
        _test.True(damage?.ResolveAsWeaponAttack == true, "伤害必须按标准武器攻击结算。");
        _test.Eq(repel?.ForcedMoveModeKind ?? BattleForcedMoveMode.Unknown, BattleForcedMoveMode.Knockback, "位移模式必须是远离射手的击退。");
        _test.Eq(repel?.ForcedMoveDistance ?? 0, 1, "所有等级都只能击退1格。");
        _test.Eq(repel?.TriggerEventKind ?? CombatEffectTriggerEvent.Unknown, CombatEffectTriggerEvent.AttackHit, "击退必须明确绑定任意攻击命中。");

        string levelZero = SkillLevelDescriptionFormatter.BuildLevelDescription(
            skill,
            0,
            new Godot.Collections.Dictionary()
        );
        string levelFive = SkillLevelDescriptionFormatter.BuildLevelDescription(
            skill,
            5,
            new Godot.Collections.Dictionary()
        );
        _test.True(!levelZero.Contains("+0"), "低等级描述不应显示多余的攻击检定+0。");
        _test.True(levelFive.Contains("攻击检定+2"), "5级描述应显示累计攻击检定+2。");
    }

    private void TestAttackHitTriggerSchema()
    {
        _test.Eq(
            CombatEffectContentRules.ToTriggerEvent("attack_hit"),
            CombatEffectTriggerEvent.AttackHit,
            "attack_hit应由通用触发事件枚举拥有。"
        );
        _test.Eq(
            CombatEffectContentRules.ToStringName(CombatEffectTriggerEvent.AttackHit),
            new StringName("attack_hit"),
            "attack_hit应能无损投影回资源值。"
        );

        using var loader = new TestContentResourceLoader();
        using var registry = new SkillContentRegistry(loader, loadDefaultContent: false);
        var validEffect = new CombatEffectDef
        {
            effect_type = "forced_move",
            forced_move_mode = "knockback",
            forced_move_distance = 1,
            trigger_event = "attack_hit",
        };
        var invalidEffect = new CombatEffectDef
        {
            effect_type = "forced_move",
            forced_move_mode = "knockback",
            forced_move_distance = 1,
            trigger_event = "hit",
        };
        var validErrors = new GStringArray();
        var invalidErrors = new GStringArray();
        registry.AppendEffectValidationErrors(validErrors, SkillId, validEffect, "valid");
        registry.AppendEffectValidationErrors(invalidErrors, SkillId, invalidEffect, "invalid");
        _test.Eq(validErrors.Count, 0, "正式attack_hit触发条件必须通过效果校验。");
        _test.True(
            ContainsError(invalidErrors, "unsupported trigger_event"),
            "未知命中触发值必须在内容校验期被拒绝。"
        );
        validEffect.Dispose();
        invalidEffect.Dispose();
    }

    private void TestBowGateAndWeaponRange(SkillDefinition skill)
    {
        using var runtime = BuildRuntime(skill);
        BattleUnitState archer = BuildUnit("repel_gate_archer", "player", Vector2I.Zero);
        archer.SetCurrentStamina(100);

        _test.Eq(
            runtime.GetSkillCastBlockReason(archer, skill),
            BattleSkillCastBlockReasonKind.RequiredWeaponFamilyMissing,
            "未装备弓时必须被武器门禁拒绝。"
        );
        ApplyWeapon(archer, "sword", "melee", 1);
        _test.Eq(
            runtime.GetSkillCastBlockReason(archer, skill),
            BattleSkillCastBlockReasonKind.RequiredWeaponFamilyMissing,
            "非弓武器不能使用震退箭。"
        );
        ApplyWeapon(archer, "bow", "ranged", 4);
        _test.Eq(
            runtime.GetSkillCastBlockReason(archer, skill),
            BattleSkillCastBlockReasonKind.None,
            "装备弓且资源充足时应通过施放门禁。"
        );
        _test.Eq(
            BattleRangeService.ResolveBaseSkillRange(archer, skill),
            4,
            "技能射程必须取当前弓的攻击射程。"
        );
        ApplyWeapon(archer, "bow", "ranged", 6);
        _test.Eq(
            BattleRangeService.ResolveBaseSkillRange(archer, skill),
            6,
            "更换弓后技能射程必须随武器更新。"
        );
    }

    private void TestAiAffordance(SkillDefinition skill)
    {
        BattleAiSkillAffordanceRecord record = new BattleAiSkillAffordanceClassifier()
            .ClassifySkill(skill, 1);
        _test.True(record.is_generatable, "AI必须能生成震退箭候选动作。");
        _test.True(record.effect_roles.Contains("damage"), "AI应识别标准武器伤害角色。");
        _test.True(record.effect_roles.Contains("forced_move"), "AI应识别击退角色。");
        _test.True(record.affordances.Contains("displacement_control"), "AI应把震退箭识别为位移控制。");
    }

    private void TestHitDealsWeaponDamageAndRepels(SkillDefinition skill)
    {
        BattleUnitState archer = BuildReadyArcher("repel_hit_archer", new Vector2I(1, 1), 0);
        BattleUnitState target = BuildUnit("repel_hit_target", "enemy", new Vector2I(3, 1));
        using BattleTestFixture fixture = CreateFixture(skill, archer, target);
        fixture.Runtime.ConfigureDamageResolverForTests(new FixedHitMaxDamageResolver());
        fixture.Runtime.ConfigureHitResolverForTests(new FixedHitResolver());

        int hpBefore = target.GetCurrentHp();
        BattleCommand command = BuildCommand(archer, target);
        try
        {
            BattlePreview preview = fixture.Runtime.PreviewCommand(command);
            using BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
            _test.True(preview?.allowed == true, "弓射程内敌方目标应允许施放。");
            _test.True(target.GetCurrentHp() < hpBefore, "命中必须造成真实武器伤害。");
            _test.Eq(target.GetAnchorCoord(), new Vector2I(4, 1), "命中后目标应远离射手1格。");
            _test.Eq(archer.GetCurrentAp(), 1, "成功施放应消耗1AP。");
            _test.Eq(archer.GetCurrentStamina(), 64, "0级成功施放应消耗36体力。");
            _test.Eq(archer.GetCooldownTyped(SkillId), 90, "0级成功施放应启动90TU冷却。");
            _test.True(batch.changed_unit_ids.Contains(target.unit_id), "伤害和击退应标记目标发生变化。");
        }
        finally
        {
            BattleTestFixture.DisposeBattleCommand(command);
        }
    }

    private void TestCriticalHitAlsoRepels(SkillDefinition skill)
    {
        BattleUnitState archer = BuildReadyArcher("repel_critical_archer", new Vector2I(1, 1), 0);
        BattleUnitState target = BuildUnit("repel_critical_target", "enemy", new Vector2I(3, 1));
        using BattleTestFixture fixture = CreateFixture(skill, archer, target);
        fixture.Runtime.ConfigureDamageResolverForTests(new FixedCriticalOneDamageResolver());
        fixture.Runtime.ConfigureHitResolverForTests(new FixedCriticalHitResolver());

        BattleCommand command = BuildCommand(archer, target);
        try
        {
            using BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
            _test.Eq(target.GetAnchorCoord(), new Vector2I(4, 1), "暴击同样属于命中并必须触发击退。");
        }
        finally
        {
            BattleTestFixture.DisposeBattleCommand(command);
        }
    }

    private void TestMissDoesNotRepel(SkillDefinition skill)
    {
        BattleUnitState archer = BuildReadyArcher("repel_miss_archer", new Vector2I(1, 1), 0);
        BattleUnitState target = BuildUnit("repel_miss_target", "enemy", new Vector2I(3, 1));
        using BattleTestFixture fixture = CreateFixture(skill, archer, target);
        fixture.Runtime.ConfigureDamageResolverForTests(new FixedMissOneDamageResolver());
        fixture.Runtime.ConfigureHitResolverForTests(new FixedMissResolver());

        int hpBefore = target.GetCurrentHp();
        BattleCommand command = BuildCommand(archer, target);
        try
        {
            using BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
            _test.Eq(target.GetCurrentHp(), hpBefore, "未命中不得造成武器伤害。");
            _test.Eq(target.GetAnchorCoord(), new Vector2I(3, 1), "未命中不得触发击退。");
            _test.Eq(archer.GetCurrentStamina(), 64, "未命中仍应支付36体力。");
            _test.Eq(archer.GetCooldownTyped(SkillId), 90, "未命中仍应进入90TU冷却。");
        }
        finally
        {
            BattleTestFixture.DisposeBattleCommand(command);
        }
    }

    private void TestLevelFiveRuntimeCosts(SkillDefinition skill)
    {
        BattleUnitState archer = BuildReadyArcher("repel_l5_archer", new Vector2I(1, 1), 5);
        BattleUnitState target = BuildUnit("repel_l5_target", "enemy", new Vector2I(3, 1));
        using BattleTestFixture fixture = CreateFixture(skill, archer, target);
        fixture.Runtime.ConfigureDamageResolverForTests(new FixedHitMaxDamageResolver());
        fixture.Runtime.ConfigureHitResolverForTests(new FixedHitResolver());

        BattleCommand command = BuildCommand(archer, target);
        try
        {
            using BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
            _test.Eq(archer.GetCurrentAp(), 1, "5级仍应消耗1AP。");
            _test.Eq(archer.GetCurrentStamina(), 66, "5级正式结算应消耗34体力。");
            _test.Eq(archer.GetCooldownTyped(SkillId), 80, "5级正式结算应启动80TU冷却。");
            _test.Eq(target.GetAnchorCoord(), new Vector2I(4, 1), "5级击退距离仍必须保持1格。");
        }
        finally
        {
            BattleTestFixture.DisposeBattleCommand(command);
        }
    }

    private void TestBlockedDestinationKeepsDamageWithoutExtraEffect(SkillDefinition skill)
    {
        BattleUnitState archer = BuildReadyArcher("repel_blocked_archer", new Vector2I(1, 1), 0);
        BattleUnitState target = BuildUnit("repel_blocked_target", "enemy", new Vector2I(3, 1));
        BattleUnitState blocker = BuildUnit("repel_blocker", "enemy", new Vector2I(4, 1));
        using BattleTestFixture fixture = CreateFixture(skill, archer, target, blocker);
        fixture.Runtime.ConfigureDamageResolverForTests(new FixedHitMaxDamageResolver());
        fixture.Runtime.ConfigureHitResolverForTests(new FixedHitResolver());

        int hpBefore = target.GetCurrentHp();
        BattleCommand command = BuildCommand(archer, target);
        try
        {
            using BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
            _test.True(target.GetCurrentHp() < hpBefore, "击退落点受阻时仍应结算标准武器伤害。");
            _test.Eq(target.GetAnchorCoord(), new Vector2I(3, 1), "落点被占据时目标不得穿过阻挡。");
        }
        finally
        {
            BattleTestFixture.DisposeBattleCommand(command);
        }
    }

    private void TestForcedMoveImmunityKeepsDamageWithoutMovement(SkillDefinition skill)
    {
        BattleUnitState archer = BuildReadyArcher("repel_immune_archer", new Vector2I(1, 1), 0);
        BattleUnitState target = BuildUnit("repel_immune_target", "enemy", new Vector2I(3, 1));
        target.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "repel_test_immune",
                stacks = 1,
                forced_move_immune = true,
            }
        );
        using BattleTestFixture fixture = CreateFixture(skill, archer, target);
        fixture.Runtime.ConfigureDamageResolverForTests(new FixedHitMaxDamageResolver());
        fixture.Runtime.ConfigureHitResolverForTests(new FixedHitResolver());

        int hpBefore = target.GetCurrentHp();
        BattleCommand command = BuildCommand(archer, target);
        try
        {
            using BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
            _test.True(target.GetCurrentHp() < hpBefore, "强制位移免疫不能免除武器伤害。");
            _test.Eq(target.GetAnchorCoord(), new Vector2I(3, 1), "强制位移免疫必须阻止击退。");
        }
        finally
        {
            BattleTestFixture.DisposeBattleCommand(command);
        }
    }

    private void AssertCosts(
        CombatSkillDefinition combat,
        int level,
        int ap,
        int stamina,
        int cooldown,
        int attackBonus
    )
    {
        CombatSkillResourceCosts costs = combat.GetEffectiveResourceCostValues(level);
        _test.Eq(costs.ApCost, ap, $"{level}级AP消耗应正确。");
        _test.Eq(costs.StaminaCost, stamina, $"{level}级体力消耗应正确。");
        _test.Eq(costs.CooldownTu, cooldown, $"{level}级冷却应正确。");
        _test.Eq(combat.GetEffectiveAttackRollBonus(level), attackBonus, $"{level}级攻击加值应正确。");
    }

    private static bool ContainsError(GStringArray errors, string fragment)
    {
        foreach (string error in errors ?? new GStringArray())
        {
            if (error.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static SkillDefinition LoadSkill() =>
        TestSkillDefinitionProjection.LoadSkillDefinition(SkillPath, "archer_repel_arrow_regression");

    private static BattleRuntimeModule BuildRuntime(SkillDefinition skill)
    {
        var runtime = new BattleRuntimeModule();
        runtime.setup(null, new Dictionary<StringName, SkillDefinition> { [SkillId] = skill });
        return runtime;
    }

    private static BattleTestFixture CreateFixture(
        SkillDefinition skill,
        BattleUnitState archer,
        params BattleUnitState[] enemies
    )
    {
        BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "archer_repel_arrow",
            new Vector2I(7, 4),
            new[] { archer },
            enemies
        );
        fixture.Runtime.setup(
            null,
            new Dictionary<StringName, SkillDefinition> { [SkillId] = skill }
        );
        fixture.Runtime.SetupStateForTests(fixture.State);
        return fixture;
    }

    private static BattleUnitState BuildReadyArcher(StringName id, Vector2I coord, int level)
    {
        BattleUnitState archer = BuildUnit(id, "player", coord);
        archer.AddKnownActiveSkill(SkillId);
        archer.SetKnownSkillLevelTyped(SkillId, level, preserveZero: true);
        archer.SetCurrentStamina(100);
        archer.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 100);
        archer.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 100);
        ApplyWeapon(archer, "bow", "ranged", 4);
        return archer;
    }

    private static BattleUnitState BuildUnit(StringName id, StringName faction, Vector2I coord)
    {
        var unit = new BattleUnitState
        {
            unit_id = id,
            display_name = id.ToString(),
            faction_id = faction,
        };
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 1);
        unit.attribute_snapshot.SetValue(AttributeService.STAMINA_MAX, 100);
        unit.SetCurrentHp(100);
        unit.SetCurrentAp(2);
        unit.SetCurrentStamina(100);
        unit.SetAnchorCoord(coord);
        return unit;
    }

    private static void ApplyWeapon(
        BattleUnitState unit,
        StringName family,
        StringName rangeType,
        int attackRange
    )
    {
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = "equipped",
                weapon_item_id = $"repel_test_{family}",
                weapon_profile_type_id = $"repel_test_{family}",
                weapon_range_type = rangeType,
                weapon_family = family,
                weapon_current_grip = "two_handed",
                weapon_attack_range = attackRange,
                weapon_one_handed_dice = new WeaponDice
                {
                    dice_count = 1,
                    dice_sides = 6,
                },
                weapon_two_handed_dice = new WeaponDice
                {
                    dice_count = 1,
                    dice_sides = 6,
                },
                weapon_physical_damage_tag = "physical_pierce",
            }
        );
    }

    private static BattleCommand BuildCommand(BattleUnitState archer, BattleUnitState target) =>
        new()
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            unit_id = archer.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(SkillId),
            skill_id = SkillId,
            target_unit_id = target.unit_id,
            target_coord = target.GetAnchorCoord(),
        };
}
