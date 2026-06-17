using Godot;

public partial class PendingCharacterRewardEntry : RefCounted
{
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

    internal StringName mastery_source_type = "";

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
            mastery_source_type = mastery_source_type,
        };
    }
}
