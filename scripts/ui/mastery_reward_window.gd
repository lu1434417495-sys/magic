## 文件说明：该脚本属于熟练度奖励窗口相关的界面窗口脚本，集中维护遮罩、标题标签、元信息标签等顶层字段。
## 审查重点：重点核对字段含义、节点绑定、信号联动以及界面状态切换是否仍与对应场景保持一致。
## 备注：后续如果调整场景节点命名、层级或交互路径，需要同步检查成员字段与信号连接。

class_name MasteryRewardWindow
extends Control

## 信号说明：当用户确认内部字段后发出的信号，供外层继续推进流程。
signal confirmed

## 字段说明：缓存遮罩节点，用于压暗背景并阻止底层界面继续接收输入。
@onready var shade: ColorRect = $Shade
## 字段说明：缓存标题标签节点，用于显示当前窗口或面板的主标题。
@onready var title_label: Label = $CenterContainer/Panel/MarginContainer/Content/Header/HeaderText/TitleLabel
## 字段说明：缓存副标题标签节点，用于显示补充状态、来源信息或筛选摘要。
@onready var meta_label: Label = $CenterContainer/Panel/MarginContainer/Content/Header/HeaderText/MetaLabel
## 字段说明：缓存详情标签节点，用于展示较长的说明文本或选中对象详情。
@onready var details_label: RichTextLabel = $CenterContainer/Panel/MarginContainer/Content/Body/DetailsLabel
## 字段说明：缓存确认按钮节点，供用户提交当前选择结果。
@onready var confirm_button: Button = $CenterContainer/Panel/MarginContainer/Content/Footer/ConfirmButton

## 字段说明：记录奖励对象，作为界面刷新、输入处理和窗口联动的重要依据。
var _reward = null


func _ready() -> void:
	hide_window()
	shade.gui_input.connect(_on_shade_gui_input)
	confirm_button.pressed.connect(_on_confirm_button_pressed)


func show_reward(reward, remaining_count: int = 1) -> void:
	_reward = reward
	visible = true

	var member_name := _read_reward_text("member_name", "成员")
	var source_label := _read_reward_text("source_label", "角色奖励")
	var summary_text := _read_reward_text("summary_text", "")

	title_label.text = "角色奖励"
	meta_label.text = "%s · %s%s" % [
		member_name,
		source_label,
		" · 待处理 %d 项" % remaining_count if remaining_count > 1 else "",
	]
	details_label.text = _build_details_text(member_name, source_label, summary_text)
	confirm_button.disabled = false


func hide_window() -> void:
	visible = false
	_reward = null
	if details_label != null:
		details_label.text = ""
	if confirm_button != null:
		confirm_button.disabled = true


func _build_details_text(member_name: String, source_label: String, summary_text: String) -> String:
	var lines: PackedStringArray = []
	if not summary_text.is_empty():
		lines.append(summary_text)
		lines.append("")
	lines.append("成员：%s" % member_name)
	lines.append("来源：%s" % source_label)
	lines.append("")
	lines.append("奖励内容：")

	for entry in _get_reward_entries():
		if entry == null:
			continue
		lines.append(_build_entry_line(entry))

	lines.append("")
	lines.append("确认后将立即把本批奖励结算到角色成长中。")
	return "\n".join(lines)


func _build_entry_line(entry) -> String:
	var entry_type := String(entry.entry_type) if entry != null else ""
	var target_label := _read_entry_target_label(entry)
	var amount := int(entry.amount) if entry != null else 0
	var reason_text := String(entry.reason_text) if entry != null else ""
	var line := ""

	match entry_type:
		"knowledge_unlock":
			line = "- 解锁知识：%s" % target_label
		"skill_unlock":
			line = "- 解锁技能：%s" % target_label
		"skill_mastery":
			line = "- %s 技能熟练度增长 %d" % [target_label, amount]
		"attribute_delta":
			var signed_amount := "+%d" % amount if amount > 0 else str(amount)
			line = "- %s %s" % [target_label, signed_amount]
		_:
			line = "- %s × %d" % [target_label, amount]

	if not reason_text.is_empty():
		line += " · %s" % reason_text
	return line


func _read_entry_target_label(entry) -> String:
	if entry == null:
		return "未知条目"
	if not String(entry.target_label).is_empty():
		return String(entry.target_label)
	if not String(entry.target_id).is_empty():
		return String(entry.target_id)
	return "未知条目"


func _get_reward_entries() -> Array:
	if _reward == null:
		return []
	return _reward.entries if _reward.entries != null else []


func _read_reward_text(field_name: String, default_value: String) -> String:
	if _reward == null:
		return default_value
	var value = null
	match field_name:
		"member_name":
			value = _reward.member_name
		"source_label":
			value = _reward.source_label
		"summary_text":
			value = _reward.summary_text
		_:
			value = null
	return String(value) if value != null and not String(value).is_empty() else default_value


func _on_confirm_button_pressed() -> void:
	if not visible:
		return
	confirmed.emit()


func _on_shade_gui_input(event: InputEvent) -> void:
	if event is not InputEventMouseButton:
		return
	var mouse_event := event as InputEventMouseButton
	if not mouse_event.pressed:
		return
	if mouse_event.button_index != MOUSE_BUTTON_LEFT and mouse_event.button_index != MOUSE_BUTTON_RIGHT:
		return
	accept_event()
