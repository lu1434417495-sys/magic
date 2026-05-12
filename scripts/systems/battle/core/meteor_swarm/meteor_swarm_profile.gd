class_name MeteorSwarmProfile
extends Resource

@export var coverage_shape_id: StringName = &"square_7x7"
@export var radius: int = 3
@export var profile_version: int = 1
@export var impact_components: Array[Resource] = []
@export var concussed_status_id: StringName = &"meteor_concussed"
@export var terrain_profiles: Array[Dictionary] = []
@export var friendly_fire_soft_expected_hp_percent := 10
@export var friendly_fire_hard_expected_hp_percent := 25
@export var friendly_fire_hard_worst_case_hp_percent := 50


func get_impact_components() -> Array:
	var components: Array = []
	for component_variant in impact_components:
		if component_variant != null:
			components.append(component_variant)
	return components


func get_terrain_profiles_for_ring(ring: int) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	for terrain_profile_variant in terrain_profiles:
		if terrain_profile_variant is not Dictionary:
			continue
		var terrain_profile := terrain_profile_variant as Dictionary
		var ring_min := int(terrain_profile.get("ring_min", terrain_profile.get(&"ring_min", 0)))
		var ring_max := int(terrain_profile.get("ring_max", terrain_profile.get(&"ring_max", 0)))
		if ring >= ring_min and ring <= ring_max:
			result.append(terrain_profile.duplicate(true))
	return result
