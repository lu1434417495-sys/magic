using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

internal enum BattleDamagePreviewRollMode
{
    Unknown = 0,
    Random,
    Average,
    Maximum,
}

internal enum BattleDamagePreviewSaveMode
{
    Unknown = 0,
    Expected,
    Worst,
}

internal sealed class BattleDamagePreviewOptions
{
    public BattleDamagePreviewRollMode RollMode { get; }
    public BattleDamagePreviewSaveMode SaveMode { get; }

    public BattleDamagePreviewOptions(
        BattleDamagePreviewRollMode rollMode = BattleDamagePreviewRollMode.Average,
        BattleDamagePreviewSaveMode saveMode = BattleDamagePreviewSaveMode.Expected
    )
    {
        RollMode = rollMode;
        SaveMode = saveMode;
    }
}

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
    private static readonly StringName StatusBlackStarBrandEliteGuardWindow =
        "black_star_brand_elite_guard_window";
    private static readonly StringName StatusCrownBreakBrokenFang = "crown_break_broken_fang";
    private static readonly StringName StatusCrownBreakBrokenHand = "crown_break_broken_hand";
    private static readonly StringName StatusCrownBreakBlindedEye = "crown_break_blinded_eye";
    private static readonly StringName EffectEquipmentDurabilityDamage =
        BattleTypedNames.EffectEquipmentDurabilityDamage;
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

        internal GDictionary ToDictionary() => Payload?.Duplicate(true) ?? new GDictionary();
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

        internal GDictionary ToDictionary()
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

        internal GDictionary ToDictionary() => Payload?.Duplicate(true) ?? new GDictionary();

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
        internal GDictionary ToDictionary() => Payload?.Duplicate(true) ?? new GDictionary();

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

        internal static DamageApplicationInput FromDictionary(GDictionary payload)
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

        internal static bool ReadBool(GDictionary payload, string key)
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
        bool SecondaryHitSuccess,
        StringName SkillId,
        IReadOnlyList<int> SaveRollOverrides
    )
    {
        public BattleSaveContext ToBattleSaveContext() =>
            new(SkillId, SaveRollOverrides ?? Array.Empty<int>());

        internal static DamageResolutionContext FromDictionary(GDictionary payload)
        {
            GDictionary normalized = payload ?? new GDictionary();
            return new DamageResolutionContext(
                normalized,
                DictStringName(normalized, "damage_roll_mode", DamagePreviewRollModeRandom),
                DamageApplicationInput.ReadBool(normalized, "critical_hit"),
                DamageApplicationInput.ReadBool(normalized, "attack_success"),
                DamageApplicationInput.ReadBool(normalized, "secondary_hit_success"),
                DictStringName(normalized, "skill_id"),
                ReadSaveRollOverrides(normalized)
            );
        }

        private static IReadOnlyList<int> ReadSaveRollOverrides(GDictionary payload)
        {
            if (payload == null)
            {
                return Array.Empty<int>();
            }
            if (payload.ContainsKey("save_roll_override"))
            {
                return new[] { Math.Clamp(DictInt(payload, "save_roll_override"), 1, 20) };
            }

            GArray rawRolls = GetArray(payload, "save_roll_overrides");
            if (rawRolls.Count == 0)
            {
                return Array.Empty<int>();
            }
            int[] rolls = new int[rawRolls.Count];
            for (int index = 0; index < rawRolls.Count; index++)
            {
                rolls[index] = Math.Clamp(rawRolls[index].AsInt32(), 1, 20);
            }
            return rolls;
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

        internal static SpellControlCheckContext FromDictionary(GDictionary payload)
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

        internal GDictionary ToDictionary() => Payload?.Duplicate(true) ?? new GDictionary();
    }

    private readonly record struct TraitTriggerResultSnapshot(
        GDictionary Payload,
        bool Triggered,
        int ExtraWeaponDiceCount,
        int ExtraWeaponDiceSides,
        int ClampToHp
    )
    {
        internal static TraitTriggerResultSnapshot FromAttackTraitTriggerResult(
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

        internal GDictionary ToDictionary() => Payload?.Duplicate(true) ?? new GDictionary();
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

        internal GDictionary ToDictionary() => Payload?.Duplicate(true) ?? new GDictionary();
    }

    private readonly record struct DamageDiceEventSnapshot(
        bool DamageDiceHighTotalRoll,
        bool SkillDamageDiceIsMax,
        bool WeaponDamageDiceIsMax
    )
    {
        public static DamageDiceEventSnapshot Empty => new(false, false, false);

        internal static DamageDiceEventSnapshot FromDictionary(GDictionary payload)
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

    private readonly Dictionary<StringName, SkillDef> _skillDefIndex = new();
    private readonly List<BattleSkillMasteryGrant> _last_stand_mastery_records = new();
    private readonly BattleFateEventBus _fate_event_bus = new();
    private readonly BattleReportFormatter _report_formatter = new();
    private readonly TraitTriggerHooks _trait_trigger_hooks = new();
    private BattleHitResolver _hit_resolver = new();
    private bool _suppress_last_stand_mastery_records;

    internal static BattleDamagePreviewRollMode ToDamagePreviewRollMode(StringName value)
    {
        if (value == DamagePreviewRollModeRandom)
            return BattleDamagePreviewRollMode.Random;
        if (value == DamagePreviewRollModeAverage)
            return BattleDamagePreviewRollMode.Average;
        if (value == DamagePreviewRollModeMaximum)
            return BattleDamagePreviewRollMode.Maximum;
        return BattleDamagePreviewRollMode.Unknown;
    }

    internal static StringName ToStringName(BattleDamagePreviewRollMode mode)
    {
        return mode switch
        {
            BattleDamagePreviewRollMode.Random => DamagePreviewRollModeRandom,
            BattleDamagePreviewRollMode.Average => DamagePreviewRollModeAverage,
            BattleDamagePreviewRollMode.Maximum => DamagePreviewRollModeMaximum,
            _ => "",
        };
    }

    internal static BattleDamagePreviewSaveMode ToDamagePreviewSaveMode(StringName value)
    {
        if (value == DamagePreviewSaveModeExpected)
            return BattleDamagePreviewSaveMode.Expected;
        if (value == DamagePreviewSaveModeWorst)
            return BattleDamagePreviewSaveMode.Worst;
        return BattleDamagePreviewSaveMode.Unknown;
    }

    internal static StringName ToStringName(BattleDamagePreviewSaveMode mode)
    {
        return mode switch
        {
            BattleDamagePreviewSaveMode.Expected => DamagePreviewSaveModeExpected,
            BattleDamagePreviewSaveMode.Worst => DamagePreviewSaveModeWorst,
            _ => "",
        };
    }

    internal void SetSkillDefs(IReadOnlyDictionary<StringName, SkillDef> skill_defs)
    {
        _skillDefIndex.Clear();
        if (skill_defs == null || skill_defs.Count == 0)
        {
            return;
        }

        foreach ((StringName skillId, SkillDef skillDef) in skill_defs)
        {
            if (skillId == "" || skillDef == null)
            {
                continue;
            }
            _skillDefIndex[skillId] = skillDef;
        }
    }

    internal List<BattleSkillMasteryGrant> GetAndClearLastStandMasteryRecordsTyped()
    {
        List<BattleSkillMasteryGrant> records = new(_last_stand_mastery_records);
        _last_stand_mastery_records.Clear();
        return records;
    }

    public void SetHitResolver(BattleHitResolver hit_resolver)
    {
        _hit_resolver = hit_resolver ?? new BattleHitResolver();
    }

    internal BattleFateEventBus GetFateEventBus()
    {
        return _fate_event_bus;
    }

    internal GDictionary ResolveSkillResult(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDef skillDef
    )
    {
        if (sourceUnit == null || targetUnit == null || skillDef?.combat_profile == null)
        {
            return BuildEmptyResult();
        }
        return ResolveEffects(
            sourceUnit,
            targetUnit,
            ToValueArray(skillDef.combat_profile.effect_defs),
            new GDictionary { ["skill_id"] = skillDef.skill_id }
        );
    }

    internal virtual GDictionary ResolveAttackEffects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        GArray effect_defs,
        AttackCheckInput attack_check
    )
    {
        return ResolveAttackEffects(
            source_unit,
            target_unit,
            effect_defs,
            attack_check,
            new AttackContext()
        );
    }

    internal virtual GDictionary ResolveAttackEffects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        IEnumerable<CombatEffectDef> effect_defs,
        AttackCheckInput attack_check
    )
    {
        return ResolveAttackEffects(
            source_unit,
            target_unit,
            effect_defs,
            attack_check,
            new AttackContext()
        );
    }

    internal virtual GDictionary ResolveAttackEffects(
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
                && effectDef.TriggerEventKind == CombatEffectTriggerEvent.SecondaryHit
            )
            {
                secondaryHitDcBase = effectDef.secondary_hit_dc_base > 0
                    ? effectDef.secondary_hit_dc_base
                    : 10;
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
            ResolveEffects(source_unit, target_unit, resolvedEffectDefs, attackEffectContext),
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

    internal virtual GDictionary ResolveAttackEffects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        IEnumerable<CombatEffectDef> effect_defs,
        AttackCheckInput attack_check,
        AttackContext attack_context = null
    )
    {
        return ResolveAttackEffects(
            source_unit,
            target_unit,
            ToValueArray(effect_defs),
            attack_check,
            attack_context
        );
    }

    internal virtual AttackEffectResolutionResult ResolveAttackEffectsTyped(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        IEnumerable<CombatEffectDef> effect_defs,
        AttackCheckInput attack_check,
        AttackContext attack_context = null
    )
    {
        GDictionary payload = ResolveAttackEffects(
            source_unit,
            target_unit,
            effect_defs,
            attack_check,
            attack_context
        );
        return AttackEffectResolutionResultReader.ReadResolverResult(payload, attack_check);
    }

    internal BattleSpellControlMetadata ResolveSpellControlCheckTyped(
        BattleUnitState source_unit,
        GDictionary attack_context = null
    )
    {
        return ResolveSpellControlCheck(
            source_unit,
            SpellControlCheckContext.FromDictionary(attack_context)
        );
    }

    internal BattleSpellControlMetadata ResolveSpellControlCheckTyped(
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

    internal virtual GDictionary PreviewDamageEffect(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        CombatEffectDef effect_def,
        GDictionary damage_context = null,
        BattleDamagePreviewRollMode roll_mode = BattleDamagePreviewRollMode.Average,
        BattleDamagePreviewSaveMode save_mode = BattleDamagePreviewSaveMode.Expected
    )
    {
        return PreviewDamageEffectTyped(
            source_unit,
            target_unit,
            effect_def,
            damage_context,
            roll_mode,
            save_mode
        ).ToDictionary();
    }

    internal virtual BattleDamagePreviewResult PreviewDamageEffectTyped(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        CombatEffectDef effect_def,
        StringName skill_id,
        BattleDamagePreviewRollMode roll_mode = BattleDamagePreviewRollMode.Average,
        BattleDamagePreviewSaveMode save_mode = BattleDamagePreviewSaveMode.Expected
    )
    {
        return PreviewDamageEffectTyped(
            source_unit,
            target_unit,
            effect_def,
            BuildPreviewDamageContext(skill_id),
            roll_mode,
            save_mode
        );
    }

    internal virtual BattleDamagePreviewResult PreviewDamageEffectTyped(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        CombatEffectDef effect_def,
        GDictionary damage_context = null,
        BattleDamagePreviewRollMode roll_mode = BattleDamagePreviewRollMode.Average,
        BattleDamagePreviewSaveMode save_mode = BattleDamagePreviewSaveMode.Expected
    )
    {
        if (source_unit == null || target_unit == null || effect_def == null)
        {
            return BattleDamagePreviewResult.Empty();
        }
        BattleDamagePreviewRollMode resolvedRollMode =
            roll_mode == BattleDamagePreviewRollMode.Unknown
                ? BattleDamagePreviewRollMode.Average
                : roll_mode;
        BattleDamagePreviewSaveMode resolvedSaveMode =
            save_mode == BattleDamagePreviewSaveMode.Unknown
                ? BattleDamagePreviewSaveMode.Expected
                : save_mode;
        StringName resolvedRollModeName = ToStringName(resolvedRollMode);
        StringName resolvedSaveModeName = ToStringName(resolvedSaveMode);
        BattleUnitState sourcePreview = source_unit.clone();
        BattleUnitState targetPreview = target_unit.clone();
        if (sourcePreview == null || targetPreview == null)
        {
            return BattleDamagePreviewResult.Empty();
        }

        GDictionary previewContext = DuplicateDictionary(damage_context);
        previewContext["damage_roll_mode"] = resolvedRollModeName;
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
                rollMode: resolvedRollModeName,
                saveMode: resolvedSaveModeName,
                shieldHpBefore: target_unit.current_shield_hp,
                shieldHpAfter: targetPreview.current_shield_hp,
                errorCode: damageOutcome.ErrorCode,
                damageOutcome: damageOutcome.ToDictionary(),
                damageResult: new GDictionary(),
                saveEstimate: BattleDamagePreviewSaveEstimate.None(0),
                diagnostics: new List<object>
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
            rollMode: resolvedRollModeName,
            saveMode: resolvedSaveModeName,
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

    internal virtual BattleDamagePreviewResult preview_damage_sequence_typed(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        GArray effect_defs,
        StringName skill_id,
        BattleDamagePreviewRollMode roll_mode = BattleDamagePreviewRollMode.Average,
        BattleDamagePreviewSaveMode save_mode = BattleDamagePreviewSaveMode.Expected
    )
    {
        return preview_damage_sequence_typed(
            source_unit,
            target_unit,
            effect_defs,
            BuildPreviewDamageContext(skill_id),
            BuildPreviewOptions(roll_mode, save_mode)
        );
    }

    internal virtual BattleDamagePreviewResult preview_damage_sequence_typed(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        GArray effect_defs,
        GDictionary damage_context = null,
        BattleDamagePreviewOptions options = null
    )
    {
        if (source_unit == null || target_unit == null)
        {
            return BattleDamagePreviewResult.Empty();
        }

        options ??= new BattleDamagePreviewOptions();
        BattleDamagePreviewRollMode rollMode =
            options.RollMode == BattleDamagePreviewRollMode.Unknown
                ? BattleDamagePreviewRollMode.Average
                : options.RollMode;
        BattleDamagePreviewSaveMode saveMode =
            options.SaveMode == BattleDamagePreviewSaveMode.Unknown
                ? BattleDamagePreviewSaveMode.Expected
                : options.SaveMode;
        StringName rollModeName = ToStringName(rollMode);
        StringName saveModeName = ToStringName(saveMode);
        GDictionary previewContext = DuplicateDictionary(damage_context);
        previewContext["damage_roll_mode"] = rollModeName;
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
        var damageEvents = new List<object>();
        var diagnostics = new List<object>();
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
                if (effectDef.EffectKind != BattleEffectKind.Damage)
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
                if (saveMode == BattleDamagePreviewSaveMode.Expected && saveEstimate.HasSave)
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
            rollMode: rollModeName,
            saveMode: saveModeName,
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

    private static GDictionary BuildPreviewDamageContext(StringName skillId)
    {
        var context = new GDictionary();
        if (!IsEmpty(skillId))
        {
            context["skill_id"] = skillId;
        }
        return context;
    }

    private static BattleDamagePreviewOptions BuildPreviewOptions(
        BattleDamagePreviewRollMode rollMode,
        BattleDamagePreviewSaveMode saveMode
    )
    {
        return new BattleDamagePreviewOptions(rollMode, saveMode);
    }

    internal virtual GDictionary ResolveEffects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        GArray effect_defs
    )
    {
        return ResolveEffects(source_unit, target_unit, effect_defs, new GDictionary());
    }

    internal virtual GDictionary ResolveEffects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        IEnumerable<CombatEffectDef> effect_defs
    )
    {
        return ResolveEffects(source_unit, target_unit, effect_defs, new GDictionary());
    }

    internal virtual GDictionary ResolveEffects(
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
        BattleSaveContext saveContext = contextFlags.ToBattleSaveContext();
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

            BattleEffectKind effectKind = effectDef.EffectKind;
            if (effectKind == BattleEffectKind.Damage)
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
                BattleSaveResult damageSaveResult = BattleSaveResolver.ResolveSaveResult(
                    source_unit,
                    target_unit,
                    effectDef,
                    saveContext
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
            else if (effectKind == BattleEffectKind.EquipmentDurabilityDamage)
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
            else if (effectKind == BattleEffectKind.Heal)
            {
                int healAmount = ResolveHealAmount(source_unit, effectDef);
                healAmount = BattleStatusModifierRules.ApplyHealMultiplier(target_unit, healAmount);
                if (healAmount > 0)
                {
                    ApplyHealing(target_unit, healAmount);
                }
                totalHealing += healAmount;
                applied = true;
            }
            else if (effectKind == BattleEffectKind.StaminaRestore)
            {
                ApplyStaminaRestore(source_unit, target_unit, effectDef);
                applied = true;
            }
            else if (effectKind == BattleEffectKind.HealFatal)
            {
                int healAmount = ResolveHealFatalAmount(target_unit, effectDef);
                if (healAmount > 0)
                {
                    ApplyHealing(target_unit, healAmount);
                    totalHealing += healAmount;
                    applied = true;
                }
            }
            else if (effectKind == BattleEffectKind.EraseStatus)
            {
                if (BattleTemporalStatusService.IsTemporalReleaseEffect(effectDef))
                {
                    List<StringName> releasedStatusIds =
                        BattleTemporalStatusService.ApplyTemporalReleaseEffects(
                            source_unit,
                            target_unit,
                            effectDef
                        );
                    foreach (StringName releasedStatusId in releasedStatusIds)
                    {
                        if (!removedStatusEffectIds.Contains(releasedStatusId))
                        {
                            removedStatusEffectIds.Add(releasedStatusId);
                        }
                        applied = true;
                    }
                }
                else
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
                    if (erasedStatusId != "" && target_unit.HasStatusEffect(erasedStatusId))
                    {
                        target_unit.EraseStatusEffect(erasedStatusId);
                        applied = true;
                    }
                }
            }
            else if (effectKind == BattleEffectKind.CleanseHarmful)
            {
                GStringNameArray removedStatusIds = new();
                foreach (StringName statusId in target_unit.GetSortedStatusEffectIdsTyped())
                {
                    if (BattleStatusSemanticTable.IsCleansableHarmfulStatus(statusId))
                    {
                        removedStatusIds.Add(statusId);
                    }
                }
                foreach (StringName statusId in removedStatusIds)
                {
                    target_unit.EraseStatusEffect(statusId);
                }
                if (removedStatusIds.Count > 0)
                {
                    applied = true;
                }
            }
            else if (effectKind == BattleEffectKind.DispelMagic)
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
            else if (
                effectKind == BattleEffectKind.Status
                || effectKind == BattleEffectKind.ApplyStatus
            )
            {
                BattleSaveResult statusSaveResult = BattleSaveResolver.ResolveSaveResult(
                    source_unit,
                    target_unit,
                    effectDef,
                    saveContext
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
                resolvedStatusId = BattleTemporalStatusService.ApplyEliteBossStasisDowngrade(
                    target_unit,
                    resolvedStatusId
                );
                if (
                    resolvedStatusId != ""
                    && ApplyStatusEffect(target_unit, source_unit, effectDef, resolvedStatusId)
                )
                {
                    AddUnique(statusEffectIds, resolvedStatusId);
                    applied = true;
                }
            }
            else if (
                effectKind == BattleEffectKind.Terrain
                || effectKind == BattleEffectKind.TerrainEffect
            )
            {
                if (effectDef.terrain_effect_id != "")
                {
                    AddUnique(terrainEffectIds, effectDef.terrain_effect_id);
                    applied = true;
                }
            }
            else if (
                effectKind == BattleEffectKind.Height
                || effectKind == BattleEffectKind.HeightDelta
            )
            {
                if (effectDef.height_delta != 0)
                {
                    totalHeightDelta += effectDef.height_delta;
                    applied = true;
                }
            }
            else if (effectKind == BattleEffectKind.Execute)
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
            sourceStatusEffectIds.Add(LowLuckRelicRules.ToStringName(LowLuckRelicStatusKind.BlackStarWedgeExposed));
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

    internal virtual GDictionary ResolveEffects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        IEnumerable<CombatEffectDef> effect_defs,
        GDictionary damage_context = null
    )
    {
        return ResolveEffects(
            source_unit,
            target_unit,
            ToValueArray(effect_defs),
            damage_context
        );
    }

    internal AttackEffectResolutionResult ResolveFallDamageResult(
        BattleUnitState targetUnit,
        int fallLayers
    )
    {
        if (targetUnit == null || fallLayers <= 0 || !targetUnit.is_alive)
        {
            return AttackEffectResolutionResultReader.ReadResolverResult(
                BuildEmptyResult(),
                new AttackCheckInput()
            );
        }
        int maxHp = GetAttributeValue(targetUnit, AttributeService.ToStringName(AttributeIdKind.HpMax));
        if (maxHp <= 0)
        {
            maxHp = Math.Max(targetUnit.current_hp, 1);
        }
        int damagePerLayer = Math.Max((maxHp + 19) / 20, 1);
        AppliedDamageResult damageResult = ApplyDamageToTargetResult(
            targetUnit,
            damagePerLayer * fallLayers
        );
        targetUnit.is_alive = targetUnit.current_hp > 0;
        return AttackEffectResolutionResultReader.ReadResolverResult(
            BuildEnvironmentalDamageResult(damageResult),
            new AttackCheckInput()
        );
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

    internal int ApplyDirectDamageToTargetTyped(
        BattleUnitState targetUnit,
        int rawDamage,
        BattleUnitState sourceUnit = null
    )
    {
        return ApplyDamageToTargetResult(targetUnit, rawDamage, sourceUnit).Damage;
    }

    internal int ApplyDirectDamageToTargetTyped(
        BattleUnitState targetUnit,
        GDictionary resolvedDamageInput,
        BattleUnitState sourceUnit = null
    )
    {
        return ApplyDamageToTargetResult(targetUnit, resolvedDamageInput, sourceUnit).Damage;
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
        return effectDef.TriggerEventKind switch
        {
            CombatEffectTriggerEvent.None => true,
            CombatEffectTriggerEvent.CriticalHit => context.CriticalHit,
            CombatEffectTriggerEvent.OrdinaryHit => context.AttackSuccess && !context.CriticalHit,
            CombatEffectTriggerEvent.SecondaryHit => context.SecondaryHitSuccess,
            _ => false,
        };
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
        int strMod = GetUnitBaseAttributeModifier(source_unit, UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Strength));
        int conMod = GetUnitBaseAttributeModifier(target_unit, UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Constitution));
        int dc = dc_base + strMod;
        _hit_resolver ??= new BattleHitResolver();
        int saveRoll = _hit_resolver.RollAttackDie(
            20,
            false,
            attack_context ?? new AttackContext()
        );
        int saveBonus = GetTargetSecondaryHitSaveBonus(target_unit);
        return saveRoll + conMod + saveBonus < dc;
    }

    public virtual int _roll_damage_die(int dice_sides)
    {
        return TrueRandomSeedService.RandiRange(1, Math.Max(dice_sides, 1));
    }

    private static int GetIntParam(GDictionary @params, StringName key, int fallback = 0)
    {
        if (@params == null || !@params.ContainsKey(key))
            return fallback;
        return (int)@params[key];
    }

    private static int GetIntParam(
        IReadOnlyDictionary<string, object> @params,
        StringName key,
        int fallback = 0
    )
    {
        if (!TryGetStatusParamTyped(@params, key, out object rawValue))
            return fallback;
        return rawValue switch
        {
            int intValue => intValue,
            long longValue => (int)longValue,
            _ => fallback,
        };
    }

    private static double GetFloatParam(GDictionary @params, StringName key, double fallback = 0.0)
    {
        if (@params == null || !@params.ContainsKey(key))
            return fallback;
        return (double)@params[key];
    }

    private static double GetFloatParam(
        IReadOnlyDictionary<string, object> @params,
        StringName key,
        double fallback = 0.0
    )
    {
        if (!TryGetStatusParamTyped(@params, key, out object rawValue))
            return fallback;
        return rawValue switch
        {
            float floatValue => floatValue,
            double doubleValue => doubleValue,
            int intValue => intValue,
            long longValue => longValue,
            _ => fallback,
        };
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

    private static StringName GetStringNameParam(
        IReadOnlyDictionary<string, object> @params,
        StringName key,
        StringName fallback = default
    )
    {
        if (!TryGetStatusParamTyped(@params, key, out object rawValue))
            return fallback ?? new StringName("");
        StringName normalized = ProgressionDataUtils.to_string_name(rawValue);
        return normalized != "" ? normalized : fallback ?? new StringName("");
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

    private static IReadOnlyList<object> GetArrayParam(
        IReadOnlyDictionary<string, object> @params,
        StringName key
    )
    {
        if (!TryGetStatusParamTyped(@params, key, out object rawValue))
            return Array.Empty<object>();
        return rawValue as IReadOnlyList<object> ?? Array.Empty<object>();
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
        targetUnit.NormalizeShieldState();

        int shieldAbsorbed = 0;
        bool shieldBroken = false;
        if (!bypassShield && targetUnit.HasShield() && shieldEfficiency > 0.0)
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
                targetUnit.ClearShield();
            }
            else
            {
                targetUnit.NormalizeShieldState();
            }
        }

        int hpDamage = Math.Max(normalizedDamage - shieldAbsorbed, 0);
        if (hpDamage > 0)
        {
            int maxHp = GetAttributeValue(targetUnit, AttributeService.ToStringName(AttributeIdKind.HpMax));
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
                    else if (targetUnit.HasStatusEffect("death_ward"))
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
        BattleDamagePreviewSaveMode saveMode
    )
    {
        BattleSaveProbabilityResult probability =
            BattleSaveResolver.EstimateSaveSuccessProbabilityResult(
                sourceUnit,
                targetUnit,
                effectDef,
                DamageResolutionContext.FromDictionary(damageContext).ToBattleSaveContext()
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
        int damageAfterSave =
            saveMode == BattleDamagePreviewSaveMode.Worst ? worstDamage : expectedDamage;
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
        if (sourceUnit != null && sourceUnit.HasStatusEffect(StatusArcherPreAim))
        {
            multiplier *= 1.15;
        }
        if (targetUnit != null && targetUnit.HasStatusEffect(StatusMarked))
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
        return DamageTagContentRules.ToDamageTagKind(explicitEffectTag) != DamageTagKind.Unknown
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
        return DamageTagContentRules.IsPhysicalDamageTag(
            DamageTagContentRules.ToDamageTagKind(damageTag)
        )
            ? damageTag
            : new StringName("");
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
                new AttackTraitTriggerResult(
                    @event: TraitTriggerContentRules.ToStringName(TraitTriggerKind.OnCrit)
                )
            );
        }
        return TraitTriggerResultSnapshot.FromAttackTraitTriggerResult(
            _trait_trigger_hooks.OnCrit(
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
            _trait_trigger_hooks.OnFatalDamage(
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



    private int GetUnitBaseAttributeModifier(BattleUnitState unitState, StringName attributeId)
    {
        if (unitState?.attribute_snapshot == null || attributeId == "")
        {
            return 0;
        }
        StringName modifierId = AttributeSnapshot.GetBaseAttributeModifierId(attributeId);
        return modifierId == "" ? 0 : GetAttributeValue(unitState, modifierId);
    }

    private int GetTargetSecondaryHitSaveBonus(BattleUnitState targetUnit)
    {
        if (targetUnit == null)
        {
            return 0;
        }
        int bonus = 0;
        foreach (StringName statusId in targetUnit.GetSortedStatusEffectIdsTyped())
        {
            BattleStatusEffectState statusEntry = targetUnit.GetStatusEffect(statusId);
            if (statusEntry == null)
            {
                continue;
            }
            bonus = Math.Max(
                bonus,
                statusEntry.control_save_bonus
            );
        }
        return bonus;
    }

    private static int GetAttributeValue(BattleUnitState unitState, StringName attributeId)
    {
        return unitState?.attribute_snapshot != null
            ? unitState.attribute_snapshot.GetValue(attributeId)
            : 0;
    }

}
