class_name ProfessionRankRequirement
extends Resource

@export var target_rank := 1
@export var required_tag_rules: Array[TagRequirement] = []
@export var required_profession_ranks: Array[ProfessionRankGate] = []


func is_empty() -> bool:
	return required_tag_rules.is_empty() and required_profession_ranks.is_empty()

