using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_world_map_settlement_entry_regression : LifecycleTestSceneTree
{
    private const string TestWorldConfig = "res://data/configs/world_map/test_world_map_config.tres";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestEnteringSettlementHidesPlayerUntilClose();

        RequestTestExit(_test.Finish("World map settlement entry regression"));
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
            _test.True(probe != null, "测试世界中应至少存在一组可从外格踏入的据点入口。");
            if (probe == null)
            {
                return;
            }

            Vector2I direction = probe.TargetCoord - probe.SourceCoord;
            context.Facade.SetPlayerCoord(probe.SourceCoord);
            context.Facade.SetSelectedCoord(probe.SourceCoord);
            context.Facade.RefreshWorldVisibility();
            context.GameSession.SetPlayerCoord(probe.SourceCoord);

            GameRuntimeFacade.RuntimeCommandResult moveResult =
                context.Facade.CommandWorldMoveTyped(direction, 1);
            _test.True(moveResult.Ok, "从外格踏入据点占格时应成功打开据点。");
            _test.Eq(context.Facade.GetActiveModalId(), "settlement", "踏入据点占格后应自动进入 settlement modal。");
            _test.Eq(context.Facade.GetActiveSettlementId(), probe.SettlementId, "自动打开的 settlement 应指向目标据点。");
            _test.Eq(context.Facade.GetPlayerCoord(), probe.SourceCoord, "据点窗口打开时玩家逻辑坐标应保留在进入前格子。");
            _test.Eq(context.Facade.GetSelectedCoord(), probe.TargetCoord, "据点窗口打开时选中格应保持在目标据点格。");
            _test.False(context.Facade.IsPlayerVisibleOnWorldMap(), "据点窗口打开时世界地图上不应绘制玩家。");

            using GodotProjectionLease<GDictionary> openSnapshotLease =
                context.Facade.BuildHeadlessSnapshotLease();
            GDictionary openSnapshot = openSnapshotLease.Value;
            GDictionary worldSnapshot = Dict(openSnapshot, "world");
            _test.False(DictBool(worldSnapshot, "player_visible_on_map", true), "据点窗口打开时 world snapshot 应暴露隐藏玩家状态。");
            _test.True(
                CoordDictEquals(Dict(worldSnapshot, "player_coord"), probe.SourceCoord),
                "据点窗口打开时快照中的 player_coord 应保持在进入前格子。"
            );

            GameRuntimeFacade.RuntimeCommandResult closeResult =
                context.Facade.CommandCloseActiveModalTyped();
            _test.True(closeResult.Ok, "关闭据点窗口应成功返回世界地图。");
            _test.Eq(context.Facade.GetActiveModalId(), "", "关闭据点窗口后不应残留 modal。");
            _test.Eq(context.Facade.GetPlayerCoord(), probe.SourceCoord, "关闭据点窗口后玩家应出现在进入前格子。");
            _test.Eq(context.Facade.GetSelectedCoord(), probe.SourceCoord, "关闭据点窗口后选中格应回到玩家当前格。");
            _test.True(context.Facade.IsPlayerVisibleOnWorldMap(), "关闭据点窗口后世界地图上应重新显示玩家。");
        }
        finally
        {
            context.Dispose();
        }
    }

    private RuntimeContext CreateRuntimeContext()
    {
        GameSession gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        int createError = gameSession.CreateNewSave(TestWorldConfig);
        _test.Eq(createError, (int)Error.Ok, "测试世界应能成功创建新存档。");
        if (createError != (int)Error.Ok)
        {
            CleanupGameSession(gameSession);
            return null;
        }

        GameRuntimeFacade facade = new();
        facade.Setup(gameSession);
        return new RuntimeContext(gameSession, facade);
    }

    private static SettlementEntryProbe FindAdjacentSettlementProbe(GameRuntimeFacade facade)
    {
        using GodotProjectionLease<GDictionary> worldDataLease = facade.GetWorldDataLease();
        GArray settlements = ArrayValue(worldDataLease.Value, "settlements");
        WorldMapGridSystem gridSystem = facade.GetGridSystem();
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
            && gridSystem.IsCellInsideWorld(sourceCoord)
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
        gameSession.ClearPersistedGame();
        gameSession.Free();
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
            Facade?.Dispose();
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
