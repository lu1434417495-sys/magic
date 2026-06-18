using System.Collections.Generic;
using Godot;

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

    private static bool IsEmpty(StringName value) =>
        value == null || string.IsNullOrEmpty(value.ToString());
}
