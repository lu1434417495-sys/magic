using System;
using System.Collections.Generic;
using Godot;
using GActionArray = Godot.Collections.Array<EnemyAiAction>;
using GGenerationSlotArray = Godot.Collections.Array<EnemyAiGenerationSlotDef>;
using GStringArray = Godot.Collections.Array<string>;

[GlobalClass]
public partial class EnemyAiStateDef : Resource
{
    [Export]
    public StringName state_id = "";

    [Export]
    public GActionArray actions = new();

    [Export]
    public GGenerationSlotArray generation_slots = new();

    internal GActionArray GetActions()
    {
        var result = new GActionArray();
        foreach (EnemyAiAction action in GetTypedActions())
        {
            result.Add(action);
        }
        return result;
    }

    internal List<EnemyAiAction> GetTypedActions()
    {
        var result = new List<EnemyAiAction>();
        try
        {
            foreach (EnemyAiAction action in actions ?? new GActionArray())
            {
                if (action != null)
                {
                    result.Add(action);
                }
            }
        }
        catch (InvalidCastException)
        {
            return result;
        }
        return result;
    }

    public GStringArray ValidateSchema(
        StringName brainId = default,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions = null
    )
    {
        var errors = new GStringArray();

        var ctxLabel = brainId != "" ? $"Enemy brain {brainId} state" : "Enemy state";

        if (state_id == "")
        {
            errors.Add($"{ctxLabel} is missing state_id.");
            return errors;
        }

        if (actions.Count == 0)
        {
            errors.Add($"{ctxLabel} {state_id} must declare at least one action.");
            return errors;
        }

        skillDefinitions ??= new Dictionary<StringName, SkillDefinition>();

        var seenActionIds = new HashSet<StringName>();

        List<EnemyAiAction> typedActions = GetTypedActions();
        if (typedActions.Count < actions.Count)
        {
            errors.Add($"{ctxLabel} {state_id} contains a non-EnemyAiAction action resource.");
        }

        foreach (EnemyAiAction actionObj in typedActions)
        {
            if (actionObj == null)
            {
                errors.Add($"{ctxLabel} {state_id} contains a null action resource.");
                continue;
            }

            if (actionObj.GetType() == typeof(EnemyAiAction))
            {
                errors.Add(
                    $"{ctxLabel} {state_id} contains base EnemyAiAction without a concrete action type."
                );
                continue;
            }

            var actionId = ProgressionDataUtils.to_string_name(actionObj.action_id);

            if (actionId != "" && !seenActionIds.Add(actionId))
                errors.Add($"{ctxLabel} {state_id} declares duplicate action_id {actionId}.");

            foreach (var ae in actionObj.ValidateSchema())
                errors.Add($"{ctxLabel} {state_id}: {ae}");

            foreach (var ase in actionObj.ValidateSkillReferences(skillDefinitions))
                errors.Add($"{ctxLabel} {state_id}: {ase}");
        }

        foreach (var e in _validate_generation_slots(ctxLabel))
            errors.Add(e);

        return errors;
    }

    internal List<EnemyAiGenerationSlotDef> GetTypedGenerationSlots()
    {
        var result = new List<EnemyAiGenerationSlotDef>();
        try
        {
            foreach (EnemyAiGenerationSlotDef slot in generation_slots ?? new GGenerationSlotArray())
            {
                if (slot != null)
                {
                    result.Add(slot);
                }
            }
        }
        catch (InvalidCastException)
        {
            return result;
        }
        return result;
    }

    private GStringArray _validate_generation_slots(string ctxLabel)
    {
        var errors = new GStringArray();

        var seenSlotIds = new HashSet<StringName>();

        var seenOrders = new HashSet<int>();

        List<EnemyAiGenerationSlotDef> typedSlots = GetTypedGenerationSlots();
        if (typedSlots.Count < generation_slots.Count)
        {
            errors.Add($"{ctxLabel} {state_id} contains a non-EnemyAiGenerationSlotDef resource.");
        }

        foreach (EnemyAiGenerationSlotDef slotObj in typedSlots)
        {
            if (slotObj == null)
            {
                errors.Add($"{ctxLabel} {state_id} contains a null generation slot resource.");
                continue;
            }

            var slotId = ProgressionDataUtils.to_string_name(slotObj.slot_id);

            if (slotId != "" && !seenSlotIds.Add(slotId))
                errors.Add(
                    $"{ctxLabel} {state_id} declares duplicate generation slot_id {slotId}."
                );

            int order = slotObj.order;

            if (!seenOrders.Add(order))
                errors.Add(
                    $"{ctxLabel} {state_id} declares duplicate generation slot order {order}."
                );

            foreach (
                var se in slotObj.ValidateSchema($"{ctxLabel} {state_id}", GetActions())
            )
                errors.Add($"{ctxLabel} {state_id}: {se}");
        }

        return errors;
    }
}
