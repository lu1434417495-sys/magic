using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

public partial class run_battle_panel_full_refresh_benchmark : SceneTree
{
    private static readonly PackedScene BattlePanelScene = GD.Load<PackedScene>(
        "res://scenes/ui/battle_map_panel.tscn"
    );

    private static readonly Vector2I ViewportSize = new(1600, 900);
    private static readonly Vector2I MapSize = new(20, 14);
    private const int AllyCount = 6;
    private const int EnemyCount = 40;
    private const int Iterations = 24;
    private const int MaxReadyFrames = 24;

    private readonly TestHarness _test = new();
    private readonly BattleGridService _gridService = new();

    public override async void _Initialize()
    {
        Root.Size = ViewportSize;
        BattleMapPanel panel = BattlePanelScene.Instantiate<BattleMapPanel>();
        if (panel == null)
        {
            GD.PushError("BattlePanelRefreshBenchmark could not instantiate BattleMapPanel.");
            Quit(1);
            return;
        }

        Root.AddChild(panel);
        await ProcessFrames(1);
        panel.Size = ViewportSize;
        panel.Visible = true;

        BattleState state = BuildFlatState(MapSize);
        PopulateUnits(state);
        List<Vector2I> selectedCycle = BuildSelectedCycle(state);
        GVector2IArray validTargetCoords = CollectAllCoords(state);
        if (selectedCycle.Count == 0)
            _test.Fail("BattlePanelRefreshBenchmark could not build a selected cycle.");

        if (_test.Failures.Count == 0)
        {
            panel.Refresh(
                state,
                selectedCycle[0],
                "",
                "",
                "",
                new GVector2IArray(),
                validTargetCoords,
                0,
                new GStringNameArray(),
                ""
            );
            if (!await WaitForPanelRenderReady(panel))
                _test.Fail("BattlePanelRefreshBenchmark did not reach render-ready state before timing.");
        }

        GDictionary fullRefresh = await RunPanelPass(
            "full_refresh",
            panel,
            state,
            selectedCycle,
            validTargetCoords,
            true
        );
        GDictionary overlayOnly = await RunPanelPass(
            "overlay_only",
            panel,
            state,
            selectedCycle,
            validTargetCoords,
            false
        );

        panel.QueueFree();
        await ProcessFrames(1);

        if (_test.Failures.Count > 0)
        {
            Quit(_test.Finish("Battle panel full refresh benchmark"));
            return;
        }

        GD.Print(FormatResult(fullRefresh));
        GD.Print(FormatResult(overlayOnly));
        GD.Print(FormatComparison(fullRefresh, overlayOnly));
        Quit(_test.Finish("Battle panel full refresh benchmark"));
    }

    private async Task<GDictionary> RunPanelPass(
        string passId,
        BattleMapPanel panel,
        BattleState state,
        List<Vector2I> selectedCycle,
        GVector2IArray validTargetCoords,
        bool redrawBoard
    )
    {
        if (!panel.IsBattleRenderContentReady() && !await WaitForPanelRenderReady(panel))
        {
            _test.Fail($"BattlePanelRefreshBenchmark pass {passId} started before render content became ready.");
            return EmptyResult(passId, redrawBoard, validTargetCoords.Count, selectedCycle.Count);
        }

        ulong callUsec = 0;
        ulong frameUsec = 0;
        for (int iteration = 0; iteration < Iterations; iteration++)
        {
            Vector2I selectedCoord = selectedCycle[iteration % selectedCycle.Count];
            ulong callStart = Time.GetTicksUsec();
            if (redrawBoard)
            {
                panel.Refresh(
                    state,
                    selectedCoord,
                    "",
                    "",
                    "",
                    new GVector2IArray(),
                    validTargetCoords,
                    0,
                    new GStringNameArray(),
                    ""
                );
            }
            else
            {
                panel.RefreshOverlay(
                    state,
                    selectedCoord,
                    "",
                    "",
                    "",
                    new GVector2IArray(),
                    validTargetCoords,
                    0,
                    new GStringNameArray(),
                    ""
                );
            }
            callUsec += Time.GetTicksUsec() - callStart;

            ulong frameStart = Time.GetTicksUsec();
            bool ready = await WaitForPanelRenderReady(panel);
            frameUsec += Time.GetTicksUsec() - frameStart;
            if (!ready)
            {
                _test.Fail(
                    $"BattlePanelRefreshBenchmark pass {passId} iteration {iteration} did not regain render-ready state."
                );
                break;
            }
        }

        return new GDictionary
        {
            ["pass_id"] = passId,
            ["iterations"] = Iterations,
            ["redraw_board"] = redrawBoard,
            ["call_usec"] = (long)callUsec,
            ["frame_usec"] = (long)frameUsec,
            ["total_usec"] = (long)(callUsec + frameUsec),
            ["valid_coord_count"] = validTargetCoords.Count,
            ["selected_cycle_count"] = selectedCycle.Count,
        };
    }

    private static GDictionary EmptyResult(
        string passId,
        bool redrawBoard,
        int validCoordCount,
        int selectedCycleCount
    ) =>
        new()
        {
            ["pass_id"] = passId,
            ["iterations"] = 0,
            ["redraw_board"] = redrawBoard,
            ["call_usec"] = 0,
            ["frame_usec"] = 0,
            ["total_usec"] = 0,
            ["valid_coord_count"] = validCoordCount,
            ["selected_cycle_count"] = selectedCycleCount,
        };

    private async Task<bool> WaitForPanelRenderReady(BattleMapPanel panel)
    {
        if (panel == null)
            return false;
        if (panel.IsBattleRenderContentReady())
            return true;
        for (int frame = 0; frame < MaxReadyFrames; frame++)
        {
            await ToSignal(this, SceneTree.SignalName.ProcessFrame);
            if (panel.IsBattleRenderContentReady())
                return true;
        }
        return false;
    }

    private BattleState BuildFlatState(Vector2I mapSize)
    {
        var state = new BattleState
        {
            battle_id = "battle_panel_full_refresh_benchmark",
            phase = "unit_acting",
            terrain_profile_id = "default",
            map_size = mapSize,
            timeline = new BattleTimelineState { current_tu = 120 },
            ally_unit_ids = new GStringNameArray(),
            enemy_unit_ids = new GStringNameArray(),
        };
        state.log_entries.Add("刷新压测：初始化战场。");
        state.log_entries.Add("刷新压测：准备执行 BattleMapPanel.Refresh().");
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                var cell = new BattleCellState
                {
                    coord = new Vector2I(x, y),
                    base_terrain = "land",
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

    private void PopulateUnits(BattleState state)
    {
        Vector2I[] allyPositions =
        {
            new(1, 4),
            new(1, 6),
            new(1, 8),
            new(2, 5),
            new(2, 7),
            new(2, 9),
        };
        for (int index = 0; index < AllyCount; index++)
        {
            BattleUnitState ally = BuildManualUnit(
                new StringName($"panel_ally_{index + 1:00}"),
                $"面板友军{index + 1:00}",
                allyPositions[index]
            );
            AddUnitToState(state, ally, false);
            if (index == 0)
                state.active_unit_id = ally.unit_id;
        }

        var enemyPositions = new List<Vector2I>();
        for (int y = 4; y < 9; y++)
        {
            for (int x = 10; x < 18; x++)
                enemyPositions.Add(new Vector2I(x, y));
        }
        enemyPositions.Sort((left, right) => left.Y == right.Y ? left.X.CompareTo(right.X) : left.Y.CompareTo(right.Y));

        for (int index = 0; index < EnemyCount; index++)
        {
            BattleUnitState enemy = BuildAiUnit(
                new StringName($"panel_enemy_{index + 1:00}"),
                $"面板敌军{index + 1:00}",
                enemyPositions[index]
            );
            AddUnitToState(state, enemy, true);
        }
    }

    private static BattleUnitState BuildManualUnit(
        StringName unitId,
        string displayName,
        Vector2I coord
    )
    {
        BattleUnitState unit = BuildUnit(unitId, displayName, "player", "manual", 280, 60, 60, 20);
        unit.SetAnchorCoord(coord);
        unit.known_active_skill_ids.Add("warrior_heavy_strike");
        unit.known_active_skill_ids.Add("warrior_guard");
        unit.known_active_skill_ids.Add("charge");
        foreach (StringName skillId in unit.known_active_skill_ids)
            unit.known_skill_level_map[skillId] = 1;
        return unit;
    }

    private static BattleUnitState BuildAiUnit(StringName unitId, string displayName, Vector2I coord)
    {
        BattleUnitState unit = BuildUnit(unitId, displayName, "enemy", "ai", 180, 80, 80, 20);
        unit.SetAnchorCoord(coord);
        unit.known_active_skill_ids.Add("archer_suppressive_fire");
        unit.known_active_skill_ids.Add("archer_pinning_shot");
        foreach (StringName skillId in unit.known_active_skill_ids)
            unit.known_skill_level_map[skillId] = 1;
        return unit;
    }

    private static BattleUnitState BuildUnit(
        StringName unitId,
        string displayName,
        StringName factionId,
        StringName controlMode,
        int hp,
        int mp,
        int stamina,
        int aura
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = displayName,
            faction_id = factionId,
            control_mode = controlMode,
            current_hp = hp,
            current_mp = mp,
            current_stamina = stamina,
            current_aura = aura,
            current_ap = 2,
            is_alive = true,
        };
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), hp);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.MpMax), mp);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.StaminaMax), stamina);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AuraMax), aura);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ActionPoints), 2);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), 16);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 18);
        return unit;
    }

    private void AddUnitToState(BattleState state, BattleUnitState unit, bool enemy)
    {
        state.SetUnit(unit);
        if (enemy)
            state.enemy_unit_ids.Add(unit.unit_id);
        else
            state.ally_unit_ids.Add(unit.unit_id);
        bool placed = _gridService.PlaceUnit(state, unit, unit.coord, true);
        if (!placed)
            _test.Fail($"BattlePanelRefreshBenchmark could not place unit {unit.unit_id}.");
    }

    private static List<Vector2I> BuildSelectedCycle(BattleState state)
    {
        var cycle = new List<Vector2I>();
        foreach (StringName unitId in state.ally_unit_ids)
        {
            BattleUnitState unit = state.GetUnit(unitId);
            if (unit != null)
                cycle.Add(unit.coord);
        }
        int enemyLimit = Mathf.Min(6, state.enemy_unit_ids.Count);
        for (int index = 0; index < enemyLimit; index++)
        {
            StringName unitId = state.enemy_unit_ids[index];
            BattleUnitState unit = state.GetUnit(unitId);
            if (unit != null)
                cycle.Add(unit.coord);
        }
        return cycle;
    }

    private static GVector2IArray CollectAllCoords(BattleState state)
    {
        var coords = new GVector2IArray();
        for (int y = 0; y < state.map_size.Y; y++)
        {
            for (int x = 0; x < state.map_size.X; x++)
                coords.Add(new Vector2I(x, y));
        }
        return coords;
    }

    private static string FormatResult(GDictionary result)
    {
        double totalUsec = DictLong(result, "total_usec");
        int iterations = Mathf.Max(DictInt(result, "iterations"), 1);
        return $"[BattlePanelRefreshBenchmark] pass={DictString(result, "pass_id")} iterations={iterations} "
            + $"total_ms={UsecToMsec(totalUsec):F3} call_ms={UsecToMsec(DictLong(result, "call_usec")):F3} "
            + $"frame_ms={UsecToMsec(DictLong(result, "frame_usec")):F3} ms_per_refresh={UsecToMsec(totalUsec) / iterations:F3} "
            + $"valid_coords={DictInt(result, "valid_coord_count")} selected_cycle={DictInt(result, "selected_cycle_count")}";
    }

    private static string FormatComparison(GDictionary fullRefresh, GDictionary overlayOnly)
    {
        long redrawDeltaUsec = System.Math.Max(
            DictLong(fullRefresh, "total_usec") - DictLong(overlayOnly, "total_usec"),
            0
        );
        int iterations = Mathf.Max(DictInt(fullRefresh, "iterations"), 1);
        return $"[BattlePanelRefreshBenchmark] comparison redraw_delta_ms={UsecToMsec(redrawDeltaUsec):F3} "
            + $"redraw_delta_ms_per_refresh={UsecToMsec(redrawDeltaUsec) / iterations:F3} "
            + $"full_refresh_ms_per_iter={UsecToMsec(DictLong(fullRefresh, "total_usec")) / iterations:F3} "
            + $"overlay_ms_per_iter={UsecToMsec(DictLong(overlayOnly, "total_usec")) / iterations:F3}";
    }

    private async Task ProcessFrames(int count)
    {
        for (int i = 0; i < count; i++)
            await ToSignal(this, SceneTree.SignalName.ProcessFrame);
    }

    private static double UsecToMsec(double valueUsec) => valueUsec / 1000.0;

    private static int DictInt(GDictionary dict, Variant key) =>
        dict != null && dict.ContainsKey(key) ? (int)dict[key].AsInt64() : 0;

    private static long DictLong(GDictionary dict, Variant key) =>
        dict != null && dict.ContainsKey(key) ? dict[key].AsInt64() : 0L;

    private static string DictString(GDictionary dict, Variant key) =>
        dict != null && dict.ContainsKey(key) ? dict[key].AsString() : "";
}
