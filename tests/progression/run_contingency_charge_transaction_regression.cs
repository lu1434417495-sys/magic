using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_contingency_charge_transaction_regression : SceneTree
{
    private const string TestConfigPath = "res://data/configs/world_map/test_world_map_config.tres";
    private static readonly StringName GemId = "special_contingency_gem";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(RunAsync));
    }

	private async void RunAsync()
	{
		try
		{
			TestSaveValidUnchargedConfigDoesNotDebitMaterialOrClampMp();
			TestSaveInvalidContentReturnsInvalidSetupWithoutMutation();
			TestSaveOverExistingChargedSetupReturnsSetupChargedWithoutMutation();
			TestInsufficientMaterialLeavesStateUnchanged();
			TestContentValidationFailureLeavesStateUnchanged();
			TestForcedSetupWriteFailureAfterWarehouseCommitRestoresWarehouse();
            TestSuccessfulChargeDeductsMaterialStoresReceiptAndClampsMp();
            TestClearChargeDoesNotRefundOrIncreaseMp();
            await TestRuntimePersistFailureRestoresCommandStartPartyAndServices();
            TestInlineChargePayloadIsRejected();
        }
        catch (Exception exception)
        {
            _test.Fail(exception.ToString());
        }
        finally
        {
            GodotSharpCleanup.CollectPendingFinalizers();
            Quit(_test.Finish("Contingency charge transaction regression"));
		}
	}

	private void TestSaveValidUnchargedConfigDoesNotDebitMaterialOrClampMp()
	{
		PartyState partyState = BuildPartyState(ValidSetup("save_valid"), currentMp: 30);
		PartyWarehouseService warehouse = BuildWarehouseService(partyState);
		warehouse.AddItemTyped(GemId, 1);
		using CharacterManagementModule manager = BuildManager(partyState);
		PartyContingencySetupService service = BuildService(partyState, warehouse, manager);
		ContingencyMatrixSetupState replacement = ContingencyMatrixSetupState.FromDictionary(
			BuildSetupPayload(
				"save_valid",
				"mage_mirror_image",
				displayName: "Updated Emergency Matrix"
			)
		);

		ContingencySetupMutationResult result = service.SaveSetup("hero", replacement);

		_test.True(result.Ok, $"Saving a valid uncharged setup should pass. error={result.ErrorCode}");
		_test.False(result.Charged, "SaveSetup should keep the saved setup uncharged.");
		_test.Eq(result.ReservedMpMax, 0, "SaveSetup should clear reserved_mp_max on saved setups.");
		_test.Eq(warehouse.CountItem(GemId), 1, "SaveSetup should not debit contingency material.");
		_test.Eq(partyState.GetMemberState("hero").current_mp, 30, "SaveSetup should not clamp current MP.");
		AssertSetupState(
			partyState,
			"save_valid",
			charged: false,
			reservedMpMax: 0,
			materialCount: 0,
			"Saving a valid uncharged setup should persist an uncharged config."
		);
		_test.Eq(
			partyState.GetMemberState("hero").GetContingencySetupsTyped()[0].DisplayName,
			"Updated Emergency Matrix",
			"SaveSetup should replace the setup contents when the payload is valid."
		);
	}

	private void TestSaveInvalidContentReturnsInvalidSetupWithoutMutation()
	{
		PartyState partyState = BuildPartyState(ValidSetup("save_invalid_original"), currentMp: 30);
		PartyWarehouseService warehouse = BuildWarehouseService(partyState);
		warehouse.AddItemTyped(GemId, 1);
		using CharacterManagementModule manager = BuildManager(partyState);
		PartyContingencySetupService service = BuildService(partyState, warehouse, manager);

		ContingencySetupMutationResult result = service.SaveSetup(
			"hero",
			BuildSaveInvalidContentSetup("save_invalid_original")
		);

		_test.False(result.Ok, "Invalid save content should fail.");
		_test.Eq(
			result.ErrorCode,
			"invalid_setup",
			"Invalid save content should use the save-path invalid_setup code."
		);
		_test.Eq(warehouse.CountItem(GemId), 1, "Invalid save content should not debit material.");
		_test.Eq(partyState.GetMemberState("hero").current_mp, 30, "Invalid save content should not clamp MP.");
		AssertSetupState(
			partyState,
			"save_invalid_original",
			charged: false,
			reservedMpMax: 0,
			materialCount: 0,
			"Invalid save content should leave the original setup untouched."
		);
		_test.Eq(
			partyState.GetMemberState("hero").GetContingencySetupsTyped()[0].DisplayName,
			"Emergency Matrix",
			"Invalid save content should not replace the stored setup."
		);
	}

	private void TestSaveOverExistingChargedSetupReturnsSetupChargedWithoutMutation()
	{
		PartyState partyState = BuildPartyState(ChargedSetup("save_charged"), currentMp: 24);
		PartyWarehouseService warehouse = BuildWarehouseService(partyState);
		using CharacterManagementModule manager = BuildManager(partyState);
		PartyContingencySetupService service = BuildService(partyState, warehouse, manager);
		ContingencyMatrixSetupState replacement = ContingencyMatrixSetupState.FromDictionary(
			BuildSetupPayload(
				"save_charged",
				"mage_mirror_image",
				displayName: "Should Not Replace Charged Setup"
			)
		);

		ContingencySetupMutationResult result = service.SaveSetup("hero", replacement);

		_test.False(result.Ok, "Saving over a charged setup should fail.");
		_test.Eq(
			result.ErrorCode,
			"setup_charged",
			"Saving over a charged setup should use the stable setup_charged code."
		);
		_test.Eq(warehouse.CountItem(GemId), 0, "Saving over a charged setup should not mutate warehouse.");
		_test.Eq(partyState.GetMemberState("hero").current_mp, 24, "Saving over a charged setup should not change MP.");
		AssertSetupState(
			partyState,
			"save_charged",
			charged: true,
			reservedMpMax: 6,
			materialCount: 1,
			"Saving over a charged setup should keep the charged setup intact."
		);
		_test.Eq(
			partyState.GetMemberState("hero").GetContingencySetupsTyped()[0].DisplayName,
			"Emergency Matrix",
			"Saving over a charged setup should not replace the stored setup."
		);
	}

    private void TestInsufficientMaterialLeavesStateUnchanged()
    {
        PartyState partyState = BuildPartyState(ValidSetup("insufficient_material"), currentMp: 30);
        PartyWarehouseService warehouse = BuildWarehouseService(partyState);
        using CharacterManagementModule manager = BuildManager(partyState);
        PartyContingencySetupService service = BuildService(partyState, warehouse, manager);

        ContingencySetupMutationResult result = service.ChargeSetup("hero", "insufficient_material");

        _test.False(result.Ok, "Charge should fail when the contingency gem is missing.");
        _test.Eq(result.ErrorCode, "material_insufficient", "Missing gem should return stable material code.");
        AssertSetupState(
            partyState,
            "insufficient_material",
            charged: false,
            reservedMpMax: 0,
            materialCount: 0,
            "Insufficient material failure should leave setup uncharged."
        );
        _test.Eq(warehouse.CountItem(GemId), 0, "Insufficient material failure should not mutate warehouse.");
        _test.Eq(partyState.GetMemberState("hero").current_mp, 30, "Insufficient material failure should not clamp MP.");
    }

    private void TestContentValidationFailureLeavesStateUnchanged()
    {
        PartyState partyState = BuildPartyState(InvalidContentSetup("invalid_content"), currentMp: 30);
        PartyWarehouseService warehouse = BuildWarehouseService(partyState);
        warehouse.AddItemTyped(GemId, 1);
        using CharacterManagementModule manager = BuildManager(partyState);
        PartyContingencySetupService service = BuildService(partyState, warehouse, manager);

        ContingencySetupMutationResult result = service.ChargeSetup("hero", "invalid_content");

        _test.False(result.Ok, "Charge should fail when the charged candidate fails content validation.");
        _test.Eq(result.ErrorCode, "content_validation_failed", "Validation failure should return stable code.");
        AssertSetupState(
            partyState,
            "invalid_content",
            charged: false,
            reservedMpMax: 0,
            materialCount: 0,
            "Content validation failure should leave setup unchanged."
        );
        _test.Eq(warehouse.CountItem(GemId), 1, "Content validation failure should not debit warehouse.");
        _test.Eq(partyState.GetMemberState("hero").current_mp, 30, "Content validation failure should not clamp MP.");
    }

    private void TestForcedSetupWriteFailureAfterWarehouseCommitRestoresWarehouse()
    {
        PartyState partyState = BuildPartyState(ValidSetup("forced_write_failure"), currentMp: 30);
        PartyWarehouseService warehouse = BuildWarehouseService(partyState);
        warehouse.AddItemTyped(GemId, 1);
        using CharacterManagementModule manager = BuildManager(partyState);
        PartyContingencySetupService service = BuildService(partyState, warehouse, manager);
        service.ForceSetupWriteFailureAfterWarehouseCommitForTests = true;

        ContingencySetupMutationResult result = service.ChargeSetup("hero", "forced_write_failure");

        _test.False(result.Ok, "Forced setup write failure should fail the charge.");
        _test.Eq(result.ErrorCode, "setup_write_failed", "Forced setup write failure should return stable code.");
        _test.Eq(warehouse.CountItem(GemId), 1, "Forced setup write failure should restore debited gem.");
        AssertSetupState(
            partyState,
            "forced_write_failure",
            charged: false,
            reservedMpMax: 0,
            materialCount: 0,
            "Forced setup write failure should restore setup list."
        );
        _test.Eq(partyState.GetMemberState("hero").current_mp, 30, "Forced setup write failure should restore MP.");
    }

    private void TestSuccessfulChargeDeductsMaterialStoresReceiptAndClampsMp()
    {
        PartyState partyState = BuildPartyState(ValidSetup("successful_charge"), currentMp: 30);
        PartyWarehouseService warehouse = BuildWarehouseService(partyState);
        warehouse.AddItemTyped(GemId, 1);
        using CharacterManagementModule manager = BuildManager(partyState);
        PartyContingencySetupService service = BuildService(partyState, warehouse, manager);

        ContingencySetupMutationResult result = service.ChargeSetup("hero", "successful_charge");

        _test.True(result.Ok, $"Successful charge should pass. error={result.ErrorCode}");
        _test.True(result.Charged, "Successful charge result should report charged=true.");
        _test.Eq(result.ReservedMpMax, 6, "reserved_mp_max should be max(matrix_load * 2, 1).");
        _test.Eq(result.EffectiveMpMax, 24, "Successful charge should report clamped effective MP max.");
        _test.Eq(warehouse.CountItem(GemId), 0, "Successful charge should deduct one contingency gem.");
        AssertSetupState(
            partyState,
            "successful_charge",
            charged: true,
            reservedMpMax: 6,
            materialCount: 1,
            "Successful charge should persist charged setup receipt."
        );
        _test.Eq(partyState.GetMemberState("hero").current_mp, 24, "Successful charge should clamp current MP.");
    }

    private void TestClearChargeDoesNotRefundOrIncreaseMp()
    {
        PartyState partyState = BuildPartyState(ValidSetup("clear_charge"), currentMp: 30);
        PartyWarehouseService warehouse = BuildWarehouseService(partyState);
        warehouse.AddItemTyped(GemId, 1);
        using CharacterManagementModule manager = BuildManager(partyState);
        PartyContingencySetupService service = BuildService(partyState, warehouse, manager);

        ContingencySetupMutationResult chargeResult = service.ChargeSetup("hero", "clear_charge");
        _test.True(chargeResult.Ok, $"Clear test setup charge should pass. error={chargeResult.ErrorCode}");

        ContingencySetupMutationResult clearResult = service.ClearCharge("hero", "clear_charge");

        _test.True(clearResult.Ok, $"Clear charge should pass. error={clearResult.ErrorCode}");
        _test.False(clearResult.Charged, "Clear charge result should report charged=false.");
        _test.Eq(warehouse.CountItem(GemId), 0, "Clear charge should not refund material.");
        AssertSetupState(
            partyState,
            "clear_charge",
            charged: false,
            reservedMpMax: 0,
            materialCount: 0,
            "Clear charge should clear receipt and reservation."
        );
        _test.Eq(partyState.GetMemberState("hero").current_mp, 24, "Clear charge should not increase current MP.");
    }

    private async Task TestRuntimePersistFailureRestoresCommandStartPartyAndServices()
    {
        RuntimeFixture fixture = await BuildRuntimeFixture();
        try
        {
            SeedGemStack(fixture.Runtime._party_state, 1);
            fixture.Runtime.SyncPartyStateServices();
            fixture.GameSession.SetPartyState(fixture.Runtime._party_state);
            fixture.GameSession.fail_payload_write = true;

            ContingencySetupMutationResult result =
                fixture.Runtime.ChargeContingencySetupRuntimeTyped("hero", "runtime_charge");

            _test.False(result.Ok, "Runtime gateway should fail when persistence fails.");
            _test.Eq(result.ErrorCode, "persistence_failure", "Runtime persist failure should return stable code.");
            _test.Eq(
                fixture.Runtime._party_warehouse_service.CountItem(GemId),
                1,
                "Runtime rollback should restore service warehouse reference."
            );
            AssertSetupState(
                fixture.Runtime._party_state,
                "runtime_charge",
                charged: false,
                reservedMpMax: 0,
                materialCount: 0,
                "Runtime rollback should restore command-start setup state."
            );
            _test.Eq(
                fixture.Runtime._party_state.GetMemberState("hero").current_mp,
                30,
                "Runtime rollback should restore command-start MP."
            );
            _test.Eq(
                fixture.GameSession.GetPartyState().GetMemberState("hero").current_mp,
                30,
                "Runtime rollback should restore session party state."
            );
        }
        finally
        {
            fixture.GameSession.fail_payload_write = false;
            await DisposeFixture(fixture);
        }
    }

    private void TestInlineChargePayloadIsRejected()
    {
        PartyState partyState = BuildPartyState(ValidSetup("inline_reject"), currentMp: 30);
        PartyWarehouseService warehouse = BuildWarehouseService(partyState);
        warehouse.AddItemTyped(GemId, 1);
        using CharacterManagementModule manager = BuildManager(partyState);
        PartyContingencySetupService service = BuildService(partyState, warehouse, manager);

        GDictionary inlinePayload = ValidSetup("inline_reject").ToDictionary();
        inlinePayload["trigger"] = new GDictionary
        {
            ["type"] = "owner_turn_started",
            ["timing"] = "turn_start",
        };
        ContingencySetupMutationResult result = service.ChargeSetup("hero", inlinePayload);

        _test.False(result.Ok, "Inline charge payload should be rejected.");
        _test.Eq(
            result.ErrorCode,
            "inline_setup_payload_not_allowed",
            "Inline charge payload should return stable rejection code."
        );
        _test.Eq(warehouse.CountItem(GemId), 1, "Inline payload rejection should not debit material.");
        AssertSetupState(
            partyState,
            "inline_reject",
            charged: false,
            reservedMpMax: 0,
            materialCount: 0,
            "Inline payload rejection should not mutate existing setup."
        );
    }

    private static PartyContingencySetupService BuildService(
        PartyState partyState,
        PartyWarehouseService warehouse,
        CharacterManagementModule manager
    )
    {
        PartyContingencySetupService service = new();
        service.Setup(
            partyState,
            warehouse,
            BuildSkillIndex(),
            manager.GetMemberAttributeSnapshot,
            () => false
        );
        return service;
    }

    private static CharacterManagementModule BuildManager(PartyState partyState)
    {
        CharacterManagementModule manager = new();
        manager.setup(
            partyState,
            BuildSkillIndex(),
            new Dictionary<StringName, ProfessionDef>(),
            new Dictionary<StringName, AchievementDef>(),
            BuildItemIndex(),
            new Dictionary<StringName, QuestDef>(),
            new Dictionary<StringName, TraitDef>(),
            null,
            new ProgressionIdentityCatalogData()
        );
        return manager;
    }

    private static PartyWarehouseService BuildWarehouseService(PartyState partyState)
    {
        PartyWarehouseService warehouse = new();
        warehouse.Setup(partyState, BuildItemIndex());
        return warehouse;
    }

    private async Task<RuntimeFixture> BuildRuntimeFixture()
    {
        GameSession gameSession = await InstallGameSession("ContingencyChargeTransactionSession");
        PartyState partyState = BuildPartyState(ValidSetup("runtime_charge"), currentMp: 30);
        gameSession.ConfigureRuntimeWorldForTests(
            "contingency_charge_transaction",
            TestConfigPath,
            BuildWorldData(),
            partyState,
            new GDictionary(),
            "contingency_charge_transaction",
            "Contingency Charge Transaction",
            new Vector2I(8, 8)
        );

        GameRuntimeFacade runtime = new()
        {
            _game_session = gameSession,
            _party_state = partyState,
            _player_coord = Vector2I.Zero,
            _selected_coord = Vector2I.Zero,
            _player_faction_id = "player",
        };
        runtime._world_map_data_context.BindRootWorldData(gameSession.GetWorldData());
        runtime._world_map_data_context.SyncActiveWorldContext(
            gameSession._generation_config,
            new WorldMapGridSystem(),
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
        runtime.SetupPartyWarehouseService(runtime._party_warehouse_service, partyState, gameSession.GetItemDefsTyped());
        runtime._party_item_use_service.Setup(
            partyState,
            gameSession.GetItemDefsTyped(),
            gameSession.GetSkillDefsTyped(),
            runtime._party_warehouse_service,
            runtime._character_management
        );
        runtime._party_equipment_service.Setup(
            partyState,
            gameSession.GetItemDefsTyped(),
            runtime._party_warehouse_service,
            gameSession.AllocateEquipmentInstanceId
        );
        return new RuntimeFixture(runtime, gameSession);
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
        fixture.Runtime?.Dispose();
        if (fixture.GameSession != null)
        {
            fixture.GameSession.ClearPersistedGame();
            fixture.GameSession.QueueFree();
        }
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
    }

    private static PartyState BuildPartyState(ContingencyMatrixSetupState setup, int currentMp)
    {
        PartyState partyState = new()
        {
            leader_member_id = "hero",
            main_character_member_id = "hero",
            active_member_ids = new GStringNameArray { "hero" },
            warehouse_state = new WarehouseState(),
        };
        PartyMemberState member = new()
        {
            member_id = "hero",
            display_name = "Hero",
            current_hp = 20,
            current_mp = currentMp,
            current_aura = 0,
            progression = BuildProgress(),
        };
        member = member.WithContingencySetupsForMutation(new[] { setup });
        partyState.SetMemberState(member);
        return partyState;
    }

    private static void SeedGemStack(PartyState partyState, int quantity)
    {
        partyState.warehouse_state ??= new WarehouseState();
        partyState.warehouse_state.AddStack(
            new WarehouseStackState
            {
                item_id = GemId,
                quantity = quantity,
            }
        );
    }

    private static UnitProgress BuildProgress()
    {
        UnitProgress progress = new()
        {
            unit_id = "hero",
            display_name = "Hero",
        };
        progress.unit_base_attributes.SetAttributeValue(AttributeService.HP_MAX, 20);
        progress.unit_base_attributes.SetAttributeValue(AttributeService.MP_MAX, 30);
        progress.unit_base_attributes.SetAttributeValue(AttributeService.AURA_MAX, 0);
        progress.unit_base_attributes.SetAttributeValue(PartyWarehouseService.StorageSpaceAttributeId, 6);
        progress.SetSkillProgress(LearnedSkill("mage_chain_contingency", 5));
        progress.SetSkillProgress(LearnedSkill("mage_mirror_image", 5));
        return progress;
    }

    private static UnitSkillProgress LearnedSkill(string skillId, int level) =>
        new()
        {
            skill_id = skillId,
            is_learned = true,
            skill_level = level,
            current_mastery = 0,
            total_mastery_earned = 0,
            is_core = false,
            granted_source_type = "test",
        };

    private static ContingencyMatrixSetupState ValidSetup(string setupId) =>
        ContingencyMatrixSetupState.FromDictionary(BuildSetupPayload(setupId, "mage_mirror_image"));

	private static ContingencyMatrixSetupState InvalidContentSetup(string setupId) =>
		ContingencyMatrixSetupState.FromDictionary(BuildSetupPayload(setupId, "mage_chain_contingency"));

	private static ContingencyMatrixSetupState BuildSaveInvalidContentSetup(string setupId)
		=> ContingencyMatrixSetupState.FromDictionary(
			BuildSetupPayload(
				setupId,
				"mage_mirror_image",
				sourceSkillId: "missing_chain_contingency_skill"
			)
		);

	private static ContingencyMatrixSetupState ChargedSetup(string setupId)
	{
		GDictionary payload = BuildSetupPayload(setupId, "mage_mirror_image");
		payload["charged"] = true;
		payload["reserved_mp_max"] = 6;
		payload["material_costs"] = new GArray
		{
			new GDictionary
			{
				["item_id"] = GemId,
				["quantity"] = 1,
			},
		};
		return ContingencyMatrixSetupState.FromDictionary(payload);
	}

    private static GDictionary BuildSetupPayload(
        string setupId,
        string storedSkillId,
        string displayName = "Emergency Matrix",
        string sourceSkillId = "mage_chain_contingency"
    ) =>
        new()
        {
            ["setup_id"] = setupId,
            ["display_name"] = displayName,
            ["enabled"] = true,
            ["charged"] = false,
            ["source_skill_id"] = sourceSkillId,
            ["source_skill_level"] = 5,
            ["matrix_load"] = 3,
            ["reserved_mp_max"] = 0,
            ["material_costs"] = new GArray(),
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
                    ["stored_skill_id"] = storedSkillId,
                    ["cast_level"] = 2,
                    ["order"] = 1,
                    ["target_resolver"] = new GDictionary { ["type"] = "self" },
                    ["parameter_bindings"] = new GDictionary(),
                    ["fallback_policy"] = "skip_if_invalid",
                },
            },
        };

    private static Dictionary<StringName, SkillDef> BuildSkillIndex() =>
        new()
        {
            ["mage_chain_contingency"] = BuildSkill("mage_chain_contingency", tags: new[] { "contingency", "meta_spell" }),
            ["mage_mirror_image"] = BuildSkill(
                "mage_mirror_image",
                automation: new ContingencyAutomationDef
                {
                    can_be_stored_in_contingency = true,
                    min_contingency_skill_level = 1,
                    effect_category = "defensive_self_buff",
                    allowed_target_resolvers = new Godot.Collections.Array<StringName> { "self" },
                    requires_manual_targeting = false,
                    allowed_parameter_bindings = new GDictionary(),
                }
            ),
        };

    private static SkillDef BuildSkill(
        string skillId,
        ContingencyAutomationDef automation = null,
        string[] tags = null
    )
    {
        SkillDef skill = new()
        {
            skill_id = skillId,
            display_name = skillId,
            icon_id = skillId,
            skill_type = "passive",
            max_level = 10,
            mastery_curve = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 },
            contingency_automation_profile = automation,
        };
        if (tags != null)
        {
            Godot.Collections.Array<StringName> tagValues = new();
            foreach (string tag in tags)
                tagValues.Add(tag);
            skill.SetTags(tagValues);
        }
        return skill;
    }

	private static Dictionary<StringName, ItemDef> BuildItemIndex() =>
		new()
        {
            [GemId] = new ItemDef
            {
                item_id = GemId,
                display_name = "Special Contingency Gem",
                CategoryKind = ItemCategoryKind.Misc,
                is_stackable = true,
                max_stack = 99,
            },
		};

    private void AssertSetupState(
        PartyState partyState,
        StringName setupId,
        bool charged,
        int reservedMpMax,
        int materialCount,
        string message
    )
    {
        PartyMemberState member = partyState.GetMemberState("hero");
        _test.True(member.TryGetContingencySetupTyped(setupId, out ContingencyMatrixSetupState setup), $"{message} setup should exist.");
        if (setup == null)
            return;
        _test.Eq(setup.Charged, charged, $"{message} charged mismatch.");
        _test.Eq(setup.ReservedMpMax, reservedMpMax, $"{message} reserved_mp_max mismatch.");
        _test.Eq(setup.MaterialCosts.Count, materialCount, $"{message} material cost count mismatch.");
        if (materialCount > 0)
        {
            _test.Eq(setup.MaterialCosts[0].ItemId, GemId, $"{message} material receipt item mismatch.");
            _test.Eq(setup.MaterialCosts[0].Quantity, 1, $"{message} material receipt quantity mismatch.");
        }
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

    private sealed class RuntimeFixture
    {
        public GameRuntimeFacade Runtime { get; }
        public GameSession GameSession { get; }

        public RuntimeFixture(GameRuntimeFacade runtime, GameSession gameSession)
        {
            Runtime = runtime;
            GameSession = gameSession;
        }
    }
}
