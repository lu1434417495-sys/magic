using System.Collections.Generic;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal static class SettlementServiceResultProjection
{
    internal static GDictionary Project(SettlementServiceResult result)
    {
        if (result == null)
            return new GDictionary();

        return new GDictionary
        {
            ["success"] = result.Success,
            ["message"] = result.Message,
            ["persist_party_state"] = result.PersistPartyState,
            ["persist_world_data"] = result.PersistWorldData,
            ["persist_player_coord"] = result.PersistPlayerCoord,
            ["inventory_delta"] = result.InventoryDelta,
            ["gold_delta"] = result.GoldDelta,
            ["pending_character_rewards"] = PendingRewardDictionaryArray(
                result.PendingCharacterRewards
            ),
            ["quest_progress_events"] = QuestProgressEventDictionaryArray(
                result.QuestProgressEvents
            ),
            ["service_side_effects"] = result.ServiceSideEffects,
        };
    }

    private static GArray PendingRewardDictionaryArray(IEnumerable<PendingCharacterReward> values)
    {
        var result = new GArray();
        if (values == null)
            return result;

        foreach (PendingCharacterReward reward in values)
        {
            PendingCharacterReward copy = reward?.DuplicateState();
            if (copy != null && !copy.IsEmpty())
                result.Add(copy.ToDictionary());
        }
        return result;
    }

    private static GArray QuestProgressEventDictionaryArray(
        IEnumerable<QuestProgressService.QuestProgressEventData> values
    )
    {
        var result = new GArray();
        if (values == null)
            return result;

        foreach (QuestProgressService.QuestProgressEventData eventData in values)
        {
            if (eventData != null && eventData.IsValid)
                result.Add(eventData.ToDictionary());
        }
        return result;
    }
}
