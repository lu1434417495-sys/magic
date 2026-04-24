class_name RuntimeLogDock
extends PanelContainer

const BattleState = preload("res://scripts/systems/battle_state.gd")

const PANEL_BG := Color(0.16, 0.06, 0.03, 0.9)
const PANEL_EDGE := Color(0.82, 0.63, 0.35, 0.96)
const TEXT_PRIMARY := Color(0.98, 0.93, 0.82, 1.0)
const TEXT_SECONDARY := Color(0.92, 0.82, 0.66, 0.94)
const TEXT_MUTED := Color(0.78, 0.66, 0.54, 0.86)
const LOG_SCROLL_FOLLOW_THRESHOLD := 18.0
const WORLD_LOG_TITLE := "运行日志"
const BATTLE_LOG_TITLE := "战斗日志"
const WORLD_LOG_EMPTY_TEXT := "等待世界运行日志。"
const BATTLE_LOG_EMPTY_TEXT := "等待战斗开始后显示完整战斗日志。"

@onready var title_label: Label = %TitleLabel
@onready var meta_label: Label = %MetaLabel
@onready var log_output: RichTextLabel = %LogOutput

var _feed_source_id := ""
var _feed_entry_count := 0
var _feed_last_entry_key := ""


func _ready() -> void:
	_apply_static_skin()
	show_world_logs({}, "", "")


func show_world_logs(log_snapshot: Dictionary, active_map_display_name: String = "", status_text: String = "") -> void:
	var display_entries := _build_runtime_log_entries(log_snapshot.get("entries", []))
	var virtual_path := String(log_snapshot.get("virtual_path", ""))
	var source_id := "runtime:%s" % virtual_path
	var scope_text := active_map_display_name if not active_map_display_name.is_empty() else "世界地图"
	var entry_count := int(log_snapshot.get("entry_count", display_entries.size()))
	var buffer_limit := maxi(int(log_snapshot.get("buffer_limit", display_entries.size())), 1)
	var meta_text := "%s  ·  最近 %d/%d 条" % [
		scope_text,
		entry_count,
		buffer_limit,
	]
	_sync_entries(source_id, WORLD_LOG_TITLE, meta_text, WORLD_LOG_EMPTY_TEXT, display_entries)
	meta_label.tooltip_text = _build_runtime_log_meta_tooltip(
		scope_text,
		status_text,
		virtual_path,
		entry_count,
		buffer_limit
	)


func show_battle_logs(battle_state: BattleState) -> void:
	if battle_state == null:
		_sync_entries("", BATTLE_LOG_TITLE, _build_default_battle_meta_text(), BATTLE_LOG_EMPTY_TEXT, [])
		meta_label.tooltip_text = ""
		return
	var display_entries := _build_battle_log_entries(battle_state.log_entries)
	var source_id := "battle:%s" % String(battle_state.battle_id)
	var meta_text := "当前 %s  ·  上限 %d 条 / %d MiB" % [
		battle_state.get_log_budget_summary_text(),
		BattleState.LOG_ENTRY_LIMIT,
		int(BattleState.LOG_TEXT_BYTE_LIMIT / (1024 * 1024)),
	]
	_sync_entries(source_id, BATTLE_LOG_TITLE, meta_text, BATTLE_LOG_EMPTY_TEXT, display_entries)
	meta_label.tooltip_text = "battle_id=%s\nphase=%s\nlog_entries=%d\ntext_budget=%d bytes" % [
		String(battle_state.battle_id),
		String(battle_state.phase),
		battle_state.log_entries.size(),
		battle_state.get_log_text_byte_size(),
	]


func clear_logs() -> void:
	_sync_entries("", WORLD_LOG_TITLE, "等待运行时。", WORLD_LOG_EMPTY_TEXT, [])
	meta_label.tooltip_text = ""


func _build_runtime_log_entries(entries_variant) -> Array[Dictionary]:
	var display_entries: Array[Dictionary] = []
	if entries_variant is not Array:
		return display_entries
	for entry_variant in entries_variant:
		if entry_variant is not Dictionary:
			continue
		var entry := entry_variant as Dictionary
		var message := String(entry.get("message", "")).strip_edges()
		if message.is_empty():
			continue
		var seq := int(entry.get("seq", 0))
		var event_id := String(entry.get("event_id", ""))
		var domain := String(entry.get("domain", "runtime")).to_upper()
		var level := String(entry.get("level", "info")).to_upper()
		var time_text := _shorten_time_text(String(entry.get("time_text", "")))
		display_entries.append({
			"key": "%d:%s:%s" % [seq, event_id, message],
			"text": "[%s][%s/%s] %s" % [time_text, domain, level, message],
		})
	return display_entries


func _build_battle_log_entries(log_entries: Array[String]) -> Array[Dictionary]:
	var display_entries: Array[Dictionary] = []
	for index in range(log_entries.size()):
		var message := String(log_entries[index]).strip_edges()
		if message.is_empty():
			continue
		display_entries.append({
			"key": "%d:%s" % [index, message],
			"text": message,
		})
	return display_entries


func _sync_entries(
	source_id: String,
	title_text: String,
	meta_text: String,
	empty_text: String,
	display_entries: Array[Dictionary]
) -> void:
	title_label.text = title_text
	meta_label.text = meta_text
	var should_follow_tail := _should_follow_tail()
	var entry_count := display_entries.size()
	var last_entry_key := _get_last_entry_key(display_entries)
	if source_id != _feed_source_id:
		_reset_feed(source_id)
		_rebuild_feed(display_entries, empty_text)
	elif entry_count < _feed_entry_count:
		_rebuild_feed(display_entries, empty_text)
	elif entry_count == _feed_entry_count and last_entry_key != _feed_last_entry_key:
		_rebuild_feed(display_entries, empty_text)
	elif entry_count > _feed_entry_count:
		for index in range(_feed_entry_count, entry_count):
			var entry := display_entries[index]
			_append_line(String(entry.get("text", "")))
		_feed_entry_count = entry_count
		_feed_last_entry_key = last_entry_key
	if should_follow_tail and (source_id != _feed_source_id or entry_count > 0):
		call_deferred("_scroll_to_bottom")


func _reset_feed(source_id: String = "") -> void:
	_feed_source_id = source_id
	_feed_entry_count = 0
	_feed_last_entry_key = ""
	if log_output != null:
		log_output.clear()


func _rebuild_feed(display_entries: Array[Dictionary], empty_text: String) -> void:
	if log_output == null:
		return
	log_output.clear()
	if display_entries.is_empty():
		if not empty_text.is_empty():
			log_output.add_text(empty_text)
		_feed_entry_count = 0
		_feed_last_entry_key = ""
		return
	for entry in display_entries:
		_append_line(String(entry.get("text", "")))
	_feed_entry_count = display_entries.size()
	_feed_last_entry_key = _get_last_entry_key(display_entries)


func _append_line(text: String) -> void:
	if log_output == null or text.is_empty():
		return
	if log_output.get_parsed_text().is_empty():
		log_output.add_text(text)
		return
	log_output.newline()
	log_output.add_text(text)


func _get_last_entry_key(display_entries: Array[Dictionary]) -> String:
	if display_entries.is_empty():
		return ""
	return String(display_entries[-1].get("key", ""))


func _shorten_time_text(time_text: String) -> String:
	if time_text.length() >= 19:
		return time_text.substr(11, time_text.length() - 11)
	return time_text if not time_text.is_empty() else "--:--:--"


func _build_runtime_log_meta_tooltip(
	scope_text: String,
	status_text: String,
	virtual_path: String,
	entry_count: int,
	buffer_limit: int
) -> String:
	var lines := PackedStringArray([
		"scope=%s" % scope_text,
		"entries=%d/%d" % [entry_count, buffer_limit],
	])
	if not status_text.is_empty():
		lines.append("status=%s" % status_text)
	if not virtual_path.is_empty():
		lines.append("log=%s" % virtual_path)
	return "\n".join(lines)


func _build_default_battle_meta_text() -> String:
	return "上限 %d 条 / %d MiB" % [
		BattleState.LOG_ENTRY_LIMIT,
		int(BattleState.LOG_TEXT_BYTE_LIMIT / (1024 * 1024)),
	]


func _should_follow_tail() -> bool:
	if log_output == null:
		return false
	var scroll_bar := log_output.get_v_scroll_bar()
	if scroll_bar == null:
		return true
	return scroll_bar.max_value - scroll_bar.value <= LOG_SCROLL_FOLLOW_THRESHOLD


func _scroll_to_bottom() -> void:
	if log_output == null:
		return
	log_output.scroll_to_line(maxi(log_output.get_line_count() - 1, 0))


func _apply_static_skin() -> void:
	add_theme_stylebox_override("panel", _build_panel_style(PANEL_BG, PANEL_EDGE))
	title_label.add_theme_font_size_override("font_size", 18)
	title_label.add_theme_color_override("font_color", TEXT_PRIMARY)
	meta_label.add_theme_font_size_override("font_size", 11)
	meta_label.add_theme_color_override("font_color", TEXT_MUTED)
	log_output.fit_content = false
	log_output.add_theme_font_size_override("normal_font_size", 12)
	log_output.add_theme_color_override("default_color", TEXT_PRIMARY)
	log_output.add_theme_constant_override("line_separation", 2)
	log_output.scroll_active = true


func _build_panel_style(
	background_color: Color,
	border_color: Color,
	radius: int = 20,
	border_width: int = 2,
	shadow_color: Color = Color(0.0, 0.0, 0.0, 0.34)
) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = background_color
	style.border_width_left = border_width
	style.border_width_top = border_width
	style.border_width_right = border_width
	style.border_width_bottom = border_width
	style.border_color = border_color
	style.corner_radius_top_left = radius
	style.corner_radius_top_right = radius
	style.corner_radius_bottom_right = radius
	style.corner_radius_bottom_left = radius
	style.shadow_color = shadow_color
	style.shadow_size = 10
	return style
