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
var effective_target_count := 0
var enemy_target_count := 0
var ally_target_count := 0
var estimated_damage := 0
var estimated_healing := 0
var estimated_enemy_damage := 0
var estimated_ally_damage := 0
var estimated_enemy_healing := 0
var estimated_ally_healing := 0
var estimated_status_count := 0
var estimated_control_count := 0
var estimated_terrain_effect_count := 0
var estimated_height_delta := 0
var estimated_lethal_target_count := 0
var estimated_lethal_threat_target_count := 0
var estimated_lethal_target_ids: Array[StringName] = []
var estimated_lethal_threat_target_ids: Array[StringName] = []
var estimated_control_target_ids: Array[StringName] = []
var estimated_control_threat_target_ids: Array[StringName] = []
var estimated_friendly_fire_target_count := 0
var estimated_friendly_fire_damage := 0
var estimated_friendly_control_target_count := 0
var estimated_friendly_lethal_target_count := 0
var estimated_chain_target_count := 0
var estimated_chain_enemy_target_count := 0
var estimated_chain_ally_target_count := 0
var estimated_hit_rate_percent := 100
var save_estimates_by_target_id: Dictionary = {}
var special_profile_preview_facts: Dictionary = {}
var target_numeric_summary: Array[Dictionary] = []
var friendly_fire_numeric_summary: Array[Dictionary] = []
var friendly_fire_reject_reason: String = ""
var meteor_use_case: StringName = &""
var high_priority_target_ids: Array[StringName] = []
var high_priority_reasons: Dictionary = {}
var low_value_penalty_reason: String = ""
var attack_roll_modifier_breakdown: Array[Dictionary] = []
var hit_payoff_score := 0
var target_priority_score := 0
var friendly_fire_penalty_score := 0
var path_step_hit_count := 0
var path_step_unique_target_count := 0
var path_step_hit_counts_by_unit_id: Dictionary = {}
var path_step_payoff_score := 0
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
var position_current_distance := -1
var position_safe_distance := -1
var position_objective_score := 0
var has_post_action_threat_projection := false
var projected_actor_coord: Vector2i = Vector2i(-1, -1)
var pre_action_threat_unit_ids: Array[StringName] = []
var pre_action_threat_count := 0
var pre_action_threat_expected_damage := 0
var pre_action_survival_margin := 0
var pre_action_is_lethal_survival_risk := false
var post_action_remaining_threat_unit_ids: Array[StringName] = []
var post_action_remaining_threat_count := 0
var post_action_remaining_threat_expected_damage := 0
var post_action_survival_margin := 0
var post_action_is_lethal_survival_risk := false
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
		"effective_target_count": effective_target_count,
		"enemy_target_count": enemy_target_count,
		"ally_target_count": ally_target_count,
		"estimated_damage": estimated_damage,
		"estimated_healing": estimated_healing,
		"estimated_enemy_damage": estimated_enemy_damage,
		"estimated_ally_damage": estimated_ally_damage,
		"estimated_enemy_healing": estimated_enemy_healing,
		"estimated_ally_healing": estimated_ally_healing,
		"estimated_status_count": estimated_status_count,
		"estimated_control_count": estimated_control_count,
		"estimated_terrain_effect_count": estimated_terrain_effect_count,
		"estimated_height_delta": estimated_height_delta,
		"estimated_lethal_target_count": estimated_lethal_target_count,
		"estimated_lethal_threat_target_count": estimated_lethal_threat_target_count,
		"estimated_lethal_target_ids": estimated_lethal_target_ids.duplicate(),
		"estimated_lethal_threat_target_ids": estimated_lethal_threat_target_ids.duplicate(),
		"estimated_control_target_ids": estimated_control_target_ids.duplicate(),
		"estimated_control_threat_target_ids": estimated_control_threat_target_ids.duplicate(),
		"estimated_friendly_fire_target_count": estimated_friendly_fire_target_count,
		"estimated_friendly_fire_damage": estimated_friendly_fire_damage,
		"estimated_friendly_control_target_count": estimated_friendly_control_target_count,
		"estimated_friendly_lethal_target_count": estimated_friendly_lethal_target_count,
		"estimated_chain_target_count": estimated_chain_target_count,
		"estimated_chain_enemy_target_count": estimated_chain_enemy_target_count,
		"estimated_chain_ally_target_count": estimated_chain_ally_target_count,
		"estimated_hit_rate_percent": estimated_hit_rate_percent,
		"save_estimates_by_target_id": save_estimates_by_target_id.duplicate(true),
		"special_profile_preview_facts": special_profile_preview_facts.duplicate(true),
		"target_numeric_summary": target_numeric_summary.duplicate(true),
		"friendly_fire_numeric_summary": friendly_fire_numeric_summary.duplicate(true),
		"friendly_fire_reject_reason": friendly_fire_reject_reason,
		"meteor_use_case": String(meteor_use_case),
		"high_priority_target_ids": high_priority_target_ids.duplicate(),
		"high_priority_reasons": high_priority_reasons.duplicate(true),
		"low_value_penalty_reason": low_value_penalty_reason,
		"attack_roll_modifier_breakdown": attack_roll_modifier_breakdown.duplicate(true),
		"hit_payoff_score": hit_payoff_score,
		"target_priority_score": target_priority_score,
		"friendly_fire_penalty_score": friendly_fire_penalty_score,
		"path_step_hit_count": path_step_hit_count,
		"path_step_unique_target_count": path_step_unique_target_count,
		"path_step_hit_counts_by_unit_id": path_step_hit_counts_by_unit_id.duplicate(true),
		"path_step_payoff_score": path_step_payoff_score,
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
		"position_current_distance": position_current_distance,
		"position_safe_distance": position_safe_distance,
		"position_objective_score": position_objective_score,
		"has_post_action_threat_projection": has_post_action_threat_projection,
		"projected_actor_coord": projected_actor_coord,
		"pre_action_threat_unit_ids": pre_action_threat_unit_ids.duplicate(),
		"pre_action_threat_count": pre_action_threat_count,
		"pre_action_threat_expected_damage": pre_action_threat_expected_damage,
		"pre_action_survival_margin": pre_action_survival_margin,
		"pre_action_is_lethal_survival_risk": pre_action_is_lethal_survival_risk,
		"post_action_remaining_threat_unit_ids": post_action_remaining_threat_unit_ids.duplicate(),
		"post_action_remaining_threat_count": post_action_remaining_threat_count,
		"post_action_remaining_threat_expected_damage": post_action_remaining_threat_expected_damage,
		"post_action_survival_margin": post_action_survival_margin,
		"post_action_is_lethal_survival_risk": post_action_is_lethal_survival_risk,
		"total_score": total_score,
	}
