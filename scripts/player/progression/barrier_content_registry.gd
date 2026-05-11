class_name BarrierContentRegistry
extends RefCounted

const BARRIER_CONFIG_DIRECTORY := "res://data/configs/barriers"
const BarrierProfileDef = preload("res://scripts/player/progression/barrier_profile_def.gd")

var _profile_defs: Dictionary = {}
var _validation_errors: Array[String] = []


func _init() -> void:
	rebuild()


func rebuild() -> void:
	_profile_defs.clear()
	_validation_errors.clear()
	_scan_directory(BARRIER_CONFIG_DIRECTORY)


func get_profile_defs() -> Dictionary:
	return _profile_defs.duplicate()


func get_profile_def(profile_id: StringName) -> BarrierProfileDef:
	if profile_id == &"":
		return null
	return _profile_defs.get(profile_id) as BarrierProfileDef


func validate() -> Array[String]:
	return _validation_errors.duplicate()


func _scan_directory(directory_path: String) -> void:
	if not DirAccess.dir_exists_absolute(ProjectSettings.globalize_path(directory_path)):
		_validation_errors.append("BarrierContentRegistry could not find %s." % directory_path)
		return
	var directory := DirAccess.open(directory_path)
	if directory == null:
		_validation_errors.append("BarrierContentRegistry could not open %s." % directory_path)
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
			_scan_directory(entry_path)
			continue
		if entry_name.ends_with(".tres") or entry_name.ends_with(".res"):
			_register_profile_resource(entry_path)
	directory.list_dir_end()


func _register_profile_resource(resource_path: String) -> void:
	var resource := load(resource_path)
	var profile := resource as BarrierProfileDef
	if profile == null:
		_validation_errors.append("Barrier profile %s must use BarrierProfileDef." % resource_path)
		return
	if profile.profile_id == &"":
		_validation_errors.append("Barrier profile %s must declare profile_id." % resource_path)
		return
	if _profile_defs.has(profile.profile_id):
		_validation_errors.append("Duplicate barrier profile_id %s." % String(profile.profile_id))
		return
	_profile_defs[profile.profile_id] = profile
