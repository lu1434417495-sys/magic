using System;
using System.Collections.Generic;
using Godot;

internal sealed class ContingencyTargetResolutionRequest
{
    internal BattleState BattleState { get; init; }
    internal BattleGridService GridService { get; init; }
    internal StringName OwnerUnitId { get; init; } = "";
    internal ContingencyTargetResolverState ResolverState { get; init; }
    internal ContingencyFrozenTriggerFacts FrozenFacts { get; init; } =
        ContingencyFrozenTriggerFacts.Empty;
    internal SkillDefinition StoredSkillDefinition { get; init; }
}

internal sealed class ContingencyTargetResolverService
{
    private static readonly Vector2I MissingCell = new(-1, -1);
    private static readonly StringName NoLegalCell = "no_legal_cell";
    private static readonly StringName OwnerUnitMissing = "owner_unit_missing";
    private static readonly StringName TriggerSourceUnitMissing = "trigger_source_unit_missing";
    private static readonly StringName TriggerTargetUnitMissing = "trigger_target_unit_missing";
    private static readonly StringName TriggerSourceCellMissing = "trigger_source_cell_missing";
    private static readonly StringName TriggerTargetCellMissing = "trigger_target_cell_missing";
    private static readonly StringName NoHostileUnit = "no_hostile_unit";
    private static readonly StringName MissingTriggerCell = "trigger_cell_missing";
    private static readonly StringName MissingAttackerCell = "attacker_cell_missing";
    private static readonly StringName InvalidResolver = "invalid_resolver";

    internal ContingencyTargetResolutionResult ResolveTarget(
        ContingencyTargetResolutionRequest request
    )
    {
        if (request == null || request.BattleState == null || request.GridService == null)
            return ContingencyTargetResolutionResult.Failure(InvalidResolver);

        ContingencyTargetResolverState resolverState = request.ResolverState;
        if (resolverState == null)
            return ContingencyTargetResolutionResult.Failure(InvalidResolver);

        BattleUnitState ownerUnit = ResolveLiveUnit(request.BattleState, request.OwnerUnitId);
        if (ownerUnit == null)
            return ContingencyTargetResolutionResult.Failure(OwnerUnitMissing);

        return resolverState.ResolverKind switch
        {
            ContingencyTargetResolverKind.Self => ResolveSelf(ownerUnit),
            ContingencyTargetResolverKind.TriggerSource => ResolveFrozenUnit(
                request.BattleState,
                request.FrozenFacts?.TriggerSourceUnitId ?? "",
                request.FrozenFacts?.TriggerSourceCell ?? MissingCell,
                TriggerSourceUnitMissing,
                TriggerSourceCellMissing
            ),
            ContingencyTargetResolverKind.TriggerTarget => ResolveFrozenUnit(
                request.BattleState,
                request.FrozenFacts?.TriggerTargetUnitId ?? "",
                request.FrozenFacts?.TriggerTargetCell ?? MissingCell,
                TriggerTargetUnitMissing,
                TriggerTargetCellMissing
            ),
            ContingencyTargetResolverKind.NearestEnemyToOwner => ResolveNearestEnemy(
                request.BattleState,
                request.GridService,
                ownerUnit,
                ownerUnit.GetAnchorCoord()
            ),
            ContingencyTargetResolverKind.NearestEnemyToTriggerCell => ResolveNearestEnemyToTriggerCell(
                request,
                ownerUnit
            ),
            ContingencyTargetResolverKind.OwnerCenteredArea => ResolveOwnerCenteredArea(
                request,
                ownerUnit
            ),
            ContingencyTargetResolverKind.AttackerCell => ResolveAttackerCell(request.FrozenFacts),
            ContingencyTargetResolverKind.EmptyCellNearOwner => ResolveEmptyCell(request, ownerUnit),
            _ => ContingencyTargetResolutionResult.Failure(InvalidResolver),
        };
    }

    private static ContingencyTargetResolutionResult ResolveSelf(BattleUnitState ownerUnit) =>
        ContingencyTargetResolutionResult.UnitTarget(
            ownerUnit.unit_id,
            ownerUnit.GetAnchorCoord()
        );

    private static ContingencyTargetResolutionResult ResolveFrozenUnit(
        BattleState state,
        StringName unitId,
        Vector2I frozenCell,
        StringName missingUnitReason,
        StringName missingCellReason
    )
    {
        BattleUnitState unit = ResolveLiveUnit(state, unitId);
        if (unit == null)
            return ContingencyTargetResolutionResult.Failure(missingUnitReason);
        if (frozenCell == MissingCell)
            return ContingencyTargetResolutionResult.Failure(missingCellReason);
        return ContingencyTargetResolutionResult.UnitTarget(unit.unit_id, frozenCell);
    }

    private static ContingencyTargetResolutionResult ResolveNearestEnemyToTriggerCell(
        ContingencyTargetResolutionRequest request,
        BattleUnitState ownerUnit
    )
    {
        Vector2I triggerCell = request.FrozenFacts?.TriggerCell ?? MissingCell;
        if (triggerCell == MissingCell)
            return ContingencyTargetResolutionResult.Failure(MissingTriggerCell);
        return ResolveNearestEnemy(request.BattleState, request.GridService, ownerUnit, triggerCell);
    }

    private static ContingencyTargetResolutionResult ResolveNearestEnemy(
        BattleState state,
        BattleGridService gridService,
        BattleUnitState ownerUnit,
        Vector2I anchorCell
    )
    {
        BattleUnitState bestUnit = null;
        int bestDistance = int.MaxValue;
        foreach (BattleUnitState candidate in state.AliveUnits())
        {
            if (!IsHostile(ownerUnit, candidate))
                continue;
            int distance = gridService.GetDistanceFromUnitToCoord(candidate, anchorCell);
            if (
                bestUnit == null
                || distance < bestDistance
                || (
                    distance == bestDistance
                    && string.CompareOrdinal(
                        candidate.unit_id.ToString(),
                        bestUnit.unit_id.ToString()
                    ) < 0
                )
            )
            {
                bestUnit = candidate;
                bestDistance = distance;
            }
        }

        return bestUnit == null
            ? ContingencyTargetResolutionResult.Failure(NoHostileUnit)
            : ContingencyTargetResolutionResult.UnitTarget(
                bestUnit.unit_id,
                bestUnit.GetAnchorCoord()
            );
    }

    private static ContingencyTargetResolutionResult ResolveOwnerCenteredArea(
        ContingencyTargetResolutionRequest request,
        BattleUnitState ownerUnit
    )
    {
        Vector2I anchor = ownerUnit.GetAnchorCoord();
        IReadOnlyList<Vector2I> areaCells = ResolveSkillAreaCells(
            request.BattleState,
            request.GridService,
            ownerUnit,
            request.StoredSkillDefinition,
            anchor
        );
        return ContingencyTargetResolutionResult.GroundTarget(anchor, areaCells);
    }

    private static ContingencyTargetResolutionResult ResolveAttackerCell(
        ContingencyFrozenTriggerFacts facts
    )
    {
        Vector2I attackerCell = facts?.TriggerSourceCell ?? MissingCell;
        if (attackerCell == MissingCell)
            return ContingencyTargetResolutionResult.Failure(MissingAttackerCell);
        return ContingencyTargetResolutionResult.GroundTarget(attackerCell, new[] { attackerCell });
    }

    private static ContingencyTargetResolutionResult ResolveEmptyCell(
        ContingencyTargetResolutionRequest request,
        BattleUnitState ownerUnit
    )
    {
        ContingencyTargetResolverState resolverState = request.ResolverState;
        Vector2I ownerCell = ownerUnit.GetAnchorCoord();
        int maxDistance = Math.Max(resolverState.MaxDistance, 1);
        HashSet<Vector2I> damageArea = BuildCellSet(
            request.FrozenFacts?.CurrentDamageEventAreaCells
        );
        EmptyCellCandidate best = default;
        bool hasBest = false;

        for (int y = ownerCell.Y - maxDistance; y <= ownerCell.Y + maxDistance; y++)
        {
            for (int x = ownerCell.X - maxDistance; x <= ownerCell.X + maxDistance; x++)
            {
                Vector2I candidateCell = new(x, y);
                int ownerDistance = request.GridService.GetDistance(ownerCell, candidateCell);
                if (ownerDistance > maxDistance)
                    continue;
                if (!IsLegalEmptyCell(request.BattleState, request.GridService, ownerUnit, candidateCell))
                    continue;

                EmptyCellCandidate candidate = ScoreEmptyCell(
                    request,
                    ownerUnit,
                    candidateCell,
                    ownerDistance,
                    damageArea
                );
                if (!hasBest || candidate.CompareTo(best) > 0)
                {
                    best = candidate;
                    hasBest = true;
                }
            }
        }

        if (!hasBest)
            return ContingencyTargetResolutionResult.Failure(NoLegalCell);

        bool movedOutsideDamage =
            request.FrozenFacts?.FatalDamageIncoming == true
            && damageArea.Count > 0
            && !damageArea.Contains(best.Cell);
        return ContingencyTargetResolutionResult.GroundTarget(
            best.Cell,
            new[] { best.Cell },
            movedOutsideDamage
        );
    }

    private static EmptyCellCandidate ScoreEmptyCell(
        ContingencyTargetResolutionRequest request,
        BattleUnitState ownerUnit,
        Vector2I cell,
        int ownerDistance,
        HashSet<Vector2I> damageArea
    )
    {
        ContingencyEmptyCellPreferenceKind preference = ToEmptyCellPreferenceKind(
            request.ResolverState.Preference
        );
        Vector2I sourceCell = request.FrozenFacts?.TriggerSourceCell ?? MissingCell;
        BattleCellState cellState = request.GridService.GetCellState(request.BattleState, cell);
        return new EmptyCellCandidate
        {
            Cell = cell,
            OutsideCurrentDamageArea = damageArea.Count == 0 || !damageArea.Contains(cell) ? 1 : 0,
            FartherFromTriggerSource =
                preference == ContingencyEmptyCellPreferenceKind.AwayFromTriggerSource
                && sourceCell != MissingCell
                    ? request.GridService.GetDistance(sourceCell, cell)
                    : 0,
            SafeTerrain = cellState != null && BattleTerrainRules.IsSafeTerrain(cellState.base_terrain) ? 1 : 0,
            NotAdjacentToHostile = IsAdjacentToHostile(request.BattleState, request.GridService, ownerUnit, cell)
                ? 0
                : 1,
            NearAlliedUnit = IsNearAlliedUnit(request.BattleState, request.GridService, ownerUnit, cell)
                ? 1
                : 0,
            OwnerDistance = ownerDistance,
        };
    }

    private static bool IsLegalEmptyCell(
        BattleState state,
        BattleGridService gridService,
        BattleUnitState ownerUnit,
        Vector2I anchorCell
    )
    {
        if (state == null || gridService == null || ownerUnit == null)
            return false;
        if (
            !gridService.CanPlaceFootprint(
                state,
                anchorCell,
                ownerUnit.GetFootprintSize(),
                "",
                ownerUnit
            )
        )
            return false;
        foreach (Vector2I coord in gridService.GetUnitTargetCoords(ownerUnit, anchorCell))
        {
            BattleCellState cell = gridService.GetCellState(state, coord);
            if (cell == null || !cell.passable || cell.occupant_unit_id != "")
                return false;
        }
        return true;
    }

    private static IReadOnlyList<Vector2I> ResolveSkillAreaCells(
        BattleState state,
        BattleGridService gridService,
        BattleUnitState ownerUnit,
        SkillDefinition skillDefinition,
        Vector2I anchor
    )
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (combatProfile == null || gridService == null || state == null)
            return new[] { anchor };

        List<Vector2I> cells = new();
        Vector2I direction =
            anchor - (ownerUnit?.GetAnchorCoord() ?? anchor);
        foreach (
            Vector2I cell in gridService.GetAreaCoords(
                state,
                anchor,
                combatProfile.AreaPattern,
                combatProfile.AreaValue,
                direction
            )
        )
        {
            cells.Add(cell);
        }
        cells.Sort(CompareCellsAscending);
        return cells;
    }

    private static BattleUnitState ResolveLiveUnit(BattleState state, StringName unitId)
    {
        if (state == null || unitId == "")
            return null;
        return state.TryGetUnitTyped(unitId, out BattleUnitState unit) && unit?.IsAlive() == true
            ? unit
            : null;
    }

    private static bool IsHostile(BattleUnitState ownerUnit, BattleUnitState candidate) =>
        ownerUnit != null
        && candidate != null
        && candidate.unit_id != ownerUnit.unit_id
        && candidate.IsAlive()
        && candidate.faction_id != ownerUnit.faction_id;

    private static bool IsAdjacentToHostile(
        BattleState state,
        BattleGridService gridService,
        BattleUnitState ownerUnit,
        Vector2I cell
    )
    {
        foreach (BattleUnitState candidate in state?.AliveUnits() ?? Array.Empty<BattleUnitState>())
        {
            if (!IsHostile(ownerUnit, candidate))
                continue;
            if (gridService.GetDistanceFromUnitToCoord(candidate, cell) <= 1)
                return true;
        }
        return false;
    }

    private static bool IsNearAlliedUnit(
        BattleState state,
        BattleGridService gridService,
        BattleUnitState ownerUnit,
        Vector2I cell
    )
    {
        foreach (BattleUnitState candidate in state?.AliveUnits() ?? Array.Empty<BattleUnitState>())
        {
            if (
                candidate == null
                || candidate.unit_id == ownerUnit.unit_id
                || candidate.faction_id != ownerUnit.faction_id
            )
                continue;
            if (gridService.GetDistanceFromUnitToCoord(candidate, cell) <= 2)
                return true;
        }
        return false;
    }

    private static HashSet<Vector2I> BuildCellSet(IReadOnlyList<Vector2I> cells)
    {
        HashSet<Vector2I> result = new();
        foreach (Vector2I cell in cells ?? Array.Empty<Vector2I>())
            result.Add(cell);
        return result;
    }

    private static ContingencyEmptyCellPreferenceKind ToEmptyCellPreferenceKind(
        StringName preference
    )
    {
        if (preference == "away_from_trigger_source")
            return ContingencyEmptyCellPreferenceKind.AwayFromTriggerSource;
        if (preference == "safe_cell")
            return ContingencyEmptyCellPreferenceKind.SafeCell;
        return ContingencyEmptyCellPreferenceKind.Unknown;
    }

    private static int CompareCellsAscending(Vector2I left, Vector2I right)
    {
        int y = left.Y.CompareTo(right.Y);
        return y != 0 ? y : left.X.CompareTo(right.X);
    }

    private readonly struct EmptyCellCandidate : IComparable<EmptyCellCandidate>
    {
        internal Vector2I Cell { get; init; }
        internal int OutsideCurrentDamageArea { get; init; }
        internal int FartherFromTriggerSource { get; init; }
        internal int SafeTerrain { get; init; }
        internal int NotAdjacentToHostile { get; init; }
        internal int NearAlliedUnit { get; init; }
        internal int OwnerDistance { get; init; }

        public int CompareTo(EmptyCellCandidate other)
        {
            int result = OutsideCurrentDamageArea.CompareTo(other.OutsideCurrentDamageArea);
            if (result != 0)
                return result;
            result = FartherFromTriggerSource.CompareTo(other.FartherFromTriggerSource);
            if (result != 0)
                return result;
            result = SafeTerrain.CompareTo(other.SafeTerrain);
            if (result != 0)
                return result;
            result = NotAdjacentToHostile.CompareTo(other.NotAdjacentToHostile);
            if (result != 0)
                return result;
            result = NearAlliedUnit.CompareTo(other.NearAlliedUnit);
            if (result != 0)
                return result;
            result = OwnerDistance.CompareTo(other.OwnerDistance);
            if (result != 0)
                return result;
            result = other.Cell.Y.CompareTo(Cell.Y);
            if (result != 0)
                return result;
            return other.Cell.X.CompareTo(Cell.X);
        }
    }
}
