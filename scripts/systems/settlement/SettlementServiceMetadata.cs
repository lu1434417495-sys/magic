using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal sealed class SettlementServiceMetadata
{
    private readonly GDictionary _extraFields;

    public string CostLabel { get; }
    public bool IsEnabled { get; }
    public string DisabledReason { get; }

    internal SettlementServiceMetadata(
        string costLabel,
        bool isEnabled,
        string disabledReason = "",
        GDictionary extraFields = null
    )
    {
        CostLabel = costLabel ?? "";
        IsEnabled = isEnabled;
        DisabledReason = disabledReason ?? "";
        _extraFields = extraFields?.Duplicate(true) ?? new GDictionary();
    }

    internal GDictionary CopyExtraFields() =>
        _extraFields?.Duplicate(true) ?? new GDictionary();
}
