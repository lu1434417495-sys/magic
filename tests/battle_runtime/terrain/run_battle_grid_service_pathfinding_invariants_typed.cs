using System;
using System.Collections.Generic;
using Godot;

public partial class run_battle_grid_service_pathfinding_invariants_typed : SceneTree
{
    private static readonly StringName[] AllTerrains =
    {
        "land",
        "forest",
        "water",
        "shallow_water",
        "flowing_water",
        "deep_water",
        "mud",
        "spike",
    };

    private readonly TestHarness _test = new();
    private readonly GodotTransientResourceScope _runtimeScope =
        new("battle_grid_service_pathfinding_invariants", quarantineOnDrain: true);
    private BattleGridService _grid = null!;

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    public int RunForWrapper() => Run();

    private int Run()
    {
        _grid = _runtimeScope.OwnValueGraph(new BattleGridService(), "grid");
        try
        {
            TestStepCostFloor();
            TestAStarSimpleOptimality();
            TestAStarStopsWhenMoveBudgetBelowHeuristic();
            TestAStarBudgetCapKeepsAdjacentPlacementFailure();
            TestAStarMatchesReferenceWithMudStripe();
            TestAStarMatchesReferenceRandomized();
            TestPathTreeMatchesReferenceWithMudStripe();
            TestPathTreeRespectsOccupantBlocks();
        }
        finally
        {
            DisposeOwnedRuntimeState();
        }
        return _test.Finish("Battle grid service pathfinding inoptions");
    }

    private void TestStepCostFloor()
    {
        BattleState state = BuildState(new Vector2I(3, 3));
        BattleUnitState unit = BuildUnit(Vector2I.Zero);
        foreach (StringName terrainId in AllTerrains)
        {
            BattleCellState cell = state.GetCell(new Vector2I(2, 2));
            if (cell == null)
            {
                _test.Fail($"setup error: cell (2,2) missing while seeding terrain {terrainId}.");
                continue;
            }

            cell.base_terrain = terrainId;
            int cost = _grid.GetUnitMoveCost(state, unit, new Vector2I(2, 2));
            if (cost < 1)
            {
                _test.Fail(
                    $"get_unit_move_cost returned {cost} for terrain={terrainId}. Manhattan A* heuristic requires step_cost >= 1."
                );
            }
        }
    }

    private void TestAStarSimpleOptimality()
    {
        BattleState state = BuildState(new Vector2I(8, 8));
        BattleUnitState unit = BuildUnit(Vector2I.Zero);
        state.SetUnit(unit);
        if (!_grid.PlaceUnit(state, unit, unit.coord, true))
        {
            _test.Fail("simple optimality: failed to place unit at origin.");
            return;
        }

        Vector2I toCoord = new(5, 3);
        BattleMovePathResult result = _grid.ResolveUnitMovePathTyped(
            state,
            unit,
            unit.coord,
            toCoord,
            99,
            null
        );

        if (!result.Allowed)
        {
            _test.Fail($"simple optimality: expected straight path to be allowed, got message={result.Message}.");
            return;
        }

        int expected = Math.Abs(toCoord.X - unit.coord.X) + Math.Abs(toCoord.Y - unit.coord.Y);
        _test.Eq(result.Cost, expected, "simple optimality: Manhattan cost should match.");
    }

    private void TestAStarStopsWhenMoveBudgetBelowHeuristic()
    {
        BattleState state = BuildState(new Vector2I(12, 1));
        BattleUnitState unit = BuildUnit(Vector2I.Zero);
        state.SetUnit(unit);
        if (!_grid.PlaceUnit(state, unit, unit.coord, true))
        {
            _test.Fail("budget cap: failed to place unit at origin.");
            return;
        }

        int budget = 3;
        int providerCalls = 0;
        BattleMovePathResult result = _grid.ResolveUnitMovePathTyped(
            state,
            unit,
            unit.coord,
            new Vector2I(11, 0),
            budget,
            (candidateUnit, coord) =>
            {
                providerCalls++;
                return _grid.GetUnitMoveCost(state, candidateUnit, coord);
            }
        );

        if (result.Allowed)
        {
            _test.Fail("budget cap: expected far destination to be rejected.");
        }
        _test.Eq(
            result.Message,
            "移动力不足，无法移动。",
            "budget cap: placeable over-budget target should report move-point failure."
        );
        if (result.Cost <= budget)
        {
            _test.Fail($"budget cap: rejected path should report cost > budget, got {result.Cost}.");
        }
        if (providerCalls != 0)
        {
            _test.Fail($"budget cap: heuristic cap should stop before move-cost queries, got {providerCalls}.");
        }
        if (result.Path.Count != 0)
        {
            _test.Fail("budget cap: early stop should not fabricate an exact path.");
        }
    }

    private void TestAStarBudgetCapKeepsAdjacentPlacementFailure()
    {
        BattleState state = BuildState(new Vector2I(2, 1));
        BattleUnitState unit = BuildUnit(Vector2I.Zero);
        BattleUnitState blocker = BuildUnit(new Vector2I(1, 0));
        blocker.unit_id = "path_inoption_adjacent_blocker";
        state.SetUnit(unit);
        state.SetUnit(blocker);
        if (!_grid.PlaceUnit(state, unit, unit.coord, true))
        {
            _test.Fail("budget cap adjacent blocker: failed to place unit.");
            return;
        }
        if (!_grid.PlaceUnit(state, blocker, blocker.coord, true))
        {
            _test.Fail("budget cap adjacent blocker: failed to place blocker.");
            return;
        }

        BattleMovePathResult result = _grid.ResolveUnitMovePathTyped(
            state,
            unit,
            unit.coord,
            blocker.coord,
            0,
            null
        );

        if (result.Allowed)
        {
            _test.Fail("budget cap adjacent blocker: expected occupied destination to be rejected.");
        }
        _test.Eq(
            result.Message,
            "目标区域不可放置当前单位。",
            "budget cap adjacent blocker: occupied target should keep placement failure."
        );
    }

    private void TestAStarMatchesReferenceWithMudStripe()
    {
        BattleState state = BuildState(new Vector2I(5, 5));
        BattleUnitState unit = BuildUnit(Vector2I.Zero);
        state.SetUnit(unit);
        if (!_grid.PlaceUnit(state, unit, unit.coord, true))
        {
            _test.Fail("mud stripe: failed to place unit at origin.");
            return;
        }

        for (int x = 0; x < 5; x++)
        {
            BattleCellState cell = state.GetCell(new Vector2I(x, 1));
            if (cell != null)
            {
                cell.base_terrain = "mud";
            }
        }

        Vector2I toCoord = new(4, 4);
        BattleMovePathResult aStar = _grid.ResolveUnitMovePathTyped(
            state,
            unit,
            unit.coord,
            toCoord,
            99,
            null
        );
        int referenceCost = ReferenceDijkstraCost(state, unit, unit.coord, toCoord);
        if (!aStar.Allowed)
        {
            _test.Fail("mud stripe: expected destination to be reachable.");
            return;
        }

        _test.Eq(
            aStar.Cost,
            referenceCost,
            "mud stripe: A* cost should match reference Dijkstra."
        );
    }

    private void TestAStarMatchesReferenceRandomized()
    {
        var rng = new RandomNumberGenerator();
        rng.Seed = 0xA11C0DE;
        int trialsRun = 0;

        for (int trial = 0; trial < 12; trial++)
        {
            Vector2I size = new(rng.RandiRange(4, 8), rng.RandiRange(4, 8));
            BattleState state = BuildState(size);
            BattleUnitState unit = BuildUnit(Vector2I.Zero);
            state.SetUnit(unit);
            if (!_grid.PlaceUnit(state, unit, unit.coord, true))
            {
                continue;
            }

            int mudCount = rng.RandiRange(0, (size.X * size.Y) / 4);
            for (int i = 0; i < mudCount; i++)
            {
                Vector2I mudCoord = new(rng.RandiRange(0, size.X - 1), rng.RandiRange(0, size.Y - 1));
                if (mudCoord == unit.coord)
                {
                    continue;
                }
                BattleCellState cell = state.GetCell(mudCoord);
                if (cell != null)
                {
                    cell.base_terrain = "mud";
                }
            }

            Vector2I dest = new(rng.RandiRange(0, size.X - 1), rng.RandiRange(0, size.Y - 1));
            if (dest == unit.coord)
            {
                continue;
            }

            int referenceCost = ReferenceDijkstraCost(state, unit, unit.coord, dest);
            BattleMovePathResult aStar = _grid.ResolveUnitMovePathTyped(
                state,
                unit,
                unit.coord,
                dest,
                9999,
                null
            );

            if (referenceCost < 0)
            {
                if (aStar.Allowed)
                {
                    _test.Fail($"randomized trial {trial}: reference says unreachable but A* allows path.");
                }
                continue;
            }

            if (!aStar.Allowed)
            {
                _test.Fail(
                    $"randomized trial {trial} size={size} dest={dest}: A* refused a path reference reaches at cost {referenceCost}."
                );
                continue;
            }

            if (aStar.Cost != referenceCost)
            {
                _test.Fail(
                    $"randomized trial {trial} size={size} dest={dest}: A* cost={aStar.Cost} != reference={referenceCost}."
                );
            }
            trialsRun++;
        }

        if (trialsRun == 0)
        {
            _test.Fail("randomized differential test ran 0 trials — RNG/setup degenerate, inoption unverified.");
        }
    }

    private void TestPathTreeMatchesReferenceWithMudStripe()
    {
        BattleState state = BuildState(new Vector2I(5, 5));
        BattleUnitState unit = BuildUnit(Vector2I.Zero);
        state.SetUnit(unit);
        if (!_grid.PlaceUnit(state, unit, unit.coord, true))
        {
            _test.Fail("path tree mud stripe: failed to place unit at origin.");
            return;
        }

        for (int x = 0; x < 5; x++)
        {
            BattleCellState cell = state.GetCell(new Vector2I(x, 1));
            if (cell != null)
            {
                cell.base_terrain = "mud";
            }
        }

        BattleMovePathTreeResult tree = _grid.BuildUnitMovePathTreeTyped(
            state,
            unit,
            unit.coord,
            99,
            null
        );

        for (int y = 0; y < state.map_size.Y; y++)
        {
            for (int x = 0; x < state.map_size.X; x++)
            {
                Vector2I dest = new(x, y);
                int referenceCost = ReferenceDijkstraCost(state, unit, unit.coord, dest);
                if (referenceCost < 0)
                {
                    if (tree.Costs.ContainsKey(dest))
                    {
                        _test.Fail(
                            $"path tree mud stripe: dest={dest} should be unreachable, got cost={tree.Costs[dest]}."
                        );
                    }
                    continue;
                }

                int treeCost = tree.Costs.TryGetValue(dest, out int cost) ? cost : -1;
                if (treeCost != referenceCost)
                {
                    _test.Fail(
                        $"path tree mud stripe: dest={dest} cost={treeCost} != reference={referenceCost}."
                    );
                }
            }
        }
    }

    private void TestPathTreeRespectsOccupantBlocks()
    {
        BattleState state = BuildState(new Vector2I(3, 1));
        BattleUnitState unit = BuildUnit(Vector2I.Zero);
        BattleUnitState blocker = BuildUnit(new Vector2I(1, 0));
        blocker.unit_id = "path_tree_blocker";
        state.SetUnit(unit);
        state.SetUnit(blocker);
        if (!_grid.PlaceUnit(state, unit, unit.coord, true))
        {
            _test.Fail("path tree occupant: failed to place unit.");
            return;
        }
        if (!_grid.PlaceUnit(state, blocker, blocker.coord, true))
        {
            _test.Fail("path tree occupant: failed to place blocker.");
            return;
        }

        BattleMovePathTreeResult tree = _grid.BuildUnitMovePathTreeTyped(
            state,
            unit,
            unit.coord,
            99,
            null
        );

        if (tree.Costs.ContainsKey(new Vector2I(1, 0)))
        {
            _test.Fail("path tree occupant: blocker coord should not be reachable.");
        }
        if (tree.Costs.ContainsKey(new Vector2I(2, 0)))
        {
            _test.Fail("path tree occupant: coord behind blocker should not be reachable on a 1-row map.");
        }
    }

    private int ReferenceDijkstraCost(
        BattleState state,
        BattleUnitState unitState,
        Vector2I fromCoord,
        Vector2I toCoord
    )
    {
        if (fromCoord == toCoord)
        {
            return 0;
        }

        var best = new Dictionary<Vector2I, int> { [fromCoord] = 0 };
        var frontier = new List<Vector2I> { fromCoord };

        while (frontier.Count > 0)
        {
            int pickIndex = 0;
            int pickCost = best[frontier[0]];
            for (int i = 1; i < frontier.Count; i++)
            {
                int candidateCost = best[frontier[i]];
                if (candidateCost < pickCost)
                {
                    pickCost = candidateCost;
                    pickIndex = i;
                }
            }

            Vector2I currentCoord = frontier[pickIndex];
            frontier.RemoveAt(pickIndex);
            int currentCost = best[currentCoord];
            if (currentCoord == toCoord)
            {
                return currentCost;
            }

            foreach (Vector2I neighbor in _grid.GetNeighbors4(state, currentCoord))
            {
                if (!_grid.CanUnitStepBetweenAnchors(state, unitState, currentCoord, neighbor))
                {
                    continue;
                }

                int stepCost = _grid.GetUnitMoveCost(state, unitState, neighbor);
                int nextCost = currentCost + stepCost;
                if (nextCost >= best.GetValueOrDefault(neighbor, int.MaxValue))
                {
                    continue;
                }

                best[neighbor] = nextCost;
                frontier.Add(neighbor);
            }
        }

        return -1;
    }

    private BattleState BuildState(Vector2I mapSize)
    {
        var state = _runtimeScope.OwnWrapper(
            new BattleState { map_size = mapSize },
            "battle-state"
        );
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                Vector2I coord = new(x, y);
                var cell = _runtimeScope.OwnWrapper(
                    new BattleCellState
                    {
                        coord = coord,
                        base_terrain = "land",
                        passable = true,
                    },
                    "battle-cell"
                );
                state.SetCell(coord, cell);
            }
        }
        _runtimeScope.OwnWrapper(state, "battle-state-built");
        return state;
    }

    private BattleUnitState BuildUnit(Vector2I coord)
    {
        var unit = new BattleUnitState
        {
            unit_id = "path_inoption_unit",
            display_name = "PathInoption",
            faction_id = "player",
            coord = coord,
            is_alive = true,
        };
        unit.RefreshFootprint();
        return _runtimeScope.OwnWrapper(unit, "battle-unit");
    }

    private void DisposeOwnedRuntimeState()
    {
        _runtimeScope.Close();
        _grid?.Dispose();
        _grid = null!;
    }
}
