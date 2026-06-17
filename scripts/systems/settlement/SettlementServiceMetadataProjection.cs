using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal static class SettlementServiceMetadataProjection
{
    private static readonly string[] ReservedFields =
    {
        "cost_label",
        "is_enabled",
        "disabled_reason",
    };

    internal static void ApplyToServiceData(
        GDictionary serviceData,
        SettlementServiceMetadata metadata
    )
    {
        if (serviceData == null || metadata == null)
            return;

        serviceData["cost_label"] = metadata.CostLabel.Trim();
        serviceData["is_enabled"] = metadata.IsEnabled;
        serviceData["disabled_reason"] = metadata.DisabledReason.Trim();

        GDictionary extraFields = metadata.CopyExtraFields();
        foreach (Variant keyValue in extraFields.Keys)
        {
            string key = keyValue.ToString();
            if (string.IsNullOrEmpty(key) || IsReservedField(key))
                continue;
            serviceData[keyValue] = extraFields[keyValue];
        }
    }

    private static bool IsReservedField(string key)
    {
        foreach (string field in ReservedFields)
        {
            if (field == key)
                return true;
        }
        return false;
    }
}
