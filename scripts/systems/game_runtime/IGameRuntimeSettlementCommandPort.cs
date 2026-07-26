using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

// Settlement-command application boundary. The handler owns workflow decisions;
// the facade adapter supplies only settlement-scoped state, content, persistence,
// modal, and world capabilities. GameSession and GameRuntimeFacade never cross it.
internal interface IGameRuntimeSettlementStatePort
{
    bool IsBattleActive();
    void UpdateStatus(string message);
    string GetActiveSettlementId();
    void SetActiveSettlementId(string settlementId);
    string GetSettlementFeedbackText();
    void SetSettlementFeedbackText(string feedbackText);
    WorldMapSettlementData GetSelectedSettlementData();
    PartyState GetPartyState();
    GodotProjectionLease<GDictionary> GetSettlementRecordLease(string settlementId);
    GodotProjectionLease<GArray> GetAllSettlementRecordsLease();
    IReadOnlyDictionary<string, object> GetSettlementRecordSnapshotPlain(string settlementId);
    WorldMapSettlementStateData GetSettlementStateData(string settlementId);
    bool SetActiveSettlementState(
        string settlementId,
        WorldMapSettlementStateData settlementState
    );
    PartyWarehouseService GetPartyWarehouseService();
    AttributeSnapshot GetMemberAttributeSnapshot(StringName memberId);
    string GetMemberDisplayName(StringName memberId);
    void OpenPartyWarehouseWindow(string entryLabel);
    void EnqueuePendingCharacterRewardsTyped(IEnumerable<PendingCharacterReward> rewards);
    void RecordMemberAchievementEvent(
        StringName memberId,
        StringName eventId,
        int value,
        StringName detailId
    );
    void SyncPartyStateFromCharacterManagement();
    PendingCharacterReward BuildPendingCharacterReward(
        StringName memberId,
        StringName rewardId,
        StringName sourceType,
        StringName sourceId,
        string sourceLabel,
        IReadOnlyList<PendingCharacterRewardEntry> entries,
        string summaryText
    );
    GDictionary ResolveLowLuckSettlementEventRewards(GDictionary context);
    QuestProgressApplyResultData ApplyQuestProgressEventsToPartyTyped(
        IEnumerable<QuestProgressService.QuestProgressEventData> eventOptions,
        string sourceDomain
    );
    IReadOnlyList<StringName> HandleMisfortuneForgeResult(
        StringName memberId,
        SettlementServiceResult result
    );
}

internal interface IGameRuntimeSettlementContentPort
{
    IReadOnlyDictionary<StringName, ItemDefinition> GetItemDefinitions();
    IReadOnlyDictionary<StringName, TraitDefinition> GetTraitDefinitions();
    IReadOnlyDictionary<StringName, RecipeDefinition> GetRecipeDefinitions();
    IReadOnlyDictionary<StringName, QuestDefinition> GetQuestDefinitions();
    IReadOnlyDictionary<StringName, EnemyTemplateDefinition> GetEnemyTemplateDefinitions();
    QuestDefinition GetQuestDefinition(StringName questId);
    string GetItemDisplayName(StringName itemId);
    RuntimeCommandResult CommandAcceptQuestTyped(StringName questId, bool allowRepeatable);
    RuntimeCommandResult CommandClaimQuestTyped(StringName questId);
    RuntimeCommandResult CommandSubmitQuestItemTyped(
        StringName questId,
        StringName objectiveId
    );
}

internal interface IGameRuntimeSettlementTransactionPort
{
    RuntimeTransactionRollbackState CaptureRuntimeTransactionRollbackState(
        RuntimeTransaction transaction
    );
    RuntimeCommitResult CommitRuntimeTransaction(
        RuntimeTransaction transaction,
        StringName reason
    );
    void RollbackRuntimeTransaction(
        RuntimeTransaction transaction,
        RuntimeTransactionRollbackState rollbackState
    );
    int PersistPartyState();
    int PersistWorldData();
    int PersistPlayerCoord();
    GDictionary BuildCommandOk(string message);
    GDictionary BuildCommandError(string message);
}

internal interface IGameRuntimeSettlementModalPort
{
    RuntimeModalKind GetActiveModalKind();
    void SetActiveModalKind(RuntimeModalKind modalKind);
    bool PresentPendingRewardIfReady();
    void SetActiveShopContext(GDictionary context);
    void SetActiveContractBoardContext(GDictionary context);
    void SetActiveNpcQuestOfferContext(NpcQuestOfferWindowData data);
    void SetActiveBountyBoardContext(BountyBoardWindowData data);
    void SetActiveForgeContext(GDictionary context);
    void SetActiveStagecoachContext(GDictionary context);
    void SetActiveShopContextPlain(IReadOnlyDictionary<string, object> context);
    void SetActiveContractBoardContextPlain(IReadOnlyDictionary<string, object> context);
    void SetActiveForgeContextPlain(IReadOnlyDictionary<string, object> context);
    void SetActiveStagecoachContextPlain(IReadOnlyDictionary<string, object> context);
    void ClearActiveShopContext();
    void ClearActiveContractBoardContext();
    void ClearActiveNpcQuestOfferContext();
    void ClearActiveBountyBoardContext();
    void ClearActiveForgeContext();
    void ClearActiveStagecoachContext();
    GodotProjectionLease<GDictionary> GetActiveShopContextLease();
    GodotProjectionLease<GDictionary> GetActiveContractBoardContextLease();
    GodotProjectionLease<GDictionary> GetActiveForgeContextLease();
    GodotProjectionLease<GDictionary> GetActiveStagecoachContextLease();
    IReadOnlyDictionary<string, object> GetActiveShopContextPlain();
    IReadOnlyDictionary<string, object> GetActiveContractBoardContextPlain();
    IReadOnlyDictionary<string, object> GetActiveForgeContextPlain();
    IReadOnlyDictionary<string, object> GetActiveStagecoachContextPlain();
    NpcQuestOfferWindowData GetActiveNpcQuestOfferData();
    BountyBoardWindowData GetActiveBountyBoardData();
}

internal interface IGameRuntimeSettlementWorldPort
{
    string GetPlayerFactionId();
    bool IsWorldCoordVisible(Vector2I coord, string factionId);
    IReadOnlyList<Vector2I> RevealWorldFogDiamond(
        Vector2I center,
        int revealRange,
        string factionId
    );
    void AdvanceWorldTimeBySteps(int deltaSteps);
    void RefreshWorldVisibility();
    int GetWorldStep();
    void SetPlayerCoord(Vector2I coord);
    void SetSelectedCoord(Vector2I coord);
    SettlementEntryRuntimeSnapshot CaptureSettlementEntrySnapshot();
    void SetSettlementEntryContext(Vector2I sourceCoord, Vector2I targetCoord);
    void ClearSettlementEntryContext(bool resetSelected);
    bool MarkSettlementVisited(string settlementId);
    bool IsSettlementVisited(string settlementId);
}

internal interface IGameRuntimeSettlementCommandPort
    : IGameRuntimeSettlementStatePort,
        IGameRuntimeSettlementContentPort,
        IGameRuntimeSettlementTransactionPort,
        IGameRuntimeSettlementModalPort,
        IGameRuntimeSettlementWorldPort { }

internal readonly struct SettlementEntryRuntimeSnapshot
{
    internal Vector2I SelectedCoord { get; }
    internal bool IsActive { get; }
    internal Vector2I SourceCoord { get; }
    internal Vector2I TargetCoord { get; }

    internal SettlementEntryRuntimeSnapshot(
        Vector2I selectedCoord,
        bool isActive,
        Vector2I sourceCoord,
        Vector2I targetCoord
    )
    {
        SelectedCoord = selectedCoord;
        IsActive = isActive;
        SourceCoord = sourceCoord;
        TargetCoord = targetCoord;
    }
}
