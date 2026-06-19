using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public sealed class BattleSimUnitMetricsSnapshot
{
    internal BattleSimUnitMetricsSnapshot(
        string unitId,
        string displayName,
        string factionId,
        string controlMode,
        string sourceMemberId,
        Dictionary<string, int> actionCounts,
        Dictionary<string, int> skillAttemptCounts,
        Dictionary<string, int> skillSuccessCounts,
        int turnCount,
        int successfulSkillCount,
        int totalDamageDone,
        int totalHealingDone,
        int totalDamageTaken,
        int totalHealingReceived,
        int killCount,
        int deathCount
    )
    {
        UnitId = unitId ?? "";
        DisplayName = displayName ?? "";
        FactionId = factionId ?? "";
        ControlMode = controlMode ?? "";
        SourceMemberId = sourceMemberId ?? "";
        ActionCounts = actionCounts ?? NewIntMap();
        SkillAttemptCounts = skillAttemptCounts ?? NewIntMap();
        SkillSuccessCounts = skillSuccessCounts ?? NewIntMap();
        TurnCount = turnCount;
        SuccessfulSkillCount = successfulSkillCount;
        TotalDamageDone = totalDamageDone;
        TotalHealingDone = totalHealingDone;
        TotalDamageTaken = totalDamageTaken;
        TotalHealingReceived = totalHealingReceived;
        KillCount = killCount;
        DeathCount = deathCount;
    }

    public string UnitId { get; }
    public string DisplayName { get; }
    public string FactionId { get; }
    public string ControlMode { get; }
    public string SourceMemberId { get; }
    public IReadOnlyDictionary<string, int> ActionCounts { get; }
    public IReadOnlyDictionary<string, int> SkillAttemptCounts { get; }
    public IReadOnlyDictionary<string, int> SkillSuccessCounts { get; }
    public int TurnCount { get; }
    public int SuccessfulSkillCount { get; }
    public int TotalDamageDone { get; }
    public int TotalHealingDone { get; }
    public int TotalDamageTaken { get; }
    public int TotalHealingReceived { get; }
    public int KillCount { get; }
    public int DeathCount { get; }

    internal static BattleSimUnitMetricsSnapshot FromDictionary(string unitId, GDictionary data)
    {
        data ??= new GDictionary();
        return new BattleSimUnitMetricsSnapshot(
            ReadString(data, "unit_id", unitId),
            ReadString(data, "display_name"),
            ReadString(data, "faction_id"),
            ReadString(data, "control_mode"),
            ReadString(data, "source_member_id"),
            ReadIntMap(ReadDictionary(data, "action_counts")),
            ReadIntMap(ReadDictionary(data, "skill_attempt_counts")),
            ReadIntMap(ReadDictionary(data, "skill_success_counts")),
            ReadInt(data, "turn_count"),
            ReadInt(data, "successful_skill_count"),
            ReadInt(data, "total_damage_done"),
            ReadInt(data, "total_healing_done"),
            ReadInt(data, "total_damage_taken"),
            ReadInt(data, "total_healing_received"),
            ReadInt(data, "kill_count"),
            ReadInt(data, "death_count")
        );
    }

    private static Dictionary<string, int> ReadIntMap(GDictionary data)
    {
        var result = NewIntMap();
        if (data == null)
        {
            return result;
        }
        foreach (Variant key in data.Keys)
        {
            Variant value = data[key];
            if (value.VariantType == Variant.Type.Int)
            {
                result[VariantText(key)] = value.AsInt32();
            }
        }
        return result;
    }

    private static Dictionary<string, int> NewIntMap() =>
        new(System.StringComparer.Ordinal);

    private static GDictionary ReadDictionary(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
            return new GDictionary();
        Variant value = data[key];
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new GDictionary();
    }

    private static int ReadInt(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
            return 0;
        Variant value = data[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : 0;
    }

    private static string ReadString(GDictionary data, string key, string fallback = "")
    {
        if (data == null || !data.ContainsKey(key))
            return fallback ?? "";
        return VariantText(data[key], fallback);
    }

    private static string VariantText(Variant value, string fallback = "")
    {
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => fallback ?? "",
        };
    }
}

public sealed class BattleSimMetricsSnapshot
{
    private readonly Dictionary<string, BattleSimUnitMetricsSnapshot> _units =
        new(System.StringComparer.Ordinal);
    private readonly Dictionary<string, BattleSimFactionMetricSummary> _factions =
        new(System.StringComparer.Ordinal);

    private BattleSimMetricsSnapshot() { }

    public string BattleId { get; private set; } = "";

    public long Seed { get; private set; }

    public IReadOnlyDictionary<string, BattleSimUnitMetricsSnapshot> Units => _units;

    public IReadOnlyDictionary<string, BattleSimFactionMetricSummary> Factions => _factions;

    internal static BattleSimMetricsSnapshot Empty() => new();

    internal static BattleSimMetricsSnapshot FromDictionary(GDictionary data)
    {
        var result = new BattleSimMetricsSnapshot();
        if (data == null)
        {
            return result;
        }
        result.BattleId = ReadString(data, "battle_id");
        result.Seed = ReadLong(data, "seed");
        foreach (KeyValuePair<string, GDictionary> entry in ReadDictionaryEntries(data, "units"))
        {
            result._units[entry.Key] = BattleSimUnitMetricsSnapshot.FromDictionary(
                entry.Key,
                entry.Value
            );
        }
        foreach (KeyValuePair<string, GDictionary> entry in ReadDictionaryEntries(data, "factions"))
        {
            BattleSimFactionMetricSummary summary = new();
            summary.AccumulateFrom(entry.Value);
            result._factions[entry.Key] = summary;
        }
        return result;
    }

    internal GDictionary ToDictionary() => BattleSimReportProjection.Project(this);

    private static IEnumerable<KeyValuePair<string, GDictionary>> ReadDictionaryEntries(
        GDictionary data,
        string key
    )
    {
        if (data == null || !data.ContainsKey(key))
        {
            yield break;
        }
        Variant value = data[key];
        if (value.VariantType != Variant.Type.Dictionary)
        {
            yield break;
        }
        GDictionary entries = value.AsGodotDictionary();
        foreach (Variant entryKey in entries.Keys)
        {
            Variant entryValue = entries[entryKey];
            if (entryValue.VariantType == Variant.Type.Dictionary)
            {
                yield return new KeyValuePair<string, GDictionary>(
                    VariantText(entryKey),
                    entryValue.AsGodotDictionary()
                );
            }
        }
    }

    private static long ReadLong(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
            return 0L;
        Variant value = data[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt64() : 0L;
    }

    private static string ReadString(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
            return "";
        return VariantText(data[key]);
    }

    private static string VariantText(Variant value)
    {
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => "",
        };
    }
}
