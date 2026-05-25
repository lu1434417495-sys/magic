using Godot;

[GlobalClass]
public partial class WarehouseState : RefCounted
{
    private static readonly GDScript EquipmentInstanceStateScript = GD.Load<GDScript>("res://scripts/player/warehouse/equipment_instance_state.gd");

    public Godot.Collections.Array stacks = new();
    public Godot.Collections.Array equipment_instances = new();

    public Godot.Collections.Array get_non_empty_stacks()
    {
        var result = new Godot.Collections.Array();
        foreach (var s in stacks)
        {
            var so = s.AsGodotObject();
            if (so == null || (bool)so.Call("is_empty")) continue;
            result.Add(s);
        }
        return result;
    }

    public Godot.Collections.Array get_non_empty_instances()
    {
        var result = new Godot.Collections.Array();
        foreach (var i in equipment_instances)
        {
            var io = i.AsGodotObject();
            if (io == null || (string)io.Get("instance_id").AsStringName() == "" || (string)io.Get("item_id").AsStringName() == "") continue;
            result.Add(i);
        }
        return result;
    }

    public WarehouseState duplicate_state()
    {
        var copy = new WarehouseState();
        foreach (var s in get_non_empty_stacks())
            copy.stacks.Add(s.AsGodotObject().Call("duplicate_state"));
        foreach (var i in get_non_empty_instances())
            copy.equipment_instances.Add(EquipmentInstanceStateScript.Call("from_dict", i.AsGodotObject().Call("to_dict")));
        return copy;
    }

    public Godot.Collections.Dictionary to_dict()
    {
        var sd = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var s in get_non_empty_stacks())
            sd.Add(s.AsGodotObject().Call("to_dict").AsGodotDictionary());
        var id = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var i in get_non_empty_instances())
            id.Add(i.AsGodotObject().Call("to_dict").AsGodotDictionary());
        return new Godot.Collections.Dictionary { {"stacks", sd}, {"equipment_instances", id} };
    }

    public static WarehouseState from_dict(Variant data)
    {
        if (data.VariantType != Variant.Type.Dictionary) return null;
        var payload = data.AsGodotDictionary();
        if (payload.Count != 2) return null;
        if (!payload.ContainsKey("stacks") || !payload.ContainsKey("equipment_instances")) return null;
        var state = new WarehouseState();
        var sd = payload["stacks"];
        if (sd.VariantType != Variant.Type.Array) return null;
        foreach (var sv in sd.AsGodotArray())
        {
            var stack = WarehouseStackState.from_dict(sv);
            if (stack == null || stack.is_empty()) return null;
            state.stacks.Add(stack);
        }
        var idv = payload["equipment_instances"];
        if (idv.VariantType != Variant.Type.Array) return null;
        foreach (var iv in idv.AsGodotArray())
        {
            var ve = EquipmentInstanceStateScript.Call("get_payload_validation_error", iv).AsString();
            if (ve.Length > 0) return null;
            var inst = EquipmentInstanceStateScript.Call("from_dict", iv).AsGodotObject();
            if (inst == null || (string)inst.Get("instance_id").AsStringName() == "" || (string)inst.Get("item_id").AsStringName() == "") return null;
            state.equipment_instances.Add(inst);
        }
        return state;
    }
}
