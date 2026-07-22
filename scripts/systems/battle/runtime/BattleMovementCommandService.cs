using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GArray = Godot.Collections.Array;
using GBattleUnitArray = System.Collections.Generic.List<BattleUnitState>;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

internal sealed class BattleMovementCommandService : BattleRuntimeModuleBorrower
{

    internal int _get_move_cost_for_unit_target(BattleUnitState unit_state, Vector2I target_coord)
    {
        _runtime._ensure_sidecars_ready();
        return _runtime._movement_service.GetMoveCostForUnitTarget(unit_state, target_coord);
    }

    internal int _get_move_path_cost(BattleUnitState unit_state, GVector2IArray anchor_path)
    {
        _runtime._ensure_sidecars_ready();
        return _runtime._movement_service.GetMovePathCost(unit_state, BattleRuntimeModule.ToVector2IList(anchor_path));
    }

    internal int _get_status_move_cost_delta(BattleUnitState unit_state)
    {
        _runtime._ensure_sidecars_ready();
        return _runtime._movement_service.GetStatusMoveCostDelta(unit_state);
    }

    internal int _get_available_move_points(BattleUnitState unit_state)
    {
        _runtime._ensure_sidecars_ready();
        return _runtime._movement_service.GetAvailableMovePoints(unit_state);
    }

    internal bool _is_normal_movement_locked(BattleUnitState unit_state)
    {
        _runtime._ensure_sidecars_ready();
        return _runtime._movement_service.IsNormalMovementLocked(unit_state);
    }

    internal void _handle_move_command(
        BattleUnitState active_unit,
        BattleCommand command,
        BattleEventBatch batch
    )
    {
        _runtime._ensure_sidecars_ready();
        _runtime._movement_service.HandleMoveCommand(active_unit, command, batch);
    }

    internal bool _move_unit_along_validated_path(
        BattleUnitState active_unit,
        GVector2IArray anchor_path,
        Vector2I target_coord,
        BattleEventBatch batch
    )
    {
        _runtime._ensure_sidecars_ready();
        return _runtime._movement_service.MoveUnitAlongValidatedPathTyped(
            active_unit,
            BattleRuntimeModule.ToVector2IList(anchor_path),
            target_coord,
            batch
        ).ReachedTarget;
    }

    internal bool _swap_unit_positions(
        BattleUnitState first_unit,
        BattleUnitState second_unit,
        BattleEventBatch batch
    )
    {
        _runtime._ensure_sidecars_ready();
        return _runtime._special_skill_resolver.SwapUnitPositions(first_unit, second_unit, batch);
    }

    internal Vector2I _pick_forced_move_coord(
        BattleUnitState unit_state,
        BattleForcedMoveMode mode,
        BattleUnitState source_unit = null,
        BattleForcedMoveContext forced_move_context = default
    )
    {
        _runtime._ensure_sidecars_ready();
        return _runtime._special_skill_resolver.PickForcedMoveCoord(
            unit_state,
            mode,
            source_unit,
            forced_move_context
        );
    }

    internal Vector2I PickForcedMoveCoord(
        BattleUnitState unit_state,
        BattleForcedMoveMode mode,
        BattleUnitState source_unit,
        BattleForcedMoveContext forced_move_context
    )
    {
        _runtime._ensure_sidecars_ready();
        return _runtime._special_skill_resolver.PickForcedMoveCoord(
            unit_state,
            mode,
            source_unit,
            forced_move_context
        );
    }

    internal int _score_forced_move_coord(
        BattleUnitState unit_state,
        Vector2I candidate_coord,
        BattleForcedMoveMode mode,
        BattleUnitState source_unit = null,
        BattleForcedMoveContext forced_move_context = default
    )
    {
        _runtime._ensure_sidecars_ready();
        return _runtime._special_skill_resolver.ScoreForcedMoveCoord(
            unit_state,
            candidate_coord,
            mode,
            source_unit,
            forced_move_context
        );
    }

    internal int ScoreForcedMoveCoord(
        BattleUnitState unit_state,
        Vector2I candidate_coord,
        BattleForcedMoveMode mode,
        BattleUnitState source_unit,
        BattleForcedMoveContext forced_move_context
    )
    {
        _runtime._ensure_sidecars_ready();
        return _runtime._special_skill_resolver.ScoreForcedMoveCoord(
            unit_state,
            candidate_coord,
            mode,
            source_unit,
            forced_move_context
        );
    }

    internal bool _are_units_adjacent(BattleUnitState first_unit, BattleUnitState second_unit)
    {
        _runtime._ensure_sidecars_ready();
        return _runtime._special_skill_resolver.AreUnitsAdjacent(first_unit, second_unit);
    }

    internal bool _is_movement_blocked(BattleUnitState unit_state) =>
        _runtime._skill_turn_resolver.IsMovementBlocked(unit_state);
}
