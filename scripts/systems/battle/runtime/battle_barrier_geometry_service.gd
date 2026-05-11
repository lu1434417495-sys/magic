class_name BattleBarrierGeometryService
extends RefCounted


func classify_footprint_transition(
	state,
	from_footprint: Array,
	to_footprint: Array,
	barrier_coords: Array
) -> Dictionary:
	var barrier_lookup := _coord_lookup(barrier_coords)
	var from_inside := _footprint_overlaps_lookup(from_footprint, barrier_lookup)
	var to_inside := _footprint_overlaps_lookup(to_footprint, barrier_lookup)
	return {
		"crosses_boundary": from_inside != to_inside,
		"from_inside": from_inside,
		"to_inside": to_inside,
	}


func line_crosses_barrier_area(
	state,
	source_coord: Vector2i,
	target_coord: Vector2i,
	barrier_coords: Array
) -> bool:
	var barrier_lookup := _coord_lookup(barrier_coords)
	var source_inside := barrier_lookup.has(source_coord)
	var target_inside := barrier_lookup.has(target_coord)
	if source_inside and target_inside:
		return false
	if source_inside != target_inside:
		return true
	for coord in _line_coords(source_coord, target_coord):
		if coord == source_coord or coord == target_coord:
			continue
		if barrier_lookup.has(coord):
			return true
	return false


func coord_inside_barrier(coord: Vector2i, barrier_coords: Array) -> bool:
	return _coord_lookup(barrier_coords).has(coord)


func _footprint_overlaps_lookup(footprint: Array, lookup: Dictionary) -> bool:
	for coord_variant in footprint:
		if coord_variant is Vector2i and lookup.has(coord_variant):
			return true
	return false


func _coord_lookup(coords: Array) -> Dictionary:
	var lookup: Dictionary = {}
	for coord_variant in coords:
		if coord_variant is Vector2i:
			lookup[coord_variant] = true
	return lookup


func _line_coords(from_coord: Vector2i, to_coord: Vector2i) -> Array[Vector2i]:
	var coords: Array[Vector2i] = []
	var x0 := from_coord.x
	var y0 := from_coord.y
	var x1 := to_coord.x
	var y1 := to_coord.y
	var dx := absi(x1 - x0)
	var sx := 1 if x0 < x1 else -1
	var dy := -absi(y1 - y0)
	var sy := 1 if y0 < y1 else -1
	var error := dx + dy
	while true:
		coords.append(Vector2i(x0, y0))
		if x0 == x1 and y0 == y1:
			break
		var doubled_error := 2 * error
		if doubled_error >= dy:
			error += dy
			x0 += sx
		if doubled_error <= dx:
			error += dx
			y0 += sy
	return coords
