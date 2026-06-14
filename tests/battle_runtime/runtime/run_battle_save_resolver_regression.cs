using System;
using System.Collections.Generic;
using Godot;

public partial class run_battle_save_resolver_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestSaveResolverHandlesImmunityBeforeRoll();
            TestSaveResolverHandlesAdvantageAndDisadvantage();
            TestSaveResolverForcesNaturalOneAndTwenty();
            TestSaveResolverResolvesSaveDegree();
            TestStatusSaveBonusUsesTypedFields();
            TestPerTagSaveBonusCoexistsWithStatusSaveBonuses();
            TestStatusSaveTagsUseTypedFields();
            TestSaveResolverEstimatesSuccessProbability();
            TestCasterSpellSaveDcUsesSourceAbilityAndSpellProficiency();
            TestLockedSkillBonusIncreasesStaticSaveDc();
            TestLockedSkillBonusIncreasesCasterSpellSaveDc();
            TestDamageSaveSuccessHalvesPartialDamage();
            TestStatusSaveSuccessBlocksAndFailureAppliesStatus();
            Quit(_test.Finish("Battle save resolver regression"));
        }
        catch (Exception ex)
        {
            GD.PushError($"Battle save resolver regression crashed: {ex}");
            Quit(1);
        }
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

        _test.True(result.Immune, "poison_immunity tag should make the save immune before rolling.");
        _test.True(result.Success, "Immune save should count as success.");
        _test.Eq(result.NaturalRoll, 0, "Immune save should not roll.");
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
        _test.Eq(advantageResult.NaturalRoll, 18, "Direct save tag should grant advantage.");
        _test.True(advantageResult.Success, "Advantage should use the higher override roll.");

        BattleUnitState disadvantageTarget = MakeUnit("disadvantage_target", "player");
        disadvantageTarget.save_advantage_tags.Add("poison_disadvantage");
        BattleSaveResult disadvantageResult = BattleSaveResolver.ResolveSaveResult(
            null,
            disadvantageTarget,
            MakeSaveDamageEffect("poison", "constitution", 15, false),
            BattleSaveContext.WithSaveRollOverrides(new[] { 18, 2 })
        );
        _test.Eq(disadvantageResult.NaturalRoll, 2, "save_tag_disadvantage should use the lower roll.");
        _test.False(disadvantageResult.Success, "Disadvantage should fail with the lower override roll.");
    }

    private void TestSaveResolverForcesNaturalOneAndTwenty()
    {
        BattleUnitState target = MakeUnit("natural_save_target", "player");
        target.attribute_snapshot.SetValue("constitution", 30);
        BattleSaveResult naturalOneResult = BattleSaveResolver.ResolveSaveResult(
            null,
            target,
            MakeSaveDamageEffect("poison", "constitution", 5, false),
            BattleSaveContext.WithSaveRollOverride(1)
        );
        _test.False(naturalOneResult.Success, "Natural 1 should force save failure.");

        target.attribute_snapshot.SetValue("constitution", 1);
        BattleSaveResult naturalTwentyResult = BattleSaveResolver.ResolveSaveResult(
            null,
            target,
            MakeSaveDamageEffect("poison", "constitution", 40, false),
            BattleSaveContext.WithSaveRollOverride(20)
        );
        _test.True(naturalTwentyResult.Success, "Natural 20 should force save success.");
    }

    private void TestSaveResolverResolvesSaveDegree()
    {
        RequireDegree(
            BattleSaveResolver.ResolveSaveDegree(10, 14, 14),
            BattleSaveDegreeKind.Success,
            "total >= DC 应为 Success。"
        );
        RequireDegree(
            BattleSaveResolver.ResolveSaveDegree(10, 13, 14),
            BattleSaveDegreeKind.Failure,
            "total < DC 应为 Failure。"
        );
        RequireDegree(
            BattleSaveResolver.ResolveSaveDegree(20, 25, 14),
            BattleSaveDegreeKind.CriticalSuccess,
            "natural 20 应把 Success 升级为 CriticalSuccess。"
        );
        RequireDegree(
            BattleSaveResolver.ResolveSaveDegree(20, 10, 14),
            BattleSaveDegreeKind.Success,
            "natural 20 应把 Failure 升级为 Success。"
        );
        RequireDegree(
            BattleSaveResolver.ResolveSaveDegree(1, 5, 14),
            BattleSaveDegreeKind.CriticalFailure,
            "natural 1 应把 Failure 降级为 CriticalFailure。"
        );
        RequireDegree(
            BattleSaveResolver.ResolveSaveDegree(1, 20, 14),
            BattleSaveDegreeKind.Failure,
            "natural 1 应把 Success 降级为 Failure。"
        );

        BattleUnitState target = MakeUnit("save_degree_target", "player");
        BattleSaveResult rolledResult = BattleSaveResolver.ResolveSaveResult(
            null,
            target,
            MakeSaveDamageEffect("poison", "constitution", 12, false),
            BattleSaveContext.WithSaveRollOverride(12)
        );
        RequireDegree(
            rolledResult.Degree,
            BattleSaveDegreeKind.Success,
            "ResolveSaveResult 应携带按 natural/total/DC 解析出的 Degree。"
        );

        target.save_advantage_tags.Add("poison_immunity");
        BattleSaveResult immuneResult = BattleSaveResolver.ResolveSaveResult(
            null,
            target,
            MakeSaveDamageEffect("poison", "constitution", 12, false),
            BattleSaveContext.WithSaveRollOverride(1)
        );
        RequireDegree(
            immuneResult.Degree,
            BattleSaveDegreeKind.CriticalSuccess,
            "immune save 的 Degree 应为 CriticalSuccess。"
        );
    }

    private void TestStatusSaveBonusUsesTypedFields()
    {
        BattleUnitState target = MakeUnit("save_bonus_target", "player");
        target.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "control_save_bonus_status",
                source_unit_id = "test_source",
                power = 1,
                stacks = 1,
                duration = -1,
                save_bonus = 2,
                control_save_bonus = 3,
            }
        );

        BattleSaveResult result = BattleSaveResolver.ResolveSaveResult(
            null,
            target,
            MakeSaveDamageEffect(BattleSaveContentRules.ToStringName(BattleSaveTagKind.Sleep), "willpower", 14, false),
            BattleSaveContext.WithSaveRollOverride(10)
        );

        _test.Eq(result.Bonus, 3, "Control save should use the highest typed status save bonus.");
        _test.Eq(result.RollTotal, 13, "Control save roll total should include control_save_bonus.");
        _test.False(result.Success, "Raised but still below-DC control save should fail.");

        using SkillContentRegistry registry = new();
        using CombatEffectDef effect = new()
        {
            effect_type = "status",
            status_id = "willpower_save_bonus_up",
            @params = new Godot.Collections.Dictionary { ["control_save_bonus"] = 1 },
        };
        var errors = new Godot.Collections.Array<string>();
        registry.AppendEffectValidationErrors(
            errors,
            "legacy_control_save_bonus",
            effect,
            "test_effect"
        );
        _test.True(errors.Count > 0, $"旧 params.control_save_bonus 应被 SkillContentRegistry 静态拒绝。 errors={FormatErrors(errors)}");
    }

    private void TestPerTagSaveBonusCoexistsWithStatusSaveBonuses()
    {
        BattleUnitState target = MakeUnit("per_tag_save_bonus_target", "player");
        BattleTemporalStatusService.ApplyTimeReverberation(target, "temporal_caster");
        target.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "willpower_save_bonus_up",
                source_unit_id = "test_source",
                power = 1,
                stacks = 1,
                duration = -1,
                save_bonus = 1,
                control_save_bonus = 3,
            }
        );

        StringName temporalTag = BattleSaveContentRules.ToStringName(BattleSaveTagKind.Temporal);
        BattleSaveResult temporalResult = BattleSaveResolver.ResolveSaveResult(
            null,
            target,
            MakeSaveDamageEffect(temporalTag, "willpower", 15, false),
            BattleSaveContext.WithSaveRollOverride(10)
        );
        _test.Eq(
            temporalResult.Bonus,
            BattleTemporalStatusService.ReverberationTemporalSaveBonus,
            "temporal save 应与 save_bonus/control_save_bonus 用同一套 Math.Max 合成，而不是求和。"
        );
        _test.Eq(
            temporalResult.RollTotal,
            10 + BattleTemporalStatusService.ReverberationTemporalSaveBonus,
            "temporal save roll total 只应计入一次最大 bonus。"
        );
        _test.True(
            temporalResult.Success,
            "time_reverberation 的 per-tag bonus 应抬高 temporal save。"
        );

        BattleSaveResult sleepResult = BattleSaveResolver.ResolveSaveResult(
            null,
            target,
            MakeSaveDamageEffect(
                BattleSaveContentRules.ToStringName(BattleSaveTagKind.Sleep),
                "willpower",
                14,
                false
            ),
            BattleSaveContext.WithSaveRollOverride(10)
        );
        _test.Eq(sleepResult.Bonus, 3, "非 temporal control save 不应吃到 temporal per-tag bonus。");

        BattleSaveResult poisonResult = BattleSaveResolver.ResolveSaveResult(
            null,
            target,
            MakeSaveDamageEffect("poison", "constitution", 14, false),
            BattleSaveContext.WithSaveRollOverride(10)
        );
        _test.Eq(poisonResult.Bonus, 1, "非 control save 只应使用 save_bonus。");
    }

    private void TestStatusSaveTagsUseTypedFields()
    {
        BattleUnitState typedTarget = MakeUnit("typed_save_tag_target", "player");
        typedTarget.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "poison_focus",
                source_unit_id = "test_source",
                power = 1,
                stacks = 1,
                duration = -1,
                save_advantage_tags = new List<StringName> { "poison" },
            }
        );

        BattleSaveResult typedResult = BattleSaveResolver.ResolveSaveResult(
            null,
            typedTarget,
            MakeSaveDamageEffect("poison", "constitution", 15, false),
            BattleSaveContext.WithSaveRollOverrides(new[] { 2, 18 })
        );
        _test.Eq(typedResult.NaturalRoll, 18, "Typed status save_advantage_tags 应驱动 advantage。");
        _test.True(typedResult.Success, "Typed status save_advantage_tags 应使用更高的 override roll。");

        BattleUnitState legacyTarget = MakeUnit("legacy_save_tag_target", "player");
        legacyTarget.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "legacy_poison_focus",
                source_unit_id = "test_source",
                power = 1,
                stacks = 1,
                duration = -1,
                @params = new Godot.Collections.Dictionary
                {
                    ["save_advantage_tags"] = new Godot.Collections.Array<StringName> { "poison" },
                },
            }
        );

        BattleSaveResult legacyResult = BattleSaveResolver.ResolveSaveResult(
            null,
            legacyTarget,
            MakeSaveDamageEffect("poison", "constitution", 15, false),
            BattleSaveContext.WithSaveRollOverrides(new[] { 2, 18 })
        );
        _test.Eq(
            legacyResult.NaturalRoll,
            2,
            "Legacy params.save_advantage_tags 不应继续驱动正式 save advantage 逻辑。"
        );
        _test.False(
            legacyResult.Success,
            "Legacy params.save_advantage_tags 不应继续让目标使用 advantage save。"
        );

        using SkillContentRegistry registry = new();
        using CombatEffectDef effect = new()
        {
            effect_type = "status",
            status_id = "legacy_save_tag_status",
            @params = new Godot.Collections.Dictionary
            {
                ["save_advantage_tags"] = new Godot.Collections.Array<StringName> { "poison" },
                ["save_disadvantage_tags"] = new Godot.Collections.Array<StringName> { "fear" },
                ["save_immunity_tags"] = new Godot.Collections.Array<StringName> { "sleep" },
                ["save_tags"] = new Godot.Collections.Array<StringName> { "charm" },
            },
        };
        var errors = new Godot.Collections.Array<string>();
        registry.AppendEffectValidationErrors(errors, "legacy_save_tags", effect, "test_effect");
        _test.True(errors.Count >= 4, $"旧 params save tags 应被 SkillContentRegistry 静态拒绝。 errors={FormatErrors(errors)}");
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
        _test.Eq(
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
        _test.Eq(
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
        _test.Eq(
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
        _test.Eq(
            immuneProbability.SuccessProbabilityBasisPoints,
            10000,
            "Immune save should estimate as 100% success."
        );
    }

    private void TestCasterSpellSaveDcUsesSourceAbilityAndSpellProficiency()
    {
        BattleUnitState source = MakeUnit("spell_dc_source", "enemy");
        source.attribute_snapshot.SetValue("intelligence", 18);
        source.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.SpellProficiencyBonus), 3);
        BattleUnitState target = MakeUnit("spell_dc_target", "player");
        target.attribute_snapshot.SetValue("agility", 10);
        CombatEffectDef effect = MakeCasterSpellSaveDamageEffect();

        BattleSaveResult failedResult = BattleSaveResolver.ResolveSaveResult(
            source,
            target,
            effect,
            BattleSaveContext.WithSaveRollOverride(14)
        );
        _test.Eq(failedResult.Dc, 15, "caster_spell DC should be 8 + INT modifier 4 + spell proficiency 3.");
        _test.Eq(failedResult.RollTotal, 14, "Agility 10 target save total should equal the d20.");
        _test.False(failedResult.Success, "Below dynamic DC agility save should fail.");

        BattleSaveResult successResult = BattleSaveResolver.ResolveSaveResult(
            source,
            target,
            effect,
            BattleSaveContext.WithSaveRollOverride(15)
        );
        _test.True(successResult.Success, "Meeting dynamic DC should succeed.");
    }

    private void TestLockedSkillBonusIncreasesStaticSaveDc()
    {
        BattleUnitState source = MakeUnit("locked_static_source", "enemy");
        source.known_skill_lock_hit_bonus_map["locked_fire"] = 2;
        BattleUnitState target = MakeUnit("locked_static_target", "player");
        target.attribute_snapshot.SetValue("constitution", 10);
        CombatEffectDef effect = MakeSaveDamageEffect("fireball", "constitution", 12, false);

        BattleSaveResult result = BattleSaveResolver.ResolveSaveResult(
            source,
            target,
            effect,
            BattleSaveContext.WithSaveRollOverride(13, "locked_fire")
        );
        _test.Eq(result.Dc, 14, "Locked skill bonus should raise static save DC.");
        _test.False(result.Success, "Target save should use the raised locked-skill DC.");
    }

    private void TestLockedSkillBonusIncreasesCasterSpellSaveDc()
    {
        BattleUnitState source = MakeUnit("locked_spell_source", "enemy");
        source.attribute_snapshot.SetValue("intelligence", 18);
        source.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.SpellProficiencyBonus), 3);
        source.known_skill_lock_hit_bonus_map["locked_spell"] = 2;
        BattleUnitState target = MakeUnit("locked_spell_target", "player");
        CombatEffectDef effect = MakeCasterSpellSaveDamageEffect();

        BattleSaveResult result = BattleSaveResolver.ResolveSaveResult(
            source,
            target,
            effect,
            BattleSaveContext.WithSaveRollOverride(16, "locked_spell")
        );
        _test.Eq(result.Dc, 17, "Locked skill bonus should raise dynamic caster save DC.");
        _test.False(result.Success, "Raised dynamic DC should make the target save fail.");
    }

    private void TestDamageSaveSuccessHalvesPartialDamage()
    {
        var resolver = new BattleDamageResolver();
        BattleUnitState source = MakeUnit("breath_source", "enemy");
        BattleUnitState target = MakeUnit("breath_target", "player");
        CombatEffectDef effect = MakeSaveDamageEffect("dragon_breath", "constitution", 12, true);
        effect.power = 10;
        Godot.Collections.Dictionary result = resolver.ResolveEffects(
            source,
            target,
            new Godot.Collections.Array { effect },
            new Godot.Collections.Dictionary { ["save_roll_override"] = 20 }
        );

        _test.Eq(DictInt(result, "damage", -1), 5, "Successful partial damage save should halve damage.");
        Godot.Collections.Dictionary @event = FirstDamageEvent(result);
        _test.True(DictBool(@event, "save_success"), "Damage event should record save success.");
        _test.True(DictBool(@event, "save_partial_applied"), "Damage event should record partial save application.");
        _test.Eq(DictInt(@event, "pre_save_damage", -1), 10, "Damage event should preserve pre-save damage.");
        _test.Eq(DictInt(@event, "save_adjusted_damage", -1), 5, "Damage event should record adjusted save damage.");
    }

    private void TestStatusSaveSuccessBlocksAndFailureAppliesStatus()
    {
        var resolver = new BattleDamageResolver();
        BattleUnitState source = MakeUnit("status_source", "enemy");
        BattleUnitState successTarget = MakeUnit("status_success_target", "player");
        CombatEffectDef effect = MakeSaveStatusEffect("sleep", "asleep", "deep_sleep");
        Godot.Collections.Dictionary successResult = resolver.ResolveEffects(
            source,
            successTarget,
            new Godot.Collections.Array { effect },
            new Godot.Collections.Dictionary { ["save_roll_override"] = 20 }
        );
        _test.False(successTarget.HasStatusEffect("asleep"), "Successful save should block default status.");
        _test.False(successTarget.HasStatusEffect("deep_sleep"), "Successful save should block failure status.");
        _test.False(DictBool(successResult, "applied", true), "Blocked status save should not mark effect applied.");

        BattleUnitState failureTarget = MakeUnit("status_failure_target", "player");
        Godot.Collections.Dictionary failureResult = resolver.ResolveEffects(
            source,
            failureTarget,
            new Godot.Collections.Array { effect },
            new Godot.Collections.Dictionary { ["save_roll_override"] = 1 }
        );
        _test.True(failureTarget.HasStatusEffect("deep_sleep"), "Failed save should apply save_failure_status_id.");
        _test.False(failureTarget.HasStatusEffect("asleep"), "save_failure_status_id should replace default status on failure.");
        _test.True(DictBool(failureResult, "applied"), "Failed status save should mark effect applied.");
        _test.True(
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
            save_dc_mode = BattleSaveContentRules.ToStringName(BattleSaveDcMode.CasterSpell),
            save_dc_source_ability = "intelligence",
            save_ability = "agility",
            save_tag = BattleSaveContentRules.ToStringName(BattleSaveTagKind.Fireball),
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
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 30);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.MpMax), 0);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ActionPoints), 2);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), 10);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 0);
        unit.attribute_snapshot.SetValue("agility", 10);
        unit.attribute_snapshot.SetValue("constitution", 10);
        unit.attribute_snapshot.SetValue("intelligence", 10);
        unit.attribute_snapshot.SetValue("willpower", 10);
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

    private static string FormatErrors(IEnumerable<string> errors)
    {
        return string.Join(" || ", errors ?? Array.Empty<string>());
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

    private void RequireNull(object value, string message)
    {
        if (value != null)
        {
            _test.Fail(message);
        }
    }

    private void RequireDegree(
        BattleSaveDegreeKind actual,
        BattleSaveDegreeKind expected,
        string message
    )
    {
        if (actual != expected)
        {
            _test.Fail($"{message} | actual={actual} expected={expected}");
        }
    }
}
