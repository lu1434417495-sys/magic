using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_world_map_save_transaction_regression : LifecycleTestSceneTree
{
    private const string TestWorldConfig = "res://data/configs/world_map/test_world_map_config.tres";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestPlainWorldMoveStagesWithoutDiskWrite();
        TestResourceHarvestRollbackOnPersistFailure();

        RequestTestExit(_test.Finish("World map save transaction regression"));
    }

    private void TestPlainWorldMoveStagesWithoutDiskWrite()
    {
        Vector2I[] directions =
        {
            Vector2I.Right,
            Vector2I.Left,
            Vector2I.Down,
            Vector2I.Up,
        };

        foreach (Vector2I direction in directions)
        {
            RuntimeContext context = CreateRuntimeContext();
            if (context == null)
            {
                return;
            }

            try
            {
                Dictionary<string, object> originalPayload =
                    ReadActiveSavePayload(context.GameSession);
                Vector2I originalCoord = PayloadPlayerCoord(originalPayload);
                RuntimeCommandResult result =
                    context.Facade.CommandWorldMoveTyped(direction, 1);
                bool movedWithoutBoundary =
                    result.Ok
                    && context.Facade.GetPlayerCoord() != originalCoord
                    && context.Facade.GetActiveModalId() == ""
                    && !context.Facade.IsBattleActive();
                if (!movedWithoutBoundary)
                {
                    continue;
                }

                _test.True(context.GameSession.HasPendingSave(), "普通大地图移动后应只标记 pending save。");
                Dictionary<string, object> diskPayload =
                    ReadActiveSavePayload(context.GameSession);
                _test.Eq(
                    PayloadPlayerCoord(diskPayload),
                    originalCoord,
                    "普通大地图移动不应逐步写入磁盘坐标。"
                );
                return;
            }
            finally
            {
                context.Dispose();
            }
        }

        _test.Fail("测试地图应至少存在一个不会打开窗口或战斗的相邻可移动格。");
    }

    private void TestResourceHarvestRollbackOnPersistFailure()
    {
        RuntimeContext context = CreateRuntimeContext();
        if (context == null)
        {
            return;
        }

        try
        {
            WorldMapResourceNodeData node = FindHarvestableResourceNode(context.Facade);
            _test.True(node != null, "资源采集回滚测试前置：测试地图应存在可采集资源点。");
            if (node == null)
            {
                return;
            }

            Vector2I coord = node.WorldCoord;
            StringName itemId = node.YieldItemId;
            int warehouseCountBefore = context.Facade
                .GetPartyWarehouseService()
                .CountItem(itemId);
            int remainingChargesBefore = node.RemainingCharges;
            bool hadPendingSaveBefore = context.GameSession.HasPendingSave();
            WorldMapResourceNodeData sessionNodeBefore = FindSessionResourceNode(
                context.GameSession,
                coord
            );
            _test.Eq(
                sessionNodeBefore?.RemainingCharges ?? -1,
                remainingChargesBefore,
                "资源采集回滚测试前置：session world 应与 active world 一致。"
            );

            context.Facade._pending_harvest_coord = coord;
            context.Facade._active_modal_kind = RuntimeModalKind.ResourceHarvestConfirm;
            context.GameSession.fail_payload_write = true;

            RuntimeCommandResult result =
                context.Facade.CommandConfirmResourceHarvestTyped();

            _test.False(result.Ok, "资源采集提交失败时命令应返回失败。");
            _test.True(
                result.Message.Contains("操作已回滚", StringComparison.Ordinal),
                "资源采集提交失败时应明确报告操作已回滚。"
            );
            _test.False(
                result.Message.Contains("已采集", StringComparison.Ordinal),
                "资源采集提交失败时不应继续报告采集成功。"
            );
            _test.Eq(
                context.Facade.GetPartyWarehouseService().CountItem(itemId),
                warehouseCountBefore,
                "资源采集提交失败后仓库物品数量应恢复。"
            );
            _test.True(
                ReferenceEquals(
                    context.Facade.GetPartyState(),
                    context.GameSession.GetPartyState()
                ),
                "资源采集回滚后 runtime 应重新绑定 session 的 canonical party state。"
            );

            WorldMapResourceNodeData activeNode =
                context.Facade._world_map_data_context.GetResourceNodeAt(coord);
            _test.True(activeNode != null, "资源采集提交失败后 active world 资源点不应丢失。");
            _test.Eq(
                activeNode?.RemainingCharges ?? -1,
                remainingChargesBefore,
                "资源采集提交失败后 active world 的剩余次数应恢复。"
            );

            WorldMapResourceNodeData rootNode = FindResourceNode(
                context.Facade._world_map_data_context.RootRuntimeData,
                coord
            );
            _test.True(rootNode != null, "资源采集提交失败后 root world 资源点不应丢失。");
            _test.Eq(
                rootNode?.RemainingCharges ?? -1,
                remainingChargesBefore,
                "资源采集提交失败后 root world 的剩余次数应恢复。"
            );

            WorldMapResourceNodeData sessionNodeAfter = FindSessionResourceNode(
                context.GameSession,
                coord
            );
            _test.True(sessionNodeAfter != null, "资源采集提交失败后 session world 资源点不应丢失。");
            _test.Eq(
                sessionNodeAfter?.RemainingCharges ?? -1,
                remainingChargesBefore,
                "资源采集提交失败后 session world 的剩余次数应恢复。"
            );
            _test.Eq(
                context.GameSession.HasPendingSave(),
                hadPendingSaveBefore,
                "资源采集提交失败后 session 保存状态应恢复到命令前。"
            );
        }
        finally
        {
            context.GameSession.fail_payload_write = false;
            context.Dispose();
        }
    }

    private static WorldMapResourceNodeData FindHarvestableResourceNode(
        GameRuntimeFacade facade
    )
    {
        foreach (
            WorldMapResourceNodeData node in facade._world_map_data_context.GetActiveResourceNodes()
        )
        {
            if (node != null && node.Exists && node.RemainingCharges > 0)
                return node;
        }
        return null;
    }

    private static WorldMapResourceNodeData FindResourceNode(
        WorldRuntimeData worldData,
        Vector2I coord
    )
    {
        if (worldData == null)
            return null;
        foreach (WorldMapResourceNodeData node in worldData.ResourceNodes)
        {
            if (node != null && node.Exists && node.WorldCoord == coord)
                return node;
        }
        return null;
    }

    private static WorldMapResourceNodeData FindSessionResourceNode(
        GameSession gameSession,
        Vector2I coord
    )
    {
        using GodotProjectionLease<GDictionary> worldDataLease =
            gameSession.GetWorldDataLease();
        return FindResourceNode(WorldRuntimeData.FromDictionary(worldDataLease.Value), coord);
    }

    private RuntimeContext CreateRuntimeContext()
    {
        GameSession gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        int createError = gameSession.CreateNewSave(TestWorldConfig);
        _test.Eq(createError, (int)Error.Ok, "大地图保存事务回归前置：应能创建测试存档。");
        if (createError != (int)Error.Ok)
        {
            CleanupGameSession(gameSession);
            return null;
        }

        GameRuntimeFacade facade = new();
        facade.Setup(gameSession);
        return new RuntimeContext(gameSession, facade);
    }

    private static Dictionary<string, object> ReadActiveSavePayload(
        GameSession gameSession
    )
    {
        string savePath = gameSession.GetActiveSavePath();
        if (string.IsNullOrEmpty(savePath))
        {
            return new Dictionary<string, object>(StringComparer.Ordinal);
        }

        int readError = gameSession.ReadSavePayload(
            savePath,
            out Dictionary<string, object> payload,
            false
        );
        return readError == (int)Error.Ok
            ? payload
            : new Dictionary<string, object>(StringComparer.Ordinal);
    }

    private static Vector2I PayloadPlayerCoord(
        IReadOnlyDictionary<string, object> payload
    )
    {
        if (
            payload == null
            || !payload.TryGetValue("world_state", out object worldStateValue)
            || worldStateValue is not IReadOnlyDictionary<string, object> worldState
        )
        {
            return Vector2I.Zero;
        }
        return worldState.TryGetValue("player_coord", out object coordValue)
            && coordValue is Vector2I coord
            ? coord
            : Vector2I.Zero;
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
}
