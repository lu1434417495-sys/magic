using System.Collections;
using System.Collections.Generic;
using Godot;

internal sealed class BattleCognitionCeilingModifierState
{
    internal StringName ModifierId { get; init; } = "";
    internal StringName BindingId { get; init; } = "";
    internal StringName SourceEquipmentInstanceId { get; init; } = "";
    internal BattleCognitionKind Ceiling { get; init; } =
        BattleCognitionKind.Unknown;

    internal BattleCognitionCeilingModifierState DuplicateState() =>
        new()
        {
            ModifierId = ModifierId,
            BindingId = BindingId,
            SourceEquipmentInstanceId = SourceEquipmentInstanceId,
            Ceiling = Ceiling,
        };
}

internal sealed class BattleCognitionCeilingModifierReadView
{
    internal BattleCognitionCeilingModifierReadView(
        BattleCognitionCeilingModifierState modifier
    )
    {
        ModifierId = modifier?.ModifierId ?? "";
        BindingId = modifier?.BindingId ?? "";
        SourceEquipmentInstanceId =
            modifier?.SourceEquipmentInstanceId ?? "";
        Ceiling =
            modifier?.Ceiling ?? BattleCognitionKind.Unknown;
    }

    internal StringName ModifierId { get; }
    internal StringName BindingId { get; }
    internal StringName SourceEquipmentInstanceId { get; }
    internal BattleCognitionKind Ceiling { get; }
}

internal readonly struct BattleCognitionCeilingModifierListReadView :
    IReadOnlyList<BattleCognitionCeilingModifierReadView>
{
    private static readonly
        List<BattleCognitionCeilingModifierReadView> Empty = new();
    private readonly List<BattleCognitionCeilingModifierReadView>
        _values;

    internal BattleCognitionCeilingModifierListReadView(
        List<BattleCognitionCeilingModifierReadView> values
    )
    {
        _values = values;
    }

    private List<BattleCognitionCeilingModifierReadView> Values =>
        _values ?? Empty;

    internal bool IsPresent => _values != null;
    public int Count => Values.Count;
    public BattleCognitionCeilingModifierReadView this[int index] =>
        Values[index];
    public List<BattleCognitionCeilingModifierReadView>.Enumerator
        GetEnumerator() => Values.GetEnumerator();
    IEnumerator<BattleCognitionCeilingModifierReadView>
        IEnumerable<BattleCognitionCeilingModifierReadView>.GetEnumerator() =>
            GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
