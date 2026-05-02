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
var _facilities: Array[Dictionary] = []
var _residents: Array[Dictionary] = []
var _selected_member_id: StringName = &""
var _services: Array[Dictionary] = []
var _selected_service_index := -1
var _facility_payload_valid := true
var _resident_payload_valid := true
var _service_payload_valid := true


func _ready() -> void:
	hide_window()
	shade.gui_input.connect(_on_shade_gui_input)
	close_button.pressed.connect(_close_from_button)
	member_selector.item_selected.connect(_on_member_selected)


func show_settlement(window_data: Dictionary) -> void:
	var normalized_window_data := _normalize_top_level_payload(window_data)
	if normalized_window_data.is_empty():
		hide_window()
		return
	_window_data = normalized_window_data
	_settlement_id = String(_window_data["settlement_id"])
	_member_options = _build_member_options()
	_member_option_map = PARTY_MEMBER_OPTION_UTILS.build_member_option_map(_member_options)
	_facilities = _build_facility_entries()
	_residents = _build_resident_entries()
	_services = _build_service_entries()
	if not _facility_payload_valid or not _resident_payload_valid or not _service_payload_valid:
		hide_window()
		return
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
	_facilities.clear()
	_residents.clear()
	_selected_member_id = &""
	_services.clear()
	_selected_service_index = -1
	_facility_payload_valid = true
	_resident_payload_valid = true
	_service_payload_valid = true
	if title_label != null:
		title_label.text = ""
	if meta_label != null:
		meta_label.text = ""
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
	title_label.text = String(_window_data["display_name"])
	meta_label.text = _build_meta_text()
	facilities_label.text = _build_facility_text()
	resident_label.text = _build_resident_text()
	_rebuild_member_selector()
	_rebuild_service_buttons()
	_refresh_member_state()
	_refresh_service_details()
	if feedback_label != null and feedback_label.text.is_empty():
		feedback_label.text = String(_window_data["feedback_text"])


func _build_meta_text() -> String:
	var tier_name := String(_window_data["tier_name"])
	var footprint_size: Vector2i = _window_data["footprint_size"]
	var faction_id := String(_window_data["faction_id"])
	var state_summary_text := String(_window_data["state_summary_text"])
	var lines := PackedStringArray([
		"%s  |  占地 %dx%d  |  阵营 %s" % [tier_name, footprint_size.x, footprint_size.y, faction_id]
	])
	if not state_summary_text.is_empty():
		lines.append(state_summary_text)
	return "\n".join(lines)


func _build_facility_text() -> String:
	if _facilities.is_empty():
		return "设施：暂无"

	var lines: PackedStringArray = ["设施："]
	for facility in _facilities:
		var display_name := String(facility["display_name"])
		var slot_tag := String(facility["slot_tag"])
		var interaction_type := String(facility["interaction_type"])
		var line := "- %s [%s]" % [display_name, slot_tag]
		if not interaction_type.is_empty():
			line += " · %s" % interaction_type
		lines.append(line)
	return "\n".join(lines)


func _build_resident_text() -> String:
	if _residents.is_empty():
		return "驻留 NPC：暂无"

	var lines: PackedStringArray = ["驻留 NPC："]
	for resident in _residents:
		var line := "- %s · %s · %s" % [
			String(resident["display_name"]),
			String(resident["service_type"]),
			String(resident["facility_name"]),
		]
		lines.append(line)
	return "\n".join(lines)


func _build_member_options() -> Array[Dictionary]:
	return PARTY_MEMBER_OPTION_UTILS.build_member_options(_window_data)


func _normalize_top_level_payload(window_data: Dictionary) -> Dictionary:
	var normalized := window_data.duplicate(true)
	for field_name in ["settlement_id", "display_name", "tier_name", "faction_id", "feedback_text"]:
		if not window_data.has(field_name) or window_data[field_name] is not String:
			return {}
		var field_value := String(window_data[field_name]).strip_edges()
		if field_value.is_empty():
			return {}
		normalized[field_name] = field_value
	if not window_data.has("state_summary_text") or window_data["state_summary_text"] is not String:
		return {}
	normalized["state_summary_text"] = String(window_data["state_summary_text"])
	if not window_data.has("footprint_size") or typeof(window_data["footprint_size"]) != TYPE_VECTOR2I:
		return {}
	var footprint_size: Vector2i = window_data["footprint_size"]
	if footprint_size.x < 1 or footprint_size.y < 1:
		return {}
	normalized["footprint_size"] = footprint_size
	for field_name in ["available_services", "facilities", "service_npcs"]:
		if not window_data.has(field_name) or window_data[field_name] is not Array:
			return {}
	return normalized


func _build_facility_entries() -> Array[Dictionary]:
	_facility_payload_valid = true
	var facilities_variant: Variant = _window_data["facilities"]
	var facilities: Array[Dictionary] = []
	if facilities_variant is not Array:
		_facility_payload_valid = false
		return facilities
	for facility_variant in facilities_variant:
		if facility_variant is not Dictionary:
			_facility_payload_valid = false
			return []
		var facility := _normalize_facility_entry(facility_variant as Dictionary)
		if facility.is_empty():
			_facility_payload_valid = false
			return []
		facilities.append(facility)
	return facilities


func _normalize_facility_entry(facility: Dictionary) -> Dictionary:
	var normalized := facility.duplicate(true)
	for field_name in ["display_name", "slot_tag"]:
		if not facility.has(field_name) or facility[field_name] is not String:
			return {}
		var field_value := String(facility[field_name]).strip_edges()
		if field_value.is_empty():
			return {}
		normalized[field_name] = field_value
	if not facility.has("interaction_type") or facility["interaction_type"] is not String:
		return {}
	normalized["interaction_type"] = String(facility["interaction_type"]).strip_edges()
	return normalized


func _build_resident_entries() -> Array[Dictionary]:
	_resident_payload_valid = true
	var residents_variant: Variant = _window_data["service_npcs"]
	var residents: Array[Dictionary] = []
	if residents_variant is not Array:
		_resident_payload_valid = false
		return residents
	for resident_variant in residents_variant:
		if resident_variant is not Dictionary:
			_resident_payload_valid = false
			return []
		var resident := _normalize_resident_entry(resident_variant as Dictionary)
		if resident.is_empty():
			_resident_payload_valid = false
			return []
		residents.append(resident)
	return residents


func _normalize_resident_entry(resident: Dictionary) -> Dictionary:
	var normalized := resident.duplicate(true)
	for field_name in ["display_name", "service_type", "facility_name"]:
		if not resident.has(field_name) or resident[field_name] is not String:
			return {}
		var field_value := String(resident[field_name]).strip_edges()
		if field_value.is_empty():
			return {}
		normalized[field_name] = field_value
	return normalized


func _build_service_entries() -> Array[Dictionary]:
	_service_payload_valid = true
	var services_variant: Variant = _window_data["available_services"]
	var services: Array[Dictionary] = []
	if services_variant is not Array:
		_service_payload_valid = false
		return services

	for service_variant in services_variant:
		if service_variant is not Dictionary:
			_service_payload_valid = false
			return []
		var service := _normalize_service_entry(service_variant as Dictionary)
		if service.is_empty():
			_service_payload_valid = false
			return []
		services.append(service)
	return services


func _normalize_service_entry(service: Dictionary) -> Dictionary:
	var normalized := service.duplicate(true)
	for legacy_field_name in ["window_kind", "service_window_kind", "panel"]:
		if service.has(legacy_field_name):
			return {}
	for field_name in [
		"action_id",
		"facility_name",
		"npc_name",
		"service_type",
		"interaction_script_id",
		"cost_label",
		"state_label",
		"summary_text",
	]:
		if not service.has(field_name) or service[field_name] is not String:
			return {}
		var field_value := String(service[field_name]).strip_edges()
		if field_value.is_empty():
			return {}
		normalized[field_name] = field_value
	if not service.has("is_enabled") or service["is_enabled"] is not bool:
		return {}
	if not service.has("disabled_reason") or service["disabled_reason"] is not String:
		return {}
	var disabled_reason := String(service["disabled_reason"]).strip_edges()
	if not bool(service["is_enabled"]) and disabled_reason.is_empty():
		return {}
	normalized["is_enabled"] = bool(service["is_enabled"])
	normalized["disabled_reason"] = disabled_reason
	if service.has("panel_kind"):
		if service["panel_kind"] is not String:
			return {}
		var panel_kind := String(service["panel_kind"]).strip_edges()
		if panel_kind.is_empty():
			return {}
		normalized["panel_kind"] = panel_kind
	if service.has("interaction_type"):
		if service["interaction_type"] is not String:
			return {}
		normalized["interaction_type"] = String(service["interaction_type"]).strip_edges()
	return normalized


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

	var display_name := PARTY_MEMBER_OPTION_UTILS.get_member_option_display_name(member_option)
	if display_name.is_empty():
		member_state_label.text = "成员：当前选择不可用。"
		return
	var roster_role := String(member_option.get("roster_role", "成员"))
	var state_summary_text := String(_window_data["state_summary_text"])
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
		var service := _resolve_service_for_selected_member(_services[index])
		var button := Button.new()
		button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		button.custom_minimum_size = Vector2(0, 58)
		button.text = _build_service_button_text(service)
		button.disabled = not bool(service["is_enabled"])
		if button.disabled:
			button.tooltip_text = String(service["disabled_reason"])
		button.pressed.connect(_on_service_button_pressed.bind(index))
		services_container.add_child(button)

	if services_container.get_child_count() == 0:
		var placeholder := Label.new()
		placeholder.text = "当前据点没有可用服务。"
		placeholder.modulate = Color(0.77, 0.83, 0.91, 0.85)
		services_container.add_child(placeholder)


func _build_service_button_text(service: Dictionary) -> String:
	var facility_name := String(service["facility_name"])
	var npc_name := String(service["npc_name"])
	var service_type := String(service["service_type"])
	var state_label := String(service["state_label"])
	var cost_label := String(service["cost_label"])
	var text := "%s · %s · %s\n%s  |  %s" % [facility_name, npc_name, service_type, state_label, cost_label]
	if not bool(service["is_enabled"]):
		var disabled_reason := String(service["disabled_reason"])
		if not disabled_reason.is_empty():
			text += "\n%s" % disabled_reason
	return text


func _build_service_summary_text(service: Dictionary) -> String:
	return String(service["summary_text"])


func _refresh_service_details() -> void:
	if _services.is_empty():
		service_state_label.text = "状态：暂无服务"
		service_cost_label.text = "费用：暂无服务"
		service_details_label.text = "当前据点没有可用服务。"
		return

	if _selected_service_index < 0 or _selected_service_index >= _services.size():
		_selected_service_index = 0
	var service := _resolve_service_for_selected_member(_services[_selected_service_index])
	service_state_label.text = String(service["state_label"])
	service_cost_label.text = String(service["cost_label"])
	service_details_label.text = _build_service_detail_text(service)


func _build_service_detail_text(service: Dictionary) -> String:
	var lines := PackedStringArray([
		"设施：%s" % String(service["facility_name"]),
		"NPC：%s" % String(service["npc_name"]),
		"服务：%s" % String(service["service_type"]),
		"交互：%s" % String(service["interaction_script_id"]),
		"状态：%s" % String(service["state_label"]),
		"费用：%s" % String(service["cost_label"]),
	])
	var disabled_reason := String(service["disabled_reason"])
	if not disabled_reason.is_empty():
		lines.append("说明：%s" % disabled_reason)
	return "\n".join(lines)


func _on_service_button_pressed(index: int) -> void:
	if index < 0 or index >= _services.size():
		return
	var service: Dictionary = _resolve_service_for_selected_member(_services[index])
	if not bool(service["is_enabled"]):
		set_feedback(String(service["disabled_reason"]))
		return
	_selected_service_index = index
	_refresh_service_details()
	var payload := _build_service_payload(service)
	action_requested.emit(_settlement_id, String(payload["action_id"]), payload)


func _build_service_payload(service: Dictionary) -> Dictionary:
	var payload := service.duplicate(true)
	payload["settlement_id"] = _settlement_id
	payload["member_id"] = String(_selected_member_id)
	payload["default_member_id"] = String(_selected_member_id)
	payload["state_summary_text"] = String(_window_data["state_summary_text"])
	payload["summary_text"] = _build_service_summary_text(service)
	payload["details_text"] = _build_service_detail_text(service)
	payload["submission_source"] = "settlement"
	var panel_kind := _resolve_service_panel_kind(service)
	if not panel_kind.is_empty():
		payload["panel_kind"] = panel_kind
	return payload


func _resolve_service_for_selected_member(service: Dictionary) -> Dictionary:
	var resolved := service.duplicate(true)
	if _selected_member_id == &"":
		return resolved
	if not service.has("member_availability"):
		return resolved
	var availability_variant = service.get("member_availability", {})
	if availability_variant is not Dictionary:
		return resolved
	var availability_by_member: Dictionary = availability_variant
	var member_availability_variant = availability_by_member.get(String(_selected_member_id), {})
	if member_availability_variant is not Dictionary:
		resolved["is_enabled"] = false
		resolved["disabled_reason"] = "当前成员不可用"
		resolved["state_label"] = "状态：当前成员不可用"
		return resolved
	var member_availability: Dictionary = member_availability_variant
	var is_enabled := bool(member_availability.get("is_enabled", false))
	var disabled_reason := String(member_availability.get("disabled_reason", "")).strip_edges()
	resolved["is_enabled"] = is_enabled
	resolved["disabled_reason"] = disabled_reason
	resolved["state_label"] = "状态：可用" if is_enabled else "状态：%s" % (disabled_reason if not disabled_reason.is_empty() else "不可用")
	return resolved


func _resolve_service_panel_kind(service: Dictionary) -> String:
	if not service.has("panel_kind"):
		return ""
	return String(service["panel_kind"])


func _clear_service_buttons() -> void:
	for child in services_container.get_children():
		child.queue_free()


func _on_member_selected(index: int) -> void:
	if index < 0 or index >= member_selector.item_count:
		_selected_member_id = &""
	else:
		_selected_member_id = ProgressionDataUtils.to_string_name(member_selector.get_item_metadata(index))
	_refresh_member_state()
	_rebuild_service_buttons()
	_refresh_service_details()


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
