using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

public partial class run_battle_save_resolver_regression : SceneTree
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
            GD.PushError($"Battle save resolver regression crashed: {ex}");
            Quit(1);
        }
    }

    private int Run()
    {
        TestResolverTypeIsPlainCSharp();
        TestSaveResolverHandlesImmunityBeforeRoll();
        TestSaveResolverHandlesAdvantageAndDisadvantage();
        TestSaveResolverForcesNaturalOneAndTwenty();
        TestStatusSaveBonusUsesStringParamKeys();
        TestSaveResolverEstimatesSuccessProbability();
        TestCasterSpellSaveDcUsesSourceAbilityAndSpellProficiency();
        TestLockedSkillBonusIncreasesStaticSaveDc();
        TestLockedSkillBonusIncreasesCasterSpellSaveDc();
        TestDamageSaveSuccessHalvesPartialDamage();
        TestStatusSaveSuccessBlocksAndFailureAppliesStatus();

        if (_failures.Count == 0)
        {
            GD.Print("Battle save resolver regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle save resolver regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestResolverTypeIsPlainCSharp()
    {
        Type resolverType = typeof(BattleSaveResolver);
        AssertFalse(
            typeof(RefCounted).IsAssignableFrom(resolverType),
            "BattleSaveResolver should not inherit RefCounted."
        );
        AssertFalse(
            HasAttributeNamed(resolverType, "GlobalClassAttribute"),
            "BattleSaveResolver should not register as GlobalClass."
        );
        AssertNull(resolverType.GetMethod("resolve_save"), "Old resolve_save API should be gone.");
        AssertNull(
            resolverType.GetMethod("resolve_save_result"),
            "Old resolve_save_result API should be gone."
        );
        AssertNull(
            resolverType.GetMethod("estimate_save_success_probability"),
            "Old estimate_save_success_probability API should be gone."
        );
        AssertNull(
            resolverType.GetMethod("resolve_save_with_context"),
            "Old resolve_save_with_context API should be gone."
        );
        AssertNull(
            resolverType.GetMethod("_resolve_save_dc"),
            "Old _resolve_save_dc API should be gone."
        );
        AssertNull(
            resolverType.GetMethod("SAVE_TAG_MAGIC"),
            "Save constants should live on BattleSaveContentRules, not the resolver."
        );
        AssertPublicApiDoesNotExposeGodotPayload(typeof(BattleSaveResolver), "BattleSaveResolver");
        AssertPublicApiDoesNotExposeGodotPayload(typeof(BattleSaveSource), "BattleSaveSource");
        AssertPublicApiDoesNotExposeGodotPayload(typeof(BattleSaveResult), "BattleSaveResult");
        AssertPublicApiDoesNotExposeGodotPayload(
            typeof(BattleSaveProbabilityResult),
            "BattleSaveProbabilityResult"
        );

        BattleSaveContext context = BattleSaveContext.WithSaveRollOverrides(new[] { 2, 18 });
        object rollOverrides = context.SaveRollOverrides;
        AssertFalse(
            rollOverrides is Godot.Collections.Array,
            "BattleSaveContext should not store save roll overrides as Godot Array."
        );
    }

    private void TestSaveResolverHandlesImmunityBeforeRoll()
    {
        BattleUnitState target = MakeUnit("poison_immune_target", "player");
        target.save_advantage_tags.Add("poison_immunity");
        BattleSaveResult result = BattleSaveResolver.ResolveSaveResult(
            null,
            target,
            MakeSaveDamageEffect("poison", "constitution", 12, false),
            BattleSaveContext.WithSaveRollOverride(1)
        );

        AssertTrue(result.Immune, "poison_immunity tag should make the save immune before rolling.");
        AssertTrue(result.Success, "Immune save should count as success.");
        AssertEq(result.NaturalRoll, 0, "Immune save should not roll.");
    }

    private void TestSaveResolverHandlesAdvantageAndDisadvantage()
    {
        BattleUnitState advantageTarget = MakeUnit("advantage_target", "player");
        advantageTarget.save_advantage_tags.Add("poison");
        BattleSaveResult advantageResult = BattleSaveResolver.ResolveSaveResult(
            null,
            advantageTarget,
            MakeSaveDamageEffect("poison", "constitution", 15, false),
            BattleSaveContext.WithSaveRollOverrides(new[] { 2, 18 })
        );
        AssertEq(advantageResult.NaturalRoll, 18, "Direct save tag should grant advantage.");
        AssertTrue(advantageResult.Success, "Advantage should use the higher override roll.");

        BattleUnitState disadvantageTarget = MakeUnit("disadvantage_target", "player");
        disadvantageTarget.save_advantage_tags.Add("poison_disadvantage");
        BattleSaveResult disadvantageResult = BattleSaveResolver.ResolveSaveResult(
            null,
            disadvantageTarget,
            MakeSaveDamageEffect("poison", "constitution", 15, false),
            BattleSaveContext.WithSaveRollOverrides(new[] { 18, 2 })
        );
        AssertEq(disadvantageResult.NaturalRoll, 2, "save_tag_disadvantage should use the lower roll.");
        AssertFalse(disadvantageResult.Success, "Disadvantage should fail with the lower override roll.");
    }

    private void TestSaveResolverForcesNaturalOneAndTwenty()
    {
        BattleUnitState target = MakeUnit("natural_save_target", "player");
        target.attribute_snapshot.set_value("constitution", 30);
        BattleSaveResult naturalOneResult = BattleSaveResolver.ResolveSaveResult(
            null,
            target,
            MakeSaveDamageEffect("poison", "constitution", 5, false),
            BattleSaveContext.WithSaveRollOverride(1)
        );
        AssertFalse(naturalOneResult.Success, "Natural 1 should force save failure.");

        target.attribute_snapshot.set_value("constitution", 1);
        BattleSaveResult naturalTwentyResult = BattleSaveResolver.ResolveSaveResult(
            null,
            target,
            MakeSaveDamageEffect("poison", "constitution", 40, false),
            BattleSaveContext.WithSaveRollOverride(20)
        );
        AssertTrue(naturalTwentyResult.Success, "Natural 20 should force save success.");
    }

    private void TestStatusSaveBonusUsesStringParamKeys()
    {
        BattleUnitState target = MakeUnit("save_bonus_target", "player");
        target.set_status_effect(
            new BattleStatusEffectState
            {
                status_id = "control_save_bonus_status",
                source_unit_id = "test_source",
                power = 1,
                stacks = 1,
                duration = -1,
                @params = new Godot.Collections.Dictionary
                {
                    ["save_bonus"] = 2,
                    ["control_save_bonus"] = 3,
                },
            }
        );

        BattleSaveResult result = BattleSaveResolver.ResolveSaveResult(
            null,
            target,
            MakeSaveDamageEffect(BattleSaveContentRules.SAVE_TAG_SLEEP, "willpower", 14, false),
            BattleSaveContext.WithSaveRollOverride(10)
        );

        AssertEq(result.Bonus, 3, "Control save should use the highest string-key status save bonus.");
        AssertEq(result.RollTotal, 13, "Control save roll total should include control_save_bonus.");
        AssertFalse(result.Success, "Raised but still below-DC control save should fail.");
    }

    private void TestSaveResolverEstimatesSuccessProbability()
    {
        BattleUnitState target = MakeUnit("save_probability_target", "player");
        BattleSaveProbabilityResult normalProbability =
            BattleSaveResolver.EstimateSaveSuccessProbabilityResult(
                null,
                target,
                MakeSaveDamageEffect("poison", "constitution", 11, false)
            );
        AssertEq(
            normalProbability.SuccessProbabilityBasisPoints,
            5000,
            "DC11 with no modifier should have 50% normal d20 save success."
        );

        target.save_advantage_tags.Add("poison");
        BattleSaveProbabilityResult advantageProbability =
            BattleSaveResolver.EstimateSaveSuccessProbabilityResult(
                null,
                target,
                MakeSaveDamageEffect("poison", "constitution", 15, false)
            );
        AssertEq(
            advantageProbability.SuccessProbabilityBasisPoints,
            5100,
            "DC15 with no modifier should have 51% advantage save success."
        );

        target.save_advantage_tags.Clear();
        target.save_advantage_tags.Add("poison_disadvantage");
        BattleSaveProbabilityResult disadvantageProbability =
            BattleSaveResolver.EstimateSaveSuccessProbabilityResult(
                null,
                target,
                MakeSaveDamageEffect("poison", "constitution", 15, false)
            );
        AssertEq(
            disadvantageProbability.SuccessProbabilityBasisPoints,
            900,
            "DC15 with no modifier should have 9% disadvantage save success."
        );

        target.save_advantage_tags.Clear();
        target.save_advantage_tags.Add("poison_immunity");
        BattleSaveProbabilityResult immuneProbability =
            BattleSaveResolver.EstimateSaveSuccessProbabilityResult(
                null,
                target,
                MakeSaveDamageEffect("poison", "constitution", 40, false)
            );
        AssertEq(
            immuneProbability.SuccessProbabilityBasisPoints,
            10000,
            "Immune save should estimate as 100% success."
        );
    }

    private void TestCasterSpellSaveDcUsesSourceAbilityAndSpellProficiency()
    {
        BattleUnitState source = MakeUnit("spell_dc_source", "enemy");
        source.attribute_snapshot.set_value("intelligence", 18);
        source.attribute_snapshot.set_value(AttributeService.SPELL_PROFICIENCY_BONUS_ID(), 3);
        BattleUnitState target = MakeUnit("spell_dc_target", "player");
        target.attribute_snapshot.set_value("agility", 10);
        CombatEffectDef effect = MakeCasterSpellSaveDamageEffect();

        BattleSaveResult failedResult = BattleSaveResolver.ResolveSaveResult(
            source,
            target,
            effect,
            BattleSaveContext.WithSaveRollOverride(14)
        );
        AssertEq(failedResult.Dc, 15, "caster_spell DC should be 8 + INT modifier 4 + spell proficiency 3.");
        AssertEq(failedResult.RollTotal, 14, "Agility 10 target save total should equal the d20.");
        AssertFalse(failedResult.Success, "Below dynamic DC agility save should fail.");

        BattleSaveResult successResult = BattleSaveResolver.ResolveSaveResult(
            source,
            target,
            effect,
            BattleSaveContext.WithSaveRollOverride(15)
        );
        AssertTrue(successResult.Success, "Meeting dynamic DC should succeed.");
    }

    private void TestLockedSkillBonusIncreasesStaticSaveDc()
    {
        BattleUnitState source = MakeUnit("locked_static_source", "enemy");
        source.known_skill_lock_hit_bonus_map["locked_fire"] = 2;
        BattleUnitState target = MakeUnit("locked_static_target", "player");
        target.attribute_snapshot.set_value("constitution", 10);
        CombatEffectDef effect = MakeSaveDamageEffect("fireball", "constitution", 12, false);

        BattleSaveResult result = BattleSaveResolver.ResolveSaveResult(
            source,
            target,
            effect,
            BattleSaveContext.WithSaveRollOverride(13, "locked_fire")
        );
        AssertEq(result.Dc, 14, "Locked skill bonus should raise static save DC.");
        AssertFalse(result.Success, "Target save should use the raised locked-skill DC.");
    }

    private void TestLockedSkillBonusIncreasesCasterSpellSaveDc()
    {
        BattleUnitState source = MakeUnit("locked_spell_source", "enemy");
        source.attribute_snapshot.set_value("intelligence", 18);
        source.attribute_snapshot.set_value(AttributeService.SPELL_PROFICIENCY_BONUS_ID(), 3);
        source.known_skill_lock_hit_bonus_map["locked_spell"] = 2;
        BattleUnitState target = MakeUnit("locked_spell_target", "player");
        CombatEffectDef effect = MakeCasterSpellSaveDamageEffect();

        BattleSaveResult result = BattleSaveResolver.ResolveSaveResult(
            source,
            target,
            effect,
            BattleSaveContext.WithSaveRollOverride(16, "locked_spell")
        );
        AssertEq(result.Dc, 17, "Locked skill bonus should raise dynamic caster save DC.");
        AssertFalse(result.Success, "Raised dynamic DC should make the target save fail.");
    }

    private void TestDamageSaveSuccessHalvesPartialDamage()
    {
        var resolver = new BattleDamageResolver();
        BattleUnitState source = MakeUnit("breath_source", "enemy");
        BattleUnitState target = MakeUnit("breath_target", "player");
        CombatEffectDef effect = MakeSaveDamageEffect("dragon_breath", "constitution", 12, true);
        effect.power = 10;
        Godot.Collections.Dictionary result = resolver.resolve_effects(
            source,
            target,
            new Godot.Collections.Array { effect },
            new Godot.Collections.Dictionary { ["save_roll_override"] = 20 }
        );

        AssertEq(DictInt(result, "damage", -1), 5, "Successful partial damage save should halve damage.");
        Godot.Collections.Dictionary @event = FirstDamageEvent(result);
        AssertTrue(DictBool(@event, "save_success"), "Damage event should record save success.");
        AssertTrue(DictBool(@event, "save_partial_applied"), "Damage event should record partial save application.");
        AssertEq(DictInt(@event, "pre_save_damage", -1), 10, "Damage event should preserve pre-save damage.");
        AssertEq(DictInt(@event, "save_adjusted_damage", -1), 5, "Damage event should record adjusted save damage.");
    }

    private void TestStatusSaveSuccessBlocksAndFailureAppliesStatus()
    {
        var resolver = new BattleDamageResolver();
        BattleUnitState source = MakeUnit("status_source", "enemy");
        BattleUnitState successTarget = MakeUnit("status_success_target", "player");
        CombatEffectDef effect = MakeSaveStatusEffect("sleep", "asleep", "deep_sleep");
        Godot.Collections.Dictionary successResult = resolver.resolve_effects(
            source,
            successTarget,
            new Godot.Collections.Array { effect },
            new Godot.Collections.Dictionary { ["save_roll_override"] = 20 }
        );
        AssertFalse(successTarget.has_status_effect("asleep"), "Successful save should block default status.");
        AssertFalse(successTarget.has_status_effect("deep_sleep"), "Successful save should block failure status.");
        AssertFalse(DictBool(successResult, "applied", true), "Blocked status save should not mark effect applied.");

        BattleUnitState failureTarget = MakeUnit("status_failure_target", "player");
        Godot.Collections.Dictionary failureResult = resolver.resolve_effects(
            source,
            failureTarget,
            new Godot.Collections.Array { effect },
            new Godot.Collections.Dictionary { ["save_roll_override"] = 1 }
        );
        AssertTrue(failureTarget.has_status_effect("deep_sleep"), "Failed save should apply save_failure_status_id.");
        AssertFalse(failureTarget.has_status_effect("asleep"), "save_failure_status_id should replace default status on failure.");
        AssertTrue(DictBool(failureResult, "applied"), "Failed status save should mark effect applied.");
        AssertTrue(
            StringNameArrayHas(DictArray(failureResult, "status_effect_ids"), "deep_sleep"),
            "Result should report applied failure status id."
        );
    }

    private static CombatEffectDef MakeSaveDamageEffect(
        StringName saveTag,
        StringName saveAbility,
        int saveDc,
        bool partial
    )
    {
        return new CombatEffectDef
        {
            effect_type = "damage",
            damage_tag = "fire",
            power = 10,
            save_dc = saveDc,
            save_ability = saveAbility,
            save_tag = saveTag,
            save_partial_on_success = partial,
        };
    }

    private static CombatEffectDef MakeSaveStatusEffect(
        StringName saveTag,
        StringName statusId,
        StringName failureStatusId
    )
    {
        return new CombatEffectDef
        {
            effect_type = "status",
            status_id = statusId,
            save_failure_status_id = failureStatusId,
            save_dc = 12,
            save_ability = "willpower",
            save_tag = saveTag,
        };
    }

    private static CombatEffectDef MakeCasterSpellSaveDamageEffect()
    {
        return new CombatEffectDef
        {
            effect_type = "damage",
            damage_tag = "fire",
            power = 10,
            save_dc_mode = BattleSaveContentRules.SAVE_DC_MODE_CASTER_SPELL,
            save_dc_source_ability = "intelligence",
            save_ability = "agility",
            save_tag = BattleSaveContentRules.SAVE_TAG_FIREBALL,
            save_partial_on_success = true,
        };
    }

    private static BattleUnitState MakeUnit(StringName unitId, StringName factionId)
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            control_mode = "manual",
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
        unit.attribute_snapshot.set_value("constitution", 10);
        unit.attribute_snapshot.set_value("intelligence", 10);
        unit.attribute_snapshot.set_value("willpower", 10);
        return unit;
    }

    private static Godot.Collections.Dictionary FirstDamageEvent(Godot.Collections.Dictionary result)
    {
        Godot.Collections.Array events = DictArray(result, "damage_events");
        if (events.Count == 0 || events[0].VariantType != Variant.Type.Dictionary)
        {
            return new Godot.Collections.Dictionary();
        }
        return events[0].AsGodotDictionary();
    }

    private static bool StringNameArrayHas(Godot.Collections.Array values, StringName needle)
    {
        foreach (Variant value in values)
        {
            if (ProgressionDataUtils.to_string_name(value) == needle)
            {
                return true;
            }
        }
        return false;
    }

    private static Godot.Collections.Array DictArray(Godot.Collections.Dictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
        {
            return new Godot.Collections.Array();
        }
        return data[key].AsGodotArray();
    }

    private static int DictInt(Godot.Collections.Dictionary data, string key, int fallback = 0)
    {
        if (data == null || !data.ContainsKey(key))
        {
            return fallback;
        }
        return data[key].AsInt32();
    }

    private static bool DictBool(Godot.Collections.Dictionary data, string key, bool fallback = false)
    {
        if (data == null || !data.ContainsKey(key))
        {
            return fallback;
        }
        return data[key].AsBool();
    }

    private static bool HasAttributeNamed(Type type, string attributeTypeName)
    {
        foreach (object attribute in type.GetCustomAttributes(false))
        {
            if (attribute.GetType().Name == attributeTypeName)
            {
                return true;
            }
        }
        return false;
    }

    private void AssertPublicApiDoesNotExposeGodotPayload(Type type, string label)
    {
        foreach (MethodInfo method in type.GetMethods(
                     BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly
                 ))
        {
            AssertFalse(
                IsGodotPayloadType(method.ReturnType),
                $"{label}.{method.Name}() should not publicly return Godot Dictionary/Array/Variant."
            );
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                AssertFalse(
                    IsGodotPayloadType(parameter.ParameterType),
                    $"{label}.{method.Name}({parameter.Name}) should not publicly accept Godot Dictionary/Array/Variant."
                );
            }
        }

        foreach (PropertyInfo property in type.GetProperties(
                     BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly
                 ))
        {
            AssertFalse(
                IsGodotPayloadType(property.PropertyType),
                $"{label}.{property.Name} should not publicly expose Godot Dictionary/Array/Variant."
            );
        }

        foreach (FieldInfo field in type.GetFields(
                     BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly
                 ))
        {
            AssertFalse(
                IsGodotPayloadType(field.FieldType),
                $"{label}.{field.Name} should not publicly expose Godot Dictionary/Array/Variant."
            );
        }
    }

    private static bool IsGodotPayloadType(Type type)
    {
        if (type.IsByRef || type.IsPointer || type.IsArray)
        {
            type = type.GetElementType() ?? type;
        }
        if (type == typeof(Variant))
        {
            return true;
        }
        string typeName = type.FullName ?? "";
        if (
            typeName.StartsWith("Godot.Collections.Dictionary", StringComparison.Ordinal)
            || typeName.StartsWith("Godot.Collections.Array", StringComparison.Ordinal)
        )
        {
            return true;
        }
        if (type.IsGenericType)
        {
            foreach (Type genericArgument in type.GetGenericArguments())
            {
                if (IsGodotPayloadType(genericArgument))
                {
                    return true;
                }
            }
        }
        return false;
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
        AssertTrue(!condition, message);
    }

    private void AssertNull(object value, string message)
    {
        if (value != null)
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
}
