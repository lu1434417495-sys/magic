class_name BattleAiScoreInput
extends RefCounted

const BattleCommand = preload("res://scripts/systems/battle/core/battle_command.gd")
const BattlePreview = preload("res://scripts/systems/battle/core/battle_preview.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")

var command: BattleCommand = null
var skill_def: SkillDef = null
var preview: BattlePreview = null
var action_kind: StringName = &"skill"
var action_label: String = ""
var score_bucket_id: StringName = &""
var score_bucket_priority := 0
var primary_coord: Vector2i = Vector2i(-1, -1)
var target_unit_ids: Array[StringName] = []
var target_coords: Array[Vector2i] = []
var target_count := 0
var estimated_damage := 0
var estimated_healing := 0
var estimated_status_count := 0
var estimated_terrain_effect_count := 0
var estimated_height_delta := 0
var estimated_hit_rate_percent := 100
var hit_payoff_score := 0
var ap_cost := 0
var mp_cost := 0
var stamina_cost := 0
var aura_cost := 0
var cooldown_tu := 0
var resource_cost_score := 0
var move_cost := 0
var position_objective_kind: StringName = &"cast_distance"
var desired_min_distance := -1
var desired_max_distance := -1
var position_anchor_coord: Vector2i = Vector2i(-1, -1)
var distance_to_primary_coord := -1
var position_objective_score := 0
var total_score := 0


func to_dict() -> Dictionary:
	return {
		"action_kind": String(action_kind),
		"action_label": action_label,
		"score_bucket_id": String(score_bucket_id),
		"score_bucket_priority": score_bucket_priority,
		"command_type": String(command.command_type) if command != null else "",
		"skill_id": String(skill_def.skill_id) if skill_def != null else "",
		"primary_coord": primary_coord,
		"target_unit_ids": target_unit_ids.duplicate(),
		"target_coords": target_coords.duplicate(),
		"target_count": target_count,
		"estimated_damage": estimated_damage,
		"estimated_healing": estimated_healing,
		"estimated_status_count": estimated_status_count,
		"estimated_terrain_effect_count": estimated_terrain_effect_count,
		"estimated_height_delta": estimated_height_delta,
		"estimated_hit_rate_percent": estimated_hit_rate_percent,
		"hit_payoff_score": hit_payoff_score,
		"ap_cost": ap_cost,
		"mp_cost": mp_cost,
		"stamina_cost": stamina_cost,
		"aura_cost": aura_cost,
		"cooldown_tu": cooldown_tu,
		"resource_cost_score": resource_cost_score,
		"move_cost": move_cost,
		"position_objective_kind": String(position_objective_kind),
		"desired_min_distance": desired_min_distance,
		"desired_max_distance": desired_max_distance,
		"position_anchor_coord": position_anchor_coord,
		"distance_to_primary_coord": distance_to_primary_coord,
		"position_objective_score": position_objective_score,
		"total_score": total_score,
	}
