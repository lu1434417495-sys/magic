using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class run_battle_round_trip_e2e : run_enter_battle_e2e
{
    private const int ManualActionLimit = 160;
    private const int ManualTurnMaxFrames = 24000;
    private const ulong ManualTurnTimeoutMsec = 20000;
    private const ulong JourneyTimeoutMsec = 90000;
    private const int UiStateMaxFrames = 1200;
    private const ulong UiStateTimeoutMsec = 10000;
    private const string SkillActionId = "skill";
    private const string MoveActionId = "move";
    private const string WaitActionId = "wait";
    private static readonly Vector2I[] CardinalDirections =
    {
        Vector2I.Right,
        Vector2I.Down,
        Vector2I.Left,
        Vector2I.Up,
    };

    private readonly HashSet<Vector2I> _visitedMoveCoords = new();

    private protected override string ScenarioLabel =>
        "E2E battle action, resolution, and world return";

    private protected override async Task RunScenarioAsync()
    {
        await base.RunScenarioAsync();

        WorldMapSystem worldMap = CurrentScene as WorldMapSystem;
        Test.True(worldMap != null, "Battle journey should remain in the real world-map scene.");
        if (worldMap == null)
            return;

        GameRuntimeFacade runtime = worldMap._runtime;
        GameSession gameSession = worldMap._game_session;
        BattleMapPanel battlePanel = worldMap.battle_map_panel;
        Test.True(runtime != null, "Battle journey should retain the scene-owned runtime.");
        Test.True(gameSession != null, "Battle journey should retain the canonical GameSession.");
        Test.True(battlePanel != null, "Battle journey should retain the production battle panel.");
        if (runtime == null || gameSession == null || battlePanel == null)
            return;

        StringName encounterId = runtime.GetActiveBattleEncounterId();
        Vector2I encounterCoord = runtime.GetPlayerCoord();
        Test.True(encounterId != "", "The entered world encounter should expose a stable ID.");
        if (encounterId == "")
            return;

        WorldRuntimeData enteredWorldData = runtime.GetActiveWorldRuntimeData();
        EncounterAnchorData enteredEncounter = enteredWorldData?.EncounterAnchors.FirstOrDefault(
            anchor => anchor != null && anchor.entity_id == encounterId
        );
        BattleEncounterDefinition encounterDefinition = null;
        bool foundEncounterDefinition =
            enteredEncounter != null
            && gameSession
                .GetBattleEncounterDefinitions()
                .TryGetValue(
                    enteredEncounter.encounter_profile_id,
                    out encounterDefinition
                );
        Test.True(
            foundEncounterDefinition,
            "The entered encounter should resolve its typed battle/world-resolution definition."
        );
        if (!foundEncounterDefinition)
            return;

        BattleWorldResolutionMode expectedSuccessResolution =
            encounterDefinition.WorldResolution.PlayerSuccessMode;
        int enteredSuppressedUntilStep = enteredEncounter.suppressed_until_step;
        int enteredWorldStep = enteredWorldData.WorldStep;
        BattleState enteredBattleState = runtime.GetBattleState();
        Test.True(enteredBattleState != null, "The interactive encounter should retain its battle state.");
        if (enteredBattleState == null)
            return;

        ulong journeyStartedAtMsec = Time.GetTicksMsec();
        int manualActionCount = 0;
        int skillActionCount = 0;
        int moveActionCount = 0;
        int waitActionCount = 0;
        int promotionConfirmCount = 0;
        string lastBattleSnapshot = DescribeBattle(runtime);

        while (runtime.IsBattleActive())
        {
            if (manualActionCount >= ManualActionLimit)
            {
                throw new InvalidOperationException(
                    $"Battle E2E exceeded {ManualActionLimit} manual commands. Last state: {lastBattleSnapshot}"
                );
            }
            if (Time.GetTicksMsec() - journeyStartedAtMsec >= JourneyTimeoutMsec)
            {
                throw new TimeoutException(
                    $"Battle E2E exceeded {JourneyTimeoutMsec} ms. Last state: {lastBattleSnapshot}"
                );
            }

            string activeModalId = runtime.GetActiveModalId();
            if (activeModalId == "promotion")
            {
                await ConfirmPromotionThroughUiAsync(worldMap, runtime);
                promotionConfirmCount++;
                continue;
            }
            if (!string.IsNullOrEmpty(activeModalId))
            {
                throw new InvalidOperationException(
                    $"Battle E2E encountered unsupported modal '{activeModalId}'. Last state: {lastBattleSnapshot}"
                );
            }

            await Wait.UntilAsync(
                () =>
                    !runtime.IsBattleActive()
                    || runtime.GetActiveModalId() == "promotion"
                    || HasTerminalDecision(runtime.GetBattleState())
                    || IsManualCommandSurfaceReady(runtime, battlePanel),
                ManualTurnMaxFrames,
                ManualTurnTimeoutMsec,
                "the battle to resolve, request promotion, or expose a manual player command"
            );
            if (!runtime.IsBattleActive())
                break;
            if (runtime.GetActiveModalId() == "promotion")
                continue;
            if (HasTerminalDecision(runtime.GetBattleState()))
            {
                throw new InvalidOperationException(
                    $"Battle reached a terminal decision but formal finalization retained the active runtime. {DescribeBattle(runtime)}"
                );
            }

            BattleUnitState actor = ResolveManualPlayerActor(runtime);
            if (actor == null)
                continue;

            lastBattleSnapshot = DescribeBattle(runtime);
            if (await TryIssueSkillThroughUiAsync(runtime, battlePanel, actor))
            {
                manualActionCount++;
                skillActionCount++;
                continue;
            }
            if (await TryIssueMoveThroughUiAsync(runtime, battlePanel, actor))
            {
                manualActionCount++;
                moveActionCount++;
                continue;
            }

            await ClearSelectedSkillThroughUiAsync(runtime);
            int waitCountBefore = GetActionCount(runtime, actor.unit_id, WaitActionId);
            await Input.ClickAsync(battlePanel.resolve_button);
            bool waitIssued =
                !runtime.IsBattleActive()
                || GetActionCount(runtime, actor.unit_id, WaitActionId) > waitCountBefore;
            Test.True(waitIssued, "The real resolve button should issue the player's wait command.");
            if (!waitIssued)
                return;
            manualActionCount++;
            waitActionCount++;
        }

        if (runtime.GetActiveModalId() == "game_over")
        {
            throw new InvalidOperationException(
                $"Battle E2E ended in game_over. actions=skill:{skillActionCount},move:{moveActionCount},wait:{waitActionCount}; metrics={DescribePlayerMetrics(runtime)}; last={lastBattleSnapshot}; logs={DescribeRecentBattleLogs(enteredBattleState)}"
            );
        }
        await CompleteKnownPostBattleModalsAsync(worldMap, runtime);

        BattleFinalDecision finalDecision = enteredBattleState.FinalDecision;
        WorldRuntimeData resolvedWorldData = runtime.GetActiveWorldRuntimeData();
        EncounterAnchorData resolvedEncounter = resolvedWorldData?.EncounterAnchors.FirstOrDefault(
            anchor => anchor != null && anchor.entity_id == encounterId
        );
        bool worldResolutionApplied = expectedSuccessResolution switch
        {
            BattleWorldResolutionMode.Clear => resolvedEncounter == null,
            BattleWorldResolutionMode.Suppress =>
                resolvedEncounter != null
                && resolvedEncounter.suppressed_until_step
                    >= Math.Max(
                        enteredSuppressedUntilStep,
                        enteredWorldStep
                            + encounterDefinition.WorldResolution.SuppressionSteps
                    ),
            BattleWorldResolutionMode.Preserve => resolvedEncounter != null,
            _ => false,
        };

        Test.True(
            skillActionCount > 0,
            "The completed journey must issue at least one real player skill command."
        );
        Test.True(
            manualActionCount > 0,
            "The completed journey must drive at least one manual battle command."
        );
        Test.True(
            finalDecision != null,
            "The completed journey should retain the objective owner's terminal decision."
        );
        Test.Eq(
            finalDecision?.Outcome ?? BattleOutcomeKind.Unknown,
            BattleOutcomeKind.PlayerSuccess,
            $"The deterministic UI journey should end in player success. actions=skill:{skillActionCount},move:{moveActionCount},wait:{waitActionCount},promotion:{promotionConfirmCount}; last={lastBattleSnapshot}"
        );
        Test.True(
            worldResolutionApplied,
            $"Player success should apply the encounter's declared {expectedSuccessResolution} world resolution. encounter={encounterId}, profile={enteredEncounter.encounter_profile_id}"
        );
        if (
            finalDecision?.Outcome != BattleOutcomeKind.PlayerSuccess
            || !worldResolutionApplied
        )
            return;

        await Wait.UntilAsync(
            () =>
                !runtime.IsBattleActive()
                && !gameSession.IsBattleSaveLocked()
                && string.IsNullOrEmpty(runtime.GetActiveModalId())
                && worldMap.world_map_view.IsVisibleInTree()
                && worldMap.bottom_action_bar.IsVisibleInTree()
                && !worldMap.battle_map_panel.IsVisibleInTree(),
            UiStateMaxFrames,
            UiStateTimeoutMsec,
            "successful battle finalization to restore the world-map interaction surface"
        );

        BattleState finalBattleState = runtime.GetBattleState();
        Test.False(runtime.IsBattleActive(), "The resolved battle should no longer be active.");
        Test.True(
            finalBattleState == null || finalBattleState.IsEmpty(),
            "Resolved battle state should be cleared from the world runtime."
        );
        Test.Eq(
            runtime.GetActiveBattleEncounterId(),
            new StringName(""),
            "Resolved battle context should clear the active encounter ID."
        );
        Test.False(
            gameSession.IsBattleSaveLocked(),
            "Successful finalization should release the canonical battle save lock."
        );
        Test.False(
            gameSession.HasPendingSave(),
            "Successful finalization should flush the battle and world writeback."
        );
        Test.Eq(
            runtime.GetPlayerCoord(),
            encounterCoord,
            "Returning to the world should retain the encounter cell reached through real input."
        );
        Test.Eq(
            gameSession.GetPlayerCoord(),
            encounterCoord,
            "The canonical session should agree with the returned world coordinate."
        );
        Test.True(
            FileAccess.FileExists(gameSession.GetActiveSavePath()),
            "The isolated save should exist after formal battle finalization."
        );
        Test.True(worldMap.world_map_view.IsVisibleInTree(), "The world map should be visible again.");
        Test.True(
            worldMap.bottom_action_bar.IsVisibleInTree(),
            "World actions should be usable again after battle."
        );
        Test.False(
            worldMap.battle_map_panel.IsVisibleInTree(),
            "The battle panel should hide after the runtime returns to the world."
        );
    }

    private async Task<bool> TryIssueSkillThroughUiAsync(
        GameRuntimeFacade runtime,
        BattleMapPanel battlePanel,
        BattleUnitState actor
    )
    {
        await ClearSelectedSkillThroughUiAsync(runtime);
        int enabledSkillCount = CollectEnabledSkillButtons(battlePanel.skill_grid).Count;
        for (int index = 0; index < enabledSkillCount; index++)
        {
            if (!runtime.IsBattleActive() || ResolveManualPlayerActor(runtime) == null)
                return false;

            await ClearSelectedSkillThroughUiAsync(runtime);
            List<BattleSkillSlotButton> currentButtons = CollectEnabledSkillButtons(
                battlePanel.skill_grid
            );
            if (index >= currentButtons.Count)
                break;

            int skillCountBefore = GetActionCount(runtime, actor.unit_id, SkillActionId);
            await Input.ClickAsync(currentButtons[index]);
            if (!runtime.IsBattleActive())
                return true;
            if (GetActionCount(runtime, actor.unit_id, SkillActionId) > skillCountBefore)
                return true;
            if (runtime.GetSelectedBattleSkillId() == "")
                continue;

            Vector2I? targetCoord = FindVisibleSelectedEnemyTarget(runtime, battlePanel, actor);
            if (!targetCoord.HasValue)
                continue;

            await ClickBattleCoordAsync(battlePanel, targetCoord.Value);
            if (!runtime.IsBattleActive())
                return true;
            if (GetActionCount(runtime, actor.unit_id, SkillActionId) > skillCountBefore)
                return true;
        }

        await ClearSelectedSkillThroughUiAsync(runtime);
        Vector2I? directTarget = FindVisibleLivingEnemyCoord(runtime, battlePanel, actor);
        if (directTarget.HasValue)
        {
            int directSkillCountBefore = GetActionCount(
                runtime,
                actor.unit_id,
                SkillActionId
            );
            await ClickBattleCoordAsync(battlePanel, directTarget.Value);
            bool directSkillIssued =
                !runtime.IsBattleActive()
                || GetActionCount(runtime, actor.unit_id, SkillActionId)
                    > directSkillCountBefore;
            if (directSkillIssued)
                return true;
        }

        await ClearSelectedSkillThroughUiAsync(runtime);
        Vector2I? adjacentEnemyCoord = CardinalDirections
            .Select(direction => actor.coord + direction)
            .Where(coord =>
                GetLivingEnemies(runtime, runtime.GetBattleState())
                    .Any(enemy => enemy.OccupiesCoord(coord))
            )
            .Select(coord => (Vector2I?)coord)
            .FirstOrDefault();
        if (!adjacentEnemyCoord.HasValue)
            return false;

        int directionalSkillCountBefore = GetActionCount(
            runtime,
            actor.unit_id,
            SkillActionId
        );
        await Input.TapKeyAsync(KeyForDirection(adjacentEnemyCoord.Value - actor.coord));
        return
            !runtime.IsBattleActive()
            || GetActionCount(runtime, actor.unit_id, SkillActionId)
                > directionalSkillCountBefore;
    }

    private async Task<bool> TryIssueMoveThroughUiAsync(
        GameRuntimeFacade runtime,
        BattleMapPanel battlePanel,
        BattleUnitState actor
    )
    {
        await ClearSelectedSkillThroughUiAsync(runtime);
        BattleState state = runtime.GetBattleState();
        if (state == null)
            return false;

        List<BattleUnitState> enemies = GetLivingEnemies(runtime, state);
        if (enemies.Count == 0)
            return false;

        _visitedMoveCoords.Add(actor.coord);
        int currentDistance = enemies.Min(enemy => DistanceBetweenUnitAndUnit(actor, enemy));
        IReadOnlyList<Vector2I> reachableCoords = runtime.GetBattleMovementReachableCoords();
        Vector2I? cardinalMoveCoord = CardinalDirections
            .Select(direction => actor.coord + direction)
            .Where(coord => reachableCoords.Contains(coord))
            .Select(coord => new
            {
                Coord = coord,
                EnemyDistance = enemies.Min(enemy => DistanceBetweenCoordAndUnit(coord, enemy)),
                Visited = _visitedMoveCoords.Contains(coord),
            })
            .Where(candidate => !candidate.Visited || candidate.EnemyDistance < currentDistance)
            .OrderBy(candidate => candidate.EnemyDistance)
            .ThenBy(candidate => candidate.Visited)
            .ThenBy(candidate => candidate.Coord.X)
            .ThenBy(candidate => candidate.Coord.Y)
            .Select(candidate => (Vector2I?)candidate.Coord)
            .FirstOrDefault();
        if (cardinalMoveCoord.HasValue)
        {
            int cardinalMoveCountBefore = GetActionCount(
                runtime,
                actor.unit_id,
                MoveActionId
            );
            await Input.TapKeyAsync(KeyForDirection(cardinalMoveCoord.Value - actor.coord));
            bool cardinalMoveIssued =
                !runtime.IsBattleActive()
                || GetActionCount(runtime, actor.unit_id, MoveActionId)
                    > cardinalMoveCountBefore;
            if (cardinalMoveIssued)
                _visitedMoveCoords.Add(cardinalMoveCoord.Value);
            return cardinalMoveIssued;
        }

        Vector2I? moveCoord = reachableCoords
            .Where(coord => coord != actor.coord && IsBattleCoordClickable(battlePanel, coord))
            .Select(coord => new
            {
                Coord = coord,
                EnemyDistance = enemies.Min(enemy => DistanceBetweenCoordAndUnit(coord, enemy)),
                TravelDistance = ManhattanDistance(actor.coord, coord),
                Visited = _visitedMoveCoords.Contains(coord),
            })
            .Where(candidate => !candidate.Visited || candidate.EnemyDistance < currentDistance)
            .OrderBy(candidate => candidate.EnemyDistance)
            .ThenBy(candidate => candidate.Visited)
            .ThenByDescending(candidate => candidate.TravelDistance)
            .ThenBy(candidate => candidate.Coord.X)
            .ThenBy(candidate => candidate.Coord.Y)
            .Select(candidate => (Vector2I?)candidate.Coord)
            .FirstOrDefault();
        if (!moveCoord.HasValue)
            return false;

        int moveCountBefore = GetActionCount(runtime, actor.unit_id, MoveActionId);
        await ClickBattleCoordAsync(battlePanel, moveCoord.Value);
        bool issued =
            !runtime.IsBattleActive()
            || GetActionCount(runtime, actor.unit_id, MoveActionId) > moveCountBefore;
        if (issued)
            _visitedMoveCoords.Add(moveCoord.Value);
        return issued;
    }

    private async Task ClearSelectedSkillThroughUiAsync(GameRuntimeFacade runtime)
    {
        if (!runtime.IsBattleActive() || runtime.GetSelectedBattleSkillId() == "")
            return;

        await Input.TapKeyAsync(Key.Escape);
        await Wait.UntilAsync(
            () => !runtime.IsBattleActive() || runtime.GetSelectedBattleSkillId() == "",
            UiStateMaxFrames,
            UiStateTimeoutMsec,
            "the real Escape input to clear the selected battle skill"
        );
    }

    private async Task ClickBattleCoordAsync(BattleMapPanel battlePanel, Vector2I coord)
    {
        if (!IsBattleCoordClickable(battlePanel, coord))
            throw new InvalidOperationException($"Battle coordinate {coord} is outside the live viewport.");
        Vector2 localPosition = battlePanel._battle_board.CoordToViewportPosition(coord);
        await Input.ClickAtAsync(battlePanel.map_viewport_container, localPosition);
    }

    private async Task ConfirmPromotionThroughUiAsync(
        WorldMapSystem worldMap,
        GameRuntimeFacade runtime
    )
    {
        PromotionChoiceWindow window = worldMap.promotion_choice_window;
        Button confirmButton = window?.GetNodeOrNull<Button>("%ConfirmButton");
        await Wait.UntilAsync(
            () => IsClickable(window, confirmButton),
            UiStateMaxFrames,
            UiStateTimeoutMsec,
            "the default promotion choice to become confirmable"
        );
        await Input.ClickAsync(confirmButton);
        await Wait.NextFrameAsync();
    }

    private async Task CompleteKnownPostBattleModalsAsync(
        WorldMapSystem worldMap,
        GameRuntimeFacade runtime
    )
    {
        for (int handledCount = 0; handledCount < 12; handledCount++)
        {
            string modalId = runtime.GetActiveModalId();
            if (string.IsNullOrEmpty(modalId))
                return;
            if (modalId == "promotion")
            {
                await ConfirmPromotionThroughUiAsync(worldMap, runtime);
                continue;
            }
            if (modalId == "reward")
            {
                MasteryRewardWindow window = worldMap.character_reward_window;
                Button confirmButton = window?.GetNodeOrNull<Button>("%ConfirmButton");
                int pendingRewardCountBefore = runtime.GetPendingRewardCount();
                StringName rewardIdBefore = runtime.GetActiveReward()?.reward_id ?? new StringName("");
                await Wait.UntilAsync(
                    () => IsClickable(window, confirmButton),
                    UiStateMaxFrames,
                    UiStateTimeoutMsec,
                    "the post-battle reward confirmation to become clickable"
                );
                await Input.ClickAsync(confirmButton);
                await Wait.UntilAsync(
                    () =>
                        runtime.GetActiveModalId() != "reward"
                        || runtime.GetPendingRewardCount() < pendingRewardCountBefore
                        || runtime.GetActiveReward()?.reward_id != rewardIdBefore,
                    UiStateMaxFrames,
                    UiStateTimeoutMsec,
                    "the real reward confirmation to advance the reward queue"
                );
                continue;
            }

            throw new InvalidOperationException(
                $"Battle E2E reached unsupported post-battle modal '{modalId}'."
            );
        }

        throw new InvalidOperationException("Battle E2E exceeded the post-battle modal limit.");
    }

    private static bool IsManualCommandSurfaceReady(
        GameRuntimeFacade runtime,
        BattleMapPanel battlePanel
    )
    {
        return ResolveManualPlayerActor(runtime) != null
            && battlePanel != null
            && battlePanel.IsVisibleInTree()
            && battlePanel.IsBattleRenderContentReady()
            && IsClickable(battlePanel, battlePanel.resolve_button)
            && battlePanel.map_viewport_container != null
            && battlePanel._battle_board != null;
    }

    private static BattleUnitState ResolveManualPlayerActor(GameRuntimeFacade runtime)
    {
        BattleState state = runtime?.GetBattleState();
        if (
            state == null
            || state.IsEmpty()
            || state.PhaseKind != BattlePhaseKind.UnitActing
            || state.timeline == null
            || state.timeline.frozen
            || !string.IsNullOrEmpty(runtime.GetActiveModalId())
        )
        {
            return null;
        }

        BattleUnitState actor = state.GetUnit(state.active_unit_id);
        if (
            actor == null
            || !actor.is_alive
            || actor.ControlModeKind != BattleUnitControlMode.Manual
            || !string.Equals(
                actor.faction_id.ToString(),
                runtime.GetPlayerFactionId(),
                StringComparison.Ordinal
            )
        )
        {
            return null;
        }
        return actor;
    }

    private static List<BattleUnitState> GetLivingEnemies(
        GameRuntimeFacade runtime,
        BattleState state
    )
    {
        if (runtime == null || state == null)
            return new List<BattleUnitState>();
        string playerFactionId = runtime.GetPlayerFactionId();
        return state
            .Units()
            .Where(unit =>
                unit != null
                && unit.is_alive
                && !string.Equals(
                    unit.faction_id.ToString(),
                    playerFactionId,
                    StringComparison.Ordinal
                )
            )
            .OrderBy(unit => unit.unit_id.ToString(), StringComparer.Ordinal)
            .ToList();
    }

    private static Vector2I? FindVisibleSelectedEnemyTarget(
        GameRuntimeFacade runtime,
        BattleMapPanel battlePanel,
        BattleUnitState actor
    )
    {
        BattleState state = runtime.GetBattleState();
        if (state == null)
            return null;
        List<BattleUnitState> enemies = GetLivingEnemies(runtime, state);
        return runtime
            .GetBattleOverlayTargetCoords()
            .Where(coord =>
                enemies.Any(enemy => enemy.OccupiesCoord(coord))
                && IsBattleCoordClickable(battlePanel, coord)
            )
            .OrderBy(coord => ManhattanDistance(actor.coord, coord))
            .ThenBy(coord => coord.X)
            .ThenBy(coord => coord.Y)
            .Select(coord => (Vector2I?)coord)
            .FirstOrDefault();
    }

    private static Vector2I? FindVisibleLivingEnemyCoord(
        GameRuntimeFacade runtime,
        BattleMapPanel battlePanel,
        BattleUnitState actor
    )
    {
        BattleState state = runtime.GetBattleState();
        if (state == null)
            return null;
        return GetLivingEnemies(runtime, state)
            .SelectMany(enemy => enemy.GetOccupiedCoordsTyped())
            .Where(coord => IsBattleCoordClickable(battlePanel, coord))
            .OrderBy(coord => ManhattanDistance(actor.coord, coord))
            .ThenBy(coord => coord.X)
            .ThenBy(coord => coord.Y)
            .Select(coord => (Vector2I?)coord)
            .FirstOrDefault();
    }

    private static bool IsBattleCoordClickable(BattleMapPanel battlePanel, Vector2I coord)
    {
        if (
            battlePanel?.map_viewport_container == null
            || battlePanel._battle_board == null
            || !GodotObject.IsInstanceValid(battlePanel.map_viewport_container)
            || !GodotObject.IsInstanceValid(battlePanel._battle_board)
            || !battlePanel.map_viewport_container.IsVisibleInTree()
            || !battlePanel._battle_board.IsCoordInViewport(coord)
        )
        {
            return false;
        }

        Vector2 position = battlePanel._battle_board.CoordToViewportPosition(coord);
        Vector2 size = battlePanel.map_viewport_container.Size;
        return float.IsFinite(position.X)
            && float.IsFinite(position.Y)
            && position.X >= 0.0f
            && position.Y >= 0.0f
            && position.X <= size.X
            && position.Y <= size.Y;
    }

    private static List<BattleSkillSlotButton> CollectEnabledSkillButtons(Node root)
    {
        var result = new List<BattleSkillSlotButton>();
        CollectEnabledSkillButtons(root, result);
        return result;
    }

    private static void CollectEnabledSkillButtons(
        Node node,
        List<BattleSkillSlotButton> result
    )
    {
        if (node == null || !GodotObject.IsInstanceValid(node))
            return;
        foreach (Node child in node.GetChildren())
        {
            if (
                child is BattleSkillSlotButton button
                && IsClickable(button, button)
            )
            {
                result.Add(button);
            }
            CollectEnabledSkillButtons(child, result);
        }
    }

    private static bool IsClickable(CanvasItem owner, Button button)
    {
        if (
            owner == null
            || button == null
            || !GodotObject.IsInstanceValid(owner)
            || !GodotObject.IsInstanceValid(button)
            || !owner.IsInsideTree()
            || !button.IsInsideTree()
            || !owner.IsVisibleInTree()
            || !button.IsVisibleInTree()
            || button.Disabled
        )
        {
            return false;
        }
        Rect2 rect = button.GetGlobalRect();
        return rect.Size.X > 0.0f && rect.Size.Y > 0.0f;
    }

    private static int GetActionCount(
        GameRuntimeFacade runtime,
        StringName unitId,
        string actionId
    )
    {
        BattleMetricsState metrics = runtime?.GetBattleRuntime()?.GetBattleMetricsTyped();
        if (
            metrics == null
            || unitId == ""
            || !metrics.Units.TryGetValue(unitId.ToString(), out BattleMetricEntry entry)
            || entry == null
            || !entry.ActionCounts.TryGetValue(actionId, out int count)
        )
        {
            return 0;
        }
        return count;
    }

    private static string DescribeBattle(GameRuntimeFacade runtime)
    {
        BattleState state = runtime?.GetBattleState();
        if (state == null)
            return "battle_state=null";

        string units = string.Join(
            ",",
            state
                .Units()
                .Where(unit => unit != null)
                .OrderBy(unit => unit.unit_id.ToString(), StringComparer.Ordinal)
                .Select(unit =>
                    $"{unit.unit_id}:{unit.faction_id}:hp={unit.current_hp}:alive={unit.is_alive}:coord={unit.coord}"
                )
        );
        return
            $"phase={state.phase},tu={state.timeline?.current_tu ?? -1},active={state.active_unit_id},modal={runtime.GetActiveModalId()},units=[{units}]";
    }

    private static string DescribePlayerMetrics(GameRuntimeFacade runtime)
    {
        BattleMetricsState metrics = runtime?.GetBattleRuntime()?.GetBattleMetricsTyped();
        if (metrics == null)
            return "metrics=null";
        BattleMetricEntry entry = metrics.Units.Values.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(
                candidate.FactionId,
                runtime.GetPlayerFactionId(),
                StringComparison.Ordinal
            )
        );
        if (entry == null)
            return "player_metrics=missing";
        return
            $"actions=[{FormatCounts(entry.ActionCounts)}],attempts=[{FormatCounts(entry.SkillAttemptCounts)}],success=[{FormatCounts(entry.SkillSuccessCounts)}],damage={entry.TotalDamageDone},taken={entry.TotalDamageTaken},kills={entry.KillCount},deaths={entry.DeathCount}";
    }

    private static string DescribeRecentBattleLogs(BattleState state)
    {
        if (state?.log_entries == null || state.log_entries.Count == 0)
            return "none";
        int start = Math.Max(state.log_entries.Count - 16, 0);
        return string.Join(
            " | ",
            Enumerable
                .Range(start, state.log_entries.Count - start)
                .Select(index => state.log_entries[index])
        );
    }

    private static string FormatCounts(IReadOnlyDictionary<string, int> counts)
    {
        if (counts == null || counts.Count == 0)
            return "";
        return string.Join(
            ",",
            counts
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => $"{entry.Key}:{entry.Value}")
        );
    }

    private static int ManhattanDistance(Vector2I left, Vector2I right) =>
        Math.Abs(left.X - right.X) + Math.Abs(left.Y - right.Y);

    private static int DistanceBetweenCoordAndUnit(Vector2I coord, BattleUnitState unit)
    {
        IReadOnlyList<Vector2I> occupiedCoords = unit?.GetOccupiedCoordsTyped();
        if (occupiedCoords == null || occupiedCoords.Count == 0)
            return unit != null ? ManhattanDistance(coord, unit.coord) : int.MaxValue;
        return occupiedCoords.Min(occupiedCoord => ManhattanDistance(coord, occupiedCoord));
    }

    private static int DistanceBetweenUnitAndUnit(
        BattleUnitState first,
        BattleUnitState second
    )
    {
        IReadOnlyList<Vector2I> firstCoords = first?.GetOccupiedCoordsTyped();
        if (firstCoords == null || firstCoords.Count == 0)
            return DistanceBetweenCoordAndUnit(first?.coord ?? Vector2I.Zero, second);
        return firstCoords.Min(coord => DistanceBetweenCoordAndUnit(coord, second));
    }

    private static bool HasTerminalDecision(BattleState state) =>
        state != null
        && (
            state.FinalDecision != null
            || state.PhaseKind == BattlePhaseKind.BattleEnded
        );

    private static Key KeyForDirection(Vector2I direction)
    {
        if (direction == Vector2I.Left)
            return Key.Left;
        if (direction == Vector2I.Right)
            return Key.Right;
        if (direction == Vector2I.Up)
            return Key.Up;
        if (direction == Vector2I.Down)
            return Key.Down;
        throw new InvalidOperationException(
            $"Battle input requested a non-cardinal direction: {direction}."
        );
    }
}
