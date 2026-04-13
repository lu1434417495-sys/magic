## 文件说明：该脚本属于聚落窗口系统相关的系统脚本，集中维护聚落窗口、按标识索引的聚落集合等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name SettlementWindowSystem
extends Node

## 字段说明：缓存聚落窗口节点，负责展示聚落详情并发出交互请求。
@onready var settlement_window = get_parent().get_node("SettlementWindow")

## 字段说明：记录按标识索引的聚落集合，作为查表、序列化和跨系统引用时使用的主键。
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
			"pending_mastery_rewards": [],
		}

	var display_name: String = settlement.get("display_name", settlement_id)
	var npc_name: String = payload.get("npc_name", "值守人员")
	var facility_name: String = payload.get("facility_name", "设施")
	var service_type: String = payload.get("service_type", "服务")
	var pending_mastery_rewards := _extract_pending_mastery_rewards(action_id, payload, facility_name, npc_name, service_type)

	return {
		"success": true,
		"message": "%s 的 %s 在 %s 中为你处理了“%s”事务。首版窗口流程已接通。"
			% [display_name, npc_name, facility_name, service_type],
		"pending_mastery_rewards": pending_mastery_rewards,
	}


func _extract_pending_mastery_rewards(
	action_id: String,
	payload: Dictionary,
	facility_name: String,
	npc_name: String,
	service_type: String
) -> Array[Dictionary]:
	var rewards: Array[Dictionary] = []
	var default_source_type := _resolve_default_mastery_source_type(action_id, service_type, payload)
	var default_source_label := _resolve_default_mastery_source_label(facility_name, npc_name, service_type)
	var explicit_rewards_variant = payload.get("pending_mastery_rewards", [])
	if explicit_rewards_variant is Array:
		for reward_variant in explicit_rewards_variant:
			if reward_variant is not Dictionary:
				continue
			var reward_data: Dictionary = reward_variant.duplicate(true)
			if String(reward_data.get("member_id", "")).is_empty():
				reward_data["member_id"] = String(payload.get("member_id", ""))
			if String(reward_data.get("source_type", "")).is_empty():
				reward_data["source_type"] = String(default_source_type)
			if String(reward_data.get("source_label", "")).is_empty():
				reward_data["source_label"] = default_source_label
			if reward_data.has("mastery_entries") and not reward_data.has("entries"):
				reward_data["entries"] = reward_data.get("mastery_entries", [])
			rewards.append(reward_data)

	var mastery_entries_variant = payload.get("mastery_entries", [])
	if mastery_entries_variant is Array:
		var member_id := String(payload.get("member_id", ""))
		if not member_id.is_empty():
			rewards.append({
				"member_id": member_id,
				"source_type": String(default_source_type),
				"source_label": default_source_label,
				"entries": mastery_entries_variant.duplicate(true),
				"summary_text": String(payload.get("summary_text", "")),
			})

	return rewards


func _resolve_default_mastery_source_type(
	action_id: String,
	service_type: String,
	payload: Dictionary
) -> StringName:
	var explicit_source_type := String(payload.get("mastery_source_type", ""))
	if not explicit_source_type.is_empty():
		return StringName(explicit_source_type)
	var combined_label := "%s %s" % [action_id, service_type]
	if combined_label.contains("传授") or combined_label.contains("指点"):
		return &"npc_teach"
	return &"training"


func _resolve_default_mastery_source_label(
	facility_name: String,
	npc_name: String,
	service_type: String
) -> String:
	if not service_type.is_empty():
		return "%s·%s" % [npc_name, service_type]
	if not facility_name.is_empty():
		return facility_name
	return "据点服务"
