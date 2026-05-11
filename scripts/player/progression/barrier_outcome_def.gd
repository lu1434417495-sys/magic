class_name BarrierOutcomeDef
extends Resource

@export var outcome_type: StringName = &""
@export var amount := 0
@export var damage_tag: StringName = &""
@export var half_on_success := false
@export var success_amount := 0
@export var success_damage_tag: StringName = &""
@export var fatal_damage := 99999
@export var status_id: StringName = &""
@export var save_ability: StringName = &""
@export var save_tag: StringName = &""
@export var save_dc := 0
@export var params: Dictionary = {}


func to_runtime_dict(default_save_dc: int = 0) -> Dictionary:
	var resolved_save_dc := int(save_dc)
	if resolved_save_dc <= 0:
		resolved_save_dc = maxi(int(default_save_dc), 0)
	return {
		"outcome_type": String(outcome_type),
		"amount": int(amount),
		"damage_tag": String(damage_tag),
		"half_on_success": bool(half_on_success),
		"success_amount": int(success_amount),
		"success_damage_tag": String(success_damage_tag),
		"fatal_damage": maxi(int(fatal_damage), 1),
		"status_id": String(status_id),
		"save_ability": String(save_ability),
		"save_tag": String(save_tag),
		"save_dc": resolved_save_dc,
		"params": params.duplicate(true) if params != null else {},
	}
