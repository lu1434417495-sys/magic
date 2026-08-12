using System;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_battle_rule_status_param_schema_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestLockDodgeBonusAcceptsStringNameParamKey();
        TestBlindAttackPenaltyUsesStatusSemanticAndTypedOverride();
        TestStatusAttackRollPenaltyUsesFormalFieldSchema();
        TestDispellableStatusFlagsUseTypedFields();
        TestStatusDurationAndTickIntervalUseFormalFieldSchema();
        TestMitigationTierUsesTypedStatusFieldSchema();
        TestFixedMitigationUsesTypedStatusFields();
        TestSecondaryHitUsesTypedControlSaveBonus();
        TestOutgoingDamageMultiplierAcceptsStringNameParamKey();

        RequestTestExit(_test.Finish("Battle rule status param schema regression"));
    }
    private void TestFixedMitigationUsesTypedStatusFields()
    {
        var resolver = new BattleDamageResolver();
        BattleUnitState source = BuildUnit("fixed_mitigation_source");

        BattleUnitState legacyPassiveTarget = BuildUnit("legacy_passive_target");
        SetStatusParams(
            legacyPassiveTarget,
            "legacy_passive",
            new GDictionary { ["passive_reduction"] = 3 }
        );
        using GodotProjectionLease<GDictionary> legacyPassiveResultLease =
            AttackEffectResolutionResultReader.BuildGodotPayloadLease(resolver.ResolveEffects(
            source,
            legacyPassiveTarget,
            new[] { BuildDamageEffect(10) },
            DamageResolutionContext.Empty()
        ));
        GDictionary legacyPassiveResult = legacyPassiveResultLease.Value;
        _test.Eq(
            DictInt(legacyPassiveResult, "damage", -1),
            10,
            "旧 params.passive_reduction 不应继续驱动正式减伤。"
        );

        BattleUnitState formalPassiveTarget = BuildUnit("formal_passive_target");
        SetTypedStatus(formalPassiveTarget, "formal_passive", passiveReduction: 3);
        using GodotProjectionLease<GDictionary> formalPassiveResultLease =
            AttackEffectResolutionResultReader.BuildGodotPayloadLease(resolver.ResolveEffects(
            source,
            formalPassiveTarget,
            new[] { BuildDamageEffect(10) },
            DamageResolutionContext.Empty()
        ));
        GDictionary formalPassiveResult = formalPassiveResultLease.Value;
        _test.Eq(
            DictInt(formalPassiveResult, "damage", -1),
            7,
            "typed passive_reduction 字段必须驱动正式减伤。"
        );

        using SkillContentRegistry registry = new(new TestContentResourceLoader(), loadDefaultContent: false);
        using CombatEffectDef effect = new()
        {
            effect_type = "status",
            status_id = "vajra_body",
            @params = new GDictionary { ["passive_reduction"] = 3 },
        };
        var errors = new GStringArray();
        registry.AppendEffectValidationErrors(
            errors,
            "legacy_passive_reduction",
            effect,
            "test_effect"
        );
        AssertOnlyValidationErrors(
            errors,
            "Skill legacy_passive_reduction effect test_effect params.passive_reduction is unsupported; use CombatEffectDef.passive_reduction."
        );
    }

    private void TestLockDodgeBonusAcceptsStringNameParamKey()
    {
        var resolver = new BattleHitResolver();
        BattleUnitState attacker = BuildUnit("hit_attacker");
        attacker.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), 0);

        BattleUnitState legacyTarget = BuildUnit("legacy_lock_dodge_bonus");
        SetAcProfile(legacyTarget, 15, 4);
        SetStatusParams(
            legacyTarget,
            "legacy_lock_dodge_bonus",
            new GDictionary { [new StringName("lock_dodge_bonus")] = true }
        );
        AttackCheckInput legacyCheck = resolver.BuildSkillAttackCheck(
            attacker,
            legacyTarget,
            null,
            0,
            0
        );
        _test.Eq(
            legacyCheck.TargetArmorClass,
            15,
            "legacy lock_dodge_bonus params 不应继续驱动正式 dodge AC 锁定。"
        );

        BattleUnitState formalTarget = BuildUnit("formal_lock_dodge_bonus");
        SetAcProfile(formalTarget, 15, 4);
        formalTarget.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "formal_lock_dodge_bonus",
                power = 1,
                stacks = 1,
                lock_dodge_bonus = true,
            }
        );
        AttackCheckInput formalCheck = resolver.BuildSkillAttackCheck(
            attacker,
            formalTarget,
            null,
            0,
            0
        );
        _test.Eq(
            formalCheck.TargetArmorClass,
            11,
            "typed lock_dodge_bonus 字段必须继续压制 dodge AC 组件。"
        );

        using SkillContentRegistry registry = new(new TestContentResourceLoader(), loadDefaultContent: false);
        using CombatEffectDef effect = new()
        {
            effect_type = "status",
            status_id = "blind",
            @params = new GDictionary { ["lock_dodge_bonus"] = true },
        };
        var errors = new GStringArray();
        registry.AppendEffectValidationErrors(
            errors,
            "legacy_lock_dodge_bonus",
            effect,
            "test_effect"
        );
        AssertOnlyValidationErrors(
            errors,
            "Skill legacy_lock_dodge_bonus effect test_effect params.lock_dodge_bonus is unsupported; use CombatEffectDef.lock_dodge_bonus."
        );
    }

    private void TestBlindAttackPenaltyUsesStatusSemanticAndTypedOverride()
    {
        var resolver = new BattleHitResolver();
        BattleUnitState target = BuildUnit("blind_penalty_target");
        SetAcProfile(target, 15, 0);

        BattleUnitState clearAttacker = BuildUnit("clear_blind_penalty_attacker");
        AttackCheckInput clearCheck = resolver.BuildSkillAttackCheck(clearAttacker, target, null, 0, 0);

        BattleUnitState defaultBlindAttacker = BuildUnit("default_blind_penalty_attacker");
        SetStatusParams(defaultBlindAttacker, "blind", new GDictionary());
        AttackCheckInput defaultCheck = resolver.BuildSkillAttackCheck(
            defaultBlindAttacker,
            target,
            null,
            0,
            0
        );
        _test.Eq(
            defaultCheck.SituationalAttackPenalty,
            4,
            "blind 默认应让攻击检定承受 -4 等价惩罚。"
        );
        _test.Eq(
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
        AttackCheckInput severeCheck = resolver.BuildSkillAttackCheck(
            severeBlindAttacker,
            target,
            null,
            0,
            0
        );
        _test.Eq(
            severeCheck.SituationalAttackPenalty,
            4,
            "legacy blind params.attack_roll_penalty 不应继续覆盖默认攻击惩罚。"
        );

        BattleUnitState typedBlindAttacker = BuildUnit("typed_blind_penalty_attacker");
        SetTypedStatus(typedBlindAttacker, "blind", attackRollPenalty: 6);
        AttackCheckInput typedCheck = resolver.BuildSkillAttackCheck(
            typedBlindAttacker,
            target,
            null,
            0,
            0
        );
        _test.Eq(
            typedCheck.SituationalAttackPenalty,
            6,
            "typed blind.attack_roll_penalty 应能覆盖默认攻击惩罚。"
        );
    }

    private void TestStatusAttackRollPenaltyUsesFormalFieldSchema()
    {
        using SkillContentRegistry registry = new(new TestContentResourceLoader(), loadDefaultContent: false);
        using CombatEffectDef effect = new()
        {
            effect_type = "status",
            status_id = "blind",
            @params = new GDictionary { ["attack_roll_penalty"] = 6 },
        };
        var errors = new GStringArray();
        registry.AppendEffectValidationErrors(
            errors,
            "legacy_status_attack_roll_penalty",
            effect,
            "test_effect"
        );
        AssertOnlyValidationErrors(
            errors,
            "Skill legacy_status_attack_roll_penalty effect test_effect params.attack_roll_penalty is unsupported; use CombatEffectDef.attack_roll_penalty."
        );
    }

    private void TestDispellableStatusFlagsUseTypedFields()
    {
        BattleStatusEffectState legacyUndispellable = new()
        {
            status_id = "burning",
            stacks = 1,
            @params = new GDictionary { ["undispellable"] = true },
        };
        _test.True(
            BattleStatusSemanticTable.IsDispellableHarmfulStatusEntry(legacyUndispellable),
            "legacy status params.undispellable 不应继续驱动正式 dispel 语义。"
        );

        BattleStatusEffectState typedUndispellable = new()
        {
            status_id = "burning",
            stacks = 1,
            undispellable = true,
        };
        _test.False(
            BattleStatusSemanticTable.IsDispellableHarmfulStatusEntry(typedUndispellable),
            "typed undispellable 字段应阻止正式 harmful dispel。"
        );

        BattleStatusEffectState legacyBeneficial = new()
        {
            status_id = "custom_blessing",
            stacks = 1,
            @params = new GDictionary { ["dispellable_beneficial_magic"] = true },
        };
        _test.False(
            BattleStatusSemanticTable.IsDispellableBeneficialStatusEntry(legacyBeneficial),
            "legacy status params.dispellable_beneficial_magic 不应继续驱动正式 beneficial dispel 语义。"
        );

        BattleStatusEffectState typedBeneficial = new()
        {
            status_id = "custom_blessing",
            stacks = 1,
            dispellable_beneficial_magic = true,
        };
        _test.True(
            BattleStatusSemanticTable.IsDispellableBeneficialStatusEntry(typedBeneficial),
            "typed dispellable_beneficial_magic 字段应驱动正式 beneficial dispel 语义。"
        );

        using SkillContentRegistry registry = new(new TestContentResourceLoader(), loadDefaultContent: false);
        using CombatEffectDef effect = new()
        {
            effect_type = "status",
            status_id = "custom_blessing",
            @params = new GDictionary
            {
                ["undispellable"] = true,
                ["dispellable_magic"] = true,
                ["dispellable_harmful_magic"] = true,
                ["dispellable_beneficial_magic"] = true,
            },
        };
        var errors = new GStringArray();
        registry.AppendEffectValidationErrors(
            errors,
            "legacy_dispel_flags",
            effect,
            "test_effect"
        );
        AssertOnlyValidationErrors(
            errors,
            "Skill legacy_dispel_flags effect test_effect params.undispellable is unsupported; use CombatEffectDef.undispellable.",
            "Skill legacy_dispel_flags effect test_effect params.dispellable_magic is unsupported; use CombatEffectDef.dispellable_magic.",
            "Skill legacy_dispel_flags effect test_effect params.dispellable_harmful_magic is unsupported; use CombatEffectDef.dispellable_harmful_magic.",
            "Skill legacy_dispel_flags effect test_effect params.dispellable_beneficial_magic is unsupported; use CombatEffectDef.dispellable_beneficial_magic."
        );
    }

    private void TestStatusDurationAndTickIntervalUseFormalFieldSchema()
    {
        using SkillContentRegistry registry = new(new TestContentResourceLoader(), loadDefaultContent: false);
        using CombatEffectDef effect = new()
        {
            effect_type = "status",
            status_id = "burning",
            @params = new GDictionary
            {
                ["duration"] = 15,
                ["duration_tu"] = 20,
                ["tick_interval_tu"] = 10,
            },
        };
        var errors = new GStringArray();
        registry.AppendEffectValidationErrors(
            errors,
            "legacy_status_duration_tick",
            effect,
            "test_effect"
        );
        AssertOnlyValidationErrors(
            errors,
            "Skill legacy_status_duration_tick effect test_effect params.duration is unsupported; use CombatEffectDef.duration_tu.",
            "Skill legacy_status_duration_tick effect test_effect params.duration_tu is unsupported; use CombatEffectDef.duration_tu.",
            "Skill legacy_status_duration_tick effect test_effect params.tick_interval_tu is unsupported; use CombatEffectDef.tick_interval_tu."
        );
    }

    private void TestMitigationTierUsesTypedStatusFieldSchema()
    {
        var resolver = new BattleDamageResolver();

        BattleUnitState legacySource = BuildUnit("legacy_mitigation_source");
        BattleUnitState legacyTarget = BuildUnit("legacy_mitigation_target");
        SetStatusParams(
            legacyTarget,
            "legacy_half_mitigation",
            new GDictionary { [new StringName("mitigation_tier")] = "half" }
        );
        using GodotProjectionLease<GDictionary> legacyResultLease =
            AttackEffectResolutionResultReader.BuildGodotPayloadLease(resolver.ResolveEffects(
            legacySource,
            legacyTarget,
            new[] { BuildDamageEffect(20) },
            DamageResolutionContext.Empty()
        ));
        GDictionary legacyResult = legacyResultLease.Value;
        _test.Eq(
            DictInt(legacyResult, "damage", -1),
            20,
            "legacy mitigation_tier params 不应继续驱动正式伤害减免。"
        );
        GDictionary legacyEvent = FirstDamageEvent(legacyResult);
        _test.Eq(
            DictStringName(legacyEvent, "mitigation_tier"),
            new StringName("normal"),
            "legacy mitigation_tier params 不应继续出现在正式伤害事件的减免 tier 上。"
        );

        BattleUnitState formalSource = BuildUnit("formal_mitigation_source");
        BattleUnitState formalTarget = BuildUnit("formal_mitigation_target");
        SetTypedStatus(formalTarget, "formal_half_mitigation", mitigationTier: "half");
        using GodotProjectionLease<GDictionary> formalResultLease =
            AttackEffectResolutionResultReader.BuildGodotPayloadLease(resolver.ResolveEffects(
            formalSource,
            formalTarget,
            new[] { BuildDamageEffect(20) },
            DamageResolutionContext.Empty()
        ));
        GDictionary formalResult = formalResultLease.Value;
        _test.Eq(
            DictInt(formalResult, "damage", -1),
            10,
            "typed mitigation_tier 字段必须继续驱动伤害减免。"
        );
        GDictionary formalEvent = FirstDamageEvent(formalResult);
        _test.Eq(
            DictStringName(formalEvent, "mitigation_tier"),
            new StringName("half"),
            "typed mitigation_tier 字段必须继续记录到伤害事件。"
        );

        using SkillContentRegistry registry = new(new TestContentResourceLoader(), loadDefaultContent: false);
        using CombatEffectDef effect = new()
        {
            effect_type = "status",
            status_id = "magic_shield",
            @params = new GDictionary
            {
                ["mitigation_tier"] = "half",
                ["damage_tag"] = "fire",
                ["damage_tags"] = new GArray { "fire", "freeze" },
                ["damage_category"] = "magic",
            },
        };
        var errors = new GStringArray();
        registry.AppendEffectValidationErrors(
            errors,
            "legacy_mitigation_tier",
            effect,
            "test_effect"
        );
        AssertOnlyValidationErrors(
            errors,
            "Skill legacy_mitigation_tier effect test_effect params.mitigation_tier is unsupported; use CombatEffectDef.mitigation_tier.",
            "Skill legacy_mitigation_tier status effect in test_effect params.damage_tag is unsupported; use CombatEffectDef.damage_tag.",
            "Skill legacy_mitigation_tier status effect in test_effect params.damage_tags is unsupported; use CombatEffectDef.damage_tags.",
            "Skill legacy_mitigation_tier status effect in test_effect params.damage_category is unsupported; use CombatEffectDef.damage_category."
        );
    }

    private void TestSecondaryHitUsesTypedControlSaveBonus()
    {
        var resolver = new BattleDamageResolver();
        resolver.SetHitResolver(new FixedHitResolver(8));

        BattleUnitState sourceUnit = BuildUnit("secondary_hit_source");
        BattleUnitState targetUnit = BuildUnit("secondary_hit_target");
        sourceUnit.attribute_snapshot.SetValue("strength", 10);
        targetUnit.attribute_snapshot.SetValue("constitution", 10);
        CombatEffectDefinition secondaryHitDamage = TestSkillDefinitionProjection.BuildEffect(
            "damage",
            power: 10,
            damageTag: "physical_blunt",
            triggerEvent: "secondary_hit"
        );
        var attackCheck = new AttackCheckInput(
            targetArmorClass: 10,
            requiredRoll: 2,
            displayRequiredRoll: 2,
            hitRatePercent: 95,
            successRatePercent: 95,
            baseHitRatePercent: 95
        );

        AttackEffectResolutionResult baselineResult = resolver.ResolveAttackEffects(
            sourceUnit,
            targetUnit,
            new[] { secondaryHitDamage },
            attackCheck,
            new AttackContext()
        );
        _test.True(
            baselineResult.SecondaryHitSuccess && baselineResult.Damage == 10,
            "无控制豁免加值时，固定 d20=8 应触发 secondary_hit 的正式伤害。"
        );

        BattleUnitState legacyTarget = BuildUnit("legacy_secondary_hit_target");
        legacyTarget.attribute_snapshot.SetValue("constitution", 10);
        SetStatusParams(
            legacyTarget,
            "legacy_secondary_hit_save_bonus",
            new GDictionary { ["secondary_hit_save_bonus"] = 3 }
        );
        AttackEffectResolutionResult legacyResult = resolver.ResolveAttackEffects(
            sourceUnit,
            legacyTarget,
            new[] { secondaryHitDamage },
            attackCheck,
            new AttackContext()
        );
        _test.True(
            legacyResult.SecondaryHitSuccess && legacyResult.Damage == 10,
            "legacy secondary_hit_save_bonus params 不应阻止 secondary_hit 的正式伤害。"
        );

        BattleUnitState typedTarget = BuildUnit("typed_secondary_hit_target");
        typedTarget.attribute_snapshot.SetValue("constitution", 10);
        typedTarget.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "typed_control_save_bonus",
                power = 1,
                stacks = 1,
                control_save_bonus = 3,
            }
        );
        AttackEffectResolutionResult typedResult = resolver.ResolveAttackEffects(
            sourceUnit,
            typedTarget,
            new[] { secondaryHitDamage },
            attackCheck,
            new AttackContext()
        );
        _test.True(
            !typedResult.SecondaryHitSuccess && typedResult.Damage == 0,
            "typed control_save_bonus 应提高二次豁免并阻止 secondary_hit 的正式伤害。"
        );

        using SkillContentRegistry registry = new(new TestContentResourceLoader(), loadDefaultContent: false);
        using CombatEffectDef effect = new()
        {
            effect_type = "status",
            status_id = "legacy_secondary_hit_bonus",
            @params = new GDictionary { ["secondary_hit_save_bonus"] = 3 },
        };
        var errors = new GStringArray();
        registry.AppendEffectValidationErrors(
            errors,
            "legacy_secondary_hit_save_bonus",
            effect,
            "test_effect"
        );
        AssertOnlyValidationErrors(
            errors,
            "Skill legacy_secondary_hit_save_bonus effect test_effect params.secondary_hit_save_bonus is unsupported; use CombatEffectDef.control_save_bonus."
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
        using GodotProjectionLease<GDictionary> legacyResultLease =
            AttackEffectResolutionResultReader.BuildGodotPayloadLease(resolver.ResolveEffects(
            legacySource,
            legacyTarget,
            new[] { BuildDamageEffect(20) },
            DamageResolutionContext.Empty()
        ));
        GDictionary legacyResult = legacyResultLease.Value;
        _test.Eq(
            DictInt(legacyResult, "damage", -1),
            20,
            "legacy outgoing_damage_multiplier params 不应继续驱动正式伤害倍率。"
        );
        GDictionary legacyEvent = FirstDamageEvent(legacyResult);
        _test.Eq(
            DictFloat(legacyEvent, "offense_multiplier"),
            1.0f,
            "legacy outgoing_damage_multiplier params 不应继续写入正式 offense_multiplier。"
        );

        BattleUnitState formalSource = BuildUnit("formal_outgoing_multiplier_source");
        BattleUnitState formalTarget = BuildUnit("formal_outgoing_multiplier_target");
        SetTypedStatus(
            formalSource,
            "formal_outgoing_multiplier",
            outgoingDamageMultiplier: 0.5
        );
        using GodotProjectionLease<GDictionary> formalResultLease =
            AttackEffectResolutionResultReader.BuildGodotPayloadLease(resolver.ResolveEffects(
            formalSource,
            formalTarget,
            new[] { BuildDamageEffect(20) },
            DamageResolutionContext.Empty()
        ));
        GDictionary formalResult = formalResultLease.Value;
        _test.Eq(
            DictInt(formalResult, "damage", -1),
            10,
            "typed outgoing_damage_multiplier 字段必须继续驱动正式伤害倍率。"
        );
        GDictionary formalEvent = FirstDamageEvent(formalResult);
        _test.Eq(
            DictFloat(formalEvent, "offense_multiplier"),
            0.5f,
            "typed outgoing_damage_multiplier 字段必须继续写入正式 offense_multiplier。"
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
            @params = @params != null ? (GDictionary)@params.Duplicate(true) : new GDictionary(),
        };
        unit.SetStatusEffect(statusEffect);
    }

    private static void SetTypedStatus(
        BattleUnitState unit,
        StringName statusId,
        bool lockCrit = false,
        int attackRollPenalty = -1,
        StringName mitigationTier = default,
        int passiveReduction = 0,
        double? outgoingDamageMultiplier = null
    )
    {
        var statusEffect = new BattleStatusEffectState
        {
            status_id = statusId,
            power = 1,
            stacks = 1,
            lock_crit = lockCrit,
            attack_roll_penalty = attackRollPenalty,
            mitigation_tier = mitigationTier,
            passive_reduction = passiveReduction,
            outgoing_damage_multiplier = outgoingDamageMultiplier,
        };
        unit.SetStatusEffect(statusEffect);
    }

    private static void SetAcProfile(BattleUnitState unit, int armorClass, int dodgeBonus)
    {
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), armorClass);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.DodgeBonus), dodgeBonus);
    }

    private static CombatEffectDefinition BuildDamageEffect(int power) =>
        TestSkillDefinitionProjection.BuildEffect(
            "damage",
            power: power,
            damageTag: "physical_slash"
        );

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
        }.WithCombatResourcesForTest(
            hp: 100,
            mp: 4,
            stamina: 4,
            aura: 0,
            ap: 2,
            movePoints: BattleUnitState.DefaultMovePointsPerTurn,
            isAlive: true
        );
        unit.SetAnchorCoord(Vector2I.Zero);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 100);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.MpMax), 4);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.StaminaMax), 4);
        unit.attribute_snapshot.SetValue("action_points", 2);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), 0);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 10);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.DodgeBonus), 0);
        return unit;
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

    private void AssertOnlyValidationErrors(
        GStringArray actualErrors,
        params string[] expectedErrors
    )
    {
        _test.Eq(
            actualErrors?.Count ?? 0,
            expectedErrors?.Length ?? 0,
            $"validator 应只报告目标字段诊断。 actual={FormatErrors(actualErrors)}"
        );
        foreach (string expectedError in expectedErrors ?? Array.Empty<string>())
        {
            bool found = false;
            foreach (string actualError in actualErrors ?? new GStringArray())
            {
                if (string.Equals(actualError, expectedError, StringComparison.Ordinal))
                {
                    found = true;
                    break;
                }
            }
            _test.True(
                found,
                $"validator 缺少精确诊断：{expectedError} actual={FormatErrors(actualErrors)}"
            );
        }
    }

    private static string FormatErrors(GStringArray errors) =>
        errors == null ? "[]" : $"[{string.Join(" | ", errors)}]";
}
