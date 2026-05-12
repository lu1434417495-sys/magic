class_name BattleSpecialProfileManifestValidator
extends RefCounted

const BattleSpecialProfileManifest = preload("res://scripts/systems/battle/core/special_profiles/battle_special_profile_manifest.gd")
const MeteorSwarmImpactComponent = preload("res://scripts/systems/battle/core/meteor_swarm/meteor_swarm_impact_component.gd")
const MeteorSwarmProfile = preload("res://scripts/systems/battle/core/meteor_swarm/meteor_swarm_profile.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")

const METEOR_SWARM_PROFILE_ID: StringName = &"meteor_swarm"
const METEOR_SWARM_RESOLVER_ID: StringName = &"meteor_swarm"
const RUNTIME_READ_POLICY_FORBIDDEN: StringName = &"forbidden"
const FORBIDDEN_FALLBACK_FIELDS := ["active_fallbacks", "fallbacks", "legacy_bridge"]
const ALLOWED_METEOR_SAVE_PROFILE_IDS := {
	&"": true,
	&"meteor_dex_half": true,
}
const ALLOWED_TERRAIN_PROFILE_KEYS := {
	"terrain_profile_id": true,
	"ring_min": true,
	"ring_max": true,
	"move_cost_delta": true,
	"move_cost_stack_key": true,
	"move_cost_stack_mode": true,
	"lifetime_policy": true,
	"duration_tu": true,
	"tick_interval_tu": true,
	"tick_effect_type": true,
	"accuracy_modifier_spec": true,
	"render_overlay_id": true,
	"overlay_priority": true,
}
const REQUIRED_TERRAIN_PROFILE_KEYS := [
	"terrain_profile_id",
	"ring_min",
	"ring_max",
	"move_cost_delta",
	"lifetime_policy",
	"duration_tu",
	"tick_interval_tu",
	"tick_effect_type",
	"render_overlay_id",
]
const ALLOWED_ACCURACY_MODIFIER_KEYS := {
	"source_domain": true,
	"label": true,
	"modifier_delta": true,
	"stack_key": true,
	"stack_mode": true,
	"roll_kind_filter": true,
	"endpoint_mode": true,
	"distance_min_exclusive": true,
	"distance_max_inclusive": true,
	"target_team_filter": true,
	"footprint_mode": true,
	"applies_to": true,
}


func validate_manifest(manifest: Resource, skill_defs: Dictionary, as_of_date: String = "") -> Array[String]:
	var errors: Array[String] = []
	var typed_manifest := manifest as BattleSpecialProfileManifest
	if typed_manifest == null:
		errors.append("Battle special profile manifest failed to cast to BattleSpecialProfileManifest.")
		return errors

	_append_forbidden_fallback_errors(errors, typed_manifest)
	if typed_manifest.profile_id == &"":
		errors.append("Battle special profile manifest is missing profile_id.")
	if int(typed_manifest.schema_version) != 1:
		errors.append("Battle special profile %s uses unsupported schema_version %d." % [
			String(typed_manifest.profile_id),
			int(typed_manifest.schema_version),
		])
	if typed_manifest.runtime_read_policy != RUNTIME_READ_POLICY_FORBIDDEN:
		errors.append("Battle special profile %s must use runtime_read_policy forbidden." % String(typed_manifest.profile_id))
	if typed_manifest.runtime_resolver_id == &"":
		errors.append("Battle special profile %s is missing runtime_resolver_id." % String(typed_manifest.profile_id))
	if typed_manifest.owning_skill_ids.is_empty():
		errors.append("Battle special profile %s must declare at least one owning_skill_id." % String(typed_manifest.profile_id))

	match typed_manifest.profile_id:
		METEOR_SWARM_PROFILE_ID:
			if typed_manifest.runtime_resolver_id != METEOR_SWARM_RESOLVER_ID:
				errors.append("Battle special profile meteor_swarm must use runtime_resolver_id meteor_swarm.")
			var meteor_profile := typed_manifest.profile_resource as MeteorSwarmProfile
			if meteor_profile == null:
				errors.append("Battle special profile meteor_swarm profile_resource must be MeteorSwarmProfile.")
			else:
				errors.append_array(validate_meteor_swarm_profile(meteor_profile, true))
		_:
			if typed_manifest.profile_resource is MeteorSwarmProfile:
				errors.append("Battle special profile %s cannot use MeteorSwarmProfile." % String(typed_manifest.profile_id))

	for skill_id in typed_manifest.owning_skill_ids:
		if skill_id == &"":
			errors.append("Battle special profile %s declares an empty owning_skill_id." % String(typed_manifest.profile_id))
			continue
		if not skill_defs.has(skill_id):
			errors.append("Battle special profile %s references missing owning skill %s." % [
				String(typed_manifest.profile_id),
				String(skill_id),
			])
			continue
		var skill_def := skill_defs.get(skill_id) as SkillDef
		if skill_def == null or skill_def.combat_profile == null:
			errors.append("Battle special profile %s owning skill %s is missing combat_profile." % [
				String(typed_manifest.profile_id),
				String(skill_id),
			])
			continue
		if skill_def.combat_profile.special_resolution_profile_id != typed_manifest.profile_id:
			errors.append("Battle special profile %s owning skill %s must set matching special_resolution_profile_id." % [
				String(typed_manifest.profile_id),
				String(skill_id),
			])
		_append_special_skill_effect_surface_errors(errors, skill_id, skill_def)

	for test_path in typed_manifest.required_regression_tests:
		if String(test_path).strip_edges().is_empty():
			errors.append("Battle special profile %s declares an empty required_regression_tests path." % String(typed_manifest.profile_id))
			continue
		if not _resource_file_exists(String(test_path)):
			errors.append("Battle special profile %s required regression test path does not exist: %s." % [
				String(typed_manifest.profile_id),
				String(test_path),
			])
		if not _is_default_regression_suite_member(String(test_path)):
			errors.append("Battle special profile %s required regression test must be a default regression suite member: %s." % [
				String(typed_manifest.profile_id),
				String(test_path),
			])

	# Kept for the explicit signature; dates are metadata until a profile defines sunset policy.
	if not as_of_date.is_empty():
		pass
	return errors


func validate_meteor_swarm_profile(profile: MeteorSwarmProfile, require_runtime_data: bool = false) -> Array[String]:
	var errors: Array[String] = []
	if profile == null:
		errors.append("MeteorSwarmProfile is required.")
		return errors
	var radius := int(profile.radius)
	if profile.coverage_shape_id != &"square_7x7":
		errors.append("MeteorSwarmProfile.coverage_shape_id must be square_7x7.")
	if radius != 3:
		errors.append("MeteorSwarmProfile.radius must be 3.")
	if int(profile.friendly_fire_soft_expected_hp_percent) < 0:
		errors.append("MeteorSwarmProfile.friendly_fire_soft_expected_hp_percent must be >= 0.")
	if int(profile.friendly_fire_hard_expected_hp_percent) < 0:
		errors.append("MeteorSwarmProfile.friendly_fire_hard_expected_hp_percent must be >= 0.")
	if int(profile.friendly_fire_hard_worst_case_hp_percent) < 0:
		errors.append("MeteorSwarmProfile.friendly_fire_hard_worst_case_hp_percent must be >= 0.")
	if int(profile.friendly_fire_hard_expected_hp_percent) < int(profile.friendly_fire_soft_expected_hp_percent):
		errors.append("MeteorSwarmProfile.friendly_fire_hard_expected_hp_percent must be >= soft threshold.")
	if int(profile.friendly_fire_hard_worst_case_hp_percent) < int(profile.friendly_fire_hard_expected_hp_percent):
		errors.append("MeteorSwarmProfile.friendly_fire_hard_worst_case_hp_percent must be >= hard expected threshold.")

	if require_runtime_data and profile.impact_components.is_empty():
		errors.append("MeteorSwarmProfile.impact_components must be non-empty for runtime resolution.")
	if require_runtime_data and profile.terrain_profiles.is_empty():
		errors.append("MeteorSwarmProfile.terrain_profiles must be non-empty for runtime resolution.")

	var seen_component_ids := {}
	for component_index in range(profile.impact_components.size()):
		var component := profile.impact_components[component_index] as MeteorSwarmImpactComponent
		if component != null and component.component_id != &"":
			if seen_component_ids.has(component.component_id):
				errors.append("MeteorSwarmProfile.impact_components[%d].component_id is duplicated: %s." % [
					component_index,
					String(component.component_id),
				])
			else:
				seen_component_ids[component.component_id] = component_index
		_append_impact_component_errors(errors, profile.impact_components[component_index], component_index, radius)
	for terrain_index in range(profile.terrain_profiles.size()):
		_append_terrain_profile_errors(errors, profile.terrain_profiles[terrain_index], terrain_index, radius)
	return errors


func _append_impact_component_errors(errors: Array[String], component_resource: Resource, component_index: int, radius: int) -> void:
	var component := component_resource as MeteorSwarmImpactComponent
	if component == null:
		errors.append("MeteorSwarmProfile.impact_components[%d] must be MeteorSwarmImpactComponent." % component_index)
		return
	if component.component_id == &"":
		errors.append("MeteorSwarmProfile.impact_components[%d].component_id must not be empty." % component_index)
	if component.role_label == &"":
		errors.append("MeteorSwarmProfile.impact_components[%d].role_label must not be empty." % component_index)
	if component.damage_tag == &"":
		errors.append("MeteorSwarmProfile.impact_components[%d].damage_tag must not be empty." % component_index)
	if int(component.base_power) < 0:
		errors.append("MeteorSwarmProfile.impact_components[%d].base_power must be >= 0." % component_index)
	if int(component.dice_count) < 0:
		errors.append("MeteorSwarmProfile.impact_components[%d].dice_count must be >= 0." % component_index)
	if int(component.dice_sides) < 0:
		errors.append("MeteorSwarmProfile.impact_components[%d].dice_sides must be >= 0." % component_index)
	if int(component.dice_count) <= 0 and int(component.base_power) <= 0:
		errors.append("MeteorSwarmProfile.impact_components[%d] must declare dice or base_power." % component_index)
	if int(component.dice_count) > 0 and int(component.dice_sides) <= 0:
		errors.append("MeteorSwarmProfile.impact_components[%d].dice_sides must be > 0 when dice_count > 0." % component_index)
	if int(component.ring_min) < 0 or int(component.ring_max) < int(component.ring_min) or int(component.ring_max) > radius:
		errors.append("MeteorSwarmProfile.impact_components[%d] ring range is invalid or outside radius." % component_index)
	if component.mastery_weight < 0.0:
		errors.append("MeteorSwarmProfile.impact_components[%d].mastery_weight must be >= 0." % component_index)
	if not ALLOWED_METEOR_SAVE_PROFILE_IDS.has(component.save_profile_id):
		errors.append("MeteorSwarmProfile.impact_components[%d].save_profile_id is unsupported: %s." % [
			component_index,
			String(component.save_profile_id),
		])


func _append_special_skill_effect_surface_errors(errors: Array[String], skill_id: StringName, skill_def: SkillDef) -> void:
	var combat_profile = skill_def.combat_profile
	if combat_profile == null:
		return
	if not combat_profile.effect_defs.is_empty():
		errors.append("Battle special profile owning skill %s must not declare executable combat_profile.effect_defs." % String(skill_id))
	for variant_index in range(combat_profile.cast_variants.size()):
		var cast_variant = combat_profile.cast_variants[variant_index]
		if cast_variant == null:
			continue
		if not cast_variant.effect_defs.is_empty():
			errors.append("Battle special profile owning skill %s must not declare executable cast_variants[%d].effect_defs." % [
				String(skill_id),
				variant_index,
			])


func _append_forbidden_fallback_errors(errors: Array[String], manifest: BattleSpecialProfileManifest) -> void:
	for property_name in FORBIDDEN_FALLBACK_FIELDS:
		if _resource_has_property(manifest, property_name):
			errors.append("Battle special profile %s declares forbidden fallback field %s." % [
				String(manifest.profile_id),
				property_name,
			])
	if manifest.resource_path.is_empty():
		return
	var file := FileAccess.open(manifest.resource_path, FileAccess.READ)
	if file == null:
		return
	var text := file.get_as_text()
	for field_name in FORBIDDEN_FALLBACK_FIELDS:
		if text.contains(field_name):
			errors.append("Battle special profile %s resource text contains forbidden fallback field %s." % [
				String(manifest.profile_id),
				field_name,
			])


func _append_terrain_profile_errors(errors: Array[String], profile_entry: Dictionary, terrain_index: int, radius: int) -> void:
	for key_variant in profile_entry.keys():
		var key := String(key_variant)
		if key == "accuracy_modifer_spec":
			errors.append("MeteorSwarmProfile.terrain_profiles[%d] uses misspelled accuracy_modifer_spec." % terrain_index)
			continue
		if not ALLOWED_TERRAIN_PROFILE_KEYS.has(key):
			errors.append("MeteorSwarmProfile.terrain_profiles[%d] uses unsupported key %s." % [terrain_index, key])
	for required_key in REQUIRED_TERRAIN_PROFILE_KEYS:
		if not profile_entry.has(required_key) and not profile_entry.has(StringName(required_key)):
			errors.append("MeteorSwarmProfile.terrain_profiles[%d] is missing %s." % [terrain_index, required_key])
	var terrain_profile_id = profile_entry.get("terrain_profile_id", profile_entry.get(&"terrain_profile_id", ""))
	if not _is_string_like(terrain_profile_id) or String(terrain_profile_id).is_empty():
		errors.append("MeteorSwarmProfile.terrain_profiles[%d].terrain_profile_id must be String/StringName." % terrain_index)
	var ring_min_value = profile_entry.get("ring_min", profile_entry.get(&"ring_min", 0))
	var ring_max_value = profile_entry.get("ring_max", profile_entry.get(&"ring_max", 0))
	if not _is_int_value(ring_min_value):
		errors.append("MeteorSwarmProfile.terrain_profiles[%d].ring_min must be int." % terrain_index)
	if not _is_int_value(ring_max_value):
		errors.append("MeteorSwarmProfile.terrain_profiles[%d].ring_max must be int." % terrain_index)
	if _is_int_value(ring_min_value) and _is_int_value(ring_max_value):
		var ring_min := int(ring_min_value)
		var ring_max := int(ring_max_value)
		if ring_min < 0 or ring_max < ring_min or ring_max > radius:
			errors.append("MeteorSwarmProfile.terrain_profiles[%d] ring range is invalid or outside radius." % terrain_index)
	if not _is_int_value(profile_entry.get("move_cost_delta", profile_entry.get(&"move_cost_delta", 0))):
		errors.append("MeteorSwarmProfile.terrain_profiles[%d].move_cost_delta must be int." % terrain_index)
	var lifetime_policy := _to_string_name(profile_entry.get("lifetime_policy", profile_entry.get(&"lifetime_policy", &"")))
	if lifetime_policy != &"battle" and lifetime_policy != &"timed":
		errors.append("MeteorSwarmProfile.terrain_profiles[%d].lifetime_policy must be battle or timed." % terrain_index)
	if not _is_int_value(profile_entry.get("duration_tu", profile_entry.get(&"duration_tu", 0))):
		errors.append("MeteorSwarmProfile.terrain_profiles[%d].duration_tu must be int." % terrain_index)
	if not _is_int_value(profile_entry.get("tick_interval_tu", profile_entry.get(&"tick_interval_tu", 0))):
		errors.append("MeteorSwarmProfile.terrain_profiles[%d].tick_interval_tu must be int." % terrain_index)
	var render_overlay_id = profile_entry.get("render_overlay_id", profile_entry.get(&"render_overlay_id", ""))
	if not _is_string_like(render_overlay_id) or String(render_overlay_id).is_empty():
		errors.append("MeteorSwarmProfile.terrain_profiles[%d].render_overlay_id must be a non-empty String/StringName." % terrain_index)
	var accuracy_spec = profile_entry.get("accuracy_modifier_spec", profile_entry.get(&"accuracy_modifier_spec", null))
	if accuracy_spec != null:
		if accuracy_spec is not Dictionary:
			errors.append("MeteorSwarmProfile.terrain_profiles[%d].accuracy_modifier_spec must be Dictionary." % terrain_index)
		else:
			_append_accuracy_modifier_spec_errors(errors, accuracy_spec as Dictionary, terrain_index)


func _append_accuracy_modifier_spec_errors(errors: Array[String], spec: Dictionary, terrain_index: int) -> void:
	for key_variant in spec.keys():
		var key := String(key_variant)
		if not ALLOWED_ACCURACY_MODIFIER_KEYS.has(key):
			errors.append("MeteorSwarmProfile.terrain_profiles[%d].accuracy_modifier_spec uses unsupported key %s." % [terrain_index, key])
	if not spec.has("modifier_delta") and not spec.has(&"modifier_delta"):
		errors.append("MeteorSwarmProfile.terrain_profiles[%d].accuracy_modifier_spec is missing modifier_delta." % terrain_index)
	elif not _is_int_value(spec.get("modifier_delta", spec.get(&"modifier_delta", 0))):
		errors.append("MeteorSwarmProfile.terrain_profiles[%d].accuracy_modifier_spec.modifier_delta must be int." % terrain_index)


func _resource_has_property(resource: Resource, property_name: String) -> bool:
	for property in resource.get_property_list():
		if String(property.get("name", "")) == property_name:
			return true
	return false


func _resource_file_exists(path: String) -> bool:
	if path.begins_with("res://") or path.begins_with("user://"):
		return FileAccess.file_exists(path)
	return FileAccess.file_exists("res://%s" % path)


func _is_default_regression_suite_member(path: String) -> bool:
	var normalized := path.replace("\\", "/").strip_edges()
	var lower_path := normalized.to_lower()
	if not lower_path.begins_with("tests/"):
		return false
	if lower_path.contains("/tools/") or lower_path.contains("/simulation/") or lower_path.contains("/benchmarks/"):
		return false
	if lower_path.ends_with("benchmark.gd") or lower_path.ends_with("analysis.gd"):
		return false
	return lower_path.get_file().begins_with("run_") and lower_path.ends_with(".gd")


func _is_int_value(value: Variant) -> bool:
	return typeof(value) == TYPE_INT


func _is_string_like(value: Variant) -> bool:
	return typeof(value) == TYPE_STRING or typeof(value) == TYPE_STRING_NAME


func _to_string_name(value: Variant) -> StringName:
	if value is StringName:
		return value
	if value is String:
		return StringName(value)
	return &""
