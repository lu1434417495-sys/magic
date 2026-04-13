## 文件说明：该脚本属于队伍仓库窗口相关的界面窗口脚本，集中维护遮罩、标题标签、元信息标签等顶层字段。
## 审查重点：重点核对字段含义、节点绑定、信号联动以及界面状态切换是否仍与对应场景保持一致。
## 备注：后续如果调整场景节点命名、层级或交互路径，需要同步检查成员字段与信号连接。

class_name PartyWarehouseWindow
extends Control

## 信号说明：当界面请求丢弃单个时发出的信号，具体处理由外层系统或控制器负责。
signal discard_one_requested(item_id: StringName)
## 信号说明：当界面请求丢弃全部时发出的信号，具体处理由外层系统或控制器负责。
signal discard_all_requested(item_id: StringName)
## 信号说明：当界面请求使用技能书时发出的信号，具体处理由外层系统或控制器负责。
signal use_requested(item_id: StringName, member_id: StringName)
## 信号说明：当窗口或面板关闭时发出的信号，供外层恢复输入焦点、刷新数据或清理临时状态。
signal closed

## 字段说明：缓存遮罩节点，用于压暗背景并阻止底层界面继续接收输入。
@onready var shade: ColorRect = $Shade
## 字段说明：缓存标题标签节点，用于显示当前窗口或面板的主标题。
@onready var title_label: Label = $CenterContainer/Panel/MarginContainer/Content/Header/HeaderText/TitleLabel
## 字段说明：缓存副标题标签节点，用于显示补充状态、来源信息或筛选摘要。
@onready var meta_label: Label = $CenterContainer/Panel/MarginContainer/Content/Header/HeaderText/MetaLabel
## 字段说明：缓存堆叠列表节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var stack_list: ItemList = $CenterContainer/Panel/MarginContainer/Content/Body/ListColumn/StackList
## 字段说明：缓存摘要标签节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var summary_label: Label = $CenterContainer/Panel/MarginContainer/Content/Body/DetailsColumn/SummaryLabel
## 字段说明：缓存物品图标节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var item_icon: TextureRect = $CenterContainer/Panel/MarginContainer/Content/Body/DetailsColumn/ItemRow/IconFrame/ItemIcon
## 字段说明：缓存详情标签节点，用于展示较长的说明文本或选中对象详情。
@onready var details_label: RichTextLabel = $CenterContainer/Panel/MarginContainer/Content/Body/DetailsColumn/ItemRow/DetailsLabel
## 字段说明：缓存状态提示标签节点，用于向玩家展示当前操作结果、错误原因或下一步引导。
@onready var status_label: Label = $CenterContainer/Panel/MarginContainer/Content/Body/DetailsColumn/StatusLabel
## 字段说明：缓存丢弃单个按钮节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var discard_one_button: Button = $CenterContainer/Panel/MarginContainer/Content/Body/Controls/DiscardOneButton
## 字段说明：缓存丢弃全部按钮节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var discard_all_button: Button = $CenterContainer/Panel/MarginContainer/Content/Body/Controls/DiscardAllButton
## 字段说明：缓存技能书目标标签节点，用于提示当前会被技能书作用到的队员。
@onready var target_member_label: Label = $CenterContainer/Panel/MarginContainer/Content/Body/Controls/TargetMemberLabel
## 字段说明：缓存技能书目标选择节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var target_member_selector: OptionButton = $CenterContainer/Panel/MarginContainer/Content/Body/Controls/TargetMemberSelector
## 字段说明：缓存使用按钮节点，用于触发技能书消耗与学习流程。
@onready var use_button: Button = $CenterContainer/Panel/MarginContainer/Content/Body/Controls/UseButton
## 字段说明：缓存关闭按钮节点，供窗口统一执行收尾和关闭逻辑。
@onready var close_button: Button = $CenterContainer/Panel/MarginContainer/Content/Header/CloseButton

## 字段说明：缓存窗口数据字典，集中保存可按键查询的运行时数据。
var _window_data: Dictionary = {}
## 字段说明：记录已选物品唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var _selected_item_id: StringName = &""
## 字段说明：记录已选堆叠索引，作为界面刷新、输入处理和窗口联动的重要依据。
var _selected_stack_index := -1
## 字段说明：记录技能书当前目标成员唯一标识，作为技能书使用流程的目标选择缓存。
var _selected_target_member_id: StringName = &""


func _ready() -> void:
	hide_window()
	shade.gui_input.connect(_on_shade_gui_input)
	stack_list.item_selected.connect(_on_stack_selected)
	discard_one_button.pressed.connect(_on_discard_one_button_pressed)
	discard_all_button.pressed.connect(_on_discard_all_button_pressed)
	target_member_selector.item_selected.connect(_on_target_member_selected)
	use_button.pressed.connect(_on_use_button_pressed)
	close_button.pressed.connect(_close_window)


func show_warehouse(window_data: Dictionary) -> void:
	set_window_data(window_data)
	visible = true
	refresh_view()


func set_window_data(window_data: Dictionary) -> void:
	_window_data = window_data.duplicate(true)
	if visible:
		refresh_view()


func refresh_view() -> void:
	title_label.text = String(_window_data.get("title", "共享仓库"))
	meta_label.text = String(_window_data.get("meta", "共享背包按堆栈占格，不计算重量。"))
	summary_label.text = String(_window_data.get("summary_text", ""))
	status_label.text = String(_window_data.get("status_text", ""))
	_rebuild_stack_list()
	_rebuild_target_member_selector()
	_restore_selection()
	_refresh_details()
	_refresh_controls()


func hide_window() -> void:
	visible = false
	_window_data.clear()
	_selected_item_id = &""
	_selected_stack_index = -1
	_selected_target_member_id = &""
	if stack_list != null:
		stack_list.clear()
	if target_member_selector != null:
		target_member_selector.clear()
	if summary_label != null:
		summary_label.text = ""
	if item_icon != null:
		item_icon.texture = null
	if details_label != null:
		details_label.text = ""
	if status_label != null:
		status_label.text = ""


func _rebuild_stack_list() -> void:
	stack_list.clear()
	for stack_data_variant in _get_stack_entries():
		var stack_data: Dictionary = stack_data_variant
		var label := "%s  x%d" % [
			String(stack_data.get("display_name", stack_data.get("item_id", "未知物品"))),
			int(stack_data.get("quantity", 0)),
		]
		if bool(stack_data.get("is_stackable", false)):
			label += "  |  堆栈 %d/%d" % [
				int(stack_data.get("quantity", 0)),
				int(stack_data.get("stack_limit", 1)),
			]
		else:
			label += "  |  不可堆叠"
		stack_list.add_item(label)
		stack_list.set_item_metadata(stack_list.item_count - 1, stack_data)


func _restore_selection() -> void:
	var stack_entries := _get_stack_entries()
	if stack_entries.is_empty():
		_selected_item_id = &""
		_selected_stack_index = -1
		return

	var target_index := -1
	if _selected_item_id != &"":
		for index in range(stack_entries.size()):
			var stack_data: Dictionary = stack_entries[index]
			if ProgressionDataUtils.to_string_name(stack_data.get("item_id", "")) == _selected_item_id:
				target_index = index
				break
	if target_index < 0:
		target_index = clampi(_selected_stack_index, 0, stack_entries.size() - 1)

	_selected_stack_index = target_index
	var selected_stack: Dictionary = stack_entries[target_index]
	_selected_item_id = ProgressionDataUtils.to_string_name(selected_stack.get("item_id", ""))
	stack_list.select(target_index)
	stack_list.ensure_current_is_visible()


func _rebuild_target_member_selector() -> void:
	target_member_selector.clear()
	var target_members := _get_target_members()
	if target_members.is_empty():
		_selected_target_member_id = &""
		return

	var selected_index := 0
	for index in range(target_members.size()):
		var member_data: Dictionary = target_members[index]
		var member_id := ProgressionDataUtils.to_string_name(member_data.get("member_id", ""))
		var label := String(member_data.get("display_name", member_id))
		target_member_selector.add_item(label)
		target_member_selector.set_item_metadata(index, member_data)
		if member_id == _selected_target_member_id:
			selected_index = index

	if _selected_target_member_id == &"" or not _has_target_member(_selected_target_member_id):
		_selected_target_member_id = _resolve_default_target_member_id()
		for index in range(target_members.size()):
			var member_data: Dictionary = target_members[index]
			if ProgressionDataUtils.to_string_name(member_data.get("member_id", "")) == _selected_target_member_id:
				selected_index = index
				break

	target_member_selector.select(selected_index)


func _refresh_details() -> void:
	var stack_entries := _get_stack_entries()
	if stack_entries.is_empty():
		item_icon.texture = null
		details_label.text = "仓库当前为空。"
		return

	var stack_data := _get_selected_stack_data()
	if stack_data.is_empty():
		item_icon.texture = null
		details_label.text = "请选择一个堆栈查看详情。"
		return

	item_icon.texture = _load_icon_texture(String(stack_data.get("icon", "")))
	var item_id := String(stack_data.get("item_id", ""))
	var total_quantity := int(stack_data.get("total_quantity", stack_data.get("quantity", 0)))
	var stack_limit := int(stack_data.get("stack_limit", 1))
	var lines := PackedStringArray([
		"物品：%s" % String(stack_data.get("display_name", item_id)),
		"物品 ID：%s" % item_id,
		"当前堆栈：%d" % int(stack_data.get("quantity", 0)),
		"同类总数：%d" % total_quantity,
		"堆叠规则：%s" % (
			"每堆上限 %d" % stack_limit
			if bool(stack_data.get("is_stackable", false))
			else "不可堆叠"
		),
		"说明：%s" % String(stack_data.get("description", "暂无说明。")),
	])
	if bool(stack_data.get("is_skill_book", false)):
		lines.append("技能书效果：使目标角色学会 %s。" % String(stack_data.get("granted_skill_name", stack_data.get("granted_skill_id", ""))))
		if _selected_target_member_id != &"":
			lines.append("当前目标：%s" % _get_target_member_display_name(_selected_target_member_id))
	details_label.text = "\n".join(lines)


func _refresh_controls() -> void:
	var has_selection := not _get_selected_stack_data().is_empty()
	var can_use_selected_item := _can_use_selected_item()
	discard_one_button.disabled = not has_selection
	discard_all_button.disabled = not has_selection
	target_member_label.visible = _selected_stack_is_skill_book()
	target_member_selector.visible = _selected_stack_is_skill_book()
	target_member_selector.disabled = not _selected_stack_is_skill_book() or _get_target_members().is_empty()
	use_button.visible = _selected_stack_is_skill_book()
	use_button.disabled = not can_use_selected_item


func _get_stack_entries() -> Array:
	var stacks_variant: Variant = _window_data.get("stacks", [])
	return stacks_variant if stacks_variant is Array else []


func _get_selected_stack_data() -> Dictionary:
	var stack_entries := _get_stack_entries()
	if _selected_stack_index < 0 or _selected_stack_index >= stack_entries.size():
		return {}
	var stack_data_variant = stack_entries[_selected_stack_index]
	return stack_data_variant if stack_data_variant is Dictionary else {}


func _get_target_members() -> Array:
	var target_members_variant: Variant = _window_data.get("target_members", [])
	return target_members_variant if target_members_variant is Array else []


func _resolve_default_target_member_id() -> StringName:
	var default_target_member_id := ProgressionDataUtils.to_string_name(_window_data.get("default_target_member_id", ""))
	if default_target_member_id != &"" and _has_target_member(default_target_member_id):
		return default_target_member_id
	var target_members := _get_target_members()
	if target_members.is_empty():
		return &""
	var first_member: Dictionary = target_members[0]
	return ProgressionDataUtils.to_string_name(first_member.get("member_id", ""))


func _has_target_member(member_id: StringName) -> bool:
	for member_data_variant in _get_target_members():
		var member_data: Dictionary = member_data_variant
		if ProgressionDataUtils.to_string_name(member_data.get("member_id", "")) == member_id:
			return true
	return false


func _get_target_member_display_name(member_id: StringName) -> String:
	for member_data_variant in _get_target_members():
		var member_data: Dictionary = member_data_variant
		if ProgressionDataUtils.to_string_name(member_data.get("member_id", "")) != member_id:
			continue
		return String(member_data.get("display_name", member_id))
	return String(member_id)


func _selected_stack_is_skill_book() -> bool:
	return bool(_get_selected_stack_data().get("is_skill_book", false))


func _can_use_selected_item() -> bool:
	return _selected_stack_is_skill_book() and _selected_target_member_id != &"" and _has_target_member(_selected_target_member_id)


func _load_icon_texture(icon_path: String) -> Texture2D:
	if icon_path.is_empty():
		return null
	var resource := load(icon_path)
	return resource as Texture2D if resource is Texture2D else null


func _on_stack_selected(index: int) -> void:
	_selected_stack_index = index
	var stack_data := _get_selected_stack_data()
	_selected_item_id = ProgressionDataUtils.to_string_name(stack_data.get("item_id", ""))
	_refresh_details()
	_refresh_controls()


func _on_target_member_selected(index: int) -> void:
	if index < 0 or index >= target_member_selector.item_count:
		_selected_target_member_id = &""
	else:
		var member_data = target_member_selector.get_item_metadata(index)
		if member_data is Dictionary:
			_selected_target_member_id = ProgressionDataUtils.to_string_name((member_data as Dictionary).get("member_id", ""))
		else:
			_selected_target_member_id = &""
	_refresh_details()
	_refresh_controls()


func _on_discard_one_button_pressed() -> void:
	if _selected_item_id == &"":
		return
	discard_one_requested.emit(_selected_item_id)


func _on_discard_all_button_pressed() -> void:
	if _selected_item_id == &"":
		return
	discard_all_requested.emit(_selected_item_id)


func _on_use_button_pressed() -> void:
	if not _can_use_selected_item():
		return
	use_requested.emit(_selected_item_id, _selected_target_member_id)


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
