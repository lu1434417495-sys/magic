using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class WarehouseState : RefCounted
{
    public Godot.Collections.Array<WarehouseStackState> stacks = new();
    public Godot.Collections.Array<EquipmentInstanceState> equipment_instances = new();

    public Godot.Collections.Array<WarehouseStackState> get_non_empty_stacks()
    {
        return new Godot.Collections.Array<WarehouseStackState>(GetNonEmptyStacksTyped());
    }

    public IReadOnlyList<WarehouseStackState> GetStacksTyped()
    {
        return stacks != null
            ? new List<WarehouseStackState>(stacks)
            : new List<WarehouseStackState>();
    }

    public IReadOnlyList<WarehouseStackState> GetNonEmptyStacksTyped()
    {
        var result = new List<WarehouseStackState>();
        foreach (var stack in GetStacksTyped())
        {
            if (stack == null || stack.is_empty())
                continue;
            result.Add(stack);
        }
        return result;
    }

    public WarehouseStackState GetStackAt(int index)
    {
        if (stacks == null || index < 0 || index >= stacks.Count)
            return null;
        return stacks[index];
    }

    public void AddStack(WarehouseStackState stack)
    {
        stacks ??= new Godot.Collections.Array<WarehouseStackState>();
        stacks.Add(stack);
    }

    public bool RemoveStackAt(int index)
    {
        if (stacks == null || index < 0 || index >= stacks.Count)
            return false;
        stacks.RemoveAt(index);
        return true;
    }

    public void ReplaceStacks(IEnumerable<WarehouseStackState> values)
    {
        stacks = new Godot.Collections.Array<WarehouseStackState>();
        if (values == null)
            return;
        foreach (WarehouseStackState stack in values)
            stacks.Add(stack);
    }

    public Godot.Collections.Array<EquipmentInstanceState> get_non_empty_instances()
    {
        return new Godot.Collections.Array<EquipmentInstanceState>(GetNonEmptyEquipmentInstancesTyped());
    }

    public IReadOnlyList<EquipmentInstanceState> GetEquipmentInstancesTyped()
    {
        return equipment_instances != null
            ? new List<EquipmentInstanceState>(equipment_instances)
            : new List<EquipmentInstanceState>();
    }

    public IReadOnlyList<EquipmentInstanceState> GetNonEmptyEquipmentInstancesTyped()
    {
        var result = new List<EquipmentInstanceState>();
        foreach (var instance in GetEquipmentInstancesTyped())
        {
            if (instance == null || instance.instance_id == "" || instance.item_id == "")
                continue;
            result.Add(instance);
        }
        return result;
    }

    public EquipmentInstanceState GetEquipmentInstanceAt(int index)
    {
        if (equipment_instances == null || index < 0 || index >= equipment_instances.Count)
            return null;
        return equipment_instances[index];
    }

    public void AddEquipmentInstance(EquipmentInstanceState instance)
    {
        equipment_instances ??= new Godot.Collections.Array<EquipmentInstanceState>();
        equipment_instances.Add(instance);
    }

    public EquipmentInstanceState RemoveEquipmentInstanceAt(int index)
    {
        EquipmentInstanceState instance = GetEquipmentInstanceAt(index);
        if (instance == null)
            return null;
        equipment_instances.RemoveAt(index);
        return instance;
    }

    public void ReplaceEquipmentInstances(IEnumerable<EquipmentInstanceState> values)
    {
        equipment_instances = new Godot.Collections.Array<EquipmentInstanceState>();
        if (values == null)
            return;
        foreach (EquipmentInstanceState instance in values)
            equipment_instances.Add(instance);
    }

    public WarehouseState duplicate_state()
    {
        var copy = new WarehouseState();
        foreach (var stack in GetNonEmptyStacksTyped())
            copy.AddStack(stack.duplicate_state());
        foreach (var instance in GetNonEmptyEquipmentInstancesTyped())
            copy.AddEquipmentInstance(instance.duplicate_state());
        return copy;
    }

    public Godot.Collections.Dictionary to_dict()
    {
        var stackPayloads = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var stack in GetNonEmptyStacksTyped())
            stackPayloads.Add(stack.to_dict());

        var instancePayloads = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var instance in GetNonEmptyEquipmentInstancesTyped())
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
            state.AddStack(stack);
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
            state.AddEquipmentInstance(instance);
        }

        return state;
    }
}
