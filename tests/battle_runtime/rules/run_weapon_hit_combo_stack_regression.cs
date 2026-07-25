using System;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_weapon_hit_combo_stack_regression : LifecycleTestSceneTree
{
    private static readonly StringName MeleeComboStackStatusId = "melee_combo_stack";
    private static readonly StringName RangedComboStackStatusId = "ranged_combo_stack";
    private static readonly StringName ObsoleteMixedComboStackStatusId = "combo_stack";
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestFormalBasicAttackGrantsStack();
            TestEquippedWeaponHitGrantsExactlyOneStack();
            TestRangedWeaponHitAlsoGrantsStack();
            TestWeaponHitWithNoAppliedDamageStillGrantsStack();
            TestNonWeaponAndUnarmedHitsDoNotGrantStack();
            TestMyriadBladesConsumesOnlyMeleeStacks();
            TestWeaponMissClearsExistingStacks();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Weapon hit combo stack regression"));
    }

    private void TestFormalBasicAttackGrantsStack()
    {
        SkillDefinition basicAttack = TestSkillDefinitionProjection.LoadSkillDefinition(
            "res://data/configs/skills/basic_attack.tres",
            "weapon_hit_combo_stack_regression"
        );
        _test.True(basicAttack?.CombatProfile != null, "应能加载正式基础攻击资源。");
        if (basicAttack?.CombatProfile == null)
        {
            return;
        }
        BattleDamageResolver resolver = BuildHitResolver();
        BattleUnitState source = BuildUnit("combo_basic_attack_source");
        BattleUnitState target = BuildUnit("combo_basic_attack_target", faction: "enemy");
        ApplyEquippedWeapon(source);

        resolver.ResolveAttackEffects(
            source,
            target,
            basicAttack.CombatProfile.EffectDefinitions,
            BuildAttackCheck()
        );

        _test.Eq(
            source.GetStatusEffect(MeleeComboStackStatusId)?.stacks ?? 0,
            1,
            "近战基础攻击应获得一层近战连击。"
        );
        _test.False(
            source.HasStatusEffect(ObsoleteMixedComboStackStatusId),
            "近战命中不应再生成混合来源的combo_stack。"
        );
    }

    private void TestEquippedWeaponHitGrantsExactlyOneStack()
    {
        BattleDamageResolver resolver = BuildHitResolver();
        BattleUnitState source = BuildUnit("combo_weapon_source");
        BattleUnitState target = BuildUnit("combo_weapon_target", faction: "enemy");
        target.SetCurrentHp(100000);
        target.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100000);
        ApplyEquippedWeapon(source);
        CombatEffectDefinition effect = BuildDamageEffect(addWeaponDice: true);

        resolver.ResolveAttackEffects(source, target, new[] { effect }, BuildAttackCheck());
        _test.Eq(
            source.GetStatusEffect(MeleeComboStackStatusId)?.stacks ?? 0,
            1,
            "一次近战武器命中应只获得一层近战连击。"
        );
        _test.Eq(
            source.GetStatusEffect(MeleeComboStackStatusId)?.duration ?? -1,
            180,
            "近战武器命中生成的连击层应持续180TU。"
        );

        resolver.ResolveAttackEffects(source, target, new[] { effect }, BuildAttackCheck());
        _test.Eq(
            source.GetStatusEffect(MeleeComboStackStatusId)?.stacks ?? 0,
            2,
            "连续两次近战武器命中应累计两层近战连击。"
        );

        for (int hitIndex = 2; hitIndex < 25; hitIndex++)
        {
            resolver.ResolveAttackEffects(source, target, new[] { effect }, BuildAttackCheck());
        }
        _test.Eq(
            source.GetStatusEffect(MeleeComboStackStatusId)?.stacks ?? 0,
            25,
            "近战连击不应再受20层玩法上限限制。"
        );
        _test.Eq(
            source.GetStatusEffect(MeleeComboStackStatusId)?.stack_limit ?? -1,
            0,
            "近战连击应使用stack_limit=0的无上限语义。"
        );
    }

    private void TestRangedWeaponHitAlsoGrantsStack()
    {
        BattleDamageResolver resolver = BuildHitResolver();
        BattleUnitState source = BuildUnit("combo_bow_source");
        BattleUnitState target = BuildUnit("combo_bow_target", faction: "enemy");
        ApplyEquippedWeapon(
            source,
            family: "bow",
            rangeType: "ranged",
            attackRange: 6,
            damageTag: "physical_pierce"
        );

        resolver.ResolveAttackEffects(
            source,
            target,
            new[] { BuildDamageEffect(addWeaponDice: true) },
            BuildAttackCheck()
        );

        _test.Eq(
            source.GetStatusEffect(RangedComboStackStatusId)?.stacks ?? 0,
            1,
            "弓等远程装备武器命中应获得一层远程连击。"
        );
        _test.Eq(
            source.GetStatusEffect(MeleeComboStackStatusId)?.stacks ?? 0,
            0,
            "弓命中不应生成近战连击层。"
        );
        _test.False(
            source.HasStatusEffect(ObsoleteMixedComboStackStatusId),
            "远程命中不应再生成混合来源的combo_stack。"
        );
    }

    private void TestWeaponHitWithNoAppliedDamageStillGrantsStack()
    {
        BattleDamageResolver resolver = BuildHitResolver();
        resolver.SetDamageApplicationHook(
            new StaticDamageHook(_ => BattleDamageApplicationHookResult.Cancel())
        );
        BattleUnitState source = BuildUnit("combo_zero_damage_source");
        BattleUnitState target = BuildUnit("combo_zero_damage_target", faction: "enemy");
        ApplyEquippedWeapon(source);
        int hpBefore = target.GetCurrentHp();

        AttackEffectResolutionResult result = resolver.ResolveAttackEffects(
            source,
            target,
            new[] { BuildDamageEffect(addWeaponDice: true) },
            BuildAttackCheck()
        );

        _test.True(result.AttackSuccess, "伤害被取消前，武器攻击应已经成功命中。");
        _test.Eq(result.Damage, 0, "测试夹具应使最终生命伤害为0。");
        _test.Eq(target.GetCurrentHp(), hpBefore, "最终生命伤害为0时不应改变目标HP。");
        _test.Eq(
            source.GetStatusEffect(MeleeComboStackStatusId)?.stacks ?? 0,
            1,
            "近战武器命中即应累计近战连击，不应要求最终造成生命或护盾伤害。"
        );
    }

    private void TestNonWeaponAndUnarmedHitsDoNotGrantStack()
    {
        BattleDamageResolver resolver = BuildHitResolver();
        BattleUnitState spellSource = BuildUnit("combo_spell_source");
        BattleUnitState spellTarget = BuildUnit("combo_spell_target", faction: "enemy");
        resolver.ResolveAttackEffects(
            spellSource,
            spellTarget,
            new[] { BuildDamageEffect(addWeaponDice: false) },
            BuildAttackCheck()
        );
        _test.False(
            spellSource.HasStatusEffect(MeleeComboStackStatusId)
                || spellSource.HasStatusEffect(RangedComboStackStatusId),
            "不包含武器伤害的命中不应生成任何武器连击层。"
        );

        BattleUnitState unarmedSource = BuildUnit("combo_unarmed_source");
        BattleUnitState unarmedTarget = BuildUnit("combo_unarmed_target", faction: "enemy");
        ApplyUnarmedProfile(unarmedSource);
        resolver.ResolveAttackEffects(
            unarmedSource,
            unarmedTarget,
            new[] { BuildDamageEffect(addWeaponDice: true) },
            BuildAttackCheck()
        );
        _test.False(
            unarmedSource.HasStatusEffect(MeleeComboStackStatusId)
                || unarmedSource.HasStatusEffect(RangedComboStackStatusId),
            "空手攻击不属于已装备武器命中，不应生成任何武器连击层。"
        );
    }

    private void TestMyriadBladesConsumesOnlyMeleeStacks()
    {
        SkillDefinition skill = TestSkillDefinitionProjection.LoadSkillDefinition(
            "res://data/configs/skills/warrior_myriad_blades_unity.tres",
            "weapon_hit_combo_stack_regression"
        );
        _test.True(skill?.CombatProfile != null, "应能加载万刃归一正式资源。");
        if (skill?.CombatProfile == null)
            return;
        _test.Eq(skill.CombatProfile.AuraCost, 1000, "万刃归一应固定消耗1000斗气。");
        _test.Eq(skill.CombatProfile.GetEffectiveAttackRollBonus(0), 2, "0级攻击检定应+2。");
        _test.Eq(skill.CombatProfile.GetEffectiveAttackRollBonus(5), 3, "5级攻击检定应+3。");
        _test.Eq(skill.CombatProfile.GetEffectiveAttackRollBonus(8), 4, "8级攻击检定应+4。");
        _test.Eq(
            skill.CombatProfile.AttackRollBonusStatusId,
            MeleeComboStackStatusId,
            "万刃归一攻击加成应读取近战连击层。"
        );
        _test.Eq(
            skill.CombatProfile.AttackRollBonusStatusStackDivisor,
            5,
            "万刃归一应每5层近战连击获得+1攻击检定。"
        );
        _test.Eq(
            skill.CombatProfile.GetEffectiveAttackResolutionMode(9),
            CombatSkillAttackResolutionMode.Auto,
            "9级万刃归一仍应进行普通攻击检定。"
        );
        _test.Eq(
            skill.CombatProfile.GetEffectiveAttackResolutionMode(10),
            CombatSkillAttackResolutionMode.ForceHitNoCrit,
            "10级万刃归一应切换为必中且不可重击。"
        );
        CombatEffectDefinition damageEffect = null;
        foreach (CombatEffectDefinition effect in skill.CombatProfile.EffectDefinitions)
        {
            if (effect?.EffectKind != BattleEffectKind.Damage)
                continue;
            _test.Eq(
                effect.ConsumedStatusId,
                MeleeComboStackStatusId,
                "万刃归一所有等级伤害效果都应消费独立的近战连击状态。"
            );
            if (effect.MinSkillLevel == 0)
                damageEffect = effect;
        }
        _test.True(damageEffect != null, "万刃归一应存在0级伤害效果。");
        if (damageEffect == null)
            return;

        FixedMaxDamageResolver resolver = new();
        resolver.SetHitResolver(new FixedHitResolver(10));
        BattleUnitState emptyStackSource = BuildUnit("myriad_empty_stack_source");
        BattleUnitState emptyStackTarget = BuildUnit(
            "myriad_empty_stack_target",
            faction: "enemy"
        );
        ApplyEquippedWeapon(emptyStackSource);
        AttackEffectResolutionResult emptyStackResult = resolver.ResolveAttackEffects(
            emptyStackSource,
            emptyStackTarget,
            new[] { damageEffect },
            BuildAttackCheck()
        );
        _test.Eq(
            emptyStackResult.Damage,
            22,
            "万刃归一在没有近战连击层时仍应使用武器骰+2D8造成基础伤害。"
        );

        BattleUnitState source = BuildUnit("myriad_melee_source");
        BattleUnitState target = BuildUnit("myriad_melee_target", faction: "enemy");
        ApplyEquippedWeapon(source);
        source.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = RangedComboStackStatusId,
                source_unit_id = source.unit_id,
                stack_behavior = "add",
                stack_limit = 20,
                power = 2,
                stacks = 2,
                duration = 180,
            }
        );
        source.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = MeleeComboStackStatusId,
                source_unit_id = source.unit_id,
                stack_behavior = "add",
                stack_limit = 20,
                power = 3,
                stacks = 3,
                duration = 180,
            }
        );

        resolver.PreviewDamageEffectTyped(
            source,
            target,
            damageEffect,
            DamageResolutionContext.Empty(),
            BattleDamagePreviewRollMode.Maximum,
            BattleDamagePreviewSaveMode.Expected
        );
        _test.Eq(
            source.GetStatusEffect(RangedComboStackStatusId)?.stacks ?? 0,
            2,
            "万刃归一伤害预览不应修改真实远程连击层。"
        );
        _test.Eq(
            source.GetStatusEffect(MeleeComboStackStatusId)?.stacks ?? 0,
            3,
            "万刃归一伤害预览不应消费真实近战连击层。"
        );

        AttackEffectResolutionResult result = resolver.ResolveAttackEffects(
            source,
            target,
            new[] { damageEffect },
            BuildAttackCheck()
        );

        _test.Eq(result.Damage, 40, "万刃归一应只按3层近战来源追加3D6伤害。");
        _test.Eq(
            source.GetStatusEffect(RangedComboStackStatusId)?.stacks ?? 0,
            2,
            "万刃归一不应消费远程连击层。"
        );
        _test.Eq(
            source.GetStatusEffect(MeleeComboStackStatusId)?.stacks ?? 0,
            1,
            "万刃归一应消耗原有3层近战连击，并由自身命中重新获得1层。"
        );
    }

    private void TestWeaponMissClearsExistingStacks()
    {
        BattleDamageResolver resolver = new();
        resolver.SetHitResolver(new FixedMissResolver());
        BattleUnitState source = BuildUnit("combo_miss_source");
        BattleUnitState target = BuildUnit("combo_miss_target", faction: "enemy");
        ApplyEquippedWeapon(source);
        source.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = RangedComboStackStatusId,
                source_unit_id = source.unit_id,
                stack_behavior = "add",
                stack_limit = 20,
                power = 2,
                stacks = 2,
                duration = 180,
            }
        );
        source.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = MeleeComboStackStatusId,
                source_unit_id = source.unit_id,
                stack_behavior = "add",
                stack_limit = 20,
                power = 3,
                stacks = 3,
                duration = 180,
            }
        );

        AttackEffectResolutionResult result = resolver.ResolveAttackEffects(
            source,
            target,
            new[] { BuildDamageEffect(addWeaponDice: true) },
            BuildAttackCheck()
        );

        _test.False(result.AttackSuccess, "测试夹具应返回武器攻击未命中。");
        _test.False(
            source.HasStatusEffect(RangedComboStackStatusId),
            "任意武器攻击未命中后应清空远程连击层。"
        );
        _test.False(
            source.HasStatusEffect(MeleeComboStackStatusId),
            "任意武器攻击未命中后应同时清空近战连击层。"
        );
    }

    private static BattleDamageResolver BuildHitResolver()
    {
        BattleDamageResolver resolver = new();
        resolver.SetHitResolver(new FixedHitResolver(10));
        return resolver;
    }

    private static CombatEffectDefinition BuildDamageEffect(bool addWeaponDice)
    {
        using CombatEffectDef resource = new()
        {
            effect_type = "damage",
            power = 1,
            add_weapon_dice = addWeaponDice,
            use_weapon_physical_damage_tag = addWeaponDice,
            damage_tag = addWeaponDice ? new StringName("") : new StringName("force"),
            @params = new GDictionary(),
        };
        return CombatEffectDefinition.FromResource(
            resource,
            "weapon_hit_combo_stack_regression"
        );
    }

    private static AttackCheckInput BuildAttackCheck() =>
        new(requiredRoll: 10, displayRequiredRoll: 10);

    private static BattleUnitState BuildUnit(StringName id, StringName faction = default)
    {
        BattleUnitState unit = new BattleUnitState()
        {
            unit_id = id,
            display_name = id.ToString(),
            faction_id = faction == default ? new StringName("player") : faction,
        }.WithCombatResourcesForTest(
            hp: 100,
            stamina: 100,
            ap: 2,
            isAlive: true
        );
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 1);
        return unit;
    }

    private static void ApplyEquippedWeapon(BattleUnitState unit) =>
        ApplyEquippedWeapon(
            unit,
            family: "sword",
            rangeType: "melee",
            attackRange: 1,
            damageTag: "physical_slash"
        );

    private static void ApplyEquippedWeapon(
        BattleUnitState unit,
        StringName family,
        StringName rangeType,
        int attackRange,
        StringName damageTag
    )
    {
        bool usesTwoHands = rangeType == "ranged";
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = "equipped",
                weapon_item_id = usesTwoHands
                    ? "combo_test_bow"
                    : "combo_test_sword",
                weapon_profile_type_id = usesTwoHands ? "longbow" : "longsword",
                weapon_range_type = rangeType,
                weapon_family = family,
                weapon_current_grip = usesTwoHands ? "two_handed" : "one_handed",
                weapon_attack_range = attackRange,
                weapon_one_handed_dice = usesTwoHands
                    ? new WeaponDice()
                    : new WeaponDice
                    {
                        dice_count = 1,
                        dice_sides = 6,
                        flat_bonus = 0,
                    },
                weapon_two_handed_dice = usesTwoHands
                    ? new WeaponDice
                    {
                        dice_count = 1,
                        dice_sides = 8,
                        flat_bonus = 0,
                    }
                    : new WeaponDice(),
                weapon_is_versatile = false,
                weapon_uses_two_hands = usesTwoHands,
                weapon_physical_damage_tag = damageTag,
            }
        );
    }

    private static void ApplyUnarmedProfile(BattleUnitState unit)
    {
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = "unarmed",
                weapon_item_id = "",
                weapon_profile_type_id = "unarmed",
                weapon_range_type = "melee",
                weapon_family = "unarmed",
                weapon_current_grip = "one_handed",
                weapon_attack_range = 1,
                weapon_one_handed_dice = new WeaponDice
                {
                    dice_count = 1,
                    dice_sides = 4,
                    flat_bonus = 0,
                },
                weapon_two_handed_dice = new WeaponDice(),
                weapon_is_versatile = false,
                weapon_uses_two_hands = false,
                weapon_physical_damage_tag = "physical_blunt",
            }
        );
    }

    private sealed class FixedMissResolver : FixedHitResolver
    {
        public override AttackResolutionMetadata ResolveAttackMetadata(
            BattleUnitState sourceUnit,
            BattleUnitState targetUnit,
            AttackCheckInput attackCheck,
            AttackContext attackContext
        ) =>
            BuildFixedAttackMetadata(
                attackCheck,
                attackContext,
                AttackResolutionCriticalFail,
                attackSuccess: false,
                criticalHit: false,
                ordinaryMiss: false
            );
    }

    private sealed class FixedMaxDamageResolver : BattleDamageResolver
    {
        public override int _roll_damage_die(int diceSides) => Math.Max(diceSides, 1);
    }

    private sealed class StaticDamageHook : IBattleDamageApplicationHook
    {
        private readonly Func<
            BattleDamageApplicationHookContext,
            BattleDamageApplicationHookResult
        > _handler;

        internal StaticDamageHook(
            Func<
                BattleDamageApplicationHookContext,
                BattleDamageApplicationHookResult
            > handler
        )
        {
            _handler = handler;
        }

        public BattleDamageApplicationHookResult BeforeDamageResolved(
            BattleDamageApplicationHookContext context
        ) =>
            _handler?.Invoke(context) ?? BattleDamageApplicationHookResult.None;
    }
}
