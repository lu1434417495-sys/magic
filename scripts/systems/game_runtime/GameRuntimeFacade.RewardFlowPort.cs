using System.Collections.Generic;
using Godot;

public sealed partial class GameRuntimeFacade : IGameRuntimeRewardFlowPort
{
    RuntimeModalKind IGameRuntimeRewardFlowPort.GetActiveModalKind() => _active_modal_kind;

    void IGameRuntimeRewardFlowPort.SetActiveModalKind(RuntimeModalKind modalKind) =>
        SetRuntimeActiveModalKind(modalKind);

    bool IGameRuntimeRewardFlowPort.IsBattleActive() => IsBattleActive();

    void IGameRuntimeRewardFlowPort.UpdateStatus(string message) => UpdateStatus(message);

    void IGameRuntimeRewardFlowPort.CloseDismissibleModal(RuntimeModalKind modalKind)
    {
        switch (modalKind)
        {
            case RuntimeModalKind.Settlement:
                CloseSettlementModal();
                break;
            case RuntimeModalKind.ContractBoard:
                CloseContractBoardModal();
                break;
            case RuntimeModalKind.BountyBoard:
                CloseBountyBoardModal();
                break;
            case RuntimeModalKind.NpcQuestOffer:
                CloseNpcQuestOfferModal();
                break;
            case RuntimeModalKind.Shop:
                CloseShopModal();
                break;
            case RuntimeModalKind.Forge:
                CloseForgeModal();
                break;
            case RuntimeModalKind.Stagecoach:
                CloseStagecoachModal();
                break;
            case RuntimeModalKind.Party:
                ClosePartyManagementModal();
                break;
            case RuntimeModalKind.Warehouse:
                ClosePartyWarehouseModal();
                break;
            case RuntimeModalKind.SubmapConfirm:
                CommandCancelSubmapEntryTyped();
                break;
        }
    }

    void IGameRuntimeRewardFlowPort.ClearActiveCharacterInfoContext() =>
        ClearActiveCharacterInfoContext();

    GameRuntimePromotionPromptContext IGameRuntimeRewardFlowPort.GetCurrentPromotionPrompt() =>
        !_pending_promotion_prompt.IsEmpty
            ? _pending_promotion_prompt
            : _pending_world_promotion_prompt;

    GameRuntimePromotionPromptContext
        IGameRuntimeRewardFlowPort.GetPendingBattlePromotionPrompt() =>
        _pending_promotion_prompt;

    GameRuntimePromotionPromptContext
        IGameRuntimeRewardFlowPort.GetPendingWorldPromotionPrompt() =>
        _pending_world_promotion_prompt;

    void IGameRuntimeRewardFlowPort.SetPendingWorldPromotionPrompt(
        GameRuntimePromotionPromptContext prompt
    ) => SetPendingWorldPromotionPromptState(prompt);

    void IGameRuntimeRewardFlowPort.ClearPendingBattlePromotionPrompt() =>
        ClearPendingPromotionPrompt();

    void IGameRuntimeRewardFlowPort.ClearPendingWorldPromotionPrompt() =>
        ClearPendingWorldPromotionPromptState();

    GameRuntimePromotionPromptContext IGameRuntimeRewardFlowPort.BuildPromotionPrompt(
        CharacterProgressionDelta delta,
        string selectionHint
    ) => _battle_session_facade.BuildPromotionPrompt(delta, selectionHint);

    BattleEventBatch IGameRuntimeRewardFlowPort.SubmitBattlePromotionChoice(
        StringName memberId,
        StringName professionId,
        PromotionSelectionData selection
    ) => SubmitBattlePromotionChoice(memberId, professionId, selection);

    void IGameRuntimeRewardFlowPort.ApplyBattleBatch(BattleEventBatch batch) =>
        ApplyBattleBatch(batch);

    CharacterProgressionDelta IGameRuntimeRewardFlowPort.PromoteProfession(
        StringName memberId,
        StringName professionId,
        PromotionSelectionData selection
    ) => PromoteProfession(memberId, professionId, selection);

    void IGameRuntimeRewardFlowPort.SyncPartyStateFromCharacterManagement() =>
        SyncPartyStateFromCharacterManagement();

    Error IGameRuntimeRewardFlowPort.PersistPartyState() => (Error)PersistPartyState();

    string IGameRuntimeRewardFlowPort.GetMemberDisplayName(StringName memberId) =>
        GetMemberDisplayName(memberId);

    PendingCharacterReward IGameRuntimeRewardFlowPort.GetActiveReward() => _active_reward;

    PendingCharacterReward IGameRuntimeRewardFlowPort.GetNextPendingReward() =>
        _party_state?.GetNextPendingCharacterReward();

    void IGameRuntimeRewardFlowPort.SetActiveReward(PendingCharacterReward reward) =>
        _active_reward = reward;

    void IGameRuntimeRewardFlowPort.ClearActiveReward() => _active_reward = null;

    CharacterProgressionDelta IGameRuntimeRewardFlowPort.ApplyPendingCharacterReward(
        PendingCharacterReward reward
    ) => ApplyPendingCharacterRewardToParty(reward);

    void IGameRuntimeRewardFlowPort.EnqueueCharacterRewards(
        IEnumerable<PendingCharacterReward> rewards
    ) => EnqueueCharacterRewardsTyped(rewards);
}
