## 文件说明：该脚本属于战斗命中解析器相关的解析脚本，集中收敛当前 BAB/降序 AC/d20 命中检定与 deterministic 掷骰口径。
## 审查重点：重点核对旧字段到攻击检定步进值的换算、required roll / 命中预览、seed/nonce 递增以及 repeat_attack 调用方是否仍保持单一 owner。
## 备注：后续若把普通单体技能也接入 miss 判定，应继续扩这个解析器，而不是把公式散回 runtime 或技能侧车。

class_name BattleHitResolver
extends RefCounted

const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attribute_service.gd")
const BattleState = preload("res://scripts/systems/battle_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle_unit_state.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")

const DEFAULT_REPEAT_ATTACK_PREVIEW_STAGE_COUNT := 3
const ATTACK_CHECK_TARGET := 21
const NATURAL_MISS_ROLL := 1
const NATURAL_HIT_ROLL := 20
const ATTACK_BONUS_STEP := 5.0


func resolve_repeat_attack_stage_hit(
	battle_state: BattleState,
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	repeat_attack_effect: CombatEffectDef,
	stage_index: int
) -> Dictionary:
	var attack_check := build_repeat_attack_stage_hit_check(
		active_unit,
		target_unit,
		skill_def,
		repeat_attack_effect,
		stage_index
	)
	return roll_attack_check(battle_state, attack_check)


func build_repeat_attack_stage_hit_check(
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
	return build_skill_attack_check(active_unit, target_unit, skill_def, base_hit_rate_bonus, stage_penalty)


func build_repeat_attack_preview(
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	repeat_attack_effect: CombatEffectDef,
	preview_stage_count: int = DEFAULT_REPEAT_ATTACK_PREVIEW_STAGE_COUNT
) -> Dictionary:
	if active_unit == null or target_unit == null or skill_def == null or repeat_attack_effect == null:
		return {}

	var normalized_stage_count := maxi(preview_stage_count, 1)
	var stage_checks: Array[Dictionary] = []
	var stage_hit_rates: Array[int] = []
	var stage_required_rolls: Array[int] = []
	var stage_preview_texts: Array[String] = []
	for stage_index in range(normalized_stage_count):
		var attack_check := build_repeat_attack_stage_hit_check(
			active_unit,
			target_unit,
			skill_def,
			repeat_attack_effect,
			stage_index
		)
		stage_checks.append(attack_check.duplicate(true))
		stage_hit_rates.append(int(attack_check.get("hit_rate_percent", 0)))
		stage_required_rolls.append(int(attack_check.get("display_required_roll", 20)))
		stage_preview_texts.append(String(attack_check.get("preview_text", "")))
	return {
		"summary_text": _format_repeat_attack_preview_summary(stage_checks),
		"stage_checks": stage_checks,
		"stage_hit_rates": stage_hit_rates,
		"stage_required_rolls": stage_required_rolls,
		"stage_preview_texts": stage_preview_texts,
		"base_hit_rate_bonus": int(repeat_attack_effect.params.get("base_hit_rate", 0)) if repeat_attack_effect.params != null else 0,
		"follow_up_hit_rate_penalty": int(repeat_attack_effect.params.get("follow_up_hit_rate_penalty", 0)) if repeat_attack_effect.params != null else 0,
	}


func build_skill_attack_check(
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	flat_bonus: int = 0,
	flat_penalty: int = 0
) -> Dictionary:
	var source_hit_rate := 0
	if active_unit != null and active_unit.attribute_snapshot != null:
		source_hit_rate = active_unit.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.HIT_RATE)
	var target_evasion := 0
	if target_unit != null and target_unit.attribute_snapshot != null:
		target_evasion = target_unit.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.EVASION)
	var skill_hit_bonus := int(skill_def.combat_profile.hit_rate) if skill_def != null and skill_def.combat_profile != null else 0
	var attacker_bab := _convert_legacy_hit_rate_to_bab(source_hit_rate)
	var target_armor_class := _convert_legacy_evasion_to_armor_class(target_evasion)
	var skill_attack_bonus := _convert_legacy_percent_to_attack_bonus(skill_hit_bonus)
	var situational_attack_bonus := _convert_legacy_percent_to_attack_bonus(flat_bonus)
	var situational_attack_penalty := _convert_legacy_percent_to_attack_bonus(flat_penalty)
	var required_roll := ATTACK_CHECK_TARGET \
		- attacker_bab \
		- target_armor_class \
		- skill_attack_bonus \
		- situational_attack_bonus \
		+ situational_attack_penalty
	var hit_rate_percent := _compute_hit_rate_percent(required_roll)
	var attack_check := {
		"attacker_bab": attacker_bab,
		"target_armor_class": target_armor_class,
		"skill_attack_bonus": skill_attack_bonus,
		"situational_attack_bonus": situational_attack_bonus,
		"situational_attack_penalty": situational_attack_penalty,
		"required_roll": required_roll,
		"display_required_roll": _get_display_required_roll(required_roll),
		"hit_rate_percent": hit_rate_percent,
	}
	attack_check["preview_text"] = format_attack_check_preview(attack_check)
	return attack_check


func roll_attack_check(battle_state: BattleState, attack_check: Dictionary) -> Dictionary:
	if attack_check.is_empty():
		return {
			"success": false,
			"roll": NATURAL_MISS_ROLL,
			"required_roll": ATTACK_CHECK_TARGET,
			"display_required_roll": _get_display_required_roll(ATTACK_CHECK_TARGET),
			"hit_rate_percent": 0,
			"preview_text": format_attack_check_preview({}),
			"resolution_text": format_attack_check_resolution({"roll": NATURAL_MISS_ROLL}),
		}
	var roll := _roll_battle_d20(battle_state)
	var required_roll := int(attack_check.get("required_roll", ATTACK_CHECK_TARGET))
	var result: Dictionary = attack_check.duplicate(true)
	result["roll"] = roll
	result["success"] = _is_attack_roll_success(roll, required_roll)
	result["resolution_text"] = format_attack_check_resolution(result)
	return result


func roll_hit_rate(battle_state: BattleState, hit_rate_percent: int) -> Dictionary:
	var synthetic_required_roll := _get_required_roll_for_hit_rate(clampi(hit_rate_percent, 0, 100))
	return roll_attack_check(
		battle_state,
		{
			"required_roll": synthetic_required_roll,
			"display_required_roll": _get_display_required_roll(synthetic_required_roll),
			"hit_rate_percent": _compute_hit_rate_percent(synthetic_required_roll),
			"preview_text": format_attack_check_preview({
				"required_roll": synthetic_required_roll,
				"display_required_roll": _get_display_required_roll(synthetic_required_roll),
				"hit_rate_percent": _compute_hit_rate_percent(synthetic_required_roll),
			}),
		}
	)


func format_attack_check_preview(attack_check: Dictionary) -> String:
	var hit_rate_percent := int(attack_check.get("hit_rate_percent", 0))
	var required_roll := int(attack_check.get("required_roll", ATTACK_CHECK_TARGET))
	return "%d%%（%s）" % [hit_rate_percent, _format_required_roll_text(required_roll)]


func format_attack_check_resolution(attack_result: Dictionary) -> String:
	var preview_text := String(attack_result.get("preview_text", format_attack_check_preview(attack_result)))
	return "%s，d20=%d" % [preview_text, int(attack_result.get("roll", NATURAL_MISS_ROLL))]


func _roll_battle_d20(battle_state: BattleState) -> int:
	if battle_state == null:
		return NATURAL_MISS_ROLL

	var nonce := maxi(int(battle_state.attack_roll_nonce), 0)
	var roll_seed_source := "%s:%d:%d" % [String(battle_state.battle_id), int(battle_state.seed), nonce]
	var rng := RandomNumberGenerator.new()
	rng.seed = int(roll_seed_source.hash())
	battle_state.attack_roll_nonce = nonce + 1
	return rng.randi_range(NATURAL_MISS_ROLL, NATURAL_HIT_ROLL)


func _format_repeat_attack_preview_summary(stage_checks: Array[Dictionary]) -> String:
	if stage_checks.is_empty():
		return ""
	var parts: PackedStringArray = []
	for stage_check in stage_checks:
		parts.append(format_attack_check_preview(stage_check))
	return "预计命中率 %s" % " -> ".join(parts)


func _convert_legacy_hit_rate_to_bab(hit_rate: int) -> int:
	return _convert_legacy_percent_to_attack_bonus(hit_rate) - 10


func _convert_legacy_evasion_to_armor_class(evasion: int) -> int:
	return 10 - _convert_legacy_percent_to_attack_bonus(evasion)


func _convert_legacy_percent_to_attack_bonus(value: int) -> int:
	return int(round(float(value) / ATTACK_BONUS_STEP))


func _compute_hit_rate_percent(required_roll: int) -> int:
	var success_count := 0
	for roll in range(NATURAL_MISS_ROLL, NATURAL_HIT_ROLL + 1):
		if _is_attack_roll_success(roll, required_roll):
			success_count += 1
	return success_count * 5


func _is_attack_roll_success(roll: int, required_roll: int) -> bool:
	if roll <= NATURAL_MISS_ROLL:
		return false
	if roll >= NATURAL_HIT_ROLL:
		return true
	return roll >= required_roll


func _get_display_required_roll(required_roll: int) -> int:
	return clampi(required_roll, NATURAL_MISS_ROLL + 1, NATURAL_HIT_ROLL)


func _format_required_roll_text(required_roll: int) -> String:
	var display_required_roll := _get_display_required_roll(required_roll)
	if required_roll > NATURAL_HIT_ROLL:
		return "需 %d+（仅天然 20）" % display_required_roll
	return "需 %d+" % display_required_roll


func _get_required_roll_for_hit_rate(hit_rate_percent: int) -> int:
	var clamped_hit_rate := clampi(hit_rate_percent, 0, 100)
	var successful_rolls := int(ceil(float(clamped_hit_rate) / 5.0))
	return ATTACK_CHECK_TARGET - successful_rolls
