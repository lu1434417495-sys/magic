class_name BattleAttackRollModifierSpec
extends RefCounted

const BATTLE_ATTACK_ROLL_MODIFIER_SPEC_SCRIPT = preload("res://scripts/systems/battle/core/battle_attack_roll_modifier_spec.gd")

var source_domain: StringName = &""
var source_id: StringName = &""
var source_instance_id: String = ""
var label: String = ""
var modifier_delta: int = 0
var stack_key: StringName = &""
var stack_mode: StringName = &"add"
var roll_kind_filter: StringName = &""
var endpoint_mode: StringName = &"either"
var distance_min_exclusive: int = -1
var distance_max_inclusive: int = -1
var target_team_filter: StringName = &"any"
var footprint_mode: StringName = &"any_cell"
var applies_to: StringName = &"attack_roll"


func to_dict(effective_modifier_delta: int = modifier_delta) -> Dictionary:
	return {
		"source_domain": String(source_domain),
		"source_id": String(source_id),
		"source_instance_id": source_instance_id,
		"label": label,
		"modifier_delta": modifier_delta,
		"effective_modifier_delta": effective_modifier_delta,
		"stack_key": String(stack_key),
		"stack_mode": String(stack_mode),
		"roll_kind_filter": String(roll_kind_filter),
		"endpoint_mode": String(endpoint_mode),
		"distance_min_exclusive": distance_min_exclusive,
		"distance_max_inclusive": distance_max_inclusive,
		"target_team_filter": String(target_team_filter),
		"footprint_mode": String(footprint_mode),
		"applies_to": String(applies_to),
	}


static func from_dict(data: Variant):
	if data is not Dictionary:
		return null
	var payload: Dictionary = data
	if not _has_exact_schema(payload):
		return null
	if not _is_string_like(payload.get("source_domain")):
		return null
	if not _is_string_like(payload.get("source_id")):
		return null
	if not _is_string_like(payload.get("source_instance_id")):
		return null
	if payload.get("label") is not String:
		return null
	if payload.get("modifier_delta") is not int:
		return null
	if not _is_string_like(payload.get("stack_key")):
		return null
	if not _is_string_like(payload.get("stack_mode")):
		return null
	if not _is_string_like(payload.get("roll_kind_filter")):
		return null
	if not _is_string_like(payload.get("endpoint_mode")):
		return null
	if payload.get("distance_min_exclusive") is not int:
		return null
	if payload.get("distance_max_inclusive") is not int:
		return null
	if not _is_string_like(payload.get("target_team_filter")):
		return null
	if not _is_string_like(payload.get("footprint_mode")):
		return null
	if not _is_string_like(payload.get("applies_to")):
		return null
	var spec := BATTLE_ATTACK_ROLL_MODIFIER_SPEC_SCRIPT.new()
	spec.source_domain = ProgressionDataUtils.to_string_name(payload.get("source_domain"))
	spec.source_id = ProgressionDataUtils.to_string_name(payload.get("source_id"))
	spec.source_instance_id = String(payload.get("source_instance_id"))
	spec.label = String(payload.get("label"))
	spec.modifier_delta = int(payload.get("modifier_delta"))
	spec.stack_key = ProgressionDataUtils.to_string_name(payload.get("stack_key"))
	spec.stack_mode = ProgressionDataUtils.to_string_name(payload.get("stack_mode"))
	spec.roll_kind_filter = ProgressionDataUtils.to_string_name(payload.get("roll_kind_filter"))
	spec.endpoint_mode = ProgressionDataUtils.to_string_name(payload.get("endpoint_mode"))
	spec.distance_min_exclusive = int(payload.get("distance_min_exclusive"))
	spec.distance_max_inclusive = int(payload.get("distance_max_inclusive"))
	spec.target_team_filter = ProgressionDataUtils.to_string_name(payload.get("target_team_filter"))
	spec.footprint_mode = ProgressionDataUtils.to_string_name(payload.get("footprint_mode"))
	spec.applies_to = ProgressionDataUtils.to_string_name(payload.get("applies_to"))
	return spec


static func from_partial_dict(data: Variant):
	if data is not Dictionary:
		return null
	var payload: Dictionary = data
	var spec := BATTLE_ATTACK_ROLL_MODIFIER_SPEC_SCRIPT.new()
	spec.source_domain = ProgressionDataUtils.to_string_name(payload.get("source_domain", &""))
	spec.source_id = ProgressionDataUtils.to_string_name(payload.get("source_id", &""))
	spec.source_instance_id = String(payload.get("source_instance_id", ""))
	spec.label = String(payload.get("label", ""))
	spec.modifier_delta = int(payload.get("modifier_delta", 0))
	spec.stack_key = ProgressionDataUtils.to_string_name(payload.get("stack_key", &""))
	spec.stack_mode = ProgressionDataUtils.to_string_name(payload.get("stack_mode", &"add"))
	spec.roll_kind_filter = ProgressionDataUtils.to_string_name(payload.get("roll_kind_filter", &""))
	spec.endpoint_mode = ProgressionDataUtils.to_string_name(payload.get("endpoint_mode", &"either"))
	spec.distance_min_exclusive = int(payload.get("distance_min_exclusive", -1))
	spec.distance_max_inclusive = int(payload.get("distance_max_inclusive", -1))
	spec.target_team_filter = ProgressionDataUtils.to_string_name(payload.get("target_team_filter", &"any"))
	spec.footprint_mode = ProgressionDataUtils.to_string_name(payload.get("footprint_mode", &"any_cell"))
	spec.applies_to = ProgressionDataUtils.to_string_name(payload.get("applies_to", &"attack_roll"))
	return spec


static func _has_exact_schema(payload: Dictionary) -> bool:
	var keys := [
		"source_domain",
		"source_id",
		"source_instance_id",
		"label",
		"modifier_delta",
		"stack_key",
		"stack_mode",
		"roll_kind_filter",
		"endpoint_mode",
		"distance_min_exclusive",
		"distance_max_inclusive",
		"target_team_filter",
		"footprint_mode",
		"applies_to",
	]
	if payload.size() != keys.size():
		return false
	for key in keys:
		if not payload.has(key):
			return false
	return true


static func _is_string_like(value: Variant) -> bool:
	return value is String or value is StringName
