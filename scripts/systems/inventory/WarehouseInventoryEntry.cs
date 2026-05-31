using Godot;
using GDictionary = Godot.Collections.Dictionary;

public sealed class WarehouseInventoryEntry
{
    public StringName ItemId { get; }
    public ItemDef ItemDef { get; }
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
        ItemDef itemDef,
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
        ItemDef = itemDef;
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

    public GDictionary ToDictionary()
    {
        var entry = new GDictionary
        {
            { "item_id", ItemId.ToString() },
            { "display_name", DisplayName },
            { "description", Description },
            { "icon", Icon },
            { "quantity", Quantity },
            { "total_quantity", TotalQuantity },
            { "is_stackable", IsStackable },
            { "stack_limit", StackLimit },
            { "item_category", ItemCategory.ToString() },
            { "is_skill_book", IsSkillBook },
            { "granted_skill_id", GrantedSkillId.ToString() },
            { "storage_mode", StorageMode.ToString() },
        };

        if (HasEquipmentInstance)
        {
            entry["instance_id"] = InstanceId.ToString();
            entry["rarity"] = Rarity;
            entry["current_durability"] = CurrentDurability;
        }
        return entry;
    }
}
