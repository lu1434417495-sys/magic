using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal static class QuestCommandResultProjection
{
    public static GDictionary Project(QuestSubmitItemResultData result)
    {
        if (result == null)
            return new GDictionary();

        return new GDictionary
        {
            ["ok"] = result.Ok,
            ["error_code"] = result.ErrorCode,
            ["objective_id"] = result.ObjectiveId,
            ["item_id"] = result.ItemId.ToString(),
            ["target_value"] = result.TargetValue,
            ["required_quantity"] = result.RequiredQuantity,
            ["submitted_quantity"] = result.SubmittedQuantity,
            ["accepted_quest_ids"] = result.CloneAcceptedQuestIds(),
            ["progressed_quest_ids"] = result.CloneProgressedQuestIds(),
            ["claimable_quest_ids"] = result.CloneClaimableQuestIds(),
            ["completed_quest_ids"] = result.CloneCompletedQuestIds(),
        };
    }

    public static GDictionary Project(QuestClaimResultData result)
    {
        if (result == null)
            return new GDictionary();

        return new GDictionary
        {
            ["ok"] = result.Ok,
            ["error_code"] = result.ErrorCode,
            ["gold_delta"] = result.GoldDelta,
            ["item_rewards"] = result.CloneItemRewards(),
            ["pending_character_rewards"] = result.ClonePendingCharacterRewards(),
            ["unsupported_reward_types"] = result.CloneUnsupportedRewardTypes(),
        };
    }
}
