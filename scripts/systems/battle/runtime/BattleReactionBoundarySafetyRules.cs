internal enum BattleReactionWorkItemKind
{
    LogicalAttack = 0,
    AttackFact,
    CounterattackDequeue,
}

internal interface IBattleReactionDrainOwner
{
    void Drain(BattleEventBatch batch);
    void AbortBoundary();
}

internal readonly record struct BattleReactionBoundaryLimits(
    int MaxNestedBoundaryDepth,
    int MaxWorkItems
);

internal static class BattleReactionBoundarySafetyRules
{
    internal static BattleReactionBoundaryLimits Production =>
        new(MaxNestedBoundaryDepth: 64, MaxWorkItems: 4096);
}
