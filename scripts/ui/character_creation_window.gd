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
const DEFAULT_CREATION_RACE_ID: StringName = &"human"
const DEFAULT_CREATION_AGE_STAGE_ID: StringName = &"adult"
const HUMAN_VERSATILITY_TRAIT_ID: StringName = &"human_versatility"

const PROGRESSION_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/player/progression/progression_content_registry.gd")
const BODY_SIZE_RULES_SCRIPT = preload("res://scripts/systems/progression/body_size_rules.gd")
const CHARACTER_CREATION_IDENTITY_OPTION_SERVICE_SCRIPT = preload("res://scripts/systems/progression/character_creation_identity_option_service.gd")
const BodySizeRules = BODY_SIZE_RULES_SCRIPT

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
@onready var content_root: VBoxContainer = $CenterContainer/Panel/Margin/Content
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
var _progression_content_registry = PROGRESSION_CONTENT_REGISTRY_SCRIPT.new()
var _race_ids: Array[StringName] = []
var _subrace_ids: Array[StringName] = []
var _age_stage_ids: Array[StringName] = []
var _selected_race_id: StringName = &""
var _selected_subrace_id: StringName = &""
var _selected_age_stage_id: StringName = &""
var _selected_age_years: int = 0
var _selected_versatility_pick: StringName = UnitBaseAttributes.STRENGTH

var race_phase: VBoxContainer
var age_phase: VBoxContainer
var identity_options_phase: VBoxContainer
var race_option_button: OptionButton
var subrace_option_button: OptionButton
var age_stage_option_button: OptionButton
var versatility_option_button: OptionButton
var race_preview_label: Label
var age_preview_label: Label
var identity_options_label: Label
var final_attribute_preview_label: Label
var race_back_button: Button
var race_next_button: Button
var age_back_button: Button
var age_next_button: Button
var options_back_button: Button
var final_confirm_button: Button


func _ready() -> void:
	_rng.randomize()
	_build_row_styles()
	_cache_attribute_rows()
	_build_identity_phase_nodes()
	_apply_button_palettes()

	name_confirm_button.pressed.connect(_on_name_confirmed)
	name_cancel_button.pressed.connect(_cancel)
	name_input.text_submitted.connect(_on_name_text_submitted)
	reroll_button.pressed.connect(_on_reroll_pressed)
	stop_button.pressed.connect(_on_stop_pressed)
	confirm_button.pressed.connect(_on_attribute_next_pressed)
	attribute_cancel_button.pressed.connect(_cancel)
	scan_container.resized.connect(_on_scan_container_resized)

	hide_window()


func set_progression_content_registry(registry) -> void:
	if registry == null:
		return
	_progression_content_registry = registry
	_rebuild_creation_identity_options()


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
	_rebuild_creation_identity_options()
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
	_hide_identity_phases()
	_refresh_reroll_count_label()
	_refresh_luck_tier_indicator()
	_set_rerolling_visuals(false)
	_update_button_states()
	name_input.grab_focus()


func hide_window() -> void:
	visible = false
	_rerolling = false
	_stop_requested = false
	_reroll_count = 0
	_rolled_attributes.clear()
	_player_name = ""
	_hide_identity_phases()
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
	for button in [
		race_back_button,
		age_back_button,
		options_back_button,
	]:
		_apply_button_palette(button, subdued, Color(0.85, 0.88, 0.95))
	for button in [
		race_next_button,
		age_next_button,
		final_confirm_button,
	]:
		_apply_button_palette(button, emphasis, Color(1.0, 0.96, 0.82))


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
	_hide_identity_phases()
	name_preview_label.text = "角色名：%s" % _player_name
	confirm_button.text = "下一步"
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
	var identity_valid := not _build_selected_identity_payload().is_empty()
	if race_next_button != null:
		race_next_button.disabled = not identity_valid
	if age_next_button != null:
		age_next_button.disabled = not identity_valid
	if final_confirm_button != null:
		final_confirm_button.disabled = not identity_valid


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


func _build_identity_phase_nodes() -> void:
	race_phase = _make_phase_container("RaceAndSubracePhase")
	race_phase.add_child(_make_phase_label("种族与亚种", 20))
	race_option_button = _make_option_button()
	subrace_option_button = _make_option_button()
	race_phase.add_child(_make_labeled_option_row("种族", race_option_button))
	race_phase.add_child(_make_labeled_option_row("亚种", subrace_option_button))
	race_preview_label = _make_phase_label("", 14)
	race_phase.add_child(race_preview_label)
	var race_buttons := _make_button_row()
	race_back_button = _make_phase_button("上一步")
	race_next_button = _make_phase_button("下一步")
	race_buttons.add_child(race_back_button)
	race_buttons.add_child(race_next_button)
	race_phase.add_child(race_buttons)
	content_root.add_child(race_phase)

	age_phase = _make_phase_container("AgePhase")
	age_phase.add_child(_make_phase_label("年龄阶段", 20))
	age_stage_option_button = _make_option_button()
	age_phase.add_child(_make_labeled_option_row("阶段", age_stage_option_button))
	age_preview_label = _make_phase_label("", 14)
	age_phase.add_child(age_preview_label)
	var age_buttons := _make_button_row()
	age_back_button = _make_phase_button("上一步")
	age_next_button = _make_phase_button("下一步")
	age_buttons.add_child(age_back_button)
	age_buttons.add_child(age_next_button)
	age_phase.add_child(age_buttons)
	content_root.add_child(age_phase)

	identity_options_phase = _make_phase_container("IdentityOptionsPhase")
	identity_options_phase.add_child(_make_phase_label("身份选项", 20))
	versatility_option_button = _make_option_button()
	identity_options_phase.add_child(_make_labeled_option_row("适应属性", versatility_option_button))
	identity_options_label = _make_phase_label("", 14)
	final_attribute_preview_label = _make_phase_label("", 14)
	identity_options_phase.add_child(identity_options_label)
	identity_options_phase.add_child(final_attribute_preview_label)
	var options_buttons := _make_button_row()
	options_back_button = _make_phase_button("上一步")
	final_confirm_button = _make_phase_button("确认")
	options_buttons.add_child(options_back_button)
	options_buttons.add_child(final_confirm_button)
	identity_options_phase.add_child(options_buttons)
	content_root.add_child(identity_options_phase)

	race_option_button.item_selected.connect(_on_race_option_selected)
	subrace_option_button.item_selected.connect(_on_subrace_option_selected)
	age_stage_option_button.item_selected.connect(_on_age_stage_option_selected)
	versatility_option_button.item_selected.connect(_on_versatility_option_selected)
	race_back_button.pressed.connect(_enter_attribute_phase_from_back)
	race_next_button.pressed.connect(_enter_age_phase)
	age_back_button.pressed.connect(_enter_race_phase)
	age_next_button.pressed.connect(_enter_identity_options_phase)
	options_back_button.pressed.connect(_enter_age_phase)
	final_confirm_button.pressed.connect(_on_confirm_pressed)


func _make_phase_container(node_name: String) -> VBoxContainer:
	var container := VBoxContainer.new()
	container.name = node_name
	container.visible = false
	container.add_theme_constant_override("separation", 12)
	return container


func _make_phase_label(text: String, font_size: int) -> Label:
	var label := Label.new()
	label.text = text
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	label.add_theme_color_override("font_color", Color(0.85, 0.92, 1.0))
	label.add_theme_font_size_override("font_size", font_size)
	return label


func _make_option_button() -> OptionButton:
	var option_button := OptionButton.new()
	option_button.custom_minimum_size = Vector2(280, 36)
	option_button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	return option_button


func _make_labeled_option_row(label_text: String, option_button: OptionButton) -> HBoxContainer:
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 12)
	var label := Label.new()
	label.custom_minimum_size = Vector2(96, 0)
	label.text = label_text
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.add_theme_color_override("font_color", Color(0.95, 0.95, 0.95))
	label.add_theme_font_size_override("font_size", 16)
	row.add_child(label)
	row.add_child(option_button)
	return row


func _make_button_row() -> HBoxContainer:
	var row := HBoxContainer.new()
	row.alignment = BoxContainer.ALIGNMENT_END
	row.add_theme_constant_override("separation", 12)
	return row


func _make_phase_button(text: String) -> Button:
	var button := Button.new()
	button.custom_minimum_size = Vector2(120, 40)
	button.text = text
	return button


func _hide_identity_phases() -> void:
	if race_phase != null:
		race_phase.visible = false
	if age_phase != null:
		age_phase.visible = false
	if identity_options_phase != null:
		identity_options_phase.visible = false


func _rebuild_creation_identity_options() -> void:
	if _progression_content_registry == null:
		_race_ids.clear()
		_subrace_ids.clear()
		_age_stage_ids.clear()
		_selected_race_id = &""
		_selected_subrace_id = &""
		_selected_age_stage_id = &""
		_selected_age_years = 0
		_refresh_identity_option_controls()
		return

	_race_ids = CHARACTER_CREATION_IDENTITY_OPTION_SERVICE_SCRIPT.collect_creation_race_ids(_progression_content_registry)
	_selected_race_id = _choose_race_id(_selected_race_id)
	_selected_subrace_id = _choose_subrace_id(_selected_subrace_id)
	_refresh_age_stage_selection()
	_refresh_versatility_selection()
	_refresh_identity_option_controls()


func _refresh_identity_option_controls() -> void:
	_refresh_race_options()
	_refresh_subrace_options()
	_refresh_age_stage_options()
	_refresh_versatility_options()
	_refresh_identity_previews()
	_update_button_states()


func _choose_race_id(current_id: StringName) -> StringName:
	return CHARACTER_CREATION_IDENTITY_OPTION_SERVICE_SCRIPT.choose_race_id(
		_progression_content_registry,
		current_id,
		DEFAULT_CREATION_RACE_ID
	)


func _choose_subrace_id(current_id: StringName) -> StringName:
	_subrace_ids = _collect_subrace_ids_for_race(_selected_race_id)
	return CHARACTER_CREATION_IDENTITY_OPTION_SERVICE_SCRIPT.choose_subrace_id(
		_progression_content_registry,
		_selected_race_id,
		current_id
	)


func _collect_subrace_ids_for_race(race_id: StringName) -> Array[StringName]:
	return CHARACTER_CREATION_IDENTITY_OPTION_SERVICE_SCRIPT.collect_subrace_ids_for_race(
		_progression_content_registry,
		race_id
	)


func _refresh_age_stage_selection() -> void:
	var age_profile := _get_selected_age_profile()
	_age_stage_ids = _collect_creation_age_stage_ids(age_profile)
	if _age_stage_ids.has(_selected_age_stage_id):
		_selected_age_years = _resolve_default_age_for_stage(age_profile, _selected_age_stage_id)
		return
	if _age_stage_ids.has(DEFAULT_CREATION_AGE_STAGE_ID):
		_selected_age_stage_id = DEFAULT_CREATION_AGE_STAGE_ID
	elif not _age_stage_ids.is_empty():
		_selected_age_stage_id = _age_stage_ids[0]
	else:
		_selected_age_stage_id = &""
	_selected_age_years = _resolve_default_age_for_stage(age_profile, _selected_age_stage_id)


func _collect_creation_age_stage_ids(age_profile: AgeProfileDef) -> Array[StringName]:
	var ids: Array[StringName] = []
	if age_profile == null:
		return ids
	for stage_id in age_profile.creation_stage_ids:
		var stage_rule := _get_age_stage_rule(age_profile, stage_id)
		if stage_rule != null and stage_rule.selectable_in_creation and not ids.has(stage_id):
			ids.append(stage_id)
	if ids.is_empty():
		for rule in age_profile.stage_rules:
			if rule != null and rule.selectable_in_creation and not ids.has(rule.stage_id):
				ids.append(rule.stage_id)
	return ids


func _refresh_versatility_selection() -> void:
	if not _selected_identity_has_human_versatility():
		_selected_versatility_pick = &""
		return
	if not ATTRIBUTE_ORDER.has(_selected_versatility_pick):
		_selected_versatility_pick = UnitBaseAttributes.STRENGTH


func _refresh_race_options() -> void:
	if race_option_button == null:
		return
	race_option_button.clear()
	for race_id in _race_ids:
		var race_def := _get_race_def(race_id)
		var index := race_option_button.get_item_count()
		race_option_button.add_item(_identity_label(race_def, race_id))
		race_option_button.set_item_metadata(index, race_id)
	_select_option_by_metadata(race_option_button, _selected_race_id)
	race_option_button.disabled = race_option_button.get_item_count() <= 1


func _refresh_subrace_options() -> void:
	if subrace_option_button == null:
		return
	subrace_option_button.clear()
	for subrace_id in _subrace_ids:
		var subrace_def := _get_subrace_def(subrace_id)
		var index := subrace_option_button.get_item_count()
		subrace_option_button.add_item(_identity_label(subrace_def, subrace_id))
		subrace_option_button.set_item_metadata(index, subrace_id)
	_select_option_by_metadata(subrace_option_button, _selected_subrace_id)
	subrace_option_button.disabled = subrace_option_button.get_item_count() <= 1


func _refresh_age_stage_options() -> void:
	if age_stage_option_button == null:
		return
	age_stage_option_button.clear()
	var age_profile := _get_selected_age_profile()
	for stage_id in _age_stage_ids:
		var stage_rule := _get_age_stage_rule(age_profile, stage_id)
		var index := age_stage_option_button.get_item_count()
		age_stage_option_button.add_item(_identity_label(stage_rule, stage_id))
		age_stage_option_button.set_item_metadata(index, stage_id)
	_select_option_by_metadata(age_stage_option_button, _selected_age_stage_id)
	age_stage_option_button.disabled = age_stage_option_button.get_item_count() <= 1


func _refresh_versatility_options() -> void:
	if versatility_option_button == null:
		return
	versatility_option_button.clear()
	if not _selected_identity_has_human_versatility():
		versatility_option_button.add_item("无")
		versatility_option_button.set_item_metadata(0, &"")
		versatility_option_button.select(0)
		versatility_option_button.disabled = true
		return
	for attribute_id in ATTRIBUTE_ORDER:
		var index := versatility_option_button.get_item_count()
		versatility_option_button.add_item(_attribute_display_name(attribute_id))
		versatility_option_button.set_item_metadata(index, attribute_id)
	_select_option_by_metadata(versatility_option_button, _selected_versatility_pick)
	versatility_option_button.disabled = false


func _select_option_by_metadata(option_button: OptionButton, target_id: StringName) -> void:
	var index := _find_option_index_by_metadata(option_button, target_id)
	if index >= 0:
		option_button.select(index)


func _find_option_index_by_metadata(option_button: OptionButton, target_id: StringName) -> int:
	if option_button == null:
		return -1
	for index in range(option_button.get_item_count()):
		if StringName(String(option_button.get_item_metadata(index))) == target_id:
			return index
	return -1


func _get_option_metadata(option_button: OptionButton, index: int) -> StringName:
	if option_button == null or index < 0 or index >= option_button.get_item_count():
		return &""
	return StringName(String(option_button.get_item_metadata(index)))


func _on_race_option_selected(index: int) -> void:
	var race_id := _get_option_metadata(race_option_button, index)
	if race_id == &"":
		return
	_selected_race_id = race_id
	_selected_subrace_id = _choose_subrace_id(&"")
	_selected_age_stage_id = &""
	_refresh_age_stage_selection()
	_refresh_versatility_selection()
	_refresh_identity_option_controls()


func _on_subrace_option_selected(index: int) -> void:
	var subrace_id := _get_option_metadata(subrace_option_button, index)
	if subrace_id == &"":
		return
	_selected_subrace_id = subrace_id
	_refresh_versatility_selection()
	_refresh_identity_option_controls()


func _on_age_stage_option_selected(index: int) -> void:
	var stage_id := _get_option_metadata(age_stage_option_button, index)
	if stage_id == &"":
		return
	_selected_age_stage_id = stage_id
	_selected_age_years = _resolve_default_age_for_stage(_get_selected_age_profile(), stage_id)
	_refresh_identity_previews()
	_update_button_states()


func _on_versatility_option_selected(index: int) -> void:
	_selected_versatility_pick = _get_option_metadata(versatility_option_button, index)
	_refresh_identity_previews()


func _on_attribute_next_pressed() -> void:
	if _rerolling:
		return
	if _rolled_attributes.is_empty():
		return
	_enter_race_phase()


func _enter_attribute_phase_from_back() -> void:
	_hide_identity_phases()
	name_phase.visible = false
	attribute_phase.visible = true
	confirm_button.text = "下一步"
	_update_button_states()
	reroll_button.grab_focus()


func _enter_race_phase() -> void:
	if _build_selected_identity_payload().is_empty():
		_rebuild_creation_identity_options()
	name_phase.visible = false
	attribute_phase.visible = false
	_hide_identity_phases()
	race_phase.visible = true
	_refresh_identity_option_controls()
	race_next_button.grab_focus()


func _enter_age_phase() -> void:
	name_phase.visible = false
	attribute_phase.visible = false
	_hide_identity_phases()
	age_phase.visible = true
	_refresh_identity_option_controls()
	age_next_button.grab_focus()


func _enter_identity_options_phase() -> void:
	name_phase.visible = false
	attribute_phase.visible = false
	_hide_identity_phases()
	identity_options_phase.visible = true
	_refresh_identity_option_controls()
	final_confirm_button.grab_focus()


func _refresh_identity_previews() -> void:
	var identity_payload := _build_selected_identity_payload()
	if race_preview_label != null:
		race_preview_label.text = _build_race_preview_text(identity_payload)
	if age_preview_label != null:
		age_preview_label.text = _build_age_preview_text(identity_payload)
	if identity_options_label != null:
		identity_options_label.text = _build_identity_options_text(identity_payload)
	if final_attribute_preview_label != null:
		final_attribute_preview_label.text = _build_attribute_preview_text()


func _build_selected_identity_payload() -> Dictionary:
	if not CHARACTER_CREATION_IDENTITY_OPTION_SERVICE_SCRIPT.is_valid_creation_race_subrace_pair(
		_progression_content_registry,
		_selected_race_id,
		_selected_subrace_id
	):
		return {}

	var race_def := _get_selected_race_def()
	var subrace_def := _get_selected_subrace_def()
	var age_profile := _get_selected_age_profile()
	var age_stage_rule := _get_age_stage_rule(age_profile, _selected_age_stage_id)
	if race_def == null or subrace_def == null or age_profile == null or age_stage_rule == null:
		return {}

	var body_size_category := _resolve_body_size_category_for_selection(race_def, subrace_def)
	var body_size := BodySizeRules.get_body_size_for_category(body_size_category)
	if body_size_category == &"" or body_size <= 0:
		return {}

	var age_years := _selected_age_years
	if age_years <= 0:
		age_years = _resolve_default_age_for_stage(age_profile, age_stage_rule.stage_id)
	var versatility_pick := _selected_versatility_pick if _selected_identity_has_human_versatility() else &""
	return {
		"race_id": race_def.race_id,
		"subrace_id": subrace_def.subrace_id,
		"age_years": age_years,
		"birth_at_world_step": 0,
		"age_profile_id": age_profile.profile_id,
		"natural_age_stage_id": age_stage_rule.stage_id,
		"effective_age_stage_id": age_stage_rule.stage_id,
		"effective_age_stage_source_type": &"",
		"effective_age_stage_source_id": &"",
		"body_size": body_size,
		"body_size_category": body_size_category,
		"versatility_pick": versatility_pick,
		"active_stage_advancement_modifier_ids": [],
		"bloodline_id": &"",
		"bloodline_stage_id": &"",
		"ascension_id": &"",
		"ascension_stage_id": &"",
		"ascension_started_at_world_step": -1,
		"original_race_id_before_ascension": &"",
		"biological_age_years": age_years,
		"astral_memory_years": 0,
	}


func _build_race_preview_text(identity_payload: Dictionary) -> String:
	if identity_payload.is_empty():
		return "身份内容无效，无法继续建卡。"
	var race_def := _get_selected_race_def()
	var subrace_def := _get_selected_subrace_def()
	var lines: Array[String] = [
		"种族：%s" % _identity_label(race_def, _selected_race_id),
		"亚种：%s" % _identity_label(subrace_def, _selected_subrace_id),
		"体型：%s（%d）" % [
			String(identity_payload.get("body_size_category", &"")),
			int(identity_payload.get("body_size", 0)),
		],
	]
	var trait_lines := _collect_trait_summary_lines(false)
	if not trait_lines.is_empty():
		lines.append("特性：%s" % _join_strings(trait_lines, "；"))
	return _join_strings(lines, "\n")


func _build_age_preview_text(identity_payload: Dictionary) -> String:
	if identity_payload.is_empty():
		return "年龄内容无效，无法继续建卡。"
	var age_profile := _get_selected_age_profile()
	var age_stage_rule := _get_age_stage_rule(age_profile, _selected_age_stage_id)
	var lines: Array[String] = [
		"年龄：%d" % int(identity_payload.get("age_years", 0)),
		"自然阶段：%s" % _identity_label(age_stage_rule, _selected_age_stage_id),
		"有效阶段：%s" % _identity_label(age_stage_rule, _selected_age_stage_id),
	]
	var age_traits := _collect_age_trait_summary_lines()
	if not age_traits.is_empty():
		lines.append("阶段特性：%s" % _join_strings(age_traits, "；"))
	return _join_strings(lines, "\n")


func _build_identity_options_text(identity_payload: Dictionary) -> String:
	if identity_payload.is_empty():
		return "身份内容无效，无法继续建卡。"
	var race_def := _get_selected_race_def()
	var subrace_def := _get_selected_subrace_def()
	var age_profile := _get_selected_age_profile()
	var age_stage_rule := _get_age_stage_rule(age_profile, _selected_age_stage_id)
	var lines: Array[String] = [
		"姓名：%s" % _player_name,
		"身份：%s / %s / %s" % [
			_identity_label(race_def, _selected_race_id),
			_identity_label(subrace_def, _selected_subrace_id),
			_identity_label(age_stage_rule, _selected_age_stage_id),
		],
		"体型：%s（%d）" % [
			String(identity_payload.get("body_size_category", &"")),
			int(identity_payload.get("body_size", 0)),
		],
	]
	if _selected_identity_has_human_versatility():
		lines.append("适应属性：%s +1" % _attribute_display_name(StringName(identity_payload.get("versatility_pick", &""))))
	var trait_lines := _collect_trait_summary_lines(true)
	if not trait_lines.is_empty():
		lines.append("特性：%s" % _join_strings(trait_lines, "；"))
	return _join_strings(lines, "\n")


func _build_attribute_preview_text() -> String:
	if _rolled_attributes.is_empty():
		return "属性：尚未掷骰"
	var lines: Array[String] = ["属性预览："]
	for attribute_id in ATTRIBUTE_ORDER:
		var base_value := int(_rolled_attributes.get(attribute_id, 0))
		var final_value := _resolve_preview_attribute_value(attribute_id, base_value)
		if final_value == base_value:
			lines.append("%s：%d" % [_attribute_display_name(attribute_id), base_value])
		else:
			lines.append("%s：%d -> %d (%+d)" % [
				_attribute_display_name(attribute_id),
				base_value,
				final_value,
				final_value - base_value,
			])
	return _join_strings(lines, "\n")


func _resolve_preview_attribute_value(attribute_id: StringName, base_value: int) -> int:
	var totals := {
		"flat": 0,
		"percent": 0,
	}
	var race_def := _get_selected_race_def()
	var subrace_def := _get_selected_subrace_def()
	var age_profile := _get_selected_age_profile()
	var age_stage_rule := _get_age_stage_rule(age_profile, _selected_age_stage_id)
	if race_def != null:
		_accumulate_attribute_modifiers(totals, attribute_id, race_def.attribute_modifiers)
	if subrace_def != null:
		_accumulate_attribute_modifiers(totals, attribute_id, subrace_def.attribute_modifiers)
	if age_stage_rule != null:
		_accumulate_attribute_modifiers(totals, attribute_id, age_stage_rule.attribute_modifiers)
	if _selected_identity_has_human_versatility() and _selected_versatility_pick == attribute_id:
		totals["flat"] = int(totals["flat"]) + 1
	var result := base_value + int(totals["flat"])
	var percent := int(totals["percent"])
	if percent != 0:
		result = int(floor(float(result) * float(100 + percent) / 100.0))
	return result


func _accumulate_attribute_modifiers(totals: Dictionary, attribute_id: StringName, modifiers: Array) -> void:
	for modifier in modifiers:
		var attribute_modifier := modifier as AttributeModifier
		if attribute_modifier == null or attribute_modifier.attribute_id != attribute_id:
			continue
		var value := attribute_modifier.get_value_for_rank(1)
		if attribute_modifier.mode == AttributeModifier.MODE_PERCENT:
			totals["percent"] = int(totals["percent"]) + value
		else:
			totals["flat"] = int(totals["flat"]) + value


func _collect_trait_summary_lines(include_options: bool) -> Array[String]:
	var lines: Array[String] = []
	var race_def := _get_selected_race_def()
	var subrace_def := _get_selected_subrace_def()
	if race_def != null:
		for line in race_def.racial_trait_summary:
			if not String(line).is_empty():
				lines.append(String(line))
	if subrace_def != null:
		for line in subrace_def.racial_trait_summary:
			if not String(line).is_empty():
				lines.append(String(line))
	lines.append_array(_collect_age_trait_summary_lines())
	if include_options and _selected_identity_has_human_versatility():
		lines.append("人类多才：%s +1" % _attribute_display_name(_selected_versatility_pick))
	return lines


func _collect_age_trait_summary_lines() -> Array[String]:
	var lines: Array[String] = []
	var age_profile := _get_selected_age_profile()
	var age_stage_rule := _get_age_stage_rule(age_profile, _selected_age_stage_id)
	if age_stage_rule == null:
		return lines
	for line in age_stage_rule.trait_summary:
		if not String(line).is_empty():
			lines.append(String(line))
	return lines


func _selected_identity_has_human_versatility() -> bool:
	var trait_ids: Array[StringName] = []
	var race_def := _get_selected_race_def()
	var subrace_def := _get_selected_subrace_def()
	if race_def != null:
		trait_ids.append_array(race_def.trait_ids)
	if subrace_def != null:
		trait_ids.append_array(subrace_def.trait_ids)
	var trait_defs: Dictionary = _progression_content_registry.get_race_trait_defs() if _progression_content_registry != null else {}
	for trait_id in trait_ids:
		if trait_id == HUMAN_VERSATILITY_TRAIT_ID:
			return true
		var trait_def := _lookup_registry_entry(trait_defs, trait_id) as RaceTraitDef
		if trait_def != null and trait_def.effect_type == HUMAN_VERSATILITY_TRAIT_ID:
			return true
	return false


func _resolve_body_size_category_for_selection(race_def: RaceDef, subrace_def: SubraceDef) -> StringName:
	if subrace_def != null \
		and subrace_def.body_size_category_override != &"" \
		and BodySizeRules.is_valid_body_size_category(subrace_def.body_size_category_override):
		return subrace_def.body_size_category_override
	if race_def != null and BodySizeRules.is_valid_body_size_category(race_def.body_size_category):
		return race_def.body_size_category
	return &""


func _get_selected_race_def() -> RaceDef:
	return _get_race_def(_selected_race_id)


func _get_selected_subrace_def() -> SubraceDef:
	return _get_subrace_def(_selected_subrace_id)


func _get_selected_age_profile() -> AgeProfileDef:
	var race_def := _get_selected_race_def()
	if race_def == null or _progression_content_registry == null:
		return null
	return _lookup_registry_entry(_progression_content_registry.get_age_profile_defs(), race_def.age_profile_id) as AgeProfileDef


func _get_race_def(race_id: StringName) -> RaceDef:
	if race_id == &"" or _progression_content_registry == null:
		return null
	return _lookup_registry_entry(_progression_content_registry.get_race_defs(), race_id) as RaceDef


func _get_subrace_def(subrace_id: StringName) -> SubraceDef:
	if subrace_id == &"" or _progression_content_registry == null:
		return null
	return _lookup_registry_entry(_progression_content_registry.get_subrace_defs(), subrace_id) as SubraceDef


func _lookup_registry_entry(registry: Dictionary, id: StringName):
	if registry.has(id):
		return registry.get(id)
	var text_id := String(id)
	if registry.has(text_id):
		return registry.get(text_id)
	return null


func _get_age_stage_rule(age_profile: AgeProfileDef, stage_id: StringName) -> AgeStageRule:
	if age_profile == null or stage_id == &"":
		return null
	for rule in age_profile.stage_rules:
		if rule != null and rule.stage_id == stage_id:
			return rule
	return null


func _resolve_default_age_for_stage(age_profile: AgeProfileDef, stage_id: StringName) -> int:
	if age_profile == null or stage_id == &"":
		return 0
	if age_profile.default_age_by_stage.has(stage_id):
		return int(age_profile.default_age_by_stage[stage_id])
	var string_stage_id := String(stage_id)
	if age_profile.default_age_by_stage.has(string_stage_id):
		return int(age_profile.default_age_by_stage[string_stage_id])
	match stage_id:
		&"child":
			return age_profile.child_age
		&"teen":
			return age_profile.teen_age
		&"young_adult":
			return age_profile.young_adult_age
		&"adult":
			return age_profile.adult_age
		&"middle_age":
			return age_profile.middle_age
		&"old":
			return age_profile.old_age
		&"venerable":
			return age_profile.venerable_age
	return age_profile.adult_age


func _identity_label(definition: Variant, fallback_id: StringName) -> String:
	if definition != null:
		var display_name: Variant = definition.get("display_name")
		if display_name is String and not String(display_name).is_empty():
			return String(display_name)
	return String(fallback_id)


func _attribute_display_name(attribute_id: StringName) -> String:
	return String(ATTRIBUTE_DISPLAY_NAMES.get(attribute_id, String(attribute_id)))


func _join_strings(values: Array[String], separator: String) -> String:
	var packed := PackedStringArray()
	for value in values:
		packed.append(value)
	return separator.join(packed)


func _on_confirm_pressed() -> void:
	if _rerolling:
		return
	if _rolled_attributes.is_empty():
		return
	var identity_payload := _build_selected_identity_payload()
	if identity_payload.is_empty():
		push_warning("CharacterCreationWindow: identity payload is invalid; confirmation blocked.")
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
	for key in identity_payload.keys():
		payload[key] = identity_payload[key]
	hide_window()
	character_confirmed.emit(payload)


func _cancel() -> void:
	if _rerolling:
		_stop_requested = true
		return
	hide_window()
	cancelled.emit()
