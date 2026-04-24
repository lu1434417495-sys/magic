## 文件说明：该脚本属于建卡窗口相关的界面窗口脚本，集中维护姓名阶段、属性阶段、5D3-1 属性掷骰与 reroll 循环的顶层字段。
## 审查重点：重点核对 reroll 循环的让帧、停止信号、预期下限判定以及与 CharacterCreationService 的 reroll 次数口径是否一致。
## 备注：后续如果调整掷骰骰式、预期下限上限或属性集合，需要同步检查 CharacterCreationService 与 GameSession 建卡 override 流程。

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

@onready var shade: ColorRect = $Shade
@onready var name_phase: Control = %NamePhase
@onready var attribute_phase: Control = %AttributePhase
@onready var name_input: LineEdit = %NameInput
@onready var name_confirm_button: Button = %NameConfirmButton
@onready var name_cancel_button: Button = %NameCancelButton
@onready var reroll_count_label: Label = %RerollCountLabel
@onready var name_preview_label: Label = %NamePreviewLabel
@onready var reroll_button: Button = %RerollButton
@onready var stop_button: Button = %StopButton
@onready var confirm_button: Button = %ConfirmButton
@onready var attribute_cancel_button: Button = %AttributeCancelButton

var _rng := RandomNumberGenerator.new()
var _rerolling := false
var _stop_requested := false
var _reroll_count := 0
var _rolled_attributes: Dictionary = {}
var _player_name: String = ""
var _attribute_value_labels: Dictionary = {}
var _attribute_threshold_spinboxes: Dictionary = {}


func _ready() -> void:
	_rng.randomize()
	_cache_attribute_rows()

	name_confirm_button.pressed.connect(_on_name_confirmed)
	name_cancel_button.pressed.connect(_cancel)
	name_input.text_submitted.connect(_on_name_text_submitted)
	reroll_button.pressed.connect(_on_reroll_pressed)
	stop_button.pressed.connect(_on_stop_pressed)
	confirm_button.pressed.connect(_on_confirm_pressed)
	attribute_cancel_button.pressed.connect(_cancel)

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
	_player_name = ""
	name_input.text = ""
	for attribute_id in ATTRIBUTE_ORDER:
		var spinbox: SpinBox = _attribute_threshold_spinboxes.get(attribute_id)
		if spinbox != null:
			spinbox.value = DICE_MIN_TOTAL
		var label: Label = _attribute_value_labels.get(attribute_id)
		if label != null:
			label.text = "—"
	name_phase.visible = true
	attribute_phase.visible = false
	_refresh_reroll_count_label()
	name_input.grab_focus()


func hide_window() -> void:
	visible = false
	_rerolling = false
	_stop_requested = false
	_reroll_count = 0
	_rolled_attributes.clear()
	_player_name = ""


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
	for attribute_id in ATTRIBUTE_ORDER:
		var spinbox: SpinBox = _attribute_threshold_spinboxes.get(attribute_id)
		if spinbox == null:
			continue
		spinbox.min_value = DICE_MIN_TOTAL
		spinbox.max_value = DICE_MAX_TOTAL
		spinbox.step = 1
		spinbox.rounded = true
		spinbox.allow_greater = false
		spinbox.allow_lesser = false
		spinbox.value = DICE_MIN_TOTAL
		spinbox.value_changed.connect(_on_threshold_value_changed.bind(spinbox))


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
	_roll_once()
	_refresh_reroll_count_label()
	_update_button_states()
	reroll_button.grab_focus()


func _roll_once() -> void:
	for attribute_id in ATTRIBUTE_ORDER:
		_rolled_attributes[attribute_id] = _roll_attribute_value()
	_refresh_attribute_value_labels()


func _roll_attribute_value() -> int:
	var total := DICE_OFFSET
	for _i in DICE_COUNT:
		total += _rng.randi_range(1, DICE_SIDES)
	return maxi(DICE_VALUE_FLOOR, total)


func _refresh_attribute_value_labels() -> void:
	for attribute_id in ATTRIBUTE_ORDER:
		var label: Label = _attribute_value_labels.get(attribute_id)
		if label == null:
			continue
		label.text = str(int(_rolled_attributes.get(attribute_id, 0)))


func _refresh_reroll_count_label() -> void:
	reroll_count_label.text = "Reroll 次数：%d" % _reroll_count


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
	_roll_once()
	_refresh_reroll_count_label()


func _on_stop_pressed() -> void:
	if not _rerolling:
		return
	_stop_requested = true


func _run_continuous_reroll() -> void:
	_rerolling = true
	_stop_requested = false
	_update_button_states()
	while true:
		_reroll_count += 1
		_roll_once()
		_refresh_reroll_count_label()
		if _meets_thresholds():
			break
		if _stop_requested:
			break
		await get_tree().process_frame
	_rerolling = false
	_stop_requested = false
	_update_button_states()


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
