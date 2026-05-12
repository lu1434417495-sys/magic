class_name BattleAiScoreService
extends RefCounted

const BATTLE_AI_SCORE_INPUT_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_score_input.gd")
const BATTLE_AI_SCORE_PROFILE_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_score_profile.gd")
const BATTLE_DAMAGE_PREVIEW_RANGE_SERVICE_SCRIPT = preload("res://scripts/systems/battle/rules/battle_damage_preview_range_service.gd")
const BATTLE_SAVE_RESOLVER_SCRIPT = preload("res://scripts/systems/battle/rules/battle_save_resolver.gd")
const BATTLE_RANGE_SERVICE_SCRIPT = preload("res://scripts/systems/battle/rules/battle_range_service.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const BattleAiScoreInput = preload("res://scripts/systems/battle/ai/battle_ai_score_input.gd")
const BattleAiScoreProfile = preload("res://scripts/systems/battle/ai/battle_ai_score_profile.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")

const BONUS_CONDITION_TARGET_LOW_HP: StringName = &"target_low_hp"
const PATH_STEP_AOE_EFFECT_TYPE: StringName = &"path_step_aoe"
const CHAIN_DAMAGE_EFFECT_TYPE: StringName = &"chain_damage"
const THREAT_MULTIPLIER_BASIS_POINTS_DENOMINATOR := 10000
const MIN_RANGED_THREAT_RANGE := 3
const METEOR_SWARM_PROFILE_ID: StringName = &"meteor_swarm"
const FORTUNE_MARK_TARGET_STAT_ID: StringName = &"fortune_mark_target"
const BOSS_TARGET_STAT_ID: StringName = &"boss_target"

var _score_profile: BattleAiScoreProfile = BATTLE_AI_SCORE_PROFILE_SCRIPT.new()


func setup(_damage_resolver = null) -> void:
	pass


func set_profile(profile: BattleAiScoreProfile) -> void:
	_score_profile = profile if profile != null else BATTLE_AI_SCORE_PROFILE_SCRIPT.new()


func get_profile() -> BattleAiScoreProfile:
	return _score_profile


func get_bucket_priority(bucket_id: StringName) -> int:
	return _score_profile.get_bucket_priority(bucket_id) if _score_profile != null else 0


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
	score_input.action_kind = ProgressionDataUtils.to_string_name(metadata.get("action_kind", "skill"))
	score_input.action_label = String(metadata.get("action_label", skill_def.display_name if skill_def != null else ""))
	score_input.score_bucket_id = ProgressionDataUtils.to_string_name(metadata.get("score_bucket_id", ""))
	score_input.score_bucket_priority = get_bucket_priority(score_input.score_bucket_id)
	score_input.primary_coord = _resolve_primary_coord(command, preview)
	score_input.target_unit_ids = _copy_target_unit_ids(preview)
	score_input.target_coords = _copy_target_coords(preview)
	score_input.target_count = score_input.target_unit_ids.size()
	var effective_effect_defs := _filter_effect_defs_for_context(effect_defs, context, skill_def)
	_populate_hit_metrics(score_input, context, effective_effect_defs)
	_populate_special_profile_metrics(score_input, context)
	_populate_path_step_aoe_metrics(score_input, context, effective_effect_defs, metadata)
	_populate_resource_cost_metrics(score_input, skill_def, context)
	_populate_position_metrics(score_input, context, metadata)
	_populate_post_action_threat_projection(score_input, context, metadata)
	score_input.total_score = _resolve_action_base_score(score_input.action_kind, metadata) \
		+ score_input.hit_payoff_score \
		+ score_input.effective_target_count * _score_profile.target_count_weight \
		- score_input.resource_cost_score \
		+ score_input.position_objective_score
	return score_input


func build_action_score_input(
	context,
	action_kind: StringName,
	action_label: String,
	score_bucket_id: StringName,
	command,
	preview,
	metadata: Dictionary = {}
) -> BattleAiScoreInput:
	var score_input := BATTLE_AI_SCORE_INPUT_SCRIPT.new()
	score_input.command = command
	score_input.preview = preview
	score_input.action_kind = action_kind
	score_input.action_label = action_label
	score_input.score_bucket_id = score_bucket_id
	score_input.score_bucket_priority = get_bucket_priority(score_bucket_id)
	score_input.primary_coord = _resolve_primary_coord(command, preview)
	score_input.target_unit_ids = _copy_target_unit_ids(preview)
	score_input.target_coords = _copy_target_coords(preview)
	score_input.target_count = _resolve_action_target_count(score_input)
	score_input.move_cost = int(metadata.get("move_cost", preview.move_cost if preview != null else 0))
	_populate_position_metrics(score_input, context, metadata)
	_populate_post_action_threat_projection(score_input, context, metadata)
	score_input.resource_cost_score = maxi(score_input.move_cost, 0) * _score_profile.movement_cost_weight
	score_input.total_score = _resolve_action_base_score(action_kind, metadata) \
		+ score_input.position_objective_score \
		+ score_input.target_count * int(metadata.get("target_count_weight", 0)) \
		- score_input.resource_cost_score
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
	AiTraceRecorder.enter(&"_populate_hit_metrics")
	_populate_hit_metrics_impl(score_input, context, effect_defs)
	AiTraceRecorder.exit(&"_populate_hit_metrics")


func _populate_hit_metrics_impl(score_input: BattleAiScoreInput, context, effect_defs: Array) -> void:
	if score_input == null:
		return
	score_input.estimated_hit_rate_percent = _resolve_estimated_hit_rate_percent(score_input.preview)
	if context == null or context.state == null or context.unit_state == null:
		return
	for target_unit_id in score_input.target_unit_ids:
		var target_unit := context.state.units.get(target_unit_id) as BattleUnitState
		if target_unit == null:
			continue
		_populate_target_effect_metrics(score_input, context, target_unit, effect_defs)
	_populate_chain_damage_metrics(score_input, context, effect_defs)
	score_input.hit_payoff_score = int(round(
		float(score_input.hit_payoff_score) * float(score_input.estimated_hit_rate_percent) / 100.0
	))
	score_input.target_priority_score = int(round(
		float(score_input.target_priority_score) * float(score_input.estimated_hit_rate_percent) / 100.0
	))


func _populate_special_profile_metrics(score_input: BattleAiScoreInput, context) -> void:
	AiTraceRecorder.enter(&"_populate_special_profile_metrics")
	_populate_special_profile_metrics_impl(score_input, context)
	AiTraceRecorder.exit(&"_populate_special_profile_metrics")


func _populate_special_profile_metrics_impl(score_input: BattleAiScoreInput, context) -> void:
	if score_input == null or score_input.preview == null or score_input.preview.special_profile_preview_facts == null:
		return
	var facts = score_input.preview.special_profile_preview_facts
	var facts_payload: Dictionary = facts.to_dict() if facts.has_method("to_dict") else {}
	score_input.special_profile_preview_facts = facts_payload.duplicate(true)
	score_input.friendly_fire_numeric_summary = facts.get_friendly_fire_numeric_summary()
	score_input.attack_roll_modifier_breakdown = facts.attack_roll_modifier_breakdown.duplicate(true)
	var target_summaries_value = facts_payload.get("target_numeric_summary", [])
	if target_summaries_value is Array:
		for summary_variant in target_summaries_value:
			if summary_variant is Dictionary:
				score_input.target_numeric_summary.append((summary_variant as Dictionary).duplicate(true))
	score_input.estimated_terrain_effect_count += maxi(int(facts_payload.get("expected_terrain_effect_count", 0)), 0)
	if score_input.estimated_terrain_effect_count > 0:
		score_input.hit_payoff_score += score_input.estimated_terrain_effect_count * _score_profile.terrain_weight
	if context == null or context.state == null or context.unit_state == null:
		score_input.meteor_use_case = _resolve_meteor_use_case(score_input)
		return
	if score_input.target_numeric_summary.is_empty():
		_populate_special_profile_target_counts_without_numeric_summary(score_input, context)
	else:
		for summary in score_input.target_numeric_summary:
			_populate_special_profile_target_summary(score_input, context, summary)
	score_input.meteor_use_case = _resolve_meteor_use_case(score_input)


func _populate_special_profile_target_summary(score_input: BattleAiScoreInput, context, summary: Dictionary) -> void:
	var target_unit_id := ProgressionDataUtils.to_string_name(summary.get("target_unit_id", summary.get("ally_unit_id", "")))
	if target_unit_id == &"":
		return
	var target_unit := context.state.units.get(target_unit_id) as BattleUnitState
	if target_unit == null:
		return
	var estimated_damage := maxi(int(summary.get("component_expected_damage", 0)), 0)
	var worst_case_damage := maxi(int(summary.get("component_worst_case_damage", 0)), estimated_damage)
	var status_ids_value = summary.get("status_effect_ids", [])
	var status_count: int = status_ids_value.size() if status_ids_value is Array else 0
	var is_ally: bool = target_unit.faction_id == context.unit_state.faction_id
	score_input.estimated_damage += estimated_damage
	score_input.estimated_status_count += status_count
	score_input.estimated_control_count += status_count
	if is_ally:
		score_input.ally_target_count += 1
		score_input.estimated_ally_damage += estimated_damage
		_populate_special_profile_ally_risk(score_input, target_unit, summary, estimated_damage, worst_case_damage, status_count)
		return
	score_input.enemy_target_count += 1
	score_input.estimated_enemy_damage += estimated_damage
	if estimated_damage > 0 or status_count > 0 or score_input.estimated_terrain_effect_count > 0:
		score_input.effective_target_count += 1
	score_input.hit_payoff_score += estimated_damage * _score_profile.damage_weight
	score_input.hit_payoff_score += status_count * _score_profile.status_weight
	var target_priority_bonus := _resolve_target_role_threat_bonus(
		context,
		target_unit,
		estimated_damage,
		status_count,
		score_input.estimated_terrain_effect_count,
		0
	)
	score_input.target_priority_score += target_priority_bonus
	score_input.hit_payoff_score += target_priority_bonus
	var lethal_basis := maxi(estimated_damage, worst_case_damage if int(summary.get("lethal_probability_percent", 0)) > 0 else estimated_damage)
	var lethal_bonus := _resolve_lethal_target_bonus(score_input, context, target_unit, lethal_basis)
	score_input.target_priority_score += lethal_bonus
	score_input.hit_payoff_score += lethal_bonus
	_record_meteor_high_priority_target(
		score_input,
		context,
		target_unit,
		summary,
		target_priority_bonus + lethal_bonus
	)
	if status_count > 0:
		_append_unique_string_name(score_input.estimated_control_target_ids, target_unit.unit_id)
		if _is_priority_threat_target(context, target_unit):
			_append_unique_string_name(score_input.estimated_control_threat_target_ids, target_unit.unit_id)


func _populate_special_profile_ally_risk(
	score_input: BattleAiScoreInput,
	target_unit: BattleUnitState,
	summary: Dictionary,
	estimated_damage: int,
	worst_case_damage: int,
	status_count: int
) -> void:
	if estimated_damage <= 0 and worst_case_damage <= 0 and status_count <= 0:
		return
	score_input.estimated_friendly_fire_target_count += 1
	score_input.estimated_friendly_fire_damage += estimated_damage
	if status_count > 0:
		score_input.estimated_friendly_control_target_count += 1
	var is_lethal := worst_case_damage >= maxi(int(target_unit.current_hp), 1) \
		or int(summary.get("lethal_probability_percent", 0)) > 0
	var penalty := estimated_damage * _score_profile.friendly_fire_damage_weight \
		+ _score_profile.friendly_fire_target_weight \
		+ status_count * _score_profile.friendly_control_target_weight
	if is_lethal:
		score_input.estimated_friendly_lethal_target_count += 1
		penalty += _score_profile.friendly_lethal_target_weight
	var reject_reason := _resolve_meteor_friendly_fire_reject_reason(
		target_unit,
		summary,
		estimated_damage,
		worst_case_damage,
		status_count
	)
	if not reject_reason.is_empty() and score_input.friendly_fire_reject_reason.is_empty():
		score_input.friendly_fire_reject_reason = reject_reason
	score_input.friendly_fire_penalty_score += penalty
	score_input.hit_payoff_score -= penalty


func _populate_special_profile_target_counts_without_numeric_summary(score_input: BattleAiScoreInput, context) -> void:
	for target_unit_id in score_input.target_unit_ids:
		var target_unit := context.state.units.get(target_unit_id) as BattleUnitState
		if target_unit == null:
			continue
		if target_unit.faction_id == context.unit_state.faction_id:
			score_input.ally_target_count += 1
		else:
			score_input.enemy_target_count += 1
			score_input.effective_target_count += 1


func _resolve_meteor_use_case(score_input: BattleAiScoreInput) -> StringName:
	if score_input == null:
		return &""
	score_input.low_value_penalty_reason = ""
	if not score_input.friendly_fire_reject_reason.is_empty():
		return &"unsafe_friendly_fire"
	if _has_meteor_decapitation_target(score_input):
		return &"decapitation"
	if int(score_input.enemy_target_count) >= 3:
		return &"cluster"
	if _has_meteor_zone_denial(score_input):
		return &"zone_denial"
	score_input.low_value_penalty_reason = "no_cluster_decapitation_or_zone_denial"
	score_input.hit_payoff_score -= maxi(int(_score_profile.target_count_weight), 0)
	return &"impact"


func _record_meteor_high_priority_target(
	score_input: BattleAiScoreInput,
	context,
	target_unit: BattleUnitState,
	summary: Dictionary,
	target_priority_score: int
) -> void:
	if score_input == null or target_unit == null:
		return
	if not _is_meteor_score_input(score_input):
		return
	var reasons := _resolve_meteor_high_priority_reasons(context, target_unit, summary, target_priority_score)
	if reasons.is_empty():
		return
	_append_unique_string_name(score_input.high_priority_target_ids, target_unit.unit_id)
	score_input.high_priority_reasons[String(target_unit.unit_id)] = reasons


func _resolve_meteor_high_priority_reasons(
	context,
	target_unit: BattleUnitState,
	summary: Dictionary,
	target_priority_score: int
) -> Array[String]:
	var reasons: Array[String] = []
	if target_unit == null:
		return reasons
	if _is_meteor_elite_or_boss_target(target_unit):
		reasons.append("elite_or_boss")
	var role_summary := _resolve_target_role_summary(context, target_unit)
	var threat_multiplier := int(role_summary.get("threat_multiplier_bp", THREAT_MULTIPLIER_BASIS_POINTS_DENOMINATOR))
	if _target_has_high_threat_role(role_summary) \
			and threat_multiplier >= int(_score_profile.meteor_high_priority_threat_multiplier_bp):
		reasons.append("role_threat_multiplier")
	var center_direct_expected := _resolve_component_expected_damage(summary, &"center_direct")
	var max_hp := _get_unit_max_hp(target_unit)
	var center_direct_hp_percent := int(round(float(center_direct_expected) * 100.0 / float(maxi(max_hp, 1))))
	if center_direct_hp_percent >= int(_score_profile.meteor_high_priority_damage_hp_percent) \
			and _target_has_high_threat_role(role_summary):
		reasons.append("center_direct_high_role_damage")
	if target_priority_score >= int(_score_profile.meteor_high_priority_target_priority_score):
		reasons.append("target_priority_score")
	var threat_rank := _resolve_meteor_threat_rank(context, target_unit)
	if threat_rank > 0 and threat_rank <= maxi(int(_score_profile.meteor_top_threat_rank), 0):
		reasons.append("top_threat_rank")
	return reasons


func _resolve_component_expected_damage(summary: Dictionary, component_id: StringName) -> int:
	var component_breakdown_value = summary.get("component_breakdown", [])
	if component_breakdown_value is not Array:
		return 0
	for component_variant in component_breakdown_value:
		if component_variant is not Dictionary:
			continue
		var component := component_variant as Dictionary
		if ProgressionDataUtils.to_string_name(component.get("component_id", "")) == component_id:
			return maxi(int(component.get("expected_damage", 0)), 0)
	return 0


func _resolve_target_role_summary(context, target_unit: BattleUnitState) -> Dictionary:
	var summary := {
		"heal_skill_count": 0,
		"control_skill_count": 0,
		"best_ranged_attack_range": 0,
		"threat_multiplier_bp": _resolve_target_role_threat_multiplier_basis_points(context, target_unit),
	}
	if context == null or target_unit == null:
		return summary
	for skill_id in target_unit.known_active_skill_ids:
		var normalized_skill_id := ProgressionDataUtils.to_string_name(skill_id)
		if normalized_skill_id == &"":
			continue
		var skill_def = context.skill_defs.get(normalized_skill_id) as SkillDef
		if skill_def == null or skill_def.combat_profile == null:
			continue
		var role_effect_defs := _collect_role_threat_effect_defs(target_unit, skill_def)
		if _is_heal_or_support_skill(skill_def, role_effect_defs):
			summary["heal_skill_count"] = int(summary.get("heal_skill_count", 0)) + 1
		if _is_control_skill(role_effect_defs):
			summary["control_skill_count"] = int(summary.get("control_skill_count", 0)) + 1
		if _is_damage_skill(role_effect_defs):
			var effective_range := BATTLE_RANGE_SERVICE_SCRIPT.get_effective_skill_threat_range(target_unit, skill_def)
			if effective_range >= MIN_RANGED_THREAT_RANGE:
				summary["best_ranged_attack_range"] = maxi(int(summary.get("best_ranged_attack_range", 0)), effective_range)
	return summary


func _target_has_high_threat_role(role_summary: Dictionary) -> bool:
	if role_summary == null:
		return false
	return int(role_summary.get("heal_skill_count", 0)) > 0 \
		or int(role_summary.get("control_skill_count", 0)) > 0 \
		or int(role_summary.get("best_ranged_attack_range", 0)) >= MIN_RANGED_THREAT_RANGE


func _is_meteor_elite_or_boss_target(target_unit: BattleUnitState) -> bool:
	if target_unit == null or target_unit.attribute_snapshot == null:
		return false
	return int(target_unit.attribute_snapshot.get_value(BOSS_TARGET_STAT_ID)) > 0 \
		or int(target_unit.attribute_snapshot.get_value(FORTUNE_MARK_TARGET_STAT_ID)) > 0


func _resolve_meteor_threat_rank(context, target_unit: BattleUnitState) -> int:
	if context == null or context.state == null or context.unit_state == null or target_unit == null:
		return 0
	var enemies: Array[BattleUnitState] = []
	for unit_variant in context.state.units.values():
		var unit_state := unit_variant as BattleUnitState
		if unit_state == null or not unit_state.is_alive:
			continue
		if unit_state.faction_id == context.unit_state.faction_id:
			continue
		enemies.append(unit_state)
	enemies.sort_custom(func(left: BattleUnitState, right: BattleUnitState) -> bool:
		var left_multiplier := _resolve_target_role_threat_multiplier_basis_points(context, left)
		var right_multiplier := _resolve_target_role_threat_multiplier_basis_points(context, right)
		if left_multiplier != right_multiplier:
			return left_multiplier > right_multiplier
		var left_boss := 1 if _is_meteor_elite_or_boss_target(left) else 0
		var right_boss := 1 if _is_meteor_elite_or_boss_target(right) else 0
		if left_boss != right_boss:
			return left_boss > right_boss
		return String(left.unit_id) < String(right.unit_id)
	)
	for index in range(enemies.size()):
		if enemies[index] != null and enemies[index].unit_id == target_unit.unit_id:
			return index + 1
	return 0


func _has_meteor_decapitation_target(score_input: BattleAiScoreInput) -> bool:
	if score_input == null:
		return false
	for summary in score_input.target_numeric_summary:
		if summary == null or not _meteor_summary_has_center_direct(summary):
			continue
		var target_id := ProgressionDataUtils.to_string_name(summary.get("target_unit_id", ""))
		if score_input.high_priority_target_ids.has(target_id):
			return true
	return false


func _meteor_summary_has_center_direct(summary: Dictionary) -> bool:
	var component_breakdown_value = summary.get("component_breakdown", [])
	if component_breakdown_value is not Array:
		return false
	for component_variant in component_breakdown_value:
		if component_variant is Dictionary \
				and ProgressionDataUtils.to_string_name((component_variant as Dictionary).get("component_id", "")) == &"center_direct":
			return true
	return false


func _has_meteor_zone_denial(score_input: BattleAiScoreInput) -> bool:
	if score_input == null:
		return false
	if int(score_input.estimated_terrain_effect_count) <= 0:
		return false
	return int(score_input.enemy_target_count) > 0 or score_input.target_numeric_summary.is_empty()


func _resolve_meteor_friendly_fire_reject_reason(
	target_unit: BattleUnitState,
	summary: Dictionary,
	estimated_damage: int,
	worst_case_damage: int,
	status_count: int
) -> String:
	if target_unit == null:
		return ""
	var target_label := String(target_unit.unit_id)
	if _is_meteor_protected_ally(target_unit) and _meteor_summary_has_any_protected_ally_consequence(summary, estimated_damage, worst_case_damage, status_count):
		return "meteor_swarm_protected_ally:%s" % target_label
	var lethal_probability := int(summary.get("lethal_probability_percent", 0))
	if lethal_probability > 0 or worst_case_damage >= maxi(int(target_unit.current_hp), 1):
		return "meteor_swarm_friendly_fire_lethal:%s" % target_label
	if _score_profile != null and _score_profile.meteor_friendly_fire_profile != &"reckless":
		if int(summary.get("expected_damage_hp_percent", 0)) >= int(_score_profile.meteor_friendly_fire_hard_expected_hp_percent):
			return "meteor_swarm_friendly_fire_expected_threshold:%s" % target_label
		if int(summary.get("worst_case_damage_hp_percent", 0)) >= int(_score_profile.meteor_friendly_fire_hard_worst_case_hp_percent):
			return "meteor_swarm_friendly_fire_worst_threshold:%s" % target_label
	if bool(summary.get("hard_reject", false)):
		return "meteor_swarm_friendly_fire_hard_reject:%s" % target_label
	return ""


func _is_meteor_protected_ally(target_unit: BattleUnitState) -> bool:
	if target_unit == null:
		return false
	if bool(target_unit.ai_blackboard.get("meteor_protected_ally", false)) \
			or bool(target_unit.ai_blackboard.get("protected_ally", false)):
		return true
	return target_unit.attribute_snapshot != null \
		and int(target_unit.attribute_snapshot.get_value(&"protected_ally")) > 0


func _meteor_summary_has_any_protected_ally_consequence(
	summary: Dictionary,
	estimated_damage: int,
	worst_case_damage: int,
	status_count: int
) -> bool:
	if estimated_damage > 0 or worst_case_damage > 0 or status_count > 0:
		return true
	if int(summary.get("ap_penalty", 0)) > 0:
		return true
	var terrain_consequence = summary.get("hostile_terrain_consequence", {})
	if terrain_consequence is not Dictionary:
		return false
	var consequence := terrain_consequence as Dictionary
	return int(consequence.get("move_cost_delta", 0)) > 0 \
		or bool(consequence.get("creates_dust", false)) \
		or bool(consequence.get("creates_crater", false)) \
		or bool(consequence.get("creates_rubble", false))


func _is_meteor_score_input(score_input: BattleAiScoreInput) -> bool:
	if score_input == null:
		return false
	return ProgressionDataUtils.to_string_name(score_input.special_profile_preview_facts.get("profile_id", "")) == METEOR_SWARM_PROFILE_ID


func _get_unit_max_hp(unit_state: BattleUnitState) -> int:
	if unit_state == null:
		return 1
	if unit_state.attribute_snapshot != null:
		var max_hp := int(unit_state.attribute_snapshot.get_value(&"hp_max"))
		if max_hp > 0:
			return max_hp
	return maxi(int(unit_state.current_hp), 1)


func _populate_target_effect_metrics(
	score_input: BattleAiScoreInput,
	context,
	target_unit: BattleUnitState,
	effect_defs: Array,
	hit_count: int = 1,
	is_chain_target: bool = false
) -> void:
	AiTraceRecorder.enter(&"_populate_target_effect_metrics")
	_populate_target_effect_metrics_impl(score_input, context, target_unit, effect_defs, hit_count, is_chain_target)
	AiTraceRecorder.exit(&"_populate_target_effect_metrics")


func _populate_target_effect_metrics_impl(
	score_input: BattleAiScoreInput,
	context,
	target_unit: BattleUnitState,
	effect_defs: Array,
	hit_count: int = 1,
	is_chain_target: bool = false
) -> void:
	if score_input == null or context == null or context.unit_state == null or target_unit == null or hit_count <= 0:
		return
	var target_metrics := _build_target_effect_metrics(score_input.skill_def, context.unit_state, target_unit, effect_defs, hit_count)
	if bool(target_metrics.get("is_empty", true)):
		return
	var estimated_damage := int(target_metrics.get("damage", 0))
	var estimated_healing := int(target_metrics.get("healing", 0))
	var harmful_control_count := int(target_metrics.get("harmful_control_count", 0))
	var beneficial_control_count := int(target_metrics.get("beneficial_control_count", 0))
	var estimated_terrain_effect_count := int(target_metrics.get("terrain_effect_count", 0))
	var estimated_height_delta := int(target_metrics.get("height_delta", 0))
	var is_ally: bool = target_unit.faction_id == context.unit_state.faction_id
	_append_save_estimates_for_target(
		score_input,
		target_unit,
		target_metrics.get("save_estimates", [])
	)

	score_input.estimated_damage += estimated_damage
	score_input.estimated_healing += estimated_healing
	score_input.estimated_status_count += harmful_control_count + beneficial_control_count
	score_input.estimated_control_count += harmful_control_count + beneficial_control_count
	score_input.estimated_terrain_effect_count += estimated_terrain_effect_count
	score_input.estimated_height_delta += estimated_height_delta

	if is_chain_target:
		score_input.estimated_chain_target_count += 1
		if is_ally:
			score_input.estimated_chain_ally_target_count += 1
		else:
			score_input.estimated_chain_enemy_target_count += 1

	if is_ally:
		score_input.ally_target_count += 1
		score_input.estimated_ally_damage += estimated_damage
		score_input.estimated_ally_healing += estimated_healing
		_populate_ally_target_payoff(
			score_input,
			target_unit,
			estimated_damage,
			estimated_healing,
			harmful_control_count,
			beneficial_control_count
		)
		return

	score_input.enemy_target_count += 1
	score_input.estimated_enemy_damage += estimated_damage
	score_input.estimated_enemy_healing += estimated_healing
	_populate_enemy_target_payoff(
		score_input,
		context,
		target_unit,
		estimated_damage,
		estimated_healing,
		harmful_control_count,
		beneficial_control_count,
		estimated_terrain_effect_count,
		estimated_height_delta
	)


func _populate_enemy_target_payoff(
	score_input: BattleAiScoreInput,
	context,
	target_unit: BattleUnitState,
	estimated_damage: int,
	estimated_healing: int,
	harmful_control_count: int,
	beneficial_control_count: int,
	estimated_terrain_effect_count: int,
	estimated_height_delta: int
) -> void:
	var has_beneficial_enemy_effect := estimated_damage > 0 \
		or harmful_control_count > 0 \
		or estimated_terrain_effect_count > 0 \
		or estimated_height_delta > 0
	if has_beneficial_enemy_effect:
		score_input.effective_target_count += 1
	score_input.hit_payoff_score += estimated_damage * _score_profile.damage_weight
	score_input.hit_payoff_score -= estimated_healing * _score_profile.heal_weight
	score_input.hit_payoff_score += harmful_control_count * _score_profile.status_weight
	score_input.hit_payoff_score -= beneficial_control_count * _score_profile.status_weight
	score_input.hit_payoff_score += estimated_terrain_effect_count * _score_profile.terrain_weight
	score_input.hit_payoff_score += estimated_height_delta * _score_profile.height_weight
	var target_priority_bonus := _resolve_target_role_threat_bonus(
		context,
		target_unit,
		estimated_damage,
		harmful_control_count,
		estimated_terrain_effect_count,
		estimated_height_delta
	)
	score_input.target_priority_score += target_priority_bonus
	score_input.hit_payoff_score += target_priority_bonus
	var lethal_bonus := _resolve_lethal_target_bonus(score_input, context, target_unit, estimated_damage)
	score_input.hit_payoff_score += lethal_bonus
	score_input.target_priority_score += lethal_bonus
	if harmful_control_count > 0:
		_append_unique_string_name(score_input.estimated_control_target_ids, target_unit.unit_id)
		if _is_priority_threat_target(context, target_unit):
			_append_unique_string_name(score_input.estimated_control_threat_target_ids, target_unit.unit_id)


func _populate_ally_target_payoff(
	score_input: BattleAiScoreInput,
	target_unit: BattleUnitState,
	estimated_damage: int,
	estimated_healing: int,
	harmful_control_count: int,
	beneficial_control_count: int
) -> void:
	var has_ally_benefit := estimated_healing > 0 or beneficial_control_count > 0
	if has_ally_benefit:
		score_input.effective_target_count += 1
		score_input.hit_payoff_score += estimated_healing * _score_profile.heal_weight
		score_input.hit_payoff_score += beneficial_control_count * _score_profile.status_weight
	if estimated_damage <= 0 and harmful_control_count <= 0:
		return
	score_input.estimated_friendly_fire_target_count += 1
	score_input.estimated_friendly_fire_damage += estimated_damage
	if harmful_control_count > 0:
		score_input.estimated_friendly_control_target_count += 1
	var penalty := estimated_damage * _score_profile.friendly_fire_damage_weight \
		+ _score_profile.friendly_fire_target_weight \
		+ harmful_control_count * _score_profile.friendly_control_target_weight
	if estimated_damage >= maxi(int(target_unit.current_hp), 1):
		score_input.estimated_friendly_lethal_target_count += 1
		penalty += _score_profile.friendly_lethal_target_weight
	score_input.friendly_fire_penalty_score += penalty
	score_input.hit_payoff_score -= penalty


func _build_target_effect_metrics(
	skill_def: SkillDef,
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	effect_defs: Array,
	hit_count: int = 1
) -> Dictionary:
	AiTraceRecorder.enter(&"_build_target_effect_metrics")
	var result := _build_target_effect_metrics_impl(skill_def, source_unit, target_unit, effect_defs, hit_count)
	AiTraceRecorder.exit(&"_build_target_effect_metrics")
	return result


func _build_target_effect_metrics_impl(
	skill_def: SkillDef,
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	effect_defs: Array,
	hit_count: int = 1
) -> Dictionary:
	var metrics := {
		"is_empty": true,
		"damage": 0,
		"healing": 0,
		"harmful_control_count": 0,
		"beneficial_control_count": 0,
		"terrain_effect_count": 0,
		"height_delta": 0,
	}
	if source_unit == null or target_unit == null or hit_count <= 0:
		return metrics
	for effect_def_variant in effect_defs:
		var effect_def := effect_def_variant as CombatEffectDef
		if effect_def == null or effect_def.effect_type == CHAIN_DAMAGE_EFFECT_TYPE:
			continue
		var target_filter := _resolve_effect_target_filter(skill_def, effect_def)
		if not _is_unit_valid_for_effect(source_unit, target_unit, target_filter):
			continue
		metrics["is_empty"] = false
		match effect_def.effect_type:
			&"damage":
				var estimate_result := _estimate_damage_for_target_result(
					source_unit,
					[effect_def],
					target_unit,
					_resolve_skill_id(skill_def)
				)
				metrics["damage"] = int(metrics.get("damage", 0)) \
					+ int(estimate_result.get("damage", 0)) * hit_count
				_append_scaled_save_estimates(metrics, estimate_result.get("save_estimates", []), hit_count)
			&"execute":
				var burst_damage := maxi(int(effect_def.params.get("burst_damage", 9999)), 0)
				metrics["damage"] = int(metrics.get("damage", 0)) + burst_damage * hit_count
			&"heal":
				metrics["healing"] = int(metrics.get("healing", 0)) + maxi(int(effect_def.power), 1) * hit_count
			&"status", &"apply_status", &"forced_move":
				var control_key := "beneficial_control_count" if _is_beneficial_effect_filter(target_filter) else "harmful_control_count"
				metrics[control_key] = int(metrics.get(control_key, 0)) + hit_count
			&"shield", &"layered_barrier", &"stamina_restore", &"body_size_category_override":
				metrics["beneficial_control_count"] = int(metrics.get("beneficial_control_count", 0)) + hit_count
			&"terrain", &"terrain_effect":
				metrics["terrain_effect_count"] = int(metrics.get("terrain_effect_count", 0)) + hit_count
			&"height", &"height_delta":
				metrics["height_delta"] = int(metrics.get("height_delta", 0)) + absi(int(effect_def.height_delta)) * hit_count
	return metrics


func _resolve_effect_target_filter(skill_def: SkillDef, effect_def: CombatEffectDef) -> StringName:
	if effect_def != null and effect_def.effect_target_team_filter != &"":
		return effect_def.effect_target_team_filter
	if skill_def != null and skill_def.combat_profile != null:
		return skill_def.combat_profile.target_team_filter
	return &"any"


func _is_unit_valid_for_effect(source_unit: BattleUnitState, target_unit: BattleUnitState, target_filter: StringName) -> bool:
	if target_unit == null or not target_unit.is_alive:
		return false
	match target_filter:
		&"", &"any":
			return true
		&"self":
			return source_unit != null and target_unit.unit_id == source_unit.unit_id
		&"ally", &"friendly":
			return source_unit != null and target_unit.faction_id == source_unit.faction_id
		&"enemy", &"hostile":
			return source_unit != null and target_unit.faction_id != source_unit.faction_id
		_:
			return true


func _is_beneficial_effect_filter(target_filter: StringName) -> bool:
	return target_filter == &"ally" or target_filter == &"friendly" or target_filter == &"self"


func _populate_chain_damage_metrics(score_input: BattleAiScoreInput, context, effect_defs: Array) -> void:
	if score_input == null or context == null or context.state == null or context.unit_state == null:
		return
	var chain_effects := _collect_chain_damage_effect_defs(effect_defs)
	if chain_effects.is_empty():
		return
	for chain_effect in chain_effects:
		var chain_target_effects := _build_chain_target_effect_defs(effect_defs, chain_effect)
		if chain_target_effects.is_empty():
			continue
		for primary_target_id in score_input.target_unit_ids:
			var primary_target := context.state.units.get(primary_target_id) as BattleUnitState
			if primary_target == null:
				continue
			for chain_target in _collect_chain_damage_targets(context, primary_target, score_input.skill_def, chain_effect):
				_populate_target_effect_metrics(score_input, context, chain_target, chain_target_effects, 1, true)


func _collect_chain_damage_effect_defs(effect_defs: Array) -> Array[CombatEffectDef]:
	var chain_effects: Array[CombatEffectDef] = []
	for effect_def_variant in effect_defs:
		var effect_def := effect_def_variant as CombatEffectDef
		if effect_def != null and effect_def.effect_type == CHAIN_DAMAGE_EFFECT_TYPE:
			chain_effects.append(effect_def)
	return chain_effects


func _build_chain_target_effect_defs(effect_defs: Array, chain_effect: CombatEffectDef) -> Array:
	var chain_target_effects: Array = []
	for effect_def_variant in effect_defs:
		var effect_def := effect_def_variant as CombatEffectDef
		if effect_def == null or effect_def == chain_effect or effect_def.effect_type == CHAIN_DAMAGE_EFFECT_TYPE:
			continue
		chain_target_effects.append(effect_def)
	return chain_target_effects


func _collect_chain_damage_targets(
	context,
	primary_target: BattleUnitState,
	skill_def: SkillDef,
	chain_effect: CombatEffectDef
) -> Array[BattleUnitState]:
	var targets: Array[BattleUnitState] = []
	if context == null or context.state == null or context.unit_state == null or primary_target == null or chain_effect == null:
		return targets
	var max_radius := _resolve_chain_damage_radius(context, primary_target, chain_effect)
	if max_radius <= 0:
		return targets
	var chain_params := _get_effect_params(chain_effect)
	var prevent_repeat_target := bool(chain_params.get("prevent_repeat_target", true))
	var target_filter := _resolve_effect_target_filter(skill_def, chain_effect)
	var visited: Dictionary = {}
	var queue: Array[BattleUnitState] = []
	visited[primary_target.unit_id] = true
	queue.append(primary_target)
	while not queue.is_empty():
		var current := queue.pop_front() as BattleUnitState
		for unit_variant in context.state.units.values():
			var candidate := unit_variant as BattleUnitState
			if candidate == null or not candidate.is_alive:
				continue
			if prevent_repeat_target and visited.has(candidate.unit_id):
				continue
			if not _is_unit_valid_for_effect(context.unit_state, candidate, target_filter):
				continue
			if not _is_within_chain_radius(context, primary_target, candidate, max_radius):
				continue
			if not _is_chain_path_clear(context, current, candidate):
				continue
			visited[candidate.unit_id] = true
			targets.append(candidate)
			queue.append(candidate)
	targets.sort_custom(func(left: BattleUnitState, right: BattleUnitState) -> bool:
		var left_distance := _distance_between_units(context, primary_target, left)
		var right_distance := _distance_between_units(context, primary_target, right)
		if left_distance != right_distance:
			return left_distance < right_distance
		if left.coord.y != right.coord.y:
			return left.coord.y < right.coord.y
		if left.coord.x != right.coord.x:
			return left.coord.x < right.coord.x
		return String(left.unit_id) < String(right.unit_id)
	)
	return targets


func _resolve_chain_damage_radius(context, primary_target: BattleUnitState, chain_effect: CombatEffectDef) -> int:
	var chain_params := _get_effect_params(chain_effect)
	var base_radius := maxi(int(chain_params.get("base_chain_radius", 1)), 0)
	var bonus_effect_id := ProgressionDataUtils.to_string_name(chain_params.get("bonus_terrain_effect_id", ""))
	if bonus_effect_id != &"" and _unit_stands_on_terrain_effect(context, primary_target, bonus_effect_id):
		return maxi(int(chain_params.get("wet_chain_radius", base_radius)), base_radius)
	return base_radius


func _unit_stands_on_terrain_effect(context, unit_state: BattleUnitState, terrain_effect_id: StringName) -> bool:
	if context == null or context.state == null or context.grid_service == null or unit_state == null or terrain_effect_id == &"":
		return false
	unit_state.refresh_footprint()
	for occupied_coord in unit_state.occupied_coords:
		var cell = context.grid_service.get_cell(context.state, occupied_coord)
		if cell == null:
			continue
		if cell.terrain_effect_ids.has(terrain_effect_id):
			return true
		for effect_state in cell.timed_terrain_effects:
			if effect_state != null and effect_state.effect_id == terrain_effect_id:
				return true
	return false


func _get_effect_params(effect_def: CombatEffectDef) -> Dictionary:
	if effect_def == null or effect_def.params == null:
		return {}
	return effect_def.params


func _is_within_chain_radius(context, primary_target: BattleUnitState, candidate: BattleUnitState, max_radius: int) -> bool:
	if context == null or context.grid_service == null or primary_target == null or candidate == null or max_radius <= 0:
		return false
	primary_target.refresh_footprint()
	candidate.refresh_footprint()
	for primary_coord in primary_target.occupied_coords:
		for candidate_coord in candidate.occupied_coords:
			if context.grid_service.get_distance(primary_coord, candidate_coord) <= max_radius:
				return true
	return false


func _get_line_coords(from: Vector2i, to: Vector2i) -> Array[Vector2i]:
	var coords: Array[Vector2i] = []
	var dx := absi(to.x - from.x)
	var dy := absi(to.y - from.y)
	var sx := 1 if from.x < to.x else -1
	var sy := 1 if from.y < to.y else -1
	var err := dx - dy
	var x := from.x
	var y := from.y
	while x != to.x or y != to.y:
		var e2 := 2 * err
		if e2 > -dy:
			err -= dy
			x += sx
		if e2 < dx:
			err += dx
			y += sy
		if x == to.x and y == to.y:
			break
		coords.append(Vector2i(x, y))
	return coords


func _is_chain_path_clear(context, source_unit: BattleUnitState, target_unit: BattleUnitState) -> bool:
	if context == null or context.state == null or context.grid_service == null or source_unit == null or target_unit == null:
		return false
	source_unit.refresh_footprint()
	target_unit.refresh_footprint()
	for source_coord in source_unit.occupied_coords:
		var source_cell = context.grid_service.get_cell(context.state, source_coord)
		if source_cell == null:
			continue
		var source_height := int(source_cell.current_height)
		for target_coord in target_unit.occupied_coords:
			for mid_coord in _get_line_coords(source_coord, target_coord):
				var mid_cell = context.grid_service.get_cell(context.state, mid_coord)
				if mid_cell == null:
					continue
				if absi(int(mid_cell.current_height) - source_height) > 1:
					return false
	return true


func _populate_path_step_aoe_metrics(
	score_input: BattleAiScoreInput,
	context,
	effect_defs: Array,
	metadata: Dictionary
) -> void:
	if score_input == null or context == null or context.state == null or context.unit_state == null:
		return
	var hit_counts_variant = metadata.get("path_step_hit_counts_by_unit_id", {})
	if hit_counts_variant is not Dictionary or (hit_counts_variant as Dictionary).is_empty():
		return
	var path_step_effect := _find_path_step_aoe_effect(effect_defs)
	if path_step_effect == null:
		path_step_effect = metadata.get("path_step_aoe_effect", null) as CombatEffectDef
	if path_step_effect == null:
		return
	var damage_effect := _build_path_step_damage_effect(path_step_effect)
	if damage_effect == null:
		return
	var hit_counts: Dictionary = hit_counts_variant
	var raw_payoff := 0
	var raw_target_priority := 0
	var raw_status_payoff := 0
	var total_hit_count := 0
	var unique_target_count := 0
	for unit_id_variant in hit_counts.keys():
		var target_unit_id := ProgressionDataUtils.to_string_name(unit_id_variant)
		var hit_count := maxi(int(hit_counts.get(unit_id_variant, 0)), 0)
		if target_unit_id == &"" or hit_count <= 0:
			continue
		var target_unit := context.state.units.get(target_unit_id) as BattleUnitState
		if target_unit == null:
			continue
		total_hit_count += hit_count
		unique_target_count += 1
		var estimate_result := _estimate_damage_for_target_result(
			context.unit_state,
			[damage_effect],
			target_unit,
			_resolve_skill_id(score_input.skill_def)
		)
		var estimated_damage := int(estimate_result.get("damage", 0)) * hit_count
		_append_save_estimates_for_target(
			score_input,
			target_unit,
			_scale_save_estimates(estimate_result.get("save_estimates", []), hit_count)
		)
		score_input.estimated_damage += estimated_damage
		if target_unit.faction_id == context.unit_state.faction_id:
			raw_payoff -= estimated_damage * _score_profile.damage_weight
			continue
		raw_payoff += estimated_damage * _score_profile.damage_weight
		var target_priority_bonus := _resolve_target_role_threat_bonus(context, target_unit, estimated_damage, 0, 0, 0)
		raw_target_priority += target_priority_bonus
		raw_payoff += target_priority_bonus
		var lethal_bonus := _resolve_lethal_target_bonus(score_input, context, target_unit, estimated_damage)
		raw_target_priority += lethal_bonus
		raw_payoff += lethal_bonus
		if _path_step_repeat_status_applies(context, score_input.skill_def, path_step_effect, hit_count):
			raw_status_payoff += _score_profile.status_weight
	raw_payoff += raw_status_payoff
	var hit_rate_multiplier := float(score_input.estimated_hit_rate_percent) / 100.0
	score_input.path_step_hit_count = total_hit_count
	score_input.path_step_unique_target_count = unique_target_count
	score_input.path_step_hit_counts_by_unit_id = hit_counts.duplicate(true)
	score_input.path_step_payoff_score = int(round(float(raw_payoff) * hit_rate_multiplier))
	score_input.hit_payoff_score += score_input.path_step_payoff_score
	score_input.target_priority_score += int(round(float(raw_target_priority) * hit_rate_multiplier))
	if score_input.target_count < unique_target_count:
		score_input.target_count = unique_target_count


func _find_path_step_aoe_effect(effect_defs: Array) -> CombatEffectDef:
	for effect_def_variant in effect_defs:
		var effect_def := effect_def_variant as CombatEffectDef
		if effect_def != null and effect_def.effect_type == PATH_STEP_AOE_EFFECT_TYPE:
			return effect_def
	return null


func _build_path_step_damage_effect(path_step_effect: CombatEffectDef) -> CombatEffectDef:
	if path_step_effect == null:
		return null
	var damage_effect := path_step_effect.duplicate_for_runtime()
	if damage_effect == null:
		return null
	damage_effect.effect_type = &"damage"
	return damage_effect


func _path_step_repeat_status_applies(
	context,
	skill_def: SkillDef,
	path_step_effect: CombatEffectDef,
	hit_count: int
) -> bool:
	if context == null or path_step_effect == null or path_step_effect.params == null or hit_count <= 0:
		return false
	var status_id := ProgressionDataUtils.to_string_name(path_step_effect.params.get("repeat_hit_status_id", ""))
	if status_id == &"":
		return false
	var threshold := maxi(int(path_step_effect.params.get("repeat_hit_status_threshold", 1)), 1)
	if hit_count < threshold:
		return false
	var duration_tu := int(path_step_effect.params.get("repeat_hit_status_duration_tu", 0))
	if duration_tu <= 0:
		return false
	var min_skill_level := maxi(int(path_step_effect.params.get("repeat_hit_status_min_skill_level", 0)), 0)
	var skill_id := skill_def.skill_id if skill_def != null else &""
	return _get_context_skill_level(context, skill_id) >= min_skill_level


func _filter_effect_defs_for_context(effect_defs: Array, context, skill_def: SkillDef) -> Array:
	var filtered_effect_defs: Array = []
	var should_filter := context != null and context.unit_state != null
	var skill_level := _get_context_skill_level(context, skill_def.skill_id if skill_def != null else &"")
	for effect_def_variant in effect_defs:
		var effect_def := effect_def_variant as CombatEffectDef
		if effect_def == null:
			continue
		if not _is_effect_unlocked_for_skill_level(effect_def, skill_level, should_filter):
			continue
		filtered_effect_defs.append(effect_def)
	return filtered_effect_defs


func _is_effect_unlocked_for_skill_level(effect_def: CombatEffectDef, skill_level: int, should_filter: bool) -> bool:
	if effect_def == null:
		return false
	if not should_filter:
		return true
	var min_level := maxi(int(effect_def.min_skill_level), 0)
	var max_level := int(effect_def.max_skill_level)
	if skill_level < min_level:
		return false
	return max_level < 0 or skill_level <= max_level


func _estimate_damage_for_target(
	source_unit: BattleUnitState,
	effect_defs: Array,
	target_unit: BattleUnitState,
	skill_id: StringName = &""
) -> int:
	return int(_estimate_damage_for_target_result(source_unit, effect_defs, target_unit, skill_id).get("damage", 0))


func _estimate_damage_for_target_result(
	source_unit: BattleUnitState,
	effect_defs: Array,
	target_unit: BattleUnitState,
	skill_id: StringName = &""
) -> Dictionary:
	var total := 0
	var save_estimates: Array[Dictionary] = []
	for effect_def_variant in effect_defs:
		var effect_def := effect_def_variant as CombatEffectDef
		if effect_def == null or effect_def.effect_type != &"damage":
			continue
		var single_effect_defs: Array = [effect_def]
		var damage_preview := BATTLE_DAMAGE_PREVIEW_RANGE_SERVICE_SCRIPT.build_skill_damage_preview(
			source_unit,
			single_effect_defs
		)
		var base_damage := _estimate_damage_from_preview(damage_preview)
		var bonus_damage := _estimate_conditional_bonus_damage(effect_def, target_unit)
		var multiplier := _resolve_effect_damage_multiplier(effect_def, target_unit)
		var pre_save_damage := maxi(int(round(float(base_damage + bonus_damage) * multiplier)), 0)
		var save_estimate := _build_damage_save_estimate(
			source_unit,
			target_unit,
			effect_def,
			pre_save_damage,
			skill_id
		)
		var adjusted_damage := int(save_estimate.get("damage_after_save_estimate", pre_save_damage))
		total += adjusted_damage
		if bool(save_estimate.get("has_save", false)):
			save_estimates.append(save_estimate)
	return {
		"damage": total,
		"save_estimates": save_estimates,
	}


func _build_damage_save_estimate(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	effect_def: CombatEffectDef,
	damage_before_save: int,
	skill_id: StringName
) -> Dictionary:
	var save_context := {}
	if skill_id != &"":
		save_context["skill_id"] = skill_id
	var probability := BATTLE_SAVE_RESOLVER_SCRIPT.estimate_save_success_probability(
		source_unit,
		target_unit,
		effect_def,
		save_context
	)
	if not bool(probability.get("has_save", false)):
		return {
			"has_save": false,
			"damage_before_save": damage_before_save,
			"damage_after_save_estimate": damage_before_save,
		}
	var success_basis_points := clampi(int(probability.get("success_probability_basis_points", 0)), 0, 10000)
	var failure_basis_points := maxi(10000 - success_basis_points, 0)
	var damage_on_save_success := 0
	if bool(effect_def.save_partial_on_success) and not bool(probability.get("immune", false)):
		damage_on_save_success = int(damage_before_save / 2)
	var expected_damage := int(round(
		(
			float(damage_before_save * failure_basis_points)
			+ float(damage_on_save_success * success_basis_points)
		) / 10000.0
	))
	return {
		"has_save": true,
		"damage_before_save": damage_before_save,
		"damage_after_save_estimate": maxi(expected_damage, 0),
		"damage_on_save_failure": damage_before_save,
		"damage_on_save_success": damage_on_save_success,
		"save_partial_on_success": bool(effect_def.save_partial_on_success),
		"save_success_probability_basis_points": success_basis_points,
		"save_success_rate_percent": int(round(float(success_basis_points) / 100.0)),
		"save_failure_probability_basis_points": failure_basis_points,
		"dc": int(probability.get("dc", 0)),
		"ability": String(probability.get("ability", "")),
		"save_tag": String(probability.get("save_tag", "")),
		"advantage_state": String(probability.get("advantage_state", "")),
		"ability_value": int(probability.get("ability_value", 0)),
		"ability_modifier": int(probability.get("ability_modifier", 0)),
		"bonus": int(probability.get("bonus", 0)),
		"immune": bool(probability.get("immune", false)),
		"hit_count": 1,
	}


func _append_scaled_save_estimates(metrics: Dictionary, estimates_value: Variant, hit_count: int) -> void:
	var scaled_estimates := _scale_save_estimates(estimates_value, hit_count)
	if scaled_estimates.is_empty():
		return
	var estimates: Array = metrics.get("save_estimates", [])
	for estimate in scaled_estimates:
		estimates.append(estimate)
	metrics["save_estimates"] = estimates


func _scale_save_estimates(estimates_value: Variant, hit_count: int) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	if estimates_value is not Array:
		return result
	var safe_hit_count := maxi(hit_count, 1)
	for estimate_variant in estimates_value:
		if estimate_variant is not Dictionary:
			continue
		var estimate := (estimate_variant as Dictionary).duplicate(true)
		var previous_hit_count := maxi(int(estimate.get("hit_count", 1)), 1)
		estimate["hit_count"] = previous_hit_count * safe_hit_count
		for damage_key in [
			"damage_before_save",
			"damage_after_save_estimate",
			"damage_on_save_failure",
			"damage_on_save_success",
		]:
			estimate[damage_key] = int(estimate.get(damage_key, 0)) * safe_hit_count
		result.append(estimate)
	return result


func _append_save_estimates_for_target(
	score_input: BattleAiScoreInput,
	target_unit: BattleUnitState,
	estimates_value: Variant
) -> void:
	if score_input == null or target_unit == null or estimates_value is not Array:
		return
	var estimates_array := estimates_value as Array
	if estimates_array.is_empty():
		return
	var target_key := String(target_unit.unit_id)
	var existing_value = score_input.save_estimates_by_target_id.get(target_key, [])
	var existing: Array = existing_value if existing_value is Array else []
	for estimate_variant in estimates_array:
		if estimate_variant is Dictionary:
			existing.append((estimate_variant as Dictionary).duplicate(true))
	score_input.save_estimates_by_target_id[target_key] = existing


func _resolve_skill_id(skill_def: SkillDef) -> StringName:
	return skill_def.skill_id if skill_def != null else &""


func _estimate_damage_from_preview(damage_preview: Dictionary) -> int:
	if damage_preview.is_empty() or not bool(damage_preview.get("has_damage", false)):
		return 0
	return maxi(int(round(
		(float(damage_preview.get("min_damage", 0)) + float(damage_preview.get("max_damage", 0))) / 2.0
	)), 0)


func _resolve_effect_damage_multiplier(effect_def: CombatEffectDef, target_unit: BattleUnitState) -> float:
	if effect_def == null:
		return 1.0
	var multiplier := _get_pre_resistance_damage_multiplier(effect_def)
	if _has_bonus_condition(effect_def, target_unit):
		multiplier *= _get_damage_ratio_multiplier(effect_def)
	return maxf(multiplier, 0.0)


func _resolve_target_role_threat_bonus(
	context,
	target_unit: BattleUnitState,
	estimated_damage: int,
	estimated_status_count: int,
	estimated_terrain_effect_count: int,
	estimated_height_delta: int
) -> int:
	var multiplier_basis_points := _resolve_target_role_threat_multiplier_basis_points(context, target_unit)
	if multiplier_basis_points <= THREAT_MULTIPLIER_BASIS_POINTS_DENOMINATOR:
		return 0
	var base_payoff := maxi(estimated_damage, 0) * _score_profile.damage_weight \
		+ maxi(estimated_status_count, 0) * _score_profile.status_weight \
		+ maxi(estimated_terrain_effect_count, 0) * _score_profile.terrain_weight \
		+ maxi(estimated_height_delta, 0) * _score_profile.height_weight
	if base_payoff <= 0:
		return 0
	var bonus_basis_points := multiplier_basis_points - THREAT_MULTIPLIER_BASIS_POINTS_DENOMINATOR
	var rounded_bonus := int((base_payoff * bonus_basis_points + int(THREAT_MULTIPLIER_BASIS_POINTS_DENOMINATOR / 2)) / THREAT_MULTIPLIER_BASIS_POINTS_DENOMINATOR)
	return maxi(rounded_bonus, 0)


func _resolve_lethal_target_bonus(
	score_input: BattleAiScoreInput,
	context,
	target_unit: BattleUnitState,
	estimated_damage: int
) -> int:
	if score_input == null or target_unit == null or not target_unit.is_alive:
		return 0
	if estimated_damage < maxi(int(target_unit.current_hp), 1):
		return 0
	score_input.estimated_lethal_target_count += 1
	_append_unique_string_name(score_input.estimated_lethal_target_ids, target_unit.unit_id)
	var bonus := maxi(int(_score_profile.lethal_target_weight), 0)
	if _is_priority_threat_target(context, target_unit):
		score_input.estimated_lethal_threat_target_count += 1
		_append_unique_string_name(score_input.estimated_lethal_threat_target_ids, target_unit.unit_id)
		bonus += maxi(int(_score_profile.lethal_threat_target_weight), 0)
	return bonus


func _is_priority_threat_target(context, target_unit: BattleUnitState) -> bool:
	if _resolve_target_role_threat_multiplier_basis_points(context, target_unit) > THREAT_MULTIPLIER_BASIS_POINTS_DENOMINATOR:
		return true
	return _is_target_currently_threatening_ally(context, target_unit)


func _is_target_currently_threatening_ally(context, target_unit: BattleUnitState) -> bool:
	if context == null or context.state == null or context.grid_service == null or context.unit_state == null or target_unit == null:
		return false
	var threat_range := _resolve_unit_effective_hostile_threat_range(context, target_unit)
	if threat_range <= 0:
		return false
	for unit_variant in context.state.units.values():
		var ally_unit := unit_variant as BattleUnitState
		if ally_unit == null or not ally_unit.is_alive or ally_unit.faction_id != context.unit_state.faction_id:
			continue
		if _distance_between_units(context, target_unit, ally_unit) <= threat_range:
			return true
	return false


func _resolve_unit_effective_hostile_threat_range(context, threat_unit: BattleUnitState) -> int:
	if context == null or threat_unit == null:
		return 0
	var best_range := 0
	for skill_id in threat_unit.known_active_skill_ids:
		var normalized_skill_id := ProgressionDataUtils.to_string_name(skill_id)
		if normalized_skill_id == &"":
			continue
		var skill_def = context.skill_defs.get(normalized_skill_id) as SkillDef
		if skill_def == null or skill_def.combat_profile == null:
			continue
		if ProgressionDataUtils.to_string_name(skill_def.combat_profile.target_team_filter) == &"ally":
			continue
		var effect_defs := _collect_role_threat_effect_defs(threat_unit, skill_def)
		if not _is_damage_skill(effect_defs) and not _is_control_skill(effect_defs):
			continue
		best_range = maxi(best_range, BATTLE_RANGE_SERVICE_SCRIPT.get_effective_skill_threat_range(threat_unit, skill_def))
	if best_range <= 0:
		best_range = BATTLE_RANGE_SERVICE_SCRIPT.get_weapon_attack_range(threat_unit)
	return best_range


func _distance_between_units(context, first_unit: BattleUnitState, second_unit: BattleUnitState) -> int:
	if context == null or context.grid_service == null or first_unit == null or second_unit == null:
		return 999999
	first_unit.refresh_footprint()
	second_unit.refresh_footprint()
	var best_distance := 999999
	for first_coord in first_unit.occupied_coords:
		for second_coord in second_unit.occupied_coords:
			best_distance = mini(best_distance, context.grid_service.get_distance(first_coord, second_coord))
	return best_distance


func _resolve_target_role_threat_multiplier_basis_points(context, target_unit: BattleUnitState) -> int:
	if context == null or target_unit == null or _score_profile == null:
		return THREAT_MULTIPLIER_BASIS_POINTS_DENOMINATOR
	var heal_skill_count := 0
	var control_skill_count := 0
	var best_ranged_attack_range := 0
	for skill_id in target_unit.known_active_skill_ids:
		var normalized_skill_id := ProgressionDataUtils.to_string_name(skill_id)
		if normalized_skill_id == &"":
			continue
		var skill_def = context.skill_defs.get(normalized_skill_id) as SkillDef
		if skill_def == null or skill_def.combat_profile == null:
			continue
		var role_effect_defs := _collect_role_threat_effect_defs(target_unit, skill_def)
		if _is_heal_or_support_skill(skill_def, role_effect_defs):
			heal_skill_count += 1
		if _is_control_skill(role_effect_defs):
			control_skill_count += 1
		if _is_damage_skill(role_effect_defs):
			var effective_range := BATTLE_RANGE_SERVICE_SCRIPT.get_effective_skill_threat_range(target_unit, skill_def)
			if effective_range >= MIN_RANGED_THREAT_RANGE:
				best_ranged_attack_range = maxi(best_ranged_attack_range, effective_range)

	var multiplier_basis_points := THREAT_MULTIPLIER_BASIS_POINTS_DENOMINATOR \
		+ heal_skill_count * maxi(int(_score_profile.threat_healer_bias_basis_points), 0) \
		+ control_skill_count * maxi(int(_score_profile.threat_control_bias_basis_points), 0)
	if best_ranged_attack_range >= MIN_RANGED_THREAT_RANGE:
		multiplier_basis_points += maxi(int(_score_profile.threat_ranged_bias_basis_points), 0)
		multiplier_basis_points += (best_ranged_attack_range - (MIN_RANGED_THREAT_RANGE - 1)) \
			* maxi(int(_score_profile.threat_range_step_bias_basis_points), 0)
	var cap_basis_points := int(_score_profile.threat_multiplier_cap_basis_points)
	if cap_basis_points > THREAT_MULTIPLIER_BASIS_POINTS_DENOMINATOR:
		multiplier_basis_points = mini(multiplier_basis_points, cap_basis_points)
	return maxi(multiplier_basis_points, THREAT_MULTIPLIER_BASIS_POINTS_DENOMINATOR)


func _collect_role_threat_effect_defs(unit_state: BattleUnitState, skill_def: SkillDef) -> Array:
	var effect_defs: Array = []
	if unit_state == null or skill_def == null or skill_def.combat_profile == null:
		return effect_defs
	var skill_level := _get_unit_skill_level(unit_state, skill_def.skill_id)
	for effect_def in skill_def.combat_profile.effect_defs:
		if effect_def != null and _is_effect_unlocked_for_skill_level(effect_def, skill_level, true):
			effect_defs.append(effect_def)
	for cast_variant in skill_def.combat_profile.get_unlocked_cast_variants(skill_level):
		if cast_variant == null:
			continue
		for effect_def in cast_variant.effect_defs:
			if effect_def != null and _is_effect_unlocked_for_skill_level(effect_def, skill_level, true):
				effect_defs.append(effect_def)
	return effect_defs


func _is_heal_or_support_skill(skill_def: SkillDef, effect_defs: Array) -> bool:
	if skill_def != null and skill_def.combat_profile != null:
		if ProgressionDataUtils.to_string_name(skill_def.combat_profile.target_team_filter) == &"ally":
			return true
	for effect_def_variant in effect_defs:
		var effect_def := effect_def_variant as CombatEffectDef
		if effect_def == null:
			continue
		if effect_def.effect_type == &"heal":
			return true
		if ProgressionDataUtils.to_string_name(effect_def.effect_target_team_filter) == &"ally":
			return true
	return false


func _is_control_skill(effect_defs: Array) -> bool:
	for effect_def_variant in effect_defs:
		var effect_def := effect_def_variant as CombatEffectDef
		if effect_def == null:
			continue
		var effect_type := ProgressionDataUtils.to_string_name(effect_def.effect_type)
		if effect_type == &"status" or effect_type == &"apply_status" or effect_type == &"forced_move":
			return true
		if effect_def.status_id != &"" or effect_def.save_failure_status_id != &"":
			return true
	return false


func _is_damage_skill(effect_defs: Array) -> bool:
	for effect_def_variant in effect_defs:
		var effect_def := effect_def_variant as CombatEffectDef
		if effect_def != null and (effect_def.effect_type == &"damage" or effect_def.effect_type == &"execute"):
			return true
	return false


func _get_unit_skill_level(unit_state: BattleUnitState, skill_id: StringName) -> int:
	if unit_state == null or skill_id == &"":
		return 0
	if unit_state.known_skill_level_map.has(skill_id):
		return int(unit_state.known_skill_level_map.get(skill_id, 0))
	return 1 if unit_state.known_active_skill_ids.has(skill_id) else 0


func _get_pre_resistance_damage_multiplier(effect_def: CombatEffectDef) -> float:
	if effect_def == null or effect_def.params == null:
		return 1.0
	return maxf(float(effect_def.params.get("runtime_pre_resistance_damage_multiplier", 1.0)), 0.0)


func _has_bonus_condition(effect_def: CombatEffectDef, target_unit: BattleUnitState) -> bool:
	if effect_def == null or target_unit == null:
		return false
	match effect_def.bonus_condition:
		BONUS_CONDITION_TARGET_LOW_HP:
			return _is_target_low_hp(effect_def, target_unit)
		_:
			return false


func _is_target_low_hp(effect_def: CombatEffectDef, target_unit: BattleUnitState) -> bool:
	var max_hp := 0
	if target_unit.attribute_snapshot != null:
		max_hp = target_unit.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX)
	if max_hp <= 0:
		max_hp = maxi(target_unit.current_hp, 1)

	var threshold_percent := 50
	if effect_def != null and effect_def.params != null:
		if effect_def.params.has("hp_ratio_threshold_percent"):
			threshold_percent = clampi(int(effect_def.params.get("hp_ratio_threshold_percent", threshold_percent)), 0, 100)
	return int(target_unit.current_hp) * 100 <= max_hp * threshold_percent


func _get_damage_ratio_multiplier(effect_def: CombatEffectDef) -> float:
	if effect_def == null:
		return 1.0
	return maxf(float(effect_def.damage_ratio_percent) / 100.0, 0.0)


func _estimate_conditional_bonus_damage(effect_def: CombatEffectDef, target_unit: BattleUnitState) -> int:
	if effect_def == null or effect_def.params == null or not _has_bonus_condition(effect_def, target_unit):
		return 0
	var dice_count := maxi(int(effect_def.params.get("bonus_damage_dice_count", 0)), 0)
	var dice_sides := maxi(int(effect_def.params.get("bonus_damage_dice_sides", 0)), 0)
	if dice_count <= 0 or dice_sides <= 0:
		return 0
	var dice_bonus := int(effect_def.params.get("bonus_damage_dice_bonus", 0))
	var numerator := dice_count * (dice_sides + 1)
	var average := int(numerator / 2)
	if numerator % 2 != 0:
		average += 1
	return average + dice_bonus


func _estimate_healing(effect_defs: Array) -> int:
	var total := 0
	for effect_def_variant in effect_defs:
		var effect_def := effect_def_variant as CombatEffectDef
		if effect_def == null or effect_def.effect_type != &"heal":
			continue
		total += maxi(int(effect_def.power), 1)
	return total


func _estimate_status_count(effect_defs: Array) -> int:
	var status_ids: Dictionary = {}
	for effect_def_variant in effect_defs:
		var effect_def := effect_def_variant as CombatEffectDef
		if effect_def == null:
			continue
		if effect_def.effect_type != &"status" and effect_def.effect_type != &"apply_status":
			continue
		if effect_def.status_id == &"":
			continue
		status_ids[effect_def.status_id] = true
	return status_ids.size()


func _estimate_terrain_effect_count(effect_defs: Array) -> int:
	var terrain_effect_ids: Dictionary = {}
	for effect_def_variant in effect_defs:
		var effect_def := effect_def_variant as CombatEffectDef
		if effect_def == null:
			continue
		if effect_def.effect_type != &"terrain" and effect_def.effect_type != &"terrain_effect":
			continue
		if effect_def.terrain_effect_id == &"":
			continue
		terrain_effect_ids[effect_def.terrain_effect_id] = true
	return terrain_effect_ids.size()


func _estimate_height_delta(effect_defs: Array) -> int:
	var total := 0
	for effect_def_variant in effect_defs:
		var effect_def := effect_def_variant as CombatEffectDef
		if effect_def == null:
			continue
		if effect_def.effect_type != &"height" and effect_def.effect_type != &"height_delta":
			continue
		total += absi(int(effect_def.height_delta))
	return total


func _resolve_estimated_hit_rate_percent(preview) -> int:
	if preview == null or preview.hit_preview.is_empty():
		return 100
	var stage_success_rates: Array = preview.hit_preview.get("stage_success_rates", [])
	if stage_success_rates is Array and not stage_success_rates.is_empty():
		var total := 0
		for hit_rate_variant in stage_success_rates:
			total += int(hit_rate_variant)
		return maxi(int(round(float(total) / float(stage_success_rates.size()))), 0)
	if preview.hit_preview.has("success_rate_percent"):
		return maxi(int(preview.hit_preview.get("success_rate_percent", 100)), 0)
	return 100


func _populate_resource_cost_metrics(score_input: BattleAiScoreInput, skill_def: SkillDef, context) -> void:
	if score_input == null or skill_def == null or skill_def.combat_profile == null:
		return
	var skill_level := _get_context_skill_level(context, skill_def.skill_id)
	var costs := skill_def.combat_profile.get_effective_resource_costs(skill_level)
	score_input.ap_cost = maxi(int(costs.get("ap_cost", skill_def.combat_profile.ap_cost)), 0)
	score_input.mp_cost = maxi(int(costs.get("mp_cost", skill_def.combat_profile.mp_cost)), 0)
	score_input.stamina_cost = maxi(int(costs.get("stamina_cost", skill_def.combat_profile.stamina_cost)), 0)
	score_input.aura_cost = maxi(int(costs.get("aura_cost", skill_def.combat_profile.aura_cost)), 0)
	score_input.cooldown_tu = maxi(int(costs.get("cooldown_tu", skill_def.combat_profile.cooldown_tu)), 0)
	score_input.resource_cost_score = score_input.ap_cost * _score_profile.ap_cost_weight \
		+ score_input.mp_cost * _score_profile.mp_cost_weight \
		+ score_input.stamina_cost * _score_profile.stamina_cost_weight \
		+ score_input.aura_cost * _score_profile.aura_cost_weight \
		+ score_input.cooldown_tu * _score_profile.cooldown_weight


func _get_context_skill_level(context, skill_id: StringName) -> int:
	if context == null or skill_id == &"":
		return 0
	var unit_state = context.get("unit_state")
	if unit_state == null:
		return 0
	if unit_state.known_skill_level_map.has(skill_id):
		return int(unit_state.known_skill_level_map.get(skill_id, 0))
	return 1 if unit_state.known_active_skill_ids.has(skill_id) else 0


func _populate_position_metrics(score_input: BattleAiScoreInput, context, metadata: Dictionary) -> void:
	if score_input == null or context == null or context.unit_state == null or context.grid_service == null:
		return
	var desired_min_distance := int(metadata.get("desired_min_distance", -1))
	var desired_max_distance := int(metadata.get("desired_max_distance", desired_min_distance))
	score_input.desired_min_distance = desired_min_distance
	score_input.desired_max_distance = maxi(desired_max_distance, desired_min_distance) if desired_min_distance >= 0 and desired_max_distance >= 0 else -1
	score_input.position_current_distance = int(metadata.get("position_current_distance", -1))
	score_input.position_safe_distance = int(metadata.get("position_safe_distance", -1))
	var explicit_objective_kind = ProgressionDataUtils.to_string_name(metadata.get("position_objective_kind", ""))
	if explicit_objective_kind == &"none":
		score_input.position_objective_kind = &"none"
		score_input.position_anchor_coord = context.unit_state.coord
		score_input.distance_to_primary_coord = -1
		score_input.position_objective_score = 0
		return
	var position_target_unit = metadata.get("position_target_unit", null) as BattleUnitState
	var current_distance_to_target := -1
	if position_target_unit != null:
		score_input.position_objective_kind = explicit_objective_kind if explicit_objective_kind != &"" else &"distance_band"
		score_input.position_anchor_coord = _resolve_position_anchor_coord(score_input, context, metadata)
		score_input.distance_to_primary_coord = _distance_from_anchor_to_unit(
			context,
			score_input.position_anchor_coord,
			position_target_unit
		)
		if score_input.position_objective_kind == &"distance_band_progress":
			current_distance_to_target = _distance_from_anchor_to_unit(
				context,
				context.unit_state.coord,
				position_target_unit
			)
	else:
		score_input.position_objective_kind = explicit_objective_kind if explicit_objective_kind != &"" else &"cast_distance"
		score_input.position_anchor_coord = _resolve_position_anchor_coord(score_input, context, metadata)
		score_input.distance_to_primary_coord = context.grid_service.get_distance_from_unit_to_coord(
			context.unit_state,
			score_input.primary_coord
		) if score_input.primary_coord != Vector2i(-1, -1) else -1
	score_input.position_objective_score = _build_position_objective_score(
		score_input.position_objective_kind,
		score_input.distance_to_primary_coord,
		score_input.desired_min_distance,
		score_input.desired_max_distance,
		current_distance_to_target
	)


func _resolve_position_anchor_coord(score_input: BattleAiScoreInput, context, metadata: Dictionary) -> Vector2i:
	if context == null or context.unit_state == null:
		return Vector2i(-1, -1)
	var metadata_anchor = metadata.get("position_anchor_coord", Vector2i(-1, -1))
	if metadata_anchor is Vector2i and metadata_anchor != Vector2i(-1, -1):
		return metadata_anchor
	if score_input != null and score_input.preview != null and score_input.preview.resolved_anchor_coord != Vector2i(-1, -1):
		return score_input.preview.resolved_anchor_coord
	return context.unit_state.coord


func _populate_post_action_threat_projection(score_input: BattleAiScoreInput, context, metadata: Dictionary) -> void:
	if score_input == null or context == null or context.state == null or context.unit_state == null or context.grid_service == null:
		return
	if not _should_populate_survival_projection(score_input, context):
		return
	var actor_hp_budget := _resolve_actor_survival_budget(context.unit_state)
	var projected_coord := _resolve_projected_actor_coord(score_input, context, metadata)
	var suppressed_threat_ids := _build_suppressed_threat_unit_ids(score_input)
	var pre_projection := _get_current_actor_threat_projection(context)
	var post_projection := _get_projected_actor_threat_projection(context, projected_coord, suppressed_threat_ids, pre_projection)
	score_input.has_post_action_threat_projection = true
	score_input.projected_actor_coord = projected_coord
	score_input.pre_action_threat_unit_ids = _copy_string_name_array(pre_projection.get("unit_ids", []))
	score_input.pre_action_threat_count = int(pre_projection.get("count", 0))
	score_input.pre_action_threat_expected_damage = int(pre_projection.get("expected_damage", 0))
	score_input.pre_action_survival_margin = actor_hp_budget - score_input.pre_action_threat_expected_damage
	score_input.pre_action_is_lethal_survival_risk = score_input.pre_action_threat_count > 0 \
		and score_input.pre_action_threat_expected_damage >= actor_hp_budget
	score_input.post_action_remaining_threat_unit_ids = _copy_string_name_array(post_projection.get("unit_ids", []))
	score_input.post_action_remaining_threat_count = int(post_projection.get("count", 0))
	score_input.post_action_remaining_threat_expected_damage = int(post_projection.get("expected_damage", 0))
	score_input.post_action_survival_margin = actor_hp_budget - score_input.post_action_remaining_threat_expected_damage
	score_input.post_action_is_lethal_survival_risk = score_input.post_action_remaining_threat_count > 0 \
		and score_input.post_action_remaining_threat_expected_damage >= actor_hp_budget


func _should_populate_survival_projection(score_input: BattleAiScoreInput, context) -> bool:
	if score_input == null:
		return false
	if score_input.score_bucket_id == &"archer_survival":
		return true
	if score_input.skill_def != null:
		if String(score_input.skill_def.skill_id).begins_with("mage_"):
			return true
		for tag in score_input.skill_def.tags:
			if ProgressionDataUtils.to_string_name(tag) == &"mage":
				return true
	if context != null and context.unit_state != null:
		for skill_id in context.unit_state.known_active_skill_ids:
			if String(skill_id).begins_with("mage_") and score_input.action_kind == &"ground_reposition_skill":
				return true
	return false


func _get_current_actor_threat_projection(context) -> Dictionary:
	if context == null or context.unit_state == null:
		return _empty_threat_projection()
	var cache_key := "current_actor_threat_projection:%s" % String(context.unit_state.unit_id)
	if context.score_projection_cache.has(cache_key):
		var cached = context.score_projection_cache.get(cache_key, {})
		return cached if cached is Dictionary else _empty_threat_projection()
	var projection := _collect_actor_threat_projection(context, context.unit_state.coord, {})
	context.score_projection_cache[cache_key] = projection
	return projection


func _get_projected_actor_threat_projection(
	context,
	projected_coord: Vector2i,
	suppressed_threat_ids: Dictionary,
	pre_projection: Dictionary
) -> Dictionary:
	if context == null or context.unit_state == null:
		return _empty_threat_projection()
	if projected_coord == Vector2i(-1, -1) or projected_coord == context.unit_state.coord:
		return _subtract_suppressed_threats_from_projection(pre_projection, suppressed_threat_ids)
	var suppressed_key := _build_projection_suppression_key(suppressed_threat_ids)
	var cache_key := "actor_threat_projection:%s:%s:%s" % [
		String(context.unit_state.unit_id),
		str(projected_coord),
		suppressed_key,
	]
	if context.score_projection_cache.has(cache_key):
		var cached = context.score_projection_cache.get(cache_key, {})
		return cached if cached is Dictionary else _empty_threat_projection()
	var projection := _collect_actor_threat_projection(context, projected_coord, suppressed_threat_ids)
	context.score_projection_cache[cache_key] = projection
	return projection


func _subtract_suppressed_threats_from_projection(pre_projection: Dictionary, suppressed_threat_ids: Dictionary) -> Dictionary:
	if suppressed_threat_ids.is_empty():
		return pre_projection
	var remaining_ids: Array[StringName] = []
	var remaining_damage := 0
	var damage_by_unit_id := pre_projection.get("expected_damage_by_unit_id", {}) as Dictionary
	for unit_id in _copy_string_name_array(pre_projection.get("unit_ids", [])):
		if suppressed_threat_ids.has(unit_id):
			continue
		remaining_ids.append(unit_id)
		remaining_damage += int(damage_by_unit_id.get(unit_id, 0))
	return {
		"unit_ids": remaining_ids,
		"count": remaining_ids.size(),
		"expected_damage": remaining_damage,
		"expected_damage_by_unit_id": _filter_damage_by_unit_id(damage_by_unit_id, remaining_ids),
	}


func _filter_damage_by_unit_id(damage_by_unit_id: Dictionary, unit_ids: Array[StringName]) -> Dictionary:
	var filtered: Dictionary = {}
	for unit_id in unit_ids:
		filtered[unit_id] = int(damage_by_unit_id.get(unit_id, 0))
	return filtered


func _build_projection_suppression_key(suppressed_threat_ids: Dictionary) -> String:
	if suppressed_threat_ids.is_empty():
		return "-"
	var parts: Array[String] = []
	for unit_id in suppressed_threat_ids.keys():
		parts.append(String(unit_id))
	parts.sort()
	return ",".join(parts)


func _empty_threat_projection() -> Dictionary:
	var empty_ids: Array[StringName] = []
	return {
		"unit_ids": empty_ids,
		"count": 0,
		"expected_damage": 0,
		"expected_damage_by_unit_id": {},
	}


func _resolve_projected_actor_coord(score_input: BattleAiScoreInput, context, metadata: Dictionary) -> Vector2i:
	var coord := _resolve_position_anchor_coord(score_input, context, metadata)
	if coord == Vector2i(-1, -1) and context != null and context.unit_state != null:
		return context.unit_state.coord
	return coord


func _resolve_actor_survival_budget(actor_unit: BattleUnitState) -> int:
	if actor_unit == null:
		return 1
	return maxi(int(actor_unit.current_hp), 1) + maxi(int(actor_unit.current_shield_hp), 0)


func _build_suppressed_threat_unit_ids(score_input: BattleAiScoreInput) -> Dictionary:
	var suppressed_ids: Dictionary = {}
	if score_input == null:
		return suppressed_ids
	for target_id in score_input.estimated_lethal_target_ids:
		var normalized := ProgressionDataUtils.to_string_name(target_id)
		if normalized != &"":
			suppressed_ids[normalized] = true
	for target_id in score_input.estimated_control_target_ids:
		var normalized := ProgressionDataUtils.to_string_name(target_id)
		if normalized != &"":
			suppressed_ids[normalized] = true
	return suppressed_ids


func _collect_actor_threat_projection(context, actor_anchor_coord: Vector2i, suppressed_threat_ids: Dictionary) -> Dictionary:
	var threat_unit_ids: Array[StringName] = []
	var expected_damage := 0
	var expected_damage_by_unit_id: Dictionary = {}
	if context == null or context.state == null or context.unit_state == null or context.grid_service == null:
		return {
			"unit_ids": threat_unit_ids,
			"count": 0,
			"expected_damage": 0,
			"expected_damage_by_unit_id": expected_damage_by_unit_id,
		}
	var actor_coord: Vector2i = actor_anchor_coord if actor_anchor_coord != Vector2i(-1, -1) else context.unit_state.coord
	for unit_variant in context.state.units.values():
		var threat_unit := unit_variant as BattleUnitState
		if threat_unit == null or not threat_unit.is_alive:
			continue
		if threat_unit.faction_id == context.unit_state.faction_id:
			continue
		if suppressed_threat_ids.has(threat_unit.unit_id):
			continue
		var threat_profile := _get_unit_threat_profile(context, threat_unit)
		var threat_range := int(threat_profile.get("range", 0))
		if threat_range <= 0:
			continue
		var distance_to_actor := _distance_from_anchor_to_unit(context, actor_coord, threat_unit)
		if distance_to_actor < 0 or distance_to_actor > threat_range:
			continue
		threat_unit_ids.append(threat_unit.unit_id)
		var threat_damage := _estimate_threat_profile_damage_at_distance(threat_profile, distance_to_actor)
		expected_damage_by_unit_id[threat_unit.unit_id] = threat_damage
		expected_damage += threat_damage
	threat_unit_ids.sort_custom(func(left: StringName, right: StringName) -> bool:
		return String(left) < String(right)
	)
	return {
		"unit_ids": threat_unit_ids,
		"count": threat_unit_ids.size(),
		"expected_damage": expected_damage,
		"expected_damage_by_unit_id": expected_damage_by_unit_id,
	}


func _get_unit_threat_profile(context, threat_unit: BattleUnitState) -> Dictionary:
	if context == null or context.unit_state == null or threat_unit == null:
		return {}
	var cache_key := "%s->%s" % [String(threat_unit.unit_id), String(context.unit_state.unit_id)]
	if context.score_projection_cache.has(cache_key):
		var cached = context.score_projection_cache.get(cache_key, {})
		return cached if cached is Dictionary else {}
	var profile := _build_unit_threat_profile(context, threat_unit)
	context.score_projection_cache[cache_key] = profile
	return profile


func _build_unit_threat_profile(context, threat_unit: BattleUnitState) -> Dictionary:
	var skill_entries: Array[Dictionary] = []
	var best_range := 0
	if context == null or context.unit_state == null or threat_unit == null:
		return {
			"range": 0,
			"skill_entries": skill_entries,
			"weapon_range": 0,
			"weapon_damage": 0,
		}
	for skill_id in threat_unit.known_active_skill_ids:
		var normalized_skill_id := ProgressionDataUtils.to_string_name(skill_id)
		if normalized_skill_id == &"":
			continue
		var skill_def = context.skill_defs.get(normalized_skill_id) as SkillDef
		if skill_def == null or skill_def.combat_profile == null:
			continue
		if ProgressionDataUtils.to_string_name(skill_def.combat_profile.target_team_filter) == &"ally":
			continue
		var effect_defs := _collect_role_threat_effect_defs(threat_unit, skill_def)
		if not _is_damage_skill(effect_defs) and not _is_control_skill(effect_defs):
			continue
		var skill_range := BATTLE_RANGE_SERVICE_SCRIPT.get_effective_skill_threat_range(threat_unit, skill_def)
		if skill_range <= 0:
			continue
		var damage := _estimate_damage_for_target(threat_unit, effect_defs, context.unit_state, normalized_skill_id)
		skill_entries.append({
			"range": skill_range,
			"damage": damage,
		})
		best_range = maxi(best_range, skill_range)
	var weapon_range := BATTLE_RANGE_SERVICE_SCRIPT.get_weapon_attack_range(threat_unit)
	var weapon_damage := _estimate_weapon_average_damage(threat_unit)
	if weapon_range > 0:
		best_range = maxi(best_range, weapon_range)
	return {
		"range": best_range,
		"skill_entries": skill_entries,
		"weapon_range": weapon_range,
		"weapon_damage": weapon_damage,
	}


func _estimate_threat_profile_damage_at_distance(threat_profile: Dictionary, distance_to_actor: int) -> int:
	var best_damage := 0
	var skill_entries_value = threat_profile.get("skill_entries", [])
	if skill_entries_value is Array:
		for entry_variant in skill_entries_value:
			if entry_variant is not Dictionary:
				continue
			var entry := entry_variant as Dictionary
			if distance_to_actor >= 0 and int(entry.get("range", 0)) < distance_to_actor:
				continue
			best_damage = maxi(best_damage, int(entry.get("damage", 0)))
	if distance_to_actor < 0 or int(threat_profile.get("weapon_range", 0)) >= distance_to_actor:
		best_damage = maxi(best_damage, int(threat_profile.get("weapon_damage", 0)))
	return best_damage


func _estimate_unit_threat_damage_to_actor(context, threat_unit: BattleUnitState, distance_to_actor: int) -> int:
	if context == null or context.unit_state == null or threat_unit == null:
		return 0
	var best_damage := 0
	for skill_id in threat_unit.known_active_skill_ids:
		var normalized_skill_id := ProgressionDataUtils.to_string_name(skill_id)
		if normalized_skill_id == &"":
			continue
		var skill_def = context.skill_defs.get(normalized_skill_id) as SkillDef
		if skill_def == null or skill_def.combat_profile == null:
			continue
		if ProgressionDataUtils.to_string_name(skill_def.combat_profile.target_team_filter) == &"ally":
			continue
		if distance_to_actor >= 0 and BATTLE_RANGE_SERVICE_SCRIPT.get_effective_skill_threat_range(threat_unit, skill_def) < distance_to_actor:
			continue
		var effect_defs := _collect_role_threat_effect_defs(threat_unit, skill_def)
		if effect_defs.is_empty():
			continue
		var damage := _estimate_damage_for_target(threat_unit, effect_defs, context.unit_state, normalized_skill_id)
		best_damage = maxi(best_damage, damage)
	if distance_to_actor < 0 or BATTLE_RANGE_SERVICE_SCRIPT.get_weapon_attack_range(threat_unit) >= distance_to_actor:
		best_damage = maxi(best_damage, _estimate_weapon_average_damage(threat_unit))
	return best_damage


func _estimate_weapon_average_damage(threat_unit: BattleUnitState) -> int:
	if threat_unit == null:
		return 0
	var dice := threat_unit.weapon_two_handed_dice if threat_unit.weapon_uses_two_hands else threat_unit.weapon_one_handed_dice
	if dice.is_empty():
		return 0
	var dice_count := maxi(int(dice.get("dice_count", 0)), 0)
	var dice_sides := maxi(int(dice.get("dice_sides", 0)), 0)
	if dice_count <= 0 or dice_sides <= 0:
		return maxi(int(dice.get("flat_bonus", 0)), 0)
	var flat_bonus := int(dice.get("flat_bonus", 0))
	return maxi(int(round(float(dice_count * (dice_sides + 1)) / 2.0 + float(flat_bonus))), 0)


func _append_unique_string_name(target_ids: Array[StringName], unit_id: StringName) -> void:
	if unit_id == &"":
		return
	if not target_ids.has(unit_id):
		target_ids.append(unit_id)


func _copy_string_name_array(value: Variant) -> Array[StringName]:
	var result: Array[StringName] = []
	if value is not Array:
		return result
	for item in value:
		var normalized := ProgressionDataUtils.to_string_name(item)
		if normalized != &"":
			result.append(normalized)
	return result


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


func _build_position_objective_score(
	position_objective_kind: StringName,
	distance_value: int,
	desired_min_distance: int,
	desired_max_distance: int,
	current_distance_value: int = -1
) -> int:
	if distance_value < 0 or desired_min_distance < 0 or desired_max_distance < 0:
		return 0
	if position_objective_kind == &"distance_band_progress":
		return _build_distance_band_progress_score(
			distance_value,
			desired_min_distance,
			desired_max_distance,
			current_distance_value
		)
	if position_objective_kind == &"distance_floor":
		if distance_value < desired_min_distance:
			return -((desired_min_distance - distance_value) * _score_profile.position_undershoot_penalty)
		return _score_profile.position_base_score \
			+ (distance_value - desired_min_distance) * _score_profile.position_distance_step
	if distance_value >= desired_min_distance and distance_value <= desired_max_distance:
		return maxi(_score_profile.position_base_score - distance_value * _score_profile.position_distance_step, 0)
	if distance_value < desired_min_distance:
		return -((desired_min_distance - distance_value) * _score_profile.position_undershoot_penalty)
	return -((distance_value - desired_max_distance) * _score_profile.position_overshoot_penalty)


func _build_distance_band_progress_score(
	distance_value: int,
	desired_min_distance: int,
	desired_max_distance: int,
	current_distance_value: int
) -> int:
	var candidate_gap := _build_distance_gap(distance_value, desired_min_distance, desired_max_distance)
	if candidate_gap < 0:
		return 0
	var current_gap := _build_distance_gap(current_distance_value, desired_min_distance, desired_max_distance)
	if current_gap < 0:
		return _build_distance_band_absolute_score(distance_value, desired_min_distance, desired_max_distance)
	if current_gap == 0:
		return _build_distance_band_absolute_score(distance_value, desired_min_distance, desired_max_distance)
	if candidate_gap < current_gap:
		var progress_steps := current_gap - candidate_gap
		return _score_profile.position_base_score + progress_steps * _score_profile.position_distance_step
	if candidate_gap == current_gap:
		return -_score_profile.position_distance_step
	return -((candidate_gap - current_gap) * _score_profile.position_overshoot_penalty)


func _build_distance_gap(distance_value: int, desired_min_distance: int, desired_max_distance: int) -> int:
	if distance_value < 0 or desired_min_distance < 0 or desired_max_distance < 0:
		return -1
	if distance_value < desired_min_distance:
		return desired_min_distance - distance_value
	if distance_value > desired_max_distance:
		return distance_value - desired_max_distance
	return 0


func _build_distance_band_absolute_score(
	distance_value: int,
	desired_min_distance: int,
	desired_max_distance: int
) -> int:
	if distance_value >= desired_min_distance and distance_value <= desired_max_distance:
		return maxi(_score_profile.position_base_score - distance_value * _score_profile.position_distance_step, 0)
	if distance_value < desired_min_distance:
		return -((desired_min_distance - distance_value) * _score_profile.position_undershoot_penalty)
	return -((distance_value - desired_max_distance) * _score_profile.position_overshoot_penalty)


func _resolve_action_base_score(action_kind: StringName, metadata: Dictionary) -> int:
	if metadata.has("action_base_score"):
		return int(metadata.get("action_base_score", 0))
	return _score_profile.get_action_base_score(action_kind) if _score_profile != null else 0


func _resolve_action_target_count(score_input: BattleAiScoreInput) -> int:
	if score_input == null:
		return 0
	if score_input.target_count > 0:
		return score_input.target_count
	if not score_input.target_unit_ids.is_empty():
		return score_input.target_unit_ids.size()
	if not score_input.target_coords.is_empty():
		return score_input.target_coords.size()
	return 0
