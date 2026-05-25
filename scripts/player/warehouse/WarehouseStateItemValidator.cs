using Godot;

[GlobalClass]
public partial class WarehouseStateItemValidator : RefCounted
{
    private static readonly GDScript ItemDefScript = GD.Load<GDScript>("res://scripts/player/warehouse/item_def.gd");

    public static Godot.Collections.Array<string> validate(GodotObject warehouseState, Godot.Collections.Dictionary itemDefs, string contextPath = "warehouse_state")
    {
        var errors = new Godot.Collections.Array<string>();
        if (warehouseState == null) { errors.Add($"{contextPath} is missing."); return errors; }
        if (!warehouseState.HasMethod("get_non_empty_stacks") || !warehouseState.HasMethod("get_non_empty_instances"))
        { errors.Add($"{contextPath} must expose WarehouseState accessors."); return errors; }
        _validate_stacks(warehouseState, itemDefs, contextPath, errors);
        _validate_equipment_instances(warehouseState, itemDefs, contextPath, errors);
        return errors;
    }

    private static void _validate_stacks(GodotObject warehouseState, Godot.Collections.Dictionary itemDefs, string contextPath, Godot.Collections.Array<string> errors)
    {
        var stacksVariant = warehouseState.Get("stacks");
        if (stacksVariant.VariantType != Variant.Type.Array) { errors.Add($"{contextPath}.stacks must be an Array."); return; }
        var stacks = stacksVariant.AsGodotArray();
        for (int i = 0; i < stacks.Count; i++)
        {
            var stack = stacks[i].AsGodotObject();
            var stackPath = $"{contextPath}.stacks[{i}]";
            if (stack == null) { errors.Add($"{stackPath} is null."); continue; }
            var itemId = ProgressionDataUtils.to_string_name(stack.Get("item_id"));
            int quantity = stack.Get("quantity").AsInt32();
            if (itemId == "" || quantity <= 0) { errors.Add($"{stackPath} must have non-empty item_id and positive quantity."); continue; }
            var itemDef = _get_item_def(itemDefs, itemId);
            if (itemDef == null) { errors.Add($"{stackPath} has unknown item_id '{itemId}'."); continue; }
            if ((bool)itemDef.Call("is_equipment")) { errors.Add($"{stackPath} stores equipment item '{itemId}' in stacks; equipment must use equipment_instances."); continue; }
            int maxStack = itemDef.Call("get_effective_max_stack").AsInt32();
            if (quantity > maxStack) errors.Add($"{stackPath} quantity {quantity} exceeds max_stack {maxStack} for item_id '{itemId}'.");
        }
    }

    private static void _validate_equipment_instances(GodotObject warehouseState, Godot.Collections.Dictionary itemDefs, string contextPath, Godot.Collections.Array<string> errors)
    {
        var instancesVariant = warehouseState.Get("equipment_instances");
        if (instancesVariant.VariantType != Variant.Type.Array) { errors.Add($"{contextPath}.equipment_instances must be an Array."); return; }
        var instances = instancesVariant.AsGodotArray();
        for (int i = 0; i < instances.Count; i++)
        {
            var instance = instances[i].AsGodotObject();
            var instancePath = $"{contextPath}.equipment_instances[{i}]";
            if (instance == null) { errors.Add($"{instancePath} is null."); continue; }
            var itemId = ProgressionDataUtils.to_string_name(instance.Get("item_id"));
            if (itemId == "") { errors.Add($"{instancePath} must have non-empty item_id."); continue; }
            var itemDef = _get_item_def(itemDefs, itemId);
            if (itemDef == null) { errors.Add($"{instancePath} has unknown item_id '{itemId}'."); continue; }
            if (!(bool)itemDef.Call("is_equipment")) { errors.Add($"{instancePath} stores non-equipment item '{itemId}' in equipment_instances."); }
        }
    }

    private static GodotObject _get_item_def(Godot.Collections.Dictionary itemDefs, StringName itemId)
    {
        if (itemDefs.ContainsKey(itemId)) return itemDefs[itemId].AsGodotObject();
        string itemKey = (string)itemId;
        return itemDefs.ContainsKey(itemKey) ? itemDefs[itemKey].AsGodotObject() : null;
    }
}
