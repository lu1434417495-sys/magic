using Godot;

[GlobalClass]
public partial class BattleCommand : RefCounted
{
    private static readonly StringName TypeMove = "move";
    private static readonly StringName TypeSkill = "skill";
    private static readonly StringName TypeWait = "wait";
    private static readonly StringName TypeChangeEquipment = "change_equipment";
    private static readonly StringName EquipmentOperationEquip = "equip";
    private static readonly StringName EquipmentOperationUnequip = "unequip";

    public StringName command_type = "";
    public StringName unit_id = "";
    public StringName skill_id = "";
    public StringName skill_variant_id = "";
    public StringName target_unit_id = "";
    public Godot.Collections.Array<StringName> target_unit_ids = new();
    public Vector2I target_coord = new(-1, -1);
    public Godot.Collections.Array<Vector2I> target_coords = new();
    public StringName equipment_operation = "";
    public StringName equipment_slot_id = "";
    public StringName equipment_item_id = "";
    public StringName equipment_instance_id = "";
    public Godot.Collections.Dictionary equipment_instance = new();
    public Godot.Collections.Array<StringName> equipment_occupied_slot_ids = new();

    public static StringName TYPE_MOVE() => TypeMove;
    public static StringName TYPE_SKILL() => TypeSkill;
    public static StringName TYPE_WAIT() => TypeWait;
    public static StringName TYPE_CHANGE_EQUIPMENT() => TypeChangeEquipment;
    public static StringName EQUIPMENT_OPERATION_EQUIP() => EquipmentOperationEquip;
    public static StringName EQUIPMENT_OPERATION_UNEQUIP() => EquipmentOperationUnequip;

    public bool is_move() => command_type == TypeMove;
    public bool is_skill() => command_type == TypeSkill;
    public bool is_wait() => command_type == TypeWait;
    public bool is_change_equipment() => command_type == TypeChangeEquipment;
}
