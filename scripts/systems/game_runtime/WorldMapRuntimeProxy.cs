using Godot;
using Godot.Collections;

[GlobalClass]
public partial class WorldMapRuntimeProxy : RefCounted
{
    private static readonly string RuntimeUnavailableMessage = "运行时尚未初始化。";

    private GodotObject _runtime;
    private Callable _renderCallback;

    public void Setup(GodotObject runtime, Callable renderCallback = default(Callable))
    {
        _runtime = runtime;
        _renderCallback = renderCallback;
    }

    public new void Dispose()
    {
        _runtime = null;
        _renderCallback = default(Callable);
    }

    public string GetStatusText()
    {
        return CallRuntimeRead("get_status_text", "").AsString();
    }

    public string GetActiveModalId()
    {
        return CallRuntimeRead("get_active_modal_id", "").AsString();
    }

    public Dictionary GetGameOverContext()
    {
        return CallRuntimeRead("get_game_over_context", new Dictionary()).AsGodotDictionary();
    }

    public string GetActiveSettlementId()
    {
        return CallRuntimeRead("get_active_settlement_id", "").AsString();
    }

    public string GetActiveMapId()
    {
        return CallRuntimeRead("get_active_map_id", "").AsString();
    }

    public string GetActiveMapDisplayName()
    {
        return CallRuntimeRead("get_active_map_display_name", "").AsString();
    }

    public string GetSubmapReturnHintText()
    {
        return CallRuntimeRead("get_submap_return_hint_text", "").AsString();
    }

    public Dictionary GetPendingSubmapPrompt()
    {
        return CallRuntimeRead("get_pending_submap_prompt", new Dictionary()).AsGodotDictionary();
    }

    public Dictionary GetPendingBattleStartPrompt()
    {
        return CallRuntimeRead("get_pending_battle_start_prompt", new Dictionary()).AsGodotDictionary();
    }

    public Dictionary GetLogSnapshot(int limit = 80)
    {
        return CallRuntimeRead("get_log_snapshot", new Dictionary(), new Godot.Collections.Array { limit }).AsGodotDictionary();
    }

    public Dictionary BuildHeadlessSnapshot()
    {
        return CallRuntimeRead("build_headless_snapshot", new Dictionary()).AsGodotDictionary();
    }

    public string BuildTextSnapshot()
    {
        return CallRuntimeRead("build_text_snapshot", "").AsString();
    }

    public bool Advance(float delta)
    {
        return CallRuntimeRead("advance", false, new Godot.Collections.Array { delta }).AsBool();
    }

    public Variant GetGridSystem()
    {
        return CallRuntimeRead("get_grid_system", default(Variant));
    }

    public Variant GetFogSystem()
    {
        return CallRuntimeRead("get_fog_system", default(Variant));
    }

    public Dictionary GetWorldData()
    {
        return CallRuntimeRead("get_world_data", new Dictionary()).AsGodotDictionary();
    }

    public Vector2I GetPlayerCoord()
    {
        return CallRuntimeRead("get_player_coord", Vector2I.Zero).AsVector2I();
    }

    public bool IsPlayerVisibleOnWorldMap()
    {
        return CallRuntimeRead("is_player_visible_on_world_map", true).AsBool();
    }

    public Vector2I GetSelectedCoord()
    {
        return CallRuntimeRead("get_selected_coord", Vector2I.Zero).AsVector2I();
    }

    public string GetPlayerFactionId()
    {
        return CallRuntimeRead("get_player_faction_id", "player").AsString();
    }

    public BattleState GetBattleState()
    {
        return CallRuntimeRead("get_battle_state", default(Variant)).As<BattleState>();
    }

    public Vector2I GetBattleSelectedCoord()
    {
        return CallRuntimeRead("get_battle_selected_coord", new Vector2I(-1, -1)).AsVector2I();
    }

    public string GetLastAdvanceBattleRefreshMode()
    {
        return CallRuntimeRead("get_last_advance_battle_refresh_mode", "").AsString();
    }

    public StringName GetSelectedBattleSkillId()
    {
        return CallRuntimeRead("get_selected_battle_skill_id", "").AsStringName();
    }

    public string GetSelectedBattleSkillName()
    {
        return CallRuntimeRead("get_selected_battle_skill_name", "").AsString();
    }

    public string GetSelectedBattleSkillVariantName()
    {
        return CallRuntimeRead("get_selected_battle_skill_variant_name", "").AsString();
    }

    public StringName GetSelectedBattleSkillVariantId()
    {
        return CallRuntimeRead("get_selected_battle_skill_variant_id", "").AsStringName();
    }

    public Array<Vector2I> GetSelectedBattleSkillTargetCoords()
    {
        return CallRuntimeRead("get_selected_battle_skill_target_coords", new Array<Vector2I>()).AsGodotArray<Vector2I>();
    }

    public Array<StringName> GetSelectedBattleSkillTargetUnitIds()
    {
        return CallRuntimeRead("get_selected_battle_skill_target_unit_ids", new Array<StringName>()).AsGodotArray<StringName>();
    }

    public Array<Vector2I> GetBattleOverlayTargetCoords()
    {
        return CallRuntimeRead("get_battle_overlay_target_coords", new Array<Vector2I>()).AsGodotArray<Vector2I>();
    }

    public int GetSelectedBattleSkillRequiredCoordCount()
    {
        return CallRuntimeRead("get_selected_battle_skill_required_coord_count", 0).AsInt32();
    }

    public string GetActiveBattleEncounterName()
    {
        return CallRuntimeRead("get_active_battle_encounter_name", "").AsString();
    }

    public Dictionary GetSettlementWindowData(string settlementId = "")
    {
        return CallRuntimeRead("get_settlement_window_data", new Dictionary(), new Godot.Collections.Array { settlementId }).AsGodotDictionary();
    }

    public string GetSettlementFeedbackText()
    {
        return CallRuntimeRead("get_settlement_feedback_text", "").AsString();
    }

    public Dictionary GetShopWindowData()
    {
        return CallRuntimeRead("get_shop_window_data", new Dictionary()).AsGodotDictionary();
    }

    public Dictionary GetContractBoardWindowData()
    {
        return CallRuntimeRead("get_contract_board_window_data", new Dictionary()).AsGodotDictionary();
    }

    public Dictionary GetForgeWindowData()
    {
        return CallRuntimeRead("get_forge_window_data", new Dictionary()).AsGodotDictionary();
    }

    public Dictionary GetStagecoachWindowData()
    {
        return CallRuntimeRead("get_stagecoach_window_data", new Dictionary()).AsGodotDictionary();
    }

    public Dictionary GetCharacterInfoContext()
    {
        return CallRuntimeRead("get_character_info_context", new Dictionary()).AsGodotDictionary();
    }

    public Variant GetPartyState()
    {
        return CallRuntimeRead("get_party_state", default(Variant));
    }

    public Variant GetCharacterManagement()
    {
        return CallRuntimeRead("get_character_management", default(Variant));
    }

    public StringName GetPartySelectedMemberId()
    {
        return CallRuntimeRead("get_party_selected_member_id", "").AsStringName();
    }

    public Dictionary GetWarehouseWindowData()
    {
        return CallRuntimeRead("get_warehouse_window_data", new Dictionary()).AsGodotDictionary();
    }

    public Dictionary GetCurrentPromotionPrompt()
    {
        return CallRuntimeRead("get_current_promotion_prompt", new Dictionary()).AsGodotDictionary();
    }

    public Variant GetActiveReward()
    {
        return CallRuntimeRead("get_active_reward", default(Variant));
    }

    public int GetPendingRewardCount()
    {
        return CallRuntimeRead("get_pending_reward_count", 0).AsInt32();
    }

    public bool IsBattleActive()
    {
        return CallRuntimeRead("is_battle_active", false).AsBool();
    }

    public bool IsModalWindowOpen()
    {
        return CallRuntimeRead("is_modal_window_open", false).AsBool();
    }

    public bool IsSubmapActive()
    {
        return CallRuntimeRead("is_submap_active", false).AsBool();
    }

    public Dictionary CommandWorldMove(Vector2I direction, int count = 1)
    {
        return CallRuntimeCommand("command_world_move", new Godot.Collections.Array { direction, count });
    }

    public Dictionary CommandWorldSelect(Vector2I coord)
    {
        return CallRuntimeCommand("command_world_select", new Godot.Collections.Array { coord });
    }

    public Dictionary CommandOpenSettlement(Vector2I coord = default)
    {
        return CallRuntimeCommand("command_open_settlement", new Godot.Collections.Array { coord });
    }

    public Dictionary CommandWorldInspect(Vector2I coord)
    {
        return CallRuntimeCommand("command_world_inspect", new Godot.Collections.Array { coord });
    }

    public Dictionary CommandOpenParty()
    {
        return CallRuntimeCommand("command_open_party");
    }

    public Dictionary CommandAcceptQuest(StringName questId, bool allowReaccept = false)
    {
        return CallRuntimeCommand("command_accept_quest", new Godot.Collections.Array { questId, allowReaccept });
    }

    public Dictionary CommandProgressQuest(StringName questId, StringName objectiveId, int progressDelta = 1, Dictionary payload = null)
    {
        return CallRuntimeCommand("command_progress_quest", new Godot.Collections.Array { questId, objectiveId, progressDelta, payload ?? new Dictionary() });
    }

    public Dictionary CommandCompleteQuest(StringName questId)
    {
        return CallRuntimeCommand("command_complete_quest", new Godot.Collections.Array { questId });
    }

    public Dictionary CommandSelectPartyMember(StringName memberId)
    {
        return CallRuntimeCommand("command_select_party_member", new Godot.Collections.Array { memberId });
    }

    public Dictionary CommandSetPartyLeader(StringName memberId)
    {
        return CallRuntimeCommand("command_set_party_leader", new Godot.Collections.Array { memberId });
    }

    public Dictionary CommandMoveMemberToActive(StringName memberId)
    {
        return CallRuntimeCommand("command_move_member_to_active", new Godot.Collections.Array { memberId });
    }

    public Dictionary CommandMoveMemberToReserve(StringName memberId)
    {
        return CallRuntimeCommand("command_move_member_to_reserve", new Godot.Collections.Array { memberId });
    }

    public Dictionary CommandOpenPartyWarehouse()
    {
        return CallRuntimeCommand("command_open_party_warehouse");
    }

    public Dictionary CommandWarehouseDiscardOne(StringName itemId, StringName instanceId = default)
    {
        return CallRuntimeCommand("command_warehouse_discard_one", new Godot.Collections.Array { itemId, instanceId });
    }

    public Dictionary CommandWarehouseDiscardAll(StringName itemId, StringName instanceId = default)
    {
        return CallRuntimeCommand("command_warehouse_discard_all", new Godot.Collections.Array { itemId, instanceId });
    }

    public Dictionary CommandWarehouseUseItem(StringName itemId, StringName memberId = default, Dictionary options = null)
    {
        return CallRuntimeCommand("command_warehouse_use_item", new Godot.Collections.Array { itemId, memberId, options ?? new Dictionary() });
    }

    public Dictionary CommandExecuteSettlementAction(string actionId, Dictionary payload = null)
    {
        return CallRuntimeCommand("command_execute_settlement_action", new Godot.Collections.Array { actionId, payload ?? new Dictionary() });
    }

    public Dictionary CommandShopBuy(StringName itemId, int quantity = 1)
    {
        return CallRuntimeCommand("command_shop_buy", new Godot.Collections.Array { itemId, quantity });
    }

    public Dictionary CommandShopSell(StringName itemId, int quantity = 1, StringName instanceId = default)
    {
        return CallRuntimeCommand("command_shop_sell", new Godot.Collections.Array { itemId, quantity, instanceId });
    }

    public Dictionary CommandStagecoachTravel(string settlementId)
    {
        return CallRuntimeCommand("command_stagecoach_travel", new Godot.Collections.Array { settlementId });
    }

    public Dictionary CommandBattleTick(int tickCount)
    {
        return CallRuntimeCommand("command_battle_tick", new Godot.Collections.Array { tickCount });
    }

    public Dictionary CommandBattleSelectSkill(int slotIndex)
    {
        return CallRuntimeCommand("command_battle_select_skill", new Godot.Collections.Array { slotIndex });
    }

    public Dictionary CommandBattleCycleVariant(int step)
    {
        return CallRuntimeCommand("command_battle_cycle_variant", new Godot.Collections.Array { step });
    }

    public Dictionary CommandBattleClearSkill()
    {
        return CallRuntimeCommand("command_battle_clear_skill");
    }

    public Dictionary CommandBattleMoveTo(Vector2I targetCoord)
    {
        return CallRuntimeCommand("command_battle_move_to", new Godot.Collections.Array { targetCoord });
    }

    public Dictionary CommandBattleMoveDirection(Vector2I direction)
    {
        return CallRuntimeCommand("command_battle_move_direction", new Godot.Collections.Array { direction });
    }

    public Dictionary CommandBattleWaitOrResolve()
    {
        return CallRuntimeCommand("command_battle_wait_or_resolve");
    }

    public Dictionary CommandBattleInspect(Vector2I coord)
    {
        return CallRuntimeCommand("command_battle_inspect", new Godot.Collections.Array { coord });
    }

    public Variant PreviewBattleCommand(Variant command)
    {
        return CallRuntimeRead("preview_battle_command", default(Variant), new Godot.Collections.Array { command });
    }

    public Dictionary IssueBattleCommand(Variant command)
    {
        if (_runtime == null)
            return RuntimeUnavailableError();
        if (!_runtime.HasMethod("issue_battle_command"))
            return new Dictionary { ["ok"] = false, ["message"] = "运行时缺少接口 issue_battle_command。" };
        var refreshMode = _runtime.Call("issue_battle_command", command).AsString();
        if (string.IsNullOrEmpty(refreshMode))
            refreshMode = "full";
        var message = "";
        if (_runtime.HasMethod("get_status_text"))
            message = _runtime.Call("get_status_text").AsString();
        var result = new Dictionary { ["ok"] = true, ["message"] = message, ["battle_refresh_mode"] = refreshMode };
        if (!_renderCallback.Equals(default(Callable)))
            _renderCallback.Call(true, result);
        return result;
    }

    public Dictionary CommandConfirmPendingReward()
    {
        return CallRuntimeCommand("command_confirm_pending_reward");
    }

    public Dictionary CommandChoosePromotion(StringName professionId)
    {
        return CallRuntimeCommand("command_choose_promotion", new Godot.Collections.Array { professionId });
    }

    public Dictionary CommandConfirmSubmapEntry()
    {
        return CallRuntimeCommand("command_confirm_submap_entry");
    }

    public Dictionary CommandConfirmBattleStart()
    {
        return CallRuntimeCommand("command_confirm_battle_start");
    }

    public Dictionary CommandCancelSubmapEntry()
    {
        return CallRuntimeCommand("command_cancel_submap_entry");
    }

    public Dictionary CommandReturnFromSubmap()
    {
        return CallRuntimeCommand("command_return_from_submap");
    }

    public Dictionary CommandCloseActiveModal()
    {
        return CallRuntimeCommand("command_close_active_modal");
    }

    public Dictionary ApplyPartyRoster(Array<StringName> activeMemberIds, Array<StringName> reserveMemberIds)
    {
        return CallRuntimeCommand("apply_party_roster", new Godot.Collections.Array { activeMemberIds, reserveMemberIds });
    }

    public Dictionary SubmitPromotionChoice(StringName memberId, StringName professionId, Dictionary selection)
    {
        return CallRuntimeCommand("submit_promotion_choice", new Godot.Collections.Array { memberId, professionId, selection });
    }

    public Dictionary CancelPromotionChoice()
    {
        return CallRuntimeCommand("cancel_promotion_choice");
    }

    public Dictionary ConfirmActiveReward()
    {
        return CallRuntimeCommand("confirm_active_reward");
    }

    public Dictionary ResetBattleFocus()
    {
        return CallRuntimeCommand("reset_battle_focus");
    }

    public Dictionary SelectWorldCell(Vector2I coord)
    {
        return CallRuntimeCommand("select_world_cell", new Godot.Collections.Array { coord });
    }

    public Dictionary InspectWorldCell(Vector2I coord)
    {
        return CallRuntimeCommand("inspect_world_cell", new Godot.Collections.Array { coord });
    }

    public Dictionary SelectBattleCell(Vector2I coord)
    {
        return CallRuntimeCommand("select_battle_cell", new Godot.Collections.Array { coord });
    }

    public Dictionary InspectBattleCell(Vector2I coord)
    {
        return CallRuntimeCommand("inspect_battle_cell", new Godot.Collections.Array { coord });
    }

    private Variant CallRuntimeRead(StringName methodName, Variant defaultValue, Godot.Collections.Array args = null)
    {
        if (_runtime == null)
            return defaultValue;
        var method = methodName.ToString();
        if (!_runtime.HasMethod(method))
            return defaultValue;
        return _runtime.Callv(method, args ?? new Godot.Collections.Array());
    }

    private Dictionary CallRuntimeCommand(StringName methodName, Godot.Collections.Array args = null)
    {
        if (_runtime == null)
            return RuntimeUnavailableError();
        var method = methodName.ToString();
        if (!_runtime.HasMethod(method))
            return new Dictionary { ["ok"] = false, ["message"] = string.Format("运行时缺少接口 {0}。", method) };
        var resultVariant = _runtime.Callv(method, args ?? new Godot.Collections.Array());
        Dictionary result;
        if (resultVariant.VariantType == Variant.Type.Dictionary)
            result = resultVariant.AsGodotDictionary();
        else
        {
            var typeName = resultVariant.VariantType.ToString();
            GD.PushWarning(string.Format("WorldMapRuntimeProxy.{0} 返回了非 Dictionary 结果（{1}），已改为错误结果。", method, typeName));
            result = new Dictionary { ["ok"] = false, ["message"] = string.Format("运行时接口 {0} 返回了非 Dictionary 结果。", method), ["invalid_result_type"] = typeName };
        }
        if (!_renderCallback.Equals(default(Callable)))
            _renderCallback.Call(true, result);
        return result;
    }

    private static Dictionary RuntimeUnavailableError()
    {
        return new Dictionary { ["ok"] = false, ["message"] = RuntimeUnavailableMessage };
    }
}
