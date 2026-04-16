class_name UseGroundSkillAction
extends "res://scripts/enemies/enemy_ai_action.gd"

var skill_ids: Array[StringName] = []
var minimum_hit_count := 1


func decide(context):
	var best_decision = null
	var best_score = -999999
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
				var primary_coord = command.target_coord
				var distance_score = 99 - context.grid_service.get_distance_from_unit_to_coord(context.unit_state, primary_coord)
				var total_score = hit_count * 100 + distance_score
				if total_score <= best_score:
					continue
				best_score = total_score
				best_decision = _create_decision(
					command,
					"%s 准备用 %s 覆盖 %d 个单位。" % [context.unit_state.display_name, skill_def.display_name, hit_count]
				)
	return best_decision
