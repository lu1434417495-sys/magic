using System;
using System.Collections.Generic;
using Godot;
using GCombatEffectArray = Godot.Collections.Array<CombatEffectDef>;
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
        params GodotObject[] ownedObjects
    )
    {
        if (runtime != null)
            runtime.SetupStateForTests(null);

        if (ownedObjects != null)
        {
            foreach (GodotObject ownedObject in ownedObjects)
                DisposeFixtureObject(ownedObject);
        }
        DisposeBattleState(state);

        DisposeRuntime(runtime);
    }

    public static void DisposeBattleState(BattleState state)
    {
        if (state == null)
            return;
        if (!GodotObject.IsInstanceValid(state))
        {
            GodotSharpCleanup.DisposeGodotObject(state);
            return;
        }

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
        GodotSharpCleanup.DisposeGodotObject(state.timeline);
        GodotSharpCleanup.DisposeGodotObject(state.party_backpack_view);
        GodotSharpCleanup.DisposeGodotObject(state);
    }

    public static void DisposeFixtureObject(GodotObject ownedObject)
    {
        switch (ownedObject)
        {
            case null:
                return;
            case BattlePreview preview:
                DisposeBattlePreview(preview);
                return;
            case BattleUnitState unit:
                DisposeBattleUnit(unit);
                return;
            case BattleCellState cell:
                DisposeBattleCell(cell);
                return;
            case SkillDef skill:
                DisposeSkill(skill);
                return;
            default:
                GodotSharpCleanup.DisposeGodotObject(ownedObject);
                return;
        }
    }

    public static void DisposeBattleAiScoreInput(BattleAiScoreInput scoreInput)
    {
        if (scoreInput == null)
            return;
        DisposeBattlePreview(scoreInput.preview);
        GodotSharpCleanup.DisposeGodotObject(scoreInput.command);
        scoreInput.preview = null;
        scoreInput.command = null;
        scoreInput.skill_def = null;
    }

    public static void DisposeBattlePreview(BattlePreview preview)
    {
        if (preview == null)
            return;
        if (!GodotObject.IsInstanceValid(preview))
        {
            GodotSharpCleanup.DisposeGodotObject(preview);
            return;
        }
        GodotSharpCleanup.DisposeGodotObject(preview.hit_preview);
        GodotSharpCleanup.DisposeGodotObject(preview);
    }

    public static void DisposeBattleUnit(BattleUnitState unit)
    {
        if (unit == null)
            return;
        if (!GodotObject.IsInstanceValid(unit))
        {
            GodotSharpCleanup.DisposeGodotObject(unit);
            return;
        }

        foreach (BattleStatusEffectState statusEffect in unit.GetStatusEffectsTyped())
            GodotSharpCleanup.DisposeGodotObject(statusEffect);
        GodotSharpCleanup.DisposeGodotObject(unit.ai_blackboard);
        GodotSharpCleanup.DisposeGodotObject(unit.attribute_snapshot);
        GodotSharpCleanup.DisposeGodotObject(unit.equipment_view);
        GodotSharpCleanup.DisposeGodotObject(unit);
    }

    public static void DisposeBattleCell(BattleCellState cell)
    {
        if (cell == null)
            return;
        if (!GodotObject.IsInstanceValid(cell))
        {
            GodotSharpCleanup.DisposeGodotObject(cell);
            return;
        }

        foreach (BattleTerrainEffectState timedEffect in cell.timed_terrain_effects)
            GodotSharpCleanup.DisposeGodotObject(timedEffect);
        GodotSharpCleanup.DisposeGodotObject(cell.edge_feature_east);
        GodotSharpCleanup.DisposeGodotObject(cell.edge_feature_south);
        GodotSharpCleanup.DisposeGodotObject(cell);
    }

    public static void DisposeSkill(SkillDef skill)
    {
        if (skill == null)
            return;
        if (GodotObject.IsInstanceValid(skill))
            GodotSharpCleanup.DisposeGodotObject(skill.combat_profile);
        GodotSharpCleanup.DisposeGodotObject(skill);
    }

    public static void DisposeEffectDefs(GCombatEffectArray effectDefs)
    {
        if (effectDefs == null)
            return;
        foreach (CombatEffectDef effectDef in effectDefs)
            GodotSharpCleanup.DisposeGodotObject(effectDef);
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
