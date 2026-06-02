using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public class PartyEquipmentService
{
    private PartyState _party_state;
    private Dictionary<StringName, ItemDef> _item_defs = new();
    private PartyWarehouseService _warehouse_service;

    private sealed class EquipmentDisplacedEntry
    {
        public StringName EntrySlotId { get; init; } = "";
        public StringName ItemId { get; init; } = "";
        public StringName InstanceId { get; init; } = "";

        public Godot.Collections.Dictionary ToDictionary() =>
            new()
            {
                { "entry_slot_id", EntrySlotId.ToString() },
                { "item_id", ItemId.ToString() },
                { "instance_id", InstanceId.ToString() },
            };
    }

    private sealed class EquipmentEquipPreviewResult
    {
        public readonly bool Success;
        public readonly string ErrorCode;
        public readonly StringName EntrySlotId;
        public readonly StringName InstanceId;
        public readonly List<string> Blockers;
        public readonly List<StringName> OccupiedSlotIds;
        public readonly List<EquipmentDisplacedEntry> DisplacedEntries;

        private EquipmentEquipPreviewResult(
            bool success,
            string errorCode,
            StringName entrySlotId,
            StringName instanceId,
            IEnumerable<string> blockers,
            IEnumerable<StringName> occupiedSlotIds,
            List<EquipmentDisplacedEntry> displacedEntries
        )
        {
            Success = success;
            ErrorCode = errorCode ?? "";
            EntrySlotId = ProgressionDataUtils.to_string_name(entrySlotId);
            InstanceId = ProgressionDataUtils.to_string_name(instanceId);
            Blockers = blockers != null ? new List<string>(blockers) : new List<string>();
            OccupiedSlotIds = occupiedSlotIds != null
                ? new List<StringName>(occupiedSlotIds)
                : new List<StringName>();
            DisplacedEntries = displacedEntries != null
                ? new List<EquipmentDisplacedEntry>(displacedEntries)
                : new List<EquipmentDisplacedEntry>();
        }

        public GStringNameArray CloneOccupiedSlotIds() =>
            new(OccupiedSlotIds);

        public Godot.Collections.Array ToDisplacedDictionaryArray()
        {
            var result = new Godot.Collections.Array();
            foreach (var entry in DisplacedEntries)
                result.Add(entry.ToDictionary());
            return result;
        }

        public Godot.Collections.Dictionary ToDictionary()
        {
            var result = new Godot.Collections.Dictionary
            {
                { "success", Success },
                { "error_code", ErrorCode },
                { "blockers", new Godot.Collections.Array<string>(Blockers) },
                { "entry_slot_id", EntrySlotId.ToString() },
                { "occupied_slot_ids", StringNameArrayToStringArray(OccupiedSlotIds) },
                { "displaced_entries", ToDisplacedDictionaryArray() },
            };
            if (Success)
                result["instance_id"] = InstanceId.ToString();
            return result;
        }

        public static EquipmentEquipPreviewResult SuccessResult(
            StringName entrySlotId,
            StringName instanceId,
            IEnumerable<StringName> occupiedSlotIds,
            List<EquipmentDisplacedEntry> displacedEntries
        ) =>
            new(
                true,
                "",
                entrySlotId,
                instanceId,
                Array.Empty<string>(),
                occupiedSlotIds,
                displacedEntries
            );

        public static EquipmentEquipPreviewResult Failed(
            StringName entrySlotId,
            IEnumerable<StringName> occupiedSlotIds,
            List<EquipmentDisplacedEntry> displacedEntries,
            string errorCode,
            IEnumerable<string> blockers = null
        ) =>
            new(
                false,
                errorCode,
                entrySlotId,
                "",
                blockers,
                occupiedSlotIds,
                displacedEntries
            );
    }

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
        _item_defs = MaterializeItemDefs(itemDefs);
        _warehouse_service = warehouseService ?? new PartyWarehouseService();
        _warehouse_service.setup(_party_state, itemDefs, equipmentInstanceIdAllocator);
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
        return n != "" && _item_defs.TryGetValue(n, out var itemDef) ? itemDef : null;
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
        foreach (var slotId in EquipmentRules.GetAllSlotIdsTyped())
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
        foreach (var esId in equipmentState.GetEntrySlotIdsTyped())
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

    public Godot.Collections.Dictionary preview_equip(StringName memberId, StringName itemId) =>
        preview_equip(memberId, itemId, default, default);

    public Godot.Collections.Dictionary preview_equip(
        StringName memberId,
        StringName itemId,
        StringName requestedSlotId
    ) =>
        preview_equip(memberId, itemId, requestedSlotId, default);

    public Godot.Collections.Dictionary preview_equip(
        StringName memberId,
        StringName itemId,
        StringName requestedSlotId,
        StringName instanceId
    ) =>
        PreviewEquipTyped(memberId, itemId, requestedSlotId, instanceId).ToDictionary();

    private EquipmentEquipPreviewResult PreviewEquipTyped(
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
            return EquipmentEquipPreviewResult.Failed(
                "",
                Array.Empty<StringName>(),
                new List<EquipmentDisplacedEntry>(),
                "member_not_found"
            );
        var id = get_item_def(ni);
        if (id == null)
            return EquipmentEquipPreviewResult.Failed(
                "",
                Array.Empty<StringName>(),
                new List<EquipmentDisplacedEntry>(),
                "item_not_found"
            );
        if (!id.is_equipment())
            return EquipmentEquipPreviewResult.Failed(
                "",
                Array.Empty<StringName>(),
                new List<EquipmentDisplacedEntry>(),
                "item_not_equipment"
            );
        if (_warehouse_service == null || _warehouse_service.count_item(ni) <= 0)
            return EquipmentEquipPreviewResult.Failed(
                "",
                Array.Empty<StringName>(),
                new List<EquipmentDisplacedEntry>(),
                "warehouse_missing_item"
            );
        if (ninst != "")
        {
            if (!_warehouse_service.has_equipment_instance(ninst, ni))
            {
                if (_warehouse_service.has_equipment_instance(ninst))
                    return EquipmentEquipPreviewResult.Failed(
                        "",
                        Array.Empty<StringName>(),
                        new List<EquipmentDisplacedEntry>(),
                        "equipment_instance_item_mismatch"
                    );
                return EquipmentEquipPreviewResult.Failed(
                    "",
                    Array.Empty<StringName>(),
                    new List<EquipmentDisplacedEntry>(),
                    "warehouse_missing_instance"
                );
            }
        }
        else if (_warehouse_service.count_item(ni) > 1)
            return EquipmentEquipPreviewResult.Failed(
                "",
                Array.Empty<StringName>(),
                new List<EquipmentDisplacedEntry>(),
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
            return EquipmentEquipPreviewResult.Failed(
                entrySlot,
                Array.Empty<StringName>(),
                new List<EquipmentDisplacedEntry>(),
                entrySlot == "" ? "slot_unresolved" : "slot_not_allowed"
            );
        var occupiedSlots = id.get_final_occupied_slot_ids(entrySlot);
        var displaced = new List<EquipmentDisplacedEntry>();
        foreach (var ees in es.GetEntrySlotIdsTyped())
        {
            var eid = es.get_equipped_item_id(ees);
            if (eid == "")
                continue;
            var eocc = new HashSet<StringName>(es.GetOccupiedSlotIdsForEntryTyped(ees));
            bool conflicts = false;
            foreach (var os in occupiedSlots)
                if (eocc.Contains(ProgressionDataUtils.to_string_name(os)))
                {
                    conflicts = true;
                    break;
                }
            if (conflicts)
                displaced.Add(
                    new EquipmentDisplacedEntry
                    {
                        EntrySlotId = ees,
                        ItemId = eid,
                        InstanceId = es.get_equipped_instance_id(ees),
                    }
                );
        }
        if (id.equip_requirement is EquipmentRequirement eqReq)
        {
            var cr = eqReq.CheckResult(ms);
            if (!cr.Allowed)
            {
                var fcode = cr.Blockers.Count > 0 ? cr.Blockers[0] : "requirement_failed";
                return EquipmentEquipPreviewResult.Failed(
                    entrySlot,
                    occupiedSlots,
                    displaced,
                    fcode,
                    cr.Blockers
                );
            }
        }
        var withdrawalEntries = new List<PartyWarehouseService.WarehouseBatchItemEntry>();
        if (ninst != "")
            withdrawalEntries.Add(
                new PartyWarehouseService.WarehouseBatchItemEntry
                {
                    ItemId = ni,
                    InstanceId = ninst,
                }
            );
        else
            withdrawalEntries.Add(
                new PartyWarehouseService.WarehouseBatchItemEntry
                {
                    ItemId = ni,
                }
            );
        var depositEntries = new List<PartyWarehouseService.WarehouseBatchItemEntry>();
        foreach (var d in displaced)
        {
            var displacedItemId = ProgressionDataUtils.to_string_name(d.ItemId);
            if (displacedItemId == "")
                continue;
            depositEntries.Add(
                new PartyWarehouseService.WarehouseBatchItemEntry
                {
                    ItemId = displacedItemId,
                    InstanceId = ProgressionDataUtils.to_string_name(d.InstanceId),
                }
            );
        }
        var bp = _warehouse_service.PreviewBatchSwapEntriesTyped(
            withdrawalEntries,
            depositEntries
        );
        if (!bp.Allowed)
            return EquipmentEquipPreviewResult.Failed(
                entrySlot,
                occupiedSlots,
                displaced,
                bp.ErrorCode.Length > 0 ? bp.ErrorCode : "warehouse_blocked_swap"
            );
        return EquipmentEquipPreviewResult.SuccessResult(entrySlot, ninst, occupiedSlots, displaced);
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
        var pr = _warehouse_service.PreviewAddItemTyped(ci, 1);
        if (pr.RemainingQuantity > 0)
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

    public Godot.Collections.Dictionary equip_item(StringName memberId, StringName itemId) =>
        equip_item(memberId, itemId, default, default);

    public Godot.Collections.Dictionary equip_item(
        StringName memberId,
        StringName itemId,
        StringName requestedSlotId
    ) =>
        equip_item(memberId, itemId, requestedSlotId, default);

    public Godot.Collections.Dictionary equip_item(
        StringName memberId,
        StringName itemId,
        StringName requestedSlotId,
        StringName instanceId
    )
    {
        var nm = ProgressionDataUtils.to_string_name(memberId);
        var ni = ProgressionDataUtils.to_string_name(itemId);
        var ninst = ProgressionDataUtils.to_string_name(instanceId);
        var preview = PreviewEquipTyped(nm, ni, requestedSlotId, ninst);
        if (!preview.Success)
            return _build_result(
                false,
                nm,
                preview.EntrySlotId,
                ni,
                "",
                preview.ErrorCode.Length > 0 ? preview.ErrorCode : "preview_failed",
                ninst
            );
        var ms = _get_member_state(nm);
        if (ms == null)
            return _build_result(false, nm, "", ni, "", "member_not_found", ninst);
        var es = _ensure_equipment_state(ms);
        if (es == null)
            return _build_result(false, nm, "", ni, "", "equipment_state_invalid", ninst);
        var entrySlot = preview.EntrySlotId;
        var occSlots = new List<StringName>(preview.OccupiedSlotIds);
        var newInst =
            ninst != ""
                ? _warehouse_service.take_equipment_instance_by_instance_id(ninst, ni)
                : _warehouse_service.take_equipment_instance_by_item(ni);
        if (newInst == null)
            return _build_result(false, nm, entrySlot, ni, "", "warehouse_missing_instance", ninst);
        foreach (var displacedEntry in preview.DisplacedEntries)
        {
            var des = displacedEntry.EntrySlotId;
            var docc = es.GetOccupiedSlotIdsForEntryTyped(des);
            var di = es.pop_equipped_instance(des) as EquipmentInstanceState;
            if (di != null)
            {
                if (!_warehouse_service.deposit_equipment_instance(di))
                {
                    es.SetEquippedEntryTyped(
                        des,
                        displacedEntry.ItemId,
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
        es.SetEquippedEntryTyped(entrySlot, ni, occSlots, newInst);
        string prevId = "",
            prevInstId = "";
        if (preview.DisplacedEntries.Count > 0)
        {
            var firstDisplaced = preview.DisplacedEntries[0];
            prevId = firstDisplaced.ItemId.ToString();
            prevInstId = firstDisplaced.InstanceId.ToString();
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
        result["displaced_entries"] = preview.ToDisplacedDictionaryArray();
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
            var pr = _warehouse_service.PreviewAddItemTyped(ci, 1);
            if (pr.RemainingQuantity > 0)
                return _build_result(false, nm, ns, ci, "", "warehouse_full");
        }
        else if (_warehouse_service.get_total_capacity() - _warehouse_service.get_used_slots() <= 0)
            return _build_result(false, nm, ns, ci, "", "warehouse_full");
        var entrySlot = es.get_entry_slot_for_slot(ns);
        var occBefore = es.GetOccupiedSlotIdsForEntryTyped(entrySlot);
        var inst = es.pop_equipped_instance(entrySlot) as EquipmentInstanceState;
        if (inst != null)
        {
            if (!_warehouse_service.deposit_equipment_instance(inst))
            {
                es.SetEquippedEntryTyped(entrySlot, ci, occBefore, inst);
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

    private static Dictionary<StringName, ItemDef> MaterializeItemDefs(GDictionary itemDefs)
    {
        var result = new Dictionary<StringName, ItemDef>();
        if (itemDefs == null)
            return result;

        foreach (Variant rawKey in itemDefs.Keys)
        {
            var itemId = ProgressionDataUtils.to_string_name(rawKey);
            if (itemId == "")
                continue;
            Variant rawValue = itemDefs[rawKey];
            if (rawValue.VariantType == Variant.Type.Object
                && rawValue.AsGodotObject() is ItemDef itemDef)
                result[itemId] = itemDef;
        }
        return result;
    }

    private static Godot.Collections.Array<string> StringNameArrayToStringArray(
        IEnumerable<StringName> values
    )
    {
        var r = new Godot.Collections.Array<string>();
        if (values == null)
            return r;
        foreach (var s in values)
            r.Add((string)s);
        return r;
    }
}
