using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_game_runtime_pending_battle_request_regression : LifecycleTestSceneTree
{
    private const string TestConfigPath =
        "res://data/configs/world_map/test_world_map_config.tres";
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestMissingObjectiveFailsWithoutPendingRequest();
        TestInvalidObjectiveBindingFailsWithoutPendingRequest();
        TestPlacementExhaustionFailsWithoutPendingRequest();
        TestPendingTerrainOverridesEarlierEscapeBindingFailure();
        TestPendingBattleGenerationRequestUsesTypedState();
        TestDeferredTerminalFailureFlushesUnlockedState();

        RequestTestExit(_test.Finish("Game runtime pending battle request regression"));
    }

    private void TestMissingObjectiveFailsWithoutPendingRequest()
    {
        GameRuntimeFacade runtime = new();
        try
        {
            EncounterAnchorData anchor = new()
            {
                entity_id = "missing_objective_anchor",
                display_name = "Missing Objective",
                encounter_profile_id = "missing_objective",
            };
            using GDictionary context = new();

            StringName startResult = runtime.BeginBattleStart(anchor, 1, context);

            _test.Eq(
                startResult.ToString(),
                "failed",
                "缺少正式目标的遭遇应立即失败。"
            );
            _test.False(
                runtime.HasPendingBattleGenerationRequest(),
                "缺少正式目标不应遗留永久 pending 请求。"
            );
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private void TestInvalidObjectiveBindingFailsWithoutPendingRequest()
    {
        using GameSession gameSession =
            GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        GameRuntimeFacade runtime = new();
        try
        {
            runtime.Setup(gameSession);
            gameSession.SetBattleSaveLock(true);
            EncounterAnchorData anchor = new()
            {
                entity_id = "invalid_boss_binding_anchor",
                display_name = "Invalid Boss Binding",
                encounter_profile_id = "invalid_boss_binding",
            };
            runtime.SetBattleEncounterDefinitionForTests(
                new BattleEncounterDefinition(
                    "invalid_boss_binding",
                    "Invalid Boss Binding",
                    "invalid_boss_binding_roster",
                    new BattleBossObjectiveDefinition("missing_boss_actor"),
                    new BattleEncounterWorldResolutionDefinition(
                        BattleWorldResolutionMode.Clear,
                        BattleWorldResolutionMode.Preserve,
                        BattleWorldResolutionMode.Preserve,
                        0
                    )
                )
            );
            BattleUnitState ally = BuildUnit(
                "invalid_boss_binding_ally",
                "player",
                new Vector2I(0, 0)
            );
            ally.source_member_id = "invalid_boss_binding_member";
            BattleUnitState enemy = BuildUnit(
                "invalid_boss_binding_enemy",
                "hostile",
                new Vector2I(2, 0)
            );
            using GodotProjectionLease<GDictionary> allyLease =
                ally.ToDictionaryLease(
                    LifetimeDomain.Request,
                    "invalid-boss-binding-ally"
                );
            using GodotProjectionLease<GDictionary> enemyLease =
                enemy.ToDictionaryLease(
                    LifetimeDomain.Request,
                    "invalid-boss-binding-enemy"
                );
            using GArray battleParty = new() { allyLease.Value };
            using GArray enemyUnits = new() { enemyLease.Value };
            using GDictionary context = new()
            {
                ["battle_terrain_profile"] = "canyon",
                ["battle_party"] = battleParty,
                ["enemy_units"] = enemyUnits,
            };

            StringName startResult = runtime.BeginBattleStart(anchor, 17, context);

            _test.Eq(
                startResult.ToString(),
                "failed",
                "无法绑定正式首领 actor 的遭遇应作为终端启动失败。"
            );
            _test.False(
                runtime.HasPendingBattleGenerationRequest(),
                "目标绑定失败后不得遗留永久 pending 请求。"
            );
            _test.False(
                gameSession.IsBattleSaveLocked(),
                "目标绑定失败后必须释放 battle save lock。"
            );
            _test.Eq(
                runtime.GetBattleRuntime().GetLastStartFailureSnapshot().Reason,
                "invalid_objective_binding",
                "测试必须命中正式目标绑定失败，而不是更早的启动失败。"
            );
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private void TestPlacementExhaustionFailsWithoutPendingRequest()
    {
        using GameSession gameSession =
            GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        GameRuntimeFacade runtime = new();
        try
        {
            runtime.Setup(gameSession);
            gameSession.SetBattleSaveLock(true);
            EncounterAnchorData anchor = new()
            {
                entity_id = "placement_exhausted_anchor",
                display_name = "Placement Exhausted",
                encounter_profile_id = "placement_exhausted_encounter",
            };
            runtime.SetBattleEncounterDefinitionForTests(
                new BattleEncounterDefinition(
                    "placement_exhausted_encounter",
                    "Placement Exhausted",
                    "placement_exhausted_roster",
                    BattleEliminationObjectiveDefinition.Instance,
                    new BattleEncounterWorldResolutionDefinition(
                        BattleWorldResolutionMode.Clear,
                        BattleWorldResolutionMode.Preserve,
                        BattleWorldResolutionMode.Preserve,
                        0
                    )
                )
            );
            BattleUnitState ally = BuildUnit(
                "placement_exhausted_ally",
                "player",
                new Vector2I(0, 0)
            );
            ally.source_member_id = "placement_exhausted_member";
            BattleUnitState enemy = BuildUnit(
                "placement_exhausted_enemy",
                "hostile",
                new Vector2I(2, 0)
            );
            using GodotProjectionLease<GDictionary> allyLease =
                ally.ToDictionaryLease(
                    LifetimeDomain.Request,
                    "placement-exhausted-ally"
                );
            using GodotProjectionLease<GDictionary> enemyLease =
                enemy.ToDictionaryLease(
                    LifetimeDomain.Request,
                    "placement-exhausted-enemy"
                );
            using GArray battleParty = new() { allyLease.Value };
            using GArray enemyUnits = new() { enemyLease.Value };
            using GDictionary context = new()
            {
                ["battle_terrain_profile"] = "missing_profile",
                ["battle_party"] = battleParty,
                ["enemy_units"] = enemyUnits,
            };

            StringName startResult = runtime.BeginBattleStart(anchor, 777, context);

            _test.Eq(
                startResult.ToString(),
                "failed",
                "同步穷尽地形与布阵尝试后应作为终端启动失败。"
            );
            _test.False(
                runtime.HasPendingBattleGenerationRequest(),
                "同步穷尽布阵后不得遗留永久 pending 请求。"
            );
            _test.False(
                gameSession.IsBattleSaveLocked(),
                "同步穷尽布阵后必须释放 battle save lock。"
            );
            _test.Eq(
                runtime.GetBattleRuntime().GetLastStartFailureSnapshot().Reason,
                "placement_exhausted",
                "不支持的正式地形配置应以 placement_exhausted 结束本次同步尝试。"
            );
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private void TestPendingBattleGenerationRequestUsesTypedState()
    {
        GameRuntimeFacade runtime = new();
        try
        {
            runtime.GetBattleRuntime()._terrain_generator =
                new PendingBattleTerrainGenerator();
            EncounterAnchorData anchor = new()
            {
                entity_id = "pending_anchor",
                display_name = "Pending Anchor",
                world_coord = new Vector2I(2, 3),
                encounter_profile_id = "pending_encounter",
            };
            runtime.SetBattleEncounterDefinitionForTests(
                new BattleEncounterDefinition(
                    "pending_encounter",
                    "Pending Encounter",
                    "pending_roster",
                    BattleEliminationObjectiveDefinition.Instance,
                    new BattleEncounterWorldResolutionDefinition(
                        BattleWorldResolutionMode.Clear,
                        BattleWorldResolutionMode.Preserve,
                        BattleWorldResolutionMode.Preserve,
                        0
                    )
                )
            );
            BattleUnitState ally = BuildUnit("pending_ally", "player", new Vector2I(0, 0));
            BattleUnitState enemy = BuildUnit("pending_enemy", "hostile", new Vector2I(1, 0));
            using GodotProjectionLease<GDictionary> allyLease = ally.ToDictionaryLease(
                LifetimeDomain.Request,
                "pending-battle-request-ally"
            );
            using GodotProjectionLease<GDictionary> enemyLease = enemy.ToDictionaryLease(
                LifetimeDomain.Request,
                "pending-battle-request-enemy"
            );
            using GArray battleParty = new() { allyLease.Value };
            using GArray enemyUnits = new() { enemyLease.Value };
            using GDictionary context = new()
            {
                ["world_coord"] = anchor.world_coord,
                ["custom_flag"] = "original",
                ["battle_terrain_profile"] = "missing_profile",
                ["battle_party"] = battleParty,
                ["enemy_units"] = enemyUnits,
            };

            StringName startResult = runtime.BeginBattleStart(anchor, 777, context);
            _test.Eq(startResult.ToString(), "pending", "Invalid fixture battle should leave generation pending.");
            _test.True(runtime.HasPendingBattleGenerationRequest(), "Runtime should report a typed pending battle generation request.");

            GameRuntimePendingBattleGenerationRequest request =
                runtime.GetPendingBattleGenerationRequestState();
            _test.True(request != null && !request.IsEmpty, "Pending request state should be non-empty.");
            _test.True(ReferenceEquals(request.EncounterAnchor, anchor), "Pending request should retain the typed encounter anchor.");
            _test.Eq(request.Seed, 777, "Pending request should retain the seed.");

            context["custom_flag"] = "mutated";
            Dictionary<string, object> storedContext = request.CloneContextPlain();
            _test.Eq(
                PlainString(storedContext, "custom_flag"),
                "original",
                "Pending request should duplicate the input context."
            );

            storedContext["custom_flag"] = "clone_mutated";
            _test.Eq(
                PlainString(request.CloneContextPlain(), "custom_flag"),
                "original",
                "Pending request should return cloned contexts."
            );

            runtime.ClearPendingBattleGenerationRequest();
            _test.False(runtime.HasPendingBattleGenerationRequest(), "Clearing typed request should clear pending state.");
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private void TestPendingTerrainOverridesEarlierEscapeBindingFailure()
    {
        using GameSession gameSession =
            GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        GameRuntimeFacade runtime = new();
        try
        {
            runtime.Setup(gameSession);
            runtime.GetBattleRuntime()._terrain_generator =
                new InvalidEscapeThenPendingTerrainGenerator();
            gameSession.SetBattleSaveLock(true);
            EncounterAnchorData anchor = new()
            {
                entity_id = "escape_retry_pending_anchor",
                display_name = "Escape Retry Pending",
                encounter_profile_id = "escape_retry_pending",
            };
            runtime.SetBattleEncounterDefinitionForTests(
                new BattleEncounterDefinition(
                    "escape_retry_pending",
                    "Escape Retry Pending",
                    "escape_retry_pending_roster",
                    new BattleEscapeObjectiveDefinition(
                        "east_exit",
                        BattleMapEdge.Right,
                        1
                    ),
                    new BattleEncounterWorldResolutionDefinition(
                        BattleWorldResolutionMode.Preserve,
                        BattleWorldResolutionMode.Preserve,
                        BattleWorldResolutionMode.Preserve,
                        0
                    )
                )
            );
            BattleUnitState ally = BuildUnit(
                "escape_retry_pending_ally",
                "player",
                new Vector2I(0, 0)
            );
            ally.source_member_id = "escape_retry_pending_member";
            BattleUnitState enemy = BuildUnit(
                "escape_retry_pending_enemy",
                "hostile",
                new Vector2I(1, 1)
            );
            using GodotProjectionLease<GDictionary> allyLease =
                ally.ToDictionaryLease(
                    LifetimeDomain.Request,
                    "escape-retry-pending-ally"
                );
            using GodotProjectionLease<GDictionary> enemyLease =
                enemy.ToDictionaryLease(
                    LifetimeDomain.Request,
                    "escape-retry-pending-enemy"
                );
            using GArray battleParty = new() { allyLease.Value };
            using GArray enemyUnits = new() { enemyLease.Value };
            using GDictionary context = new()
            {
                ["battle_terrain_profile"] = "missing_profile",
                ["battle_party"] = battleParty,
                ["enemy_units"] = enemyUnits,
            };

            StringName startResult = runtime.BeginBattleStart(anchor, 41, context);

            _test.Eq(
                startResult.ToString(),
                "pending",
                "较早的 Escape 绑定失败后若最新地形明确尚未就绪，应继续 pending。"
            );
            _test.True(
                runtime.HasPendingBattleGenerationRequest(),
                "最新地形尚未就绪时必须保留 pending 请求。"
            );
            _test.Eq(
                runtime.GetBattleRuntime().GetLastStartFailureSnapshot().Reason,
                "terrain_generation_pending",
                "启动失败快照必须反映最新一次异步地形状态。"
            );
            _test.True(
                gameSession.IsBattleSaveLocked(),
                "真正 pending 期间必须保持 battle save lock。"
            );
            runtime.HandleBattleStartFailure();
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private void TestDeferredTerminalFailureFlushesUnlockedState()
    {
        using GameSession gameSession =
            GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        GameRuntimeFacade runtime = new();
        try
        {
            _test.Eq(
                (Error)gameSession.StartNewGame(TestConfigPath),
                Error.Ok,
                "延迟失败持久化回归应能创建测试世界。"
            );
            runtime.Setup(gameSession);
            runtime.GetBattleRuntime()._terrain_generator =
                new PendingThenTerminalTerrainGenerator();
            EncounterAnchorData anchor = new()
            {
                entity_id = "deferred_terminal_anchor",
                display_name = "Deferred Terminal",
                encounter_profile_id = "deferred_terminal_encounter",
            };
            runtime.SetBattleEncounterDefinitionForTests(
                new BattleEncounterDefinition(
                    "deferred_terminal_encounter",
                    "Deferred Terminal",
                    "deferred_terminal_roster",
                    BattleEliminationObjectiveDefinition.Instance,
                    new BattleEncounterWorldResolutionDefinition(
                        BattleWorldResolutionMode.Clear,
                        BattleWorldResolutionMode.Preserve,
                        BattleWorldResolutionMode.Preserve,
                        0
                    )
                )
            );
            BattleUnitState ally = BuildUnit(
                "deferred_terminal_ally",
                "player",
                new Vector2I(0, 0)
            );
            ally.source_member_id = "deferred_terminal_member";
            BattleUnitState enemy = BuildUnit(
                "deferred_terminal_enemy",
                "hostile",
                new Vector2I(1, 0)
            );
            using GodotProjectionLease<GDictionary> allyLease =
                ally.ToDictionaryLease(
                    LifetimeDomain.Request,
                    "deferred-terminal-ally"
                );
            using GodotProjectionLease<GDictionary> enemyLease =
                enemy.ToDictionaryLease(
                    LifetimeDomain.Request,
                    "deferred-terminal-enemy"
                );
            using GArray battleParty = new() { allyLease.Value };
            using GArray enemyUnits = new() { enemyLease.Value };
            using GDictionary context = new()
            {
                ["battle_terrain_profile"] = "missing_profile",
                ["battle_party"] = battleParty,
                ["enemy_units"] = enemyUnits,
            };

            gameSession.SetBattleSaveLock(true);
            _test.Eq(
                (Error)gameSession.SetPlayerCoord(new Vector2I(2, 2)),
                Error.Ok,
                "延迟失败前应能写入玩家位置。"
            );
            _test.True(
                gameSession.HasPendingSave(),
                "battle save lock 下的位置写入应先保留 pending save。"
            );
            int initialWorldStep = runtime.GetWorldStep();
            runtime.AdvanceWorldTimeBySteps(1);
            int expectedWorldStep = runtime.GetWorldStep();
            _test.True(
                expectedWorldStep > initialWorldStep,
                "延迟失败回归必须先修改 runtime-owned world step。"
            );
            _test.True(
                PlainInt(
                    gameSession.CaptureWorldDataPlain(),
                    "world_step",
                    int.MinValue
                ) != expectedWorldStep,
                "flush 前 GameSession world payload 应仍落后于 typed world owner。"
            );
            _test.Eq(
                runtime.BeginBattleStart(anchor, 51, context).ToString(),
                "pending",
                "第一轮异步地形生成应进入 pending。"
            );

            _test.True(
                runtime.advance(0.016f),
                "后续 frame 的确定失败应产生完整刷新。"
            );
            _test.False(
                runtime.HasPendingBattleGenerationRequest(),
                "延迟终端失败后必须清除 pending 请求。"
            );
            _test.False(
                gameSession.IsBattleSaveLocked(),
                "延迟终端失败后必须释放 battle save lock。"
            );
            _test.False(
                gameSession.HasPendingSave(),
                "解锁后的玩家位置与世界状态必须完成 canonical flush。"
            );
            _test.Eq(
                PlainInt(
                    gameSession.CaptureWorldDataPlain(),
                    "world_step",
                    int.MinValue
                ),
                expectedWorldStep,
                "延迟失败必须先把 runtime-owned world step 同步进 GameSession 再落盘。"
            );
        }
        finally
        {
            runtime.Dispose();
            gameSession.ClearPersistedGame();
        }
    }

    private static BattleUnitState BuildUnit(StringName unitId, StringName factionId, Vector2I coord)
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            coord = coord,
            is_alive = true,
            current_hp = 10,
        };
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 10);
        unit.RefreshFootprint();
        return unit;
    }

    private static string PlainString(
        IReadOnlyDictionary<string, object> values,
        string key,
        string fallback = ""
    )
    {
        return values != null
            && values.TryGetValue(key, out object value)
            && value is string text
                ? text
                : fallback;
    }

    private static int PlainInt(
        IReadOnlyDictionary<string, object> values,
        string key,
        int fallback = 0
    )
    {
        if (
            values == null
            || !values.TryGetValue(key, out object value)
            || value == null
        )
        {
            return fallback;
        }
        try
        {
            return Convert.ToInt32(value);
        }
        catch
        {
            return fallback;
        }
    }

    private sealed class InvalidEscapeThenPendingTerrainGenerator
        : BattleTerrainGenerator
    {
        private int _generateCallCount;

        internal override bool EmptyGenerationIsPending => true;

        internal override GodotProjectionLease<GDictionary> GenerateLease(
            EncounterAnchorData encounterAnchor,
            long seed,
            GDictionary context,
            LifetimeDomain domain = LifetimeDomain.Battle
        )
        {
            _generateCallCount++;
            if (_generateCallCount > 1)
                return base.GenerateLease(encounterAnchor, seed, context, domain);

            GDictionary root = new();
            GodotProjectionLease<GDictionary> lease =
                GodotProjectionLease<GDictionary>.CreateOwnedRoot(
                    root,
                    "invalid-escape-then-pending-terrain",
                    domain,
                    "InvalidEscapeThenPendingTerrainGenerator.GenerateLease"
                );
            Vector2I mapSize = new(3, 2);
            var cells = new Dictionary<Vector2I, BattleCellState>();
            for (int y = 0; y < mapSize.Y; y++)
            {
                for (int x = 0; x < mapSize.X; x++)
                {
                    var cell = new BattleCellState
                    {
                        coord = new Vector2I(x, y),
                        base_terrain =
                            x == mapSize.X - 1 ? "deep_water" : "land",
                        base_height = 4,
                    };
                    cell.RecalculateRuntimeValues();
                    cells[cell.coord] = cell;
                }
            }
            root["map_size"] = mapSize;
            root["cells"] = BattleCellState.ProjectCellsToPayload(lease, cells);
            root["cell_columns"] = BattleCellState.ProjectColumnsToPayload(
                lease,
                BattleCellState.BuildColumnsFromSurfaceCells(cells)
            );
            root["ally_spawns"] = lease.Own(
                new GArray { new Vector2I(0, 0) },
                "InvalidEscapeThenPendingTerrainGenerator.ally_spawns"
            );
            root["enemy_spawns"] = lease.Own(
                new GArray { new Vector2I(1, 1) },
                "InvalidEscapeThenPendingTerrainGenerator.enemy_spawns"
            );
            root["terrain_profile_id"] = new StringName("default");
            return lease;
        }
    }

    private sealed class PendingThenTerminalTerrainGenerator
        : BattleTerrainGenerator
    {
        private int _generateCallCount;

        internal override bool EmptyGenerationIsPending =>
            _generateCallCount <= 8;

        internal override GodotProjectionLease<GDictionary> GenerateLease(
            EncounterAnchorData encounterAnchor,
            long seed,
            GDictionary context,
            LifetimeDomain domain = LifetimeDomain.Battle
        )
        {
            _generateCallCount++;
            return base.GenerateLease(encounterAnchor, seed, context, domain);
        }
    }
}
