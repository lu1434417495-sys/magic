using Godot;

[GlobalClass]
public partial class EquipmentState : RefCounted
{
    private static readonly GDScript EquipmentEntryStateScript = GD.Load<GDScript>("res://scripts/player/equipment/equipment_entry_state.gd");
    private static readonly GDScript EquipmentInstanceStateScript = GD.Load<GDScript>("res://scripts/player/warehouse/equipment_instance_state.gd");

    public Godot.Collections.Dictionary equipped_slots = new();
    private Godot.Collections.Dictionary _slot_to_entry_slot = new();

    public StringName get_equipped_item_id(StringName slot_id) { var e = get_entry_for_slot(slot_id); return e != null ? e.Get("item_id").AsStringName() : new StringName(""); }
    public StringName get_equipped_instance_id(StringName slot_id) { var e = get_entry_for_slot(slot_id); return e != null ? e.Get("instance_id").AsStringName() : new StringName(""); }

    public GodotObject get_equipped_instance(StringName slot_id)
    {
        var e = get_entry_for_slot(slot_id);
        return e != null && e.HasMethod("get_equipment_instance") ? e.Call("get_equipment_instance").AsGodotObject() : null;
    }

    public GodotObject get_entry(StringName entry_slot_id)
    {
        var n = ProgressionDataUtils.to_string_name(entry_slot_id);
        if (!equipped_slots.ContainsKey(n)) return null;
        return _normalize_entry_variant(equipped_slots[n], n);
    }

    public GodotObject get_entry_for_slot(StringName slot_id)
    {
        var es = _get_entry_slot_for_slot(slot_id);
        return (string)es == "" ? null : get_entry(es);
    }

    public Godot.Collections.Array<StringName> get_occupied_slot_ids_for_entry(StringName entry_slot_id)
    {
        var e = get_entry(entry_slot_id);
        if (e != null) { var occ = e.Get("occupied_slot_ids").AsGodotArray(); var r = new Godot.Collections.Array<StringName>(); foreach (var o in occ) r.Add(ProgressionDataUtils.to_string_name(o)); return r; }
        return new Godot.Collections.Array<StringName>();
    }

    public bool set_equipped_entry(StringName entry_slot_id, StringName item_id, Godot.Collections.Array<StringName> occupied, Variant equipment_instance_variant = default)
    {
        var ne = ProgressionDataUtils.to_string_name(entry_slot_id);
        var ni = ProgressionDataUtils.to_string_name(item_id);
        if (!EquipmentRules.is_valid_slot(ne)) return false;
        if ((string)ni == "") { clear_entry_slot(ne); return true; }
        var ei = _normalize_equipment_instance_variant(equipment_instance_variant, ni);
        if (ei == null) return false;
        var entry = EquipmentEntryStateScript.New().AsGodotObject();
        if (!(bool)entry.Call("set_equipment_instance", ei)) return false;
        entry.Set("occupied_slot_ids", Variant.From(_normalize_occupied_slot_ids(ne, occupied)));
        _store_entry(ne, entry);
        return true;
    }

    public void clear_slot(StringName slot_id)
    {
        var es = _get_entry_slot_for_slot(ProgressionDataUtils.to_string_name(slot_id));
        if ((string)es != "") clear_entry_slot(es);
    }

    public void clear_entry_slot(StringName entry_slot_id)
    {
        var n = ProgressionDataUtils.to_string_name(entry_slot_id);
        var entry = get_entry(n);
        if (entry != null)
        {
            var occ = entry.Get("occupied_slot_ids").AsGodotArray();
            foreach (var o in occ)
            {
                var os = ProgressionDataUtils.to_string_name(o);
                if (_slot_to_entry_slot.ContainsKey(os) && ProgressionDataUtils.to_string_name(_slot_to_entry_slot[os]) == n)
                    _slot_to_entry_slot.Remove(os);
            }
        }
        equipped_slots.Remove(n);
    }

    public GodotObject pop_equipped_instance(StringName entry_slot_id)
    {
        var ne = ProgressionDataUtils.to_string_name(entry_slot_id);
        var entry = get_entry(ne);
        if (entry == null) return null;
        if ((string)entry.Get("item_id").AsStringName() == "") { clear_entry_slot(ne); return null; }
        var inst = entry.Call("get_equipment_instance").AsGodotObject();
        if (inst == null) { clear_entry_slot(ne); return null; }
        clear_entry_slot(ne);
        return inst;
    }

    private StringName _get_entry_slot_for_slot(StringName slot_id)
    {
        var n = ProgressionDataUtils.to_string_name(slot_id);
        if (!EquipmentRules.is_valid_slot(n)) return new StringName("");
        return ProgressionDataUtils.to_string_name(_slot_to_entry_slot.ContainsKey(n) ? _slot_to_entry_slot[n] : new StringName(""));
    }

    public Godot.Collections.Array<StringName> get_entry_slot_ids()
    {
        var r = new Godot.Collections.Array<StringName>();
        foreach (var sid in EquipmentRules.get_all_slot_ids())
        {
            var e = get_entry(sid);
            if (e != null && !(bool)e.Call("is_empty")) r.Add(sid);
        }
        return r;
    }

    public Godot.Collections.Array<StringName> get_filled_slot_ids()
    {
        var r = new Godot.Collections.Array<StringName>();
        foreach (var sid in EquipmentRules.get_all_slot_ids())
            if ((string)_get_entry_slot_for_slot(sid) != "") r.Add(sid);
        return r;
    }

    public int get_equipped_count() => get_entry_slot_ids().Count;

    public EquipmentState duplicate_state() => from_dict(to_dict());

    public Godot.Collections.Dictionary to_dict()
    {
        var sd = new Godot.Collections.Dictionary();
        foreach (var es in get_entry_slot_ids()) { var e = get_entry(es); if (e != null) sd[(string)es] = e.Call("to_dict"); }
        return new Godot.Collections.Dictionary { {"equipped_slots", sd} };
    }

    public static EquipmentState from_dict(Variant data)
    {
        if (data.VariantType != Variant.Type.Dictionary) return null;
        var d = data.AsGodotDictionary();
        if (d.Count != 1 || !d.ContainsKey("equipped_slots")) return null;
        var sd = d["equipped_slots"];
        if (sd.VariantType != Variant.Type.Dictionary) return null;
        var sdd = sd.AsGodotDictionary();
        var state = new EquipmentState();
        var seen_entry = new Godot.Collections.Dictionary();
        var used_occ = new Godot.Collections.Dictionary();
        foreach (var key in sdd.Keys)
        {
            if (!_is_string_name_payload_value(key)) return null;
            var slot_id = ProgressionDataUtils.to_string_name(key);
            if ((string)slot_id == "" || !EquipmentRules.is_valid_slot(slot_id) || seen_entry.ContainsKey(slot_id)) return null;
            var entry = EquipmentEntryStateScript.Call("from_dict", sdd[key]).AsGodotObject();
            if (entry == null || (bool)entry.Call("is_empty")) return null;
            var occ = entry.Get("occupied_slot_ids").AsGodotArray();
            bool hasSlot = false;
            foreach (var os in occ) if (ProgressionDataUtils.to_string_name(os) == slot_id) { hasSlot = true; break; }
            if (!hasSlot) return null;
            foreach (var os in occ) { var osn = ProgressionDataUtils.to_string_name(os); if (used_occ.ContainsKey(osn)) return null; used_occ[osn] = slot_id; }
            seen_entry[slot_id] = true;
            state.equipped_slots[slot_id] = entry;
            state._register_entry_slots(slot_id, entry);
        }
        return state;
    }

    private GodotObject _normalize_entry_variant(Variant ev, StringName entry_slot_id)
    {
        if (ev.VariantType == Variant.Type.Object && ev.AsGodotObject().HasMethod("to_dict") && ev.AsGodotObject().HasMethod("is_empty")) return ev.AsGodotObject();
        var entry = EquipmentEntryStateScript.Call("from_dict", ev).AsGodotObject();
        if (entry == null || (bool)entry.Call("is_empty")) { equipped_slots.Remove(entry_slot_id); _rebuild_slot_lookup(); return null; }
        equipped_slots[entry_slot_id] = entry;
        _register_entry_slots(entry_slot_id, entry);
        return entry;
    }

    private Godot.Collections.Array<StringName> _normalize_occupied_slot_ids(StringName entry_slot_id, Godot.Collections.Array<StringName> occupied)
    {
        var validated = new Godot.Collections.Array<StringName>();
        foreach (var rs in occupied) { var sid = ProgressionDataUtils.to_string_name(rs); if (!EquipmentRules.is_valid_slot(sid) || validated.Contains(sid)) continue; validated.Add(sid); }
        if (validated.Count == 0) validated.Add(entry_slot_id);
        else if (!validated.Contains(entry_slot_id)) validated.Insert(0, entry_slot_id);
        return validated;
    }

    private GodotObject _normalize_equipment_instance_variant(Variant eiv, StringName item_id)
    {
        if (eiv.VariantType == Variant.Type.Nil) return null;
        var ni = ProgressionDataUtils.to_string_name(item_id);
        if (eiv.VariantType == Variant.Type.Object && eiv.AsGodotObject().HasMethod("to_dict")) { var ni2 = EquipmentInstanceStateScript.Call("from_dict", eiv.AsGodotObject().Call("to_dict")).AsGodotObject(); return ni2 != null && ni2.Get("item_id").AsStringName() == ni ? ni2 : null; }
        if (eiv.VariantType == Variant.Type.Dictionary) { var di = EquipmentInstanceStateScript.Call("from_dict", eiv).AsGodotObject(); return di != null && di.Get("item_id").AsStringName() == ni ? di : null; }
        return null;
    }

    private void _store_entry(StringName entry_slot_id, GodotObject entry)
    {
        var n = ProgressionDataUtils.to_string_name(entry_slot_id);
        clear_entry_slot(n);
        var ne = _normalize_entry_variant(Variant.From(entry), n);
        if (ne == null) return;
        var occ = ne.Get("occupied_slot_ids").AsGodotArray();
        foreach (var os in occ) { var osn = ProgressionDataUtils.to_string_name(os); if (_slot_to_entry_slot.ContainsKey(osn) && ProgressionDataUtils.to_string_name(_slot_to_entry_slot[osn]) != n) clear_entry_slot(ProgressionDataUtils.to_string_name(_slot_to_entry_slot[osn])); }
        equipped_slots[n] = ne;
        _register_entry_slots(n, ne);
    }

    private void _register_entry_slots(StringName entry_slot_id, GodotObject entry)
    {
        if (entry == null) return;
        _slot_to_entry_slot[entry_slot_id] = Variant.From(entry_slot_id);
        var occ = entry.Get("occupied_slot_ids").AsGodotArray();
        foreach (var os in occ) _slot_to_entry_slot[ProgressionDataUtils.to_string_name(os)] = Variant.From(entry_slot_id);
    }

    private void _rebuild_slot_lookup()
    {
        _slot_to_entry_slot.Clear();
        foreach (var ev in equipped_slots.Keys)
        {
            var esid = ProgressionDataUtils.to_string_name(ev);
            var entry = _normalize_entry_variant(equipped_slots[ev], esid);
            if (entry != null) _register_entry_slots(esid, entry);
        }
    }

    private static bool _is_string_name_payload_value(Variant v) => v.VariantType == Variant.Type.String || v.VariantType == Variant.Type.StringName;
}
