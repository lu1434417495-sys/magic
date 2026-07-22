using System.Collections.Generic;
using GDictionary = Godot.Collections.Dictionary;

public sealed class BattleSimUnitMetricsSnapshot
{
    internal BattleSimUnitMetricsSnapshot(
        string unitId,
        string displayName,
        string factionId,
        string controlMode,
        string sourceMemberId,
        int unitCount,
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
        UnitCount = unitCount;
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
    public int UnitCount { get; }
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

    internal static BattleSimUnitMetricsSnapshot Capture(
        string unitId,
        BattleMetricEntry data,
        string forcedFactionId = ""
    )
    {
        data ??= new BattleMetricEntry();
        return new BattleSimUnitMetricsSnapshot(
            string.IsNullOrEmpty(data.UnitId) ? unitId : data.UnitId,
            data.DisplayName,
            string.IsNullOrEmpty(forcedFactionId) ? data.FactionId : forcedFactionId,
            data.ControlMode,
            data.SourceMemberId,
            data.UnitCount,
            CloneIntMap(data.ActionCounts),
            CloneIntMap(data.SkillAttemptCounts),
            CloneIntMap(data.SkillSuccessCounts),
            data.TurnCount,
            data.SuccessfulSkillCount,
            data.TotalDamageDone,
            data.TotalHealingDone,
            data.TotalDamageTaken,
            data.TotalHealingReceived,
            data.KillCount,
            data.DeathCount
        );
    }

    internal Dictionary<string, object> BuildPlain()
    {
        var result = new Dictionary<string, object>(System.StringComparer.Ordinal)
        {
            ["faction_id"] = FactionId,
            ["turn_count"] = TurnCount,
            ["action_counts"] = ActionCounts,
            ["skill_attempt_counts"] = SkillAttemptCounts,
            ["skill_success_counts"] = SkillSuccessCounts,
            ["successful_skill_count"] = SuccessfulSkillCount,
            ["total_damage_done"] = TotalDamageDone,
            ["total_healing_done"] = TotalHealingDone,
            ["total_damage_taken"] = TotalDamageTaken,
            ["total_healing_received"] = TotalHealingReceived,
            ["kill_count"] = KillCount,
            ["death_count"] = DeathCount,
        };
        if (!string.IsNullOrEmpty(UnitId))
        {
            result["unit_id"] = UnitId;
            result["display_name"] = DisplayName;
            result["control_mode"] = ControlMode;
            result["source_member_id"] = SourceMemberId;
        }
        else
        {
            result["unit_count"] = UnitCount;
        }
        return result;
    }

    internal Dictionary<string, object> BuildFactionPlain() =>
        new(System.StringComparer.Ordinal)
        {
            ["faction_id"] = FactionId,
            ["unit_count"] = UnitCount,
            ["turn_count"] = TurnCount,
            ["action_counts"] = ActionCounts,
            ["skill_attempt_counts"] = SkillAttemptCounts,
            ["skill_success_counts"] = SkillSuccessCounts,
            ["successful_skill_count"] = SuccessfulSkillCount,
            ["total_damage_done"] = TotalDamageDone,
            ["total_healing_done"] = TotalHealingDone,
            ["total_damage_taken"] = TotalDamageTaken,
            ["total_healing_received"] = TotalHealingReceived,
            ["kill_count"] = KillCount,
            ["death_count"] = DeathCount,
        };

    private static Dictionary<string, int> CloneIntMap(
        IReadOnlyDictionary<string, int> data
    )
    {
        var result = NewIntMap();
        if (data == null)
            return result;
        foreach (KeyValuePair<string, int> entry in data)
            if (!string.IsNullOrEmpty(entry.Key))
                result[entry.Key] = entry.Value;
        return result;
    }

    private static Dictionary<string, int> NewIntMap() =>
        new(System.StringComparer.Ordinal);

}

public sealed class BattleSimMetricsSnapshot
{
    private readonly Dictionary<string, BattleSimUnitMetricsSnapshot> _units =
        new(System.StringComparer.Ordinal);
    private readonly Dictionary<string, BattleSimUnitMetricsSnapshot> _factions =
        new(System.StringComparer.Ordinal);

    private BattleSimMetricsSnapshot() { }

    public string BattleId { get; private set; } = "";

    public long Seed { get; private set; }

    public IReadOnlyDictionary<string, BattleSimUnitMetricsSnapshot> Units => _units;

    public IReadOnlyDictionary<string, BattleSimUnitMetricsSnapshot> Factions => _factions;

    internal static BattleSimMetricsSnapshot Empty() => new();

    internal static BattleSimMetricsSnapshot Capture(BattleMetricsState data)
    {
        var result = new BattleSimMetricsSnapshot();
        if (data == null)
            return result;
        result.BattleId = data.BattleId ?? "";
        result.Seed = data.Seed;
        foreach (KeyValuePair<string, BattleMetricEntry> entry in data.Units)
            if (!string.IsNullOrEmpty(entry.Key))
                result._units[entry.Key] = BattleSimUnitMetricsSnapshot.Capture(
                    entry.Key,
                    entry.Value
                );
        foreach (KeyValuePair<string, BattleMetricEntry> entry in data.Factions)
        {
            if (string.IsNullOrEmpty(entry.Key) || entry.Value == null)
                continue;
            result._factions[entry.Key] = BattleSimUnitMetricsSnapshot.Capture(
                "",
                entry.Value,
                entry.Key
            );
        }
        return result;
    }

    internal GodotProjectionLease<GDictionary> BuildLease() =>
        BattleSimReportProjection.BuildMetricsLease(this);

    internal Dictionary<string, object> BuildPlain()
    {
        var units = new Dictionary<string, object>(System.StringComparer.Ordinal);
        foreach (KeyValuePair<string, BattleSimUnitMetricsSnapshot> entry in _units)
            units[entry.Key] = entry.Value?.BuildPlain() ?? NewPlainMap();

        var factions = new Dictionary<string, object>(System.StringComparer.Ordinal);
        foreach (KeyValuePair<string, BattleSimUnitMetricsSnapshot> entry in _factions)
            factions[entry.Key] = entry.Value?.BuildFactionPlain() ?? NewPlainMap();

        return new Dictionary<string, object>(System.StringComparer.Ordinal)
        {
            ["battle_id"] = BattleId,
            ["seed"] = Seed,
            ["units"] = units,
            ["factions"] = factions,
        };
    }

    private static Dictionary<string, object> NewPlainMap() =>
        new(System.StringComparer.Ordinal);

}
