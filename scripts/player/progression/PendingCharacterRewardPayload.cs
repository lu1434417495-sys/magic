using Godot;
using System.Collections.Generic;
using GDictionary = Godot.Collections.Dictionary;

internal static class PendingCharacterRewardPayload
{
    private static readonly string[] RewardFields =
    {
        "reward_id",
        "member_id",
        "member_name",
        "source_type",
        "source_id",
        "source_label",
        "summary_text",
        "entries",
    };

    private static readonly string[] EntryFields =
    {
        "entry_type",
        "target_id",
        "target_label",
        "amount",
        "reason_text",
    };

    internal static GDictionary Project(PendingCharacterReward reward)
    {
        if (reward == null)
            return new GDictionary();

        var entryData = new Godot.Collections.Array<GDictionary>();

        foreach (var entry in reward.entries)
        {
            if (entry != null)
                entryData.Add(ProjectEntry(entry));
        }

        return new GDictionary
        {
            { "reward_id", (string)reward.reward_id },
            { "member_id", (string)reward.member_id },
            { "member_name", reward.member_name },
            { "source_type", (string)reward.source_type },
            { "source_id", (string)reward.source_id },
            { "source_label", reward.source_label },
            { "summary_text", reward.summary_text },
            { "entries", entryData },
        };
    }

    internal static PendingCharacterReward ReadSavePayload(GDictionary data)
    {
        if (!HasExactFields(data, RewardFields))
            return null;

        var rewardId = ParseStringNameField(data, "reward_id", false, out bool ok1);
        var memId = ParseStringNameField(data, "member_id", false, out bool ok2);
        var srcType = ParseStringNameField(data, "source_type", false, out bool ok3);
        var srcId = ParseStringNameField(data, "source_id", false, out bool ok4);

        if (!ok1 || !ok2 || !ok3 || !ok4)
            return null;

        foreach (string textField in new[] { "member_name", "source_label", "summary_text" })
            if (data[textField].VariantType != Variant.Type.String)
                return null;

        var entriesVar = data["entries"];

        if (entriesVar.VariantType != Variant.Type.Array)
            return null;

        var parsedEntries = new List<PendingCharacterRewardEntry>();

        foreach (var entryData in entriesVar.AsGodotArray())
        {
            if (entryData.VariantType != Variant.Type.Dictionary)
                return null;

            var parsed = ReadEntrySavePayload(entryData.AsGodotDictionary());

            if (parsed == null)
                return null;

            parsedEntries.Add(parsed);
        }

        if (parsedEntries.Count == 0)
            return null;

        return new PendingCharacterReward
        {
            reward_id = rewardId,
            member_id = memId,
            member_name = data["member_name"].AsString(),
            source_type = srcType,
            source_id = srcId,
            source_label = data["source_label"].AsString(),
            summary_text = data["summary_text"].AsString(),
            entries = parsedEntries,
        };
    }

    private static GDictionary ProjectEntry(PendingCharacterRewardEntry entry)
    {
        return new GDictionary
        {
            { "entry_type", (string)entry.entry_type },
            { "target_id", (string)entry.target_id },
            { "target_label", entry.target_label },
            { "amount", entry.amount },
            { "reason_text", entry.reason_text },
        };
    }

    private static PendingCharacterRewardEntry ReadEntrySavePayload(GDictionary data)
    {
        if (!HasExactFields(data, EntryFields))
            return null;

        var entryType = ParseStringNameField(data, "entry_type", false, out bool ok1);
        var targetId = ParseStringNameField(data, "target_id", false, out bool ok2);

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

    private static StringName ParseStringNameField(
        GDictionary data,
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

    private static bool HasExactFields(
        GDictionary data,
        IReadOnlyCollection<string> expectedFields
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
