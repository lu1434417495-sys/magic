using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal sealed class BattleRatingMemberStats
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

    internal static BattleRatingMemberStats FromUnit(BattleUnitState unitState)
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

    internal GDictionary ToDictionary()
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

    private static int ReadInt(GDictionary source, string key)
    {
        if (source == null || string.IsNullOrEmpty(key) || !source.ContainsKey(key))
        {
            return 0;
        }
        Variant value = source[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : 0;
    }

    private static string ReadString(GDictionary source, string key)
    {
        if (source == null || string.IsNullOrEmpty(key) || !source.ContainsKey(key))
        {
            return "";
        }
        return source[key].ToString();
    }

    private static StringName ReadStringName(GDictionary source, string key)
    {
        string text = ReadString(source, key);
        return string.IsNullOrEmpty(text) ? new StringName("") : new StringName(text);
    }

    private static GDictionary ReadDictionary(GDictionary source, string key)
    {
        if (source == null || string.IsNullOrEmpty(key) || !source.ContainsKey(key))
        {
            return new GDictionary();
        }
        Variant value = source[key];
        return value.VariantType == Variant.Type.Dictionary ? value.AsGodotDictionary() : new GDictionary();
    }
}
