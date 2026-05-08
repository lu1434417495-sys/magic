class_name WorldTimeSystem
extends RefCounted

const STEPS_PER_DAY := 15


func get_world_step(world_data: Dictionary) -> int:
	if not _has_valid_world_step(world_data):
		return -1
	return int(world_data["world_step"])


func get_world_day(world_data: Dictionary) -> int:
	var step := get_world_step(world_data)
	if step < 0:
		return -1
	return step / STEPS_PER_DAY


static func step_to_day(world_step: int) -> int:
	if world_step < 0:
		return -1
	return world_step / STEPS_PER_DAY


func advance(world_data: Dictionary, delta_steps: int) -> Dictionary:
	var old_step := get_world_step(world_data)
	if old_step < 0:
		return {
			"old_step": -1,
			"new_step": -1,
			"old_day": -1,
			"new_day": -1,
			"changed": false,
			"day_changed": false,
			"days_elapsed": 0,
			"error_code": "invalid_world_step",
		}
	var old_day := step_to_day(old_step)
	var next_step := old_step + maxi(delta_steps, 0)
	var new_day := step_to_day(next_step)
	if world_data != null:
		world_data["world_step"] = next_step
	return {
		"old_step": old_step,
		"new_step": next_step,
		"old_day": old_day,
		"new_day": new_day,
		"changed": next_step != old_step,
		"day_changed": new_day != old_day,
		"days_elapsed": new_day - old_day,
	}


func _has_valid_world_step(world_data: Dictionary) -> bool:
	return world_data != null \
		and world_data.has("world_step") \
		and world_data["world_step"] is int \
		and int(world_data["world_step"]) >= 0
