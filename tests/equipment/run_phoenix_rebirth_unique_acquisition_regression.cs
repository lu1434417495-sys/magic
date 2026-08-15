using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_phoenix_rebirth_unique_acquisition_regression : LifecycleTestSceneTree
{
    private const string TestWorldConfig =
        "res://data/configs/world_map/test_world_map_config.tres";

    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        RunCase("new-save/shop", TestNewSaveAndShopTransfer);
        RunCase("warehouse-guards", TestWorldUniqueWarehouseGuards);
        RunCase("random-drop", TestRandomEquipmentDropTransfersReservedInstance);
        RequestTestExit(_test.Finish("Phoenix rebirth unique acquisition regression"));
    }

    private void RunCase(string name, Action test)
    {
        try
        {
            test?.Invoke();
        }
        catch (Exception exception)
        {
            _test.Fail($"{name} threw {exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}");
        }
    }

    private void TestNewSaveAndShopTransfer()
    {
        GameSession session = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        try
        {
            session.SetRandomStartingSkillSelectorForTests(_ => "mage_arcane_missile");
            Error createError = (Error)session.CreateNewSave(TestWorldConfig);
            _test.Eq(createError, Error.Ok, "CreateNewSave 应完成凤凰套装储备池初始化。");
            if (createError != Error.Ok)
                return;

            WorldRuntimeData worldData = ReadSessionWorldData(session);
            WorldUniqueEquipmentPoolState pool = worldData?.UniqueEquipmentPool;
            _test.True(pool != null, "新存档 world_data 应拥有凤凰唯一装备池。");
            if (pool == null)
                return;

            GearSetDefinition phoenixSet = session
                .GetContentCatalogTyped()
                .GetGearSetDefinitionsTyped()[WorldUniqueEquipmentPoolState.PhoenixRebirthPoolId];
            IReadOnlyList<EquipmentInstanceState> seededInstances = pool.SnapshotInstances();
            _test.Eq(seededInstances.Count, 10, "新存档必须且只初始生成十件凤凰套装实例。");
            _test.Eq(
                pool.CountAtLocation(WorldUniqueEquipmentLocationKind.Reserve),
                10,
                "初始十件实例应全部位于隐藏储备池。"
            );

            var expectedItems = new HashSet<StringName>(phoenixSet.MemberItemIds);
            var actualItems = new HashSet<StringName>();
            var instanceIds = new HashSet<StringName>();
            using var traitRollService = new EquipmentTraitRollService(
                session.GetTraitDefsTyped().Values
            );
            foreach (EquipmentInstanceState instance in seededInstances)
            {
                actualItems.Add(instance.item_id);
                instanceIds.Add(instance.instance_id);
                _test.Eq(
                    instance.rarity,
                    (int)EquipmentInstanceState.RarityTier.LEGENDARY,
                    $"{instance.item_id} 应在初始化时固定为传奇稀有度。"
                );
                _test.Eq(
                    instance.current_durability,
                    EquipmentDurabilityRules.GetDefaultCurrentDurability(instance.rarity),
                    $"{instance.item_id} 应在初始化时获得传奇默认耐久。"
                );
                _test.True(
                    traitRollService.ValidateRehydrated(instance),
                    $"{instance.item_id} 的已有 equipment_roll trait 应在池中可重载。"
                );
            }
            _test.Eq(actualItems.Count, expectedItems.Count, "储备池应覆盖套装十个不同 member_item_id。");
            _test.Eq(instanceIds.Count, 10, "储备池十件装备应各有不同且稳定的 instance_id。");
            foreach (StringName memberItemId in expectedItems)
                _test.True(actualItems.Contains(memberItemId), $"储备池缺少套装成员 {memberItemId}。");

            AssertEditedDuplicatePoolIsAccepted(session.GetWorldDataSnapshotPlain());
            TestRuntimeShopRoundTripAndReload(session, pool, seededInstances);
            AssertOldSaveShapeDoesNotBackfill(session);
        }
        finally
        {
            CleanupSession(session);
        }
    }

    private void TestRuntimeShopRoundTripAndReload(
        GameSession session,
        WorldUniqueEquipmentPoolState sourcePool,
        IReadOnlyList<EquipmentInstanceState> seededInstances
    )
    {
        const string shopId = "village_basic_supply";
        string saveId = session.GetActiveSaveId();
        string settlementId = ReadSessionWorldData(session)?.PlayerStartSettlementId ?? "";
        _test.True(settlementId.Length > 0, "正式商店链应能解析新世界的起始据点。");
        if (settlementId.Length == 0)
            return;

        GameRuntimeFacade runtime = null;
        EquipmentInstanceState offeredExpected = null;
        try
        {
            session.GetPartyState().gold = 100000;
            EnsureWarehouseCapacity(session.GetPartyState(), 20);
            runtime = new GameRuntimeFacade();
            runtime.Setup(session);
            runtime._settlement_command_handler._shop_service
                .SetUniqueOfferRollRangeForTesting((min, _) => min);

            WorldUniqueEquipmentPoolState runtimePool = runtime
                .GetActiveWorldRuntimeData()
                ?.UniqueEquipmentPool;
            _test.True(runtimePool != null, "正式 runtime root 应装载 CreateNewSave 生成的唯一池。");
            _test.Eq(
                runtimePool?.RemainingInstanceCount ?? -1,
                sourcePool.RemainingInstanceCount,
                "runtime 不得另造或漏掉 CreateNewSave 的凤凰实例。"
            );

            RuntimeCommandResult openResult = OpenBasicSupplyShop(runtime, settlementId);
            _test.True(openResult.Ok, $"正式据点命令应打开并刷新商店：{openResult.Message}");
            if (!openResult.Ok || runtimePool == null)
                return;
            _test.Eq(
                runtimePool.CountAtLocation(WorldUniqueEquipmentLocationKind.Shop),
                1,
                "强制命中后正式商店刷新应移动一件新档池实例到 shop location。"
            );

            foreach (EquipmentInstanceState seeded in seededInstances)
            {
                if (
                    runtimePool.TryGetShopOffer(
                        settlementId,
                        shopId,
                        seeded.item_id,
                        out StringName offeredInstanceId
                    )
                    && offeredInstanceId == seeded.instance_id
                )
                {
                    offeredExpected = seeded.DuplicateState();
                    break;
                }
            }
            _test.True(offeredExpected != null, "商店 offer 必须对应 CreateNewSave 的真实原实例。");
            if (offeredExpected == null)
                return;
            _test.True(
                ContainsPlainWindowBuyInstance(
                    runtime.GetShopWindowDataSnapshotPlain(),
                    offeredExpected.item_id,
                    offeredExpected.instance_id
                ),
                "正式商店窗口应显示新档原实例的稳定 ID。"
            );

            RuntimeCommandResult purchase = runtime.CommandShopBuyTyped(
                offeredExpected.item_id,
                1
            );
            _test.True(purchase.Ok, $"正式商店购买应成功：{purchase.Message}");
            _test.Eq(runtimePool.RemainingInstanceCount, 9, "购买应使 world pool 10→9。");
            _test.Eq(
                runtimePool.CountAtLocation(WorldUniqueEquipmentLocationKind.Shop),
                0,
                "购买后该实例不应继续占据 shop location。"
            );
            EquipmentInstanceState purchased = FindWarehouseInstance(
                session.GetPartyState().warehouse_state,
                offeredExpected.instance_id
            );
            AssertSameEquipmentInstance(
                offeredExpected,
                purchased,
                "正式购买必须转移原 instance_id/耐久/traits"
            );

            RuntimeCommandResult sale = runtime.CommandShopSellTyped(
                offeredExpected.item_id,
                1,
                offeredExpected.instance_id
            );
            _test.True(sale.Ok, $"正式卖回应成功：{sale.Message}");
            _test.True(
                FindWarehouseInstance(
                    session.GetPartyState().warehouse_state,
                    offeredExpected.instance_id
                ) == null,
                "卖回后玩家仓库不应继续持有该实例。"
            );
            _test.Eq(runtimePool.RemainingInstanceCount, 10, "卖回应把原实例重新纳入世界池。");
            _test.Eq(
                runtimePool.CountAtLocation(WorldUniqueEquipmentLocationKind.Shop),
                1,
                "卖回应把原实例定位到当前商店。"
            );
            _test.True(
                runtimePool.TryGetShopOffer(
                    settlementId,
                    shopId,
                    offeredExpected.item_id,
                    out StringName resaleId
                )
                && resaleId == offeredExpected.instance_id,
                "卖回库存必须继续引用同一稳定实例。"
            );
            _test.Eq(
                (Error)session.CommitRuntimeState("phoenix_unique_shop_round_trip"),
                Error.Ok,
                "正式商店写回后应可提交存档。"
            );

            runtime.Dispose();
            runtime = null;
            session.UnloadActiveWorld();
            _test.Eq((Error)session.LoadSave(saveId), Error.Ok, "商店持有实例的存档应可重载。");

            runtime = new GameRuntimeFacade();
            runtime.Setup(session);
            runtime._settlement_command_handler._shop_service
                .SetUniqueOfferRollRangeForTesting((min, _) => min);
            WorldUniqueEquipmentPoolState reloadedPool = runtime
                .GetActiveWorldRuntimeData()
                ?.UniqueEquipmentPool;
            _test.True(reloadedPool != null, "重载后 unique pool 应继续存在。");
            _test.Eq(
                reloadedPool?.CountAtLocation(WorldUniqueEquipmentLocationKind.Shop) ?? -1,
                1,
                "重载后应保留 shop location。"
            );
            _test.True(
                reloadedPool != null
                && reloadedPool.TryGetShopOffer(
                    settlementId,
                    shopId,
                    offeredExpected.item_id,
                    out StringName reloadedOfferId
                )
                && reloadedOfferId == offeredExpected.instance_id,
                "重载后商店仍应持有同一稳定 ID。"
            );
            AssertSameEquipmentInstance(
                offeredExpected,
                FindPoolInstance(reloadedPool, offeredExpected.instance_id),
                "存档重载必须保留 shop 实例的耐久与 traits"
            );

            RuntimeCommandResult reopenResult = OpenBasicSupplyShop(runtime, settlementId);
            _test.True(reopenResult.Ok, $"重载后正式商店应可重新打开：{reopenResult.Message}");
            _test.True(
                ContainsPlainWindowBuyInstance(
                    runtime.GetShopWindowDataSnapshotPlain(),
                    offeredExpected.item_id,
                    offeredExpected.instance_id
                ),
                "重载后的商店窗口应展示同一实例。"
            );

            session.fail_payload_write = true;
            RuntimeCommandResult failedPurchase = runtime.CommandShopBuyTyped(
                offeredExpected.item_id,
                1
            );
            session.fail_payload_write = false;
            _test.False(failedPurchase.Ok, "持久化失败时唯一实例购买必须失败。");
            _test.Eq(
                failedPurchase.Code,
                RuntimeCommandCode.PersistenceFailure,
                "持久化失败应走正式 transaction failure code。"
            );
            WorldUniqueEquipmentPoolState rolledBackPool = runtime
                .GetActiveWorldRuntimeData()
                ?.UniqueEquipmentPool;
            _test.True(
                rolledBackPool != null
                && rolledBackPool.TryGetShopOffer(
                    settlementId,
                    shopId,
                    offeredExpected.item_id,
                    out StringName rolledBackId
                )
                && rolledBackId == offeredExpected.instance_id,
                "持久化失败后 pool 应回滚到原 shop 实例。"
            );
            _test.True(
                FindWarehouseInstance(
                    session.GetPartyState().warehouse_state,
                    offeredExpected.instance_id
                ) == null,
                "持久化失败后仓库不得残留半提交实例。"
            );

            RuntimeCommandResult retryPurchase = runtime.CommandShopBuyTyped(
                offeredExpected.item_id,
                1
            );
            _test.True(retryPurchase.Ok, $"回滚后的同一 offer 应可再次购买：{retryPurchase.Message}");
            AssertSameEquipmentInstance(
                offeredExpected,
                FindWarehouseInstance(
                    session.GetPartyState().warehouse_state,
                    offeredExpected.instance_id
                ),
                "持久化回滚后重试购买仍须得到原实例"
            );
        }
        finally
        {
            session.fail_payload_write = false;
            runtime?.Dispose();
        }
    }

    private void TestWorldUniqueWarehouseGuards()
    {
        GameSession session = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        GameRuntimeFacade runtime = null;
        try
        {
            Error createError = (Error)session.CreateNewSave(TestWorldConfig);
            _test.Eq(createError, Error.Ok, "仓库唯一装备门禁应能创建新档前置。");
            if (createError != Error.Ok)
                return;

            EnsureWarehouseCapacity(session.GetPartyState(), 20);
            runtime = new GameRuntimeFacade();
            runtime.Setup(session);
            WorldUniqueEquipmentPoolState pool = runtime
                .GetActiveWorldRuntimeData()
                ?.UniqueEquipmentPool;
            EquipmentInstanceState seeded = pool?.SnapshotInstances().Count > 0
                ? pool.SnapshotInstances()[0]
                : null;
            _test.True(seeded != null, "仓库门禁前置应从新档池读取原实例。");
            if (seeded == null)
                return;

            PartyWarehouseService warehouse = runtime.GetPartyWarehouseService();
            RuntimeCommandResult rawAdd = runtime.CommandWarehouseAddItemTyped(
                seeded.item_id,
                1
            );
            _test.False(rawAdd.Ok, "world_unique_equipment 的 raw AddItem 必须 fail closed。");
            _test.True(
                FindWarehouseInstance(
                    session.GetPartyState().warehouse_state,
                    seeded.instance_id
                ) == null,
                "raw AddItem 失败不得向仓库伪造池实例。"
            );
            _test.Eq(pool.RemainingInstanceCount, 10, "raw AddItem 失败不得消耗 world pool。");

            EquipmentInstanceState emptyId = EquipmentInstanceState.CreateTransientInstance(
                seeded.item_id
            );
            PartyWarehouseService.WarehouseAddItemResult emptyIdResult =
                warehouse.AddEquipmentInstanceTyped(emptyId);
            _test.Eq(emptyIdResult.AddedQuantity, 0, "唯一装备空 instance_id 必须被拒绝。");
            _test.Eq(
                emptyId.instance_id,
                new StringName(""),
                "拒绝空 ID 时不得偷偷分配新稳定 ID。"
            );

            EquipmentInstanceState forceNew = seeded.DuplicateState();
            PartyWarehouseService.WarehouseAddItemResult forceNewResult =
                warehouse.AddEquipmentInstanceTyped(forceNew, true);
            _test.Eq(forceNewResult.AddedQuantity, 0, "唯一装备 force-new 路径必须被拒绝。");
            _test.Eq(
                forceNew.instance_id,
                seeded.instance_id,
                "拒绝 force-new 时不得替换原稳定 ID。"
            );

            _test.True(
                pool.TryTakeReserveByItem(
                    seeded.item_id,
                    out EquipmentInstanceState transferred
                ),
                "合法转移必须先由 world pool 交出原实例。"
            );
            PartyWarehouseService.WarehouseAddItemResult legalResult =
                warehouse.AddEquipmentInstanceTyped(transferred);
            _test.Eq(legalResult.AddedQuantity, 1, "持有稳定 ID 的合法池实例应允许入仓。");
            _test.Eq(pool.RemainingInstanceCount, 9, "合法转移应只从池移走一件。");
            AssertSameEquipmentInstance(
                seeded,
                FindWarehouseInstance(
                    session.GetPartyState().warehouse_state,
                    seeded.instance_id
                ),
                "合法池实例入仓必须保持 instance_id/耐久/traits"
            );
        }
        finally
        {
            runtime?.Dispose();
            CleanupSession(session);
        }
    }

    private void AssertEditedDuplicatePoolIsAccepted(
        IReadOnlyDictionary<string, object> worldSnapshot
    )
    {
        Dictionary<string, object> edited = RuntimePlainPayload.CloneDictionary(worldSnapshot);
        var pool = (Dictionary<string, object>)edited[WorldRuntimeSaveSchema.UniqueEquipmentPool];
        var entries = (List<object>)pool["entries"];
        entries.Add(
            RuntimePlainPayload.CloneDictionary(
                (IReadOnlyDictionary<string, object>)entries[0]
            )
        );
        using GodotProjectionLease<GDictionary> lease =
            RuntimePlainPayload.ProjectDictionaryLease(
                edited,
                "phoenix-edited-duplicate-pool",
                LifetimeDomain.Request,
                "run_phoenix_rebirth_unique_acquisition_regression.edited_duplicate"
            );
        WorldRuntimeData decoded = WorldRuntimeData.FromDictionary(lease.Value);
        var serializer = new SaveSerializer();
        _test.Eq(
            serializer.GetWorldDataValidationError(lease.Value),
            "",
            "save/schema 边界不应扫描或去重玩家手改的凤凰实例。"
        );
        _test.True(decoded?.UniqueEquipmentPool != null, "玩家手改重复实例不应触发全局去重或拒绝加载。");
        _test.Eq(
            decoded?.UniqueEquipmentPool?.RemainingInstanceCount ?? 0,
            11,
            "玩家手改出来的第十一条实例应原样保留。"
        );
    }

    private void AssertOldSaveShapeDoesNotBackfill(GameSession session)
    {
        string saveId = session.GetActiveSaveId();
        Dictionary<string, object> oldWorld = RuntimePlainPayload.CloneDictionary(
            session.GetWorldDataSnapshotPlain()
        );
        oldWorld.Remove(WorldRuntimeSaveSchema.UniqueEquipmentPool);
        using (GodotProjectionLease<GDictionary> oldWorldLease =
            RuntimePlainPayload.ProjectDictionaryLease(
                oldWorld,
                "phoenix-old-world-shape",
                LifetimeDomain.Request,
                "run_phoenix_rebirth_unique_acquisition_regression.old_world"
            ))
        {
            _test.Eq(
                (Error)session.SetWorldData(oldWorldLease.Value),
                Error.Ok,
                "缺少可选 unique_equipment_pool 的旧 world_data 仍应是合法存档形状。"
            );
        }
        _test.Eq(
            (Error)session.CommitRuntimeState("phoenix_old_shape_test"),
            Error.Ok,
            "旧形状 world_data 应可持久化。"
        );
        session.UnloadActiveWorld();
        _test.Eq((Error)session.LoadSave(saveId), Error.Ok, "旧形状存档应可重新加载。");
        WorldRuntimeData reloaded = ReadSessionWorldData(session);
        _test.False(
            reloaded?.HasUniqueEquipmentPool ?? true,
            "LoadSave 不得给旧存档回填凤凰套装。"
        );
    }

    private void TestRandomEquipmentDropTransfersReservedInstance()
    {
        GameSession session = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        GameRuntimeFacade runtime = null;
        try
        {
            Error createError = (Error)session.CreateNewSave(TestWorldConfig);
            _test.Eq(createError, Error.Ok, "随机掉落回归前置应能创建新存档。");
            if (createError != Error.Ok)
                return;

            EnsureWarehouseCapacity(session.GetPartyState(), 20);
            runtime = new GameRuntimeFacade();
            runtime.Setup(session);
            runtime.SetUniqueEquipmentDropRollRangeForTesting((min, _) => min);
            runtime._equipment_drop_service?.Dispose();
            var countingDropService = new CountingEquipmentDropService();
            runtime._equipment_drop_service = countingDropService;

            WorldUniqueEquipmentPoolState pool = runtime
                .GetActiveWorldRuntimeData()
                ?.UniqueEquipmentPool;
            IReadOnlyList<EquipmentInstanceState> before = pool?.SnapshotInstances()
                ?? Array.Empty<EquipmentInstanceState>();
            var seededIds = new HashSet<StringName>();
            foreach (EquipmentInstanceState instance in before)
                seededIds.Add(instance.instance_id);
            _test.Eq(pool?.RemainingInstanceCount ?? -1, 10, "正式击杀掉落前池应有十件原实例。");

            BattleRuntimeModule battleRuntime = runtime.GetBattleRuntime();
            InjectRandomEquipmentEnemyTemplate(
                runtime,
                "phoenix_unique_random_enemy_template",
                "bronze_sword"
            );
            BattleUnitState defeatedEnemy = BuildDefeatedEnemyUnit(
                "phoenix_unique_random_enemy",
                "phoenix_unique_random_enemy_template"
            );
            battleRuntime._loot_resolver.CollectDefeatedUnitLoot(defeatedEnemy, null);
            BattleState battleState = BuildPlayerVictoryState("phoenix_unique_random_battle");
            battleRuntime.SetupStateForTests(battleState);
            BattleResolutionResult resolution =
                battleRuntime._loot_resolver.BuildBattleResolutionResult();
            _test.Eq(resolution.loot_entries.Count, 1, "真实 loot resolver 应冻结一条随机装备请求。");
            if (resolution.loot_entries.Count == 1)
            {
                _test.Eq(
                    resolution.loot_entries[0].DropKind,
                    BattleLootDropKind.RandomEquipment,
                    "world commit 前 BattleResolution 必须保持 RandomEquipment request。"
                );
                _test.Eq(
                    resolution.loot_entries[0].ItemId,
                    new StringName("bronze_sword"),
                    "真实击杀请求应保留敌人模板中的普通装备 item_id。"
                );
            }
            _test.Eq(
                countingDropService.RollItemInstancesCallCount,
                0,
                "BattleRuntimeLootResolver 只冻结请求，不得提前调用普通生成器。"
            );
            _test.Eq(pool?.RemainingInstanceCount ?? -1, 10, "resolver 阶段不得提前消费 world pool。");

            GameRuntimeBattleLootCommitService.BattleLootCommitResult commit =
                runtime.CommitBattleLootToSharedWarehouseTyped(resolution);
            _test.True(commit.Ok, $"随机装备掉落提交应成功：{commit.ErrorCode}");
            _test.Eq(commit.CommittedItemCount, 1, "命中凤凰池的随机装备掉落应提交一个实例。");
            _test.Eq(
                countingDropService.RollItemInstancesCallCount,
                0,
                "命中凤凰成员时绝不能调用 EquipmentDropService 生成新实例。"
            );
            _test.Eq(pool?.RemainingInstanceCount ?? -1, 9, "随机掉落应从储备池移走一个原实例。");
            _test.Eq(
                resolution.loot_entries.Count,
                1,
                "命中唯一池后战利品结果应物化为一个实际装备实例。"
            );
            if (resolution.loot_entries.Count == 1)
            {
                _test.Eq(
                    resolution.loot_entries[0].DropKind,
                    BattleLootDropKind.EquipmentInstance,
                    "战利品展示不得继续伪装成原普通随机装备。"
                );
            }

            EquipmentInstanceState acquired = FindWarehouseInstanceFromIds(
                session.GetPartyState()?.warehouse_state,
                seededIds
            );
            _test.True(acquired != null, "战利品领取应把凤凰原实例转移到共享仓库。");
            if (acquired != null)
            {
                _test.True(
                    seededIds.Contains(acquired.instance_id),
                    "战利品中的 instance_id 必须来自新存档初始化池。"
                );
                if (resolution.loot_entries.Count == 1)
                {
                    _test.Eq(
                        resolution.loot_entries[0].EquipmentInstance?.instance_id
                            ?? new StringName(""),
                        acquired.instance_id,
                        "战利品结果与仓库必须引用同一个初始化 instance_id。"
                    );
                }
                _test.Eq(
                    acquired.rarity,
                    (int)EquipmentInstanceState.RarityTier.LEGENDARY,
                    "战利品转移应保留初始化时的传奇稀有度。"
                );
                EquipmentInstanceState expected = null;
                foreach (EquipmentInstanceState seeded in before)
                {
                    if (seeded.instance_id == acquired.instance_id)
                    {
                        expected = seeded;
                        break;
                    }
                }
                AssertSameEquipmentInstance(
                    expected,
                    acquired,
                    "真实击杀 world commit 必须转移新档池原实例"
                );

                int matchingWarehouseCountBefore = CountWarehouseInstancesByItem(
                    session.GetPartyState().warehouse_state,
                    acquired.item_id
                );
                battleRuntime._active_loot_entries.Clear();
                InjectRandomEquipmentEnemyTemplate(
                    runtime,
                    "phoenix_explicit_unavailable_template",
                    acquired.item_id
                );
                BattleUnitState explicitEnemy = BuildDefeatedEnemyUnit(
                    "phoenix_explicit_unavailable_enemy",
                    "phoenix_explicit_unavailable_template"
                );
                battleRuntime._loot_resolver.CollectDefeatedUnitLoot(explicitEnemy, null);
                battleRuntime.SetupStateForTests(
                    BuildPlayerVictoryState("phoenix_explicit_unavailable_battle")
                );
                BattleResolutionResult explicitResolution =
                    battleRuntime._loot_resolver.BuildBattleResolutionResult();
                _test.Eq(
                    explicitResolution.loot_entries.Count,
                    1,
                    "显式凤凰请求应由真实 resolver 保留为一条 request。"
                );
                if (explicitResolution.loot_entries.Count == 1)
                {
                    _test.Eq(
                        explicitResolution.loot_entries[0].DropKind,
                        BattleLootDropKind.RandomEquipment,
                        "显式凤凰请求在 commit 前仍应保持 RandomEquipment。"
                    );
                    _test.Eq(
                        explicitResolution.loot_entries[0].ItemId,
                        acquired.item_id,
                        "显式请求应指向已经离开 reserve 的凤凰成员。"
                    );
                }
                GameRuntimeBattleLootCommitService.BattleLootCommitResult explicitCommit =
                    runtime.CommitBattleLootToSharedWarehouseTyped(explicitResolution);
                _test.True(explicitCommit.Ok, "reserve 无对应实例时应以无掉落方式 fail closed。");
                _test.Eq(
                    explicitCommit.CommittedItemCount,
                    0,
                    "reserve 无对应凤凰成员时不得提交复制品。"
                );
                _test.Eq(
                    countingDropService.RollItemInstancesCallCount,
                    0,
                    "显式凤凰成员无 reserve 实例时普通 generator 调用数必须保持 0。"
                );
                _test.Eq(pool?.RemainingInstanceCount ?? -1, 9, "显式缺货请求不得继续消耗池。");
                _test.Eq(
                    CountWarehouseInstancesByItem(
                        session.GetPartyState().warehouse_state,
                        acquired.item_id
                    ),
                    matchingWarehouseCountBefore,
                    "显式缺货请求不得向仓库增加同 item_id 的复制品。"
                );
            }
        }
        finally
        {
            runtime?.Dispose();
            CleanupSession(session);
        }
    }

    private static void InjectRandomEquipmentEnemyTemplate(
        GameRuntimeFacade runtime,
        StringName templateId,
        StringName itemId
    )
    {
        var template = new EnemyTemplateDef
        {
            template_id = templateId,
            display_name = "凤凰唯一掉落测试敌人",
            cognition_kind = "instinctive",
        };
        template.drop_entries.Add(
            new DropEntryDef
            {
                drop_entry_id = "unique_equipment_request",
                drop_type = "random_equipment",
                item_id = itemId,
                quantity = 1,
            }
        );
        runtime.GetBattleRuntime().ReplaceEnemyTemplatesTyped(
            new Dictionary<StringName, EnemyTemplateDefinition>
            {
                [templateId] = template.ToDefinition(runtime.GetItemDefsTyped()),
            }
        );
    }

    private static BattleUnitState BuildDefeatedEnemyUnit(
        StringName unitId,
        StringName templateId
    ) =>
        new BattleUnitState
        {
            unit_id = unitId,
            enemy_template_id = templateId,
            display_name = unitId.ToString(),
            faction_id = "hostile",
            control_mode = "ai",
        }.WithCombatResourcesForTest(isAlive: false);

    private static BattleState BuildPlayerVictoryState(StringName battleId)
    {
        var state = new BattleState { battle_id = battleId };
        BattleObjectiveTestFactory.SetEliminationDecision(state, "player");
        return state;
    }

    private static WorldRuntimeData ReadSessionWorldData(GameSession session)
    {
        using GodotProjectionLease<GDictionary> lease = session.GetWorldDataLease();
        return WorldRuntimeData.FromDictionary(lease.Value);
    }

    private static EquipmentInstanceState FindWarehouseInstance(
        WarehouseState warehouse,
        StringName instanceId
    )
    {
        foreach (
            EquipmentInstanceState instance in warehouse?.GetNonEmptyEquipmentInstancesTyped()
                ?? Array.Empty<EquipmentInstanceState>()
        )
        {
            if (instance.instance_id == instanceId)
                return instance;
        }
        return null;
    }

    private static EquipmentInstanceState FindWarehouseInstanceFromIds(
        WarehouseState warehouse,
        IReadOnlySet<StringName> instanceIds
    )
    {
        foreach (EquipmentInstanceState instance in warehouse?.GetNonEmptyEquipmentInstancesTyped()
            ?? Array.Empty<EquipmentInstanceState>())
        {
            if (instance != null && instanceIds?.Contains(instance.instance_id) == true)
                return instance;
        }
        return null;
    }

    private static int CountWarehouseInstancesByItem(
        WarehouseState warehouse,
        StringName itemId
    )
    {
        int count = 0;
        foreach (EquipmentInstanceState instance in warehouse?.GetNonEmptyEquipmentInstancesTyped()
            ?? Array.Empty<EquipmentInstanceState>())
        {
            if (instance?.item_id == itemId)
                count++;
        }
        return count;
    }

    private static EquipmentInstanceState FindPoolInstance(
        WorldUniqueEquipmentPoolState pool,
        StringName instanceId
    )
    {
        foreach (EquipmentInstanceState instance in pool?.SnapshotInstances()
            ?? Array.Empty<EquipmentInstanceState>())
        {
            if (instance.instance_id == instanceId)
                return instance;
        }
        return null;
    }

    private RuntimeCommandResult OpenBasicSupplyShop(
        GameRuntimeFacade runtime,
        string settlementId
    )
    {
        runtime.SetActiveSettlementId(settlementId);
        runtime.SetRuntimeActiveModalKind(RuntimeModalKind.Settlement);
        return runtime.CommandExecuteSettlementActionTyped(
            new SettlementActionRequest(
                new StringName(settlementId),
                new StringName("service:basic_supply"),
                new StringName("service:basic_supply"),
                new StringName(""),
                0,
                SettlementSubmissionSource.Settlement
            )
        );
    }

    private static void EnsureWarehouseCapacity(PartyState party, int capacity)
    {
        if (party == null)
            return;
        bool assigned = false;
        foreach (PartyMemberState member in party.GetMemberStates())
        {
            UnitBaseAttributes attributes = member?.progression?.unit_base_attributes;
            if (attributes == null)
                continue;
            attributes.custom_stats[PartyWarehouseService.StorageSpaceAttributeId] = assigned
                ? 0
                : Mathf.Max(capacity, 0);
            assigned = true;
        }
    }

    private static bool ContainsPlainWindowBuyInstance(
        IReadOnlyDictionary<string, object> window,
        StringName itemId,
        StringName instanceId
    )
    {
        if (
            window == null
            || !window.TryGetValue("entries", out object rawEntries)
            || rawEntries is not IEnumerable<object> entries
        )
        {
            return false;
        }
        foreach (object rawEntry in entries)
        {
            if (rawEntry is not IReadOnlyDictionary<string, object> entry)
                continue;
            string shopAction = entry.TryGetValue("shop_action", out object rawShopAction)
                ? rawShopAction?.ToString() ?? ""
                : "";
            if (shopAction != "buy")
                continue;
            string entryItemId = entry.TryGetValue("item_id", out object rawItemId)
                ? rawItemId?.ToString() ?? ""
                : "";
            string entryId = entry.TryGetValue("entry_id", out object rawEntryId)
                ? rawEntryId?.ToString() ?? ""
                : "";
            if (
                entryItemId == itemId.ToString()
                && entryId.Contains(instanceId.ToString(), StringComparison.Ordinal)
            )
            {
                return true;
            }
        }
        return false;
    }

    private void AssertSameEquipmentInstance(
        EquipmentInstanceState expected,
        EquipmentInstanceState actual,
        string context
    )
    {
        _test.True(expected != null, $"{context}：expected 实例不能为空。");
        _test.True(actual != null, $"{context}：actual 实例不能为空。");
        if (expected == null || actual == null)
            return;
        _test.Eq(actual.instance_id, expected.instance_id, $"{context}：instance_id 应保持。");
        _test.Eq(actual.item_id, expected.item_id, $"{context}：item_id 应保持。");
        _test.Eq(actual.rarity, expected.rarity, $"{context}：rarity 应保持。");
        _test.Eq(
            actual.current_durability,
            expected.current_durability,
            $"{context}：current_durability 应保持。"
        );
        _test.Eq(
            actual.trait_instances.Count,
            expected.trait_instances.Count,
            $"{context}：trait 数量应保持。"
        );
        int traitCount = Math.Min(
            actual.trait_instances.Count,
            expected.trait_instances.Count
        );
        for (int index = 0; index < traitCount; index++)
        {
            TraitInstanceState expectedTrait = expected.trait_instances[index];
            TraitInstanceState actualTrait = actual.trait_instances[index];
            _test.True(actualTrait != null && expectedTrait != null, $"{context}：trait[{index}] 不能为空。");
            if (actualTrait == null || expectedTrait == null)
                continue;
            _test.Eq(actualTrait.trait_instance_id, expectedTrait.trait_instance_id, $"{context}：trait[{index}] instance id 应保持。");
            _test.Eq(actualTrait.trait_id, expectedTrait.trait_id, $"{context}：trait[{index}] id 应保持。");
            _test.Eq(actualTrait.source_type, expectedTrait.source_type, $"{context}：trait[{index}] source type 应保持。");
            _test.Eq(actualTrait.source_id, expectedTrait.source_id, $"{context}：trait[{index}] source id 应保持。");
            _test.Eq(actualTrait.rank, expectedTrait.rank, $"{context}：trait[{index}] rank 应保持。");
            _test.Eq(actualTrait.stacks, expectedTrait.stacks, $"{context}：trait[{index}] stacks 应保持。");
            List<TraitRollValueState> expectedRolls = TraitInstanceState.NormalizeRollValues(
                expectedTrait.roll_values
            );
            List<TraitRollValueState> actualRolls = TraitInstanceState.NormalizeRollValues(
                actualTrait.roll_values
            );
            _test.Eq(actualRolls.Count, expectedRolls.Count, $"{context}：trait[{index}] roll 数量应保持。");
            int rollCount = Math.Min(actualRolls.Count, expectedRolls.Count);
            for (int rollIndex = 0; rollIndex < rollCount; rollIndex++)
            {
                TraitRollValueState expectedRoll = expectedRolls[rollIndex];
                TraitRollValueState actualRoll = actualRolls[rollIndex];
                _test.Eq(actualRoll.key, expectedRoll.key, $"{context}：trait[{index}] roll[{rollIndex}] key 应保持。");
                _test.Eq(actualRoll.value_type, expectedRoll.value_type, $"{context}：trait[{index}] roll[{rollIndex}] type 应保持。");
                _test.Eq(actualRoll.int_value, expectedRoll.int_value, $"{context}：trait[{index}] roll[{rollIndex}] int 应保持。");
                _test.Eq(actualRoll.string_name_value, expectedRoll.string_name_value, $"{context}：trait[{index}] roll[{rollIndex}] StringName 应保持。");
                _test.Eq(actualRoll.bool_value, expectedRoll.bool_value, $"{context}：trait[{index}] roll[{rollIndex}] bool 应保持。");
            }
        }
    }

    private static void CleanupSession(GameSession session)
    {
        if (session == null)
            return;
        if (session.HasActiveWorld())
            session.UnloadActiveWorld();
        session.ClearPersistedGame();
        session.Dispose();
    }

    private sealed class CountingEquipmentDropService : EquipmentDropService
    {
        internal int RollItemInstancesCallCount { get; private set; }

        public override List<EquipmentInstanceState> RollItemInstances(
            StringName itemId,
            int quantity,
            int dropLuck
        )
        {
            RollItemInstancesCallCount++;
            return base.RollItemInstances(itemId, quantity, dropLuck);
        }
    }
}
