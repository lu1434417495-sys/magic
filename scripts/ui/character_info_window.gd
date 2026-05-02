## 文件说明：该脚本属于角色信息窗口相关的界面窗口脚本，集中维护遮罩、标题标签、元信息标签等顶层字段。
## 审查重点：重点核对字段含义、节点绑定、信号联动以及界面状态切换是否仍与对应场景保持一致。
## 备注：后续如果调整场景节点命名、层级或交互路径，需要同步检查成员字段与信号连接。

class_name CharacterInfoWindow
extends Control

const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")
const FATE_SECTION_TITLE := "命运"
const LEGACY_TOP_LEVEL_KEYS := ["type_label", "faction_label", "coord"]
const SECTION_KEYS := ["title", "entries"]
const PAIR_ENTRY_KEYS := ["label", "value"]
const TEXT_ENTRY_KEYS := ["text"]
const FATE_KEYS := [
	"hidden_luck_at_birth",
	"faith_luck_bonus",
	"effective_luck",
	"fortune_marked",
	"doom_marked",
	"doom_authority",
	"has_misfortune",
]

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
	var top_level_payload := _normalize_top_level_payload(window_data)
	if top_level_payload.is_empty():
		hide_window()
		return
	var sections := _normalize_sections(window_data)
	if sections.is_empty():
		hide_window()
		return
	var display_name := String(top_level_payload["display_name"])
	var meta_text := String(top_level_payload["meta_label"]).strip_edges()
	var status_text := String(top_level_payload["status_label"])

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


func _normalize_top_level_payload(window_data: Dictionary) -> Dictionary:
	for legacy_key in LEGACY_TOP_LEVEL_KEYS:
		if window_data.has(legacy_key):
			return {}
	for field_name in ["display_name", "meta_label", "status_label"]:
		if not window_data.has(field_name) or window_data[field_name] is not String:
			return {}
	var display_name := String(window_data["display_name"]).strip_edges()
	if display_name.is_empty():
		return {}
	return {
		"display_name": display_name,
		"meta_label": String(window_data["meta_label"]),
		"status_label": String(window_data["status_label"]),
	}


func _normalize_sections(window_data: Dictionary) -> Array[Dictionary]:
	if not window_data.has("sections"):
		return []
	var sections := _normalize_explicit_sections(window_data["sections"])
	if sections.is_empty():
		return sections
	if not window_data.has("fate"):
		return sections
	var fate_section := _build_fate_section(window_data["fate"], sections)
	if fate_section.is_empty():
		return []
	else:
		sections.append(fate_section)
	return sections


func _normalize_explicit_sections(section_variants: Variant) -> Array[Dictionary]:
	var sections: Array[Dictionary] = []
	if section_variants is not Array or (section_variants as Array).is_empty():
		return sections
	for section_variant in section_variants:
		if section_variant is not Dictionary:
			return []
		var section_data := section_variant as Dictionary
		if not _has_exact_keys(section_data, SECTION_KEYS):
			return []
		if not section_data.has("title") or section_data["title"] is not String:
			return []
		var title_text := String(section_data["title"]).strip_edges()
		if title_text.is_empty():
			return []
		if not section_data.has("entries") or section_data["entries"] is not Array:
			return []
		var normalized_entries := _normalize_section_entries(section_data["entries"])
		if normalized_entries.is_empty():
			return []
		sections.append({
			"title": title_text,
			"entries": normalized_entries,
		})
	return sections


func _normalize_section_entries(entry_variants: Array) -> Array[Dictionary]:
	var entries: Array[Dictionary] = []
	if entry_variants.is_empty():
		return entries
	for entry_variant in entry_variants:
		var normalized_entry := _normalize_entry(entry_variant)
		if normalized_entry.is_empty():
			return []
		entries.append(normalized_entry)
	return entries


func _normalize_entry(entry_variant: Variant) -> Dictionary:
	if entry_variant is not Dictionary:
		return {}
	var entry := entry_variant as Dictionary
	if _has_exact_keys(entry, PAIR_ENTRY_KEYS):
		if entry["label"] is not String or not entry.has("value") or entry["value"] is not String:
			return {}
		var label_text := String(entry["label"]).strip_edges()
		if label_text.is_empty():
			return {}
		return {
			"kind": "pair",
			"label": label_text,
			"value": String(entry["value"]),
		}
	if _has_exact_keys(entry, TEXT_ENTRY_KEYS):
		if entry["text"] is not String:
			return {}
		var text_value := String(entry["text"]).strip_edges()
		if text_value.is_empty():
			return {}
		return {
			"kind": "text",
			"text": text_value,
		}
	return {}


func _build_fate_section(fate_variant: Variant, existing_sections: Array[Dictionary]) -> Dictionary:
	if fate_variant is not Dictionary:
		return {}
	if _has_section_title(existing_sections, FATE_SECTION_TITLE):
		return {}

	var fate_data := fate_variant as Dictionary
	if not _has_exact_keys(fate_data, FATE_KEYS):
		return {}
	for field_name in [
		"hidden_luck_at_birth",
		"faith_luck_bonus",
		"effective_luck",
		"fortune_marked",
		"doom_marked",
		"doom_authority",
	]:
		if not fate_data.has(field_name) or fate_data[field_name] is not int:
			return {}
	if not fate_data.has("has_misfortune") or fate_data["has_misfortune"] is not bool:
		return {}

	var hidden_luck_at_birth := int(fate_data["hidden_luck_at_birth"])
	var faith_luck_bonus := int(fate_data["faith_luck_bonus"])
	var effective_luck := int(fate_data["effective_luck"])
	var fortune_marked := int(fate_data["fortune_marked"])
	var doom_marked := int(fate_data["doom_marked"])
	var doom_authority := int(fate_data["doom_authority"])
	var has_misfortune := bool(fate_data["has_misfortune"])
	var expected_effective_luck := clampi(
		hidden_luck_at_birth + faith_luck_bonus,
		UNIT_BASE_ATTRIBUTES_SCRIPT.EFFECTIVE_LUCK_MIN,
		UNIT_BASE_ATTRIBUTES_SCRIPT.EFFECTIVE_LUCK_MAX
	)
	if effective_luck != expected_effective_luck:
		return {}
	if fortune_marked < 0 or doom_marked < 0 or doom_authority < 0:
		return {}
	if has_misfortune != (doom_authority > 0):
		return {}

	var entries: Array[Dictionary] = [
		{
			"kind": "pair",
			"label": "生来暗运",
			"value": _format_signed_number(hidden_luck_at_birth),
		},
		{
			"kind": "pair",
			"label": "信仰赐运",
			"value": _format_signed_number(faith_luck_bonus),
		},
		{
			"kind": "pair",
			"label": "有效运势",
			"value": _format_signed_number(effective_luck),
		},
		{
			"kind": "pair",
			"label": "Fortuna 标记",
			"value": _format_fate_mark_value(fortune_marked, "已获福印", "未获福印"),
		},
		{
			"kind": "pair",
			"label": "Misfortune 黑兆",
			"value": _format_fate_mark_value(doom_marked, "已见黑兆", "未见黑兆"),
		},
	]
	if has_misfortune:
		entries.append({
			"kind": "pair",
			"label": "厄权",
			"value": "%d 级" % doom_authority,
		})

	for hint_text in _build_fate_hint_texts(hidden_luck_at_birth, effective_luck):
		entries.append({
			"kind": "text",
			"text": hint_text,
		})

	return {
		"title": FATE_SECTION_TITLE,
		"entries": entries,
	}


func _has_exact_keys(data: Dictionary, expected_keys: Array) -> bool:
	if data.size() != expected_keys.size():
		return false
	for expected_key in expected_keys:
		if not data.has(expected_key):
			return false
	return true


func _has_section_title(sections: Array[Dictionary], title_text: String) -> bool:
	for section in sections:
		if String(section["title"]).strip_edges() == title_text:
			return true
	return false


func _format_signed_number(value: int) -> String:
	if value > 0:
		return "+%d" % value
	return "%d" % value


func _format_fate_mark_value(value: int, marked_text: String, unmarked_text: String) -> String:
	return "%d（%s）" % [value, marked_text if value > 0 else unmarked_text]


func _build_fate_hint_texts(hidden_luck_at_birth: int, effective_luck: int) -> Array[String]:
	var hints: Array[String] = []
	if hidden_luck_at_birth >= UNIT_BASE_ATTRIBUTES_SCRIPT.EFFECTIVE_LUCK_MAX:
		hints.append("生来暗运已处于极端正运档，界面会按原值保留该刻印。")
	elif hidden_luck_at_birth <= UNIT_BASE_ATTRIBUTES_SCRIPT.EFFECTIVE_LUCK_MIN:
		hints.append("生来暗运已压到最深坏运档，这类角色更容易撞进命运事件的极端分支。")

	if effective_luck >= UNIT_BASE_ATTRIBUTES_SCRIPT.EFFECTIVE_LUCK_MAX:
		hints.append("有效运势已到 +7 上限：高位大成功威胁区会吃满，但随机掉落仍只按 +5 结算。")
	elif effective_luck <= UNIT_BASE_ATTRIBUTES_SCRIPT.EFFECTIVE_LUCK_MIN:
		hints.append("有效运势已压到 -6 下限：大失败区间会扩到 1-3；若处于劣势，命运的怜悯仍只回拉一档暴击门。")
	return hints


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

	var title_text := String(section_data["title"]).strip_edges()
	if not title_text.is_empty():
		var section_title := Label.new()
		section_title.text = title_text
		section_title.add_theme_color_override("font_color", Color(0.972549, 0.815686, 0.427451, 1.0))
		section_title.add_theme_font_size_override("font_size", 18)
		content.add_child(section_title)

	for entry in section_data["entries"]:
		var kind := String(entry["kind"])
		if kind == "pair":
			content.add_child(_build_pair_entry(entry))
		else:
			content.add_child(_build_text_entry(String(entry["text"])))

	return panel


func _build_pair_entry(entry: Dictionary) -> Control:
	var row := HBoxContainer.new()
	row.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_theme_constant_override("separation", 12)

	var label_node := Label.new()
	label_node.custom_minimum_size = Vector2(108.0, 0.0)
	label_node.text = "%s：" % String(entry["label"])
	label_node.add_theme_color_override("font_color", Color(0.635294, 0.713726, 0.85098, 1.0))
	row.add_child(label_node)

	var value_node := Label.new()
	value_node.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	value_node.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	value_node.text = String(entry["value"])
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
