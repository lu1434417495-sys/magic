## Lightweight tracer for AI hot-path profiling.
##
## Design:
##  - Default state: `instance == null`, so `enter()` / `exit()` reduce to a single
##    static-variable read plus a null check (~50 ns). Production code can safely
##    call them on the hot path with negligible overhead.
##  - Activated state: a benchmark script creates an instance and assigns it to
##    `AiTraceRecorder.instance`. Every `enter()` / `exit()` then records a Chrome
##    Tracing event AND aggregates per-function stats with self/total split.
##  - Output:
##      * Chrome Tracing JSON (open in chrome://tracing or ui.perfetto.dev)
##      * `func_stats` dict (cProfile-style: ncalls / self_usec / total_usec / max_usec)
##
## Pairing rule: every `enter(name)` must have a matching `exit(name)`, including
## along early-return paths. If you add a `return` before `exit()`, the call stack
## desynchronises and self/total accounting drifts for every later frame.
extends RefCounted
class_name AiTraceRecorder

static var instance: AiTraceRecorder = null

const _EVENT_BEGIN := "B"
const _EVENT_END := "E"

var _events: Array = []
var _func_stats: Dictionary = {}
var _call_stack: Array = []
var _start_ts_usec: int = 0
var _pid: int = 1
var _tid: int = 1
var _max_events: int = 200000  # Soft cap; warn but keep aggregating stats.
var _truncated: bool = false
var _collect_events := true


static func enter(name: StringName) -> void:
	var i := instance
	if i == null:
		return
	i._enter_impl(name)


static func exit(name: StringName) -> void:
	var i := instance
	if i == null:
		return
	i._exit_impl(name)


func _init() -> void:
	_start_ts_usec = Time.get_ticks_usec()


func set_event_capture_enabled(enabled: bool) -> void:
	_collect_events = enabled
	if not _collect_events:
		_events.clear()
		_truncated = false


func _enter_impl(name: StringName) -> void:
	var ts := Time.get_ticks_usec()
	if _collect_events:
		if _events.size() < _max_events:
			_events.append({"name": String(name), "cat": "ai", "ph": _EVENT_BEGIN, "ts": ts - _start_ts_usec, "pid": _pid, "tid": _tid})
		else:
			_truncated = true
	_call_stack.append({"name": name, "t_enter": ts, "child_usec": 0})


func _exit_impl(name: StringName) -> void:
	var ts := Time.get_ticks_usec()
	if _call_stack.is_empty():
		push_warning("AiTraceRecorder.exit(%s) called with empty stack" % String(name))
		return
	var frame: Dictionary = _call_stack.pop_back()
	if frame.get("name", &"") != name:
		push_warning("AiTraceRecorder.exit(%s) mismatched stack top=%s" % [String(name), String(frame.get("name", &""))])
		# Best-effort: try to flush frame anyway.
	if _collect_events:
		if _events.size() < _max_events:
			_events.append({"name": String(name), "cat": "ai", "ph": _EVENT_END, "ts": ts - _start_ts_usec, "pid": _pid, "tid": _tid})
		else:
			_truncated = true

	var own_usec := ts - int(frame["t_enter"])
	var child_usec := int(frame["child_usec"])
	var self_usec := own_usec - child_usec
	if self_usec < 0:
		self_usec = 0

	var stats: Dictionary
	if _func_stats.has(name):
		stats = _func_stats[name]
	else:
		stats = {"ncalls": 0, "self_usec": 0, "total_usec": 0, "max_usec": 0}
	stats["ncalls"] = int(stats["ncalls"]) + 1
	stats["self_usec"] = int(stats["self_usec"]) + self_usec
	stats["total_usec"] = int(stats["total_usec"]) + own_usec
	if own_usec > int(stats["max_usec"]):
		stats["max_usec"] = own_usec
	_func_stats[name] = stats

	if not _call_stack.is_empty():
		var parent: Dictionary = _call_stack[-1]
		parent["child_usec"] = int(parent["child_usec"]) + own_usec
		_call_stack[-1] = parent


func get_func_stats() -> Dictionary:
	return _func_stats


func get_events() -> Array:
	return _events


func is_truncated() -> bool:
	return _truncated


func dump_trace_json(path: String, metadata: Dictionary = {}) -> bool:
	var doc := {
		"traceEvents": _events,
		"displayTimeUnit": "us",
		"metadata": metadata,
	}
	var dir_path := path.get_base_dir()
	if not DirAccess.dir_exists_absolute(dir_path):
		DirAccess.make_dir_recursive_absolute(dir_path)
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		return false
	file.store_string(JSON.stringify(doc))
	file.close()
	return true


func assert_balanced() -> bool:
	# Returns true when every B has a matching E.
	if not _call_stack.is_empty():
		return false
	var begins := 0
	var ends := 0
	for ev in _events:
		var ph: String = ev.get("ph", "")
		if ph == _EVENT_BEGIN:
			begins += 1
		elif ph == _EVENT_END:
			ends += 1
	return begins == ends
