class_name WildSpawnRule
extends Resource

@export var region_tag: String = ""
@export var monster_name: String = "野怪"
@export var density_per_chunk := 1
@export var min_distance_to_settlement := 2
@export var vision_range := 1
@export var chunk_coords: Array[Vector2i] = []
