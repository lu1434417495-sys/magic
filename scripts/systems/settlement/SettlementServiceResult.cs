using System.Collections.Generic;
using Godot;
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
        ReplacePayloadEntryList(_inventoryDelta, value);
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
        ReplacePayloadEntryList(_serviceSideEffects, effects);
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
        GDictionary values
    )
    {
        target.Clear();
        if (values == null)
            return;
        foreach (Variant key in values.Keys)
            target.Add(new SettlementServiceResultPayloadEntry(key, values[key]));
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
    internal readonly Variant Key;
    private readonly Variant _value;

    internal SettlementServiceResultPayloadEntry(Variant key, Variant value)
    {
        Key = key;
        _value = DuplicateVariant(value);
    }

    internal Variant Value => DuplicateVariant(_value);

    internal SettlementServiceResultPayloadEntry Duplicate() => new(Key, _value);

    private static Variant DuplicateVariant(Variant value)
    {
        return value.VariantType switch
        {
            Variant.Type.Dictionary => Variant.From(value.AsGodotDictionary().Duplicate(true)),
            Variant.Type.Array => Variant.From(value.AsGodotArray().Duplicate(true)),
            _ => value,
        };
    }
}
