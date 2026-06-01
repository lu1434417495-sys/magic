using System;
using System.Collections.Generic;
using Godot;
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
    private static readonly StringName EffectHeal = BattleTypedNames.EffectHeal;
    private static readonly StringName EffectStaminaRestore = BattleTypedNames.EffectStaminaRestore;
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

    private readonly record struct DamageEffectRuntimeParameters(
        GDictionary RawParams,
        bool UseWeaponPhysicalDamageTag,
        bool AddWeaponDice,
        bool RemoveHarmful,
        bool RemoveHarmfulFromAllies,
        bool RemoveBeneficial,
        bool RemoveBeneficialFromEnemies,
        bool RequireDamageApplied,
        bool StagedExecution
    )
    {
        public static DamageEffectRuntimeParameters FromEffect(CombatEffectDef effectDef)
        {
            GDictionary parameters = effectDef?.@params ?? new GDictionary();
            return new DamageEffectRuntimeParameters(
                parameters,
                effectDef?.use_weapon_physical_damage_tag ?? false,
                effectDef?.add_weapon_dice ?? false,
                effectDef?.remove_harmful ?? false,
                effectDef?.remove_harmful_from_allies ?? true,
                effectDef?.remove_beneficial ?? false,
                effectDef?.remove_beneficial_from_enemies ?? true,
                effectDef?.require_damage_applied ?? false,
                effectDef?.staged_execution ?? false
            );
        }
    }

    private readonly record struct EquipmentDurabilitySaveResolution(
        GDictionary Payload,
        bool HasSave,
        bool Success
    );

    private readonly record struct EquipmentDurabilityDamageEffectResult(
        GDictionary Payload,
        bool HasEvent,
        int DurabilityLoss,
        bool Destroyed,
        EquipmentDurabilitySaveResolution SaveResult
    )
    {
        public static EquipmentDurabilityDamageEffectResult Empty => new(
            new GDictionary(),
            false,
            0,
            false,
            default
        );

        public GDictionary ToDictionary() => Payload?.Duplicate(true) ?? new GDictionary();
    }

    private readonly record struct DamagePreviewSaveEstimate(
        bool HasSave,
        int DamageBeforeSave,
        int DamageAfterSave,
        int DamageAfterSaveEstimate,
        int DamageAfterSaveWorst,
        int DamageOnSaveFailure,
        int DamageOnSaveSuccess,
        bool SavePartialOnSuccess,
        int SaveSuccessProbabilityBasisPoints,
        int SaveSuccessRatePercent,
        int SaveFailureProbabilityBasisPoints,
        int Dc,
        string Ability,
        string SaveTag,
        string AdvantageState,
        int AbilityValue,
        int AbilityModifier,
        int Bonus,
        bool Immune,
        IReadOnlyList<BattleSaveSource> Sources
    )
    {
        public static DamagePreviewSaveEstimate None(int damageBeforeSave)
        {
            return new DamagePreviewSaveEstimate(
                false,
                damageBeforeSave,
                damageBeforeSave,
                damageBeforeSave,
                damageBeforeSave,
                damageBeforeSave,
                damageBeforeSave,
                false,
                0,
                0,
                10000,
                0,
                "",
                "",
                "",
                0,
                0,
                0,
                false,
                Array.Empty<BattleSaveSource>()
            );
        }

        public GDictionary ToDictionary()
        {
            if (!HasSave)
            {
                return new GDictionary
                {
                    ["has_save"] = false,
                    ["damage_before_save"] = DamageBeforeSave,
                    ["damage_after_save"] = DamageAfterSave,
                    ["damage_after_save_estimate"] = DamageAfterSaveEstimate,
                    ["damage_after_save_worst"] = DamageAfterSaveWorst,
                };
            }
            return new GDictionary
            {
                ["has_save"] = true,
                ["damage_before_save"] = DamageBeforeSave,
                ["damage_after_save"] = DamageAfterSave,
                ["damage_after_save_estimate"] = DamageAfterSaveEstimate,
                ["damage_after_save_worst"] = DamageAfterSaveWorst,
                ["damage_on_save_failure"] = DamageOnSaveFailure,
                ["damage_on_save_success"] = DamageOnSaveSuccess,
                ["save_partial_on_success"] = SavePartialOnSuccess,
                ["save_success_probability_basis_points"] = SaveSuccessProbabilityBasisPoints,
                ["save_success_rate_percent"] = SaveSuccessRatePercent,
                ["save_failure_probability_basis_points"] = SaveFailureProbabilityBasisPoints,
                ["dc"] = Dc,
                ["ability"] = Ability ?? "",
                ["save_tag"] = SaveTag ?? "",
                ["advantage_state"] = AdvantageState ?? "",
                ["ability_value"] = AbilityValue,
                ["ability_modifier"] = AbilityModifier,
                ["bonus"] = Bonus,
                ["immune"] = Immune,
                ["sources"] = BuildSaveSourceArray(Sources),
            };
        }

        public BattleDamagePreviewSaveEstimate ToPreviewSaveEstimate()
        {
            return BattleDamagePreviewSaveEstimate.Create(
                HasSave,
                DamageBeforeSave,
                DamageAfterSave,
                DamageAfterSaveEstimate,
                DamageAfterSaveWorst,
                DamageOnSaveFailure,
                DamageOnSaveSuccess,
                SavePartialOnSuccess,
                SaveSuccessProbabilityBasisPoints,
                SaveSuccessRatePercent,
                SaveFailureProbabilityBasisPoints,
                Dc,
                Ability,
                SaveTag,
                AdvantageState,
                AbilityValue,
                AbilityModifier,
                Bonus,
                Immune,
                Sources
            );
        }
    }

    private readonly record struct DamagePreviewBranchLethalEstimate(
        bool FailureKills,
        bool SuccessKills,
        int FailureHpDamage,
        int SuccessHpDamage,
        bool StableLethal,
        int LethalProbabilityBasisPoints
    );

    private readonly record struct AppliedDamageResult(
        GDictionary Payload,
        int Damage,
        int HpDamage,
        int ShieldAbsorbed,
        bool ShieldBroken,
        bool LowLuckBlackStarWedgeTriggered,
        DamageDiceEventSnapshot DamageDiceEvent
    )
    {
        public bool HasAppliedDamage => Damage > 0 || ShieldAbsorbed > 0;

        public GDictionary ToDictionary() => Payload?.Duplicate(true) ?? new GDictionary();

        public AppliedDamageResult WithHpDamage(int hpDamage)
        {
            int normalizedHpDamage = Math.Max(hpDamage, 0);
            GDictionary payload = ToDictionary();
            payload["damage"] = normalizedHpDamage;
            payload["hp_damage"] = normalizedHpDamage;
            payload["fully_absorbed_by_shield"] =
                normalizedHpDamage <= 0 && ShieldAbsorbed > 0;
            return new AppliedDamageResult(
                payload,
                normalizedHpDamage,
                normalizedHpDamage,
                ShieldAbsorbed,
                ShieldBroken,
                LowLuckBlackStarWedgeTriggered,
                DamageDiceEvent
            );
        }
    }

    private readonly record struct DamageOutcomeResult(
        GDictionary Payload,
        bool InvalidDamageTag,
        string ErrorCode,
        string Reason,
        string DamageTagSource,
        StringName DamageTag,
        int ResolvedDamage,
        bool BypassShield,
        bool BypassDeathPrevention,
        double ShieldAbsorptionPercent,
        int MinHpAfterDamage,
        bool LowLuckBlackStarWedgeTriggered,
        DamageDiceEventSnapshot DamageDiceEvent
    )
    {
        public GDictionary ToDictionary() => Payload?.Duplicate(true) ?? new GDictionary();

        public DamageOutcomeResult WithResolvedDamage(int resolvedDamage)
        {
            int normalizedDamage = Math.Max(resolvedDamage, 0);
            GDictionary payload = ToDictionary();
            payload["resolved_damage"] = normalizedDamage;
            return this with { Payload = payload, ResolvedDamage = normalizedDamage };
        }

        public DamageApplicationInput ToDamageApplicationInput()
        {
            return new DamageApplicationInput(
                Payload ?? new GDictionary(),
                Math.Max(ResolvedDamage, 0),
                BypassShield,
                BypassDeathPrevention,
                ShieldAbsorptionPercent,
                MinHpAfterDamage,
                LowLuckBlackStarWedgeTriggered,
                DamageDiceEvent
            );
        }
    }

    private readonly record struct DamageApplicationInput(
        GDictionary Payload,
        int ResolvedDamage,
        bool BypassShield,
        bool BypassDeathPrevention,
        double ShieldAbsorptionPercent,
        int MinHpAfterDamage,
        bool LowLuckBlackStarWedgeTriggered,
        DamageDiceEventSnapshot DamageDiceEvent
    )
    {
        public static DamageApplicationInput Empty => new(
            new GDictionary(),
            0,
            false,
            false,
            100.0,
            0,
            false,
            DamageDiceEventSnapshot.Empty
        );

        public static DamageApplicationInput Create(
            GDictionary payload,
            int resolvedDamage,
            bool bypassShield = false,
            bool bypassDeathPrevention = false,
            double shieldAbsorptionPercent = 100.0,
            int minHpAfterDamage = 0,
            bool lowLuckBlackStarWedgeTriggered = false,
            DamageDiceEventSnapshot damageDiceEvent = default
        )
        {
            return new DamageApplicationInput(
                payload ?? new GDictionary(),
                Math.Max(resolvedDamage, 0),
                bypassShield,
                bypassDeathPrevention,
                shieldAbsorptionPercent,
                Math.Max(minHpAfterDamage, 0),
                lowLuckBlackStarWedgeTriggered,
                damageDiceEvent
            );
        }

        public static DamageApplicationInput FromDictionary(GDictionary payload)
        {
            GDictionary normalized = payload ?? new GDictionary();
            return new DamageApplicationInput(
                normalized,
                Math.Max(GetInt(normalized, "resolved_damage"), 0),
                ReadBool(normalized, "bypass_shield"),
                ReadBool(normalized, "bypass_death_prevention"),
                GetFloat(normalized, "shield_absorption_percent", 100.0),
                Math.Max(GetInt(normalized, "min_hp_after_damage"), 0),
                ReadBool(normalized, "low_luck_black_star_wedge_triggered"),
                DamageDiceEventSnapshot.FromDictionary(normalized)
            );
        }

        public static bool ReadBool(GDictionary payload, string key)
        {
            return TryGet(payload, key, out Variant value)
                && value.VariantType == Variant.Type.Bool
                && value.AsBool();
        }
    }

    private readonly record struct DamageResolutionContext(
        GDictionary Payload,
        StringName DamageRollMode,
        bool CriticalHit,
        bool AttackSuccess,
        bool SecondaryHitSuccess
    )
    {
        public static DamageResolutionContext FromDictionary(GDictionary payload)
        {
            GDictionary normalized = payload ?? new GDictionary();
            return new DamageResolutionContext(
                normalized,
                DictStringName(normalized, "damage_roll_mode", DamagePreviewRollModeRandom),
                DamageApplicationInput.ReadBool(normalized, "critical_hit"),
                DamageApplicationInput.ReadBool(normalized, "attack_success"),
                DamageApplicationInput.ReadBool(normalized, "secondary_hit_success")
            );
        }
    }

    private readonly record struct SpellControlCheckContext(
        BattleState BattleState,
        StringName SkillId,
        bool DispatchEvents,
        bool HasIsDisadvantage,
        bool IsDisadvantage
    )
    {
        public static SpellControlCheckContext ForSkill(BattleState battleState, StringName skillId)
        {
            return new SpellControlCheckContext(
                battleState,
                skillId,
                true,
                false,
                false
            );
        }

        public static SpellControlCheckContext FromDictionary(GDictionary payload)
        {
            GDictionary normalized = payload ?? new GDictionary();
            bool hasDisadvantage = false;
            bool isDisadvantage = false;
            if (TryGetStatusParam(normalized, "is_disadvantage", out object disadvantageValue))
            {
                hasDisadvantage = true;
                isDisadvantage = ToBool(disadvantageValue, false);
            }
            return new SpellControlCheckContext(
                DictObject<BattleState>(normalized, "battle_state"),
                DictStringNameLocal(normalized, "skill_id"),
                ReadDispatchEvents(normalized),
                hasDisadvantage,
                isDisadvantage
            );
        }

        public AttackContext ToAttackContext()
        {
            var result = new AttackContext
            {
                BattleState = BattleState,
                SkillId = SkillId,
            };
            if (HasIsDisadvantage)
            {
                result.HasIsDisadvantage = true;
                result.IsDisadvantage = IsDisadvantage;
            }
            return result;
        }

        private static bool ReadDispatchEvents(GDictionary payload)
        {
            if (!TryGet(payload, "dispatch_events", out Variant value))
            {
                return true;
            }
            return value.VariantType == Variant.Type.Bool ? value.AsBool() : true;
        }
    }

    private readonly record struct ExecuteEffectResult(
        GDictionary Payload,
        bool Applied,
        int ExecuteStage,
        StringName ExecuteOutcome,
        IReadOnlyList<AppliedDamageResult> DamageResults
    )
    {
        public static ExecuteEffectResult Empty => new(
            new GDictionary(),
            false,
            -1,
            "",
            Array.Empty<AppliedDamageResult>()
        );

        public GDictionary ToDictionary() => Payload?.Duplicate(true) ?? new GDictionary();
    }

    private readonly record struct TraitTriggerResultSnapshot(
        GDictionary Payload,
        bool Triggered,
        int ExtraWeaponDiceCount,
        int ExtraWeaponDiceSides,
        int ClampToHp
    )
    {
        public static TraitTriggerResultSnapshot FromDictionary(GDictionary payload)
        {
            GDictionary normalized = payload ?? new GDictionary();
            return new TraitTriggerResultSnapshot(
                normalized,
                DamageApplicationInput.ReadBool(normalized, "triggered"),
                Math.Max(GetInt(normalized, "extra_weapon_dice_count"), 0),
                Math.Max(GetInt(normalized, "extra_weapon_dice_sides"), 0),
                Math.Max(GetInt(normalized, "clamp_to_hp"), 0)
            );
        }

        public static TraitTriggerResultSnapshot FromAttackTraitTriggerResult(
            AttackTraitTriggerResult result
        )
        {
            if (!result.Triggered)
            {
                return new TraitTriggerResultSnapshot(new GDictionary(), false, 0, 0, 0);
            }
            GDictionary payload = new()
            {
                ["triggered"] = true,
                ["event"] = result.Event,
                ["trait_id"] = result.TraitId,
                ["effect_type"] = result.EffectType,
                ["extra_weapon_dice_count"] = Math.Max(result.ExtraWeaponDiceCount, 0),
                ["extra_weapon_dice_sides"] = Math.Max(result.ExtraWeaponDiceSides, 0),
                ["clamp_to_hp"] = Math.Max(result.ClampToHp, 0),
                ["projected_hp"] = result.ProjectedHp,
                ["hp_damage"] = Math.Max(result.HpDamage, 0),
                ["charge_key"] = result.ChargeKey,
                ["charges_remaining"] = Math.Max(result.ChargesRemaining, 0),
            };
            return new TraitTriggerResultSnapshot(
                payload,
                true,
                Math.Max(result.ExtraWeaponDiceCount, 0),
                Math.Max(result.ExtraWeaponDiceSides, 0),
                Math.Max(result.ClampToHp, 0)
            );
        }

        public GDictionary ToDictionary() => Payload?.Duplicate(true) ?? new GDictionary();
    }

    private readonly record struct DicePoolRollResult(
        GDictionary Payload,
        int Count,
        int Sides,
        GArray Rolls,
        int Total,
        int Bonus,
        int MaxTotal,
        bool IsMax
    )
    {
        public static DicePoolRollResult Empty => new(
            new GDictionary(),
            0,
            0,
            new GArray(),
            0,
            0,
            0,
            false
        );

        public bool HasDice => Count > 0 && Sides > 0;

        public int TotalWithBonus => Total + Bonus;

        public GDictionary ToDictionary() => Payload?.Duplicate(true) ?? new GDictionary();
    }

    private readonly record struct DamageDiceEventSnapshot(
        bool DamageDiceHighTotalRoll,
        bool SkillDamageDiceIsMax,
        bool WeaponDamageDiceIsMax
    )
    {
        public static DamageDiceEventSnapshot Empty => new(false, false, false);

        public static DamageDiceEventSnapshot FromDictionary(GDictionary payload)
        {
            GDictionary normalized = EnsureDamageDiceEventDefaults(payload);
            return new DamageDiceEventSnapshot(
                ReadDamageDiceFlag(normalized, "damage_dice_high_total_roll"),
                ReadDamageDiceFlag(normalized, "skill_damage_dice_is_max"),
                ReadDamageDiceFlag(normalized, "weapon_damage_dice_is_max")
            );
        }

        private static bool ReadDamageDiceFlag(GDictionary payload, string key)
        {
            return TryGet(payload, key, out Variant value)
                && value.VariantType == Variant.Type.Bool
                && value.AsBool();
        }
    }

    private readonly record struct DamageDiceEventFlags(
        GDictionary Payload,
        DamageDiceEventSnapshot Snapshot
    );

    private GDictionary _skill_defs = new();
    private readonly List<BattleSkillMasteryGrant> _last_stand_mastery_records = new();
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
        List<BattleSkillMasteryGrant> typedRecords = GetAndClearLastStandMasteryRecordsTyped();
        GArray records = new();
        foreach (BattleSkillMasteryGrant record in typedRecords)
        {
            if (record != null)
            {
                records.Add(record.ToDictionary());
            }
        }
        return records;
    }

    internal List<BattleSkillMasteryGrant> GetAndClearLastStandMasteryRecordsTyped()
    {
        List<BattleSkillMasteryGrant> records = new(_last_stand_mastery_records);
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
            AttachAttackReportEntry(failedResult, source_unit, target_unit, attackMetadata);
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
        AttachAttackReportEntry(resolvedResult, source_unit, target_unit, attackMetadata);
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
        return resolve_spell_control_check_typed(source_unit, attack_context).ToDictionary();
    }

    public BattleSpellControlMetadata resolve_spell_control_check_typed(
        BattleUnitState source_unit,
        GDictionary attack_context = null
    )
    {
        return ResolveSpellControlCheck(
            source_unit,
            SpellControlCheckContext.FromDictionary(attack_context)
        );
    }

    public BattleSpellControlMetadata resolve_spell_control_check_typed(
        BattleUnitState source_unit,
        BattleState battle_state,
        StringName skill_id
    )
    {
        return ResolveSpellControlCheck(
            source_unit,
            SpellControlCheckContext.ForSkill(battle_state, skill_id)
        );
    }

    private BattleSpellControlMetadata ResolveSpellControlCheck(
        BattleUnitState source_unit,
        SpellControlCheckContext context
    )
    {
        if (source_unit == null)
        {
            return BattleSpellControlMetadata.Empty();
        }
        BattleSpellControlMetadata controlMetadata = ResolveSpellControlMetadata(
            source_unit,
            context
        );
        if (context.DispatchEvents)
        {
            DispatchSpellControlResolutionEvents(source_unit, controlMetadata, context);
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
        return preview_damage_effect_typed(
            source_unit,
            target_unit,
            effect_def,
            damage_context,
            roll_mode,
            save_mode
        ).ToDictionary();
    }

    internal virtual BattleDamagePreviewResult preview_damage_effect_typed(
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
            return BattleDamagePreviewResult.Empty();
        }
        StringName resolvedRollMode = IsEmpty(roll_mode) ? DamagePreviewRollModeAverage : roll_mode;
        StringName resolvedSaveMode = IsEmpty(save_mode)
            ? DamagePreviewSaveModeExpected
            : save_mode;
        BattleUnitState sourcePreview = source_unit.clone();
        BattleUnitState targetPreview = target_unit.clone();
        if (sourcePreview == null || targetPreview == null)
        {
            return BattleDamagePreviewResult.Empty();
        }

        GDictionary previewContext = DuplicateDictionary(damage_context);
        previewContext["damage_roll_mode"] = resolvedRollMode;
        DamageResolutionContext previewContextFlags =
            DamageResolutionContext.FromDictionary(previewContext);
        DamageOutcomeResult damageOutcome = ResolveDamageOutcome(
            sourcePreview,
            targetPreview,
            effect_def,
            previewContextFlags
        );
        if (damageOutcome.InvalidDamageTag)
        {
            return BattleDamagePreviewResult.Create(
                rollMode: resolvedRollMode,
                saveMode: resolvedSaveMode,
                shieldHpBefore: target_unit.current_shield_hp,
                shieldHpAfter: targetPreview.current_shield_hp,
                errorCode: damageOutcome.ErrorCode,
                damageOutcome: damageOutcome.ToDictionary(),
                damageResult: new GDictionary(),
                saveEstimate: BattleDamagePreviewSaveEstimate.None(0),
                diagnostics: new GArray
                {
                    BuildInvalidDamageTagDiagnostic(
                        source_unit,
                        target_unit,
                        effect_def,
                        damageOutcome
                    ),
                },
                sourcePreviewAfter: sourcePreview,
                targetPreviewAfter: targetPreview
            );
        }

        int preSaveDamage = damageOutcome.ResolvedDamage;
        DamagePreviewSaveEstimate saveEstimate = BuildDamagePreviewSaveEstimate(
            sourcePreview,
            targetPreview,
            effect_def,
            previewContextFlags.Payload,
            preSaveDamage,
            resolvedSaveMode
        );
        damageOutcome = WithDamagePreviewSaveEstimate(damageOutcome, saveEstimate);
        AppliedDamageResult damageResult = ApplyDamageToTargetResult(
            targetPreview,
            damageOutcome,
            sourcePreview
        );
        return BattleDamagePreviewResult.Create(
            applied: damageResult.HasAppliedDamage,
            rollMode: resolvedRollMode,
            saveMode: resolvedSaveMode,
            preSaveDamage: preSaveDamage,
            postSaveDamage: saveEstimate.DamageAfterSave,
            hpDamage: damageResult.HpDamage,
            damage: damageResult.Damage,
            incomingBudgetDamage: saveEstimate.DamageAfterSave,
            shieldAbsorbed: damageResult.ShieldAbsorbed,
            shieldBroken: damageResult.ShieldBroken,
            shieldHpBefore: target_unit.current_shield_hp,
            shieldHpAfter: targetPreview.current_shield_hp,
            damageOutcome: damageOutcome.ToDictionary(),
            damageResult: damageResult.ToDictionary(),
            saveEstimate: saveEstimate.ToPreviewSaveEstimate(),
            sourcePreviewAfter: sourcePreview,
            targetPreviewAfter: targetPreview
        );
    }

    public virtual GDictionary preview_damage_sequence(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        GArray effect_defs,
        GDictionary damage_context = null,
        GDictionary options = null
    )
    {
        GDictionary result = preview_damage_sequence_typed(
            source_unit,
            target_unit,
            effect_defs,
            damage_context,
            options
        ).ToDictionary();
        AttachDamageEventAggregates(result);
        return result;
    }

    internal virtual BattleDamagePreviewResult preview_damage_sequence_typed(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        GArray effect_defs,
        GDictionary damage_context = null,
        GDictionary options = null
    )
    {
        if (source_unit == null || target_unit == null)
        {
            return BattleDamagePreviewResult.Empty();
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
        DamageResolutionContext previewContextFlags =
            DamageResolutionContext.FromDictionary(previewContext);
        BattleUnitState sourcePreview = source_unit.clone();
        BattleUnitState targetPreview = target_unit.clone();
        if (sourcePreview == null || targetPreview == null)
        {
            return BattleDamagePreviewResult.Empty();
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
        var saveEstimates = new List<BattleDamagePreviewSaveEstimate>();

        bool previousSuppression = _suppress_last_stand_mastery_records;
        _suppress_last_stand_mastery_records = true;
        try
        {
            foreach (var effectValue in CoerceEffectDefs(effect_defs))
            {
                CombatEffectDef effectDef = effectValue.AsGodotObject() as CombatEffectDef;
                if (effectDef == null || !DoesEffectTrigger(effectDef, previewContextFlags))
                {
                    continue;
                }
                if (effectDef.effect_type != "damage")
                {
                    continue;
                }
                DamageOutcomeResult damageOutcome = ResolveDamageOutcome(
                    sourcePreview,
                    targetPreview,
                    effectDef,
                    previewContextFlags
                );
                if (damageOutcome.InvalidDamageTag)
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
                int preSaveDamage = damageOutcome.ResolvedDamage;
                int targetHpBeforeEffect = Math.Max(targetPreview.current_hp, 1);
                DamagePreviewSaveEstimate saveEstimate = BuildDamagePreviewSaveEstimate(
                    sourcePreview,
                    targetPreview,
                    effectDef,
                    previewContextFlags.Payload,
                    preSaveDamage,
                    saveMode
                );
                DamagePreviewBranchLethalEstimate branchLethal = default;
                if (saveEstimate.HasSave)
                {
                    saveEstimates.Add(saveEstimate.ToPreviewSaveEstimate());
                    branchLethal = BuildSaveBranchLethalEstimate(
                        targetPreview,
                        damageOutcome,
                        saveEstimate,
                        sourcePreview
                    );
                    stableLethalFromBranches =
                        stableLethalFromBranches || branchLethal.StableLethal;
                    lethalProbabilityBasisPoints = Math.Max(
                        lethalProbabilityBasisPoints,
                        branchLethal.LethalProbabilityBasisPoints
                    );
                }
                totalPreSaveDamage += preSaveDamage;
                totalPostSaveDamage += saveEstimate.DamageAfterSave;

                AppliedDamageResult damageResult;
                if (saveMode == DamagePreviewSaveModeExpected && saveEstimate.HasSave)
                {
                    damageResult = BuildExpectedSaveBranchDamageResult(
                        targetPreview,
                        damageOutcome,
                        saveEstimate,
                        sourcePreview
                    );
                }
                else
                {
                    damageOutcome = WithDamagePreviewSaveEstimate(
                        damageOutcome,
                        saveEstimate
                    );
                    damageResult = ApplyDamageToTargetResult(
                        targetPreview,
                        damageOutcome,
                        sourcePreview
                    );
                }

                int hpDamage = damageResult.HpDamage;
                if (
                    branchLethal.FailureKills
                    && !branchLethal.SuccessKills
                    && hpDamage >= targetHpBeforeEffect
                )
                {
                    hpDamage = Math.Max(branchLethal.SuccessHpDamage, 0);
                    damageResult = damageResult.WithHpDamage(hpDamage);
                }
                totalHpDamage += hpDamage;
                totalShieldAbsorbed += damageResult.ShieldAbsorbed;
                shieldBroken = shieldBroken || damageResult.ShieldBroken;
                damageEvents.Add(damageResult.ToDictionary());
                applied = true;
            }
        }
        finally
        {
            _suppress_last_stand_mastery_records = previousSuppression;
        }

        bool stableLethal = targetPreview.current_hp <= 0 || stableLethalFromBranches;
        return BattleDamagePreviewResult.Create(
            applied: applied,
            rollMode: rollMode,
            saveMode: saveMode,
            preSaveDamage: totalPreSaveDamage,
            postSaveDamage: totalPostSaveDamage,
            hpDamage: totalHpDamage,
            damage: totalHpDamage,
            incomingBudgetDamage: totalPostSaveDamage,
            shieldAbsorbed: totalShieldAbsorbed,
            shieldBroken: shieldBroken,
            shieldHpBefore: target_unit.current_shield_hp,
            shieldHpAfter: targetPreview.current_shield_hp,
            stableLethal: stableLethal,
            lethalProbabilityBasisPoints: targetPreview.current_hp <= 0
                ? 10000
                : lethalProbabilityBasisPoints,
            saveEstimates: saveEstimates,
            damageEvents: damageEvents,
            diagnostics: diagnostics,
            sourcePreviewAfter: sourcePreview,
            targetPreviewAfter: targetPreview
        );
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
        DamageResolutionContext contextFlags = DamageResolutionContext.FromDictionary(context);
        int totalDamage = 0;
        int totalHealing = 0;
        int totalShieldAbsorbed = 0;
        var damageEvents = new GArray();
        var damageDiceEvents = new List<DamageDiceEventSnapshot>();
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
            if (effectDef == null || !DoesEffectTrigger(effectDef, contextFlags))
            {
                continue;
            }

            StringName effectType = ProgressionDataUtils.to_string_name(effectDef.effect_type);
            if (effectType == "damage")
            {
                DamageOutcomeResult damageOutcome = ResolveDamageOutcome(
                    source_unit,
                    target_unit,
                    effectDef,
                    contextFlags
                );
                if (damageOutcome.InvalidDamageTag)
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
                BattleSaveResult damageSaveResult = BattleSaveResolver.resolve_save_result(
                    source_unit,
                    target_unit,
                    effectDef,
                    context
                );
                if (damageSaveResult.HasSave)
                {
                    saveResults.Add(damageSaveResult.ToDictionary());
                }
                damageOutcome = WithSaveResult(
                    damageOutcome,
                    damageSaveResult,
                    effectDef
                );
                AppliedDamageResult damageResult = ApplyDamageToTargetResult(
                    target_unit,
                    damageOutcome,
                    source_unit
                );
                int hpDamage = damageResult.Damage;
                totalDamage += hpDamage;
                totalShieldAbsorbed += damageResult.ShieldAbsorbed;
                damageEvents.Add(damageResult.ToDictionary());
                damageDiceEvents.Add(damageResult.DamageDiceEvent);
                blackStarWedgeTriggered =
                    blackStarWedgeTriggered
                    || damageResult.LowLuckBlackStarWedgeTriggered;
                shieldBroken = shieldBroken || damageResult.ShieldBroken;
                applied = true;
                if (damageResult.HasAppliedDamage)
                {
                    GrantStatusOnHitToSource(source_unit, effectDef, context);
                }
            }
            else if (effectType == EffectEquipmentDurabilityDamage)
            {
                EquipmentDurabilityDamageEffectResult durabilityResult =
                    ApplyEquipmentDurabilityDamageEffect(
                    source_unit,
                    target_unit,
                    effectDef,
                    contextFlags,
                    totalDamage,
                    totalShieldAbsorbed
                );
                if (durabilityResult.HasEvent)
                {
                    equipmentDurabilityEvents.Add(durabilityResult.ToDictionary());
                    if (durabilityResult.SaveResult.HasSave)
                    {
                        saveResults.Add(DuplicateDictionary(durabilityResult.SaveResult.Payload));
                    }
                    if (durabilityResult.DurabilityLoss > 0 || durabilityResult.Destroyed)
                    {
                        applied = true;
                    }
                }
            }
            else if (effectType == EffectHeal)
            {
                int healAmount = ResolveHealAmount(source_unit, effectDef);
                ApplyHealing(target_unit, healAmount);
                totalHealing += healAmount;
                applied = true;
            }
            else if (effectType == EffectStaminaRestore)
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
                BattleSaveResult statusSaveResult = BattleSaveResolver.resolve_save_result(
                    source_unit,
                    target_unit,
                    effectDef,
                    context
                );
                if (statusSaveResult.HasSave)
                {
                    saveResults.Add(statusSaveResult.ToDictionary());
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
                ExecuteEffectResult executeResult = ResolveExecuteEffect(
                    source_unit,
                    target_unit,
                    effectDef,
                    context,
                    statusEffectIds,
                    saveResults
                );
                if (executeResult.ExecuteStage >= 0)
                {
                    executeStage = executeResult.ExecuteStage;
                }
                if (!IsEmpty(executeResult.ExecuteOutcome))
                {
                    executeOutcome = executeResult.ExecuteOutcome;
                }
                if (executeResult.Applied)
                {
                    applied = true;
                }
                foreach (AppliedDamageResult damageResult in executeResult.DamageResults)
                {
                    totalDamage += damageResult.Damage;
                    totalShieldAbsorbed += damageResult.ShieldAbsorbed;
                    shieldBroken = shieldBroken || damageResult.ShieldBroken;
                    damageEvents.Add(damageResult.ToDictionary());
                    damageDiceEvents.Add(damageResult.DamageDiceEvent);
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
            sourceStatusEffectIds.Add(LowLuckRelicRules.STATUS_BLACK_STAR_WEDGE_EXPOSED);
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
        AttachDamageEventAggregates(result, damageDiceEvents);
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
        AppliedDamageResult damageResult = ApplyDamageToTargetResult(
            target_unit,
            damagePerLayer * fall_layers
        );
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
        AppliedDamageResult damageResult = ApplyDamageToTargetResult(
            target_unit,
            10 + sizeGap * 10
        );
        target_unit.is_alive = target_unit.current_hp > 0;
        return BuildEnvironmentalDamageResult(damageResult);
    }

    private AppliedDamageResult ApplyDamageToTargetResult(
        BattleUnitState targetUnit,
        int rawDamage,
        BattleUnitState sourceUnit = null
    )
    {
        int normalizedDamage = Math.Max(rawDamage, 0);
        if (targetUnit == null || normalizedDamage <= 0)
        {
            return BuildAppliedDamageResult(
                DamageApplicationInput.Empty,
                0,
                0,
                false
            );
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
        return ApplyDamageToTargetResult(
            targetUnit,
            DamageApplicationInput.Create(
                damageOutcome,
                normalizedDamage,
                shieldAbsorptionPercent: 100.0
            ),
            sourceUnit
        );
    }

    public GDictionary apply_direct_damage_to_target(
        BattleUnitState target_unit,
        GDictionary resolved_damage_input,
        BattleUnitState source_unit = null
    )
    {
        return ApplyDamageToTargetResult(
            target_unit,
            resolved_damage_input,
            source_unit
        ).ToDictionary();
    }

    public bool _does_effect_trigger(CombatEffectDef effect_def, GDictionary damage_context)
    {
        return DoesEffectTrigger(
            effect_def,
            DamageResolutionContext.FromDictionary(damage_context)
        );
    }

    private static bool DoesEffectTrigger(
        CombatEffectDef effectDef,
        DamageResolutionContext context
    )
    {
        if (effectDef == null)
        {
            return false;
        }
        StringName triggerEvent = ProgressionDataUtils.to_string_name(effectDef.trigger_event);
        if (triggerEvent == "")
        {
            return true;
        }
        if (triggerEvent == AttackResolutionCriticalHit)
        {
            return context.CriticalHit;
        }
        if (triggerEvent == TriggerEventOrdinaryHit)
        {
            return context.AttackSuccess && !context.CriticalHit;
        }
        if (triggerEvent == "secondary_hit")
        {
            return context.SecondaryHitSuccess;
        }
        GameLog.Warning(
            $"Unsupported combat effect trigger_event '{triggerEvent}' for effect_type '{ProgressionDataUtils.to_string_name(effectDef.effect_type)}'.",
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
            if (
                TryGetStatusBoolParam(statusEntry.@params, param_key, out bool boolValue)
                && boolValue
            )
            {
                return true;
            }
        }
        return false;
    }

    private static bool TryGetStatusBoolParam(
        GDictionary @params,
        StringName key,
        out bool value
    )
    {
        value = false;
        if (!TryGetStatusParam(@params, key, out object rawValue))
            return false;
        if (rawValue is bool boolValue)
        {
            value = boolValue;
            return true;
        }
        if (rawValue is Variant variantValue && variantValue.VariantType == Variant.Type.Bool)
        {
            value = variantValue.AsBool();
            return true;
        }
        return false;
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


    private DamageOutcomeResult ResolveDamageOutcome(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDef effectDef,
        DamageResolutionContext damageContext
    )
    {
        StringName damageTag = ResolveDamageTag(sourceUnit, effectDef);
        if (damageTag == "")
        {
            return BuildInvalidDamageTagOutcome(sourceUnit, effectDef);
        }
        StringName rollMode = damageContext.DamageRollMode;
        DicePoolRollResult damageRoll = RollDamageDice(effectDef, true, "damage_dice", rollMode);
        DicePoolRollResult weaponRoll = RollWeaponDice(
            sourceUnit,
            effectDef,
            true,
            "weapon_damage_dice",
            rollMode
        );
        bool criticalHit = damageContext.CriticalHit;
        bool bonusConditionMet = HasBonusCondition(effectDef, targetUnit);
        DicePoolRollResult bonusDamageRoll = bonusConditionMet
            ? RollBonusDamageDice(effectDef, true, "bonus_damage_dice", rollMode)
            : DicePoolRollResult.Empty;
        DicePoolRollResult criticalDamageRoll =
            criticalHit && damageRoll.HasDice
                ? RollDamageDice(effectDef, false, "critical_extra_damage_dice", rollMode)
                : DicePoolRollResult.Empty;
        DicePoolRollResult criticalWeaponRoll =
            criticalHit && weaponRoll.HasDice
                ? RollWeaponDice(
                    sourceUnit,
                    effectDef,
                    false,
                    "critical_extra_weapon_damage_dice",
                    rollMode
                )
                : DicePoolRollResult.Empty;
        DicePoolRollResult criticalBonusDamageRoll =
            criticalHit && bonusDamageRoll.HasDice
                ? RollBonusDamageDice(
                    effectDef,
                    false,
                    "critical_extra_bonus_damage_dice",
                    rollMode
                )
                : DicePoolRollResult.Empty;
        TraitTriggerResultSnapshot traitCritResult = ResolveCritTraitResult(
            sourceUnit,
            targetUnit,
            effectDef,
            criticalHit
        );
        DicePoolRollResult traitExtraWeaponRoll = traitCritResult.Triggered
            ? RollDicePool(
                traitCritResult.ExtraWeaponDiceCount,
                traitCritResult.ExtraWeaponDiceSides,
                0,
                "trait_extra_weapon_damage_dice",
                rollMode
            )
            : DicePoolRollResult.Empty;
        DicePoolRollResult consumedStackRoll = RollConsumedStackDice(
            sourceUnit,
            effectDef,
            rollMode
        );

        int baseDamage =
            Math.Max(effectDef?.power ?? 0, 0)
            + weaponRoll.TotalWithBonus
            + damageRoll.TotalWithBonus
            + bonusDamageRoll.TotalWithBonus
            + criticalWeaponRoll.Total
            + criticalDamageRoll.Total
            + criticalBonusDamageRoll.Total
            + traitExtraWeaponRoll.Total
            + consumedStackRoll.Total;
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
        bool lowLuckBlackStarWedgeTriggered = ApplyLowLuckBlackStarWedgeGuardIgnore(
            mitigation,
            sourceUnit
        );
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
        DamageDiceEventFlags damageDiceEventFlags = BuildDamageDiceEventFlags(
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
            ["damage_dice_count"] = damageRoll.Count,
            ["damage_dice_sides"] = damageRoll.Sides,
            ["damage_dice_rolls"] = damageRoll.Rolls,
            ["damage_dice_total"] = damageRoll.Total,
            ["damage_dice_bonus"] = damageRoll.Bonus,
            ["damage_dice_max_total"] = damageRoll.MaxTotal,
            ["damage_dice_is_max"] = damageRoll.IsMax,
            ["bonus_condition_met"] = bonusConditionMet,
            ["bonus_damage_dice_count"] = bonusDamageRoll.Count,
            ["bonus_damage_dice_sides"] = bonusDamageRoll.Sides,
            ["bonus_damage_dice_rolls"] = bonusDamageRoll.Rolls,
            ["bonus_damage_dice_total"] = bonusDamageRoll.Total,
            ["bonus_damage_dice_bonus"] = bonusDamageRoll.Bonus,
            ["bonus_damage_dice_max_total"] = bonusDamageRoll.MaxTotal,
            ["bonus_damage_dice_is_max"] = bonusDamageRoll.IsMax,
            ["weapon_damage_dice_count"] = weaponRoll.Count,
            ["weapon_damage_dice_sides"] = weaponRoll.Sides,
            ["weapon_damage_dice_rolls"] = weaponRoll.Rolls,
            ["weapon_damage_dice_total"] = weaponRoll.Total,
            ["weapon_damage_dice_bonus"] = weaponRoll.Bonus,
            ["weapon_damage_dice_max_total"] = weaponRoll.MaxTotal,
            ["weapon_damage_dice_is_max"] = weaponRoll.IsMax,
            ["critical_extra_damage_dice_count"] = criticalDamageRoll.Count,
            ["critical_extra_damage_dice_sides"] = criticalDamageRoll.Sides,
            ["critical_extra_damage_dice_rolls"] = criticalDamageRoll.Rolls,
            ["critical_extra_damage_dice_total"] = criticalDamageRoll.Total,
            ["critical_extra_damage_dice_max_total"] = criticalDamageRoll.MaxTotal,
            ["critical_extra_bonus_damage_dice_count"] = criticalBonusDamageRoll.Count,
            ["critical_extra_bonus_damage_dice_sides"] = criticalBonusDamageRoll.Sides,
            ["critical_extra_bonus_damage_dice_rolls"] = criticalBonusDamageRoll.Rolls,
            ["critical_extra_bonus_damage_dice_total"] = criticalBonusDamageRoll.Total,
            ["critical_extra_bonus_damage_dice_max_total"] = criticalBonusDamageRoll.MaxTotal,
            ["critical_extra_weapon_damage_dice_count"] = criticalWeaponRoll.Count,
            ["critical_extra_weapon_damage_dice_sides"] = criticalWeaponRoll.Sides,
            ["critical_extra_weapon_damage_dice_rolls"] = criticalWeaponRoll.Rolls,
            ["critical_extra_weapon_damage_dice_total"] = criticalWeaponRoll.Total,
            ["critical_extra_weapon_damage_dice_max_total"] = criticalWeaponRoll.MaxTotal,
            ["trait_extra_weapon_damage_dice_count"] = traitExtraWeaponRoll.Count,
            ["trait_extra_weapon_damage_dice_sides"] = traitExtraWeaponRoll.Sides,
            ["trait_extra_weapon_damage_dice_rolls"] = traitExtraWeaponRoll.Rolls,
            ["trait_extra_weapon_damage_dice_total"] = traitExtraWeaponRoll.Total,
            ["trait_extra_weapon_damage_dice_max_total"] = traitExtraWeaponRoll.MaxTotal,
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
            ["low_luck_black_star_wedge_triggered"] = lowLuckBlackStarWedgeTriggered,
            ["fixed_mitigation_total"] = fixedMitigationTotal,
            ["fully_absorbed_by_mitigation"] =
                resolvedDamage <= 0
                && mitigationTier != MitigationTierImmune
                && tierAdjustedDamage > 0,
            ["trait_trigger_results"] = new GArray(),
        };
        AppendTraitTriggerResult(result, traitCritResult);
        ApplyDamageDiceEventFlags(result, damageDiceEventFlags.Payload);
        return new DamageOutcomeResult(
            result,
            false,
            "",
            "",
            "",
            damageTag,
            resolvedDamage,
            false,
            false,
            100.0,
            0,
            lowLuckBlackStarWedgeTriggered,
            damageDiceEventFlags.Snapshot
        );
    }

    private AppliedDamageResult ApplyDamageToTargetResult(
        BattleUnitState targetUnit,
        GDictionary damageOutcome,
        BattleUnitState sourceUnit = null
    )
    {
        return ApplyDamageToTargetResult(
            targetUnit,
            DamageApplicationInput.FromDictionary(damageOutcome),
            sourceUnit
        );
    }

    private AppliedDamageResult ApplyDamageToTargetResult(
        BattleUnitState targetUnit,
        DamageOutcomeResult damageOutcome,
        BattleUnitState sourceUnit = null
    )
    {
        return ApplyDamageToTargetResult(
            targetUnit,
            damageOutcome.ToDamageApplicationInput(),
            sourceUnit
        );
    }

    private AppliedDamageResult ApplyDamageToTargetResult(
        BattleUnitState targetUnit,
        DamageApplicationInput damageInput,
        BattleUnitState sourceUnit = null
    )
    {
        int normalizedDamage = damageInput.ResolvedDamage;
        if (targetUnit == null || normalizedDamage <= 0)
        {
            return BuildAppliedDamageResult(damageInput, 0, 0, false);
        }

        bool bypassShield = damageInput.BypassShield;
        bool bypassDeathPrevention = damageInput.BypassDeathPrevention;
        double shieldEfficiency = damageInput.ShieldAbsorptionPercent / 100.0;
        int minHpAfterDamage = damageInput.MinHpAfterDamage;
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
                    TraitTriggerResultSnapshot fatalTraitResult =
                        ResolveFatalDamageTraitResult(
                            targetUnit,
                            sourceUnit,
                            hpDamage,
                            projectedHp
                        );
                    if (
                        fatalTraitResult.Triggered
                        && fatalTraitResult.ClampToHp > 0
                    )
                    {
                        targetUnit.current_hp = Math.Max(
                            fatalTraitResult.ClampToHp,
                            1
                        );
                        AppendTraitTriggerResult(damageInput.Payload, fatalTraitResult);
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

        return BuildAppliedDamageResult(damageInput, hpDamage, shieldAbsorbed, shieldBroken);
    }

    private AppliedDamageResult BuildExpectedSaveBranchDamageResult(
        BattleUnitState targetPreview,
        DamageOutcomeResult damageOutcome,
        DamagePreviewSaveEstimate saveEstimate,
        BattleUnitState sourcePreview
    )
    {
        int successBasis = Math.Clamp(saveEstimate.SaveSuccessProbabilityBasisPoints, 0, 10000);
        int failureBasis = Math.Clamp(saveEstimate.SaveFailureProbabilityBasisPoints, 0, 10000);
        int failureDamage = Math.Max(saveEstimate.DamageOnSaveFailure, 0);
        int successDamage = Math.Max(saveEstimate.DamageOnSaveSuccess, 0);

        BattleUnitState failureTarget = targetPreview.clone();
        BattleUnitState successTarget = targetPreview.clone();
        DamageOutcomeResult failureOutcome = damageOutcome.WithResolvedDamage(failureDamage);
        DamageOutcomeResult successOutcome = damageOutcome.WithResolvedDamage(successDamage);
        AppliedDamageResult failureResult = ApplyDamageToTargetResult(
            failureTarget,
            failureOutcome,
            sourcePreview
        );
        AppliedDamageResult successResult = ApplyDamageToTargetResult(
            successTarget,
            successOutcome,
            sourcePreview
        );

        int expectedHpDamage = RoundToInt(
            (
                failureResult.HpDamage * failureBasis
                + successResult.HpDamage * successBasis
            ) / 10000.0
        );
        int expectedShieldAbsorbed = RoundToInt(
            (
                failureResult.ShieldAbsorbed * failureBasis
                + successResult.ShieldAbsorbed * successBasis
            ) / 10000.0
        );

        GDictionary result = WithDamagePreviewSaveEstimate(
            damageOutcome,
            saveEstimate
        ).ToDictionary();
        result["damage"] = expectedHpDamage;
        result["hp_damage"] = expectedHpDamage;
        result["shield_absorbed"] = expectedShieldAbsorbed;
        result["shield_broken"] = failureResult.ShieldBroken && failureBasis > 0;
        result["fully_absorbed_by_shield"] = expectedHpDamage <= 0 && expectedShieldAbsorbed > 0;
        return new AppliedDamageResult(
            result,
            expectedHpDamage,
            expectedHpDamage,
            expectedShieldAbsorbed,
            failureResult.ShieldBroken && failureBasis > 0,
            failureResult.LowLuckBlackStarWedgeTriggered
                || successResult.LowLuckBlackStarWedgeTriggered,
            damageOutcome.DamageDiceEvent
        );
    }

    private DamagePreviewBranchLethalEstimate BuildSaveBranchLethalEstimate(
        BattleUnitState targetPreview,
        DamageOutcomeResult damageOutcome,
        DamagePreviewSaveEstimate saveEstimate,
        BattleUnitState sourcePreview
    )
    {
        int failureBasis = Math.Clamp(saveEstimate.SaveFailureProbabilityBasisPoints, 0, 10000);
        int failureDamage = Math.Max(saveEstimate.DamageOnSaveFailure, 0);
        int successDamage = Math.Max(saveEstimate.DamageOnSaveSuccess, 0);

        BattleUnitState failureTarget = targetPreview.clone();
        BattleUnitState successTarget = targetPreview.clone();
        DamageOutcomeResult failureOutcome = damageOutcome.WithResolvedDamage(failureDamage);
        DamageOutcomeResult successOutcome = damageOutcome.WithResolvedDamage(successDamage);
        AppliedDamageResult failureResult = ApplyDamageToTargetResult(
            failureTarget,
            failureOutcome,
            sourcePreview
        );
        AppliedDamageResult successResult = ApplyDamageToTargetResult(
            successTarget,
            successOutcome,
            sourcePreview
        );

        bool failureKills = failureTarget != null && failureTarget.current_hp <= 0;
        bool successKills = successTarget != null && successTarget.current_hp <= 0;
        return new DamagePreviewBranchLethalEstimate(
            failureKills,
            successKills,
            failureResult.HpDamage,
            successResult.HpDamage,
            failureKills && successKills,
            failureKills
                ? (successKills ? 10000 : failureBasis)
                : 0
        );
    }

    private DamagePreviewSaveEstimate BuildDamagePreviewSaveEstimate(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDef effectDef,
        GDictionary damageContext,
        int damageBeforeSave,
        StringName saveMode
    )
    {
        BattleSaveProbabilityResult probability =
            BattleSaveResolver.estimate_save_success_probability_result(
            sourceUnit,
            targetUnit,
            effectDef,
            damageContext ?? new GDictionary()
        );
        if (!probability.HasSave)
        {
            return DamagePreviewSaveEstimate.None(damageBeforeSave);
        }
        int successBasisPoints = Math.Clamp(probability.SuccessProbabilityBasisPoints, 0, 10000);
        int failureBasisPoints = Math.Clamp(probability.FailureProbabilityBasisPoints, 0, 10000);
        int damageOnSaveSuccess =
            effectDef != null
            && effectDef.save_partial_on_success
            && !probability.Immune
                ? damageBeforeSave / 2
                : 0;
        int expectedDamage = RoundToInt(
            (damageBeforeSave * failureBasisPoints + damageOnSaveSuccess * successBasisPoints)
                / 10000.0
        );
        int worstDamage = failureBasisPoints <= 0 ? damageOnSaveSuccess : damageBeforeSave;
        int damageAfterSave = saveMode == DamagePreviewSaveModeWorst ? worstDamage : expectedDamage;
        return new DamagePreviewSaveEstimate(
            true,
            damageBeforeSave,
            Math.Max(damageAfterSave, 0),
            Math.Max(expectedDamage, 0),
            Math.Max(worstDamage, 0),
            damageBeforeSave,
            damageOnSaveSuccess,
            effectDef != null && effectDef.save_partial_on_success,
            successBasisPoints,
            RoundToInt(successBasisPoints / 100.0),
            failureBasisPoints,
            probability.Dc,
            probability.Ability.ToString(),
            probability.SaveTag.ToString(),
            probability.AdvantageState.ToString(),
            probability.AbilityValue,
            probability.AbilityModifier,
            probability.Bonus,
            probability.Immune,
            probability.Sources ?? Array.Empty<BattleSaveSource>()
        );
    }

    private static GArray BuildSaveSourceArray(IReadOnlyList<BattleSaveSource> sources)
    {
        var result = new GArray();
        if (sources == null)
        {
            return result;
        }
        foreach (BattleSaveSource source in sources)
        {
            result.Add(source.ToDictionary());
        }
        return result;
    }

    private static DamageOutcomeResult WithDamagePreviewSaveEstimate(
        DamageOutcomeResult damageOutcome,
        DamagePreviewSaveEstimate saveEstimate
    )
    {
        GDictionary payload = damageOutcome.ToDictionary();
        int resolvedDamage = ApplyDamagePreviewSaveEstimate(payload, saveEstimate);
        return damageOutcome with
        {
            Payload = payload,
            ResolvedDamage = Math.Max(resolvedDamage, 0),
        };
    }

    private static int ApplyDamagePreviewSaveEstimate(
        GDictionary damageOutcome,
        DamagePreviewSaveEstimate saveEstimate
    )
    {
        if (damageOutcome == null)
        {
            return Math.Max(saveEstimate.DamageAfterSave, 0);
        }
        damageOutcome["pre_save_damage"] = saveEstimate.DamageBeforeSave;
        if (!saveEstimate.HasSave)
        {
            damageOutcome["save_adjusted_damage"] = saveEstimate.DamageAfterSave;
            damageOutcome["fully_absorbed_by_save"] = false;
            return Math.Max(saveEstimate.DamageAfterSave, 0);
        }
        int adjustedDamage = Math.Max(saveEstimate.DamageAfterSave, 0);
        damageOutcome["save_result"] = saveEstimate.ToDictionary();
        damageOutcome["save_success_probability_basis_points"] =
            saveEstimate.SaveSuccessProbabilityBasisPoints;
        damageOutcome["save_failure_probability_basis_points"] =
            saveEstimate.SaveFailureProbabilityBasisPoints;
        damageOutcome["save_immune"] = saveEstimate.Immune;
        damageOutcome["save_partial_applied"] = saveEstimate.SavePartialOnSuccess;
        damageOutcome["resolved_damage"] = adjustedDamage;
        damageOutcome["save_adjusted_damage"] = adjustedDamage;
        damageOutcome["fully_absorbed_by_save"] =
            saveEstimate.DamageBeforeSave > 0 && adjustedDamage <= 0;
        return adjustedDamage;
    }

    private static DamageOutcomeResult WithSaveResult(
        DamageOutcomeResult damageOutcome,
        BattleSaveResult saveResult,
        CombatEffectDef effectDef
    )
    {
        GDictionary payload = damageOutcome.ToDictionary();
        int resolvedDamage = ApplySaveResultToDamageOutcome(
            payload,
            saveResult,
            effectDef,
            damageOutcome.ResolvedDamage
        );
        return damageOutcome with
        {
            Payload = payload,
            ResolvedDamage = Math.Max(resolvedDamage, 0),
        };
    }

    private static int ApplySaveResultToDamageOutcome(
        GDictionary damageOutcome,
        BattleSaveResult saveResult,
        CombatEffectDef effectDef,
        int preSaveDamage
    )
    {
        if (damageOutcome == null || !saveResult.HasSave)
        {
            return Math.Max(preSaveDamage, 0);
        }
        preSaveDamage = Math.Max(preSaveDamage, 0);
        damageOutcome["save_result"] = saveResult.ToDictionary();
        damageOutcome["save_success"] = saveResult.Success;
        damageOutcome["save_immune"] = saveResult.Immune;
        damageOutcome["save_partial_applied"] = false;
        damageOutcome["pre_save_damage"] = preSaveDamage;
        if (!saveResult.Success)
        {
            damageOutcome["save_adjusted_damage"] = preSaveDamage;
            damageOutcome["fully_absorbed_by_save"] = false;
            return preSaveDamage;
        }
        int adjustedDamage = 0;
        if (
            effectDef != null
            && effectDef.save_partial_on_success
            && !saveResult.Immune
        )
        {
            adjustedDamage = preSaveDamage / 2;
            damageOutcome["save_partial_applied"] = true;
        }
        damageOutcome["resolved_damage"] = adjustedDamage;
        damageOutcome["save_adjusted_damage"] = adjustedDamage;
        damageOutcome["fully_absorbed_by_save"] = preSaveDamage > 0 && adjustedDamage <= 0;
        return adjustedDamage;
    }

    private DicePoolRollResult RollDamageDice(
        CombatEffectDef effectDef,
        bool includeBonus = true,
        string fieldPrefix = "damage_dice",
        StringName rollMode = default
    )
    {
        if (effectDef == null)
        {
            return DicePoolRollResult.Empty;
        }
        int diceCount = Math.Max(effectDef.dice_count, 0);
        int diceSides = Math.Max(effectDef.dice_sides, 0);
        int diceBonus = includeBonus ? effectDef.dice_bonus : 0;
        return RollDicePool(
            diceCount,
            diceSides,
            diceBonus,
            fieldPrefix,
            IsEmpty(rollMode) ? DamagePreviewRollModeRandom : rollMode
        );
    }

    private DicePoolRollResult RollBonusDamageDice(
        CombatEffectDef effectDef,
        bool includeBonus = true,
        string fieldPrefix = "bonus_damage_dice",
        StringName rollMode = default
    )
    {
        if (effectDef == null)
        {
            return DicePoolRollResult.Empty;
        }
        int diceCount = Math.Max(effectDef.bonus_damage_dice_count, 0);
        int diceSides = Math.Max(effectDef.bonus_damage_dice_sides, 0);
        int diceBonus = includeBonus ? effectDef.bonus_damage_dice_bonus : 0;
        return RollDicePool(
            diceCount,
            diceSides,
            diceBonus,
            fieldPrefix,
            IsEmpty(rollMode) ? DamagePreviewRollModeRandom : rollMode
        );
    }

    private DicePoolRollResult RollWeaponDice(
        BattleUnitState sourceUnit,
        CombatEffectDef effectDef,
        bool includeBonus = true,
        string fieldPrefix = "weapon_damage_dice",
        StringName rollMode = default
    )
    {
        if (!ShouldAddWeaponDice(effectDef))
        {
            return DicePoolRollResult.Empty;
        }
        GDictionary dice = GetCurrentWeaponDamageDice(sourceUnit);
        if (dice.Count == 0)
        {
            return DicePoolRollResult.Empty;
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

    private DicePoolRollResult RollDicePool(
        int diceCount,
        int diceSides,
        int diceBonus,
        string fieldPrefix,
        StringName rollMode = default
    )
    {
        if (string.IsNullOrEmpty(fieldPrefix))
        {
            return DicePoolRollResult.Empty;
        }
        DicePoolRollResult rollResult = RollDicePoolValues(
            diceCount,
            diceSides,
            diceBonus,
            rollMode
        );
        if (!rollResult.HasDice)
        {
            return DicePoolRollResult.Empty;
        }
        GDictionary payload = new()
        {
            [$"{fieldPrefix}_count"] = rollResult.Count,
            [$"{fieldPrefix}_sides"] = rollResult.Sides,
            [$"{fieldPrefix}_rolls"] = rollResult.Rolls,
            [$"{fieldPrefix}_total"] = rollResult.Total,
            [$"{fieldPrefix}_bonus"] = rollResult.Bonus,
            [$"{fieldPrefix}_max_total"] = rollResult.MaxTotal,
            [$"{fieldPrefix}_is_max"] = rollResult.IsMax,
        };
        return rollResult with { Payload = payload };
    }

    private DicePoolRollResult RollDicePoolValues(
        int diceCount,
        int diceSides,
        int diceBonus,
        StringName rollMode = default
    )
    {
        if (diceCount <= 0 || diceSides <= 0)
        {
            return DicePoolRollResult.Empty;
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
        return new DicePoolRollResult(
            new GDictionary(),
            diceCount,
            diceSides,
            rolls,
            diceTotal,
            diceBonus,
            maxTotal,
            diceTotal == maxTotal
        );
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

    private static DamageDiceEventFlags BuildDamageDiceEventFlags(
        bool criticalHit,
        DicePoolRollResult skillRoll,
        DicePoolRollResult weaponRoll,
        DicePoolRollResult bonusSkillRoll = default
    )
    {
        int skillDiceCount = skillRoll.Count;
        int skillDiceSides = skillRoll.Sides;
        int skillDiceTotal = skillRoll.Total;
        int skillDiceMaxTotal = skillRoll.MaxTotal;
        int bonusSkillDiceCount = bonusSkillRoll.Count;
        int bonusSkillDiceSides = bonusSkillRoll.Sides;
        int bonusSkillDiceTotal = bonusSkillRoll.Total;
        int bonusSkillDiceMaxTotal = bonusSkillRoll.MaxTotal;
        bool hasSkillDice =
            (skillDiceCount > 0 && skillDiceSides > 0 && skillDiceMaxTotal > 0)
            || (bonusSkillDiceCount > 0 && bonusSkillDiceSides > 0 && bonusSkillDiceMaxTotal > 0);
        skillDiceTotal += bonusSkillDiceTotal;
        skillDiceMaxTotal += bonusSkillDiceMaxTotal;

        int weaponDiceCount = weaponRoll.Count;
        int weaponDiceSides = weaponRoll.Sides;
        int weaponDiceTotal = weaponRoll.Total;
        int weaponDiceMaxTotal = weaponRoll.MaxTotal;
        bool hasWeaponDice = weaponDiceCount > 0 && weaponDiceSides > 0 && weaponDiceMaxTotal > 0;
        bool hasAnyRegularDice = hasSkillDice || hasWeaponDice;
        int regularDiceTotal = skillDiceTotal + weaponDiceTotal;
        int regularDiceMaxTotal = skillDiceMaxTotal + weaponDiceMaxTotal;

        bool damageDiceHighTotalRoll = false;
        bool skillDamageDiceIsMax = false;
        bool weaponDamageDiceIsMax = false;
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
            damageDiceHighTotalRoll = true;
            result["damage_dice_high_total_roll"] = true;
            result["damage_dice_high_total_roll_reason"] = DiceEventReasonCriticalHit;
        }
        else if (
            hasAnyRegularDice
            && regularDiceTotal * DamageDiceHighTotalThresholdDenominator
                >= regularDiceMaxTotal * DamageDiceHighTotalThresholdNumerator
        )
        {
            damageDiceHighTotalRoll = true;
            result["damage_dice_high_total_roll"] = true;
            result["damage_dice_high_total_roll_reason"] = DiceEventReasonDiceThreshold;
        }
        if (criticalHit && hasSkillDice)
        {
            skillDamageDiceIsMax = true;
            result["skill_damage_dice_is_max"] = true;
            result["skill_damage_dice_is_max_reason"] = DiceEventReasonCriticalHit;
        }
        else if (hasSkillDice && skillDiceTotal == skillDiceMaxTotal)
        {
            skillDamageDiceIsMax = true;
            result["skill_damage_dice_is_max"] = true;
            result["skill_damage_dice_is_max_reason"] = DiceEventReasonSkillDiceMax;
        }
        if (criticalHit && hasWeaponDice)
        {
            weaponDamageDiceIsMax = true;
            result["weapon_damage_dice_is_max"] = true;
            result["weapon_damage_dice_is_max_reason"] = DiceEventReasonCriticalHit;
        }
        else if (hasWeaponDice && weaponDiceTotal == weaponDiceMaxTotal)
        {
            weaponDamageDiceIsMax = true;
            result["weapon_damage_dice_is_max"] = true;
            result["weapon_damage_dice_is_max_reason"] = DiceEventReasonWeaponDiceMax;
        }
        return new DamageDiceEventFlags(
            result,
            new DamageDiceEventSnapshot(
                damageDiceHighTotalRoll,
                skillDamageDiceIsMax,
                weaponDamageDiceIsMax
            )
        );
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
            DamageDiceEventSnapshot damageEvent = DamageDiceEventSnapshot.FromDictionary(
                eventValue
            );
            if (damageEvent.DamageDiceHighTotalRoll)
                result["damage_dice_high_total_roll"] = true;
            if (damageEvent.SkillDamageDiceIsMax)
                result["skill_damage_dice_is_max"] = true;
            if (damageEvent.WeaponDamageDiceIsMax)
                result["weapon_damage_dice_is_max"] = true;
        }
    }

    private static void AttachDamageEventAggregates(
        GDictionary result,
        IEnumerable<DamageDiceEventSnapshot> damageEvents
    )
    {
        result["damage_dice_high_total_roll"] = false;
        result["skill_damage_dice_is_max"] = false;
        result["weapon_damage_dice_is_max"] = false;
        if (damageEvents == null)
        {
            return;
        }
        foreach (DamageDiceEventSnapshot damageEvent in damageEvents)
        {
            if (damageEvent.DamageDiceHighTotalRoll)
                result["damage_dice_high_total_roll"] = true;
            if (damageEvent.SkillDamageDiceIsMax)
                result["skill_damage_dice_is_max"] = true;
            if (damageEvent.WeaponDamageDiceIsMax)
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
        return DamageEffectRuntimeParameters.FromEffect(effectDef).UseWeaponPhysicalDamageTag;
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

    private bool ApplyLowLuckBlackStarWedgeGuardIgnore(
        GDictionary mitigation,
        BattleUnitState sourceUnit
    )
    {
        if (mitigation == null || sourceUnit == null)
        {
            return false;
        }
        if (!LowLuckRelicRules.UnitHasFlag(sourceUnit, LowLuckRelicRules.ATTR_BLACK_STAR_WEDGE))
        {
            return false;
        }
        BattleAiBlackboard aiBlackboard = sourceUnit.ai_blackboard;
        if (aiBlackboard == null || aiBlackboard.low_luck_black_star_wedge_used)
        {
            return false;
        }
        aiBlackboard.low_luck_black_star_wedge_used = true;
        int remainingIgnore = LowLuckRelicRules.BLACK_STAR_WEDGE_GUARD_IGNORE_FLAT;
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
        return true;
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
        return effectDef != null
            && bypassTag != ""
            && ProgressionDataUtils.to_string_name(effectDef.dr_bypass_tag) == bypassTag;
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
        int thresholdPercent =
            effectDef != null && effectDef.hp_ratio_threshold_percent > 0
                ? Math.Clamp(effectDef.hp_ratio_threshold_percent, 0, 100)
                : 50;
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
        return DamageEffectRuntimeParameters.FromEffect(effectDef).AddWeaponDice;
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

    private static int GetCurrentWeaponDamageDiceSides(BattleUnitState unitState)
    {
        GDictionary dice = GetCurrentWeaponDamageDice(unitState);
        return Math.Max(DictInt(dice, "dice_sides"), 0);
    }

    private DamageOutcomeResult BuildInvalidDamageTagOutcome(
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
        GDictionary payload = new()
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
        return new DamageOutcomeResult(
            payload,
            true,
            "invalid_damage_tag",
            reason.ToString(),
            sourceLabel.ToString(),
            configuredTag,
            0,
            false,
            false,
            100.0,
            0,
            false,
            DamageDiceEventSnapshot.Empty
        );
    }

    private static GDictionary BuildInvalidDamageTagDiagnostic(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDef effectDef,
        DamageOutcomeResult damageOutcome
    )
    {
        return new GDictionary
        {
            ["error_code"] = "invalid_damage_tag",
            ["reason"] = damageOutcome.Reason,
            ["damage_tag_source"] = damageOutcome.DamageTagSource,
            ["damage_tag"] = damageOutcome.DamageTag,
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

    private TraitTriggerResultSnapshot ResolveCritTraitResult(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDef effectDef,
        bool criticalHit
    )
    {
        if (!criticalHit)
        {
            return TraitTriggerResultSnapshot.FromAttackTraitTriggerResult(
                new AttackTraitTriggerResult(@event: TraitTriggerHooks.TRIGGER_ON_CRIT())
            );
        }
        return TraitTriggerResultSnapshot.FromAttackTraitTriggerResult(
            _trait_trigger_hooks.on_crit_typed(
                sourceUnit,
                targetUnit,
                criticalHit,
                ShouldAddWeaponDice(effectDef),
                sourceUnit != null ? sourceUnit.weapon_attack_range : 0,
                GetCurrentWeaponDamageDiceSides(sourceUnit)
            )
        );
    }

    private TraitTriggerResultSnapshot ResolveFatalDamageTraitResult(
        BattleUnitState targetUnit,
        BattleUnitState sourceUnit,
        int hpDamage,
        int projectedHp
    )
    {
        return TraitTriggerResultSnapshot.FromAttackTraitTriggerResult(
            _trait_trigger_hooks.on_fatal_damage_typed(
                targetUnit,
                sourceUnit,
                hpDamage,
                projectedHp
            )
        );
    }

    private static bool DoesSaveBlockEffect(BattleSaveResult saveResult)
    {
        return saveResult.HasSave && saveResult.Success;
    }

    private static StringName ResolveStatusIdForSave(
        CombatEffectDef effectDef,
        BattleSaveResult saveResult
    )
    {
        if (effectDef == null)
        {
            return "";
        }
        if (
            saveResult.HasSave
            && !saveResult.Success
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
        DamageEffectRuntimeParameters parameters = DamageEffectRuntimeParameters.FromEffect(
            effectDef
        );
        GDictionary @params = parameters.RawParams;
        bool sameFaction = sourceUnit != null && sourceUnit.faction_id == targetUnit.faction_id;
        bool removeHarmful =
            parameters.RemoveHarmful || (sameFaction && parameters.RemoveHarmfulFromAllies);
        bool removeBeneficial =
            parameters.RemoveBeneficial
            || (!sameFaction && parameters.RemoveBeneficialFromEnemies);
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

    private EquipmentDurabilityDamageEffectResult ApplyEquipmentDurabilityDamageEffect(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDef effectDef,
        DamageResolutionContext damageContext,
        int totalDamage,
        int totalShieldAbsorbed
    )
    {
        if (targetUnit == null || effectDef == null)
        {
            return EquipmentDurabilityDamageEffectResult.Empty;
        }
        DamageEffectRuntimeParameters parameters = DamageEffectRuntimeParameters.FromEffect(
            effectDef
        );
        bool attackSuccess = damageContext.AttackSuccess;
        if (
            parameters.RequireDamageApplied
            && !attackSuccess
            && totalDamage <= 0
            && totalShieldAbsorbed <= 0
        )
        {
            return EquipmentDurabilityDamageEffectResult.Empty;
        }
        GDictionary selection = SelectEquipmentForDurabilityDamage(
            targetUnit,
            effectDef,
            damageContext.Payload
        );
        if (selection.Count == 0)
        {
            return EquipmentDurabilityDamageEffectResult.Empty;
        }
        EquipmentState equipmentView = targetUnit.get_equipment_view();
        StringName entrySlotId = DictStringName(selection, "entry_slot_id");
        EquipmentInstanceState equipmentInstance =
            GetObject(selection, "equipment_instance") as EquipmentInstanceState;
        if (equipmentView == null || entrySlotId == "" || equipmentInstance == null)
        {
            return EquipmentDurabilityDamageEffectResult.Empty;
        }
        int before = Math.Max(equipmentInstance.current_durability, 0);
        if (before <= 0)
        {
            equipmentView.clear_entry_slot(entrySlotId);
            return EquipmentDurabilityDamageEffectResult.Empty;
        }
        int rarity = equipmentInstance.rarity;
        EquipmentDurabilitySaveResolution saveResult = ResolveEquipmentDurabilitySave(
            sourceUnit,
            targetUnit,
            effectDef,
            damageContext.Payload,
            rarity
        );
        GDictionary @event = new()
        {
            ["effect_type"] = EffectEquipmentDurabilityDamage.ToString(),
            ["target_unit_id"] = targetUnit.unit_id.ToString(),
            ["entry_slot_id"] = entrySlotId.ToString(),
            ["slot_id"] = DictString(selection, "slot_id", entrySlotId.ToString()),
            ["item_id"] = equipmentInstance.item_id.ToString(),
            ["instance_id"] = equipmentInstance.instance_id.ToString(),
            ["rarity"] = rarity,
            ["durability_before"] = before,
            ["durability_after"] = before,
            ["durability_loss"] = 0,
            ["destroyed"] = false,
            ["save_result"] = DuplicateDictionary(saveResult.Payload),
        };
        if (saveResult.HasSave && saveResult.Success)
        {
            return new EquipmentDurabilityDamageEffectResult(@event, true, 0, false, saveResult);
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
            equipmentInstance.current_durability = after;
        }
        return new EquipmentDurabilityDamageEffectResult(
            @event,
            true,
            durabilityLoss,
            after <= 0,
            saveResult
        );
    }

    private static EquipmentDurabilitySaveResolution ResolveEquipmentDurabilitySave(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDef effectDef,
        GDictionary damageContext,
        int rarity
    )
    {
        BattleSaveResult baseSaveResult = BattleSaveResolver.resolve_save_result(
            sourceUnit,
            targetUnit,
            effectDef,
            damageContext ?? new GDictionary()
        );
        GDictionary saveResult = baseSaveResult.ToDictionary();
        int rarityBonus = EquipmentDurabilityRules.GetDisjunctionSaveBonusForRarity(rarity);
        saveResult["equipment_rarity_bonus"] = rarityBonus;
        if (!baseSaveResult.HasSave)
        {
            return new EquipmentDurabilitySaveResolution(saveResult, false, false);
        }
        saveResult["status_save_bonus"] = baseSaveResult.Bonus;
        saveResult["bonus"] = baseSaveResult.Bonus + rarityBonus;
        if (baseSaveResult.Immune)
        {
            return new EquipmentDurabilitySaveResolution(saveResult, true, true);
        }
        int naturalRoll = baseSaveResult.NaturalRoll;
        int rollTotal = baseSaveResult.RollTotal + rarityBonus;
        saveResult["roll_total"] = rollTotal;
        bool success = rollTotal >= baseSaveResult.Dc;
        if (naturalRoll <= 1)
            success = false;
        else if (naturalRoll >= 20)
            success = true;
        saveResult["success"] = success;
        return new EquipmentDurabilitySaveResolution(saveResult, true, success);
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
        if (equipmentInstance == null || equipmentInstance.current_durability <= 0)
        {
            return new GDictionary();
        }
        return new GDictionary
        {
            ["entry_slot_id"] = normalizedEntrySlot,
            ["slot_id"] = ProgressionDataUtils.to_string_name(slotId),
            ["occupied_slot_ids"] = entry.occupied_slot_ids.Duplicate(),
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

    private ExecuteEffectResult ResolveExecuteEffect(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDef effectDef,
        GDictionary context,
        GStringNameArray statusEffectIds,
        GArray saveResults
    )
    {
        DamageEffectRuntimeParameters parameters = DamageEffectRuntimeParameters.FromEffect(
            effectDef
        );
        GDictionary @params = parameters.RawParams;
        if (parameters.StagedExecution)
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
            return ExecuteEffectResult.Empty;
        }
        BattleSaveResult saveResult = BattleSaveResolver.resolve_save_result(
            sourceUnit,
            targetUnit,
            effectDef,
            context ?? new GDictionary()
        );
        if (saveResult.HasSave)
        {
            saveResults.Add(saveResult.ToDictionary());
        }
        GDictionary soulFractureParams = GetDictionary(executePlan, "soul_fracture_params");
        if (saveResult.Success)
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
                return new ExecuteEffectResult(
                    new GDictionary
                    {
                        ["applied"] = true,
                        ["execute_stage"] = 0,
                        ["execute_outcome"] = "resisted",
                    },
                    true,
                    0,
                    "resisted",
                    Array.Empty<AppliedDamageResult>()
                );
            }
            return new ExecuteEffectResult(
                new GDictionary
                {
                    ["applied"] = false,
                    ["execute_stage"] = 0,
                    ["execute_outcome"] = "resisted",
                },
                false,
                0,
                "resisted",
                Array.Empty<AppliedDamageResult>()
            );
        }
        int fatalDamage = Math.Max(DictInt(executePlan, "fatal_damage", targetUnit.current_hp), 0);
        DamageApplicationInput fatalDamageInput = BuildFatalExecuteDamageInput(
            effectDef,
            fatalDamage
        );
        AppliedDamageResult fatalResult = ApplyDamageToTargetResult(
            targetUnit,
            fatalDamageInput,
            sourceUnit
        );
        return new ExecuteEffectResult(
            new GDictionary
            {
                ["applied"] = true,
                ["execute_stage"] = 2,
                ["execute_outcome"] = "failed_save_fatal",
                ["damage_result"] = fatalResult.ToDictionary(),
            },
            true,
            2,
            "failed_save_fatal",
            new[] { fatalResult }
        );
    }

    private ExecuteEffectResult ResolveStagedExecuteEffect(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDef effectDef,
        GDictionary context,
        GStringNameArray statusEffectIds,
        GArray saveResults,
        GDictionary @params
    )
    {
        BattleSaveResult saveResult = BattleSaveResolver.resolve_save_result(
            sourceUnit,
            targetUnit,
            effectDef,
            context ?? new GDictionary()
        );
        if (saveResult.HasSave)
        {
            saveResults.Add(saveResult.ToDictionary());
        }

        int threshold = BattleExecutionRules.resolve_threshold(sourceUnit, targetUnit, @params);
        bool isVulnerable = targetUnit.current_hp <= threshold;
        bool isBoss = BattleExecutionRules.is_boss_target(targetUnit);
        var damageResults = new GArray();
        var typedDamageResults = new List<AppliedDamageResult>();
        bool applied = false;

        if (!isVulnerable || isBoss)
        {
            int nonLethalDamage = BattleExecutionRules.resolve_non_lethal_damage(
                sourceUnit,
                targetUnit,
                @params,
                isBoss
            );
            DamageApplicationInput nonLethalInput = BuildStagedExecuteDamageInput(
                effectDef,
                @params,
                nonLethalDamage,
                1
            );
            AppliedDamageResult nonLethalResult = ApplyDamageToTargetResult(
                targetUnit,
                nonLethalInput,
                sourceUnit
            );
            damageResults.Add(nonLethalResult.ToDictionary());
            typedDamageResults.Add(nonLethalResult);
            applied = true;
            if (nonLethalResult.HasAppliedDamage)
            {
                GrantStatusOnHitToSource(sourceUnit, effectDef, context);
            }
        }
        else
        {
            int burstDamage = Math.Max(DictInt(@params, "burst_damage", 9999), 0);
            DamageApplicationInput burstInput = BuildStagedExecuteDamageInput(
                effectDef,
                @params,
                burstDamage,
                1
            );
            AppliedDamageResult burstResult = ApplyDamageToTargetResult(
                targetUnit,
                burstInput,
                sourceUnit
            );
            damageResults.Add(burstResult.ToDictionary());
            typedDamageResults.Add(burstResult);
            applied = true;
            if (burstResult.HasAppliedDamage)
            {
                GrantStatusOnHitToSource(sourceUnit, effectDef, context);
            }

            if (!saveResult.Success && targetUnit.current_hp <= 1)
            {
                int finisherDamage = Math.Max(DictInt(@params, "finisher_damage", 1), 0);
                DamageApplicationInput finisherInput = BuildStagedExecuteDamageInput(
                    effectDef,
                    @params,
                    finisherDamage,
                    0
                );
                AppliedDamageResult finisherResult = ApplyDamageToTargetResult(
                    targetUnit,
                    finisherInput,
                    sourceUnit
                );
                damageResults.Add(finisherResult.ToDictionary());
                typedDamageResults.Add(finisherResult);
                if (finisherResult.HasAppliedDamage)
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

        return new ExecuteEffectResult(
            new GDictionary { ["applied"] = applied, ["damage_results"] = damageResults },
            applied,
            -1,
            "",
            typedDamageResults
        );
    }

    private static DamageApplicationInput BuildFatalExecuteDamageInput(
        CombatEffectDef effectDef,
        int resolvedDamage
    )
    {
        int normalizedDamage = Math.Max(resolvedDamage, 0);
        GDictionary payload = new()
        {
            ["damage_tag"] = ProgressionDataUtils.to_string_name(effectDef?.damage_tag ?? ""),
            ["resolved_damage"] = normalizedDamage,
            ["min_hp_after_damage"] = 0,
            ["bypass_shield"] = true,
            ["bypass_death_prevention"] = true,
            ["shield_absorption_percent"] = 0.0,
            ["execute_stage"] = 2,
            ["execute_outcome"] = "failed_save_fatal",
            ["death_source"] = "power_word_kill_execute",
            ["death_source_priority"] = 900,
        };
        return DamageApplicationInput.Create(
            payload,
            normalizedDamage,
            bypassShield: true,
            bypassDeathPrevention: true,
            shieldAbsorptionPercent: 0.0
        );
    }

    private static DamageApplicationInput BuildStagedExecuteDamageInput(
        CombatEffectDef effectDef,
        GDictionary @params,
        int resolvedDamage,
        int minHpAfterDamage
    )
    {
        int normalizedDamage = Math.Max(resolvedDamage, 0);
        int normalizedMinHpAfterDamage = Math.Max(minHpAfterDamage, 0);
        double shieldAbsorptionPercent = DictFloat(@params, "shield_absorption_percent", 50.0);
        GDictionary outcome = new()
        {
            ["resolved_damage"] = normalizedDamage,
            ["min_hp_after_damage"] = normalizedMinHpAfterDamage,
            ["shield_absorption_percent"] = shieldAbsorptionPercent,
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
        return DamageApplicationInput.Create(
            outcome,
            normalizedDamage,
            shieldAbsorptionPercent: shieldAbsorptionPercent,
            minHpAfterDamage: normalizedMinHpAfterDamage
        );
    }

    private int ResolveHealAmount(BattleUnitState sourceUnit, CombatEffectDef effectDef)
    {
        int healAmount = Math.Max(effectDef?.power ?? 0, 0);
        DicePoolRollResult healDiceRoll = RollEffectDice(sourceUnit, effectDef);
        if (healDiceRoll.HasDice)
        {
            healAmount += healDiceRoll.TotalWithBonus;
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
        if (targetUnit == null || effectDef == null)
        {
            return;
        }
        int staminaAmount = Math.Max(effectDef.power, 0);
        DicePoolRollResult staminaDiceRoll = RollEffectDice(sourceUnit, effectDef);
        if (staminaDiceRoll.HasDice)
        {
            staminaAmount += staminaDiceRoll.TotalWithBonus;
        }
        if (staminaAmount <= 0)
        {
            return;
        }
        int maxStamina = Math.Max(
            GetAttributeValue(targetUnit, AttributeService.STAMINA_MAX_ID()),
            0
        );
        targetUnit.current_stamina = Math.Min(
            targetUnit.current_stamina + staminaAmount,
            maxStamina
        );
    }

    private DicePoolRollResult RollEffectDice(
        BattleUnitState sourceUnit,
        CombatEffectDef effectDef
    )
    {
        if (effectDef == null)
        {
            return DicePoolRollResult.Empty;
        }
        int diceCount = Math.Max(effectDef.dice_count, 0);
        int diceSides = ResolveEffectDiceSides(sourceUnit, effectDef);
        int diceBonus = effectDef.dice_bonus;
        return RollDicePoolValues(diceCount, diceSides, diceBonus);
    }

    private int ResolveEffectDiceSides(BattleUnitState sourceUnit, CombatEffectDef effectDef)
    {
        if (effectDef == null)
        {
            return 0;
        }
        if (effectDef.dice_sides_base > 0)
        {
            return ResolveAttributeScaledDiceSides(sourceUnit, effectDef);
        }
        return Math.Max(effectDef.dice_sides, 0);
    }

    private int ResolveAttributeScaledDiceSides(
        BattleUnitState sourceUnit,
        CombatEffectDef effectDef
    )
    {
        int conMod = GetUnitBaseAttributeModifier(sourceUnit, UnitBaseAttributes.CONSTITUTION());
        int willMod = GetUnitBaseAttributeModifier(sourceUnit, UnitBaseAttributes.WILLPOWER());
        int baseSides = Math.Max(effectDef.dice_sides_base, 0);
        int conModSides = Math.Max(effectDef.dice_sides_per_constitution_mod, 0);
        int willModSides = Math.Max(effectDef.dice_sides_per_willpower_mod, 0);
        long diceSidesRaw =
            (long)baseSides + (long)conMod * conModSides + (long)willMod * willModSides;
        return (int)Math.Clamp(diceSidesRaw, 4L, int.MaxValue);
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
        BattleStatusEffectState statusEntry = BattleStatusSemanticTable.merge_status_typed(
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
        if (!LowLuckRelicRules.UnitHasFlag(targetUnit, LowLuckRelicRules.ATTR_BLOOD_DEBT_SHAWL))
        {
            return 1.0;
        }
        if (!IsUnitBelowHpRatio(targetUnit, LowLuckRelicRules.BLOOD_DEBT_LOW_HP_THRESHOLD_RATIO))
        {
            return 1.0;
        }
        return LowLuckRelicRules.BLOOD_DEBT_DAMAGE_MULTIPLIER;
    }

    private bool ApplyLowLuckBlackStarWedgeExposed(BattleUnitState sourceUnit)
    {
        if (sourceUnit == null)
        {
            return false;
        }
        ApplyRuntimeStatus(
            sourceUnit,
            LowLuckRelicRules.STATUS_BLACK_STAR_WEDGE_EXPOSED,
            LowLuckRelicRules.BLACK_STAR_WEDGE_EXPOSED_DURATION_TU,
            new GDictionary
            {
                ["incoming_damage_multiplier"] =
                    LowLuckRelicRules.BLACK_STAR_WEDGE_EXPOSED_INCOMING_DAMAGE_MULTIPLIER,
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

    private DicePoolRollResult RollConsumedStackDice(
        BattleUnitState sourceUnit,
        CombatEffectDef effectDef,
        StringName rollMode = default
    )
    {
        if (sourceUnit == null || effectDef == null)
        {
            return DicePoolRollResult.Empty;
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
            return DicePoolRollResult.Empty;
        }
        BattleStatusEffectState statusEntry = sourceUnit.get_status_effect(consumedId);
        int stackCount = Math.Max(statusEntry?.stacks ?? 0, 0);
        if (stackCount <= 0)
        {
            return DicePoolRollResult.Empty;
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
            new BattleSkillMasteryGrant
            {
                MemberId = targetUnit.source_member_id,
                SkillId = "warrior_last_stand",
                Amount = baseAmount,
                SourceType = sourceType,
                SourceLabel = "不屈",
                ReasonText = sourceType == "last_stand_triggered" ? "触发免死" : "极限承伤",
                AllowUnlocks = true,
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


    private static AppliedDamageResult BuildAppliedDamageResult(
        DamageApplicationInput damageInput,
        int hpDamage,
        int shieldAbsorbed,
        bool shieldBroken
    )
    {
        GDictionary result = DuplicateDictionary(damageInput.Payload);
        EnsureDamageDiceEventDefaults(result);
        result["damage"] = hpDamage;
        result["hp_damage"] = hpDamage;
        result["shield_absorbed"] = shieldAbsorbed;
        result["shield_broken"] = shieldBroken;
        result["fully_absorbed_by_shield"] = hpDamage <= 0 && shieldAbsorbed > 0;
        return new AppliedDamageResult(
            result,
            hpDamage,
            hpDamage,
            shieldAbsorbed,
            shieldBroken,
            damageInput.LowLuckBlackStarWedgeTriggered,
            damageInput.DamageDiceEvent
        );
    }

    private static GDictionary BuildEnvironmentalDamageResult(AppliedDamageResult damageResult)
    {
        GDictionary result = BuildEmptyResult();
        result["applied"] = damageResult.HasAppliedDamage;
        result["damage"] = damageResult.Damage;
        result["hp_damage"] = damageResult.HpDamage;
        result["shield_absorbed"] = damageResult.ShieldAbsorbed;
        result["shield_broken"] = damageResult.ShieldBroken;
        result["damage_events"] = new GArray { damageResult.ToDictionary() };
        AttachDamageEventAggregates(result, new[] { damageResult.DamageDiceEvent });
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

    private BattleSpellControlMetadata ResolveSpellControlMetadata(
        BattleUnitState sourceUnit,
        SpellControlCheckContext context
    )
    {
        _hit_resolver ??= new BattleHitResolver();
        return BattleSpellControlMetadata.FromDictionary(
            _hit_resolver.resolve_spell_control_metadata(sourceUnit, context.ToAttackContext())
        );
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
        BattleUnitState targetUnit,
        AttackResolutionMetadata attackMetadata
    )
    {
        if (result == null || result.Count == 0)
        {
            return;
        }
        GDictionary reportEntry = _report_formatter.BuildAttackReportEntry(
            sourceUnit,
            targetUnit,
            attackMetadata,
            ResolveCriticalSource(attackMetadata),
            BuildAttackEventTags(attackMetadata)
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
        BattleSpellControlMetadata controlMetadata,
        SpellControlCheckContext context
    )
    {
        if (controlMetadata == null || !controlMetadata.HasPayload)
        {
            return;
        }
        GDictionary payload = BuildSpellControlEventPayload(
            sourceUnit,
            controlMetadata,
            context
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
        BattleSpellControlMetadata controlMetadata,
        SpellControlCheckContext context
    )
    {
        controlMetadata ??= BattleSpellControlMetadata.Empty();
        return new GDictionary
        {
            ["battle_id"] =
                context.BattleState != null ? context.BattleState.battle_id : new StringName(""),
            ["attacker_id"] = sourceUnit != null ? sourceUnit.unit_id : new StringName(""),
            ["attacker_member_id"] =
                sourceUnit != null ? sourceUnit.source_member_id : new StringName(""),
            ["attacker_low_hp_hardship"] = IsLowHpHardship(sourceUnit),
            ["attacker_strong_attack_debuff_ids"] = GetStrongAttackDebuffIds(sourceUnit),
            ["defender_id"] = new StringName(""),
            ["defender_member_id"] = new StringName(""),
            ["defender_is_elite_or_boss"] = false,
            ["attack_resolution"] = controlMetadata.AttackResolution,
            ["spell_control_resolution"] = controlMetadata.SpellControlResolution,
            ["critical_source"] = ResolveCriticalSource(controlMetadata),
            ["is_disadvantage"] = controlMetadata.IsDisadvantage,
            ["crit_gate_die"] = controlMetadata.CritGateDie,
            ["crit_gate_roll"] = controlMetadata.CritGateRoll,
            ["hit_roll"] = controlMetadata.HitRoll,
            ["luck_snapshot"] = BuildAttackLuckSnapshot(controlMetadata),
            ["event_family"] = "spell_control",
            ["skill_id"] = context.SkillId,
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

    private static GDictionary BuildAttackLuckSnapshot(BattleSpellControlMetadata attackMetadata)
    {
        attackMetadata ??= BattleSpellControlMetadata.Empty();
        return new GDictionary
        {
            ["hidden_luck_at_birth"] = attackMetadata.HiddenLuckAtBirth,
            ["faith_luck_bonus"] = attackMetadata.FaithLuckBonus,
            ["effective_luck"] = attackMetadata.EffectiveLuck,
            ["fumble_low_end"] = attackMetadata.FumbleLowEnd,
            ["crit_threshold"] = attackMetadata.CritThreshold,
        };
    }

    private static StringName ResolveCriticalSource(AttackResolutionMetadata attackMetadata)
    {
        return attackMetadata == null || !attackMetadata.CriticalHit ? new StringName("")
            : IsHighThreatCriticalHit(attackMetadata) ? new StringName("high_threat")
            : new StringName("gate_die");
    }

    private static StringName ResolveCriticalSource(BattleSpellControlMetadata attackMetadata)
    {
        return attackMetadata == null || !attackMetadata.CriticalHit ? new StringName("")
            : IsHighThreatCriticalHit(attackMetadata) ? new StringName("high_threat")
            : new StringName("gate_die");
    }

    private static bool IsHighThreatCriticalHit(AttackResolutionMetadata attackMetadata)
    {
        return attackMetadata != null
            && attackMetadata.CriticalHit
            && attackMetadata.CritGateDie == NaturalHitRoll;
    }

    private static bool IsHighThreatCriticalHit(BattleSpellControlMetadata attackMetadata)
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

    private static GStringNameArray BuildSpellControlEventTags(
        BattleSpellControlMetadata controlMetadata
    )
    {
        var tags = new GStringNameArray();
        if (controlMetadata == null)
        {
            return tags;
        }
        if (controlMetadata.CriticalFail)
            tags.Add("critical_fail");
        if (IsHighThreatCriticalHit(controlMetadata))
            tags.Add("high_threat_critical_hit");
        if (controlMetadata.CriticalHit && controlMetadata.IsDisadvantage)
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

    private static bool TryGet(GDictionary source, object key, out Variant value)
    {
        value = default;
        if (source == null || key == null)
            return false;
        Variant variantKey = key switch
        {
            Variant valueKey => valueKey,
            string stringKey => stringKey,
            StringName stringNameKey => stringNameKey,
            int intKey => intKey,
            long longKey => longKey,
            _ => default,
        };
        if (variantKey.VariantType == Variant.Type.Nil)
            return false;
        if (source.ContainsKey(variantKey))
        {
            value = source[variantKey];
            return value.VariantType != Variant.Type.Nil;
        }
        if (variantKey.VariantType == Variant.Type.String)
        {
            StringName stringNameKey = new(variantKey.AsString());
            if (source.ContainsKey(stringNameKey))
            {
                value = source[stringNameKey];
                return value.VariantType != Variant.Type.Nil;
            }
        }
        else if (variantKey.VariantType == Variant.Type.StringName)
        {
            string stringKey = variantKey.AsStringName().ToString();
            if (source.ContainsKey(stringKey))
            {
                value = source[stringKey];
                return value.VariantType != Variant.Type.Nil;
            }
        }
        return false;
    }

    private static GDictionary GetDictionary(GDictionary source, object key)
    {
        if (!TryGet(source, key, out Variant value))
            return new GDictionary();
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new GDictionary();
    }

    private static GArray GetArray(GDictionary source, object key)
    {
        if (!TryGet(source, key, out Variant value))
            return new GArray();
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
    }

    private static GodotObject GetObject(GDictionary source, object key)
    {
        if (!TryGet(source, key, out Variant value))
            return null;
        return value.VariantType == Variant.Type.Object ? value.AsGodotObject() : null;
    }

    private static int GetInt(GDictionary source, object key, int fallback = 0)
    {
        if (!TryGet(source, key, out Variant value))
            return fallback;
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static double GetFloat(GDictionary source, object key, double fallback = 0.0)
    {
        if (!TryGet(source, key, out Variant value))
            return fallback;
        return value.VariantType switch
        {
            Variant.Type.Int => value.AsInt64(),
            Variant.Type.Float => value.AsDouble(),
            _ => fallback,
        };
    }

    private static string GetString(GDictionary source, object key, string fallback = "")
    {
        if (!TryGet(source, key, out Variant value))
            return fallback;
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => fallback,
        };
    }

    private static StringName GetStringName(
        GDictionary source,
        object key,
        StringName fallback = default
    )
    {
        if (!TryGet(source, key, out Variant value))
            return fallback ?? "";
        return value.VariantType switch
        {
            Variant.Type.StringName => value.AsStringName(),
            Variant.Type.String => new StringName(value.AsString()),
            _ => fallback ?? "",
        };
    }

    private static IEnumerable<GDictionary> ReadDictionaryItems(GArray values)
    {
        if (values == null)
            yield break;
        foreach (Variant value in values)
        {
            if (value.VariantType == Variant.Type.Dictionary)
                yield return value.AsGodotDictionary();
        }
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }

    private static bool HasKey(GDictionary source, object key)
    {
        return TryGet(source, key, out _);
    }

    private static int DictInt(GDictionary source, object key, int fallback = 0)
    {
        return GetInt(source, key, fallback);
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

    private static T DictObject<T>(GDictionary source, StringName key)
        where T : GodotObject
    {
        return TryGetStatusParam(source, key, out object rawValue)
            ? ToGodotObject<T>(rawValue)
            : null;
    }

    private static T ToGodotObject<T>(object rawValue)
        where T : GodotObject
    {
        if (rawValue is T typedValue)
        {
            return typedValue;
        }
        if (rawValue is Variant variantValue && variantValue.VariantType == Variant.Type.Object)
        {
            return variantValue.AsGodotObject() as T;
        }
        return null;
    }

    private static StringName DictStringNameLocal(
        GDictionary source,
        StringName key,
        StringName fallback = default
    )
    {
        if (!TryGetStatusParam(source, key, out object rawValue))
        {
            return fallback ?? "";
        }
        StringName normalized = ProgressionDataUtils.to_string_name(rawValue);
        return normalized == "" ? fallback ?? "" : normalized;
    }

    private static bool ToBool(object rawValue, bool fallback)
    {
        if (rawValue is bool boolValue)
        {
            return boolValue;
        }
        if (rawValue is not Variant variantValue)
        {
            return rawValue != null ? rawValue.ToString()?.ToLowerInvariant() == "true" : fallback;
        }
        return variantValue.VariantType switch
        {
            Variant.Type.Bool => variantValue.AsBool(),
            Variant.Type.Int => variantValue.AsInt32() != 0,
            Variant.Type.Float => !Mathf.IsZeroApprox((float)variantValue.AsDouble()),
            Variant.Type.String => variantValue.AsString().ToLowerInvariant() == "true",
            Variant.Type.StringName
                => variantValue.AsStringName().ToString().ToLowerInvariant() == "true",
            _ => fallback,
        };
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

    private static void AppendTraitTriggerResult(
        GDictionary target,
        TraitTriggerResultSnapshot triggerResult
    )
    {
        if (target == null || !triggerResult.Triggered)
        {
            return;
        }
        GArray results = GetArray(target, "trait_trigger_results");
        results = (GArray)results.Duplicate(true);
        results.Add(triggerResult.ToDictionary());
        target["trait_trigger_results"] = results;
    }
}
