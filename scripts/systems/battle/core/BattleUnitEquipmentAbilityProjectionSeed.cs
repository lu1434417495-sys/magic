using System;
using System.Collections.Generic;

/// <summary>
/// Normalized, owned equipment-ability projection data that may cross an
/// in-process runtime-definition boundary without carrying mutation-exact
/// diagnostic state.
/// </summary>
internal sealed class BattleUnitEquipmentAbilityProjectionSeed
{
    private readonly BattleEquipmentAbilitySourceState[] _sources;
    private readonly BattleTemporalProgressModifierState[] _temporalProgressModifiers;
    private readonly BattleCognitionCeilingModifierState[]
        _cognitionCeilingModifiers;

    private BattleUnitEquipmentAbilityProjectionSeed(
        IEnumerable<BattleEquipmentAbilitySourceState> sources,
        IEnumerable<BattleTemporalProgressModifierState> temporalProgressModifiers,
        IEnumerable<BattleCognitionCeilingModifierState>
            cognitionCeilingModifiers
    )
    {
        _sources = DuplicateSourcesNormalized(sources);
        _temporalProgressModifiers = DuplicateTemporalProgressModifiersNormalized(
            temporalProgressModifiers
        );
        _cognitionCeilingModifiers =
            DuplicateCognitionCeilingModifiersNormalized(
                cognitionCeilingModifiers
            );
    }

    internal static BattleUnitEquipmentAbilityProjectionSeed Empty { get; } =
        new(null, null, null);

    internal static BattleUnitEquipmentAbilityProjectionSeed CreateNormalized(
        IEnumerable<BattleEquipmentAbilitySourceState> sources,
        IEnumerable<BattleTemporalProgressModifierState> temporalProgressModifiers,
        IEnumerable<BattleCognitionCeilingModifierState>
            cognitionCeilingModifiers = null
    ) => new(
        sources,
        temporalProgressModifiers,
        cognitionCeilingModifiers
    );

    internal BattleUnitEquipmentAbilityProjectionSeed DeepClone() =>
        new(
            _sources,
            _temporalProgressModifiers,
            _cognitionCeilingModifiers
        );

    internal void ApplyTo(BattleUnitState target)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.ReplaceEquipmentAbilityProjectionTyped(
            _sources,
            _temporalProgressModifiers,
            _cognitionCeilingModifiers
        );
    }

    private static BattleEquipmentAbilitySourceState[] DuplicateSourcesNormalized(
        IEnumerable<BattleEquipmentAbilitySourceState> sources
    )
    {
        var result = new List<BattleEquipmentAbilitySourceState>();
        foreach (
            BattleEquipmentAbilitySourceState source
            in sources ?? Array.Empty<BattleEquipmentAbilitySourceState>()
        )
        {
            if (source != null)
                result.Add(source.DuplicateState());
        }
        return result.ToArray();
    }

    private static BattleTemporalProgressModifierState[]
        DuplicateTemporalProgressModifiersNormalized(
            IEnumerable<BattleTemporalProgressModifierState> temporalProgressModifiers
        )
    {
        var result = new List<BattleTemporalProgressModifierState>();
        foreach (
            BattleTemporalProgressModifierState modifier
            in temporalProgressModifiers
                ?? Array.Empty<BattleTemporalProgressModifierState>()
        )
        {
            if (modifier != null)
                result.Add(modifier.DuplicateState());
        }
        return result.ToArray();
    }

    private static BattleCognitionCeilingModifierState[]
        DuplicateCognitionCeilingModifiersNormalized(
            IEnumerable<BattleCognitionCeilingModifierState> modifiers
        )
    {
        var result =
            new List<BattleCognitionCeilingModifierState>();
        foreach (
            BattleCognitionCeilingModifierState modifier
            in modifiers
                ?? Array.Empty<BattleCognitionCeilingModifierState>()
        )
        {
            if (modifier != null)
                result.Add(modifier.DuplicateState());
        }
        return result.ToArray();
    }
}
