using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public sealed class SettlementServiceResult
{
    private GDictionary _inventoryDelta = new();
    private readonly List<PendingCharacterReward> _pendingCharacterRewards = new();
    private readonly List<QuestProgressService.QuestProgressEventData> _questProgressEvents = new();
    private GDictionary _serviceSideEffects = new();

    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public bool PersistPartyState { get; set; }
    public bool PersistWorldData { get; set; }
    public bool PersistPlayerCoord { get; set; }
    public int GoldDelta { get; set; }
    public GDictionary InventoryDelta => DuplicateDictionary(_inventoryDelta);
    internal IReadOnlyList<PendingCharacterReward> PendingCharacterRewards =>
        DuplicatePendingRewardList(_pendingCharacterRewards);
    internal IReadOnlyList<QuestProgressService.QuestProgressEventData> QuestProgressEvents =>
        new List<QuestProgressService.QuestProgressEventData>(_questProgressEvents);
    public GDictionary ServiceSideEffects => DuplicateDictionary(_serviceSideEffects);

    public SettlementServiceResult SetInventoryDelta(GDictionary value)
    {
        _inventoryDelta = DuplicateDictionary(value);
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
        _serviceSideEffects = DuplicateDictionary(effects);
        return this;
    }

    public GDictionary ToDictionary()
    {
        return new GDictionary
        {
            ["success"] = Success,
            ["message"] = Message,
            ["persist_party_state"] = PersistPartyState,
            ["persist_world_data"] = PersistWorldData,
            ["persist_player_coord"] = PersistPlayerCoord,
            ["inventory_delta"] = DuplicateDictionary(_inventoryDelta),
            ["gold_delta"] = GoldDelta,
            ["pending_character_rewards"] = PendingRewardDictionaryArray(_pendingCharacterRewards),
            ["quest_progress_events"] = QuestProgressEventDictionaryArray(_questProgressEvents),
            ["service_side_effects"] = DuplicateDictionary(_serviceSideEffects),
        };
    }

    private static GDictionary DuplicateDictionary(GDictionary value)
    {
        return value?.Duplicate(true) ?? new GDictionary();
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

    private static GArray PendingRewardDictionaryArray(IEnumerable<PendingCharacterReward> values)
    {
        var result = new GArray();
        if (values == null)
        {
            return result;
        }
        foreach (PendingCharacterReward reward in values)
        {
            PendingCharacterReward copy = reward?.DuplicateState();
            if (copy != null && !copy.IsEmpty())
            {
                result.Add(copy.ToDictionary());
            }
        }
        return result;
    }

    private static GArray QuestProgressEventDictionaryArray(
        IEnumerable<QuestProgressService.QuestProgressEventData> values
    )
    {
        var result = new GArray();
        if (values == null)
        {
            return result;
        }
        foreach (QuestProgressService.QuestProgressEventData eventData in values)
        {
            if (eventData != null && eventData.IsValid)
            {
                result.Add(eventData.ToDictionary());
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
