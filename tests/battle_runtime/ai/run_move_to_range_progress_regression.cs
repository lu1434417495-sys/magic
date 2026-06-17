using System;
using System.Collections.Generic;
using Godot;
using GActionArray = Godot.Collections.Array<EnemyAiAction>;
using GStringArray = Godot.Collections.Array<string>;
using GStateArray = Godot.Collections.Array<EnemyAiStateDef>;

public partial class run_move_to_range_progress_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestCandidateRequestTypedValidation();
        TestCandidateProbeActionBuildsTypedSections();
        TestMoveToRangePrefersProgressOverWaitWhenFarFromBand();
        TestMoveToRangeUsesPathDetourWhenDirectProgressIsBlocked();
        TestScreeningMoveToRangeUsesPathProgressBeforeLocalGreedyMove();

        Quit(_test.Finish("Move-to-range progress regression"));
    }

    private void TestCandidateRequestTypedValidation()
    {
        BattleAiCandidateRequest request = BuildValidRequest();
        _test.True(
            request.TryValidateMoveToRange(out string error),
            $"valid typed move_to_range request should pass validation: {error}"
        );

        request = BuildValidRequest();
        request.ActionIntent = "legacy_intent";
        _test.False(
            request.TryValidateMoveToRange(out error),
            "unknown action intent must fail candidate request validation."
        );
        _test.True(!string.IsNullOrEmpty(error), "invalid intent failure should provide an error.");

        request = BuildValidRequest();
        request.MaxCandidateCount = 5;
        request.SetMoveToRangeSections(
            new MoveToRangePathSearchBudget { MaxCost = 2, MaxDestinations = 3 },
            new MoveToRangeTacticalParams(),
            new MoveToRangeRuntimeMetadata()
        );
        _test.False(
            request.TryValidateMoveToRange(out error),
            "max_candidate_count must not exceed typed path budget max destinations."
        );
        _test.True(!string.IsNullOrEmpty(error), "path budget validation should provide an error.");
    }

    private void TestCandidateProbeActionBuildsTypedSections()
    {
        var action = new BattleAiCandidateProbeTestAction();
        BattleAiCandidateRequest request = action.BuildCandidateRequest(null);
        request.ActorUnitId = "probe_actor";

        _test.True(
            request.TryValidateMoveToRange(out string error),
            $"probe test action should build a valid typed request after actor injection: {error}"
        );
        _test.True(
            request.TryGetMoveToRangeSections(
                out MoveToRangePathSearchBudget budget,
                out MoveToRangeTacticalParams tactical,
                out MoveToRangeRuntimeMetadata runtime,
                out error
            ),
            $"probe request should expose typed section snapshots: {error}"
        );
        _test.Eq(budget.MaxCost, 2, "probe path budget max cost should stay typed.");
        _test.Eq(budget.MaxDestinations, 4, "probe path budget max destinations should stay typed.");
        _test.Eq(tactical.TargetSelector, new StringName("nearest_enemy"), "probe tactical selector should stay typed.");
        _test.Eq(runtime.EffectiveAttackRange, -1, "probe runtime metadata should stay typed.");
    }

    private void TestMoveToRangePrefersProgressOverWaitWhenFarFromBand()
    {
        BattleRuntimeModule runtime = BuildRuntimeWithEnemyContent();
        try
        {
            EnemyAiBrainDef brain = BuildBrain(
                "far_gap_mover_brain",
                "engage",
                new MoveToRangeAction
                {
                    action_id = "far_gap_close_in",
                    action_intent = BattleAiActionIntent.Positioning,
                    score_bucket_id = "positioning",
                    ai_evaluation_mode = MoveToRangeAction.ToStringName(
                        MoveToRangeAction.MoveToRangeAiEvaluationMode.CandidateRequest
                    ),
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
            runtime.SetupStateForTests(state);

            BattleAiDecision decision = runtime._ai_service.ChooseCommand(BuildAiContext(runtime, mover));
            _test.True(decision?.command != null, "far move_to_range should produce a legal command.");
            _test.Eq(
                decision?.command?.command_type ?? new StringName(""),
                BattleTypedNames.ToStringName(BattleCommandKind.Move),
                "far move_to_range should not wait when outside the distance band."
            );
            _test.Eq(
                decision?.command?.target_coord ?? new Vector2I(-1, -1),
                new Vector2I(3, 1),
                "far move_to_range should choose the farthest reachable progress tile."
            );
        }
        finally
        {
            runtime.dispose();
        }
    }

    private void TestMoveToRangeUsesPathDetourWhenDirectProgressIsBlocked()
    {
        BattleRuntimeModule runtime = BuildRuntimeWithEnemyContent();
        try
        {
            EnemyAiBrainDef brain = BuildBrain(
                "detour_mover_brain",
                "engage",
                new MoveToRangeAction
                {
                    action_id = "detour_close_in",
                    action_intent = BattleAiActionIntent.Positioning,
                    score_bucket_id = "positioning",
                    ai_evaluation_mode = MoveToRangeAction.ToStringName(
                        MoveToRangeAction.MoveToRangeAiEvaluationMode.CandidateRequest
                    ),
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
            runtime.SetupStateForTests(state);

            BattleAiDecision decision = runtime._ai_service.ChooseCommand(BuildAiContext(runtime, mover));
            _test.True(decision?.command != null, "detour move_to_range should produce a legal command.");
            _test.Eq(
                decision?.command?.command_type ?? new StringName(""),
                BattleTypedNames.ToStringName(BattleCommandKind.Move),
                "detour move_to_range should not wait when a path detour exists."
            );

            Vector2I actual = decision?.command?.target_coord ?? new Vector2I(-1, -1);
            bool validLanding = actual == new Vector2I(2, 0) || actual == new Vector2I(2, 2);
            _test.True(
                validLanding,
                $"detour move_to_range should choose one of the equivalent detour tiles. actual={actual}"
            );
        }
        finally
        {
            runtime.dispose();
        }
    }

    private void TestScreeningMoveToRangeUsesPathProgressBeforeLocalGreedyMove()
    {
        BattleRuntimeModule runtime = BuildRuntimeWithEnemyContent();
        try
        {
            MoveToRangeAction moveAction = new()
            {
                action_id = "screening_detour_close_in",
                action_intent = BattleAiActionIntent.Positioning,
                score_bucket_id = "positioning",
                target_selector = "nearest_enemy",
                desired_min_distance = 1,
                desired_max_distance = 1,
                screening_mode = MoveToRangeAction.ToStringName(
                    MoveToRangeAction.MoveToRangeScreeningMode.RangedAlly
                ),
                screening_ally_min_attack_range = 1,
                screening_threat_distance_buffer = 99,
            };
            EnemyAiBrainDef brain = BuildBrain(
                "screening_detour_mover_brain",
                "engage",
                moveAction,
                new WaitAction { action_id = "screening_detour_wait" }
            );
            runtime._enemy_ai_brains[brain.brain_id] = brain;
            runtime._ai_service.Setup(
                new Dictionary<StringName, EnemyAiBrainDef> { [brain.brain_id] = brain },
                null
            );

            BattleState state = BuildFlatState(new Vector2I(9, 5));
            for (int y = 1; y < state.map_size.Y; y++)
                SetTerrain(state, new Vector2I(3, y), BattleTerrainKind.DeepWater);

            BattleUnitState mover = BuildAiUnit(
                "screening_enemy",
                "Screening mover",
                "hostile",
                new Vector2I(1, 2),
                brain.brain_id,
                "engage"
            );
            BattleUnitState protectedAlly = BuildAiUnit(
                "screening_archer",
                "Protected archer",
                "hostile",
                new Vector2I(1, 4),
                brain.brain_id,
                "engage"
            );
            protectedAlly.weapon_attack_range = 4;
            BattleUnitState player = BuildManualUnit(
                "screening_player",
                "Screening target",
                "player",
                new Vector2I(7, 2)
            );
            player.weapon_attack_range = 1;
            AddUnitToState(runtime, state, mover, isEnemy: true);
            AddUnitToState(runtime, state, protectedAlly, isEnemy: true);
            AddUnitToState(runtime, state, player, isEnemy: false);
            runtime.SetupStateForTests(state);

            BattleAiDecision decision = moveAction.Decide(BuildAiContext(runtime, mover));
            _test.True(decision?.command != null, "screening move_to_range should produce a legal command.");
            _test.Eq(
                decision?.command?.command_type ?? new StringName(""),
                BattleTypedNames.ToStringName(BattleCommandKind.Move),
                "screening move_to_range should move instead of waiting when a global detour exists."
            );

            Vector2I actual = decision?.command?.target_coord ?? new Vector2I(-1, -1);
            _test.True(
                actual.Y < mover.coord.Y,
                $"screening move_to_range should follow the path toward the doorway before local direct-distance greed. actual={actual}"
            );
            _test.True(
                actual != new Vector2I(2, 2),
                "screening move_to_range should not stop at the local wall-side tile that reduces direct distance but stalls path progress."
            );
        }
        finally
        {
            runtime.dispose();
        }
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
        try
        {
            runtime.setup(
                null,
                gameSession.GetSkillDefsTyped(),
                gameSession.GetEnemyTemplatesTyped(),
                gameSession.GetEnemyAiBrainsTyped(),
                null
            );
        }
        finally
        {
            gameSession.DisposeOwnedRuntimeResources();
            gameSession.Free();
        }
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
                    base_terrain = BattleTerrainRules.ToStringName(BattleTerrainKind.Land),
                    base_height = 4,
                    height_offset = 0,
                };
                cell.RecalculateRuntimeValues();
                state.SetCell(cell.coord, cell);
            }
        }
        state.RebuildCellColumns();
        return state;
    }

    private static void SetTerrain(BattleState state, Vector2I coord, BattleTerrainKind terrainKind)
    {
        if (state == null || !state.ContainsCell(coord))
            return;
        BattleCellState cell = state.GetCell(coord);
        if (cell == null)
            return;
        cell.base_terrain = BattleTerrainRules.ToStringName(terrainKind);
        cell.RecalculateRuntimeValues();
        state.RebuildCellColumns();
    }

    private static BattleAiContext BuildAiContext(BattleRuntimeModule runtime, BattleUnitState unitState)
    {
        var aiContext = new BattleAiContext
        {
            state = runtime._state,
            unit_state = unitState,
            grid_service = runtime._grid_service,
            allow_authored_action_fallback_for_tests = true,
        };
        aiContext.SetSkillDefs(runtime.GetSkillDefIndexTyped());
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
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 26);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.MpMax), 0);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.StaminaMax), 8);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ActionPoints), 2);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), 0);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 4);
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
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 30);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ActionPoints), 2);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), 6);
        return unit;
    }

    private void AddUnitToState(
        BattleRuntimeModule runtime,
        BattleState state,
        BattleUnitState unit,
        bool isEnemy
    )
    {
        state.SetUnit(unit);
        if (isEnemy)
        {
            state.enemy_unit_ids.Add(unit.unit_id);
        }
        else
        {
            state.ally_unit_ids.Add(unit.unit_id);
        }
        bool placed = runtime._grid_service.PlaceUnit(state, unit, unit.coord, true);
        _test.True(placed, $"test unit {unit.unit_id} should be placeable.");
    }

    private void AssertNoGlobalClass(Type type, string label)
    {
    }

}
