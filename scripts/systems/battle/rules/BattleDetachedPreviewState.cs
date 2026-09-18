using System.Collections.Generic;
using Godot;

internal sealed class BattleDetachedPreviewState
{
    private BattleDetachedPreviewState(BattleState state)
    {
        State = state;
    }

    internal BattleState State { get; }

    internal BattleUnitState GetUnit(StringName unitId)
    {
        return State != null && State.TryGetUnitTyped(unitId, out BattleUnitState unit)
            ? unit
            : null;
    }

    internal static BattleDetachedPreviewState Create(
        BattleState sourceState,
        params BattleUnitState[] requiredUnits
    )
    {
        var detached = new BattleState();
        if (sourceState != null)
        {
            detached.battle_id = sourceState.battle_id;
            detached.seed = sourceState.seed;
            detached.attack_roll_nonce = sourceState.attack_roll_nonce;
            detached.phase = sourceState.phase;
            detached.map_size = sourceState.map_size;
            detached.world_coord = sourceState.world_coord;
            detached.encounter_anchor_id = sourceState.encounter_anchor_id;
            detached.terrain_profile_id = sourceState.terrain_profile_id;
            detached.attack_disadvantage_tags =
                sourceState.attack_disadvantage_tags?.Duplicate() ?? new StringNameList();
            detached.ally_unit_ids =
                sourceState.ally_unit_ids?.Duplicate() ?? new StringNameList();
            detached.enemy_unit_ids =
                sourceState.enemy_unit_ids?.Duplicate() ?? new StringNameList();
            detached.timeline =
                sourceState.timeline?.DuplicateState() ?? new BattleTimelineState();
            detached.active_unit_id = sourceState.active_unit_id;
            detached.modal_state = sourceState.modal_state;
            detached.ReplaceEnvironmentSnapshot(sourceState.GetEnvironmentSnapshot());

            var cells = new List<BattleCellState>();
            foreach (BattleState.BattleCellEntry entry in sourceState.GetCellEntriesTyped())
            {
                if (entry.Cell != null)
                    cells.Add(entry.Cell.DuplicateCell());
            }
            detached.SetCells(cells);

            var units = new List<BattleUnitState>();
            foreach (BattleUnitState unit in sourceState.GetUnitsTyped())
            {
                BattleUnitState clone = unit?.DuplicateForPreview();
                if (clone != null)
                    units.Add(clone);
            }
            detached.SetUnits(units);
        }

        foreach (BattleUnitState required in requiredUnits ?? System.Array.Empty<BattleUnitState>())
        {
            if (required == null || required.unit_id == "")
                continue;
            BattleUnitState clone = required.DuplicateForPreview();
            if (clone != null)
                detached.SetUnit(clone);
        }

        return new BattleDetachedPreviewState(detached);
    }
}
