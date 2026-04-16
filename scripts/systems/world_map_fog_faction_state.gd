class_name WorldMapFogFactionState
extends RefCounted

var visible_now: Dictionary = {}
var explored: Dictionary = {}


func clear_visible() -> void:
	visible_now.clear()


func mark_visible(coord: Vector2i) -> void:
	visible_now[coord] = true
	explored[coord] = true


func is_visible(coord: Vector2i) -> bool:
	return visible_now.has(coord)


func is_explored(coord: Vector2i) -> bool:
	return explored.has(coord)
