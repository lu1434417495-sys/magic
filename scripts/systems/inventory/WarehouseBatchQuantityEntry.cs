using Godot;

internal readonly struct WarehouseBatchQuantityEntry
{
    public readonly StringName ItemId;
    public readonly int Quantity;

    public WarehouseBatchQuantityEntry(StringName itemId, int quantity)
    {
        ItemId = ProgressionDataUtils.to_string_name(itemId);
        Quantity = quantity;
    }
}
