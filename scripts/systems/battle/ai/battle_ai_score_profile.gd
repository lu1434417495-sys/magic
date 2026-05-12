class_name BattleAiScoreProfile
extends Resource

const THREAT_MULTIPLIER_BASIS_POINTS_DENOMINATOR := 10000

@export var damage_weight := 10
@export var heal_weight := 8
@export var status_weight := 25
@export var terrain_weight := 15
@export var height_weight := 12
@export var lethal_target_weight := 500
@export var lethal_threat_target_weight := 900
@export var target_count_weight := 40
@export var friendly_fire_damage_weight := 35
@export var friendly_fire_target_weight := 250
@export var friendly_control_target_weight := 350
@export var friendly_lethal_target_weight := 5000
@export var ap_cost_weight := 25
@export var mp_cost_weight := 15
@export var stamina_cost_weight := 2
@export var aura_cost_weight := 35
@export var cooldown_weight := 8
@export var movement_cost_weight := 18
@export var position_base_score := 60
@export var position_distance_step := 4
@export var position_undershoot_penalty := 15
@export var position_overshoot_penalty := 12
@export var threat_healer_bias_basis_points := 1500
@export var threat_control_bias_basis_points := 500
@export var threat_ranged_bias_basis_points := 800
@export var threat_range_step_bias_basis_points := 200
@export var threat_multiplier_cap_basis_points := 15000
@export var meteor_high_priority_threat_multiplier_bp := 11000
@export var meteor_high_priority_damage_hp_percent := 35
@export var meteor_high_priority_target_priority_score := 250
@export var meteor_top_threat_rank := 1
@export var meteor_friendly_fire_profile: StringName = &"default"
@export var meteor_friendly_fire_soft_expected_hp_percent := 10
@export var meteor_friendly_fire_hard_expected_hp_percent := 25
@export var meteor_friendly_fire_hard_worst_case_hp_percent := 50
@export var action_base_scores: Dictionary = {
	"skill": 0,
	"move": 20,
	"retreat": 35,
	"wait": -40,
}
@export var default_bucket_priority := 0
@export var bucket_priorities: Dictionary = {
	"mist_support": 120,
	"mist_control": 110,
	"mist_offense": 100,
	"frontline_guard": 130,
	"harrier_pressure": 100,
	"charge_open": 100,
	"archer_survival": 150,
	"archer_positioning": 110,
	"archer_pressure": 90,
}


func get_action_base_score(action_kind: StringName) -> int:
	var action_key := String(action_kind)
	if action_base_scores.has(action_key):
		return int(action_base_scores.get(action_key, 0))
	return int(action_base_scores.get("skill", 0))


func get_bucket_priority(bucket_id: StringName) -> int:
	var bucket_key := String(bucket_id)
	if bucket_priorities.has(bucket_key):
		return int(bucket_priorities.get(bucket_key, default_bucket_priority))
	return default_bucket_priority


func to_dict() -> Dictionary:
	return {
		"damage_weight": damage_weight,
		"heal_weight": heal_weight,
		"status_weight": status_weight,
		"terrain_weight": terrain_weight,
		"height_weight": height_weight,
		"lethal_target_weight": lethal_target_weight,
		"lethal_threat_target_weight": lethal_threat_target_weight,
		"target_count_weight": target_count_weight,
		"friendly_fire_damage_weight": friendly_fire_damage_weight,
		"friendly_fire_target_weight": friendly_fire_target_weight,
		"friendly_control_target_weight": friendly_control_target_weight,
		"friendly_lethal_target_weight": friendly_lethal_target_weight,
		"ap_cost_weight": ap_cost_weight,
		"mp_cost_weight": mp_cost_weight,
		"stamina_cost_weight": stamina_cost_weight,
		"aura_cost_weight": aura_cost_weight,
		"cooldown_weight": cooldown_weight,
		"movement_cost_weight": movement_cost_weight,
		"position_base_score": position_base_score,
		"position_distance_step": position_distance_step,
		"position_undershoot_penalty": position_undershoot_penalty,
		"position_overshoot_penalty": position_overshoot_penalty,
		"threat_healer_bias_basis_points": threat_healer_bias_basis_points,
		"threat_control_bias_basis_points": threat_control_bias_basis_points,
		"threat_ranged_bias_basis_points": threat_ranged_bias_basis_points,
		"threat_range_step_bias_basis_points": threat_range_step_bias_basis_points,
		"threat_multiplier_cap_basis_points": threat_multiplier_cap_basis_points,
		"meteor_high_priority_threat_multiplier_bp": meteor_high_priority_threat_multiplier_bp,
		"meteor_high_priority_damage_hp_percent": meteor_high_priority_damage_hp_percent,
		"meteor_high_priority_target_priority_score": meteor_high_priority_target_priority_score,
		"meteor_top_threat_rank": meteor_top_threat_rank,
		"meteor_friendly_fire_profile": String(meteor_friendly_fire_profile),
		"meteor_friendly_fire_soft_expected_hp_percent": meteor_friendly_fire_soft_expected_hp_percent,
		"meteor_friendly_fire_hard_expected_hp_percent": meteor_friendly_fire_hard_expected_hp_percent,
		"meteor_friendly_fire_hard_worst_case_hp_percent": meteor_friendly_fire_hard_worst_case_hp_percent,
		"action_base_scores": action_base_scores.duplicate(true),
		"default_bucket_priority": default_bucket_priority,
		"bucket_priorities": bucket_priorities.duplicate(true),
	}
