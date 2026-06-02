using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using GActionArray = Godot.Collections.Array<EnemyAiAction>;
using GStringArray = Godot.Collections.Array<string>;
using GStateArray = Godot.Collections.Array<EnemyAiStateDef>;

public partial class run_move_to_range_progress_regression : SceneTree
{
    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestCandidateRequestBoundaryIsPlainTypedCSharp();
        TestCandidateRequestTypedValidation();
        TestCandidateProbeActionBuildsTypedSections();
        TestMoveToRangePrefersProgressOverWaitWhenFarFromBand();
        TestMoveToRangeUsesPathDetourWhenDirectProgressIsBlocked();

        if (_failures.Count == 0)
        {
            GD.Print("Move-to-range progress regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Move-to-range progress regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestCandidateRequestBoundaryIsPlainTypedCSharp()
    {
        Type requestType = typeof(BattleAiCandidateRequest);
        AssertTrue(
            !typeof(GodotObject).IsAssignableFrom(requestType),
            "BattleAiCandidateRequest must be a plain C# payload."
        );
        AssertNoGlobalClass(requestType, "BattleAiCandidateRequest");
        AssertTrue(
            requestType.GetProperty("PathSearchBudget") == null
                && requestType.GetProperty("path_search_budget") == null
                && requestType.GetProperty("TacticalParams") == null
                && requestType.GetProperty("RuntimeMetadata") == null,
            "BattleAiCandidateRequest must not expose Godot Dictionary section properties."
        );
        AssertTrue(
            requestType.GetMethod("RequireValidPayload") == null,
            "BattleAiCandidateRequest must not keep the old fail-loud validation API."
        );

        Type serviceType = typeof(BattleAiCandidateEvaluationService);
        AssertTrue(
            !typeof(GodotObject).IsAssignableFrom(serviceType),
            "BattleAiCandidateEvaluationService must be a plain C# service."
        );
        AssertNoGlobalClass(serviceType, "BattleAiCandidateEvaluationService");
        AssertTrue(
            serviceType.GetMethod("setup") == null
                && serviceType.GetMethod("evaluate") == null
                && serviceType.GetMethod("evaluate_move_to_range_request") == null
                && serviceType.GetMethod("register_evaluator") == null
                && serviceType.GetMethod("_trim_reason") == null,
            "BattleAiCandidateEvaluationService must not keep GDScript-style API."
        );

        Type evaluatorType = typeof(BattleAiMoveToRangeCandidateEvaluator);
        AssertTrue(
            !typeof(GodotObject).IsAssignableFrom(evaluatorType),
            "BattleAiMoveToRangeCandidateEvaluator must be a plain C# helper."
        );
        AssertNoGlobalClass(evaluatorType, "BattleAiMoveToRangeCandidateEvaluator");
        AssertTrue(
            evaluatorType.GetMethod("evaluate_move_to_range_request") == null,
            "BattleAiMoveToRangeCandidateEvaluator must expose only PascalCase typed API."
        );
    }

    private void TestCandidateRequestTypedValidation()
    {
        BattleAiCandidateRequest request = BuildValidRequest();
        AssertTrue(
            request.TryValidateMoveToRange(out string error),
            $"valid typed move_to_range request should pass validation: {error}"
        );

        request = BuildValidRequest();
        request.ActionIntent = "legacy_intent";
        AssertFalse(
            request.TryValidateMoveToRange(out error),
            "unknown action intent must fail candidate request validation."
        );
        AssertContains(error, "action_intent", "invalid intent failure should name action_intent.");

        request = BuildValidRequest();
        request.MaxCandidateCount = 5;
        request.SetMoveToRangeSections(
            new MoveToRangePathSearchBudget { MaxCost = 2, MaxDestinations = 3 },
            new MoveToRangeTacticalParams(),
            new MoveToRangeRuntimeMetadata()
        );
        AssertFalse(
            request.TryValidateMoveToRange(out error),
            "max_candidate_count must not exceed typed path budget max destinations."
        );
        AssertContains(error, "max_candidate_count", "path budget validation should explain max candidate overflow.");
    }

    private void TestCandidateProbeActionBuildsTypedSections()
    {
        var action = new BattleAiCandidateProbeTestAction();
        BattleAiCandidateRequest request = action.build_candidate_request(null);
        request.ActorUnitId = "probe_actor";

        AssertTrue(
            request.TryValidateMoveToRange(out string error),
            $"probe test action should build a valid typed request after actor injection: {error}"
        );
        AssertTrue(
            request.TryGetMoveToRangeSections(
                out MoveToRangePathSearchBudget budget,
                out MoveToRangeTacticalParams tactical,
                out MoveToRangeRuntimeMetadata runtime,
                out error
            ),
            $"probe request should expose typed section snapshots: {error}"
        );
        AssertEq(budget.MaxCost, 2, "probe path budget max cost should stay typed.");
        AssertEq(budget.MaxDestinations, 4, "probe path budget max destinations should stay typed.");
        AssertEq(tactical.TargetSelector, new StringName("nearest_enemy"), "probe tactical selector should stay typed.");
        AssertEq(runtime.EffectiveAttackRange, -1, "probe runtime metadata should stay typed.");
    }

    private void TestMoveToRangePrefersProgressOverWaitWhenFarFromBand()
    {
        BattleRuntimeModule runtime = BuildRuntimeWithEnemyContent();
        EnemyAiBrainDef brain = BuildBrain(
            "far_gap_mover_brain",
            "engage",
            new MoveToRangeAction
            {
                action_id = "far_gap_close_in",
                action_intent = BattleAiActionIntent.Positioning,
                score_bucket_id = "positioning",
                ai_evaluation_mode = MoveToRangeAction.AiEvaluationCandidateRequest,
                target_selector = "nearest_enemy",
                desired_min_distance = 4,
                desired_max_distance = 5,
            },
            new WaitAction { action_id = "far_gap_wait" }
        );
        runtime._enemy_ai_brains[brain.brain_id] = brain;
        runtime._ai_service.Setup(
            new Dictionary<StringName, EnemyAiBrainDef> { [brain.brain_id] = brain },
            null
        );

        BattleState state = BuildFlatState(new Vector2I(31, 3));
        runtime._state = state;
        BattleUnitState mover = BuildAiUnit(
            "far_gap_enemy",
            "Far gap mover",
            "hostile",
            new Vector2I(1, 1),
            brain.brain_id,
            "engage"
        );
        BattleUnitState player = BuildManualUnit(
            "far_gap_player",
            "Far gap target",
            "player",
            new Vector2I(28, 1)
        );
        AddUnitToState(runtime, state, mover, isEnemy: true);
        AddUnitToState(runtime, state, player, isEnemy: false);

        BattleAiDecision decision = runtime._ai_service.ChooseCommand(BuildAiContext(runtime, mover));
        AssertTrue(decision?.command != null, "far move_to_range should produce a legal command.");
        AssertEq(
            decision?.command?.command_type ?? new StringName(""),
            BattleCommand.TYPE_MOVE(),
            "far move_to_range should not wait when outside the distance band."
        );
        AssertEq(
            decision?.command?.target_coord ?? new Vector2I(-1, -1),
            new Vector2I(3, 1),
            "far move_to_range should choose the farthest reachable progress tile."
        );
    }

    private void TestMoveToRangeUsesPathDetourWhenDirectProgressIsBlocked()
    {
        BattleRuntimeModule runtime = BuildRuntimeWithEnemyContent();
        EnemyAiBrainDef brain = BuildBrain(
            "detour_mover_brain",
            "engage",
            new MoveToRangeAction
            {
                action_id = "detour_close_in",
                action_intent = BattleAiActionIntent.Positioning,
                score_bucket_id = "positioning",
                ai_evaluation_mode = MoveToRangeAction.AiEvaluationCandidateRequest,
                target_selector = "nearest_enemy",
                desired_min_distance = 1,
                desired_max_distance = 1,
            },
            new WaitAction { action_id = "detour_wait" }
        );
        runtime._enemy_ai_brains[brain.brain_id] = brain;
        runtime._ai_service.Setup(
            new Dictionary<StringName, EnemyAiBrainDef> { [brain.brain_id] = brain },
            null
        );

        BattleState state = BuildFlatState(new Vector2I(7, 3));
        runtime._state = state;
        BattleUnitState mover = BuildAiUnit(
            "detour_enemy",
            "Detour mover",
            "hostile",
            new Vector2I(1, 1),
            brain.brain_id,
            "engage"
        );
        BattleUnitState blocker = BuildAiUnit(
            "detour_blocker",
            "Blocker",
            "hostile",
            new Vector2I(2, 1),
            brain.brain_id,
            "engage"
        );
        BattleUnitState player = BuildManualUnit(
            "detour_player",
            "Detour target",
            "player",
            new Vector2I(5, 1)
        );
        AddUnitToState(runtime, state, mover, isEnemy: true);
        AddUnitToState(runtime, state, blocker, isEnemy: true);
        AddUnitToState(runtime, state, player, isEnemy: false);

        BattleAiDecision decision = runtime._ai_service.ChooseCommand(BuildAiContext(runtime, mover));
        AssertTrue(decision?.command != null, "detour move_to_range should produce a legal command.");
        AssertEq(
            decision?.command?.command_type ?? new StringName(""),
            BattleCommand.TYPE_MOVE(),
            "detour move_to_range should not wait when a path detour exists."
        );

        Vector2I actual = decision?.command?.target_coord ?? new Vector2I(-1, -1);
        bool validLanding = actual == new Vector2I(2, 0) || actual == new Vector2I(2, 2);
        AssertTrue(
            validLanding,
            $"detour move_to_range should choose one of the equivalent detour tiles. actual={actual}"
        );
    }

    private static BattleAiCandidateRequest BuildValidRequest()
    {
        var request = new BattleAiCandidateRequest
        {
            FamilyId = BattleAiCandidateRequest.FamilyMoveToRange,
            ActionId = "move",
            ActionLabel = "move",
            ActionIntent = BattleAiActionIntent.Positioning,
            ScoreBucketId = "positioning",
            ActorUnitId = "actor",
            FocusTargetUnitId = "target",
            DesiredMinDistance = 1,
            DesiredMaxDistance = 2,
            MaxCandidateCount = 3,
        };
        request.SetMoveToRangeSections(
            new MoveToRangePathSearchBudget
            {
                MaxCost = 2,
                MaxDestinations = 3,
                PreferProgress = true,
            },
            new MoveToRangeTacticalParams
            {
                TargetSelector = "nearest_enemy",
                PositionObjectiveKind = "distance_band_progress",
            },
            new MoveToRangeRuntimeMetadata
            {
                ConfiguredDesiredMinDistance = 1,
                ConfiguredDesiredMaxDistance = 2,
                EffectiveAttackRange = -1,
            }
        );
        return request;
    }

    private static EnemyAiBrainDef BuildBrain(
        StringName brainId,
        StringName stateId,
        MoveToRangeAction moveAction,
        WaitAction waitAction
    )
    {
        var state = new EnemyAiStateDef
        {
            state_id = stateId,
            actions = new GActionArray { moveAction, waitAction },
        };
        return new EnemyAiBrainDef
        {
            brain_id = brainId,
            default_state_id = stateId,
            states = new GStateArray { state },
        };
    }

    private static BattleRuntimeModule BuildRuntimeWithEnemyContent()
    {
        var gameSession = new GameSession();
        var runtime = new BattleRuntimeModule();
        runtime.setup(
            null,
            gameSession.get_skill_defs(),
            gameSession.get_enemy_templates(),
            gameSession.get_enemy_ai_brains(),
            null
        );
        gameSession.Free();
        return runtime;
    }

    private static BattleState BuildFlatState(Vector2I mapSize)
    {
        var state = new BattleState
        {
            battle_id = "move_to_range_progress_regression",
            phase = "timeline_running",
            map_size = mapSize,
            timeline = new BattleTimelineState(),
        };
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                var cell = new BattleCellState
                {
                    coord = new Vector2I(x, y),
                    base_terrain = BattleCellState.TERRAIN_LAND(),
                    base_height = 4,
                    height_offset = 0,
                };
                cell.recalculate_runtime_values();
                state.cells[cell.coord] = cell;
            }
        }
        state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells);
        return state;
    }

    private static BattleAiContext BuildAiContext(BattleRuntimeModule runtime, BattleUnitState unitState)
    {
        var aiContext = new BattleAiContext
        {
            state = runtime._state,
            unit_state = unitState,
            grid_service = runtime._grid_service,
            skill_defs = runtime._skill_defs,
            allow_authored_action_fallback_for_tests = true,
        };
        runtime._bind_ai_helper_services_for_decision(unitState, aiContext);
        return aiContext;
    }

    private static BattleUnitState BuildAiUnit(
        StringName unitId,
        string displayName,
        StringName factionId,
        Vector2I coord,
        StringName brainId,
        StringName stateId
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = displayName,
            faction_id = factionId,
            control_mode = "ai",
            ai_brain_id = brainId,
            ai_state_id = stateId,
            current_hp = 26,
            current_mp = 0,
            current_stamina = 8,
            current_ap = 2,
            is_alive = true,
        };
        unit.set_anchor_coord(coord);
        unit.attribute_snapshot.set_value(AttributeService.HP_MAX_ID(), 26);
        unit.attribute_snapshot.set_value(AttributeService.MP_MAX_ID(), 0);
        unit.attribute_snapshot.set_value(AttributeService.STAMINA_MAX_ID(), 8);
        unit.attribute_snapshot.set_value(AttributeService.ACTION_POINTS_ID(), 2);
        unit.attribute_snapshot.set_value(AttributeService.ATTACK_BONUS_ID(), 0);
        unit.attribute_snapshot.set_value(AttributeService.ARMOR_CLASS_ID(), 4);
        return unit;
    }

    private static BattleUnitState BuildManualUnit(
        StringName unitId,
        string displayName,
        StringName factionId,
        Vector2I coord
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = displayName,
            faction_id = factionId,
            control_mode = "manual",
            current_hp = 30,
            current_ap = 2,
            is_alive = true,
        };
        unit.set_anchor_coord(coord);
        unit.attribute_snapshot.set_value(AttributeService.HP_MAX_ID(), 30);
        unit.attribute_snapshot.set_value(AttributeService.ACTION_POINTS_ID(), 2);
        unit.attribute_snapshot.set_value(AttributeService.ATTACK_BONUS_ID(), 6);
        return unit;
    }

    private void AddUnitToState(
        BattleRuntimeModule runtime,
        BattleState state,
        BattleUnitState unit,
        bool isEnemy
    )
    {
        state.units[unit.unit_id] = unit;
        if (isEnemy)
        {
            state.enemy_unit_ids.Add(unit.unit_id);
        }
        else
        {
            state.ally_unit_ids.Add(unit.unit_id);
        }
        bool placed = runtime._grid_service.place_unit(state, unit, unit.coord, true);
        AssertTrue(placed, $"test unit {unit.unit_id} should be placeable.");
    }

    private void AssertNoGlobalClass(Type type, string label)
    {
        AssertTrue(
            type.GetCustomAttribute<GlobalClassAttribute>() == null,
            $"{label} must not be registered as GlobalClass."
        );
    }

    private void AssertContains(string value, string expectedFragment, string message)
    {
        if (string.IsNullOrEmpty(value) || !value.Contains(expectedFragment, StringComparison.Ordinal))
        {
            _failures.Add($"{message} value={value}");
        }
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertFalse(bool condition, string message)
    {
        AssertTrue(!condition, message);
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
        {
            _failures.Add($"{message} actual={actual} expected={expected}");
        }
    }
}
