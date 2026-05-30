using System;
using System.Collections.Generic;
using Godot;
using static GdInterop;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

[GlobalClass]
public partial class BattleDamageResolver : RefCounted
{
    private static readonly StringName FortuneMarkTargetStatId = "fortune_mark_target";
    private static readonly StringName StatusAttackUp = "attack_up";
    private static readonly StringName StatusDamageReductionUp = "damage_reduction_up";
    private static readonly StringName StatusGuarding = "guarding";
    private static readonly StringName StatusMarked = "marked";
    private static readonly StringName StatusArcherPreAim = "archer_pre_aim";
    private static readonly StringName BonusConditionTargetLowHp = "target_low_hp";
    private static readonly StringName BonusConditionTargetDebuffCount = "target_debuff_count";
    private static readonly StringName MitigationTierNormal = "normal";
    private static readonly StringName MitigationTierHalf = "half";
    private static readonly StringName MitigationTierDouble = "double";
    private static readonly StringName MitigationTierImmune = "immune";
    private static readonly StringName DiceEventReasonCriticalHit = "critical_hit";
    private static readonly StringName DiceEventReasonDiceThreshold = "dice_threshold";
    private static readonly StringName DiceEventReasonSkillDiceMax = "skill_dice_max";
    private static readonly StringName DiceEventReasonWeaponDiceMax = "weapon_dice_max";
    private static readonly StringName AttackResolutionCriticalHit = "critical_hit";
    private static readonly StringName TriggerEventOrdinaryHit = "ordinary_hit";
    private static readonly StringName StatusBlackStarBrandEliteGuardWindow =
        "black_star_brand_elite_guard_window";
    private static readonly StringName StatusCrownBreakBrokenFang = "crown_break_broken_fang";
    private static readonly StringName StatusCrownBreakBrokenHand = "crown_break_broken_hand";
    private static readonly StringName StatusCrownBreakBlindedEye = "crown_break_blinded_eye";
    private static readonly StringName StatusParamControlSaveBonus = "control_save_bonus";
    private static readonly StringName StatusParamSecondaryHitSaveBonus =
        "secondary_hit_save_bonus";
    private static readonly StringName EffectEquipmentDurabilityDamage =
        "equipment_durability_damage";
    private static readonly StringName EffectDispelMagic = "dispel_magic";
    private static readonly StringName DamagePreviewRollModeRandom = "random";
    private static readonly StringName DamagePreviewRollModeAverage = "average";
    private static readonly StringName DamagePreviewRollModeMaximum = "maximum";
    private static readonly StringName DamagePreviewSaveModeExpected = "expected";
    private static readonly StringName DamagePreviewSaveModeWorst = "worst";

    private const int MinDamageFloor = 0;
    private const int DamageReductionUpFixedPerPower = 2;
    private const int DamageDiceHighTotalThresholdNumerator = 4;
    private const int DamageDiceHighTotalThresholdDenominator = 5;
    private const int AttackCheckTarget = 21;
    private const int NaturalHitRoll = 20;
    private const int BlackStarBrandGuardIgnoreFlat = 4;

    private GDictionary _skill_defs = new();
    private readonly GArray _last_stand_mastery_records = new();
    private readonly BattleFateEventBus _fate_event_bus = new();
    private readonly BattleReportFormatter _report_formatter = new();
    private readonly TraitTriggerHooks _trait_trigger_hooks = new();
    private BattleHitResolver _hit_resolver = new();
    private bool _suppress_last_stand_mastery_records;

    public static StringName FORTUNE_MARK_TARGET_STAT_ID() => FortuneMarkTargetStatId;

    public static StringName DAMAGE_PREVIEW_ROLL_MODE_RANDOM() => DamagePreviewRollModeRandom;

    public static StringName DAMAGE_PREVIEW_ROLL_MODE_AVERAGE() => DamagePreviewRollModeAverage;

    public static StringName DAMAGE_PREVIEW_ROLL_MODE_MAXIMUM() => DamagePreviewRollModeMaximum;

    public static StringName DAMAGE_PREVIEW_SAVE_MODE_EXPECTED() => DamagePreviewSaveModeExpected;

    public static StringName DAMAGE_PREVIEW_SAVE_MODE_WORST() => DamagePreviewSaveModeWorst;

    public void set_skill_defs(GDictionary skill_defs)
    {
        _skill_defs = skill_defs != null ? DuplicateDictionary(skill_defs) : new GDictionary();
    }

    public GArray get_and_clear_last_stand_mastery_records()
    {
        GArray records = (GArray)_last_stand_mastery_records.Duplicate(true);
        _last_stand_mastery_records.Clear();
        return records;
    }

    public void set_hit_resolver(BattleHitResolver hit_resolver)
    {
        _hit_resolver = hit_resolver ?? new BattleHitResolver();
    }

    public void set_hit_resolver(GodotObject hit_resolver)
    {
        set_hit_resolver(hit_resolver as BattleHitResolver);
    }

    public BattleFateEventBus get_fate_event_bus()
    {
        return _fate_event_bus;
    }

    public GDictionary resolve_skill(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        SkillDef skill_def
    )
    {
        if (source_unit == null || target_unit == null || skill_def?.combat_profile == null)
        {
            return BuildEmptyResult();
        }
        return resolve_effects(
            source_unit,
            target_unit,
            ToValueArray(skill_def.combat_profile.effect_defs),
            new GDictionary { ["skill_id"] = skill_def.skill_id }
        );
    }

    public virtual GDictionary resolve_attack_effects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        GArray effect_defs,
        AttackCheckInput attack_check
    )
    {
        return resolve_attack_effects(
            source_unit,
            target_unit,
            effect_defs,
            attack_check,
            new AttackContext()
        );
    }

    public virtual GDictionary resolve_attack_effects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        GArray effect_defs,
        AttackCheckInput attack_check,
        AttackContext attack_context = null
    )
    {
        if (source_unit == null || target_unit == null)
        {
            return BuildAttackMetadataResult(BuildEmptyResult(), new AttackResolutionMetadata());
        }

        GArray resolvedEffectDefs = CoerceEffectDefs(effect_defs);
        AttackContext normalizedAttackContext = attack_context ?? new AttackContext();
        AttackResolutionMetadata attackMetadata = ResolveAttackMetadata(
            source_unit,
            target_unit,
            attack_check,
            normalizedAttackContext
        );
        if (attackMetadata.SkillId == "" && normalizedAttackContext.SkillId != "")
        {
            attackMetadata.SkillId = normalizedAttackContext.SkillId;
        }
        if (!attackMetadata.AttackSuccess)
        {
            GDictionary failedResult = BuildAttackMetadataResult(
                BuildEmptyResult(),
                attackMetadata
            );
            AttachAttackReportEntry(failedResult, source_unit, target_unit);
            DispatchAttackResolutionEvents(
                source_unit,
                target_unit,
                attackMetadata,
                normalizedAttackContext
            );
            ClearComboStackOnMiss(source_unit);
            return failedResult;
        }

        int secondaryHitDcBase = 10;
        foreach (var effectValue in resolvedEffectDefs)
        {
            CombatEffectDef effectDef = effectValue.AsGodotObject() as CombatEffectDef;
            if (
                effectDef != null
                && effectDef.trigger_event == "secondary_hit"
                && effectDef.@params != null
            )
            {
                secondaryHitDcBase = DictInt(effectDef.@params, "secondary_hit_dc_base", 10);
                break;
            }
        }
        attackMetadata.SecondaryHitSuccess = _resolve_secondary_hit(
            source_unit,
            target_unit,
            normalizedAttackContext,
            secondaryHitDcBase
        );
        GDictionary attackEffectContext = BuildAttackEffectContext(attackMetadata);

        GDictionary resolvedResult = BuildAttackMetadataResult(
            resolve_effects(source_unit, target_unit, resolvedEffectDefs, attackEffectContext),
            attackMetadata
        );
        AttachAttackReportEntry(resolvedResult, source_unit, target_unit);
        DispatchAttackResolutionEvents(
            source_unit,
            target_unit,
            attackMetadata,
            normalizedAttackContext
        );
        return resolvedResult;
    }

    public GDictionary resolve_spell_control_check(
        BattleUnitState source_unit,
        GDictionary attack_context = null
    )
    {
        if (source_unit == null)
        {
            return new GDictionary();
        }
        GDictionary normalizedContext = attack_context ?? new GDictionary();
        GDictionary controlMetadata = ResolveSpellControlMetadata(source_unit, normalizedContext);
        if (DictBool(normalizedContext, "dispatch_events", true))
        {
            DispatchSpellControlResolutionEvents(source_unit, controlMetadata, normalizedContext);
        }
        return controlMetadata;
    }

    public GDictionary resolve_spell_control_check(BattleUnitState source_unit)
    {
        return resolve_spell_control_check(source_unit, new GDictionary());
    }

    public virtual GDictionary preview_damage_effect(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        CombatEffectDef effect_def,
        GDictionary damage_context = null,
        StringName roll_mode = default,
        StringName save_mode = default
    )
    {
        if (source_unit == null || target_unit == null || effect_def == null)
        {
            return new GDictionary();
        }
        StringName resolvedRollMode = IsEmpty(roll_mode) ? DamagePreviewRollModeAverage : roll_mode;
        StringName resolvedSaveMode = IsEmpty(save_mode)
            ? DamagePreviewSaveModeExpected
            : save_mode;
        BattleUnitState sourcePreview = source_unit.clone();
        BattleUnitState targetPreview = target_unit.clone();
        if (sourcePreview == null || targetPreview == null)
        {
            return new GDictionary();
        }

        GDictionary previewContext = DuplicateDictionary(damage_context);
        previewContext["damage_roll_mode"] = resolvedRollMode;
        GDictionary damageOutcome = ResolveDamageOutcome(
            sourcePreview,
            targetPreview,
            effect_def,
            previewContext
        );
        if (DictBool(damageOutcome, "invalid_damage_tag"))
        {
            return new GDictionary
            {
                ["roll_mode"] = resolvedRollMode.ToString(),
                ["save_mode"] = resolvedSaveMode.ToString(),
                ["pre_save_damage"] = 0,
                ["post_save_damage"] = 0,
                ["hp_damage"] = 0,
                ["damage"] = 0,
                ["shield_absorbed"] = 0,
                ["shield_hp_before"] = target_unit.current_shield_hp,
                ["shield_hp_after"] = targetPreview.current_shield_hp,
                ["damage_outcome"] = DuplicateDictionary(damageOutcome),
                ["damage_result"] = new GDictionary(),
                ["save_estimate"] = new GDictionary(),
                ["error_code"] = DictString(damageOutcome, "error_code"),
                ["diagnostics"] = new GArray
                {
                    BuildInvalidDamageTagDiagnostic(
                        source_unit,
                        target_unit,
                        effect_def,
                        damageOutcome
                    ),
                },
                ["source_preview_after"] = sourcePreview,
                ["target_preview_after"] = targetPreview,
            };
        }

        int preSaveDamage = DictInt(damageOutcome, "resolved_damage");
        GDictionary saveEstimate = BuildDamagePreviewSaveEstimate(
            sourcePreview,
            targetPreview,
            effect_def,
            previewContext,
            preSaveDamage,
            resolvedSaveMode
        );
        ApplyDamagePreviewSaveEstimate(damageOutcome, saveEstimate);
        GDictionary damageResult = ApplyDamageToTarget(targetPreview, damageOutcome, sourcePreview);
        return new GDictionary
        {
            ["roll_mode"] = resolvedRollMode.ToString(),
            ["save_mode"] = resolvedSaveMode.ToString(),
            ["pre_save_damage"] = preSaveDamage,
            ["post_save_damage"] = DictInt(damageOutcome, "resolved_damage"),
            ["hp_damage"] = DictInt(damageResult, "hp_damage", DictInt(damageResult, "damage")),
            ["damage"] = DictInt(damageResult, "damage"),
            ["shield_absorbed"] = DictInt(damageResult, "shield_absorbed"),
            ["shield_hp_before"] = target_unit.current_shield_hp,
            ["shield_hp_after"] = targetPreview.current_shield_hp,
            ["damage_outcome"] = DuplicateDictionary(damageOutcome),
            ["damage_result"] = DuplicateDictionary(damageResult),
            ["save_estimate"] = DuplicateDictionary(saveEstimate),
            ["source_preview_after"] = sourcePreview,
            ["target_preview_after"] = targetPreview,
        };
    }

    public virtual GDictionary preview_damage_sequence(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        GArray effect_defs,
        GDictionary damage_context = null,
        GDictionary options = null
    )
    {
        if (source_unit == null || target_unit == null)
        {
            return BuildEmptyResult();
        }

        GDictionary normalizedOptions = options ?? new GDictionary();
        StringName rollMode = DictStringName(
            normalizedOptions,
            "roll_mode",
            DamagePreviewRollModeAverage
        );
        StringName saveMode = DictStringName(
            normalizedOptions,
            "save_mode",
            DamagePreviewSaveModeExpected
        );
        GDictionary previewContext = DuplicateDictionary(damage_context);
        previewContext["damage_roll_mode"] = rollMode;
        BattleUnitState sourcePreview = source_unit.clone();
        BattleUnitState targetPreview = target_unit.clone();
        if (sourcePreview == null || targetPreview == null)
        {
            return BuildEmptyResult();
        }

        int totalPreSaveDamage = 0;
        int totalPostSaveDamage = 0;
        int totalHpDamage = 0;
        int totalShieldAbsorbed = 0;
        bool shieldBroken = false;
        bool applied = false;
        bool stableLethalFromBranches = false;
        int lethalProbabilityBasisPoints = 0;
        var damageEvents = new GArray();
        var diagnostics = new GArray();
        var saveEstimates = new GArray();

        bool previousSuppression = _suppress_last_stand_mastery_records;
        _suppress_last_stand_mastery_records = true;
        try
        {
            foreach (var effectValue in CoerceEffectDefs(effect_defs))
            {
                CombatEffectDef effectDef = effectValue.AsGodotObject() as CombatEffectDef;
                if (effectDef == null || !_does_effect_trigger(effectDef, previewContext))
                {
                    continue;
                }
                if (effectDef.effect_type != "damage")
                {
                    continue;
                }
                GDictionary damageOutcome = ResolveDamageOutcome(
                    sourcePreview,
                    targetPreview,
                    effectDef,
                    previewContext
                );
                if (DictBool(damageOutcome, "invalid_damage_tag"))
                {
                    diagnostics.Add(
                        BuildInvalidDamageTagDiagnostic(
                            sourcePreview,
                            targetPreview,
                            effectDef,
                            damageOutcome
                        )
                    );
                    continue;
                }
                int preSaveDamage = DictInt(damageOutcome, "resolved_damage");
                int targetHpBeforeEffect = Math.Max(targetPreview.current_hp, 1);
                GDictionary saveEstimate = BuildDamagePreviewSaveEstimate(
                    sourcePreview,
                    targetPreview,
                    effectDef,
                    previewContext,
                    preSaveDamage,
                    saveMode
                );
                GDictionary branchLethal = new();
                if (DictBool(saveEstimate, "has_save"))
                {
                    saveEstimates.Add(DuplicateDictionary(saveEstimate));
                    branchLethal = BuildSaveBranchLethalEstimate(
                        targetPreview,
                        damageOutcome,
                        saveEstimate,
                        sourcePreview
                    );
                    stableLethalFromBranches =
                        stableLethalFromBranches || DictBool(branchLethal, "stable_lethal");
                    lethalProbabilityBasisPoints = Math.Max(
                        lethalProbabilityBasisPoints,
                        DictInt(branchLethal, "lethal_probability_basis_points")
                    );
                }
                totalPreSaveDamage += preSaveDamage;
                totalPostSaveDamage += DictInt(saveEstimate, "damage_after_save", preSaveDamage);

                GDictionary damageResult;
                if (saveMode == DamagePreviewSaveModeExpected && DictBool(saveEstimate, "has_save"))
                {
                    damageResult = BuildExpectedSaveBranchDamageResult(
                        targetPreview,
                        damageOutcome,
                        saveEstimate,
                        sourcePreview
                    );
                    ApplyDamagePreviewSaveEstimate(damageOutcome, saveEstimate);
                }
                else
                {
                    ApplyDamagePreviewSaveEstimate(damageOutcome, saveEstimate);
                    damageResult = ApplyDamageToTarget(targetPreview, damageOutcome, sourcePreview);
                }

                int hpDamage = DictInt(damageResult, "hp_damage", DictInt(damageResult, "damage"));
                if (
                    DictBool(branchLethal, "failure_kills")
                    && !DictBool(branchLethal, "success_kills")
                    && hpDamage >= targetHpBeforeEffect
                )
                {
                    hpDamage = Math.Max(DictInt(branchLethal, "success_hp_damage"), 0);
                    damageResult["hp_damage"] = hpDamage;
                    damageResult["damage"] = hpDamage;
                }
                totalHpDamage += hpDamage;
                totalShieldAbsorbed += DictInt(damageResult, "shield_absorbed");
                shieldBroken = shieldBroken || DictBool(damageResult, "shield_broken");
                damageEvents.Add(DuplicateDictionary(damageResult));
                applied = true;
            }
        }
        finally
        {
            _suppress_last_stand_mastery_records = previousSuppression;
        }

        GDictionary result = BuildEmptyResult();
        result["applied"] = applied;
        result["pre_save_damage"] = totalPreSaveDamage;
        result["post_save_damage"] = totalPostSaveDamage;
        result["damage"] = totalHpDamage;
        result["hp_damage"] = totalHpDamage;
        result["shield_absorbed"] = totalShieldAbsorbed;
        result["shield_broken"] = shieldBroken;
        result["damage_events"] = damageEvents;
        result["diagnostics"] = diagnostics;
        result["save_estimates"] = saveEstimates;
        result["stable_lethal"] = targetPreview.current_hp <= 0 || stableLethalFromBranches;
        result["lethal_probability_basis_points"] =
            targetPreview.current_hp <= 0 ? 10000 : lethalProbabilityBasisPoints;
        result["source_preview_after"] = sourcePreview;
        result["target_preview_after"] = targetPreview;
        AttachDamageEventAggregates(result);
        return result;
    }

    public virtual GDictionary resolve_effects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        GArray effect_defs
    )
    {
        return resolve_effects(source_unit, target_unit, effect_defs, new GDictionary());
    }

    public virtual GDictionary resolve_effects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        GArray effect_defs,
        GDictionary damage_context = null
    )
    {
        if (source_unit == null || target_unit == null)
        {
            return BuildEmptyResult();
        }

        GArray resolvedEffectDefs = CoerceEffectDefs(effect_defs);
        GDictionary context = damage_context ?? new GDictionary();
        int totalDamage = 0;
        int totalHealing = 0;
        int totalShieldAbsorbed = 0;
        var damageEvents = new GArray();
        var equipmentDurabilityEvents = new GArray();
        var dispelEvents = new GArray();
        var statusEffectIds = new GStringNameArray();
        var removedStatusEffectIds = new GStringNameArray();
        var sourceStatusEffectIds = new GStringNameArray();
        var terrainEffectIds = new GStringNameArray();
        var saveResults = new GArray();
        var diagnostics = new GArray();
        int totalHeightDelta = 0;
        bool shieldBroken = false;
        bool applied = false;
        bool blackStarWedgeTriggered = false;
        int executeStage = -1;
        StringName executeOutcome = "";

        foreach (var effectValue in resolvedEffectDefs)
        {
            CombatEffectDef effectDef = effectValue.AsGodotObject() as CombatEffectDef;
            if (effectDef == null || !_does_effect_trigger(effectDef, context))
            {
                continue;
            }

            StringName effectType = ProgressionDataUtils.to_string_name(effectDef.effect_type);
            if (effectType == "damage")
            {
                GDictionary damageOutcome = ResolveDamageOutcome(
                    source_unit,
                    target_unit,
                    effectDef,
                    context
                );
                if (DictBool(damageOutcome, "invalid_damage_tag"))
                {
                    diagnostics.Add(
                        BuildInvalidDamageTagDiagnostic(
                            source_unit,
                            target_unit,
                            effectDef,
                            damageOutcome
                        )
                    );
                    continue;
                }
                GDictionary damageSaveResult = BattleSaveResolver.resolve_save(
                    source_unit,
                    target_unit,
                    effectDef,
                    context
                );
                if (DictBool(damageSaveResult, "has_save"))
                {
                    saveResults.Add(DuplicateDictionary(damageSaveResult));
                }
                ApplySaveResultToDamageOutcome(damageOutcome, damageSaveResult, effectDef);
                GDictionary damageResult = ApplyDamageToTarget(
                    target_unit,
                    damageOutcome,
                    source_unit
                );
                int hpDamage = DictInt(damageResult, "damage");
                totalDamage += hpDamage;
                totalShieldAbsorbed += DictInt(damageResult, "shield_absorbed");
                damageEvents.Add(DuplicateDictionary(damageResult));
                blackStarWedgeTriggered =
                    blackStarWedgeTriggered
                    || DictBool(damageResult, "low_luck_black_star_wedge_triggered");
                shieldBroken = shieldBroken || DictBool(damageResult, "shield_broken");
                applied = true;
                if (hpDamage > 0 || DictInt(damageResult, "shield_absorbed") > 0)
                {
                    GrantStatusOnHitToSource(source_unit, effectDef, context);
                }
            }
            else if (effectType == EffectEquipmentDurabilityDamage)
            {
                GDictionary durabilityResult = ApplyEquipmentDurabilityDamageEffect(
                    source_unit,
                    target_unit,
                    effectDef,
                    context,
                    totalDamage,
                    totalShieldAbsorbed
                );
                if (durabilityResult.Count > 0)
                {
                    equipmentDurabilityEvents.Add(DuplicateDictionary(durabilityResult));
                    GDictionary equipmentSaveResult = GetDictionary(
                        durabilityResult,
                        "save_result"
                    );
                    if (DictBool(equipmentSaveResult, "has_save"))
                    {
                        saveResults.Add(DuplicateDictionary(equipmentSaveResult));
                    }
                    if (
                        DictInt(durabilityResult, "durability_loss") > 0
                        || DictBool(durabilityResult, "destroyed")
                    )
                    {
                        applied = true;
                    }
                }
            }
            else if (effectType == "heal")
            {
                int healAmount = ResolveHealAmount(source_unit, effectDef);
                ApplyHealing(target_unit, healAmount);
                totalHealing += healAmount;
                applied = true;
            }
            else if (effectType == "stamina_restore")
            {
                ApplyStaminaRestore(source_unit, target_unit, effectDef);
                applied = true;
            }
            else if (effectType == "heal_fatal")
            {
                int healAmount = ResolveHealFatalAmount(target_unit, effectDef);
                if (healAmount > 0)
                {
                    ApplyHealing(target_unit, healAmount);
                    totalHealing += healAmount;
                    applied = true;
                }
            }
            else if (effectType == "erase_status")
            {
                StringName erasedStatusId = ProgressionDataUtils.to_string_name(
                    effectDef.status_id
                );
                if (erasedStatusId == "")
                {
                    erasedStatusId = ProgressionDataUtils.to_string_name(
                        effectDef.trigger_status_id
                    );
                }
                if (erasedStatusId != "" && target_unit.has_status_effect(erasedStatusId))
                {
                    target_unit.erase_status_effect(erasedStatusId);
                    applied = true;
                }
            }
            else if (effectType == "cleanse_harmful")
            {
                GStringNameArray removedStatusIds = new();
                foreach (StringName statusId in SortedStatusIds(target_unit.status_effects))
                {
                    if (BattleStatusSemanticTable.is_cleansable_harmful_status(statusId))
                    {
                        removedStatusIds.Add(statusId);
                    }
                }
                foreach (StringName statusId in removedStatusIds)
                {
                    target_unit.erase_status_effect(statusId);
                }
                if (removedStatusIds.Count > 0)
                {
                    applied = true;
                }
            }
            else if (effectType == EffectDispelMagic)
            {
                GDictionary dispelResult = ApplyDispelMagicEffect(
                    source_unit,
                    target_unit,
                    effectDef
                );
                GArray removedIds = GetArray(dispelResult, "removed_status_ids");
                if (removedIds.Count > 0)
                {
                    dispelEvents.Add(DuplicateDictionary(dispelResult));
                    foreach (var removedValue in removedIds)
                    {
                        StringName removedId = ProgressionDataUtils.to_string_name(removedValue);
                        if (removedId != "" && !removedStatusEffectIds.Contains(removedId))
                        {
                            removedStatusEffectIds.Add(removedId);
                        }
                    }
                    applied = true;
                }
            }
            else if (effectType == "status" || effectType == "apply_status")
            {
                GDictionary statusSaveResult = BattleSaveResolver.resolve_save(
                    source_unit,
                    target_unit,
                    effectDef,
                    context
                );
                if (DictBool(statusSaveResult, "has_save"))
                {
                    saveResults.Add(DuplicateDictionary(statusSaveResult));
                }
                if (DoesSaveBlockEffect(statusSaveResult))
                {
                    continue;
                }
                StringName resolvedStatusId = ResolveStatusIdForSave(effectDef, statusSaveResult);
                if (
                    resolvedStatusId != ""
                    && ApplyStatusEffect(target_unit, source_unit, effectDef, resolvedStatusId)
                )
                {
                    AddUnique(statusEffectIds, resolvedStatusId);
                    applied = true;
                }
            }
            else if (effectType == "terrain" || effectType == "terrain_effect")
            {
                if (effectDef.terrain_effect_id != "")
                {
                    AddUnique(terrainEffectIds, effectDef.terrain_effect_id);
                    applied = true;
                }
            }
            else if (effectType == "height" || effectType == "height_delta")
            {
                if (effectDef.height_delta != 0)
                {
                    totalHeightDelta += effectDef.height_delta;
                    applied = true;
                }
            }
            else if (effectType == "execute")
            {
                GDictionary executeResult = ResolveExecuteEffect(
                    source_unit,
                    target_unit,
                    effectDef,
                    context,
                    statusEffectIds,
                    saveResults
                );
                executeStage = DictInt(executeResult, "execute_stage", executeStage);
                executeOutcome = DictStringName(executeResult, "execute_outcome", executeOutcome);
                if (DictBool(executeResult, "applied"))
                {
                    applied = true;
                }
                GArray executeDamageResults = GetArray(executeResult, "damage_results");
                if (executeDamageResults.Count > 0)
                {
                    foreach (GDictionary damageResult in ReadDictionaryItems(executeDamageResults))
                    {
                        totalDamage += DictInt(damageResult, "damage");
                        totalShieldAbsorbed += DictInt(damageResult, "shield_absorbed");
                        shieldBroken = shieldBroken || DictBool(damageResult, "shield_broken");
                        damageEvents.Add(DuplicateDictionary(damageResult));
                    }
                }
                else
                {
                    GDictionary fatalResult = GetDictionary(executeResult, "damage_result");
                    if (fatalResult.Count > 0)
                    {
                        totalDamage += DictInt(fatalResult, "damage");
                        totalShieldAbsorbed += DictInt(fatalResult, "shield_absorbed");
                        shieldBroken = shieldBroken || DictBool(fatalResult, "shield_broken");
                        damageEvents.Add(DuplicateDictionary(fatalResult));
                    }
                }
            }
        }

        target_unit.is_alive = target_unit.current_hp > 0;
        if (
            blackStarWedgeTriggered
            && target_unit.is_alive
            && ApplyLowLuckBlackStarWedgeExposed(source_unit)
        )
        {
            sourceStatusEffectIds.Add(LowLuckRelicRules.status_black_star_wedge_exposed());
        }

        GDictionary result = new()
        {
            ["applied"] = applied,
            ["damage"] = totalDamage,
            ["hp_damage"] = totalDamage,
            ["healing"] = totalHealing,
            ["shield_absorbed"] = totalShieldAbsorbed,
            ["shield_broken"] = shieldBroken,
            ["damage_events"] = damageEvents,
            ["equipment_durability_events"] = equipmentDurabilityEvents,
            ["dispel_events"] = dispelEvents,
            ["status_effect_ids"] = statusEffectIds,
            ["removed_status_effect_ids"] = removedStatusEffectIds,
            ["source_status_effect_ids"] = sourceStatusEffectIds,
            ["terrain_effect_ids"] = terrainEffectIds,
            ["save_results"] = saveResults,
            ["height_delta"] = totalHeightDelta,
            ["diagnostics"] = diagnostics,
        };
        foreach (GDictionary diagnostic in ReadDictionaryItems(diagnostics))
        {
            result["error_code"] = DictString(diagnostic, "error_code");
            break;
        }
        if (executeStage >= 0)
        {
            result["execute_stage"] = executeStage;
            result["execute_outcome"] = executeOutcome.ToString();
        }
        AttachDamageEventAggregates(result);
        return result;
    }

    public GDictionary resolve_fall_damage(BattleUnitState target_unit, int fall_layers)
    {
        if (target_unit == null || fall_layers <= 0 || !target_unit.is_alive)
        {
            return BuildEmptyResult();
        }
        int maxHp = GetAttributeValue(target_unit, AttributeService.HP_MAX_ID());
        if (maxHp <= 0)
        {
            maxHp = Math.Max(target_unit.current_hp, 1);
        }
        int damagePerLayer = Math.Max((maxHp + 19) / 20, 1);
        GDictionary damageResult = ApplyDamageToTarget(target_unit, damagePerLayer * fall_layers);
        target_unit.is_alive = target_unit.current_hp > 0;
        return BuildEnvironmentalDamageResult(damageResult);
    }

    public GDictionary resolve_collision_damage(
        BattleUnitState target_unit,
        int source_body_size,
        int target_body_size
    )
    {
        if (target_unit == null || !target_unit.is_alive)
        {
            return BuildEmptyResult();
        }
        int sizeGap = Math.Max(source_body_size - target_body_size, 0);
        GDictionary damageResult = ApplyDamageToTarget(target_unit, 10 + sizeGap * 10);
        target_unit.is_alive = target_unit.current_hp > 0;
        return BuildEnvironmentalDamageResult(damageResult);
    }

    private GDictionary ApplyDamageToTarget(
        BattleUnitState targetUnit,
        int rawDamage,
        BattleUnitState sourceUnit = null
    )
    {
        int normalizedDamage = Math.Max(rawDamage, 0);
        if (targetUnit == null || normalizedDamage <= 0)
        {
            return BuildAppliedDamageResult(null, 0, 0, false);
        }
        GDictionary damageOutcome = new()
        {
            ["damage_tag"] = new StringName(""),
            ["mitigation_tier"] = MitigationTierNormal,
            ["mitigation_sources"] = new GArray(),
            ["base_damage"] = normalizedDamage,
            ["offense_multiplier"] = 1.0,
            ["defense_multiplier"] = 1.0,
            ["true_damage"] = false,
            ["bypass_mitigation"] = false,
            ["bypass_shield"] = false,
            ["shield_absorption_percent"] = 100.0,
            ["min_hp_after_damage"] = 0,
            ["resolved_damage"] = normalizedDamage,
        };
        return ApplyDamageToTarget(targetUnit, damageOutcome, sourceUnit);
    }

    public GDictionary apply_direct_damage_to_target(
        BattleUnitState target_unit,
        GDictionary resolved_damage_input,
        BattleUnitState source_unit = null
    )
    {
        return ApplyDamageToTarget(target_unit, resolved_damage_input, source_unit);
    }

    public bool _does_effect_trigger(CombatEffectDef effect_def, GDictionary damage_context)
    {
        if (effect_def == null)
        {
            return false;
        }
        GDictionary context = damage_context ?? new GDictionary();
        StringName triggerEvent = ProgressionDataUtils.to_string_name(effect_def.trigger_event);
        if (triggerEvent == "")
        {
            return true;
        }
        if (triggerEvent == AttackResolutionCriticalHit)
        {
            return DictBool(context, "critical_hit");
        }
        if (triggerEvent == TriggerEventOrdinaryHit)
        {
            return DictBool(context, "attack_success") && !DictBool(context, "critical_hit");
        }
        if (triggerEvent == "secondary_hit")
        {
            return DictBool(context, "secondary_hit_success");
        }
        GameLog.Warning(
            $"Unsupported combat effect trigger_event '{triggerEvent}' for effect_type '{ProgressionDataUtils.to_string_name(effect_def.effect_type)}'.",
            "battle.damage.unsupported_trigger",
            "battle"
        );
        return false;
    }

    public bool _resolve_secondary_hit(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        AttackContext attack_context,
        int dc_base = 10
    )
    {
        if (source_unit == null || target_unit == null)
        {
            return false;
        }
        int strMod = GetUnitBaseAttributeModifier(source_unit, UnitBaseAttributes.STRENGTH());
        int conMod = GetUnitBaseAttributeModifier(target_unit, UnitBaseAttributes.CONSTITUTION());
        int dc = dc_base + strMod;
        _hit_resolver ??= new BattleHitResolver();
        int saveRoll = _hit_resolver.roll_attack_die(
            20,
            false,
            attack_context ?? new AttackContext()
        );
        int saveBonus = GetTargetSecondaryHitSaveBonus(target_unit);
        return saveRoll + conMod + saveBonus < dc;
    }

    public virtual int _roll_damage_die(int dice_sides)
    {
        return TrueRandomSeedService.randi_range(1, Math.Max(dice_sides, 1));
    }

    public bool _unit_has_status_bool_param(BattleUnitState unit_state, StringName param_key)
    {
        if (unit_state == null || param_key == "")
        {
            return false;
        }
        foreach (StringName statusId in SortedStatusIds(unit_state.status_effects))
        {
            BattleStatusEffectState statusEntry = unit_state.get_status_effect(statusId);
            if (statusEntry?.@params == null)
            {
                continue;
            }
            if (GetBoolParam(statusEntry.@params, param_key, false))
            {
                return true;
            }
        }
        return false;
    }

    private static bool GetBoolParam(GDictionary @params, StringName key, bool fallback = false)
    {
        if (@params == null || !@params.ContainsKey(key))
            return fallback;
        return (bool)@params[key];
    }

    private static int GetIntParam(GDictionary @params, StringName key, int fallback = 0)
    {
        if (@params == null || !@params.ContainsKey(key))
            return fallback;
        return (int)@params[key];
    }

    private static double GetFloatParam(GDictionary @params, StringName key, double fallback = 0.0)
    {
        if (@params == null || !@params.ContainsKey(key))
            return fallback;
        return (double)@params[key];
    }

    private static StringName GetStringNameParam(
        GDictionary @params,
        StringName key,
        StringName fallback = default
    )
    {
        if (@params == null || !@params.ContainsKey(key))
            return fallback ?? new StringName("");
        return ProgressionDataUtils.to_string_name(@params[key]);
    }

    private static GArray GetArrayParam(
        GDictionary @params,
        StringName key,
        GArray fallback = null
    )
    {
        if (@params == null || !@params.ContainsKey(key))
            return fallback ?? new GArray();
        try
        {
            return (GArray)@params[key];
        }
        catch
        {
            return fallback ?? new GArray();
        }
    }


    private GDictionary ResolveDamageOutcome(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDef effectDef,
        GDictionary damageContext = null
    )
    {
        StringName damageTag = ResolveDamageTag(sourceUnit, effectDef);
        if (damageTag == "")
        {
            return BuildInvalidDamageTagOutcome(sourceUnit, effectDef);
        }
        GDictionary context = damageContext ?? new GDictionary();
        StringName rollMode = DictStringName(
            context,
            "damage_roll_mode",
            DamagePreviewRollModeRandom
        );
        GDictionary damageRoll = RollDamageDice(effectDef, true, "damage_dice", rollMode);
        GDictionary weaponRoll = RollWeaponDice(
            sourceUnit,
            effectDef,
            true,
            "weapon_damage_dice",
            rollMode
        );
        bool criticalHit = DictBool(context, "critical_hit");
        bool bonusConditionMet = HasBonusCondition(effectDef, targetUnit);
        GDictionary bonusDamageRoll = bonusConditionMet
            ? RollBonusDamageDice(effectDef, true, "bonus_damage_dice", rollMode)
            : new GDictionary();
        GDictionary criticalDamageRoll =
            criticalHit && damageRoll.Count > 0
                ? RollDamageDice(effectDef, false, "critical_extra_damage_dice", rollMode)
                : new GDictionary();
        GDictionary criticalWeaponRoll =
            criticalHit && weaponRoll.Count > 0
                ? RollWeaponDice(
                    sourceUnit,
                    effectDef,
                    false,
                    "critical_extra_weapon_damage_dice",
                    rollMode
                )
                : new GDictionary();
        GDictionary criticalBonusDamageRoll =
            criticalHit && bonusDamageRoll.Count > 0
                ? RollBonusDamageDice(
                    effectDef,
                    false,
                    "critical_extra_bonus_damage_dice",
                    rollMode
                )
                : new GDictionary();
        GDictionary traitCritResult = ResolveCritTraitResult(
            sourceUnit,
            targetUnit,
            effectDef,
            criticalHit
        );
        GDictionary traitExtraWeaponRoll = DictBool(traitCritResult, "triggered")
            ? RollDicePool(
                Math.Max(DictInt(traitCritResult, "extra_weapon_dice_count"), 0),
                Math.Max(DictInt(traitCritResult, "extra_weapon_dice_sides"), 0),
                0,
                "trait_extra_weapon_damage_dice",
                rollMode
            )
            : new GDictionary();
        GDictionary consumedStackRoll = RollConsumedStackDice(sourceUnit, effectDef, rollMode);

        int baseDamage =
            Math.Max(effectDef?.power ?? 0, 0)
            + GetRollTotalWithBonus(weaponRoll, "weapon_damage_dice")
            + GetRollTotalWithBonus(damageRoll, "damage_dice")
            + GetRollTotalWithBonus(bonusDamageRoll, "bonus_damage_dice")
            + GetRollTotal(criticalWeaponRoll, "critical_extra_weapon_damage_dice")
            + GetRollTotal(criticalDamageRoll, "critical_extra_damage_dice")
            + GetRollTotal(criticalBonusDamageRoll, "critical_extra_bonus_damage_dice")
            + GetRollTotal(traitExtraWeaponRoll, "trait_extra_weapon_damage_dice")
            + GetRollTotal(consumedStackRoll, "consumed_stack_damage_dice");
        double offenseMultiplier = BuildOffenseMultiplier(sourceUnit, targetUnit, effectDef);
        int rolledDamage = Math.Max(RoundToInt(baseDamage * offenseMultiplier), 0);
        GDictionary mitigationTierResult = ResolveMitigationTierResult(targetUnit, damageTag);
        StringName mitigationTier = DictStringName(
            mitigationTierResult,
            "tier",
            MitigationTierNormal
        );
        int tierAdjustedDamage = rolledDamage;
        if (mitigationTier == MitigationTierImmune)
        {
            tierAdjustedDamage = 0;
        }
        else if (mitigationTier == MitigationTierHalf)
        {
            tierAdjustedDamage /= 2;
        }
        else if (mitigationTier == MitigationTierDouble)
        {
            tierAdjustedDamage *= 2;
        }

        GDictionary mitigation = BuildFixedMitigation(targetUnit, effectDef, damageTag);
        ApplyBlackStarBrandGuardIgnore(mitigation, targetUnit);
        ApplyLowLuckBlackStarWedgeGuardIgnore(mitigation, sourceUnit);
        TrimFixedMitigationSources(mitigation);
        int buffReduction = DictInt(mitigation, "buff_reduction");
        int stanceReduction = DictInt(mitigation, "stance_reduction");
        int passiveReduction = DictInt(mitigation, "passive_reduction");
        int contentDr = DictInt(mitigation, "content_dr");
        int guardBlock = DictInt(mitigation, "guard_block");
        int guardIgnoreApplied = DictInt(mitigation, "guard_ignore_applied");
        int fixedMitigationTotal =
            buffReduction + stanceReduction + passiveReduction + contentDr + guardBlock;
        int resolvedDamage = Math.Max(tierAdjustedDamage - fixedMitigationTotal, MinDamageFloor);
        GDictionary damageDiceEventFlags = BuildDamageDiceEventFlags(
            criticalHit,
            damageRoll,
            weaponRoll,
            bonusDamageRoll
        );

        GDictionary result = new()
        {
            ["damage_tag"] = damageTag,
            ["mitigation_tier"] = mitigationTier,
            ["mitigation_sources"] = GetArray(mitigationTierResult, "sources"),
            ["base_damage"] = baseDamage,
            ["critical_hit"] = criticalHit,
            ["add_weapon_dice"] = ShouldAddWeaponDice(effectDef),
            ["damage_dice_count"] = DictInt(damageRoll, "damage_dice_count"),
            ["damage_dice_sides"] = DictInt(damageRoll, "damage_dice_sides"),
            ["damage_dice_rolls"] = GetArray(damageRoll, "damage_dice_rolls"),
            ["damage_dice_total"] = DictInt(damageRoll, "damage_dice_total"),
            ["damage_dice_bonus"] = DictInt(damageRoll, "damage_dice_bonus"),
            ["damage_dice_max_total"] = DictInt(damageRoll, "damage_dice_max_total"),
            ["damage_dice_is_max"] = DictBool(damageRoll, "damage_dice_is_max"),
            ["bonus_condition_met"] = bonusConditionMet,
            ["bonus_damage_dice_count"] = DictInt(bonusDamageRoll, "bonus_damage_dice_count"),
            ["bonus_damage_dice_sides"] = DictInt(bonusDamageRoll, "bonus_damage_dice_sides"),
            ["bonus_damage_dice_rolls"] = GetArray(bonusDamageRoll, "bonus_damage_dice_rolls"),
            ["bonus_damage_dice_total"] = DictInt(bonusDamageRoll, "bonus_damage_dice_total"),
            ["bonus_damage_dice_bonus"] = DictInt(bonusDamageRoll, "bonus_damage_dice_bonus"),
            ["bonus_damage_dice_max_total"] = DictInt(
                bonusDamageRoll,
                "bonus_damage_dice_max_total"
            ),
            ["bonus_damage_dice_is_max"] = DictBool(bonusDamageRoll, "bonus_damage_dice_is_max"),
            ["weapon_damage_dice_count"] = DictInt(weaponRoll, "weapon_damage_dice_count"),
            ["weapon_damage_dice_sides"] = DictInt(weaponRoll, "weapon_damage_dice_sides"),
            ["weapon_damage_dice_rolls"] = GetArray(weaponRoll, "weapon_damage_dice_rolls"),
            ["weapon_damage_dice_total"] = DictInt(weaponRoll, "weapon_damage_dice_total"),
            ["weapon_damage_dice_bonus"] = DictInt(weaponRoll, "weapon_damage_dice_bonus"),
            ["weapon_damage_dice_max_total"] = DictInt(weaponRoll, "weapon_damage_dice_max_total"),
            ["weapon_damage_dice_is_max"] = DictBool(weaponRoll, "weapon_damage_dice_is_max"),
            ["critical_extra_damage_dice_count"] = DictInt(
                criticalDamageRoll,
                "critical_extra_damage_dice_count"
            ),
            ["critical_extra_damage_dice_sides"] = DictInt(
                criticalDamageRoll,
                "critical_extra_damage_dice_sides"
            ),
            ["critical_extra_damage_dice_rolls"] = GetArray(
                criticalDamageRoll,
                "critical_extra_damage_dice_rolls"
            ),
            ["critical_extra_damage_dice_total"] = DictInt(
                criticalDamageRoll,
                "critical_extra_damage_dice_total"
            ),
            ["critical_extra_damage_dice_max_total"] = DictInt(
                criticalDamageRoll,
                "critical_extra_damage_dice_max_total"
            ),
            ["critical_extra_bonus_damage_dice_count"] = DictInt(
                criticalBonusDamageRoll,
                "critical_extra_bonus_damage_dice_count"
            ),
            ["critical_extra_bonus_damage_dice_sides"] = DictInt(
                criticalBonusDamageRoll,
                "critical_extra_bonus_damage_dice_sides"
            ),
            ["critical_extra_bonus_damage_dice_rolls"] = GetArray(
                criticalBonusDamageRoll,
                "critical_extra_bonus_damage_dice_rolls"
            ),
            ["critical_extra_bonus_damage_dice_total"] = DictInt(
                criticalBonusDamageRoll,
                "critical_extra_bonus_damage_dice_total"
            ),
            ["critical_extra_bonus_damage_dice_max_total"] = DictInt(
                criticalBonusDamageRoll,
                "critical_extra_bonus_damage_dice_max_total"
            ),
            ["critical_extra_weapon_damage_dice_count"] = DictInt(
                criticalWeaponRoll,
                "critical_extra_weapon_damage_dice_count"
            ),
            ["critical_extra_weapon_damage_dice_sides"] = DictInt(
                criticalWeaponRoll,
                "critical_extra_weapon_damage_dice_sides"
            ),
            ["critical_extra_weapon_damage_dice_rolls"] = GetArray(
                criticalWeaponRoll,
                "critical_extra_weapon_damage_dice_rolls"
            ),
            ["critical_extra_weapon_damage_dice_total"] = DictInt(
                criticalWeaponRoll,
                "critical_extra_weapon_damage_dice_total"
            ),
            ["critical_extra_weapon_damage_dice_max_total"] = DictInt(
                criticalWeaponRoll,
                "critical_extra_weapon_damage_dice_max_total"
            ),
            ["trait_extra_weapon_damage_dice_count"] = DictInt(
                traitExtraWeaponRoll,
                "trait_extra_weapon_damage_dice_count"
            ),
            ["trait_extra_weapon_damage_dice_sides"] = DictInt(
                traitExtraWeaponRoll,
                "trait_extra_weapon_damage_dice_sides"
            ),
            ["trait_extra_weapon_damage_dice_rolls"] = GetArray(
                traitExtraWeaponRoll,
                "trait_extra_weapon_damage_dice_rolls"
            ),
            ["trait_extra_weapon_damage_dice_total"] = DictInt(
                traitExtraWeaponRoll,
                "trait_extra_weapon_damage_dice_total"
            ),
            ["trait_extra_weapon_damage_dice_max_total"] = DictInt(
                traitExtraWeaponRoll,
                "trait_extra_weapon_damage_dice_max_total"
            ),
            ["offense_multiplier"] = offenseMultiplier,
            ["rolled_damage"] = rolledDamage,
            ["tier_adjusted_damage"] = tierAdjustedDamage,
            ["resolved_damage"] = resolvedDamage,
            ["buff_reduction"] = buffReduction,
            ["stance_reduction"] = stanceReduction,
            ["passive_reduction"] = passiveReduction,
            ["content_dr"] = contentDr,
            ["guard_block"] = guardBlock,
            ["guard_ignore_applied"] = guardIgnoreApplied,
            ["fixed_mitigation_sources"] = GetArray(mitigation, "fixed_mitigation_sources"),
            ["low_luck_black_star_wedge_triggered"] = DictBool(
                mitigation,
                "low_luck_black_star_wedge_triggered"
            ),
            ["fixed_mitigation_total"] = fixedMitigationTotal,
            ["fully_absorbed_by_mitigation"] =
                resolvedDamage <= 0
                && mitigationTier != MitigationTierImmune
                && tierAdjustedDamage > 0,
            ["trait_trigger_results"] = new GArray(),
        };
        AppendTraitTriggerResult(result, traitCritResult);
        ApplyDamageDiceEventFlags(result, damageDiceEventFlags);
        return result;
    }

    private GDictionary ApplyDamageToTarget(
        BattleUnitState targetUnit,
        GDictionary damageOutcome,
        BattleUnitState sourceUnit = null
    )
    {
        if (damageOutcome == null)
        {
            damageOutcome = new GDictionary();
        }
        int normalizedDamage = Math.Max(DictInt(damageOutcome, "resolved_damage"), 0);
        if (targetUnit == null || normalizedDamage <= 0)
        {
            return BuildAppliedDamageResult(damageOutcome, 0, 0, false);
        }

        bool bypassShield = DictBool(damageOutcome, "bypass_shield");
        bool bypassDeathPrevention = DictBool(damageOutcome, "bypass_death_prevention");
        double shieldEfficiency =
            DictFloat(damageOutcome, "shield_absorption_percent", 100.0) / 100.0;
        int minHpAfterDamage = Math.Max(DictInt(damageOutcome, "min_hp_after_damage"), 0);
        targetUnit.normalize_shield_state();

        int shieldAbsorbed = 0;
        bool shieldBroken = false;
        if (!bypassShield && targetUnit.has_shield() && shieldEfficiency > 0.0)
        {
            int shieldCapacity = (int)Math.Ceiling(targetUnit.current_shield_hp * shieldEfficiency);
            shieldAbsorbed = Math.Min(normalizedDamage, shieldCapacity);
            int actualDrain =
                shieldEfficiency > 0.0
                    ? Math.Min(
                        (int)Math.Ceiling(shieldAbsorbed / shieldEfficiency),
                        targetUnit.current_shield_hp
                    )
                    : 0;
            targetUnit.current_shield_hp = Math.Max(targetUnit.current_shield_hp - actualDrain, 0);
            if (targetUnit.current_shield_hp <= 0)
            {
                shieldBroken = shieldAbsorbed > 0;
                targetUnit.clear_shield();
            }
            else
            {
                targetUnit.normalize_shield_state();
            }
        }

        int hpDamage = Math.Max(normalizedDamage - shieldAbsorbed, 0);
        if (hpDamage > 0)
        {
            int maxHp = GetAttributeValue(targetUnit, AttributeService.HP_MAX_ID());
            if (maxHp > 0 && hpDamage * 10 >= maxHp * 6)
            {
                RecordLastStandMastery(targetUnit, sourceUnit, "critical_survival", 20);
            }
            int projectedHp = targetUnit.current_hp - hpDamage;
            if (projectedHp <= minHpAfterDamage)
            {
                if (minHpAfterDamage > 0)
                {
                    targetUnit.current_hp = Math.Min(
                        Math.Max(projectedHp, minHpAfterDamage),
                        targetUnit.current_hp
                    );
                }
                else if (bypassDeathPrevention)
                {
                    targetUnit.current_hp = 0;
                }
                else
                {
                    GDictionary fatalTraitResult = ResolveFatalDamageTraitResult(
                        targetUnit,
                        sourceUnit,
                        damageOutcome,
                        hpDamage,
                        projectedHp
                    );
                    if (
                        DictBool(fatalTraitResult, "triggered")
                        && DictInt(fatalTraitResult, "clamp_to_hp") > 0
                    )
                    {
                        targetUnit.current_hp = Math.Max(
                            DictInt(fatalTraitResult, "clamp_to_hp", 1),
                            1
                        );
                        AppendTraitTriggerResult(damageOutcome, fatalTraitResult);
                    }
                    else if (targetUnit.has_status_effect("death_ward"))
                    {
                        targetUnit.current_hp = 0;
                        if (!TriggerLastStand(targetUnit, sourceUnit))
                        {
                            targetUnit.current_hp = 0;
                        }
                    }
                    else
                    {
                        targetUnit.current_hp = 0;
                    }
                }
            }
            else
            {
                targetUnit.current_hp = Math.Max(projectedHp, 0);
            }
        }

        return BuildAppliedDamageResult(damageOutcome, hpDamage, shieldAbsorbed, shieldBroken);
    }

    private GDictionary BuildExpectedSaveBranchDamageResult(
        BattleUnitState targetPreview,
        GDictionary damageOutcome,
        GDictionary saveEstimate,
        BattleUnitState sourcePreview
    )
    {
        int successBasis = Math.Clamp(
            DictInt(saveEstimate, "save_success_probability_basis_points"),
            0,
            10000
        );
        int failureBasis = Math.Max(10000 - successBasis, 0);
        int failureDamage = Math.Max(DictInt(saveEstimate, "damage_on_save_failure"), 0);
        int successDamage = Math.Max(DictInt(saveEstimate, "damage_on_save_success"), 0);

        BattleUnitState failureTarget = targetPreview.clone();
        BattleUnitState successTarget = targetPreview.clone();
        GDictionary failureOutcome = DuplicateDictionary(damageOutcome);
        failureOutcome["resolved_damage"] = failureDamage;
        GDictionary successOutcome = DuplicateDictionary(damageOutcome);
        successOutcome["resolved_damage"] = successDamage;
        GDictionary failureResult = ApplyDamageToTarget(
            failureTarget,
            failureOutcome,
            sourcePreview
        );
        GDictionary successResult = ApplyDamageToTarget(
            successTarget,
            successOutcome,
            sourcePreview
        );

        int expectedHpDamage = RoundToInt(
            (
                DictInt(failureResult, "hp_damage", DictInt(failureResult, "damage")) * failureBasis
                + DictInt(successResult, "hp_damage", DictInt(successResult, "damage"))
                    * successBasis
            ) / 10000.0
        );
        int expectedShieldAbsorbed = RoundToInt(
            (
                DictInt(failureResult, "shield_absorbed") * failureBasis
                + DictInt(successResult, "shield_absorbed") * successBasis
            ) / 10000.0
        );

        GDictionary result = DuplicateDictionary(damageOutcome);
        ApplyDamagePreviewSaveEstimate(result, saveEstimate);
        result["damage"] = expectedHpDamage;
        result["hp_damage"] = expectedHpDamage;
        result["shield_absorbed"] = expectedShieldAbsorbed;
        result["shield_broken"] = DictBool(failureResult, "shield_broken") && failureBasis > 0;
        result["fully_absorbed_by_shield"] = expectedHpDamage <= 0 && expectedShieldAbsorbed > 0;
        return result;
    }

    private GDictionary BuildSaveBranchLethalEstimate(
        BattleUnitState targetPreview,
        GDictionary damageOutcome,
        GDictionary saveEstimate,
        BattleUnitState sourcePreview
    )
    {
        int successBasis = Math.Clamp(
            DictInt(saveEstimate, "save_success_probability_basis_points"),
            0,
            10000
        );
        int failureBasis = Math.Max(10000 - successBasis, 0);
        int failureDamage = Math.Max(DictInt(saveEstimate, "damage_on_save_failure"), 0);
        int successDamage = Math.Max(DictInt(saveEstimate, "damage_on_save_success"), 0);

        BattleUnitState failureTarget = targetPreview.clone();
        BattleUnitState successTarget = targetPreview.clone();
        GDictionary failureOutcome = DuplicateDictionary(damageOutcome);
        failureOutcome["resolved_damage"] = failureDamage;
        GDictionary successOutcome = DuplicateDictionary(damageOutcome);
        successOutcome["resolved_damage"] = successDamage;
        GDictionary failureResult = ApplyDamageToTarget(
            failureTarget,
            failureOutcome,
            sourcePreview
        );
        GDictionary successResult = ApplyDamageToTarget(
            successTarget,
            successOutcome,
            sourcePreview
        );

        bool failureKills = failureTarget != null && failureTarget.current_hp <= 0;
        bool successKills = successTarget != null && successTarget.current_hp <= 0;
        return new GDictionary
        {
            ["failure_kills"] = failureKills,
            ["success_kills"] = successKills,
            ["failure_hp_damage"] = DictInt(
                failureResult,
                "hp_damage",
                DictInt(failureResult, "damage")
            ),
            ["success_hp_damage"] = DictInt(
                successResult,
                "hp_damage",
                DictInt(successResult, "damage")
            ),
            ["stable_lethal"] = failureKills && successKills,
            ["lethal_probability_basis_points"] = failureKills
                ? (successKills ? 10000 : failureBasis)
                : 0,
        };
    }

    private GDictionary BuildDamagePreviewSaveEstimate(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDef effectDef,
        GDictionary damageContext,
        int damageBeforeSave,
        StringName saveMode
    )
    {
        GDictionary probability = BattleSaveResolver.estimate_save_success_probability(
            sourceUnit,
            targetUnit,
            effectDef,
            damageContext ?? new GDictionary()
        );
        if (!DictBool(probability, "has_save"))
        {
            return new GDictionary
            {
                ["has_save"] = false,
                ["damage_before_save"] = damageBeforeSave,
                ["damage_after_save"] = damageBeforeSave,
                ["damage_after_save_estimate"] = damageBeforeSave,
                ["damage_after_save_worst"] = damageBeforeSave,
            };
        }
        int successBasisPoints = Math.Clamp(
            DictInt(probability, "success_probability_basis_points"),
            0,
            10000
        );
        int failureBasisPoints = Math.Max(10000 - successBasisPoints, 0);
        int damageOnSaveSuccess =
            effectDef != null
            && effectDef.save_partial_on_success
            && !DictBool(probability, "immune")
                ? damageBeforeSave / 2
                : 0;
        int expectedDamage = RoundToInt(
            (damageBeforeSave * failureBasisPoints + damageOnSaveSuccess * successBasisPoints)
                / 10000.0
        );
        int worstDamage = failureBasisPoints <= 0 ? damageOnSaveSuccess : damageBeforeSave;
        int damageAfterSave = saveMode == DamagePreviewSaveModeWorst ? worstDamage : expectedDamage;
        return new GDictionary
        {
            ["has_save"] = true,
            ["damage_before_save"] = damageBeforeSave,
            ["damage_after_save"] = Math.Max(damageAfterSave, 0),
            ["damage_after_save_estimate"] = Math.Max(expectedDamage, 0),
            ["damage_after_save_worst"] = Math.Max(worstDamage, 0),
            ["damage_on_save_failure"] = damageBeforeSave,
            ["damage_on_save_success"] = damageOnSaveSuccess,
            ["save_partial_on_success"] = effectDef != null && effectDef.save_partial_on_success,
            ["save_success_probability_basis_points"] = successBasisPoints,
            ["save_success_rate_percent"] = RoundToInt(successBasisPoints / 100.0),
            ["save_failure_probability_basis_points"] = failureBasisPoints,
            ["dc"] = DictInt(probability, "dc"),
            ["ability"] = DictString(probability, "ability"),
            ["save_tag"] = DictString(probability, "save_tag"),
            ["advantage_state"] = DictString(probability, "advantage_state"),
            ["ability_value"] = DictInt(probability, "ability_value"),
            ["ability_modifier"] = DictInt(probability, "ability_modifier"),
            ["bonus"] = DictInt(probability, "bonus"),
            ["immune"] = DictBool(probability, "immune"),
            ["sources"] = GetArray(probability, "sources"),
        };
    }

    private static void ApplyDamagePreviewSaveEstimate(
        GDictionary damageOutcome,
        GDictionary saveEstimate
    )
    {
        if (damageOutcome == null || saveEstimate == null)
        {
            return;
        }
        damageOutcome["pre_save_damage"] = DictInt(
            saveEstimate,
            "damage_before_save",
            DictInt(damageOutcome, "resolved_damage")
        );
        if (!DictBool(saveEstimate, "has_save"))
        {
            damageOutcome["save_adjusted_damage"] = DictInt(damageOutcome, "resolved_damage");
            damageOutcome["fully_absorbed_by_save"] = false;
            return;
        }
        int adjustedDamage = Math.Max(
            DictInt(saveEstimate, "damage_after_save", DictInt(damageOutcome, "resolved_damage")),
            0
        );
        damageOutcome["save_result"] = DuplicateDictionary(saveEstimate);
        damageOutcome["save_success_probability_basis_points"] = DictInt(
            saveEstimate,
            "save_success_probability_basis_points"
        );
        damageOutcome["save_failure_probability_basis_points"] = DictInt(
            saveEstimate,
            "save_failure_probability_basis_points"
        );
        damageOutcome["save_immune"] = DictBool(saveEstimate, "immune");
        damageOutcome["save_partial_applied"] = DictBool(saveEstimate, "save_partial_on_success");
        damageOutcome["resolved_damage"] = adjustedDamage;
        damageOutcome["save_adjusted_damage"] = adjustedDamage;
        damageOutcome["fully_absorbed_by_save"] =
            DictInt(damageOutcome, "pre_save_damage") > 0 && adjustedDamage <= 0;
    }

    private static void ApplySaveResultToDamageOutcome(
        GDictionary damageOutcome,
        GDictionary saveResult,
        CombatEffectDef effectDef
    )
    {
        if (damageOutcome == null || saveResult == null || !DictBool(saveResult, "has_save"))
        {
            return;
        }
        damageOutcome["save_result"] = DuplicateDictionary(saveResult);
        damageOutcome["save_success"] = DictBool(saveResult, "success");
        damageOutcome["save_immune"] = DictBool(saveResult, "immune");
        damageOutcome["save_partial_applied"] = false;
        damageOutcome["pre_save_damage"] = DictInt(damageOutcome, "resolved_damage");
        if (!DictBool(saveResult, "success"))
        {
            damageOutcome["save_adjusted_damage"] = DictInt(damageOutcome, "resolved_damage");
            damageOutcome["fully_absorbed_by_save"] = false;
            return;
        }
        int preSaveDamage = Math.Max(DictInt(damageOutcome, "resolved_damage"), 0);
        int adjustedDamage = 0;
        if (
            effectDef != null
            && effectDef.save_partial_on_success
            && !DictBool(saveResult, "immune")
        )
        {
            adjustedDamage = preSaveDamage / 2;
            damageOutcome["save_partial_applied"] = true;
        }
        damageOutcome["resolved_damage"] = adjustedDamage;
        damageOutcome["save_adjusted_damage"] = adjustedDamage;
        damageOutcome["fully_absorbed_by_save"] = preSaveDamage > 0 && adjustedDamage <= 0;
    }

    private GDictionary RollDamageDice(
        CombatEffectDef effectDef,
        bool includeBonus = true,
        string fieldPrefix = "damage_dice",
        StringName rollMode = default
    )
    {
        if (effectDef?.@params == null)
        {
            return new GDictionary();
        }
        int diceCount = Math.Max(DictInt(effectDef.@params, "dice_count"), 0);
        int diceSides = Math.Max(DictInt(effectDef.@params, "dice_sides"), 0);
        int diceBonus = includeBonus ? DictInt(effectDef.@params, "dice_bonus") : 0;
        return RollDicePool(
            diceCount,
            diceSides,
            diceBonus,
            fieldPrefix,
            IsEmpty(rollMode) ? DamagePreviewRollModeRandom : rollMode
        );
    }

    private GDictionary RollBonusDamageDice(
        CombatEffectDef effectDef,
        bool includeBonus = true,
        string fieldPrefix = "bonus_damage_dice",
        StringName rollMode = default
    )
    {
        if (effectDef?.@params == null)
        {
            return new GDictionary();
        }
        int diceCount = Math.Max(DictInt(effectDef.@params, "bonus_damage_dice_count"), 0);
        int diceSides = Math.Max(DictInt(effectDef.@params, "bonus_damage_dice_sides"), 0);
        int diceBonus = includeBonus ? DictInt(effectDef.@params, "bonus_damage_dice_bonus") : 0;
        return RollDicePool(
            diceCount,
            diceSides,
            diceBonus,
            fieldPrefix,
            IsEmpty(rollMode) ? DamagePreviewRollModeRandom : rollMode
        );
    }

    private GDictionary RollWeaponDice(
        BattleUnitState sourceUnit,
        CombatEffectDef effectDef,
        bool includeBonus = true,
        string fieldPrefix = "weapon_damage_dice",
        StringName rollMode = default
    )
    {
        if (!ShouldAddWeaponDice(effectDef))
        {
            return new GDictionary();
        }
        GDictionary dice = GetCurrentWeaponDamageDice(sourceUnit);
        if (dice.Count == 0)
        {
            return new GDictionary();
        }
        int diceCount = Math.Max(DictInt(dice, "dice_count"), 0);
        int diceSides = Math.Max(DictInt(dice, "dice_sides"), 0);
        int diceBonus = includeBonus ? DictInt(dice, "flat_bonus") : 0;
        return RollDicePool(
            diceCount,
            diceSides,
            diceBonus,
            fieldPrefix,
            IsEmpty(rollMode) ? DamagePreviewRollModeRandom : rollMode
        );
    }

    private GDictionary RollDicePool(
        int diceCount,
        int diceSides,
        int diceBonus,
        string fieldPrefix,
        StringName rollMode = default
    )
    {
        if (diceCount <= 0 || diceSides <= 0 || string.IsNullOrEmpty(fieldPrefix))
        {
            return new GDictionary();
        }
        StringName resolvedRollMode = IsEmpty(rollMode) ? DamagePreviewRollModeRandom : rollMode;
        var rolls = new GArray();
        int diceTotal = BuildDicePoolTotal(diceCount, diceSides, resolvedRollMode);
        if (resolvedRollMode == DamagePreviewRollModeRandom)
        {
            diceTotal = 0;
            for (int i = 0; i < diceCount; i++)
            {
                int roll = RollDamageDieVirtual(diceSides);
                rolls.Add(roll);
                diceTotal += roll;
            }
        }
        else
        {
            rolls = BuildPreviewDiceRolls(diceCount, diceSides, diceTotal);
        }
        int maxTotal = diceCount * diceSides;
        return new GDictionary
        {
            [$"{fieldPrefix}_count"] = diceCount,
            [$"{fieldPrefix}_sides"] = diceSides,
            [$"{fieldPrefix}_rolls"] = rolls,
            [$"{fieldPrefix}_total"] = diceTotal,
            [$"{fieldPrefix}_bonus"] = diceBonus,
            [$"{fieldPrefix}_max_total"] = maxTotal,
            [$"{fieldPrefix}_is_max"] = diceTotal == maxTotal,
        };
    }

    private int RollDamageDieVirtual(int diceSides)
    {
        return Call("_roll_damage_die", diceSides).AsInt32();
    }

    private static int BuildDicePoolTotal(int diceCount, int diceSides, StringName rollMode)
    {
        if (rollMode == DamagePreviewRollModeAverage)
        {
            return RoundToInt((double)diceCount * (diceSides + 1) / 2.0);
        }
        if (rollMode == DamagePreviewRollModeMaximum)
        {
            return diceCount * diceSides;
        }
        return 0;
    }

    private static GArray BuildPreviewDiceRolls(int diceCount, int diceSides, int diceTotal)
    {
        var rolls = new GArray();
        if (diceCount <= 0)
        {
            return rolls;
        }
        int remainingTotal = Math.Clamp(diceTotal, diceCount, diceCount * diceSides);
        for (int index = 0; index < diceCount; index++)
        {
            int remainingDice = diceCount - index;
            int roll = Math.Clamp(RoundToInt((double)remainingTotal / remainingDice), 1, diceSides);
            rolls.Add(roll);
            remainingTotal -= roll;
        }
        return rolls;
    }

    private static GDictionary BuildDamageDiceEventFlags(
        bool criticalHit,
        GDictionary skillRoll,
        GDictionary weaponRoll,
        GDictionary bonusSkillRoll = null
    )
    {
        bonusSkillRoll ??= new GDictionary();
        int skillDiceCount = DictInt(skillRoll, "damage_dice_count");
        int skillDiceSides = DictInt(skillRoll, "damage_dice_sides");
        int skillDiceTotal = DictInt(skillRoll, "damage_dice_total");
        int skillDiceMaxTotal = DictInt(skillRoll, "damage_dice_max_total");
        int bonusSkillDiceCount = DictInt(bonusSkillRoll, "bonus_damage_dice_count");
        int bonusSkillDiceSides = DictInt(bonusSkillRoll, "bonus_damage_dice_sides");
        int bonusSkillDiceTotal = DictInt(bonusSkillRoll, "bonus_damage_dice_total");
        int bonusSkillDiceMaxTotal = DictInt(bonusSkillRoll, "bonus_damage_dice_max_total");
        bool hasSkillDice =
            (skillDiceCount > 0 && skillDiceSides > 0 && skillDiceMaxTotal > 0)
            || (bonusSkillDiceCount > 0 && bonusSkillDiceSides > 0 && bonusSkillDiceMaxTotal > 0);
        skillDiceTotal += bonusSkillDiceTotal;
        skillDiceMaxTotal += bonusSkillDiceMaxTotal;

        int weaponDiceCount = DictInt(weaponRoll, "weapon_damage_dice_count");
        int weaponDiceSides = DictInt(weaponRoll, "weapon_damage_dice_sides");
        int weaponDiceTotal = DictInt(weaponRoll, "weapon_damage_dice_total");
        int weaponDiceMaxTotal = DictInt(weaponRoll, "weapon_damage_dice_max_total");
        bool hasWeaponDice = weaponDiceCount > 0 && weaponDiceSides > 0 && weaponDiceMaxTotal > 0;
        bool hasAnyRegularDice = hasSkillDice || hasWeaponDice;
        int regularDiceTotal = skillDiceTotal + weaponDiceTotal;
        int regularDiceMaxTotal = skillDiceMaxTotal + weaponDiceMaxTotal;

        GDictionary result = new()
        {
            ["damage_dice_high_total_roll"] = false,
            ["damage_dice_high_total_roll_reason"] = new StringName(""),
            ["skill_damage_dice_is_max"] = false,
            ["skill_damage_dice_is_max_reason"] = new StringName(""),
            ["weapon_damage_dice_is_max"] = false,
            ["weapon_damage_dice_is_max_reason"] = new StringName(""),
        };
        if (criticalHit && hasAnyRegularDice)
        {
            result["damage_dice_high_total_roll"] = true;
            result["damage_dice_high_total_roll_reason"] = DiceEventReasonCriticalHit;
        }
        else if (
            hasAnyRegularDice
            && regularDiceTotal * DamageDiceHighTotalThresholdDenominator
                >= regularDiceMaxTotal * DamageDiceHighTotalThresholdNumerator
        )
        {
            result["damage_dice_high_total_roll"] = true;
            result["damage_dice_high_total_roll_reason"] = DiceEventReasonDiceThreshold;
        }
        if (criticalHit && hasSkillDice)
        {
            result["skill_damage_dice_is_max"] = true;
            result["skill_damage_dice_is_max_reason"] = DiceEventReasonCriticalHit;
        }
        else if (hasSkillDice && skillDiceTotal == skillDiceMaxTotal)
        {
            result["skill_damage_dice_is_max"] = true;
            result["skill_damage_dice_is_max_reason"] = DiceEventReasonSkillDiceMax;
        }
        if (criticalHit && hasWeaponDice)
        {
            result["weapon_damage_dice_is_max"] = true;
            result["weapon_damage_dice_is_max_reason"] = DiceEventReasonCriticalHit;
        }
        else if (hasWeaponDice && weaponDiceTotal == weaponDiceMaxTotal)
        {
            result["weapon_damage_dice_is_max"] = true;
            result["weapon_damage_dice_is_max_reason"] = DiceEventReasonWeaponDiceMax;
        }
        return result;
    }

    private static void ApplyDamageDiceEventFlags(GDictionary result, GDictionary eventFlags)
    {
        foreach (var key in eventFlags.Keys)
        {
            result[key] = eventFlags[key];
        }
    }

    private static GDictionary EnsureDamageDiceEventDefaults(GDictionary @event)
    {
        @event ??= new GDictionary();
        if (!HasKey(@event, "damage_dice_high_total_roll"))
            @event["damage_dice_high_total_roll"] = false;
        if (!HasKey(@event, "damage_dice_high_total_roll_reason"))
            @event["damage_dice_high_total_roll_reason"] = new StringName("");
        if (!HasKey(@event, "skill_damage_dice_is_max"))
            @event["skill_damage_dice_is_max"] = false;
        if (!HasKey(@event, "skill_damage_dice_is_max_reason"))
            @event["skill_damage_dice_is_max_reason"] = new StringName("");
        if (!HasKey(@event, "weapon_damage_dice_is_max"))
            @event["weapon_damage_dice_is_max"] = false;
        if (!HasKey(@event, "weapon_damage_dice_is_max_reason"))
            @event["weapon_damage_dice_is_max_reason"] = new StringName("");
        return @event;
    }

    private static void AttachDamageEventAggregates(GDictionary result)
    {
        result["damage_dice_high_total_roll"] = false;
        result["skill_damage_dice_is_max"] = false;
        result["weapon_damage_dice_is_max"] = false;
        GArray damageEvents = GetArray(result, "damage_events");
        foreach (GDictionary eventValue in ReadDictionaryItems(damageEvents))
        {
            GDictionary damageEvent = EnsureDamageDiceEventDefaults(eventValue);
            if (DictBool(damageEvent, "damage_dice_high_total_roll"))
                result["damage_dice_high_total_roll"] = true;
            if (DictBool(damageEvent, "skill_damage_dice_is_max"))
                result["skill_damage_dice_is_max"] = true;
            if (DictBool(damageEvent, "weapon_damage_dice_is_max"))
                result["weapon_damage_dice_is_max"] = true;
        }
    }

    private double BuildOffenseMultiplier(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDef effectDef
    )
    {
        double multiplier = GetPreResistanceDamageMultiplier(effectDef);
        if (HasBonusCondition(effectDef, targetUnit))
        {
            multiplier *= GetDamageRatioMultiplier(effectDef);
        }
        if (HasStatusEffect(sourceUnit, StatusAttackUp))
        {
            multiplier *= 1.0 + 0.10 * GetStatusStrength(sourceUnit, StatusAttackUp);
        }
        if (sourceUnit != null && sourceUnit.has_status_effect(StatusArcherPreAim))
        {
            multiplier *= 1.15;
        }
        if (targetUnit != null && targetUnit.has_status_effect(StatusMarked))
        {
            multiplier *= 1.10;
        }
        multiplier *= GetLowLuckBloodDebtMultiplier(targetUnit);
        multiplier *= GetSourceOutgoingDamageMultiplier(sourceUnit);
        multiplier *= GetTargetIncomingDamageMultiplier(targetUnit);
        return Math.Max(multiplier, 0.0);
    }

    private static StringName ResolveDamageTag(
        BattleUnitState sourceUnit,
        CombatEffectDef effectDef
    )
    {
        if (ShouldUseWeaponPhysicalDamageTag(effectDef))
        {
            return GetUnitWeaponPhysicalDamageTag(sourceUnit);
        }
        StringName explicitEffectTag = effectDef?.damage_tag ?? new StringName("");
        return DamageTagContentRules.is_valid_damage_tag(explicitEffectTag)
            ? explicitEffectTag
            : new StringName("");
    }

    private static bool ShouldUseWeaponPhysicalDamageTag(CombatEffectDef effectDef)
    {
        return effectDef?.@params != null
            && DictBool(effectDef.@params, "use_weapon_physical_damage_tag");
    }

    private static StringName GetUnitWeaponPhysicalDamageTag(BattleUnitState unitState)
    {
        if (unitState == null)
        {
            return "";
        }
        StringName damageTag = unitState.weapon_physical_damage_tag;
        return DamageTagContentRules.is_valid_physical_damage_tag(damageTag)
            ? damageTag
            : new StringName("");
    }

    private GDictionary ResolveMitigationTierResult(
        BattleUnitState targetUnit,
        StringName damageTag
    )
    {
        if (targetUnit == null)
        {
            return new GDictionary { ["tier"] = MitigationTierNormal, ["sources"] = new GArray() };
        }
        var halfSources = new GArray();
        var doubleSources = new GArray();
        var immuneSources = new GArray();
        foreach (StringName statusId in SortedStatusIds(targetUnit.status_effects))
        {
            BattleStatusEffectState statusEntry = targetUnit.get_status_effect(statusId);
            if (
                statusEntry?.@params == null
                || !StatusParamsApplyToDamageTag(statusEntry.@params, damageTag)
            )
            {
                continue;
            }
            StringName mitigationTier = GetStringNameParam(
                statusEntry.@params,
                "mitigation_tier",
                ""
            );
            if (mitigationTier == MitigationTierImmune)
            {
                immuneSources.Add(
                    BuildMitigationSource(statusId, "mitigation_tier", 0, mitigationTier)
                );
            }
            else if (mitigationTier == MitigationTierHalf)
            {
                halfSources.Add(
                    BuildMitigationSource(statusId, "mitigation_tier", 0, mitigationTier)
                );
            }
            else if (mitigationTier == MitigationTierDouble)
            {
                doubleSources.Add(
                    BuildMitigationSource(statusId, "mitigation_tier", 0, mitigationTier)
                );
            }
        }
        AppendDamageResistanceSources(
            targetUnit,
            damageTag,
            halfSources,
            doubleSources,
            immuneSources
        );
        if (immuneSources.Count > 0)
            return new GDictionary { ["tier"] = MitigationTierImmune, ["sources"] = immuneSources };
        if (halfSources.Count > 0 && doubleSources.Count > 0)
        {
            var cancelled = new GArray();
            cancelled.AddRange(halfSources);
            cancelled.AddRange(doubleSources);
            return new GDictionary { ["tier"] = MitigationTierNormal, ["sources"] = cancelled };
        }
        if (halfSources.Count > 0)
            return new GDictionary { ["tier"] = MitigationTierHalf, ["sources"] = halfSources };
        if (doubleSources.Count > 0)
            return new GDictionary { ["tier"] = MitigationTierDouble, ["sources"] = doubleSources };
        return new GDictionary { ["tier"] = MitigationTierNormal, ["sources"] = new GArray() };
    }

    private static void AppendDamageResistanceSources(
        BattleUnitState targetUnit,
        StringName damageTag,
        GArray halfSources,
        GArray doubleSources,
        GArray immuneSources
    )
    {
        if (targetUnit == null || damageTag == "")
        {
            return;
        }
        foreach (var rawDamageTag in targetUnit.damage_resistances.Keys)
        {
            StringName resistanceDamageTag = ProgressionDataUtils.to_string_name(rawDamageTag);
            if (resistanceDamageTag != damageTag)
            {
                continue;
            }
            StringName mitigationTier = ProgressionDataUtils.to_string_name(
                targetUnit.damage_resistances[rawDamageTag]
            );
            StringName sourceId = new($"damage_resistance_{resistanceDamageTag}");
            if (mitigationTier == MitigationTierImmune)
                immuneSources.Add(
                    BuildMitigationSource(sourceId, "damage_resistance", 0, mitigationTier)
                );
            else if (mitigationTier == MitigationTierHalf)
                halfSources.Add(
                    BuildMitigationSource(sourceId, "damage_resistance", 0, mitigationTier)
                );
            else if (mitigationTier == MitigationTierDouble)
                doubleSources.Add(
                    BuildMitigationSource(sourceId, "damage_resistance", 0, mitigationTier)
                );
        }
    }

    private bool StatusParamsApplyToDamageTag(GDictionary @params, StringName damageTag)
    {
        if (@params == null || damageTag == "")
        {
            return true;
        }
        StringName explicitDamageTag = GetStringNameParam(@params, "damage_tag", "");
        if (explicitDamageTag != "")
        {
            return explicitDamageTag == damageTag;
        }
        GArray damageTagsValue = GetArrayParam(@params, "damage_tags", new GArray());
        if (damageTagsValue.Count > 0)
        {
            foreach (var tagValue in damageTagsValue)
            {
                if (ProgressionDataUtils.to_string_name(tagValue) == damageTag)
                {
                    return true;
                }
            }
            return false;
        }
        StringName damageCategory = GetStringNameParam(
            @params,
            "damage_category",
            ""
        );
        if (damageCategory == "physical")
        {
            return IsPhysicalDamageTag(damageTag);
        }
        if (damageCategory == "spell" || damageCategory == "magic" || damageCategory == "energy")
        {
            return !IsPhysicalDamageTag(damageTag);
        }
        return true;
    }

    private static bool IsPhysicalDamageTag(StringName damageTag)
    {
        return DamageTagContentRules.is_valid_physical_damage_tag(damageTag);
    }

    private GDictionary BuildFixedMitigation(
        BattleUnitState targetUnit,
        CombatEffectDef effectDef,
        StringName damageTag
    )
    {
        GDictionary buffReduction = ResolveBuffReductionResult(targetUnit);
        GDictionary stanceReduction = ResolveStanceReductionResult(targetUnit, damageTag);
        GDictionary passiveReduction = ResolvePassiveReductionResult(targetUnit);
        GDictionary contentDr = ResolveContentDrResult(targetUnit, effectDef, damageTag);
        GDictionary guardBlock = ResolveGuardBlockResult(targetUnit, damageTag);
        var sources = new GArray();
        sources.AddRange(GetArray(buffReduction, "sources"));
        sources.AddRange(GetArray(stanceReduction, "sources"));
        sources.AddRange(GetArray(passiveReduction, "sources"));
        sources.AddRange(GetArray(contentDr, "sources"));
        sources.AddRange(GetArray(guardBlock, "sources"));
        return new GDictionary
        {
            ["buff_reduction"] = DictInt(buffReduction, "value"),
            ["stance_reduction"] = DictInt(stanceReduction, "value"),
            ["passive_reduction"] = DictInt(passiveReduction, "value"),
            ["content_dr"] = DictInt(contentDr, "value"),
            ["guard_block"] = DictInt(guardBlock, "value"),
            ["fixed_mitigation_sources"] = sources,
            ["guard_ignore_applied"] = 0,
        };
    }

    private GDictionary ResolveBuffReductionResult(BattleUnitState targetUnit)
    {
        if (!HasStatusEffect(targetUnit, StatusDamageReductionUp))
        {
            return ZeroSourceResult();
        }
        int strength = GetStatusStrength(targetUnit, StatusDamageReductionUp);
        int value = Math.Max(strength, 0) * DamageReductionUpFixedPerPower;
        return new GDictionary
        {
            ["value"] = value,
            ["sources"] = new GArray
            {
                BuildMitigationSource(StatusDamageReductionUp, "buff_reduction", value),
            },
        };
    }

    private GDictionary ResolveStanceReductionResult(
        BattleUnitState targetUnit,
        StringName damageTag
    )
    {
        if (!IsPhysicalDamageTag(damageTag) || !HasStatusEffect(targetUnit, StatusGuarding))
        {
            return ZeroSourceResult();
        }
        int value = Math.Max(GetStatusStrength(targetUnit, StatusGuarding), 0);
        return new GDictionary
        {
            ["value"] = value,
            ["sources"] = new GArray
            {
                BuildMitigationSource(StatusGuarding, "stance_reduction", value),
            },
        };
    }

    private GDictionary ResolvePassiveReductionResult(BattleUnitState targetUnit)
    {
        if (targetUnit == null)
        {
            return ZeroSourceResult();
        }
        int maxPassiveReduction = 0;
        var sources = new GArray();
        foreach (StringName statusId in SortedStatusIds(targetUnit.status_effects))
        {
            BattleStatusEffectState statusEntry = targetUnit.get_status_effect(statusId);
            if (statusEntry?.@params == null)
            {
                continue;
            }
            int passiveReduction = Math.Max(
                GetIntParam(statusEntry.@params, "passive_reduction", 0),
                0
            );
            if (passiveReduction <= 0)
            {
                continue;
            }
            if (passiveReduction > maxPassiveReduction)
            {
                maxPassiveReduction = passiveReduction;
                sources.Clear();
                sources.Add(BuildMitigationSource(statusId, "passive_reduction", passiveReduction));
            }
            else if (passiveReduction == maxPassiveReduction)
            {
                sources.Add(BuildMitigationSource(statusId, "passive_reduction", passiveReduction));
            }
        }
        return new GDictionary { ["value"] = maxPassiveReduction, ["sources"] = sources };
    }

    private GDictionary ResolveContentDrResult(
        BattleUnitState targetUnit,
        CombatEffectDef effectDef,
        StringName damageTag
    )
    {
        if (targetUnit == null || !IsPhysicalDamageTag(damageTag))
        {
            return ZeroSourceResult();
        }
        int maxContentDr = 0;
        var sources = new GArray();
        foreach (StringName statusId in SortedStatusIds(targetUnit.status_effects))
        {
            BattleStatusEffectState statusEntry = targetUnit.get_status_effect(statusId);
            if (
                statusEntry?.@params == null
                || !StatusParamsApplyToDamageTag(statusEntry.@params, damageTag)
            )
            {
                continue;
            }
            int contentDr = Math.Max(
                GetIntParam(statusEntry.@params, "content_dr", 0),
                0
            );
            if (contentDr <= 0)
            {
                continue;
            }
            StringName bypassTag = GetStringNameParam(
                statusEntry.@params,
                "dr_bypass_tag",
                ""
            );
            if (bypassTag != "" && EffectHasBypassTag(effectDef, bypassTag))
            {
                continue;
            }
            if (contentDr > maxContentDr)
            {
                maxContentDr = contentDr;
                sources.Clear();
                sources.Add(BuildMitigationSource(statusId, "content_dr", contentDr));
            }
            else if (contentDr == maxContentDr)
            {
                sources.Add(BuildMitigationSource(statusId, "content_dr", contentDr));
            }
        }
        return new GDictionary { ["value"] = maxContentDr, ["sources"] = sources };
    }

    private GDictionary ResolveGuardBlockResult(BattleUnitState targetUnit, StringName damageTag)
    {
        if (targetUnit == null)
        {
            return ZeroSourceResult();
        }
        int maxGuardBlock = 0;
        var sources = new GArray();
        foreach (StringName statusId in SortedStatusIds(targetUnit.status_effects))
        {
            BattleStatusEffectState statusEntry = targetUnit.get_status_effect(statusId);
            if (
                statusEntry?.@params == null
                || !StatusParamsApplyToDamageTag(statusEntry.@params, damageTag)
            )
            {
                continue;
            }
            int guardBlock = Math.Max(
                GetIntParam(statusEntry.@params, "guard_block", 0),
                0
            );
            if (guardBlock <= 0)
            {
                continue;
            }
            if (guardBlock > maxGuardBlock)
            {
                maxGuardBlock = guardBlock;
                sources.Clear();
                sources.Add(BuildMitigationSource(statusId, "guard_block", guardBlock));
            }
            else if (guardBlock == maxGuardBlock)
            {
                sources.Add(BuildMitigationSource(statusId, "guard_block", guardBlock));
            }
        }
        return new GDictionary { ["value"] = maxGuardBlock, ["sources"] = sources };
    }

    private static GDictionary ZeroSourceResult()
    {
        return new GDictionary { ["value"] = 0, ["sources"] = new GArray() };
    }

    private static GDictionary BuildMitigationSource(
        StringName statusId,
        string sourceType,
        int value = 0,
        StringName tier = default
    )
    {
        return new GDictionary
        {
            ["status_id"] = statusId.ToString(),
            ["type"] = sourceType,
            ["value"] = value,
            ["tier"] = (tier == default ? new StringName("") : tier).ToString(),
        };
    }

    private void ApplyBlackStarBrandGuardIgnore(GDictionary mitigation, BattleUnitState targetUnit)
    {
        if (
            mitigation == null
            || targetUnit == null
            || !targetUnit.has_status_effect(StatusBlackStarBrandEliteGuardWindow)
        )
        {
            return;
        }
        int remainingIgnore = BlackStarBrandGuardIgnoreFlat;
        int ignoredTotal = ApplyIgnoreToMitigationField(
            mitigation,
            "guard_block",
            ref remainingIgnore
        );
        ignoredTotal += ApplyIgnoreToMitigationField(
            mitigation,
            "stance_reduction",
            ref remainingIgnore
        );
        mitigation["guard_ignore_applied"] = ignoredTotal;
        targetUnit.erase_status_effect(StatusBlackStarBrandEliteGuardWindow);
    }

    private static int ApplyIgnoreToMitigationField(
        GDictionary mitigation,
        string field,
        ref int remainingIgnore
    )
    {
        if (remainingIgnore <= 0)
        {
            return 0;
        }
        int value = Math.Max(DictInt(mitigation, field), 0);
        if (value <= 0)
        {
            return 0;
        }
        int ignored = Math.Min(value, remainingIgnore);
        mitigation[field] = value - ignored;
        remainingIgnore -= ignored;
        return ignored;
    }

    private void ApplyLowLuckBlackStarWedgeGuardIgnore(
        GDictionary mitigation,
        BattleUnitState sourceUnit
    )
    {
        if (mitigation == null || sourceUnit == null)
        {
            return;
        }
        if (!LowLuckRelicRules.unit_has_flag(sourceUnit, LowLuckRelicRules.attr_black_star_wedge()))
        {
            return;
        }
        string flag = LowLuckRelicRules.battle_flag_black_star_wedge_used();
        if (DictBool(sourceUnit.ai_blackboard, flag))
        {
            return;
        }
        sourceUnit.ai_blackboard[flag] = true;
        int remainingIgnore = LowLuckRelicRules.black_star_wedge_guard_ignore_flat();
        int ignoredTotal = ApplyIgnoreToMitigationField(
            mitigation,
            "guard_block",
            ref remainingIgnore
        );
        ignoredTotal += ApplyIgnoreToMitigationField(
            mitigation,
            "stance_reduction",
            ref remainingIgnore
        );
        mitigation["guard_ignore_applied"] =
            DictInt(mitigation, "guard_ignore_applied") + ignoredTotal;
        mitigation["low_luck_black_star_wedge_triggered"] = true;
    }

    private static void TrimFixedMitigationSources(GDictionary mitigation)
    {
        if (mitigation == null)
        {
            return;
        }
        GArray sources = GetArray(mitigation, "fixed_mitigation_sources");
        var filteredSources = new GArray();
        foreach (GDictionary source in ReadDictionaryItems(sources))
        {
            string sourceType = DictString(source, "type");
            int remaining = sourceType switch
            {
                "buff_reduction" => DictInt(mitigation, "buff_reduction"),
                "stance_reduction" => DictInt(mitigation, "stance_reduction"),
                "passive_reduction" => DictInt(mitigation, "passive_reduction"),
                "content_dr" => DictInt(mitigation, "content_dr"),
                "guard_block" => DictInt(mitigation, "guard_block"),
                _ => 0,
            };
            if (remaining <= 0)
            {
                continue;
            }
            GDictionary updatedSource = DuplicateDictionary(source, false);
            updatedSource["value"] = remaining;
            filteredSources.Add(updatedSource);
        }
        mitigation["fixed_mitigation_sources"] = filteredSources;
    }

    private static bool EffectHasBypassTag(CombatEffectDef effectDef, StringName bypassTag)
    {
        return effectDef?.@params != null
            && bypassTag != ""
            && ProgressionDataUtils.to_string_name(
                effectDef.@params.GetValueOrDefault("dr_bypass_tag", "")
            )
                == bypassTag;
    }

    private bool HasBonusCondition(CombatEffectDef effectDef, BattleUnitState targetUnit)
    {
        if (effectDef == null || targetUnit == null)
        {
            return false;
        }
        if (effectDef.bonus_condition == BonusConditionTargetLowHp)
        {
            return IsTargetLowHp(effectDef, targetUnit);
        }
        if (effectDef.bonus_condition == BonusConditionTargetDebuffCount)
        {
            return TargetHasEnoughDebuffs(effectDef, targetUnit);
        }
        return false;
    }

    private static bool IsTargetLowHp(CombatEffectDef effectDef, BattleUnitState targetUnit)
    {
        int maxHp = GetAttributeValue(targetUnit, AttributeService.HP_MAX_ID());
        if (maxHp <= 0)
        {
            maxHp = Math.Max(targetUnit.current_hp, 1);
        }
        int thresholdPercent = 50;
        if (effectDef?.@params != null && HasKey(effectDef.@params, "hp_ratio_threshold_percent"))
        {
            thresholdPercent = Math.Clamp(
                DictInt(effectDef.@params, "hp_ratio_threshold_percent", thresholdPercent),
                0,
                100
            );
        }
        return targetUnit.current_hp * 100 <= maxHp * thresholdPercent;
    }

    private static bool TargetHasEnoughDebuffs(
        CombatEffectDef effectDef,
        BattleUnitState targetUnit
    )
    {
        if (targetUnit == null)
        {
            return false;
        }
        int threshold =
            effectDef?.@params != null
                ? Math.Max(DictInt(effectDef.@params, "debuff_count_threshold", 3), 1)
                : 3;
        int count = 0;
        foreach (StringName statusId in SortedStatusIds(targetUnit.status_effects))
        {
            if (BattleStatusSemanticTable.is_harmful_status(statusId))
            {
                count += 1;
                if (count >= threshold)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static double GetDamageRatioMultiplier(CombatEffectDef effectDef)
    {
        return effectDef == null ? 1.0 : Math.Max(effectDef.damage_ratio_percent / 100.0, 0.0);
    }

    private static double GetPreResistanceDamageMultiplier(CombatEffectDef effectDef)
    {
        return effectDef?.@params == null
            ? 1.0
            : Math.Max(
                DictFloat(effectDef.@params, "runtime_pre_resistance_damage_multiplier", 1.0),
                0.0
            );
    }

    private static bool ShouldAddWeaponDice(CombatEffectDef effectDef)
    {
        return effectDef?.@params != null && DictBool(effectDef.@params, "add_weapon_dice");
    }

    private static GDictionary GetCurrentWeaponDamageDice(BattleUnitState unitState)
    {
        if (unitState == null)
        {
            return new GDictionary();
        }
        return unitState.weapon_uses_two_hands
            ? unitState.weapon_two_handed_dice
            : unitState.weapon_one_handed_dice;
    }

    private static int GetRollTotalWithBonus(GDictionary rollData, string fieldPrefix)
    {
        return GetRollTotal(rollData, fieldPrefix) + DictInt(rollData, $"{fieldPrefix}_bonus");
    }

    private static int GetRollTotal(GDictionary rollData, string fieldPrefix)
    {
        return rollData == null || rollData.Count == 0 || string.IsNullOrEmpty(fieldPrefix)
            ? 0
            : DictInt(rollData, $"{fieldPrefix}_total");
    }

    private GDictionary BuildInvalidDamageTagOutcome(
        BattleUnitState sourceUnit,
        CombatEffectDef effectDef
    )
    {
        StringName sourceLabel = "effect.damage_tag";
        StringName configuredTag;
        if (ShouldUseWeaponPhysicalDamageTag(effectDef))
        {
            sourceLabel = "weapon_physical_damage_tag";
            configuredTag = ProgressionDataUtils.to_string_name(
                sourceUnit != null
                    ? sourceUnit.weapon_physical_damage_tag
                    : Variant.From(new StringName(""))
            );
        }
        else
        {
            configuredTag = ProgressionDataUtils.to_string_name(
                effectDef != null
                    ? effectDef.damage_tag
                    : Variant.From(new StringName(""))
            );
        }
        StringName reason = configuredTag == "" ? "missing_damage_tag" : "unsupported_damage_tag";
        return new GDictionary
        {
            ["invalid_damage_tag"] = true,
            ["error_code"] = "invalid_damage_tag",
            ["reason"] = reason,
            ["damage_tag_source"] = sourceLabel,
            ["damage_tag"] = configuredTag,
            ["mitigation_tier"] = MitigationTierNormal,
            ["mitigation_sources"] = new GArray(),
            ["base_damage"] = 0,
            ["rolled_damage"] = 0,
            ["tier_adjusted_damage"] = 0,
            ["resolved_damage"] = 0,
            ["fixed_mitigation_sources"] = new GArray(),
            ["fixed_mitigation_total"] = 0,
            ["fully_absorbed_by_mitigation"] = false,
        };
    }

    private static GDictionary BuildInvalidDamageTagDiagnostic(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDef effectDef,
        GDictionary damageOutcome
    )
    {
        return new GDictionary
        {
            ["error_code"] = "invalid_damage_tag",
            ["reason"] = DictString(damageOutcome, "reason"),
            ["damage_tag_source"] = DictString(damageOutcome, "damage_tag_source"),
            ["damage_tag"] = DictString(damageOutcome, "damage_tag"),
            ["effect_type"] = ProgressionDataUtils
                .to_string_name(
                    effectDef != null
                        ? effectDef.effect_type
                        : Variant.From(new StringName(""))
                )
                .ToString(),
            ["source_unit_id"] = sourceUnit != null ? sourceUnit.unit_id.ToString() : "",
            ["target_unit_id"] = targetUnit != null ? targetUnit.unit_id.ToString() : "",
        };
    }

    private GDictionary ResolveCritTraitResult(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDef effectDef,
        bool criticalHit
    )
    {
        if (!criticalHit)
        {
            return new GDictionary();
        }
        return _trait_trigger_hooks.on_crit(
            sourceUnit,
            targetUnit,
            new GDictionary
            {
                ["critical_hit"] = criticalHit,
                ["add_weapon_dice"] = ShouldAddWeaponDice(effectDef),
                ["weapon_attack_range"] = sourceUnit != null ? sourceUnit.weapon_attack_range : 0,
                ["weapon_dice"] = GetCurrentWeaponDamageDice(sourceUnit),
            }
        );
    }

    private GDictionary ResolveFatalDamageTraitResult(
        BattleUnitState targetUnit,
        BattleUnitState sourceUnit,
        GDictionary damageOutcome,
        int hpDamage,
        int projectedHp
    )
    {
        return _trait_trigger_hooks.on_fatal_damage(
            targetUnit,
            sourceUnit,
            new GDictionary
            {
                ["damage_outcome"] = damageOutcome,
                ["hp_damage"] = hpDamage,
                ["projected_hp"] = projectedHp,
            }
        );
    }

    private static bool DoesSaveBlockEffect(GDictionary saveResult)
    {
        return saveResult != null
            && DictBool(saveResult, "has_save")
            && DictBool(saveResult, "success");
    }

    private static StringName ResolveStatusIdForSave(
        CombatEffectDef effectDef,
        GDictionary saveResult
    )
    {
        if (effectDef == null)
        {
            return "";
        }
        if (
            saveResult != null
            && DictBool(saveResult, "has_save")
            && !DictBool(saveResult, "success")
            && effectDef.save_failure_status_id != ""
        )
        {
            return ProgressionDataUtils.to_string_name(effectDef.save_failure_status_id);
        }
        return ProgressionDataUtils.to_string_name(effectDef.status_id);
    }

    private GDictionary ApplyDispelMagicEffect(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDef effectDef
    )
    {
        if (targetUnit == null || effectDef == null)
        {
            return new GDictionary();
        }
        GDictionary @params = effectDef.@params ?? new GDictionary();
        bool sameFaction = sourceUnit != null && sourceUnit.faction_id == targetUnit.faction_id;
        bool removeHarmful =
            DictBool(@params, "remove_harmful")
            || (sameFaction && DictBool(@params, "remove_harmful_from_allies", true));
        bool removeBeneficial =
            DictBool(@params, "remove_beneficial")
            || (!sameFaction && DictBool(@params, "remove_beneficial_from_enemies", true));
        int maxRemoved = Math.Max(
            DictInt(@params, "max_status_removed", Math.Max(effectDef.power, 1)),
            1
        );
        var candidates = new List<StringName>();
        foreach (StringName statusId in SortedStatusIds(targetUnit.status_effects))
        {
            BattleStatusEffectState statusEntry = targetUnit.get_status_effect(statusId);
            if (statusEntry == null)
            {
                continue;
            }
            if (
                removeHarmful
                && BattleStatusSemanticTable.is_dispellable_harmful_status_entry(statusEntry)
            )
            {
                candidates.Add(statusId);
            }
            else if (
                removeBeneficial
                && BattleStatusSemanticTable.is_dispellable_beneficial_status_entry(statusEntry)
            )
            {
                candidates.Add(statusId);
            }
        }
        candidates.Sort(
            (left, right) =>
            {
                int priorityCompare = BattleStatusSemanticTable
                    .get_dispel_priority(right)
                    .CompareTo(BattleStatusSemanticTable.get_dispel_priority(left));
                return priorityCompare != 0
                    ? priorityCompare
                    : left.ToString().CompareTo(right.ToString());
            }
        );
        var removedStatusIds = new GStringNameArray();
        foreach (StringName statusId in candidates)
        {
            if (removedStatusIds.Count >= maxRemoved)
            {
                break;
            }
            targetUnit.erase_status_effect(statusId);
            removedStatusIds.Add(statusId);
        }
        if (removedStatusIds.Count == 0)
        {
            return new GDictionary();
        }
        return new GDictionary
        {
            ["effect_type"] = EffectDispelMagic.ToString(),
            ["target_unit_id"] = targetUnit.unit_id.ToString(),
            ["mode"] = sameFaction ? "ally_harmful" : "enemy_beneficial",
            ["max_status_removed"] = maxRemoved,
            ["removed_status_ids"] = removedStatusIds.Duplicate(),
        };
    }

    private GDictionary ApplyEquipmentDurabilityDamageEffect(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDef effectDef,
        GDictionary damageContext,
        int totalDamage,
        int totalShieldAbsorbed
    )
    {
        if (targetUnit == null || effectDef == null)
        {
            return new GDictionary();
        }
        GDictionary @params = effectDef.@params ?? new GDictionary();
        bool attackSuccess = DictBool(damageContext, "attack_success");
        if (
            DictBool(@params, "require_damage_applied")
            && !attackSuccess
            && totalDamage <= 0
            && totalShieldAbsorbed <= 0
        )
        {
            return new GDictionary();
        }
        GDictionary selection = SelectEquipmentForDurabilityDamage(
            targetUnit,
            effectDef,
            damageContext
        );
        if (selection.Count == 0)
        {
            return new GDictionary();
        }
        EquipmentState equipmentView = targetUnit.get_equipment_view();
        StringName entrySlotId = DictStringName(selection, "entry_slot_id");
        GodotObject equipmentInstance = GetObject(selection, "equipment_instance");
        if (equipmentView == null || entrySlotId == "" || equipmentInstance == null)
        {
            return new GDictionary();
        }
        int before = Math.Max(GetInt(equipmentInstance, "current_durability"), 0);
        if (before <= 0)
        {
            equipmentView.clear_entry_slot(entrySlotId);
            return new GDictionary();
        }
        int rarity = GetInt(equipmentInstance, "rarity");
        GDictionary saveResult = ResolveEquipmentDurabilitySave(
            sourceUnit,
            targetUnit,
            effectDef,
            damageContext,
            rarity
        );
        GDictionary @event = new()
        {
            ["effect_type"] = EffectEquipmentDurabilityDamage.ToString(),
            ["target_unit_id"] = targetUnit.unit_id.ToString(),
            ["entry_slot_id"] = entrySlotId.ToString(),
            ["slot_id"] = DictString(selection, "slot_id", entrySlotId.ToString()),
            ["item_id"] = GetString(equipmentInstance, "item_id"),
            ["instance_id"] = GetString(equipmentInstance, "instance_id"),
            ["rarity"] = rarity,
            ["durability_before"] = before,
            ["durability_after"] = before,
            ["durability_loss"] = 0,
            ["destroyed"] = false,
            ["save_result"] = DuplicateDictionary(saveResult),
        };
        if (DictBool(saveResult, "has_save") && DictBool(saveResult, "success"))
        {
            return @event;
        }
        int durabilityLoss = Math.Min(Math.Max(effectDef.power, 0), before);
        int after = before - durabilityLoss;
        @event["durability_loss"] = durabilityLoss;
        @event["durability_after"] = Math.Max(after, 0);
        if (after <= 0)
        {
            equipmentView.clear_entry_slot(entrySlotId);
            @event["destroyed"] = true;
        }
        else
        {
            equipmentInstance.Set("current_durability", after);
        }
        return @event;
    }

    private static GDictionary ResolveEquipmentDurabilitySave(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDef effectDef,
        GDictionary damageContext,
        int rarity
    )
    {
        GDictionary saveResult = BattleSaveResolver.resolve_save(
            sourceUnit,
            targetUnit,
            effectDef,
            damageContext ?? new GDictionary()
        );
        int rarityBonus = EquipmentDurabilityRules.GetDisjunctionSaveBonusForRarity(rarity);
        saveResult["equipment_rarity_bonus"] = rarityBonus;
        if (!DictBool(saveResult, "has_save"))
        {
            return saveResult;
        }
        saveResult["status_save_bonus"] = DictInt(saveResult, "bonus");
        saveResult["bonus"] = DictInt(saveResult, "bonus") + rarityBonus;
        if (DictBool(saveResult, "immune"))
        {
            return saveResult;
        }
        int naturalRoll = DictInt(saveResult, "natural_roll");
        int rollTotal = DictInt(saveResult, "roll_total") + rarityBonus;
        saveResult["roll_total"] = rollTotal;
        bool success = rollTotal >= DictInt(saveResult, "dc");
        if (naturalRoll <= 1)
            success = false;
        else if (naturalRoll >= 20)
            success = true;
        saveResult["success"] = success;
        return saveResult;
    }

    private GDictionary SelectEquipmentForDurabilityDamage(
        BattleUnitState targetUnit,
        CombatEffectDef effectDef,
        GDictionary damageContext
    )
    {
        if (targetUnit == null)
        {
            return new GDictionary();
        }
        EquipmentState equipmentView = targetUnit.get_equipment_view();
        if (equipmentView == null)
        {
            return new GDictionary();
        }
        StringName overrideSlot = DictStringName(damageContext, "equipment_slot_override");
        if (overrideSlot == "" && effectDef?.@params != null)
        {
            overrideSlot = DictStringName(effectDef.@params, "equipment_slot_override");
        }
        if (overrideSlot != "")
        {
            StringName overrideEntrySlot = ProgressionDataUtils.to_string_name(
                equipmentView.get_entry_slot_for_slot(overrideSlot)
            );
            return BuildEquipmentDurabilitySelection(
                equipmentView,
                overrideEntrySlot,
                overrideSlot
            );
        }

        GStringNameArray allowedSlots = GetEquipmentDurabilityTargetSlots(effectDef);
        var candidates = new GArray();
        int totalWeight = 0;
        foreach (var entrySlotValue in equipmentView.get_entry_slot_ids())
        {
            StringName entrySlotId = ProgressionDataUtils.to_string_name(entrySlotValue);
            GDictionary selection = BuildEquipmentDurabilitySelection(
                equipmentView,
                entrySlotId,
                entrySlotId
            );
            if (selection.Count == 0)
            {
                continue;
            }
            GStringNameArray occupiedSlots = ToStringNameArray(
                GetArray(selection, "occupied_slot_ids")
            );
            if (!IsEquipmentDurabilityEntryAllowed(entrySlotId, occupiedSlots, allowedSlots))
            {
                continue;
            }
            int weight = GetEquipmentDurabilitySlotWeight(effectDef, entrySlotId, occupiedSlots);
            if (weight <= 0)
            {
                continue;
            }
            totalWeight += weight;
            candidates.Add(new GDictionary { ["selection"] = selection, ["weight"] = weight });
        }
        if (candidates.Count == 0 || totalWeight <= 0)
        {
            return new GDictionary();
        }
        int roll = TrueRandomSeedService.randi_range(1, totalWeight);
        int cursor = 0;
        foreach (var candidateValue in candidates)
        {
            GDictionary candidate = candidateValue.AsGodotDictionary();
            cursor += DictInt(candidate, "weight");
            if (roll <= cursor)
            {
                return DuplicateDictionary(GetDictionary(candidate, "selection"));
            }
        }
        return DuplicateDictionary(GetDictionary(candidates[^1].AsGodotDictionary(), "selection"));
    }

    private static GDictionary BuildEquipmentDurabilitySelection(
        EquipmentState equipmentView,
        StringName entrySlotId,
        StringName slotId
    )
    {
        StringName normalizedEntrySlot = ProgressionDataUtils.to_string_name(entrySlotId);
        if (equipmentView == null || normalizedEntrySlot == "")
        {
            return new GDictionary();
        }
        EquipmentEntryState entry = equipmentView.get_entry(normalizedEntrySlot);
        if (entry == null || entry.is_empty())
        {
            return new GDictionary();
        }
        EquipmentInstanceState equipmentInstance = entry.get_equipment_instance();
        if (equipmentInstance == null || GetInt(equipmentInstance, "current_durability") <= 0)
        {
            return new GDictionary();
        }
        return new GDictionary
        {
            ["entry_slot_id"] = normalizedEntrySlot,
            ["slot_id"] = ProgressionDataUtils.to_string_name(slotId),
            ["occupied_slot_ids"] = GetArray(entry, "occupied_slot_ids").Duplicate(),
            ["entry"] = entry,
            ["equipment_instance"] = equipmentInstance,
        };
    }

    private static GStringNameArray GetEquipmentDurabilityTargetSlots(CombatEffectDef effectDef)
    {
        var result = new GStringNameArray();
        if (effectDef?.@params == null)
        {
            return result;
        }
        foreach (
            StringName slotId in ProgressionDataUtils.to_string_name_array(
                effectDef.@params.GetValueOrDefault("target_slots", new GArray())
            )
        )
        {
            if (EquipmentRules.is_valid_slot(slotId) && !result.Contains(slotId))
            {
                result.Add(slotId);
            }
        }
        return result;
    }

    private static bool IsEquipmentDurabilityEntryAllowed(
        StringName entrySlotId,
        GStringNameArray occupiedSlots,
        GStringNameArray allowedSlots
    )
    {
        if (allowedSlots.Count == 0 || allowedSlots.Contains(entrySlotId))
        {
            return true;
        }
        foreach (StringName occupiedSlotId in occupiedSlots)
        {
            if (allowedSlots.Contains(occupiedSlotId))
            {
                return true;
            }
        }
        return false;
    }

    private static int GetEquipmentDurabilitySlotWeight(
        CombatEffectDef effectDef,
        StringName entrySlotId,
        GStringNameArray occupiedSlots
    )
    {
        if (effectDef?.@params == null)
        {
            return 1;
        }
        GDictionary weightMap = GetDictionary(effectDef.@params, "slot_weight_map");
        if (weightMap.Count == 0)
        {
            return 1;
        }
        int weight = GetEquipmentDurabilityWeightForSlot(weightMap, entrySlotId);
        foreach (StringName occupiedSlotId in occupiedSlots)
        {
            weight = Math.Max(
                weight,
                GetEquipmentDurabilityWeightForSlot(weightMap, occupiedSlotId)
            );
        }
        return Math.Max(weight, 1);
    }

    private static int GetEquipmentDurabilityWeightForSlot(GDictionary weightMap, StringName slotId)
    {
        if (weightMap == null)
        {
            return 0;
        }
        if (TryGet(weightMap, slotId, out var directValue))
        {
            return directValue.AsInt32();
        }
        return 0;
    }

    private GDictionary ResolveExecuteEffect(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDef effectDef,
        GDictionary context,
        GStringNameArray statusEffectIds,
        GArray saveResults
    )
    {
        GDictionary @params = effectDef.@params ?? new GDictionary();
        if (DictBool(@params, "staged_execution"))
        {
            return ResolveStagedExecuteEffect(
                sourceUnit,
                targetUnit,
                effectDef,
                context,
                statusEffectIds,
                saveResults,
                @params
            );
        }
        GDictionary executePlan = BattleExecutionRules.build_execute_plan(
            sourceUnit,
            targetUnit,
            @params
        );
        if (
            ProgressionDataUtils.to_string_name(executePlan.GetValueOrDefault("branch", ""))
            == BattleExecutionRules.BRANCH_INVALID_TARGET()
        )
        {
            return new GDictionary();
        }
        GDictionary saveResult = BattleSaveResolver.resolve_save(
            sourceUnit,
            targetUnit,
            effectDef,
            context ?? new GDictionary()
        );
        if (DictBool(saveResult, "has_save"))
        {
            saveResults.Add(DuplicateDictionary(saveResult));
        }
        GDictionary soulFractureParams = GetDictionary(executePlan, "soul_fracture_params");
        if (DictBool(saveResult, "success", true))
        {
            var tempEffectDef = new CombatEffectDef
            {
                effect_type = "apply_status",
                status_id = DictStringName(soulFractureParams, "status_id", "soul_fracture"),
                duration_tu = DictInt(soulFractureParams, "duration_tu", 60),
                @params = DuplicateDictionary(soulFractureParams),
            };
            if (ApplyStatusEffect(targetUnit, sourceUnit, tempEffectDef, tempEffectDef.status_id))
            {
                AddUnique(statusEffectIds, tempEffectDef.status_id);
                return new GDictionary
                {
                    ["applied"] = true,
                    ["execute_stage"] = 0,
                    ["execute_outcome"] = "resisted",
                };
            }
            return new GDictionary
            {
                ["applied"] = false,
                ["execute_stage"] = 0,
                ["execute_outcome"] = "resisted",
            };
        }
        int fatalDamage = Math.Max(DictInt(executePlan, "fatal_damage", targetUnit.current_hp), 0);
        GDictionary fatalOutcome = new()
        {
            ["damage_tag"] = ProgressionDataUtils.to_string_name(effectDef.damage_tag),
            ["resolved_damage"] = fatalDamage,
            ["min_hp_after_damage"] = 0,
            ["bypass_shield"] = true,
            ["bypass_death_prevention"] = true,
            ["shield_absorption_percent"] = 0.0,
            ["execute_stage"] = 2,
            ["execute_outcome"] = "failed_save_fatal",
            ["death_source"] = "power_word_kill_execute",
            ["death_source_priority"] = 900,
        };
        GDictionary fatalResult = ApplyDamageToTarget(targetUnit, fatalOutcome, sourceUnit);
        return new GDictionary
        {
            ["applied"] = true,
            ["execute_stage"] = 2,
            ["execute_outcome"] = "failed_save_fatal",
            ["damage_result"] = fatalResult,
        };
    }

    private GDictionary ResolveStagedExecuteEffect(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDef effectDef,
        GDictionary context,
        GStringNameArray statusEffectIds,
        GArray saveResults,
        GDictionary @params
    )
    {
        GDictionary saveResult = BattleSaveResolver.resolve_save(
            sourceUnit,
            targetUnit,
            effectDef,
            context ?? new GDictionary()
        );
        if (DictBool(saveResult, "has_save"))
        {
            saveResults.Add(DuplicateDictionary(saveResult));
        }

        int threshold = BattleExecutionRules.resolve_threshold(sourceUnit, targetUnit, @params);
        bool isVulnerable = targetUnit.current_hp <= threshold;
        bool isBoss = BattleExecutionRules.is_boss_target(targetUnit);
        var damageResults = new GArray();
        bool applied = false;

        if (!isVulnerable || isBoss)
        {
            int nonLethalDamage = BattleExecutionRules.resolve_non_lethal_damage(
                sourceUnit,
                targetUnit,
                @params,
                isBoss
            );
            GDictionary nonLethalOutcome = BuildStagedExecuteDamageOutcome(
                effectDef,
                @params,
                nonLethalDamage,
                1
            );
            GDictionary nonLethalResult = ApplyDamageToTarget(
                targetUnit,
                nonLethalOutcome,
                sourceUnit
            );
            damageResults.Add(DuplicateDictionary(nonLethalResult));
            applied = true;
            if (
                DictInt(nonLethalResult, "damage") > 0
                || DictInt(nonLethalResult, "shield_absorbed") > 0
            )
            {
                GrantStatusOnHitToSource(sourceUnit, effectDef, context);
            }
        }
        else
        {
            int burstDamage = Math.Max(DictInt(@params, "burst_damage", 9999), 0);
            GDictionary burstOutcome = BuildStagedExecuteDamageOutcome(
                effectDef,
                @params,
                burstDamage,
                1
            );
            GDictionary burstResult = ApplyDamageToTarget(targetUnit, burstOutcome, sourceUnit);
            damageResults.Add(DuplicateDictionary(burstResult));
            applied = true;
            if (DictInt(burstResult, "damage") > 0 || DictInt(burstResult, "shield_absorbed") > 0)
            {
                GrantStatusOnHitToSource(sourceUnit, effectDef, context);
            }

            if (!DictBool(saveResult, "success", true) && targetUnit.current_hp <= 1)
            {
                int finisherDamage = Math.Max(DictInt(@params, "finisher_damage", 1), 0);
                GDictionary finisherOutcome = BuildStagedExecuteDamageOutcome(
                    effectDef,
                    @params,
                    finisherDamage,
                    0
                );
                GDictionary finisherResult = ApplyDamageToTarget(
                    targetUnit,
                    finisherOutcome,
                    sourceUnit
                );
                damageResults.Add(DuplicateDictionary(finisherResult));
                if (
                    DictInt(finisherResult, "damage") > 0
                    || DictInt(finisherResult, "shield_absorbed") > 0
                )
                {
                    GrantStatusOnHitToSource(sourceUnit, effectDef, context);
                }
            }
        }

        GDictionary soulFractureParams = GetDictionary(@params, "soul_fracture_status");
        if (soulFractureParams.Count > 0)
        {
            var tempEffectDef = new CombatEffectDef
            {
                effect_type = "apply_status",
                status_id = DictStringName(soulFractureParams, "status_id", "soul_fracture"),
                duration_tu = DictInt(soulFractureParams, "duration_tu", 60),
                @params = DuplicateDictionary(soulFractureParams),
            };
            if (ApplyStatusEffect(targetUnit, sourceUnit, tempEffectDef, tempEffectDef.status_id))
            {
                AddUnique(statusEffectIds, tempEffectDef.status_id);
                applied = true;
            }
        }

        return new GDictionary { ["applied"] = applied, ["damage_results"] = damageResults };
    }

    private static GDictionary BuildStagedExecuteDamageOutcome(
        CombatEffectDef effectDef,
        GDictionary @params,
        int resolvedDamage,
        int minHpAfterDamage
    )
    {
        GDictionary outcome = new()
        {
            ["resolved_damage"] = Math.Max(resolvedDamage, 0),
            ["min_hp_after_damage"] = Math.Max(minHpAfterDamage, 0),
            ["shield_absorption_percent"] = DictFloat(@params, "shield_absorption_percent", 50.0),
        };
        if (HasKey(@params, "damage_tag"))
        {
            outcome["damage_tag"] = ProgressionDataUtils.to_string_name(
                @params.GetValueOrDefault("damage_tag", "")
            );
        }
        else if (effectDef != null && effectDef.damage_tag != "")
        {
            outcome["damage_tag"] = effectDef.damage_tag;
        }
        return outcome;
    }

    private int ResolveHealAmount(BattleUnitState sourceUnit, CombatEffectDef effectDef)
    {
        if (effectDef?.@params != null && HasKey(effectDef.@params, "base_sides"))
        {
            int conMod = GetUnitBaseAttributeModifier(
                sourceUnit,
                UnitBaseAttributes.CONSTITUTION()
            );
            int willMod = GetUnitBaseAttributeModifier(sourceUnit, UnitBaseAttributes.WILLPOWER());
            int diceCount = Math.Max(effectDef.power, 1);
            int baseSides = DictInt(effectDef.@params, "base_sides", 4);
            int conModSides = DictInt(effectDef.@params, "con_mod_sides", 2);
            int willModSides = DictInt(effectDef.@params, "will_mod_sides", 1);
            // 用 long 累加再夹取，避免 con/will 修正堆叠到极端值时 int 溢出回绕成负数。
            long diceSidesRaw =
                (long)baseSides + (long)conMod * conModSides + (long)willMod * willModSides;
            int diceSides = (int)Math.Clamp(diceSidesRaw, 4L, int.MaxValue);
            GDictionary diceRoll = RollDicePool(diceCount, diceSides, 0, "heal");
            return Math.Max(DictInt(diceRoll, "heal_total"), 1);
        }
        int healAmount = Math.Max(effectDef?.power ?? 0, 0);
        GDictionary healDiceRoll = RollDamageDice(effectDef);
        if (healDiceRoll.Count > 0)
        {
            healAmount += DictInt(healDiceRoll, "damage_dice_total");
        }
        return Math.Max(healAmount, 1);
    }

    private static void ApplyHealing(BattleUnitState targetUnit, int healAmount)
    {
        if (targetUnit == null || healAmount <= 0)
        {
            return;
        }
        int maxHp = Math.Max(GetAttributeValue(targetUnit, AttributeService.HP_MAX_ID()), 0);
        targetUnit.current_hp = Math.Min(targetUnit.current_hp + healAmount, maxHp);
    }

    private void ApplyStaminaRestore(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDef effectDef
    )
    {
        if (targetUnit == null || effectDef?.@params == null)
        {
            return;
        }
        int conMod = GetUnitBaseAttributeModifier(sourceUnit, UnitBaseAttributes.CONSTITUTION());
        int willMod = GetUnitBaseAttributeModifier(sourceUnit, UnitBaseAttributes.WILLPOWER());
        int diceCount = Math.Max(effectDef.power, 1);
        int baseSides = DictInt(effectDef.@params, "base_sides", 4);
        int conModSides = DictInt(effectDef.@params, "con_mod_sides", 2);
        int willModSides = DictInt(effectDef.@params, "will_mod_sides", 1);
        int diceSides = Math.Max(baseSides + conMod * conModSides + willMod * willModSides, 4);
        GDictionary diceRoll = RollDicePool(diceCount, diceSides, 0, "stamina_restore");
        int staminaAmount = Math.Max(DictInt(diceRoll, "stamina_restore_total"), 1);
        int maxStamina = Math.Max(GetAttributeValue(targetUnit, AttributeService.STAMINA_MAX_ID()), 0);
        targetUnit.current_stamina = Math.Min(targetUnit.current_stamina + staminaAmount, maxStamina);
    }

    private int ResolveHealFatalAmount(BattleUnitState targetUnit, CombatEffectDef effectDef)
    {
        if (effectDef == null || targetUnit == null)
        {
            return 0;
        }
        GDictionary @params = effectDef.@params ?? new GDictionary();
        int baseHeal = DictInt(@params, "base_heal", 8);
        int healPerLevel = DictInt(@params, "heal_per_level", 4);
        int conModBase = DictInt(@params, "con_mod_base", 2);
        int conModPer2Levels = DictInt(@params, "con_mod_per_2_levels", 1);
        int skillLevel = Math.Max(DictInt(@params, "skill_level", 1), 1);
        int conMod = GetUnitBaseAttributeModifier(targetUnit, UnitBaseAttributes.CONSTITUTION());
        int healAmount = baseHeal + healPerLevel * (skillLevel - 1);
        int conLevelBonus = conModBase + ((skillLevel - 1) / 2) * conModPer2Levels;
        healAmount += conMod * conLevelBonus;
        return Math.Max(healAmount, 1);
    }

    private bool ApplyStatusEffect(
        BattleUnitState targetUnit,
        BattleUnitState sourceUnit,
        CombatEffectDef effectDef,
        StringName statusIdOverride = default
    )
    {
        if (targetUnit == null || effectDef == null)
        {
            return false;
        }
        StringName resolvedStatusId = !IsEmpty(statusIdOverride)
            ? statusIdOverride
            : ProgressionDataUtils.to_string_name(effectDef.status_id);
        if (resolvedStatusId == "")
        {
            return false;
        }
        if (IsCrownBreakSealStatus(resolvedStatusId))
        {
            ClearOtherCrownBreakSeals(targetUnit, resolvedStatusId);
        }
        CombatEffectDef runtimeEffectDef = effectDef.duplicate_for_runtime();
        if (runtimeEffectDef == null)
        {
            return false;
        }
        runtimeEffectDef.status_id = resolvedStatusId;
        BattleStatusEffectState statusEntry = BattleStatusSemanticTable.merge_status(
            runtimeEffectDef,
            sourceUnit != null ? sourceUnit.unit_id : new StringName(""),
            targetUnit.get_status_effect(resolvedStatusId)
        );
        if (statusEntry == null)
        {
            return false;
        }
        targetUnit.set_status_effect(statusEntry);
        return true;
    }

    private static bool IsCrownBreakSealStatus(StringName statusId)
    {
        return statusId == StatusCrownBreakBrokenFang
            || statusId == StatusCrownBreakBrokenHand
            || statusId == StatusCrownBreakBlindedEye;
    }

    private static void ClearOtherCrownBreakSeals(
        BattleUnitState targetUnit,
        StringName keptStatusId
    )
    {
        if (targetUnit == null)
        {
            return;
        }
        foreach (
            StringName sealStatusId in new[]
            {
                StatusCrownBreakBrokenFang,
                StatusCrownBreakBrokenHand,
                StatusCrownBreakBlindedEye,
            }
        )
        {
            if (sealStatusId != keptStatusId)
            {
                targetUnit.erase_status_effect(sealStatusId);
            }
        }
    }

    private static bool HasStatusEffect(BattleUnitState unitState, StringName statusId)
    {
        return unitState != null && unitState.has_status_effect(statusId);
    }

    private static int GetStatusStrength(BattleUnitState unitState, StringName statusId)
    {
        BattleStatusEffectState statusEntry = unitState?.get_status_effect(statusId);
        return statusEntry == null ? 0 : Math.Max(statusEntry.power, 1);
    }

    private double GetTargetIncomingDamageMultiplier(BattleUnitState targetUnit)
    {
        if (targetUnit == null)
        {
            return 1.0;
        }
        double multiplier = 1.0;
        foreach (StringName statusId in SortedStatusIds(targetUnit.status_effects))
        {
            BattleStatusEffectState statusEntry = targetUnit.get_status_effect(statusId);
            if (statusEntry?.@params == null)
            {
                continue;
            }
            double statusMultiplier = GetFloatParam(
                statusEntry.@params,
                "incoming_damage_multiplier",
                1.0
            );
            if (statusMultiplier > multiplier)
            {
                multiplier = statusMultiplier;
            }
        }
        return Math.Max(multiplier, 1.0);
    }

    private double GetSourceOutgoingDamageMultiplier(BattleUnitState sourceUnit)
    {
        if (sourceUnit == null)
        {
            return 1.0;
        }
        double multiplier = 1.0;
        foreach (StringName statusId in SortedStatusIds(sourceUnit.status_effects))
        {
            BattleStatusEffectState statusEntry = sourceUnit.get_status_effect(statusId);
            if (statusEntry?.@params == null)
            {
                continue;
            }
            double statusMultiplier = GetFloatParam(
                statusEntry.@params,
                "outgoing_damage_multiplier",
                1.0
            );
            if (statusMultiplier > 0.0)
            {
                multiplier *= statusMultiplier;
            }
        }
        return Math.Max(multiplier, 0.0);
    }

    private static double GetLowLuckBloodDebtMultiplier(BattleUnitState targetUnit)
    {
        if (!LowLuckRelicRules.unit_has_flag(targetUnit, LowLuckRelicRules.attr_blood_debt_shawl()))
        {
            return 1.0;
        }
        if (!IsUnitBelowHpRatio(targetUnit, LowLuckRelicRules.blood_debt_low_hp_threshold_ratio()))
        {
            return 1.0;
        }
        return LowLuckRelicRules.blood_debt_damage_multiplier();
    }

    private bool ApplyLowLuckBlackStarWedgeExposed(BattleUnitState sourceUnit)
    {
        if (sourceUnit == null)
        {
            return false;
        }
        ApplyRuntimeStatus(
            sourceUnit,
            LowLuckRelicRules.status_black_star_wedge_exposed(),
            LowLuckRelicRules.black_star_wedge_exposed_duration_tu(),
            new GDictionary
            {
                ["incoming_damage_multiplier"] =
                    LowLuckRelicRules.black_star_wedge_exposed_incoming_damage_multiplier(),
                ["counts_as_debuff"] = true,
            }
        );
        return true;
    }

    private static void ApplyRuntimeStatus(
        BattleUnitState unitState,
        StringName statusId,
        int durationTu,
        GDictionary @params = null,
        StringName sourceUnitId = default
    )
    {
        if (unitState == null || statusId == "")
        {
            return;
        }
        var statusEntry = new BattleStatusEffectState
        {
            status_id = statusId,
            source_unit_id = IsEmpty(sourceUnitId) ? new StringName("") : sourceUnitId,
            power = 1,
            stacks = 1,
            duration = Math.Max(durationTu, -1),
            @params = DuplicateDictionary(@params),
        };
        unitState.set_status_effect(statusEntry);
    }

    private static bool IsUnitBelowHpRatio(BattleUnitState unitState, double thresholdRatio)
    {
        if (unitState?.attribute_snapshot == null)
        {
            return false;
        }
        int maxHp = Math.Max(GetAttributeValue(unitState, AttributeService.HP_MAX_ID()), 0);
        return maxHp > 0 && unitState.current_hp <= maxHp * Math.Clamp(thresholdRatio, 0.0, 1.0);
    }

    private void GrantStatusOnHitToSource(
        BattleUnitState sourceUnit,
        CombatEffectDef effectDef,
        GDictionary damageContext = null
    )
    {
        if (sourceUnit == null || effectDef?.@params == null)
        {
            return;
        }
        StringName grantStatusId = DictStringName(effectDef.@params, "grant_status_id");
        if (grantStatusId == "")
        {
            return;
        }
        int grantPower = Math.Max(DictInt(effectDef.@params, "grant_status_power", 1), 1);
        int grantDuration = Math.Max(
            DictInt(effectDef.@params, "grant_status_duration_tu", 180),
            0
        );
        BattleStatusEffectState existingEntry = sourceUnit.get_status_effect(grantStatusId);
        if (existingEntry != null)
        {
            int newStacks = Math.Min(
                existingEntry.stacks + grantPower,
                Math.Max(DictInt(effectDef.@params, "grant_status_stack_limit", 20), 1)
            );
            existingEntry.stacks = newStacks;
            existingEntry.duration = Math.Max(existingEntry.duration, grantDuration);
            existingEntry.power = newStacks;
            sourceUnit.set_status_effect(existingEntry);
            return;
        }
        var statusEntry = new BattleStatusEffectState
        {
            status_id = grantStatusId,
            source_unit_id = sourceUnit.unit_id,
            power = grantPower,
            stacks = grantPower,
            duration = grantDuration,
            @params = new GDictionary
            {
                ["stack_behavior"] = "add",
                ["stack_limit"] = DictInt(effectDef.@params, "grant_status_stack_limit", 20),
            },
        };
        sourceUnit.set_status_effect(statusEntry);
    }

    private GDictionary RollConsumedStackDice(
        BattleUnitState sourceUnit,
        CombatEffectDef effectDef,
        StringName rollMode = default
    )
    {
        if (sourceUnit == null || effectDef == null)
        {
            return new GDictionary();
        }
        StringName consumedId = ProgressionDataUtils.to_string_name(effectDef.consumed_status_id);
        int dicePerStack = Math.Max(effectDef.dice_per_consumed_stack, 0);
        int diceSides = Math.Max(effectDef.dice_sides_per_stack, 0);
        if (
            consumedId == ""
            || dicePerStack <= 0
            || diceSides <= 0
            || !sourceUnit.has_status_effect(consumedId)
        )
        {
            return new GDictionary();
        }
        BattleStatusEffectState statusEntry = sourceUnit.get_status_effect(consumedId);
        int stackCount = Math.Max(statusEntry?.stacks ?? 0, 0);
        if (stackCount <= 0)
        {
            return new GDictionary();
        }
        sourceUnit.erase_status_effect(consumedId);
        return RollDicePool(
            dicePerStack * stackCount,
            diceSides,
            0,
            "consumed_stack_damage_dice",
            IsEmpty(rollMode) ? DamagePreviewRollModeRandom : rollMode
        );
    }

    private static void ClearComboStackOnMiss(BattleUnitState sourceUnit)
    {
        if (sourceUnit != null && sourceUnit.has_status_effect("combo_stack"))
        {
            sourceUnit.erase_status_effect("combo_stack");
        }
    }

    private void RecordLastStandMastery(
        BattleUnitState targetUnit,
        BattleUnitState sourceUnit,
        StringName sourceType,
        int baseAmount
    )
    {
        if (_suppress_last_stand_mastery_records || targetUnit == null || baseAmount <= 0)
        {
            return;
        }
        _last_stand_mastery_records.Add(
            new GDictionary
            {
                ["member_id"] = targetUnit.source_member_id,
                ["skill_id"] = "warrior_last_stand",
                ["amount"] = baseAmount,
                ["source_type"] = sourceType,
                ["source_label"] = "不屈",
                ["reason_text"] = sourceType == "last_stand_triggered" ? "触发免死" : "极限承伤",
                ["allow_unlocks"] = true,
            }
        );
    }

    private bool TriggerLastStand(BattleUnitState targetUnit, BattleUnitState sourceUnit = null)
    {
        BattleStatusEffectState deathWardEntry = targetUnit.get_status_effect("death_ward");
        if (deathWardEntry == null)
        {
            return false;
        }
        GDictionary deathWardParams = deathWardEntry.@params ?? new GDictionary();
        StringName sourceSkillId = DictStringName(deathWardParams, "source_skill_id");
        int skillLevel = DictInt(deathWardParams, "skill_level");
        SkillDef skillDef = GetObject(_skill_defs, sourceSkillId) as SkillDef;
        if (skillDef?.combat_profile == null)
        {
            return false;
        }
        StringName fatalStatusId = ProgressionDataUtils.to_string_name(deathWardEntry.status_id);
        foreach (CombatEffectDef effectDef in skillDef.combat_profile.passive_effect_defs)
        {
            if (effectDef == null || effectDef.trigger_condition != "on_fatal_damage")
            {
                continue;
            }
            StringName requiredStatusId = ProgressionDataUtils.to_string_name(
                effectDef.trigger_status_id
            );
            if (requiredStatusId != "" && requiredStatusId != fatalStatusId)
            {
                continue;
            }
            int minLevel = Math.Max(effectDef.min_skill_level, 0);
            int maxLevel = effectDef.max_skill_level;
            if (skillLevel < minLevel || (maxLevel >= 0 && skillLevel > maxLevel))
            {
                continue;
            }
            CombatEffectDef runtimeEffectDef = effectDef.duplicate_for_runtime();
            if (runtimeEffectDef == null)
            {
                continue;
            }
            runtimeEffectDef.@params ??= new GDictionary();
            runtimeEffectDef.@params["skill_level"] = skillLevel;
            resolve_effects(targetUnit, targetUnit, new GArray { runtimeEffectDef });
        }
        bool triggered = targetUnit.current_hp > 0;
        if (triggered)
        {
            RecordLastStandMastery(targetUnit, sourceUnit, "last_stand_triggered", 50);
            targetUnit.erase_status_effect("death_ward");
            targetUnit.death_ward_consumed_this_battle = true;
        }
        return triggered;
    }


    private static GDictionary BuildAppliedDamageResult(
        GDictionary damageOutcome,
        int hpDamage,
        int shieldAbsorbed,
        bool shieldBroken
    )
    {
        GDictionary result = DuplicateDictionary(damageOutcome);
        EnsureDamageDiceEventDefaults(result);
        result["damage"] = hpDamage;
        result["hp_damage"] = hpDamage;
        result["shield_absorbed"] = shieldAbsorbed;
        result["shield_broken"] = shieldBroken;
        result["fully_absorbed_by_shield"] = hpDamage <= 0 && shieldAbsorbed > 0;
        return result;
    }

    private static GDictionary BuildEnvironmentalDamageResult(GDictionary damageResult)
    {
        GDictionary result = BuildEmptyResult();
        result["applied"] =
            DictInt(damageResult, "damage") > 0 || DictInt(damageResult, "shield_absorbed") > 0;
        result["damage"] = DictInt(damageResult, "damage");
        result["hp_damage"] = DictInt(damageResult, "hp_damage", DictInt(result, "damage"));
        result["shield_absorbed"] = DictInt(damageResult, "shield_absorbed");
        result["shield_broken"] = DictBool(damageResult, "shield_broken");
        result["damage_events"] = new GArray { DuplicateDictionary(damageResult) };
        AttachDamageEventAggregates(result);
        return result;
    }

    private static GDictionary BuildEmptyResult()
    {
        return new GDictionary
        {
            ["applied"] = false,
            ["damage"] = 0,
            ["hp_damage"] = 0,
            ["healing"] = 0,
            ["shield_absorbed"] = 0,
            ["shield_broken"] = false,
            ["damage_events"] = new GArray(),
            ["equipment_durability_events"] = new GArray(),
            ["dispel_events"] = new GArray(),
            ["damage_dice_high_total_roll"] = false,
            ["skill_damage_dice_is_max"] = false,
            ["weapon_damage_dice_is_max"] = false,
            ["status_effect_ids"] = new GStringNameArray(),
            ["removed_status_effect_ids"] = new GStringNameArray(),
            ["source_status_effect_ids"] = new GStringNameArray(),
            ["terrain_effect_ids"] = new GStringNameArray(),
            ["height_delta"] = 0,
            ["diagnostics"] = new GArray(),
        };
    }

    private AttackResolutionMetadata ResolveAttackMetadata(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        AttackCheckInput attackCheck,
        AttackContext attackContext
    )
    {
        _hit_resolver ??= new BattleHitResolver();
        return _hit_resolver.resolve_attack_metadata(
            sourceUnit,
            targetUnit,
            attackCheck,
            attackContext
        );
    }

    private GDictionary ResolveSpellControlMetadata(
        BattleUnitState sourceUnit,
        GDictionary attackContext
    )
    {
        _hit_resolver ??= new BattleHitResolver();
        return _hit_resolver.resolve_spell_control_metadata(sourceUnit, attackContext);
    }

    private GDictionary BuildAttackMetadataResult(
        GDictionary result,
        AttackResolutionMetadata attackMetadata
    )
    {
        GDictionary merged = DuplicateDictionary(result);
        attackMetadata ??= new AttackResolutionMetadata();
        merged["attack_resolution"] = attackMetadata.AttackResolution;
        merged["attack_success"] = attackMetadata.AttackSuccess;
        merged["critical_hit"] = attackMetadata.CriticalHit;
        merged["critical_fail"] = attackMetadata.CriticalFail;
        merged["ordinary_miss"] = attackMetadata.OrdinaryMiss;
        merged["critical_source"] = ResolveCriticalSource(attackMetadata);
        merged["is_disadvantage"] = attackMetadata.IsDisadvantage;
        merged["hidden_luck_at_birth"] = attackMetadata.HiddenLuckAtBirth;
        merged["faith_luck_bonus"] = attackMetadata.FaithLuckBonus;
        merged["effective_luck"] = attackMetadata.EffectiveLuck;
        merged["crit_locked"] = attackMetadata.CritLocked;
        merged["crit_gate_die"] = attackMetadata.CritGateDie;
        merged["crit_gate_roll"] = attackMetadata.CritGateRoll;
        merged["hit_roll"] = attackMetadata.HitRoll;
        merged["fumble_low_end"] = attackMetadata.FumbleLowEnd;
        merged["crit_threshold"] = attackMetadata.CritThreshold;
        merged["required_roll"] = attackMetadata.RequiredRoll;
        merged["display_required_roll"] = attackMetadata.DisplayRequiredRoll;
        merged["hit_rate_percent"] = attackMetadata.HitRatePercent;
        merged["success_rate_percent"] = attackMetadata.SuccessRatePercent;
        merged["reverse_fate_downgraded"] = attackMetadata.ReverseFateDowngraded;
        merged["secondary_hit_success"] = attackMetadata.SecondaryHitSuccess;
        merged["skill_id"] = attackMetadata.SkillId;
        merged["trait_trigger_results"] = BuildTraitTriggerResultsArray(attackMetadata);
        merged["fate_event_tags"] = ProgressionDataUtils.string_name_array_to_string_array(
            BuildAttackEventTags(attackMetadata)
        );
        return merged;
    }

    private GDictionary BuildAttackEffectContext(AttackResolutionMetadata attackMetadata)
    {
        attackMetadata ??= new AttackResolutionMetadata();
        return new GDictionary
        {
            ["attack_resolution"] = attackMetadata.AttackResolution,
            ["attack_success"] = attackMetadata.AttackSuccess,
            ["critical_hit"] = attackMetadata.CriticalHit,
            ["critical_fail"] = attackMetadata.CriticalFail,
            ["ordinary_miss"] = attackMetadata.OrdinaryMiss,
            ["critical_source"] = ResolveCriticalSource(attackMetadata),
            ["is_disadvantage"] = attackMetadata.IsDisadvantage,
            ["hidden_luck_at_birth"] = attackMetadata.HiddenLuckAtBirth,
            ["faith_luck_bonus"] = attackMetadata.FaithLuckBonus,
            ["effective_luck"] = attackMetadata.EffectiveLuck,
            ["crit_locked"] = attackMetadata.CritLocked,
            ["crit_gate_die"] = attackMetadata.CritGateDie,
            ["crit_gate_roll"] = attackMetadata.CritGateRoll,
            ["hit_roll"] = attackMetadata.HitRoll,
            ["fumble_low_end"] = attackMetadata.FumbleLowEnd,
            ["crit_threshold"] = attackMetadata.CritThreshold,
            ["required_roll"] = attackMetadata.RequiredRoll,
            ["display_required_roll"] = attackMetadata.DisplayRequiredRoll,
            ["hit_rate_percent"] = attackMetadata.HitRatePercent,
            ["success_rate_percent"] = attackMetadata.SuccessRatePercent,
            ["reverse_fate_downgraded"] = attackMetadata.ReverseFateDowngraded,
            ["secondary_hit_success"] = attackMetadata.SecondaryHitSuccess,
            ["skill_id"] = attackMetadata.SkillId,
            ["trait_trigger_results"] = BuildTraitTriggerResultsArray(attackMetadata),
        };
    }

    private static GArray BuildTraitTriggerResultsArray(AttackResolutionMetadata attackMetadata)
    {
        var results = new GArray();
        if (attackMetadata?.TraitTriggerResults == null)
        {
            return results;
        }
        foreach (AttackTraitTriggerResult triggerResult in attackMetadata.TraitTriggerResults)
        {
            if (!triggerResult.Triggered)
            {
                continue;
            }
            results.Add(
                new GDictionary
                {
                    ["triggered"] = triggerResult.Triggered,
                    ["event"] = triggerResult.Event,
                    ["trait_id"] = triggerResult.TraitId,
                    ["effect_type"] = triggerResult.EffectType,
                    ["original_roll"] = triggerResult.OriginalRoll,
                    ["reroll_die"] = triggerResult.RerollDie,
                    ["rerolled_roll"] = triggerResult.RerolledRoll,
                    ["die_size"] = triggerResult.DieSize,
                    ["charge_key"] = triggerResult.ChargeKey,
                    ["charges_remaining"] = triggerResult.ChargesRemaining,
                }
            );
        }
        return results;
    }

    private void AttachAttackReportEntry(
        GDictionary result,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit
    )
    {
        if (result == null || result.Count == 0)
        {
            return;
        }
        GDictionary reportEntry = _report_formatter.build_attack_report_entry(
            sourceUnit,
            targetUnit,
            result
        );
        if (reportEntry.Count > 0)
        {
            result["report_entry"] = DuplicateDictionary(reportEntry);
        }
    }

    private void DispatchAttackResolutionEvents(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        AttackResolutionMetadata attackMetadata,
        AttackContext attackContext
    )
    {
        if (attackMetadata == null)
        {
            return;
        }
        GDictionary payload = BuildAttackEventPayload(
            sourceUnit,
            targetUnit,
            attackMetadata,
            attackContext
        );
        foreach (StringName eventType in BuildAttackEventTags(attackMetadata))
        {
            _fate_event_bus.dispatch(eventType, payload);
        }
    }

    private void DispatchSpellControlResolutionEvents(
        BattleUnitState sourceUnit,
        GDictionary controlMetadata,
        GDictionary attackContext
    )
    {
        if (controlMetadata == null || controlMetadata.Count == 0)
        {
            return;
        }
        GDictionary payload = BuildSpellControlEventPayload(
            sourceUnit,
            controlMetadata,
            attackContext
        );
        foreach (StringName eventType in BuildSpellControlEventTags(controlMetadata))
        {
            _fate_event_bus.dispatch(eventType, payload);
        }
    }

    private GDictionary BuildAttackEventPayload(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        AttackResolutionMetadata attackMetadata,
        AttackContext attackContext
    )
    {
        BattleState battleState = attackContext?.BattleState;
        attackMetadata ??= new AttackResolutionMetadata();
        return new GDictionary
        {
            ["battle_id"] = battleState != null ? battleState.battle_id : new StringName(""),
            ["attacker_id"] = sourceUnit != null ? sourceUnit.unit_id : new StringName(""),
            ["attacker_member_id"] =
                sourceUnit != null ? sourceUnit.source_member_id : new StringName(""),
            ["attacker_low_hp_hardship"] = IsLowHpHardship(sourceUnit),
            ["attacker_strong_attack_debuff_ids"] = GetStrongAttackDebuffIds(sourceUnit),
            ["defender_id"] = targetUnit != null ? targetUnit.unit_id : new StringName(""),
            ["defender_member_id"] =
                targetUnit != null ? targetUnit.source_member_id : new StringName(""),
            ["defender_is_elite_or_boss"] = IsEliteOrBoss(targetUnit),
            ["attack_resolution"] = attackMetadata.AttackResolution,
            ["critical_source"] = ResolveCriticalSource(attackMetadata),
            ["is_disadvantage"] = attackMetadata.IsDisadvantage,
            ["crit_gate_die"] = attackMetadata.CritGateDie,
            ["crit_gate_roll"] = attackMetadata.CritGateRoll,
            ["hit_roll"] = attackMetadata.HitRoll,
            ["luck_snapshot"] = BuildAttackLuckSnapshot(attackMetadata),
        };
    }

    private GDictionary BuildSpellControlEventPayload(
        BattleUnitState sourceUnit,
        GDictionary controlMetadata,
        GDictionary attackContext
    )
    {
        BattleState battleState = GetObject(attackContext, "battle_state") as BattleState;
        return new GDictionary
        {
            ["battle_id"] = battleState != null ? battleState.battle_id : new StringName(""),
            ["attacker_id"] = sourceUnit != null ? sourceUnit.unit_id : new StringName(""),
            ["attacker_member_id"] =
                sourceUnit != null ? sourceUnit.source_member_id : new StringName(""),
            ["attacker_low_hp_hardship"] = IsLowHpHardship(sourceUnit),
            ["attacker_strong_attack_debuff_ids"] = GetStrongAttackDebuffIds(sourceUnit),
            ["defender_id"] = new StringName(""),
            ["defender_member_id"] = new StringName(""),
            ["defender_is_elite_or_boss"] = false,
            ["attack_resolution"] = DictStringName(controlMetadata, "attack_resolution"),
            ["spell_control_resolution"] = DictStringName(
                controlMetadata,
                "spell_control_resolution"
            ),
            ["critical_source"] = ResolveCriticalSource(controlMetadata),
            ["is_disadvantage"] = DictBool(controlMetadata, "is_disadvantage"),
            ["crit_gate_die"] = DictInt(controlMetadata, "crit_gate_die"),
            ["crit_gate_roll"] = DictInt(controlMetadata, "crit_gate_roll"),
            ["hit_roll"] = DictInt(controlMetadata, "hit_roll"),
            ["luck_snapshot"] = BuildAttackLuckSnapshot(controlMetadata),
            ["event_family"] = "spell_control",
            ["skill_id"] = DictStringName(attackContext, "skill_id"),
        };
    }

    private static GDictionary BuildAttackLuckSnapshot(GDictionary attackMetadata)
    {
        return new GDictionary
        {
            ["hidden_luck_at_birth"] = DictInt(attackMetadata, "hidden_luck_at_birth"),
            ["faith_luck_bonus"] = DictInt(attackMetadata, "faith_luck_bonus"),
            ["effective_luck"] = DictInt(attackMetadata, "effective_luck"),
            ["fumble_low_end"] = DictInt(attackMetadata, "fumble_low_end"),
            ["crit_threshold"] = DictInt(attackMetadata, "crit_threshold"),
        };
    }

    private static GDictionary BuildAttackLuckSnapshot(AttackResolutionMetadata attackMetadata)
    {
        attackMetadata ??= new AttackResolutionMetadata();
        return new GDictionary
        {
            ["hidden_luck_at_birth"] = attackMetadata.HiddenLuckAtBirth,
            ["faith_luck_bonus"] = attackMetadata.FaithLuckBonus,
            ["effective_luck"] = attackMetadata.EffectiveLuck,
            ["fumble_low_end"] = attackMetadata.FumbleLowEnd,
            ["crit_threshold"] = attackMetadata.CritThreshold,
        };
    }

    private static StringName ResolveCriticalSource(GDictionary attackMetadata)
    {
        return !DictBool(attackMetadata, "critical_hit") ? new StringName("")
            : IsHighThreatCriticalHit(attackMetadata) ? new StringName("high_threat")
            : new StringName("gate_die");
    }

    private static StringName ResolveCriticalSource(AttackResolutionMetadata attackMetadata)
    {
        return attackMetadata == null || !attackMetadata.CriticalHit ? new StringName("")
            : IsHighThreatCriticalHit(attackMetadata) ? new StringName("high_threat")
            : new StringName("gate_die");
    }

    private static bool IsHighThreatCriticalHit(GDictionary attackMetadata)
    {
        return DictBool(attackMetadata, "critical_hit")
            && DictInt(attackMetadata, "crit_gate_die") == NaturalHitRoll;
    }

    private static bool IsHighThreatCriticalHit(AttackResolutionMetadata attackMetadata)
    {
        return attackMetadata != null
            && attackMetadata.CriticalHit
            && attackMetadata.CritGateDie == NaturalHitRoll;
    }

    private static bool IsLowHpHardship(BattleUnitState unitState)
    {
        int maxHp = GetAttributeValue(unitState, AttributeService.HP_MAX_ID());
        return unitState != null
            && maxHp > 0
            && unitState.current_hp * 100
                <= maxHp * BattleState.LOW_HP_ATTACK_DISADVANTAGE_PERCENT();
    }

    private static GStringNameArray GetStrongAttackDebuffIds(BattleUnitState unitState)
    {
        var strongStatusIds = new GStringNameArray();
        if (unitState == null)
        {
            return strongStatusIds;
        }
        foreach (var statusKey in BattleState.STRONG_ATTACK_DISADVANTAGE_STATUS_IDS().Keys)
        {
            StringName statusId = new(statusKey.ToString());
            if (statusId != "" && unitState.has_status_effect(statusId))
            {
                strongStatusIds.Add(statusId);
            }
        }
        return strongStatusIds;
    }

    private static bool IsEliteOrBoss(BattleUnitState unitState)
    {
        return GetAttributeValue(unitState, FortuneMarkTargetStatId) > 0;
    }

    private static GStringNameArray BuildAttackEventTags(GDictionary attackMetadata)
    {
        var tags = new GStringNameArray();
        if (DictBool(attackMetadata, "critical_fail"))
            tags.Add("critical_fail");
        if (IsHighThreatCriticalHit(attackMetadata))
            tags.Add("high_threat_critical_hit");
        if (DictBool(attackMetadata, "critical_hit") && DictBool(attackMetadata, "is_disadvantage"))
            tags.Add("critical_success_under_disadvantage");
        if (DictBool(attackMetadata, "ordinary_miss"))
            tags.Add("ordinary_miss");
        if (
            DictBool(attackMetadata, "attack_success")
            && DictBool(attackMetadata, "is_disadvantage")
            && !DictBool(attackMetadata, "critical_hit")
        )
            tags.Add("hardship_survival");
        return tags;
    }

    private static GStringNameArray BuildAttackEventTags(AttackResolutionMetadata attackMetadata)
    {
        var tags = new GStringNameArray();
        if (attackMetadata == null)
        {
            return tags;
        }
        if (attackMetadata.CriticalFail)
            tags.Add("critical_fail");
        if (IsHighThreatCriticalHit(attackMetadata))
            tags.Add("high_threat_critical_hit");
        if (attackMetadata.CriticalHit && attackMetadata.IsDisadvantage)
            tags.Add("critical_success_under_disadvantage");
        if (attackMetadata.OrdinaryMiss)
            tags.Add("ordinary_miss");
        if (
            attackMetadata.AttackSuccess
            && attackMetadata.IsDisadvantage
            && !attackMetadata.CriticalHit
        )
            tags.Add("hardship_survival");
        return tags;
    }

    private static GStringNameArray BuildSpellControlEventTags(GDictionary controlMetadata)
    {
        var tags = new GStringNameArray();
        if (DictBool(controlMetadata, "critical_fail"))
            tags.Add("critical_fail");
        if (IsHighThreatCriticalHit(controlMetadata))
            tags.Add("high_threat_critical_hit");
        if (
            DictBool(controlMetadata, "critical_hit")
            && DictBool(controlMetadata, "is_disadvantage")
        )
            tags.Add("critical_success_under_disadvantage");
        return tags;
    }

    private int GetUnitBaseAttributeModifier(BattleUnitState unitState, StringName attributeId)
    {
        if (unitState?.attribute_snapshot == null || attributeId == "")
        {
            return 0;
        }
        StringName modifierId = AttributeSnapshot.get_base_attribute_modifier_id(attributeId);
        return modifierId == "" ? 0 : GetAttributeValue(unitState, modifierId);
    }

    private int GetTargetSecondaryHitSaveBonus(BattleUnitState targetUnit)
    {
        if (targetUnit == null)
        {
            return 0;
        }
        int bonus = 0;
        foreach (StringName statusId in SortedStatusIds(targetUnit.status_effects))
        {
            BattleStatusEffectState statusEntry = targetUnit.get_status_effect(statusId);
            if (statusEntry?.@params == null)
            {
                continue;
            }
            bonus = Math.Max(
                bonus,
                GetIntParam(statusEntry.@params, StatusParamControlSaveBonus, 0)
            );
            bonus = Math.Max(
                bonus,
                GetIntParam(statusEntry.@params, StatusParamSecondaryHitSaveBonus, 0)
            );
        }
        return bonus;
    }

    private static int GetAttributeValue(BattleUnitState unitState, StringName attributeId)
    {
        return unitState?.attribute_snapshot != null
            ? unitState.attribute_snapshot.get_value(attributeId)
            : 0;
    }

    private static GArray CoerceEffectDefs(GArray effectDefs)
    {
        return effectDefs ?? new GArray();
    }

    private static GArray ToValueArray(Godot.Collections.Array<CombatEffectDef> values)
    {
        var result = new GArray();
        if (values == null)
        {
            return result;
        }
        foreach (CombatEffectDef value in values)
        {
            if (value != null)
            {
                result.Add(value);
            }
        }
        return result;
    }

    private static GDictionary DuplicateDictionary(GDictionary source, bool deep = true)
    {
        return source != null ? source.Duplicate(deep) : new GDictionary();
    }

    private static bool HasKey(GDictionary source, object key)
    {
        return TryGet(source, key, out _);
    }

    private static int DictInt(GDictionary source, object key, int fallback = 0)
    {
        return GetInt(source, key, fallback);
    }

    private static bool DictBool(GDictionary source, object key, bool fallback = false)
    {
        return GetBool(source, key, fallback);
    }

    private static double DictFloat(GDictionary source, object key, double fallback = 0.0)
    {
        return GetFloat(source, key, fallback);
    }

    private static string DictString(GDictionary source, object key, string fallback = "")
    {
        return GetString(source, key, fallback);
    }

    private static StringName DictStringName(
        GDictionary source,
        object key,
        StringName fallback = default
    )
    {
        return GetStringName(source, key, fallback);
    }

    private static bool TryGetStatusParam(
        GDictionary @params,
        StringName param_key,
        out object value
    )
    {
        if (@params == null || param_key == "")
        {
            value = default;
            return false;
        }
        if (@params.ContainsKey(param_key))
        {
            value = @params[param_key];
            return true;
        }
        string paramName = param_key.ToString();
        if (@params.ContainsKey(paramName))
        {
            value = @params[paramName];
            return true;
        }
        foreach (Variant keyValue in @params.Keys)
        {
            if (ProgressionDataUtils.to_string_name(keyValue) == param_key)
            {
                value = @params[keyValue];
                return true;
            }
        }
        value = default;
        return false;
    }

    private static void AddUnique(GStringNameArray target, StringName value)
    {
        if (value != "" && !target.Contains(value))
        {
            target.Add(value);
        }
    }

    private static GStringNameArray ToStringNameArray(GArray values)
    {
        var result = new GStringNameArray();
        foreach (var value in values)
        {
            StringName normalized = ProgressionDataUtils.to_string_name(value);
            if (normalized != "")
            {
                result.Add(normalized);
            }
        }
        return result;
    }

    private static GStringNameArray SortedStatusIds(GDictionary statusEffects)
    {
        var ids = new List<StringName>();
        if (statusEffects != null)
        {
            foreach (var key in statusEffects.Keys)
            {
                StringName statusId = ProgressionDataUtils.to_string_name(key);
                if (statusId != "")
                {
                    ids.Add(statusId);
                }
            }
        }
        ids.Sort((left, right) => left.ToString().CompareTo(right.ToString()));
        var result = new GStringNameArray();
        foreach (StringName id in ids)
        {
            result.Add(id);
        }
        return result;
    }

    private static int RoundToInt(double value)
    {
        return (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }

    private static void AppendTraitTriggerResult(GDictionary target, GDictionary triggerResult)
    {
        if (target == null || triggerResult == null || !DictBool(triggerResult, "triggered"))
        {
            return;
        }
        GArray results = GetArray(target, "trait_trigger_results");
        results = (GArray)results.Duplicate(true);
        results.Add(DuplicateDictionary(triggerResult));
        target["trait_trigger_results"] = results;
    }
}
