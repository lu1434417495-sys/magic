using Godot;

public sealed class WarehouseInventoryEntry
{
    public StringName ItemId { get; }
    public ItemDefinition ItemDefinition { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public string Icon { get; }
    public int Quantity { get; }
    public int TotalQuantity { get; }
    public bool IsStackable { get; }
    public int StackLimit { get; }
    public StringName ItemCategory { get; }
    public bool IsSkillBook { get; }
    public StringName GrantedSkillId { get; }
    public StringName StorageMode { get; }
    public StringName InstanceId { get; }
    public int Rarity { get; }
    public int CurrentDurability { get; }
    public bool HasEquipmentInstance { get; }

    public WarehouseInventoryEntry(
        StringName itemId,
        ItemDefinition itemDefinition,
        string displayName,
        string description,
        string icon,
        int quantity,
        int totalQuantity,
        bool isStackable,
        int stackLimit,
        StringName itemCategory,
        bool isSkillBook,
        StringName grantedSkillId,
        StringName storageMode,
        StringName instanceId = default,
        int rarity = 0,
        int currentDurability = 0,
        bool hasEquipmentInstance = false)
    {
        ItemId = ProgressionDataUtils.to_string_name(itemId);
        ItemDefinition = itemDefinition;
        DisplayName = displayName ?? "";
        Description = description ?? "";
        Icon = icon ?? "";
        Quantity = Mathf.Max(quantity, 0);
        TotalQuantity = Mathf.Max(totalQuantity, 0);
        IsStackable = isStackable;
        StackLimit = Mathf.Max(stackLimit, 1);
        ItemCategory = ProgressionDataUtils.to_string_name(itemCategory);
        IsSkillBook = isSkillBook;
        GrantedSkillId = ProgressionDataUtils.to_string_name(grantedSkillId);
        StorageMode = ProgressionDataUtils.to_string_name(storageMode);
        InstanceId = ProgressionDataUtils.to_string_name(instanceId);
        Rarity = rarity;
        CurrentDurability = currentDurability;
        HasEquipmentInstance = hasEquipmentInstance;
    }
}
