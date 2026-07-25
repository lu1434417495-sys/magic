using System.Collections;
using System.Collections.Generic;
using Godot;

internal sealed class BattleTemporalProgressModifierState
{
    internal StringName ModifierId { get; init; } = "";
    internal StringName BindingId { get; init; } = "";
    internal StringName SourceEquipmentInstanceId { get; init; } = "";
    internal bool AppliesToActionProgress { get; init; }
    internal bool AppliesToCastProgress { get; init; }
    internal int SaveDc { get; init; }
    internal StringName AttributeModifierId { get; init; } = "";
    internal int SuccessRatePercent { get; init; }
    internal int FailureRatePercent { get; init; }
    internal string Label { get; init; } = "";

    internal BattleTemporalProgressModifierState DuplicateState()
    {
        return new BattleTemporalProgressModifierState
        {
            ModifierId = ModifierId,
            BindingId = BindingId,
            SourceEquipmentInstanceId = SourceEquipmentInstanceId,
            AppliesToActionProgress = AppliesToActionProgress,
            AppliesToCastProgress = AppliesToCastProgress,
            SaveDc = SaveDc,
            AttributeModifierId = AttributeModifierId,
            SuccessRatePercent = SuccessRatePercent,
            FailureRatePercent = FailureRatePercent,
            Label = Label,
        };
    }
}

internal sealed class BattleTemporalProgressModifierReadView
{
    internal BattleTemporalProgressModifierReadView(
        BattleTemporalProgressModifierState modifier
    )
    {
        ModifierId = modifier.ModifierId;
        BindingId = modifier.BindingId;
        SourceEquipmentInstanceId =
            modifier.SourceEquipmentInstanceId;
        AppliesToActionProgress =
            modifier.AppliesToActionProgress;
        AppliesToCastProgress =
            modifier.AppliesToCastProgress;
        SaveDc = modifier.SaveDc;
        AttributeModifierId = modifier.AttributeModifierId;
        SuccessRatePercent = modifier.SuccessRatePercent;
        FailureRatePercent = modifier.FailureRatePercent;
        Label = modifier.Label;
    }

    internal StringName ModifierId { get; }
    internal StringName BindingId { get; }
    internal StringName SourceEquipmentInstanceId { get; }
    internal bool AppliesToActionProgress { get; }
    internal bool AppliesToCastProgress { get; }
    internal int SaveDc { get; }
    internal StringName AttributeModifierId { get; }
    internal int SuccessRatePercent { get; }
    internal int FailureRatePercent { get; }
    internal string Label { get; }
}

internal readonly struct
    BattleTemporalProgressModifierListReadView :
        IReadOnlyList<BattleTemporalProgressModifierReadView>
{
    private static readonly
        List<BattleTemporalProgressModifierReadView> Empty =
            new();
    private readonly
        List<BattleTemporalProgressModifierReadView> _values;

    internal BattleTemporalProgressModifierListReadView(
        List<BattleTemporalProgressModifierReadView> values
    )
    {
        _values = values;
    }

    internal bool IsPresent => _values != null;

    private List<BattleTemporalProgressModifierReadView>
        Values =>
            _values ?? Empty;

    public int Count => Values.Count;

    public BattleTemporalProgressModifierReadView this[
        int index
    ] =>
        Values[index];

    public List<BattleTemporalProgressModifierReadView>
        .Enumerator GetEnumerator() =>
            Values.GetEnumerator();

    IEnumerator<BattleTemporalProgressModifierReadView>
        IEnumerable<BattleTemporalProgressModifierReadView>
            .GetEnumerator() =>
                GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() =>
        GetEnumerator();
}
