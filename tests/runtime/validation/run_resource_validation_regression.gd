extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const ProgressionDataUtils = preload("res://scripts/player/progression/progression_data_utils.gd")
const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")
const ItemContentRegistry = preload("res://scripts/player/warehouse/item_content_registry.gd")
const EnemyContentRegistry = preload("res://scripts/enemies/enemy_content_registry.gd")
const QuestDef = preload("res://scripts/player/progression/quest_def.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const CombatSkillDef = preload("res://scripts/player/progression/combat_skill_def.gd")
const CONTENT_VALIDATION_RUNNER_SCRIPT = preload("res://tests/runtime/validation/content_validation_runner.gd")
const BattleSpecialProfileManifest = preload("res://scripts/systems/battle/core/special_profiles/battle_special_profile_manifest.gd")
const MeteorSwarmProfile = preload("res://scripts/systems/battle/core/meteor_swarm/meteor_swarm_profile.gd")
const WorldMapGenerationConfig = preload("res://scripts/utils/world_map_generation_config.gd")
const SettlementConfig = preload("res://scripts/utils/settlement_config.gd")
const FacilityConfig = preload("res://scripts/utils/facility_config.gd")
const FacilitySlotConfig = preload("res://scripts/utils/facility_slot_config.gd")
const SettlementDistributionRule = preload("res://scripts/utils/settlement_distribution_rule.gd")
const WeightedFacilityEntry = preload("res://scripts/utils/weighted_facility_entry.gd")
const WildSpawnRule = preload("res://scripts/utils/wild_spawn_rule.gd")

const OFFICIAL_SKILL_DIRECTORY := "res://data/configs/skills"
const OFFICIAL_PROFESSION_DIRECTORY := "res://data/configs/professions"
const OFFICIAL_ITEM_DIRECTORY := "res://data/configs/items"
const OFFICIAL_RECIPE_DIRECTORY := "res://data/configs/recipes"
const OFFICIAL_ENEMY_SEED_PATH := "res://data/configs/enemies/enemy_content_seed.tres"
const SKILL_INVALID_DIRECTORY := "res://tests/progression/fixtures/skill_registry_invalid"
const PROFESSION_INVALID_DIRECTORY := "res://tests/progression/fixtures/profession_registry_invalid"
const ITEM_INVALID_DIRECTORY := "res://tests/fixtures/resource_validation/item_registry_invalid"
const RECIPE_INVALID_DIRECTORY := "res://tests/fixtures/resource_validation/recipe_registry_invalid"
const IDENTITY_INVALID_RACE_DIRECTORY := "res://tests/progression/fixtures/identity_registry_invalid/races"
const IDENTITY_INVALID_SUBRACE_DIRECTORY := "res://tests/progression/fixtures/identity_registry_invalid/subraces"
const IDENTITY_INVALID_RACE_TRAIT_DIRECTORY := "res://tests/progression/fixtures/identity_registry_invalid/race_traits"
const IDENTITY_INVALID_STAGE_ADVANCEMENT_DIRECTORY := "res://tests/progression/fixtures/identity_registry_invalid/stage_advancements"
const ENEMY_MISSING_ID_SEED_PATH := "res://tests/fixtures/enemy_content/missing_template_id/enemy_content_seed.tres"
const ENEMY_DUPLICATE_ID_SEED_PATH := "res://tests/fixtures/enemy_content/duplicate_template_id/enemy_content_seed.tres"
const ENEMY_INVALID_REFERENCE_SEED_PATH := "res://tests/fixtures/enemy_content/invalid_roster/enemy_content_seed.tres"
const BATTLE_SPECIAL_PROFILE_FIXTURE_ROOT := "user://resource_validation/battle_special_profiles"

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures
var _reports: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var validation_runner := CONTENT_VALIDATION_RUNNER_SCRIPT.new()
	var progression_registry := ProgressionContentRegistry.new()
	var skill_defs := progression_registry.get_skill_defs()
	var item_registry := ItemContentRegistry.new()
	var item_defs := item_registry.get_item_defs()
	var enemy_registry := EnemyContentRegistry.new()
	var enemy_templates := enemy_registry.get_enemy_templates()
	var wild_encounter_rosters := enemy_registry.get_wild_encounter_rosters()

	var official_report := validation_runner.build_run_report("official_content", [
		validation_runner.validate_skill_directory(OFFICIAL_SKILL_DIRECTORY),
		validation_runner.validate_profession_directory(OFFICIAL_PROFESSION_DIRECTORY, skill_defs),
		validation_runner.validate_identity_content("official_identity", skill_defs),
		validation_runner.validate_battle_special_profile_registry("official_battle_special_profiles", skill_defs),
		validation_runner.validate_item_directory(OFFICIAL_ITEM_DIRECTORY),
		validation_runner.validate_recipe_directory(OFFICIAL_RECIPE_DIRECTORY, item_defs),
		validation_runner.validate_enemy_seed(OFFICIAL_ENEMY_SEED_PATH),
		validation_runner.validate_world_presets(enemy_templates, wild_encounter_rosters),
		validation_runner.validate_quest_entries(
			"official_quests",
			_build_quest_entries_from_dict(progression_registry.get_quest_defs(), "progression_seed"),
			item_defs,
			skill_defs,
			enemy_templates
		),
	])
	_reports.append(validation_runner.format_report(official_report))
	_assert_true(bool(official_report.get("ok", false)), "正式内容 validation runner 应通过。")
	_assert_true(int(official_report.get("error_count", -1)) == 0, "正式内容 validation runner 不应报告错误。")

	var skill_result := validation_runner.validate_skill_directory(SKILL_INVALID_DIRECTORY, true)
	var profession_result := validation_runner.validate_profession_directory(PROFESSION_INVALID_DIRECTORY, skill_defs)
	var identity_result := validation_runner.validate_identity_directories(
		"invalid_identity_directories",
		["res://data/configs/races", IDENTITY_INVALID_RACE_DIRECTORY],
		["res://data/configs/subraces", IDENTITY_INVALID_SUBRACE_DIRECTORY],
		["res://data/configs/race_traits", IDENTITY_INVALID_RACE_TRAIT_DIRECTORY],
		["res://data/configs/age_profiles"],
		["res://data/configs/bloodlines"],
		["res://data/configs/ascensions"],
		["res://data/configs/stage_advancements", IDENTITY_INVALID_STAGE_ADVANCEMENT_DIRECTORY],
		skill_defs
	)
	var item_result := validation_runner.validate_item_directory(ITEM_INVALID_DIRECTORY)
	var recipe_result := validation_runner.validate_recipe_directory(RECIPE_INVALID_DIRECTORY, item_defs)
	var enemy_missing_result := validation_runner.validate_enemy_seed(ENEMY_MISSING_ID_SEED_PATH)
	var enemy_duplicate_result := validation_runner.validate_enemy_seed(ENEMY_DUPLICATE_ID_SEED_PATH)
	var enemy_invalid_reference_result := validation_runner.validate_enemy_seed(ENEMY_INVALID_REFERENCE_SEED_PATH)
	var battle_special_missing_manifest_result := validation_runner.validate_battle_special_profile_registry(
		"battle_special_profile_missing_manifest",
		skill_defs,
		_prepare_empty_battle_special_profile_manifest_dir("missing_manifest")
	)
	var battle_special_unknown_profile_result := validation_runner.validate_battle_special_profile_registry(
		"battle_special_profile_unknown_profile_missing_manifest",
		_build_single_special_profile_skill_defs(&"phantom_special_skill", &"phantom_profile"),
		_prepare_empty_battle_special_profile_manifest_dir("unknown_profile_missing_manifest")
	)
	var battle_special_duplicate_profile_result := validation_runner.validate_battle_special_profile_registry(
		"battle_special_profile_duplicate_profile",
		skill_defs,
		_prepare_battle_special_profile_manifest_dir("duplicate_profile", [
			{"file_name": "a", "profile_id": &"meteor_swarm", "owning_skill_ids": [&"mage_meteor_swarm"], "profile_resource": _build_valid_meteor_swarm_profile()},
			{"file_name": "b", "profile_id": &"meteor_swarm", "owning_skill_ids": [&"mage_meteor_swarm"], "profile_resource": _build_valid_meteor_swarm_profile()},
		])
	)
	var battle_special_duplicate_owner_result := validation_runner.validate_battle_special_profile_registry(
		"battle_special_profile_duplicate_owner",
		skill_defs,
		_prepare_battle_special_profile_manifest_dir("duplicate_owner", [
			{"file_name": "a", "profile_id": &"meteor_swarm", "owning_skill_ids": [&"mage_meteor_swarm"], "profile_resource": _build_valid_meteor_swarm_profile()},
			{"file_name": "b", "profile_id": &"other_profile", "runtime_resolver_id": &"other_profile", "owning_skill_ids": [&"mage_meteor_swarm"], "profile_resource": Resource.new()},
		])
	)
	var battle_special_wrong_resource_result := validation_runner.validate_battle_special_profile_registry(
		"battle_special_profile_wrong_resource_type",
		skill_defs,
		_prepare_battle_special_profile_manifest_dir("wrong_resource_type", [
			{"file_name": "wrong_resource", "profile_id": &"meteor_swarm", "owning_skill_ids": [&"mage_meteor_swarm"], "profile_resource": Resource.new()},
		])
	)
	var battle_special_missing_owner_result := validation_runner.validate_battle_special_profile_registry(
		"battle_special_profile_missing_owner",
		skill_defs,
		_prepare_battle_special_profile_manifest_dir("missing_owner", [
			{"file_name": "missing_owner", "profile_id": &"meteor_swarm", "owning_skill_ids": [&"missing_skill"], "profile_resource": _build_valid_meteor_swarm_profile()},
		])
	)
	var battle_special_missing_required_test_result := validation_runner.validate_battle_special_profile_registry(
		"battle_special_profile_missing_required_test",
		skill_defs,
		_prepare_battle_special_profile_manifest_dir("missing_required_test", [
			{
				"file_name": "missing_required_test",
				"profile_id": &"meteor_swarm",
				"owning_skill_ids": [&"mage_meteor_swarm"],
				"profile_resource": _build_valid_meteor_swarm_profile(),
				"required_regression_tests": ["tests/missing/missing_profile_regression.gd"],
			},
		])
	)
	var battle_special_bad_schema_result := validation_runner.validate_battle_special_profile_registry(
		"battle_special_profile_bad_schema",
		skill_defs,
		_prepare_battle_special_profile_manifest_dir("bad_schema", [
			{"file_name": "bad_schema", "profile_id": &"meteor_swarm", "owning_skill_ids": [&"mage_meteor_swarm"], "profile_resource": _build_bad_schema_meteor_swarm_profile()},
		])
	)
	var world_result := validation_runner.validate_world_generation_config(
		"invalid_world_generation_config",
		_build_invalid_world_generation_config(),
		enemy_templates,
		wild_encounter_rosters
	)
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
		identity_result,
		item_result,
		recipe_result,
		enemy_missing_result,
		enemy_duplicate_result,
		enemy_invalid_reference_result,
		battle_special_missing_manifest_result,
		battle_special_unknown_profile_result,
		battle_special_duplicate_profile_result,
		battle_special_duplicate_owner_result,
		battle_special_wrong_resource_result,
		battle_special_missing_owner_result,
		battle_special_missing_required_test_result,
		battle_special_bad_schema_result,
		world_result,
		quest_result,
	])
	_reports.append(validation_runner.format_report(invalid_fixture_report))

	_assert_domain_has_fragment(skill_result, "is missing skill_id", "技能 validation runner 应覆盖缺失 skill_id。")
	_assert_domain_has_fragment(skill_result, "Duplicate skill_id registered: duplicate_skill", "技能 validation runner 应覆盖重复 skill_id。")
	_assert_domain_has_fragment(skill_result, "references missing skill missing_skill", "技能 validation runner 应覆盖非法技能引用。")

	_assert_domain_has_fragment(profession_result, "is missing profession_id", "职业 validation runner 应覆盖缺失 profession_id。")
	_assert_domain_has_fragment(profession_result, "Duplicate profession_id registered: duplicate_profession", "职业 validation runner 应覆盖重复 profession_id。")
	_assert_domain_has_fragment(profession_result, "references missing skill missing_skill", "职业 validation runner 应覆盖非法技能引用。")

	_assert_domain_has_fragment(identity_result, "is missing race_id", "身份 validation runner 应覆盖缺失 race_id。")
	_assert_domain_has_fragment(identity_result, "Duplicate race_id registered: duplicate_identity_race", "身份 validation runner 应覆盖重复 race_id。")
	_assert_domain_has_fragment(identity_result, "references missing age_profile missing_age_profile", "身份 validation runner 应覆盖非法 age_profile 引用。")
	_assert_domain_has_fragment(identity_result, "references missing parent_race missing_parent_race", "身份 validation runner 应覆盖非法 parent_race 引用。")
	_assert_domain_has_fragment(identity_result, "uses unsupported trigger_type not_a_trigger", "身份 validation runner 应覆盖非法 trigger_type。")
	_assert_domain_has_fragment(identity_result, "uses unsupported target_axis unlisted_axis", "身份 validation runner 应覆盖非法 target_axis。")
	_assert_domain_has_fragment(identity_result, "unsupported damage tag cold", "身份 validation runner 应覆盖非法 damage resistance tag。")
	_assert_domain_has_fragment(identity_result, "uses unsupported mitigation tier resist", "身份 validation runner 应覆盖非法 damage resistance tier。")

	_assert_domain_has_fragment(item_result, "is missing item_id", "物品 validation runner 应覆盖缺失 item_id。")
	_assert_domain_has_fragment(item_result, "Duplicate item_id registered: duplicate_item", "物品 validation runner 应覆盖重复 item_id。")
	_assert_domain_has_fragment(item_result, "declares invalid slot phantom_slot", "物品 validation runner 应覆盖非法槽位引用。")
	_assert_domain_has_fragment(item_result, "must declare weapon_profile", "物品 validation runner 应拒绝只声明旧武器裸字段的资源。")
	_assert_domain_has_fragment(item_result, "must declare explicit buy_price", "物品 validation runner 应拒绝只声明 base_price 的可交易资源。")

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

	var battle_special_errors := _combine_domain_errors([
		battle_special_missing_manifest_result,
		battle_special_unknown_profile_result,
		battle_special_duplicate_profile_result,
		battle_special_duplicate_owner_result,
		battle_special_wrong_resource_result,
		battle_special_missing_owner_result,
		battle_special_missing_required_test_result,
		battle_special_bad_schema_result,
	])
	_assert_messages_have_fragment(battle_special_errors, "is missing manifest for skill mage_meteor_swarm", "特殊技能 profile validation runner 应覆盖缺失 manifest。")
	_assert_messages_have_fragment(battle_special_errors, "Battle special profile phantom_profile is missing manifest for skill phantom_special_skill", "特殊技能 profile 合法性应由 battle manifest registry 判断，而不是 SkillContentRegistry 白名单。")
	_assert_messages_have_fragment(battle_special_errors, "Duplicate battle special profile_id registered: meteor_swarm", "特殊技能 profile validation runner 应覆盖重复 profile_id。")
	_assert_messages_have_fragment(battle_special_errors, "Duplicate battle special profile owning_skill_id registered: mage_meteor_swarm", "特殊技能 profile validation runner 应覆盖重复 owning_skill_ids。")
	_assert_messages_have_fragment(battle_special_errors, "profile_resource must be MeteorSwarmProfile", "特殊技能 profile validation runner 应覆盖错误 profile_resource 类型。")
	_assert_messages_have_fragment(battle_special_errors, "references missing owning skill missing_skill", "特殊技能 profile validation runner 应覆盖缺失 owning skill。")
	_assert_messages_have_fragment(battle_special_errors, "required regression test path does not exist", "特殊技能 profile validation runner 应覆盖缺失 required regression test。")
	_assert_messages_have_fragment(battle_special_errors, "accuracy_modifer_spec", "特殊技能 profile validation runner 应覆盖 Meteor exact schema 拼写错误。")

	_assert_domain_has_fragment(world_result, "facility missing facility_id", "世界 validation runner 应覆盖缺失 facility_id。")
	_assert_domain_has_fragment(world_result, "duplicate facility_id known_facility", "世界 validation runner 应覆盖重复 facility_id。")
	_assert_domain_has_fragment(world_result, "settlement missing settlement_id", "世界 validation runner 应覆盖缺失 settlement_id。")
	_assert_domain_has_fragment(world_result, "duplicate settlement_id known_settlement", "世界 validation runner 应覆盖重复 settlement_id。")
	_assert_domain_has_fragment(world_result, "settlement distribution references missing settlement missing_settlement", "世界 validation runner 应覆盖非法据点分布引用。")
	_assert_domain_has_fragment(world_result, "references missing guaranteed facility missing_facility", "世界 validation runner 应覆盖非法保底设施引用。")
	_assert_domain_has_fragment(world_result, "references missing optional facility missing_optional_facility", "世界 validation runner 应覆盖非法可选设施引用。")
	_assert_domain_has_fragment(world_result, "references missing enemy roster template missing_enemy", "世界 validation runner 应覆盖非法野怪敌方模板引用。")
	_assert_domain_has_fragment(world_result, "references missing encounter profile missing_roster", "世界 validation runner 应覆盖非法野怪 roster 引用。")

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


func _build_single_special_profile_skill_defs(skill_id: StringName, profile_id: StringName) -> Dictionary:
	var combat_profile := CombatSkillDef.new()
	combat_profile.skill_id = skill_id
	combat_profile.special_resolution_profile_id = profile_id

	var skill_def := SkillDef.new()
	skill_def.skill_id = skill_id
	skill_def.display_name = "Special Profile Fixture"
	skill_def.icon_id = skill_id
	skill_def.mastery_curve = PackedInt32Array([100])
	skill_def.combat_profile = combat_profile
	return {skill_id: skill_def}


func _prepare_empty_battle_special_profile_manifest_dir(fixture_id: String) -> String:
	return _prepare_battle_special_profile_manifest_dir(fixture_id, [])


func _prepare_battle_special_profile_manifest_dir(fixture_id: String, manifest_specs: Array[Dictionary]) -> String:
	var fixture_root := "%s/%s" % [BATTLE_SPECIAL_PROFILE_FIXTURE_ROOT, fixture_id]
	_remove_dir_recursive(fixture_root)
	var manifest_dir := "%s/manifests" % fixture_root
	var profile_dir := "%s/profiles" % fixture_root
	var manifest_error := DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(manifest_dir))
	var profile_error := DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(profile_dir))
	_assert_true(manifest_error == OK, "应能创建 battle special profile manifest fixture 目录。")
	_assert_true(profile_error == OK, "应能创建 battle special profile profile fixture 目录。")

	for spec_index in range(manifest_specs.size()):
		var spec := manifest_specs[spec_index]
		var file_name := String(spec.get("file_name", "manifest_%d" % spec_index))
		var profile_resource := spec.get("profile_resource", null) as Resource
		var saved_profile: Resource = null
		if profile_resource != null:
			var profile_path := "%s/%s_profile.tres" % [profile_dir, file_name]
			var profile_save_error := ResourceSaver.save(profile_resource, profile_path)
			_assert_true(profile_save_error == OK, "应能保存 battle special profile fixture profile。")
			saved_profile = load(profile_path)

		var manifest := BattleSpecialProfileManifest.new()
		manifest.profile_id = _to_string_name(spec.get("profile_id", &"meteor_swarm"))
		manifest.schema_version = int(spec.get("schema_version", 1))
		manifest.owning_skill_ids = _to_string_name_array(spec.get("owning_skill_ids", [&"mage_meteor_swarm"]))
		manifest.runtime_resolver_id = _to_string_name(spec.get("runtime_resolver_id", manifest.profile_id))
		manifest.profile_resource = saved_profile
		manifest.runtime_read_policy = _to_string_name(spec.get("runtime_read_policy", &"forbidden"))
		manifest.required_regression_tests = _to_string_array(spec.get("required_regression_tests", []))
		var manifest_path := "%s/%s.tres" % [manifest_dir, file_name]
		var manifest_save_error := ResourceSaver.save(manifest, manifest_path)
		_assert_true(manifest_save_error == OK, "应能保存 battle special profile fixture manifest。")
	return manifest_dir


func _build_valid_meteor_swarm_profile() -> MeteorSwarmProfile:
	var profile := MeteorSwarmProfile.new()
	profile.coverage_shape_id = &"square_7x7"
	profile.radius = 3
	profile.friendly_fire_soft_expected_hp_percent = 10
	profile.friendly_fire_hard_expected_hp_percent = 25
	profile.friendly_fire_hard_worst_case_hp_percent = 50
	return profile


func _build_bad_schema_meteor_swarm_profile() -> MeteorSwarmProfile:
	var profile := _build_valid_meteor_swarm_profile()
	profile.terrain_profiles = [
		{
			"terrain_profile_id": &"meteor_swarm_dust",
			"ring_min": 0,
			"ring_max": 2,
			"move_cost_delta": 0,
			"lifetime_policy": &"timed",
			"duration_tu": 50,
			"tick_interval_tu": 5,
			"tick_effect_type": &"none",
			"accuracy_modifer_spec": {
				"modifier_delta": -2,
			},
			"render_overlay_id": "meteor_dust_cloud",
		},
	]
	return profile


func _to_string_name_array(values_variant) -> Array[StringName]:
	var result: Array[StringName] = []
	if values_variant is not Array:
		return result
	for value in values_variant:
		if value is StringName:
			result.append(value)
		elif value is String:
			result.append(StringName(value))
	return result


func _to_string_array(values_variant) -> Array[String]:
	var result: Array[String] = []
	if values_variant is not Array:
		return result
	for value in values_variant:
		result.append(String(value))
	return result


func _to_string_name(value: Variant) -> StringName:
	if value is StringName:
		return value
	if value is String:
		return StringName(value)
	return &""


func _remove_dir_recursive(directory_path: String) -> void:
	var absolute_path := ProjectSettings.globalize_path(directory_path)
	if not DirAccess.dir_exists_absolute(absolute_path):
		return
	var directory := DirAccess.open(directory_path)
	if directory == null:
		return
	directory.list_dir_begin()
	while true:
		var entry_name := directory.get_next()
		if entry_name.is_empty():
			break
		if entry_name == "." or entry_name == "..":
			continue
		var entry_path := "%s/%s" % [directory_path, entry_name]
		if directory.current_is_dir():
			_remove_dir_recursive(entry_path)
		else:
			DirAccess.remove_absolute(ProjectSettings.globalize_path(entry_path))
	directory.list_dir_end()
	DirAccess.remove_absolute(absolute_path)


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


func _build_invalid_world_generation_config():
	var valid_facility := FacilityConfig.new()
	valid_facility.facility_id = "known_facility"
	valid_facility.display_name = "Known Facility"
	valid_facility.interaction_type = "service_known"
	valid_facility.allowed_slot_tags = ["core"]

	var duplicate_facility := FacilityConfig.new()
	duplicate_facility.facility_id = "known_facility"
	duplicate_facility.display_name = "Duplicate Facility"
	duplicate_facility.interaction_type = "service_duplicate"
	duplicate_facility.allowed_slot_tags = ["core"]

	var missing_id_facility := FacilityConfig.new()
	missing_id_facility.display_name = "Missing Id Facility"
	missing_id_facility.interaction_type = "service_missing_id"
	missing_id_facility.allowed_slot_tags = ["core"]

	var known_slot := FacilitySlotConfig.new()
	known_slot.slot_id = "core_slot"
	known_slot.slot_tag = "core"

	var optional_missing_facility := WeightedFacilityEntry.new()
	optional_missing_facility.facility_id = "missing_optional_facility"
	optional_missing_facility.weight = 1

	var settlement := SettlementConfig.new()
	settlement.settlement_id = "known_settlement"
	settlement.display_name = "Known Settlement"
	settlement.facility_slots = [known_slot]
	settlement.guaranteed_facility_ids = ["missing_facility"]
	settlement.optional_facility_pool = [optional_missing_facility]

	var duplicate_settlement := SettlementConfig.new()
	duplicate_settlement.settlement_id = "known_settlement"
	duplicate_settlement.display_name = "Duplicate Settlement"
	duplicate_settlement.facility_slots = [known_slot]

	var missing_id_settlement := SettlementConfig.new()
	missing_id_settlement.display_name = "Missing Id Settlement"
	missing_id_settlement.facility_slots = [known_slot]

	var missing_distribution := SettlementDistributionRule.new()
	missing_distribution.settlement_id = "missing_settlement"
	missing_distribution.faction_id = "neutral"

	var missing_wild_rule := WildSpawnRule.new()
	missing_wild_rule.region_tag = "invalid_wilds"
	missing_wild_rule.enemy_roster_template_id = &"missing_enemy"
	missing_wild_rule.encounter_profile_id = &"missing_roster"
	missing_wild_rule.density_per_chunk = 1

	var config := WorldMapGenerationConfig.new()
	config.world_size_in_chunks = Vector2i(1, 1)
	config.chunk_size = Vector2i(4, 4)
	config.settlement_library = [settlement, duplicate_settlement, missing_id_settlement]
	config.facility_library = [valid_facility, duplicate_facility, missing_id_facility]
	config.settlement_distribution = [missing_distribution]
	config.wild_monster_distribution = [missing_wild_rule]
	return config


func _combine_domain_errors(domain_results: Array[Dictionary]) -> Array[String]:
	var errors: Array[String] = []
	for domain_result in domain_results:
		for error_variant in domain_result.get("errors", []):
			errors.append(String(error_variant))
	return errors


func _assert_domain_has_fragment(domain_result: Dictionary, fragment: String, message: String) -> void:
	_assert_messages_have_fragment(_combine_domain_errors([domain_result]), fragment, message)


func _assert_report_has_fragment(report: Dictionary, fragment: String, message: String) -> void:
	var domain_results: Array[Dictionary] = []
	for domain_variant in report.get("domains", []):
		if domain_variant is Dictionary:
			domain_results.append(domain_variant)
	_assert_messages_have_fragment(_combine_domain_errors(domain_results), fragment, message)


func _assert_messages_have_fragment(messages: Array[String], fragment: String, message: String) -> void:
	for error_message in messages:
		if error_message.contains(fragment):
			return
	_test.fail(message)


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)
