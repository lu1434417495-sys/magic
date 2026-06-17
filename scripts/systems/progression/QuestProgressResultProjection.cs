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

    public static GDictionary Project(QuestProgressService.QuestProgressEventData eventData)
    {
        if (eventData == null || !eventData.IsValid)
            return new GDictionary();

        GDictionary result = new()
        {
            ["event_type"] = eventData.EventType,
            ["world_step"] = eventData.WorldStep,
        };

        if (eventData.QuestId != "")
            result["quest_id"] = eventData.QuestId;
        if (eventData.ObjectiveId != "")
            result["objective_id"] = eventData.ObjectiveId;
        if (eventData.ObjectiveType != "")
            result["objective_type"] = eventData.ObjectiveType;
        if (eventData.TargetId != "")
            result["target_id"] = eventData.TargetId;
        if (eventData.AllowReaccept)
            result["allow_reaccept"] = true;
        if (eventData.AutoAccept)
            result["auto_accept"] = true;
        if (eventData.ProgressDelta > 0)
            result["progress_delta"] = eventData.ProgressDelta;
        if (eventData.HasTargetValue)
            result["target_value"] = eventData.TargetValue;
        if (eventData.EncounterId != "")
            result["encounter_id"] = eventData.EncounterId;
        if (eventData.EncounterKind != "")
            result["encounter_kind"] = eventData.EncounterKind;

        GDictionary context = ProjectContext(eventData.ContextData);
        if (context.Count > 0)
            result["context"] = context;
        AppendTopLevelContextMetadata(result, eventData.ContextData);
        return result;
    }

    public static GDictionary ProjectProgressRecordContext(
        QuestProgressEventContextData contextData
    )
    {
        GDictionary context = ProjectContext(contextData);
        AppendTopLevelContextMetadata(context, contextData);
        return context;
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

    private static void AppendTopLevelContextMetadata(
        GDictionary target,
        QuestProgressEventContextData contextData
    )
    {
        if (target == null || contextData == null)
            return;
        if (contextData.MemberId != "")
            target["member_id"] = contextData.MemberId;
        if (!string.IsNullOrEmpty(contextData.ActionId))
            target["action_id"] = contextData.ActionId;
        if (contextData.EnemyTemplateId != "")
            target["enemy_template_id"] = contextData.EnemyTemplateId;
        if (!string.IsNullOrEmpty(contextData.SettlementId))
            target["settlement_id"] = contextData.SettlementId;
        if (contextData.SourceType != "")
            target["source_type"] = contextData.SourceType;
        if (contextData.SourceId != "")
            target["source_id"] = contextData.SourceId;
    }
}
