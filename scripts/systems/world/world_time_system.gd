class_name WorldTimeSystem
extends RefCounted


func get_world_step(world_data: Dictionary) -> int:
	if world_data == null:
		return 0
	return maxi(int(world_data.get("world_step", 0)), 0)


func advance(world_data: Dictionary, delta_steps: int) -> Dictionary:
	var old_step := get_world_step(world_data)
	var next_step := old_step + maxi(delta_steps, 0)
	if world_data != null:
		world_data["world_step"] = next_step
	return {
		"old_step": old_step,
		"new_step": next_step,
		"changed": next_step != old_step,
	}
