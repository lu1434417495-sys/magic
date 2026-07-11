using System.Collections.Generic;
using GDictionary = Godot.Collections.Dictionary;

public sealed class SettlementServiceResult
{
    private readonly List<SettlementServiceResultPayloadEntry> _inventoryDelta = new();
    private readonly List<PendingCharacterReward> _pendingCharacterRewards = new();
    private readonly List<QuestProgressService.QuestProgressEventData> _questProgressEvents = new();
    private readonly List<SettlementServiceResultPayloadEntry> _serviceSideEffects = new();

    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public bool PersistPartyState { get; set; }
    public bool PersistWorldData { get; set; }
    public bool PersistPlayerCoord { get; set; }
    public int GoldDelta { get; set; }
    internal IReadOnlyList<SettlementServiceResultPayloadEntry> InventoryDeltaEntries =>
        DuplicatePayloadEntryList(_inventoryDelta);
    internal IReadOnlyList<PendingCharacterReward> PendingCharacterRewards =>
        DuplicatePendingRewardList(_pendingCharacterRewards);
    internal IReadOnlyList<QuestProgressService.QuestProgressEventData> QuestProgressEvents =>
        new List<QuestProgressService.QuestProgressEventData>(_questProgressEvents);
    internal IReadOnlyList<SettlementServiceResultPayloadEntry> ServiceSideEffectEntries =>
        DuplicatePayloadEntryList(_serviceSideEffects);

    public SettlementServiceResult SetInventoryDelta(GDictionary value)
    {
        ReplacePayloadEntryList(
            _inventoryDelta,
            value,
            "SettlementServiceResult.inventory_delta"
        );
        return this;
    }

    internal SettlementServiceResult SetPendingCharacterRewardsTyped(
        IEnumerable<PendingCharacterReward> rewards
    )
    {
        ReplacePendingRewardList(_pendingCharacterRewards, rewards);
        return this;
    }

    internal SettlementServiceResult SetQuestProgressEventsTyped(
        IEnumerable<QuestProgressService.QuestProgressEventData> events
    )
    {
        ReplaceQuestProgressEventList(_questProgressEvents, events);
        return this;
    }

    public SettlementServiceResult SetServiceSideEffects(GDictionary effects)
    {
        ReplacePayloadEntryList(
            _serviceSideEffects,
            effects,
            "SettlementServiceResult.service_side_effects"
        );
        return this;
    }

    private static IReadOnlyList<SettlementServiceResultPayloadEntry> DuplicatePayloadEntryList(
        IEnumerable<SettlementServiceResultPayloadEntry> values
    )
    {
        var result = new List<SettlementServiceResultPayloadEntry>();
        if (values == null)
            return result;
        foreach (SettlementServiceResultPayloadEntry entry in values)
            result.Add(entry.Duplicate());
        return result;
    }

    private static void ReplacePayloadEntryList(
        List<SettlementServiceResultPayloadEntry> target,
        GDictionary values,
        string ownerPath
    )
    {
        Dictionary<string, object> normalized = RuntimePlainPayload.NormalizeDictionaryStrict(
            values,
            ownerPath
        );
        target.Clear();
        foreach (KeyValuePair<string, object> entry in normalized)
            target.Add(new SettlementServiceResultPayloadEntry(entry.Key, entry.Value));
    }

    private static IReadOnlyList<PendingCharacterReward> DuplicatePendingRewardList(
        IEnumerable<PendingCharacterReward> values
    )
    {
        var result = new List<PendingCharacterReward>();
        if (values == null)
        {
            return result;
        }
        foreach (PendingCharacterReward reward in values)
        {
            PendingCharacterReward copy = reward?.DuplicateState();
            if (copy != null && !copy.IsEmpty())
            {
                result.Add(copy);
            }
        }
        return result;
    }

    private static void ReplacePendingRewardList(
        List<PendingCharacterReward> target,
        IEnumerable<PendingCharacterReward> values
    )
    {
        target.Clear();
        if (values == null)
        {
            return;
        }
        foreach (PendingCharacterReward reward in values)
        {
            PendingCharacterReward copy = reward?.DuplicateState();
            if (copy != null && !copy.IsEmpty())
            {
                target.Add(copy);
            }
        }
    }

    private static void ReplaceQuestProgressEventList(
        List<QuestProgressService.QuestProgressEventData> target,
        IEnumerable<QuestProgressService.QuestProgressEventData> values
    )
    {
        target.Clear();
        if (values == null)
        {
            return;
        }
        foreach (QuestProgressService.QuestProgressEventData eventData in values)
        {
            if (eventData != null && eventData.IsValid)
            {
                target.Add(eventData);
            }
        }
    }
}

internal readonly struct SettlementServiceResultPayloadEntry
{
    internal readonly string Key;
    private readonly object _value;

    internal SettlementServiceResultPayloadEntry(string key, object value)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new System.InvalidOperationException(
                "Settlement service result payload entries require a non-empty string key."
            );
        }
        Key = key;
        _value = RuntimePlainPayload.CloneValue(value);
    }

    internal object Value => RuntimePlainPayload.CloneValue(_value);

    internal SettlementServiceResultPayloadEntry Duplicate() => new(Key, _value);
}
