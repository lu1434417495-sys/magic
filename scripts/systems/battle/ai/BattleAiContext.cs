using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleAiContext : RefCounted
{
    private static readonly StringName StatusTaunted = "taunted";
    private static readonly StringName AnonymousAction = "anonymous_action";

    private static readonly HashSet<string> RuntimeFixedMetadataKeys = new()
    {
        "generated",
        "state_id",
        "slot_id",
        "skill_id",
        "variant_id",
        "action_family",
        "source_action_id",
        "identity_key",
        "score_bucket_id",
        "action_id",
    };

    private static readonly HashSet<string> RuntimeMetadataExportKeys = new()
    {
        "generated",
        "state_id",
        "slot_id",
        "skill_id",
        "variant_id",
        "action_family",
        "source_action_id",
        "identity_key",
    };

    public BattleState state { get; set; }
    public BattleUnitState unit_state { get; set; }
    public GodotObject grid_service { get; set; }
    public GDictionary skill_defs { get; set; } = new();
    public Callable preview_callback { get; set; } = new();
    public Callable skill_score_input_callback { get; set; } = new();
    public Callable action_score_input_callback { get; set; } = new();
    public Callable move_cost_callback { get; set; } = new();
    public BattleAiRuntimeActionPlan runtime_action_plan { get; set; }
    public BattleAiQueryService ai_query_service;
    public BattleAiCandidateEvaluationService candidate_evaluator { get; set; }
    public bool allow_authored_action_fallback_for_tests { get; set; }
    public bool trace_enabled { get; set; }
    public GArray action_traces { get; set; } = new();
    public GDictionary score_projection_cache { get; set; } = new();
    public GArray mutation_guard_violations { get; set; } = new();

    private int _action_trace_nonce;
    private readonly List<GDictionary> _action_metadata_stack = new();
    private readonly BattleAiSkillAffordanceClassifier _skill_affordance_classifier = new();
    private readonly GDictionary _skill_affordance_cache = new();

    public BattlePreview preview_command(GodotObject command)
    {
        AiTraceRecorder.enter("preview_command");
        BattlePreview result = _preview_command_impl(command);
        AiTraceRecorder.exit("preview_command");
        return result;
    }

    public BattlePreview _preview_command_impl(GodotObject command)
    {
        if (!IsCallableValid(preview_callback))
        {
            return new BattlePreview();
        }
        GodotObject previewObject = preview_callback.Call(command).AsGodotObject();
        return previewObject as BattlePreview ?? new BattlePreview();
    }

    public BattleAiQueryService get_ai_query_service()
    {
        return ai_query_service;
    }

    public BattleAiDecision evaluate_candidate_request(GodotObject request)
    {
        AiTraceRecorder.enter("candidate:context.evaluate_request");
        BattleAiDecision result = _evaluate_candidate_request_impl(request);
        AiTraceRecorder.exit("candidate:context.evaluate_request");
        return result;
    }

    public BattleAiDecision _evaluate_candidate_request_impl(GodotObject request)
    {
        var candidateRequest = request as BattleAiCandidateRequest;
        if (candidateRequest == null)
        {
            BattleAiPayloadGuard.FailLoud(
                "evaluate_candidate_request requires BattleAiCandidateRequest.",
                new GDictionary { ["source"] = "BattleAiContext" });
            return null;
        }
        if (candidate_evaluator == null)
        {
            BattleAiPayloadGuard.FailLoud(
                "evaluate_candidate_request requires candidate_evaluator.",
                new GDictionary { ["source"] = "BattleAiContext" });
            return null;
        }
        if (ai_query_service == null)
        {
            BattleAiPayloadGuard.FailLoud(
                "evaluate_candidate_request requires ai_query_service.",
                new GDictionary { ["source"] = "BattleAiContext" });
            return null;
        }
        return candidate_evaluator.evaluate(candidateRequest, ai_query_service).AsGodotObject() as BattleAiDecision;
    }

    public int get_move_cost(GodotObject target_unit_state, Vector2I target_coord)
    {
        if (IsCallableValid(move_cost_callback))
        {
            return move_cost_callback.Call(target_unit_state, target_coord).AsInt32();
        }
        if (grid_service != null && state != null && grid_service.HasMethod("get_unit_move_cost"))
        {
            return grid_service.Call("get_unit_move_cost", state, target_unit_state, target_coord).AsInt32();
        }
        return 1;
    }

    public BattleAiScoreInput build_skill_score_input(
        GodotObject skill_def,
        GodotObject command,
        BattlePreview preview,
        GArray effect_defs = null,
        GDictionary metadata = null)
    {
        if (!IsCallableValid(skill_score_input_callback))
        {
            return null;
        }
        GodotObject scoreInput = skill_score_input_callback.Call(
            this,
            skill_def,
            command,
            preview,
            effect_defs ?? new GArray(),
            metadata ?? new GDictionary()).AsGodotObject();
        return scoreInput as BattleAiScoreInput;
    }

    public BattleAiScoreInput build_action_score_input(
        StringName action_kind,
        string action_label,
        StringName score_bucket_id,
        GodotObject command,
        BattlePreview preview,
        GDictionary metadata = null)
    {
        if (!IsCallableValid(action_score_input_callback))
        {
            return null;
        }
        GodotObject scoreInput = action_score_input_callback.Call(
            this,
            action_kind,
            action_label,
            score_bucket_id,
            command,
            preview,
            metadata ?? new GDictionary()).AsGodotObject();
        return scoreInput as BattleAiScoreInput;
    }

    public GArray get_runtime_actions(StringName state_id)
    {
        if (IsEmpty(state_id))
        {
            return new GArray();
        }
        return runtime_action_plan != null ? runtime_action_plan.get_actions(state_id) : new GArray();
    }

    public bool has_runtime_action_state(StringName state_id)
    {
        return !IsEmpty(state_id) && runtime_action_plan != null && runtime_action_plan.has_state(state_id);
    }

    public bool is_runtime_action_plan_stale(GodotObject brain)
    {
        return runtime_action_plan != null && runtime_action_plan.is_stale_for(unit_state, brain, skill_defs);
    }

    public GDictionary get_runtime_action_metadata(GodotObject action)
    {
        return runtime_action_plan != null ? runtime_action_plan.get_action_metadata(action) : new GDictionary();
    }

    public GDictionary get_skill_affordance_record(StringName skill_id)
    {
        StringName normalizedSkillId = ProgressionDataUtils.to_string_name(skill_id);
        if (IsEmpty(normalizedSkillId))
        {
            return new GDictionary();
        }
        if (runtime_action_plan != null)
        {
            GDictionary planRecord = runtime_action_plan.get_skill_affordance_record(normalizedSkillId);
            if (planRecord.Count > 0)
            {
                return planRecord;
            }
        }
        if (_skill_affordance_cache.ContainsKey(normalizedSkillId)
            && _skill_affordance_cache[normalizedSkillId].VariantType == Variant.Type.Dictionary)
        {
            return _skill_affordance_cache[normalizedSkillId].AsGodotDictionary().Duplicate(true);
        }

        SkillDef skillDef = null;
        if (skill_defs != null && skill_defs.ContainsKey(normalizedSkillId))
        {
            skillDef = skill_defs[normalizedSkillId].AsGodotObject() as SkillDef;
        }
        if (skillDef == null)
        {
            return new GDictionary();
        }

        int skillLevel = 1;
        if (unit_state != null && unit_state.known_skill_level_map.ContainsKey(normalizedSkillId))
        {
            skillLevel = unit_state.known_skill_level_map[normalizedSkillId].AsInt32();
        }
        GDictionary record = _skill_affordance_classifier.classify_skill(skillDef, skillLevel);
        _skill_affordance_cache[normalizedSkillId] = record.Duplicate(true);
        return record;
    }

    public bool has_skill_affordance(GArray affordances)
    {
        if (unit_state == null || affordances == null || affordances.Count == 0)
        {
            return false;
        }

        var desiredLookup = new GDictionary();
        foreach (Variant affordanceVariant in affordances)
        {
            StringName affordance = ProgressionDataUtils.to_string_name(affordanceVariant);
            if (!IsEmpty(affordance))
            {
                desiredLookup[affordance] = true;
            }
        }
        if (desiredLookup.Count == 0)
        {
            return false;
        }

        foreach (StringName rawSkillId in unit_state.known_active_skill_ids)
        {
            StringName skillId = ProgressionDataUtils.to_string_name(rawSkillId);
            if (IsEmpty(skillId))
            {
                continue;
            }
            GDictionary record = get_skill_affordance_record(skillId);
            if (!record.ContainsKey("affordances") || record["affordances"].VariantType != Variant.Type.Array)
            {
                continue;
            }
            foreach (Variant skillAffordanceVariant in record["affordances"].AsGodotArray())
            {
                StringName skillAffordance = ProgressionDataUtils.to_string_name(skillAffordanceVariant);
                if (desiredLookup.ContainsKey(skillAffordance))
                {
                    return true;
                }
            }
        }
        return false;
    }

    public void push_action_metadata(GDictionary metadata)
    {
        _action_metadata_stack.Add(_normalize_runtime_action_metadata(metadata ?? new GDictionary()));
    }

    public GDictionary pop_action_metadata()
    {
        if (_action_metadata_stack.Count == 0)
        {
            return new GDictionary();
        }
        int lastIndex = _action_metadata_stack.Count - 1;
        GDictionary result = _action_metadata_stack[lastIndex];
        _action_metadata_stack.RemoveAt(lastIndex);
        return result;
    }

    public GDictionary get_current_action_metadata()
    {
        if (_action_metadata_stack.Count == 0)
        {
            return new GDictionary();
        }
        return _action_metadata_stack[^1].Duplicate(true);
    }

    public GDictionary merge_current_action_metadata(GDictionary metadata = null)
    {
        GDictionary merged = get_current_action_metadata().Duplicate(true);
        metadata ??= new GDictionary();
        foreach (Variant key in metadata.Keys)
        {
            if (_is_runtime_fixed_metadata_key(key) && merged.ContainsKey(key))
            {
                continue;
            }
            merged[key] = metadata[key];
        }

        GDictionary runtimeActionMetadata = new();
        if (merged.ContainsKey("runtime_action_metadata")
            && merged["runtime_action_metadata"].VariantType == Variant.Type.Dictionary)
        {
            runtimeActionMetadata = merged["runtime_action_metadata"].AsGodotDictionary().Duplicate(true);
        }
        foreach (Variant key in merged.Keys)
        {
            if (_is_runtime_metadata_export_key(key))
            {
                runtimeActionMetadata[key] = merged[key];
            }
        }
        merged["runtime_action_metadata"] = runtimeActionMetadata;
        return merged;
    }

    public BattleUnitState resolve_forced_target_unit(StringName target_filter)
    {
        if (state == null || unit_state == null)
        {
            return null;
        }
        if (target_filter != "enemy")
        {
            return null;
        }

        BattleStatusEffectState tauntEntry = unit_state.get_status_effect(StatusTaunted);
        if (tauntEntry == null)
        {
            return null;
        }
        StringName sourceId = ProgressionDataUtils.to_string_name(tauntEntry.source_unit_id);
        if (IsEmpty(sourceId) || !state.units.ContainsKey(sourceId))
        {
            return null;
        }
        BattleUnitState sourceUnit = state.units[sourceId].AsGodotObject() as BattleUnitState;
        if (sourceUnit == null || !sourceUnit.is_alive)
        {
            return null;
        }
        if (sourceUnit.faction_id == unit_state.faction_id)
        {
            return null;
        }
        return sourceUnit;
    }

    public StringName next_action_trace_id(StringName action_id)
    {
        _action_trace_nonce += 1;
        StringName normalizedActionId = !IsEmpty(action_id) ? action_id : AnonymousAction;
        return new StringName($"{normalizedActionId}_{_action_trace_nonce}");
    }

    public void record_action_trace(GDictionary action_trace)
    {
        if (!trace_enabled || action_trace == null || action_trace.Count == 0)
        {
            return;
        }
        action_traces.Add(action_trace.Duplicate(true));
    }

    public void mark_action_trace_chosen(StringName action_trace_id, BattleAiDecision decision = null)
    {
        if (IsEmpty(action_trace_id))
        {
            return;
        }

        for (int traceIndex = 0; traceIndex < action_traces.Count; traceIndex += 1)
        {
            if (action_traces[traceIndex].VariantType != Variant.Type.Dictionary)
            {
                continue;
            }
            GDictionary actionTrace = action_traces[traceIndex].AsGodotDictionary();
            StringName traceId = GdInterop.GetStringName(actionTrace, "trace_id");
            if (traceId != action_trace_id)
            {
                continue;
            }

            actionTrace["chosen"] = true;
            if (decision != null)
            {
                actionTrace["chosen_reason_text"] = decision.reason_text;
                actionTrace["chosen_command"] = _build_command_dict(decision.command);
                BattleAiScoreInput scoreInput = ResolveDecisionScoreInput(decision);
                actionTrace["chosen_score_input"] = scoreInput != null ? scoreInput.to_dict() : new GDictionary();
            }
            action_traces[traceIndex] = actionTrace;
            return;
        }
    }

    public GDictionary build_turn_trace(BattleAiDecision decision = null)
    {
        string resolvedBrainId = unit_state != null ? unit_state.ai_brain_id.ToString() : "";
        string resolvedStateId = unit_state != null ? unit_state.ai_state_id.ToString() : "";
        if (decision != null)
        {
            resolvedBrainId = decision.brain_id.ToString();
            resolvedStateId = decision.state_id.ToString();
        }

        int turnStartedTu = -1;
        if (unit_state != null && unit_state.ai_blackboard != null && unit_state.ai_blackboard.ContainsKey("turn_started_tu"))
        {
            turnStartedTu = unit_state.ai_blackboard["turn_started_tu"].AsInt32();
        }

        var turnTrace = new GDictionary
        {
            ["battle_id"] = state != null ? state.battle_id.ToString() : "",
            ["turn_started_tu"] = turnStartedTu,
            ["unit_id"] = unit_state != null ? unit_state.unit_id.ToString() : "",
            ["unit_name"] = unit_state != null ? unit_state.display_name : "",
            ["faction_id"] = unit_state != null ? unit_state.faction_id.ToString() : "",
            ["brain_id"] = resolvedBrainId,
            ["state_id"] = resolvedStateId,
            ["action_id"] = decision != null ? decision.action_id.ToString() : "",
            ["reason_text"] = decision != null ? decision.reason_text : "",
            ["command"] = decision != null ? _build_command_dict(decision.command) : new GDictionary(),
            ["transition"] = decision != null && decision.transition != null ? decision.transition.Duplicate(true) : new GDictionary(),
            ["score_input"] = new GDictionary(),
            ["action_traces"] = action_traces.Duplicate(true),
        };

        if (decision != null)
        {
            BattleAiScoreInput scoreInput = ResolveDecisionScoreInput(decision);
            if (scoreInput != null)
            {
                turnTrace["score_input"] = scoreInput.to_dict();
            }
        }
        return turnTrace;
    }

    public GDictionary _build_command_dict(BattleCommand command)
    {
        if (command == null)
        {
            return new GDictionary();
        }
        return new GDictionary
        {
            ["command_type"] = command.command_type.ToString(),
            ["unit_id"] = command.unit_id.ToString(),
            ["skill_id"] = command.skill_id.ToString(),
            ["skill_variant_id"] = command.skill_variant_id.ToString(),
            ["target_unit_id"] = command.target_unit_id.ToString(),
            ["target_unit_ids"] = command.target_unit_ids.Duplicate(),
            ["target_coord"] = command.target_coord,
            ["target_coords"] = command.target_coords.Duplicate(),
        };
    }

    public GDictionary _normalize_runtime_action_metadata(GDictionary metadata)
    {
        return metadata?.Duplicate(true) ?? new GDictionary();
    }

    public bool _is_runtime_fixed_metadata_key(Variant key)
    {
        return RuntimeFixedMetadataKeys.Contains(key.ToString());
    }

    public bool _is_runtime_metadata_export_key(Variant key)
    {
        return RuntimeMetadataExportKeys.Contains(key.ToString());
    }

    private static BattleAiScoreInput ResolveDecisionScoreInput(BattleAiDecision decision)
    {
        if (decision == null)
        {
            return null;
        }
        return decision.score_input as BattleAiScoreInput ?? decision.skill_score_input as BattleAiScoreInput;
    }

    private static bool IsCallableValid(Callable callable)
    {
        return !callable.Equals(default(Callable)) && !string.IsNullOrEmpty(callable.Method.ToString());
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }
}
