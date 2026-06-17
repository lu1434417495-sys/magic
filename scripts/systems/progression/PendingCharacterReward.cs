using Godot;

public partial class PendingCharacterReward : RefCounted
{
    public StringName reward_id = "";

    public StringName member_id = "";

    public string member_name = "";

    public StringName source_type = "";

    public StringName source_id = "";

    public string source_label = "";

    public string summary_text = "";

    public Godot.Collections.Array<PendingCharacterRewardEntry> entries = new();

    public bool IsEmpty()
    {
        if (
            reward_id == ""
            || member_id == ""
            || source_type == ""
            || source_id == ""
            || entries.Count == 0
        )
            return true;

        foreach (var entry in entries)
        {
            if (entry != null && !entry.IsEmpty())
                return false;
        }

        return true;
    }

    public PendingCharacterReward DuplicateState()
    {
        var copy = new PendingCharacterReward
        {
            reward_id = reward_id,
            member_id = member_id,
            member_name = member_name,
            source_type = source_type,
            source_id = source_id,
            source_label = source_label,
            summary_text = summary_text,
        };
        foreach (var entry in entries)
            if (entry != null)
                copy.entries.Add(entry.DuplicateState());
        return copy;
    }
}
