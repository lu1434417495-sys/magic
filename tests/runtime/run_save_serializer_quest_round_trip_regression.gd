extends SceneTree

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/persistence/game_session.gd")
const PartyMemberState = preload("res://scripts/player/progression/party_member_state.gd")
const PartyState = preload("res://scripts/player/progression/party_state.gd")
const QuestState = preload("res://scripts/player/progression/quest_state.gd")

const TEST_WORLD_CONFIG := "res://data/configs/world_map/test_world_map_config.tres"

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_save_serializer_round_trip_preserves_party_quest_schema()
	_test_world_map_template_bindings_round_trip()
	_test_extract_save_meta_recovers_missing_slot_fields()
	_test_normalize_party_state_keeps_main_character_active()
	_test_normalize_party_state_preserves_explicit_dead_main_character()
	_test_party_state_from_dict_requires_main_character_member_id()
	_test_party_state_from_dict_rejects_overlapping_quest_buckets()
	_test_decode_v5_payload_rejects_missing_main_character_member_id()

	if _failures.is_empty():
		print("Save serializer quest round trip regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Save serializer quest round trip regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_save_serializer_round_trip_preserves_party_quest_schema() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_true(create_error == OK, "GameSession 应能基于测试世界配置创建新存档。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return

	var party_state = game_session.get_party_state()
	var quest_state := QuestState.new()
	quest_state.quest_id = &"contract_wolf_pack"
	quest_state.mark_accepted(8)
	quest_state.record_objective_progress(&"defeat_wolves", 2, 3, {"enemy_template_id": "wolf_raider"})
	party_state.set_active_quest_state(quest_state)
	var claimable_quest := QuestState.new()
	claimable_quest.quest_id = &"contract_settlement_warehouse"
	claimable_quest.mark_accepted(5)
	claimable_quest.mark_completed(11)
	party_state.set_claimable_quest_state(claimable_quest)
	party_state.add_completed_quest_id(&"intro_contract")

	var serializer = game_session._save_serializer
	_assert_true(serializer != null, "GameSession 应暴露已初始化的 SaveSerializer。")
	if serializer == null:
		_cleanup_test_session(game_session)
		return

	var payload: Dictionary = serializer.build_save_payload(
		game_session.get_active_save_id(),
		game_session.get_generation_config_path(),
		game_session.get_active_save_meta(),
		game_session.get_world_data(),
		game_session.get_player_coord(),
		game_session.get_player_faction_id(),
		party_state,
		int(Time.get_unix_time_from_system())
	)
	var decode_result: Dictionary = serializer.decode_v5_payload(
		payload,
		game_session.get_generation_config_path(),
		game_session.get_generation_config(),
		game_session.get_active_save_meta()
	)
	_assert_eq(int(decode_result.get("error", ERR_INVALID_DATA)), OK, "SaveSerializer 应能成功解码带 quest schema 的 payload。")
	if int(decode_result.get("error", ERR_INVALID_DATA)) != OK:
		_cleanup_test_session(game_session)
		return

	var restored_party_state = decode_result.get("party_state")
	_assert_true(restored_party_state != null, "解码后的 payload 应返回 PartyState。")
	_assert_eq(restored_party_state.version, 3, "Quest schema 接入后 PartyState.version 应保持为 3。")
	_assert_eq(restored_party_state.main_character_member_id, party_state.main_character_member_id, "完整 save round-trip 后应保留 main_character_member_id。")
	_assert_true(restored_party_state.has_active_quest(&"contract_wolf_pack"), "SaveSerializer 往返后应保留 active_quests。")
	_assert_true(restored_party_state.has_claimable_quest(&"contract_settlement_warehouse"), "SaveSerializer 往返后应保留 claimable_quests。")
	_assert_true(restored_party_state.has_completed_quest(&"intro_contract"), "SaveSerializer 往返后应保留 completed_quest_ids。")
	var restored_quest: QuestState = restored_party_state.get_active_quest_state(&"contract_wolf_pack")
	var restored_claimable_quest: QuestState = restored_party_state.get_claimable_quest_state(&"contract_settlement_warehouse")
	_assert_true(restored_quest != null, "SaveSerializer 往返后应恢复 QuestState。")
	_assert_true(restored_claimable_quest != null, "SaveSerializer 往返后应恢复待领奖励 QuestState。")
	if restored_quest != null:
		_assert_eq(restored_quest.get_objective_progress(&"defeat_wolves"), 2, "QuestState 进度应穿过 save payload 保持稳定。")
		_assert_eq(restored_quest.accepted_at_world_step, 8, "QuestState 接取时间应穿过 save payload 保持稳定。")
	if restored_claimable_quest != null:
		_assert_eq(restored_claimable_quest.completed_at_world_step, 11, "待领奖励 QuestState 完成时间应穿过 save payload 保持稳定。")

	_cleanup_test_session(game_session)


func _test_world_map_template_bindings_round_trip() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_true(create_error == OK, "GameSession 应能基于测试世界配置生成模板绑定世界。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return

	var serializer = game_session._save_serializer
	_assert_true(serializer != null, "模板绑定回归需要已初始化的 SaveSerializer。")
	if serializer == null:
		_cleanup_test_session(game_session)
		return

	var binding_snapshot := _extract_first_template_binding(game_session.get_world_data())
	_assert_true(not binding_snapshot.is_empty(), "测试世界应至少生成一条 settlement/facility/npc 模板绑定。")
	if binding_snapshot.is_empty():
		_cleanup_test_session(game_session)
		return

	_assert_true(
		String(binding_snapshot.get("settlement_template_id", "")) != String(binding_snapshot.get("settlement_id", "")),
		"据点运行时记录应区分 settlement 模板 id 与实例 id。"
	)
	_assert_true(
		String(binding_snapshot.get("facility_template_id", "")) != String(binding_snapshot.get("facility_id", "")),
		"设施运行时记录应区分 facility 模板 id 与实例 id。"
	)
	_assert_true(
		String(binding_snapshot.get("npc_template_id", "")) != String(binding_snapshot.get("npc_id", "")),
		"NPC 运行时记录应区分 npc 模板 id 与实例 id。"
	)
	_assert_eq(
		String(binding_snapshot.get("service_facility_id", "")),
		String(binding_snapshot.get("facility_id", "")),
		"服务入口应绑定到设施实例 id。"
	)
	_assert_eq(
		String(binding_snapshot.get("service_facility_template_id", "")),
		String(binding_snapshot.get("facility_template_id", "")),
		"服务入口应保留设施模板 id。"
	)
	_assert_eq(
		String(binding_snapshot.get("service_npc_id", "")),
		String(binding_snapshot.get("npc_id", "")),
		"服务入口应绑定到 NPC 实例 id。"
	)
	_assert_eq(
		String(binding_snapshot.get("service_npc_template_id", "")),
		String(binding_snapshot.get("npc_template_id", "")),
		"服务入口应保留 NPC 模板 id。"
	)

	var payload: Dictionary = serializer.build_save_payload(
		game_session.get_active_save_id(),
		game_session.get_generation_config_path(),
		game_session.get_active_save_meta(),
		game_session.get_world_data(),
		game_session.get_player_coord(),
		game_session.get_player_faction_id(),
		game_session.get_party_state(),
		int(Time.get_unix_time_from_system())
	)
	var decode_result: Dictionary = serializer.decode_v5_payload(
		payload,
		game_session.get_generation_config_path(),
		game_session.get_generation_config(),
		game_session.get_active_save_meta()
	)
	_assert_eq(int(decode_result.get("error", ERR_INVALID_DATA)), OK, "模板绑定 world_data 应能成功穿过 SaveSerializer。")
	if int(decode_result.get("error", ERR_INVALID_DATA)) == OK:
		var restored_binding_snapshot := _extract_first_template_binding(decode_result.get("world_data", {}))
		_assert_eq(
			restored_binding_snapshot,
			binding_snapshot,
			"SaveSerializer 往返后应保留据点模板绑定快照。"
		)

	_cleanup_test_session(game_session)


func _test_extract_save_meta_recovers_missing_slot_fields() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_true(create_error == OK, "Save meta 恢复回归需要可创建的测试世界。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return

	var serializer = game_session._save_serializer
	_assert_true(serializer != null, "Save meta 恢复回归需要已初始化的 SaveSerializer。")
	if serializer == null:
		_cleanup_test_session(game_session)
		return

	var payload: Dictionary = serializer.build_save_payload(
		game_session.get_active_save_id(),
		game_session.get_generation_config_path(),
		game_session.get_active_save_meta(),
		game_session.get_world_data(),
		game_session.get_player_coord(),
		game_session.get_player_faction_id(),
		game_session.get_party_state(),
		int(Time.get_unix_time_from_system())
	)
	payload["save_slot_meta"] = {
		"world_preset_id": String(game_session.get_active_save_meta().get("world_preset_id", "")),
	}

	var recovered_meta: Dictionary = serializer.extract_save_meta_from_payload(payload, game_session.get_active_save_id())
	_assert_true(not recovered_meta.is_empty(), "缺失 display_name/world_size/timestamps 的 save meta 应能从 payload 恢复。")
	if not recovered_meta.is_empty():
		_assert_eq(
			String(recovered_meta.get("display_name", "")),
			String(game_session.get_active_save_id()),
			"缺失 display_name 时应回退到 save_id。"
		)
		_assert_eq(
			String(recovered_meta.get("world_preset_name", "")),
			String(game_session.get_active_save_meta().get("world_preset_name", "")),
			"缺失 world_preset_name 时应回退到 registry 的预设名。"
		)
		_assert_eq(
			recovered_meta.get("world_size_cells", Vector2i.ZERO),
			game_session.get_active_save_meta().get("world_size_cells", Vector2i.ZERO),
			"缺失 world_size_cells 时应通过 generation config 恢复。"
		)
		_assert_true(
			int(recovered_meta.get("created_at_unix_time", 0)) > 0,
			"缺失 created_at_unix_time 时应回退到 payload.meta.saved_at_unix_time。"
		)
		_assert_true(
			int(recovered_meta.get("updated_at_unix_time", 0)) > 0,
			"缺失 updated_at_unix_time 时应回退到 payload.meta.saved_at_unix_time。"
		)

	_cleanup_test_session(game_session)


func _test_normalize_party_state_keeps_main_character_active() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_true(create_error == OK, "主角上阵归一化回归需要可创建的测试世界。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return

	var serializer = game_session._save_serializer
	_assert_true(serializer != null, "主角上阵归一化回归需要已初始化的 SaveSerializer。")
	if serializer == null:
		_cleanup_test_session(game_session)
		return

	var invalid_party_state := PartyState.new()
	invalid_party_state.main_character_member_id = &"hero"
	invalid_party_state.leader_member_id = &"mage"
	invalid_party_state.active_member_ids = [&"mage", &"rogue", &"cleric", &"tank"]
	invalid_party_state.reserve_member_ids = [&"hero"]
	invalid_party_state.set_member_state(_build_party_member_state(&"hero", "Hero"))
	invalid_party_state.set_member_state(_build_party_member_state(&"mage", "Mage"))
	invalid_party_state.set_member_state(_build_party_member_state(&"rogue", "Rogue"))
	invalid_party_state.set_member_state(_build_party_member_state(&"cleric", "Cleric"))
	invalid_party_state.set_member_state(_build_party_member_state(&"tank", "Tank"))

	var normalized_party_state = serializer.normalize_party_state(invalid_party_state)
	_assert_true(normalized_party_state != null, "归一化后应返回 PartyState。")
	if normalized_party_state == null:
		_cleanup_test_session(game_session)
		return

	_assert_true(
		normalized_party_state.active_member_ids.has(&"hero"),
		"归一化后主角必须重新回到 active roster。"
	)
	_assert_true(
		not normalized_party_state.reserve_member_ids.has(&"hero"),
		"归一化后主角不应继续留在 reserve roster。"
	)
	_assert_eq(normalized_party_state.active_member_ids.size(), 4, "归一化后 active roster 仍应遵守人数上限。")
	var roster_member_ids: Dictionary = {}
	for member_id in normalized_party_state.active_member_ids:
		roster_member_ids[String(member_id)] = true
	for member_id in normalized_party_state.reserve_member_ids:
		roster_member_ids[String(member_id)] = true
	_assert_eq(roster_member_ids.size(), 5, "归一化后所有存活成员应各自只出现一次。")

	_cleanup_test_session(game_session)


func _test_normalize_party_state_preserves_explicit_dead_main_character() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_true(create_error == OK, "显式死亡主角回归需要可创建的测试世界。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return

	var serializer = game_session._save_serializer
	_assert_true(serializer != null, "显式死亡主角回归需要已初始化的 SaveSerializer。")
	if serializer == null:
		_cleanup_test_session(game_session)
		return

	var invalid_party_state := PartyState.new()
	invalid_party_state.main_character_member_id = &"hero"
	invalid_party_state.leader_member_id = &"hero"
	invalid_party_state.active_member_ids = [&"hero", &"mage"]
	invalid_party_state.reserve_member_ids = [&"cleric"]
	invalid_party_state.set_member_state(_build_party_member_state(&"hero", "Hero", true))
	invalid_party_state.set_member_state(_build_party_member_state(&"mage", "Mage"))
	invalid_party_state.set_member_state(_build_party_member_state(&"cleric", "Cleric"))

	_assert_eq(
		invalid_party_state.get_resolved_main_character_member_id(),
		&"hero",
		"显式主角即使死亡，也应继续保留给正式 GameOver 链使用。"
	)

	var normalized_party_state = serializer.normalize_party_state(invalid_party_state)
	_assert_true(normalized_party_state != null, "显式死亡主角归一化后应返回 PartyState。")
	if normalized_party_state == null:
		_cleanup_test_session(game_session)
		return

	_assert_eq(normalized_party_state.main_character_member_id, &"hero", "归一化后不应把显式死亡主角偷偷回填成其他成员。")
	_assert_eq(normalized_party_state.get_resolved_main_character_member_id(), &"hero", "归一化后主角解析结果应继续指向显式死亡主角。")
	_assert_true(not normalized_party_state.active_member_ids.has(&"hero"), "归一化后死亡成员不应继续留在 active roster。")
	_assert_true(normalized_party_state.active_member_ids.has(&"mage"), "归一化后存活成员仍应保留在 active roster。")

	_cleanup_test_session(game_session)


func _test_party_state_from_dict_requires_main_character_member_id() -> void:
	var invalid_party_state = PartyState.from_dict({
		"version": 3,
		"gold": 180,
		"leader_member_id": "hero",
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
	_assert_true(invalid_party_state == null, "缺少 main_character_member_id 的旧 PartyState shape 不再支持。")


func _test_party_state_from_dict_rejects_overlapping_quest_buckets() -> void:
	var active_quest := QuestState.new()
	active_quest.quest_id = &"contract_overlap"
	active_quest.mark_accepted(3)
	var claimable_quest := QuestState.new()
	claimable_quest.quest_id = &"contract_overlap"
	claimable_quest.mark_accepted(3)
	claimable_quest.mark_completed(7)
	var invalid_party_state = PartyState.from_dict({
		"version": 3,
		"gold": 180,
		"leader_member_id": "hero",
		"main_character_member_id": "hero",
		"fate_run_flags": {},
		"meta_flags": {},
		"active_member_ids": ["hero"],
		"reserve_member_ids": [],
		"member_states": {
			"hero": _build_party_member_state(&"hero", "Hero").to_dict(),
		},
		"pending_character_rewards": [],
		"active_quests": [active_quest.to_dict()],
		"claimable_quests": [claimable_quest.to_dict()],
		"completed_quest_ids": [],
		"warehouse_state": {"stacks": [], "equipment_instances": []},
	})
	_assert_true(invalid_party_state == null, "同一 quest 同时出现在多个 bucket 的坏 PartyState shape 应直接拒绝。")


func _test_decode_v5_payload_rejects_missing_main_character_member_id() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_true(create_error == OK, "缺主角字段的旧存档回归需要可创建的测试世界。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return

	var serializer = game_session._save_serializer
	_assert_true(serializer != null, "缺主角字段的旧存档回归需要已初始化的 SaveSerializer。")
	if serializer == null:
		_cleanup_test_session(game_session)
		return

	var payload: Dictionary = serializer.build_save_payload(
		game_session.get_active_save_id(),
		game_session.get_generation_config_path(),
		game_session.get_active_save_meta(),
		game_session.get_world_data(),
		game_session.get_player_coord(),
		game_session.get_player_faction_id(),
		game_session.get_party_state(),
		int(Time.get_unix_time_from_system())
	)
	var party_state_payload: Dictionary = (payload.get("party_state", {}) as Dictionary).duplicate(true)
	party_state_payload.erase("main_character_member_id")
	payload["party_state"] = party_state_payload

	var decode_result: Dictionary = serializer.decode_v5_payload(
		payload,
		game_session.get_generation_config_path(),
		game_session.get_generation_config(),
		game_session.get_active_save_meta()
	)
	_assert_eq(int(decode_result.get("error", OK)), ERR_INVALID_DATA, "缺少 main_character_member_id 的旧存档应直接判为坏数据。")

	_cleanup_test_session(game_session)


func _build_party_member_state(member_id: StringName, display_name: String, is_dead: bool = false) -> PartyMemberState:
	var member_state := PartyMemberState.new()
	member_state.member_id = member_id
	member_state.display_name = display_name
	member_state.progression.unit_id = member_id
	member_state.progression.display_name = display_name
	member_state.is_dead = is_dead
	return member_state


func _cleanup_test_session(game_session) -> void:
	if game_session == null:
		return
	game_session.clear_persisted_game()
	game_session.free()


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])


func _extract_first_template_binding(world_data: Dictionary) -> Dictionary:
	for settlement_variant in world_data.get("settlements", []):
		if settlement_variant is not Dictionary:
			continue
		var settlement: Dictionary = settlement_variant
		var facilities_variant: Variant = settlement.get("facilities", [])
		if facilities_variant is not Array:
			continue
		for facility_variant in facilities_variant:
			if facility_variant is not Dictionary:
				continue
			var facility: Dictionary = facility_variant
			var npcs_variant: Variant = facility.get("service_npcs", [])
			if npcs_variant is not Array:
				continue
			for npc_variant in npcs_variant:
				if npc_variant is not Dictionary:
					continue
				var npc: Dictionary = npc_variant
				var matched_service := _find_matching_service_entry(settlement.get("available_services", []), facility, npc)
				if matched_service.is_empty():
					continue
				return {
					"settlement_template_id": String(settlement.get("template_id", "")),
					"settlement_id": String(settlement.get("settlement_id", "")),
					"facility_template_id": String(facility.get("template_id", "")),
					"facility_id": String(facility.get("facility_id", "")),
					"npc_template_id": String(npc.get("template_id", "")),
					"npc_id": String(npc.get("npc_id", "")),
					"service_facility_id": String(matched_service.get("facility_id", "")),
					"service_facility_template_id": String(matched_service.get("facility_template_id", "")),
					"service_npc_id": String(matched_service.get("npc_id", "")),
					"service_npc_template_id": String(matched_service.get("npc_template_id", "")),
				}
	return {}


func _find_matching_service_entry(service_variants, facility: Dictionary, npc: Dictionary) -> Dictionary:
	if service_variants is not Array:
		return {}
	for service_variant in service_variants:
		if service_variant is not Dictionary:
			continue
		var service: Dictionary = service_variant
		if String(service.get("facility_id", "")) != String(facility.get("facility_id", "")):
			continue
		if String(service.get("npc_id", "")) != String(npc.get("npc_id", "")):
			continue
		return service.duplicate(true)
	return {}
