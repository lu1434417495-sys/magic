## 文件说明：战斗悬停浮层（A1）。把 BattleHudAdapter.build_hover_preview() 的数据贴近被悬停的单位。
## 审查重点：apply_preview 必须容忍空字段；浮层不参与点击（mouse_filter = IGNORE）。
## 备注：节点不直接连接信号，仅由 BattleMapPanel 调度其 apply_preview / clear。

class_name BattleHoverPreviewOverlay
extends PanelContainer

const BattleUiTheme = preload("res://scripts/ui/battle_ui_theme.gd")

const HIT_STAGE_SEGMENT_WIDTH := 28
const HIT_STAGE_SEGMENT_HEIGHT := 8
const HIT_STAGE_SEGMENT_SEPARATION := 4
const HP_BAR_HEIGHT := 6
const HP_BAR_MIN_WIDTH := 140

var _layout: VBoxContainer = null
var _target_header: HBoxContainer = null
var _target_name_label: Label = null
var _target_faction_label: Label = null
var _target_hp_bar: ProgressBar = null
var _target_hp_label: Label = null
var _hit_stage_row: HBoxContainer = null
var _hit_summary_label: Label = null
var _fate_badge_row: HFlowContainer = null
var _damage_label: Label = null
var _invalid_label: Label = null


func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	visible = false
	custom_minimum_size = Vector2(180, 0)
	add_theme_stylebox_override("panel", _build_panel_style())
	_build_layout()


func clear() -> void:
	visible = false


## 把 BattleHudAdapter.build_hover_preview(...) 的结果应用到浮层。
## preview 缺 target_unit 时显示"不可达 / 空格"；技能未选中则隐藏命中 / 命运 / 伤害区域。
func apply_preview(preview: Dictionary) -> void:
	if preview.is_empty():
		visible = false
		return
	var target_unit := preview.get("target_unit", {}) as Dictionary
	var has_target_unit := not target_unit.is_empty()
	var has_skill := bool(preview.get("has_selected_skill", false))
	var is_valid_target := bool(preview.get("hover_is_valid_target", false))

	if not has_target_unit and not has_skill:
		visible = false
		return

	_refresh_target_unit(target_unit)
	_refresh_hit_stages(preview.get("hit_stage_rates", []) as Array)
	_refresh_fate_badges(preview.get("fate_badges", []) as Array)
	_refresh_damage_label(
		int(preview.get("damage_min", 0)),
		int(preview.get("damage_max", 0)),
		String(preview.get("damage_text", ""))
	)
	_refresh_hit_summary(String(preview.get("hit_badge_text", "")))
	_refresh_invalid_label(has_skill and not is_valid_target)

	visible = true


func _build_layout() -> void:
	_layout = VBoxContainer.new()
	_layout.name = "HoverLayout"
	_layout.add_theme_constant_override("separation", 6)
	add_child(_layout)

	_target_header = HBoxContainer.new()
	_target_header.name = "TargetHeader"
	_target_header.add_theme_constant_override("separation", 8)
	_layout.add_child(_target_header)

	_target_name_label = Label.new()
	_target_name_label.name = "TargetNameLabel"
	_target_name_label.add_theme_font_size_override("font_size", BattleUiTheme.FONT_LABEL)
	_target_name_label.add_theme_color_override("font_color", BattleUiTheme.TEXT_PRIMARY)
	_target_name_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_target_header.add_child(_target_name_label)

	_target_faction_label = Label.new()
	_target_faction_label.name = "TargetFactionLabel"
	_target_faction_label.add_theme_font_size_override("font_size", BattleUiTheme.FONT_CAPTION)
	_target_faction_label.add_theme_color_override("font_color", BattleUiTheme.TEXT_SECONDARY)
	_target_header.add_child(_target_faction_label)

	_target_hp_bar = ProgressBar.new()
	_target_hp_bar.name = "TargetHpBar"
	_target_hp_bar.show_percentage = false
	_target_hp_bar.custom_minimum_size = Vector2(HP_BAR_MIN_WIDTH, HP_BAR_HEIGHT)
	_target_hp_bar.add_theme_stylebox_override("background", _build_progress_background_style())
	_target_hp_bar.add_theme_stylebox_override("fill", _build_progress_fill_style(BattleUiTheme.RESOURCE_HP))
	_layout.add_child(_target_hp_bar)

	_target_hp_label = Label.new()
	_target_hp_label.name = "TargetHpLabel"
	_target_hp_label.add_theme_font_size_override("font_size", BattleUiTheme.FONT_CAPTION)
	_target_hp_label.add_theme_color_override("font_color", BattleUiTheme.TEXT_SECONDARY)
	_layout.add_child(_target_hp_label)

	_hit_stage_row = HBoxContainer.new()
	_hit_stage_row.name = "HitStageRow"
	_hit_stage_row.add_theme_constant_override("separation", HIT_STAGE_SEGMENT_SEPARATION)
	_layout.add_child(_hit_stage_row)

	_hit_summary_label = Label.new()
	_hit_summary_label.name = "HitSummaryLabel"
	_hit_summary_label.add_theme_font_size_override("font_size", BattleUiTheme.FONT_LABEL)
	_hit_summary_label.add_theme_color_override("font_color", BattleUiTheme.TEXT_PRIMARY)
	_layout.add_child(_hit_summary_label)

	_fate_badge_row = HFlowContainer.new()
	_fate_badge_row.name = "FateBadgeRow"
	_fate_badge_row.add_theme_constant_override("h_separation", 6)
	_fate_badge_row.add_theme_constant_override("v_separation", 4)
	_layout.add_child(_fate_badge_row)

	_damage_label = Label.new()
	_damage_label.name = "DamageRangeLabel"
	_damage_label.add_theme_font_size_override("font_size", BattleUiTheme.FONT_LABEL)
	_damage_label.add_theme_color_override("font_color", BattleUiTheme.TEXT_PRIMARY)
	_layout.add_child(_damage_label)

	_invalid_label = Label.new()
	_invalid_label.name = "InvalidTargetLabel"
	_invalid_label.text = "不可达"
	_invalid_label.add_theme_font_size_override("font_size", BattleUiTheme.FONT_CAPTION)
	_invalid_label.add_theme_color_override("font_color", BattleUiTheme.FATE_DANGER)
	_invalid_label.visible = false
	_layout.add_child(_invalid_label)


func _refresh_target_unit(target_unit: Dictionary) -> void:
	if target_unit.is_empty():
		_target_header.visible = false
		_target_hp_bar.visible = false
		_target_hp_label.visible = false
		return
	_target_header.visible = true
	_target_hp_bar.visible = true
	_target_hp_label.visible = true
	_target_name_label.text = String(target_unit.get("name", "单位"))
	var faction_text := ""
	if bool(target_unit.get("is_self", false)):
		faction_text = "本单位"
	elif bool(target_unit.get("is_enemy", false)):
		faction_text = "敌方"
	else:
		faction_text = "我方"
	_target_faction_label.text = faction_text

	var hp_current := int(target_unit.get("hp_current", 0))
	var hp_max := maxi(int(target_unit.get("hp_max", 1)), 1)
	_target_hp_bar.min_value = 0
	_target_hp_bar.max_value = hp_max
	_target_hp_bar.value = clampi(hp_current, 0, hp_max)
	_target_hp_label.text = "HP %d/%d" % [hp_current, hp_max]


func _refresh_hit_stages(stage_rates: Array) -> void:
	for child in _hit_stage_row.get_children():
		_hit_stage_row.remove_child(child)
		child.queue_free()
	if stage_rates.is_empty():
		_hit_stage_row.visible = false
		return
	_hit_stage_row.visible = true
	for rate_variant in stage_rates:
		var rate := int(rate_variant)
		_hit_stage_row.add_child(_build_hit_stage_segment(rate))


func _refresh_hit_summary(summary_text: String) -> void:
	if summary_text.is_empty():
		_hit_summary_label.visible = false
		_hit_summary_label.text = ""
		return
	_hit_summary_label.visible = true
	_hit_summary_label.text = summary_text


func _refresh_fate_badges(badges: Array) -> void:
	for child in _fate_badge_row.get_children():
		_fate_badge_row.remove_child(child)
		child.queue_free()
	if badges.is_empty():
		_fate_badge_row.visible = false
		return
	_fate_badge_row.visible = true
	for badge_variant in badges:
		var badge := badge_variant as Dictionary
		if badge == null or badge.is_empty():
			continue
		_fate_badge_row.add_child(_build_fate_badge(badge))


func _refresh_damage_label(damage_min: int, damage_max: int, damage_text: String) -> void:
	if damage_max <= 0 and damage_text.is_empty():
		_damage_label.visible = false
		_damage_label.text = ""
		return
	_damage_label.visible = true
	if damage_max > 0:
		if damage_min == damage_max:
			_damage_label.text = "伤害 %d" % damage_max
		else:
			_damage_label.text = "伤害 %d-%d" % [damage_min, damage_max]
	else:
		_damage_label.text = damage_text


func _refresh_invalid_label(should_show: bool) -> void:
	_invalid_label.visible = should_show


func _build_hit_stage_segment(rate_percent: int) -> Control:
	var rect := ColorRect.new()
	rect.custom_minimum_size = Vector2(HIT_STAGE_SEGMENT_WIDTH, HIT_STAGE_SEGMENT_HEIGHT)
	rect.color = _hit_stage_color(rate_percent)
	rect.tooltip_text = "命中 %d%%" % clampi(rate_percent, 0, 100)
	return rect


func _build_fate_badge(badge: Dictionary) -> Control:
	var container := PanelContainer.new()
	container.add_theme_stylebox_override("panel", _build_fate_badge_style(StringName(badge.get("tone", &"calm"))))
	var label := Label.new()
	label.text = String(badge.get("text", ""))
	label.add_theme_font_size_override("font_size", BattleUiTheme.FONT_CAPTION)
	label.add_theme_color_override("font_color", BattleUiTheme.TEXT_PRIMARY)
	container.add_child(label)
	var tooltip_text := String(badge.get("tooltip_text", ""))
	if not tooltip_text.is_empty():
		container.tooltip_text = tooltip_text
	return container


static func _hit_stage_color(rate_percent: int) -> Color:
	var clamped := clampi(rate_percent, 0, 100)
	if clamped >= 65:
		return BattleUiTheme.FATE_CALM
	if clamped >= 35:
		return BattleUiTheme.FATE_WARNING
	return BattleUiTheme.FATE_DANGER


func _build_panel_style() -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = BattleUiTheme.PANEL_BG_DEEP
	style.border_color = BattleUiTheme.PANEL_EDGE
	style.set_border_width_all(BattleUiTheme.PANEL_BORDER)
	style.corner_radius_top_left = BattleUiTheme.PANEL_RADIUS_SMALL
	style.corner_radius_top_right = BattleUiTheme.PANEL_RADIUS_SMALL
	style.corner_radius_bottom_left = BattleUiTheme.PANEL_RADIUS_SMALL
	style.corner_radius_bottom_right = BattleUiTheme.PANEL_RADIUS_SMALL
	style.content_margin_left = 10
	style.content_margin_right = 10
	style.content_margin_top = 8
	style.content_margin_bottom = 8
	return style


func _build_progress_background_style() -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = BattleUiTheme.PANEL_BG_ALT
	style.set_border_width_all(0)
	style.corner_radius_top_left = 2
	style.corner_radius_top_right = 2
	style.corner_radius_bottom_left = 2
	style.corner_radius_bottom_right = 2
	return style


func _build_progress_fill_style(fill_color: Color) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = fill_color
	style.set_border_width_all(0)
	style.corner_radius_top_left = 2
	style.corner_radius_top_right = 2
	style.corner_radius_bottom_left = 2
	style.corner_radius_bottom_right = 2
	return style


func _build_fate_badge_style(tone: StringName) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = BattleUiTheme.PANEL_BG
	style.border_color = BattleUiTheme.fate_color(tone)
	style.set_border_width_all(1)
	style.corner_radius_top_left = BattleUiTheme.PANEL_RADIUS_TINY
	style.corner_radius_top_right = BattleUiTheme.PANEL_RADIUS_TINY
	style.corner_radius_bottom_left = BattleUiTheme.PANEL_RADIUS_TINY
	style.corner_radius_bottom_right = BattleUiTheme.PANEL_RADIUS_TINY
	style.content_margin_left = 6
	style.content_margin_right = 6
	style.content_margin_top = 2
	style.content_margin_bottom = 2
	return style
