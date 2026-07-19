using System;
using Godot;
using Godot.Collections;
using RuntimeCommandResult = GameRuntimeFacade.RuntimeCommandResult;

internal sealed class WorldMapRuntimeProxy
{
    private static readonly string RuntimeUnavailableMessage = "运行时尚未初始化。";

    private GameRuntimeFacade _runtime;
    private WorldMapSystem _renderTarget;

    public void Setup(GameRuntimeFacade runtime, WorldMapSystem renderTarget = null)
    {
        _runtime = runtime;
        _renderTarget = renderTarget;
    }

    public void Dispose()
    {
        _runtime = null;
        _renderTarget = null;
    }

    public string GetStatusText()
    {
        return _runtime?.GetStatusText() ?? "";
    }

    internal WorldRuntimeViewModel GetWorldRuntimeViewModel(int nearbyLimit = 8)
    {
        return _runtime?.GetWorldRuntimeViewModel(nearbyLimit)
            ?? WorldRuntimeViewModel.Empty;
    }

    public string GetActiveModalId()
    {
        return _runtime?.GetActiveModalId() ?? "";
    }

    internal GodotProjectionLease<Dictionary> GetGameOverContextLease()
    {
        return _runtime?.GetGameOverContextLease()
            ?? RuntimePlainPayload.ProjectDictionaryLease(
                new System.Collections.Generic.Dictionary<string, object>(System.StringComparer.Ordinal),
                "WorldMapRuntimeProxy.game_over_context",
                LifetimeDomain.Request,
                "WorldMapRuntimeProxy.game_over_context"
            );
    }

    public string GetActiveSettlementId()
    {
        return _runtime?.GetActiveSettlementId() ?? "";
    }

    public string GetActiveMapId()
    {
        return _runtime?.GetActiveMapId() ?? "";
    }

    public string GetActiveMapDisplayName()
    {
        return _runtime?.GetActiveMapDisplayName() ?? "";
    }

    public string GetSubmapReturnHintText()
    {
        return _runtime?.GetSubmapReturnHintText() ?? "";
    }

    public Dictionary GetPendingSubmapPrompt()
    {
        return _runtime?.GetPendingSubmapPrompt() ?? new Dictionary();
    }

    internal GodotProjectionLease<Dictionary> GetPendingBattleStartPromptLease()
    {
        return _runtime?.GetPendingBattleStartPromptLease()
            ?? RuntimePlainPayload.ProjectDictionaryLease(
                new System.Collections.Generic.Dictionary<string, object>(System.StringComparer.Ordinal),
                "WorldMapRuntimeProxy.pending_battle_start_prompt",
                LifetimeDomain.Request,
                "WorldMapRuntimeProxy.pending_battle_start_prompt"
            );
    }

    public Dictionary GetPendingResourceHarvestPrompt()
    {
        return _runtime?.GetPendingResourceHarvestPrompt() ?? new Dictionary();
    }

    public System.Collections.Generic.IReadOnlyDictionary<string, object> GetLogSnapshotPlain(
        int limit = 80
    )
    {
        return _runtime?.GetLogSnapshotPlain(limit)
            ?? new System.Collections.Generic.Dictionary<string, object>(
                StringComparer.Ordinal
            );
    }

    internal System.Collections.Generic.IReadOnlyDictionary<string, object> BuildHeadlessSnapshotPlain()
    {
        return _runtime?.BuildHeadlessSnapshotPlain()
            ?? new System.Collections.Generic.Dictionary<string, object>(
                StringComparer.Ordinal
            );
    }

    internal GodotProjectionLease<Dictionary> BuildHeadlessSnapshotLease()
    {
        return _runtime != null
            ? _runtime.BuildHeadlessSnapshotLease()
            : RuntimePlainPayload.ProjectDictionaryLease(
                new System.Collections.Generic.Dictionary<string, object>(
                    StringComparer.Ordinal
                ),
                "world-map-runtime-proxy-headless-snapshot",
                LifetimeDomain.Request,
                "WorldMapRuntimeProxy.empty"
            );
    }

    public string BuildTextSnapshot()
    {
        return _runtime?.BuildTextSnapshot() ?? "";
    }

    public bool Advance(float delta)
    {
        return _runtime?.advance(delta) ?? false;
    }

    public WorldMapGridSystem GetGridSystem()
    {
        return _runtime?.GetGridSystem();
    }

    internal WorldMapFogSystem GetFogSystem()
    {
        return _runtime?.GetFogSystem();
    }

    public bool IsWorldCoordVisible(Vector2I coord, string factionId = "")
    {
        return _runtime?.IsWorldCoordVisible(coord, factionId) ?? false;
    }

    internal WorldRuntimeData GetWorldRuntimeData() =>
        _runtime?.GetActiveWorldRuntimeData() ?? WorldRuntimeData.Empty();

    public Vector2I GetPlayerCoord()
    {
        return _runtime?.GetPlayerCoord() ?? Vector2I.Zero;
    }

    public bool IsPlayerVisibleOnWorldMap()
    {
        return _runtime?.IsPlayerVisibleOnWorldMap() ?? true;
    }

    public Vector2I GetSelectedCoord()
    {
        return _runtime?.GetSelectedCoord() ?? Vector2I.Zero;
    }

    public string GetPlayerFactionId()
    {
        return _runtime?.GetPlayerFactionId() ?? "player";
    }

    public BattleState GetBattleState()
    {
        return _runtime?.GetBattleState();
    }

    public Vector2I GetBattleSelectedCoord()
    {
        return _runtime?.GetBattleSelectedCoord() ?? new Vector2I(-1, -1);
    }

    public string GetLastAdvanceBattleRefreshMode()
    {
        return _runtime?.GetLastAdvanceBattleRefreshMode() ?? "";
    }

    internal BattlePresentationDelta GetLastAdvanceBattlePresentationDelta()
    {
        return _runtime?.GetLastAdvanceBattlePresentationDelta() ?? BattlePresentationDelta.None;
    }

    internal BattlePresentationDelta GetLastCommandBattlePresentationDelta()
    {
        return _runtime?.GetLastCommandBattlePresentationDelta() ?? BattlePresentationDelta.None;
    }

    public StringName GetSelectedBattleSkillId()
    {
        return _runtime?.GetSelectedBattleSkillId() ?? new StringName("");
    }

    public StringName GetSelectedBattleSkillEntryId()
    {
        return _runtime?.GetSelectedBattleSkillEntryId() ?? new StringName("");
    }

    public string GetSelectedBattleSkillName()
    {
        return _runtime?.GetSelectedBattleSkillName() ?? "";
    }

    public string GetSelectedBattleSkillVariantName()
    {
        return _runtime?.GetSelectedBattleSkillVariantName() ?? "";
    }

    public StringName GetSelectedBattleSkillVariantId()
    {
        return _runtime?.GetSelectedBattleSkillVariantId() ?? new StringName("");
    }

    public System.Collections.Generic.IReadOnlyList<Vector2I> GetSelectedBattleSkillTargetCoords()
    {
        return _runtime?.GetSelectedBattleSkillTargetCoords() ?? System.Array.Empty<Vector2I>();
    }

    public System.Collections.Generic.IReadOnlyList<StringName> GetSelectedBattleSkillTargetUnitIds()
    {
        return _runtime?.GetSelectedBattleSkillTargetUnitIds() ?? System.Array.Empty<StringName>();
    }

    public System.Collections.Generic.IReadOnlyList<Vector2I> GetBattleOverlayTargetCoords()
    {
        return _runtime?.GetBattleOverlayTargetCoords() ?? System.Array.Empty<Vector2I>();
    }

    public int GetSelectedBattleSkillRequiredCoordCount()
    {
        return _runtime?.GetSelectedBattleSkillRequiredCoordCount() ?? 0;
    }

    public BattlePreview GetSelectedBattleSkillPreview()
    {
        return _runtime?.GetSelectedBattleSkillPreview();
    }

    public BattlePreview PreviewSelectedBattleSkillAtCoord(Vector2I coord)
    {
        return _runtime?.PreviewSelectedBattleSkillAtCoord(coord);
    }

    public string GetActiveBattleEncounterName()
    {
        return _runtime?.GetActiveBattleEncounterName() ?? "";
    }

    public Dictionary GetSettlementWindowData(string settlementId = "")
    {
        return _runtime?.GetSettlementWindowData(settlementId) ?? new Dictionary();
    }

    public string GetSettlementFeedbackText()
    {
        return _runtime?.GetSettlementFeedbackText() ?? "";
    }

    internal GodotProjectionLease<Dictionary> GetShopWindowDataLease() =>
        _runtime?.GetShopWindowDataLease()
        ?? EmptyWindowDataLease("shop");

    internal GodotProjectionLease<Dictionary> GetContractBoardWindowDataLease() =>
        _runtime?.GetContractBoardWindowDataLease()
        ?? EmptyWindowDataLease("contract-board");

    internal NpcQuestOfferWindowData GetNpcQuestOfferWindowDataTyped()
    {
        return _runtime?.GetActiveNpcQuestOfferData();
    }

    internal BountyBoardWindowData GetBountyBoardWindowDataTyped()
    {
        return _runtime?.GetActiveBountyBoardData();
    }

    internal GodotProjectionLease<Dictionary> GetForgeWindowDataLease() =>
        _runtime?.GetForgeWindowDataLease()
        ?? EmptyWindowDataLease("forge");

    internal GodotProjectionLease<Dictionary> GetStagecoachWindowDataLease() =>
        _runtime?.GetStagecoachWindowDataLease()
        ?? EmptyWindowDataLease("stagecoach");

    private static GodotProjectionLease<Dictionary> EmptyWindowDataLease(string windowId) =>
        RuntimePlainPayload.ProjectDictionaryLease(
            new System.Collections.Generic.Dictionary<string, object>(System.StringComparer.Ordinal),
            $"world-map-proxy-{windowId}",
            LifetimeDomain.Request,
            $"WorldMapRuntimeProxy.{windowId}"
        );

    internal GodotProjectionLease<Dictionary> GetCharacterInfoContextLease()
    {
        return _runtime?.GetCharacterInfoContextLease()
            ?? RuntimePlainPayload.ProjectDictionaryLease(
                new System.Collections.Generic.Dictionary<string, object>(System.StringComparer.Ordinal),
                "WorldMapRuntimeProxy.character_info_context",
                LifetimeDomain.Request,
                "WorldMapRuntimeProxy.character_info_context"
            );
    }

    public PartyState GetPartyState()
    {
        return _runtime?.GetPartyState();
    }

    public CharacterManagementModule GetCharacterManagement()
    {
        return _runtime?.GetCharacterManagement();
    }

    public StringName GetPartySelectedMemberId()
    {
        return _runtime?.GetPartySelectedMemberId() ?? new StringName("");
    }

    public Dictionary GetWarehouseWindowData()
    {
        return _runtime?.GetWarehouseWindowData() ?? new Dictionary();
    }

    internal System.Collections.Generic.IReadOnlyDictionary<string, object>
        GetCurrentPromotionPromptSnapshotPlain()
    {
        return _runtime?.GetCurrentPromotionPromptSnapshotPlain()
            ?? new System.Collections.Generic.Dictionary<string, object>(
                System.StringComparer.Ordinal
            );
    }

    public PendingCharacterReward GetActiveReward()
    {
        return _runtime?.GetActiveReward();
    }

    public int GetPendingRewardCount()
    {
        return _runtime?.GetPendingRewardCount() ?? 0;
    }

    public bool IsBattleActive()
    {
        return _runtime?.IsBattleActive() ?? false;
    }

    public bool IsModalWindowOpen()
    {
        return _runtime?.IsModalWindowOpen() ?? false;
    }

    public bool IsSubmapActive()
    {
        return _runtime?.IsSubmapActive() ?? false;
    }

    internal RuntimeCommandResult CommandWorldMove(Vector2I direction, int count = 1)
    {
        return RunRuntimeCommand(() => _runtime.CommandWorldMoveTyped(direction, count));
    }

    internal RuntimeCommandResult CommandWorldSelect(Vector2I coord)
    {
        return RunRuntimeCommand(() => _runtime.CommandWorldSelectTyped(coord));
    }

    internal RuntimeCommandResult CommandOpenSettlement(Vector2I coord = default)
    {
        return RunRuntimeCommand(() => _runtime.CommandOpenSettlementTyped(coord));
    }

    internal RuntimeCommandResult CommandWorldInspect(Vector2I coord)
    {
        return RunRuntimeCommand(() => _runtime.CommandWorldInspectTyped(coord));
    }

    internal RuntimeCommandResult CommandOpenParty()
    {
        return RunRuntimeCommand(() => _runtime.CommandOpenPartyTyped());
    }

    internal RuntimeCommandResult CommandAcceptQuest(StringName questId, bool allowReaccept = false)
    {
        return RunRuntimeCommand(() => _runtime.CommandAcceptQuestTyped(questId, allowReaccept));
    }

    internal RuntimeCommandResult CommandProgressQuest(
        StringName questId,
        StringName objectiveId,
        int progressDelta = 1,
        Dictionary payload = null
    ) =>
        CommandProgressQuest(
            questId,
            objectiveId,
            progressDelta,
            QuestProgressCommandPayloadData.FromDictionary(payload, _runtime?.GetWorldStep() ?? -1)
        );

    internal RuntimeCommandResult CommandProgressQuest(
        StringName questId,
        StringName objectiveId,
        int progressDelta,
        QuestProgressCommandPayloadData payload
    )
    {
        return RunRuntimeCommand(
            () =>
                _runtime.CommandProgressQuestTyped(
                    questId,
                    objectiveId,
                    progressDelta,
                    payload
                )
        );
    }

    internal RuntimeCommandResult CommandCompleteQuest(StringName questId)
    {
        return RunRuntimeCommand(() => _runtime.CommandCompleteQuestTyped(questId));
    }

    internal RuntimeCommandResult CommandSelectPartyMember(StringName memberId)
    {
        return RunRuntimeCommand(() => _runtime.CommandSelectPartyMemberTyped(memberId));
    }

    internal RuntimeCommandResult CommandSetPartyLeader(StringName memberId)
    {
        return RunRuntimeCommand(() => _runtime.CommandSetPartyLeaderTyped(memberId));
    }

    internal RuntimeCommandResult CommandMoveMemberToActive(StringName memberId)
    {
        return RunRuntimeCommand(() => _runtime.CommandMoveMemberToActiveTyped(memberId));
    }

    internal RuntimeCommandResult CommandMoveMemberToReserve(StringName memberId)
    {
        return RunRuntimeCommand(() => _runtime.CommandMoveMemberToReserveTyped(memberId));
    }

    internal RuntimeCommandResult CommandOpenPartyWarehouse()
    {
        return RunRuntimeCommand(() => _runtime.CommandOpenPartyWarehouseTyped());
    }

    internal RuntimeCommandResult CommandWarehouseDiscardOne(
        StringName itemId,
        StringName instanceId = default
    )
    {
        return RunRuntimeCommand(() => _runtime.CommandWarehouseDiscardOneTyped(itemId, instanceId));
    }

    internal RuntimeCommandResult CommandWarehouseDiscardAll(StringName itemId)
    {
        return RunRuntimeCommand(() => _runtime.CommandWarehouseDiscardAllTyped(itemId));
    }

    internal RuntimeCommandResult CommandWarehouseUseItem(
        StringName itemId,
        StringName memberId = default,
        PartyItemUseService.PartyItemUseOptions options = null
    )
    {
        return RunRuntimeCommand(
            () => _runtime.CommandWarehouseUseItemTyped(itemId, memberId, options)
        );
    }

    internal RuntimeCommandResult CommandExecuteSettlementAction(
        SettlementActionRequest request
    )
    {
        return RunRuntimeCommand(
            () => _runtime.CommandExecuteSettlementActionTyped(request)
        );
    }

    internal RuntimeCommandResult CommandExecuteSettlementAction(
        string actionId,
        Dictionary payload = null
    )
    {
        return RunRuntimeCommand(
            () => _runtime.CommandExecuteSettlementActionTyped(actionId, payload ?? new Dictionary())
        );
    }

    internal RuntimeCommandResult CommandShopBuy(StringName itemId, int quantity = 1)
    {
        return RunRuntimeCommand(() => _runtime.CommandShopBuyTyped(itemId, quantity));
    }

    internal RuntimeCommandResult CommandShopSell(
        StringName itemId,
        int quantity = 1,
        StringName instanceId = default
    )
    {
        return RunRuntimeCommand(() => _runtime.CommandShopSellTyped(itemId, quantity, instanceId));
    }

    internal RuntimeCommandResult CommandStagecoachTravel(string settlementId)
    {
        return RunRuntimeCommand(() => _runtime.CommandStagecoachTravelTyped(settlementId));
    }

    internal RuntimeCommandResult CommandBattleTick(int tickCount)
    {
        return RunRuntimeCommand(() => _runtime.CommandBattleTickTyped(tickCount));
    }

    internal RuntimeCommandResult CommandBattleSelectSkill(int slotIndex)
    {
        return RunRuntimeCommand(() => _runtime.CommandBattleSelectSkillTyped(slotIndex));
    }

    internal RuntimeCommandResult CommandBattleCycleVariant(int step)
    {
        return RunRuntimeCommand(() => _runtime.CommandBattleCycleVariantTyped(step));
    }

    internal RuntimeCommandResult CommandBattleClearSkill()
    {
        return RunRuntimeCommand(() => _runtime.CommandBattleClearSkillTyped());
    }

    internal RuntimeCommandResult CommandBattleMoveTo(Vector2I targetCoord)
    {
        return RunRuntimeCommand(() => _runtime.CommandBattleMoveToTyped(targetCoord));
    }

    internal RuntimeCommandResult CommandBattleMoveDirection(Vector2I direction)
    {
        return RunRuntimeCommand(() => _runtime.CommandBattleMoveDirectionTyped(direction));
    }

    internal RuntimeCommandResult CommandBattleWaitOrResolve()
    {
        return RunRuntimeCommand(() => _runtime.CommandBattleWaitOrResolveTyped());
    }

    internal RuntimeCommandResult CommandBattleInspect(Vector2I coord)
    {
        return RunRuntimeCommand(() => _runtime.CommandBattleInspectTyped(coord));
    }

    public BattlePreview PreviewBattleCommand(BattleCommand command)
    {
        return _runtime != null ? _runtime.PreviewBattleCommand(command) : null;
    }

    internal RuntimeCommandResult IssueBattleCommand(BattleCommand command)
    {
        if (_runtime == null)
            return RuntimeUnavailableError();
        if (command == null)
            return RuntimeCommandResult.Failure(
                "战斗命令无效。",
                GameRuntimeFacade.RuntimeCommandCode.InvalidArgument
            );
        _runtime.ResetLastCommandBattlePresentationDelta();
        var refreshMode = _runtime.IssueBattleCommand(command);
        if (refreshMode == BattleRefreshMode.None)
            refreshMode = BattleRefreshMode.Full;
        var message = _runtime.GetStatusText();
        RuntimeCommandResult result = RuntimeCommandResult.Success(
            message,
            GameRuntimeFacade.RuntimeCommandCode.Ok,
            refreshMode
        );
        RenderRuntimeCommandResult(result);
        return result;
    }

    internal RuntimeCommandResult CommandConfirmPendingReward()
    {
        return RunRuntimeCommand(() => _runtime.CommandConfirmPendingRewardTyped());
    }

    internal RuntimeCommandResult CommandChoosePromotion(StringName professionId)
    {
        return RunRuntimeCommand(() => _runtime.CommandChoosePromotionTyped(professionId));
    }

    internal RuntimeCommandResult CommandConfirmSubmapEntry()
    {
        return RunRuntimeCommand(() => _runtime.CommandConfirmSubmapEntryTyped());
    }

    internal RuntimeCommandResult CommandConfirmBattleStart()
    {
        return RunRuntimeCommand(() => _runtime.CommandConfirmBattleStartTyped());
    }

    internal RuntimeCommandResult CommandCancelSubmapEntry()
    {
        return RunRuntimeCommand(() => _runtime.CommandCancelSubmapEntryTyped());
    }

    internal RuntimeCommandResult CommandConfirmResourceHarvest()
    {
        return RunRuntimeCommand(() => _runtime.CommandConfirmResourceHarvestTyped());
    }

    internal RuntimeCommandResult CommandCancelResourceHarvest()
    {
        return RunRuntimeCommand(() => _runtime.CommandCancelResourceHarvestTyped());
    }

    internal RuntimeCommandResult CommandReturnFromSubmap()
    {
        return RunRuntimeCommand(() => _runtime.CommandReturnFromSubmapTyped());
    }

    internal RuntimeCommandResult CommandCloseActiveModal()
    {
        return RunRuntimeCommand(() => _runtime.CommandCloseActiveModalTyped());
    }

    internal RuntimeCommandResult ApplyPartyRoster(
        Array<StringName> activeMemberIds,
        Array<StringName> reserveMemberIds
    )
    {
        return RunRuntimeCommand(
            () => _runtime.CommandApplyPartyRosterTyped(activeMemberIds, reserveMemberIds)
        );
    }

    internal RuntimeCommandResult SubmitPromotionChoice(
        StringName memberId,
        StringName professionId,
        PromotionSelectionData selection
    )
    {
        return RunRuntimeCommand(
            () => _runtime.CommandSubmitPromotionChoiceTyped(memberId, professionId, selection)
        );
    }

    internal RuntimeCommandResult CancelPromotionChoice()
    {
        return RunRuntimeCommand(() => _runtime.CommandCancelPromotionChoiceTyped());
    }

    internal RuntimeCommandResult ConfirmActiveReward()
    {
        return RunRuntimeCommand(() => _runtime.CommandConfirmActiveRewardTyped());
    }

    internal RuntimeCommandResult ResetBattleFocus()
    {
        return RunRuntimeCommand(() => _runtime.ResetBattleFocusTyped());
    }

    internal RuntimeCommandResult SelectWorldCell(Vector2I coord)
    {
        return RunRuntimeCommand(() => _runtime.SelectWorldCellTyped(coord));
    }

    internal RuntimeCommandResult InspectWorldCell(Vector2I coord)
    {
        return RunRuntimeCommand(() => _runtime.InspectWorldCellTyped(coord));
    }

    internal RuntimeCommandResult SelectBattleCell(Vector2I coord)
    {
        return RunRuntimeCommand(() => _runtime.SelectBattleCellTyped(coord));
    }

    internal RuntimeCommandResult InspectBattleCell(Vector2I coord)
    {
        return RunRuntimeCommand(() => _runtime.InspectBattleCellTyped(coord));
    }

    private RuntimeCommandResult RunRuntimeCommand(Func<RuntimeCommandResult> command)
    {
        if (_runtime == null)
            return RuntimeUnavailableError();
        _runtime.ResetLastCommandBattlePresentationDelta();
        RuntimeCommandResult result = command?.Invoke() ?? RuntimeCommandResult.Failure("");
        RenderRuntimeCommandResult(result);
        return result;
    }

    private Dictionary RunRuntimeDictionaryCommand(Func<Dictionary> command)
    {
        if (_runtime == null)
            return RuntimeCommandResultProjection.Project(RuntimeUnavailableError());
        _runtime.ResetLastCommandBattlePresentationDelta();
        Dictionary result = command?.Invoke() ?? new Dictionary();
        RenderCommandPayload(result);
        return result;
    }

    private void RenderRuntimeCommandResult(RuntimeCommandResult result)
    {
        if (TryRenderLastCommandPresentationDelta())
            return;
        using Dictionary payload = RuntimeCommandResultProjection.Project(result);
        _renderTarget?.RenderFromRuntime(true, payload);
    }

    private void RenderCommandPayload(Dictionary commandResult)
    {
        if (TryRenderLastCommandPresentationDelta())
            return;
        _renderTarget?.RenderFromRuntime(true, commandResult);
    }

    private bool TryRenderLastCommandPresentationDelta()
    {
        BattlePresentationDelta delta = GetLastCommandBattlePresentationDelta();
        if (_runtime?.IsBattleActive() != true || !delta.HasChanges)
            return false;
        _renderTarget?.RenderFromRuntime(true, delta);
        return true;
    }

    private static RuntimeCommandResult RuntimeUnavailableError()
    {
        return RuntimeCommandResult.Failure(
            RuntimeUnavailableMessage,
            GameRuntimeFacade.RuntimeCommandCode.RuntimeUnavailable
        );
    }
}
