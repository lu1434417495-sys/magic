extends "res://scripts/systems/battle/ai/battle_ai_action_assembler.gd"

const AiTraceRecorderScript = preload("res://scripts/dev_tools/ai_trace_recorder.gd")

var stats_assemble: Dictionary = _new_stats()


static func _new_stats() -> Dictionary:
	return {
		"call_count": 0,
		"total_usec": 0,
		"max_usec": 0,
		"samples": PackedInt64Array(),
	}


func build_unit_action_plan(unit_state, brain, skill_defs: Dictionary) -> Dictionary:
	AiTraceRecorderScript.enter(&"build_unit_action_plan")
	var t := Time.get_ticks_usec()
	var result = super.build_unit_action_plan(unit_state, brain, skill_defs)
	_record(stats_assemble, Time.get_ticks_usec() - t)
	AiTraceRecorderScript.exit(&"build_unit_action_plan")
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
	stats_assemble = _new_stats()
