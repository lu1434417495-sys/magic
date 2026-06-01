using System;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_battle_rule_status_param_schema_regression : SceneTree
{
    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestLockCritUsesTypedStatusField();
        TestFateAttackRulesNoLongerRequireGodotRegistration();
        TestLockDodgeBonusAcceptsStringNameParamKey();
        TestBlindAttackPenaltyUsesStatusSemanticAndParamOverride();
        TestDamageBoolHelperAcceptsStringNameParamKey();
        TestMitigationTierAcceptsStringNameParamKey();
        TestOutgoingDamageMultiplierAcceptsStringNameParamKey();

        if (_failures.Count == 0)
        {
            GD.Print("Battle rule status param schema regression: PASS");
            return 0;
        }
        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle rule status param schema regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestLockCritUsesTypedStatusField()
    {
        BattleUnitState legacyUnit = BuildUnit("legacy_lock_crit");
        SetStatusParams(
            legacyUnit,
            "legacy_lock_crit",
            new GDictionary { [new StringName("lock_crit")] = true }
        );
        AssertFalse(
            BattleFateAttackRules.IsAttackCritLocked(legacyUnit),
            "StringName-only lock_crit params must not drive crit locks after typed status migration."
        );

        BattleUnitState formalUnit = BuildUnit("formal_lock_crit");
        SetTypedStatus(formalUnit, "formal_lock_crit", lockCrit: true);
        AssertTrue(
            BattleFateAttackRules.IsAttackCritLocked(formalUnit),
            "typed lock_crit status field must lock crit."
        );
    }

    private void TestFateAttackRulesNoLongerRequireGodotRegistration()
    {
        Type rulesType = typeof(BattleFateAttackRules);
        AssertTrue(
            rulesType.IsAbstract && rulesType.IsSealed,
            "BattleFateAttackRules 应是 static C# rules helper。"
        );
        AssertFalse(
            typeof(GodotObject).IsAssignableFrom(rulesType),
            "BattleFateAttackRules 不应继承 GodotObject/RefCounted。"
        );
        AssertFalse(
            rulesType.GetCustomAttributes(typeof(GlobalClassAttribute), inherit: false).Length > 0,
            "BattleFateAttackRules 不应继续注册为 Godot GlobalClass。"
        );
        AssertTrue(
            rulesType.GetMethod("is_attack_crit_locked") == null,
            "BattleFateAttackRules 不应保留 snake_case crit lock API。"
        );
    }

    private void TestLockDodgeBonusAcceptsStringNameParamKey()
    {
        var resolver = new BattleHitResolver();
        BattleUnitState attacker = BuildUnit("hit_attacker");
        attacker.attribute_snapshot.set_value(AttributeService.ATTACK_BONUS_ID(), 0);

        BattleUnitState legacyTarget = BuildUnit("legacy_lock_dodge_bonus");
        SetAcProfile(legacyTarget, 15, 4);
        SetStatusParams(
            legacyTarget,
            "legacy_lock_dodge_bonus",
            new GDictionary { [new StringName("lock_dodge_bonus")] = true }
        );
        AttackCheckInput legacyCheck = resolver.build_skill_attack_check(
            attacker,
            legacyTarget,
            null,
            0,
            0
        );
        AssertEq(
            legacyCheck.TargetArmorClass,
            11,
            "StringName-only lock_dodge_bonus params should remove the dodge AC component."
        );

        BattleUnitState formalTarget = BuildUnit("formal_lock_dodge_bonus");
        SetAcProfile(formalTarget, 15, 4);
        SetStatusParams(
            formalTarget,
            "formal_lock_dodge_bonus",
            new GDictionary { ["lock_dodge_bonus"] = true }
        );
        AttackCheckInput formalCheck = resolver.build_skill_attack_check(
            attacker,
            formalTarget,
            null,
            0,
            0
        );
        AssertEq(
            formalCheck.TargetArmorClass,
            11,
            "String key lock_dodge_bonus params must still remove the dodge AC component."
        );
    }

    private void TestBlindAttackPenaltyUsesStatusSemanticAndParamOverride()
    {
        var resolver = new BattleHitResolver();
        BattleUnitState target = BuildUnit("blind_penalty_target");
        SetAcProfile(target, 15, 0);

        BattleUnitState clearAttacker = BuildUnit("clear_blind_penalty_attacker");
        AttackCheckInput clearCheck = resolver.build_skill_attack_check(clearAttacker, target, null, 0, 0);

        BattleUnitState defaultBlindAttacker = BuildUnit("default_blind_penalty_attacker");
        SetStatusParams(defaultBlindAttacker, "blind", new GDictionary());
        AttackCheckInput defaultCheck = resolver.build_skill_attack_check(
            defaultBlindAttacker,
            target,
            null,
            0,
            0
        );
        AssertEq(
            defaultCheck.SituationalAttackPenalty,
            4,
            "blind 默认应让攻击检定承受 -4 等价惩罚。"
        );
        AssertEq(
            defaultCheck.RequiredRoll,
            clearCheck.RequiredRoll + 4,
            "blind 攻击惩罚应提高命中所需 d20 点数。"
        );

        BattleUnitState severeBlindAttacker = BuildUnit("severe_blind_penalty_attacker");
        SetStatusParams(
            severeBlindAttacker,
            "blind",
            new GDictionary { ["attack_roll_penalty"] = 6 }
        );
        AttackCheckInput severeCheck = resolver.build_skill_attack_check(
            severeBlindAttacker,
            target,
            null,
            0,
            0
        );
        AssertEq(
            severeCheck.SituationalAttackPenalty,
            6,
            "blind 的 attack_roll_penalty 参数应能覆盖默认攻击惩罚。"
        );
    }

    private void TestDamageBoolHelperAcceptsStringNameParamKey()
    {
        var resolver = new BattleDamageResolver();

        BattleUnitState legacyUnit = BuildUnit("legacy_damage_bool_param");
        SetStatusParams(
            legacyUnit,
            "legacy_damage_bool_param",
            new GDictionary { [new StringName("lock_crit")] = true }
        );
        AssertTrue(
            resolver._unit_has_status_bool_param(legacyUnit, "lock_crit"),
            "BattleDamageResolver bool helper should accept StringName-only params."
        );

        BattleUnitState formalUnit = BuildUnit("formal_damage_bool_param");
        SetStatusParams(
            formalUnit,
            "formal_damage_bool_param",
            new GDictionary { ["lock_crit"] = true }
        );
        AssertTrue(
            resolver._unit_has_status_bool_param(formalUnit, "lock_crit"),
            "BattleDamageResolver bool helper must still accept String-key params."
        );
    }

    private void TestMitigationTierAcceptsStringNameParamKey()
    {
        var resolver = new BattleDamageResolver();

        BattleUnitState legacySource = BuildUnit("legacy_mitigation_source");
        BattleUnitState legacyTarget = BuildUnit("legacy_mitigation_target");
        SetStatusParams(
            legacyTarget,
            "legacy_half_mitigation",
            new GDictionary { [new StringName("mitigation_tier")] = "half" }
        );
        GDictionary legacyResult = resolver.resolve_effects(
            legacySource,
            legacyTarget,
            new GArray { BuildDamageEffect(20) },
            new GDictionary()
        );
        AssertEq(
            DictInt(legacyResult, "damage", -1),
            10,
            "StringName-only mitigation_tier params should reduce damage."
        );
        GDictionary legacyEvent = FirstDamageEvent(legacyResult);
        AssertEq(
            DictStringName(legacyEvent, "mitigation_tier"),
            new StringName("half"),
            "StringName-only mitigation_tier params should be reported on the damage event."
        );

        BattleUnitState formalSource = BuildUnit("formal_mitigation_source");
        BattleUnitState formalTarget = BuildUnit("formal_mitigation_target");
        SetStatusParams(
            formalTarget,
            "formal_half_mitigation",
            new GDictionary { ["mitigation_tier"] = "half" }
        );
        GDictionary formalResult = resolver.resolve_effects(
            formalSource,
            formalTarget,
            new GArray { BuildDamageEffect(20) },
            new GDictionary()
        );
        AssertEq(
            DictInt(formalResult, "damage", -1),
            10,
            "String key mitigation_tier params must still reduce damage."
        );
        GDictionary formalEvent = FirstDamageEvent(formalResult);
        AssertEq(
            DictStringName(formalEvent, "mitigation_tier"),
            new StringName("half"),
            "String key mitigation_tier params must still be reported on the damage event."
        );
    }

    private void TestOutgoingDamageMultiplierAcceptsStringNameParamKey()
    {
        var resolver = new BattleDamageResolver();

        BattleUnitState legacySource = BuildUnit("legacy_outgoing_multiplier_source");
        BattleUnitState legacyTarget = BuildUnit("legacy_outgoing_multiplier_target");
        SetStatusParams(
            legacySource,
            "legacy_outgoing_multiplier",
            new GDictionary { [new StringName("outgoing_damage_multiplier")] = 0.5 }
        );
        GDictionary legacyResult = resolver.resolve_effects(
            legacySource,
            legacyTarget,
            new GArray { BuildDamageEffect(20) },
            new GDictionary()
        );
        AssertEq(
            DictInt(legacyResult, "damage", -1),
            10,
            "StringName-only outgoing_damage_multiplier params should scale damage."
        );
        GDictionary legacyEvent = FirstDamageEvent(legacyResult);
        AssertEq(
            DictFloat(legacyEvent, "offense_multiplier"),
            0.5f,
            "StringName-only outgoing_damage_multiplier params should be reported in offense_multiplier."
        );

        BattleUnitState formalSource = BuildUnit("formal_outgoing_multiplier_source");
        BattleUnitState formalTarget = BuildUnit("formal_outgoing_multiplier_target");
        SetStatusParams(
            formalSource,
            "formal_outgoing_multiplier",
            new GDictionary { ["outgoing_damage_multiplier"] = 0.5 }
        );
        GDictionary formalResult = resolver.resolve_effects(
            formalSource,
            formalTarget,
            new GArray { BuildDamageEffect(20) },
            new GDictionary()
        );
        AssertEq(
            DictInt(formalResult, "damage", -1),
            10,
            "String key outgoing_damage_multiplier params must still scale damage."
        );
        GDictionary formalEvent = FirstDamageEvent(formalResult);
        AssertEq(
            DictFloat(formalEvent, "offense_multiplier"),
            0.5f,
            "String key outgoing_damage_multiplier params must still be reported in offense_multiplier."
        );
    }

    private static void SetStatusParams(
        BattleUnitState unit,
        StringName statusId,
        GDictionary @params
    )
    {
        var statusEffect = new BattleStatusEffectState
        {
            status_id = statusId,
            power = 1,
            stacks = 1,
            @params = @params?.Duplicate(true).AsGodotDictionary() ?? new GDictionary(),
        };
        unit.set_status_effect(statusEffect);
    }

    private static void SetTypedStatus(
        BattleUnitState unit,
        StringName statusId,
        bool lockCrit = false
    )
    {
        var statusEffect = new BattleStatusEffectState
        {
            status_id = statusId,
            power = 1,
            stacks = 1,
            lock_crit = lockCrit,
        };
        unit.set_status_effect(statusEffect);
    }

    private static void SetAcProfile(BattleUnitState unit, int armorClass, int dodgeBonus)
    {
        unit.attribute_snapshot.set_value(AttributeService.ARMOR_CLASS_ID(), armorClass);
        unit.attribute_snapshot.set_value(AttributeService.DODGE_BONUS_ID(), dodgeBonus);
    }

    private static CombatEffectDef BuildDamageEffect(int power)
    {
        return new CombatEffectDef
        {
            effect_type = "damage",
            power = power,
            damage_tag = "physical_slash",
            @params = new GDictionary(),
        };
    }

    private static GDictionary FirstDamageEvent(GDictionary result)
    {
        GArray damageEvents = DictArray(result, "damage_events");
        if (damageEvents.Count == 0 || damageEvents[0].VariantType != Variant.Type.Dictionary)
        {
            return new GDictionary();
        }
        return damageEvents[0].AsGodotDictionary();
    }

    private static GArray DictArray(GDictionary result, string key)
    {
        if (result == null || !result.ContainsKey(key))
            return new GArray();
        Variant value = result[key];
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
    }

    private static BattleUnitState BuildUnit(StringName unitId)
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "player",
            current_ap = 2,
            current_move_points = BattleUnitState.DEFAULT_MOVE_POINTS_PER_TURN(),
            current_hp = 100,
            current_mp = 4,
            current_stamina = 4,
            current_aura = 0,
            is_alive = true,
        };
        unit.set_anchor_coord(Vector2I.Zero);
        unit.attribute_snapshot.set_value(AttributeService.HP_MAX_ID(), 100);
        unit.attribute_snapshot.set_value(AttributeService.MP_MAX_ID(), 4);
        unit.attribute_snapshot.set_value(AttributeService.STAMINA_MAX_ID(), 4);
        unit.attribute_snapshot.set_value("action_points", 2);
        unit.attribute_snapshot.set_value(AttributeService.ATTACK_BONUS_ID(), 0);
        unit.attribute_snapshot.set_value(AttributeService.ARMOR_CLASS_ID(), 10);
        unit.attribute_snapshot.set_value(AttributeService.DODGE_BONUS_ID(), 0);
        return unit;
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertFalse(bool condition, string message)
    {
        if (condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
        {
            _failures.Add($"{message} actual={actual} expected={expected}");
        }
    }

    private static int DictInt(GDictionary dictionary, string key, int fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return fallback;
        }
        return dictionary[key].AsInt32();
    }

    private static float DictFloat(GDictionary dictionary, string key)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return 0.0f;
        }
        return (float)dictionary[key].AsDouble();
    }

    private static StringName DictStringName(GDictionary dictionary, string key)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return new StringName("");
        }
        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.StringName
            ? value.AsStringName()
            : new StringName(value.AsString());
    }
}
