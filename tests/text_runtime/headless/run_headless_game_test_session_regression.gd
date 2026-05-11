extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const HEADLESS_GAME_TEST_SESSION_SCRIPT = preload("res://scripts/systems/game_runtime/headless/headless_game_test_session.gd")
const GAME_SESSION_SCRIPT = preload("res://scripts/systems/persistence/game_session.gd")
const ENCOUNTER_ANCHOR_DATA_SCRIPT = preload("res://scripts/systems/world/encounter_anchor_data.gd")

const SAVE_INDEX_PATH := "user://saves/index.dat"

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await _test_dispose_clears_battle_save_lock_on_shared_game_session()
	await _test_build_snapshot_does_not_rebuild_missing_save_index()

	if _failures.is_empty():
		print("Headless game test session regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Headless game test session regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_dispose_clears_battle_save_lock_on_shared_game_session() -> void:
	var shared_game_session = root.get_node_or_null("GameSession")
	_assert_true(shared_game_session != null, "Headless session 回归前置：SceneTree 应提供共享 GameSession。")
	if shared_game_session == null:
		return
	shared_game_session.clear_persisted_game()
	await process_frame

	var session = HEADLESS_GAME_TEST_SESSION_SCRIPT.new()
	await session.initialize()

	var create_result: Dictionary = await session.create_new_game(&"test")
	_assert_true(bool(create_result.get("ok", false)), "Headless session 应能在共享 GameSession 上创建测试世界。")
	if not bool(create_result.get("ok", false)):
		await _cleanup_shared_game_session(shared_game_session)
		return

	var battle_result: Dictionary = await session.start_battle_by_kind(ENCOUNTER_ANCHOR_DATA_SCRIPT.ENCOUNTER_KIND_SINGLE)
	_assert_true(bool(battle_result.get("ok", false)), "Headless session 应能启动单体遭遇战。")
	if not bool(battle_result.get("ok", false)):
		await session.dispose(true)
		await _cleanup_shared_game_session(shared_game_session)
		return

	_assert_true(shared_game_session.is_battle_save_locked(), "进入 headless 战斗后 GameSession 应持有 battle save lock。")

	await session.dispose(true)

	_assert_true(not shared_game_session.is_battle_save_locked(), "Headless session dispose 后应清掉 GameSession 的 battle save lock。")
	_assert_true(session.get_runtime_facade() == null, "Headless session dispose 后不应继续保留 runtime facade。")

	await _cleanup_shared_game_session(shared_game_session)


func _test_build_snapshot_does_not_rebuild_missing_save_index() -> void:
	var shared_game_session = root.get_node_or_null("GameSession")
	_assert_true(shared_game_session != null, "Headless snapshot 只读回归前置：SceneTree 应提供共享 GameSession。")
	if shared_game_session == null:
		return
	shared_game_session.clear_persisted_game()
	await process_frame

	var session = HEADLESS_GAME_TEST_SESSION_SCRIPT.new()
	await session.initialize()
	var create_result: Dictionary = await session.create_new_game(&"test")
	_assert_true(bool(create_result.get("ok", false)), "Headless snapshot 只读回归前置：应能创建测试世界。")
	if not bool(create_result.get("ok", false)):
		await session.dispose(true)
		await _cleanup_shared_game_session(shared_game_session)
		return

	_remove_user_file_if_exists(SAVE_INDEX_PATH)
	_assert_true(not FileAccess.file_exists(ProjectSettings.globalize_path(SAVE_INDEX_PATH)), "Headless snapshot 只读回归前置：index.dat 应已被删除。")
	var snapshot := session.build_snapshot()
	_assert_true(snapshot.get("session", {}) is Dictionary, "Headless snapshot 应仍返回 session 字段。")
	_assert_true(
		not FileAccess.file_exists(ProjectSettings.globalize_path(SAVE_INDEX_PATH)),
		"build_snapshot() 不应通过 list_save_slots() 或恢复流程重建 index.dat。"
	)

	await session.dispose(true)
	await _cleanup_shared_game_session(shared_game_session)


func _cleanup_shared_game_session(shared_game_session) -> void:
	if shared_game_session == null or not is_instance_valid(shared_game_session):
		return
	shared_game_session.clear_persisted_game()
	await process_frame


func _remove_user_file_if_exists(path: String) -> void:
	var absolute_path := ProjectSettings.globalize_path(path)
	if FileAccess.file_exists(absolute_path):
		DirAccess.remove_absolute(absolute_path)


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)
