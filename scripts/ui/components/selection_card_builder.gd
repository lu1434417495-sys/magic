## 文件说明：选择卡片的构造器，集中提供"暖金标题 + 摘要 + 底部 chip 列表"的统一卡片外观，供升级、奖励、预设选择等模态共享同一视觉语言。
## 审查重点：构造逻辑应保持纯函数特性（不依赖外部状态），新增字段时同步更新调用方说明，避免视觉漂移。
## 备注：内层子节点统一关闭 mouse_filter，确保父级 PanelContainer 的 gui_input 能完整接管点击；样式使用静态创建，调用方可自行缓存以减少分配。

class_name SelectionCardBuilder
extends RefCounted

const _CARD_BG_NORMAL := Color(0.10, 0.13, 0.20, 0.94)
const _CARD_BG_SELECTED := Color(0.18, 0.16, 0.10, 0.98)
const _CARD_BORDER_NORMAL := Color(0.40, 0.50, 0.66, 0.7)
const _CARD_BORDER_SELECTED := Color(0.95, 0.78, 0.32, 1.0)
const _COLOR_TITLE := Color(0.98, 0.86, 0.46, 1)
const _COLOR_SUMMARY := Color(0.85, 0.92, 1.0, 0.92)
const _COLOR_CHIP_HEADER := Color(0.756, 0.835, 0.957, 0.85)
const _COLOR_CHIP := Color(0.95, 0.95, 0.95, 1)


## 返回单张卡片的样式盒：常态使用冷蓝低对比边框，选中态切换为暖金 + 外晕。
static func make_style(selected: bool) -> StyleBoxFlat:
	var sb := StyleBoxFlat.new()
	sb.bg_color = _CARD_BG_SELECTED if selected else _CARD_BG_NORMAL
	sb.border_width_left = 2
	sb.border_width_top = 2
	sb.border_width_right = 2
	sb.border_width_bottom = 2
	sb.border_color = _CARD_BORDER_SELECTED if selected else _CARD_BORDER_NORMAL
	sb.corner_radius_top_left = 14
	sb.corner_radius_top_right = 14
	sb.corner_radius_bottom_right = 14
	sb.corner_radius_bottom_left = 14
	if selected:
		sb.shadow_color = Color(0.95, 0.78, 0.32, 0.35)
		sb.shadow_size = 8
	return sb


## 构造一张选择卡片节点，调用方负责挂载到容器并自行连接 gui_input/选中态切换。
## spec 字段（除 title 外均可选）：
##   title: String         — 卡片主标题（暖金加粗）
##   summary: String       — 一行/多行摘要（autowrap，灰蓝）
##   chip_header: String   — 底部 chip 区的小标题
##   chips: Array/PackedStringArray — 底部 chip 文本
static func build_card(spec: Dictionary) -> PanelContainer:
	var card := PanelContainer.new()
	card.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	card.size_flags_vertical = Control.SIZE_EXPAND_FILL
	card.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	card.add_theme_stylebox_override("panel", make_style(false))

	var margin := MarginContainer.new()
	margin.mouse_filter = Control.MOUSE_FILTER_IGNORE
	margin.add_theme_constant_override("margin_left", 18)
	margin.add_theme_constant_override("margin_right", 18)
	margin.add_theme_constant_override("margin_top", 16)
	margin.add_theme_constant_override("margin_bottom", 16)
	card.add_child(margin)

	var vbox := VBoxContainer.new()
	vbox.mouse_filter = Control.MOUSE_FILTER_IGNORE
	vbox.add_theme_constant_override("separation", 8)
	margin.add_child(vbox)

	var title := String(spec.get("title", ""))
	var summary := String(spec.get("summary", ""))
	var chip_header := String(spec.get("chip_header", ""))
	var chips_raw = spec.get("chips", [])

	vbox.add_child(_make_label(title, _COLOR_TITLE, 24, false))

	if not summary.is_empty():
		vbox.add_child(_make_label(summary, _COLOR_SUMMARY, 14, true))

	var spacer := Control.new()
	spacer.mouse_filter = Control.MOUSE_FILTER_IGNORE
	spacer.size_flags_vertical = Control.SIZE_EXPAND_FILL
	vbox.add_child(spacer)

	var chip_strings: PackedStringArray = []
	for chip_value in chips_raw:
		chip_strings.append(String(chip_value))

	if not chip_strings.is_empty():
		if not chip_header.is_empty():
			vbox.add_child(_make_label(chip_header, _COLOR_CHIP_HEADER, 12, false))
		vbox.add_child(_make_label("  ·  ".join(chip_strings), _COLOR_CHIP, 14, true))

	return card


static func _make_label(text: String, color: Color, font_size: int, autowrap: bool) -> Label:
	var label := Label.new()
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	label.text = text
	label.add_theme_color_override("font_color", color)
	label.add_theme_font_size_override("font_size", font_size)
	if autowrap:
		label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	return label
