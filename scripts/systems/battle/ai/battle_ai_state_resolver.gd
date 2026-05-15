class_name BattleAiStateResolver
extends RefCounted

const ENEMY_AI_TRANSITION_CONDITION_DEF_SCRIPT = preload("res://scripts/enemies/enemy_ai_transition_condition_def.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")

const HP_BASIS_POINTS_DENOMINATOR := 10000


func resolve(context, brain) -> Dictionary:
	var previous_state_id := _get_previous_state_id(context)
	var current_state_id := _resolve_current_state_id(context, brain)
	if brain == null:
		return _result(previous_state_id, current_state_id, &"", &"missing_brain", [])
	var rules: Array = _get_sorted_rules(brain)
	if rules.is_empty():
		return _result(previous_state_id, current_state_id, &"", &"no_transition_rules", [])
	for rule in rules:
		if rule == null:
			continue
		if not bool(rule.applies_to_state(current_state_id)):
			continue
		var matched_conditions: Array[Dictionary] = []
		if _rule_matches(context, current_state_id, rule, matched_conditions):
			return _result(
				previous_state_id,
				ProgressionDataUtils.to_string_name(rule.target_state_id),
				ProgressionDataUtils.to_string_name(rule.rule_id),
				&"matched_rule",
				matched_conditions
			)
	return _result(previous_state_id, current_state_id, &"", &"no_matching_rule", [])


func _get_previous_state_id(context) -> StringName:
	if context == null or context.unit_state == null:
		return &""
	return ProgressionDataUtils.to_string_name(context.unit_state.ai_state_id)


func _resolve_current_state_id(context, brain) -> StringName:
	if brain == null:
		return _get_previous_state_id(context)
	var current_state_id := _get_previous_state_id(context)
	if current_state_id != &"" and brain.has_method("has_state") and brain.has_state(current_state_id):
		return current_state_id
	var default_state_id := ProgressionDataUtils.to_string_name(brain.default_state_id)
	if default_state_id != &"" and brain.has_method("has_state") and brain.has_state(default_state_id):
		return default_state_id
	return default_state_id


func _get_sorted_rules(brain) -> Array:
	if brain == null or not brain.has_method("get_transition_rules"):
		return []
	var rules: Array = brain.get_transition_rules()
	rules.sort_custom(func(left, right) -> bool:
		var left_order := int(left.get("order")) if left != null else 0
		var right_order := int(right.get("order")) if right != null else 0
		if left_order != right_order:
			return left_order < right_order
		var left_id := String(left.get("rule_id")) if left != null else ""
		var right_id := String(right.get("rule_id")) if right != null else ""
		if left_id != right_id:
			return left_id < right_id
		return String(left.get("target_state_id")) < String(right.get("target_state_id"))
	)
	return rules


func _rule_matches(context, current_state_id: StringName, rule, matched_conditions: Array[Dictionary]) -> bool:
	if rule == null or not rule.has_method("get_conditions"):
		return false
	for condition in rule.get_conditions():
		if condition == null:
			return false
		if not _condition_matches(context, current_state_id, condition):
			return false
		matched_conditions.append(condition.to_trace_dict())
	return true


func _condition_matches(context, current_state_id: StringName, condition) -> bool:
	match ProgressionDataUtils.to_string_name(condition.predicate):
		ENEMY_AI_TRANSITION_CONDITION_DEF_SCRIPT.PREDICATE_ALWAYS:
			return true
		ENEMY_AI_TRANSITION_CONDITION_DEF_SCRIPT.PREDICATE_CURRENT_STATE_IS:
			return condition.state_ids.has(current_state_id)
		ENEMY_AI_TRANSITION_CONDITION_DEF_SCRIPT.PREDICATE_SELF_HP_AT_OR_BELOW:
			return _is_unit_at_or_below_hp_basis_points(_get_unit_state(context), int(condition.basis_points))
		ENEMY_AI_TRANSITION_CONDITION_DEF_SCRIPT.PREDICATE_ALLY_HP_AT_OR_BELOW:
			return _has_ally_at_or_below_hp_basis_points(context, int(condition.basis_points))
		ENEMY_AI_TRANSITION_CONDITION_DEF_SCRIPT.PREDICATE_NEAREST_ENEMY_DISTANCE_AT_OR_BELOW:
			return _nearest_enemy_distance_at_or_below(context, int(condition.max_distance))
		ENEMY_AI_TRANSITION_CONDITION_DEF_SCRIPT.PREDICATE_HAS_SKILL_AFFORDANCE:
			return context != null and context.has_method("has_skill_affordance") and context.has_skill_affordance(condition.affordances)
	return false


func _get_unit_state(context) -> BattleUnitState:
	if context == null:
		return null
	return context.unit_state as BattleUnitState


func _has_ally_at_or_below_hp_basis_points(context, threshold_basis_points: int) -> bool:
	var unit_state := _get_unit_state(context)
	if context == null or context.state == null or unit_state == null:
		return false
	for unit_variant in context.state.units.values():
		var ally_unit = unit_variant as BattleUnitState
		if ally_unit == null or not bool(ally_unit.is_alive):
			continue
		if ally_unit == unit_state or ally_unit.unit_id == unit_state.unit_id:
			continue
		if ally_unit.faction_id != unit_state.faction_id:
			continue
		if _is_unit_at_or_below_hp_basis_points(ally_unit, threshold_basis_points):
			return true
	return false


func _nearest_enemy_distance_at_or_below(context, max_distance: int) -> bool:
	if max_distance < 0:
		return false
	var unit_state := _get_unit_state(context)
	if context == null or context.state == null or unit_state == null or context.grid_service == null:
		return false
	var candidate_ids = context.state.enemy_unit_ids if unit_state.faction_id == &"player" else context.state.ally_unit_ids
	var best_distance := 999999
	for unit_id in candidate_ids:
		var candidate = context.state.units.get(unit_id) as BattleUnitState
		if candidate == null or not bool(candidate.is_alive):
			continue
		var distance := int(context.grid_service.get_distance_between_units(unit_state, candidate))
		if distance < best_distance:
			best_distance = distance
	return best_distance <= max_distance


func _is_unit_at_or_below_hp_basis_points(unit_state: BattleUnitState, threshold_basis_points: int) -> bool:
	if unit_state == null or unit_state.attribute_snapshot == null:
		return false
	var hp_max := maxi(int(unit_state.attribute_snapshot.get_value(&"hp_max")), 1)
	var clamped_threshold := clampi(threshold_basis_points, 0, HP_BASIS_POINTS_DENOMINATOR)
	var current_hp := clampi(int(unit_state.current_hp), 0, hp_max)
	return current_hp * HP_BASIS_POINTS_DENOMINATOR <= hp_max * clamped_threshold


func _result(
	previous_state_id: StringName,
	state_id: StringName,
	rule_id: StringName,
	reason: StringName,
	matched_conditions: Array[Dictionary]
) -> Dictionary:
	return {
		"previous_state_id": previous_state_id,
		"state_id": state_id,
		"rule_id": rule_id,
		"reason": reason,
		"matched_conditions": matched_conditions.duplicate(true),
	}
