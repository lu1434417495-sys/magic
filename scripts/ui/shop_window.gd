class_name ShopWindow
extends Control

const PARTY_MEMBER_OPTION_UTILS = preload("res://scripts/ui/party_member_option_utils.gd")

const SUPPORTED_PANEL_KINDS := {
	"shop": true,
	"contract_board": true,
	"forge": true,
	"stagecoach": true,
}

signal action_requested(settlement_id: String, action_id: String, payload: Dictionary)
signal closed

@onready var shade: ColorRect = $Shade
@onready var title_label: Label = $CenterContainer/Panel/MarginContainer/Content/Header/HeaderText/TitleLabel
@onready var meta_label: Label = $CenterContainer/Panel/MarginContainer/Content/Header/HeaderText/MetaLabel
@onready var entry_title_label: Label = $CenterContainer/Panel/MarginContainer/Content/Body/EntryColumn/EntryTitle
@onready var summary_label: Label = $CenterContainer/Panel/MarginContainer/Content/Body/DetailsColumn/SummaryLabel
@onready var summary_title_label: Label = $CenterContainer/Panel/MarginContainer/Content/Body/DetailsColumn/SummaryTitle
@onready var entry_list: ItemList = $CenterContainer/Panel/MarginContainer/Content/Body/EntryColumn/EntryList
@onready var details_label: RichTextLabel = $CenterContainer/Panel/MarginContainer/Content/Body/DetailsColumn/DetailsLabel
@onready var state_label: Label = $CenterContainer/Panel/MarginContainer/Content/Body/DetailsColumn/StateLabel
@onready var state_title_label: Label = $CenterContainer/Panel/MarginContainer/Content/Body/DetailsColumn/StateTitle
@onready var cost_label: Label = $CenterContainer/Panel/MarginContainer/Content/Body/DetailsColumn/CostLabel
@onready var cost_title_label: Label = $CenterContainer/Panel/MarginContainer/Content/Body/DetailsColumn/CostTitle
@onready var details_title_label: Label = $CenterContainer/Panel/MarginContainer/Content/Body/DetailsColumn/DetailsTitle
@onready var member_selector: OptionButton = $CenterContainer/Panel/MarginContainer/Content/Body/DetailsColumn/MemberSelector
@onready var member_title_label: Label = $CenterContainer/Panel/MarginContainer/Content/Body/DetailsColumn/MemberTitle
@onready var member_state_label: Label = $CenterContainer/Panel/MarginContainer/Content/Body/DetailsColumn/MemberStateLabel
@onready var confirm_button: Button = $CenterContainer/Panel/MarginContainer/Content/Footer/ConfirmButton
@onready var cancel_button: Button = $CenterContainer/Panel/MarginContainer/Content/Footer/CancelButton
@onready var close_button: Button = $CenterContainer/Panel/MarginContainer/Content/Header/CloseButton

var _window_data: Dictionary = {}
var _settlement_id := ""
var _action_id := ""
var _entries: Array[Dictionary] = []
var _member_options: Array[Dictionary] = []
var _member_option_map: Dictionary = {}
var _entry_payload_valid := true
var _selected_entry_index := -1
var _selected_member_id: StringName = &""


func _ready() -> void:
	hide_window()
	shade.gui_input.connect(_on_shade_gui_input)
	entry_list.item_selected.connect(_on_entry_selected)
	member_selector.item_selected.connect(_on_member_selected)
	confirm_button.pressed.connect(_on_confirm_button_pressed)
	cancel_button.pressed.connect(_on_cancel_button_pressed)
	close_button.pressed.connect(_close_window)


func show_shop(window_data: Dictionary) -> void:
	var normalized_window_data := _normalize_top_level_payload(window_data)
	if normalized_window_data.is_empty():
		hide_window()
		return
	_window_data = normalized_window_data
	_settlement_id = String(_window_data["settlement_id"])
	_action_id = String(_window_data["action_id"])
	_entries = _build_entries()
	if not _entry_payload_valid:
		hide_window()
		return
	_member_options = _build_member_options()
	_member_option_map = PARTY_MEMBER_OPTION_UTILS.build_member_option_map(_member_options)
	_selected_entry_index = -1
	_selected_member_id = _resolve_default_member_id()
	visible = true
	refresh_view()


func show_stagecoach(window_data: Dictionary) -> void:
	var normalized_window_data := window_data.duplicate(true)
	if not normalized_window_data.has("panel_kind") or normalized_window_data["panel_kind"] is not String:
		hide_window()
		return
	var panel_kind := String(normalized_window_data["panel_kind"]).strip_edges()
	if panel_kind != "stagecoach":
		hide_window()
		return
	normalized_window_data["panel_kind"] = panel_kind
	show_shop(normalized_window_data)


func hide_window() -> void:
	visible = false
	_window_data.clear()
	_settlement_id = ""
	_action_id = ""
	_entries.clear()
	_member_options.clear()
	_member_option_map.clear()
	_entry_payload_valid = true
	_selected_entry_index = -1
	_selected_member_id = &""
	if title_label != null:
		title_label.text = ""
	if meta_label != null:
		meta_label.text = ""
	if entry_title_label != null:
		entry_title_label.text = ""
	if summary_title_label != null:
		summary_title_label.text = ""
	if state_title_label != null:
		state_title_label.text = ""
	if cost_title_label != null:
		cost_title_label.text = ""
	if details_title_label != null:
		details_title_label.text = ""
	if member_title_label != null:
		member_title_label.text = ""
	if entry_list != null:
		entry_list.clear()
	if member_selector != null:
		member_selector.clear()
	if summary_label != null:
		summary_label.text = ""
	if details_label != null:
		details_label.text = ""
	if state_label != null:
		state_label.text = ""
	if cost_label != null:
		cost_label.text = ""
	if member_state_label != null:
		member_state_label.text = ""
	if confirm_button != null:
		confirm_button.text = ""
		confirm_button.disabled = true
	if cancel_button != null:
		cancel_button.text = ""


func refresh_view() -> void:
	title_label.text = String(_window_data["title"])
	meta_label.text = _build_meta_text()
	summary_label.text = String(_window_data["summary_text"])
	confirm_button.text = String(_window_data["confirm_label"])
	cancel_button.text = String(_window_data["cancel_label"])
	_apply_section_titles()
	_rebuild_entry_list()
	_build_member_selector()
	_select_entry(_selected_entry_index if _selected_entry_index >= 0 else 0)
	_refresh_member_state()
	_refresh_details()
	_refresh_controls()


func _normalize_top_level_payload(window_data: Dictionary) -> Dictionary:
	var normalized := window_data.duplicate(true)
	for field_name in [
		"settlement_id",
		"action_id",
		"panel_kind",
		"title",
		"meta",
		"summary_text",
		"confirm_label",
		"cancel_label",
		"entry_title",
		"summary_title",
		"state_title",
		"cost_title",
		"details_title",
		"member_title",
		"empty_state_label",
		"empty_cost_label",
		"empty_details_text",
	]:
		if not window_data.has(field_name) or window_data[field_name] is not String:
			return {}
		var field_value := String(window_data[field_name]).strip_edges()
		if field_value.is_empty():
			return {}
		normalized[field_name] = field_value

	var panel_kind := String(normalized["panel_kind"])
	if not SUPPORTED_PANEL_KINDS.has(panel_kind):
		return {}
	if not window_data.has("state_summary_text") or window_data["state_summary_text"] is not String:
		return {}
	normalized["state_summary_text"] = String(window_data["state_summary_text"])
	if not window_data.has("show_member_selector") or window_data["show_member_selector"] is not bool:
		return {}
	normalized["show_member_selector"] = bool(window_data["show_member_selector"])
	return normalized


func _build_meta_text() -> String:
	var state_summary_text := String(_window_data["state_summary_text"])
	var meta_text := String(_window_data["meta"])
	if not state_summary_text.is_empty():
		return "%s\n%s" % [meta_text, state_summary_text]
	return meta_text


func _build_entries() -> Array[Dictionary]:
	_entry_payload_valid = true
	var entries_variant: Variant = _window_data.get("entries", null)
	var entries: Array[Dictionary] = []
	if entries_variant is not Array:
		_entry_payload_valid = false
		return entries
	for entry_variant in entries_variant:
		if entry_variant is not Dictionary:
			_entry_payload_valid = false
			return []
		var entry := _normalize_entry(entry_variant as Dictionary)
		if entry.is_empty():
			_entry_payload_valid = false
			return []
		entries.append(entry)
	return entries


func _normalize_entry(entry: Dictionary) -> Dictionary:
	var normalized := entry.duplicate(true)
	for field_name in [
		"entry_id",
		"display_name",
		"summary_text",
		"details_text",
		"state_label",
		"cost_label",
	]:
		if not entry.has(field_name) or entry[field_name] is not String:
			return {}
		var field_value := String(entry[field_name]).strip_edges()
		if field_value.is_empty():
			return {}
		normalized[field_name] = field_value
	if not entry.has("is_enabled") or entry["is_enabled"] is not bool:
		return {}
	normalized["is_enabled"] = bool(entry["is_enabled"])
	if not entry.has("disabled_reason") or entry["disabled_reason"] is not String:
		return {}
	var disabled_reason := String(entry["disabled_reason"]).strip_edges()
	if not bool(entry["is_enabled"]) and disabled_reason.is_empty():
		return {}
	normalized["disabled_reason"] = disabled_reason
	return normalized


func _build_member_options() -> Array[Dictionary]:
	return PARTY_MEMBER_OPTION_UTILS.build_member_options(_window_data)


func _build_member_selector() -> void:
	if not _should_show_member_selector():
		member_selector.clear()
		member_selector.visible = false
		member_state_label.visible = false
		return
	member_selector.clear()
	for index in range(_member_options.size()):
		var member_option := _member_options[index]
		var member_id := ProgressionDataUtils.to_string_name(member_option.get("member_id", ""))
		var label := PARTY_MEMBER_OPTION_UTILS.build_member_option_label(member_option)
		member_selector.add_item(label)
		member_selector.set_item_metadata(index, member_id)

	member_selector.visible = not _member_options.is_empty()
	member_state_label.visible = true

	var selected_member_id := _resolve_default_member_id()
	if selected_member_id == &"" and not _member_options.is_empty():
		selected_member_id = ProgressionDataUtils.to_string_name(_member_options[0].get("member_id", ""))
	_select_member(selected_member_id)


func _resolve_default_member_id() -> StringName:
	return PARTY_MEMBER_OPTION_UTILS.resolve_default_member_id(
		_window_data,
		_member_option_map,
		_member_options
	)


func _select_member(member_id: StringName) -> void:
	if member_id != &"" and _member_option_map.has(member_id):
		_selected_member_id = member_id
	else:
		_selected_member_id = &""

	for index in range(member_selector.item_count):
		if ProgressionDataUtils.to_string_name(member_selector.get_item_metadata(index)) == _selected_member_id:
			member_selector.select(index)
			break


func _refresh_member_state() -> void:
	if not _should_show_member_selector():
		member_state_label.text = ""
		member_state_label.visible = false
		return
	if _member_options.is_empty():
		member_state_label.text = "成员：暂无可用成员。"
		return
	if _selected_member_id == &"":
		member_state_label.text = "成员：请选择一名成员。"
		return

	var member_option: Dictionary = _member_option_map.get(_selected_member_id, {})
	if member_option.is_empty():
		member_state_label.text = "成员：当前选择不可用。"
		return

	var display_name := PARTY_MEMBER_OPTION_UTILS.get_member_option_display_name(member_option)
	if display_name.is_empty():
		member_state_label.text = "成员：当前选择不可用。"
		return
	var roster_role := String(member_option.get("roster_role", "成员"))
	var state_summary_text := String(_window_data.get("state_summary_text", ""))
	var lines := PackedStringArray([
		"成员：%s" % display_name,
		"编组：%s" % roster_role,
		"HP %d / MP %d" % [int(member_option.get("current_hp", 0)), int(member_option.get("current_mp", 0))],
	])
	if bool(member_option.get("is_leader", false)):
		lines.append("状态：当前队长")
	if not state_summary_text.is_empty():
		lines.append(state_summary_text)
	member_state_label.text = "\n".join(lines)


func _rebuild_entry_list() -> void:
	entry_list.clear()
	for index in range(_entries.size()):
		var entry := _entries[index]
		var label := _build_entry_label(entry)
		entry_list.add_item(label)
		entry_list.set_item_metadata(index, index)


func _build_entry_label(entry: Dictionary) -> String:
	var display_name := String(entry["display_name"])
	var state_label := String(entry["state_label"])
	var cost_label := String(entry["cost_label"])
	var label := "%s\n%s  |  %s" % [display_name, state_label, cost_label]
	if not bool(entry["is_enabled"]):
		var disabled_reason := String(entry["disabled_reason"])
		if not disabled_reason.is_empty():
			label += "\n%s" % disabled_reason
	return label


func _select_entry(index: int) -> void:
	if _entries.is_empty():
		_selected_entry_index = -1
		return
	if index < 0 or index >= _entries.size():
		index = 0
	_selected_entry_index = index
	entry_list.deselect_all()
	entry_list.select(index)


func _refresh_details() -> void:
	if _entries.is_empty():
		state_label.text = String(_window_data["empty_state_label"])
		cost_label.text = String(_window_data["empty_cost_label"])
		details_label.text = String(_window_data["empty_details_text"])
		confirm_button.disabled = true
		return

	var entry := _entries[_selected_entry_index] if _selected_entry_index >= 0 and _selected_entry_index < _entries.size() else _entries[0]
	var entry_state_label := String(entry["state_label"])
	var resolved_cost_label := String(entry["cost_label"])
	state_label.text = entry_state_label
	cost_label.text = resolved_cost_label
	details_label.text = _build_entry_details(entry)


func _build_entry_details(entry: Dictionary) -> String:
	var lines := PackedStringArray([
		"条目：%s" % String(entry["display_name"]),
		"摘要：%s" % String(entry["summary_text"]),
		"说明：%s" % String(entry["details_text"]),
		"状态：%s" % String(entry["state_label"]),
		"费用：%s" % String(entry["cost_label"]),
	])
	var disabled_reason := String(entry["disabled_reason"])
	if not disabled_reason.is_empty():
		lines.append("不可用原因：%s" % disabled_reason)
	if _should_show_member_selector():
		var selected_member_display_name := _get_selected_member_display_name(_selected_member_id) if _selected_member_id != &"" else ""
		lines.append("当前成员：%s" % (selected_member_display_name if not selected_member_display_name.is_empty() else "未选择"))
	return "\n".join(lines)


func _refresh_controls() -> void:
	var has_member := _selected_member_id != &"" and _member_option_map.has(_selected_member_id)
	var has_entry := not _entries.is_empty()
	var entry_enabled := false
	if has_entry:
		var entry := _entries[_selected_entry_index] if _selected_entry_index >= 0 and _selected_entry_index < _entries.size() else _entries[0]
		entry_enabled = bool(entry["is_enabled"])
	confirm_button.disabled = ((_should_show_member_selector() and not has_member) or not entry_enabled)
	member_selector.disabled = _member_options.is_empty()


func _get_selected_member_display_name(member_id: StringName) -> String:
	var member_option: Dictionary = _member_option_map.get(member_id, {})
	if member_option.is_empty():
		return ""
	return PARTY_MEMBER_OPTION_UTILS.get_member_option_display_name(member_option)


func _build_confirm_payload() -> Dictionary:
	var entry := _entries[_selected_entry_index] if _selected_entry_index >= 0 and _selected_entry_index < _entries.size() else _entries[0]
	var payload := entry.duplicate(true)
	var panel_kind := _get_panel_kind()
	payload["settlement_id"] = _settlement_id
	payload["action_id"] = _action_id
	payload["interaction_script_id"] = String(_window_data.get("interaction_script_id", payload.get("interaction_script_id", "")))
	payload["facility_id"] = String(_window_data.get("facility_id", payload.get("facility_id", "")))
	payload["facility_name"] = String(_window_data.get("facility_name", payload.get("facility_name", "")))
	payload["npc_id"] = String(_window_data.get("npc_id", payload.get("npc_id", "")))
	payload["npc_name"] = String(_window_data.get("npc_name", payload.get("npc_name", "")))
	payload["service_type"] = String(_window_data.get("service_type", payload.get("service_type", "")))
	payload["member_id"] = String(_selected_member_id)
	payload["default_member_id"] = String(_selected_member_id)
	payload["submission_source"] = panel_kind
	payload["panel_kind"] = panel_kind
	payload["state_summary_text"] = String(_window_data.get("state_summary_text", ""))
	return payload


func _on_entry_selected(index: int) -> void:
	_select_entry(index)
	_refresh_details()
	_refresh_controls()


func _on_member_selected(index: int) -> void:
	if index < 0 or index >= member_selector.item_count:
		_selected_member_id = &""
	else:
		_selected_member_id = ProgressionDataUtils.to_string_name(member_selector.get_item_metadata(index))
	_refresh_member_state()
	_refresh_details()
	_refresh_controls()


func _on_confirm_button_pressed() -> void:
	if confirm_button.disabled:
		return
	var payload := _build_confirm_payload()
	var settlement_id := _settlement_id
	var action_id := _action_id
	hide_window()
	action_requested.emit(settlement_id, action_id, payload)


func _on_cancel_button_pressed() -> void:
	if not visible:
		return
	hide_window()
	closed.emit()


func _close_window() -> void:
	_on_cancel_button_pressed()


func _on_shade_gui_input(event: InputEvent) -> void:
	if event is not InputEventMouseButton:
		return
	var mouse_event := event as InputEventMouseButton
	if not mouse_event.pressed:
		return
	if mouse_event.button_index != MOUSE_BUTTON_LEFT:
		return
	_on_cancel_button_pressed()


func _get_panel_kind() -> String:
	return String(_window_data["panel_kind"])


func _should_show_member_selector() -> bool:
	return bool(_window_data["show_member_selector"])


func _apply_section_titles() -> void:
	entry_title_label.text = String(_window_data["entry_title"])
	summary_title_label.text = String(_window_data["summary_title"])
	state_title_label.text = String(_window_data["state_title"])
	cost_title_label.text = String(_window_data["cost_title"])
	details_title_label.text = String(_window_data["details_title"])
	member_title_label.text = String(_window_data["member_title"])
