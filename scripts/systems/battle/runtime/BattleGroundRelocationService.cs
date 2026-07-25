using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal class BattleGroundRelocationService
{
    private WeakReference<BattleRuntimeModule> _runtimeRef;
    private BattleGroundEffectService _owner;
    private BattleGroundEffectCoordService _coordService;

    private BattleRuntimeModule _runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<BattleRuntimeModule>(value) : null;
    }

    internal void Setup(
        BattleRuntimeModule runtime,
        BattleGroundEffectService owner,
        BattleGroundEffectCoordService coordService
    )
    {
        _runtime = runtime;
        _owner = owner;
        _coordService = coordService;
    }

    internal int ActiveDependencyCount =>
        (_runtime != null ? 1 : 0)
        + (_owner != null ? 1 : 0)
        + (_coordService != null ? 1 : 0);

    internal void DisposeRuntime()
    {
        _coordService = null;
        _owner = null;
        _runtime = null;
    }

    private static BattleRuntimeModule ResolveWeakRef(WeakReference<BattleRuntimeModule> weakRef)
    {
        if (weakRef == null || !weakRef.TryGetTarget(out BattleRuntimeModule target))
        {
            return null;
        }
        return target;
    }

    private static readonly StringName Empty = "";

    private BattleRuntimeModule Runtime => _runtime;
    private BattleState State => Runtime?._state;
    private BattleGridService GridService => Runtime?._grid_service;
    private BattleLayeredBarrierService LayeredBarrierService => Runtime?._layered_barrier_service;


    internal bool ApplyGroundPrecastSpecialEffects(
        BattleUnitState activeUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        IReadOnlyList<Vector2I> targetCoords,
        BattleEventBatch batch
    )
    {
        return _get_ground_relocation_effect_definition(
                skillDefinition,
                castVariantDefinition
            ) == null
            || ApplyGroundRelocation(
                activeUnit,
                skillDefinition,
                castVariantDefinition,
                targetCoords,
                batch
            );
    }

    private bool ApplyGroundRelocation(
        BattleUnitState activeUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        IReadOnlyList<Vector2I> targetCoords,
        BattleEventBatch batch
    )
    {
        if (State == null || activeUnit == null || targetCoords == null || targetCoords.Count == 0)
        {
            return false;
        }
        CombatEffectDefinition effectDefinition =
            _get_ground_relocation_effect_definition(skillDefinition, castVariantDefinition);
        return effectDefinition != null
            && ApplyGroundRelocationWithMode(
                activeUnit,
                targetCoords,
                batch,
                effectDefinition.ForcedMoveModeKind
            );
    }

    private bool ApplyGroundRelocationWithMode(
        BattleUnitState active_unit,
        IReadOnlyList<Vector2I> target_coords,
        BattleEventBatch batch,
        BattleForcedMoveMode move_mode
    )
    {
        BattleState state = State;
        BattleGridService gridService = GridService;
        if (
            state == null
            || gridService == null
            || active_unit == null
            || target_coords == null
            || target_coords.Count == 0
        )
        {
            return false;
        }
        Vector2I landingCoord = target_coords[0];
        if (active_unit.GetAnchorCoord() == landingCoord)
        {
            return true;
        }
        Vector2I previousAnchor = active_unit.GetAnchorCoord();
        BattleOccupiedCoordReadView occupiedCoords =
            active_unit.GetOccupiedCoordsReadViewTyped();
        List<Vector2I> previousCoords =
            occupiedCoords.IsPresent
                ? new List<Vector2I>(occupiedCoords)
                : new List<Vector2I>();
        BattleLayeredBarrierService layeredBarrierService = LayeredBarrierService;
        if (layeredBarrierService != null)
        {
            BattleBarrierInteractionResult barrierResult =
                layeredBarrierService.ResolveUnitBoundaryCrossingResult(
                    active_unit,
                    previousAnchor,
                    landingCoord,
                    batch
                );
            if (
                barrierResult.Blocked
                || !active_unit.IsAlive()
                || active_unit.GetAnchorCoord() != previousAnchor
            )
            {
                return false;
            }
        }
        if (!gridService.MoveUnitForce(state, active_unit, landingCoord))
        {
            return false;
        }
        _owner.AppendChangedCoords(batch, previousCoords);
        _owner._append_changed_unit_coords(batch, active_unit);
        _owner._append_changed_unit_id(batch, active_unit.unit_id);
        string moveLabel = move_mode == BattleForcedMoveMode.Blink ? "闪现至" : "跳至";
        BattleGroundEffectService.AppendLog(
            batch,
            $"{BattleGroundEffectService.DisplayName(active_unit)} 从 ({previousAnchor.X}, {previousAnchor.Y}) {moveLabel} ({landingCoord.X}, {landingCoord.Y})。"
        );
        return true;
    }

    internal bool ApplyGroundJumpRelocation(
        BattleUnitState active_unit,
        IReadOnlyList<Vector2I> target_coords,
        BattleEventBatch batch
    )
    {
        return ApplyGroundRelocationWithMode(
            active_unit,
            target_coords,
            batch,
            BattleForcedMoveMode.Jump
        );
    }

    internal CombatEffectDefinition _get_ground_relocation_effect_definition(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition
    )
    {
        if (castVariantDefinition != null)
        {
            foreach (
                CombatEffectDefinition effectDefinition in castVariantDefinition.EffectDefinitions
                    ?? Array.Empty<CombatEffectDefinition>()
            )
            {
                if (_is_ground_relocation_effect(effectDefinition))
                {
                    return effectDefinition;
                }
            }
        }
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (combatProfile != null)
        {
            foreach (
                CombatEffectDefinition effectDefinition in combatProfile.EffectDefinitions
                    ?? Array.Empty<CombatEffectDefinition>()
            )
            {
                if (_is_ground_relocation_effect(effectDefinition))
                {
                    return effectDefinition;
                }
            }
        }
        return null;
    }

    internal bool _is_ground_relocation_effect(CombatEffectDefinition effectDefinition)
    {
        return effectDefinition != null
            && effectDefinition.EffectKind == BattleEffectKind.ForcedMove
            && _is_ground_relocation_mode(effectDefinition.ForcedMoveModeKind);
    }

    internal bool _is_ground_relocation_mode(BattleForcedMoveMode mode)
    {
        return mode == BattleForcedMoveMode.Jump || mode == BattleForcedMoveMode.Blink;
    }

    internal bool _can_use_ground_relocation(
        BattleUnitState active_unit,
        Vector2I landing_coord,
        CombatEffectDefinition effectDefinition
    )
    {
        if (effectDefinition == null || GridService == null)
        {
            return false;
        }
        BattleForcedMoveMode mode = effectDefinition.ForcedMoveModeKind;
        if (mode == BattleForcedMoveMode.Jump)
        {
            return GridService.CanJumpArc(
                State,
                active_unit,
                landing_coord,
                effectDefinition
            );
        }
        if (mode == BattleForcedMoveMode.Blink)
        {
            return GridService.CanBlinkToCoord(
                State,
                active_unit,
                landing_coord,
                effectDefinition
            );
        }
        return false;
    }

    internal bool _can_use_ground_relocation(
        BattleUnitReadView active_unit,
        Vector2I landing_coord,
        CombatEffectDefinition effectDefinition
    )
    {
        if (effectDefinition == null || GridService == null)
        {
            return false;
        }
        BattleForcedMoveMode mode = effectDefinition.ForcedMoveModeKind;
        if (mode == BattleForcedMoveMode.Jump)
        {
            return GridService.CanJumpArc(
                State,
                active_unit,
                landing_coord,
                effectDefinition
            );
        }
        if (mode == BattleForcedMoveMode.Blink)
        {
            return GridService.CanBlinkToCoord(
                State,
                active_unit,
                landing_coord,
                effectDefinition
            );
        }
        return false;
    }

    internal static BattleForcedMoveContext BuildGroundForcedMoveContext(
        BattleUnitState sourceUnit,
        IReadOnlyList<Vector2I> targetCoords
    )
    {
        if (sourceUnit == null || targetCoords == null || targetCoords.Count == 0)
        {
            return BattleForcedMoveContext.Empty;
        }
        return BattleForcedMoveContext.FromDirection(
            targetCoords[0] - sourceUnit.GetAnchorCoord()
        );
    }

    internal Vector2I _normalize_axis_direction(Vector2I direction)
    {
        if (direction == Vector2I.Zero)
        {
            return Vector2I.Zero;
        }
        int absX = Math.Abs(direction.X);
        int absY = Math.Abs(direction.Y);
        if (absX >= absY && absX > 0)
        {
            return new Vector2I(direction.X > 0 ? 1 : -1, 0);
        }
        if (absY > 0)
        {
            return new Vector2I(0, direction.Y > 0 ? 1 : -1);
        }
        return Vector2I.Zero;
    }

    internal static IReadOnlyList<CombatEffectDefinition> CollectWindPushEffectDefinitions(
        IReadOnlyList<CombatEffectDefinition> effectDefinitions
    )
    {
        var windPushEffects = new List<CombatEffectDefinition>();
        var seen = new HashSet<int>();
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effectDefinition == null
                || effectDefinition.EffectKind != BattleEffectKind.ForcedMove
                || effectDefinition.ForcedMoveModeKind != BattleForcedMoveMode.WindPush
            )
            {
                continue;
            }
            int instanceId = RuntimeHelpers.GetHashCode(effectDefinition);
            if (seen.Add(instanceId))
            {
                windPushEffects.Add(effectDefinition);
            }
        }
        return windPushEffects;
    }

    internal int _dot_coord(Vector2I coord, Vector2I direction) =>
        coord.X * direction.X + coord.Y * direction.Y;

    internal int _perpendicular_coord(Vector2I coord, Vector2I direction) =>
        direction.X != 0 ? coord.Y : coord.X;

    private List<BattleUnitState> SortWindPushUnitsNearToFar(
        IReadOnlyList<BattleUnitState> units,
        Vector2I direction
    )
    {
        var sorted = new List<BattleUnitState>();
        foreach (BattleUnitState unitState in units ?? Array.Empty<BattleUnitState>())
        {
            if (unitState != null && unitState.IsAlive())
            {
                sorted.Add(unitState);
            }
        }
        sorted.Sort(
            (left, right) =>
            {
                int leftProjection = _dot_coord(
                    left.GetAnchorCoord(),
                    direction
                );
                int rightProjection = _dot_coord(
                    right.GetAnchorCoord(),
                    direction
                );
                if (leftProjection != rightProjection)
                {
                    return leftProjection.CompareTo(rightProjection);
                }
                int leftSide = _perpendicular_coord(
                    left.GetAnchorCoord(),
                    direction
                );
                int rightSide = _perpendicular_coord(
                    right.GetAnchorCoord(),
                    direction
                );
                if (leftSide != rightSide)
                {
                    return leftSide.CompareTo(rightSide);
                }
                return string.Compare(
                    left.unit_id.ToString(),
                    right.unit_id.ToString(),
                    StringComparison.Ordinal
                );
            }
        );
        return sorted;
    }

    internal static void AppendAffectedUnitId(
        HashSet<StringName> affectedUnitIds,
        BattleUnitState unitState
    )
    {
        if (unitState != null)
        {
            affectedUnitIds?.Add(unitState.unit_id);
        }
    }

    private List<BattleUnitState> CollectWindPushTargetUnits(
        BattleUnitState sourceUnit,
        SkillDefinition skillDefinition,
        CombatEffectDefinition effectDefinition,
        IReadOnlyList<Vector2I> effectCoords
    )
    {
        var units = new List<BattleUnitState>();
        if (effectDefinition == null)
        {
            return units;
        }
        StringName targetFilter = _owner.ResolveEffectTargetFilter(skillDefinition, effectDefinition);
        foreach (BattleUnitState targetUnit in _coordService.CollectUnitsInCoords(effectCoords))
        {
            if (targetUnit == null || !targetUnit.IsAlive())
            {
                continue;
            }
            if (!_owner._is_unit_valid_for_effect(sourceUnit, targetUnit, targetFilter))
            {
                continue;
            }
            units.Add(targetUnit);
        }
        return units;
    }

    private bool TryWindPushUnitOneStep(
        BattleUnitState sourceUnit,
        SkillDefinition skillDefinition,
        CombatEffectDefinition effectDefinition,
        BattleUnitState unitState,
        Vector2I direction,
        HashSet<StringName> movedThisStep,
        HashSet<StringName> affectedUnitIds,
        HashSet<StringName> recursionStack,
        BattleEventBatch batch
    )
    {
        BattleState state = State;
        BattleGridService gridService = GridService;
        if (
            Runtime == null
            || state == null
            || gridService == null
            || unitState == null
            || !unitState.IsAlive()
            || direction == Vector2I.Zero
        )
        {
            return false;
        }
        StringName unitId = unitState.unit_id;
        if (movedThisStep.Contains(unitId))
        {
            return false;
        }
        if (Runtime._blocks_enemy_forced_move(sourceUnit, unitState))
        {
            BattleGroundEffectService.AppendLog(batch, $"{unitState.display_name} 稳如金刚，未被强制位移。");
            return false;
        }
        if (recursionStack.Contains(unitId))
        {
            return false;
        }
        Vector2I currentCoord = unitState.GetAnchorCoord();
        Vector2I nextCoord = currentCoord + direction;
        if (!gridService.IsInside(state, nextCoord))
        {
            return false;
        }
        var nextStack = new HashSet<StringName>(recursionStack) { unitId };
        StringName targetFilter = _owner.ResolveEffectTargetFilter(skillDefinition, effectDefinition);
        foreach (
            var rawBlockingUnitId in gridService.CollectBlockingUnitIds(
                state,
                unitState,
                nextCoord
            )
        )
        {
            StringName blockingUnitId = BattleGroundEffectService.ToStringName(rawBlockingUnitId);
            if (blockingUnitId == unitId)
            {
                continue;
            }
            if (
                !state.TryGetUnitTyped(blockingUnitId, out BattleUnitState blockingUnit)
                || !blockingUnit.IsAlive()
            )
            {
                return false;
            }
            if (!_owner._is_unit_valid_for_effect(sourceUnit, blockingUnit, targetFilter))
            {
                return false;
            }
            if (
                !TryWindPushUnitOneStep(
                    sourceUnit,
                    skillDefinition,
                    effectDefinition,
                    blockingUnit,
                    direction,
                    movedThisStep,
                    affectedUnitIds,
                    nextStack,
                    batch
                )
            )
            {
                return false;
            }
        }
        if (!gridService.CanTraverse(state, currentCoord, nextCoord, unitState))
        {
            return false;
        }
        BattleLayeredBarrierService layeredBarrierService = LayeredBarrierService;
        BattleBarrierInteractionResult barrierResult =
            layeredBarrierService != null
                ? layeredBarrierService.ResolveUnitBoundaryCrossingResult(
                    unitState,
                    currentCoord,
                    nextCoord,
                    batch
                )
                : new BattleBarrierInteractionResult(false, false);
        if (barrierResult.Blocked || !unitState.IsAlive())
        {
            AppendAffectedUnitId(affectedUnitIds, unitState);
            return false;
        }
        BattleOccupiedCoordReadView occupiedCoords =
            unitState.GetOccupiedCoordsReadViewTyped();
        List<Vector2I> previousCoords =
            occupiedCoords.IsPresent
                ? new List<Vector2I>(occupiedCoords)
                : new List<Vector2I>();
        if (!gridService.MoveUnit(state, unitState, nextCoord))
        {
            return false;
        }
        movedThisStep.Add(unitId);
        AppendAffectedUnitId(affectedUnitIds, unitState);
        _owner.AppendChangedCoords(batch, previousCoords);
        _owner._append_changed_unit_coords(batch, unitState);
        _owner._append_changed_unit_id(batch, unitId);
        return true;
    }

    internal BattleGroundWindPushResult _apply_ground_wind_push_effects_result(
        BattleUnitState sourceUnit,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> windPushEffects,
        IReadOnlyList<Vector2I> effectCoords,
        IReadOnlyList<Vector2I> targetCoords,
        BattleEventBatch batch
    )
    {
        bool applied = false;
        if (sourceUnit == null || windPushEffects == null || windPushEffects.Count == 0)
        {
            return new BattleGroundWindPushResult(false, System.Array.Empty<StringName>());
        }
        BattleForcedMoveContext forcedMoveContext = BuildGroundForcedMoveContext(
            sourceUnit,
            targetCoords
        );
        Vector2I direction = forcedMoveContext.Direction;
        if (direction == Vector2I.Zero)
        {
            return new BattleGroundWindPushResult(false, System.Array.Empty<StringName>());
        }
        var affectedUnitIds = new HashSet<StringName>();
        foreach (CombatEffectDefinition effectDefinition in windPushEffects)
        {
            if (effectDefinition == null)
            {
                continue;
            }
            List<BattleUnitState> targetUnits = CollectWindPushTargetUnits(
                sourceUnit,
                skillDefinition,
                effectDefinition,
                effectCoords
            );
            if (targetUnits.Count == 0)
            {
                continue;
            }
            int moveDistance = Math.Max(effectDefinition.ForcedMoveDistance, 0);
            for (int stepIndex = 0; stepIndex < moveDistance; stepIndex++)
            {
                var movedThisStep = new HashSet<StringName>();
                bool movedAny = false;
                List<BattleUnitState> orderedUnits = SortWindPushUnitsNearToFar(
                    targetUnits,
                    direction
                );
                foreach (BattleUnitState targetUnit in orderedUnits)
                {
                    if (targetUnit == null || !targetUnit.IsAlive())
                    {
                        continue;
                    }
                    if (movedThisStep.Contains(targetUnit.unit_id))
                    {
                        continue;
                    }
                    if (
                        TryWindPushUnitOneStep(
                            sourceUnit,
                            skillDefinition,
                            effectDefinition,
                            targetUnit,
                            direction,
                            movedThisStep,
                            affectedUnitIds,
                            new HashSet<StringName>(),
                            batch
                        )
                    )
                    {
                        movedAny = true;
                        applied = true;
                    }
                }
                if (!movedAny)
                {
                    break;
                }
            }
        }
        return new BattleGroundWindPushResult(applied, BattleGroundEffectService.KeysStringNameList(affectedUnitIds));
    }
}
