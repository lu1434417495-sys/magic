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
            RequiredRoll = PayloadReader.ReadInt(source, "required_roll", attackCheck.RequiredRoll),
            DisplayRequiredRoll = PayloadReader.ReadInt(
                source,
                "display_required_roll",
                attackCheck.DisplayRequiredRoll
            ),
            HitRatePercent = PayloadReader.ReadInt(source, "hit_rate_percent", attackCheck.HitRatePercent),
            SuccessRatePercent = PayloadReader.ReadInt(
                source,
                "success_rate_percent",
                attackCheck.SuccessRatePercent
            ),
            ResolutionText = PayloadReader.ReadString(source, "resolution_text", ""),
            SkillId = PayloadReader.ReadStringName(source, "skill_id", attackCheck.SkillId),
            StatusEffectIds = ReadStringNameArray(source, "status_effect_ids"),
            RemovedStatusEffectIds = ReadStringNameArray(source, "removed_status_effect_ids"),
            SourceStatusEffectIds = ReadStringNameArray(source, "source_status_effect_ids"),
            TerrainEffectIds = ReadStringNameArray(source, "terrain_effect_ids"),
            HeightDelta = PayloadReader.ReadInt(source, "height_delta", 0),
            ExecuteStage = PayloadReader.ReadInt(source, "execute_stage", -1),
            ExecuteOutcome = ParseExecuteOutcome(PayloadReader.ReadStringName(source, "execute_outcome")),
            ErrorCode = PayloadReader.ReadString(source, "error_code", ""),
            BlockedReason = PayloadReader.ReadString(source, "blocked_reason", ""),
            AttackCheck = attackCheck,
            DamageEvents = ReadDamageEvents(source),
            EquipmentDurabilityEvents = ReadEquipmentDurabilityEvents(source),
            DispelEvents = ReadDispelEvents(source),
            SaveResults = ReadSaveResults(source),
            Diagnostics = ReadDiagnostics(source),
            ReportEntry = BattleReportEntryPayload.ReadLegacy(
                PayloadReader.ReadDictionary(source, "report_entry")
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
        string key,
        bool fallback = false
    )
    {
        if (source == null || string.IsNullOrEmpty(key) || !source.ContainsKey(key))
            return fallback;
        return source[key].AsBool();
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

    private static DamageEventResult[] ReadDamageEvents(GDictionary source)
    {
        var results = new List<DamageEventResult>();
        foreach (var eventValue in PayloadReader.ReadArray(source, "damage_events"))
        {
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
                    Damage = PayloadReader.ReadInt(evt, "damage", 0),
                    HpDamage = PayloadReader.ReadInt(evt, "hp_damage", PayloadReader.ReadInt(evt, "damage", 0)),
                    ShieldAbsorbed = PayloadReader.ReadInt(evt, "shield_absorbed", 0),
                    ShieldBroken = ReadExactBooleanField(evt, "shield_broken", false),
                    BypassShield = ReadExactBooleanField(evt, "bypass_shield", false),
                    MitigationTier = ParseMitigationTier(
                        PayloadReader.ReadStringName(evt, "mitigation_tier")
                    ),
                    BuffReduction = PayloadReader.ReadInt(evt, "buff_reduction", 0),
                    StanceReduction = PayloadReader.ReadInt(evt, "stance_reduction", 0),
                    PassiveReduction = PayloadReader.ReadInt(evt, "passive_reduction", 0),
                    ContentDr = PayloadReader.ReadInt(evt, "content_dr", 0),
                    GuardBlock = PayloadReader.ReadInt(evt, "guard_block", 0),
                    GuardIgnoreApplied = PayloadReader.ReadInt(evt, "guard_ignore_applied", 0),
                    FixedMitigationTotal = PayloadReader.ReadInt(evt, "fixed_mitigation_total", 0),
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
        foreach (var eventValue in PayloadReader.ReadArray(source, "equipment_durability_events"))
        {
            GDictionary evt = eventValue.AsGodotDictionary();
            results.Add(
                new EquipmentDurabilityEventResult
                {
                    EquipmentInstanceId = PayloadReader.ReadStringName(evt, "equipment_instance_id"),
                    ItemId = PayloadReader.ReadString(evt, "item_id", ""),
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
            Roll = PayloadReader.ReadInt(save, "roll", 0),
            Total = PayloadReader.ReadInt(save, "total", 0),
            Dc = PayloadReader.ReadInt(save, "dc", 0),
            SaveKind = PayloadReader.ReadStringName(save, "save_kind"),
        };
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

    private static string[] ReadFixedMitigationSourceLabels(GDictionary damageEvent)
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

