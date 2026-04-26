## 文件说明：该脚本属于野外遭遇成长回归测试相关的回归脚本，集中覆盖聚落类野怪的序列化、成长推进与战后分支。
## 审查重点：重点核对新增字段兼容、混编敌方编队构建、世界时间推进与战斗收尾后的世界状态是否稳定。
## 备注：后续若聚落类野怪改为可重建刷新，需要同步补充重建阶段与地图可见性测试。

extends SceneTree

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/game_session.gd")
const GAME_RUNTIME_FACADE_SCRIPT = preload("res://scripts/systems/game_runtime_facade.gd")
const PARTY_WAREHOUSE_SERVICE_SCRIPT = preload("res://scripts/systems/party_warehouse_service.gd")
const BATTLE_RESOLUTION_RESULT_SCRIPT = preload("res://scripts/systems/battle_resolution_result.gd")
const BATTLE_AI_CONTEXT_SCRIPT = preload("res://scripts/systems/battle_ai_context.gd")
const BATTLE_COMMAND_SCRIPT = preload("res://scripts/systems/battle_command.gd")
const ENCOUNTER_ANCHOR_DATA_SCRIPT = preload("res://scripts/systems/encounter_anchor_data.gd")
const ENCOUNTER_ROSTER_BUILDER_SCRIPT = preload("res://scripts/systems/encounter_roster_builder.gd")
const ENEMY_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/enemies/enemy_content_registry.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attribute_service.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")
const WORLD_TIME_SYSTEM_SCRIPT = preload("res://scripts/systems/world_time_system.gd")
const WILD_ENCOUNTER_GROWTH_SYSTEM_SCRIPT = preload("res://scripts/systems/wild_encounter_growth_system.gd")
const WAREHOUSE_STATE_SCRIPT = preload("res://scripts/player/warehouse/warehouse_state.gd")

const TEST_WORLD_CONFIG := "res://data/configs/world_map/test_world_map_config.tres"
const SMALL_WORLD_CONFIG := "res://data/configs/world_map/small_world_map_config.tres"
const ENEMY_CONTENT_SEED_PATH := "res://data/configs/enemies/enemy_content_seed.tres"
const FIXTURE_MISSING_BRAIN_SEED_PATH := "res://tests/fixtures/enemy_content/missing_brain/enemy_content_seed.tres"
const FIXTURE_INVALID_ROSTER_SEED_PATH := "res://tests/fixtures/enemy_content/invalid_roster/enemy_content_seed.tres"

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_enemy_content_registry_loads_formal_seed_resource()
	_test_enemy_content_registry_reports_missing_brain_reference()
	_test_enemy_content_registry_reports_missing_roster_template_reference()
	_test_encounter_anchor_round_trip_preserves_growth_fields()
	_test_encounter_roster_builder_builds_mixed_wolf_den_units()
	_test_encounter_roster_builder_initializes_formal_wolf_attack_and_ac_defaults()
	_test_enemy_content_registry_registers_second_formal_roster()
	_test_encounter_roster_builder_exposes_formal_wolf_den_drop_schema()
	_test_encounter_roster_builder_builds_mixed_mist_hollow_units()
	_test_wild_encounter_units_always_include_six_base_attributes()
	_test_beast_wild_units_roll_deterministic_5d3_minus_1_base_attributes()
	_test_wild_encounter_growth_respects_suppression_window()
	_test_game_runtime_facade_move_advances_world_step()
	_test_test_preset_uses_same_battle_terrain_profile_as_small_preset()
	_test_main_world_wilds_use_canyon_battle_terrain_profile()
	_test_formal_wolf_den_battle_prefers_close_in_over_wait_when_far()
	_test_world_spawn_explicitly_maps_south_wilds_to_mist_hollow()
	_test_game_runtime_facade_battle_requires_confirm_before_tu_advances()
	_test_game_runtime_facade_commit_battle_loot_records_overflow_entries()
	_test_game_runtime_facade_single_victory_removes_encounter()
	_test_game_runtime_facade_can_start_second_battle_after_first_victory()
	_test_game_runtime_facade_settlement_victory_downgrades_encounter()
	_test_game_runtime_facade_battle_overflow_feedback_surfaces_in_message_and_snapshot()
	if _failures.is_empty():
		print("Wild encounter regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Wild encounter regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_enemy_content_registry_loads_formal_seed_resource() -> void:
	var registry = ENEMY_CONTENT_REGISTRY_SCRIPT.new()
	registry.configure_seed_resource(ENEMY_CONTENT_SEED_PATH)
	var validation_errors := registry.validate()
	_assert_true(validation_errors.is_empty(), "EnemyContentRegistry 的正式 seed 资源不应产出校验错误。")
	_assert_eq(registry.get_enemy_ai_brains().size(), 5, "正式 enemy brain 资源目录应注册 5 个 brain。")
	_assert_eq(registry.get_enemy_templates().size(), 8, "正式 enemy template 资源目录应注册 8 个模板。")
	_assert_eq(registry.get_wild_encounter_rosters().size(), 2, "正式 roster 资源目录应注册 2 个编队。")
	var wolf_pack = registry.get_enemy_templates().get(&"wolf_pack")
	_assert_true(wolf_pack != null, "正式 enemy seed 应继续注册 wolf_pack。")
	if wolf_pack != null:
		_assert_eq(wolf_pack.resource_path, "res://data/configs/enemies/templates/wolf_pack.tres", "正式 enemy seed 应继续引用稳定 template 资源路径。")


func _test_enemy_content_registry_reports_missing_brain_reference() -> void:
	var registry = ENEMY_CONTENT_REGISTRY_SCRIPT.new()
	registry.configure_seed_resource(FIXTURE_MISSING_BRAIN_SEED_PATH)
	var validation_errors := registry.validate()
	_assert_true(
		_errors_contain_fragment(validation_errors, "references missing brain missing_brain"),
		"EnemyContentRegistry 应报告 template 引用缺失 brain 的配置错误。"
	)


func _test_enemy_content_registry_reports_missing_roster_template_reference() -> void:
	var registry = ENEMY_CONTENT_REGISTRY_SCRIPT.new()
	registry.configure_seed_resource(FIXTURE_INVALID_ROSTER_SEED_PATH)
	var validation_errors := registry.validate()
	_assert_true(
		_errors_contain_fragment(validation_errors, "references missing template missing_template"),
		"EnemyContentRegistry 应报告 roster 非法引用缺失 template 的配置错误。"
	)


func _test_encounter_anchor_round_trip_preserves_growth_fields() -> void:
	var encounter_anchor = ENCOUNTER_ANCHOR_DATA_SCRIPT.new()
	encounter_anchor.entity_id = &"wild_den_round_trip"
	encounter_anchor.display_name = "荒狼巢穴"
	encounter_anchor.world_coord = Vector2i(3, 7)
	encounter_anchor.faction_id = &"hostile"
	encounter_anchor.enemy_roster_template_id = &"wolf_pack"
	encounter_anchor.region_tag = &"north_wilds"
	encounter_anchor.vision_range = 2
	encounter_anchor.encounter_kind = ENCOUNTER_ANCHOR_DATA_SCRIPT.ENCOUNTER_KIND_SETTLEMENT
	encounter_anchor.encounter_profile_id = &"wolf_den"
	encounter_anchor.growth_stage = 3
	encounter_anchor.suppressed_until_step = 9

	var restored_anchor = ENCOUNTER_ANCHOR_DATA_SCRIPT.from_dict(encounter_anchor.to_dict())
	_assert_true(restored_anchor != null, "EncounterAnchorData 应能完成 round-trip 反序列化。")
	if restored_anchor == null:
		return
	_assert_eq(String(restored_anchor.encounter_kind), "settlement", "遭遇类别应在 round-trip 后保留。")
	_assert_eq(String(restored_anchor.encounter_profile_id), "wolf_den", "聚落编队配置标识应在 round-trip 后保留。")
	_assert_eq(restored_anchor.growth_stage, 3, "成长阶段应在 round-trip 后保留。")
	_assert_eq(restored_anchor.suppressed_until_step, 9, "压制截止 step 应在 round-trip 后保留。")


func _test_encounter_roster_builder_builds_mixed_wolf_den_units() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var builder = ENCOUNTER_ROSTER_BUILDER_SCRIPT.new()
	builder.setup(game_session.get_wild_encounter_rosters(), game_session.get_enemy_templates())

	var encounter_anchor = _build_settlement_encounter_anchor(&"wolf_den_stage4", Vector2i(4, 4))
	encounter_anchor.growth_stage = 4

	var enemy_units: Array = builder.build_enemy_units(encounter_anchor, {
		"skill_defs": game_session.get_skill_defs(),
		"enemy_templates": game_session.get_enemy_templates(),
		"enemy_ai_brains": game_session.get_enemy_ai_brains(),
	})
	_assert_eq(enemy_units.size(), 6, "wolf_den 第 4 阶段应构建 6 个敌方单位。")
	_assert_eq(_count_units_with_name_prefix(enemy_units, "荒狼·"), 4, "wolf_den 第 4 阶段应包含 4 个普通荒狼。")
	_assert_eq(_count_units_with_exact_name(enemy_units, "荒狼头目"), 1, "wolf_den 第 4 阶段应包含 1 个荒狼头目。")
	_assert_eq(_count_units_with_exact_name(enemy_units, "荒狼祭司"), 1, "wolf_den 第 4 阶段应包含 1 个荒狼祭司。")
	_assert_eq(_count_units_with_brain(enemy_units, &"ranged_controller"), 1, "荒狼祭司应使用 ranged_controller brain。")
	_assert_true(
		_unit_has_skill(enemy_units, "荒狼祭司", &"mage_temporal_rewind"),
		"荒狼祭司应携带治疗/支援技能 mage_temporal_rewind。"
	)
	game_session.free()


func _test_encounter_roster_builder_initializes_formal_wolf_attack_and_ac_defaults() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var builder = ENCOUNTER_ROSTER_BUILDER_SCRIPT.new()
	builder.setup(game_session.get_wild_encounter_rosters(), game_session.get_enemy_templates())

	var encounter_anchor = _build_settlement_encounter_anchor(&"wolf_den_hit_defaults", Vector2i(4, 4))
	encounter_anchor.growth_stage = 1
	var enemy_units: Array = builder.build_enemy_units(encounter_anchor, {
		"skill_defs": game_session.get_skill_defs(),
		"enemy_templates": game_session.get_enemy_templates(),
		"enemy_ai_brains": game_session.get_enemy_ai_brains(),
	})
	var wolf_unit = _find_first_unit_with_brain(enemy_units, &"melee_aggressor")
	_assert_true(wolf_unit != null, "wolf_den 正式编队应至少产出一个 melee_aggressor 荒狼单位。")
	if wolf_unit != null:
		_assert_eq(
			wolf_unit.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS),
			4,
			"正式狼系遭遇构建出的模板敌人应初始化 attack_bonus。"
		)
		_assert_eq(
			wolf_unit.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS),
			13,
			"正式狼系遭遇构建出的模板敌人应初始化 armor_class。"
		)
		_assert_true(
			not wolf_unit.attribute_snapshot.has_value(ATTRIBUTE_SERVICE_SCRIPT.WEAPON_ATTACK_RANGE),
			"正式狼系遭遇不应再把武器攻击范围写入 attribute_snapshot。"
		)
		_assert_eq(wolf_unit.weapon_attack_range, 1, "正式狼系遭遇应把武器攻击范围投影到 BattleUnitState。")
	game_session.free()


func _test_enemy_content_registry_registers_second_formal_roster() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var rosters: Dictionary = game_session.get_wild_encounter_rosters()
	_assert_true(rosters.has(&"wolf_den"), "wild encounter rosters 应继续保留 wolf_den。")
	_assert_true(rosters.has(&"mist_hollow"), "wild encounter rosters 应注册第二个正式 roster mist_hollow。")
	var mist_hollow = rosters.get(&"mist_hollow")
	_assert_true(mist_hollow != null, "mist_hollow roster 应能被正式查到。")
	if mist_hollow != null:
		_assert_eq(String(mist_hollow.display_name), "雾沼伏猎群", "mist_hollow roster 应暴露稳定显示名。")
	game_session.free()


func _test_encounter_roster_builder_exposes_formal_wolf_den_drop_schema() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var builder = ENCOUNTER_ROSTER_BUILDER_SCRIPT.new()
	builder.setup(game_session.get_wild_encounter_rosters(), game_session.get_enemy_templates())

	var encounter_anchor = _build_settlement_encounter_anchor(&"wolf_den_drop_schema", Vector2i(6, 6))
	var loot_entries: Array = builder.build_loot_entries(encounter_anchor, {})
	_assert_eq(loot_entries.size(), 1, "wolf_den 应暴露 1 条正式掉落 schema。")
	if not loot_entries.is_empty():
		var loot_entry: Dictionary = loot_entries[0]
		_assert_eq(String(loot_entry.get("drop_source_kind", "")), "encounter_roster", "掉落 schema 应标记来源类型。")
		_assert_eq(String(loot_entry.get("drop_source_id", "")), "wolf_den", "掉落 schema 应保留稳定 roster 标识。")
		_assert_eq(String(loot_entry.get("drop_entry_id", "")), "encounter_roster_wolf_den_beast_hide", "掉落 schema 应保留稳定 entry 标识。")
		_assert_eq(String(loot_entry.get("item_id", "")), "beast_hide", "wolf_den 掉落应指向正式物品标识。")
		_assert_eq(int(loot_entry.get("quantity", 0)), 2, "wolf_den 掉落数量应保持稳定。")
	game_session.free()


func _test_encounter_roster_builder_builds_mixed_mist_hollow_units() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var builder = ENCOUNTER_ROSTER_BUILDER_SCRIPT.new()
	builder.setup(game_session.get_wild_encounter_rosters(), game_session.get_enemy_templates())

	var encounter_anchor = ENCOUNTER_ANCHOR_DATA_SCRIPT.new()
	encounter_anchor.entity_id = &"mist_hollow_stage2"
	encounter_anchor.display_name = "雾沼伏猎群"
	encounter_anchor.world_coord = Vector2i(8, 8)
	encounter_anchor.faction_id = &"hostile"
	encounter_anchor.enemy_roster_template_id = &"mist_beast"
	encounter_anchor.region_tag = &"south_wilds"
	encounter_anchor.vision_range = 2
	encounter_anchor.encounter_profile_id = &"mist_hollow"
	encounter_anchor.growth_stage = 2

	var enemy_units: Array = builder.build_enemy_units(encounter_anchor, {
		"skill_defs": game_session.get_skill_defs(),
		"enemy_templates": game_session.get_enemy_templates(),
		"enemy_ai_brains": game_session.get_enemy_ai_brains(),
	})
	_assert_eq(enemy_units.size(), 5, "mist_hollow 第 2 阶段应构建 5 个敌方单位。")
	_assert_eq(_count_units_with_name_prefix(enemy_units, "雾沼异兽·"), 2, "mist_hollow 第 2 阶段应包含 2 个雾沼异兽。")
	_assert_eq(_count_units_with_name_prefix(enemy_units, "雾沼猎压者·"), 2, "mist_hollow 第 2 阶段应包含 2 个雾沼猎压者。")
	_assert_eq(_count_units_with_exact_name(enemy_units, "雾沼织咒者"), 1, "mist_hollow 第 2 阶段应包含 1 个雾沼织咒者。")
	_assert_eq(_count_units_with_brain(enemy_units, &"ranged_suppressor"), 2, "雾沼猎压者应使用 ranged_suppressor brain。")
	_assert_eq(_count_units_with_brain(enemy_units, &"healer_controller"), 1, "雾沼织咒者应使用 healer_controller brain。")
	_assert_true(
		_unit_has_skill(enemy_units, "雾沼织咒者", &"mage_glacial_prison"),
		"雾沼织咒者应携带控制技能 mage_glacial_prison。"
	)
	game_session.free()


func _test_wild_encounter_units_always_include_six_base_attributes() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var builder = ENCOUNTER_ROSTER_BUILDER_SCRIPT.new()
	builder.setup(game_session.get_wild_encounter_rosters(), game_session.get_enemy_templates())

	var wolf_anchor = _build_settlement_encounter_anchor(&"wolf_den_base_attributes", Vector2i(4, 4))
	wolf_anchor.growth_stage = 4
	var wolf_units: Array = builder.build_enemy_units(wolf_anchor, {
		"battle_seed": 4101,
		"skill_defs": game_session.get_skill_defs(),
		"enemy_templates": game_session.get_enemy_templates(),
		"enemy_ai_brains": game_session.get_enemy_ai_brains(),
	})
	for unit_variant in wolf_units:
		_assert_snapshot_has_all_base_attributes(unit_variant, "wolf_den mixed roster")

	var mist_anchor = ENCOUNTER_ANCHOR_DATA_SCRIPT.new()
	mist_anchor.entity_id = &"mist_hollow_base_attributes"
	mist_anchor.display_name = "雾沼伏猎群"
	mist_anchor.world_coord = Vector2i(8, 8)
	mist_anchor.faction_id = &"hostile"
	mist_anchor.enemy_roster_template_id = &"mist_beast"
	mist_anchor.region_tag = &"south_wilds"
	mist_anchor.vision_range = 2
	mist_anchor.encounter_profile_id = &"mist_hollow"
	mist_anchor.growth_stage = 2
	var mist_units: Array = builder.build_enemy_units(mist_anchor, {
		"battle_seed": 4102,
		"skill_defs": game_session.get_skill_defs(),
		"enemy_templates": game_session.get_enemy_templates(),
		"enemy_ai_brains": game_session.get_enemy_ai_brains(),
	})
	for unit_variant in mist_units:
		_assert_snapshot_has_all_base_attributes(unit_variant, "mist_hollow mixed roster")
	game_session.free()


func _test_beast_wild_units_roll_deterministic_5d3_minus_1_base_attributes() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var builder = ENCOUNTER_ROSTER_BUILDER_SCRIPT.new()
	builder.setup(game_session.get_wild_encounter_rosters(), game_session.get_enemy_templates())

	var encounter_anchor = _build_settlement_encounter_anchor(&"wolf_den_beast_roll", Vector2i(4, 4))
	encounter_anchor.growth_stage = 1
	var build_context := {
		"battle_seed": 5119,
		"skill_defs": game_session.get_skill_defs(),
		"enemy_templates": game_session.get_enemy_templates(),
		"enemy_ai_brains": game_session.get_enemy_ai_brains(),
	}
	var first_units: Array = builder.build_enemy_units(encounter_anchor, build_context)
	var second_units: Array = builder.build_enemy_units(encounter_anchor, build_context)
	var first_wolf = _find_first_unit_with_brain(first_units, &"melee_aggressor")
	var second_wolf = _find_first_unit_with_brain(second_units, &"melee_aggressor")
	_assert_true(first_wolf != null and second_wolf != null, "beast 六维掷骰回归应能找到正式荒狼单位。")
	if first_wolf != null and second_wolf != null:
		for attribute_id in UNIT_BASE_ATTRIBUTES_SCRIPT.BASE_ATTRIBUTE_IDS:
			var first_value := int(first_wolf.attribute_snapshot.get_value(attribute_id))
			var second_value := int(second_wolf.attribute_snapshot.get_value(attribute_id))
			_assert_eq(
				first_value,
				second_value,
				"同一 battle_seed 下 beast 六维应稳定复现 %s。" % String(attribute_id)
			)
			_assert_true(
				first_value >= 4 and first_value <= 14,
				"beast 六维 %s 应满足 5D3-1 的正式区间。" % String(attribute_id)
			)
	game_session.free()


func _test_wild_encounter_growth_respects_suppression_window() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var world_time_system = WORLD_TIME_SYSTEM_SCRIPT.new()
	var growth_system = WILD_ENCOUNTER_GROWTH_SYSTEM_SCRIPT.new()
	var encounter_anchor = _build_settlement_encounter_anchor(&"wolf_den_growth", Vector2i(5, 5))
	var world_data := {
		"world_step": 0,
		"encounter_anchors": [encounter_anchor],
	}

	var advance_result = world_time_system.advance(world_data, 2)
	growth_system.apply_step_advance(
		world_data,
		int(advance_result.get("old_step", 0)),
		int(advance_result.get("new_step", 0)),
		game_session.get_wild_encounter_rosters()
	)
	_assert_eq(encounter_anchor.growth_stage, 1, "聚落类野怪应在到达成长间隔后提升阶段。")

	var victory_applied := growth_system.apply_battle_victory(
		encounter_anchor,
		int(world_data.get("world_step", 0)),
		game_session.get_wild_encounter_rosters()
	)
	_assert_true(victory_applied, "聚落类野怪战后应能应用压制逻辑。")
	_assert_eq(encounter_anchor.growth_stage, 0, "聚落类野怪战胜后应至少降回初始阶段。")
	_assert_eq(encounter_anchor.suppressed_until_step, 5, "wolf_den 战胜后的压制时间应按配置推进 3 step。")

	advance_result = world_time_system.advance(world_data, 2)
	growth_system.apply_step_advance(
		world_data,
		int(advance_result.get("old_step", 0)),
		int(advance_result.get("new_step", 0)),
		game_session.get_wild_encounter_rosters()
	)
	_assert_eq(encounter_anchor.growth_stage, 0, "压制期内推进世界时间不应让聚落类野怪恢复增长。")

	advance_result = world_time_system.advance(world_data, 3)
	growth_system.apply_step_advance(
		world_data,
		int(advance_result.get("old_step", 0)),
		int(advance_result.get("new_step", 0)),
		game_session.get_wild_encounter_rosters()
	)
	_assert_eq(encounter_anchor.growth_stage, 1, "压制期结束后，聚落类野怪应重新按成长间隔恢复增长。")
	game_session.free()


func _test_game_runtime_facade_move_advances_world_step() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return
	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)

	var before_snapshot: Dictionary = facade.build_headless_snapshot()
	var before_world: Dictionary = before_snapshot.get("world", {})
	var command_result: Dictionary = facade.command_world_move(Vector2i.RIGHT)
	var after_snapshot: Dictionary = facade.build_headless_snapshot()
	var after_world: Dictionary = after_snapshot.get("world", {})

	_assert_true(bool(command_result.get("ok", false)), "command_world_move() 应能返回成功结果。")
	_assert_eq(
		int(after_world.get("world_step", -1)),
		int(before_world.get("world_step", -1)) + 1,
		"世界地图移动一步后应同步推进 world_step。"
	)
	_assert_eq(
		int(game_session.get_world_data().get("world_step", -1)),
		int(after_world.get("world_step", -1)),
		"GameSession 持有的 world_data 应与 facade 的 world_step 保持一致。"
	)
	_cleanup_test_session(game_session)


func _test_test_preset_uses_same_battle_terrain_profile_as_small_preset() -> void:
	var test_session = _create_session(TEST_WORLD_CONFIG)
	var small_session = _create_session(SMALL_WORLD_CONFIG)
	if test_session == null or small_session == null:
		_cleanup_test_session(test_session)
		_cleanup_test_session(small_session)
		return

	var test_facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	var small_facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	test_facade.setup(test_session)
	small_facade.setup(small_session)

	var test_anchor = _find_encounter_anchor_by_region_tag(test_session.get_world_data(), &"north_wilds")
	var small_anchor = _find_encounter_anchor_by_region_tag(small_session.get_world_data(), &"north_wilds")
	_assert_true(test_anchor != null, "测试世界应至少包含一个 north_wilds 野外遭遇。")
	_assert_true(small_anchor != null, "small 预设应至少包含一个 north_wilds 野外遭遇。")
	if test_anchor == null or small_anchor == null:
		_cleanup_test_session(test_session)
		_cleanup_test_session(small_session)
		return

	var test_context: Dictionary = test_facade._build_battle_start_context(test_anchor)
	var small_context: Dictionary = small_facade._build_battle_start_context(small_anchor)
	_assert_eq(
		String(test_context.get("battle_terrain_profile", "")),
		String(small_context.get("battle_terrain_profile", "")),
		"测试预设与 small 预设的同类野外区域应使用同一 battle terrain profile。"
	)

	var test_state = test_facade._battle_runtime.start_battle(test_anchor, 1201, test_context)
	var small_state = small_facade._battle_runtime.start_battle(small_anchor, 1201, small_context)
	_assert_true(test_state != null and not test_state.is_empty(), "测试预设应能基于正式 battle start 流程生成战斗状态。")
	_assert_true(small_state != null and not small_state.is_empty(), "small 预设应能基于正式 battle start 流程生成战斗状态。")
	if test_state != null and small_state != null:
		_assert_eq(
			String(test_state.terrain_profile_id),
			String(small_state.terrain_profile_id),
			"测试预设与 small 预设进入同类野外战斗时，最终战斗地形 profile 应保持一致。"
		)

	_cleanup_test_session(test_session)
	_cleanup_test_session(small_session)


func _test_main_world_wilds_use_canyon_battle_terrain_profile() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return
	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)

	for region_tag in [&"north_wilds", &"south_wilds"]:
		var encounter_anchor = _find_encounter_anchor_by_region_tag(game_session.get_world_data(), region_tag)
		_assert_true(encounter_anchor != null, "正式主世界应至少包含 %s 野外遭遇。" % String(region_tag))
		if encounter_anchor == null:
			continue
		var battle_context: Dictionary = facade._build_battle_start_context(encounter_anchor)
		_assert_eq(
			String(battle_context.get("battle_terrain_profile", "")),
			"canyon",
			"正式主世界 %s 野外遭遇应走 canyon battle terrain profile，确保战斗棋盘使用 20px 视觉层距。" % String(region_tag)
		)
		var battle_state = facade._battle_runtime.start_battle(encounter_anchor, 1203, battle_context)
		_assert_true(battle_state != null and not battle_state.is_empty(), "正式主世界 %s 野外遭遇应能创建战斗状态。" % String(region_tag))
		if battle_state != null and not battle_state.is_empty():
			_assert_eq(
				String(battle_state.terrain_profile_id),
				"canyon",
				"正式主世界 %s 野外遭遇进入战斗后应保留 canyon terrain_profile_id。" % String(region_tag)
			)

	_cleanup_test_session(game_session)


func _test_formal_wolf_den_battle_prefers_close_in_over_wait_when_far() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return
	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)

	var encounter_anchor = _find_encounter_anchor_by_region_tag(game_session.get_world_data(), &"north_wilds")
	_assert_true(encounter_anchor != null, "正式狼巢 AI 接敌回归需要一个 north_wilds 遭遇。")
	if encounter_anchor == null:
		_cleanup_test_session(game_session)
		return

	var battle_context: Dictionary = facade._build_battle_start_context(encounter_anchor)
	var battle_state = facade._battle_runtime.start_battle(encounter_anchor, 1202, battle_context)
	_assert_true(battle_state != null and not battle_state.is_empty(), "正式狼巢 AI 接敌回归应能创建正式战斗状态。")
	if battle_state == null or battle_state.is_empty():
		_cleanup_test_session(game_session)
		return

	var wolf_unit = _find_first_enemy_unit_with_brain(battle_state, &"melee_aggressor")
	var player_unit = battle_state.units.get(battle_state.ally_unit_ids[0]) if not battle_state.ally_unit_ids.is_empty() else null
	_assert_true(wolf_unit != null, "正式狼巢 AI 接敌回归应至少找到一个 melee_aggressor 荒狼单位。")
	_assert_true(player_unit != null, "正式狼巢 AI 接敌回归应至少找到一个玩家单位。")
	if wolf_unit == null or player_unit == null:
		_cleanup_test_session(game_session)
		return

	_reduce_state_to_duel(facade._battle_runtime._grid_service, battle_state, wolf_unit, player_unit)
	var duel_coords := _find_far_gap_duel_coords(
		facade._battle_runtime._grid_service,
		battle_state,
		wolf_unit,
		player_unit,
		8
	)
	_assert_true(not duel_coords.is_empty(), "正式狼巢 AI 接敌回归应能在正式地图上找到一组远距离可接敌的对战坐标。")
	if duel_coords.is_empty():
		_cleanup_test_session(game_session)
		return

	var wolf_coord: Vector2i = duel_coords.get("enemy_coord", Vector2i(-1, -1))
	var player_coord: Vector2i = duel_coords.get("player_coord", Vector2i(-1, -1))
	var placed_wolf: bool = facade._battle_runtime._grid_service.place_unit(battle_state, wolf_unit, wolf_coord, true)
	var placed_player: bool = facade._battle_runtime._grid_service.place_unit(battle_state, player_unit, player_coord, true)
	_assert_true(placed_wolf and placed_player, "正式狼巢 AI 接敌回归应能把狼与玩家放到远距离有效落点。")
	if not placed_wolf or not placed_player:
		_cleanup_test_session(game_session)
		return

	var ai_context = BATTLE_AI_CONTEXT_SCRIPT.new()
	ai_context.state = battle_state
	ai_context.unit_state = wolf_unit
	ai_context.grid_service = facade._battle_runtime._grid_service
	ai_context.skill_defs = facade._battle_runtime._skill_defs
	ai_context.preview_callback = Callable(facade._battle_runtime, "preview_command")
	ai_context.skill_score_input_callback = Callable(facade._battle_runtime._ai_service, "build_skill_score_input")
	ai_context.action_score_input_callback = Callable(facade._battle_runtime._ai_service, "build_action_score_input")
	ai_context.trace_enabled = true

	var decision = facade._battle_runtime._ai_service.choose_command(ai_context)
	_assert_true(decision != null and decision.command != null, "正式狼巢 AI 接敌回归应产出合法 AI 指令。")
	_assert_eq(
		String(decision.action_id if decision != null else &""),
		"wolf_close_in",
		"正式狼巢中的远距离荒狼应优先走 wolf_close_in，而不是直接待机。"
	)
	_assert_eq(
		String(decision.command.command_type if decision != null and decision.command != null else &""),
		String(BATTLE_COMMAND_SCRIPT.TYPE_MOVE),
		"正式狼巢中的远距离荒狼应生成移动指令，而不是待机。"
	)
	_assert_true(
		decision != null and decision.score_input != null and decision.score_input.position_objective_kind == &"distance_band_progress",
		"正式狼巢中的 wolf_close_in 评分应使用 progress 型距离带评分。"
	)

	_cleanup_test_session(game_session)


func _test_world_spawn_explicitly_maps_south_wilds_to_mist_hollow() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return
	var south_anchor = _find_encounter_anchor_by_region_tag(game_session.get_world_data(), &"south_wilds")
	_assert_true(south_anchor != null, "测试世界应至少包含一个 south_wilds 野外遭遇。")
	if south_anchor != null:
		_assert_eq(String(south_anchor.encounter_profile_id), "mist_hollow", "south_wilds 野外遭遇应显式命中 mist_hollow roster。")
	_cleanup_test_session(game_session)


func _test_game_runtime_facade_battle_requires_confirm_before_tu_advances() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return
	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)

	var encounter_anchor = _find_encounter_anchor_by_kind(
		game_session.get_world_data(),
		ENCOUNTER_ANCHOR_DATA_SCRIPT.ENCOUNTER_KIND_SINGLE
	)
	_assert_true(encounter_anchor != null, "确认开战回归需要至少一个单体野怪遭遇。")
	if encounter_anchor == null:
		_cleanup_test_session(game_session)
		return

	game_session.set_battle_save_lock(true)
	facade._start_battle(encounter_anchor)
	var started_snapshot: Dictionary = facade.build_headless_snapshot().get("battle", {})
	_assert_true(bool(started_snapshot.get("active", false)), "正式 battle start 后应处于 battle active。")
	_assert_true(bool(started_snapshot.get("start_confirm_visible", false)), "正式 battle start 后应先弹出开始战斗确认。")
	_assert_eq(String(started_snapshot.get("modal_state", "")), "start_confirm", "确认前 battle modal_state 应保持在 start_confirm。")
	_assert_eq(facade._battle_runtime.get_state().timeline.current_tu, 0, "确认前 TU 应从 0 开始。")

	facade.advance(2.0)
	_assert_eq(facade._battle_runtime.get_state().timeline.current_tu, 0, "未确认开始战斗前，TU 不应增长。")

	var confirm_result: Dictionary = facade.command_confirm_battle_start()
	_assert_true(bool(confirm_result.get("ok", false)), "确认开始战斗命令应成功。")
	var confirmed_snapshot: Dictionary = facade.build_headless_snapshot().get("battle", {})
	_assert_true(not bool(confirmed_snapshot.get("start_confirm_visible", false)), "确认后开始战斗确认窗应关闭。")
	_assert_eq(String(confirmed_snapshot.get("modal_state", "")), "", "确认后 battle modal_state 应清空。")

	facade.command_battle_tick(1.0)
	_assert_eq(facade._battle_runtime.get_state().timeline.current_tu, 5, "确认后 battle tick 1 秒应推进 5 TU。")

	_cleanup_test_session(game_session)


func _test_game_runtime_facade_commit_battle_loot_records_overflow_entries() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return
	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)
	_force_party_storage_capacity(facade.get_party_state(), 1)
	facade.get_party_state().warehouse_state = WAREHOUSE_STATE_SCRIPT.new()

	var warehouse_service = PARTY_WAREHOUSE_SERVICE_SCRIPT.new()
	warehouse_service.setup(facade.get_party_state(), game_session.get_item_defs())
	warehouse_service.add_item(&"bronze_sword", 1)

	var resolution_result = BATTLE_RESOLUTION_RESULT_SCRIPT.new()
	resolution_result.winner_faction_id = &"player"
	resolution_result.set_loot_entries([_build_formal_beast_hide_loot_entry(2)])

	var commit_result: Dictionary = facade._commit_battle_loot_to_shared_warehouse(resolution_result)
	_assert_true(bool(commit_result.get("ok", false)), "battle loot commit 遇到容量不足时仍应以 overflow 方式完成。")
	_assert_eq(int(commit_result.get("committed_item_count", -1)), 0, "无空余格位时不应误写入 beast_hide。")
	_assert_eq(int(commit_result.get("overflow_entry_count", 0)), 1, "容量不足时 battle loot commit 应生成 1 条 overflow entry。")
	_assert_eq(resolution_result.overflow_entries.size(), 1, "BattleResolutionResult 应正式写入 overflow_entries。")
	if resolution_result.overflow_entries.size() > 0 and resolution_result.overflow_entries[0] is Dictionary:
		var overflow_entry: Dictionary = resolution_result.overflow_entries[0]
		_assert_eq(String(overflow_entry.get("item_id", "")), "beast_hide", "overflow entry 应保留原始掉落物品。")
		_assert_eq(int(overflow_entry.get("quantity", 0)), 2, "overflow entry 应保留未装下的数量。")
	_assert_eq(warehouse_service.count_item(&"bronze_sword"), 1, "battle loot overflow 不应影响原有仓库物品。")
	_assert_eq(warehouse_service.count_item(&"beast_hide"), 0, "battle loot overflow 不应静默写入 beast_hide。")
	_cleanup_test_session(game_session)


func _test_game_runtime_facade_single_victory_removes_encounter() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return
	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)

	var encounter_anchor = _find_encounter_anchor_by_kind(
		game_session.get_world_data(),
		ENCOUNTER_ANCHOR_DATA_SCRIPT.ENCOUNTER_KIND_SINGLE
	)
	_assert_true(encounter_anchor != null, "测试世界应至少包含一个单体野怪遭遇。")
	if encounter_anchor == null:
		_cleanup_test_session(game_session)
		return

	var before_count := _count_encounter_anchors(game_session.get_world_data())
	game_session.set_battle_save_lock(true)
	facade._start_battle(encounter_anchor)
	_mark_active_battle_as_player_victory(facade)
	facade._resolve_active_battle()

	var after_count := _count_encounter_anchors(game_session.get_world_data())
	var remaining_anchor = _find_encounter_anchor_by_id(game_session.get_world_data(), encounter_anchor.entity_id)
	_assert_true(remaining_anchor == null, "单体野怪战斗胜利后应从世界锚点列表中移除。")
	_assert_eq(after_count, before_count - 1, "单体野怪战斗胜利后，世界遭遇总数应减少 1。")
	_assert_true(
		not bool(facade.build_headless_snapshot().get("battle", {}).get("active", false)),
		"战斗结算完成后，battle 快照应回到 inactive。"
	)
	_assert_true(not game_session.is_battle_save_locked(), "战斗结算完成后应释放 battle save lock。")
	_cleanup_test_session(game_session)


func _test_game_runtime_facade_can_start_second_battle_after_first_victory() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return
	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)

	var first_anchor = _find_encounter_anchor_by_kind(
		game_session.get_world_data(),
		ENCOUNTER_ANCHOR_DATA_SCRIPT.ENCOUNTER_KIND_SINGLE
	)
	_assert_true(first_anchor != null, "连续遭遇回归需要至少一个单体野怪遭遇作为首战。")
	if first_anchor == null:
		_cleanup_test_session(game_session)
		return

	game_session.set_battle_save_lock(true)
	facade._start_battle(first_anchor)
	_mark_active_battle_as_player_victory(facade)
	facade._resolve_active_battle()

	var second_anchor = _find_first_other_encounter_anchor(game_session.get_world_data(), first_anchor.entity_id)
	_assert_true(second_anchor != null, "连续遭遇回归需要在首战后仍保留至少一个后续遭遇。")
	if second_anchor == null:
		_cleanup_test_session(game_session)
		return

	game_session.set_battle_save_lock(true)
	facade._start_battle(second_anchor)
	var second_state = facade._battle_runtime.get_state()
	_assert_true(second_state != null and not second_state.is_empty(), "同一 world session 的第二场战斗应能成功启动。")
	if second_state != null:
		_assert_true(not second_state.ally_unit_ids.is_empty(), "第二场战斗应继续构建友军单位，而不是空队伍。")
	_cleanup_test_session(game_session)


func _test_game_runtime_facade_settlement_victory_downgrades_encounter() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return
	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)
	var warehouse_service = PARTY_WAREHOUSE_SERVICE_SCRIPT.new()
	warehouse_service.setup(facade.get_party_state(), game_session.get_item_defs())

	var encounter_anchor = _find_encounter_anchor_by_kind(
		game_session.get_world_data(),
		ENCOUNTER_ANCHOR_DATA_SCRIPT.ENCOUNTER_KIND_SETTLEMENT
	)
	_assert_true(encounter_anchor != null, "测试世界应至少包含一个聚落类野怪遭遇。")
	if encounter_anchor == null:
		_cleanup_test_session(game_session)
		return

	encounter_anchor.growth_stage = 3
	facade.get_world_data()["world_step"] = 4
	var beast_hide_before_victory := warehouse_service.count_item(&"beast_hide")
	game_session.set_battle_save_lock(true)
	facade._start_battle(encounter_anchor)
	_mark_active_battle_as_player_victory(facade)
	facade._resolve_active_battle()
	warehouse_service.setup(facade.get_party_state(), game_session.get_item_defs())

	var remaining_anchor = _find_encounter_anchor_by_id(game_session.get_world_data(), encounter_anchor.entity_id)
	_assert_true(remaining_anchor != null, "聚落类野怪战斗胜利后应继续保留在世界锚点中。")
	if remaining_anchor != null:
		_assert_eq(remaining_anchor.growth_stage, 2, "聚落类野怪战斗胜利后应下降 1 个成长阶段。")
		_assert_eq(remaining_anchor.suppressed_until_step, 7, "聚落类野怪战斗胜利后应写入压制截止 step。")
	_assert_eq(warehouse_service.count_item(&"beast_hide"), beast_hide_before_victory + 4, "聚落类 wild encounter 胜利后应把本场击杀聚合后的正式 loot 写入 shared warehouse。")
	_assert_true(
		not bool(facade.build_headless_snapshot().get("battle", {}).get("active", false)),
		"聚落类野怪战后结算完成后，battle 快照应回到 inactive。"
	)
	_assert_true(not game_session.is_battle_save_locked(), "聚落类野怪战后结算完成后应释放 battle save lock。")
	_cleanup_test_session(game_session)


func _test_game_runtime_facade_battle_overflow_feedback_surfaces_in_message_and_snapshot() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return
	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)
	_force_party_storage_capacity(facade.get_party_state(), 1)
	facade.get_party_state().warehouse_state = WAREHOUSE_STATE_SCRIPT.new()

	var warehouse_service = PARTY_WAREHOUSE_SERVICE_SCRIPT.new()
	warehouse_service.setup(facade.get_party_state(), game_session.get_item_defs())
	warehouse_service.add_item(&"bronze_sword", 1)
	var beast_hide_def = game_session.get_item_defs().get(&"beast_hide")
	var beast_hide_label := String(beast_hide_def.display_name) if beast_hide_def != null else "beast_hide"

	var encounter_anchor = _find_encounter_anchor_by_kind(
		game_session.get_world_data(),
		ENCOUNTER_ANCHOR_DATA_SCRIPT.ENCOUNTER_KIND_SETTLEMENT
	)
	_assert_true(encounter_anchor != null, "battle overflow 反馈回归需要一个聚落类野怪遭遇。")
	if encounter_anchor == null:
		_cleanup_test_session(game_session)
		return

	encounter_anchor.growth_stage = 3
	game_session.set_battle_save_lock(true)
	facade._start_battle(encounter_anchor)
	var confirm_result: Dictionary = facade.command_confirm_battle_start()
	_assert_true(bool(confirm_result.get("ok", false)), "battle overflow 反馈回归中，开始战斗确认命令应成功返回。")
	_mark_active_battle_as_player_victory(facade)
	var resolve_result: Dictionary = facade.command_battle_wait_or_resolve()
	warehouse_service.setup(facade.get_party_state(), game_session.get_item_defs())

	_assert_true(bool(resolve_result.get("ok", false)), "battle overflow 反馈回归中，结束战斗命令应成功返回。")
	_assert_true(String(resolve_result.get("message", "")).find("未装下的掉落") >= 0, "battle 结算 message 应显式提示未装下的掉落。")
	_assert_true(String(resolve_result.get("message", "")).find(beast_hide_label) >= 0, "battle 结算 message 应包含未装下的掉落名称。")
	_assert_eq(warehouse_service.count_item(&"bronze_sword"), 1, "battle overflow 后原有仓库占位物应保留。")
	_assert_eq(warehouse_service.count_item(&"beast_hide"), 0, "battle overflow 后 beast_hide 不应误写入共享仓库。")

	var snapshot: Dictionary = facade.build_headless_snapshot()
	var status_text := String(snapshot.get("status", {}).get("text", ""))
	var text_snapshot := facade.build_text_snapshot()
	var loot_snapshot: Dictionary = snapshot.get("loot", {})
	_assert_true(status_text.find("未装下的掉落") >= 0, "headless snapshot status.text 应显式提示未装下的掉落。")
	_assert_true(status_text.find(beast_hide_label) >= 0, "headless snapshot status.text 应包含未装下的掉落名称。")
	_assert_eq(String(loot_snapshot.get("loot_summary_text", "")), "%s x4" % beast_hide_label, "headless snapshot 应暴露本次战斗的 loot 摘要。")
	_assert_eq(String(loot_snapshot.get("overflow_summary_text", "")), "%s x4" % beast_hide_label, "headless snapshot 应暴露本次战斗的 overflow 摘要。")
	_assert_true(text_snapshot.find("未装下的掉落") >= 0, "text snapshot 应显式提示未装下的掉落。")
	_assert_true(text_snapshot.find(beast_hide_label) >= 0, "text snapshot 应包含未装下的掉落名称。")
	_assert_true(text_snapshot.find("[LOOT]") >= 0, "text snapshot 应渲染 LOOT 分段。")
	_assert_true(text_snapshot.find("loot_summary=%s x4" % beast_hide_label) >= 0, "text snapshot 应渲染本次战斗 loot 摘要。")
	_assert_true(text_snapshot.find("overflow_summary=%s x4" % beast_hide_label) >= 0, "text snapshot 应渲染本次战斗 overflow 摘要。")
	var resolved_log := _find_recent_log_entry(game_session.get_log_snapshot().get("entries", []), "battle.resolved")
	_assert_true(not resolved_log.is_empty(), "battle overflow 结算后应写入 battle.resolved 日志。")
	if not resolved_log.is_empty():
		var log_context: Dictionary = resolved_log.get("context", {})
		_assert_eq(int(log_context.get("overflow_entry_count", 0)), 1, "battle.resolved 日志应记录 overflow entry 数量。")
		_assert_eq((log_context.get("loot_overflow_entries", []) as Array).size(), 1, "battle.resolved 日志应记录 overflow entries。")
	_assert_true(not game_session.is_battle_save_locked(), "battle overflow 结算完成后应释放 battle save lock。")
	_cleanup_test_session(game_session)


func _create_test_session():
	return _create_session(TEST_WORLD_CONFIG)


func _create_session(config_path: String):
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(config_path))
	_assert_true(create_error == OK, "GameSession 应能基于配置 %s 创建新存档。" % config_path)
	if create_error != OK:
		_cleanup_test_session(game_session)
		return null
	return game_session


func _cleanup_test_session(game_session) -> void:
	if game_session == null:
		return
	game_session.clear_persisted_game()
	game_session.free()


func _build_settlement_encounter_anchor(entity_id: StringName, coord: Vector2i):
	var encounter_anchor = ENCOUNTER_ANCHOR_DATA_SCRIPT.new()
	encounter_anchor.entity_id = entity_id
	encounter_anchor.display_name = "荒狼巢穴"
	encounter_anchor.world_coord = coord
	encounter_anchor.faction_id = &"hostile"
	encounter_anchor.enemy_roster_template_id = &"wolf_pack"
	encounter_anchor.region_tag = &"north_wilds"
	encounter_anchor.vision_range = 2
	encounter_anchor.encounter_kind = ENCOUNTER_ANCHOR_DATA_SCRIPT.ENCOUNTER_KIND_SETTLEMENT
	encounter_anchor.encounter_profile_id = &"wolf_den"
	encounter_anchor.growth_stage = 0
	encounter_anchor.suppressed_until_step = 0
	return encounter_anchor


func _mark_active_battle_as_player_victory(facade) -> void:
	var runtime_state = facade._battle_runtime.get_state()
	_assert_true(runtime_state != null and not runtime_state.is_empty(), "遭遇战应能创建可结算的 battle runtime state。")
	if runtime_state == null or runtime_state.is_empty():
		return
	var default_killer = runtime_state.units.get(runtime_state.ally_unit_ids[0]) if not runtime_state.ally_unit_ids.is_empty() else null
	for enemy_unit_id in runtime_state.enemy_unit_ids:
		var enemy_unit = runtime_state.units.get(enemy_unit_id)
		if enemy_unit == null or not enemy_unit.is_alive:
			continue
		enemy_unit.is_alive = false
		facade._battle_runtime._collect_defeated_unit_loot(enemy_unit, default_killer)
	runtime_state.phase = &"battle_ended"
	runtime_state.winner_faction_id = &"player"
	facade._refresh_battle_runtime_state()


func _find_encounter_anchor_by_kind(world_data: Dictionary, encounter_kind: StringName):
	for encounter_variant in world_data.get("encounter_anchors", []):
		var encounter_anchor = encounter_variant as ENCOUNTER_ANCHOR_DATA_SCRIPT
		if encounter_anchor == null:
			continue
		if encounter_anchor.encounter_kind == encounter_kind:
			return encounter_anchor
	return null


func _find_encounter_anchor_by_id(world_data: Dictionary, encounter_id: StringName):
	for encounter_variant in world_data.get("encounter_anchors", []):
		var encounter_anchor = encounter_variant as ENCOUNTER_ANCHOR_DATA_SCRIPT
		if encounter_anchor == null:
			continue
		if encounter_anchor.entity_id == encounter_id:
			return encounter_anchor
	return null


func _find_encounter_anchor_by_region_tag(world_data: Dictionary, region_tag: StringName):
	for encounter_variant in world_data.get("encounter_anchors", []):
		var encounter_anchor = encounter_variant as ENCOUNTER_ANCHOR_DATA_SCRIPT
		if encounter_anchor == null:
			continue
		if encounter_anchor.region_tag == region_tag:
			return encounter_anchor
	return null


func _find_first_other_encounter_anchor(world_data: Dictionary, excluded_encounter_id: StringName):
	for encounter_variant in world_data.get("encounter_anchors", []):
		var encounter_anchor = encounter_variant as ENCOUNTER_ANCHOR_DATA_SCRIPT
		if encounter_anchor == null or encounter_anchor.entity_id == excluded_encounter_id:
			continue
		return encounter_anchor
	return null


func _force_party_storage_capacity(party_state, capacity: int) -> void:
	if party_state == null:
		return
	var resolved_capacity := maxi(capacity, 0)
	var first_member_assigned := false
	for member_variant in party_state.member_states.values():
		var member_state = member_variant
		if member_state == null or member_state.progression == null or member_state.progression.unit_base_attributes == null:
			continue
		member_state.progression.unit_base_attributes.custom_stats[&"storage_space"] = resolved_capacity if not first_member_assigned else 0
		first_member_assigned = true


func _build_formal_beast_hide_loot_entry(quantity: int) -> Dictionary:
	return {
		"drop_type": "item",
		"drop_source_kind": "encounter_roster",
		"drop_source_id": "wolf_den",
		"drop_source_label": "荒狼巢穴",
		"drop_entry_id": "wolf_den_hide_bundle",
		"item_id": "beast_hide",
		"quantity": quantity,
	}


func _find_recent_log_entry(entries_variant, event_id: String) -> Dictionary:
	if entries_variant is not Array:
		return {}
	for index in range(entries_variant.size() - 1, -1, -1):
		var entry_variant = entries_variant[index]
		if entry_variant is not Dictionary:
			continue
		var entry: Dictionary = entry_variant
		if String(entry.get("event_id", "")) == event_id:
			return entry
	return {}


func _count_encounter_anchors(world_data: Dictionary) -> int:
	var count := 0
	for encounter_variant in world_data.get("encounter_anchors", []):
		var encounter_anchor = encounter_variant as ENCOUNTER_ANCHOR_DATA_SCRIPT
		if encounter_anchor != null:
			count += 1
	return count


func _count_units_with_name_prefix(units: Array, prefix: String) -> int:
	var count := 0
	for unit_variant in units:
		var display_name := String(unit_variant.display_name if unit_variant != null else "")
		if display_name.begins_with(prefix):
			count += 1
	return count


func _count_units_with_exact_name(units: Array, expected_name: String) -> int:
	var count := 0
	for unit_variant in units:
		var display_name := String(unit_variant.display_name if unit_variant != null else "")
		if display_name == expected_name:
			count += 1
	return count


func _count_units_with_brain(units: Array, brain_id: StringName) -> int:
	var count := 0
	for unit_variant in units:
		if unit_variant == null:
			continue
		if unit_variant.ai_brain_id == brain_id:
			count += 1
	return count


func _unit_has_skill(units: Array, display_name: String, skill_id: StringName) -> bool:
	for unit_variant in units:
		if unit_variant == null:
			continue
		if String(unit_variant.display_name) != display_name:
			continue
		return unit_variant.known_active_skill_ids.has(skill_id)
	return false


func _find_first_unit_with_brain(units: Array, brain_id: StringName):
	for unit_variant in units:
		if unit_variant == null:
			continue
		if unit_variant.ai_brain_id == brain_id:
			return unit_variant
	return null


func _assert_snapshot_has_all_base_attributes(unit_state, scope: String) -> void:
	if unit_state == null:
		_failures.append("%s 应产出有效单位。" % scope)
		return
	for attribute_id in UNIT_BASE_ATTRIBUTES_SCRIPT.BASE_ATTRIBUTE_IDS:
		var value := int(unit_state.attribute_snapshot.get_value(attribute_id))
		if value <= 0:
			_failures.append("%s 缺失基础六维 %s。" % [scope, String(attribute_id)])


func _find_first_enemy_unit_with_brain(battle_state, brain_id: StringName):
	if battle_state == null:
		return null
	for enemy_unit_id in battle_state.enemy_unit_ids:
		var enemy_unit = battle_state.units.get(enemy_unit_id)
		if enemy_unit == null:
			continue
		if enemy_unit.ai_brain_id == brain_id and enemy_unit.is_alive:
			return enemy_unit
	return null


func _reduce_state_to_duel(grid_service, battle_state, enemy_unit, player_unit) -> void:
	if grid_service == null or battle_state == null or enemy_unit == null or player_unit == null:
		return
	for unit_variant in battle_state.units.values():
		var unit_state = unit_variant
		if unit_state == null:
			continue
		grid_service.clear_unit_occupancy(battle_state, unit_state)
		unit_state.is_alive = unit_state.unit_id == enemy_unit.unit_id or unit_state.unit_id == player_unit.unit_id
	battle_state.enemy_unit_ids.clear()
	battle_state.enemy_unit_ids.append(enemy_unit.unit_id)
	battle_state.ally_unit_ids.clear()
	battle_state.ally_unit_ids.append(player_unit.unit_id)
	enemy_unit.is_alive = true
	player_unit.is_alive = true


func _find_far_gap_duel_coords(
	grid_service,
	battle_state,
	enemy_unit,
	player_unit,
	minimum_distance: int
) -> Dictionary:
	if grid_service == null or battle_state == null or enemy_unit == null or player_unit == null:
		return {}
	var candidate_coords: Array[Vector2i] = []
	for coord_variant in battle_state.cells.keys():
		if coord_variant is not Vector2i:
			continue
		var coord: Vector2i = coord_variant
		if not grid_service.can_place_unit(battle_state, enemy_unit, coord, true):
			continue
		if not grid_service.can_place_unit(battle_state, player_unit, coord, true):
			continue
		candidate_coords.append(coord)

	var best_pair: Dictionary = {}
	var best_distance := -1
	for enemy_coord in candidate_coords:
		for player_coord in candidate_coords:
			if enemy_coord == player_coord:
				continue
			if enemy_coord.x == player_coord.x or enemy_coord.y == player_coord.y:
				continue
			var distance: int = grid_service.get_distance(enemy_coord, player_coord)
			if distance < minimum_distance:
				continue
			if not _has_progress_step_toward_target(grid_service, battle_state, enemy_unit, enemy_coord, player_coord, distance):
				continue
			if distance <= best_distance:
				continue
			best_distance = distance
			best_pair = {
				"enemy_coord": enemy_coord,
				"player_coord": player_coord,
			}
	return best_pair


func _has_progress_step_toward_target(
	grid_service,
	battle_state,
	enemy_unit,
	enemy_coord: Vector2i,
	player_coord: Vector2i,
	current_distance: int
) -> bool:
	for neighbor in grid_service.get_neighbors_4(battle_state, enemy_coord):
		if not grid_service.can_unit_step_between_anchors(battle_state, enemy_unit, enemy_coord, neighbor):
			continue
		if grid_service.get_distance(neighbor, player_coord) < current_distance:
			return true
	return false


func _errors_contain_fragment(errors: Array[String], fragment: String) -> bool:
	for error_text in errors:
		if String(error_text).find(fragment) >= 0:
			return true
	return false


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
