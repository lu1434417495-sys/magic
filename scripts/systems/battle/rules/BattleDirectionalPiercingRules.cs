using System;
using System.Collections.Generic;
using Godot;

internal enum BattleDirectionalPiercingSkipReason
{
    None = 0,
    HeightOutOfRange,
    OppositeHeightChannel,
}

internal readonly record struct BattleDirectionalPiercingSkippedTarget(
    StringName UnitId,
    int HeightDelta,
    BattleDirectionalPiercingSkipReason Reason
);

internal sealed class BattleDirectionalPiercingPlan
{
    internal bool Allowed { get; init; }
    internal string Message { get; init; } = "";
    internal int FullRange { get; init; }
    internal int SourceHeight { get; init; }
    internal int LockedHeightSign { get; init; }
    internal Vector2I Direction { get; init; }
    internal Vector2I BlockedBeforeCoord { get; init; } = new(-1, -1);
    internal IReadOnlyList<Vector2I> PathCoords { get; init; } = Array.Empty<Vector2I>();
    internal IReadOnlyList<BattleUnitState> Targets { get; init; } = Array.Empty<BattleUnitState>();
    internal IReadOnlyList<BattleDirectionalPiercingSkippedTarget> SkippedTargets { get; init; } =
        Array.Empty<BattleDirectionalPiercingSkippedTarget>();

    internal static BattleDirectionalPiercingPlan Denied(string message) =>
        new() { Allowed = false, Message = message ?? "贯穿方向无效。" };
}

internal static class BattleDirectionalPiercingRules
{
    internal static bool IsDirectionalPiercingSkill(SkillDefinition skillDefinition) =>
        skillDefinition?.CombatProfile?.DirectionalPiercing != null;

    internal static int CalculateStaminaCost(
        CombatDirectionalPiercingDefinition profile,
        int effectiveRange,
        int strengthModifier
    )
    {
        if (profile == null)
            return 0;
        long normalizedRange = Math.Max(effectiveRange, 0);
        long rangeSquare = SaturatingMultiply(normalizedRange, normalizedRange);
        long baseCost = SaturatingAdd(
            Math.Max(profile.StaminaFlatBase, 0),
            SaturatingMultiply(Math.Max(profile.StaminaRangeSquareCoefficient, 0), rangeSquare)
        );
        long strength = strengthModifier;
        long strengthSquare = SaturatingMultiply(Math.Abs(strength), Math.Abs(strength));
        long scale = Math.Max(profile.StaminaStrengthSquareScale, 1);
        long numerator;
        long denominator;
        if (strengthModifier >= 0)
        {
            numerator = SaturatingMultiply(baseCost, scale);
            denominator = SaturatingAdd(scale, strengthSquare);
        }
        else
        {
            numerator = SaturatingMultiply(baseCost, SaturatingAdd(scale, strengthSquare));
            denominator = scale;
        }
        long roundedUp = DivideRoundUp(numerator, denominator);
        return (int)Math.Clamp(
            Math.Max(roundedUp, profile.MinimumStaminaCost),
            0L,
            int.MaxValue
        );
    }

    internal static int GetDamagePercentAfterSuccessfulHits(
        CombatDirectionalPiercingDefinition profile,
        int successfulHitCount
    )
    {
        if (profile == null)
            return 100;
        long remaining = 100L
            - (long)Math.Max(successfulHitCount, 0)
                * Math.Max(profile.SuccessfulHitDecayPercent, 0);
        return (int)Math.Max(remaining, profile.MinimumDamagePercent);
    }

    internal static double GetExpectedDecayMultiplier(
        CombatDirectionalPiercingDefinition profile,
        int priorTargetCount,
        int hitRatePercent
    )
    {
        if (profile == null || priorTargetCount <= 0)
            return 1.0;
        double hit = Math.Clamp(hitRatePercent, 0, 100) / 100.0;
        double miss = 1.0 - hit;
        int decayPercent = Math.Max(profile.SuccessfulHitDecayPercent, 1);
        int floorPercent = Math.Clamp(profile.MinimumDamagePercent, 0, 100);
        int saturatedHitCount = DivideRoundUp(100 - floorPercent, decayPercent);
        double[] states = new double[saturatedHitCount + 1];
        states[0] = 1.0;
        for (int targetIndex = 0; targetIndex < priorTargetCount; targetIndex++)
        {
            double[] next = new double[states.Length];
            for (int state = 0; state < states.Length; state++)
            {
                next[state] += states[state] * miss;
                next[Math.Min(state + 1, saturatedHitCount)] += states[state] * hit;
            }
            states = next;
        }
        double expectedPercent = 0.0;
        for (int state = 0; state < states.Length; state++)
        {
            expectedPercent += states[state]
                * GetDamagePercentAfterSuccessfulHits(profile, state);
        }
        return expectedPercent / 100.0;
    }

    private static int DivideRoundUp(int numerator, int denominator)
    {
        if (numerator <= 0 || denominator <= 0)
            return 0;
        return 1 + (numerator - 1) / denominator;
    }

    internal static BattleDirectionalPiercingPlan BuildPlan(
        BattleState state,
        BattleGridService gridService,
        BattleUnitState sourceUnit,
        SkillDefinition skillDefinition,
        Vector2I selectedCoord,
        int effectiveRange
    )
    {
        CombatDirectionalPiercingDefinition profile =
            skillDefinition?.CombatProfile?.DirectionalPiercing;
        if (state == null || gridService == null || sourceUnit == null || profile == null)
            return BattleDirectionalPiercingPlan.Denied("贯穿规则未绑定。" );

        Vector2I sourceCoord = sourceUnit.GetAnchorCoord();
        Vector2I delta = selectedCoord - sourceCoord;
        if (Math.Abs(delta.X) + Math.Abs(delta.Y) != 1)
            return BattleDirectionalPiercingPlan.Denied("请选择相邻的上、下、左或右作为射击方向。" );
        if (effectiveRange <= 0)
            return BattleDirectionalPiercingPlan.Denied("当前弓没有可用射程。" );

        Vector2I direction = new(Math.Sign(delta.X), Math.Sign(delta.Y));
        BattleCellState sourceCell = gridService.GetCellState(state, sourceCoord);
        if (sourceCell == null)
            return BattleDirectionalPiercingPlan.Denied("施术者所在格数据不可用。" );

        int sourceHeight = sourceCell.current_height;
        int lockedHeightSign = 0;
        Vector2I previousCoord = sourceCoord;
        Vector2I blockedBeforeCoord = new(-1, -1);
        var pathCoords = new List<Vector2I>();
        var targets = new List<BattleUnitState>();
        var skippedTargets = new List<BattleDirectionalPiercingSkippedTarget>();
        var seenUnitIds = new HashSet<StringName> { sourceUnit.unit_id };

        for (int distance = 1; distance <= effectiveRange; distance++)
        {
            Vector2I coord = sourceCoord + direction * distance;
            if (!gridService.IsInside(state, coord))
                break;
            BattleEdgeFaceState crossedEdge = gridService.GetEdgeFace(state, previousCoord, coord);
            if (crossedEdge?.feature_blocks_los == true)
            {
                blockedBeforeCoord = coord;
                break;
            }
            pathCoords.Add(coord);
            previousCoord = coord;

            BattleUnitState targetUnit = gridService.GetUnitAtCoord(state, coord);
            if (
                targetUnit == null
                || !targetUnit.IsAlive()
                || !seenUnitIds.Add(targetUnit.unit_id)
            )
            {
                continue;
            }

            BattleCellState firstIntersectedCell = gridService.GetCellState(state, coord);
            int heightDelta = (firstIntersectedCell?.current_height ?? sourceHeight) - sourceHeight;
            if (Math.Abs(heightDelta) > profile.MaximumHeightDelta)
            {
                skippedTargets.Add(
                    new BattleDirectionalPiercingSkippedTarget(
                        targetUnit.unit_id,
                        heightDelta,
                        BattleDirectionalPiercingSkipReason.HeightOutOfRange
                    )
                );
                continue;
            }

            int targetHeightSign = Math.Sign(heightDelta);
            if (
                lockedHeightSign != 0
                && targetHeightSign != 0
                && targetHeightSign != lockedHeightSign
            )
            {
                skippedTargets.Add(
                    new BattleDirectionalPiercingSkippedTarget(
                        targetUnit.unit_id,
                        heightDelta,
                        BattleDirectionalPiercingSkipReason.OppositeHeightChannel
                    )
                );
                continue;
            }
            if (lockedHeightSign == 0 && targetHeightSign != 0)
                lockedHeightSign = targetHeightSign;
            targets.Add(targetUnit);
        }

        if (targets.Count == 0)
            return BattleDirectionalPiercingPlan.Denied("该方向的有效射程内没有可攻击单位。" );
        return new BattleDirectionalPiercingPlan
        {
            Allowed = true,
            Message = "可施放。",
            FullRange = effectiveRange,
            SourceHeight = sourceHeight,
            LockedHeightSign = lockedHeightSign,
            Direction = direction,
            BlockedBeforeCoord = blockedBeforeCoord,
            PathCoords = pathCoords.AsReadOnly(),
            Targets = targets.AsReadOnly(),
            SkippedTargets = skippedTargets.AsReadOnly(),
        };
    }

    private static long DivideRoundUp(long numerator, long denominator)
    {
        if (numerator <= 0 || denominator <= 0)
            return 0;
        return 1L + (numerator - 1L) / denominator;
    }

    private static long SaturatingAdd(long first, long second)
    {
        if (first >= long.MaxValue - second)
            return long.MaxValue;
        return first + second;
    }

    private static long SaturatingMultiply(long first, long second)
    {
        if (first <= 0 || second <= 0)
            return 0;
        if (first > long.MaxValue / second)
            return long.MaxValue;
        return first * second;
    }
}
