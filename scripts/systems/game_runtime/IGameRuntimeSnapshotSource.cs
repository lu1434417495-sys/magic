using System.Collections.Generic;
using Godot;

// Snapshot sources expose only detached plain facts or borrowed typed domain state.
// Godot collection projection belongs exclusively to the outer snapshot lease boundary.
public interface IGameRuntimeSnapshotSource
{
    bool IsBattleActive();
    string GetStatusText();
    string GetActiveModalId();
    RuntimeModalKind GetActiveModalKind();
    IReadOnlyDictionary<string, object> GetLogSnapshotPlain(int limit);
    WorldMapSettlementData GetSelectedSettlementData();
    WorldMapNpcData GetSelectedWorldNpcData();
    EncounterAnchorData GetSelectedEncounterAnchor();
    WorldMapEventData GetSelectedWorldEventData();
    IReadOnlyList<IReadOnlyDictionary<string, object>> GetNearbyEncounterEntriesSnapshotPlain(
        int limit
    );
    IReadOnlyList<IReadOnlyDictionary<string, object>> GetNearbyWorldEventEntriesSnapshotPlain(
        int limit
    );
    string GetActiveMapId();
    string GetActiveMapDisplayName();
    bool IsSubmapActive();
    int GetWorldStep();
    Vector2I GetPlayerCoord();
    bool IsPlayerVisibleOnWorldMap();
    Vector2I GetSelectedCoord();
    IReadOnlyDictionary<string, object> GetPendingSubmapPromptSnapshotPlain();
    string GetSubmapReturnHintText();
    IReadOnlyDictionary<string, object> GetGameOverContextSnapshotPlain();
    PartyState GetPartyState();
    StringName GetPartySelectedMemberId();
    int GetPendingRewardCount();
    IReadOnlyDictionary<string, object> GetMemberAchievementSummarySnapshotPlain(
        StringName member_id
    );
    AttributeSnapshot GetMemberAttributeSnapshot(StringName member_id);
    IReadOnlyList<IReadOnlyDictionary<string, object>> GetMemberEquippedEntriesSnapshotPlain(
        StringName member_id
    );
    string GetMemberDisplayName(StringName member_id);
    string GetResolvedSettlementId();
    IReadOnlyDictionary<string, object> GetSettlementHeadlessFactsPlain(
        string settlement_id
    );
    string GetSettlementFeedbackText();
    IReadOnlyDictionary<string, object> GetShopWindowDataSnapshotPlain();
    IReadOnlyDictionary<string, object> GetContractBoardWindowDataSnapshotPlain();
    IReadOnlyDictionary<string, object> GetNpcQuestOfferWindowDataSnapshotPlain();
    IReadOnlyDictionary<string, object> GetBountyBoardWindowDataSnapshotPlain();
    IReadOnlyDictionary<string, object> GetForgeWindowDataSnapshotPlain();
    IReadOnlyDictionary<string, object> GetStagecoachWindowDataSnapshotPlain();
    IReadOnlyDictionary<string, object> GetCharacterInfoContextSnapshotPlain();
    string GetActiveWarehouseEntryLabel();
    IReadOnlyDictionary<string, object> GetWarehouseWindowDataSnapshotPlain();
    ContingencySetupMutationResult GetLastContingencyCommandResultTyped();
    BattleState GetBattleState();
    BattleRuntimeModule GetBattleRuntime();
    Vector2I GetBattleSelectedCoord();
    StringName GetSelectedBattleSkillEntryId();
    StringName GetSelectedBattleSkillId();
    StringName GetSelectedBattleSkillVariantId();
    string GetSelectedBattleSkillName();
    string GetSelectedBattleSkillVariantName();
    IReadOnlyList<Vector2I> GetSelectedBattleSkillTargetCoordsSnapshotPlain();
    IReadOnlyList<StringName> GetSelectedBattleSkillTargetUnitIdsSnapshotPlain();
    int GetSelectedBattleSkillRequiredCoordCount();
    BattlePreview GetSelectedBattleSkillPreview();
    StringName GetActiveBattleEncounterId();
    string GetActiveBattleEncounterName();
    string GetBattleActiveUnitName();
    IReadOnlyDictionary<string, object> GetPendingBattleStartPromptSnapshotPlain();
    IReadOnlyDictionary<string, int> GetBattleTerrainCountsSnapshotTyped();
    PendingCharacterReward GetSnapshotReward();
    IReadOnlyDictionary<string, object> GetLastBattleLootSnapshotPlain();
    IReadOnlyDictionary<string, object> GetCurrentPromotionPromptSnapshotPlain();
    GameSession GetGameSession();
}
