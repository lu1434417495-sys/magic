using Godot;

internal sealed class BattleLootEntry
{
    private BattleLootEntry(
        BattleLootDropKind dropKind,
        BattleLootSourceKind sourceKind,
        StringName sourceId,
        string sourceLabel,
        StringName dropEntryId,
        StringName itemId,
        int quantity,
        int dropLuck,
        EquipmentInstanceState equipmentInstance
    )
    {
        DropKind = dropKind;
        SourceKind = sourceKind;
        SourceId = ProgressionDataUtils.to_string_name(sourceId);
        SourceLabel = sourceLabel?.StripEdges() ?? "";
        DropEntryId = ProgressionDataUtils.to_string_name(dropEntryId);
        ItemId = ProgressionDataUtils.to_string_name(itemId);
        Quantity = Mathf.Max(quantity, 0);
        DropLuck = Mathf.Clamp(dropLuck, -6, 5);
        EquipmentInstance = equipmentInstance?.DuplicateState();
    }

    internal BattleLootDropKind DropKind { get; }
    internal BattleLootSourceKind SourceKind { get; }
    internal StringName SourceId { get; }
    internal string SourceLabel { get; }
    internal StringName DropEntryId { get; }
    internal StringName ItemId { get; }
    internal int Quantity { get; }
    internal int DropLuck { get; }
    internal EquipmentInstanceState EquipmentInstance { get; }

    internal bool IsEmpty =>
        DropKind == BattleLootDropKind.Unknown
        || SourceKind == BattleLootSourceKind.Unknown
        || SourceId == ""
        || DropEntryId == ""
        || ItemId == ""
        || Quantity <= 0
        || string.IsNullOrEmpty(SourceLabel)
        || (DropKind == BattleLootDropKind.EquipmentInstance && EquipmentInstance == null);

    internal static BattleLootEntry CreateItem(
        BattleLootSourceKind sourceKind,
        StringName sourceId,
        string sourceLabel,
        StringName dropEntryId,
        StringName itemId,
        int quantity
    ) =>
        Create(
            BattleLootDropKind.Item,
            sourceKind,
            sourceId,
            sourceLabel,
            dropEntryId,
            itemId,
            quantity,
            0,
            null
        );

    internal static BattleLootEntry CreateRandomEquipment(
        BattleLootSourceKind sourceKind,
        StringName sourceId,
        string sourceLabel,
        StringName dropEntryId,
        StringName itemId,
        int quantity,
        int dropLuck
    ) =>
        Create(
            BattleLootDropKind.RandomEquipment,
            sourceKind,
            sourceId,
            sourceLabel,
            dropEntryId,
            itemId,
            quantity,
            dropLuck,
            null
        );

    internal static BattleLootEntry CreateEquipmentInstance(
        BattleLootSourceKind sourceKind,
        StringName sourceId,
        string sourceLabel,
        StringName dropEntryId,
        EquipmentInstanceState equipmentInstance
    )
    {
        StringName itemId = equipmentInstance?.item_id ?? new StringName("");
        return Create(
            BattleLootDropKind.EquipmentInstance,
            sourceKind,
            sourceId,
            sourceLabel,
            dropEntryId,
            itemId,
            1,
            0,
            equipmentInstance
        );
    }

    internal static BattleLootEntry Create(
        BattleLootDropKind dropKind,
        BattleLootSourceKind sourceKind,
        StringName sourceId,
        string sourceLabel,
        StringName dropEntryId,
        StringName itemId,
        int quantity,
        int dropLuck = 0,
        EquipmentInstanceState equipmentInstance = null
    )
    {
        var entry = new BattleLootEntry(
            dropKind,
            sourceKind,
            sourceId,
            sourceLabel,
            dropEntryId,
            itemId,
            quantity,
            dropLuck,
            equipmentInstance
        );
        if (entry.IsEmpty)
            return null;
        if (entry.DropKind == BattleLootDropKind.EquipmentInstance)
        {
            if (entry.Quantity != 1 || entry.EquipmentInstance.item_id != entry.ItemId)
                return null;
        }
        return entry;
    }

    internal BattleLootEntry Duplicate() =>
        Create(
            DropKind,
            SourceKind,
            SourceId,
            SourceLabel,
            DropEntryId,
            ItemId,
            Quantity,
            DropLuck,
            EquipmentInstance
        );

    internal BattleLootEntry WithQuantity(int quantity) =>
        Create(
            DropKind,
            SourceKind,
            SourceId,
            SourceLabel,
            DropEntryId,
            ItemId,
            quantity,
            DropLuck,
            EquipmentInstance
        );
}
