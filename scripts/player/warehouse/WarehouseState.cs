using Godot;

[GlobalClass]
public partial class WarehouseState : RefCounted
{
    public Godot.Collections.Array<WarehouseStackState> stacks = new();
    public Godot.Collections.Array<EquipmentInstanceState> equipment_instances = new();

    public Godot.Collections.Array<WarehouseStackState> get_non_empty_stacks()
    {
        var result = new Godot.Collections.Array<WarehouseStackState>();
        foreach (var stack in stacks)
        {
            if (stack == null || stack.is_empty())
                continue;
            result.Add(stack);
        }
        return result;
    }

    public Godot.Collections.Array<EquipmentInstanceState> get_non_empty_instances()
    {
        var result = new Godot.Collections.Array<EquipmentInstanceState>();
        foreach (var instance in equipment_instances)
        {
            if (instance == null || instance.instance_id == "" || instance.item_id == "")
                continue;
            result.Add(instance);
        }
        return result;
    }

    public WarehouseState duplicate_state()
    {
        var copy = new WarehouseState();
        foreach (var stack in get_non_empty_stacks())
            copy.stacks.Add(stack.duplicate_state());
        foreach (var instance in get_non_empty_instances())
            copy.equipment_instances.Add(EquipmentInstanceState.from_dict(instance.to_dict()));
        return copy;
    }

    public Godot.Collections.Dictionary to_dict()
    {
        var stackPayloads = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var stack in get_non_empty_stacks())
            stackPayloads.Add(stack.to_dict());

        var instancePayloads = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var instance in get_non_empty_instances())
            instancePayloads.Add(instance.to_dict());

        return new Godot.Collections.Dictionary
        {
            { "stacks", stackPayloads },
            { "equipment_instances", instancePayloads },
        };
    }

    public static WarehouseState from_dict(Godot.Collections.Dictionary payload)
    {
        if (payload == null)
            return null;
        if (payload.Count != 2)
            return null;
        if (!payload.ContainsKey("stacks") || !payload.ContainsKey("equipment_instances"))
            return null;

        var stacksPayload = payload["stacks"];
        if (stacksPayload.VariantType != Variant.Type.Array)
            return null;

        var state = new WarehouseState();
        foreach (var stackPayload in stacksPayload.AsGodotArray())
        {
            if (stackPayload.VariantType != Variant.Type.Dictionary)
                return null;
            var stack = WarehouseStackState.from_dict(stackPayload.AsGodotDictionary());
            if (stack == null || stack.is_empty())
                return null;
            state.stacks.Add(stack);
        }

        var instancesPayload = payload["equipment_instances"];
        if (instancesPayload.VariantType != Variant.Type.Array)
            return null;

        foreach (var instancePayload in instancesPayload.AsGodotArray())
        {
            if (instancePayload.VariantType != Variant.Type.Dictionary)
                return null;
            var instanceDictionary = instancePayload.AsGodotDictionary();
            var validationError = EquipmentInstanceState.get_payload_validation_error(
                instanceDictionary
            );
            if (validationError.Length > 0)
                return null;

            var instance = EquipmentInstanceState.from_dict(instanceDictionary);
            if (instance == null || instance.instance_id == "" || instance.item_id == "")
                return null;
            state.equipment_instances.Add(instance);
        }

        return state;
    }
}
