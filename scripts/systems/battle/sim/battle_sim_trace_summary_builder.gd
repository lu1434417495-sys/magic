class_name BattleSimTraceSummaryBuilder
extends RefCounted

const DEFAULT_FOCUS_FACTION_ID := "player"
const DEFAULT_TOP_CANDIDATES_PER_ACTION := 2


func has_traces(report: Dictionary) -> bool:
	for entry in _collect_run_entries(report):
		var run_entry: Dictionary = entry.get("run", {})
		for trace_entry in run_entry.get("ai_turn_traces", []):
			if trace_entry is Dictionary:
				return true
	return false


func build(report: Dictionary, source_report_path: String = "", options: Dictionary = {}) -> Dictionary:
	var focus_faction_id := _as_string(options.get("focus_faction_id", DEFAULT_FOCUS_FACTION_ID))
	var top_candidate_limit := maxi(int(options.get("top_candidates_per_action", DEFAULT_TOP_CANDIDATES_PER_ACTION)), 0)
	var compact_runs: Array = []
	var trace_count := 0
	for entry in _collect_run_entries(report):
		var run_entry: Dictionary = entry.get("run", {})
		var compact_run := _build_compact_run_trace(
			run_entry,
			_as_string(entry.get("profile_id", "")),
			focus_faction_id,
			top_candidate_limit
		)
		trace_count += int(compact_run.get("trace_count", 0))
		compact_runs.append(compact_run)
	return {
		"source_report": source_report_path,
		"scenario": report.get("scenario", {}),
		"batch_id": report.get("batch_id", 0),
		"generated_at_unix": int(report.get("generated_at_unix", 0)),
		"profile_count": _count_profiles(report),
		"run_count": compact_runs.size(),
		"trace_count": trace_count,
		"elapsed_seconds": float(report.get("elapsed_seconds", 0.0)),
		"ended_count": int(report.get("ended_count", 0)),
		"avg_iterations": float(report.get("avg_iterations", 0.0)),
		"avg_timeline_steps": float(report.get("avg_timeline_steps", 0.0)),
		"win_rate": report.get("win_rate", {}),
		"comparisons": report.get("comparisons", []),
		"profile_summaries": _collect_profile_summaries(report),
		"global": report.get("global", {}),
		"player": report.get("player", {}),
		"hostile": report.get("hostile", {}),
		"trace_compaction": {
			"full_trace_embedded_in_source_report": true,
			"focus_faction_id": focus_faction_id,
			"focus_turns_keep_action_trace_summaries": true,
			"top_candidates_per_action_trace": top_candidate_limit,
		},
		"runs": compact_runs,
	}


func _collect_run_entries(report: Dictionary) -> Array:
	var entries: Array = []
	var profile_entries = report.get("profile_entries", [])
	if profile_entries is Array and not profile_entries.is_empty():
		for profile_entry in profile_entries:
			if profile_entry is not Dictionary:
				continue
			var profile: Dictionary = profile_entry.get("profile", {}) if profile_entry.get("profile", {}) is Dictionary else {}
			var profile_id := _as_string(profile.get("profile_id", ""))
			for run_entry in profile_entry.get("runs", []):
				if run_entry is Dictionary:
					entries.append({
						"profile_id": profile_id,
						"run": run_entry,
					})
		return entries
	for run_entry in report.get("runs", []):
		if run_entry is Dictionary:
			entries.append({
				"profile_id": _as_string(run_entry.get("profile_id", "")),
				"run": run_entry,
			})
	return entries


func _count_profiles(report: Dictionary) -> int:
	var profile_entries = report.get("profile_entries", [])
	if profile_entries is Array:
		return profile_entries.size()
	return 0


func _collect_profile_summaries(report: Dictionary) -> Array:
	var summaries: Array = []
	var profile_entries = report.get("profile_entries", [])
	if profile_entries is not Array:
		return summaries
	for profile_entry in profile_entries:
		if profile_entry is not Dictionary:
			continue
		summaries.append({
			"profile": profile_entry.get("profile", {}),
			"summary": profile_entry.get("summary", {}),
		})
	return summaries


func _build_compact_run_trace(
	run_entry: Dictionary,
	profile_id: String,
	focus_faction_id: String,
	top_candidate_limit: int
) -> Dictionary:
	var action_counts_by_faction: Dictionary = {}
	var command_counts_by_faction: Dictionary = {}
	var block_reasons_by_faction: Dictionary = {}
	var wait_counts_by_faction: Dictionary = {}
	var focus_turns: Array = []
	var focus_wait_turns: Array = []
	var trace_count := 0
	for trace_entry in run_entry.get("ai_turn_traces", []):
		if trace_entry is not Dictionary:
			continue
		trace_count += 1
		var faction_id := _as_string(trace_entry.get("faction_id", ""))
		var action_id := _as_string(trace_entry.get("action_id", ""))
		var command_summary := _summarize_trace_command(trace_entry.get("command", {}))
		var command_type := _as_string(command_summary.get("command_type", ""))
		_increment_nested_counter(action_counts_by_faction, faction_id, action_id)
		_increment_nested_counter(command_counts_by_faction, faction_id, command_type)
		if command_type == "wait":
			_increment_nested_counter(wait_counts_by_faction, faction_id, action_id)
		var action_traces := _summarize_action_traces(
			trace_entry.get("action_traces", []),
			faction_id,
			block_reasons_by_faction,
			top_candidate_limit
		)
		if faction_id != focus_faction_id:
			continue
		var turn_summary := {
			"turn_started_tu": int(trace_entry.get("turn_started_tu", -1)),
			"unit_id": _as_string(trace_entry.get("unit_id", "")),
			"unit_name": _as_string(trace_entry.get("unit_name", "")),
			"faction_id": faction_id,
			"brain_id": _as_string(trace_entry.get("brain_id", "")),
			"state_id": _as_string(trace_entry.get("state_id", "")),
			"action_id": action_id,
			"reason_text": _as_string(trace_entry.get("reason_text", "")),
			"command": command_summary,
			"score": _summarize_score_input(trace_entry.get("score_input", {})),
			"action_traces": action_traces,
		}
		focus_turns.append(turn_summary)
		if command_type == "wait":
			focus_wait_turns.append(turn_summary)
	return {
		"profile_id": profile_id,
		"run_index": int(run_entry.get("run_index", 0)),
		"seed": int(run_entry.get("seed", 0)),
		"battle_ended": bool(run_entry.get("battle_ended", false)),
		"winner_faction_id": _as_string(run_entry.get("winner_faction_id", "")),
		"final_tu": int(run_entry.get("final_tu", 0)),
		"iterations": int(run_entry.get("iterations", 0)),
		"timeline_steps": int(run_entry.get("timeline_steps", 0)),
		"trace_count": trace_count,
		"factions": run_entry.get("factions", run_entry.get("metrics", {}).get("factions", {}) if run_entry.get("metrics", {}) is Dictionary else {}),
		"units": run_entry.get("units", run_entry.get("metrics", {}).get("units", {}) if run_entry.get("metrics", {}) is Dictionary else {}),
		"action_counts_by_faction": action_counts_by_faction,
		"command_counts_by_faction": command_counts_by_faction,
		"wait_counts_by_faction": wait_counts_by_faction,
		"block_reasons_by_faction": block_reasons_by_faction,
		"focus_turns": focus_turns,
		"focus_wait_turns": focus_wait_turns,
	}


func _summarize_action_traces(
	action_traces: Variant,
	faction_id: String,
	block_reasons_by_faction: Dictionary,
	top_candidate_limit: int
) -> Array:
	var summaries: Array = []
	if action_traces is not Array:
		return summaries
	for action_trace in action_traces:
		if action_trace is not Dictionary:
			continue
		var block_reasons: Dictionary = action_trace.get("block_reasons", {}) if action_trace.get("block_reasons", {}) is Dictionary else {}
		for reason_key in block_reasons.keys():
			_increment_nested_counter(block_reasons_by_faction, faction_id, _as_string(reason_key), int(block_reasons.get(reason_key, 0)))
		summaries.append({
			"trace_id": _as_string(action_trace.get("trace_id", "")),
			"action_id": _as_string(action_trace.get("action_id", "")),
			"chosen": bool(action_trace.get("chosen", false)),
			"score_bucket_id": _as_string(action_trace.get("score_bucket_id", "")),
			"metadata": action_trace.get("metadata", {}),
			"block_reasons": block_reasons,
			"blocked_count": int(action_trace.get("blocked_count", 0)),
			"candidate_count": int(action_trace.get("candidate_count", 0)),
			"evaluation_count": int(action_trace.get("evaluation_count", 0)),
			"preview_reject_count": int(action_trace.get("preview_reject_count", 0)),
			"top_candidates": _summarize_top_candidates(action_trace.get("top_candidates", []), top_candidate_limit),
		})
	return summaries


func _summarize_top_candidates(candidates: Variant, limit: int) -> Array:
	var summaries: Array = []
	if candidates is not Array:
		return summaries
	for candidate in candidates:
		if candidate is not Dictionary:
			continue
		if summaries.size() >= limit:
			break
		var score_summary := _summarize_score_input(candidate.get("score_input", {}))
		var candidate_summary := {
			"label": _as_string(candidate.get("label", "")),
			"total_score": int(candidate.get("total_score", score_summary.get("total_score", 0))),
			"predicted_distance": int(candidate.get("predicted_distance", -1)) if candidate.has("predicted_distance") else -1,
			"command": _summarize_trace_command(candidate.get("command", {})),
			"score": score_summary,
		}
		_copy_optional_candidate_int(candidate_summary, candidate, "screening_bonus")
		_copy_optional_candidate_int(candidate_summary, candidate, "screening_penalty")
		_copy_optional_candidate_int(candidate_summary, candidate, "screening_path_cost_delta")
		_copy_optional_candidate_int(candidate_summary, candidate, "screening_base_path_cost")
		_copy_optional_candidate_int(candidate_summary, candidate, "screening_blocked_path_cost")
		_copy_optional_candidate_int(candidate_summary, candidate, "screening_current_bonus")
		_copy_optional_candidate_int(candidate_summary, candidate, "screening_candidate_bonus")
		_copy_optional_candidate_int(candidate_summary, candidate, "screening_uncapped_bonus")
		_copy_optional_candidate_string(candidate_summary, candidate, "screening_threat_unit_id")
		_copy_optional_candidate_string(candidate_summary, candidate, "screening_protected_unit_id")
		_copy_optional_candidate_bool(candidate_summary, candidate, "screening_on_shortest_path")
		_copy_optional_candidate_bool(candidate_summary, candidate, "screening_keeps_contact")
		_copy_optional_candidate_bool(candidate_summary, candidate, "screening_can_counterattack")
		_copy_optional_candidate_bool(candidate_summary, candidate, "screening_hard_block")
		_copy_optional_candidate_bool(candidate_summary, candidate, "screening_distance_band_capped")
		summaries.append(candidate_summary)
	return summaries


func _copy_optional_candidate_int(target: Dictionary, source: Dictionary, key: String) -> void:
	if source.has(key):
		target[key] = int(source.get(key, 0))


func _copy_optional_candidate_string(target: Dictionary, source: Dictionary, key: String) -> void:
	if source.has(key):
		target[key] = _as_string(source.get(key, ""))


func _copy_optional_candidate_bool(target: Dictionary, source: Dictionary, key: String) -> void:
	if source.has(key):
		target[key] = bool(source.get(key, false))


func _summarize_trace_command(command_value: Variant) -> Dictionary:
	if command_value is not Dictionary:
		return {}
	var command: Dictionary = command_value
	return {
		"command_type": _as_string(command.get("command_type", "")),
		"unit_id": _as_string(command.get("unit_id", "")),
		"skill_id": _as_string(command.get("skill_id", "")),
		"skill_variant_id": _as_string(command.get("skill_variant_id", "")),
		"target_unit_id": _as_string(command.get("target_unit_id", "")),
		"target_unit_ids": _stringify_array(command.get("target_unit_ids", [])),
		"target_coord": _as_string(command.get("target_coord", "")),
		"target_coords": _stringify_array(command.get("target_coords", [])),
	}


func _summarize_score_input(score_value: Variant) -> Dictionary:
	if score_value is not Dictionary:
		return {}
	var score: Dictionary = score_value
	return {
		"total_score": int(score.get("total_score", 0)),
		"score_bucket_id": _as_string(score.get("score_bucket_id", "")),
		"score_bucket_priority": int(score.get("score_bucket_priority", 0)),
		"command_type": _as_string(score.get("command_type", "")),
		"skill_id": _as_string(score.get("skill_id", "")),
		"target_count": int(score.get("target_count", 0)),
		"estimated_damage": int(score.get("estimated_damage", 0)),
		"estimated_hit_rate_percent": int(score.get("estimated_hit_rate_percent", 0)),
		"hit_payoff_score": int(score.get("hit_payoff_score", 0)),
		"position_objective_kind": _as_string(score.get("position_objective_kind", "")),
		"position_objective_score": int(score.get("position_objective_score", 0)),
		"resource_cost_score": int(score.get("resource_cost_score", 0)),
		"distance_to_primary_coord": int(score.get("distance_to_primary_coord", -1)),
		"ap_cost": int(score.get("ap_cost", 0)),
		"stamina_cost": int(score.get("stamina_cost", 0)),
		"mp_cost": int(score.get("mp_cost", 0)),
		"aura_cost": int(score.get("aura_cost", 0)),
		"move_cost": int(score.get("move_cost", 0)),
	}


func _increment_nested_counter(counters: Dictionary, outer_key: String, inner_key: String, amount: int = 1) -> void:
	if outer_key.is_empty() or inner_key.is_empty() or amount == 0:
		return
	var inner: Dictionary = counters.get(outer_key, {}) if counters.get(outer_key, {}) is Dictionary else {}
	inner[inner_key] = int(inner.get(inner_key, 0)) + amount
	counters[outer_key] = inner


func _stringify_array(value: Variant) -> Array[String]:
	var results: Array[String] = []
	if value is not Array:
		return results
	for entry in value:
		results.append(_as_string(entry))
	return results


func _as_string(value: Variant) -> String:
	if value == null:
		return ""
	return str(value)
