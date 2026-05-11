class_name ContentValidationRunner
extends RefCounted

const ProgressionDataUtils = preload("res://scripts/player/progression/progression_data_utils.gd")
const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")
const QuestDef = preload("res://scripts/player/progression/quest_def.gd")
const SkillContentRegistry = preload("res://scripts/player/progression/skill_content_registry.gd")
const ProfessionContentRegistry = preload("res://scripts/player/progression/profession_content_registry.gd")
const RaceContentRegistry = preload("res://scripts/player/progression/race_content_registry.gd")
const SubraceContentRegistry = preload("res://scripts/player/progression/subrace_content_registry.gd")
const RaceTraitContentRegistry = preload("res://scripts/player/progression/race_trait_content_registry.gd")
const AgeContentRegistry = preload("res://scripts/player/progression/age_content_registry.gd")
const BloodlineContentRegistry = preload("res://scripts/player/progression/bloodline_content_registry.gd")
const AscensionContentRegistry = preload("res://scripts/player/progression/ascension_content_registry.gd")
const StageAdvancementContentRegistry = preload("res://scripts/player/progression/stage_advancement_content_registry.gd")
const ItemContentRegistry = preload("res://scripts/player/warehouse/item_content_registry.gd")
const RecipeContentRegistry = preload("res://scripts/player/warehouse/recipe_content_registry.gd")
const EnemyContentRegistry = preload("res://scripts/enemies/enemy_content_registry.gd")
const WorldMapContentValidator = preload("res://scripts/utils/world_map_content_validator.gd")

const SUPPORTED_QUEST_PROVIDER_IDS := {
	&"service_contract_board": true,
	&"service_bounty_registry": true,
}

const QUEST_REWARD_ENTRY_TYPES_REQUIRING_SKILL := {
	&"skill_unlock": true,
	&"skill_mastery": true,
	&"skill_level": true,
}


func build_run_report(label: String, domain_results: Array[Dictionary]) -> Dictionary:
	var report := {
		"label": label,
		"ok": true,
		"error_count": 0,
		"domains": [],
	}
	var normalized_domain_results: Array[Dictionary] = []
	for domain_result_variant in domain_results:
		if domain_result_variant is not Dictionary:
			continue
		var domain_result := (domain_result_variant as Dictionary).duplicate(true)
		var error_count := int(domain_result.get("error_count", 0))
		report["error_count"] = int(report.get("error_count", 0)) + error_count
		if error_count > 0:
			report["ok"] = false
		normalized_domain_results.append(domain_result)
	report["domains"] = normalized_domain_results
	return report


func format_report(report: Dictionary) -> String:
	var label := String(report.get("label", "validation"))
	var lines := PackedStringArray([
		"Validation report: %s | %s | errors=%d" % [
			label,
			"PASS" if bool(report.get("ok", false)) else "FAIL",
			int(report.get("error_count", 0)),
		],
	])
	for domain_variant in report.get("domains", []):
		if domain_variant is not Dictionary:
			continue
		var domain_result := domain_variant as Dictionary
		var domain_label := String(domain_result.get("domain", "unknown"))
		var source_label := String(domain_result.get("label", ""))
		lines.append("[%s] source=%s errors=%d" % [
			domain_label,
			source_label,
			int(domain_result.get("error_count", 0)),
		])
		for error_variant in domain_result.get("errors", []):
			lines.append("  - %s" % String(error_variant))
	return "\n".join(lines)


func validate_skill_directory(
	directory_path: String,
	include_progression_skill_checks: bool = false
) -> Dictionary:
	var registry := SkillContentRegistry.new()
	registry._skill_defs.clear()
	registry._validation_errors.clear()
	registry._scan_directory(directory_path)
	registry._validation_errors.append_array(registry._collect_validation_errors())
	var errors := registry.validate()
	if include_progression_skill_checks:
		var progression_registry := ProgressionContentRegistry.new()
		progression_registry._validation_errors.clear()
		progression_registry._skill_defs = registry.get_skill_defs().duplicate()
		progression_registry._profession_defs.clear()
		progression_registry._achievement_defs.clear()
		progression_registry._quest_defs.clear()
		progression_registry._race_defs.clear()
		progression_registry._subrace_defs.clear()
		progression_registry._race_trait_defs.clear()
		progression_registry._age_profile_defs.clear()
		progression_registry._bloodline_defs.clear()
		progression_registry._bloodline_stage_defs.clear()
		progression_registry._ascension_defs.clear()
		progression_registry._ascension_stage_defs.clear()
		progression_registry._stage_advancement_defs.clear()
		errors.append_array(progression_registry._collect_validation_errors())
	return _build_domain_result("skill", directory_path, errors)


func validate_profession_directory(directory_path: String, skill_defs: Dictionary) -> Dictionary:
	var registry := ProfessionContentRegistry.new(skill_defs)
	registry._profession_defs.clear()
	registry._validation_errors.clear()
	registry._scan_directory(directory_path)
	registry._validation_errors.append_array(registry._collect_validation_errors())
	return _build_domain_result("profession", directory_path, registry.validate())


func validate_identity_content(label: String, skill_defs: Dictionary = {}) -> Dictionary:
	return validate_identity_directories(
		label,
		[RaceContentRegistry.RACE_CONFIG_DIRECTORY],
		[SubraceContentRegistry.SUBRACE_CONFIG_DIRECTORY],
		[RaceTraitContentRegistry.RACE_TRAIT_CONFIG_DIRECTORY],
		[AgeContentRegistry.AGE_PROFILE_CONFIG_DIRECTORY],
		[BloodlineContentRegistry.BLOODLINE_CONFIG_DIRECTORY],
		[AscensionContentRegistry.ASCENSION_CONFIG_DIRECTORY],
		[StageAdvancementContentRegistry.STAGE_ADVANCEMENT_CONFIG_DIRECTORY],
		skill_defs
	)


func validate_identity_directories(
	label: String,
	race_directories: Array[String],
	subrace_directories: Array[String],
	race_trait_directories: Array[String],
	age_profile_directories: Array[String],
	bloodline_directories: Array[String],
	ascension_directories: Array[String],
	stage_advancement_directories: Array[String],
	skill_defs: Dictionary = {}
) -> Dictionary:
	var race_registry := _build_race_registry(race_directories)
	var subrace_registry := _build_subrace_registry(subrace_directories)
	var race_trait_registry := _build_race_trait_registry(race_trait_directories)
	var age_registry := _build_age_registry(age_profile_directories)
	var bloodline_registry := _build_bloodline_registry(bloodline_directories)
	var ascension_registry := _build_ascension_registry(ascension_directories)
	var stage_advancement_registry := _build_stage_advancement_registry(stage_advancement_directories)

	var errors: Array[String] = []
	_append_unique_errors(errors, race_registry.validate())
	_append_unique_errors(errors, subrace_registry.validate())
	_append_unique_errors(errors, race_trait_registry.validate())
	_append_unique_errors(errors, age_registry.validate())
	_append_unique_errors(errors, bloodline_registry.validate())
	_append_unique_errors(errors, ascension_registry.validate())
	_append_unique_errors(errors, stage_advancement_registry.validate())

	var progression_registry := ProgressionContentRegistry.new()
	_prepare_identity_phase2_registry(
		progression_registry,
		skill_defs,
		race_registry,
		subrace_registry,
		race_trait_registry,
		age_registry,
		bloodline_registry,
		ascension_registry,
		stage_advancement_registry
	)
	var phase2_errors: Array[String] = []
	progression_registry._append_identity_phase2_validation_errors(phase2_errors)
	_append_unique_errors(errors, phase2_errors)
	return _build_domain_result("identity", label, errors)


func validate_item_directory(directory_path: String) -> Dictionary:
	var registry := ItemContentRegistry.new()
	registry._item_defs.clear()
	registry._validation_errors.clear()
	registry._scan_directory(directory_path)
	return _build_domain_result("item", directory_path, registry.validate())


func validate_recipe_directory(directory_path: String, item_defs: Dictionary) -> Dictionary:
	var registry := RecipeContentRegistry.new(item_defs)
	registry._recipe_defs.clear()
	registry._validation_errors.clear()
	registry._scan_directory(directory_path)
	return _build_domain_result("recipe", directory_path, registry.validate())


func validate_enemy_seed(seed_resource_path: String) -> Dictionary:
	var registry := EnemyContentRegistry.new()
	registry.configure_seed_resource(seed_resource_path)
	return _build_domain_result("enemy", seed_resource_path, registry.validate())


func validate_world_presets(enemy_templates: Dictionary = {}, wild_encounter_rosters: Dictionary = {}) -> Dictionary:
	var validator := WorldMapContentValidator.new()
	return _build_domain_result("world", "world_presets", validator.validate_world_presets(enemy_templates, wild_encounter_rosters))


func validate_world_generation_config(
	label: String,
	generation_config,
	enemy_templates: Dictionary = {},
	wild_encounter_rosters: Dictionary = {}
) -> Dictionary:
	var validator := WorldMapContentValidator.new()
	return _build_domain_result("world", label, validator.validate_generation_config(
		generation_config,
		label,
		enemy_templates,
		wild_encounter_rosters
	))


func validate_quest_entries(
	label: String,
	quest_entries: Array[Dictionary],
	item_defs: Dictionary = {},
	skill_defs: Dictionary = {},
	enemy_templates: Dictionary = {}
) -> Dictionary:
	var errors: Array[String] = []
	var seen_quest_ids: Dictionary = {}

	for entry in quest_entries:
		var source_label := String(entry.get("source", label))
		var quest_def := entry.get("quest_def") as QuestDef
		if quest_def == null:
			errors.append("Quest entry %s failed to cast to QuestDef." % source_label)
			continue
		if quest_def.quest_id == &"":
			errors.append("Quest entry %s is missing quest_id." % source_label)
			continue
		if seen_quest_ids.has(quest_def.quest_id):
			errors.append("Duplicate quest_id registered: %s" % String(quest_def.quest_id))
			continue
		seen_quest_ids[quest_def.quest_id] = true

		for schema_error in quest_def.validate_schema():
			errors.append("Quest %s: %s" % [String(quest_def.quest_id), schema_error])

		if quest_def.provider_interaction_id == &"":
			errors.append("Quest %s is missing provider_interaction_id." % String(quest_def.quest_id))
		elif not SUPPORTED_QUEST_PROVIDER_IDS.has(quest_def.provider_interaction_id):
			errors.append(
				"Quest %s references missing provider_interaction_id %s." % [
					String(quest_def.quest_id),
					String(quest_def.provider_interaction_id),
				]
			)

		_append_quest_objective_reference_errors(errors, quest_def, item_defs, enemy_templates)
		_append_quest_reward_reference_errors(errors, quest_def, item_defs, skill_defs)

	return _build_domain_result("quest", label, errors)


func _append_quest_objective_reference_errors(
	errors: Array[String],
	quest_def: QuestDef,
	item_defs: Dictionary,
	enemy_templates: Dictionary
) -> void:
	for objective_variant in quest_def.objective_defs:
		if objective_variant is not Dictionary:
			continue
		var objective_data := objective_variant as Dictionary
		var objective_id := ProgressionDataUtils.to_string_name(objective_data.get("objective_id", ""))
		var objective_type := ProgressionDataUtils.to_string_name(objective_data.get("objective_type", ""))
		var target_id := ProgressionDataUtils.to_string_name(objective_data.get("target_id", ""))
		match objective_type:
			QuestDef.OBJECTIVE_SUBMIT_ITEM:
				if target_id != &"" and not item_defs.is_empty() and not item_defs.has(target_id):
					errors.append(
						"Quest %s submit_item objective %s references missing item %s." % [
							String(quest_def.quest_id),
							String(objective_id),
							String(target_id),
						]
					)
			QuestDef.OBJECTIVE_DEFEAT_ENEMY:
				if target_id != &"" and not enemy_templates.is_empty() and not enemy_templates.has(target_id):
					errors.append(
						"Quest %s defeat_enemy objective %s references missing enemy %s." % [
							String(quest_def.quest_id),
							String(objective_id),
							String(target_id),
						]
					)


func _append_quest_reward_reference_errors(
	errors: Array[String],
	quest_def: QuestDef,
	item_defs: Dictionary,
	skill_defs: Dictionary
) -> void:
	for reward_variant in quest_def.reward_entries:
		if reward_variant is not Dictionary:
			continue
		var reward_data := reward_variant as Dictionary
		var reward_type := ProgressionDataUtils.to_string_name(reward_data.get("reward_type", ""))
		match reward_type:
			QuestDef.REWARD_ITEM:
				var reward_item_id := QuestDef.get_reward_item_id(reward_data)
				if reward_item_id != &"" and not item_defs.is_empty() and not item_defs.has(reward_item_id):
					errors.append(
						"Quest %s reward references missing item %s." % [
							String(quest_def.quest_id),
							String(reward_item_id),
						]
					)
			QuestDef.REWARD_PENDING_CHARACTER_REWARD:
				var entries_variant: Variant = reward_data.get("entries", [])
				if entries_variant is not Array:
					continue
				for entry_variant in entries_variant:
					if entry_variant is not Dictionary:
						continue
					var entry_data := entry_variant as Dictionary
					var entry_type := ProgressionDataUtils.to_string_name(entry_data.get("entry_type", ""))
					var target_id := ProgressionDataUtils.to_string_name(entry_data.get("target_id", ""))
					if not QUEST_REWARD_ENTRY_TYPES_REQUIRING_SKILL.has(entry_type):
						continue
					if target_id != &"" and not skill_defs.is_empty() and not skill_defs.has(target_id):
						errors.append(
							"Quest %s pending_character_reward references missing skill %s." % [
								String(quest_def.quest_id),
								String(target_id),
							]
						)


func _build_race_registry(directory_paths: Array[String]) -> RaceContentRegistry:
	var registry := RaceContentRegistry.new()
	registry._race_defs.clear()
	registry._validation_errors.clear()
	for directory_path in directory_paths:
		registry._scan_directory(directory_path)
	registry._validation_errors.append_array(registry._collect_validation_errors())
	return registry


func _build_subrace_registry(directory_paths: Array[String]) -> SubraceContentRegistry:
	var registry := SubraceContentRegistry.new()
	registry._subrace_defs.clear()
	registry._validation_errors.clear()
	for directory_path in directory_paths:
		registry._scan_directory(directory_path)
	registry._validation_errors.append_array(registry._collect_validation_errors())
	return registry


func _build_race_trait_registry(directory_paths: Array[String]) -> RaceTraitContentRegistry:
	var registry := RaceTraitContentRegistry.new()
	registry._race_trait_defs.clear()
	registry._validation_errors.clear()
	for directory_path in directory_paths:
		registry._scan_directory(directory_path)
	registry._validation_errors.append_array(registry._collect_validation_errors())
	return registry


func _build_age_registry(directory_paths: Array[String]) -> AgeContentRegistry:
	var registry := AgeContentRegistry.new()
	registry._age_profile_defs.clear()
	registry._validation_errors.clear()
	for directory_path in directory_paths:
		registry._scan_directory(directory_path)
	registry._validation_errors.append_array(registry._collect_validation_errors())
	return registry


func _build_bloodline_registry(directory_paths: Array[String]) -> BloodlineContentRegistry:
	var registry := BloodlineContentRegistry.new()
	registry._bloodline_defs.clear()
	registry._bloodline_stage_defs.clear()
	registry._validation_errors.clear()
	for directory_path in directory_paths:
		registry._scan_directory(directory_path)
	registry._validation_errors.append_array(registry._collect_validation_errors())
	return registry


func _build_ascension_registry(directory_paths: Array[String]) -> AscensionContentRegistry:
	var registry := AscensionContentRegistry.new()
	registry._ascension_defs.clear()
	registry._ascension_stage_defs.clear()
	registry._validation_errors.clear()
	for directory_path in directory_paths:
		registry._scan_directory(directory_path)
	registry._validation_errors.append_array(registry._collect_validation_errors())
	return registry


func _build_stage_advancement_registry(directory_paths: Array[String]) -> StageAdvancementContentRegistry:
	var registry := StageAdvancementContentRegistry.new()
	registry._stage_advancement_defs.clear()
	registry._validation_errors.clear()
	for directory_path in directory_paths:
		registry._scan_directory(directory_path)
	registry._validation_errors.append_array(registry._collect_validation_errors())
	return registry


func _prepare_identity_phase2_registry(
	progression_registry: ProgressionContentRegistry,
	skill_defs: Dictionary,
	race_registry: RaceContentRegistry,
	subrace_registry: SubraceContentRegistry,
	race_trait_registry: RaceTraitContentRegistry,
	age_registry: AgeContentRegistry,
	bloodline_registry: BloodlineContentRegistry,
	ascension_registry: AscensionContentRegistry,
	stage_advancement_registry: StageAdvancementContentRegistry
) -> void:
	progression_registry._validation_errors.clear()
	progression_registry._skill_defs = skill_defs.duplicate()
	progression_registry._profession_defs.clear()
	progression_registry._achievement_defs.clear()
	progression_registry._quest_defs.clear()
	progression_registry._race_defs = race_registry.get_race_defs().duplicate()
	progression_registry._subrace_defs = subrace_registry.get_subrace_defs().duplicate()
	progression_registry._race_trait_defs = race_trait_registry.get_race_trait_defs().duplicate()
	progression_registry._age_profile_defs = age_registry.get_age_profile_defs().duplicate()
	progression_registry._bloodline_defs = bloodline_registry.get_bloodline_defs().duplicate()
	progression_registry._bloodline_stage_defs = bloodline_registry.get_bloodline_stage_defs().duplicate()
	progression_registry._ascension_defs = ascension_registry.get_ascension_defs().duplicate()
	progression_registry._ascension_stage_defs = ascension_registry.get_ascension_stage_defs().duplicate()
	progression_registry._stage_advancement_defs = stage_advancement_registry.get_stage_advancement_defs().duplicate()


func _append_unique_errors(errors: Array[String], additional_errors: Array[String]) -> void:
	for error_message in additional_errors:
		if not errors.has(error_message):
			errors.append(error_message)


func _build_domain_result(domain: String, label: String, error_messages: Array[String]) -> Dictionary:
	var normalized_errors: Array[String] = []
	for error_message in error_messages:
		normalized_errors.append(String(error_message))
	return {
		"domain": domain,
		"label": label,
		"error_count": normalized_errors.size(),
		"errors": normalized_errors,
	}
