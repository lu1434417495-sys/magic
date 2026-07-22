using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class run_enter_battle_e2e : E2eSceneTree
{
    private const int RouteStepMaxFrames = 600;
    private const ulong RouteStepTimeoutMsec = 5000;
    private const int BattleReadyMaxFrames = 12000;
    private const ulong BattleReadyTimeoutMsec = 30000;
    private static readonly StringName SingleEncounterKind = "single";

    private readonly record struct RouteState(Vector2I Coord, bool HasExitedStartSettlement);

    private sealed record RoutePlan(EncounterAnchorData Target, List<Vector2I> Steps);

    private protected override string ScenarioLabel => "E2E world map to battle";

    private protected override async Task RunScenarioAsync()
    {
        WorldMapSystem worldMap = await CreateTestGameThroughUiAsync("E2E Battler");
        GameRuntimeFacade runtime = worldMap._runtime;
        GameSession gameSession = worldMap._game_session;
        WorldRuntimeData worldData = runtime.GetActiveWorldRuntimeData();
        RoutePlan route = FindRouteToEncounter(runtime, worldData);

        Test.True(route != null, "Generated test world should expose a safe route to a wild encounter.");
        if (route == null)
            return;

        Vector2I expectedCoord = runtime.GetPlayerCoord();
        foreach (Vector2I nextCoord in route.Steps)
        {
            Vector2I direction = nextCoord - expectedCoord;
            await Input.TapKeyAsync(KeyForDirection(direction));
            await Wait.UntilAsync(
                () => runtime.GetPlayerCoord() == nextCoord || runtime.IsBattleActive(),
                RouteStepMaxFrames,
                RouteStepTimeoutMsec,
                $"world movement to reach {nextCoord}"
            );

            expectedCoord = nextCoord;
            if (nextCoord != route.Target.world_coord)
            {
                Test.Eq(
                    runtime.GetPlayerCoord(),
                    nextCoord,
                    "Each routed world step should be applied through keyboard input."
                );
                Test.False(runtime.IsBattleActive(), "Battle should not start before the target cell.");
                Test.True(
                    string.IsNullOrEmpty(runtime.GetActiveModalId()),
                    "The planned route should avoid incidental world modals."
                );
            }
        }

        await Wait.UntilAsync(
            () =>
                runtime.IsBattleActive()
                && runtime.GetActiveModalId() == "battle_start_confirm"
                && runtime.GetBattleState() != null
                && !runtime.GetBattleState().IsEmpty(),
            BattleReadyMaxFrames,
            BattleReadyTimeoutMsec,
            "wild encounter battle-start confirmation"
        );

        BattleState battleState = runtime.GetBattleState();
        Test.Eq(
            runtime.GetPlayerCoord(),
            route.Target.world_coord,
            "Keyboard movement should land on the dynamically selected encounter anchor."
        );
        Test.True(gameSession.IsBattleSaveLocked(), "Encounter entry should lock world-save writes.");
        Test.True(worldMap.submap_entry_window.IsVisibleInTree(), "Battle confirmation UI should be visible.");
        Test.True(
            battleState.timeline != null && battleState.timeline.frozen,
            "Battle timeline should remain frozen before the player confirms entry."
        );

        await Wait.UntilAsync(
            () =>
                !worldMap.battle_loading_overlay.Visible
                && !worldMap.battle_map_panel.IsLoadingBattle()
                && worldMap.battle_map_panel.IsBattleRenderContentReady()
                && worldMap.submap_entry_window.IsVisibleInTree()
                && worldMap.submap_entry_window.confirm_button != null
                && !worldMap.submap_entry_window.confirm_button.Disabled,
            BattleReadyMaxFrames,
            BattleReadyTimeoutMsec,
            "battle render content and unobstructed confirmation button"
        );
        await Input.ClickAsync(worldMap.submap_entry_window.confirm_button);

        await Wait.UntilAsync(
            () =>
            {
                BattleState current = runtime.GetBattleState();
                return runtime.IsBattleActive()
                    && string.IsNullOrEmpty(runtime.GetActiveModalId())
                    && current?.timeline != null
                    && !current.timeline.frozen
                    && worldMap.battle_map_panel.IsVisibleInTree()
                    && worldMap.battle_map_panel.IsBattleRenderContentReady()
                    && !worldMap.battle_loading_overlay.Visible;
            },
            BattleReadyMaxFrames,
            BattleReadyTimeoutMsec,
            "confirmed battle UI to become interactive"
        );

        Test.True(
            !string.IsNullOrWhiteSpace(runtime.GetActiveBattleEncounterName()),
            "Ready battle UI should expose the active encounter name."
        );
        Test.True(gameSession.IsBattleSaveLocked(), "Save lock should remain active during battle.");
        Test.False(worldMap.world_map_view.Visible, "World-map rendering should hide during battle.");
        Test.False(worldMap.bottom_action_bar.Visible, "World actions should hide during battle.");
        Test.False(
            worldMap.submap_entry_window.Visible,
            "Battle-start confirmation should close after the real button click."
        );
    }

    private static RoutePlan FindRouteToEncounter(
        GameRuntimeFacade runtime,
        WorldRuntimeData worldData
    )
    {
        Vector2I start = runtime.GetPlayerCoord();
        IEnumerable<EncounterAnchorData> candidates = worldData.EncounterAnchors
            .Where(anchor =>
                anchor != null
                && !anchor.is_cleared
                && anchor.encounter_kind == SingleEncounterKind
                && anchor.suppressed_until_step <= worldData.WorldStep
                && anchor.world_coord != start
            )
            .OrderBy(anchor => ManhattanDistance(start, anchor.world_coord))
            .ThenBy(anchor => anchor.entity_id.ToString(), StringComparer.Ordinal);

        foreach (EncounterAnchorData target in candidates)
        {
            List<Vector2I> steps = FindRoute(runtime.GetGridSystem(), worldData, start, target);
            if (steps != null)
                return new RoutePlan(target, steps);
        }
        return null;
    }

    private static List<Vector2I> FindRoute(
        WorldMapGridSystem grid,
        WorldRuntimeData worldData,
        Vector2I start,
        EncounterAnchorData target
    )
    {
        WorldMapSettlementRecordData startSettlement = worldData.Settlements.FirstOrDefault(
            settlement => Contains(settlement, start)
        );
        var startState = new RouteState(start, startSettlement == null);
        var queue = new Queue<RouteState>();
        var visited = new HashSet<RouteState> { startState };
        var previous = new Dictionary<RouteState, RouteState>();
        queue.Enqueue(startState);

        RouteState? found = null;
        while (queue.Count > 0)
        {
            RouteState current = queue.Dequeue();
            if (current.Coord == target.world_coord)
            {
                found = current;
                break;
            }

            foreach (Vector2I nextCoord in grid.GetNeighbors4(current.Coord))
            {
                bool exitedStartSettlement =
                    current.HasExitedStartSettlement
                    || (startSettlement != null && !Contains(startSettlement, nextCoord));
                var next = new RouteState(nextCoord, exitedStartSettlement);
                if (visited.Contains(next))
                    continue;
                if (IsBlocked(worldData, next, startSettlement, target))
                    continue;
                visited.Add(next);
                previous[next] = current;
                queue.Enqueue(next);
            }
        }

        if (found == null)
            return null;

        var reversed = new List<Vector2I>();
        RouteState cursor = found.Value;
        while (cursor != startState)
        {
            reversed.Add(cursor.Coord);
            cursor = previous[cursor];
        }
        reversed.Reverse();
        return reversed;
    }

    private static bool IsBlocked(
        WorldRuntimeData worldData,
        RouteState state,
        WorldMapSettlementRecordData startSettlement,
        EncounterAnchorData target
    )
    {
        foreach (WorldMapSettlementRecordData settlement in worldData.Settlements)
        {
            if (!Contains(settlement, state.Coord))
                continue;
            if (ReferenceEquals(settlement, startSettlement) && !state.HasExitedStartSettlement)
                break;
            return true;
        }

        if (worldData.WorldEvents.Any(worldEvent => worldEvent.WorldCoord == state.Coord))
            return true;
        if (worldData.ResourceNodes.Any(resourceNode => resourceNode.WorldCoord == state.Coord))
            return true;
        return worldData.EncounterAnchors.Any(anchor =>
            anchor != null
            && !anchor.is_cleared
            && anchor.world_coord == state.Coord
            && !ReferenceEquals(anchor, target)
        );
    }

    private static bool Contains(WorldMapSettlementRecordData settlement, Vector2I coord)
    {
        if (settlement == null)
            return false;
        Vector2I offset = coord - settlement.Origin;
        return offset.X >= 0
            && offset.Y >= 0
            && offset.X < settlement.FootprintSize.X
            && offset.Y < settlement.FootprintSize.Y;
    }

    private static int ManhattanDistance(Vector2I left, Vector2I right) =>
        Math.Abs(left.X - right.X) + Math.Abs(left.Y - right.Y);

    private static Key KeyForDirection(Vector2I direction)
    {
        if (direction == Vector2I.Left)
            return Key.A;
        if (direction == Vector2I.Right)
            return Key.D;
        if (direction == Vector2I.Up)
            return Key.W;
        if (direction == Vector2I.Down)
            return Key.S;
        throw new InvalidOperationException($"Route contains a non-cardinal step: {direction}.");
    }
}
