using Godot;

[GlobalClass]
public partial class EquipmentEntryState : RefCounted
{
    public StringName item_id = "";
    public Godot.Collections.Array<StringName> occupied_slot_ids = new();
    public StringName instance_id = "";
    public EquipmentInstanceState equipment_instance;

    public bool is_empty() =>
        equipment_instance == null || (string)item_id == "" || (string)instance_id == "";

    public EquipmentInstanceState get_equipment_instance() => equipment_instance;

    public bool set_equipment_instance(EquipmentInstanceState instance)
    {
        if (instance == null)
            return false;
        var ni = EquipmentInstanceState.from_dict(instance.to_dict());
        if (ni == null)
            return false;
        if (ni.item_id == "" || ni.instance_id == "")
            return false;
        equipment_instance = ni;
        item_id = ni.item_id;
        instance_id = ni.instance_id;
        return true;
    }

    public EquipmentEntryState duplicate_state()
    {
        if (is_empty())
            return new EquipmentEntryState();
        return from_dict(to_dict());
    }

    public Godot.Collections.Dictionary to_dict() =>
        new()
        {
            {
                "occupied_slot_ids",
                ProgressionDataUtils.string_name_array_to_string_array(occupied_slot_ids)
            },
            {
                "equipment_instance",
                equipment_instance?.to_dict() ?? new Godot.Collections.Dictionary()
            },
        };

    public static EquipmentEntryState from_dict(Godot.Collections.Dictionary payload)
    {
        if (payload == null)
            return null;
        if (payload.Count != 2)
            return null;
        if (!payload.ContainsKey("occupied_slot_ids") || !payload.ContainsKey("equipment_instance"))
            return null;
        var ov = payload["occupied_slot_ids"];
        if (ov.VariantType != Variant.Type.Array)
            return null;
        if (payload["equipment_instance"].VariantType != Variant.Type.Dictionary)
            return null;
        var inst = EquipmentInstanceState.from_dict(
            payload["equipment_instance"].AsGodotDictionary()
        );
        if (inst == null)
            return null;
        var entry = new EquipmentEntryState();
        if (!entry.set_equipment_instance(inst))
            return null;
        foreach (var rsv in ov.AsGodotArray())
        {
            if (!_is_string_name_payload_type((long)rsv.VariantType))
                return null;
            var slot_id = ProgressionDataUtils.to_string_name(rsv);
            if ((string)slot_id == "" || !EquipmentRules.is_valid_slot(slot_id))
                return null;
            if (entry.occupied_slot_ids.Contains(slot_id))
                return null;
            entry.occupied_slot_ids.Add(slot_id);
        }
        if ((string)entry.item_id == "" || entry.occupied_slot_ids.Count == 0)
            return null;
        return entry;
    }

    private static bool _is_string_name_payload_type(long valueType)
    {
        return valueType == (long)Variant.Type.String || valueType == (long)Variant.Type.StringName;
    }
}
