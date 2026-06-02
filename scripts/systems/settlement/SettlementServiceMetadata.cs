using Godot;
using GDictionary = Godot.Collections.Dictionary;

public sealed class SettlementServiceMetadata
{
    private static readonly string[] ReservedFields =
    {
        "cost_label",
        "is_enabled",
        "disabled_reason",
    };

    private readonly GDictionary _extraFields;

    public string CostLabel { get; }
    public bool IsEnabled { get; }
    public string DisabledReason { get; }

    public SettlementServiceMetadata(
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

    public GDictionary ToDictionary()
    {
        var result = new GDictionary
        {
            ["cost_label"] = CostLabel,
            ["is_enabled"] = IsEnabled,
            ["disabled_reason"] = DisabledReason,
        };
        foreach (Variant keyValue in _extraFields.Keys)
        {
            string key = keyValue.ToString();
            if (string.IsNullOrEmpty(key) || IsReservedField(key))
            {
                continue;
            }
            result[keyValue] = _extraFields[keyValue];
        }
        return result;
    }

    private static bool IsReservedField(string key)
    {
        foreach (string field in ReservedFields)
        {
            if (field == key)
            {
                return true;
            }
        }
        return false;
    }
}
