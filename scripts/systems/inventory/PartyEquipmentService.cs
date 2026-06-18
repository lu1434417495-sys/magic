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
    private bool _ownsWarehouseService;

    internal sealed class EquipmentDisplacedEntry
    {
        public StringName EntrySlotId { get; init; } = "";
        public StringName ItemId { get; init; } = "";
        public StringName InstanceId { get; init; } = "";

    }

    internal sealed class EquipmentEquipPreviewResult
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

    internal sealed class EquipmentActionResult
    {
        public readonly bool Success;
        public readonly StringName MemberId;
        public readonly StringName SlotId;
        public readonly string SlotLabel;
        public readonly StringName ItemId;
        public readonly StringName InstanceId;
        public readonly StringName PreviousItemId;
        public readonly StringName PreviousInstanceId;
        public readonly string ErrorCode;
        public readonly List<EquipmentDisplacedEntry> DisplacedEntries;

        public EquipmentActionResult(
            bool success,
            StringName memberId,
            StringName slotId,
            StringName itemId,
            StringName instanceId,
            StringName previousItemId,
            StringName previousInstanceId,
            string errorCode,
            IEnumerable<EquipmentDisplacedEntry> displacedEntries = null
        )
        {
            Success = success;
            MemberId = ProgressionDataUtils.to_string_name(memberId);
            SlotId = ProgressionDataUtils.to_string_name(slotId);
            SlotLabel = EquipmentRules.GetSlotLabel(SlotId);
            ItemId = ProgressionDataUtils.to_string_name(itemId);
            InstanceId = ProgressionDataUtils.to_string_name(instanceId);
            PreviousItemId = ProgressionDataUtils.to_string_name(previousItemId);
            PreviousInstanceId = ProgressionDataUtils.to_string_name(previousInstanceId);
            ErrorCode = errorCode ?? "";
            DisplacedEntries =
                displacedEntries != null
                    ? new List<EquipmentDisplacedEntry>(displacedEntries)
                    : new List<EquipmentDisplacedEntry>();
        }
    }

    internal sealed class EquipmentViewEntry
    {
        public StringName SlotId { get; init; } = "";
        public string SlotLabel { get; init; } = "";
        public StringName ItemId { get; init; } = "";
        public StringName InstanceId { get; init; } = "";
        public StringName EquipmentTypeId { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string Icon { get; init; } = "";
        public string Description { get; init; } = "";
    }

    public PartyEquipmentService()
    {
        _party_state = new PartyState();
        _warehouse_service = new PartyWarehouseService();
        _ownsWarehouseService = true;
    }

    public void Setup(
        PartyState partyState,
        IReadOnlyDictionary<StringName, ItemDef> itemDefs,
        PartyWarehouseService warehouseService = null,
        Func<StringName> equipmentInstanceIdAllocator = null
    )
    {
        ReleaseOwnedWarehouseService();
        _party_state = partyState ?? new PartyState();
        _item_defs =
            itemDefs != null ? new Dictionary<StringName, ItemDef>(itemDefs) : new Dictionary<StringName, ItemDef>();
        _warehouse_service = warehouseService ?? new PartyWarehouseService();
        _ownsWarehouseService = warehouseService == null;
        _warehouse_service.Setup(_party_state, _item_defs, equipmentInstanceIdAllocator);
    }

    public void Dispose()
    {
        ReleaseOwnedWarehouseService();
        _warehouse_service = null;
        _party_state = null;
        _item_defs.Clear();
    }

    public ItemDef GetItemDef(StringName itemId)
    {
        var n = ProgressionDataUtils.to_string_name(itemId);
        return n != "" && _item_defs.TryGetValue(n, out var itemDef) ? itemDef : null;
    }

    public EquipmentState GetEquipmentState(StringName memberId)
    {
        var ms = _GetMemberState(memberId);
        return ms != null ? _ensure_equipment_state(ms) : new EquipmentState();
    }

    internal List<EquipmentViewEntry> GetEquippedEntriesTyped(StringName memberId)
    {
        var entries = new List<EquipmentViewEntry>();
        var es = GetEquipmentState(memberId);
        foreach (var slotId in EquipmentRules.GetAllSlotIdsTyped())
        {
            var itemId = es.GetEquippedItemId(slotId);
            if (itemId == "")
                continue;
            var itemDef = GetItemDef(itemId);
            entries.Add(
                new EquipmentViewEntry
                {
                    SlotId = slotId,
                    SlotLabel = EquipmentRules.GetSlotLabel(slotId),
                    ItemId = itemId,
                    InstanceId = es.GetEquippedInstanceId(slotId),
                    EquipmentTypeId = itemDef?.GetEquipmentTypeIdNormalized() ?? "",
                    DisplayName = itemDef != null && itemDef.display_name.Length > 0
                        ? itemDef.display_name
                        : itemId.ToString(),
                    Icon = itemDef?.icon ?? "",
                    Description = itemDef?.description ?? "",
                }
            );
        }
        return entries;
    }

    internal List<AttributeModifier> BuildAttributeModifiersTyped(
        EquipmentState equipmentState
    )
    {
        var r = new List<AttributeModifier>();
        if (equipmentState == null)
            return r;
        foreach (var esId in equipmentState.GetEntrySlotIdsTyped())
        {
            var itemId = equipmentState.GetEquippedItemId(esId);
            var id = GetItemDef(itemId);
            if (id == null || !id.IsEquipment())
                continue;
            foreach (var m in id.GetAttributeModifiersTyped())
                if (m != null)
                    r.Add(m);
            _append_armor_max_dex_modifier(r, id);
        }
        return r;
    }

    internal EquipmentEquipPreviewResult PreviewEquipTyped(
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
        var ms = _GetMemberState(nm);
        if (ms == null)
            return EquipmentEquipPreviewResult.Failed(
                "",
                Array.Empty<StringName>(),
                new List<EquipmentDisplacedEntry>(),
                "member_not_found"
            );
        var id = GetItemDef(ni);
        if (id == null)
            return EquipmentEquipPreviewResult.Failed(
                "",
                Array.Empty<StringName>(),
                new List<EquipmentDisplacedEntry>(),
                "item_not_found"
            );
        if (!id.IsEquipment())
            return EquipmentEquipPreviewResult.Failed(
                "",
                Array.Empty<StringName>(),
                new List<EquipmentDisplacedEntry>(),
                "item_not_equipment"
            );
        if (_warehouse_service == null || _warehouse_service.CountItem(ni) <= 0)
            return EquipmentEquipPreviewResult.Failed(
                "",
                Array.Empty<StringName>(),
                new List<EquipmentDisplacedEntry>(),
                "warehouse_missing_item"
            );
        if (ninst != "")
        {
            if (!_warehouse_service.HasEquipmentInstance(ninst, ni))
            {
                if (_warehouse_service.HasEquipmentInstance(ninst))
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
        else if (_warehouse_service.CountItem(ni) > 1)
            return EquipmentEquipPreviewResult.Failed(
                "",
                Array.Empty<StringName>(),
                new List<EquipmentDisplacedEntry>(),
                "equipment_instance_id_required"
            );
        var allowedSlots = id.GetEquipmentSlotIdsTyped();
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
        var occupiedSlots = id.GetFinalOccupiedSlotIdsTyped(entrySlot);
        var displaced = new List<EquipmentDisplacedEntry>();
        foreach (var ees in es.GetEntrySlotIdsTyped())
        {
            var eid = es.GetEquippedItemId(ees);
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
                        InstanceId = es.GetEquippedInstanceId(ees),
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

    internal EquipmentActionResult EquipItemTyped(StringName memberId, StringName itemId) =>
        EquipItemTyped(memberId, itemId, default, default);

    internal EquipmentActionResult EquipItemTyped(
        StringName memberId,
        StringName itemId,
        StringName requestedSlotId
    ) =>
        EquipItemTyped(memberId, itemId, requestedSlotId, default);

    internal EquipmentActionResult EquipItemTyped(
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
        var ms = _GetMemberState(nm);
        if (ms == null)
            return _build_result(false, nm, "", ni, "", "member_not_found", ninst);
        var es = _ensure_equipment_state(ms);
        if (es == null)
            return _build_result(false, nm, "", ni, "", "equipment_state_invalid", ninst);
        var entrySlot = preview.EntrySlotId;
        var occSlots = new List<StringName>(preview.OccupiedSlotIds);
        var newInst =
            ninst != ""
                ? _warehouse_service.TakeEquipmentInstanceByInstanceId(ninst, ni)
                : _warehouse_service.TakeEquipmentInstanceByItem(ni);
        if (newInst == null)
            return _build_result(false, nm, entrySlot, ni, "", "warehouse_missing_instance", ninst);
        foreach (var displacedEntry in preview.DisplacedEntries)
        {
            var des = displacedEntry.EntrySlotId;
            var docc = es.GetOccupiedSlotIdsForEntryTyped(des);
            var di = es.PopEquippedInstance(des) as EquipmentInstanceState;
            if (di != null)
            {
                if (!_warehouse_service.DepositEquipmentInstance(di))
                {
                    es.SetEquippedEntry(
                        des,
                        displacedEntry.ItemId,
                        docc,
                        di
                    );
                    _warehouse_service.DepositEquipmentInstance(newInst);
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
                es.ClearEntrySlot(des);
            }
        }
        es.SetEquippedEntry(entrySlot, ni, occSlots, newInst);
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
        result.DisplacedEntries.AddRange(preview.DisplacedEntries);
        return result;
    }

    internal EquipmentActionResult UnequipItemTyped(StringName memberId, StringName slotId)
    {
        var nm = ProgressionDataUtils.to_string_name(memberId);
        var ns = ProgressionDataUtils.to_string_name(slotId);
        var ms = _GetMemberState(nm);
        if (ms == null)
            return _build_result(false, nm, ns, "", "", "member_not_found");
        if (!EquipmentRules.IsValidSlot(ns))
            return _build_result(false, nm, ns, "", "", "slot_invalid");
        var es = _ensure_equipment_state(ms);
        var ci = es.GetEquippedItemId(ns);
        if (ci == "")
            return _build_result(false, nm, ns, "", "", "slot_empty");
        var id = GetItemDef(ci);
        if (id != null)
        {
            var pr = _warehouse_service.PreviewAddItemTyped(ci, 1);
            if (pr.RemainingQuantity > 0)
                return _build_result(false, nm, ns, ci, "", "warehouse_full");
        }
        else if (_warehouse_service.GetTotalCapacity() - _warehouse_service.GetUsedSlots() <= 0)
            return _build_result(false, nm, ns, ci, "", "warehouse_full");
        var entrySlot = es.GetEntrySlotForSlot(ns);
        var occBefore = es.GetOccupiedSlotIdsForEntryTyped(entrySlot);
        var inst = es.PopEquippedInstance(entrySlot) as EquipmentInstanceState;
        if (inst != null)
        {
            if (!_warehouse_service.DepositEquipmentInstance(inst))
            {
                es.SetEquippedEntry(entrySlot, ci, occBefore, inst);
                return _build_result(false, nm, ns, ci, "", "warehouse_deposit_failed");
            }
        }
        else
            es.ClearSlot(ns);
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

    private PartyMemberState _GetMemberState(StringName memberId) =>
        _party_state?.GetMemberState(ProgressionDataUtils.to_string_name(memberId));

    private static EquipmentState _ensure_equipment_state(PartyMemberState ms)
    {
        if (ms == null)
            return new EquipmentState();
        if (ms.equipment_state == null)
            ms.equipment_state = new EquipmentState();
        return ms.equipment_state;
    }

    private void _append_armor_max_dex_modifier(
        List<AttributeModifier> mods,
        ItemDef id
    )
    {
        if (id == null || !id.IsArmor())
            return;
        int mdb = id.GetMaxDexBonus();
        if (mdb < 0)
            return;
        mods.Add(
            new AttributeModifier
            {
                attribute_id = AttributeService.ToStringName(AttributeIdKind.ArmorMaxDexBonus),
                mode = AttributeModifier.ToStringName(AttributeModifierMode.Flat),
                value = mdb,
                source_type = "equipment",
                source_id = id.item_id,
            }
        );
    }

    private static StringName _resolve_target_slot(
        IReadOnlyList<StringName> allowed,
        EquipmentState es
    )
    {
        foreach (var s in allowed)
            if (es.GetEquippedItemId(s) == "" && es.GetEntrySlotForSlot(s) == "")
                return s;
        return allowed.Count > 0 ? allowed[0] : new StringName("");
    }

    private static EquipmentActionResult _build_result(
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
        return new EquipmentActionResult(success, mid, sid, iid, instId, pid, prevInstId, ec);
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

    private void ReleaseOwnedWarehouseService()
    {
        if (
            !_ownsWarehouseService
            || _warehouse_service == null
        )
        {
            _ownsWarehouseService = false;
            return;
        }

        _warehouse_service.Dispose();
        _ownsWarehouseService = false;
    }
}
