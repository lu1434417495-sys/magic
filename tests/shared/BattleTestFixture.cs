using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

internal sealed class BattleTestFixture : IDisposable
{
    private BattleTestFixture(
        BattleRuntimeModule runtime,
        BattleState state,
        IReadOnlyList<BattleUnitState> allies,
        IReadOnlyList<BattleUnitState> enemies
    )
    {
        Runtime = runtime;
        State = state;
        Allies = allies;
        Enemies = enemies;
    }

    public BattleRuntimeModule Runtime { get; }
    public BattleState State { get; }
    public IReadOnlyList<BattleUnitState> Allies { get; }
    public IReadOnlyList<BattleUnitState> Enemies { get; }

    public static BattleTestFixture CreateFlatBattle(
        StringName battleId,
        Vector2I mapSize,
        IEnumerable<BattleUnitState> allies,
        IEnumerable<BattleUnitState> enemies
    )
    {
        BattleState state = BuildFlatState(battleId, mapSize);
        List<BattleUnitState> allyList = CopyUnits(allies);
        List<BattleUnitState> enemyList = CopyUnits(enemies);
        InstallUnits(state, allyList, enemyList);

        var runtime = new BattleRuntimeModule();
        runtime.setup();
        runtime.SetupStateForTests(state);
        return new BattleTestFixture(runtime, state, allyList, enemyList);
    }

    public static BattleTestFixture CreateFlatBattle(StringName battleId, Vector2I mapSize)
    {
        return CreateFlatBattle(
            battleId,
            mapSize,
            Array.Empty<BattleUnitState>(),
            Array.Empty<BattleUnitState>()
        );
    }

    public static BattleState BuildFlatState(StringName battleId, Vector2I mapSize)
    {
        var state = new BattleState
        {
            battle_id = battleId,
            phase = "unit_acting",
            map_size = mapSize,
            timeline = new BattleTimelineState(),
        };
        state.SetCellsFromDictionary(BuildFlatCells(mapSize), duplicateCells: false);
        return state;
    }

    public static BattleUnitState BuildUnit(
        StringName unitId,
        StringName factionId,
        Vector2I coord,
        int currentAp = 1,
        int currentHp = 0
    )
    {
        int resolvedHp = currentHp > 0 ? currentHp : (factionId == new StringName("enemy") ? 30 : 100);
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            current_ap = currentAp,
            current_move_points = 2,
            current_hp = resolvedHp,
            is_alive = true,
        };
        unit.attribute_snapshot.SetValue("hp_max", resolvedHp);
        unit.SetAnchorCoord(coord);
        return unit;
    }

    public static void InstallUnits(
        BattleState state,
        IReadOnlyList<BattleUnitState> allyUnits,
        IReadOnlyList<BattleUnitState> enemyUnits
    )
    {
        state.ClearUnits();
        state.ally_unit_ids = new GStringNameArray();
        state.enemy_unit_ids = new GStringNameArray();
        var gridService = new BattleGridService();

        foreach (BattleUnitState unit in allyUnits ?? Array.Empty<BattleUnitState>())
        {
            state.SetUnit(unit);
            gridService.PlaceUnit(state, unit, unit.coord, ignore_height: true);
            state.ally_unit_ids.Add(unit.unit_id);
        }
        foreach (BattleUnitState unit in enemyUnits ?? Array.Empty<BattleUnitState>())
        {
            state.SetUnit(unit);
            gridService.PlaceUnit(state, unit, unit.coord, ignore_height: true);
            state.enemy_unit_ids.Add(unit.unit_id);
        }
        state.active_unit_id =
            state.ally_unit_ids.Count > 0 ? state.ally_unit_ids[0] : new StringName("");
    }

    public void Dispose()
    {
        Runtime?.Dispose();
    }

    private static GDictionary BuildFlatCells(Vector2I mapSize)
    {
        var cells = new GDictionary();
        for (int y = 0; y < Mathf.Max(mapSize.Y, 0); y++)
        {
            for (int x = 0; x < Mathf.Max(mapSize.X, 0); x++)
            {
                Vector2I coord = new(x, y);
                var cell = new BattleCellState
                {
                    coord = coord,
                    base_terrain = "land",
                    base_height = 4,
                    height_offset = 0,
                };
                cell.RecalculateRuntimeValues();
                cells[coord] = cell;
            }
        }
        return cells;
    }

    private static List<BattleUnitState> CopyUnits(IEnumerable<BattleUnitState> units)
    {
        return units == null ? new List<BattleUnitState>() : new List<BattleUnitState>(units);
    }
}
