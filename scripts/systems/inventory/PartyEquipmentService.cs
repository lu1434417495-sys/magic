using System;
using Godot;

[GlobalClass]
public partial class PartyEquipmentService : RefCounted
{
    private PartyState _party_state;
    private Godot.Collections.Dictionary _item_defs = new();
    private PartyWarehouseService _warehouse_service;

    public PartyEquipmentService()
    {
        _party_state = new PartyState();
        _warehouse_service = new PartyWarehouseService();
    }

    public void setup(
        PartyState partyState,
        Godot.Collections.Dictionary itemDefs = null,
        PartyWarehouseService warehouseService = null,
        Func<StringName> equipmentInstanceIdAllocator = null
    )
    {
        _party_state = partyState ?? new PartyState();
        _item_defs = itemDefs ?? new Godot.Collections.Dictionary();
        _warehouse_service = warehouseService ?? new PartyWarehouseService();
        _warehouse_service.setup(_party_state, _item_defs, equipmentInstanceIdAllocator);
    }

    public void setup(
        PartyState partyState,
        Godot.Collections.Dictionary itemDefs,
        PartyWarehouseService warehouseService
    ) => setup(partyState, itemDefs, warehouseService, default);

    public void setup(PartyState partyState, Godot.Collections.Dictionary itemDefs) =>
        setup(partyState, itemDefs, null, default);

    public ItemDef get_item_def(StringName itemId)
    {
        var n = ProgressionDataUtils.to_string_name(itemId);
        return _item_defs.ContainsKey(n) ? _item_defs[n].AsGodotObject() as ItemDef : null;
    }

    public EquipmentState get_equipment_state(StringName memberId)
    {
        var ms = _get_member_state(memberId);
        return ms != null ? _ensure_equipment_state(ms) : new EquipmentState();
    }

    public Godot.Collections.Array<Godot.Collections.Dictionary> get_equipped_entries(
        StringName memberId
    )
    {
        var entries = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        var es = get_equipment_state(memberId);
        foreach (var slotId in EquipmentRules.get_all_slot_ids())
        {
            var itemId = es.get_equipped_item_id(slotId);
            if (itemId == "")
                continue;
            var itemDef = get_item_def(itemId);
            entries.Add(
                new Godot.Collections.Dictionary
                {
                    { "slot_id", (string)slotId },
                    { "slot_label", EquipmentRules.get_slot_label(slotId) },
                    { "item_id", (string)itemId },
                    { "instance_id", (string)es.get_equipped_instance_id(slotId) },
                    {
                        "equipment_type_id",
                        itemDef != null ? (string)itemDef.get_equipment_type_id_normalized() : ""
                    },
                    {
                        "display_name",
                        itemDef != null && itemDef.display_name.Length > 0
                            ? itemDef.display_name
                            : (string)itemId
                    },
                    { "icon", itemDef != null ? itemDef.icon : "" },
                    { "description", itemDef != null ? itemDef.description : "" },
                }
            );
        }
        return entries;
    }

    public Godot.Collections.Array<AttributeModifier> build_attribute_modifiers(
        EquipmentState equipmentState
    )
    {
        var r = new Godot.Collections.Array<AttributeModifier>();
        if (equipmentState == null)
            return r;
        foreach (var esId in equipmentState.get_entry_slot_ids())
        {
            var itemId = equipmentState.get_equipped_item_id(esId);
            var id = get_item_def(itemId);
            if (id == null || !id.is_equipment())
                continue;
            foreach (var m in id.get_attribute_modifiers())
                if (m != null)
                    r.Add(m);
            _append_armor_max_dex_modifier(r, id);
        }
        return r;
    }

    public Godot.Collections.Dictionary preview_equip(
        StringName memberId,
        StringName itemId,
        StringName requestedSlotId = default,
        StringName instanceId = default
    )
    {
        var nm = ProgressionDataUtils.to_string_name(memberId);
        var ni = ProgressionDataUtils.to_string_name(itemId);
        var ns = ProgressionDataUtils.to_string_name(requestedSlotId);
        var ninst = ProgressionDataUtils.to_string_name(instanceId);
        var ms = _get_member_state(nm);
        if (ms == null)
            return _build_preview_fail(
                "",
                new Godot.Collections.Array(),
                new Godot.Collections.Array(),
                "member_not_found"
            );
        var id = get_item_def(ni);
        if (id == null)
            return _build_preview_fail(
                "",
                new Godot.Collections.Array(),
                new Godot.Collections.Array(),
                "item_not_found"
            );
        if (!id.is_equipment())
            return _build_preview_fail(
                "",
                new Godot.Collections.Array(),
                new Godot.Collections.Array(),
                "item_not_equipment"
            );
        if (_warehouse_service == null || _warehouse_service.count_item(ni) <= 0)
            return _build_preview_fail(
                "",
                new Godot.Collections.Array(),
                new Godot.Collections.Array(),
                "warehouse_missing_item"
            );
        if (ninst != "")
        {
            if (!_warehouse_service.has_equipment_instance(ninst, ni))
            {
                if (_warehouse_service.has_equipment_instance(ninst))
                    return _build_preview_fail(
                        "",
                        new Godot.Collections.Array(),
                        new Godot.Collections.Array(),
                        "equipment_instance_item_mismatch"
                    );
                return _build_preview_fail(
                    "",
                    new Godot.Collections.Array(),
                    new Godot.Collections.Array(),
                    "warehouse_missing_instance"
                );
            }
        }
        else if (_warehouse_service.count_item(ni) > 1)
            return _build_preview_fail(
                "",
                new Godot.Collections.Array(),
                new Godot.Collections.Array(),
                "equipment_instance_id_required"
            );
        var allowedSlots = id.get_equipment_slot_ids();
        if (allowedSlots == null)
            allowedSlots = id.get_equipment_slot_ids();
        var es = _ensure_equipment_state(ms);
        var entrySlot = ns;
        if (entrySlot == "")
            entrySlot = _resolve_target_slot(allowedSlots, es);
        if (entrySlot == "" || !allowedSlots.Contains(entrySlot))
            return _build_preview_fail(
                entrySlot,
                new Godot.Collections.Array(),
                new Godot.Collections.Array(),
                entrySlot == "" ? "slot_unresolved" : "slot_not_allowed"
            );
        var occupiedSlots = id.get_final_occupied_slot_ids(entrySlot);
        var displaced = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var ees in es.get_entry_slot_ids())
        {
            var eid = es.get_equipped_item_id(ees);
            if (eid == "")
                continue;
            var eocc = es.get_occupied_slot_ids_for_entry(ees);
            bool conflicts = false;
            foreach (var os in occupiedSlots)
                if (eocc.Contains(os))
                {
                    conflicts = true;
                    break;
                }
            if (conflicts)
                displaced.Add(
                    new Godot.Collections.Dictionary
                    {
                        { "entry_slot_id", (string)ees },
                        { "item_id", (string)eid },
                        { "instance_id", (string)es.get_equipped_instance_id(ees) },
                    }
                );
        }
        if (id.equip_requirement is EquipmentRequirement eqReq)
        {
            var cr = eqReq.Check(ms);
            if (!cr.ContainsKey("allowed") || !cr["allowed"].AsBool())
            {
                var blv = cr.ContainsKey("blockers") ? cr["blockers"] : default(Variant);
                var bl =
                    blv.VariantType == Variant.Type.Array
                        ? blv.AsGodotArray()
                        : new Godot.Collections.Array();
                var fcode = bl.Count > 0 ? bl[0].AsString() : "requirement_failed";
                return _build_preview_fail(entrySlot, occupiedSlots, displaced, fcode, bl);
            }
        }
        var we = new Godot.Collections.Array();
        if (ninst != "")
            we.Add(
                new Godot.Collections.Dictionary
                {
                    { "item_id", (string)ni },
                    { "instance_id", (string)ninst },
                }
            );
        else
            we.Add(ni);
        var itd = new Godot.Collections.Array<StringName>();
        foreach (var d in displaced)
        {
            var dIid = ProgressionDataUtils.to_string_name(
                d.ContainsKey("item_id") ? d["item_id"] : ""
            );
            if (dIid != "")
                itd.Add(dIid);
        }
        var bp = _warehouse_service.preview_batch_swap_entries(
            we,
            Variant.From(itd).AsGodotArray()
        );
        if (!bp.ContainsKey("allowed") || !bp["allowed"].AsBool())
            return _build_preview_fail(
                entrySlot,
                occupiedSlots,
                displaced,
                bp.ContainsKey("error_code")
                    ? bp["error_code"].AsString()
                    : "warehouse_blocked_swap"
            );
        return new Godot.Collections.Dictionary
        {
            { "success", true },
            { "error_code", "" },
            { "blockers", new Godot.Collections.Array<string>() },
            { "entry_slot_id", (string)entrySlot },
            { "instance_id", (string)ninst },
            { "occupied_slot_ids", _sa(occupiedSlots) },
            { "displaced_entries", displaced },
        };
    }

    public Godot.Collections.Dictionary preview_unequip(StringName memberId, StringName slotId)
    {
        var ns = ProgressionDataUtils.to_string_name(slotId);
        var ms = _get_member_state(ProgressionDataUtils.to_string_name(memberId));
        if (ms == null)
            return new Godot.Collections.Dictionary
            {
                { "success", false },
                { "error_code", "member_not_found" },
                { "blockers", new Godot.Collections.Array<string>() },
                { "item_id", "" },
                { "entry_slot_id", "" },
            };
        if (!EquipmentRules.is_valid_slot(ns))
            return new Godot.Collections.Dictionary
            {
                { "success", false },
                { "error_code", "slot_invalid" },
                { "blockers", new Godot.Collections.Array<string>() },
                { "item_id", "" },
                { "entry_slot_id", "" },
            };
        var es = _ensure_equipment_state(ms);
        var ci = es.get_equipped_item_id(ns);
        if (ci == "")
            return new Godot.Collections.Dictionary
            {
                { "success", false },
                { "error_code", "slot_empty" },
                { "blockers", new Godot.Collections.Array<string>() },
                { "item_id", "" },
                { "entry_slot_id", "" },
            };
        var entrySlot = es.get_entry_slot_for_slot(ns);
        var pr = _warehouse_service.preview_add_item(ci, 1);
        if (pr.ContainsKey("remaining_quantity") && pr["remaining_quantity"].AsInt32() > 0)
            return new Godot.Collections.Dictionary
            {
                { "success", false },
                { "error_code", "warehouse_full" },
                {
                    "blockers",
                    new Godot.Collections.Array<string> { "warehouse_full" }
                },
                { "item_id", (string)ci },
                { "entry_slot_id", (string)entrySlot },
            };
        return new Godot.Collections.Dictionary
        {
            { "success", true },
            { "error_code", "" },
            { "blockers", new Godot.Collections.Array<string>() },
            { "item_id", (string)ci },
            { "instance_id", (string)es.get_equipped_instance_id(entrySlot) },
            { "entry_slot_id", (string)entrySlot },
        };
    }

    public Godot.Collections.Dictionary equip_item(
        StringName memberId,
        StringName itemId,
        StringName requestedSlotId = default,
        StringName instanceId = default
    )
    {
        var nm = ProgressionDataUtils.to_string_name(memberId);
        var ni = ProgressionDataUtils.to_string_name(itemId);
        var ninst = ProgressionDataUtils.to_string_name(instanceId);
        var pv = preview_equip(nm, ni, requestedSlotId, ninst);
        if (!pv.ContainsKey("success") || !pv["success"].AsBool())
            return _build_result(
                false,
                nm,
                ProgressionDataUtils.to_string_name(
                    pv.ContainsKey("entry_slot_id") ? pv["entry_slot_id"] : ""
                ),
                ni,
                "",
                pv.ContainsKey("error_code") ? pv["error_code"].AsString() : "preview_failed",
                ninst
            );
        var ms = _get_member_state(nm);
        if (ms == null)
            return _build_result(false, nm, "", ni, "", "member_not_found", ninst);
        var es = _ensure_equipment_state(ms);
        if (es == null)
            return _build_result(false, nm, "", ni, "", "equipment_state_invalid", ninst);
        var entrySlot = ProgressionDataUtils.to_string_name(
            pv.ContainsKey("entry_slot_id") ? pv["entry_slot_id"] : ""
        );
        var occSlots = ProgressionDataUtils.to_string_name_array(
            pv.ContainsKey("occupied_slot_ids")
                ? pv["occupied_slot_ids"]
                : new Godot.Collections.Array()
        );
        var newInst =
            ninst != ""
                ? _warehouse_service.take_equipment_instance_by_instance_id(ninst, ni)
                : _warehouse_service.take_equipment_instance_by_item(ni);
        if (newInst == null)
            return _build_result(false, nm, entrySlot, ni, "", "warehouse_missing_instance", ninst);
        foreach (
            var d in pv.ContainsKey("displaced_entries")
                ? pv["displaced_entries"].AsGodotArray()
                : new Godot.Collections.Array()
        )
        {
            var dd = d.AsGodotDictionary();
            var des = ProgressionDataUtils.to_string_name(
                dd.ContainsKey("entry_slot_id") ? dd["entry_slot_id"] : ""
            );
            var docc = es.get_occupied_slot_ids_for_entry(des);
            var di = es.pop_equipped_instance(des) as EquipmentInstanceState;
            if (di != null)
            {
                if (!_warehouse_service.deposit_equipment_instance(di))
                {
                    es.set_equipped_entry(
                        des,
                        ProgressionDataUtils.to_string_name(
                            dd.ContainsKey("item_id") ? dd["item_id"] : ""
                        ),
                        docc,
                        di
                    );
                    _warehouse_service.deposit_equipment_instance(newInst);
                    return _build_result(
                        false,
                        nm,
                        entrySlot,
                        ni,
                        "",
                        "warehouse_deposit_failed",
                        ninst
                    );
                }
            }
            else if (des != "")
            {
                es.clear_entry_slot(des);
            }
        }
        es.set_equipped_entry(entrySlot, ni, occSlots, newInst);
        string prevId = "",
            prevInstId = "";
        if (pv.ContainsKey("displaced_entries") && pv["displaced_entries"].AsGodotArray().Count > 0)
        {
            var dd0 = pv["displaced_entries"].AsGodotArray()[0].AsGodotDictionary();
            prevId = (string)
                ProgressionDataUtils.to_string_name(
                    dd0.ContainsKey("item_id") ? dd0["item_id"] : ""
                );
            prevInstId = (string)
                ProgressionDataUtils.to_string_name(
                    dd0.ContainsKey("instance_id") ? dd0["instance_id"] : ""
                );
        }
        var result = _build_result(
            true,
            nm,
            entrySlot,
            ni,
            prevId,
            "equipped",
            newInst.instance_id,
            prevInstId
        );
        result["displaced_entries"] = pv.ContainsKey("displaced_entries")
            ? pv["displaced_entries"].AsGodotArray().Duplicate()
            : new Godot.Collections.Array();
        return result;
    }

    public Godot.Collections.Dictionary unequip_item(StringName memberId, StringName slotId)
    {
        var nm = ProgressionDataUtils.to_string_name(memberId);
        var ns = ProgressionDataUtils.to_string_name(slotId);
        var ms = _get_member_state(nm);
        if (ms == null)
            return _build_result(false, nm, ns, "", "", "member_not_found");
        if (!EquipmentRules.is_valid_slot(ns))
            return _build_result(false, nm, ns, "", "", "slot_invalid");
        var es = _ensure_equipment_state(ms);
        var ci = es.get_equipped_item_id(ns);
        if (ci == "")
            return _build_result(false, nm, ns, "", "", "slot_empty");
        var id = get_item_def(ci);
        if (id != null)
        {
            var pr = _warehouse_service.preview_add_item(ci, 1);
            if (pr.ContainsKey("remaining_quantity") && pr["remaining_quantity"].AsInt32() > 0)
                return _build_result(false, nm, ns, ci, "", "warehouse_full");
        }
        else if (_warehouse_service.get_total_capacity() - _warehouse_service.get_used_slots() <= 0)
            return _build_result(false, nm, ns, ci, "", "warehouse_full");
        var entrySlot = es.get_entry_slot_for_slot(ns);
        var occBefore = es.get_occupied_slot_ids_for_entry(entrySlot);
        var inst = es.pop_equipped_instance(entrySlot) as EquipmentInstanceState;
        if (inst != null)
        {
            if (!_warehouse_service.deposit_equipment_instance(inst))
            {
                es.set_equipped_entry(entrySlot, ci, occBefore, inst);
                return _build_result(false, nm, ns, ci, "", "warehouse_deposit_failed");
            }
        }
        else
            es.clear_slot(ns);
        return _build_result(
            true,
            nm,
            ns,
            ci,
            "",
            "unequipped",
            inst != null ? inst.instance_id : new StringName("")
        );
    }

    private PartyMemberState _get_member_state(StringName memberId) =>
        _party_state?.get_member_state(ProgressionDataUtils.to_string_name(memberId));

    private static EquipmentState _ensure_equipment_state(PartyMemberState ms)
    {
        if (ms == null)
            return new EquipmentState();
        if (ms.equipment_state == null)
            ms.equipment_state = new EquipmentState();
        return ms.equipment_state;
    }

    private void _append_armor_max_dex_modifier(
        Godot.Collections.Array<AttributeModifier> mods,
        ItemDef id
    )
    {
        if (id == null || !id.is_armor())
            return;
        int mdb = id.get_max_dex_bonus();
        if (mdb < 0)
            return;
        mods.Add(
            new AttributeModifier
            {
                attribute_id = AttributeService.ARMOR_MAX_DEX_BONUS_ID(),
                mode = AttributeModifier.MODE_FLAT(),
                value = mdb,
                source_type = "equipment",
                source_id = id.item_id,
            }
        );
    }

    private static StringName _resolve_target_slot(
        Godot.Collections.Array<StringName> allowed,
        EquipmentState es
    )
    {
        foreach (var s in allowed)
            if (es.get_equipped_item_id(s) == "" && es.get_entry_slot_for_slot(s) == "")
                return s;
        return allowed.Count > 0 ? allowed[0] : new StringName("");
    }

    private static Godot.Collections.Dictionary _build_result(
        bool success,
        StringName mid,
        StringName sid,
        StringName iid,
        StringName pid,
        string ec,
        StringName instId = default,
        StringName prevInstId = default
    )
    {
        return new Godot.Collections.Dictionary
        {
            { "success", success },
            { "member_id", (string)mid },
            { "slot_id", (string)sid },
            { "slot_label", EquipmentRules.get_slot_label(sid) },
            { "item_id", (string)iid },
            { "instance_id", (string)instId },
            { "previous_item_id", (string)pid },
            { "previous_instance_id", (string)prevInstId },
            { "error_code", ec },
        };
    }

    private static Godot.Collections.Dictionary _build_preview_fail(
        object es,
        object os,
        object de,
        string ec,
        Godot.Collections.Array blockers = null
    )
    {
        var ostr = new Godot.Collections.Array<string>();
        if (os is System.Collections.IEnumerable occupiedSlots)
        {
            foreach (var s in occupiedSlots)
            {
                var slotId = ProgressionDataUtils.to_string_name(s);
                if (slotId != "")
                    ostr.Add(slotId.ToString());
            }
        }
        var displacedEntries = new Godot.Collections.Array();
        if (de is Godot.Collections.Array rawDisplacedEntries)
        {
            displacedEntries = rawDisplacedEntries;
        }
        else if (de is System.Collections.IEnumerable typedDisplacedEntries)
        {
            foreach (var entry in typedDisplacedEntries)
            {
                if (entry is Variant variantEntry)
                    displacedEntries.Add(variantEntry);
                else if (entry is Godot.Collections.Dictionary dictionaryEntry)
                    displacedEntries.Add(dictionaryEntry);
            }
        }
        return new Godot.Collections.Dictionary
        {
            { "success", false },
            { "error_code", ec },
            { "blockers", blockers != null ? blockers : new Godot.Collections.Array<string>() },
            { "entry_slot_id", ProgressionDataUtils.to_string_name(es).ToString() },
            { "occupied_slot_ids", ostr },
            { "displaced_entries", displacedEntries },
        };
    }

    private static Godot.Collections.Array<string> _sa(Godot.Collections.Array<StringName> a)
    {
        var r = new Godot.Collections.Array<string>();
        foreach (var s in a)
            r.Add((string)s);
        return r;
    }
}
