## 文件说明：战斗技能槽按钮，重写 _make_custom_tooltip 以渲染深色主题的自定义 tooltip。
## 审查重点：
##   1) 技能字段（display_name / description / footer / disabled_reason / accent_color / cooldown）是否完整传入，避免出现空白 tooltip。
##   2) tooltip 视觉与 skill slot 形态有显著差异（更亮底 + 金色 2px 边 + 大圆角 + 强阴影），不应再复用槽位风格。
##   3) Godot 默认把 tooltip popup 放在鼠标右下；我们用 resized 信号把承载 Window 上移到鼠标上方，避免遮挡技能图标。
## 备注：节点本身不渲染文本（保持透明命中层语义），只在 hover 时构造一次 Control 给引擎接管。

class_name BattleSkillSlotButton
extends Button

const BattleUiTheme = preload("res://scripts/ui/battle_ui_theme.gd")

const TOOLTIP_MIN_WIDTH := 240.0
const TOOLTIP_MAX_WIDTH := 320.0
const TOOLTIP_PADDING := 14
const TOOLTIP_MOUSE_GAP := 18  # tooltip 底边距离鼠标的纵向间距
const TOOLTIP_SCREEN_MARGIN := 8

## 字段说明：技能展示名，作为 tooltip 标题使用。
var skill_display_name: String = ""
## 字段说明：技能描述文本（多行），作为 tooltip 主体；空串时不渲染描述段。
var skill_description: String = ""
## 字段说明：底部辅助信息（消耗 / 状态），与 slot 上下文一致；空串或 "READY" 时不渲染。
var skill_footer_text: String = ""
## 字段说明：禁用原因；非空时替代 footer 用警示色显示。
var skill_disabled_reason: String = ""
## 字段说明：当前冷却剩余回合；>0 时附加在 footer 行尾。
var skill_cooldown: int = 0
## 字段说明：命运色 / 主题色，用于 tooltip 顶部 3px 高光带（仍呼应技能槽底部光带，做语义关联）。
var skill_accent_color: Color = BattleUiTheme.FATE_GATE


func _make_custom_tooltip(_for_text: String) -> Control:
	# 引擎传入的 _for_text 是 tooltip_text，这里忽略，全部走结构化字段。
	var root := PanelContainer.new()
	root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.custom_minimum_size = Vector2(TOOLTIP_MIN_WIDTH, 0)
	root.add_theme_stylebox_override("panel", _build_tooltip_panel_style())

	var layout := VBoxContainer.new()
	layout.add_theme_constant_override("separation", 8)
	root.add_child(layout)

	# 顶部 3px 命运色高光带，呼应技能槽底部光带；这是 tooltip 与槽位之间唯一保留的视觉同构点。
	var accent_strip := ColorRect.new()
	accent_strip.custom_minimum_size = Vector2(0, 3)
	accent_strip.color = skill_accent_color
	layout.add_child(accent_strip)

	var text_padding := MarginContainer.new()
	text_padding.add_theme_constant_override("margin_left", TOOLTIP_PADDING)
	text_padding.add_theme_constant_override("margin_right", TOOLTIP_PADDING)
	text_padding.add_theme_constant_override("margin_top", 6)
	text_padding.add_theme_constant_override("margin_bottom", TOOLTIP_PADDING)
	layout.add_child(text_padding)

	var text_column := VBoxContainer.new()
	text_column.add_theme_constant_override("separation", 6)
	text_padding.add_child(text_column)

	var title_label := Label.new()
	title_label.text = skill_display_name if not skill_display_name.is_empty() else "未知技能"
	title_label.add_theme_font_size_override("font_size", BattleUiTheme.FONT_TITLE)
	title_label.add_theme_color_override("font_color", BattleUiTheme.TEXT_PRIMARY)
	title_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	title_label.custom_minimum_size = Vector2(TOOLTIP_MIN_WIDTH - TOOLTIP_PADDING * 2, 0)
	text_column.add_child(title_label)

	if not skill_description.is_empty():
		var description_label := Label.new()
		description_label.text = skill_description
		description_label.add_theme_font_size_override("font_size", BattleUiTheme.FONT_BODY)
		description_label.add_theme_color_override("font_color", BattleUiTheme.TEXT_SECONDARY)
		description_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		description_label.custom_minimum_size = Vector2(TOOLTIP_MIN_WIDTH - TOOLTIP_PADDING * 2, 0)
		text_column.add_child(description_label)

	var meta_segments: Array[String] = []
	if skill_cooldown > 0:
		meta_segments.append("冷却 %d" % skill_cooldown)
	if not skill_footer_text.is_empty() and skill_footer_text != "READY":
		meta_segments.append(skill_footer_text)
	if not meta_segments.is_empty():
		var meta_label := Label.new()
		meta_label.text = "  ·  ".join(PackedStringArray(meta_segments))
		meta_label.add_theme_font_size_override("font_size", BattleUiTheme.FONT_LABEL)
		meta_label.add_theme_color_override("font_color", BattleUiTheme.TEXT_MUTED)
		meta_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		meta_label.custom_minimum_size = Vector2(TOOLTIP_MIN_WIDTH - TOOLTIP_PADDING * 2, 0)
		text_column.add_child(meta_label)

	if not skill_disabled_reason.is_empty():
		var disabled_label := Label.new()
		disabled_label.text = "不可用：%s" % skill_disabled_reason
		disabled_label.add_theme_font_size_override("font_size", BattleUiTheme.FONT_LABEL)
		disabled_label.add_theme_color_override("font_color", BattleUiTheme.FATE_DANGER)
		disabled_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		disabled_label.custom_minimum_size = Vector2(TOOLTIP_MIN_WIDTH - TOOLTIP_PADDING * 2, 0)
		text_column.add_child(disabled_label)

	# 上限 320，超出由 autowrap 处理；最小 240 给标题留呼吸。
	root.custom_minimum_size = Vector2(TOOLTIP_MIN_WIDTH, 0)
	root.set("size", Vector2(TOOLTIP_MAX_WIDTH, 0))

	# tooltip Control 被 PopupPanel 接管后会触发 resized，那时尺寸才确定，
	# 在回调里把 PopupPanel(Window) 移到鼠标上方。lambda 不引用 self，避免按钮先于 tooltip 释放时报错。
	root.resized.connect(func() -> void:
		if not is_instance_valid(root):
			return
		var window := root.get_viewport()
		if window == null or not (window is Window):
			return
		var window_node := window as Window
		var mouse_screen: Vector2i = DisplayServer.mouse_get_position()
		var tip_w: int = int(root.size.x)
		var tip_h: int = int(root.size.y)
		var screen_size: Vector2i = DisplayServer.screen_get_size()
		var max_x: int = maxi(screen_size.x - tip_w - TOOLTIP_SCREEN_MARGIN, TOOLTIP_SCREEN_MARGIN)
		var new_x: int = clampi(mouse_screen.x - tip_w / 2, TOOLTIP_SCREEN_MARGIN, max_x)
		var new_y: int = maxi(mouse_screen.y - tip_h - TOOLTIP_MOUSE_GAP, TOOLTIP_SCREEN_MARGIN)
		window_node.position = Vector2i(new_x, new_y)
	)

	return root


func _build_tooltip_panel_style() -> StyleBoxFlat:
	# 与技能槽（PANEL_BG_DEEP + 1px 软描边 + 3px 圆角）形成对比：
	# tooltip 用更亮的 PANEL_BG_ALT、2px 金色描边、大圆角、强阴影，看一眼就能辨别 "这是浮层不是按钮"。
	var style := StyleBoxFlat.new()
	style.bg_color = BattleUiTheme.PANEL_BG_ALT
	style.border_color = BattleUiTheme.TEXT_ACCENT
	style.border_width_left = 2
	style.border_width_right = 2
	style.border_width_top = 2
	style.border_width_bottom = 2
	style.corner_radius_top_left = BattleUiTheme.PANEL_RADIUS_LARGE
	style.corner_radius_top_right = BattleUiTheme.PANEL_RADIUS_LARGE
	style.corner_radius_bottom_left = BattleUiTheme.PANEL_RADIUS_LARGE
	style.corner_radius_bottom_right = BattleUiTheme.PANEL_RADIUS_LARGE
	style.shadow_color = Color(0, 0, 0, 0.7)
	style.shadow_size = 12
	return style
