class_name BattleCommonSkillOutcome
extends RefCounted

var source_unit_id: StringName = &""
var skill_id: StringName = &""
var total_damage := 0
var total_healing := 0
var defeated_unit_ids: Array[StringName] = []
var changed_unit_ids: Array[StringName] = []
var changed_coords: Array[Vector2i] = []
var log_lines: Array[String] = []
var report_entries: Array[Dictionary] = []
var status_effect_ids_by_unit_id: Dictionary = {}


func add_changed_unit_id(unit_id: StringName) -> void:
	if unit_id == &"" or changed_unit_ids.has(unit_id):
		return
	changed_unit_ids.append(unit_id)


func add_changed_coord(coord: Vector2i) -> void:
	if changed_coords.has(coord):
		return
	changed_coords.append(coord)


func add_defeated_unit_id(unit_id: StringName) -> void:
	if unit_id == &"" or defeated_unit_ids.has(unit_id):
		return
	defeated_unit_ids.append(unit_id)


func add_status_effect_ids(unit_id: StringName, status_effect_ids: Array[StringName]) -> void:
	if unit_id == &"" or status_effect_ids.is_empty():
		return
	var existing: Array[StringName] = []
	var existing_value = status_effect_ids_by_unit_id.get(unit_id, [])
	if existing_value is Array:
		for status_variant in existing_value:
			var status_id := ProgressionDataUtils.to_string_name(status_variant)
			if status_id != &"" and not existing.has(status_id):
				existing.append(status_id)
	for status_id in status_effect_ids:
		if status_id != &"" and not existing.has(status_id):
			existing.append(status_id)
	status_effect_ids_by_unit_id[unit_id] = existing
