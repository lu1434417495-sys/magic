using System;
using System.Collections.Generic;
using Godot;

public partial class run_move_to_range_progress_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestCandidateRequestTypedValidation();
        TestCandidateProbeActionBuildsTypedSections();
        TestMoveToRangePrefersProgressOverWaitWhenFarFromBand();
        TestMoveToRangeUsesPathDetourWhenDirectProgressIsBlocked();
        TestScreeningMoveToRangeUsesPathProgressBeforeLocalGreedyMove();
        TestHighGroundPositionRequiresProgressWhenBeyondBand();

        RequestTestExit(_test.Finish("Move-to-range progress regression"));
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
        BattleAiCandidateRequest request = BuildProbeRequest();

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
        BattleAiDecision decision = null;
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithEnemyContent();
        BattleRuntimeModule runtime = runtimeScope.Runtime;
        try
        {
            MoveToRangeActionDefinition moveAction = BuildMoveToRangeAction(
                "far_gap_close_in",
                desiredMinDistance: 4,
                desiredMaxDistance: 5,
                aiEvaluationMode: "inline_decide",
                screeningMode: "none"
            );
            EnemyAiBrainDefinition brain = BuildBrain(
                "far_gap_mover_brain",
                "engage",
                moveAction,
                BuildWaitAction("far_gap_wait")
            );
            runtime.ReplaceEnemyAiBrainsTyped(
                new Dictionary<StringName, EnemyAiBrainDefinition>
                {
                    [brain.BrainId] = brain,
                }
            );

            BattleState state = BuildFlatState(new Vector2I(31, 3));
            BattleUnitState mover = BuildAiUnit(
                "far_gap_enemy",
                "Far gap mover",
                "hostile",
                new Vector2I(1, 1),
                brain.BrainId,
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

            decision = runtime._ai_service
                .ChooseCommand(BuildAiContext(runtime, mover), captureTrace: false)
                ?.Decision;
            _test.Eq(
                moveAction.AiEvaluationMode,
                new StringName("inline_decide"),
                "plain move definition should preserve the authored default inline mode; runtime metadata owns the forced dispatch."
            );
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
            _test.True(
                decision?.reason_text?.Contains("战术位置", StringComparison.Ordinal) == true,
                "authored default-mode no-screening move should dispatch through candidate request metadata."
            );
        }
        finally
        {
            DisposeDecision(decision);
        }
    }

    private void TestMoveToRangeUsesPathDetourWhenDirectProgressIsBlocked()
    {
        BattleAiDecision decision = null;
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithEnemyContent();
        BattleRuntimeModule runtime = runtimeScope.Runtime;
        try
        {
            EnemyAiBrainDefinition brain = BuildBrain(
                "detour_mover_brain",
                "engage",
                BuildMoveToRangeAction(
                    "detour_close_in",
                    desiredMinDistance: 1,
                    desiredMaxDistance: 1,
                    aiEvaluationMode: "candidate_request",
                    screeningMode: "none"
                ),
                BuildWaitAction("detour_wait")
            );
            runtime.ReplaceEnemyAiBrainsTyped(
                new Dictionary<StringName, EnemyAiBrainDefinition>
                {
                    [brain.BrainId] = brain,
                }
            );

            BattleState state = BuildFlatState(new Vector2I(7, 3));
            BattleUnitState mover = BuildAiUnit(
                "detour_enemy",
                "Detour mover",
                "hostile",
                new Vector2I(1, 1),
                brain.BrainId,
                "engage"
            );
            BattleUnitState blocker = BuildAiUnit(
                "detour_blocker",
                "Blocker",
                "hostile",
                new Vector2I(2, 1),
                brain.BrainId,
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

            decision = runtime._ai_service
                .ChooseCommand(BuildAiContext(runtime, mover), captureTrace: false)
                ?.Decision;
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
            DisposeDecision(decision);
        }
    }

    private void TestScreeningMoveToRangeUsesPathProgressBeforeLocalGreedyMove()
    {
        BattleAiDecision decision = null;
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithEnemyContent();
        BattleRuntimeModule runtime = runtimeScope.Runtime;
        try
        {
            MoveToRangeActionDefinition moveAction = BuildMoveToRangeAction(
                "screening_detour_close_in",
                desiredMinDistance: 1,
                desiredMaxDistance: 1,
                aiEvaluationMode: "inline_decide",
                screeningMode: "ranged_ally",
                screeningAllyMinAttackRange: 1,
                screeningThreatDistanceBuffer: 99
            );
            EnemyAiBrainDefinition brain = BuildBrain(
                "screening_detour_mover_brain",
                "engage",
                moveAction,
                BuildWaitAction("screening_detour_wait")
            );
            runtime.ReplaceEnemyAiBrainsTyped(
                new Dictionary<StringName, EnemyAiBrainDefinition>
                {
                    [brain.BrainId] = brain,
                }
            );

            BattleState state = BuildFlatState(new Vector2I(9, 5));
            for (int y = 1; y < state.map_size.Y; y++)
                SetTerrain(state, new Vector2I(3, y), BattleTerrainKind.DeepWater);

            BattleUnitState mover = BuildAiUnit(
                "screening_enemy",
                "Screening mover",
                "hostile",
                new Vector2I(1, 2),
                brain.BrainId,
                "engage"
            );
            BattleUnitState protectedAlly = BuildAiUnit(
                "screening_archer",
                "Protected archer",
                "hostile",
                new Vector2I(1, 4),
                brain.BrainId,
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

            decision = new BattleAiMoveToRangeActionEvaluator().Evaluate(
                moveAction,
                BuildAiContext(runtime, mover)
            );
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
            DisposeDecision(decision);
        }
    }

    private void TestHighGroundPositionRequiresProgressWhenBeyondBand()
    {
        BattleAiDecision stalledDecision = null;
        BattleAiDecision progressDecision = null;
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithEnemyContent();
        BattleRuntimeModule runtime = runtimeScope.Runtime;
        try
        {
            var action = new MoveToAdvantagePositionActionDefinition(
                actionId: "high_ground_progress_gate",
                scoreBucketId: "archer_positioning",
                actionIntent: BattleAiActionIntent.Positioning,
                targetSelector: "nearest_enemy",
                desiredMinDistance: 3,
                desiredMaxDistance: 5,
                rangeSkillIds: Array.Empty<StringName>(),
                minimumSafeDistance: 3,
                safeDistanceMargin: 1,
                minSurvivalMarginGainToEscape: 1,
                minDistanceProgressWhenBeyondBand: 1,
                positioningMode: "high_ground",
                highGroundWeight: 60,
                safetyWeight: 50,
                distanceBandWeight: 20,
                candidateLimit: 96
            );

            stalledDecision = DecideHighGroundProgressCase(
                runtime,
                action,
                new Vector2I(1, 0)
            );
            _test.True(
                stalledDecision == null,
                "射程外 high_ground 不应选择没有缩短目标距离的高地。"
            );

            progressDecision = DecideHighGroundProgressCase(
                runtime,
                action,
                new Vector2I(2, 1)
            );
            _test.Eq(
                progressDecision?.command?.target_coord ?? new Vector2I(-1, -1),
                new Vector2I(2, 1),
                "射程外 high_ground 仍应允许能缩短目标距离的高地。"
            );
        }
        finally
        {
            DisposeDecision(stalledDecision);
            DisposeDecision(progressDecision);
        }
    }

    private static BattleAiDecision DecideHighGroundProgressCase(
        BattleRuntimeModule runtime,
        MoveToAdvantagePositionActionDefinition action,
        Vector2I highGroundCoord
    )
    {
        BattleState state = null;
        try
        {
            state = BuildFlatState(new Vector2I(8, 3));
            SetHeightOffset(state, highGroundCoord, 1);

            BattleUnitState mover = BuildAiUnit(
                "high_ground_mover",
                "High Ground Mover",
                "hostile",
                new Vector2I(1, 1),
                "",
                "engage"
            );
            mover.current_move_points = 2;
            BattleUnitState player = BuildManualUnit(
                "high_ground_target",
                "High Ground Target",
                "player",
                new Vector2I(7, 1)
            );
            AddUnitToStateStatic(runtime, state, mover, isEnemy: true);
            AddUnitToStateStatic(runtime, state, player, isEnemy: false);
            runtime.SetupStateForTests(state);

            return new BattleAiMoveToAdvantageActionEvaluator().Evaluate(
                action,
                BuildAiContext(runtime, mover)
            );
        }
        finally
        {
            runtime?.SetupStateForTests(null);
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

    private static BattleAiCandidateRequest BuildProbeRequest()
    {
        var request = new BattleAiCandidateRequest
        {
            FamilyId = BattleAiCandidateRequest.FamilyMoveToRange,
            ActionId = "candidate_probe",
            ActionLabel = "candidate probe",
            ActionIntent = BattleAiActionIntent.Positioning,
            ScoreBucketId = "positioning",
            ActorUnitId = "probe_actor",
            FocusTargetUnitId = "hero",
            DesiredMinDistance = 1,
            DesiredMaxDistance = 2,
            MaxCandidateCount = 4,
        };
        request.SetMoveToRangeSections(
            new MoveToRangePathSearchBudget
            {
                MaxCost = 2,
                MaxDestinations = 4,
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

    private static MoveToRangeActionDefinition BuildMoveToRangeAction(
        StringName actionId,
        int desiredMinDistance,
        int desiredMaxDistance,
        StringName aiEvaluationMode,
        StringName screeningMode,
        int screeningAllyMinAttackRange = 4,
        int screeningThreatDistanceBuffer = 2
    )
    {
        return new MoveToRangeActionDefinition(
            actionId: actionId,
            scoreBucketId: "positioning",
            actionIntent: BattleAiActionIntent.Positioning,
            aiEvaluationMode: aiEvaluationMode,
            targetSelector: "nearest_enemy",
            desiredMinDistance: desiredMinDistance,
            desiredMaxDistance: desiredMaxDistance,
            rangeSkillIds: Array.Empty<StringName>(),
            screeningMode: screeningMode,
            enableAoeSetupPositioning: true,
            aoeSetupMinTargetCount: 2,
            aoeSetupTargetCountWeight: 140,
            aoeSetupImprovementWeight: 220,
            aoeSetupFriendlyFirePenalty: 1000,
            screeningMinHpBasisPoints: 4000,
            screeningAllyMinAttackRange: screeningAllyMinAttackRange,
            screeningEnemyMaxContactRange: 2,
            screeningThreatDistanceBuffer: screeningThreatDistanceBuffer,
            screeningPathBonus: 45
        );
    }

    private static WaitActionDefinition BuildWaitAction(StringName actionId) =>
        new(
            actionId: actionId,
            scoreBucketId: "",
            actionIntent: BattleAiActionIntent.Positioning,
            activeRestActionBaseScore: 10,
            activeRestMinStaminaResidue: 1
        );

    private static EnemyAiBrainDefinition BuildBrain(
        StringName brainId,
        StringName stateId,
        MoveToRangeActionDefinition moveAction,
        WaitActionDefinition waitAction
    )
    {
        var state = new EnemyAiStateDefinition(
            stateId,
            new EnemyAiActionDefinition[] { moveAction, waitAction },
            Array.Empty<EnemyAiGenerationSlotDefinition>()
        );
        return new EnemyAiBrainDefinition(
            brainId,
            stateId,
            BattleAiScoreProfileDefinition.Default,
            new[] { state },
            Array.Empty<EnemyAiTransitionRuleDefinition>()
        );
    }

    private static BattleRuntimeScope BuildRuntimeWithEnemyContent()
    {
        var gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        var runtime = new BattleRuntimeModule();
        runtime.setup(
            null,
            gameSession.GetSkillDefinitionsTyped(),
            gameSession.GetEnemyTemplateDefinitions(),
            gameSession.GetEnemyAiBrainDefinitions(),
            null
        );
        return new BattleRuntimeScope(runtime, gameSession);
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

    private static void SetHeightOffset(BattleState state, Vector2I coord, int heightOffset)
    {
        if (state == null || !state.ContainsCell(coord))
            return;
        BattleCellState cell = state.GetCell(coord);
        if (cell == null)
            return;
        cell.height_offset = heightOffset;
        cell.RecalculateRuntimeValues();
        state.RebuildCellColumns();
    }

    private static BattleAiContext BuildAiContext(BattleRuntimeModule runtime, BattleUnitState unitState)
    {
        runtime._ensure_ai_action_plan_for_unit(unitState);
        runtime._ai_action_plans_by_unit_id.TryGetValue(
            unitState.unit_id,
            out BattleAiRuntimeActionPlan actionPlan
        );
        var aiContext = new BattleAiContext
        {
            state = runtime._state,
            unit_state = unitState,
            grid_service = runtime._grid_service,
            runtime_action_plan = actionPlan,
        };
        aiContext.SetSkillDefinitions(runtime.GetSkillDefinitionIndexTyped());
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

    private static void AddUnitToStateStatic(
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
        if (!runtime._grid_service.PlaceUnit(state, unit, unit.coord, true))
        {
            throw new InvalidOperationException($"test unit {unit.unit_id} should be placeable.");
        }
    }

    private void AssertNoGlobalClass(Type type, string label)
    {
    }

    private static void DisposeDecision(BattleAiDecision decision)
    {
        decision?.ClearOwnedRuntimeReferences();
    }

    private sealed class BattleRuntimeScope : IDisposable
    {
        private readonly GameSession _gameSession;

        internal BattleRuntimeScope(BattleRuntimeModule runtime, GameSession gameSession)
        {
            Runtime = runtime;
            _gameSession = gameSession;
        }

        internal BattleRuntimeModule Runtime { get; }

        public void Dispose()
        {
            Runtime?.SetupStateForTests(null);
            Runtime?.Dispose();
            _gameSession?.Dispose();
        }
    }

}
