class_name ProgressionTestFixtures
extends RefCounted

const SKILL_DEF_SCRIPT = preload("res://scripts/player/progression/skill_def.gd")
const TAG_REQUIREMENT_SCRIPT = preload("res://scripts/player/progression/tag_requirement.gd")
const PROFESSION_RANK_GATE_SCRIPT = preload("res://scripts/player/progression/profession_rank_gate.gd")
const ATTRIBUTE_REQUIREMENT_SCRIPT = preload("res://scripts/player/progression/attribute_requirement.gd")
const PROFESSION_PROMOTION_REQUIREMENT_SCRIPT = preload("res://scripts/player/progression/profession_promotion_requirement.gd")
const PROFESSION_RANK_REQUIREMENT_SCRIPT = preload("res://scripts/player/progression/profession_rank_requirement.gd")
const PROFESSION_GRANTED_SKILL_SCRIPT = preload("res://scripts/player/progression/profession_granted_skill.gd")
const PROFESSION_ACTIVE_CONDITION_SCRIPT = preload("res://scripts/player/progression/profession_active_condition.gd")
const PROFESSION_DEF_SCRIPT = preload("res://scripts/player/progression/profession_def.gd")
const PLAYER_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/player_base_attributes.gd")
const PLAYER_SKILL_PROGRESS_SCRIPT = preload("res://scripts/player/progression/player_skill_progress.gd")
const PLAYER_PROFESSION_PROGRESS_SCRIPT = preload("res://scripts/player/progression/player_profession_progress.gd")
const PLAYER_PROGRESS_SCRIPT = preload("res://scripts/player/progression/player_progress.gd")
const PROFESSION_RULE_SERVICE_SCRIPT = preload("res://scripts/systems/profession_rule_service.gd")
const PROFESSION_ASSIGNMENT_SERVICE_SCRIPT = preload("res://scripts/systems/profession_assignment_service.gd")
const SKILL_MERGE_SERVICE_SCRIPT = preload("res://scripts/systems/skill_merge_service.gd")
const PROGRESSION_SERVICE_SCRIPT = preload("res://scripts/systems/progression_service.gd")


static func create_skill_def(
	skill_id: StringName,
	max_level: int = 1,
	mastery_curve: PackedInt32Array = PackedInt32Array([10]),
	tags: Array[StringName] = [],
	learn_source: StringName = &"book",
	mastery_sources: Array[StringName] = [&"training", &"battle"],
	skill_type: StringName = &"active",
	learn_requirements: Array[StringName] = []
) -> SkillDef:
	var skill_def := SKILL_DEF_SCRIPT.new()
	skill_def.skill_id = skill_id
	skill_def.display_name = String(skill_id)
	skill_def.max_level = max_level
	skill_def.mastery_curve = mastery_curve
	skill_def.tags = tags.duplicate()
	skill_def.learn_source = learn_source
	skill_def.mastery_sources = mastery_sources.duplicate()
	skill_def.skill_type = skill_type
	skill_def.learn_requirements = learn_requirements.duplicate()
	return skill_def


static func create_tag_requirement(tag: StringName, count: int) -> TagRequirement:
	var requirement := TAG_REQUIREMENT_SCRIPT.new()
	requirement.tag = tag
	requirement.count = count
	return requirement


static func create_rank_gate(
	profession_id: StringName,
	min_rank: int,
	check_mode: StringName = &"historical"
) -> ProfessionRankGate:
	var gate := PROFESSION_RANK_GATE_SCRIPT.new()
	gate.profession_id = profession_id
	gate.min_rank = min_rank
	gate.check_mode = check_mode
	return gate


static func create_attribute_requirement(
	attribute_id: StringName,
	min_value: int,
	max_value: int
) -> AttributeRequirement:
	var requirement := ATTRIBUTE_REQUIREMENT_SCRIPT.new()
	requirement.attribute_id = attribute_id
	requirement.min_value = min_value
	requirement.max_value = max_value
	return requirement


static func create_unlock_requirement(
	required_skill_ids: Array[StringName] = [],
	required_tag_rules: Array[TagRequirement] = [],
	required_profession_ranks: Array[ProfessionRankGate] = [],
	required_attribute_rules: Array[AttributeRequirement] = []
) -> ProfessionPromotionRequirement:
	var requirement := PROFESSION_PROMOTION_REQUIREMENT_SCRIPT.new()
	requirement.required_skill_ids = required_skill_ids.duplicate()
	requirement.required_tag_rules = required_tag_rules.duplicate()
	requirement.required_profession_ranks = required_profession_ranks.duplicate()
	requirement.required_attribute_rules = required_attribute_rules.duplicate()
	return requirement


static func create_rank_requirement(
	target_rank: int,
	required_tag_rules: Array[TagRequirement] = [],
	required_profession_ranks: Array[ProfessionRankGate] = []
) -> ProfessionRankRequirement:
	var requirement := PROFESSION_RANK_REQUIREMENT_SCRIPT.new()
	requirement.target_rank = target_rank
	requirement.required_tag_rules = required_tag_rules.duplicate()
	requirement.required_profession_ranks = required_profession_ranks.duplicate()
	return requirement


static func create_granted_skill(
	skill_id: StringName,
	unlock_rank: int,
	skill_type: StringName = &"active"
) -> ProfessionGrantedSkill:
	var granted_skill := PROFESSION_GRANTED_SKILL_SCRIPT.new()
	granted_skill.skill_id = skill_id
	granted_skill.unlock_rank = unlock_rank
	granted_skill.skill_type = skill_type
	return granted_skill


static func create_active_condition(
	attribute_id: StringName,
	min_value: int,
	max_value: int,
	condition_type: StringName = &"attribute_range"
) -> ProfessionActiveCondition:
	var condition := PROFESSION_ACTIVE_CONDITION_SCRIPT.new()
	condition.condition_type = condition_type
	condition.attribute_id = attribute_id
	condition.min_value = min_value
	condition.max_value = max_value
	return condition


static func create_profession_def(
	profession_id: StringName,
	max_rank: int = 1,
	unlock_requirement: ProfessionPromotionRequirement = null,
	rank_requirements: Array[ProfessionRankRequirement] = [],
	granted_skills: Array[ProfessionGrantedSkill] = [],
	active_conditions: Array[ProfessionActiveCondition] = [],
	reactivation_mode: StringName = &"auto",
	dependency_visibility_mode: StringName = &"count_when_hidden"
) -> ProfessionDef:
	var profession_def := PROFESSION_DEF_SCRIPT.new()
	profession_def.profession_id = profession_id
	profession_def.display_name = String(profession_id)
	profession_def.max_rank = max_rank
	profession_def.unlock_requirement = unlock_requirement
	profession_def.rank_requirements = rank_requirements.duplicate()
	profession_def.granted_skills = granted_skills.duplicate()
	profession_def.active_conditions = active_conditions.duplicate()
	profession_def.reactivation_mode = reactivation_mode
	profession_def.dependency_visibility_mode = dependency_visibility_mode
	return profession_def


static func create_base_attributes(
	hp_max: int = 0,
	strength: int = 0,
	agility: int = 0,
	intelligence: int = 0,
	morality: int = 0
) -> PlayerBaseAttributes:
	var attributes := PLAYER_BASE_ATTRIBUTES_SCRIPT.new()
	attributes.hp_max = hp_max
	attributes.strength = strength
	attributes.agility = agility
	attributes.intelligence = intelligence
	attributes.morality = morality
	return attributes


static func create_player_progress(base_attributes: PlayerBaseAttributes = null) -> PlayerProgress:
	var progress := PLAYER_PROGRESS_SCRIPT.new()
	if base_attributes != null:
		progress.base_attributes = base_attributes
	return progress


static func create_skill_progress(
	skill_id: StringName,
	learned: bool = true,
	level: int = 0,
	is_core: bool = false,
	assigned_profession_id: StringName = &"",
	profession_granted_by: StringName = &""
) -> PlayerSkillProgress:
	var progress := PLAYER_SKILL_PROGRESS_SCRIPT.new()
	progress.skill_id = skill_id
	progress.is_learned = learned
	progress.skill_level = level
	progress.is_core = is_core
	progress.assigned_profession_id = assigned_profession_id
	progress.profession_granted_by = profession_granted_by
	return progress


static func create_profession_progress(
	profession_id: StringName,
	rank: int = 0,
	core_skill_ids: Array[StringName] = []
) -> PlayerProfessionProgress:
	var progress := PLAYER_PROFESSION_PROGRESS_SCRIPT.new()
	progress.profession_id = profession_id
	progress.rank = rank
	progress.core_skill_ids = core_skill_ids.duplicate()
	progress.is_active = rank > 0
	return progress


static func create_services(
	player_progress: PlayerProgress,
	skill_defs: Array[SkillDef],
	profession_defs: Array[ProfessionDef]
) -> Dictionary:
	var rule_service := PROFESSION_RULE_SERVICE_SCRIPT.new()
	rule_service.setup(player_progress, skill_defs, profession_defs)

	var assignment_service := PROFESSION_ASSIGNMENT_SERVICE_SCRIPT.new()
	assignment_service.setup(player_progress, skill_defs, profession_defs)

	var merge_service := SKILL_MERGE_SERVICE_SCRIPT.new()
	merge_service.setup(player_progress, skill_defs, assignment_service)

	var progression_service := PROGRESSION_SERVICE_SCRIPT.new()
	progression_service.setup(
		player_progress,
		skill_defs,
		profession_defs,
		rule_service,
		assignment_service
	)

	return {
		"progression": progression_service,
		"rule": rule_service,
		"assignment": assignment_service,
		"merge": merge_service,
	}

