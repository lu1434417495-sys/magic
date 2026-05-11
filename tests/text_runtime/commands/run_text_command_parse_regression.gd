extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const GAME_TEXT_COMMAND_RUNNER_SCRIPT = preload("res://scripts/systems/game_runtime/headless/game_text_command_runner.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var runner = GAME_TEXT_COMMAND_RUNNER_SCRIPT.new()
	await runner.initialize()

	await _run_command(runner, "game new test")
	await _assert_invalid_scalar_inputs_fail_without_state_drift(runner)

	await runner.dispose(true)
	_finish()


func _assert_invalid_scalar_inputs_fail_without_state_drift(runner) -> void:
	var before_snapshot: Dictionary = runner.get_session().build_snapshot()
	var before_coord: Dictionary = before_snapshot.get("world", {}).get("player_coord", {})

	var bad_move = await _run_command_expect_fail(runner, "world move right nope")
	_assert_true(String(bad_move.message).contains("移动次数"), "非法 world move count 应返回明确整数校验错误。")
	_assert_eq(bad_move.snapshot.get("world", {}).get("player_coord", {}), before_coord, "非法 world move count 不应漂移玩家坐标。")

	var bad_select = await _run_command_expect_fail(runner, "world select left 3")
	_assert_true(String(bad_select.message).contains("世界坐标 X"), "非法 world 坐标应返回明确坐标校验错误。")

	var bad_tick = await _run_command_expect_fail(runner, "battle tick nope")
	_assert_true(String(bad_tick.message).contains("战斗推进 tick"), "非法 battle tick 秒数应返回明确数值校验错误。")

	var bad_capacity = await _run_command_expect_fail(runner, "warehouse capacity nope")
	_assert_true(String(bad_capacity.message).contains("仓库容量"), "非法 warehouse capacity 应返回明确整数校验错误。")


func _run_command(runner, command_text: String) -> void:
	var result = await runner.execute_line(command_text)
	if result.skipped:
		return
	print(result.render())
	_assert_true(result.ok, "命令失败：%s | %s" % [command_text, result.message])


func _run_command_expect_fail(runner, command_text: String):
	var result = await runner.execute_line(command_text)
	if result.skipped:
		return result
	print(result.render())
	_assert_true(not result.ok, "命令本应失败：%s" % command_text)
	return result


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])


func _finish() -> void:
	if _failures.is_empty():
		print("Text command parse regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Text command parse regression: FAIL (%d)" % _failures.size())
	quit(1)
