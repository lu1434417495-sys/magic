## 文件说明：该脚本属于显示设置窗口相关的界面窗口脚本，集中维护遮罩、分辨率选项按钮、全屏校验按钮等顶层字段。
## 审查重点：重点核对字段含义、节点绑定、信号联动以及界面状态切换是否仍与对应场景保持一致。
## 备注：后续如果调整场景节点命名、层级或交互路径，需要同步检查成员字段与信号连接。

class_name DisplaySettingsWindow
extends Control

## 信号说明：当界面请求设置应用时发出的信号，具体处理由外层系统或控制器负责。
signal settings_apply_requested(settings: Dictionary)
## 信号说明：当用户取消当前流程时发出的信号，供外层恢复默认界面状态或焦点。
signal cancelled

## 字段说明：缓存遮罩节点，用于压暗背景并阻止底层界面继续接收输入。
@onready var shade: ColorRect = $Shade
## 字段说明：缓存分辨率选项按钮节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var resolution_option_button: OptionButton = %ResolutionOptionButton
## 字段说明：缓存全屏校验按钮，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var fullscreen_check_button: CheckButton = %FullscreenCheckButton
## 字段说明：缓存提示标签节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var hint_label: Label = %HintLabel
## 字段说明：缓存应用按钮节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var apply_button: Button = %ApplyButton
## 字段说明：缓存取消按钮节点，供用户主动中止当前流程。
@onready var cancel_button: Button = %CancelButton
## 字段说明：缓存头部关闭按钮节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var header_close_button: Button = %HeaderCloseButton

## 字段说明：缓存分辨率选项列表字典，集中保存可按键查询的运行时数据。
var _resolution_options: Array[Dictionary] = []


func _ready() -> void:
	hide_window()
	shade.gui_input.connect(_on_shade_gui_input)
	fullscreen_check_button.toggled.connect(_on_fullscreen_toggled)
	apply_button.pressed.connect(_apply)
	cancel_button.pressed.connect(_cancel)
	header_close_button.pressed.connect(_cancel)


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
			_apply()


func configure_options(resolution_options: Array[Dictionary]) -> void:
	_resolution_options.clear()
	for entry in resolution_options:
		var resolution: Vector2i = entry.get("size", Vector2i.ZERO)
		if resolution.x <= 0 or resolution.y <= 0:
			continue
		_resolution_options.append({
			"label": String(entry.get("label", "%d x %d" % [resolution.x, resolution.y])),
			"size": resolution,
		})
	_rebuild_resolution_options()


func show_window(current_settings: Dictionary) -> void:
	visible = true
	_rebuild_resolution_options()

	var selected_resolution: Vector2i = current_settings.get("resolution", Vector2i(1280, 720))
	var selected_index := _find_resolution_index(selected_resolution)
	if resolution_option_button.get_item_count() > 0:
		resolution_option_button.select(selected_index)
	fullscreen_check_button.button_pressed = bool(current_settings.get("fullscreen", false))
	_update_hint()

	if resolution_option_button.get_item_count() > 0:
		resolution_option_button.grab_focus()
	else:
		cancel_button.grab_focus()


func hide_window() -> void:
	visible = false
	fullscreen_check_button.button_pressed = false
	hint_label.text = ""


func get_selected_settings() -> Dictionary:
	return {
		"resolution": _get_selected_resolution(),
		"fullscreen": fullscreen_check_button.button_pressed,
	}


func _rebuild_resolution_options() -> void:
	if resolution_option_button == null:
		return
	resolution_option_button.clear()
	for entry in _resolution_options:
		resolution_option_button.add_item(String(entry.get("label", "")))
	apply_button.disabled = _resolution_options.is_empty()


func _find_resolution_index(resolution: Vector2i) -> int:
	for index in range(_resolution_options.size()):
		if _resolution_options[index].size == resolution:
			return index
	return 0


func _get_selected_resolution() -> Vector2i:
	if _resolution_options.is_empty():
		return Vector2i(1280, 720)
	var selected_index := maxi(resolution_option_button.get_selected_id(), 0)
	if selected_index >= _resolution_options.size():
		selected_index = 0
	return _resolution_options[selected_index].size


func _on_fullscreen_toggled(_pressed: bool) -> void:
	_update_hint()


func _update_hint() -> void:
	if fullscreen_check_button.button_pressed:
		hint_label.text = "全屏模式会优先使用显示器的全屏显示；退出全屏后恢复所选窗口分辨率。"
	else:
		hint_label.text = "窗口模式会立即切换到所选的常见分辨率。"


func _apply() -> void:
	if not visible or apply_button.disabled:
		return
	var settings := get_selected_settings()
	hide_window()
	settings_apply_requested.emit(settings)


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
