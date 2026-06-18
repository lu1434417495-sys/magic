using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_rating_projection_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestMemberStatsProjectionCopiesTypedState();
        TestStatsMapProjectionUsesMemberKeys();
        Quit(_test.Finish("Battle rating projection regression"));
    }

    private void TestMemberStatsProjectionCopiesTypedState()
    {
        BattleRatingMemberStats stats = BuildStats();

        GDictionary payload = BattleRatingProjection.ProjectMemberStats(stats);
        GDictionary castCounts = payload["cast_counts"].AsGodotDictionary();

        _test.Eq(payload["member_id"].AsStringName(), new StringName("hero_member"), "rating stats 投影应保留 member id。");
        _test.Eq(castCounts[new StringName("fireball")].AsInt32(), 2, "rating stats 投影应保留 cast count。");
        _test.Eq(payload["hostile_damage_done"].AsInt32(), 17, "rating stats 投影应保留伤害。");

        castCounts[new StringName("fireball")] = 99;
        payload["hostile_damage_done"] = 99;

        _test.Eq(stats.cast_counts[new StringName("fireball")], 2, "修改 rating 投影不应污染 typed cast counts。");
        _test.Eq(stats.hostile_damage_done, 17, "修改 rating 投影不应污染 typed stats。");
    }

    private void TestStatsMapProjectionUsesMemberKeys()
    {
        BattleRatingMemberStats stats = BuildStats();
        var map = new Dictionary<StringName, BattleRatingMemberStats>
        {
            [new StringName("hero_member")] = stats,
        };

        GDictionary payload = BattleRatingProjection.ProjectStatsMap(map);
        GDictionary memberPayload = payload[new StringName("hero_member")].AsGodotDictionary();

        _test.Eq(memberPayload["member_name"].AsString(), "Hero", "rating stats map 应按 member id 投影成员。");
    }

    private static BattleRatingMemberStats BuildStats()
    {
        var stats = new BattleRatingMemberStats
        {
            member_id = "hero_member",
            member_name = "Hero",
            successful_skill_count = 3,
            hostile_damage_done = 17,
            ally_healing_done = 5,
            enemy_kill_count = 1,
            total_damage_done = 19,
            total_healing_done = 5,
            kill_count = 1,
        };
        stats.cast_counts[new StringName("fireball")] = 2;
        return stats;
    }
}
