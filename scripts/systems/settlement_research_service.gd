## 文件说明：该脚本属于聚落研究服务相关的服务脚本，集中处理 research 入口的正式交付、基础花费和 canonical 结果构造。
## 审查重点：重点核对 interaction_script_id、金币消耗、失败反馈以及 SettlementServiceResult 的字段稳定性。
## 备注：当前只落地 research 的正式 dispatch 与基础成本；奖励构造会在后续 story 接到 pending reward 主链。

class_name SettlementResearchService
extends RefCounted

const SETTLEMENT_SERVICE_RESULT_SCRIPT = preload("res://scripts/systems/settlement_service_result.gd")

const RESEARCH_INTERACTION_ID := "service_research"
const RESEARCH_GOLD_COST := 200


func is_supported_interaction(interaction_script_id: String) -> bool:
	return interaction_script_id.strip_edges() == RESEARCH_INTERACTION_ID


func build_service_metadata(party_state) -> Dictionary:
	var can_afford_research: bool = party_state != null and party_state.can_afford(RESEARCH_GOLD_COST)
	return {
		"cost_label": "%d 金" % RESEARCH_GOLD_COST,
		"is_enabled": can_afford_research,
		"disabled_reason": "" if can_afford_research else "金币不足",
	}


func execute(
	settlement: Dictionary,
	payload: Dictionary,
	party_state,
	quest_progress_events: Array = []
) -> Dictionary:
	if party_state == null:
		return _build_result(false, "当前不存在队伍数据。", quest_progress_events)
	if not party_state.spend_gold(RESEARCH_GOLD_COST):
		return _build_result(false, "金币不足，无法委托研究。", quest_progress_events)

	var settlement_name := String(settlement.get("display_name", settlement.get("settlement_id", "据点")))
	var facility_name := String(payload.get("facility_name", "档案馆"))
	var npc_name := String(payload.get("npc_name", "研究员"))
	var service_type := String(payload.get("service_type", "研究"))
	var message := "%s 的 %s 已收下 %d 金研究经费，由 %s 启动本次%s委托。" % [
		settlement_name,
		facility_name,
		RESEARCH_GOLD_COST,
		npc_name,
		service_type,
	]
	return _build_result(
		true,
		message,
		quest_progress_events,
		true,
		-RESEARCH_GOLD_COST,
		{
			"research_interaction_id": RESEARCH_INTERACTION_ID,
			"gold_spent": RESEARCH_GOLD_COST,
			"facility_name": facility_name,
		}
	)


func _build_result(
	success: bool,
	message: String,
	quest_progress_events: Array,
	persist_party_state: bool = false,
	gold_delta: int = 0,
	service_side_effects: Dictionary = {}
) -> Dictionary:
	var result := SETTLEMENT_SERVICE_RESULT_SCRIPT.new()
	result.success = success
	result.message = message
	result.persist_party_state = persist_party_state
	result.gold_delta = gold_delta
	result.quest_progress_events = _duplicate_dictionary_array(quest_progress_events)
	result.service_side_effects = service_side_effects.duplicate(true) if service_side_effects is Dictionary else {}
	return result.to_dictionary()


func _duplicate_dictionary_array(value) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	if value is not Array:
		return result
	for entry_variant in value:
		if entry_variant is Dictionary:
			result.append((entry_variant as Dictionary).duplicate(true))
	return result
