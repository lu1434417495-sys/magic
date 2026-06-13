using Godot;

public partial class PendingCharacterRewardEntry : RefCounted
{
    private static readonly Godot.Collections.Array<string> TO_DICT_FIELDS = new()
    {
        "entry_type",
        "target_id",
        "target_label",
        "amount",
        "reason_text",
    };

    public StringName entry_type = "";

    internal PendingCharacterRewardEntryKind EntryKind
    {
        get => PendingCharacterRewardContentRules.ToEntryKind(entry_type);
        set => entry_type = PendingCharacterRewardContentRules.ToStringName(value);
    }

    public StringName target_id = "";

    public string target_label = "";

    public int amount;

    public string reason_text = "";

    public bool IsEmpty() =>
        EntryKind == PendingCharacterRewardEntryKind.Unknown || target_id == "" || amount == 0;

    public PendingCharacterRewardEntry DuplicateState()
    {
        return new PendingCharacterRewardEntry
        {
            entry_type = entry_type,
            target_id = target_id,
            target_label = target_label,
            amount = amount,
            reason_text = reason_text,
        };
    }

    public Godot.Collections.Dictionary ToDictionary()
    {
        return new Godot.Collections.Dictionary
        {
            { "entry_type", (string)entry_type },
            { "target_id", (string)target_id },
            { "target_label", target_label },
            { "amount", amount },
            { "reason_text", reason_text },
        };
    }

    public static PendingCharacterRewardEntry FromDictionary(Godot.Collections.Dictionary data)
    {
        if (!_has_exact_fields(data, TO_DICT_FIELDS))
            return null;

        var entryType = _parse_string_name_field(data, "entry_type", false, out bool ok1);

        var targetId = _parse_string_name_field(data, "target_id", false, out bool ok2);

        if (!ok1 || !ok2)
            return null;

        if (
            PendingCharacterRewardContentRules.ToEntryKind(entryType)
            == PendingCharacterRewardEntryKind.Unknown
        )
            return null;

        if (
            data["target_label"].VariantType != Variant.Type.String
            || data["reason_text"].VariantType != Variant.Type.String
        )
            return null;

        var amountVar = data["amount"];

        if (amountVar.VariantType != Variant.Type.Int || amountVar.AsInt32() == 0)
            return null;

        return new PendingCharacterRewardEntry
        {
            entry_type = entryType,
            target_id = targetId,
            target_label = data["target_label"].AsString(),
            amount = amountVar.AsInt32(),
            reason_text = data["reason_text"].AsString(),
        };
    }

    private static StringName _parse_string_name_field(
        Godot.Collections.Dictionary data,
        string fieldName,
        bool allowEmpty,
        out bool ok
    )
    {
        ok = false;
        var value = data[fieldName];

        if (
            value.VariantType != Variant.Type.String
            && value.VariantType != Variant.Type.StringName
        )
            return new StringName("");

        var parsed = ProgressionDataUtils.to_string_name(value);

        if (parsed == "" && !allowEmpty)
            return new StringName("");

        ok = true;

        return parsed;
    }

    private static bool _has_exact_fields(
        Godot.Collections.Dictionary data,
        Godot.Collections.Array<string> expectedFields
    )
    {
        if (data.Count != expectedFields.Count)
            return false;

        foreach (string fieldName in expectedFields)
        {
            if (!data.ContainsKey(fieldName))
                return false;
        }

        return true;
    }

}
