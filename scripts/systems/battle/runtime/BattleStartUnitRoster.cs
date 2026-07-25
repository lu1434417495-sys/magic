using System;
using System.Collections.Generic;

internal readonly record struct BattleStartUnitRosterMaterialization(
    bool ProvidesAllyUnits,
    List<BattleUnitState> AllyUnits,
    bool ProvidesEnemyUnits,
    List<BattleUnitState> EnemyUnits
);

/// <summary>
/// One-shot ownership transfer for fresh mutable battle units. The caller must
/// not retain or mutate unit graphs after constructing this roster.
/// </summary>
internal sealed class BattleStartUnitRoster
{
    private List<BattleUnitState> _allyUnits;
    private List<BattleUnitState> _enemyUnits;
    private bool _consumed;

    internal BattleStartUnitRoster(
        IReadOnlyList<BattleUnitState> allyUnits = null,
        IReadOnlyList<BattleUnitState> enemyUnits = null
    )
    {
        if (allyUnits == null && enemyUnits == null)
        {
            throw new ArgumentException(
                "A typed battle start roster must provide at least one side."
            );
        }

        ProvidesAllyUnits = allyUnits != null;
        ProvidesEnemyUnits = enemyUnits != null;
        _allyUnits = CopyOwnedUnits(allyUnits, "ally");
        _enemyUnits = CopyOwnedUnits(enemyUnits, "enemy");
    }

    internal bool ProvidesAllyUnits { get; }
    internal bool ProvidesEnemyUnits { get; }

    internal BattleStartUnitRosterMaterialization ConsumeForStart()
    {
        if (_consumed)
        {
            throw new InvalidOperationException(
                "A typed battle start roster can only be consumed once."
            );
        }

        _consumed = true;
        var result = new BattleStartUnitRosterMaterialization(
            ProvidesAllyUnits,
            _allyUnits,
            ProvidesEnemyUnits,
            _enemyUnits
        );
        _allyUnits = null;
        _enemyUnits = null;
        return result;
    }

    private static List<BattleUnitState> CopyOwnedUnits(
        IReadOnlyList<BattleUnitState> source,
        string side
    )
    {
        if (source == null)
            return null;

        var result = new List<BattleUnitState>(source.Count);
        for (int index = 0; index < source.Count; index++)
        {
            BattleUnitState unit = source[index];
            if (unit == null)
            {
                throw new ArgumentException(
                    $"Typed battle start {side} roster cannot contain null at index {index}.",
                    nameof(source)
                );
            }
            result.Add(unit);
        }
        return result;
    }
}
