using Godot;
using GCandidateArray = Godot.Collections.Array<AiCandidateSummary>;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class AiActionTrace : RefCounted
{
	public StringName trace_id = "";
	public string action_id = "";
	public string score_bucket_id = "";
	public GDictionary metadata = new();
	public int evaluation_count = 0;
	public int blocked_count = 0;
	public int preview_reject_count = 0;
	public int candidate_count = 0;
	public GDictionary block_reasons = new();
	public GCandidateArray top_candidates = new();
	public bool chosen = false;

	public string best_reason_text = "";
	public AiCommandSummary best_command;
	public GDictionary best_score_input = new();
	public string chosen_reason_text = "";
	public GDictionary chosen_command = new();
	public GDictionary chosen_score_input = new();
	public GDictionary candidate_trace_counters = new();

	public bool gate_rejected = false;
	public string gate_rejection_reason = "";

	public AiActionTrace()
	{
		best_command = new AiCommandSummary();
	}

	public AiActionTrace(
		StringName p_trace_id,
		string p_action_id = "",
		string p_score_bucket_id = "",
		GDictionary p_metadata = null
	)
	{
		trace_id = p_trace_id;
		action_id = p_action_id;
		score_bucket_id = p_score_bucket_id;
		metadata = p_metadata != null ? p_metadata.Duplicate(true) : new GDictionary();
		best_command = new AiCommandSummary();
	}

	public bool is_empty()
	{
		return trace_id == (StringName)"";
	}

	public GDictionary to_dict()
	{
		var top_candidate_dicts = new Godot.Collections.Array<GDictionary>();
		foreach (var candidate in top_candidates)
		{
			if (candidate != null)
				top_candidate_dicts.Add(candidate.to_dict());
		}

		return new GDictionary
		{
			["trace_id"] = trace_id.ToString(),
			["action_id"] = action_id,
			["score_bucket_id"] = score_bucket_id,
			["metadata"] = metadata.Duplicate(true),
			["evaluation_count"] = evaluation_count,
			["blocked_count"] = blocked_count,
			["preview_reject_count"] = preview_reject_count,
			["candidate_count"] = candidate_count,
			["block_reasons"] = block_reasons.Duplicate(true),
			["top_candidates"] = top_candidate_dicts,
			["chosen"] = chosen,
			["best_reason_text"] = best_reason_text,
			["best_command"] = best_command != null ? best_command.to_dict() : new GDictionary(),
			["best_score_input"] = best_score_input.Duplicate(true),
			["chosen_reason_text"] = chosen_reason_text,
			["chosen_command"] = chosen_command.Duplicate(true),
			["chosen_score_input"] = chosen_score_input.Duplicate(true),
			["candidate_trace_counters"] = candidate_trace_counters.Duplicate(true),
			["gate_rejected"] = gate_rejected,
			["gate_rejection_reason"] = gate_rejection_reason,
		};
	}
}
