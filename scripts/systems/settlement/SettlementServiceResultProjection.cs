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
            ["inventory_delta"] = ProjectInventoryDelta(result),
            ["gold_delta"] = result.GoldDelta,
            ["pending_character_rewards"] = PendingRewardDictionaryArray(
                result.PendingCharacterRewards
            ),
            ["quest_progress_events"] = QuestProgressEventDictionaryArray(
                result.QuestProgressEvents
            ),
            ["service_side_effects"] = ProjectServiceSideEffects(result),
        };
    }

    internal static GDictionary ProjectInventoryDelta(SettlementServiceResult result) =>
        result != null ? ProjectPayloadEntries(result.InventoryDeltaEntries) : new GDictionary();

    internal static GDictionary ProjectServiceSideEffects(SettlementServiceResult result) =>
        result != null ? ProjectPayloadEntries(result.ServiceSideEffectEntries) : new GDictionary();

    private static GArray PendingRewardDictionaryArray(IEnumerable<PendingCharacterReward> values)
    {
        var result = new GArray();
        if (values == null)
            return result;

        foreach (PendingCharacterReward reward in values)
        {
            PendingCharacterReward copy = reward?.DuplicateState();
            if (copy != null && !copy.IsEmpty())
                result.Add(PendingCharacterRewardPayload.Project(copy));
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
                result.Add(QuestProgressResultProjection.Project(eventData));
        }
        return result;
    }

    private static GDictionary ProjectPayloadEntries(
        IEnumerable<SettlementServiceResultPayloadEntry> entries
    )
    {
        var plain = new Dictionary<string, object>(System.StringComparer.Ordinal);
        if (entries == null)
            return new GDictionary();
        foreach (SettlementServiceResultPayloadEntry entry in entries)
            plain[entry.Key] = entry.Value;

        using GodotProjectionLease<GDictionary> lease =
            RuntimePlainPayload.ProjectDictionaryLease(
                plain,
                "settlement-service-result",
                LifetimeDomain.Request,
                "SettlementServiceResultProjection payload"
            );
        return lease.Value.Duplicate(true);
    }
}
