using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public sealed partial class GameRuntimeFacade : IGameRuntimeSettlementCommandPort
{
    bool IGameRuntimeSettlementStatePort.IsBattleActive() => IsBattleActive();

    void IGameRuntimeSettlementStatePort.UpdateStatus(string message) =>
        UpdateStatusInternal(message);

    string IGameRuntimeSettlementStatePort.GetActiveSettlementId() => _active_settlement_id;

    void IGameRuntimeSettlementStatePort.SetActiveSettlementId(string settlementId) =>
        SetActiveSettlementId(settlementId);

    string IGameRuntimeSettlementStatePort.GetSettlementFeedbackText() =>
        _active_settlement_feedback_text;

    void IGameRuntimeSettlementStatePort.SetSettlementFeedbackText(string feedbackText) =>
        SetSettlementFeedbackText(feedbackText);

    WorldMapSettlementData IGameRuntimeSettlementStatePort.GetSelectedSettlementData() =>
        GetSelectedSettlementData();

    PartyState IGameRuntimeSettlementStatePort.GetPartyState() => _party_state;

    GodotProjectionLease<GDictionary>
        IGameRuntimeSettlementStatePort.GetSettlementRecordLease(string settlementId) =>
        GetSettlementRecordLease(settlementId);

    GodotProjectionLease<GArray>
        IGameRuntimeSettlementStatePort.GetAllSettlementRecordsLease() =>
        GetAllSettlementRecordsLease();

    IReadOnlyDictionary<string, object>
        IGameRuntimeSettlementStatePort.GetSettlementRecordSnapshotPlain(string settlementId)
    {
        if (string.IsNullOrEmpty(settlementId))
            return new Dictionary<string, object>(StringComparer.Ordinal);
        WorldRuntimeData worldData = GetActiveWorldRuntimeData();
        foreach (WorldMapSettlementRecordData settlement in worldData?.Settlements ?? Array.Empty<WorldMapSettlementRecordData>())
        {
            if (settlement != null && settlement.SettlementId == settlementId)
                return settlement.BuildSaveSnapshotPlain();
        }
        return new Dictionary<string, object>(StringComparer.Ordinal);
    }

    WorldMapSettlementStateData
        IGameRuntimeSettlementStatePort.GetSettlementStateData(string settlementId) =>
        GetSettlementStateData(settlementId);

    bool IGameRuntimeSettlementStatePort.SetActiveSettlementState(
        string settlementId,
        WorldMapSettlementStateData settlementState
    ) => SetActiveSettlementState(settlementId, settlementState);

    PartyWarehouseService IGameRuntimeSettlementStatePort.GetPartyWarehouseService() =>
        _party_warehouse_service;

    AttributeSnapshot IGameRuntimeSettlementStatePort.GetMemberAttributeSnapshot(
        StringName memberId
    ) => GetMemberAttributeSnapshot(memberId);

    string IGameRuntimeSettlementStatePort.GetMemberDisplayName(StringName memberId) =>
        GetMemberDisplayName(memberId);

    void IGameRuntimeSettlementStatePort.OpenPartyWarehouseWindow(string entryLabel) =>
        OpenPartyWarehouseWindow(entryLabel);

    void IGameRuntimeSettlementStatePort.EnqueuePendingCharacterRewardsTyped(
        IEnumerable<PendingCharacterReward> rewards
    ) => EnqueuePendingCharacterRewardsTyped(rewards);

    void IGameRuntimeSettlementStatePort.RecordMemberAchievementEvent(
        StringName memberId,
        StringName eventId,
        int value,
        StringName detailId
    ) => RecordMemberAchievementEvent(memberId, eventId, value, detailId);

    void IGameRuntimeSettlementStatePort.SyncPartyStateFromCharacterManagement() =>
        SyncPartyStateFromCharacterManagement();

    PendingCharacterReward IGameRuntimeSettlementStatePort.BuildPendingCharacterReward(
        StringName memberId,
        StringName rewardId,
        StringName sourceType,
        StringName sourceId,
        string sourceLabel,
        IReadOnlyList<PendingCharacterRewardEntry> entries,
        string summaryText
    ) =>
        _character_management?.BuildPendingCharacterReward(
            memberId,
            rewardId,
            sourceType,
            sourceId,
            sourceLabel,
            entries,
            summaryText
        );

    GDictionary IGameRuntimeSettlementStatePort.ResolveLowLuckSettlementEventRewards(
        GDictionary context
    ) => ResolveLowLuckSettlementEventRewards(context);

    QuestProgressApplyResultData
        IGameRuntimeSettlementStatePort.ApplyQuestProgressEventsToPartyTyped(
            IEnumerable<QuestProgressService.QuestProgressEventData> eventOptions,
            string sourceDomain
        ) => ApplyQuestProgressEventsToPartyTyped(eventOptions, sourceDomain);

    IReadOnlyList<StringName> IGameRuntimeSettlementStatePort.HandleMisfortuneForgeResult(
        StringName memberId,
        SettlementServiceResult result
    ) => HandleMisfortuneForgeResult(memberId, result);

    IReadOnlyDictionary<StringName, ItemDefinition>
        IGameRuntimeSettlementContentPort.GetItemDefinitions() =>
        _game_session?.GetItemDefsTyped()
        ?? new Dictionary<StringName, ItemDefinition>();

    IReadOnlyDictionary<StringName, TraitDefinition>
        IGameRuntimeSettlementContentPort.GetTraitDefinitions() =>
        _game_session?.GetTraitDefsTyped()
        ?? new Dictionary<StringName, TraitDefinition>();

    IReadOnlyDictionary<StringName, RecipeDefinition>
        IGameRuntimeSettlementContentPort.GetRecipeDefinitions() =>
        _game_session?.GetRecipeDefsTyped()
        ?? new Dictionary<StringName, RecipeDefinition>();

    IReadOnlyDictionary<StringName, QuestDefinition>
        IGameRuntimeSettlementContentPort.GetQuestDefinitions() =>
        _game_session?.GetQuestDefsTyped()
        ?? new Dictionary<StringName, QuestDefinition>();

    IReadOnlyDictionary<StringName, EnemyTemplateDefinition>
        IGameRuntimeSettlementContentPort.GetEnemyTemplateDefinitions() =>
        _game_session?.GetEnemyTemplateDefinitions()
        ?? new Dictionary<StringName, EnemyTemplateDefinition>();

    QuestDefinition IGameRuntimeSettlementContentPort.GetQuestDefinition(StringName questId) =>
        GetQuestDef(questId);

    string IGameRuntimeSettlementContentPort.GetItemDisplayName(StringName itemId) =>
        GetItemDisplayName(itemId);

    RuntimeCommandResult IGameRuntimeSettlementContentPort.CommandAcceptQuestTyped(
        StringName questId,
        bool allowRepeatable
    ) => CommandAcceptQuestTyped(questId, allowRepeatable);

    RuntimeCommandResult IGameRuntimeSettlementContentPort.CommandClaimQuestTyped(
        StringName questId
    ) => CommandClaimQuestTyped(questId);

    RuntimeCommandResult IGameRuntimeSettlementContentPort.CommandSubmitQuestItemTyped(
        StringName questId,
        StringName objectiveId
    ) => CommandSubmitQuestItemTyped(questId, objectiveId);

    RuntimeTransactionRollbackState
        IGameRuntimeSettlementTransactionPort.CaptureRuntimeTransactionRollbackState(
            RuntimeTransaction transaction
        ) => RuntimeTransactionRollbackState.Capture(this, transaction);

    RuntimeCommitResult IGameRuntimeSettlementTransactionPort.CommitRuntimeTransaction(
        RuntimeTransaction transaction,
        StringName reason
    ) => CommitRuntimeTransaction(transaction, reason);

    void IGameRuntimeSettlementTransactionPort.RollbackRuntimeTransaction(
        RuntimeTransaction transaction,
        RuntimeTransactionRollbackState rollbackState
    ) => transaction?.Rollback(this, rollbackState);

    int IGameRuntimeSettlementTransactionPort.PersistPartyState() => PersistPartyState();
    int IGameRuntimeSettlementTransactionPort.PersistWorldData() => PersistWorldData();
    int IGameRuntimeSettlementTransactionPort.PersistPlayerCoord() => PersistPlayerCoord();

    GDictionary IGameRuntimeSettlementTransactionPort.BuildCommandOk(string message) =>
        BuildCommandOk(message);

    GDictionary IGameRuntimeSettlementTransactionPort.BuildCommandError(string message) =>
        BuildCommandError(message);

    RuntimeModalKind IGameRuntimeSettlementModalPort.GetActiveModalKind() =>
        _active_modal_kind;

    void IGameRuntimeSettlementModalPort.SetActiveModalKind(RuntimeModalKind modalKind) =>
        SetRuntimeActiveModalKind(modalKind);

    bool IGameRuntimeSettlementModalPort.PresentPendingRewardIfReady() =>
        PresentPendingRewardIfReady();

    void IGameRuntimeSettlementModalPort.SetActiveShopContext(GDictionary context) =>
        SetActiveShopContext(context);

    void IGameRuntimeSettlementModalPort.SetActiveContractBoardContext(GDictionary context) =>
        SetActiveContractBoardContext(context);

    void IGameRuntimeSettlementModalPort.SetActiveNpcQuestOfferContext(
        NpcQuestOfferWindowData data
    ) => SetActiveNpcQuestOfferContext(data);

    void IGameRuntimeSettlementModalPort.SetActiveBountyBoardContext(
        BountyBoardWindowData data
    ) => SetActiveBountyBoardContext(data);

    void IGameRuntimeSettlementModalPort.SetActiveForgeContext(GDictionary context) =>
        SetActiveForgeContext(context);

    void IGameRuntimeSettlementModalPort.SetActiveStagecoachContext(GDictionary context) =>
        SetActiveStagecoachContext(context);

    void IGameRuntimeSettlementModalPort.SetActiveShopContextPlain(
        IReadOnlyDictionary<string, object> context
    ) => SetActiveShopContextPlain(context);

    void IGameRuntimeSettlementModalPort.SetActiveContractBoardContextPlain(
        IReadOnlyDictionary<string, object> context
    ) => SetActiveContractBoardContextPlain(context);

    void IGameRuntimeSettlementModalPort.SetActiveForgeContextPlain(
        IReadOnlyDictionary<string, object> context
    ) => SetActiveForgeContextPlain(context);

    void IGameRuntimeSettlementModalPort.SetActiveStagecoachContextPlain(
        IReadOnlyDictionary<string, object> context
    ) => SetActiveStagecoachContextPlain(context);

    void IGameRuntimeSettlementModalPort.ClearActiveShopContext() =>
        ClearActiveShopContext();

    void IGameRuntimeSettlementModalPort.ClearActiveContractBoardContext() =>
        ClearActiveContractBoardContext();

    void IGameRuntimeSettlementModalPort.ClearActiveNpcQuestOfferContext() =>
        ClearActiveNpcQuestOfferContext();

    void IGameRuntimeSettlementModalPort.ClearActiveBountyBoardContext() =>
        ClearActiveBountyBoardContext();

    void IGameRuntimeSettlementModalPort.ClearActiveForgeContext() =>
        ClearActiveForgeContext();

    void IGameRuntimeSettlementModalPort.ClearActiveStagecoachContext() =>
        ClearActiveStagecoachContext();

    GodotProjectionLease<GDictionary>
        IGameRuntimeSettlementModalPort.GetActiveShopContextLease() =>
        GetActiveShopContextLease();

    GodotProjectionLease<GDictionary>
        IGameRuntimeSettlementModalPort.GetActiveContractBoardContextLease() =>
        GetActiveContractBoardContextLease();

    GodotProjectionLease<GDictionary>
        IGameRuntimeSettlementModalPort.GetActiveForgeContextLease() =>
        GetActiveForgeContextLease();

    GodotProjectionLease<GDictionary>
        IGameRuntimeSettlementModalPort.GetActiveStagecoachContextLease() =>
        GetActiveStagecoachContextLease();

    IReadOnlyDictionary<string, object>
        IGameRuntimeSettlementModalPort.GetActiveShopContextPlain() =>
        GetActiveShopContextPlain();

    IReadOnlyDictionary<string, object>
        IGameRuntimeSettlementModalPort.GetActiveContractBoardContextPlain() =>
        GetActiveContractBoardContextPlain();

    IReadOnlyDictionary<string, object>
        IGameRuntimeSettlementModalPort.GetActiveForgeContextPlain() =>
        GetActiveForgeContextPlain();

    IReadOnlyDictionary<string, object>
        IGameRuntimeSettlementModalPort.GetActiveStagecoachContextPlain() =>
        GetActiveStagecoachContextPlain();

    NpcQuestOfferWindowData IGameRuntimeSettlementModalPort.GetActiveNpcQuestOfferData() =>
        GetActiveNpcQuestOfferData();

    BountyBoardWindowData IGameRuntimeSettlementModalPort.GetActiveBountyBoardData() =>
        GetActiveBountyBoardData();

    string IGameRuntimeSettlementWorldPort.GetPlayerFactionId() => _player_faction_id;

    bool IGameRuntimeSettlementWorldPort.IsWorldCoordVisible(
        Vector2I coord,
        string factionId
    ) => _fog_system != null && _fog_system.IsVisible(coord, factionId);

    IReadOnlyList<Vector2I> IGameRuntimeSettlementWorldPort.RevealWorldFogDiamond(
        Vector2I center,
        int revealRange,
        string factionId
    )
    {
        IReadOnlyList<Vector2I> revealed =
            _fog_system?.RevealDiamond(center, revealRange, factionId);
        return revealed ?? Array.Empty<Vector2I>();
    }

    void IGameRuntimeSettlementWorldPort.AdvanceWorldTimeBySteps(int deltaSteps) =>
        AdvanceWorldTimeBySteps(deltaSteps);

    void IGameRuntimeSettlementWorldPort.RefreshWorldVisibility() =>
        RefreshWorldVisibility();

    int IGameRuntimeSettlementWorldPort.GetWorldStep() => GetWorldStep();

    void IGameRuntimeSettlementWorldPort.SetPlayerCoord(Vector2I coord) =>
        SetPlayerCoord(coord);

    void IGameRuntimeSettlementWorldPort.SetSelectedCoord(Vector2I coord) =>
        SetSelectedCoord(coord);

    SettlementEntryRuntimeSnapshot
        IGameRuntimeSettlementWorldPort.CaptureSettlementEntrySnapshot() =>
        new(
            _selected_coord,
            _settlement_entry_active,
            _settlement_entry_source_coord,
            _settlement_entry_target_coord
        );

    void IGameRuntimeSettlementWorldPort.SetSettlementEntryContext(
        Vector2I sourceCoord,
        Vector2I targetCoord
    ) => SetSettlementEntryContext(sourceCoord, targetCoord);

    void IGameRuntimeSettlementWorldPort.ClearSettlementEntryContext(bool resetSelected) =>
        ClearSettlementEntryContext(resetSelected);

    bool IGameRuntimeSettlementWorldPort.MarkSettlementVisited(string settlementId) =>
        MarkSettlementVisited(settlementId);

    bool IGameRuntimeSettlementWorldPort.IsSettlementVisited(string settlementId) =>
        IsSettlementVisited(settlementId);
}
