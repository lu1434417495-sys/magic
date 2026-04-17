extends SceneTree

const GAME_TEXT_COMMAND_RUNNER_SCRIPT = preload("res://scripts/systems/game_text_command_runner.gd")
const ITEM_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/player/warehouse/item_content_registry.gd")

const INVALID_ITEM_DIRECTORY := "res://tests/fixtures/resource_validation/item_registry_invalid"

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var runner = GAME_TEXT_COMMAND_RUNNER_SCRIPT.new()
	await runner.initialize()

	await _assert_official_validation_surface(runner)
	await _assert_invalid_item_validation_surface(runner)

	await runner.dispose(true)
	_finish()


func _assert_official_validation_surface(runner) -> void:
	var snapshot_result = await _run_command(runner, "snapshot")
	var validation_snapshot: Dictionary = snapshot_result.snapshot.get("validation", {})
	var domains: Dictionary = validation_snapshot.get("domains", {})
	var item_domain: Dictionary = domains.get("item", {})

	_assert_true(bool(validation_snapshot.get("ok", false)), "正式 headless validation 快照应标记为 ok。")
	_assert_eq(int(validation_snapshot.get("error_count", -1)), 0, "正式 headless validation 快照不应有错误。")
	_assert_eq(int(item_domain.get("error_count", -1)), 0, "正式 item validation domain 不应有错误。")
	_assert_true(snapshot_result.snapshot_text.contains("[VALIDATION]"), "headless 文本快照应包含 VALIDATION 分段。")
	_assert_true(snapshot_result.snapshot_text.contains("domain=progression | errors=0"), "文本快照应稳定渲染 progression validation 摘要。")
	_assert_true(snapshot_result.snapshot_text.contains("domain=item | errors=0"), "文本快照应稳定渲染 item validation 摘要。")
	_assert_true(_find_log_entry(snapshot_result.snapshot, "session.content.item_validation_failed").is_empty(), "正式内容不应依赖 item validation 错误日志。")

	await _run_command(runner, "expect field validation.ok == true")
	await _run_command(runner, "expect field validation.error_count == 0")


func _assert_invalid_item_validation_surface(runner) -> void:
	var game_session = runner.get_session().get_game_session()
	_assert_true(game_session != null, "validation surface 回归前置：GameSession 应可访问。")
	if game_session == null:
		return

	var invalid_item_registry = ITEM_CONTENT_REGISTRY_SCRIPT.new()
	invalid_item_registry._item_defs.clear()
	invalid_item_registry._validation_errors.clear()
	invalid_item_registry._scan_directory(INVALID_ITEM_DIRECTORY)
	game_session._item_content_registry = invalid_item_registry
	game_session.refresh_content_validation_snapshot()

	var snapshot_result = await _run_command(runner, "snapshot")
	var validation_snapshot: Dictionary = snapshot_result.snapshot.get("validation", {})
	var domains: Dictionary = validation_snapshot.get("domains", {})
	var item_domain: Dictionary = domains.get("item", {})
	var item_errors: Array = item_domain.get("errors", [])

	_assert_true(not bool(validation_snapshot.get("ok", true)), "非法 item registry 应让 headless validation 快照标记失败。")
	_assert_eq(int(item_domain.get("error_count", 0)), 3, "非法 item registry 应稳定暴露 3 条 item 校验错误。")
	_assert_error_contains(item_errors, "is missing item_id", "headless validation 快照应暴露缺失 item_id。")
	_assert_error_contains(item_errors, "Duplicate item_id registered: duplicate_item", "headless validation 快照应暴露重复 item_id。")
	_assert_error_contains(item_errors, "declares invalid slot phantom_slot", "headless validation 快照应暴露非法槽位引用。")
	_assert_true(snapshot_result.snapshot_text.contains("domain=item | errors=3"), "headless 文本快照应稳定渲染 item validation 错误计数。")
	_assert_true(snapshot_result.snapshot_text.contains("is missing item_id"), "headless 文本快照应渲染缺失 item_id 错误。")
	_assert_true(snapshot_result.snapshot_text.contains("Duplicate item_id registered: duplicate_item"), "headless 文本快照应渲染重复 item_id 错误。")
	_assert_true(snapshot_result.snapshot_text.contains("declares invalid slot phantom_slot"), "headless 文本快照应渲染非法槽位错误。")
	_assert_true(_find_log_entry(snapshot_result.snapshot, "session.content.item_validation_failed").is_empty(), "validation surface 回归不应依赖额外日志注入来暴露 item 错误。")

	await _run_command(runner, "expect field validation.ok == false")
	await _run_command(runner, "expect field validation.domains.item.error_count == 3")


func _find_log_entry(snapshot: Dictionary, event_id: String) -> Dictionary:
	var entries: Array = snapshot.get("logs", {}).get("entries", [])
	for entry_variant in entries:
		if entry_variant is not Dictionary:
			continue
		var entry: Dictionary = entry_variant
		if String(entry.get("event_id", "")) == event_id:
			return entry
	return {}


func _assert_error_contains(errors: Array, fragment: String, message: String) -> void:
	for error_variant in errors:
		if String(error_variant).contains(fragment):
			return
	_failures.append(message)


func _run_command(runner, command_text: String):
	var result = await runner.execute_line(command_text)
	if result.skipped:
		return result
	print(result.render())
	_assert_true(result.ok, "命令失败：%s | %s" % [command_text, result.message])
	return result


func _assert_true(condition: bool, message: String) -> void:
	if condition:
		return
	_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual == expected:
		return
	_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])


func _finish() -> void:
	if _failures.is_empty():
		print("Validation text surface regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Validation text surface regression: FAIL (%d)" % _failures.size())
	quit(1)
