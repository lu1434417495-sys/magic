using Godot;
using GStringArray = Godot.Collections.Array<string>;

[GlobalClass]
public partial class FaithDeityDef : Resource
{
    [Export]
    public StringName deity_id { get; set; } = "";

    [Export]
    public string display_name { get; set; } = "";

    [Export]
    public StringName facility_id { get; set; } = "";

    [Export]
    public string service_type_label { get; set; } = "";

    [Export]
    public Godot.Collections.Array<StringName> power_domain_tags { get; set; } = new();

    [Export]
    public StringName rank_progress_stat_id { get; set; } = "";

    [Export]
    public Godot.Collections.Array<FaithRankDef> rank_defs { get; set; } = new();

    public FaithRankDef get_rank_def(int rankIndex)
    {
        foreach (var rd in rank_defs)
        {
            if (rd != null && rd.rank_index == rankIndex)
                return rd;
        }
        return null;
    }

    public int get_max_rank()
    {
        int m = 0;
        foreach (var rd in rank_defs)
        {
            if (rd != null)
                m = Mathf.Max(m, rd.rank_index);
        }
        return m;
    }

    public GStringArray validate()
    {
        var errors = new GStringArray();
        if (deity_id == "")
            errors.Add("Faith deity config is missing deity_id.");
        if (display_name.Length == 0)
            errors.Add($"Faith deity {deity_id} is missing display_name.");
        if (rank_progress_stat_id == "")
            errors.Add($"Faith deity {deity_id} is missing rank_progress_stat_id.");
        if (rank_defs.Count == 0)
            errors.Add($"Faith deity {deity_id} must declare at least one rank_def.");

        var seenRanks = new Godot.Collections.Dictionary();
        foreach (var rd in rank_defs)
        {
            if (rd == null)
            {
                errors.Add($"Faith deity {deity_id} contains a null rank_def.");
                continue;
            }
            if (seenRanks.ContainsKey(rd.rank_index))
            {
                errors.Add($"Faith deity {deity_id} declares duplicate rank {rd.rank_index}.");
                continue;
            }
            seenRanks[rd.rank_index] = true;
            foreach (var rankError in rd.validate())
                errors.Add($"Faith deity {deity_id}: {rankError}");
            if (rank_progress_stat_id != "" && !_has_rank_progress_reward(rd))
                errors.Add(
                    $"Faith deity {deity_id} rank {rd.rank_index} is missing rank progress reward {rank_progress_stat_id}."
                );
        }

        int maxRank = get_max_rank();
        for (int expected = 1; expected <= maxRank; expected++)
            if (!seenRanks.ContainsKey(expected))
                errors.Add($"Faith deity {deity_id} is missing rank {expected}.");
        return errors;
    }

    private bool _has_rank_progress_reward(FaithRankDef rankDef)
    {
        if (rankDef == null || rank_progress_stat_id == "")
            return false;
        foreach (FaithRankRewardEntrySpec entry in rankDef.GetRewardEntrySpecs())
        {
            if (
                entry.EntryType == "attribute_delta"
                && entry.TargetId == rank_progress_stat_id
            )
                return true;
        }
        return false;
    }
}
