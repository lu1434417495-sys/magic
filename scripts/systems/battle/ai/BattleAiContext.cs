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
    public Dictionary<string, object> score_projection_cache { get; set; } = new();
    public GArray mutation_guard_violations { get; set; } = new();

    private int _action_trace_nonce;
    private readonly List<RuntimeActionMetadata> _action_metadata_stack = new();
    private readonly List<ActionTraceEntry> _action_trace_entries = new();
    private readonly BattleAiSkillAffordanceClassifier _skill_affordance_classifier = new();
    private readonly Dictionary<StringName, BattleAiSkillAffordanceRecord> _skill_affordance_records_by_skill_id =
        new();

    private sealed class RuntimeActionMetadata
    {
        private readonly List<RuntimeMetadataEntry> _entries = new();

        public IReadOnlyList<RuntimeMetadataEntry> Entries => _entries;

        public RuntimeActionMetadata Clone()
        {
            var clone = new RuntimeActionMetadata();
            foreach (RuntimeMetadataEntry entry in _entries)
            {
                clone.Set(entry);
            }
            return clone;
        }

        public bool ContainsKey(string key)
        {
            foreach (RuntimeMetadataEntry entry in _entries)
            {
                if (entry.Key == key)
                {
                    return true;
                }
            }
            return false;
        }

        public void Set(RuntimeMetadataEntry entry)
        {
            if (string.IsNullOrEmpty(entry.Key))
            {
                return;
            }
            for (int index = 0; index < _entries.Count; index += 1)
            {
                if (_entries[index].Key == entry.Key)
                {
                    _entries[index] = entry.Clone();
                    return;
                }
            }
            _entries.Add(entry.Clone());
        }

        public bool TryGetValue(string key, out RuntimeMetadataValue value)
        {
            foreach (RuntimeMetadataEntry entry in _entries)
            {
                if (entry.Key == key)
                {
                    value = entry.Value;
                    return true;
                }
            }
            value = RuntimeMetadataValue.Nil();
            return false;
        }

        public GDictionary ToDictionary()
        {
            var result = new GDictionary();
            foreach (RuntimeMetadataEntry entry in _entries)
            {
                result[entry.Key] = entry.Value.ToVariant();
            }
            return result;
        }

        public static RuntimeActionMetadata FromDictionary(GDictionary source)
        {
            var result = new RuntimeActionMetadata();
            foreach (RuntimeMetadataEntry entry in ReadRuntimeMetadataEntries(source))
            {
                result.Set(entry);
            }
            return result;
        }
    }

    private sealed class ActionTraceEntry
    {
        private readonly List<RuntimeMetadataEntry> _entries = new();

        public StringName TraceId { get; private set; } = "";

        public void MarkChosen(BattleAiDecision decision)
        {
            Set("chosen", true);
            if (decision == null)
            {
                return;
            }
            Set("chosen_reason_text", decision.reason_text);
            Set("chosen_command", BuildCommandDictionary(decision.command));
            BattleAiScoreInput scoreInput = ResolveDecisionScoreInput(decision);
            Set(
                "chosen_score_input",
                scoreInput != null ? scoreInput.to_dict() : new GDictionary()
            );
        }

        public GDictionary ToDictionary()
        {
            var result = new GDictionary();
            foreach (RuntimeMetadataEntry entry in _entries)
            {
                result[entry.Key] = entry.Value.ToVariant();
            }
            return result;
        }

        public static ActionTraceEntry FromDictionary(GDictionary source)
        {
            var result = new ActionTraceEntry();
            foreach (RuntimeMetadataEntry entry in ReadRuntimeMetadataEntries(source))
            {
                result.Set(entry);
            }
            result.TraceId = GdInterop.GetStringName(source ?? new GDictionary(), "trace_id");
            return result;
        }

        private void Set(string key, bool value)
        {
            Set(new RuntimeMetadataEntry(key, RuntimeMetadataValue.FromVariant(Variant.From(value))));
        }

        private void Set(string key, string value)
        {
            Set(
                new RuntimeMetadataEntry(
                    key,
                    RuntimeMetadataValue.FromVariant(Variant.From(value ?? ""))
                )
            );
        }

        private void Set(string key, GDictionary value)
        {
            Set(
                new RuntimeMetadataEntry(
                    key,
                    RuntimeMetadataValue.FromDictionary(value ?? new GDictionary())
                )
            );
        }

        private void Set(RuntimeMetadataEntry entry)
        {
            if (string.IsNullOrEmpty(entry.Key))
            {
                return;
            }
            for (int index = 0; index < _entries.Count; index += 1)
            {
                if (_entries[index].Key == entry.Key)
                {
                    _entries[index] = entry.Clone();
                    return;
                }
            }
            _entries.Add(entry.Clone());
        }
    }

    private readonly struct RuntimeMetadataEntry
    {
        public RuntimeMetadataEntry(string key, RuntimeMetadataValue value)
        {
            Key = key ?? "";
            Value = value ?? RuntimeMetadataValue.Nil();
        }

        public string Key { get; }

        public RuntimeMetadataValue Value { get; }

        public RuntimeMetadataEntry Clone() => new(Key, Value.Clone());
    }

    private enum RuntimeMetadataValueKind
    {
        Nil,
        Bool,
        Integer,
        Float,
        Text,
        StringName,
        Vector2I,
        Dictionary,
        Array,
        Object,
        Fallback,
    }

    private sealed class RuntimeMetadataValue
    {
        private readonly RuntimeMetadataValueKind _kind;
        private readonly bool _boolValue;
        private readonly long _integerValue;
        private readonly double _floatValue;
        private readonly string _textValue;
        private readonly StringName _stringNameValue;
        private readonly Vector2I _vector2IValue;
        private readonly GDictionary _dictionaryValue;
        private readonly GArray _arrayValue;
        private readonly GodotObject _objectValue;
        private readonly Variant _fallbackValue;

        private RuntimeMetadataValue(
            RuntimeMetadataValueKind kind,
            bool boolValue = false,
            long integerValue = 0,
            double floatValue = 0,
            string textValue = "",
            StringName stringNameValue = default,
            Vector2I vector2IValue = default,
            GDictionary dictionaryValue = null,
            GArray arrayValue = null,
            GodotObject objectValue = null,
            Variant fallbackValue = default
        )
        {
            _kind = kind;
            _boolValue = boolValue;
            _integerValue = integerValue;
            _floatValue = floatValue;
            _textValue = textValue ?? "";
            _stringNameValue = stringNameValue;
            _vector2IValue = vector2IValue;
            _dictionaryValue = dictionaryValue;
            _arrayValue = arrayValue;
            _objectValue = objectValue;
            _fallbackValue = fallbackValue;
        }

        public RuntimeMetadataValue Clone()
        {
            return _kind switch
            {
                RuntimeMetadataValueKind.Dictionary
                    => FromDictionary(_dictionaryValue?.Duplicate(true) ?? new GDictionary()),
                RuntimeMetadataValueKind.Array
                    => FromArray(_arrayValue?.Duplicate(true) ?? new GArray()),
                _ => this,
            };
        }

        public bool TryGetDictionary(out GDictionary value)
        {
            if (_kind == RuntimeMetadataValueKind.Dictionary)
            {
                value = _dictionaryValue?.Duplicate(true) ?? new GDictionary();
                return true;
            }
            value = new GDictionary();
            return false;
        }

        public Variant ToVariant()
        {
            return _kind switch
            {
                RuntimeMetadataValueKind.Nil => default,
                RuntimeMetadataValueKind.Bool => Variant.From(_boolValue),
                RuntimeMetadataValueKind.Integer => Variant.From(_integerValue),
                RuntimeMetadataValueKind.Float => Variant.From(_floatValue),
                RuntimeMetadataValueKind.Text => Variant.From(_textValue),
                RuntimeMetadataValueKind.StringName => Variant.From(_stringNameValue),
                RuntimeMetadataValueKind.Vector2I => Variant.From(_vector2IValue),
                RuntimeMetadataValueKind.Dictionary
                    => Variant.From(_dictionaryValue?.Duplicate(true) ?? new GDictionary()),
                RuntimeMetadataValueKind.Array
                    => Variant.From(_arrayValue?.Duplicate(true) ?? new GArray()),
                RuntimeMetadataValueKind.Object => Variant.From(_objectValue),
                RuntimeMetadataValueKind.Fallback => _fallbackValue,
                _ => default,
            };
        }

        public static RuntimeMetadataValue Nil() => new(RuntimeMetadataValueKind.Nil);

        public static RuntimeMetadataValue FromDictionary(GDictionary value) =>
            new(
                RuntimeMetadataValueKind.Dictionary,
                dictionaryValue: value?.Duplicate(true) ?? new GDictionary()
            );

        public static RuntimeMetadataValue FromArray(GArray value) =>
            new(
                RuntimeMetadataValueKind.Array,
                arrayValue: value?.Duplicate(true) ?? new GArray()
            );

        public static RuntimeMetadataValue FromVariant(Variant value)
        {
            return value.VariantType switch
            {
                Variant.Type.Nil => Nil(),
                Variant.Type.Bool => new RuntimeMetadataValue(
                    RuntimeMetadataValueKind.Bool,
                    boolValue: value.AsBool()
                ),
                Variant.Type.Int => new RuntimeMetadataValue(
                    RuntimeMetadataValueKind.Integer,
                    integerValue: value.AsInt64()
                ),
                Variant.Type.Float => new RuntimeMetadataValue(
                    RuntimeMetadataValueKind.Float,
                    floatValue: value.AsDouble()
                ),
                Variant.Type.String => new RuntimeMetadataValue(
                    RuntimeMetadataValueKind.Text,
                    textValue: value.AsString()
                ),
                Variant.Type.StringName => new RuntimeMetadataValue(
                    RuntimeMetadataValueKind.StringName,
                    stringNameValue: value.AsStringName()
                ),
                Variant.Type.Vector2I => new RuntimeMetadataValue(
                    RuntimeMetadataValueKind.Vector2I,
                    vector2IValue: value.AsVector2I()
                ),
                Variant.Type.Dictionary => FromDictionary(value.AsGodotDictionary()),
                Variant.Type.Array => FromArray(value.AsGodotArray()),
                Variant.Type.Object => new RuntimeMetadataValue(
                    RuntimeMetadataValueKind.Object,
                    objectValue: value.AsGodotObject()
                ),
                _ => new RuntimeMetadataValue(
                    RuntimeMetadataValueKind.Fallback,
                    fallbackValue: value
                ),
            };
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
            GDictionary planRecord = runtime_action_plan.get_skill_affordance_record(
                normalizedSkillId
            );
            if (planRecord.Count > 0)
            {
                BattleAiSkillAffordanceRecord typedPlanRecord =
                    BattleAiSkillAffordanceRecord.FromDictionary(normalizedSkillId, planRecord);
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

        skillDef = skill_defs[normalizedSkillId].AsGodotObject() as SkillDef;
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
        foreach (RuntimeMetadataEntry entry in incoming.Entries)
        {
            if (_is_runtime_fixed_metadata_key(entry.Key) && merged.ContainsKey(entry.Key))
            {
                continue;
            }
            merged.Set(entry);
        }

        RuntimeActionMetadata runtimeActionMetadata = new();
        if (merged.TryGetValue("runtime_action_metadata", out RuntimeMetadataValue runtimeValue))
        {
            runtimeActionMetadata = runtimeValue.TryGetDictionary(out GDictionary runtimeDictionary)
                ? RuntimeActionMetadata.FromDictionary(runtimeDictionary)
                : new RuntimeActionMetadata();
        }
        foreach (RuntimeMetadataEntry entry in merged.Entries)
        {
            if (_is_runtime_metadata_export_key(entry.Key))
            {
                runtimeActionMetadata.Set(entry);
            }
        }
        merged.Set(
            new RuntimeMetadataEntry(
                "runtime_action_metadata",
                RuntimeMetadataValue.FromDictionary(runtimeActionMetadata.ToDictionary())
            )
        );
        return merged.ToDictionary();
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
        if (
            unit_state != null
            && unit_state.ai_blackboard != null
            && unit_state.ai_blackboard.ContainsKey("turn_started_tu")
        )
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
        return RuntimeActionMetadata.FromDictionary(metadata).ToDictionary();
    }

    private static bool _is_runtime_fixed_metadata_key(string key)
    {
        return RuntimeFixedMetadataKeys.Contains(key ?? "");
    }

    private static bool _is_runtime_metadata_export_key(string key)
    {
        return RuntimeMetadataExportKeys.Contains(key ?? "");
    }

    private static List<RuntimeMetadataEntry> ReadRuntimeMetadataEntries(GDictionary source)
    {
        var result = new List<RuntimeMetadataEntry>();
        if (source == null)
        {
            return result;
        }
        foreach (var rawKey in source.Keys)
        {
            string key = ReadRuntimeMetadataKey(rawKey);
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }
            result.Add(new RuntimeMetadataEntry(key, RuntimeMetadataValue.FromVariant(source[rawKey])));
        }
        return result;
    }

    private static string ReadRuntimeMetadataKey(Variant rawKey)
    {
        return rawKey.VariantType switch
        {
            Variant.Type.String => rawKey.AsString(),
            Variant.Type.StringName => rawKey.AsStringName().ToString(),
            Variant.Type.Nil => "",
            _ => rawKey.ToString(),
        };
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
