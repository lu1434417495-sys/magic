using Godot;
using System.Collections.Generic;

public partial class WarehouseState : RefCounted
{
    public Godot.Collections.Array<WarehouseStackState> stacks = new();
    public Godot.Collections.Array<EquipmentInstanceState> equipment_instances = new();
    private bool _disposed;

    public new void Dispose()
    {
        if (_disposed)
            return;
        System.GC.SuppressFinalize(this);
        Dispose(true);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            DisposeManagedState();
        base.Dispose(disposing);
    }

    private void DisposeManagedState()
    {
        if (_disposed)
            return;
        _disposed = true;
        GodotRefCountedDisposer.DisposeAll(stacks);
        GodotRefCountedDisposer.DisposeAll(equipment_instances);
        stacks.Clear();
        equipment_instances.Clear();
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
            if (stack == null || stack.IsEmpty())
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
        WarehouseStackState removedStack = stacks[index];
        stacks.RemoveAt(index);
        GodotRefCountedDisposer.DisposeIfValid(removedStack);
        return true;
    }

    public void ReplaceStacks(IEnumerable<WarehouseStackState> values)
    {
        var next = new Godot.Collections.Array<WarehouseStackState>();
        var retained = new HashSet<WarehouseStackState>();
        if (values != null)
        {
            foreach (WarehouseStackState stack in values)
            {
                next.Add(stack);
                if (stack != null)
                    retained.Add(stack);
            }
        }
        foreach (WarehouseStackState oldStack in GetStacksTyped())
        {
            if (oldStack != null && !retained.Contains(oldStack))
                GodotRefCountedDisposer.DisposeIfValid(oldStack);
        }
        stacks = next;
        if (values == null)
            return;
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
        var next = new Godot.Collections.Array<EquipmentInstanceState>();
        var retained = new HashSet<EquipmentInstanceState>();
        if (values != null)
        {
            foreach (EquipmentInstanceState instance in values)
            {
                next.Add(instance);
                if (instance != null)
                    retained.Add(instance);
            }
        }
        foreach (EquipmentInstanceState oldInstance in GetEquipmentInstancesTyped())
        {
            if (oldInstance != null && !retained.Contains(oldInstance))
                GodotRefCountedDisposer.DisposeIfValid(oldInstance);
        }
        equipment_instances = next;
        if (values == null)
            return;
    }

    public WarehouseState DuplicateState()
    {
        var copy = new WarehouseState();
        foreach (var stack in GetNonEmptyStacksTyped())
            copy.AddStack(stack.DuplicateState());
        foreach (var instance in GetNonEmptyEquipmentInstancesTyped())
            copy.AddEquipmentInstance(instance.DuplicateState());
        return copy;
    }

    public Godot.Collections.Dictionary ToDictionary()
    {
        var stackPayloads = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var stack in GetNonEmptyStacksTyped())
            stackPayloads.Add(stack.ToDictionary());

        var instancePayloads = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var instance in GetNonEmptyEquipmentInstancesTyped())
            instancePayloads.Add(instance.ToDictionary());

        return new Godot.Collections.Dictionary
        {
            { "stacks", stackPayloads },
            { "equipment_instances", instancePayloads },
        };
    }

    public static WarehouseState FromDictionary(Godot.Collections.Dictionary payload)
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
            var stack = WarehouseStackState.FromDictionary(stackPayload.AsGodotDictionary());
            if (stack == null || stack.IsEmpty())
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
            var validationError = EquipmentInstanceState.GetPayloadValidationError(
                instanceDictionary
            );
            if (validationError.Length > 0)
                return null;

            var instance = EquipmentInstanceState.FromDictionary(instanceDictionary);
            if (instance == null || instance.instance_id == "" || instance.item_id == "")
                return null;
            state.AddEquipmentInstance(instance);
        }

        return state;
    }
}
