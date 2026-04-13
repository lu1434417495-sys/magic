## 文件说明：该脚本属于晋升选择窗口相关的界面窗口脚本，集中维护遮罩、标题标签、元信息标签等顶层字段。
## 审查重点：重点核对字段含义、节点绑定、信号联动以及界面状态切换是否仍与对应场景保持一致。
## 备注：后续如果调整场景节点命名、层级或交互路径，需要同步检查成员字段与信号连接。

class_name PromotionChoiceWindow
extends Control

## 信号说明：当选择已经提交时发出的信号，供外层写回数据并触发后续逻辑。
signal choice_submitted(member_id: StringName, profession_id: StringName, selection: Dictionary)
## 信号说明：当用户取消当前流程时发出的信号，供外层恢复默认界面状态或焦点。
signal cancelled

## 字段说明：缓存遮罩节点，用于压暗背景并阻止底层界面继续接收输入。
@onready var shade: ColorRect = $Shade
## 字段说明：缓存标题标签节点，用于显示当前窗口或面板的主标题。
@onready var title_label: Label = $CenterContainer/Panel/MarginContainer/Content/Header/HeaderText/TitleLabel
## 字段说明：缓存副标题标签节点，用于显示补充状态、来源信息或筛选摘要。
@onready var meta_label: Label = $CenterContainer/Panel/MarginContainer/Content/Header/HeaderText/MetaLabel
## 字段说明：缓存选择列表节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var choice_list: ItemList = $CenterContainer/Panel/MarginContainer/Content/Body/ChoiceList
## 字段说明：缓存详情标签节点，用于展示较长的说明文本或选中对象详情。
@onready var details_label: RichTextLabel = $CenterContainer/Panel/MarginContainer/Content/Body/DetailsLabel
## 字段说明：缓存确认按钮节点，供用户提交当前选择结果。
@onready var confirm_button: Button = $CenterContainer/Panel/MarginContainer/Content/Footer/ConfirmButton
## 字段说明：缓存取消按钮节点，供用户主动中止当前流程。
@onready var cancel_button: Button = $CenterContainer/Panel/MarginContainer/Content/Footer/CancelButton

## 字段说明：记录成员唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var _member_id: StringName = &""
## 字段说明：记录成员名称，作为界面刷新、输入处理和窗口联动的重要依据。
var _member_name := ""
## 字段说明：缓存候选项字典，集中保存可按键查询的运行时数据。
var _choices: Array[Dictionary] = []
## 字段说明：记录已选索引，作为界面刷新、输入处理和窗口联动的重要依据。
var _selected_index := -1


func _ready() -> void:
	hide_window()
	shade.gui_input.connect(_on_shade_gui_input)
	choice_list.item_selected.connect(_on_choice_selected)
	confirm_button.pressed.connect(_on_confirm_button_pressed)
	cancel_button.pressed.connect(_on_cancel_button_pressed)


func show_promotion(prompt_data: Dictionary) -> void:
	_member_id = ProgressionDataUtils.to_string_name(prompt_data.get("member_id", ""))
	_member_name = String(prompt_data.get("member_name", "成员"))
	_choices = []
	for choice_data in prompt_data.get("choices", []):
		if choice_data is Dictionary:
			_choices.append(choice_data.duplicate(true))

	visible = true
	title_label.text = "职业晋升"
	meta_label.text = "%s 触发了新的职业晋升选择。" % _member_name
	_rebuild_choice_list()
	_select_choice(0 if not _choices.is_empty() else -1)


func hide_window() -> void:
	visible = false
	_member_id = &""
	_member_name = ""
	_choices.clear()
	_selected_index = -1
	if choice_list != null:
		choice_list.clear()
	if details_label != null:
		details_label.text = ""
	if confirm_button != null:
		confirm_button.disabled = true


func _rebuild_choice_list() -> void:
	choice_list.clear()
	for choice_data in _choices:
		var profession_id := ProgressionDataUtils.to_string_name(choice_data.get("profession_id", ""))
		var display_name := String(choice_data.get("display_name", profession_id))
		var summary := String(choice_data.get("summary", ""))
		var label := display_name if summary.is_empty() else "%s  |  %s" % [display_name, summary]
		choice_list.add_item(label)
		choice_list.set_item_metadata(choice_list.item_count - 1, profession_id)


func _select_choice(index: int) -> void:
	_selected_index = index
	choice_list.deselect_all()
	if index >= 0 and index < choice_list.item_count:
		choice_list.select(index)
	_refresh_details()


func _refresh_details() -> void:
	if _selected_index < 0 or _selected_index >= _choices.size():
		details_label.text = "当前没有可用晋升项。"
		confirm_button.disabled = true
		return

	var choice_data: Dictionary = _choices[_selected_index]
	var profession_id := ProgressionDataUtils.to_string_name(choice_data.get("profession_id", ""))
	var display_name := String(choice_data.get("display_name", profession_id))
	var description := String(choice_data.get("description", ""))
	var granted_skill_ids := ProgressionDataUtils.to_string_name_array(choice_data.get("granted_skill_ids", []))
	var selection_hint := String(choice_data.get("selection_hint", "确认后将在战斗中立即生效。"))

	details_label.text = "\n".join(PackedStringArray([
		"成员：%s" % _member_name,
		"职业：%s" % display_name,
		"ID：%s" % String(profession_id),
		"描述：%s" % (description if not description.is_empty() else "暂无"),
		"授予技能：%s" % (", ".join(granted_skill_ids) if not granted_skill_ids.is_empty() else "暂无"),
		"说明：%s" % selection_hint,
	]))
	confirm_button.disabled = false


func _on_choice_selected(index: int) -> void:
	_select_choice(index)


func _on_confirm_button_pressed() -> void:
	if _selected_index < 0 or _selected_index >= _choices.size():
		return

	var choice_data: Dictionary = _choices[_selected_index]
	var profession_id := ProgressionDataUtils.to_string_name(choice_data.get("profession_id", ""))
	var selection: Dictionary = choice_data.get("selection", {}).duplicate(true)
	hide_window()
	choice_submitted.emit(_member_id, profession_id, selection)


func _on_cancel_button_pressed() -> void:
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
	_on_cancel_button_pressed()
