using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class EnemyAiActionHelper : RefCounted
{
    private sealed class ActionTraceState
    {
        private readonly Dictionary<string, int> _counters = new();
        private readonly List<ActionTraceCandidateSummary> _topCandidates = new();
        private readonly List<TracePayloadField> _extraFields = new();
        private TraceReasonCounts _blockReasons = new();

        public StringName TraceId { get; private set; } = "";
        public StringName ActionId { get; private set; } = "";
        public StringName ScoreBucketId { get; private set; } = "";
        public GDictionary Metadata { get; private set; } = new();
        public bool Chosen { get; private set; }

        public void Increment(string key, int amount = 1)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }
            _counters[key] = _counters.GetValueOrDefault(key, 0) + amount;
        }

        public void AddBlockReason(string reasonKey)
        {
            if (string.IsNullOrEmpty(reasonKey))
            {
                return;
            }
            Increment("blocked_count", 1);
            _blockReasons.Increment(reasonKey);
        }

        public void OfferCandidate(ActionTraceCandidateSummary candidate, int keepCount)
        {
            if (candidate == null)
            {
                return;
            }
            Increment("candidate_count", 1);
            _topCandidates.Add(candidate.Clone());
            _topCandidates.Sort(
                (left, right) => right.TotalScore.CompareTo(left.TotalScore)
            );

            int limit = Mathf.Max(keepCount, 0);
            if (_topCandidates.Count > limit)
            {
                _topCandidates.RemoveRange(limit, _topCandidates.Count - limit);
            }
        }

        public void SetBestDecision(BattleAiDecision decision)
        {
            if (decision == null)
            {
                return;
            }
            SetExtraField("best_reason_text", decision.reason_text);
            SetExtraField("best_command", build_command_summary(decision.command));
            BattleAiScoreInput scoreInput = decision.score_input ?? decision.skill_score_input;
            SetExtraField("best_score_input", EnemyAiActionHelper.ToDictionary(scoreInput));
            decision.action_trace_id = TraceId;
        }

        public GDictionary ToDictionary()
        {
            var result = new GDictionary
            {
                ["trace_id"] = TraceId,
                ["action_id"] = ActionId.ToString(),
                ["score_bucket_id"] = ScoreBucketId.ToString(),
                ["metadata"] = Metadata?.Duplicate(true) ?? new GDictionary(),
            };
            foreach (KeyValuePair<string, int> counter in _counters)
            {
                result[counter.Key] = counter.Value;
            }
            result["block_reasons"] = _blockReasons.ToDictionary();
            result["top_candidates"] = TopCandidatesToArray();
            result["chosen"] = Chosen;
            foreach (TracePayloadField field in _extraFields)
            {
                result[field.Key] = field.Value;
            }
            return result;
        }

        public static ActionTraceState Create(
            StringName traceId,
            StringName actionId,
            StringName scoreBucketId,
            GDictionary metadata
        )
        {
            var trace = new ActionTraceState
            {
                TraceId = traceId,
                ActionId = actionId,
                ScoreBucketId = scoreBucketId,
                Metadata = metadata?.Duplicate(true) ?? new GDictionary(),
                Chosen = false,
            };
            trace._counters["evaluation_count"] = 0;
            trace._counters["blocked_count"] = 0;
            trace._counters["preview_reject_count"] = 0;
            trace._counters["candidate_count"] = 0;
            return trace;
        }

        public static ActionTraceState FromDictionary(GDictionary source)
        {
            var trace = new ActionTraceState();
            foreach (TracePayloadField field in ReadPayloadFields(source))
            {
                string key = field.KeyText;
                switch (key)
                {
                    case "trace_id":
                        trace.TraceId = ReadStringName(field.Value);
                        break;
                    case "action_id":
                        trace.ActionId = ReadStringName(field.Value);
                        break;
                    case "score_bucket_id":
                        trace.ScoreBucketId = ReadStringName(field.Value);
                        break;
                    case "metadata":
                        trace.Metadata = ReadDictionary(field.Value);
                        break;
                    case "block_reasons":
                        trace._blockReasons = TraceReasonCounts.FromDictionary(
                            ReadDictionary(field.Value)
                        );
                        break;
                    case "top_candidates":
                        trace._topCandidates.AddRange(
                            ActionTraceCandidateSummary.FromArray(ReadArray(field.Value))
                        );
                        break;
                    case "chosen":
                        trace.Chosen = ReadBool(field.Value);
                        break;
                    default:
                        if (IsCounterKey(key))
                        {
                            trace._counters[key] = ReadInt(field.Value, 0);
                        }
                        else
                        {
                            trace.SetExtraField(field);
                        }
                        break;
                }
            }
            return trace;
        }

        private void SetExtraField(string key, string value)
        {
            SetExtraField(
                new TracePayloadField(
                    Variant.From(key),
                    Variant.From(value ?? "")
                )
            );
        }

        private void SetExtraField(string key, GDictionary value)
        {
            SetExtraField(
                new TracePayloadField(
                    Variant.From(key),
                    Variant.From(value ?? new GDictionary())
                )
            );
        }

        private void SetExtraField(TracePayloadField field)
        {
            if (string.IsNullOrEmpty(field.KeyText))
            {
                return;
            }
            for (int index = 0; index < _extraFields.Count; index += 1)
            {
                if (_extraFields[index].KeyText == field.KeyText)
                {
                    _extraFields[index] = field.Clone();
                    return;
                }
            }
            _extraFields.Add(field.Clone());
        }

        private GArray TopCandidatesToArray()
        {
            var result = new GArray();
            foreach (ActionTraceCandidateSummary candidate in _topCandidates)
            {
                result.Add(candidate.ToDictionary());
            }
            return result;
        }

        private static bool IsCounterKey(string key) =>
            !string.IsNullOrEmpty(key) && key.EndsWith("_count");
    }

    private sealed class TraceReasonCounts
    {
        private readonly Dictionary<string, int> _counts = new();

        public void Increment(string reasonKey)
        {
            if (string.IsNullOrEmpty(reasonKey))
            {
                return;
            }
            _counts[reasonKey] = _counts.GetValueOrDefault(reasonKey, 0) + 1;
        }

        public GDictionary ToDictionary()
        {
            var result = new GDictionary();
            foreach (KeyValuePair<string, int> entry in _counts)
            {
                result[entry.Key] = entry.Value;
            }
            return result;
        }

        public static TraceReasonCounts FromDictionary(GDictionary source)
        {
            var result = new TraceReasonCounts();
            foreach (TracePayloadField field in ReadPayloadFields(source))
            {
                if (!string.IsNullOrEmpty(field.KeyText))
                {
                    result._counts[field.KeyText] = ReadInt(field.Value, 0);
                }
            }
            return result;
        }
    }

    private sealed class ActionTraceCandidateSummary
    {
        private readonly List<TracePayloadField> _extraFields = new();

        public string Label { get; private set; } = "";
        public GDictionary Command { get; private set; } = new();
        public int TotalScore { get; private set; }
        public GDictionary ScoreInput { get; private set; } = new();

        public ActionTraceCandidateSummary Clone()
        {
            var clone = new ActionTraceCandidateSummary
            {
                Label = Label,
                Command = Command?.Duplicate(true) ?? new GDictionary(),
                TotalScore = TotalScore,
                ScoreInput = ScoreInput?.Duplicate(true) ?? new GDictionary(),
            };
            foreach (TracePayloadField field in _extraFields)
            {
                clone._extraFields.Add(field.Clone());
            }
            return clone;
        }

        public GDictionary ToDictionary()
        {
            var result = new GDictionary
            {
                ["label"] = Label,
                ["command"] = Command?.Duplicate(true) ?? new GDictionary(),
                ["total_score"] = TotalScore,
                ["score_input"] = ScoreInput?.Duplicate(true) ?? new GDictionary(),
            };
            foreach (TracePayloadField field in _extraFields)
            {
                result[field.Key] = field.Value;
            }
            return result;
        }

        public static ActionTraceCandidateSummary Create(
            string label,
            BattleCommand command,
            BattleAiScoreInput scoreInput,
            GDictionary extra
        )
        {
            var result = new ActionTraceCandidateSummary
            {
                Label = label ?? "",
                Command = build_command_summary(command),
                TotalScore = scoreInput != null ? scoreInput.total_score : GetInt(extra, "total_score"),
                ScoreInput = EnemyAiActionHelper.ToDictionary(scoreInput),
            };
            foreach (TracePayloadField field in ReadPayloadFields(extra))
            {
                result.SetExtraField(field);
                if (field.KeyText == "total_score")
                {
                    result.TotalScore = ReadInt(field.Value, result.TotalScore);
                }
            }
            return result;
        }

        public static ActionTraceCandidateSummary FromDictionary(GDictionary source)
        {
            var result = new ActionTraceCandidateSummary();
            foreach (TracePayloadField field in ReadPayloadFields(source))
            {
                switch (field.KeyText)
                {
                    case "label":
                        result.Label = ReadString(field.Value);
                        break;
                    case "command":
                        result.Command = ReadDictionary(field.Value);
                        break;
                    case "total_score":
                        result.TotalScore = ReadInt(field.Value, -999999);
                        break;
                    case "score_input":
                        result.ScoreInput = ReadDictionary(field.Value);
                        break;
                    default:
                        result.SetExtraField(field);
                        break;
                }
            }
            return result;
        }

        public static List<ActionTraceCandidateSummary> FromArray(GArray source)
        {
            var result = new List<ActionTraceCandidateSummary>();
            foreach (TracePayloadValue value in ReadPayloadValues(source))
            {
                if (value.TryGetDictionary(out GDictionary candidate))
                {
                    result.Add(FromDictionary(candidate));
                }
            }
            return result;
        }

        private void SetExtraField(TracePayloadField field)
        {
            if (string.IsNullOrEmpty(field.KeyText))
            {
                return;
            }
            for (int index = 0; index < _extraFields.Count; index += 1)
            {
                if (_extraFields[index].KeyText == field.KeyText)
                {
                    _extraFields[index] = field.Clone();
                    return;
                }
            }
            _extraFields.Add(field.Clone());
        }
    }

    private readonly struct TracePayloadField
    {
        public TracePayloadField(Variant key, Variant value)
        {
            Key = key;
            Value = value;
            KeyText = ReadKeyText(key);
        }

        public Variant Key { get; }
        public Variant Value { get; }
        public string KeyText { get; }

        public TracePayloadField Clone() => new(Key, CloneVariantValue(Value));
    }

    private readonly struct TracePayloadValue
    {
        public TracePayloadValue(Variant value)
        {
            Value = value;
        }

        public Variant Value { get; }

        public bool TryGetDictionary(out GDictionary value)
        {
            if (Value.VariantType == Variant.Type.Dictionary)
            {
                value = Value.AsGodotDictionary();
                return true;
            }
            value = new GDictionary();
            return false;
        }

        public bool TryGetVector2I(out Vector2I value)
        {
            if (Value.VariantType == Variant.Type.Vector2I)
            {
                value = Value.AsVector2I();
                return true;
            }
            value = default;
            return false;
        }
    }

    public static BattleAiDecision create_decision(
        StringName action_id,
        StringName score_bucket_id,
        BattleCommand command,
        string reason_text = ""
    )
    {
        return new BattleAiDecision
        {
            command = command,
            action_id = action_id,
            reason_text = reason_text,
            score_bucket_id = score_bucket_id,
        };
    }

    public static BattleAiDecision create_scored_decision(
        StringName action_id,
        StringName score_bucket_id,
        BattleCommand command,
        BattleAiScoreInput score_input,
        string reason_text = ""
    )
    {
        var decision = create_decision(action_id, score_bucket_id, command, reason_text);
        decision.skill_score_input = score_input;
        decision.score_input = score_input;
        return decision;
    }

    public static BattleCommand build_wait_command(BattleAiContext context)
    {
        BattleUnitState unitState = context?.unit_state;
        if (unitState == null)
            return null;
        return new BattleCommand
        {
            command_type = BattleCommand.TYPE_WAIT(),
            unit_id = unitState.unit_id,
        };
    }

    public static BattleCommand build_move_command(BattleAiContext context, Vector2I target_coord)
    {
        BattleUnitState unitState = context?.unit_state;
        if (unitState == null)
            return null;
        return new BattleCommand
        {
            command_type = BattleCommand.TYPE_MOVE(),
            unit_id = unitState.unit_id,
            target_coord = target_coord,
        };
    }

    public static BattleCommand build_unit_skill_command(
        BattleAiContext context,
        StringName skill_id,
        BattleUnitState target_unit,
        StringName skill_variant_id = default
    )
    {
        BattleUnitState unitState = context?.unit_state;
        if (unitState == null || target_unit == null)
            return null;
        return new BattleCommand
        {
            command_type = BattleCommand.TYPE_SKILL(),
            unit_id = unitState.unit_id,
            skill_id = skill_id,
            skill_variant_id = skill_variant_id,
            target_unit_id = target_unit.unit_id,
            target_coord = target_unit.coord,
        };
    }

    public static BattleCommand build_ground_skill_command(
        BattleAiContext context,
        StringName skill_id,
        StringName skill_variant_id,
        GArray target_coords
    )
    {
        BattleUnitState unitState = context?.unit_state;
        if (unitState == null)
            return null;
        var sortedCoords = sort_coords(target_coords);
        var command = new BattleCommand
        {
            command_type = BattleCommand.TYPE_SKILL(),
            unit_id = unitState.unit_id,
            skill_id = skill_id,
            skill_variant_id = skill_variant_id,
            target_coords = sortedCoords,
        };
        if (command.target_coords.Count > 0)
            command.target_coord = command.target_coords[0];
        return command;
    }

    public static Godot.Collections.Array<Vector2I> sort_coords(GArray coords)
    {
        List<Vector2I> coordList = ReadVector2ICoords(coords);
        coordList.Sort(
            (left, right) =>
            {
                int yComparison = left.Y.CompareTo(right.Y);
                return yComparison != 0 ? yComparison : left.X.CompareTo(right.X);
            }
        );
        var sortedCoords = new Godot.Collections.Array<Vector2I>();
        foreach (Vector2I coord in coordList)
            sortedCoords.Add(coord);
        return sortedCoords;
    }

    public static string coord_set_key(GArray coords)
    {
        var parts = new List<string>();
        foreach (Vector2I coord in sort_coords(coords))
            parts.Add($"{coord.X}:{coord.Y}");
        return string.Join("|", parts);
    }

    public static GDictionary begin_action_trace(
        StringName action_id,
        StringName score_bucket_id,
        BattleAiContext context,
        GDictionary metadata = null
    )
    {
        StringName traceId = context != null ? context.next_action_trace_id(action_id) : action_id;
        return ActionTraceState.Create(traceId, action_id, score_bucket_id, metadata).ToDictionary();
    }

    public static void trace_count_increment(GDictionary action_trace, string key, int amount = 1)
    {
        if (action_trace == null || action_trace.Count == 0 || string.IsNullOrEmpty(key))
            return;
        ActionTraceState trace = ActionTraceState.FromDictionary(action_trace);
        trace.Increment(key, amount);
        ReplaceDictionaryContents(action_trace, trace.ToDictionary());
    }

    public static void trace_add_block_reason(GDictionary action_trace, string reason_key)
    {
        if (action_trace == null || action_trace.Count == 0 || string.IsNullOrEmpty(reason_key))
            return;
        ActionTraceState trace = ActionTraceState.FromDictionary(action_trace);
        trace.AddBlockReason(reason_key);
        ReplaceDictionaryContents(action_trace, trace.ToDictionary());
    }

    public static void trace_offer_candidate(
        GDictionary action_trace,
        GDictionary candidate_summary,
        int keep_count = 5
    )
    {
        if (
            action_trace == null
            || action_trace.Count == 0
            || candidate_summary == null
            || candidate_summary.Count == 0
        )
            return;
        ActionTraceState trace = ActionTraceState.FromDictionary(action_trace);
        trace.OfferCandidate(
            ActionTraceCandidateSummary.FromDictionary(candidate_summary),
            keep_count
        );
        ReplaceDictionaryContents(action_trace, trace.ToDictionary());
    }

    public static StringName finalize_action_trace(
        BattleAiContext context,
        GDictionary action_trace,
        BattleAiDecision best_decision = null
    )
    {
        if (action_trace == null || action_trace.Count == 0)
            return "";
        ActionTraceState trace = ActionTraceState.FromDictionary(action_trace);
        if (best_decision != null)
        {
            trace.SetBestDecision(best_decision);
            ReplaceDictionaryContents(action_trace, trace.ToDictionary());
        }
        context?.record_action_trace(action_trace);
        return trace.TraceId;
    }

    public static GDictionary build_candidate_summary(
        string label,
        BattleCommand command,
        BattleAiScoreInput score_input = null,
        GDictionary extra = null
    )
    {
        return ActionTraceCandidateSummary.Create(label, command, score_input, extra).ToDictionary();
    }

    public static string format_skill_variant_label(
        SkillDef skill_def,
        CombatCastVariantDef cast_variant
    )
    {
        if (skill_def == null)
            return "";
        if (cast_variant == null || cast_variant.display_name.Length == 0)
            return skill_def.display_name;
        return $"{skill_def.display_name}·{cast_variant.display_name}";
    }

    public static GDictionary build_command_summary(BattleCommand command)
    {
        if (command == null)
            return new GDictionary();
        return new GDictionary
        {
            ["command_type"] = (string)command.command_type,
            ["unit_id"] = (string)command.unit_id,
            ["skill_id"] = (string)command.skill_id,
            ["skill_variant_id"] = (string)command.skill_variant_id,
            ["target_unit_id"] = (string)command.target_unit_id,
            ["target_unit_ids"] = command.target_unit_ids.Duplicate(),
            ["target_coord"] = command.target_coord,
            ["target_coords"] = command.target_coords.Duplicate(),
        };
    }

    private static GDictionary ToDictionary(BattleAiScoreInput value)
    {
        if (value == null)
            return new GDictionary();
        return value.to_dict();
    }

    private static void ReplaceDictionaryContents(GDictionary target, GDictionary source)
    {
        if (target == null)
        {
            return;
        }
        target.Clear();
        foreach (TracePayloadField field in ReadPayloadFields(source))
        {
            TracePayloadField clonedField = field.Clone();
            target[clonedField.Key] = clonedField.Value;
        }
    }

    private static List<TracePayloadField> ReadPayloadFields(GDictionary source)
    {
        var result = new List<TracePayloadField>();
        if (source == null)
        {
            return result;
        }
        foreach (var key in source.Keys)
        {
            if (TryGetDictionaryValue(source, key, out Variant value))
            {
                result.Add(new TracePayloadField(key, value));
            }
        }
        return result;
    }

    private static List<TracePayloadValue> ReadPayloadValues(GArray source)
    {
        var result = new List<TracePayloadValue>();
        if (source == null)
        {
            return result;
        }
        foreach (var rawValue in source)
        {
            result.Add(new TracePayloadValue(rawValue));
        }
        return result;
    }

    private static List<Vector2I> ReadVector2ICoords(GArray coords)
    {
        var result = new List<Vector2I>();
        foreach (TracePayloadValue value in ReadPayloadValues(coords))
        {
            if (value.TryGetVector2I(out Vector2I coord))
            {
                result.Add(coord);
            }
        }
        return result;
    }

    private static int GetInt(GDictionary data, string key)
    {
        return TryGetDictionaryValue(data, key, out Variant value) ? ReadInt(value, 0) : 0;
    }

    private static bool TryGetDictionaryValue(
        GDictionary dictionary,
        string key,
        out Variant value
    )
    {
        value = default;
        if (dictionary == null || string.IsNullOrEmpty(key))
        {
            return false;
        }
        if (dictionary.ContainsKey(key))
        {
            value = dictionary[key];
            return true;
        }
        StringName stringNameKey = new(key);
        if (dictionary.ContainsKey(stringNameKey))
        {
            value = dictionary[stringNameKey];
            return true;
        }
        return false;
    }

    private static bool TryGetDictionaryValue(
        GDictionary dictionary,
        Variant key,
        out Variant value
    )
    {
        value = default;
        if (dictionary == null)
        {
            return false;
        }
        if (dictionary.ContainsKey(key))
        {
            value = dictionary[key];
            return true;
        }
        return false;
    }

    private static Variant CloneVariantValue(Variant value)
    {
        return value.VariantType switch
        {
            Variant.Type.Dictionary
                => Variant.From(value.AsGodotDictionary().Duplicate(true)),
            Variant.Type.Array => Variant.From(value.AsGodotArray().Duplicate(true)),
            _ => value,
        };
    }

    private static string ReadKeyText(Variant key)
    {
        return key.VariantType switch
        {
            Variant.Type.String => key.AsString(),
            Variant.Type.StringName => key.AsStringName().ToString(),
            Variant.Type.Nil => "",
            _ => key.ToString(),
        };
    }

    private static string ReadString(Variant value)
    {
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            Variant.Type.Nil => "",
            _ => value.ToString(),
        };
    }

    private static StringName ReadStringName(Variant value)
    {
        return value.VariantType switch
        {
            Variant.Type.StringName => value.AsStringName(),
            Variant.Type.String => new StringName(value.AsString()),
            Variant.Type.Nil => "",
            _ => ProgressionDataUtils.to_string_name(value),
        };
    }

    private static int ReadInt(Variant value, int fallback = 0)
    {
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static bool ReadBool(Variant value, bool fallback = false)
    {
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
    }

    private static GDictionary ReadDictionary(Variant value)
    {
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary().Duplicate(true)
            : new GDictionary();
    }

    private static GArray ReadArray(Variant value)
    {
        return value.VariantType == Variant.Type.Array
            ? value.AsGodotArray().Duplicate(true)
            : new GArray();
    }
}
