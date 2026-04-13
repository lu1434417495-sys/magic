## 文件说明：该脚本属于世界预设选择器窗口相关的界面窗口脚本，集中维护遮罩、预设列表、详情标签等顶层字段。
## 审查重点：重点核对字段含义、节点绑定、信号联动以及界面状态切换是否仍与对应场景保持一致。
## 备注：后续如果调整场景节点命名、层级或交互路径，需要同步检查成员字段与信号连接。

class_name WorldPresetPickerWindow
extends Control

## 信号说明：当用户确认预设后发出的信号，供外层继续推进流程。
signal preset_confirmed(preset_id: StringName)
## 信号说明：当用户取消当前流程时发出的信号，供外层恢复默认界面状态或焦点。
signal cancelled

## 字段说明：缓存遮罩节点，用于压暗背景并阻止底层界面继续接收输入。
@onready var shade: ColorRect = $Shade
## 字段说明：缓存预设列表节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var preset_list: ItemList = %PresetList
## 字段说明：缓存详情标签节点，用于展示当前选中对象的关键信息。
@onready var detail_label: Label = %DetailLabel
## 字段说明：缓存确认按钮节点，供用户提交当前选择结果。
@onready var confirm_button: Button = %ConfirmButton
## 字段说明：缓存取消按钮节点，供用户主动中止当前流程。
@onready var cancel_button: Button = %CancelButton
## 字段说明：缓存底部取消按钮节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var footer_cancel_button: Button = $CenterContainer/Panel/MarginContainer/Content/Footer/FooterCancelButton

## 字段说明：缓存预设列表字典，集中保存可按键查询的运行时数据。
var _presets: Array[Dictionary] = []


func _ready() -> void:
	hide_window()
	shade.gui_input.connect(_on_shade_gui_input)
	preset_list.item_selected.connect(_on_preset_selected)
	preset_list.item_activated.connect(_on_preset_activated)
	confirm_button.pressed.connect(_emit_selected_preset)
	cancel_button.pressed.connect(_cancel)
	footer_cancel_button.pressed.connect(_cancel)


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
			_cancel()
		KEY_ENTER, KEY_KP_ENTER:
			get_viewport().set_input_as_handled()
			_emit_selected_preset()


func show_window(presets: Array[Dictionary], default_preset_id: StringName = &"") -> void:
	visible = true
	_presets.clear()
	preset_list.clear()

	for preset_data in presets:
		var normalized_preset := {
			"preset_id": StringName(String(preset_data.get("preset_id", ""))),
			"display_name": String(preset_data.get("display_name", "未命名世界")),
			"size_label": String(preset_data.get("size_label", "")),
			"generation_config_path": String(preset_data.get("generation_config_path", "")),
		}
		_presets.append(normalized_preset)
		preset_list.add_item("%s  |  %s" % [
			String(normalized_preset.get("display_name", "未命名世界")),
			String(normalized_preset.get("size_label", "")),
		])

	var has_presets := not _presets.is_empty()
	confirm_button.disabled = not has_presets
	if not has_presets:
		detail_label.text = "当前没有可用的世界预设。"
		cancel_button.grab_focus()
		return

	var selected_index := 0
	for preset_index in range(_presets.size()):
		if _presets[preset_index].preset_id == default_preset_id:
			selected_index = preset_index
			break

	preset_list.select(selected_index)
	preset_list.ensure_current_is_visible()
	_refresh_detail(selected_index)
	preset_list.grab_focus()


func hide_window() -> void:
	visible = false
	_presets.clear()
	preset_list.clear()
	detail_label.text = ""
	confirm_button.disabled = false


func get_selected_preset_id() -> StringName:
	var selected_items := preset_list.get_selected_items()
	if selected_items.is_empty():
		return &""
	var selected_index := selected_items[0]
	if selected_index < 0 or selected_index >= _presets.size():
		return &""
	return StringName(String(_presets[selected_index].get("preset_id", "")))


func _on_preset_selected(index: int) -> void:
	_refresh_detail(index)


func _on_preset_activated(index: int) -> void:
	preset_list.select(index)
	_refresh_detail(index)
	_emit_selected_preset()


func _refresh_detail(index: int) -> void:
	if index < 0 or index >= _presets.size():
		detail_label.text = ""
		return

	var preset_data := _presets[index]
	var preset_name := String(preset_data.get("display_name", "世界"))
	var size_label := String(preset_data.get("size_label", "未知尺寸"))
	var dev_hint := "会创建一个全新的唯一存档。"
	detail_label.text = "\n".join(PackedStringArray([
		"世界类型：%s" % preset_name,
		"地图尺寸：%s" % size_label,
		dev_hint,
	]))


func _emit_selected_preset() -> void:
	if not visible:
		return
	var preset_id := get_selected_preset_id()
	if preset_id == &"":
		return
	hide_window()
	preset_confirmed.emit(preset_id)


func _cancel() -> void:
	if not visible:
		return
	hide_window()
	cancelled.emit()


func _on_shade_gui_input(event: InputEvent) -> void:
	if event is not InputEventMouseButton:
		return

	var mouse_event := event as InputEventMouseButton
	if not mouse_event.pressed:
		return
	if mouse_event.button_index != MOUSE_BUTTON_LEFT and mouse_event.button_index != MOUSE_BUTTON_RIGHT:
		return

	_cancel()
