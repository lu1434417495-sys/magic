using Godot;
using System.Collections.Generic;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class EnemyAiActionHelper : RefCounted
{
    public static BattleAiDecision create_decision(StringName action_id, StringName score_bucket_id, BattleCommand command, string reason_text = "")
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
        GodotObject score_input,
        string reason_text = ""
    )
    {
        var decision = create_decision(action_id, score_bucket_id, command, reason_text);
        decision.skill_score_input = score_input;
        decision.score_input = score_input;
        return decision;
    }

    public static BattleCommand build_wait_command(GodotObject context)
    {
        BattleUnitState unitState = GetContextUnitState(context);
        if (unitState == null)
            return null;
        return new BattleCommand
        {
            command_type = BattleCommand.TYPE_WAIT(),
            unit_id = unitState.unit_id,
        };
    }

    public static BattleCommand build_move_command(GodotObject context, Vector2I target_coord)
    {
        BattleUnitState unitState = GetContextUnitState(context);
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
        GodotObject context,
        StringName skill_id,
        BattleUnitState target_unit,
        StringName skill_variant_id = default
    )
    {
        BattleUnitState unitState = GetContextUnitState(context);
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
        GodotObject context,
        StringName skill_id,
        StringName skill_variant_id,
        GArray target_coords
    )
    {
        BattleUnitState unitState = GetContextUnitState(context);
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
        var coordList = new List<Vector2I>();
        if (coords == null)
            return new Godot.Collections.Array<Vector2I>();
        foreach (Variant coordVariant in coords)
        {
            if (coordVariant.VariantType == Variant.Type.Vector2I)
                coordList.Add(coordVariant.AsVector2I());
        }
        coordList.Sort((left, right) =>
        {
            int yComparison = left.Y.CompareTo(right.Y);
            return yComparison != 0 ? yComparison : left.X.CompareTo(right.X);
        });
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
        GodotObject context,
        GDictionary metadata = null
    )
    {
        StringName traceId = action_id;
        if (context != null && context.HasMethod("next_action_trace_id"))
            traceId = ProgressionDataUtils.to_string_name(context.Call("next_action_trace_id", action_id));
        return new GDictionary
        {
            ["trace_id"] = traceId,
            ["action_id"] = (string)action_id,
            ["score_bucket_id"] = (string)score_bucket_id,
            ["metadata"] = metadata?.Duplicate(true) ?? new GDictionary(),
            ["evaluation_count"] = 0,
            ["blocked_count"] = 0,
            ["preview_reject_count"] = 0,
            ["candidate_count"] = 0,
            ["block_reasons"] = new GDictionary(),
            ["top_candidates"] = new GArray(),
            ["chosen"] = false,
        };
    }

    public static void trace_count_increment(GDictionary action_trace, string key, int amount = 1)
    {
        if (action_trace == null || action_trace.Count == 0 || string.IsNullOrEmpty(key))
            return;
        action_trace[key] = GetInt(action_trace, key) + amount;
    }

    public static void trace_add_block_reason(GDictionary action_trace, string reason_key)
    {
        if (action_trace == null || action_trace.Count == 0 || string.IsNullOrEmpty(reason_key))
            return;
        trace_count_increment(action_trace, "blocked_count", 1);
        GDictionary blockReasons = Get(action_trace, "block_reasons", new GDictionary()).AsGodotDictionary();
        blockReasons[reason_key] = GetInt(blockReasons, reason_key) + 1;
        action_trace["block_reasons"] = blockReasons;
    }

    public static void trace_offer_candidate(GDictionary action_trace, GDictionary candidate_summary, int keep_count = 5)
    {
        if (action_trace == null || action_trace.Count == 0 || candidate_summary == null || candidate_summary.Count == 0)
            return;
        trace_count_increment(action_trace, "candidate_count", 1);
        var topCandidates = new List<GDictionary>();
        Variant topCandidatesVariant = Get(action_trace, "top_candidates", new GArray());
        if (topCandidatesVariant.VariantType == Variant.Type.Array)
        {
            foreach (Variant candidateVariant in topCandidatesVariant.AsGodotArray())
            {
                if (candidateVariant.VariantType == Variant.Type.Dictionary)
                    topCandidates.Add(candidateVariant.AsGodotDictionary().Duplicate(true));
            }
        }
        topCandidates.Add(candidate_summary.Duplicate(true));
        topCandidates.Sort((left, right) => GetInt(right, "total_score").CompareTo(GetInt(left, "total_score")));

        var kept = new GArray();
        int limit = Mathf.Max(keep_count, 0);
        for (int i = 0; i < topCandidates.Count && i < limit; i++)
            kept.Add(topCandidates[i]);
        action_trace["top_candidates"] = kept;
    }

    public static StringName finalize_action_trace(GodotObject context, GDictionary action_trace, BattleAiDecision best_decision = null)
    {
        if (action_trace == null || action_trace.Count == 0)
            return "";
        if (best_decision != null)
        {
            action_trace["best_reason_text"] = best_decision.reason_text;
            action_trace["best_command"] = build_command_summary(best_decision.command);
            GodotObject scoreInput = best_decision.score_input ?? best_decision.skill_score_input;
            action_trace["best_score_input"] = ToDictionary(scoreInput);
            best_decision.action_trace_id = ProgressionDataUtils.to_string_name(Get(action_trace, "trace_id", ""));
        }
        if (context != null && context.HasMethod("record_action_trace"))
            context.Call("record_action_trace", action_trace);
        return ProgressionDataUtils.to_string_name(Get(action_trace, "trace_id", ""));
    }

    public static GDictionary build_candidate_summary(
        string label,
        BattleCommand command,
        GodotObject score_input = null,
        GDictionary extra = null
    )
    {
        var summary = new GDictionary
        {
            ["label"] = label,
            ["command"] = build_command_summary(command),
            ["total_score"] = score_input != null ? GetInt(score_input, "total_score") : GetInt(extra, "total_score"),
            ["score_input"] = ToDictionary(score_input),
        };
        if (extra != null)
        {
            foreach (Variant key in extra.Keys)
                summary[key] = extra[key];
        }
        return summary;
    }

    public static string format_skill_variant_label(SkillDef skill_def, CombatCastVariantDef cast_variant)
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

    private static BattleUnitState GetContextUnitState(GodotObject context)
    {
        if (context == null)
            return null;
        Variant unitState = context.Get("unit_state");
        return unitState.VariantType == Variant.Type.Object ? unitState.AsGodotObject() as BattleUnitState : null;
    }

    private static GDictionary ToDictionary(GodotObject value)
    {
        if (value == null || !value.HasMethod("to_dict"))
            return new GDictionary();
        Variant result = value.Call("to_dict");
        return result.VariantType == Variant.Type.Dictionary ? result.AsGodotDictionary() : new GDictionary();
    }

    private static int GetInt(GDictionary data, string key)
    {
        return Get(data, key, 0).AsInt32();
    }

    private static int GetInt(GodotObject value, string propertyName)
    {
        if (value == null)
            return 0;
        Variant rawValue = value.Get(propertyName);
        return rawValue.VariantType == Variant.Type.Int ? rawValue.AsInt32() : 0;
    }

    private static Variant Get(GDictionary data, string key, Variant fallback = default)
    {
        if (data != null && data.ContainsKey(key))
            return data[key];
        return fallback;
    }
}
