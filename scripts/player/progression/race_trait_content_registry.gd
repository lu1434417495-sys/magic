class_name RaceTraitContentRegistry
extends "res://scripts/player/progression/identity_content_registry_base.gd"

const RACE_TRAIT_CONFIG_DIRECTORY := "res://data/configs/race_traits"
const RACE_TRAIT_DEF_SCRIPT = preload("res://scripts/player/progression/race_trait_def.gd")
const TRAIT_TRIGGER_HOOKS_SCRIPT = preload("res://scripts/systems/battle/runtime/trait_trigger_hooks.gd")

var _race_trait_defs: Dictionary = {}


func _init() -> void:
	_registry_label = "RaceTraitContentRegistry"
	rebuild()


func rebuild() -> void:
	load_from_directory(RACE_TRAIT_CONFIG_DIRECTORY)


func load_from_directory(directory_path: String) -> void:
	_race_trait_defs.clear()
	_validation_errors.clear()
	_scan_directory(directory_path)
	_validation_errors.append_array(_collect_validation_errors())


func get_race_trait_defs() -> Dictionary:
	return _race_trait_defs


func _register_resource(resource_path: String) -> void:
	var resource := load(resource_path)
	if resource == null:
		_validation_errors.append("Failed to load race trait config %s." % resource_path)
		return
	if resource.get_script() != RACE_TRAIT_DEF_SCRIPT:
		_validation_errors.append("Race trait config %s is not a RaceTraitDef." % resource_path)
		return

	var trait_def := resource as RaceTraitDef
	if trait_def == null:
		_validation_errors.append("Race trait config %s failed to cast to RaceTraitDef." % resource_path)
		return
	if trait_def.trait_id == &"":
		_validation_errors.append("Race trait config %s is missing trait_id." % resource_path)
		return
	if _race_trait_defs.has(trait_def.trait_id):
		_validation_errors.append("Duplicate race trait_id registered: %s" % String(trait_def.trait_id))
		return

	_race_trait_defs[trait_def.trait_id] = trait_def


func _collect_validation_errors() -> Array[String]:
	var errors: Array[String] = []
	for trait_key in _sorted_registry_keys(_race_trait_defs):
		var trait_id := StringName(trait_key)
		var trait_def := _race_trait_defs.get(trait_id) as RaceTraitDef
		if trait_def == null:
			continue
		_append_trait_validation_errors(errors, trait_id, trait_def)
	return errors


func _append_trait_validation_errors(errors: Array[String], trait_id: StringName, trait_def: RaceTraitDef) -> void:
	var owner_label := "RaceTrait %s" % String(trait_id)
	_append_string_name_field_error(errors, owner_label, "trait_id", trait_def.trait_id)
	_append_string_field_error(errors, owner_label, "display_name", trait_def.display_name)
	_append_string_field_error(errors, owner_label, "description", trait_def.description)
	_append_string_name_field_error(errors, owner_label, "trigger_type", trait_def.trigger_type)
	if not TRAIT_TRIGGER_HOOKS_SCRIPT.VALID_TRIGGER_TYPES.has(trait_def.trigger_type):
		errors.append("%s uses unsupported trigger_type %s." % [owner_label, String(trait_def.trigger_type)])
	_append_string_name_field_error(errors, owner_label, "effect_type", trait_def.effect_type)
	if not RaceTraitDef.VALID_EFFECT_TYPES.has(trait_def.effect_type):
		errors.append("%s uses unsupported effect_type %s." % [owner_label, String(trait_def.effect_type)])
	if typeof(trait_def.params) != TYPE_DICTIONARY:
		errors.append("%s.params must be a Dictionary." % owner_label)
	else:
		for key_variant in trait_def.params.keys():
			if not _is_string_or_string_name(key_variant):
				errors.append("%s.params key %s must be a String or StringName." % [owner_label, str(key_variant)])
				continue
			if String(key_variant).strip_edges().is_empty():
				errors.append("%s.params has an empty key." % owner_label)
