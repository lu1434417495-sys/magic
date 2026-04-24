## 文件说明：该脚本属于通用确认窗口相关的界面窗口脚本，集中维护标题、说明和确认按钮等顶层字段。
## 审查重点：重点核对字段含义、节点绑定、信号联动以及界面状态切换是否仍与对应场景保持一致。
## 备注：后续如果调整场景节点命名、层级或交互路径，需要同步检查成员字段与信号连接。

class_name SubmapEntryWindow
extends Control

## 信号说明：当界面请求确认进入子地图时发出的信号，具体处理由外层系统或控制器负责。
signal confirmed
## 信号说明：当界面请求取消进入子地图时发出的信号，供外层恢复探索输入。
signal cancelled

## 字段说明：缓存遮罩节点，用于压暗背景并阻止底层界面继续接收输入。
@onready var shade: ColorRect = $Shade
## 字段说明：缓存标题标签节点，用于显示当前窗口或面板的主标题。
@onready var title_label: Label = $CenterContainer/Panel/MarginContainer/Layout/TitleLabel
## 字段说明：缓存说明标签节点，用于显示补充状态、来源信息或筛选摘要。
@onready var description_label: Label = $CenterContainer/Panel/MarginContainer/Layout/DescriptionLabel
## 字段说明：缓存确认按钮节点，供用户提交当前选择结果。
@onready var confirm_button: Button = $CenterContainer/Panel/MarginContainer/Layout/ButtonRow/ConfirmButton
## 字段说明：缓存取消按钮节点，供窗口统一执行收尾和关闭逻辑。
@onready var cancel_button: Button = $CenterContainer/Panel/MarginContainer/Layout/ButtonRow/CancelButton
## 字段说明：缓存主面板节点，用于按不同确认场景切换窗口尺寸和视觉规格。
@onready var panel: PanelContainer = $CenterContainer/Panel
## 字段说明：缓存边距容器节点，用于按不同确认场景切换留白规格。
@onready var margin_container: MarginContainer = $CenterContainer/Panel/MarginContainer
## 字段说明：缓存布局容器节点，用于按不同确认场景切换纵向间距。
@onready var layout: VBoxContainer = $CenterContainer/Panel/MarginContainer/Layout
## 字段说明：缓存按钮行节点，用于按不同确认场景切换按钮间距。
@onready var button_row: HBoxContainer = $CenterContainer/Panel/MarginContainer/Layout/ButtonRow

var _dismiss_on_shade := true
var _cancel_visible := true
var _accept_input_enabled := false

var _default_panel_min_size := Vector2.ZERO
var _default_confirm_button_min_size := Vector2.ZERO
var _default_cancel_button_min_size := Vector2.ZERO
var _default_title_font_size := 0
var _default_description_font_size := 0
var _default_confirm_button_font_size := 0
var _default_cancel_button_font_size := 0
var _default_margin_left := 0
var _default_margin_top := 0
var _default_margin_right := 0
var _default_margin_bottom := 0
var _default_layout_separation := 0
var _default_button_row_separation := 0


func _ready() -> void:
	_cache_default_metrics()
	hide_window()
	shade.gui_input.connect(_on_shade_gui_input)
	confirm_button.pressed.connect(_on_confirm_button_pressed)
	cancel_button.pressed.connect(_on_cancel_button_pressed)
	confirm_button.focus_mode = Control.FOCUS_ALL
	cancel_button.focus_mode = Control.FOCUS_ALL


func show_prompt(prompt: Dictionary) -> void:
	_apply_prompt_metrics(prompt)
	visible = true
	title_label.text = String(prompt.get("title", "进入子地图"))
	description_label.text = String(prompt.get("description", "确认后将进入新的区域。"))
	confirm_button.text = String(prompt.get("confirm_text", "确认进入"))
	cancel_button.text = String(prompt.get("cancel_text", "取消"))
	_cancel_visible = bool(prompt.get("cancel_visible", true))
	cancel_button.visible = _cancel_visible
	cancel_button.disabled = not _cancel_visible
	_dismiss_on_shade = bool(prompt.get("dismiss_on_shade", true))
	_accept_input_enabled = bool(prompt.get("accept_input_enabled", false))
	if _accept_input_enabled:
		confirm_button.grab_focus()


func hide_window() -> void:
	_restore_default_metrics()
	visible = false
	title_label.text = ""
	description_label.text = ""
	confirm_button.text = "确认进入"
	cancel_button.text = "取消"
	cancel_button.visible = true
	cancel_button.disabled = false
	_dismiss_on_shade = true
	_cancel_visible = true
	_accept_input_enabled = false


func _on_confirm_button_pressed() -> void:
	hide_window()
	confirmed.emit()


func _on_cancel_button_pressed() -> void:
	if not _cancel_visible:
		return
	hide_window()
	cancelled.emit()


func _on_shade_gui_input(event: InputEvent) -> void:
	if not visible:
		return
	if not _dismiss_on_shade:
		return
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		hide_window()
		cancelled.emit()


func _unhandled_input(event: InputEvent) -> void:
	if not visible or not _accept_input_enabled or event == null:
		return
	if event.is_action_pressed("ui_accept"):
		get_viewport().set_input_as_handled()
		_on_confirm_button_pressed()


func _cache_default_metrics() -> void:
	_default_panel_min_size = panel.custom_minimum_size
	_default_confirm_button_min_size = confirm_button.custom_minimum_size
	_default_cancel_button_min_size = cancel_button.custom_minimum_size
	_default_title_font_size = _read_int_property(title_label, "theme_override_font_sizes/font_size")
	_default_description_font_size = _read_int_property(description_label, "theme_override_font_sizes/font_size")
	_default_confirm_button_font_size = _read_int_property(confirm_button, "theme_override_font_sizes/font_size")
	_default_cancel_button_font_size = _read_int_property(cancel_button, "theme_override_font_sizes/font_size")
	_default_margin_left = _read_int_property(margin_container, "theme_override_constants/margin_left")
	_default_margin_top = _read_int_property(margin_container, "theme_override_constants/margin_top")
	_default_margin_right = _read_int_property(margin_container, "theme_override_constants/margin_right")
	_default_margin_bottom = _read_int_property(margin_container, "theme_override_constants/margin_bottom")
	_default_layout_separation = _read_int_property(layout, "theme_override_constants/separation")
	_default_button_row_separation = _read_int_property(button_row, "theme_override_constants/separation")


func _apply_prompt_metrics(prompt: Dictionary) -> void:
	panel.custom_minimum_size = prompt.get("panel_min_size", _default_panel_min_size)
	confirm_button.custom_minimum_size = prompt.get("confirm_button_min_size", _default_confirm_button_min_size)
	cancel_button.custom_minimum_size = prompt.get("cancel_button_min_size", _default_cancel_button_min_size)
	_set_font_override(title_label, int(prompt.get("title_font_size", _default_title_font_size)))
	_set_font_override(description_label, int(prompt.get("description_font_size", _default_description_font_size)))
	_set_font_override(confirm_button, int(prompt.get("confirm_button_font_size", _default_confirm_button_font_size)))
	_set_font_override(cancel_button, int(prompt.get("cancel_button_font_size", _default_cancel_button_font_size)))
	margin_container.add_theme_constant_override("margin_left", int(prompt.get("margin_left", _default_margin_left)))
	margin_container.add_theme_constant_override("margin_top", int(prompt.get("margin_top", _default_margin_top)))
	margin_container.add_theme_constant_override("margin_right", int(prompt.get("margin_right", _default_margin_right)))
	margin_container.add_theme_constant_override("margin_bottom", int(prompt.get("margin_bottom", _default_margin_bottom)))
	layout.add_theme_constant_override("separation", int(prompt.get("layout_separation", _default_layout_separation)))
	button_row.add_theme_constant_override("separation", int(prompt.get("button_row_separation", _default_button_row_separation)))


func _restore_default_metrics() -> void:
	panel.custom_minimum_size = _default_panel_min_size
	confirm_button.custom_minimum_size = _default_confirm_button_min_size
	cancel_button.custom_minimum_size = _default_cancel_button_min_size
	_set_font_override(title_label, _default_title_font_size)
	_set_font_override(description_label, _default_description_font_size)
	_set_font_override(confirm_button, _default_confirm_button_font_size)
	_set_font_override(cancel_button, _default_cancel_button_font_size)
	margin_container.add_theme_constant_override("margin_left", _default_margin_left)
	margin_container.add_theme_constant_override("margin_top", _default_margin_top)
	margin_container.add_theme_constant_override("margin_right", _default_margin_right)
	margin_container.add_theme_constant_override("margin_bottom", _default_margin_bottom)
	layout.add_theme_constant_override("separation", _default_layout_separation)
	button_row.add_theme_constant_override("separation", _default_button_row_separation)


func _set_font_override(control: Control, font_size: int) -> void:
	if font_size > 0:
		control.add_theme_font_size_override("font_size", font_size)
		return
	control.remove_theme_font_size_override("font_size")


func _read_int_property(target: Object, property_name: String, fallback: int = 0) -> int:
	if target == null:
		return fallback
	var value: Variant = target.get(property_name)
	return value if value is int else fallback
