using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_battle_loot_commit_service_regression : LifecycleTestSceneTree
{
    private const string TestWorldConfig = "res://data/configs/world_map/test_world_map_config.tres";
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        RunTest(
            nameof(TestEquipmentInstanceCommitKeepsPublicDictionaryShape),
            TestEquipmentInstanceCommitKeepsPublicDictionaryShape
        );
        RunTest(
            nameof(TestEquipmentInstanceAliasPayloadIsRejected),
            TestEquipmentInstanceAliasPayloadIsRejected
        );
        RunTest(
            nameof(TestStringNameDropEntryFieldsAreRejected),
            TestStringNameDropEntryFieldsAreRejected
        );
        RunTest(
            nameof(TestBattleSessionWaitOrResolveTypedDropsInvalidRewardsAndFinalizes),
            TestBattleSessionWaitOrResolveTypedDropsInvalidRewardsAndFinalizes
        );
        RunTest(
            nameof(TestBattleStartConfirmRequiresExplicitConfirmBeforeTickAdvances),
            TestBattleStartConfirmRequiresExplicitConfirmBeforeTickAdvances
        );
        RunTest(
            nameof(TestBattleOverflowFeedbackSurfacesInMessageAndSnapshot),
            TestBattleOverflowFeedbackSurfacesInMessageAndSnapshot
        );

        RequestTestExit(_test.Finish("Battle loot commit service regression"));
    }

    private void RunTest(string name, System.Action test)
    {
        try
        {
            test?.Invoke();
        }
        catch (System.Exception ex)
        {
            _test.Fail($"{name} threw {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void TestEquipmentInstanceCommitKeepsPublicDictionaryShape()
    {
        RuntimeFixture fixture = BuildFixture(capacity: 3);
        try
        {
            BattleResolutionResult battleResolutionResult = BuildPlayerResolutionWithLootEntry(
                BuildFormalEquipmentInstanceLootEntry("iron_sword", "eq_000001")
            );
            var typedResult = fixture.Service.CommitBattleLootToSharedWarehouseTyped(
                battleResolutionResult
            );
            _test.True(typedResult.Ok, "装备实例掉落应提交成功。");
            if (!typedResult.Ok)
            {
                _test.Fail(
                    $"装备实例掉落正式提交失败错误码不应出现。 actual={typedResult.ErrorCode}"
                );
            }
            _test.Eq(typedResult.CommittedItemCount, 1, "提交成功应保留 committed_item_count。");
            _test.Eq(typedResult.OverflowEntries.Count, 0, "容量充足时不应产生 overflow。");
            _test.Eq(
                fixture.PartyState.warehouse_state.equipment_instances.Count,
                1,
                "装备实例应写入共享仓库。"
            );
            if (fixture.PartyState.warehouse_state.equipment_instances.Count > 0)
            {
                _test.Eq(
                    fixture.PartyState.warehouse_state.equipment_instances[0].item_id,
                    new StringName("iron_sword"),
                    "写入仓库的装备实例应保留 item_id。"
                );
            }
        }
        finally
        {
            fixture.Dispose();
        }
    }

    private void TestEquipmentInstanceAliasPayloadIsRejected()
    {
        RuntimeFixture fixture = BuildFixture(capacity: 3);
        try
        {
            bool threw = false;
            try
            {
                BattleLootEntryPayload.FormalDropEntryPayloadToTyped(
                    BuildFormalEquipmentInstanceLootEntry(
                        "iron_sword",
                        "eq_000002",
                        "equipment_instance_data"
                    )
                );
            }
            catch (KeyNotFoundException)
            {
                threw = true;
            }
            _test.True(threw, "旧 equipment_instance_data alias 现在应沿正式链抛出 KeyNotFoundException。");
            _test.Eq(
                fixture.PartyState.warehouse_state.equipment_instances.Count,
                0,
                "失败提交不应修改共享仓库。"
            );
        }
        finally
        {
            fixture.Dispose();
        }
    }

    private static GDictionary BuildFormalEquipmentInstanceLootEntry(
        string itemId,
        string instanceId,
        string payloadKey = "equipment_instance"
    )
    {
        return new GDictionary
        {
            ["drop_type"] = BattleLootIds.ToStringName(BattleLootDropKind.EquipmentInstance).ToString(),
            ["drop_source_kind"] = "enemy_unit",
            ["drop_source_id"] = "wolf_alpha",
            ["drop_source_label"] = "Wolf Alpha",
            ["drop_entry_id"] = $"enemy_unit_wolf_alpha_{instanceId}",
            ["item_id"] = itemId,
            ["quantity"] = 1,
            [payloadKey] = EquipmentInstanceState.CreateInstance(itemId, instanceId).ToDictionary(),
        };
    }

    private static BattleResolutionResult BuildPlayerResolutionWithLootEntry(
        GDictionary lootEntryPayload
    )
    {
        BattleLootEntry lootEntry = BattleLootEntryPayload.FormalDropEntryPayloadToTyped(
            lootEntryPayload
        );
        BattleResolutionResult result = new() { winner_faction_id = "player" };
        result.SetLootEntries(
            lootEntry != null ? new[] { lootEntry } : System.Array.Empty<BattleLootEntry>()
        );
        return result;
    }

    private void TestStringNameDropEntryFieldsAreRejected()
    {
        string[] formalStringFields =
        {
            "drop_type",
            "drop_source_kind",
            "drop_source_id",
            "drop_source_label",
            "drop_entry_id",
            "item_id",
        };
        foreach (string fieldName in formalStringFields)
        {
            GDictionary payload = BuildFormalEquipmentInstanceLootEntry("iron_sword", $"eq_{fieldName}");
            payload[fieldName] = new StringName(payload[fieldName].AsString());
            _test.True(
                BattleLootEntryPayload.NormalizeFormalDropEntryPayload(payload) == null,
                $"StringName {fieldName} 不应被 battle loot drop entry 当作正式字符串字段。"
            );
        }

        GDictionary unknownFieldPayload = BuildFormalEquipmentInstanceLootEntry(
            "iron_sword",
            "eq_unknown_field"
        );
        unknownFieldPayload["legacy_item_id"] = "iron_sword";
        _test.True(
            BattleLootEntryPayload.NormalizeFormalDropEntryPayload(unknownFieldPayload) == null,
            "旧 legacy_item_id 字段不应被 battle loot drop entry 当作正式字段。"
        );
    }

    private void TestBattleSessionWaitOrResolveTypedDropsInvalidRewardsAndFinalizes()
    {
        BattleSessionFacadeFixture fixture = BuildBattleSessionInvalidRewardFixture();
        if (fixture == null)
            return;
        try
        {
            GameRuntimeFacade.RuntimeCommandResult commandResult =
                fixture.Facade.CommandBattleWaitOrResolveTyped();

            _test.True(
                commandResult.Ok,
                "非法战后奖励应记录并丢弃，但 battle.wait_or_resolve 应成功。"
            );
            _test.True(
                fixture.BattleRuntime.GetBattleResolutionResult() == null,
                "战后结算成功时应消费 canonical battle result。"
            );
            _test.True(
                fixture.Runtime.GetPartyState().GetPendingCharacterReward("hero_reward") == null,
                "缺失 skill_def 的 pending character reward 不应写入 party_state。"
            );

            IReadOnlyList<object> logEntries = PlainList(
                fixture.GameSession.GetLogSnapshotPlain(),
                "entries"
            );
            _test.Eq(
                CountRecentLogEntries(logEntries, "battle.loot_dropped"),
                1,
                "缺失 item_def 的战斗掉落应只记录一次 battle.loot_dropped 日志。"
            );
            _test.Eq(
                CountRecentLogEntries(logEntries, "battle.pending_reward_dropped"),
                1,
                "缺失 skill_def 的战斗角色奖励应只记录一次 battle.pending_reward_dropped 日志。"
            );
            _test.True(
                FindRecentLogEntry(logEntries, "battle.resolved").Count > 0,
                "非法奖励丢弃后仍应写入 battle.resolved 日志。"
            );
        }
        finally
        {
            fixture.Dispose();
        }
    }

    private void TestBattleStartConfirmRequiresExplicitConfirmBeforeTickAdvances()
    {
        WorldBattleFixture fixture = BuildWorldBattleFixture();
        try
        {
            using GodotProjectionLease<GDictionary> worldDataLease =
                fixture.GameSession.GetWorldDataLease();
            EncounterAnchorData encounterAnchor = FindEncounterAnchorByKind(
                worldDataLease.Value,
                EncounterAnchorData.ToStringName(EncounterAnchorKind.Single)
            );
            _test.True(encounterAnchor != null, "确认开战回归需要至少一个单体野怪遭遇。");
            if (encounterAnchor == null)
                return;

            fixture.GameSession.SetBattleSaveLock(true);
            fixture.Facade.StartBattle(encounterAnchor);
            using GodotProjectionLease<GDictionary> startedSnapshotLease =
                fixture.Facade.BuildHeadlessSnapshotLease();
            GDictionary startedSnapshot = DictDictionary(
                startedSnapshotLease.Value,
                "battle"
            );
            _test.True(DictBool(startedSnapshot, "active", false), "正式 battle start 后应处于 battle active。");
            _test.True(
                DictBool(startedSnapshot, "start_confirm_visible", false),
                "正式 battle start 后应先弹出开始战斗确认。"
            );
            _test.Eq(
                DictString(startedSnapshot, "modal_state", ""),
                "start_confirm",
                "确认前 battle modal_state 应保持在 start_confirm。"
            );
            _test.Eq(
                fixture.Facade._battle_runtime.GetState().timeline.current_tu,
                0,
                "确认前 TU 应从 0 开始。"
            );

            fixture.Facade.advance(2);
            _test.Eq(
                fixture.Facade._battle_runtime.GetState().timeline.current_tu,
                0,
                "未确认开始战斗前，TU 不应增长。"
            );

            GameRuntimeFacade.RuntimeCommandResult confirmResult =
                fixture.Facade.CommandConfirmBattleStartTyped();
            _test.True(confirmResult.Ok, "确认开始战斗命令应成功。");
            using GodotProjectionLease<GDictionary> confirmedSnapshotLease =
                fixture.Facade.BuildHeadlessSnapshotLease();
            GDictionary confirmedSnapshot = DictDictionary(
                confirmedSnapshotLease.Value,
                "battle"
            );
            _test.False(
                DictBool(confirmedSnapshot, "start_confirm_visible", true),
                "确认后开始战斗确认窗应关闭。"
            );
            _test.Eq(
                DictString(confirmedSnapshot, "modal_state", ""),
                "",
                "确认后 battle modal_state 应清空。"
            );

            GameRuntimeFacade.RuntimeCommandResult tickResult =
                fixture.Facade.CommandBattleTickTyped(1);
            _test.True(tickResult.Ok, "确认后 battle tick 应成功。");
            _test.Eq(
                fixture.Facade._battle_runtime.GetState().timeline.current_tu,
                5,
                "确认后 battle tick 1 秒应推进 5 TU。"
            );
        }
        finally
        {
            fixture.Dispose();
        }
    }

    private void TestBattleOverflowFeedbackSurfacesInMessageAndSnapshot()
    {
        WorldBattleFixture fixture = BuildWorldBattleFixture();
        try
        {
            ForcePartyStorageCapacity(fixture.Facade.GetPartyState(), 1);
            fixture.Facade.GetPartyState().warehouse_state = new WarehouseState();

            PartyWarehouseService warehouseService = new();
            warehouseService.Setup(
                fixture.Facade.GetPartyState(),
                fixture.GameSession.GetItemDefsTyped()
            );
            warehouseService.AddItemTyped("healing_herb", 1);

            using GodotProjectionLease<GDictionary> worldDataLease =
                fixture.GameSession.GetWorldDataLease();
            EncounterAnchorData encounterAnchor = FindEncounterAnchorByKind(
                worldDataLease.Value,
                EncounterAnchorData.ToStringName(EncounterAnchorKind.Settlement)
            );
            _test.True(encounterAnchor != null, "battle overflow 反馈回归需要一个聚落类野怪遭遇。");
            if (encounterAnchor == null)
                return;

            encounterAnchor.growth_stage = 3;
            fixture.GameSession.SetBattleSaveLock(true);
            fixture.Facade.StartBattle(encounterAnchor);
            GameRuntimeFacade.RuntimeCommandResult confirmResult =
                fixture.Facade.CommandConfirmBattleStartTyped();
            _test.True(
                confirmResult.Ok,
                "battle overflow 反馈回归中，开始战斗确认命令应成功返回。"
            );
            MarkActiveBattleAsPlayerVictory(fixture.Facade);
            GameRuntimeFacade.RuntimeCommandResult resolveResult =
                fixture.Facade.CommandBattleWaitOrResolveTyped();

            warehouseService.Setup(
                fixture.Facade.GetPartyState(),
                fixture.GameSession.GetItemDefsTyped()
            );
            _test.True(resolveResult.Ok, "battle overflow 反馈回归中，结束战斗命令应成功返回。");
            _test.Eq(
                warehouseService.CountItem("healing_herb"),
                1,
                "battle overflow 后原有仓库占位物应保留。"
            );
            _test.Eq(
                warehouseService.CountItem("beast_hide"),
                0,
                "battle overflow 后 beast_hide 不应误写入共享仓库。"
            );

            using GodotProjectionLease<GDictionary> snapshotLease =
                fixture.Facade.BuildHeadlessSnapshotLease();
            GDictionary snapshot = snapshotLease.Value;
            GDictionary lootSnapshot = DictDictionary(snapshot, "loot");
            Godot.Collections.Array lootEntries = DictArray(lootSnapshot, "loot_entries");
            Godot.Collections.Array overflowEntries = DictArray(lootSnapshot, "overflow_entries");
            _test.True(
                DictBool(lootSnapshot, "commit_ok", false),
                $"headless snapshot 应保留本次战斗 loot commit 成功状态。 loot={lootSnapshot}"
            );
            _test.Eq(
                DictInt(lootSnapshot, "loot_entry_count", 0),
                lootEntries.Count,
                "headless snapshot 的 loot_entry_count 应匹配正式 loot_entries 数量。"
            );
            _test.Eq(
                DictInt(lootSnapshot, "overflow_entry_count", 0),
                overflowEntries.Count,
                "headless snapshot 的 overflow_entry_count 应匹配正式 overflow_entries 数量。"
            );
            _test.True(
                HasLootEntry(lootEntries, "beast_hide", 6),
                $"headless snapshot 应暴露本次战斗原始 loot entry（4 荒狼各 1 张 + 狼王 2 张兽皮）。 entries={lootEntries}"
            );
            _test.True(
                HasLootEntry(overflowEntries, "beast_hide", 6),
                $"headless snapshot 应暴露本次战斗 overflow entry（4 荒狼各 1 张 + 狼王 2 张兽皮）。 entries={overflowEntries}"
            );

            IReadOnlyDictionary<string, object> resolvedLog = FindRecentLogEntry(
                PlainList(fixture.GameSession.GetLogSnapshotPlain(), "entries"),
                "battle.resolved"
            );
            _test.True(resolvedLog.Count > 0, "battle overflow 结算后应写入 battle.resolved 日志。");
            _test.False(
                fixture.GameSession.IsBattleSaveLocked(),
                "battle overflow 结算完成后应释放 battle save lock。"
            );
        }
        finally
        {
            fixture.Dispose();
        }
    }

    private static RuntimeFixture BuildFixture(int capacity)
    {
        Dictionary<StringName, ItemDefinition> itemDefinitions = BuildItemDefinitions();
        PartyState partyState = BuildPartyState(capacity);
        PartyWarehouseService warehouseService = new();
        warehouseService.Setup(partyState, itemDefinitions);

        GameSession gameSession = GameSessionTestFactory.CreateSyntheticFromProcessSnapshot(
            seed => seed.Items = itemDefinitions
        );
        GameRuntimeFacade runtime = new()
        {
            _game_session = gameSession,
            _party_state = partyState,
            _party_warehouse_service = warehouseService,
            _equipment_drop_service = new EquipmentDropService(),
        };
        runtime._battle_loot_commit_service.Setup(runtime);
        return new RuntimeFixture(runtime, gameSession, runtime._battle_loot_commit_service, partyState);
    }

    private static PartyState BuildPartyState(int capacity)
    {
        PartyState partyState = new()
        {
            leader_member_id = "hero",
            main_character_member_id = "hero",
            active_member_ids = new Godot.Collections.Array<StringName> { "hero" },
            warehouse_state = new WarehouseState(),
        };
        PartyMemberState memberState = new()
        {
            member_id = "hero",
            display_name = "Hero",
        };
        memberState.progression.unit_id = "hero";
        memberState.progression.display_name = "Hero";
        memberState
            .progression
            .unit_base_attributes
            .SetAttributeValue(PartyWarehouseService.StorageSpaceAttributeId, capacity);
        partyState.SetMemberState(memberState);
        return partyState;
    }

    private static Dictionary<StringName, ItemDefinition> BuildItemDefinitions()
    {
        ItemDef sword = new()
        {
            item_id = "iron_sword",
            display_name = "Iron Sword",
            CategoryKind = ItemCategoryKind.Equipment,
            is_stackable = false,
            max_stack = 1,
            EquipmentTypeKind = ItemEquipmentTypeKind.Weapon,
            equipment_slot_ids = new GStringArray
            {
                EquipmentRules.ToStringName(EquipmentSlotKind.MainHand).ToString(),
            },
        };
        ItemDefinition swordDefinition = TestResourceOwnership
            .Own(sword, "BattleLootCommitService.BuildItemDefinitions.iron_sword")
            .ToDefinition();
        return new Dictionary<StringName, ItemDefinition>
        {
            [swordDefinition.ItemId] = swordDefinition,
        };
    }

    private BattleSessionFacadeFixture BuildBattleSessionInvalidRewardFixture()
    {
        GameSession gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        int createError = gameSession.CreateNewSave(TestWorldConfig);
        _test.Eq(
            createError,
            (int)Error.Ok,
            "battle session invalid reward fixture 应能创建测试 GameSession。"
        );
        if (createError != (int)Error.Ok)
        {
            gameSession.Free();
            return null;
        }

        GameRuntimeFacade runtime = new();
        runtime.Setup(gameSession);
        BattleRuntimeModule battleRuntime = runtime.GetBattleRuntime();
        BattleState endedState = BuildEndedBattleState();
        PendingCharacterReward reward = BuildCanonicalReward("hero", "battle_skill");
        battleRuntime.GetPendingPostBattleCharacterRewards().Add(reward);
        battleRuntime.SetupStateForTests(endedState);
        battleRuntime.EndBattle(new BattleEndOptions());
        BattleResolutionResult expectedResult = battleRuntime.GetBattleResolutionResult();
        expectedResult.SetLootEntries(new[] { BuildMissingItemLootEntry() });
        runtime.SetRuntimeBattleState(endedState);

        BattleSessionFacade facade = new(new FixedBattleSeedSource(1729));
        facade.Setup(runtime);
        return new BattleSessionFacadeFixture(facade, runtime, gameSession, battleRuntime, expectedResult);
    }

    private static PendingCharacterReward BuildCanonicalReward(string memberId, string skillId)
    {
        PendingCharacterReward reward = new()
        {
            reward_id = $"{memberId}_reward",
            member_id = memberId,
            member_name = memberId,
            source_type = "battle_rating",
            source_id = "battle_rating",
            source_label = "战斗结算",
            summary_text = "战斗评分结算。",
        };
        PendingCharacterRewardEntry entry = new()
        {
            entry_type = "skill_mastery",
            target_id = skillId,
            target_label = skillId,
            amount = 4,
            reason_text = "战斗评分 4 · 渐入佳境",
        };
        reward.entries = new List<PendingCharacterRewardEntry> { entry };
        return reward;
    }

    private static BattleLootEntry BuildMissingItemLootEntry() =>
        BattleLootEntry.CreateItem(
            BattleLootSourceKind.EnemyUnit,
            "wolf_alpha",
            "Wolf Alpha",
            "enemy_unit_wolf_alpha_missing_reward_item",
            "missing_reward_item",
            1
        );

    private static BattleState BuildEndedBattleState()
    {
        return new BattleState
        {
            battle_id = "battle_session",
            seed = 99,
            world_coord = new Vector2I(6, 12),
            encounter_anchor_id = "encounter_session",
            terrain_profile_id = "default",
            phase = "battle_ended",
            winner_faction_id = "player",
            timeline = new BattleTimelineState(),
        };
    }

    private static WorldBattleFixture BuildWorldBattleFixture()
    {
        GameSession gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        int createError = gameSession.CreateNewSave(TestWorldConfig);
        if (createError != (int)Error.Ok)
        {
            gameSession.QueueFree();
            return null;
        }

        GameRuntimeFacade facade = new(new FixedBattleSeedSource(1729));
        facade.Setup(gameSession);
        ConfigureFixedCombatForFacade(facade);
        return new WorldBattleFixture(facade, gameSession);
    }

    private static void ConfigureFixedCombatForFacade(GameRuntimeFacade facade)
    {
        BattleRuntimeModule runtime = facade?.GetBattleRuntime();
        if (runtime == null)
            return;

        runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
        FixedSuccessOneDamageResolver damageResolver = new();
        damageResolver.SetSkillDefinitions(
            facade.GetGameSession().GetContentCatalogTyped().GetSkillDefinitionsTyped()
        );
        runtime.ConfigureDamageResolverForTests(damageResolver);
    }

    private static EncounterAnchorData FindEncounterAnchorByKind(
        GDictionary worldData,
        StringName encounterKind
    )
    {
        foreach (Variant encounterValue in DictArray(worldData, "encounter_anchors"))
        {
            EncounterAnchorData encounterAnchor =
                encounterValue.VariantType == Variant.Type.Dictionary
                    ? EncounterAnchorData.FromDictionary(encounterValue.AsGodotDictionary())
                    : null;
            if (encounterAnchor == null)
                continue;
            if (encounterAnchor.encounter_kind == encounterKind)
                return encounterAnchor;
        }
        return null;
    }

    private static void MarkActiveBattleAsPlayerVictory(GameRuntimeFacade facade)
    {
        BattleState runtimeState = facade?._battle_runtime?.GetState();
        if (runtimeState == null || runtimeState.IsEmpty())
            return;

        BattleUnitState defaultKiller = null;
        if (runtimeState.ally_unit_ids.Count > 0)
        {
            StringName allyUnitId = runtimeState.ally_unit_ids[0];
            if (runtimeState.ContainsUnit(allyUnitId))
                defaultKiller = runtimeState.GetUnit(allyUnitId);
        }

        foreach (StringName enemyUnitId in runtimeState.enemy_unit_ids)
        {
            if (!runtimeState.ContainsUnit(enemyUnitId))
                continue;
            BattleUnitState enemyUnit = runtimeState.GetUnit(enemyUnitId);
            if (enemyUnit == null || !enemyUnit.is_alive)
                continue;
            enemyUnit.is_alive = false;
            facade._battle_runtime._collect_defeated_unit_loot(enemyUnit, defaultKiller);
        }
        runtimeState.phase = "battle_ended";
        runtimeState.winner_faction_id = "player";
        facade.RefreshBattleRuntimeState();
    }

    private static void ForcePartyStorageCapacity(PartyState partyState, int capacity)
    {
        if (partyState == null)
            return;
        int resolvedCapacity = Mathf.Max(capacity, 0);
        bool firstMemberAssigned = false;
        foreach (PartyMemberState memberState in partyState.GetMemberStates())
        {
            if (memberState?.progression?.unit_base_attributes == null)
                continue;
            memberState.progression.unit_base_attributes.custom_stats[
                PartyWarehouseService.StorageSpaceAttributeId
            ] = !firstMemberAssigned ? resolvedCapacity : 0;
            firstMemberAssigned = true;
        }
    }

    private static bool DictBool(GDictionary dictionary, string key, bool fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
    }

    private static int DictInt(GDictionary dictionary, string key, int fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static string DictString(GDictionary dictionary, string key, string fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.String ? value.AsString() : fallback;
    }

    private static string DictIdString(GDictionary dictionary, string key, string fallback = "")
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        Variant value = dictionary[key];
        if (value.VariantType == Variant.Type.StringName)
            return value.AsStringName().ToString();
        if (value.VariantType == Variant.Type.String)
            return value.AsString();
        return fallback;
    }

    private static Godot.Collections.Array DictArray(GDictionary dictionary, string key)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return new Godot.Collections.Array();
        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.Array
            ? value.AsGodotArray()
            : new Godot.Collections.Array();
    }

    private static GDictionary DictDictionary(GDictionary dictionary, string key)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return new GDictionary();
        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new GDictionary();
    }

    private static bool HasLootEntry(Godot.Collections.Array entries, string itemId, int quantity)
    {
        if (entries == null)
            return false;
        foreach (Variant entryValue in entries)
        {
            if (entryValue.VariantType != Variant.Type.Dictionary)
                continue;
            GDictionary entry = entryValue.AsGodotDictionary();
            if (DictIdString(entry, "item_id") == itemId && DictInt(entry, "quantity", 0) == quantity)
                return true;
        }
        return false;
    }

    private static IReadOnlyList<object> PlainList(
        IReadOnlyDictionary<string, object> values,
        string key
    )
    {
        return values != null
            && values.TryGetValue(key, out object value)
            && value is IReadOnlyList<object> items
                ? items
                : System.Array.Empty<object>();
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

    private static IReadOnlyDictionary<string, object> FindRecentLogEntry(
        IReadOnlyList<object> entries,
        string eventId
    )
    {
        if (entries == null)
            return new Dictionary<string, object>();
        for (int index = entries.Count - 1; index >= 0; index--)
        {
            if (entries[index] is not IReadOnlyDictionary<string, object> entry)
                continue;
            if (PlainString(entry, "event_id") == eventId)
                return entry;
        }
        return new Dictionary<string, object>();
    }

    private static int CountRecentLogEntries(
        IReadOnlyList<object> entries,
        string eventId
    )
    {
        if (entries == null)
            return 0;

        int count = 0;
        foreach (object value in entries)
        {
            if (
                value is IReadOnlyDictionary<string, object> entry
                && PlainString(entry, "event_id") == eventId
            )
            {
                count += 1;
            }
        }
        return count;
    }

    private sealed class RuntimeFixture
    {
        public RuntimeFixture(
            GameRuntimeFacade runtime,
            GameSession gameSession,
            GameRuntimeBattleLootCommitService service,
            PartyState partyState
        )
        {
            Runtime = runtime;
            GameSession = gameSession;
            Service = service;
            PartyState = partyState;
        }

        public GameRuntimeFacade Runtime { get; }
        public GameSession GameSession { get; }
        public GameRuntimeBattleLootCommitService Service { get; }
        public PartyState PartyState { get; }

        public void Dispose()
        {
            Runtime?.Dispose();
            GameSession?.QueueFree();
        }
    }

    private sealed class BattleSessionFacadeFixture
    {
        public BattleSessionFacadeFixture(
            BattleSessionFacade facade,
            GameRuntimeFacade runtime,
            GameSession gameSession,
            BattleRuntimeModule battleRuntime,
            BattleResolutionResult expectedResult
        )
        {
            Facade = facade;
            Runtime = runtime;
            GameSession = gameSession;
            BattleRuntime = battleRuntime;
            ExpectedResult = expectedResult;
        }

        public BattleSessionFacade Facade { get; }
        public GameRuntimeFacade Runtime { get; }
        public GameSession GameSession { get; }
        public BattleRuntimeModule BattleRuntime { get; }
        internal BattleResolutionResult ExpectedResult { get; }

        public void Dispose()
        {
            Facade?.Dispose();
            Runtime?.Dispose();
            GameSession?.ClearPersistedGame();
            GameSession?.QueueFree();
        }
    }

    private sealed class WorldBattleFixture
    {
        public WorldBattleFixture(GameRuntimeFacade facade, GameSession gameSession)
        {
            Facade = facade;
            GameSession = gameSession;
        }

        public GameRuntimeFacade Facade { get; }
        public GameSession GameSession { get; }

        public void Dispose()
        {
            Facade?.Dispose();
            GameSession?.ClearPersistedGame();
            GameSession?.QueueFree();
        }
    }
}
