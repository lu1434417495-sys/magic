using Godot;

[GlobalClass]
public partial class WarehouseStateItemValidator : RefCounted
{
    public static Godot.Collections.Array<string> validate(
        WarehouseState warehouseState,
        Godot.Collections.Dictionary itemDefs,
        string contextPath = "warehouse_state"
    )
    {
        var errors = new Godot.Collections.Array<string>();

        if (warehouseState == null)
        {
            errors.Add($"{contextPath} is missing.");
            return errors;
        }

        _validate_stacks(warehouseState, itemDefs, contextPath, errors);

        _validate_equipment_instances(warehouseState, itemDefs, contextPath, errors);

        return errors;
    }

    private static void _validate_stacks(
        WarehouseState warehouseState,
        Godot.Collections.Dictionary itemDefs,
        string contextPath,
        Godot.Collections.Array<string> errors
    )
    {
        for (int i = 0; i < warehouseState.stacks.Count; i++)
        {
            WarehouseStackState stack = warehouseState.stacks[i];

            var stackPath = $"{contextPath}.stacks[{i}]";

            if (stack == null)
            {
                errors.Add($"{stackPath} is null.");
                continue;
            }

            var itemId = ProgressionDataUtils.to_string_name(stack.item_id);

            int quantity = stack.quantity;

            if (itemId == "" || quantity <= 0)
            {
                errors.Add($"{stackPath} must have non-empty item_id and positive quantity.");
                continue;
            }

            ItemDef itemDef = _get_item_def(itemDefs, itemId);

            if (itemDef == null)
            {
                errors.Add($"{stackPath} has unknown item_id '{itemId}'.");
                continue;
            }

            if (itemDef.is_equipment())
            {
                errors.Add(
                    $"{stackPath} stores equipment item '{itemId}' in stacks; equipment must use equipment_instances."
                );
                continue;
            }

            int maxStack = itemDef.get_effective_max_stack();

            if (quantity > maxStack)
                errors.Add(
                    $"{stackPath} quantity {quantity} exceeds max_stack {maxStack} for item_id '{itemId}'."
                );
        }
    }

    private static void _validate_equipment_instances(
        WarehouseState warehouseState,
        Godot.Collections.Dictionary itemDefs,
        string contextPath,
        Godot.Collections.Array<string> errors
    )
    {
        for (int i = 0; i < warehouseState.equipment_instances.Count; i++)
        {
            EquipmentInstanceState instance = warehouseState.equipment_instances[i];

            var instancePath = $"{contextPath}.equipment_instances[{i}]";

            if (instance == null)
            {
                errors.Add($"{instancePath} is null.");
                continue;
            }

            var itemId = ProgressionDataUtils.to_string_name(instance.item_id);

            if (itemId == "")
            {
                errors.Add($"{instancePath} must have non-empty item_id.");
                continue;
            }

            ItemDef itemDef = _get_item_def(itemDefs, itemId);

            if (itemDef == null)
            {
                errors.Add($"{instancePath} has unknown item_id '{itemId}'.");
                continue;
            }

            if (!itemDef.is_equipment())
            {
                errors.Add(
                    $"{instancePath} stores non-equipment item '{itemId}' in equipment_instances."
                );
            }
        }
    }

    private static ItemDef _get_item_def(
        Godot.Collections.Dictionary itemDefs,
        StringName itemId
    )
    {
        if (itemDefs.ContainsKey(itemId))
            return itemDefs[itemId].AsGodotObject() as ItemDef;

        string itemKey = (string)itemId;

        return itemDefs.ContainsKey(itemKey) ? itemDefs[itemKey].AsGodotObject() as ItemDef : null;
    }
}
