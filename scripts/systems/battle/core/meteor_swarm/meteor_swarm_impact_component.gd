class_name MeteorSwarmImpactComponent
extends Resource

@export var component_id: StringName = &""
@export var role_label: StringName = &""
@export var damage_tag: StringName = &""
@export var base_power: int = 0
@export var dice_count: int = 0
@export var dice_sides: int = 0
@export var ring_weight: float = 1.0
@export var save_profile_id: StringName = &""
@export var can_crit: bool = false
@export var mastery_weight: float = 1.0
@export var ring_min: int = 0
@export var ring_max: int = 3
@export var ring_damage_scale_bp: Dictionary = {}


func applies_to_distance(distance_from_anchor: int, center_direct: bool = false) -> bool:
	if component_id == &"center_direct":
		return center_direct
	return distance_from_anchor >= ring_min and distance_from_anchor <= ring_max


func get_damage_scale(distance_from_anchor: int) -> float:
	var key := str(distance_from_anchor)
	var raw_value = ring_damage_scale_bp.get(distance_from_anchor, ring_damage_scale_bp.get(key, int(round(ring_weight * 10000.0))))
	return maxf(float(raw_value) / 10000.0, 0.0)


func get_average_base_damage(distance_from_anchor: int) -> int:
	var dice_average := float(maxi(dice_count, 0)) * (float(maxi(dice_sides, 0)) + 1.0) / 2.0
	return maxi(int(round((float(base_power) + dice_average) * get_damage_scale(distance_from_anchor))), 0)


func get_worst_case_base_damage(distance_from_anchor: int) -> int:
	var dice_worst := maxi(dice_count, 0) * maxi(dice_sides, 0)
	return maxi(int(round(float(base_power + dice_worst) * get_damage_scale(distance_from_anchor))), 0)


func to_component_fact(distance_from_anchor: int) -> Dictionary:
	return {
		"component_id": String(component_id),
		"role_label": String(role_label),
		"damage_tag": String(damage_tag),
		"base_power": base_power,
		"dice_count": dice_count,
		"dice_sides": dice_sides,
		"damage_scale": get_damage_scale(distance_from_anchor),
		"save_profile_id": String(save_profile_id),
		"can_crit": can_crit,
		"mastery_weight": mastery_weight,
		"ring_min": ring_min,
		"ring_max": ring_max,
	}
