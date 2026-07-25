using Godot;

public sealed partial class GameRuntimeFacade : IGameRuntimeQuestCommandPort
{
    bool IGameRuntimeQuestCommandPort.IsAvailable() => _character_management != null;

    QuestCommandDefData IGameRuntimeQuestCommandPort.GetQuestCommandDefData(
        StringName questId
    ) => QuestCommandDefData.FromQuestDefinition(GetQuestDef(questId));

    QuestCommandStateData IGameRuntimeQuestCommandPort.GetQuestCommandStateData(
        StringName questId
    )
    {
        PartyState partyState = _party_state;
        return new QuestCommandStateData(
            partyState?.HasActiveQuest(questId) ?? false,
            partyState?.HasClaimableQuest(questId) ?? false,
            partyState?.HasCompletedQuest(questId) ?? false,
            partyState?.HasFailedQuest(questId) ?? false
        );
    }

    int IGameRuntimeQuestCommandPort.GetWorldStep() => GetWorldStep();

    string IGameRuntimeQuestCommandPort.GetItemDisplayName(StringName itemId) =>
        GetItemDisplayName(itemId);

    bool IGameRuntimeQuestCommandPort.AcceptQuestAndSyncParty(
        StringName questId,
        bool allowReaccept
    )
    {
        CharacterManagementModule characterManagement = _character_management;
        if (characterManagement == null)
            return false;
        bool accepted = characterManagement.AcceptQuest(
            questId,
            GetWorldStep(),
            allowReaccept
        );
        if (accepted)
            SetPartyState(characterManagement.GetPartyState());
        return accepted;
    }

    QuestProgressApplyResultData IGameRuntimeQuestCommandPort.ApplyDirectQuestProgressAndSyncParty(
        StringName questId,
        StringName objectiveId,
        int progressDelta,
        QuestProgressCommandPayloadData progressPayload
    )
    {
        CharacterManagementModule characterManagement = _character_management;
        if (characterManagement == null)
            return new QuestProgressApplyResultData();
        QuestProgressApplyResultData summary =
            characterManagement.ApplyDirectQuestProgressTyped(
                questId,
                objectiveId,
                Mathf.Max(progressDelta, 0),
                progressPayload.WorldStep,
                progressPayload.HasTargetValue,
                progressPayload.TargetValue,
                progressPayload.BuildContextData()
            );
        SetPartyState(characterManagement.GetPartyState());
        return summary;
    }

    bool IGameRuntimeQuestCommandPort.CompleteQuestAndSyncParty(StringName questId)
    {
        CharacterManagementModule characterManagement = _character_management;
        if (characterManagement == null)
            return false;
        bool completed = characterManagement.CompleteQuest(questId, GetWorldStep());
        if (completed)
            SetPartyState(characterManagement.GetPartyState());
        return completed;
    }

    QuestSubmitItemResultData IGameRuntimeQuestCommandPort.SubmitItemObjectiveAndSyncParty(
        StringName questId,
        StringName objectiveId
    )
    {
        CharacterManagementModule characterManagement = _character_management;
        if (characterManagement == null)
            return QuestSubmitItemResultData.Failed("runtime_unavailable");
        QuestSubmitItemResultData result = characterManagement.SubmitItemObjectiveTyped(
            questId,
            objectiveId,
            GetWorldStep()
        );
        if (result.Ok)
            SetPartyState(characterManagement.GetPartyState());
        return result;
    }

    QuestClaimResultData IGameRuntimeQuestCommandPort.ClaimQuestRewardAndSyncParty(
        StringName questId
    )
    {
        CharacterManagementModule characterManagement = _character_management;
        if (characterManagement == null)
            return QuestClaimResultData.Failed("runtime_unavailable");
        QuestClaimResultData result = characterManagement.ClaimQuestRewardTyped(
            questId,
            GetWorldStep()
        );
        if (result.Ok)
            SetPartyState(characterManagement.GetPartyState());
        return result;
    }

    Error IGameRuntimeQuestCommandPort.PersistQuestPartyState() =>
        (Error)PersistPartyStateInternal();

    void IGameRuntimeQuestCommandPort.UpdateStatus(string message) =>
        UpdateStatusInternal(message);
}
