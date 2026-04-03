class_name WorldMapCellData
extends RefCounted

var coord: Vector2i
var chunk_coord: Vector2i
var terrain_visual_type: String
var occupant_id: String = ""
var footprint_root_id: String = ""


func _init(
	cell_coord: Vector2i = Vector2i.ZERO,
	cell_chunk_coord: Vector2i = Vector2i.ZERO,
	cell_terrain_visual_type: String = "plains"
) -> void:
	coord = cell_coord
	chunk_coord = cell_chunk_coord
	terrain_visual_type = cell_terrain_visual_type
