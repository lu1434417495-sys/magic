extends SceneTree

const SETTLEMENT_SERVICE_RESULT_SCRIPT = preload("res://scripts/systems/settlement_service_result.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_canonical_result_dictionary_shape()
	_test_dictionary_round_trip()

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
	result.pending_character_rewards = [
		{"member_id": "hero", "source_type": "training", "source_id": "training", "source_label": "旅店"},
	]
	result.quest_progress_events = [
		{"quest_id": "quest_a", "progress_delta": 1},
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
	var input := {
		"success": false,
		"message": "据点结果",
		"persist_party_state": true,
		"persist_world_data": false,
		"persist_player_coord": true,
		"gold_delta": -12,
		"pending_character_rewards": [
			{"member_id": "hero", "source_type": "training", "source_id": "training", "source_label": "来源"},
		],
		"quest_progress_events": [
			{"quest_id": "quest_b", "progress_delta": 2},
		],
		"service_side_effects": {
			"fog_revealed": [Vector2i(1, 2)],
		},
	}

	var result := SETTLEMENT_SERVICE_RESULT_SCRIPT.new()
	result.from_dictionary(input)
	var dictionary := result.to_dictionary()

	_assert_true(not result.success, "输入应保留 success。")
	_assert_eq(result.message, "据点结果", "输入应保留 message。")
	_assert_true(result.persist_party_state, "输入应保留 persist_party_state。")
	_assert_true(result.persist_player_coord, "输入应保留 persist_player_coord。")
	_assert_eq(result.gold_delta, -12, "输入应回填 gold_delta。")
	_assert_eq(result.pending_character_rewards.size(), 1, "pending_character_rewards 应回填。")
	_assert_eq(result.quest_progress_events.size(), 1, "quest_progress_events 应保留。")
	_assert_true(result.service_side_effects.has("fog_revealed"), "service_side_effects 应回填。")
	_assert_true(dictionary.has("pending_character_rewards"), "round trip 后应保留 pending_character_rewards。")
	_assert_true(dictionary.has("service_side_effects"), "round trip 后应保留 service_side_effects。")
	_assert_true(not dictionary.has("pending_mastery_rewards"), "round trip 后不应出现 pending_mastery_rewards。")
	_assert_true(not dictionary.has("effects"), "round trip 后不应出现 effects。")


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
