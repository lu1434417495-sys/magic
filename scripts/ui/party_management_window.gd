## 文件说明：该脚本属于队伍管理窗口相关的界面窗口脚本，集中维护遮罩、标题标签、元信息标签等顶层字段。
## 审查重点：重点核对字段含义、节点绑定、信号联动以及界面状态切换是否仍与对应场景保持一致。
## 备注：后续如果调整场景节点命名、层级或交互路径，需要同步检查成员字段与信号连接。

class_name PartyManagementWindow
extends Control

const EQUIPMENT_RULES_SCRIPT = preload("res://scripts/player/equipment/equipment_rules.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")

## 信号说明：当界面请求队长变化时发出的信号，具体处理由外层系统或控制器负责。
signal leader_change_requested(member_id: StringName)
## 信号说明：当界面请求编队变化时发出的信号，具体处理由外层系统或控制器负责。
signal roster_change_requested(active_member_ids: Array[StringName], reserve_member_ids: Array[StringName])
## 信号说明：当界面请求仓库时发出的信号，具体处理由外层系统或控制器负责。
signal warehouse_requested
## 信号说明：当窗口或面板关闭时发出的信号，供外层恢复输入焦点、刷新数据或清理临时状态。
signal closed

const MAX_ACTIVE_MEMBER_COUNT := 4
const DESIGN_PANEL_SIZE := Vector2(1380.0, 780.0)
const MIN_PANEL_SIZE := Vector2(820.0, 540.0)
const VIEWPORT_SAFE_MARGIN := Vector2(48.0, 30.0)
const DESIGN_CONTENT_MARGIN_LEFT := 148
const DESIGN_CONTENT_MARGIN_TOP := 118
const DESIGN_CONTENT_MARGIN_RIGHT := 148
const DESIGN_CONTENT_MARGIN_BOTTOM := 112

## 字段说明：缓存遮罩节点，用于压暗背景并阻止底层界面继续接收输入。
@onready var shade: ColorRect = $Shade
## 字段说明：缓存主窗口节点，用于根据视口大小调整窗口尺寸。
@onready var panel: Control = %Panel
## 字段说明：缓存内容边距节点，用于配合九宫格外框调整安全内容区。
@onready var content_margin: MarginContainer = %MarginContainer
## 字段说明：缓存标题标签节点，用于显示当前窗口或面板的主标题。
@onready var title_label: Label = %TitleLabel
## 字段说明：缓存副标题标签节点，用于显示补充状态、来源信息或筛选摘要。
@onready var meta_label: Label = %MetaLabel
## 字段说明：缓存激活列表节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var active_list: ItemList = %ActiveList
## 字段说明：缓存预备列表节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var reserve_list: ItemList = %ReserveList
## 字段说明：缓存成员列表栏节点，用于随窗口宽度收缩。
@onready var lists_column: Control = %Lists
## 字段说明：缓存集合队长按钮节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var set_leader_button: Button = %SetLeaderButton
## 字段说明：缓存操作按钮栏节点，用于随窗口宽度收缩。
@onready var controls_column: Control = %Controls
## 字段说明：缓存移动激活按钮节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var move_to_active_button: Button = %MoveToActiveButton
## 字段说明：缓存移动预备按钮节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var move_to_reserve_button: Button = %MoveToReserveButton
## 字段说明：缓存仓库按钮节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var warehouse_button: Button = %WarehouseButton
## 字段说明：缓存角色概览栏节点，用于随窗口宽度收缩。
@onready var overview_column: Control = %OverviewColumn
## 字段说明：缓存角色概览标签节点，用于展示选中成员的身份、资源和主要属性。
@onready var overview_label: RichTextLabel = %OverviewLabel
## 字段说明：缓存属性页文本节点，用于展示最终属性快照。
@onready var attributes_label: RichTextLabel = %AttributesLabel
## 字段说明：缓存装备页文本节点，用于展示成员装备槽与装备说明。
@onready var equipment_label: RichTextLabel = %EquipmentLabel
## 字段说明：缓存技能页文本节点，用于展示已学技能、等级、来源与描述。
@onready var skills_label: RichTextLabel = %SkillsLabel
## 字段说明：缓存职业页文本节点，用于展示职业阶位、激活状态与授予技能。
@onready var professions_label: RichTextLabel = %ProfessionsLabel
## 字段说明：缓存状态提示标签节点，用于向玩家展示当前操作结果、错误原因或下一步引导。
@onready var status_label: Label = %StatusLabel
## 字段说明：缓存关闭按钮节点，供窗口统一执行收尾和关闭逻辑。
@onready var close_button: Button = %CloseButton

## 字段说明：缓存队伍状态实例，作为界面刷新、输入处理和窗口联动的重要依据。
var _party_state: PartyState = null
## 字段说明：缓存成就定义集合字典，集中保存可按键查询的运行时数据。
var _achievement_defs: Dictionary = {}
## 字段说明：缓存物品定义集合字典，集中保存可按键查询的运行时数据。
var _item_defs: Dictionary = {}
## 字段说明：缓存技能定义集合字典，用于把技能进度翻译成玩家可读的名称和说明。
var _skill_defs: Dictionary = {}
## 字段说明：缓存职业定义集合字典，用于把职业进度翻译成玩家可读的名称和说明。
var _profession_defs: Dictionary = {}
## 字段说明：缓存成员状态集合字典，集中保存可按键查询的运行时数据。
var _member_states: Dictionary = {}
## 字段说明：记录队长成员唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var _leader_member_id: StringName = &""
## 字段说明：记录主角成员唯一标识，用于维持“主角必须保持上阵”的正式编队约束。
var _main_character_member_id: StringName = &""
## 字段说明：保存激活成员标识列表，便于批量遍历、交叉查找和界面展示。
var _active_member_ids: Array[StringName] = []
## 字段说明：保存预备成员标识列表，便于批量遍历、交叉查找和界面展示。
var _reserve_member_ids: Array[StringName] = []
## 字段说明：记录已选成员唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var _selected_member_id: StringName = &""


func _ready() -> void:
	hide_window()
	resized.connect(_update_responsive_layout)
	_update_responsive_layout()
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
	_update_responsive_layout()
	title_label.text = "人物管理"
	meta_label.text = "查看成员属性、装备、技能和职业；上阵人数上限 %d，主角必须保持上阵。" % MAX_ACTIVE_MEMBER_COUNT
	refresh_view()


func set_achievement_defs(achievement_defs: Dictionary) -> void:
	_achievement_defs = achievement_defs if achievement_defs != null else {}
	if visible:
		refresh_view()


func set_item_defs(item_defs: Dictionary) -> void:
	_item_defs = item_defs if item_defs != null else {}
	if visible:
		refresh_view()


func set_skill_defs(skill_defs: Dictionary) -> void:
	_skill_defs = skill_defs if skill_defs != null else {}
	if visible:
		refresh_view()


func set_profession_defs(profession_defs: Dictionary) -> void:
	_profession_defs = profession_defs if profession_defs != null else {}
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
	_main_character_member_id = &""
	_active_member_ids.clear()
	_reserve_member_ids.clear()
	_selected_member_id = &""
	if active_list != null:
		active_list.clear()
	if reserve_list != null:
		reserve_list.clear()
	_clear_detail_labels()
	if status_label != null:
		status_label.text = ""


func _update_responsive_layout() -> void:
	if panel == null or content_margin == null:
		return

	var viewport_size := size
	if viewport_size.x <= 0.0 or viewport_size.y <= 0.0:
		viewport_size = get_viewport_rect().size
	if viewport_size.x <= 0.0 or viewport_size.y <= 0.0:
		return

	var available_size := Vector2(
		maxf(viewport_size.x - VIEWPORT_SAFE_MARGIN.x * 2.0, 320.0),
		maxf(viewport_size.y - VIEWPORT_SAFE_MARGIN.y * 2.0, 320.0)
	)
	var panel_size := Vector2(
		minf(DESIGN_PANEL_SIZE.x, available_size.x),
		minf(DESIGN_PANEL_SIZE.y, available_size.y)
	)
	panel_size.x = maxf(panel_size.x, minf(MIN_PANEL_SIZE.x, available_size.x))
	panel_size.y = maxf(panel_size.y, minf(MIN_PANEL_SIZE.y, available_size.y))
	panel.custom_minimum_size = panel_size

	var layout_scale := clampf(
		minf(panel_size.x / DESIGN_PANEL_SIZE.x, panel_size.y / DESIGN_PANEL_SIZE.y),
		0.72,
		1.0
	)
	var margin_left := maxi(int(round(float(DESIGN_CONTENT_MARGIN_LEFT) * layout_scale)), 84)
	var margin_top := maxi(int(round(float(DESIGN_CONTENT_MARGIN_TOP) * layout_scale)), 70)
	var margin_right := maxi(int(round(float(DESIGN_CONTENT_MARGIN_RIGHT) * layout_scale)), 84)
	var margin_bottom := maxi(int(round(float(DESIGN_CONTENT_MARGIN_BOTTOM) * layout_scale)), 66)
	content_margin.add_theme_constant_override("margin_left", margin_left)
	content_margin.add_theme_constant_override("margin_top", margin_top)
	content_margin.add_theme_constant_override("margin_right", margin_right)
	content_margin.add_theme_constant_override("margin_bottom", margin_bottom)

	var content_width := maxf(panel_size.x - float(margin_left + margin_right), 320.0)
	var list_width := clampf(300.0 * layout_scale, 210.0, 300.0)
	var controls_width := clampf(136.0 * layout_scale, 116.0, 136.0)
	var overview_width := clampf(250.0 * layout_scale, 180.0, 250.0)
	if content_width < 760.0:
		list_width = 200.0
		controls_width = 112.0
		overview_width = 170.0
	lists_column.custom_minimum_size.x = list_width
	controls_column.custom_minimum_size.x = controls_width
	overview_column.custom_minimum_size.x = overview_width
	for button in [set_leader_button, move_to_active_button, move_to_reserve_button, warehouse_button]:
		if button != null:
			button.custom_minimum_size.x = controls_width

	var text_height := maxf(panel_size.y - float(margin_top + margin_bottom) - 168.0, 260.0)
	overview_label.custom_minimum_size.y = text_height
	for label in [attributes_label, equipment_label, skills_label, professions_label]:
		if label != null:
			label.custom_minimum_size.y = text_height
	active_list.custom_minimum_size.y = maxf(145.0, text_height * 0.46)
	reserve_list.custom_minimum_size.y = maxf(130.0, text_height * 0.40)


func _capture_party_state(party_state: PartyState) -> void:
	_party_state = party_state
	_member_states.clear()
	_active_member_ids.clear()
	_reserve_member_ids.clear()
	_selected_member_id = &""

	if party_state == null:
		_leader_member_id = &""
		_main_character_member_id = &""
		return

	_leader_member_id = party_state.leader_member_id
	_main_character_member_id = party_state.get_resolved_main_character_member_id() if party_state.has_method("get_resolved_main_character_member_id") else &""
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
	var can_move_to_reserve: bool = (
		has_selection
		and _active_member_ids.has(_selected_member_id)
		and _active_member_ids.size() > 1
		and _selected_member_id != _main_character_member_id
	)

	set_leader_button.disabled = not can_set_leader
	move_to_active_button.disabled = not can_move_to_active
	move_to_reserve_button.disabled = not can_move_to_reserve
	warehouse_button.disabled = _party_state == null


func _refresh_details() -> void:
	if _member_states.is_empty():
		overview_label.text = "当前没有队伍成员数据。"
		_clear_detail_tabs()
		status_label.text = "上阵 %d / %d  |  替补 %d" % [
			_active_member_ids.size(),
			MAX_ACTIVE_MEMBER_COUNT,
			_reserve_member_ids.size(),
		]
		return

	if _selected_member_id == &"":
		overview_label.text = "请选择一名成员查看详情。"
		_clear_detail_tabs()
		status_label.text = ""
		return

	var member_state: PartyMemberState = _member_states.get(_selected_member_id)
	if member_state == null:
		overview_label.text = "当前成员数据不可用。"
		_clear_detail_tabs()
		status_label.text = ""
		return

	var snapshot = _build_attribute_snapshot(member_state)
	overview_label.text = _build_overview_text(member_state, snapshot)
	attributes_label.text = _build_attributes_text(member_state, snapshot)
	equipment_label.text = "\n".join(_build_equipment_detail_lines(member_state))
	skills_label.text = "\n".join(_build_skill_detail_lines(member_state.progression))
	professions_label.text = "\n".join(_build_profession_detail_lines(member_state.progression))
	status_label.text = "当前队长：%s  |  上阵 %d / %d  |  替补 %d" % [
		String(_leader_member_id),
		_active_member_ids.size(),
		MAX_ACTIVE_MEMBER_COUNT,
		_reserve_member_ids.size(),
	]


func _build_attribute_snapshot(member_state: PartyMemberState):
	if member_state == null or member_state.progression == null:
		return null
	var attribute_service = ATTRIBUTE_SERVICE_SCRIPT.new()
	attribute_service.setup(member_state.progression, _skill_defs, _profession_defs, member_state.equipment_state)
	return attribute_service.get_snapshot()


func _build_overview_text(member_state: PartyMemberState, snapshot) -> String:
	var progression: UnitProgress = member_state.progression
	var lines := PackedStringArray([
		"姓名：%s" % member_state.display_name,
		"成员 ID：%s" % String(member_state.member_id),
		"编成：%s" % ("上阵" if _active_member_ids.has(member_state.member_id) else "替补"),
		"主角：%s" % ("是" if member_state.member_id == _main_character_member_id else "否"),
		"队长：%s" % ("是" if member_state.member_id == _leader_member_id else "否"),
		"身份：%s%s" % [
			"主角 " if member_state.member_id == _main_character_member_id else "",
			"队长" if member_state.member_id == _leader_member_id else "",
		],
		"控制：%s" % String(member_state.control_mode),
		"等级：%d" % int(progression.character_level if progression != null else 0),
		"当前资源：HP %d / %d  MP %d / %d" % [
			int(member_state.current_hp),
			_get_snapshot_value(snapshot, ATTRIBUTE_SERVICE_SCRIPT.HP_MAX),
			int(member_state.current_mp),
			_get_snapshot_value(snapshot, ATTRIBUTE_SERVICE_SCRIPT.MP_MAX),
		],
		"体型：%d" % int(member_state.body_size),
		"",
		"核心属性：",
	])
	for attribute_id in UnitBaseAttributes.BASE_ATTRIBUTE_IDS:
		lines.append("- %s：%d" % [_get_attribute_label(attribute_id), _get_snapshot_value(snapshot, attribute_id)])
	lines.append("")
	lines.append("成就摘要：")
	lines.append_array(_build_achievement_summary_lines(progression))
	return "\n".join(lines)


func _build_attributes_text(member_state: PartyMemberState, snapshot) -> String:
	if member_state == null:
		return "当前成员数据不可用。"
	var lines := PackedStringArray(["基础属性："])
	for attribute_id in UnitBaseAttributes.BASE_ATTRIBUTE_IDS:
		lines.append("- %s：%d" % [_get_attribute_label(attribute_id), _get_snapshot_value(snapshot, attribute_id)])
	lines.append("")
	lines.append("资源属性：")
	for attribute_id in ATTRIBUTE_SERVICE_SCRIPT.RESOURCE_ATTRIBUTE_IDS:
		lines.append("- %s：%d" % [_get_attribute_label(attribute_id), _get_snapshot_value(snapshot, attribute_id)])
	lines.append("")
	lines.append("战斗属性：")
	for attribute_id in ATTRIBUTE_SERVICE_SCRIPT.COMBAT_ATTRIBUTE_IDS:
		lines.append("- %s：%d" % [_get_attribute_label(attribute_id), _get_snapshot_value(snapshot, attribute_id)])
	lines.append("")
	lines.append("命运：")
	lines.append("- 出生隐藏幸运：%d" % int(member_state.get_hidden_luck_at_birth()))
	lines.append("- 信仰幸运加值：%d" % int(member_state.get_faith_luck_bonus()))
	lines.append("- 有效幸运：%d" % int(member_state.get_effective_luck()))
	lines.append("- 战斗幸运：%d" % int(member_state.get_combat_luck_score()))
	lines.append("- 掉落幸运：%d" % int(member_state.get_drop_luck()))
	return "\n".join(lines)


func _build_equipment_detail_lines(member_state: PartyMemberState) -> PackedStringArray:
	var lines := PackedStringArray()
	if member_state == null:
		lines.append("当前成员数据不可用。")
		return lines

	var equipment_state = member_state.equipment_state
	var filled_count := 0
	for slot_id in EQUIPMENT_RULES_SCRIPT.get_all_slot_ids():
		var item_id := _get_equipped_item_id(equipment_state, slot_id)
		if item_id == &"":
			lines.append("%s：空" % EQUIPMENT_RULES_SCRIPT.get_slot_label(slot_id))
			continue
		filled_count += 1
		var item_def = _item_defs.get(item_id)
		var item_name := _get_item_display_name(item_id)
		lines.append("%s：%s" % [EQUIPMENT_RULES_SCRIPT.get_slot_label(slot_id), item_name])
		if item_def != null:
			var type_label := _get_equipment_type_label(item_def.get_equipment_type_id_normalized() if item_def.has_method("get_equipment_type_id_normalized") else &"")
			if not type_label.is_empty():
				lines.append("  类型：%s" % type_label)
			var modifier_lines := _build_modifier_lines(item_def.attribute_modifiers)
			if not modifier_lines.is_empty():
				lines.append("  属性：%s" % "，".join(modifier_lines))
			if not item_def.description.is_empty():
				lines.append("  说明：%s" % item_def.description)
	lines.insert(0, "已装备：%d" % filled_count)
	return lines


func _build_skill_detail_lines(progression: UnitProgress) -> PackedStringArray:
	var lines := PackedStringArray()
	if progression == null:
		lines.append("暂无技能数据。")
		return lines

	var learned_count := 0
	for skill_id_text in ProgressionDataUtils.sorted_string_keys(progression.skills):
		var skill_id := StringName(skill_id_text)
		var skill_progress: UnitSkillProgress = progression.get_skill_progress(skill_id)
		if skill_progress == null or not skill_progress.is_learned:
			continue
		learned_count += 1
		var skill_def = _skill_defs.get(skill_id)
		var tags := PackedStringArray()
		if skill_progress.is_core:
			tags.append("核心")
		if skill_progress.profession_granted_by != &"":
			tags.append("职业授予：%s" % _get_profession_display_name(skill_progress.profession_granted_by))
		elif skill_progress.assigned_profession_id != &"":
			tags.append("指派：%s" % _get_profession_display_name(skill_progress.assigned_profession_id))
		var type_label := _get_skill_type_label(skill_def.skill_type if skill_def != null else &"")
		lines.append("%s  Lv.%d%s" % [
			_get_skill_display_name(skill_id),
			int(skill_progress.skill_level),
			"  |  %s" % "，".join(tags) if not tags.is_empty() else "",
		])
		if not type_label.is_empty():
			lines.append("  类型：%s" % type_label)
		lines.append("  熟练度：%d  总获得：%d" % [int(skill_progress.current_mastery), int(skill_progress.total_mastery_earned)])
		if skill_def != null and not skill_def.description.is_empty():
			lines.append("  说明：%s" % skill_def.description)
		var current_level := int(skill_progress.skill_level)
		var level_desc := skill_def.level_descriptions.get(str(current_level), "") as String
		if not level_desc.is_empty():
			lines.append("  当前效果：%s" % level_desc)
		var preview_lines := _build_level_override_preview(skill_def, current_level)
		for preview_line in preview_lines:
			lines.append(preview_line)
		if not skill_progress.merged_from_skill_ids.is_empty():
			lines.append("  来源：%s" % _format_skill_id_list(skill_progress.merged_from_skill_ids))
	if learned_count <= 0:
		lines.append("暂无已学技能。")
	else:
		lines.insert(0, "已学技能：%d" % learned_count)
	return lines


func _build_level_override_preview(skill_def, skill_level: int) -> PackedStringArray:
	var lines := PackedStringArray()
	if skill_def == null or skill_def.combat_profile == null:
		return lines
	var overrides: Dictionary = skill_def.combat_profile.level_overrides
	if overrides.is_empty():
		return lines
	var next_levels := PackedStringArray()
	for level_key in overrides.keys():
		var level := int(level_key)
		if level > skill_level:
			var data := overrides[level_key] as Dictionary
			var parts := PackedStringArray()
			for cost_key in ["ap_cost", "mp_cost", "stamina_cost", "aura_cost", "cooldown_tu"]:
				if data.has(cost_key):
					var label := ""
					match cost_key:
						"ap_cost": label = "AP"
						"mp_cost": label = "MP"
						"stamina_cost": label = "体力"
						"aura_cost": label = "斗气"
						"cooldown_tu": label = "冷却"
					parts.append("%s→%d" % [label, int(data[cost_key])])
			if not parts.is_empty():
				next_levels.append("Lv.%d：%s" % [level, "，".join(parts)])
	if not next_levels.is_empty():
		lines.append("  升级预览：%s" % "；".join(next_levels))
	return lines

func _build_profession_detail_lines(progression: UnitProgress) -> PackedStringArray:
	var lines := PackedStringArray()
	if progression == null:
		lines.append("暂无职业数据。")
		return lines

	var profession_count := 0
	for profession_id_text in ProgressionDataUtils.sorted_string_keys(progression.professions):
		var profession_id := StringName(profession_id_text)
		var profession_progress: UnitProfessionProgress = progression.get_profession_progress(profession_id)
		if profession_progress == null or profession_progress.is_hidden:
			continue
		profession_count += 1
		var profession_def = _profession_defs.get(profession_id)
		lines.append("%s  Rank %d%s" % [
			_get_profession_display_name(profession_id),
			int(profession_progress.rank),
			"  |  激活" if profession_progress.is_active else "  |  未激活",
		])
		if profession_progress.inactive_reason != &"":
			lines.append("  原因：%s" % String(profession_progress.inactive_reason))
		if profession_def != null and not profession_def.description.is_empty():
			lines.append("  说明：%s" % profession_def.description)
		var modifier_lines := _build_modifier_lines(profession_def.attribute_modifiers if profession_def != null else [], int(profession_progress.rank))
		if not modifier_lines.is_empty():
			lines.append("  属性修正：%s" % "，".join(modifier_lines))
		if not profession_progress.core_skill_ids.is_empty():
			lines.append("  核心技能：%s" % _format_skill_id_list(profession_progress.core_skill_ids))
		if not profession_progress.granted_skill_ids.is_empty():
			lines.append("  授予技能：%s" % _format_skill_id_list(profession_progress.granted_skill_ids))
	if profession_count <= 0:
		lines.append("暂无职业。")
	else:
		lines.insert(0, "职业：%d" % profession_count)
	return lines


func _clear_detail_labels() -> void:
	if overview_label != null:
		overview_label.text = ""
	_clear_detail_tabs()


func _clear_detail_tabs() -> void:
	if attributes_label != null:
		attributes_label.text = ""
	if equipment_label != null:
		equipment_label.text = ""
	if skills_label != null:
		skills_label.text = ""
	if professions_label != null:
		professions_label.text = ""


func _get_snapshot_value(snapshot, attribute_id: StringName) -> int:
	if snapshot == null or not snapshot.has_method("get_value"):
		return 0
	return int(snapshot.get_value(attribute_id))


func _get_equipped_item_id(equipment_state, slot_id: StringName) -> StringName:
	if equipment_state is Object and equipment_state.has_method("get_equipped_item_id"):
		return equipment_state.get_equipped_item_id(slot_id)
	if equipment_state is Dictionary:
		return ProgressionDataUtils.to_string_name(equipment_state.get(slot_id, equipment_state.get(String(slot_id), "")))
	return &""


func _get_item_display_name(item_id: StringName) -> String:
	var item_def = _item_defs.get(item_id)
	if item_def != null and not item_def.display_name.is_empty():
		return item_def.display_name
	return String(item_id)


func _get_skill_display_name(skill_id: StringName) -> String:
	var skill_def = _skill_defs.get(skill_id)
	if skill_def != null and not skill_def.display_name.is_empty():
		return skill_def.display_name
	return String(skill_id)


func _get_profession_display_name(profession_id: StringName) -> String:
	var profession_def = _profession_defs.get(profession_id)
	if profession_def != null and not profession_def.display_name.is_empty():
		return profession_def.display_name
	return String(profession_id)


func _format_skill_id_list(skill_ids: Array[StringName]) -> String:
	var labels := PackedStringArray()
	for skill_id in skill_ids:
		labels.append(_get_skill_display_name(skill_id))
	return "，".join(labels)


func _build_modifier_lines(modifiers: Array, rank: int = 1) -> PackedStringArray:
	var lines := PackedStringArray()
	for modifier in modifiers:
		if modifier == null:
			continue
		var attribute_id: StringName = modifier.attribute_id
		var value := int(modifier.get_value_for_rank(rank)) if modifier.has_method("get_value_for_rank") else int(modifier.value)
		if attribute_id == &"" or value == 0:
			continue
		lines.append("%s %+d" % [_get_attribute_label(attribute_id), value])
	return lines


func _get_equipment_type_label(equipment_type_id: StringName) -> String:
	match equipment_type_id:
		&"weapon":
			return "武器"
		&"armor":
			return "防具"
		&"accessory":
			return "饰品"
		_:
			return ""


func _get_skill_type_label(skill_type: StringName) -> String:
	match skill_type:
		&"active":
			return "主动"
		&"passive":
			return "被动"
		&"combat":
			return "战斗"
		_:
			return String(skill_type) if skill_type != &"" else ""


func _get_attribute_label(attribute_id: StringName) -> String:
	match attribute_id:
		UnitBaseAttributes.STRENGTH:
			return "力量"
		UnitBaseAttributes.AGILITY:
			return "敏捷"
		UnitBaseAttributes.CONSTITUTION:
			return "体质"
		UnitBaseAttributes.PERCEPTION:
			return "感知"
		UnitBaseAttributes.INTELLIGENCE:
			return "智力"
		UnitBaseAttributes.WILLPOWER:
			return "意志"
		UnitBaseAttributes.HIDDEN_LUCK_AT_BIRTH:
			return "出生隐藏幸运"
		UnitBaseAttributes.FAITH_LUCK_BONUS:
			return "信仰幸运加值"
		ATTRIBUTE_SERVICE_SCRIPT.HP_MAX:
			return "生命上限"
		ATTRIBUTE_SERVICE_SCRIPT.MP_MAX:
			return "法力上限"
		ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX:
			return "体力上限"
		ATTRIBUTE_SERVICE_SCRIPT.AURA_MAX:
			return "灵气上限"
		ATTRIBUTE_SERVICE_SCRIPT.ACTION_POINTS:
			return "行动点"
		ATTRIBUTE_SERVICE_SCRIPT.ACTION_THRESHOLD:
			return "行动阈值 TU"
		ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS:
			return "攻击加值"
		ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS:
			return "AC"
		ATTRIBUTE_SERVICE_SCRIPT.ARMOR_AC_BONUS:
			return "护甲 AC"
		ATTRIBUTE_SERVICE_SCRIPT.SHIELD_AC_BONUS:
			return "盾牌 AC"
		ATTRIBUTE_SERVICE_SCRIPT.DODGE_BONUS:
			return "闪避加值"
		ATTRIBUTE_SERVICE_SCRIPT.DEFLECTION_BONUS:
			return "偏斜加值"
		ATTRIBUTE_SERVICE_SCRIPT.CRIT_RATE:
			return "暴击率"
		ATTRIBUTE_SERVICE_SCRIPT.CRIT_DAMAGE:
			return "暴击伤害"
		_:
			return String(attribute_id)


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
	if _selected_member_id == _main_character_member_id:
		status_label.text = "主角必须保持上阵，不能移至替补。"
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
