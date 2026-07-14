using System.Collections.Generic;

/// <summary>
/// NPC 委托 offer 窗口中的单个任务条目。
/// 内部为 plain C# DTO；headless 与 UI 都读取 typed/plain snapshot。
/// </summary>
internal sealed class NpcQuestOfferEntryData
{
    public string QuestId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public string AcceptDialogueText { get; set; } = "";
    public string SummaryText { get; set; } = "";
    public string CostLabel { get; set; } = "";
    public bool IsEnabled { get; set; }
    public string DisabledReason { get; set; } = "";
    public string LockReasonId { get; set; } = "";
    public string AcceptFeedbackSuccess { get; set; } = "";
    public string AcceptFeedbackFailure { get; set; } = "";
    public string AcceptConfirmationText { get; set; } = "";

    internal IReadOnlyDictionary<string, object> BuildSnapshotPlain()
    {
        return new Dictionary<string, object>(System.StringComparer.Ordinal)
        {
            ["quest_id"] = QuestId,
            ["display_name"] = DisplayName,
            ["description"] = Description,
            ["accept_dialogue_text"] = AcceptDialogueText,
            ["summary_text"] = SummaryText,
            ["cost_label"] = CostLabel,
            ["is_enabled"] = IsEnabled,
            ["disabled_reason"] = DisabledReason,
            ["lock_reason_id"] = LockReasonId,
            ["accept_feedback_success"] = AcceptFeedbackSuccess,
            ["accept_feedback_failure"] = AcceptFeedbackFailure,
            ["accept_confirmation_text"] = AcceptConfirmationText,
        };
    }

}

/// <summary>
/// NPC 委托 offer 窗口的完整运行时状态。
/// 内部为 plain C# DTO；headless 与 Godot UI 都读取 typed/plain snapshot。
/// </summary>
internal sealed class NpcQuestOfferWindowData
{
    public string SettlementId { get; set; } = "";
    public string ActionId { get; set; } = "";
    public string NpcInteractionId { get; set; } = "";
    public string NpcName { get; set; } = "";
    public string SelectedQuestId { get; set; } = "";
    public List<NpcQuestOfferEntryData> Entries { get; set; } = new();
    public string FeedbackText { get; set; } = "";
    public string PendingConfirmationQuestId { get; set; } = "";
    public string PendingConfirmationText { get; set; } = "";
    public string PendingConfirmationSource { get; set; } = "";

    internal static NpcQuestOfferWindowData Empty => new();

    internal IReadOnlyDictionary<string, object> BuildSnapshotPlain()
    {
        var entries = new List<object>();
        foreach (NpcQuestOfferEntryData entry in Entries)
        {
            if (entry != null)
                entries.Add(entry.BuildSnapshotPlain());
        }

        var result = new Dictionary<string, object>(System.StringComparer.Ordinal)
        {
            ["settlement_id"] = SettlementId,
            ["action_id"] = ActionId,
            ["npc_interaction_id"] = NpcInteractionId,
            ["npc_name"] = NpcName,
            ["selected_quest_id"] = SelectedQuestId,
            ["entries"] = entries,
            ["feedback_text"] = FeedbackText,
        };
        if (!string.IsNullOrEmpty(PendingConfirmationQuestId))
        {
            result["pending_confirmation_quest_id"] = PendingConfirmationQuestId;
            result["pending_confirmation_text"] = PendingConfirmationText;
            result["pending_confirmation_source"] = PendingConfirmationSource;
        }
        return result;
    }

}
