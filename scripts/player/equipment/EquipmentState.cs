using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class EquipmentState : RefCounted
{
    private readonly Dictionary<StringName, EquipmentEntryState> _equipped_slots = new();
    private readonly Dictionary<StringName, StringName> _slot_to_entry_slot = new();

    public StringName get_equipped_item_id(StringName slot_id)
    {
        var e = get_entry_for_slot(slot_id);
        return e != null ? e.item_id : new StringName("");
    }

    public StringName get_equipped_instance_id(StringName slot_id)
    {
        var e = get_entry_for_slot(slot_id);
        return e != null ? e.instance_id : new StringName("");
    }

    public EquipmentInstanceState get_equipped_instance(StringName slot_id)
    {
        var e = get_entry_for_slot(slot_id);
        return e?.get_equipment_instance();
    }

    public EquipmentEntryState get_entry(StringName entry_slot_id)
    {
        var n = ProgressionDataUtils.to_string_name(entry_slot_id);
        return n != "" && _equipped_slots.TryGetValue(n, out var entry)
            ? entry
            : null;
    }

    public EquipmentEntryState get_entry_for_slot(StringName slot_id)
    {
        var es = _get_entry_slot_for_slot(slot_id);
        return es == "" ? null : get_entry(es);
    }

    public Godot.Collections.Array<StringName> get_occupied_slot_ids_for_entry(
        StringName entry_slot_id
    )
    {
        return new Godot.Collections.Array<StringName>(
            GetOccupiedSlotIdsForEntryTyped(entry_slot_id)
        );
    }

    public IReadOnlyList<StringName> GetOccupiedSlotIdsForEntryTyped(StringName entry_slot_id)
    {
        var e = get_entry(entry_slot_id);
        return e != null ? new List<StringName>(e.occupied_slot_ids) : new List<StringName>();
    }

    public bool set_equipped_entry(
        StringName entry_slot_id,
        StringName item_id,
        Godot.Collections.Array<StringName> occupied,
        EquipmentInstanceState equipment_instance = null
    )
    {
        return SetEquippedEntryTyped(entry_slot_id, item_id, occupied, equipment_instance);
    }

    public bool SetEquippedEntryTyped(
        StringName entry_slot_id,
        StringName item_id,
        IEnumerable<StringName> occupied,
        EquipmentInstanceState equipment_instance = null
    )
    {
        var ne = ProgressionDataUtils.to_string_name(entry_slot_id);
        var ni = ProgressionDataUtils.to_string_name(item_id);
        if (!EquipmentRules.is_valid_slot(ne))
            return false;
        if (ni == "")
        {
            clear_entry_slot(ne);
            return true;
        }
        var ei = _normalize_equipment_instance(equipment_instance, ni);
        if (ei == null)
            return false;
        var entry = new EquipmentEntryState();
        if (!entry.set_equipment_instance(ei))
            return false;
        entry.occupied_slot_ids = _normalize_occupied_slot_ids(ne, occupied);
        _store_entry(ne, entry);
        return true;
    }

    public void clear_slot(StringName slot_id)
    {
        var es = _get_entry_slot_for_slot(ProgressionDataUtils.to_string_name(slot_id));
        if (es != "")
            clear_entry_slot(es);
    }

    public void clear_entry_slot(StringName entry_slot_id)
    {
        var n = ProgressionDataUtils.to_string_name(entry_slot_id);
        var entry = get_entry(n);
        if (entry != null)
        {
            foreach (var o in entry.occupied_slot_ids)
            {
                var os = ProgressionDataUtils.to_string_name(o);
                if (
                    _slot_to_entry_slot.TryGetValue(os, out StringName entrySlot)
                    && entrySlot == n
                )
                    _slot_to_entry_slot.Remove(os);
            }
        }
        _equipped_slots.Remove(n);
    }

    public EquipmentInstanceState pop_equipped_instance(StringName entry_slot_id)
    {
        var ne = ProgressionDataUtils.to_string_name(entry_slot_id);
        var entry = get_entry(ne);
        if (entry == null)
            return null;
        if (entry.item_id == "")
        {
            clear_entry_slot(ne);
            return null;
        }
        var inst = entry.get_equipment_instance();
        if (inst == null)
        {
            clear_entry_slot(ne);
            return null;
        }
        clear_entry_slot(ne);
        return inst;
    }

    private StringName _get_entry_slot_for_slot(StringName slot_id)
    {
        var n = ProgressionDataUtils.to_string_name(slot_id);
        if (!EquipmentRules.is_valid_slot(n))
            return new StringName("");
        return _slot_to_entry_slot.TryGetValue(n, out StringName ev) ? ev : new StringName("");
    }

    public StringName get_entry_slot_for_slot(StringName slot_id) =>
        _get_entry_slot_for_slot(slot_id);

    public Godot.Collections.Array<StringName> get_entry_slot_ids()
    {
        return new Godot.Collections.Array<StringName>(GetEntrySlotIdsTyped());
    }

    public IReadOnlyList<StringName> GetEntrySlotIdsTyped()
    {
        var r = new List<StringName>();
        foreach (var sid in EquipmentRules.GetAllSlotIdsTyped())
        {
            var e = get_entry(sid);
            if (e != null && !e.is_empty())
                r.Add(sid);
        }
        return r;
    }

    public Godot.Collections.Array<StringName> get_filled_slot_ids()
    {
        return new Godot.Collections.Array<StringName>(GetFilledSlotIdsTyped());
    }

    public IReadOnlyList<StringName> GetFilledSlotIdsTyped()
    {
        var r = new List<StringName>();
        foreach (var sid in EquipmentRules.GetAllSlotIdsTyped())
            if (_get_entry_slot_for_slot(sid) != "")
                r.Add(sid);
        return r;
    }

    public int get_equipped_count() => GetEntrySlotIdsTyped().Count;

    public EquipmentState duplicate_state()
    {
        var state = new EquipmentState();
        foreach (StringName entrySlotId in GetEntrySlotIdsTyped())
        {
            EquipmentEntryState entry = get_entry(entrySlotId)?.duplicate_state();
            if (entry == null || entry.is_empty())
                continue;
            state._equipped_slots[entrySlotId] = entry;
            state._register_entry_slots(entrySlotId, entry);
        }
        return state;
    }

    public Godot.Collections.Dictionary to_dict()
    {
        var sd = new Godot.Collections.Dictionary();
        foreach (var es in GetEntrySlotIdsTyped())
        {
            var e = get_entry(es);
            if (e != null)
                sd[es.ToString()] = e.to_dict();
        }
        return new Godot.Collections.Dictionary { { "equipped_slots", sd } };
    }

    public static EquipmentState from_dict(Godot.Collections.Dictionary data)
    {
        if (data == null)
            return null;
        if (data.Count != 1 || !data.ContainsKey("equipped_slots"))
            return null;
        var sd = data["equipped_slots"];
        if (sd.VariantType != Variant.Type.Dictionary)
            return null;
        var sdd = sd.AsGodotDictionary();
        var state = new EquipmentState();
        var seenEntries = new HashSet<StringName>();
        var usedOccupiedSlots = new HashSet<StringName>();
        foreach (var key in sdd.Keys)
        {
            if (!_is_string_name_payload_type((long)key.VariantType))
                return null;
            var slot_id = ProgressionDataUtils.to_string_name(key);
            if (
                slot_id == ""
                || !EquipmentRules.is_valid_slot(slot_id)
                || !seenEntries.Add(slot_id)
            )
                return null;
            var entryValue = sdd[key];
            if (entryValue.VariantType != Variant.Type.Dictionary)
                return null;
            var entry = EquipmentEntryState.from_dict(entryValue.AsGodotDictionary());
            if (entry == null || entry.is_empty())
                return null;
            bool hasSlot = false;
            foreach (var os in entry.occupied_slot_ids)
                if (ProgressionDataUtils.to_string_name(os) == slot_id)
                {
                    hasSlot = true;
                    break;
                }
            if (!hasSlot)
                return null;
            foreach (var os in entry.occupied_slot_ids)
            {
                var osn = ProgressionDataUtils.to_string_name(os);
                if (!usedOccupiedSlots.Add(osn))
                    return null;
            }
            state._equipped_slots[slot_id] = entry;
            state._register_entry_slots(slot_id, entry);
        }
        return state;
    }

    private List<StringName> _normalize_occupied_slot_ids(
        StringName entry_slot_id,
        IEnumerable<StringName> occupied
    )
    {
        var validated = new List<StringName>();
        var seen = new HashSet<StringName>();
        if (occupied != null)
        {
            foreach (var rs in occupied)
            {
                var sid = ProgressionDataUtils.to_string_name(rs);
                if (!EquipmentRules.is_valid_slot(sid) || !seen.Add(sid))
                    continue;
                validated.Add(sid);
            }
        }
        if (validated.Count == 0)
            validated.Add(entry_slot_id);
        else if (!seen.Contains(entry_slot_id))
            validated.Insert(0, entry_slot_id);
        return validated;
    }

    private EquipmentInstanceState _normalize_equipment_instance(
        EquipmentInstanceState equipment_instance,
        StringName item_id
    )
    {
        if (equipment_instance == null)
            return null;
        var ni = ProgressionDataUtils.to_string_name(item_id);
        return equipment_instance.item_id == ni ? equipment_instance.duplicate_state() : null;
    }

    private void _store_entry(StringName entry_slot_id, EquipmentEntryState entry)
    {
        var n = ProgressionDataUtils.to_string_name(entry_slot_id);
        clear_entry_slot(n);
        var ne = _normalize_entry_object(entry);
        if (ne == null)
            return;
        foreach (var os in ne.occupied_slot_ids)
        {
            var osn = ProgressionDataUtils.to_string_name(os);
            if (
                _slot_to_entry_slot.TryGetValue(osn, out StringName existingEntry)
                && existingEntry != n
            )
                clear_entry_slot(existingEntry);
        }
        _equipped_slots[n] = ne;
        _register_entry_slots(n, ne);
    }

    private void _register_entry_slots(StringName entry_slot_id, EquipmentEntryState entry)
    {
        if (entry == null)
            return;
        _slot_to_entry_slot[entry_slot_id] = entry_slot_id;
        foreach (var os in entry.occupied_slot_ids)
            _slot_to_entry_slot[ProgressionDataUtils.to_string_name(os)] = entry_slot_id;
    }

    private static EquipmentEntryState _normalize_entry_object(EquipmentEntryState entry)
    {
        return entry == null || entry.is_empty() ? null : entry;
    }

    private static bool _is_string_name_payload_type(long valueType)
    {
        return valueType == (long)Variant.Type.String || valueType == (long)Variant.Type.StringName;
    }
}
