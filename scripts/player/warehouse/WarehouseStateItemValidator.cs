using System.Collections.Generic;
using Godot;

public static class WarehouseStateItemValidator
{
    public static List<string> Validate(
        WarehouseState warehouseState,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs,
        string contextPath = "warehouse_state"
    )
    {
        var errors = new List<string>();

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
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs,
        string contextPath,
        List<string> errors
    )
    {
        IReadOnlyList<WarehouseStackState> stacks = warehouseState.GetStacksTyped();
        for (int i = 0; i < stacks.Count; i++)
        {
            WarehouseStackState stack = stacks[i];

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

            ItemDefinition itemDef = _get_item_def(itemDefs, itemId);

            if (itemDef == null)
            {
                errors.Add($"{stackPath} has unknown item_id '{itemId}'.");
                continue;
            }

            if (itemDef.IsEquipment())
            {
                errors.Add(
                    $"{stackPath} stores equipment item '{itemId}' in stacks; equipment must use equipment_instances."
                );
                continue;
            }

            int maxStack = itemDef.GetEffectiveMaxStack();

            if (quantity > maxStack)
                errors.Add(
                    $"{stackPath} quantity {quantity} exceeds max_stack {maxStack} for item_id '{itemId}'."
                );
        }
    }

    private static void _validate_equipment_instances(
        WarehouseState warehouseState,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs,
        string contextPath,
        List<string> errors
    )
    {
        IReadOnlyList<EquipmentInstanceState> equipmentInstances =
            warehouseState.GetEquipmentInstancesTyped();
        for (int i = 0; i < equipmentInstances.Count; i++)
        {
            EquipmentInstanceState instance = equipmentInstances[i];

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

            ItemDefinition itemDef = _get_item_def(itemDefs, itemId);

            if (itemDef == null)
            {
                errors.Add($"{instancePath} has unknown item_id '{itemId}'.");
                continue;
            }

            if (!itemDef.IsEquipment())
            {
                errors.Add(
                    $"{instancePath} stores non-equipment item '{itemId}' in equipment_instances."
                );
            }
        }
    }

    private static ItemDefinition _get_item_def(
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs,
        StringName itemId
    )
    {
        return itemDefs != null && itemDefs.TryGetValue(itemId, out ItemDefinition itemDef)
            ? itemDef
            : null;
    }
}
