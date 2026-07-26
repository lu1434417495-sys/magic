using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public sealed partial class GameRuntimeFacade : IGameRuntimeBattleSessionPort
{
    IBattleSelectionSessionSurface IGameRuntimeBattleSessionPort.GetBattleSelection() =>
        _battle_selection;

    StringName IGameRuntimeBattleSessionPort.GetSelectedBattleSkillId() =>
        _selected_battle_skill_id;

    IReadOnlyList<Vector2I> IGameRuntimeBattleSessionPort.GetBattleMovementReachableCoords(
        BattleUnitState unitState
    ) =>
        _battle_runtime?.GetUnitReachableMoveCoordsTyped(unitState)
        ?? Array.Empty<Vector2I>();

    BattleState IGameRuntimeBattleSessionPort.GetRuntimeBattleState() =>
        _battle_runtime?.GetState();

    BattleState IGameRuntimeBattleSessionPort.GetPublishedBattleState() => _battle_state;

    BattleUnitState IGameRuntimeBattleSessionPort.GetBattleUnitAtCoord(
        BattleState battleState,
        Vector2I coord
    ) => _battle_grid_service?.GetUnitAtCoord(battleState, coord);

    BattleEventBatch IGameRuntimeBattleSessionPort.AdvanceBattle(int tickCount) =>
        _battle_runtime?.advance(tickCount);

    BattlePreview IGameRuntimeBattleSessionPort.PreviewBattleCommand(BattleCommand command) =>
        _battle_runtime?.PreviewCommand(command);

    BattleEventBatch IGameRuntimeBattleSessionPort.IssueBattleCommand(BattleCommand command) =>
        _battle_runtime?.IssueCommand(command);

    BattleResolutionResult IGameRuntimeBattleSessionPort.GetBattleResolutionResult() =>
        _battle_runtime?.GetBattleResolutionResult();

    BattleResolutionResult IGameRuntimeBattleSessionPort.ConsumeBattleResolutionResult() =>
        _battle_runtime?.ConsumeBattleResolutionResult();

    void IGameRuntimeBattleSessionPort.CaptureLastCommandBattlePresentationDelta(
        BattleEventBatch batch
    ) => CaptureLastCommandBattlePresentationDelta(batch);

    void IGameRuntimeBattleSessionPort.PrepareBattleStart(EncounterAnchorData encounterAnchor) =>
        PrepareBattleStart(encounterAnchor);

    StringName IGameRuntimeBattleSessionPort.BeginBattleStart(
        EncounterAnchorData encounterAnchor,
        int seed,
        GDictionary context
    ) => BeginBattleStart(encounterAnchor, seed, context);

    bool IGameRuntimeBattleSessionPort.FinalizeBattleResolution(
        BattleResolutionResult battleResolutionResult
    ) => FinalizeBattleResolution(battleResolutionResult);

    void IGameRuntimeBattleSessionPort.RecordCommandBattleBatch(BattleEventBatch batch) =>
        RecordCommandBattleBatch(batch);

    Vector2I IGameRuntimeBattleSessionPort.GetBattleSelectedCoord() =>
        _battle_selected_coord;

    void IGameRuntimeBattleSessionPort.SetPublishedBattleState(BattleState state) =>
        SetRuntimeBattleState(state);

    void IGameRuntimeBattleSessionPort.SetBattleSelectedCoord(Vector2I coord) =>
        _battle_selected_coord = coord;

    void IGameRuntimeBattleSessionPort.SetActiveModalKind(RuntimeModalKind modalKind) =>
        SetRuntimeActiveModalKind(modalKind);

    void IGameRuntimeBattleSessionPort.ClearBattleSelectionTargets() =>
        _battle_selection_state.ClearTargets();

    bool IGameRuntimeBattleSessionPort.IsBattleActive() => IsBattleActive();

    bool IGameRuntimeBattleSessionPort.HasPendingPromotionPrompt() =>
        !_pending_promotion_prompt.IsEmpty;

    void IGameRuntimeBattleSessionPort.SetPendingPromotionPrompt(
        GameRuntimePromotionPromptContext prompt
    ) => SetPendingPromotionPrompt(prompt);

    string IGameRuntimeBattleSessionPort.GetMemberDisplayName(StringName memberId) =>
        GetMemberDisplayName(memberId);

    bool IGameRuntimeBattleSessionPort.TryGetProfessionDefinition(
        StringName professionId,
        out ProfessionDefinition professionDefinition
    )
    {
        professionDefinition = null;
        IReadOnlyDictionary<StringName, ProfessionDefinition> professionDefinitions =
            GetContentCatalogTyped()?.GetProfessionDefsTyped();
        return professionDefinitions != null
            && professionDefinitions.TryGetValue(professionId, out professionDefinition);
    }

    Vector2I IGameRuntimeBattleSessionPort.GetPlayerCoord() => _player_coord;

    int IGameRuntimeBattleSessionPort.GetWorldStep() => GetWorldStep();

    string IGameRuntimeBattleSessionPort.GetStatusText() => _current_status_message;

    RuntimeModalKind IGameRuntimeBattleSessionPort.GetActiveModalKind() =>
        _active_modal_kind;

    bool IGameRuntimeBattleSessionPort.IsModalWindowOpen() => IsModalWindowOpenInternal();

    void IGameRuntimeBattleSessionPort.UpdateStatus(string message) =>
        UpdateStatusInternal(message);

    bool IGameRuntimeBattleSessionPort.TryOpenCharacterInfoAtBattleCoord(Vector2I coord) =>
        TryOpenCharacterInfoAtBattleCoord(coord);
}
