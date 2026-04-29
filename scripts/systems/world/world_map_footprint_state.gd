class_name WorldMapFootprintState
extends RefCounted

const SCRIPT = preload("res://scripts/systems/world/world_map_footprint_state.gd")

var origin: Vector2i = Vector2i.ZERO
var size: Vector2i = Vector2i.ZERO


static func create(next_origin: Vector2i, next_size: Vector2i) -> WorldMapFootprintState:
	var state := SCRIPT.new()
	state.origin = next_origin
	state.size = next_size
	return state


func is_empty() -> bool:
	return size.x <= 0 or size.y <= 0
