using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public sealed record FaithRankRewardEntryDefinition(
    StringName EntryType,
    StringName TargetId,
    int Amount,
    string TargetLabel,
    string ReasonText
);

public sealed class FaithRankDefinition
{
    public FaithRankDefinition(
        int rankIndex,
        string rankName,
        int requiredGold,
        int requiredLevel,
        StringName requiredCustomStatId,
        int requiredCustomStatMinValue,
        StringName requiredAchievementId,
        IReadOnlyList<FaithRankRewardEntryDefinition> rewardEntries
    )
    {
        RankIndex = rankIndex;
        RankName = rankName
            ?? throw new InvalidDataException("FaithRankDefinition.RankName must not be null.");
        RequiredGold = requiredGold;
        RequiredLevel = requiredLevel;
        RequiredCustomStatId = requiredCustomStatId;
        RequiredCustomStatMinValue = requiredCustomStatMinValue;
        RequiredAchievementId = requiredAchievementId;
        RewardEntries = ProgressionDefinitionProjection.FreezeValues(
            rewardEntries,
            "FaithRankDefinition.RewardEntries"
        );
    }

    public int RankIndex { get; }
    public string RankName { get; }
    public int RequiredGold { get; }
    public int RequiredLevel { get; }
    public StringName RequiredCustomStatId { get; }
    public int RequiredCustomStatMinValue { get; }
    public StringName RequiredAchievementId { get; }
    public IReadOnlyList<FaithRankRewardEntryDefinition> RewardEntries { get; }

    public bool HasCustomStatRequirement() =>
        RequiredCustomStatId != "" && RequiredCustomStatMinValue > 0;

    public bool HasAchievementRequirement() => RequiredAchievementId != "";

    internal static FaithRankDefinition FromResource(FaithRankDef source, string path)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.RewardEntriesProjectionBorrowed == null)
            throw Invalid(path + ".reward_entries", "collection is null");

        var rewards = new List<FaithRankRewardEntryDefinition>(
            source.RewardEntriesProjectionBorrowed.Count
        );
        for (int index = 0; index < source.RewardEntriesProjectionBorrowed.Count; index++)
        {
            rewards.Add(
                ProjectReward(
                    source.RewardEntriesProjectionBorrowed[index],
                    $"{path}.reward_entries[{index}]"
                )
            );
        }

        return new FaithRankDefinition(
            source.rank_index,
            source.rank_name,
            source.required_gold,
            source.required_level,
            source.required_custom_stat_id,
            source.required_custom_stat_min_value,
            source.required_achievement_id,
            new ReadOnlyCollection<FaithRankRewardEntryDefinition>(rewards)
        );
    }

    private static FaithRankRewardEntryDefinition ProjectReward(
        GDictionary source,
        string path
    )
    {
        if (source == null)
            throw Invalid(path, "dictionary is null");

        Dictionary<string, Variant> values = NormalizeKeys(source, path);
        string[] allowedKeys =
        {
            "entry_type",
            "target_id",
            "amount",
            "target_label",
            "reason_text",
        };
        foreach (string key in values.Keys)
        {
            if (System.Array.IndexOf(allowedKeys, key) < 0)
                throw Invalid(path + "." + key, "field is not supported");
        }
        foreach (string key in new[] { "entry_type", "target_id", "amount" })
        {
            if (!values.ContainsKey(key))
                throw Invalid(path + "." + key, "required field is missing");
        }

        return new FaithRankRewardEntryDefinition(
            ReadStringName(values["entry_type"], path + ".entry_type"),
            ReadStringName(values["target_id"], path + ".target_id"),
            ReadInt(values["amount"], path + ".amount"),
            ReadOptionalString(values, "target_label", path),
            ReadOptionalString(values, "reason_text", path)
        );
    }

    private static Dictionary<string, Variant> NormalizeKeys(GDictionary source, string path)
    {
        var result = new Dictionary<string, Variant>(StringComparer.Ordinal);
        int index = 0;
        foreach (Variant rawKey in source.Keys)
        {
            string key = rawKey.VariantType switch
            {
                Variant.Type.String => rawKey.AsString(),
                Variant.Type.StringName => rawKey.AsStringName().ToString(),
                _ => throw Invalid(
                    $"{path}[key:{index}]",
                    $"key must be String or StringName, got {rawKey.VariantType}"
                ),
            };
            if (string.IsNullOrEmpty(key))
                throw Invalid($"{path}[key:{index}]", "key must not be empty");
            if (!result.TryAdd(key, source[rawKey]))
                throw Invalid(path + "." + key, "duplicate normalized key");
            index++;
        }
        return result;
    }

    private static StringName ReadStringName(Variant value, string path)
    {
        return value.VariantType switch
        {
            Variant.Type.String => new StringName(value.AsString()),
            Variant.Type.StringName => value.AsStringName(),
            _ => throw Invalid(path, $"must be String or StringName, got {value.VariantType}"),
        };
    }

    private static int ReadInt(Variant value, string path)
    {
        if (value.VariantType != Variant.Type.Int)
            throw Invalid(path, $"must be Int, got {value.VariantType}");
        return value.AsInt32();
    }

    private static string ReadString(Variant value, string path)
    {
        if (value.VariantType != Variant.Type.String)
            throw Invalid(path, $"must be String, got {value.VariantType}");
        return value.AsString();
    }

    private static string ReadOptionalString(
        IReadOnlyDictionary<string, Variant> values,
        string key,
        string path
    ) =>
        values.TryGetValue(key, out Variant value)
            ? ReadString(value, path + "." + key)
            : "";

    private static InvalidDataException Invalid(string path, string message) =>
        new($"Invalid authored faith content at '{path}': {message}.");
}
