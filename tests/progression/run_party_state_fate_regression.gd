extends SceneTree

const PartyMemberState = preload("res://scripts/player/progression/party_member_state.gd")
const PartyState = preload("res://scripts/player/progression/party_state.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_party_state_fate_fields_round_trip()
	_test_party_state_from_dict_missing_fate_fields_falls_back_to_defaults()

	if _failures.is_empty():
		print("PartyState fate regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("PartyState fate regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_party_state_fate_fields_round_trip() -> void:
	var party_state := _build_party_state()
	party_state.set_fate_run_flag(&"fortuna_guidance_blessed", true)
	party_state.set_fate_run_flag(&"doom_gate_opened", false)

	_assert_true(party_state.has_fate_run_flag(&"fortuna_guidance_blessed"), "写接口应保留 true 的命运周目标记。")
	_assert_true(
		not party_state.get_fate_run_flag(&"doom_gate_opened", true),
		"写接口应保留 false 的命运周目标记。"
	)

	var payload: Dictionary = party_state.to_dict()
	_assert_true(not payload.has("party_drop_luck_source_member_id"), "掉落承担者字段已废弃，不应继续写入 PartyState 存档。")
	var payload_flags: Dictionary = payload.get("fate_run_flags", {})
	_assert_true(not payload_flags.is_empty(), "序列化结果应暴露 fate_run_flags 字典。")
	_assert_true(payload_flags.has("fortuna_guidance_blessed"), "fate_run_flags 应使用稳定字符串键写入存档。")
	_assert_true(payload_flags.has("doom_gate_opened"), "false 的命运周目标记也应稳定写入存档。")
	_assert_true(bool(payload_flags.get("fortuna_guidance_blessed", false)), "true 的命运周目标记不应在序列化时丢失。")
	_assert_true(not bool(payload_flags.get("doom_gate_opened", true)), "false 的命运周目标记不应在序列化时漂移。")

	var restored: PartyState = PartyState.from_dict(payload)
	_assert_true(restored != null, "带 fate 字段的 PartyState 应能完成 round-trip。")
	if restored == null:
		return

	_assert_true(restored.has_fate_run_flag(&"fortuna_guidance_blessed"), "round-trip 后应保留 true 的命运周目标记。")
	_assert_true(
		not restored.get_fate_run_flag(&"doom_gate_opened", true),
		"round-trip 后应保留 false 的命运周目标记。"
	)


func _test_party_state_from_dict_missing_fate_fields_falls_back_to_defaults() -> void:
	var legacy_party_state = PartyState.from_dict({
		"version": 3,
		"gold": 180,
		"leader_member_id": "hero",
		"main_character_member_id": "hero",
		"active_member_ids": ["hero"],
		"reserve_member_ids": [],
		"member_states": {
			"hero": _build_party_member_state(&"hero", "Hero").to_dict(),
		},
		"pending_character_rewards": [],
		"active_quests": [],
		"claimable_quests": [],
		"completed_quest_ids": [],
		"warehouse_state": {"stacks": [], "equipment_instances": []},
	})
	_assert_true(legacy_party_state != null, "缺少 fate 字段的旧 PartyState shape 应能回退到默认值。")
	if legacy_party_state == null:
		return

	_assert_true(legacy_party_state.get_fate_run_flags().is_empty(), "旧存档缺少 fate_run_flags 时应回退到空字典。")

	var normalized_payload: Dictionary = legacy_party_state.to_dict()
	_assert_true(not normalized_payload.has("party_drop_luck_source_member_id"), "回填后的 PartyState 再序列化时不应回写旧掉落承担者字段。")
	_assert_true(normalized_payload.has("fate_run_flags"), "回填后的 PartyState 再序列化时应带上 fate_run_flags 字段。")


func _build_party_state() -> PartyState:
	var party_state := PartyState.new()
	party_state.leader_member_id = &"hero"
	party_state.main_character_member_id = &"hero"
	party_state.active_member_ids = [&"hero"]
	party_state.set_member_state(_build_party_member_state(&"hero", "Hero"))
	return party_state


func _build_party_member_state(member_id: StringName, display_name: String) -> PartyMemberState:
	var member_state := PartyMemberState.new()
	member_state.member_id = member_id
	member_state.display_name = display_name
	member_state.progression.unit_id = member_id
	member_state.progression.display_name = display_name
	return member_state


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
