class_name AiBaselineDiff
extends RefCounted

const SCHEMA_VERSION := 1
const DEFAULT_TOLERANCE_PCT := 20.0
const NOISE_FLOOR_USEC := 30


static func summarize_stats(stats: Dictionary) -> Dictionary:
	var call_count := int(stats.get("call_count", 0))
	var total_usec := int(stats.get("total_usec", 0))
	var max_usec := int(stats.get("max_usec", 0))
	var samples_variant = stats.get("samples", PackedInt64Array())
	var samples: PackedInt64Array = samples_variant
	var summary := {
		"call_count": call_count,
		"total_usec": total_usec,
		"max_usec": max_usec,
		"avg_usec": 0,
		"p50_usec": 0,
		"p95_usec": 0,
	}
	if call_count <= 0 or samples.is_empty():
		return summary
	summary["avg_usec"] = int(total_usec / float(call_count))
	var sorted := samples.duplicate()
	sorted.sort()
	summary["p50_usec"] = sorted[int(sorted.size() * 0.50)]
	var p95_index := int(sorted.size() * 0.95)
	if p95_index >= sorted.size():
		p95_index = sorted.size() - 1
	summary["p95_usec"] = sorted[p95_index]
	return summary


static func merge_runs(per_run_stats: Array) -> Dictionary:
	# per_run_stats: Array of Dictionary (each is a single run's stats: {call_count, total_usec, max_usec, samples}).
	# Returns a merged stats dictionary by concatenating samples across runs.
	var merged := {
		"call_count": 0,
		"total_usec": 0,
		"max_usec": 0,
		"samples": PackedInt64Array(),
	}
	for run_stats in per_run_stats:
		merged["call_count"] = int(merged["call_count"]) + int(run_stats.get("call_count", 0))
		merged["total_usec"] = int(merged["total_usec"]) + int(run_stats.get("total_usec", 0))
		merged["max_usec"] = max(int(merged["max_usec"]), int(run_stats.get("max_usec", 0)))
		var run_samples_variant = run_stats.get("samples", PackedInt64Array())
		var run_samples: PackedInt64Array = run_samples_variant
		var merged_samples: PackedInt64Array = merged["samples"]
		merged_samples.append_array(run_samples)
		merged["samples"] = merged_samples
	return merged


static func build_baseline_doc(scenarios: Dictionary, git_commit: String) -> Dictionary:
	return {
		"schema_version": SCHEMA_VERSION,
		"godot_version": Engine.get_version_info().get("string", "unknown"),
		"generated_at_unix": int(Time.get_unix_time_from_system()),
		"git_commit": git_commit,
		"scenarios": scenarios,
	}


static func write_baseline(path: String, doc: Dictionary) -> bool:
	var dir_path := path.get_base_dir()
	if not DirAccess.dir_exists_absolute(dir_path):
		DirAccess.make_dir_recursive_absolute(dir_path)
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		return false
	file.store_string(JSON.stringify(doc, "\t", true))
	file.close()
	return true


static func read_baseline(path: String) -> Dictionary:
	if not FileAccess.file_exists(path):
		return {}
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		return {}
	var text := file.get_as_text()
	file.close()
	var parsed = JSON.parse_string(text)
	if parsed is Dictionary:
		return parsed
	return {}


static func compare(baseline: Dictionary, current: Dictionary, tolerance_pct: float) -> Array:
	# Returns Array[Dictionary]: each entry is one metric diff with status "ok"/"regression"/"missing".
	var diffs: Array = []
	var current_scenarios: Dictionary = current.get("scenarios", {})
	var baseline_scenarios: Dictionary = baseline.get("scenarios", {})
	for scenario_id in current_scenarios.keys():
		var current_scenario: Dictionary = current_scenarios[scenario_id]
		var baseline_scenario_variant = baseline_scenarios.get(scenario_id, null)
		if baseline_scenario_variant == null:
			diffs.append({
				"scenario_id": scenario_id,
				"layer": "(scenario)",
				"metric": "(any)",
				"status": "missing_in_baseline",
			})
			continue
		var baseline_scenario: Dictionary = baseline_scenario_variant
		var current_layers: Dictionary = current_scenario.get("layers", {})
		var baseline_layers: Dictionary = baseline_scenario.get("layers", {})
		for layer_name in current_layers.keys():
			var current_layer: Dictionary = current_layers[layer_name]
			var baseline_layer_variant = baseline_layers.get(layer_name, null)
			if baseline_layer_variant == null:
				diffs.append({
					"scenario_id": scenario_id,
					"layer": layer_name,
					"metric": "(any)",
					"status": "missing_in_baseline",
				})
				continue
			var baseline_layer: Dictionary = baseline_layer_variant
			for metric in ["avg_usec", "p50_usec", "p95_usec"]:
				var baseline_value := int(baseline_layer.get(metric, 0))
				var current_value := int(current_layer.get(metric, 0))
				var entry := {
					"scenario_id": scenario_id,
					"layer": layer_name,
					"metric": metric,
					"baseline_usec": baseline_value,
					"current_usec": current_value,
					"delta_pct": 0.0,
					"status": "ok",
				}
				if baseline_value <= 0:
					entry["status"] = "skipped_zero_baseline"
				elif baseline_value < NOISE_FLOOR_USEC:
					entry["status"] = "noise_floor"
				else:
					var delta_pct := (current_value - baseline_value) * 100.0 / float(baseline_value)
					entry["delta_pct"] = delta_pct
					if delta_pct > tolerance_pct:
						entry["status"] = "regression"
				diffs.append(entry)
	return diffs


static func format_diff_report(diffs: Array, tolerance_pct: float) -> String:
	var lines: Array[String] = []
	lines.append("[BASELINE_DIFF] tolerance=±%.1f%%" % tolerance_pct)
	for d in diffs:
		var status := String(d.get("status", "?"))
		var line := "  [%s] %s/%s/%s baseline=%dus current=%dus delta=%+.1f%%" % [
			status,
			String(d.get("scenario_id", "")),
			String(d.get("layer", "")),
			String(d.get("metric", "")),
			int(d.get("baseline_usec", 0)),
			int(d.get("current_usec", 0)),
			float(d.get("delta_pct", 0.0)),
		]
		lines.append(line)
	return "\n".join(lines)


static func count_regressions(diffs: Array) -> int:
	var n := 0
	for d in diffs:
		if String(d.get("status", "")) == "regression":
			n += 1
	return n
