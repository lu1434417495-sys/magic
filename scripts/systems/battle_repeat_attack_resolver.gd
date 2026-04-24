class_name BattleRepeatAttackResolver
extends RefCounted

const REPEAT_ATTACK_EFFECT_TYPE: StringName = &"repeat_attack_until_fail"
const REPEAT_ATTACK_STAGE_GUARD := 32
const BATTLE_HIT_RESOLVER_SCRIPT = preload("res://scripts/systems/battle_hit_resolver.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const BattleUnitState = preload("res://scripts/systems/battle_unit_state.gd")
const BattleEventBatch = preload("res://scripts/systems/battle_event_batch.gd")
const ProgressionDataUtils = preload("res://scripts/player/progression/progression_data_utils.gd")

var _runtime_ref: WeakRef = null
var _runtime = null:
	get:
		return _runtime_ref.get_ref() if _runtime_ref != null else null
	set(value):
		_runtime_ref = weakref(value) if value != null else null


func setup(runtime) -> void:
	_runtime = runtime


func dispose() -> void:
	_runtime = null


func apply_repeat_attack_skill_result(
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef],
	repeat_attack_effect: CombatEffectDef,
	batch: BattleEventBatch
) -> bool:
	var staged_effects := collect_repeat_attack_base_effects(effect_defs)
	if active_unit == null or target_unit == null or skill_def == null or repeat_attack_effect == null or staged_effects.is_empty():
		return false

	var total_damage := 0
	var total_healing := 0
	var total_kill_count := 0
	var stage_index := 0
	var executed := false

	while stage_index < REPEAT_ATTACK_STAGE_GUARD and target_unit.is_alive:
		if stage_index > 0 and _runtime != null and _runtime.is_unit_follow_up_locked(active_unit):
			batch.log_lines.append("%s 受折手封印影响，无法继续追击。" % active_unit.display_name)
			break
		var stage_aura_cost: int = _get_repeat_attack_stage_cost(skill_def, repeat_attack_effect, stage_index)
		if stage_index > 0:
			if not _can_pay_repeat_attack_stage_cost(active_unit, repeat_attack_effect, stage_aura_cost):
				batch.log_lines.append("%s 的 %s 在第 %d 段前斗气不足，连斩中止。" % [
					active_unit.display_name,
					skill_def.display_name,
					stage_index + 1,
				])
				break
			_consume_repeat_attack_stage_cost(active_unit, repeat_attack_effect, stage_aura_cost)
			_runtime.append_changed_unit_id(batch, active_unit.unit_id)

		var stage_damage_multiplier: float = _get_repeat_attack_stage_damage_multiplier(repeat_attack_effect, stage_index)
		var stage_effects := _build_repeat_attack_stage_effects(staged_effects, repeat_attack_effect, stage_damage_multiplier)
		var stage_result := _resolve_repeat_attack_stage_result(
			active_unit,
			target_unit,
			skill_def,
			repeat_attack_effect,
			stage_index,
			stage_effects
		)
		var stage_hit_rate: int = int(stage_result.get("hit_rate_percent", 0))
		var stage_resolution_text := String(stage_result.get("resolution_text", "%d%%" % stage_hit_rate))
		executed = true
		if not bool(stage_result.get("attack_success", false)):
			batch.log_lines.append("%s 的 %s 第 %d 段未命中 %s，%s，AU 消耗 %d。" % [
				active_unit.display_name,
				skill_def.display_name,
				stage_index + 1,
				target_unit.display_name,
				stage_resolution_text,
				stage_aura_cost,
			])
			_runtime.append_result_report_entry(batch, stage_result)
			if _should_stop_repeat_attack_on_miss(repeat_attack_effect):
				break
			stage_index += 1
			continue

		_runtime.mark_applied_statuses_for_turn_timing(target_unit, stage_result.get("status_effect_ids", []))
		_runtime.append_result_source_status_effects(batch, active_unit, stage_result)
		_runtime.append_changed_unit_id(batch, target_unit.unit_id)
		_runtime.append_changed_unit_coords(batch, target_unit)

		var damage := int(stage_result.get("damage", 0))
		var healing := int(stage_result.get("healing", 0))
		total_damage += damage
		total_healing += healing
		_runtime.append_damage_result_log_lines(
			batch,
			"%s 的 %s 第 %d 段，倍率 x%s，AU 消耗 %d，%s" % [
				active_unit.display_name,
				skill_def.display_name,
				stage_index + 1,
				_format_runtime_multiplier(stage_damage_multiplier),
				stage_aura_cost,
				stage_resolution_text,
			],
			target_unit.display_name,
			stage_result
		)
		_runtime.append_result_report_entry(batch, stage_result)
		if healing > 0:
			batch.log_lines.append("%s 的 %s 第 %d 段为 %s 恢复 %d 点生命。" % [
				active_unit.display_name,
				skill_def.display_name,
				stage_index + 1,
				target_unit.display_name,
				healing,
			])
		for status_id in stage_result.get("status_effect_ids", []):
			batch.log_lines.append("%s 获得状态 %s。" % [target_unit.display_name, String(status_id)])

		if not target_unit.is_alive:
			total_kill_count += 1
			_runtime.clear_defeated_unit(target_unit, batch)
			batch.log_lines.append("%s 被击倒。" % target_unit.display_name)
			_runtime.get_battle_rating_system().record_enemy_defeated_achievement(active_unit, target_unit)
			if _should_stop_repeat_attack_on_target_down(repeat_attack_effect):
				break

		stage_index += 1

	if stage_index >= REPEAT_ATTACK_STAGE_GUARD and target_unit.is_alive:
		batch.log_lines.append("%s 的 %s 达到内部连斩保护上限后被强制中止。" % [
			active_unit.display_name,
			skill_def.display_name,
		])

	if total_damage > 0 or total_healing > 0 or total_kill_count > 0:
		_runtime.record_skill_effect_result(active_unit, total_damage, total_healing, total_kill_count)
	return executed


func get_repeat_attack_effect_def(effect_defs: Array[CombatEffectDef]) -> CombatEffectDef:
	for effect_def in effect_defs:
		if effect_def != null and effect_def.effect_type == REPEAT_ATTACK_EFFECT_TYPE:
			return effect_def
	return null


func collect_repeat_attack_base_effects(effect_defs: Array[CombatEffectDef]) -> Array[CombatEffectDef]:
	var staged_effects: Array[CombatEffectDef] = []
	for effect_def in effect_defs:
		if _runtime.is_unit_effect(effect_def):
			staged_effects.append(effect_def)
	return staged_effects


func _resolve_repeat_attack_stage_result(
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	repeat_attack_effect: CombatEffectDef,
	stage_index: int,
	stage_effects: Array[CombatEffectDef]
) -> Dictionary:
	var hit_resolver = _runtime.get_hit_resolver() if _has_runtime() and _runtime.get_hit_resolver() != null else BATTLE_HIT_RESOLVER_SCRIPT.new()
	var battle_state = _runtime.get_state() if _has_runtime() else null
	var attack_check: Dictionary = hit_resolver.build_fate_aware_repeat_attack_stage_hit_check(
		battle_state,
		active_unit,
		target_unit,
		skill_def,
		repeat_attack_effect,
		stage_index
	)
	var damage_resolver = _runtime.get_damage_resolver() if _has_runtime() else null
	var result: Dictionary = damage_resolver.resolve_attack_effects(
		active_unit,
		target_unit,
		stage_effects,
		attack_check,
		{"battle_state": battle_state}
	) if damage_resolver != null else {
		"attack_success": false,
		"hit_rate_percent": int(attack_check.get("hit_rate_percent", 0)),
	}
	result["hit_rate_percent"] = int(attack_check.get("hit_rate_percent", result.get("hit_rate_percent", 0)))
	result["success_rate_percent"] = int(attack_check.get("success_rate_percent", result.get("hit_rate_percent", 0)))
	result["resolution_text"] = _format_repeat_attack_stage_resolution_text(attack_check, result)
	return result


func _format_repeat_attack_stage_resolution_text(attack_check: Dictionary, attack_result: Dictionary) -> String:
	var preview_text := String(attack_check.get("preview_text", "%d%%" % int(attack_check.get("hit_rate_percent", 0))))
	var attack_resolution := ProgressionDataUtils.to_string_name(attack_result.get("attack_resolution", ""))
	var hit_roll := int(attack_result.get("hit_roll", 0))
	if hit_roll > 0:
		match attack_resolution:
			&"critical_hit":
				return "%s，d20=%d（大成功）" % [preview_text, hit_roll]
			&"critical_fail":
				return "%s，d20=%d（大失败）" % [preview_text, hit_roll]
			&"miss":
				return "%s，d20=%d（未命中）" % [preview_text, hit_roll]
			_:
				return "%s，d20=%d" % [preview_text, hit_roll]
	var crit_gate_die := int(attack_result.get("crit_gate_die", 0))
	var crit_gate_roll := int(attack_result.get("crit_gate_roll", 0))
	if crit_gate_die > 0 and crit_gate_roll > 0:
		return "%s，门骰 d%d=%d" % [preview_text, crit_gate_die, crit_gate_roll]
	return preview_text


func _get_repeat_attack_stage_cost(
	skill_def: SkillDef,
	repeat_attack_effect: CombatEffectDef,
	stage_index: int
) -> int:
	var base_cost := _get_repeat_attack_base_resource_cost(skill_def, repeat_attack_effect)
	if stage_index <= 0:
		return base_cost
	var follow_up_cost_multiplier := maxf(float(repeat_attack_effect.params.get("follow_up_cost_multiplier", 1.0)), 1.0)
	return maxi(int(round(float(base_cost) * pow(follow_up_cost_multiplier, stage_index))), 0)


func _get_repeat_attack_base_resource_cost(skill_def: SkillDef, repeat_attack_effect: CombatEffectDef) -> int:
	if skill_def == null or skill_def.combat_profile == null or repeat_attack_effect == null:
		return 0
	match ProgressionDataUtils.to_string_name(repeat_attack_effect.params.get("cost_resource", "aura")):
		&"mp":
			return int(skill_def.combat_profile.mp_cost)
		&"stamina":
			return int(skill_def.combat_profile.stamina_cost)
		&"ap":
			return int(skill_def.combat_profile.ap_cost)
		_:
			return int(skill_def.combat_profile.aura_cost)


func _can_pay_repeat_attack_stage_cost(
	active_unit: BattleUnitState,
	repeat_attack_effect: CombatEffectDef,
	stage_cost: int
) -> bool:
	if active_unit == null or repeat_attack_effect == null:
		return false
	if stage_cost <= 0:
		return true
	match ProgressionDataUtils.to_string_name(repeat_attack_effect.params.get("cost_resource", "aura")):
		&"mp":
			return active_unit.current_mp >= stage_cost
		&"stamina":
			return active_unit.current_stamina >= stage_cost
		&"ap":
			return active_unit.current_ap >= stage_cost
		_:
			return active_unit.current_aura >= stage_cost


func _consume_repeat_attack_stage_cost(
	active_unit: BattleUnitState,
	repeat_attack_effect: CombatEffectDef,
	stage_cost: int
) -> void:
	if active_unit == null or repeat_attack_effect == null or stage_cost <= 0:
		return
	match ProgressionDataUtils.to_string_name(repeat_attack_effect.params.get("cost_resource", "aura")):
		&"mp":
			active_unit.current_mp = maxi(active_unit.current_mp - stage_cost, 0)
		&"stamina":
			active_unit.current_stamina = maxi(active_unit.current_stamina - stage_cost, 0)
		&"ap":
			active_unit.current_ap = maxi(active_unit.current_ap - stage_cost, 0)
		_:
			active_unit.current_aura = maxi(active_unit.current_aura - stage_cost, 0)
func _should_stop_repeat_attack_on_miss(repeat_attack_effect: CombatEffectDef) -> bool:
	return bool(repeat_attack_effect.params.get("stop_on_miss", true))


func _should_stop_repeat_attack_on_target_down(repeat_attack_effect: CombatEffectDef) -> bool:
	return bool(repeat_attack_effect.params.get("stop_on_target_down", true))


func _get_repeat_attack_stage_damage_multiplier(repeat_attack_effect: CombatEffectDef, stage_index: int) -> float:
	if repeat_attack_effect == null or stage_index <= 0:
		return 1.0
	var follow_up_damage_multiplier := maxf(float(repeat_attack_effect.params.get("follow_up_damage_multiplier", 1.0)), 1.0)
	return pow(follow_up_damage_multiplier, stage_index)


func _build_repeat_attack_stage_effects(
	base_effects: Array[CombatEffectDef],
	repeat_attack_effect: CombatEffectDef,
	damage_multiplier: float
) -> Array[CombatEffectDef]:
	var staged_effects: Array[CombatEffectDef] = []
	var damage_multiplier_stage := String(repeat_attack_effect.params.get("damage_multiplier_stage", "pre_resistance"))
	for effect_def in base_effects:
		if effect_def == null:
			continue
		var stage_effect := effect_def.duplicate(true) as CombatEffectDef
		if stage_effect == null:
			continue
		if stage_effect.params == null:
			stage_effect.params = {}
		else:
			stage_effect.params = stage_effect.params.duplicate(true)
		if stage_effect.effect_type == &"damage" and damage_multiplier_stage == "pre_resistance" and damage_multiplier > 1.0:
			stage_effect.params["runtime_pre_resistance_damage_multiplier"] = damage_multiplier
		staged_effects.append(stage_effect)
	return staged_effects


func _format_runtime_multiplier(multiplier: float) -> String:
	if is_equal_approx(multiplier, round(multiplier)):
		return str(int(round(multiplier)))
	return str(snappedf(multiplier, 0.01))


func _has_runtime() -> bool:
	return _runtime != null
