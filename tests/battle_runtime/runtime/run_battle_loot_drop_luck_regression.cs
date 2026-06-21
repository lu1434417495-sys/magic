using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;

public partial class run_battle_loot_drop_luck_regression : SceneTree
{
    private const string TestWorldConfig =
        "res://data/configs/world_map/test_world_map_config.tres";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestPerKillLootUsesKillerLuckAndCommitsFixedItem();
        TestPerKillRandomEquipmentWithoutPlayerKillerUsesNeutralLuck();
        TestPerKillRandomEquipmentOverflowIsLostInSettlementCommit();
        TestPerKillAttackEquipmentIsNotImplicitLoot();

        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(_test.Finish("Battle loot drop luck regression"));
    }

    private void TestPerKillLootUsesKillerLuckAndCommitsFixedItem()
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
            StringName mainMemberId = partyState.GetResolvedMainCharacterMemberId();
            PartyMemberState mainMember = partyState.GetMemberState(mainMemberId);
            _test.True(mainMember != null, "测试前置：应能读取主角成员。");
            if (mainMember == null)
                return;

            SetMemberLuck(mainMember, 2, 5);
            PartyMemberState killerMember = AddPartyMember(
                partyState,
                "low_luck_killer",
                "Low Luck Killer"
            );
            SetMemberLuck(killerMember, -6, 0);
            facade.GetCharacterManagement().SetPartyState(partyState);

            using SpyEquipmentDropService dropService = new();
            InjectDropServices(facade, dropService);
            InjectEnemyTemplate(
                facade,
                BuildEnemyTemplateWithMixedLoot("per_kill_loot_wolf")
            );
            AssertRuntimeEnemyTemplate(
                facade,
                "per_kill_loot_wolf",
                2,
                "per-kill mixed loot template 应通过 typed enemy-template index 生效。"
            );

            BattleRuntimeModule battleRuntime = facade.GetBattleRuntime();
            BattleUnitState defeatedEnemy = BuildDefeatedEnemyUnit(
                "per_kill_enemy",
                "per_kill_loot_wolf",
                "战利品荒狼"
            );
            BattleUnitState killerUnit = BuildKillerUnit(
                killerMember.member_id,
                "Low Luck Killer"
            );
            battleRuntime._collect_defeated_unit_loot(defeatedEnemy, killerUnit);
            battleRuntime._collect_defeated_unit_loot(defeatedEnemy, killerUnit);

            BattleResolutionResult resolutionResult = new()
            {
                winner_faction_id = "player",
            };
            resolutionResult.SetLootEntries(battleRuntime._active_loot_entries);
            int equipmentEntries = CountDropType(
                resolutionResult.loot_entries,
                BattleLootIds.ToStringName(BattleLootDropKind.EquipmentInstance)
            );
            GameRuntimeBattleLootCommitService.BattleLootCommitResult commitResult = facade.CommitBattleLootToSharedWarehouseTyped(
                resolutionResult
            );

            _test.Eq(
                dropService.Calls.Count,
                1,
                "fixed item 掉落不应重复调用 equipment_drop_service。"
            );
            if (dropService.Calls.Count > 0)
            {
                DropCall call = dropService.Calls[0];
                _test.Eq(
                    call.ItemId.ToString(),
                    "bronze_sword",
                    "随机装备掉落应保留稳定装备 item_id。"
                );
                _test.Eq(
                    call.Quantity,
                    1,
                    "随机装备掉落应把稳定数量传给 equipment_drop_service。"
                );
                _test.Eq(
                    call.DropLuck,
                    -6,
                    "per-kill 方案应读取击杀者的有效幸运值。"
                );
            }
            _test.Eq(
                equipmentEntries,
                1,
                "BattleResolutionResult 应保存击杀时已解析完成的 equipment_instance 条目。"
            );
            _test.True(commitResult.Ok, "per-kill 掉落应能成功提交到共享仓库。");
            _test.Eq(
                commitResult.OverflowEntryCount,
                0,
                "容量充足时不应产出 overflow entry。"
            );
            _test.Eq(
                commitResult.CommittedItemCount,
                3,
                "1 件装备实例 + 2 个固定材料应共同计入 committed_item_count。"
            );
            _test.Eq(
                partyState.warehouse_state.equipment_instances.Count,
                1,
                "equipment_instance 条目应向共享仓库写入 1 个装备实例。"
            );
            if (partyState.warehouse_state.equipment_instances.Count > 0)
            {
                EquipmentInstanceState equipmentInstance =
                    partyState.warehouse_state.equipment_instances[0];
                _test.Eq(
                    equipmentInstance.item_id.ToString(),
                    "bronze_sword",
                    "equipment_instance 提交后应保留稳定 item_id。"
                );
                _test.Eq(
                    equipmentInstance.rarity,
                    (int)EquipmentInstanceState.RarityTier.COMMON,
                    "低 luck 击杀者应保留击杀时 roll 出的低稀有度。"
                );
            }
            _test.Eq(
                CountStackQuantity(partyState, "beast_hide"),
                2,
                "固定材料掉落应继续按堆叠物品入仓。"
            );
        }
        finally
        {
            facade.Dispose();
            CleanupTestSession(gameSession);
        }
    }

    private void TestPerKillRandomEquipmentWithoutPlayerKillerUsesNeutralLuck()
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
            StringName mainMemberId = partyState.GetResolvedMainCharacterMemberId();
            PartyMemberState mainMember = partyState.GetMemberState(mainMemberId);
            _test.True(mainMember != null, "测试前置：应能读取主角成员。");
            if (mainMember == null)
                return;

            SetMemberLuck(mainMember, 2, 5);
            facade.GetCharacterManagement().SetPartyState(partyState);

            using SpyEquipmentDropService dropService = new();
            InjectDropServices(facade, dropService);
            InjectEnemyTemplate(
                facade,
                BuildEnemyTemplateWithRandomEquipmentOnly("neutral_loot_wolf")
            );
            AssertRuntimeEnemyTemplate(
                facade,
                "neutral_loot_wolf",
                1,
                "neutral random-equipment template 应通过 typed enemy-template index 生效。"
            );

            BattleRuntimeModule battleRuntime = facade.GetBattleRuntime();
            BattleUnitState defeatedEnemy = BuildDefeatedEnemyUnit(
                "neutral_enemy",
                "neutral_loot_wolf",
                "中立掉落荒狼"
            );
            battleRuntime._collect_defeated_unit_loot(defeatedEnemy, null);

            BattleResolutionResult resolutionResult = new()
            {
                winner_faction_id = "player",
            };
            resolutionResult.SetLootEntries(battleRuntime._active_loot_entries);
            GameRuntimeBattleLootCommitService.BattleLootCommitResult commitResult = facade.CommitBattleLootToSharedWarehouseTyped(
                resolutionResult
            );

            _test.True(commitResult.Ok, "没有玩家击杀归属时，战利品仍应能成功提交。");
            _test.Eq(dropService.Calls.Count, 1, "中立掉落路径应调用一次 equipment_drop_service。");
            if (dropService.Calls.Count > 0)
            {
                DropCall call = dropService.Calls[0];
                _test.Eq(call.DropLuck, 0, "缺少玩家击杀者时，应按中性 luck=0 结算随机装备。");
            }
            _test.Eq(
                partyState.warehouse_state.equipment_instances.Count,
                1,
                "中立击杀路径应继续产出 1 个装备实例。"
            );
            if (partyState.warehouse_state.equipment_instances.Count > 0)
            {
                EquipmentInstanceState equipmentInstance =
                    partyState.warehouse_state.equipment_instances[0];
                _test.Eq(
                    equipmentInstance.rarity,
                    (int)EquipmentInstanceState.RarityTier.COMMON,
                    "neutral luck=0 时应保留 spy service 返回的默认稀有度。"
                );
            }
        }
        finally
        {
            facade.Dispose();
            CleanupTestSession(gameSession);
        }
    }

    private void TestPerKillRandomEquipmentOverflowIsLostInSettlementCommit()
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
            EnsureCapacity(partyState, 1);
            facade.GetPartyWarehouseService().Setup(
                partyState,
                gameSession.GetItemDefsTyped(),
                gameSession.AllocateEquipmentInstanceId
            );
            facade.GetPartyWarehouseService().AddItemTyped("bronze_sword", 1);
            facade.GetCharacterManagement().SetPartyState(partyState);

            using SpyEquipmentDropService dropService = new();
            InjectDropServices(facade, dropService);
            InjectEnemyTemplate(
                facade,
                BuildEnemyTemplateWithRandomEquipmentOnly("overflow_loot_wolf")
            );
            AssertRuntimeEnemyTemplate(
                facade,
                "overflow_loot_wolf",
                1,
                "overflow random-equipment template 应通过 typed enemy-template index 生效。"
            );

            BattleRuntimeModule battleRuntime = facade.GetBattleRuntime();
            BattleUnitState defeatedEnemy = BuildDefeatedEnemyUnit(
                "overflow_enemy",
                "overflow_loot_wolf",
                "满包掉落荒狼"
            );
            battleRuntime._collect_defeated_unit_loot(defeatedEnemy, null);

            BattleResolutionResult resolutionResult = new()
            {
                winner_faction_id = "player",
            };
            resolutionResult.SetLootEntries(battleRuntime._active_loot_entries);
            GameRuntimeBattleLootCommitService.BattleLootCommitResult commitResult = facade.CommitBattleLootToSharedWarehouseTyped(
                resolutionResult
            );

            _test.True(commitResult.Ok, "随机装备掉落遇到满包时仍应以丢失方式完成结算。");
            _test.Eq(
                commitResult.CommittedItemCount,
                0,
                "随机装备掉落满包时不应写入新装备实例。"
            );
            _test.Eq(
                commitResult.OverflowEntryCount,
                1,
                "随机装备掉落满包时应记录 overflow entry。"
            );
            _test.Eq(
                resolutionResult.overflow_entries.Count,
                1,
                "随机装备掉落满包时应写入 BattleResolutionResult overflow_entries。"
            );
            if (
                resolutionResult.overflow_entries.Count > 0
            )
            {
                BattleLootEntry overflowEntry = resolutionResult.overflow_entries[0];
                _test.Eq(
                    overflowEntry?.ItemId.ToString() ?? "",
                    "bronze_sword",
                    "随机装备 overflow entry 应保留掉落装备 item_id。"
                );
                _test.Eq(
                    overflowEntry?.Quantity ?? 0,
                    1,
                    "随机装备 overflow entry 应记录丢失件数。"
                );
            }
            _test.Eq(
                partyState.warehouse_state.equipment_instances.Count,
                1,
                "随机装备满包丢失时只应保留原有占位装备。"
            );
        }
        finally
        {
            facade.Dispose();
            CleanupTestSession(gameSession);
        }
    }

    private void TestPerKillAttackEquipmentIsNotImplicitLoot()
    {
        GameSession gameSession = CreateTestSession();
        if (gameSession == null)
            return;

        GameRuntimeFacade facade = new();
        try
        {
            facade.Setup(gameSession);
            InjectEnemyTemplate(
                facade,
                BuildEnemyTemplateWithAttackEquipmentOnly("attack_equipment_only_enemy")
            );

            BattleUnitState defeatedEnemy = BuildDefeatedEnemyUnit(
                "attack_equipment_enemy",
                "attack_equipment_only_enemy",
                "持钉锤敌人"
            );
            BattleRuntimeModule battleRuntime = facade.GetBattleRuntime();
            battleRuntime._collect_defeated_unit_loot(defeatedEnemy, null);

            _test.True(
                battleRuntime._active_loot_entries.Count == 0,
                "敌人死亡不应因为 attack_equipment_item_id 自动掉落攻击装备；per-kill 掉落只读取 drop_entries。"
            );
        }
        finally
        {
            facade.Dispose();
            CleanupTestSession(gameSession);
        }
    }

    private GameSession CreateTestSession()
    {
        GameSession gameSession = new();
        Error createError = (Error)gameSession.CreateNewSave(TestWorldConfig);
        _test.True(
            createError == Error.Ok,
            "GameSession 应能为 per-kill 掉落回归创建测试存档。"
        );
        if (createError == Error.Ok)
            return gameSession;

        CleanupTestSession(gameSession);
        return null;
    }

    private static void CleanupTestSession(GameSession gameSession)
    {
        if (gameSession == null)
            return;
        gameSession.ClearPersistedGame();
        gameSession.Dispose();
    }

    private static void InjectDropServices(
        GameRuntimeFacade facade,
        SpyEquipmentDropService dropService
    )
    {
        if (facade == null)
            return;
        GameContentCatalog catalog = facade.GetContentCatalogTyped();
        facade.GetBattleRuntime()
            ?.setup(
                facade.GetCharacterManagement(),
                facade.GetSkillDefsTyped(),
                catalog?.GetEnemyTemplatesTyped(),
                catalog?.GetEnemyAiBrainsTyped(),
                null,
                dropService,
                facade.GetItemDefsTyped(),
                null,
                facade.GetEquipmentInstanceIdAllocator(),
                catalog?.GetBattleSpecialProfileRegistrySnapshot(),
                facade.GetSkillCatalogTyped()
            );
    }

    private static void InjectEnemyTemplate(GameRuntimeFacade facade, EnemyTemplateDef enemyTemplate)
    {
        BattleRuntimeModule battleRuntime = facade?.GetBattleRuntime();
        if (battleRuntime == null || enemyTemplate == null || enemyTemplate.template_id == "")
            return;
        battleRuntime.ReplaceEnemyTemplatesTyped(
            new Dictionary<StringName, EnemyTemplateDef>
            {
                [enemyTemplate.template_id] = enemyTemplate,
            }
        );
    }

    private void AssertRuntimeEnemyTemplate(
        GameRuntimeFacade facade,
        StringName templateId,
        int expectedDropEntryCount,
        string message
    )
    {
        EnemyTemplateDef runtimeTemplate = facade?.GetBattleRuntime()?.GetEnemyTemplateTyped(templateId);
        _test.True(runtimeTemplate != null, message);
        if (runtimeTemplate == null)
            return;
        _test.Eq(
            runtimeTemplate.drop_entries.Count,
            expectedDropEntryCount,
            $"{message} drop_entries count 应与测试注入一致。"
        );
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

        bool firstMemberAssigned = false;
        foreach (object memberOption in partyState.member_states.Values)
        {
            if (!TryAsPartyMemberState(memberOption, out PartyMemberState memberState))
                continue;
            UnitBaseAttributes attributes = memberState
                .progression
                ?.unit_base_attributes;
            if (attributes == null)
                continue;
            attributes.custom_stats["storage_space"] = firstMemberAssigned
                ? 0
                : Mathf.Max(storageSpace, 0);
            firstMemberAssigned = true;
        }
    }

    private static void SetMemberLuck(
        PartyMemberState memberState,
        int hiddenLuckAtBirth,
        int faithLuckBonus
    )
    {
        UnitBaseAttributes attributes = memberState?.progression?.unit_base_attributes;
        if (attributes == null)
            return;
        attributes.custom_stats["hidden_luck_at_birth"] = hiddenLuckAtBirth;
        attributes.custom_stats["faith_luck_bonus"] = faithLuckBonus;
    }

    private static PartyMemberState AddPartyMember(
        PartyState partyState,
        StringName memberId,
        string displayName
    )
    {
        PartyMemberState memberState = new()
        {
            member_id = memberId,
            display_name = displayName,
        };
        memberState.progression.unit_id = memberId;
        memberState.progression.display_name = displayName;
        partyState.SetMemberState(memberState);
        return memberState;
    }

    private static EnemyTemplateDef BuildEnemyTemplateWithMixedLoot(StringName templateId)
    {
        EnemyTemplateDef template = new()
        {
            template_id = templateId,
            display_name = "战利品荒狼",
        };
        template.drop_entries.Add(new DropEntryDef
        {
            drop_entry_id = "weapon_roll",
            drop_type = "random_equipment",
            item_id = "bronze_sword",
            quantity = 1,
        });
        template.drop_entries.Add(new DropEntryDef
        {
            drop_entry_id = "hide_bundle",
            drop_type = "item",
            item_id = "beast_hide",
            quantity = 2,
        });
        return template;
    }

    private static EnemyTemplateDef BuildEnemyTemplateWithRandomEquipmentOnly(StringName templateId)
    {
        EnemyTemplateDef template = new()
        {
            template_id = templateId,
            display_name = "中立掉落荒狼",
        };
        template.drop_entries.Add(new DropEntryDef
        {
            drop_entry_id = "weapon_roll",
            drop_type = "random_equipment",
            item_id = "bronze_sword",
            quantity = 1,
        });
        return template;
    }

    private static EnemyTemplateDef BuildEnemyTemplateWithAttackEquipmentOnly(StringName templateId)
    {
        return new EnemyTemplateDef
        {
            template_id = templateId,
            display_name = "持钉锤敌人",
            attack_equipment_item_id = "watchman_mace",
        };
    }

    private static BattleUnitState BuildDefeatedEnemyUnit(
        StringName unitId,
        StringName templateId,
        string displayName
    )
    {
        return new BattleUnitState
        {
            unit_id = unitId,
            enemy_template_id = templateId,
            display_name = displayName,
            faction_id = "hostile",
            control_mode = "ai",
            is_alive = false,
        };
    }

    private static BattleUnitState BuildKillerUnit(StringName memberId, string displayName)
    {
        return new BattleUnitState
        {
            unit_id = new StringName($"{memberId}_unit"),
            source_member_id = memberId,
            display_name = displayName,
            faction_id = "player",
            control_mode = "manual",
            is_alive = true,
        };
    }

    private static int CountDropType(IEnumerable<BattleLootEntry> lootEntries, StringName dropType)
    {
        int total = 0;
        foreach (BattleLootEntry lootEntry in lootEntries ?? System.Array.Empty<BattleLootEntry>())
        {
            if (BattleLootIds.ToStringName(lootEntry?.DropKind ?? BattleLootDropKind.Unknown) == dropType)
                total += 1;
        }
        return total;
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

    private static bool TryAsPartyMemberState(object rawValue, out PartyMemberState value)
    {
        if (rawValue is PartyMemberState typedValue)
        {
            value = typedValue;
            return true;
        }

        try
        {
            dynamic dynamicValue = rawValue;
            value = dynamicValue.As<PartyMemberState>();
            return value != null;
        }
        catch
        {
        }

        value = null;
        return false;
    }

    private readonly record struct DropCall(StringName ItemId, int Quantity, int DropLuck);

    private sealed class SpyEquipmentDropService : EquipmentDropService
    {
        public readonly List<DropCall> Calls = new();

        public override List<EquipmentInstanceState> RollItemInstances(
            StringName itemId,
            int quantity,
            int dropLuck
        )
        {
            Calls.Add(new DropCall(itemId, quantity, dropLuck));
            List<EquipmentInstanceState> instances = new();
            for (int index = 0; index < Mathf.Max(quantity, 0); index++)
            {
                EquipmentInstanceState instance =
                    EquipmentInstanceState.CreateTransientInstance(itemId);
                instance.rarity =
                    dropLuck >= 5
                        ? (int)EquipmentInstanceState.RarityTier.EPIC
                        : (int)EquipmentInstanceState.RarityTier.COMMON;
                instances.Add(instance);
            }
            return instances;
        }
    }
}
