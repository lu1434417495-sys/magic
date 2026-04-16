class_name UseGroundSkillAction
extends "res://scripts/enemies/enemy_ai_action.gd"

var skill_ids: Array[StringName] = []
var minimum_hit_count := 1


func decide(context):
	var best_decision = null
	var best_score_input = null
	for skill_id in _resolve_known_skill_ids(context, skill_ids):
		var skill_def = _get_skill_def(context, skill_id)
		if skill_def == null or skill_def.combat_profile == null:
			continue
		if skill_def.combat_profile.target_mode != &"ground":
			continue
		if not _get_skill_cast_block_reason(context, skill_def).is_empty():
			continue
		for cast_variant in _get_ground_variants(context, skill_def):
			if cast_variant == null or _is_charge_variant(cast_variant):
				continue
			for target_coords in _enumerate_ground_target_coord_sets(context, cast_variant):
				var command = _build_ground_skill_command(context, skill_id, cast_variant.variant_id, target_coords)
				var preview = context.preview_command(command)
				if preview == null or not bool(preview.allowed):
					continue
				var hit_count = preview.target_unit_ids.size()
				if hit_count < minimum_hit_count:
					continue
				var score_input = _build_skill_score_input(
					context,
					skill_def,
					command,
					preview,
					cast_variant.effect_defs,
					{
						"position_target_coord": command.target_coord,
					}
				)
				if score_input == null:
					return _create_decision(
						command,
						"%s 准备用 %s 覆盖 %d 个单位。" % [context.unit_state.display_name, skill_def.display_name, hit_count]
					)
				if not _is_better_skill_score_input(score_input, best_score_input):
					continue
				best_score_input = score_input
				best_decision = _create_decision(
					command,
					"%s 准备用 %s 覆盖 %d 个单位（评分 %d）。" % [
						context.unit_state.display_name,
						skill_def.display_name,
						hit_count,
						int(score_input.total_score),
					]
				)
	return best_decision
