using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_contingency_battle_lifecycle_regression : SceneTree
{
    private const string TestConfigPath = "res://data/configs/world_map/test_world_map_config.tres";
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(RunAsync));
    }

    private async void RunAsync()
    {
        try
        {
            TestEndBattleClearsConsumedSetupBeforeResourceClamp();
            TestDeadMemberUsesDeathWritebackWithoutContingencyConsumption();
            await TestVictoryFinalizationPersistsConsumedSetupRelease();
            await TestEscapeFinalizationPersistsConsumedSetupRelease();
            await TestLossWithoutConsumedSetupLeavesChargeUnchanged();
            await TestConsumedSetupFailureRollsBackFinalizationMemory();
            await TestFlushFailureRollsBackFinalizationMemory();
            await TestLowLuckSidecarRollbackSurvivesLateFlushFailure();
            TestEndBattleResourceCommitFailureReturnsTypedResult();
            await TestResourceCommitFailureRollsBackFinalizationMemory();
        }
        catch (Exception ex)
        {
            _test.Fail($"Unhandled exception: {ex.GetType().Name}: {ex.Message}");
        }

        Quit(_test.Finish("Contingency battle lifecycle regression"));
    }

    private void TestEndBattleClearsConsumedSetupBeforeResourceClamp()
    {
        PartyState partyState = BuildPartyState(ChargedSetup("life_mirror"), currentMp: 5);
        using CharacterManagementModule manager = BuildManager(partyState);
        using BattleRuntimeModule battleRuntime = new();
        battleRuntime.setup(character_gateway: manager);

        BattleState battleState = BuildBattleState(
            BattleUnit("hero_unit", "hero", alive: true, currentHp: 20, currentMp: 28)
        );
        battleState.GetUnit("hero_unit").MarkContingencySetupConsumed("life_mirror");
        battleRuntime.SetupStateForTests(battleState);

        BattleEndResult result = battleRuntime.EndBattle(new BattleEndOptions(commitProgression: true));

        PartyMemberState member = partyState.GetMemberState("hero");
        _test.True(result.Ok, $"EndBattle consumed setup commit should succeed. error={result.ErrorCode}");
        AssertSetupReleased(member, "life_mirror", "Consumed setup should be released before battle resources are clamped.");
        _test.Eq(
            member.current_mp,
            28,
            "Battle MP writeback should clamp against post-consumption effective MP max."
        );
    }

    private void TestDeadMemberUsesDeathWritebackWithoutContingencyConsumption()
    {
        PartyState partyState = BuildPartyState(ChargedSetup("death_skip"), currentMp: 5);
        using CharacterManagementModule manager = BuildManager(partyState);
        using BattleRuntimeModule battleRuntime = new();
        battleRuntime.setup(character_gateway: manager);

        BattleState battleState = BuildBattleState(
            BattleUnit("hero_unit", "hero", alive: false, currentHp: 0, currentMp: 0)
        );
        battleState.GetUnit("hero_unit").MarkContingencySetupConsumed("death_skip");
        battleRuntime.SetupStateForTests(battleState);

        BattleEndResult result = battleRuntime.EndBattle(new BattleEndOptions(commitProgression: true));

        PartyMemberState member = partyState.GetMemberState("hero");
        _test.True(result.Ok, $"Dead member battle end should follow death writeback. error={result.ErrorCode}");
        _test.True(member.is_dead, "Dead battle unit should mark the party member dead.");
        AssertSetupCharged(member, "death_skip", "Dead member should not run contingency consumed writeback.");
    }

    private async Task TestVictoryFinalizationPersistsConsumedSetupRelease()
    {
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "victory",
            ChargedSetup("victory_release"),
            winnerFactionId: "player",
            consumedSetupIds: new[] { "victory_release" }
        );
        try
        {
            bool finalized = fixture.Runtime.FinalizeBattleResolution(fixture.ResolutionResult);

            _test.True(finalized, "Victory finalization should succeed.");
            AssertSetupReleased(
                fixture.GameSession.GetPartyState().GetMemberState("hero"),
                "victory_release",
                "Victory finalization should persist charged=false for consumed contingency setup."
            );
            _test.False(
                fixture.GameSession.IsBattleSaveLocked(),
                "Successful victory finalization should release battle save lock."
            );
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    private async Task TestEscapeFinalizationPersistsConsumedSetupRelease()
    {
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "escape",
            ChargedSetup("escape_release"),
            winnerFactionId: "escaped",
            consumedSetupIds: new[] { "escape_release" }
        );
        try
        {
            bool finalized = fixture.Runtime.FinalizeBattleResolution(fixture.ResolutionResult);

            _test.True(finalized, "Escape finalization should succeed under current commit rules.");
            AssertSetupReleased(
                fixture.GameSession.GetPartyState().GetMemberState("hero"),
                "escape_release",
                "Escape finalization should persist charged=false for consumed contingency setup."
            );
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    private async Task TestLossWithoutConsumedSetupLeavesChargeUnchanged()
    {
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "loss_retry",
            ChargedSetup("loss_keeps_charge"),
            winnerFactionId: "hostile",
            consumedSetupIds: Array.Empty<string>()
        );
        try
        {
            bool finalized = fixture.Runtime.FinalizeBattleResolution(fixture.ResolutionResult);

            _test.True(finalized, "Loss finalization without consumed setup IDs should still settle current rules.");
            AssertSetupCharged(
                fixture.GameSession.GetPartyState().GetMemberState("hero"),
                "loss_keeps_charge",
                "Retry/loss path without consumed setup IDs should leave charged setup unchanged."
            );
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    private async Task TestConsumedSetupFailureRollsBackFinalizationMemory()
    {
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "missing_consumed",
            ChargedSetup("rollback_charge"),
            winnerFactionId: "player",
            consumedSetupIds: new[] { "missing_setup" }
        );
        try
        {
            GDictionary sessionBefore = fixture.GameSession.CaptureRuntimeState();
            int mpBefore = fixture.Runtime.GetPartyState().GetMemberState("hero").current_mp;

            bool finalized = fixture.Runtime.FinalizeBattleResolution(fixture.ResolutionResult);

            _test.False(finalized, "Missing consumed setup should fail finalization.");
            AssertSetupCharged(
                fixture.Runtime.GetPartyState().GetMemberState("hero"),
                "rollback_charge",
                "Consumed writeback failure should restore runtime party setup state."
            );
            _test.Eq(
                fixture.Runtime.GetPartyState().GetMemberState("hero").current_mp,
                mpBefore,
                "Consumed writeback failure should restore runtime party vitals."
            );
            AssertRuntimeSaveMetadataEqual(
                fixture.GameSession.CaptureRuntimeState(),
                sessionBefore,
                "Consumed writeback failure should restore session memory snapshot."
            );
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    private async Task TestFlushFailureRollsBackFinalizationMemory()
    {
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "flush_failure",
            ChargedSetup("flush_restore"),
            winnerFactionId: "player",
            consumedSetupIds: new[] { "flush_restore" }
        );
        try
        {
            GDictionary sessionBefore = fixture.GameSession.CaptureRuntimeState();
            fixture.GameSession.fail_payload_write = true;

            bool finalized = fixture.Runtime.FinalizeBattleResolution(fixture.ResolutionResult);

            _test.False(finalized, "Forced FlushGameState failure should return retryable failure.");
            AssertSetupCharged(
                fixture.Runtime.GetPartyState().GetMemberState("hero"),
                "flush_restore",
                "Flush failure should restore runtime party setup state."
            );
            AssertRuntimeSaveMetadataEqual(
                fixture.GameSession.CaptureRuntimeState(),
                sessionBefore,
                "Flush failure should restore session memory snapshot."
            );
            _test.True(
                fixture.GameSession.IsBattleSaveLocked(),
                "Flush failure should leave the battle save lock as it was at finalization start."
            );
        }
        finally
        {
            fixture.GameSession.fail_payload_write = false;
            await DisposeFixture(fixture);
        }
    }

    private async Task TestLowLuckSidecarRollbackSurvivesLateFlushFailure()
    {
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "low_luck_retry",
            ChargedSetup("low_luck_retry_charge"),
            winnerFactionId: "player",
            consumedSetupIds: Array.Empty<string>()
        );
        try
        {
            DispatchHardshipLowLuckEvent(fixture.Runtime, fixture.ResolutionResult.battle_id);
            int lootEntryCountBefore = fixture.ResolutionResult.loot_entries.Count;
            fixture.GameSession.fail_payload_write = true;

            bool firstFinalized = fixture.Runtime.FinalizeBattleResolution(fixture.ResolutionResult);

            _test.False(firstFinalized, "Forced late flush failure should fail first finalization.");
            _test.Eq(
                fixture.ResolutionResult.loot_entries.Count,
                lootEntryCountBefore,
                "Rollback after late failure should restore battle resolution loot mutations."
            );
            _test.Eq(
                fixture.Runtime.GetPendingRewardCount(),
                0,
                "Rollback after late failure should not leave queued low-luck rewards from the failed attempt."
            );

            fixture.GameSession.fail_payload_write = false;
            bool retryFinalized = fixture.Runtime.FinalizeBattleResolution(fixture.ResolutionResult);

            _test.True(retryFinalized, "Retry after late failure should finalize from restored battle-sidecar memory.");
            _test.True(
                CountLowLuckLootEntries(fixture.ResolutionResult) == 1,
                "Retry should add exactly one low-luck loot entry from restored battle-sidecar memory."
            );
        }
        finally
        {
            fixture.GameSession.fail_payload_write = false;
            await DisposeFixture(fixture);
        }
    }

    private void TestEndBattleResourceCommitFailureReturnsTypedResult()
    {
        using BattleRuntimeModule battleRuntime = new();
        FailingResourceCommitGateway gateway = new(() => new PartyState());
        battleRuntime.setup(character_gateway: gateway);
        battleRuntime.SetupStateForTests(
            BuildBattleState(BattleUnit("hero_unit", "hero", alive: true, currentHp: 20, currentMp: 28))
        );

        BattleEndResult result = battleRuntime.EndBattle(new BattleEndOptions(commitProgression: true));

        _test.False(result.Ok, "EndBattle should fail when resource writeback fails.");
        _test.Eq(
            result.ErrorCode,
            "battle_resource_commit_failed",
            "EndBattle should expose stable resource failure code."
        );
        _test.True(
            result.ResourceCommitResult != null,
            "EndBattle should expose the typed resource commit failure result."
        );
        if (result.ResourceCommitResult != null)
        {
            _test.Eq(
                result.ResourceCommitResult.ErrorCode,
                FailingResourceCommitGateway.ForcedErrorCode,
                "EndBattle should preserve the gateway resource failure code."
            );
        }
    }

    private async Task TestResourceCommitFailureRollsBackFinalizationMemory()
    {
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "resource_failure",
            ChargedSetup("resource_failure_charge"),
            winnerFactionId: "player",
            consumedSetupIds: Array.Empty<string>()
        );
        try
        {
            FailingResourceCommitGateway gateway = new(() => fixture.Runtime.GetPartyState());
            fixture.Runtime._battle_runtime.setup(character_gateway: gateway);
            fixture.Runtime._battle_runtime.SetupStateForTests(fixture.Runtime.GetBattleState());
            DispatchHardshipLowLuckEvent(fixture.Runtime, fixture.ResolutionResult.battle_id);
            GDictionary sessionBefore = fixture.GameSession.CaptureRuntimeState();
            int lootEntryCountBefore = fixture.ResolutionResult.loot_entries.Count;

            bool finalized = fixture.Runtime.FinalizeBattleResolution(fixture.ResolutionResult);

            _test.False(finalized, "Forced resource commit failure should fail finalization.");
            _test.Eq(
                fixture.ResolutionResult.loot_entries.Count,
                lootEntryCountBefore,
                "Resource commit failure should restore battle resolution loot mutations."
            );
            AssertSetupCharged(
                fixture.Runtime.GetPartyState().GetMemberState("hero"),
                "resource_failure_charge",
                "Resource commit failure should restore runtime party setup state."
            );
            AssertRuntimeSaveMetadataEqual(
                fixture.GameSession.CaptureRuntimeState(),
                sessionBefore,
                "Resource commit failure should restore session memory snapshot."
            );
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    private async Task<RuntimeFixture> BuildRuntimeFixture(
        string suffix,
        ContingencyMatrixSetupState setup,
        string winnerFactionId,
        IReadOnlyList<string> consumedSetupIds
    )
    {
        PartyState partyState = BuildPartyState(setup, currentMp: 5);
        GameSession gameSession = await InstallGameSession($"ContingencyBattleLifecycle_{suffix}");
        GDictionary worldData = BuildWorldData();
        gameSession.ConfigureRuntimeWorldForTests(
            $"contingency_battle_lifecycle_{suffix}",
            TestConfigPath,
            worldData,
            partyState,
            new GDictionary(),
            "contingency_battle_lifecycle_test",
            "Contingency Battle Lifecycle Test",
            new Vector2I(8, 8)
        );
        gameSession.SetBattleSaveLock(true);

        GameRuntimeFacade runtime = new()
        {
            _game_session = gameSession,
            _party_state = partyState,
            _player_coord = Vector2I.Zero,
            _selected_coord = Vector2I.Zero,
            _player_faction_id = "player",
        };
        runtime._world_map_data_context.BindRootWorldData(worldData);
        runtime._world_map_data_context.SyncActiveWorldContext(
            gameSession.GetGenerationConfig(),
            runtime._grid_system,
            Vector2I.Zero,
            Vector2I.Zero
        );
        runtime._fog_system.Setup(new Vector2I(8, 8));
        runtime._character_management.setup(
            partyState,
            gameSession.GetSkillDefsTyped(),
            gameSession.GetProfessionDefsTyped(),
            gameSession.GetAchievementDefsTyped(),
            gameSession.GetItemDefsTyped(),
            gameSession.GetQuestDefsTyped(),
            gameSession.GetTraitDefsTyped(),
            gameSession.AllocateEquipmentInstanceId,
            gameSession.GetProgressionIdentityCatalogTyped()
        );
        runtime._party_warehouse_service.Setup(
            partyState,
            gameSession.GetItemDefsTyped(),
            gameSession.AllocateEquipmentInstanceId
        );
        runtime._battle_runtime.setup(
            runtime._character_management,
            gameSession.GetSkillDefsTyped(),
            gameSession.GetEnemyTemplatesTyped(),
            gameSession.GetEnemyAiBrainsTyped(),
            runtime._encounter_roster_builder,
            runtime._equipment_drop_service,
            gameSession.GetItemDefsTyped(),
            null,
            gameSession.AllocateEquipmentInstanceId,
            gameSession.GetBattleSpecialProfileRegistrySnapshot(),
            gameSession.GetGameRootTyped()?.GetContentCatalogTyped()?.GetSkillCatalogTyped()
        );

        BattleUnitState heroUnit = BattleUnit("hero_unit", "hero", alive: true, currentHp: 20, currentMp: 28);
        foreach (string setupId in consumedSetupIds ?? Array.Empty<string>())
            heroUnit.MarkContingencySetupConsumed(setupId);
        BattleState battleState = BuildBattleState(heroUnit);
        battleState.winner_faction_id = winnerFactionId;
        runtime._battle_runtime.SetupStateForTests(battleState);
        runtime.SetRuntimeBattleState(battleState);

        BattleResolutionResult resolutionResult = new()
        {
            battle_id = battleState.battle_id,
            seed = battleState.seed,
            world_coord = battleState.world_coord,
            encounter_anchor_id = battleState.encounter_anchor_id,
            terrain_profile_id = battleState.terrain_profile_id,
            winner_faction_id = winnerFactionId,
        };
        return new RuntimeFixture(runtime, gameSession, resolutionResult);
    }

    private static BattleState BuildBattleState(BattleUnitState heroUnit)
    {
        BattleState battleState = new()
        {
            battle_id = "contingency_lifecycle_battle",
            seed = 77,
            world_coord = Vector2I.Zero,
            encounter_anchor_id = "contingency_lifecycle_encounter",
            terrain_profile_id = "default",
            phase = "battle_ended",
            winner_faction_id = "player",
            timeline = new BattleTimelineState(),
        };
        battleState.SetPartyBackpackView(new WarehouseState());
        battleState.SetUnit(heroUnit);
        battleState.ally_unit_ids = new GStringNameArray { heroUnit.unit_id };
        return battleState;
    }

    private static BattleUnitState BattleUnit(
        string unitId,
        string memberId,
        bool alive,
        int currentHp,
        int currentMp
    )
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            source_member_id = memberId,
            display_name = "Hero",
            faction_id = "player",
            control_mode = "manual",
            is_alive = alive,
            current_hp = currentHp,
            current_mp = currentMp,
            current_aura = 0,
            current_stamina = 10,
            current_ap = 2,
        };
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 20);
        unit.attribute_snapshot.SetValue(AttributeService.MP_MAX, 30);
        unit.attribute_snapshot.SetValue(AttributeService.AURA_MAX, 0);
        unit.SetEquipmentView(new EquipmentState());
        return unit;
    }

    private static CharacterManagementModule BuildManager(PartyState partyState)
    {
        CharacterManagementModule manager = new();
        manager.setup(
            partyState,
            new Dictionary<StringName, SkillDef>(),
            new Dictionary<StringName, ProfessionDef>(),
            new Dictionary<StringName, AchievementDef>(),
            new Dictionary<StringName, ItemDef>(),
            new Dictionary<StringName, QuestDef>(),
            new Dictionary<StringName, TraitDef>(),
            null,
            new ProgressionIdentityCatalogData()
        );
        return manager;
    }

    private static PartyState BuildPartyState(ContingencyMatrixSetupState setup, int currentMp)
    {
        PartyMemberState member = new()
        {
            member_id = "hero",
            display_name = "Hero",
            progression = MakeProgress("hero", 30),
            current_hp = 20,
            current_mp = currentMp,
            current_aura = 0,
        };
        member = member.WithContingencySetupsForMutation(new[] { setup });

        PartyState partyState = new()
        {
            leader_member_id = "hero",
            main_character_member_id = "hero",
            active_member_ids = new GStringNameArray { "hero" },
            warehouse_state = new WarehouseState(),
        };
        partyState.SetMemberState(member);
        return partyState;
    }

    private static UnitProgress MakeProgress(StringName unitId, int mpMax)
    {
        UnitProgress progress = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString().Capitalize(),
        };
        progress.unit_base_attributes.SetAttributeValue(AttributeService.HP_MAX, 20);
        progress.unit_base_attributes.SetAttributeValue(AttributeService.MP_MAX, mpMax);
        progress.unit_base_attributes.SetAttributeValue(AttributeService.AURA_MAX, 0);
        return progress;
    }

    private static ContingencyMatrixSetupState ChargedSetup(string setupId) =>
        ContingencyMatrixSetupState.FromDictionary(
            BuildSetupPayload(setupId, charged: true, reservedMpMax: 12, ChargedMaterialCosts())
        );

    private static GArray ChargedMaterialCosts() =>
        new()
        {
            new GDictionary
            {
                ["item_id"] = "special_contingency_gem",
                ["quantity"] = 1,
            },
        };

    private static GDictionary BuildSetupPayload(
        string setupId,
        bool charged,
        int reservedMpMax,
        GArray materialCosts
    )
    {
        return new GDictionary
        {
            ["setup_id"] = setupId,
            ["display_name"] = "Emergency Matrix",
            ["enabled"] = true,
            ["charged"] = charged,
            ["source_skill_id"] = "mage_chain_contingency",
            ["source_skill_level"] = 5,
            ["matrix_load"] = 3,
            ["reserved_mp_max"] = reservedMpMax,
            ["material_costs"] = materialCosts,
            ["trigger"] = new GDictionary
            {
                ["type"] = "hp_below_percent",
                ["subject"] = "owner",
                ["percent"] = 30,
                ["crossing_only"] = true,
                ["timing"] = "after_hp_changed",
            },
            ["release_mode"] = "burst_release",
            ["stored_spells"] = new GArray
            {
                new GDictionary
                {
                    ["stored_skill_id"] = "mage_mirror_image",
                    ["cast_level"] = 2,
                    ["order"] = 1,
                    ["target_resolver"] = new GDictionary { ["type"] = "self" },
                    ["parameter_bindings"] = new GDictionary(),
                    ["fallback_policy"] = "skip_if_invalid",
                },
            },
        };
    }

    private static GDictionary BuildWorldData() =>
        new()
        {
            ["map_seed"] = 1,
            ["world_step"] = 0,
            ["next_equipment_instance_serial"] = 1,
            ["active_submap_id"] = "",
            ["submap_return_stack"] = new GArray(),
            ["settlements"] = new GArray(),
            ["world_events"] = new GArray(),
            ["encounter_anchors"] = new GArray(),
            ["mounted_submaps"] = new GDictionary(),
            ["world_npcs"] = new GArray(),
            ["player_start_coord"] = Vector2I.Zero,
            ["player_start_settlement_id"] = "",
            ["player_start_settlement_name"] = "",
            ["fog_states"] = new GDictionary(),
        };

    private static void DispatchHardshipLowLuckEvent(GameRuntimeFacade runtime, StringName battleId)
    {
        runtime?._battle_runtime?.GetFateEventBus()?.Dispatch(
            BattleFateEventPayload.Create(
                "hardship_survival",
                battleId,
                attackerMemberId: "hero",
                attackerId: "hero_unit",
                attackerLowHpHardship: true,
                attackerStrongAttackDebuffIds: new[] { new StringName("low_hp_attack_disadvantage") },
                hiddenLuckAtBirth: -5
            )
        );
    }

    private static int CountLowLuckLootEntries(BattleResolutionResult resolutionResult)
    {
        int count = 0;
        foreach (BattleLootEntry entry in resolutionResult?.loot_entries ?? new List<BattleLootEntry>())
        {
            if (entry != null && entry.SourceKind == BattleLootSourceKind.LowLuckEvent)
                count++;
        }
        return count;
    }

    private async Task<GameSession> InstallGameSession(string nodeName)
    {
        foreach (Node child in Root.GetChildren())
        {
            if (child.Name == nodeName)
                child.QueueFree();
        }
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        GameSession gameSession = new() { Name = nodeName };
        Root.AddChild(gameSession);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        return gameSession;
    }

    private async Task DisposeFixture(RuntimeFixture fixture)
    {
        if (fixture == null)
            return;
        fixture.Runtime?.Dispose();
        if (fixture.GameSession != null)
        {
            fixture.GameSession.fail_payload_write = false;
            fixture.GameSession.SetBattleSaveLock(false);
            fixture.GameSession.ClearPersistedGame();
            fixture.GameSession.QueueFree();
            await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        }
    }

    private void AssertSetupReleased(PartyMemberState member, string setupId, string message)
    {
        _test.True(member.TryGetContingencySetupTyped(setupId, out ContingencyMatrixSetupState setup), $"{message} setup should exist.");
        if (setup == null)
            return;
        _test.False(setup.Charged, $"{message} charged should be false.");
        _test.Eq(setup.ReservedMpMax, 0, $"{message} reserved_mp_max should be cleared.");
        _test.Eq(setup.MaterialCosts.Count, 0, $"{message} material receipt should be cleared.");
    }

    private void AssertSetupCharged(PartyMemberState member, string setupId, string message)
    {
        _test.True(member.TryGetContingencySetupTyped(setupId, out ContingencyMatrixSetupState setup), $"{message} setup should exist.");
        if (setup == null)
            return;
        _test.True(setup.Charged, $"{message} charged should remain true.");
        _test.Eq(setup.ReservedMpMax, 12, $"{message} reserved_mp_max should remain charged.");
        _test.Eq(setup.MaterialCosts.Count, 1, $"{message} material receipt should remain charged.");
    }

    private void AssertRuntimeSaveMetadataEqual(
        GDictionary actual,
        GDictionary expected,
        string messagePrefix
    )
    {
        _test.Eq(DictBool(actual, "battle_save_lock_enabled"), DictBool(expected, "battle_save_lock_enabled"), $"{messagePrefix} battle_save_lock_enabled mismatch.");
        _test.Eq(DictBool(actual, "battle_save_dirty"), DictBool(expected, "battle_save_dirty"), $"{messagePrefix} battle_save_dirty mismatch.");
        _test.Eq(DictBool(actual, "runtime_save_dirty"), DictBool(expected, "runtime_save_dirty"), $"{messagePrefix} runtime_save_dirty mismatch.");
        _test.Eq(DictInt(actual, "last_save_error"), DictInt(expected, "last_save_error"), $"{messagePrefix} last_save_error mismatch.");
        _test.Eq(DictString(actual, "last_save_error_reason"), DictString(expected, "last_save_error_reason"), $"{messagePrefix} last_save_error_reason mismatch.");
        _test.Eq(DictBool(actual, "post_decode_save_pending"), DictBool(expected, "post_decode_save_pending"), $"{messagePrefix} post_decode_save_pending mismatch.");
        _test.Eq(StringifyArray(DictArray(actual, "runtime_save_dirty_scopes")), StringifyArray(DictArray(expected, "runtime_save_dirty_scopes")), $"{messagePrefix} runtime_save_dirty_scopes mismatch.");
        _test.Eq(StringifyArray(DictArray(actual, "post_decode_save_reasons")), StringifyArray(DictArray(expected, "post_decode_save_reasons")), $"{messagePrefix} post_decode_save_reasons mismatch.");
    }

    private static bool DictBool(GDictionary dictionary, string key) =>
        dictionary != null && dictionary.ContainsKey(key) && dictionary[key].VariantType == Variant.Type.Bool
            ? dictionary[key].AsBool()
            : false;

    private static int DictInt(GDictionary dictionary, string key) =>
        dictionary != null && dictionary.ContainsKey(key) && dictionary[key].VariantType == Variant.Type.Int
            ? dictionary[key].AsInt32()
            : 0;

    private static string DictString(GDictionary dictionary, string key) =>
        dictionary != null && dictionary.ContainsKey(key) && dictionary[key].VariantType == Variant.Type.String
            ? dictionary[key].AsString()
            : "";

    private static GArray DictArray(GDictionary dictionary, string key) =>
        dictionary != null && dictionary.ContainsKey(key) && dictionary[key].VariantType == Variant.Type.Array
            ? dictionary[key].AsGodotArray()
            : new GArray();

    private static string StringifyArray(GArray values)
    {
        List<string> entries = new();
        foreach (Variant value in values ?? new GArray())
            entries.Add(value.ToString());
        return string.Join("|", entries);
    }

    private sealed class RuntimeFixture
    {
        internal RuntimeFixture(
            GameRuntimeFacade runtime,
            GameSession gameSession,
            BattleResolutionResult resolutionResult
        )
        {
            Runtime = runtime;
            GameSession = gameSession;
            ResolutionResult = resolutionResult;
        }

        internal GameRuntimeFacade Runtime { get; }
        internal GameSession GameSession { get; }
        internal BattleResolutionResult ResolutionResult { get; }
    }

    private sealed class FailingResourceCommitGateway : IBattleRuntimeCharacterGateway
    {
        internal const string ForcedErrorCode = "forced_resource_commit_failure";
        private readonly Func<PartyState> _partyProvider;

        internal FailingResourceCommitGateway(Func<PartyState> partyProvider)
        {
            _partyProvider = partyProvider;
        }

        public PartyState GetPartyState() => _partyProvider?.Invoke();

        public IReadOnlyDictionary<StringName, ItemDef> GetItemDefsTyped() =>
            new Dictionary<StringName, ItemDef>();

        public bool HasItemDefCatalog() => false;

        public ItemDef GetItemDef(StringName item_id) => null;

        public PartyMemberState GetMemberState(StringName member_id) =>
            GetPartyState()?.GetMemberState(member_id);

        public AttributeSnapshot GetMemberAttributeSnapshotForEquipmentView(
            StringName member_id,
            EquipmentState equipment_view
        ) => new();

        public WeaponProjection GetMemberWeaponProjectionForEquipmentViewTyped(
            StringName member_id,
            EquipmentState equipment_view
        ) => new();

        public BattleEffectiveTraitProjection BuildEffectiveTraitProjectionForEquipmentView(
            StringName member_id,
            EquipmentState equipment_view
        ) => BattleEffectiveTraitProjection.Empty;

        public PassiveSourceContext BuildPassiveSourceContext(
            StringName member_id,
            UnitProgress progression_state
        ) => null;

        public CharacterProgressionDelta PromoteProfession(
            StringName member_id,
            StringName profession_id,
            PromotionSelectionData selection
        ) => new() { member_id = member_id };

        public BattleResourceCommitResult CommitBattleResources(
            StringName member_id,
            int current_hp,
            int current_mp,
            int current_aura
        ) => BattleResourceCommitResult.Failure(ForcedErrorCode, member_id);

        public void CommitBattleDeath(StringName member_id) { }

        public int FlushAfterBattle() => (int)Error.Ok;

        public CharacterProgressionDelta GrantBattleMastery(
            StringName member_id,
            StringName skill_id,
            int amount
        ) => new() { member_id = member_id };

        public CharacterProgressionDelta GrantSkillMasteryFromSource(
            StringName member_id,
            StringName skill_id,
            int amount,
            StringName source_type,
            string source_label,
            string reason_text,
            bool emit_achievement_event
        ) => new() { member_id = member_id };

        public GStringNameArray RecordAchievementEvent(
            StringName member_id,
            StringName event_type,
            int amount
        ) => new();

        public GStringNameArray RecordAchievementEvent(
            StringName member_id,
            StringName event_type,
            int amount,
            StringName subject_id,
            GDictionary meta
        ) => new();

        public PendingCharacterReward BuildPendingSkillMasteryReward(
            StringName member_id,
            StringName source_type,
            string source_label,
            IEnumerable<PendingCharacterRewardEntry> entry_options,
            string summary_text
        ) => null;
    }
}
