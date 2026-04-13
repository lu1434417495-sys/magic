## 文件说明：该脚本属于野外遭遇成长回归测试相关的回归脚本，集中覆盖聚落类野怪的序列化、成长推进与战后分支。
## 审查重点：重点核对新增字段兼容、混编敌方编队构建、世界时间推进与战斗收尾后的世界状态是否稳定。
## 备注：后续若聚落类野怪改为可重建刷新，需要同步补充重建阶段与地图可见性测试。

extends SceneTree

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/game_session.gd")
const GAME_RUNTIME_FACADE_SCRIPT = preload("res://scripts/systems/game_runtime_facade.gd")
const ENCOUNTER_ANCHOR_DATA_SCRIPT = preload("res://scripts/systems/encounter_anchor_data.gd")
const ENCOUNTER_ROSTER_BUILDER_SCRIPT = preload("res://scripts/systems/encounter_roster_builder.gd")
const WORLD_TIME_SYSTEM_SCRIPT = preload("res://scripts/systems/world_time_system.gd")
const WILD_ENCOUNTER_GROWTH_SYSTEM_SCRIPT = preload("res://scripts/systems/wild_encounter_growth_system.gd")

const TEST_WORLD_CONFIG := "res://data/configs/world_map/test_world_map_config.tres"

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_encounter_anchor_round_trip_preserves_growth_fields()
	_test_encounter_roster_builder_builds_mixed_wolf_den_units()
	_test_wild_encounter_growth_respects_suppression_window()
	_test_game_runtime_facade_move_advances_world_step()
	_test_game_runtime_facade_single_victory_removes_encounter()
	_test_game_runtime_facade_settlement_victory_downgrades_encounter()
	if _failures.is_empty():
		print("Wild encounter regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Wild encounter regression: FAIL (%d)" % _failures.size())
	quit(1)


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
	builder.setup(game_session.get_wild_encounter_rosters())

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


func _test_game_runtime_facade_settlement_victory_downgrades_encounter() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return
	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)

	var encounter_anchor = _find_encounter_anchor_by_kind(
		game_session.get_world_data(),
		ENCOUNTER_ANCHOR_DATA_SCRIPT.ENCOUNTER_KIND_SETTLEMENT
	)
	_assert_true(encounter_anchor != null, "测试世界应至少包含一个聚落类野怪遭遇。")
	if encounter_anchor == null:
		_cleanup_test_session(game_session)
		return

	encounter_anchor.growth_stage = 3
	facade._world_data["world_step"] = 4
	game_session.set_battle_save_lock(true)
	facade._start_battle(encounter_anchor)
	_mark_active_battle_as_player_victory(facade)
	facade._resolve_active_battle()

	var remaining_anchor = _find_encounter_anchor_by_id(game_session.get_world_data(), encounter_anchor.entity_id)
	_assert_true(remaining_anchor != null, "聚落类野怪战斗胜利后应继续保留在世界锚点中。")
	if remaining_anchor != null:
		_assert_eq(remaining_anchor.growth_stage, 2, "聚落类野怪战斗胜利后应下降 1 个成长阶段。")
		_assert_eq(remaining_anchor.suppressed_until_step, 7, "聚落类野怪战斗胜利后应写入压制截止 step。")
	_assert_true(
		not bool(facade.build_headless_snapshot().get("battle", {}).get("active", false)),
		"聚落类野怪战后结算完成后，battle 快照应回到 inactive。"
	)
	_assert_true(not game_session.is_battle_save_locked(), "聚落类野怪战后结算完成后应释放 battle save lock。")
	_cleanup_test_session(game_session)


func _create_test_session():
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_true(create_error == OK, "GameSession 应能基于测试世界配置创建新存档。")
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


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
