extends SceneTree

const GAME_LOG_SERVICE_SCRIPT = preload("res://scripts/systems/persistence/game_log_service.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_game_log_service_keeps_ring_buffer_without_default_file_output()
	_test_game_log_service_can_append_opt_in_file()

	if _failures.is_empty():
		print("Game log service regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Game log service regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_game_log_service_keeps_ring_buffer_without_default_file_output() -> void:
	var log_service = GAME_LOG_SERVICE_SCRIPT.new(3)
	var virtual_path := String(log_service.get_virtual_log_path())
	var absolute_path := String(log_service.get_log_path())

	_assert_true(virtual_path.is_empty(), "日志服务默认不应初始化虚拟日志路径。")
	_assert_true(absolute_path.is_empty(), "日志服务默认不应初始化绝对日志路径。")

	log_service.append_entry("info", "session", "session.test.first", "first", {"step": 1})
	log_service.append_entry("info", "world", "world.test.second", "second", {"step": 2})
	log_service.append_entry("warn", "world", "world.test.third", "third", {"step": 3})
	log_service.append_entry("error", "battle", "battle.test.fourth", "fourth", {"step": 4})

	var recent_entries := log_service.get_recent_entries(10)
	_assert_eq(recent_entries.size(), 3, "ring buffer 应只保留最近 3 条内存日志。")
	if recent_entries.size() == 3:
		_assert_eq(int(recent_entries[0].get("seq", 0)), 2, "ring buffer 应丢弃最早一条日志。")
		_assert_eq(String(recent_entries[2].get("message", "")), "fourth", "最后一条内存日志应保留最新消息。")

	var snapshot: Dictionary = log_service.build_snapshot(10)
	_assert_eq(String(snapshot.get("virtual_path", "")), virtual_path, "日志快照应返回当前虚拟路径。")
	_assert_eq(String(snapshot.get("file_path", "")), "", "默认日志快照不应暴露文件路径。")
	_assert_eq(bool(snapshot.get("file_output_enabled", true)), false, "日志文件输出默认应关闭。")
	_assert_eq(bool(snapshot.get("file_write_active", true)), false, "日志文件写入默认不应处于 active 状态。")
	_assert_eq(int(snapshot.get("entry_count", 0)), 3, "日志快照 entry_count 应匹配当前内存缓冲。")


func _test_game_log_service_can_append_opt_in_file() -> void:
	var log_service = GAME_LOG_SERVICE_SCRIPT.new(3, true)
	var virtual_path := String(log_service.get_virtual_log_path())
	var absolute_path := String(log_service.get_log_path())

	_assert_true(not virtual_path.is_empty(), "显式开启文件输出时，日志服务应初始化虚拟日志路径。")
	_assert_true(not absolute_path.is_empty(), "显式开启文件输出时，日志服务应初始化绝对日志路径。")

	log_service.append_entry("info", "session", "session.test.first", "first", {"step": 1})
	log_service.append_entry("info", "world", "world.test.second", "second", {"step": 2})
	log_service.append_entry("warn", "world", "world.test.third", "third", {"step": 3})
	log_service.append_entry("error", "battle", "battle.test.fourth", "fourth", {"step": 4})

	var snapshot: Dictionary = log_service.build_snapshot(10)
	_assert_eq(bool(snapshot.get("file_output_enabled", false)), true, "显式开启时日志快照应标记文件输出启用。")
	_assert_eq(bool(snapshot.get("file_write_active", false)), true, "显式开启时日志文件写入应处于 active 状态。")
	var lines := _read_non_empty_lines(virtual_path)
	_assert_eq(lines.size(), 4, "jsonl 文件应追加所有写入日志，而不仅是 ring buffer。")
	if lines.size() == 4:
		var last_entry_variant = JSON.parse_string(lines[3])
		_assert_true(last_entry_variant is Dictionary, "日志文件中的每一行都应是合法 JSON。")
		if last_entry_variant is Dictionary:
			var last_entry: Dictionary = last_entry_variant
			_assert_eq(String(last_entry.get("event_id", "")), "battle.test.fourth", "日志文件应按顺序追加最新事件。")
			_assert_eq(String(last_entry.get("level", "")), "error", "日志文件应保留日志级别。")
			_assert_true(not String(last_entry.get("time_text", "")).is_empty(), "日志文件应额外保留可读时间文本。")

	_cleanup_log_file(absolute_path)


func _read_non_empty_lines(virtual_path: String) -> Array[String]:
	var result: Array[String] = []
	var file := FileAccess.open(virtual_path, FileAccess.READ)
	if file == null:
		return result
	var contents := file.get_as_text()
	file.close()
	for line in contents.split("\n", false):
		var trimmed := line.strip_edges()
		if trimmed.is_empty():
			continue
		result.append(trimmed)
	return result


func _cleanup_log_file(absolute_path: String) -> void:
	if absolute_path.is_empty():
		return
	if FileAccess.file_exists(absolute_path):
		DirAccess.remove_absolute(absolute_path)


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
