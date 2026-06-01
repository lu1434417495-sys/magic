using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_misfortune_service_regression : SceneTree
{
    private static readonly StringName ReverseFortuneStatusId = "reverse_fortune";

    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        Run();
    }

    private void Run()
    {
        TestRuntimeTracksAllCalamityReasonsAndSnapshot();
        TestFirstCriticalFailGrantsReverseFortuneAndCapClamps();

        if (_failures.Count == 0)
        {
            GD.Print("MisfortuneService regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"MisfortuneService regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestRuntimeTracksAllCalamityReasonsAndSnapshot()
    {
        BattleRuntimeModule runtime = BuildRuntime(-6, 2);
        try
        {
            BattleState state = runtime.get_state();
            BattleUnitState hero = GetRuntimeUnit(state, "hero");
            BattleUnitState buddy = GetRuntimeUnit(state, "buddy");
            if (hero == null || buddy == null)
            {
                AssertTrue(false, "全理由 case 前置构建失败。");
                return;
            }

            DispatchFateEvent(runtime, "ordinary_miss", "hero");
            runtime.mark_applied_statuses_for_turn_timing(hero, new GArray { new StringName("stunned") });
            buddy.current_hp = 0;
            buddy.is_alive = false;
            runtime.clear_defeated_unit(buddy);
            hero.current_hp = 20;
            hero.current_ap = 1;
            state.phase = "unit_acting";
            state.active_unit_id = hero.unit_id;
            runtime.issue_command(BuildWaitCommand(hero.unit_id));
            runtime.notify_member_boss_phase_changed("hero", "phase_2");
            DispatchFateEvent(runtime, "critical_fail", "hero");
            DispatchFateEvent(runtime, "ordinary_miss", "hero");

            AssertEq(runtime.get_member_calamity_cap("hero"), 6, "rank 2/4 bonus 与极低 hidden luck 组合后 calamity cap 应为 6。");
            AssertEq(runtime.get_member_calamity("hero"), 6, "六类首次坏运事件后 calamity 应累计到 6。");
            AssertFalse(
                hero.has_status_effect(ReverseFortuneStatusId),
                "若第一条 calamity 事件不是大失败，则不应补发 reverse_fortune。"
            );

            var snapshotRuntime = new SnapshotTestRuntime
            {
                BattleState = state,
                BattleRuntime = runtime,
                ActiveBattleEncounterId = "misfortune_test_anchor",
                ActiveBattleEncounterName = "灾厄测试遭遇",
            };
            var builder = new GameRuntimeSnapshotBuilder();
            builder.Setup(snapshotRuntime);
            GDictionary snapshot = builder.BuildHeadlessSnapshot();
            string textSnapshot = builder.BuildTextSnapshot();
            builder.Dispose();

            AssertEq(
                IntValue(Dict(Dict(snapshot, "battle"), "calamity_by_member_id"), "hero", -1),
                6,
                "battle snapshot 应暴露 hero 的当前 calamity。"
            );
            AssertTrue(textSnapshot.Contains("calamity=hero=6"), "battle 文本快照应渲染 calamity 段。");
        }
        finally
        {
            runtime.dispose();
        }
    }

    private void TestFirstCriticalFailGrantsReverseFortuneAndCapClamps()
    {
        BattleRuntimeModule runtime = BuildRuntime(0, 0);
        try
        {
            BattleState state = runtime.get_state();
            BattleUnitState hero = GetRuntimeUnit(state, "hero");
            BattleUnitState buddy = GetRuntimeUnit(state, "buddy");
            if (hero == null || buddy == null)
            {
                AssertTrue(false, "critical fail first case 前置构建失败。");
                return;
            }

            DispatchFateEvent(runtime, "critical_fail", "hero");
            AssertEq(runtime.get_member_calamity_cap("hero"), 3, "默认角色 calamity cap 应为 3。");
            AssertEq(runtime.get_member_calamity("hero"), 1, "第一次 critical_fail 应先授予 1 点 calamity。");
            AssertTrue(
                hero.has_status_effect(ReverseFortuneStatusId),
                "第一次 calamity 事件就是大失败时应授予 reverse_fortune。"
            );
            BattleStatusEffectState reverseFortune = hero.get_status_effect(ReverseFortuneStatusId);
            AssertEq(
                reverseFortune != null ? reverseFortune.duration : -1,
                60,
                "reverse_fortune 应维持 1 回合基准 duration。"
            );

            DispatchFateEvent(runtime, "ordinary_miss", "hero");
            runtime.mark_applied_statuses_for_turn_timing(hero, new GArray { new StringName("fear") });
            runtime.notify_member_boss_phase_changed("hero", "phase_2");
            buddy.current_hp = 0;
            buddy.is_alive = false;
            runtime.clear_defeated_unit(buddy);

            AssertEq(runtime.get_member_calamity("hero"), 3, "超出默认上限后 calamity 不应继续增长。");
            AssertEq(
                IntValue(runtime.get_calamity_by_member_id(), "hero", 0),
                3,
                "BattleRuntime.calamity_by_member_id 应与 MisfortuneService 计算结果保持同步。"
            );
        }
        finally
        {
            runtime.dispose();
        }
    }

    private static BattleRuntimeModule BuildRuntime(
        int hiddenLuckAtBirth,
        int calamityCapacityBonus
    )
    {
        var runtime = new BattleRuntimeModule();
        runtime.setup(null, new GDictionary(), new GDictionary(), new GDictionary(), null);

        BattleUnitState hero = BuildMemberUnit(
            "hero",
            "Hero",
            100,
            hiddenLuckAtBirth,
            calamityCapacityBonus
        );
        BattleUnitState buddy = BuildMemberUnit("buddy", "Buddy", 80, 0, 0);
        BattleUnitState boss = BuildEnemyUnit("boss_01", "Boss");
        var encounterAnchor = new EncounterAnchorData
        {
            entity_id = "misfortune_test_anchor",
            display_name = "灾厄测试遭遇",
            world_coord = Vector2I.Zero,
            faction_id = "hostile",
            region_tag = "test_region",
            enemy_roster_template_id = "",
            encounter_profile_id = "",
            growth_stage = 0,
        };
        var context = new GDictionary
        {
            ["battle_map_size"] = new Vector2I(6, 6),
            ["ally_spawns"] = new GArray { new Vector2I(1, 1), new Vector2I(2, 1) },
            ["enemy_spawns"] = new GArray { new Vector2I(4, 4) },
            ["battle_party"] = new GArray { hero.to_dict(), buddy.to_dict() },
            ["enemy_units"] = new GArray { boss.to_dict() },
        };
        BattleState state = runtime.start_battle(encounterAnchor, 101, context);
        BattleUnitState runtimeHero = GetRuntimeUnit(state, "hero");
        BattleUnitState runtimeBuddy = GetRuntimeUnit(state, "buddy");
        if (runtimeHero != null)
            runtime._grid_service.place_unit(state, runtimeHero, new Vector2I(1, 1), true);
        if (runtimeBuddy != null)
            runtime._grid_service.place_unit(state, runtimeBuddy, new Vector2I(2, 1), true);
        return runtime;
    }

    private static BattleUnitState BuildMemberUnit(
        StringName memberId,
        string displayName,
        int hpMax,
        int hiddenLuckAtBirth,
        int calamityCapacityBonus
    )
    {
        return new BattleUnitState
        {
            unit_id = memberId,
            source_member_id = memberId,
            display_name = displayName,
            faction_id = "player",
            control_mode = "manual",
            attribute_snapshot = BuildAttributeSnapshot(
                hpMax,
                hiddenLuckAtBirth,
                calamityCapacityBonus
            ),
            current_hp = hpMax,
            current_mp = 0,
            current_stamina = 0,
            current_aura = 0,
            current_ap = 1,
            is_alive = true,
        };
    }

    private static BattleUnitState BuildEnemyUnit(StringName unitId, string displayName)
    {
        return new BattleUnitState
        {
            unit_id = unitId,
            display_name = displayName,
            faction_id = "hostile",
            control_mode = "ai",
            attribute_snapshot = BuildAttributeSnapshot(160, 0, 0),
            current_hp = 160,
            current_ap = 1,
            is_alive = true,
        };
    }

    private static AttributeSnapshot BuildAttributeSnapshot(
        int hpMax,
        int hiddenLuckAtBirth,
        int calamityCapacityBonus
    )
    {
        var snapshot = new AttributeSnapshot();
        snapshot.set_value(AttributeService.HP_MAX_ID(), hpMax);
        snapshot.set_value(AttributeService.MP_MAX_ID(), 0);
        snapshot.set_value(AttributeService.STAMINA_MAX_ID(), 0);
        snapshot.set_value(AttributeService.AURA_MAX_ID(), 0);
        snapshot.set_value(AttributeService.ACTION_POINTS_ID(), 1);
        snapshot.set_value("hidden_luck_at_birth", hiddenLuckAtBirth);
        snapshot.set_value("calamity_capacity_bonus", calamityCapacityBonus);
        SeedAttributeSnapshotBaseAttributesAndAc(snapshot);
        return snapshot;
    }

    private static void SeedAttributeSnapshotBaseAttributesAndAc(AttributeSnapshot snapshot)
    {
        if (snapshot == null)
            return;
        foreach (
            StringName attributeId in new[]
            {
                new StringName("strength"),
                new StringName("agility"),
                new StringName("constitution"),
                new StringName("perception"),
                new StringName("intelligence"),
                new StringName("willpower"),
            }
        )
        {
            if (!snapshot.has_value(attributeId))
                snapshot.set_value(attributeId, 10);
        }
        if (!snapshot.has_value(AttributeService.ARMOR_CLASS_ID()))
        {
            int agilityModifier = AttributeSnapshot.calculate_score_modifier(
                snapshot.get_value("agility")
            );
            snapshot.set_value(
                AttributeService.ARMOR_CLASS_ID(),
                Math.Clamp(AttributeService.BASE_ARMOR_CLASS_VALUE() + agilityModifier, 1, 99)
            );
        }
    }

    private static BattleUnitState GetRuntimeUnit(BattleState state, StringName memberId)
    {
        if (state == null || !state.units.ContainsKey(memberId))
            return null;
        return state.units[memberId].AsGodotObject() as BattleUnitState;
    }

    private static BattleCommand BuildWaitCommand(StringName unitId)
    {
        return new BattleCommand { command_type = BattleCommand.TYPE_WAIT(), unit_id = unitId };
    }

    private static void DispatchFateEvent(
        BattleRuntimeModule runtime,
        StringName eventType,
        StringName memberId
    )
    {
        runtime.get_fate_event_bus().dispatch(
            eventType,
            new GDictionary
            {
                ["battle_id"] = runtime.get_state()?.battle_id ?? new StringName(""),
                ["attacker_member_id"] = memberId,
            }
        );
    }

    private static GDictionary Dict(GDictionary source, string key)
    {
        if (
            source != null
            && source.ContainsKey(key)
            && source[key].VariantType == Variant.Type.Dictionary
        )
            return source[key].AsGodotDictionary();
        return new GDictionary();
    }

    private static int IntValue(GDictionary source, string key, int fallback = 0)
    {
        if (source == null || !source.ContainsKey(key))
            return fallback;
        Variant value = source[key];
        return value.VariantType == Variant.Type.Nil ? fallback : value.AsInt32();
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
            _failures.Add(message);
    }

    private void AssertFalse(bool condition, string message)
    {
        if (condition)
            _failures.Add(message);
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
            _failures.Add($"{message} | actual={actual} expected={expected}");
    }
}
