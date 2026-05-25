using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GStringArray = Godot.Collections.Array<string>;

[GlobalClass]
public partial class BattleAiRuntimeActionPlan : RefCounted
{
    public StringName unit_id = "";
    public StringName brain_id = "";
    public string fingerprint = "";
    public GStringNameArray state_ids = new();
    public GDictionary actions_by_state = new();
    public GDictionary generated_actions_by_state = new();
    public GDictionary metadata_by_instance_id = new();
    public GDictionary skill_affordance_records_by_skill_id = new();
    public GStringArray warnings = new();
    public GStringArray errors = new();

    public void set_source(BattleUnitState unit_state, GodotObject brain, GDictionary skill_defs)
    {
        unit_id = unit_state != null ? unit_state.unit_id : new StringName("");
        brain_id = brain != null ? ProgressionDataUtils.to_string_name(brain.Get("brain_id")) : new StringName("");
        fingerprint = build_fingerprint(unit_state, brain, skill_defs);
    }

    public void add_state_actions(StringName state_id, GArray actions)
    {
        StringName normalizedStateId = ProgressionDataUtils.to_string_name(state_id);
        if (normalizedStateId == "")
        {
            return;
        }
        EnsureState(normalizedStateId);
        var copiedActions = new GArray();
        if (actions != null)
        {
            foreach (Variant actionVariant in actions)
            {
                GodotObject action = actionVariant.AsGodotObject();
                if (action == null)
                {
                    continue;
                }
                copiedActions.Add(action);
                if (get_action_metadata(action).Count == 0)
                {
                    set_action_metadata(action, new GDictionary
                    {
                        ["generated"] = false,
                        ["state_id"] = normalizedStateId,
                        ["action_id"] = ProgressionDataUtils.to_string_name(action.Get("action_id")),
                        ["score_bucket_id"] = ProgressionDataUtils.to_string_name(action.Get("score_bucket_id")),
                    });
                }
            }
        }
        actions_by_state[normalizedStateId] = copiedActions;
    }

    public void add_action(StringName state_id, GodotObject action, GDictionary metadata = null)
    {
        if (action == null)
        {
            return;
        }
        StringName normalizedStateId = ProgressionDataUtils.to_string_name(state_id);
        if (normalizedStateId == "")
        {
            return;
        }
        EnsureState(normalizedStateId);
        GetStateActions(normalizedStateId).Add(action);
        GDictionary resolvedMetadata = metadata?.Duplicate(true) ?? new GDictionary();
        resolvedMetadata["state_id"] = normalizedStateId;
        if (!resolvedMetadata.ContainsKey("action_id"))
        {
            resolvedMetadata["action_id"] = ProgressionDataUtils.to_string_name(action.Get("action_id"));
        }
        if (!resolvedMetadata.ContainsKey("score_bucket_id"))
        {
            resolvedMetadata["score_bucket_id"] = ProgressionDataUtils.to_string_name(action.Get("score_bucket_id"));
        }
        set_action_metadata(action, resolvedMetadata);
        if (GdInterop.GetBool(resolvedMetadata, "generated"))
        {
            if (!generated_actions_by_state.ContainsKey(normalizedStateId))
            {
                generated_actions_by_state[normalizedStateId] = new GArray();
            }
            generated_actions_by_state[normalizedStateId].AsGodotArray().Add(action);
        }
    }

    public GArray get_actions(StringName state_id)
    {
        StringName normalizedStateId = ProgressionDataUtils.to_string_name(state_id);
        if (!actions_by_state.ContainsKey(normalizedStateId)
            || actions_by_state[normalizedStateId].VariantType != Variant.Type.Array)
        {
            return new GArray();
        }
        return actions_by_state[normalizedStateId].AsGodotArray().Duplicate();
    }

    public bool has_state(StringName state_id)
    {
        return actions_by_state.ContainsKey(ProgressionDataUtils.to_string_name(state_id));
    }

    public bool is_empty_state(StringName state_id)
    {
        return has_state(state_id) && get_actions(state_id).Count == 0;
    }

    public void set_action_metadata(GodotObject action, GDictionary metadata)
    {
        if (action == null)
        {
            return;
        }
        metadata_by_instance_id[InstanceKey(action)] = metadata?.Duplicate(true) ?? new GDictionary();
    }

    public GDictionary get_action_metadata(GodotObject action)
    {
        if (action == null)
        {
            return new GDictionary();
        }
        long instanceId = InstanceKey(action);
        if (!metadata_by_instance_id.ContainsKey(instanceId)
            || metadata_by_instance_id[instanceId].VariantType != Variant.Type.Dictionary)
        {
            return new GDictionary();
        }
        return metadata_by_instance_id[instanceId].AsGodotDictionary().Duplicate(true);
    }

    public void set_skill_affordance_record(StringName skill_id, GDictionary record)
    {
        StringName normalizedSkillId = ProgressionDataUtils.to_string_name(skill_id);
        if (normalizedSkillId == "")
        {
            return;
        }
        GDictionary copiedRecord = record?.Duplicate(true) ?? new GDictionary();
        copiedRecord["skill_id"] = normalizedSkillId;
        skill_affordance_records_by_skill_id[normalizedSkillId] = copiedRecord;
    }

    public GDictionary get_skill_affordance_record(StringName skill_id)
    {
        StringName normalizedSkillId = ProgressionDataUtils.to_string_name(skill_id);
        if (normalizedSkillId == "")
        {
            return new GDictionary();
        }
        if (!skill_affordance_records_by_skill_id.ContainsKey(normalizedSkillId)
            || skill_affordance_records_by_skill_id[normalizedSkillId].VariantType != Variant.Type.Dictionary)
        {
            return new GDictionary();
        }
        return skill_affordance_records_by_skill_id[normalizedSkillId].AsGodotDictionary().Duplicate(true);
    }

    public GStringArray validate()
    {
        var validationErrors = new GStringArray();
        if (unit_id == "")
        {
            validationErrors.Add("Runtime action plan is missing unit_id.");
        }
        if (brain_id == "")
        {
            validationErrors.Add("Runtime action plan is missing brain_id.");
        }
        foreach (StringName stateId in state_ids)
        {
            if (!actions_by_state.ContainsKey(stateId) || actions_by_state[stateId].VariantType != Variant.Type.Array)
            {
                validationErrors.Add($"Runtime action plan state {stateId} actions payload is invalid.");
                continue;
            }
            foreach (Variant actionVariant in actions_by_state[stateId].AsGodotArray())
            {
                GodotObject action = actionVariant.AsGodotObject();
                if (action == null)
                {
                    validationErrors.Add($"Runtime action plan state {stateId} contains null action.");
                    continue;
                }
                if (get_action_metadata(action).Count == 0)
                {
                    validationErrors.Add($"Runtime action plan action {ProgressionDataUtils.to_string_name(action.Get("action_id"))} is missing metadata.");
                }
            }
        }
        errors = validationErrors.Duplicate();
        return validationErrors;
    }

    public bool is_stale_for(BattleUnitState unit_state, GodotObject brain, GDictionary skill_defs)
    {
        return fingerprint != build_fingerprint(unit_state, brain, skill_defs);
    }

    public static string build_fingerprint(BattleUnitState unit_state, GodotObject brain, GDictionary skill_defs)
    {
        var parts = new List<string>
        {
            $"unit={(unit_state != null ? unit_state.unit_id.ToString() : "")}",
            $"brain={(brain != null ? ProgressionDataUtils.to_string_name(brain.Get("brain_id")).ToString() : "")}",
            $"skills={BuildSkillSignature(unit_state)}",
            $"brain_shape={BuildBrainShapeSignature(brain)}",
        };
        return string.Join("|", parts);
    }

    private void EnsureState(StringName stateId)
    {
        if (!state_ids.Contains(stateId))
        {
            state_ids.Add(stateId);
        }
        if (!actions_by_state.ContainsKey(stateId))
        {
            actions_by_state[stateId] = new GArray();
        }
    }

    private GArray GetStateActions(StringName stateId)
    {
        if (!actions_by_state.ContainsKey(stateId) || actions_by_state[stateId].VariantType != Variant.Type.Array)
        {
            actions_by_state[stateId] = new GArray();
        }
        return actions_by_state[stateId].AsGodotArray();
    }

    private static string BuildSkillSignature(BattleUnitState unitState)
    {
        if (unitState == null)
        {
            return "";
        }
        var entries = new List<string>();
        foreach (Variant rawSkillId in unitState.known_active_skill_ids)
        {
            StringName skillId = ProgressionDataUtils.to_string_name(rawSkillId);
            if (skillId == "")
            {
                continue;
            }
            int level = unitState.known_skill_level_map.ContainsKey(skillId)
                ? unitState.known_skill_level_map[skillId].AsInt32()
                : 1;
            entries.Add($"{skillId}:{level}");
        }
        entries.Sort(System.StringComparer.Ordinal);
        return string.Join(",", entries);
    }

    private static string BuildBrainShapeSignature(GodotObject brain)
    {
        if (brain is not EnemyAiBrainDef typedBrain)
        {
            return "";
        }
        var stateEntries = new List<string>();
        foreach (EnemyAiStateDef stateDef in typedBrain.get_resolved_states())
        {
            if (stateDef == null)
            {
                continue;
            }
            var actionEntries = new List<string>();
            foreach (Variant actionVariant in stateDef.actions)
            {
                GodotObject action = actionVariant.AsGodotObject();
                if (action == null)
                {
                    continue;
                }
                var declaredSkillIds = new List<string>();
                if (action.HasMethod("get_declared_skill_ids"))
                {
                    foreach (Variant skillId in action.Call("get_declared_skill_ids").AsGodotArray())
                    {
                        declaredSkillIds.Add(skillId.ToString());
                    }
                    declaredSkillIds.Sort(System.StringComparer.Ordinal);
                }
                string scriptPath = "";
                GodotObject script = action.GetScript().AsGodotObject();
                if (script is Resource scriptResource)
                {
                    scriptPath = scriptResource.ResourcePath;
                }
                actionEntries.Add(string.Format(
                    "{0}:{1}:{2}:{3}",
                    ProgressionDataUtils.to_string_name(action.Get("action_id")),
                    scriptPath,
                    ProgressionDataUtils.to_string_name(action.Get("score_bucket_id")),
                    string.Join(",", declaredSkillIds)));
            }

            var slotEntries = new List<string>();
            foreach (Variant slotVariant in stateDef.generation_slots)
            {
                GodotObject slot = slotVariant.AsGodotObject();
                if (slot != null && slot.HasMethod("to_signature"))
                {
                    slotEntries.Add(slot.Call("to_signature").ToString());
                }
            }
            stateEntries.Add($"{stateDef.state_id}{{actions=[{string.Join(";", actionEntries)}];slots=[{string.Join(";", slotEntries)}]}}");
        }

        var transitionEntries = new List<string>();
        foreach (Variant ruleVariant in typedBrain.transition_rules)
        {
            GodotObject rule = ruleVariant.AsGodotObject();
            if (rule != null && rule.HasMethod("to_signature"))
            {
                transitionEntries.Add(rule.Call("to_signature").ToString());
            }
        }
        transitionEntries.Sort(System.StringComparer.Ordinal);
        return $"states={string.Join("||", stateEntries)}|transitions={string.Join("||", transitionEntries)}";
    }

    private static long InstanceKey(GodotObject action)
    {
        return unchecked((long)action.GetInstanceId());
    }
}
