using System.Collections.Generic;
using Godot;

internal interface IGameRuntimeBattleSelectionPort
{
    Vector2I GetBattleSelectedCoord();
    BattleUnitState GetManualBattleUnit();
    BattleUnitState GetRuntimeBattleActiveUnit();
    BattleUnitState GetRuntimeBattleUnitAtCoord(Vector2I coord);
    BattleUnitState GetRuntimeBattleUnitById(StringName unitId);
    BattleState GetBattleState();
    BattleGridService GetBattleGridService();
    ISkillCatalog GetSkillCatalog();
    IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition>
        GetEquipmentAbilityBindings();
    int GetBattleWorldStep();
    BattlePreview PreviewBattleCommand(BattleCommand command);
    string GetBattleSkillCastBlockMessage(BattleUnitState activeUnit, StringName skillId);
    BattleRefreshMode IssueBattleCommand(BattleCommand command);
    void RefreshBattleSelectionState();
    void UpdateStatus(string message);
    string FormatCoord(Vector2I coord);
    bool IsBattleActive();

    StringName GetSelectedSkillId();
    StringName GetSelectedSkillEntryId();
    void SetSelectedSkillEntryId(StringName skillEntryId);
    void SetSelectedSkillId(StringName skillId);
    StringName GetSelectedSkillVariantId();
    void SetSelectedSkillVariantId(StringName variantId);
    StringName GetLastManualUnitId();
    void SetLastManualUnitId(StringName unitId);
    IReadOnlyList<Vector2I> GetTargetCoords();
    void SetTargetCoords(IEnumerable<Vector2I> targetCoords);
    IReadOnlyList<StringName> GetTargetUnitIds();
    void SetTargetUnitIds(IEnumerable<StringName> targetUnitIds);
    void SetBattleSelectedCoord(Vector2I coord);
}

internal readonly struct BattleSelectionCommandResult
{
    private BattleSelectionCommandResult(bool ok, string message)
    {
        Ok = ok;
        Message = message ?? "";
    }

    public bool Ok { get; }
    public string Message { get; }

    public static BattleSelectionCommandResult Success() => new(true, "");

    public static BattleSelectionCommandResult Failure(string message) => new(false, message);
}
