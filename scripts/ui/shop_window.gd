class_name ShopWindow
extends Control

const PARTY_MEMBER_OPTION_UTILS = preload("res://scripts/ui/party_member_option_utils.gd")

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
	_window_data = window_data.duplicate(true)
	_settlement_id = String(_window_data.get("settlement_id", ""))
	_action_id = String(_window_data.get("action_id", ""))
	_entries = _build_entries()
	_member_options = _build_member_options()
	_member_option_map = PARTY_MEMBER_OPTION_UTILS.build_member_option_map(_member_options)
	_selected_entry_index = -1
	_selected_member_id = _resolve_default_member_id() if _should_show_member_selector() else &""
	visible = true
	refresh_view()


func show_stagecoach(window_data: Dictionary) -> void:
	var normalized_window_data := window_data.duplicate(true)
	if String(normalized_window_data.get("panel_kind", "")).is_empty():
		normalized_window_data["panel_kind"] = "stagecoach"
	show_shop(normalized_window_data)


func hide_window() -> void:
	visible = false
	_window_data.clear()
	_settlement_id = ""
	_action_id = ""
	_entries.clear()
	_member_options.clear()
	_member_option_map.clear()
	_selected_entry_index = -1
	_selected_member_id = &""
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


func refresh_view() -> void:
	var panel_profile := _get_panel_profile()
	title_label.text = String(_window_data.get("title", panel_profile.get("window_title", "交易窗口")))
	meta_label.text = _build_meta_text(panel_profile)
	summary_label.text = String(_window_data.get("summary_text", panel_profile.get("summary_text", "选择一项交易后确认执行。")))
	confirm_button.text = String(_window_data.get("confirm_label", panel_profile.get("confirm_label", "确认")))
	cancel_button.text = String(_window_data.get("cancel_label", panel_profile.get("cancel_label", "返回")))
	_apply_section_titles(panel_profile)
	_rebuild_entry_list()
	_build_member_selector()
	_select_entry(_selected_entry_index if _selected_entry_index >= 0 else 0)
	_refresh_member_state()
	_refresh_details()
	_refresh_controls()


func _build_meta_text(panel_profile: Dictionary = {}) -> String:
	var state_summary_text := String(_window_data.get("state_summary_text", ""))
	var meta_text := String(_window_data.get("meta", ""))
	if not state_summary_text.is_empty():
		if meta_text.is_empty():
			return state_summary_text
		return "%s\n%s" % [meta_text, state_summary_text]
	if not meta_text.is_empty():
		return meta_text
	return String(panel_profile.get("meta_fallback_text", "交易窗口将使用当前选定成员。")) if _should_show_member_selector() else String(panel_profile.get("summary_text", "选择一项后确认执行。"))


func _build_entries() -> Array[Dictionary]:
	var entries_variant: Variant = _window_data.get("entries", [])
	var entries: Array[Dictionary] = []
	if entries_variant is Array:
		for entry_variant in entries_variant:
			if entry_variant is Dictionary:
				entries.append((entry_variant as Dictionary).duplicate(true))
	if entries.is_empty() and not bool(_window_data.get("allow_empty_entries", false)):
		entries.append(_build_fallback_entry())
	return entries


func _build_fallback_entry() -> Dictionary:
	var panel_profile := _get_panel_profile()
	return {
		"entry_id": "default",
		"display_name": String(_window_data.get("service_name", _window_data.get("title", panel_profile.get("fallback_entry_name", "交易")))),
		"summary_text": String(_window_data.get("summary_text", panel_profile.get("fallback_summary_text", "默认交易条目。"))),
		"details_text": String(_window_data.get("details_text", _window_data.get("summary_text", ""))),
		"state_label": String(_window_data.get("state_label", _window_data.get("state_text", panel_profile.get("fallback_state_label", "状态：可用")))),
		"cost_label": String(_window_data.get("cost_label", "费用：待定")),
		"is_enabled": bool(_window_data.get("is_enabled", true)),
		"disabled_reason": String(_window_data.get("disabled_reason", "")),
	}


func _build_member_options() -> Array[Dictionary]:
	if not _should_show_member_selector():
		return []
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

	var display_name := String(member_option.get("display_name", String(_selected_member_id)))
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
	var display_name := String(entry.get("display_name", entry.get("entry_id", "条目")))
	var state_label := String(entry.get("state_label", entry.get("state_text", "状态：可用")))
	var cost_label := String(entry.get("cost_label", "费用：待定"))
	var label := "%s\n%s  |  %s" % [display_name, state_label, cost_label]
	if not bool(entry.get("is_enabled", true)):
		var disabled_reason := String(entry.get("disabled_reason", ""))
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
		var panel_profile := _get_panel_profile()
		state_label.text = String(panel_profile.get("empty_state_label", "状态：暂无条目"))
		cost_label.text = String(panel_profile.get("empty_cost_label", "费用：暂无条目"))
		details_label.text = String(panel_profile.get("empty_details_text", "当前没有可用条目。"))
		confirm_button.disabled = true
		return

	var entry := _entries[_selected_entry_index] if _selected_entry_index >= 0 and _selected_entry_index < _entries.size() else _entries[0]
	var state_text := String(entry.get("state_label", entry.get("state_text", "状态：可用")))
	var resolved_cost_label := String(entry.get("cost_label", "费用：待定"))
	state_label.text = state_text
	cost_label.text = resolved_cost_label
	details_label.text = _build_entry_details(entry)


func _build_entry_details(entry: Dictionary) -> String:
	var lines := PackedStringArray([
		"条目：%s" % String(entry.get("display_name", entry.get("entry_id", "条目"))),
		"摘要：%s" % String(entry.get("summary_text", "暂无摘要。")),
		"说明：%s" % String(entry.get("details_text", "暂无说明。")),
		"状态：%s" % String(entry.get("state_label", entry.get("state_text", "状态：可用"))),
		"费用：%s" % String(entry.get("cost_label", "费用：待定")),
	])
	var disabled_reason := String(entry.get("disabled_reason", ""))
	if not disabled_reason.is_empty():
		lines.append("不可用原因：%s" % disabled_reason)
	if _should_show_member_selector():
		lines.append("当前成员：%s" % (_get_selected_member_display_name(_selected_member_id) if _selected_member_id != &"" else "未选择"))
	return "\n".join(lines)


func _refresh_controls() -> void:
	var has_member := _selected_member_id != &"" and _member_option_map.has(_selected_member_id)
	var has_entry := not _entries.is_empty()
	var entry_enabled := has_entry and bool((_entries[_selected_entry_index] if _selected_entry_index >= 0 and _selected_entry_index < _entries.size() else _entries[0]).get("is_enabled", true))
	confirm_button.disabled = ((_should_show_member_selector() and not has_member) or not entry_enabled)
	member_selector.disabled = _member_options.is_empty()


func _get_selected_member_display_name(member_id: StringName) -> String:
	var member_option: Dictionary = _member_option_map.get(member_id, {})
	if member_option.is_empty():
		return String(member_id)
	return String(member_option.get("display_name", member_id))


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
	var panel_kind := String(_window_data.get("panel_kind", "shop"))
	return panel_kind if not panel_kind.is_empty() else "shop"


func _should_show_member_selector() -> bool:
	return bool(_window_data.get("show_member_selector", true))


func _apply_section_titles(panel_profile: Dictionary) -> void:
	entry_title_label.text = String(_window_data.get("entry_title", panel_profile.get("entry_title", "可选条目")))
	summary_title_label.text = String(_window_data.get("summary_title", panel_profile.get("summary_title", "交易概况")))
	state_title_label.text = String(_window_data.get("state_title", panel_profile.get("state_title", "交易状态")))
	cost_title_label.text = String(_window_data.get("cost_title", panel_profile.get("cost_title", "交易费用")))
	details_title_label.text = String(_window_data.get("details_title", panel_profile.get("details_title", "交易说明")))
	member_title_label.text = String(_window_data.get("member_title", panel_profile.get("member_title", "交易成员")))


func _get_panel_profile() -> Dictionary:
	match _get_panel_kind():
		"stagecoach":
			return {
				"window_title": "驿站窗口",
				"summary_text": "选择一项行程后确认出发。",
				"confirm_label": "确认出发",
				"cancel_label": "返回",
				"meta_fallback_text": "驿站窗口将使用当前选定成员。",
				"entry_title": "可选路线",
				"summary_title": "行程概况",
				"state_title": "行程状态",
				"cost_title": "行程费用",
				"details_title": "行程说明",
				"member_title": "出发成员",
				"fallback_entry_name": "行程",
				"fallback_summary_text": "默认行程条目。",
				"fallback_state_label": "状态：可出发",
				"empty_state_label": "状态：暂无路线",
				"empty_cost_label": "费用：暂无路线",
				"empty_details_text": "当前没有可用路线。",
			}
		_:
			return {
				"window_title": "交易窗口",
				"summary_text": "选择一项交易后确认执行。",
				"confirm_label": "确认",
				"cancel_label": "返回",
				"meta_fallback_text": "交易窗口将使用当前选定成员。",
				"entry_title": "可选条目",
				"summary_title": "交易概况",
				"state_title": "交易状态",
				"cost_title": "交易费用",
				"details_title": "交易说明",
				"member_title": "交易成员",
				"fallback_entry_name": "交易",
				"fallback_summary_text": "默认交易条目。",
				"fallback_state_label": "状态：可用",
				"empty_state_label": "状态：暂无条目",
				"empty_cost_label": "费用：暂无条目",
				"empty_details_text": "当前没有可用条目。",
			}
