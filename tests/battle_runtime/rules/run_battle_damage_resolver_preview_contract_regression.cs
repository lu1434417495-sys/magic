using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_damage_resolver_preview_contract_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestPreviewDamageEffectUsesSharedDamageMathWithoutMutatingUnits();
            TestPreviewDamageEffectUsesSaveProbabilityWithoutRolling();
            TestAttributeScaledRecoveryDiceUseFormalFields();
            TestHealFatalUsesTypedEffectParams();
            TestDispelMagicUsesTypedEffectParams();
        }
        catch (Exception ex)
        {
            _test.Fail($"Battle damage resolver preview contract regression crashed: {ex}");
        }

        Quit(_test.Finish("Battle damage resolver preview contract regression"));
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
        CombatEffectDefinition effect = MakeDamageEffect(
            "fire",
            10,
            diceCount: 2,
            diceSides: 6
        );

        GDictionary expectedPreview = BattleDamagePreviewProjection.Project(
            resolver.PreviewDamageEffectTyped(
                source,
                target,
                effect,
                DamageResolutionContext.Empty(),
                BattleDamagePreviewRollMode.Average,
                BattleDamagePreviewSaveMode.Expected
            )
        );
        GDictionary expectedOutcome = DictDictionary(expectedPreview, "damage_outcome");
        _test.Eq(DictInt(expectedOutcome, "rolled_damage", -1), 20, "Average preview should reuse offense-multiplied rolled_damage.");
        _test.Eq(DictStringName(expectedOutcome, "mitigation_tier"), "half", "Average preview should reuse mitigation tier.");
        _test.Eq(DictInt(expectedOutcome, "fixed_mitigation_total", -1), 2, "Average preview should reuse fixed mitigation.");
        _test.Eq(DictInt(expectedPreview, "post_save_damage", -1), 8, "Average preview post-save damage should come from shared outcome.");
        _test.Eq(DictInt(expectedPreview, "shield_absorbed", -1), 5, "Average preview should use shared shield absorption.");
        _test.Eq(DictInt(expectedPreview, "hp_damage", -1), 3, "Average preview hp_damage should subtract absorbed shield.");

        GDictionary worstPreview = BattleDamagePreviewProjection.Project(
            resolver.PreviewDamageEffectTyped(
                source,
                target,
                effect,
                DamageResolutionContext.Empty(),
                BattleDamagePreviewRollMode.Maximum,
                BattleDamagePreviewSaveMode.Worst
            )
        );
        _test.Eq(DictInt(worstPreview, "post_save_damage", -1), 11, "Worst preview should use max dice and same mitigation chain.");
        _test.Eq(DictInt(worstPreview, "hp_damage", -1), 6, "Worst preview should resolve hp damage on cloned shield state.");
        _test.Eq(target.current_hp, 30, "Preview should not mutate target HP.");
        _test.Eq(target.current_shield_hp, 5, "Preview should not mutate target shield.");
        _test.True(target.HasStatusEffect("damage_reduction_up"), "Preview should not mutate target statuses.");
        _test.True(source.HasStatusEffect("attack_up"), "Preview should not mutate source statuses.");
    }

    private void TestPreviewDamageEffectUsesSaveProbabilityWithoutRolling()
    {
        var resolver = new BattleDamageResolver();
        BattleUnitState source = MakeUnit("save_preview_source", "player");
        BattleUnitState target = MakeUnit("save_preview_target", "enemy");
        CombatEffectDefinition effect = MakeDamageEffect(
            "fire",
            20,
            saveDc: 10,
            saveAbility: "agility",
            saveTag: BattleSaveContentRules.ToStringName(BattleSaveTagKind.Magic),
            savePartialOnSuccess: true
        );

        GDictionary preview = BattleDamagePreviewProjection.Project(
            resolver.PreviewDamageEffectTyped(
                source,
                target,
                effect,
                DamageResolutionContext.FromDictionary(
                    new GDictionary { ["save_roll_override"] = 20 }
                ),
                BattleDamagePreviewRollMode.Average,
                BattleDamagePreviewSaveMode.Expected
            )
        );
        GDictionary saveEstimate = DictDictionary(preview, "save_estimate");
        _test.True(DictBool(saveEstimate, "has_save"), "Save preview should output save_estimate.");
        _test.Eq(
            DictInt(saveEstimate, "save_success_probability_basis_points", -1),
            10000,
            "save_roll_override=20 should become 100% success probability."
        );
        _test.Eq(DictInt(preview, "post_save_damage", -1), 10, "Successful partial save should halve damage.");
        _test.Eq(target.current_hp, 30, "Save preview should not mutate the target by rolling a real save.");
    }

    private void TestAttributeScaledRecoveryDiceUseFormalFields()
    {
        var resolver = new BattleDamageResolver();
        BattleUnitState source = MakeUnit("recovery_source", "player");
        source.attribute_snapshot.SetValue("constitution", 12);
        source.attribute_snapshot.SetValue("constitution_modifier", 1);
        source.attribute_snapshot.SetValue("willpower", 14);
        source.attribute_snapshot.SetValue("willpower_modifier", 2);

        BattleUnitState healTarget = MakeUnit("heal_target", "player");
        healTarget.current_hp = 10;
        CombatEffectDefinition healEffect = TestSkillDefinitionProjection.BuildEffect(
            "heal",
            effectTargetTeamFilter: "ally",
            diceCount: 2,
            diceSidesBase: 4,
            diceSidesPerConstitutionMod: 1,
            diceSidesPerWillpowerMod: 1
        );
        GDictionary healResult = AttackEffectResolutionResultReader.BuildGodotPayload(resolver.ResolveEffects(
            source,
            healTarget,
            new[] { healEffect },
            DamageResolutionContext.Empty()
        ));
        int healing = DictInt(healResult, "healing");
        _test.True(healing >= 2 && healing <= 14, "Healing should use typed 2D(4+CON+WILL) dice sides.");
        _test.Eq(healTarget.current_hp, 10 + healing, "Typed healing dice should write back HP.");

        BattleUnitState staminaTarget = MakeUnit("stamina_target", "player");
        staminaTarget.current_stamina = 0;
        staminaTarget.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.StaminaMax), 30);
        CombatEffectDefinition staminaEffect = TestSkillDefinitionProjection.BuildEffect(
            "stamina_restore",
            effectTargetTeamFilter: "ally",
            diceCount: 2,
            diceSidesBase: 4,
            diceSidesPerConstitutionMod: 1,
            diceSidesPerWillpowerMod: 1
        );
        resolver.ResolveEffects(
            source,
            staminaTarget,
            new[] { staminaEffect },
            DamageResolutionContext.Empty()
        );
        _test.True(
            staminaTarget.current_stamina >= 2 && staminaTarget.current_stamina <= 14,
            "Stamina restore should use typed attribute-scaled dice sides."
        );

        var shieldService = new BattleShieldService();
        CombatEffectDefinition shieldEffect = TestSkillDefinitionProjection.BuildEffect(
            "shield",
            diceCount: 2,
            diceSidesBase: 4,
            diceSidesPerConstitutionMod: 1,
            diceSidesPerWillpowerMod: 1
        );
        _test.True(shieldService._has_shield_dice_config(shieldEffect), "Shield service should detect typed attribute-scaled dice.");
        int shieldHp = shieldService._resolve_shield_hp(source, shieldEffect, new GDictionary());
        _test.True(shieldHp >= 2 && shieldHp <= 14, "Shield HP should use typed attribute-scaled dice.");
    }

    private void TestHealFatalUsesTypedEffectParams()
    {
        var resolver = new BattleDamageResolver();
        BattleUnitState source = MakeUnit("heal_fatal_source", "player");
        BattleUnitState target = MakeUnit("heal_fatal_target", "player");
        target.current_hp = 5;
        target.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 50);
        target.attribute_snapshot.SetValue("constitution", 14);
        target.attribute_snapshot.SetValue("constitution_modifier", 2);
        CombatEffectDefinition healFatalEffect = TestSkillDefinitionProjection.BuildEffect(
            "heal_fatal",
            baseHeal: 8,
            healPerLevel: 4,
            conModBase: 2,
            conModPer2Levels: 1
        );

        GDictionary result = AttackEffectResolutionResultReader.BuildGodotPayload(resolver.ResolveEffects(
            source,
            target,
            new[] { healFatalEffect },
            DamageResolutionContext.Empty().WithSourceSkillLevel(3)
        ));
        _test.Eq(DictInt(result, "healing"), 22, "heal_fatal 应按 typed 参数公式结算治疗量。");
        _test.Eq(target.current_hp, 27, "heal_fatal 应按 typed 参数公式回写目标 HP。");
    }

    private void TestDispelMagicUsesTypedEffectParams()
    {
        var resolver = new BattleDamageResolver();
        BattleUnitState source = MakeUnit("dispel_source", "player");
        BattleUnitState target = MakeUnit("dispel_target", "player");
        SetStatus(target, "burning", 1, new GDictionary());
        SetStatus(target, "slow", 1, new GDictionary());
        CombatEffectDefinition dispelEffect = TestSkillDefinitionProjection.BuildEffect(
            "dispel_magic",
            removeHarmful: true,
            maxStatusRemoved: 1
        );

        GDictionary result = AttackEffectResolutionResultReader.BuildGodotPayload(resolver.ResolveEffects(
            source,
            target,
            new[] { dispelEffect },
            DamageResolutionContext.Empty()
        ));
        GArray dispelEvents = result.ContainsKey("dispel_events")
            ? result["dispel_events"].AsGodotArray()
            : new GArray();
        _test.Eq(dispelEvents.Count, 1, "dispel_magic 应产出一条正式 dispel event。");
        GDictionary dispelEvent = dispelEvents.Count > 0 ? dispelEvents[0].AsGodotDictionary() : new GDictionary();
        GArray removedIds = dispelEvent.ContainsKey("removed_status_ids")
            ? dispelEvent["removed_status_ids"].AsGodotArray()
            : new GArray();
        _test.Eq(removedIds.Count, 1, "typed max_status_removed=1 应只移除一个状态。");
        _test.Eq(
            (target.HasStatusEffect("burning") ? 1 : 0) + (target.HasStatusEffect("slow") ? 1 : 0),
            1,
            "typed max_status_removed=1 后目标应只剩一个有害状态。"
        );
    }

    private static CombatEffectDefinition MakeDamageEffect(
        StringName damageTag,
        int power,
        int diceCount = 0,
        int diceSides = 0,
        int saveDc = 0,
        StringName saveAbility = default,
        StringName saveTag = default,
        bool savePartialOnSuccess = false
    )
    {
        return TestSkillDefinitionProjection.BuildEffect(
            "damage",
            damageTag: damageTag,
            power: power,
            diceCount: diceCount,
            diceSides: diceSides,
            saveDc: saveDc,
            saveAbility: saveAbility,
            saveTag: saveTag,
            savePartialOnSuccess: savePartialOnSuccess
        );
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
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 30);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.MpMax), 0);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ActionPoints), 2);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), 10);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 0);
        unit.attribute_snapshot.SetValue("agility", 10);
        unit.attribute_snapshot.SetValue("agility_modifier", 0);
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
        unit.SetStatusEffect(status);
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

}
