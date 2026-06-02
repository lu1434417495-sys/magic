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

    public GDictionary ToDictionary()
    {
        return new GDictionary
        {
            ["move_cost_delta"] = MoveCostDelta,
            ["creates_dust"] = CreatesDust,
            ["creates_crater"] = CreatesCrater,
            ["creates_rubble"] = CreatesRubble,
        };
    }

    public static MeteorSwarmHostileTerrainConsequence FromDictionary(GDictionary source)
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
        StringName stringNameKey = new(key);
        if (source.ContainsKey(stringNameKey))
        {
            value = source[stringNameKey];
            return true;
        }
        return false;
    }
}

public sealed class MeteorSwarmComponentBreakdownEntry
{
    public StringName ComponentId = "";
    public int ExpectedDamage;
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
    public GDictArray ComponentBreakdown = new();
    public int LethalProbabilityPercent;
    public GStringArray SaveProfileIds = new();
    public GDictionary ResistanceTiersByDamageTag = new();
    public int ShieldHp;
    public int GuardBlockEstimate;
    public GStringNameArray StatusEffectIds = new();
    public int ApPenalty;
    public MeteorSwarmHostileTerrainConsequence HostileTerrain = new();
    public int ExpectedDamageHpPercent;
    public int WorstCaseDamageHpPercent;
    public bool HardReject;
    public bool SoftPenalty;
    public readonly List<MeteorSwarmComponentBreakdownEntry> Components = new();

    public int StatusEffectCount => StatusEffectIds?.Count ?? 0;

    public bool HasCenterDirect
    {
        get
        {
            foreach (MeteorSwarmComponentBreakdownEntry component in Components)
            {
                if (component?.ComponentId == "center_direct")
                    return true;
            }
            return false;
        }
    }

    public GDictionary ToDictionary()
    {
        return new GDictionary
        {
            ["candidate_anchor_coord"] = CandidateAnchorCoord,
            ["target_unit_id"] = TargetUnitId.ToString(),
            ["ally_unit_id"] = AllyUnitId.ToString(),
            ["target_faction_id"] = TargetFactionId.ToString(),
            ["is_ally"] = IsAlly,
            ["distance_from_anchor"] = DistanceFromAnchor,
            ["component_expected_damage"] = ComponentExpectedDamage,
            ["component_worst_case_damage"] = ComponentWorstCaseDamage,
            ["component_breakdown"] = ComponentBreakdown?.Duplicate(true) ?? new GDictArray(),
            ["lethal_probability_percent"] = LethalProbabilityPercent,
            ["save_profile_ids"] = SaveProfileIds?.Duplicate() ?? new GStringArray(),
            ["resistance_tiers_by_damage_tag"] =
                ResistanceTiersByDamageTag?.Duplicate(true) ?? new GDictionary(),
            ["shield_hp"] = ShieldHp,
            ["guard_block_estimate"] = GuardBlockEstimate,
            ["status_effect_ids"] = StatusEffectIds?.Duplicate() ?? new GStringNameArray(),
            ["ap_penalty"] = ApPenalty,
            ["hostile_terrain_consequence"] =
                HostileTerrain?.ToDictionary() ?? new GDictionary(),
            ["expected_damage_hp_percent"] = ExpectedDamageHpPercent,
            ["worst_case_damage_hp_percent"] = WorstCaseDamageHpPercent,
            ["hard_reject"] = HardReject,
            ["soft_penalty"] = SoftPenalty,
        };
    }

    public static MeteorSwarmNumericSummary FromDictionary(GDictionary source)
    {
        source ??= new GDictionary();
        GDictArray componentBreakdown = ReadDictArray(source, "component_breakdown");
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
            SaveProfileIds = ReadStringArray(source, "save_profile_ids"),
            ResistanceTiersByDamageTag = ReadDictionary(
                source,
                "resistance_tiers_by_damage_tag"
            ),
            ShieldHp = ReadInt(source, "shield_hp"),
            GuardBlockEstimate = ReadInt(source, "guard_block_estimate"),
            StatusEffectIds = ReadStringNameArray(source, "status_effect_ids"),
            ApPenalty = ReadInt(source, "ap_penalty"),
            HostileTerrain = MeteorSwarmHostileTerrainConsequence.FromDictionary(
                ReadDictionary(source, "hostile_terrain_consequence")
            ),
            ExpectedDamageHpPercent = ReadInt(source, "expected_damage_hp_percent"),
            WorstCaseDamageHpPercent = ReadInt(source, "worst_case_damage_hp_percent"),
            HardReject = ReadBool(source, "hard_reject"),
            SoftPenalty = ReadBool(source, "soft_penalty"),
        };
        summary.Components.AddRange(ReadComponents(componentBreakdown));
        return summary;
    }

    public static GDictArray ToDictionaryArray(IEnumerable<MeteorSwarmNumericSummary> summaries)
    {
        var result = new GDictArray();
        foreach (MeteorSwarmNumericSummary summary in summaries ?? Array.Empty<MeteorSwarmNumericSummary>())
        {
            if (summary != null)
                result.Add(summary.ToDictionary());
        }
        return result;
    }

    private static List<MeteorSwarmComponentBreakdownEntry> ReadComponents(
        GDictArray componentBreakdown
    )
    {
        var result = new List<MeteorSwarmComponentBreakdownEntry>();
        foreach (GDictionary component in componentBreakdown ?? new GDictArray())
        {
            result.Add(
                new MeteorSwarmComponentBreakdownEntry
                {
                    ComponentId = ReadStringName(component, "component_id"),
                    ExpectedDamage = ReadInt(component, "expected_damage"),
                }
            );
        }
        return result;
    }

    private static int ReadInt(GDictionary source, string key, int fallback = 0)
    {
        if (!TryRead(source, key, out dynamic value))
            return fallback;
        return value.AsInt32();
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
        StringName stringNameKey = new(key);
        if (source.ContainsKey(stringNameKey))
        {
            value = source[stringNameKey];
            return true;
        }
        return false;
    }
}
