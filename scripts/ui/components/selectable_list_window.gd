## 文件说明：可选列表窗口的抽象基类，统一遮罩 / ItemList / 详情 / 空状态 / 确认与取消的标准行为。
## 审查重点：子类需通过 override 钩子提供条目格式化、详情文本、ID 提取以及类型化信号发射；不要在子类直接重声明
## 父类已经处理过的输入与生命周期。
## 备注：子类对应场景需提供以下唯一名节点：%List、%DetailLabel、%EmptyLabel、%ConfirmButton、%CancelButton、%FooterCancelButton；
## 根节点下需有 $Shade(ColorRect)。

class_name SelectableListWindow
extends Control

## ItemList 主题色板：常态走冷蓝，选中切到暖金，与 SelectionCardBuilder 的卡片选中态保持同一视觉语言。
const _LIST_ITEM_HOVER_BG := Color(0.40, 0.50, 0.66, 0.18)
const _LIST_ITEM_SELECTED_BG := Color(0.22, 0.18, 0.10, 0.95)
const _LIST_ITEM_SELECTED_BORDER := Color(0.95, 0.78, 0.32, 1.0)
const _LIST_ITEM_CURSOR_BORDER := Color(0.40, 0.50, 0.66, 0.55)
const _LIST_FONT_NORMAL := Color(0.85, 0.92, 1.0, 0.92)
const _LIST_FONT_HOVER := Color(0.98, 0.94, 0.78, 1.0)
const _LIST_FONT_SELECTED := Color(0.98, 0.86, 0.46, 1.0)
const _LIST_ITEM_CORNER_RADIUS := 6
const _LIST_ITEM_PAD_X := 12
const _LIST_ITEM_PAD_Y := 8

## 字段说明：缓存遮罩节点，用于压暗背景并阻止底层界面继续接收输入。
@onready var shade: ColorRect = $Shade
## 字段说明：缓存条目列表节点，承载单选的 ItemList 行为。
@onready var item_list: ItemList = %List
## 字段说明：缓存详情标签节点，用于展示当前选中条目的关键信息。
@onready var detail_label: Label = %DetailLabel
## 字段说明：缓存空状态标签节点，列表为空时显示。
@onready var empty_label: Label = %EmptyLabel
## 字段说明：缓存确认按钮节点，提交当前选择。
@onready var confirm_button: Button = %ConfirmButton
## 字段说明：缓存头部取消按钮节点，用户主动放弃当前流程。
@onready var cancel_button: Button = %CancelButton
## 字段说明：缓存底部取消按钮节点，与头部取消按钮同义，便于在底部 Footer 中也能放置一致的退出入口。
@onready var footer_cancel_button: Button = %FooterCancelButton

## 字段说明：缓存当前展示的条目数据，按列表顺序与 ItemList 索引对齐。
var _items: Array[Dictionary] = []


func _ready() -> void:
	_apply_item_list_theme(item_list)
	hide_window()
	shade.gui_input.connect(_on_shade_gui_input)
	item_list.item_selected.connect(_on_item_selected)
	item_list.item_activated.connect(_on_item_activated)
	confirm_button.pressed.connect(_emit_selected)
	cancel_button.pressed.connect(_emit_cancel)
	footer_cancel_button.pressed.connect(_emit_cancel)


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
			_emit_cancel()
		KEY_ENTER, KEY_KP_ENTER:
			get_viewport().set_input_as_handled()
			_emit_selected()


## 展示窗口并以传入条目重建列表；若指定 default_id 则在条目中查找并默认选中，否则选中第 0 项。
func show_window(items: Array[Dictionary], default_id: StringName = &"") -> void:
	visible = true
	_items.clear()
	item_list.clear()

	for item in items:
		_items.append(item)
		item_list.add_item(_format_item_label(item))

	var has_items := not _items.is_empty()
	confirm_button.disabled = not has_items
	if empty_label != null:
		empty_label.visible = not has_items

	if not has_items:
		detail_label.text = _format_empty_detail()
		cancel_button.grab_focus()
		return

	var selected_index := 0
	if default_id != &"":
		for i in range(_items.size()):
			if _get_item_id(_items[i]) == default_id:
				selected_index = i
				break

	item_list.select(selected_index)
	item_list.ensure_current_is_visible()
	_refresh_detail(selected_index)
	item_list.grab_focus()


func hide_window() -> void:
	visible = false
	_items.clear()
	if item_list != null:
		item_list.clear()
	if detail_label != null:
		detail_label.text = ""
	if empty_label != null:
		empty_label.visible = false
	if confirm_button != null:
		confirm_button.disabled = false


func get_selected_item_id() -> StringName:
	var selected := item_list.get_selected_items()
	if selected.is_empty():
		return &""
	var idx := selected[0]
	if idx < 0 or idx >= _items.size():
		return &""
	return _get_item_id(_items[idx])


# ----- 子类需要实现的钩子 -----

## 钩子：将单条数据格式化为 ItemList 行文本。子类必须覆盖。
func _format_item_label(_item: Dictionary) -> String:
	return ""


## 钩子：将选中数据格式化为详情面板文本。子类必须覆盖。
func _format_detail_text(_item: Dictionary) -> String:
	return ""


## 钩子：列表为空时的详情提示文本。子类按业务覆盖。
func _format_empty_detail() -> String:
	return "当前没有可用的条目。"


## 钩子：从条目数据中提取 ID。子类必须覆盖以适配自身的字段命名。
func _get_item_id(_item: Dictionary) -> StringName:
	return &""


## 钩子：用户确认时由子类发射类型化信号。基类不持有 confirmed 信号，避免与子类已有信号同名。
func _emit_confirmed_for_id(_item_id: StringName) -> void:
	pass


## 钩子：用户取消时由子类发射类型化信号。基类不持有 cancelled 信号，子类按需命名。
func _emit_cancelled() -> void:
	pass


# ----- 内部分发 -----

func _on_item_selected(index: int) -> void:
	_refresh_detail(index)


func _on_item_activated(index: int) -> void:
	item_list.select(index)
	_refresh_detail(index)
	_emit_selected()


func _refresh_detail(index: int) -> void:
	if index < 0 or index >= _items.size():
		detail_label.text = ""
		return
	detail_label.text = _format_detail_text(_items[index])


func _emit_selected() -> void:
	if not visible:
		return
	var item_id := get_selected_item_id()
	if item_id == &"":
		return
	hide_window()
	_emit_confirmed_for_id(item_id)


func _emit_cancel() -> void:
	if not visible:
		return
	hide_window()
	_emit_cancelled()


func _on_shade_gui_input(event: InputEvent) -> void:
	if event is not InputEventMouseButton:
		return
	var mouse_event := event as InputEventMouseButton
	if not mouse_event.pressed:
		return
	if mouse_event.button_index != MOUSE_BUTTON_LEFT and mouse_event.button_index != MOUSE_BUTTON_RIGHT:
		return
	_emit_cancel()


# ----- 主题样式 -----

## 给 ItemList 套上统一的暖金选中 / 冷蓝悬停样式，背景透明以贴合外层 Panel。
func _apply_item_list_theme(list: ItemList) -> void:
	if list == null:
		return

	var transparent_bg := StyleBoxEmpty.new()
	list.add_theme_stylebox_override("panel", transparent_bg)
	list.add_theme_stylebox_override("focus", transparent_bg)

	var selected_style := _make_item_style(_LIST_ITEM_SELECTED_BG, _LIST_ITEM_SELECTED_BORDER, 3)
	list.add_theme_stylebox_override("selected", selected_style)
	list.add_theme_stylebox_override("selected_focus", selected_style)
	list.add_theme_stylebox_override("hovered_selected", selected_style)
	list.add_theme_stylebox_override("hovered_selected_focus", selected_style)

	var hover_style := _make_item_style(_LIST_ITEM_HOVER_BG, Color(0, 0, 0, 0), 0)
	list.add_theme_stylebox_override("hovered", hover_style)

	var cursor_style := _make_item_style(Color(0, 0, 0, 0), _LIST_ITEM_CURSOR_BORDER, 2)
	list.add_theme_stylebox_override("cursor", cursor_style)
	list.add_theme_stylebox_override("cursor_unfocused", cursor_style)

	list.add_theme_color_override("font_color", _LIST_FONT_NORMAL)
	list.add_theme_color_override("font_hovered_color", _LIST_FONT_HOVER)
	list.add_theme_color_override("font_selected_color", _LIST_FONT_SELECTED)
	list.add_theme_color_override("font_hovered_selected_color", _LIST_FONT_SELECTED)
	list.add_theme_constant_override("v_separation", 4)


static func _make_item_style(bg: Color, border: Color, border_width_left: int) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = bg
	if border_width_left > 0:
		style.border_color = border
		style.border_width_left = border_width_left
	style.corner_radius_top_left = _LIST_ITEM_CORNER_RADIUS
	style.corner_radius_top_right = _LIST_ITEM_CORNER_RADIUS
	style.corner_radius_bottom_right = _LIST_ITEM_CORNER_RADIUS
	style.corner_radius_bottom_left = _LIST_ITEM_CORNER_RADIUS
	style.content_margin_left = _LIST_ITEM_PAD_X
	style.content_margin_right = _LIST_ITEM_PAD_X - 2
	style.content_margin_top = _LIST_ITEM_PAD_Y
	style.content_margin_bottom = _LIST_ITEM_PAD_Y
	return style
