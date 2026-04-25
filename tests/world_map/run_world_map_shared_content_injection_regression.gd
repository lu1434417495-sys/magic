extends SceneTree

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/game_session.gd")
const WORLD_MAP_GRID_SYSTEM_SCRIPT = preload("res://scripts/systems/world_map_grid_system.gd")
const WORLD_MAP_SPAWN_SYSTEM_SCRIPT = preload("res://scripts/systems/world_map_spawn_system.gd")
const SETTLEMENT_CONFIG_SCRIPT = preload("res://scripts/utils/settlement_config.gd")
const ENCOUNTER_ANCHOR_DATA_SCRIPT = preload("res://scripts/systems/encounter_anchor_data.gd")
const WILD_SPAWN_RULE_SCRIPT = preload("res://scripts/utils/wild_spawn_rule.gd")

const TEST_WORLD_CONFIG := "res://data/configs/world_map/test_world_map_config.tres"
const SMALL_WORLD_CONFIG := "res://data/configs/world_map/small_world_map_config.tres"
const MEDIUM_WORLD_CONFIG := "res://data/configs/world_map/medium_world_map_config.tres"
const DEMO_WORLD_CONFIG := "res://data/configs/world_map/demo_world_map_config.tres"
const SHARED_SETTLEMENT_BUNDLE_PATH := "res://data/configs/world_map/shared/main_world_default_settlement_bundle.tres"
const SHARED_SETTLEMENT_NAME_POOL_PATH := "res://data/configs/world_map/shared/main_world_settlement_name_pool.tres"
const SHARED_TOWN_NAME_POOL_PATH := "res://data/configs/world_map/shared/main_world_town_name_pool.tres"
const SHARED_CITY_NAME_POOL_PATH := "res://data/configs/world_map/shared/main_world_city_name_pool.tres"
const SHARED_CAPITAL_NAME_POOL_PATH := "res://data/configs/world_map/shared/main_world_capital_name_pool.tres"
const SHARED_METROPOLIS_NAME_POOL_PATH := "res://data/configs/world_map/shared/main_world_metropolis_name_pool.tres"

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_generic_main_world_presets_keep_template_shape()
	_test_shared_settlement_bundle_uses_generic_template_names()
	_test_shared_settlement_name_pool_exposes_1000_unique_names()
	_test_shared_town_name_pool_exposes_500_unique_names()
	_test_shared_city_name_pool_exposes_300_unique_names()
	_test_shared_capital_name_pool_exposes_100_unique_names()
	_test_shared_metropolis_name_pool_exposes_50_unique_names()
	_test_new_world_generation_records_runtime_map_seed()
	_test_world_generation_injects_shared_main_world_content()
	_test_world_stronghold_instances_keep_stronghold_semantics()
	_test_demo_world_generation_includes_metropolis_instances()
	_test_procedural_wild_spawn_density_can_be_configured()
	_test_procedural_wild_spawn_region_tags_ignore_rule_order()
	_test_small_world_generation_assigns_unique_display_names()

	if _failures.is_empty():
		print("World map shared content injection regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("World map shared content injection regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_generic_main_world_presets_keep_template_shape() -> void:
	for config_path in [TEST_WORLD_CONFIG, SMALL_WORLD_CONFIG, MEDIUM_WORLD_CONFIG, DEMO_WORLD_CONFIG]:
		var config = load(config_path)
		_assert_true(config != null, "主世界预设 %s 应能正常加载。" % config_path)
		if config == null:
			continue
		_assert_true(bool(config.inject_default_main_world_content), "%s 应开启共享主世界内容注入。" % config_path)
		_assert_eq(int(config.procedural_wild_spawn_chunk_chance_denominator), 2, "%s 应使用更密集的主世界野怪 chunk 抽签配置。" % config_path)
		_assert_true(bool(config.guarantee_starting_wild_encounter), "%s 应保底在起始区域附近生成野外遭遇。" % config_path)
		_assert_eq((config.settlement_library as Array).size(), 0, "%s 不应再内嵌通用据点模板。" % config_path)
		_assert_eq((config.facility_library as Array).size(), 0, "%s 不应再内嵌通用设施模板。" % config_path)
		_assert_eq((config.wild_monster_distribution as Array).size(), 0, "%s 不应再内嵌通用野怪规则。" % config_path)


func _test_world_generation_injects_shared_main_world_content() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG, &"shared_content_injection", "共享内容注入验证"))
	_assert_eq(create_error, OK, "共享内容注入验证世界应能成功创建。")
	if create_error != OK:
		_cleanup(game_session)
		return

	var world_data: Dictionary = game_session.get_world_data()
	var found_world_stronghold := false
	var found_master_reforge_service := false
	for settlement_variant in world_data.get("settlements", []):
		if settlement_variant is not Dictionary:
			continue
		var settlement: Dictionary = settlement_variant
		if int(settlement.get("tier", -1)) == SETTLEMENT_CONFIG_SCRIPT.SettlementTier.WORLD_STRONGHOLD:
			found_world_stronghold = true
		for service_variant in settlement.get("available_services", []):
			if service_variant is not Dictionary:
				continue
			if String((service_variant as Dictionary).get("interaction_script_id", "")) == "service_master_reforge":
				found_master_reforge_service = true

	var found_north_wild := false
	var found_south_mist_hollow := false
	for encounter_variant in world_data.get("encounter_anchors", []):
		var encounter_anchor = encounter_variant as ENCOUNTER_ANCHOR_DATA_SCRIPT
		if encounter_anchor == null:
			continue
		if encounter_anchor.region_tag == &"north_wilds":
			found_north_wild = true
		if (
			encounter_anchor.region_tag == &"south_wilds"
			and encounter_anchor.enemy_roster_template_id == &"mist_beast"
			and encounter_anchor.encounter_profile_id == &"mist_hollow"
		):
			found_south_mist_hollow = true

	_assert_true(found_world_stronghold, "共享内容注入后应生成世界据点模板。")
	_assert_true(found_master_reforge_service, "共享内容注入后应暴露大师重铸服务。")
	_assert_true(found_north_wild, "共享内容注入后应生成 north_wilds 遭遇。")
	_assert_true(found_south_mist_hollow, "共享内容注入后应生成指向 mist_hollow 的 south_wilds 遭遇。")

	_cleanup(game_session)


func _test_new_world_generation_records_runtime_map_seed() -> void:
	var config = load(TEST_WORLD_CONFIG)
	_assert_true(config != null, "runtime map seed 回归需要可加载的测试世界配置。")
	if config == null:
		return

	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG, &"runtime_map_seed", "运行时地图 seed 验证"))
	_assert_eq(create_error, OK, "新世界应能成功创建以验证 runtime map seed。")
	if create_error != OK:
		_cleanup(game_session)
		return

	var map_seed := int(game_session.get_world_data().get("map_seed", 0))
	_assert_true(map_seed > 0, "新世界 world_data 应记录由真随机接口分配的 map_seed。")
	_assert_true(map_seed != int(config.seed), "运行时 map_seed 不应直接沿用 world config 的固定 seed。")
	_cleanup(game_session)


func _test_world_stronghold_instances_keep_stronghold_semantics() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG, &"shared_stronghold_names", "世界据点命名验证"))
	_assert_eq(create_error, OK, "测试世界应能成功创建以验证世界据点命名。")
	if create_error != OK:
		_cleanup(game_session)
		return

	var found_world_stronghold := false
	for settlement_variant in game_session.get_world_data().get("settlements", []):
		if settlement_variant is not Dictionary:
			continue
		var settlement: Dictionary = settlement_variant
		if int(settlement.get("tier", -1)) != SETTLEMENT_CONFIG_SCRIPT.SettlementTier.WORLD_STRONGHOLD:
			continue
		found_world_stronghold = true
		var display_name := String(settlement.get("display_name", "")).strip_edges()
		_assert_true(display_name.begins_with("世界据点"), "世界据点实例应保留 world stronghold 语义，而不是回退到通用村名池。")
		_assert_true(not display_name.ends_with("村"), "世界据点实例不应使用以村结尾的通用名称。")

	_assert_true(found_world_stronghold, "测试世界应至少生成一个世界据点实例。")
	_cleanup(game_session)


func _test_demo_world_generation_includes_metropolis_instances() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(DEMO_WORLD_CONFIG, &"shared_demo_metropolis", "巨型世界都会验证"))
	_assert_eq(create_error, OK, "demo 世界应能成功创建以验证都会生成。")
	if create_error != OK:
		_cleanup(game_session)
		return

	var found_metropolis_instance := false
	for settlement_variant in game_session.get_world_data().get("settlements", []):
		if settlement_variant is not Dictionary:
			continue
		var settlement: Dictionary = settlement_variant
		if int(settlement.get("tier", -1)) != SETTLEMENT_CONFIG_SCRIPT.SettlementTier.METROPOLIS:
			continue
		found_metropolis_instance = true
		var display_name := String(settlement.get("display_name", "")).strip_edges()
		_assert_true(display_name.ends_with("帝都"), "demo 世界里的 metropolis 实例应继续使用都会名称池。")

	_assert_true(found_metropolis_instance, "demo 世界应至少生成一个 metropolis 实例。")
	_cleanup(game_session)


func _test_procedural_wild_spawn_region_tags_ignore_rule_order() -> void:
	var base_config = load(TEST_WORLD_CONFIG)
	_assert_true(base_config != null, "wild spawn region-tag 回归需要可加载的测试世界配置。")
	if base_config == null:
		return
	var settlement_bundle = load(SHARED_SETTLEMENT_BUNDLE_PATH)
	_assert_true(settlement_bundle != null, "wild spawn region-tag 回归需要可加载的共享据点 bundle。")
	if settlement_bundle == null:
		return

	var config = base_config.duplicate(true)
	_assert_true(config != null, "测试世界配置应支持 duplicate(true)。")
	if config == null:
		return
	config.inject_default_main_world_content = false
	config.settlement_library = settlement_bundle.settlement_library.duplicate(true)
	config.facility_library = settlement_bundle.facility_library.duplicate(true)
	config.guarantee_starting_wild_encounter = false
	config.procedural_wild_spawn_chunk_chance_denominator = 1

	var south_rule = WILD_SPAWN_RULE_SCRIPT.new()
	south_rule.region_tag = "south_wilds"
	south_rule.monster_name = "南境雾兽"
	south_rule.monster_template_id = &"mist_beast"
	south_rule.encounter_profile_id = &"mist_hollow"
	south_rule.density_per_chunk = 1
	south_rule.min_distance_to_settlement = 3
	south_rule.vision_range = 1

	var north_rule = WILD_SPAWN_RULE_SCRIPT.new()
	north_rule.region_tag = "north_wilds"
	north_rule.monster_name = "北境狼群"
	north_rule.monster_template_id = &"wolf_pack"
	north_rule.density_per_chunk = 1
	north_rule.min_distance_to_settlement = 3
	north_rule.vision_range = 1

	config.wild_monster_distribution = [south_rule, north_rule]

	var grid_system = WORLD_MAP_GRID_SYSTEM_SCRIPT.new()
	grid_system.setup(config.world_size_in_chunks, config.chunk_size)
	var spawn_system = WORLD_MAP_SPAWN_SYSTEM_SCRIPT.new()
	var world_data: Dictionary = spawn_system.build_world(config, grid_system)

	var midpoint_chunk_y: int = int(config.world_size_in_chunks.y / 2)
	var found_north_in_north := false
	var found_south_in_south := false
	var misplaced_north := false
	var misplaced_south := false
	for encounter_variant in world_data.get("encounter_anchors", []):
		var encounter_anchor = encounter_variant as ENCOUNTER_ANCHOR_DATA_SCRIPT
		if encounter_anchor == null:
			continue
		if encounter_anchor.encounter_kind != ENCOUNTER_ANCHOR_DATA_SCRIPT.ENCOUNTER_KIND_SINGLE:
			continue
		var chunk_coord := grid_system.get_chunk_coord(encounter_anchor.world_coord)
		if encounter_anchor.region_tag == &"north_wilds":
			if chunk_coord.y < midpoint_chunk_y:
				found_north_in_north = true
			else:
				misplaced_north = true
		elif encounter_anchor.region_tag == &"south_wilds":
			if chunk_coord.y >= midpoint_chunk_y:
				found_south_in_south = true
			else:
				misplaced_south = true

	_assert_true(found_north_in_north, "north_wilds 应仍然出现在世界北半区。")
	_assert_true(found_south_in_south, "south_wilds 应仍然出现在世界南半区。")
	_assert_true(not misplaced_north, "north_wilds 不应因为数组顺序变化而跑到南半区。")
	_assert_true(not misplaced_south, "south_wilds 不应因为数组顺序变化而跑到北半区。")


func _test_procedural_wild_spawn_density_can_be_configured() -> void:
	var base_config = load(TEST_WORLD_CONFIG)
	_assert_true(base_config != null, "wild spawn density 回归需要可加载的测试世界配置。")
	if base_config == null:
		return
	var settlement_bundle = load(SHARED_SETTLEMENT_BUNDLE_PATH)
	_assert_true(settlement_bundle != null, "wild spawn density 回归需要可加载的共享据点 bundle。")
	if settlement_bundle == null:
		return

	var config = base_config.duplicate(true)
	_assert_true(config != null, "测试世界配置应支持 duplicate(true) 以验证野怪密度。")
	if config == null:
		return
	config.inject_default_main_world_content = false
	config.settlement_library = settlement_bundle.settlement_library.duplicate(true)
	config.facility_library = settlement_bundle.facility_library.duplicate(true)
	config.guarantee_starting_wild_encounter = false
	config.procedural_wild_spawn_chunk_chance_denominator = 1

	var north_rule = WILD_SPAWN_RULE_SCRIPT.new()
	north_rule.region_tag = "north_wilds"
	north_rule.monster_name = "北境狼群"
	north_rule.monster_template_id = &"wolf_pack"
	north_rule.density_per_chunk = 1
	north_rule.min_distance_to_settlement = 3
	north_rule.vision_range = 1

	var south_rule = WILD_SPAWN_RULE_SCRIPT.new()
	south_rule.region_tag = "south_wilds"
	south_rule.monster_name = "南境雾兽"
	south_rule.monster_template_id = &"mist_beast"
	south_rule.encounter_profile_id = &"mist_hollow"
	south_rule.density_per_chunk = 1
	south_rule.min_distance_to_settlement = 3
	south_rule.vision_range = 1

	config.wild_monster_distribution = [north_rule, south_rule]

	var grid_system = WORLD_MAP_GRID_SYSTEM_SCRIPT.new()
	grid_system.setup(config.world_size_in_chunks, config.chunk_size)
	var spawn_system = WORLD_MAP_SPAWN_SYSTEM_SCRIPT.new()
	var world_data: Dictionary = spawn_system.build_world(config, grid_system)
	var single_encounter_count := 0
	for encounter_variant in world_data.get("encounter_anchors", []):
		var encounter_anchor = encounter_variant as ENCOUNTER_ANCHOR_DATA_SCRIPT
		if encounter_anchor == null:
			continue
		if encounter_anchor.encounter_kind == ENCOUNTER_ANCHOR_DATA_SCRIPT.ENCOUNTER_KIND_SINGLE:
			single_encounter_count += 1

	var expected_minimum := int(config.world_size_in_chunks.x * config.world_size_in_chunks.y)
	_assert_true(
		single_encounter_count >= expected_minimum,
		"chunk 抽签分母为 1 时，每个 chunk 至少应生成一组单体野外遭遇。actual=%d expected_minimum=%d" % [single_encounter_count, expected_minimum]
	)


func _test_small_world_generation_assigns_unique_display_names() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(SMALL_WORLD_CONFIG, &"shared_name_pool", "共享名称池验证"))
	_assert_eq(create_error, OK, "small 世界应能成功创建以验证据点名称池。")
	if create_error != OK:
		_cleanup(game_session)
		return

	var world_data: Dictionary = game_session.get_world_data()
	var seen_names: Dictionary = {}
	var generic_placeholder_names := {
		"村落": true,
		"城镇": true,
		"城市": true,
		"主城": true,
		"世界据点": true,
		"都会": true,
	}
	var found_town_instance := false
	var found_city_instance := false
	var found_capital_instance := false
	var found_metropolis_instance := false
	for settlement_variant in world_data.get("settlements", []):
		if settlement_variant is not Dictionary:
			continue
		var settlement: Dictionary = settlement_variant
		var display_name := String(settlement.get("display_name", "")).strip_edges()
		var tier := int(settlement.get("tier", -1))
		_assert_true(not display_name.is_empty(), "small 世界里的据点实例展示名不应为空。")
		if tier == SETTLEMENT_CONFIG_SCRIPT.SettlementTier.WORLD_STRONGHOLD:
			_assert_true(display_name.begins_with("世界据点"), "small 世界里的世界据点实例应保留 stronghold 语义名。")
		else:
			_assert_true(not generic_placeholder_names.has(display_name), "small 世界里的据点实例展示名应来自名称池而不是模板占位名。")
		_assert_true(not seen_names.has(display_name), "small 世界里的据点实例展示名不应重复。")
		if tier == SETTLEMENT_CONFIG_SCRIPT.SettlementTier.TOWN:
			found_town_instance = true
			_assert_true(display_name.ends_with("镇"), "small 世界里的 town 实例应使用城镇名称池并以镇结尾。")
		if tier == SETTLEMENT_CONFIG_SCRIPT.SettlementTier.CITY:
			found_city_instance = true
			_assert_true(display_name.ends_with("城"), "small 世界里的 city 实例应使用城市名称池并以城结尾。")
		if tier == SETTLEMENT_CONFIG_SCRIPT.SettlementTier.CAPITAL:
			found_capital_instance = true
			_assert_true(display_name.ends_with("王都"), "small 世界里的 capital 实例应使用主城名称池并以王都结尾。")
			_assert_true(display_name.find("王国") > 0, "small 世界里的 capital 实例应使用 XX王国...王都 语义。")
		if tier == SETTLEMENT_CONFIG_SCRIPT.SettlementTier.METROPOLIS:
			found_metropolis_instance = true
			_assert_true(display_name.ends_with("帝都"), "small 世界里的 metropolis 实例应使用都会名称池并以帝都结尾。")
			_assert_true(display_name.find("帝国") > 0, "small 世界里的 metropolis 实例应使用 XX帝国...帝都 语义。")
		seen_names[display_name] = true

	_assert_true(found_town_instance, "small 世界里应至少生成一个 town 实例以验证城镇名称池。")
	_assert_true(found_city_instance, "small 世界里应至少生成一个 city 实例以验证城市名称池。")
	_assert_true(found_capital_instance, "small 世界里应至少生成一个 capital 实例以验证主城名称池。")
	_assert_true(found_metropolis_instance, "small 世界里应至少生成一个 metropolis 实例以验证都会名称池。")
	_cleanup(game_session)


func _test_shared_settlement_bundle_uses_generic_template_names() -> void:
	var settlement_bundle = load(SHARED_SETTLEMENT_BUNDLE_PATH)
	_assert_true(settlement_bundle != null, "共享据点 bundle 应能正常加载。")
	if settlement_bundle == null:
		return

	var seen_template_town := false
	for settlement_variant in settlement_bundle.settlement_library:
		if settlement_variant == null:
			continue
		var settlement_id := String(settlement_variant.settlement_id)
		var display_name := String(settlement_variant.display_name)
		_assert_true(settlement_id.begins_with("template_"), "共享据点模板 ID 应使用 template_* 前缀。")
		_assert_true(display_name.find("灰石镇") < 0, "共享据点模板不应继续使用具体镇名。")
		_assert_true(display_name.find("晨星城") < 0, "共享据点模板不应继续使用具体城名。")
		if settlement_id == "template_town":
			seen_template_town = true

	_assert_true(seen_template_town, "共享据点 bundle 应包含抽象化的 town 模板。")


func _test_shared_settlement_name_pool_exposes_1000_unique_names() -> void:
	var name_pool = load(SHARED_SETTLEMENT_NAME_POOL_PATH)
	_assert_true(name_pool != null, "共享据点名称池应能正常加载。")
	if name_pool == null:
		return

	var display_names: Array[String] = name_pool.build_unique_display_names()
	_assert_eq(display_names.size(), 1000, "共享据点名称池应提供 1000 个唯一名称。")
	if display_names.size() > 0:
		_assert_true(display_names[0].strip_edges() != "", "共享据点名称池首项不应为空。")


func _test_shared_town_name_pool_exposes_500_unique_names() -> void:
	var name_pool = load(SHARED_TOWN_NAME_POOL_PATH)
	_assert_true(name_pool != null, "共享城镇名称池应能正常加载。")
	if name_pool == null:
		return

	var display_names: Array[String] = name_pool.build_unique_display_names()
	_assert_eq(display_names.size(), 500, "共享城镇名称池应提供 500 个唯一名称。")
	if display_names.size() > 0:
		_assert_true(display_names[0].strip_edges() != "", "共享城镇名称池首项不应为空。")
		_assert_true(display_names[0].ends_with("镇"), "共享城镇名称池应使用以镇结尾的名称。")


func _test_shared_city_name_pool_exposes_300_unique_names() -> void:
	var name_pool = load(SHARED_CITY_NAME_POOL_PATH)
	_assert_true(name_pool != null, "共享城市名称池应能正常加载。")
	if name_pool == null:
		return

	var display_names: Array[String] = name_pool.build_unique_display_names()
	_assert_eq(display_names.size(), 300, "共享城市名称池应提供 300 个唯一名称。")
	if display_names.size() > 0:
		_assert_true(display_names[0].strip_edges() != "", "共享城市名称池首项不应为空。")
		_assert_true(display_names[0].ends_with("城"), "共享城市名称池应使用以城结尾的名称。")


func _test_shared_capital_name_pool_exposes_100_unique_names() -> void:
	var name_pool = load(SHARED_CAPITAL_NAME_POOL_PATH)
	_assert_true(name_pool != null, "共享主城名称池应能正常加载。")
	if name_pool == null:
		return

	var display_names: Array[String] = name_pool.build_unique_display_names()
	_assert_eq(display_names.size(), 100, "共享主城名称池应提供 100 个唯一名称。")
	if display_names.size() > 0:
		_assert_true(display_names[0].strip_edges() != "", "共享主城名称池首项不应为空。")
		_assert_true(display_names[0].ends_with("王都"), "共享主城名称池应使用以王都结尾的名称。")
		_assert_true(display_names[0].find("王国") > 0, "共享主城名称池应使用 XX王国...王都 语义。")


func _test_shared_metropolis_name_pool_exposes_50_unique_names() -> void:
	var name_pool = load(SHARED_METROPOLIS_NAME_POOL_PATH)
	_assert_true(name_pool != null, "共享都会名称池应能正常加载。")
	if name_pool == null:
		return

	var display_names: Array[String] = name_pool.build_unique_display_names()
	_assert_eq(display_names.size(), 50, "共享都会名称池应提供 50 个唯一名称。")
	if display_names.size() > 0:
		_assert_true(display_names[0].strip_edges() != "", "共享都会名称池首项不应为空。")
		_assert_true(display_names[0].ends_with("帝都"), "共享都会名称池应使用以帝都结尾的名称。")
		_assert_true(display_names[0].find("帝国") > 0, "共享都会名称池应使用 XX帝国...帝都 语义。")


func _cleanup(game_session) -> void:
	if game_session == null:
		return
	game_session.clear_persisted_game()
	game_session.free()


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
