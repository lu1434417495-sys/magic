class_name EnemyAiGenerationSlotDef
extends Resource

const VALID_AFFORDANCES := {
	"unit_hostile.damage": true,
	"unit_hostile.control": true,
	"ground_hostile.aoe": true,
	"ground_control": true,
	"terrain_control": true,
	"displacement_control": true,
	"charge_engage": true,
	"charge_path_aoe": true,
	"multi_unit": true,
	"random_chain": true,
	"special_ground": true,
	"ally_heal": true,
	"self_or_ally_buff": true,
	"reposition": true,
	"escape": true,
	"utility": true,
	"breaker": true,
}
const VALID_ACTION_FAMILIES := {
	"use_unit_skill": true,
	"use_ground_skill": true,
	"use_multi_unit_skill": true,
	"use_random_chain_skill": true,
	"use_charge": true,
	"use_charge_path_aoe": true,
	"move_to_range": true,
	"move_to_multi_unit_skill_position": true,
}
const VALID_SLOT_ROLES := {
	"offense": true,
	"control": true,
	"support": true,
	"positioning": true,
	"survival": true,
	"engage": true,
}
const VALID_TARGET_SELECTORS := {
	"": true,
	"nearest_enemy": true,
	"lowest_hp_enemy": true,
	"nearest_role_threat_enemy": true,
	"nearest_ally": true,
	"lowest_hp_ally": true,
	"self": true,
}
const VALID_DISTANCE_REFERENCES := {
	"": true,
	"target_unit": true,
	"target_coord": true,
	"candidate_pool": true,
	"enemy_frontline": true,
}
const VALID_SUPPRESSION_POLICIES := {
	"suppress_matching_family": true,
	"allow_companion": true,
	"manual_only": true,
}

@export var slot_id: StringName = &""
@export var slot_role: StringName = &"offense"
@export var order: int = 0
@export var allowed_affordances: Array[StringName] = []
@export var action_families: Array[StringName] = []
@export var style_template_action_id: StringName = &""
@export var score_bucket_id: StringName = &""
@export var target_selector: StringName = &""
@export var desired_min_distance: int = -1
@export var desired_max_distance: int = -1
@export var distance_reference: StringName = &""
@export var suppression_policy: StringName = &"suppress_matching_family"


func matches_affordance(record: Dictionary, action_family: StringName) -> bool:
	if not action_families.has(action_family):
		return false
	if allowed_affordances.is_empty():
		return false
	var record_affordances = record.get("affordances", [])
	if record_affordances is not Array:
		return false
	for affordance in record_affordances:
		if allowed_affordances.has(ProgressionDataUtils.to_string_name(affordance)):
			return true
	return false


func to_signature() -> Dictionary:
	return {
		"slot_id": String(slot_id),
		"slot_role": String(slot_role),
		"order": order,
		"allowed_affordances": _stringify_array(allowed_affordances),
		"action_families": _stringify_array(action_families),
		"style_template_action_id": String(style_template_action_id),
		"score_bucket_id": String(score_bucket_id),
		"target_selector": String(target_selector),
		"desired_min_distance": desired_min_distance,
		"desired_max_distance": desired_max_distance,
		"distance_reference": String(distance_reference),
		"suppression_policy": String(suppression_policy),
	}


func validate_schema(context_label: String = "Enemy AI generation slot", state_actions: Array = []) -> Array[String]:
	var errors: Array[String] = []
	var label := "%s generation slot %s" % [context_label, String(slot_id)]
	if slot_id == &"":
		errors.append("%s is missing slot_id." % context_label)
	if not VALID_SLOT_ROLES.has(String(slot_role)):
		errors.append("%s declares unsupported slot_role %s." % [label, String(slot_role)])
	if order < 0:
		errors.append("%s order must be >= 0." % label)
	if allowed_affordances.is_empty():
		errors.append("%s must declare at least one allowed_affordance." % label)
	for affordance in allowed_affordances:
		if not VALID_AFFORDANCES.has(String(affordance)):
			errors.append("%s declares unsupported affordance %s." % [label, String(affordance)])
	if action_families.is_empty():
		errors.append("%s must declare at least one action_family." % label)
	for family in action_families:
		if not VALID_ACTION_FAMILIES.has(String(family)):
			errors.append("%s declares unsupported action_family %s." % [label, String(family)])
	if style_template_action_id != &"" and _find_action_by_id(state_actions, style_template_action_id) == null:
		errors.append("%s style_template_action_id %s does not exist in the same state." % [label, String(style_template_action_id)])
	if not VALID_TARGET_SELECTORS.has(String(target_selector)):
		errors.append("%s declares unsupported target_selector %s." % [label, String(target_selector)])
	if desired_min_distance < -1:
		errors.append("%s desired_min_distance must be >= -1." % label)
	if desired_max_distance < -1:
		errors.append("%s desired_max_distance must be >= -1." % label)
	if desired_min_distance >= 0 and desired_max_distance >= 0 and desired_min_distance > desired_max_distance:
		errors.append("%s desired_min_distance cannot exceed desired_max_distance." % label)
	if not VALID_DISTANCE_REFERENCES.has(String(distance_reference)):
		errors.append("%s declares unsupported distance_reference %s." % [label, String(distance_reference)])
	if not VALID_SUPPRESSION_POLICIES.has(String(suppression_policy)):
		errors.append("%s declares unsupported suppression_policy %s." % [label, String(suppression_policy)])
	return errors


func _find_action_by_id(state_actions: Array, expected_action_id: StringName):
	for action in state_actions:
		if action != null and ProgressionDataUtils.to_string_name(action.get("action_id")) == expected_action_id:
			return action
	return null


func _stringify_array(values: Array) -> Array[String]:
	var result: Array[String] = []
	for value in values:
		result.append(String(value))
	result.sort()
	return result
