using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GArray = Godot.Collections.Array;
using GDictArray = Godot.Collections.Array<Godot.Collections.Dictionary>;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

// 翻译自 game_runtime_settlement_command_handler.gd（2026-05-26，据点命令处理 C# 迁移）。
// Settlement workflows depend on a capability-segregated runtime port; child
// services call semantic methods on this owner rather than borrowing the facade.
public sealed class GameRuntimeSettlementCommandHandler : IDisposable
{
    internal const int REST_FULL_COST = 50;
    internal const int INTEL_NETWORK_COST = 50;
    internal const int STAGECOACH_COST_PER_STEP = 10;
    private const int VILLAGE_RUMOR_RANGE = 5;
    private const int INTEL_NETWORK_RANGE = 8;
    private const string PERSIST_FAILURE_ROLLBACK_MESSAGE = "存档提交失败，操作已回滚。";
    internal static readonly StringName NPC_OFFER_LISTING_CHANNEL = "npc_offer";

    internal static readonly HashSet<string> SHOP_INTERACTION_IDS = new()
    {
        "service_basic_supply",
        "service_local_trade",
        "service_city_market",
        "service_military_supply",
        "service_grand_auction",
    };

    internal static readonly HashSet<string> STAGECOACH_INTERACTION_IDS = new()
    {
        "service_stagecoach",
        "service_world_gate_travel",
    };

    internal static readonly HashSet<string> UNIMPLEMENTED_INTERACTION_IDS = new()
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

    private WeakReference<IGameRuntimeSettlementCommandPort> _runtimePortRef;

    private IGameRuntimeSettlementCommandPort Port
    {
        get => ResolveWeakRef(_runtimePortRef);
        set =>
            _runtimePortRef =
                value != null
                    ? new WeakReference<IGameRuntimeSettlementCommandPort>(value)
                    : null;
    }

    internal SettlementShopService _shop_service = new();
    internal SettlementForgeService _forge_service = new();
    internal SettlementResearchService _research_service = new();
    internal readonly QuestAcceptRequirementEvaluator _quest_accept_evaluator = new();
    private readonly GameRuntimeContractBoardCommandHandler _contractBoardHandler = new();
    private readonly GameRuntimeNpcQuestOfferCommandHandler _npcQuestOfferHandler = new();
    private readonly GameRuntimeServiceWindowCommandHandler _serviceWindowHandler = new();
    private readonly GameRuntimeSettlementWindowDataBuilder _windowDataBuilder = new();

    public GameRuntimeSettlementCommandHandler()
    {
        _contractBoardHandler.Setup(this, _npcQuestOfferHandler, _windowDataBuilder);
        _npcQuestOfferHandler.Setup(this, _contractBoardHandler);
        _serviceWindowHandler.Setup(this, _windowDataBuilder, _contractBoardHandler);
        _windowDataBuilder.Setup(this, _serviceWindowHandler);
    }

    internal readonly struct SettlementPersistResult
    {
        public readonly bool Ok;
        public readonly int PartyError;
        public readonly int WorldError;
        public readonly int PlayerError;

        internal SettlementPersistResult(int partyError, int worldError, int playerError)
        {
            PartyError = partyError;
            WorldError = worldError;
            PlayerError = playerError;
            Ok =
                PartyError == (int)Error.Ok
                && WorldError == (int)Error.Ok
                && PlayerError == (int)Error.Ok;
        }

    }

    internal sealed class SettlementCommandRollbackSnapshot
    {
        private readonly Dictionary<string, object> _activeShopContext =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, object> _activeContractBoardContext =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, object> _activeForgeContext =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, object> _activeStagecoachContext =
            new(StringComparer.Ordinal);
        private NpcQuestOfferWindowData _activeNpcQuestOfferContext;
        private BountyBoardWindowData _activeBountyBoardContext;
        public RuntimeTransactionRollbackState RuntimeState { get; }
        public RuntimeModalKind ActiveModalKind { get; }
        public string ActiveSettlementId { get; }
        public string SettlementFeedbackText { get; }
        public Vector2I SelectedCoord { get; }
        public bool SettlementEntryActive { get; }
        public Vector2I SettlementEntrySourceCoord { get; }
        public Vector2I SettlementEntryTargetCoord { get; }
        internal IReadOnlyDictionary<string, object> ActiveShopContextPlain =>
            RuntimePlainPayload.CloneDictionary(_activeShopContext);
        internal IReadOnlyDictionary<string, object> ActiveContractBoardContextPlain =>
            RuntimePlainPayload.CloneDictionary(_activeContractBoardContext);
        internal IReadOnlyDictionary<string, object> ActiveForgeContextPlain =>
            RuntimePlainPayload.CloneDictionary(_activeForgeContext);
        internal IReadOnlyDictionary<string, object> ActiveStagecoachContextPlain =>
            RuntimePlainPayload.CloneDictionary(_activeStagecoachContext);
        internal NpcQuestOfferWindowData ActiveNpcQuestOfferContext => _activeNpcQuestOfferContext;
        internal BountyBoardWindowData ActiveBountyBoardContext => _activeBountyBoardContext;

        internal SettlementCommandRollbackSnapshot(
            RuntimeTransactionRollbackState runtimeState,
            RuntimeModalKind activeModalKind,
            string activeSettlementId,
            string settlementFeedbackText,
            Vector2I selectedCoord,
            bool settlementEntryActive,
            Vector2I settlementEntrySourceCoord,
            Vector2I settlementEntryTargetCoord,
            IReadOnlyDictionary<string, object> activeShopContext,
            IReadOnlyDictionary<string, object> activeContractBoardContext,
            IReadOnlyDictionary<string, object> activeForgeContext,
            IReadOnlyDictionary<string, object> activeStagecoachContext,
            NpcQuestOfferWindowData activeNpcQuestOfferContext,
            BountyBoardWindowData activeBountyBoardContext
        )
        {
            RuntimeState = runtimeState;
            ActiveModalKind = activeModalKind;
            ActiveSettlementId = activeSettlementId ?? "";
            SettlementFeedbackText = settlementFeedbackText ?? "";
            SelectedCoord = selectedCoord;
            SettlementEntryActive = settlementEntryActive;
            SettlementEntrySourceCoord = settlementEntrySourceCoord;
            SettlementEntryTargetCoord = settlementEntryTargetCoord;
            ReplacePlainPayload(
                _activeShopContext,
                activeShopContext
            );
            ReplacePlainPayload(
                _activeContractBoardContext,
                activeContractBoardContext
            );
            ReplacePlainPayload(
                _activeForgeContext,
                activeForgeContext
            );
            ReplacePlainPayload(
                _activeStagecoachContext,
                activeStagecoachContext
            );
            _activeNpcQuestOfferContext = activeNpcQuestOfferContext;
            _activeBountyBoardContext = activeBountyBoardContext;
        }
    }

    private static void ReplacePlainPayload(
        Dictionary<string, object> target,
        GDictionary payload,
        string ownerPath
    )
    {
        target.Clear();
        Dictionary<string, object> normalized =
            RuntimePlainPayload.NormalizeDictionary(payload ?? new GDictionary(), ownerPath);
        foreach (KeyValuePair<string, object> entry in normalized)
        {
            target[entry.Key] = entry.Value;
        }
    }

    private static void ReplacePlainPayload(
        Dictionary<string, object> target,
        IReadOnlyDictionary<string, object> payload
    )
    {
        target.Clear();
        foreach (KeyValuePair<string, object> entry in RuntimePlainPayload.CloneDictionary(payload))
            target[entry.Key] = entry.Value;
    }

    internal void SetupRuntime(IGameRuntimeSettlementCommandPort runtimePort)
    {
        Port = runtimePort;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Port = null;
        DisposeServiceInstances(recreate: false);
    }

    internal void DisposeRuntime()
    {
        Port = null;
        DisposeServiceInstances(recreate: true);
    }

    internal QuestAcceptContext _build_quest_accept_context()
    {
        return new QuestAcceptContext
        {
            PartyState = GetPartyState(),
            WarehouseService = GetPartyWarehouseService(),
            PartyGold = GetPartyGold(),
            WorldStep = GetWorldStep(),
            SettlementId = GetActiveSettlementId(),
            SettlementTier = GetSettlementTier(),
            QuestDefs = GetQuestDefsTyped(),
        };
    }

    private int GetSettlementTier()
    {
        string settlementId = GetActiveSettlementId();
        if (settlementId == "")
            return 0;
        using GodotProjectionLease<GDictionary> settlementLease =
            GetSettlementRecordLease(settlementId);
        GDictionary settlement = settlementLease.Value;
        if (settlement == null)
            return 0;
        return ReadInt(settlement, "tier", 0);
    }

    internal GodotProjectionLease<GDictionary> GetShopWindowDataLease() =>
        ProjectWindowDataLease(GetShopWindowDataSnapshotPlain(), "shop");

    internal GodotProjectionLease<GDictionary> GetContractBoardWindowDataLease() =>
        ProjectWindowDataLease(GetContractBoardWindowDataSnapshotPlain(), "contract-board");

    internal GodotProjectionLease<GDictionary> GetForgeWindowDataLease() =>
        ProjectWindowDataLease(GetForgeWindowDataSnapshotPlain(), "forge");

    internal GodotProjectionLease<GDictionary> GetStagecoachWindowDataLease() =>
        ProjectWindowDataLease(GetStagecoachWindowDataSnapshotPlain(), "stagecoach");

    internal GDictionary GetSettlementWindowData(string settlement_id = "") =>
        _windowDataBuilder.GetSettlementWindowData(settlement_id);

    internal IReadOnlyDictionary<string, object> GetSettlementHeadlessFactsPlain(
        string settlementId
    ) => _windowDataBuilder.GetSettlementHeadlessFactsPlain(settlementId);

    internal IReadOnlyDictionary<string, object> GetContractBoardWindowDataSnapshotPlain() =>
        _contractBoardHandler.GetContractBoardWindowDataSnapshotPlain();

    internal IReadOnlyDictionary<string, object> GetNpcQuestOfferWindowDataSnapshotPlain() =>
        _npcQuestOfferHandler.GetNpcQuestOfferWindowDataSnapshotPlain();

    internal NpcQuestOfferWindowData GetActiveNpcQuestOfferContextTyped() =>
        _npcQuestOfferHandler.GetActiveNpcQuestOfferContextTyped();

    internal IReadOnlyDictionary<string, object> GetShopWindowDataSnapshotPlain() =>
        _serviceWindowHandler.GetShopWindowDataSnapshotPlain();

    internal IReadOnlyDictionary<string, object> GetForgeWindowDataSnapshotPlain() =>
        _serviceWindowHandler.GetForgeWindowDataSnapshotPlain();

    internal IReadOnlyDictionary<string, object> GetStagecoachWindowDataSnapshotPlain() =>
        _serviceWindowHandler.GetStagecoachWindowDataSnapshotPlain();

    internal IReadOnlyDictionary<string, object> GetBountyBoardWindowDataSnapshotPlain() =>
        _contractBoardHandler.GetBountyBoardWindowDataSnapshotPlain();

    internal BountyBoardWindowData GetActiveBountyBoardContextTyped() =>
        _contractBoardHandler.GetActiveBountyBoardContextTyped();

    internal void SetActiveBountyBoardContext(BountyBoardWindowData data) =>
        _contractBoardHandler.SetActiveBountyBoardContext(data);

    internal void ClearActiveBountyBoardContext() =>
        _contractBoardHandler.ClearActiveBountyBoardContext();

    internal RuntimeCommandResult CommandShopBuyTyped(
        StringName item_id,
        int quantity
    ) => _serviceWindowHandler.CommandShopBuyTyped(item_id, quantity);

    internal RuntimeCommandResult CommandShopSellTyped(
        StringName item_id,
        int quantity,
        StringName instance_id = null
    ) => _serviceWindowHandler.CommandShopSellTyped(item_id, quantity, instance_id);

    internal RuntimeCommandResult CommandStagecoachTravelTyped(
        string settlement_id
    ) => _serviceWindowHandler.CommandStagecoachTravelTyped(settlement_id);

    internal static GodotProjectionLease<GDictionary> ProjectWindowDataLease(
        IReadOnlyDictionary<string, object> snapshot,
        string windowId
    ) =>
        RuntimePlainPayload.ProjectDictionaryLease(
            snapshot,
            $"settlement-window-{windowId}",
            LifetimeDomain.Request,
            $"GameRuntimeSettlementCommandHandler.{windowId}"
        );

    internal RuntimeCommandResult CommandExecuteSettlementActionRuntimeTyped(
        string action_id,
        GDictionary payload = null
    )
    {
        GDictionary payloadData = payload ?? new GDictionary();
        SettlementSubmissionSource source = ReadSubmissionSource(payloadData);
        if (
            source == SettlementSubmissionSource.ContractBoard
            || source == SettlementSubmissionSource.BountyBoard
            || source == SettlementSubmissionSource.Forge
            || source == SettlementSubmissionSource.NpcQuestOffer
        )
        {
            return CommandExecuteSettlementModalActionRuntimeTyped(action_id, payloadData);
        }
        string settlementId = ResolveCommandSettlementId();
        SettlementActionRequest request = BuildSettlementActionRequestFromBoundaryPayload(
            settlementId,
            action_id,
            payloadData,
            source
        );
        return CommandExecuteSettlementActionRuntimeTyped(request);
    }

    internal RuntimeCommandResult CommandExecuteSettlementActionRuntimeTyped(
        SettlementActionRequest request
    ) => CommandExecuteSettlementActionRuntimeTyped(request, default);

    internal RuntimeCommandResult CommandExecuteForgeActionRuntimeTyped(
        ForgeActionRequest request
    )
    {
        if (!request.IsValid)
        {
            return RuntimeCommandError("锻造请求缺少据点、服务或配方 ID。");
        }
        return CommandExecuteSettlementActionRuntimeTyped(
            request.ToSettlementActionRequest(),
            request.RecipeId
        );
    }

    private RuntimeCommandResult CommandExecuteSettlementActionRuntimeTyped(
        SettlementActionRequest request,
        StringName recipeId
    )
    {
        if (!_has_runtime())
        {
            return RuntimeCommandError("运行时尚未初始化。");
        }
        if (!request.IsValid)
        {
            return RuntimeCommandError("据点动作 ID 不能为空。");
        }
        if (IsBattleActive())
        {
            return RuntimeCommandError("当前处于战斗中，不能执行据点动作。");
        }
        string settlementId = request.SettlementId.ToString();
        string actionId = request.ActionId.ToString();
        if (string.IsNullOrEmpty(settlementId))
        {
            return RuntimeCommandError("当前没有可执行动作的据点。");
        }
        SettlementActionValidationResult validation = ValidateSettlementActionRequestTyped(
            request
        );
        if (!validation.Ok)
        {
            return RuntimeCommandError(
                string.IsNullOrEmpty(validation.Message)
                    ? "当前据点未开放该服务。"
                    : validation.Message
            );
        }
        using GodotProjectionLease<GDictionary> serviceEntryLease =
            RuntimePlainPayload.ProjectDictionaryLease(
                validation.ServiceEntryPlain,
                "SettlementActionValidationResult.ServiceEntry",
                LifetimeDomain.Request,
                "SettlementActionValidationResult.ServiceEntry"
            );
        GDictionary serviceEntry = serviceEntryLease.Value;
        if (serviceEntry.Count == 0)
        {
            return RuntimeCommandError("当前据点未开放该服务。");
        }
        GDictionary mergedPayload = BuildSettlementActionPayloadFromRequest(
            serviceEntry,
            request,
            recipeId
        );
        if (mergedPayload.Count == 0)
        {
            return RuntimeCommandError(
                _build_unknown_settlement_action_message(settlementId, actionId)
            );
        }
        return BuildRuntimeCommandResult(
            _dispatch_settlement_action(settlementId, actionId, mergedPayload)
        );
    }

    internal RuntimeCommandResult ExecuteSettlementAction(
        SettlementActionRequest request
    ) => CommandExecuteSettlementActionRuntimeTyped(request);

    private RuntimeCommandResult CommandExecuteSettlementModalActionRuntimeTyped(
        string action_id,
        GDictionary payloadData
    )
    {
        if (!_has_runtime())
        {
            return RuntimeCommandError("运行时尚未初始化。");
        }
        if (string.IsNullOrEmpty(action_id))
        {
            return RuntimeCommandError("据点动作 ID 不能为空。");
        }
        if (IsBattleActive())
        {
            return RuntimeCommandError("当前处于战斗中，不能执行据点动作。");
        }
        string settlementId = ResolveCommandSettlementId();
        if (string.IsNullOrEmpty(settlementId))
        {
            return RuntimeCommandError("当前没有可执行动作的据点。");
        }
        SettlementActionValidationResult validation = ValidateSettlementActionRequestTyped(
            settlementId,
            action_id,
            payloadData
        );
        if (!validation.Ok)
        {
            return RuntimeCommandError(
                string.IsNullOrEmpty(validation.Message)
                    ? "当前据点未开放该服务。"
                    : validation.Message
            );
        }
        using GodotProjectionLease<GDictionary> serviceEntryLease =
            RuntimePlainPayload.ProjectDictionaryLease(
                validation.ServiceEntryPlain,
                "SettlementActionValidationResult.ServiceEntry",
                LifetimeDomain.Request,
                "SettlementActionValidationResult.ServiceEntry"
            );
        GDictionary serviceEntry = serviceEntryLease.Value;
        if (serviceEntry.Count == 0)
        {
            return RuntimeCommandError("当前据点未开放该服务。");
        }
        GDictionary mergedPayload = BuildSettlementModalActionPayloadFromServiceEntry(
            action_id,
            serviceEntry,
            payloadData
        );
        if (mergedPayload.Count == 0)
        {
            return RuntimeCommandError(
                _build_unknown_settlement_action_message(settlementId, action_id)
            );
        }
        return BuildRuntimeCommandResult(
            _dispatch_settlement_action(settlementId, action_id, mergedPayload)
        );
    }

    internal void OnSettlementActionRequested(
        string settlement_id,
        string action_id,
        GDictionary payload
    )
    {
        GDictionary payloadData = payload ?? new GDictionary();
        SettlementActionRequest request = BuildSettlementActionRequestFromBoundaryPayload(
            settlement_id,
            action_id,
            payloadData,
            SettlementSubmissionSource.Settlement
        );
        OnSettlementActionRequested(request);
    }

    internal void OnSettlementActionRequested(SettlementActionRequest request)
    {
        SettlementActionValidationResult validation = ValidateSettlementActionRequestTyped(
            request
        );
        if (!validation.Ok)
        {
            string message = string.IsNullOrEmpty(validation.Message)
                ? "当前据点未开放该服务。"
                : validation.Message;
            SetSettlementFeedbackText(message);
            UpdateStatus(message);
            return;
        }
        string settlementId = request.SettlementId.ToString();
        string actionId = request.ActionId.ToString();
        using GodotProjectionLease<GDictionary> serviceEntryLease =
            RuntimePlainPayload.ProjectDictionaryLease(
                validation.ServiceEntryPlain,
                "SettlementActionValidationResult.ServiceEntry",
                LifetimeDomain.Request,
                "SettlementActionValidationResult.ServiceEntry"
            );
        GDictionary serviceEntry = serviceEntryLease.Value;
        if (serviceEntry.Count == 0)
        {
            string serviceErrorMessage = "当前据点未开放该服务。";
            SetSettlementFeedbackText(serviceErrorMessage);
            UpdateStatus(serviceErrorMessage);
            return;
        }
        GDictionary mergedPayload = BuildSettlementActionPayloadFromRequest(
            serviceEntry,
            request
        );
        if (mergedPayload.Count == 0)
        {
            string unknownMessage = _build_unknown_settlement_action_message(
                settlementId,
                actionId
            );
            SetSettlementFeedbackText(unknownMessage);
            UpdateStatus(unknownMessage);
            return;
        }
        _dispatch_settlement_action(settlementId, actionId, mergedPayload);
    }

    private GDictionary _dispatch_settlement_action(
        string settlement_id,
        string action_id,
        GDictionary payload
    )
    {
        if (!_has_runtime())
        {
            return CommandError("运行时尚未初始化。");
        }
        string interactionScriptId = ReadString(payload, "interaction_script_id");
        if (interactionScriptId == "party_warehouse")
        {
            string warehouseMessage = "已从据点服务打开共享仓库。";
            SettlementCommandRollbackSnapshot warehouseRollbackSnapshot =
                CaptureRollbackSnapshot(new RuntimeTransaction().MarkPartyChanged());
            var warehouseResult = new SettlementServiceResult
            {
                Success = true,
                Message = warehouseMessage,
                PersistPartyState = true,
            };
            warehouseResult.SetQuestProgressEventsTyped(
                _extract_quest_progress_events(payload, action_id, settlement_id)
            );
            SettlementPersistResult warehousePersistResult = FinalizeSuccessfulActionTyped(
                action_id,
                payload,
                warehouseResult,
                warehouseRollbackSnapshot
            );
            if (!warehousePersistResult.Ok)
                return CommandPersistFailure();
            ClearSettlementEntryContext();
            SetActiveSettlementId(settlement_id);
            SetActiveModalKind(RuntimeModalKind.None);
            OpenPartyWarehouseWindow(
                $"据点服务：{ReadString(payload, "facility_name", "设施")}·{ReadString(payload, "npc_name", "值守人员")}"
            );
            UpdateStatus(warehouseMessage);
            return CommandOk(warehouseMessage);
        }
        // NPC quest offer branch must run before the generic QuestProviderContentRules service-provider
        // branch so that `provider_kind == "npc"` quests are handled by NpcQuestOfferDialog rather than
        // swallowed by the contract-board modal. `npc` is intentionally absent from
        // QuestProviderContentRules.SupportedProviderIds().
        if (_npcQuestOfferHandler._try_open_npc_quest_offer(settlement_id, action_id, payload, out GDictionary npcResult))
        {
            return npcResult;
        }
        if (_npcQuestOfferHandler._is_npc_quest_offer_modal_submission(payload))
        {
            return _npcQuestOfferHandler._submit_npc_quest_offer_action(settlement_id, action_id, payload);
        }
        if (interactionScriptId == "service_bounty_registry")
        {
            if (_contractBoardHandler._is_bounty_board_modal_submission(payload))
            {
                return _contractBoardHandler._submit_bounty_board_quest_action(settlement_id, action_id, payload);
            }
            _contractBoardHandler._open_bounty_board_modal(settlement_id, payload);
            return CommandOk(
                $"已打开 {ReadString(payload, "facility_name", "悬赏板")} 的悬赏板。"
            );
        }
        if (QuestProviderContentRules.IsSupportedProviderId(interactionScriptId))
        {
            if (_contractBoardHandler._is_contract_board_modal_submission(payload))
            {
                return _contractBoardHandler._submit_contract_board_quest_action(settlement_id, action_id, payload);
            }
            _contractBoardHandler._open_contract_board_modal(settlement_id, payload);
            return CommandOk(
                $"已打开 {ReadString(payload, "facility_name", "据点任务板")} 的任务板。"
            );
        }
        if (SHOP_INTERACTION_IDS.Contains(interactionScriptId))
        {
            return RuntimeCommandResultProjection.Project(
                _serviceWindowHandler.OpenShopModalTyped(settlement_id, payload)
            );
        }
        if (_serviceWindowHandler._is_forge_interaction(interactionScriptId) && !_serviceWindowHandler._is_forge_modal_submission(payload))
        {
            _serviceWindowHandler._open_forge_modal(settlement_id, payload);
            return CommandOk(
                $"已打开 {ReadString(payload, "facility_name", "锻造设施")} 的锻造界面。"
            );
        }
        if (STAGECOACH_INTERACTION_IDS.Contains(interactionScriptId))
        {
            _serviceWindowHandler._open_stagecoach_modal(settlement_id, payload);
            return CommandOk(
                $"已打开 {ReadString(payload, "facility_name", "驿站")} 的驿站路线。"
            );
        }
        SettlementCommandRollbackSnapshot rollbackSnapshot = CaptureRollbackSnapshot(
            BuildSettlementActionRollbackScope(interactionScriptId)
        );
        SettlementServiceResult serviceResult = ExecuteSettlementActionTyped(
            settlement_id,
            action_id,
            payload
        );
        string message = serviceResult?.Message ?? "交互已完成。";
        SetSettlementFeedbackText(message);
        bool actionSucceeded = serviceResult?.Success ?? false;
        if (_serviceWindowHandler._is_forge_interaction(interactionScriptId))
        {
            _serviceWindowHandler._refresh_active_forge_context(message);
            if (actionSucceeded)
            {
                SettlementPersistResult forgePersistResult = FinalizeSuccessfulActionTyped(
                    action_id,
                    payload,
                    serviceResult,
                    rollbackSnapshot
                );
                if (forgePersistResult.Ok)
                {
                    UpdateStatus(message);
                    return CommandOk(message);
                }
                return CommandPersistFailure();
            }
            UpdateStatus(message);
            return CommandError(message);
        }
        if (actionSucceeded)
        {
            SettlementPersistResult persistResult = FinalizeSuccessfulActionTyped(
                action_id,
                payload,
                serviceResult,
                rollbackSnapshot
            );
            if (persistResult.Ok)
            {
                UpdateStatus(message);
                return CommandOk(message);
            }
            return CommandPersistFailure();
        }
        UpdateStatus(message);
        return CommandError(message);
    }

    internal void OnSettlementWindowClosed()
    {
        if (!_has_runtime())
        {
            return;
        }
        ClearSettlementEntryContext();
        SetActiveSettlementId("");
        SetSettlementFeedbackText("");
        ClearActiveContractBoardContext();
        ClearActiveBountyBoardContext();
        ClearActiveShopContext();
        ClearActiveForgeContext();
        ClearActiveStagecoachContext();
        ClearActiveNpcQuestOfferContext();
        SetActiveModalKind(RuntimeModalKind.None);
        UpdateStatus("已关闭据点窗口，返回世界地图。");
        PresentPendingRewardIfReady();
    }

    internal void OnShopWindowClosed()
    {
        ClearActiveShopContext();
        SetActiveModalKind(RuntimeModalKind.Settlement);
        UpdateStatus("已关闭商店，返回据点服务。");
    }

    internal void OnContractBoardWindowClosed()
    {
        ClearActiveContractBoardContext();
        SetActiveModalKind(RuntimeModalKind.Settlement);
        UpdateStatus("已关闭任务板，返回据点服务。");
    }

    internal void OnBountyBoardWindowClosed()
    {
        ClearActiveBountyBoardContext();
        SetActiveModalKind(RuntimeModalKind.Settlement);
        UpdateStatus("已关闭悬赏板，返回据点服务。");
    }

    internal void OnNpcQuestOfferWindowClosed()
    {
        ClearActiveNpcQuestOfferContext();
        SetActiveModalKind(RuntimeModalKind.Settlement);
        UpdateStatus("已关闭 NPC 委托面板，返回据点服务。");
    }

    internal void OnForgeWindowClosed()
    {
        using GodotProjectionLease<GDictionary> contextLease = GetActiveForgeContextLease();
        GDictionary context = contextLease.Value;
        string forgeLabel = _serviceWindowHandler._resolve_forge_service_label(context);
        ClearActiveForgeContext();
        SetActiveModalKind(RuntimeModalKind.Settlement);
        UpdateStatus($"已关闭{forgeLabel}，返回据点服务。");
    }

    internal void OnStagecoachWindowClosed()
    {
        ClearActiveStagecoachContext();
        SetActiveModalKind(RuntimeModalKind.Settlement);
        UpdateStatus("已关闭驿站路线，返回据点服务。");
    }

    internal string ResolveCommandSettlementId()
    {
        if (!_has_runtime())
        {
            return "";
        }
        string activeSettlementId = GetActiveSettlementId();
        if (!string.IsNullOrEmpty(activeSettlementId))
        {
            return activeSettlementId;
        }
        WorldMapSettlementData settlement = GetSelectedSettlementData();
        return settlement?.SettlementId ?? "";
    }

    private GDictionary BuildSettlementActionPayloadFromRequest(
        GDictionary service_data,
        SettlementActionRequest request
    ) => BuildSettlementActionPayloadFromRequest(service_data, request, default);

    private GDictionary BuildSettlementActionPayloadFromRequest(
        GDictionary service_data,
        SettlementActionRequest request,
        StringName recipeId
    ) =>
        SettlementActionPayloadBuilder.BuildActionPayload(
            service_data,
            request,
            recipeId,
            ResolveDefaultSettlementMemberId()
        );

    private GDictionary BuildSettlementModalActionPayloadFromServiceEntry(
        string action_id,
        GDictionary service_data,
        GDictionary overrides
    ) =>
        SettlementActionPayloadBuilder.BuildModalActionPayload(
            action_id,
            service_data,
            overrides,
            ResolveCommandSettlementId(),
            ResolveDefaultSettlementMemberId()
        );

    private SettlementActionRequest BuildSettlementActionRequestFromBoundaryPayload(
        string fallback_settlement_id,
        string action_id,
        GDictionary payload,
        SettlementSubmissionSource default_source
    ) =>
        SettlementActionPayloadBuilder.BuildRequestFromBoundary(
            fallback_settlement_id,
            action_id,
            payload,
            default_source
        );

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
        bool requiresEnabledService = _settlement_action_requires_enabled_service(payload);
        string disabledMessage = "";
        if (serviceResolution.Found && requiresEnabledService && !serviceResolution.IsEnabled)
        {
            using GodotProjectionLease<GDictionary> serviceEntryLease =
                RuntimePlainPayload.ProjectDictionaryLease(
                    serviceResolution.ServiceEntryPlain,
                    "SettlementServiceEntryResolution.ServiceEntry",
                    LifetimeDomain.Request,
                    "SettlementServiceEntryResolution.ServiceEntry"
                );
            disabledMessage = _build_disabled_settlement_action_message(
                serviceEntryLease.Value
            );
        }
        string unknownMessage = serviceResolution.Found
            ? ""
            : _build_unknown_settlement_action_message(settlement_id, action_id);
        return SettlementActionValidationPolicy.ValidateResolvedService(
            serviceResolution,
            requiresEnabledService,
            unknownMessage,
            disabledMessage
        );
    }

    private SettlementActionValidationResult ValidateSettlementActionRequestTyped(
        SettlementActionRequest request
    )
    {
        GDictionary requestPayload = BuildSettlementActionRequestValidationPayload(request);
        return ValidateSettlementActionRequestTyped(
            request.SettlementId.ToString(),
            request.ActionId.ToString(),
            requestPayload
        );
    }

    private static GDictionary BuildSettlementActionRequestValidationPayload(
        SettlementActionRequest request
    ) => SettlementActionPayloadBuilder.BuildValidationPayload(request);

    private SettlementActionValidationResult ValidateSettlementActionModalContextTyped(
        string settlement_id,
        string action_id,
        GDictionary payload
    )
    {
        if (_contractBoardHandler._is_contract_board_modal_submission(payload))
        {
            if (GetActiveModalKind() != RuntimeModalKind.ContractBoard)
            {
                return SettlementActionValidationResult.Failure("当前没有打开对应的任务板。");
            }
            using GodotProjectionLease<GDictionary> contractBoardContextLease =
                GetActiveContractBoardContextLease();
            GDictionary contractBoardContext = contractBoardContextLease.Value;
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
        if (_contractBoardHandler._is_bounty_board_modal_submission(payload))
        {
            if (GetActiveModalKind() != RuntimeModalKind.BountyBoard)
            {
                return SettlementActionValidationResult.Failure("当前没有打开对应的悬赏板。");
            }
            BountyBoardWindowData bountyContext = GetActiveBountyBoardContextTyped();
            if (bountyContext == null || bountyContext.SettlementId.Trim() != settlement_id)
            {
                return SettlementActionValidationResult.Failure("当前悬赏板与请求的据点不一致。");
            }
            if (bountyContext.ActionId.Trim() != action_id)
            {
                return SettlementActionValidationResult.Failure("当前悬赏板与请求的服务入口不一致。");
            }
            return SettlementActionValidationResult.Success();
        }
        if (_serviceWindowHandler._is_forge_modal_submission(payload))
        {
            if (GetActiveModalKind() != RuntimeModalKind.Forge)
            {
                return SettlementActionValidationResult.Failure("当前没有打开对应的锻造界面。");
            }
            using GodotProjectionLease<GDictionary> forgeContextLease =
                GetActiveForgeContextLease();
            GDictionary forgeContext = forgeContextLease.Value;
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
        if (_npcQuestOfferHandler._is_npc_quest_offer_modal_submission(payload))
        {
            if (GetActiveModalKind() != RuntimeModalKind.NpcQuestOffer)
            {
                return SettlementActionValidationResult.Failure("当前没有打开 NPC 委托面板。");
            }
            NpcQuestOfferWindowData npcContext = GetActiveNpcQuestOfferContextTyped();
            if (npcContext == null || npcContext.SettlementId.Trim() != settlement_id)
            {
                return SettlementActionValidationResult.Failure("当前 NPC 委托面板与请求的据点不一致。");
            }
            if (npcContext.ActionId.Trim() != action_id)
            {
                return SettlementActionValidationResult.Failure(
                    "当前 NPC 委托面板与请求的服务入口不一致。"
                );
            }
            return SettlementActionValidationResult.Success();
        }
        if (GetActiveModalKind() != RuntimeModalKind.Settlement)
        {
            return SettlementActionValidationResult.Failure("当前没有打开对应的据点窗口。");
        }
        string activeSettlementId = GetActiveSettlementId();
        if (string.IsNullOrEmpty(activeSettlementId) || activeSettlementId != settlement_id)
        {
            return SettlementActionValidationResult.Failure("当前据点窗口与请求的据点不一致。");
        }
        return SettlementActionValidationResult.Success();
    }

    private SettlementActionValidationResult ValidateSettlementVisibilityContextTyped(
        string settlement_id
    )
    {
        using GodotProjectionLease<GDictionary> settlementLease =
            GetSettlementRecordLease(settlement_id);
        GDictionary settlement = settlementLease.Value;
        if (settlement.Count == 0)
        {
            return SettlementActionValidationResult.Failure("未找到据点数据。");
        }
        if (!IsSettlementVisibleToPlayer(settlement))
        {
            return SettlementActionValidationResult.Failure("当前据点不在视野中，不能执行据点服务。");
        }
        return SettlementActionValidationResult.Success();
    }

    private bool _settlement_action_requires_enabled_service(GDictionary payload)
    {
        return !_contractBoardHandler._is_contract_board_modal_submission(payload)
            && !_contractBoardHandler._is_bounty_board_modal_submission(payload)
            && !_serviceWindowHandler._is_forge_modal_submission(payload)
            && !_npcQuestOfferHandler._is_npc_quest_offer_modal_submission(payload);
    }

    private SettlementServiceEntryResolution ResolveSettlementServiceEntryTyped(
        string settlement_id,
        string action_id
    )
    {
        using GodotProjectionLease<GDictionary> settlementLease =
            GetSettlementRecordLease(settlement_id);
        GDictionary settlement = settlementLease.Value;
        if (settlement.Count == 0)
        {
            return SettlementServiceEntryResolution.Missing();
        }
        GArray serviceOptions = ReadArray(settlement, "available_services");
        if (serviceOptions.Count == 0)
        {
            return SettlementServiceEntryResolution.Missing();
        }
        foreach (GDictionary sourceServiceData in Dictionaries(serviceOptions))
        {
            GDictionary serviceData = sourceServiceData;
            if (ReadString(serviceData, "action_id").Trim() != action_id)
            {
                continue;
            }
            SettlementServiceMetadata metadata = _windowDataBuilder.BuildServiceMetadataTyped(
                settlement,
                serviceData
            );
            SettlementServiceMetadataProjection.ApplyToServiceData(serviceData, metadata);
            string disabledReason = metadata.DisabledReason.Trim();
            serviceData["state_label"] = _windowDataBuilder._build_service_state_label(
                metadata.IsEnabled,
                disabledReason
            );
            serviceData["summary_text"] = _windowDataBuilder._build_service_summary_text(serviceData);
            SettlementPanelKind panelKind = _windowDataBuilder._resolve_service_panel_kind(serviceData);
            string panelKindText = SettlementPanelKinds.ToPayloadValue(panelKind);
            if (!string.IsNullOrEmpty(panelKindText))
            {
                serviceData["panel_kind"] = panelKindText;
            }
            return SettlementServiceEntryResolution.FromServiceData(serviceData, metadata);
        }
        return SettlementServiceEntryResolution.Missing();
    }

    private string _build_unknown_settlement_action_message(string settlement_id, string action_id)
    {
        using GodotProjectionLease<GDictionary> settlementLease =
            GetSettlementRecordLease(settlement_id);
        GDictionary settlement = settlementLease.Value;
        string settlementLabel = ReadString(settlement, "display_name", settlement_id).Trim();
        if (string.IsNullOrEmpty(settlementLabel))
        {
            settlementLabel = "当前据点";
        }
        return $"{settlementLabel} 未开放该服务：{action_id}。";
    }

    private string _build_disabled_settlement_action_message(GDictionary service_entry)
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

    internal StringName ResolveDefaultSettlementMemberId()
    {
        PartyState partyState = GetPartyState();
        if (partyState == null)
        {
            return "";
        }
        StringName leaderMemberId = partyState.leader_member_id;
        if (
            leaderMemberId != ""
            && partyState.GetMemberState(leaderMemberId) != null
        )
        {
            return leaderMemberId;
        }
        foreach (StringName memberId in partyState.active_member_ids)
        {
            if (
                memberId != ""
                && partyState.GetMemberState(memberId) != null
            )
            {
                return memberId;
            }
        }
        return "";
    }

    internal SettlementServiceResult ExecuteSettlementActionTyped(
        string settlement_id,
        string action_id,
        GDictionary payload
    )
    {
        using GodotProjectionLease<GDictionary> settlementLease =
            GetSettlementRecordLease(settlement_id);
        GDictionary settlement = settlementLease.Value;
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
        if (_serviceWindowHandler._is_forge_interaction(interactionScriptId))
        {
            return _forge_service.ExecuteRecipeResultTyped(
                settlement,
                payload,
                _GetItemDefsTyped(),
                GetRecipeDefsTyped(),
                GetPartyWarehouseService(),
                GetPartyState(),
                _extract_quest_progress_events(payload, action_id, settlement_id)
            );
        }
        if (_serviceWindowHandler._is_research_interaction(interactionScriptId))
        {
            return _research_service.ExecuteTyped(
                settlement,
                payload,
                GetPartyState(),
                _extract_quest_progress_events(payload, action_id, settlement_id)
            );
        }
        if (UNIMPLEMENTED_INTERACTION_IDS.Contains(interactionScriptId))
        {
            return BuildSettlementServiceResultTyped(
                true,
                "该据点服务入口已接通，但其配套系统尚未开放。",
                new List<PendingCharacterReward>(),
                false,
                false,
                false,
                0,
                new GDictionary(),
                _extract_quest_progress_events(payload, action_id, settlement_id)
            );
        }
        List<PendingCharacterReward> pendingCharacterRewards = ExtractPendingCharacterRewards(
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

    private List<PendingCharacterReward> ExtractPendingCharacterRewards(
        string action_id,
        GDictionary payload,
        string facility_name,
        string npc_name,
        string service_type
    )
    {
        var rewards = new List<PendingCharacterReward>();
        StringName defaultSourceType = ResolveDefaultRewardSourceType(
            action_id,
            service_type,
            payload
        );
        string defaultSourceLabel = ResolveDefaultRewardSourceLabel(
            facility_name,
            npc_name,
            service_type
        );
        GArray explicitRewards = ReadArray(payload, "pending_character_rewards");
        if (explicitRewards.Count != 0)
        {
            foreach (GDictionary sourceReward in Dictionaries(explicitRewards))
            {
                PendingCharacterReward rewardData = BuildPendingCharacterRewardTyped(
                    sourceReward,
                    payload,
                    defaultSourceType,
                    defaultSourceLabel
                );
                if (rewardData != null && !rewardData.IsEmpty())
                {
                    rewards.Add(rewardData);
                }
            }
        }
        if (Port != null)
        {
            object lowLuckResultValue = Port.ResolveLowLuckSettlementEventRewards(
                new GDictionary
                {
                    ["action_id"] = action_id,
                    ["facility_id"] = ReadString(payload, "facility_id"),
                    ["facility_name"] = facility_name,
                    ["interaction_script_id"] = ReadString(payload, "interaction_script_id"),
                    ["npc_name"] = npc_name,
                    ["payload"] = payload,
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
                        PendingCharacterReward normalizedRewardData = BuildPendingCharacterRewardTyped(
                            rewardData,
                            payload,
                            defaultSourceType,
                            defaultSourceLabel
                        );
                        if (normalizedRewardData != null && !normalizedRewardData.IsEmpty())
                        {
                            rewards.Add(normalizedRewardData);
                        }
                    }
                }
            }
        }
        return rewards;
    }

    private PendingCharacterReward BuildPendingCharacterRewardTyped(
        GDictionary source_reward,
        GDictionary payload,
        StringName default_source_type,
        string default_source_label
    )
    {
        if (source_reward == null || source_reward.Count == 0)
        {
            return null;
        }
        if (Port == null)
        {
            return null;
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
        List<PendingCharacterRewardEntry> entries = BuildPendingCharacterRewardEntriesTyped(
            ReadArray(source_reward, "entries")
        );
        PendingCharacterReward reward = Port.BuildPendingCharacterReward(
            memberId,
            ReadStringName(source_reward, "reward_id"),
            sourceType,
            sourceId,
            sourceLabel,
            entries,
            ReadString(source_reward, "summary_text")
        );
        return reward;
    }

    private static List<PendingCharacterRewardEntry> BuildPendingCharacterRewardEntriesTyped(
        GArray sourceEntries
    )
    {
        var entries = new List<PendingCharacterRewardEntry>();
        if (sourceEntries == null)
        {
            return entries;
        }
        foreach (Variant entryValue in sourceEntries)
        {
            if (!entryValue.TryAsDictionary(out GDictionary entryData))
            {
                continue;
            }
            entries.Add(
                new PendingCharacterRewardEntry
                {
                    entry_type = ReadStringName(entryData, "entry_type"),
                    target_id = ReadStringName(entryData, "target_id"),
                    target_label = ReadString(entryData, "target_label"),
                    amount = ReadInt(entryData, "amount"),
                    reason_text = ReadString(entryData, "reason_text"),
                    mastery_source_type = ReadStringName(entryData, "mastery_source_type"),
                }
            );
        }
        return entries;
    }

    private StringName ResolveDefaultRewardSourceType(
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

    private string ResolveDefaultRewardSourceLabel(
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

    private SettlementServiceResult ExecuteRestBasicTyped(
        GDictionary settlement,
        string action_id,
        GDictionary payload
    )
    {
        GDictionary memberEffects = RestorePartyResources(0.3f, false);
        var summaryLines = new List<string>();
        foreach (object memberIdValue in memberEffects.Keys)
        {
            string memberId = memberIdValue.ToString();
            GDictionary effect = ReadDictionary(memberEffects, memberId);
            summaryLines.Add($"{GetMemberDisplayName(new StringName(memberId))} +{ReadInt(effect, "hp_restored")} HP");
        }
        return BuildSettlementServiceResultTyped(
            true,
            $"{ReadString(settlement, "display_name", "据点")} 的篝火让全队稍作歇脚。{(summaryLines.Count != 0 ? string.Join("；", summaryLines) : "体力恢复有限。")}",
            ExtractPendingCharacterRewards(
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

    private SettlementServiceResult ExecuteRestFullTyped(
        GDictionary settlement,
        string action_id,
        GDictionary payload
    )
    {
        PartyState partyState = GetPartyState();
        if (partyState == null)
        {
            return BuildSettlementServiceResultTyped(false, "当前不存在队伍数据。");
        }
        if (!partyState.SpendGold(REST_FULL_COST))
        {
            return BuildSettlementServiceResultTyped(false, "金币不足，无法在旅店整备。");
        }
        GDictionary memberEffects = RestorePartyResources(1.0f, true);
        AdvanceWorldTimeBySteps(1);
        var summaryLines = new List<string>();
        foreach (object memberIdValue in memberEffects.Keys)
        {
            string memberId = memberIdValue.ToString();
            GDictionary effect = ReadDictionary(memberEffects, memberId);
            summaryLines.Add($"{GetMemberDisplayName(new StringName(memberId))} HP+{ReadInt(effect, "hp_restored")} MP+{ReadInt(effect, "mp_restored")}");
        }
        return BuildSettlementServiceResultTyped(
            true,
            $"{ReadString(settlement, "display_name", "据点")} 的旅店让全队完成整备，花费 {REST_FULL_COST} 金。{(summaryLines.Count != 0 ? string.Join("；", summaryLines) : "状态恢复如初。")}",
            ExtractPendingCharacterRewards(
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

    private GDictionary _execute_fog_reveal(
        GDictionary settlement,
        string action_id,
        GDictionary payload,
        int reveal_range,
        int gold_cost,
        string message_prefix
    ) => SettlementServiceResultProjection.Project(
        ExecuteFogRevealTyped(
            settlement,
            action_id,
            payload,
            reveal_range,
            gold_cost,
            message_prefix
        )
    );

    private SettlementServiceResult ExecuteFogRevealTyped(
        GDictionary settlement,
        string action_id,
        GDictionary payload,
        int reveal_range,
        int gold_cost,
        string message_prefix
    )
    {
        PartyState partyState = GetPartyState();
        if (partyState == null)
        {
            return BuildSettlementServiceResultTyped(false, "当前不存在队伍数据。");
        }
        if (gold_cost > 0 && !partyState.SpendGold(gold_cost))
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
            ExtractPendingCharacterRewards(
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

    internal static IReadOnlyList<object> ReadPlainList(
        IReadOnlyDictionary<string, object> values,
        string key
    )
    {
        return values != null
            && values.TryGetValue(key, out object value)
            && value is IReadOnlyList<object> list
                ? list
                : Array.Empty<object>();
    }

    internal static string ReadPlainString(
        IReadOnlyDictionary<string, object> values,
        string key,
        string fallback = ""
    )
    {
        if (values == null || !values.TryGetValue(key, out object value))
            return fallback;
        return value switch
        {
            string text => text,
            StringName stringName => stringName.ToString(),
            _ => fallback,
        };
    }

    internal static int ReadPlainInt(
        IReadOnlyDictionary<string, object> values,
        string key,
        int fallback = 0
    )
    {
        if (values == null || !values.TryGetValue(key, out object value))
            return fallback;
        return value switch
        {
            byte number => number,
            short number => number,
            int number => number,
            long number when number >= int.MinValue && number <= int.MaxValue => (int)number,
            _ => fallback,
        };
    }

    internal static Dictionary<string, object> EmptyPlainDictionary() =>
        new(StringComparer.Ordinal);

    internal GDictionary RestorePartyResources(float restore_ratio, bool restore_full)
    {
        var effects = new GDictionary();
        PartyState partyState = GetPartyState();
        if (partyState == null)
        {
            return effects;
        }
        foreach (StringName memberId in partyState.active_member_ids)
        {
            PartyMemberState memberState = partyState.GetMemberState(memberId);
            if (memberState == null || memberState.IsDead())
            {
                continue;
            }
            AttributeSnapshot attributeSnapshot = GetMemberAttributeSnapshot(memberId);
            int hpMax =
                attributeSnapshot != null
                    ? attributeSnapshot.GetValue(new StringName("hp_max"))
                    : Math.Max(memberState.current_hp, 1);
            int mpMax =
                attributeSnapshot != null
                    ? attributeSnapshot.GetValue(new StringName("mp_max"))
                    : Math.Max(memberState.current_mp, 0);
            int oldHp = memberState.current_hp;
            int oldMp = memberState.current_mp;
            double recoveryMultiplier = 1.0;
            if (
                attributeSnapshot != null
                && attributeSnapshot.GetValue(LowLuckRelicRules.ToStringName(LowLuckRelicAttributeKind.BloodDebtShawl)) > 0
            )
            {
                recoveryMultiplier = LowLuckRelicRules.BloodDebtRecoveryMultiplier;
            }
            int hpRestoreAmount = restore_full
                ? hpMax - oldHp
                : (int)Math.Ceiling(hpMax * (double)restore_ratio);
            hpRestoreAmount = (int)Math.Ceiling(Math.Max(hpRestoreAmount, 0) * recoveryMultiplier);
            memberState.SetCurrentHp(Math.Min(oldHp + hpRestoreAmount, hpMax));
            int mpRestoreAmount = restore_full ? mpMax - oldMp : 0;
            mpRestoreAmount = (int)Math.Ceiling(Math.Max(mpRestoreAmount, 0) * recoveryMultiplier);
            memberState.SetCurrentMp(Math.Min(oldMp + mpRestoreAmount, mpMax));
            effects[memberId.ToString()] = new GDictionary
            {
                ["hp_restored"] = Math.Max(memberState.current_hp - oldHp, 0),
                ["mp_restored"] = Math.Max(memberState.current_mp - oldMp, 0),
            };
        }
        return effects;
    }

    private GArray _reveal_world_fog(Vector2I center, int reveal_range)
    {
        GArray revealedCoords =
            Port != null
                ? new GArray(
                    Port
                        .RevealWorldFogDiamond(center, reveal_range, GetPlayerFactionId())
                        .Select(v => Variant.From(v))
                )
                : new GArray();
        if (revealedCoords.Count != 0)
        {
            RefreshWorldVisibility();
        }
        return revealedCoords;
    }

    internal void _mark_settlement_visited(string settlement_id)
    {
        if (!_has_runtime())
        {
            return;
        }
        Port.MarkSettlementVisited(settlement_id);
    }

    internal bool IsSettlementVisited(string settlementId) =>
        Port?.IsSettlementVisited(settlementId) ?? false;

    private SettlementPersistResult FinalizeSuccessfulActionTyped(
        string action_id,
        GDictionary payload,
        SettlementServiceResult result,
        SettlementCommandRollbackSnapshot rollbackSnapshot = null
    )
    {
        if (result == null)
            return PersistChangesTyped(false, false, false, rollbackSnapshot);

        EnqueuePendingCharacterRewardsTyped(result.PendingCharacterRewards);
        _apply_quest_progress_events(result.QuestProgressEvents);
        StringName memberId = ReadStringName(payload, "member_id");
        if (memberId != "")
        {
            _serviceWindowHandler._notify_misfortune_guidance_of_forge_result(memberId, result);
            RecordMemberAchievementEvent(
                memberId,
                "settlement_action_completed",
                1,
                ProgressionDataUtils.to_string_name(action_id)
            );
        }
        SyncPartyStateFromCharacterManagement();
        bool partyStateChanged =
            result.PersistPartyState
            || result.PendingCharacterRewards.Count > 0
            || result.QuestProgressEvents.Count > 0
            || memberId != "";
        return PersistChangesTyped(
            partyStateChanged,
            result.PersistWorldData,
            result.PersistPlayerCoord,
            rollbackSnapshot
        );
    }

    private SettlementServiceResult BuildSettlementServiceResultTyped(
        bool success,
        string message,
        IEnumerable<PendingCharacterReward> pending_character_rewards = null,
        bool persist_party_state = false,
        bool persist_world_data = false,
        bool persist_player_coord = false,
        int gold_delta = 0,
        GDictionary inventory_delta = null,
        IEnumerable<QuestProgressService.QuestProgressEventData> quest_progress_events = null,
        GDictionary service_side_effects = null
    )
    {
        inventory_delta ??= new GDictionary();
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
        result.SetPendingCharacterRewardsTyped(pending_character_rewards);
        result.SetQuestProgressEventsTyped(quest_progress_events);
        result.SetServiceSideEffects(service_side_effects);
        return result;
    }

    private List<QuestProgressService.QuestProgressEventData> _extract_quest_progress_events(
        GDictionary payload,
        string action_id,
        string settlement_id
    )
    {
        var questProgressEvents = new List<QuestProgressService.QuestProgressEventData>();
        int worldStep = GetWorldStep();
        foreach (GDictionary sourceEventData in Dictionaries(ReadArray(payload, "quest_progress_events")))
        {
            GDictionary eventData = sourceEventData;
            if (!eventData.ContainsKey("world_step"))
            {
                eventData["world_step"] = worldStep;
            }
            QuestProgressService.QuestProgressEventData typedEvent =
                QuestProgressService.QuestProgressEventData.FromDictionary(eventData);
            if (typedEvent != null && typedEvent.IsValid)
            {
                questProgressEvents.Add(typedEvent);
            }
        }
        if (!ReadBool(payload, "emit_default_quest_progress_event", true))
        {
            return questProgressEvents;
        }
        QuestProgressService.QuestProgressEventData defaultEvent =
            QuestProgressService.QuestProgressEventData.FromDictionary(
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
        if (defaultEvent != null && defaultEvent.IsValid)
        {
            questProgressEvents.Add(defaultEvent);
        }
        return questProgressEvents;
    }

    private void _apply_quest_progress_events(
        IEnumerable<QuestProgressService.QuestProgressEventData> event_options
    )
    {
        if (!_has_runtime() || event_options == null)
        {
            return;
        }
        Port.ApplyQuestProgressEventsToPartyTyped(event_options, "settlement");
    }

    private static RuntimeTransaction BuildSettlementActionRollbackScope(
        string interactionScriptId
    )
    {
        RuntimeTransaction scope = new RuntimeTransaction().MarkPartyChanged();
        if (
            interactionScriptId == "service_rest_full"
            || interactionScriptId == "service_village_rumor"
            || interactionScriptId == "service_intel_network"
        )
        {
            scope.MarkWorldChanged();
        }
        return scope;
    }

    internal SettlementCommandRollbackSnapshot CaptureRollbackSnapshot(
        RuntimeTransaction rollbackScope
    )
    {
        if (!_has_runtime())
            return null;

        RuntimeTransaction transaction =
            rollbackScope ?? throw new ArgumentNullException(nameof(rollbackScope));
        SettlementEntryRuntimeSnapshot entrySnapshot =
            Port.CaptureSettlementEntrySnapshot();
        return new SettlementCommandRollbackSnapshot(
            Port.CaptureRuntimeTransactionRollbackState(transaction),
            GetActiveModalKind(),
            GetActiveSettlementId(),
            GetSettlementFeedbackText(),
            entrySnapshot.SelectedCoord,
            entrySnapshot.IsActive,
            entrySnapshot.SourceCoord,
            entrySnapshot.TargetCoord,
            Port.GetActiveShopContextPlain(),
            Port.GetActiveContractBoardContextPlain(),
            Port.GetActiveForgeContextPlain(),
            Port.GetActiveStagecoachContextPlain(),
            GetActiveNpcQuestOfferContextTyped(),
            GetActiveBountyBoardContextTyped()
        );
    }

    private void RestoreRollbackSnapshot(SettlementCommandRollbackSnapshot snapshot)
    {
        if (!_has_runtime() || snapshot == null)
            return;

        SetSelectedCoord(snapshot.SelectedCoord);
        SetActiveSettlementId(snapshot.ActiveSettlementId);
        SetSettlementFeedbackText(snapshot.SettlementFeedbackText);
        Port.SetActiveShopContextPlain(snapshot.ActiveShopContextPlain);
        Port.SetActiveContractBoardContextPlain(snapshot.ActiveContractBoardContextPlain);
        Port.SetActiveForgeContextPlain(snapshot.ActiveForgeContextPlain);
        Port.SetActiveStagecoachContextPlain(snapshot.ActiveStagecoachContextPlain);
        if (snapshot.ActiveNpcQuestOfferContext != null)
            SetActiveNpcQuestOfferContext(snapshot.ActiveNpcQuestOfferContext);
        if (snapshot.ActiveBountyBoardContext != null)
            SetActiveBountyBoardContext(snapshot.ActiveBountyBoardContext);
        if (snapshot.SettlementEntryActive)
            Port.SetSettlementEntryContext(
                snapshot.SettlementEntrySourceCoord,
                snapshot.SettlementEntryTargetCoord
            );
        else
            Port.ClearSettlementEntryContext(false);
        SetActiveModalKind(snapshot.ActiveModalKind);
    }

    internal void RestoreRollbackSnapshotForFailure(
        SettlementCommandRollbackSnapshot snapshot,
        RuntimeTransaction transaction
    )
    {
        if (!_has_runtime() || snapshot == null || transaction == null)
            return;
        RestoreRollbackSnapshot(snapshot);
        Port.RollbackRuntimeTransaction(transaction, snapshot.RuntimeState);
    }

    private GDictionary _build_member_effect_value_map(GDictionary member_effects, string value_key)
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

    private GDictionary _persist_changes(
        bool persist_party_state,
        bool persist_world_data,
        bool persist_player_coord
    ) =>
        ProjectSettlementPersistResult(
            PersistChangesTyped(
                persist_party_state,
                persist_world_data,
                persist_player_coord
            )
        );

    private static GDictionary ProjectSettlementPersistResult(
        SettlementPersistResult result
    ) =>
        new()
        {
            ["ok"] = result.Ok,
            ["party_error"] = result.PartyError,
            ["world_error"] = result.WorldError,
            ["player_error"] = result.PlayerError,
        };

    internal SettlementPersistResult PersistChangesTyped(
        bool persist_party_state,
        bool persist_world_data,
        bool persist_player_coord,
        SettlementCommandRollbackSnapshot rollbackSnapshot = null
    )
    {
        if (!_has_runtime())
            return new SettlementPersistResult(
                persist_party_state ? (int)Error.Unavailable : (int)Error.Ok,
                persist_world_data ? (int)Error.Unavailable : (int)Error.Ok,
                persist_player_coord ? (int)Error.Unavailable : (int)Error.Ok
            );
        var transaction = new RuntimeTransaction();
        if (persist_party_state)
        {
            transaction.MarkPartyChanged();
        }
        if (persist_world_data)
        {
            transaction.MarkWorldChanged();
        }
        if (persist_player_coord)
        {
            transaction.MarkPlayerCoordChanged();
        }
        RuntimeCommitResult result = Port.CommitRuntimeTransaction(
            transaction,
            "settlement_command"
        );
        if (!result.Ok && rollbackSnapshot != null)
        {
            RestoreRollbackSnapshot(rollbackSnapshot);
            Port.RollbackRuntimeTransaction(transaction, rollbackSnapshot.RuntimeState);
        }
        int commitError = result.CommitError;
        return new SettlementPersistResult(
            result.PartyError == (int)Error.Ok && persist_party_state
                ? commitError
                : result.PartyError,
            result.WorldError == (int)Error.Ok && persist_world_data
                ? commitError
                : result.WorldError,
            result.PlayerError == (int)Error.Ok && persist_player_coord
                ? commitError
                : result.PlayerError
        );
    }

    internal bool _has_runtime()
    {
        return Port != null;
    }

    internal GDictionary CommandOk(string message = "")
    {
        if (Port == null)
        {
            return new GDictionary
            {
                ["ok"] = true,
                ["message"] = message,
                ["battle_refresh_mode"] = "",
            };
        }
        return Port.BuildCommandOk(message);
    }

    internal RuntimeCommandResult RuntimeCommandOk(string message = "")
    {
        return RuntimeCommandResult.Success(message ?? "");
    }

    private static RuntimeCommandResult BuildRuntimeCommandResult(
        GDictionary result
    )
    {
        if (result == null)
        {
            return RuntimeCommandResult.Failure("");
        }

        bool ok =
            result.ContainsKey("ok")
            && result["ok"].VariantType == Variant.Type.Bool
            && result["ok"].AsBool();
        string message =
            result.ContainsKey("message")
                ? result["message"].VariantType switch
                {
                    Variant.Type.String => result["message"].AsString(),
                    Variant.Type.StringName => result["message"].AsStringName().ToString(),
                    _ => "",
                }
                : "";
        RuntimeCommandCode code =
            result.ContainsKey("code")
            && result["code"].VariantType == Variant.Type.Int
            && Enum.IsDefined(
                typeof(RuntimeCommandCode),
                result["code"].AsInt32()
            )
                ? (RuntimeCommandCode)result["code"].AsInt32()
                : ok
                    ? RuntimeCommandCode.Ok
                    : RuntimeCommandCode.Failed;
        return ok
            ? RuntimeCommandResult.Success(message, code)
            : RuntimeCommandResult.Failure(message, code);
    }

    internal GDictionary CommandError(string message)
    {
        if (Port == null)
        {
            return new GDictionary { ["ok"] = false, ["message"] = message };
        }
        return Port.BuildCommandError(message);
    }

    private GDictionary CommandPersistFailure()
    {
        UpdateStatus(PERSIST_FAILURE_ROLLBACK_MESSAGE);
        return new GDictionary
        {
            ["ok"] = false,
            ["message"] = PERSIST_FAILURE_ROLLBACK_MESSAGE,
            ["code"] = (int)RuntimeCommandCode.PersistenceFailure,
        };
    }

    internal RuntimeCommandResult RuntimeCommandError(string message)
    {
        if (!string.IsNullOrEmpty(message))
            UpdateStatus(message);
        return RuntimeCommandResult.Failure(
            message ?? "",
            RuntimeCommandCode.InvalidState
        );
    }

    internal RuntimeCommandResult RuntimeCommandPersistFailure()
    {
        UpdateStatus(PERSIST_FAILURE_ROLLBACK_MESSAGE);
        return RuntimeCommandResult.Failure(
            PERSIST_FAILURE_ROLLBACK_MESSAGE,
            RuntimeCommandCode.PersistenceFailure
        );
    }

    internal bool IsBattleActive()
    {
        return _has_runtime() && Port.IsBattleActive();
    }

    internal void UpdateStatus(string message)
    {
        if (_has_runtime())
        {
            Port.UpdateStatus(message);
        }
    }

    internal string GetActiveSettlementId()
    {
        return Port?.GetActiveSettlementId() ?? "";
    }

    internal void SetActiveSettlementId(string settlement_id)
    {
        if (_has_runtime())
        {
            Port.SetActiveSettlementId(settlement_id);
        }
    }

    internal void SetSettlementFeedbackText(string feedback_text)
    {
        if (_has_runtime())
        {
            Port.SetSettlementFeedbackText(feedback_text);
        }
    }

    internal string GetSettlementFeedbackText()
    {
        return Port?.GetSettlementFeedbackText() ?? "";
    }

    internal WorldMapSettlementData GetSelectedSettlementData() =>
        _has_runtime() ? Port.GetSelectedSettlementData() : WorldMapSettlementData.Empty;

    internal PartyState GetPartyState()
    {
        return Port?.GetPartyState();
    }

    internal int GetPartyGold()
    {
        return GetPartyState()?.GetGold() ?? 0;
    }

    internal GodotProjectionLease<GDictionary> GetSettlementRecordLease(string settlement_id) =>
        _has_runtime()
            ? Port.GetSettlementRecordLease(settlement_id)
            : EmptyDictionaryLease("settlement_record");

    internal GodotProjectionLease<GArray> GetAllSettlementRecordsLease() =>
        _has_runtime()
            ? Port.GetAllSettlementRecordsLease()
            : RuntimePlainPayload.ProjectArrayLease(
                Array.Empty<object>(),
                "GameRuntimeSettlementCommandHandler.empty_settlements",
                LifetimeDomain.Request,
                "GameRuntimeSettlementCommandHandler.empty_settlements"
            );

    internal WorldMapSettlementStateData GetSettlementStateData(string settlement_id) =>
        _has_runtime() ? Port.GetSettlementStateData(settlement_id) : null;

    private static GodotProjectionLease<GDictionary> EmptyDictionaryLease(string reason) =>
        RuntimePlainPayload.ProjectDictionaryLease(
            new System.Collections.Generic.Dictionary<string, object>(StringComparer.Ordinal),
            $"GameRuntimeSettlementCommandHandler.empty.{reason}",
            LifetimeDomain.Request,
            $"GameRuntimeSettlementCommandHandler.empty.{reason}"
        );

    internal bool SetActiveSettlementState(
        string settlement_id,
        WorldMapSettlementStateData settlement_state
    )
    {
        return _has_runtime()
            && Port.SetActiveSettlementState(settlement_id, settlement_state);
    }

    internal PartyWarehouseService GetPartyWarehouseService()
    {
        return _has_runtime() ? Port.GetPartyWarehouseService() : null;
    }

    internal IReadOnlyDictionary<StringName, ItemDefinition> _GetItemDefsTyped()
    {
        return Port?.GetItemDefinitions()
            ?? new Dictionary<StringName, ItemDefinition>();
    }

    internal IReadOnlyDictionary<StringName, TraitDefinition> _GetTraitDefsTyped()
    {
        return Port?.GetTraitDefinitions()
            ?? new Dictionary<StringName, TraitDefinition>();
    }

    internal string GetItemDisplayName(StringName item_id)
    {
        return Port?.GetItemDisplayName(item_id) ?? item_id.ToString();
    }

    internal IReadOnlyDictionary<StringName, RecipeDefinition> GetRecipeDefsTyped()
    {
        return Port?.GetRecipeDefinitions()
            ?? new Dictionary<StringName, RecipeDefinition>();
    }

    internal IReadOnlyDictionary<StringName, QuestDefinition> GetQuestDefsTyped()
    {
        return Port?.GetQuestDefinitions()
            ?? new Dictionary<StringName, QuestDefinition>();
    }

    internal QuestDefinition GetQuestDefinition(StringName questId) =>
        Port?.GetQuestDefinition(questId);

    internal RuntimeCommandResult CommandAcceptQuestTyped(
        StringName questId,
        bool allowRepeatable
    ) =>
        Port?.CommandAcceptQuestTyped(questId, allowRepeatable)
        ?? RuntimeCommandResult.Failure(
            "运行时尚未初始化。",
            RuntimeCommandCode.RuntimeUnavailable
        );

    internal RuntimeCommandResult CommandClaimQuestTyped(StringName questId) =>
        Port?.CommandClaimQuestTyped(questId)
        ?? RuntimeCommandResult.Failure(
            "运行时尚未初始化。",
            RuntimeCommandCode.RuntimeUnavailable
        );

    internal RuntimeCommandResult CommandSubmitQuestItemTyped(
        StringName questId,
        StringName objectiveId
    ) =>
        Port?.CommandSubmitQuestItemTyped(questId, objectiveId)
        ?? RuntimeCommandResult.Failure(
            "运行时尚未初始化。",
            RuntimeCommandCode.RuntimeUnavailable
        );

    internal IReadOnlyDictionary<StringName, EnemyTemplateDefinition>
        GetEnemyTemplateDefinitionsTyped() =>
        Port?.GetEnemyTemplateDefinitions()
        ?? new Dictionary<StringName, EnemyTemplateDefinition>();

    internal BountyBoardWindowData GetActiveBountyBoardData() =>
        Port?.GetActiveBountyBoardData();

    internal void SetActiveBountyBoardRuntimeContext(BountyBoardWindowData data) =>
        Port?.SetActiveBountyBoardContext(data);

    internal void ClearActiveBountyBoardRuntimeContext() =>
        Port?.ClearActiveBountyBoardContext();

    internal NpcQuestOfferWindowData GetActiveNpcQuestOfferData() =>
        Port?.GetActiveNpcQuestOfferData();

    internal IReadOnlyDictionary<string, object> GetSettlementRecordSnapshotPlain(
        string settlementId
    )
    {
        return Port?.GetSettlementRecordSnapshotPlain(settlementId)
            ?? EmptyPlainDictionary();
    }

    internal IReadOnlyDictionary<string, object> GetActiveShopContextPlain() =>
        Port?.GetActiveShopContextPlain() ?? EmptyPlainDictionary();

    internal IReadOnlyDictionary<string, object> GetActiveContractBoardContextPlain() =>
        Port?.GetActiveContractBoardContextPlain() ?? EmptyPlainDictionary();

    internal IReadOnlyDictionary<string, object> GetActiveForgeContextPlain() =>
        Port?.GetActiveForgeContextPlain() ?? EmptyPlainDictionary();

    internal IReadOnlyDictionary<string, object> GetActiveStagecoachContextPlain() =>
        Port?.GetActiveStagecoachContextPlain() ?? EmptyPlainDictionary();

    internal void NotifyMisfortuneGuidanceOfForgeResult(
        StringName memberId,
        SettlementServiceResult result
    ) => Port?.HandleMisfortuneForgeResult(memberId, result);

    internal static SettlementSubmissionSource ReadSubmissionSource(GDictionary payload)
        => SettlementActionPayloadBuilder.ReadSubmissionSource(payload);

    private void DisposeServiceInstances(bool recreate)
    {
        _shop_service?.Dispose();
        _forge_service?.Dispose();

        if (recreate)
        {
            _shop_service = new SettlementShopService();
            _forge_service = new SettlementForgeService();
            _research_service = new SettlementResearchService();
            return;
        }

        _shop_service = null;
        _forge_service = null;
        _research_service = null;
    }

    internal AttributeSnapshot GetMemberAttributeSnapshot(StringName member_id)
    {
        return _has_runtime() ? Port.GetMemberAttributeSnapshot(member_id) : null;
    }

    internal string GetMemberDisplayName(StringName member_id)
    {
        return Port?.GetMemberDisplayName(member_id) ?? member_id.ToString();
    }

    internal void OpenPartyWarehouseWindow(string entry_label)
    {
        if (_has_runtime())
        {
            Port.OpenPartyWarehouseWindow(entry_label);
        }
    }

    internal void EnqueuePendingCharacterRewardsTyped(
        IEnumerable<PendingCharacterReward> rewards
    )
    {
        if (_has_runtime())
        {
            Port.EnqueuePendingCharacterRewardsTyped(rewards);
        }
    }

    internal void RecordMemberAchievementEvent(
        StringName member_id,
        StringName event_id,
        int value,
        StringName detail_id = null
    )
    {
        detail_id ??= new StringName("");
        if (_has_runtime())
        {
            Port.RecordMemberAchievementEvent(member_id, event_id, value, detail_id);
        }
    }

    internal void SyncPartyStateFromCharacterManagement()
    {
        if (_has_runtime())
        {
            Port.SyncPartyStateFromCharacterManagement();
        }
    }

    internal int PersistPartyState()
    {
        return _has_runtime() ? Port.PersistPartyState() : (int)Error.Unavailable;
    }

    internal int PersistWorldData()
    {
        return _has_runtime() ? Port.PersistWorldData() : (int)Error.Unavailable;
    }

    internal int PersistPlayerCoord()
    {
        return _has_runtime() ? Port.PersistPlayerCoord() : (int)Error.Unavailable;
    }

    internal bool IsSettlementVisibleToPlayer(GDictionary settlement)
    {
        if (!_has_runtime())
        {
            return false;
        }
        Vector2I origin = ReadVector2I(settlement, "origin");
        Vector2I footprintSize = ReadVector2I(settlement, "footprint_size", Vector2I.One);
        int width = Math.Max(footprintSize.X, 1);
        int height = Math.Max(footprintSize.Y, 1);
        string factionId = GetPlayerFactionId();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (Port.IsWorldCoordVisible(origin + new Vector2I(x, y), factionId))
                {
                    return true;
                }
            }
        }
        return false;
    }

    internal string GetPlayerFactionId()
    {
        return Port?.GetPlayerFactionId() ?? "player";
    }

    internal void AdvanceWorldTimeBySteps(int delta_steps)
    {
        if (_has_runtime())
        {
            Port.AdvanceWorldTimeBySteps(delta_steps);
        }
    }

    internal void RefreshWorldVisibility()
    {
        if (_has_runtime())
        {
            Port.RefreshWorldVisibility();
        }
    }

    internal int GetWorldStep()
    {
        return _has_runtime() ? Port.GetWorldStep() : 0;
    }

    internal void SetPlayerCoord(Vector2I coord)
    {
        if (_has_runtime())
        {
            Port.SetPlayerCoord(coord);
        }
    }

    internal void SetSelectedCoord(Vector2I coord)
    {
        if (_has_runtime())
        {
            Port.SetSelectedCoord(coord);
        }
    }

    internal void ClearSettlementEntryContext(bool reset_selected = true)
    {
        if (_has_runtime())
        {
            Port.ClearSettlementEntryContext(reset_selected);
        }
    }

    internal RuntimeModalKind GetActiveModalKind()
    {
        return Port?.GetActiveModalKind() ?? RuntimeModalKind.None;
    }

    internal void SetActiveModalKind(RuntimeModalKind modalKind)
    {
        if (_has_runtime())
        {
            Port.SetActiveModalKind(modalKind);
        }
    }

    internal bool PresentPendingRewardIfReady()
    {
        return _has_runtime() && Port.PresentPendingRewardIfReady();
    }

    internal void SetActiveShopContext(GDictionary context)
    {
        if (_has_runtime())
        {
            Port.SetActiveShopContext(context);
        }
    }

    internal void SetActiveContractBoardContext(GDictionary context)
    {
        if (_has_runtime())
        {
            Port.SetActiveContractBoardContext(context);
        }
    }

    internal void SetActiveNpcQuestOfferContext(NpcQuestOfferWindowData data)
    {
        if (_has_runtime())
        {
            Port.SetActiveNpcQuestOfferContext(data);
        }
    }

    internal void SetActiveForgeContext(GDictionary context)
    {
        if (_has_runtime())
        {
            Port.SetActiveForgeContext(context);
        }
    }

    internal void ClearActiveShopContext()
    {
        if (_has_runtime())
        {
            Port.ClearActiveShopContext();
        }
    }

    internal void ClearActiveContractBoardContext()
    {
        if (_has_runtime())
        {
            Port.ClearActiveContractBoardContext();
        }
    }

    internal void ClearActiveNpcQuestOfferContext()
    {
        if (_has_runtime())
        {
            Port.ClearActiveNpcQuestOfferContext();
        }
    }

    internal void ClearActiveForgeContext()
    {
        if (_has_runtime())
        {
            Port.ClearActiveForgeContext();
        }
    }

    internal GodotProjectionLease<GDictionary> GetActiveShopContextLease()
    {
        return _has_runtime()
            ? Port.GetActiveShopContextLease()
            : EmptyContextLease("shop");
    }

    internal GodotProjectionLease<GDictionary> GetActiveContractBoardContextLease()
    {
        return _has_runtime()
            ? Port.GetActiveContractBoardContextLease()
            : EmptyContextLease("contract_board");
    }

    internal GodotProjectionLease<GDictionary> GetActiveForgeContextLease()
    {
        return _has_runtime()
            ? Port.GetActiveForgeContextLease()
            : EmptyContextLease("forge");
    }

    internal void SetActiveStagecoachContext(GDictionary context)
    {
        if (_has_runtime())
        {
            Port.SetActiveStagecoachContext(context);
        }
    }

    internal void ClearActiveStagecoachContext()
    {
        if (_has_runtime())
        {
            Port.ClearActiveStagecoachContext();
        }
    }

    internal GodotProjectionLease<GDictionary> GetActiveStagecoachContextLease()
    {
        return _has_runtime()
            ? Port.GetActiveStagecoachContextLease()
            : EmptyContextLease("stagecoach");
    }

    private static GodotProjectionLease<GDictionary> EmptyContextLease(string label) =>
        RuntimePlainPayload.ProjectDictionaryLease(
            new Dictionary<string, object>(StringComparer.Ordinal),
            $"GameRuntimeSettlementCommandHandler.{label}",
            LifetimeDomain.Request,
            $"GameRuntimeSettlementCommandHandler.{label}"
        );

    internal static IEnumerable<GDictionary> Dictionaries(GArray values)
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
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.String)
        {
            value = variant.AsString();
            return true;
        }
        value = "";
        return false;
    }

    internal static string ReadString(GDictionary data, string key, string fallback = "")
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
            return fallback;
        return TryAsString(data[key], out string value) ? value : fallback;
    }

    internal static Variant ReadVariant(GDictionary data, string key)
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
            return default;
        return data[key];
    }

    internal static bool ReadBool(GDictionary data, string key, bool fallback = false)
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

    internal static GArray ReadArray(GDictionary data, string key)
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
            return new GArray();
        return TryAsArray(data[key], out GArray value) ? value : new GArray();
    }

    internal static GDictionary ReadDictionary(GDictionary data, string key)
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
            return new GDictionary();
        return TryAsDictionary(data[key], out GDictionary value) ? value : new GDictionary();
    }

    internal static Vector2I ReadVector2I(
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
        if (rawValue is string stringValue)
        {
            value = new StringName(stringValue);
            return true;
        }
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.String)
        {
            value = new StringName(variant.AsString());
            return true;
        }
        value = "";
        return false;
    }

    internal static StringName ReadStringName(GDictionary data, string key)
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

    private static T ResolveWeakRef<T>(WeakReference<T> weakRef)
        where T : class
    {
        if (
            weakRef == null
            || !weakRef.TryGetTarget(out T target)
        )
        {
            return null;
        }
        return target;
    }
}
