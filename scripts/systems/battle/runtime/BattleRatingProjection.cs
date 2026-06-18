using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal static class BattleRatingProjection
{
    internal static GDictionary ProjectStatsMap(
        IReadOnlyDictionary<StringName, BattleRatingMemberStats> statsByMemberId
    )
    {
        var snapshot = new GDictionary();
        if (statsByMemberId == null)
            return snapshot;
        foreach (KeyValuePair<StringName, BattleRatingMemberStats> entry in statsByMemberId)
        {
            if (entry.Value != null)
            {
                snapshot[entry.Key] = ProjectMemberStats(entry.Value);
            }
        }
        return snapshot;
    }

    internal static GDictionary ProjectMemberStats(BattleRatingMemberStats stats)
    {
        if (stats == null)
            return new GDictionary();

        var castCounts = new GDictionary();
        foreach (KeyValuePair<StringName, int> entry in stats.cast_counts)
        {
            castCounts[entry.Key] = entry.Value;
        }

        return new GDictionary
        {
            ["member_id"] = stats.member_id,
            ["member_name"] = stats.member_name,
            ["cast_counts"] = castCounts,
            ["successful_skill_count"] = stats.successful_skill_count,
            ["hostile_damage_done"] = stats.hostile_damage_done,
            ["ally_healing_done"] = stats.ally_healing_done,
            ["enemy_kill_count"] = stats.enemy_kill_count,
            ["friendly_fire_damage"] = stats.friendly_fire_damage,
            ["ally_defeat_count"] = stats.ally_defeat_count,
            ["enemy_healing_done"] = stats.enemy_healing_done,
            ["total_damage_done"] = stats.total_damage_done,
            ["total_healing_done"] = stats.total_healing_done,
            ["kill_count"] = stats.kill_count,
        };
    }
}
