class_name ProfessionPromotionRequirement
extends Resource

@export var required_skill_ids: Array[StringName] = []
@export var required_tag_rules: Array[TagRequirement] = []
@export var required_profession_ranks: Array[ProfessionRankGate] = []
@export var required_attribute_rules: Array[AttributeRequirement] = []
@export var required_reputation_rules: Array[ReputationRequirement] = []
@export var assigned_core_must_be_subset_of_qualifiers := false


func is_empty() -> bool:
	return required_skill_ids.is_empty() \
		and required_tag_rules.is_empty() \
		and required_profession_ranks.is_empty() \
		and required_attribute_rules.is_empty() \
		and required_reputation_rules.is_empty()
