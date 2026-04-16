## 文件说明：该脚本属于战斗命中解析器相关的解析脚本，集中收敛当前命中率合成与 deterministic 掷骰口径。
## 审查重点：重点核对属性取值、命中率裁剪、seed/nonce 递增以及 repeat_attack 调用方是否仍保持单一 owner。
## 备注：后续若把普通单体技能也接入 miss 判定，应继续扩这个解析器，而不是把公式散回 runtime 或技能侧车。

class_name BattleHitResolver
extends RefCounted

const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attribute_service.gd")
const BattleState = preload("res://scripts/systems/battle_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle_unit_state.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")


func resolve_repeat_attack_stage_hit(
	battle_state: BattleState,
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	repeat_attack_effect: CombatEffectDef,
	stage_index: int
) -> Dictionary:
	var stage_penalty := 0
	var base_hit_rate_bonus := 0
	if repeat_attack_effect != null and repeat_attack_effect.params != null:
		base_hit_rate_bonus = int(repeat_attack_effect.params.get("base_hit_rate", 0))
		stage_penalty = maxi(stage_index, 0) * int(repeat_attack_effect.params.get("follow_up_hit_rate_penalty", 0))
	var hit_rate_percent := build_skill_hit_rate(active_unit, target_unit, skill_def, base_hit_rate_bonus, stage_penalty)
	return roll_hit_rate(battle_state, hit_rate_percent)


func build_skill_hit_rate(
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	flat_bonus: int = 0,
	flat_penalty: int = 0
) -> int:
	var source_hit_rate := 0
	if active_unit != null and active_unit.attribute_snapshot != null:
		source_hit_rate = active_unit.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.HIT_RATE)
	var target_evasion := 0
	if target_unit != null and target_unit.attribute_snapshot != null:
		target_evasion = target_unit.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.EVASION)
	var skill_hit_bonus := int(skill_def.combat_profile.hit_rate) if skill_def != null and skill_def.combat_profile != null else 0
	return clampi(source_hit_rate + skill_hit_bonus + flat_bonus - target_evasion - flat_penalty, 0, 100)


func roll_hit_rate(battle_state: BattleState, hit_rate_percent: int) -> Dictionary:
	var clamped_hit_rate := clampi(hit_rate_percent, 0, 100)
	if clamped_hit_rate <= 0:
		return {
			"success": false,
			"roll": 100,
			"hit_rate_percent": clamped_hit_rate,
		}
	if clamped_hit_rate >= 100:
		return {
			"success": true,
			"roll": 1,
			"hit_rate_percent": clamped_hit_rate,
		}

	var roll := _roll_battle_percent(battle_state)
	return {
		"success": roll <= clamped_hit_rate,
		"roll": roll,
		"hit_rate_percent": clamped_hit_rate,
	}


func _roll_battle_percent(battle_state: BattleState) -> int:
	if battle_state == null:
		return 1

	var nonce := maxi(int(battle_state.attack_roll_nonce), 0)
	var roll_seed_source := "%s:%d:%d" % [String(battle_state.battle_id), int(battle_state.seed), nonce]
	var rng := RandomNumberGenerator.new()
	rng.seed = int(roll_seed_source.hash())
	battle_state.attack_roll_nonce = nonce + 1
	return rng.randi_range(1, 100)
