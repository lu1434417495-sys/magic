# Development script runner for text-runtime scenarios.
# Use it for automation and smoke checks, not as a player startup flow.
extends SceneTree

const GAME_TEXT_COMMAND_RUNNER_SCRIPT = preload("res://scripts/systems/game_text_command_runner.gd")
const DEFAULT_SCENARIO_PATH := "res://tests/text_runtime/scenarios/smoke_startup.txt"


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var runner = GAME_TEXT_COMMAND_RUNNER_SCRIPT.new()
	await runner.initialize()

	var scenario_path := DEFAULT_SCENARIO_PATH
	var user_args := OS.get_cmdline_user_args()
	if not user_args.is_empty():
		scenario_path = String(user_args[0])

	var read_result := _read_scenario_lines(scenario_path)
	if int(read_result.get("error", ERR_CANT_OPEN)) != OK:
		push_error("Failed to read scenario: %s" % scenario_path)
		quit(1)
		return

	var lines: PackedStringArray = read_result.get("lines", PackedStringArray())
	var executed_count := 0
	for line_index in range(lines.size()):
		var line := String(lines[line_index])
		var result = await runner.execute_line(line)
		if result.skipped:
			continue
		executed_count += 1
		print("LINE %d\n%s" % [line_index + 1, result.render()])
		if not result.ok:
			await runner.dispose()
			push_error("Scenario failed at line %d: %s" % [line_index + 1, line])
			quit(1)
			return

	print("Text command script: PASS (%d)" % executed_count)
	await runner.dispose()
	quit(0)


func _read_scenario_lines(scenario_path: String) -> Dictionary:
	var file := FileAccess.open(scenario_path, FileAccess.READ)
	if file == null:
		return {
			"error": FileAccess.get_open_error(),
			"lines": PackedStringArray(),
		}
	var content := file.get_as_text()
	file.close()
	return {
		"error": OK,
		"lines": content.split("\n", false),
	}
