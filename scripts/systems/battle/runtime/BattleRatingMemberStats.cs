using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public sealed class BattleRatingMemberStats
{
    public StringName member_id = "";
    public string member_name = "";
    public readonly Dictionary<StringName, int> cast_counts = new();
    public int successful_skill_count;
    public int hostile_damage_done;
    public int ally_healing_done;
    public int enemy_kill_count;
    public int friendly_fire_damage;
    public int ally_defeat_count;
    public int enemy_healing_done;
    public int total_damage_done;
    public int total_healing_done;
    public int kill_count;

    public static BattleRatingMemberStats FromUnit(BattleUnitState unitState)
    {
        if (unitState == null || IsEmpty(unitState.source_member_id))
        {
            return null;
        }
        string memberName = unitState.display_name;
        return new BattleRatingMemberStats
        {
            member_id = unitState.source_member_id,
            member_name = string.IsNullOrEmpty(memberName)
                ? unitState.source_member_id.ToString()
                : memberName,
        };
    }

    public static BattleRatingMemberStats FromDictionary(GDictionary source)
    {
        if (source == null || source.Count == 0)
        {
            return null;
        }
        var stats = new BattleRatingMemberStats
        {
            member_id = ProgressionDataUtils.to_string_name(
                source.GetValueOrDefault("member_id", "")
            ),
            member_name = source.GetValueOrDefault("member_name", "").AsString(),
            successful_skill_count = ReadInt(source, "successful_skill_count"),
            hostile_damage_done = ReadInt(source, "hostile_damage_done"),
            ally_healing_done = ReadInt(source, "ally_healing_done"),
            enemy_kill_count = ReadInt(source, "enemy_kill_count"),
            friendly_fire_damage = ReadInt(source, "friendly_fire_damage"),
            ally_defeat_count = ReadInt(source, "ally_defeat_count"),
            enemy_healing_done = ReadInt(source, "enemy_healing_done"),
            total_damage_done = ReadInt(source, "total_damage_done"),
            total_healing_done = ReadInt(source, "total_healing_done"),
            kill_count = ReadInt(source, "kill_count"),
        };

        GDictionary castCounts = source
            .GetValueOrDefault("cast_counts", new GDictionary())
            .AsGodotDictionary();
        foreach (var skillKey in castCounts.Keys)
        {
            StringName skillId = ProgressionDataUtils.to_string_name(skillKey);
            int count = castCounts[skillKey].AsInt32();
            if (!IsEmpty(skillId) && count > 0)
            {
                stats.cast_counts[skillId] = count;
            }
        }
        return stats;
    }

    public GDictionary ToDictionary()
    {
        var castCounts = new GDictionary();
        foreach (KeyValuePair<StringName, int> entry in cast_counts)
        {
            castCounts[entry.Key] = entry.Value;
        }

        return new GDictionary
        {
            ["member_id"] = member_id,
            ["member_name"] = member_name,
            ["cast_counts"] = castCounts,
            ["successful_skill_count"] = successful_skill_count,
            ["hostile_damage_done"] = hostile_damage_done,
            ["ally_healing_done"] = ally_healing_done,
            ["enemy_kill_count"] = enemy_kill_count,
            ["friendly_fire_damage"] = friendly_fire_damage,
            ["ally_defeat_count"] = ally_defeat_count,
            ["enemy_healing_done"] = enemy_healing_done,
            ["total_damage_done"] = total_damage_done,
            ["total_healing_done"] = total_healing_done,
            ["kill_count"] = kill_count,
        };
    }

    private static bool IsEmpty(StringName value) =>
        value == null || string.IsNullOrEmpty(value.ToString());

    private static int ReadInt(GDictionary source, string key) =>
        source.GetValueOrDefault(key, 0).AsInt32();
}
