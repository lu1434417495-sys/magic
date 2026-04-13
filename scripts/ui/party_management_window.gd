## 文件说明：该脚本属于队伍管理窗口相关的界面窗口脚本，集中维护遮罩、标题标签、元信息标签等顶层字段。
## 审查重点：重点核对字段含义、节点绑定、信号联动以及界面状态切换是否仍与对应场景保持一致。
## 备注：后续如果调整场景节点命名、层级或交互路径，需要同步检查成员字段与信号连接。

class_name PartyManagementWindow
extends Control

const EQUIPMENT_RULES_SCRIPT = preload("res://scripts/player/equipment/equipment_rules.gd")

## 信号说明：当界面请求队长变化时发出的信号，具体处理由外层系统或控制器负责。
signal leader_change_requested(member_id: StringName)
## 信号说明：当界面请求编队变化时发出的信号，具体处理由外层系统或控制器负责。
signal roster_change_requested(active_member_ids: Array[StringName], reserve_member_ids: Array[StringName])
## 信号说明：当界面请求仓库时发出的信号，具体处理由外层系统或控制器负责。
signal warehouse_requested
## 信号说明：当窗口或面板关闭时发出的信号，供外层恢复输入焦点、刷新数据或清理临时状态。
signal closed

const MAX_ACTIVE_MEMBER_COUNT := 4

## 字段说明：缓存遮罩节点，用于压暗背景并阻止底层界面继续接收输入。
@onready var shade: ColorRect = $Shade
## 字段说明：缓存标题标签节点，用于显示当前窗口或面板的主标题。
@onready var title_label: Label = $CenterContainer/Panel/MarginContainer/Content/Header/HeaderText/TitleLabel
## 字段说明：缓存副标题标签节点，用于显示补充状态、来源信息或筛选摘要。
@onready var meta_label: Label = $CenterContainer/Panel/MarginContainer/Content/Header/HeaderText/MetaLabel
## 字段说明：缓存激活列表节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var active_list: ItemList = $CenterContainer/Panel/MarginContainer/Content/Body/Lists/ActiveColumn/ActiveList
## 字段说明：缓存预备列表节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var reserve_list: ItemList = $CenterContainer/Panel/MarginContainer/Content/Body/Lists/ReserveColumn/ReserveList
## 字段说明：缓存集合队长按钮节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var set_leader_button: Button = $CenterContainer/Panel/MarginContainer/Content/Body/Controls/SetLeaderButton
## 字段说明：缓存移动激活按钮节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var move_to_active_button: Button = $CenterContainer/Panel/MarginContainer/Content/Body/Controls/MoveToActiveButton
## 字段说明：缓存移动预备按钮节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var move_to_reserve_button: Button = $CenterContainer/Panel/MarginContainer/Content/Body/Controls/MoveToReserveButton
## 字段说明：缓存仓库按钮节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var warehouse_button: Button = $CenterContainer/Panel/MarginContainer/Content/Body/Controls/WarehouseButton
## 字段说明：缓存详情标签节点，用于展示较长的说明文本或选中对象详情。
@onready var details_label: RichTextLabel = $CenterContainer/Panel/MarginContainer/Content/Body/DetailsColumn/DetailsLabel
## 字段说明：缓存状态提示标签节点，用于向玩家展示当前操作结果、错误原因或下一步引导。
@onready var status_label: Label = $CenterContainer/Panel/MarginContainer/Content/Body/DetailsColumn/StatusLabel
## 字段说明：缓存关闭按钮节点，供窗口统一执行收尾和关闭逻辑。
@onready var close_button: Button = $CenterContainer/Panel/MarginContainer/Content/Header/CloseButton

## 字段说明：缓存队伍状态实例，作为界面刷新、输入处理和窗口联动的重要依据。
var _party_state: PartyState = null
## 字段说明：缓存成就定义集合字典，集中保存可按键查询的运行时数据。
var _achievement_defs: Dictionary = {}
## 字段说明：缓存物品定义集合字典，集中保存可按键查询的运行时数据。
var _item_defs: Dictionary = {}
## 字段说明：缓存成员状态集合字典，集中保存可按键查询的运行时数据。
var _member_states: Dictionary = {}
## 字段说明：记录队长成员唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var _leader_member_id: StringName = &""
## 字段说明：保存激活成员标识列表，便于批量遍历、交叉查找和界面展示。
var _active_member_ids: Array[StringName] = []
## 字段说明：保存预备成员标识列表，便于批量遍历、交叉查找和界面展示。
var _reserve_member_ids: Array[StringName] = []
## 字段说明：记录已选成员唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var _selected_member_id: StringName = &""


func _ready() -> void:
	hide_window()
	shade.gui_input.connect(_on_shade_gui_input)
	close_button.pressed.connect(_close_window)
	active_list.item_selected.connect(_on_active_item_selected)
	reserve_list.item_selected.connect(_on_reserve_item_selected)
	set_leader_button.pressed.connect(_on_set_leader_button_pressed)
	move_to_active_button.pressed.connect(_on_move_to_active_button_pressed)
	move_to_reserve_button.pressed.connect(_on_move_to_reserve_button_pressed)
	warehouse_button.pressed.connect(_on_warehouse_button_pressed)


func show_party(party_state: PartyState) -> void:
	set_party_state(party_state)
	visible = true
	title_label.text = "队伍管理"
	meta_label.text = "队长必须属于上阵成员，上阵人数上限 %d。此处同时显示角色成就摘要，并可进入共享仓库。" % MAX_ACTIVE_MEMBER_COUNT
	refresh_view()


func set_achievement_defs(achievement_defs: Dictionary) -> void:
	_achievement_defs = achievement_defs if achievement_defs != null else {}
	if visible:
		refresh_view()


func set_item_defs(item_defs: Dictionary) -> void:
	_item_defs = item_defs if item_defs != null else {}
	if visible:
		refresh_view()


func set_party_state(party_state: PartyState) -> void:
	_capture_party_state(party_state)
	if visible:
		refresh_view()


func refresh_view() -> void:
	_rebuild_lists()
	_restore_selection()
	_refresh_controls()
	_refresh_details()


func get_party_state() -> PartyState:
	return _party_state


func get_selected_member_id() -> StringName:
	return _selected_member_id


func select_member(member_id: StringName) -> bool:
	if not _member_states.has(member_id):
		return false

	if _active_member_ids.has(member_id):
		_select_active_member_id(member_id)
	elif _reserve_member_ids.has(member_id):
		_select_reserve_member_id(member_id)
	else:
		return false

	_refresh_controls()
	_refresh_details()
	return true


func hide_window() -> void:
	visible = false
	_party_state = null
	_member_states.clear()
	_leader_member_id = &""
	_active_member_ids.clear()
	_reserve_member_ids.clear()
	_selected_member_id = &""
	if active_list != null:
		active_list.clear()
	if reserve_list != null:
		reserve_list.clear()
	if details_label != null:
		details_label.text = ""
	if status_label != null:
		status_label.text = ""


func _capture_party_state(party_state: PartyState) -> void:
	_party_state = party_state
	_member_states.clear()
	_active_member_ids.clear()
	_reserve_member_ids.clear()
	_selected_member_id = &""

	if party_state == null:
		_leader_member_id = &""
		return

	_leader_member_id = party_state.leader_member_id
	_active_member_ids = ProgressionDataUtils.to_string_name_array(party_state.active_member_ids)
	_reserve_member_ids = ProgressionDataUtils.to_string_name_array(party_state.reserve_member_ids)
	_member_states = party_state.member_states.duplicate()


func _rebuild_lists() -> void:
	active_list.clear()
	reserve_list.clear()

	for member_id in _active_member_ids:
		var member_state: PartyMemberState = _member_states.get(member_id)
		if member_state == null:
			continue
		var label: String = "%s%s" % [
			"队长 · " if member_id == _leader_member_id else "",
			_build_member_list_label(member_state),
		]
		active_list.add_item(label)
		active_list.set_item_metadata(active_list.item_count - 1, member_id)

	for member_id in _reserve_member_ids:
		var member_state: PartyMemberState = _member_states.get(member_id)
		if member_state == null:
			continue
		reserve_list.add_item(_build_member_list_label(member_state))
		reserve_list.set_item_metadata(reserve_list.item_count - 1, member_id)


func _build_member_list_label(member_state: PartyMemberState) -> String:
	return "%s  |  HP %d  MP %d" % [
		member_state.display_name,
		int(member_state.current_hp),
		int(member_state.current_mp),
	]


func _restore_selection() -> void:
	if _selected_member_id == &"":
		if not _active_member_ids.is_empty():
			_select_active_member_id(_active_member_ids[0])
		elif not _reserve_member_ids.is_empty():
			_select_reserve_member_id(_reserve_member_ids[0])
		return

	if _active_member_ids.has(_selected_member_id):
		_select_active_member_id(_selected_member_id)
	elif _reserve_member_ids.has(_selected_member_id):
		_select_reserve_member_id(_selected_member_id)


func _refresh_controls() -> void:
	var has_selection: bool = _selected_member_id != &""
	var can_set_leader: bool = has_selection and _active_member_ids.has(_selected_member_id)
	var can_move_to_active: bool = has_selection and _reserve_member_ids.has(_selected_member_id) and _active_member_ids.size() < MAX_ACTIVE_MEMBER_COUNT
	var can_move_to_reserve: bool = has_selection and _active_member_ids.has(_selected_member_id) and _active_member_ids.size() > 1

	set_leader_button.disabled = not can_set_leader
	move_to_active_button.disabled = not can_move_to_active
	move_to_reserve_button.disabled = not can_move_to_reserve
	warehouse_button.disabled = _party_state == null


func _refresh_details() -> void:
	if _member_states.is_empty():
		details_label.text = "当前没有队伍成员数据。"
		status_label.text = "上阵 %d / %d  |  替补 %d" % [
			_active_member_ids.size(),
			MAX_ACTIVE_MEMBER_COUNT,
			_reserve_member_ids.size(),
		]
		return

	if _selected_member_id == &"":
		details_label.text = "请选择一名成员查看详情。"
		status_label.text = ""
		return

	var member_state: PartyMemberState = _member_states.get(_selected_member_id)
	if member_state == null:
		details_label.text = "当前成员数据不可用。"
		status_label.text = ""
		return

	var progression: UnitProgress = member_state.progression
	var profession_lines: PackedStringArray = []
	if progression != null:
		for profession_id in ProgressionDataUtils.sorted_string_keys(progression.professions):
			var profession_progress: UnitProfessionProgress = progression.get_profession_progress(StringName(profession_id))
			if profession_progress == null:
				continue
			profession_lines.append("%s Rank %d" % [profession_id, int(profession_progress.rank)])

	var skill_lines: PackedStringArray = []
	if progression != null:
		for skill_id in ProgressionDataUtils.sorted_string_keys(progression.skills):
			var skill_progress: UnitSkillProgress = progression.get_skill_progress(StringName(skill_id))
			if skill_progress == null or not skill_progress.is_learned:
				continue
			skill_lines.append("%s Lv.%d" % [skill_id, int(skill_progress.skill_level)])

	var equipment_lines := _build_equipment_summary_lines(member_state)
	var equipment_count: int = 0
	for line in equipment_lines:
		if line == "暂无":
			continue
		equipment_count += 1

	var achievement_lines := _build_achievement_summary_lines(progression)
	var detail_lines := PackedStringArray([
		"姓名：%s" % member_state.display_name,
		"成员 ID：%s" % String(member_state.member_id),
		"编成：%s" % ("上阵" if _active_member_ids.has(member_state.member_id) else "替补"),
		"队长：%s" % ("是" if member_state.member_id == _leader_member_id else "否"),
		"控制：%s" % String(member_state.control_mode),
		"角色等级：%d" % int(progression.character_level if progression != null else 0),
		"当前资源：HP %d / MP %d" % [int(member_state.current_hp), int(member_state.current_mp)],
		"装备条目：%d" % equipment_count,
		"装备：",
	])
	detail_lines.append_array(equipment_lines)
	detail_lines.append_array(PackedStringArray([
		"职业：%s" % (", ".join(profession_lines) if not profession_lines.is_empty() else "暂无"),
		"技能：%s" % (", ".join(skill_lines) if not skill_lines.is_empty() else "暂无"),
		"",
		"成就摘要：",
	]))
	detail_lines.append_array(achievement_lines)
	details_label.text = "\n".join(detail_lines)
	status_label.text = "当前队长：%s  |  上阵 %d / %d  |  替补 %d" % [
		String(_leader_member_id),
		_active_member_ids.size(),
		MAX_ACTIVE_MEMBER_COUNT,
		_reserve_member_ids.size(),
	]


func _build_achievement_summary_lines(progression: UnitProgress) -> PackedStringArray:
	var lines: PackedStringArray = []
	if progression == null:
		lines.append("暂无成就数据。")
		return lines

	var unlocked_count := 0
	var in_progress_entries: Array[Dictionary] = []
	var recent_unlocked_name := ""
	var recent_unlocked_time := 0

	for achievement_key in ProgressionDataUtils.sorted_string_keys(_achievement_defs):
		var achievement_id := StringName(achievement_key)
		var achievement_def = _achievement_defs.get(achievement_id)
		if achievement_def == null:
			continue

		var progress_state = progression.get_achievement_progress_state(achievement_id)
		if progress_state != null and progress_state.is_unlocked:
			unlocked_count += 1
			var unlocked_at := int(progress_state.unlocked_at_unix_time)
			if unlocked_at >= recent_unlocked_time:
				recent_unlocked_time = unlocked_at
				recent_unlocked_name = achievement_def.display_name
			continue

		var current_value := int(progress_state.current_value) if progress_state != null else 0
		if current_value <= 0:
			continue
		in_progress_entries.append({
			"display_name": achievement_def.display_name,
			"current_value": current_value,
			"threshold": int(achievement_def.threshold),
			"progress_ratio": float(current_value) / float(maxi(int(achievement_def.threshold), 1)),
		})

	in_progress_entries.sort_custom(func(a: Dictionary, b: Dictionary) -> bool:
		var ratio_a := float(a.get("progress_ratio", 0.0))
		var ratio_b := float(b.get("progress_ratio", 0.0))
		if ratio_a == ratio_b:
			return String(a.get("display_name", "")) < String(b.get("display_name", ""))
		return ratio_a > ratio_b
	)

	lines.append("已解锁：%d" % unlocked_count)
	lines.append("进行中：%d" % in_progress_entries.size())
	lines.append("最近解锁：%s" % (recent_unlocked_name if not recent_unlocked_name.is_empty() else "暂无"))
	if in_progress_entries.is_empty():
		lines.append("当前进行中条目：暂无")
		return lines

	lines.append("当前进行中条目：")
	var preview_count := mini(in_progress_entries.size(), 3)
	for index in range(preview_count):
		var entry: Dictionary = in_progress_entries[index]
		lines.append(
			"- %s %d / %d" % [
				String(entry.get("display_name", "")),
				int(entry.get("current_value", 0)),
				int(entry.get("threshold", 0)),
			]
		)
	return lines


func _build_equipment_summary_lines(member_state: PartyMemberState) -> PackedStringArray:
	var lines: PackedStringArray = []
	if member_state == null:
		lines.append("暂无")
		return lines

	var equipment_state = member_state.equipment_state
	var filled_count := 0
	for slot_id in EQUIPMENT_RULES_SCRIPT.get_all_slot_ids():
		var item_id: StringName = &""
		if equipment_state is Object and equipment_state.has_method("get_equipped_item_id"):
			item_id = equipment_state.get_equipped_item_id(slot_id)
		elif equipment_state is Dictionary:
			item_id = ProgressionDataUtils.to_string_name(equipment_state.get(slot_id, equipment_state.get(String(slot_id), "")))
		if item_id == &"":
			continue
		filled_count += 1
		var item_def = _item_defs.get(item_id)
		var item_name: String = item_def.display_name if item_def != null and not item_def.display_name.is_empty() else String(item_id)
		lines.append("- %s：%s" % [EQUIPMENT_RULES_SCRIPT.get_slot_label(slot_id), item_name])

	if filled_count <= 0:
		lines.append("暂无")
	return lines


func _select_active_member_id(member_id: StringName) -> void:
	_selected_member_id = member_id
	reserve_list.deselect_all()
	for index in range(active_list.item_count):
		if active_list.get_item_metadata(index) == member_id:
			active_list.select(index)
			break


func _select_reserve_member_id(member_id: StringName) -> void:
	_selected_member_id = member_id
	active_list.deselect_all()
	for index in range(reserve_list.item_count):
		if reserve_list.get_item_metadata(index) == member_id:
			reserve_list.select(index)
			break


func _on_active_item_selected(index: int) -> void:
	var member_id_variant: Variant = active_list.get_item_metadata(index)
	if member_id_variant == null:
		return
	var member_id: StringName = member_id_variant
	_select_active_member_id(member_id)
	_refresh_controls()
	_refresh_details()


func _on_reserve_item_selected(index: int) -> void:
	var member_id_variant: Variant = reserve_list.get_item_metadata(index)
	if member_id_variant == null:
		return
	var member_id: StringName = member_id_variant
	_select_reserve_member_id(member_id)
	_refresh_controls()
	_refresh_details()


func _on_set_leader_button_pressed() -> void:
	if _selected_member_id == &"":
		return
	if not _active_member_ids.has(_selected_member_id):
		return
	_leader_member_id = _selected_member_id
	_rebuild_lists()
	_restore_selection()
	_refresh_details()
	leader_change_requested.emit(_leader_member_id)


func _on_move_to_active_button_pressed() -> void:
	if _selected_member_id == &"":
		return
	if not _reserve_member_ids.has(_selected_member_id):
		return
	if _active_member_ids.size() >= MAX_ACTIVE_MEMBER_COUNT:
		return

	_reserve_member_ids.erase(_selected_member_id)
	_active_member_ids.append(_selected_member_id)
	if _leader_member_id == &"":
		_leader_member_id = _selected_member_id
		leader_change_requested.emit(_leader_member_id)
	_rebuild_lists()
	_select_active_member_id(_selected_member_id)
	_refresh_controls()
	_refresh_details()
	_emit_roster_change()


func _on_move_to_reserve_button_pressed() -> void:
	if _selected_member_id == &"":
		return
	if not _active_member_ids.has(_selected_member_id):
		return
	if _active_member_ids.size() <= 1:
		return

	_active_member_ids.erase(_selected_member_id)
	_reserve_member_ids.append(_selected_member_id)
	if _leader_member_id == _selected_member_id:
		_leader_member_id = _active_member_ids[0]
		leader_change_requested.emit(_leader_member_id)
	_rebuild_lists()
	_select_reserve_member_id(_selected_member_id)
	_refresh_controls()
	_refresh_details()
	_emit_roster_change()


func _emit_roster_change() -> void:
	roster_change_requested.emit(_active_member_ids.duplicate(), _reserve_member_ids.duplicate())


func _close_window() -> void:
	if not visible:
		return
	hide_window()
	closed.emit()


func _on_warehouse_button_pressed() -> void:
	if _party_state == null:
		return
	warehouse_requested.emit()


func _on_shade_gui_input(event: InputEvent) -> void:
	if event is not InputEventMouseButton:
		return
	var mouse_event: InputEventMouseButton = event as InputEventMouseButton
	if not mouse_event.pressed:
		return
	if mouse_event.button_index != MOUSE_BUTTON_LEFT and mouse_event.button_index != MOUSE_BUTTON_RIGHT:
		return
	_close_window()
