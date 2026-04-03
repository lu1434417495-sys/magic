class_name SettlementWindow
extends Control

signal action_requested(settlement_id: String, action_id: String, payload: Dictionary)
signal closed

@onready var shade: ColorRect = $Shade
@onready var title_label: Label = $CenterContainer/Panel/MarginContainer/Content/Header/HeaderText/TitleLabel
@onready var meta_label: Label = $CenterContainer/Panel/MarginContainer/Content/Header/HeaderText/MetaLabel
@onready var facilities_label: RichTextLabel = $CenterContainer/Panel/MarginContainer/Content/Body/LeftColumn/FacilitiesLabel
@onready var resident_label: RichTextLabel = $CenterContainer/Panel/MarginContainer/Content/Body/LeftColumn/ResidentLabel
@onready var feedback_label: Label = $CenterContainer/Panel/MarginContainer/Content/Body/RightColumn/FeedbackLabel
@onready var services_container: VBoxContainer = $CenterContainer/Panel/MarginContainer/Content/Body/RightColumn/ServicesScroll/ServicesContainer
@onready var close_button: Button = $CenterContainer/Panel/MarginContainer/Content/Header/CloseButton

var _settlement_id := ""


func _ready() -> void:
	hide_window()
	shade.gui_input.connect(_on_shade_gui_input)
	close_button.pressed.connect(_close_from_button)


func show_settlement(window_data: Dictionary) -> void:
	_settlement_id = window_data.get("settlement_id", "")
	visible = true

	var display_name: String = window_data.get("display_name", "据点")
	var tier_name: String = window_data.get("tier_name", "未知")
	var footprint_size: Vector2i = window_data.get("footprint_size", Vector2i.ONE)
	var faction_id: String = window_data.get("faction_id", "neutral")
	var facilities: Array = window_data.get("facilities", [])
	var service_npcs: Array = window_data.get("service_npcs", [])
	var services: Array = window_data.get("available_services", [])

	title_label.text = display_name
	meta_label.text = "%s  |  占地 %dx%d  |  阵营 %s" % [tier_name, footprint_size.x, footprint_size.y, faction_id]
	facilities_label.text = _build_facility_text(facilities)
	resident_label.text = _build_resident_text(service_npcs)
	feedback_label.text = "据点通过窗口交付，不切换到城内地图。"
	_rebuild_service_buttons(services)


func hide_window() -> void:
	visible = false
	_settlement_id = ""
	feedback_label.text = ""
	_clear_service_buttons()


func set_feedback(message: String) -> void:
	feedback_label.text = message


func _rebuild_service_buttons(services: Array) -> void:
	_clear_service_buttons()

	for service in services:
		var button := Button.new()
		button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		button.custom_minimum_size = Vector2(0, 44)
		button.text = "%s · %s · %s" % [
			service.get("facility_name", "设施"),
			service.get("npc_name", "NPC"),
			service.get("service_type", "服务"),
		]
		button.pressed.connect(_on_service_button_pressed.bind(
			service.get("action_id", ""),
			{
				"facility_id": service.get("facility_id", ""),
				"facility_name": service.get("facility_name", ""),
				"npc_id": service.get("npc_id", ""),
				"npc_name": service.get("npc_name", ""),
				"service_type": service.get("service_type", ""),
			}
		))
		services_container.add_child(button)

	if services_container.get_child_count() == 0:
		var placeholder := Label.new()
		placeholder.text = "当前据点没有可用服务。"
		placeholder.modulate = Color(0.77, 0.83, 0.91, 0.85)
		services_container.add_child(placeholder)


func _clear_service_buttons() -> void:
	for child in services_container.get_children():
		child.queue_free()


func _build_facility_text(facilities: Array) -> String:
	if facilities.is_empty():
		return "设施：暂无"

	var lines: PackedStringArray = ["设施："]
	for facility in facilities:
		lines.append("- %s [%s]" % [
			facility.get("display_name", "设施"),
			facility.get("slot_tag", "未标记"),
		])
	return "\n".join(lines)


func _build_resident_text(service_npcs: Array) -> String:
	if service_npcs.is_empty():
		return "驻留 NPC：暂无"

	var lines: PackedStringArray = ["驻留 NPC："]
	for npc in service_npcs:
		lines.append("- %s · %s" % [
			npc.get("display_name", "NPC"),
			npc.get("service_type", "服务"),
		])
	return "\n".join(lines)


func _on_service_button_pressed(action_id: String, payload: Dictionary) -> void:
	action_requested.emit(_settlement_id, action_id, payload)


func _close_from_button() -> void:
	hide_window()
	closed.emit()


func _on_shade_gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		hide_window()
		closed.emit()
