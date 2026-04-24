## 文件说明：该脚本属于战斗命中解析器相关的解析脚本，集中收敛当前 BAB/降序 AC/d20 命中检定与 deterministic 掷骰口径。
## 审查重点：重点核对 attack bonus / AC 检定值、required roll / 命中预览、seed/nonce 递增以及 repeat_attack 调用方是否仍保持单一 owner。
## 备注：普通单体攻击与 repeat attack 都必须通过这里生成命中预览，不要把 D20 vs AC 公式散回 runtime 或技能侧车。

class_name BattleHitResolver
extends RefCounted

const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attribute_service.gd")
const BATTLE_FATE_ATTACK_RULES_SCRIPT = preload("res://scripts/systems/battle_fate_attack_rules.gd")
const BattleState = preload("res://scripts/systems/battle_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle_unit_state.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const FATE_ATTACK_FORMULA_SCRIPT = preload("res://scripts/systems/fate_attack_formula.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")

const DEFAULT_REPEAT_ATTACK_PREVIEW_STAGE_COUNT := 3
const REPEAT_ATTACK_PREVIEW_STAGE_GUARD := 32
const ATTACK_CHECK_TARGET := 21
const NATURAL_MISS_ROLL := 1
const NATURAL_HIT_ROLL := 20
const ROLL_DISPOSITION_THRESHOLD_HIT: StringName = &"threshold_hit"
const ROLL_DISPOSITION_THRESHOLD_MISS: StringName = &"threshold_miss"
const ROLL_DISPOSITION_NATURAL_AUTO_MISS: StringName = &"natural_1_auto_miss"
const ROLL_DISPOSITION_NATURAL_AUTO_HIT: StringName = &"natural_20_auto_hit"
const STATUS_BLACK_STAR_BRAND_ELITE: StringName = &"black_star_brand_elite"
const STATUS_CROWN_BREAK_BROKEN_HAND: StringName = &"crown_break_broken_hand"
const STATUS_CROWN_BREAK_BLINDED_EYE: StringName = &"crown_break_blinded_eye"
const STATUS_DODGE_BONUS_UP: StringName = &"dodge_bonus_up"
const BLACK_STAR_BRAND_ATTACK_BONUS_DELTA := -3

var _fate_attack_rules = BATTLE_FATE_ATTACK_RULES_SCRIPT.new()


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
	var base_attack_bonus := 0
	if repeat_attack_effect != null and repeat_attack_effect.params != null:
		base_attack_bonus = int(repeat_attack_effect.params.get("base_attack_bonus", 0))
		stage_penalty = maxi(stage_index, 0) * int(repeat_attack_effect.params.get("follow_up_attack_penalty", 0))
	return build_skill_attack_check(active_unit, target_unit, skill_def, base_attack_bonus, stage_penalty)


func build_fate_aware_repeat_attack_stage_hit_check(
	battle_state: BattleState,
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	repeat_attack_effect: CombatEffectDef,
	stage_index: int
) -> Dictionary:
	var base_attack_check := build_repeat_attack_stage_hit_check(
		active_unit,
		target_unit,
		skill_def,
		repeat_attack_effect,
		stage_index
	)
	return _build_fate_aware_attack_check_preview(
		battle_state,
		active_unit,
		target_unit,
		base_attack_check
	)


func build_repeat_attack_preview(
	battle_state: BattleState,
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	repeat_attack_effect: CombatEffectDef,
	preview_stage_count: int = -1
) -> Dictionary:
	if active_unit == null or target_unit == null or skill_def == null or repeat_attack_effect == null:
		return {}

	var resolved_stage_count := preview_stage_count
	if resolved_stage_count <= 0:
		resolved_stage_count = _resolve_repeat_attack_preview_stage_count(active_unit, skill_def, repeat_attack_effect)
	var normalized_stage_count := mini(maxi(resolved_stage_count, 1), REPEAT_ATTACK_PREVIEW_STAGE_GUARD)
	var stage_checks: Array[Dictionary] = []
	var stage_hit_rates: Array[int] = []
	var stage_base_hit_rates: Array[int] = []
	var stage_required_rolls: Array[int] = []
	var stage_preview_texts: Array[String] = []
	for stage_index in range(normalized_stage_count):
		var attack_check := build_fate_aware_repeat_attack_stage_hit_check(
			battle_state,
			active_unit,
			target_unit,
			skill_def,
			repeat_attack_effect,
			stage_index
		)
		stage_checks.append(attack_check.duplicate(true))
		stage_hit_rates.append(int(attack_check.get("hit_rate_percent", 0)))
		stage_base_hit_rates.append(int(attack_check.get("base_hit_rate_percent", 0)))
		stage_required_rolls.append(int(attack_check.get("display_required_roll", 20)))
		stage_preview_texts.append(String(attack_check.get("preview_text", "")))
	return {
		"summary_text": _format_repeat_attack_preview_summary(stage_checks),
		"stage_checks": stage_checks,
		"stage_hit_rates": stage_hit_rates,
		"stage_success_rates": stage_hit_rates.duplicate(),
		"stage_base_hit_rates": stage_base_hit_rates,
		"stage_required_rolls": stage_required_rolls,
		"stage_preview_texts": stage_preview_texts,
		"hit_rate_percent": int(round(float(_average_ints(stage_hit_rates)))),
		"base_hit_rate_percent": int(round(float(_average_ints(stage_base_hit_rates)))),
		"base_attack_bonus": int(repeat_attack_effect.params.get("base_attack_bonus", 0)) if repeat_attack_effect.params != null else 0,
		"follow_up_attack_penalty": int(repeat_attack_effect.params.get("follow_up_attack_penalty", 0)) if repeat_attack_effect.params != null else 0,
	}


func build_skill_attack_preview(
	battle_state: BattleState,
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	force_hit_no_crit: bool = false
) -> Dictionary:
	if active_unit == null or target_unit == null or skill_def == null:
		return {}
	if force_hit_no_crit:
		return build_force_hit_no_crit_attack_preview()
	var attack_check := _build_fate_aware_attack_check_preview(
		battle_state,
		active_unit,
		target_unit,
		build_skill_attack_check(active_unit, target_unit, skill_def)
	)
	var hit_rate := int(attack_check.get("hit_rate_percent", 0))
	var base_hit_rate := int(attack_check.get("base_hit_rate_percent", hit_rate))
	var preview_text := String(attack_check.get("preview_text", ""))
	return {
		"summary_text": "预计命中率 %s" % preview_text,
		"stage_checks": [attack_check.duplicate(true)],
		"stage_hit_rates": [hit_rate],
		"stage_success_rates": [hit_rate],
		"stage_base_hit_rates": [base_hit_rate],
		"stage_required_rolls": [int(attack_check.get("display_required_roll", NATURAL_HIT_ROLL))],
		"stage_preview_texts": [preview_text],
		"hit_rate_percent": hit_rate,
		"success_rate_percent": hit_rate,
		"base_hit_rate_percent": base_hit_rate,
	}


func build_force_hit_no_crit_attack_preview() -> Dictionary:
	var preview_text := "100%（必定命中；禁暴击）"
	var attack_check := {
		"required_roll": NATURAL_MISS_ROLL,
		"display_required_roll": NATURAL_MISS_ROLL,
		"hit_rate_percent": 100,
		"success_rate_percent": 100,
		"base_hit_rate_percent": 100,
		"force_hit_no_crit": true,
		"crit_locked": true,
		"natural_one_auto_miss": false,
		"natural_twenty_auto_hit": false,
		"preview_text": preview_text,
	}
	return {
		"summary_text": "预计命中率 %s" % preview_text,
		"stage_checks": [attack_check.duplicate(true)],
		"stage_hit_rates": [100],
		"stage_success_rates": [100],
		"stage_base_hit_rates": [100],
		"stage_required_rolls": [NATURAL_MISS_ROLL],
		"stage_preview_texts": [preview_text],
		"hit_rate_percent": 100,
		"success_rate_percent": 100,
		"base_hit_rate_percent": 100,
		"force_hit_no_crit": true,
		"crit_locked": true,
	}


func build_skill_attack_check(
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	flat_bonus: int = 0,
	flat_penalty: int = 0
) -> Dictionary:
	var attacker_attack_bonus := _get_unit_attribute_value(active_unit, ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 0)
	var target_armor_class := _get_target_armor_class(target_unit)
	var skill_attack_bonus := int(skill_def.combat_profile.attack_roll_bonus) if skill_def != null and skill_def.combat_profile != null else 0
	var status_attack_bonus_delta := _get_attacker_status_attack_bonus_delta(active_unit)
	var situational_attack_bonus := flat_bonus + maxi(status_attack_bonus_delta, 0)
	var situational_attack_penalty := flat_penalty + maxi(-status_attack_bonus_delta, 0)
	var required_roll := target_armor_class \
		- attacker_attack_bonus \
		- skill_attack_bonus \
		- situational_attack_bonus \
		+ situational_attack_penalty
	var hit_rate_percent := _compute_hit_rate_percent(required_roll)
	var attack_check := {
		"attacker_attack_bonus": attacker_attack_bonus,
		"attacker_bab": attacker_attack_bonus,
		"target_armor_class": target_armor_class,
		"skill_attack_bonus": skill_attack_bonus,
		"situational_attack_bonus": situational_attack_bonus,
		"situational_attack_penalty": situational_attack_penalty,
		"required_roll": required_roll,
		"display_required_roll": _get_display_required_roll(required_roll),
		"hit_rate_percent": hit_rate_percent,
		"natural_one_auto_miss": true,
		"natural_twenty_auto_hit": true,
	}
	attack_check["preview_text"] = format_attack_check_preview(attack_check)
	return attack_check


func _get_unit_attribute_value(unit_state: BattleUnitState, attribute_id: StringName, fallback: int = 0) -> int:
	if unit_state == null or unit_state.attribute_snapshot == null:
		return fallback
	if not unit_state.attribute_snapshot.has_value(attribute_id):
		return fallback
	return unit_state.attribute_snapshot.get_value(attribute_id)


func _get_target_armor_class(target_unit: BattleUnitState) -> int:
	var target_armor_class := _get_unit_attribute_value(target_unit, ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 10)
	if _is_target_dodge_bonus_locked(target_unit):
		target_armor_class -= maxi(_get_unit_attribute_value(target_unit, ATTRIBUTE_SERVICE_SCRIPT.DODGE_BONUS, 0), 0)
	else:
		target_armor_class += _get_target_status_dodge_bonus(target_unit)
	return maxi(target_armor_class, 1)


func _get_target_status_dodge_bonus(target_unit: BattleUnitState) -> int:
	if target_unit == null:
		return 0
	var status_entry = target_unit.get_status_effect(STATUS_DODGE_BONUS_UP)
	if status_entry == null:
		return 0
	return maxi(maxi(int(status_entry.power), int(status_entry.stacks)), 1) * 2


func _get_attacker_status_attack_bonus_delta(active_unit: BattleUnitState) -> int:
	if active_unit == null:
		return 0
	if active_unit.has_status_effect(STATUS_BLACK_STAR_BRAND_ELITE):
		return BLACK_STAR_BRAND_ATTACK_BONUS_DELTA
	return 0


func _is_target_dodge_bonus_locked(target_unit: BattleUnitState) -> bool:
	return target_unit != null and (
		target_unit.has_status_effect(STATUS_CROWN_BREAK_BLINDED_EYE)
		or _unit_has_status_bool_param(target_unit, &"lock_dodge_bonus")
	)


func _unit_has_status_bool_param(unit_state: BattleUnitState, param_key: StringName) -> bool:
	if unit_state == null or param_key == &"":
		return false
	for status_id_variant in unit_state.status_effects.keys():
		var status_id := ProgressionDataUtils.to_string_name(status_id_variant)
		var status_entry = unit_state.get_status_effect(status_id)
		if status_entry == null or status_entry.params == null:
			continue
		var params: Dictionary = status_entry.params
		if bool(params.get(String(param_key), params.get(param_key, false))):
			return true
	return false


func roll_attack_check(battle_state: BattleState, attack_check: Dictionary) -> Dictionary:
	if attack_check.is_empty():
		var empty_disposition := _resolve_attack_roll_disposition(NATURAL_MISS_ROLL, ATTACK_CHECK_TARGET)
		return {
			"success": false,
			"roll": NATURAL_MISS_ROLL,
			"required_roll": ATTACK_CHECK_TARGET,
			"display_required_roll": _get_display_required_roll(ATTACK_CHECK_TARGET),
			"hit_rate_percent": 0,
			"natural_one_auto_miss": true,
			"natural_twenty_auto_hit": true,
			"roll_disposition": empty_disposition,
			"preview_text": format_attack_check_preview({}),
			"resolution_text": format_attack_check_resolution({
				"roll": NATURAL_MISS_ROLL,
				"required_roll": ATTACK_CHECK_TARGET,
				"roll_disposition": empty_disposition,
			}),
		}
	var roll := _roll_battle_d20(battle_state)
	var required_roll := int(attack_check.get("required_roll", ATTACK_CHECK_TARGET))
	var roll_disposition := _resolve_attack_roll_disposition(roll, required_roll)
	var result: Dictionary = attack_check.duplicate(true)
	result["roll"] = roll
	result["roll_disposition"] = roll_disposition
	result["success"] = _is_attack_roll_disposition_success(roll_disposition)
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
	var roll := int(attack_result.get("roll", NATURAL_MISS_ROLL))
	var roll_disposition := StringName(
		attack_result.get("roll_disposition", _resolve_attack_roll_disposition(
			roll,
			int(attack_result.get("required_roll", ATTACK_CHECK_TARGET))
		))
	)
	match roll_disposition:
		ROLL_DISPOSITION_NATURAL_AUTO_MISS:
			return "%s，d20=%d（天然 1 失手）" % [preview_text, roll]
		ROLL_DISPOSITION_NATURAL_AUTO_HIT:
			return "%s，d20=%d（天然 20 命中）" % [preview_text, roll]
		_:
			return "%s，d20=%d" % [preview_text, roll]


func _roll_battle_d20(battle_state: BattleState) -> int:
	if battle_state == null:
		return NATURAL_MISS_ROLL

	var nonce := maxi(int(battle_state.attack_roll_nonce), 0)
	var roll_seed_source := "%s:%d:%d" % [String(battle_state.battle_id), int(battle_state.seed), nonce]
	var rng := RandomNumberGenerator.new()
	rng.seed = int(roll_seed_source.hash())
	battle_state.attack_roll_nonce = nonce + 1
	return rng.randi_range(NATURAL_MISS_ROLL, NATURAL_HIT_ROLL)


func _resolve_repeat_attack_preview_stage_count(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	repeat_attack_effect: CombatEffectDef
) -> int:
	if active_unit == null or skill_def == null or skill_def.combat_profile == null or repeat_attack_effect == null:
		return DEFAULT_REPEAT_ATTACK_PREVIEW_STAGE_COUNT
	if active_unit.has_status_effect(STATUS_CROWN_BREAK_BROKEN_HAND):
		return 1

	var params: Dictionary = repeat_attack_effect.params if repeat_attack_effect.params is Dictionary else {}
	var cost_resource := StringName(params.get("cost_resource", "aura"))
	var base_cost := _get_repeat_attack_preview_base_cost(skill_def, cost_resource)
	if base_cost <= 0:
		return REPEAT_ATTACK_PREVIEW_STAGE_GUARD

	var follow_up_cost_multiplier := maxf(float(params.get("follow_up_cost_multiplier", 1.0)), 1.0)
	var remaining_resource := _get_unit_resource_value(active_unit, cost_resource)
	if remaining_resource < base_cost:
		return 1

	remaining_resource -= base_cost
	var stages := 1
	while stages < REPEAT_ATTACK_PREVIEW_STAGE_GUARD:
		var next_stage_cost := maxi(int(round(float(base_cost) * pow(follow_up_cost_multiplier, stages))), 0)
		if next_stage_cost > 0 and remaining_resource < next_stage_cost:
			break
		remaining_resource -= next_stage_cost
		stages += 1
	return stages


func _get_repeat_attack_preview_base_cost(skill_def: SkillDef, cost_resource: StringName) -> int:
	match cost_resource:
		&"mp":
			return int(skill_def.combat_profile.mp_cost)
		&"stamina":
			return int(skill_def.combat_profile.stamina_cost)
		&"ap":
			return int(skill_def.combat_profile.ap_cost)
		_:
			return int(skill_def.combat_profile.aura_cost)


func _get_unit_resource_value(active_unit: BattleUnitState, cost_resource: StringName) -> int:
	match cost_resource:
		&"mp":
			return int(active_unit.current_mp)
		&"stamina":
			return int(active_unit.current_stamina)
		&"ap":
			return int(active_unit.current_ap)
		_:
			return int(active_unit.current_aura)


func _format_repeat_attack_preview_summary(stage_checks: Array[Dictionary]) -> String:
	if stage_checks.is_empty():
		return ""
	var parts: PackedStringArray = []
	for stage_check in stage_checks:
		parts.append(String(stage_check.get("preview_text", format_attack_check_preview(stage_check))))
	return "预计命中率 %s" % " -> ".join(parts)


func _build_fate_aware_attack_check_preview(
	battle_state: BattleState,
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	attack_check: Dictionary
) -> Dictionary:
	var resolved_check: Dictionary = attack_check.duplicate(true)
	var base_hit_rate_percent := int(attack_check.get("hit_rate_percent", 0))
	resolved_check["base_hit_rate_percent"] = base_hit_rate_percent
	if battle_state == null or active_unit == null or target_unit == null:
		resolved_check["preview_text"] = format_attack_check_preview(attack_check)
		return resolved_check

	var is_disadvantage := battle_state.is_attack_disadvantage(active_unit, target_unit)
	var hidden_luck_at_birth := _get_hidden_luck_at_birth(active_unit)
	var faith_luck_bonus := _get_faith_luck_bonus(active_unit)
	var effective_luck := clampi(
		hidden_luck_at_birth + faith_luck_bonus,
		UNIT_BASE_ATTRIBUTES_SCRIPT.EFFECTIVE_LUCK_MIN,
		UNIT_BASE_ATTRIBUTES_SCRIPT.EFFECTIVE_LUCK_MAX
	)
	var crit_gate_die := FATE_ATTACK_FORMULA_SCRIPT.calc_crit_gate_die_size(effective_luck, is_disadvantage)
	var crit_threshold := FATE_ATTACK_FORMULA_SCRIPT.calc_crit_threshold(hidden_luck_at_birth, faith_luck_bonus)
	var fumble_low_end := FATE_ATTACK_FORMULA_SCRIPT.calc_fumble_low_end(effective_luck)
	var crit_locked := _fate_attack_rules.is_attack_crit_locked(active_unit)
	var success_rate_percent := _compute_fate_attack_success_rate_percent(
		attack_check,
		crit_locked,
		crit_gate_die,
		crit_threshold,
		fumble_low_end,
		is_disadvantage
	)
	resolved_check["hit_rate_percent"] = success_rate_percent
	resolved_check["success_rate_percent"] = success_rate_percent
	resolved_check["is_disadvantage"] = is_disadvantage
	resolved_check["effective_luck"] = effective_luck
	resolved_check["crit_gate_die"] = crit_gate_die
	resolved_check["crit_threshold"] = crit_threshold
	resolved_check["fumble_low_end"] = fumble_low_end
	resolved_check["crit_locked"] = crit_locked
	resolved_check["preview_text"] = _format_fate_aware_attack_check_preview(resolved_check)
	return resolved_check


func _format_fate_aware_attack_check_preview(attack_check: Dictionary) -> String:
	var success_rate_percent := int(attack_check.get("success_rate_percent", attack_check.get("hit_rate_percent", 0)))
	var required_roll_text := _format_required_roll_text(int(attack_check.get("required_roll", ATTACK_CHECK_TARGET)))
	var base_hit_rate_percent := int(attack_check.get("base_hit_rate_percent", success_rate_percent))
	if success_rate_percent <= base_hit_rate_percent:
		return "%d%%（%s）" % [success_rate_percent, required_roll_text]
	var crit_locked := bool(attack_check.get("crit_locked", false))
	var crit_gate_die := int(attack_check.get("crit_gate_die", NATURAL_HIT_ROLL))
	if not crit_locked and crit_gate_die == NATURAL_HIT_ROLL:
		return "%d%%（%s；高位大成功 %d-20 直达）" % [
			success_rate_percent,
			required_roll_text,
			int(attack_check.get("crit_threshold", NATURAL_HIT_ROLL)),
		]
	if not crit_locked and crit_gate_die > NATURAL_HIT_ROLL:
		return "%d%%（%s；含门骰 d%d）" % [
			success_rate_percent,
			required_roll_text,
			crit_gate_die,
		]
	return "%d%%（%s）" % [success_rate_percent, required_roll_text]


func _compute_fate_attack_success_rate_percent(
	attack_check: Dictionary,
	crit_locked: bool,
	crit_gate_die: int,
	crit_threshold: int,
	fumble_low_end: int,
	is_disadvantage: bool
) -> int:
	var basis_points := _compute_fate_attack_success_rate_basis_points(
		attack_check,
		crit_locked,
		crit_gate_die,
		crit_threshold,
		fumble_low_end,
		is_disadvantage
	)
	return clampi(int(round(float(basis_points) / 100.0)), 0, 100)


func _compute_fate_attack_success_rate_basis_points(
	attack_check: Dictionary,
	crit_locked: bool,
	crit_gate_die: int,
	crit_threshold: int,
	fumble_low_end: int,
	is_disadvantage: bool
) -> float:
	var d20_success_basis_points := _compute_d20_attack_success_rate_basis_points(
		attack_check,
		crit_locked,
		crit_gate_die,
		crit_threshold,
		fumble_low_end,
		is_disadvantage
	)
	if crit_locked or crit_gate_die <= NATURAL_HIT_ROLL:
		return d20_success_basis_points
	var gate_crit_basis_points := 10000.0 / float(crit_gate_die)
	if is_disadvantage:
		gate_crit_basis_points /= float(crit_gate_die)
	return gate_crit_basis_points + (10000.0 - gate_crit_basis_points) * d20_success_basis_points / 10000.0


func _compute_d20_attack_success_rate_basis_points(
	attack_check: Dictionary,
	crit_locked: bool,
	crit_gate_die: int,
	crit_threshold: int,
	fumble_low_end: int,
	is_disadvantage: bool
) -> float:
	var success_outcomes := 0
	var total_outcomes := NATURAL_HIT_ROLL
	if not is_disadvantage:
		for roll in range(NATURAL_MISS_ROLL, NATURAL_HIT_ROLL + 1):
			if _is_d20_attack_success_roll(roll, attack_check, crit_locked, crit_gate_die, crit_threshold, fumble_low_end):
				success_outcomes += 1
		return float(success_outcomes) * 10000.0 / float(total_outcomes)
	total_outcomes *= NATURAL_HIT_ROLL
	for first_roll in range(NATURAL_MISS_ROLL, NATURAL_HIT_ROLL + 1):
		for second_roll in range(NATURAL_MISS_ROLL, NATURAL_HIT_ROLL + 1):
			var roll := mini(first_roll, second_roll)
			if _is_d20_attack_success_roll(roll, attack_check, crit_locked, crit_gate_die, crit_threshold, fumble_low_end):
				success_outcomes += 1
	return float(success_outcomes) * 10000.0 / float(total_outcomes)


func _is_d20_attack_success_roll(
	roll: int,
	attack_check: Dictionary,
	crit_locked: bool,
	crit_gate_die: int,
	crit_threshold: int,
	fumble_low_end: int
) -> bool:
	if roll <= fumble_low_end:
		return false
	if _fate_attack_rules.is_high_threat_crit_roll(roll, crit_locked, crit_gate_die, crit_threshold):
		return true
	return _fate_attack_rules.does_attack_roll_hit(roll, attack_check)


func _get_hidden_luck_at_birth(unit_state: BattleUnitState) -> int:
	if unit_state == null or unit_state.attribute_snapshot == null:
		return 0
	return int(unit_state.attribute_snapshot.get_value(UNIT_BASE_ATTRIBUTES_SCRIPT.HIDDEN_LUCK_AT_BIRTH))


func _get_faith_luck_bonus(unit_state: BattleUnitState) -> int:
	if unit_state == null or unit_state.attribute_snapshot == null:
		return 0
	return int(unit_state.attribute_snapshot.get_value(UNIT_BASE_ATTRIBUTES_SCRIPT.FAITH_LUCK_BONUS))


func _average_ints(values: Array[int]) -> float:
	if values.is_empty():
		return 0.0
	var total := 0
	for value in values:
		total += int(value)
	return float(total) / float(values.size())


func _compute_hit_rate_percent(required_roll: int) -> int:
	var success_count := 0
	for roll in range(NATURAL_MISS_ROLL, NATURAL_HIT_ROLL + 1):
		if _is_attack_roll_success(roll, required_roll):
			success_count += 1
	return success_count * 5


func _is_attack_roll_success(roll: int, required_roll: int) -> bool:
	return _is_attack_roll_disposition_success(_resolve_attack_roll_disposition(roll, required_roll))


func _resolve_attack_roll_disposition(roll: int, required_roll: int) -> StringName:
	if roll <= NATURAL_MISS_ROLL:
		return ROLL_DISPOSITION_NATURAL_AUTO_MISS
	if roll >= NATURAL_HIT_ROLL:
		return ROLL_DISPOSITION_NATURAL_AUTO_HIT
	if roll >= required_roll:
		return ROLL_DISPOSITION_THRESHOLD_HIT
	return ROLL_DISPOSITION_THRESHOLD_MISS


func _is_attack_roll_disposition_success(roll_disposition: StringName) -> bool:
	return roll_disposition == ROLL_DISPOSITION_THRESHOLD_HIT or roll_disposition == ROLL_DISPOSITION_NATURAL_AUTO_HIT


func _get_display_required_roll(required_roll: int) -> int:
	return clampi(required_roll, NATURAL_MISS_ROLL + 1, NATURAL_HIT_ROLL)


func _format_required_roll_text(required_roll: int) -> String:
	var display_required_roll := _get_display_required_roll(required_roll)
	if required_roll <= NATURAL_MISS_ROLL + 1:
		return "需 %d+（天然 1 仍失手）" % display_required_roll
	if required_roll > NATURAL_HIT_ROLL:
		return "需 %d+（仅天然 20）" % display_required_roll
	return "需 %d+" % display_required_roll


func _get_required_roll_for_hit_rate(hit_rate_percent: int) -> int:
	var clamped_hit_rate := clampi(hit_rate_percent, 0, 100)
	var successful_rolls := int(ceil(float(clamped_hit_rate) / 5.0))
	return ATTACK_CHECK_TARGET - successful_rolls
