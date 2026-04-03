class_name VisionSourceData
extends RefCounted

var source_id: String
var center: Vector2i
var range: int
var faction_id: String


func _init(
	source_identifier: String = "",
	source_center: Vector2i = Vector2i.ZERO,
	source_range: int = 0,
	source_faction_id: String = ""
) -> void:
	source_id = source_identifier
	center = source_center
	range = source_range
	faction_id = source_faction_id
