using Godot;

[GlobalClass]
public partial class EnemyBattleEquipmentDef : Resource
{
    [Export]
    public StringName slot_id { get; set; } = "";

    [Export]
    public StringName item_id { get; set; } = "";

    [Export(PropertyHint.Range, "0,4,1")]
    public int rarity { get; set; }

    [Export]
    public int current_durability { get; set; }

    internal EnemyBattleEquipmentDefinition ToDefinition() =>
        new(
            ProgressionDataUtils.to_string_name(slot_id),
            ProgressionDataUtils.to_string_name(item_id),
            rarity,
            current_durability
        );
}
