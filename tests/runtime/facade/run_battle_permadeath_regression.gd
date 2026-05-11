extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const GameSession = preload("res://scripts/systems/persistence/game_session.gd")
const GameRuntimeFacade = preload("res://scripts/systems/game_runtime/game_runtime_facade.gd")
const BattleResolutionResult = preload("res://scripts/systems/battle/core/battle_resolution_result.gd")
const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleTimelineState = preload("res://scripts/systems/battle/core/battle_timeline_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const EquipmentState = preload("res://scripts/player/equipment/equipment_state.gd")
const PartyMemberState = preload("res://scripts/player/progression/party_member_state.gd")
const ProgressionDataUtils = preload("res://scripts/player/progression/progression_data_utils.gd")

const TEST_WORLD_CONFIG := "res://data/configs/world_map/test_world_map_config.tres"

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_non_main_character_battle_death_persists_as_real_death()
	_test_main_character_battle_death_triggers_game_over()

	if _failures.is_empty():
		print("Battle permadeath regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Battle permadeath regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_non_main_character_battle_death_persists_as_real_death() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return
	var facade: GameRuntimeFacade = null
	var reloaded_session: GameSession = null

	var party_state = game_session.get_party_state()
	var ally_state := _build_party_member(&"ally_guard_01", "护卫")
	party_state.set_member_state(ally_state)
	party_state.active_member_ids = ProgressionDataUtils.to_string_name_array([&"player_sword_01", &"ally_guard_01"])
	party_state.reserve_member_ids = []
	party_state.main_character_member_id = &"player_sword_01"
	var persist_error := int(game_session.set_party_state(party_state))
	_assert_eq(persist_error, OK, "补充测试队友后应能持久化队伍状态。")
	if persist_error != OK:
		_cleanup_session(game_session)
		return

	facade = GameRuntimeFacade.new()
	facade.setup(game_session)
	_prepare_battle_resolution_context(
		facade,
		[
			_build_ally_unit(&"hero_unit", &"player_sword_01", true, 18),
			_build_ally_unit(&"ally_unit", &"ally_guard_01", false, 0),
		]
	)
	facade.finalize_battle_resolution(_build_resolution_result(&"player"))

	var updated_party = facade.get_party_state()
	var persisted_ally_state = updated_party.get_member_state(&"ally_guard_01")
	_assert_true(persisted_ally_state != null, "战后队友状态仍应保留在 PartyState.member_states 中。")
	_assert_true(persisted_ally_state != null and bool(persisted_ally_state.is_dead), "非主角在战斗中死亡后应被标记为真实死亡。")
	_assert_eq(int(persisted_ally_state.current_hp) if persisted_ally_state != null else -1, 0, "真实死亡成员的 HP 应写回 0。")
	_assert_true(not updated_party.active_member_ids.has(&"ally_guard_01"), "真实死亡成员不应继续留在 active roster。")
	_assert_true(not updated_party.reserve_member_ids.has(&"ally_guard_01"), "真实死亡成员不应继续留在 reserve roster。")
	_assert_eq(String(updated_party.main_character_member_id), "player_sword_01", "主角标识不应因队友死亡而漂移。")
	_assert_true(facade.get_active_modal_id() != "game_over", "只有主角死亡时才应进入 GameOver。")

	reloaded_session = GameSession.new()
	var load_error := int(reloaded_session.load_save(game_session.get_active_save_id()))
	_assert_eq(load_error, OK, "真实死亡结果应能通过存档重新加载。")
	if load_error == OK:
		var reloaded_party = reloaded_session.get_party_state()
		var reloaded_ally_state = reloaded_party.get_member_state(&"ally_guard_01")
		_assert_true(reloaded_ally_state != null and bool(reloaded_ally_state.is_dead), "重新加载存档后，队友死亡标记应保持稳定。")
		_assert_true(not reloaded_party.active_member_ids.has(&"ally_guard_01"), "重新加载存档后，死亡队友不应被归一化回 active roster。")
		_assert_true(not reloaded_party.reserve_member_ids.has(&"ally_guard_01"), "重新加载存档后，死亡队友不应被归一化回 reserve roster。")

	if facade != null:
		facade.dispose()
	_cleanup_session(reloaded_session)
	_cleanup_session(game_session)


func _test_main_character_battle_death_triggers_game_over() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return
	var facade := GameRuntimeFacade.new()
	facade.setup(game_session)
	var persisted_player_coord: Vector2i = game_session.get_player_coord()
	game_session.set_battle_save_lock(true)
	var staged_coord: Vector2i = persisted_player_coord + Vector2i(3, 0)
	var staged_coord_error := int(game_session.set_player_coord(staged_coord))
	_assert_eq(staged_coord_error, OK, "战斗锁开启时仍应允许暂存待刷新坐标。")
	_assert_true(game_session.has_pending_save(), "进入战斗后写入的位置变更应先积累为 pending save。")
	_prepare_battle_resolution_context(
		facade,
		[
			_build_ally_unit(&"hero_unit", &"player_sword_01", false, 0),
		]
	)
	facade.finalize_battle_resolution(_build_resolution_result(&"hostile"))

	var party_state = facade.get_party_state()
	var protagonist_state = party_state.get_member_state(&"player_sword_01")
	_assert_true(protagonist_state != null and bool(protagonist_state.is_dead), "主角在战斗中死亡后应被正式标记为真实死亡。")
	_assert_true(party_state.active_member_ids.is_empty(), "主角死亡后，active roster 应为空。")
	_assert_eq(facade.get_active_modal_id(), "game_over", "主角死亡后运行时应直接切到 GameOver modal。")
	_assert_true(bool(facade.get_game_over_context().get("main_character_dead", false)), "GameOver 上下文应标记主角死亡。")
	_assert_true(not String(facade.get_status_text()).is_empty(), "GameOver 后应写入稳定状态文本。")
	_assert_true(not game_session.has_pending_save(), "GameOver 分支不应继续保留待刷新的 battle save。")
	_assert_true(not game_session.is_battle_save_locked(), "GameOver 结束后应解除 battle save lock。")
	var persisted_save_id: String = game_session.get_active_save_id()
	game_session.unload_active_world()
	_assert_true(not game_session.has_active_world(), "主角死亡后返回标题前应清掉 GameSession 当前内存态。")
	_assert_eq(String(game_session.get_active_save_id()), "", "卸载运行时后不应继续保留 active save id。")
	var reload_error := int(game_session.load_save(persisted_save_id))
	_assert_eq(reload_error, OK, "卸载内存态后应仍能从磁盘加载上一份存档。")
	if reload_error == OK:
		_assert_eq(game_session.get_player_coord(), persisted_player_coord, "卸载后重载应回到战斗前最后一次已存档的位置。")
		var reloaded_party_state = game_session.get_party_state()
		var reloaded_main_character = reloaded_party_state.get_member_state(&"player_sword_01")
		_assert_true(reloaded_main_character != null and not bool(reloaded_main_character.is_dead), "卸载后重载不应带回主角死亡状态。")

	if reload_error == OK:
		var reloaded_facade := GameRuntimeFacade.new()
		reloaded_facade.setup(game_session)
		_assert_true(reloaded_facade.get_active_modal_id() != "game_over", "重新加载上一份存档后不应继续停留在 GameOver。")
		reloaded_facade.dispose()

	facade.dispose()
	_cleanup_session(game_session)


func _create_test_session():
	var game_session = GameSession.new()
	game_session.clear_persisted_game()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_eq(create_error, OK, "测试会话应能创建测试世界存档。")
	if create_error != OK:
		_cleanup_session(game_session)
		return null
	return game_session


func _cleanup_session(game_session) -> void:
	if game_session == null:
		return
	game_session.clear_persisted_game()
	game_session.free()


func _prepare_battle_resolution_context(facade: GameRuntimeFacade, ally_units: Array[BattleUnitState]) -> void:
	var battle_state := BattleState.new()
	battle_state.phase = &"battle_ended"
	battle_state.timeline = BattleTimelineState.new()
	for ally_unit in ally_units:
		battle_state.ally_unit_ids.append(ally_unit.unit_id)
		battle_state.units[ally_unit.unit_id] = ally_unit
	facade._battle_runtime._state = battle_state
	facade._battle_state = battle_state
	facade._active_battle_encounter_id = &"test_encounter"
	facade._active_battle_encounter_name = "真实死亡测试"


func _build_resolution_result(winner_faction_id: StringName) -> BattleResolutionResult:
	var result := BattleResolutionResult.new()
	result.battle_id = &"battle_permadeath_test"
	result.winner_faction_id = winner_faction_id
	result.encounter_resolution = &"player_victory" if winner_faction_id == &"player" else &"hostile_victory"
	return result


func _build_ally_unit(unit_id: StringName, member_id: StringName, is_alive: bool, current_hp: int) -> BattleUnitState:
	var unit := BattleUnitState.new()
	unit.unit_id = unit_id
	unit.source_member_id = member_id
	unit.display_name = String(member_id)
	unit.faction_id = &"player"
	unit.control_mode = &"manual"
	unit.is_alive = is_alive
	unit.current_hp = current_hp
	unit.current_mp = 0
	unit.set_equipment_view(EquipmentState.new())
	return unit


func _build_party_member(member_id: StringName, display_name: String) -> PartyMemberState:
	var member_state := PartyMemberState.new()
	member_state.member_id = member_id
	member_state.display_name = display_name
	member_state.current_hp = 22
	member_state.current_mp = 4
	member_state.progression.unit_id = member_id
	member_state.progression.display_name = display_name
	return member_state


func _assert_true(condition: bool, message: String) -> void:
	if condition:
		return
	_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual == expected:
		return
	_test.fail("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
