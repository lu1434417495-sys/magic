class_name FacilityConfig
extends Resource

@export var facility_id: String = ""
@export var display_name: String = ""
@export var category: String = ""
@export var min_settlement_tier := 0
@export var allowed_slot_tags: Array[String] = []
@export var bound_service_npcs: Array = []
@export var interaction_type: String = ""


func get_primary_service_name() -> String:
	if bound_service_npcs.is_empty():
		return interaction_type.capitalize()

	return bound_service_npcs[0].service_type.capitalize()
