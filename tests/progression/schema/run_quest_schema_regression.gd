extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const ItemContentRegistry = preload("res://scripts/player/warehouse/item_content_registry.gd")
const CharacterManagementModule = preload("res://scripts/systems/progression/character_management_module.gd")
const PartyState = preload("res://scripts/player/progression/party_state.gd")
const QuestDef = preload("res://scripts/player/progression/quest_def.gd")
const QuestState = preload("res://scripts/player/progression/quest_state.gd")
const QUEST_ITEM_IDS := [
	&"sealed_dispatch",
	&"bandit_insignia",
	&"moonfern_sample",
]

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_quest_def_round_trip_and_validation()
	_test_quest_def_from_dict_rejects_bad_schema()
	_test_quest_item_cross_reference()
	_test_quest_state_progress_and_round_trip()
	_test_quest_state_from_dict_rejects_schema_defaults()
	_test_character_management_reads_only_string_name_quest_keys()

	if _failures.is_empty():
		print("Quest schema regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Quest schema regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_quest_def_round_trip_and_validation() -> void:
	var quest_def := QuestDef.new()
	quest_def.quest_id = &"contract_wolf_pack"
	quest_def.display_name = "清理狼群"
	quest_def.provider_interaction_id = &"service_contract_board"
	quest_def.tags = [&"contract", &"bounty"]
	quest_def.accept_requirements = [
		{"requirement_type": "settlement_reputation", "settlement_id": "spring_village_01", "minimum_value": 0},
	]
	quest_def.objective_defs = [
		{
			"objective_id": "defeat_wolves",
			"objective_type": QuestDef.OBJECTIVE_DEFEAT_ENEMY,
			"target_id": "wolf_raider",
			"target_value": 3,
		},
		{
			"objective_id": "report_back",
			"objective_type": QuestDef.OBJECTIVE_SETTLEMENT_ACTION,
			"target_id": "service_contract_board",
			"target_value": 1,
		},
	]
	quest_def.reward_entries = [
		{"reward_type": QuestDef.REWARD_GOLD, "amount": 120},
		{"reward_type": QuestDef.REWARD_ITEM, "item_id": "iron_ore", "quantity": 2},
		{
			"reward_type": QuestDef.REWARD_PENDING_CHARACTER_REWARD,
			"member_id": "hero",
			"entries": [
				{
					"entry_type": "skill_unlock",
					"target_id": "charge",
					"amount": 1,
				},
			],
		},
	]

	var restored: QuestDef = QuestDef.from_dict(quest_def.to_dict())
	_assert_eq(restored.quest_id, &"contract_wolf_pack", "QuestDef 应保留 quest_id。")
	_assert_eq(restored.get_objective_ids().size(), 2, "QuestDef 应保留 objective_defs。")
	_assert_true(restored.validate_schema().is_empty(), "合法 QuestDef 不应产生 schema 错误。")

	var invalid_quest := QuestDef.new()
	invalid_quest.quest_id = &"broken_contract"
	invalid_quest.objective_defs = [
		{"objective_id": "dup", "objective_type": QuestDef.OBJECTIVE_DEFEAT_ENEMY, "target_value": 1},
		{"objective_id": "dup", "objective_type": QuestDef.OBJECTIVE_DEFEAT_ENEMY, "target_value": 1},
	]
	var errors: Array[String] = invalid_quest.validate_schema()
	_assert_true(errors.size() >= 1, "重复 objective_id 应被 validate_schema() 拒绝。")
	_assert_true(_has_error_containing(errors, "缺少 display_name"), "缺少 display_name 的 QuestDef 应被 validate_schema() 拒绝。")

	var invalid_reward_quest := QuestDef.new()
	invalid_reward_quest.quest_id = &"broken_reward_contract"
	invalid_reward_quest.objective_defs = [
		{"objective_id": "report_back", "objective_type": QuestDef.OBJECTIVE_SETTLEMENT_ACTION, "target_value": 1},
	]
	invalid_reward_quest.reward_entries = [
		{"reward_type": QuestDef.REWARD_GOLD, "amount": 0},
		{"reward_type": QuestDef.REWARD_ITEM, "item_id": "", "quantity": 0},
	]
	var reward_errors: Array[String] = invalid_reward_quest.validate_schema()
	_assert_true(reward_errors.size() >= 2, "无效 gold/item reward 应被 validate_schema() 拒绝。")

	var string_gold_reward_quest := QuestDef.new()
	string_gold_reward_quest.quest_id = &"string_gold_reward_contract"
	string_gold_reward_quest.display_name = "字符串金币奖励契约"
	string_gold_reward_quest.objective_defs = [
		{"objective_id": "report_back", "objective_type": QuestDef.OBJECTIVE_SETTLEMENT_ACTION, "target_value": 1},
	]
	string_gold_reward_quest.reward_entries = [
		{"reward_type": QuestDef.REWARD_GOLD, "amount": "2"},
	]
	var string_gold_reward_errors: Array[String] = string_gold_reward_quest.validate_schema()
	_assert_true(
		_has_error_containing(string_gold_reward_errors, "gold reward 必须有正 amount"),
		"gold reward 不应继续把字符串 amount 转成 int。"
	)

	var legacy_item_reward_quest := QuestDef.new()
	legacy_item_reward_quest.quest_id = &"legacy_item_reward_contract"
	legacy_item_reward_quest.objective_defs = [
		{"objective_id": "report_back", "objective_type": QuestDef.OBJECTIVE_SETTLEMENT_ACTION, "target_value": 1},
	]
	legacy_item_reward_quest.reward_entries = [
		{"reward_type": QuestDef.REWARD_ITEM, "target_id": "iron_ore", "amount": 2},
	]
	var legacy_reward_errors: Array[String] = legacy_item_reward_quest.validate_schema()
	_assert_true(_has_error_containing(legacy_reward_errors, "item reward 缺少 item_id"), "item reward 不应继续把 target_id 当成 item_id。")
	_assert_true(_has_error_containing(legacy_reward_errors, "item reward 必须有正 quantity"), "item reward 不应继续把 amount 当成 quantity。")
	_assert_eq(QuestDef.get_reward_item_id(legacy_item_reward_quest.reward_entries[0]), &"", "get_reward_item_id() 不应读取 target_id 旧别名。")
	_assert_eq(QuestDef.get_reward_quantity(legacy_item_reward_quest.reward_entries[0]), 0, "get_reward_quantity() 不应读取 amount 旧别名。")

	var invalid_pending_reward_quest := QuestDef.new()
	invalid_pending_reward_quest.quest_id = &"broken_pending_reward_contract"
	invalid_pending_reward_quest.objective_defs = [
		{"objective_id": "report_back", "objective_type": QuestDef.OBJECTIVE_SETTLEMENT_ACTION, "target_value": 1},
	]
	invalid_pending_reward_quest.reward_entries = [
		{
			"reward_type": QuestDef.REWARD_PENDING_CHARACTER_REWARD,
			"entries": [
				{
					"entry_type": "",
					"target_id": "",
					"amount": 0,
				},
			],
		},
	]
	var pending_reward_errors: Array[String] = invalid_pending_reward_quest.validate_schema()
	_assert_true(pending_reward_errors.size() >= 3, "无效 pending_character_reward 应被 validate_schema() 拒绝。")

	var invalid_submit_item_quest := QuestDef.new()
	invalid_submit_item_quest.quest_id = &"broken_submit_item_contract"
	invalid_submit_item_quest.objective_defs = [
		{
			"objective_id": "deliver_ore",
			"objective_type": QuestDef.OBJECTIVE_SUBMIT_ITEM,
			"target_id": "",
			"target_value": 2,
		},
	]
	var submit_item_errors: Array[String] = invalid_submit_item_quest.validate_schema()
	var found_submit_item_error := false
	for submit_item_error in submit_item_errors:
		if not submit_item_error.contains("submit_item objective deliver_ore 缺少 target_id"):
			continue
		found_submit_item_error = true
		break
	_assert_true(
		found_submit_item_error,
		"submit_item objective 缺少 target_id 时应被 validate_schema() 拒绝。"
	)

	var missing_target_value_quest := QuestDef.new()
	missing_target_value_quest.quest_id = &"missing_target_value_contract"
	missing_target_value_quest.display_name = "缺目标值契约"
	missing_target_value_quest.objective_defs = [
		{"objective_id": "report_back", "objective_type": QuestDef.OBJECTIVE_SETTLEMENT_ACTION},
	]
	var missing_target_value_errors: Array[String] = missing_target_value_quest.validate_schema()
	_assert_true(
		_has_error_containing(missing_target_value_errors, "必须显式提供 int target_value"),
		"缺少 target_value 的 QuestDef objective 不应再按 1 兼容通过。"
	)

	var string_target_value_quest := QuestDef.new()
	string_target_value_quest.quest_id = &"string_target_value_contract"
	string_target_value_quest.display_name = "字符串目标值契约"
	string_target_value_quest.objective_defs = [
		{"objective_id": "report_back", "objective_type": QuestDef.OBJECTIVE_SETTLEMENT_ACTION, "target_value": "1"},
	]
	var string_target_value_errors: Array[String] = string_target_value_quest.validate_schema()
	_assert_true(
		_has_error_containing(string_target_value_errors, "必须显式提供 int target_value"),
		"字符串 target_value 的 QuestDef objective 不应被 int() 兼容转换。"
	)


func _test_quest_def_from_dict_rejects_bad_schema() -> void:
	_assert_true(QuestDef.from_dict([]) == null, "非 Dictionary QuestDef payload 应直接拒绝。")

	for field_name in [
		"quest_id",
		"display_name",
		"description",
		"provider_interaction_id",
		"tags",
		"accept_requirements",
		"objective_defs",
		"reward_entries",
		"is_repeatable",
	]:
		var payload := _build_valid_quest_def_payload()
		payload.erase(field_name)
		_assert_true(
			QuestDef.from_dict(payload) == null,
			"缺少 QuestDef.%s 的 payload 应直接拒绝。" % field_name
		)

	var extra_field_payload := _build_valid_quest_def_payload()
	extra_field_payload["legacy_reward_amount"] = 1
	_assert_true(
		QuestDef.from_dict(extra_field_payload) == null,
		"QuestDef payload 包含非当前字段时应直接拒绝。"
	)

	for field_case in [
		{"field": "quest_id", "value": 7},
		{"field": "display_name", "value": 7},
		{"field": "description", "value": 7},
		{"field": "provider_interaction_id", "value": 7},
		{"field": "accept_requirements", "value": {}},
		{"field": "is_repeatable", "value": "false"},
	]:
		var payload := _build_valid_quest_def_payload()
		payload[String(field_case.get("field", ""))] = field_case.get("value")
		_assert_true(
			QuestDef.from_dict(payload) == null,
			"QuestDef.%s 错类型的 payload 应直接拒绝。" % String(field_case.get("field", ""))
		)

	for field_case in [
		{"field": "quest_id", "value": ""},
		{"field": "display_name", "value": ""},
		{"field": "provider_interaction_id", "value": ""},
	]:
		var payload := _build_valid_quest_def_payload()
		payload[String(field_case.get("field", ""))] = field_case.get("value")
		_assert_true(
			QuestDef.from_dict(payload) == null,
			"QuestDef.%s 为空的 payload 应直接拒绝。" % String(field_case.get("field", ""))
		)

	for field_name in [
		"tags",
		"objective_defs",
		"reward_entries",
	]:
		var payload := _build_valid_quest_def_payload()
		payload[field_name] = {}
		_assert_true(
			QuestDef.from_dict(payload) == null,
			"QuestDef.%s 非 Array 的 payload 应直接拒绝。" % field_name
		)

	var invalid_tag_type_payload := _build_valid_quest_def_payload()
	invalid_tag_type_payload["tags"] = ["contract", 7]
	_assert_true(
		QuestDef.from_dict(invalid_tag_type_payload) == null,
		"QuestDef.tags 包含非 String/StringName 元素时应直接拒绝。"
	)

	var empty_tag_payload := _build_valid_quest_def_payload()
	empty_tag_payload["tags"] = ["contract", ""]
	_assert_true(
		QuestDef.from_dict(empty_tag_payload) == null,
		"QuestDef.tags 包含空元素时应直接拒绝。"
	)

	for field_name in [
		"accept_requirements",
		"objective_defs",
		"reward_entries",
	]:
		var payload := _build_valid_quest_def_payload()
		payload[field_name] = ["not_dictionary"]
		_assert_true(
			QuestDef.from_dict(payload) == null,
			"QuestDef.%s 包含非 Dictionary 条目时应直接拒绝。" % field_name
		)

	var empty_objectives_payload := _build_valid_quest_def_payload()
	empty_objectives_payload["objective_defs"] = []
	_assert_true(
		QuestDef.from_dict(empty_objectives_payload) == null,
		"QuestDef.objective_defs 为空时应直接拒绝。"
	)

	var invalid_objective_payload := _build_valid_quest_def_payload()
	var invalid_objective_defs := invalid_objective_payload["objective_defs"] as Array
	(invalid_objective_defs[0] as Dictionary).erase("target_value")
	_assert_true(
		QuestDef.from_dict(invalid_objective_payload) == null,
		"QuestDef.objective_defs 内缺少 target_value 时应直接拒绝。"
	)

	var invalid_gold_reward_payload := _build_valid_quest_def_payload()
	invalid_gold_reward_payload["reward_entries"] = [{"reward_type": QuestDef.REWARD_GOLD, "amount": "120"}]
	_assert_true(
		QuestDef.from_dict(invalid_gold_reward_payload) == null,
		"QuestDef.reward_entries 内字符串 gold amount 应直接拒绝。"
	)

	var empty_tags_payload := _build_valid_quest_def_payload()
	empty_tags_payload["tags"] = []
	_assert_true(
		QuestDef.from_dict(empty_tags_payload) != null,
		"QuestDef.tags 为空 Array 时应允许。"
	)


func _test_quest_state_progress_and_round_trip() -> void:
	var quest_def := QuestDef.new()
	quest_def.quest_id = &"contract_wolf_pack"
	quest_def.objective_defs = [
		{
			"objective_id": "defeat_wolves",
			"objective_type": QuestDef.OBJECTIVE_DEFEAT_ENEMY,
			"target_value": 3,
		},
		{
			"objective_id": "report_back",
			"objective_type": QuestDef.OBJECTIVE_SETTLEMENT_ACTION,
			"target_value": 1,
		},
	]

	var quest_state := QuestState.new()
	quest_state.quest_id = quest_def.quest_id
	quest_state.mark_accepted(12)
	quest_state.record_objective_progress(&"defeat_wolves", 2, 3, {"enemy_template_id": "wolf_raider"})
	quest_state.record_objective_progress(&"defeat_wolves", 2, 3, {"enemy_template_id": "wolf_raider"})
	quest_state.record_objective_progress(&"report_back", 1, 1, {"settlement_id": "spring_village_01"})

	_assert_eq(quest_state.get_objective_progress(&"defeat_wolves"), 3, "QuestState 进度应按 target_value 封顶。")
	_assert_true(quest_state.has_completed_all_objectives(quest_def), "所有 objective 完成后应通过完成检查。")

	var missing_target_quest_def := QuestDef.new()
	missing_target_quest_def.quest_id = &"contract_missing_target"
	missing_target_quest_def.objective_defs = [
		{"objective_id": "report_back", "objective_type": QuestDef.OBJECTIVE_SETTLEMENT_ACTION},
	]
	var missing_target_state := QuestState.new()
	missing_target_state.quest_id = missing_target_quest_def.quest_id
	missing_target_state.mark_accepted(13)
	missing_target_state.record_objective_progress(&"report_back", 1)
	_assert_eq(missing_target_state.get_objective_progress(&"report_back"), 0, "缺 target_value 的 record_objective_progress 不应按默认 1 记录进度。")
	_assert_true(not missing_target_state.is_objective_complete(&"report_back"), "缺 target_value 的完成检查不应按默认 1 通过。")
	_assert_true(
		not missing_target_state.has_completed_all_objectives(missing_target_quest_def),
		"缺 target_value 的 QuestDef 不应驱动 has_completed_all_objectives 通过。"
	)

	quest_state.mark_completed(18)
	quest_state.mark_reward_claimed(19)
	var restored: QuestState = QuestState.from_dict(quest_state.to_dict())
	_assert_eq(restored.status_id, QuestState.STATUS_REWARDED, "QuestState 应保留完成后的状态。")
	_assert_eq(restored.reward_claimed_at_world_step, 19, "QuestState 应保留领奖时间。")
	_assert_eq(
		String(restored.last_progress_context.get("settlement_id", "")),
		"spring_village_01",
		"QuestState 应保留最近一次进度上下文。"
	)


func _test_quest_state_from_dict_rejects_schema_defaults() -> void:
	_assert_true(QuestState.from_dict([]) == null, "非 Dictionary QuestState payload 应直接拒绝。")

	for field_name in [
		"quest_id",
		"status_id",
		"objective_progress",
		"accepted_at_world_step",
		"completed_at_world_step",
		"reward_claimed_at_world_step",
		"last_progress_context",
	]:
		var payload := _build_valid_quest_state_payload()
		payload.erase(field_name)
		_assert_true(
			QuestState.from_dict(payload) == null,
			"缺少 QuestState.%s 的 payload 应直接拒绝。" % field_name
		)

	var extra_field_payload := _build_valid_quest_state_payload()
	extra_field_payload["legacy_status"] = "active"
	_assert_true(
		QuestState.from_dict(extra_field_payload) == null,
		"QuestState payload 包含非当前字段时应直接拒绝。"
	)

	for field_case in [
		{"field": "quest_id", "value": ""},
		{"field": "quest_id", "value": 12},
		{"field": "status_id", "value": 12},
		{"field": "status_id", "value": "legacy_unknown"},
		{"field": "objective_progress", "value": []},
		{"field": "accepted_at_world_step", "value": "12"},
		{"field": "completed_at_world_step", "value": "18"},
		{"field": "reward_claimed_at_world_step", "value": "19"},
		{"field": "last_progress_context", "value": []},
	]:
		var payload := _build_valid_quest_state_payload()
		payload[String(field_case.get("field", ""))] = field_case.get("value")
		_assert_true(
			QuestState.from_dict(payload) == null,
			"QuestState.%s 非法的 payload 应直接拒绝。" % String(field_case.get("field", ""))
		)

	var empty_objective_key_payload := _build_valid_quest_state_payload()
	empty_objective_key_payload["objective_progress"] = {"": 1}
	_assert_true(
		QuestState.from_dict(empty_objective_key_payload) == null,
		"QuestState.objective_progress 出现空 objective key 时应直接拒绝。"
	)

	var string_objective_key_payload := _build_valid_quest_state_payload()
	string_objective_key_payload["objective_progress"] = {12: 1}
	_assert_true(
		QuestState.from_dict(string_objective_key_payload) == null,
		"QuestState.objective_progress key 非 String/StringName 时应直接拒绝。"
	)

	var string_objective_value_payload := _build_valid_quest_state_payload()
	string_objective_value_payload["objective_progress"] = {"defeat_wolves": "1"}
	_assert_true(
		QuestState.from_dict(string_objective_value_payload) == null,
		"QuestState.objective_progress value 非 int 时应直接拒绝。"
	)

	var negative_objective_value_payload := _build_valid_quest_state_payload()
	negative_objective_value_payload["objective_progress"] = {"defeat_wolves": -1}
	_assert_true(
		QuestState.from_dict(negative_objective_value_payload) == null,
		"QuestState.objective_progress value 为负数时应直接拒绝。"
	)


func _test_character_management_reads_only_string_name_quest_keys() -> void:
	var formal_party_state := PartyState.new()
	var formal_claimable_quest := QuestState.new()
	formal_claimable_quest.quest_id = &"contract_formal_key_reward"
	formal_claimable_quest.mark_accepted(1)
	formal_claimable_quest.mark_completed(2)
	formal_party_state.set_claimable_quest_state(formal_claimable_quest)

	var formal_quest_def := _build_gold_reward_quest_def(&"contract_formal_key_reward", "正式 key 奖励", 9)
	var formal_quest_defs: Dictionary = {}
	formal_quest_defs[formal_quest_def.quest_id] = formal_quest_def
	var formal_character_management := CharacterManagementModule.new()
	formal_character_management.setup(
		formal_party_state,
		{},
		{},
		{},
		{},
		formal_quest_defs
	)
	var formal_claim_result := formal_character_management.claim_quest_reward(&"contract_formal_key_reward", 3)
	_assert_true(bool(formal_claim_result.get("ok", false)), "CharacterManagementModule 应按 StringName key 读取正式 quest_def。")
	_assert_eq(int(formal_claim_result.get("gold_delta", 0)), 9, "正式 StringName key quest reward 应正常入账。")
	_assert_true(formal_party_state.has_completed_quest(&"contract_formal_key_reward"), "正式 StringName key quest reward 领取后应进入 completed_quest_ids。")

	var legacy_party_state := PartyState.new()
	var legacy_claimable_quest := QuestState.new()
	legacy_claimable_quest.quest_id = &"contract_string_key_reward"
	legacy_claimable_quest.mark_accepted(1)
	legacy_claimable_quest.mark_completed(2)
	legacy_party_state.set_claimable_quest_state(legacy_claimable_quest)

	var legacy_quest_def := _build_gold_reward_quest_def(&"contract_string_key_reward", "旧 String key 奖励", 7)
	var legacy_quest_defs: Dictionary = {}
	legacy_quest_defs[String(legacy_quest_def.quest_id)] = legacy_quest_def
	var legacy_character_management := CharacterManagementModule.new()
	legacy_character_management.setup(
		legacy_party_state,
		{},
		{},
		{},
		{},
		legacy_quest_defs
	)
	var legacy_claim_result := legacy_character_management.claim_quest_reward(&"contract_string_key_reward", 3)
	_assert_true(not bool(legacy_claim_result.get("ok", true)), "String key-only quest_def 不应被 CharacterManagementModule 恢复。")
	_assert_eq(String(legacy_claim_result.get("error_code", "")), "quest_def_missing", "String key-only quest reward 应按缺失定义处理。")
	_assert_true(legacy_party_state.has_claimable_quest(&"contract_string_key_reward"), "String key-only quest reward 失败后应继续停留在 claimable_quests。")
	_assert_true(not legacy_party_state.has_completed_quest(&"contract_string_key_reward"), "String key-only quest reward 失败后不应进入 completed_quest_ids。")
	_assert_eq(legacy_party_state.get_gold(), 0, "String key-only quest reward 失败后不应写入金币。")


func _test_quest_item_cross_reference() -> void:
	var item_defs := ItemContentRegistry.new().get_item_defs()
	for quest_item_id in QUEST_ITEM_IDS:
		_assert_true(item_defs.has(quest_item_id), "任务 schema 回归前置：应存在正式任务物品 %s。" % String(quest_item_id))

	var quest_def := QuestDef.new()
	quest_def.quest_id = &"contract_archive_delivery"
	quest_def.display_name = "归档交接"
	quest_def.provider_interaction_id = &"service_contract_board"
	quest_def.objective_defs = [
		{
			"objective_id": "deliver_dispatch",
			"objective_type": QuestDef.OBJECTIVE_SUBMIT_ITEM,
			"target_id": String(QUEST_ITEM_IDS[0]),
			"target_value": 1,
		},
		{
			"objective_id": "deliver_insignia",
			"objective_type": QuestDef.OBJECTIVE_SUBMIT_ITEM,
			"target_id": String(QUEST_ITEM_IDS[1]),
			"target_value": 2,
		},
		{
			"objective_id": "deliver_sample",
			"objective_type": QuestDef.OBJECTIVE_SUBMIT_ITEM,
			"target_id": String(QUEST_ITEM_IDS[2]),
			"target_value": 1,
		},
	]
	quest_def.reward_entries = [
		{"reward_type": QuestDef.REWARD_GOLD, "amount": 36},
	]

	var restored: QuestDef = QuestDef.from_dict(quest_def.to_dict())
	_assert_true(restored.validate_schema().is_empty(), "引用正式任务物品的 submit_item QuestDef 不应产生 schema 错误。")
	_assert_eq(
		String(restored.get_objective_def(&"deliver_dispatch").get("target_id", "")),
		"sealed_dispatch",
		"QuestDef 应保留封缄急件的正式 target_id。"
	)
	_assert_eq(
		String(restored.get_objective_def(&"deliver_insignia").get("target_id", "")),
		"bandit_insignia",
		"QuestDef 应保留匪徒纹章的正式 target_id。"
	)
	_assert_eq(
		String(restored.get_objective_def(&"deliver_sample").get("target_id", "")),
		"moonfern_sample",
		"QuestDef 应保留月蕨样本的正式 target_id。"
	)


func _build_valid_quest_def_payload() -> Dictionary:
	return {
		"quest_id": "contract_wolf_pack",
		"display_name": "清理狼群",
		"description": "清理村外的狼群。",
		"provider_interaction_id": "service_contract_board",
		"tags": ["contract", "bounty"],
		"accept_requirements": [
			{"requirement_type": "settlement_reputation", "settlement_id": "spring_village_01", "minimum_value": 0},
		],
		"objective_defs": [
			{
				"objective_id": "defeat_wolves",
				"objective_type": QuestDef.OBJECTIVE_DEFEAT_ENEMY,
				"target_id": "wolf_raider",
				"target_value": 3,
			},
		],
		"reward_entries": [
			{"reward_type": QuestDef.REWARD_GOLD, "amount": 120},
		],
		"is_repeatable": false,
	}


func _build_valid_quest_state_payload() -> Dictionary:
	var quest_state := QuestState.new()
	quest_state.quest_id = &"contract_wolf_pack"
	quest_state.mark_accepted(12)
	quest_state.record_objective_progress(
		&"defeat_wolves",
		1,
		3,
		{"enemy_template_id": "wolf_raider"}
	)
	return quest_state.to_dict()


func _build_gold_reward_quest_def(quest_id: StringName, display_name: String, amount: int) -> QuestDef:
	var quest_def := QuestDef.new()
	quest_def.quest_id = quest_id
	quest_def.display_name = display_name
	quest_def.description = "用于验证 quest_defs key 类型。"
	quest_def.provider_interaction_id = &"service_contract_board"
	quest_def.objective_defs = [
		{
			"objective_id": "report_back",
			"objective_type": QuestDef.OBJECTIVE_SETTLEMENT_ACTION,
			"target_id": "service_contract_board",
			"target_value": 1,
		},
	]
	quest_def.reward_entries = [
		{"reward_type": QuestDef.REWARD_GOLD, "amount": amount},
	]
	return quest_def


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])


func _has_error_containing(errors: Array[String], expected_fragment: String) -> bool:
	for error in errors:
		if error.contains(expected_fragment):
			return true
	return false
