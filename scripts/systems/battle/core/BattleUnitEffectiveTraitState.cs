using System.Collections;
using System.Collections.Generic;
using Godot;

internal readonly struct BattleEffectiveTraitRollValueReadView
{
    private readonly List<TraitRollValueState> _values;

    internal BattleEffectiveTraitRollValueReadView(
        List<TraitRollValueState> values
    )
    {
        _values = values;
    }

    internal int Count => _values?.Count ?? 0;

    internal List<TraitRollValueState> CopyNormalized() =>
        TraitInstanceState.NormalizeRollValues(_values);
}

internal readonly record struct BattleEffectiveTraitInstanceReadView(
    bool IsPresent,
    StringName TraitId,
    StringName EffectiveInstanceKey,
    StringName SourceType,
    StringName SourceId,
    StringName EffectType,
    StringName TriggerType,
    StringName ChargeScope,
    StringName ChargeResetTiming,
    int Rank,
    int Stacks,
    BattleEffectiveTraitRollValueReadView RollValues
)
{
    internal TraitEffectKind EffectKind =>
        TraitContentRules.ToEffectKind(EffectType);

    internal TraitTriggerKind TriggerKind =>
        TraitTriggerContentRules.ToTriggerKind(TriggerType);

    internal TraitChargeScopeKind ChargeScopeKind =>
        TraitContentRules.ToChargeScopeKind(ChargeScope);

    internal TraitChargeResetTimingKind ChargeResetTimingKind =>
        TraitContentRules.ToChargeResetTimingKind(ChargeResetTiming);

    internal static BattleEffectiveTraitInstanceReadView Missing =>
        new(
            false,
            new StringName(""),
            new StringName(""),
            new StringName(""),
            new StringName(""),
            new StringName(""),
            new StringName(""),
            new StringName(""),
            new StringName(""),
            0,
            0,
            new BattleEffectiveTraitRollValueReadView(null)
        );

    internal static BattleEffectiveTraitInstanceReadView FromState(
        BattleEffectiveTraitInstanceState state
    ) =>
        state == null
            ? Missing
            : new BattleEffectiveTraitInstanceReadView(
                true,
                state.trait_id,
                state.effective_instance_key,
                state.source_type,
                state.source_id,
                state.effect_type,
                state.trigger_type,
                state.charge_scope,
                state.charge_reset_timing,
                state.rank,
                state.stacks,
                new BattleEffectiveTraitRollValueReadView(
                    state.roll_values
                )
            );
}

internal readonly struct BattleEffectiveTraitInstanceListReadView :
    IReadOnlyList<BattleEffectiveTraitInstanceReadView>
{
    private static readonly List<BattleEffectiveTraitInstanceReadView> Empty =
        new();
    private readonly List<BattleEffectiveTraitInstanceReadView> _values;

    internal BattleEffectiveTraitInstanceListReadView(
        List<BattleEffectiveTraitInstanceReadView> values
    )
    {
        _values = values;
    }

    private List<BattleEffectiveTraitInstanceReadView> Values =>
        _values ?? Empty;

    internal bool IsPresent => _values != null;

    public int Count => Values.Count;

    public BattleEffectiveTraitInstanceReadView this[int index] =>
        Values[index];

    public List<BattleEffectiveTraitInstanceReadView>.Enumerator
        GetEnumerator() =>
            Values.GetEnumerator();

    IEnumerator<BattleEffectiveTraitInstanceReadView>
        IEnumerable<BattleEffectiveTraitInstanceReadView>.GetEnumerator() =>
            GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

internal readonly struct BattleEffectiveTraitIdReadView :
    IReadOnlyList<StringName>
{
    private static readonly StringNameList Empty = new();
    private readonly StringNameList _values;

    internal BattleEffectiveTraitIdReadView(StringNameList values)
    {
        _values = values;
    }

    private StringNameList Values => _values ?? Empty;

    internal bool IsPresent => _values != null;

    public int Count => Values.Count;

    public StringName this[int index] => Values[index];

    internal bool Contains(StringName traitId) =>
        Values.Contains(traitId);

    public List<StringName>.Enumerator GetEnumerator() =>
        Values.GetEnumerator();

    IEnumerator<StringName> IEnumerable<StringName>.GetEnumerator() =>
        GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

internal readonly record struct BattleUnitEffectiveTraitReadView(
    bool OwnerPresent,
    BattleEffectiveTraitInstanceListReadView Instances,
    BattleEffectiveTraitIdReadView TraitIds
)
{
    internal static BattleUnitEffectiveTraitReadView MissingOwner =>
        new(
            false,
            new BattleEffectiveTraitInstanceListReadView(null),
            new BattleEffectiveTraitIdReadView(null)
        );
}

internal readonly record struct BattleUnitEffectiveTraitSnapshot(
    bool OwnerPresent,
    List<BattleEffectiveTraitInstanceState> Instances,
    StringNameList TraitIds
)
{
    internal static BattleUnitEffectiveTraitSnapshot Present(
        List<BattleEffectiveTraitInstanceState> instances,
        StringNameList traitIds
    ) =>
        new(true, instances, traitIds);

    internal static BattleUnitEffectiveTraitSnapshot MissingOwner =>
        new(false, null, null);
}

internal sealed class BattleUnitEffectiveTraitState
{
    private List<BattleEffectiveTraitInstanceState> _instances = new();
    private StringNameList _traitIds = new();
    private StringNameList _derivedTraitIds = new();
    private List<BattleEffectiveTraitInstanceReadView> _instanceReadViews =
        new();

    internal BattleUnitEffectiveTraitReadView GetReadView() =>
        new(
            true,
            new BattleEffectiveTraitInstanceListReadView(
                _instances == null ? null : _instanceReadViews
            ),
            new BattleEffectiveTraitIdReadView(_traitIds)
        );

    internal BattleEffectiveTraitIdReadView
        GetDerivedTraitIdsReadView() =>
            new(_derivedTraitIds);

    internal int GetInstanceCount() => _instances?.Count ?? 0;

    internal bool ContainsTraitId(StringName traitId) =>
        !IsEmpty(traitId)
        && _traitIds?.Contains(traitId) == true;

    internal void ReplaceNormalized(
        IEnumerable<BattleEffectiveTraitInstanceState> instances
    )
    {
        _instances = DuplicateInstancesNormalized(instances);
        _traitIds = DeriveTraitIds(_instances);
        RebuildReadViews();
    }

    internal List<BattleEffectiveTraitInstanceState>
        CopyInstancesNormalized() =>
            DuplicateInstancesNormalized(_instances);

    internal BattleUnitEffectiveTraitSnapshot CaptureRaw() =>
        BattleUnitEffectiveTraitSnapshot.Present(
            DuplicateInstancesExact(_instances),
            _traitIds?.Duplicate()
        );

    internal void RestoreRaw(
        BattleUnitEffectiveTraitSnapshot snapshot
    )
    {
        _instances = DuplicateInstancesExact(snapshot.Instances);
        _traitIds = snapshot.TraitIds?.Duplicate();
        RebuildReadViews();
    }

    internal BattleUnitEffectiveTraitState DuplicateState()
    {
        var result = new BattleUnitEffectiveTraitState();
        result.ReplaceNormalized(_instances);
        return result;
    }

    internal static BattleUnitEffectiveTraitState FromRaw(
        BattleUnitEffectiveTraitSnapshot snapshot
    )
    {
        var result = new BattleUnitEffectiveTraitState();
        result.RestoreRaw(snapshot);
        return result;
    }

    internal static List<BattleEffectiveTraitInstanceState>
        DuplicateInstancesNormalized(
            IEnumerable<BattleEffectiveTraitInstanceState> source
        )
    {
        var result = new List<BattleEffectiveTraitInstanceState>();
        if (source == null)
            return result;

        foreach (BattleEffectiveTraitInstanceState entry in source)
        {
            if (entry != null)
                result.Add(entry.DuplicateState());
        }
        return result;
    }

    internal static List<BattleEffectiveTraitInstanceState>
        DuplicateInstancesExact(
            IEnumerable<BattleEffectiveTraitInstanceState> source
        )
    {
        if (source == null)
            return null;

        var result = new List<BattleEffectiveTraitInstanceState>();
        foreach (BattleEffectiveTraitInstanceState entry in source)
            result.Add(entry?.DuplicateForMutationSnapshotExact());
        return result;
    }

    internal static StringNameList DeriveTraitIds(
        IEnumerable<BattleEffectiveTraitInstanceState> source
    )
    {
        var values = new List<StringName>();
        if (source != null)
        {
            foreach (BattleEffectiveTraitInstanceState entry in source)
            {
                if (
                    entry == null
                    || IsEmpty(entry.trait_id)
                    || values.Contains(entry.trait_id)
                )
                {
                    continue;
                }
                values.Add(entry.trait_id);
            }
        }
        values.Sort(
            (left, right) =>
                string.CompareOrdinal(
                    left.ToString(),
                    right.ToString()
                )
        );

        var result = new StringNameList();
        foreach (StringName value in values)
            result.Add(value);
        return result;
    }

    private void RebuildReadViews()
    {
        _instanceReadViews = new List<BattleEffectiveTraitInstanceReadView>();
        _derivedTraitIds = DeriveTraitIds(_instances);
        if (_instances == null)
            return;

        foreach (BattleEffectiveTraitInstanceState instance in _instances)
        {
            _instanceReadViews.Add(
                BattleEffectiveTraitInstanceReadView.FromState(instance)
            );
        }
    }

    private static bool IsEmpty(StringName value) =>
        value == null || string.IsNullOrEmpty(value.ToString());
}
