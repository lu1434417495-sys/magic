class_name UseUnitSkillAction
extends "res://scripts/enemies/enemy_ai_action.gd"

var skill_ids: Array[StringName] = []
var target_selector: StringName = &"nearest_enemy"


func decide(context):
	for skill_id in _resolve_known_skill_ids(context, skill_ids):
		var skill_def = _get_skill_def(context, skill_id)
		if skill_def == null or skill_def.combat_profile == null:
			continue
		if skill_def.combat_profile.target_mode != &"unit":
			continue
		if not _get_skill_cast_block_reason(context, skill_def).is_empty():
			continue
		for target_unit in _sort_target_units(context, skill_def.combat_profile.target_team_filter, target_selector):
			var command = _build_unit_skill_command(context, skill_id, target_unit)
			if not _preview_allowed(context, command):
				continue
			return _create_decision(
				command,
				"%s 选择对 %s 使用 %s。" % [context.unit_state.display_name, target_unit.display_name, skill_def.display_name]
			)
	return null
