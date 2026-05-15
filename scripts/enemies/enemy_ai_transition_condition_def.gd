class_name EnemyAiTransitionConditionDef
extends Resource

const HP_BASIS_POINTS_DENOMINATOR := 10000

const PREDICATE_ALWAYS: StringName = &"always"
const PREDICATE_CURRENT_STATE_IS: StringName = &"current_state_is"
const PREDICATE_SELF_HP_AT_OR_BELOW: StringName = &"self_hp_at_or_below_basis_points"
const PREDICATE_ALLY_HP_AT_OR_BELOW: StringName = &"ally_hp_at_or_below_basis_points"
const PREDICATE_NEAREST_ENEMY_DISTANCE_AT_OR_BELOW: StringName = &"nearest_enemy_distance_at_or_below"
const PREDICATE_HAS_SKILL_AFFORDANCE: StringName = &"has_skill_affordance"

const VALID_PREDICATES := {
	PREDICATE_ALWAYS: true,
	PREDICATE_CURRENT_STATE_IS: true,
	PREDICATE_SELF_HP_AT_OR_BELOW: true,
	PREDICATE_ALLY_HP_AT_OR_BELOW: true,
	PREDICATE_NEAREST_ENEMY_DISTANCE_AT_OR_BELOW: true,
	PREDICATE_HAS_SKILL_AFFORDANCE: true,
}

@export var predicate: StringName = &""
@export var basis_points := -1
@export var max_distance := -1
@export var state_ids: Array[StringName] = []
@export var affordances: Array[StringName] = []


func validate_schema(owner_label: String, declared_state_ids: Dictionary = {}) -> Array[String]:
	var errors: Array[String] = []
	if predicate == &"":
		errors.append("%s transition condition is missing predicate." % owner_label)
		return errors
	if not VALID_PREDICATES.has(predicate):
		errors.append("%s transition condition uses unsupported predicate %s." % [owner_label, String(predicate)])
		return errors
	match predicate:
		PREDICATE_ALWAYS:
			pass
		PREDICATE_CURRENT_STATE_IS:
			if state_ids.is_empty():
				errors.append("%s current_state_is condition must declare state_ids." % owner_label)
			for state_id in state_ids:
				if state_id == &"":
					errors.append("%s current_state_is condition contains empty state_id." % owner_label)
				elif not declared_state_ids.is_empty() and not declared_state_ids.has(state_id):
					errors.append("%s current_state_is condition references undeclared state_id %s." % [owner_label, String(state_id)])
		PREDICATE_SELF_HP_AT_OR_BELOW, PREDICATE_ALLY_HP_AT_OR_BELOW:
			if basis_points < 0 or basis_points > HP_BASIS_POINTS_DENOMINATOR:
				errors.append("%s %s condition basis_points must be within [0, 10000]." % [owner_label, String(predicate)])
		PREDICATE_NEAREST_ENEMY_DISTANCE_AT_OR_BELOW:
			if max_distance < 0:
				errors.append("%s nearest_enemy_distance_at_or_below condition max_distance must be >= 0." % owner_label)
		PREDICATE_HAS_SKILL_AFFORDANCE:
			if affordances.is_empty():
				errors.append("%s has_skill_affordance condition must declare affordances." % owner_label)
			for affordance in affordances:
				if affordance == &"":
					errors.append("%s has_skill_affordance condition contains empty affordance." % owner_label)
	return errors


func to_trace_dict() -> Dictionary:
	return {
		"predicate": String(predicate),
		"basis_points": int(basis_points),
		"max_distance": int(max_distance),
		"state_ids": _string_name_array_to_strings(state_ids),
		"affordances": _string_name_array_to_strings(affordances),
	}


func to_signature() -> String:
	return "%s(bp=%d,dist=%d,states=%s,affordances=%s)" % [
		String(predicate),
		int(basis_points),
		int(max_distance),
		",".join(_string_name_array_to_strings(state_ids)),
		",".join(_string_name_array_to_strings(affordances)),
	]


static func _string_name_array_to_strings(values: Array[StringName]) -> Array[String]:
	var results: Array[String] = []
	for value in values:
		results.append(String(value))
	return results
