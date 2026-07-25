using System;
using System.Collections.Generic;
using Godot;

internal readonly record struct
    BattleUnitEquipmentAbilityProjectionReadView(
        bool OwnerPresent,
        BattleEquipmentAbilitySourceListReadView Sources,
        BattleTemporalProgressModifierListReadView
            TemporalProgressModifiers
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
                )
            );
}

internal readonly record struct
    BattleUnitEquipmentAbilityProjectionSnapshot(
        bool OwnerPresent,
        List<BattleEquipmentAbilitySourceState> Sources,
        List<BattleTemporalProgressModifierState>
            TemporalProgressModifiers
    )
{
    internal static BattleUnitEquipmentAbilityProjectionSnapshot
        Present(
            List<BattleEquipmentAbilitySourceState> sources,
            List<BattleTemporalProgressModifierState>
                temporalProgressModifiers
        ) =>
            new(true, sources, temporalProgressModifiers);

    internal static BattleUnitEquipmentAbilityProjectionSnapshot
        MissingOwner =>
            new(false, null, null);
}

internal sealed class BattleUnitEquipmentAbilityProjectionState
{
    private List<BattleEquipmentAbilitySourceState> _sources =
        new();
    private List<BattleTemporalProgressModifierState>
        _temporalProgressModifiers = new();
    private List<BattleEquipmentAbilitySourceReadView>
        _sourceReadViews = new();
    private List<BattleTemporalProgressModifierReadView>
        _temporalProgressModifierReadViews = new();
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
            temporalProgressModifiers
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
        ProjectionReadCache readCache = BuildReadCache(
            normalizedSources,
            normalizedTemporalProgressModifiers
        );

        _sources = normalizedSources;
        _temporalProgressModifiers =
            normalizedTemporalProgressModifiers;
        ApplyReadCache(readCache);
    }

    internal BattleUnitEquipmentAbilityProjectionSnapshot
        CaptureRaw() =>
            BattleUnitEquipmentAbilityProjectionSnapshot.Present(
                DuplicateSourcesExact(_sources),
                DuplicateTemporalProgressModifiersExact(
                    _temporalProgressModifiers
                )
            );

    internal BattleUnitEquipmentAbilityProjectionSeed
        CaptureNormalizedSeed() =>
            BattleUnitEquipmentAbilityProjectionSeed
                .CreateNormalized(
                    _sources,
                    _temporalProgressModifiers
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
        ProjectionReadCache readCache = BuildReadCache(
            exactSources,
            exactTemporalProgressModifiers
        );

        _sources = exactSources;
        _temporalProgressModifiers =
            exactTemporalProgressModifiers;
        ApplyReadCache(readCache);
    }

    internal BattleUnitEquipmentAbilityProjectionState
        DuplicateState()
    {
        var result =
            new BattleUnitEquipmentAbilityProjectionState();
        result.ReplaceNormalized(
            _sources,
            _temporalProgressModifiers
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
        _selectedActionProgressModifier =
            readCache.SelectedActionProgressModifier;
        _selectedCastProgressModifier =
            readCache.SelectedCastProgressModifier;
    }

    private static ProjectionReadCache BuildReadCache(
        List<BattleEquipmentAbilitySourceState> sources,
        List<BattleTemporalProgressModifierState>
            temporalProgressModifiers
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
        return new ProjectionReadCache(
            sourceReadViews,
            temporalProgressModifierReadViews,
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

    private readonly record struct ProjectionReadCache(
        List<BattleEquipmentAbilitySourceReadView>
            SourceReadViews,
        List<BattleTemporalProgressModifierReadView>
            TemporalProgressModifierReadViews,
        BattleTemporalProgressModifierReadView
            SelectedActionProgressModifier,
        BattleTemporalProgressModifierReadView
            SelectedCastProgressModifier
    );
}
