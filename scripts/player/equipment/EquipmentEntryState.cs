using Godot;

[GlobalClass]
public partial class EquipmentEntryState : RefCounted
{
    private static readonly GDScript EquipmentInstanceStateScript = GD.Load<GDScript>("res://scripts/player/warehouse/equipment_instance_state.gd");

    public StringName item_id = "";
    public Godot.Collections.Array<StringName> occupied_slot_ids = new();
    public StringName instance_id = "";
    public GodotObject equipment_instance;

    public bool is_empty() => equipment_instance == null || (string)item_id == "" || (string)instance_id == "";

    public GodotObject get_equipment_instance() => equipment_instance;

    public bool set_equipment_instance(GodotObject instance)
    {
        if (instance == null || !instance.HasMethod("to_dict")) return false;
        var payload = instance.Call("to_dict");
        var ni = EquipmentInstanceStateScript.Call("from_dict", payload).AsGodotObject();
        if (ni == null) return false;
        if ((string)ni.Get("item_id").AsStringName() == "" || (string)ni.Get("instance_id").AsStringName() == "") return false;
        equipment_instance = ni;
        item_id = ni.Get("item_id").AsStringName();
        instance_id = ni.Get("instance_id").AsStringName();
        return true;
    }

    public EquipmentEntryState duplicate_state() => from_dict(to_dict());

    public Godot.Collections.Dictionary to_dict() => new()
    {
        {"occupied_slot_ids", ProgressionDataUtils.string_name_array_to_string_array(occupied_slot_ids)},
        {"equipment_instance", equipment_instance?.Call("to_dict").AsGodotDictionary() ?? new Godot.Collections.Dictionary()},
    };

    public static EquipmentEntryState from_dict(Variant data)
    {
        if (data.VariantType != Variant.Type.Dictionary) return null;
        var payload = data.AsGodotDictionary();
        if (payload.Count != 2) return null;
        if (!payload.ContainsKey("occupied_slot_ids") || !payload.ContainsKey("equipment_instance")) return null;
        var ov = payload["occupied_slot_ids"];
        if (ov.VariantType != Variant.Type.Array) return null;
        var inst = EquipmentInstanceStateScript.Call("from_dict", payload["equipment_instance"]).AsGodotObject();
        if (inst == null) return null;
        var entry = new EquipmentEntryState();
        if (!entry.set_equipment_instance(inst)) return null;
        foreach (var rsv in ov.AsGodotArray())
        {
            if (!_is_string_name_payload_value(rsv)) return null;
            var slot_id = ProgressionDataUtils.to_string_name(rsv);
            if ((string)slot_id == "" || !EquipmentRules.is_valid_slot(slot_id)) return null;
            if (entry.occupied_slot_ids.Contains(slot_id)) return null;
            entry.occupied_slot_ids.Add(slot_id);
        }
        if ((string)entry.item_id == "" || entry.occupied_slot_ids.Count == 0) return null;
        return entry;
    }

    private static bool _is_string_name_payload_value(Variant value)
    {
        var vt = value.VariantType;
        return vt == Variant.Type.String || vt == Variant.Type.StringName;
    }
}
