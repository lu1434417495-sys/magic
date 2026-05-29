extends RefCounted

const ProgressionDataUtils = preload("res://scripts/player/progression/ProgressionDataUtils.cs")
const ProgressionContentRegistry = preload("res://scripts/player/progression/ProgressionContentRegistry.cs")
const QUEST_CONTENT_VALIDATOR_SCRIPT_PATH := "res://scripts/player/progression/QuestContentValidator.cs"
const SkillContentRegistry = preload("res://scripts/player/progression/SkillContentRegistry.cs")
const ProfessionContentRegistry = preload("res://scripts/player/progression/ProfessionContentRegistry.cs")
const RaceContentRegistry = preload("res://scripts/player/progression/RaceContentRegistry.cs")
const SubraceContentRegistry = preload("res://scripts/player/progression/SubraceContentRegistry.cs")
const RaceTraitContentRegistry = preload("res://scripts/player/progression/RaceTraitContentRegistry.cs")
const AgeContentRegistry = preload("res://scripts/player/progression/AgeContentRegistry.cs")
const BloodlineContentRegistry = preload("res://scripts/player/progression/BloodlineContentRegistry.cs")
const AscensionContentRegistry = preload("res://scripts/player/progression/AscensionContentRegistry.cs")
const StageAdvancementContentRegistry = preload("res://scripts/player/progression/StageAdvancementContentRegistry.cs")
const ItemContentRegistry = preload("res://scripts/player/warehouse/ItemContentRegistry.cs")
const RecipeContentRegistry = preload("res://scripts/player/warehouse/RecipeContentRegistry.cs")
const EnemyContentRegistry = preload("res://scripts/enemies/EnemyContentRegistry.cs")
const WorldMapContentValidator = preload("res://scripts/utils/WorldMapContentValidator.cs")
const BattleSpecialProfileRegistry = preload("res://scripts/systems/battle/core/special_profiles/BattleSpecialProfileRegistry.cs")

func build_run_report(label: String, domain_results: Array[Dictionary]) -> Dictionary:
	var report := {
		"label": label,
		"ok": true,
		"error_count": 0,
		"domains": [],
	}
	var normalized_domain_results: Array[Dictionary] = []
	for domain_result_option in domain_results:
		if domain_result_option is not Dictionary:
			continue
		var domain_result := (domain_result_option as Dictionary).duplicate(true)
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
	for domain_option in report.get("domains", []):
		if domain_option is not Dictionary:
			continue
		var domain_result := domain_option as Dictionary
		var domain_label := String(domain_result.get("domain", "unknown"))
		var source_label := String(domain_result.get("label", ""))
		lines.append("[%s] source=%s errors=%d" % [
			domain_label,
			source_label,
			int(domain_result.get("error_count", 0)),
		])
		for error_option in domain_result.get("errors", []):
			lines.append("  - %s" % String(error_option))
	return "\n".join(lines)


func validate_skill_directory(
	directory_path: String,
	include_progression_skill_checks: bool = false
) -> Dictionary:
	var registry = SkillContentRegistry.new()
	registry._skill_defs.clear()
	registry._validation_errors.clear()
	registry._scan_directory(directory_path)
	registry._validation_errors.append_array(registry._collect_validation_errors())
	var errors := registry.validate()
	if include_progression_skill_checks:
		var progression_registry = ProgressionContentRegistry.new()
		progression_registry.replace_validation_sources({
			"skill_defs": registry.get_skill_defs().duplicate(),
		})
		errors.append_array(progression_registry._collect_validation_errors())
	return _build_domain_result("skill", directory_path, errors)


func validate_profession_directory(directory_path: String, skill_defs: Dictionary) -> Dictionary:
	var registry = ProfessionContentRegistry.new()
	registry.setup(skill_defs)
	registry._profession_defs.clear()
	registry._validation_errors.clear()
	registry._scan_directory(directory_path)
	registry._validation_errors.append_array(registry._collect_validation_errors())
	return _build_domain_result("profession", directory_path, registry.validate())


func validate_identity_content(label: String, skill_defs: Dictionary = {}) -> Dictionary:
	return validate_identity_directories(
		label,
		[RaceContentRegistry.race_config_directory()],
		[SubraceContentRegistry.subrace_config_directory()],
		[RaceTraitContentRegistry.race_trait_config_directory()],
		[AgeContentRegistry.age_profile_config_directory()],
		[BloodlineContentRegistry.bloodline_config_directory()],
		[AscensionContentRegistry.ascension_config_directory()],
		[StageAdvancementContentRegistry.stage_advancement_config_directory()],
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
	var race_registry = _build_race_registry(race_directories)
	var subrace_registry = _build_subrace_registry(subrace_directories)
	var race_trait_registry = _build_race_trait_registry(race_trait_directories)
	var age_registry = _build_age_registry(age_profile_directories)
	var bloodline_registry = _build_bloodline_registry(bloodline_directories)
	var ascension_registry = _build_ascension_registry(ascension_directories)
	var stage_advancement_registry = _build_stage_advancement_registry(stage_advancement_directories)

	var errors: Array[String] = []
	_append_unique_errors(errors, race_registry.validate())
	_append_unique_errors(errors, subrace_registry.validate())
	_append_unique_errors(errors, race_trait_registry.validate())
	_append_unique_errors(errors, age_registry.validate())
	_append_unique_errors(errors, bloodline_registry.validate())
	_append_unique_errors(errors, ascension_registry.validate())
	_append_unique_errors(errors, stage_advancement_registry.validate())

	var progression_registry = ProgressionContentRegistry.new()
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


func validate_official_item_content() -> Dictionary:
	return validate_item_directories(
		"official_items",
		["res://data/configs/items"],
		["res://data/configs/items_templates"]
	)


func validate_item_directory(directory_path: String) -> Dictionary:
	return validate_item_directories(directory_path, [directory_path], [])


func validate_item_directories(
	label: String,
	item_directories: Array,
	template_directories: Array = [],
	skill_defs: Dictionary = {}
) -> Dictionary:
	var registry = ItemContentRegistry.new()
	registry.rebuild_from_directories(item_directories, template_directories)
	var combined_errors: Array[String] = registry.validate()
	# Cross-table SkillBookItemContentValidator: catches handwritten skill
	# books that reference missing skills, learn_source != book, or collide
	# with canonical `skill_book_<skill_id>` ids that the factory would
	# otherwise generate.
	if not skill_defs.is_empty():
		var skill_book_errors := SkillBookItemContentValidator.validate(registry._item_defs, skill_defs)
		_append_unique_errors(combined_errors, skill_book_errors)
	return _build_domain_result("item", label, combined_errors)


func validate_recipe_directory(directory_path: String, item_defs: Dictionary) -> Dictionary:
	var registry = RecipeContentRegistry.new()
	registry.setup(item_defs)
	registry._recipe_defs.clear()
	registry._validation_errors.clear()
	registry._scan_directory(directory_path)
	return _build_domain_result("recipe", directory_path, registry.validate())


func validate_enemy_seed(seed_resource_path: String) -> Dictionary:
	var registry = EnemyContentRegistry.new()
	registry.configure_seed_resource(seed_resource_path, true, false)
	return _build_domain_result("enemy", seed_resource_path, registry.validate())


func validate_enemy_seed_with_directory_completeness(
	seed_resource_path: String,
	template_directory: String,
	brain_directory: String,
	roster_directory: String
) -> Dictionary:
	var registry = EnemyContentRegistry.new()
	registry.configure_directories(template_directory, brain_directory, roster_directory, false)
	registry.configure_seed_resource(seed_resource_path, true, true)
	return _build_domain_result("enemy", seed_resource_path, registry.validate())


func validate_battle_special_profile_registry(
	label: String,
	skill_defs: Dictionary,
	manifest_directory: String = ""
) -> Dictionary:
	var registry = BattleSpecialProfileRegistry.new()
	if not manifest_directory.is_empty():
		registry.set_manifest_directory(manifest_directory)
	registry.rebuild(skill_defs)
	return _build_domain_result("battle_special_profile", label, registry.validate())


func validate_world_presets(enemy_templates: Dictionary = {}, wild_encounter_rosters: Dictionary = {}) -> Dictionary:
	var validator = WorldMapContentValidator.new()
	return _build_domain_result("world", "world_presets", validator.validate_world_presets(enemy_templates, wild_encounter_rosters))


func validate_world_generation_config(
	label: String,
	generation_config,
	enemy_templates: Dictionary = {},
	wild_encounter_rosters: Dictionary = {}
) -> Dictionary:
	var validator = WorldMapContentValidator.new()
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
	var quest_content_validator = load(QUEST_CONTENT_VALIDATOR_SCRIPT_PATH)
	var errors: Array[String] = quest_content_validator.validate_entries(label, quest_entries, item_defs, skill_defs, enemy_templates)
	return _build_domain_result("quest", label, errors)


func _build_race_registry(directory_paths: Array[String]):
	var registry = RaceContentRegistry.new()
	registry._race_defs.clear()
	registry._validation_errors.clear()
	for directory_path in directory_paths:
		registry._scan_directory(directory_path)
	registry._validation_errors.append_array(registry._collect_validation_errors())
	return registry


func _build_subrace_registry(directory_paths: Array[String]):
	var registry = SubraceContentRegistry.new()
	registry._subrace_defs.clear()
	registry._validation_errors.clear()
	for directory_path in directory_paths:
		registry._scan_directory(directory_path)
	registry._validation_errors.append_array(registry._collect_validation_errors())
	return registry


func _build_race_trait_registry(directory_paths: Array[String]):
	var registry = RaceTraitContentRegistry.new()
	registry._race_trait_defs.clear()
	registry._validation_errors.clear()
	for directory_path in directory_paths:
		registry._scan_directory(directory_path)
	registry._validation_errors.append_array(registry._collect_validation_errors())
	return registry


func _build_age_registry(directory_paths: Array[String]):
	var registry = AgeContentRegistry.new()
	registry._age_profile_defs.clear()
	registry._validation_errors.clear()
	for directory_path in directory_paths:
		registry._scan_directory(directory_path)
	registry._validation_errors.append_array(registry._collect_validation_errors())
	return registry


func _build_bloodline_registry(directory_paths: Array[String]):
	var registry = BloodlineContentRegistry.new()
	registry._bloodline_defs.clear()
	registry._bloodline_stage_defs.clear()
	registry._validation_errors.clear()
	for directory_path in directory_paths:
		registry._scan_directory(directory_path)
	registry._validation_errors.append_array(registry._collect_validation_errors())
	return registry


func _build_ascension_registry(directory_paths: Array[String]):
	var registry = AscensionContentRegistry.new()
	registry._ascension_defs.clear()
	registry._ascension_stage_defs.clear()
	registry._validation_errors.clear()
	for directory_path in directory_paths:
		registry._scan_directory(directory_path)
	registry._validation_errors.append_array(registry._collect_validation_errors())
	return registry


func _build_stage_advancement_registry(directory_paths: Array[String]):
	var registry = StageAdvancementContentRegistry.new()
	registry._stage_advancement_defs.clear()
	registry._validation_errors.clear()
	for directory_path in directory_paths:
		registry._scan_directory(directory_path)
	registry._validation_errors.append_array(registry._collect_validation_errors())
	return registry


func _prepare_identity_phase2_registry(
	progression_registry,
	skill_defs: Dictionary,
	race_registry,
	subrace_registry,
	race_trait_registry,
	age_registry,
	bloodline_registry,
	ascension_registry,
	stage_advancement_registry
) -> void:
	progression_registry.replace_validation_sources({
		"skill_defs": skill_defs.duplicate(),
		"race_defs": race_registry.get_race_defs().duplicate(),
		"subrace_defs": subrace_registry.get_subrace_defs().duplicate(),
		"race_trait_defs": race_trait_registry.get_race_trait_defs().duplicate(),
		"age_profile_defs": age_registry.get_age_profile_defs().duplicate(),
		"bloodline_defs": bloodline_registry.get_bloodline_defs().duplicate(),
		"bloodline_stage_defs": bloodline_registry.get_bloodline_stage_defs().duplicate(),
		"ascension_defs": ascension_registry.get_ascension_defs().duplicate(),
		"ascension_stage_defs": ascension_registry.get_ascension_stage_defs().duplicate(),
		"stage_advancement_defs": stage_advancement_registry.get_stage_advancement_defs().duplicate(),
	})


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
