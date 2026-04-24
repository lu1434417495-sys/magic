class_name EnemyTemplateDef
extends Resource

const BattleUnitState = preload("res://scripts/systems/battle_unit_state.gd")
const UnitBaseAttributes = preload("res://scripts/player/progression/unit_base_attributes.gd")

const DROP_TYPE_ITEM: StringName = &"item"
const DROP_TYPE_RANDOM_EQUIPMENT: StringName = &"random_equipment"
const TAG_BEAST: StringName = &"beast"

@export var template_id: StringName = &""
@export var display_name: String = ""
@export var brain_id: StringName = &""
@export var initial_state_id: StringName = &""
@export var enemy_count := 1
@export var body_size := 1
@export var action_threshold := BattleUnitState.DEFAULT_ACTION_THRESHOLD
@export var tags: Array[StringName] = []
@export var base_attribute_overrides: Dictionary = {}
@export var skill_ids: Array[StringName] = []
@export var skill_level_map: Dictionary = {}
@export var attribute_overrides: Dictionary = {}
@export var drop_entries: Array[Dictionary] = []


func get_initial_state_id(brain) -> StringName:
	if initial_state_id != &"":
		return initial_state_id
	if brain != null and brain.has_method("has_state") and brain.has_state(brain.default_state_id):
		return brain.default_state_id
	return &"engage"


func get_drop_entries() -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	for entry_variant in drop_entries:
		if entry_variant is not Dictionary:
			continue
		result.append((entry_variant as Dictionary).duplicate(true))
	return result


func has_tag(tag: StringName) -> bool:
	return tag != &"" and tags.has(tag)


func get_base_attribute_overrides() -> Dictionary:
	var resolved: Dictionary = {}
	for attribute_id in UnitBaseAttributes.BASE_ATTRIBUTE_IDS:
		if base_attribute_overrides.has(attribute_id):
			resolved[attribute_id] = int(base_attribute_overrides.get(attribute_id, 0))
			continue
		var attribute_key := String(attribute_id)
		if base_attribute_overrides.has(attribute_key):
			resolved[attribute_id] = int(base_attribute_overrides.get(attribute_key, 0))
	return resolved


func validate_schema(known_brains: Dictionary = {}) -> Array[String]:
	var errors: Array[String] = []
	if template_id == &"":
		errors.append("Enemy template is missing template_id.")
		return errors
	if display_name.strip_edges().is_empty():
		errors.append("Enemy template %s is missing display_name." % String(template_id))
	if brain_id == &"":
		errors.append("Enemy template %s is missing brain_id." % String(template_id))
	else:
		var brain = known_brains.get(brain_id)
		if brain == null:
			errors.append("Enemy template %s references missing brain %s." % [String(template_id), String(brain_id)])
		elif initial_state_id != &"" and not brain.has_state(initial_state_id):
			errors.append(
				"Enemy template %s initial_state_id %s is not declared by brain %s." % [
					String(template_id),
					String(initial_state_id),
					String(brain_id),
				]
			)
	if enemy_count <= 0:
		errors.append("Enemy template %s must have enemy_count >= 1." % String(template_id))
	if body_size <= 0:
		errors.append("Enemy template %s must have body_size >= 1." % String(template_id))
	if action_threshold <= 0:
		errors.append("Enemy template %s action_threshold must be > 0." % String(template_id))
	elif action_threshold % 5 != 0:
		errors.append("Enemy template %s action_threshold must be a multiple of 5 TU." % String(template_id))
	var explicit_base_attributes := get_base_attribute_overrides()
	if has_tag(TAG_BEAST):
		if not base_attribute_overrides.is_empty():
			errors.append(
				"Enemy template %s is tagged beast and should not define base_attribute_overrides." % String(template_id)
			)
	else:
		for attribute_id in UnitBaseAttributes.BASE_ATTRIBUTE_IDS:
			if not explicit_base_attributes.has(attribute_id):
				errors.append(
					"Enemy template %s is missing base attribute %s." % [String(template_id), String(attribute_id)]
				)
				continue
			if int(explicit_base_attributes.get(attribute_id, 0)) <= 0:
				errors.append(
					"Enemy template %s base attribute %s must be > 0." % [String(template_id), String(attribute_id)]
				)
	for entry_variant in drop_entries:
		if entry_variant is not Dictionary:
			errors.append("Enemy template %s contains a non-Dictionary drop entry." % String(template_id))
			continue
		var entry_data := entry_variant as Dictionary
		var drop_id := ProgressionDataUtils.to_string_name(entry_data.get("drop_id", ""))
		var drop_type := ProgressionDataUtils.to_string_name(entry_data.get("drop_type", ""))
		var item_id := ProgressionDataUtils.to_string_name(entry_data.get("item_id", ""))
		var quantity := int(entry_data.get("quantity", 0))
		if drop_id == &"":
			errors.append("Enemy template %s contains a drop entry without drop_id." % String(template_id))
		if drop_type != DROP_TYPE_ITEM and drop_type != DROP_TYPE_RANDOM_EQUIPMENT:
			errors.append("Enemy template %s drop %s declares unsupported drop_type %s." % [
				String(template_id),
				String(drop_id),
				String(drop_type),
			])
		if item_id == &"":
			errors.append("Enemy template %s drop %s is missing item_id." % [String(template_id), String(drop_id)])
		if quantity <= 0:
			errors.append("Enemy template %s drop %s must have quantity >= 1." % [String(template_id), String(drop_id)])
	return errors
