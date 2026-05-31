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
    public int DurabilityLoss;
    public bool Destroyed;
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
            Applied = GdInterop.GetBool(source, "applied", false),
            Damage = GdInterop.GetInt(source, "damage", 0),
            HpDamage = GdInterop.GetInt(source, "hp_damage", GdInterop.GetInt(source, "damage", 0)),
            Healing = GdInterop.GetInt(source, "healing", 0),
            ShieldAbsorbed = GdInterop.GetInt(source, "shield_absorbed", 0),
            ShieldBroken = GdInterop.GetBool(source, "shield_broken", false),
            AttackSuccess = GdInterop.GetBool(source, "attack_success", false),
            AttackResolution = ParseAttackResolution(GdInterop.GetStringName(source, "attack_resolution")),
            CriticalHit = GdInterop.GetBool(source, "critical_hit", false),
            CriticalFail = GdInterop.GetBool(source, "critical_fail", false),
            SecondaryHitSuccess = GdInterop.GetBool(source, "secondary_hit_success", false),
            CriticalSource = ParseCriticalSource(GdInterop.GetStringName(source, "critical_source")),
            ReverseFateDowngraded = GdInterop.GetBool(source, "reverse_fate_downgraded", false),
            HitRoll = GdInterop.GetInt(source, "hit_roll", 0),
            RerollDie = GdInterop.GetInt(source, "reroll_die", 0),
            RerolledRoll = GdInterop.GetInt(source, "rerolled_roll", 0),
            CritGateDie = GdInterop.GetInt(source, "crit_gate_die", 0),
            CritGateRoll = GdInterop.GetInt(source, "crit_gate_roll", 0),
            RequiredRoll = GdInterop.GetInt(source, "required_roll", attackCheck.RequiredRoll),
            DisplayRequiredRoll = GdInterop.GetInt(
                source,
                "display_required_roll",
                attackCheck.DisplayRequiredRoll
            ),
            HitRatePercent = GdInterop.GetInt(source, "hit_rate_percent", attackCheck.HitRatePercent),
            SuccessRatePercent = GdInterop.GetInt(
                source,
                "success_rate_percent",
                attackCheck.SuccessRatePercent
            ),
            ResolutionText = GdInterop.GetString(source, "resolution_text", ""),
            SkillId = GdInterop.GetStringName(source, "skill_id", attackCheck.SkillId),
            StatusEffectIds = ReadStringNameArray(source, "status_effect_ids"),
            RemovedStatusEffectIds = ReadStringNameArray(source, "removed_status_effect_ids"),
            SourceStatusEffectIds = ReadStringNameArray(source, "source_status_effect_ids"),
            TerrainEffectIds = ReadStringNameArray(source, "terrain_effect_ids"),
            HeightDelta = GdInterop.GetInt(source, "height_delta", 0),
            ExecuteStage = GdInterop.GetInt(source, "execute_stage", -1),
            ExecuteOutcome = ParseExecuteOutcome(GdInterop.GetStringName(source, "execute_outcome")),
            ErrorCode = GdInterop.GetString(source, "error_code", ""),
            BlockedReason = GdInterop.GetString(source, "blocked_reason", ""),
            AttackCheck = attackCheck,
            DamageEvents = ReadDamageEvents(source),
            EquipmentDurabilityEvents = ReadEquipmentDurabilityEvents(source),
            DispelEvents = ReadDispelEvents(source),
            SaveResults = ReadSaveResults(source),
            Diagnostics = ReadDiagnostics(source),
            ReportEntry = BattleReportEntryPayload.ReadLegacy(
                GdInterop.GetDictionary(source, "report_entry")
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
        foreach (Variant value in GdInterop.GetArray(source, key))
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
        foreach (Variant eventValue in GdInterop.GetArray(source, "damage_events"))
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
                    Damage = GdInterop.GetInt(evt, "damage", 0),
                    HpDamage = GdInterop.GetInt(evt, "hp_damage", GdInterop.GetInt(evt, "damage", 0)),
                    ShieldAbsorbed = GdInterop.GetInt(evt, "shield_absorbed", 0),
                    ShieldBroken = GdInterop.GetBool(evt, "shield_broken", false),
                    BypassShield = GdInterop.GetBool(evt, "bypass_shield", false),
                    MitigationTier = ParseMitigationTier(
                        GdInterop.GetStringName(evt, "mitigation_tier")
                    ),
                    BuffReduction = GdInterop.GetInt(evt, "buff_reduction", 0),
                    StanceReduction = GdInterop.GetInt(evt, "stance_reduction", 0),
                    PassiveReduction = GdInterop.GetInt(evt, "passive_reduction", 0),
                    ContentDr = GdInterop.GetInt(evt, "content_dr", 0),
                    GuardBlock = GdInterop.GetInt(evt, "guard_block", 0),
                    GuardIgnoreApplied = GdInterop.GetInt(evt, "guard_ignore_applied", 0),
                    FixedMitigationTotal = GdInterop.GetInt(evt, "fixed_mitigation_total", 0),
                    DamageDiceHighTotalRoll = GdInterop.GetBool(
                        evt,
                        "damage_dice_high_total_roll",
                        false
                    ),
                    SkillDamageDiceIsMax = GdInterop.GetBool(
                        evt,
                        "skill_damage_dice_is_max",
                        false
                    ),
                    SkillDamageDiceIsMaxReason = ParseDamageDiceMaxReason(
                        GdInterop.GetStringName(evt, "skill_damage_dice_is_max_reason")
                    ),
                    WeaponDamageDiceIsMax = GdInterop.GetBool(
                        evt,
                        "weapon_damage_dice_is_max",
                        false
                    ),
                    WeaponDamageDiceIsMaxReason = ParseDamageDiceMaxReason(
                        GdInterop.GetStringName(evt, "weapon_damage_dice_is_max_reason")
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
        foreach (Variant eventValue in GdInterop.GetArray(source, "equipment_durability_events"))
        {
            if (eventValue.VariantType != Variant.Type.Dictionary)
                continue;
            GDictionary evt = eventValue.AsGodotDictionary();
            results.Add(
                new EquipmentDurabilityEventResult
                {
                    EquipmentInstanceId = GdInterop.GetStringName(evt, "equipment_instance_id"),
                    DurabilityLoss = GdInterop.GetInt(evt, "durability_loss", 0),
                    Destroyed = GdInterop.GetBool(evt, "destroyed", false),
                }
            );
        }
        return results.ToArray();
    }

    private static DispelEventResult[] ReadDispelEvents(GDictionary source)
    {
        var results = new List<DispelEventResult>();
        foreach (Variant eventValue in GdInterop.GetArray(source, "dispel_events"))
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
        foreach (Variant saveValue in GdInterop.GetArray(source, "save_results"))
        {
            if (saveValue.VariantType != Variant.Type.Dictionary)
                continue;
            GDictionary save = saveValue.AsGodotDictionary();
            results.Add(
                new SaveResolutionResult
                {
                    HasSave = GdInterop.GetBool(save, "has_save", false),
                    Success = GdInterop.GetBool(save, "success", false),
                    Roll = GdInterop.GetInt(save, "roll", 0),
                    Total = GdInterop.GetInt(save, "total", 0),
                    Dc = GdInterop.GetInt(save, "dc", 0),
                    SaveKind = GdInterop.GetStringName(save, "save_kind"),
                }
            );
        }
        return results.ToArray();
    }

    private static ResolutionDiagnostic[] ReadDiagnostics(GDictionary source)
    {
        var results = new List<ResolutionDiagnostic>();
        foreach (Variant diagnosticValue in GdInterop.GetArray(source, "diagnostics"))
        {
            if (diagnosticValue.VariantType != Variant.Type.Dictionary)
                continue;
            GDictionary diagnostic = diagnosticValue.AsGodotDictionary();
            results.Add(
                new ResolutionDiagnostic
                {
                    ErrorCode = GdInterop.GetString(diagnostic, "error_code", ""),
                    Message = GdInterop.GetString(diagnostic, "message", ""),
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
        result.DamageDiceHighTotalRoll = GdInterop.GetBool(
            source,
            "damage_dice_high_total_roll",
            false
        );
        result.SkillDamageDiceIsMax = GdInterop.GetBool(source, "skill_damage_dice_is_max", false);
        result.WeaponDamageDiceIsMax = GdInterop.GetBool(source, "weapon_damage_dice_is_max", false);

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
        foreach (Variant sourceValue in GdInterop.GetArray(damageEvent, "mitigation_sources"))
        {
            if (sourceValue.VariantType != Variant.Type.Dictionary)
                continue;
            GDictionary source = sourceValue.AsGodotDictionary();
            string label = FormatDamageSourceLabel(source);
            if (string.IsNullOrEmpty(label))
                continue;
            switch (ParseMitigationTier(GdInterop.GetStringName(source, "tier")))
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
        foreach (Variant sourceValue in GdInterop.GetArray(damageEvent, "fixed_mitigation_sources"))
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
        string statusId = GdInterop.GetString(source, "status_id", "");
        if (!string.IsNullOrEmpty(statusId))
            return statusId;
        return GdInterop.GetString(source, "type", "");
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
        GDictionary luckSnapshot = GdInterop.GetDictionary(entry, "luck_snapshot");
        return new BattleReportEntry
        {
            EntryKind = ParseReportEntryKind(GdInterop.GetStringName(entry, "entry_type")),
            ReasonId = GdInterop.GetStringName(entry, "reason_id"),
            Text = GdInterop.GetString(entry, "text", ""),
            EventTags = ReadStringNameArray(entry, "event_tags"),
            AttackerId = GdInterop.GetStringName(entry, "attacker_id"),
            AttackerMemberId = GdInterop.GetStringName(entry, "attacker_member_id"),
            AttackerName = GdInterop.GetString(entry, "attacker_name", ""),
            DefenderId = GdInterop.GetStringName(entry, "defender_id"),
            DefenderMemberId = GdInterop.GetStringName(entry, "defender_member_id"),
            DefenderName = GdInterop.GetString(entry, "defender_name", ""),
            DefenderIsEliteOrBoss = GdInterop.GetBool(entry, "defender_is_elite_or_boss", false),
            AttackResolution = AttackEffectResolutionResultReader.ParseAttackResolution(
                GdInterop.GetStringName(entry, "attack_resolution")
            ),
            CriticalSource = AttackEffectResolutionResultReader.ParseCriticalSource(
                GdInterop.GetStringName(entry, "critical_source")
            ),
            IsDisadvantage = GdInterop.GetBool(entry, "is_disadvantage", false),
            CritGateDie = GdInterop.GetInt(entry, "crit_gate_die", 0),
            CritGateRoll = GdInterop.GetInt(entry, "crit_gate_roll", 0),
            HitRoll = GdInterop.GetInt(entry, "hit_roll", 0),
            RequiredRoll = GdInterop.GetInt(entry, "required_roll", 0),
            DisplayRequiredRoll = GdInterop.GetInt(entry, "display_required_roll", 0),
            HiddenLuckAtBirth = GdInterop.GetInt(luckSnapshot, "hidden_luck_at_birth", 0),
            FaithLuckBonus = GdInterop.GetInt(luckSnapshot, "faith_luck_bonus", 0),
            EffectiveLuck = GdInterop.GetInt(luckSnapshot, "effective_luck", 0),
            FumbleLowEnd = GdInterop.GetInt(luckSnapshot, "fumble_low_end", 0),
            CritThreshold = GdInterop.GetInt(luckSnapshot, "crit_threshold", 0),
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
        foreach (Variant value in GdInterop.GetArray(source, key))
        {
            StringName normalized = ProgressionDataUtils.to_string_name(value);
            if (normalized == "" || result.Contains(normalized))
                continue;
            result.Add(normalized);
        }
        return result;
    }
}
