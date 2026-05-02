class_name BattleStatusEffectState
extends RefCounted

const SCRIPT = preload("res://scripts/systems/battle/core/battle_status_effect_state.gd")

const REQUIRED_SCHEMA_FIELDS: Array[String] = [
	"status_id",
	"source_unit_id",
	"power",
	"params",
	"stacks",
]
const OPTIONAL_SCHEMA_FIELDS: Array[String] = [
	"duration",
	"tick_interval_tu",
	"next_tick_at_tu",
	"skip_next_turn_end_decay",
]

var status_id: StringName = &""
var source_unit_id: StringName = &""
var power := 0
var params: Dictionary = {}
var stacks := 0
var duration := -1
var tick_interval_tu := 0
var next_tick_at_tu := 0
var skip_next_turn_end_decay := false


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
	if tick_interval_tu > 0:
		payload["tick_interval_tu"] = tick_interval_tu
	if next_tick_at_tu > 0:
		payload["next_tick_at_tu"] = next_tick_at_tu
	if skip_next_turn_end_decay:
		payload["skip_next_turn_end_decay"] = true
	return payload


static func from_dict(data: Variant) -> BattleStatusEffectState:
	if data is not Dictionary:
		return null
	var effect_dict := data as Dictionary
	if not _has_current_schema_fields(effect_dict):
		return null

	var raw_status_id: Variant = effect_dict["status_id"]
	if not _is_non_empty_string_like(raw_status_id):
		return null
	var raw_source_unit_id: Variant = effect_dict["source_unit_id"]
	if not _is_string_like(raw_source_unit_id):
		return null
	var raw_power: Variant = effect_dict["power"]
	if raw_power is not int:
		return null
	var raw_params: Variant = effect_dict["params"]
	if raw_params is not Dictionary:
		return null
	var raw_stacks: Variant = effect_dict["stacks"]
	if raw_stacks is not int or raw_stacks < 0:
		return null
	var duration_value := -1
	if effect_dict.has("duration"):
		var raw_duration: Variant = effect_dict["duration"]
		if raw_duration is not int or raw_duration < 0:
			return null
		duration_value = raw_duration
	var tick_interval_value := 0
	if effect_dict.has("tick_interval_tu"):
		var raw_tick_interval: Variant = effect_dict["tick_interval_tu"]
		if raw_tick_interval is not int or raw_tick_interval <= 0:
			return null
		tick_interval_value = raw_tick_interval
	var next_tick_at_value := 0
	if effect_dict.has("next_tick_at_tu"):
		var raw_next_tick_at: Variant = effect_dict["next_tick_at_tu"]
		if raw_next_tick_at is not int or raw_next_tick_at <= 0:
			return null
		next_tick_at_value = raw_next_tick_at
	var skip_decay_value := false
	if effect_dict.has("skip_next_turn_end_decay"):
		var raw_skip_decay: Variant = effect_dict["skip_next_turn_end_decay"]
		if raw_skip_decay is not bool or not raw_skip_decay:
			return null
		skip_decay_value = true

	var effect := SCRIPT.new()
	effect.status_id = StringName(raw_status_id)
	effect.source_unit_id = StringName(raw_source_unit_id)
	effect.power = raw_power
	effect.params = raw_params.duplicate(true)
	effect.stacks = raw_stacks
	effect.duration = duration_value
	effect.tick_interval_tu = tick_interval_value
	effect.next_tick_at_tu = next_tick_at_value
	effect.skip_next_turn_end_decay = skip_decay_value
	return effect


static func _has_current_schema_fields(effect_dict: Dictionary) -> bool:
	for field in REQUIRED_SCHEMA_FIELDS:
		if not effect_dict.has(field):
			return false
	for key_variant in effect_dict.keys():
		if key_variant is not String:
			return false
		if not REQUIRED_SCHEMA_FIELDS.has(key_variant) and not OPTIONAL_SCHEMA_FIELDS.has(key_variant):
			return false
	return true


static func _is_string_like(value: Variant) -> bool:
	return value is String or value is StringName


static func _is_non_empty_string_like(value: Variant) -> bool:
	if not _is_string_like(value):
		return false
	return not String(value).is_empty()
