using System;
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

    RuntimeTransactionRollbackState
        IGameRuntimeQuestCommandPort.CaptureQuestAcceptRollbackState(
            RuntimeTransaction transaction
        ) => RuntimeTransactionRollbackState.Capture(this, transaction);

    QuestAcceptEncounterSpawnResult
        IGameRuntimeQuestCommandPort.TryAddQuestAcceptEncounter(
            StringName questId,
            StringName encounterProfileId,
            string encounterDisplayName,
            int encounterGrowthStage
        )
    {
        if (
            questId == ""
            || encounterProfileId == ""
            || string.IsNullOrWhiteSpace(encounterDisplayName)
            || encounterGrowthStage < 0
        )
        {
            return QuestAcceptEncounterSpawnResult.Failure("任务遭遇绑定不完整。");
        }
        if (!_battle_encounter_definitions.ContainsKey(encounterProfileId))
        {
            return QuestAcceptEncounterSpawnResult.Failure(
                $"未找到任务遭遇 {encounterProfileId}。"
            );
        }

        StringName anchorId = QuestAcceptEncounterPlacement.BuildStableAnchorId(questId);
        EncounterAnchorData existingAnchor =
            _world_map_data_context.GetEncounterAnchorById(anchorId);
        if (existingAnchor != null)
        {
            if (existingAnchor.encounter_profile_id != encounterProfileId)
            {
                return QuestAcceptEncounterSpawnResult.Failure(
                    $"任务遭遇锚点 {anchorId} 已被其它遭遇占用。"
                );
            }
            if (!existingAnchor.is_cleared)
            {
                return existingAnchor.growth_stage == encounterGrowthStage
                    ? QuestAcceptEncounterSpawnResult.ExistingAnchor(anchorId)
                    : QuestAcceptEncounterSpawnResult.Failure(
                        $"任务遭遇锚点 {anchorId} 的 growth stage 与当前任务配置不一致。"
                    );
            }

            // Reaccepting/restarting a quest must not reuse an already-cleared
            // marker: that would leave the newly active objective with no
            // battle to enter. The surrounding quest-accept transaction owns
            // rollback, so a later placement/add/commit failure restores this
            // cleared anchor from the captured world snapshot.
            _world_map_data_context.RemoveEncounterAnchorById(anchorId);
        }

        Vector2I center =
            _settlement_entry_active
            && _grid_system.IsCellInsideWorld(_settlement_entry_target_coord)
                ? _settlement_entry_target_coord
                : _player_coord;
        if (
            !QuestAcceptEncounterPlacement.TryFindAvailableCoord(
                _grid_system,
                center,
                candidate =>
                    candidate != _player_coord
                    && _grid_system.GetOccupantRoot(candidate).Length == 0
                    && _world_map_data_context.IsEncounterPlacementCoordAvailable(candidate),
                out Vector2I encounterCoord
            )
        )
        {
            return QuestAcceptEncounterSpawnResult.Failure(
                "发布者附近没有可放置任务遭遇的空格。"
            );
        }

        var encounterAnchor = new EncounterAnchorData
        {
            entity_id = anchorId,
            display_name = encounterDisplayName.Trim(),
            world_coord = encounterCoord,
            faction_id = "hostile",
            region_tag = "",
            vision_range = 1,
            is_cleared = false,
            encounter_kind = EncounterAnchorData.ToStringName(EncounterAnchorKind.Single),
            encounter_profile_id = encounterProfileId,
            growth_stage = encounterGrowthStage,
            suppressed_until_step = 0,
        };
        return _world_map_data_context.TryAddEncounterAnchor(encounterAnchor)
            ? QuestAcceptEncounterSpawnResult.AddedAnchor(anchorId)
            : QuestAcceptEncounterSpawnResult.Failure("任务遭遇锚点添加失败。");
    }

    void IGameRuntimeQuestCommandPort.RemoveQuestAcceptEncounter(
        StringName encounterAnchorId
    )
    {
        if (encounterAnchorId != "")
            _world_map_data_context.RemoveEncounterAnchorById(encounterAnchorId);
    }

    RuntimeCommitResult IGameRuntimeQuestCommandPort.CommitQuestAcceptTransaction(
        RuntimeTransaction transaction
    ) => CommitRuntimeTransaction(transaction, "quest_accept");

    void IGameRuntimeQuestCommandPort.RollbackQuestAcceptTransaction(
        RuntimeTransaction transaction,
        RuntimeTransactionRollbackState rollbackState
    ) => transaction?.Rollback(this, rollbackState);

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

internal static class QuestAcceptEncounterPlacement
{
    private const int MaxPlacementRadius = 8;

    internal static StringName BuildStableAnchorId(StringName questId) =>
        questId == "" ? new StringName("") : new StringName($"quest_{questId}_encounter");

    internal static bool TryFindAvailableCoord(
        WorldMapGridSystem gridSystem,
        Vector2I center,
        Func<Vector2I, bool> isAvailable,
        out Vector2I result
    )
    {
        result = new Vector2I(-1, -1);
        if (gridSystem == null || isAvailable == null)
            return false;

        for (int radius = 1; radius <= MaxPlacementRadius; radius++)
        {
            int minX = center.X - radius;
            int maxX = center.X + radius;
            int minY = center.Y - radius;
            int maxY = center.Y + radius;
            if (
                TryCandidate(
                    gridSystem,
                    isAvailable,
                    new Vector2I(center.X, maxY),
                    out result
                )
            )
                return true;
            for (int offset = 1; offset <= radius; offset++)
            {
                if (
                    TryCandidate(
                        gridSystem,
                        isAvailable,
                        new Vector2I(center.X - offset, maxY),
                        out result
                    )
                )
                    return true;
                if (
                    TryCandidate(
                        gridSystem,
                        isAvailable,
                        new Vector2I(center.X + offset, maxY),
                        out result
                    )
                )
                    return true;
            }
            if (
                TryCandidate(
                    gridSystem,
                    isAvailable,
                    new Vector2I(center.X, minY),
                    out result
                )
            )
                return true;
            for (int offset = 1; offset <= radius; offset++)
            {
                if (
                    TryCandidate(
                        gridSystem,
                        isAvailable,
                        new Vector2I(center.X - offset, minY),
                        out result
                    )
                )
                    return true;
                if (
                    TryCandidate(
                        gridSystem,
                        isAvailable,
                        new Vector2I(center.X + offset, minY),
                        out result
                    )
                )
                    return true;
            }
            for (int y = minY + 1; y < maxY; y++)
            {
                if (TryCandidate(gridSystem, isAvailable, new Vector2I(minX, y), out result))
                    return true;
                if (
                    maxX != minX
                    && TryCandidate(
                        gridSystem,
                        isAvailable,
                        new Vector2I(maxX, y),
                        out result
                    )
                )
                    return true;
            }
        }
        return false;
    }

    private static bool TryCandidate(
        WorldMapGridSystem gridSystem,
        Func<Vector2I, bool> isAvailable,
        Vector2I candidate,
        out Vector2I result
    )
    {
        if (
            gridSystem.IsCellInsideWorld(candidate)
            && gridSystem.IsCellWalkable(candidate)
            && isAvailable(candidate)
        )
        {
            result = candidate;
            return true;
        }
        result = new Vector2I(-1, -1);
        return false;
    }
}
