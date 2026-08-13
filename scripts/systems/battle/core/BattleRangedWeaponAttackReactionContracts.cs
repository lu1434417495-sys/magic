using System;
using Godot;

internal readonly record struct BattleRangedWeaponAttackSnapshot(
    StringName WeaponFamily,
    StringName WeaponRangeType,
    int BaseDiceCount,
    int DiceSides,
    int MainWeaponDiceMultiplier
)
{
    internal static BattleRangedWeaponAttackSnapshot Empty => new("", "", 0, 0, 0);

    internal bool IsUsable =>
        WeaponRangeType == new StringName("ranged")
        && WeaponFamily != ""
        && BaseDiceCount > 0
        && DiceSides > 0
        && MainWeaponDiceMultiplier > 0;

    internal int TotalDiceCount =>
        (int)Math.Clamp(
            (long)Math.Max(BaseDiceCount, 0) * Math.Max(MainWeaponDiceMultiplier, 0),
            0L,
            int.MaxValue
        );
}

internal sealed class BattleRangedWeaponAttackReactionContext
{
    internal BattleUnitState Attacker { get; init; }
    internal BattleUnitState Defender { get; init; }
    internal BattleState BattleState { get; init; }
    internal StringName TriggeringSkillId { get; init; } = "";
    internal bool TriggeringAttackSucceeded { get; init; }
    internal BattleRangedWeaponAttackSnapshot WeaponSnapshot { get; init; }
    internal BattleEventBatch Batch { get; init; }
}

internal interface IBattleRangedWeaponAttackReactionSink
{
    void ResolveRangedWeaponAttackReaction(
        BattleRangedWeaponAttackReactionContext context
    );
}
