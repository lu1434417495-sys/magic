using System.Collections.Generic;
using Godot;

public partial class EquipmentState : RefCounted
{
    private readonly Dictionary<StringName, EquipmentEntryState> _equipped_slots = new();
    private readonly Dictionary<StringName, StringName> _slot_to_entry_slot = new();

    public StringName GetEquippedItemId(StringName slot_id)
    {
        var e = GetEntryForSlot(slot_id);
        return e != null ? e.item_id : new StringName("");
    }

    public StringName GetEquippedInstanceId(StringName slot_id)
    {
        var e = GetEntryForSlot(slot_id);
        return e != null ? e.instance_id : new StringName("");
    }

    public EquipmentInstanceState GetEquippedInstance(StringName slot_id)
    {
        var e = GetEntryForSlot(slot_id);
        return e?.GetEquipmentInstance();
    }

    public EquipmentEntryState GetEntry(StringName entry_slot_id)
    {
        var n = ProgressionDataUtils.to_string_name(entry_slot_id);
        return n != "" && _equipped_slots.TryGetValue(n, out var entry)
            ? entry
            : null;
    }

    public EquipmentEntryState GetEntryForSlot(StringName slot_id)
    {
        var es = ResolveEntrySlotForSlot(slot_id);
        return es == "" ? null : GetEntry(es);
    }

    public IReadOnlyList<StringName> GetOccupiedSlotIdsForEntryTyped(StringName entry_slot_id)
    {
        var e = GetEntry(entry_slot_id);
        return e != null ? new List<StringName>(e.occupied_slot_ids) : new List<StringName>();
    }

    public bool SetEquippedEntry(
        StringName entry_slot_id,
        StringName item_id,
        IEnumerable<StringName> occupied,
        EquipmentInstanceState equipment_instance = null
    )
    {
        var ne = ProgressionDataUtils.to_string_name(entry_slot_id);
        var ni = ProgressionDataUtils.to_string_name(item_id);
        if (!EquipmentRules.IsValidSlot(ne))
            return false;
        if (ni == "")
        {
            ClearEntrySlot(ne);
            return true;
        }
        var ei = _normalize_equipment_instance(equipment_instance, ni);
        if (ei == null)
            return false;
        var entry = new EquipmentEntryState();
        if (!entry.SetEquipmentInstance(ei))
            return false;
        entry.occupied_slot_ids = _normalize_occupied_slot_ids(ne, occupied);
        _store_entry(ne, entry);
        return true;
    }

    public void ClearSlot(StringName slot_id)
    {
        var es = ResolveEntrySlotForSlot(ProgressionDataUtils.to_string_name(slot_id));
        if (es != "")
            ClearEntrySlot(es);
    }

    public void ClearEntrySlot(StringName entry_slot_id)
    {
        var n = ProgressionDataUtils.to_string_name(entry_slot_id);
        var entry = GetEntry(n);
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

    public EquipmentInstanceState PopEquippedInstance(StringName entry_slot_id)
    {
        var ne = ProgressionDataUtils.to_string_name(entry_slot_id);
        var entry = GetEntry(ne);
        if (entry == null)
            return null;
        if (entry.item_id == "")
        {
            ClearEntrySlot(ne);
            return null;
        }
        var inst = entry.GetEquipmentInstance();
        if (inst == null)
        {
            ClearEntrySlot(ne);
            return null;
        }
        ClearEntrySlot(ne);
        return inst;
    }

    private StringName ResolveEntrySlotForSlot(StringName slot_id)
    {
        var n = ProgressionDataUtils.to_string_name(slot_id);
        if (!EquipmentRules.IsValidSlot(n))
            return new StringName("");
        return _slot_to_entry_slot.TryGetValue(n, out StringName ev) ? ev : new StringName("");
    }

    public StringName GetEntrySlotForSlot(StringName slot_id) =>
        ResolveEntrySlotForSlot(slot_id);

    public IReadOnlyList<StringName> GetEntrySlotIdsTyped()
    {
        var r = new List<StringName>();
        foreach (var sid in EquipmentRules.GetAllSlotIdsTyped())
        {
            var e = GetEntry(sid);
            if (e != null && !e.IsEmpty())
                r.Add(sid);
        }
        return r;
    }

    public IReadOnlyList<StringName> GetFilledSlotIdsTyped()
    {
        var r = new List<StringName>();
        foreach (var sid in EquipmentRules.GetAllSlotIdsTyped())
            if (ResolveEntrySlotForSlot(sid) != "")
                r.Add(sid);
        return r;
    }

    public int GetEquippedCount() => GetEntrySlotIdsTyped().Count;

    public EquipmentState DuplicateState()
    {
        var state = new EquipmentState();
        foreach (StringName entrySlotId in GetEntrySlotIdsTyped())
        {
            EquipmentEntryState entry = GetEntry(entrySlotId)?.DuplicateState();
            if (entry == null || entry.IsEmpty())
                continue;
            state._equipped_slots[entrySlotId] = entry;
            state._register_entry_slots(entrySlotId, entry);
        }
        return state;
    }

    public Godot.Collections.Dictionary ToDictionary()
    {
        var sd = new Godot.Collections.Dictionary();
        foreach (var es in GetEntrySlotIdsTyped())
        {
            var e = GetEntry(es);
            if (e != null)
                sd[es.ToString()] = e.ToDictionary();
        }
        return new Godot.Collections.Dictionary { { "equipped_slots", sd } };
    }

    public static EquipmentState FromDictionary(Godot.Collections.Dictionary data)
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
            if (key.VariantType != Variant.Type.String)
                return null;
            string slotText = key.AsString().StripEdges();
            if (slotText.Length == 0)
                return null;
            var slot_id = new StringName(slotText);
            if (
                !EquipmentRules.IsValidSlot(slot_id)
                || !seenEntries.Add(slot_id)
            )
                return null;
            var entryValue = sdd[key];
            if (entryValue.VariantType != Variant.Type.Dictionary)
                return null;
            var entry = EquipmentEntryState.FromDictionary(entryValue.AsGodotDictionary());
            if (entry == null || entry.IsEmpty())
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
                if (!EquipmentRules.IsValidSlot(sid) || !seen.Add(sid))
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
        return equipment_instance.item_id == ni ? equipment_instance.DuplicateState() : null;
    }

    private void _store_entry(StringName entry_slot_id, EquipmentEntryState entry)
    {
        var n = ProgressionDataUtils.to_string_name(entry_slot_id);
        ClearEntrySlot(n);
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
                ClearEntrySlot(existingEntry);
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
        return entry == null || entry.IsEmpty() ? null : entry;
    }
}
