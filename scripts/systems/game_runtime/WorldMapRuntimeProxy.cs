using System;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class WorldMapRuntimeProxy : RefCounted
{
    private static readonly string RuntimeUnavailableMessage = "运行时尚未初始化。";

    private GameRuntimeFacade _runtime;
    private WorldMapSystem _renderTarget;

    public void Setup(GameRuntimeFacade runtime, WorldMapSystem renderTarget = null)
    {
        _runtime = runtime;
        _renderTarget = renderTarget;
    }

    public new void Dispose()
    {
        _runtime = null;
        _renderTarget = null;
    }

    public string GetStatusText()
    {
        return _runtime?.get_status_text() ?? "";
    }

    public string GetActiveModalId()
    {
        return _runtime?.get_active_modal_id() ?? "";
    }

    public Dictionary GetGameOverContext()
    {
        return _runtime?.get_game_over_context() ?? new Dictionary();
    }

    public string GetActiveSettlementId()
    {
        return _runtime?.get_active_settlement_id() ?? "";
    }

    public string GetActiveMapId()
    {
        return _runtime?.get_active_map_id() ?? "";
    }

    public string GetActiveMapDisplayName()
    {
        return _runtime?.get_active_map_display_name() ?? "";
    }

    public string GetSubmapReturnHintText()
    {
        return _runtime?.get_submap_return_hint_text() ?? "";
    }

    public Dictionary GetPendingSubmapPrompt()
    {
        return _runtime?.get_pending_submap_prompt() ?? new Dictionary();
    }

    public Dictionary GetPendingBattleStartPrompt()
    {
        return _runtime?.get_pending_battle_start_prompt() ?? new Dictionary();
    }

    public Dictionary GetLogSnapshot(int limit = 80)
    {
        return _runtime?.get_log_snapshot(limit) ?? new Dictionary();
    }

    public Dictionary BuildHeadlessSnapshot()
    {
        return _runtime?.build_headless_snapshot() ?? new Dictionary();
    }

    public string BuildTextSnapshot()
    {
        return _runtime?.build_text_snapshot() ?? "";
    }

    public bool Advance(float delta)
    {
        return _runtime?.advance(delta) ?? false;
    }

    public WorldMapGridSystem GetGridSystem()
    {
        return _runtime?.get_grid_system();
    }

    public WorldMapFogSystem GetFogSystem()
    {
        return _runtime?.get_fog_system();
    }

    public Dictionary GetWorldData()
    {
        return _runtime?.get_world_data() ?? new Dictionary();
    }

    public Vector2I GetPlayerCoord()
    {
        return _runtime?.get_player_coord() ?? Vector2I.Zero;
    }

    public bool IsPlayerVisibleOnWorldMap()
    {
        return _runtime?.is_player_visible_on_world_map() ?? true;
    }

    public Vector2I GetSelectedCoord()
    {
        return _runtime?.get_selected_coord() ?? Vector2I.Zero;
    }

    public string GetPlayerFactionId()
    {
        return _runtime?.get_player_faction_id() ?? "player";
    }

    public BattleState GetBattleState()
    {
        return _runtime?.get_battle_state();
    }

    public Vector2I GetBattleSelectedCoord()
    {
        return _runtime?.get_battle_selected_coord() ?? new Vector2I(-1, -1);
    }

    public string GetLastAdvanceBattleRefreshMode()
    {
        return _runtime?.get_last_advance_battle_refresh_mode() ?? "";
    }

    public StringName GetSelectedBattleSkillId()
    {
        return _runtime?.get_selected_battle_skill_id() ?? new StringName("");
    }

    public string GetSelectedBattleSkillName()
    {
        return _runtime?.get_selected_battle_skill_name() ?? "";
    }

    public string GetSelectedBattleSkillVariantName()
    {
        return _runtime?.get_selected_battle_skill_variant_name() ?? "";
    }

    public StringName GetSelectedBattleSkillVariantId()
    {
        return _runtime?.get_selected_battle_skill_variant_id() ?? new StringName("");
    }

    public Array<Vector2I> GetSelectedBattleSkillTargetCoords()
    {
        return _runtime?.get_selected_battle_skill_target_coords() ?? new Array<Vector2I>();
    }

    public Array<StringName> GetSelectedBattleSkillTargetUnitIds()
    {
        return _runtime?.get_selected_battle_skill_target_unit_ids() ?? new Array<StringName>();
    }

    public Array<Vector2I> GetBattleOverlayTargetCoords()
    {
        return _runtime?.get_battle_overlay_target_coords() ?? new Array<Vector2I>();
    }

    public int GetSelectedBattleSkillRequiredCoordCount()
    {
        return _runtime?.get_selected_battle_skill_required_coord_count() ?? 0;
    }

    public string GetActiveBattleEncounterName()
    {
        return _runtime?.get_active_battle_encounter_name() ?? "";
    }

    public Dictionary GetSettlementWindowData(string settlementId = "")
    {
        return _runtime?.get_settlement_window_data(settlementId) ?? new Dictionary();
    }

    public string GetSettlementFeedbackText()
    {
        return _runtime?.get_settlement_feedback_text() ?? "";
    }

    public Dictionary GetShopWindowData()
    {
        return _runtime?.get_shop_window_data() ?? new Dictionary();
    }

    public Dictionary GetContractBoardWindowData()
    {
        return _runtime?.get_contract_board_window_data() ?? new Dictionary();
    }

    public Dictionary GetForgeWindowData()
    {
        return _runtime?.get_forge_window_data() ?? new Dictionary();
    }

    public Dictionary GetStagecoachWindowData()
    {
        return _runtime?.get_stagecoach_window_data() ?? new Dictionary();
    }

    public Dictionary GetCharacterInfoContext()
    {
        return _runtime?.get_character_info_context() ?? new Dictionary();
    }

    public PartyState GetPartyState()
    {
        return _runtime?.get_party_state();
    }

    public CharacterManagementModule GetCharacterManagement()
    {
        return _runtime?.get_character_management();
    }

    public StringName GetPartySelectedMemberId()
    {
        return _runtime?.get_party_selected_member_id() ?? new StringName("");
    }

    public Dictionary GetWarehouseWindowData()
    {
        return _runtime?.get_warehouse_window_data() ?? new Dictionary();
    }

    public Dictionary GetCurrentPromotionPrompt()
    {
        return _runtime?.get_current_promotion_prompt() ?? new Dictionary();
    }

    public PendingCharacterReward GetActiveReward()
    {
        return _runtime?.get_active_reward();
    }

    public int GetPendingRewardCount()
    {
        return _runtime?.get_pending_reward_count() ?? 0;
    }

    public bool IsBattleActive()
    {
        return _runtime?.is_battle_active() ?? false;
    }

    public bool IsModalWindowOpen()
    {
        return _runtime?.is_modal_window_open() ?? false;
    }

    public bool IsSubmapActive()
    {
        return _runtime?.is_submap_active() ?? false;
    }

    public Dictionary CommandWorldMove(Vector2I direction, int count = 1)
    {
        return RunRuntimeCommand(() => _runtime.command_world_move(direction, count));
    }

    public Dictionary CommandWorldSelect(Vector2I coord)
    {
        return RunRuntimeCommand(() => _runtime.command_world_select(coord));
    }

    public Dictionary CommandOpenSettlement(Vector2I coord = default)
    {
        return RunRuntimeCommand(() => _runtime.command_open_settlement(coord));
    }

    public Dictionary CommandWorldInspect(Vector2I coord)
    {
        return RunRuntimeCommand(() => _runtime.command_world_inspect(coord));
    }

    public Dictionary CommandOpenParty()
    {
        return RunRuntimeCommand(() => _runtime.command_open_party());
    }

    public Dictionary CommandAcceptQuest(StringName questId, bool allowReaccept = false)
    {
        return RunRuntimeCommand(() => _runtime.command_accept_quest(questId, allowReaccept));
    }

    public Dictionary CommandProgressQuest(
        StringName questId,
        StringName objectiveId,
        int progressDelta = 1,
        Dictionary payload = null
    )
    {
        return RunRuntimeCommand(
            () =>
                _runtime.command_progress_quest(
                    questId,
                    objectiveId,
                    progressDelta,
                    payload ?? new Dictionary()
                )
        );
    }

    public Dictionary CommandCompleteQuest(StringName questId)
    {
        return RunRuntimeCommand(() => _runtime.command_complete_quest(questId));
    }

    public Dictionary CommandSelectPartyMember(StringName memberId)
    {
        return RunRuntimeCommand(() => _runtime.command_select_party_member(memberId));
    }

    public Dictionary CommandSetPartyLeader(StringName memberId)
    {
        return RunRuntimeCommand(() => _runtime.command_set_party_leader(memberId));
    }

    public Dictionary CommandMoveMemberToActive(StringName memberId)
    {
        return RunRuntimeCommand(() => _runtime.command_move_member_to_active(memberId));
    }

    public Dictionary CommandMoveMemberToReserve(StringName memberId)
    {
        return RunRuntimeCommand(() => _runtime.command_move_member_to_reserve(memberId));
    }

    public Dictionary CommandOpenPartyWarehouse()
    {
        return RunRuntimeCommand(() => _runtime.command_open_party_warehouse());
    }

    public Dictionary CommandWarehouseDiscardOne(StringName itemId, StringName instanceId = default)
    {
        return RunRuntimeCommand(() => _runtime.command_warehouse_discard_one(itemId, instanceId));
    }

    public Dictionary CommandWarehouseDiscardAll(StringName itemId, StringName instanceId = default)
    {
        return RunRuntimeCommand(() => _runtime.command_warehouse_discard_all(itemId, instanceId));
    }

    public Dictionary CommandWarehouseUseItem(
        StringName itemId,
        StringName memberId = default,
        Dictionary options = null
    )
    {
        return RunRuntimeCommand(
            () => _runtime.command_warehouse_use_item(itemId, memberId, options ?? new Dictionary())
        );
    }

    public Dictionary CommandExecuteSettlementAction(string actionId, Dictionary payload = null)
    {
        return RunRuntimeCommand(
            () => _runtime.command_execute_settlement_action(actionId, payload ?? new Dictionary())
        );
    }

    public Dictionary CommandShopBuy(StringName itemId, int quantity = 1)
    {
        return RunRuntimeCommand(() => _runtime.command_shop_buy(itemId, quantity));
    }

    public Dictionary CommandShopSell(
        StringName itemId,
        int quantity = 1,
        StringName instanceId = default
    )
    {
        return RunRuntimeCommand(() => _runtime.command_shop_sell(itemId, quantity, instanceId));
    }

    public Dictionary CommandStagecoachTravel(string settlementId)
    {
        return RunRuntimeCommand(() => _runtime.command_stagecoach_travel(settlementId));
    }

    public Dictionary CommandBattleTick(int tickCount)
    {
        return RunRuntimeCommand(() => _runtime.command_battle_tick(tickCount));
    }

    public Dictionary CommandBattleSelectSkill(int slotIndex)
    {
        return RunRuntimeCommand(() => _runtime.command_battle_select_skill(slotIndex));
    }

    public Dictionary CommandBattleCycleVariant(int step)
    {
        return RunRuntimeCommand(() => _runtime.command_battle_cycle_variant(step));
    }

    public Dictionary CommandBattleClearSkill()
    {
        return RunRuntimeCommand(() => _runtime.command_battle_clear_skill());
    }

    public Dictionary CommandBattleMoveTo(Vector2I targetCoord)
    {
        return RunRuntimeCommand(() => _runtime.command_battle_move_to(targetCoord));
    }

    public Dictionary CommandBattleMoveDirection(Vector2I direction)
    {
        return RunRuntimeCommand(() => _runtime.command_battle_move_direction(direction));
    }

    public Dictionary CommandBattleWaitOrResolve()
    {
        return RunRuntimeCommand(() => _runtime.command_battle_wait_or_resolve());
    }

    public Dictionary CommandBattleInspect(Vector2I coord)
    {
        return RunRuntimeCommand(() => _runtime.command_battle_inspect(coord));
    }

    public BattlePreview PreviewBattleCommand(BattleCommand command)
    {
        return _runtime != null ? _runtime.preview_battle_command(command) : null;
    }

    public Dictionary IssueBattleCommand(BattleCommand command)
    {
        if (_runtime == null)
            return RuntimeUnavailableError();
        if (command == null)
            return new Dictionary { ["ok"] = false, ["message"] = "战斗命令无效。" };
        var refreshMode = _runtime.issue_battle_command(command).ToString();
        if (string.IsNullOrEmpty(refreshMode))
            refreshMode = "full";
        var message = _runtime.get_status_text();
        var result = new Dictionary
        {
            ["ok"] = true,
            ["message"] = message,
            ["battle_refresh_mode"] = refreshMode,
        };
        _renderTarget?._render_from_runtime(true, result);
        return result;
    }

    public Dictionary CommandConfirmPendingReward()
    {
        return RunRuntimeCommand(() => _runtime.command_confirm_pending_reward());
    }

    public Dictionary CommandChoosePromotion(StringName professionId)
    {
        return RunRuntimeCommand(() => _runtime.command_choose_promotion(professionId));
    }

    public Dictionary CommandConfirmSubmapEntry()
    {
        return RunRuntimeCommand(() => _runtime.command_confirm_submap_entry());
    }

    public Dictionary CommandConfirmBattleStart()
    {
        return RunRuntimeCommand(() => _runtime.command_confirm_battle_start());
    }

    public Dictionary CommandCancelSubmapEntry()
    {
        return RunRuntimeCommand(() => _runtime.command_cancel_submap_entry());
    }

    public Dictionary CommandReturnFromSubmap()
    {
        return RunRuntimeCommand(() => _runtime.command_return_from_submap());
    }

    public Dictionary CommandCloseActiveModal()
    {
        return RunRuntimeCommand(() => _runtime.command_close_active_modal());
    }

    public Dictionary ApplyPartyRoster(
        Array<StringName> activeMemberIds,
        Array<StringName> reserveMemberIds
    )
    {
        return RunRuntimeCommand(() => _runtime.apply_party_roster(activeMemberIds, reserveMemberIds));
    }

    public Dictionary SubmitPromotionChoice(
        StringName memberId,
        StringName professionId,
        Dictionary selection
    )
    {
        return RunRuntimeCommand(
            () => _runtime.submit_promotion_choice(memberId, professionId, selection)
        );
    }

    public Dictionary CancelPromotionChoice()
    {
        return RunRuntimeCommand(() => _runtime.cancel_promotion_choice());
    }

    public Dictionary ConfirmActiveReward()
    {
        return RunRuntimeCommand(() => _runtime.confirm_active_reward());
    }

    public Dictionary ResetBattleFocus()
    {
        return RunRuntimeCommand(() => _runtime.reset_battle_focus());
    }

    public Dictionary SelectWorldCell(Vector2I coord)
    {
        return RunRuntimeCommand(() => _runtime.select_world_cell(coord));
    }

    public Dictionary InspectWorldCell(Vector2I coord)
    {
        return RunRuntimeCommand(() => _runtime.inspect_world_cell(coord));
    }

    public Dictionary SelectBattleCell(Vector2I coord)
    {
        return RunRuntimeCommand(() => _runtime.select_battle_cell(coord));
    }

    public Dictionary InspectBattleCell(Vector2I coord)
    {
        return RunRuntimeCommand(() => _runtime.inspect_battle_cell(coord));
    }

    public void dispose() => Dispose();

    public string get_status_text() => GetStatusText();

    public string get_active_modal_id() => GetActiveModalId();

    public Dictionary get_game_over_context() => GetGameOverContext();

    public string get_active_settlement_id() => GetActiveSettlementId();

    public string get_active_map_id() => GetActiveMapId();

    public string get_active_map_display_name() => GetActiveMapDisplayName();

    public string get_submap_return_hint_text() => GetSubmapReturnHintText();

    public Dictionary get_pending_submap_prompt() => GetPendingSubmapPrompt();

    public Dictionary get_pending_battle_start_prompt() => GetPendingBattleStartPrompt();

    public Dictionary get_log_snapshot(int limit = 80) => GetLogSnapshot(limit);

    public Dictionary build_headless_snapshot() => BuildHeadlessSnapshot();

    public string build_text_snapshot() => BuildTextSnapshot();

    public bool advance(float delta) => Advance(delta);

    public WorldMapGridSystem get_grid_system() => GetGridSystem();

    public WorldMapFogSystem get_fog_system() => GetFogSystem();

    public Dictionary get_world_data() => GetWorldData();

    public Vector2I get_player_coord() => GetPlayerCoord();

    public bool is_player_visible_on_world_map() => IsPlayerVisibleOnWorldMap();

    public Vector2I get_selected_coord() => GetSelectedCoord();

    public string get_player_faction_id() => GetPlayerFactionId();

    public BattleState get_battle_state() => GetBattleState();

    public Vector2I get_battle_selected_coord() => GetBattleSelectedCoord();

    public string get_last_advance_battle_refresh_mode() => GetLastAdvanceBattleRefreshMode();

    public StringName get_selected_battle_skill_id() => GetSelectedBattleSkillId();

    public string get_selected_battle_skill_name() => GetSelectedBattleSkillName();

    public string get_selected_battle_skill_variant_name() => GetSelectedBattleSkillVariantName();

    public StringName get_selected_battle_skill_variant_id() => GetSelectedBattleSkillVariantId();

    public Array<Vector2I> get_selected_battle_skill_target_coords() =>
        GetSelectedBattleSkillTargetCoords();

    public Array<StringName> get_selected_battle_skill_target_unit_ids() =>
        GetSelectedBattleSkillTargetUnitIds();

    public Array<Vector2I> get_battle_overlay_target_coords() => GetBattleOverlayTargetCoords();

    public int get_selected_battle_skill_required_coord_count() =>
        GetSelectedBattleSkillRequiredCoordCount();

    public string get_active_battle_encounter_name() => GetActiveBattleEncounterName();

    public Dictionary get_settlement_window_data(string settlement_id = "") =>
        GetSettlementWindowData(settlement_id);

    public string get_settlement_feedback_text() => GetSettlementFeedbackText();

    public Dictionary get_shop_window_data() => GetShopWindowData();

    public Dictionary get_contract_board_window_data() => GetContractBoardWindowData();

    public Dictionary get_forge_window_data() => GetForgeWindowData();

    public Dictionary get_stagecoach_window_data() => GetStagecoachWindowData();

    public Dictionary get_character_info_context() => GetCharacterInfoContext();

    public PartyState get_party_state() => GetPartyState();

    public CharacterManagementModule get_character_management() => GetCharacterManagement();

    public StringName get_party_selected_member_id() => GetPartySelectedMemberId();

    public Dictionary get_warehouse_window_data() => GetWarehouseWindowData();

    public Dictionary get_current_promotion_prompt() => GetCurrentPromotionPrompt();

    public PendingCharacterReward get_active_reward() => GetActiveReward();

    public int get_pending_reward_count() => GetPendingRewardCount();

    public bool is_battle_active() => IsBattleActive();

    public bool is_modal_window_open() => IsModalWindowOpen();

    public bool is_submap_active() => IsSubmapActive();

    public Dictionary command_world_move(Vector2I direction, int count = 1) =>
        CommandWorldMove(direction, count);

    public Dictionary command_world_select(Vector2I coord) => CommandWorldSelect(coord);

    public Dictionary command_open_settlement() => CommandOpenSettlement(new Vector2I(-1, -1));

    public Dictionary command_open_settlement(Vector2I coord) => CommandOpenSettlement(coord);

    public Dictionary command_world_inspect(Vector2I coord) => CommandWorldInspect(coord);

    public Dictionary command_open_party() => CommandOpenParty();

    public Dictionary command_accept_quest(StringName quest_id, bool allow_reaccept = false) =>
        CommandAcceptQuest(quest_id, allow_reaccept);

    public Dictionary command_progress_quest(
        StringName quest_id,
        StringName objective_id,
        int progress_delta = 1,
        Dictionary payload = null
    ) => CommandProgressQuest(quest_id, objective_id, progress_delta, payload);

    public Dictionary command_complete_quest(StringName quest_id) => CommandCompleteQuest(quest_id);

    public Dictionary command_select_party_member(StringName member_id) =>
        CommandSelectPartyMember(member_id);

    public Dictionary command_set_party_leader(StringName member_id) =>
        CommandSetPartyLeader(member_id);

    public Dictionary command_move_member_to_active(StringName member_id) =>
        CommandMoveMemberToActive(member_id);

    public Dictionary command_move_member_to_reserve(StringName member_id) =>
        CommandMoveMemberToReserve(member_id);

    public Dictionary command_open_party_warehouse() => CommandOpenPartyWarehouse();

    public Dictionary command_warehouse_discard_one(
        StringName item_id,
        StringName instance_id = default
    ) => CommandWarehouseDiscardOne(item_id, instance_id);

    public Dictionary command_warehouse_discard_all(
        StringName item_id,
        StringName instance_id = default
    ) => CommandWarehouseDiscardAll(item_id, instance_id);

    public Dictionary command_warehouse_use_item(
        StringName item_id,
        StringName member_id = default,
        Dictionary options = null
    ) => CommandWarehouseUseItem(item_id, member_id, options);

    public Dictionary command_execute_settlement_action(
        string action_id,
        Dictionary payload = null
    ) => CommandExecuteSettlementAction(action_id, payload);

    public Dictionary command_shop_buy(StringName item_id, int quantity = 1) =>
        CommandShopBuy(item_id, quantity);

    public Dictionary command_shop_sell(
        StringName item_id,
        int quantity = 1,
        StringName instance_id = default
    ) => CommandShopSell(item_id, quantity, instance_id);

    public Dictionary command_stagecoach_travel(string settlement_id) =>
        CommandStagecoachTravel(settlement_id);

    public Dictionary command_battle_tick(int tick_count) => CommandBattleTick(tick_count);

    public Dictionary command_battle_select_skill(int slot_index) =>
        CommandBattleSelectSkill(slot_index);

    public Dictionary command_battle_cycle_variant(int step) => CommandBattleCycleVariant(step);

    public Dictionary command_battle_clear_skill() => CommandBattleClearSkill();

    public Dictionary command_battle_move_to(Vector2I target_coord) =>
        CommandBattleMoveTo(target_coord);

    public Dictionary command_battle_move_direction(Vector2I direction) =>
        CommandBattleMoveDirection(direction);

    public Dictionary command_battle_wait_or_resolve() => CommandBattleWaitOrResolve();

    public Dictionary command_battle_inspect(Vector2I coord) => CommandBattleInspect(coord);

    public BattlePreview preview_battle_command(BattleCommand command) => PreviewBattleCommand(command);

    public Dictionary issue_battle_command(BattleCommand command) => IssueBattleCommand(command);

    public Dictionary command_confirm_pending_reward() => CommandConfirmPendingReward();

    public Dictionary command_choose_promotion(StringName profession_id) =>
        CommandChoosePromotion(profession_id);

    public Dictionary command_confirm_submap_entry() => CommandConfirmSubmapEntry();

    public Dictionary command_confirm_battle_start() => CommandConfirmBattleStart();

    public Dictionary command_cancel_submap_entry() => CommandCancelSubmapEntry();

    public Dictionary command_return_from_submap() => CommandReturnFromSubmap();

    public Dictionary command_close_active_modal() => CommandCloseActiveModal();

    public Dictionary apply_party_roster(
        Array<StringName> active_member_ids,
        Array<StringName> reserve_member_ids
    ) => ApplyPartyRoster(active_member_ids, reserve_member_ids);

    public Dictionary submit_promotion_choice(
        StringName member_id,
        StringName profession_id,
        Dictionary selection
    ) => SubmitPromotionChoice(member_id, profession_id, selection);

    public Dictionary cancel_promotion_choice() => CancelPromotionChoice();

    public Dictionary confirm_active_reward() => ConfirmActiveReward();

    public Dictionary reset_battle_focus() => ResetBattleFocus();

    public Dictionary select_world_cell(Vector2I coord) => SelectWorldCell(coord);

    public Dictionary inspect_world_cell(Vector2I coord) => InspectWorldCell(coord);

    public Dictionary select_battle_cell(Vector2I coord) => SelectBattleCell(coord);

    public Dictionary inspect_battle_cell(Vector2I coord) => InspectBattleCell(coord);

    private Dictionary RunRuntimeCommand(Func<Dictionary> command)
    {
        if (_runtime == null)
            return RuntimeUnavailableError();
        var result = command?.Invoke() ?? new Dictionary();
        _renderTarget?._render_from_runtime(true, result);
        return result;
    }

    private static Dictionary RuntimeUnavailableError()
    {
        return new Dictionary { ["ok"] = false, ["message"] = RuntimeUnavailableMessage };
    }
}
