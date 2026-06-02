using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_damage_resolver_preview_contract_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        try
        {
            int exitCode = Run();
            Quit(exitCode);
        }
        catch (Exception ex)
        {
            GD.PushError($"Battle damage resolver preview contract regression crashed: {ex}");
            Quit(1);
        }
    }

    private int Run()
    {
        TestPreviewDamageEffectUsesSharedDamageMathWithoutMutatingUnits();
        TestPreviewDamageEffectUsesSaveProbabilityWithoutRolling();
        TestAttributeScaledRecoveryDiceUseFormalFields();

        if (_failures.Count == 0)
        {
            GD.Print("Battle damage resolver preview contract regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle damage resolver preview contract regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestPreviewDamageEffectUsesSharedDamageMathWithoutMutatingUnits()
    {
        var resolver = new BattleDamageResolver();
        BattleUnitState source = MakeUnit("preview_source", "player");
        BattleUnitState target = MakeUnit("preview_target", "enemy");
        SetStatus(source, "attack_up", 2, new GDictionary());
        SetStatus(target, "damage_reduction_up", 1, new GDictionary());
        target.damage_resistances["fire"] = "half";
        target.current_shield_hp = 5;
        target.shield_max_hp = 5;
        target.shield_duration = 100;
        CombatEffectDef effect = MakeDamageEffect("fire", 10);
        effect.dice_count = 2;
        effect.dice_sides = 6;

        GDictionary expectedPreview = resolver.preview_damage_effect(
            source,
            target,
            effect,
            new GDictionary(),
            BattleDamageResolver.DAMAGE_PREVIEW_ROLL_MODE_AVERAGE(),
            BattleDamageResolver.DAMAGE_PREVIEW_SAVE_MODE_EXPECTED()
        );
        GDictionary expectedOutcome = DictDictionary(expectedPreview, "damage_outcome");
        AssertEq(DictInt(expectedOutcome, "rolled_damage", -1), 20, "Average preview should reuse offense-multiplied rolled_damage.");
        AssertStringNameEq(DictStringName(expectedOutcome, "mitigation_tier"), "half", "Average preview should reuse mitigation tier.");
        AssertEq(DictInt(expectedOutcome, "fixed_mitigation_total", -1), 2, "Average preview should reuse fixed mitigation.");
        AssertEq(DictInt(expectedPreview, "post_save_damage", -1), 8, "Average preview post-save damage should come from shared outcome.");
        AssertEq(DictInt(expectedPreview, "shield_absorbed", -1), 5, "Average preview should use shared shield absorption.");
        AssertEq(DictInt(expectedPreview, "hp_damage", -1), 3, "Average preview hp_damage should subtract absorbed shield.");

        GDictionary worstPreview = resolver.preview_damage_effect(
            source,
            target,
            effect,
            new GDictionary(),
            BattleDamageResolver.DAMAGE_PREVIEW_ROLL_MODE_MAXIMUM(),
            BattleDamageResolver.DAMAGE_PREVIEW_SAVE_MODE_WORST()
        );
        AssertEq(DictInt(worstPreview, "post_save_damage", -1), 11, "Worst preview should use max dice and same mitigation chain.");
        AssertEq(DictInt(worstPreview, "hp_damage", -1), 6, "Worst preview should resolve hp damage on cloned shield state.");
        AssertEq(target.current_hp, 30, "Preview should not mutate target HP.");
        AssertEq(target.current_shield_hp, 5, "Preview should not mutate target shield.");
        AssertTrue(target.has_status_effect("damage_reduction_up"), "Preview should not mutate target statuses.");
        AssertTrue(source.has_status_effect("attack_up"), "Preview should not mutate source statuses.");
    }

    private void TestPreviewDamageEffectUsesSaveProbabilityWithoutRolling()
    {
        var resolver = new BattleDamageResolver();
        BattleUnitState source = MakeUnit("save_preview_source", "player");
        BattleUnitState target = MakeUnit("save_preview_target", "enemy");
        CombatEffectDef effect = MakeDamageEffect("fire", 20);
        effect.save_dc = 10;
        effect.save_ability = "agility";
        effect.save_tag = BattleSaveContentRules.SAVE_TAG_MAGIC;
        effect.save_partial_on_success = true;

        GDictionary preview = resolver.preview_damage_effect(
            source,
            target,
            effect,
            new GDictionary { ["save_roll_override"] = 20 },
            BattleDamageResolver.DAMAGE_PREVIEW_ROLL_MODE_AVERAGE(),
            BattleDamageResolver.DAMAGE_PREVIEW_SAVE_MODE_EXPECTED()
        );
        GDictionary saveEstimate = DictDictionary(preview, "save_estimate");
        AssertTrue(DictBool(saveEstimate, "has_save"), "Save preview should output save_estimate.");
        AssertEq(
            DictInt(saveEstimate, "save_success_probability_basis_points", -1),
            10000,
            "save_roll_override=20 should become 100% success probability."
        );
        AssertEq(DictInt(preview, "post_save_damage", -1), 10, "Successful partial save should halve damage.");
        AssertEq(target.current_hp, 30, "Save preview should not mutate the target by rolling a real save.");
    }

    private void TestAttributeScaledRecoveryDiceUseFormalFields()
    {
        var resolver = new BattleDamageResolver();
        BattleUnitState source = MakeUnit("recovery_source", "player");
        source.attribute_snapshot.set_value("constitution", 12);
        source.attribute_snapshot.set_value("constitution_modifier", 1);
        source.attribute_snapshot.set_value("willpower", 14);
        source.attribute_snapshot.set_value("willpower_modifier", 2);

        BattleUnitState healTarget = MakeUnit("heal_target", "player");
        healTarget.current_hp = 10;
        var healEffect = new CombatEffectDef
        {
            effect_type = "heal",
            effect_target_team_filter = "ally",
            dice_count = 2,
            dice_sides_base = 4,
            dice_sides_per_constitution_mod = 1,
            dice_sides_per_willpower_mod = 1,
        };
        GDictionary healResult = resolver.resolve_effects(
            source,
            healTarget,
            new Godot.Collections.Array { healEffect },
            new GDictionary()
        );
        int healing = DictInt(healResult, "healing");
        AssertTrue(healing >= 2 && healing <= 14, "Healing should use typed 2D(4+CON+WILL) dice sides.");
        AssertEq(healTarget.current_hp, 10 + healing, "Typed healing dice should write back HP.");

        BattleUnitState staminaTarget = MakeUnit("stamina_target", "player");
        staminaTarget.current_stamina = 0;
        staminaTarget.attribute_snapshot.set_value(AttributeService.STAMINA_MAX_ID(), 30);
        var staminaEffect = new CombatEffectDef
        {
            effect_type = "stamina_restore",
            effect_target_team_filter = "ally",
            dice_count = 2,
            dice_sides_base = 4,
            dice_sides_per_constitution_mod = 1,
            dice_sides_per_willpower_mod = 1,
        };
        resolver.resolve_effects(
            source,
            staminaTarget,
            new Godot.Collections.Array { staminaEffect },
            new GDictionary()
        );
        AssertTrue(
            staminaTarget.current_stamina >= 2 && staminaTarget.current_stamina <= 14,
            "Stamina restore should use typed attribute-scaled dice sides."
        );

        var shieldService = new BattleShieldService();
        var shieldEffect = new CombatEffectDef
        {
            effect_type = "shield",
            dice_count = 2,
            dice_sides_base = 4,
            dice_sides_per_constitution_mod = 1,
            dice_sides_per_willpower_mod = 1,
        };
        AssertTrue(shieldService._has_shield_dice_config(shieldEffect), "Shield service should detect typed attribute-scaled dice.");
        int shieldHp = shieldService._resolve_shield_hp(source, shieldEffect, new GDictionary());
        AssertTrue(shieldHp >= 2 && shieldHp <= 14, "Shield HP should use typed attribute-scaled dice.");
    }

    private static CombatEffectDef MakeDamageEffect(StringName damageTag, int power)
    {
        return new CombatEffectDef
        {
            effect_type = "damage",
            damage_tag = damageTag,
            power = power,
            @params = new GDictionary(),
        };
    }

    private static BattleUnitState MakeUnit(StringName unitId, StringName factionId)
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            current_hp = 30,
            current_mp = 0,
            current_ap = 2,
            current_stamina = 20,
            is_alive = true,
        };
        unit.attribute_snapshot.set_value(AttributeService.HP_MAX_ID(), 30);
        unit.attribute_snapshot.set_value(AttributeService.MP_MAX_ID(), 0);
        unit.attribute_snapshot.set_value(AttributeService.ACTION_POINTS_ID(), 2);
        unit.attribute_snapshot.set_value(AttributeService.ATTACK_BONUS_ID(), 10);
        unit.attribute_snapshot.set_value(AttributeService.ARMOR_CLASS_ID(), 0);
        unit.attribute_snapshot.set_value("agility", 10);
        unit.attribute_snapshot.set_value("agility_modifier", 0);
        return unit;
    }

    private static void SetStatus(
        BattleUnitState unit,
        StringName statusId,
        int power,
        GDictionary @params
    )
    {
        var status = new BattleStatusEffectState
        {
            status_id = statusId,
            source_unit_id = unit.unit_id,
            power = power,
            stacks = power,
            duration = -1,
            @params = @params?.Duplicate(true) ?? new GDictionary(),
        };
        unit.set_status_effect(status);
    }

    private static GDictionary DictDictionary(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key) || data[key].VariantType != Variant.Type.Dictionary)
        {
            return new GDictionary();
        }
        return data[key].AsGodotDictionary();
    }

    private static int DictInt(GDictionary data, string key, int fallback = 0)
    {
        if (data == null || !data.ContainsKey(key))
        {
            return fallback;
        }
        return data[key].AsInt32();
    }

    private static bool DictBool(GDictionary data, string key, bool fallback = false)
    {
        if (data == null || !data.ContainsKey(key))
        {
            return fallback;
        }
        return data[key].AsBool();
    }

    private static StringName DictStringName(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
        {
            return "";
        }
        return ProgressionDataUtils.to_string_name(data[key]);
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertEq(int actual, int expected, string message)
    {
        if (actual != expected)
        {
            _failures.Add($"{message} | actual={actual} expected={expected}");
        }
    }

    private void AssertStringNameEq(StringName actual, StringName expected, string message)
    {
        if (actual != expected)
        {
            _failures.Add($"{message} | actual={actual} expected={expected}");
        }
    }
}
