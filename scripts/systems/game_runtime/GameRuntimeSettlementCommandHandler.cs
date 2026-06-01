using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GArray = Godot.Collections.Array;
using GDictArray = Godot.Collections.Array<Godot.Collections.Dictionary>;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

// 翻译自 game_runtime_settlement_command_handler.gd（2026-05-26，据点命令处理 C# 迁移）。
// runtime 强耦合：绝大多数状态/动作委托 GameRuntimeFacade；子服务为 C# 实例直接调用。
[GlobalClass]
public partial class GameRuntimeSettlementCommandHandler : RefCounted
{
    private const int REST_FULL_COST = 50;
    private const int INTEL_NETWORK_COST = 50;
    private const int STAGECOACH_COST_PER_STEP = 10;
    private const int VILLAGE_RUMOR_RANGE = 5;
    private const int INTEL_NETWORK_RANGE = 8;

    private static readonly HashSet<string> SHOP_INTERACTION_IDS = new()
    {
        "service_basic_supply",
        "service_local_trade",
        "service_city_market",
        "service_military_supply",
        "service_grand_auction",
    };

    private static readonly HashSet<string> STAGECOACH_INTERACTION_IDS = new()
    {
        "service_stagecoach",
        "service_world_gate_travel",
    };

    private static readonly HashSet<string> UNIMPLEMENTED_INTERACTION_IDS = new()
    {
        "service_join_guild",
        "service_identify_relic",
        "service_recruit_specialist",
        "service_issue_regional_edict",
        "service_unlock_archive",
        "service_diplomatic_clearance",
        "service_amnesty_review",
        "service_elite_recruitment",
        "service_respecialize_build",
        "service_manage_reputation",
        "service_open_trade_route",
        "service_legend_contracts",
        "service_hire_expert",
    };

    private static readonly HashSet<string> AUTHORITATIVE_SETTLEMENT_ACTION_PAYLOAD_KEYS = new()
    {
        "action_id",
        "facility_id",
        "facility_template_id",
        "facility_name",
        "npc_id",
        "npc_template_id",
        "npc_name",
        "service_type",
        "interaction_script_id",
    };

    private WeakReference<GameRuntimeFacade> _runtimeRef;

    private GameRuntimeFacade Runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<GameRuntimeFacade>(value) : null;
    }

    private SettlementShopService _shop_service = new();
    private SettlementForgeService _forge_service = new();
    private SettlementResearchService _research_service = new();

    private sealed class SettlementActionValidationResult
    {
        public bool Ok { get; }
        public string Message { get; }
        public GDictionary ServiceEntry { get; }

        private SettlementActionValidationResult(
            bool ok,
            string message,
            GDictionary serviceEntry = null
        )
        {
            Ok = ok;
            Message = message ?? "";
            ServiceEntry = serviceEntry?.Duplicate(true) ?? new GDictionary();
        }

        public static SettlementActionValidationResult Success(GDictionary serviceEntry = null) =>
            new(true, "", serviceEntry);

        public static SettlementActionValidationResult Failure(string message) =>
            new(false, message);

        public GDictionary ToDictionary()
        {
            var result = new GDictionary
            {
                ["ok"] = Ok,
            };
            if (!Ok || !string.IsNullOrEmpty(Message))
            {
                result["message"] = Message;
            }
            if (Ok && ServiceEntry.Count != 0)
            {
                result["service_entry"] = ServiceEntry.Duplicate(true);
            }
            return result;
        }
    }

    private readonly record struct RuntimeCommandResultSnapshot(
        GDictionary Payload,
        bool Ok,
        string Message
    )
    {
        public static RuntimeCommandResultSnapshot FromDictionary(GDictionary payload)
        {
            GDictionary normalized = payload ?? new GDictionary();
            return new RuntimeCommandResultSnapshot(
                normalized,
                ReadBool(normalized, "ok", false),
                ReadString(normalized, "message", "")
            );
        }
    }

    private sealed class SettlementServiceEntryResolution
    {
        public GDictionary ServiceEntry { get; }
        public bool IsEnabled { get; }
        public string DisabledReason { get; }
        public bool Found => ServiceEntry.Count != 0;

        private SettlementServiceEntryResolution(
            GDictionary serviceEntry,
            bool isEnabled,
            string disabledReason
        )
        {
            ServiceEntry = serviceEntry?.Duplicate(true) ?? new GDictionary();
            IsEnabled = isEnabled;
            DisabledReason = disabledReason ?? "";
        }

        public static SettlementServiceEntryResolution Missing() => new(null, false, "");

        public static SettlementServiceEntryResolution FromServiceData(
            GDictionary serviceEntry,
            SettlementServiceMetadata metadata
        ) =>
            new(
                serviceEntry,
                metadata?.IsEnabled ?? false,
                metadata?.DisabledReason ?? ""
            );
    }

    private sealed class StagecoachDestinationData
    {
        public string SettlementId { get; }
        public string DisplayName { get; }
        public string TierName { get; }
        public int TravelCost { get; }
        public bool CanTravel { get; }
        public string DisabledReason { get; }
        public Vector2I Coord { get; }
        public string InteractionScriptId { get; }

        public StagecoachDestinationData(
            string settlementId,
            string displayName,
            string tierName,
            int travelCost,
            bool canTravel,
            string disabledReason,
            Vector2I coord,
            string interactionScriptId
        )
        {
            SettlementId = settlementId ?? "";
            DisplayName = displayName ?? "";
            TierName = tierName ?? "";
            TravelCost = travelCost;
            CanTravel = canTravel;
            DisabledReason = disabledReason ?? "";
            Coord = coord;
            InteractionScriptId = interactionScriptId ?? "";
        }

        public GDictionary ToDictionary() =>
            new()
            {
                ["settlement_id"] = SettlementId,
                ["entry_id"] = $"travel:{SettlementId}",
                ["display_name"] = DisplayName,
                ["tier_name"] = TierName,
                ["travel_cost"] = TravelCost,
                ["can_travel"] = CanTravel,
                ["state_label"] = CanTravel ? "状态：可出发" : "状态：不可出发",
                ["cost_label"] = $"路费 {TravelCost} 金",
                ["summary_text"] = TierName,
                ["details_text"] = $"{TierName} {DisabledReason}",
                ["is_enabled"] = CanTravel,
                ["target_settlement_id"] = SettlementId,
                ["disabled_reason"] = DisabledReason,
                ["coord"] = new GDictionary { ["x"] = Coord.X, ["y"] = Coord.Y },
                ["interaction_script_id"] = InteractionScriptId,
            };
    }

    private readonly struct SettlementPersistResult
    {
        public readonly bool Ok;
        public readonly int PartyError;
        public readonly int WorldError;
        public readonly int PlayerError;

        public SettlementPersistResult(int partyError, int worldError, int playerError)
        {
            PartyError = partyError;
            WorldError = worldError;
            PlayerError = playerError;
            Ok =
                PartyError == (int)Error.Ok
                && WorldError == (int)Error.Ok
                && PlayerError == (int)Error.Ok;
        }

        public GDictionary ToDictionary() =>
            new()
            {
                ["ok"] = Ok,
                ["party_error"] = PartyError,
                ["world_error"] = WorldError,
                ["player_error"] = PlayerError,
            };
    }

    public void setup(GameRuntimeFacade runtime)
    {
        Runtime = runtime;
    }

    public void dispose()
    {
        Runtime = null;
        _shop_service = new SettlementShopService();
        _forge_service = new SettlementForgeService();
        _research_service = new SettlementResearchService();
    }

    public GDictionary get_settlement_window_data(string settlement_id = "")
    {
        if (!_has_runtime())
        {
            return new GDictionary();
        }
        string targetId = !string.IsNullOrEmpty(settlement_id)
            ? settlement_id
            : resolve_command_settlement_id();
        GDictionary settlement = _get_settlement_record(targetId);
        if (settlement.Count == 0)
        {
            return new GDictionary();
        }
        GDictionary settlementState = _get_or_create_settlement_state(targetId);
        return new GDictionary
        {
            ["settlement_id"] = ReadString(settlement, "settlement_id"),
            ["display_name"] = ReadString(settlement, "display_name"),
            ["tier_name"] = ReadString(settlement, "tier_name"),
            ["footprint_size"] = ReadVariant(settlement, "footprint_size"),
            ["faction_id"] = ReadString(settlement, "faction_id"),
            ["facilities"] = ReadVariant(settlement, "facilities"),
            ["available_services"] = _build_service_entries(settlement, settlementState),
            ["service_npcs"] = ReadVariant(settlement, "service_npcs"),
            ["member_options"] = _build_member_options(),
            ["default_member_id"] = resolve_default_settlement_member_id().ToString(),
            ["state_summary_text"] = _build_settlement_state_summary(settlementState),
            ["feedback_text"] = _build_settlement_window_feedback_text(),
        };
    }

    public GDictionary get_shop_window_data()
    {
        GDictionary context = _get_active_shop_context();
        if (context.Count == 0)
        {
            return new GDictionary();
        }
        var entries = new GDictArray();
        foreach (GDictionary entryData in Dictionaries(ReadArray(context, "buy_entries")))
        {
            GDictionary entry = (GDictionary)entryData.Duplicate(true);
            if (!entry.ContainsKey("is_enabled"))
            {
                entry["is_enabled"] = false;
            }
            entries.Add(entry);
        }
        foreach (GDictionary entryData in Dictionaries(ReadArray(context, "sell_entries")))
        {
            GDictionary entry = (GDictionary)entryData.Duplicate(true);
            if (!entry.ContainsKey("is_enabled"))
            {
                entry["is_enabled"] = false;
            }
            entries.Add(entry);
        }
        context["entries"] = entries;
        context["summary_text"] = $"持有金币：{ReadInt(context, "gold")}";
        context["state_summary_text"] = ReadString(context, "feedback_text");
        context["action_id"] = "shop:trade";
        context["panel_kind"] = "shop";
        context["show_member_selector"] = true;
        context["party_state"] = _get_party_state();
        context["member_options"] = _build_member_options();
        context["default_member_id"] = resolve_default_settlement_member_id().ToString();
        return context;
    }

    public GDictionary get_contract_board_window_data()
    {
        GDictionary context = _get_active_contract_board_context();
        if (context.Count == 0)
        {
            return new GDictionary();
        }
        return (GDictionary)context.Duplicate(true);
    }

    public GDictionary get_forge_window_data()
    {
        GDictionary context = _get_active_forge_context();
        if (context.Count == 0)
        {
            return new GDictionary();
        }
        return (GDictionary)context.Duplicate(true);
    }

    public GDictionary get_stagecoach_window_data()
    {
        GDictionary context = _get_active_stagecoach_context();
        if (context.Count == 0)
        {
            return new GDictionary();
        }
        var entries = new GDictArray();
        foreach (GDictionary entryData in Dictionaries(ReadArray(context, "destinations")))
        {
            GDictionary entry = (GDictionary)entryData.Duplicate(true);
            if (!entry.ContainsKey("is_enabled"))
            {
                entry["is_enabled"] = false;
            }
            entries.Add(entry);
        }
        context["entries"] = entries;
        context["summary_text"] = $"持有金币：{ReadInt(context, "gold")}";
        context["state_summary_text"] = ReadString(context, "feedback_text");
        context["action_id"] = "stagecoach:travel";
        context["panel_kind"] = "stagecoach";
        context["meta"] =
            $"驿站：{ReadString(context, "origin_name")}  |  金币：{ReadInt(context, "gold")}";
        context["confirm_label"] = "确认出发";
        context["cancel_label"] = "返回据点";
        context["show_member_selector"] = true;
        context["entry_title"] = "可选路线";
        context["summary_title"] = "行程概况";
        context["state_title"] = "行程状态";
        context["cost_title"] = "行程费用";
        context["details_title"] = "行程说明";
        context["member_title"] = "出发成员";
        context["empty_state_label"] = "状态：暂无路线";
        context["empty_cost_label"] = "费用：暂无路线";
        context["empty_details_text"] = "当前没有可用路线。";
        context["party_state"] = _get_party_state();
        context["member_options"] = _build_member_options();
        context["default_member_id"] = resolve_default_settlement_member_id().ToString();
        return context;
    }

    public GDictionary command_execute_settlement_action(
        string action_id,
        GDictionary payload = null
    )
    {
        GDictionary payloadData = payload ?? new GDictionary();
        if (!_has_runtime())
        {
            return _command_error("运行时尚未初始化。");
        }
        if (string.IsNullOrEmpty(action_id))
        {
            return _command_error("据点动作 ID 不能为空。");
        }
        if (_is_battle_active())
        {
            return _command_error("当前处于战斗中，不能执行据点动作。");
        }
        string settlementId = resolve_command_settlement_id();
        if (string.IsNullOrEmpty(settlementId))
        {
            return _command_error("当前没有可执行动作的据点。");
        }
        SettlementActionValidationResult validation = ValidateSettlementActionRequestTyped(
            settlementId,
            action_id,
            payloadData
        );
        if (!validation.Ok)
        {
            return _command_error(
                string.IsNullOrEmpty(validation.Message)
                    ? "当前据点未开放该服务。"
                    : validation.Message
            );
        }
        GDictionary serviceEntry = validation.ServiceEntry;
        if (serviceEntry.Count == 0)
        {
            return _command_error("当前据点未开放该服务。");
        }
        GDictionary mergedPayload = _build_settlement_action_payload_from_service_entry(
            action_id,
            serviceEntry,
            payloadData
        );
        if (mergedPayload.Count == 0)
        {
            return _command_error(
                _build_unknown_settlement_action_message(settlementId, action_id)
            );
        }
        return _dispatch_settlement_action(settlementId, action_id, mergedPayload);
    }

    public GDictionary command_shop_buy(StringName item_id, int quantity)
    {
        if (_get_active_modal_id() != "shop")
        {
            return _command_error("当前没有打开据点商店。");
        }
        GDictionary context = _get_active_shop_context();
        if (context.Count == 0)
        {
            return _command_error("当前商店上下文缺失。");
        }
        string settlementId = ReadString(context, "settlement_id");
        GDictionary settlementState = _get_or_create_settlement_state(settlementId);
        SettlementShopTradeResult result = _shop_service.BuyTyped(
            ReadString(context, "interaction_script_id"),
            _get_settlement_record(settlementId),
            settlementState,
            _get_item_defs(),
            _get_party_warehouse_service(),
            _get_party_state(),
            item_id,
            quantity
        );
        if (!result.Success)
        {
            _refresh_active_shop_context();
            return _command_error(
                string.IsNullOrEmpty(result.Message) ? "购买失败。" : result.Message
            );
        }
        _set_active_settlement_state(settlementId, settlementState);
        SettlementPersistResult persistResult = PersistChangesTyped(true, true, false);
        string message = string.IsNullOrEmpty(result.Message) ? "购买成功。" : result.Message;
        if (!persistResult.Ok)
        {
            message = $"{message} 但队伍或据点状态持久化失败。";
        }
        _refresh_active_shop_context();
        _update_status(message);
        return _command_ok(message);
    }

    public GDictionary command_shop_sell(
        StringName item_id,
        int quantity,
        StringName instance_id = null
    )
    {
        instance_id ??= new StringName("");
        if (_get_active_modal_id() != "shop")
        {
            return _command_error("当前没有打开据点商店。");
        }
        GDictionary context = _get_active_shop_context();
        if (context.Count == 0)
        {
            return _command_error("当前商店上下文缺失。");
        }
        string settlementId = ReadString(context, "settlement_id");
        GDictionary settlementState = _get_or_create_settlement_state(settlementId);
        SettlementShopTradeResult result = _shop_service.SellTyped(
            ReadString(context, "interaction_script_id"),
            _get_settlement_record(settlementId),
            settlementState,
            _get_item_defs(),
            _get_party_warehouse_service(),
            _get_party_state(),
            item_id,
            quantity,
            instance_id
        );
        if (!result.Success)
        {
            _refresh_active_shop_context();
            return _command_error(
                string.IsNullOrEmpty(result.Message) ? "出售失败。" : result.Message
            );
        }
        _set_active_settlement_state(settlementId, settlementState);
        SettlementPersistResult persistResult = PersistChangesTyped(true, true, false);
        string message = string.IsNullOrEmpty(result.Message) ? "出售成功。" : result.Message;
        if (!persistResult.Ok)
        {
            message = $"{message} 但队伍或据点状态持久化失败。";
        }
        _refresh_active_shop_context();
        _update_status(message);
        return _command_ok(message);
    }

    public GDictionary command_stagecoach_travel(string settlement_id)
    {
        if (_get_active_modal_id() != "stagecoach")
        {
            return _command_error("当前没有打开驿站路线窗口。");
        }
        GDictionary context = _get_active_stagecoach_context();
        if (context.Count == 0)
        {
            return _command_error("当前没有可用的驿站路线。");
        }
        StagecoachDestinationData destination = ResolveStagecoachDestinationTyped(
            context,
            settlement_id
        );
        if (destination == null)
        {
            return _command_error("当前驿站路线中不存在该目的地。");
        }
        if (!destination.CanTravel)
        {
            return _command_error(
                !string.IsNullOrEmpty(destination.DisabledReason)
                    ? destination.DisabledReason
                    : "当前无法前往该据点。"
            );
        }
        PartyState partyState = _get_party_state();
        if (partyState == null)
        {
            return _command_error("当前不存在队伍数据。");
        }
        int travelCost = destination.TravelCost;
        if (!partyState.spend_gold(travelCost))
        {
            return _command_error("金币不足，无法启程。");
        }
        string destinationId = destination.SettlementId;
        GDictionary destinationRecord = _get_settlement_record(destinationId);
        if (destinationRecord.Count == 0)
        {
            return _command_error("未找到目标据点。");
        }
        Vector2I destinationCoord = destination.Coord;
        _clear_settlement_entry_context(false);
        _set_player_coord(destinationCoord);
        _set_selected_coord(destinationCoord);
        _mark_settlement_visited(destinationId);
        _clear_active_stagecoach_context();
        _set_active_modal_id("settlement");
        _set_active_settlement_id(destinationId);
        _set_settlement_feedback_text(
            $"驿队将你送到了 {ReadString(destinationRecord, "display_name", destinationId)}。"
        );
        _refresh_world_visibility();
        SettlementPersistResult persistResult = PersistChangesTyped(true, true, true);
        string message =
            $"已从 {ReadString(context, "origin_name", "当前据点")} 抵达 {ReadString(destinationRecord, "display_name", destinationId)}，花费 {travelCost} 金。";
        if (!persistResult.Ok)
        {
            message = $"{message} 但队伍或世界状态持久化失败。";
        }
        _update_status(message);
        return _command_ok(message);
    }

    public void on_settlement_action_requested(
        string settlement_id,
        string action_id,
        GDictionary payload
    )
    {
        SettlementActionValidationResult validation = ValidateSettlementActionRequestTyped(
            settlement_id,
            action_id,
            payload
        );
        if (!validation.Ok)
        {
            string message = string.IsNullOrEmpty(validation.Message)
                ? "当前据点未开放该服务。"
                : validation.Message;
            _set_settlement_feedback_text(message);
            _update_status(message);
            return;
        }
        GDictionary serviceEntry = validation.ServiceEntry;
        if (serviceEntry.Count == 0)
        {
            string serviceErrorMessage = "当前据点未开放该服务。";
            _set_settlement_feedback_text(serviceErrorMessage);
            _update_status(serviceErrorMessage);
            return;
        }
        GDictionary mergedPayload = _build_settlement_action_payload_from_service_entry(
            action_id,
            serviceEntry,
            payload
        );
        if (mergedPayload.Count == 0)
        {
            string unknownMessage = _build_unknown_settlement_action_message(
                settlement_id,
                action_id
            );
            _set_settlement_feedback_text(unknownMessage);
            _update_status(unknownMessage);
            return;
        }
        _dispatch_settlement_action(settlement_id, action_id, mergedPayload);
    }

    public GDictionary _dispatch_settlement_action(
        string settlement_id,
        string action_id,
        GDictionary payload
    )
    {
        if (!_has_runtime())
        {
            return _command_error("运行时尚未初始化。");
        }
        string interactionScriptId = ReadString(payload, "interaction_script_id");
        if (interactionScriptId == "party_warehouse")
        {
            string warehouseMessage = "已从据点服务打开共享仓库。";
            var warehouseResult = new SettlementServiceResult
            {
                Success = true,
                Message = warehouseMessage,
                PersistPartyState = true,
            };
            warehouseResult.SetQuestProgressEventPayloads(
                ToUntypedDictArray(_extract_quest_progress_events(payload, action_id, settlement_id))
            );
            _finalize_successful_action(action_id, payload, warehouseResult);
            _clear_settlement_entry_context();
            _set_active_settlement_id(settlement_id);
            _set_active_modal_id("");
            _open_party_warehouse_window(
                $"据点服务：{ReadString(payload, "facility_name", "设施")}·{ReadString(payload, "npc_name", "值守人员")}"
            );
            _update_status(warehouseMessage);
            return _command_ok(warehouseMessage);
        }
        if (QuestProviderContentRules.is_supported_provider_id(interactionScriptId))
        {
            if (_is_contract_board_modal_submission(payload))
            {
                return _submit_contract_board_quest_action(settlement_id, action_id, payload);
            }
            _open_contract_board_modal(settlement_id, payload);
            return _command_ok(
                $"已打开 {ReadString(payload, "facility_name", "据点任务板")} 的任务板。"
            );
        }
        if (SHOP_INTERACTION_IDS.Contains(interactionScriptId))
        {
            _open_shop_modal(settlement_id, payload);
            return _command_ok(
                $"已打开 {ReadString(payload, "facility_name", "据点商店")} 的商店。"
            );
        }
        if (_is_forge_interaction(interactionScriptId) && !_is_forge_modal_submission(payload))
        {
            _open_forge_modal(settlement_id, payload);
            return _command_ok(
                $"已打开 {ReadString(payload, "facility_name", "锻造设施")} 的锻造界面。"
            );
        }
        if (STAGECOACH_INTERACTION_IDS.Contains(interactionScriptId))
        {
            _open_stagecoach_modal(settlement_id, payload);
            return _command_ok(
                $"已打开 {ReadString(payload, "facility_name", "驿站")} 的驿站路线。"
            );
        }
        SettlementServiceResult serviceResult = ExecuteSettlementActionTyped(
            settlement_id,
            action_id,
            payload
        );
        string message = serviceResult?.Message ?? "交互已完成。";
        _set_settlement_feedback_text(message);
        bool actionSucceeded = serviceResult?.Success ?? false;
        if (_is_forge_interaction(interactionScriptId))
        {
            _refresh_active_forge_context(message);
            if (actionSucceeded)
            {
                SettlementPersistResult forgePersistResult = FinalizeSuccessfulActionTyped(
                    action_id,
                    payload,
                    serviceResult
                );
                if (forgePersistResult.Ok)
                {
                    _update_status(message);
                    return _command_ok(message);
                }
                string forgePersistMessage = $"{message} 但队伍或据点状态持久化失败。";
                _update_status(forgePersistMessage);
                return _command_error(forgePersistMessage);
            }
            _update_status(message);
            return _command_error(message);
        }
        if (actionSucceeded)
        {
            SettlementPersistResult persistResult = FinalizeSuccessfulActionTyped(
                action_id,
                payload,
                serviceResult
            );
            if (persistResult.Ok)
            {
                _update_status(message);
                return _command_ok(message);
            }
            string persistMessage = $"{message} 但队伍或据点状态持久化失败。";
            _update_status(persistMessage);
            return _command_error(persistMessage);
        }
        _update_status(message);
        return _command_error(message);
    }

    public void on_settlement_window_closed()
    {
        if (!_has_runtime())
        {
            return;
        }
        _clear_settlement_entry_context();
        _set_active_settlement_id("");
        _set_settlement_feedback_text("");
        _clear_active_contract_board_context();
        _clear_active_shop_context();
        _clear_active_forge_context();
        _clear_active_stagecoach_context();
        _set_active_modal_id("");
        _update_status("已关闭据点窗口，返回世界地图。");
        _present_pending_reward_if_ready();
    }

    public void on_shop_window_closed()
    {
        _clear_active_shop_context();
        _set_active_modal_id("settlement");
        _update_status("已关闭商店，返回据点服务。");
    }

    public void on_contract_board_window_closed()
    {
        _clear_active_contract_board_context();
        _set_active_modal_id("settlement");
        _update_status("已关闭任务板，返回据点服务。");
    }

    public void on_forge_window_closed()
    {
        GDictionary context = _get_active_forge_context();
        string forgeLabel = _resolve_forge_service_label(context);
        _clear_active_forge_context();
        _set_active_modal_id("settlement");
        _update_status($"已关闭{forgeLabel}，返回据点服务。");
    }

    public void on_stagecoach_window_closed()
    {
        _clear_active_stagecoach_context();
        _set_active_modal_id("settlement");
        _update_status("已关闭驿站路线，返回据点服务。");
    }

    public string resolve_command_settlement_id()
    {
        if (!_has_runtime())
        {
            return "";
        }
        string activeSettlementId = _get_active_settlement_id();
        if (!string.IsNullOrEmpty(activeSettlementId))
        {
            return activeSettlementId;
        }
        GDictionary settlement = _get_selected_settlement();
        return ReadString(settlement, "settlement_id");
    }

    public GDictionary build_settlement_action_payload(
        string settlement_id,
        string action_id,
        GDictionary overrides
    )
    {
        GDictionary serviceEntry = _resolve_settlement_service_entry(settlement_id, action_id);
        if (serviceEntry.Count == 0)
        {
            return new GDictionary();
        }
        return _build_settlement_action_payload_from_service_entry(
            action_id,
            serviceEntry,
            overrides
        );
    }

    public GDictionary _build_settlement_action_payload_from_service_entry(
        string action_id,
        GDictionary service_data,
        GDictionary overrides
    )
    {
        if (service_data.Count == 0)
        {
            return new GDictionary();
        }
        var payload = new GDictionary
        {
            ["action_id"] = action_id,
            ["facility_id"] = ReadString(service_data, "facility_id"),
            ["facility_template_id"] = ReadString(service_data, "facility_template_id"),
            ["facility_name"] = ReadString(service_data, "facility_name"),
            ["npc_id"] = ReadString(service_data, "npc_id"),
            ["npc_template_id"] = ReadString(service_data, "npc_template_id"),
            ["npc_name"] = ReadString(service_data, "npc_name"),
            ["service_type"] = ReadString(service_data, "service_type"),
            ["interaction_script_id"] = ReadString(service_data, "interaction_script_id"),
        };
        foreach (var key in overrides.Keys)
        {
            if (AUTHORITATIVE_SETTLEMENT_ACTION_PAYLOAD_KEYS.Contains(key.ToString()))
            {
                continue;
            }
            payload[key] = overrides[key];
        }
        if (string.IsNullOrEmpty(ReadString(payload, "member_id")))
        {
            StringName memberId = resolve_default_settlement_member_id();
            if (memberId != "")
            {
                payload["member_id"] = memberId.ToString();
            }
        }
        return payload;
    }

    public GDictionary _validate_settlement_action_request(
        string settlement_id,
        string action_id,
        GDictionary payload
    )
    {
        return ValidateSettlementActionRequestTyped(settlement_id, action_id, payload).ToDictionary();
    }

    private SettlementActionValidationResult ValidateSettlementActionRequestTyped(
        string settlement_id,
        string action_id,
        GDictionary payload
    )
    {
        SettlementActionValidationResult modalValidation = ValidateSettlementActionModalContextTyped(
            settlement_id,
            action_id,
            payload
        );
        if (!modalValidation.Ok)
        {
            return modalValidation;
        }
        SettlementActionValidationResult visibilityValidation = ValidateSettlementVisibilityContextTyped(
            settlement_id
        );
        if (!visibilityValidation.Ok)
        {
            return visibilityValidation;
        }
        SettlementServiceEntryResolution serviceResolution = ResolveSettlementServiceEntryTyped(
            settlement_id,
            action_id
        );
        GDictionary serviceEntry = serviceResolution.ServiceEntry;
        if (serviceEntry.Count == 0)
        {
            return SettlementActionValidationResult.Failure(
                _build_unknown_settlement_action_message(settlement_id, action_id)
            );
        }
        if (
            _settlement_action_requires_enabled_service(payload)
            && !serviceResolution.IsEnabled
        )
        {
            return SettlementActionValidationResult.Failure(
                _build_disabled_settlement_action_message(serviceEntry)
            );
        }
        return SettlementActionValidationResult.Success(serviceEntry);
    }

    public GDictionary _validate_settlement_action_modal_context(
        string settlement_id,
        string action_id,
        GDictionary payload
    ) =>
        ValidateSettlementActionModalContextTyped(settlement_id, action_id, payload).ToDictionary();

    private SettlementActionValidationResult ValidateSettlementActionModalContextTyped(
        string settlement_id,
        string action_id,
        GDictionary payload
    )
    {
        if (_is_contract_board_modal_submission(payload))
        {
            if (_get_active_modal_id() != "contract_board")
            {
                return SettlementActionValidationResult.Failure("当前没有打开对应的任务板。");
            }
            GDictionary contractBoardContext = _get_active_contract_board_context();
            if (ReadString(contractBoardContext, "settlement_id").Trim() != settlement_id)
            {
                return SettlementActionValidationResult.Failure("当前任务板与请求的据点不一致。");
            }
            if (ReadString(contractBoardContext, "action_id").Trim() != action_id)
            {
                return SettlementActionValidationResult.Failure("当前任务板与请求的服务入口不一致。");
            }
            return SettlementActionValidationResult.Success();
        }
        if (_is_forge_modal_submission(payload))
        {
            if (_get_active_modal_id() != "forge")
            {
                return SettlementActionValidationResult.Failure("当前没有打开对应的锻造界面。");
            }
            GDictionary forgeContext = _get_active_forge_context();
            if (ReadString(forgeContext, "settlement_id").Trim() != settlement_id)
            {
                return SettlementActionValidationResult.Failure("当前锻造界面与请求的据点不一致。");
            }
            if (ReadString(forgeContext, "action_id").Trim() != action_id)
            {
                return SettlementActionValidationResult.Failure("当前锻造界面与请求的服务入口不一致。");
            }
            return SettlementActionValidationResult.Success();
        }
        if (_get_active_modal_id() != "settlement")
        {
            return SettlementActionValidationResult.Failure("当前没有打开对应的据点窗口。");
        }
        string activeSettlementId = _get_active_settlement_id();
        if (string.IsNullOrEmpty(activeSettlementId) || activeSettlementId != settlement_id)
        {
            return SettlementActionValidationResult.Failure("当前据点窗口与请求的据点不一致。");
        }
        return SettlementActionValidationResult.Success();
    }

    public GDictionary _validate_settlement_visibility_context(string settlement_id) =>
        ValidateSettlementVisibilityContextTyped(settlement_id).ToDictionary();

    private SettlementActionValidationResult ValidateSettlementVisibilityContextTyped(
        string settlement_id
    )
    {
        GDictionary settlement = _get_settlement_record(settlement_id);
        if (settlement.Count == 0)
        {
            return SettlementActionValidationResult.Failure("未找到据点数据。");
        }
        if (!_is_settlement_visible_to_player(settlement))
        {
            return SettlementActionValidationResult.Failure("当前据点不在视野中，不能执行据点服务。");
        }
        return SettlementActionValidationResult.Success();
    }

    public bool _settlement_action_requires_enabled_service(GDictionary payload)
    {
        return !_is_contract_board_modal_submission(payload)
            && !_is_forge_modal_submission(payload);
    }

    public GDictionary _resolve_settlement_service_entry(string settlement_id, string action_id)
    {
        return ResolveSettlementServiceEntryTyped(settlement_id, action_id).ServiceEntry;
    }

    private SettlementServiceEntryResolution ResolveSettlementServiceEntryTyped(
        string settlement_id,
        string action_id
    )
    {
        GDictionary settlement = _get_settlement_record(settlement_id);
        if (settlement.Count == 0)
        {
            return SettlementServiceEntryResolution.Missing();
        }
        GDictionary settlementState = _get_or_create_settlement_state(settlement_id);
        GArray serviceOptions = ReadArray(settlement, "available_services");
        if (serviceOptions.Count == 0)
        {
            return SettlementServiceEntryResolution.Missing();
        }
        foreach (GDictionary sourceServiceData in Dictionaries(serviceOptions))
        {
            GDictionary serviceData = (GDictionary)sourceServiceData.Duplicate(true);
            if (ReadString(serviceData, "action_id").Trim() != action_id)
            {
                continue;
            }
            SettlementServiceMetadata metadata = BuildServiceMetadataTyped(
                settlement,
                serviceData,
                settlementState
            );
            string disabledReason = metadata.DisabledReason.Trim();
            serviceData["cost_label"] = metadata.CostLabel.Trim();
            serviceData["is_enabled"] = metadata.IsEnabled;
            serviceData["disabled_reason"] = disabledReason;
            serviceData["state_label"] = _build_service_state_label(
                metadata.IsEnabled,
                disabledReason
            );
            serviceData["summary_text"] = _build_service_summary_text(serviceData);
            string panelKind = _resolve_service_panel_kind(serviceData);
            if (!string.IsNullOrEmpty(panelKind))
            {
                serviceData["panel_kind"] = panelKind;
            }
            return SettlementServiceEntryResolution.FromServiceData(serviceData, metadata);
        }
        return SettlementServiceEntryResolution.Missing();
    }

    public string _build_unknown_settlement_action_message(string settlement_id, string action_id)
    {
        GDictionary settlement = _get_settlement_record(settlement_id);
        string settlementLabel = ReadString(settlement, "display_name", settlement_id).Trim();
        if (string.IsNullOrEmpty(settlementLabel))
        {
            settlementLabel = "当前据点";
        }
        return $"{settlementLabel} 未开放该服务：{action_id}。";
    }

    public string _build_disabled_settlement_action_message(GDictionary service_entry)
    {
        string serviceLabel = ReadString(service_entry, "service_type").Trim();
        if (string.IsNullOrEmpty(serviceLabel))
        {
            serviceLabel = ReadString(service_entry, "facility_name").Trim();
        }
        if (string.IsNullOrEmpty(serviceLabel))
        {
            serviceLabel = ReadString(service_entry, "action_id", "该服务").Trim();
        }
        string disabledReason = ReadString(service_entry, "disabled_reason").Trim();
        if (string.IsNullOrEmpty(disabledReason))
        {
            return $"{serviceLabel} 当前不可用。";
        }
        return $"{serviceLabel} 当前不可用：{disabledReason}。";
    }

    public StringName resolve_default_settlement_member_id()
    {
        PartyState partyState = _get_party_state();
        if (partyState == null)
        {
            return "";
        }
        StringName leaderMemberId = partyState.leader_member_id;
        if (
            leaderMemberId != ""
            && partyState.get_member_state(leaderMemberId) != null
        )
        {
            return leaderMemberId;
        }
        foreach (StringName memberId in partyState.active_member_ids)
        {
            if (
                memberId != ""
                && partyState.get_member_state(memberId) != null
            )
            {
                return memberId;
            }
        }
        return "";
    }

    public GDictionary execute_settlement_action(
        string settlement_id,
        string action_id,
        GDictionary payload
    ) => ExecuteSettlementActionTyped(settlement_id, action_id, payload).ToDictionary();

    public SettlementServiceResult ExecuteSettlementActionTyped(
        string settlement_id,
        string action_id,
        GDictionary payload
    )
    {
        GDictionary settlement = _get_settlement_record(settlement_id);
        if (settlement.Count == 0)
        {
            return BuildSettlementServiceResultTyped(false, "未找到据点数据。");
        }
        string interactionScriptId = ReadString(payload, "interaction_script_id");
        if (interactionScriptId == "service_rest_basic")
        {
            return ExecuteRestBasicTyped(settlement, action_id, payload);
        }
        if (interactionScriptId == "service_rest_full")
        {
            return ExecuteRestFullTyped(settlement, action_id, payload);
        }
        if (interactionScriptId == "service_village_rumor")
        {
            return ExecuteFogRevealTyped(
                settlement,
                action_id,
                payload,
                VILLAGE_RUMOR_RANGE,
                0,
                "乡野传闻让周边地貌更加清晰。"
            );
        }
        if (interactionScriptId == "service_intel_network")
        {
            return ExecuteFogRevealTyped(
                settlement,
                action_id,
                payload,
                INTEL_NETWORK_RANGE,
                INTEL_NETWORK_COST,
                "情报网更新了周边的行路信息。"
            );
        }
        if (_is_forge_interaction(interactionScriptId))
        {
            return _forge_service.ExecuteRecipeResultTyped(
                settlement,
                payload,
                _get_item_defs(),
                _get_recipe_defs(),
                _get_party_warehouse_service(),
                _get_party_state(),
                ToUntypedDictArray(
                    _extract_quest_progress_events(payload, action_id, settlement_id)
                )
            );
        }
        if (_is_research_interaction(interactionScriptId))
        {
            return _research_service.ExecuteTyped(
                settlement,
                payload,
                _get_party_state(),
                ToUntypedDictArray(
                    _extract_quest_progress_events(payload, action_id, settlement_id)
                )
            );
        }
        if (UNIMPLEMENTED_INTERACTION_IDS.Contains(interactionScriptId))
        {
            return BuildSettlementServiceResultTyped(
                true,
                "该据点服务入口已接通，但其配套系统尚未开放。",
                new GDictArray(),
                false,
                false,
                false,
                0,
                new GDictionary(),
                _extract_quest_progress_events(payload, action_id, settlement_id)
            );
        }
        GDictArray pendingCharacterRewards = extract_pending_character_rewards(
            action_id,
            payload,
            ReadString(payload, "facility_name"),
            ReadString(payload, "npc_name"),
            ReadString(payload, "service_type", "服务")
        );
        return BuildSettlementServiceResultTyped(
            true,
            $"{ReadString(settlement, "display_name", settlement_id)} 的 {ReadString(payload, "npc_name", "值守人员")} 在 {ReadString(payload, "facility_name", "设施")} 中为你处理了“{ReadString(payload, "service_type", "服务")}”事务。首版窗口流程已接通。",
            pendingCharacterRewards,
            true,
            false,
            false,
            0,
            new GDictionary(),
            _extract_quest_progress_events(payload, action_id, settlement_id)
        );
    }

    public GDictArray extract_pending_character_rewards(
        string action_id,
        GDictionary payload,
        string facility_name,
        string npc_name,
        string service_type
    )
    {
        var rewards = new GDictArray();
        StringName defaultSourceType = resolve_default_reward_source_type(
            action_id,
            service_type,
            payload
        );
        string defaultSourceLabel = resolve_default_reward_source_label(
            facility_name,
            npc_name,
            service_type
        );
        GArray explicitRewards = ReadArray(payload, "pending_character_rewards");
        if (explicitRewards.Count != 0)
        {
            foreach (GDictionary sourceReward in Dictionaries(explicitRewards))
            {
                GDictionary rewardData = BuildPendingCharacterRewardPayload(
                    sourceReward,
                    payload,
                    defaultSourceType,
                    defaultSourceLabel
                );
                if (rewardData.Count != 0)
                {
                    rewards.Add(rewardData);
                }
            }
        }
        if (Runtime != null)
        {
            object lowLuckResultValue = Runtime.resolve_low_luck_settlement_event_rewards(
                new GDictionary
                {
                    ["action_id"] = action_id,
                    ["facility_id"] = ReadString(payload, "facility_id"),
                    ["facility_name"] = facility_name,
                    ["interaction_script_id"] = ReadString(payload, "interaction_script_id"),
                    ["npc_name"] = npc_name,
                    ["payload"] = payload.Duplicate(true),
                    ["service_type"] = service_type,
                }
            );
            if (TryAsDictionary(lowLuckResultValue, out GDictionary lowLuckResult))
            {
                GArray lowLuckRewards = ReadArray(lowLuckResult, "pending_character_rewards");
                if (lowLuckRewards.Count != 0)
                {
                    foreach (GDictionary rewardData in Dictionaries(lowLuckRewards))
                    {
                        GDictionary normalizedRewardData = BuildPendingCharacterRewardPayload(
                            rewardData,
                            payload,
                            defaultSourceType,
                            defaultSourceLabel
                        );
                        if (normalizedRewardData.Count != 0)
                        {
                            rewards.Add(normalizedRewardData);
                        }
                    }
                }
            }
        }
        return rewards;
    }

    private GDictionary BuildPendingCharacterRewardPayload(
        GDictionary source_reward,
        GDictionary payload,
        StringName default_source_type,
        string default_source_label
    )
    {
        if (source_reward == null || source_reward.Count == 0)
        {
            return new GDictionary();
        }
        CharacterManagementModule characterManagement = Runtime?.get_character_management();
        if (characterManagement == null)
        {
            return new GDictionary();
        }

        StringName memberId = ReadStringName(source_reward, "member_id");
        if (memberId == "")
        {
            memberId = ReadStringName(payload, "member_id");
        }
        StringName sourceType = ReadStringName(source_reward, "source_type");
        if (sourceType == "")
        {
            sourceType = default_source_type;
        }
        StringName sourceId = ReadStringName(source_reward, "source_id");
        if (sourceId == "")
        {
            sourceId = sourceType;
        }
        string sourceLabel = ReadString(source_reward, "source_label", default_source_label);
        if (string.IsNullOrEmpty(sourceLabel))
        {
            sourceLabel = default_source_label;
        }
        GArray entries = ReadArray(source_reward, "entries");
        PendingCharacterReward reward = characterManagement.build_pending_character_reward(
            memberId,
            ReadStringName(source_reward, "reward_id"),
            sourceType,
            sourceId,
            sourceLabel,
            entries,
            ReadString(source_reward, "summary_text")
        );
        return reward != null ? reward.to_dict() : new GDictionary();
    }

    public StringName resolve_default_reward_source_type(
        string action_id,
        string service_type,
        GDictionary payload
    )
    {
        string explicitSourceType = ReadString(payload, "reward_source_type").Trim();
        if (!string.IsNullOrEmpty(explicitSourceType))
        {
            return new StringName(explicitSourceType);
        }
        string combinedLabel = $"{action_id} {service_type}";
        if (combinedLabel.Contains("传授") || combinedLabel.Contains("指点"))
        {
            return "npc_teach";
        }
        return "training";
    }

    public string resolve_default_reward_source_label(
        string facility_name,
        string npc_name,
        string service_type
    )
    {
        if (!string.IsNullOrEmpty(service_type))
        {
            return $"{npc_name}·{service_type}";
        }
        if (!string.IsNullOrEmpty(facility_name))
        {
            return facility_name;
        }
        return "据点服务";
    }

    public GDictionary _execute_rest_basic(
        GDictionary settlement,
        string action_id,
        GDictionary payload
    ) => ExecuteRestBasicTyped(settlement, action_id, payload).ToDictionary();

    private SettlementServiceResult ExecuteRestBasicTyped(
        GDictionary settlement,
        string action_id,
        GDictionary payload
    )
    {
        GDictionary memberEffects = _restore_party_resources(0.3f, false);
        var summaryLines = new List<string>();
        foreach (object memberIdValue in memberEffects.Keys)
        {
            string memberId = memberIdValue.ToString();
            GDictionary effect = ReadDictionary(memberEffects, memberId);
            summaryLines.Add($"{_get_member_display_name(new StringName(memberId))} +{ReadInt(effect, "hp_restored")} HP");
        }
        return BuildSettlementServiceResultTyped(
            true,
            $"{ReadString(settlement, "display_name", "据点")} 的篝火让全队稍作歇脚。{(summaryLines.Count != 0 ? string.Join("；", summaryLines) : "体力恢复有限。")}",
            extract_pending_character_rewards(
                action_id,
                payload,
                ReadString(payload, "facility_name"),
                ReadString(payload, "npc_name"),
                ReadString(payload, "service_type", "歇脚")
            ),
            true,
            false,
            false,
            0,
            new GDictionary(),
            _extract_quest_progress_events(
                payload,
                action_id,
                ReadString(settlement, "settlement_id")
            ),
            new GDictionary
            {
                ["hp_restored"] = _build_member_effect_value_map(memberEffects, "hp_restored"),
            }
        );
    }

    public GDictionary _execute_rest_full(
        GDictionary settlement,
        string action_id,
        GDictionary payload
    ) => ExecuteRestFullTyped(settlement, action_id, payload).ToDictionary();

    private SettlementServiceResult ExecuteRestFullTyped(
        GDictionary settlement,
        string action_id,
        GDictionary payload
    )
    {
        PartyState partyState = _get_party_state();
        if (partyState == null)
        {
            return BuildSettlementServiceResultTyped(false, "当前不存在队伍数据。");
        }
        if (!partyState.spend_gold(REST_FULL_COST))
        {
            return BuildSettlementServiceResultTyped(false, "金币不足，无法在旅店整备。");
        }
        GDictionary memberEffects = _restore_party_resources(1.0f, true);
        _advance_world_time_by_steps(1);
        var summaryLines = new List<string>();
        foreach (object memberIdValue in memberEffects.Keys)
        {
            string memberId = memberIdValue.ToString();
            GDictionary effect = ReadDictionary(memberEffects, memberId);
            summaryLines.Add($"{_get_member_display_name(new StringName(memberId))} HP+{ReadInt(effect, "hp_restored")} MP+{ReadInt(effect, "mp_restored")}");
        }
        return BuildSettlementServiceResultTyped(
            true,
            $"{ReadString(settlement, "display_name", "据点")} 的旅店让全队完成整备，花费 {REST_FULL_COST} 金。{(summaryLines.Count != 0 ? string.Join("；", summaryLines) : "状态恢复如初。")}",
            extract_pending_character_rewards(
                action_id,
                payload,
                ReadString(payload, "facility_name"),
                ReadString(payload, "npc_name"),
                ReadString(payload, "service_type", "整备")
            ),
            true,
            true,
            false,
            -REST_FULL_COST,
            new GDictionary(),
            _extract_quest_progress_events(
                payload,
                action_id,
                ReadString(settlement, "settlement_id")
            ),
            new GDictionary
            {
                ["hp_restored"] = _build_member_effect_value_map(memberEffects, "hp_restored"),
                ["mp_restored"] = _build_member_effect_value_map(memberEffects, "mp_restored"),
                ["world_step_advanced"] = 1,
            }
        );
    }

    public GDictionary _execute_fog_reveal(
        GDictionary settlement,
        string action_id,
        GDictionary payload,
        int reveal_range,
        int gold_cost,
        string message_prefix
    ) => ExecuteFogRevealTyped(
        settlement,
        action_id,
        payload,
        reveal_range,
        gold_cost,
        message_prefix
    ).ToDictionary();

    private SettlementServiceResult ExecuteFogRevealTyped(
        GDictionary settlement,
        string action_id,
        GDictionary payload,
        int reveal_range,
        int gold_cost,
        string message_prefix
    )
    {
        PartyState partyState = _get_party_state();
        if (partyState == null)
        {
            return BuildSettlementServiceResultTyped(false, "当前不存在队伍数据。");
        }
        if (gold_cost > 0 && !partyState.spend_gold(gold_cost))
        {
            return BuildSettlementServiceResultTyped(false, "金币不足，无法购买情报。");
        }
        GArray revealedCoords = _reveal_world_fog(
            ReadVector2I(settlement, "origin"),
            reveal_range
        );
        string message = $"{message_prefix} 共揭示了 {revealedCoords.Count} 个周边格子。";
        if (gold_cost > 0)
        {
            message =
                $"{ReadString(settlement, "display_name", "据点")} 花费 {gold_cost} 金。{message}";
        }
        return BuildSettlementServiceResultTyped(
            true,
            message,
            extract_pending_character_rewards(
                action_id,
                payload,
                ReadString(payload, "facility_name"),
                ReadString(payload, "npc_name"),
                ReadString(payload, "service_type", "情报")
            ),
            gold_cost > 0,
            true,
            false,
            -gold_cost,
            new GDictionary(),
            _extract_quest_progress_events(
                payload,
                action_id,
                ReadString(settlement, "settlement_id")
            ),
            new GDictionary { ["fog_revealed"] = revealedCoords }
        );
    }

    public GDictArray _build_service_entries(GDictionary settlement, GDictionary settlement_state)
    {
        var entries = new GDictArray();
        foreach (
            GDictionary sourceService in Dictionaries(
                ReadArray(settlement, "available_services")
            )
        )
        {
            GDictionary serviceData = (GDictionary)sourceService.Duplicate(true);
            SettlementServiceMetadata metadata = BuildServiceMetadataTyped(
                settlement,
                serviceData,
                settlement_state
            );
            bool isEnabled = metadata.IsEnabled;
            string disabledReason = metadata.DisabledReason.Trim();
            serviceData["cost_label"] = metadata.CostLabel.Trim();
            serviceData["is_enabled"] = isEnabled;
            serviceData["disabled_reason"] = disabledReason;
            serviceData["state_label"] = _build_service_state_label(isEnabled, disabledReason);
            serviceData["summary_text"] = _build_service_summary_text(serviceData);
            string panelKind = _resolve_service_panel_kind(serviceData);
            if (!string.IsNullOrEmpty(panelKind))
            {
                serviceData["panel_kind"] = panelKind;
            }
            entries.Add(serviceData);
        }
        return entries;
    }

    public string _build_service_state_label(bool is_enabled, string disabled_reason)
    {
        if (is_enabled)
        {
            return "状态：可用";
        }
        if (!string.IsNullOrEmpty(disabled_reason))
        {
            return $"状态：{disabled_reason}";
        }
        return "状态：不可用";
    }

    public string _build_service_summary_text(GDictionary service_data)
    {
        return $"{ReadString(service_data, "facility_name").Trim()} · {ReadString(service_data, "npc_name").Trim()} · {ReadString(service_data, "service_type").Trim()}";
    }

    public string _resolve_service_panel_kind(GDictionary service_data)
    {
        string interactionScriptId = ReadString(service_data, "interaction_script_id").Trim();
        if (SHOP_INTERACTION_IDS.Contains(interactionScriptId))
        {
            return "shop";
        }
        if (STAGECOACH_INTERACTION_IDS.Contains(interactionScriptId))
        {
            return "stagecoach";
        }
        if (QuestProviderContentRules.is_supported_provider_id(interactionScriptId))
        {
            return "contract_board";
        }
        if (_is_forge_interaction(interactionScriptId))
        {
            return "forge";
        }
        return "";
    }

    public GDictionary _build_service_metadata(
        GDictionary settlement,
        GDictionary service_data,
        GDictionary settlement_state
    ) =>
        BuildServiceMetadataTyped(settlement, service_data, settlement_state).ToDictionary();

    private SettlementServiceMetadata BuildServiceMetadataTyped(
        GDictionary settlement,
        GDictionary service_data,
        GDictionary settlement_state
    )
    {
        string interactionScriptId = ReadString(service_data, "interaction_script_id");
        PartyState typedPartyState = _get_party_state();
        if (interactionScriptId == "party_warehouse")
        {
            return new SettlementServiceMetadata("免费", true);
        }
        if (interactionScriptId == "service_rest_basic")
        {
            return new SettlementServiceMetadata("免费", true);
        }
        if (interactionScriptId == "service_rest_full")
        {
            bool canAffordRest =
                typedPartyState != null && typedPartyState.can_afford(REST_FULL_COST);
            return new SettlementServiceMetadata(
                $"{REST_FULL_COST} 金",
                canAffordRest,
                canAffordRest ? "" : "金币不足"
            );
        }
        if (interactionScriptId == "service_village_rumor")
        {
            return new SettlementServiceMetadata("免费", true);
        }
        if (interactionScriptId == "service_intel_network")
        {
            bool canAffordIntel =
                typedPartyState != null && typedPartyState.can_afford(INTEL_NETWORK_COST);
            return new SettlementServiceMetadata(
                $"{INTEL_NETWORK_COST} 金",
                canAffordIntel,
                canAffordIntel ? "" : "金币不足"
            );
        }
        if (QuestProviderContentRules.is_supported_provider_id(interactionScriptId))
        {
            return new SettlementServiceMetadata("查看任务", true);
        }
        if (SHOP_INTERACTION_IDS.Contains(interactionScriptId))
        {
            return new SettlementServiceMetadata("按商品计价", true);
        }
        if (STAGECOACH_INTERACTION_IDS.Contains(interactionScriptId))
        {
            List<StagecoachDestinationData> destinations = BuildStagecoachDestinationData(
                settlement,
                interactionScriptId
            );
            bool hasDestinations = destinations.Count != 0;
            return new SettlementServiceMetadata(
                $"{STAGECOACH_COST_PER_STEP} 金/格",
                hasDestinations,
                hasDestinations ? "" : "暂无已访问路线"
            );
        }
        if (_is_forge_interaction(interactionScriptId))
        {
            bool hasRecipe = _forge_service.has_available_recipe(
                settlement,
                service_data,
                _get_item_defs(),
                _get_recipe_defs()
            );
            return new SettlementServiceMetadata(
                "按配方材料",
                hasRecipe,
                hasRecipe ? "" : _build_forge_unavailable_reason(interactionScriptId)
            );
        }
        if (_is_research_interaction(interactionScriptId))
        {
            return _research_service.BuildServiceMetadataTyped(typedPartyState);
        }
        if (UNIMPLEMENTED_INTERACTION_IDS.Contains(interactionScriptId))
        {
            return new SettlementServiceMetadata("未开放", false, "系统未开放");
        }
        return new SettlementServiceMetadata("", true);
    }

    public string _build_settlement_state_summary(GDictionary settlement_state)
    {
        WorldMapSettlementStateData stateData =
            WorldMapSettlementStateData.FromDictionary(settlement_state);
        IReadOnlyList<string> conditionStrings = stateData.ActiveConditions;
        return string.Join(
            "\n",
            new[]
            {
                $"访问：{(stateData.Visited ? "是" : "否")}",
                $"声望：{stateData.Reputation}",
                $"活跃条件：{(conditionStrings.Count != 0 ? string.Join("、", conditionStrings) : "无")}",
            }
        );
    }

    public string _build_settlement_window_feedback_text()
    {
        string feedbackText = _get_settlement_feedback_text().Trim();
        if (!string.IsNullOrEmpty(feedbackText))
        {
            return feedbackText;
        }
        return "点击服务继续，或切换成员后再操作。";
    }

    public GDictArray _build_member_options()
    {
        var options = new GDictArray();
        PartyState partyState = _get_party_state();
        if (partyState == null)
        {
            return options;
        }
        var seenMemberIds = new GDictionary();
        foreach (StringName memberId in partyState.active_member_ids)
        {
            if (
                memberId == ""
                || seenMemberIds.ContainsKey(memberId)
                || partyState.get_member_state(memberId) == null
            )
            {
                continue;
            }
            seenMemberIds[memberId] = true;
            options.Add(_build_member_option(partyState, memberId, "上阵"));
        }
        foreach (StringName memberId in partyState.reserve_member_ids)
        {
            if (
                memberId == ""
                || seenMemberIds.ContainsKey(memberId)
                || partyState.get_member_state(memberId) == null
            )
            {
                continue;
            }
            seenMemberIds[memberId] = true;
            options.Add(_build_member_option(partyState, memberId, "替补"));
        }
        return options;
    }

    public GDictionary _build_member_option(
        PartyState party_state,
        StringName member_id,
        string roster_role
    )
    {
        PartyMemberState memberState = party_state.get_member_state(member_id);
        if (memberState == null)
        {
            return new GDictionary();
        }
        return new GDictionary
        {
            ["member_id"] = member_id.ToString(),
            ["display_name"] = _get_member_display_name(member_id),
            ["roster_role"] = roster_role,
            ["is_leader"] = party_state.leader_member_id == member_id,
            ["current_hp"] = memberState.current_hp,
            ["current_mp"] = memberState.current_mp,
        };
    }

    public void _open_contract_board_modal(string settlement_id, GDictionary payload)
    {
        GDictionary windowData = _build_contract_board_window_data(settlement_id, payload);
        _set_active_contract_board_context(windowData);
        _set_active_modal_id("contract_board");
        _update_status(
            $"已打开 {ReadString(payload, "facility_name", "据点任务板")} 的任务板。"
        );
    }

    public GDictionary _build_contract_board_window_data(string settlement_id, GDictionary payload)
    {
        GDictionary settlement = _get_settlement_record(settlement_id);
        string providerInteractionId = ReadString(payload, "interaction_script_id").Trim();
        GDictArray entries = _build_contract_board_entries(providerInteractionId);
        string summaryText = ReadString(payload, "feedback_text").Trim();
        if (string.IsNullOrEmpty(summaryText))
        {
            summaryText =
                "选择契约后会按当前状态执行接取或领奖；重复接取、待领奖励和可重复任务都会返回明确反馈。";
        }
        return new GDictionary
        {
            ["title"] =
                $"{ReadString(settlement, "display_name", settlement_id)} · 任务板",
            ["meta"] =
                $"{ReadString(payload, "facility_name", "任务板")} · {ReadString(payload, "npc_name", "值守人员")} · {ReadString(payload, "service_type", "契约")}",
            ["summary_text"] = summaryText,
            ["state_summary_text"] = _build_contract_board_state_summary(entries),
            ["service_name"] = ReadString(payload, "service_type", "任务板"),
            ["settlement_id"] = settlement_id,
            ["action_id"] = ReadString(payload, "action_id"),
            ["interaction_script_id"] = providerInteractionId,
            ["provider_interaction_id"] = providerInteractionId,
            ["facility_id"] = ReadString(payload, "facility_id"),
            ["facility_name"] = ReadString(payload, "facility_name"),
            ["npc_id"] = ReadString(payload, "npc_id"),
            ["npc_name"] = ReadString(payload, "npc_name"),
            ["service_type"] = ReadString(payload, "service_type"),
            ["panel_kind"] = "contract_board",
            ["show_member_selector"] = false,
            ["confirm_label"] = "确认操作",
            ["cancel_label"] = "返回据点",
            ["entry_title"] = "可选契约",
            ["summary_title"] = "任务板概况",
            ["state_title"] = "契约状态",
            ["cost_title"] = "契约奖励",
            ["details_title"] = "契约说明",
            ["member_title"] = "执行成员",
            ["empty_state_label"] = "状态：暂无契约",
            ["empty_cost_label"] = "奖励：无",
            ["empty_details_text"] = "当前没有可查看契约。",
            ["entries"] = entries,
        };
    }

    public GDictArray _build_contract_board_entries(string interaction_script_id)
    {
        var entries = new GDictArray();
        string normalizedInteractionId = interaction_script_id.Trim();
        GDictionary questDefs = _get_quest_defs();
        var questIds = new List<StringName>();
        foreach (object questKeyValue in questDefs.Keys)
        {
            if (!TryAsStrictStringNameKey(questKeyValue, out StringName questKey))
            {
                continue;
            }
            questIds.Add(questKey);
        }
        questIds.Sort((a, b) => string.CompareOrdinal(a.ToString(), b.ToString()));
        foreach (StringName questId in questIds)
        {
            object questValue = questDefs[questId];
            GDictionary questData = new GDictionary();
            if (TryAsDictionary(questValue, out GDictionary questDictionary))
            {
                questData = (GDictionary)questDictionary.Duplicate(true);
            }
            else if (TryAsObject(questValue, out QuestDef questDef))
            {
                questData = questDef.to_dict().Duplicate(true);
            }
            GDictionary questEntry = _build_contract_board_entry(
                questData,
                normalizedInteractionId
            );
            if (questEntry.Count != 0)
            {
                entries.Add(questEntry);
            }
        }
        if (entries.Count == 0)
        {
            string missingProviderText =
                $"当前没有 provider_interaction_id 绑定到 {normalizedInteractionId} 的任务定义。";
            if (string.IsNullOrEmpty(normalizedInteractionId))
            {
                missingProviderText =
                    "当前任务板缺少 interaction_script_id，无法匹配 provider_interaction_id。";
            }
            entries.Add(
                new GDictionary
                {
                    ["entry_id"] = "placeholder",
                    ["display_name"] = "当前暂无可展示契约",
                    ["summary_text"] = "任务定义尚未挂到这块任务板上。",
                    ["details_text"] = missingProviderText,
                    ["state_id"] = "empty",
                    ["state_label"] = "状态：空",
                    ["cost_label"] = "奖励：无",
                    ["is_enabled"] = false,
                    ["disabled_reason"] = "暂无可查看任务。",
                }
            );
        }
        return entries;
    }

    public GDictionary _build_contract_board_entry(
        GDictionary quest_data,
        string interaction_script_id
    )
    {
        GDictionary questData = _normalize_contract_board_quest_data(quest_data);
        if (questData.Count == 0)
        {
            return new GDictionary();
        }
        string providerInteractionId = ReadString(questData, "provider_interaction_id").Trim();
        if (
            string.IsNullOrEmpty(providerInteractionId)
            || providerInteractionId != interaction_script_id
        )
        {
            return new GDictionary();
        }
        StringName questId = ReadStringName(questData, "quest_id");
        if (questId == "")
        {
            return new GDictionary();
        }
        string stateId = _resolve_contract_board_quest_state_id(questId, questData);
        return new GDictionary
        {
            ["entry_id"] = questId.ToString(),
            ["quest_id"] = questId.ToString(),
            ["provider_interaction_id"] = providerInteractionId,
            ["display_name"] = questData["display_name"].AsString(),
            ["summary_text"] = _build_contract_board_objective_summary(questId, questData),
            ["details_text"] = _build_contract_board_entry_details(questId, questData),
            ["state_id"] = stateId,
            ["state_label"] = _build_contract_board_state_label(stateId),
            ["cost_label"] = _build_contract_board_reward_label(
                questData["reward_entries"].AsGodotArray()
            ),
            ["is_enabled"] = true,
            ["disabled_reason"] = "",
            ["is_repeatable"] = IsContractBoardQuestRepeatable(questData),
        };
    }

    public string _resolve_contract_board_quest_state_id(
        StringName quest_id,
        GDictionary quest_data = null
    )
    {
        quest_data ??= new GDictionary();
        PartyState partyState = _get_party_state();
        if (partyState == null)
        {
            return "available";
        }
        if (partyState.get_active_quest_state(quest_id) != null)
        {
            return "active";
        }
        if (partyState.has_claimable_quest(quest_id))
        {
            return "claimable";
        }
        if (partyState.has_completed_quest(quest_id))
        {
            if (IsContractBoardQuestRepeatable(quest_data))
            {
                return "repeatable";
            }
            return "completed";
        }
        return "available";
    }

    public string _build_contract_board_state_label(string state_id)
    {
        switch (state_id)
        {
            case "active":
                return "状态：进行中";
            case "claimable":
                return "状态：待领奖励";
            case "repeatable":
                return "状态：可重复接取";
            case "completed":
                return "状态：已完成";
            case "empty":
                return "状态：空";
            default:
                return "状态：待接取";
        }
    }

    public string _build_contract_board_state_summary(GDictArray entries)
    {
        int activeCount = 0;
        int availableCount = 0;
        int claimableCount = 0;
        int repeatableCount = 0;
        int completedCount = 0;
        foreach (GDictionary entry in entries)
        {
            switch (ReadString(entry, "state_id"))
            {
                case "active":
                    activeCount += 1;
                    break;
                case "claimable":
                    claimableCount += 1;
                    break;
                case "repeatable":
                    repeatableCount += 1;
                    break;
                case "completed":
                    completedCount += 1;
                    break;
                case "empty":
                    break;
                default:
                    availableCount += 1;
                    break;
            }
        }
        var parts = new List<string> { $"进行中 {activeCount}", $"待接取 {availableCount}" };
        if (claimableCount > 0)
        {
            parts.Add($"待领奖励 {claimableCount}");
        }
        if (repeatableCount > 0)
        {
            parts.Add($"可重复 {repeatableCount}");
        }
        parts.Add($"已完成 {completedCount}");
        return string.Join("  |  ", parts);
    }

    public string _build_contract_board_objective_summary(
        StringName quest_id,
        GDictionary quest_data
    )
    {
        List<string> objectiveLines = _build_contract_board_objective_lines(quest_id, quest_data);
        return "目标：" + string.Join(" / ", objectiveLines);
    }

    public string _build_contract_board_entry_details(StringName quest_id, GDictionary quest_data)
    {
        var lines = new List<string>
        {
            quest_data["description"].AsString(),
            _build_contract_board_objective_summary(quest_id, quest_data),
            _build_contract_board_reward_label(quest_data["reward_entries"].AsGodotArray()),
        };
        if (IsContractBoardQuestRepeatable(quest_data))
        {
            lines.Add("说明：该契约完成后可再次接取。");
        }
        return string.Join("\n", lines);
    }

    public List<string> _build_contract_board_objective_lines(
        StringName quest_id,
        GDictionary quest_data
    )
    {
        var objectiveLines = new List<string>();
        GArray objectiveDefs = quest_data["objective_defs"].AsGodotArray();
        QuestState questState = _get_active_quest_state(quest_id);
        string stateId = _resolve_contract_board_quest_state_id(quest_id, quest_data);
        bool isCompleted = _is_contract_board_completed_state(stateId);
        foreach (GDictionary objectiveData in Dictionaries(objectiveDefs))
        {
            StringName objectiveId = ProgressionDataUtils.to_string_name(
                objectiveData["objective_id"]
            );
            int targetValue = objectiveData["target_value"].AsInt32();
            int currentValue = isCompleted ? targetValue : 0;
            if (!isCompleted && questState != null)
            {
                currentValue = questState.get_objective_progress(objectiveId);
            }
            objectiveLines.Add(
                $"{_describe_contract_board_objective(objectiveData)} {currentValue}/{targetValue}"
            );
        }
        return objectiveLines;
    }

    public string _describe_contract_board_objective(GDictionary objective_data)
    {
        StringName objectiveType = ProgressionDataUtils.to_string_name(
            objective_data["objective_type"]
        );
        string targetId = objective_data["target_id"].AsString();
        if (objectiveType == "settlement_action")
        {
            return $"据点事务 {targetId}";
        }
        if (objectiveType == "defeat_enemy")
        {
            return "击败敌对遭遇";
        }
        if (objectiveType == "submit_item")
        {
            StringName itemId = ProgressionDataUtils.to_string_name(targetId);
            return $"提交物资 {_get_item_display_name(itemId)}";
        }
        return "";
    }

    public string _build_contract_board_reward_label(GArray reward_entries)
    {
        var rewardParts = new List<string>();
        foreach (GDictionary rewardData in Dictionaries(reward_entries))
        {
            StringName rewardType = ProgressionDataUtils.to_string_name(rewardData["reward_type"]);
            if (rewardType == "gold")
            {
                rewardParts.Add($"{rewardData["amount"].AsInt32()} 金");
            }
            else if (rewardType == "item")
            {
                StringName rewardItemId = QuestDef.get_reward_item_id(rewardData);
                int rewardQuantity = QuestDef.get_reward_quantity(rewardData);
                rewardParts.Add($"{_get_item_display_name(rewardItemId)} x{rewardQuantity}");
            }
            else if (rewardType == "pending_character_reward")
            {
                rewardParts.Add("角色奖励");
            }
        }
        return $"奖励：{string.Join("、", rewardParts)}";
    }

    public QuestState _get_active_quest_state(StringName quest_id)
    {
        PartyState partyState = _get_party_state();
        if (partyState == null)
        {
            return null;
        }
        return partyState.get_active_quest_state(quest_id);
    }

    public StringName _resolve_active_submit_item_objective_id(
        StringName quest_id,
        GDictionary quest_data
    )
    {
        QuestState questState = _get_active_quest_state(quest_id);
        if (questState == null)
        {
            return "";
        }
        GArray objectiveDefs = quest_data["objective_defs"].AsGodotArray();
        foreach (GDictionary objectiveData in Dictionaries(objectiveDefs))
        {
            if (
                ProgressionDataUtils.to_string_name(objectiveData["objective_type"])
                != QuestDef.OBJECTIVE_SUBMIT_ITEM()
            )
            {
                continue;
            }
            StringName objectiveId = ProgressionDataUtils.to_string_name(
                objectiveData["objective_id"]
            );
            int targetValue = objectiveData["target_value"].AsInt32();
            if (questState.is_objective_complete(objectiveId, targetValue))
            {
                continue;
            }
            return objectiveId;
        }
        return "";
    }

    public bool _quest_has_submit_item_objective(GDictionary quest_data)
    {
        GArray objectiveDefs = quest_data["objective_defs"].AsGodotArray();
        foreach (GDictionary objectiveData in Dictionaries(objectiveDefs))
        {
            if (
                ProgressionDataUtils.to_string_name(
                    objectiveData["objective_type"]
                ) == QuestDef.OBJECTIVE_SUBMIT_ITEM()
            )
            {
                return true;
            }
        }
        return false;
    }

    public void _open_shop_modal(string settlement_id, GDictionary payload)
    {
        GDictionary settlementState = _get_or_create_settlement_state(settlement_id);
        settlementState["world_step"] = _get_world_step();
        GDictionary windowData = _shop_service.BuildWindowDataTyped(
            ReadString(payload, "interaction_script_id"),
            _get_settlement_record(settlement_id),
            settlementState,
            _get_item_defs(),
            _get_party_warehouse_service(),
            _get_party_gold()
        );
        _set_active_settlement_state(settlement_id, settlementState);
        windowData["settlement_id"] = settlement_id;
        windowData["interaction_script_id"] = ReadString(payload, "interaction_script_id");
        _set_active_shop_context(windowData);
        _set_active_modal_id("shop");
        _update_status(
            $"已打开 {ReadString(payload, "facility_name", "据点商店")} 的商店。"
        );
    }

    public void _open_forge_modal(string settlement_id, GDictionary payload)
    {
        GDictionary windowData = _forge_service.BuildWindowDataTyped(
            ReadString(payload, "interaction_script_id"),
            _get_settlement_record(settlement_id),
            payload,
            _get_item_defs(),
            _get_recipe_defs(),
            _get_party_warehouse_service()
        );
        windowData["settlement_id"] = settlement_id;
        windowData["interaction_script_id"] = ReadString(payload, "interaction_script_id");
        windowData["service_payload"] = payload.Duplicate(true);
        windowData["member_options"] = _build_member_options();
        StringName selectedMemberId = ReadStringName(payload, "member_id");
        if (selectedMemberId == "")
        {
            selectedMemberId = resolve_default_settlement_member_id();
        }
        windowData["default_member_id"] = selectedMemberId.ToString();
        windowData["selected_member_id"] = selectedMemberId.ToString();
        _set_active_forge_context(windowData);
        _set_active_modal_id("forge");
        _update_status(
            $"已打开 {ReadString(payload, "facility_name", "据点工坊")} 的{_resolve_forge_service_label(payload)}窗口。"
        );
    }

    public void _open_stagecoach_modal(string settlement_id, GDictionary payload)
    {
        GDictionary settlement = _get_settlement_record(settlement_id);
        _set_active_stagecoach_context(
            new GDictionary
            {
                ["title"] = $"{ReadString(settlement, "display_name", "据点")} · 驿站路线",
                ["settlement_id"] = settlement_id,
                ["origin_name"] = ReadString(settlement, "display_name", "据点"),
                ["interaction_script_id"] = ReadString(payload, "interaction_script_id"),
                ["gold"] = _get_party_gold(),
                ["destinations"] = _build_stagecoach_destinations(
                    settlement,
                    ReadString(payload, "interaction_script_id")
                ),
                ["feedback_text"] = "选择一个已访问据点并支付路费后即可启程。",
            }
        );
        _set_active_modal_id("stagecoach");
        _update_status("已打开驿站路线。");
    }

    public void _refresh_active_shop_context()
    {
        GDictionary context = _get_active_shop_context();
        if (context.Count == 0)
        {
            return;
        }
        string settlementId = ReadString(context, "settlement_id");
        GDictionary settlementState = _get_or_create_settlement_state(settlementId);
        settlementState["world_step"] = _get_world_step();
        GDictionary nextContext = _shop_service.BuildWindowDataTyped(
            ReadString(context, "interaction_script_id"),
            _get_settlement_record(settlementId),
            settlementState,
            _get_item_defs(),
            _get_party_warehouse_service(),
            _get_party_gold()
        );
        _set_active_settlement_state(settlementId, settlementState);
        nextContext["settlement_id"] = settlementId;
        nextContext["interaction_script_id"] = ReadString(context, "interaction_script_id");
        _set_active_shop_context(nextContext);
    }

    public void _refresh_active_contract_board_context(string feedback_text = "")
    {
        GDictionary context = _get_active_contract_board_context();
        if (context.Count == 0)
        {
            return;
        }
        string settlementId = ReadString(context, "settlement_id");
        GDictionary nextPayload = (GDictionary)context.Duplicate(true);
        if (!string.IsNullOrEmpty(feedback_text))
        {
            nextPayload["feedback_text"] = feedback_text;
        }
        GDictionary nextContext = _build_contract_board_window_data(settlementId, nextPayload);
        _set_active_contract_board_context(nextContext);
    }

    public void _refresh_active_forge_context(string feedback_text = "")
    {
        GDictionary context = _get_active_forge_context();
        if (context.Count == 0)
        {
            return;
        }
        string settlementId = ReadString(context, "settlement_id");
        GDictionary servicePayload = (GDictionary)ReadDictionary(context, "service_payload").Duplicate(true);
        string interactionScriptId = ReadString(context, "interaction_script_id");
        if (string.IsNullOrEmpty(interactionScriptId))
        {
            interactionScriptId = ReadString(servicePayload, "interaction_script_id");
        }
        GDictionary nextContext = _forge_service.BuildWindowDataTyped(
            interactionScriptId,
            _get_settlement_record(settlementId),
            servicePayload,
            _get_item_defs(),
            _get_recipe_defs(),
            _get_party_warehouse_service(),
            !string.IsNullOrEmpty(feedback_text)
                ? feedback_text
                : ReadString(context, "feedback_text")
        );
        nextContext["settlement_id"] = settlementId;
        nextContext["interaction_script_id"] = interactionScriptId;
        nextContext["service_payload"] = servicePayload;
        nextContext["member_options"] = ReadArray(context, "member_options");
        string defaultMemberId = ReadString(context, "default_member_id");
        if (string.IsNullOrEmpty(defaultMemberId))
        {
            defaultMemberId = ReadString(servicePayload, "member_id");
        }
        nextContext["default_member_id"] = defaultMemberId;
        string selectedMemberId = ReadString(context, "selected_member_id");
        if (string.IsNullOrEmpty(selectedMemberId))
        {
            selectedMemberId = defaultMemberId;
        }
        nextContext["selected_member_id"] = selectedMemberId;
        _set_active_forge_context(nextContext);
    }

    public GDictArray _build_stagecoach_destinations(
        GDictionary origin_settlement,
        string interaction_script_id
    )
    {
        var entries = new GDictArray();
        foreach (
            StagecoachDestinationData destination in BuildStagecoachDestinationData(
                origin_settlement,
                interaction_script_id
            )
        )
        {
            entries.Add(destination.ToDictionary());
        }
        return entries;
    }

    private List<StagecoachDestinationData> BuildStagecoachDestinationData(
        GDictionary origin_settlement,
        string interaction_script_id
    )
    {
        var entries = new List<StagecoachDestinationData>();
        string originSettlementId = ReadString(origin_settlement, "settlement_id");
        Vector2I originCoord = ReadVector2I(origin_settlement, "origin");
        foreach (GDictionary settlement in Dictionaries(_get_all_settlement_records()))
        {
            string settlementId = ReadString(settlement, "settlement_id");
            if (string.IsNullOrEmpty(settlementId) || settlementId == originSettlementId)
            {
                continue;
            }
            if (!IsSettlementVisited(settlementId))
            {
                continue;
            }
            Vector2I targetCoord = ReadVector2I(settlement, "origin");
            int travelCost =
                (Math.Abs(targetCoord.X - originCoord.X) + Math.Abs(targetCoord.Y - originCoord.Y))
                * STAGECOACH_COST_PER_STEP;
            bool canTravel = _get_party_gold() >= travelCost;
            entries.Add(
                new StagecoachDestinationData(
                    settlementId,
                    ReadString(settlement, "display_name", settlementId),
                    ReadString(settlement, "tier_name"),
                    travelCost,
                    canTravel,
                    canTravel ? "" : "金币不足",
                    targetCoord,
                    interaction_script_id
                )
            );
        }
        return entries;
    }

    public GDictionary _find_stagecoach_destination(
        GDictionary stagecoach_context,
        string settlement_id
    )
    {
        foreach (
            GDictionary destination in Dictionaries(
                ReadArray(stagecoach_context, "destinations")
            )
        )
        {
            if (ReadString(destination, "settlement_id") == settlement_id)
            {
                return destination;
            }
        }
        return new GDictionary();
    }

    private StagecoachDestinationData ResolveStagecoachDestinationTyped(
        GDictionary stagecoach_context,
        string settlement_id
    )
    {
        string originSettlementId = ReadString(stagecoach_context, "settlement_id");
        if (string.IsNullOrEmpty(originSettlementId) || string.IsNullOrEmpty(settlement_id))
        {
            return null;
        }
        GDictionary originSettlement = _get_settlement_record(originSettlementId);
        if (originSettlement.Count == 0)
        {
            return null;
        }
        string interactionScriptId = ReadString(stagecoach_context, "interaction_script_id");
        foreach (
            StagecoachDestinationData destination in BuildStagecoachDestinationData(
                originSettlement,
                interactionScriptId
            )
        )
        {
            if (destination.SettlementId == settlement_id)
            {
                return destination;
            }
        }
        return null;
    }

    public GDictionary _restore_party_resources(float restore_ratio, bool restore_full)
    {
        var effects = new GDictionary();
        PartyState partyState = _get_party_state();
        if (partyState == null)
        {
            return effects;
        }
        foreach (StringName memberId in partyState.active_member_ids)
        {
            PartyMemberState memberState = partyState.get_member_state(memberId);
            if (memberState == null)
            {
                continue;
            }
            AttributeSnapshot attributeSnapshot = _get_member_attribute_snapshot(memberId);
            int hpMax =
                attributeSnapshot != null
                    ? attributeSnapshot.get_value(new StringName("hp_max"))
                    : Math.Max(memberState.current_hp, 1);
            int mpMax =
                attributeSnapshot != null
                    ? attributeSnapshot.get_value(new StringName("mp_max"))
                    : Math.Max(memberState.current_mp, 0);
            int oldHp = memberState.current_hp;
            int oldMp = memberState.current_mp;
            double recoveryMultiplier = 1.0;
            if (
                attributeSnapshot != null
                && attributeSnapshot.get_value(LowLuckRelicRules.ATTR_BLOOD_DEBT_SHAWL) > 0
            )
            {
                recoveryMultiplier = LowLuckRelicRules.BLOOD_DEBT_RECOVERY_MULTIPLIER;
            }
            int hpRestoreAmount = restore_full
                ? hpMax - oldHp
                : (int)Math.Ceiling(hpMax * (double)restore_ratio);
            hpRestoreAmount = (int)Math.Ceiling(Math.Max(hpRestoreAmount, 0) * recoveryMultiplier);
            memberState.current_hp = Math.Min(oldHp + hpRestoreAmount, hpMax);
            int mpRestoreAmount = restore_full ? mpMax - oldMp : 0;
            mpRestoreAmount = (int)Math.Ceiling(Math.Max(mpRestoreAmount, 0) * recoveryMultiplier);
            memberState.current_mp = Math.Min(oldMp + mpRestoreAmount, mpMax);
            effects[memberId.ToString()] = new GDictionary
            {
                ["hp_restored"] = Math.Max(memberState.current_hp - oldHp, 0),
                ["mp_restored"] = Math.Max(memberState.current_mp - oldMp, 0),
            };
        }
        return effects;
    }

    public GArray _reveal_world_fog(Vector2I center, int reveal_range)
    {
        WorldMapFogSystem fogSystem = _get_fog_system();
        GArray revealedCoords =
            fogSystem != null
                ? new GArray(fogSystem.RevealDiamond(center, reveal_range, _get_player_faction_id()).Select(v => Variant.From(v)))
                : new GArray();
        if (revealedCoords.Count != 0)
        {
            _refresh_world_visibility();
        }
        return revealedCoords;
    }

    public void _mark_settlement_visited(string settlement_id)
    {
        if (!_has_runtime())
        {
            return;
        }
        Runtime.MarkSettlementVisited(settlement_id);
    }

    private bool IsSettlementVisited(string settlementId) =>
        Runtime?.IsSettlementVisited(settlementId) ?? false;

    public GDictionary _get_or_create_settlement_state(string settlement_id)
    {
        GDictionary settlementState = _get_settlement_state(settlement_id);
        if (settlementState.Count == 0)
        {
            settlementState = new GDictionary
            {
                ["visited"] = false,
                ["reputation"] = 0,
                ["active_conditions"] = new GArray(),
                ["cooldowns"] = new GDictionary(),
                ["shop_inventory_seed"] = TrueRandomSeedService.generate_seed(),
                ["shop_last_refresh_step"] = 0,
                ["shop_states"] = new GDictionary(),
            };
            _set_active_settlement_state(settlement_id, settlementState);
        }
        return settlementState;
    }

    public GDictionary _finalize_successful_action(
        string action_id,
        GDictionary payload,
        GDictionary result
    ) =>
        FinalizeSuccessfulActionTyped(
            action_id,
            payload,
            SettlementServiceResult.FromDictionary(result)
        ).ToDictionary();

    public GDictionary _finalize_successful_action(
        string action_id,
        GDictionary payload,
        SettlementServiceResult result
    ) =>
        FinalizeSuccessfulActionTyped(action_id, payload, result).ToDictionary();

    private SettlementPersistResult FinalizeSuccessfulActionTyped(
        string action_id,
        GDictionary payload,
        SettlementServiceResult result
    )
    {
        if (result == null)
            return PersistChangesTyped(false, false, false);

        _enqueue_pending_character_rewards(ResultPendingCharacterRewards(result));
        _apply_quest_progress_events(ResultQuestProgressEvents(result));
        StringName memberId = ReadStringName(payload, "member_id");
        if (memberId != "")
        {
            _notify_misfortune_guidance_of_forge_result(memberId, result);
            _record_member_achievement_event(
                memberId,
                "settlement_action_completed",
                1,
                ProgressionDataUtils.to_string_name(action_id)
            );
        }
        _sync_party_state_from_character_management();
        return PersistChangesTyped(
            result.PersistPartyState,
            result.PersistWorldData,
            result.PersistPlayerCoord
        );
    }

    public void _notify_misfortune_guidance_of_forge_result(
        StringName member_id,
        SettlementServiceResult result
    )
    {
        if (member_id == "" || Runtime == null || result == null)
        {
            return;
        }
        GDictionary inventoryDelta = result.InventoryDelta;
        if (ReadStringName(inventoryDelta, "recipe_id") == "")
        {
            return;
        }
        Runtime?.HandleMisfortuneForgeResult(member_id, result);
    }

    public GDictionary _build_settlement_service_result(
        bool success,
        string message,
        GDictArray pending_character_rewards = null,
        bool persist_party_state = false,
        bool persist_world_data = false,
        bool persist_player_coord = false,
        int gold_delta = 0,
        GDictionary inventory_delta = null,
        GDictArray quest_progress_events = null,
        GDictionary service_side_effects = null
    ) => BuildSettlementServiceResultTyped(
        success,
        message,
        pending_character_rewards,
        persist_party_state,
        persist_world_data,
        persist_player_coord,
        gold_delta,
        inventory_delta,
        quest_progress_events,
        service_side_effects
    ).ToDictionary();

    private SettlementServiceResult BuildSettlementServiceResultTyped(
        bool success,
        string message,
        GDictArray pending_character_rewards = null,
        bool persist_party_state = false,
        bool persist_world_data = false,
        bool persist_player_coord = false,
        int gold_delta = 0,
        GDictionary inventory_delta = null,
        GDictArray quest_progress_events = null,
        GDictionary service_side_effects = null
    )
    {
        pending_character_rewards ??= new GDictArray();
        inventory_delta ??= new GDictionary();
        quest_progress_events ??= new GDictArray();
        service_side_effects ??= new GDictionary();
        var result = new SettlementServiceResult
        {
            Success = success,
            Message = message,
            PersistPartyState = persist_party_state,
            PersistWorldData = persist_world_data,
            PersistPlayerCoord = persist_player_coord,
            GoldDelta = gold_delta,
        };
        result.SetInventoryDelta(inventory_delta);
        result.SetPendingCharacterRewardPayloads(
            ToUntypedDictArray(_duplicate_dictionary_array(ToUntypedDictArray(pending_character_rewards)))
        );
        result.SetQuestProgressEventPayloads(
            ToUntypedDictArray(_duplicate_dictionary_array(ToUntypedDictArray(quest_progress_events)))
        );
        result.SetServiceSideEffects(service_side_effects);
        return result;
    }

    public GArray _result_pending_character_rewards(GDictionary result)
    {
        var parsed = SettlementServiceResult.FromDictionary(result);
        return parsed != null ? ResultPendingCharacterRewards(parsed) : new GArray();
    }

    public GArray _result_quest_progress_events(GDictionary result)
    {
        var parsed = SettlementServiceResult.FromDictionary(result);
        return parsed != null ? ResultQuestProgressEvents(parsed) : new GArray();
    }

    private static GArray ResultPendingCharacterRewards(SettlementServiceResult result) =>
        result != null ? ToUntypedDictArray(result.PendingCharacterRewards) : new GArray();

    private static GArray ResultQuestProgressEvents(SettlementServiceResult result) =>
        result != null ? ToUntypedDictArray(result.QuestProgressEvents) : new GArray();

    public GDictArray _extract_quest_progress_events(
        GDictionary payload,
        string action_id,
        string settlement_id
    )
    {
        GDictArray questProgressEvents = _duplicate_dictionary_array(
            ReadArray(payload, "quest_progress_events")
        );
        int worldStep = _get_world_step();
        foreach (GDictionary eventData in questProgressEvents)
        {
            if (!eventData.ContainsKey("world_step"))
            {
                eventData["world_step"] = worldStep;
            }
        }
        if (!ReadBool(payload, "emit_default_quest_progress_event", true))
        {
            return questProgressEvents;
        }
        questProgressEvents.Add(
            new GDictionary
            {
                ["event_type"] = "progress",
                ["objective_type"] = "settlement_action",
                ["target_id"] = action_id,
                ["progress_delta"] = 1,
                ["world_step"] = worldStep,
                ["action_id"] = action_id,
                ["settlement_id"] = settlement_id,
                ["member_id"] = ReadString(payload, "member_id"),
            }
        );
        return questProgressEvents;
    }

    public void _apply_quest_progress_events(GArray event_options)
    {
        if (!_has_runtime() || event_options.Count == 0)
        {
            return;
        }
        Runtime.apply_quest_progress_events_to_party(event_options, "settlement");
    }

    public GDictArray _duplicate_dictionary_array(GArray value)
    {
        var result = new GDictArray();
        if (value == null)
        {
            return result;
        }
        foreach (GDictionary entryData in Dictionaries(value))
        {
            result.Add((GDictionary)entryData.Duplicate(true));
        }
        return result;
    }

    public GDictionary _build_member_effect_value_map(GDictionary member_effects, string value_key)
    {
        var values = new GDictionary();
        foreach (object memberIdValue in member_effects.Keys)
        {
            string memberId = memberIdValue.ToString();
            GDictionary effect = ReadDictionary(member_effects, memberId);
            if (effect.Count == 0)
            {
                continue;
            }
            values[memberId] = ReadInt(effect, value_key);
        }
        return values;
    }

    public GDictionary _persist_changes(
        bool persist_party_state,
        bool persist_world_data,
        bool persist_player_coord
    ) =>
        PersistChangesTyped(
            persist_party_state,
            persist_world_data,
            persist_player_coord
        ).ToDictionary();

    private SettlementPersistResult PersistChangesTyped(
        bool persist_party_state,
        bool persist_world_data,
        bool persist_player_coord
    )
    {
        int partyError = (int)Error.Ok;
        int worldError = (int)Error.Ok;
        int playerError = (int)Error.Ok;
        if (persist_party_state)
        {
            partyError = _persist_party_state();
        }
        if (persist_world_data)
        {
            worldError = _persist_world_data();
        }
        if (persist_player_coord)
        {
            playerError = _persist_player_coord();
        }
        return new SettlementPersistResult(partyError, worldError, playerError);
    }

    public bool _has_runtime()
    {
        return Runtime != null;
    }

    public GDictionary _command_ok(string message = "")
    {
        if (Runtime == null)
        {
            return new GDictionary
            {
                ["ok"] = true,
                ["message"] = message,
                ["battle_refresh_mode"] = "",
            };
        }
        return Runtime.build_command_ok(message);
    }

    public GDictionary _command_error(string message)
    {
        if (Runtime == null)
        {
            return new GDictionary { ["ok"] = false, ["message"] = message };
        }
        return Runtime.build_command_error(message);
    }

    public bool _is_battle_active()
    {
        return _has_runtime() && Runtime.is_battle_active();
    }

    public void _update_status(string message)
    {
        if (_has_runtime())
        {
            Runtime.update_status(message);
        }
    }

    public string _get_active_settlement_id()
    {
        return Runtime?.get_active_settlement_id() ?? "";
    }

    public void _set_active_settlement_id(string settlement_id)
    {
        if (_has_runtime())
        {
            Runtime.set_active_settlement_id(settlement_id);
        }
    }

    public void _set_settlement_feedback_text(string feedback_text)
    {
        if (_has_runtime())
        {
            Runtime.set_settlement_feedback_text(feedback_text);
        }
    }

    public string _get_settlement_feedback_text()
    {
        return Runtime?.get_settlement_feedback_text() ?? "";
    }

    public GDictionary _get_selected_settlement()
    {
        return _has_runtime() ? Runtime.get_selected_settlement() : new GDictionary();
    }

    public PartyState _get_party_state()
    {
        return Runtime?.get_party_state();
    }

    public int _get_party_gold()
    {
        return _get_party_state()?.get_gold() ?? 0;
    }

    public GDictionary _get_settlement_record(string settlement_id)
    {
        return _has_runtime() ? Runtime.get_settlement_record(settlement_id) : new GDictionary();
    }

    public GArray _get_all_settlement_records()
    {
        return _has_runtime() ? Runtime.get_all_settlement_records() : new GArray();
    }

    public GDictionary _get_settlement_state(string settlement_id)
    {
        return _has_runtime() ? Runtime.get_settlement_state(settlement_id) : new GDictionary();
    }

    public bool _set_active_settlement_state(string settlement_id, GDictionary settlement_state)
    {
        return _has_runtime()
            && Runtime.set_active_settlement_state(settlement_id, settlement_state);
    }

    public PartyWarehouseService _get_party_warehouse_service()
    {
        return _has_runtime() ? Runtime.get_party_warehouse_service() : null;
    }

    public GDictionary _get_item_defs()
    {
        if (!_has_runtime())
        {
            return new GDictionary();
        }
        GameSession gameSession = Runtime.get_game_session();
        return gameSession != null ? gameSession.get_item_defs() : new GDictionary();
    }

    public string _get_item_display_name(StringName item_id)
    {
        return Runtime?.get_item_display_name(item_id) ?? item_id.ToString();
    }

    public GDictionary _get_recipe_defs()
    {
        if (!_has_runtime())
        {
            return new GDictionary();
        }
        GameSession gameSession = Runtime.get_game_session();
        return gameSession != null ? gameSession.get_recipe_defs() : new GDictionary();
    }

    public GDictionary _get_quest_defs()
    {
        if (!_has_runtime())
        {
            return new GDictionary();
        }
        GameSession gameSession = Runtime.get_game_session();
        return gameSession != null ? gameSession.get_quest_defs() : new GDictionary();
    }

    public bool _is_forge_modal_submission(GDictionary payload)
    {
        return ReadString(payload, "submission_source") == "forge";
    }

    public bool _is_contract_board_modal_submission(GDictionary payload)
    {
        return ReadString(payload, "submission_source").Trim() == "contract_board";
    }

    public GDictionary _submit_contract_board_quest_action(
        string settlement_id,
        string action_id,
        GDictionary payload
    )
    {
        if (!_has_runtime())
        {
            return _command_error("运行时尚未初始化。");
        }
        StringName questId = ReadStringName(payload, "quest_id");
        if (questId == "")
        {
            string missingIdMessage = "当前契约条目缺少 quest_id，无法接取。";
            _set_settlement_feedback_text(missingIdMessage);
            _refresh_active_contract_board_context(missingIdMessage);
            _update_status(missingIdMessage);
            return _command_error(missingIdMessage);
        }
        GDictionary questData = _resolve_contract_board_submission_quest_data(questId);
        if (questData.Count == 0)
        {
            string missingQuestMessage = $"当前任务板未找到契约 {questId}。";
            _set_settlement_feedback_text(missingQuestMessage);
            _refresh_active_contract_board_context(missingQuestMessage);
            _update_status(missingQuestMessage);
            return _command_error(missingQuestMessage);
        }
        string providerInteractionId = ReadString(payload, "provider_interaction_id").Trim();
        if (string.IsNullOrEmpty(providerInteractionId))
        {
            string missingProviderMessage =
                "当前契约条目缺少 provider_interaction_id，无法匹配任务板。";
            _set_settlement_feedback_text(missingProviderMessage);
            _refresh_active_contract_board_context(missingProviderMessage);
            _update_status(missingProviderMessage);
            return _command_error(missingProviderMessage);
        }
        string questProviderInteractionId = ReadString(
            questData,
            "provider_interaction_id"
        ).Trim();
        if (questProviderInteractionId != providerInteractionId)
        {
            string providerMismatchMessage =
                $"契约 {questData["display_name"].AsString()} 不属于当前任务板。";
            _set_settlement_feedback_text(providerMismatchMessage);
            _refresh_active_contract_board_context(providerMismatchMessage);
            _update_status(providerMismatchMessage);
            return _command_error(providerMismatchMessage);
        }
        string stateId = _resolve_contract_board_quest_state_id(questId, questData);
        GDictionary commandResult;
        if (stateId == "claimable")
        {
            commandResult = Runtime.command_claim_quest(questId);
        }
        else if (stateId == "active")
        {
            StringName submitItemObjectiveId = _resolve_active_submit_item_objective_id(
                questId,
                questData
            );
            if (
                submitItemObjectiveId != ""
                || _quest_has_submit_item_objective(questData)
            )
            {
                commandResult = Runtime.command_submit_quest_item(questId, submitItemObjectiveId);
            }
            else
            {
                bool allowReaccept = IsContractBoardQuestRepeatable(questData);
                commandResult = Runtime.command_accept_quest(questId, allowReaccept);
            }
        }
        else
        {
            bool allowReaccept = IsContractBoardQuestRepeatable(questData);
            commandResult = Runtime.command_accept_quest(questId, allowReaccept);
        }
        RuntimeCommandResultSnapshot commandSnapshot =
            RuntimeCommandResultSnapshot.FromDictionary(commandResult);
        string message = string.IsNullOrEmpty(commandSnapshot.Message)
            ? "任务处理失败。"
            : commandSnapshot.Message;
        _set_active_settlement_id(settlement_id);
        _set_active_modal_id("contract_board");
        _set_settlement_feedback_text(message);
        _refresh_active_contract_board_context(message);
        if (commandSnapshot.Ok)
        {
            return _command_ok(message);
        }
        return _command_error(message);
    }

    public GDictionary _resolve_contract_board_submission_quest_data(StringName quest_id)
    {
        GDictionary questDefs = _get_quest_defs();
        object questValue = null;
        if (quest_id != "")
        {
            foreach (object valueKey in questDefs.Keys)
            {
                if (!TryAsStrictStringNameKey(valueKey, out StringName typedKey))
                    continue;
                if (typedKey == quest_id)
                {
                    questValue = questDefs[typedKey];
                    break;
                }
            }
        }
        if (questValue == null)
            return new GDictionary();
        GDictionary questData = new GDictionary();
        if (TryAsDictionary(questValue, out GDictionary questDictionary))
        {
            questData = (GDictionary)questDictionary.Duplicate(true);
        }
        else if (TryAsObject(questValue, out QuestDef questDef))
        {
            questData = questDef.to_dict().Duplicate(true);
        }
        return _normalize_contract_board_quest_data(questData);
    }

    public GDictionary _normalize_contract_board_quest_data(GDictionary quest_option)
    {
        var questData =
            quest_option != null ? (GDictionary)quest_option.Duplicate(true) : new GDictionary();
        if (questData.Count == 0)
        {
            return new GDictionary();
        }
        foreach (
            string fieldName in new[]
            {
                "quest_id",
                "provider_interaction_id",
                "display_name",
                "description",
            }
        )
        {
            if (!questData.ContainsKey(fieldName) || !TryAsString(questData[fieldName], out string fieldValue))
            {
                return new GDictionary();
            }
            fieldValue = fieldValue.Trim();
            if (string.IsNullOrEmpty(fieldValue))
            {
                return new GDictionary();
            }
            questData[fieldName] = fieldValue;
        }
        GDictArray objectiveDefs = _normalize_contract_board_objective_defs(questData);
        if (objectiveDefs.Count == 0)
        {
            return new GDictionary();
        }
        GDictArray rewardEntries = _normalize_contract_board_reward_entries(questData);
        if (rewardEntries.Count == 0)
        {
            return new GDictionary();
        }
        questData["objective_defs"] = objectiveDefs;
        questData["reward_entries"] = rewardEntries;
        if (!TryReadContractBoardRepeatable(questData, out bool isRepeatable))
        {
            return new GDictionary();
        }
        questData["is_repeatable"] = isRepeatable;
        return questData;
    }

    private static bool IsContractBoardQuestRepeatable(GDictionary questData)
    {
        return TryReadContractBoardRepeatable(questData, out bool isRepeatable)
            && isRepeatable;
    }

    private static bool TryReadContractBoardRepeatable(
        GDictionary questData,
        out bool isRepeatable
    )
    {
        isRepeatable = false;
        return questData != null
            && questData.ContainsKey("is_repeatable")
            && TryAsBool(questData["is_repeatable"], out isRepeatable);
    }

    public GDictArray _normalize_contract_board_objective_defs(GDictionary quest_data)
    {
        var normalizedObjectives = new GDictArray();
        GArray objectiveDefs = ReadArray(quest_data, "objective_defs");
        if (objectiveDefs.Count == 0)
        {
            return normalizedObjectives;
        }
        var seenObjectiveIds = new GDictionary();
        foreach (object objectiveValue in objectiveDefs)
        {
            if (!TryAsDictionary(objectiveValue, out GDictionary sourceObjectiveData))
            {
                return new GDictArray();
            }
            GDictionary objectiveData = (GDictionary)sourceObjectiveData.Duplicate(true);
            StringName objectiveId = _read_contract_board_required_string_name(
                objectiveData,
                "objective_id"
            );
            if (objectiveId == "" || seenObjectiveIds.ContainsKey(objectiveId))
            {
                return new GDictArray();
            }
            seenObjectiveIds[objectiveId] = true;
            StringName objectiveType = _read_contract_board_required_string_name(
                objectiveData,
                "objective_type"
            );
            if (objectiveType == "")
            {
                return new GDictArray();
            }
            if (!objectiveData.ContainsKey("target_id"))
            {
                return new GDictArray();
            }
            if (
                !TryAsStringLike(objectiveData["target_id"], out StringName targetId)
            )
            {
                return new GDictArray();
            }
            if (
                !objectiveData.ContainsKey("target_value")
                || !TryAsInt(objectiveData["target_value"], out int targetValue)
            )
            {
                return new GDictArray();
            }
            if (targetValue <= 0)
            {
                return new GDictArray();
            }
            if (objectiveType == QuestDef.OBJECTIVE_SETTLEMENT_ACTION())
            {
                if (targetId == "")
                {
                    return new GDictArray();
                }
            }
            else if (objectiveType == QuestDef.OBJECTIVE_SUBMIT_ITEM())
            {
                if (targetId == "")
                {
                    return new GDictArray();
                }
            }
            else if (objectiveType == QuestDef.OBJECTIVE_DEFEAT_ENEMY())
            {
                // no extra validation
            }
            else
            {
                return new GDictArray();
            }
            objectiveData["objective_id"] = objectiveId.ToString();
            objectiveData["objective_type"] = objectiveType.ToString();
            objectiveData["target_id"] = targetId.ToString();
            objectiveData["target_value"] = targetValue;
            normalizedObjectives.Add(objectiveData);
        }
        return normalizedObjectives;
    }

    public GDictArray _normalize_contract_board_reward_entries(GDictionary quest_data)
    {
        var normalizedRewards = new GDictArray();
        GArray rewardEntries = ReadArray(quest_data, "reward_entries");
        if (rewardEntries.Count == 0)
        {
            return normalizedRewards;
        }
        foreach (object rewardValue in rewardEntries)
        {
            if (!TryAsDictionary(rewardValue, out GDictionary sourceRewardData))
            {
                return new GDictArray();
            }
            GDictionary rewardData = (GDictionary)sourceRewardData.Duplicate(true);
            StringName rewardType = _read_contract_board_required_string_name(
                rewardData,
                "reward_type"
            );
            if (rewardType == "")
            {
                return new GDictArray();
            }
            if (rewardType == QuestDef.REWARD_GOLD())
            {
                if (
                    !rewardData.ContainsKey("amount")
                    || !TryAsInt(rewardData["amount"], out int goldAmount)
                )
                {
                    return new GDictArray();
                }
                if (goldAmount <= 0)
                {
                    return new GDictArray();
                }
                rewardData["amount"] = goldAmount;
            }
            else if (rewardType == QuestDef.REWARD_ITEM())
            {
                StringName rewardItemId = _read_contract_board_required_string_name(
                    rewardData,
                    "item_id"
                );
                if (rewardItemId == "")
                {
                    return new GDictArray();
                }
                if (
                    !rewardData.ContainsKey("quantity")
                    || !TryAsInt(rewardData["quantity"], out int rewardQuantity)
                )
                {
                    return new GDictArray();
                }
                if (rewardQuantity <= 0)
                {
                    return new GDictArray();
                }
                rewardData["item_id"] = rewardItemId.ToString();
                rewardData["quantity"] = rewardQuantity;
            }
            else if (rewardType == QuestDef.REWARD_PENDING_CHARACTER_REWARD())
            {
                if (!_is_contract_board_pending_character_reward_valid(rewardData))
                {
                    return new GDictArray();
                }
            }
            else
            {
                return new GDictArray();
            }
            rewardData["reward_type"] = rewardType.ToString();
            normalizedRewards.Add(rewardData);
        }
        return normalizedRewards;
    }

    public bool _is_contract_board_pending_character_reward_valid(GDictionary reward_data)
    {
        if (_read_contract_board_required_string_name(reward_data, "member_id") == "")
        {
            return false;
        }
        GArray entries = ReadArray(reward_data, "entries");
        if (entries.Count == 0)
        {
            return false;
        }
        foreach (object entryValue in entries)
        {
            if (!TryAsDictionary(entryValue, out GDictionary entryData))
            {
                return false;
            }
            if (
                _read_contract_board_required_string_name(entryData, "entry_type") == ""
            )
            {
                return false;
            }
            if (
                _read_contract_board_required_string_name(entryData, "target_id") == ""
            )
            {
                return false;
            }
            if (
                !entryData.ContainsKey("amount")
                || !TryAsInt(entryData["amount"], out int amount)
                || amount == 0
            )
            {
                return false;
            }
        }
        return true;
    }

    public StringName _read_contract_board_required_string_name(GDictionary data, string field_name)
    {
        if (!data.ContainsKey(field_name))
        {
            return "";
        }
        if (!TryAsStringLike(data[field_name], out StringName value))
        {
            return "";
        }
        return value;
    }

    public bool _is_contract_board_completed_state(string state_id)
    {
        return state_id == "claimable" || state_id == "completed" || state_id == "repeatable";
    }

    public bool _is_forge_interaction(string interaction_script_id)
    {
        return _forge_service != null
            && _forge_service.is_supported_interaction(interaction_script_id);
    }

    public bool _is_research_interaction(string interaction_script_id)
    {
        return _research_service != null
            && _research_service.is_supported_interaction(interaction_script_id);
    }

    public string _build_forge_unavailable_reason(string interaction_script_id)
    {
        return interaction_script_id == "service_master_reforge"
            ? "当前没有可用重铸配方"
            : "当前没有可用锻造配方";
    }

    public string _resolve_forge_service_label(GDictionary payload)
    {
        string serviceType = ReadString(payload, "service_type").Trim();
        if (!string.IsNullOrEmpty(serviceType))
        {
            return serviceType;
        }
        return
            ReadString(payload, "interaction_script_id").Trim() == "service_master_reforge"
            ? "大师重铸"
            : "锻造";
    }

    public AttributeSnapshot _get_member_attribute_snapshot(StringName member_id)
    {
        return _has_runtime() ? Runtime.get_member_attribute_snapshot(member_id) : null;
    }

    public string _get_member_display_name(StringName member_id)
    {
        return Runtime?.get_member_display_name(member_id) ?? member_id.ToString();
    }

    public void _open_party_warehouse_window(string entry_label)
    {
        if (_has_runtime())
        {
            Runtime.open_party_warehouse_window(entry_label);
        }
    }

    public void _enqueue_pending_character_rewards(GArray reward_options)
    {
        if (_has_runtime())
        {
            Runtime.enqueue_pending_character_rewards(reward_options);
        }
    }

    public void _record_member_achievement_event(
        StringName member_id,
        StringName event_id,
        int value,
        StringName detail_id = null
    )
    {
        detail_id ??= new StringName("");
        if (_has_runtime())
        {
            Runtime.record_member_achievement_event(member_id, event_id, value, detail_id);
        }
    }

    public void _sync_party_state_from_character_management()
    {
        if (_has_runtime())
        {
            Runtime.sync_party_state_from_character_management();
        }
    }

    public int _persist_party_state()
    {
        return _has_runtime() ? Runtime.persist_party_state() : (int)Error.Unavailable;
    }

    public int _persist_world_data()
    {
        return _has_runtime() ? Runtime.persist_world_data() : (int)Error.Unavailable;
    }

    public int _persist_player_coord()
    {
        return _has_runtime() ? Runtime.persist_player_coord() : (int)Error.Unavailable;
    }

    public WorldMapFogSystem _get_fog_system()
    {
        return _has_runtime() ? Runtime.GetFogSystem() : null;
    }

    public bool _is_settlement_visible_to_player(GDictionary settlement)
    {
        WorldMapFogSystem fogSystem = _get_fog_system();
        if (fogSystem == null)
        {
            return false;
        }
        Vector2I origin = ReadVector2I(settlement, "origin");
        Vector2I footprintSize = ReadVector2I(settlement, "footprint_size", Vector2I.One);
        int width = Math.Max(footprintSize.X, 1);
        int height = Math.Max(footprintSize.Y, 1);
        string factionId = _get_player_faction_id();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (fogSystem.is_visible(origin + new Vector2I(x, y), factionId))
                {
                    return true;
                }
            }
        }
        return false;
    }

    public string _get_player_faction_id()
    {
        return Runtime?.get_player_faction_id() ?? "player";
    }

    public void _advance_world_time_by_steps(int delta_steps)
    {
        if (_has_runtime())
        {
            Runtime.advance_world_time_by_steps(delta_steps);
        }
    }

    public void _refresh_world_visibility()
    {
        if (_has_runtime())
        {
            Runtime.refresh_world_visibility();
        }
    }

    public int _get_world_step()
    {
        return _has_runtime() ? Runtime.get_world_step() : 0;
    }

    public void _set_player_coord(Vector2I coord)
    {
        if (_has_runtime())
        {
            Runtime.set_player_coord(coord);
        }
    }

    public void _set_selected_coord(Vector2I coord)
    {
        if (_has_runtime())
        {
            Runtime.set_selected_coord(coord);
        }
    }

    public void _clear_settlement_entry_context(bool reset_selected = true)
    {
        if (_has_runtime())
        {
            Runtime.clear_settlement_entry_context(reset_selected);
        }
    }

    public string _get_active_modal_id()
    {
        return Runtime?.get_active_modal_id() ?? "";
    }

    public void _set_active_modal_id(string modal_id)
    {
        if (_has_runtime())
        {
            Runtime.set_runtime_active_modal_id(modal_id);
        }
    }

    public bool _present_pending_reward_if_ready()
    {
        return _has_runtime() && Runtime.present_pending_reward_if_ready();
    }

    public void _set_active_shop_context(GDictionary context)
    {
        if (_has_runtime())
        {
            Runtime.set_active_shop_context(context);
        }
    }

    public void _set_active_contract_board_context(GDictionary context)
    {
        if (_has_runtime())
        {
            Runtime.set_active_contract_board_context(context);
        }
    }

    public void _set_active_forge_context(GDictionary context)
    {
        if (_has_runtime())
        {
            Runtime.set_active_forge_context(context);
        }
    }

    public void _clear_active_shop_context()
    {
        if (_has_runtime())
        {
            Runtime.clear_active_shop_context();
        }
    }

    public void _clear_active_contract_board_context()
    {
        if (_has_runtime())
        {
            Runtime.clear_active_contract_board_context();
        }
    }

    public void _clear_active_forge_context()
    {
        if (_has_runtime())
        {
            Runtime.clear_active_forge_context();
        }
    }

    public GDictionary _get_active_shop_context()
    {
        return _has_runtime() ? Runtime.get_active_shop_context() : new GDictionary();
    }

    public GDictionary _get_active_contract_board_context()
    {
        return _has_runtime() ? Runtime.get_active_contract_board_context() : new GDictionary();
    }

    public GDictionary _get_active_forge_context()
    {
        return _has_runtime() ? Runtime.get_active_forge_context() : new GDictionary();
    }

    public void _set_active_stagecoach_context(GDictionary context)
    {
        if (_has_runtime())
        {
            Runtime.set_active_stagecoach_context(context);
        }
    }

    public void _clear_active_stagecoach_context()
    {
        if (_has_runtime())
        {
            Runtime.clear_active_stagecoach_context();
        }
    }

    public GDictionary _get_active_stagecoach_context()
    {
        return _has_runtime() ? Runtime.get_active_stagecoach_context() : new GDictionary();
    }

    private static IEnumerable<GDictionary> Dictionaries(GArray values)
    {
        if (values == null)
        {
            yield break;
        }
        foreach (object rawValue in values)
        {
            if (TryAsDictionary(rawValue, out GDictionary value))
            {
                yield return value;
            }
        }
    }

    private static bool TryAsArray(object rawValue, out GArray value)
    {
        if (rawValue is GArray array)
        {
            value = array;
            return true;
        }
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Array)
        {
            value = variant.AsGodotArray();
            return true;
        }
        value = new GArray();
        return false;
    }

    private static bool TryAsDictionary(object rawValue, out GDictionary value)
    {
        if (rawValue is GDictionary dictionary)
        {
            value = dictionary;
            return true;
        }
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Dictionary)
        {
            value = variant.AsGodotDictionary();
            return true;
        }
        value = new GDictionary();
        return false;
    }

    private static bool TryAsObject<T>(object rawValue, out T value)
        where T : class
    {
        if (rawValue is T typedValue)
        {
            value = typedValue;
            return true;
        }
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Object)
        {
            value = variant.AsGodotObject() as T;
            return value != null;
        }
        value = null;
        return false;
    }

    private static bool TryAsString(object rawValue, out string value)
    {
        if (rawValue is string stringValue)
        {
            value = stringValue;
            return true;
        }
        if (rawValue is StringName stringName)
        {
            value = stringName.ToString();
            return true;
        }
        if (rawValue is Variant variant && variant.TryAsString(out value))
            return true;
        value = "";
        return false;
    }

    private static string ReadString(GDictionary data, string key, string fallback = "")
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
            return fallback;
        return TryAsString(data[key], out string value) ? value : fallback;
    }

    private static Variant ReadVariant(GDictionary data, string key)
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
            return default;
        return data[key];
    }

    private static bool ReadBool(GDictionary data, string key, bool fallback = false)
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
            return fallback;
        object rawValue = data[key];
        if (rawValue is bool boolValue)
            return boolValue;
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Bool)
            return variant.AsBool();
        return fallback;
    }

    private static int ReadInt(GDictionary data, string key, int fallback = 0)
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
            return fallback;
        return TryAsInt(data[key], out int value) ? value : fallback;
    }

    private static GArray ReadArray(GDictionary data, string key)
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
            return new GArray();
        return TryAsArray(data[key], out GArray value) ? value : new GArray();
    }

    private static GDictionary ReadDictionary(GDictionary data, string key)
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
            return new GDictionary();
        return TryAsDictionary(data[key], out GDictionary value) ? value : new GDictionary();
    }

    private static Vector2I ReadVector2I(
        GDictionary data,
        string key,
        Vector2I fallback = default
    )
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
            return fallback;
        object rawValue = data[key];
        if (rawValue is Vector2I vector)
            return vector;
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Vector2I)
            return variant.AsVector2I();
        return fallback;
    }

    private static bool TryAsStringName(object rawValue, out StringName value)
    {
        if (rawValue is StringName stringName)
        {
            value = stringName;
            return true;
        }
        if (rawValue is string stringValue)
        {
            value = new StringName(stringValue);
            return true;
        }
        if (rawValue is Variant variant && variant.TryAsStringName(out value))
            return true;
        value = "";
        return false;
    }

    private static StringName ReadStringName(GDictionary data, string key)
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
            return "";
        return TryAsStringName(data[key], out StringName value) ? value : "";
    }

    private static bool TryAsStrictStringNameKey(object rawValue, out StringName value)
    {
        if (rawValue is StringName stringName)
        {
            value = stringName;
            return true;
        }
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.StringName)
        {
            value = variant.AsStringName();
            return true;
        }
        value = "";
        return false;
    }

    private static bool TryAsStringLike(object rawValue, out StringName value)
    {
        if (TryAsStringName(rawValue, out value))
        {
            return true;
        }
        value = "";
        return false;
    }

    private static bool TryAsInt(object rawValue, out int value)
    {
        if (rawValue is int intValue)
        {
            value = intValue;
            return true;
        }
        if (rawValue is long longValue)
        {
            value = (int)longValue;
            return true;
        }
        if (rawValue is Variant variant && variant.TryAsInt(out value))
            return true;
        value = 0;
        return false;
    }

    private static bool TryAsBool(object rawValue, out bool value)
    {
        if (rawValue is bool boolValue)
        {
            value = boolValue;
            return true;
        }
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Bool)
        {
            value = variant.AsBool();
            return true;
        }
        value = false;
        return false;
    }

    private static GArray ToUntypedDictArray(IEnumerable<GDictionary> src)
    {
        var result = new GArray();
        if (src == null)
            return result;
        foreach (GDictionary d in src)
        {
            result.Add(d);
        }
        return result;
    }

    private static GameRuntimeFacade ResolveWeakRef(WeakReference<GameRuntimeFacade> weakRef)
    {
        if (
            weakRef == null
            || !weakRef.TryGetTarget(out GameRuntimeFacade target)
            || !GodotObject.IsInstanceValid(target)
        )
        {
            return null;
        }
        return target;
    }
}
