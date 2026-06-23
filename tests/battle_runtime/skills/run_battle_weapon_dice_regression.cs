using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_battle_weapon_dice_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        RunCase("TestAddWeaponDiceExplicitFormula", TestAddWeaponDiceExplicitFormula);
        RunCase("TestLegacySkillDiceAliasesAreNotUsed", TestLegacySkillDiceAliasesAreNotUsed);
        RunCase("TestPhysicalDamageDoesNotAddWeaponDiceByDefault", TestPhysicalDamageDoesNotAddWeaponDiceByDefault);
        RunCase("TestCriticalHitRollsExtraWeaponAndSkillDiceOnce", TestCriticalHitRollsExtraWeaponAndSkillDiceOnce);
        RunCase("TestEachDamageEffectReadsAddWeaponDiceIndependently", TestEachDamageEffectReadsAddWeaponDiceIndependently);
        RunCase("TestCurrentTwoHandedWeaponDiceIsUsed", TestCurrentTwoHandedWeaponDiceIsUsed);
        RunCase("TestVersatileCurrentGripSelectsActiveDice", TestVersatileCurrentGripSelectsActiveDice);
        RunCase("TestUnarmedAndNaturalWeaponDiceFeedAddWeaponDice", TestUnarmedAndNaturalWeaponDiceFeedAddWeaponDice);
        RunCase("TestRequiresWeaponGateAcceptsEquippedOnly", TestRequiresWeaponGateAcceptsEquippedOnly);
        RunCase("TestNaturalWeaponDiceDoNotTriggerSkillMastery", TestNaturalWeaponDiceDoNotTriggerSkillMastery);
        RunCase("TestDiceEventFieldsSplitByDiceGroup", TestDiceEventFieldsSplitByDiceGroup);
        RunCase("TestDiceEventFieldsStayFalseWithoutDiceGroups", TestDiceEventFieldsStayFalseWithoutDiceGroups);
        RunCase("TestWarriorHeavyStrikeUsesWeaponPlusSkillDiceTemplate", TestWarriorHeavyStrikeUsesWeaponPlusSkillDiceTemplate);

        return _test.Finish("Battle weapon dice regression");
    }

    private void RunCase(string name, Action test)
    {
        test.Invoke();
    }

    private void TestAddWeaponDiceExplicitFormula()
    {
        var resolver = BuildFixedRollDamageResolver(new[] { 2, 3, 6 });
        BattleUnitState source = BuildUnit("weapon_formula_user");
        ApplyWeapon(source, 1, 6, 2);
        BattleUnitState target = BuildUnit("weapon_formula_target");
        CombatEffectDef effect = BuildDamageEffect(5, true, 2, 4, 3);

        GDictionary result = AttackEffectResolutionResultReader.BuildGodotPayload(resolver.ResolveEffects(source, target, new GArray { effect }, new GDictionary()));
        GDictionary damageEvent = FirstDamageEvent(result);
        _test.Eq(
            DictInt(damageEvent, "base_damage", 0),
            21,
            "add_weapon_dice 公式应为 weapon dice + skill dice + skill bonus + power。"
        );
        _test.Eq(
            DictInt(damageEvent, "weapon_damage_dice_total", 0),
            6,
            "武器骰应加入当前 damage event。"
        );
        _test.Eq(
            DictInt(damageEvent, "weapon_damage_dice_bonus", 0),
            2,
            "武器骰 flat_bonus 应只作为普通武器骰静态项加入。"
        );
        _test.Eq(
            DictInt(damageEvent, "damage_dice_total", 0),
            5,
            "技能骰应保留为 damage_dice_total。"
        );
        _test.Eq(
            DictInt(damageEvent, "damage_dice_bonus", 0),
            3,
            "技能骰 bonus 应加入基础伤害。"
        );
        _test.Eq(
            DictInt(result, "damage", 0),
            21,
            "无减伤时总 HP 伤害应等于 base_damage。"
        );
    }

    private void TestLegacySkillDiceAliasesAreNotUsed()
    {
        var resolver = BuildFixedRollDamageResolver(new[] { 6, 6, 6 });
        BattleUnitState source = BuildUnit("legacy_dice_alias_user");
        BattleUnitState target = BuildUnit("legacy_dice_alias_target");
        CombatEffectDef effect = BuildDamageEffect(5, false);
        effect.@params["damage_dice_count"] = 3;
        effect.@params["damage_dice_sides"] = 6;
        effect.@params["damage_dice_bonus"] = 9;

        GDictionary result = AttackEffectResolutionResultReader.BuildGodotPayload(resolver.ResolveEffects(source, target, new GArray { effect }, new GDictionary()));
        GDictionary damageEvent = FirstDamageEvent(result);
        _test.Eq(
            DictInt(damageEvent, "base_damage", 0),
            5,
            "旧技能骰 alias 不应加入真实伤害。"
        );
        _test.Eq(
            DictInt(damageEvent, "damage_dice_count", -1),
            0,
            "旧 damage_dice_count alias 不应再被读取。"
        );
        _test.Eq(
            DictInt(damageEvent, "damage_dice_sides", -1),
            0,
            "旧 damage_dice_sides alias 不应再被读取。"
        );
        _test.Eq(
            DictInt(damageEvent, "damage_dice_bonus", -1),
            0,
            "旧 damage_dice_bonus alias 不应再被读取。"
        );
    }

    private void TestPhysicalDamageDoesNotAddWeaponDiceByDefault()
    {
        var resolver = BuildFixedRollDamageResolver(new[] { 4, 6 });
        BattleUnitState source = BuildUnit("physical_default_user");
        ApplyWeapon(source, 1, 6, 2);
        BattleUnitState target = BuildUnit("physical_default_target");
        CombatEffectDef effect = BuildDamageEffect(5, false, 1, 4, 1, "physical_slash");

        GDictionary result = AttackEffectResolutionResultReader.BuildGodotPayload(resolver.ResolveEffects(source, target, new GArray { effect }, new GDictionary()));
        GDictionary damageEvent = FirstDamageEvent(result);
        _test.Eq(
            DictInt(damageEvent, "base_damage", 0),
            10,
            "物理伤害默认不应自动加入武器骰。"
        );
        _test.Eq(
            DictInt(damageEvent, "weapon_damage_dice_count", 0),
            0,
            "未显式 add_weapon_dice 时不应掷武器骰。"
        );
        _test.True(
            !DictBool(damageEvent, "add_weapon_dice", false),
            "未显式配置时 add_weapon_dice 应为 false。"
        );
    }

    private void TestCriticalHitRollsExtraWeaponAndSkillDiceOnce()
    {
        var resolver = BuildFixedRollDamageResolver(new[] { 2, 5, 3, 4 }, new[] { 20 });
        BattleUnitState source = BuildUnit("critical_weapon_user");
        ApplyWeapon(source, 1, 6, 2);
        BattleUnitState target = BuildUnit("critical_weapon_target");
        CombatEffectDef effect = BuildDamageEffect(7, true, 1, 4, 3);

        GDictionary result = AttackEffectResolutionResultReader.BuildGodotPayload(resolver.ResolveEffects(
            source,
            target,
            new GArray { effect },
            new GDictionary { ["critical_hit"] = true }
        ));
        GDictionary damageEvent = FirstDamageEvent(result);
        _test.True(
            DictBool(damageEvent, "critical_hit", false),
            "damage event 应记录本段来自暴击。"
        );
        _test.Eq(
            DictInt(damageEvent, "base_damage", 0),
            26,
            "暴击应额外掷一组 weapon dice 与 skill dice，但不翻倍 power 或 bonus。"
        );
        _test.Eq(
            DictInt(damageEvent, "critical_extra_damage_dice_total", 0),
            3,
            "暴击应额外掷一组技能骰。"
        );
        _test.Eq(
            DictInt(damageEvent, "critical_extra_weapon_damage_dice_total", 0),
            4,
            "暴击应额外掷一组武器骰。"
        );
        _test.Eq(
            DictInt(damageEvent, "damage_dice_bonus", 0),
            3,
            "技能 dice_bonus 不应因暴击重复加入。"
        );
        _test.Eq(
            DictInt(damageEvent, "weapon_damage_dice_bonus", 0),
            2,
            "武器 flat_bonus 不应因暴击重复加入。"
        );
        _test.True(
            DictBool(damageEvent, "damage_dice_high_total_roll", false),
            "暴击且存在任意骰组时本段 high-total 事件应为 true。"
        );
        _test.Eq(
            DictString(damageEvent, "damage_dice_high_total_roll_reason"),
            "critical_hit",
            "暴击 high-total reason 应记录 critical_hit。"
        );
        _test.True(
            DictBool(damageEvent, "skill_damage_dice_is_max", false),
            "暴击且存在技能骰时本段技能骰事件应为 true。"
        );
        _test.Eq(
            DictString(damageEvent, "skill_damage_dice_is_max_reason"),
            "critical_hit",
            "暴击技能骰 reason 应记录 critical_hit。"
        );
        _test.True(
            DictBool(damageEvent, "weapon_damage_dice_is_max", false),
            "暴击且存在武器骰时本段武器骰事件应为 true。"
        );
        _test.Eq(
            DictString(damageEvent, "weapon_damage_dice_is_max_reason"),
            "critical_hit",
            "暴击武器骰 reason 应记录 critical_hit。"
        );
        _test.True(
            DictBool(result, "damage_dice_high_total_roll", false),
            "顶层 high-total 事件应 OR 汇总 damage_events。"
        );
        _test.True(
            DictBool(result, "skill_damage_dice_is_max", false),
            "顶层技能骰事件应 OR 汇总 damage_events。"
        );
        _test.True(
            DictBool(result, "weapon_damage_dice_is_max", false),
            "顶层武器骰事件应 OR 汇总 damage_events。"
        );
    }

    private void TestEachDamageEffectReadsAddWeaponDiceIndependently()
    {
        var resolver = BuildFixedRollDamageResolver(new[] { 4, 5 });
        BattleUnitState source = BuildUnit("multi_weapon_user");
        ApplyWeapon(source, 1, 6, 0);
        BattleUnitState target = BuildUnit("multi_weapon_target");
        CombatEffectDef firstEffect = BuildDamageEffect(0, true);
        CombatEffectDef secondEffect = BuildDamageEffect(0, true);

        GDictionary result = AttackEffectResolutionResultReader.BuildGodotPayload(resolver.ResolveEffects(
            source,
            target,
            new GArray { firstEffect, secondEffect },
            new GDictionary()
        ));
        GArray events = GetArray(result, "damage_events");
        _test.Eq(events.Count, 2, "多段 damage effect 应各自产生 damage event。");
        if (events.Count >= 2)
        {
            GDictionary firstEvent = events[0].AsGodotDictionary();
            GDictionary secondEvent = events[1].AsGodotDictionary();
            _test.Eq(
                DictInt(firstEvent, "weapon_damage_dice_total", 0),
                4,
                "第一段应独立掷当前武器骰。"
            );
            _test.Eq(
                DictInt(secondEvent, "weapon_damage_dice_total", 0),
                5,
                "第二段应再次独立掷当前武器骰。"
            );
            _test.Eq(
                DictInt(firstEvent, "base_damage", 0),
                4,
                "第一段 base_damage 应只包含本段武器骰。"
            );
            _test.Eq(
                DictInt(secondEvent, "base_damage", 0),
                5,
                "第二段 base_damage 应只包含本段武器骰。"
            );
        }
        _test.Eq(
            DictInt(result, "damage", 0),
            9,
            "多段 add_weapon_dice 应允许重复加入当前武器骰。"
        );
    }

    private void TestCurrentTwoHandedWeaponDiceIsUsed()
    {
        var resolver = BuildFixedRollDamageResolver(new[] { 3, 4 });
        BattleUnitState source = BuildUnit("two_handed_user");
        source.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = BattleUnitState.ToStringName(BattleWeaponProfileKind.Equipped),
                weapon_item_id = "two_handed_test_weapon",
                weapon_profile_type_id = "greatsword",
                weapon_current_grip = BattleUnitState.ToStringName(BattleWeaponGripKind.TwoHanded),
                weapon_attack_range = 1,
                weapon_one_handed_dice = new WeaponDice
                {
                    dice_count = 1,
                    dice_sides = 4,
                    flat_bonus = 0,
                },
                weapon_two_handed_dice = new WeaponDice
                {
                    dice_count = 2,
                    dice_sides = 6,
                    flat_bonus = 0,
                },
                weapon_uses_two_hands = true,
                weapon_physical_damage_tag = "physical_slash",
            }
        );
        BattleUnitState target = BuildUnit("two_handed_target");
        CombatEffectDef effect = BuildDamageEffect(0, true);

        GDictionary result = AttackEffectResolutionResultReader.BuildGodotPayload(resolver.ResolveEffects(source, target, new GArray { effect }, new GDictionary()));
        GDictionary damageEvent = FirstDamageEvent(result);
        _test.Eq(
            DictInt(damageEvent, "weapon_damage_dice_count", 0),
            2,
            "双手握法应读取 two_handed_dice 的骰子数量。"
        );
        _test.Eq(
            DictInt(damageEvent, "weapon_damage_dice_sides", 0),
            6,
            "双手握法应读取 two_handed_dice 的骰面。"
        );
        _test.Eq(
            DictInt(damageEvent, "base_damage", 0),
            7,
            "双手武器应按当前握法掷 2D6。"
        );
    }

    private void TestVersatileCurrentGripSelectsActiveDice()
    {
        var resolver = BuildFixedRollDamageResolver(new[] { 5, 2, 4 });
        BattleUnitState source = BuildUnit("versatile_user");
        BattleUnitState target = BuildUnit("versatile_target");
        CombatEffectDef effect = BuildDamageEffect(0, true);

        ApplyVersatileWeapon(source, false);
        GDictionary oneHandedResult = AttackEffectResolutionResultReader.BuildGodotPayload(resolver.ResolveEffects(
            source,
            target,
            new GArray { effect },
            new GDictionary()
        ));
        GDictionary oneHandedEvent = FirstDamageEvent(oneHandedResult);
        _test.Eq(
            source.weapon_current_grip.ToString(),
            "one_handed",
            "versatile 单手握法应保留当前 grip。"
        );
        _test.True(!source.weapon_uses_two_hands, "versatile 单手握法不应标记双手。");
        _test.Eq(
            DictInt(oneHandedEvent, "weapon_damage_dice_count", 0),
            1,
            "versatile 单手应读取 one_handed_dice 数量。"
        );
        _test.Eq(
            DictInt(oneHandedEvent, "weapon_damage_dice_sides", 0),
            8,
            "versatile 单手应读取 one_handed_dice 骰面。"
        );
        _test.Eq(DictInt(oneHandedEvent, "base_damage", 0), 5, "versatile 单手应只掷 1D8。");

        ApplyVersatileWeapon(source, true);
        GDictionary twoHandedResult = AttackEffectResolutionResultReader.BuildGodotPayload(resolver.ResolveEffects(
            source,
            target,
            new GArray { effect },
            new GDictionary()
        ));
        GDictionary twoHandedEvent = FirstDamageEvent(twoHandedResult);
        _test.Eq(
            source.weapon_current_grip.ToString(),
            "two_handed",
            "versatile 双手握法应保留当前 grip。"
        );
        _test.True(source.weapon_uses_two_hands, "versatile 双手握法应标记双手。");
        _test.Eq(
            DictInt(twoHandedEvent, "weapon_damage_dice_count", 0),
            2,
            "versatile 双手应读取 two_handed_dice 数量。"
        );
        _test.Eq(
            DictInt(twoHandedEvent, "weapon_damage_dice_sides", 0),
            6,
            "versatile 双手应读取 two_handed_dice 骰面。"
        );
        _test.Eq(DictInt(twoHandedEvent, "base_damage", 0), 6, "versatile 双手应按 2D6 结算。");
    }

    private void TestUnarmedAndNaturalWeaponDiceFeedAddWeaponDice()
    {
        var resolver = BuildFixedRollDamageResolver(new[] { 4, 6 });
        BattleUnitState source = BuildUnit("innate_weapon_user");
        BattleUnitState target = BuildUnit("innate_weapon_target");
        CombatEffectDef effect = BuildDamageEffect(0, true);

        source.SetUnarmedWeaponProjectionTyped(
            "physical_blunt",
            new WeaponDice
            {
                dice_count = 1,
                dice_sides = 4,
                flat_bonus = 0,
            },
            1
        );
        GDictionary unarmedResult = AttackEffectResolutionResultReader.BuildGodotPayload(resolver.ResolveEffects(
            source,
            target,
            new GArray { effect },
            new GDictionary()
        ));
        GDictionary unarmedEvent = FirstDamageEvent(unarmedResult);
        _test.Eq(
            source.weapon_profile_kind.ToString(),
            "unarmed",
            "空手攻击应通过 unarmed profile 表达。"
        );
        _test.Eq(
            DictInt(unarmedEvent, "weapon_damage_dice_count", 0),
            1,
            "空手 add_weapon_dice 应读取 1 颗武器骰。"
        );
        _test.Eq(
            DictInt(unarmedEvent, "weapon_damage_dice_sides", 0),
            4,
            "空手 add_weapon_dice 应读取 1D4。"
        );
        _test.Eq(DictInt(unarmedEvent, "base_damage", 0), 4, "空手 add_weapon_dice 应使用空手骰。");

        source.SetNaturalWeaponProjectionTyped(
            "natural_weapon",
            "physical_pierce",
            1,
            new WeaponDice
            {
                dice_count = 1,
                dice_sides = 6,
                flat_bonus = 0,
            },
            ""
        );
        GDictionary naturalResult = AttackEffectResolutionResultReader.BuildGodotPayload(resolver.ResolveEffects(
            source,
            target,
            new GArray { effect },
            new GDictionary()
        ));
        GDictionary naturalEvent = FirstDamageEvent(naturalResult);
        _test.Eq(
            source.weapon_profile_kind.ToString(),
            "natural",
            "天生武器应通过 natural profile 表达。"
        );
        _test.Eq(
            DictInt(naturalEvent, "weapon_damage_dice_count", 0),
            1,
            "天生武器 add_weapon_dice 应读取 1 颗武器骰。"
        );
        _test.Eq(
            DictInt(naturalEvent, "weapon_damage_dice_sides", 0),
            6,
            "天生武器 add_weapon_dice 应读取 natural weapon 骰面。"
        );
        _test.Eq(
            DictInt(naturalEvent, "base_damage", 0),
            6,
            "天生武器 add_weapon_dice 应使用 natural weapon 骰。"
        );
    }

    private void TestRequiresWeaponGateAcceptsEquippedOnly()
    {
        SkillDef skill = BuildRuntimeDamageSkill("requires_weapon_contract", 1, true, false);
        var runtime = new BattleRuntimeModule();
        runtime.setup(
            null,
            new Dictionary<StringName, SkillDef> { [skill.skill_id] = skill }
        );
        runtime.ConfigureDamageResolverForTests(
            BuildFixedRollDamageResolver(new[] { 1 }, new[] { 10 })
        );
        runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));

        RuntimeDuelFixture fixture = BuildRuntimeDuelFixture(runtime, skill.skill_id);
        BattleUnitState attacker = fixture.Attacker;
        BattleUnitState target = fixture.Target;
        BattleCommand command = fixture.Command;

        attacker.SetUnarmedWeaponProjectionTyped(
            "physical_blunt",
            new WeaponDice
            {
                dice_count = 1,
                dice_sides = 4,
                flat_bonus = 0,
            },
            1
        );
        int targetHpBefore = target.current_hp;
        BattleEventBatch unarmedBatch = runtime.IssueCommand(command);
        _test.True(
            unarmedBatch != null && unarmedBatch.log_lines.Count > 0,
            $"空手攻击不应满足 requires_weapon，且应回传阻断反馈。 log={FormatLogs(unarmedBatch?.log_lines)}"
        );
        _test.Eq(attacker.current_ap, 2, "空手被 requires_weapon 阻断时不应扣除 AP。");
        _test.Eq(target.current_hp, targetHpBefore, "空手被 requires_weapon 阻断时不应结算伤害。");

        attacker.SetNaturalWeaponProjectionTyped(
            "natural_weapon",
            "physical_slash",
            1,
            new WeaponDice
            {
                dice_count = 1,
                dice_sides = 6,
                flat_bonus = 0,
            },
            ""
        );
        BattleEventBatch naturalBatch = runtime.IssueCommand(command);
        _test.True(
            naturalBatch != null && naturalBatch.log_lines.Count > 0,
            $"天生武器不应满足 requires_weapon，且应回传阻断反馈。 log={FormatLogs(naturalBatch?.log_lines)}"
        );
        _test.Eq(attacker.current_ap, 2, "天生武器被 requires_weapon 阻断时不应扣除 AP。");
        _test.Eq(target.current_hp, targetHpBefore, "天生武器被 requires_weapon 阻断时不应结算伤害。");

        ApplyWeapon(attacker, 1, 6, 0);
        BattleEventBatch equippedBatch = runtime.IssueCommand(command);
        _test.True(
            equippedBatch.changed_unit_ids.Contains(attacker.unit_id),
            "装备武器应满足 requires_weapon 并正常结算施法者。"
        );
        _test.Eq(attacker.current_ap, 1, "装备武器满足 requires_weapon 后应正常扣除 AP。");
        _test.True(target.current_hp < targetHpBefore, "装备武器满足 requires_weapon 后应造成伤害。");
    }

    private void TestNaturalWeaponDiceDoNotTriggerSkillMastery()
    {
        var gateway = new MasteryGatewayStub();
        SkillDef skill = BuildRuntimeDamageSkill("natural_weapon_only_mastery_contract", 0, false, true);
        var runtime = new BattleRuntimeModule();
        runtime.setup(
            gateway,
            new Dictionary<StringName, SkillDef> { [skill.skill_id] = skill }
        );
        runtime.ConfigureDamageResolverForTests(
            BuildFixedRollDamageResolver(new[] { 1 }, new[] { 10 })
        );
        runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));

        RuntimeDuelFixture fixture = BuildRuntimeDuelFixture(runtime, skill.skill_id);
        BattleUnitState attacker = fixture.Attacker;
        BattleCommand command = fixture.Command;
        attacker.source_member_id = "hero";
        attacker.SetNaturalWeaponProjectionTyped(
            "natural_weapon",
            "physical_slash",
            1,
            new WeaponDice
            {
                dice_count = 1,
                dice_sides = 1,
                flat_bonus = 0,
            },
            ""
        );

        BattleEventBatch batch = runtime.IssueCommand(command);
        _test.True(
            batch.changed_unit_ids.Contains(attacker.unit_id),
            "天生武器骰技能应正常完成一次主动技能结算。"
        );
        _test.Eq(gateway.SkillUsedEvents, 1, "天生武器骰技能成功后仍应记录技能使用事件。");
        _test.Eq(gateway.Grants.Count, 0, "天生武器骰满值不应触发主动技能熟练度 / 精通入账。");
    }

    private void TestDiceEventFieldsSplitByDiceGroup()
    {
        var resolver = BuildFixedRollDamageResolver(new[] { 6, 4 });
        BattleUnitState source = BuildUnit("dice_event_split_user");
        ApplyWeapon(source, 1, 6, 0);
        BattleUnitState target = BuildUnit("dice_event_split_target");
        CombatEffectDef weaponOnlyEffect = BuildDamageEffect(0, true);
        CombatEffectDef skillOnlyEffect = BuildDamageEffect(0, false, 1, 4);

        GDictionary result = AttackEffectResolutionResultReader.BuildGodotPayload(resolver.ResolveEffects(
            source,
            target,
            new GArray { weaponOnlyEffect, skillOnlyEffect },
            new GDictionary()
        ));
        GArray events = GetArray(result, "damage_events");
        _test.Eq(events.Count, 2, "拆分骰子事件回归应产生两段 damage event。");
        _test.True(
            DictBool(result, "damage_dice_high_total_roll", false),
            "顶层 high-total 应只表达任意一段满足。"
        );
        _test.True(
            DictBool(result, "skill_damage_dice_is_max", false),
            "顶层技能骰事件应只表达任意一段满足。"
        );
        _test.True(
            DictBool(result, "weapon_damage_dice_is_max", false),
            "顶层武器骰事件应只表达任意一段满足。"
        );
        _test.True(!result.ContainsKey("damage_dice_high_total_roll_reason"), "顶层 high-total 不应携带单段 reason。");
        _test.True(!result.ContainsKey("skill_damage_dice_is_max_reason"), "顶层技能骰事件不应携带单段 reason。");
        _test.True(!result.ContainsKey("weapon_damage_dice_is_max_reason"), "顶层武器骰事件不应携带单段 reason。");

        if (events.Count >= 2)
        {
            GDictionary weaponEvent = events[0].AsGodotDictionary();
            GDictionary skillEvent = events[1].AsGodotDictionary();
            _test.True(
                DictBool(weaponEvent, "damage_dice_high_total_roll", false),
                "武器满骰段应满足 high-total 阈值。"
            );
            _test.Eq(
                DictString(weaponEvent, "damage_dice_high_total_roll_reason"),
                "dice_threshold",
                "非暴击 high-total reason 应记录 dice_threshold。"
            );
            _test.True(
                !DictBool(weaponEvent, "skill_damage_dice_is_max", true),
                "没有技能骰的段不应触发技能骰事件。"
            );
            _test.Eq(
                DictString(weaponEvent, "skill_damage_dice_is_max_reason"),
                "",
                "没有技能骰时技能骰 reason 应为空。"
            );
            _test.True(
                DictBool(weaponEvent, "weapon_damage_dice_is_max", false),
                "武器满骰段应触发武器骰事件。"
            );
            _test.Eq(
                DictString(weaponEvent, "weapon_damage_dice_is_max_reason"),
                "weapon_dice_max",
                "武器满骰 reason 应记录 weapon_dice_max。"
            );
            _test.True(
                DictBool(skillEvent, "damage_dice_high_total_roll", false),
                "技能满骰段应满足 high-total 阈值。"
            );
            _test.Eq(
                DictString(skillEvent, "damage_dice_high_total_roll_reason"),
                "dice_threshold",
                "技能满骰 high-total reason 应记录 dice_threshold。"
            );
            _test.True(
                DictBool(skillEvent, "skill_damage_dice_is_max", false),
                "技能满骰段应触发技能骰事件。"
            );
            _test.Eq(
                DictString(skillEvent, "skill_damage_dice_is_max_reason"),
                "skill_dice_max",
                "技能满骰 reason 应记录 skill_dice_max。"
            );
            _test.True(
                !DictBool(skillEvent, "weapon_damage_dice_is_max", true),
                "没有武器骰的段不应触发武器骰事件。"
            );
            _test.Eq(
                DictString(skillEvent, "weapon_damage_dice_is_max_reason"),
                "",
                "没有武器骰时武器骰 reason 应为空。"
            );
        }
    }

    private void TestDiceEventFieldsStayFalseWithoutDiceGroups()
    {
        var resolver = BuildFixedRollDamageResolver(new[] { 6 });
        BattleUnitState source = BuildUnit("no_dice_event_user");
        BattleUnitState target = BuildUnit("no_dice_event_target");
        CombatEffectDef effect = BuildDamageEffect(5, false);

        GDictionary result = AttackEffectResolutionResultReader.BuildGodotPayload(resolver.ResolveEffects(source, target, new GArray { effect }, new GDictionary()));
        GDictionary damageEvent = FirstDamageEvent(result);
        _test.True(
            !DictBool(damageEvent, "damage_dice_high_total_roll", true),
            "无骰组时 high-total 事件必须为 false。"
        );
        _test.True(
            !DictBool(damageEvent, "skill_damage_dice_is_max", true),
            "无技能骰组时技能骰事件必须为 false。"
        );
        _test.True(
            !DictBool(damageEvent, "weapon_damage_dice_is_max", true),
            "无武器骰组时武器骰事件必须为 false。"
        );
        _test.Eq(
            DictString(damageEvent, "damage_dice_high_total_roll_reason"),
            "",
            "无骰组时 high-total reason 应为空。"
        );
        _test.Eq(
            DictString(damageEvent, "skill_damage_dice_is_max_reason"),
            "",
            "无技能骰组时技能骰 reason 应为空。"
        );
        _test.Eq(
            DictString(damageEvent, "weapon_damage_dice_is_max_reason"),
            "",
            "无武器骰组时武器骰 reason 应为空。"
        );
        _test.True(
            !DictBool(result, "damage_dice_high_total_roll", true),
            "顶层 high-total 汇总不应因 0 == 0 触发。"
        );
        _test.True(
            !DictBool(result, "skill_damage_dice_is_max", true),
            "顶层技能骰汇总不应因 0 == 0 触发。"
        );
        _test.True(
            !DictBool(result, "weapon_damage_dice_is_max", true),
            "顶层武器骰汇总不应因 0 == 0 触发。"
        );
    }

    private void TestWarriorHeavyStrikeUsesWeaponPlusSkillDiceTemplate()
    {
        var registry = new ProgressionContentRegistry();
        IReadOnlyDictionary<StringName, SkillDef> skillDefs = registry.GetSkillDefsTyped();
        skillDefs.TryGetValue("warrior_heavy_strike", out SkillDef skillDef);
        _test.True(
            skillDef?.combat_profile != null,
            "重击技能配置应可加载。"
        );
        if (skillDef?.combat_profile == null)
        {
            return;
        }

        var expectedProfiles = new[]
        {
            new ExpectedDamageProfile(0, 0, 1, 4),
            new ExpectedDamageProfile(1, 2, 1, 6),
            new ExpectedDamageProfile(3, 4, 1, 8),
            new ExpectedDamageProfile(5, -1, 2, 5),
        };
        var damageEffects = new Godot.Collections.Array<CombatEffectDef>();
        foreach (CombatEffectDef effect in skillDef.combat_profile.effect_defs)
        {
            if (effect == null || effect.effect_type != "damage")
            {
                continue;
            }
            damageEffects.Add(effect);
        }

        _test.Eq(
            damageEffects.Count,
            expectedProfiles.Length,
            "重击应保留 0 / 1-2 / 3-4 / 5+ 四段技能骰样板。"
        );
        int count = Math.Min(damageEffects.Count, expectedProfiles.Length);
        for (int i = 0; i < count; i++)
        {
            CombatEffectDef effect = damageEffects[i];
            ExpectedDamageProfile expected = expectedProfiles[i];
            _test.True(effect.add_weapon_dice, "重击每段伤害样板都应显式 add_weapon_dice。");
            _test.True(effect.requires_weapon, "重击仍应要求装备武器。");
            _test.True(effect.use_weapon_physical_damage_tag, "重击每段伤害应使用当前武器物理伤害标签。");
            _test.Eq(
                effect.min_skill_level,
                expected.MinSkillLevel,
                "重击技能骰分段 min_skill_level 应匹配当前样板。"
            );
            _test.Eq(
                effect.max_skill_level,
                expected.MaxSkillLevel,
                "重击技能骰分段 max_skill_level 应匹配当前样板。"
            );
            _test.Eq(
                effect.dice_count,
                expected.DiceCount,
                "重击技能骰数量应按等级样板递进。"
            );
            _test.Eq(
                effect.dice_sides,
                expected.DiceSides,
                "重击技能骰骰面应按等级样板递进。"
            );
        }
    }

    private static CombatEffectDef BuildDamageEffect(
        int power,
        bool addWeaponDice,
        int diceCount = 0,
        int diceSides = 0,
        int diceBonus = 0,
        StringName damageTag = default
    )
    {
        if (damageTag == default || damageTag == (StringName)"")
        {
            damageTag = "physical_blunt";
        }

        var effect = new CombatEffectDef
        {
            effect_type = "damage",
            power = power,
            damage_tag = damageTag,
            @params = new GDictionary(),
        };
        if (addWeaponDice)
        {
            effect.add_weapon_dice = true;
        }
        if (diceCount > 0 && diceSides > 0)
        {
            effect.dice_count = diceCount;
            effect.dice_sides = diceSides;
            effect.dice_bonus = diceBonus;
        }
        return effect;
    }

    private static SkillDef BuildRuntimeDamageSkill(
        StringName skillId,
        int power,
        bool requiresWeapon,
        bool addWeaponDice,
        int diceCount = 0,
        int diceSides = 0
    )
    {
        CombatEffectDef damageEffect = BuildDamageEffect(
            power,
            addWeaponDice,
            diceCount,
            diceSides
        );
        damageEffect.effect_target_team_filter = "enemy";
        if (requiresWeapon)
        {
            damageEffect.requires_weapon = true;
            damageEffect.use_weapon_physical_damage_tag = true;
        }

        var combatProfile = new CombatSkillDef
        {
            skill_id = skillId,
            target_mode = "unit",
            target_team_filter = "enemy",
            range_value = 1,
            ap_cost = 1,
        };
        combatProfile.effect_defs = new Godot.Collections.Array<CombatEffectDef>
        {
            damageEffect,
        };

        return new SkillDef
        {
            skill_id = skillId,
            display_name = skillId.ToString(),
            tags = new GStringNameArray { "warrior", "melee" },
            combat_profile = combatProfile,
        };
    }

    private RuntimeDuelFixture BuildRuntimeDuelFixture(BattleRuntimeModule runtime, StringName skillId)
    {
        BattleState state = BuildSkillTestState(new Vector2I(2, 1));
        BattleUnitState attacker = BuildUnit("weapon_contract_user", new Vector2I(0, 0), 2);
        attacker.known_active_skill_ids.Add(skillId);
        attacker.known_skill_level_map[skillId] = 1;
        attacker.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), 100);
        BattleUnitState target = BuildEnemyUnit("weapon_contract_target", new Vector2I(1, 0));
        target.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 1);
        state.SetUnit(attacker);
        state.SetUnit(target);
        state.ally_unit_ids.Add(attacker.unit_id);
        state.enemy_unit_ids.Add(target.unit_id);
        state.active_unit_id = attacker.unit_id;
        _test.True(
            runtime._grid_service.PlaceUnit(state, attacker, attacker.coord, true),
            "武器骰 runtime 夹具攻击者应能放入战场。"
        );
        _test.True(
            runtime._grid_service.PlaceUnit(state, target, target.coord, true),
            "武器骰 runtime 夹具目标应能放入战场。"
        );
        runtime.SetupStateForTests(state);

        var command = new BattleCommand
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            unit_id = attacker.unit_id,
            skill_id = skillId,
            target_unit_id = target.unit_id,
            target_coord = target.coord,
        };
        return new RuntimeDuelFixture(state, attacker, target, command);
    }

    private static BattleState BuildSkillTestState(Vector2I mapSize)
    {
        var state = new BattleState
        {
            battle_id = "weapon_dice_runtime_contract",
            phase = "unit_acting",
            map_size = mapSize,
            timeline = new BattleTimelineState(),
        };
        state.ClearCells();
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                Vector2I coord = new(x, y);
                state.SetCell(coord, BuildCell(coord));
            }
        }
        state.RebuildCellColumns();
        return state;
    }

    private static BattleCellState BuildCell(Vector2I coord)
    {
        var cell = new BattleCellState
        {
            coord = coord,
            base_terrain = BattleTerrainRules.ToStringName(BattleTerrainKind.Land),
            base_height = 4,
            height_offset = 0,
        };
        cell.RecalculateRuntimeValues();
        return cell;
    }

    private static BattleUnitState BuildUnit(
        StringName unitId,
        Vector2I coord = default,
        int currentAp = 1
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "player",
            current_ap = currentAp,
            current_hp = 100,
            is_alive = true,
        };
        unit.attribute_snapshot.SetValue("hp_max", 100);
        unit.SetAnchorCoord(coord);
        return unit;
    }

    private static BattleUnitState BuildEnemyUnit(StringName unitId, Vector2I coord)
    {
        BattleUnitState unit = BuildUnit(unitId, coord);
        unit.faction_id = "enemy";
        unit.current_hp = 30;
        unit.attribute_snapshot.SetValue("hp_max", 30);
        return unit;
    }

    private static void ApplyWeapon(BattleUnitState unit, int diceCount, int diceSides, int flatBonus)
    {
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = BattleUnitState.ToStringName(BattleWeaponProfileKind.Equipped),
                weapon_item_id = "weapon_dice_test_weapon",
                weapon_profile_type_id = "test_weapon",
                weapon_current_grip = BattleUnitState.ToStringName(BattleWeaponGripKind.OneHanded),
                weapon_attack_range = 1,
                weapon_one_handed_dice = new WeaponDice
                {
                    dice_count = diceCount,
                    dice_sides = diceSides,
                    flat_bonus = flatBonus,
                },
                weapon_uses_two_hands = false,
                weapon_physical_damage_tag = "physical_slash",
            }
        );
    }

    private static void ApplyVersatileWeapon(BattleUnitState unit, bool usesTwoHands)
    {
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = BattleUnitState.ToStringName(BattleWeaponProfileKind.Equipped),
                weapon_item_id = "versatile_test_longsword",
                weapon_profile_type_id = "longsword",
                weapon_current_grip = usesTwoHands
                    ? BattleUnitState.ToStringName(BattleWeaponGripKind.TwoHanded)
                    : BattleUnitState.ToStringName(BattleWeaponGripKind.OneHanded),
                weapon_attack_range = 1,
                weapon_one_handed_dice = new WeaponDice
                {
                    dice_count = 1,
                    dice_sides = 8,
                    flat_bonus = 0,
                },
                weapon_two_handed_dice = new WeaponDice
                {
                    dice_count = 2,
                    dice_sides = 6,
                    flat_bonus = 0,
                },
                weapon_is_versatile = true,
                weapon_uses_two_hands = usesTwoHands,
                weapon_physical_damage_tag = "physical_slash",
            }
        );
    }

    private static FixedRollDamageResolver BuildFixedRollDamageResolver(
        int[] damageRolls,
        int[] attackRolls = null
    )
    {
        return new FixedRollDamageResolver(ToArray(damageRolls), ToArray(attackRolls));
    }

    private static GArray ToArray(int[] values)
    {
        var result = new GArray();
        if (values == null)
        {
            return result;
        }
        foreach (int value in values)
        {
            result.Add(value);
        }
        return result;
    }

    private static GDictionary FirstDamageEvent(GDictionary result)
    {
        GArray events = GetArray(result, "damage_events");
        return events.Count > 0 ? events[0].AsGodotDictionary() : new GDictionary();
    }

    private static GArray GetArray(GDictionary dictionary, Variant key)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return new GArray();
        }
        return dictionary[key].AsGodotArray();
    }

    private static int DictInt(GDictionary dictionary, Variant key, int fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return fallback;
        }
        return dictionary[key].AsInt32();
    }

    private static bool DictBool(GDictionary dictionary, Variant key, bool fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return fallback;
        }
        return dictionary[key].AsBool();
    }

    private static string DictString(GDictionary dictionary, Variant key)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return "";
        }
        return dictionary[key].AsString();
    }

    private static string FormatLogs(GStringArray logLines)
    {
        if (logLines == null)
        {
            return "[]";
        }
        return "[" + string.Join(", ", logLines) + "]";
    }

    private sealed class ExpectedDamageProfile
    {
        public ExpectedDamageProfile(
            int minSkillLevel,
            int maxSkillLevel,
            int diceCount,
            int diceSides
        )
        {
            MinSkillLevel = minSkillLevel;
            MaxSkillLevel = maxSkillLevel;
            DiceCount = diceCount;
            DiceSides = diceSides;
        }

        public int MinSkillLevel { get; }
        public int MaxSkillLevel { get; }
        public int DiceCount { get; }
        public int DiceSides { get; }
    }

    private sealed class RuntimeDuelFixture
    {
        public RuntimeDuelFixture(
            BattleState state,
            BattleUnitState attacker,
            BattleUnitState target,
            BattleCommand command
        )
        {
            State = state;
            Attacker = attacker;
            Target = target;
            Command = command;
        }

        public BattleState State { get; }
        public BattleUnitState Attacker { get; }
        public BattleUnitState Target { get; }
        public BattleCommand Command { get; }
    }

    private sealed class MasteryGatewayStub : IBattleRuntimeCharacterGateway
    {
        public int SkillUsedEvents { get; private set; }
        public List<CharacterMasteryChangeFact> Grants { get; } = new();

        public PartyState GetPartyState() => null;

        public IReadOnlyDictionary<StringName, ItemDef> GetItemDefsTyped() =>
            new Dictionary<StringName, ItemDef>();

        public bool HasItemDefCatalog() => false;

        public ItemDef GetItemDef(StringName item_id) => null;

        public PartyMemberState GetMemberState(StringName member_id) => null;

        public AttributeSnapshot GetMemberAttributeSnapshotForEquipmentView(
            StringName member_id,
            EquipmentState equipment_view
        ) => null;

        public WeaponProjection GetMemberWeaponProjectionForEquipmentViewTyped(
            StringName member_id,
            EquipmentState equipment_view
        ) => new();

        public BattleEffectiveTraitProjection BuildEffectiveTraitProjectionForEquipmentView(
            StringName member_id,
            EquipmentState equipment_view
        ) => BattleEffectiveTraitProjection.Empty;

        public PassiveSourceContext BuildPassiveSourceContext(
            StringName member_id,
            UnitProgress progression_state
        ) => null;

        public CharacterProgressionDelta PromoteProfession(
            StringName member_id,
            StringName profession_id,
            PromotionSelectionData selection
        ) => new() { member_id = member_id };

        public BattleResourceCommitResult CommitBattleResources(
            StringName member_id,
            int current_hp,
            int current_mp,
            int current_aura
        ) => BattleResourceCommitResult.Success(member_id);

        public void CommitBattleDeath(StringName member_id) { }

        public int FlushAfterBattle() => (int)Error.Ok;

        public CharacterProgressionDelta GrantBattleMastery(
            StringName member_id,
            StringName skill_id,
            int amount
        )
        {
            return RecordGrant(member_id, skill_id, amount, "battle");
        }

        public CharacterProgressionDelta GrantSkillMasteryFromSource(
            StringName member_id,
            StringName skill_id,
            int amount,
            StringName source_type,
            string source_label,
            string reason_text,
            bool emit_achievement_event
        )
        {
            return RecordGrant(member_id, skill_id, amount, source_type);
        }

        public GStringNameArray RecordAchievementEvent(
            StringName member_id,
            StringName event_type,
            int amount
        )
        {
            return RecordAchievementEvent(member_id, event_type, amount, "", new GDictionary());
        }

        public GStringNameArray RecordAchievementEvent(
            StringName member_id,
            StringName event_type,
            int amount,
            StringName subject_id,
            GDictionary meta
        )
        {
            if (event_type == "skill_used")
            {
                SkillUsedEvents += amount;
            }
            return new GStringNameArray();
        }

        public PendingCharacterReward BuildPendingSkillMasteryReward(
            StringName member_id,
            StringName source_type,
            string source_label,
            IEnumerable<PendingCharacterRewardEntry> entry_options,
            string summary_text
        ) => null;

        private CharacterProgressionDelta RecordGrant(
            StringName memberId,
            StringName skillId,
            int amount,
            StringName sourceType
        )
        {
            var change = new CharacterMasteryChangeFact(
                skillId,
                skillId.ToString(),
                amount,
                sourceType,
                sourceType.ToString(),
                ""
            );
            Grants.Add(change);
            var delta = new CharacterProgressionDelta
            {
                member_id = memberId,
            };
            delta.AddMasteryChange(change);
            return delta;
        }
    }
}
