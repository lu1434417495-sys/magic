class_name BattleSpecialProfileRegistry
extends RefCounted

const BattleSpecialProfileManifest = preload("res://scripts/systems/battle/core/special_profiles/battle_special_profile_manifest.gd")
const BattleSpecialProfileManifestValidator = preload("res://scripts/systems/battle/core/special_profiles/battle_special_profile_manifest_validator.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")

const MANIFEST_DIRECTORY := "res://data/configs/skill_special_profiles/manifests"

var _manifest_directory := MANIFEST_DIRECTORY
var _manifests_by_profile_id: Dictionary = {}
var _profile_id_by_skill_id: Dictionary = {}
var _validation_errors: Array[String] = []
var _validator = BattleSpecialProfileManifestValidator.new()


func set_manifest_directory(directory_path: String) -> void:
	_manifest_directory = directory_path if not directory_path.is_empty() else MANIFEST_DIRECTORY


func rebuild(skill_defs: Dictionary, as_of_date: String = "") -> void:
	_manifests_by_profile_id.clear()
	_profile_id_by_skill_id.clear()
	_validation_errors.clear()

	var special_profile_id_by_skill_id := _collect_special_profile_ids(skill_defs)
	var has_special_skills := not special_profile_id_by_skill_id.is_empty()
	if not DirAccess.dir_exists_absolute(ProjectSettings.globalize_path(_manifest_directory)):
		if has_special_skills:
			_validation_errors.append("BattleSpecialProfileRegistry could not find %s." % _manifest_directory)
			_append_missing_manifest_errors(special_profile_id_by_skill_id)
		return

	var directory := DirAccess.open(_manifest_directory)
	if directory == null:
		_validation_errors.append("BattleSpecialProfileRegistry could not open %s." % _manifest_directory)
		return

	directory.list_dir_begin()
	while true:
		var entry_name := directory.get_next()
		if entry_name.is_empty():
			break
		if entry_name == "." or entry_name == "..":
			continue
		if directory.current_is_dir():
			continue
		if not entry_name.ends_with(".tres") and not entry_name.ends_with(".res"):
			continue
		_register_manifest_resource("%s/%s" % [_manifest_directory, entry_name], skill_defs, as_of_date)
	directory.list_dir_end()

	_append_missing_manifest_errors(special_profile_id_by_skill_id)


func validate() -> Array[String]:
	return _validation_errors.duplicate()


func get_manifest(profile_id: StringName):
	return _manifests_by_profile_id.get(profile_id)


func get_manifest_for_skill(skill_id: StringName):
	var profile_id := _profile_id_by_skill_id.get(skill_id, &"") as StringName
	if profile_id == &"":
		return null
	return get_manifest(profile_id)


func has_profile(profile_id: StringName) -> bool:
	return _manifests_by_profile_id.has(profile_id)


func get_snapshot() -> Dictionary:
	var profiles: Dictionary = {}
	for profile_id in _manifests_by_profile_id.keys():
		var manifest := _manifests_by_profile_id.get(profile_id) as BattleSpecialProfileManifest
		if manifest == null:
			continue
		var owning_skill_ids: Array[String] = []
		for skill_id in manifest.owning_skill_ids:
			owning_skill_ids.append(String(skill_id))
		profiles[String(profile_id)] = {
			"profile_id": String(manifest.profile_id),
			"runtime_resolver_id": String(manifest.runtime_resolver_id),
			"owning_skill_ids": owning_skill_ids,
			"profile_resource": manifest.profile_resource,
			"presentation_metadata": manifest.presentation_metadata.duplicate(true),
			"required_regression_tests": manifest.required_regression_tests.duplicate(),
		}

	var profile_id_by_skill_id: Dictionary = {}
	for skill_id in _profile_id_by_skill_id.keys():
		profile_id_by_skill_id[String(skill_id)] = String(_profile_id_by_skill_id.get(skill_id, &""))
	return {
		"ok": _validation_errors.is_empty(),
		"errors": _validation_errors.duplicate(),
		"profiles": profiles,
		"profile_id_by_skill_id": profile_id_by_skill_id,
	}


func _register_manifest_resource(resource_path: String, skill_defs: Dictionary, as_of_date: String) -> void:
	var resource := load(resource_path)
	if resource == null:
		_validation_errors.append("BattleSpecialProfileRegistry failed to load %s." % resource_path)
		return
	var manifest := resource as BattleSpecialProfileManifest
	if manifest == null:
		_validation_errors.append("BattleSpecialProfileRegistry %s is not a BattleSpecialProfileManifest." % resource_path)
		return
	if manifest.profile_id == &"":
		_validation_errors.append("BattleSpecialProfileRegistry %s is missing profile_id." % resource_path)
		return
	if _manifests_by_profile_id.has(manifest.profile_id):
		_validation_errors.append("Duplicate battle special profile_id registered: %s" % String(manifest.profile_id))
		return
	_manifests_by_profile_id[manifest.profile_id] = manifest

	_append_profile_resource_path_errors(manifest)
	_validation_errors.append_array(_validator.validate_manifest(manifest, skill_defs, as_of_date))

	for skill_id in manifest.owning_skill_ids:
		if skill_id == &"":
			continue
		if _profile_id_by_skill_id.has(skill_id):
			_validation_errors.append("Duplicate battle special profile owning_skill_id registered: %s" % String(skill_id))
			continue
		_profile_id_by_skill_id[skill_id] = manifest.profile_id


func _append_profile_resource_path_errors(manifest: BattleSpecialProfileManifest) -> void:
	if manifest.profile_resource == null:
		return
	var profile_path := String(manifest.profile_resource.resource_path)
	if profile_path.is_empty():
		_validation_errors.append("Battle special profile %s profile_resource must be saved under the sibling profiles directory." % String(manifest.profile_id))
		return
	var expected_prefix := "%s/profiles/" % _manifest_directory.get_base_dir()
	if not profile_path.begins_with(expected_prefix):
		_validation_errors.append("Battle special profile %s profile_resource must be under %s." % [
			String(manifest.profile_id),
			expected_prefix,
		])


func _append_missing_manifest_errors(special_profile_id_by_skill_id: Dictionary) -> void:
	for skill_id in special_profile_id_by_skill_id.keys():
		var profile_id := special_profile_id_by_skill_id.get(skill_id, &"") as StringName
		if profile_id == &"":
			continue
		if not _manifests_by_profile_id.has(profile_id):
			_validation_errors.append("Battle special profile %s is missing manifest for skill %s." % [
				String(profile_id),
				String(skill_id),
			])
			continue
		if _profile_id_by_skill_id.get(skill_id, &"") != profile_id:
			_validation_errors.append("Battle special profile %s manifest does not own skill %s." % [
				String(profile_id),
				String(skill_id),
			])


func _collect_special_profile_ids(skill_defs: Dictionary) -> Dictionary:
	var result: Dictionary = {}
	for skill_id_variant in skill_defs.keys():
		var skill_id := skill_id_variant as StringName
		var skill_def := skill_defs.get(skill_id) as SkillDef
		if skill_def == null or skill_def.combat_profile == null:
			continue
		var profile_id: StringName = skill_def.combat_profile.special_resolution_profile_id
		if profile_id == &"":
			continue
		result[skill_id] = profile_id
	return result
