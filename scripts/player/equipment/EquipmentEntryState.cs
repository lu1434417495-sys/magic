using System.Collections.Generic;
using Godot;

public class EquipmentEntryState
{
    public StringName item_id = "";
    public List<StringName> occupied_slot_ids = new();
    public StringName instance_id = "";
    public EquipmentInstanceState equipment_instance;

    public bool IsEmpty() =>
        equipment_instance == null || item_id == "" || instance_id == "";

    public EquipmentInstanceState GetEquipmentInstance() => equipment_instance;

    public bool SetEquipmentInstance(EquipmentInstanceState instance)
    {
        if (instance == null)
            return false;
        var ni = instance.DuplicateState();
        if (ni == null)
            return false;
        if (ni.item_id == "" || ni.instance_id == "")
            return false;
        equipment_instance = ni;
        item_id = ni.item_id;
        instance_id = ni.instance_id;
        return true;
    }

    public EquipmentEntryState DuplicateState()
    {
        if (IsEmpty())
            return new EquipmentEntryState();
        var entry = new EquipmentEntryState();
        if (!entry.SetEquipmentInstance(equipment_instance))
            return new EquipmentEntryState();
        entry.occupied_slot_ids = new List<StringName>(occupied_slot_ids);
        return entry;
    }

    internal EquipmentEntryState DuplicateForMutationSnapshotExact() =>
        new()
        {
            item_id = item_id,
            occupied_slot_ids = occupied_slot_ids == null
                ? null
                : new List<StringName>(occupied_slot_ids),
            instance_id = instance_id,
            equipment_instance = equipment_instance?.DuplicateForMutationSnapshotExact(),
        };

    public Godot.Collections.Dictionary ToDictionary() =>
        new()
        {
            {
                "occupied_slot_ids",
                ProgressionDataUtils.string_name_array_to_string_array(occupied_slot_ids)
            },
            {
                "equipment_instance",
                equipment_instance?.ToDictionary() ?? new Godot.Collections.Dictionary()
            },
        };

    public static EquipmentEntryState FromDictionary(Godot.Collections.Dictionary payload)
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
        var inst = EquipmentInstanceState.FromDictionary(
            payload["equipment_instance"].AsGodotDictionary()
        );
        if (inst == null)
            return null;
        var entry = new EquipmentEntryState();
        if (!entry.SetEquipmentInstance(inst))
            return null;
        var occupiedSlotIds = new HashSet<StringName>();
        foreach (var rsv in ov.AsGodotArray())
        {
            if (rsv.VariantType != Variant.Type.String)
                return null;
            string slotText = rsv.AsString().StripEdges();
            if (slotText.Length == 0)
                return null;
            var slot_id = new StringName(slotText);
            if (!EquipmentRules.IsValidSlot(slot_id))
                return null;
            if (!occupiedSlotIds.Add(slot_id))
                return null;
            entry.occupied_slot_ids.Add(slot_id);
        }
        if (entry.item_id == "" || entry.occupied_slot_ids.Count == 0)
            return null;
        return entry;
    }
}
