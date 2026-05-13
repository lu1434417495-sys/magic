class_name WaitAction
extends "res://scripts/enemies/enemy_ai_action.gd"

const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")

const BASIC_ATTACK_SKILL_ID: StringName = &"basic_attack"
const TU_GRANULARITY := 5
const STAMINA_RECOVERY_PROGRESS_BASE := 11
const STAMINA_RECOVERY_PROGRESS_DENOMINATOR := 10
const STAMINA_RESTING_RECOVERY_MULTIPLIER := 2

@export var active_rest_action_base_score := 10
@export var active_rest_min_stamina_residue := 1


func decide(context):
	AI_TRACE_RECORDER.enter(&"decide:wait")
	var result = _decide_impl(context)
	AI_TRACE_RECORDER.exit(&"decide:wait")
	return result


func _decide_impl(context):
	var active_rest_profile := _build_active_rest_profile(context)
	var action_trace := _begin_action_trace(context, {
		"action_kind": "wait",
		"active_rest": bool(active_rest_profile.get("active", false)),
		"will_rest": bool(active_rest_profile.get("will_rest", false)),
		"current_stamina": int(active_rest_profile.get("current_stamina", 0)),
		"projected_rest_stamina": int(active_rest_profile.get("projected_rest_stamina", 0)),
		"desired_stamina": int(active_rest_profile.get("desired_stamina", 0)),
	})
	var command = _build_wait_command(context)
	var metadata := {
		"position_objective_kind": &"none",
	}
	if bool(active_rest_profile.get("active", false)):
		metadata["action_base_score"] = active_rest_action_base_score
		metadata["active_rest"] = true
	var score_input = _build_action_score_input(
		context,
		&"wait",
		String(action_id),
		command,
		null,
		metadata
	)
	var reason_text := "%s 没有更优动作，选择待机。" % [context.unit_state.display_name]
	if bool(active_rest_profile.get("active", false)):
		reason_text = "%s 体力不足，选择主动休息以恢复到 %d/%d。" % [
			context.unit_state.display_name,
			int(active_rest_profile.get("projected_rest_stamina", 0)),
			int(active_rest_profile.get("stamina_max", 0)),
		]
	elif bool(active_rest_profile.get("will_rest", false)):
		reason_text = "%s 没有更优动作，选择休息恢复体力。" % context.unit_state.display_name
	var decision = _create_scored_decision(
		command,
		score_input,
		reason_text
	)
	_trace_offer_candidate(action_trace, _build_candidate_summary("wait", command, score_input))
	_finalize_action_trace(context, action_trace, decision)
	return decision


func validate_schema() -> Array[String]:
	var errors := _collect_base_validation_errors()
	if active_rest_action_base_score < -1000:
		errors.append("WaitAction %s active_rest_action_base_score is unexpectedly low." % String(action_id))
	if active_rest_min_stamina_residue < 0:
		errors.append("WaitAction %s active_rest_min_stamina_residue must be >= 0." % String(action_id))
	return errors


func _build_active_rest_profile(context) -> Dictionary:
	var profile := {
		"active": false,
		"will_rest": false,
		"current_stamina": 0,
		"projected_rest_stamina": 0,
		"desired_stamina": 0,
		"stamina_max": 0,
	}
	if context == null or context.unit_state == null:
		return profile
	var unit_state: BattleUnitState = context.unit_state
	var stamina_max := _get_unit_stamina_max(unit_state)
	var current_stamina := maxi(int(unit_state.current_stamina), 0)
	profile["current_stamina"] = current_stamina
	profile["stamina_max"] = stamina_max
	profile["will_rest"] = _will_wait_trigger_rest(unit_state, current_stamina, stamina_max)
	if stamina_max <= 0 or current_stamina >= stamina_max:
		profile["projected_rest_stamina"] = current_stamina
		return profile
	if bool(unit_state.has_taken_action_this_turn):
		profile["projected_rest_stamina"] = current_stamina
		return profile
	if _has_affordable_legal_hostile_skill(context):
		profile["projected_rest_stamina"] = current_stamina
		return profile
	var desired_stamina := _resolve_desired_rest_stamina(context)
	profile["desired_stamina"] = desired_stamina
	if desired_stamina <= 0 or current_stamina >= desired_stamina:
		profile["projected_rest_stamina"] = current_stamina
		return profile
	var projected_stamina := mini(
		current_stamina + _estimate_resting_recovery(unit_state, _resolve_action_threshold_tu(unit_state)),
		stamina_max
	)
	profile["projected_rest_stamina"] = projected_stamina
	profile["active"] = projected_stamina >= desired_stamina
	return profile


func _will_wait_trigger_rest(unit_state: BattleUnitState, current_stamina: int, stamina_max: int) -> bool:
	if unit_state == null:
		return false
	if bool(unit_state.has_taken_action_this_turn):
		return false
	return stamina_max > 0 and current_stamina < stamina_max


func _has_affordable_legal_hostile_skill(context) -> bool:
	if context == null or context.unit_state == null:
		return false
	var unit_state: BattleUnitState = context.unit_state
	for raw_skill_id in unit_state.known_active_skill_ids:
		var skill_id := ProgressionDataUtils.to_string_name(raw_skill_id)
		var skill_def := _get_skill_def(context, skill_id)
		if skill_def == null or skill_def.combat_profile == null or not _is_hostile_threat_skill(skill_def):
			continue
		if not _can_pay_skill_cost(unit_state, skill_def):
			continue
		if _has_legal_unit_skill_target(context, skill_def):
			return true
	return false


func _has_legal_unit_skill_target(context, skill_def: SkillDef) -> bool:
	if context == null or skill_def == null or skill_def.combat_profile == null:
		return false
	if skill_def.combat_profile.target_mode != &"unit":
		return false
	for target_unit in _sort_target_units(context, &"enemy", &"nearest_enemy"):
		var command = _build_unit_skill_command(context, skill_def.skill_id, target_unit)
		var preview = context.preview_command(command)
		if preview != null and bool(preview.allowed):
			return true
	return false


func _can_pay_skill_cost(unit_state: BattleUnitState, skill_def: SkillDef) -> bool:
	if unit_state == null or skill_def == null or skill_def.combat_profile == null:
		return false
	var costs: Dictionary = skill_def.combat_profile.get_effective_resource_costs(_get_skill_level(unit_state, skill_def.skill_id))
	if not _get_locked_combat_resource_block_reason(unit_state, costs).is_empty():
		return false
	return int(unit_state.current_ap) >= int(costs.get("ap_cost", skill_def.combat_profile.ap_cost)) \
		and int(unit_state.current_mp) >= int(costs.get("mp_cost", skill_def.combat_profile.mp_cost)) \
		and int(unit_state.current_stamina) >= int(costs.get("stamina_cost", skill_def.combat_profile.stamina_cost)) \
		and int(unit_state.current_aura) >= int(costs.get("aura_cost", skill_def.combat_profile.aura_cost))


func _resolve_desired_rest_stamina(context) -> int:
	if context == null or context.unit_state == null:
		return 0
	var unit_state: BattleUnitState = context.unit_state
	var desired_cost := _get_skill_stamina_cost(context, BASIC_ATTACK_SKILL_ID)
	for raw_skill_id in unit_state.known_active_skill_ids:
		var skill_id := ProgressionDataUtils.to_string_name(raw_skill_id)
		var skill_def := _get_skill_def(context, skill_id)
		if skill_def == null or skill_def.combat_profile == null or not _is_hostile_threat_skill(skill_def):
			continue
		var stamina_cost := _get_skill_stamina_cost(context, skill_id)
		if stamina_cost <= 0:
			continue
		if desired_cost <= 0:
			desired_cost = stamina_cost
		else:
			desired_cost = mini(desired_cost, stamina_cost)
	if desired_cost <= 0:
		return 0
	return desired_cost + active_rest_min_stamina_residue


func _get_skill_stamina_cost(context, skill_id: StringName) -> int:
	var skill_def := _get_skill_def(context, skill_id)
	if skill_def == null or skill_def.combat_profile == null:
		return 0
	var skill_level := _get_skill_level(context.unit_state, skill_id) if context != null and context.unit_state != null else 1
	var costs := skill_def.combat_profile.get_effective_resource_costs(maxi(skill_level, 1))
	return maxi(int(costs.get("stamina_cost", skill_def.combat_profile.stamina_cost)), 0)


func _estimate_resting_recovery(unit_state: BattleUnitState, tu_delta: int) -> int:
	if unit_state == null or tu_delta <= 0:
		return 0
	var tick_count := maxi(int(tu_delta / TU_GRANULARITY), 0)
	if tick_count <= 0:
		return 0
	var progress_gain_per_tick := STAMINA_RECOVERY_PROGRESS_BASE + _get_unit_constitution(unit_state)
	progress_gain_per_tick = _apply_stamina_recovery_percent_bonus(unit_state, progress_gain_per_tick)
	progress_gain_per_tick *= STAMINA_RESTING_RECOVERY_MULTIPLIER
	var progress := maxi(int(unit_state.stamina_recovery_progress), 0)
	var recovered := 0
	for _tick_index in range(tick_count):
		progress += progress_gain_per_tick
		recovered += int(progress / STAMINA_RECOVERY_PROGRESS_DENOMINATOR)
		progress %= STAMINA_RECOVERY_PROGRESS_DENOMINATOR
	return recovered


func _resolve_action_threshold_tu(unit_state: BattleUnitState) -> int:
	if unit_state == null:
		return 30
	return maxi(int(unit_state.action_threshold), 1)


func _get_unit_constitution(unit_state: BattleUnitState) -> int:
	if unit_state == null or unit_state.attribute_snapshot == null:
		return 0
	return maxi(int(unit_state.attribute_snapshot.get_value(UNIT_BASE_ATTRIBUTES_SCRIPT.CONSTITUTION)), 0)


func _get_unit_stamina_max(unit_state: BattleUnitState) -> int:
	if unit_state == null or unit_state.attribute_snapshot == null:
		return 0
	return maxi(int(unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX)), 0)


func _apply_stamina_recovery_percent_bonus(unit_state: BattleUnitState, base_progress_gain: int) -> int:
	if unit_state == null or unit_state.attribute_snapshot == null:
		return base_progress_gain
	var percent_bonus := maxi(int(unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_RECOVERY_PERCENT_BONUS)), 0)
	if percent_bonus <= 0:
		return base_progress_gain
	return int(floor(float(base_progress_gain) * float(100 + percent_bonus) / 100.0))
