class_name BattleAiScoreService
extends RefCounted

const BATTLE_AI_SCORE_INPUT_SCRIPT = preload("res://scripts/systems/battle_ai_score_input.gd")
const BattleAiScoreInput = preload("res://scripts/systems/battle_ai_score_input.gd")
const BattleUnitState = preload("res://scripts/systems/battle_unit_state.gd")
const BattleDamageResolver = preload("res://scripts/systems/battle_damage_resolver.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")

const DAMAGE_WEIGHT := 10
const HEAL_WEIGHT := 8
const STATUS_WEIGHT := 25
const TERRAIN_WEIGHT := 15
const HEIGHT_WEIGHT := 12
const TARGET_COUNT_WEIGHT := 40
const AP_COST_WEIGHT := 25
const MP_COST_WEIGHT := 15
const STAMINA_COST_WEIGHT := 20
const AURA_COST_WEIGHT := 35
const COOLDOWN_WEIGHT := 8
const POSITION_BASE_SCORE := 60
const POSITION_DISTANCE_STEP := 4
const POSITION_UNDERSHOOT_PENALTY := 15
const POSITION_OVERSHOOT_PENALTY := 12

var _damage_resolver: BattleDamageResolver = null


func setup(damage_resolver: BattleDamageResolver = null) -> void:
	if damage_resolver != null:
		_damage_resolver = damage_resolver


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
	score_input.primary_coord = _resolve_primary_coord(command, preview)
	score_input.target_unit_ids = _copy_target_unit_ids(preview)
	score_input.target_coords = _copy_target_coords(preview)
	score_input.target_count = score_input.target_unit_ids.size()
	_populate_hit_metrics(score_input, context, effect_defs)
	_populate_resource_cost_metrics(score_input, skill_def)
	_populate_position_metrics(score_input, context, metadata)
	score_input.total_score = score_input.hit_payoff_score \
		+ score_input.target_count * TARGET_COUNT_WEIGHT \
		- score_input.resource_cost_score \
		+ score_input.position_objective_score
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
	if context == null or context.state == null or context.unit_state == null or _damage_resolver == null:
		return
	for target_unit_id in score_input.target_unit_ids:
		var target_unit := context.state.units.get(target_unit_id) as BattleUnitState
		if target_unit == null:
			continue
		var simulated_target: BattleUnitState = BattleUnitState.from_dict(target_unit.to_dict())
		if simulated_target == null:
			continue
		var result := _damage_resolver.resolve_effects(context.unit_state, simulated_target, effect_defs)
		var damage := int(result.get("damage", 0))
		var healing := int(result.get("healing", 0))
		var status_count := (result.get("status_effect_ids", []) as Array).size()
		var terrain_effect_count := (result.get("terrain_effect_ids", []) as Array).size()
		var height_delta := absi(int(result.get("height_delta", 0)))
		score_input.estimated_damage += damage
		score_input.estimated_healing += healing
		score_input.estimated_status_count += status_count
		score_input.estimated_terrain_effect_count += terrain_effect_count
		score_input.estimated_height_delta += height_delta
		if target_unit.faction_id == context.unit_state.faction_id:
			score_input.hit_payoff_score += healing * HEAL_WEIGHT
			score_input.hit_payoff_score -= damage * DAMAGE_WEIGHT
		else:
			score_input.hit_payoff_score += damage * DAMAGE_WEIGHT
			score_input.hit_payoff_score -= healing * HEAL_WEIGHT
		score_input.hit_payoff_score += status_count * STATUS_WEIGHT
		score_input.hit_payoff_score += terrain_effect_count * TERRAIN_WEIGHT
		score_input.hit_payoff_score += height_delta * HEIGHT_WEIGHT
	score_input.hit_payoff_score = int(round(
		float(score_input.hit_payoff_score) * float(score_input.estimated_hit_rate_percent) / 100.0
	))


func _resolve_estimated_hit_rate_percent(preview) -> int:
	if preview == null or preview.hit_preview.is_empty():
		return 100
	var stage_hit_rates: Array = preview.hit_preview.get("stage_hit_rates", [])
	if stage_hit_rates is Array and not stage_hit_rates.is_empty():
		var total := 0
		for hit_rate_variant in stage_hit_rates:
			total += int(hit_rate_variant)
		return maxi(int(round(float(total) / float(stage_hit_rates.size()))), 0)
	if preview.hit_preview.has("hit_rate_percent"):
		return maxi(int(preview.hit_preview.get("hit_rate_percent", 100)), 0)
	return 100


func _populate_resource_cost_metrics(score_input: BattleAiScoreInput, skill_def: SkillDef) -> void:
	if score_input == null or skill_def == null or skill_def.combat_profile == null:
		return
	score_input.ap_cost = maxi(int(skill_def.combat_profile.ap_cost), 0)
	score_input.mp_cost = maxi(int(skill_def.combat_profile.mp_cost), 0)
	score_input.stamina_cost = maxi(int(skill_def.combat_profile.stamina_cost), 0)
	score_input.aura_cost = maxi(int(skill_def.combat_profile.aura_cost), 0)
	score_input.cooldown_tu = maxi(int(skill_def.combat_profile.cooldown_tu), 0)
	score_input.resource_cost_score = score_input.ap_cost * AP_COST_WEIGHT \
		+ score_input.mp_cost * MP_COST_WEIGHT \
		+ score_input.stamina_cost * STAMINA_COST_WEIGHT \
		+ score_input.aura_cost * AURA_COST_WEIGHT \
		+ score_input.cooldown_tu * COOLDOWN_WEIGHT


func _populate_position_metrics(score_input: BattleAiScoreInput, context, metadata: Dictionary) -> void:
	if score_input == null or context == null or context.unit_state == null or context.grid_service == null:
		return
	var desired_min_distance := int(metadata.get("desired_min_distance", 0))
	var desired_max_distance := int(metadata.get(
		"desired_max_distance",
		int(score_input.skill_def.combat_profile.range_value) if score_input.skill_def != null and score_input.skill_def.combat_profile != null else desired_min_distance
	))
	score_input.desired_min_distance = desired_min_distance
	score_input.desired_max_distance = maxi(desired_max_distance, desired_min_distance)
	var position_target_unit = metadata.get("position_target_unit", null) as BattleUnitState
	if position_target_unit != null:
		score_input.position_objective_kind = &"distance_band"
		score_input.distance_to_primary_coord = _distance_from_anchor_to_unit(
			context,
			Vector2i(metadata.get("position_anchor_coord", context.unit_state.coord)),
			position_target_unit
		)
	else:
		score_input.position_objective_kind = ProgressionDataUtils.to_string_name(
			metadata.get("position_objective_kind", "cast_distance")
		)
		score_input.distance_to_primary_coord = context.grid_service.get_distance_from_unit_to_coord(
			context.unit_state,
			score_input.primary_coord
		) if score_input.primary_coord != Vector2i(-1, -1) else -1
	score_input.position_objective_score = _build_position_objective_score(
		score_input.distance_to_primary_coord,
		score_input.desired_min_distance,
		score_input.desired_max_distance
	)


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


func _build_position_objective_score(distance_value: int, desired_min_distance: int, desired_max_distance: int) -> int:
	if distance_value < 0:
		return 0
	if distance_value >= desired_min_distance and distance_value <= desired_max_distance:
		return maxi(POSITION_BASE_SCORE - distance_value * POSITION_DISTANCE_STEP, 0)
	if distance_value < desired_min_distance:
		return -((desired_min_distance - distance_value) * POSITION_UNDERSHOOT_PENALTY)
	return -((distance_value - desired_max_distance) * POSITION_OVERSHOOT_PENALTY)
