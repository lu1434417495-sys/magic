class_name SettlementWindow
extends Control

const PARTY_MEMBER_OPTION_UTILS = preload("res://scripts/ui/party_member_option_utils.gd")

signal action_requested(settlement_id: String, action_id: String, payload: Dictionary)
signal closed

@onready var shade: ColorRect = $Shade
@onready var title_label: Label = $CenterContainer/Panel/MarginContainer/Content/Header/HeaderText/TitleLabel
@onready var meta_label: Label = $CenterContainer/Panel/MarginContainer/Content/Header/HeaderText/MetaLabel
@onready var facilities_label: RichTextLabel = $CenterContainer/Panel/MarginContainer/Content/Body/LeftColumn/FacilitiesLabel
@onready var resident_label: RichTextLabel = $CenterContainer/Panel/MarginContainer/Content/Body/LeftColumn/ResidentLabel
@onready var member_selector: OptionButton = $CenterContainer/Panel/MarginContainer/Content/Body/LeftColumn/MemberSelector
@onready var member_state_label: Label = $CenterContainer/Panel/MarginContainer/Content/Body/LeftColumn/MemberStateLabel
@onready var services_container: VBoxContainer = $CenterContainer/Panel/MarginContainer/Content/Body/RightColumn/ServicesScroll/ServicesContainer
@onready var service_state_label: Label = $CenterContainer/Panel/MarginContainer/Content/Body/RightColumn/ServiceStateLabel
@onready var service_cost_label: Label = $CenterContainer/Panel/MarginContainer/Content/Body/RightColumn/ServiceCostLabel
@onready var service_details_label: RichTextLabel = $CenterContainer/Panel/MarginContainer/Content/Body/RightColumn/ServiceDetailsLabel
@onready var feedback_label: Label = $CenterContainer/Panel/MarginContainer/Content/Body/RightColumn/FeedbackLabel
@onready var close_button: Button = $CenterContainer/Panel/MarginContainer/Content/Header/CloseButton

var _window_data: Dictionary = {}
var _settlement_id := ""
var _member_options: Array[Dictionary] = []
var _member_option_map: Dictionary = {}
var _selected_member_id: StringName = &""
var _services: Array[Dictionary] = []
var _selected_service_index := -1


func _ready() -> void:
	hide_window()
	shade.gui_input.connect(_on_shade_gui_input)
	close_button.pressed.connect(_close_from_button)
	member_selector.item_selected.connect(_on_member_selected)


func show_settlement(window_data: Dictionary) -> void:
	_window_data = window_data.duplicate(true)
	_settlement_id = String(_window_data.get("settlement_id", ""))
	_member_options = _build_member_options()
	_member_option_map = PARTY_MEMBER_OPTION_UTILS.build_member_option_map(_member_options)
	_services = _build_service_entries()
	_selected_member_id = PARTY_MEMBER_OPTION_UTILS.resolve_default_member_id(_window_data, _member_option_map, _member_options)
	_selected_service_index = -1
	visible = true
	_refresh_view()


func hide_window() -> void:
	visible = false
	_window_data.clear()
	_settlement_id = ""
	_member_options.clear()
	_member_option_map.clear()
	_selected_member_id = &""
	_services.clear()
	_selected_service_index = -1
	if facilities_label != null:
		facilities_label.text = ""
	if resident_label != null:
		resident_label.text = ""
	if member_selector != null:
		member_selector.clear()
	if member_state_label != null:
		member_state_label.text = ""
	if services_container != null:
		_clear_service_buttons()
	if service_state_label != null:
		service_state_label.text = ""
	if service_cost_label != null:
		service_cost_label.text = ""
	if service_details_label != null:
		service_details_label.text = ""
	if feedback_label != null:
		feedback_label.text = ""


func set_feedback(message: String) -> void:
	if feedback_label != null:
		feedback_label.text = message


func _refresh_view() -> void:
	title_label.text = String(_window_data.get("display_name", "据点"))
	meta_label.text = _build_meta_text()
	facilities_label.text = _build_facility_text()
	resident_label.text = _build_resident_text()
	_rebuild_member_selector()
	_rebuild_service_buttons()
	_refresh_member_state()
	_refresh_service_details()
	if feedback_label != null and feedback_label.text.is_empty():
		feedback_label.text = String(_window_data.get("feedback_text", "点击服务继续，或切换成员后再操作。"))


func _build_meta_text() -> String:
	var tier_name := String(_window_data.get("tier_name", "未知"))
	var footprint_size: Vector2i = _window_data.get("footprint_size", Vector2i.ONE)
	var faction_id := String(_window_data.get("faction_id", "neutral"))
	var state_summary_text := String(_window_data.get("state_summary_text", ""))
	var lines := PackedStringArray([
		"%s  |  占地 %dx%d  |  阵营 %s" % [tier_name, footprint_size.x, footprint_size.y, faction_id]
	])
	if not state_summary_text.is_empty():
		lines.append(state_summary_text)
	return "\n".join(lines)


func _build_facility_text() -> String:
	var facilities := PARTY_MEMBER_OPTION_UTILS.get_dictionary_array(_window_data.get("facilities", []))
	if facilities.is_empty():
		return "设施：暂无"

	var lines: PackedStringArray = ["设施："]
	for facility_variant in facilities:
		var facility: Dictionary = facility_variant
		var display_name := String(facility.get("display_name", "设施"))
		var slot_tag := String(facility.get("slot_tag", "未标记"))
		var interaction_type := String(facility.get("interaction_type", ""))
		var line := "- %s [%s]" % [display_name, slot_tag]
		if not interaction_type.is_empty():
			line += " · %s" % interaction_type
		lines.append(line)
	return "\n".join(lines)


func _build_resident_text() -> String:
	var residents := PARTY_MEMBER_OPTION_UTILS.get_dictionary_array(_window_data.get("service_npcs", []))
	if residents.is_empty():
		return "驻留 NPC：暂无"

	var lines: PackedStringArray = ["驻留 NPC："]
	for resident_variant in residents:
		var resident: Dictionary = resident_variant
		var display_name := String(resident.get("display_name", "NPC"))
		var service_type := String(resident.get("service_type", "服务"))
		var facility_name := String(resident.get("facility_name", ""))
		var line := "- %s · %s" % [display_name, service_type]
		if not facility_name.is_empty():
			line += " · %s" % facility_name
		lines.append(line)
	return "\n".join(lines)


func _build_member_options() -> Array[Dictionary]:
	return PARTY_MEMBER_OPTION_UTILS.build_member_options(_window_data)


func _build_service_entries() -> Array[Dictionary]:
	var services_variant: Variant = _window_data.get("available_services", [])
	var services: Array[Dictionary] = []
	if services_variant is not Array:
		return services

	var facilities_by_id := _build_facility_lookup()
	for service_variant in services_variant:
		if service_variant is not Dictionary:
			continue
		var service := (service_variant as Dictionary).duplicate(true)
		var facility_id := String(service.get("facility_id", ""))
		var facility: Dictionary = facilities_by_id.get(facility_id, {})
		if not facility.is_empty():
			if String(service.get("facility_name", "")).is_empty():
				service["facility_name"] = String(facility.get("display_name", "设施"))
			if String(service.get("interaction_type", "")).is_empty():
				service["interaction_type"] = String(facility.get("interaction_type", ""))
		if not service.has("is_enabled"):
			service["is_enabled"] = true
		if not service.has("disabled_reason"):
			service["disabled_reason"] = ""
		if not service.has("cost_label"):
			service["cost_label"] = _build_default_cost_label(service)
		if not service.has("state_label"):
			service["state_label"] = _build_default_state_label(service)
		if not service.has("summary_text"):
			service["summary_text"] = _build_service_summary_text(service)
		services.append(service)
	return services


func _build_facility_lookup() -> Dictionary:
	var facility_lookup: Dictionary = {}
	var facilities := PARTY_MEMBER_OPTION_UTILS.get_dictionary_array(_window_data.get("facilities", []))
	for facility_variant in facilities:
		var facility: Dictionary = facility_variant
		var facility_id := String(facility.get("facility_id", ""))
		if not facility_id.is_empty():
			facility_lookup[facility_id] = facility.duplicate(true)
	return facility_lookup


func _rebuild_member_selector() -> void:
	member_selector.clear()
	for index in range(_member_options.size()):
		var member_option := _member_options[index]
		var member_id := ProgressionDataUtils.to_string_name(member_option.get("member_id", ""))
		var label := PARTY_MEMBER_OPTION_UTILS.build_member_option_label(member_option)
		member_selector.add_item(label)
		member_selector.set_item_metadata(index, member_id)

	member_selector.visible = not _member_options.is_empty()
	member_state_label.visible = true

	var selected_member_id := PARTY_MEMBER_OPTION_UTILS.resolve_default_member_id(_window_data, _member_option_map, _member_options)
	if selected_member_id == &"" and not _member_options.is_empty():
		selected_member_id = ProgressionDataUtils.to_string_name(_member_options[0].get("member_id", ""))
	_select_member(selected_member_id)


func _select_member(member_id: StringName) -> void:
	if member_id != &"" and _member_option_map.has(member_id):
		_selected_member_id = member_id
	else:
		_selected_member_id = &""

	for index in range(member_selector.item_count):
		if ProgressionDataUtils.to_string_name(member_selector.get_item_metadata(index)) == _selected_member_id:
			member_selector.select(index)
			break
	_refresh_member_state()


func _refresh_member_state() -> void:
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


func _rebuild_service_buttons() -> void:
	_clear_service_buttons()
	for index in range(_services.size()):
		var service := _services[index]
		var button := Button.new()
		button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		button.custom_minimum_size = Vector2(0, 58)
		button.text = _build_service_button_text(service)
		button.disabled = not bool(service.get("is_enabled", true))
		if button.disabled and String(service.get("disabled_reason", "")).is_empty():
			button.tooltip_text = "当前服务不可用。"
		elif button.disabled:
			button.tooltip_text = String(service.get("disabled_reason", ""))
		button.pressed.connect(_on_service_button_pressed.bind(index))
		services_container.add_child(button)

	if services_container.get_child_count() == 0:
		var placeholder := Label.new()
		placeholder.text = "当前据点没有可用服务。"
		placeholder.modulate = Color(0.77, 0.83, 0.91, 0.85)
		services_container.add_child(placeholder)


func _build_service_button_text(service: Dictionary) -> String:
	var facility_name := String(service.get("facility_name", "设施"))
	var npc_name := String(service.get("npc_name", "NPC"))
	var service_type := String(service.get("service_type", "服务"))
	var state_label := String(service.get("state_label", "状态：可用"))
	var cost_label := String(service.get("cost_label", "费用：待定"))
	var text := "%s · %s · %s\n%s  |  %s" % [facility_name, npc_name, service_type, state_label, cost_label]
	if not bool(service.get("is_enabled", true)):
		var disabled_reason := String(service.get("disabled_reason", ""))
		if not disabled_reason.is_empty():
			text += "\n%s" % disabled_reason
	return text


func _build_service_summary_text(service: Dictionary) -> String:
	var facility_name := String(service.get("facility_name", "设施"))
	var npc_name := String(service.get("npc_name", "NPC"))
	var service_type := String(service.get("service_type", "服务"))
	return "%s · %s · %s" % [facility_name, npc_name, service_type]


func _build_default_cost_label(service: Dictionary) -> String:
	var cost_label := String(service.get("cost_label", ""))
	if not cost_label.is_empty():
		return cost_label
	return "费用：待定"


func _build_default_state_label(service: Dictionary) -> String:
	var explicit_state_label := String(service.get("state_label", service.get("state_text", "")))
	if not explicit_state_label.is_empty():
		return explicit_state_label
	if not bool(service.get("is_enabled", true)):
		var disabled_reason := String(service.get("disabled_reason", ""))
		if not disabled_reason.is_empty():
			return "状态：%s" % disabled_reason
		return "状态：不可用"
	return "状态：可用"


func _refresh_service_details() -> void:
	if _services.is_empty():
		service_state_label.text = "状态：暂无服务"
		service_cost_label.text = "费用：暂无服务"
		service_details_label.text = "当前据点没有可用服务。"
		return

	if _selected_service_index < 0 or _selected_service_index >= _services.size():
		_selected_service_index = 0
	var service := _services[_selected_service_index]
	var state_label := String(service.get("state_label", _build_default_state_label(service)))
	var cost_label := String(service.get("cost_label", _build_default_cost_label(service)))
	service_state_label.text = state_label
	service_cost_label.text = cost_label
	service_details_label.text = _build_service_detail_text(service)


func _build_service_detail_text(service: Dictionary) -> String:
	var lines := PackedStringArray([
		"设施：%s" % String(service.get("facility_name", "设施")),
		"NPC：%s" % String(service.get("npc_name", "NPC")),
		"服务：%s" % String(service.get("service_type", "服务")),
		"交互：%s" % String(service.get("interaction_type", "unknown")),
		"状态：%s" % String(service.get("state_label", _build_default_state_label(service))),
		"费用：%s" % String(service.get("cost_label", _build_default_cost_label(service))),
	])
	var disabled_reason := String(service.get("disabled_reason", ""))
	if not disabled_reason.is_empty():
		lines.append("说明：%s" % disabled_reason)
	return "\n".join(lines)


func _on_service_button_pressed(index: int) -> void:
	if index < 0 or index >= _services.size():
		return
	var service: Dictionary = _services[index]
	if not bool(service.get("is_enabled", true)):
		set_feedback(String(service.get("disabled_reason", "当前服务不可用。")))
		return
	_selected_service_index = index
	_refresh_service_details()
	var payload := _build_service_payload(service)
	action_requested.emit(_settlement_id, String(payload.get("action_id", "")), payload)


func _build_service_payload(service: Dictionary) -> Dictionary:
	var payload := service.duplicate(true)
	payload["settlement_id"] = _settlement_id
	payload["member_id"] = String(_selected_member_id)
	payload["default_member_id"] = String(_selected_member_id)
	payload["state_summary_text"] = String(_window_data.get("state_summary_text", ""))
	payload["summary_text"] = _build_service_summary_text(service)
	payload["details_text"] = _build_service_detail_text(service)
	payload["submission_source"] = "settlement"
	var panel_kind := _resolve_service_panel_kind(service)
	if not panel_kind.is_empty():
		payload["panel_kind"] = panel_kind
	return payload


func _resolve_service_panel_kind(service: Dictionary) -> String:
	var explicit_panel_kind := String(service.get("panel_kind", service.get("window_kind", service.get("service_window_kind", ""))))
	if not explicit_panel_kind.is_empty():
		match explicit_panel_kind.to_lower():
			"buy", "sell", "trade", "shop":
				return "shop"
			"travel", "stagecoach", "route":
				return "stagecoach"
			_:
				return explicit_panel_kind
	var interaction_type := String(service.get("interaction_type", ""))
	var service_type := String(service.get("service_type", ""))
	if interaction_type == "shop" or service_type.find("交易") != -1 or service_type.find("商") != -1:
		return "shop"
	if interaction_type == "travel" or service_type.find("传送") != -1 or service_type.find("行路") != -1:
		return "stagecoach"
	return ""


func _clear_service_buttons() -> void:
	for child in services_container.get_children():
		child.queue_free()


func _on_member_selected(index: int) -> void:
	if index < 0 or index >= member_selector.item_count:
		_selected_member_id = &""
	else:
		_selected_member_id = ProgressionDataUtils.to_string_name(member_selector.get_item_metadata(index))
	_refresh_member_state()


func _close_from_button() -> void:
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
	if mouse_event.button_index != MOUSE_BUTTON_LEFT:
		return
	_close_from_button()
