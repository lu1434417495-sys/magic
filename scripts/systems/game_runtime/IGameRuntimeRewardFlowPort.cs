using System.Collections.Generic;
using Godot;

internal interface IGameRuntimeRewardFlowPort
{
    RuntimeModalKind GetActiveModalKind();
    void SetActiveModalKind(RuntimeModalKind modalKind);
    bool IsBattleActive();
    void UpdateStatus(string message);
    void CloseDismissibleModal(RuntimeModalKind modalKind);
    void ClearActiveCharacterInfoContext();

    GameRuntimePromotionPromptContext GetCurrentPromotionPrompt();
    GameRuntimePromotionPromptContext GetPendingBattlePromotionPrompt();
    GameRuntimePromotionPromptContext GetPendingWorldPromotionPrompt();
    void SetPendingWorldPromotionPrompt(GameRuntimePromotionPromptContext prompt);
    void ClearPendingBattlePromotionPrompt();
    void ClearPendingWorldPromotionPrompt();
    GameRuntimePromotionPromptContext BuildPromotionPrompt(
        CharacterProgressionDelta delta,
        string selectionHint
    );

    BattleEventBatch SubmitBattlePromotionChoice(
        StringName memberId,
        StringName professionId,
        PromotionSelectionData selection
    );
    void ApplyBattleBatch(BattleEventBatch batch);
    CharacterProgressionDelta PromoteProfession(
        StringName memberId,
        StringName professionId,
        PromotionSelectionData selection
    );
    void SyncPartyStateFromCharacterManagement();
    Error PersistPartyState();
    string GetMemberDisplayName(StringName memberId);

    PendingCharacterReward GetActiveReward();
    PendingCharacterReward GetNextPendingReward();
    void SetActiveReward(PendingCharacterReward reward);
    void ClearActiveReward();
    CharacterProgressionDelta ApplyPendingCharacterReward(PendingCharacterReward reward);
    void EnqueueCharacterRewards(IEnumerable<PendingCharacterReward> rewards);
}
