using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GArray = Godot.Collections.Array;
using GDictArray = Godot.Collections.Array<Godot.Collections.Dictionary>;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

internal sealed class NpcQuestOfferActionRequest
{
    internal StringName QuestId { get; init; } = "";
    internal bool ConfirmAccept { get; init; }

    internal static bool TryParse(GDictionary payload, out NpcQuestOfferActionRequest request)
    {
        request = null;
        if (payload == null)
            return false;
        StringName questId = GameRuntimeSettlementCommandHandler.ReadStringName(payload, "quest_id");
        if (questId == "")
            return false;
        request = new NpcQuestOfferActionRequest
        {
            QuestId = questId,
            ConfirmAccept = GameRuntimeSettlementCommandHandler.ReadBool(payload, "confirm_accept", false),
        };
        return true;
    }
}

internal sealed class GameRuntimeNpcQuestOfferCommandHandler
{
    private GameRuntimeSettlementCommandHandler _owner;
    private GameRuntimeContractBoardCommandHandler _contractBoardHandler;

    internal void Setup(
        GameRuntimeSettlementCommandHandler owner,
        GameRuntimeContractBoardCommandHandler contractBoardHandler
    )
    {
        _owner = owner;
        _contractBoardHandler = contractBoardHandler;
    }

    internal IReadOnlyDictionary<string, object> GetNpcQuestOfferWindowDataSnapshotPlain()
    {
        NpcQuestOfferWindowData data = GetActiveNpcQuestOfferContextTyped();
        return data?.BuildSnapshotPlain() ?? GameRuntimeSettlementCommandHandler.EmptyPlainDictionary();
    }

    internal NpcQuestOfferWindowData GetActiveNpcQuestOfferContextTyped()
    {
        return _owner.GetActiveNpcQuestOfferData();
    }

    internal bool _try_open_npc_quest_offer(
        string settlement_id,
        string action_id,
        GDictionary payload,
        out GDictionary result
    )
    {
        result = new GDictionary();
        if (_is_npc_quest_offer_modal_submission(payload))
            return false;
        string interactionScriptId = GameRuntimeSettlementCommandHandler.ReadString(payload, "interaction_script_id");
        if (interactionScriptId == "")
            return false;

        var npcQuests = new List<QuestDefinition>();
        foreach (QuestDefinition questDefinition in _owner.GetQuestDefsTyped().Values)
        {
            if (questDefinition.ProviderKind != "npc")
                continue;
            if (questDefinition.ProviderInteractionId != interactionScriptId)
                continue;
            if (!questDefinition.ListingChannels.Contains(GameRuntimeSettlementCommandHandler.NPC_OFFER_LISTING_CHANNEL))
                continue;
            npcQuests.Add(questDefinition);
        }

        if (npcQuests.Count == 0)
            return false;

        NpcQuestOfferWindowData windowData = _build_npc_quest_offer_window_data(
            settlement_id,
            action_id,
            interactionScriptId,
            npcQuests
        );
        _owner.SetActiveNpcQuestOfferContext(windowData);
        _owner.SetActiveModalKind(RuntimeModalKind.NpcQuestOffer);
        _owner.UpdateStatus($"已打开 {_resolve_npc_display_name(interactionScriptId)} 的委托。");
        result = _owner.CommandOk($"已打开 {interactionScriptId} 的委托。");
        return true;
    }

    private NpcQuestOfferWindowData _build_npc_quest_offer_window_data(
        string settlement_id,
        string actionId,
        string npcInteractionId,
        List<QuestDefinition> npcQuests
    )
    {
        var windowData = new NpcQuestOfferWindowData
        {
            SettlementId = settlement_id,
            ActionId = actionId,
            NpcInteractionId = npcInteractionId,
            NpcName = _resolve_npc_display_name(npcInteractionId),
            SelectedQuestId = npcQuests[0].QuestId.ToString(),
        };

        foreach (QuestDefinition questDefinition in npcQuests)
        {
            ContractBoardQuestData questData = _contractBoardHandler._build_contract_board_quest_data(
                questDefinition
            );
            string stateId = _contractBoardHandler._resolve_contract_board_quest_state_id(
                questDefinition.QuestId,
                questDefinition.IsRepeatable,
                questDefinition.CanRestartAfterFailure
            );
            bool hasSubmitItemObjective = questData != null
                && _contractBoardHandler._quest_has_submit_item_objective(questData.ObjectiveEntries);
            bool isEnabled;
            string disabledReason;
            StringName lockReasonId;
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
            else if (stateId == "active")
            {
                isEnabled = hasSubmitItemObjective;
                disabledReason = isEnabled ? "" : "任务目标尚未完成。";
                lockReasonId = "";
            }
            else if (stateId == "claimable")
            {
                isEnabled = true;
                disabledReason = "";
                lockReasonId = "";
            }
            else
            {
                isEnabled = false;
                disabledReason = "任务已完成。";
                lockReasonId = "";
            }
            windowData.Entries.Add(
                new NpcQuestOfferEntryData
                {
                    QuestId = questDefinition.QuestId.ToString(),
                    DisplayName = questDefinition.DisplayName,
                    Description = questDefinition.Description,
                    AcceptDialogueText = questDefinition.AcceptDialogueText,
                    SummaryText = questData != null
                        ? _contractBoardHandler._build_contract_board_objective_summary(questData)
                        : "",
                    CostLabel = questData != null
                        ? _contractBoardHandler._build_contract_board_reward_label(questData.RewardEntries)
                        : "奖励：无",
                    StateId = stateId,
                    StateLabel = _contractBoardHandler._build_contract_board_state_label(stateId),
                    ActionLabel = _build_npc_quest_action_label(
                        stateId,
                        hasSubmitItemObjective
                    ),
                    IsEnabled = isEnabled,
                    DisabledReason = disabledReason,
                    LockReasonId = lockReasonId,
                    AcceptFeedbackSuccess = questDefinition.AcceptFeedbackSuccess,
                    AcceptFeedbackFailure = questDefinition.AcceptFeedbackFailure,
                    AcceptConfirmationText = questDefinition.AcceptConfirmationText,
                }
            );
        }

        return windowData;
    }

    private static string _build_npc_quest_action_label(
        string state_id,
        bool has_submit_item_objective
    )
    {
        return state_id switch
        {
            "active" => has_submit_item_objective ? "提交物品" : "进行中",
            "claimable" => "领取奖励",
            "completed" => "已完成",
            "failed" => "已失败",
            "restartable_failed" => "重新接取",
            _ => "接受委托",
        };
    }

    private static string _resolve_npc_display_name(string npcInteractionId)
    {
        if (npcInteractionId.StartsWith("npc_"))
            npcInteractionId = npcInteractionId.Substring(4);
        return npcInteractionId.Replace("_", " ");
    }

    internal bool _is_npc_quest_offer_modal_submission(GDictionary payload) =>
        GameRuntimeSettlementCommandHandler.ReadString(payload, "submission_source") == "npc_quest_offer";


    internal GDictionary _submit_npc_quest_offer_action(
        string settlement_id,
        string action_id,
        GDictionary payload
    )
    {
        if (!_owner._has_runtime())
        {
            return _owner.CommandError("运行时尚未初始化。");
        }
        if (_owner.GetActiveModalKind() != RuntimeModalKind.NpcQuestOffer)
        {
            string notOpenMessage = "当前没有打开 NPC 委托面板。";
            _owner.UpdateStatus(notOpenMessage);
            return _owner.CommandError(notOpenMessage);
        }

        if (!NpcQuestOfferActionRequest.TryParse(payload, out NpcQuestOfferActionRequest request))
        {
            string missingIdMessage = "NPC 委托提交缺少 quest_id。";
            _owner.UpdateStatus(missingIdMessage);
            return _owner.CommandError(missingIdMessage);
        }

        NpcQuestOfferWindowData npcContext = GetActiveNpcQuestOfferContextTyped();
        if (npcContext == null || npcContext.SettlementId.Trim() != settlement_id)
        {
            string settlementMismatchMessage = "当前 NPC 委托面板与请求的据点不一致。";
            _owner.UpdateStatus(settlementMismatchMessage);
            return _owner.CommandError(settlementMismatchMessage);
        }

        StringName questId = request.QuestId;
        QuestDefinition questDefinition = _owner.GetQuestDefinition(questId);
        if (questDefinition == null || questDefinition.ProviderKind != "npc")
        {
            string notNpcMessage = "该任务不是 NPC 委托。";
            _owner.UpdateStatus(notNpcMessage);
            return _owner.CommandError(notNpcMessage);
        }

        if (questDefinition.ProviderInteractionId != npcContext.NpcInteractionId)
        {
            string wrongNpcMessage = "该任务不属于当前 NPC。";
            _owner.UpdateStatus(wrongNpcMessage);
            return _owner.CommandError(wrongNpcMessage);
        }

        if (!questDefinition.ListingChannels.Contains(GameRuntimeSettlementCommandHandler.NPC_OFFER_LISTING_CHANNEL))
        {
            string notOfferMessage = "该任务未配置为 NPC 委托。";
            _owner.UpdateStatus(notOfferMessage);
            return _owner.CommandError(notOfferMessage);
        }

        string stateId = _contractBoardHandler._resolve_contract_board_quest_state_id(
            questDefinition.QuestId,
            questDefinition.IsRepeatable,
            questDefinition.CanRestartAfterFailure
        );
        bool isAcceptAction =
            stateId is "available" or "repeatable" or "restartable_failed";
        bool isConfirmationSubmission = request.ConfirmAccept;
        bool hasPendingConfirmation = npcContext.PendingConfirmationQuestId == questId.ToString();
        if (isAcceptAction)
        {
            QuestAcceptAvailabilityResult availability = _owner._quest_accept_evaluator.Evaluate(
                questDefinition,
                _owner._build_quest_accept_context()
            );
            if (!availability.CanAccept)
            {
                string feedback = !string.IsNullOrEmpty(questDefinition.AcceptFeedbackFailure)
                    ? questDefinition.AcceptFeedbackFailure
                    : $"不满足接取条件：{availability.DisabledReason}";
                _refresh_active_npc_quest_offer_context(feedback);
                _owner.UpdateStatus(feedback);
                return _owner.CommandError(feedback);
            }

            if (!string.IsNullOrEmpty(questDefinition.AcceptConfirmationText))
            {
                if (!isConfirmationSubmission && !hasPendingConfirmation)
                {
                    _set_npc_quest_offer_confirmation_context(
                        questId,
                        questDefinition.AcceptConfirmationText
                    );
                    return _owner.CommandOk("请确认是否接受该委托。");
                }

                if (isConfirmationSubmission && !hasPendingConfirmation)
                {
                    string bypassMessage = "该委托需要先在面板中确认。";
                    _refresh_active_npc_quest_offer_context(bypassMessage);
                    _owner.UpdateStatus(bypassMessage);
                    return _owner.CommandError(bypassMessage);
                }

                if (!isConfirmationSubmission && hasPendingConfirmation)
                {
                    string pendingMessage = "请确认是否接受该委托。";
                    _refresh_active_npc_quest_offer_context(pendingMessage);
                    _owner.UpdateStatus(pendingMessage);
                    return _owner.CommandOk(pendingMessage);
                }
            }
        }

        if (hasPendingConfirmation)
            _clear_npc_quest_offer_confirmation_context();

        RuntimeCommandResult commandResult;
        if (stateId == "claimable")
        {
            commandResult = _owner.CommandClaimQuestTyped(questId);
        }
        else if (stateId == "active")
        {
            ContractBoardQuestData questData = _contractBoardHandler._build_contract_board_quest_data(questDefinition);
            StringName submitItemObjectiveId = questData == null
                ? ""
                : _contractBoardHandler._resolve_active_submit_item_objective_id(
                    questId,
                    questData.ObjectiveEntries
                );
            if (
                questData == null
                || (
                    submitItemObjectiveId == ""
                    && !_contractBoardHandler._quest_has_submit_item_objective(questData.ObjectiveEntries)
                )
            )
            {
                string activeMessage = "任务目标尚未完成。";
                _refresh_active_npc_quest_offer_context(activeMessage);
                _owner.UpdateStatus(activeMessage);
                return _owner.CommandError(activeMessage);
            }
            commandResult = _owner.CommandSubmitQuestItemTyped(
                questId,
                submitItemObjectiveId
            );
        }
        else if (stateId == "completed")
        {
            string completedMessage = "该委托已经完成。";
            _refresh_active_npc_quest_offer_context(completedMessage);
            _owner.UpdateStatus(completedMessage);
            return _owner.CommandError(completedMessage);
        }
        else if (stateId == "failed")
        {
            string failedMessage = "该委托已经失败，不能重新接取。";
            _refresh_active_npc_quest_offer_context(failedMessage);
            _owner.UpdateStatus(failedMessage);
            return _owner.CommandError(failedMessage);
        }
        else
        {
            commandResult = _owner.CommandAcceptQuestTyped(
                questId,
                questDefinition.IsRepeatable
            );
        }
        if (!commandResult.Ok)
        {
            _refresh_active_npc_quest_offer_context(commandResult.Message);
            _owner.UpdateStatus(commandResult.Message);
            return _owner.CommandError(commandResult.Message);
        }

        string successFeedback = isAcceptAction
            ? !string.IsNullOrEmpty(questDefinition.AcceptFeedbackSuccess)
                ? questDefinition.AcceptFeedbackSuccess
                : $"已接受委托 {questDefinition.DisplayName}。"
            : string.IsNullOrEmpty(commandResult.Message)
                ? stateId == "claimable"
                    ? $"已领取 {questDefinition.DisplayName} 的奖励。"
                    : $"已提交 {questDefinition.DisplayName} 的任务物品。"
                : commandResult.Message;
        _refresh_active_npc_quest_offer_context(successFeedback);
        _owner.UpdateStatus(successFeedback);
        return _owner.CommandOk(successFeedback);
    }

    private void _refresh_active_npc_quest_offer_context(string feedback_text)
    {
        NpcQuestOfferWindowData context = GetActiveNpcQuestOfferContextTyped();
        if (context == null)
            return;

        string settlementId = context.SettlementId;
        string npcInteractionId = context.NpcInteractionId;
        var npcQuests = new List<QuestDefinition>();
        foreach (QuestDefinition questDefinition in _owner.GetQuestDefsTyped().Values)
        {
            if (questDefinition.ProviderKind != "npc")
                continue;
            if (questDefinition.ProviderInteractionId != npcInteractionId)
                continue;
            if (!questDefinition.ListingChannels.Contains(GameRuntimeSettlementCommandHandler.NPC_OFFER_LISTING_CHANNEL))
                continue;
            npcQuests.Add(questDefinition);
        }

        if (npcQuests.Count == 0)
            return;

        NpcQuestOfferWindowData refreshed = _build_npc_quest_offer_window_data(
            settlementId,
            context.ActionId,
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
        _owner.SetActiveNpcQuestOfferContext(refreshed);
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
        _owner.SetActiveNpcQuestOfferContext(context);
    }

    private void _clear_npc_quest_offer_confirmation_context()
    {
        NpcQuestOfferWindowData context = GetActiveNpcQuestOfferContextTyped();
        if (context == null)
            return;
        context.PendingConfirmationQuestId = "";
        context.PendingConfirmationText = "";
        context.PendingConfirmationSource = "";
        _owner.SetActiveNpcQuestOfferContext(context);
    }
}
