extends SceneTree

const SettlementResearchService = preload("res://scripts/systems/settlement/settlement_research_service.gd")

var _failures: Array[String] = []


class FakeSkillProgress:
	extends RefCounted

	var is_learned := false


class FakeProgression:
	extends RefCounted

	var learned_knowledge: Dictionary = {}
	var learned_skills: Dictionary = {}

	func has_knowledge(knowledge_id: StringName) -> bool:
		return learned_knowledge.has(knowledge_id)

	func get_skill_progress(skill_id: StringName):
		if not learned_skills.has(skill_id):
			return null
		var progress := FakeSkillProgress.new()
		progress.is_learned = bool(learned_skills[skill_id])
		return progress


class FakeMember:
	extends RefCounted

	var member_id: StringName = &"hero"
	var display_name := "Hero"
	var progression = FakeProgression.new()


class FakeParty:
	extends RefCounted

	var gold := 250
	var leader_member_id: StringName = &"hero"
	var active_member_ids: Array = [&"hero"]
	var pending_character_rewards: Array = []
	var _members: Dictionary = {}

	func _init() -> void:
		var hero := FakeMember.new()
		_members[&"hero"] = hero

	func can_afford(amount: int) -> bool:
		return gold >= amount

	func spend_gold(amount: int) -> bool:
		if gold < amount:
			return false
		gold -= amount
		return true

	func get_gold() -> int:
		return gold

	func get_member_state(member_id: StringName):
		return _members.get(member_id)


class CatalogOverrideResearchService:
	extends SettlementResearchService

	var catalog: Array = []

	func _init(catalog_data: Array) -> void:
		catalog = catalog_data.duplicate(true)

	func _get_research_reward_catalog() -> Array:
		return catalog


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_valid_payload_succeeds()
	_test_rejects_missing_facility_name()
	_test_rejects_non_string_npc_name()
	_test_rejects_missing_settlement_display_name()
	_test_rejects_candidate_missing_target_label()
	_test_rejects_candidate_missing_research_id()

	if _failures.is_empty():
		print("Settlement research service schema regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Settlement research service schema regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_valid_payload_succeeds() -> void:
	var party := FakeParty.new()
	var service := SettlementResearchService.new()
	var result := service.execute(_valid_settlement(), _valid_payload(), party)

	_assert_true(bool(result.get("success", false)), "正式 payload 应成功执行 research 服务。")
	_assert_eq(party.get_gold(), 50, "正式 research 成功后应扣除 200 金。")
	_assert_true(bool(result.get("persist_party_state", false)), "正式 research 成功后应要求持久化队伍状态。")
	_assert_eq(int(result.get("gold_delta", 0)), -200, "正式 research 成功结果应记录 gold_delta。")
	var rewards: Array = result.get("pending_character_rewards", [])
	_assert_eq(rewards.size(), 1, "正式 research 成功后应返回一条 pending_character_rewards。")
	var reward: Dictionary = rewards[0] if rewards.size() > 0 and rewards[0] is Dictionary else {}
	_assert_eq(String(reward.get("source_id", "")), "research_field_manual", "research 奖励应使用显式 research_id。")
	_assert_eq(String(reward.get("source_label", "")), "大图书官·研究", "research 奖励应使用正式来源标签。")
	var entry := _get_first_reward_entry(reward)
	_assert_eq(String(entry.get("entry_type", "")), "knowledge_unlock", "research 奖励 entry_type 应来自显式候选条目。")
	_assert_eq(String(entry.get("target_id", "")), "field_manual", "research 奖励 target_id 应来自显式候选条目。")
	_assert_eq(String(entry.get("target_label", "")), "野外手册", "research 奖励 target_label 不应由 target_id 回填。")
	_assert_eq(String(entry.get("reason_text", "")), "研究员整理出一份可长期翻阅的野外手册抄本。", "research 奖励 reason_text 不应由 source_label 回填。")


func _test_rejects_missing_facility_name() -> void:
	var payload := _valid_payload()
	payload.erase("facility_name")
	_assert_rejects_without_side_effect(
		"缺 facility_name 的 payload 应被拒绝且不扣金币。",
		_valid_settlement(),
		payload
	)


func _test_rejects_non_string_npc_name() -> void:
	var payload := _valid_payload()
	payload["npc_name"] = 12
	_assert_rejects_without_side_effect(
		"非 String npc_name 的 payload 应被拒绝且不扣金币。",
		_valid_settlement(),
		payload
	)


func _test_rejects_missing_settlement_display_name() -> void:
	var settlement := _valid_settlement()
	settlement.erase("display_name")
	_assert_rejects_without_side_effect(
		"缺 settlement.display_name 时应被拒绝且不扣金币。",
		settlement,
		_valid_payload()
	)


func _test_rejects_candidate_missing_target_label() -> void:
	var candidate := _valid_research_candidate()
	candidate.erase("target_label")
	var service := CatalogOverrideResearchService.new([candidate])
	_assert_rejects_without_side_effect(
		"候选缺 target_label 时应被拒绝且不扣金币。",
		_valid_settlement(),
		_valid_payload(),
		service
	)


func _test_rejects_candidate_missing_research_id() -> void:
	var candidate := _valid_research_candidate()
	candidate.erase("research_id")
	var service := CatalogOverrideResearchService.new([candidate])
	_assert_rejects_without_side_effect(
		"候选缺 research_id 时应被拒绝且不扣金币。",
		_valid_settlement(),
		_valid_payload(),
		service
	)


func _assert_rejects_without_side_effect(
	message: String,
	settlement: Dictionary,
	payload: Dictionary,
	service = null
) -> void:
	var party := FakeParty.new()
	var research_service = service if service != null else SettlementResearchService.new()
	var result: Dictionary = research_service.execute(settlement, payload, party)
	_assert_true(not bool(result.get("success", true)), message)
	_assert_eq(party.get_gold(), 250, "%s 金币不应变化。" % message)
	var result_rewards: Array = result.get("pending_character_rewards", [])
	_assert_eq(result_rewards.size(), 0, "%s 失败结果不应包含 pending_character_rewards。" % message)
	_assert_eq(party.pending_character_rewards.size(), 0, "%s party_state 不应写入 pending_character_rewards。" % message)


func _valid_settlement() -> Dictionary:
	return {
		"settlement_id": "graystone_town_01",
		"display_name": "灰石镇",
	}


func _valid_payload() -> Dictionary:
	return {
		"facility_name": "大图书馆",
		"npc_name": "大图书官",
		"service_type": "研究",
	}


func _valid_research_candidate() -> Dictionary:
	return {
		"research_id": "research_field_manual",
		"entry_type": "knowledge_unlock",
		"target_id": "field_manual",
		"target_label": "野外手册",
		"reason_text": "研究员整理出一份可长期翻阅的野外手册抄本。",
	}


func _get_first_reward_entry(reward_data: Dictionary) -> Dictionary:
	var entries: Array = reward_data.get("entries", [])
	if entries.is_empty() or entries[0] is not Dictionary:
		return {}
	return (entries[0] as Dictionary).duplicate(true)


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
