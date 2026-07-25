using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;

public partial class run_warrior_chain_slashes_regression : LifecycleTestSceneTree
{
    private static readonly StringName SkillId = "warrior_chain_slashes";
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            SkillDefinition skill = LoadSkill();
            TestContentAndLevelContract(skill);
            TestMeleeWeaponGate(skill);
            TestThreeHitsUseOneSkillCost(skill);
            TestMissDoesNotCancelRemainingAttacks(skill);
            TestKillStopsRemainingAttacks(skill);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Warrior chain slashes regression"));
    }

    private void TestContentAndLevelContract(SkillDefinition skill)
    {
        _test.True(skill?.CombatProfile != null, "应能加载三连斩正式资源。");
        if (skill?.CombatProfile == null)
            return;

        _test.Eq(skill.DisplayName, "三连斩", "技能显示名应明确三段身份。");
        _test.Eq(skill.NonCoreMaxLevel, 5, "三连斩非核心上限应为5级。");
        _test.Eq(skill.MaxLevel, 7, "三连斩核心上限应为7级。");
        _test.Eq(skill.MasteryCurve.Count, 7, "熟练度曲线应覆盖0至7级。");
        _test.Eq(skill.GetMasteryRequiredForLevel(0), 240, "三连斩0升1应需要240熟练度。");
        _test.Eq(skill.GetMasteryRequiredForLevel(6), 7800, "三连斩6升7应需要7800熟练度。");

        CombatEffectDefinition repeatEffect = FindFixedRepeatEffect(skill);
        _test.Eq(repeatEffect?.FixedAttackCount ?? 0, 3, "三连斩应固定结算三段。");
        _test.Eq(
            BattleRepeatAttackResolver.resolve_repeat_attack_preview_stage_count(
                BuildUnit("chain_preview", "player", Vector2I.Zero),
                skill,
                repeatEffect
            ),
            3,
            "HUD与AI预览应得到精确三段。"
        );

        int[] expectedStamina = { 30, 30, 30, 24, 24, 24, 20, 20 };
        int[] expectedAttackBonus = { 0, 0, 0, 0, 0, 1, 1, 1 };
        int[] expectedDiceSides = { 4, 4, 4, 4, 6, 6, 6, 8 };
        for (int level = 0; level <= 7; level++)
        {
            SkillEffectiveCombatDefinition effective =
                SkillEffectiveCombatDefinition.BuildUncached(skill, level);
            _test.Eq(
                effective.ResourceCosts.StaminaCost,
                expectedStamina[level],
                $"三连斩{level}级体力消耗应匹配设计。"
            );
            _test.Eq(
                effective.AttackRollBonus,
                expectedAttackBonus[level],
                $"三连斩{level}级攻击加值应匹配设计。"
            );
            CombatEffectDefinition damage = FindActiveDamageEffect(skill, level);
            _test.Eq(
                damage?.DiceSides ?? 0,
                expectedDiceSides[level],
                $"三连斩{level}级技能伤害骰应匹配设计。"
            );
        }
    }

    private void TestMeleeWeaponGate(SkillDefinition skill)
    {
        using BattleRuntimeModule runtime = BuildRuntime(skill);
        BattleUnitState caster = BuildUnit("chain_gate", "player", Vector2I.Zero);
        caster.SetCurrentStamina(100);
        ApplyWeapon(caster, "spear", "melee", 2);
        _test.Eq(
            runtime.GetSkillCastBlockReason(caster, skill),
            BattleSkillCastBlockReasonKind.None,
            "长矛属于近战武器，应允许使用三连斩。"
        );
        ApplyWeapon(caster, "bow", "ranged", 4);
        _test.Eq(
            runtime.GetSkillCastBlockReason(caster, skill),
            BattleSkillCastBlockReasonKind.MeleeWeaponRequired,
            "弓应被三连斩的melee门禁拒绝。"
        );
        ClearWeapon(caster);
        _test.Eq(
            runtime.GetSkillCastBlockReason(caster, skill),
            BattleSkillCastBlockReasonKind.MeleeWeaponRequired,
            "未装备武器时应拒绝三连斩。"
        );
    }

    private void TestThreeHitsUseOneSkillCost(SkillDefinition skill)
    {
        using BattleRuntimeModule runtime = BuildRuntime(skill);
        runtime.ConfigureDamageResolverForTests(
            new FixedRollDamageResolver(new GArray { 4, 3, 5, 2, 6, 1 })
        );
        runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
        (BattleUnitState caster, BattleUnitState target) = SetupDuel(runtime, targetHp: 200);

        int hpBefore = target.GetCurrentHp();
        BattleEventBatch batch = runtime.IssueCommand(BuildCommand(caster, target));

        _test.True(ContainsLog(batch, "第 1 段"), "三连斩应结算第一段。");
        _test.True(ContainsLog(batch, "第 2 段"), "三连斩应结算第二段。");
        _test.True(ContainsLog(batch, "第 3 段"), "三连斩应结算第三段。");
        _test.True(target.GetCurrentHp() < hpBefore, "三段攻击应造成真实HP伤害。");
        _test.Eq(caster.GetCurrentAp(), 1, "三连斩只应消耗一次1 AP。");
        _test.Eq(caster.GetCurrentStamina(), 70, "0级三连斩只应消耗一次30体力。");
        _test.Eq(
            caster.GetStatusEffect("melee_combo_stack")?.stacks ?? 0,
            3,
            "三段全部命中应获得3层melee_combo_stack。"
        );

        BattleUnitState eventSource = BuildUnit("chain_event_source", "player", Vector2I.Zero);
        BattleUnitState eventTarget = BuildUnit("chain_event_target", "enemy", Vector2I.One);
        ApplyWeapon(eventSource, "sword", "melee", 1);
        AttackEffectResolutionResult damageResult = runtime
            .GetDamageResolver()
            .ResolveEffects(
                eventSource,
                eventTarget,
                skill.CombatProfile.EffectDefinitions,
                DamageResolutionContext.Empty()
            );
        _test.True(damageResult.Damage > 0, "三连斩伤害模板应产生正数damage数据。");
        _test.True(
            damageResult.DamageEvents.Length > 0,
            "三连斩伤害模板应产生正式DamageEvent数据。"
        );
    }

    private void TestMissDoesNotCancelRemainingAttacks(SkillDefinition skill)
    {
        using BattleRuntimeModule runtime = BuildRuntime(skill);
        StageOutcomeDamageResolver stageResolver = new();
        stageResolver.stage_successes.Add(false);
        stageResolver.stage_successes.Add(true);
        stageResolver.stage_successes.Add(true);
        stageResolver.stage_damage.Add(0);
        stageResolver.stage_damage.Add(7);
        stageResolver.stage_damage.Add(8);
        runtime.ConfigureDamageResolverForTests(stageResolver);
        (BattleUnitState caster, BattleUnitState target) = SetupDuel(runtime, targetHp: 100);

        int hpBefore = target.GetCurrentHp();
        BattleEventBatch batch = runtime.IssueCommand(BuildCommand(caster, target));

        _test.Eq(stageResolver.call_count, 3, "首段未命中后仍应完成剩余两段。");
        _test.True(ContainsLog(batch, "第 1 段未命中"), "首段应记录未命中。");
        _test.True(target.GetCurrentHp() < hpBefore, "后续命中应实际扣除目标HP。");
        _test.Eq(caster.GetCurrentStamina(), 70, "一失两中仍只应支付一次技能体力。");
    }

    private void TestKillStopsRemainingAttacks(SkillDefinition skill)
    {
        using BattleRuntimeModule runtime = BuildRuntime(skill);
        StageOutcomeDamageResolver stageResolver = new();
        stageResolver.stage_successes.Add(true);
        stageResolver.stage_successes.Add(true);
        stageResolver.stage_successes.Add(true);
        stageResolver.stage_damage.Add(10);
        stageResolver.stage_damage.Add(10);
        stageResolver.stage_damage.Add(10);
        runtime.ConfigureDamageResolverForTests(stageResolver);
        (BattleUnitState caster, BattleUnitState target) = SetupDuel(runtime, targetHp: 1);

        runtime.IssueCommand(BuildCommand(caster, target));

        _test.True(!target.IsAlive(), "第一段足以击杀时应正常击倒目标。");
        _test.Eq(stageResolver.call_count, 1, "第一段击杀后不应继续攻击尸体。");
    }

    private static SkillDefinition LoadSkill() =>
        TestSkillDefinitionProjection.LoadSkillDefinition(
            "res://data/configs/skills/warrior_chain_slashes.tres",
            "warrior_chain_slashes_regression"
        );

    private static BattleRuntimeModule BuildRuntime(SkillDefinition skill)
    {
        BattleRuntimeModule runtime = new();
        runtime.setup(
            null,
            new Dictionary<StringName, SkillDefinition> { [skill.SkillId] = skill }
        );
        return runtime;
    }

    private static (BattleUnitState caster, BattleUnitState target) SetupDuel(
        BattleRuntimeModule runtime,
        int targetHp
    )
    {
        BattleState state = BuildState();
        BattleUnitState caster = BuildUnit("chain_user", "player", new Vector2I(1, 1));
        BattleUnitState target = BuildUnit("chain_target", "enemy", new Vector2I(2, 1));
        target.SetCurrentHp(targetHp);
        target.attribute_snapshot.SetValue(AttributeService.HP_MAX, targetHp);
        caster.AddKnownActiveSkill(SkillId);
        caster.SetKnownSkillLevelTyped(SkillId, 0, preserveZero: true);
        ApplyWeapon(caster, "sword", "melee", 1);
        AddUnit(runtime, state, caster);
        AddUnit(runtime, state, target);
        state.active_unit_id = caster.unit_id;
        runtime.SetupStateForTests(state);
        return (caster, target);
    }

    private static BattleState BuildState()
    {
        BattleState state = new()
        {
            battle_id = "warrior_chain_slashes_regression",
            phase = "unit_acting",
            map_size = new Vector2I(4, 4),
            timeline = new BattleTimelineState(),
        };
        for (int y = 0; y < 4; y++)
        for (int x = 0; x < 4; x++)
        {
            Vector2I coord = new(x, y);
            BattleCellState cell = new()
            {
                coord = coord,
                base_terrain = BattleTerrainRules.ToStringName(BattleTerrainKind.Land),
                base_height = 4,
            };
            cell.RecalculateRuntimeValues();
            state.SetCell(coord, cell);
        }
        state.RebuildCellColumns();
        return state;
    }

    private static BattleUnitState BuildUnit(StringName id, StringName faction, Vector2I coord)
    {
        BattleUnitState unit = new BattleUnitState()
        {
            unit_id = id,
            display_name = id.ToString(),
            faction_id = faction,
        }.WithCombatResourcesForTest(
            hp: 100,
            stamina: 100,
            ap: 2,
            isAlive: true
        );
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        unit.attribute_snapshot.SetValue(AttributeService.STAMINA_MAX, 100);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 1);
        unit.SetAnchorCoord(coord);
        return unit;
    }

    private static void ApplyWeapon(BattleUnitState unit, StringName family, StringName rangeType, int range)
    {
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = "equipped",
                weapon_item_id = "chain_slashes_test_weapon",
                weapon_profile_type_id = "test_blade",
                weapon_range_type = rangeType,
                weapon_family = family,
                weapon_current_grip = "one_handed",
                weapon_attack_range = range,
                weapon_one_handed_dice = new WeaponDice
                {
                    dice_count = 1,
                    dice_sides = 6,
                    flat_bonus = 2,
                },
                weapon_two_handed_dice = new WeaponDice(),
                weapon_is_versatile = false,
                weapon_uses_two_hands = false,
                weapon_physical_damage_tag = "physical_slash",
            }
        );
    }

    private static void ClearWeapon(BattleUnitState unit)
    {
        unit.ClearWeaponProjection();
    }

    private static void AddUnit(BattleRuntimeModule runtime, BattleState state, BattleUnitState unit)
    {
        state.SetUnit(unit);
        runtime._grid_service.PlaceUnit(state, unit, unit.GetAnchorCoord(), true);
    }

    private static BattleCommand BuildCommand(BattleUnitState caster, BattleUnitState target) =>
        new()
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            unit_id = caster.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(SkillId),
            skill_id = SkillId,
            target_unit_id = target.unit_id,
            target_coord = target.GetAnchorCoord(),
        };

    private static CombatEffectDefinition FindFixedRepeatEffect(SkillDefinition skill)
    {
        foreach (
            CombatEffectDefinition effect in
                skill?.CombatProfile?.EffectDefinitions
                    ?? Array.Empty<CombatEffectDefinition>()
        )
            if (effect?.EffectKind == BattleEffectKind.FixedRepeatAttack)
                return effect;
        return null;
    }

    private static CombatEffectDefinition FindActiveDamageEffect(
        SkillDefinition skill,
        int level
    )
    {
        foreach (CombatEffectDefinition effect in skill.CombatProfile.EffectDefinitions)
        {
            if (
                effect?.EffectKind == BattleEffectKind.Damage
                && level >= effect.MinSkillLevel
                && (effect.MaxSkillLevel < 0 || level <= effect.MaxSkillLevel)
            )
                return effect;
        }
        return null;
    }

    private static bool ContainsLog(BattleEventBatch batch, string text)
    {
        if (batch == null)
            return false;
        foreach (string line in batch.log_lines)
            if (line.Contains(text))
                return true;
        return false;
    }
}
