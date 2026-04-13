## 文件说明：该脚本属于存档列表窗口相关的界面窗口脚本，集中维护遮罩、存档列表、空标签等顶层字段。
## 审查重点：重点核对字段含义、节点绑定、信号联动以及界面状态切换是否仍与对应场景保持一致。
## 备注：后续如果调整场景节点命名、层级或交互路径，需要同步检查成员字段与信号连接。

class_name SaveListWindow
extends Control

## 信号说明：当界面请求存档加载时发出的信号，具体处理由外层系统或控制器负责。
signal save_load_requested(save_id: String)
## 信号说明：当窗口或面板关闭时发出的信号，供外层恢复输入焦点、刷新数据或清理临时状态。
signal closed

## 字段说明：缓存遮罩节点，用于压暗背景并阻止底层界面继续接收输入。
@onready var shade: ColorRect = $Shade
## 字段说明：缓存存档列表节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var save_list: ItemList = %SaveList
## 字段说明：缓存空标签节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var empty_label: Label = %EmptyLabel
## 字段说明：缓存详情标签节点，用于展示当前选中对象的关键信息。
@onready var detail_label: Label = %DetailLabel
## 字段说明：缓存加载按钮节点，供用户进入存档加载流程。
@onready var load_button: Button = %LoadButton
## 字段说明：缓存关闭按钮节点，供窗口统一执行收尾和关闭逻辑。
@onready var close_button: Button = %CloseButton
## 字段说明：缓存底部关闭按钮节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var footer_close_button: Button = $CenterContainer/Panel/MarginContainer/Content/Footer/FooterCloseButton

## 字段说明：缓存存档槽位列表字典，集中保存可按键查询的运行时数据。
var _save_slots: Array[Dictionary] = []


func _ready() -> void:
	hide_window()
	shade.gui_input.connect(_on_shade_gui_input)
	save_list.item_selected.connect(_on_save_selected)
	save_list.item_activated.connect(_on_save_activated)
	load_button.pressed.connect(_emit_load_requested)
	close_button.pressed.connect(_close_window)
	footer_close_button.pressed.connect(_close_window)


func _unhandled_input(event: InputEvent) -> void:
	if not visible:
		return
	if event is not InputEventKey:
		return

	var key_event := event as InputEventKey
	if not key_event.pressed or key_event.echo:
		return

	match key_event.keycode:
		KEY_ESCAPE:
			get_viewport().set_input_as_handled()
			_close_window()
		KEY_ENTER, KEY_KP_ENTER:
			get_viewport().set_input_as_handled()
			_emit_load_requested()


func show_window(save_slots: Array[Dictionary]) -> void:
	visible = true
	_save_slots.clear()
	save_list.clear()

	for save_slot in save_slots:
		var normalized_slot := {
			"save_id": String(save_slot.get("save_id", "")),
			"display_name": String(save_slot.get("display_name", "")),
			"world_preset_name": String(save_slot.get("world_preset_name", "世界")),
			"generation_config_path": String(save_slot.get("generation_config_path", "")),
			"world_size_cells": save_slot.get("world_size_cells", Vector2i.ZERO),
			"created_at_unix_time": int(save_slot.get("created_at_unix_time", 0)),
			"updated_at_unix_time": int(save_slot.get("updated_at_unix_time", 0)),
		}
		_save_slots.append(normalized_slot)
		save_list.add_item("%s  |  %s  |  %s" % [
			String(normalized_slot.get("display_name", "")),
			String(normalized_slot.get("world_preset_name", "世界")),
			_format_unix_time(int(normalized_slot.get("updated_at_unix_time", 0))),
		])

	var has_slots := not _save_slots.is_empty()
	empty_label.visible = not has_slots
	load_button.disabled = not has_slots

	if not has_slots:
		detail_label.text = "当前没有可读取的存档。"
		close_button.grab_focus()
		return

	save_list.select(0)
	save_list.ensure_current_is_visible()
	_refresh_detail(0)
	save_list.grab_focus()


func hide_window() -> void:
	visible = false
	_save_slots.clear()
	save_list.clear()
	empty_label.visible = false
	detail_label.text = ""
	load_button.disabled = false


func get_selected_save_id() -> String:
	var selected_items := save_list.get_selected_items()
	if selected_items.is_empty():
		return ""
	var selected_index := selected_items[0]
	if selected_index < 0 or selected_index >= _save_slots.size():
		return ""
	return String(_save_slots[selected_index].get("save_id", ""))


func _on_save_selected(index: int) -> void:
	_refresh_detail(index)


func _on_save_activated(index: int) -> void:
	save_list.select(index)
	_refresh_detail(index)
	_emit_load_requested()


func _refresh_detail(index: int) -> void:
	if index < 0 or index >= _save_slots.size():
		detail_label.text = ""
		return

	var save_slot := _save_slots[index]
	var world_size: Variant = save_slot.get("world_size_cells", Vector2i.ZERO)
	var size_label := "%d x %d" % [world_size.x, world_size.y] if world_size is Vector2i else "未知尺寸"
	detail_label.text = "\n".join(PackedStringArray([
		"存档名：%s" % String(save_slot.get("display_name", "")),
		"世界类型：%s" % String(save_slot.get("world_preset_name", "世界")),
		"地图尺寸：%s" % size_label,
		"创建时间：%s" % _format_unix_time(int(save_slot.get("created_at_unix_time", 0))),
		"最近保存：%s" % _format_unix_time(int(save_slot.get("updated_at_unix_time", 0))),
	]))


func _emit_load_requested() -> void:
	if not visible:
		return
	var save_id := get_selected_save_id()
	if save_id.is_empty():
		return
	hide_window()
	save_load_requested.emit(save_id)


func _close_window() -> void:
	if not visible:
		return
	hide_window()
	closed.emit()


func _on_shade_gui_input(event: InputEvent) -> void:
	if event is not InputEventMouseButton:
		return

	var mouse_event := event as InputEventMouseButton
	if not mouse_event.pressed:
		return
	if mouse_event.button_index != MOUSE_BUTTON_LEFT and mouse_event.button_index != MOUSE_BUTTON_RIGHT:
		return

	_close_window()


func _format_unix_time(unix_time: int) -> String:
	if unix_time <= 0:
		return "未知"
	var datetime := Time.get_datetime_dict_from_unix_time(unix_time)
	return "%04d-%02d-%02d %02d:%02d:%02d" % [
		int(datetime.get("year", 1970)),
		int(datetime.get("month", 1)),
		int(datetime.get("day", 1)),
		int(datetime.get("hour", 0)),
		int(datetime.get("minute", 0)),
		int(datetime.get("second", 0)),
	]
