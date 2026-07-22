using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GArray = Godot.Collections.Array;
using GDictArray = Godot.Collections.Array<Godot.Collections.Dictionary>;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

internal sealed class StagecoachDestinationData
{
    public string SettlementId { get; }
    public string DisplayName { get; }
    public string TierName { get; }
    public int TravelCost { get; }
    public bool CanTravel { get; }
    public string DisabledReason { get; }
    public Vector2I Coord { get; }
    public string InteractionScriptId { get; }

    internal StagecoachDestinationData(
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

}

internal sealed class GameRuntimeServiceWindowCommandHandler
{
    private GameRuntimeSettlementCommandHandler _owner;
    private GameRuntimeSettlementWindowDataBuilder _windowDataBuilder;
    private GameRuntimeContractBoardCommandHandler _contractBoardHandler;

    internal void Setup(
        GameRuntimeSettlementCommandHandler owner,
        GameRuntimeSettlementWindowDataBuilder windowDataBuilder,
        GameRuntimeContractBoardCommandHandler contractBoardHandler
    )
    {
        _owner = owner;
        _windowDataBuilder = windowDataBuilder;
        _contractBoardHandler = contractBoardHandler;
    }


    internal IReadOnlyDictionary<string, object> GetShopWindowDataSnapshotPlain()
    {
        Dictionary<string, object> context = _windowDataBuilder.CloneActiveShopContextPlain();
        if (context.Count == 0)
            return context;

        var entries = new List<object>();
        GameRuntimeSettlementWindowDataBuilder.AppendWindowEntriesPlain(entries, context, "buy_entries");
        GameRuntimeSettlementWindowDataBuilder.AppendWindowEntriesPlain(entries, context, "sell_entries");
        context["entries"] = entries;
        context["summary_text"] = $"持有金币：{GameRuntimeSettlementCommandHandler.ReadPlainInt(context, "gold")}";
        context["state_summary_text"] = GameRuntimeSettlementCommandHandler.ReadPlainString(context, "feedback_text");
        context["action_id"] = "shop:trade";
        context["panel_kind"] = SettlementPanelKinds.ToPayloadValue(
            SettlementPanelKind.Shop
        );
        context["show_member_selector"] = true;
        context.Remove("party_state");
        context["member_options"] = _windowDataBuilder.BuildMemberOptionsSnapshotPlain();
        context["default_member_id"] = _owner.ResolveDefaultSettlementMemberId().ToString();
        return context;
    }

    internal IReadOnlyDictionary<string, object> GetForgeWindowDataSnapshotPlain()
    {
        Dictionary<string, object> context = _windowDataBuilder.CloneActiveForgeContextPlain();
        if (context.Count == 0)
        {
            Dictionary<string, object> shopContext = _windowDataBuilder.CloneActiveShopContextPlain();
            if (GameRuntimeSettlementWindowDataBuilder.WindowDataMatchesPanelKindPlain(shopContext, SettlementPanelKind.Forge))
                context = shopContext;
        }
        context.Remove("party_state");
        return context;
    }

    internal IReadOnlyDictionary<string, object> GetStagecoachWindowDataSnapshotPlain()
    {
        Dictionary<string, object> context = _windowDataBuilder.CloneActiveStagecoachContextPlain();
        if (context.Count == 0)
            return context;

        var entries = new List<object>();
        GameRuntimeSettlementWindowDataBuilder.AppendWindowEntriesPlain(entries, context, "destinations");
        int gold = GameRuntimeSettlementCommandHandler.ReadPlainInt(context, "gold");
        context["entries"] = entries;
        context["summary_text"] = $"持有金币：{gold}";
        context["state_summary_text"] = GameRuntimeSettlementCommandHandler.ReadPlainString(context, "feedback_text");
        context["action_id"] = "stagecoach:travel";
        context["panel_kind"] = SettlementPanelKinds.ToPayloadValue(
            SettlementPanelKind.Stagecoach
        );
        context["meta"] =
            $"驿站：{GameRuntimeSettlementCommandHandler.ReadPlainString(context, "origin_name")}  |  金币：{gold}";
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
        context.Remove("party_state");
        context["member_options"] = _windowDataBuilder.BuildMemberOptionsSnapshotPlain();
        context["default_member_id"] = _owner.ResolveDefaultSettlementMemberId().ToString();
        return context;
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandShopBuyTyped(
        StringName item_id,
        int quantity
    )
    {
        if (_owner.GetActiveModalKind() != RuntimeModalKind.Shop)
        {
            return _owner.RuntimeCommandError("当前没有打开据点商店。");
        }
        using GodotProjectionLease<GDictionary> contextLease = _owner.GetActiveShopContextLease();
        GDictionary context = contextLease.Value;
        if (context.Count == 0)
        {
            return _owner.RuntimeCommandError("当前商店上下文缺失。");
        }
        string settlementId = GameRuntimeSettlementCommandHandler.ReadString(context, "settlement_id");
        RuntimeTransaction transaction = new RuntimeTransaction()
            .MarkPartyChanged()
            .MarkWorldChanged();
        GameRuntimeSettlementCommandHandler.SettlementCommandRollbackSnapshot rollbackSnapshot =
            _owner.CaptureRollbackSnapshot(transaction);
        WorldMapSettlementStateData settlementState =
            _owner.GetSettlementStateData(settlementId);
        if (settlementState == null)
            return _owner.RuntimeCommandError("当前据点状态无效。");
        SettlementShopTradeResult result = _owner._shop_service.BuyTyped(
            GameRuntimeSettlementCommandHandler.ReadString(context, "interaction_script_id"),
            settlementState,
            _owner.GetWorldStep(),
            _owner._GetItemDefsTyped(),
            _owner.GetPartyWarehouseService(),
            _owner.GetPartyState(),
            item_id,
            quantity
        );
        if (!result.Success)
        {
            string failureMessage = string.IsNullOrEmpty(result.Message)
                ? "购买失败。"
                : result.Message;
            _owner.SetSettlementFeedbackText(failureMessage);
            _refresh_active_shop_context(failureMessage);
            return _owner.RuntimeCommandError(failureMessage);
        }
        if (
            result.UpdatedSettlementState == null
            || !_owner.SetActiveSettlementState(settlementId, result.UpdatedSettlementState)
        )
        {
            _owner.RestoreRollbackSnapshotForFailure(rollbackSnapshot, transaction);
            return _owner.RuntimeCommandError("购买失败：无法写回据点状态。");
        }
        GameRuntimeSettlementCommandHandler.SettlementPersistResult persistResult = _owner.PersistChangesTyped(
            true,
            true,
            false,
            rollbackSnapshot
        );
        string message = string.IsNullOrEmpty(result.Message) ? "购买成功。" : result.Message;
        if (!persistResult.Ok)
            return _owner.RuntimeCommandPersistFailure();
        _owner.SetSettlementFeedbackText(message);
        _refresh_active_shop_context(message);
        _owner.UpdateStatus(message);
        return _owner.RuntimeCommandOk(message);
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandShopSellTyped(
        StringName item_id,
        int quantity,
        StringName instance_id = null
    )
    {
        instance_id ??= new StringName("");
        if (_owner.GetActiveModalKind() != RuntimeModalKind.Shop)
        {
            return _owner.RuntimeCommandError("当前没有打开据点商店。");
        }
        using GodotProjectionLease<GDictionary> contextLease = _owner.GetActiveShopContextLease();
        GDictionary context = contextLease.Value;
        if (context.Count == 0)
        {
            return _owner.RuntimeCommandError("当前商店上下文缺失。");
        }
        RuntimeTransaction transaction = new RuntimeTransaction().MarkPartyChanged();
        GameRuntimeSettlementCommandHandler.SettlementCommandRollbackSnapshot rollbackSnapshot =
            _owner.CaptureRollbackSnapshot(transaction);
        SettlementShopTradeResult result = _owner._shop_service.SellTyped(
            GameRuntimeSettlementCommandHandler.ReadString(context, "interaction_script_id"),
            _owner._GetItemDefsTyped(),
            _owner.GetPartyWarehouseService(),
            _owner.GetPartyState(),
            item_id,
            quantity,
            instance_id
        );
        if (!result.Success)
        {
            string failureMessage = string.IsNullOrEmpty(result.Message)
                ? "出售失败。"
                : result.Message;
            _owner.SetSettlementFeedbackText(failureMessage);
            _refresh_active_shop_context(failureMessage);
            return _owner.RuntimeCommandError(failureMessage);
        }
        GameRuntimeSettlementCommandHandler.SettlementPersistResult persistResult = _owner.PersistChangesTyped(
            true,
            false,
            false,
            rollbackSnapshot
        );
        string message = string.IsNullOrEmpty(result.Message) ? "出售成功。" : result.Message;
        if (!persistResult.Ok)
            return _owner.RuntimeCommandPersistFailure();
        _owner.SetSettlementFeedbackText(message);
        _refresh_active_shop_context(message);
        _owner.UpdateStatus(message);
        return _owner.RuntimeCommandOk(message);
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandStagecoachTravelTyped(
        string settlement_id
    )
    {
        if (_owner.GetActiveModalKind() != RuntimeModalKind.Stagecoach)
        {
            return _owner.RuntimeCommandError("当前没有打开驿站路线窗口。");
        }
        using GodotProjectionLease<GDictionary> contextLease =
            _owner.GetActiveStagecoachContextLease();
        GDictionary context = contextLease.Value;
        if (context.Count == 0)
        {
            return _owner.RuntimeCommandError("当前没有可用的驿站路线。");
        }
        StagecoachDestinationData destination = ResolveStagecoachDestinationTyped(
            context,
            settlement_id
        );
        if (destination == null)
        {
            return _owner.RuntimeCommandError("当前驿站路线中不存在该目的地。");
        }
        if (!destination.CanTravel)
        {
            return _owner.RuntimeCommandError(
                !string.IsNullOrEmpty(destination.DisabledReason)
                    ? destination.DisabledReason
                    : "当前无法前往该据点。"
            );
        }
        PartyState partyState = _owner.GetPartyState();
        if (partyState == null)
        {
            return _owner.RuntimeCommandError("当前不存在队伍数据。");
        }
        GameRuntimeSettlementCommandHandler.SettlementCommandRollbackSnapshot rollbackSnapshot = _owner.CaptureRollbackSnapshot(
            new RuntimeTransaction()
                .MarkPartyChanged()
                .MarkWorldChanged()
                .MarkPlayerCoordChanged()
        );
        int travelCost = destination.TravelCost;
        if (!partyState.SpendGold(travelCost))
        {
            return _owner.RuntimeCommandError("金币不足，无法启程。");
        }
        string destinationId = destination.SettlementId;
        using GodotProjectionLease<GDictionary> destinationRecordLease =
            _owner.GetSettlementRecordLease(destinationId);
        GDictionary destinationRecord = destinationRecordLease.Value;
        if (destinationRecord.Count == 0)
        {
            return _owner.RuntimeCommandError("未找到目标据点。");
        }
        Vector2I destinationCoord = destination.Coord;
        _owner.ClearSettlementEntryContext(false);
        _owner.SetPlayerCoord(destinationCoord);
        _owner.SetSelectedCoord(destinationCoord);
        _owner._mark_settlement_visited(destinationId);
        _owner.ClearActiveStagecoachContext();
        _owner.SetActiveModalKind(RuntimeModalKind.Settlement);
        _owner.SetActiveSettlementId(destinationId);
        _owner.SetSettlementFeedbackText(
            $"驿队将你送到了 {GameRuntimeSettlementCommandHandler.ReadString(destinationRecord, "display_name", destinationId)}。"
        );
        _owner.RefreshWorldVisibility();
        GameRuntimeSettlementCommandHandler.SettlementPersistResult persistResult = _owner.PersistChangesTyped(
            true,
            true,
            true,
            rollbackSnapshot
        );
        string message =
            $"已从 {GameRuntimeSettlementCommandHandler.ReadString(context, "origin_name", "当前据点")} 抵达 {GameRuntimeSettlementCommandHandler.ReadString(destinationRecord, "display_name", destinationId)}，花费 {travelCost} 金。";
        if (!persistResult.Ok)
            return _owner.RuntimeCommandPersistFailure();
        _owner.UpdateStatus(message);
        return _owner.RuntimeCommandOk(message);
    }

    internal GameRuntimeFacade.RuntimeCommandResult OpenShopModalTyped(
        string settlementId,
        GDictionary payload
    )
    {
        WorldMapSettlementStateData settlementState =
            _owner.GetSettlementStateData(settlementId);
        if (settlementState == null)
        {
            return _owner.RuntimeCommandError("无法打开商店：据点状态无效。");
        }
        using GodotProjectionLease<GDictionary> settlementLease =
            _owner.GetSettlementRecordLease(settlementId);
        SettlementShopWindowBuildResult buildResult =
            _owner._shop_service.BuildWindowDataTyped(
                GameRuntimeSettlementCommandHandler.ReadString(
                    payload,
                    "interaction_script_id"
                ),
                settlementLease.Value,
                settlementState,
                _owner.GetWorldStep(),
                "",
                _owner._GetItemDefsTyped(),
                _owner.GetPartyWarehouseService(),
                _owner.GetPartyGold(),
                _owner._GetTraitDefsTyped()
            );
        if (buildResult.WindowDataPlain.Count == 0)
            return _owner.RuntimeCommandError("无法打开商店：商店配置无效。");
        using GodotProjectionLease<GDictionary> windowDataLease =
            buildResult.ProjectWindowDataLease(
                "GameRuntimeServiceWindowCommandHandler.OpenShopModalTyped"
            );
        GDictionary windowData = windowDataLease.Value;
        if (
            buildResult.StateChanged
            && !_owner.SetActiveSettlementState(
                settlementId,
                buildResult.UpdatedSettlementState
            )
        )
        {
            return _owner.RuntimeCommandError("无法打开商店：据点状态写回失败。");
        }
        windowData["settlement_id"] = settlementId;
        windowData["interaction_script_id"] = GameRuntimeSettlementCommandHandler.ReadString(payload, "interaction_script_id");
        _owner.SetActiveShopContext(windowData);
        _owner.SetActiveModalKind(RuntimeModalKind.Shop);
        string message =
            $"已打开 {GameRuntimeSettlementCommandHandler.ReadString(payload, "facility_name", "据点商店")} 的商店。";
        _owner.UpdateStatus(message);
        return _owner.RuntimeCommandOk(message);
    }

    internal void _open_forge_modal(string settlement_id, GDictionary payload)
    {
        using GodotProjectionLease<GDictionary> settlementLease =
            _owner.GetSettlementRecordLease(settlement_id);
        GDictionary windowData = _owner._forge_service.BuildWindowDataTyped(
            GameRuntimeSettlementCommandHandler.ReadString(payload, "interaction_script_id"),
            settlementLease.Value,
            payload,
            _owner._GetItemDefsTyped(),
            _owner.GetRecipeDefsTyped(),
            _owner.GetPartyWarehouseService()
        );
        windowData["settlement_id"] = settlement_id;
        windowData["interaction_script_id"] = GameRuntimeSettlementCommandHandler.ReadString(payload, "interaction_script_id");
        windowData["service_payload"] = payload;
        windowData["member_options"] = _windowDataBuilder._build_member_options();
        StringName selectedMemberId = GameRuntimeSettlementCommandHandler.ReadStringName(payload, "member_id");
        if (selectedMemberId == "")
        {
            selectedMemberId = _owner.ResolveDefaultSettlementMemberId();
        }
        windowData["default_member_id"] = selectedMemberId.ToString();
        windowData["selected_member_id"] = selectedMemberId.ToString();
        _owner.SetActiveForgeContext(windowData);
        _owner.SetActiveModalKind(RuntimeModalKind.Forge);
        _owner.UpdateStatus(
            $"已打开 {GameRuntimeSettlementCommandHandler.ReadString(payload, "facility_name", "据点工坊")} 的{_resolve_forge_service_label(payload)}窗口。"
        );
    }

    internal void _open_stagecoach_modal(string settlement_id, GDictionary payload)
    {
        using GodotProjectionLease<GDictionary> settlementLease =
            _owner.GetSettlementRecordLease(settlement_id);
        GDictionary settlement = settlementLease.Value;
        _owner.SetActiveStagecoachContext(
            new GDictionary
            {
                ["title"] = $"{GameRuntimeSettlementCommandHandler.ReadString(settlement, "display_name", "据点")} · 驿站路线",
                ["settlement_id"] = settlement_id,
                ["origin_name"] = GameRuntimeSettlementCommandHandler.ReadString(settlement, "display_name", "据点"),
                ["interaction_script_id"] = GameRuntimeSettlementCommandHandler.ReadString(payload, "interaction_script_id"),
                ["gold"] = _owner.GetPartyGold(),
                ["destinations"] = _build_stagecoach_destinations(
                    settlement,
                    GameRuntimeSettlementCommandHandler.ReadString(payload, "interaction_script_id")
                ),
                ["feedback_text"] = "选择一个已访问据点并支付路费后即可启程。",
            }
        );
        _owner.SetActiveModalKind(RuntimeModalKind.Stagecoach);
        _owner.UpdateStatus("已打开驿站路线。");
    }

    private void _refresh_active_shop_context(string feedbackText = null)
    {
        using GodotProjectionLease<GDictionary> contextLease = _owner.GetActiveShopContextLease();
        GDictionary context = contextLease.Value;
        if (context.Count == 0)
        {
            return;
        }
        string settlementId = GameRuntimeSettlementCommandHandler.ReadString(context, "settlement_id");
        WorldMapSettlementStateData settlementState =
            _owner.GetSettlementStateData(settlementId);
        if (settlementState == null)
            return;
        using GodotProjectionLease<GDictionary> settlementLease =
            _owner.GetSettlementRecordLease(settlementId);
        string nextFeedback = feedbackText
            ?? GameRuntimeSettlementCommandHandler.ReadString(context, "feedback_text");
        SettlementShopWindowBuildResult buildResult =
            _owner._shop_service.BuildWindowDataTyped(
                GameRuntimeSettlementCommandHandler.ReadString(
                    context,
                    "interaction_script_id"
                ),
                settlementLease.Value,
                settlementState,
                _owner.GetWorldStep(),
                nextFeedback,
                _owner._GetItemDefsTyped(),
                _owner.GetPartyWarehouseService(),
                _owner.GetPartyGold(),
                _owner._GetTraitDefsTyped()
            );
        if (buildResult.WindowDataPlain.Count == 0)
            return;
        using GodotProjectionLease<GDictionary> nextContextLease =
            buildResult.ProjectWindowDataLease(
                "GameRuntimeServiceWindowCommandHandler.refresh_active_shop_context"
            );
        GDictionary nextContext = nextContextLease.Value;
        if (
            buildResult.StateChanged
            && !_owner.SetActiveSettlementState(
                settlementId,
                buildResult.UpdatedSettlementState
            )
        )
        {
            return;
        }
        nextContext["settlement_id"] = settlementId;
        nextContext["interaction_script_id"] = GameRuntimeSettlementCommandHandler.ReadString(context, "interaction_script_id");
        _owner.SetActiveShopContext(nextContext);
    }

    internal void _refresh_active_forge_context(string feedback_text = "")
    {
        using GodotProjectionLease<GDictionary> contextLease = _owner.GetActiveForgeContextLease();
        GDictionary context = contextLease.Value;
        if (context.Count == 0)
        {
            return;
        }
        string settlementId = GameRuntimeSettlementCommandHandler.ReadString(context, "settlement_id");
        GDictionary servicePayload = GameRuntimeSettlementCommandHandler.ReadDictionary(context, "service_payload");
        string interactionScriptId = GameRuntimeSettlementCommandHandler.ReadString(context, "interaction_script_id");
        if (string.IsNullOrEmpty(interactionScriptId))
        {
            interactionScriptId = GameRuntimeSettlementCommandHandler.ReadString(servicePayload, "interaction_script_id");
        }
        using GodotProjectionLease<GDictionary> settlementLease =
            _owner.GetSettlementRecordLease(settlementId);
        GDictionary nextContext = _owner._forge_service.BuildWindowDataTyped(
            interactionScriptId,
            settlementLease.Value,
            servicePayload,
            _owner._GetItemDefsTyped(),
            _owner.GetRecipeDefsTyped(),
            _owner.GetPartyWarehouseService(),
            !string.IsNullOrEmpty(feedback_text)
                ? feedback_text
                : GameRuntimeSettlementCommandHandler.ReadString(context, "feedback_text")
        );
        nextContext["settlement_id"] = settlementId;
        nextContext["interaction_script_id"] = interactionScriptId;
        nextContext["service_payload"] = servicePayload;
        nextContext["member_options"] = GameRuntimeSettlementCommandHandler.ReadArray(context, "member_options");
        string defaultMemberId = GameRuntimeSettlementCommandHandler.ReadString(context, "default_member_id");
        if (string.IsNullOrEmpty(defaultMemberId))
        {
            defaultMemberId = GameRuntimeSettlementCommandHandler.ReadString(servicePayload, "member_id");
        }
        nextContext["default_member_id"] = defaultMemberId;
        string selectedMemberId = GameRuntimeSettlementCommandHandler.ReadString(context, "selected_member_id");
        if (string.IsNullOrEmpty(selectedMemberId))
        {
            selectedMemberId = defaultMemberId;
        }
        nextContext["selected_member_id"] = selectedMemberId;
        _owner.SetActiveForgeContext(nextContext);
    }

    private GDictArray _build_stagecoach_destinations(
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
            entries.Add(ProjectStagecoachDestination(destination));
        }
        return entries;
    }

    private static GDictionary ProjectStagecoachDestination(
        StagecoachDestinationData destination
    )
    {
        if (destination == null)
            return new GDictionary();

        return new GDictionary
        {
            ["settlement_id"] = destination.SettlementId,
            ["entry_id"] = $"travel:{destination.SettlementId}",
            ["display_name"] = destination.DisplayName,
            ["tier_name"] = destination.TierName,
            ["travel_cost"] = destination.TravelCost,
            ["can_travel"] = destination.CanTravel,
            ["state_label"] = destination.CanTravel ? "状态：可出发" : "状态：不可出发",
            ["cost_label"] = $"路费 {destination.TravelCost} 金",
            ["summary_text"] = destination.TierName,
            ["details_text"] = $"{destination.TierName} {destination.DisabledReason}",
            ["is_enabled"] = destination.CanTravel,
            ["target_settlement_id"] = destination.SettlementId,
            ["disabled_reason"] = destination.DisabledReason,
            ["coord"] = new GDictionary { ["x"] = destination.Coord.X, ["y"] = destination.Coord.Y },
            ["interaction_script_id"] = destination.InteractionScriptId,
        };
    }

    internal List<StagecoachDestinationData> BuildStagecoachDestinationData(
        GDictionary origin_settlement,
        string interaction_script_id
    )
    {
        var entries = new List<StagecoachDestinationData>();
        string originSettlementId = GameRuntimeSettlementCommandHandler.ReadString(origin_settlement, "settlement_id");
        Vector2I originCoord = GameRuntimeSettlementCommandHandler.ReadVector2I(origin_settlement, "origin");
        using GodotProjectionLease<GArray> settlementsLease = _owner.GetAllSettlementRecordsLease();
        foreach (GDictionary settlement in GameRuntimeSettlementCommandHandler.Dictionaries(settlementsLease.Value))
        {
            string settlementId = GameRuntimeSettlementCommandHandler.ReadString(settlement, "settlement_id");
            if (string.IsNullOrEmpty(settlementId) || settlementId == originSettlementId)
            {
                continue;
            }
            if (!_owner.IsSettlementVisited(settlementId))
            {
                continue;
            }
            Vector2I targetCoord = GameRuntimeSettlementCommandHandler.ReadVector2I(settlement, "origin");
            int travelCost =
                (Math.Abs(targetCoord.X - originCoord.X) + Math.Abs(targetCoord.Y - originCoord.Y))
                * GameRuntimeSettlementCommandHandler.STAGECOACH_COST_PER_STEP;
            bool canTravel = _owner.GetPartyGold() >= travelCost;
            entries.Add(
                new StagecoachDestinationData(
                    settlementId,
                    GameRuntimeSettlementCommandHandler.ReadString(settlement, "display_name", settlementId),
                    GameRuntimeSettlementCommandHandler.ReadString(settlement, "tier_name"),
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

    private GDictionary _find_stagecoach_destination(
        GDictionary stagecoach_context,
        string settlement_id
    )
    {
        foreach (
            GDictionary destination in GameRuntimeSettlementCommandHandler.Dictionaries(
                GameRuntimeSettlementCommandHandler.ReadArray(stagecoach_context, "destinations")
            )
        )
        {
            if (GameRuntimeSettlementCommandHandler.ReadString(destination, "settlement_id") == settlement_id)
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
        string originSettlementId = GameRuntimeSettlementCommandHandler.ReadString(stagecoach_context, "settlement_id");
        if (string.IsNullOrEmpty(originSettlementId) || string.IsNullOrEmpty(settlement_id))
        {
            return null;
        }
        using GodotProjectionLease<GDictionary> originSettlementLease =
            _owner.GetSettlementRecordLease(originSettlementId);
        GDictionary originSettlement = originSettlementLease.Value;
        if (originSettlement.Count == 0)
        {
            return null;
        }
        string interactionScriptId = GameRuntimeSettlementCommandHandler.ReadString(stagecoach_context, "interaction_script_id");
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

    internal void _notify_misfortune_guidance_of_forge_result(
        StringName member_id,
        SettlementServiceResult result
    )
    {
        if (member_id == "" || _owner.Runtime == null || result == null)
        {
            return;
        }
        GDictionary inventoryDelta = SettlementServiceResultProjection.ProjectInventoryDelta(result);
        if (GameRuntimeSettlementCommandHandler.ReadStringName(inventoryDelta, "recipe_id") == "")
        {
            return;
        }
        _owner.Runtime?.HandleMisfortuneForgeResult(member_id, result);
    }

    internal bool _is_forge_modal_submission(GDictionary payload)
    {
        return GameRuntimeSettlementCommandHandler.ReadSubmissionSource(payload) == SettlementSubmissionSource.Forge;
    }

    internal bool _is_forge_interaction(string interaction_script_id)
    {
        return _owner._forge_service != null
            && _owner._forge_service.IsSupportedInteraction(interaction_script_id);
    }

    internal bool _is_research_interaction(string interaction_script_id)
    {
        return _owner._research_service != null
            && _owner._research_service.IsSupportedInteraction(interaction_script_id);
    }

    internal string _build_forge_unavailable_reason(string interaction_script_id)
    {
        return interaction_script_id == "service_master_reforge"
            ? "当前没有可用重铸配方"
            : "当前没有可用锻造配方";
    }

    internal string _resolve_forge_service_label(GDictionary payload)
    {
        string serviceType = GameRuntimeSettlementCommandHandler.ReadString(payload, "service_type").Trim();
        if (!string.IsNullOrEmpty(serviceType))
        {
            return serviceType;
        }
        return
            GameRuntimeSettlementCommandHandler.ReadString(payload, "interaction_script_id").Trim() == "service_master_reforge"
            ? "大师重铸"
            : "锻造";
    }
}
