using Godot;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_meteor_swarm_terrain_modifier_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestResult exitCode = Run();
        RequestTestExit(exitCode);
    }

    private TestResult Run()
    {
        TestDustAttackModifierUsesSchemaNotSourceId();
        TestDustDistanceGateAndEndpointStacking();
        TestDustExpiresWhileBattleLifetimeTerrainStaysActive();

        return _test.Finish("Meteor swarm terrain modifier regression");
    }

    private void TestDustAttackModifierUsesSchemaNotSourceId()
    {
        Fixture setup = BuildRuntimeWithUnits(new Vector2I(5, 3), new Vector2I(0, 1), new Vector2I(3, 1));
        CombatEffectDefinition oddNamedDust = BuildDustEffect("schema_driven_not_meteor_named");
        _test.True(
            setup.Runtime._terrain_effect_system.UpsertTimedTerrainEffectFromDefinition(
                setup.Target.coord,
                setup.Attacker,
                null,
                oddNamedDust,
                "dust_target"
            ),
            "dust schema 测试应能写入 target footprint。"
        );

        AttackPolicyProbe probe = BuildPolicyAttackProbe(setup.Runtime, setup.Attacker, setup.Target);
        _test.Eq(
            probe.AttackCheck.SituationalAttackPenalty,
            2,
            "尘土命中 -2 应通过 accuracy_modifier_spec 生效，而不是靠 source id。"
        );
        _test.Eq(probe.ModifierBundle.Breakdown.Count, 1, "尘土命中修饰应输出一条 post-stack breakdown。");
        _test.Eq(
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
        _test.True(
            adjacentSetup.Runtime._terrain_effect_system.UpsertTimedTerrainEffectFromDefinition(
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
        _test.Eq(
            adjacentProbe.ModifierBundle.Breakdown.Count,
            0,
            "distance_min_exclusive=1 时相邻攻击不应吃尘土命中惩罚。"
        );

        Fixture doubleSetup = BuildRuntimeWithUnits(
            new Vector2I(5, 2),
            new Vector2I(0, 0),
            new Vector2I(3, 0)
        );
        _test.True(
            doubleSetup.Runtime._terrain_effect_system.UpsertTimedTerrainEffectFromDefinition(
                doubleSetup.Attacker.coord,
                doubleSetup.Attacker,
                null,
                BuildDustEffect("attacker_dust"),
                "attacker_dust"
            ),
            "attacker footprint 尘土应能写入。"
        );
        _test.True(
            doubleSetup.Runtime._terrain_effect_system.UpsertTimedTerrainEffectFromDefinition(
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
        _test.Eq(
            doubleProbe.AttackCheck.SituationalAttackPenalty,
            2,
            "attacker/target 同时处于 dust 时同 stack_key 不应叠成 -4。"
        );
        _test.Eq(
            doubleProbe.ModifierBundle.Breakdown.Count,
            1,
            "同 stack_key dust 只应保留一条 post-stack breakdown。"
        );
    }

    private void TestDustExpiresWhileBattleLifetimeTerrainStaysActive()
    {
        Fixture setup = BuildRuntimeWithUnits(new Vector2I(5, 2), new Vector2I(0, 0), new Vector2I(3, 0));
        _test.True(
            setup.Runtime._terrain_effect_system.UpsertTimedTerrainEffectFromDefinition(
                setup.Target.coord,
                setup.Attacker,
                null,
                BuildDustEffect("meteor_swarm_dust"),
                "timed_dust"
            ),
            "timed dust 应能写入。"
        );
        _test.True(
            setup.Runtime._terrain_effect_system.UpsertTimedTerrainEffectFromDefinition(
                setup.Target.coord,
                setup.Attacker,
                null,
                BuildBattleTerrainEffect("meteor_swarm_rubble", 2),
                "rubble"
            ),
            "battle lifetime rubble 应能写入。"
        );
        setup.State.timeline.current_tu = 55;
        setup.Runtime._terrain_effect_system.ProcessTimedTerrainEffects(new BattleEventBatch());

        AttackPolicyProbe probe = BuildPolicyAttackProbe(setup.Runtime, setup.Attacker, setup.Target);
        _test.Eq(
            probe.ModifierBundle.Breakdown.Count,
            0,
            "timed dust 到期后不应继续提供命中惩罚。"
        );
        _test.Eq(
            setup.Runtime._terrain_effect_system.GetMoveCostDeltaForUnitTarget(
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
        BattleAttackCheckPolicyService attackPolicy = runtime.GetAttackCheckPolicyService();
        BattleAttackCheckPolicyContext context = attackPolicy.BuildSkillDefinitionAttackContext(
            runtime.GetState(),
            attacker,
            target,
            (SkillDefinition)null,
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
        runtime.setup();
        BattleState state = BuildState(mapSize);
        BattleUnitState attacker = BuildUnit("attacker", attackerCoord, "player");
        BattleUnitState target = BuildUnit("target", targetCoord, "enemy");
        state.SetUnit(attacker);
        state.SetUnit(target);
        state.ally_unit_ids = new Godot.Collections.Array<StringName> { attacker.unit_id };
        state.enemy_unit_ids = new Godot.Collections.Array<StringName> { target.unit_id };
        state.active_unit_id = attacker.unit_id;
        _test.True(
            runtime._grid_service.PlaceUnit(state, attacker, attacker.coord, true),
            "attacker 应能放入 terrain modifier fixture。"
        );
        _test.True(
            runtime._grid_service.PlaceUnit(state, target, target.coord, true),
            "target 应能放入 terrain modifier fixture。"
        );
        runtime.SetupStateForTests(state);
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
                state.SetCell(coord, cell);
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
        unit.RefreshFootprint();
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
                UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Strength),
                UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Agility),
                UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Constitution),
                UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Perception),
                UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Intelligence),
                UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Willpower),
            }
        )
        {
            if (!snapshot.HasValue(attributeId))
                snapshot.SetValue(attributeId, 10);
        }
        if (!snapshot.HasValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass)))
        {
            int agilityModifier = AttributeSnapshot.CalculateScoreModifier(
                snapshot.GetValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Agility))
            );
            snapshot.SetValue(
                AttributeService.ToStringName(AttributeIdKind.ArmorClass),
                Mathf.Clamp(AttributeService.BASE_ARMOR_CLASS + agilityModifier, 1, 99)
            );
        }
    }

    private static CombatEffectDefinition BuildDustEffect(StringName effectId)
    {
        return BuildTimedTerrainEffect(
            effectId,
            0,
            "timed",
            50,
            5,
            new BattleAttackRollModifierSpec
            {
                source_domain = "terrain",
                label = "尘土",
                modifier_delta = -2,
                stack_key = "dust_attack_roll_penalty",
                stack_mode = "max",
                roll_kind_filter = "spell_attack",
                endpoint_mode = "either",
                distance_min_exclusive = 1,
                distance_max_inclusive = -1,
                target_team_filter = "any",
                footprint_mode = "any_cell",
                applies_to = "attack_roll",
            }
        );
    }

    private static CombatEffectDefinition BuildBattleTerrainEffect(StringName effectId, int moveCostDelta)
    {
        return BuildTimedTerrainEffect(effectId, moveCostDelta, "battle", 0, 0);
    }

    private static CombatEffectDefinition BuildTimedTerrainEffect(
        StringName effectId,
        int moveCostDelta,
        StringName lifetimePolicy,
        int durationTu,
        int tickIntervalTu,
        BattleAttackRollModifierSpec accuracyModifierSpec = null
    ) =>
        TestSkillDefinitionProjection.BuildEffect(
            "terrain_effect",
            effectTargetTeamFilter: "any",
            tickEffectType: "none",
            lifetimePolicy: lifetimePolicy,
            moveCostDelta: moveCostDelta,
            terrainEffectId: effectId,
            displayName: effectId.ToString(),
            renderOverlayId: effectId,
            durationTu: durationTu,
            tickIntervalTu: tickIntervalTu,
            accuracyModifierSpec: accuracyModifierSpec
        );

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
