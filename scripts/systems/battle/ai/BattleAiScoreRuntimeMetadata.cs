using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public sealed class BattleAiScoreRuntimeMetadata
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
    public bool HasActionBaseScore;
    public int action_base_score;
    public bool HasActiveRest;
    public bool active_rest;
    public int? configured_desired_min_distance;
    public int? configured_desired_max_distance;
    public int? effective_attack_range;
    public int? aoe_setup_bonus;
    public int? aoe_setup_enemy_hit_count;
    public int? aoe_setup_ally_hit_count;
    public StringName aoe_setup_skill_id = "";
    public StringName aoe_setup_kind = "";
    public Vector2I? aoe_setup_center;
    public Vector2I? aoe_setup_anchor;
    public Vector2I? aoe_setup_move_target;
    public StringName aoe_setup_area_pattern = "";
    public int? aoe_setup_area_value;
    public int? aoe_setup_cast_range;
    public int? aoe_setup_current_score;
    public int? aoe_setup_candidate_score;
    public bool HasAoeSetupFutureDiscounted;
    public bool aoe_setup_future_discounted;

    public BattleAiScoreRuntimeMetadata Clone()
    {
        return new BattleAiScoreRuntimeMetadata
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
            HasActionBaseScore = HasActionBaseScore,
            action_base_score = action_base_score,
            HasActiveRest = HasActiveRest,
            active_rest = active_rest,
            configured_desired_min_distance = configured_desired_min_distance,
            configured_desired_max_distance = configured_desired_max_distance,
            effective_attack_range = effective_attack_range,
            aoe_setup_bonus = aoe_setup_bonus,
            aoe_setup_enemy_hit_count = aoe_setup_enemy_hit_count,
            aoe_setup_ally_hit_count = aoe_setup_ally_hit_count,
            aoe_setup_skill_id = aoe_setup_skill_id,
            aoe_setup_kind = aoe_setup_kind,
            aoe_setup_center = aoe_setup_center,
            aoe_setup_anchor = aoe_setup_anchor,
            aoe_setup_move_target = aoe_setup_move_target,
            aoe_setup_area_pattern = aoe_setup_area_pattern,
            aoe_setup_area_value = aoe_setup_area_value,
            aoe_setup_cast_range = aoe_setup_cast_range,
            aoe_setup_current_score = aoe_setup_current_score,
            aoe_setup_candidate_score = aoe_setup_candidate_score,
            HasAoeSetupFutureDiscounted = HasAoeSetupFutureDiscounted,
            aoe_setup_future_discounted = aoe_setup_future_discounted,
        };
    }

    public bool IsEmpty()
    {
        return !generated
            && IsEmptyStringName(state_id)
            && IsEmptyStringName(slot_id)
            && IsEmptyStringName(slot_role)
            && IsEmptyStringName(skill_id)
            && IsEmptyStringName(variant_id)
            && IsEmptyStringName(action_family)
            && IsEmptyStringName(source_action_id)
            && IsEmptyStringName(score_bucket_id)
            && IsEmptyStringName(action_id)
            && string.IsNullOrEmpty(identity_key)
            && !HasActionBaseScore
            && !HasActiveRest
            && configured_desired_min_distance == null
            && configured_desired_max_distance == null
            && effective_attack_range == null
            && aoe_setup_bonus == null
            && aoe_setup_enemy_hit_count == null
            && aoe_setup_ally_hit_count == null
            && IsEmptyStringName(aoe_setup_skill_id)
            && IsEmptyStringName(aoe_setup_kind)
            && aoe_setup_center == null
            && aoe_setup_anchor == null
            && aoe_setup_move_target == null
            && IsEmptyStringName(aoe_setup_area_pattern)
            && aoe_setup_area_value == null
            && aoe_setup_cast_range == null
            && aoe_setup_current_score == null
            && aoe_setup_candidate_score == null
            && !HasAoeSetupFutureDiscounted;
    }

    internal Dictionary<string, object> ToTraceDictionary()
    {
        var result = new Dictionary<string, object>(System.StringComparer.Ordinal);
        if (generated)
        {
            result["generated"] = true;
        }
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
        {
            result["identity_key"] = identity_key;
        }
        if (HasActionBaseScore)
        {
            result["action_base_score"] = action_base_score;
        }
        if (HasActiveRest)
        {
            result["active_rest"] = active_rest;
        }
        AddTraceInt(result, "configured_desired_min_distance", configured_desired_min_distance);
        AddTraceInt(result, "configured_desired_max_distance", configured_desired_max_distance);
        AddTraceInt(result, "effective_attack_range", effective_attack_range);
        AddTraceInt(result, "aoe_setup_bonus", aoe_setup_bonus);
        AddTraceInt(result, "aoe_setup_enemy_hit_count", aoe_setup_enemy_hit_count);
        AddTraceInt(result, "aoe_setup_ally_hit_count", aoe_setup_ally_hit_count);
        AddTraceStringName(result, "aoe_setup_skill_id", aoe_setup_skill_id);
        AddTraceStringName(result, "aoe_setup_kind", aoe_setup_kind);
        AddTraceVector2I(result, "aoe_setup_center", aoe_setup_center);
        AddTraceVector2I(result, "aoe_setup_anchor", aoe_setup_anchor);
        AddTraceVector2I(result, "aoe_setup_move_target", aoe_setup_move_target);
        AddTraceStringName(result, "aoe_setup_area_pattern", aoe_setup_area_pattern);
        AddTraceInt(result, "aoe_setup_area_value", aoe_setup_area_value);
        AddTraceInt(result, "aoe_setup_cast_range", aoe_setup_cast_range);
        AddTraceInt(result, "aoe_setup_current_score", aoe_setup_current_score);
        AddTraceInt(result, "aoe_setup_candidate_score", aoe_setup_candidate_score);
        if (HasAoeSetupFutureDiscounted)
        {
            result["aoe_setup_future_discounted"] = aoe_setup_future_discounted;
        }
        return result;
    }

    internal GDictionary ToDictionary()
    {
        var result = new GDictionary();
        if (generated)
        {
            result["generated"] = true;
        }
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
        {
            result["identity_key"] = identity_key;
        }
        if (HasActionBaseScore)
        {
            result["action_base_score"] = action_base_score;
        }
        if (HasActiveRest)
        {
            result["active_rest"] = active_rest;
        }
        AddInt(result, "configured_desired_min_distance", configured_desired_min_distance);
        AddInt(result, "configured_desired_max_distance", configured_desired_max_distance);
        AddInt(result, "effective_attack_range", effective_attack_range);
        AddInt(result, "aoe_setup_bonus", aoe_setup_bonus);
        AddInt(result, "aoe_setup_enemy_hit_count", aoe_setup_enemy_hit_count);
        AddInt(result, "aoe_setup_ally_hit_count", aoe_setup_ally_hit_count);
        AddStringName(result, "aoe_setup_skill_id", aoe_setup_skill_id);
        AddStringName(result, "aoe_setup_kind", aoe_setup_kind);
        AddVector2I(result, "aoe_setup_center", aoe_setup_center);
        AddVector2I(result, "aoe_setup_anchor", aoe_setup_anchor);
        AddVector2I(result, "aoe_setup_move_target", aoe_setup_move_target);
        AddStringName(result, "aoe_setup_area_pattern", aoe_setup_area_pattern);
        AddInt(result, "aoe_setup_area_value", aoe_setup_area_value);
        AddInt(result, "aoe_setup_cast_range", aoe_setup_cast_range);
        AddInt(result, "aoe_setup_current_score", aoe_setup_current_score);
        AddInt(result, "aoe_setup_candidate_score", aoe_setup_candidate_score);
        if (HasAoeSetupFutureDiscounted)
        {
            result["aoe_setup_future_discounted"] = aoe_setup_future_discounted;
        }
        return result;
    }

    public static BattleAiScoreRuntimeMetadata FromMetadata(
        IReadOnlyDictionary<string, object> source
    )
    {
        var result = new BattleAiScoreRuntimeMetadata();
        if (
            TryReadTypedDictionary(
                source,
                "runtime_action_metadata",
                out IReadOnlyDictionary<string, object> nested
            )
        )
        {
            result.MergeMetadata(nested);
        }
        result.MergeMetadata(source);
        return result;
    }

    internal static BattleAiScoreRuntimeMetadata FromRuntimeActionExportMetadata(
        BattleAiRuntimeActionPlan.RuntimeActionExportMetadata source
    )
    {
        if (source == null || source.IsEmpty())
        {
            return new BattleAiScoreRuntimeMetadata();
        }
        return new BattleAiScoreRuntimeMetadata
        {
            generated = source.generated,
            state_id = source.state_id,
            slot_id = source.slot_id,
            slot_role = source.slot_role,
            skill_id = source.skill_id,
            variant_id = source.variant_id,
            action_family = source.action_family,
            source_action_id = source.source_action_id,
            identity_key = source.identity_key ?? "",
        };
    }

    private void MergeMetadata(IReadOnlyDictionary<string, object> source)
    {
        if (source == null)
        {
            return;
        }
        generated |= ReadBool(source, "generated");
        MergeStringName(ref state_id, ReadStringName(source, "state_id"));
        MergeStringName(ref slot_id, ReadStringName(source, "slot_id"));
        MergeStringName(ref slot_role, ReadStringName(source, "slot_role"));
        MergeStringName(ref skill_id, ReadStringName(source, "skill_id"));
        MergeStringName(ref variant_id, ReadStringName(source, "variant_id"));
        MergeStringName(ref action_family, ReadStringName(source, "action_family"));
        MergeStringName(ref source_action_id, ReadStringName(source, "source_action_id"));
        MergeStringName(ref score_bucket_id, ReadStringName(source, "score_bucket_id"));
        MergeStringName(ref action_id, ReadStringName(source, "action_id"));
        string nextIdentityKey = ReadString(source, "identity_key");
        if (!string.IsNullOrEmpty(nextIdentityKey))
        {
            identity_key = nextIdentityKey;
        }
        if (HasKey(source, "action_base_score"))
        {
            HasActionBaseScore = true;
            action_base_score = ReadInt(source, "action_base_score");
        }
        if (HasKey(source, "active_rest"))
        {
            HasActiveRest = true;
            active_rest = ReadBool(source, "active_rest");
        }
        MergeNullableInt(ref configured_desired_min_distance, source, "configured_desired_min_distance");
        MergeNullableInt(ref configured_desired_max_distance, source, "configured_desired_max_distance");
        MergeNullableInt(ref effective_attack_range, source, "effective_attack_range");
        MergeNullableInt(ref aoe_setup_bonus, source, "aoe_setup_bonus");
        MergeNullableInt(ref aoe_setup_enemy_hit_count, source, "aoe_setup_enemy_hit_count");
        MergeNullableInt(ref aoe_setup_ally_hit_count, source, "aoe_setup_ally_hit_count");
        MergeStringName(ref aoe_setup_skill_id, ReadStringName(source, "aoe_setup_skill_id"));
        MergeStringName(ref aoe_setup_kind, ReadStringName(source, "aoe_setup_kind"));
        MergeNullableVector(ref aoe_setup_center, source, "aoe_setup_center");
        MergeNullableVector(ref aoe_setup_anchor, source, "aoe_setup_anchor");
        MergeNullableVector(ref aoe_setup_move_target, source, "aoe_setup_move_target");
        MergeStringName(ref aoe_setup_area_pattern, ReadStringName(source, "aoe_setup_area_pattern"));
        MergeNullableInt(ref aoe_setup_area_value, source, "aoe_setup_area_value");
        MergeNullableInt(ref aoe_setup_cast_range, source, "aoe_setup_cast_range");
        MergeNullableInt(ref aoe_setup_current_score, source, "aoe_setup_current_score");
        MergeNullableInt(ref aoe_setup_candidate_score, source, "aoe_setup_candidate_score");
        if (HasKey(source, "aoe_setup_future_discounted"))
        {
            HasAoeSetupFutureDiscounted = true;
            aoe_setup_future_discounted = ReadBool(source, "aoe_setup_future_discounted");
        }
    }

    private static void MergeStringName(ref StringName target, StringName incoming)
    {
        if (!IsEmptyStringName(incoming))
        {
            target = incoming;
        }
    }

    private static void MergeNullableInt(
        ref int? target,
        IReadOnlyDictionary<string, object> source,
        string key
    )
    {
        if (HasKey(source, key))
        {
            target = ReadInt(source, key);
        }
    }

    private static void MergeNullableVector(
        ref Vector2I? target,
        IReadOnlyDictionary<string, object> source,
        string key
    )
    {
        if (HasKey(source, key))
        {
            target = ReadVector2I(source, key);
        }
    }

    private static bool HasKey(IReadOnlyDictionary<string, object> source, string key)
    {
        return source != null && !string.IsNullOrEmpty(key) && source.ContainsKey(key);
    }

    private static bool TryReadTypedDictionary(
        IReadOnlyDictionary<string, object> source,
        string key,
        out IReadOnlyDictionary<string, object> result
    )
    {
        result = null;
        if (!HasKey(source, key))
        {
            return false;
        }
        result = source[key] as IReadOnlyDictionary<string, object>;
        return result != null;
    }

    private static bool ReadBool(IReadOnlyDictionary<string, object> source, string key)
    {
        if (!HasKey(source, key))
        {
            return false;
        }
        object value = source[key];
        return value switch
        {
            bool flag => flag,
            int intValue => intValue != 0,
            long longValue => longValue != 0,
            string text when bool.TryParse(text, out bool parsed) => parsed,
            _ => false,
        };
    }

    private static int ReadInt(IReadOnlyDictionary<string, object> source, string key)
    {
        if (!HasKey(source, key))
        {
            return 0;
        }
        object value = source[key];
        return value switch
        {
            int intValue => intValue,
            long longValue => (int)longValue,
            float floatValue => (int)floatValue,
            double doubleValue => (int)doubleValue,
            string text when int.TryParse(text, out int parsed) => parsed,
            _ => 0,
        };
    }

    private static StringName ReadStringName(IReadOnlyDictionary<string, object> source, string key)
    {
        if (!HasKey(source, key))
        {
            return "";
        }
        return ProgressionDataUtils.to_string_name(source[key]);
    }

    private static string ReadString(IReadOnlyDictionary<string, object> source, string key)
    {
        if (!HasKey(source, key))
        {
            return "";
        }
        return source[key]?.ToString() ?? "";
    }

    private static Vector2I ReadVector2I(IReadOnlyDictionary<string, object> source, string key)
    {
        if (!HasKey(source, key))
        {
            return new Vector2I(-1, -1);
        }
        return source[key] is Vector2I coord ? coord : new Vector2I(-1, -1);
    }

    private static void AddStringName(GDictionary target, string key, StringName value)
    {
        if (!IsEmptyStringName(value))
        {
            target[key] = value;
        }
    }

    private static void AddInt(GDictionary target, string key, int? value)
    {
        if (value != null)
        {
            target[key] = value.Value;
        }
    }

    private static void AddVector2I(GDictionary target, string key, Vector2I? value)
    {
        if (value != null)
        {
            target[key] = value.Value;
        }
    }

    private static void AddTraceStringName(
        Dictionary<string, object> target,
        string key,
        StringName value
    )
    {
        if (!IsEmptyStringName(value))
        {
            target[key] = value.ToString();
        }
    }

    private static void AddTraceInt(Dictionary<string, object> target, string key, int? value)
    {
        if (value != null)
        {
            target[key] = value.Value;
        }
    }

    private static void AddTraceVector2I(
        Dictionary<string, object> target,
        string key,
        Vector2I? value
    )
    {
        if (value != null)
        {
            target[key] = value.Value;
        }
    }

    private static bool IsEmptyStringName(StringName value)
    {
        return ProgressionDataUtils.to_string_name(value) == "";
    }
}
