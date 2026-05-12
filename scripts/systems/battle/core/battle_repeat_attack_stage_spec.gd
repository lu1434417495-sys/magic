class_name BattleRepeatAttackStageSpec
extends RefCounted

const BATTLE_REPEAT_ATTACK_STAGE_SPEC_SCRIPT = preload("res://scripts/systems/battle/core/battle_repeat_attack_stage_spec.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")

var stage_index: int = 0
var stage_count: int = 0
var skill_level: int = 0
var stage_base_attack_bonus: int = 0
var follow_up_attack_penalty: int = 0
var penalty_free_stages: int = 0
var exponential_penalty: bool = false
var fate_aware: bool = false
var stage_label: StringName = &""


static func from_repeat_attack_effect(
	repeat_attack_effect: CombatEffectDef,
	stage_index_value: int,
	stage_count_value: int,
	skill_level_value: int,
	fate_aware_value: bool = false
):
	var spec := BATTLE_REPEAT_ATTACK_STAGE_SPEC_SCRIPT.new()
	spec.stage_index = maxi(stage_index_value, 0)
	spec.stage_count = maxi(stage_count_value, 0)
	spec.skill_level = maxi(skill_level_value, 0)
	spec.fate_aware = fate_aware_value
	spec.stage_label = StringName("repeat_stage_%d" % spec.stage_index)
	if repeat_attack_effect == null or repeat_attack_effect.params == null:
		return spec
	var params: Dictionary = repeat_attack_effect.params
	spec.stage_base_attack_bonus = int(params.get("base_attack_bonus", 0))
	spec.follow_up_attack_penalty = maxi(int(params.get("follow_up_attack_penalty", 0)), 0)
	spec.exponential_penalty = bool(params.get("exponential_penalty", false))
	spec.penalty_free_stages = _resolve_penalty_free_stages(params, spec.skill_level)
	return spec


func resolve_stage_attack_penalty() -> int:
	if stage_index < penalty_free_stages:
		return 0
	if exponential_penalty:
		return int(pow(2, stage_index)) * follow_up_attack_penalty
	return maxi(stage_index, 0) * follow_up_attack_penalty


static func _resolve_penalty_free_stages(params: Dictionary, skill_level: int) -> int:
	var level_stages_map: Dictionary = params.get("penalty_free_stages_by_level", {})
	if level_stages_map.is_empty():
		return 0
	var resolved_stages := 0
	var best_level := -1
	for level_key in level_stages_map.keys():
		var level_value := int(level_key)
		if level_value <= skill_level and level_value > best_level:
			best_level = level_value
			resolved_stages = int(level_stages_map.get(level_key, 0))
	return maxi(resolved_stages, 0)
