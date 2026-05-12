class_name BattleAttackRollModifierBundle
extends RefCounted

const BattleAttackRollModifierSpec = preload("res://scripts/systems/battle/core/battle_attack_roll_modifier_spec.gd")

var total_bonus: int = 0
var total_penalty: int = 0
var breakdown: Array[BattleAttackRollModifierSpec] = []


func is_empty() -> bool:
	return total_bonus == 0 and total_penalty == 0 and breakdown.is_empty()


func add_spec(spec: BattleAttackRollModifierSpec) -> void:
	if spec == null:
		return
	if int(spec.modifier_delta) == 0:
		return
	breakdown.append(spec)
	if int(spec.modifier_delta) > 0:
		total_bonus += int(spec.modifier_delta)
	else:
		total_penalty += absi(int(spec.modifier_delta))


func get_effective_modifier_delta() -> int:
	return total_bonus - total_penalty


func get_breakdown_payload() -> Array[Dictionary]:
	var payloads: Array[Dictionary] = []
	for spec in breakdown:
		if spec == null:
			continue
		payloads.append(spec.to_dict(int(spec.modifier_delta)))
	return payloads


func to_dict() -> Dictionary:
	return {
		"total_bonus": total_bonus,
		"total_penalty": total_penalty,
		"effective_modifier_delta": get_effective_modifier_delta(),
		"breakdown": get_breakdown_payload(),
	}
