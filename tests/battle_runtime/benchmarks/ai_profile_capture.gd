## Shared capture helper for AI function-level hotspot profiling.
##
## Benchmark runners install the probe services, then use this helper to attach a
## fresh AiTraceRecorder per measured run and write the common reports.
class_name AiProfileCapture
extends RefCounted

const AiTraceRecorderScript = preload("res://scripts/dev_tools/ai_trace_recorder.gd")
const AiHotspotsFormatterScript = preload("res://tests/battle_runtime/benchmarks/ai_hotspots_formatter.gd")

var scenario_id := ""
var output_dir := "res://tests/battle_runtime/benchmarks/profiles/"
var top_n := 20
var sort_by := "self_usec"
var name_filter := ""
var dump_trace_json := false
var git_commit := "unknown"
var file_prefix := "ai_profile"

var aggregate_stats: Dictionary = {}
var measured_runs := 0
var measured_ai_turns := 0
var balanced := true
var truncated := false
var trace_events_sample: Array = []
var last_report: Dictionary = {}


func setup(
	p_scenario_id: String,
	p_output_dir: String,
	p_top_n: int,
	p_sort_by: String,
	p_name_filter: String = "",
	p_dump_trace_json: bool = false,
	p_git_commit: String = "unknown",
	p_file_prefix: String = "ai_profile"
) -> void:
	scenario_id = p_scenario_id
	output_dir = p_output_dir
	if output_dir.is_empty():
		output_dir = "res://tests/battle_runtime/benchmarks/profiles/"
	if not output_dir.ends_with("/"):
		output_dir += "/"
	top_n = maxi(p_top_n, 1)
	sort_by = p_sort_by
	name_filter = p_name_filter
	dump_trace_json = p_dump_trace_json
	git_commit = p_git_commit
	file_prefix = p_file_prefix
	aggregate_stats.clear()
	measured_runs = 0
	measured_ai_turns = 0
	balanced = true
	truncated = false
	trace_events_sample.clear()
	last_report.clear()
	AiTraceRecorderScript.instance = null


func begin_run(measured: bool = true):
	AiTraceRecorderScript.instance = null
	if not measured:
		return null
	var recorder = AiTraceRecorderScript.new()
	if recorder.has_method("set_event_capture_enabled"):
		recorder.set_event_capture_enabled(dump_trace_json)
	AiTraceRecorderScript.instance = recorder
	return recorder


func end_run(recorder, ai_turns: int = 0) -> Dictionary:
	AiTraceRecorderScript.instance = null
	if recorder == null:
		return {}
	_merge_stats(aggregate_stats, recorder.get_func_stats())
	measured_runs += 1
	measured_ai_turns += maxi(ai_turns, 0)
	if not recorder.assert_balanced():
		balanced = false
	if recorder.is_truncated():
		truncated = true
	if trace_events_sample.is_empty() and dump_trace_json:
		trace_events_sample = recorder.get_events()
	return build_summary()


func write_reports() -> Dictionary:
	var timestamp := _format_timestamp()
	var basename := "%s_%s_%s" % [file_prefix, scenario_id, timestamp]
	var header := format_header()
	var body := format_body()
	var hotspots_path := output_dir + basename + ".hotspots.txt"
	var csv_path := output_dir + basename + ".functions.csv"
	var ok_txt := AiHotspotsFormatterScript.write_text_report(hotspots_path, header, body)
	var ok_csv := AiHotspotsFormatterScript.write_csv(csv_path, aggregate_stats)
	var trace_path := ""
	var ok_trace := false
	if dump_trace_json and not trace_events_sample.is_empty():
		trace_path = output_dir + basename + ".trace.json"
		ok_trace = _write_trace_json(trace_path)
	last_report = build_summary()
	last_report.merge({
		"header": header,
		"body": body,
		"hotspots_path": hotspots_path,
		"functions_csv_path": csv_path,
		"trace_path": trace_path,
		"wrote_hotspots": ok_txt,
		"wrote_functions_csv": ok_csv,
		"wrote_trace": ok_trace,
	}, true)
	return last_report


func format_header() -> String:
	return AiHotspotsFormatterScript.format_header(
		scenario_id,
		measured_ai_turns,
		total_self_usec(),
		sort_by,
		Engine.get_version_info().get("string", "unknown"),
		git_commit
	)


func format_body() -> String:
	return AiHotspotsFormatterScript.format_top_n(aggregate_stats, sort_by, top_n, name_filter)


func total_self_usec() -> int:
	return AiHotspotsFormatterScript.total_self_usec(aggregate_stats)


func build_summary() -> Dictionary:
	return {
		"enabled": true,
		"scenario": scenario_id,
		"measured_runs": measured_runs,
		"measured_ai_turns": measured_ai_turns,
		"total_self_usec": total_self_usec(),
		"sort": sort_by,
		"top_n": top_n,
		"filter": name_filter,
		"trace_json": dump_trace_json,
		"balanced": balanced,
		"truncated": truncated,
		"git_commit": git_commit,
	}


static func resolve_git_commit() -> String:
	var head := FileAccess.open("res://.git/HEAD", FileAccess.READ)
	if head == null:
		return "unknown"
	var line := head.get_as_text().strip_edges()
	head.close()
	if line.begins_with("ref: "):
		var ref_path := "res://.git/" + line.substr(5).strip_edges()
		var ref_file := FileAccess.open(ref_path, FileAccess.READ)
		if ref_file == null:
			return "unknown"
		var sha := ref_file.get_as_text().strip_edges()
		ref_file.close()
		if sha.length() >= 7:
			return sha.substr(0, 7)
		return sha
	if line.length() >= 7:
		return line.substr(0, 7)
	return "unknown"


func _merge_stats(target: Dictionary, source: Dictionary) -> void:
	for name_variant in source.keys():
		var src: Dictionary = source[name_variant]
		var dst: Dictionary
		if target.has(name_variant):
			dst = target[name_variant]
		else:
			dst = {"ncalls": 0, "self_usec": 0, "total_usec": 0, "max_usec": 0}
		dst["ncalls"] = int(dst["ncalls"]) + int(src.get("ncalls", 0))
		dst["self_usec"] = int(dst["self_usec"]) + int(src.get("self_usec", 0))
		dst["total_usec"] = int(dst["total_usec"]) + int(src.get("total_usec", 0))
		dst["max_usec"] = max(int(dst["max_usec"]), int(src.get("max_usec", 0)))
		target[name_variant] = dst


func _write_trace_json(path: String) -> bool:
	var trace_doc := {
		"traceEvents": trace_events_sample,
		"displayTimeUnit": "us",
		"metadata": {
			"scenario": scenario_id,
			"godot_version": Engine.get_version_info().get("string", ""),
			"git_commit": git_commit,
		},
	}
	var dir_part := path.get_base_dir()
	if not DirAccess.dir_exists_absolute(dir_part):
		DirAccess.make_dir_recursive_absolute(dir_part)
	var fh := FileAccess.open(path, FileAccess.WRITE)
	if fh == null:
		return false
	fh.store_string(JSON.stringify(trace_doc))
	fh.close()
	return true


func _format_timestamp() -> String:
	var t := Time.get_datetime_dict_from_system()
	return "%04d%02d%02d_%02d%02d%02d" % [
		int(t["year"]), int(t["month"]), int(t["day"]),
		int(t["hour"]), int(t["minute"]), int(t["second"]),
	]
