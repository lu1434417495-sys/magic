using System;
using System.Collections.Generic;
using Godot;

internal readonly record struct
    BattleUnitEquipmentAbilityProjectionReadView(
        bool OwnerPresent,
        BattleEquipmentAbilitySourceListReadView Sources,
        BattleTemporalProgressModifierListReadView
            TemporalProgressModifiers,
        BattleCognitionCeilingModifierListReadView
            CognitionCeilingModifiers
    )
{
    internal static
        BattleUnitEquipmentAbilityProjectionReadView MissingOwner =>
            new(
                false,
                new BattleEquipmentAbilitySourceListReadView(
                    null
                ),
                new BattleTemporalProgressModifierListReadView(
                    null
                ),
                new BattleCognitionCeilingModifierListReadView(
                    null
                )
            );
}

internal readonly record struct
    BattleUnitEquipmentAbilityProjectionSnapshot(
        bool OwnerPresent,
        List<BattleEquipmentAbilitySourceState> Sources,
        List<BattleTemporalProgressModifierState>
            TemporalProgressModifiers,
        List<BattleCognitionCeilingModifierState>
            CognitionCeilingModifiers
    )
{
    internal static BattleUnitEquipmentAbilityProjectionSnapshot
        Present(
            List<BattleEquipmentAbilitySourceState> sources,
            List<BattleTemporalProgressModifierState>
                temporalProgressModifiers,
            List<BattleCognitionCeilingModifierState>
                cognitionCeilingModifiers
        ) =>
            new(
                true,
                sources,
                temporalProgressModifiers,
                cognitionCeilingModifiers
            );

    internal static BattleUnitEquipmentAbilityProjectionSnapshot
        MissingOwner =>
            new(false, null, null, null);
}

internal sealed class BattleUnitEquipmentAbilityProjectionState
{
    private List<BattleEquipmentAbilitySourceState> _sources =
        new();
    private List<BattleTemporalProgressModifierState>
        _temporalProgressModifiers = new();
    private List<BattleCognitionCeilingModifierState>
        _cognitionCeilingModifiers = new();
    private List<BattleEquipmentAbilitySourceReadView>
        _sourceReadViews = new();
    private List<BattleTemporalProgressModifierReadView>
        _temporalProgressModifierReadViews = new();
    private List<BattleCognitionCeilingModifierReadView>
        _cognitionCeilingModifierReadViews = new();
    private BattleTemporalProgressModifierReadView
        _selectedActionProgressModifier;
    private BattleTemporalProgressModifierReadView
        _selectedCastProgressModifier;

    internal BattleUnitEquipmentAbilityProjectionReadView
        GetReadView() =>
            new(
                true,
                new BattleEquipmentAbilitySourceListReadView(
                    _sources == null ? null : _sourceReadViews
                ),
                new BattleTemporalProgressModifierListReadView(
                    _temporalProgressModifiers == null
                        ? null
                        : _temporalProgressModifierReadViews
                ),
                new BattleCognitionCeilingModifierListReadView(
                    _cognitionCeilingModifiers == null
                        ? null
                        : _cognitionCeilingModifierReadViews
                )
            );

    internal BattleTemporalProgressModifierReadView
        GetSelectedTemporalProgressModifier(
            bool actionProgress
        ) =>
            actionProgress
                ? _selectedActionProgressModifier
                : _selectedCastProgressModifier;

    internal void ReplaceNormalized(
        IEnumerable<BattleEquipmentAbilitySourceState> sources,
        IEnumerable<BattleTemporalProgressModifierState>
            temporalProgressModifiers,
        IEnumerable<BattleCognitionCeilingModifierState>
            cognitionCeilingModifiers = null
    )
    {
        List<BattleEquipmentAbilitySourceState>
            normalizedSources =
                DuplicateSourcesNormalized(sources);
        List<BattleTemporalProgressModifierState>
            normalizedTemporalProgressModifiers =
                DuplicateTemporalProgressModifiersNormalized(
                    temporalProgressModifiers
                );
        List<BattleCognitionCeilingModifierState>
            normalizedCognitionCeilingModifiers =
                DuplicateCognitionCeilingModifiersNormalized(
                    cognitionCeilingModifiers
                );
        ProjectionReadCache readCache = BuildReadCache(
            normalizedSources,
            normalizedTemporalProgressModifiers,
            normalizedCognitionCeilingModifiers
        );

        _sources = normalizedSources;
        _temporalProgressModifiers =
            normalizedTemporalProgressModifiers;
        _cognitionCeilingModifiers =
            normalizedCognitionCeilingModifiers;
        ApplyReadCache(readCache);
    }

    internal BattleUnitEquipmentAbilityProjectionSnapshot
        CaptureRaw() =>
            BattleUnitEquipmentAbilityProjectionSnapshot.Present(
                DuplicateSourcesExact(_sources),
                DuplicateTemporalProgressModifiersExact(
                    _temporalProgressModifiers
                ),
                DuplicateCognitionCeilingModifiersExact(
                    _cognitionCeilingModifiers
                )
            );

    internal BattleUnitEquipmentAbilityProjectionSeed
        CaptureNormalizedSeed() =>
            BattleUnitEquipmentAbilityProjectionSeed
                .CreateNormalized(
                    _sources,
                    _temporalProgressModifiers,
                    _cognitionCeilingModifiers
                );

    internal void RestoreRaw(
        BattleUnitEquipmentAbilityProjectionSnapshot snapshot
    )
    {
        List<BattleEquipmentAbilitySourceState> exactSources =
            DuplicateSourcesExact(snapshot.Sources);
        List<BattleTemporalProgressModifierState>
            exactTemporalProgressModifiers =
                DuplicateTemporalProgressModifiersExact(
                    snapshot.TemporalProgressModifiers
                );
        List<BattleCognitionCeilingModifierState>
            exactCognitionCeilingModifiers =
                DuplicateCognitionCeilingModifiersExact(
                    snapshot.CognitionCeilingModifiers
                );
        ProjectionReadCache readCache = BuildReadCache(
            exactSources,
            exactTemporalProgressModifiers,
            exactCognitionCeilingModifiers
        );

        _sources = exactSources;
        _temporalProgressModifiers =
            exactTemporalProgressModifiers;
        _cognitionCeilingModifiers =
            exactCognitionCeilingModifiers;
        ApplyReadCache(readCache);
    }

    internal BattleUnitEquipmentAbilityProjectionState
        DuplicateState()
    {
        var result =
            new BattleUnitEquipmentAbilityProjectionState();
        result.ReplaceNormalized(
            _sources,
            _temporalProgressModifiers,
            _cognitionCeilingModifiers
        );
        return result;
    }

    internal static
        BattleUnitEquipmentAbilityProjectionState
            FromSourcesNormalized(
                IEnumerable<
                    BattleEquipmentAbilitySourceState
                > sources
            )
    {
        var result =
            new BattleUnitEquipmentAbilityProjectionState();
        result.ReplaceNormalized(sources, null);
        return result;
    }

    private void ApplyReadCache(
        ProjectionReadCache readCache
    )
    {
        _sourceReadViews = readCache.SourceReadViews;
        _temporalProgressModifierReadViews =
            readCache.TemporalProgressModifierReadViews;
        _cognitionCeilingModifierReadViews =
            readCache.CognitionCeilingModifierReadViews;
        _selectedActionProgressModifier =
            readCache.SelectedActionProgressModifier;
        _selectedCastProgressModifier =
            readCache.SelectedCastProgressModifier;
    }

    private static ProjectionReadCache BuildReadCache(
        List<BattleEquipmentAbilitySourceState> sources,
        List<BattleTemporalProgressModifierState>
            temporalProgressModifiers,
        List<BattleCognitionCeilingModifierState>
            cognitionCeilingModifiers
    )
    {
        List<BattleEquipmentAbilitySourceReadView>
            sourceReadViews =
                BuildSourceReadViews(sources);
        List<BattleTemporalProgressModifierReadView>
            temporalProgressModifierReadViews =
                BuildTemporalProgressModifierReadViews(
                    temporalProgressModifiers
                );
        List<BattleCognitionCeilingModifierReadView>
            cognitionCeilingModifierReadViews =
                BuildCognitionCeilingModifierReadViews(
                    cognitionCeilingModifiers
                );
        return new ProjectionReadCache(
            sourceReadViews,
            temporalProgressModifierReadViews,
            cognitionCeilingModifierReadViews,
            SelectTemporalProgressModifier(
                temporalProgressModifierReadViews,
                actionProgress: true
            ),
            SelectTemporalProgressModifier(
                temporalProgressModifierReadViews,
                actionProgress: false
            )
        );
    }

    private static List<BattleEquipmentAbilitySourceReadView>
        BuildSourceReadViews(
            IEnumerable<BattleEquipmentAbilitySourceState>
                sources
        )
    {
        if (sources == null)
            return null;

        var result =
            new List<BattleEquipmentAbilitySourceReadView>();
        foreach (
            BattleEquipmentAbilitySourceState source in sources
        )
        {
            result.Add(
                source == null
                    ? null
                    : new BattleEquipmentAbilitySourceReadView(
                        source
                    )
            );
        }
        return result;
    }

    private static
        List<BattleTemporalProgressModifierReadView>
            BuildTemporalProgressModifierReadViews(
                IEnumerable<
                    BattleTemporalProgressModifierState
                > temporalProgressModifiers
            )
    {
        if (temporalProgressModifiers == null)
            return null;

        var result =
            new List<
                BattleTemporalProgressModifierReadView
            >();
        foreach (
            BattleTemporalProgressModifierState modifier
            in temporalProgressModifiers
        )
        {
            result.Add(
                modifier == null
                    ? null
                    : new BattleTemporalProgressModifierReadView(
                        modifier
                    )
            );
        }
        return result;
    }

    private static BattleTemporalProgressModifierReadView
        SelectTemporalProgressModifier(
            IEnumerable<
                BattleTemporalProgressModifierReadView
            > modifiers,
            bool actionProgress
        )
    {
        BattleTemporalProgressModifierReadView selected =
            null;
        foreach (
            BattleTemporalProgressModifierReadView modifier
            in modifiers
                ?? Array.Empty<
                    BattleTemporalProgressModifierReadView
                >()
        )
        {
            if (modifier == null)
                continue;
            if (
                actionProgress
                    ? !modifier.AppliesToActionProgress
                    : !modifier.AppliesToCastProgress
            )
            {
                continue;
            }
            if (
                selected == null
                || string.CompareOrdinal(
                    modifier.ModifierId?.ToString() ?? "",
                    selected.ModifierId?.ToString() ?? ""
                ) < 0
            )
            {
                selected = modifier;
            }
        }
        return selected;
    }

    private static List<BattleCognitionCeilingModifierReadView>
        BuildCognitionCeilingModifierReadViews(
            IEnumerable<BattleCognitionCeilingModifierState> modifiers
        )
    {
        if (modifiers == null)
            return null;
        var result =
            new List<BattleCognitionCeilingModifierReadView>();
        foreach (BattleCognitionCeilingModifierState modifier in modifiers)
        {
            result.Add(
                modifier == null
                    ? null
                    : new BattleCognitionCeilingModifierReadView(
                        modifier
                    )
            );
        }
        return result;
    }

    private static List<BattleEquipmentAbilitySourceState>
        DuplicateSourcesNormalized(
            IEnumerable<BattleEquipmentAbilitySourceState>
                sources
        )
    {
        var result =
            new List<BattleEquipmentAbilitySourceState>();
        if (sources == null)
            return result;

        foreach (
            BattleEquipmentAbilitySourceState source in sources
        )
        {
            if (source != null)
                result.Add(source.DuplicateState());
        }
        return result;
    }

    private static
        List<BattleTemporalProgressModifierState>
            DuplicateTemporalProgressModifiersNormalized(
                IEnumerable<
                    BattleTemporalProgressModifierState
                > temporalProgressModifiers
            )
    {
        var result =
            new List<BattleTemporalProgressModifierState>();
        if (temporalProgressModifiers == null)
            return result;

        foreach (
            BattleTemporalProgressModifierState modifier
            in temporalProgressModifiers
        )
        {
            if (modifier != null)
                result.Add(modifier.DuplicateState());
        }
        return result;
    }

    private static List<BattleCognitionCeilingModifierState>
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
            if (
                modifier != null
                && modifier.ModifierId != ""
                && BattleCognitionContentRules.IsKnown(
                    modifier.Ceiling
                )
            )
            {
                result.Add(modifier.DuplicateState());
            }
        }
        return result;
    }

    private static List<BattleEquipmentAbilitySourceState>
        DuplicateSourcesExact(
            IEnumerable<BattleEquipmentAbilitySourceState>
                sources
        )
    {
        if (sources == null)
            return null;

        var result =
            new List<BattleEquipmentAbilitySourceState>();
        foreach (
            BattleEquipmentAbilitySourceState source in sources
        )
        {
            if (source == null)
            {
                result.Add(null);
                continue;
            }
            result.Add(
                new BattleEquipmentAbilitySourceState
                {
                    EffectiveInstanceKey =
                        source.EffectiveInstanceKey,
                    EquipmentDefId = source.EquipmentDefId,
                    SourceEquipmentInstanceId =
                        source.SourceEquipmentInstanceId,
                    SourceKind = source.SourceKind,
                    AbilityIds = source.AbilityIds == null
                        ? null
                        : new List<StringName>(
                            source.AbilityIds
                        ),
                }
            );
        }
        return result;
    }

    private static
        List<BattleTemporalProgressModifierState>
            DuplicateTemporalProgressModifiersExact(
                IEnumerable<
                    BattleTemporalProgressModifierState
                > temporalProgressModifiers
            )
    {
        if (temporalProgressModifiers == null)
            return null;

        var result =
            new List<BattleTemporalProgressModifierState>();
        foreach (
            BattleTemporalProgressModifierState modifier
            in temporalProgressModifiers
        )
        {
            result.Add(modifier?.DuplicateState());
        }
        return result;
    }

    private static List<BattleCognitionCeilingModifierState>
        DuplicateCognitionCeilingModifiersExact(
            IEnumerable<BattleCognitionCeilingModifierState> modifiers
        )
    {
        if (modifiers == null)
            return null;
        var result =
            new List<BattleCognitionCeilingModifierState>();
        foreach (BattleCognitionCeilingModifierState modifier in modifiers)
            result.Add(modifier?.DuplicateState());
        return result;
    }

    private readonly record struct ProjectionReadCache(
        List<BattleEquipmentAbilitySourceReadView>
            SourceReadViews,
        List<BattleTemporalProgressModifierReadView>
            TemporalProgressModifierReadViews,
        List<BattleCognitionCeilingModifierReadView>
            CognitionCeilingModifierReadViews,
        BattleTemporalProgressModifierReadView
            SelectedActionProgressModifier,
        BattleTemporalProgressModifierReadView
            SelectedCastProgressModifier
    );
}
