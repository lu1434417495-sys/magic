extends SceneTree

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/game_session.gd")
const QuestState = preload("res://scripts/player/progression/quest_state.gd")

const TEST_WORLD_CONFIG := "res://data/configs/world_map/test_world_map_config.tres"

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_save_serializer_round_trip_preserves_party_quest_schema()

	if _failures.is_empty():
		print("Save serializer quest round trip regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Save serializer quest round trip regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_save_serializer_round_trip_preserves_party_quest_schema() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_true(create_error == OK, "GameSession 应能基于测试世界配置创建新存档。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return

	var party_state = game_session.get_party_state()
	var quest_state := QuestState.new()
	quest_state.quest_id = &"contract_wolf_pack"
	quest_state.mark_accepted(8)
	quest_state.record_objective_progress(&"defeat_wolves", 2, 3, {"enemy_template_id": "wolf_raider"})
	party_state.set_active_quest_state(quest_state)
	party_state.add_completed_quest_id(&"intro_contract")

	var serializer = game_session._save_serializer
	_assert_true(serializer != null, "GameSession 应暴露已初始化的 SaveSerializer。")
	if serializer == null:
		_cleanup_test_session(game_session)
		return

	var payload: Dictionary = serializer.build_save_payload(
		game_session.get_active_save_id(),
		game_session.get_generation_config_path(),
		game_session.get_active_save_meta(),
		game_session.get_world_data(),
		game_session.get_player_coord(),
		game_session.get_player_faction_id(),
		party_state,
		int(Time.get_unix_time_from_system())
	)
	var decode_result: Dictionary = serializer.decode_v5_payload(
		payload,
		game_session.get_generation_config_path(),
		game_session.get_generation_config(),
		game_session.get_active_save_meta()
	)
	_assert_eq(int(decode_result.get("error", ERR_INVALID_DATA)), OK, "SaveSerializer 应能成功解码带 quest schema 的 payload。")
	if int(decode_result.get("error", ERR_INVALID_DATA)) != OK:
		_cleanup_test_session(game_session)
		return

	var restored_party_state = decode_result.get("party_state")
	_assert_true(restored_party_state != null, "解码后的 payload 应返回 PartyState。")
	_assert_eq(restored_party_state.version, 3, "Quest schema 接入后 PartyState.version 应保持为 3。")
	_assert_true(restored_party_state.has_active_quest(&"contract_wolf_pack"), "SaveSerializer 往返后应保留 active_quests。")
	_assert_true(restored_party_state.has_completed_quest(&"intro_contract"), "SaveSerializer 往返后应保留 completed_quest_ids。")
	var restored_quest: QuestState = restored_party_state.get_active_quest_state(&"contract_wolf_pack")
	_assert_true(restored_quest != null, "SaveSerializer 往返后应恢复 QuestState。")
	if restored_quest != null:
		_assert_eq(restored_quest.get_objective_progress(&"defeat_wolves"), 2, "QuestState 进度应穿过 save payload 保持稳定。")
		_assert_eq(restored_quest.accepted_at_world_step, 8, "QuestState 接取时间应穿过 save payload 保持稳定。")

	_cleanup_test_session(game_session)


func _cleanup_test_session(game_session) -> void:
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
