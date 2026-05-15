class_name BattleSimOverrideApplier
extends RefCounted

const BATTLE_AI_SCORE_PROFILE_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_score_profile.gd")


func apply_profile(skill_defs: Dictionary, enemy_ai_brains: Dictionary, profile) -> Dictionary:
	var cloned_skill_defs := _duplicate_resource_dict(skill_defs)
	var cloned_enemy_ai_brains := _duplicate_resource_dict(enemy_ai_brains)
	var ai_score_profile = profile.ai_score_profile.duplicate(true) if profile != null and profile.ai_score_profile != null else BATTLE_AI_SCORE_PROFILE_SCRIPT.new()
	var errors: Array[String] = []
	if profile != null:
		for patch_entry in profile.override_patches:
			if patch_entry is not Dictionary:
				errors.append("Battle sim profile %s contains a non-Dictionary override patch." % String(profile.profile_id))
				continue
			errors.append_array(_apply_patch_entry(cloned_skill_defs, cloned_enemy_ai_brains, ai_score_profile, patch_entry))
	for error in errors:
		push_error(error)
	return {
		"skill_defs": cloned_skill_defs,
		"enemy_ai_brains": cloned_enemy_ai_brains,
		"ai_score_profile": ai_score_profile,
		"errors": errors,
	}


func _duplicate_resource_dict(source: Dictionary) -> Dictionary:
	var duplicated: Dictionary = {}
	for key in source.keys():
		var value = source.get(key)
		duplicated[key] = value.duplicate(true) if value != null and value is Resource else value
	return duplicated


func _apply_patch_entry(
	skill_defs: Dictionary,
	enemy_ai_brains: Dictionary,
	ai_score_profile,
	patch_entry: Dictionary
) -> Array[String]:
	var errors: Array[String] = []
	var target_type := String(patch_entry.get("target_type", ""))
	var path := String(patch_entry.get("path", ""))
	var value = patch_entry.get("value", null)
	if path.is_empty():
		return ["Battle sim override patch for target_type=%s is missing path." % target_type]
	match target_type:
		"skill":
			var skill_id := ProgressionDataUtils.to_string_name(patch_entry.get("target_id", ""))
			if not skill_defs.has(skill_id):
				return ["Battle sim override patch target skill %s was not found for path %s." % [String(skill_id), path]]
			var error := _set_value_by_path(skill_defs.get(skill_id), path, value)
			if not error.is_empty():
				errors.append(error)
		"brain":
			var brain_id := ProgressionDataUtils.to_string_name(patch_entry.get("target_id", ""))
			if not enemy_ai_brains.has(brain_id):
				return ["Battle sim override patch target brain %s was not found for path %s." % [String(brain_id), path]]
			var error := _set_value_by_path(enemy_ai_brains.get(brain_id), path, value)
			if not error.is_empty():
				errors.append(error)
		"action":
			var action_resource = _resolve_action_resource(enemy_ai_brains, patch_entry)
			if action_resource == null:
				return ["Battle sim override patch target action was not found for path %s: %s." % [path, str(patch_entry)]]
			var error := _set_value_by_path(action_resource, path, value)
			if not error.is_empty():
				errors.append(error)
		"ai_score_profile":
			var error := _set_value_by_path(ai_score_profile, path, value)
			if not error.is_empty():
				errors.append(error)
		_:
			errors.append("Battle sim override patch uses unsupported target_type %s for path %s." % [target_type, path])
	return errors


func _resolve_action_resource(enemy_ai_brains: Dictionary, patch_entry: Dictionary):
	var brain_id := ProgressionDataUtils.to_string_name(patch_entry.get("brain_id", patch_entry.get("target_id", "")))
	if brain_id == &"" or not enemy_ai_brains.has(brain_id):
		return null
	var brain = enemy_ai_brains.get(brain_id)
	var state_id := ProgressionDataUtils.to_string_name(patch_entry.get("state_id", ""))
	var action_id := ProgressionDataUtils.to_string_name(patch_entry.get("action_id", ""))
	for state_def in brain.get_states():
		if state_def == null:
			continue
		if state_id != &"" and state_def.state_id != state_id:
			continue
		for action_resource in state_def.get_actions():
			if action_resource == null:
				continue
			if action_id == &"" or action_resource.action_id == action_id:
				return action_resource
	return null


func _set_value_by_path(target, path: String, value) -> String:
	var segments := path.split(".", false)
	if segments.is_empty():
		return "Battle sim override patch has empty path."
	return _set_value_recursive(target, segments, 0, value, path)


func _set_value_recursive(target, segments: PackedStringArray, index: int, value, full_path: String) -> String:
	if target == null or index >= segments.size():
		return "Battle sim override path %s could not be applied: null target at segment %d." % [full_path, index]
	var segment := segments[index]
	var is_last := index == segments.size() - 1
	if target is Array:
		if not String(segment).is_valid_int():
			return "Battle sim override path %s expected an array index at segment %s." % [full_path, segment]
		var array_index := int(segment)
		if array_index < 0 or array_index >= target.size():
			return "Battle sim override path %s has out-of-range array index %d at segment %s." % [full_path, array_index, segment]
		if is_last:
			target[array_index] = _coerce_value(target[array_index], value)
			return ""
		return _set_value_recursive(target[array_index], segments, index + 1, value, full_path)
	if target is Dictionary:
		var resolved_key = _resolve_dictionary_key(target, segment)
		if resolved_key == null:
			return "Battle sim override path %s references missing dictionary key %s." % [full_path, segment]
		if is_last:
			target[resolved_key] = _coerce_value(target.get(resolved_key, null), value)
			return ""
		return _set_value_recursive(target.get(resolved_key, null), segments, index + 1, value, full_path)
	if not _object_has_property(target, segment):
		return "Battle sim override path %s references missing property %s on %s." % [
			full_path,
			segment,
			target.get_class() if target is Object else typeof(target),
		]
	if is_last:
		var current_value = target.get(segment)
		target.set(segment, _coerce_value(current_value, value))
		return ""
	return _set_value_recursive(target.get(segment), segments, index + 1, value, full_path)


func _resolve_dictionary_key(target: Dictionary, segment: String):
	if target.has(segment):
		return segment
	var string_name_key := StringName(segment)
	if target.has(string_name_key):
		return string_name_key
	return null


func _object_has_property(target, property_name: String) -> bool:
	if target == null or target is not Object:
		return false
	for property_info in target.get_property_list():
		if String(property_info.get("name", "")) == property_name:
			return true
	return false


func _coerce_value(current_value, value):
	if current_value is StringName:
		return ProgressionDataUtils.to_string_name(value)
	if current_value is Vector2i:
		if value is Vector2i:
			return value
		if value is Dictionary:
			return Vector2i(int(value.get("x", 0)), int(value.get("y", 0)))
	if current_value is int:
		return int(value)
	if current_value is float:
		return float(value)
	if current_value is bool:
		return bool(value)
	return value
