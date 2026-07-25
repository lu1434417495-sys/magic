using System;
using System.Linq;
using Godot;

public partial class run_warrior_perfect_rhythm_regression : LifecycleTestSceneTree
{
    private static readonly StringName SkillId = "warrior_perfect_rhythm";
    private static readonly StringName RhythmStatusId = "perfect_rhythm";
    private static readonly StringName DisruptedStatusId = "rhythm_disrupted";
    private static readonly StringName MeleeComboStatusId = "melee_combo_stack";
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestAuthoredContract();
            TestMaintainedStatusSnapshotRoundTrip();
            TestMeleeHitStackGainByLevel();
            TestComboAttackBonusOnlyAppliesToMeleeWeaponAttacks();
            TestUpkeepEscalationBoundaries();
            TestInsufficientAuraTerminatesWithPenaltyAndCooldown();
            TestHardControlTerminatesWithPenaltyAndCooldown();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Warrior perfect rhythm regression"));
    }

    private void TestAuthoredContract()
    {
        SkillDefinition skill = LoadSkill();
        _test.True(skill?.CombatProfile != null, "应能加载完美节奏正式技能资源。");
        if (skill?.CombatProfile == null)
            return;
        _test.Eq(skill.CombatProfile.ApCost, 1, "启动完美节奏应消耗1AP。");
        _test.Eq(skill.CombatProfile.AuraCost, 200, "启动完美节奏应消耗200斗气。");
        _test.Eq(skill.CombatProfile.CooldownTu, 0, "启动时不应立刻进入冷却。");

        CombatEffectDefinition low = FindEffect(skill, 0);
        CombatEffectDefinition max = FindEffect(skill, 7);
        AssertMaintainedEffect(low, 1, "0–6级");
        AssertMaintainedEffect(max, 2, "7级");
    }

    private void TestMeleeHitStackGainByLevel()
    {
        SkillDefinition skill = LoadSkill();
        BattleDamageResolver resolver = new();
        resolver.SetHitResolver(new FixedHitResolver(10));
        CombatEffectDefinition weaponDamage = BuildWeaponDamageEffect();

        BattleUnitState levelZero = BuildUnit("perfect_rhythm_level_zero");
        ApplyMeleeWeapon(levelZero);
        ApplyRhythm(levelZero, FindEffect(skill, 0));
        resolver.ResolveAttackEffects(
            levelZero,
            BuildUnit("perfect_rhythm_target_0", "enemy"),
            new[] { weaponDamage },
            BuildAttackCheck()
        );
        _test.Eq(
            levelZero.GetStatusEffect(MeleeComboStatusId)?.stacks ?? 0,
            2,
            "0–6级完美节奏下，一次近战武器命中应总计获得2层。"
        );

        BattleUnitState levelSeven = BuildUnit("perfect_rhythm_level_seven");
        ApplyMeleeWeapon(levelSeven);
        ApplyRhythm(levelSeven, FindEffect(skill, 7));
        resolver.ResolveAttackEffects(
            levelSeven,
            BuildUnit("perfect_rhythm_target_7", "enemy"),
            new[] { weaponDamage },
            BuildAttackCheck()
        );
        _test.Eq(
            levelSeven.GetStatusEffect(MeleeComboStatusId)?.stacks ?? 0,
            3,
            "7级完美节奏下，一次近战武器命中应总计获得3层。"
        );
    }

    private void TestMaintainedStatusSnapshotRoundTrip()
    {
        BattleUnitState unit = BuildUnit("perfect_rhythm_snapshot");
        ApplyRhythm(unit, FindEffect(LoadSkill(), 7));
        BattleStatusEffectState source = unit.GetStatusEffect(RhythmStatusId);
        source.upkeep_elapsed_tu = 65;
        using GodotProjectionLease<Godot.Collections.Dictionary> lease =
            source.ToDictionaryLease();
        BattleStatusEffectState restored = BattleStatusEffectState.FromDictionary(
            lease.Value
        );
        _test.True(restored != null, "完美节奏状态应能通过正式快照契约往返。");
        _test.Eq(restored?.melee_combo_stack_gain_bonus ?? -1, 2, "快照应保留7级额外层数。");
        _test.Eq(restored?.upkeep_elapsed_tu ?? -1, 65, "快照应保留维持时间。");
        _test.Eq(
            restored?.termination_attack_roll_penalty ?? -1,
            3,
            "快照应保留终止惩罚。"
        );
    }

    private void TestComboAttackBonusOnlyAppliesToMeleeWeaponAttacks()
    {
        SkillDefinition rhythm = LoadSkill();
        SkillDefinition basicAttack = TestSkillDefinitionProjection.LoadSkillDefinition(
            "res://data/configs/skills/basic_attack.tres",
            "warrior_perfect_rhythm_regression"
        );
        SkillDefinition spell = TestSkillDefinitionProjection.LoadSkillDefinition(
            "res://data/configs/skills/mage_arcane_missile.tres",
            "warrior_perfect_rhythm_regression"
        );
        BattleUnitState source = BuildUnit("perfect_rhythm_attack_bonus");
        BattleUnitState target = BuildUnit("perfect_rhythm_attack_bonus_target", "enemy");
        target.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 20);
        ApplyMeleeWeapon(source);
        ApplyRhythm(source, FindEffect(rhythm, 7));
        source.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = MeleeComboStatusId,
                source_unit_id = source.unit_id,
                power = 20,
                stacks = 20,
                duration = 180,
            }
        );
        BattleHitResolver hitResolver = new();
        AttackCheckInput meleeCheck = hitResolver.BuildSkillAttackCheck(
            new BattleUnitReadView(source),
            new BattleUnitReadView(target),
            basicAttack
        );

        source.EraseStatusEffect(RhythmStatusId);
        AttackCheckInput baseMeleeCheck = hitResolver.BuildSkillAttackCheck(
            new BattleUnitReadView(source),
            new BattleUnitReadView(target),
            basicAttack
        );
        _test.Eq(
            meleeCheck.RequiredRoll,
            baseMeleeCheck.RequiredRoll - 2,
            "20层近战连击应使近战武器攻击检定+2。"
        );

        ApplyRhythm(source, FindEffect(rhythm, 7));
        AttackCheckInput spellCheck = hitResolver.BuildSkillAttackCheck(
            new BattleUnitReadView(source),
            new BattleUnitReadView(target),
            spell
        );
        source.EraseStatusEffect(RhythmStatusId);
        AttackCheckInput baseSpellCheck = hitResolver.BuildSkillAttackCheck(
            new BattleUnitReadView(source),
            new BattleUnitReadView(target),
            spell
        );
        _test.Eq(
            spellCheck.RequiredRoll,
            baseSpellCheck.RequiredRoll,
            "即使装备近战武器，非武器法术也不应获得完美节奏攻击加成。"
        );
    }

    private void TestUpkeepEscalationBoundaries()
    {
        BattleRuntimeModule runtime = BuildRuntime(out BattleState state);
        BattleUnitState unit = BuildUnit("perfect_rhythm_upkeep");
        unit.SetCurrentAura(1000);
        state.SetUnit(unit);
        ApplyRhythm(unit, FindEffect(LoadSkill(), 7));
        runtime._skill_turn_resolver.InitializeAppliedStatusTimelineTicks(
            unit,
            new[] { RhythmStatusId }
        );

        AdvanceTicks(runtime, state, unit, 60);
        _test.Eq(unit.GetCurrentAura(), 880, "5–60TU的12次维持应各消耗10斗气。");
        AdvanceTicks(runtime, state, unit, 5);
        _test.Eq(unit.GetCurrentAura(), 860, "65TU的首次第二档维持应消耗20斗气。");
        AdvanceTicks(runtime, state, unit, 55);
        _test.Eq(unit.GetCurrentAura(), 640, "70–120TU仍应每次消耗20斗气。");
        AdvanceTicks(runtime, state, unit, 5);
        _test.Eq(unit.GetCurrentAura(), 600, "125TU的首次第三档维持应消耗40斗气。");
        runtime.Dispose();
    }

    private void TestInsufficientAuraTerminatesWithPenaltyAndCooldown()
    {
        BattleRuntimeModule runtime = BuildRuntime(out BattleState state);
        BattleUnitState unit = BuildUnit("perfect_rhythm_no_aura");
        unit.SetCurrentAura(9);
        state.SetUnit(unit);
        ApplyRhythm(unit, FindEffect(LoadSkill(), 7));
        runtime._skill_turn_resolver.InitializeAppliedStatusTimelineTicks(
            unit,
            new[] { RhythmStatusId }
        );

        AdvanceTicks(runtime, state, unit, 5);
        AssertTermination(unit, "斗气不足");
        runtime.Dispose();
    }

    private void TestHardControlTerminatesWithPenaltyAndCooldown()
    {
        BattleRuntimeModule runtime = BuildRuntime(out _);
        BattleUnitState unit = BuildUnit("perfect_rhythm_hard_control");
        ApplyRhythm(unit, FindEffect(LoadSkill(), 7));
        unit.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "stunned",
                source_unit_id = "enemy",
                power = 1,
                stacks = 1,
                duration = 20,
            }
        );
        runtime._skill_turn_resolver.InitializeAppliedStatusTimelineTicks(
            unit,
            new[] { RhythmStatusId, new StringName("stunned") }
        );
        AssertTermination(unit, "硬控");
        runtime.Dispose();
    }

    private void AssertTermination(BattleUnitState unit, string reason)
    {
        _test.False(unit.HasStatusEffect(RhythmStatusId), $"{reason}后应移除完美节奏。");
        BattleStatusEffectState penalty = unit.GetStatusEffect(DisruptedStatusId);
        _test.True(penalty != null, $"{reason}后应施加节奏崩解。");
        _test.Eq(penalty?.duration ?? -1, 90, "节奏崩解应持续90TU。");
        _test.Eq(
            penalty?.attack_roll_penalty ?? -1,
            3,
            "节奏崩解应使所有攻击检定-3。"
        );
        _test.Eq(unit.GetCooldownTyped(SkillId), 120, "终止后应进入120TU冷却。");
    }

    private void AssertMaintainedEffect(
        CombatEffectDefinition effect,
        int expectedBonus,
        string label
    )
    {
        _test.True(effect != null, $"{label}应有完美节奏状态效果。");
        if (effect == null)
            return;
        _test.Eq(effect.DurationTu, 0, $"{label}状态应为无限持续。");
        _test.Eq(
            effect.MeleeComboStackGainBonus,
            expectedBonus,
            $"{label}近战命中额外层数不正确。"
        );
        _test.Eq(effect.ComboAttackBonusStatusId, MeleeComboStatusId, "攻击加成应读取近战层。");
        _test.Eq(effect.ComboAttackBonusStackDivisor, 10, "应每10层攻击检定+1。");
        _test.Eq(effect.UpkeepIntervalTu, 5, "应每5TU结算维持费用。");
        _test.Eq(effect.UpkeepBaseCost, 10, "首个60TU应每次消耗10斗气。");
        _test.Eq(effect.UpkeepEscalationIntervalTu, 60, "维持费用应每60TU升档。");
        _test.Eq(effect.UpkeepCostMultiplier, 2, "每档维持费用应翻倍。");
        _test.True(effect.BreakOnHardControl, "硬控应终止完美节奏。");
    }

    private static SkillDefinition LoadSkill() =>
        TestSkillDefinitionProjection.LoadSkillDefinition(
            "res://data/configs/skills/warrior_perfect_rhythm.tres",
            "warrior_perfect_rhythm_regression"
        );

    private static CombatEffectDefinition FindEffect(SkillDefinition skill, int level) =>
        skill?.CombatProfile?.EffectDefinitions.FirstOrDefault(
            effect =>
                effect != null
                && level >= effect.MinSkillLevel
                && (effect.MaxSkillLevel < 0 || level <= effect.MaxSkillLevel)
        );

    private static void ApplyRhythm(
        BattleUnitState unit,
        CombatEffectDefinition effect
    )
    {
        BattleStatusEffectState status = BattleStatusSemanticTable.MergeStatus(
            effect,
            unit.unit_id,
            unit.GetStatusEffect(RhythmStatusId),
            RhythmStatusId
        );
        status.source_skill_id = SkillId;
        unit.SetStatusEffect(status);
    }

    private static BattleRuntimeModule BuildRuntime(out BattleState state)
    {
        BattleRuntimeModule runtime = new();
        runtime.setup();
        state = new BattleState
        {
            battle_id = "perfect_rhythm_regression",
            timeline = new BattleTimelineState { current_tu = 0 },
        };
        runtime.SetupStateForTests(state);
        return runtime;
    }

    private static void AdvanceTicks(
        BattleRuntimeModule runtime,
        BattleState state,
        BattleUnitState unit,
        int elapsedTu
    )
    {
        state.timeline.current_tu += elapsedTu;
        runtime._skill_turn_resolver.ApplyUnitStatusPeriodicTicksResult(
            unit,
            elapsedTu,
            null
        );
    }

    private static CombatEffectDefinition BuildWeaponDamageEffect()
    {
        using CombatEffectDef effect = new()
        {
            effect_type = "damage",
            power = 1,
            add_weapon_dice = true,
            requires_weapon = true,
            use_weapon_physical_damage_tag = true,
        };
        return CombatEffectDefinition.FromResource(
            effect,
            "warrior_perfect_rhythm_regression.weapon_damage"
        );
    }

    private static AttackCheckInput BuildAttackCheck() =>
        new(requiredRoll: 10, displayRequiredRoll: 10);

    private static BattleUnitState BuildUnit(StringName id, StringName faction = default)
    {
        BattleUnitState unit = new BattleUnitState
        {
            unit_id = id,
            display_name = id.ToString(),
            faction_id = faction == default ? new StringName("player") : faction,
        }.WithCombatResourcesForTest(
            hp: 100,
            stamina: 100,
            aura: 2000,
            ap: 2,
            isAlive: true
        );
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 10);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
        return unit;
    }

    private static void ApplyMeleeWeapon(BattleUnitState unit)
    {
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = "equipped",
                weapon_item_id = "perfect_rhythm_test_sword",
                weapon_profile_type_id = "longsword",
                weapon_range_type = "melee",
                weapon_family = "sword",
                weapon_current_grip = "one_handed",
                weapon_attack_range = 1,
                weapon_one_handed_dice = new WeaponDice
                {
                    dice_count = 1,
                    dice_sides = 6,
                },
                weapon_physical_damage_tag = "physical_slash",
            }
        );
    }
}
