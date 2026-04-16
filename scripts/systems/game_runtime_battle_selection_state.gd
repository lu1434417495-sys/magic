class_name GameRuntimeBattleSelectionState
extends RefCounted

var battle_selected_coord: Vector2i = Vector2i(-1, -1)
var selected_skill_id: StringName = &""
var selected_skill_variant_id: StringName = &""
var queued_target_coords: Array[Vector2i] = []
var queued_target_unit_ids: Array[StringName] = []
var last_manual_unit_id: StringName = &""


func clear_targets() -> void:
	queued_target_coords.clear()
	queued_target_unit_ids.clear()


func clear_skill_selection(reset_last_manual: bool = false) -> void:
	selected_skill_id = &""
	selected_skill_variant_id = &""
	clear_targets()
	if reset_last_manual:
		last_manual_unit_id = &""


func reset_for_battle_end() -> void:
	battle_selected_coord = Vector2i(-1, -1)
	clear_skill_selection(true)
