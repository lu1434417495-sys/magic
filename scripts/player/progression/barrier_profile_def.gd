class_name BarrierProfileDef
extends Resource

const BarrierLayerDef = preload("res://scripts/player/progression/barrier_layer_def.gd")

@export var profile_id: StringName = &""
@export var display_name := ""
@export var anchor_mode: StringName = &"fixed"
@export var area_pattern: StringName = &"diamond"
@export var radius_cells := 0
@export var duration_tu := 0
@export var catch_all_projected_effects := false
@export var layers: Array[BarrierLayerDef] = []


func get_ordered_layers() -> Array[BarrierLayerDef]:
	var ordered: Array[BarrierLayerDef] = []
	for layer in layers:
		if layer != null:
			ordered.append(layer)
	ordered.sort_custom(func(left: BarrierLayerDef, right: BarrierLayerDef) -> bool:
		return int(left.order) < int(right.order)
	)
	return ordered
