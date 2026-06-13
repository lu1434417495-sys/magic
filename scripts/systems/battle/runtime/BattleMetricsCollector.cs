using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleMetricEntry
{
    public string UnitId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string FactionId { get; set; } = "";
    public string ControlMode { get; set; } = "";
    public string SourceMemberId { get; set; } = "";
    public int UnitCount { get; set; }
    public int TurnCount { get; set; }
    public Dictionary<string, int> ActionCounts { get; } = new(StringComparer.Ordinal)
    {
        ["move"] = 0,
        ["skill"] = 0,
        ["wait"] = 0,
    };
    public Dictionary<string, int> SkillAttemptCounts { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, int> SkillSuccessCounts { get; } = new(StringComparer.Ordinal);
    public int SuccessfulSkillCount { get; set; }
    public int TotalDamageDone { get; set; }
    public int TotalHealingDone { get; set; }
    public int TotalDamageTaken { get; set; }
    public int TotalHealingReceived { get; set; }
    public int KillCount { get; set; }
    public int DeathCount { get; set; }

    internal Godot.Collections.Dictionary ToDictionary()
    {
        var payload = new Godot.Collections.Dictionary
        {
            ["faction_id"] = FactionId,
            ["turn_count"] = TurnCount,
            ["action_counts"] = IntMapToDictionary(ActionCounts),
            ["skill_attempt_counts"] = IntMapToDictionary(SkillAttemptCounts),
            ["skill_success_counts"] = IntMapToDictionary(SkillSuccessCounts),
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
            payload["unit_id"] = UnitId;
            payload["display_name"] = DisplayName;
            payload["control_mode"] = ControlMode;
            payload["source_member_id"] = SourceMemberId;
        }
        else
        {
            payload["unit_count"] = UnitCount;
        }
        return payload;
    }

    private static Godot.Collections.Dictionary IntMapToDictionary(Dictionary<string, int> source)
    {
        var payload = new Godot.Collections.Dictionary();
        foreach (KeyValuePair<string, int> entry in source)
        {
            payload[entry.Key] = entry.Value;
        }
        return payload;
    }
}

internal sealed class BattleMetricsState
{
    public string BattleId { get; set; } = "";
    public long Seed { get; set; }
    public Dictionary<string, BattleMetricEntry> Units { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, BattleMetricEntry> Factions { get; } = new(StringComparer.Ordinal);

    internal void Clear()
    {
        BattleId = "";
        Seed = 0;
        Units.Clear();
        Factions.Clear();
    }

    internal Godot.Collections.Dictionary ToDictionary()
    {
        var units = new Godot.Collections.Dictionary();
        foreach (KeyValuePair<string, BattleMetricEntry> entry in Units)
        {
            units[entry.Key] = entry.Value.ToDictionary();
        }

        var factions = new Godot.Collections.Dictionary();
        foreach (KeyValuePair<string, BattleMetricEntry> entry in Factions)
        {
            factions[entry.Key] = entry.Value.ToDictionary();
        }

        return new Godot.Collections.Dictionary
        {
            ["battle_id"] = BattleId,
            ["seed"] = Seed,
            ["units"] = units,
            ["factions"] = factions,
        };
    }
}

internal sealed class BattleMetricsCollector
{
    private static readonly StringName Empty = "";
    private static readonly StringName TypeMove = "move";
    private static readonly StringName TypeWait = "wait";

    private WeakReference<BattleRuntimeModule> _runtimeRef;

    private BattleRuntimeModule Runtime => ResolveWeakRef(_runtimeRef);

    internal void Setup(BattleRuntimeModule runtime)
    {
        _runtimeRef = runtime != null ? new WeakReference<BattleRuntimeModule>(runtime) : null;
    }

    internal void Dispose()
    {
        _runtimeRef = null;
    }

    public void InitializeBattleMetrics()
    {
        BattleRuntimeModule runtime = Runtime;
        BattleState state = runtime?._state;
        var metrics = new BattleMetricsState
        {
            BattleId = state != null ? (state.battle_id ?? Empty).ToString() : "",
            Seed = state?.seed ?? 0,
        };
        if (runtime != null)
        {
            runtime._battle_metrics = metrics;
        }
        if (state == null)
        {
            return;
        }

        foreach (BattleUnitState unitState in state.GetUnitsTyped())
        {
            if (unitState == null)
            {
                continue;
            }
            metrics.Units[unitState.unit_id.ToString()] = BuildUnitMetricEntry(unitState);
            BattleMetricEntry factionEntry = EnsureFactionMetricEntry(unitState.faction_id);
            factionEntry.UnitCount += 1;
        }
    }

    internal BattleMetricEntry BuildUnitMetricEntry(BattleUnitState unitState)
    {
        if (unitState == null)
        {
            return new BattleMetricEntry();
        }
        return new BattleMetricEntry
        {
            UnitId = (unitState.unit_id ?? Empty).ToString(),
            DisplayName = unitState.display_name,
            FactionId = (unitState.faction_id ?? Empty).ToString(),
            ControlMode = (unitState.control_mode ?? Empty).ToString(),
            SourceMemberId = (unitState.source_member_id ?? Empty).ToString(),
        };
    }

    internal BattleMetricEntry EnsureUnitMetricEntry(BattleUnitState unitState)
    {
        BattleMetricsState battleMetrics = Metrics();
        if (battleMetrics == null || unitState == null)
        {
            return new BattleMetricEntry();
        }

        string unitKey = unitState.unit_id.ToString();
        if (!battleMetrics.Units.TryGetValue(unitKey, out BattleMetricEntry entry))
        {
            entry = BuildUnitMetricEntry(unitState);
            battleMetrics.Units[unitKey] = entry;
        }
        return entry;
    }

    internal BattleMetricEntry EnsureFactionMetricEntry(StringName factionId)
    {
        BattleMetricsState battleMetrics = Metrics();
        if (battleMetrics == null)
        {
            return new BattleMetricEntry();
        }

        string factionKey = (factionId ?? Empty).ToString();
        if (!battleMetrics.Factions.TryGetValue(factionKey, out BattleMetricEntry entry))
        {
            entry = new BattleMetricEntry { FactionId = factionKey };
            battleMetrics.Factions[factionKey] = entry;
        }
        return entry;
    }

    public void RecordTurnStarted(BattleUnitState unitState)
    {
        BattleMetricEntry unitEntry = EnsureUnitMetricEntry(unitState);
        if (unitState == null || string.IsNullOrEmpty(unitEntry.UnitId))
        {
            return;
        }
        unitEntry.TurnCount += 1;
        BattleMetricEntry factionEntry = EnsureFactionMetricEntry(unitState.faction_id);
        factionEntry.TurnCount += 1;
    }

    public void RecordActionIssued(BattleUnitState unitState, StringName commandType, int apCost = 0)
    {
        if (unitState != null)
        {
            if (commandType == TypeMove)
            {
                unitState.has_moved_this_turn = true;
            }
            else if (commandType != TypeWait && apCost > 0)
            {
                unitState.has_taken_action_this_turn = true;
                unitState.is_resting = false;
            }
        }

        string commandKey = (commandType ?? Empty).ToString();
        if (string.IsNullOrEmpty(commandKey) || unitState == null)
        {
            return;
        }
        BattleMetricEntry unitEntry = EnsureUnitMetricEntry(unitState);
        if (string.IsNullOrEmpty(unitEntry.UnitId))
        {
            return;
        }
        IncrementMetricCount(unitEntry.ActionCounts, commandKey, 1);
        BattleMetricEntry factionEntry = EnsureFactionMetricEntry(unitState.faction_id);
        IncrementMetricCount(factionEntry.ActionCounts, commandKey, 1);
    }

    public void RecordSkillAttempt(BattleUnitState unitState, StringName skillId)
    {
        string skillKey = (skillId ?? Empty).ToString();
        if (string.IsNullOrEmpty(skillKey) || unitState == null)
        {
            return;
        }
        BattleMetricEntry unitEntry = EnsureUnitMetricEntry(unitState);
        if (string.IsNullOrEmpty(unitEntry.UnitId))
        {
            return;
        }
        IncrementMetricCount(unitEntry.SkillAttemptCounts, skillKey, 1);
        BattleMetricEntry factionEntry = EnsureFactionMetricEntry(unitState.faction_id);
        IncrementMetricCount(factionEntry.SkillAttemptCounts, skillKey, 1);
    }

    public void RecordSkillSuccess(BattleUnitState unitState, StringName skillId)
    {
        string skillKey = (skillId ?? Empty).ToString();
        if (string.IsNullOrEmpty(skillKey) || unitState == null)
        {
            return;
        }
        BattleMetricEntry unitEntry = EnsureUnitMetricEntry(unitState);
        if (string.IsNullOrEmpty(unitEntry.UnitId))
        {
            return;
        }
        IncrementMetricCount(unitEntry.SkillSuccessCounts, skillKey, 1);
        unitEntry.SuccessfulSkillCount += 1;
        BattleMetricEntry factionEntry = EnsureFactionMetricEntry(unitState.faction_id);
        IncrementMetricCount(factionEntry.SkillSuccessCounts, skillKey, 1);
        factionEntry.SuccessfulSkillCount += 1;
    }

    public void RecordEffectMetrics(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        int damage,
        int healing,
        int killCount
    )
    {
        if (sourceUnit == null || targetUnit == null)
        {
            return;
        }
        BattleMetricEntry sourceEntry = EnsureUnitMetricEntry(sourceUnit);
        BattleMetricEntry targetEntry = EnsureUnitMetricEntry(targetUnit);
        BattleMetricEntry sourceFactionEntry = EnsureFactionMetricEntry(sourceUnit.faction_id);
        BattleMetricEntry targetFactionEntry = EnsureFactionMetricEntry(targetUnit.faction_id);
        int positiveDamage = Math.Max(damage, 0);
        int positiveHealing = Math.Max(healing, 0);
        if (positiveDamage > 0)
        {
            sourceEntry.TotalDamageDone += positiveDamage;
            targetEntry.TotalDamageTaken += positiveDamage;
            sourceFactionEntry.TotalDamageDone += positiveDamage;
            targetFactionEntry.TotalDamageTaken += positiveDamage;
        }
        if (positiveHealing > 0)
        {
            sourceEntry.TotalHealingDone += positiveHealing;
            targetEntry.TotalHealingReceived += positiveHealing;
            sourceFactionEntry.TotalHealingDone += positiveHealing;
            targetFactionEntry.TotalHealingReceived += positiveHealing;
        }
        if (killCount > 0)
        {
            sourceEntry.KillCount += killCount;
            sourceFactionEntry.KillCount += killCount;
        }
    }

    public void RecordSkillEffectResult(
        BattleUnitState sourceUnit,
        int damage,
        int healing,
        int killCount
    )
    {
        if (sourceUnit == null)
        {
            return;
        }
        BattleMetricEntry sourceEntry = EnsureUnitMetricEntry(sourceUnit);
        BattleMetricEntry factionEntry = EnsureFactionMetricEntry(sourceUnit.faction_id);
        int positiveDamage = Math.Max(damage, 0);
        int positiveHealing = Math.Max(healing, 0);
        int positiveKills = Math.Max(killCount, 0);
        sourceEntry.TotalDamageDone += positiveDamage;
        sourceEntry.TotalHealingDone += positiveHealing;
        sourceEntry.KillCount += positiveKills;
        factionEntry.TotalDamageDone += positiveDamage;
        factionEntry.TotalHealingDone += positiveHealing;
        factionEntry.KillCount += positiveKills;
    }

    public void RecordUnitDefeated(BattleUnitState unitState)
    {
        BattleMetricEntry unitEntry = EnsureUnitMetricEntry(unitState);
        if (unitState == null || string.IsNullOrEmpty(unitEntry.UnitId))
        {
            return;
        }
        unitEntry.DeathCount += 1;
        BattleMetricEntry factionEntry = EnsureFactionMetricEntry(unitState.faction_id);
        factionEntry.DeathCount += 1;
    }

    public static void IncrementMetricCount(Dictionary<string, int> metricMap, string key, int delta)
    {
        if (metricMap == null || string.IsNullOrEmpty(key))
        {
            return;
        }
        metricMap[key] = metricMap.GetValueOrDefault(key) + delta;
    }

    private BattleMetricsState Metrics() => Runtime?._battle_metrics;

    private static BattleRuntimeModule ResolveWeakRef(WeakReference<BattleRuntimeModule> weakRef)
    {
        if (weakRef == null || !weakRef.TryGetTarget(out BattleRuntimeModule target))
        {
            return null;
        }
        return target;
    }
}
