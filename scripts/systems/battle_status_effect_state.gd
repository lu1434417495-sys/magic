class_name BattleStatusEffectState
extends RefCounted

const SCRIPT = preload("res://scripts/systems/battle_status_effect_state.gd")

var status_id: StringName = &""
var source_unit_id: StringName = &""
var power := 0
var params: Dictionary = {}
var stacks := 0
var duration := -1


func is_empty() -> bool:
	return status_id == &""


func has_duration() -> bool:
	return duration >= 0


func duplicate_state() -> BattleStatusEffectState:
	return SCRIPT.from_dict(to_dict())


func to_dict() -> Dictionary:
	var payload := {
		"status_id": String(status_id),
		"source_unit_id": String(source_unit_id),
		"power": power,
		"params": params.duplicate(true),
		"stacks": stacks,
	}
	if has_duration():
		payload["duration"] = duration
	return payload


static func from_dict(data: Variant, fallback_status_id: StringName = &"") -> BattleStatusEffectState:
	if data is BattleStatusEffectState:
		return (data as BattleStatusEffectState).duplicate_state()

	var effect := SCRIPT.new()
	if data is not Dictionary:
		effect.status_id = ProgressionDataUtils.to_string_name(fallback_status_id)
		return effect

	effect.status_id = ProgressionDataUtils.to_string_name(data.get("status_id", fallback_status_id))
	effect.source_unit_id = ProgressionDataUtils.to_string_name(data.get("source_unit_id", ""))
	effect.power = int(data.get("power", 0))
	effect.params = data.get("params", {}).duplicate(true) if data.get("params", {}) is Dictionary else {}
	effect.stacks = maxi(int(data.get("stacks", 1)), 1)
	effect.duration = int(data.get("duration", -1)) if data.has("duration") else -1
	return effect
