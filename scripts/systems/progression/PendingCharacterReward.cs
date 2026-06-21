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
    private bool _disposed;

    public new void Dispose()
    {
        if (_disposed)
            return;
        System.GC.SuppressFinalize(this);
        Dispose(true);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            DisposeManagedState();
        base.Dispose(disposing);
    }

    private void DisposeManagedState()
    {
        if (_disposed)
            return;
        _disposed = true;
        GodotRefCountedDisposer.DisposeAll(entries);
        entries.Clear();
    }

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
