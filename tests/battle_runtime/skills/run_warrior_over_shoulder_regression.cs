using System;
using System.Collections.Generic;
using Godot;

public partial class run_warrior_over_shoulder_regression : LifecycleTestSceneTree
{
    private static readonly StringName SkillId = "warrior_over_shoulder";
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            SkillDefinition skill = LoadSkill();
            TestAuthoredContract(skill);
            TestMeleeWeaponGate(skill);
            TestHitDealsDamageAndVaults(skill);
            TestMissDoesNotVault(skill);
            TestBlockedLandingRejectsBeforeCost(skill);
            TestWallRejectsBeforeCost(skill);
            TestBarrierRejectsBeforeCost(skill);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Warrior over shoulder regression"));
    }

    private void TestAuthoredContract(SkillDefinition skill)
    {
        CombatSkillDefinition combat = skill?.CombatProfile;
        _test.True(combat != null, "借势越肩正式资源应可加载。");
        if (combat == null)
            return;

        _test.Eq(skill.MaxLevel, 5, "借势越肩核心上限应为5级。");
        _test.Eq(skill.NonCoreMaxLevel, 3, "借势越肩非核心上限应为3级。");
        _test.Eq(combat.RangeValue, 1, "借势越肩只能攻击相邻目标。");
        _test.Eq(
            combat.WeaponRangePolicy.ToString(),
            "configured",
            "长柄武器不能扩大借势越肩的施放距离。"
        );
        _test.Eq(combat.GetEffectiveResourceCostValues(0).ApCost, 1, "借势越肩消耗1 AP。");
        _test.Eq(combat.GetEffectiveResourceCostValues(0).StaminaCost, 35, "0级消耗35体力。");
        _test.Eq(combat.GetEffectiveResourceCostValues(2).StaminaCost, 35, "2级仍应消耗35体力。");
        _test.Eq(combat.GetEffectiveResourceCostValues(3).StaminaCost, 25, "3级起降低为25体力。");
        _test.Eq(combat.GetEffectiveResourceCostValues(5).StaminaCost, 25, "5级应继承3级减耗。");

        CombatEffectDefinition lowDamage = null;
        CombatEffectDefinition highDamage = null;
        bool hasVault = false;
        foreach (CombatEffectDefinition effect in combat.EffectDefinitions)
        {
            if (effect?.EffectKind == BattleEffectKind.VaultBehindTarget)
                hasVault = true;
            if (effect?.EffectKind != BattleEffectKind.Damage)
                continue;
            if (effect.MinSkillLevel == 0)
                lowDamage = effect;
            if (effect.MinSkillLevel == 4)
                highDamage = effect;
        }
        _test.True(hasVault, "借势越肩必须声明通用越肩落位效果。");
        _test.Eq(lowDamage?.MaxSkillLevel ?? -2, 3, "1d4附加伤害应止于3级。");
        _test.Eq(lowDamage?.DiceSides ?? 0, 4, "0至3级应附加1d4伤害。");
        _test.Eq(highDamage?.DiceSides ?? 0, 6, "4至5级应附加1d6伤害。");
        _test.True(lowDamage?.AddWeaponDice == true, "低级伤害应包含完整武器骰。");
        _test.True(highDamage?.AddWeaponDice == true, "高级伤害应包含完整武器骰。");
    }

    private void TestMeleeWeaponGate(SkillDefinition skill)
    {
        using var runtime = BuildRuntime(skill);
        BattleUnitState caster = BuildUnit("vault_gate", "player", Vector2I.Zero);
        caster.SetCurrentStamina(100);

        ApplyWeapon(caster, "spear", "melee", 2);
        _test.Eq(
            runtime.GetSkillCastBlockReason(caster, skill),
            BattleSkillCastBlockReasonKind.None,
            "任意近战武器都应允许借势越肩。"
        );
        _test.Eq(
            BattleRangeService.ResolveBaseSkillRange(caster, skill),
            1,
            "长矛射程不能把借势越肩扩到2格。"
        );

        ApplyWeapon(caster, "bow", "ranged", 4);
        _test.Eq(
            runtime.GetSkillCastBlockReason(caster, skill),
            BattleSkillCastBlockReasonKind.MeleeWeaponRequired,
            "远程武器必须被借势越肩的melee门禁拒绝。"
        );
    }

    private void TestHitDealsDamageAndVaults(SkillDefinition skill)
    {
        BattleUnitState caster = BuildReadyCaster("vault_hit_caster", new Vector2I(1, 1));
        BattleUnitState target = BuildUnit("vault_hit_target", "enemy", new Vector2I(2, 1));
        using BattleTestFixture fixture = CreateFixture(skill, caster, target);
        fixture.Runtime.ConfigureDamageResolverForTests(new FixedHitMaxDamageResolver());
        fixture.Runtime.ConfigureHitResolverForTests(new FixedHitResolver());

        BattleCommand command = BuildCommand(caster, target);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(preview?.allowed == true, "目标背后可落脚时应允许施放。");
        _test.True(
            preview?.target_coords.Contains(new Vector2I(3, 1)) == true,
            "技能预览必须包含最终落点。"
        );

        int hpBefore = target.GetCurrentHp();
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.True(batch != null, "借势越肩应通过正式技能命令结算。");
        _test.True(target.GetCurrentHp() < hpBefore, "借势越肩必须产生真实HP伤害。");
        _test.Eq(caster.GetAnchorCoord(), new Vector2I(3, 1), "命中后使用者应落到目标正后方。");
        _test.Eq(caster.GetCurrentAp(), 1, "成功施放应消耗1 AP。");
        _test.Eq(caster.GetCurrentStamina(), 65, "成功施放应消耗35体力。");
        _test.True(batch.changed_unit_ids.Contains(caster.unit_id), "位移应写入变更单位数据。");
        _test.True(batch.changed_unit_ids.Contains(target.unit_id), "伤害应写入变更单位数据。");
        batch?.Dispose();
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private void TestMissDoesNotVault(SkillDefinition skill)
    {
        BattleUnitState caster = BuildReadyCaster("vault_miss_caster", new Vector2I(1, 1));
        BattleUnitState target = BuildUnit("vault_miss_target", "enemy", new Vector2I(2, 1));
        using BattleTestFixture fixture = CreateFixture(skill, caster, target);
        fixture.Runtime.ConfigureDamageResolverForTests(new FixedHitMaxDamageResolver());
        fixture.Runtime.ConfigureHitResolverForTests(new FixedMissResolver());

        Vector2I coordBefore = caster.GetAnchorCoord();
        int hpBefore = target.GetCurrentHp();
        BattleCommand command = BuildCommand(caster, target);
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.True(batch != null, "未命中仍应完成一次合法技能结算。");
        _test.Eq(target.GetCurrentHp(), hpBefore, "未命中不得造成伤害。");
        _test.Eq(caster.GetAnchorCoord(), coordBefore, "未命中不得触发越肩位移。");
        batch?.Dispose();
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private void TestBlockedLandingRejectsBeforeCost(SkillDefinition skill)
    {
        BattleUnitState caster = BuildReadyCaster("vault_blocked_caster", new Vector2I(1, 1));
        BattleUnitState target = BuildUnit("vault_blocked_target", "enemy", new Vector2I(2, 1));
        BattleUnitState blocker = BuildUnit("vault_blocker", "enemy", new Vector2I(3, 1));
        using BattleTestFixture fixture = CreateFixture(skill, caster, target, blocker);

        AssertRejectedWithoutCost(fixture.Runtime, caster, target, "落点被占据时");
    }

    private void TestWallRejectsBeforeCost(SkillDefinition skill)
    {
        BattleUnitState caster = BuildReadyCaster("vault_wall_caster", new Vector2I(1, 1));
        BattleUnitState target = BuildUnit("vault_wall_target", "enemy", new Vector2I(2, 1));
        using BattleTestFixture fixture = CreateFixture(skill, caster, target);
        fixture.Runtime
            .GetGridService()
            .SetEdgeFeature(
                fixture.State,
                target.GetAnchorCoord(),
                Vector2I.Right,
                BattleEdgeFeatureState.MakeWall()
            );

        AssertRejectedWithoutCost(fixture.Runtime, caster, target, "目标与落点之间有墙时");
    }

    private void TestBarrierRejectsBeforeCost(SkillDefinition skill)
    {
        BattleUnitState caster = BuildReadyCaster("vault_barrier_caster", new Vector2I(1, 1));
        BattleUnitState target = BuildUnit("vault_barrier_target", "enemy", new Vector2I(2, 1));
        using BattleTestFixture fixture = CreateFixture(skill, caster, target);
        var barrier = new BattleBarrierInstanceState
        {
            BarrierInstanceId = "vault_test_barrier",
            ProfileId = "vault_test_barrier",
            DisplayName = "测试屏障",
            SourceUnitId = "other_unit",
            AnchorCoord = target.GetAnchorCoord(),
            RadiusCells = 0,
            AreaPattern = "diamond",
            RemainingTu = 100,
        };
        fixture.State.PutLayeredBarrierFieldPayload(
            barrier.BarrierInstanceId,
            barrier.ToRuntimeDict()
        );

        AssertRejectedWithoutCost(fixture.Runtime, caster, target, "路径跨越屏障边界时");
    }

    private void AssertRejectedWithoutCost(
        BattleRuntimeModule runtime,
        BattleUnitState caster,
        BattleUnitState target,
        string label
    )
    {
        int apBefore = caster.GetCurrentAp();
        int staminaBefore = caster.GetCurrentStamina();
        BattleCommand command = BuildCommand(caster, target);
        BattlePreview preview = runtime.PreviewCommand(command);
        _test.True(preview != null && !preview.allowed, $"{label}预览必须拒绝技能。");
        BattleEventBatch batch = runtime.IssueCommand(command);
        _test.Eq(caster.GetCurrentAp(), apBefore, $"{label}不得消耗AP。");
        _test.Eq(caster.GetCurrentStamina(), staminaBefore, $"{label}不得消耗体力。");
        batch?.Dispose();
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private static BattleTestFixture CreateFixture(
        SkillDefinition skill,
        BattleUnitState caster,
        params BattleUnitState[] enemies
    )
    {
        BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "warrior_over_shoulder",
            new Vector2I(6, 4),
            new[] { caster },
            enemies
        );
        fixture.Runtime.setup(
            null,
            new Dictionary<StringName, SkillDefinition> { [SkillId] = skill }
        );
        fixture.Runtime.SetupStateForTests(fixture.State);
        return fixture;
    }

    private static SkillDefinition LoadSkill() =>
        TestSkillDefinitionProjection.LoadSkillDefinition(
            "res://data/configs/skills/warrior_over_shoulder.tres",
            "warrior_over_shoulder_regression"
        );

    private static BattleRuntimeModule BuildRuntime(SkillDefinition skill)
    {
        var runtime = new BattleRuntimeModule();
        runtime.setup(
            null,
            new Dictionary<StringName, SkillDefinition> { [SkillId] = skill }
        );
        return runtime;
    }

    private static BattleUnitState BuildReadyCaster(StringName id, Vector2I coord)
    {
        BattleUnitState caster = BuildUnit(id, "player", coord);
        caster.AddKnownActiveSkill(SkillId);
        caster.SetKnownSkillLevelTyped(SkillId, 0, preserveZero: true);
        caster.SetCurrentStamina(100);
        caster.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 100);
        caster.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 100);
        ApplyWeapon(caster, "sword", "melee", 1);
        return caster;
    }

    private static BattleUnitState BuildUnit(
        StringName id,
        StringName faction,
        Vector2I coord
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = id,
            display_name = id.ToString(),
            faction_id = faction,
        };
        unit.SetCurrentHp(100);
        unit.SetCurrentAp(2);
        unit.SetCurrentStamina(100);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 1);
        unit.attribute_snapshot.SetValue(AttributeService.STAMINA_MAX, 100);
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
                weapon_item_id = $"vault_test_{family}",
                weapon_profile_type_id = $"test_{family}",
                weapon_range_type = rangeType,
                weapon_family = family,
                weapon_current_grip = "one_handed",
                weapon_attack_range = attackRange,
                weapon_one_handed_dice = new WeaponDice
                {
                    dice_count = 1,
                    dice_sides = 6,
                },
                weapon_two_handed_dice = new WeaponDice(),
                weapon_physical_damage_tag = "physical_slash",
            }
        );
    }

    private static BattleCommand BuildCommand(
        BattleUnitState caster,
        BattleUnitState target
    ) =>
        new()
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            unit_id = caster.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(SkillId),
            skill_id = SkillId,
            target_unit_id = target.unit_id,
            target_coord = target.GetAnchorCoord(),
        };
}
