class_name WorldMapOccupantState
extends RefCounted

const SCRIPT = preload("res://scripts/systems/world/world_map_occupant_state.gd")

var occupant_id: String = ""
var footprint_root_id: String = ""


static func create(next_occupant_id: String, next_footprint_root_id: String = "") -> WorldMapOccupantState:
	var state := SCRIPT.new()
	state.occupant_id = next_occupant_id
	state.footprint_root_id = next_footprint_root_id if not next_footprint_root_id.is_empty() else next_occupant_id
	return state


func is_empty() -> bool:
	return occupant_id.is_empty() and footprint_root_id.is_empty()
