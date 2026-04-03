class_name ProfessionDef
extends Resource

@export var profession_id: StringName = &""
@export var display_name: String = ""
@export_multiline var description: String = ""
@export var max_rank := 1
@export var unlock_requirement: ProfessionPromotionRequirement
@export var rank_requirements: Array[ProfessionRankRequirement] = []
@export var granted_skills: Array[ProfessionGrantedSkill] = []
@export var attribute_modifiers: Array[AttributeModifier] = []
@export var active_conditions: Array[ProfessionActiveCondition] = []
@export var reactivation_mode: StringName = &"auto"
@export var dependency_visibility_mode: StringName = &"count_when_hidden"


func get_rank_requirement(target_rank: int) -> ProfessionRankRequirement:
	for requirement in rank_requirements:
		if requirement != null and requirement.target_rank == target_rank:
			return requirement
	return null


func get_granted_skills_for_rank(target_rank: int) -> Array[ProfessionGrantedSkill]:
	var result: Array[ProfessionGrantedSkill] = []
	for granted_skill in granted_skills:
		if granted_skill != null and granted_skill.unlock_rank == target_rank:
			result.append(granted_skill)
	return result
