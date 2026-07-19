using System.Collections.Generic;

/// <summary>
/// 悬赏板窗口中的单个悬赏条目。
/// 内部为 plain C# DTO；headless 与 UI 都读取 typed/plain snapshot。
/// danger_stars/danger_source 来自 QuestDangerRatingResolver 的纯派生结果，
/// 只用于展示，不写入存档。
/// </summary>
internal sealed class BountyBoardEntryData
{
    public string QuestId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string ObjectiveSummary { get; set; } = "";
    public string RewardLabel { get; set; } = "";
    public string DetailsText { get; set; } = "";
    public int DangerStars { get; set; }
    public string DangerSource { get; set; } = "";
    public string DangerLabel { get; set; } = "";
    public string StateId { get; set; } = "";
    public string StateLabel { get; set; } = "";
    public string ActionLabel { get; set; } = "";
    public bool IsEnabled { get; set; }
    public string DisabledReason { get; set; } = "";
    public string LockReasonId { get; set; } = "";

    internal IReadOnlyDictionary<string, object> BuildSnapshotPlain()
    {
        return new Dictionary<string, object>(System.StringComparer.Ordinal)
        {
            ["quest_id"] = QuestId,
            ["display_name"] = DisplayName,
            ["objective_summary"] = ObjectiveSummary,
            ["reward_label"] = RewardLabel,
            ["details_text"] = DetailsText,
            ["danger_stars"] = DangerStars,
            ["danger_source"] = DangerSource,
            ["danger_label"] = DangerLabel,
            ["state_id"] = StateId,
            ["state_label"] = StateLabel,
            ["action_label"] = ActionLabel,
            ["is_enabled"] = IsEnabled,
            ["disabled_reason"] = DisabledReason,
            ["lock_reason_id"] = LockReasonId,
        };
    }
}

/// <summary>
/// 悬赏板窗口的完整运行时状态。与契约板（ShopWindow 复用）完全分离的类型链：
/// RuntimeModalKind.BountyBoard / SettlementPanelKind.BountyBoard /
/// SettlementSubmissionSource.BountyBoard。
/// V1 为单任务接取（P2 扩展批量时在此补 selected_quest_ids/max_batch_size）。
/// </summary>
internal sealed class BountyBoardWindowData
{
    public string SettlementId { get; set; } = "";

    // 当前据点的模板 id（= SettlementConfig.settlement_id），
    // 用于按 QuestDefinition.ListingSettlementIds 过滤悬赏条目。
    public string SettlementTemplateId { get; set; } = "";
    public string ActionId { get; set; } = "";
    public string ProviderInteractionId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Meta { get; set; } = "";
    public string FeedbackText { get; set; } = "";
    public string SelectedQuestId { get; set; } = "";
    public List<BountyBoardEntryData> Entries { get; set; } = new();

    internal static BountyBoardWindowData Empty => new();

    internal IReadOnlyDictionary<string, object> BuildSnapshotPlain()
    {
        var entries = new List<object>();
        foreach (BountyBoardEntryData entry in Entries)
        {
            if (entry != null)
                entries.Add(entry.BuildSnapshotPlain());
        }

        return new Dictionary<string, object>(System.StringComparer.Ordinal)
        {
            ["settlement_id"] = SettlementId,
            ["settlement_template_id"] = SettlementTemplateId,
            ["action_id"] = ActionId,
            ["provider_interaction_id"] = ProviderInteractionId,
            ["title"] = Title,
            ["meta"] = Meta,
            ["feedback_text"] = FeedbackText,
            ["selected_quest_id"] = SelectedQuestId,
            ["panel_kind"] = SettlementPanelKinds.ToPayloadValue(
                SettlementPanelKind.BountyBoard
            ),
            ["entries"] = entries,
        };
    }
}
