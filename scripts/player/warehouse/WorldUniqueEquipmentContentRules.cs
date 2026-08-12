using Godot;

public static class WorldUniqueEquipmentContentRules
{
    public static readonly StringName WorldUniqueEquipmentTag = "world_unique_equipment";

    public static bool IsWorldUniqueEquipment(ItemDefinition itemDefinition)
    {
        if (itemDefinition == null || !itemDefinition.IsEquipment())
            return false;

        foreach (StringName tag in itemDefinition.Tags)
        {
            if (ProgressionDataUtils.to_string_name(tag) == WorldUniqueEquipmentTag)
                return true;
        }
        return false;
    }
}
