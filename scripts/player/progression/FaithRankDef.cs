using Godot;

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

    public bool has_custom_stat_requirement()
    {
        return required_custom_stat_id != "" && required_custom_stat_min_value > 0;
    }

    public bool has_achievement_requirement()
    {
        return required_achievement_id != "";
    }

    public Godot.Collections.Array<string> validate()
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
        if (has_custom_stat_requirement() && has_achievement_requirement())
            errors.Add(
                $"Faith rank {rank_index} should not mix custom stat and achievement placeholder gates."
            );
        if (required_custom_stat_id == "" && required_custom_stat_min_value != 0)
            errors.Add(
                $"Faith rank {rank_index} sets required_custom_stat_min_value without required_custom_stat_id."
            );
        if (reward_entries.Count == 0)
            errors.Add($"Faith rank {rank_index} must define at least one reward entry.");

        foreach (var rewardData in reward_entries)
        {
            if (rewardData == null)
            {
                errors.Add($"Faith rank {rank_index} contains a non-dictionary reward entry.");
                continue;
            }
            StringName entryType = GdInterop.GetStringName(rewardData, "entry_type");
            StringName targetId = GdInterop.GetStringName(rewardData, "target_id");
            int amount = GdInterop.GetInt(rewardData, "amount");
            if (entryType == "" || targetId == "" || amount == 0)
            {
                errors.Add($"Faith rank {rank_index} contains an invalid reward entry.");
                continue;
            }
            if (!PendingCharacterRewardContentRules.is_supported_entry_type(entryType))
            {
                errors.Add(
                    $"Faith rank {rank_index} contains unsupported reward entry_type {entryType}."
                );
                continue;
            }
            if (
                PendingCharacterRewardContentRules.is_attribute_progress_entry(entryType)
                && !PendingCharacterRewardContentRules.is_valid_attribute_progress_target(targetId)
            )
            {
                errors.Add(
                    $"Faith rank {rank_index} attribute_progress reward references unsupported attribute {targetId}."
                );
            }
        }
        return errors;
    }
}
