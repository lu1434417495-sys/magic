## 文件说明：该脚本属于角色信息窗口相关的界面窗口脚本，集中维护遮罩、标题标签、元信息标签等顶层字段。
## 审查重点：重点核对字段含义、节点绑定、信号联动以及界面状态切换是否仍与对应场景保持一致。
## 备注：后续如果调整场景节点命名、层级或交互路径，需要同步检查成员字段与信号连接。

class_name CharacterInfoWindow
extends Control

## 信号说明：当窗口或面板关闭时发出的信号，供外层恢复输入焦点、刷新数据或清理临时状态。
signal closed

## 字段说明：缓存遮罩节点，用于压暗背景并阻止底层界面继续接收输入。
@onready var shade: ColorRect = $Shade
## 字段说明：缓存标题标签节点，用于显示当前窗口或面板的主标题。
@onready var title_label: Label = $CenterContainer/Panel/MarginContainer/Content/Header/HeaderText/TitleLabel
## 字段说明：缓存副标题标签节点，用于显示补充状态、来源信息或筛选摘要。
@onready var meta_label: Label = $CenterContainer/Panel/MarginContainer/Content/Header/HeaderText/MetaLabel
## 字段说明：缓存详情标签节点，用于展示较长的说明文本或选中对象详情。
@onready var details_label: RichTextLabel = $CenterContainer/Panel/MarginContainer/Content/Body/DetailsLabel
## 字段说明：缓存状态提示标签节点，用于向玩家展示当前操作结果、错误原因或下一步引导。
@onready var status_label: Label = $CenterContainer/Panel/MarginContainer/Content/Body/StatusLabel
## 字段说明：缓存关闭按钮节点，供窗口统一执行收尾和关闭逻辑。
@onready var close_button: Button = $CenterContainer/Panel/MarginContainer/Content/Header/CloseButton


func _ready() -> void:
	hide_window()
	shade.gui_input.connect(_on_shade_gui_input)
	close_button.pressed.connect(_close_window)


func show_character(window_data: Dictionary) -> void:
	var display_name := String(window_data.get("display_name", "人物"))
	var type_label := _normalize_label(String(window_data.get("type_label", "")), "未知类型")
	var faction_label := _normalize_label(String(window_data.get("faction_label", "")), "未知")
	var coord_text := _format_coord(window_data.get("coord", Vector2i.ZERO))
	var status_text := _normalize_label(String(window_data.get("status_label", "")), "状态未知")

	visible = true
	title_label.text = display_name
	meta_label.text = "%s  |  阵营 %s  |  坐标 %s" % [type_label, faction_label, coord_text]
	details_label.text = "\n".join(PackedStringArray([
		"姓名：%s" % display_name,
		"类型：%s" % type_label,
		"阵营：%s" % faction_label,
		"坐标：%s" % coord_text,
	]))
	status_label.text = status_text


func hide_window() -> void:
	visible = false
	title_label.text = "人物信息"
	meta_label.text = ""
	details_label.text = ""
	status_label.text = ""


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


func _format_coord(coord_value: Variant) -> String:
	if coord_value is Vector2i:
		var coord := coord_value as Vector2i
		return "(%d, %d)" % [coord.x, coord.y]
	if coord_value is Vector2:
		var coordf := coord_value as Vector2
		return "(%d, %d)" % [int(coordf.x), int(coordf.y)]
	return "(0, 0)"


func _normalize_label(value: String, fallback: String) -> String:
	if value.is_empty():
		return fallback
	return value
