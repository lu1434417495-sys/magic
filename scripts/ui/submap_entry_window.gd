## 文件说明：该脚本属于子地图进入确认窗口相关的界面窗口脚本，集中维护标题、说明和确认按钮等顶层字段。
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

var _dismiss_on_shade := true


func _ready() -> void:
	hide_window()
	shade.gui_input.connect(_on_shade_gui_input)
	confirm_button.pressed.connect(_on_confirm_button_pressed)
	cancel_button.pressed.connect(_on_cancel_button_pressed)


func show_prompt(prompt: Dictionary) -> void:
	visible = true
	title_label.text = String(prompt.get("title", "进入子地图"))
	description_label.text = String(prompt.get("description", "确认后将进入新的区域。"))
	confirm_button.text = String(prompt.get("confirm_text", "确认进入"))
	cancel_button.text = String(prompt.get("cancel_text", "取消"))
	cancel_button.visible = bool(prompt.get("cancel_visible", true))
	_dismiss_on_shade = bool(prompt.get("dismiss_on_shade", true))


func hide_window() -> void:
	visible = false
	title_label.text = ""
	description_label.text = ""
	confirm_button.text = "确认进入"
	cancel_button.text = "取消"
	cancel_button.visible = true
	_dismiss_on_shade = true


func _on_confirm_button_pressed() -> void:
	hide_window()
	confirmed.emit()


func _on_cancel_button_pressed() -> void:
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
