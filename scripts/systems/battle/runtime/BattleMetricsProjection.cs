using System.Collections.Generic;
using Godot;

internal static class BattleMetricsProjection
{
    internal static Godot.Collections.Dictionary Project(BattleMetricsState metrics)
    {
        if (metrics == null)
            return new Godot.Collections.Dictionary();

        var units = new Godot.Collections.Dictionary();
        foreach (KeyValuePair<string, BattleMetricEntry> entry in metrics.Units)
        {
            units[entry.Key] = ProjectEntry(entry.Value);
        }

        var factions = new Godot.Collections.Dictionary();
        foreach (KeyValuePair<string, BattleMetricEntry> entry in metrics.Factions)
        {
            factions[entry.Key] = ProjectEntry(entry.Value);
        }

        return new Godot.Collections.Dictionary
        {
            ["battle_id"] = metrics.BattleId,
            ["seed"] = metrics.Seed,
            ["units"] = units,
            ["factions"] = factions,
        };
    }

    private static Godot.Collections.Dictionary ProjectEntry(BattleMetricEntry entry)
    {
        if (entry == null)
            return new Godot.Collections.Dictionary();
        var payload = new Godot.Collections.Dictionary
        {
            ["faction_id"] = entry.FactionId,
            ["turn_count"] = entry.TurnCount,
            ["action_counts"] = IntMapToDictionary(entry.ActionCounts),
            ["skill_attempt_counts"] = IntMapToDictionary(entry.SkillAttemptCounts),
            ["skill_success_counts"] = IntMapToDictionary(entry.SkillSuccessCounts),
            ["successful_skill_count"] = entry.SuccessfulSkillCount,
            ["total_damage_done"] = entry.TotalDamageDone,
            ["total_healing_done"] = entry.TotalHealingDone,
            ["total_damage_taken"] = entry.TotalDamageTaken,
            ["total_healing_received"] = entry.TotalHealingReceived,
            ["kill_count"] = entry.KillCount,
            ["death_count"] = entry.DeathCount,
        };
        if (!string.IsNullOrEmpty(entry.UnitId))
        {
            payload["unit_id"] = entry.UnitId;
            payload["display_name"] = entry.DisplayName;
            payload["control_mode"] = entry.ControlMode;
            payload["source_member_id"] = entry.SourceMemberId;
        }
        else
        {
            payload["unit_count"] = entry.UnitCount;
        }
        return payload;
    }

    private static Godot.Collections.Dictionary IntMapToDictionary(Dictionary<string, int> source)
    {
        var payload = new Godot.Collections.Dictionary();
        if (source == null)
            return payload;
        foreach (KeyValuePair<string, int> entry in source)
        {
            payload[entry.Key] = entry.Value;
        }
        return payload;
    }
}
