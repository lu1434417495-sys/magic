class_name GameLogService
extends RefCounted

signal entry_added(entry: Dictionary)

const LOG_DIRECTORY := "user://logs"
const DEFAULT_BUFFER_LIMIT := 400
const DEFAULT_TAIL_LIMIT := 50

var _entries: Array[Dictionary] = []
var _max_entries := DEFAULT_BUFFER_LIMIT
var _next_seq := 1
var _session_log_virtual_path := ""
var _write_enabled := true


func _init(max_entries: int = DEFAULT_BUFFER_LIMIT) -> void:
	_max_entries = maxi(max_entries, 1)
	_session_log_virtual_path = _build_session_log_virtual_path()
	_initialize_log_file()


func append_entry(
	level: String,
	domain: String,
	event_id: String,
	message: String,
	context: Dictionary = {}
) -> Dictionary:
	var timestamp_ms := int(Time.get_unix_time_from_system() * 1000.0)
	var entry := {
		"seq": _next_seq,
		"time_unix_ms": timestamp_ms,
		"time_text": _format_unix_time_ms(timestamp_ms),
		"level": level if not level.is_empty() else "info",
		"domain": domain if not domain.is_empty() else "runtime",
		"event_id": event_id,
		"message": message,
		"context": _normalize_variant(context),
	}
	_next_seq += 1
	_entries.append(entry)
	if _entries.size() > _max_entries:
		_entries.remove_at(0)
	_append_to_file(entry)
	entry_added.emit(entry.duplicate(true))
	return entry.duplicate(true)


func get_recent_entries(limit: int = DEFAULT_TAIL_LIMIT) -> Array[Dictionary]:
	var resolved_limit := maxi(limit, 0)
	var start_index := maxi(_entries.size() - resolved_limit, 0)
	var result: Array[Dictionary] = []
	for index in range(start_index, _entries.size()):
		result.append((_entries[index] as Dictionary).duplicate(true))
	return result


func build_snapshot(limit: int = DEFAULT_TAIL_LIMIT) -> Dictionary:
	return {
		"file_path": get_log_path(),
		"virtual_path": _session_log_virtual_path,
		"entry_count": _entries.size(),
		"buffer_limit": _max_entries,
		"entries": get_recent_entries(limit),
	}


func start_new_session() -> void:
	clear_entries()
	_next_seq = 1
	_write_enabled = true
	_session_log_virtual_path = _build_session_log_virtual_path()
	_initialize_log_file()


func clear_entries() -> void:
	_entries.clear()


func get_log_path() -> String:
	if _session_log_virtual_path.is_empty():
		return ""
	return ProjectSettings.globalize_path(_session_log_virtual_path)


func get_virtual_log_path() -> String:
	return _session_log_virtual_path


func _build_session_log_virtual_path() -> String:
	var rng := RandomNumberGenerator.new()
	rng.randomize()
	var timestamp_ms := int(Time.get_unix_time_from_system() * 1000.0)
	return "%s/session_%d_%06d.jsonl" % [
		LOG_DIRECTORY,
		timestamp_ms,
		rng.randi_range(0, 999999),
	]


func _initialize_log_file() -> void:
	var ensure_dir_error := DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(LOG_DIRECTORY))
	if ensure_dir_error != OK:
		_disable_file_write("Failed to create log directory %s. Error: %d" % [LOG_DIRECTORY, ensure_dir_error])
		return
	var file := FileAccess.open(_session_log_virtual_path, FileAccess.WRITE)
	if file == null:
		_disable_file_write("Failed to initialize log file %s. Error: %d" % [
			_session_log_virtual_path,
			FileAccess.get_open_error(),
		])
		return
	file.close()


func _append_to_file(entry: Dictionary) -> void:
	if not _write_enabled or _session_log_virtual_path.is_empty():
		return
	var file := FileAccess.open(_session_log_virtual_path, FileAccess.READ_WRITE)
	if file == null:
		_disable_file_write("Failed to append log file %s. Error: %d" % [
			_session_log_virtual_path,
			FileAccess.get_open_error(),
		])
		return
	file.seek_end()
	file.store_line(JSON.stringify(entry))
	file.close()


func _disable_file_write(message: String) -> void:
	_write_enabled = false
	push_warning(message)


func _format_unix_time_ms(unix_time_ms: int) -> String:
	if unix_time_ms <= 0:
		return ""
	var unix_time_seconds := int(unix_time_ms / 1000)
	var datetime := Time.get_datetime_dict_from_unix_time(unix_time_seconds)
	return "%04d-%02d-%02d %02d:%02d:%02d.%03d" % [
		int(datetime.get("year", 1970)),
		int(datetime.get("month", 1)),
		int(datetime.get("day", 1)),
		int(datetime.get("hour", 0)),
		int(datetime.get("minute", 0)),
		int(datetime.get("second", 0)),
		posmod(unix_time_ms, 1000),
	]


func _normalize_variant(value):
	match typeof(value):
		TYPE_STRING_NAME:
			return String(value)
		TYPE_VECTOR2I:
			var coord: Vector2i = value
			return {
				"x": coord.x,
				"y": coord.y,
			}
		TYPE_VECTOR2:
			var float_coord: Vector2 = value
			return {
				"x": float_coord.x,
				"y": float_coord.y,
			}
		TYPE_DICTIONARY:
			var normalized_dict: Dictionary = {}
			for key in value.keys():
				normalized_dict[String(key)] = _normalize_variant(value.get(key))
			return normalized_dict
		TYPE_ARRAY:
			var normalized_array: Array = []
			for entry in value:
				normalized_array.append(_normalize_variant(entry))
			return normalized_array
		TYPE_OBJECT:
			if value == null:
				return null
			if value.has_method("to_dict"):
				return _normalize_variant(value.to_dict())
			return str(value)
		_:
			return value
