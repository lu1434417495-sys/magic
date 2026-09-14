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
    internal const string StagecoachActionId = "stagecoach:travel";

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


    internal SettlementServiceWindowData GetShopWindowDataTyped() =>
        _owner.GetActiveShopContextTyped();

    internal IReadOnlyDictionary<string, object> GetShopWindowDataSnapshotPlain() =>
        GetShopWindowDataTyped().BuildSnapshotPlain();

    internal SettlementServiceWindowData GetForgeWindowDataTyped()
    {
        SettlementServiceWindowData context = _owner.GetActiveForgeContextTyped();
        if (context.IsValid)
            return context;
        SettlementServiceWindowData shopContext = _owner.GetActiveShopContextTyped();
        return shopContext.PanelKind == SettlementPanelKind.Forge
            ? shopContext
            : SettlementServiceWindowData.Empty;
    }

    internal IReadOnlyDictionary<string, object> GetForgeWindowDataSnapshotPlain() =>
        GetForgeWindowDataTyped().BuildSnapshotPlain();

    internal SettlementServiceWindowData GetStagecoachWindowDataTyped() =>
        _owner.GetActiveStagecoachContextTyped();

    internal IReadOnlyDictionary<string, object> GetStagecoachWindowDataSnapshotPlain() =>
        GetStagecoachWindowDataTyped().BuildSnapshotPlain();

    internal RuntimeCommandResult CommandShopBuyTyped(
        StringName item_id,
        int quantity
    )
    {
        if (_owner.GetActiveModalKind() != RuntimeModalKind.Shop)
        {
            return _owner.RuntimeCommandError("当前没有打开据点商店。");
        }
        SettlementServiceWindowData context = _owner.GetActiveShopContextTyped();
        if (!context.IsValid)
        {
            return _owner.RuntimeCommandError("当前商店上下文缺失。");
        }
        string settlementId = context.SettlementId.ToString();
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
            context.InteractionScriptId.ToString(),
            settlementState,
            _owner.GetWorldStep(),
            _owner._GetItemDefsTyped(),
            _owner.GetPartyWarehouseService(),
            _owner.GetPartyState(),
            item_id,
            quantity,
            _owner.GetUniqueEquipmentPoolState(),
            settlementId
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

    internal RuntimeCommandResult CommandShopSellTyped(
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
        SettlementServiceWindowData context = _owner.GetActiveShopContextTyped();
        if (!context.IsValid)
        {
            return _owner.RuntimeCommandError("当前商店上下文缺失。");
        }
        string settlementId = context.SettlementId.ToString();
        WorldUniqueEquipmentPoolState uniqueEquipmentPool =
            _owner.GetUniqueEquipmentPoolState();
        bool transferUniqueInstanceToShop =
            uniqueEquipmentPool != null
            && _owner.IsUniqueWorldEquipmentItem(item_id);
        RuntimeTransaction transaction = new RuntimeTransaction().MarkPartyChanged();
        if (transferUniqueInstanceToShop)
            transaction.MarkWorldChanged();
        GameRuntimeSettlementCommandHandler.SettlementCommandRollbackSnapshot rollbackSnapshot =
            _owner.CaptureRollbackSnapshot(transaction);
        SettlementShopTradeResult result = _owner._shop_service.SellTyped(
            context.InteractionScriptId.ToString(),
            _owner._GetItemDefsTyped(),
            _owner.GetPartyWarehouseService(),
            _owner.GetPartyState(),
            item_id,
            quantity,
            instance_id,
            _owner.GetSettlementStateData(settlementId),
            uniqueEquipmentPool,
            settlementId,
            transferUniqueInstanceToShop
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
        if (
            result.UpdatedSettlementState != null
            && !_owner.SetActiveSettlementState(
                settlementId,
                result.UpdatedSettlementState
            )
        )
        {
            _owner.RestoreRollbackSnapshotForFailure(rollbackSnapshot, transaction);
            return _owner.RuntimeCommandError("出售失败：无法写回据点状态。");
        }
        GameRuntimeSettlementCommandHandler.SettlementPersistResult persistResult = _owner.PersistChangesTyped(
            true,
            result.UpdatedSettlementState != null,
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

    internal RuntimeCommandResult CommandStagecoachTravelTyped(
        string settlement_id
    )
    {
        if (_owner.GetActiveModalKind() != RuntimeModalKind.Stagecoach)
        {
            return _owner.RuntimeCommandError("当前没有打开驿站路线窗口。");
        }
        SettlementServiceWindowData context = _owner.GetActiveStagecoachContextTyped();
        if (!context.IsValid)
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
        string originName = ResolveSettlementDisplayName(
            context.SettlementId.ToString(),
            "当前据点"
        );
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
            $"已从 {originName} 抵达 {GameRuntimeSettlementCommandHandler.ReadString(destinationRecord, "display_name", destinationId)}，花费 {travelCost} 金。";
        if (!persistResult.Ok)
            return _owner.RuntimeCommandPersistFailure();
        _owner.UpdateStatus(message);
        return _owner.RuntimeCommandOk(message);
    }

    internal RuntimeCommandResult OpenShopModalTyped(
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
        WorldUniqueEquipmentPoolState uniqueEquipmentPool =
            _owner.GetUniqueEquipmentPoolState();
        WorldUniqueEquipmentPoolState uniqueEquipmentPoolCheckpoint =
            uniqueEquipmentPool?.DuplicateState();
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
                _owner._GetTraitDefsTyped(),
                uniqueEquipmentPool
            );
        if (!buildResult.HasWindowData)
        {
            uniqueEquipmentPool?.RestoreFrom(uniqueEquipmentPoolCheckpoint);
            return _owner.RuntimeCommandError("无法打开商店：商店配置无效。");
        }
        if (
            buildResult.StateChanged
            && !_owner.SetActiveSettlementState(
                settlementId,
                buildResult.UpdatedSettlementState
            )
        )
        {
            uniqueEquipmentPool?.RestoreFrom(uniqueEquipmentPoolCheckpoint);
            return _owner.RuntimeCommandError("无法打开商店：据点状态写回失败。");
        }
        _owner.SetActiveShopContext(
            ApplyShopMemberOptions(
                buildResult.WindowData.WithIdentity(
                    settlementId,
                    GameRuntimeSettlementCommandHandler.ReadString(
                        payload,
                        "interaction_script_id"
                    )
                )
            )
        );
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
        SettlementServiceWindowData windowData = _owner._forge_service.BuildWindowDataTyped(
            GameRuntimeSettlementCommandHandler.ReadString(payload, "interaction_script_id"),
            settlementLease.Value,
            payload,
            _owner._GetItemDefsTyped(),
            _owner.GetRecipeDefsTyped(),
            _owner.GetPartyWarehouseService()
        );
        StringName selectedMemberId = GameRuntimeSettlementCommandHandler.ReadStringName(payload, "member_id");
        if (selectedMemberId == "")
        {
            selectedMemberId = _owner.ResolveDefaultSettlementMemberId();
        }
        _owner.SetActiveForgeContext(
            windowData
                .WithIdentity(
                    settlement_id,
                    GameRuntimeSettlementCommandHandler.ReadString(
                        payload,
                        "interaction_script_id"
                    )
                )
                .WithMemberOptions(
                    _windowDataBuilder.BuildMemberOptionData(),
                    selectedMemberId,
                    selectedMemberId
                )
        );
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
        string interactionScriptId = GameRuntimeSettlementCommandHandler.ReadString(
            payload,
            "interaction_script_id"
        );
        _owner.SetActiveStagecoachContext(
            BuildStagecoachWindowData(
                settlement_id,
                GameRuntimeSettlementCommandHandler.ReadString(settlement, "display_name", "据点"),
                interactionScriptId,
                _build_stagecoach_entries(settlement, interactionScriptId),
                "选择一个已访问据点并支付路费后即可启程。"
            )
        );
        _owner.SetActiveModalKind(RuntimeModalKind.Stagecoach);
        _owner.UpdateStatus("已打开驿站路线。");
    }

    private SettlementServiceWindowData BuildStagecoachWindowData(
        string settlementId,
        string originName,
        string interactionScriptId,
        List<SettlementServiceWindowEntryData> entries,
        string feedbackText
    )
    {
        int gold = _owner.GetPartyGold();
        return new SettlementServiceWindowData(
            settlementId,
            StagecoachActionId,
            SettlementPanelKind.Stagecoach,
            $"{originName} · 驿站路线",
            $"驿站：{originName}  |  金币：{gold}",
            $"持有金币：{gold}",
            feedbackText ?? "",
            new SettlementServiceWindowLabelsData(
                "确认出发",
                "返回据点",
                "可选路线",
                "行程概况",
                "行程状态",
                "行程费用",
                "行程说明",
                "出发成员",
                "状态：暂无路线",
                "费用：暂无路线",
                "当前没有可用路线。"
            ),
            true,
            interactionScriptId,
            "",
            "",
            "",
            "",
            "",
            entries,
            _windowDataBuilder.BuildMemberOptionData(),
            _owner.ResolveDefaultSettlementMemberId(),
            _owner.ResolveDefaultSettlementMemberId(),
            null
        );
    }

    private void _refresh_active_shop_context(string feedbackText = null)
    {
        SettlementServiceWindowData context = _owner.GetActiveShopContextTyped();
        if (!context.IsValid)
        {
            return;
        }
        string settlementId = context.SettlementId.ToString();
        string interactionScriptId = context.InteractionScriptId.ToString();
        WorldMapSettlementStateData settlementState =
            _owner.GetSettlementStateData(settlementId);
        if (settlementState == null)
            return;
        using GodotProjectionLease<GDictionary> settlementLease =
            _owner.GetSettlementRecordLease(settlementId);
        WorldUniqueEquipmentPoolState uniqueEquipmentPool =
            _owner.GetUniqueEquipmentPoolState();
        WorldUniqueEquipmentPoolState uniqueEquipmentPoolCheckpoint =
            uniqueEquipmentPool?.DuplicateState();
        string nextFeedback = feedbackText ?? context.StateSummaryText;
        SettlementShopWindowBuildResult buildResult =
            _owner._shop_service.BuildWindowDataTyped(
                interactionScriptId,
                settlementLease.Value,
                settlementState,
                _owner.GetWorldStep(),
                nextFeedback,
                _owner._GetItemDefsTyped(),
                _owner.GetPartyWarehouseService(),
                _owner.GetPartyGold(),
                _owner._GetTraitDefsTyped(),
                uniqueEquipmentPool
            );
        if (!buildResult.HasWindowData)
        {
            uniqueEquipmentPool?.RestoreFrom(uniqueEquipmentPoolCheckpoint);
            return;
        }
        if (
            buildResult.StateChanged
            && !_owner.SetActiveSettlementState(
                settlementId,
                buildResult.UpdatedSettlementState
            )
        )
        {
            uniqueEquipmentPool?.RestoreFrom(uniqueEquipmentPoolCheckpoint);
            return;
        }
        _owner.SetActiveShopContext(
            ApplyShopMemberOptions(
                buildResult.WindowData.WithIdentity(settlementId, interactionScriptId)
            )
        );
    }

    private SettlementServiceWindowData ApplyShopMemberOptions(
        SettlementServiceWindowData windowData
    ) =>
        windowData.WithMemberOptions(
            _windowDataBuilder.BuildMemberOptionData(),
            _owner.ResolveDefaultSettlementMemberId(),
            _owner.ResolveDefaultSettlementMemberId()
        );

    internal void _refresh_active_forge_context(string feedback_text = "")
    {
        SettlementServiceWindowData context = _owner.GetActiveForgeContextTyped();
        if (!context.IsValid)
        {
            return;
        }
        string settlementId = context.SettlementId.ToString();
        string interactionScriptId = context.InteractionScriptId.ToString();
        using GodotProjectionLease<GDictionary> settlementLease =
            _owner.GetSettlementRecordLease(settlementId);
        // The forge service still reads the originating service entry as a Godot dictionary
        // (facility tags come from the world record); it is rebuilt here from the typed
        // context's stable ids instead of being stored as a property bag.
        SettlementServiceWindowData nextContext = _owner._forge_service.BuildWindowDataTyped(
            interactionScriptId,
            settlementLease.Value,
            BuildForgeServicePayload(context),
            _owner._GetItemDefsTyped(),
            _owner.GetRecipeDefsTyped(),
            _owner.GetPartyWarehouseService(),
            !string.IsNullOrEmpty(feedback_text) ? feedback_text : context.StateSummaryText
        );
        _owner.SetActiveForgeContext(
            nextContext
                .WithIdentity(settlementId, interactionScriptId)
                .WithMemberOptions(
                    context.MemberOptions,
                    context.DefaultMemberId,
                    context.SelectedMemberId != (StringName)""
                        ? context.SelectedMemberId
                        : context.DefaultMemberId
                )
        );
    }

    private static GDictionary BuildForgeServicePayload(SettlementServiceWindowData context)
    {
        return new GDictionary
        {
            ["settlement_id"] = context.SettlementId.ToString(),
            ["action_id"] = context.ActionId.ToString(),
            ["interaction_script_id"] = context.InteractionScriptId.ToString(),
            ["facility_id"] = context.FacilityId.ToString(),
            ["facility_name"] = context.FacilityName,
            ["npc_id"] = context.NpcId.ToString(),
            ["npc_name"] = context.NpcName,
            ["service_type"] = context.ServiceType,
            ["member_id"] = context.SelectedMemberId.ToString(),
            ["default_member_id"] = context.DefaultMemberId.ToString(),
        };
    }

    private List<SettlementServiceWindowEntryData> _build_stagecoach_entries(
        GDictionary origin_settlement,
        string interaction_script_id
    )
    {
        var entries = new List<SettlementServiceWindowEntryData>();
        foreach (
            StagecoachDestinationData destination in BuildStagecoachDestinationData(
                origin_settlement,
                interaction_script_id
            )
        )
        {
            entries.Add(
                new SettlementServiceWindowEntryData(
                    $"travel:{destination.SettlementId}",
                    destination.DisplayName,
                    destination.TierName,
                    $"{destination.TierName} {destination.DisabledReason}",
                    destination.CanTravel ? "状态：可出发" : "状态：不可出发",
                    $"路费 {destination.TravelCost} 金",
                    destination.CanTravel,
                    destination.DisabledReason,
                    new SettlementStagecoachSelectionData(destination.SettlementId)
                )
            );
        }
        return entries;
    }

    private string ResolveSettlementDisplayName(string settlementId, string fallback)
    {
        if (string.IsNullOrEmpty(settlementId))
            return fallback;
        using GodotProjectionLease<GDictionary> settlementLease =
            _owner.GetSettlementRecordLease(settlementId);
        return GameRuntimeSettlementCommandHandler.ReadString(
            settlementLease.Value,
            "display_name",
            fallback
        );
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

    private StagecoachDestinationData ResolveStagecoachDestinationTyped(
        SettlementServiceWindowData stagecoach_context,
        string settlement_id
    )
    {
        string originSettlementId = stagecoach_context.SettlementId.ToString();
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
        string interactionScriptId = stagecoach_context.InteractionScriptId.ToString();
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
        if (member_id == "" || !_owner._has_runtime() || result == null)
        {
            return;
        }
        GDictionary inventoryDelta = SettlementServiceResultProjection.ProjectInventoryDelta(result);
        if (GameRuntimeSettlementCommandHandler.ReadStringName(inventoryDelta, "recipe_id") == "")
        {
            return;
        }
        _owner.NotifyMisfortuneGuidanceOfForgeResult(member_id, result);
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

    internal string _resolve_forge_service_label(SettlementServiceWindowData context)
    {
        string serviceType = (context.ServiceType ?? "").Trim();
        if (!string.IsNullOrEmpty(serviceType))
        {
            return serviceType;
        }
        return context.InteractionScriptId.ToString().Trim() == "service_master_reforge"
            ? "大师重铸"
            : "锻造";
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
