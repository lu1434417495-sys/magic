using Godot;

[GlobalClass]
public partial class EnemyAiStateDef : Resource
{
    private static readonly GDScript EnemyAiActionScript = GD.Load<GDScript>("res://scripts/enemies/enemy_ai_action.gd");
    private static readonly Script EnemyAiGenerationSlotDefScript = GD.Load<Script>("res://scripts/enemies/EnemyAiGenerationSlotDef.cs");

    [Export] public StringName state_id = "";
    [Export] public Godot.Collections.Array actions = new();
    [Export] public Godot.Collections.Array generation_slots = new();

    public Godot.Collections.Array get_actions() { var r = new Godot.Collections.Array(); foreach (var a in actions) { if (a.VariantType != Variant.Type.Nil) r.Add(a); } return r; }

    public Godot.Collections.Array<string> validate_schema(StringName brainId = default, Godot.Collections.Dictionary skillDefs = null)
    {
        var errors = new Godot.Collections.Array<string>();
        var ctxLabel = brainId != "" ? $"Enemy brain {brainId} state" : "Enemy state";
        if (state_id == "") { errors.Add($"{ctxLabel} is missing state_id."); return errors; }
        if (actions.Count == 0) { errors.Add($"{ctxLabel} {state_id} must declare at least one action."); return errors; }
        skillDefs ??= new Godot.Collections.Dictionary();

        var seenActionIds = new Godot.Collections.Dictionary();
        foreach (var av in actions)
        {
            if (av.VariantType == Variant.Type.Nil) { errors.Add($"{ctxLabel} {state_id} contains a null action resource."); continue; }
            var actionObj = av.AsGodotObject();
            if (actionObj.GetScript().AsGodotObject() == null || !actionObj.HasMethod("decide") || !actionObj.HasMethod("validate_schema"))
            { errors.Add($"{ctxLabel} {state_id} contains an invalid action resource."); continue; }
            if (actionObj.GetScript().AsGodotObject() == EnemyAiActionScript)
            { errors.Add($"{ctxLabel} {state_id} contains base EnemyAiAction without a concrete action type."); continue; }
            var actionId = ProgressionDataUtils.to_string_name(actionObj.Get("action_id"));
            if (actionId != "" && seenActionIds.ContainsKey(actionId))
                errors.Add($"{ctxLabel} {state_id} declares duplicate action_id {actionId}.");
            else if (actionId != "") seenActionIds[actionId] = true;
            foreach (var ae in actionObj.Call("validate_schema").AsGodotArray<string>())
                errors.Add($"{ctxLabel} {state_id}: {ae}");
            if (actionObj.HasMethod("validate_skill_references"))
                foreach (var ase in actionObj.Call("validate_skill_references", skillDefs).AsGodotArray<string>())
                    errors.Add($"{ctxLabel} {state_id}: {ase}");
        }
        foreach (var e in _validate_generation_slots(ctxLabel)) errors.Add(e);
        return errors;
    }

    public Godot.Collections.Array get_generation_slots() { var r = new Godot.Collections.Array(); foreach (var s in generation_slots) { if (s.VariantType != Variant.Type.Nil) r.Add(s); } return r; }

    private Godot.Collections.Array<string> _validate_generation_slots(string ctxLabel)
    {
        var errors = new Godot.Collections.Array<string>();
        var seenSlotIds = new Godot.Collections.Dictionary();
        var seenOrders = new Godot.Collections.Dictionary();
        foreach (var sv in generation_slots)
        {
            if (sv.VariantType == Variant.Type.Nil) { errors.Add($"{ctxLabel} {state_id} contains a null generation slot resource."); continue; }
            var slotObj = sv.AsGodotObject();
            if (slotObj.GetScript().AsGodotObject() == null || !slotObj.HasMethod("matches_affordance"))
            { errors.Add($"{ctxLabel} {state_id} contains an invalid generation slot resource."); continue; }
            if (slotObj.GetScript().AsGodotObject() != EnemyAiGenerationSlotDefScript)
            { errors.Add($"{ctxLabel} {state_id} contains unsupported generation slot type."); continue; }
            var slotId = ProgressionDataUtils.to_string_name(slotObj.Get("slot_id"));
            if (slotId != "" && seenSlotIds.ContainsKey(slotId))
                errors.Add($"{ctxLabel} {state_id} declares duplicate generation slot_id {slotId}.");
            else if (slotId != "") seenSlotIds[slotId] = true;
            int order = slotObj.Get("order").AsInt32();
            if (seenOrders.ContainsKey(order)) errors.Add($"{ctxLabel} {state_id} declares duplicate generation slot order {order}.");
            else seenOrders[order] = true;
            foreach (var se in slotObj.Call("validate_schema", $"{ctxLabel} {state_id}", get_actions()).AsGodotArray<string>())
                errors.Add($"{ctxLabel} {state_id}: {se}");
        }
        return errors;
    }
}
