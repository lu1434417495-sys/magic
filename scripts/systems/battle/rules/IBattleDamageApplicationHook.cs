using Godot;

internal interface IBattleDamageApplicationHook
{
    BattleDamageApplicationHookResult BeforeDamageResolved(
        BattleDamageApplicationHookContext context
    );
}

internal sealed class BattleDamageApplicationHookContext
{
    internal BattleUnitState SourceUnit { get; init; }
    internal BattleUnitState TargetUnit { get; init; }
    internal DamageApplicationInput DamageInput { get; init; }
    internal DamageApplicationProjection Projection { get; init; }
    internal BattleEventBatch Batch { get; init; }
    internal BattleEffectOrigin Origin { get; init; }
}

internal readonly record struct BattleDamageApplicationHookResult(
    bool CancelDamage,
    bool HasModifiedResolvedDamage,
    int ModifiedResolvedDamage,
    bool StateMayHaveChanged
)
{
    internal static BattleDamageApplicationHookResult None => new(false, false, 0, false);

    internal static BattleDamageApplicationHookResult Cancel() => new(true, false, 0, false);

    internal static BattleDamageApplicationHookResult ModifyResolvedDamage(int resolvedDamage) =>
        new(false, true, Mathf.Max(resolvedDamage, 0), false);

    internal static BattleDamageApplicationHookResult StateChanged() =>
        new(false, false, 0, true);
}
