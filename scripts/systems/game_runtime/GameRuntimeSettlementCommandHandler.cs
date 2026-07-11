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
public sealed class GameRuntimeSettlementCommandHandler : IDisposable
{
    private const int REST_FULL_COST = 50;
    private const int INTEL_NETWORK_COST = 50;
    private const int STAGECOACH_COST_PER_STEP = 10;
    private const int VILLAGE_RUMOR_RANGE = 5;
    private const int INTEL_NETWORK_RANGE = 8;
    private const string PERSIST_FAILURE_ROLLBACK_MESSAGE = "存档提交失败，操作已回滚。";
    private static readonly StringName NPC_OFFER_LISTING_CHANNEL = "npc_offer";

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

    private WeakReference<GameRuntimeFacade> _runtimeRef;

    private GameRuntimeFacade Runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<GameRuntimeFacade>(value) : null;
    }

    private SettlementShopService _shop_service = new();
    private SettlementForgeService _forge_service = new();
    private SettlementResearchService _research_service = new();
    private readonly QuestAcceptRequirementEvaluator _quest_accept_evaluator = new();

    private sealed class SettlementActionValidationResult
    {
        private readonly Dictionary<string, object> _serviceEntry = new(StringComparer.Ordinal);
        public bool Ok { get; }
        public string Message { get; }
        internal GDictionary ServiceEntry =>
            RuntimePlainPayload.ProjectDictionary(
                _serviceEntry,
                "GameRuntimeSettlementCommandHandler.SettlementActionValidationResult.serviceEntry"
            );

        private SettlementActionValidationResult(
            bool ok,
            string message,
            GDictionary serviceEntry = null
        )
        {
            Ok = ok;
            Message = message ?? "";
            ReplacePlainPayload(
                _serviceEntry,
                serviceEntry,
                "GameRuntimeSettlementCommandHandler.SettlementActionValidationResult.serviceEntry"
            );
        }

        internal static SettlementActionValidationResult Success(GDictionary serviceEntry = null) =>
            new(true, "", serviceEntry);

        internal static SettlementActionValidationResult Failure(string message) =>
            new(false, message);
    }

    private sealed class ContractBoardQuestData
    {
        public QuestDefinition QuestDefinition { get; }
        public StringName QuestId { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public string ProviderInteractionId { get; }
        public IReadOnlyList<QuestObjectiveDefinition> ObjectiveEntries { get; }
        public IReadOnlyList<QuestRewardDefinition> RewardEntries { get; }
        public bool IsRepeatable { get; }
        public string AcceptDialogueText { get; }
        public string AcceptFeedbackSuccess { get; }
        public string AcceptFeedbackFailure { get; }
        public string AcceptConfirmationText { get; }

        internal ContractBoardQuestData(
            QuestDefinition questDefinition,
            string displayName,
            string description,
            string providerInteractionId,
            IReadOnlyList<QuestObjectiveDefinition> objectiveEntries,
            IReadOnlyList<QuestRewardDefinition> rewardEntries
        )
        {
            QuestDefinition = questDefinition;
            QuestId = questDefinition?.QuestId ?? "";
            DisplayName = displayName ?? "";
            Description = description ?? "";
            ProviderInteractionId = providerInteractionId ?? "";
            ObjectiveEntries =
                objectiveEntries ?? System.Array.Empty<QuestObjectiveDefinition>();
            RewardEntries = rewardEntries ?? System.Array.Empty<QuestRewardDefinition>();
            IsRepeatable = questDefinition?.IsRepeatable ?? false;
            AcceptDialogueText = questDefinition?.AcceptDialogueText ?? "";
            AcceptFeedbackSuccess = questDefinition?.AcceptFeedbackSuccess ?? "";
            AcceptFeedbackFailure = questDefinition?.AcceptFeedbackFailure ?? "";
            AcceptConfirmationText = questDefinition?.AcceptConfirmationText ?? "";
        }
    }

    private sealed class SettlementServiceEntryResolution
    {
        private readonly Dictionary<string, object> _serviceEntry = new(StringComparer.Ordinal);
        internal GDictionary ServiceEntry =>
            RuntimePlainPayload.ProjectDictionary(
                _serviceEntry,
                "GameRuntimeSettlementCommandHandler.SettlementServiceEntryResolution.serviceEntry"
            );
        public bool IsEnabled { get; }
        public string DisabledReason { get; }
        public bool Found => _serviceEntry.Count != 0;

        private SettlementServiceEntryResolution(
            GDictionary serviceEntry,
            bool isEnabled,
            string disabledReason
        )
        {
            ReplacePlainPayload(
                _serviceEntry,
                serviceEntry,
                "GameRuntimeSettlementCommandHandler.SettlementServiceEntryResolution.serviceEntry"
            );
            IsEnabled = isEnabled;
            DisabledReason = disabledReason ?? "";
        }

        internal static SettlementServiceEntryResolution Missing() => new(null, false, "");

        internal static SettlementServiceEntryResolution FromServiceData(
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

    private readonly struct SettlementPersistResult
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

    private sealed class SettlementCommandRollbackSnapshot
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
        public RuntimeTransactionRollbackState RuntimeState { get; }
        public RuntimeModalKind ActiveModalKind { get; }
        public string ActiveSettlementId { get; }
        public string SettlementFeedbackText { get; }
        public Vector2I SelectedCoord { get; }
        public bool SettlementEntryActive { get; }
        public Vector2I SettlementEntrySourceCoord { get; }
        public Vector2I SettlementEntryTargetCoord { get; }
        internal GDictionary ActiveShopContext =>
            RuntimePlainPayload.ProjectDictionary(
                _activeShopContext,
                "GameRuntimeSettlementCommandHandler.RollbackSnapshot.activeShopContext"
            );
        internal GDictionary ActiveContractBoardContext =>
            RuntimePlainPayload.ProjectDictionary(
                _activeContractBoardContext,
                "GameRuntimeSettlementCommandHandler.RollbackSnapshot.activeContractBoardContext"
            );
        internal GDictionary ActiveForgeContext =>
            RuntimePlainPayload.ProjectDictionary(
                _activeForgeContext,
                "GameRuntimeSettlementCommandHandler.RollbackSnapshot.activeForgeContext"
            );
        internal GDictionary ActiveStagecoachContext =>
            RuntimePlainPayload.ProjectDictionary(
                _activeStagecoachContext,
                "GameRuntimeSettlementCommandHandler.RollbackSnapshot.activeStagecoachContext"
            );
        internal NpcQuestOfferWindowData ActiveNpcQuestOfferContext => _activeNpcQuestOfferContext;

        internal SettlementCommandRollbackSnapshot(
            RuntimeTransactionRollbackState runtimeState,
            RuntimeModalKind activeModalKind,
            string activeSettlementId,
            string settlementFeedbackText,
            Vector2I selectedCoord,
            bool settlementEntryActive,
            Vector2I settlementEntrySourceCoord,
            Vector2I settlementEntryTargetCoord,
            GDictionary activeShopContext,
            GDictionary activeContractBoardContext,
            GDictionary activeForgeContext,
            GDictionary activeStagecoachContext,
            NpcQuestOfferWindowData activeNpcQuestOfferContext
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
                activeShopContext,
                "GameRuntimeSettlementCommandHandler.RollbackSnapshot.activeShopContext"
            );
            ReplacePlainPayload(
                _activeContractBoardContext,
                activeContractBoardContext,
                "GameRuntimeSettlementCommandHandler.RollbackSnapshot.activeContractBoardContext"
            );
            ReplacePlainPayload(
                _activeForgeContext,
                activeForgeContext,
                "GameRuntimeSettlementCommandHandler.RollbackSnapshot.activeForgeContext"
            );
            ReplacePlainPayload(
                _activeStagecoachContext,
                activeStagecoachContext,
                "GameRuntimeSettlementCommandHandler.RollbackSnapshot.activeStagecoachContext"
            );
            _activeNpcQuestOfferContext = activeNpcQuestOfferContext;
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

    internal void SetupRuntime(GameRuntimeFacade runtime)
    {
        Runtime = runtime;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Runtime = null;
        DisposeServiceInstances(recreate: false);
    }

    internal void DisposeRuntime()
    {
        Runtime = null;
        DisposeServiceInstances(recreate: true);
    }

    private QuestAcceptContext _build_quest_accept_context()
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
        GDictionary settlement = GetSettlementRecord(settlementId);
        if (settlement == null)
            return 0;
        return ReadInt(settlement, "tier", 0);
    }

    internal GDictionary GetSettlementWindowData(string settlement_id = "")
    {
        if (!_has_runtime())
        {
            return new GDictionary();
        }
        string targetId = !string.IsNullOrEmpty(settlement_id)
            ? settlement_id
            : ResolveCommandSettlementId();
        GDictionary settlement = GetSettlementRecord(targetId);
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
            ["default_member_id"] = ResolveDefaultSettlementMemberId().ToString(),
            ["state_summary_text"] = _build_settlement_state_summary(settlementState),
            ["feedback_text"] = _build_settlement_window_feedback_text(),
        };
    }

    internal IReadOnlyDictionary<string, object> GetSettlementHeadlessFactsPlain(
        string settlementId
    )
    {
        if (!_has_runtime())
            return EmptyPlainDictionary();

        string targetId = settlementId ?? "";
        if (string.IsNullOrEmpty(targetId))
            return EmptyPlainDictionary();
        IReadOnlyDictionary<string, object> settlement = GetSettlementRecordSnapshotPlain(
            targetId
        );
        if (settlement.Count == 0)
            return EmptyPlainDictionary();

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["settlement_id"] = ReadPlainString(settlement, "settlement_id"),
            ["display_name"] = ReadPlainString(settlement, "display_name"),
            ["tier_name"] = ReadPlainString(settlement, "tier_name"),
            ["faction_id"] = ReadPlainString(settlement, "faction_id"),
            ["services"] = BuildSettlementServiceIdentityFactsPlain(settlement),
        };
    }

    internal GDictionary GetShopWindowData()
    {
        GDictionary context = GetActiveShopContext();
        if (context.Count == 0)
        {
            return new GDictionary();
        }
        var entries = new GDictArray();
        foreach (GDictionary entryData in Dictionaries(ReadArray(context, "buy_entries")))
        {
            GDictionary entry = RuntimePayloadCopy.Dictionary(
                entryData,
                "GameRuntimeSettlementCommandHandler.GetShopWindowData.buy_entry"
            );
            if (!entry.ContainsKey("is_enabled"))
            {
                entry["is_enabled"] = false;
            }
            entries.Add(entry);
        }
        foreach (GDictionary entryData in Dictionaries(ReadArray(context, "sell_entries")))
        {
            GDictionary entry = RuntimePayloadCopy.Dictionary(
                entryData,
                "GameRuntimeSettlementCommandHandler.GetShopWindowData.sell_entry"
            );
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
        context["panel_kind"] = SettlementPanelKinds.ToPayloadValue(SettlementPanelKind.Shop);
        context["show_member_selector"] = true;
        context["party_state"] = GetPartyState()?.ToDictionary() ?? new GDictionary();
        context["member_options"] = _build_member_options();
        context["default_member_id"] = ResolveDefaultSettlementMemberId().ToString();
        return context;
    }

    internal IReadOnlyDictionary<string, object> GetShopWindowDataSnapshotPlain()
    {
        Dictionary<string, object> context = CloneActiveShopContextPlain();
        if (context.Count == 0)
            return context;

        var entries = new List<object>();
        AppendWindowEntriesPlain(entries, context, "buy_entries");
        AppendWindowEntriesPlain(entries, context, "sell_entries");
        context["entries"] = entries;
        context["summary_text"] = $"持有金币：{ReadPlainInt(context, "gold")}";
        context["state_summary_text"] = ReadPlainString(context, "feedback_text");
        context["action_id"] = "shop:trade";
        context["panel_kind"] = SettlementPanelKinds.ToPayloadValue(
            SettlementPanelKind.Shop
        );
        context["show_member_selector"] = true;
        context.Remove("party_state");
        context["member_options"] = BuildMemberOptionsSnapshotPlain();
        context["default_member_id"] = ResolveDefaultSettlementMemberId().ToString();
        return context;
    }

    internal GDictionary GetContractBoardWindowData()
    {
        GDictionary context = GetActiveContractBoardContext();
        if (context.Count == 0)
        {
            return new GDictionary();
        }
        return RuntimePayloadCopy.Dictionary(
            context,
            "GameRuntimeSettlementCommandHandler.GetContractBoardWindowData"
        );
    }

    internal IReadOnlyDictionary<string, object> GetContractBoardWindowDataSnapshotPlain()
    {
        Dictionary<string, object> context = CloneActiveContractBoardContextPlain();
        context.Remove("party_state");
        return context;
    }

    internal GDictionary GetNpcQuestOfferWindowData()
    {
        NpcQuestOfferWindowData data = GetActiveNpcQuestOfferContextTyped();
        if (data == null)
        {
            return new GDictionary();
        }
        return RuntimePayloadCopy.Dictionary(
            data.ToDictionary(),
            "GameRuntimeSettlementCommandHandler.GetNpcQuestOfferWindowData"
        );
    }

    internal IReadOnlyDictionary<string, object> GetNpcQuestOfferWindowDataSnapshotPlain()
    {
        NpcQuestOfferWindowData data = GetActiveNpcQuestOfferContextTyped();
        return data?.BuildSnapshotPlain() ?? EmptyPlainDictionary();
    }

    internal NpcQuestOfferWindowData GetActiveNpcQuestOfferContextTyped()
    {
        return _has_runtime() ? Runtime.GetActiveNpcQuestOfferData() : null;
    }

    internal GDictionary GetForgeWindowData()
    {
        GDictionary context = GetActiveForgeContext();
        if (context.Count == 0)
        {
            return new GDictionary();
        }
        return RuntimePayloadCopy.Dictionary(
            context,
            "GameRuntimeSettlementCommandHandler.GetForgeWindowData"
        );
    }

    internal IReadOnlyDictionary<string, object> GetForgeWindowDataSnapshotPlain()
    {
        Dictionary<string, object> context = CloneActiveForgeContextPlain();
        if (context.Count == 0)
        {
            Dictionary<string, object> shopContext = CloneActiveShopContextPlain();
            if (WindowDataMatchesPanelKindPlain(shopContext, SettlementPanelKind.Forge))
                context = shopContext;
        }
        context.Remove("party_state");
        return context;
    }

    internal GDictionary GetStagecoachWindowData()
    {
        GDictionary context = GetActiveStagecoachContext();
        if (context.Count == 0)
        {
            return new GDictionary();
        }
        var entries = new GDictArray();
        foreach (GDictionary entryData in Dictionaries(ReadArray(context, "destinations")))
        {
            GDictionary entry = RuntimePayloadCopy.Dictionary(
                entryData,
                "GameRuntimeSettlementCommandHandler.GetStagecoachWindowData.entry"
            );
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
        context["panel_kind"] = SettlementPanelKinds.ToPayloadValue(SettlementPanelKind.Stagecoach);
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
        context["party_state"] = GetPartyState()?.ToDictionary() ?? new GDictionary();
        context["member_options"] = _build_member_options();
        context["default_member_id"] = ResolveDefaultSettlementMemberId().ToString();
        return context;
    }

    internal IReadOnlyDictionary<string, object> GetStagecoachWindowDataSnapshotPlain()
    {
        Dictionary<string, object> context = CloneActiveStagecoachContextPlain();
        if (context.Count == 0)
            return context;

        var entries = new List<object>();
        AppendWindowEntriesPlain(entries, context, "destinations");
        int gold = ReadPlainInt(context, "gold");
        context["entries"] = entries;
        context["summary_text"] = $"持有金币：{gold}";
        context["state_summary_text"] = ReadPlainString(context, "feedback_text");
        context["action_id"] = "stagecoach:travel";
        context["panel_kind"] = SettlementPanelKinds.ToPayloadValue(
            SettlementPanelKind.Stagecoach
        );
        context["meta"] =
            $"驿站：{ReadPlainString(context, "origin_name")}  |  金币：{gold}";
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
        context["member_options"] = BuildMemberOptionsSnapshotPlain();
        context["default_member_id"] = ResolveDefaultSettlementMemberId().ToString();
        return context;
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandExecuteSettlementActionRuntimeTyped(
        string action_id,
        GDictionary payload = null
    )
    {
        GDictionary payloadData = payload ?? new GDictionary();
        SettlementSubmissionSource source = ReadSubmissionSource(payloadData);
        if (
            source == SettlementSubmissionSource.ContractBoard
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

    internal GameRuntimeFacade.RuntimeCommandResult CommandExecuteSettlementActionRuntimeTyped(
        SettlementActionRequest request
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
        GDictionary serviceEntry = validation.ServiceEntry;
        if (serviceEntry.Count == 0)
        {
            return RuntimeCommandError("当前据点未开放该服务。");
        }
        GDictionary mergedPayload = BuildSettlementActionPayloadFromRequest(
            serviceEntry,
            request
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

    internal GameRuntimeFacade.RuntimeCommandResult ExecuteSettlementAction(
        SettlementActionRequest request
    ) => CommandExecuteSettlementActionRuntimeTyped(request);

    private GameRuntimeFacade.RuntimeCommandResult CommandExecuteSettlementModalActionRuntimeTyped(
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
        GDictionary serviceEntry = validation.ServiceEntry;
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

    internal GameRuntimeFacade.RuntimeCommandResult CommandShopBuyTyped(
        StringName item_id,
        int quantity
    )
    {
        if (GetActiveModalKind() != RuntimeModalKind.Shop)
        {
            return RuntimeCommandError("当前没有打开据点商店。");
        }
        GDictionary context = GetActiveShopContext();
        if (context.Count == 0)
        {
            return RuntimeCommandError("当前商店上下文缺失。");
        }
        string settlementId = ReadString(context, "settlement_id");
        SettlementCommandRollbackSnapshot rollbackSnapshot = CaptureRollbackSnapshot();
        GDictionary settlementState = _get_or_create_settlement_state(settlementId);
        SettlementShopTradeResult result = _shop_service.BuyTyped(
            ReadString(context, "interaction_script_id"),
            GetSettlementRecord(settlementId),
            settlementState,
            _GetItemDefsTyped(),
            GetPartyWarehouseService(),
            GetPartyState(),
            item_id,
            quantity
        );
        if (!result.Success)
        {
            _refresh_active_shop_context();
            return RuntimeCommandError(
                string.IsNullOrEmpty(result.Message) ? "购买失败。" : result.Message
            );
        }
        SetActiveSettlementState(settlementId, settlementState);
        SettlementPersistResult persistResult = PersistChangesTyped(
            true,
            true,
            false,
            rollbackSnapshot
        );
        string message = string.IsNullOrEmpty(result.Message) ? "购买成功。" : result.Message;
        if (!persistResult.Ok)
            return RuntimeCommandPersistFailure();
        _refresh_active_shop_context();
        UpdateStatus(message);
        return RuntimeCommandOk(message);
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandShopSellTyped(
        StringName item_id,
        int quantity,
        StringName instance_id = null
    )
    {
        instance_id ??= new StringName("");
        if (GetActiveModalKind() != RuntimeModalKind.Shop)
        {
            return RuntimeCommandError("当前没有打开据点商店。");
        }
        GDictionary context = GetActiveShopContext();
        if (context.Count == 0)
        {
            return RuntimeCommandError("当前商店上下文缺失。");
        }
        string settlementId = ReadString(context, "settlement_id");
        SettlementCommandRollbackSnapshot rollbackSnapshot = CaptureRollbackSnapshot();
        GDictionary settlementState = _get_or_create_settlement_state(settlementId);
        SettlementShopTradeResult result = _shop_service.SellTyped(
            ReadString(context, "interaction_script_id"),
            GetSettlementRecord(settlementId),
            settlementState,
            _GetItemDefsTyped(),
            GetPartyWarehouseService(),
            GetPartyState(),
            item_id,
            quantity,
            instance_id
        );
        if (!result.Success)
        {
            _refresh_active_shop_context();
            return RuntimeCommandError(
                string.IsNullOrEmpty(result.Message) ? "出售失败。" : result.Message
            );
        }
        SetActiveSettlementState(settlementId, settlementState);
        SettlementPersistResult persistResult = PersistChangesTyped(
            true,
            true,
            false,
            rollbackSnapshot
        );
        string message = string.IsNullOrEmpty(result.Message) ? "出售成功。" : result.Message;
        if (!persistResult.Ok)
            return RuntimeCommandPersistFailure();
        _refresh_active_shop_context();
        UpdateStatus(message);
        return RuntimeCommandOk(message);
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandStagecoachTravelTyped(
        string settlement_id
    )
    {
        if (GetActiveModalKind() != RuntimeModalKind.Stagecoach)
        {
            return RuntimeCommandError("当前没有打开驿站路线窗口。");
        }
        GDictionary context = GetActiveStagecoachContext();
        if (context.Count == 0)
        {
            return RuntimeCommandError("当前没有可用的驿站路线。");
        }
        StagecoachDestinationData destination = ResolveStagecoachDestinationTyped(
            context,
            settlement_id
        );
        if (destination == null)
        {
            return RuntimeCommandError("当前驿站路线中不存在该目的地。");
        }
        if (!destination.CanTravel)
        {
            return RuntimeCommandError(
                !string.IsNullOrEmpty(destination.DisabledReason)
                    ? destination.DisabledReason
                    : "当前无法前往该据点。"
            );
        }
        PartyState partyState = GetPartyState();
        if (partyState == null)
        {
            return RuntimeCommandError("当前不存在队伍数据。");
        }
        SettlementCommandRollbackSnapshot rollbackSnapshot = CaptureRollbackSnapshot();
        int travelCost = destination.TravelCost;
        if (!partyState.SpendGold(travelCost))
        {
            return RuntimeCommandError("金币不足，无法启程。");
        }
        string destinationId = destination.SettlementId;
        GDictionary destinationRecord = GetSettlementRecord(destinationId);
        if (destinationRecord.Count == 0)
        {
            return RuntimeCommandError("未找到目标据点。");
        }
        Vector2I destinationCoord = destination.Coord;
        ClearSettlementEntryContext(false);
        SetPlayerCoord(destinationCoord);
        SetSelectedCoord(destinationCoord);
        _mark_settlement_visited(destinationId);
        ClearActiveStagecoachContext();
        SetActiveModalKind(RuntimeModalKind.Settlement);
        SetActiveSettlementId(destinationId);
        SetSettlementFeedbackText(
            $"驿队将你送到了 {ReadString(destinationRecord, "display_name", destinationId)}。"
        );
        RefreshWorldVisibility();
        SettlementPersistResult persistResult = PersistChangesTyped(
            true,
            true,
            true,
            rollbackSnapshot
        );
        string message =
            $"已从 {ReadString(context, "origin_name", "当前据点")} 抵达 {ReadString(destinationRecord, "display_name", destinationId)}，花费 {travelCost} 金。";
        if (!persistResult.Ok)
            return RuntimeCommandPersistFailure();
        UpdateStatus(message);
        return RuntimeCommandOk(message);
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
        GDictionary serviceEntry = validation.ServiceEntry;
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
                CaptureRollbackSnapshot();
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
        if (_try_open_npc_quest_offer(settlement_id, action_id, payload, out GDictionary npcResult))
        {
            return npcResult;
        }
        if (_is_npc_quest_offer_modal_submission(payload))
        {
            return _submit_npc_quest_offer_action(settlement_id, action_id, payload);
        }
        if (QuestProviderContentRules.IsSupportedProviderId(interactionScriptId))
        {
            if (_is_contract_board_modal_submission(payload))
            {
                return _submit_contract_board_quest_action(settlement_id, action_id, payload);
            }
            _open_contract_board_modal(settlement_id, payload);
            return CommandOk(
                $"已打开 {ReadString(payload, "facility_name", "据点任务板")} 的任务板。"
            );
        }
        if (SHOP_INTERACTION_IDS.Contains(interactionScriptId))
        {
            _open_shop_modal(settlement_id, payload);
            return CommandOk(
                $"已打开 {ReadString(payload, "facility_name", "据点商店")} 的商店。"
            );
        }
        if (_is_forge_interaction(interactionScriptId) && !_is_forge_modal_submission(payload))
        {
            _open_forge_modal(settlement_id, payload);
            return CommandOk(
                $"已打开 {ReadString(payload, "facility_name", "锻造设施")} 的锻造界面。"
            );
        }
        if (STAGECOACH_INTERACTION_IDS.Contains(interactionScriptId))
        {
            _open_stagecoach_modal(settlement_id, payload);
            return CommandOk(
                $"已打开 {ReadString(payload, "facility_name", "驿站")} 的驿站路线。"
            );
        }
        SettlementCommandRollbackSnapshot rollbackSnapshot = CaptureRollbackSnapshot();
        SettlementServiceResult serviceResult = ExecuteSettlementActionTyped(
            settlement_id,
            action_id,
            payload
        );
        string message = serviceResult?.Message ?? "交互已完成。";
        SetSettlementFeedbackText(message);
        bool actionSucceeded = serviceResult?.Success ?? false;
        if (_is_forge_interaction(interactionScriptId))
        {
            _refresh_active_forge_context(message);
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

    internal void OnNpcQuestOfferWindowClosed()
    {
        ClearActiveNpcQuestOfferContext();
        SetActiveModalKind(RuntimeModalKind.Settlement);
        UpdateStatus("已关闭 NPC 委托面板，返回据点服务。");
    }

    internal void OnForgeWindowClosed()
    {
        GDictionary context = GetActiveForgeContext();
        string forgeLabel = _resolve_forge_service_label(context);
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
        GDictionary settlement = GetSelectedSettlement();
        return ReadString(settlement, "settlement_id");
    }

    private GDictionary BuildSettlementActionPayloadFromRequest(
        GDictionary service_data,
        SettlementActionRequest request
    )
    {
        if (service_data.Count == 0)
        {
            return new GDictionary();
        }
        GDictionary payload = BuildSettlementActionBasePayload(
            request.ActionId.ToString(),
            service_data
        );
        payload["settlement_id"] = request.SettlementId.ToString();
        payload["service_id"] = request.ServiceId.ToString();
        if (request.MemberId != "")
        {
            payload["member_id"] = request.MemberId.ToString();
            payload["default_member_id"] = request.MemberId.ToString();
        }
        if (request.Quantity > 0)
        {
            payload["request_quantity"] = request.Quantity;
            payload["quantity"] = request.Quantity;
        }
        string submissionSource = SettlementSubmissionSources.ToPayloadValue(request.Source);
        if (!string.IsNullOrEmpty(submissionSource))
            payload["submission_source"] = submissionSource;
        EnsureSettlementActionMemberId(payload);
        return payload;
    }

    private GDictionary BuildSettlementModalActionPayloadFromServiceEntry(
        string action_id,
        GDictionary service_data,
        GDictionary overrides
    )
    {
        if (service_data.Count == 0)
        {
            return new GDictionary();
        }
        SettlementSubmissionSource source = ReadSubmissionSource(overrides);
        if (
            source != SettlementSubmissionSource.ContractBoard
            && source != SettlementSubmissionSource.Forge
            && source != SettlementSubmissionSource.NpcQuestOffer
        )
        {
            return new GDictionary();
        }
        SettlementActionRequest request = BuildSettlementActionRequestFromBoundaryPayload(
            ResolveCommandSettlementId(),
            action_id,
            overrides ?? new GDictionary(),
            source
        );
        GDictionary payload = BuildSettlementActionPayloadFromRequest(service_data, request);
        CopyIfPresent(payload, overrides, "submission_source");
        CopyIfPresent(payload, overrides, "panel_kind");
        CopyIfPresent(payload, overrides, "state_summary_text");
        if (source == SettlementSubmissionSource.ContractBoard)
        {
            CopyIfPresent(payload, overrides, "quest_id");
            CopyIfPresent(payload, overrides, "provider_interaction_id");
            CopyIfPresent(payload, overrides, "confirm_accept");
        }
        else if (source == SettlementSubmissionSource.Forge)
        {
            CopyIfPresent(payload, overrides, "recipe_id");
        }
        else if (source == SettlementSubmissionSource.NpcQuestOffer)
        {
            CopyIfPresent(payload, overrides, "quest_id");
            CopyIfPresent(payload, overrides, "confirm_accept");
        }
        return payload;
    }

    private GDictionary BuildSettlementActionBasePayload(
        string action_id,
        GDictionary service_data
    )
    {
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
        CopyIfPresent(payload, service_data, "state_label");
        CopyIfPresent(payload, service_data, "cost_label");
        CopyIfPresent(payload, service_data, "summary_text");
        CopyIfPresent(payload, service_data, "disabled_reason");
        CopyIfPresent(payload, service_data, "panel_kind");
        CopyIfPresent(payload, service_data, "interaction_type");
        CopyIfPresent(payload, service_data, "is_enabled");
        return payload;
    }

    private void EnsureSettlementActionMemberId(GDictionary payload)
    {
        if (string.IsNullOrEmpty(ReadString(payload, "member_id")))
        {
            StringName memberId = ResolveDefaultSettlementMemberId();
            if (memberId != "")
            {
                payload["member_id"] = memberId.ToString();
                if (string.IsNullOrEmpty(ReadString(payload, "default_member_id")))
                    payload["default_member_id"] = memberId.ToString();
            }
        }
    }

    private SettlementActionRequest BuildSettlementActionRequestFromBoundaryPayload(
        string fallback_settlement_id,
        string action_id,
        GDictionary payload,
        SettlementSubmissionSource default_source
    )
    {
        GDictionary payloadData = payload ?? new GDictionary();
        string settlementId = ReadString(
            payloadData,
            "settlement_id",
            fallback_settlement_id ?? ""
        ).StripEdges();
        string serviceId = ReadString(payloadData, "service_id", action_id ?? "").StripEdges();
        string actionId = (action_id ?? "").StripEdges();
        StringName memberId = ReadStringName(payloadData, "member_id");
        int quantity = ReadInt(
            payloadData,
            "request_quantity",
            ReadInt(payloadData, "quantity", 0)
        );
        if (quantity < 0)
            quantity = 0;
        SettlementSubmissionSource source = ReadSubmissionSource(payloadData);
        if (source == SettlementSubmissionSource.None && default_source != SettlementSubmissionSource.None)
            source = default_source;
        return new SettlementActionRequest(
            new StringName(settlementId),
            new StringName(string.IsNullOrEmpty(serviceId) ? actionId : serviceId),
            new StringName(actionId),
            memberId,
            quantity,
            source
        );
    }

    private static void CopyIfPresent(GDictionary target, GDictionary source, string key)
    {
        if (target == null || source == null || string.IsNullOrEmpty(key) || !source.ContainsKey(key))
            return;
        target[key] = source[key];
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
    )
    {
        var payload = new GDictionary
        {
            ["settlement_id"] = request.SettlementId.ToString(),
            ["service_id"] = request.ServiceId.ToString(),
            ["action_id"] = request.ActionId.ToString(),
        };
        if (request.MemberId != "")
            payload["member_id"] = request.MemberId.ToString();
        if (request.Quantity > 0)
            payload["request_quantity"] = request.Quantity;
        string submissionSource = SettlementSubmissionSources.ToPayloadValue(request.Source);
        if (!string.IsNullOrEmpty(submissionSource))
            payload["submission_source"] = submissionSource;
        return payload;
    }

    private SettlementActionValidationResult ValidateSettlementActionModalContextTyped(
        string settlement_id,
        string action_id,
        GDictionary payload
    )
    {
        if (_is_contract_board_modal_submission(payload))
        {
            if (GetActiveModalKind() != RuntimeModalKind.ContractBoard)
            {
                return SettlementActionValidationResult.Failure("当前没有打开对应的任务板。");
            }
            GDictionary contractBoardContext = GetActiveContractBoardContext();
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
            if (GetActiveModalKind() != RuntimeModalKind.Forge)
            {
                return SettlementActionValidationResult.Failure("当前没有打开对应的锻造界面。");
            }
            GDictionary forgeContext = GetActiveForgeContext();
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
        if (_is_npc_quest_offer_modal_submission(payload))
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
        GDictionary settlement = GetSettlementRecord(settlement_id);
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
        return !_is_contract_board_modal_submission(payload)
            && !_is_forge_modal_submission(payload)
            && !_is_npc_quest_offer_modal_submission(payload);
    }

    private SettlementServiceEntryResolution ResolveSettlementServiceEntryTyped(
        string settlement_id,
        string action_id
    )
    {
        GDictionary settlement = GetSettlementRecord(settlement_id);
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
            GDictionary serviceData = RuntimePayloadCopy.Dictionary(
                sourceServiceData,
                "GameRuntimeSettlementCommandHandler.ResolveSettlementServiceEntry"
            );
            if (ReadString(serviceData, "action_id").Trim() != action_id)
            {
                continue;
            }
            SettlementServiceMetadata metadata = BuildServiceMetadataTyped(
                settlement,
                serviceData,
                settlementState
            );
            SettlementServiceMetadataProjection.ApplyToServiceData(serviceData, metadata);
            string disabledReason = metadata.DisabledReason.Trim();
            serviceData["state_label"] = _build_service_state_label(
                metadata.IsEnabled,
                disabledReason
            );
            serviceData["summary_text"] = _build_service_summary_text(serviceData);
            SettlementPanelKind panelKind = _resolve_service_panel_kind(serviceData);
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
        GDictionary settlement = GetSettlementRecord(settlement_id);
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

    private StringName ResolveDefaultSettlementMemberId()
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
        GDictionary settlement = GetSettlementRecord(settlement_id);
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
                _GetItemDefsTyped(),
                GetRecipeDefsTyped(),
                GetPartyWarehouseService(),
                GetPartyState(),
                _extract_quest_progress_events(payload, action_id, settlement_id)
            );
        }
        if (_is_research_interaction(interactionScriptId))
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
        if (Runtime != null)
        {
            object lowLuckResultValue = Runtime.ResolveLowLuckSettlementEventRewards(
                new GDictionary
                {
                    ["action_id"] = action_id,
                    ["facility_id"] = ReadString(payload, "facility_id"),
                    ["facility_name"] = facility_name,
                    ["interaction_script_id"] = ReadString(payload, "interaction_script_id"),
                    ["npc_name"] = npc_name,
                    ["payload"] = RuntimePayloadCopy.Dictionary(
                        payload,
                        "GameRuntimeSettlementCommandHandler.LowLuckEvent.payload"
                    ),
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
        CharacterManagementModule characterManagement = Runtime?.GetCharacterManagement();
        if (characterManagement == null)
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
        PendingCharacterReward reward = characterManagement.BuildPendingCharacterReward(
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

    private GDictArray _build_service_entries(GDictionary settlement, GDictionary settlement_state)
    {
        var entries = new GDictArray();
        foreach (
            GDictionary sourceService in Dictionaries(
                ReadArray(settlement, "available_services")
            )
        )
        {
            GDictionary serviceData = RuntimePayloadCopy.Dictionary(
                sourceService,
                "GameRuntimeSettlementCommandHandler.BuildServiceEntries"
            );
            SettlementServiceMetadata metadata = BuildServiceMetadataTyped(
                settlement,
                serviceData,
                settlement_state
            );
            SettlementServiceMetadataProjection.ApplyToServiceData(serviceData, metadata);
            bool isEnabled = metadata.IsEnabled;
            string disabledReason = metadata.DisabledReason.Trim();
            serviceData["state_label"] = _build_service_state_label(isEnabled, disabledReason);
            serviceData["summary_text"] = _build_service_summary_text(serviceData);
            SettlementPanelKind panelKind = _resolve_service_panel_kind(serviceData);
            string panelKindText = SettlementPanelKinds.ToPayloadValue(panelKind);
            if (!string.IsNullOrEmpty(panelKindText))
            {
                serviceData["panel_kind"] = panelKindText;
            }
            entries.Add(serviceData);
        }
        return entries;
    }

    private string _build_service_state_label(bool is_enabled, string disabled_reason)
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

    private string _build_service_summary_text(GDictionary service_data)
    {
        return $"{ReadString(service_data, "facility_name").Trim()} · {ReadString(service_data, "npc_name").Trim()} · {ReadString(service_data, "service_type").Trim()}";
    }

    private SettlementPanelKind _resolve_service_panel_kind(GDictionary service_data)
    {
        string interactionScriptId = ReadString(service_data, "interaction_script_id").Trim();
        if (SHOP_INTERACTION_IDS.Contains(interactionScriptId))
        {
            return SettlementPanelKind.Shop;
        }
        if (STAGECOACH_INTERACTION_IDS.Contains(interactionScriptId))
        {
            return SettlementPanelKind.Stagecoach;
        }
        if (QuestProviderContentRules.IsSupportedProviderId(interactionScriptId))
        {
            return SettlementPanelKind.ContractBoard;
        }
        if (_is_forge_interaction(interactionScriptId))
        {
            return SettlementPanelKind.Forge;
        }
        return SettlementPanelKind.None;
    }

    private SettlementServiceMetadata BuildServiceMetadataTyped(
        GDictionary settlement,
        GDictionary service_data,
        GDictionary settlement_state
    )
    {
        string interactionScriptId = ReadString(service_data, "interaction_script_id");
        PartyState typedPartyState = GetPartyState();
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
                typedPartyState != null && typedPartyState.CanAfford(REST_FULL_COST);
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
                typedPartyState != null && typedPartyState.CanAfford(INTEL_NETWORK_COST);
            return new SettlementServiceMetadata(
                $"{INTEL_NETWORK_COST} 金",
                canAffordIntel,
                canAffordIntel ? "" : "金币不足"
            );
        }
        if (QuestProviderContentRules.IsSupportedProviderId(interactionScriptId))
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
            bool hasRecipe = _forge_service.HasAvailableRecipeTyped(
                settlement,
                service_data,
                _GetItemDefsTyped(),
                GetRecipeDefsTyped()
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

    private string _build_settlement_state_summary(GDictionary settlement_state)
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

    private string _build_settlement_window_feedback_text()
    {
        string feedbackText = GetSettlementFeedbackText().Trim();
        if (!string.IsNullOrEmpty(feedbackText))
        {
            return feedbackText;
        }
        return "点击服务继续，或切换成员后再操作。";
    }

    private GDictArray _build_member_options()
    {
        var options = new GDictArray();
        PartyState partyState = GetPartyState();
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
                || partyState.GetMemberState(memberId) == null
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
                || partyState.GetMemberState(memberId) == null
            )
            {
                continue;
            }
            seenMemberIds[memberId] = true;
            options.Add(_build_member_option(partyState, memberId, "替补"));
        }
        return options;
    }

    private GDictionary _build_member_option(
        PartyState party_state,
        StringName member_id,
        string roster_role
    )
    {
        PartyMemberState memberState = party_state.GetMemberState(member_id);
        if (memberState == null)
        {
            return new GDictionary();
        }
        return new GDictionary
        {
            ["member_id"] = member_id.ToString(),
            ["display_name"] = GetMemberDisplayName(member_id),
            ["roster_role"] = roster_role,
            ["is_leader"] = party_state.leader_member_id == member_id,
            ["current_hp"] = memberState.current_hp,
            ["current_mp"] = memberState.current_mp,
        };
    }

    private IReadOnlyDictionary<string, object> GetSettlementRecordSnapshotPlain(
        string settlementId
    )
    {
        WorldRuntimeData worldData = Runtime?.GetActiveWorldRuntimeData();
        if (worldData == null || string.IsNullOrEmpty(settlementId))
            return EmptyPlainDictionary();
        foreach (WorldMapSettlementRecordData settlement in worldData.Settlements)
        {
            if (settlement != null && settlement.SettlementId == settlementId)
                return settlement.BuildSaveSnapshotPlain();
        }
        return EmptyPlainDictionary();
    }

    private static IReadOnlyList<object> BuildSettlementServiceIdentityFactsPlain(
        IReadOnlyDictionary<string, object> settlement
    )
    {
        var entries = new List<object>();
        foreach (object rawEntry in ReadPlainList(settlement, "available_services"))
        {
            if (rawEntry is not IReadOnlyDictionary<string, object> entry)
                continue;
            entries.Add(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["action_id"] = ReadPlainString(entry, "action_id"),
                    ["facility_name"] = ReadPlainString(entry, "facility_name"),
                    ["npc_name"] = ReadPlainString(entry, "npc_name"),
                    ["service_type"] = ReadPlainString(entry, "service_type"),
                    ["interaction_script_id"] = ReadPlainString(
                        entry,
                        "interaction_script_id"
                    ),
                }
            );
        }
        return entries;
    }

    private IReadOnlyList<object> BuildMemberOptionsSnapshotPlain()
    {
        var options = new List<object>();
        PartyState partyState = GetPartyState();
        if (partyState == null)
            return options;

        var seenMemberIds = new HashSet<StringName>();
        foreach (StringName memberId in partyState.active_member_ids)
        {
            if (
                memberId == ""
                || !seenMemberIds.Add(memberId)
                || partyState.GetMemberState(memberId) == null
            )
            {
                continue;
            }
            options.Add(BuildMemberOptionSnapshotPlain(partyState, memberId, "上阵"));
        }
        foreach (StringName memberId in partyState.reserve_member_ids)
        {
            if (
                memberId == ""
                || !seenMemberIds.Add(memberId)
                || partyState.GetMemberState(memberId) == null
            )
            {
                continue;
            }
            options.Add(BuildMemberOptionSnapshotPlain(partyState, memberId, "替补"));
        }
        return options;
    }

    private IReadOnlyDictionary<string, object> BuildMemberOptionSnapshotPlain(
        PartyState partyState,
        StringName memberId,
        string rosterRole
    )
    {
        PartyMemberState memberState = partyState.GetMemberState(memberId);
        if (memberState == null)
            return EmptyPlainDictionary();
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["member_id"] = memberId.ToString(),
            ["display_name"] = GetMemberDisplayName(memberId),
            ["roster_role"] = rosterRole,
            ["is_leader"] = partyState.leader_member_id == memberId,
            ["current_hp"] = memberState.current_hp,
            ["current_mp"] = memberState.current_mp,
        };
    }

    private static void AppendWindowEntriesPlain(
        List<object> target,
        IReadOnlyDictionary<string, object> context,
        string key
    )
    {
        foreach (object rawEntry in ReadPlainList(context, key))
        {
            if (rawEntry is not IReadOnlyDictionary<string, object> entry)
                continue;
            Dictionary<string, object> copy = RuntimePlainPayload.CloneDictionary(entry);
            if (!copy.ContainsKey("is_enabled"))
                copy["is_enabled"] = false;
            target.Add(copy);
        }
    }

    private Dictionary<string, object> CloneActiveShopContextPlain() =>
        _has_runtime()
            ? RuntimePlainPayload.CloneDictionary(Runtime.GetActiveShopContextPlain())
            : new Dictionary<string, object>(StringComparer.Ordinal);

    private Dictionary<string, object> CloneActiveContractBoardContextPlain() =>
        _has_runtime()
            ? RuntimePlainPayload.CloneDictionary(Runtime.GetActiveContractBoardContextPlain())
            : new Dictionary<string, object>(StringComparer.Ordinal);

    private Dictionary<string, object> CloneActiveForgeContextPlain() =>
        _has_runtime()
            ? RuntimePlainPayload.CloneDictionary(Runtime.GetActiveForgeContextPlain())
            : new Dictionary<string, object>(StringComparer.Ordinal);

    private Dictionary<string, object> CloneActiveStagecoachContextPlain() =>
        _has_runtime()
            ? RuntimePlainPayload.CloneDictionary(Runtime.GetActiveStagecoachContextPlain())
            : new Dictionary<string, object>(StringComparer.Ordinal);

    private static bool WindowDataMatchesPanelKindPlain(
        IReadOnlyDictionary<string, object> context,
        SettlementPanelKind panelKind
    )
    {
        return ReadPlainString(context, "panel_kind")
            == SettlementPanelKinds.ToPayloadValue(panelKind);
    }

    private static IReadOnlyList<object> ReadPlainList(
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

    private static string ReadPlainString(
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

    private static int ReadPlainInt(
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

    private static Dictionary<string, object> EmptyPlainDictionary() =>
        new(StringComparer.Ordinal);

    private void _open_contract_board_modal(string settlement_id, GDictionary payload)
    {
        GDictionary windowData = _build_contract_board_window_data(settlement_id, payload);
        SetActiveContractBoardContext(windowData);
        SetActiveModalKind(RuntimeModalKind.ContractBoard);
        UpdateStatus(
            $"已打开 {ReadString(payload, "facility_name", "据点任务板")} 的任务板。"
        );
    }

    private GDictionary _build_contract_board_window_data(string settlement_id, GDictionary payload)
    {
        GDictionary settlement = GetSettlementRecord(settlement_id);
        string providerInteractionId = ReadString(payload, "interaction_script_id").Trim();
        GDictArray entries = _build_contract_board_entries(providerInteractionId);
        string summaryText = ReadString(payload, "feedback_text").Trim();
        if (string.IsNullOrEmpty(summaryText))
        {
            summaryText =
                "选择契约后会按当前状态执行接取或领奖；重复接取、待领奖励和可重复任务都会返回明确反馈。";
        }
        string feedbackText = ReadString(payload, "feedback_text", "");
        string stateSummaryText = !string.IsNullOrEmpty(feedbackText)
            ? feedbackText
            : _build_contract_board_state_summary(entries);
        return new GDictionary
        {
            ["title"] =
                $"{ReadString(settlement, "display_name", settlement_id)} · 任务板",
            ["meta"] =
                $"{ReadString(payload, "facility_name", "任务板")} · {ReadString(payload, "npc_name", "值守人员")} · {ReadString(payload, "service_type", "契约")}",
            ["summary_text"] = summaryText,
            ["state_summary_text"] = stateSummaryText,
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
            ["panel_kind"] = SettlementPanelKinds.ToPayloadValue(
                SettlementPanelKind.ContractBoard
            ),
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

    private bool _try_open_npc_quest_offer(
        string settlement_id,
        string action_id,
        GDictionary payload,
        out GDictionary result
    )
    {
        result = new GDictionary();
        if (_is_npc_quest_offer_modal_submission(payload))
            return false;
        string interactionScriptId = ReadString(payload, "interaction_script_id");
        if (interactionScriptId == "")
            return false;

        var npcQuests = new List<QuestDefinition>();
        foreach (QuestDefinition questDefinition in GetQuestDefsTyped().Values)
        {
            if (questDefinition.ProviderKind != "npc")
                continue;
            if (questDefinition.ProviderInteractionId != interactionScriptId)
                continue;
            if (!questDefinition.ListingChannels.Contains(NPC_OFFER_LISTING_CHANNEL))
                continue;
            npcQuests.Add(questDefinition);
        }

        if (npcQuests.Count == 0)
            return false;

        NpcQuestOfferWindowData windowData = _build_npc_quest_offer_window_data(
            settlement_id,
            interactionScriptId,
            npcQuests
        );
        SetActiveNpcQuestOfferContext(windowData);
        SetActiveModalKind(RuntimeModalKind.NpcQuestOffer);
        UpdateStatus($"已打开 {_resolve_npc_display_name(interactionScriptId)} 的委托。");
        result = CommandOk($"已打开 {interactionScriptId} 的委托。");
        return true;
    }

    private NpcQuestOfferWindowData _build_npc_quest_offer_window_data(
        string settlement_id,
        string npcInteractionId,
        List<QuestDefinition> npcQuests
    )
    {
        var windowData = new NpcQuestOfferWindowData
        {
            SettlementId = settlement_id,
            ActionId = "",
            NpcInteractionId = npcInteractionId,
            NpcName = _resolve_npc_display_name(npcInteractionId),
            SelectedQuestId = npcQuests[0].QuestId.ToString(),
        };

        foreach (QuestDefinition questDefinition in npcQuests)
        {
            ContractBoardQuestData questData = _build_contract_board_quest_data(
                questDefinition
            );
            QuestAcceptAvailabilityResult availability = _quest_accept_evaluator.Evaluate(
                questDefinition,
                _build_quest_accept_context()
            );
            windowData.Entries.Add(
                new NpcQuestOfferEntryData
                {
                    QuestId = questDefinition.QuestId.ToString(),
                    DisplayName = questDefinition.DisplayName,
                    Description = questDefinition.Description,
                    AcceptDialogueText = questDefinition.AcceptDialogueText,
                    SummaryText = questData != null
                        ? _build_contract_board_objective_summary(questData)
                        : "",
                    CostLabel = questData != null
                        ? _build_contract_board_reward_label(questData.RewardEntries)
                        : "奖励：无",
                    IsEnabled = availability.CanAccept,
                    DisabledReason = availability.DisabledReason,
                    LockReasonId = availability.LockReasonId,
                    AcceptFeedbackSuccess = questDefinition.AcceptFeedbackSuccess,
                    AcceptFeedbackFailure = questDefinition.AcceptFeedbackFailure,
                    AcceptConfirmationText = questDefinition.AcceptConfirmationText,
                }
            );
        }

        return windowData;
    }

    private static string _resolve_npc_display_name(string npcInteractionId)
    {
        if (npcInteractionId.StartsWith("npc_"))
            npcInteractionId = npcInteractionId.Substring(4);
        return npcInteractionId.Replace("_", " ");
    }

    private GDictArray _build_contract_board_entries(string interaction_script_id)
    {
        var entries = new GDictArray();
        string normalizedInteractionId = interaction_script_id.Trim();
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs = GetQuestDefsTyped();
        var questIds = new List<StringName>(questDefs.Keys);
        questIds.Sort((a, b) => string.CompareOrdinal(a.ToString(), b.ToString()));
        foreach (StringName questId in questIds)
        {
            QuestDefinition questDefinition = questDefs[questId];
            GDictionary questEntry = _build_contract_board_entry(
                questDefinition,
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
                    ["provider_kind"] = "",
                    ["listing_channels"] = new Godot.Collections.Array<string>(),
                    ["summary_text"] = "任务定义尚未挂到这块任务板上。",
                    ["details_text"] = missingProviderText,
                    ["state_id"] = "empty",
                    ["state_label"] = "状态：空",
                    ["cost_label"] = "奖励：无",
                    ["is_enabled"] = false,
                    ["disabled_reason"] = "暂无可查看任务。",
                    ["accept_dialogue_text"] = "",
                }
            );
        }
        return entries;
    }

    private GDictionary _build_contract_board_entry(
        QuestDefinition quest_definition,
        string interaction_script_id
    )
    {
        ContractBoardQuestData questData = _build_contract_board_quest_data(quest_definition);
        if (questData == null)
        {
            return new GDictionary();
        }
        string providerInteractionId = questData.ProviderInteractionId.Trim();
        if (
            string.IsNullOrEmpty(providerInteractionId)
            || providerInteractionId != interaction_script_id
        )
        {
            return new GDictionary();
        }

        QuestProviderKind providerKind = QuestProviderContentRules.ToProviderKind(
            quest_definition
        );
        if (!QuestProviderContentRules.IsSupportedProviderKind(providerKind))
        {
            return new GDictionary();
        }

        bool isContractBoard = interaction_script_id == "service_contract_board";
        bool matchesProviderKind = isContractBoard
            ? providerKind == QuestProviderKind.ServiceContractBoard
            : providerKind == QuestProviderKind.ServiceBountyRegistry;
        Godot.Collections.Array<QuestListingChannel> listingChannels =
            QuestProviderContentRules.ToListingChannels(quest_definition);
        bool matchesChannel = isContractBoard
            ? listingChannels.Contains(QuestListingChannel.ContractBoard)
            : listingChannels.Contains(QuestListingChannel.BountyRegistry);

        if (!matchesProviderKind || !matchesChannel)
        {
            return new GDictionary();
        }

        string stateId = _resolve_contract_board_quest_state_id(
            questData.QuestId,
            questData.IsRepeatable
        );

        string disabledReason = "";
        StringName lockReasonId = "";
        bool isEnabled = true;

        if (stateId is "available" or "repeatable")
        {
            QuestAcceptAvailabilityResult availability = _quest_accept_evaluator.Evaluate(
                quest_definition,
                _build_quest_accept_context()
            );
            isEnabled = availability.CanAccept;
            disabledReason = availability.DisabledReason;
            lockReasonId = availability.LockReasonId;
        }

        return new GDictionary
        {
            ["entry_id"] = questData.QuestId.ToString(),
            ["quest_id"] = questData.QuestId.ToString(),
            ["provider_interaction_id"] = providerInteractionId,
            ["provider_kind"] = quest_definition.ProviderKind.ToString(),
            ["listing_channels"] = new Godot.Collections.Array<string>(
                quest_definition.ListingChannels.Select(c => c.ToString())
            ),
            ["display_name"] = questData.DisplayName,
            ["summary_text"] = _build_contract_board_objective_summary(questData),
            ["details_text"] = _build_contract_board_entry_details(questData),
            ["state_id"] = stateId,
            ["state_label"] = _build_contract_board_state_label(stateId),
            ["cost_label"] = _build_contract_board_reward_label(questData.RewardEntries),
            ["is_enabled"] = isEnabled,
            ["disabled_reason"] = disabledReason,
            ["lock_reason_id"] = lockReasonId,
            ["is_repeatable"] = questData.IsRepeatable,
            ["accept_dialogue_text"] = quest_definition.AcceptDialogueText,
            ["accept_feedback_success"] = quest_definition.AcceptFeedbackSuccess,
            ["accept_feedback_failure"] = quest_definition.AcceptFeedbackFailure,
            ["accept_confirmation_text"] = quest_definition.AcceptConfirmationText,
        };
    }

    private string _resolve_contract_board_quest_state_id(
        StringName quest_id,
        bool is_repeatable = false
    )
    {
        PartyState partyState = GetPartyState();
        if (partyState == null)
        {
            return "available";
        }
        if (partyState.GetActiveQuestState(quest_id) != null)
        {
            return "active";
        }
        if (partyState.HasClaimableQuest(quest_id))
        {
            return "claimable";
        }
        if (partyState.HasCompletedQuest(quest_id))
        {
            if (is_repeatable)
            {
                return "repeatable";
            }
            return "completed";
        }
        return "available";
    }

    private string _build_contract_board_state_label(string state_id)
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

    private string _build_contract_board_state_summary(GDictArray entries)
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

    private string _build_contract_board_objective_summary(ContractBoardQuestData quest_data)
    {
        List<string> objectiveLines = _build_contract_board_objective_lines(quest_data);
        return "目标：" + string.Join(" / ", objectiveLines);
    }

    private string _build_contract_board_entry_details(ContractBoardQuestData quest_data)
    {
        var lines = new List<string>
        {
            quest_data.Description,
            _build_contract_board_objective_summary(quest_data),
            _build_contract_board_reward_label(quest_data.RewardEntries),
        };
        if (!string.IsNullOrEmpty(quest_data.AcceptDialogueText))
        {
            lines.Add($"接取对话：{quest_data.AcceptDialogueText}");
        }
        if (quest_data.IsRepeatable)
        {
            lines.Add("说明：该契约完成后可再次接取。");
        }
        return string.Join("\n", lines);
    }

    private List<string> _build_contract_board_objective_lines(ContractBoardQuestData quest_data)
    {
        var objectiveLines = new List<string>();
        QuestState questState = _get_active_quest_state(quest_data.QuestId);
        string stateId = _resolve_contract_board_quest_state_id(
            quest_data.QuestId,
            quest_data.IsRepeatable
        );
        bool isCompleted = _is_contract_board_completed_state(stateId);
        foreach (QuestObjectiveDefinition objectiveData in quest_data.ObjectiveEntries)
        {
            StringName objectiveId = objectiveData.ObjectiveId;
            int targetValue = objectiveData.TargetValue;
            int currentValue = isCompleted ? targetValue : 0;
            if (!isCompleted && questState != null)
            {
                currentValue = questState.GetObjectiveProgress(objectiveId);
            }
            objectiveLines.Add(
                $"{_describe_contract_board_objective(objectiveData)} {currentValue}/{targetValue}"
            );
        }
        return objectiveLines;
    }

    private string _describe_contract_board_objective(
        QuestObjectiveDefinition objective_data
    )
    {
        string targetId = objective_data.TargetId.ToString();
        if (objective_data.ObjectiveKind == QuestObjectiveKind.SettlementAction)
        {
            return $"据点事务 {targetId}";
        }
        if (objective_data.ObjectiveKind == QuestObjectiveKind.DefeatEnemy)
        {
            return "击败敌对遭遇";
        }
        if (objective_data.ObjectiveKind == QuestObjectiveKind.SubmitItem)
        {
            return $"提交物资 {GetItemDisplayName(objective_data.TargetId)}";
        }
        return "";
    }

    private string _build_contract_board_reward_label(
        IReadOnlyList<QuestRewardDefinition> reward_entries
    )
    {
        var rewardParts = new List<string>();
        foreach (QuestRewardDefinition rewardData in reward_entries)
        {
            if (rewardData.RewardKind == QuestRewardKind.Gold)
            {
                rewardParts.Add($"{rewardData.GoldAmount} 金");
            }
            else if (rewardData.RewardKind == QuestRewardKind.Item)
            {
                rewardParts.Add($"{GetItemDisplayName(rewardData.ItemId)} x{rewardData.ItemQuantity}");
            }
            else if (rewardData.RewardKind == QuestRewardKind.PendingCharacterReward)
            {
                rewardParts.Add("角色奖励");
            }
        }
        return $"奖励：{string.Join("、", rewardParts)}";
    }

    private QuestState _get_active_quest_state(StringName quest_id)
    {
        PartyState partyState = GetPartyState();
        if (partyState == null)
        {
            return null;
        }
        return partyState.GetActiveQuestState(quest_id);
    }

    private StringName _resolve_active_submit_item_objective_id(
        StringName quest_id,
        IReadOnlyList<QuestObjectiveDefinition> objective_defs
    )
    {
        QuestState questState = _get_active_quest_state(quest_id);
        if (questState == null)
        {
            return "";
        }
        foreach (QuestObjectiveDefinition objectiveData in objective_defs)
        {
            if (objectiveData.ObjectiveKind != QuestObjectiveKind.SubmitItem)
            {
                continue;
            }
            StringName objectiveId = objectiveData.ObjectiveId;
            int targetValue = objectiveData.TargetValue;
            if (questState.IsObjectiveComplete(objectiveId, targetValue))
            {
                continue;
            }
            return objectiveId;
        }
        return "";
    }

    private bool _quest_has_submit_item_objective(
        IReadOnlyList<QuestObjectiveDefinition> objective_defs
    )
    {
        foreach (QuestObjectiveDefinition objectiveData in objective_defs)
        {
            if (objectiveData.ObjectiveKind == QuestObjectiveKind.SubmitItem)
            {
                return true;
            }
        }
        return false;
    }

    private void _open_shop_modal(string settlement_id, GDictionary payload)
    {
        GDictionary settlementState = _get_or_create_settlement_state(settlement_id);
        settlementState["world_step"] = GetWorldStep();
        GDictionary windowData = _shop_service.BuildWindowDataTyped(
            ReadString(payload, "interaction_script_id"),
            GetSettlementRecord(settlement_id),
            settlementState,
            _GetItemDefsTyped(),
            GetPartyWarehouseService(),
            GetPartyGold(),
            _GetTraitDefsTyped()
        );
        SetActiveSettlementState(settlement_id, settlementState);
        windowData["settlement_id"] = settlement_id;
        windowData["interaction_script_id"] = ReadString(payload, "interaction_script_id");
        SetActiveShopContext(windowData);
        SetActiveModalKind(RuntimeModalKind.Shop);
        UpdateStatus(
            $"已打开 {ReadString(payload, "facility_name", "据点商店")} 的商店。"
        );
    }

    private void _open_forge_modal(string settlement_id, GDictionary payload)
    {
        GDictionary windowData = _forge_service.BuildWindowDataTyped(
            ReadString(payload, "interaction_script_id"),
            GetSettlementRecord(settlement_id),
            payload,
            _GetItemDefsTyped(),
            GetRecipeDefsTyped(),
            GetPartyWarehouseService()
        );
        windowData["settlement_id"] = settlement_id;
        windowData["interaction_script_id"] = ReadString(payload, "interaction_script_id");
        windowData["service_payload"] = RuntimePayloadCopy.Dictionary(
            payload,
            "GameRuntimeSettlementCommandHandler.OpenForgeModal.service_payload"
        );
        windowData["member_options"] = _build_member_options();
        StringName selectedMemberId = ReadStringName(payload, "member_id");
        if (selectedMemberId == "")
        {
            selectedMemberId = ResolveDefaultSettlementMemberId();
        }
        windowData["default_member_id"] = selectedMemberId.ToString();
        windowData["selected_member_id"] = selectedMemberId.ToString();
        SetActiveForgeContext(windowData);
        SetActiveModalKind(RuntimeModalKind.Forge);
        UpdateStatus(
            $"已打开 {ReadString(payload, "facility_name", "据点工坊")} 的{_resolve_forge_service_label(payload)}窗口。"
        );
    }

    private void _open_stagecoach_modal(string settlement_id, GDictionary payload)
    {
        GDictionary settlement = GetSettlementRecord(settlement_id);
        SetActiveStagecoachContext(
            new GDictionary
            {
                ["title"] = $"{ReadString(settlement, "display_name", "据点")} · 驿站路线",
                ["settlement_id"] = settlement_id,
                ["origin_name"] = ReadString(settlement, "display_name", "据点"),
                ["interaction_script_id"] = ReadString(payload, "interaction_script_id"),
                ["gold"] = GetPartyGold(),
                ["destinations"] = _build_stagecoach_destinations(
                    settlement,
                    ReadString(payload, "interaction_script_id")
                ),
                ["feedback_text"] = "选择一个已访问据点并支付路费后即可启程。",
            }
        );
        SetActiveModalKind(RuntimeModalKind.Stagecoach);
        UpdateStatus("已打开驿站路线。");
    }

    private void _refresh_active_shop_context()
    {
        GDictionary context = GetActiveShopContext();
        if (context.Count == 0)
        {
            return;
        }
        string settlementId = ReadString(context, "settlement_id");
        GDictionary settlementState = _get_or_create_settlement_state(settlementId);
        settlementState["world_step"] = GetWorldStep();
        GDictionary nextContext = _shop_service.BuildWindowDataTyped(
            ReadString(context, "interaction_script_id"),
            GetSettlementRecord(settlementId),
            settlementState,
            _GetItemDefsTyped(),
            GetPartyWarehouseService(),
            GetPartyGold(),
            _GetTraitDefsTyped()
        );
        SetActiveSettlementState(settlementId, settlementState);
        nextContext["settlement_id"] = settlementId;
        nextContext["interaction_script_id"] = ReadString(context, "interaction_script_id");
        SetActiveShopContext(nextContext);
    }

    private void _refresh_active_contract_board_context(string feedback_text = "")
    {
        GDictionary context = GetActiveContractBoardContext();
        if (context.Count == 0)
        {
            return;
        }
        string settlementId = ReadString(context, "settlement_id");
        GDictionary nextPayload = RuntimePayloadCopy.Dictionary(
            context,
            "GameRuntimeSettlementCommandHandler.RefreshContractBoard"
        );
        if (!string.IsNullOrEmpty(feedback_text))
        {
            nextPayload["feedback_text"] = feedback_text;
        }
        GDictionary nextContext = _build_contract_board_window_data(settlementId, nextPayload);
        SetActiveContractBoardContext(nextContext);
    }

    private void _set_contract_board_confirmation_context(StringName quest_id, string confirmation_text)
    {
        GDictionary context = GetActiveContractBoardContext();
        context["pending_confirmation_quest_id"] = quest_id.ToString();
        context["pending_confirmation_text"] = confirmation_text;
        context["pending_confirmation_source"] = "contract_board";
        SetActiveContractBoardContext(context);
    }

    private void _clear_contract_board_confirmation_context()
    {
        GDictionary context = GetActiveContractBoardContext();
        context.Remove("pending_confirmation_quest_id");
        context.Remove("pending_confirmation_text");
        context.Remove("pending_confirmation_source");
        SetActiveContractBoardContext(context);
    }

    private void _refresh_active_forge_context(string feedback_text = "")
    {
        GDictionary context = GetActiveForgeContext();
        if (context.Count == 0)
        {
            return;
        }
        string settlementId = ReadString(context, "settlement_id");
        GDictionary servicePayload = RuntimePayloadCopy.Dictionary(
            ReadDictionary(context, "service_payload"),
            "GameRuntimeSettlementCommandHandler.RefreshForge.service_payload"
        );
        string interactionScriptId = ReadString(context, "interaction_script_id");
        if (string.IsNullOrEmpty(interactionScriptId))
        {
            interactionScriptId = ReadString(servicePayload, "interaction_script_id");
        }
        GDictionary nextContext = _forge_service.BuildWindowDataTyped(
            interactionScriptId,
            GetSettlementRecord(settlementId),
            servicePayload,
            _GetItemDefsTyped(),
            GetRecipeDefsTyped(),
            GetPartyWarehouseService(),
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
        SetActiveForgeContext(nextContext);
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

    private List<StagecoachDestinationData> BuildStagecoachDestinationData(
        GDictionary origin_settlement,
        string interaction_script_id
    )
    {
        var entries = new List<StagecoachDestinationData>();
        string originSettlementId = ReadString(origin_settlement, "settlement_id");
        Vector2I originCoord = ReadVector2I(origin_settlement, "origin");
        foreach (GDictionary settlement in Dictionaries(GetAllSettlementRecords()))
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
            bool canTravel = GetPartyGold() >= travelCost;
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

    private GDictionary _find_stagecoach_destination(
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
        GDictionary originSettlement = GetSettlementRecord(originSettlementId);
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
            if (memberState == null)
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
            memberState.SetCurrentHp(Math.Min(oldHp + hpRestoreAmount, hpMax), syncDeathState: false);
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
        WorldMapFogSystem fogSystem = GetFogSystem();
        GArray revealedCoords =
            fogSystem != null
                ? new GArray(fogSystem.RevealDiamond(center, reveal_range, GetPlayerFactionId()).Select(v => Variant.From(v)))
                : new GArray();
        if (revealedCoords.Count != 0)
        {
            RefreshWorldVisibility();
        }
        return revealedCoords;
    }

    private void _mark_settlement_visited(string settlement_id)
    {
        if (!_has_runtime())
        {
            return;
        }
        Runtime.MarkSettlementVisited(settlement_id);
    }

    private bool IsSettlementVisited(string settlementId) =>
        Runtime?.IsSettlementVisited(settlementId) ?? false;

    private GDictionary _get_or_create_settlement_state(string settlement_id)
    {
        GDictionary settlementState = GetSettlementState(settlement_id);
        if (settlementState.Count == 0)
        {
            settlementState = new GDictionary
            {
                ["visited"] = false,
                ["reputation"] = 0,
                ["active_conditions"] = new GArray(),
                ["cooldowns"] = new GDictionary(),
                ["shop_inventory_seed"] = TrueRandomSeedService.GenerateSeed(),
                ["shop_last_refresh_step"] = 0,
                ["shop_states"] = new GDictionary(),
            };
            SetActiveSettlementState(settlement_id, settlementState);
        }
        return settlementState;
    }

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
            _notify_misfortune_guidance_of_forge_result(memberId, result);
            RecordMemberAchievementEvent(
                memberId,
                "settlement_action_completed",
                1,
                ProgressionDataUtils.to_string_name(action_id)
            );
        }
        SyncPartyStateFromCharacterManagement();
        return PersistChangesTyped(
            result.PersistPartyState,
            result.PersistWorldData,
            result.PersistPlayerCoord,
            rollbackSnapshot
        );
    }

    private void _notify_misfortune_guidance_of_forge_result(
        StringName member_id,
        SettlementServiceResult result
    )
    {
        if (member_id == "" || Runtime == null || result == null)
        {
            return;
        }
        GDictionary inventoryDelta = SettlementServiceResultProjection.ProjectInventoryDelta(result);
        if (ReadStringName(inventoryDelta, "recipe_id") == "")
        {
            return;
        }
        Runtime?.HandleMisfortuneForgeResult(member_id, result);
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
            var eventData = RuntimePayloadCopy.Dictionary(
                sourceEventData,
                "GameRuntimeSettlementCommandHandler.ExtractQuestProgressEvents"
            );
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
        Runtime.ApplyQuestProgressEventsToPartyTyped(event_options, "settlement");
    }

    private SettlementCommandRollbackSnapshot CaptureRollbackSnapshot()
    {
        if (!_has_runtime())
            return null;

        return new SettlementCommandRollbackSnapshot(
            RuntimeTransactionRollbackState.Capture(Runtime),
            GetActiveModalKind(),
            GetActiveSettlementId(),
            GetSettlementFeedbackText(),
            Runtime.GetSelectedCoord(),
            Runtime._settlement_entry_active,
            Runtime._settlement_entry_source_coord,
            Runtime._settlement_entry_target_coord,
            GetActiveShopContext(),
            GetActiveContractBoardContext(),
            GetActiveForgeContext(),
            GetActiveStagecoachContext(),
            GetActiveNpcQuestOfferContextTyped()
        );
    }

    private void RestoreRollbackSnapshot(SettlementCommandRollbackSnapshot snapshot)
    {
        if (!_has_runtime() || snapshot == null)
            return;

        SetSelectedCoord(snapshot.SelectedCoord);
        SetActiveSettlementId(snapshot.ActiveSettlementId);
        SetSettlementFeedbackText(snapshot.SettlementFeedbackText);
        SetActiveShopContext(snapshot.ActiveShopContext);
        SetActiveContractBoardContext(snapshot.ActiveContractBoardContext);
        SetActiveForgeContext(snapshot.ActiveForgeContext);
        SetActiveStagecoachContext(snapshot.ActiveStagecoachContext);
        if (snapshot.ActiveNpcQuestOfferContext != null)
            SetActiveNpcQuestOfferContext(snapshot.ActiveNpcQuestOfferContext);
        if (snapshot.SettlementEntryActive)
            Runtime.SetSettlementEntryContext(
                snapshot.SettlementEntrySourceCoord,
                snapshot.SettlementEntryTargetCoord
            );
        else
            Runtime.ClearSettlementEntryContext(false);
        SetActiveModalKind(snapshot.ActiveModalKind);
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

    private SettlementPersistResult PersistChangesTyped(
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
        RuntimeCommitResult result = Runtime.CommitRuntimeTransaction(
            transaction,
            "settlement_command"
        );
        if (!result.Ok && rollbackSnapshot != null)
        {
            RestoreRollbackSnapshot(rollbackSnapshot);
            transaction.Rollback(Runtime, rollbackSnapshot.RuntimeState);
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

    private bool _has_runtime()
    {
        return Runtime != null;
    }

    internal GDictionary CommandOk(string message = "")
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
        return Runtime.BuildCommandOk(message);
    }

    private GameRuntimeFacade.RuntimeCommandResult RuntimeCommandOk(string message = "")
    {
        return GameRuntimeFacade.RuntimeCommandResult.Success(message ?? "");
    }

    private static GameRuntimeFacade.RuntimeCommandResult BuildRuntimeCommandResult(
        GDictionary result
    )
    {
        if (result == null)
        {
            return GameRuntimeFacade.RuntimeCommandResult.Failure("");
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
        GameRuntimeFacade.RuntimeCommandCode code =
            result.ContainsKey("code")
            && result["code"].VariantType == Variant.Type.Int
            && Enum.IsDefined(
                typeof(GameRuntimeFacade.RuntimeCommandCode),
                result["code"].AsInt32()
            )
                ? (GameRuntimeFacade.RuntimeCommandCode)result["code"].AsInt32()
                : ok
                    ? GameRuntimeFacade.RuntimeCommandCode.Ok
                    : GameRuntimeFacade.RuntimeCommandCode.Failed;
        return ok
            ? GameRuntimeFacade.RuntimeCommandResult.Success(message, code)
            : GameRuntimeFacade.RuntimeCommandResult.Failure(message, code);
    }

    internal GDictionary CommandError(string message)
    {
        if (Runtime == null)
        {
            return new GDictionary { ["ok"] = false, ["message"] = message };
        }
        return Runtime.BuildCommandError(message);
    }

    private GDictionary CommandPersistFailure()
    {
        UpdateStatus(PERSIST_FAILURE_ROLLBACK_MESSAGE);
        return new GDictionary
        {
            ["ok"] = false,
            ["message"] = PERSIST_FAILURE_ROLLBACK_MESSAGE,
            ["code"] = (int)GameRuntimeFacade.RuntimeCommandCode.PersistenceFailure,
        };
    }

    private GameRuntimeFacade.RuntimeCommandResult RuntimeCommandError(string message)
    {
        if (!string.IsNullOrEmpty(message))
            UpdateStatus(message);
        return GameRuntimeFacade.RuntimeCommandResult.Failure(
            message ?? "",
            GameRuntimeFacade.RuntimeCommandCode.InvalidState
        );
    }

    private GameRuntimeFacade.RuntimeCommandResult RuntimeCommandPersistFailure()
    {
        UpdateStatus(PERSIST_FAILURE_ROLLBACK_MESSAGE);
        return GameRuntimeFacade.RuntimeCommandResult.Failure(
            PERSIST_FAILURE_ROLLBACK_MESSAGE,
            GameRuntimeFacade.RuntimeCommandCode.PersistenceFailure
        );
    }

    internal bool IsBattleActive()
    {
        return _has_runtime() && Runtime.IsBattleActive();
    }

    internal void UpdateStatus(string message)
    {
        if (_has_runtime())
        {
            Runtime.UpdateStatus(message);
        }
    }

    internal string GetActiveSettlementId()
    {
        return Runtime?.GetActiveSettlementId() ?? "";
    }

    internal void SetActiveSettlementId(string settlement_id)
    {
        if (_has_runtime())
        {
            Runtime.SetActiveSettlementId(settlement_id);
        }
    }

    internal void SetSettlementFeedbackText(string feedback_text)
    {
        if (_has_runtime())
        {
            Runtime.SetSettlementFeedbackText(feedback_text);
        }
    }

    internal string GetSettlementFeedbackText()
    {
        return Runtime?.GetSettlementFeedbackText() ?? "";
    }

    internal GDictionary GetSelectedSettlement()
    {
        return _has_runtime() ? Runtime.GetSelectedSettlement() : new GDictionary();
    }

    internal PartyState GetPartyState()
    {
        return Runtime?.GetPartyState();
    }

    internal int GetPartyGold()
    {
        return GetPartyState()?.GetGold() ?? 0;
    }

    internal GDictionary GetSettlementRecord(string settlement_id)
    {
        return _has_runtime() ? Runtime.GetSettlementRecord(settlement_id) : new GDictionary();
    }

    internal GArray GetAllSettlementRecords()
    {
        return _has_runtime() ? Runtime.GetAllSettlementRecords() : new GArray();
    }

    internal GDictionary GetSettlementState(string settlement_id)
    {
        return _has_runtime() ? Runtime.GetSettlementState(settlement_id) : new GDictionary();
    }

    internal bool SetActiveSettlementState(string settlement_id, GDictionary settlement_state)
    {
        return _has_runtime()
            && Runtime.SetActiveSettlementState(settlement_id, settlement_state);
    }

    internal PartyWarehouseService GetPartyWarehouseService()
    {
        return _has_runtime() ? Runtime.GetPartyWarehouseService() : null;
    }

    private IReadOnlyDictionary<StringName, ItemDef> _GetItemDefsTyped()
    {
        if (!_has_runtime())
        {
            return new Dictionary<StringName, ItemDef>();
        }
        GameSession gameSession = Runtime.GetGameSession();
        return gameSession != null
            ? gameSession.GetItemDefsTyped()
            : new Dictionary<StringName, ItemDef>();
    }

    private IReadOnlyDictionary<StringName, TraitDefinition> _GetTraitDefsTyped()
    {
        if (!_has_runtime())
        {
            return new Dictionary<StringName, TraitDefinition>();
        }
        GameSession gameSession = Runtime.GetGameSession();
        return gameSession != null
            ? gameSession.GetTraitDefsTyped()
            : new Dictionary<StringName, TraitDefinition>();
    }

    internal string GetItemDisplayName(StringName item_id)
    {
        return Runtime?.GetItemDisplayName(item_id) ?? item_id.ToString();
    }

    internal IReadOnlyDictionary<StringName, RecipeDef> GetRecipeDefsTyped()
    {
        if (!_has_runtime())
        {
            return new Dictionary<StringName, RecipeDef>();
        }
        GameSession gameSession = Runtime.GetGameSession();
        return gameSession != null
            ? gameSession.GetRecipeDefsTyped()
            : new Dictionary<StringName, RecipeDef>();
    }

    internal IReadOnlyDictionary<StringName, QuestDefinition> GetQuestDefsTyped()
    {
        if (!_has_runtime())
        {
            return new Dictionary<StringName, QuestDefinition>();
        }
        GameSession gameSession = Runtime.GetGameSession();
        return gameSession != null
            ? gameSession.GetQuestDefsTyped()
            : new Dictionary<StringName, QuestDefinition>();
    }

    private bool _is_forge_modal_submission(GDictionary payload)
    {
        return ReadSubmissionSource(payload) == SettlementSubmissionSource.Forge;
    }

    private bool _is_contract_board_modal_submission(GDictionary payload)
    {
        return ReadSubmissionSource(payload) == SettlementSubmissionSource.ContractBoard;
    }

    private static SettlementSubmissionSource ReadSubmissionSource(GDictionary payload)
    {
        if (
            SettlementSubmissionSources.TryParse(
                ReadString(payload, "submission_source"),
                out SettlementSubmissionSource source
            )
        )
        {
            return source;
        }
        return SettlementSubmissionSource.None;
    }

    private GDictionary _submit_contract_board_quest_action(
        string settlement_id,
        string action_id,
        GDictionary payload
    )
    {
        if (!_has_runtime())
        {
            return CommandError("运行时尚未初始化。");
        }
        GDictionary contractBoardContext = GetActiveContractBoardContext();
        if (ReadString(contractBoardContext, "action_id").Trim() != action_id)
        {
            string actionMismatchMessage = "当前任务板与请求的服务入口不一致。";
            SetSettlementFeedbackText(actionMismatchMessage);
            _refresh_active_contract_board_context(actionMismatchMessage);
            UpdateStatus(actionMismatchMessage);
            return CommandError(actionMismatchMessage);
        }
        StringName questId = ReadStringName(payload, "quest_id");
        if (questId == "")
        {
            string missingIdMessage = "当前契约条目缺少 quest_id，无法接取。";
            SetSettlementFeedbackText(missingIdMessage);
            _refresh_active_contract_board_context(missingIdMessage);
            UpdateStatus(missingIdMessage);
            return CommandError(missingIdMessage);
        }
        ContractBoardQuestData questData = _resolve_contract_board_submission_quest_data(questId);
        if (questData == null)
        {
            string missingQuestMessage = $"当前任务板未找到契约 {questId}。";
            SetSettlementFeedbackText(missingQuestMessage);
            _refresh_active_contract_board_context(missingQuestMessage);
            UpdateStatus(missingQuestMessage);
            return CommandError(missingQuestMessage);
        }
        string providerInteractionId = ReadString(payload, "provider_interaction_id").Trim();
        if (string.IsNullOrEmpty(providerInteractionId))
        {
            string missingProviderMessage =
                "当前契约条目缺少 provider_interaction_id，无法匹配任务板。";
            SetSettlementFeedbackText(missingProviderMessage);
            _refresh_active_contract_board_context(missingProviderMessage);
            UpdateStatus(missingProviderMessage);
            return CommandError(missingProviderMessage);
        }
        string questProviderInteractionId = questData.ProviderInteractionId.Trim();
        if (questProviderInteractionId != providerInteractionId)
        {
            string providerMismatchMessage =
                $"契约 {questData.DisplayName} 不属于当前任务板。";
            SetSettlementFeedbackText(providerMismatchMessage);
            _refresh_active_contract_board_context(providerMismatchMessage);
            UpdateStatus(providerMismatchMessage);
            return CommandError(providerMismatchMessage);
        }

        QuestAcceptAvailabilityResult availability = _quest_accept_evaluator.Evaluate(
            questData.QuestDefinition,
            _build_quest_accept_context()
        );

        if (!availability.CanAccept)
        {
            string feedback = !string.IsNullOrEmpty(questData.AcceptFeedbackFailure)
                ? questData.AcceptFeedbackFailure
                : $"不满足接取条件：{availability.DisabledReason}";
            _refresh_active_contract_board_context(feedback);
            SetSettlementFeedbackText(feedback);
            UpdateStatus(feedback);
            return CommandError(feedback);
        }

        bool isConfirmationSubmission = ReadBool(payload, "confirm_accept", false);
        bool hasPendingConfirmation = ReadStringName(GetActiveContractBoardContext(), "pending_confirmation_quest_id") == questId;

        if (!string.IsNullOrEmpty(questData.AcceptConfirmationText))
        {
            if (!isConfirmationSubmission && !hasPendingConfirmation)
            {
                _set_contract_board_confirmation_context(questId, questData.AcceptConfirmationText);
                return CommandOk("请确认是否接取该契约。");
            }

            if (isConfirmationSubmission && !hasPendingConfirmation)
            {
                string bypassMessage = "该契约需要先在面板中确认。";
                _refresh_active_contract_board_context(bypassMessage);
                SetSettlementFeedbackText(bypassMessage);
                UpdateStatus(bypassMessage);
                return CommandError(bypassMessage);
            }

            if (!isConfirmationSubmission && hasPendingConfirmation)
            {
                string pendingMessage = "请确认是否接取该契约。";
                _refresh_active_contract_board_context(pendingMessage);
                SetSettlementFeedbackText(pendingMessage);
                UpdateStatus(pendingMessage);
                return CommandOk(pendingMessage);
            }
        }

        if (hasPendingConfirmation)
            _clear_contract_board_confirmation_context();

        string stateId = _resolve_contract_board_quest_state_id(
            questData.QuestId,
            questData.IsRepeatable
        );
        GameRuntimeFacade.RuntimeCommandResult commandResult;
        bool isAcceptAction = false;
        if (stateId == "claimable")
        {
            commandResult = Runtime.CommandClaimQuestTyped(questId);
        }
        else if (stateId == "active")
        {
            StringName submitItemObjectiveId = _resolve_active_submit_item_objective_id(
                questId,
                questData.ObjectiveEntries
            );
            if (
                submitItemObjectiveId != ""
                || _quest_has_submit_item_objective(questData.ObjectiveEntries)
            )
            {
                commandResult = Runtime.CommandSubmitQuestItemTyped(
                    questId,
                    submitItemObjectiveId
                );
            }
            else
            {
                commandResult = Runtime.CommandAcceptQuestTyped(questId, questData.IsRepeatable);
                isAcceptAction = true;
            }
        }
        else
        {
            commandResult = Runtime.CommandAcceptQuestTyped(questId, questData.IsRepeatable);
            isAcceptAction = true;
        }

        string message;
        if (commandResult.Ok && isAcceptAction)
        {
            message = !string.IsNullOrEmpty(questData.AcceptFeedbackSuccess)
                ? questData.AcceptFeedbackSuccess
                : $"已接取契约 {questData.DisplayName}。";
        }
        else
        {
            message = string.IsNullOrEmpty(commandResult.Message)
                ? "任务处理失败。"
                : commandResult.Message;
        }
        SetActiveSettlementId(settlement_id);
        SetActiveModalKind(RuntimeModalKind.ContractBoard);
        SetSettlementFeedbackText(message);
        _refresh_active_contract_board_context(message);
        if (commandResult.Ok)
        {
            return CommandOk(message);
        }
        return CommandError(message);
    }

    private ContractBoardQuestData _resolve_contract_board_submission_quest_data(StringName quest_id)
    {
        if (quest_id == "")
            return null;
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs = GetQuestDefsTyped();
        if (
            !questDefs.TryGetValue(quest_id, out QuestDefinition questDefinition)
            || questDefinition == null
        )
            return null;
        return _build_contract_board_quest_data(questDefinition);
    }

    private ContractBoardQuestData _build_contract_board_quest_data(
        QuestDefinition quest_definition
    )
    {
        if (quest_definition == null || quest_definition.QuestId == "")
            return null;
        string displayName = quest_definition.DisplayName.StripEdges();
        string description = quest_definition.Description.StripEdges();
        string providerInteractionId = quest_definition.ProviderInteractionId
            .ToString()
            .Trim();
        if (
            string.IsNullOrEmpty(displayName)
            || string.IsNullOrEmpty(description)
            || string.IsNullOrEmpty(providerInteractionId)
        )
        {
            return null;
        }
        IReadOnlyList<QuestObjectiveDefinition> objectiveEntries =
            quest_definition.Objectives;
        if (!_is_contract_board_objective_entries_valid(objectiveEntries))
            return null;
        IReadOnlyList<QuestRewardDefinition> rewardEntries = quest_definition.Rewards;
        if (!_is_contract_board_reward_entries_valid(rewardEntries))
            return null;
        return new ContractBoardQuestData(
            quest_definition,
            displayName,
            description,
            providerInteractionId,
            objectiveEntries,
            rewardEntries
        );
    }

    private static bool _is_contract_board_objective_entries_valid(
        IReadOnlyList<QuestObjectiveDefinition> objective_entries
    )
    {
        if (objective_entries == null || objective_entries.Count == 0)
            return false;
        var seenObjectiveIds = new HashSet<StringName>();
        foreach (QuestObjectiveDefinition objectiveData in objective_entries)
        {
            if (objectiveData == null)
                return false;
            if (objectiveData.ObjectiveId == "" || !seenObjectiveIds.Add(objectiveData.ObjectiveId))
                return false;
            if (objectiveData.TargetValue <= 0)
                return false;
            if (objectiveData.ObjectiveKind == QuestObjectiveKind.SettlementAction)
            {
                if (objectiveData.TargetId == "")
                    return false;
            }
            else if (objectiveData.ObjectiveKind == QuestObjectiveKind.SubmitItem)
            {
                if (objectiveData.TargetId == "")
                    return false;
            }
            else if (objectiveData.ObjectiveKind != QuestObjectiveKind.DefeatEnemy)
            {
                return false;
            }
        }
        return true;
    }

    private static bool _is_contract_board_reward_entries_valid(
        IReadOnlyList<QuestRewardDefinition> reward_entries
    )
    {
        if (reward_entries == null || reward_entries.Count == 0)
            return false;
        foreach (QuestRewardDefinition rewardData in reward_entries)
        {
            if (rewardData == null)
                return false;
            if (rewardData.RewardKind == QuestRewardKind.Gold)
            {
                if (rewardData.GoldAmount <= 0)
                    return false;
            }
            else if (rewardData.RewardKind == QuestRewardKind.Item)
            {
                if (rewardData.ItemId == "" || rewardData.ItemQuantity <= 0)
                {
                    return false;
                }
            }
            else if (rewardData.RewardKind == QuestRewardKind.PendingCharacterReward)
            {
                if (!_is_contract_board_pending_character_reward_valid(rewardData))
                    return false;
            }
            else
            {
                return false;
            }
        }
        return true;
    }

    private static bool _is_contract_board_pending_character_reward_valid(
        QuestRewardDefinition reward_data
    )
    {
        if (reward_data.PendingRewardMemberId == "")
            return false;
        if (reward_data.PendingRewardEntries == null || reward_data.PendingRewardEntries.Count == 0)
            return false;
        foreach (
            QuestPendingRewardEntryDefinition entryData in reward_data.PendingRewardEntries
        )
        {
            if (
                entryData == null
                || entryData.EntryType == ""
                || !PendingCharacterRewardContentRules.IsSupportedEntryType(entryData.EntryType)
                || entryData.TargetId == ""
                || entryData.Amount == 0
            )
            {
                return false;
            }
        }
        return true;
    }

    private bool _is_contract_board_completed_state(string state_id)
    {
        return state_id == "claimable" || state_id == "completed" || state_id == "repeatable";
    }

    private bool _is_npc_quest_offer_modal_submission(GDictionary payload) =>
        ReadString(payload, "submission_source") == "npc_quest_offer";

    private sealed class NpcQuestOfferActionRequest
    {
        internal StringName QuestId { get; init; } = "";
        internal bool ConfirmAccept { get; init; }

        internal static bool TryParse(GDictionary payload, out NpcQuestOfferActionRequest request)
        {
            request = null;
            if (payload == null)
                return false;
            StringName questId = ReadStringName(payload, "quest_id");
            if (questId == "")
                return false;
            request = new NpcQuestOfferActionRequest
            {
                QuestId = questId,
                ConfirmAccept = ReadBool(payload, "confirm_accept", false),
            };
            return true;
        }
    }

    private GDictionary _submit_npc_quest_offer_action(
        string settlement_id,
        string action_id,
        GDictionary payload
    )
    {
        if (!_has_runtime())
        {
            return CommandError("运行时尚未初始化。");
        }
        if (GetActiveModalKind() != RuntimeModalKind.NpcQuestOffer)
        {
            string notOpenMessage = "当前没有打开 NPC 委托面板。";
            UpdateStatus(notOpenMessage);
            return CommandError(notOpenMessage);
        }

        if (!NpcQuestOfferActionRequest.TryParse(payload, out NpcQuestOfferActionRequest request))
        {
            string missingIdMessage = "NPC 委托提交缺少 quest_id。";
            UpdateStatus(missingIdMessage);
            return CommandError(missingIdMessage);
        }

        NpcQuestOfferWindowData npcContext = GetActiveNpcQuestOfferContextTyped();
        if (npcContext == null || npcContext.SettlementId.Trim() != settlement_id)
        {
            string settlementMismatchMessage = "当前 NPC 委托面板与请求的据点不一致。";
            UpdateStatus(settlementMismatchMessage);
            return CommandError(settlementMismatchMessage);
        }

        StringName questId = request.QuestId;
        QuestDefinition questDefinition = Runtime.GetQuestDef(questId);
        if (questDefinition == null || questDefinition.ProviderKind != "npc")
        {
            string notNpcMessage = "该任务不是 NPC 委托。";
            UpdateStatus(notNpcMessage);
            return CommandError(notNpcMessage);
        }

        if (questDefinition.ProviderInteractionId != npcContext.NpcInteractionId)
        {
            string wrongNpcMessage = "该任务不属于当前 NPC。";
            UpdateStatus(wrongNpcMessage);
            return CommandError(wrongNpcMessage);
        }

        if (!questDefinition.ListingChannels.Contains(NPC_OFFER_LISTING_CHANNEL))
        {
            string notOfferMessage = "该任务未配置为 NPC 委托。";
            UpdateStatus(notOfferMessage);
            return CommandError(notOfferMessage);
        }

        QuestAcceptAvailabilityResult availability = _quest_accept_evaluator.Evaluate(
            questDefinition,
            _build_quest_accept_context()
        );

        if (!availability.CanAccept)
        {
            string feedback = !string.IsNullOrEmpty(questDefinition.AcceptFeedbackFailure)
                ? questDefinition.AcceptFeedbackFailure
                : $"不满足接取条件：{availability.DisabledReason}";
            _refresh_active_npc_quest_offer_context(feedback);
            UpdateStatus(feedback);
            return CommandError(feedback);
        }

        bool isConfirmationSubmission = request.ConfirmAccept;
        bool hasPendingConfirmation = npcContext.PendingConfirmationQuestId == questId.ToString();

        if (!string.IsNullOrEmpty(questDefinition.AcceptConfirmationText))
        {
            if (!isConfirmationSubmission && !hasPendingConfirmation)
            {
                _set_npc_quest_offer_confirmation_context(
                    questId,
                    questDefinition.AcceptConfirmationText
                );
                return CommandOk("请确认是否接受该委托。");
            }

            if (isConfirmationSubmission && !hasPendingConfirmation)
            {
                string bypassMessage = "该委托需要先在面板中确认。";
                _refresh_active_npc_quest_offer_context(bypassMessage);
                UpdateStatus(bypassMessage);
                return CommandError(bypassMessage);
            }

            if (!isConfirmationSubmission && hasPendingConfirmation)
            {
                string pendingMessage = "请确认是否接受该委托。";
                _refresh_active_npc_quest_offer_context(pendingMessage);
                UpdateStatus(pendingMessage);
                return CommandOk(pendingMessage);
            }
        }

        if (hasPendingConfirmation)
            _clear_npc_quest_offer_confirmation_context();

        GameRuntimeFacade.RuntimeCommandResult commandResult = Runtime.CommandAcceptQuestTyped(
            questId,
            questDefinition.IsRepeatable
        );
        if (!commandResult.Ok)
        {
            _refresh_active_npc_quest_offer_context(commandResult.Message);
            UpdateStatus(commandResult.Message);
            return CommandError(commandResult.Message);
        }

        string successFeedback = !string.IsNullOrEmpty(
            questDefinition.AcceptFeedbackSuccess
        )
            ? questDefinition.AcceptFeedbackSuccess
            : $"已接受委托 {questDefinition.DisplayName}。";
        _refresh_active_npc_quest_offer_context(successFeedback);
        UpdateStatus(successFeedback);
        return CommandOk(successFeedback);
    }

    private void _refresh_active_npc_quest_offer_context(string feedback_text)
    {
        NpcQuestOfferWindowData context = GetActiveNpcQuestOfferContextTyped();
        if (context == null)
            return;

        string settlementId = context.SettlementId;
        string npcInteractionId = context.NpcInteractionId;
        var npcQuests = new List<QuestDefinition>();
        foreach (QuestDefinition questDefinition in GetQuestDefsTyped().Values)
        {
            if (questDefinition.ProviderKind != "npc")
                continue;
            if (questDefinition.ProviderInteractionId != npcInteractionId)
                continue;
            if (!questDefinition.ListingChannels.Contains(NPC_OFFER_LISTING_CHANNEL))
                continue;
            npcQuests.Add(questDefinition);
        }

        if (npcQuests.Count == 0)
            return;

        NpcQuestOfferWindowData refreshed = _build_npc_quest_offer_window_data(
            settlementId,
            npcInteractionId,
            npcQuests
        );
        refreshed.FeedbackText = feedback_text;
        refreshed.SelectedQuestId = context.SelectedQuestId;
        if (!npcQuests.Exists(q => q.QuestId.ToString() == refreshed.SelectedQuestId))
            refreshed.SelectedQuestId = npcQuests[0].QuestId.ToString();
        refreshed.PendingConfirmationQuestId = context.PendingConfirmationQuestId;
        refreshed.PendingConfirmationText = context.PendingConfirmationText;
        refreshed.PendingConfirmationSource = context.PendingConfirmationSource;
        SetActiveNpcQuestOfferContext(refreshed);
    }

    private void _set_npc_quest_offer_confirmation_context(
        StringName questId,
        string confirmationText
    )
    {
        NpcQuestOfferWindowData context = GetActiveNpcQuestOfferContextTyped();
        if (context == null)
            return;
        context.PendingConfirmationQuestId = questId.ToString();
        context.PendingConfirmationText = confirmationText;
        context.PendingConfirmationSource = "npc_quest_offer";
        SetActiveNpcQuestOfferContext(context);
    }

    private void _clear_npc_quest_offer_confirmation_context()
    {
        NpcQuestOfferWindowData context = GetActiveNpcQuestOfferContextTyped();
        if (context == null)
            return;
        context.PendingConfirmationQuestId = "";
        context.PendingConfirmationText = "";
        context.PendingConfirmationSource = "";
        SetActiveNpcQuestOfferContext(context);
    }

    private bool _is_forge_interaction(string interaction_script_id)
    {
        return _forge_service != null
            && _forge_service.IsSupportedInteraction(interaction_script_id);
    }

    private bool _is_research_interaction(string interaction_script_id)
    {
        return _research_service != null
            && _research_service.IsSupportedInteraction(interaction_script_id);
    }

    private string _build_forge_unavailable_reason(string interaction_script_id)
    {
        return interaction_script_id == "service_master_reforge"
            ? "当前没有可用重铸配方"
            : "当前没有可用锻造配方";
    }

    private string _resolve_forge_service_label(GDictionary payload)
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
        return _has_runtime() ? Runtime.GetMemberAttributeSnapshot(member_id) : null;
    }

    internal string GetMemberDisplayName(StringName member_id)
    {
        return Runtime?.GetMemberDisplayName(member_id) ?? member_id.ToString();
    }

    internal void OpenPartyWarehouseWindow(string entry_label)
    {
        if (_has_runtime())
        {
            Runtime.OpenPartyWarehouseWindow(entry_label);
        }
    }

    internal void EnqueuePendingCharacterRewardsTyped(
        IEnumerable<PendingCharacterReward> rewards
    )
    {
        if (_has_runtime())
        {
            Runtime.EnqueuePendingCharacterRewardsTyped(rewards);
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
            Runtime.RecordMemberAchievementEvent(member_id, event_id, value, detail_id);
        }
    }

    internal void SyncPartyStateFromCharacterManagement()
    {
        if (_has_runtime())
        {
            Runtime.SyncPartyStateFromCharacterManagement();
        }
    }

    internal int PersistPartyState()
    {
        return _has_runtime() ? Runtime.PersistPartyState() : (int)Error.Unavailable;
    }

    internal int PersistWorldData()
    {
        return _has_runtime() ? Runtime.PersistWorldData() : (int)Error.Unavailable;
    }

    internal int PersistPlayerCoord()
    {
        return _has_runtime() ? Runtime.PersistPlayerCoord() : (int)Error.Unavailable;
    }

    internal WorldMapFogSystem GetFogSystem()
    {
        return _has_runtime() ? Runtime.GetFogSystem() : null;
    }

    internal bool IsSettlementVisibleToPlayer(GDictionary settlement)
    {
        WorldMapFogSystem fogSystem = GetFogSystem();
        if (fogSystem == null)
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
                if (fogSystem.IsVisible(origin + new Vector2I(x, y), factionId))
                {
                    return true;
                }
            }
        }
        return false;
    }

    internal string GetPlayerFactionId()
    {
        return Runtime?.GetPlayerFactionId() ?? "player";
    }

    internal void AdvanceWorldTimeBySteps(int delta_steps)
    {
        if (_has_runtime())
        {
            Runtime.AdvanceWorldTimeBySteps(delta_steps);
        }
    }

    internal void RefreshWorldVisibility()
    {
        if (_has_runtime())
        {
            Runtime.RefreshWorldVisibility();
        }
    }

    internal int GetWorldStep()
    {
        return _has_runtime() ? Runtime.GetWorldStep() : 0;
    }

    internal void SetPlayerCoord(Vector2I coord)
    {
        if (_has_runtime())
        {
            Runtime.SetPlayerCoord(coord);
        }
    }

    internal void SetSelectedCoord(Vector2I coord)
    {
        if (_has_runtime())
        {
            Runtime.SetSelectedCoord(coord);
        }
    }

    internal void ClearSettlementEntryContext(bool reset_selected = true)
    {
        if (_has_runtime())
        {
            Runtime.ClearSettlementEntryContext(reset_selected);
        }
    }

    internal RuntimeModalKind GetActiveModalKind()
    {
        return Runtime?.GetActiveModalKind() ?? RuntimeModalKind.None;
    }

    internal void SetActiveModalKind(RuntimeModalKind modalKind)
    {
        if (_has_runtime())
        {
            Runtime.SetRuntimeActiveModalKind(modalKind);
        }
    }

    internal bool PresentPendingRewardIfReady()
    {
        return _has_runtime() && Runtime.PresentPendingRewardIfReady();
    }

    internal void SetActiveShopContext(GDictionary context)
    {
        if (_has_runtime())
        {
            Runtime.SetActiveShopContext(context);
        }
    }

    internal void SetActiveContractBoardContext(GDictionary context)
    {
        if (_has_runtime())
        {
            Runtime.SetActiveContractBoardContext(context);
        }
    }

    internal void SetActiveNpcQuestOfferContext(NpcQuestOfferWindowData data)
    {
        if (_has_runtime())
        {
            Runtime.SetActiveNpcQuestOfferContext(data);
        }
    }

    internal void SetActiveForgeContext(GDictionary context)
    {
        if (_has_runtime())
        {
            Runtime.SetActiveForgeContext(context);
        }
    }

    internal void ClearActiveShopContext()
    {
        if (_has_runtime())
        {
            Runtime.ClearActiveShopContext();
        }
    }

    internal void ClearActiveContractBoardContext()
    {
        if (_has_runtime())
        {
            Runtime.ClearActiveContractBoardContext();
        }
    }

    internal void ClearActiveNpcQuestOfferContext()
    {
        if (_has_runtime())
        {
            Runtime.ClearActiveNpcQuestOfferContext();
        }
    }

    internal void ClearActiveForgeContext()
    {
        if (_has_runtime())
        {
            Runtime.ClearActiveForgeContext();
        }
    }

    internal GDictionary GetActiveShopContext()
    {
        return _has_runtime() ? Runtime.GetActiveShopContext() : new GDictionary();
    }

    internal GDictionary GetActiveContractBoardContext()
    {
        return _has_runtime() ? Runtime.GetActiveContractBoardContext() : new GDictionary();
    }

    internal GDictionary GetActiveNpcQuestOfferContext()
    {
        return _has_runtime() ? Runtime.GetActiveNpcQuestOfferContext() : new GDictionary();
    }

    internal GDictionary GetActiveForgeContext()
    {
        return _has_runtime() ? Runtime.GetActiveForgeContext() : new GDictionary();
    }

    internal void SetActiveStagecoachContext(GDictionary context)
    {
        if (_has_runtime())
        {
            Runtime.SetActiveStagecoachContext(context);
        }
    }

    internal void ClearActiveStagecoachContext()
    {
        if (_has_runtime())
        {
            Runtime.ClearActiveStagecoachContext();
        }
    }

    internal GDictionary GetActiveStagecoachContext()
    {
        return _has_runtime() ? Runtime.GetActiveStagecoachContext() : new GDictionary();
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
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.String)
        {
            value = variant.AsString();
            return true;
        }
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

    private static GameRuntimeFacade ResolveWeakRef(WeakReference<GameRuntimeFacade> weakRef)
    {
        if (
            weakRef == null
            || !weakRef.TryGetTarget(out GameRuntimeFacade target)
        )
        {
            return null;
        }
        return target;
    }
}
