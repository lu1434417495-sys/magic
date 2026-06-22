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

public partial class BattleDamageResolver : IDisposable
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
        bool RequireDamageApplied
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
                effectDef?.require_damage_applied ?? false
            );
        }
    }

    public void Dispose()
    {
        _fate_event_bus.Dispose();
    }

    private readonly record struct EquipmentDurabilitySaveResolution(
        SaveResolutionResult Result,
        bool HasSave,
        bool Success
    );

    private readonly record struct EquipmentDurabilityDamageEffectResult(
        EquipmentDurabilityEventResult Event,
        bool HasEvent,
        int DurabilityLoss,
        bool Destroyed,
        EquipmentDurabilitySaveResolution SaveResult
    )
    {
        public static EquipmentDurabilityDamageEffectResult Empty => new(
            new EquipmentDurabilityEventResult(),
            false,
            0,
            false,
            default
        );
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
        DamageEventResult Event,
        int Damage,
        int HpDamage,
        int ShieldAbsorbed,
        bool ShieldBroken,
        bool LowLuckBlackStarWedgeTriggered,
        DamageDiceEventSnapshot DamageDiceEvent
    )
    {
        public bool HasAppliedDamage => Damage > 0 || ShieldAbsorbed > 0;

        public AppliedDamageResult WithHpDamage(int hpDamage)
        {
            int normalizedHpDamage = Math.Max(hpDamage, 0);
            DamageEventResult @event = Event;
            @event.Damage = normalizedHpDamage;
            @event.HpDamage = normalizedHpDamage;
            @event.FullyAbsorbedByShield = normalizedHpDamage <= 0 && ShieldAbsorbed > 0;
            return new AppliedDamageResult(
                @event,
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
        DamageEventResult Event,
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
        public DamageOutcomeResult WithResolvedDamage(int resolvedDamage)
        {
            int normalizedDamage = Math.Max(resolvedDamage, 0);
            DamageEventResult @event = Event;
            @event.ResolvedDamage = normalizedDamage;
            return this with { Event = @event, ResolvedDamage = normalizedDamage };
        }

        public DamageApplicationInput ToDamageApplicationInput()
        {
            return new DamageApplicationInput(
                Event,
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
        DamageEventResult Event,
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
            new DamageEventResult(),
            0,
            false,
            false,
            100.0,
            0,
            false,
            DamageDiceEventSnapshot.Empty
        );

        public static DamageApplicationInput Create(
            DamageEventResult @event,
            int resolvedDamage,
            bool bypassShield = false,
            bool bypassDeathPrevention = false,
            double shieldAbsorptionPercent = 100.0,
            int minHpAfterDamage = 0,
            bool lowLuckBlackStarWedgeTriggered = false,
            DamageDiceEventSnapshot damageDiceEvent = default
        )
        {
            @event.ResolvedDamage = Math.Max(resolvedDamage, 0);
            @event.BypassShield = bypassShield;
            @event.BypassDeathPrevention = bypassDeathPrevention;
            @event.ShieldAbsorptionPercent = shieldAbsorptionPercent;
            @event.MinHpAfterDamage = Math.Max(minHpAfterDamage, 0);
            @event.LowLuckBlackStarWedgeTriggered = lowLuckBlackStarWedgeTriggered;
            return new DamageApplicationInput(
                @event,
                Math.Max(resolvedDamage, 0),
                bypassShield,
                bypassDeathPrevention,
                shieldAbsorptionPercent,
                Math.Max(minHpAfterDamage, 0),
                lowLuckBlackStarWedgeTriggered,
                damageDiceEvent
            );
        }

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
            return Create(
                AttackEffectResolutionResultReader.ReadDamageEventPayload(payload),
                resolvedDamage,
                bypassShield,
                bypassDeathPrevention,
                shieldAbsorptionPercent,
                minHpAfterDamage,
                lowLuckBlackStarWedgeTriggered,
                damageDiceEvent
            );
        }

        internal static DamageApplicationInput FromDictionary(GDictionary payload)
        {
            GDictionary normalized = payload ?? new GDictionary();
            DamageEventResult @event =
                AttackEffectResolutionResultReader.ReadDamageEventPayload(normalized);
            return new DamageApplicationInput(
                @event,
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
        bool Applied,
        int ExecuteStage,
        StringName ExecuteOutcome,
        IReadOnlyList<AppliedDamageResult> DamageResults
    )
    {
        public static ExecuteEffectResult Empty => new(
            false,
            -1,
            "",
            Array.Empty<AppliedDamageResult>()
        );
    }

    private readonly record struct GradedSaveExecuteEffectResult(
        bool Applied,
        IReadOnlyList<AppliedDamageResult> DamageResults,
        IReadOnlyList<ResolutionDiagnostic> Diagnostics
    )
    {
        public static GradedSaveExecuteEffectResult Empty => new(
            false,
            Array.Empty<AppliedDamageResult>(),
            Array.Empty<ResolutionDiagnostic>()
        );
    }

    private readonly record struct TraitTriggerResultSnapshot(
        TraitTriggerEventResult Event,
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
                return new TraitTriggerResultSnapshot(
                    new TraitTriggerEventResult(),
                    false,
                    0,
                    0,
                    0
                );
            }
            TraitTriggerEventResult @event = new()
            {
                Triggered = true,
                Event = result.Event,
                TraitId = result.TraitId,
                EffectType = result.EffectType,
                OriginalRoll = result.OriginalRoll,
                RerollDie = result.RerollDie,
                RerolledRoll = result.RerolledRoll,
                DieSize = result.DieSize,
                ExtraWeaponDiceCount = Math.Max(result.ExtraWeaponDiceCount, 0),
                ExtraWeaponDiceSides = Math.Max(result.ExtraWeaponDiceSides, 0),
                ClampToHp = Math.Max(result.ClampToHp, 0),
                ProjectedHp = result.ProjectedHp,
                HpDamage = Math.Max(result.HpDamage, 0),
                ChargeKey = result.ChargeKey,
                ChargesRemaining = Math.Max(result.ChargesRemaining, 0),
            };
            return new TraitTriggerResultSnapshot(
                @event,
                true,
                Math.Max(result.ExtraWeaponDiceCount, 0),
                Math.Max(result.ExtraWeaponDiceSides, 0),
                Math.Max(result.ClampToHp, 0)
            );
        }

        internal TraitTriggerEventResult ToEventResult() => Event;
    }

    private readonly record struct DicePoolRollResult(
        int Count,
        int Sides,
        int[] Rolls,
        int Total,
        int Bonus,
        int MaxTotal,
        bool IsMax
    )
    {
        public static DicePoolRollResult Empty => new(
            0,
            0,
            Array.Empty<int>(),
            0,
            0,
            0,
            false
        );

        public bool HasDice => Count > 0 && Sides > 0;

        public int TotalWithBonus => Total + Bonus;

        internal DamageDiceRollDetail ToDamageDiceRollDetail() =>
            new()
            {
                Count = Count,
                Sides = Sides,
                Rolls = Rolls ?? Array.Empty<int>(),
                Total = Total,
                Bonus = Bonus,
                MaxTotal = MaxTotal,
                IsMax = IsMax,
            };
    }

    private readonly record struct DamageDiceEventSnapshot(
        bool DamageDiceHighTotalRoll,
        StringName DamageDiceHighTotalRollReason,
        bool SkillDamageDiceIsMax,
        DamageDiceMaxReasonKind SkillDamageDiceIsMaxReason,
        bool WeaponDamageDiceIsMax,
        DamageDiceMaxReasonKind WeaponDamageDiceIsMaxReason
    )
    {
        public static DamageDiceEventSnapshot Empty => new(
            false,
            "",
            false,
            DamageDiceMaxReasonKind.None,
            false,
            DamageDiceMaxReasonKind.None
        );

        internal static DamageDiceEventSnapshot FromDictionary(GDictionary payload)
        {
            GDictionary normalized = EnsureDamageDiceEventDefaults(payload);
            return new DamageDiceEventSnapshot(
                ReadDamageDiceFlag(normalized, "damage_dice_high_total_roll"),
                DictStringName(normalized, "damage_dice_high_total_roll_reason"),
                ReadDamageDiceFlag(normalized, "skill_damage_dice_is_max"),
                AttackEffectResolutionResultReader.ParseDamageDiceMaxReason(
                    DictStringName(normalized, "skill_damage_dice_is_max_reason")
                ),
                ReadDamageDiceFlag(normalized, "weapon_damage_dice_is_max"),
                AttackEffectResolutionResultReader.ParseDamageDiceMaxReason(
                    DictStringName(normalized, "weapon_damage_dice_is_max_reason")
                )
            );
        }

        private static bool ReadDamageDiceFlag(GDictionary payload, string key)
        {
            return TryGet(payload, key, out Variant value)
                && value.VariantType == Variant.Type.Bool
                && value.AsBool();
        }
    }

    private readonly record struct DamageDiceEventFlags(DamageDiceEventSnapshot Snapshot);

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

    internal AttackEffectResolutionResult ResolveSkillResult(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDef skillDef
    )
    {
        if (sourceUnit == null || targetUnit == null || skillDef?.combat_profile == null)
        {
            return BuildEmptyResolutionResult(skillDef?.skill_id ?? new StringName(""));
        }
        return ResolveEffects(
            sourceUnit,
            targetUnit,
            ToValueArray(skillDef.combat_profile.effect_defs),
            new GDictionary { ["skill_id"] = skillDef.skill_id }
        );
    }

    internal virtual AttackEffectResolutionResult ResolveAttackEffects(
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

    internal virtual AttackEffectResolutionResult ResolveAttackEffects(
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

    internal virtual AttackEffectResolutionResult ResolveAttackEffects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        GArray effect_defs,
        AttackCheckInput attack_check,
        AttackContext attack_context = null
    )
    {
        if (source_unit == null || target_unit == null)
        {
            AttackEffectResolutionResult emptyResult = ApplyAttackMetadataResult(
                BuildEmptyResolutionResult(attack_check.SkillId),
                new AttackResolutionMetadata()
            );
            emptyResult.AttackCheck = attack_check;
            return AttackEffectResolutionResultReader.FinalizeTypedResult(emptyResult);
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
            AttackEffectResolutionResult failedResult = ApplyAttackMetadataResult(
                BuildEmptyResolutionResult(attackMetadata.SkillId),
                attackMetadata
            );
            failedResult.AttackCheck = attack_check;
            failedResult = AttachAttackReportEntry(
                failedResult,
                source_unit,
                target_unit,
                attackMetadata
            );
            DispatchAttackResolutionEvents(
                source_unit,
                target_unit,
                attackMetadata,
                normalizedAttackContext
            );
            ClearComboStackOnMiss(source_unit);
            return AttackEffectResolutionResultReader.FinalizeTypedResult(failedResult);
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
        DamageResolutionContext attackEffectContext =
            DamageResolutionContext.FromDictionary(BuildAttackEffectContext(attackMetadata));

        AttackEffectResolutionResult resolvedResult = ApplyAttackMetadataResult(
            ResolveEffectsTypedCore(source_unit, target_unit, resolvedEffectDefs, attackEffectContext),
            attackMetadata
        );
        resolvedResult.AttackCheck = attack_check;
        resolvedResult = AttachAttackReportEntry(
            resolvedResult,
            source_unit,
            target_unit,
            attackMetadata
        );
        DispatchAttackResolutionEvents(
            source_unit,
            target_unit,
            attackMetadata,
            normalizedAttackContext
        );
        return AttackEffectResolutionResultReader.FinalizeTypedResult(resolvedResult);
    }

    internal virtual AttackEffectResolutionResult ResolveAttackEffects(
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
        return ResolveAttackEffects(
            source_unit,
            target_unit,
            effect_defs,
            attack_check,
            attack_context
        );
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
        return BattleDamagePreviewProjection.Project(
            PreviewDamageEffectTyped(
                source_unit,
                target_unit,
                effect_def,
                damage_context,
                roll_mode,
                save_mode
            )
        );
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
        return PreviewDamageEffectTyped(
            source_unit,
            target_unit,
            effect_def,
            DamageResolutionContext.FromDictionary(damage_context),
            roll_mode,
            save_mode
        );
    }

    internal virtual BattleDamagePreviewResult PreviewDamageEffectTyped(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        CombatEffectDef effect_def,
        DamageResolutionContext damage_context,
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

        DamageResolutionContext previewContextFlags =
            (damage_context ?? DamageResolutionContext.Empty()).WithDamageRollMode(
                resolvedRollModeName
            );
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
                damageOutcome: ProjectDamageOutcomePayload(damageOutcome),
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
            previewContextFlags,
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
            damageOutcome: ProjectDamageOutcomePayload(damageOutcome),
            damageResult: ProjectAppliedDamagePayload(damageResult),
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
        return preview_damage_sequence_typed(
            source_unit,
            target_unit,
            effect_defs,
            DamageResolutionContext.FromDictionary(damage_context),
            options
        );
    }

    internal virtual BattleDamagePreviewResult preview_damage_sequence_typed(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        GArray effect_defs,
        DamageResolutionContext damage_context,
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
        DamageResolutionContext previewContextFlags =
            (damage_context ?? DamageResolutionContext.Empty()).WithDamageRollMode(rollModeName);
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
                    previewContextFlags,
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
                damageEvents.Add(ProjectAppliedDamagePayload(damageResult));
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

    private static DamageResolutionContext BuildPreviewDamageContext(StringName skillId)
    {
        return DamageResolutionContext.ForSkill(skillId);
    }

    private static BattleDamagePreviewOptions BuildPreviewOptions(
        BattleDamagePreviewRollMode rollMode,
        BattleDamagePreviewSaveMode saveMode
    )
    {
        return new BattleDamagePreviewOptions(rollMode, saveMode);
    }

    internal virtual AttackEffectResolutionResult ResolveEffects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        GArray effect_defs
    )
    {
        return ResolveEffects(source_unit, target_unit, effect_defs, DamageResolutionContext.Empty());
    }

    internal virtual AttackEffectResolutionResult ResolveEffects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        IEnumerable<CombatEffectDef> effect_defs
    )
    {
        return ResolveEffects(
            source_unit,
            target_unit,
            ToValueArray(effect_defs),
            DamageResolutionContext.Empty()
        );
    }

    internal virtual AttackEffectResolutionResult ResolveEffects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        GArray effect_defs,
        GDictionary damage_context = null
    )
    {
        return ResolveEffects(
            source_unit,
            target_unit,
            effect_defs,
            DamageResolutionContext.FromDictionary(damage_context)
        );
    }

    internal virtual AttackEffectResolutionResult ResolveEffects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        GArray effect_defs,
        DamageResolutionContext damage_context
    )
    {
        return ResolveEffectsTypedCore(
            source_unit,
            target_unit,
            effect_defs,
            damage_context ?? DamageResolutionContext.Empty()
        );
    }

    private AttackEffectResolutionResult ResolveEffectsTypedCore(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        GArray effect_defs,
        DamageResolutionContext damage_context
    )
    {
        if (source_unit == null || target_unit == null)
        {
            return BuildEmptyResolutionResult();
        }

        GArray resolvedEffectDefs = CoerceEffectDefs(effect_defs);
        DamageResolutionContext contextFlags = damage_context ?? DamageResolutionContext.Empty();
        BattleSaveContext saveContext = contextFlags.ToBattleSaveContext();
        int totalDamage = 0;
        int totalHealing = 0;
        int totalShieldAbsorbed = 0;
        var damageEvents = new List<DamageEventResult>();
        var equipmentDurabilityEvents = new List<EquipmentDurabilityEventResult>();
        var dispelEvents = new List<DispelEventResult>();
        var statusEffectIds = new GStringNameArray();
        var removedStatusEffectIds = new GStringNameArray();
        var sourceStatusEffectIds = new GStringNameArray();
        var terrainEffectIds = new GStringNameArray();
        var saveResults = new List<SaveResolutionResult>();
        var diagnostics = new List<ResolutionDiagnostic>();
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
                        new ResolutionDiagnostic
                        {
                            ErrorCode = damageOutcome.ErrorCode,
                            Message = damageOutcome.Reason,
                        }
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
                    saveResults.Add(SaveResolutionFromBattleSave(damageSaveResult));
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
                damageEvents.Add(damageResult.Event);
                blackStarWedgeTriggered =
                    blackStarWedgeTriggered
                    || damageResult.LowLuckBlackStarWedgeTriggered;
                shieldBroken = shieldBroken || damageResult.ShieldBroken;
                applied = true;
                if (damageResult.HasAppliedDamage)
                {
                    GrantStatusOnHitToSource(source_unit, effectDef);
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
                    equipmentDurabilityEvents.Add(durabilityResult.Event);
                    if (durabilityResult.SaveResult.HasSave)
                    {
                        saveResults.Add(durabilityResult.SaveResult.Result);
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
                DispelEventResult dispelResult = ApplyDispelMagicEffect(
                    source_unit,
                    target_unit,
                    effectDef
                );
                GStringNameArray removedIds =
                    dispelResult.RemovedStatusIds ?? new GStringNameArray();
                if (removedIds.Count > 0)
                {
                    dispelEvents.Add(dispelResult);
                    foreach (StringName removedId in removedIds)
                    {
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
                    saveResults.Add(SaveResolutionFromBattleSave(statusSaveResult));
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
                    contextFlags,
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
                    damageEvents.Add(damageResult.Event);
                }
            }
            else if (effectKind == BattleEffectKind.GradedSaveExecute)
            {
                GradedSaveExecuteEffectResult gradedSaveResult =
                    ResolveGradedSaveExecuteEffect(
                        source_unit,
                        target_unit,
                        effectDef,
                        contextFlags,
                        statusEffectIds,
                        saveResults
                    );
                if (gradedSaveResult.Applied)
                {
                    applied = true;
                }
                foreach (ResolutionDiagnostic diagnostic in gradedSaveResult.Diagnostics)
                {
                    diagnostics.Add(diagnostic);
                }
                foreach (AppliedDamageResult damageResult in gradedSaveResult.DamageResults)
                {
                    totalDamage += damageResult.Damage;
                    totalShieldAbsorbed += damageResult.ShieldAbsorbed;
                    shieldBroken = shieldBroken || damageResult.ShieldBroken;
                    blackStarWedgeTriggered =
                        blackStarWedgeTriggered
                        || damageResult.LowLuckBlackStarWedgeTriggered;
                    damageEvents.Add(damageResult.Event);
                }
            }
        }

        target_unit.SetCurrentHp(target_unit.current_hp);
        if (
            blackStarWedgeTriggered
            && target_unit.is_alive
            && ApplyLowLuckBlackStarWedgeExposed(source_unit)
        )
        {
            sourceStatusEffectIds.Add(LowLuckRelicRules.ToStringName(LowLuckRelicStatusKind.BlackStarWedgeExposed));
        }

        string errorCode = diagnostics.Count > 0 ? diagnostics[0].ErrorCode : "";
        return AttackEffectResolutionResultReader.FinalizeTypedResult(
            new AttackEffectResolutionResult
            {
                Applied = applied,
                Damage = totalDamage,
                HpDamage = totalDamage,
                Healing = totalHealing,
                ShieldAbsorbed = totalShieldAbsorbed,
                ShieldBroken = shieldBroken,
                DamageEvents = damageEvents.ToArray(),
                EquipmentDurabilityEvents = equipmentDurabilityEvents.ToArray(),
                DispelEvents = dispelEvents.ToArray(),
                StatusEffectIds = statusEffectIds,
                RemovedStatusEffectIds = removedStatusEffectIds,
                SourceStatusEffectIds = sourceStatusEffectIds,
                TerrainEffectIds = terrainEffectIds,
                SaveResults = saveResults.ToArray(),
                HeightDelta = totalHeightDelta,
                Diagnostics = diagnostics.ToArray(),
                ExecuteStage = executeStage,
                ExecuteOutcome =
                    executeStage >= 0
                        ? AttackEffectResolutionResultReader.ParseExecuteOutcome(executeOutcome)
                        : ExecuteOutcomeKind.None,
                ErrorCode = errorCode,
                SkillId = contextFlags.SkillId,
                AttackCheck = new AttackCheckInput(skillId: contextFlags.SkillId),
                TraitTriggerResults = AttackEffectResolutionResultReader.ReadTraitTriggerResults(
                    contextFlags.RawContext,
                    "trait_trigger_results"
                ),
            }
        );
    }

    internal virtual AttackEffectResolutionResult ResolveEffects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        IEnumerable<CombatEffectDef> effect_defs,
        GDictionary damage_context = null
    )
    {
        return ResolveEffects(
            source_unit,
            target_unit,
            effect_defs,
            DamageResolutionContext.FromDictionary(damage_context)
        );
    }

    internal virtual AttackEffectResolutionResult ResolveEffects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        IEnumerable<CombatEffectDef> effect_defs,
        DamageResolutionContext damage_context
    )
    {
        return ResolveEffects(
            source_unit,
            target_unit,
            ToValueArray(effect_defs),
            damage_context ?? DamageResolutionContext.Empty()
        );
    }

    internal AttackEffectResolutionResult ResolveFallDamageResult(
        BattleUnitState targetUnit,
        int fallLayers
    )
    {
        if (targetUnit == null || fallLayers <= 0 || !targetUnit.is_alive)
        {
            return BuildEmptyResolutionResult();
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
        targetUnit.SetCurrentHp(targetUnit.current_hp);
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
        DamageEventResult damageOutcome = new()
        {
            DamageTag = "",
            MitigationTier = MitigationTierKind.Normal,
            MitigationSources = Array.Empty<MitigationSourceResult>(),
            BaseDamage = normalizedDamage,
            OffenseMultiplier = 1.0,
            BypassShield = false,
            ShieldAbsorptionPercent = 100.0,
            MinHpAfterDamage = 0,
            ResolvedDamage = normalizedDamage,
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

        FixedMitigationResult mitigation = BuildFixedMitigation(targetUnit, effectDef, damageTag);
        ApplyBlackStarBrandGuardIgnore(mitigation, targetUnit);
        bool lowLuckBlackStarWedgeTriggered = ApplyLowLuckBlackStarWedgeGuardIgnore(
            mitigation,
            sourceUnit
        );
        TrimFixedMitigationSources(mitigation);
        int buffReduction = mitigation.BuffReduction;
        int stanceReduction = mitigation.StanceReduction;
        int passiveReduction = mitigation.PassiveReduction;
        int contentDr = mitigation.ContentDr;
        int guardBlock = mitigation.GuardBlock;
        int guardIgnoreApplied = mitigation.GuardIgnoreApplied;
        int fixedMitigationTotal = mitigation.Total;
        int resolvedDamage = Math.Max(tierAdjustedDamage - fixedMitigationTotal, MinDamageFloor);
        DamageDiceEventFlags damageDiceEventFlags = BuildDamageDiceEventFlags(
            criticalHit,
            damageRoll,
            weaponRoll,
            bonusDamageRoll
        );

        DamageDiceEventSnapshot diceSnapshot = damageDiceEventFlags.Snapshot;
        DamageEventResult result = new()
        {
            DamageTag = damageTag,
            MitigationTier = AttackEffectResolutionResultReader.ParseMitigationTier(
                mitigationTier
            ),
            MitigationSources =
                AttackEffectResolutionResultReader.ReadMitigationSourcesFromArray(
                    GetArray(mitigationTierResult, "sources")
                ),
            BaseDamage = baseDamage,
            CriticalHit = criticalHit,
            AddWeaponDice = ShouldAddWeaponDice(effectDef),
            DamageDice = damageRoll.ToDamageDiceRollDetail(),
            BonusConditionMet = bonusConditionMet,
            BonusDamageDice = bonusDamageRoll.ToDamageDiceRollDetail(),
            WeaponDamageDice = weaponRoll.ToDamageDiceRollDetail(),
            CriticalExtraDamageDice = criticalDamageRoll.ToDamageDiceRollDetail(),
            CriticalExtraBonusDamageDice = criticalBonusDamageRoll.ToDamageDiceRollDetail(),
            CriticalExtraWeaponDamageDice = criticalWeaponRoll.ToDamageDiceRollDetail(),
            TraitExtraWeaponDamageDice = traitExtraWeaponRoll.ToDamageDiceRollDetail(),
            OffenseMultiplier = offenseMultiplier,
            RolledDamage = rolledDamage,
            TierAdjustedDamage = tierAdjustedDamage,
            ResolvedDamage = resolvedDamage,
            BuffReduction = buffReduction,
            StanceReduction = stanceReduction,
            PassiveReduction = passiveReduction,
            ContentDr = contentDr,
            GuardBlock = guardBlock,
            GuardIgnoreApplied = guardIgnoreApplied,
            FixedMitigationSourceLabels =
                mitigation.SourceLabels(),
            LowLuckBlackStarWedgeTriggered = lowLuckBlackStarWedgeTriggered,
            FixedMitigationTotal = fixedMitigationTotal,
            FullyAbsorbedByMitigation =
                resolvedDamage <= 0
                && mitigationTier != MitigationTierImmune
                && tierAdjustedDamage > 0,
            TraitTriggerResults = traitCritResult.Triggered
                ? new[] { traitCritResult.ToEventResult() }
                : Array.Empty<TraitTriggerEventResult>(),
            DamageDiceHighTotalRoll = diceSnapshot.DamageDiceHighTotalRoll,
            DamageDiceHighTotalRollReason = diceSnapshot.DamageDiceHighTotalRollReason,
            SkillDamageDiceIsMax = diceSnapshot.SkillDamageDiceIsMax,
            SkillDamageDiceIsMaxReason = diceSnapshot.SkillDamageDiceIsMaxReason,
            WeaponDamageDiceIsMax = diceSnapshot.WeaponDamageDiceIsMax,
            WeaponDamageDiceIsMaxReason = diceSnapshot.WeaponDamageDiceIsMaxReason,
        };
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
        DamageEventResult applicationEvent = damageInput.Event;
        int normalizedDamage = damageInput.ResolvedDamage;
        if (targetUnit == null || normalizedDamage <= 0)
        {
            return BuildAppliedDamageResult(
                damageInput with { Event = applicationEvent },
                0,
                0,
                false
            );
        }

        bool bypassShield = damageInput.BypassShield;
        bool bypassDeathPrevention = damageInput.BypassDeathPrevention;
        double shieldEfficiency = damageInput.ShieldAbsorptionPercent / 100.0;
        int minHpAfterDamage = damageInput.MinHpAfterDamage;
        int hpBeforeDamage = Math.Max(targetUnit.current_hp, 0);
        if (!bypassShield)
        {
            targetUnit.NormalizeShieldState();
        }

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
                    targetUnit.SetCurrentHp(Math.Min(
                        Math.Max(projectedHp, minHpAfterDamage),
                        targetUnit.current_hp
                    ));
                }
                else if (bypassDeathPrevention)
                {
                    targetUnit.MarkDead();
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
                        targetUnit.SetCurrentHp(Math.Max(
                            fatalTraitResult.ClampToHp,
                            1
                        ));
                        AppendTraitTriggerResult(ref applicationEvent, fatalTraitResult);
                    }
                    else if (TryGetBlockingDeathWard(targetUnit, applicationEvent, out _))
                    {
                        targetUnit.MarkDead();
                        if (!TriggerLastStand(targetUnit, sourceUnit))
                        {
                            targetUnit.MarkDead();
                        }
                    }
                    else
                    {
                        targetUnit.MarkDead();
                    }
                }
            }
            else
            {
                targetUnit.SetCurrentHp(projectedHp);
            }
        }

        int actualHpDamage = Math.Max(hpBeforeDamage - Math.Max(targetUnit.current_hp, 0), 0);
        return BuildAppliedDamageResult(
            damageInput with { Event = applicationEvent },
            actualHpDamage,
            shieldAbsorbed,
            shieldBroken
        );
    }

    private static bool TryGetBlockingDeathWard(
        BattleUnitState targetUnit,
        DamageEventResult damageEvent,
        out BattleStatusEffectState deathWardEntry
    )
    {
        deathWardEntry = targetUnit?.GetStatusEffect("death_ward");
        if (deathWardEntry == null)
        {
            return false;
        }
        DeathResolutionContext deathContext = ResolveDeathContext(damageEvent);
        int protectionPriority = ResolveDeathPreventionPriority(deathWardEntry);
        return BattleDeathResolutionRules.CanDeathPreventionBlock(
            deathContext,
            protectionPriority
        );
    }

    private static DeathResolutionContext ResolveDeathContext(DamageEventResult damageEvent)
    {
        StringName deathSource = ProgressionDataUtils.to_string_name(damageEvent.DeathSource);
        int deathSourcePriority = Math.Max(damageEvent.DeathSourcePriority, 0);
        if (deathSource != "" || deathSourcePriority > 0)
        {
            return new DeathResolutionContext(
                deathSource == "" ? BattleDeathResolutionRules.DamageDeathSource : deathSource,
                deathSourcePriority
            );
        }
        return BattleDeathResolutionRules.NormalFatalContext();
    }

    private static int ResolveDeathPreventionPriority(BattleStatusEffectState statusEntry)
    {
        if (statusEntry == null)
        {
            return 0;
        }
        if (statusEntry.death_prevention_priority > 0)
        {
            return statusEntry.death_prevention_priority;
        }
        if (
            ProgressionDataUtils.to_string_name(statusEntry.status_id) == "death_ward"
            && ProgressionDataUtils.to_string_name(statusEntry.source_skill_id) == "warrior_last_stand"
        )
        {
            return BattleDeathResolutionRules.NormalFatalPriority;
        }
        return 0;
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

        DamageEventResult resultEvent = WithDamagePreviewSaveEstimate(
            damageOutcome,
            saveEstimate
        ).Event;
        resultEvent.Damage = expectedHpDamage;
        resultEvent.HpDamage = expectedHpDamage;
        resultEvent.ShieldAbsorbed = expectedShieldAbsorbed;
        resultEvent.ShieldBroken = failureResult.ShieldBroken && failureBasis > 0;
        resultEvent.FullyAbsorbedByShield =
            expectedHpDamage <= 0 && expectedShieldAbsorbed > 0;
        return new AppliedDamageResult(
            resultEvent,
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
        DamageResolutionContext damageContext,
        int damageBeforeSave,
        BattleDamagePreviewSaveMode saveMode
    )
    {
        BattleSaveProbabilityResult probability =
            BattleSaveResolver.EstimateSaveSuccessProbabilityResult(
                sourceUnit,
                targetUnit,
                effectDef,
                (damageContext ?? DamageResolutionContext.Empty()).ToBattleSaveContext()
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
            result.Add(BattleSaveResultProjection.Project(source));
        }
        return result;
    }

    private static GDictionary ProjectAppliedDamagePayload(AppliedDamageResult result) =>
        AttackEffectResolutionResultReader.BuildDamageEventPayload(result.Event);

    private static GDictionary ProjectDamageOutcomePayload(DamageOutcomeResult result) =>
        AttackEffectResolutionResultReader.BuildDamageEventPayload(result.Event);

    private static DamageOutcomeResult WithDamagePreviewSaveEstimate(
        DamageOutcomeResult damageOutcome,
        DamagePreviewSaveEstimate saveEstimate
    )
    {
        DamageEventResult @event = damageOutcome.Event;
        @event.PreSaveDamage = saveEstimate.DamageBeforeSave;
        @event.SaveAdjustedDamage = Math.Max(saveEstimate.DamageAfterSave, 0);
        @event.SaveResult = SaveResolutionFromPreviewEstimate(saveEstimate);
        @event.SaveSuccessProbabilityBasisPoints =
            saveEstimate.SaveSuccessProbabilityBasisPoints;
        @event.SaveFailureProbabilityBasisPoints =
            saveEstimate.SaveFailureProbabilityBasisPoints;
        @event.SaveImmune = saveEstimate.Immune;
        @event.SavePartialApplied = saveEstimate.HasSave && saveEstimate.SavePartialOnSuccess;
        @event.FullyAbsorbedBySave =
            saveEstimate.HasSave
            && saveEstimate.DamageBeforeSave > 0
            && saveEstimate.DamageAfterSave <= 0;
        if (saveEstimate.HasSave)
        {
            @event.ResolvedDamage = Math.Max(saveEstimate.DamageAfterSave, 0);
        }
        return damageOutcome with
        {
            Event = @event,
            ResolvedDamage = Math.Max(@event.ResolvedDamage, 0),
        };
    }

    private static SaveResolutionResult SaveResolutionFromPreviewEstimate(
        DamagePreviewSaveEstimate saveEstimate
    )
    {
        return new SaveResolutionResult
        {
            HasSave = saveEstimate.HasSave,
            Immune = saveEstimate.Immune,
            Success = false,
            Dc = saveEstimate.Dc,
            Ability = new StringName(saveEstimate.Ability ?? ""),
            SaveTag = new StringName(saveEstimate.SaveTag ?? ""),
            SaveKind = new StringName(saveEstimate.SaveTag ?? ""),
            AdvantageState = new StringName(saveEstimate.AdvantageState ?? ""),
            AbilityValue = saveEstimate.AbilityValue,
            AbilityModifier = saveEstimate.AbilityModifier,
            Bonus = saveEstimate.Bonus,
            Sources = CopySaveSources(saveEstimate.Sources),
            DamageBeforeSave = saveEstimate.DamageBeforeSave,
            DamageAfterSave = saveEstimate.DamageAfterSave,
            DamageAfterSaveEstimate = saveEstimate.DamageAfterSaveEstimate,
            DamageAfterSaveWorst = saveEstimate.DamageAfterSaveWorst,
            DamageOnSaveFailure = saveEstimate.DamageOnSaveFailure,
            DamageOnSaveSuccess = saveEstimate.DamageOnSaveSuccess,
            SavePartialOnSuccess = saveEstimate.SavePartialOnSuccess,
            SaveSuccessProbabilityBasisPoints =
                saveEstimate.SaveSuccessProbabilityBasisPoints,
            SaveSuccessRatePercent = saveEstimate.SaveSuccessRatePercent,
            SaveFailureProbabilityBasisPoints =
                saveEstimate.SaveFailureProbabilityBasisPoints,
        };
    }

    private static DamageOutcomeResult WithSaveResult(
        DamageOutcomeResult damageOutcome,
        BattleSaveResult saveResult,
        CombatEffectDef effectDef
    )
    {
        DamageEventResult @event = damageOutcome.Event;
        int resolvedDamage = ApplySaveResultToDamageEvent(
            ref @event,
            saveResult,
            effectDef,
            damageOutcome.ResolvedDamage
        );
        return damageOutcome with
        {
            Event = @event,
            ResolvedDamage = Math.Max(resolvedDamage, 0),
        };
    }

    private static int ApplySaveResultToDamageEvent(
        ref DamageEventResult damageEvent,
        BattleSaveResult saveResult,
        CombatEffectDef effectDef,
        int preSaveDamage
    )
    {
        if (!saveResult.HasSave)
        {
            return Math.Max(preSaveDamage, 0);
        }
        preSaveDamage = Math.Max(preSaveDamage, 0);
        damageEvent.SaveResult = SaveResolutionFromBattleSave(saveResult);
        damageEvent.SaveSuccess = saveResult.Success;
        damageEvent.SaveImmune = saveResult.Immune;
        damageEvent.SavePartialApplied = false;
        damageEvent.PreSaveDamage = preSaveDamage;
        if (!saveResult.Success)
        {
            damageEvent.SaveAdjustedDamage = preSaveDamage;
            damageEvent.FullyAbsorbedBySave = false;
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
            damageEvent.SavePartialApplied = true;
        }
        damageEvent.ResolvedDamage = adjustedDamage;
        damageEvent.SaveAdjustedDamage = adjustedDamage;
        damageEvent.FullyAbsorbedBySave = preSaveDamage > 0 && adjustedDamage <= 0;
        return adjustedDamage;
    }

    private static SaveResolutionResult SaveResolutionFromBattleSave(BattleSaveResult saveResult)
    {
        return new SaveResolutionResult
        {
            HasSave = saveResult.HasSave,
            Success = saveResult.Success,
            Immune = saveResult.Immune,
            Roll = saveResult.NaturalRoll,
            Total = saveResult.RollTotal,
            NaturalRoll = saveResult.NaturalRoll,
            RollTotal = saveResult.RollTotal,
            Dc = saveResult.Dc,
            SaveKind = saveResult.SaveTag,
            Ability = saveResult.Ability,
            SaveTag = saveResult.SaveTag,
            AdvantageState = saveResult.AdvantageState,
            AbilityValue = saveResult.AbilityValue,
            AbilityModifier = saveResult.AbilityModifier,
            Bonus = saveResult.Bonus,
            Degree = saveResult.Degree.ToString(),
            Sources = CopySaveSources(saveResult.Sources),
        };
    }

    private static BattleSaveSource[] CopySaveSources(IReadOnlyList<BattleSaveSource> sources)
    {
        if (sources == null || sources.Count == 0)
        {
            return Array.Empty<BattleSaveSource>();
        }
        var result = new BattleSaveSource[sources.Count];
        for (int index = 0; index < sources.Count; index++)
        {
            result[index] = sources[index];
        }
        return result;
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
