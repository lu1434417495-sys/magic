class_name WorldTimeSystem
extends RefCounted


func get_world_step(world_data: Dictionary) -> int:
	if not _has_valid_world_step(world_data):
		return -1
	return int(world_data["world_step"])


func advance(world_data: Dictionary, delta_steps: int) -> Dictionary:
	var old_step := get_world_step(world_data)
	if old_step < 0:
		return {
			"old_step": -1,
			"new_step": -1,
			"changed": false,
			"error_code": "invalid_world_step",
		}
	var next_step := old_step + maxi(delta_steps, 0)
	if world_data != null:
		world_data["world_step"] = next_step
	return {
		"old_step": old_step,
		"new_step": next_step,
		"changed": next_step != old_step,
	}


func _has_valid_world_step(world_data: Dictionary) -> bool:
	return world_data != null \
		and world_data.has("world_step") \
		and world_data["world_step"] is int \
		and int(world_data["world_step"]) >= 0
