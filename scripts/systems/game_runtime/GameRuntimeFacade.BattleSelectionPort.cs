using System;
using System.Collections.Generic;
using Godot;

public sealed partial class GameRuntimeFacade : IGameRuntimeBattleSelectionPort
{
    Vector2I IGameRuntimeBattleSelectionPort.GetBattleSelectedCoord() =>
        _battle_selected_coord;

    BattleUnitState IGameRuntimeBattleSelectionPort.GetManualBattleUnit() =>
        GetManualBattleUnit();

    BattleUnitState IGameRuntimeBattleSelectionPort.GetRuntimeBattleActiveUnit() =>
        GetRuntimeBattleActiveUnit();

    BattleUnitState IGameRuntimeBattleSelectionPort.GetRuntimeBattleUnitAtCoord(Vector2I coord) =>
        GetRuntimeBattleUnitAtCoord(coord);

    BattleUnitState IGameRuntimeBattleSelectionPort.GetRuntimeBattleUnitById(StringName unitId) =>
        GetRuntimeBattleUnitById(unitId);

    BattleState IGameRuntimeBattleSelectionPort.GetBattleState() => _battle_state;

    BattleGridService IGameRuntimeBattleSelectionPort.GetBattleGridService() =>
        _battle_grid_service;

    ISkillCatalog IGameRuntimeBattleSelectionPort.GetSkillCatalog() =>
        GetSkillCatalogTyped();

    IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition>
        IGameRuntimeBattleSelectionPort.GetEquipmentAbilityBindings() =>
        _battle_runtime?.GetEquipmentAbilityBindingIndexTyped()
        ?? new Dictionary<StringName, EquipmentAbilityBindingDefinition>();

    int IGameRuntimeBattleSelectionPort.GetBattleWorldStep() =>
        _battle_runtime?.GetBattleWorldStep() ?? GetWorldStep();

    BattlePreview IGameRuntimeBattleSelectionPort.PreviewBattleCommand(BattleCommand command) =>
        PreviewBattleCommand(command);

    string IGameRuntimeBattleSelectionPort.GetBattleSkillCastBlockMessage(
        BattleUnitState activeUnit,
        StringName skillId
    ) => GetBattleSkillCastBlockMessage(activeUnit, skillId);

    BattleRefreshMode IGameRuntimeBattleSelectionPort.IssueBattleCommand(BattleCommand command) =>
        IssueBattleCommand(command);

    void IGameRuntimeBattleSelectionPort.RefreshBattleSelectionState() =>
        RefreshBattleSelectionState();

    void IGameRuntimeBattleSelectionPort.UpdateStatus(string message) => UpdateStatus(message);

    string IGameRuntimeBattleSelectionPort.FormatCoord(Vector2I coord) => FormatCoord(coord);

    bool IGameRuntimeBattleSelectionPort.IsBattleActive() => IsBattleActive();

    StringName IGameRuntimeBattleSelectionPort.GetSelectedSkillId() =>
        _selected_battle_skill_id;

    StringName IGameRuntimeBattleSelectionPort.GetSelectedSkillEntryId() =>
        _selected_battle_skill_entry_id;

    void IGameRuntimeBattleSelectionPort.SetSelectedSkillEntryId(StringName skillEntryId) =>
        _selected_battle_skill_entry_id = skillEntryId;

    void IGameRuntimeBattleSelectionPort.SetSelectedSkillId(StringName skillId) =>
        _selected_battle_skill_id = skillId;

    StringName IGameRuntimeBattleSelectionPort.GetSelectedSkillVariantId() =>
        _selected_battle_skill_variant_id;

    void IGameRuntimeBattleSelectionPort.SetSelectedSkillVariantId(StringName variantId) =>
        _selected_battle_skill_variant_id = variantId;

    StringName IGameRuntimeBattleSelectionPort.GetLastManualUnitId() =>
        _last_manual_battle_unit_id;

    void IGameRuntimeBattleSelectionPort.SetLastManualUnitId(StringName unitId) =>
        _last_manual_battle_unit_id = unitId;

    IReadOnlyList<Vector2I> IGameRuntimeBattleSelectionPort.GetTargetCoords() =>
        _battle_selection_state.queued_target_coords;

    void IGameRuntimeBattleSelectionPort.SetTargetCoords(IEnumerable<Vector2I> targetCoords) =>
        _battle_selection_state.SetTargetCoords(targetCoords ?? Array.Empty<Vector2I>());

    IReadOnlyList<StringName> IGameRuntimeBattleSelectionPort.GetTargetUnitIds() =>
        _battle_selection_state.queued_target_unit_ids;

    void IGameRuntimeBattleSelectionPort.SetTargetUnitIds(IEnumerable<StringName> targetUnitIds) =>
        _battle_selection_state.SetTargetUnitIds(targetUnitIds ?? Array.Empty<StringName>());

    void IGameRuntimeBattleSelectionPort.SetBattleSelectedCoord(Vector2I coord) =>
        _battle_selected_coord = coord;
}
