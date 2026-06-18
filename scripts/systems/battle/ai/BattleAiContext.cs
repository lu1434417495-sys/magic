using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public class BattleAiContext : IBattleAiScoreContext
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

    private IReadOnlyDictionary<StringName, SkillDef> _skillDefsSource;
    private Dictionary<StringName, SkillDef> _skillDefsById = new();

    public BattleState state { get; set; }
    public BattleUnitState unit_state { get; set; }
    public BattleGridService grid_service { get; set; }
    public BattleAiScoreProfile active_score_profile { get; set; }
    internal ISkillCatalog skill_catalog { get; private set; }
    IReadOnlyDictionary<StringName, SkillDef> IBattleAiScoreContext.skill_defs => _skillDefsById;
    ISkillCatalog IBattleAiScoreContext.skill_catalog => skill_catalog;
    public Func<
        BattleAiContext,
        SkillDef,
        BattleCommand,
        BattlePreview,
        IReadOnlyList<CombatEffectDef>,
        IReadOnlyDictionary<string, object>,
        BattleAiScoreInput
    > skill_score_input_callback { get; set; }
    public Func<
        BattleAiContext,
        StringName,
        string,
        StringName,
        BattleCommand,
        BattlePreview,
        IReadOnlyDictionary<string, object>,
        BattleAiScoreInput
    > action_score_input_callback { get; set; }
    public Func<BattleCommand, BattlePreview> preview_command_callback { get; set; }
    public Func<BattleUnitState, Vector2I, int> move_cost_callback { get; set; }
    public Func<
        BattleUnitState,
        SkillDef,
        BattleSkillCastBlockReasonKind
    > skill_cast_block_reason_callback { get; set; }
    internal BattleAiRuntimeActionPlan runtime_action_plan { get; set; }
    internal BattleAiQueryService ai_query_service;
    internal BattleAiCandidateEvaluationService candidate_evaluator { get; set; }
    public bool allow_authored_action_fallback_for_tests { get; set; }
    public bool trace_enabled { get; set; }

    private int _action_trace_nonce;
    private readonly List<RuntimeActionMetadata> _action_metadata_stack = new();
    private readonly List<AiActionTrace> _action_trace_entries = new();
    private readonly List<string> _mutation_guard_violation_entries = new();
    private readonly BattleAiSkillAffordanceClassifier _skill_affordance_classifier = new();
    private readonly Dictionary<StringName, BattleAiSkillAffordanceRecord> _skill_affordance_records_by_skill_id =
        new();
    private readonly Dictionary<TargetSortCacheKey, List<BattleUnitState>> _sorted_target_units_cache =
        new();

    private readonly record struct TargetSortCacheKey(StringName TargetFilter, StringName Selector);

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

        public Dictionary<string, object> ToTraceDictionary(bool includeRuntimeExport = false)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            if (generated)
                result["generated"] = true;
            AddTraceStringName(result, "state_id", state_id);
            AddTraceStringName(result, "slot_id", slot_id);
            AddTraceStringName(result, "slot_role", slot_role);
            AddTraceStringName(result, "skill_id", skill_id);
            AddTraceStringName(result, "variant_id", variant_id);
            AddTraceStringName(result, "action_family", action_family);
            AddTraceStringName(result, "source_action_id", source_action_id);
            AddTraceStringName(result, "score_bucket_id", score_bucket_id);
            AddTraceStringName(result, "action_id", action_id);
            if (!string.IsNullOrEmpty(identity_key))
                result["identity_key"] = identity_key;
            if (includeRuntimeExport)
            {
                RuntimeActionMetadata exportMetadata = ExportMetadata();
                if (!exportMetadata.IsMetadataEmpty())
                    result["runtime_action_metadata"] = exportMetadata.ToTraceDictionary();
            }
            return result;
        }

        public static RuntimeActionMetadata FromTraceDictionary(
            IReadOnlyDictionary<string, object> source
        )
        {
            if (source == null)
                return new RuntimeActionMetadata();

            var result = new RuntimeActionMetadata
            {
                generated = ReadTraceBoolValue(source, "generated"),
                state_id = ReadTraceStringNameValue(source, "state_id"),
                slot_id = ReadTraceStringNameValue(source, "slot_id"),
                slot_role = ReadTraceStringNameValue(source, "slot_role"),
                skill_id = ReadTraceStringNameValue(source, "skill_id"),
                variant_id = ReadTraceStringNameValue(source, "variant_id"),
                action_family = ReadTraceStringNameValue(source, "action_family"),
                source_action_id = ReadTraceStringNameValue(source, "source_action_id"),
                score_bucket_id = ReadTraceStringNameValue(source, "score_bucket_id"),
                action_id = ReadTraceStringNameValue(source, "action_id"),
                identity_key = ReadTraceTextValue(source, "identity_key"),
            };
            if (
                TryReadTraceDictionaryValue(
                    source,
                    "runtime_action_metadata",
                    out IReadOnlyDictionary<string, object> runtimeMetadata
                )
            )
            {
                result.MergeFrom(FromTraceDictionary(runtimeMetadata));
            }
            return result;
        }

        public static RuntimeActionMetadata FromPlanMetadata(
            BattleAiRuntimeActionPlan.RuntimeActionMetadata source
        )
        {
            if (source == null)
            {
                return new RuntimeActionMetadata();
            }
            var result = new RuntimeActionMetadata
            {
                generated = source.generated,
                state_id = source.state_id,
                slot_id = source.slot_id,
                slot_role = source.slot_role,
                skill_id = source.skill_id,
                variant_id = source.variant_id,
                action_family = source.action_family,
                source_action_id = source.source_action_id,
                score_bucket_id = source.score_bucket_id,
                action_id = source.action_id,
                identity_key = source.identity_key ?? "",
            };
            if (source.runtime_action_metadata != null)
            {
                result.MergeFrom(
                    new RuntimeActionMetadata
                    {
                        generated = source.runtime_action_metadata.generated,
                        state_id = source.runtime_action_metadata.state_id,
                        slot_id = source.runtime_action_metadata.slot_id,
                        slot_role = source.runtime_action_metadata.slot_role,
                        skill_id = source.runtime_action_metadata.skill_id,
                        variant_id = source.runtime_action_metadata.variant_id,
                        action_family = source.runtime_action_metadata.action_family,
                        source_action_id = source.runtime_action_metadata.source_action_id,
                        identity_key = source.runtime_action_metadata.identity_key ?? "",
                    }
                );
            }
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

        private static void AddTraceStringName(
            Dictionary<string, object> result,
            string key,
            StringName value
        )
        {
            if (!BattleAiContext.IsEmpty(value))
                result[key] = value;
        }

        private static bool ReadTraceBoolValue(
            IReadOnlyDictionary<string, object> source,
            string key
        )
        {
            if (
                source == null
                || string.IsNullOrEmpty(key)
                || !source.TryGetValue(key, out object value)
                || value == null
            )
            {
                return false;
            }
            return value switch
            {
                bool boolValue => boolValue,
                Variant variant when variant.VariantType == Variant.Type.Bool => variant.AsBool(),
                _ => false,
            };
        }

        private static StringName ReadTraceStringNameValue(
            IReadOnlyDictionary<string, object> source,
            string key
        )
        {
            if (
                source == null
                || string.IsNullOrEmpty(key)
                || !source.TryGetValue(key, out object value)
                || value == null
            )
            {
                return "";
            }
            return value switch
            {
                StringName stringName => stringName,
                string text when !string.IsNullOrEmpty(text) => new StringName(text),
                Variant variant when variant.VariantType == Variant.Type.StringName =>
                    variant.AsStringName(),
                Variant variant when variant.VariantType == Variant.Type.String =>
                    new StringName(variant.AsString()),
                _ => "",
            };
        }

        private static string ReadTraceTextValue(
            IReadOnlyDictionary<string, object> source,
            string key
        )
        {
            if (
                source == null
                || string.IsNullOrEmpty(key)
                || !source.TryGetValue(key, out object value)
                || value == null
            )
            {
                return "";
            }
            return value switch
            {
                string text => text,
                StringName stringName => stringName.ToString(),
                Variant variant when variant.VariantType == Variant.Type.String =>
                    variant.AsString(),
                Variant variant when variant.VariantType == Variant.Type.StringName =>
                    variant.AsStringName().ToString(),
                _ => value.ToString() ?? "",
            };
        }

        private static bool TryReadTraceDictionaryValue(
            IReadOnlyDictionary<string, object> source,
            string key,
            out IReadOnlyDictionary<string, object> dictionary
        )
        {
            dictionary = null;
            if (
                source == null
                || string.IsNullOrEmpty(key)
                || !source.TryGetValue(key, out object value)
                || value == null
            )
            {
                return false;
            }
            if (value is IReadOnlyDictionary<string, object> readOnlyDictionary)
            {
                dictionary = readOnlyDictionary;
                return true;
            }
            if (value is Dictionary<string, object> concreteDictionary)
            {
                dictionary = concreteDictionary;
                return true;
            }
            return false;
        }
    }

    internal BattleAiQueryService GetAiQueryService()
    {
        return ai_query_service;
    }

    internal void ResetForDecision(
        BattleState battleState,
        BattleUnitState actorUnitState,
        BattleGridService battleGridService,
        BattleAiRuntimeActionPlan actionPlan,
        IReadOnlyDictionary<StringName, SkillDef> skillDefs,
        bool traceEnabled,
        ISkillCatalog skillCatalog = null
    )
    {
        state = battleState;
        unit_state = actorUnitState;
        grid_service = battleGridService;
        active_score_profile = null;
        skill_catalog = skillCatalog;
        runtime_action_plan = actionPlan;
        trace_enabled = traceEnabled;
        ai_query_service = null;
        candidate_evaluator = null;
        allow_authored_action_fallback_for_tests = false;
        ClearDecisionState();
        SetSkillDefsCached(skillDefs);
    }

    internal void ClearRuntimeBindings()
    {
        state = null;
        unit_state = null;
        grid_service = null;
        active_score_profile = null;
        runtime_action_plan = null;
        ai_query_service = null;
        candidate_evaluator = null;
        trace_enabled = false;
        allow_authored_action_fallback_for_tests = false;
        move_cost_callback = null;
        preview_command_callback = null;
        skill_score_input_callback = null;
        action_score_input_callback = null;
        skill_cast_block_reason_callback = null;
        ClearDecisionState();
        _skillDefsSource = null;
        skill_catalog = null;
        _skillDefsById.Clear();
    }

    private void ClearDecisionState()
    {
        _action_trace_nonce = 0;
        _action_metadata_stack.Clear();
        _action_trace_entries.Clear();
        _mutation_guard_violation_entries.Clear();
        _skill_affordance_records_by_skill_id.Clear();
        _sorted_target_units_cache.Clear();
    }

    internal bool TryGetSortedTargetUnits(
        StringName targetFilter,
        StringName selector,
        out List<BattleUnitState> units
    )
    {
        var key = new TargetSortCacheKey(
            ProgressionDataUtils.to_string_name(targetFilter),
            ProgressionDataUtils.to_string_name(selector)
        );
        if (_sorted_target_units_cache.TryGetValue(key, out List<BattleUnitState> cached))
        {
            units = new List<BattleUnitState>(cached);
            return true;
        }
        units = null;
        return false;
    }

    internal void CacheSortedTargetUnits(
        StringName targetFilter,
        StringName selector,
        List<BattleUnitState> units
    )
    {
        if (units == null)
        {
            return;
        }
        var key = new TargetSortCacheKey(
            ProgressionDataUtils.to_string_name(targetFilter),
            ProgressionDataUtils.to_string_name(selector)
        );
        _sorted_target_units_cache[key] = new List<BattleUnitState>(units);
    }

    internal BattleAiDecision EvaluateCandidateRequest(BattleAiCandidateRequest request)
    {
        AiTraceRecorder.Enter("candidate:context.evaluate_request");
        BattleAiDecision result = EvaluateCandidateRequestImpl(request);
        AiTraceRecorder.Exit("candidate:context.evaluate_request");
        return result;
    }

    private BattleAiDecision EvaluateCandidateRequestImpl(BattleAiCandidateRequest request)
    {
        if (request == null)
        {
            BattleAiPayloadGuard.FailLoud(
                "evaluate_candidate_request requires BattleAiCandidateRequest.",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["source"] = "BattleAiContext",
                }
            );
            return null;
        }
        if (candidate_evaluator == null)
        {
            BattleAiPayloadGuard.FailLoud(
                "evaluate_candidate_request requires candidate_evaluator.",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["source"] = "BattleAiContext",
                }
            );
            return null;
        }
        if (ai_query_service == null)
        {
            BattleAiPayloadGuard.FailLoud(
                "evaluate_candidate_request requires ai_query_service.",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["source"] = "BattleAiContext",
                }
            );
            return null;
        }
        return candidate_evaluator.Evaluate(request, ai_query_service);
    }

    internal int GetMoveCost(BattleUnitState target_unit_state, Vector2I target_coord)
    {
        if (move_cost_callback != null)
        {
            return move_cost_callback.Invoke(target_unit_state, target_coord);
        }
        if (grid_service != null && state != null)
        {
            return grid_service.GetUnitMoveCost(
                state,
                target_unit_state,
                target_coord
            );
        }
        return 1;
    }

    internal BattlePreview PreviewCommand(BattleCommand command)
    {
        if (command == null || preview_command_callback == null)
        {
            return null;
        }
        return preview_command_callback.Invoke(command);
    }

    internal BattleAiScoreInput BuildSkillScoreInputTyped(
        SkillDef skillDef,
        BattleCommand command,
        BattlePreview preview,
        IEnumerable<CombatEffectDef> effectDefs = null,
        IReadOnlyDictionary<string, object> metadata = null
    )
    {
        if (skill_score_input_callback == null || skillDef == null || command == null)
        {
            return null;
        }
        return skill_score_input_callback.Invoke(
            this,
            skillDef,
            command,
            preview,
            CopyCombatEffectList(effectDefs),
            metadata
        );
    }

    internal BattleAiScoreInput BuildActionScoreInputTyped(
        StringName actionKind,
        string actionLabel,
        StringName scoreBucketId,
        BattleCommand command,
        BattlePreview preview,
        IReadOnlyDictionary<string, object> metadata = null
    )
    {
        if (action_score_input_callback == null || command == null)
        {
            return null;
        }
        return action_score_input_callback.Invoke(
            this,
            actionKind,
            actionLabel,
            scoreBucketId,
            command,
            preview,
            metadata
        );
    }

    internal IReadOnlyList<EnemyAiAction> GetRuntimeActionsTyped(StringName state_id)
    {
        if (IsEmpty(state_id) || runtime_action_plan == null)
        {
            return System.Array.Empty<EnemyAiAction>();
        }
        return runtime_action_plan.GetActions(state_id);
    }

    internal bool HasRuntimeActionState(StringName state_id)
    {
        return !IsEmpty(state_id)
            && runtime_action_plan != null
            && runtime_action_plan.HasState(state_id);
    }

    internal bool IsRuntimeActionPlanStale(EnemyAiBrainDef brain)
    {
        return runtime_action_plan != null
            && runtime_action_plan.IsStaleFor(unit_state, brain);
    }

    internal BattleAiRuntimeActionPlan.RuntimeActionMetadata GetRuntimeActionMetadataTyped(
        EnemyAiAction action
    )
    {
        return runtime_action_plan != null
            ? runtime_action_plan.GetActionMetadata(action)
            : new BattleAiRuntimeActionPlan.RuntimeActionMetadata();
    }

    private static List<CombatEffectDef> CopyCombatEffectList(IEnumerable<CombatEffectDef> effectDefs)
    {
        var result = new List<CombatEffectDef>();
        foreach (CombatEffectDef effectDef in effectDefs ?? Array.Empty<CombatEffectDef>())
        {
            if (effectDef != null)
                result.Add(effectDef);
        }
        return result;
    }

    private static GDictionary ToStringNameIntDictionary(
        IReadOnlyDictionary<StringName, int> source
    )
    {
        var result = new GDictionary();
        if (source == null)
            return result;

        foreach (KeyValuePair<StringName, int> entry in source)
        {
            if (entry.Key != "")
                result[entry.Key] = entry.Value;
        }
        return result;
    }

    private static Godot.Collections.Array<StringName> ToStringNameArray(
        IEnumerable<StringName> values
    )
    {
        var result = new Godot.Collections.Array<StringName>();
        foreach (StringName value in values ?? Array.Empty<StringName>())
        {
            result.Add(ProgressionDataUtils.to_string_name(value));
        }
        return result;
    }

    private static Godot.Collections.Array<Vector2I> ToVector2IArray(
        IEnumerable<Vector2I> values
    )
    {
        var result = new Godot.Collections.Array<Vector2I>();
        foreach (Vector2I value in values ?? Array.Empty<Vector2I>())
        {
            result.Add(value);
        }
        return result;
    }
    internal BattleAiSkillAffordanceRecord GetSkillAffordanceRecordTyped(StringName skill_id)
    {
        StringName normalizedSkillId = ProgressionDataUtils.to_string_name(skill_id);
        if (IsEmpty(normalizedSkillId))
        {
            return null;
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
                return typedPlanRecord;
            }
        }
        if (
            _skill_affordance_records_by_skill_id.TryGetValue(
                normalizedSkillId,
                out BattleAiSkillAffordanceRecord cachedRecord
            )
        )
        {
            return cachedRecord;
        }

        if (!TryGetSkillDefTyped(normalizedSkillId, out SkillDef skillDef))
        {
            return null;
        }

        int skillLevel =
            unit_state != null ? Math.Max(unit_state.GetKnownSkillLevelTyped(normalizedSkillId), 1) : 1;
        BattleAiSkillAffordanceRecord record = _skill_affordance_classifier.ClassifySkill(
            skillDef,
            skillLevel,
            skill_catalog
        );
        if (record.skill_id == "")
        {
            record.skill_id = normalizedSkillId;
        }
        _skill_affordance_records_by_skill_id[normalizedSkillId] = record;
        return record;
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
        BattleAiSkillAffordanceRecord resolvedRecord = GetSkillAffordanceRecordTyped(
            normalizedSkillId
        );
        return resolvedRecord != null
            ? resolvedRecord.affordances
            : (IReadOnlyList<StringName>)System.Array.Empty<StringName>();
    }

    internal IReadOnlyDictionary<StringName, SkillDef> GetSkillDefIndexTyped() => _skillDefsById;

    internal void SetSkillDefs(IReadOnlyDictionary<StringName, SkillDef> skillDefs)
    {
        _skillDefsSource = null;
        RebuildSkillDefs(skillDefs);
    }

    private void SetSkillDefsCached(IReadOnlyDictionary<StringName, SkillDef> skillDefs)
    {
        if (ReferenceEquals(_skillDefsSource, skillDefs))
        {
            return;
        }
        RebuildSkillDefs(skillDefs);
        _skillDefsSource = skillDefs;
    }

    private void RebuildSkillDefs(IReadOnlyDictionary<StringName, SkillDef> skillDefs)
    {
        _skillDefsById = new Dictionary<StringName, SkillDef>();
        if (skillDefs == null)
        {
            return;
        }
        foreach ((StringName skillId, SkillDef skillDef) in skillDefs)
        {
            StringName normalizedSkillId = ProgressionDataUtils.to_string_name(skillId);
            if (!IsEmpty(normalizedSkillId) && skillDef != null)
            {
                _skillDefsById[normalizedSkillId] = skillDef;
            }
        }
    }

    internal SkillDef GetSkillDefTyped(StringName skillId)
    {
        return TryGetSkillDefTyped(skillId, out SkillDef skillDef) ? skillDef : null;
    }

    internal bool TryGetSkillDefTyped(StringName skillId, out SkillDef skillDef)
    {
        skillDef = null;
        StringName normalizedSkillId = ProgressionDataUtils.to_string_name(skillId);
        if (IsEmpty(normalizedSkillId))
        {
            return false;
        }
        return _skillDefsById.TryGetValue(normalizedSkillId, out skillDef) && skillDef != null;
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

    internal void PushActionMetadata(BattleAiRuntimeActionPlan.RuntimeActionMetadata metadata)
    {
        _action_metadata_stack.Add(RuntimeActionMetadata.FromPlanMetadata(metadata));
    }

    internal void PopActionMetadata()
    {
        if (_action_metadata_stack.Count == 0)
        {
            return;
        }
        _action_metadata_stack.RemoveAt(_action_metadata_stack.Count - 1);
    }

    internal Dictionary<string, object> MergeCurrentActionMetadataTyped(
        IReadOnlyDictionary<string, object> metadata = null
    )
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        if (metadata != null)
        {
            foreach (KeyValuePair<string, object> entry in metadata)
            {
                if (!string.IsNullOrEmpty(entry.Key))
                    result[entry.Key] = entry.Value;
            }
        }

        RuntimeActionMetadata merged =
            _action_metadata_stack.Count > 0
                ? _action_metadata_stack[^1].Clone()
                : new RuntimeActionMetadata();
        RuntimeActionMetadata incoming = RuntimeActionMetadata.FromTraceDictionary(metadata);
        merged.MergeFrom(incoming);
        Dictionary<string, object> runtimeMetadata = merged.ToTraceDictionary(
            includeRuntimeExport: true
        );
        foreach (KeyValuePair<string, object> entry in runtimeMetadata)
        {
            result[entry.Key] = entry.Value;
        }
        return result;
    }

    internal BattleUnitState ResolveForcedTargetUnit(StringName target_filter)
    {
        if (state == null || unit_state == null)
        {
            return null;
        }
        if (BattleTypedNames.ToTargetFilter(target_filter) != BattleTargetFilter.Enemy)
        {
            return null;
        }

        BattleStatusEffectState tauntEntry = unit_state.GetStatusEffect(StatusTaunted);
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

    internal StringName NextActionTraceId(StringName action_id)
    {
        _action_trace_nonce += 1;
        StringName normalizedActionId = !IsEmpty(action_id) ? action_id : AnonymousAction;
        return new StringName($"{normalizedActionId}_{_action_trace_nonce}");
    }

    internal void RecordActionTrace(AiActionTrace actionTrace)
    {
        if (!trace_enabled || actionTrace == null || actionTrace.IsEmpty())
        {
            return;
        }
        _action_trace_entries.Add(actionTrace);
    }

    internal void MarkActionTraceChosen(
        StringName action_trace_id,
        BattleAiDecision decision = null
    )
    {
        if (IsEmpty(action_trace_id))
        {
            return;
        }

        foreach (AiActionTrace actionTrace in _action_trace_entries)
        {
            if (actionTrace.TraceId != action_trace_id)
            {
                continue;
            }

            actionTrace.MarkChosen(decision);
            return;
        }
    }

    internal BattleAiTurnTraceProjection BuildTurnTraceTyped(BattleAiDecision decision)
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
            turnStartedTu = unit_state.ai_blackboard.GetInt("turn_started_tu", 0);
        }

        BattleAiScoreInput scoreInput = ResolveDecisionScoreInput(decision);
        return new BattleAiTurnTraceProjection
        {
            BattleId = state != null ? state.battle_id.ToString() : "",
            TurnStartedTu = turnStartedTu,
            UnitId = unit_state != null ? unit_state.unit_id.ToString() : "",
            UnitName = unit_state != null ? unit_state.display_name : "",
            FactionId = unit_state != null ? unit_state.faction_id.ToString() : "",
            BrainId = resolvedBrainId,
            StateId = resolvedStateId,
            ActionId = decision != null ? decision.action_id.ToString() : "",
            ReasonText = decision != null ? decision.reason_text : "",
            Command = decision != null ? AiCommandSummary.FromCommand(decision.command) : new AiCommandSummary(),
            Transition = BuildTransitionProjection(decision?.Transition),
            ScoreInput = scoreInput,
            ActionTraces = BuildActionTracePayloads(),
        };
    }

    private static BattleAiTraceTransitionProjection BuildTransitionProjection(
        BattleAiStateResolver.TransitionResult result
    )
    {
        if (result == null)
        {
            return new BattleAiTraceTransitionProjection();
        }

        var matchedConditions = new List<BattleAiTraceTransitionConditionProjection>();
        foreach (BattleAiStateResolver.TransitionConditionTrace condition in result.MatchedConditions)
        {
            if (condition != null)
            {
                matchedConditions.Add(BuildTransitionConditionProjection(condition));
            }
        }

        return new BattleAiTraceTransitionProjection
        {
            PreviousStateId = result.PreviousStateId.ToString(),
            StateId = result.StateId.ToString(),
            RuleId = result.RuleId.ToString(),
            Reason = result.Reason,
            MatchedConditions = matchedConditions,
        };
    }

    private static BattleAiTraceTransitionConditionProjection BuildTransitionConditionProjection(
        BattleAiStateResolver.TransitionConditionTrace condition
    )
    {
        if (condition == null)
        {
            return new BattleAiTraceTransitionConditionProjection();
        }
        return new BattleAiTraceTransitionConditionProjection
        {
            Predicate = condition.Predicate.ToString(),
            BasisPoints = condition.BasisPoints,
            MaxDistance = condition.MaxDistance,
            StateIds = BuildStringList(condition.StateIds),
            Affordances = BuildStringList(condition.Affordances),
        };
    }

    private static List<string> BuildStringList(
        IReadOnlyList<StringName> values
    )
    {
        var result = new List<string>();
        if (values == null)
        {
            return result;
        }
        foreach (StringName value in values)
        {
            result.Add(value.ToString());
        }
        return result;
    }

    internal IReadOnlyList<AiActionTrace> GetActionTracesTyped()
    {
        return new List<AiActionTrace>(_action_trace_entries);
    }

    internal IReadOnlyList<string> GetMutationGuardViolationsTyped()
    {
        return new List<string>(_mutation_guard_violation_entries);
    }

    private List<AiActionTrace> BuildActionTracePayloads()
    {
        return new List<AiActionTrace>(_action_trace_entries);
    }

    internal void ClearMutationGuardViolations()
    {
        _mutation_guard_violation_entries.Clear();
    }

    internal void SetMutationGuardViolations(IEnumerable<string> violations)
    {
        _mutation_guard_violation_entries.Clear();
        foreach (string violation in violations ?? Array.Empty<string>())
        {
            if (!string.IsNullOrEmpty(violation))
            {
                _mutation_guard_violation_entries.Add(violation);
            }
        }
    }

    private static bool _is_runtime_fixed_metadata_key(string key)
    {
        return RuntimeFixedMetadataKeys.Contains(key ?? "");
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
