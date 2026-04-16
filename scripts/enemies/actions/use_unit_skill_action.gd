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
		var best_decision = null
		var best_score_input = null
		for target_unit in _sort_target_units(context, skill_def.combat_profile.target_team_filter, target_selector):
			var command = _build_unit_skill_command(context, skill_id, target_unit)
			var preview = context.preview_command(command)
			if preview == null or not bool(preview.allowed):
				continue
			var score_input = _build_skill_score_input(
				context,
				skill_def,
				command,
				preview,
				skill_def.combat_profile.effect_defs,
				{
					"position_target_unit": target_unit,
				}
			)
			if score_input == null:
				return _create_decision(
					command,
					"%s 选择对 %s 使用 %s。" % [context.unit_state.display_name, target_unit.display_name, skill_def.display_name]
				)
			if not _is_better_skill_score_input(score_input, best_score_input):
				continue
			best_score_input = score_input
			best_decision = _create_scored_decision(
				command,
				score_input,
				"%s 选择对 %s 使用 %s（评分 %d）。" % [
					context.unit_state.display_name,
					target_unit.display_name,
					skill_def.display_name,
					int(score_input.total_score),
				]
			)
		if best_decision != null:
			return best_decision
	return null
