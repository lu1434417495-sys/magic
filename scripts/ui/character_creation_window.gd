## 文件说明：该脚本属于建卡窗口相关的界面窗口脚本，集中维护姓名阶段、属性阶段、5D3-1 属性掷骰与 reroll 循环的顶层字段，同时承载属性翻滚动画、阈值命中高光、扫光指示与出生运势档位展示等界面美化逻辑。
## 审查重点：重点核对 reroll 循环的让帧、停止信号、预期下限判定与 CharacterCreationService.map_reroll_count_to_hidden_luck_at_birth 档位口径是否一致，以及节流刷新和动画 tween 在 hide_window/cancel 时是否被正确清理。
## 备注：后续如果调整掷骰骰式、预期下限上限、属性集合或运势档位，需要同步检查 CharacterCreationService 与 GameSession 建卡 override 流程，以及 _luck_tier_glyphs/_luck_tier_color 的档位映射。

class_name CharacterCreationWindow
extends Control

## 信号说明：当用户完成建卡并确认后发出，携带姓名、六属性与 reroll 次数。
signal character_confirmed(payload: Dictionary)
## 信号说明：当用户主动取消建卡流程时发出，供外层恢复登录界面。
signal cancelled

const ATTRIBUTE_ORDER: Array[StringName] = [
	UnitBaseAttributes.STRENGTH,
	UnitBaseAttributes.AGILITY,
	UnitBaseAttributes.CONSTITUTION,
	UnitBaseAttributes.PERCEPTION,
	UnitBaseAttributes.INTELLIGENCE,
	UnitBaseAttributes.WILLPOWER,
]
const ATTRIBUTE_DISPLAY_NAMES := {
	UnitBaseAttributes.STRENGTH: "力量",
	UnitBaseAttributes.AGILITY: "敏捷",
	UnitBaseAttributes.CONSTITUTION: "体质",
	UnitBaseAttributes.PERCEPTION: "感知",
	UnitBaseAttributes.INTELLIGENCE: "智力",
	UnitBaseAttributes.WILLPOWER: "意志",
}
const DICE_COUNT := 5
const DICE_SIDES := 3
const DICE_OFFSET := -1
const DICE_VALUE_FLOOR := 4
const DICE_MIN_TOTAL := DICE_VALUE_FLOOR
const DICE_MAX_TOTAL := DICE_COUNT * DICE_SIDES + DICE_OFFSET
const DEFAULT_NAME := "主角"

const LABEL_REFRESH_INTERVAL_MS := 60
const TUMBLE_STEPS := 6
const TUMBLE_INTERVAL_SEC := 0.05
const ROW_FLASH_DURATION_SEC := 0.6
const SCAN_LIGHT_PERIOD_SEC := 0.7

@onready var shade: ColorRect = $Shade
@onready var name_phase: Control = %NamePhase
@onready var attribute_phase: Control = %AttributePhase
@onready var name_input: LineEdit = %NameInput
@onready var name_confirm_button: Button = %NameConfirmButton
@onready var name_cancel_button: Button = %NameCancelButton
@onready var reroll_count_label: Label = %RerollCountLabel
@onready var luck_tier_label: Label = %LuckTierLabel
@onready var name_preview_label: Label = %NamePreviewLabel
@onready var reroll_button: Button = %RerollButton
@onready var stop_button: Button = %StopButton
@onready var confirm_button: Button = %ConfirmButton
@onready var attribute_cancel_button: Button = %AttributeCancelButton
@onready var scan_container: Control = %ScanContainer
@onready var scan_light: ColorRect = %ScanLight

var _rng := RandomNumberGenerator.new()
var _rerolling := false
var _stop_requested := false
var _reroll_count := 0
var _rolled_attributes: Dictionary = {}
var _player_name: String = ""
var _attribute_value_labels: Dictionary = {}
var _attribute_threshold_spinboxes: Dictionary = {}
var _attribute_row_panels: Dictionary = {}
var _row_previous_met: Dictionary = {}
var _row_value_tweens: Dictionary = {}
var _scan_tween: Tween
var _last_label_refresh_msec: int = 0
var _row_style_normal: StyleBoxFlat
var _row_style_met: StyleBoxFlat


func _ready() -> void:
	_rng.randomize()
	_build_row_styles()
	_cache_attribute_rows()
	_apply_button_palettes()

	name_confirm_button.pressed.connect(_on_name_confirmed)
	name_cancel_button.pressed.connect(_cancel)
	name_input.text_submitted.connect(_on_name_text_submitted)
	reroll_button.pressed.connect(_on_reroll_pressed)
	stop_button.pressed.connect(_on_stop_pressed)
	confirm_button.pressed.connect(_on_confirm_pressed)
	attribute_cancel_button.pressed.connect(_cancel)
	scan_container.resized.connect(_on_scan_container_resized)

	hide_window()


func _unhandled_input(event: InputEvent) -> void:
	if not visible:
		return
	if event is not InputEventKey:
		return

	var key_event := event as InputEventKey
	if not key_event.pressed or key_event.echo:
		return

	if key_event.keycode == KEY_ESCAPE:
		get_viewport().set_input_as_handled()
		_cancel()


func show_window() -> void:
	visible = true
	_rerolling = false
	_stop_requested = false
	_reroll_count = 0
	_rolled_attributes.clear()
	_row_previous_met.clear()
	_player_name = ""
	name_input.text = ""
	for attribute_id in ATTRIBUTE_ORDER:
		var spinbox: SpinBox = _attribute_threshold_spinboxes.get(attribute_id)
		if spinbox != null:
			spinbox.value = DICE_MIN_TOTAL
		var label: Label = _attribute_value_labels.get(attribute_id)
		if label != null:
			label.text = "—"
			label.add_theme_color_override("font_color", Color(0.7, 0.75, 0.85))
		var panel: PanelContainer = _attribute_row_panels.get(attribute_id)
		if panel != null:
			panel.modulate = Color(1, 1, 1)
		_apply_row_state(attribute_id, false)
	name_phase.visible = true
	attribute_phase.visible = false
	_refresh_reroll_count_label()
	_refresh_luck_tier_indicator()
	_set_rerolling_visuals(false)
	name_input.grab_focus()


func hide_window() -> void:
	visible = false
	_rerolling = false
	_stop_requested = false
	_reroll_count = 0
	_rolled_attributes.clear()
	_player_name = ""
	_set_rerolling_visuals(false)
	_kill_all_value_tweens()
	for attribute_id in ATTRIBUTE_ORDER:
		var panel: PanelContainer = _attribute_row_panels.get(attribute_id)
		if panel != null:
			panel.modulate = Color(1, 1, 1)


func _cache_attribute_rows() -> void:
	_attribute_value_labels = {
		UnitBaseAttributes.STRENGTH: %StrengthValueLabel,
		UnitBaseAttributes.AGILITY: %AgilityValueLabel,
		UnitBaseAttributes.CONSTITUTION: %ConstitutionValueLabel,
		UnitBaseAttributes.PERCEPTION: %PerceptionValueLabel,
		UnitBaseAttributes.INTELLIGENCE: %IntelligenceValueLabel,
		UnitBaseAttributes.WILLPOWER: %WillpowerValueLabel,
	}
	_attribute_threshold_spinboxes = {
		UnitBaseAttributes.STRENGTH: %StrengthThresholdSpinBox,
		UnitBaseAttributes.AGILITY: %AgilityThresholdSpinBox,
		UnitBaseAttributes.CONSTITUTION: %ConstitutionThresholdSpinBox,
		UnitBaseAttributes.PERCEPTION: %PerceptionThresholdSpinBox,
		UnitBaseAttributes.INTELLIGENCE: %IntelligenceThresholdSpinBox,
		UnitBaseAttributes.WILLPOWER: %WillpowerThresholdSpinBox,
	}
	_attribute_row_panels = {
		UnitBaseAttributes.STRENGTH: %StrengthRow,
		UnitBaseAttributes.AGILITY: %AgilityRow,
		UnitBaseAttributes.CONSTITUTION: %ConstitutionRow,
		UnitBaseAttributes.PERCEPTION: %PerceptionRow,
		UnitBaseAttributes.INTELLIGENCE: %IntelligenceRow,
		UnitBaseAttributes.WILLPOWER: %WillpowerRow,
	}
	for attribute_id in ATTRIBUTE_ORDER:
		var spinbox: SpinBox = _attribute_threshold_spinboxes.get(attribute_id)
		if spinbox != null:
			spinbox.min_value = DICE_MIN_TOTAL
			spinbox.max_value = DICE_MAX_TOTAL
			spinbox.step = 1
			spinbox.rounded = true
			spinbox.allow_greater = false
			spinbox.allow_lesser = false
			spinbox.value = DICE_MIN_TOTAL
			spinbox.value_changed.connect(_on_threshold_value_changed.bind(spinbox))
		_apply_row_state(attribute_id, false)


func _build_row_styles() -> void:
	_row_style_normal = StyleBoxFlat.new()
	_row_style_normal.bg_color = Color(0.10, 0.13, 0.20, 0.65)
	_row_style_normal.border_color = Color(0.32, 0.4, 0.55, 0.45)
	_row_style_normal.set_border_width_all(1)
	_row_style_normal.set_corner_radius_all(6)
	_row_style_normal.content_margin_left = 12
	_row_style_normal.content_margin_right = 12
	_row_style_normal.content_margin_top = 6
	_row_style_normal.content_margin_bottom = 6

	_row_style_met = StyleBoxFlat.new()
	_row_style_met.bg_color = Color(0.18, 0.14, 0.07, 0.92)
	_row_style_met.border_color = Color(1.0, 0.78, 0.32, 1.0)
	_row_style_met.set_border_width_all(2)
	_row_style_met.set_corner_radius_all(6)
	_row_style_met.shadow_color = Color(1.0, 0.7, 0.2, 0.4)
	_row_style_met.shadow_size = 8
	_row_style_met.content_margin_left = 12
	_row_style_met.content_margin_right = 12
	_row_style_met.content_margin_top = 6
	_row_style_met.content_margin_bottom = 6


func _apply_button_palettes() -> void:
	var subdued := Color(0.22, 0.24, 0.32)
	var primary := Color(0.27, 0.32, 0.5)
	var danger := Color(0.55, 0.18, 0.18)
	var emphasis := Color(0.65, 0.45, 0.18)
	_apply_button_palette(name_cancel_button, subdued, Color(0.85, 0.88, 0.95))
	_apply_button_palette(attribute_cancel_button, subdued, Color(0.85, 0.88, 0.95))
	_apply_button_palette(reroll_button, primary, Color(0.95, 0.95, 1.0))
	_apply_button_palette(stop_button, danger, Color(1.0, 0.92, 0.85))
	_apply_button_palette(confirm_button, emphasis, Color(1.0, 0.96, 0.82))
	_apply_button_palette(name_confirm_button, emphasis, Color(1.0, 0.96, 0.82))


func _apply_button_palette(button: Button, base_color: Color, text_color: Color) -> void:
	if button == null:
		return
	var normal := _make_button_stylebox(base_color)
	normal.border_color = base_color.lightened(0.18)
	var hover := _make_button_stylebox(base_color.lightened(0.12))
	hover.border_color = base_color.lightened(0.4)
	var pressed := _make_button_stylebox(base_color.darkened(0.18))
	pressed.border_color = base_color.darkened(0.05)
	var focus := _make_button_stylebox(Color(0, 0, 0, 0))
	focus.border_color = Color(1.0, 0.85, 0.4)
	focus.set_border_width_all(2)
	var disabled := _make_button_stylebox(Color(0.18, 0.18, 0.22, 0.85))
	disabled.border_color = Color(0.3, 0.3, 0.35, 0.5)
	button.add_theme_stylebox_override("normal", normal)
	button.add_theme_stylebox_override("hover", hover)
	button.add_theme_stylebox_override("pressed", pressed)
	button.add_theme_stylebox_override("focus", focus)
	button.add_theme_stylebox_override("disabled", disabled)
	button.add_theme_color_override("font_color", text_color)
	button.add_theme_color_override("font_hover_color", text_color)
	button.add_theme_color_override("font_pressed_color", text_color)
	button.add_theme_color_override("font_focus_color", text_color)
	button.add_theme_color_override("font_disabled_color", Color(0.55, 0.55, 0.6))


func _make_button_stylebox(bg: Color) -> StyleBoxFlat:
	var sb := StyleBoxFlat.new()
	sb.bg_color = bg
	sb.set_corner_radius_all(8)
	sb.set_content_margin_all(10)
	sb.set_border_width_all(1)
	return sb


func _on_name_text_submitted(_text: String) -> void:
	_on_name_confirmed()


func _on_name_confirmed() -> void:
	if _rerolling:
		return
	if not name_phase.visible:
		return
	_player_name = _resolve_name_input()
	_enter_attribute_phase()


func _resolve_name_input() -> String:
	var text := name_input.text.strip_edges()
	if text.is_empty():
		return DEFAULT_NAME
	return text


func _enter_attribute_phase() -> void:
	name_phase.visible = false
	attribute_phase.visible = true
	name_preview_label.text = "角色名：%s" % _player_name
	_reroll_count = 0
	_row_previous_met.clear()
	_roll_once_silent()
	_refresh_reroll_count_label()
	_refresh_luck_tier_indicator()
	_animate_all_value_tumbles()
	_update_button_states()
	reroll_button.grab_focus()


func _roll_once_silent() -> void:
	for attribute_id in ATTRIBUTE_ORDER:
		_rolled_attributes[attribute_id] = _roll_attribute_value()


func _roll_attribute_value() -> int:
	var total := DICE_OFFSET
	for _i in DICE_COUNT:
		total += _rng.randi_range(1, DICE_SIDES)
	return maxi(DICE_VALUE_FLOOR, total)


func _refresh_attribute_value_labels() -> void:
	for attribute_id in ATTRIBUTE_ORDER:
		var value := int(_rolled_attributes.get(attribute_id, 0))
		_set_value_label(attribute_id, value, true)
		var met := _row_meets_threshold(attribute_id)
		var was_met := bool(_row_previous_met.get(attribute_id, false))
		_apply_row_state(attribute_id, met)
		if met and not was_met and not _rerolling:
			_flash_row(attribute_id)
		_row_previous_met[attribute_id] = met


func _set_value_label(attribute_id: StringName, value: int, final: bool) -> void:
	var label: Label = _attribute_value_labels.get(attribute_id)
	if label == null:
		return
	label.text = str(value)
	if final:
		label.add_theme_color_override("font_color", _value_tier_color(value))
	else:
		label.add_theme_color_override("font_color", Color(0.78, 0.82, 0.92))


func _value_tier_color(value: int) -> Color:
	if value <= 6:
		return Color(0.6, 0.62, 0.66)
	if value <= 9:
		return Color(0.92, 0.94, 0.97)
	if value <= 11:
		return Color(0.95, 0.85, 0.55)
	if value <= 13:
		return Color(1.0, 0.8, 0.3)
	return Color(1.0, 0.66, 0.2)


func _row_meets_threshold(attribute_id: StringName) -> bool:
	var spinbox: SpinBox = _attribute_threshold_spinboxes.get(attribute_id)
	if spinbox == null:
		return false
	var threshold := int(spinbox.value)
	if threshold <= DICE_MIN_TOTAL:
		return false
	return int(_rolled_attributes.get(attribute_id, 0)) >= threshold


func _apply_row_state(attribute_id: StringName, met: bool) -> void:
	var panel: PanelContainer = _attribute_row_panels.get(attribute_id)
	if panel == null:
		return
	if met:
		panel.add_theme_stylebox_override("panel", _row_style_met)
	else:
		panel.add_theme_stylebox_override("panel", _row_style_normal)


func _flash_row(attribute_id: StringName) -> void:
	var panel: PanelContainer = _attribute_row_panels.get(attribute_id)
	if panel == null:
		return
	panel.modulate = Color(1.55, 1.4, 1.05)
	var tween := create_tween()
	tween.tween_property(panel, "modulate", Color(1, 1, 1), ROW_FLASH_DURATION_SEC).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_OUT)


func _animate_all_value_tumbles() -> void:
	for attribute_id in ATTRIBUTE_ORDER:
		var target := int(_rolled_attributes.get(attribute_id, 0))
		_animate_value_tumble(attribute_id, target)


func _kill_all_value_tweens() -> void:
	for attribute_id in ATTRIBUTE_ORDER:
		var existing: Tween = _row_value_tweens.get(attribute_id)
		if existing != null and existing.is_valid():
			existing.kill()
		_row_value_tweens[attribute_id] = null


func _animate_value_tumble(attribute_id: StringName, target_value: int) -> void:
	var label: Label = _attribute_value_labels.get(attribute_id)
	if label == null:
		return
	var existing: Tween = _row_value_tweens.get(attribute_id)
	if existing != null and existing.is_valid():
		existing.kill()
	var tween := create_tween()
	_row_value_tweens[attribute_id] = tween
	for i in TUMBLE_STEPS:
		var sample := _rng.randi_range(DICE_VALUE_FLOOR, DICE_MAX_TOTAL)
		tween.tween_callback(_set_value_label.bind(attribute_id, sample, false))
		tween.tween_interval(TUMBLE_INTERVAL_SEC)
	tween.tween_callback(_set_value_label.bind(attribute_id, target_value, true))
	tween.tween_callback(_finalize_row_state.bind(attribute_id))


func _finalize_row_state(attribute_id: StringName) -> void:
	var met := _row_meets_threshold(attribute_id)
	var was_met := bool(_row_previous_met.get(attribute_id, false))
	_apply_row_state(attribute_id, met)
	if met and not was_met:
		_flash_row(attribute_id)
	_row_previous_met[attribute_id] = met


func _refresh_reroll_count_label() -> void:
	reroll_count_label.text = "Reroll 次数：%d" % _reroll_count


func _refresh_luck_tier_indicator() -> void:
	var tier := CharacterCreationService.map_reroll_count_to_hidden_luck_at_birth(_reroll_count)
	luck_tier_label.text = "出生运势 %+d  %s" % [tier, _luck_tier_glyphs(tier)]
	luck_tier_label.add_theme_color_override("font_color", _luck_tier_color(tier))


func _luck_tier_glyphs(tier: int) -> String:
	if tier >= 2:
		return "✦✦✦✦✦"
	if tier == 1:
		return "✦✦✦✦·"
	if tier == 0:
		return "✦✦✦··"
	if tier == -1:
		return "✦✦···"
	if tier == -2:
		return "✦····"
	return "·····"


func _luck_tier_color(tier: int) -> Color:
	if tier >= 2:
		return Color(1.0, 0.85, 0.4)
	if tier == 1:
		return Color(0.95, 0.85, 0.55)
	if tier == 0:
		return Color(0.92, 0.93, 0.95)
	if tier == -1:
		return Color(0.7, 0.7, 0.7)
	if tier == -2:
		return Color(0.6, 0.55, 0.5)
	return Color(0.7, 0.4, 0.35)


func _update_button_states() -> void:
	reroll_button.disabled = _rerolling
	stop_button.disabled = not _rerolling
	confirm_button.disabled = _rerolling
	attribute_cancel_button.disabled = _rerolling
	name_confirm_button.disabled = _rerolling
	name_cancel_button.disabled = _rerolling
	for attribute_id in ATTRIBUTE_ORDER:
		var spinbox: SpinBox = _attribute_threshold_spinboxes.get(attribute_id)
		if spinbox != null:
			spinbox.editable = not _rerolling


func _has_any_threshold() -> bool:
	for attribute_id in ATTRIBUTE_ORDER:
		var spinbox: SpinBox = _attribute_threshold_spinboxes.get(attribute_id)
		if spinbox == null:
			continue
		if int(spinbox.value) > DICE_MIN_TOTAL:
			return true
	return false


func _meets_thresholds() -> bool:
	for attribute_id in ATTRIBUTE_ORDER:
		var spinbox: SpinBox = _attribute_threshold_spinboxes.get(attribute_id)
		if spinbox == null:
			continue
		var threshold := int(spinbox.value)
		if threshold <= DICE_MIN_TOTAL:
			continue
		if int(_rolled_attributes.get(attribute_id, 0)) < threshold:
			return false
	return true


func _on_threshold_value_changed(value: float, spinbox: SpinBox) -> void:
	if spinbox == null:
		return
	var clamped_value := clampi(int(round(value)), DICE_MIN_TOTAL, DICE_MAX_TOTAL)
	if int(spinbox.value) == clamped_value:
		return
	spinbox.set_value_no_signal(clamped_value)


func _on_reroll_pressed() -> void:
	if _rerolling:
		return
	if _has_any_threshold():
		await _run_continuous_reroll()
		return
	_reroll_count += 1
	_roll_once_silent()
	_refresh_reroll_count_label()
	_refresh_luck_tier_indicator()
	_animate_all_value_tumbles()


func _on_stop_pressed() -> void:
	if not _rerolling:
		return
	_stop_requested = true


func _run_continuous_reroll() -> void:
	_rerolling = true
	_stop_requested = false
	_last_label_refresh_msec = 0
	_kill_all_value_tweens()
	_set_rerolling_visuals(true)
	_update_button_states()
	while true:
		_reroll_count += 1
		_roll_once_silent()
		if _meets_thresholds():
			break
		if _stop_requested:
			break
		var now := Time.get_ticks_msec()
		if now - _last_label_refresh_msec >= LABEL_REFRESH_INTERVAL_MS:
			_refresh_attribute_value_labels()
			_refresh_reroll_count_label()
			_refresh_luck_tier_indicator()
			_last_label_refresh_msec = now
		await get_tree().process_frame
	_rerolling = false
	_stop_requested = false
	_set_rerolling_visuals(false)
	_refresh_attribute_value_labels()
	_refresh_reroll_count_label()
	_refresh_luck_tier_indicator()
	_update_button_states()


func _set_rerolling_visuals(active: bool) -> void:
	if scan_light == null or scan_container == null:
		return
	if _scan_tween != null and _scan_tween.is_valid():
		_scan_tween.kill()
		_scan_tween = null
	scan_light.visible = active
	if active:
		_start_scan_tween()


func _on_scan_container_resized() -> void:
	if not _rerolling:
		return
	if _scan_tween != null and _scan_tween.is_valid():
		_scan_tween.kill()
		_scan_tween = null
	_start_scan_tween()


func _start_scan_tween() -> void:
	if scan_light == null or scan_container == null:
		return
	var max_x := maxf(scan_container.size.x - scan_light.size.x, 0.0)
	scan_light.position.x = 0.0
	_scan_tween = create_tween().set_loops()
	_scan_tween.tween_property(scan_light, "position:x", max_x, SCAN_LIGHT_PERIOD_SEC).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	_scan_tween.tween_property(scan_light, "position:x", 0.0, SCAN_LIGHT_PERIOD_SEC).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)


func _on_confirm_pressed() -> void:
	if _rerolling:
		return
	if _rolled_attributes.is_empty():
		return
	var payload := {
		"display_name": _player_name,
		"reroll_count": _reroll_count,
		"strength": int(_rolled_attributes.get(UnitBaseAttributes.STRENGTH, 0)),
		"agility": int(_rolled_attributes.get(UnitBaseAttributes.AGILITY, 0)),
		"constitution": int(_rolled_attributes.get(UnitBaseAttributes.CONSTITUTION, 0)),
		"perception": int(_rolled_attributes.get(UnitBaseAttributes.PERCEPTION, 0)),
		"intelligence": int(_rolled_attributes.get(UnitBaseAttributes.INTELLIGENCE, 0)),
		"willpower": int(_rolled_attributes.get(UnitBaseAttributes.WILLPOWER, 0)),
	}
	hide_window()
	character_confirmed.emit(payload)


func _cancel() -> void:
	if _rerolling:
		_stop_requested = true
		return
	hide_window()
	cancelled.emit()
