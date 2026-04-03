class_name SettlementWindowSystem
extends Node

@onready var settlement_window = get_parent().get_node("SettlementWindow")

var _settlements_by_id: Dictionary = {}


func setup(settlements: Array[Dictionary]) -> void:
	_settlements_by_id.clear()
	for settlement in settlements:
		_settlements_by_id[settlement.get("settlement_id", "")] = settlement


func is_window_open() -> bool:
	return settlement_window.visible


func open_settlement_window(settlement_id: String) -> void:
	var window_data: Dictionary = get_settlement_window_data(settlement_id)
	if window_data.is_empty():
		return
	settlement_window.show_settlement(window_data)


func close_settlement_window() -> void:
	settlement_window.hide_window()


func get_settlement_window_data(settlement_id: String) -> Dictionary:
	var settlement: Dictionary = _settlements_by_id.get(settlement_id, {})
	if settlement.is_empty():
		return {}

	return {
		"settlement_id": settlement.get("settlement_id", ""),
		"display_name": settlement.get("display_name", ""),
		"tier_name": settlement.get("tier_name", ""),
		"footprint_size": settlement.get("footprint_size", Vector2i.ONE),
		"faction_id": settlement.get("faction_id", "neutral"),
		"facilities": settlement.get("facilities", []),
		"available_services": settlement.get("available_services", []),
		"service_npcs": settlement.get("service_npcs", []),
	}


func execute_settlement_action(settlement_id: String, action_id: String, payload: Dictionary) -> Dictionary:
	var settlement: Dictionary = _settlements_by_id.get(settlement_id, {})
	if settlement.is_empty():
		return {
			"success": false,
			"message": "未找到据点数据。",
		}

	var display_name: String = settlement.get("display_name", settlement_id)
	var npc_name: String = payload.get("npc_name", "值守人员")
	var facility_name: String = payload.get("facility_name", "设施")
	var service_type: String = payload.get("service_type", "服务")

	return {
		"success": true,
		"message": "%s 的 %s 在 %s 中为你处理了“%s”事务。首版窗口流程已接通。"
			% [display_name, npc_name, facility_name, service_type],
	}
