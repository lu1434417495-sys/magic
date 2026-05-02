class_name BattleAiScoreService
extends RefCounted

const BATTLE_AI_SCORE_INPUT_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_score_input.gd")
const BATTLE_AI_SCORE_PROFILE_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_score_profile.gd")
const BATTLE_DAMAGE_PREVIEW_RANGE_SERVICE_SCRIPT = preload("res://scripts/systems/battle/rules/battle_damage_preview_range_service.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const BattleAiScoreInput = preload("res://scripts/systems/battle/ai/battle_ai_score_input.gd")
const BattleAiScoreProfile = preload("res://scripts/systems/battle/ai/battle_ai_score_profile.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")

const BONUS_CONDITION_TARGET_LOW_HP: StringName = &"target_low_hp"

var _score_profile: BattleAiScoreProfile = BATTLE_AI_SCORE_PROFILE_SCRIPT.new()


func setup(_damage_resolver = null) -> void:
	pass


func set_profile(profile: BattleAiScoreProfile) -> void:
	_score_profile = profile if profile != null else BATTLE_AI_SCORE_PROFILE_SCRIPT.new()


func get_profile() -> BattleAiScoreProfile:
	return _score_profile


func get_bucket_priority(bucket_id: StringName) -> int:
	return _score_profile.get_bucket_priority(bucket_id) if _score_profile != null else 0


func build_skill_score_input(
	context,
	skill_def: SkillDef,
	command,
	preview,
	effect_defs: Array = [],
	metadata: Dictionary = {}
) -> BattleAiScoreInput:
	var score_input := BATTLE_AI_SCORE_INPUT_SCRIPT.new()
	score_input.command = command
	score_input.skill_def = skill_def
	score_input.preview = preview
	score_input.action_kind = ProgressionDataUtils.to_string_name(metadata.get("action_kind", "skill"))
	score_input.action_label = String(metadata.get("action_label", skill_def.display_name if skill_def != null else ""))
	score_input.score_bucket_id = ProgressionDataUtils.to_string_name(metadata.get("score_bucket_id", ""))
	score_input.score_bucket_priority = get_bucket_priority(score_input.score_bucket_id)
	score_input.primary_coord = _resolve_primary_coord(command, preview)
	score_input.target_unit_ids = _copy_target_unit_ids(preview)
	score_input.target_coords = _copy_target_coords(preview)
	score_input.target_count = score_input.target_unit_ids.size()
	var effective_effect_defs := _filter_effect_defs_for_context(effect_defs, context, skill_def)
	_populate_hit_metrics(score_input, context, effective_effect_defs)
	_populate_resource_cost_metrics(score_input, skill_def, context)
	_populate_position_metrics(score_input, context, metadata)
	score_input.total_score = _resolve_action_base_score(score_input.action_kind, metadata) \
		+ score_input.hit_payoff_score \
		+ score_input.target_count * _score_profile.target_count_weight \
		- score_input.resource_cost_score \
		+ score_input.position_objective_score
	return score_input


func build_action_score_input(
	context,
	action_kind: StringName,
	action_label: String,
	score_bucket_id: StringName,
	command,
	preview,
	metadata: Dictionary = {}
) -> BattleAiScoreInput:
	var score_input := BATTLE_AI_SCORE_INPUT_SCRIPT.new()
	score_input.command = command
	score_input.preview = preview
	score_input.action_kind = action_kind
	score_input.action_label = action_label
	score_input.score_bucket_id = score_bucket_id
	score_input.score_bucket_priority = get_bucket_priority(score_bucket_id)
	score_input.primary_coord = _resolve_primary_coord(command, preview)
	score_input.target_unit_ids = _copy_target_unit_ids(preview)
	score_input.target_coords = _copy_target_coords(preview)
	score_input.target_count = _resolve_action_target_count(score_input)
	score_input.move_cost = int(metadata.get("move_cost", preview.move_cost if preview != null else 0))
	_populate_position_metrics(score_input, context, metadata)
	score_input.resource_cost_score = maxi(score_input.move_cost, 0) * _score_profile.movement_cost_weight
	score_input.total_score = _resolve_action_base_score(action_kind, metadata) \
		+ score_input.position_objective_score \
		+ score_input.target_count * int(metadata.get("target_count_weight", 0)) \
		- score_input.resource_cost_score
	return score_input


func _resolve_primary_coord(command, preview) -> Vector2i:
	if command != null and command.target_coord != Vector2i(-1, -1):
		return command.target_coord
	if preview != null and not preview.target_coords.is_empty():
		return preview.target_coords[0]
	return Vector2i(-1, -1)


func _copy_target_unit_ids(preview) -> Array[StringName]:
	var target_unit_ids: Array[StringName] = []
	if preview == null:
		return target_unit_ids
	for unit_id_variant in preview.target_unit_ids:
		target_unit_ids.append(ProgressionDataUtils.to_string_name(unit_id_variant))
	return target_unit_ids


func _copy_target_coords(preview) -> Array[Vector2i]:
	var target_coords: Array[Vector2i] = []
	if preview == null:
		return target_coords
	for coord_variant in preview.target_coords:
		if coord_variant is Vector2i:
			target_coords.append(coord_variant)
	return target_coords


func _populate_hit_metrics(score_input: BattleAiScoreInput, context, effect_defs: Array) -> void:
	if score_input == null:
		return
	score_input.estimated_hit_rate_percent = _resolve_estimated_hit_rate_percent(score_input.preview)
	if context == null or context.state == null or context.unit_state == null:
		return
	var estimated_healing := _estimate_healing(effect_defs)
	var estimated_status_count := _estimate_status_count(effect_defs)
	var estimated_terrain_effect_count := _estimate_terrain_effect_count(effect_defs)
	var estimated_height_delta := _estimate_height_delta(effect_defs)
	for target_unit_id in score_input.target_unit_ids:
		var target_unit := context.state.units.get(target_unit_id) as BattleUnitState
		if target_unit == null:
			continue
		var estimated_damage := _estimate_damage_for_target(context.unit_state, effect_defs, target_unit)
		score_input.estimated_damage += estimated_damage
		score_input.estimated_healing += estimated_healing
		score_input.estimated_status_count += estimated_status_count
		score_input.estimated_terrain_effect_count += estimated_terrain_effect_count
		score_input.estimated_height_delta += estimated_height_delta
		if target_unit.faction_id == context.unit_state.faction_id:
			score_input.hit_payoff_score += estimated_healing * _score_profile.heal_weight
			score_input.hit_payoff_score -= estimated_damage * _score_profile.damage_weight
		else:
			score_input.hit_payoff_score += estimated_damage * _score_profile.damage_weight
			score_input.hit_payoff_score -= estimated_healing * _score_profile.heal_weight
		score_input.hit_payoff_score += estimated_status_count * _score_profile.status_weight
		score_input.hit_payoff_score += estimated_terrain_effect_count * _score_profile.terrain_weight
		score_input.hit_payoff_score += estimated_height_delta * _score_profile.height_weight
	score_input.hit_payoff_score = int(round(
		float(score_input.hit_payoff_score) * float(score_input.estimated_hit_rate_percent) / 100.0
	))


func _filter_effect_defs_for_context(effect_defs: Array, context, skill_def: SkillDef) -> Array:
	var filtered_effect_defs: Array = []
	var should_filter := context != null and context.unit_state != null
	var skill_level := _get_context_skill_level(context, skill_def.skill_id if skill_def != null else &"")
	for effect_def_variant in effect_defs:
		var effect_def := effect_def_variant as CombatEffectDef
		if effect_def == null:
			continue
		if not _is_effect_unlocked_for_skill_level(effect_def, skill_level, should_filter):
			continue
		filtered_effect_defs.append(effect_def)
	return filtered_effect_defs


func _is_effect_unlocked_for_skill_level(effect_def: CombatEffectDef, skill_level: int, should_filter: bool) -> bool:
	if effect_def == null:
		return false
	if not should_filter:
		return true
	var min_level := maxi(int(effect_def.min_skill_level), 0)
	var max_level := int(effect_def.max_skill_level)
	if skill_level < min_level:
		return false
	return max_level < 0 or skill_level <= max_level


func _estimate_damage_for_target(source_unit: BattleUnitState, effect_defs: Array, target_unit: BattleUnitState) -> int:
	var total := 0
	for effect_def_variant in effect_defs:
		var effect_def := effect_def_variant as CombatEffectDef
		if effect_def == null or effect_def.effect_type != &"damage":
			continue
		var single_effect_defs: Array = [effect_def]
		var damage_preview := BATTLE_DAMAGE_PREVIEW_RANGE_SERVICE_SCRIPT.build_skill_damage_preview(
			source_unit,
			single_effect_defs
		)
		var base_damage := _estimate_damage_from_preview(damage_preview)
		var multiplier := _resolve_effect_damage_multiplier(effect_def, target_unit)
		total += maxi(int(round(float(base_damage) * multiplier)), 0)
	return total


func _estimate_damage_from_preview(damage_preview: Dictionary) -> int:
	if damage_preview.is_empty() or not bool(damage_preview.get("has_damage", false)):
		return 0
	return maxi(int(round(
		(float(damage_preview.get("min_damage", 0)) + float(damage_preview.get("max_damage", 0))) / 2.0
	)), 0)


func _resolve_effect_damage_multiplier(effect_def: CombatEffectDef, target_unit: BattleUnitState) -> float:
	if effect_def == null:
		return 1.0
	var multiplier := _get_pre_resistance_damage_multiplier(effect_def)
	if _has_bonus_condition(effect_def, target_unit):
		multiplier *= _get_damage_ratio_multiplier(effect_def)
	return maxf(multiplier, 0.0)


func _get_pre_resistance_damage_multiplier(effect_def: CombatEffectDef) -> float:
	if effect_def == null or effect_def.params == null:
		return 1.0
	return maxf(float(effect_def.params.get("runtime_pre_resistance_damage_multiplier", 1.0)), 0.0)


func _has_bonus_condition(effect_def: CombatEffectDef, target_unit: BattleUnitState) -> bool:
	if effect_def == null or target_unit == null:
		return false
	match effect_def.bonus_condition:
		BONUS_CONDITION_TARGET_LOW_HP:
			return _is_target_low_hp(effect_def, target_unit)
		_:
			return false


func _is_target_low_hp(effect_def: CombatEffectDef, target_unit: BattleUnitState) -> bool:
	var max_hp := 0
	if target_unit.attribute_snapshot != null:
		max_hp = target_unit.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX)
	if max_hp <= 0:
		max_hp = maxi(target_unit.current_hp, 1)

	var threshold_ratio := 0.5
	if effect_def != null and effect_def.params != null:
		if effect_def.params.has("hp_ratio_threshold"):
			threshold_ratio = clampf(float(effect_def.params.get("hp_ratio_threshold", threshold_ratio)), 0.0, 1.0)
	return float(target_unit.current_hp) <= float(max_hp) * threshold_ratio


func _get_damage_ratio_multiplier(effect_def: CombatEffectDef) -> float:
	if effect_def == null:
		return 1.0
	return maxf(float(effect_def.damage_ratio_percent) / 100.0, 0.0)


func _estimate_healing(effect_defs: Array) -> int:
	var total := 0
	for effect_def_variant in effect_defs:
		var effect_def := effect_def_variant as CombatEffectDef
		if effect_def == null or effect_def.effect_type != &"heal":
			continue
		total += maxi(int(effect_def.power), 1)
	return total


func _estimate_status_count(effect_defs: Array) -> int:
	var status_ids: Dictionary = {}
	for effect_def_variant in effect_defs:
		var effect_def := effect_def_variant as CombatEffectDef
		if effect_def == null:
			continue
		if effect_def.effect_type != &"status" and effect_def.effect_type != &"apply_status":
			continue
		if effect_def.status_id == &"":
			continue
		status_ids[effect_def.status_id] = true
	return status_ids.size()


func _estimate_terrain_effect_count(effect_defs: Array) -> int:
	var terrain_effect_ids: Dictionary = {}
	for effect_def_variant in effect_defs:
		var effect_def := effect_def_variant as CombatEffectDef
		if effect_def == null:
			continue
		if effect_def.effect_type != &"terrain" and effect_def.effect_type != &"terrain_effect":
			continue
		if effect_def.terrain_effect_id == &"":
			continue
		terrain_effect_ids[effect_def.terrain_effect_id] = true
	return terrain_effect_ids.size()


func _estimate_height_delta(effect_defs: Array) -> int:
	var total := 0
	for effect_def_variant in effect_defs:
		var effect_def := effect_def_variant as CombatEffectDef
		if effect_def == null:
			continue
		if effect_def.effect_type != &"height" and effect_def.effect_type != &"height_delta":
			continue
		total += absi(int(effect_def.height_delta))
	return total


func _resolve_estimated_hit_rate_percent(preview) -> int:
	if preview == null or preview.hit_preview.is_empty():
		return 100
	var stage_success_rates: Array = preview.hit_preview.get("stage_success_rates", [])
	if stage_success_rates is Array and not stage_success_rates.is_empty():
		var total := 0
		for hit_rate_variant in stage_success_rates:
			total += int(hit_rate_variant)
		return maxi(int(round(float(total) / float(stage_success_rates.size()))), 0)
	if preview.hit_preview.has("success_rate_percent"):
		return maxi(int(preview.hit_preview.get("success_rate_percent", 100)), 0)
	return 100


func _populate_resource_cost_metrics(score_input: BattleAiScoreInput, skill_def: SkillDef, context) -> void:
	if score_input == null or skill_def == null or skill_def.combat_profile == null:
		return
	var skill_level := _get_context_skill_level(context, skill_def.skill_id)
	var costs := skill_def.combat_profile.get_effective_resource_costs(skill_level)
	score_input.ap_cost = maxi(int(costs.get("ap_cost", skill_def.combat_profile.ap_cost)), 0)
	score_input.mp_cost = maxi(int(costs.get("mp_cost", skill_def.combat_profile.mp_cost)), 0)
	score_input.stamina_cost = maxi(int(costs.get("stamina_cost", skill_def.combat_profile.stamina_cost)), 0)
	score_input.aura_cost = maxi(int(costs.get("aura_cost", skill_def.combat_profile.aura_cost)), 0)
	score_input.cooldown_tu = maxi(int(costs.get("cooldown_tu", skill_def.combat_profile.cooldown_tu)), 0)
	score_input.resource_cost_score = score_input.ap_cost * _score_profile.ap_cost_weight \
		+ score_input.mp_cost * _score_profile.mp_cost_weight \
		+ score_input.stamina_cost * _score_profile.stamina_cost_weight \
		+ score_input.aura_cost * _score_profile.aura_cost_weight \
		+ score_input.cooldown_tu * _score_profile.cooldown_weight


func _get_context_skill_level(context, skill_id: StringName) -> int:
	if context == null or skill_id == &"":
		return 0
	var unit_state = context.get("unit_state")
	if unit_state == null:
		return 0
	if unit_state.known_skill_level_map.has(skill_id):
		return int(unit_state.known_skill_level_map.get(skill_id, 0))
	return 1 if unit_state.known_active_skill_ids.has(skill_id) else 0


func _populate_position_metrics(score_input: BattleAiScoreInput, context, metadata: Dictionary) -> void:
	if score_input == null or context == null or context.unit_state == null or context.grid_service == null:
		return
	var desired_min_distance := int(metadata.get("desired_min_distance", -1))
	var desired_max_distance := int(metadata.get("desired_max_distance", desired_min_distance))
	score_input.desired_min_distance = desired_min_distance
	score_input.desired_max_distance = maxi(desired_max_distance, desired_min_distance) if desired_min_distance >= 0 and desired_max_distance >= 0 else -1
	var explicit_objective_kind = ProgressionDataUtils.to_string_name(metadata.get("position_objective_kind", ""))
	if explicit_objective_kind == &"none":
		score_input.position_objective_kind = &"none"
		score_input.position_anchor_coord = context.unit_state.coord
		score_input.distance_to_primary_coord = -1
		score_input.position_objective_score = 0
		return
	var position_target_unit = metadata.get("position_target_unit", null) as BattleUnitState
	var current_distance_to_target := -1
	if position_target_unit != null:
		score_input.position_objective_kind = explicit_objective_kind if explicit_objective_kind != &"" else &"distance_band"
		score_input.position_anchor_coord = _resolve_position_anchor_coord(score_input, context, metadata)
		score_input.distance_to_primary_coord = _distance_from_anchor_to_unit(
			context,
			score_input.position_anchor_coord,
			position_target_unit
		)
		if score_input.position_objective_kind == &"distance_band_progress":
			current_distance_to_target = _distance_from_anchor_to_unit(
				context,
				context.unit_state.coord,
				position_target_unit
			)
	else:
		score_input.position_objective_kind = explicit_objective_kind if explicit_objective_kind != &"" else &"cast_distance"
		score_input.position_anchor_coord = _resolve_position_anchor_coord(score_input, context, metadata)
		score_input.distance_to_primary_coord = context.grid_service.get_distance_from_unit_to_coord(
			context.unit_state,
			score_input.primary_coord
		) if score_input.primary_coord != Vector2i(-1, -1) else -1
	score_input.position_objective_score = _build_position_objective_score(
		score_input.position_objective_kind,
		score_input.distance_to_primary_coord,
		score_input.desired_min_distance,
		score_input.desired_max_distance,
		current_distance_to_target
	)


func _resolve_position_anchor_coord(score_input: BattleAiScoreInput, context, metadata: Dictionary) -> Vector2i:
	if context == null or context.unit_state == null:
		return Vector2i(-1, -1)
	var metadata_anchor = metadata.get("position_anchor_coord", Vector2i(-1, -1))
	if metadata_anchor is Vector2i and metadata_anchor != Vector2i(-1, -1):
		return metadata_anchor
	if score_input != null and score_input.preview != null and score_input.preview.resolved_anchor_coord != Vector2i(-1, -1):
		return score_input.preview.resolved_anchor_coord
	return context.unit_state.coord


func _distance_from_anchor_to_unit(context, anchor_coord: Vector2i, target_unit: BattleUnitState) -> int:
	if context == null or context.unit_state == null or context.grid_service == null or target_unit == null:
		return -1
	context.unit_state.refresh_footprint()
	target_unit.refresh_footprint()
	var best_distance := 999999
	for source_coord in context.grid_service.get_footprint_coords(anchor_coord, context.unit_state.footprint_size):
		for target_coord in target_unit.occupied_coords:
			best_distance = mini(best_distance, context.grid_service.get_distance(source_coord, target_coord))
	return best_distance if best_distance < 999999 else -1


func _build_position_objective_score(
	position_objective_kind: StringName,
	distance_value: int,
	desired_min_distance: int,
	desired_max_distance: int,
	current_distance_value: int = -1
) -> int:
	if distance_value < 0 or desired_min_distance < 0 or desired_max_distance < 0:
		return 0
	if position_objective_kind == &"distance_band_progress":
		return _build_distance_band_progress_score(
			distance_value,
			desired_min_distance,
			desired_max_distance,
			current_distance_value
		)
	if position_objective_kind == &"distance_floor":
		if distance_value < desired_min_distance:
			return -((desired_min_distance - distance_value) * _score_profile.position_undershoot_penalty)
		return _score_profile.position_base_score \
			+ (distance_value - desired_min_distance) * _score_profile.position_distance_step
	if distance_value >= desired_min_distance and distance_value <= desired_max_distance:
		return maxi(_score_profile.position_base_score - distance_value * _score_profile.position_distance_step, 0)
	if distance_value < desired_min_distance:
		return -((desired_min_distance - distance_value) * _score_profile.position_undershoot_penalty)
	return -((distance_value - desired_max_distance) * _score_profile.position_overshoot_penalty)


func _build_distance_band_progress_score(
	distance_value: int,
	desired_min_distance: int,
	desired_max_distance: int,
	current_distance_value: int
) -> int:
	var candidate_gap := _build_distance_gap(distance_value, desired_min_distance, desired_max_distance)
	if candidate_gap < 0:
		return 0
	var current_gap := _build_distance_gap(current_distance_value, desired_min_distance, desired_max_distance)
	if current_gap < 0:
		return _build_distance_band_absolute_score(distance_value, desired_min_distance, desired_max_distance)
	if current_gap == 0:
		return _build_distance_band_absolute_score(distance_value, desired_min_distance, desired_max_distance)
	if candidate_gap < current_gap:
		var progress_steps := current_gap - candidate_gap
		return _score_profile.position_base_score + progress_steps * _score_profile.position_distance_step
	if candidate_gap == current_gap:
		return -_score_profile.position_distance_step
	return -((candidate_gap - current_gap) * _score_profile.position_overshoot_penalty)


func _build_distance_gap(distance_value: int, desired_min_distance: int, desired_max_distance: int) -> int:
	if distance_value < 0 or desired_min_distance < 0 or desired_max_distance < 0:
		return -1
	if distance_value < desired_min_distance:
		return desired_min_distance - distance_value
	if distance_value > desired_max_distance:
		return distance_value - desired_max_distance
	return 0


func _build_distance_band_absolute_score(
	distance_value: int,
	desired_min_distance: int,
	desired_max_distance: int
) -> int:
	if distance_value >= desired_min_distance and distance_value <= desired_max_distance:
		return maxi(_score_profile.position_base_score - distance_value * _score_profile.position_distance_step, 0)
	if distance_value < desired_min_distance:
		return -((desired_min_distance - distance_value) * _score_profile.position_undershoot_penalty)
	return -((distance_value - desired_max_distance) * _score_profile.position_overshoot_penalty)


func _resolve_action_base_score(action_kind: StringName, metadata: Dictionary) -> int:
	if metadata.has("action_base_score"):
		return int(metadata.get("action_base_score", 0))
	return _score_profile.get_action_base_score(action_kind) if _score_profile != null else 0


func _resolve_action_target_count(score_input: BattleAiScoreInput) -> int:
	if score_input == null:
		return 0
	if score_input.target_count > 0:
		return score_input.target_count
	if not score_input.target_unit_ids.is_empty():
		return score_input.target_unit_ids.size()
	if not score_input.target_coords.is_empty():
		return score_input.target_coords.size()
	return 0
