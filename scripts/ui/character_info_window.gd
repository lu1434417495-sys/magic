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
## 字段说明：缓存 section 滚动容器，用于在切换人物时重置滚动位置。
@onready var sections_scroll: ScrollContainer = $CenterContainer/Panel/MarginContainer/Content/Body/SectionsScroll
## 字段说明：缓存 section 容器节点，用于动态构建分段详情内容。
@onready var sections_container: VBoxContainer = $CenterContainer/Panel/MarginContainer/Content/Body/SectionsScroll/SectionsContainer
## 字段说明：缓存状态块节点，用于在状态为空时整体隐藏底部提示区域。
@onready var status_block: VBoxContainer = $CenterContainer/Panel/MarginContainer/Content/Body/StatusBlock
## 字段说明：缓存状态提示标签节点，用于向玩家展示当前操作结果、错误原因或下一步引导。
@onready var status_label: Label = $CenterContainer/Panel/MarginContainer/Content/Body/StatusBlock/StatusLabel
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
	var status_text := String(window_data.get("status_label", ""))
	var meta_text := _build_meta_text(window_data, type_label, faction_label, coord_text)
	var sections := _normalize_sections(window_data, display_name, type_label, faction_label, coord_text)

	visible = true
	title_label.text = display_name
	meta_label.text = meta_text
	meta_label.visible = not meta_text.is_empty()
	_rebuild_sections(sections)
	status_label.text = status_text
	status_block.visible = not status_text.is_empty()


func hide_window() -> void:
	visible = false
	title_label.text = "人物信息"
	meta_label.text = ""
	meta_label.visible = false
	_clear_sections()
	status_label.text = ""
	status_block.visible = false


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


func _build_meta_text(window_data: Dictionary, type_label: String, faction_label: String, coord_text: String) -> String:
	var meta_text := String(window_data.get("meta_label", ""))
	if not meta_text.is_empty():
		return meta_text
	var has_legacy_meta := window_data.has("type_label") or window_data.has("faction_label") or window_data.has("coord")
	if not has_legacy_meta:
		return ""
	return "%s  |  阵营 %s  |  坐标 %s" % [type_label, faction_label, coord_text]


func _normalize_sections(
	window_data: Dictionary,
	display_name: String,
	type_label: String,
	faction_label: String,
	coord_text: String
) -> Array[Dictionary]:
	var sections := _normalize_explicit_sections(window_data.get("sections", []))
	if not sections.is_empty():
		return sections
	return [{
		"title": "身份信息",
		"entries": [
			{
				"kind": "pair",
				"label": "姓名",
				"value": display_name,
			},
			{
				"kind": "pair",
				"label": "类型",
				"value": type_label,
			},
			{
				"kind": "pair",
				"label": "阵营",
				"value": faction_label,
			},
			{
				"kind": "pair",
				"label": "坐标",
				"value": coord_text,
			},
		],
	}]


func _normalize_explicit_sections(section_variants: Variant) -> Array[Dictionary]:
	var sections: Array[Dictionary] = []
	if section_variants is not Array:
		return sections
	for section_variant in section_variants:
		if section_variant is not Dictionary:
			continue
		var section_data := section_variant as Dictionary
		var normalized_entries := _normalize_section_entries(section_data)
		if normalized_entries.is_empty():
			continue
		sections.append({
			"title": String(section_data.get("title", "")),
			"entries": normalized_entries,
		})
	return sections


func _normalize_section_entries(section_data: Dictionary) -> Array[Dictionary]:
	var entries: Array[Dictionary] = []
	var entry_source: Variant = section_data.get("entries", section_data.get("rows", section_data.get("lines", [])))
	if entry_source is Array:
		for entry_variant in entry_source:
			var normalized_entry := _normalize_entry(entry_variant)
			if normalized_entry.is_empty():
				continue
			entries.append(normalized_entry)
	elif entry_source is PackedStringArray:
		for line in entry_source:
			var normalized_entry := _normalize_entry(String(line))
			if normalized_entry.is_empty():
				continue
			entries.append(normalized_entry)
	var body_text := String(section_data.get("body", "")).strip_edges()
	if entries.is_empty() and not body_text.is_empty():
		entries.append({
			"kind": "text",
			"text": body_text,
		})
	return entries


func _normalize_entry(entry_variant: Variant) -> Dictionary:
	if entry_variant is Dictionary:
		var entry := entry_variant as Dictionary
		var label_text := String(entry.get("label", ""))
		var value_text := String(entry.get("value", ""))
		var text_value := String(entry.get("text", "")).strip_edges()
		if not label_text.is_empty():
			return {
				"kind": "pair",
				"label": label_text,
				"value": value_text,
			}
		if not text_value.is_empty():
			return {
				"kind": "text",
				"text": text_value,
			}
		if not value_text.is_empty():
			return {
				"kind": "text",
				"text": value_text,
			}
		return {}
	var text := String(entry_variant).strip_edges()
	if text.is_empty():
		return {}
	return {
		"kind": "text",
		"text": text,
	}


func _rebuild_sections(sections: Array[Dictionary]) -> void:
	_clear_sections()
	for section in sections:
		sections_container.add_child(_build_section_panel(section))
	if sections_scroll != null:
		sections_scroll.set_deferred("scroll_vertical", 0)


func _clear_sections() -> void:
	if sections_container == null:
		return
	for child in sections_container.get_children():
		child.free()


func _build_section_panel(section_data: Dictionary) -> PanelContainer:
	var panel := PanelContainer.new()
	panel.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	panel.add_theme_stylebox_override("panel", _create_section_panel_stylebox())

	var margin := MarginContainer.new()
	margin.add_theme_constant_override("margin_left", 16)
	margin.add_theme_constant_override("margin_top", 14)
	margin.add_theme_constant_override("margin_right", 16)
	margin.add_theme_constant_override("margin_bottom", 14)
	panel.add_child(margin)

	var content := VBoxContainer.new()
	content.add_theme_constant_override("separation", 10)
	margin.add_child(content)

	var title_text := String(section_data.get("title", "")).strip_edges()
	if not title_text.is_empty():
		var section_title := Label.new()
		section_title.text = title_text
		section_title.add_theme_color_override("font_color", Color(0.972549, 0.815686, 0.427451, 1.0))
		section_title.add_theme_font_size_override("font_size", 18)
		content.add_child(section_title)

	for entry_variant in section_data.get("entries", []):
		if entry_variant is not Dictionary:
			continue
		var entry := entry_variant as Dictionary
		var kind := String(entry.get("kind", "text"))
		if kind == "pair":
			content.add_child(_build_pair_entry(entry))
		else:
			content.add_child(_build_text_entry(String(entry.get("text", ""))))

	return panel


func _build_pair_entry(entry: Dictionary) -> Control:
	var row := HBoxContainer.new()
	row.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_theme_constant_override("separation", 12)

	var label_node := Label.new()
	label_node.custom_minimum_size = Vector2(108.0, 0.0)
	label_node.text = "%s：" % String(entry.get("label", ""))
	label_node.add_theme_color_override("font_color", Color(0.635294, 0.713726, 0.85098, 1.0))
	row.add_child(label_node)

	var value_node := Label.new()
	value_node.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	value_node.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	value_node.text = String(entry.get("value", ""))
	value_node.add_theme_color_override("font_color", Color(0.960784, 0.976471, 1.0, 1.0))
	row.add_child(value_node)

	return row


func _build_text_entry(text: String) -> Label:
	var text_label := Label.new()
	text_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	text_label.text = text
	text_label.add_theme_color_override("font_color", Color(0.901961, 0.933333, 0.980392, 1.0))
	return text_label


func _create_section_panel_stylebox() -> StyleBoxFlat:
	var stylebox := StyleBoxFlat.new()
	stylebox.bg_color = Color(0.109804, 0.145098, 0.235294, 0.88)
	stylebox.border_width_left = 1
	stylebox.border_width_top = 1
	stylebox.border_width_right = 1
	stylebox.border_width_bottom = 1
	stylebox.border_color = Color(0.278431, 0.384314, 0.584314, 0.9)
	stylebox.corner_radius_top_left = 14
	stylebox.corner_radius_top_right = 14
	stylebox.corner_radius_bottom_right = 14
	stylebox.corner_radius_bottom_left = 14
	return stylebox
