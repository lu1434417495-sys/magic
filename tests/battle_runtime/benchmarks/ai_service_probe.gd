extends "res://scripts/systems/battle/ai/battle_ai_service.gd"

const AiTraceRecorderScript = preload("res://scripts/dev_tools/ai_trace_recorder.gd")

var stats_choose: Dictionary = _new_stats()
var stats_skill_input: Dictionary = _new_stats()
var stats_action_input: Dictionary = _new_stats()


static func _new_stats() -> Dictionary:
	return {
		"call_count": 0,
		"total_usec": 0,
		"max_usec": 0,
		"samples": PackedInt64Array(),
	}


func choose_command(context):
	AiTraceRecorderScript.enter(&"choose_command")
	var t := Time.get_ticks_usec()
	var result = super.choose_command(context)
	_record(stats_choose, Time.get_ticks_usec() - t)
	AiTraceRecorderScript.exit(&"choose_command")
	return result


func build_skill_score_input(context, skill_def, command, preview, effect_defs: Array = [], metadata: Dictionary = {}):
	AiTraceRecorderScript.enter(&"build_skill_score_input")
	var t := Time.get_ticks_usec()
	var result = super.build_skill_score_input(context, skill_def, command, preview, effect_defs, metadata)
	_record(stats_skill_input, Time.get_ticks_usec() - t)
	AiTraceRecorderScript.exit(&"build_skill_score_input")
	return result


func build_action_score_input(context, action_kind: StringName, action_label: String, score_bucket_id: StringName, command, preview, metadata: Dictionary = {}):
	AiTraceRecorderScript.enter(&"build_action_score_input")
	var t := Time.get_ticks_usec()
	var result = super.build_action_score_input(context, action_kind, action_label, score_bucket_id, command, preview, metadata)
	_record(stats_action_input, Time.get_ticks_usec() - t)
	AiTraceRecorderScript.exit(&"build_action_score_input")
	return result


func _record(s: Dictionary, dt: int) -> void:
	s["call_count"] = int(s["call_count"]) + 1
	s["total_usec"] = int(s["total_usec"]) + dt
	if dt > int(s["max_usec"]):
		s["max_usec"] = dt
	var samples: PackedInt64Array = s["samples"]
	samples.append(dt)
	s["samples"] = samples


func reset_stats() -> void:
	stats_choose = _new_stats()
	stats_skill_input = _new_stats()
	stats_action_input = _new_stats()
