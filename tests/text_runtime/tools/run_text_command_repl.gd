# Development REPL for the headless text command chain.
# It exists for local debugging, not as a shipping game entry.
extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const GAME_TEXT_COMMAND_RUNNER_SCRIPT = preload("res://scripts/systems/game_runtime/headless/game_text_command_runner.gd")


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var runner = GAME_TEXT_COMMAND_RUNNER_SCRIPT.new()
	await runner.initialize()
	print("Headless text REPL ready. Type 'help' for commands, 'exit' to quit.")

	while true:
		var line := OS.read_string_from_stdin()
		if line == "":
			await runner.dispose()
			quit(0)
			return
		var command_text := String(line).strip_edges()
		if command_text == "exit" or command_text == "quit":
			print("Bye.")
			await runner.dispose()
			quit(0)
			return
		var result = await runner.execute_line(command_text)
		if result.skipped:
			continue
		print(result.render())
