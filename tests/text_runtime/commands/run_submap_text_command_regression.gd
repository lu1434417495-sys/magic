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

	await _run_command(runner, "game new ashen_intersection")
	await _run_command(runner, "world move right 3")
	await _expect_command(runner, "expect window == submap_confirm")
	await _expect_command(runner, "expect field submap.confirm_visible == true")
	await _run_command(runner, "submap confirm")
	await _expect_command(runner, "expect field world.is_submap == true")
	await _expect_command(runner, "expect field world.map_id == ashen_ashlands")
	await _run_command(runner, "submap return")
	await _expect_command(runner, "expect field world.is_submap == false")
	await _expect_command(runner, "expect field world.player_coord.x == 52")
	await _expect_command(runner, "expect field world.player_coord.y == 49")

	await runner.dispose(true)
	_finish()


func _run_command(runner, command_text: String) -> void:
	var result = await runner.execute_line(command_text)
	if result.skipped:
		return
	print(result.render())
	_assert_true(result.ok, "命令失败：%s | %s" % [command_text, result.message])


func _expect_command(runner, command_text: String) -> void:
	var result = await runner.execute_line(command_text)
	print(result.render())
	_assert_true(result.ok, "断言失败：%s | %s" % [command_text, result.message])


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)


func _finish() -> void:
	if _failures.is_empty():
		print("Submap text command regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Submap text command regression: FAIL (%d)" % _failures.size())
	quit(1)
