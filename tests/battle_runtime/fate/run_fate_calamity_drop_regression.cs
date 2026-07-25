using Godot;

public partial class run_fate_calamity_drop_regression : LifecycleTestSceneTree
{
    private const string TestWorldConfig = "res://data/configs/world_map/test_world_map_config.tres";

    private static readonly StringName StatusBlackStarBrandElite = "black_star_brand_elite";
    private static readonly StringName StatusDoomSentenceVerdict = "doom_sentence_verdict";
    private static readonly StringName FortuneMarkTargetStatId = "fortune_mark_target";
    private static readonly StringName BossTargetStatId = "boss_target";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestOrdinaryBattleCalamityConversionRespectsChapterCap();
        TestEliteBossLootPathsBypassOrdinaryChapterCap();
        TestBrandedEliteGrantsFixedCalamityShard();
        TestBossTargetWithoutFortuneMarkCountsAsEliteOrBoss();
        TestDoomSentenceBossDefeatReturnsCalamityAndCore();

        RequestTestExit(_test.Finish("Fate calamity drop regression"));
    }

    private void TestOrdinaryBattleCalamityConversionRespectsChapterCap()
    {
        GameSession gameSession = CreateTestSession();
        if (gameSession == null)
            return;
        GameRuntimeFacade facade = new();
        try
        {
            facade.Setup(gameSession);
            PartyState partyState = facade.GetPartyState();
            ResetPartyWarehouse(partyState);
            EnsureCapacity(partyState, 10);
            SeedRegularBattleShardFlags(partyState, 2);

            BattleResolutionResult resolutionResult =
                BattleObjectiveTestFactory.CreateEliminationResolution("player");
            resolutionResult.SetLootEntries(new[]
            {
                BuildLootEntry(
                    BattleLootIds.ToStringName(BattleLootSourceKind.CalamityConversion),
                    BattleLootIds.ToStringName(BattleLootSourceIdKind.OrdinaryBattle),
                    "ordinary_conversion",
                    BattleLootIds.ToStringName(BattleLootSpecialItemKind.CalamityShard),
                    3
                ),
            });

            GameRuntimeBattleLootCommitService.BattleLootCommitResult commitResult = facade.CommitBattleLootToSharedWarehouseTyped(
                resolutionResult
            );
            _test.True(commitResult.Ok, "普通战 calamity 结算应能正常提交。");
            _test.Eq(
                commitResult.CommittedItemCount,
                2,
                "章节内已拿 2 个碎片后，普通战结算最多还能提交 2 个。"
            );
            _test.Eq(
                CountStackQuantity(partyState, BattleLootIds.ToStringName(BattleLootSpecialItemKind.CalamityShard)),
                2,
                "普通战结算应只向仓库写入剩余额度内的碎片。"
            );
            _test.Eq(
                GetRegularBattleShardFlagCount(partyState),
                4,
                "普通战结算成功后，应补齐本章 4 个碎片上限标记。"
            );
            _test.Eq(
                CountMatchingLootQuantity(
                    resolutionResult.loot_entries,
                    BattleLootIds.ToStringName(BattleLootSpecialItemKind.CalamityShard),
                    BattleLootIds.ToStringName(BattleLootSourceKind.CalamityConversion),
                    BattleLootIds.ToStringName(BattleLootSourceIdKind.OrdinaryBattle)
                ),
                2,
                "结算结果中的普通战碎片数量应在提交前被裁切到章节剩余额度。"
            );
        }
        finally
        {
            facade.Dispose();
            CleanupTestSession(gameSession);
        }
    }

    private void TestEliteBossLootPathsBypassOrdinaryChapterCap()
    {
        GameSession gameSession = CreateTestSession();
        if (gameSession == null)
            return;
        GameRuntimeFacade facade = new();
        try
        {
            facade.Setup(gameSession);
            PartyState partyState = facade.GetPartyState();
            ResetPartyWarehouse(partyState);
            EnsureCapacity(partyState, 16);
            SeedRegularBattleShardFlags(
                partyState,
                BattleLootIds.OrdinaryBattleCalamityShardChapterCap
            );

            BattleResolutionResult resolutionResult =
                BattleObjectiveTestFactory.CreateEliminationResolution("player");
            resolutionResult.SetLootEntries(new[]
            {
                BuildLootEntry(
                    BattleLootIds.ToStringName(BattleLootSourceKind.CalamityConversion),
                    BattleLootIds.ToStringName(BattleLootSourceIdKind.EliteBossBattle),
                    "elite_boss_conversion",
                    BattleLootIds.ToStringName(BattleLootSpecialItemKind.CalamityShard),
                    6
                ),
                BuildLootEntry(
                    BattleLootIds.ToStringName(BattleLootSourceKind.FateStatusDrop),
                    "elite_target",
                    "elite_fixed_shard",
                    BattleLootIds.ToStringName(BattleLootSpecialItemKind.CalamityShard),
                    1
                ),
            });

            GameRuntimeBattleLootCommitService.BattleLootCommitResult commitResult = facade.CommitBattleLootToSharedWarehouseTyped(
                resolutionResult
            );
            _test.True(commitResult.Ok, "elite/boss 旁路掉落应能正常提交。");
            _test.Eq(
                commitResult.CommittedItemCount,
                7,
                "elite/boss 战结算与固定状态掉落不应受到普通战章节上限影响。"
            );
            _test.Eq(
                CountStackQuantity(partyState, BattleLootIds.ToStringName(BattleLootSpecialItemKind.CalamityShard)),
                7,
                "elite/boss 旁路路径应完整写入全部碎片。"
            );
            _test.Eq(
                GetRegularBattleShardFlagCount(partyState),
                4,
                "elite/boss 旁路路径不应污染普通战章节上限标记。"
            );
        }
        finally
        {
            facade.Dispose();
            CleanupTestSession(gameSession);
        }
    }

    private void TestBrandedEliteGrantsFixedCalamityShard()
    {
        BattleRuntimeModule runtime = BuildRuntime();
        try
        {
            BattleState state = BuildFinishedBattleState("brand_elite_resolution");
            BattleUnitState elite = BuildEnemyUnit("brand_elite_target", "被烙印精英", true, false);
            SetStatus(elite, StatusBlackStarBrandElite, "hero");
            elite.MarkDead();
            elite.SetCurrentHp(0);
            state.SetUnit(elite);
            state.enemy_unit_ids.Add(elite.unit_id);
            runtime.SetupStateForTests(state);

            BattleResolutionResult result = runtime._build_battle_resolution_result();
            _test.Eq(
                CountMatchingLootQuantity(
                    result.loot_entries,
                    BattleLootIds.ToStringName(BattleLootSpecialItemKind.CalamityShard),
                    BattleLootIds.ToStringName(BattleLootSourceKind.FateStatusDrop),
                    "brand_elite_target"
                ),
                1,
                "被黑星烙印终结的 elite 应固定掉落 1 个 calamity_shard。"
            );
        }
        finally
        {
            runtime.dispose();
        }
    }

    private void TestBossTargetWithoutFortuneMarkCountsAsEliteOrBoss()
    {
        BattleRuntimeModule runtime = BuildRuntime();
        try
        {
            BattleState state = BuildFinishedBattleState("boss_target_only_resolution");
            BattleUnitState boss = BuildEnemyUnit("boss_target_only", "Boss 标记目标", false, true);
            SetStatus(boss, StatusBlackStarBrandElite, "hero");
            boss.MarkDead();
            boss.SetCurrentHp(0);
            state.SetUnit(boss);
            state.enemy_unit_ids.Add(boss.unit_id);
            runtime.SetupStateForTests(state);

            BattleResolutionResult result = runtime._build_battle_resolution_result();
            _test.Eq(
                CountMatchingLootQuantity(
                    result.loot_entries,
                    BattleLootIds.ToStringName(BattleLootSpecialItemKind.CalamityShard),
                    BattleLootIds.ToStringName(BattleLootSourceKind.FateStatusDrop),
                    "boss_target_only"
                ),
                1,
                "只有 boss_target 标记、没有 fortune_mark_target 的目标也应走 elite/boss 固定掉落判定。"
            );
        }
        finally
        {
            runtime.dispose();
        }
    }

    private void TestDoomSentenceBossDefeatReturnsCalamityAndCore()
    {
        BattleRuntimeModule runtime = BuildRuntime();
        try
        {
            BattleState state = BuildFinishedBattleState("doom_sentence_boss_resolution");
            BattleUnitState boss = BuildEnemyUnit("doom_boss_target", "章末 Boss", true, true);
            SetStatus(boss, StatusDoomSentenceVerdict, "hero");
            boss.MarkDead();
            boss.SetCurrentHp(0);
            state.SetUnit(boss);
            state.enemy_unit_ids.Add(boss.unit_id);
            runtime.SetupStateForTests(state);

            BattleResolutionResult result = runtime._build_battle_resolution_result();
            _test.Eq(
                runtime.GetDoomSentenceRefundCalamityTotal(),
                5,
                "boss 在厄命宣判下死亡时应返还 5 点 calamity，用于后续碎片结算。"
            );
            _test.Eq(
                CountMatchingLootQuantity(
                    result.loot_entries,
                    BattleLootIds.ToStringName(BattleLootSpecialItemKind.BlackCrownCore),
                    BattleLootIds.ToStringName(BattleLootSourceKind.FateStatusDrop),
                    "doom_boss_target"
                ),
                1,
                "boss 在厄命宣判下死亡时应固定掉落 1 个 black_crown_core。"
            );
            _test.Eq(
                CountMatchingLootQuantity(
                    result.loot_entries,
                    BattleLootIds.ToStringName(BattleLootSpecialItemKind.CalamityShard),
                    BattleLootIds.ToStringName(BattleLootSourceKind.CalamityConversion),
                    BattleLootIds.ToStringName(BattleLootSourceIdKind.EliteBossBattle)
                ),
                2,
                "宣判击杀返还的 calamity 应在战后折算为 2 个 calamity_shard。"
            );
        }
        finally
        {
            runtime.dispose();
        }
    }

    private static BattleRuntimeModule BuildRuntime()
    {
        BattleRuntimeModule runtime = new();
        runtime.setup();
        return runtime;
    }

    private static BattleState BuildFinishedBattleState(StringName battleId)
    {
        BattleState state = new()
        {
            battle_id = battleId,
            phase = "battle_ended",
        };
        BattleObjectiveTestFactory.SetEliminationDecision(state, "player");
        return state;
    }

    private static BattleUnitState BuildEnemyUnit(
        StringName unitId,
        string displayName,
        bool isEliteOrBoss,
        bool isBoss
    )
    {
        BattleUnitState unit = new BattleUnitState()
        {
            unit_id = unitId,
            display_name = displayName,
            faction_id = "enemy",
        }.WithCombatResourcesForTest(
            hp: 60,
            isAlive: true
        );
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 60);
        unit.attribute_snapshot.SetValue(FortuneMarkTargetStatId, isEliteOrBoss ? 1 : 0);
        unit.attribute_snapshot.SetValue(BossTargetStatId, isBoss ? 1 : 0);
        return unit;
    }

    private static void SetStatus(
        BattleUnitState unitState,
        StringName statusId,
        StringName sourceUnitId = default
    )
    {
        if (unitState == null || statusId == default)
            return;
        BattleStatusEffectState statusEntry = new()
        {
            status_id = statusId,
            source_unit_id = sourceUnitId,
            power = 1,
            stacks = 1,
            duration = 60,
        };
        unitState.SetStatusEffect(statusEntry);
    }

    private GameSession CreateTestSession()
    {
        GameSession gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        int createError = gameSession.CreateNewSave(TestWorldConfig);
        _test.Eq(createError, (int)Error.Ok, "GameSession 应能为灾厄掉落回归创建测试存档。");
        if (createError == (int)Error.Ok)
            return gameSession;
        CleanupTestSession(gameSession);
        return null;
    }

    private static void CleanupTestSession(GameSession gameSession)
    {
        if (gameSession == null)
            return;
        gameSession.ClearPersistedGame();
        gameSession.Free();
    }

    private static void ResetPartyWarehouse(PartyState partyState)
    {
        if (partyState == null)
            return;
        partyState.warehouse_state = new WarehouseState();
    }

    private static void EnsureCapacity(PartyState partyState, int storageSpace)
    {
        if (partyState == null)
            return;
        foreach (PartyMemberState memberState in partyState.GetMemberStates())
        {
            if (memberState?.progression?.unit_base_attributes == null)
                continue;
            memberState
                .progression
                .unit_base_attributes
                .SetAttributeValue(
                    PartyWarehouseService.StorageSpaceAttributeId,
                    Mathf.Max(storageSpace, 0)
                );
            return;
        }
    }

    private static void SeedRegularBattleShardFlags(PartyState partyState, int count)
    {
        if (partyState == null)
            return;
        int cap = BattleLootIds.OrdinaryBattleCalamityShardChapterCap;
        for (int slotIndex = 0; slotIndex < cap; slotIndex++)
            partyState.ClearFateRunFlag(BuildRegularBattleShardFlagId(slotIndex));
        int seededCount = Mathf.Min(Mathf.Max(count, 0), cap);
        for (int slotIndex = 0; slotIndex < seededCount; slotIndex++)
            partyState.SetFateRunFlag(BuildRegularBattleShardFlagId(slotIndex), true);
    }

    private static int GetRegularBattleShardFlagCount(PartyState partyState)
    {
        if (partyState == null)
            return 0;
        int flagCount = 0;
        int cap = BattleLootIds.OrdinaryBattleCalamityShardChapterCap;
        for (int slotIndex = 0; slotIndex < cap; slotIndex++)
        {
            if (partyState.GetFateRunFlag(BuildRegularBattleShardFlagId(slotIndex), false))
                flagCount++;
        }
        return flagCount;
    }

    private static StringName BuildRegularBattleShardFlagId(int slotIndex)
    {
        return new StringName(
            $"{BattleLootIds.CalamityShardChapterFlagPrefix}{Mathf.Max(slotIndex, 0)}"
        );
    }

    private static int CountStackQuantity(PartyState partyState, StringName itemId)
    {
        if (partyState?.warehouse_state == null)
            return 0;
        int totalQuantity = 0;
        foreach (WarehouseStackState stack in partyState.warehouse_state.stacks)
        {
            if (stack == null || stack.item_id != itemId)
                continue;
            totalQuantity += stack.quantity;
        }
        return totalQuantity;
    }

    private static BattleLootEntry BuildLootEntry(
        StringName dropSourceKind,
        StringName dropSourceId,
        string dropEntryId,
        StringName itemId,
        int quantity
    )
    {
        return BattleLootEntry.CreateItem(
            BattleLootIds.ToSourceKind(dropSourceKind),
            dropSourceId,
            dropSourceId.ToString(),
            dropEntryId,
            itemId,
            quantity
        );
    }

    private static int CountMatchingLootQuantity(
        System.Collections.Generic.IEnumerable<BattleLootEntry> lootEntries,
        StringName itemId,
        StringName dropSourceKind,
        StringName dropSourceId
    )
    {
        int totalQuantity = 0;
        if (lootEntries == null)
            return totalQuantity;
        foreach (BattleLootEntry lootEntry in lootEntries)
        {
            if (lootEntry == null)
                continue;
            if (lootEntry.ItemId != itemId)
                continue;
            if (BattleLootIds.ToStringName(lootEntry.SourceKind) != dropSourceKind)
                continue;
            if (lootEntry.SourceId != dropSourceId)
                continue;
            totalQuantity += lootEntry.Quantity;
        }
        return totalQuantity;
    }

}
