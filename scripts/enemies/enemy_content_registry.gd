class_name EnemyContentRegistry
extends RefCounted

const ENEMY_CONTENT_SEED_RESOURCE_PATH := "res://data/configs/enemies/enemy_content_seed.tres"
const ENEMY_BRAIN_CONFIG_DIRECTORY := "res://data/configs/enemies/brains"
const ENEMY_TEMPLATE_CONFIG_DIRECTORY := "res://data/configs/enemies/templates"
const WILD_ENCOUNTER_ROSTER_CONFIG_DIRECTORY := "res://data/configs/enemies/rosters"

const ENEMY_CONTENT_SEED_SCRIPT = preload("res://scripts/enemies/enemy_content_seed.gd")
const ENEMY_AI_BRAIN_DEF_SCRIPT = preload("res://scripts/enemies/enemy_ai_brain_def.gd")
const ENEMY_TEMPLATE_DEF_SCRIPT = preload("res://scripts/enemies/enemy_template_def.gd")
const WILD_ENCOUNTER_ROSTER_DEF_SCRIPT = preload("res://scripts/enemies/wild_encounter_roster_def.gd")
const ItemContentRegistry = preload("res://scripts/player/warehouse/item_content_registry.gd")
const EnemyContentSeed = preload("res://scripts/enemies/enemy_content_seed.gd")
const EnemyAiBrainDef = preload("res://scripts/enemies/enemy_ai_brain_def.gd")
const EnemyTemplateDef = preload("res://scripts/enemies/enemy_template_def.gd")
const WildEncounterRosterDef = preload("res://scripts/enemies/wild_encounter_roster_def.gd")

var _enemy_templates: Dictionary = {}
var _enemy_ai_brains: Dictionary = {}
var _wild_encounter_rosters: Dictionary = {}
var _validation_errors: Array[String] = []

var _enemy_content_seed_resource_path := ENEMY_CONTENT_SEED_RESOURCE_PATH
var _enemy_template_directory := ENEMY_TEMPLATE_CONFIG_DIRECTORY
var _enemy_ai_brain_directory := ENEMY_BRAIN_CONFIG_DIRECTORY
var _wild_encounter_roster_directory := WILD_ENCOUNTER_ROSTER_CONFIG_DIRECTORY


func _init() -> void:
	rebuild()


func configure_seed_resource(
	seed_resource_path: String = ENEMY_CONTENT_SEED_RESOURCE_PATH,
	rebuild_now: bool = true
) -> void:
	_enemy_content_seed_resource_path = seed_resource_path
	if rebuild_now:
		rebuild()


func configure_directories(
	template_directory: String = ENEMY_TEMPLATE_CONFIG_DIRECTORY,
	brain_directory: String = ENEMY_BRAIN_CONFIG_DIRECTORY,
	roster_directory: String = WILD_ENCOUNTER_ROSTER_CONFIG_DIRECTORY,
	rebuild_now: bool = true
) -> void:
	_enemy_content_seed_resource_path = ""
	_enemy_template_directory = template_directory
	_enemy_ai_brain_directory = brain_directory
	_wild_encounter_roster_directory = roster_directory
	if rebuild_now:
		rebuild()


func rebuild() -> void:
	_enemy_templates.clear()
	_enemy_ai_brains.clear()
	_wild_encounter_rosters.clear()
	_validation_errors.clear()
	if not _enemy_content_seed_resource_path.is_empty():
		_register_seed_resource(_enemy_content_seed_resource_path)
	else:
		_scan_directory(
			_enemy_ai_brain_directory,
			Callable(self, "_register_brain_resource"),
			"EnemyContentRegistry brain scan"
		)
		_scan_directory(
			_enemy_template_directory,
			Callable(self, "_register_template_resource"),
			"EnemyContentRegistry template scan"
		)
		_scan_directory(
			_wild_encounter_roster_directory,
			Callable(self, "_register_wild_encounter_roster_resource"),
			"EnemyContentRegistry roster scan"
		)
	_validation_errors.append_array(_collect_validation_errors())


func get_enemy_templates() -> Dictionary:
	return _enemy_templates


func get_enemy_ai_brains() -> Dictionary:
	return _enemy_ai_brains


func get_wild_encounter_rosters() -> Dictionary:
	return _wild_encounter_rosters


func validate() -> Array[String]:
	return _validation_errors.duplicate()


func _register_seed_resource(resource_path: String) -> void:
	var resource := load(resource_path)
	if resource == null:
		_validation_errors.append("Failed to load enemy content seed %s." % resource_path)
		return
	if resource.get_script() != ENEMY_CONTENT_SEED_SCRIPT:
		_validation_errors.append("Enemy content seed %s is not an EnemyContentSeed." % resource_path)
		return

	var seed_resource := resource as EnemyContentSeed
	if seed_resource == null:
		_validation_errors.append("Enemy content seed %s failed to cast to EnemyContentSeed." % resource_path)
		return

	for brain_variant in seed_resource.enemy_ai_brains:
		_register_brain_entry(brain_variant, "%s::enemy_ai_brains" % resource_path)
	for template_variant in seed_resource.enemy_templates:
		_register_template_entry(template_variant, "%s::enemy_templates" % resource_path)
	for roster_variant in seed_resource.wild_encounter_rosters:
		_register_wild_encounter_roster_entry(roster_variant, "%s::wild_encounter_rosters" % resource_path)


func _scan_directory(directory_path: String, register_callback: Callable, scan_label: String) -> void:
	if not DirAccess.dir_exists_absolute(ProjectSettings.globalize_path(directory_path)):
		_validation_errors.append("%s could not find %s." % [scan_label, directory_path])
		return

	var directory := DirAccess.open(directory_path)
	if directory == null:
		_validation_errors.append("%s could not open %s." % [scan_label, directory_path])
		return

	directory.list_dir_begin()
	while true:
		var entry_name := directory.get_next()
		if entry_name.is_empty():
			break
		if entry_name == "." or entry_name == "..":
			continue

		var entry_path := "%s/%s" % [directory_path, entry_name]
		if directory.current_is_dir():
			_scan_directory(entry_path, register_callback, scan_label)
			continue
		if not entry_name.ends_with(".tres") and not entry_name.ends_with(".res"):
			continue
		register_callback.call(entry_path)
	directory.list_dir_end()


func _register_brain_resource(resource_path: String) -> void:
	var resource := load(resource_path)
	_register_brain_entry(resource, resource_path)


func _register_template_resource(resource_path: String) -> void:
	var resource := load(resource_path)
	_register_template_entry(resource, resource_path)


func _register_wild_encounter_roster_resource(resource_path: String) -> void:
	var resource := load(resource_path)
	_register_wild_encounter_roster_entry(resource, resource_path)

func _register_brain_entry(resource: Variant, source_label: String) -> void:
	if resource == null:
		_validation_errors.append("Failed to load enemy brain config %s." % source_label)
		return
	if resource is not Resource:
		_validation_errors.append("Enemy brain config %s is not an EnemyAiBrainDef." % source_label)
		return
	if resource.get_script() != ENEMY_AI_BRAIN_DEF_SCRIPT:
		_validation_errors.append("Enemy brain config %s is not an EnemyAiBrainDef." % source_label)
		return

	var brain := resource as EnemyAiBrainDef
	if brain == null:
		_validation_errors.append("Enemy brain config %s failed to cast to EnemyAiBrainDef." % source_label)
		return
	if brain.brain_id == &"":
		_validation_errors.append("Enemy brain config %s is missing brain_id." % source_label)
		return
	if _enemy_ai_brains.has(brain.brain_id):
		_validation_errors.append("Duplicate enemy brain_id registered: %s" % String(brain.brain_id))
		return
	_enemy_ai_brains[brain.brain_id] = brain


func _register_template_entry(resource: Variant, source_label: String) -> void:
	if resource == null:
		_validation_errors.append("Failed to load enemy template config %s." % source_label)
		return
	if resource is not Resource:
		_validation_errors.append("Enemy template config %s is not an EnemyTemplateDef." % source_label)
		return
	if resource.get_script() != ENEMY_TEMPLATE_DEF_SCRIPT:
		_validation_errors.append("Enemy template config %s is not an EnemyTemplateDef." % source_label)
		return

	var template := resource as EnemyTemplateDef
	if template == null:
		_validation_errors.append("Enemy template config %s failed to cast to EnemyTemplateDef." % source_label)
		return
	if template.template_id == &"":
		_validation_errors.append("Enemy template config %s is missing template_id." % source_label)
		return
	if _enemy_templates.has(template.template_id):
		_validation_errors.append("Duplicate enemy template_id registered: %s" % String(template.template_id))
		return
	_enemy_templates[template.template_id] = template


func _register_wild_encounter_roster_entry(resource: Variant, source_label: String) -> void:
	if resource == null:
		_validation_errors.append("Failed to load wild encounter roster config %s." % source_label)
		return
	if resource is not Resource:
		_validation_errors.append("Wild encounter roster config %s is not a WildEncounterRosterDef." % source_label)
		return
	if resource.get_script() != WILD_ENCOUNTER_ROSTER_DEF_SCRIPT:
		_validation_errors.append("Wild encounter roster config %s is not a WildEncounterRosterDef." % source_label)
		return

	var roster := resource as WildEncounterRosterDef
	if roster == null:
		_validation_errors.append("Wild encounter roster config %s failed to cast to WildEncounterRosterDef." % source_label)
		return
	if roster.profile_id == &"":
		_validation_errors.append("Wild encounter roster config %s is missing profile_id." % source_label)
		return
	if _wild_encounter_rosters.has(roster.profile_id):
		_validation_errors.append("Duplicate wild encounter profile_id registered: %s" % String(roster.profile_id))
		return
	_wild_encounter_rosters[roster.profile_id] = roster


func _collect_validation_errors() -> Array[String]:
	var errors: Array[String] = []

	for brain_key in ProgressionDataUtils.sorted_string_keys(_enemy_ai_brains):
		var brain_id := StringName(brain_key)
		var brain := _enemy_ai_brains.get(brain_id) as EnemyAiBrainDef
		if brain == null:
			continue
		errors.append_array(brain.validate_schema())

	var item_defs := _get_item_defs_for_validation()
	for template_key in ProgressionDataUtils.sorted_string_keys(_enemy_templates):
		var template_id := StringName(template_key)
		var template := _enemy_templates.get(template_id) as EnemyTemplateDef
		if template == null:
			continue
		errors.append_array(template.validate_schema(_enemy_ai_brains, item_defs))

	for roster_key in ProgressionDataUtils.sorted_string_keys(_wild_encounter_rosters):
		var roster_id := StringName(roster_key)
		var roster := _wild_encounter_rosters.get(roster_id) as WildEncounterRosterDef
		if roster == null:
			continue
		errors.append_array(roster.validate_schema(_enemy_templates))

	return errors


func _get_item_defs_for_validation() -> Dictionary:
	var item_registry := ItemContentRegistry.new()
	return item_registry.get_item_defs()
