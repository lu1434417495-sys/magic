using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

internal readonly record struct BattleChainDamageHopPlan(
    int HopIndex,
    StringName OriginUnitId,
    Vector2I OriginCoord,
    StringName TargetUnitId,
    Vector2I TargetCoord,
    int Distance,
    int OutgoingRange,
    bool OriginWasConductive
);

internal sealed class BattleChainDamagePlan
{
    internal static BattleChainDamagePlan Empty { get; } =
        new("", Array.Empty<BattleChainDamageHopPlan>());

    internal BattleChainDamagePlan(
        StringName primaryTargetUnitId,
        IReadOnlyList<BattleChainDamageHopPlan> hops
    )
    {
        PrimaryTargetUnitId = primaryTargetUnitId;
        Hops = hops == null || hops.Count == 0
            ? Array.Empty<BattleChainDamageHopPlan>()
            : new ReadOnlyCollection<BattleChainDamageHopPlan>(
                new List<BattleChainDamageHopPlan>(hops)
            );
    }

    internal StringName PrimaryTargetUnitId { get; }
    internal IReadOnlyList<BattleChainDamageHopPlan> Hops { get; }
}

internal static class BattleChainDamageRules
{
    internal static BattleChainDamagePlan BuildPlan(
        BattleStateReadView state,
        BattleUnitReadView primaryTarget,
        CombatChainDamageDefinition definition,
        bool backlashTriggered,
        Func<BattleUnitReadView, bool> isValidTarget
    )
    {
        if (
            !state.IsValid
            || !primaryTarget.IsValid
            || !primaryTarget.IsAlive
            || definition == null
            || definition.BaseHopRange < 1
            || definition.MaxTotalTargets < 0
            || definition.MaxTotalTargets == 1
            || isValidTarget == null
        )
            return BattleChainDamagePlan.Empty;

        var visited = new HashSet<StringName> { primaryTarget.UnitId };
        var hops = new List<BattleChainDamageHopPlan>();
        BattleUnitReadView current = primaryTarget;
        int secondaryTargetLimit = definition.HasTargetLimit
            ? definition.MaxTotalTargets - 1
            : int.MaxValue;
        while (hops.Count < secondaryTargetLimit)
        {
            bool conductive = IsConductive(state, current, definition);
            int outgoingRange = conductive
                ? definition.ConductiveHopRange
                : definition.BaseHopRange;
            if (backlashTriggered)
                outgoingRange += definition.BacklashHopRangeBonus;
            if (outgoingRange < 1)
                break;

            Candidate best = default;
            foreach (BattleUnitReadView candidate in state.AliveUnits())
            {
                if (
                    !candidate.IsValid
                    || visited.Contains(candidate.UnitId)
                    || !isValidTarget(candidate)
                )
                    continue;
                Candidate considered = BuildCandidate(current, candidate);
                if (!considered.IsValid || considered.Distance > outgoingRange)
                    continue;
                if (!best.IsValid || CompareCandidates(considered, best) < 0)
                    best = considered;
            }
            if (!best.IsValid)
                break;

            hops.Add(
                new BattleChainDamageHopPlan(
                    hops.Count + 1,
                    current.UnitId,
                    best.OriginCoord,
                    best.Target.UnitId,
                    best.TargetCoord,
                    best.Distance,
                    outgoingRange,
                    conductive
                )
            );
            visited.Add(best.Target.UnitId);
            current = best.Target;
        }

        return new BattleChainDamagePlan(primaryTarget.UnitId, hops);
    }

    private static bool IsConductive(
        BattleStateReadView state,
        BattleUnitReadView unit,
        CombatChainDamageDefinition definition
    )
    {
        foreach (StringName statusId in definition.ConductiveStatusIds)
        {
            if (statusId != "" && unit.HasStatusEffect(statusId))
                return true;
        }
        foreach (Vector2I coord in unit.GetOccupiedCoords())
        {
            BattleCellReadView cell = state.GetCell(coord);
            foreach (StringName effectId in definition.ConductiveTerrainEffectIds)
            {
                if (effectId != "" && cell.HasTerrainEffect(effectId))
                    return true;
            }
        }
        return false;
    }

    private static Candidate BuildCandidate(
        BattleUnitReadView origin,
        BattleUnitReadView target
    )
    {
        int bestDistance = int.MaxValue;
        Vector2I bestOriginCoord = Vector2I.Zero;
        Vector2I bestTargetCoord = Vector2I.Zero;
        bool found = false;
        foreach (Vector2I originCoord in origin.GetOccupiedCoords())
        {
            foreach (Vector2I targetCoord in target.GetOccupiedCoords())
            {
                int distance = BattleGridDistanceService.GetDistance(
                    originCoord,
                    targetCoord
                );
                if (
                    !found
                    || distance < bestDistance
                    || (
                        distance == bestDistance
                        && CompareCoordPairs(
                            originCoord,
                            targetCoord,
                            bestOriginCoord,
                            bestTargetCoord
                        ) < 0
                    )
                )
                {
                    found = true;
                    bestDistance = distance;
                    bestOriginCoord = originCoord;
                    bestTargetCoord = targetCoord;
                }
            }
        }
        return found
            ? new Candidate(target, bestOriginCoord, bestTargetCoord, bestDistance)
            : default;
    }

    private static int CompareCandidates(Candidate left, Candidate right)
    {
        int distance = left.Distance.CompareTo(right.Distance);
        if (distance != 0)
            return distance;
        int targetAnchor = CompareCoords(left.Target.Coord, right.Target.Coord);
        if (targetAnchor != 0)
            return targetAnchor;
        return string.CompareOrdinal(
            left.Target.UnitId.ToString(),
            right.Target.UnitId.ToString()
        );
    }

    private static int CompareCoordPairs(
        Vector2I leftOrigin,
        Vector2I leftTarget,
        Vector2I rightOrigin,
        Vector2I rightTarget
    )
    {
        int origin = CompareCoords(leftOrigin, rightOrigin);
        return origin != 0 ? origin : CompareCoords(leftTarget, rightTarget);
    }

    private static int CompareCoords(Vector2I left, Vector2I right)
    {
        int y = left.Y.CompareTo(right.Y);
        return y != 0 ? y : left.X.CompareTo(right.X);
    }

    private readonly record struct Candidate(
        BattleUnitReadView Target,
        Vector2I OriginCoord,
        Vector2I TargetCoord,
        int Distance
    )
    {
        internal bool IsValid => Target.IsValid;
    }
}
