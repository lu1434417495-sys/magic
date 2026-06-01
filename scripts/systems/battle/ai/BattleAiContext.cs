using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleAiContext : RefCounted, IBattleAiScoreContext
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

    public BattleState state { get; set; }
    public BattleUnitState unit_state { get; set; }
    public BattleGridService grid_service { get; set; }
    public GDictionary skill_defs { get; set; } = new();
    public Func<
        BattleAiContext,
        SkillDef,
        BattleCommand,
        BattlePreview,
        GArray,
        GDictionary,
        BattleAiScoreInput
    > skill_score_input_callback { get; set; }
    public Func<
        BattleAiContext,
        StringName,
        string,
        StringName,
        BattleCommand,
        BattlePreview,
        GDictionary,
        BattleAiScoreInput
    > action_score_input_callback { get; set; }
    public Func<BattleUnitState, Vector2I, int> move_cost_callback { get; set; }
    public BattleAiRuntimeActionPlan runtime_action_plan { get; set; }
    public BattleAiQueryService ai_query_service;
    public BattleAiCandidateEvaluationService candidate_evaluator { get; set; }
    public bool allow_authored_action_fallback_for_tests { get; set; }
    public bool trace_enabled { get; set; }
    public GArray action_traces { get; set; } = new();
    public GArray mutation_guard_violations { get; set; } = new();

    private int _action_trace_nonce;
    private readonly List<RuntimeActionMetadata> _action_metadata_stack = new();
    private readonly List<ActionTraceEntry> _action_trace_entries = new();
    private readonly BattleAiSkillAffordanceClassifier _skill_affordance_classifier = new();
    private readonly Dictionary<StringName, BattleAiSkillAffordanceRecord> _skill_affordance_records_by_skill_id =
        new();

    private sealed class RuntimeActionMetadata
    {
        public bool generated;
        public StringName state_id = "";
        public StringName slot_id = "";
        public StringName slot_role = "";
        public StringName skill_id = "";
        public StringName variant_id = "";
        public StringName action_family = "";
        public StringName source_action_id = "";
        public StringName score_bucket_id = "";
        public StringName action_id = "";
        public string identity_key = "";

        public RuntimeActionMetadata Clone()
        {
            return new RuntimeActionMetadata
            {
                generated = generated,
                state_id = state_id,
                slot_id = slot_id,
                slot_role = slot_role,
                skill_id = skill_id,
                variant_id = variant_id,
                action_family = action_family,
                source_action_id = source_action_id,
                score_bucket_id = score_bucket_id,
                action_id = action_id,
                identity_key = identity_key ?? "",
            };
        }

        public bool ContainsKey(string key)
        {
            return key switch
            {
                "generated" => generated,
                "state_id" => !BattleAiContext.IsEmpty(state_id),
                "slot_id" => !BattleAiContext.IsEmpty(slot_id),
                "slot_role" => !BattleAiContext.IsEmpty(slot_role),
                "skill_id" => !BattleAiContext.IsEmpty(skill_id),
                "variant_id" => !BattleAiContext.IsEmpty(variant_id),
                "action_family" => !BattleAiContext.IsEmpty(action_family),
                "source_action_id" => !BattleAiContext.IsEmpty(source_action_id),
                "score_bucket_id" => !BattleAiContext.IsEmpty(score_bucket_id),
                "action_id" => !BattleAiContext.IsEmpty(action_id),
                "identity_key" => !string.IsNullOrEmpty(identity_key),
                _ => false,
            };
        }

        public void MergeFrom(RuntimeActionMetadata incoming)
        {
            if (incoming == null)
                return;
            if (incoming.generated && ShouldMerge("generated"))
                generated = true;
            MergeStringName("state_id", incoming.state_id);
            MergeStringName("slot_id", incoming.slot_id);
            MergeStringName("slot_role", incoming.slot_role);
            MergeStringName("skill_id", incoming.skill_id);
            MergeStringName("variant_id", incoming.variant_id);
            MergeStringName("action_family", incoming.action_family);
            MergeStringName("source_action_id", incoming.source_action_id);
            MergeStringName("score_bucket_id", incoming.score_bucket_id);
            MergeStringName("action_id", incoming.action_id);
            if (!string.IsNullOrEmpty(incoming.identity_key) && ShouldMerge("identity_key"))
                identity_key = incoming.identity_key;
        }

        public RuntimeActionMetadata ExportMetadata()
        {
            return new RuntimeActionMetadata
            {
                generated = generated,
                state_id = state_id,
                slot_id = slot_id,
                skill_id = skill_id,
                variant_id = variant_id,
                action_family = action_family,
                source_action_id = source_action_id,
                identity_key = identity_key ?? "",
            };
        }

        public bool IsMetadataEmpty()
        {
            return !generated
                && BattleAiContext.IsEmpty(state_id)
                && BattleAiContext.IsEmpty(slot_id)
                && BattleAiContext.IsEmpty(slot_role)
                && BattleAiContext.IsEmpty(skill_id)
                && BattleAiContext.IsEmpty(variant_id)
                && BattleAiContext.IsEmpty(action_family)
                && BattleAiContext.IsEmpty(source_action_id)
                && BattleAiContext.IsEmpty(score_bucket_id)
                && BattleAiContext.IsEmpty(action_id)
                && string.IsNullOrEmpty(identity_key);
        }

        public GDictionary ToDictionary(bool includeRuntimeExport = false)
        {
            var result = new GDictionary();
            if (generated)
                result["generated"] = true;
            AddStringName(result, "state_id", state_id);
            AddStringName(result, "slot_id", slot_id);
            AddStringName(result, "slot_role", slot_role);
            AddStringName(result, "skill_id", skill_id);
            AddStringName(result, "variant_id", variant_id);
            AddStringName(result, "action_family", action_family);
            AddStringName(result, "source_action_id", source_action_id);
            AddStringName(result, "score_bucket_id", score_bucket_id);
            AddStringName(result, "action_id", action_id);
            if (!string.IsNullOrEmpty(identity_key))
                result["identity_key"] = identity_key;
            if (includeRuntimeExport)
            {
                RuntimeActionMetadata exportMetadata = ExportMetadata();
                if (!exportMetadata.IsMetadataEmpty())
                    result["runtime_action_metadata"] = exportMetadata.ToDictionary();
            }
            return result;
        }

        public static RuntimeActionMetadata FromDictionary(GDictionary source)
        {
            var result = new RuntimeActionMetadata
            {
                generated = ReadBoolValue(source, "generated"),
                state_id = ReadStringNameValue(source, "state_id"),
                slot_id = ReadStringNameValue(source, "slot_id"),
                slot_role = ReadStringNameValue(source, "slot_role"),
                skill_id = ReadStringNameValue(source, "skill_id"),
                variant_id = ReadStringNameValue(source, "variant_id"),
                action_family = ReadStringNameValue(source, "action_family"),
                source_action_id = ReadStringNameValue(source, "source_action_id"),
                score_bucket_id = ReadStringNameValue(source, "score_bucket_id"),
                action_id = ReadStringNameValue(source, "action_id"),
                identity_key = ReadTextValue(source, "identity_key"),
            };
            if (TryReadDictionaryValue(source, "runtime_action_metadata", out GDictionary runtimeMetadata))
                result.MergeFrom(FromDictionary(runtimeMetadata));
            return result;
        }

        private bool ShouldMerge(string key) =>
            !_is_runtime_fixed_metadata_key(key) || !ContainsKey(key);

        private void MergeStringName(string key, StringName value)
        {
            if (BattleAiContext.IsEmpty(value) || !ShouldMerge(key))
                return;
            switch (key)
            {
                case "state_id":
                    state_id = value;
                    break;
                case "slot_id":
                    slot_id = value;
                    break;
                case "slot_role":
                    slot_role = value;
                    break;
                case "skill_id":
                    skill_id = value;
                    break;
                case "variant_id":
                    variant_id = value;
                    break;
                case "action_family":
                    action_family = value;
                    break;
                case "source_action_id":
                    source_action_id = value;
                    break;
                case "score_bucket_id":
                    score_bucket_id = value;
                    break;
                case "action_id":
                    action_id = value;
                    break;
            }
        }

        private static void AddStringName(GDictionary result, string key, StringName value)
        {
            if (!BattleAiContext.IsEmpty(value))
                result[key] = value;
        }
    }

    private sealed class ActionTraceEntry
    {
        public StringName TraceId { get; private set; } = "";
        public StringName ActionId { get; private set; } = "";
        public StringName ScoreBucketId { get; private set; } = "";
        public RuntimeActionMetadata Metadata { get; private set; } = new();
        public bool Chosen { get; private set; }
        public string ChosenReasonText { get; private set; } = "";
        public GDictionary ChosenCommand { get; private set; } = new();
        public GDictionary ChosenScoreInput { get; private set; } = new();

        public void MarkChosen(BattleAiDecision decision)
        {
            Chosen = true;
            if (decision == null)
                return;
            ChosenReasonText = decision.reason_text ?? "";
            ChosenCommand = BuildCommandDictionary(decision.command);
            BattleAiScoreInput scoreInput = ResolveDecisionScoreInput(decision);
            ChosenScoreInput = scoreInput != null ? scoreInput.to_dict() : new GDictionary();
        }

        public GDictionary ToDictionary()
        {
            var result = new GDictionary
            {
                ["trace_id"] = TraceId,
                ["action_id"] = ActionId.ToString(),
                ["score_bucket_id"] = ScoreBucketId.ToString(),
                ["metadata"] = Metadata?.ToDictionary() ?? new GDictionary(),
                ["chosen"] = Chosen,
            };
            if (!string.IsNullOrEmpty(ChosenReasonText))
                result["chosen_reason_text"] = ChosenReasonText;
            if (ChosenCommand.Count > 0)
                result["chosen_command"] = ChosenCommand.Duplicate(true);
            if (ChosenScoreInput.Count > 0)
                result["chosen_score_input"] = ChosenScoreInput.Duplicate(true);
            return result;
        }

        public static ActionTraceEntry FromDictionary(GDictionary source)
        {
            var result = new ActionTraceEntry
            {
                TraceId = ReadStringNameValue(source, "trace_id"),
                ActionId = ReadStringNameValue(source, "action_id"),
                ScoreBucketId = ReadStringNameValue(source, "score_bucket_id"),
                Metadata = TryReadDictionaryValue(source, "metadata", out GDictionary metadata)
                    ? RuntimeActionMetadata.FromDictionary(metadata)
                    : new RuntimeActionMetadata(),
                Chosen = ReadBoolValue(source, "chosen"),
                ChosenReasonText = ReadTextValue(source, "chosen_reason_text"),
                ChosenCommand = TryReadDictionaryValue(source, "chosen_command", out GDictionary chosenCommand)
                    ? chosenCommand.Duplicate(true)
                    : new GDictionary(),
                ChosenScoreInput = TryReadDictionaryValue(source, "chosen_score_input", out GDictionary chosenScoreInput)
                    ? chosenScoreInput.Duplicate(true)
                    : new GDictionary(),
            };
            return result;
        }
    }

    public BattleAiQueryService get_ai_query_service()
    {
        return ai_query_service;
    }

    public BattleAiDecision evaluate_candidate_request(BattleAiCandidateRequest request)
    {
        AiTraceRecorder.enter("candidate:context.evaluate_request");
        BattleAiDecision result = _evaluate_candidate_request_impl(request);
        AiTraceRecorder.exit("candidate:context.evaluate_request");
        return result;
    }

    public BattleAiDecision _evaluate_candidate_request_impl(BattleAiCandidateRequest request)
    {
        if (request == null)
        {
            BattleAiPayloadGuard.FailLoud(
                "evaluate_candidate_request requires BattleAiCandidateRequest.",
                new GDictionary { ["source"] = "BattleAiContext" }
            );
            return null;
        }
        if (candidate_evaluator == null)
        {
            BattleAiPayloadGuard.FailLoud(
                "evaluate_candidate_request requires candidate_evaluator.",
                new GDictionary { ["source"] = "BattleAiContext" }
            );
            return null;
        }
        if (ai_query_service == null)
        {
            BattleAiPayloadGuard.FailLoud(
                "evaluate_candidate_request requires ai_query_service.",
                new GDictionary { ["source"] = "BattleAiContext" }
            );
            return null;
        }
        return candidate_evaluator.evaluate(request, ai_query_service);
    }

    public int get_move_cost(BattleUnitState target_unit_state, Vector2I target_coord)
    {
        if (move_cost_callback != null)
        {
            return move_cost_callback.Invoke(target_unit_state, target_coord);
        }
        if (grid_service != null && state != null)
        {
            return grid_service.get_unit_move_cost(
                state,
                target_unit_state,
                target_coord
            );
        }
        return 1;
    }

    public BattleAiScoreInput build_skill_score_input(
        SkillDef skill_def,
        BattleCommand command,
        BattlePreview preview,
        GArray effect_defs = null,
        GDictionary metadata = null
    )
    {
        if (skill_score_input_callback == null || skill_def == null || command == null)
        {
            return null;
        }
        return skill_score_input_callback.Invoke(
            this,
            skill_def,
            command,
            preview,
            effect_defs ?? new GArray(),
            metadata ?? new GDictionary()
        );
    }

    public BattleAiScoreInput build_action_score_input(
        StringName action_kind,
        string action_label,
        StringName score_bucket_id,
        BattleCommand command,
        BattlePreview preview,
        GDictionary metadata = null
    )
    {
        if (action_score_input_callback == null || command == null)
        {
            return null;
        }
        return action_score_input_callback.Invoke(
            this,
            action_kind,
            action_label,
            score_bucket_id,
            command,
            preview,
            metadata ?? new GDictionary()
        );
    }

    public GArray get_runtime_actions(StringName state_id)
    {
        if (IsEmpty(state_id))
        {
            return new GArray();
        }
        return runtime_action_plan != null
            ? runtime_action_plan.get_actions(state_id)
            : new GArray();
    }

    internal IReadOnlyList<EnemyAiAction> GetRuntimeActionsTyped(StringName state_id)
    {
        if (IsEmpty(state_id) || runtime_action_plan == null)
        {
            return System.Array.Empty<EnemyAiAction>();
        }
        return runtime_action_plan.GetTypedActions(state_id);
    }

    public bool has_runtime_action_state(StringName state_id)
    {
        return !IsEmpty(state_id)
            && runtime_action_plan != null
            && runtime_action_plan.has_state(state_id);
    }

    public bool is_runtime_action_plan_stale(EnemyAiBrainDef brain)
    {
        return runtime_action_plan != null
            && runtime_action_plan.is_stale_for(unit_state, brain, skill_defs);
    }

    public GDictionary get_runtime_action_metadata(EnemyAiAction action)
    {
        return runtime_action_plan != null
            ? runtime_action_plan.get_action_metadata(action)
            : new GDictionary();
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
            if (
                runtime_action_plan.TryGetSkillAffordanceRecordTyped(
                    normalizedSkillId,
                    out BattleAiSkillAffordanceRecord typedPlanRecord
                )
            )
            {
                _skill_affordance_records_by_skill_id[normalizedSkillId] = typedPlanRecord;
                return typedPlanRecord.ToDictionary();
            }
        }
        if (
            _skill_affordance_records_by_skill_id.TryGetValue(
                normalizedSkillId,
                out BattleAiSkillAffordanceRecord cachedRecord
            )
        )
        {
            return cachedRecord.ToDictionary();
        }

        if (!TryGetSkillDef(normalizedSkillId, out SkillDef skillDef))
        {
            return new GDictionary();
        }

        int skillLevel = 1;
        if (unit_state != null && unit_state.known_skill_level_map.ContainsKey(normalizedSkillId))
        {
            skillLevel = unit_state.known_skill_level_map[normalizedSkillId].AsInt32();
        }
        BattleAiSkillAffordanceRecord record = _skill_affordance_classifier.ClassifySkill(
            skillDef,
            skillLevel
        );
        if (record.skill_id == "")
        {
            record.skill_id = normalizedSkillId;
        }
        _skill_affordance_records_by_skill_id[normalizedSkillId] = record;
        return record.ToDictionary();
    }

    public bool has_skill_affordance(GArray affordances)
    {
        return HasSkillAffordanceValues(DecodeStringNameList(affordances));
    }

    internal bool HasSkillAffordanceValues(IEnumerable<StringName> affordances)
    {
        if (unit_state == null || affordances == null)
        {
            return false;
        }

        HashSet<StringName> desiredLookup = DecodeStringNameSet(affordances);
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
            foreach (StringName skillAffordance in GetSkillAffordanceValues(skillId))
            {
                if (!IsEmpty(skillAffordance) && desiredLookup.Contains(skillAffordance))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private IReadOnlyList<StringName> GetSkillAffordanceValues(StringName skillId)
    {
        StringName normalizedSkillId = ProgressionDataUtils.to_string_name(skillId);
        if (IsEmpty(normalizedSkillId))
        {
            return System.Array.Empty<StringName>();
        }
        if (
            runtime_action_plan != null
            && runtime_action_plan.TryGetSkillAffordances(
                normalizedSkillId,
                out IReadOnlyList<StringName> planAffordances
            )
        )
        {
            return planAffordances;
        }
        if (
            _skill_affordance_records_by_skill_id.TryGetValue(
                normalizedSkillId,
                out BattleAiSkillAffordanceRecord cachedRecord
            )
        )
        {
            return cachedRecord.affordances;
        }
        get_skill_affordance_record(normalizedSkillId);
        return _skill_affordance_records_by_skill_id.TryGetValue(
            normalizedSkillId,
            out BattleAiSkillAffordanceRecord resolvedRecord
        )
            ? resolvedRecord.affordances
            : System.Array.Empty<StringName>();
    }

    private bool TryGetSkillDef(StringName skillId, out SkillDef skillDef)
    {
        skillDef = null;
        StringName normalizedSkillId = ProgressionDataUtils.to_string_name(skillId);
        if (
            skill_defs == null
            || IsEmpty(normalizedSkillId)
            || !skill_defs.ContainsKey(normalizedSkillId)
        )
        {
            return false;
        }

        skillDef = skill_defs[normalizedSkillId].As<SkillDef>();
        return skillDef != null;
    }

    private static HashSet<StringName> DecodeStringNameSet(IEnumerable<StringName> values)
    {
        var result = new HashSet<StringName>();
        if (values == null)
        {
            return result;
        }
        foreach (StringName value in values)
        {
            StringName normalizedValue = ProgressionDataUtils.to_string_name(value);
            if (!IsEmpty(normalizedValue))
            {
                result.Add(normalizedValue);
            }
        }
        return result;
    }

    private static List<StringName> DecodeStringNameList(GArray values)
    {
        var result = new List<StringName>();
        if (values == null)
        {
            return result;
        }
        foreach (var value in values)
        {
            StringName normalizedValue = ProgressionDataUtils.to_string_name(value);
            if (!IsEmpty(normalizedValue))
            {
                result.Add(normalizedValue);
            }
        }
        return result;
    }

    public void push_action_metadata(GDictionary metadata)
    {
        _action_metadata_stack.Add(RuntimeActionMetadata.FromDictionary(metadata));
    }

    public GDictionary pop_action_metadata()
    {
        if (_action_metadata_stack.Count == 0)
        {
            return new GDictionary();
        }
        int lastIndex = _action_metadata_stack.Count - 1;
        GDictionary result = _action_metadata_stack[lastIndex].ToDictionary();
        _action_metadata_stack.RemoveAt(lastIndex);
        return result;
    }

    public GDictionary get_current_action_metadata()
    {
        if (_action_metadata_stack.Count == 0)
        {
            return new GDictionary();
        }
        return _action_metadata_stack[^1].ToDictionary();
    }

    public GDictionary merge_current_action_metadata(GDictionary metadata = null)
    {
        RuntimeActionMetadata merged =
            _action_metadata_stack.Count > 0 ? _action_metadata_stack[^1].Clone() : new RuntimeActionMetadata();
        RuntimeActionMetadata incoming = RuntimeActionMetadata.FromDictionary(metadata);
        merged.MergeFrom(incoming);
        return merged.ToDictionary(includeRuntimeExport: true);
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
        if (IsEmpty(sourceId))
        {
            return null;
        }
        state.TryGetUnitTyped(sourceId, out BattleUnitState sourceUnit);
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
        _action_trace_entries.Add(ActionTraceEntry.FromDictionary(action_trace));
        SyncActionTracesMirror();
    }

    public void mark_action_trace_chosen(
        StringName action_trace_id,
        BattleAiDecision decision = null
    )
    {
        if (IsEmpty(action_trace_id))
        {
            return;
        }

        foreach (ActionTraceEntry actionTrace in _action_trace_entries)
        {
            if (actionTrace.TraceId != action_trace_id)
            {
                continue;
            }

            actionTrace.MarkChosen(decision);
            SyncActionTracesMirror();
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
        if (unit_state != null && unit_state.ai_blackboard != null)
        {
            turnStartedTu = unit_state.ai_blackboard.get_int("turn_started_tu", 0);
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
            ["command"] =
                decision != null ? BuildCommandDictionary(decision.command) : new GDictionary(),
            ["transition"] =
                decision != null && decision.transition != null
                    ? decision.transition.Duplicate(true)
                    : new GDictionary(),
            ["score_input"] = new GDictionary(),
            ["action_traces"] = BuildActionTraceArray(),
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
        return BuildCommandDictionary(command);
    }

    private static GDictionary BuildCommandDictionary(BattleCommand command)
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

    private void SyncActionTracesMirror()
    {
        action_traces = BuildActionTraceArray();
    }

    private GArray BuildActionTraceArray()
    {
        var result = new GArray();
        foreach (ActionTraceEntry entry in _action_trace_entries)
        {
            result.Add(entry.ToDictionary());
        }
        return result;
    }

    public GDictionary _normalize_runtime_action_metadata(GDictionary metadata)
    {
        return RuntimeActionMetadata.FromDictionary(metadata).ToDictionary(includeRuntimeExport: true);
    }

    private static bool _is_runtime_fixed_metadata_key(string key)
    {
        return RuntimeFixedMetadataKeys.Contains(key ?? "");
    }

    private static StringName ReadStringName(GDictionary source, string key)
    {
        return ReadStringNameValue(source, key);
    }

    private static bool TryReadDictionaryValue(GDictionary source, string key, out GDictionary dictionary)
    {
        dictionary = null;
        try
        {
            if (source.ContainsKey(key))
            {
                dictionary = source[key].AsGodotDictionary();
                return dictionary != null;
            }
            StringName stringNameKey = key;
            if (source.ContainsKey(stringNameKey))
            {
                dictionary = source[stringNameKey].AsGodotDictionary();
                return dictionary != null;
            }
        }
        catch
        {
        }
        return false;
    }

    private static bool TryReadBoolValue(GDictionary source, string key, out bool boolValue)
    {
        boolValue = false;
        try
        {
            if (source.ContainsKey(key))
            {
                boolValue = source[key].AsBool();
                return true;
            }
            StringName stringNameKey = key;
            if (source.ContainsKey(stringNameKey))
            {
                boolValue = source[stringNameKey].AsBool();
                return true;
            }
        }
        catch
        {
        }
        return false;
    }

    private static bool ReadBoolValue(GDictionary source, string key)
    {
        return TryReadBoolValue(source, key, out bool value) && value;
    }

    private static StringName ReadStringNameValue(GDictionary source, string key)
    {
        try
        {
            if (source.ContainsKey(key))
                return ProgressionDataUtils.to_string_name(source[key]);
            StringName stringNameKey = key;
            if (source.ContainsKey(stringNameKey))
                return ProgressionDataUtils.to_string_name(source[stringNameKey]);
        }
        catch
        {
        }
        return "";
    }

    private static string ReadTextValue(GDictionary source, string key)
    {
        try
        {
            if (source.ContainsKey(key))
                return source[key].ToString();
            StringName stringNameKey = key;
            if (source.ContainsKey(stringNameKey))
                return source[stringNameKey].ToString();
        }
        catch
        {
        }
        return "";
    }

    private static BattleAiScoreInput ResolveDecisionScoreInput(BattleAiDecision decision)
    {
        if (decision == null)
        {
            return null;
        }
        return decision.score_input ?? decision.skill_score_input;
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }
}
