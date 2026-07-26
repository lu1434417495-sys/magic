using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GArray = Godot.Collections.Array;
using GDictArray = Godot.Collections.Array<Godot.Collections.Dictionary>;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

internal sealed class ContractBoardQuestData
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

internal sealed class GameRuntimeContractBoardCommandHandler
{
    private GameRuntimeSettlementCommandHandler _owner;
    private GameRuntimeNpcQuestOfferCommandHandler _npcQuestOfferHandler;
    private GameRuntimeSettlementWindowDataBuilder _windowDataBuilder;

    internal void Setup(
        GameRuntimeSettlementCommandHandler owner,
        GameRuntimeNpcQuestOfferCommandHandler npcQuestOfferHandler,
        GameRuntimeSettlementWindowDataBuilder windowDataBuilder
    )
    {
        _owner = owner;
        _npcQuestOfferHandler = npcQuestOfferHandler;
        _windowDataBuilder = windowDataBuilder;
    }


    internal IReadOnlyDictionary<string, object> GetContractBoardWindowDataSnapshotPlain()
    {
        Dictionary<string, object> context = _windowDataBuilder.CloneActiveContractBoardContextPlain();
        context.Remove("party_state");
        return context;
    }

    internal void _open_contract_board_modal(string settlement_id, GDictionary payload)
    {
        GDictionary windowData = _build_contract_board_window_data(settlement_id, payload);
        _owner.SetActiveContractBoardContext(windowData);
        _owner.SetActiveModalKind(RuntimeModalKind.ContractBoard);
        _owner.UpdateStatus(
            $"已打开 {GameRuntimeSettlementCommandHandler.ReadString(payload, "facility_name", "据点任务板")} 的任务板。"
        );
    }

    private GDictionary _build_contract_board_window_data(string settlement_id, GDictionary payload)
    {
        using GodotProjectionLease<GDictionary> settlementLease =
            _owner.GetSettlementRecordLease(settlement_id);
        GDictionary settlement = settlementLease.Value;
        string providerInteractionId = GameRuntimeSettlementCommandHandler.ReadString(payload, "interaction_script_id").Trim();
        GDictArray entries = _build_contract_board_entries(providerInteractionId);
        string summaryText = GameRuntimeSettlementCommandHandler.ReadString(payload, "feedback_text").Trim();
        if (string.IsNullOrEmpty(summaryText))
        {
            summaryText =
                "选择契约后会按当前状态执行接取或领奖；重复接取、待领奖励和可重复任务都会返回明确反馈。";
        }
        string feedbackText = GameRuntimeSettlementCommandHandler.ReadString(payload, "feedback_text", "");
        string stateSummaryText = !string.IsNullOrEmpty(feedbackText)
            ? feedbackText
            : _build_contract_board_state_summary(entries);
        return new GDictionary
        {
            ["title"] =
                $"{GameRuntimeSettlementCommandHandler.ReadString(settlement, "display_name", settlement_id)} · 任务板",
            ["meta"] =
                $"{GameRuntimeSettlementCommandHandler.ReadString(payload, "facility_name", "任务板")} · {GameRuntimeSettlementCommandHandler.ReadString(payload, "npc_name", "值守人员")} · {GameRuntimeSettlementCommandHandler.ReadString(payload, "service_type", "契约")}",
            ["summary_text"] = summaryText,
            ["state_summary_text"] = stateSummaryText,
            ["service_name"] = GameRuntimeSettlementCommandHandler.ReadString(payload, "service_type", "任务板"),
            ["settlement_id"] = settlement_id,
            ["action_id"] = GameRuntimeSettlementCommandHandler.ReadString(payload, "action_id"),
            ["interaction_script_id"] = providerInteractionId,
            ["provider_interaction_id"] = providerInteractionId,
            ["facility_id"] = GameRuntimeSettlementCommandHandler.ReadString(payload, "facility_id"),
            ["facility_name"] = GameRuntimeSettlementCommandHandler.ReadString(payload, "facility_name"),
            ["npc_id"] = GameRuntimeSettlementCommandHandler.ReadString(payload, "npc_id"),
            ["npc_name"] = GameRuntimeSettlementCommandHandler.ReadString(payload, "npc_name"),
            ["service_type"] = GameRuntimeSettlementCommandHandler.ReadString(payload, "service_type"),
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

    internal bool _is_bounty_board_modal_submission(GDictionary payload)
    {
        return GameRuntimeSettlementCommandHandler.ReadSubmissionSource(payload) == SettlementSubmissionSource.BountyBoard;
    }

    internal void _open_bounty_board_modal(string settlement_id, GDictionary payload)
    {
        BountyBoardWindowData windowData = _build_bounty_board_window_data(
            settlement_id,
            payload
        );
        SetActiveBountyBoardContext(windowData);
        _owner.SetActiveModalKind(RuntimeModalKind.BountyBoard);
        _owner.UpdateStatus(
            $"已打开 {GameRuntimeSettlementCommandHandler.ReadString(payload, "facility_name", "悬赏板")} 的悬赏板。"
        );
    }

    private BountyBoardWindowData _build_bounty_board_window_data(
        string settlement_id,
        GDictionary payload
    )
    {
        using GodotProjectionLease<GDictionary> settlementLease =
            _owner.GetSettlementRecordLease(settlement_id);
        GDictionary settlement = settlementLease.Value;
        string providerInteractionId = GameRuntimeSettlementCommandHandler.ReadString(payload, "interaction_script_id").Trim();
        string settlementTemplateId =
            GameRuntimeSettlementCommandHandler.ReadString(settlement, "template_id").Trim();
        var windowData = new BountyBoardWindowData
        {
            SettlementId = settlement_id,
            SettlementTemplateId = settlementTemplateId,
            ActionId = GameRuntimeSettlementCommandHandler.ReadString(payload, "action_id"),
            ProviderInteractionId = providerInteractionId,
            Title =
                $"{GameRuntimeSettlementCommandHandler.ReadString(settlement, "display_name", settlement_id)} · 悬赏板",
            Meta =
                $"{GameRuntimeSettlementCommandHandler.ReadString(payload, "facility_name", "悬赏板")} · {GameRuntimeSettlementCommandHandler.ReadString(payload, "npc_name", "登记员")} · {GameRuntimeSettlementCommandHandler.ReadString(payload, "service_type", "悬赏")}",
            FeedbackText = GameRuntimeSettlementCommandHandler.ReadString(payload, "feedback_text"),
            Entries = _build_bounty_board_entries(providerInteractionId, settlementTemplateId),
        };
        if (windowData.Entries.Count > 0)
            windowData.SelectedQuestId = windowData.Entries[0].QuestId;
        return windowData;
    }

    private List<BountyBoardEntryData> _build_bounty_board_entries(
        string provider_interaction_id,
        string settlement_template_id
    )
    {
        var entries = new List<BountyBoardEntryData>();
        string normalizedInteractionId = (provider_interaction_id ?? "").Trim();
        string normalizedTemplateId = (settlement_template_id ?? "").Trim();
        if (string.IsNullOrEmpty(normalizedInteractionId))
            return entries;
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs = _owner.GetQuestDefsTyped();
        IReadOnlyDictionary<StringName, EnemyTemplateDefinition> enemyTemplates =
            _GetEnemyTemplatesTyped();
        var questIds = new List<StringName>(questDefs.Keys);
        questIds.Sort((a, b) => string.CompareOrdinal(a.ToString(), b.ToString()));
        foreach (StringName questId in questIds)
        {
            QuestDefinition questDefinition = questDefs[questId];
            if (
                !_is_bounty_board_quest(
                    questDefinition,
                    normalizedInteractionId,
                    normalizedTemplateId
                )
            )
                continue;
            ContractBoardQuestData questData = _build_contract_board_quest_data(
                questDefinition
            );
            if (questData == null)
                continue;

            string stateId = _resolve_contract_board_quest_state_id(
                questData.QuestId,
                questData.IsRepeatable,
                questDefinition.CanRestartAfterFailure
            );
            bool isEnabled;
            string disabledReason = "";
            StringName lockReasonId = "";
            if (stateId is "available" or "repeatable" or "restartable_failed")
            {
                QuestAcceptAvailabilityResult availability = _owner._quest_accept_evaluator.Evaluate(
                    questDefinition,
                    _owner._build_quest_accept_context()
                );
                isEnabled = availability.CanAccept;
                disabledReason = availability.DisabledReason;
                lockReasonId = availability.LockReasonId;
            }
            else if (stateId == "claimable")
            {
                isEnabled = true;
            }
            else if (stateId == "active")
            {
                isEnabled = false;
                disabledReason = "悬赏仍在进行中。";
            }
            else
            {
                isEnabled = false;
                disabledReason = "该悬赏已完成。";
            }

            QuestDangerRatingResult danger = QuestDangerRatingResolver.Resolve(
                questDefinition,
                enemyTemplates
            );
            string dangerLabel = QuestDangerRatingResolver.BuildStarsLabel(danger);
            entries.Add(
                new BountyBoardEntryData
                {
                    QuestId = questData.QuestId.ToString(),
                    DisplayName = questData.DisplayName,
                    ObjectiveSummary = _build_contract_board_objective_summary(questData),
                    RewardLabel = _build_contract_board_reward_label(questData.RewardEntries),
                    DetailsText =
                        dangerLabel + "\n" + _build_contract_board_entry_details(questData),
                    DangerStars = danger.IsRated ? danger.Stars : 0,
                    DangerSource = danger.Source.ToString(),
                    DangerLabel = dangerLabel,
                    StateId = stateId,
                    StateLabel = _build_contract_board_state_label(stateId),
                    ActionLabel = _build_bounty_board_action_label(stateId),
                    IsEnabled = isEnabled,
                    DisabledReason = disabledReason,
                    LockReasonId = lockReasonId.ToString(),
                }
            );
        }
        return entries;
    }

    private bool _is_bounty_board_quest(
        QuestDefinition quest_definition,
        string provider_interaction_id,
        string settlement_template_id
    )
    {
        if (quest_definition == null)
            return false;
        if (quest_definition.ProviderInteractionId.ToString().Trim() != provider_interaction_id)
            return false;
        if (
            QuestProviderContentRules.ToProviderKind(quest_definition)
            != QuestProviderKind.ServiceBountyRegistry
        )
            return false;
        if (
            !QuestProviderContentRules
                .ToListingChannels(quest_definition)
                .Contains(QuestListingChannel.BountyRegistry)
        )
            return false;
        return _is_quest_listed_for_settlement(quest_definition, settlement_template_id);
    }

    // 悬赏必须按据点绑定（listing_settlement_ids = SettlementConfig.settlement_id 白名单）。
    // 未绑定或据点不匹配的悬赏在本板不可见；validator 已保证正式内容非空绑定。
    private static bool _is_quest_listed_for_settlement(
        QuestDefinition quest_definition,
        string settlement_template_id
    )
    {
        if (string.IsNullOrEmpty(settlement_template_id))
            return false;
        foreach (StringName listedSettlementId in quest_definition.ListingSettlementIds)
        {
            if (listedSettlementId.ToString() == settlement_template_id)
                return true;
        }
        return false;
    }

    private static string _build_bounty_board_action_label(string state_id)
    {
        return state_id switch
        {
            "active" => "进行中",
            "claimable" => "领取奖励",
            "completed" => "已完成",
            "failed" => "已失败",
            "restartable_failed" => "重新接取",
            _ => "接取悬赏",
        };
    }

    private void _refresh_active_bounty_board_context(string feedbackText)
    {
        BountyBoardWindowData context = GetActiveBountyBoardContextTyped();
        if (context == null)
            return;
        string selectedQuestId = context.SelectedQuestId;
        context.Entries = _build_bounty_board_entries(
            context.ProviderInteractionId,
            context.SettlementTemplateId
        );
        context.FeedbackText = feedbackText ?? "";
        bool selectionStillListed = false;
        foreach (BountyBoardEntryData entry in context.Entries)
        {
            if (entry.QuestId == selectedQuestId)
                selectionStillListed = true;
        }
        if (!selectionStillListed)
            context.SelectedQuestId =
                context.Entries.Count > 0 ? context.Entries[0].QuestId : "";
        SetActiveBountyBoardContext(context);
    }

    internal GDictionary _submit_bounty_board_quest_action(
        string settlement_id,
        string action_id,
        GDictionary payload
    )
    {
        if (!_owner._has_runtime())
        {
            return _owner.CommandError("运行时尚未初始化。");
        }
        BountyBoardWindowData bountyContext = GetActiveBountyBoardContextTyped();
        if (bountyContext == null || bountyContext.ActionId.Trim() != action_id)
        {
            string actionMismatchMessage = "当前悬赏板与请求的服务入口不一致。";
            _owner.SetSettlementFeedbackText(actionMismatchMessage);
            _refresh_active_bounty_board_context(actionMismatchMessage);
            _owner.UpdateStatus(actionMismatchMessage);
            return _owner.CommandError(actionMismatchMessage);
        }
        StringName questId = GameRuntimeSettlementCommandHandler.ReadStringName(payload, "quest_id");
        if (questId == "")
        {
            string missingIdMessage = "当前悬赏条目缺少 quest_id，无法接取。";
            _owner.SetSettlementFeedbackText(missingIdMessage);
            _refresh_active_bounty_board_context(missingIdMessage);
            _owner.UpdateStatus(missingIdMessage);
            return _owner.CommandError(missingIdMessage);
        }
        string providerInteractionId = GameRuntimeSettlementCommandHandler.ReadString(payload, "provider_interaction_id").Trim();
        if (string.IsNullOrEmpty(providerInteractionId))
            providerInteractionId = bountyContext.ProviderInteractionId.Trim();
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs = _owner.GetQuestDefsTyped();
        if (
            !questDefs.TryGetValue(questId, out QuestDefinition questDefinition)
            || questDefinition == null
            || !_is_bounty_board_quest(
                questDefinition,
                providerInteractionId,
                bountyContext.SettlementTemplateId
            )
        )
        {
            string missingQuestMessage = $"当前悬赏板未找到悬赏 {questId}。";
            _owner.SetSettlementFeedbackText(missingQuestMessage);
            _refresh_active_bounty_board_context(missingQuestMessage);
            _owner.UpdateStatus(missingQuestMessage);
            return _owner.CommandError(missingQuestMessage);
        }
        ContractBoardQuestData questData = _build_contract_board_quest_data(questDefinition);
        if (questData == null)
        {
            string invalidQuestMessage = $"悬赏 {questId} 配置无效，无法处理。";
            _owner.SetSettlementFeedbackText(invalidQuestMessage);
            _refresh_active_bounty_board_context(invalidQuestMessage);
            _owner.UpdateStatus(invalidQuestMessage);
            return _owner.CommandError(invalidQuestMessage);
        }

        string stateId = _resolve_contract_board_quest_state_id(
            questData.QuestId,
            questData.IsRepeatable,
            questDefinition.CanRestartAfterFailure
        );
        RuntimeCommandResult commandResult;
        bool isAcceptAction = false;
        if (stateId == "claimable")
        {
            commandResult = _owner.CommandClaimQuestTyped(questId);
        }
        else if (stateId == "active")
        {
            commandResult = RuntimeCommandResult.Failure(
                $"悬赏《{questData.DisplayName}》仍在进行中。",
                RuntimeCommandCode.InvalidState
            );
        }
        else if (stateId == "completed")
        {
            commandResult = RuntimeCommandResult.Failure(
                $"悬赏《{questData.DisplayName}》已完成，不能再次接取。",
                RuntimeCommandCode.InvalidState
            );
        }
        else if (stateId == "failed")
        {
            commandResult = RuntimeCommandResult.Failure(
                $"悬赏《{questData.DisplayName}》已经失败，不能再次接取。",
                RuntimeCommandCode.InvalidState
            );
        }
        else
        {
            QuestAcceptAvailabilityResult availability = _owner._quest_accept_evaluator.Evaluate(
                questDefinition,
                _owner._build_quest_accept_context()
            );
            if (!availability.CanAccept)
            {
                string feedback = !string.IsNullOrEmpty(questData.AcceptFeedbackFailure)
                    ? questData.AcceptFeedbackFailure
                    : $"不满足接取条件：{availability.DisabledReason}";
                _owner.SetSettlementFeedbackText(feedback);
                _refresh_active_bounty_board_context(feedback);
                _owner.UpdateStatus(feedback);
                return _owner.CommandError(feedback);
            }
            commandResult = _owner.CommandAcceptQuestTyped(questId, questData.IsRepeatable);
            isAcceptAction = true;
        }

        string message;
        if (commandResult.Ok && isAcceptAction)
        {
            message = !string.IsNullOrEmpty(questData.AcceptFeedbackSuccess)
                ? questData.AcceptFeedbackSuccess
                : $"已接取悬赏 {questData.DisplayName}。";
        }
        else
        {
            message = string.IsNullOrEmpty(commandResult.Message)
                ? "悬赏处理失败。"
                : commandResult.Message;
        }
        _owner.SetActiveSettlementId(settlement_id);
        _owner.SetActiveModalKind(RuntimeModalKind.BountyBoard);
        _owner.SetSettlementFeedbackText(message);
        BountyBoardWindowData refreshedContext = GetActiveBountyBoardContextTyped();
        if (refreshedContext != null)
        {
            refreshedContext.SelectedQuestId = questId.ToString();
            SetActiveBountyBoardContext(refreshedContext);
        }
        _refresh_active_bounty_board_context(message);
        if (commandResult.Ok)
        {
            return _owner.CommandOk(message);
        }
        return _owner.CommandError(message);
    }

    internal IReadOnlyDictionary<string, object> GetBountyBoardWindowDataSnapshotPlain()
    {
        BountyBoardWindowData data = GetActiveBountyBoardContextTyped();
        return (data ?? BountyBoardWindowData.Empty).BuildSnapshotPlain();
    }

    internal BountyBoardWindowData GetActiveBountyBoardContextTyped()
    {
        return _owner.GetActiveBountyBoardData();
    }

    internal void SetActiveBountyBoardContext(BountyBoardWindowData data)
    {
        _owner.SetActiveBountyBoardRuntimeContext(data);
    }

    internal void ClearActiveBountyBoardContext()
    {
        _owner.ClearActiveBountyBoardRuntimeContext();
    }

    private IReadOnlyDictionary<StringName, EnemyTemplateDefinition> _GetEnemyTemplatesTyped()
    {
        return _owner.GetEnemyTemplateDefinitionsTyped();
    }

    private GDictArray _build_contract_board_entries(string interaction_script_id)
    {
        var entries = new GDictArray();
        string normalizedInteractionId = interaction_script_id.Trim();
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs = _owner.GetQuestDefsTyped();
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
            questData.IsRepeatable,
            quest_definition.CanRestartAfterFailure
        );

        string disabledReason = "";
        StringName lockReasonId = "";
        bool isEnabled = true;

        if (stateId is "available" or "repeatable" or "restartable_failed")
        {
            QuestAcceptAvailabilityResult availability = _owner._quest_accept_evaluator.Evaluate(
                quest_definition,
                _owner._build_quest_accept_context()
            );
            isEnabled = availability.CanAccept;
            disabledReason = availability.DisabledReason;
            lockReasonId = availability.LockReasonId;
        }
        else if (stateId == "failed")
        {
            isEnabled = false;
            disabledReason = "该任务已经失败，不能重新接取。";
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

    internal string _resolve_contract_board_quest_state_id(
        StringName quest_id,
        bool is_repeatable = false,
        bool can_restart_after_failure = false
    )
    {
        PartyState partyState = _owner.GetPartyState();
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
        if (partyState.HasFailedQuest(quest_id))
        {
            return can_restart_after_failure
                ? "restartable_failed"
                : "failed";
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

    internal string _build_contract_board_state_label(string state_id)
    {
        switch (state_id)
        {
            case "active":
                return "状态：进行中";
            case "claimable":
                return "状态：待领奖励";
            case "repeatable":
                return "状态：可重复接取";
            case "restartable_failed":
                return "状态：失败，可重新接取";
            case "failed":
                return "状态：已失败";
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
        int restartableFailedCount = 0;
        int failedCount = 0;
        int completedCount = 0;
        foreach (GDictionary entry in entries)
        {
            switch (GameRuntimeSettlementCommandHandler.ReadString(entry, "state_id"))
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
                case "restartable_failed":
                    restartableFailedCount += 1;
                    break;
                case "failed":
                    failedCount += 1;
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
        if (restartableFailedCount > 0)
        {
            parts.Add($"失败可重试 {restartableFailedCount}");
        }
        if (failedCount > 0)
        {
            parts.Add($"已失败 {failedCount}");
        }
        parts.Add($"已完成 {completedCount}");
        return string.Join("  |  ", parts);
    }

    internal string _build_contract_board_objective_summary(ContractBoardQuestData quest_data)
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
            quest_data.IsRepeatable,
            quest_data.QuestDefinition.CanRestartAfterFailure
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
            return $"提交物资 {_owner.GetItemDisplayName(objective_data.TargetId)}";
        }
        return "";
    }

    internal string _build_contract_board_reward_label(
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
                rewardParts.Add($"{_owner.GetItemDisplayName(rewardData.ItemId)} x{rewardData.ItemQuantity}");
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
        PartyState partyState = _owner.GetPartyState();
        if (partyState == null)
        {
            return null;
        }
        return partyState.GetActiveQuestState(quest_id);
    }

    internal StringName _resolve_active_submit_item_objective_id(
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

    internal bool _quest_has_submit_item_objective(
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

    private void _refresh_active_contract_board_context(string feedback_text = "")
    {
        using GodotProjectionLease<GDictionary> contextLease =
            _owner.GetActiveContractBoardContextLease();
        GDictionary context = contextLease.Value;
        if (context.Count == 0)
        {
            return;
        }
        string settlementId = GameRuntimeSettlementCommandHandler.ReadString(context, "settlement_id");
        GDictionary nextPayload = context;
        if (!string.IsNullOrEmpty(feedback_text))
        {
            nextPayload["feedback_text"] = feedback_text;
        }
        GDictionary nextContext = _build_contract_board_window_data(settlementId, nextPayload);
        _owner.SetActiveContractBoardContext(nextContext);
    }

    private void _set_contract_board_confirmation_context(StringName quest_id, string confirmation_text)
    {
        using GodotProjectionLease<GDictionary> contextLease =
            _owner.GetActiveContractBoardContextLease();
        GDictionary context = contextLease.Value;
        context["pending_confirmation_quest_id"] = quest_id.ToString();
        context["pending_confirmation_text"] = confirmation_text;
        context["pending_confirmation_source"] = "contract_board";
        _owner.SetActiveContractBoardContext(context);
    }

    private void _clear_contract_board_confirmation_context()
    {
        using GodotProjectionLease<GDictionary> contextLease =
            _owner.GetActiveContractBoardContextLease();
        GDictionary context = contextLease.Value;
        context.Remove("pending_confirmation_quest_id");
        context.Remove("pending_confirmation_text");
        context.Remove("pending_confirmation_source");
        _owner.SetActiveContractBoardContext(context);
    }

    internal bool _is_contract_board_modal_submission(GDictionary payload)
    {
        return GameRuntimeSettlementCommandHandler.ReadSubmissionSource(payload) == SettlementSubmissionSource.ContractBoard;
    }

    internal GDictionary _submit_contract_board_quest_action(
        string settlement_id,
        string action_id,
        GDictionary payload
    )
    {
        if (!_owner._has_runtime())
        {
            return _owner.CommandError("运行时尚未初始化。");
        }
        using GodotProjectionLease<GDictionary> contractBoardContextLease =
            _owner.GetActiveContractBoardContextLease();
        GDictionary contractBoardContext = contractBoardContextLease.Value;
        if (GameRuntimeSettlementCommandHandler.ReadString(contractBoardContext, "action_id").Trim() != action_id)
        {
            string actionMismatchMessage = "当前任务板与请求的服务入口不一致。";
            _owner.SetSettlementFeedbackText(actionMismatchMessage);
            _refresh_active_contract_board_context(actionMismatchMessage);
            _owner.UpdateStatus(actionMismatchMessage);
            return _owner.CommandError(actionMismatchMessage);
        }
        StringName questId = GameRuntimeSettlementCommandHandler.ReadStringName(payload, "quest_id");
        if (questId == "")
        {
            string missingIdMessage = "当前契约条目缺少 quest_id，无法接取。";
            _owner.SetSettlementFeedbackText(missingIdMessage);
            _refresh_active_contract_board_context(missingIdMessage);
            _owner.UpdateStatus(missingIdMessage);
            return _owner.CommandError(missingIdMessage);
        }
        ContractBoardQuestData questData = _resolve_contract_board_submission_quest_data(questId);
        if (questData == null)
        {
            string missingQuestMessage = $"当前任务板未找到契约 {questId}。";
            _owner.SetSettlementFeedbackText(missingQuestMessage);
            _refresh_active_contract_board_context(missingQuestMessage);
            _owner.UpdateStatus(missingQuestMessage);
            return _owner.CommandError(missingQuestMessage);
        }
        string providerInteractionId = GameRuntimeSettlementCommandHandler.ReadString(payload, "provider_interaction_id").Trim();
        if (string.IsNullOrEmpty(providerInteractionId))
        {
            string missingProviderMessage =
                "当前契约条目缺少 provider_interaction_id，无法匹配任务板。";
            _owner.SetSettlementFeedbackText(missingProviderMessage);
            _refresh_active_contract_board_context(missingProviderMessage);
            _owner.UpdateStatus(missingProviderMessage);
            return _owner.CommandError(missingProviderMessage);
        }
        string questProviderInteractionId = questData.ProviderInteractionId.Trim();
        if (questProviderInteractionId != providerInteractionId)
        {
            string providerMismatchMessage =
                $"契约 {questData.DisplayName} 不属于当前任务板。";
            _owner.SetSettlementFeedbackText(providerMismatchMessage);
            _refresh_active_contract_board_context(providerMismatchMessage);
            _owner.UpdateStatus(providerMismatchMessage);
            return _owner.CommandError(providerMismatchMessage);
        }

        QuestAcceptAvailabilityResult availability = _owner._quest_accept_evaluator.Evaluate(
            questData.QuestDefinition,
            _owner._build_quest_accept_context()
        );

        if (!availability.CanAccept)
        {
            string feedback = !string.IsNullOrEmpty(questData.AcceptFeedbackFailure)
                ? questData.AcceptFeedbackFailure
                : $"不满足接取条件：{availability.DisabledReason}";
            _refresh_active_contract_board_context(feedback);
            _owner.SetSettlementFeedbackText(feedback);
            _owner.UpdateStatus(feedback);
            return _owner.CommandError(feedback);
        }

        bool isConfirmationSubmission = GameRuntimeSettlementCommandHandler.ReadBool(payload, "confirm_accept", false);
        using GodotProjectionLease<GDictionary> activeContractBoardLease =
            _owner.GetActiveContractBoardContextLease();
        bool hasPendingConfirmation =
            GameRuntimeSettlementCommandHandler.ReadStringName(
                activeContractBoardLease.Value,
                "pending_confirmation_quest_id"
            ) == questId;

        if (!string.IsNullOrEmpty(questData.AcceptConfirmationText))
        {
            if (!isConfirmationSubmission && !hasPendingConfirmation)
            {
                _set_contract_board_confirmation_context(questId, questData.AcceptConfirmationText);
                return _owner.CommandOk("请确认是否接取该契约。");
            }

            if (isConfirmationSubmission && !hasPendingConfirmation)
            {
                string bypassMessage = "该契约需要先在面板中确认。";
                _refresh_active_contract_board_context(bypassMessage);
                _owner.SetSettlementFeedbackText(bypassMessage);
                _owner.UpdateStatus(bypassMessage);
                return _owner.CommandError(bypassMessage);
            }

            if (!isConfirmationSubmission && hasPendingConfirmation)
            {
                string pendingMessage = "请确认是否接取该契约。";
                _refresh_active_contract_board_context(pendingMessage);
                _owner.SetSettlementFeedbackText(pendingMessage);
                _owner.UpdateStatus(pendingMessage);
                return _owner.CommandOk(pendingMessage);
            }
        }

        if (hasPendingConfirmation)
            _clear_contract_board_confirmation_context();

        string stateId = _resolve_contract_board_quest_state_id(
            questData.QuestId,
            questData.IsRepeatable,
            questData.QuestDefinition.CanRestartAfterFailure
        );
        RuntimeCommandResult commandResult;
        bool isAcceptAction = false;
        if (stateId == "claimable")
        {
            commandResult = _owner.CommandClaimQuestTyped(questId);
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
                commandResult = _owner.CommandSubmitQuestItemTyped(
                    questId,
                    submitItemObjectiveId
                );
            }
            else
            {
                commandResult = _owner.CommandAcceptQuestTyped(questId, questData.IsRepeatable);
                isAcceptAction = true;
            }
        }
        else if (stateId == "failed")
        {
            commandResult = RuntimeCommandResult.Failure(
                $"契约《{questData.DisplayName}》已经失败，不能再次接取。",
                RuntimeCommandCode.InvalidState
            );
        }
        else
        {
            commandResult = _owner.CommandAcceptQuestTyped(questId, questData.IsRepeatable);
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
        _owner.SetActiveSettlementId(settlement_id);
        _owner.SetActiveModalKind(RuntimeModalKind.ContractBoard);
        _owner.SetSettlementFeedbackText(message);
        _refresh_active_contract_board_context(message);
        if (commandResult.Ok)
        {
            return _owner.CommandOk(message);
        }
        return _owner.CommandError(message);
    }

    private ContractBoardQuestData _resolve_contract_board_submission_quest_data(StringName quest_id)
    {
        if (quest_id == "")
            return null;
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs = _owner.GetQuestDefsTyped();
        if (
            !questDefs.TryGetValue(quest_id, out QuestDefinition questDefinition)
            || questDefinition == null
        )
            return null;
        return _build_contract_board_quest_data(questDefinition);
    }

    internal ContractBoardQuestData _build_contract_board_quest_data(
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
}
