using Godot;

// Quest-command application port. It exposes detached quest facts and semantic mutations,
// never the facade's PartyState, content catalog, or CharacterManagementModule owners.
internal interface IGameRuntimeQuestCommandPort
{
    bool IsAvailable();

    QuestCommandDefData GetQuestCommandDefData(StringName questId);

    QuestCommandStateData GetQuestCommandStateData(StringName questId);

    int GetWorldStep();

    string GetItemDisplayName(StringName itemId);

    bool AcceptQuestAndSyncParty(StringName questId, bool allowReaccept);

    QuestProgressApplyResultData ApplyDirectQuestProgressAndSyncParty(
        StringName questId,
        StringName objectiveId,
        int progressDelta,
        QuestProgressCommandPayloadData progressPayload
    );

    bool CompleteQuestAndSyncParty(StringName questId);

    QuestSubmitItemResultData SubmitItemObjectiveAndSyncParty(
        StringName questId,
        StringName objectiveId
    );

    QuestClaimResultData ClaimQuestRewardAndSyncParty(StringName questId);

    Error PersistQuestPartyState();

    void UpdateStatus(string message);
}

internal readonly struct QuestCommandStateData
{
    internal bool IsActive { get; }
    internal bool IsClaimable { get; }
    internal bool IsCompleted { get; }
    internal bool IsFailed { get; }

    internal QuestCommandStateData(
        bool isActive,
        bool isClaimable,
        bool isCompleted,
        bool isFailed
    )
    {
        IsActive = isActive;
        IsClaimable = isClaimable;
        IsCompleted = isCompleted;
        IsFailed = isFailed;
    }
}
