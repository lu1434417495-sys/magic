using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public enum AttackResolutionKind
{
    None,
    Hit,
    Miss,
    CriticalHit,
    CriticalFail,
}

public enum CriticalSourceKind
{
    None,
    HighThreat,
    GateDie,
}

public enum ExecuteOutcomeKind
{
    None,
    Resisted,
    FailedSaveFatal,
}

public enum MitigationTierKind
{
    None,
    Normal,
    Half,
    Double,
    Immune,
}

public enum DamageDiceMaxReasonKind
{
    None,
    CriticalHit,
    SkillDiceMax,
    WeaponDiceMax,
}

public enum ReportEntryKind
{
    None,
    FateAttack,
    SkillEvent,
    MeteorSwarmImpact,
}

public struct TraitTriggerEventResult
{
    public bool Triggered;
    public StringName Event;
    public StringName TraitId;
    public StringName EffectType;
    public int OriginalRoll;
    public bool RerollDie;
    public int RerolledRoll;
    public int DieSize;
    public int ExtraWeaponDiceCount;
    public int ExtraWeaponDiceSides;
    public int ClampToHp;
    public int ProjectedHp;
    public int HpDamage;
    public StringName ChargeKey;
    public int ChargesRemaining;
}

public struct DamageDiceRollDetail
{
    public int Count;
    public int Sides;
    public int[] Rolls;
    public int Total;
    public int Bonus;
    public int MaxTotal;
    public bool IsMax;
}

public struct AttackEffectResolutionResult
{
    public bool Applied;
    public int Damage;
    public int HpDamage;
    public int Healing;
    public int ShieldAbsorbed;
    public bool ShieldBroken;

    public bool AttackSuccess;
    public AttackResolutionKind AttackResolution;
    public bool CriticalHit;
    public bool CriticalFail;
    public bool SecondaryHitSuccess;
    public CriticalSourceKind CriticalSource;
    public bool ReverseFateDowngraded;

    public int HitRoll;
    public int RerollDie;
    public int RerolledRoll;
    public int CritGateDie;
    public int CritGateRoll;
    public int RequiredRoll;
    public int DisplayRequiredRoll;
    public int HitRatePercent;
    public int SuccessRatePercent;
    public string ResolutionText;

    public StringName SkillId;
    public GStringNameArray StatusEffectIds;
    public GStringNameArray RemovedStatusEffectIds;
    public GStringNameArray SourceStatusEffectIds;
    public GStringNameArray TerrainEffectIds;
    public int HeightDelta;

    public int ExecuteStage;
    public ExecuteOutcomeKind ExecuteOutcome;
    public string ErrorCode;
    public string BlockedReason;

    public AttackCheckInput AttackCheck;
    public AttackRollResult AttackRoll;
    public AttackResolutionMetadata AttackMetadata;

    public DamageEventResult[] DamageEvents;
    public EquipmentDurabilityEventResult[] EquipmentDurabilityEvents;
    public DispelEventResult[] DispelEvents;
    public SaveResolutionResult[] SaveResults;
    public ResolutionDiagnostic[] Diagnostics;
    public TraitTriggerEventResult[] TraitTriggerResults;

    public bool HasDamageEvent;
    public bool DamageDiceHighTotalRoll;
    public bool SkillDamageDiceIsMax;
    public bool WeaponDamageDiceIsMax;
    public bool BypassShield;
    public bool AnyImmune;
    public bool AnyHalf;
    public bool AnyDouble;
    public int FixedMitigationTotal;
    public string AbsorbReasonText;
    public string FixedMitigationSourceText;
    public string[] AbsorbLabels;
    public string[] HalfSourceLabels;
    public string[] DoubleSourceLabels;
    public string[] ImmuneSourceLabels;
    public string[] FixedMitigationSourceLabels;

    public BattleReportEntry ReportEntry;
    public bool HasReportEntry;
}

public struct DamageEventResult
{
    public StringName DamageTag;
    public int Damage;
    public int HpDamage;
    public int ShieldAbsorbed;
    public bool ShieldBroken;
    public bool BypassShield;
    public bool BypassDeathPrevention;
    public double ShieldAbsorptionPercent;
    public int MinHpAfterDamage;

    public MitigationTierKind MitigationTier;
    public MitigationSourceResult[] MitigationSources;
    public int BaseDamage;
    public bool CriticalHit;
    public bool AddWeaponDice;
    public DamageDiceRollDetail DamageDice;
    public bool BonusConditionMet;
    public DamageDiceRollDetail BonusDamageDice;
    public DamageDiceRollDetail WeaponDamageDice;
    public DamageDiceRollDetail CriticalExtraDamageDice;
    public DamageDiceRollDetail CriticalExtraBonusDamageDice;
    public DamageDiceRollDetail CriticalExtraWeaponDamageDice;
    public DamageDiceRollDetail TraitExtraWeaponDamageDice;
    public double OffenseMultiplier;
    public int RolledDamage;
    public int TierAdjustedDamage;
    public int ResolvedDamage;
    public int BuffReduction;
    public int StanceReduction;
    public int PassiveReduction;
    public int ContentDr;
    public int GuardBlock;
    public int GuardIgnoreApplied;
    public int FixedMitigationTotal;
    public bool FullyAbsorbedByMitigation;
    public bool FullyAbsorbedByShield;
    public bool LowLuckBlackStarWedgeTriggered;

    public bool DamageDiceHighTotalRoll;
    public StringName DamageDiceHighTotalRollReason;
    public bool SkillDamageDiceIsMax;
    public DamageDiceMaxReasonKind SkillDamageDiceIsMaxReason;
    public bool WeaponDamageDiceIsMax;
    public DamageDiceMaxReasonKind WeaponDamageDiceIsMaxReason;
    public SaveResolutionResult SaveResult;
    public bool SaveSuccess;
    public bool SaveImmune;
    public bool SavePartialApplied;
    public int PreSaveDamage;
    public int SaveAdjustedDamage;
    public int SaveSuccessProbabilityBasisPoints;
    public int SaveFailureProbabilityBasisPoints;
    public bool FullyAbsorbedBySave;
    public StringName DeathSource;
    public int DeathSourcePriority;

    public TraitTriggerEventResult[] TraitTriggerResults;
    public string[] HalfSourceLabels;
    public string[] DoubleSourceLabels;
    public string[] ImmuneSourceLabels;
    public string[] FixedMitigationSourceLabels;
}

public struct MitigationSourceResult
{
    public string StatusId;
    public string Type;
    public int Value;
    public MitigationTierKind Tier;
}

public struct EquipmentDurabilityEventResult
{
    public StringName EffectType;
    public StringName TargetUnitId;
    public StringName EntrySlotId;
    public StringName SlotId;
    public StringName EquipmentInstanceId;
    public StringName ItemId;
    public int Rarity;
    public int DurabilityLoss;
    public int DurabilityBefore;
    public int DurabilityAfter;
    public bool Destroyed;
    public SaveResolutionResult SaveResult;
}

public struct DispelEventResult
{
    public StringName EffectType;
    public StringName TargetUnitId;
    public string Mode;
    public int MaxStatusRemoved;
    public GStringNameArray RemovedStatusIds;
}

public struct SaveResolutionResult
{
    public bool HasSave;
    public bool Success;
    public bool Immune;
    public int Roll;
    public int Total;
    public int NaturalRoll;
    public int RollTotal;
    public int Dc;
    public StringName SaveKind;
    public StringName Ability;
    public StringName SaveTag;
    public StringName AdvantageState;
    public int AbilityValue;
    public int AbilityModifier;
    public int Bonus;
    public string Degree;
    public BattleSaveSource[] Sources;
    public int DamageBeforeSave;
    public int DamageAfterSave;
    public int DamageAfterSaveEstimate;
    public int DamageAfterSaveWorst;
    public int DamageOnSaveFailure;
    public int DamageOnSaveSuccess;
    public bool SavePartialOnSuccess;
    public int SaveSuccessProbabilityBasisPoints;
    public int SaveSuccessRatePercent;
    public int SaveFailureProbabilityBasisPoints;
    public int EquipmentRarityBonus;
    public int StatusSaveBonus;
}

public struct ResolutionDiagnostic
{
    public string ErrorCode;
    public string Message;
}

public struct BattleReportEntry
{
    public ReportEntryKind EntryKind;
    public StringName ReasonId;
    public string Text;
    public GStringNameArray EventTags;

    public StringName AttackerId;
    public StringName AttackerMemberId;
    public string AttackerName;
    public StringName DefenderId;
    public StringName DefenderMemberId;
    public string DefenderName;
    public bool DefenderIsEliteOrBoss;

    public AttackResolutionKind AttackResolution;
    public CriticalSourceKind CriticalSource;
    public bool IsDisadvantage;
    public int CritGateDie;
    public int CritGateRoll;
    public int HitRoll;
    public int RequiredRoll;
    public int DisplayRequiredRoll;
    public int HiddenLuckAtBirth;
    public int FaithLuckBonus;
    public int EffectiveLuck;
    public int FumbleLowEnd;
    public int CritThreshold;
}

internal static class PayloadReader
{
    internal static GArray ReadArray(GDictionary source, string key)
    {
        if (source == null || string.IsNullOrEmpty(key) || !source.ContainsKey(key))
            return new GArray();
        return source[key].AsGodotArray();
    }

    internal static GDictionary ReadDictionary(GDictionary source, string key)
    {
        if (source == null || string.IsNullOrEmpty(key) || !source.ContainsKey(key))
            return new GDictionary();
        return source[key].AsGodotDictionary();
    }

    internal static int ReadInt(GDictionary source, string key, int fallback = 0)
    {
        if (source == null || string.IsNullOrEmpty(key) || !source.ContainsKey(key))
            return fallback;
        return source[key].AsInt32();
    }

    internal static string ReadString(GDictionary source, string key, string fallback = "")
    {
        if (source == null || string.IsNullOrEmpty(key) || !source.ContainsKey(key))
            return fallback;
        return source[key].ToString();
    }

    internal static StringName ReadStringName(
        GDictionary source,
        string key,
        StringName fallback = default
    )
    {
        string text = ReadString(source, key, "");
        if (string.IsNullOrEmpty(text))
        {
            return fallback;
        }
        return new StringName(text);
    }
}

internal static class AttackEffectResolutionResultReader
{
    internal static AttackEffectResolutionResult ReadResolverResult(
        GDictionary source,
        AttackCheckInput attackCheck
    )
    {
        source ??= new GDictionary();
        AttackCheckInput normalizedAttackCheck = attackCheck;
        var result = new AttackEffectResolutionResult
        {
            Applied = ReadExactBooleanField(source, "applied", false),
            Damage = PayloadReader.ReadInt(source, "damage", 0),
            HpDamage = PayloadReader.ReadInt(source, "hp_damage", PayloadReader.ReadInt(source, "damage", 0)),
            Healing = PayloadReader.ReadInt(source, "healing", 0),
            ShieldAbsorbed = PayloadReader.ReadInt(source, "shield_absorbed", 0),
            ShieldBroken = ReadExactBooleanField(source, "shield_broken", false),
            AttackSuccess = ReadExactBooleanField(source, "attack_success", false),
            AttackResolution = ParseAttackResolution(PayloadReader.ReadStringName(source, "attack_resolution")),
            CriticalHit = ReadExactBooleanField(source, "critical_hit", false),
            CriticalFail = ReadExactBooleanField(source, "critical_fail", false),
            SecondaryHitSuccess = ReadExactBooleanField(source, "secondary_hit_success", false),
            CriticalSource = ParseCriticalSource(PayloadReader.ReadStringName(source, "critical_source")),
            ReverseFateDowngraded = ReadExactBooleanField(source, "reverse_fate_downgraded", false),
            HitRoll = PayloadReader.ReadInt(source, "hit_roll", 0),
            RerollDie = PayloadReader.ReadInt(source, "reroll_die", 0),
            RerolledRoll = PayloadReader.ReadInt(source, "rerolled_roll", 0),
            CritGateDie = PayloadReader.ReadInt(source, "crit_gate_die", 0),
            CritGateRoll = PayloadReader.ReadInt(source, "crit_gate_roll", 0),
            RequiredRoll = PayloadReader.ReadInt(source, "required_roll", normalizedAttackCheck.RequiredRoll),
            DisplayRequiredRoll = PayloadReader.ReadInt(
                source,
                "display_required_roll",
                normalizedAttackCheck.DisplayRequiredRoll
            ),
            HitRatePercent = PayloadReader.ReadInt(
                source,
                "hit_rate_percent",
                normalizedAttackCheck.HitRatePercent
            ),
            SuccessRatePercent = PayloadReader.ReadInt(
                source,
                "success_rate_percent",
                normalizedAttackCheck.SuccessRatePercent
            ),
            ResolutionText = PayloadReader.ReadString(source, "resolution_text", ""),
            SkillId = PayloadReader.ReadStringName(source, "skill_id", normalizedAttackCheck.SkillId),
            StatusEffectIds = ReadStringNameArray(source, "status_effect_ids"),
            RemovedStatusEffectIds = ReadStringNameArray(source, "removed_status_effect_ids"),
            SourceStatusEffectIds = ReadStringNameArray(source, "source_status_effect_ids"),
            TerrainEffectIds = ReadStringNameArray(source, "terrain_effect_ids"),
            HeightDelta = PayloadReader.ReadInt(source, "height_delta", 0),
            ExecuteStage = PayloadReader.ReadInt(source, "execute_stage", -1),
            ExecuteOutcome = ParseExecuteOutcome(PayloadReader.ReadStringName(source, "execute_outcome")),
            ErrorCode = PayloadReader.ReadString(source, "error_code", ""),
            BlockedReason = PayloadReader.ReadString(source, "blocked_reason", ""),
            AttackCheck = normalizedAttackCheck,
            DamageEvents = ReadDamageEvents(source),
            EquipmentDurabilityEvents = ReadEquipmentDurabilityEvents(source),
            DispelEvents = ReadDispelEvents(source),
            SaveResults = ReadSaveResults(source),
            Diagnostics = ReadDiagnostics(source),
            TraitTriggerResults = ReadTraitTriggerResults(source, "trait_trigger_results"),
            ReportEntry = BattleReportEntryPayload.ReadPayload(
                PayloadReader.ReadDictionary(source, "report_entry")
            ),
        };
        result.HasReportEntry =
            result.ReportEntry.EntryKind != ReportEntryKind.None
            || !string.IsNullOrEmpty(result.ReportEntry.Text);
        return FinalizeTypedResult(result, source);
    }

    internal static AttackEffectResolutionResult FinalizeTypedResult(
        AttackEffectResolutionResult result,
        GDictionary source = null
    )
    {
        result.StatusEffectIds ??= new GStringNameArray();
        result.RemovedStatusEffectIds ??= new GStringNameArray();
        result.SourceStatusEffectIds ??= new GStringNameArray();
        result.TerrainEffectIds ??= new GStringNameArray();
        result.DamageEvents ??= Array.Empty<DamageEventResult>();
        result.EquipmentDurabilityEvents ??= Array.Empty<EquipmentDurabilityEventResult>();
        result.DispelEvents ??= Array.Empty<DispelEventResult>();
        result.SaveResults ??= Array.Empty<SaveResolutionResult>();
        result.Diagnostics ??= Array.Empty<ResolutionDiagnostic>();
        result.TraitTriggerResults ??= Array.Empty<TraitTriggerEventResult>();
        result.AttackMetadata = BuildAttackMetadata(result);
        result.AttackRoll = new AttackRollResult(
            result.HitRoll,
            AttackResolutionToStringName(result.AttackResolution),
            result.AttackSuccess,
            result.ResolutionText,
            result.RerollDie,
            result.RerolledRoll
        );
        AttachDamageEventAggregates(ref result, source);
        return result;
    }

    internal static GDictionary BuildGodotPayload(AttackEffectResolutionResult result)
    {
        var payload = new GDictionary
        {
            ["applied"] = result.Applied,
            ["damage"] = result.Damage,
            ["hp_damage"] = result.HpDamage,
            ["healing"] = result.Healing,
            ["shield_absorbed"] = result.ShieldAbsorbed,
            ["shield_broken"] = result.ShieldBroken,
            ["attack_success"] = result.AttackSuccess,
            ["attack_resolution"] = AttackResolutionToStringName(result.AttackResolution).ToString(),
            ["crit_locked"] = result.AttackCheck.CritLocked,
            ["critical_hit"] = result.CriticalHit,
            ["critical_fail"] = result.CriticalFail,
            ["secondary_hit_success"] = result.SecondaryHitSuccess,
            ["critical_source"] = CriticalSourceToStringName(result.CriticalSource).ToString(),
            ["reverse_fate_downgraded"] = result.ReverseFateDowngraded,
            ["hit_roll"] = result.HitRoll,
            ["reroll_die"] = result.RerollDie,
            ["rerolled_roll"] = result.RerolledRoll,
            ["crit_gate_die"] = result.CritGateDie,
            ["crit_gate_roll"] = result.CritGateRoll,
            ["required_roll"] = result.RequiredRoll,
            ["display_required_roll"] = result.DisplayRequiredRoll,
            ["hit_rate_percent"] = result.HitRatePercent,
            ["success_rate_percent"] = result.SuccessRatePercent,
            ["resolution_text"] = result.ResolutionText ?? "",
            ["skill_id"] = (result.SkillId ?? new StringName("")).ToString(),
            ["status_effect_ids"] = BuildStringNameArrayPayload(result.StatusEffectIds),
            ["removed_status_effect_ids"] = BuildStringNameArrayPayload(result.RemovedStatusEffectIds),
            ["source_status_effect_ids"] = BuildStringNameArrayPayload(result.SourceStatusEffectIds),
            ["terrain_effect_ids"] = BuildStringNameArrayPayload(result.TerrainEffectIds),
            ["height_delta"] = result.HeightDelta,
            ["execute_stage"] = result.ExecuteStage,
            ["execute_outcome"] = ExecuteOutcomeToStringName(result.ExecuteOutcome).ToString(),
            ["error_code"] = result.ErrorCode ?? "",
            ["blocked_reason"] = result.BlockedReason ?? "",
            ["damage_events"] = BuildDamageEventsPayload(result.DamageEvents),
            ["equipment_durability_events"] = BuildEquipmentDurabilityEventsPayload(
                result.EquipmentDurabilityEvents
            ),
            ["dispel_events"] = BuildDispelEventsPayload(result.DispelEvents),
            ["save_results"] = BuildSaveResultsPayload(result.SaveResults),
            ["diagnostics"] = BuildDiagnosticsPayload(result.Diagnostics),
            ["trait_trigger_results"] = BuildTraitTriggerResultsPayload(
                result.TraitTriggerResults
            ),
            ["damage_dice_high_total_roll"] = result.DamageDiceHighTotalRoll,
            ["skill_damage_dice_is_max"] = result.SkillDamageDiceIsMax,
            ["weapon_damage_dice_is_max"] = result.WeaponDamageDiceIsMax,
        };
        if (result.HasReportEntry)
        {
            payload["report_entry"] = BattleReportEntryPayload.BuildGodotPayload(result.ReportEntry);
        }
        return payload;
    }

    internal static AttackResolutionKind ParseAttackResolution(StringName value)
    {
        return ProgressionDataUtils.to_string_name(value).ToString() switch
        {
            "hit" => AttackResolutionKind.Hit,
            "miss" => AttackResolutionKind.Miss,
            "critical_hit" => AttackResolutionKind.CriticalHit,
            "critical_fail" => AttackResolutionKind.CriticalFail,
            _ => AttackResolutionKind.None,
        };
    }

    internal static CriticalSourceKind ParseCriticalSource(StringName value)
    {
        return ProgressionDataUtils.to_string_name(value).ToString() switch
        {
            "high_threat" => CriticalSourceKind.HighThreat,
            "gate_die" => CriticalSourceKind.GateDie,
            _ => CriticalSourceKind.None,
        };
    }

    internal static ExecuteOutcomeKind ParseExecuteOutcome(StringName value)
    {
        return ProgressionDataUtils.to_string_name(value).ToString() switch
        {
            "resisted" => ExecuteOutcomeKind.Resisted,
            "failed_save_fatal" => ExecuteOutcomeKind.FailedSaveFatal,
            _ => ExecuteOutcomeKind.None,
        };
    }

    internal static MitigationTierKind ParseMitigationTier(StringName value)
    {
        return ProgressionDataUtils.to_string_name(value).ToString() switch
        {
            "normal" => MitigationTierKind.Normal,
            "half" => MitigationTierKind.Half,
            "double" => MitigationTierKind.Double,
            "immune" => MitigationTierKind.Immune,
            _ => MitigationTierKind.None,
        };
    }

    internal static DamageDiceMaxReasonKind ParseDamageDiceMaxReason(StringName value)
    {
        return ProgressionDataUtils.to_string_name(value).ToString() switch
        {
            "critical_hit" => DamageDiceMaxReasonKind.CriticalHit,
            "skill_dice_max" => DamageDiceMaxReasonKind.SkillDiceMax,
            "weapon_dice_max" => DamageDiceMaxReasonKind.WeaponDiceMax,
            _ => DamageDiceMaxReasonKind.None,
        };
    }

    internal static StringName ExecuteOutcomeToStringName(ExecuteOutcomeKind value)
    {
        return value switch
        {
            ExecuteOutcomeKind.Resisted => new StringName("resisted"),
            ExecuteOutcomeKind.FailedSaveFatal => new StringName("failed_save_fatal"),
            _ => new StringName(""),
        };
    }

    internal static StringName MitigationTierToStringName(MitigationTierKind value)
    {
        return value switch
        {
            MitigationTierKind.Normal => new StringName("normal"),
            MitigationTierKind.Half => new StringName("half"),
            MitigationTierKind.Double => new StringName("double"),
            MitigationTierKind.Immune => new StringName("immune"),
            _ => new StringName(""),
        };
    }

    internal static StringName DamageDiceMaxReasonToStringName(DamageDiceMaxReasonKind value)
    {
        return value switch
        {
            DamageDiceMaxReasonKind.CriticalHit => new StringName("critical_hit"),
            DamageDiceMaxReasonKind.SkillDiceMax => new StringName("skill_dice_max"),
            DamageDiceMaxReasonKind.WeaponDiceMax => new StringName("weapon_dice_max"),
            _ => new StringName(""),
        };
    }

    internal static StringName AttackResolutionToStringName(AttackResolutionKind value)
    {
        return value switch
        {
            AttackResolutionKind.Hit => new StringName("hit"),
            AttackResolutionKind.Miss => new StringName("miss"),
            AttackResolutionKind.CriticalHit => new StringName("critical_hit"),
            AttackResolutionKind.CriticalFail => new StringName("critical_fail"),
            _ => new StringName(""),
        };
    }

    internal static StringName CriticalSourceToStringName(CriticalSourceKind value)
    {
        return value switch
        {
            CriticalSourceKind.HighThreat => new StringName("high_threat"),
            CriticalSourceKind.GateDie => new StringName("gate_die"),
            _ => new StringName(""),
        };
    }

    internal static bool ReadExactBooleanField(
        GDictionary source,
        string key,
        bool fallback = false
    )
    {
        if (source == null || string.IsNullOrEmpty(key) || !source.ContainsKey(key))
            return fallback;
        return source[key].AsBool();
    }

    private static double ReadDouble(GDictionary source, string key, double fallback = 0.0)
    {
        if (source == null || string.IsNullOrEmpty(key) || !source.ContainsKey(key))
            return fallback;
        Variant value = source[key];
        return value.VariantType switch
        {
            Variant.Type.Int => value.AsInt64(),
            Variant.Type.Float => value.AsDouble(),
            _ => fallback,
        };
    }

    private static AttackResolutionMetadata BuildAttackMetadata(AttackEffectResolutionResult result)
    {
        return new AttackResolutionMetadata
        {
            AttackResolution = AttackResolutionToStringName(result.AttackResolution),
            AttackSuccess = result.AttackSuccess,
            CriticalHit = result.CriticalHit,
            CriticalFail = result.CriticalFail,
            OrdinaryMiss = result.AttackResolution == AttackResolutionKind.Miss,
            IsDisadvantage = result.AttackCheck.IsDisadvantage,
            HiddenLuckAtBirth = result.ReportEntry.HiddenLuckAtBirth,
            FaithLuckBonus = result.ReportEntry.FaithLuckBonus,
            EffectiveLuck = result.ReportEntry.EffectiveLuck,
            CritLocked = result.AttackCheck.CritLocked,
            CritGateDie = result.CritGateDie,
            CritGateRoll = result.CritGateRoll,
            HitRoll = result.HitRoll,
            FumbleLowEnd = result.ReportEntry.FumbleLowEnd,
            CritThreshold = result.ReportEntry.CritThreshold,
            RequiredRoll = result.RequiredRoll,
            DisplayRequiredRoll = result.DisplayRequiredRoll,
            HitRatePercent = result.HitRatePercent,
            SuccessRatePercent = result.SuccessRatePercent,
            ReverseFateDowngraded = result.ReverseFateDowngraded,
            SecondaryHitSuccess = result.SecondaryHitSuccess,
            SkillId = result.SkillId ?? new StringName(""),
        };
    }

    private static GStringNameArray ReadStringNameArray(GDictionary source, string key)
    {
        var result = new GStringNameArray();
        foreach (var value in PayloadReader.ReadArray(source, key))
        {
            StringName normalized = ProgressionDataUtils.to_string_name(value);
            if (normalized == "" || result.Contains(normalized))
                continue;
            result.Add(normalized);
        }
        return result;
    }

    private static int[] ReadIntArray(GDictionary source, string key)
    {
        GArray values = PayloadReader.ReadArray(source, key);
        if (values.Count == 0)
        {
            return Array.Empty<int>();
        }
        int[] result = new int[values.Count];
        for (int index = 0; index < values.Count; index++)
        {
            result[index] = values[index].AsInt32();
        }
        return result;
    }

    private static DamageDiceRollDetail ReadDiceRollDetail(
        GDictionary source,
        string prefix,
        bool includeBonus
    )
    {
        return new DamageDiceRollDetail
        {
            Count = PayloadReader.ReadInt(source, $"{prefix}_count", 0),
            Sides = PayloadReader.ReadInt(source, $"{prefix}_sides", 0),
            Rolls = ReadIntArray(source, $"{prefix}_rolls"),
            Total = PayloadReader.ReadInt(source, $"{prefix}_total", 0),
            Bonus = includeBonus ? PayloadReader.ReadInt(source, $"{prefix}_bonus", 0) : 0,
            MaxTotal = PayloadReader.ReadInt(source, $"{prefix}_max_total", 0),
            IsMax = includeBonus && ReadExactBooleanField(source, $"{prefix}_is_max", false),
        };
    }

    internal static TraitTriggerEventResult[] ReadTraitTriggerResults(
        GDictionary source,
        string key
    )
    {
        var results = new List<TraitTriggerEventResult>();
        foreach (var value in PayloadReader.ReadArray(source, key))
        {
            GDictionary payload = value.AsGodotDictionary();
            results.Add(
                new TraitTriggerEventResult
                {
                    Triggered = ReadExactBooleanField(payload, "triggered", false),
                    Event = PayloadReader.ReadStringName(payload, "event"),
                    TraitId = PayloadReader.ReadStringName(payload, "trait_id"),
                    EffectType = PayloadReader.ReadStringName(payload, "effect_type"),
                    OriginalRoll = PayloadReader.ReadInt(payload, "original_roll", 0),
                    RerollDie = ReadExactBooleanField(payload, "reroll_die", false),
                    RerolledRoll = PayloadReader.ReadInt(payload, "rerolled_roll", 0),
                    DieSize = PayloadReader.ReadInt(payload, "die_size", 0),
                    ExtraWeaponDiceCount = PayloadReader.ReadInt(
                        payload,
                        "extra_weapon_dice_count",
                        0
                    ),
                    ExtraWeaponDiceSides = PayloadReader.ReadInt(
                        payload,
                        "extra_weapon_dice_sides",
                        0
                    ),
                    ClampToHp = PayloadReader.ReadInt(payload, "clamp_to_hp", 0),
                    ProjectedHp = PayloadReader.ReadInt(payload, "projected_hp", 0),
                    HpDamage = PayloadReader.ReadInt(payload, "hp_damage", 0),
                    ChargeKey = PayloadReader.ReadStringName(payload, "charge_key"),
                    ChargesRemaining = PayloadReader.ReadInt(payload, "charges_remaining", 0),
                }
            );
        }
        return results.ToArray();
    }

    private static GArray BuildStringNameArrayPayload(GStringNameArray values)
    {
        var result = new GArray();
        foreach (StringName value in values ?? new GStringNameArray())
        {
            result.Add(value.ToString());
        }
        return result;
    }

    private static GArray BuildDamageEventsPayload(DamageEventResult[] values)
    {
        var result = new GArray();
        foreach (DamageEventResult value in values ?? System.Array.Empty<DamageEventResult>())
        {
            result.Add(BuildDamageEventPayload(value));
        }
        return result;
    }

    internal static GDictionary BuildDamageEventPayload(DamageEventResult value)
    {
        GDictionary payload = new()
        {
            ["damage_tag"] = (value.DamageTag ?? new StringName("")).ToString(),
            ["damage"] = value.Damage,
            ["hp_damage"] = value.HpDamage,
            ["shield_absorbed"] = value.ShieldAbsorbed,
            ["shield_broken"] = value.ShieldBroken,
            ["bypass_shield"] = value.BypassShield,
            ["bypass_death_prevention"] = value.BypassDeathPrevention,
            ["shield_absorption_percent"] = value.ShieldAbsorptionPercent,
            ["min_hp_after_damage"] = value.MinHpAfterDamage,
            ["mitigation_tier"] = MitigationTierToStringName(value.MitigationTier).ToString(),
            ["base_damage"] = value.BaseDamage,
            ["critical_hit"] = value.CriticalHit,
            ["add_weapon_dice"] = value.AddWeaponDice,
            ["bonus_condition_met"] = value.BonusConditionMet,
            ["offense_multiplier"] = value.OffenseMultiplier,
            ["rolled_damage"] = value.RolledDamage,
            ["tier_adjusted_damage"] = value.TierAdjustedDamage,
            ["resolved_damage"] = value.ResolvedDamage,
            ["buff_reduction"] = value.BuffReduction,
            ["stance_reduction"] = value.StanceReduction,
            ["passive_reduction"] = value.PassiveReduction,
            ["content_dr"] = value.ContentDr,
            ["guard_block"] = value.GuardBlock,
            ["guard_ignore_applied"] = value.GuardIgnoreApplied,
            ["fixed_mitigation_total"] = value.FixedMitigationTotal,
            ["fully_absorbed_by_mitigation"] = value.FullyAbsorbedByMitigation,
            ["fully_absorbed_by_shield"] = value.FullyAbsorbedByShield,
            ["low_luck_black_star_wedge_triggered"] = value.LowLuckBlackStarWedgeTriggered,
            ["damage_dice_high_total_roll"] = value.DamageDiceHighTotalRoll,
            ["damage_dice_high_total_roll_reason"] =
                (value.DamageDiceHighTotalRollReason ?? new StringName("")).ToString(),
            ["skill_damage_dice_is_max"] = value.SkillDamageDiceIsMax,
            ["skill_damage_dice_is_max_reason"] =
                DamageDiceMaxReasonToStringName(value.SkillDamageDiceIsMaxReason).ToString(),
            ["weapon_damage_dice_is_max"] = value.WeaponDamageDiceIsMax,
            ["weapon_damage_dice_is_max_reason"] =
                DamageDiceMaxReasonToStringName(value.WeaponDamageDiceIsMaxReason).ToString(),
            ["save_success"] = value.SaveSuccess,
            ["save_immune"] = value.SaveImmune,
            ["save_partial_applied"] = value.SavePartialApplied,
            ["pre_save_damage"] = value.PreSaveDamage,
            ["save_adjusted_damage"] = value.SaveAdjustedDamage,
            ["save_success_probability_basis_points"] =
                value.SaveSuccessProbabilityBasisPoints,
            ["save_failure_probability_basis_points"] =
                value.SaveFailureProbabilityBasisPoints,
            ["fully_absorbed_by_save"] = value.FullyAbsorbedBySave,
            ["mitigation_sources"] =
                value.MitigationSources != null && value.MitigationSources.Length > 0
                    ? BuildMitigationSourcesPayload(value.MitigationSources)
                    : BuildMitigationSourcesPayload(
                        value.HalfSourceLabels,
                        value.DoubleSourceLabels,
                        value.ImmuneSourceLabels
                    ),
            ["fixed_mitigation_sources"] = BuildFixedMitigationSourcesPayload(
                value.FixedMitigationSourceLabels
            ),
            ["trait_trigger_results"] = BuildTraitTriggerResultsPayload(
                value.TraitTriggerResults
            ),
            [BattleDeathResolutionRules.DeathSourcePayloadKey] =
                (value.DeathSource ?? new StringName("")).ToString(),
            [BattleDeathResolutionRules.DeathSourcePriorityPayloadKey] =
                value.DeathSourcePriority,
        };
        AppendDicePayload(payload, "damage_dice", value.DamageDice, includeBonus: true);
        AppendDicePayload(payload, "bonus_damage_dice", value.BonusDamageDice, includeBonus: true);
        AppendDicePayload(payload, "weapon_damage_dice", value.WeaponDamageDice, includeBonus: true);
        AppendDicePayload(
            payload,
            "critical_extra_damage_dice",
            value.CriticalExtraDamageDice,
            includeBonus: false
        );
        AppendDicePayload(
            payload,
            "critical_extra_bonus_damage_dice",
            value.CriticalExtraBonusDamageDice,
            includeBonus: false
        );
        AppendDicePayload(
            payload,
            "critical_extra_weapon_damage_dice",
            value.CriticalExtraWeaponDamageDice,
            includeBonus: false
        );
        AppendDicePayload(
            payload,
            "trait_extra_weapon_damage_dice",
            value.TraitExtraWeaponDamageDice,
            includeBonus: false
        );
        payload["damage_dice_high_total_roll"] = value.DamageDiceHighTotalRoll;
        payload["damage_dice_high_total_roll_reason"] =
            (value.DamageDiceHighTotalRollReason ?? new StringName("")).ToString();
        payload["skill_damage_dice_is_max"] = value.SkillDamageDiceIsMax;
        payload["skill_damage_dice_is_max_reason"] =
            DamageDiceMaxReasonToStringName(value.SkillDamageDiceIsMaxReason).ToString();
        payload["weapon_damage_dice_is_max"] = value.WeaponDamageDiceIsMax;
        payload["weapon_damage_dice_is_max_reason"] =
            DamageDiceMaxReasonToStringName(value.WeaponDamageDiceIsMaxReason).ToString();
        if (value.SaveResult.HasSave || value.SaveResult.Dc > 0)
        {
            payload["save_result"] = BuildSaveResolutionPayload(value.SaveResult);
        }
        return payload;
    }

    private static void AppendDicePayload(
        GDictionary payload,
        string prefix,
        DamageDiceRollDetail value,
        bool includeBonus
    )
    {
        payload[$"{prefix}_count"] = value.Count;
        payload[$"{prefix}_sides"] = value.Sides;
        payload[$"{prefix}_rolls"] = BuildIntArrayPayload(value.Rolls);
        payload[$"{prefix}_total"] = value.Total;
        if (includeBonus)
        {
            payload[$"{prefix}_bonus"] = value.Bonus;
            payload[$"{prefix}_is_max"] = value.IsMax;
        }
        payload[$"{prefix}_max_total"] = value.MaxTotal;
    }

    private static GArray BuildIntArrayPayload(int[] values)
    {
        var result = new GArray();
        foreach (int value in values ?? Array.Empty<int>())
        {
            result.Add(value);
        }
        return result;
    }

    private static GArray BuildEquipmentDurabilityEventsPayload(
        EquipmentDurabilityEventResult[] values
    )
    {
        var result = new GArray();
        foreach (
            EquipmentDurabilityEventResult value
            in values ?? System.Array.Empty<EquipmentDurabilityEventResult>()
        )
        {
            GDictionary payload = new()
            {
                ["effect_type"] = (value.EffectType ?? new StringName("")).ToString(),
                ["target_unit_id"] = (value.TargetUnitId ?? new StringName("")).ToString(),
                ["entry_slot_id"] = (value.EntrySlotId ?? new StringName("")).ToString(),
                ["slot_id"] = (value.SlotId ?? new StringName("")).ToString(),
                ["item_id"] = (value.ItemId ?? new StringName("")).ToString(),
                ["instance_id"] =
                    (value.EquipmentInstanceId ?? new StringName("")).ToString(),
                ["equipment_instance_id"] =
                    (value.EquipmentInstanceId ?? new StringName("")).ToString(),
                ["rarity"] = value.Rarity,
                ["durability_loss"] = value.DurabilityLoss,
                ["durability_before"] = value.DurabilityBefore,
                ["durability_after"] = value.DurabilityAfter,
                ["destroyed"] = value.Destroyed,
                ["save_result"] = BuildSaveResolutionPayload(value.SaveResult),
            };
            result.Add(payload);
        }
        return result;
    }

    private static GArray BuildDispelEventsPayload(DispelEventResult[] values)
    {
        var result = new GArray();
        foreach (DispelEventResult value in values ?? System.Array.Empty<DispelEventResult>())
        {
            result.Add(
                new GDictionary
                {
                    ["effect_type"] = (value.EffectType ?? new StringName("")).ToString(),
                    ["target_unit_id"] = (value.TargetUnitId ?? new StringName("")).ToString(),
                    ["mode"] = value.Mode ?? "",
                    ["max_status_removed"] = value.MaxStatusRemoved,
                    ["removed_status_ids"] = BuildStringNameArrayPayload(value.RemovedStatusIds),
                }
            );
        }
        return result;
    }

    private static GArray BuildSaveResultsPayload(SaveResolutionResult[] values)
    {
        var result = new GArray();
        foreach (SaveResolutionResult value in values ?? System.Array.Empty<SaveResolutionResult>())
        {
            result.Add(BuildSaveResolutionPayload(value));
        }
        return result;
    }

    private static GDictionary BuildSaveResolutionPayload(SaveResolutionResult value)
    {
        return new GDictionary
        {
            ["has_save"] = value.HasSave,
            ["success"] = value.Success,
            ["immune"] = value.Immune,
            ["roll"] = value.Roll > 0 ? value.Roll : value.NaturalRoll,
            ["total"] = value.Total > 0 ? value.Total : value.RollTotal,
            ["natural_roll"] = value.NaturalRoll > 0 ? value.NaturalRoll : value.Roll,
            ["roll_total"] = value.RollTotal > 0 ? value.RollTotal : value.Total,
            ["dc"] = value.Dc,
            ["save_kind"] =
                (value.SaveKind != "" ? value.SaveKind : value.SaveTag).ToString(),
            ["ability"] = (value.Ability ?? new StringName("")).ToString(),
            ["save_tag"] =
                (value.SaveTag != "" ? value.SaveTag : value.SaveKind).ToString(),
            ["advantage_state"] = (value.AdvantageState ?? new StringName("")).ToString(),
            ["ability_value"] = value.AbilityValue,
            ["ability_modifier"] = value.AbilityModifier,
            ["bonus"] = value.Bonus,
            ["degree"] = value.Degree ?? "",
            ["sources"] = BuildSaveSourcePayload(value.Sources),
            ["damage_before_save"] = value.DamageBeforeSave,
            ["damage_after_save"] = value.DamageAfterSave,
            ["damage_after_save_estimate"] = value.DamageAfterSaveEstimate,
            ["damage_after_save_worst"] = value.DamageAfterSaveWorst,
            ["damage_on_save_failure"] = value.DamageOnSaveFailure,
            ["damage_on_save_success"] = value.DamageOnSaveSuccess,
            ["save_partial_on_success"] = value.SavePartialOnSuccess,
            ["save_success_probability_basis_points"] =
                value.SaveSuccessProbabilityBasisPoints,
            ["save_success_rate_percent"] = value.SaveSuccessRatePercent,
            ["save_failure_probability_basis_points"] =
                value.SaveFailureProbabilityBasisPoints,
            ["equipment_rarity_bonus"] = value.EquipmentRarityBonus,
            ["status_save_bonus"] = value.StatusSaveBonus,
        };
    }

    private static GArray BuildSaveSourcePayload(BattleSaveSource[] sources)
    {
        var result = new GArray();
        foreach (BattleSaveSource source in sources ?? Array.Empty<BattleSaveSource>())
        {
            result.Add(source.ToDictionary());
        }
        return result;
    }

    internal static GArray BuildTraitTriggerResultsPayload(TraitTriggerEventResult[] values)
    {
        var result = new GArray();
        foreach (TraitTriggerEventResult value in values ?? Array.Empty<TraitTriggerEventResult>())
        {
            if (!value.Triggered)
            {
                continue;
            }
            result.Add(
                new GDictionary
                {
                    ["triggered"] = value.Triggered,
                    ["event"] = (value.Event ?? new StringName("")).ToString(),
                    ["trait_id"] = (value.TraitId ?? new StringName("")).ToString(),
                    ["effect_type"] = (value.EffectType ?? new StringName("")).ToString(),
                    ["original_roll"] = value.OriginalRoll,
                    ["reroll_die"] = value.RerollDie,
                    ["rerolled_roll"] = value.RerolledRoll,
                    ["die_size"] = value.DieSize,
                    ["extra_weapon_dice_count"] = value.ExtraWeaponDiceCount,
                    ["extra_weapon_dice_sides"] = value.ExtraWeaponDiceSides,
                    ["clamp_to_hp"] = value.ClampToHp,
                    ["projected_hp"] = value.ProjectedHp,
                    ["hp_damage"] = value.HpDamage,
                    ["charge_key"] = (value.ChargeKey ?? new StringName("")).ToString(),
                    ["charges_remaining"] = value.ChargesRemaining,
                }
            );
        }
        return result;
    }

    private static GArray BuildDiagnosticsPayload(ResolutionDiagnostic[] values)
    {
        var result = new GArray();
        foreach (ResolutionDiagnostic value in values ?? System.Array.Empty<ResolutionDiagnostic>())
        {
            result.Add(
                new GDictionary
                {
                    ["error_code"] = value.ErrorCode ?? "",
                    ["message"] = value.Message ?? "",
                }
            );
        }
        return result;
    }

    internal static GArray BuildMitigationSourcesPayload(
        string[] halfSourceLabels,
        string[] doubleSourceLabels,
        string[] immuneSourceLabels
    )
    {
        var result = new GArray();
        AppendMitigationSourcePayloads(result, halfSourceLabels, MitigationTierKind.Half);
        AppendMitigationSourcePayloads(result, doubleSourceLabels, MitigationTierKind.Double);
        AppendMitigationSourcePayloads(result, immuneSourceLabels, MitigationTierKind.Immune);
        return result;
    }

    internal static GArray BuildMitigationSourcesPayload(MitigationSourceResult[] sources)
    {
        var result = new GArray();
        foreach (MitigationSourceResult source in sources ?? System.Array.Empty<MitigationSourceResult>())
        {
            if (string.IsNullOrEmpty(source.StatusId))
            {
                continue;
            }
            result.Add(
                new GDictionary
                {
                    ["status_id"] = source.StatusId,
                    ["type"] = source.Type ?? "",
                    ["value"] = source.Value,
                    ["tier"] = MitigationTierToStringName(source.Tier).ToString(),
                }
            );
        }
        return result;
    }

    private static void AppendMitigationSourcePayloads(
        GArray result,
        string[] labels,
        MitigationTierKind tier
    )
    {
        foreach (string label in labels ?? System.Array.Empty<string>())
        {
            if (string.IsNullOrEmpty(label))
            {
                continue;
            }
            result.Add(
                new GDictionary
                {
                    ["tier"] = MitigationTierToStringName(tier).ToString(),
                    ["status_id"] = label,
                }
            );
        }
    }

    internal static GArray BuildFixedMitigationSourcesPayload(string[] labels)
    {
        var result = new GArray();
        foreach (string label in labels ?? System.Array.Empty<string>())
        {
            if (!string.IsNullOrEmpty(label))
            {
                result.Add(new GDictionary { ["status_id"] = label });
            }
        }
        return result;
    }

    private static DamageEventResult[] ReadDamageEvents(GDictionary source)
    {
        var results = new List<DamageEventResult>();
        foreach (var eventValue in PayloadReader.ReadArray(source, "damage_events"))
        {
            results.Add(ReadDamageEventPayload(eventValue.AsGodotDictionary()));
        }
        return results.ToArray();
    }

    internal static DamageEventResult ReadDamageEventPayload(GDictionary evt)
    {
        evt ??= new GDictionary();
        ReadMitigationSourceLabels(
            evt,
            out string[] halfSourceLabels,
            out string[] doubleSourceLabels,
            out string[] immuneSourceLabels
        );
        SaveResolutionResult saveResult = ReadSaveResolution(
            PayloadReader.ReadDictionary(evt, "save_result")
        );
        return new DamageEventResult
        {
            DamageTag = PayloadReader.ReadStringName(evt, "damage_tag"),
            Damage = PayloadReader.ReadInt(evt, "damage", 0),
            HpDamage = PayloadReader.ReadInt(
                evt,
                "hp_damage",
                PayloadReader.ReadInt(evt, "damage", 0)
            ),
            ShieldAbsorbed = PayloadReader.ReadInt(evt, "shield_absorbed", 0),
            ShieldBroken = ReadExactBooleanField(evt, "shield_broken", false),
            BypassShield = ReadExactBooleanField(evt, "bypass_shield", false),
            BypassDeathPrevention = ReadExactBooleanField(
                evt,
                "bypass_death_prevention",
                false
            ),
            ShieldAbsorptionPercent = ReadDouble(evt, "shield_absorption_percent", 100.0),
            MinHpAfterDamage = PayloadReader.ReadInt(evt, "min_hp_after_damage", 0),
            MitigationTier = ParseMitigationTier(
                PayloadReader.ReadStringName(evt, "mitigation_tier")
            ),
            MitigationSources = ReadMitigationSources(evt),
            BaseDamage = PayloadReader.ReadInt(evt, "base_damage", 0),
            CriticalHit = ReadExactBooleanField(evt, "critical_hit", false),
            AddWeaponDice = ReadExactBooleanField(evt, "add_weapon_dice", false),
            DamageDice = ReadDiceRollDetail(evt, "damage_dice", includeBonus: true),
            BonusConditionMet = ReadExactBooleanField(evt, "bonus_condition_met", false),
            BonusDamageDice = ReadDiceRollDetail(evt, "bonus_damage_dice", includeBonus: true),
            WeaponDamageDice = ReadDiceRollDetail(evt, "weapon_damage_dice", includeBonus: true),
            CriticalExtraDamageDice = ReadDiceRollDetail(
                evt,
                "critical_extra_damage_dice",
                includeBonus: false
            ),
            CriticalExtraBonusDamageDice = ReadDiceRollDetail(
                evt,
                "critical_extra_bonus_damage_dice",
                includeBonus: false
            ),
            CriticalExtraWeaponDamageDice = ReadDiceRollDetail(
                evt,
                "critical_extra_weapon_damage_dice",
                includeBonus: false
            ),
            TraitExtraWeaponDamageDice = ReadDiceRollDetail(
                evt,
                "trait_extra_weapon_damage_dice",
                includeBonus: false
            ),
            OffenseMultiplier = ReadDouble(evt, "offense_multiplier", 0.0),
            RolledDamage = PayloadReader.ReadInt(evt, "rolled_damage", 0),
            TierAdjustedDamage = PayloadReader.ReadInt(evt, "tier_adjusted_damage", 0),
            ResolvedDamage = PayloadReader.ReadInt(evt, "resolved_damage", 0),
            BuffReduction = PayloadReader.ReadInt(evt, "buff_reduction", 0),
            StanceReduction = PayloadReader.ReadInt(evt, "stance_reduction", 0),
            PassiveReduction = PayloadReader.ReadInt(evt, "passive_reduction", 0),
            ContentDr = PayloadReader.ReadInt(evt, "content_dr", 0),
            GuardBlock = PayloadReader.ReadInt(evt, "guard_block", 0),
            GuardIgnoreApplied = PayloadReader.ReadInt(evt, "guard_ignore_applied", 0),
            FixedMitigationTotal = PayloadReader.ReadInt(evt, "fixed_mitigation_total", 0),
            FullyAbsorbedByMitigation = ReadExactBooleanField(
                evt,
                "fully_absorbed_by_mitigation",
                false
            ),
            FullyAbsorbedByShield = ReadExactBooleanField(
                evt,
                "fully_absorbed_by_shield",
                false
            ),
            LowLuckBlackStarWedgeTriggered = ReadExactBooleanField(
                evt,
                "low_luck_black_star_wedge_triggered",
                false
            ),
            DamageDiceHighTotalRoll = ReadExactBooleanField(
                evt,
                "damage_dice_high_total_roll",
                false
            ),
            DamageDiceHighTotalRollReason = PayloadReader.ReadStringName(
                evt,
                "damage_dice_high_total_roll_reason"
            ),
            SkillDamageDiceIsMax = ReadExactBooleanField(
                evt,
                "skill_damage_dice_is_max",
                false
            ),
            SkillDamageDiceIsMaxReason = ParseDamageDiceMaxReason(
                PayloadReader.ReadStringName(evt, "skill_damage_dice_is_max_reason")
            ),
            WeaponDamageDiceIsMax = ReadExactBooleanField(
                evt,
                "weapon_damage_dice_is_max",
                false
            ),
            WeaponDamageDiceIsMaxReason = ParseDamageDiceMaxReason(
                PayloadReader.ReadStringName(evt, "weapon_damage_dice_is_max_reason")
            ),
            SaveResult = saveResult,
            SaveSuccess = ReadExactBooleanField(evt, "save_success", saveResult.Success),
            SaveImmune = ReadExactBooleanField(evt, "save_immune", saveResult.Immune),
            SavePartialApplied = ReadExactBooleanField(evt, "save_partial_applied", false),
            PreSaveDamage = PayloadReader.ReadInt(evt, "pre_save_damage", 0),
            SaveAdjustedDamage = PayloadReader.ReadInt(evt, "save_adjusted_damage", 0),
            SaveSuccessProbabilityBasisPoints = PayloadReader.ReadInt(
                evt,
                "save_success_probability_basis_points",
                saveResult.SaveSuccessProbabilityBasisPoints
            ),
            SaveFailureProbabilityBasisPoints = PayloadReader.ReadInt(
                evt,
                "save_failure_probability_basis_points",
                saveResult.SaveFailureProbabilityBasisPoints
            ),
            FullyAbsorbedBySave = ReadExactBooleanField(
                evt,
                "fully_absorbed_by_save",
                false
            ),
            DeathSource = PayloadReader.ReadStringName(
                evt,
                BattleDeathResolutionRules.DeathSourcePayloadKey
            ),
            DeathSourcePriority = PayloadReader.ReadInt(
                evt,
                BattleDeathResolutionRules.DeathSourcePriorityPayloadKey,
                0
            ),
            TraitTriggerResults = ReadTraitTriggerResults(evt, "trait_trigger_results"),
            HalfSourceLabels = halfSourceLabels,
            DoubleSourceLabels = doubleSourceLabels,
            ImmuneSourceLabels = immuneSourceLabels,
            FixedMitigationSourceLabels = ReadFixedMitigationSourceLabels(evt),
        };
    }

    private static EquipmentDurabilityEventResult[] ReadEquipmentDurabilityEvents(GDictionary source)
    {
        var results = new List<EquipmentDurabilityEventResult>();
        foreach (var eventValue in PayloadReader.ReadArray(source, "equipment_durability_events"))
        {
            GDictionary evt = eventValue.AsGodotDictionary();
            results.Add(
                new EquipmentDurabilityEventResult
                {
                    EffectType = PayloadReader.ReadStringName(evt, "effect_type"),
                    TargetUnitId = PayloadReader.ReadStringName(evt, "target_unit_id"),
                    EntrySlotId = PayloadReader.ReadStringName(evt, "entry_slot_id"),
                    SlotId = PayloadReader.ReadStringName(evt, "slot_id"),
                    EquipmentInstanceId = PayloadReader.ReadStringName(
                        evt,
                        "equipment_instance_id",
                        PayloadReader.ReadStringName(evt, "instance_id")
                    ),
                    ItemId = PayloadReader.ReadStringName(evt, "item_id"),
                    Rarity = PayloadReader.ReadInt(evt, "rarity", 0),
                    DurabilityLoss = PayloadReader.ReadInt(evt, "durability_loss", 0),
                    DurabilityBefore = PayloadReader.ReadInt(evt, "durability_before", 0),
                    DurabilityAfter = PayloadReader.ReadInt(evt, "durability_after", 0),
                    Destroyed = ReadExactBooleanField(evt, "destroyed", false),
                    SaveResult = ReadSaveResolution(
                        PayloadReader.ReadDictionary(evt, "save_result")
                    ),
                }
            );
        }
        return results.ToArray();
    }

    private static DispelEventResult[] ReadDispelEvents(GDictionary source)
    {
        var results = new List<DispelEventResult>();
        foreach (var eventValue in PayloadReader.ReadArray(source, "dispel_events"))
        {
            results.Add(
                new DispelEventResult
                {
                    EffectType = PayloadReader.ReadStringName(
                        eventValue.AsGodotDictionary(),
                        "effect_type"
                    ),
                    TargetUnitId = PayloadReader.ReadStringName(
                        eventValue.AsGodotDictionary(),
                        "target_unit_id"
                    ),
                    Mode = PayloadReader.ReadString(
                        eventValue.AsGodotDictionary(),
                        "mode",
                        ""
                    ),
                    MaxStatusRemoved = PayloadReader.ReadInt(
                        eventValue.AsGodotDictionary(),
                        "max_status_removed",
                        0
                    ),
                    RemovedStatusIds = ReadStringNameArray(
                        eventValue.AsGodotDictionary(),
                        "removed_status_ids"
                    ),
                }
            );
        }
        return results.ToArray();
    }

    private static SaveResolutionResult[] ReadSaveResults(GDictionary source)
    {
        var results = new List<SaveResolutionResult>();
        foreach (var saveValue in PayloadReader.ReadArray(source, "save_results"))
        {
            results.Add(ReadSaveResolution(saveValue.AsGodotDictionary()));
        }
        return results.ToArray();
    }

    private static SaveResolutionResult ReadSaveResolution(GDictionary save)
    {
        save ??= new GDictionary();
        return new SaveResolutionResult
        {
            HasSave = ReadExactBooleanField(save, "has_save", false),
            Success = ReadExactBooleanField(save, "success", false),
            Immune = ReadExactBooleanField(save, "immune", false),
            Roll = PayloadReader.ReadInt(save, "roll", 0),
            Total = PayloadReader.ReadInt(save, "total", 0),
            NaturalRoll = PayloadReader.ReadInt(
                save,
                "natural_roll",
                PayloadReader.ReadInt(save, "roll", 0)
            ),
            RollTotal = PayloadReader.ReadInt(
                save,
                "roll_total",
                PayloadReader.ReadInt(save, "total", 0)
            ),
            Dc = PayloadReader.ReadInt(save, "dc", 0),
            SaveKind = PayloadReader.ReadStringName(save, "save_kind"),
            Ability = PayloadReader.ReadStringName(save, "ability"),
            SaveTag = PayloadReader.ReadStringName(save, "save_tag"),
            AdvantageState = PayloadReader.ReadStringName(save, "advantage_state"),
            AbilityValue = PayloadReader.ReadInt(save, "ability_value", 0),
            AbilityModifier = PayloadReader.ReadInt(save, "ability_modifier", 0),
            Bonus = PayloadReader.ReadInt(save, "bonus", 0),
            Degree = PayloadReader.ReadString(save, "degree", ""),
            Sources = ReadSaveSources(save),
            DamageBeforeSave = PayloadReader.ReadInt(save, "damage_before_save", 0),
            DamageAfterSave = PayloadReader.ReadInt(save, "damage_after_save", 0),
            DamageAfterSaveEstimate = PayloadReader.ReadInt(
                save,
                "damage_after_save_estimate",
                0
            ),
            DamageAfterSaveWorst = PayloadReader.ReadInt(save, "damage_after_save_worst", 0),
            DamageOnSaveFailure = PayloadReader.ReadInt(save, "damage_on_save_failure", 0),
            DamageOnSaveSuccess = PayloadReader.ReadInt(save, "damage_on_save_success", 0),
            SavePartialOnSuccess = ReadExactBooleanField(
                save,
                "save_partial_on_success",
                false
            ),
            SaveSuccessProbabilityBasisPoints = PayloadReader.ReadInt(
                save,
                "save_success_probability_basis_points",
                0
            ),
            SaveSuccessRatePercent = PayloadReader.ReadInt(
                save,
                "save_success_rate_percent",
                0
            ),
            SaveFailureProbabilityBasisPoints = PayloadReader.ReadInt(
                save,
                "save_failure_probability_basis_points",
                0
            ),
            EquipmentRarityBonus = PayloadReader.ReadInt(save, "equipment_rarity_bonus", 0),
            StatusSaveBonus = PayloadReader.ReadInt(save, "status_save_bonus", 0),
        };
    }

    private static BattleSaveSource[] ReadSaveSources(GDictionary save)
    {
        var result = new List<BattleSaveSource>();
        foreach (var sourceValue in PayloadReader.ReadArray(save, "sources"))
        {
            GDictionary source = sourceValue.AsGodotDictionary();
            result.Add(
                new BattleSaveSource(
                    PayloadReader.ReadStringName(source, "source_id"),
                    PayloadReader.ReadString(source, "type", ""),
                    PayloadReader.ReadStringName(source, "tag"),
                    PayloadReader.ReadStringName(source, "mode")
                )
            );
        }
        return result.ToArray();
    }

    private static ResolutionDiagnostic[] ReadDiagnostics(GDictionary source)
    {
        var results = new List<ResolutionDiagnostic>();
        foreach (var diagnosticValue in PayloadReader.ReadArray(source, "diagnostics"))
        {
            GDictionary diagnostic = diagnosticValue.AsGodotDictionary();
            results.Add(
                new ResolutionDiagnostic
                {
                    ErrorCode = PayloadReader.ReadString(diagnostic, "error_code", ""),
                    Message = PayloadReader.ReadString(diagnostic, "message", ""),
                }
            );
        }
        return results.ToArray();
    }

    private static void AttachDamageEventAggregates(
        ref AttackEffectResolutionResult result,
        GDictionary source
    )
    {
        var absorbLabels = new List<string>();
        var halfSourceLabels = new List<string>();
        var doubleSourceLabels = new List<string>();
        var immuneSourceLabels = new List<string>();
        var fixedMitigationSourceLabels = new List<string>();
        result.HasDamageEvent = result.DamageEvents != null && result.DamageEvents.Length > 0;
        result.DamageDiceHighTotalRoll = ReadExactBooleanField(
            source,
            "damage_dice_high_total_roll",
            false
        );
        result.SkillDamageDiceIsMax = ReadExactBooleanField(
            source,
            "skill_damage_dice_is_max",
            false
        );
        result.WeaponDamageDiceIsMax = ReadExactBooleanField(
            source,
            "weapon_damage_dice_is_max",
            false
        );

        foreach (DamageEventResult damageEvent in result.DamageEvents ?? System.Array.Empty<DamageEventResult>())
        {
            result.BypassShield = result.BypassShield || damageEvent.BypassShield;
            result.ShieldBroken = result.ShieldBroken || damageEvent.ShieldBroken;
            result.FixedMitigationTotal += damageEvent.FixedMitigationTotal;
            result.DamageDiceHighTotalRoll =
                result.DamageDiceHighTotalRoll || damageEvent.DamageDiceHighTotalRoll;
            result.SkillDamageDiceIsMax =
                result.SkillDamageDiceIsMax || damageEvent.SkillDamageDiceIsMax;
            result.WeaponDamageDiceIsMax =
                result.WeaponDamageDiceIsMax || damageEvent.WeaponDamageDiceIsMax;
            result.AnyImmune = result.AnyImmune || damageEvent.MitigationTier == MitigationTierKind.Immune;
            result.AnyHalf = result.AnyHalf || damageEvent.MitigationTier == MitigationTierKind.Half;
            result.AnyDouble = result.AnyDouble || damageEvent.MitigationTier == MitigationTierKind.Double;
            AppendUniqueRange(halfSourceLabels, damageEvent.HalfSourceLabels);
            AppendUniqueRange(doubleSourceLabels, damageEvent.DoubleSourceLabels);
            AppendUniqueRange(immuneSourceLabels, damageEvent.ImmuneSourceLabels);
            AppendMitigationSourceLabelsByTier(
                halfSourceLabels,
                doubleSourceLabels,
                immuneSourceLabels,
                damageEvent.MitigationSources
            );
            AppendUniqueRange(fixedMitigationSourceLabels, damageEvent.FixedMitigationSourceLabels);
            if (
                damageEvent.BuffReduction > 0
                || damageEvent.PassiveReduction > 0
                || damageEvent.ContentDr > 0
            )
                AppendUnique(absorbLabels, "减伤");
            if (damageEvent.StanceReduction > 0 || damageEvent.GuardBlock > 0)
                AppendUnique(absorbLabels, "格挡");
        }

        result.AbsorbLabels = absorbLabels.ToArray();
        result.HalfSourceLabels = halfSourceLabels.ToArray();
        result.DoubleSourceLabels = doubleSourceLabels.ToArray();
        result.ImmuneSourceLabels = immuneSourceLabels.ToArray();
        result.FixedMitigationSourceLabels = fixedMitigationSourceLabels.ToArray();
        result.FixedMitigationSourceText = JoinLabels(result.FixedMitigationSourceLabels);
        result.AbsorbReasonText = BuildDamageAbsorbReasonText(result);
    }

    private static string BuildDamageAbsorbReasonText(AttackEffectResolutionResult result)
    {
        if (result.AnyImmune)
            return JoinLabels(result.ImmuneSourceLabels, "免疫");
        var labels = new List<string>();
        if (result.AnyHalf)
            labels.Add(string.IsNullOrEmpty(JoinLabels(result.HalfSourceLabels)) ? "减半" : JoinLabels(result.HalfSourceLabels));
        if (string.IsNullOrEmpty(result.FixedMitigationSourceText))
            AppendUniqueRange(labels, result.AbsorbLabels);
        if (!string.IsNullOrEmpty(result.FixedMitigationSourceText))
            AppendUnique(labels, result.FixedMitigationSourceText);
        return labels.Count == 0 ? "防护" : string.Join("、", labels);
    }

    internal static void ReadMitigationSourceLabels(
        GDictionary damageEvent,
        out string[] halfSourceLabels,
        out string[] doubleSourceLabels,
        out string[] immuneSourceLabels
    )
    {
        var half = new List<string>();
        var @double = new List<string>();
        var immune = new List<string>();
        foreach (var sourceValue in PayloadReader.ReadArray(damageEvent, "mitigation_sources"))
        {
            GDictionary source = sourceValue.AsGodotDictionary();
            string label = FormatDamageSourceLabel(source);
            if (string.IsNullOrEmpty(label))
                continue;
            switch (ParseMitigationTier(PayloadReader.ReadStringName(source, "tier")))
            {
                case MitigationTierKind.Half:
                    AppendUnique(half, label);
                    break;
                case MitigationTierKind.Double:
                    AppendUnique(@double, label);
                    break;
                case MitigationTierKind.Immune:
                    AppendUnique(immune, label);
                    break;
            }
        }
        halfSourceLabels = half.ToArray();
        doubleSourceLabels = @double.ToArray();
        immuneSourceLabels = immune.ToArray();
    }

    internal static MitigationSourceResult[] ReadMitigationSources(GDictionary damageEvent)
    {
        return ReadMitigationSourcesFromArray(
            PayloadReader.ReadArray(damageEvent, "mitigation_sources")
        );
    }

    internal static MitigationSourceResult[] ReadMitigationSourcesFromArray(GArray sourceValues)
    {
        var results = new List<MitigationSourceResult>();
        foreach (var sourceValue in sourceValues ?? new GArray())
        {
            GDictionary source = sourceValue.AsGodotDictionary();
            string statusId = PayloadReader.ReadString(source, "status_id", "");
            if (string.IsNullOrEmpty(statusId))
            {
                continue;
            }
            results.Add(
                new MitigationSourceResult
                {
                    StatusId = statusId,
                    Type = PayloadReader.ReadString(source, "type", ""),
                    Value = PayloadReader.ReadInt(source, "value", 0),
                    Tier = ParseMitigationTier(PayloadReader.ReadStringName(source, "tier")),
                }
            );
        }
        return results.ToArray();
    }

    internal static string[] ReadFixedMitigationSourceLabels(GDictionary damageEvent)
    {
        var labels = new List<string>();
        foreach (var sourceValue in PayloadReader.ReadArray(damageEvent, "fixed_mitigation_sources"))
        {
            string label = FormatDamageSourceLabel(sourceValue.AsGodotDictionary());
            if (!string.IsNullOrEmpty(label))
                AppendUnique(labels, label);
        }
        return labels.ToArray();
    }

    private static string FormatDamageSourceLabel(GDictionary source)
    {
        string statusId = PayloadReader.ReadString(source, "status_id", "");
        if (!string.IsNullOrEmpty(statusId))
            return statusId;
        return PayloadReader.ReadString(source, "type", "");
    }

    private static void AppendUniqueRange(List<string> target, string[] values)
    {
        if (values == null)
            return;
        foreach (string value in values)
            AppendUnique(target, value);
    }

    private static void AppendUnique(List<string> target, string value)
    {
        if (string.IsNullOrEmpty(value) || target.Contains(value))
            return;
        target.Add(value);
    }

    private static void AppendMitigationSourceLabelsByTier(
        List<string> halfSourceLabels,
        List<string> doubleSourceLabels,
        List<string> immuneSourceLabels,
        MitigationSourceResult[] sources
    )
    {
        foreach (MitigationSourceResult source in sources ?? Array.Empty<MitigationSourceResult>())
        {
            string label = !string.IsNullOrEmpty(source.StatusId)
                ? source.StatusId
                : source.Type ?? "";
            if (string.IsNullOrEmpty(label))
            {
                continue;
            }
            switch (source.Tier)
            {
                case MitigationTierKind.Half:
                    AppendUnique(halfSourceLabels, label);
                    break;
                case MitigationTierKind.Double:
                    AppendUnique(doubleSourceLabels, label);
                    break;
                case MitigationTierKind.Immune:
                    AppendUnique(immuneSourceLabels, label);
                    break;
            }
        }
    }

    internal static string JoinLabels(string[] labels, string fallback = "")
    {
        if (labels == null || labels.Length == 0)
            return fallback;
        var unique = new List<string>();
        AppendUniqueRange(unique, labels);
        return unique.Count == 0 ? fallback : string.Join("、", unique);
    }
}

internal static class BattleReportEntryPayload
{
    internal static BattleReportEntry ReadPayload(GDictionary entry)
    {
        entry ??= new GDictionary();
        GDictionary luckSnapshot = PayloadReader.ReadDictionary(entry, "luck_snapshot");
        return new BattleReportEntry
        {
            EntryKind = ParseReportEntryKind(PayloadReader.ReadStringName(entry, "entry_type")),
            ReasonId = PayloadReader.ReadStringName(entry, "reason_id"),
            Text = PayloadReader.ReadString(entry, "text", ""),
            EventTags = ReadStringNameArray(entry, "event_tags"),
            AttackerId = PayloadReader.ReadStringName(entry, "attacker_id"),
            AttackerMemberId = PayloadReader.ReadStringName(entry, "attacker_member_id"),
            AttackerName = PayloadReader.ReadString(entry, "attacker_name", ""),
            DefenderId = PayloadReader.ReadStringName(entry, "defender_id"),
            DefenderMemberId = PayloadReader.ReadStringName(entry, "defender_member_id"),
            DefenderName = PayloadReader.ReadString(entry, "defender_name", ""),
            DefenderIsEliteOrBoss = AttackEffectResolutionResultReader.ReadExactBooleanField(
                entry,
                "defender_is_elite_or_boss",
                false
            ),
            AttackResolution = AttackEffectResolutionResultReader.ParseAttackResolution(
                PayloadReader.ReadStringName(entry, "attack_resolution")
            ),
            CriticalSource = AttackEffectResolutionResultReader.ParseCriticalSource(
                PayloadReader.ReadStringName(entry, "critical_source")
            ),
            IsDisadvantage = AttackEffectResolutionResultReader.ReadExactBooleanField(
                entry,
                "is_disadvantage",
                false
            ),
            CritGateDie = PayloadReader.ReadInt(entry, "crit_gate_die", 0),
            CritGateRoll = PayloadReader.ReadInt(entry, "crit_gate_roll", 0),
            HitRoll = PayloadReader.ReadInt(entry, "hit_roll", 0),
            RequiredRoll = PayloadReader.ReadInt(entry, "required_roll", 0),
            DisplayRequiredRoll = PayloadReader.ReadInt(entry, "display_required_roll", 0),
            HiddenLuckAtBirth = PayloadReader.ReadInt(luckSnapshot, "hidden_luck_at_birth", 0),
            FaithLuckBonus = PayloadReader.ReadInt(luckSnapshot, "faith_luck_bonus", 0),
            EffectiveLuck = PayloadReader.ReadInt(luckSnapshot, "effective_luck", 0),
            FumbleLowEnd = PayloadReader.ReadInt(luckSnapshot, "fumble_low_end", 0),
            CritThreshold = PayloadReader.ReadInt(luckSnapshot, "crit_threshold", 0),
        };
    }

    internal static GDictionary BuildGodotPayload(BattleReportEntry entry)
    {
        if (entry.EntryKind == ReportEntryKind.None && string.IsNullOrEmpty(entry.Text))
            return new GDictionary();
        return new GDictionary
        {
            ["entry_type"] = EntryKindToString(entry.EntryKind),
            ["reason_id"] = (entry.ReasonId ?? new StringName("")).ToString(),
            ["text"] = entry.Text ?? "",
            ["event_tags"] = ProgressionDataUtils.string_name_array_to_string_array(
                entry.EventTags ?? new GStringNameArray()
            ),
            ["attacker_id"] = (entry.AttackerId ?? new StringName("")).ToString(),
            ["attacker_member_id"] = (entry.AttackerMemberId ?? new StringName("")).ToString(),
            ["attacker_name"] = entry.AttackerName ?? "",
            ["defender_id"] = (entry.DefenderId ?? new StringName("")).ToString(),
            ["defender_member_id"] = (entry.DefenderMemberId ?? new StringName("")).ToString(),
            ["defender_name"] = entry.DefenderName ?? "",
            ["defender_is_elite_or_boss"] = entry.DefenderIsEliteOrBoss,
            ["attack_resolution"] = AttackEffectResolutionResultReader
                .AttackResolutionToStringName(entry.AttackResolution)
                .ToString(),
            ["critical_source"] = CriticalSourceToStringName(entry.CriticalSource).ToString(),
            ["is_disadvantage"] = entry.IsDisadvantage,
            ["crit_gate_die"] = entry.CritGateDie,
            ["crit_gate_roll"] = entry.CritGateRoll,
            ["hit_roll"] = entry.HitRoll,
            ["required_roll"] = entry.RequiredRoll,
            ["display_required_roll"] = entry.DisplayRequiredRoll,
            ["luck_snapshot"] = new GDictionary
            {
                ["hidden_luck_at_birth"] = entry.HiddenLuckAtBirth,
                ["faith_luck_bonus"] = entry.FaithLuckBonus,
                ["effective_luck"] = entry.EffectiveLuck,
                ["fumble_low_end"] = entry.FumbleLowEnd,
                ["crit_threshold"] = entry.CritThreshold,
            },
        };
    }

    private static ReportEntryKind ParseReportEntryKind(StringName value)
    {
        return ProgressionDataUtils.to_string_name(value).ToString() switch
        {
            "fate_attack_resolution" => ReportEntryKind.FateAttack,
            "battle_skill_event" => ReportEntryKind.SkillEvent,
            "meteor_swarm_impact_summary" => ReportEntryKind.MeteorSwarmImpact,
            _ => ReportEntryKind.None,
        };
    }

    private static string EntryKindToString(ReportEntryKind value)
    {
        return value switch
        {
            ReportEntryKind.FateAttack => "fate_attack_resolution",
            ReportEntryKind.SkillEvent => "battle_skill_event",
            ReportEntryKind.MeteorSwarmImpact => "meteor_swarm_impact_summary",
            _ => "",
        };
    }

    private static StringName CriticalSourceToStringName(CriticalSourceKind value)
    {
        return value switch
        {
            CriticalSourceKind.HighThreat => new StringName("high_threat"),
            CriticalSourceKind.GateDie => new StringName("gate_die"),
            _ => new StringName(""),
        };
    }

    private static GStringNameArray ReadStringNameArray(GDictionary source, string key)
    {
        var result = new GStringNameArray();
        foreach (var value in PayloadReader.ReadArray(source, key))
        {
            StringName normalized = ProgressionDataUtils.to_string_name(value);
            if (normalized == "" || result.Contains(normalized))
                continue;
            result.Add(normalized);
        }
        return result;
    }
}
