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
    public int Damage;
    public int HpDamage;
    public int ShieldAbsorbed;
    public bool ShieldBroken;
    public bool BypassShield;

    public MitigationTierKind MitigationTier;
    public int BuffReduction;
    public int StanceReduction;
    public int PassiveReduction;
    public int ContentDr;
    public int GuardBlock;
    public int GuardIgnoreApplied;
    public int FixedMitigationTotal;

    public bool DamageDiceHighTotalRoll;
    public bool SkillDamageDiceIsMax;
    public DamageDiceMaxReasonKind SkillDamageDiceIsMaxReason;
    public bool WeaponDamageDiceIsMax;
    public DamageDiceMaxReasonKind WeaponDamageDiceIsMaxReason;

    public string[] HalfSourceLabels;
    public string[] DoubleSourceLabels;
    public string[] ImmuneSourceLabels;
    public string[] FixedMitigationSourceLabels;
}

public struct EquipmentDurabilityEventResult
{
    public StringName EquipmentInstanceId;
    public string ItemId;
    public int DurabilityLoss;
    public int DurabilityBefore;
    public int DurabilityAfter;
    public bool Destroyed;
    public SaveResolutionResult SaveResult;
}

public struct DispelEventResult
{
    public GStringNameArray RemovedStatusIds;
}

public struct SaveResolutionResult
{
    public bool HasSave;
    public bool Success;
    public int Roll;
    public int Total;
    public int Dc;
    public StringName SaveKind;
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

internal static class LegacyPayloadReader
{
    internal static bool TryRead(GDictionary source, object key, out Variant value)
    {
        if (source == null || key == null)
        {
            value = default;
            return false;
        }
        Variant variantKey = key switch
        {
            Variant valueKey => valueKey,
            string stringKey => stringKey,
            StringName stringNameKey => stringNameKey,
            int intKey => intKey,
            long longKey => longKey,
            _ => default,
        };
        if (source.ContainsKey(variantKey))
        {
            value = source[variantKey];
            return true;
        }
        if (variantKey.VariantType == Variant.Type.String)
        {
            StringName stringNameKey = new(variantKey.AsString());
            if (source.ContainsKey(stringNameKey))
            {
                value = source[stringNameKey];
                return true;
            }
        }
        else if (variantKey.VariantType == Variant.Type.StringName)
        {
            string stringKey = variantKey.AsStringName().ToString();
            if (source.ContainsKey(stringKey))
            {
                value = source[stringKey];
                return true;
            }
        }
        value = default;
        return false;
    }

    internal static GArray ReadArray(GDictionary source, object key)
    {
        if (!TryRead(source, key, out Variant value))
            return new GArray();
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
    }

    internal static GDictionary ReadDictionary(GDictionary source, object key)
    {
        if (!TryRead(source, key, out Variant value))
            return new GDictionary();
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new GDictionary();
    }

    internal static int ReadInt(GDictionary source, object key, int fallback = 0)
    {
        if (!TryRead(source, key, out Variant value))
            return fallback;
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    internal static string ReadString(GDictionary source, object key, string fallback = "")
    {
        if (!TryRead(source, key, out Variant value))
            return fallback;
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => fallback,
        };
    }

    internal static StringName ReadStringName(
        GDictionary source,
        object key,
        StringName fallback = default
    )
    {
        if (!TryRead(source, key, out Variant value))
            return fallback ?? new StringName("");
        return value.VariantType switch
        {
            Variant.Type.StringName => value.AsStringName(),
            Variant.Type.String => new StringName(value.AsString()),
            _ => fallback ?? new StringName(""),
        };
    }
}

internal static class AttackEffectResolutionResultReader
{
    internal static AttackEffectResolutionResult ReadLegacyResolverResult(
        GDictionary source,
        AttackCheckInput attackCheck
    )
    {
        source ??= new GDictionary();
        var result = new AttackEffectResolutionResult
        {
            Applied = ReadExactBooleanField(source, "applied", false),
            Damage = LegacyPayloadReader.ReadInt(source, "damage", 0),
            HpDamage = LegacyPayloadReader.ReadInt(source, "hp_damage", LegacyPayloadReader.ReadInt(source, "damage", 0)),
            Healing = LegacyPayloadReader.ReadInt(source, "healing", 0),
            ShieldAbsorbed = LegacyPayloadReader.ReadInt(source, "shield_absorbed", 0),
            ShieldBroken = ReadExactBooleanField(source, "shield_broken", false),
            AttackSuccess = ReadExactBooleanField(source, "attack_success", false),
            AttackResolution = ParseAttackResolution(LegacyPayloadReader.ReadStringName(source, "attack_resolution")),
            CriticalHit = ReadExactBooleanField(source, "critical_hit", false),
            CriticalFail = ReadExactBooleanField(source, "critical_fail", false),
            SecondaryHitSuccess = ReadExactBooleanField(source, "secondary_hit_success", false),
            CriticalSource = ParseCriticalSource(LegacyPayloadReader.ReadStringName(source, "critical_source")),
            ReverseFateDowngraded = ReadExactBooleanField(source, "reverse_fate_downgraded", false),
            HitRoll = LegacyPayloadReader.ReadInt(source, "hit_roll", 0),
            RerollDie = LegacyPayloadReader.ReadInt(source, "reroll_die", 0),
            RerolledRoll = LegacyPayloadReader.ReadInt(source, "rerolled_roll", 0),
            CritGateDie = LegacyPayloadReader.ReadInt(source, "crit_gate_die", 0),
            CritGateRoll = LegacyPayloadReader.ReadInt(source, "crit_gate_roll", 0),
            RequiredRoll = LegacyPayloadReader.ReadInt(source, "required_roll", attackCheck.RequiredRoll),
            DisplayRequiredRoll = LegacyPayloadReader.ReadInt(
                source,
                "display_required_roll",
                attackCheck.DisplayRequiredRoll
            ),
            HitRatePercent = LegacyPayloadReader.ReadInt(source, "hit_rate_percent", attackCheck.HitRatePercent),
            SuccessRatePercent = LegacyPayloadReader.ReadInt(
                source,
                "success_rate_percent",
                attackCheck.SuccessRatePercent
            ),
            ResolutionText = LegacyPayloadReader.ReadString(source, "resolution_text", ""),
            SkillId = LegacyPayloadReader.ReadStringName(source, "skill_id", attackCheck.SkillId),
            StatusEffectIds = ReadStringNameArray(source, "status_effect_ids"),
            RemovedStatusEffectIds = ReadStringNameArray(source, "removed_status_effect_ids"),
            SourceStatusEffectIds = ReadStringNameArray(source, "source_status_effect_ids"),
            TerrainEffectIds = ReadStringNameArray(source, "terrain_effect_ids"),
            HeightDelta = LegacyPayloadReader.ReadInt(source, "height_delta", 0),
            ExecuteStage = LegacyPayloadReader.ReadInt(source, "execute_stage", -1),
            ExecuteOutcome = ParseExecuteOutcome(LegacyPayloadReader.ReadStringName(source, "execute_outcome")),
            ErrorCode = LegacyPayloadReader.ReadString(source, "error_code", ""),
            BlockedReason = LegacyPayloadReader.ReadString(source, "blocked_reason", ""),
            AttackCheck = attackCheck,
            DamageEvents = ReadDamageEvents(source),
            EquipmentDurabilityEvents = ReadEquipmentDurabilityEvents(source),
            DispelEvents = ReadDispelEvents(source),
            SaveResults = ReadSaveResults(source),
            Diagnostics = ReadDiagnostics(source),
            ReportEntry = BattleReportEntryPayload.ReadLegacy(
                LegacyPayloadReader.ReadDictionary(source, "report_entry")
            ),
        };
        result.HasReportEntry =
            result.ReportEntry.EntryKind != ReportEntryKind.None
            || !string.IsNullOrEmpty(result.ReportEntry.Text);
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

    internal static bool ReadExactBooleanField(
        GDictionary source,
        object key,
        bool fallback = false
    )
    {
        if (!LegacyPayloadReader.TryRead(source, key, out Variant value))
            return fallback;
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
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
        foreach (Variant value in LegacyPayloadReader.ReadArray(source, key))
        {
            StringName normalized = ProgressionDataUtils.to_string_name(value);
            if (normalized == "" || result.Contains(normalized))
                continue;
            result.Add(normalized);
        }
        return result;
    }

    private static DamageEventResult[] ReadDamageEvents(GDictionary source)
    {
        var results = new List<DamageEventResult>();
        foreach (Variant eventValue in LegacyPayloadReader.ReadArray(source, "damage_events"))
        {
            if (eventValue.VariantType != Variant.Type.Dictionary)
                continue;
            GDictionary evt = eventValue.AsGodotDictionary();
            ReadMitigationSourceLabels(
                evt,
                out string[] halfSourceLabels,
                out string[] doubleSourceLabels,
                out string[] immuneSourceLabels
            );
            results.Add(
                new DamageEventResult
                {
                    Damage = LegacyPayloadReader.ReadInt(evt, "damage", 0),
                    HpDamage = LegacyPayloadReader.ReadInt(evt, "hp_damage", LegacyPayloadReader.ReadInt(evt, "damage", 0)),
                    ShieldAbsorbed = LegacyPayloadReader.ReadInt(evt, "shield_absorbed", 0),
                    ShieldBroken = ReadExactBooleanField(evt, "shield_broken", false),
                    BypassShield = ReadExactBooleanField(evt, "bypass_shield", false),
                    MitigationTier = ParseMitigationTier(
                        LegacyPayloadReader.ReadStringName(evt, "mitigation_tier")
                    ),
                    BuffReduction = LegacyPayloadReader.ReadInt(evt, "buff_reduction", 0),
                    StanceReduction = LegacyPayloadReader.ReadInt(evt, "stance_reduction", 0),
                    PassiveReduction = LegacyPayloadReader.ReadInt(evt, "passive_reduction", 0),
                    ContentDr = LegacyPayloadReader.ReadInt(evt, "content_dr", 0),
                    GuardBlock = LegacyPayloadReader.ReadInt(evt, "guard_block", 0),
                    GuardIgnoreApplied = LegacyPayloadReader.ReadInt(evt, "guard_ignore_applied", 0),
                    FixedMitigationTotal = LegacyPayloadReader.ReadInt(evt, "fixed_mitigation_total", 0),
                    DamageDiceHighTotalRoll = ReadExactBooleanField(
                        evt,
                        "damage_dice_high_total_roll",
                        false
                    ),
                    SkillDamageDiceIsMax = ReadExactBooleanField(
                        evt,
                        "skill_damage_dice_is_max",
                        false
                    ),
                    SkillDamageDiceIsMaxReason = ParseDamageDiceMaxReason(
                        LegacyPayloadReader.ReadStringName(evt, "skill_damage_dice_is_max_reason")
                    ),
                    WeaponDamageDiceIsMax = ReadExactBooleanField(
                        evt,
                        "weapon_damage_dice_is_max",
                        false
                    ),
                    WeaponDamageDiceIsMaxReason = ParseDamageDiceMaxReason(
                        LegacyPayloadReader.ReadStringName(evt, "weapon_damage_dice_is_max_reason")
                    ),
                    HalfSourceLabels = halfSourceLabels,
                    DoubleSourceLabels = doubleSourceLabels,
                    ImmuneSourceLabels = immuneSourceLabels,
                    FixedMitigationSourceLabels = ReadFixedMitigationSourceLabels(evt),
                }
            );
        }
        return results.ToArray();
    }

    private static EquipmentDurabilityEventResult[] ReadEquipmentDurabilityEvents(GDictionary source)
    {
        var results = new List<EquipmentDurabilityEventResult>();
        foreach (Variant eventValue in LegacyPayloadReader.ReadArray(source, "equipment_durability_events"))
        {
            if (eventValue.VariantType != Variant.Type.Dictionary)
                continue;
            GDictionary evt = eventValue.AsGodotDictionary();
            results.Add(
                new EquipmentDurabilityEventResult
                {
                    EquipmentInstanceId = LegacyPayloadReader.ReadStringName(evt, "equipment_instance_id"),
                    ItemId = LegacyPayloadReader.ReadString(evt, "item_id", ""),
                    DurabilityLoss = LegacyPayloadReader.ReadInt(evt, "durability_loss", 0),
                    DurabilityBefore = LegacyPayloadReader.ReadInt(evt, "durability_before", 0),
                    DurabilityAfter = LegacyPayloadReader.ReadInt(evt, "durability_after", 0),
                    Destroyed = ReadExactBooleanField(evt, "destroyed", false),
                    SaveResult = ReadSaveResolution(
                        LegacyPayloadReader.ReadDictionary(evt, "save_result")
                    ),
                }
            );
        }
        return results.ToArray();
    }

    private static DispelEventResult[] ReadDispelEvents(GDictionary source)
    {
        var results = new List<DispelEventResult>();
        foreach (Variant eventValue in LegacyPayloadReader.ReadArray(source, "dispel_events"))
        {
            if (eventValue.VariantType != Variant.Type.Dictionary)
                continue;
            results.Add(
                new DispelEventResult
                {
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
        foreach (Variant saveValue in LegacyPayloadReader.ReadArray(source, "save_results"))
        {
            if (saveValue.VariantType != Variant.Type.Dictionary)
                continue;
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
            Roll = LegacyPayloadReader.ReadInt(save, "roll", 0),
            Total = LegacyPayloadReader.ReadInt(save, "total", 0),
            Dc = LegacyPayloadReader.ReadInt(save, "dc", 0),
            SaveKind = LegacyPayloadReader.ReadStringName(save, "save_kind"),
        };
    }

    private static ResolutionDiagnostic[] ReadDiagnostics(GDictionary source)
    {
        var results = new List<ResolutionDiagnostic>();
        foreach (Variant diagnosticValue in LegacyPayloadReader.ReadArray(source, "diagnostics"))
        {
            if (diagnosticValue.VariantType != Variant.Type.Dictionary)
                continue;
            GDictionary diagnostic = diagnosticValue.AsGodotDictionary();
            results.Add(
                new ResolutionDiagnostic
                {
                    ErrorCode = LegacyPayloadReader.ReadString(diagnostic, "error_code", ""),
                    Message = LegacyPayloadReader.ReadString(diagnostic, "message", ""),
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

    private static void ReadMitigationSourceLabels(
        GDictionary damageEvent,
        out string[] halfSourceLabels,
        out string[] doubleSourceLabels,
        out string[] immuneSourceLabels
    )
    {
        var half = new List<string>();
        var @double = new List<string>();
        var immune = new List<string>();
        foreach (Variant sourceValue in LegacyPayloadReader.ReadArray(damageEvent, "mitigation_sources"))
        {
            if (sourceValue.VariantType != Variant.Type.Dictionary)
                continue;
            GDictionary source = sourceValue.AsGodotDictionary();
            string label = FormatDamageSourceLabel(source);
            if (string.IsNullOrEmpty(label))
                continue;
            switch (ParseMitigationTier(LegacyPayloadReader.ReadStringName(source, "tier")))
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

    private static string[] ReadFixedMitigationSourceLabels(GDictionary damageEvent)
    {
        var labels = new List<string>();
        foreach (Variant sourceValue in LegacyPayloadReader.ReadArray(damageEvent, "fixed_mitigation_sources"))
        {
            if (sourceValue.VariantType != Variant.Type.Dictionary)
                continue;
            string label = FormatDamageSourceLabel(sourceValue.AsGodotDictionary());
            if (!string.IsNullOrEmpty(label))
                AppendUnique(labels, label);
        }
        return labels.ToArray();
    }

    private static string FormatDamageSourceLabel(GDictionary source)
    {
        string statusId = LegacyPayloadReader.ReadString(source, "status_id", "");
        if (!string.IsNullOrEmpty(statusId))
            return statusId;
        return LegacyPayloadReader.ReadString(source, "type", "");
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
    internal static BattleReportEntry ReadLegacy(GDictionary entry)
    {
        entry ??= new GDictionary();
        GDictionary luckSnapshot = LegacyPayloadReader.ReadDictionary(entry, "luck_snapshot");
        return new BattleReportEntry
        {
            EntryKind = ParseReportEntryKind(LegacyPayloadReader.ReadStringName(entry, "entry_type")),
            ReasonId = LegacyPayloadReader.ReadStringName(entry, "reason_id"),
            Text = LegacyPayloadReader.ReadString(entry, "text", ""),
            EventTags = ReadStringNameArray(entry, "event_tags"),
            AttackerId = LegacyPayloadReader.ReadStringName(entry, "attacker_id"),
            AttackerMemberId = LegacyPayloadReader.ReadStringName(entry, "attacker_member_id"),
            AttackerName = LegacyPayloadReader.ReadString(entry, "attacker_name", ""),
            DefenderId = LegacyPayloadReader.ReadStringName(entry, "defender_id"),
            DefenderMemberId = LegacyPayloadReader.ReadStringName(entry, "defender_member_id"),
            DefenderName = LegacyPayloadReader.ReadString(entry, "defender_name", ""),
            DefenderIsEliteOrBoss = AttackEffectResolutionResultReader.ReadExactBooleanField(
                entry,
                "defender_is_elite_or_boss",
                false
            ),
            AttackResolution = AttackEffectResolutionResultReader.ParseAttackResolution(
                LegacyPayloadReader.ReadStringName(entry, "attack_resolution")
            ),
            CriticalSource = AttackEffectResolutionResultReader.ParseCriticalSource(
                LegacyPayloadReader.ReadStringName(entry, "critical_source")
            ),
            IsDisadvantage = AttackEffectResolutionResultReader.ReadExactBooleanField(
                entry,
                "is_disadvantage",
                false
            ),
            CritGateDie = LegacyPayloadReader.ReadInt(entry, "crit_gate_die", 0),
            CritGateRoll = LegacyPayloadReader.ReadInt(entry, "crit_gate_roll", 0),
            HitRoll = LegacyPayloadReader.ReadInt(entry, "hit_roll", 0),
            RequiredRoll = LegacyPayloadReader.ReadInt(entry, "required_roll", 0),
            DisplayRequiredRoll = LegacyPayloadReader.ReadInt(entry, "display_required_roll", 0),
            HiddenLuckAtBirth = LegacyPayloadReader.ReadInt(luckSnapshot, "hidden_luck_at_birth", 0),
            FaithLuckBonus = LegacyPayloadReader.ReadInt(luckSnapshot, "faith_luck_bonus", 0),
            EffectiveLuck = LegacyPayloadReader.ReadInt(luckSnapshot, "effective_luck", 0),
            FumbleLowEnd = LegacyPayloadReader.ReadInt(luckSnapshot, "fumble_low_end", 0),
            CritThreshold = LegacyPayloadReader.ReadInt(luckSnapshot, "crit_threshold", 0),
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
        foreach (Variant value in LegacyPayloadReader.ReadArray(source, key))
        {
            StringName normalized = ProgressionDataUtils.to_string_name(value);
            if (normalized == "" || result.Contains(normalized))
                continue;
            result.Add(normalized);
        }
        return result;
    }
}
