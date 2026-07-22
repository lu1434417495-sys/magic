using System;
using System.Collections.Generic;
using Godot;

public sealed partial class GameRuntimeFacade : IGameRuntimeBattleWritebackPort
{
    void IGameRuntimeBattleWritebackPort.ApplyBattleLocalPartyState(
        PartyState candidateParty
    )
    {
        SetPartyState(candidateParty);
        SyncRuntimePartyServicesAfterBattleLocalWriteback();
    }

    void IGameRuntimeBattleWritebackPort.ReportBattleLocalWritebackInvariantFailure(
        string statusMessage,
        string contextJson
    )
    {
        UpdateStatus(statusMessage);
        _log_runtime_event(
            GameLogLevel.Error,
            "battle",
            "battle.local_writeback_inoption_failed",
            GetStatusText(),
            contextJson
        );
    }

    private void SyncRuntimePartyServicesAfterBattleLocalWriteback()
    {
        IReadOnlyDictionary<StringName, ItemDefinition> typedItemDefs =
            GetBattleWritebackItemDefsTyped();
        PartyState partyState = GetPartyState();

        CharacterManagementModule characterManagement = GetCharacterManagement();
        if (characterManagement != null)
            characterManagement.SetPartyState(partyState);

        PartyWarehouseService partyWarehouseService = GetPartyWarehouseService();
        if (partyWarehouseService != null)
            SetupPartyWarehouseService(
                partyWarehouseService,
                partyState,
                typedItemDefs
            );

        PartyItemUseService partyItemUseService = GetPartyItemUseService();
        if (partyItemUseService != null)
        {
            IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions =
                new Dictionary<StringName, SkillDefinition>();
            GameSession gameSession = GetGameSession();
            if (gameSession != null)
                skillDefinitions =
                    gameSession.GetContentCatalogTyped().GetSkillDefinitionsTyped();
            partyItemUseService.Setup(
                partyState,
                typedItemDefs,
                skillDefinitions,
                partyWarehouseService,
                characterManagement
            );
        }

        PartyEquipmentService partyEquipmentService = GetPartyEquipmentService();
        if (partyEquipmentService != null)
        {
            Func<StringName> allocator = GetEquipmentInstanceIdAllocator();
            partyEquipmentService.Setup(
                partyState,
                typedItemDefs,
                partyWarehouseService,
                allocator,
                characterManagement == null
                    ? null
                    : new Func<StringName, EquipmentState, AttributeSnapshot>(
                        characterManagement.GetMemberAttributeSnapshotForEquipmentView
                    )
            );
        }
    }

    private IReadOnlyDictionary<StringName, ItemDefinition> GetBattleWritebackItemDefsTyped()
    {
        GameSession gameSession = GetGameSession();
        if (gameSession != null)
            return gameSession.GetItemDefsTyped();
        return new Dictionary<StringName, ItemDefinition>();
    }
}
