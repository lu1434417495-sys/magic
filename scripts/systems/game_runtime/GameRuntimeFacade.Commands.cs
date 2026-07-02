using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

// Partial slice of GameRuntimeFacade — runtime Command*Typed surface + command execution/logging infra.
// Pure physical split: same class, no behavior change. See GameRuntimeFacade.cs.
public sealed partial class GameRuntimeFacade
{

    internal RuntimeCommandResult CommandWorldMoveTyped(Vector2I direction, int count) =>
        ExecuteLoggedCommandTyped(
            "world.move",
            "world",
            new GDictionary { ["direction"] = direction, ["count"] = count },
            () =>
            {
                if (_generation_config == null)
                    return BuildCommandErrorResult("世界地图尚未初始化。");
                if (IsBattleActive())
                    return BuildCommandErrorResult("当前处于战斗中，不能执行大地图移动。");
                if (IsModalWindowOpenInternal())
                    return BuildCommandErrorResult("当前有窗口打开，不能执行大地图移动。");
                if (direction == Vector2I.Zero)
                    return BuildCommandErrorResult("移动方向不能为空。");
                int moveCount = Math.Min(Math.Max(count, 1), MaxCommandWorldMoveCount);
                for (int i = 0; i < moveCount; i++)
                {
                    _move_player(direction);
                    if (IsBattleActive() || IsModalWindowOpenInternal())
                        break;
                }
                return BuildCommandOkResult();
            }
        );

    internal RuntimeCommandResult CommandWorldSelectTyped(Vector2I coord) =>
        ExecuteLoggedCommandTyped(
            "world.select",
            "world",
            new GDictionary { ["coord"] = coord },
            () =>
            {
                if (_generation_config == null)
                    return BuildCommandErrorResult("世界地图尚未初始化。");
                if (IsBattleActive())
                    return BuildCommandErrorResult("当前处于战斗中，不能选择大地图坐标。");
                if (IsModalWindowOpenInternal())
                    return BuildCommandErrorResult("当前有窗口打开，不能切换大地图选择。");
                if (!_grid_system.IsCellWalkable(coord))
                    return BuildCommandErrorResult("该大地图格超出当前世界范围。");
                _selected_coord = coord;
                UpdateStatusInternal($"已选中格子 {FormatCoordInternal(coord)}。");
                return BuildCommandOkResult();
            }
        );

    internal RuntimeCommandResult CommandOpenSettlementTyped() =>
        CommandOpenSettlementTyped(new Vector2I(-1, -1));

    internal RuntimeCommandResult CommandOpenSettlementTyped(Vector2I coord) =>
        ExecuteLoggedCommandTyped(
            "settlement.open",
            "settlement",
            new GDictionary { ["coord"] = coord },
            () =>
            {
                if (_generation_config == null)
                    return BuildCommandErrorResult("世界地图尚未初始化。");
                if (IsBattleActive())
                    return BuildCommandErrorResult("当前处于战斗中，不能打开据点。");
                if (IsModalWindowOpenInternal())
                    return BuildCommandErrorResult("当前有窗口打开，不能打开新的据点窗口。");
                var targetCoord = coord == new Vector2I(-1, -1) ? _selected_coord : coord;
                if (_try_open_settlement_at(targetCoord))
                    return BuildCommandOkResult();
                return BuildCommandErrorResult(
                    string.IsNullOrEmpty(_current_status_message)
                        ? "据点打开失败。"
                        : _current_status_message
                );
            }
        );

    internal RuntimeCommandResult CommandWorldInspectTyped(Vector2I coord) =>
        ExecuteLoggedCommandTyped(
            "world.inspect",
            "world",
            new GDictionary { ["coord"] = coord },
            () =>
            {
                if (_generation_config == null)
                    return BuildCommandErrorResult("世界地图尚未初始化。");
                if (IsBattleActive())
                    return BuildCommandErrorResult("当前处于战斗中，不能查看大地图人物。");
                if (IsModalWindowOpenInternal())
                    return BuildCommandErrorResult("当前有窗口打开，不能查看大地图人物。");
                if (!_fog_system.IsVisible(coord, _player_faction_id))
                {
                    UpdateStatusInternal("该格当前不在视野中。");
                    return BuildCommandErrorResult(_current_status_message);
                }
                if (_try_open_character_info_at_world_coord(coord))
                    return BuildCommandOkResult();
                UpdateStatusInternal("当前格没有可查看人物。");
                return BuildCommandErrorResult(_current_status_message);
            }
        );

    internal RuntimeCommandResult CommandOpenPartyTyped() =>
        ExecuteLoggedCommandTyped(
            "party.open",
            "party",
            new GDictionary(),
            () => _party_command_handler.CommandOpenPartyTyped()
        );

    internal RuntimeCommandResult CommandAcceptQuestTyped(
        StringName quest_id,
        bool allow_reaccept
    ) =>
        ExecuteLoggedCommandTyped(
            "quest.accept",
            "quest",
            new GDictionary { ["quest_id"] = quest_id, ["allow_reaccept"] = allow_reaccept },
            () => _quest_command_handler.CommandAcceptQuestTyped(quest_id, allow_reaccept)
        );

    internal RuntimeCommandResult CommandProgressQuestTyped(
        StringName quest_id,
        StringName objective_id,
        int progress_delta,
        QuestProgressCommandPayloadData payload
    ) =>
        ExecuteLoggedCommandTyped(
            "quest.progress",
            "quest",
            new GDictionary
            {
                ["quest_id"] = quest_id,
                ["objective_id"] = objective_id,
                ["progress_delta"] = progress_delta,
                ["payload"] = BuildQuestProgressPayloadContext(payload),
            },
            () =>
                _quest_command_handler.CommandProgressQuestTyped(
                    quest_id,
                    objective_id,
                    progress_delta,
                    payload
                )
        );

    private static GDictionary BuildQuestProgressPayloadContext(
        QuestProgressCommandPayloadData payload
    )
    {
        if (payload == null)
            return new GDictionary();

        GDictionary result = new()
        {
            ["world_step"] = payload.WorldStep,
            ["action_id"] = payload.ActionId,
            ["member_id"] = payload.MemberId,
            ["enemy_template_id"] = payload.EnemyTemplateId,
            ["settlement_id"] = payload.SettlementId,
            ["source_type"] = payload.SourceType,
            ["source_id"] = payload.SourceId,
        };

        if (payload.HasTargetValue)
            result["target_value"] = payload.TargetValue;

        return result;
    }

    internal RuntimeCommandResult CommandCompleteQuestTyped(StringName quest_id) =>
        ExecuteLoggedCommandTyped(
            "quest.complete",
            "quest",
            new GDictionary { ["quest_id"] = quest_id },
            () => _quest_command_handler.CommandCompleteQuestTyped(quest_id)
        );

    internal RuntimeCommandResult CommandSubmitQuestItemTyped(
        StringName quest_id,
        StringName objective_id
    ) =>
        ExecuteLoggedCommandTyped(
            "quest.submit_item",
            "quest",
            new GDictionary { ["quest_id"] = quest_id, ["objective_id"] = objective_id },
            () => _quest_command_handler.CommandSubmitQuestItemTyped(quest_id, objective_id)
        );

    internal RuntimeCommandResult CommandClaimQuestTyped(StringName quest_id) =>
        ExecuteLoggedCommandTyped(
            "quest.claim",
            "quest",
            new GDictionary { ["quest_id"] = quest_id },
            () => _quest_command_handler.CommandClaimQuestTyped(quest_id)
        );

    internal RuntimeCommandResult CommandSelectPartyMemberTyped(StringName member_id) =>
        ExecuteLoggedCommandTyped(
            "party.select_member",
            "party",
            new GDictionary { ["member_id"] = member_id },
            () => _party_command_handler.CommandSelectPartyMemberTyped(member_id)
        );

    internal RuntimeCommandResult CommandSetPartyLeaderTyped(StringName member_id) =>
        ExecuteLoggedCommandTyped(
            "party.set_leader",
            "party",
            new GDictionary { ["member_id"] = member_id },
            () => _party_command_handler.CommandSetPartyLeaderTyped(member_id)
        );

    internal RuntimeCommandResult CommandMoveMemberToActiveTyped(StringName member_id) =>
        ExecuteLoggedCommandTyped(
            "party.move_member_to_active",
            "party",
            new GDictionary { ["member_id"] = member_id },
            () => _party_command_handler.CommandMoveMemberToActiveTyped(member_id)
        );

    internal RuntimeCommandResult CommandMoveMemberToReserveTyped(StringName member_id) =>
        ExecuteLoggedCommandTyped(
            "party.move_member_to_reserve",
            "party",
            new GDictionary { ["member_id"] = member_id },
            () => _party_command_handler.CommandMoveMemberToReserveTyped(member_id)
        );

    internal RuntimeCommandResult CommandPartyEquipItemTyped(
        StringName member_id,
        StringName item_id,
        StringName slot_id,
        StringName instance_id
    ) =>
        ExecuteLoggedCommandTyped(
            "party.equip_item",
            "party",
            new GDictionary
            {
                ["member_id"] = member_id,
                ["item_id"] = item_id,
                ["slot_id"] = slot_id,
                ["instance_id"] = instance_id,
            },
            () =>
                _party_command_handler.CommandPartyEquipItemTyped(
                    member_id,
                    item_id,
                    slot_id,
                    instance_id
                )
        );

    internal RuntimeCommandResult CommandPartyUnequipItemTyped(
        StringName member_id,
        StringName slot_id
    ) =>
        ExecuteLoggedCommandTyped(
            "party.unequip_item",
            "party",
            new GDictionary { ["member_id"] = member_id, ["slot_id"] = slot_id },
            () => _party_command_handler.CommandPartyUnequipItemTyped(member_id, slot_id)
        );

    internal RuntimeCommandResult CommandOpenPartyWarehouseTyped() =>
        ExecuteLoggedCommandTyped(
            "warehouse.open",
            "warehouse",
            new GDictionary(),
            () => _warehouse_handler.CommandOpenPartyWarehouseTyped()
        );

    internal RuntimeCommandResult CommandWarehouseDiscardOneTyped(
        StringName item_id,
        StringName instance_id
    ) =>
        ExecuteLoggedCommandTyped(
            "warehouse.discard_one",
            "warehouse",
            new GDictionary { ["item_id"] = item_id, ["instance_id"] = instance_id },
            () => _warehouse_handler.CommandDiscardOneTyped(item_id, instance_id)
        );

    internal RuntimeCommandResult CommandWarehouseDiscardAllTyped(
        StringName item_id,
        StringName instance_id
    ) =>
        ExecuteLoggedCommandTyped(
            "warehouse.discard_all",
            "warehouse",
            new GDictionary { ["item_id"] = item_id, ["instance_id"] = instance_id },
            () => _warehouse_handler.CommandDiscardAllTyped(item_id, instance_id)
        );

    internal RuntimeCommandResult CommandWarehouseUseItemTyped(
        StringName item_id,
        StringName member_id,
        PartyItemUseService.PartyItemUseOptions options
    ) =>
        ExecuteLoggedCommandTyped(
            "warehouse.use_item",
            "warehouse",
            new GDictionary
            {
                ["item_id"] = item_id,
                ["member_id"] = member_id,
                ["confirm_practice_replacement"] =
                    options?.ConfirmPracticeReplacement ?? false,
            },
            () => _warehouse_handler.CommandUseItemTyped(item_id, member_id, options)
        );

    internal RuntimeCommandResult CommandWarehouseAddItemTyped(
        StringName item_id,
        int quantity
    ) =>
        ExecuteLoggedCommandTyped(
            "warehouse.add_item",
            "warehouse",
            new GDictionary { ["item_id"] = item_id, ["quantity"] = quantity },
            () => _warehouse_handler.CommandAddItemTyped(item_id, quantity)
        );

    internal RuntimeCommandResult CommandExecuteSettlementActionTyped(
        SettlementActionRequest request
    ) =>
        ExecuteLoggedCommandTyped(
            "settlement.execute_action",
            "settlement",
            new GDictionary
            {
                ["settlement_id"] = request.SettlementId.ToString(),
                ["service_id"] = request.ServiceId.ToString(),
                ["action_id"] = request.ActionId.ToString(),
                ["member_id"] = request.MemberId.ToString(),
                ["quantity"] = request.Quantity,
                ["submission_source"] = SettlementSubmissionSources.ToPayloadValue(request.Source),
            },
            () =>
                _settlement_command_handler.CommandExecuteSettlementActionRuntimeTyped(
                    request
                )
        );

    internal RuntimeCommandResult CommandExecuteSettlementActionTyped(
        string action_id,
        GDictionary payload
    ) =>
        ExecuteLoggedCommandTyped(
            "settlement.execute_action",
            "settlement",
            new GDictionary
            {
                ["action_id"] = action_id,
                ["payload"] = payload ?? new GDictionary(),
            },
            () =>
                _settlement_command_handler.CommandExecuteSettlementActionRuntimeTyped(
                    action_id,
                    payload ?? new GDictionary()
                )
        );

    internal RuntimeCommandResult CommandShopBuyTyped(StringName item_id, int quantity) =>
        ExecuteLoggedCommandTyped(
            "shop.buy",
            "shop",
            new GDictionary { ["item_id"] = item_id, ["quantity"] = quantity },
            () => _settlement_command_handler.CommandShopBuyTyped(item_id, quantity)
        );

    internal RuntimeCommandResult CommandShopSellTyped(
        StringName item_id,
        int quantity,
        StringName instance_id
    ) =>
        ExecuteLoggedCommandTyped(
            "shop.sell",
            "shop",
            new GDictionary
            {
                ["item_id"] = item_id,
                ["quantity"] = quantity,
                ["instance_id"] = instance_id,
            },
            () =>
                _settlement_command_handler.CommandShopSellTyped(
                    item_id,
                    quantity,
                    instance_id
                )
        );

    internal RuntimeCommandResult CommandStagecoachTravelTyped(string settlement_id) =>
        ExecuteLoggedCommandTyped(
            "stagecoach.travel",
            "stagecoach",
            new GDictionary { ["settlement_id"] = settlement_id },
            () => _settlement_command_handler.CommandStagecoachTravelTyped(settlement_id)
        );

    internal RuntimeCommandResult CommandBattleTickTyped(int tick_count) =>
        ExecuteLoggedCommandTyped(
            "battle.tick",
            "battle",
            new GDictionary { ["tick_count"] = tick_count },
            () => _battle_session_facade.CommandBattleTickTyped(tick_count)
        );

    internal RuntimeCommandResult CommandBattleSelectSkillTyped(int slot_index) =>
        ExecuteLoggedCommandTyped(
            "battle.select_skill",
            "battle",
            new GDictionary { ["slot_index"] = slot_index },
            () => _battle_session_facade.CommandBattleSelectSkillTyped(slot_index)
        );

    internal RuntimeCommandResult CommandBattleCycleVariantTyped(int step) =>
        ExecuteLoggedCommandTyped(
            "battle.cycle_variant",
            "battle",
            new GDictionary { ["step"] = step },
            () => _battle_session_facade.CommandBattleCycleVariantTyped(step)
        );

    internal RuntimeCommandResult CommandBattleClearSkillTyped() =>
        ExecuteLoggedCommandTyped(
            "battle.clear_skill",
            "battle",
            new GDictionary(),
            () => _battle_session_facade.CommandBattleClearSkillTyped()
        );

    internal RuntimeCommandResult CommandBattleMoveToTyped(Vector2I target_coord) =>
        ExecuteLoggedCommandTyped(
            "battle.move_to",
            "battle",
            new GDictionary { ["target_coord"] = target_coord },
            () => _battle_session_facade.CommandBattleMoveToTyped(target_coord)
        );

    internal RuntimeCommandResult CommandBattleMoveDirectionTyped(Vector2I direction) =>
        ExecuteLoggedCommandTyped(
            "battle.move_direction",
            "battle",
            new GDictionary { ["direction"] = direction },
            () => _battle_session_facade.CommandBattleMoveDirectionTyped(direction)
        );

    internal RuntimeCommandResult CommandBattleWaitOrResolveTyped() =>
        ExecuteLoggedCommandTyped(
            "battle.wait_or_resolve",
            "battle",
            new GDictionary(),
            () => _battle_session_facade.CommandBattleWaitOrResolveTyped()
        );

    internal RuntimeCommandResult CommandBattleCancelCastTyped(StringName unit_id) =>
        ExecuteLoggedCommandTyped(
            "battle.cancel_cast",
            "battle",
            new GDictionary { ["unit_id"] = unit_id },
            () => _battle_session_facade.CommandBattleCancelCastTyped(unit_id)
        );

    internal RuntimeCommandResult CommandBattleInspectTyped(Vector2I coord) =>
        ExecuteLoggedCommandTyped(
            "battle.inspect",
            "battle",
            new GDictionary { ["coord"] = coord },
            () => _battle_session_facade.CommandBattleInspectTyped(coord)
        );

    internal RuntimeCommandResult CommandConfirmPendingRewardTyped() =>
        ExecuteLoggedCommandTyped(
            "reward.confirm_pending",
            "reward",
            new GDictionary(),
            () =>
                _reward_flow_handler != null
                    ? _reward_flow_handler.CommandConfirmPendingRewardTyped()
                    : BuildCommandErrorResult("运行时尚未初始化。")
        );

    internal RuntimeCommandResult CommandChoosePromotionTyped(StringName profession_id) =>
        ExecuteLoggedCommandTyped(
            "promotion.choose",
            "promotion",
            new GDictionary { ["profession_id"] = profession_id },
            () =>
                _reward_flow_handler != null
                    ? _reward_flow_handler.CommandChoosePromotionTyped(profession_id)
                    : BuildCommandErrorResult("运行时尚未初始化。")
        );

    internal RuntimeCommandResult CommandConfirmSubmapEntryTyped() =>
        ExecuteLoggedCommandTyped(
            "submap.confirm_entry",
            "submap",
            new GDictionary
            {
                ["target_submap_id"] = _pending_submap_prompt.TargetSubmapId.ToString(),
            },
            () =>
            {
                if (_pending_submap_prompt.IsEmpty)
                    return BuildCommandErrorResult("当前没有待确认的子地图入口。");
                return ConfirmPendingSubmapEntryTyped();
            }
        );

    internal RuntimeCommandResult CommandCancelSubmapEntryTyped() =>
        ExecuteLoggedCommandTyped(
            "submap.cancel_entry",
            "submap",
            new GDictionary
            {
                ["target_submap_id"] = _pending_submap_prompt.TargetSubmapId.ToString(),
            },
            () =>
            {
                if (_pending_submap_prompt.IsEmpty)
                    return BuildCommandErrorResult("当前没有待确认的子地图入口。");
                string targetName = string.IsNullOrEmpty(_pending_submap_prompt.TargetDisplayName)
                    ? "子地图"
                    : _pending_submap_prompt.TargetDisplayName;
                _pending_submap_prompt.Clear();
                _active_modal_kind = RuntimeModalKind.None;
                UpdateStatusInternal($"已取消进入 {targetName}。");
                return BuildCommandOkResult();
            }
        );

    internal RuntimeCommandResult CommandConfirmResourceHarvestTyped() =>
        ExecuteLoggedCommandTyped(
            "world.resource_harvest",
            "world",
            new GDictionary { ["coord"] = _pending_harvest_coord },
            HarvestPendingResourceNodeTyped
        );

    internal RuntimeCommandResult CommandCancelResourceHarvestTyped() =>
        ExecuteLoggedCommandTyped(
            "world.resource_harvest_cancel",
            "world",
            new GDictionary { ["coord"] = _pending_harvest_coord },
            CancelPendingResourceHarvestTyped
        );

    internal RuntimeCommandResult CommandConfirmBattleStartTyped() =>
        ExecuteLoggedCommandTyped(
            "battle.confirm_start",
            "battle",
            new GDictionary { ["encounter_id"] = _active_battle_encounter_id },
            () =>
            {
                if (_pending_battle_start_prompt.Count == 0)
                    return BuildCommandErrorResult("当前没有待确认的战斗开始提示。");
                if (!IsBattleActive() || _battle_state == null)
                    return BuildCommandErrorResult("当前没有待开始的战斗。");
                _pending_battle_start_prompt.Clear();
                _active_modal_kind = RuntimeModalKind.None;
                _battle_state.ModalStateKind = BattleModalStateKind.None;
                if (_battle_state.timeline != null)
                    _battle_state.timeline.frozen = false;
                _battle_runtime?.OnBattleConfirmed();
                UpdateStatusInternal("战斗开始，TU 现在按每秒 5 点推进。");
                return BuildCommandOkResult();
            }
        );

    internal RuntimeCommandResult CommandReturnFromSubmapTyped() =>
        ExecuteLoggedCommandTyped(
            "submap.return",
            "submap",
            new GDictionary { ["active_map_id"] = _world_map_data_context.active_map_id },
            () =>
            {
                if (!IsSubmapActive())
                    return BuildCommandErrorResult("当前不在子地图中。");
                if (IsBattleActive())
                    return BuildCommandErrorResult("当前处于战斗中，不能从子地图返回。");
                if (IsModalWindowOpenInternal())
                    return BuildCommandErrorResult("当前有窗口打开，不能从子地图返回。");
                return ReturnFromActiveSubmapTyped();
            }
        );

    internal RuntimeCommandResult CommandCloseActiveModalTyped() =>
        ExecuteLoggedCommandTyped(
            "modal.close_active",
            "ui",
            new GDictionary { ["modal_id"] = GetActiveModalId() },
            () =>
                _reward_flow_handler != null
                    ? _reward_flow_handler.CommandCloseActiveModalTyped()
                    : BuildCommandErrorResult("运行时尚未初始化。")
        );

    internal RuntimeCommandResult CommandApplyPartyRosterTyped(
        GStringNameArray active_member_ids,
        GStringNameArray reserve_member_ids
    ) =>
        ExecuteLoggedCommandTyped(
            "party.apply_roster",
            "party",
            new GDictionary
            {
                ["active_member_ids"] = active_member_ids,
                ["reserve_member_ids"] = reserve_member_ids,
            },
            () => _party_command_handler.CommandApplyPartyRosterTyped(active_member_ids, reserve_member_ids)
        );

    internal RuntimeCommandResult CommandSubmitPromotionChoiceTyped(
        StringName member_id,
        StringName profession_id,
        PromotionSelectionData selection
    ) =>
        ExecuteLoggedCommandTyped(
            "promotion.submit_choice",
            "promotion",
            new GDictionary
            {
                ["member_id"] = member_id,
                ["profession_id"] = profession_id,
                ["selection"] = selection?.ToPayloadProjection() ?? new GDictionary(),
            },
            () =>
                _reward_flow_handler != null
                    ? _reward_flow_handler.CommandSubmitPromotionChoiceTyped(
                        member_id,
                        profession_id,
                        selection
                    )
                    : BuildCommandErrorResult("运行时尚未初始化。")
        );

    internal RuntimeCommandResult CommandCancelPromotionChoiceTyped() =>
        ExecuteLoggedCommandTyped(
            "promotion.cancel_choice",
            "promotion",
            new GDictionary(),
            () =>
                _reward_flow_handler != null
                    ? _reward_flow_handler.CommandCancelPromotionChoiceTyped()
                    : BuildCommandErrorResult("运行时尚未初始化。")
        );

    internal RuntimeCommandResult CommandConfirmActiveRewardTyped() =>
        ExecuteLoggedCommandTyped(
            "reward.confirm_active",
            "reward",
            new GDictionary(),
            () =>
                _reward_flow_handler != null
                    ? _reward_flow_handler.CommandConfirmActiveRewardTyped()
                    : BuildCommandErrorResult("运行时尚未初始化。")
        );

    internal RuntimeCommandResult ResetBattleFocusTyped() =>
        ExecuteLoggedCommandTyped(
            "battle.reset_focus",
            "battle",
            new GDictionary(),
            () => _battle_session_facade.ResetBattleFocusTyped()
        );

    internal RuntimeCommandResult SelectWorldCellTyped(Vector2I coord) =>
        ExecuteLoggedCommandTyped(
            "world.click_select",
            "world",
            new GDictionary { ["coord"] = coord },
            () =>
            {
                if (IsSubmapActive() && !IsBattleActive() && !IsModalWindowOpenInternal())
                    return ReturnFromActiveSubmapTyped();
                _on_world_map_cell_clicked(coord);
                return BuildCommandOkResult();
            }
        );

    internal RuntimeCommandResult InspectWorldCellTyped(Vector2I coord) =>
        ExecuteLoggedCommandTyped(
            "world.click_inspect",
            "world",
            new GDictionary { ["coord"] = coord },
            () =>
            {
                _on_world_map_cell_right_clicked(coord);
                return BuildCommandOkResult();
            }
        );

    internal RuntimeCommandResult SelectBattleCellTyped(Vector2I coord) =>
        ExecuteLoggedCommandTyped(
            "battle.click_select",
            "battle",
            new GDictionary { ["coord"] = coord },
            () => _battle_session_facade.CommandBattleMoveToTyped(coord)
        );

    internal RuntimeCommandResult InspectBattleCellTyped(Vector2I coord) =>
        ExecuteLoggedCommandTyped(
            "battle.click_inspect",
            "battle",
            new GDictionary { ["coord"] = coord },
            () =>
            {
                _on_battle_cell_right_clicked(coord);
                return BuildCommandOkResult();
            }
        );

    internal RuntimeCommandResult CommandShopSell(
        StringName itemId,
        int quantity,
        StringName instanceId
    ) => CommandShopSellTyped(itemId, quantity, instanceId);

    internal RuntimeCommandResult CommandWarehouseDiscardOne(
        StringName itemId,
        StringName instanceId
    ) => CommandWarehouseDiscardOneTyped(itemId, instanceId);

    internal RuntimeCommandResult CommandWarehouseDiscardAll(
        StringName itemId,
        StringName instanceId
    ) => CommandWarehouseDiscardAllTyped(itemId, instanceId);

    private GDictionary _command_ok() => _command_ok("", BattleRefreshMode.None);

    private GDictionary _command_ok(string message) => _command_ok(message, BattleRefreshMode.None);

    private GDictionary _command_ok(string message, BattleRefreshMode battleRefreshMode) =>
        FinalizeCommandResult(BuildCommandOkResult(message, battleRefreshMode));

    private GDictionary _command_error(string message) =>
        FinalizeCommandResult(BuildCommandErrorResult(message));

    private RuntimeCommandResult BuildCommandOkResult(
        string message = "",
        BattleRefreshMode battleRefreshMode = BattleRefreshMode.None
    )
    {
        string resolvedMessage = string.IsNullOrEmpty(message) ? _current_status_message : message;
        return RuntimeCommandResult.Success(resolvedMessage, RuntimeCommandCode.Ok, battleRefreshMode);
    }

    private RuntimeCommandResult BuildCommandErrorResult(
        string message,
        RuntimeCommandCode code = RuntimeCommandCode.Failed
    )
    {
        string resolvedMessage = message ?? "";
        if (!string.IsNullOrEmpty(resolvedMessage))
            UpdateStatusInternal(resolvedMessage);
        return RuntimeCommandResult.Failure(resolvedMessage, code);
    }

    private GDictionary FinalizeCommandResult(RuntimeCommandResult commandResult)
    {
        var result = RuntimeCommandResultProjection.Project(commandResult);
        _log_active_command_scope_result(result);
        return result;
    }

    private RuntimeCommandResult ExecuteLoggedCommandTyped(
        string event_id,
        string domain,
        GDictionary context,
        Func<RuntimeCommandResult> action
    )
    {
        _command_logger.BeginLoggedCommand(event_id, domain, context ?? new GDictionary());
        RuntimeCommandResult result = action?.Invoke() ?? RuntimeCommandResult.Failure("");
        _log_active_command_scope_result(RuntimeCommandResultProjection.Project(result));
        return result;
    }

    private GDictionary _execute_logged_command(
        string event_id,
        string domain,
        GDictionary context,
        Func<GDictionary> action
    )
    {
        _command_logger.BeginLoggedCommand(event_id, domain, context ?? new GDictionary());
        var result = action?.Invoke() ?? new GDictionary();
        return _command_logger.FinishLoggedCommand(result);
    }

    private void _log_active_command_scope_result(GDictionary result) =>
        _command_logger.LogActiveCommandScopeResult(result);

    private GDictionary _build_runtime_log_state() => _command_logger.BuildRuntimeLogState();

    internal void _log_runtime_event(string level, string domain, string event_id, string message) =>
        _command_logger.LogRuntimeEvent(level, domain, event_id, message, "");

    internal void _log_runtime_event(
        string level,
        string domain,
        string event_id,
        string message,
        string context
    ) =>
        _command_logger.LogRuntimeEvent(
            level,
            domain,
            event_id,
            message,
            context ?? ""
        );

    private void _log_battle_batch_entries(BattleEventBatch batch) =>
        _command_logger.LogBattleBatchEntries(batch);

    private GDictionary _build_battle_log_state() => _command_logger.BuildBattleLogState();

    private GDictionary _build_battle_batch_log_context(BattleEventBatch batch) =>
        _command_logger.BuildBattleBatchLogContext(batch);

    private string ResolveCommandSettlementId() =>
        _settlement_command_handler.ResolveCommandSettlementId();
}
