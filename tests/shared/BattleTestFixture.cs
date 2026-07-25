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
        state.SetCells(BuildFlatCells(mapSize));
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
        };
        unit.SetCombatResources(
            resolvedHp,
            mp: 0,
            stamina: 0,
            aura: 0,
            currentAp,
            movePoints: 2
        );
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
            gridService.PlaceUnit(state, unit, unit.GetAnchorCoord(), ignore_height: true);
            state.ally_unit_ids.Add(unit.unit_id);
        }
        foreach (BattleUnitState unit in enemyUnits ?? Array.Empty<BattleUnitState>())
        {
            state.SetUnit(unit);
            gridService.PlaceUnit(state, unit, unit.GetAnchorCoord(), ignore_height: true);
            state.enemy_unit_ids.Add(unit.unit_id);
        }
        state.active_unit_id =
            state.ally_unit_ids.Count > 0 ? state.ally_unit_ids[0] : new StringName("");
    }

    public void Dispose()
    {
        DisposeBattleFixture(Runtime, State);
    }

    public static void ConfigureDamageResolverForTests(
        BattleRuntimeModule runtime,
        BattleDamageResolver damageResolver
    )
    {
        if (runtime == null)
        {
            damageResolver?.Dispose();
            return;
        }

        BattleDamageResolver previousResolver = runtime.GetDamageResolver();
        runtime.ConfigureDamageResolverForTests(damageResolver);
        if (!ReferenceEquals(previousResolver, damageResolver))
            DisposeDamageResolver(previousResolver);
    }

    public static void ConfigureHitResolverForTests(
        BattleRuntimeModule runtime,
        BattleHitResolver hitResolver
    )
    {
        if (runtime == null)
        {
            hitResolver?.Dispose();
            return;
        }

        BattleHitResolver previousResolver = runtime.GetHitResolver();
        runtime.ConfigureHitResolverForTests(hitResolver);
        if (!ReferenceEquals(previousResolver, hitResolver))
            DisposeHitResolver(previousResolver);
    }

    public static void DisposeRuntime(BattleRuntimeModule runtime)
    {
        if (runtime == null)
            return;
        runtime.SetupStateForTests(null);
        runtime.Dispose();
    }

    public static void DisposeBattleFixture(
        BattleRuntimeModule runtime,
        BattleState state,
        params object[] ownedObjects
    )
    {
        if (runtime != null)
            runtime.SetupStateForTests(null);

        if (ownedObjects != null)
        {
            foreach (object ownedObject in ownedObjects)
                DisposeFixtureObject(ownedObject);
        }
        DisposeBattleState(state);

        DisposeRuntime(runtime);
    }

    public static void DisposeBattleState(BattleState state)
    {
        if (state == null)
            return;
        var units = new List<BattleUnitState>();
        var cells = new List<BattleCellState>();
        foreach (BattleUnitState unit in state.Units())
            units.Add(unit);
        foreach (BattleCellState cell in state.Cells())
            cells.Add(cell);

        state.ClearBattleTopology();
        foreach (BattleUnitState unit in units)
            DisposeBattleUnit(unit);
        foreach (BattleCellState cell in cells)
            DisposeBattleCell(cell);
        DisposeWarehouseState(state.party_backpack_view);
    }

    public static void DisposeFixtureObject(object ownedObject)
    {
        switch (ownedObject)
        {
            case null:
                return;
            case BattleCommand command:
                DisposeBattleCommand(command);
                return;
            case BattlePreview preview:
                DisposeBattlePreview(preview);
                return;
            case BattleEventBatch batch:
                batch.Dispose();
                return;
            case BattleUnitState unit:
                DisposeBattleUnit(unit);
                return;
            default:
                return;
        }
    }

    public static void DisposeBattleAiScoreInput(BattleAiScoreInput scoreInput)
    {
        if (scoreInput == null)
            return;
        DisposeBattlePreview(scoreInput.preview);
        DisposeBattleCommand(scoreInput.command);
        scoreInput.preview = null;
        scoreInput.command = null;
    }

    public static void DisposeBattlePreview(BattlePreview preview)
    {
        if (preview == null)
            return;
        preview.hit_preview = null;
    }

    public static void DisposeBattleUnit(BattleUnitState unit)
    {
        if (unit == null)
            return;
        DisposeEquipmentState(unit.equipment_view);
        unit.ClearStatusEffects();
    }

    public static void DisposeBattleCell(BattleCellState cell)
    {
        BattleCellState.DisposeRuntimeGraph(cell);
    }

    public static void DisposeWarehouseState(WarehouseState warehouseState)
    {
        if (warehouseState == null)
            return;
        warehouseState.stacks.Clear();
        warehouseState.equipment_instances.Clear();
    }

    public static void DisposeBattleCommand(BattleCommand command)
    {
        if (command == null)
            return;
        command.equipment_instance = null;
    }

    public static void DisposeEquipmentState(EquipmentState equipmentState)
    {
        if (equipmentState == null)
            return;
        foreach (StringName entrySlotId in equipmentState.GetEntrySlotIdsTyped())
            equipmentState.ClearEntrySlot(entrySlotId);
    }

    public static void DisposeDamageResolver(BattleDamageResolver resolver)
    {
        if (resolver == null)
            return;
        resolver.Dispose();
    }

    public static void DisposeHitResolver(BattleHitResolver resolver)
    {
        resolver?.Dispose();
    }

    private static Dictionary<Vector2I, BattleCellState> BuildFlatCells(Vector2I mapSize)
    {
        var cells = new Dictionary<Vector2I, BattleCellState>();
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

internal static class BattleTestCombatResourceFixtureExtensions
{
    internal static BattleUnitState WithCombatResourcesForTest(
        this BattleUnitState unit,
        int? hp = null,
        int? mp = null,
        int? stamina = null,
        int? aura = null,
        int? ap = null,
        int? movePoints = null,
        bool? isAlive = null
    )
    {
        ArgumentNullException.ThrowIfNull(unit);

        if (hp.HasValue)
            unit.SetCurrentHp(hp.Value);
        if (mp.HasValue)
            unit.SetCurrentMp(mp.Value);
        if (stamina.HasValue)
            unit.SetCurrentStamina(stamina.Value);
        if (aura.HasValue)
            unit.SetCurrentAura(aura.Value);
        if (ap.HasValue)
            unit.SetCurrentAp(ap.Value);
        if (movePoints.HasValue)
            unit.SetCurrentMovePoints(movePoints.Value);

        if (isAlive.HasValue && unit.IsAlive() != isAlive.Value)
        {
            if (!isAlive.Value && unit.GetCurrentHp() <= 0)
            {
                unit.MarkDead();
            }
            else
            {
                BattleUnitCombatResourceValues values =
                    unit.GetCombatResourcesReadViewTyped().Values;
                unit.RestoreCombatResourcesForMutationSnapshotExact(
                    BattleUnitCombatResourceSnapshot.Present(
                        values with { IsAlive = isAlive.Value }
                    )
                );
            }
        }
        return unit;
    }

    internal static BattleUnitState WithCombatResourcesForTestExact(
        this BattleUnitState unit,
        int? hp = null,
        int? mp = null,
        int? stamina = null,
        int? aura = null,
        int? ap = null,
        int? movePoints = null,
        int? staminaRecoveryProgress = null,
        bool? isAlive = null
    )
    {
        ArgumentNullException.ThrowIfNull(unit);

        BattleUnitCombatResourceValues values =
            unit.CaptureCombatResourcesForMutationSnapshotExact().Values;
        unit.RestoreCombatResourcesForMutationSnapshotExact(
            BattleUnitCombatResourceSnapshot.Present(
                values with
                {
                    Hp = hp ?? values.Hp,
                    Mp = mp ?? values.Mp,
                    Stamina = stamina ?? values.Stamina,
                    Aura = aura ?? values.Aura,
                    Ap = ap ?? values.Ap,
                    MovePoints = movePoints ?? values.MovePoints,
                    StaminaRecoveryProgress =
                        staminaRecoveryProgress
                        ?? values.StaminaRecoveryProgress,
                    IsAlive = isAlive ?? values.IsAlive,
                }
            )
        );
        return unit;
    }
}
