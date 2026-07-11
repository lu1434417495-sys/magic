using System;
using System.Collections.Generic;
using Godot;

public partial class run_battle_ai_unit_snapshot_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestPayloadGuardIsPlainStaticBoundaryHelper();
        TestSnapshotCopiesUnitStateIntoTypedCollections();
        TestSnapshotPlainPayloadIsBoundaryOnly();
        RequestTestExit(_test.Finish("Battle AI unit snapshot regression"));
    }

    private void TestPayloadGuardIsPlainStaticBoundaryHelper()
    {
        Type guardType = typeof(BattleAiPayloadGuard);
        _test.True(
            guardType.IsAbstract && guardType.IsSealed,
            "BattleAiPayloadGuard 应是 plain static C# helper。"
        );
    }

    private void TestSnapshotCopiesUnitStateIntoTypedCollections()
    {
        BattleUnitState unit = BuildUnit();
        BattleAiUnitSnapshot snapshot = BattleAiUnitSnapshot.FromUnit(unit);

        _test.True(snapshot != null, "FromUnit 应创建 snapshot。");
        if (snapshot == null)
            return;

        _test.Eq(snapshot.unit_id, new StringName("snapshot_actor"), "unit_id 应复制。");
        _test.Eq(snapshot.occupied_coords.Count, 2, "occupied coords 应复制为 C# List。");
        _test.Eq(snapshot.known_active_skill_ids.Count, 1, "known skill ids 应复制为 C# List。");
        _test.Eq(snapshot.known_skill_level_map[new StringName("fireball")], 4, "skill level 应复制为 int map。");
        _test.Eq(snapshot.cooldowns[new StringName("fireball")], 2, "cooldown 应复制为 int map。");
        _test.True(snapshot.ai_blackboard.madness_target_any_team, "blackboard bool 应复制。");
        _test.True(snapshot.ai_blackboard.protected_ally, "protected ally bool 应复制。");
        _test.Eq(snapshot.ai_blackboard.summon_source_unit_id, new StringName("summoner"), "summon source 应复制。");
        _test.True(snapshot.status_ids.Contains(new StringName("burning")), "status id 应复制到 typed list。");

        unit.occupied_coords.Add(new Vector2I(9, 9));
        unit.known_active_skill_ids.Add("icebolt");
        unit.known_skill_level_map["fireball"] = 1;
        unit.cooldowns["fireball"] = 7;
        unit.ai_blackboard.protected_ally = false;

        _test.Eq(snapshot.occupied_coords.Count, 2, "snapshot occupied coords 不应引用原 unit array。");
        _test.Eq(snapshot.known_active_skill_ids.Count, 1, "snapshot skill ids 不应引用原 unit array。");
        _test.Eq(snapshot.known_skill_level_map[new StringName("fireball")], 4, "snapshot skill level map 不应引用原 unit dictionary。");
        _test.Eq(snapshot.cooldowns[new StringName("fireball")], 2, "snapshot cooldown map 不应引用原 unit dictionary。");
        _test.True(snapshot.ai_blackboard.protected_ally, "snapshot blackboard 不应引用原 unit blackboard。");
    }

    private void TestSnapshotPlainPayloadIsBoundaryOnly()
    {
        BattleAiUnitSnapshot snapshot = BattleAiUnitSnapshot.FromUnit(BuildUnit());
        IReadOnlyDictionary<string, object> payload = snapshot?.BuildPayloadPlain();

        _test.True(payload != null, "BuildPayloadPlain 应生成纯 CLR payload。");
        if (payload == null)
            return;

        _test.Eq((StringName)payload["unit_id"], new StringName("snapshot_actor"), "payload unit_id 应保留。");
        _test.Eq(((IReadOnlyList<Vector2I>)payload["occupied_coords"]).Count, 2, "payload occupied_coords 应保留 typed list。");
        _test.Eq(
            ((IReadOnlyDictionary<StringName, int>)payload["known_skill_level_map"])["fireball"],
            4,
            "payload skill level map 应保留 typed map。"
        );
        _test.Eq(
            ((IReadOnlyDictionary<StringName, int>)payload["cooldowns"])["fireball"],
            2,
            "payload cooldown map 应保留 typed map。"
        );

        IReadOnlyDictionary<string, object> blackboard =
            (IReadOnlyDictionary<string, object>)payload["ai_blackboard"];
        _test.True((bool)blackboard["madness_target_any_team"], "payload blackboard 应保留 madness flag。");
        _test.True((bool)blackboard["protected_ally"], "payload blackboard 应保留 protected flag。");
        _test.Eq(
            (StringName)blackboard["summon_source_unit_id"],
            new StringName("summoner"),
            "payload blackboard 应保留 summon source。"
        );
    }

    private static BattleUnitState BuildUnit()
    {
        var unit = new BattleUnitState
        {
            unit_id = "snapshot_actor",
            display_name = "Snapshot Actor",
            faction_id = "hostile",
            coord = new Vector2I(2, 3),
            footprint_size = new Vector2I(2, 1),
            current_hp = 17,
            current_ap = 2,
            current_mp = 11,
            current_stamina = 9,
            current_aura = 1,
            current_move_points = 3,
            is_alive = true,
        };
        unit.occupied_coords.Clear();
        unit.occupied_coords.Add(new Vector2I(2, 3));
        unit.occupied_coords.Add(new Vector2I(3, 3));
        unit.known_active_skill_ids.Add("fireball");
        unit.known_skill_level_map["fireball"] = 4;
        unit.cooldowns["fireball"] = 2;
        unit.ai_blackboard.madness_target_any_team = true;
        unit.ai_blackboard.protected_ally = true;
        unit.ai_blackboard.summon_source_unit_id = "summoner";
        unit.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "burning",
                source_unit_id = "snapshot_actor",
                stacks = 1,
            }
        );
        return unit;
    }

}
