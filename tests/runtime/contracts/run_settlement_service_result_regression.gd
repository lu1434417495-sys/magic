extends SceneTree

const SETTLEMENT_SERVICE_RESULT_SCRIPT = preload("res://scripts/systems/settlement/settlement_service_result.gd")
const PENDING_CHARACTER_REWARD_SCRIPT = preload("res://scripts/systems/progression/pending_character_reward.gd")
const PENDING_CHARACTER_REWARD_ENTRY_SCRIPT = preload("res://scripts/systems/progression/pending_character_reward_entry.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_canonical_result_dictionary_shape()
	_test_dictionary_round_trip()
	_test_rejects_bad_schema()

	if _failures.is_empty():
		print("Settlement service result regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Settlement service result regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_canonical_result_dictionary_shape() -> void:
	var result := SETTLEMENT_SERVICE_RESULT_SCRIPT.new()
	result.success = true
	result.message = "settlement ok"
	result.persist_party_state = true
	result.persist_world_data = true
	result.persist_player_coord = false
	result.gold_delta = -50
	result.inventory_delta = {
		"items_added": [
			{"item_id": "healing_herb", "quantity": 2},
		],
	}
	result.pending_character_rewards = [_build_pending_reward_dictionary("hero_training_reward")]
	result.quest_progress_events = [
		_build_settlement_quest_progress_event("service:training"),
	]
	result.service_side_effects = {
		"hp_restored": {"hero": 12},
	}

	var dictionary := result.to_dictionary()

	_assert_true(dictionary.has("pending_character_rewards"), "结果应包含 pending_character_rewards。")
	_assert_true(dictionary.has("service_side_effects"), "结果应包含 service_side_effects。")
	_assert_true(not dictionary.has("pending_mastery_rewards"), "结果不应再包含 pending_mastery_rewards。")
	_assert_true(not dictionary.has("effects"), "结果不应再包含 effects。")
	_assert_eq(int(dictionary.get("gold_delta", 0)), -50, "gold_delta 应保留。")
	_assert_eq((dictionary.get("pending_character_rewards", []) as Array).size(), 1, "pending rewards 数量应保持稳定。")
	var side_effects: Dictionary = dictionary.get("service_side_effects", {})
	var hp_restored: Dictionary = side_effects.get("hp_restored", {})
	_assert_eq(int(hp_restored.get("hero", 0)), 12, "service_side_effects 应保留具体副作用。")


func _test_dictionary_round_trip() -> void:
	var default_result := SETTLEMENT_SERVICE_RESULT_SCRIPT.new()
	var default_parsed = SETTLEMENT_SERVICE_RESULT_SCRIPT.new().from_dictionary(default_result.to_dictionary())
	_assert_true(default_parsed != null, "默认正式 to_dictionary payload 应能 from_dictionary。")

	var input := {
		"success": false,
		"message": "据点结果",
		"persist_party_state": true,
		"persist_world_data": false,
		"persist_player_coord": true,
		"inventory_delta": {
			"items_removed": [
				{"item_id": "training_ticket", "quantity": 1},
			],
		},
		"gold_delta": -12,
		"pending_character_rewards": [_build_pending_reward_dictionary("hero_roundtrip_reward")],
		"quest_progress_events": [
			_build_direct_quest_progress_event("quest_b", "train_once", 2),
		],
		"service_side_effects": {
			"fog_revealed": [Vector2i(1, 2)],
		},
	}

	var result := SETTLEMENT_SERVICE_RESULT_SCRIPT.new()
	var parsed = result.from_dictionary(input)
	var dictionary := result.to_dictionary()

	_assert_true(parsed == result, "合法输入应返回当前 result 实例。")
	_assert_true(not result.success, "输入应保留 success。")
	_assert_eq(result.message, "据点结果", "输入应保留 message。")
	_assert_true(result.persist_party_state, "输入应保留 persist_party_state。")
	_assert_true(result.persist_player_coord, "输入应保留 persist_player_coord。")
	_assert_true(result.inventory_delta.has("items_removed"), "输入应保留 inventory_delta。")
	_assert_eq(result.gold_delta, -12, "输入应回填 gold_delta。")
	_assert_eq(result.pending_character_rewards.size(), 1, "pending_character_rewards 应回填。")
	_assert_eq(result.quest_progress_events.size(), 1, "quest_progress_events 应保留。")
	_assert_true(result.service_side_effects.has("fog_revealed"), "service_side_effects 应回填。")
	_assert_true(dictionary.has("inventory_delta"), "round trip 后应保留 inventory_delta。")
	_assert_true(dictionary.has("pending_character_rewards"), "round trip 后应保留 pending_character_rewards。")
	_assert_true(dictionary.has("service_side_effects"), "round trip 后应保留 service_side_effects。")
	_assert_true(not dictionary.has("pending_mastery_rewards"), "round trip 后不应出现 pending_mastery_rewards。")
	_assert_true(not dictionary.has("effects"), "round trip 后不应出现 effects。")


func _test_rejects_bad_schema() -> void:
	_assert_rejects("not a dictionary", "非 Dictionary payload 应被拒绝。")
	_assert_rejects({}, "空 Dictionary payload 应被拒绝。")

	var missing_field := _valid_dictionary()
	missing_field.erase("inventory_delta")
	_assert_rejects(missing_field, "缺少必需字段时应被拒绝。")

	var extra_field := _valid_dictionary()
	extra_field["effects"] = {}
	_assert_rejects(extra_field, "包含非当前字段时应被拒绝。")

	var non_string_key := _valid_dictionary()
	var success_value = non_string_key["success"]
	non_string_key.erase("success")
	non_string_key[1] = success_value
	_assert_rejects(non_string_key, "顶层字段 key 不是 String 时应被拒绝。")

	_assert_rejects(_dictionary_with("success", "true"), "success 类型错误时应被拒绝。")
	_assert_rejects(_dictionary_with("message", 12), "message 类型错误时应被拒绝。")
	_assert_rejects(_dictionary_with("persist_party_state", 1), "persist_party_state 类型错误时应被拒绝。")
	_assert_rejects(_dictionary_with("persist_world_data", "false"), "persist_world_data 类型错误时应被拒绝。")
	_assert_rejects(_dictionary_with("persist_player_coord", 0), "persist_player_coord 类型错误时应被拒绝。")
	_assert_rejects(_dictionary_with("inventory_delta", []), "inventory_delta 类型错误时应被拒绝。")
	_assert_rejects(_dictionary_with("gold_delta", "-12"), "gold_delta 类型错误时应被拒绝。")
	_assert_rejects(_dictionary_with("pending_character_rewards", {}), "pending_character_rewards 非 Array 时应被拒绝。")
	_assert_rejects(_dictionary_with("quest_progress_events", {}), "quest_progress_events 非 Array 时应被拒绝。")
	_assert_rejects(_dictionary_with("service_side_effects", []), "service_side_effects 类型错误时应被拒绝。")
	_assert_rejects(_dictionary_with("pending_character_rewards", ["bad"]), "pending_character_rewards 含非 Dictionary 元素时应被拒绝。")
	_assert_rejects(_dictionary_with("quest_progress_events", [12]), "quest_progress_events 含非 Dictionary 元素时应被拒绝。")

	var missing_reward_field := _valid_dictionary()
	var reward_missing_entries := _build_pending_reward_dictionary("bad_reward_missing_entries")
	reward_missing_entries.erase("entries")
	missing_reward_field["pending_character_rewards"] = [reward_missing_entries]
	_assert_rejects(missing_reward_field, "pending_character_rewards 内奖励缺字段时应被拒绝。")

	var extra_reward_field := _valid_dictionary()
	var reward_with_extra := _build_pending_reward_dictionary("bad_reward_extra")
	reward_with_extra["pending_mastery_rewards"] = []
	extra_reward_field["pending_character_rewards"] = [reward_with_extra]
	_assert_rejects(extra_reward_field, "pending_character_rewards 内奖励含旧字段时应被拒绝。")

	var bad_reward_entry_amount := _valid_dictionary()
	var reward_with_string_amount := _build_pending_reward_dictionary("bad_reward_entry_amount")
	var string_amount_entries: Array = reward_with_string_amount["entries"]
	var string_amount_entry: Dictionary = string_amount_entries[0]
	string_amount_entry["amount"] = "1"
	reward_with_string_amount["entries"] = string_amount_entries
	bad_reward_entry_amount["pending_character_rewards"] = [reward_with_string_amount]
	_assert_rejects(bad_reward_entry_amount, "pending_character_rewards 内 entry 字符串数字应被拒绝。")

	var extra_reward_entry_field := _valid_dictionary()
	var reward_with_extra_entry := _build_pending_reward_dictionary("bad_reward_entry_extra")
	var extra_entries: Array = reward_with_extra_entry["entries"]
	var extra_entry: Dictionary = extra_entries[0]
	extra_entry["amount_alias"] = 1
	reward_with_extra_entry["entries"] = extra_entries
	extra_reward_entry_field["pending_character_rewards"] = [reward_with_extra_entry]
	_assert_rejects(extra_reward_entry_field, "pending_character_rewards 内 entry 额外字段应被拒绝。")

	var missing_quest_event_type := _valid_dictionary()
	var quest_event_missing_type := _build_direct_quest_progress_event("quest_a", "train_once", 1)
	quest_event_missing_type.erase("event_type")
	missing_quest_event_type["quest_progress_events"] = [quest_event_missing_type]
	_assert_rejects(missing_quest_event_type, "quest_progress_events 缺 event_type 时应被拒绝。")

	var quest_amount_alias := _valid_dictionary()
	quest_amount_alias["quest_progress_events"] = [{
		"event_type": "progress",
		"quest_id": "quest_a",
		"objective_id": "train_once",
		"amount": 1,
	}]
	_assert_rejects(quest_amount_alias, "quest_progress_events 使用 amount 旧字段时应被拒绝。")

	_assert_rejects(_dictionary_with("quest_progress_events", [_quest_event_with("progress_delta", "1")]), "quest_progress_events 字符串 progress_delta 应被拒绝。")
	_assert_rejects(_dictionary_with("quest_progress_events", [_quest_event_with("target_value", "2")]), "quest_progress_events 字符串 target_value 应被拒绝。")
	_assert_rejects(_dictionary_with("quest_progress_events", [_quest_event_with("unexpected_field", "bad")]), "quest_progress_events 含额外字段时应被拒绝。")
	_assert_rejects(_dictionary_with("quest_progress_events", [{
		"event_type": "accept",
		"quest_id": "quest_a",
		"allow_reaccept": "false",
	}]), "quest_progress_events 字符串 bool 应被拒绝。")


func _valid_dictionary() -> Dictionary:
	return {
		"success": true,
		"message": "valid settlement result",
		"persist_party_state": true,
		"persist_world_data": false,
		"persist_player_coord": true,
		"inventory_delta": {
			"items_added": [
				{"item_id": "healing_herb", "quantity": 1},
			],
		},
		"gold_delta": 5,
		"pending_character_rewards": [_build_pending_reward_dictionary("hero_valid_reward")],
		"quest_progress_events": [
			_build_direct_quest_progress_event("quest_a", "train_once", 1),
		],
		"service_side_effects": {
			"hp_restored": {"hero": 2},
		},
	}


func _build_pending_reward_dictionary(reward_id: String) -> Dictionary:
	var reward := PENDING_CHARACTER_REWARD_SCRIPT.new()
	reward.reward_id = StringName(reward_id)
	reward.member_id = &"hero"
	reward.member_name = "Hero"
	reward.source_type = &"training"
	reward.source_id = &"training"
	reward.source_label = "旅店训练"
	reward.summary_text = "Hero 完成旅店训练。"
	var entry := PENDING_CHARACTER_REWARD_ENTRY_SCRIPT.new()
	entry.entry_type = &"skill_mastery"
	entry.target_id = &"basic_sword"
	entry.target_label = "基础剑术"
	entry.amount = 1
	entry.reason_text = "训练奖励"
	reward.entries.append(entry)
	return reward.to_dict()


func _build_direct_quest_progress_event(quest_id: String, objective_id: String, progress_delta: int) -> Dictionary:
	return {
		"event_type": "progress",
		"quest_id": quest_id,
		"objective_id": objective_id,
		"progress_delta": progress_delta,
	}


func _build_settlement_quest_progress_event(target_id: String) -> Dictionary:
	return {
		"event_type": "progress",
		"objective_type": "settlement_action",
		"target_id": target_id,
		"progress_delta": 1,
		"action_id": target_id,
		"settlement_id": "settlement_alpha",
		"member_id": "hero",
	}


func _quest_event_with(field_name: String, field_value: Variant) -> Dictionary:
	var event := _build_direct_quest_progress_event("quest_a", "train_once", 1)
	event[field_name] = field_value
	return event


func _dictionary_with(field_name: String, field_value: Variant) -> Dictionary:
	var dictionary := _valid_dictionary()
	dictionary[field_name] = field_value
	return dictionary


func _assert_rejects(payload: Variant, message: String) -> void:
	var result := SETTLEMENT_SERVICE_RESULT_SCRIPT.new()
	var parsed = result.from_dictionary(payload)
	_assert_true(parsed == null, message)


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
