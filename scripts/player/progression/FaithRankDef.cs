using System.Collections.Generic;
using Godot;

public readonly record struct FaithRankRewardEntrySpec(
    StringName EntryType,
    StringName TargetId,
    int Amount,
    string TargetLabel,
    string ReasonText
)
{
    public bool IsEmpty => EntryType == "" || TargetId == "" || Amount == 0;
}

[GlobalClass]
public partial class FaithRankDef : Resource
{
    [Export]
    public int rank_index { get; set; } = 1;

    [Export]
    public string rank_name { get; set; } = "";

    [Export]
    public int required_gold { get; set; }

    [Export]
    public int required_level { get; set; }

    [Export]
    public StringName required_custom_stat_id { get; set; } = "";

    [Export]
    public int required_custom_stat_min_value { get; set; }

    [Export]
    public StringName required_achievement_id { get; set; } = "";

    [Export]
    public Godot.Collections.Array<Godot.Collections.Dictionary> reward_entries { get; set; } =
        new();

    public bool HasCustomStatRequirement()
    {
        return required_custom_stat_id != "" && required_custom_stat_min_value > 0;
    }

    public bool HasAchievementRequirement()
    {
        return required_achievement_id != "";
    }

    public List<FaithRankRewardEntrySpec> GetRewardEntrySpecs()
    {
        var result = new List<FaithRankRewardEntrySpec>();
        foreach (var rewardData in reward_entries)
        {
            result.Add(ParseRewardEntrySpec(rewardData));
        }
        return result;
    }

    public Godot.Collections.Array<string> Validate()
    {
        var errors = new Godot.Collections.Array<string>();
        if (rank_index <= 0)
            errors.Add("Faith rank must have rank_index >= 1.");
        if (string.IsNullOrEmpty(rank_name))
            errors.Add($"Faith rank {rank_index} is missing rank_name.");
        if (required_gold < 0)
            errors.Add($"Faith rank {rank_index} uses negative required_gold {required_gold}.");
        if (required_level < 0)
            errors.Add($"Faith rank {rank_index} uses negative required_level {required_level}.");
        if (HasCustomStatRequirement() && HasAchievementRequirement())
            errors.Add(
                $"Faith rank {rank_index} should not mix custom stat and achievement placeholder gates."
            );
        if (required_custom_stat_id == "" && required_custom_stat_min_value != 0)
            errors.Add(
                $"Faith rank {rank_index} sets required_custom_stat_min_value without required_custom_stat_id."
            );
        if (reward_entries.Count == 0)
            errors.Add($"Faith rank {rank_index} must define at least one reward entry.");

        foreach (FaithRankRewardEntrySpec rewardSpec in GetRewardEntrySpecs())
        {
            if (rewardSpec.IsEmpty)
            {
                errors.Add($"Faith rank {rank_index} contains an invalid reward entry.");
                continue;
            }
            if (!PendingCharacterRewardContentRules.IsSupportedEntryType(rewardSpec.EntryType))
            {
                errors.Add(
                    $"Faith rank {rank_index} contains unsupported reward entry_type {rewardSpec.EntryType}."
                );
                continue;
            }
            if (
                PendingCharacterRewardContentRules.IsAttributeProgressEntry(
                    rewardSpec.EntryType
                )
                && !PendingCharacterRewardContentRules.IsValidAttributeProgressTarget(
                    rewardSpec.TargetId
                )
            )
            {
                errors.Add(
                    $"Faith rank {rank_index} attribute_progress reward references unsupported attribute {rewardSpec.TargetId}."
                );
            }
        }
        return errors;
    }

    private static FaithRankRewardEntrySpec ParseRewardEntrySpec(
        Godot.Collections.Dictionary data
    )
    {
        if (data == null)
            return new FaithRankRewardEntrySpec("", "", 0, "", "");
        return new FaithRankRewardEntrySpec(
            ReadStringName(data, "entry_type"),
            ReadStringName(data, "target_id"),
            ReadInt(data, "amount"),
            ReadString(data, "target_label"),
            ReadString(data, "reason_text")
        );
    }

    private static StringName ReadStringName(
        Godot.Collections.Dictionary data,
        string key,
        StringName fallback = default
    )
    {
        var value = ReadValue(data, key);
        if (value.VariantType == Variant.Type.StringName)
            return value.AsStringName();
        if (value.VariantType == Variant.Type.String)
            return new StringName(value.AsString());
        return fallback ?? new StringName("");
    }

    private static int ReadInt(Godot.Collections.Dictionary data, string key, int fallback = 0)
    {
        var value = ReadValue(data, key);
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static string ReadString(
        Godot.Collections.Dictionary data,
        string key,
        string fallback = ""
    )
    {
        var value = ReadValue(data, key);
        return value.VariantType == Variant.Type.String ? value.AsString() : fallback;
    }

    private static Variant ReadValue(Godot.Collections.Dictionary data, string key)
    {
        if (data == null)
            return default;
        if (data.ContainsKey(key))
            return data[key];
        var stringNameKey = new StringName(key);
        if (data.ContainsKey(stringNameKey))
            return data[stringNameKey];
        return default;
    }
}
