using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal interface IBattleSelectionSessionSurface
{
    string GetSelectedBattleSkillName();
    string GetSelectedBattleSkillVariantName();
    IReadOnlyList<Vector2I> GetSelectedBattleSkillTargetCoordsSnapshotPlain();
    IReadOnlyList<StringName> GetSelectedBattleSkillTargetUnitIdsSnapshotPlain();
    IReadOnlyList<Vector2I> GetSelectedBattleSkillValidTargetCoordsSnapshotPlain();
    int GetSelectedBattleSkillRequiredCoordCount();
    BattlePreview GetSelectedBattleSkillPreview();
    BattlePreview PreviewSelectedBattleSkillAtCoord(Vector2I coord);
    BattleSelectionCommandResult SelectBattleSkillSlotTyped(int index);
    void CycleSelectedBattleSkillOption(int step);
    void ClearBattleSkillSelection(bool announce = false);
    BattleRefreshMode AttemptBattleMoveTo(Vector2I targetCoord);
    BattleRefreshMode ResetBattleMovement();
    void SyncSelectedBattleSkillState();
}

internal interface IGameRuntimeBattleSessionPort
{
    IBattleSelectionSessionSurface GetBattleSelection();
    StringName GetSelectedBattleSkillId();
    IReadOnlyList<Vector2I> GetBattleMovementReachableCoords(BattleUnitState unitState);
    BattleState GetRuntimeBattleState();
    BattleState GetPublishedBattleState();
    BattleUnitState GetBattleUnitAtCoord(BattleState battleState, Vector2I coord);
    BattleEventBatch AdvanceBattle(int tickCount);
    BattlePreview PreviewBattleCommand(BattleCommand command);
    BattleEventBatch IssueBattleCommand(BattleCommand command);
    BattleResolutionResult GetBattleResolutionResult();
    BattleResolutionResult ConsumeBattleResolutionResult();

    void CaptureLastCommandBattlePresentationDelta(BattleEventBatch batch);
    void PrepareBattleStart(EncounterAnchorData encounterAnchor);
    StringName BeginBattleStart(
        EncounterAnchorData encounterAnchor,
        int seed,
        GDictionary context
    );
    bool FinalizeBattleResolution(BattleResolutionResult battleResolutionResult);
    void RecordCommandBattleBatch(BattleEventBatch batch);

    Vector2I GetBattleSelectedCoord();
    void SetPublishedBattleState(BattleState state);
    void SetBattleSelectedCoord(Vector2I coord);
    void SetActiveModalKind(RuntimeModalKind modalKind);
    void ClearBattleSelectionTargets();
    bool IsBattleActive();

    bool HasPendingPromotionPrompt();
    void SetPendingPromotionPrompt(GameRuntimePromotionPromptContext prompt);
    string GetMemberDisplayName(StringName memberId);
    bool TryGetProfessionDefinition(
        StringName professionId,
        out ProfessionDefinition professionDefinition
    );

    Vector2I GetPlayerCoord();
    int GetWorldStep();
    string GetStatusText();
    RuntimeModalKind GetActiveModalKind();
    bool IsModalWindowOpen();
    void UpdateStatus(string message);
    bool TryOpenCharacterInfoAtBattleCoord(Vector2I coord);
}
