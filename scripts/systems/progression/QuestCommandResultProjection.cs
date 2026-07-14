using System.Collections.Generic;
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

    internal static GodotProjectionLease<GDictionary> ProjectLease(
        QuestClaimResultData result
    )
    {
        IReadOnlyDictionary<string, object> plain = result != null
            ? new System.Collections.Generic.Dictionary<string, object>(
                System.StringComparer.Ordinal
            )
            {
                ["ok"] = result.Ok,
                ["error_code"] = result.ErrorCode,
                ["gold_delta"] = result.GoldDelta,
                ["item_rewards"] = result.CloneItemRewardsPlain(),
                ["pending_character_rewards"] = result.ClonePendingCharacterRewardsPlain(),
                ["unsupported_reward_types"] = BuildStringList(
                    result.CloneUnsupportedRewardTypes()
                ),
            }
            : new System.Collections.Generic.Dictionary<string, object>(
                System.StringComparer.Ordinal
            );
        return RuntimePlainPayload.ProjectDictionaryLease(
            plain,
            "QuestCommandResultProjection.claim_result",
            LifetimeDomain.Request,
            "QuestCommandResultProjection.claim_result"
        );
    }

    private static System.Collections.Generic.List<object> BuildStringList(
        System.Collections.Generic.IEnumerable<StringName> values
    )
    {
        var result = new System.Collections.Generic.List<object>();
        if (values == null)
            return result;
        foreach (StringName value in values)
            result.Add(value.ToString());
        return result;
    }
}
