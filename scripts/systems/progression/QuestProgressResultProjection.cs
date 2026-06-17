using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal static class QuestProgressResultProjection
{
    public static GDictionary Project(QuestProgressApplyResultData result)
    {
        if (result == null)
            return new GDictionary();

        return new GDictionary
        {
            ["accepted_quest_ids"] = result.CloneAcceptedQuestIds(),
            ["progressed_quest_ids"] = result.CloneProgressedQuestIds(),
            ["claimable_quest_ids"] = result.CloneClaimableQuestIds(),
            ["completed_quest_ids"] = result.CloneCompletedQuestIds(),
        };
    }

    public static GDictionary ProjectContext(QuestProgressEventContextData contextData)
    {
        GDictionary context = new();
        if (contextData == null)
            return context;
        if (contextData.ItemId != "")
            context["item_id"] = contextData.ItemId;
        if (contextData.SubmittedQuantity > 0)
            context["submitted_quantity"] = contextData.SubmittedQuantity;
        return context;
    }
}
