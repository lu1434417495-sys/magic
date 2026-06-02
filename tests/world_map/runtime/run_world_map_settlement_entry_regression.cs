using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_world_map_settlement_entry_regression : SceneTree
{
    private const string TestWorldConfig = "res://data/configs/world_map/test_world_map_config.tres";

    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestEnteringSettlementHidesPlayerUntilClose();

        if (_failures.Count == 0)
        {
            GD.Print("World map settlement entry regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"World map settlement entry regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestEnteringSettlementHidesPlayerUntilClose()
    {
        RuntimeContext context = CreateRuntimeContext();
        if (context == null)
        {
            return;
        }

        try
        {
            SettlementEntryProbe probe = FindAdjacentSettlementProbe(context.Facade);
            AssertTrue(probe != null, "测试世界中应至少存在一组可从外格踏入的据点入口。");
            if (probe == null)
            {
                return;
            }

            Vector2I direction = probe.TargetCoord - probe.SourceCoord;
            context.Facade.set_player_coord(probe.SourceCoord);
            context.Facade.set_selected_coord(probe.SourceCoord);
            context.Facade.refresh_world_visibility();
            context.GameSession.set_player_coord(probe.SourceCoord);

            GDictionary moveResult = context.Facade.command_world_move(direction, 1);
            AssertTrue(DictBool(moveResult, "ok", false), "从外格踏入据点占格时应成功打开据点。");
            AssertEq(context.Facade.get_active_modal_id(), "settlement", "踏入据点占格后应自动进入 settlement modal。");
            AssertEq(context.Facade.get_active_settlement_id(), probe.SettlementId, "自动打开的 settlement 应指向目标据点。");
            AssertEq(context.Facade.get_player_coord(), probe.SourceCoord, "据点窗口打开时玩家逻辑坐标应保留在进入前格子。");
            AssertEq(context.Facade.get_selected_coord(), probe.TargetCoord, "据点窗口打开时选中格应保持在目标据点格。");
            AssertFalse(context.Facade.is_player_visible_on_world_map(), "据点窗口打开时世界地图上不应绘制玩家。");

            GDictionary openSnapshot = context.Facade.build_headless_snapshot();
            GDictionary worldSnapshot = Dict(openSnapshot, "world");
            AssertFalse(DictBool(worldSnapshot, "player_visible_on_map", true), "据点窗口打开时 world snapshot 应暴露隐藏玩家状态。");
            AssertTrue(
                CoordDictEquals(Dict(worldSnapshot, "player_coord"), probe.SourceCoord),
                "据点窗口打开时快照中的 player_coord 应保持在进入前格子。"
            );

            GDictionary closeResult = context.Facade.command_close_active_modal();
            AssertTrue(DictBool(closeResult, "ok", false), "关闭据点窗口应成功返回世界地图。");
            AssertEq(context.Facade.get_active_modal_id(), "", "关闭据点窗口后不应残留 modal。");
            AssertEq(context.Facade.get_player_coord(), probe.SourceCoord, "关闭据点窗口后玩家应出现在进入前格子。");
            AssertEq(context.Facade.get_selected_coord(), probe.SourceCoord, "关闭据点窗口后选中格应回到玩家当前格。");
            AssertTrue(context.Facade.is_player_visible_on_world_map(), "关闭据点窗口后世界地图上应重新显示玩家。");
        }
        finally
        {
            context.Dispose();
        }
    }

    private RuntimeContext CreateRuntimeContext()
    {
        GameSession gameSession = new();
        int createError = gameSession.create_new_save(TestWorldConfig);
        AssertEq(createError, (int)Error.Ok, "测试世界应能成功创建新存档。");
        if (createError != (int)Error.Ok)
        {
            CleanupGameSession(gameSession);
            return null;
        }

        GameRuntimeFacade facade = new();
        facade.setup(gameSession);
        return new RuntimeContext(gameSession, facade);
    }

    private static SettlementEntryProbe FindAdjacentSettlementProbe(GameRuntimeFacade facade)
    {
        GArray settlements = ArrayValue(facade.get_world_data(), "settlements");
        WorldMapGridSystem gridSystem = facade.get_grid_system();
        foreach (Variant settlementValue in settlements)
        {
            if (settlementValue.VariantType != Variant.Type.Dictionary)
            {
                continue;
            }
            GDictionary settlement = settlementValue.AsGodotDictionary();
            Vector2I origin = Vector2IValue(settlement, "origin", Vector2I.Zero);
            Vector2I size = Vector2IValue(settlement, "footprint_size", Vector2I.One);
            for (int offsetY = 0; offsetY < size.Y; offsetY++)
            {
                Vector2I candidateSource = origin + new Vector2I(-1, offsetY);
                if (IsValidEntryProbe(settlements, gridSystem, candidateSource))
                {
                    return new SettlementEntryProbe(
                        StringValue(settlement, "settlement_id"),
                        candidateSource,
                        origin + new Vector2I(0, offsetY)
                    );
                }
                candidateSource = origin + new Vector2I(size.X, offsetY);
                if (IsValidEntryProbe(settlements, gridSystem, candidateSource))
                {
                    return new SettlementEntryProbe(
                        StringValue(settlement, "settlement_id"),
                        candidateSource,
                        origin + new Vector2I(size.X - 1, offsetY)
                    );
                }
            }
            for (int offsetX = 0; offsetX < size.X; offsetX++)
            {
                Vector2I topSource = origin + new Vector2I(offsetX, -1);
                if (IsValidEntryProbe(settlements, gridSystem, topSource))
                {
                    return new SettlementEntryProbe(
                        StringValue(settlement, "settlement_id"),
                        topSource,
                        origin + new Vector2I(offsetX, 0)
                    );
                }
                Vector2I bottomSource = origin + new Vector2I(offsetX, size.Y);
                if (IsValidEntryProbe(settlements, gridSystem, bottomSource))
                {
                    return new SettlementEntryProbe(
                        StringValue(settlement, "settlement_id"),
                        bottomSource,
                        origin + new Vector2I(offsetX, size.Y - 1)
                    );
                }
            }
        }
        return null;
    }

    private static bool IsValidEntryProbe(
        GArray settlements,
        WorldMapGridSystem gridSystem,
        Vector2I sourceCoord
    )
    {
        return gridSystem != null
            && gridSystem.is_cell_inside_world(sourceCoord)
            && FindSettlementCoveringCoord(settlements, sourceCoord).Count == 0;
    }

    private static GDictionary FindSettlementCoveringCoord(GArray settlements, Vector2I coord)
    {
        foreach (Variant settlementValue in settlements)
        {
            if (settlementValue.VariantType != Variant.Type.Dictionary)
            {
                continue;
            }
            GDictionary settlement = settlementValue.AsGodotDictionary();
            Vector2I origin = Vector2IValue(settlement, "origin", Vector2I.Zero);
            Vector2I footprintSize = Vector2IValue(settlement, "footprint_size", Vector2I.One);
            if (new Rect2I(origin, footprintSize).HasPoint(coord))
            {
                return settlement;
            }
        }
        return new GDictionary();
    }

    private static GArray ArrayValue(GDictionary dictionary, string key)
    {
        return dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsGodotArray()
            : new GArray();
    }

    private static GDictionary Dict(GDictionary dictionary, string key)
    {
        return dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsGodotDictionary()
            : new GDictionary();
    }

    private static bool DictBool(GDictionary dictionary, string key, bool fallback)
    {
        return dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsBool()
            : fallback;
    }

    private static string StringValue(GDictionary dictionary, string key)
    {
        return dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsString()
            : "";
    }

    private static Vector2I Vector2IValue(GDictionary dictionary, string key, Vector2I fallback)
    {
        return dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsVector2I()
            : fallback;
    }

    private static bool CoordDictEquals(GDictionary coord, Vector2I expected)
    {
        return coord != null
            && coord.ContainsKey("x")
            && coord.ContainsKey("y")
            && coord["x"].AsInt32() == expected.X
            && coord["y"].AsInt32() == expected.Y;
    }

    private static void CleanupGameSession(GameSession gameSession)
    {
        if (gameSession == null)
        {
            return;
        }
        gameSession.clear_persisted_game();
        gameSession.Free();
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
        if (condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
        {
            _failures.Add($"{message} | actual={actual} expected={expected}");
        }
    }

    private sealed class RuntimeContext
    {
        public RuntimeContext(GameSession gameSession, GameRuntimeFacade facade)
        {
            GameSession = gameSession;
            Facade = facade;
        }

        public GameSession GameSession { get; }

        public GameRuntimeFacade Facade { get; }

        public void Dispose()
        {
            Facade?.dispose();
            CleanupGameSession(GameSession);
        }
    }

    private sealed class SettlementEntryProbe
    {
        public SettlementEntryProbe(string settlementId, Vector2I sourceCoord, Vector2I targetCoord)
        {
            SettlementId = settlementId;
            SourceCoord = sourceCoord;
            TargetCoord = targetCoord;
        }

        public string SettlementId { get; }

        public Vector2I SourceCoord { get; }

        public Vector2I TargetCoord { get; }
    }
}
