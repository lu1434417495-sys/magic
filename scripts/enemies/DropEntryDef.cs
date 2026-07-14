using Godot;

[GlobalClass]
public partial class DropEntryDef : Resource
{
    [Export]
    public StringName drop_entry_id { get; set; } = "";

    [Export]
    public StringName drop_type { get; set; } = "item";

    [Export]
    public StringName item_id { get; set; } = "";

    [Export]
    public int quantity { get; set; } = 1;

    public DropEntryDef() { }

    internal DropEntryDefinition ToDefinition() =>
        new(drop_entry_id, drop_type, item_id, quantity);

}
