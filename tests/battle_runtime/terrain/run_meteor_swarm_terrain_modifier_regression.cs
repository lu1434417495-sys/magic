using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_meteor_swarm_terrain_modifier_regression : SceneTree
{
    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestDustAttackModifierUsesSchemaNotSourceId();
        TestDustDistanceGateAndEndpointStacking();
        TestDustExpiresWhileBattleLifetimeTerrainStaysActive();

        if (_failures.Count == 0)
        {
            GD.Print("Meteor swarm terrain modifier regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Meteor swarm terrain modifier regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestDustAttackModifierUsesSchemaNotSourceId()
    {
        Fixture setup = BuildRuntimeWithUnits(new Vector2I(5, 3), new Vector2I(0, 1), new Vector2I(3, 1));
        CombatEffectDef oddNamedDust = BuildDustEffect("schema_driven_not_meteor_named");
        AssertTrue(
            setup.Runtime._terrain_effect_system.upsert_timed_terrain_effect(
                setup.Target.coord,
                setup.Attacker,
                null,
                oddNamedDust,
                "dust_target"
            ),
            "dust schema 测试应能写入 target footprint。"
        );

        AttackPolicyProbe probe = BuildPolicyAttackProbe(setup.Runtime, setup.Attacker, setup.Target);
        AssertEq(
            probe.AttackCheck.SituationalAttackPenalty,
            2,
            "尘土命中 -2 应通过 accuracy_modifier_spec 生效，而不是靠 source id。"
        );
        AssertEq(probe.ModifierBundle.Breakdown.Count, 1, "尘土命中修饰应输出一条 post-stack breakdown。");
        AssertEq(
            probe.ModifierBundle.Breakdown.Count > 0
                ? probe.ModifierBundle.Breakdown[0].modifier_delta
                : 0,
            -2,
            "breakdown 应保留有效 -2 修饰。"
        );
    }

    private void TestDustDistanceGateAndEndpointStacking()
    {
        Fixture adjacentSetup = BuildRuntimeWithUnits(
            new Vector2I(4, 2),
            new Vector2I(0, 0),
            new Vector2I(1, 0)
        );
        AssertTrue(
            adjacentSetup.Runtime._terrain_effect_system.upsert_timed_terrain_effect(
                adjacentSetup.Target.coord,
                adjacentSetup.Attacker,
                null,
                BuildDustEffect("adjacent_dust"),
                "adjacent_dust"
            ),
            "相邻尘土 fixture 应能写入。"
        );
        AttackPolicyProbe adjacentProbe = BuildPolicyAttackProbe(
            adjacentSetup.Runtime,
            adjacentSetup.Attacker,
            adjacentSetup.Target
        );
        AssertEq(
            adjacentProbe.ModifierBundle.Breakdown.Count,
            0,
            "distance_min_exclusive=1 时相邻攻击不应吃尘土命中惩罚。"
        );

        Fixture doubleSetup = BuildRuntimeWithUnits(
            new Vector2I(5, 2),
            new Vector2I(0, 0),
            new Vector2I(3, 0)
        );
        AssertTrue(
            doubleSetup.Runtime._terrain_effect_system.upsert_timed_terrain_effect(
                doubleSetup.Attacker.coord,
                doubleSetup.Attacker,
                null,
                BuildDustEffect("attacker_dust"),
                "attacker_dust"
            ),
            "attacker footprint 尘土应能写入。"
        );
        AssertTrue(
            doubleSetup.Runtime._terrain_effect_system.upsert_timed_terrain_effect(
                doubleSetup.Target.coord,
                doubleSetup.Attacker,
                null,
                BuildDustEffect("target_dust"),
                "target_dust"
            ),
            "target footprint 尘土应能写入。"
        );
        AttackPolicyProbe doubleProbe = BuildPolicyAttackProbe(
            doubleSetup.Runtime,
            doubleSetup.Attacker,
            doubleSetup.Target
        );
        AssertEq(
            doubleProbe.AttackCheck.SituationalAttackPenalty,
            2,
            "attacker/target 同时处于 dust 时同 stack_key 不应叠成 -4。"
        );
        AssertEq(
            doubleProbe.ModifierBundle.Breakdown.Count,
            1,
            "同 stack_key dust 只应保留一条 post-stack breakdown。"
        );
    }

    private void TestDustExpiresWhileBattleLifetimeTerrainStaysActive()
    {
        Fixture setup = BuildRuntimeWithUnits(new Vector2I(5, 2), new Vector2I(0, 0), new Vector2I(3, 0));
        AssertTrue(
            setup.Runtime._terrain_effect_system.upsert_timed_terrain_effect(
                setup.Target.coord,
                setup.Attacker,
                null,
                BuildDustEffect("meteor_swarm_dust"),
                "timed_dust"
            ),
            "timed dust 应能写入。"
        );
        AssertTrue(
            setup.Runtime._terrain_effect_system.upsert_timed_terrain_effect(
                setup.Target.coord,
                setup.Attacker,
                null,
                BuildBattleTerrainEffect("meteor_swarm_rubble", 2),
                "rubble"
            ),
            "battle lifetime rubble 应能写入。"
        );
        setup.State.timeline.current_tu = 55;
        setup.Runtime._terrain_effect_system.process_timed_terrain_effects(new BattleEventBatch());

        AttackPolicyProbe probe = BuildPolicyAttackProbe(setup.Runtime, setup.Attacker, setup.Target);
        AssertEq(
            probe.ModifierBundle.Breakdown.Count,
            0,
            "timed dust 到期后不应继续提供命中惩罚。"
        );
        AssertEq(
            setup.Runtime._terrain_effect_system.get_move_cost_delta_for_unit_target(
                setup.Target,
                setup.Target.coord
            ),
            2,
            "battle lifetime rubble 推进后仍应保留移动成本。"
        );
    }

    private AttackPolicyProbe BuildPolicyAttackProbe(
        BattleRuntimeModule runtime,
        BattleUnitState attacker,
        BattleUnitState target
    )
    {
        BattleAttackCheckPolicyService attackPolicy = runtime.get_attack_check_policy_service();
        BattleAttackCheckPolicyContext context = attackPolicy.BuildAttackContext(
            runtime.get_state(),
            attacker,
            target,
            null,
            "skill_attack_check",
            "execute",
            false
        );
        return new AttackPolicyProbe
        {
            AttackCheck = attackPolicy.BuildAttackCheck(context, 0, 0),
            ModifierBundle = attackPolicy.BuildModifierBundle(context),
        };
    }

    private Fixture BuildRuntimeWithUnits(Vector2I mapSize, Vector2I attackerCoord, Vector2I targetCoord)
    {
        var runtime = new BattleRuntimeModule();
        runtime.setup(null, new GDictionary(), new GDictionary(), new GDictionary());
        BattleState state = BuildState(mapSize);
        BattleUnitState attacker = BuildUnit("attacker", attackerCoord, "player");
        BattleUnitState target = BuildUnit("target", targetCoord, "enemy");
        state.units[attacker.unit_id] = attacker;
        state.units[target.unit_id] = target;
        state.ally_unit_ids = new Godot.Collections.Array<StringName> { attacker.unit_id };
        state.enemy_unit_ids = new Godot.Collections.Array<StringName> { target.unit_id };
        state.active_unit_id = attacker.unit_id;
        AssertTrue(
            runtime._grid_service.place_unit(state, attacker, attacker.coord, true),
            "attacker 应能放入 terrain modifier fixture。"
        );
        AssertTrue(
            runtime._grid_service.place_unit(state, target, target.coord, true),
            "target 应能放入 terrain modifier fixture。"
        );
        runtime._state = state;
        return new Fixture
        {
            Runtime = runtime,
            State = state,
            Attacker = attacker,
            Target = target,
        };
    }

    private static BattleState BuildState(Vector2I mapSize)
    {
        var state = new BattleState { map_size = mapSize };
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                var coord = new Vector2I(x, y);
                var cell = new BattleCellState
                {
                    coord = coord,
                    passable = true,
                };
                state.cells[coord] = cell;
            }
        }
        return state;
    }

    private static BattleUnitState BuildUnit(StringName unitId, Vector2I coord, StringName factionId)
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            coord = coord,
            is_alive = true,
        };
        SeedBaseAttributesAndDeriveAc(unit);
        unit.refresh_footprint();
        return unit;
    }

    private static void SeedBaseAttributesAndDeriveAc(BattleUnitState unit)
    {
        if (unit == null)
            return;
        SeedAttributeSnapshotBaseAttributesAndAc(unit.attribute_snapshot);
    }

    private static void SeedAttributeSnapshotBaseAttributesAndAc(AttributeSnapshot snapshot)
    {
        if (snapshot == null)
            return;
        foreach (
            StringName attributeId in new Godot.Collections.Array<StringName>
            {
                UnitBaseAttributes.STRENGTH(),
                UnitBaseAttributes.AGILITY(),
                UnitBaseAttributes.CONSTITUTION(),
                UnitBaseAttributes.PERCEPTION(),
                UnitBaseAttributes.INTELLIGENCE(),
                UnitBaseAttributes.WILLPOWER(),
            }
        )
        {
            if (!snapshot.has_value(attributeId))
                snapshot.set_value(attributeId, 10);
        }
        if (!snapshot.has_value(AttributeService.ARMOR_CLASS_ID()))
        {
            int agilityModifier = AttributeSnapshot.calculate_score_modifier(
                snapshot.get_value(UnitBaseAttributes.AGILITY())
            );
            snapshot.set_value(
                AttributeService.ARMOR_CLASS_ID(),
                Mathf.Clamp(AttributeService.BASE_ARMOR_CLASS_VALUE() + agilityModifier, 1, 99)
            );
        }
    }

    private static CombatEffectDef BuildDustEffect(StringName effectId)
    {
        CombatEffectDef effect = BuildTimedTerrainEffect(effectId, 0, "timed", 50, 5);
        effect.@params["accuracy_modifier_spec"] = new GDictionary
        {
            ["source_domain"] = "terrain",
            ["label"] = "尘土",
            ["modifier_delta"] = -2,
            ["stack_key"] = "dust_attack_roll_penalty",
            ["stack_mode"] = "max",
            ["roll_kind_filter"] = "spell_attack",
            ["endpoint_mode"] = "either",
            ["distance_min_exclusive"] = 1,
            ["distance_max_inclusive"] = -1,
            ["target_team_filter"] = "any",
            ["footprint_mode"] = "any_cell",
            ["applies_to"] = "attack_roll",
        };
        return effect;
    }

    private static CombatEffectDef BuildBattleTerrainEffect(StringName effectId, int moveCostDelta)
    {
        return BuildTimedTerrainEffect(effectId, moveCostDelta, "battle", 0, 0);
    }

    private static CombatEffectDef BuildTimedTerrainEffect(
        StringName effectId,
        int moveCostDelta,
        StringName lifetimePolicy,
        int durationTu,
        int tickIntervalTu
    )
    {
        return new CombatEffectDef
        {
            effect_type = "terrain_effect",
            tick_effect_type = "none",
            terrain_effect_id = effectId,
            duration_tu = durationTu,
            tick_interval_tu = tickIntervalTu,
            effect_target_team_filter = "any",
            @params = new GDictionary
            {
                ["lifetime_policy"] = lifetimePolicy,
                ["move_cost_delta"] = moveCostDelta,
                ["display_name"] = effectId.ToString(),
                ["render_overlay_id"] = effectId.ToString(),
            },
        };
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
        {
            _failures.Add($"{message} actual={actual} expected={expected}");
        }
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private sealed class Fixture
    {
        public BattleRuntimeModule Runtime;
        public BattleState State;
        public BattleUnitState Attacker;
        public BattleUnitState Target;
    }

    private sealed class AttackPolicyProbe
    {
        public AttackCheckInput AttackCheck;
        public BattleAttackRollModifierBundle ModifierBundle;
    }
}
