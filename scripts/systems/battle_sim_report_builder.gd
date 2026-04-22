class_name BattleSimReportBuilder
extends RefCounted


func build_profile_summary(profile, runs: Array) -> Dictionary:
	var wins_by_faction: Dictionary = {}
	var skill_usage_totals: Dictionary = {}
	var action_choice_counts: Dictionary = {}
	var faction_metric_totals: Dictionary = {}
	var total_final_tu := 0
	var total_iterations := 0
	for run_entry in runs:
		if run_entry is not Dictionary:
			continue
		var winner_faction := String(run_entry.get("winner_faction_id", ""))
		if not winner_faction.is_empty():
			wins_by_faction[winner_faction] = int(wins_by_faction.get(winner_faction, 0)) + 1
		total_final_tu += int(run_entry.get("final_tu", 0))
		total_iterations += int(run_entry.get("iterations", 0))
		_merge_skill_usage(skill_usage_totals, run_entry.get("metrics", {}))
		_merge_action_choices(action_choice_counts, run_entry.get("ai_turn_traces", []))
		_merge_faction_metric_totals(faction_metric_totals, run_entry.get("metrics", {}))
	var run_count := maxi(runs.size(), 1)
	return {
		"profile_id": String(profile.profile_id) if profile != null else "",
		"display_name": profile.display_name if profile != null else "",
		"run_count": runs.size(),
		"wins_by_faction": wins_by_faction,
		"win_rate_by_faction": _build_rate_dictionary(wins_by_faction, runs.size()),
		"average_final_tu": float(total_final_tu) / float(run_count),
		"average_iterations": float(total_iterations) / float(run_count),
		"skill_usage_totals": skill_usage_totals,
		"action_choice_counts": action_choice_counts,
		"faction_metric_totals": faction_metric_totals,
	}


func build_profile_comparisons(profile_entries: Array) -> Array[Dictionary]:
	var comparisons: Array[Dictionary] = []
	if profile_entries.size() <= 1:
		return comparisons
	var baseline_entry := profile_entries[0] as Dictionary
	if baseline_entry == null:
		return comparisons
	var baseline_summary := baseline_entry.get("summary", {}) as Dictionary
	for entry_index in range(1, profile_entries.size()):
		var candidate_entry := profile_entries[entry_index] as Dictionary
		if candidate_entry == null:
			continue
		var candidate_summary := candidate_entry.get("summary", {}) as Dictionary
		comparisons.append({
			"baseline_profile_id": String(baseline_summary.get("profile_id", "")),
			"candidate_profile_id": String(candidate_summary.get("profile_id", "")),
			"average_final_tu_delta": float(candidate_summary.get("average_final_tu", 0.0)) - float(baseline_summary.get("average_final_tu", 0.0)),
			"average_iterations_delta": float(candidate_summary.get("average_iterations", 0.0)) - float(baseline_summary.get("average_iterations", 0.0)),
			"win_rate_delta": _diff_number_dictionary(
				baseline_summary.get("win_rate_by_faction", {}),
				candidate_summary.get("win_rate_by_faction", {})
			),
			"skill_usage_delta": _diff_int_dictionary(
				baseline_summary.get("skill_usage_totals", {}),
				candidate_summary.get("skill_usage_totals", {})
			),
			"action_choice_delta": _diff_int_dictionary(
				baseline_summary.get("action_choice_counts", {}),
				candidate_summary.get("action_choice_counts", {})
			),
		})
	return comparisons


func _merge_skill_usage(skill_usage_totals: Dictionary, metrics: Dictionary) -> void:
	var units = metrics.get("units", {})
	if units is not Dictionary:
		return
	for unit_entry in units.values():
		if unit_entry is not Dictionary:
			continue
		var skill_counts = unit_entry.get("skill_success_counts", {})
		if skill_counts is not Dictionary:
			continue
		for skill_key in skill_counts.keys():
			var normalized_key := String(skill_key)
			skill_usage_totals[normalized_key] = int(skill_usage_totals.get(normalized_key, 0)) + int(skill_counts.get(skill_key, 0))


func _merge_action_choices(action_choice_counts: Dictionary, ai_turn_traces: Variant) -> void:
	if ai_turn_traces is not Array:
		return
	for trace_entry in ai_turn_traces:
		if trace_entry is not Dictionary:
			continue
		var action_id := String(trace_entry.get("action_id", ""))
		if action_id.is_empty():
			continue
		action_choice_counts[action_id] = int(action_choice_counts.get(action_id, 0)) + 1


func _merge_faction_metric_totals(faction_metric_totals: Dictionary, metrics: Dictionary) -> void:
	var factions = metrics.get("factions", {})
	if factions is not Dictionary:
		return
	for faction_key in factions.keys():
		var source_entry = factions.get(faction_key, {})
		if source_entry is not Dictionary:
			continue
		var normalized_key := String(faction_key)
		var target_entry: Dictionary = faction_metric_totals.get(normalized_key, {}).duplicate(true)
		for metric_key in [
			"unit_count",
			"turn_count",
			"successful_skill_count",
			"total_damage_done",
			"total_healing_done",
			"total_damage_taken",
			"total_healing_received",
			"kill_count",
			"death_count",
		]:
			target_entry[metric_key] = int(target_entry.get(metric_key, 0)) + int(source_entry.get(metric_key, 0))
		faction_metric_totals[normalized_key] = target_entry


func _build_rate_dictionary(counts: Dictionary, total_count: int) -> Dictionary:
	var rates: Dictionary = {}
	if total_count <= 0:
		return rates
	for count_key in counts.keys():
		rates[String(count_key)] = float(counts.get(count_key, 0)) / float(total_count)
	return rates


func _diff_int_dictionary(baseline: Variant, candidate: Variant) -> Dictionary:
	var diff: Dictionary = {}
	var keys: Dictionary = {}
	if baseline is Dictionary:
		for key in baseline.keys():
			keys[String(key)] = true
	if candidate is Dictionary:
		for key in candidate.keys():
			keys[String(key)] = true
	for key in keys.keys():
		diff[key] = int((candidate as Dictionary).get(key, 0) if candidate is Dictionary else 0) - int((baseline as Dictionary).get(key, 0) if baseline is Dictionary else 0)
	return diff


func _diff_number_dictionary(baseline: Variant, candidate: Variant) -> Dictionary:
	var diff: Dictionary = {}
	var keys: Dictionary = {}
	if baseline is Dictionary:
		for key in baseline.keys():
			keys[String(key)] = true
	if candidate is Dictionary:
		for key in candidate.keys():
			keys[String(key)] = true
	for key in keys.keys():
		diff[key] = float((candidate as Dictionary).get(key, 0.0) if candidate is Dictionary else 0.0) - float((baseline as Dictionary).get(key, 0.0) if baseline is Dictionary else 0.0)
	return diff
