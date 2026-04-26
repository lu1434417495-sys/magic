extends SceneTree

const ProgressionDataUtils = preload("res://scripts/player/progression/progression_data_utils.gd")
const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")
const ItemContentRegistry = preload("res://scripts/player/warehouse/item_content_registry.gd")
const EnemyContentRegistry = preload("res://scripts/enemies/enemy_content_registry.gd")
const QuestDef = preload("res://scripts/player/progression/quest_def.gd")
const ContentValidationRunner = preload("res://tests/runtime/content_validation_runner.gd")

const OFFICIAL_SKILL_DIRECTORY := "res://data/configs/skills"
const OFFICIAL_PROFESSION_DIRECTORY := "res://data/configs/professions"
const OFFICIAL_ITEM_DIRECTORY := "res://data/configs/items"
const OFFICIAL_RECIPE_DIRECTORY := "res://data/configs/recipes"
const OFFICIAL_ENEMY_SEED_PATH := "res://data/configs/enemies/enemy_content_seed.tres"

const SKILL_INVALID_DIRECTORY := "res://tests/progression/fixtures/skill_registry_invalid"
const PROFESSION_INVALID_DIRECTORY := "res://tests/progression/fixtures/profession_registry_invalid"
const ITEM_INVALID_DIRECTORY := "res://tests/fixtures/resource_validation/item_registry_invalid"
const RECIPE_INVALID_DIRECTORY := "res://tests/fixtures/resource_validation/recipe_registry_invalid"
const ENEMY_MISSING_ID_SEED_PATH := "res://tests/fixtures/enemy_content/missing_template_id/enemy_content_seed.tres"
const ENEMY_DUPLICATE_ID_SEED_PATH := "res://tests/fixtures/enemy_content/duplicate_template_id/enemy_content_seed.tres"
const ENEMY_INVALID_REFERENCE_SEED_PATH := "res://tests/fixtures/enemy_content/invalid_roster/enemy_content_seed.tres"

var _failures: Array[String] = []
var _reports: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var validation_runner := ContentValidationRunner.new()
	var progression_registry := ProgressionContentRegistry.new()
	var skill_defs := progression_registry.get_skill_defs()
	var item_registry := ItemContentRegistry.new()
	var item_defs := item_registry.get_item_defs()
	var enemy_registry := EnemyContentRegistry.new()
	var enemy_templates := enemy_registry.get_enemy_templates()

	var official_report := validation_runner.build_run_report("official_content", [
		validation_runner.validate_skill_directory(OFFICIAL_SKILL_DIRECTORY),
		validation_runner.validate_profession_directory(OFFICIAL_PROFESSION_DIRECTORY, skill_defs),
		validation_runner.validate_item_directory(OFFICIAL_ITEM_DIRECTORY),
		validation_runner.validate_recipe_directory(OFFICIAL_RECIPE_DIRECTORY, item_defs),
		validation_runner.validate_enemy_seed(OFFICIAL_ENEMY_SEED_PATH),
		validation_runner.validate_quest_entries(
			"official_quests",
			_build_quest_entries_from_dict(progression_registry.get_quest_defs(), "progression_seed"),
			item_defs,
			skill_defs,
			enemy_templates
		),
	])
	_reports.append(validation_runner.format_report(official_report))
	_assert_true(bool(official_report.get("ok", false)), "正式内容 validation runner 不应报告错误。")

	var skill_result := validation_runner.validate_skill_directory(SKILL_INVALID_DIRECTORY, true)
	var profession_result := validation_runner.validate_profession_directory(PROFESSION_INVALID_DIRECTORY, skill_defs)
	var item_result := validation_runner.validate_item_directory(ITEM_INVALID_DIRECTORY)
	var recipe_result := validation_runner.validate_recipe_directory(RECIPE_INVALID_DIRECTORY, item_defs)
	var enemy_missing_result := validation_runner.validate_enemy_seed(ENEMY_MISSING_ID_SEED_PATH)
	var enemy_duplicate_result := validation_runner.validate_enemy_seed(ENEMY_DUPLICATE_ID_SEED_PATH)
	var enemy_invalid_reference_result := validation_runner.validate_enemy_seed(ENEMY_INVALID_REFERENCE_SEED_PATH)
	var quest_result := validation_runner.validate_quest_entries(
		"invalid_quest_entries",
		_build_invalid_quest_entries(),
		item_defs,
		skill_defs,
		enemy_templates
	)
	var invalid_fixture_report := validation_runner.build_run_report("invalid_fixture_coverage", [
		skill_result,
		profession_result,
		item_result,
		recipe_result,
		enemy_missing_result,
		enemy_duplicate_result,
		enemy_invalid_reference_result,
		quest_result,
	])
	_reports.append(validation_runner.format_report(invalid_fixture_report))

	_assert_domain_has_fragment(skill_result, "is missing skill_id", "技能 validation runner 应覆盖缺失 skill_id。")
	_assert_domain_has_fragment(skill_result, "Duplicate skill_id registered: duplicate_skill", "技能 validation runner 应覆盖重复 skill_id。")
	_assert_domain_has_fragment(skill_result, "references missing skill missing_skill", "技能 validation runner 应覆盖非法技能引用。")

	_assert_domain_has_fragment(profession_result, "is missing profession_id", "职业 validation runner 应覆盖缺失 profession_id。")
	_assert_domain_has_fragment(profession_result, "Duplicate profession_id registered: duplicate_profession", "职业 validation runner 应覆盖重复 profession_id。")
	_assert_domain_has_fragment(profession_result, "references missing skill missing_skill", "职业 validation runner 应覆盖非法技能引用。")

	_assert_domain_has_fragment(item_result, "is missing item_id", "物品 validation runner 应覆盖缺失 item_id。")
	_assert_domain_has_fragment(item_result, "Duplicate item_id registered: duplicate_item", "物品 validation runner 应覆盖重复 item_id。")
	_assert_domain_has_fragment(item_result, "declares invalid slot phantom_slot", "物品 validation runner 应覆盖非法槽位引用。")
	_assert_domain_has_fragment(item_result, "must declare weapon_profile", "物品 validation runner 应拒绝只声明旧武器裸字段的资源。")

	_assert_domain_has_fragment(recipe_result, "is missing recipe_id", "配方 validation runner 应覆盖缺失 recipe_id。")
	_assert_domain_has_fragment(recipe_result, "Duplicate recipe_id registered: duplicate_recipe", "配方 validation runner 应覆盖重复 recipe_id。")
	_assert_domain_has_fragment(recipe_result, "references missing input item missing_item", "配方 validation runner 应覆盖非法物品引用。")

	var enemy_errors := _combine_domain_errors([
		enemy_missing_result,
		enemy_duplicate_result,
		enemy_invalid_reference_result,
	])
	_assert_messages_have_fragment(enemy_errors, "is missing template_id", "敌方 validation runner 应覆盖缺失 template_id。")
	_assert_messages_have_fragment(enemy_errors, "Duplicate enemy template_id registered: duplicate_enemy", "敌方 validation runner 应覆盖重复 template_id。")
	_assert_messages_have_fragment(enemy_errors, "references missing template missing_template", "敌方 validation runner 应覆盖非法 template 引用。")
	_assert_messages_have_fragment(enemy_errors, "must declare attack_equipment_item_id", "敌方 validation runner 应覆盖非 beast 模板缺失真实攻击装备。")

	_assert_domain_has_fragment(quest_result, "is missing quest_id", "任务 validation runner 应覆盖缺失 quest_id。")
	_assert_domain_has_fragment(quest_result, "Duplicate quest_id registered: duplicate_quest", "任务 validation runner 应覆盖重复 quest_id。")
	_assert_domain_has_fragment(quest_result, "references missing item missing_item", "任务 validation runner 应覆盖非法物品引用。")

	for report_text in _reports:
		print(report_text)

	if _failures.is_empty():
		print("Resource validation regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Resource validation regression: FAIL (%d)" % _failures.size())
	quit(1)


func _build_quest_entries_from_dict(quest_defs: Dictionary, source_prefix: String) -> Array[Dictionary]:
	var entries: Array[Dictionary] = []
	for quest_key in ProgressionDataUtils.sorted_string_keys(quest_defs):
		var quest_id := StringName(quest_key)
		entries.append({
			"source": "%s::%s" % [source_prefix, String(quest_id)],
			"quest_def": quest_defs.get(quest_id) as QuestDef,
		})
	return entries


func _build_invalid_quest_entries() -> Array[Dictionary]:
	var missing_id_quest := QuestDef.new()
	missing_id_quest.display_name = "Missing Quest Id"
	missing_id_quest.provider_interaction_id = &"service_contract_board"
	missing_id_quest.objective_defs = [
		{
			"objective_id": "report_once",
			"objective_type": QuestDef.OBJECTIVE_SETTLEMENT_ACTION,
			"target_id": "service:training",
			"target_value": 1,
		},
	]
	missing_id_quest.reward_entries = [
		{"reward_type": QuestDef.REWARD_GOLD, "amount": 10},
	]

	var duplicate_a := QuestDef.new()
	duplicate_a.quest_id = &"duplicate_quest"
	duplicate_a.display_name = "Duplicate Quest A"
	duplicate_a.provider_interaction_id = &"service_contract_board"
	duplicate_a.objective_defs = [
		{
			"objective_id": "report_once",
			"objective_type": QuestDef.OBJECTIVE_SETTLEMENT_ACTION,
			"target_id": "service:training",
			"target_value": 1,
		},
	]
	duplicate_a.reward_entries = [
		{"reward_type": QuestDef.REWARD_GOLD, "amount": 10},
	]

	var duplicate_b := QuestDef.new()
	duplicate_b.quest_id = &"duplicate_quest"
	duplicate_b.display_name = "Duplicate Quest B"
	duplicate_b.provider_interaction_id = &"service_contract_board"
	duplicate_b.objective_defs = duplicate_a.objective_defs.duplicate(true)
	duplicate_b.reward_entries = duplicate_a.reward_entries.duplicate(true)

	var invalid_reference_quest := QuestDef.new()
	invalid_reference_quest.quest_id = &"invalid_reference_quest"
	invalid_reference_quest.display_name = "Invalid Reference Quest"
	invalid_reference_quest.provider_interaction_id = &"service_missing"
	invalid_reference_quest.objective_defs = [
		{
			"objective_id": "submit_missing_item",
			"objective_type": QuestDef.OBJECTIVE_SUBMIT_ITEM,
			"target_id": "missing_item",
			"target_value": 1,
		},
		{
			"objective_id": "defeat_missing_enemy",
			"objective_type": QuestDef.OBJECTIVE_DEFEAT_ENEMY,
			"target_id": "missing_enemy",
			"target_value": 1,
		},
	]
	invalid_reference_quest.reward_entries = [
		{"reward_type": QuestDef.REWARD_ITEM, "item_id": "missing_item", "quantity": 1},
		{
			"reward_type": QuestDef.REWARD_PENDING_CHARACTER_REWARD,
			"member_id": "hero",
			"entries": [
				{
					"entry_type": "skill_unlock",
					"target_id": "missing_skill",
					"amount": 1,
				},
			],
		},
	]

	return [
		{"source": "fixture::missing_quest_id", "quest_def": missing_id_quest},
		{"source": "fixture::duplicate_quest_a", "quest_def": duplicate_a},
		{"source": "fixture::duplicate_quest_b", "quest_def": duplicate_b},
		{"source": "fixture::invalid_reference_quest", "quest_def": invalid_reference_quest},
	]


func _combine_domain_errors(domain_results: Array[Dictionary]) -> Array[String]:
	var errors: Array[String] = []
	for domain_result in domain_results:
		for error_variant in domain_result.get("errors", []):
			errors.append(String(error_variant))
	return errors


func _assert_domain_has_fragment(domain_result: Dictionary, fragment: String, message: String) -> void:
	_assert_messages_have_fragment(_combine_domain_errors([domain_result]), fragment, message)


func _assert_messages_have_fragment(messages: Array[String], fragment: String, message: String) -> void:
	for error_message in messages:
		if error_message.contains(fragment):
			return
	_failures.append(message)


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)
