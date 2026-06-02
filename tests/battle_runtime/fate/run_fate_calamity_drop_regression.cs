using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_fate_calamity_drop_regression : SceneTree
{
    private const string TestWorldConfig = "res://data/configs/world_map/test_world_map_config.tres";

    private static readonly StringName StatusBlackStarBrandElite = "black_star_brand_elite";
    private static readonly StringName StatusDoomSentenceVerdict = "doom_sentence_verdict";
    private static readonly StringName FortuneMarkTargetStatId = "fortune_mark_target";
    private static readonly StringName BossTargetStatId = "boss_target";

    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestOrdinaryBattleCalamityConversionRespectsChapterCap();
        TestEliteBossLootPathsBypassOrdinaryChapterCap();
        TestBrandedEliteGrantsFixedCalamityShard();
        TestBossTargetWithoutFortuneMarkCountsAsEliteOrBoss();
        TestDoomSentenceBossDefeatReturnsCalamityAndCore();

        GodotSharpCleanup.collect_pending_finalizers();
        if (_failures.Count == 0)
        {
            GD.Print("Fate calamity drop regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
            GD.PushError(failure);
        GD.Print($"Fate calamity drop regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestOrdinaryBattleCalamityConversionRespectsChapterCap()
    {
        GameSession gameSession = CreateTestSession();
        if (gameSession == null)
            return;
        GameRuntimeFacade facade = new();
        try
        {
            facade.setup(gameSession);
            PartyState partyState = facade.get_party_state();
            ResetPartyWarehouse(partyState);
            EnsureCapacity(partyState, 10);
            SeedRegularBattleShardFlags(partyState, 2);

            BattleResolutionResult resolutionResult = new()
            {
                winner_faction_id = "player",
            };
            resolutionResult.set_loot_entries(new GArray
            {
                BuildLootEntry(
                    BattleLootConstants.SOURCE_KIND_CALAMITY_CONVERSION(),
                    BattleLootConstants.SOURCE_ID_ORDINARY_BATTLE(),
                    "ordinary_conversion",
                    BattleLootConstants.ITEM_CALAMITY_SHARD(),
                    3
                ),
            });

            GDictionary commitResult = facade._commit_battle_loot_to_shared_warehouse(
                resolutionResult
            );
            AssertTrue(DictBool(commitResult, "ok", false), "普通战 calamity 结算应能正常提交。");
            AssertEq(
                DictInt(commitResult, "committed_item_count", -1),
                2,
                "章节内已拿 2 个碎片后，普通战结算最多还能提交 2 个。"
            );
            AssertEq(
                CountStackQuantity(partyState, BattleLootConstants.ITEM_CALAMITY_SHARD()),
                2,
                "普通战结算应只向仓库写入剩余额度内的碎片。"
            );
            AssertEq(
                GetRegularBattleShardFlagCount(partyState),
                4,
                "普通战结算成功后，应补齐本章 4 个碎片上限标记。"
            );
            AssertEq(
                CountMatchingLootQuantity(
                    resolutionResult.loot_entries,
                    BattleLootConstants.ITEM_CALAMITY_SHARD(),
                    BattleLootConstants.SOURCE_KIND_CALAMITY_CONVERSION(),
                    BattleLootConstants.SOURCE_ID_ORDINARY_BATTLE()
                ),
                2,
                "结算结果中的普通战碎片数量应在提交前被裁切到章节剩余额度。"
            );
        }
        finally
        {
            facade.dispose();
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
            facade.setup(gameSession);
            PartyState partyState = facade.get_party_state();
            ResetPartyWarehouse(partyState);
            EnsureCapacity(partyState, 16);
            SeedRegularBattleShardFlags(
                partyState,
                BattleLootConstants.ORDINARY_BATTLE_CALAMITY_SHARD_CHAPTER_CAP()
            );

            BattleResolutionResult resolutionResult = new()
            {
                winner_faction_id = "player",
            };
            resolutionResult.set_loot_entries(new GArray
            {
                BuildLootEntry(
                    BattleLootConstants.SOURCE_KIND_CALAMITY_CONVERSION(),
                    BattleLootConstants.SOURCE_ID_ELITE_BOSS_BATTLE(),
                    "elite_boss_conversion",
                    BattleLootConstants.ITEM_CALAMITY_SHARD(),
                    6
                ),
                BuildLootEntry(
                    BattleLootConstants.SOURCE_KIND_FATE_STATUS_DROP(),
                    "elite_target",
                    "elite_fixed_shard",
                    BattleLootConstants.ITEM_CALAMITY_SHARD(),
                    1
                ),
            });

            GDictionary commitResult = facade._commit_battle_loot_to_shared_warehouse(
                resolutionResult
            );
            AssertTrue(DictBool(commitResult, "ok", false), "elite/boss 旁路掉落应能正常提交。");
            AssertEq(
                DictInt(commitResult, "committed_item_count", -1),
                7,
                "elite/boss 战结算与固定状态掉落不应受到普通战章节上限影响。"
            );
            AssertEq(
                CountStackQuantity(partyState, BattleLootConstants.ITEM_CALAMITY_SHARD()),
                7,
                "elite/boss 旁路路径应完整写入全部碎片。"
            );
            AssertEq(
                GetRegularBattleShardFlagCount(partyState),
                4,
                "elite/boss 旁路路径不应污染普通战章节上限标记。"
            );
        }
        finally
        {
            facade.dispose();
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
            elite.is_alive = false;
            elite.current_hp = 0;
            state.units[elite.unit_id] = elite;
            state.enemy_unit_ids.Add(elite.unit_id);
            runtime._state = state;

            BattleResolutionResult result = runtime._build_battle_resolution_result();
            AssertEq(
                CountMatchingLootQuantity(
                    result.loot_entries,
                    BattleLootConstants.ITEM_CALAMITY_SHARD(),
                    BattleLootConstants.SOURCE_KIND_FATE_STATUS_DROP(),
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
            boss.is_alive = false;
            boss.current_hp = 0;
            state.units[boss.unit_id] = boss;
            state.enemy_unit_ids.Add(boss.unit_id);
            runtime._state = state;

            BattleResolutionResult result = runtime._build_battle_resolution_result();
            AssertEq(
                CountMatchingLootQuantity(
                    result.loot_entries,
                    BattleLootConstants.ITEM_CALAMITY_SHARD(),
                    BattleLootConstants.SOURCE_KIND_FATE_STATUS_DROP(),
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
            boss.is_alive = false;
            boss.current_hp = 0;
            state.units[boss.unit_id] = boss;
            state.enemy_unit_ids.Add(boss.unit_id);
            runtime._state = state;

            BattleResolutionResult result = runtime._build_battle_resolution_result();
            AssertEq(
                DictInt(result.party_resource_commit, "returned_calamity", 0),
                5,
                "boss 在厄命宣判下死亡时应返还 5 点 calamity，用于后续碎片结算。"
            );
            AssertEq(
                CountMatchingLootQuantity(
                    result.loot_entries,
                    BattleLootConstants.ITEM_BLACK_CROWN_CORE(),
                    BattleLootConstants.SOURCE_KIND_FATE_STATUS_DROP(),
                    "doom_boss_target"
                ),
                1,
                "boss 在厄命宣判下死亡时应固定掉落 1 个 black_crown_core。"
            );
            AssertEq(
                CountMatchingLootQuantity(
                    result.loot_entries,
                    BattleLootConstants.ITEM_CALAMITY_SHARD(),
                    BattleLootConstants.SOURCE_KIND_CALAMITY_CONVERSION(),
                    BattleLootConstants.SOURCE_ID_ELITE_BOSS_BATTLE()
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
        runtime.setup(null, new GDictionary(), new GDictionary(), new GDictionary());
        return runtime;
    }

    private static BattleState BuildFinishedBattleState(StringName battleId)
    {
        return new BattleState
        {
            battle_id = battleId,
            winner_faction_id = "player",
            phase = "battle_ended",
        };
    }

    private static BattleUnitState BuildEnemyUnit(
        StringName unitId,
        string displayName,
        bool isEliteOrBoss,
        bool isBoss
    )
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = displayName,
            faction_id = "enemy",
            current_hp = 60,
            is_alive = true,
        };
        unit.attribute_snapshot.set_value(AttributeService.HP_MAX_ID(), 60);
        unit.attribute_snapshot.set_value(FortuneMarkTargetStatId, isEliteOrBoss ? 1 : 0);
        unit.attribute_snapshot.set_value(BossTargetStatId, isBoss ? 1 : 0);
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
        unitState.set_status_effect(statusEntry);
    }

    private GameSession CreateTestSession()
    {
        GameSession gameSession = new();
        int createError = gameSession.create_new_save(TestWorldConfig);
        AssertEq(createError, (int)Error.Ok, "GameSession 应能为灾厄掉落回归创建测试存档。");
        if (createError == (int)Error.Ok)
            return gameSession;
        CleanupTestSession(gameSession);
        return null;
    }

    private static void CleanupTestSession(GameSession gameSession)
    {
        if (gameSession == null)
            return;
        gameSession.clear_persisted_game();
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
        foreach (PartyMemberState memberState in partyState.get_member_states())
        {
            if (memberState?.progression?.unit_base_attributes == null)
                continue;
            memberState
                .progression
                .unit_base_attributes
                .set_attribute_value(
                    PartyWarehouseService.STORAGE_SPACE_ATTRIBUTE_ID(),
                    Mathf.Max(storageSpace, 0)
                );
            return;
        }
    }

    private static void SeedRegularBattleShardFlags(PartyState partyState, int count)
    {
        if (partyState == null)
            return;
        int cap = BattleLootConstants.ORDINARY_BATTLE_CALAMITY_SHARD_CHAPTER_CAP();
        for (int slotIndex = 0; slotIndex < cap; slotIndex++)
            partyState.clear_fate_run_flag(BuildRegularBattleShardFlagId(slotIndex));
        int seededCount = Mathf.Min(Mathf.Max(count, 0), cap);
        for (int slotIndex = 0; slotIndex < seededCount; slotIndex++)
            partyState.set_fate_run_flag(BuildRegularBattleShardFlagId(slotIndex), true);
    }

    private static int GetRegularBattleShardFlagCount(PartyState partyState)
    {
        if (partyState == null)
            return 0;
        int flagCount = 0;
        int cap = BattleLootConstants.ORDINARY_BATTLE_CALAMITY_SHARD_CHAPTER_CAP();
        for (int slotIndex = 0; slotIndex < cap; slotIndex++)
        {
            if (partyState.get_fate_run_flag(BuildRegularBattleShardFlagId(slotIndex), false))
                flagCount++;
        }
        return flagCount;
    }

    private static StringName BuildRegularBattleShardFlagId(int slotIndex)
    {
        return new StringName(
            $"{BattleLootConstants.CALAMITY_SHARD_CHAPTER_FLAG_PREFIX()}{Mathf.Max(slotIndex, 0)}"
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

    private static GDictionary BuildLootEntry(
        StringName dropSourceKind,
        StringName dropSourceId,
        string dropEntryId,
        StringName itemId,
        int quantity
    )
    {
        return new GDictionary
        {
            ["drop_type"] = BattleLootConstants.DROP_TYPE_ITEM().ToString(),
            ["drop_source_kind"] = dropSourceKind.ToString(),
            ["drop_source_id"] = dropSourceId.ToString(),
            ["drop_source_label"] = dropSourceId.ToString(),
            ["drop_entry_id"] = dropEntryId,
            ["item_id"] = itemId.ToString(),
            ["quantity"] = quantity,
        };
    }

    private static int CountMatchingLootQuantity(
        GArray lootEntries,
        StringName itemId,
        StringName dropSourceKind,
        StringName dropSourceId
    )
    {
        int totalQuantity = 0;
        if (lootEntries == null)
            return totalQuantity;
        foreach (Variant lootEntryValue in lootEntries)
        {
            if (lootEntryValue.VariantType != Variant.Type.Dictionary)
                continue;
            GDictionary lootEntry = lootEntryValue.AsGodotDictionary();
            if (ProgressionDataUtils.to_string_name(lootEntry.GetValueOrDefault("item_id", "")) != itemId)
                continue;
            if (
                ProgressionDataUtils.to_string_name(
                    lootEntry.GetValueOrDefault("drop_source_kind", "")
                ) != dropSourceKind
            )
                continue;
            if (
                ProgressionDataUtils.to_string_name(
                    lootEntry.GetValueOrDefault("drop_source_id", "")
                ) != dropSourceId
            )
                continue;
            totalQuantity += DictInt(lootEntry, "quantity", 0);
        }
        return totalQuantity;
    }

    private static bool DictBool(GDictionary dictionary, string key, bool fallback)
    {
        if (dictionary == null || string.IsNullOrEmpty(key) || !dictionary.ContainsKey(key))
            return fallback;
        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
    }

    private static int DictInt(GDictionary dictionary, string key, int fallback)
    {
        if (dictionary == null || string.IsNullOrEmpty(key) || !dictionary.ContainsKey(key))
            return fallback;
        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
            _failures.Add(message);
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
            _failures.Add($"{message} actual={actual} expected={expected}");
    }
}
