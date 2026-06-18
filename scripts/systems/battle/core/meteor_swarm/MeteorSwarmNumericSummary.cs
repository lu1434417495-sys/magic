using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictArray = Godot.Collections.Array<Godot.Collections.Dictionary>;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public sealed class MeteorSwarmHostileTerrainConsequence
{
    public int MoveCostDelta;
    public bool CreatesDust;
    public bool CreatesCrater;
    public bool CreatesRubble;

    public bool HasProtectedAllyConsequence =>
        MoveCostDelta > 0 || CreatesDust || CreatesCrater || CreatesRubble;

    internal Dictionary<string, object> ToTraceDictionary()
    {
        return new Dictionary<string, object>(System.StringComparer.Ordinal)
        {
            ["move_cost_delta"] = MoveCostDelta,
            ["creates_dust"] = CreatesDust,
            ["creates_crater"] = CreatesCrater,
            ["creates_rubble"] = CreatesRubble,
        };
    }

    internal static MeteorSwarmHostileTerrainConsequence FromDictionary(GDictionary source)
    {
        source ??= new GDictionary();
        return new MeteorSwarmHostileTerrainConsequence
        {
            MoveCostDelta = ReadInt(source, "move_cost_delta"),
            CreatesDust = ReadBool(source, "creates_dust"),
            CreatesCrater = ReadBool(source, "creates_crater"),
            CreatesRubble = ReadBool(source, "creates_rubble"),
        };
    }

    private static int ReadInt(GDictionary source, string key, int fallback = 0)
    {
        if (!TryRead(source, key, out dynamic value))
            return fallback;
        return value.AsInt32();
    }

    private static string ReadString(GDictionary source, string key, string fallback = "")
    {
        if (!TryRead(source, key, out dynamic value))
            return fallback;
        string text = value.ToString();
        return string.IsNullOrEmpty(text) ? fallback : text;
    }

    private static bool ReadBool(GDictionary source, string key, bool fallback = false)
    {
        if (!TryRead(source, key, out dynamic value))
            return fallback;
        return value.AsBool();
    }

    private static bool TryRead(GDictionary source, string key, out dynamic value)
    {
        value = default;
        if (source == null || key == null)
            return false;
        if (source.ContainsKey(key))
        {
            value = source[key];
            return true;
        }
        return false;
    }
}

public sealed class MeteorSwarmComponentBreakdownEntry
{
    public StringName ComponentId = "";
    public StringName RoleLabel = "";
    public StringName DamageTag = "";
    public int ExpectedDamage;
    public int WorstCaseDamage;
    public int PostSaveExpectedDamage;
    public int PostSaveWorstCaseDamage;
    public int PreSaveExpectedDamage;
    public int PreSaveWorstCaseDamage;
    public StringName ResistanceTier = "";
    public string SaveProfileId = "";
    public BattleDamagePreviewSaveEstimate SaveEstimate =
        BattleDamagePreviewSaveEstimate.None(0);
    public BattleDamagePreviewSaveEstimate WorstSaveEstimate =
        BattleDamagePreviewSaveEstimate.None(0);
    public List<string> HalfSourceLabels = new();
    public List<string> DoubleSourceLabels = new();
    public List<string> ImmuneSourceLabels = new();
    public List<string> FixedMitigationSourceLabels = new();
    public int ShieldAbsorbedEstimate;
    public int ShieldAbsorbedWorst;

    internal Dictionary<string, object> ToTraceDictionary()
    {
        return new Dictionary<string, object>(System.StringComparer.Ordinal)
        {
            ["component_id"] = ComponentId.ToString(),
            ["role_label"] = RoleLabel.ToString(),
            ["damage_tag"] = DamageTag.ToString(),
            ["expected_damage"] = ExpectedDamage,
            ["worst_case_damage"] = WorstCaseDamage,
            ["post_save_expected_damage"] = PostSaveExpectedDamage,
            ["post_save_worst_case_damage"] = PostSaveWorstCaseDamage,
            ["pre_save_expected_damage"] = PreSaveExpectedDamage,
            ["pre_save_worst_case_damage"] = PreSaveWorstCaseDamage,
            ["resistance_tier"] = ResistanceTier.ToString(),
            ["save_profile_id"] = SaveProfileId ?? "",
            ["save_estimate"] = SaveEstimate?.ToTraceDictionary()
                ?? new Dictionary<string, object>(System.StringComparer.Ordinal),
            ["worst_save_estimate"] = WorstSaveEstimate?.ToTraceDictionary()
                ?? new Dictionary<string, object>(System.StringComparer.Ordinal),
            ["half_source_labels"] = new List<string>(HalfSourceLabels ?? new List<string>()),
            ["double_source_labels"] = new List<string>(DoubleSourceLabels ?? new List<string>()),
            ["immune_source_labels"] = new List<string>(ImmuneSourceLabels ?? new List<string>()),
            ["fixed_mitigation_source_labels"] = new List<string>(
                FixedMitigationSourceLabels ?? new List<string>()
            ),
            ["shield_absorbed_estimate"] = ShieldAbsorbedEstimate,
            ["shield_absorbed_worst"] = ShieldAbsorbedWorst,
        };
    }
}

public sealed class MeteorSwarmNumericSummary
{
    public Vector2I CandidateAnchorCoord = new(-1, -1);
    public StringName TargetUnitId = "";
    public StringName AllyUnitId = "";
    public StringName TargetFactionId = "";
    public bool IsAlly;
    public int DistanceFromAnchor = -1;
    public int ComponentExpectedDamage;
    public int ComponentWorstCaseDamage;
    public List<MeteorSwarmComponentBreakdownEntry> ComponentBreakdown = new();
    public int LethalProbabilityPercent;
    public List<string> SaveProfileIds = new();
    public Dictionary<StringName, StringName> ResistanceTiersByDamageTag = new();
    public int ShieldHp;
    public int GuardBlockEstimate;
    public List<StringName> StatusEffectIds = new();
    public int ApPenalty;
    public MeteorSwarmHostileTerrainConsequence HostileTerrain = new();
    public int ExpectedDamageHpPercent;
    public int WorstCaseDamageHpPercent;
    public bool HardReject;
    public bool SoftPenalty;
    public IReadOnlyList<MeteorSwarmComponentBreakdownEntry> Components => ComponentBreakdown;

    public int StatusEffectCount => StatusEffectIds?.Count ?? 0;

    public bool HasCenterDirect
    {
        get
        {
            foreach (MeteorSwarmComponentBreakdownEntry component in ComponentBreakdown)
            {
                if (component?.ComponentId == "center_direct")
                    return true;
            }
            return false;
        }
    }

    internal Dictionary<string, object> ToTraceDictionary()
    {
        return new Dictionary<string, object>(System.StringComparer.Ordinal)
        {
            ["candidate_anchor_coord"] = CandidateAnchorCoord,
            ["target_unit_id"] = TargetUnitId.ToString(),
            ["ally_unit_id"] = AllyUnitId.ToString(),
            ["target_faction_id"] = TargetFactionId.ToString(),
            ["is_ally"] = IsAlly,
            ["distance_from_anchor"] = DistanceFromAnchor,
            ["component_expected_damage"] = ComponentExpectedDamage,
            ["component_worst_case_damage"] = ComponentWorstCaseDamage,
            ["component_breakdown"] = BuildTraceComponentBreakdownList(ComponentBreakdown),
            ["lethal_probability_percent"] = LethalProbabilityPercent,
            ["save_profile_ids"] = new List<string>(SaveProfileIds ?? new List<string>()),
            ["resistance_tiers_by_damage_tag"] = BuildTraceResistanceTierDictionary(
                ResistanceTiersByDamageTag
            ),
            ["shield_hp"] = ShieldHp,
            ["guard_block_estimate"] = GuardBlockEstimate,
            ["status_effect_ids"] = new List<StringName>(
                StatusEffectIds ?? new List<StringName>()
            ),
            ["ap_penalty"] = ApPenalty,
            ["hostile_terrain_consequence"] =
                HostileTerrain?.ToTraceDictionary()
                ?? new Dictionary<string, object>(System.StringComparer.Ordinal),
            ["expected_damage_hp_percent"] = ExpectedDamageHpPercent,
            ["worst_case_damage_hp_percent"] = WorstCaseDamageHpPercent,
            ["hard_reject"] = HardReject,
            ["soft_penalty"] = SoftPenalty,
        };
    }

    internal static MeteorSwarmNumericSummary FromDictionary(GDictionary source)
    {
        source ??= new GDictionary();
        List<MeteorSwarmComponentBreakdownEntry> componentBreakdown = ReadComponents(
            ReadDictArray(source, "component_breakdown")
        );
        var summary = new MeteorSwarmNumericSummary
        {
            CandidateAnchorCoord = ReadVector2I(
                source,
                "candidate_anchor_coord",
                new Vector2I(-1, -1)
            ),
            TargetUnitId = ReadStringName(
                source,
                "target_unit_id",
                ReadStringName(source, "ally_unit_id")
            ),
            AllyUnitId = ReadStringName(source, "ally_unit_id"),
            TargetFactionId = ReadStringName(source, "target_faction_id"),
            IsAlly = ReadBool(source, "is_ally"),
            DistanceFromAnchor = ReadInt(source, "distance_from_anchor", -1),
            ComponentExpectedDamage = Math.Max(ReadInt(source, "component_expected_damage"), 0),
            ComponentWorstCaseDamage = Math.Max(
                ReadInt(source, "component_worst_case_damage"),
                0
            ),
            ComponentBreakdown = componentBreakdown,
            LethalProbabilityPercent = ReadInt(source, "lethal_probability_percent"),
            SaveProfileIds = ReadStringList(source, "save_profile_ids"),
            ResistanceTiersByDamageTag = ReadResistanceTierDictionary(
                source,
                "resistance_tiers_by_damage_tag"
            ),
            ShieldHp = ReadInt(source, "shield_hp"),
            GuardBlockEstimate = ReadInt(source, "guard_block_estimate"),
            StatusEffectIds = ReadStringNameList(source, "status_effect_ids"),
            ApPenalty = ReadInt(source, "ap_penalty"),
            HostileTerrain = MeteorSwarmHostileTerrainConsequence.FromDictionary(
                ReadDictionary(source, "hostile_terrain_consequence")
            ),
            ExpectedDamageHpPercent = ReadInt(source, "expected_damage_hp_percent"),
            WorstCaseDamageHpPercent = ReadInt(source, "worst_case_damage_hp_percent"),
            HardReject = ReadBool(source, "hard_reject"),
            SoftPenalty = ReadBool(source, "soft_penalty"),
        };
        return summary;
    }

    private static List<MeteorSwarmComponentBreakdownEntry> ReadComponents(
        GDictArray componentBreakdown
    )
    {
        var result = new List<MeteorSwarmComponentBreakdownEntry>();
        foreach (GDictionary component in componentBreakdown ?? new GDictArray())
        {
            AttackEffectResolutionResultReader.ReadMitigationSourceLabels(
                component,
                out string[] halfSourceLabels,
                out string[] doubleSourceLabels,
                out string[] immuneSourceLabels
            );
            result.Add(
                new MeteorSwarmComponentBreakdownEntry
                {
                    ComponentId = ReadStringName(component, "component_id"),
                    RoleLabel = ReadStringName(component, "role_label"),
                    DamageTag = ReadStringName(component, "damage_tag"),
                    ExpectedDamage = ReadInt(component, "expected_damage"),
                    WorstCaseDamage = ReadInt(component, "worst_case_damage"),
                    PostSaveExpectedDamage = ReadInt(component, "post_save_expected_damage"),
                    PostSaveWorstCaseDamage = ReadInt(
                        component,
                        "post_save_worst_case_damage"
                    ),
                    PreSaveExpectedDamage = ReadInt(component, "pre_save_expected_damage"),
                    PreSaveWorstCaseDamage = ReadInt(
                        component,
                        "pre_save_worst_case_damage"
                    ),
                    ResistanceTier = ReadStringName(component, "resistance_tier"),
                    SaveProfileId = ReadString(component, "save_profile_id"),
                    SaveEstimate = ReadSaveEstimate(component, "save_estimate"),
                    WorstSaveEstimate = ReadSaveEstimate(component, "worst_save_estimate"),
                    HalfSourceLabels = new List<string>(halfSourceLabels),
                    DoubleSourceLabels = new List<string>(doubleSourceLabels),
                    ImmuneSourceLabels = new List<string>(immuneSourceLabels),
                    FixedMitigationSourceLabels = new List<string>(
                        AttackEffectResolutionResultReader.ReadFixedMitigationSourceLabels(
                            component
                        )
                    ),
                    ShieldAbsorbedEstimate = ReadInt(component, "shield_absorbed_estimate"),
                    ShieldAbsorbedWorst = ReadInt(component, "shield_absorbed_worst"),
                }
            );
        }
        return result;
    }

    private static List<object> BuildTraceComponentBreakdownList(
        IEnumerable<MeteorSwarmComponentBreakdownEntry> values
    )
    {
        var result = new List<object>();
        foreach (
            MeteorSwarmComponentBreakdownEntry value
            in values ?? Array.Empty<MeteorSwarmComponentBreakdownEntry>()
        )
        {
            if (value != null)
            {
                result.Add(value.ToTraceDictionary());
            }
        }
        return result;
    }

    private static Dictionary<string, object> BuildTraceResistanceTierDictionary(
        Dictionary<StringName, StringName> values
    )
    {
        var result = new Dictionary<string, object>(System.StringComparer.Ordinal);
        foreach (
            KeyValuePair<StringName, StringName> entry
            in values ?? new Dictionary<StringName, StringName>()
        )
        {
            if (entry.Key != "" && entry.Value != "")
            {
                result[entry.Key.ToString()] = entry.Value.ToString();
            }
        }
        return result;
    }

    private static int ReadInt(GDictionary source, string key, int fallback = 0)
    {
        if (!TryRead(source, key, out dynamic value))
            return fallback;
        return value.AsInt32();
    }

    private static string ReadString(GDictionary source, string key, string fallback = "")
    {
        if (!TryRead(source, key, out dynamic value))
            return fallback;
        string text = value.ToString();
        return string.IsNullOrEmpty(text) ? fallback : text;
    }

    private static bool ReadBool(GDictionary source, string key, bool fallback = false)
    {
        if (!TryRead(source, key, out dynamic value))
            return fallback;
        return value.AsBool();
    }

    private static StringName ReadStringName(
        GDictionary source,
        string key,
        StringName fallback = default
    )
    {
        if (!TryRead(source, key, out dynamic value))
            return fallback ?? "";
        StringName normalized = ProgressionDataUtils.to_string_name(value);
        return normalized == "" ? fallback ?? "" : normalized;
    }

    private static Vector2I ReadVector2I(GDictionary source, string key, Vector2I fallback)
    {
        if (!TryRead(source, key, out dynamic value))
            return fallback;
        return value.AsVector2I();
    }

    private static GDictionary ReadDictionary(GDictionary source, string key)
    {
        if (!TryRead(source, key, out dynamic value))
            return new GDictionary();
        return value.AsGodotDictionary();
    }

    private static GArray ReadArray(GDictionary source, string key)
    {
        if (!TryRead(source, key, out dynamic value))
            return new GArray();
        return value.AsGodotArray();
    }

    private static BattleDamagePreviewSaveEstimate ReadSaveEstimate(
        GDictionary source,
        string key
    )
    {
        GDictionary payload = ReadDictionary(source, key);
        if (payload.Count == 0)
        {
            return BattleDamagePreviewSaveEstimate.None(0);
        }

        bool hasSave = ReadBool(payload, "has_save");
        int damageBeforeSave = ReadInt(payload, "damage_before_save");
        int damageAfterSave = ReadInt(
            payload,
            "damage_after_save",
            damageBeforeSave
        );
        int damageAfterSaveEstimate = ReadInt(
            payload,
            "damage_after_save_estimate",
            damageAfterSave
        );
        int damageAfterSaveWorst = ReadInt(
            payload,
            "damage_after_save_worst",
            damageAfterSaveEstimate
        );
        if (!hasSave)
        {
            return BattleDamagePreviewSaveEstimate.None(damageBeforeSave);
        }

        return BattleDamagePreviewSaveEstimate.Create(
            hasSave: true,
            damageBeforeSave: damageBeforeSave,
            damageAfterSave: damageAfterSave,
            damageAfterSaveEstimate: damageAfterSaveEstimate,
            damageAfterSaveWorst: damageAfterSaveWorst,
            damageOnSaveFailure: ReadInt(payload, "damage_on_save_failure", damageBeforeSave),
            damageOnSaveSuccess: ReadInt(payload, "damage_on_save_success", damageAfterSave),
            savePartialOnSuccess: ReadBool(payload, "save_partial_on_success"),
            saveSuccessProbabilityBasisPoints: ReadInt(
                payload,
                "save_success_probability_basis_points"
            ),
            saveSuccessRatePercent: ReadInt(payload, "save_success_rate_percent"),
            saveFailureProbabilityBasisPoints: ReadInt(
                payload,
                "save_failure_probability_basis_points"
            ),
            dc: ReadInt(payload, "dc"),
            ability: ReadString(payload, "ability"),
            saveTag: ReadString(payload, "save_tag"),
            advantageState: ReadString(payload, "advantage_state"),
            abilityValue: ReadInt(payload, "ability_value"),
            abilityModifier: ReadInt(payload, "ability_modifier"),
            bonus: ReadInt(payload, "bonus"),
            immune: ReadBool(payload, "immune"),
            sources: ReadSaveSources(payload)
        );
    }

    private static IReadOnlyList<BattleSaveSource> ReadSaveSources(GDictionary source)
    {
        var result = new List<BattleSaveSource>();
        foreach (Variant item in ReadArray(source, "sources"))
        {
            GDictionary payload = item.AsGodotDictionary();
            result.Add(
                new BattleSaveSource(
                    ReadStringName(payload, "source_id"),
                    ReadString(payload, "type"),
                    ReadStringName(payload, "tag"),
                    ReadStringName(payload, "mode")
                )
            );
        }
        return result;
    }

    private static GDictArray ReadDictArray(GDictionary source, string key)
    {
        var result = new GDictArray();
        if (!TryRead(source, key, out dynamic value))
            return result;
        foreach (var item in value.AsGodotArray())
        {
            result.Add(item.AsGodotDictionary());
        }
        return result;
    }

    private static GStringArray ReadStringArray(GDictionary source, string key)
    {
        var result = new GStringArray();
        if (!TryRead(source, key, out dynamic value))
            return result;
        foreach (var item in value.AsGodotArray())
        {
            string text = item.ToString();
            if (!string.IsNullOrEmpty(text))
                result.Add(text);
        }
        return result;
    }

    private static List<string> ReadStringList(GDictionary source, string key)
    {
        var result = new List<string>();
        foreach (string item in ReadStringArray(source, key))
        {
            if (!string.IsNullOrEmpty(item))
            {
                result.Add(item);
            }
        }
        return result;
    }

    private static GStringNameArray ReadStringNameArray(GDictionary source, string key)
    {
        var result = new GStringNameArray();
        if (!TryRead(source, key, out dynamic value))
            return result;
        foreach (var item in value.AsGodotArray())
        {
            StringName normalized = ProgressionDataUtils.to_string_name(item);
            if (normalized != "")
                result.Add(normalized);
        }
        return result;
    }

    private static List<StringName> ReadStringNameList(GDictionary source, string key)
    {
        var result = new List<StringName>();
        foreach (StringName item in ReadStringNameArray(source, key))
        {
            if (item != "")
            {
                result.Add(item);
            }
        }
        return result;
    }

    private static Dictionary<StringName, StringName> ReadResistanceTierDictionary(
        GDictionary source,
        string key
    )
    {
        var result = new Dictionary<StringName, StringName>();
        GDictionary payload = ReadDictionary(source, key);
        foreach (Variant payloadKey in payload.Keys)
        {
            StringName damageTag = ProgressionDataUtils.to_string_name(payloadKey);
            if (damageTag == "")
            {
                continue;
            }
            StringName tier = ProgressionDataUtils.to_string_name(payload[payloadKey]);
            if (tier != "")
            {
                result[damageTag] = tier;
            }
        }
        return result;
    }

    private static bool TryRead(GDictionary source, string key, out dynamic value)
    {
        value = default;
        if (source == null || key == null)
            return false;
        if (source.ContainsKey(key))
        {
            value = source[key];
            return true;
        }
        return false;
    }
}
