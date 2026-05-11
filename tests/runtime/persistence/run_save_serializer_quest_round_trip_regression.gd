extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/persistence/game_session.gd")
const AchievementProgressState = preload("res://scripts/player/progression/achievement_progress_state.gd")
const PendingCharacterReward = preload("res://scripts/systems/progression/pending_character_reward.gd")
const PendingCharacterRewardEntry = preload("res://scripts/systems/progression/pending_character_reward_entry.gd")
const PendingProfessionChoice = preload("res://scripts/player/progression/pending_profession_choice.gd")
const PartyMemberState = preload("res://scripts/player/progression/party_member_state.gd")
const PartyState = preload("res://scripts/player/progression/party_state.gd")
const ProfessionPromotionRecord = preload("res://scripts/player/progression/profession_promotion_record.gd")
const QuestState = preload("res://scripts/player/progression/quest_state.gd")
const UnitBaseAttributes = preload("res://scripts/player/progression/unit_base_attributes.gd")
const UnitProfessionProgress = preload("res://scripts/player/progression/unit_profession_progress.gd")
const UnitProgress = preload("res://scripts/player/progression/unit_progress.gd")
const UnitReputationState = preload("res://scripts/player/progression/unit_reputation_state.gd")

const TEST_WORLD_CONFIG := "res://data/configs/world_map/test_world_map_config.tres"

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_save_serializer_round_trip_preserves_party_quest_schema()
	_test_world_map_template_bindings_round_trip()
	_test_extract_save_meta_rejects_missing_slot_fields()
	_test_normalize_party_state_keeps_main_character_active()
	_test_normalize_party_state_preserves_explicit_dead_main_character()
	_test_party_state_from_dict_requires_main_character_member_id()
	_test_party_state_from_dict_requires_claimable_quests()
	_test_party_state_from_dict_requires_roster_header_fields()
	_test_party_state_from_dict_requires_member_schema_fields()
	_test_runtime_dtos_reject_extra_fields()
	_test_party_state_from_dict_rejects_bad_quest_state_schema()
	_test_party_state_from_dict_rejects_bad_completed_quest_ids()
	_test_party_state_from_dict_rejects_overlapping_quest_buckets()
	_test_decode_payload_rejects_v5_version()
	_test_decode_payload_rejects_missing_main_character_member_id()
	_test_decode_payload_rejects_missing_claimable_quests()
	_test_decode_payload_rejects_missing_roster_header_fields()
	_test_decode_payload_rejects_missing_member_schema_fields()

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
	var decode_result: Dictionary = serializer.decode_payload(
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
	var decode_result: Dictionary = serializer.decode_payload(
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


func _test_extract_save_meta_rejects_missing_slot_fields() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_true(create_error == OK, "Save meta 严格校验回归需要可创建的测试世界。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return

	var serializer = game_session._save_serializer
	_assert_true(serializer != null, "Save meta 严格校验回归需要已初始化的 SaveSerializer。")
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

	var rejected_meta: Dictionary = serializer.extract_save_meta_from_payload(payload)
	_assert_true(rejected_meta.is_empty(), "缺失 display_name/world_size/timestamps 的 save_slot_meta 应直接拒绝。")
	var decode_result: Dictionary = serializer.decode_payload(
		payload,
		game_session.get_generation_config_path(),
		game_session.get_generation_config(),
		game_session.get_active_save_meta()
	)
	_assert_eq(int(decode_result.get("error", OK)), ERR_INVALID_DATA, "缺失完整 save_slot_meta 的 payload 应直接判为坏数据。")

	var missing_generation_path_payload: Dictionary = serializer.build_save_payload(
		game_session.get_active_save_id(),
		game_session.get_generation_config_path(),
		game_session.get_active_save_meta(),
		game_session.get_world_data(),
		game_session.get_player_coord(),
		game_session.get_player_faction_id(),
		game_session.get_party_state(),
		int(Time.get_unix_time_from_system())
	)
	missing_generation_path_payload.erase("generation_config_path")
	_assert_true(
		serializer.extract_save_meta_from_payload(missing_generation_path_payload).is_empty(),
		"缺失 generation_config_path 的 payload 不应再从调用方参数恢复 meta。"
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


func _test_party_state_from_dict_requires_claimable_quests() -> void:
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
		"active_quests": [],
		"completed_quest_ids": [],
		"warehouse_state": {"stacks": [], "equipment_instances": []},
	})
	_assert_true(invalid_party_state == null, "缺少 claimable_quests 的 PartyState shape 应直接拒绝。")


func _test_party_state_from_dict_requires_roster_header_fields() -> void:
	for field_name in ["version", "gold", "leader_member_id", "active_member_ids", "reserve_member_ids"]:
		var party_state_payload := _build_minimal_party_state_payload()
		party_state_payload.erase(field_name)
		var invalid_party_state = PartyState.from_dict(party_state_payload)
		_assert_true(invalid_party_state == null, "缺少 %s 的 PartyState shape 应直接拒绝。" % field_name)

	for field_name in ["active_member_ids", "reserve_member_ids"]:
		var party_state_payload := _build_minimal_party_state_payload()
		party_state_payload[field_name] = "hero"
		var invalid_party_state = PartyState.from_dict(party_state_payload)
		_assert_true(invalid_party_state == null, "%s 不是数组的 PartyState shape 应直接拒绝。" % field_name)

	for field_case in [
		{"field": "leader_member_id", "value": ""},
		{"field": "leader_member_id", "value": "ghost"},
		{"field": "leader_member_id", "value": 123},
		{"field": "main_character_member_id", "value": "ghost"},
		{"field": "main_character_member_id", "value": 123},
		{"field": "version", "value": "3"},
		{"field": "version", "value": 2},
		{"field": "gold", "value": "180"},
		{"field": "gold", "value": -1},
		{"field": "active_member_ids", "value": [""]},
		{"field": "active_member_ids", "value": ["hero", "hero"]},
		{"field": "active_member_ids", "value": ["ghost"]},
		{"field": "reserve_member_ids", "value": [""]},
		{"field": "reserve_member_ids", "value": ["hero", "hero"]},
		{"field": "reserve_member_ids", "value": ["ghost"]},
	]:
		var party_state_payload := _build_minimal_party_state_payload()
		party_state_payload[String(field_case.get("field", ""))] = field_case.get("value")
		var invalid_party_state = PartyState.from_dict(party_state_payload)
		_assert_true(
			invalid_party_state == null,
			"%s 类型错误、取值非法、重复或引用未知成员的 PartyState shape 应直接拒绝。" % String(field_case.get("field", ""))
		)

	var overlapping_roster_payload := _build_minimal_party_state_payload()
	overlapping_roster_payload["reserve_member_ids"] = ["hero"]
	_assert_true(
		PartyState.from_dict(overlapping_roster_payload) == null,
		"同一成员同时出现在 active/reserve roster 时应直接拒绝。"
	)


func _test_party_state_from_dict_requires_member_schema_fields() -> void:
	for field_name in [
		"member_id",
		"display_name",
		"faction_id",
		"portrait_id",
		"control_mode",
		"current_hp",
		"current_mp",
		"is_dead",
		"race_id",
		"subrace_id",
		"age_years",
		"birth_at_world_step",
		"age_profile_id",
		"natural_age_stage_id",
		"effective_age_stage_id",
		"effective_age_stage_source_type",
		"effective_age_stage_source_id",
		"body_size",
		"body_size_category",
		"versatility_pick",
		"active_stage_advancement_modifier_ids",
		"bloodline_id",
		"bloodline_stage_id",
		"ascension_id",
		"ascension_stage_id",
		"ascension_started_at_world_step",
		"original_race_id_before_ascension",
		"biological_age_years",
		"astral_memory_years",
	]:
		var missing_field_payload := _build_party_payload_with_member_field_removed(field_name)
		_assert_true(
			PartyState.from_dict(missing_field_payload) == null,
			"缺少成员字段 %s 的 PartyState shape 应直接拒绝。" % field_name
		)

	for field_case in [
		{"field": "member_id", "value": ""},
		{"field": "member_id", "value": 123},
		{"field": "display_name", "value": ""},
		{"field": "display_name", "value": 123},
		{"field": "faction_id", "value": ""},
		{"field": "faction_id", "value": 123},
		{"field": "portrait_id", "value": 123},
		{"field": "control_mode", "value": ""},
		{"field": "control_mode", "value": 123},
		{"field": "current_hp", "value": "18"},
		{"field": "current_hp", "value": -1},
		{"field": "current_mp", "value": "6"},
		{"field": "current_mp", "value": -1},
		{"field": "is_dead", "value": 0},
		{"field": "race_id", "value": ""},
		{"field": "race_id", "value": 123},
		{"field": "subrace_id", "value": ""},
		{"field": "subrace_id", "value": 123},
		{"field": "age_years", "value": "24"},
		{"field": "age_years", "value": -1},
		{"field": "birth_at_world_step", "value": "0"},
		{"field": "birth_at_world_step", "value": -1},
		{"field": "age_profile_id", "value": ""},
		{"field": "age_profile_id", "value": 123},
		{"field": "natural_age_stage_id", "value": ""},
		{"field": "natural_age_stage_id", "value": 123},
		{"field": "effective_age_stage_id", "value": ""},
		{"field": "effective_age_stage_id", "value": 123},
		{"field": "effective_age_stage_source_type", "value": 123},
		{"field": "effective_age_stage_source_id", "value": 123},
		{"field": "body_size", "value": "1"},
		{"field": "body_size", "value": 0},
		{"field": "body_size_category", "value": ""},
		{"field": "body_size_category", "value": 123},
		{"field": "versatility_pick", "value": 123},
		{"field": "active_stage_advancement_modifier_ids", "value": ""},
		{"field": "active_stage_advancement_modifier_ids", "value": [""]},
		{"field": "active_stage_advancement_modifier_ids", "value": [123]},
		{"field": "active_stage_advancement_modifier_ids", "value": ["blessing", "blessing"]},
		{"field": "bloodline_id", "value": 123},
		{"field": "bloodline_stage_id", "value": 123},
		{"field": "ascension_id", "value": 123},
		{"field": "ascension_stage_id", "value": 123},
		{"field": "ascension_started_at_world_step", "value": "0"},
		{"field": "ascension_started_at_world_step", "value": -2},
		{"field": "original_race_id_before_ascension", "value": 123},
		{"field": "biological_age_years", "value": "24"},
		{"field": "biological_age_years", "value": -1},
		{"field": "astral_memory_years", "value": "0"},
		{"field": "astral_memory_years", "value": -1},
	]:
		var invalid_field_payload := _build_party_payload_with_member_field_value(
			String(field_case.get("field", "")),
			field_case.get("value")
		)
		_assert_true(
			PartyState.from_dict(invalid_field_payload) == null,
			"成员字段 %s 非法的 PartyState shape 应直接拒绝。" % String(field_case.get("field", ""))
		)

	var mismatched_key_payload := _build_party_payload_with_member_field_value("member_id", "mage")
	_assert_true(
		PartyState.from_dict(mismatched_key_payload) == null,
		"member_states key 与成员 member_id 不一致的 PartyState shape 应直接拒绝。"
	)

	for field_name in ["unit_id", "display_name"]:
		var missing_progression_payload := _build_party_payload_with_progression_field_removed(field_name)
		_assert_true(
			PartyState.from_dict(missing_progression_payload) == null,
			"缺少 progression.%s 的 PartyState shape 应直接拒绝。" % field_name
		)

	for field_case in [
		{"field": "unit_id", "value": ""},
		{"field": "unit_id", "value": "mage"},
		{"field": "display_name", "value": ""},
	]:
		var invalid_progression_payload := _build_party_payload_with_progression_field_value(
			String(field_case.get("field", "")),
			field_case.get("value")
		)
		_assert_true(
			PartyState.from_dict(invalid_progression_payload) == null,
		"progression.%s 非法的 PartyState shape 应直接拒绝。" % String(field_case.get("field", ""))
		)

	var extra_member_field_payload := _build_party_payload_with_member_field_value("legacy_identity_payload", "human")
	_assert_true(
		PartyState.from_dict(extra_member_field_payload) == null,
		"包含未知成员字段的 PartyState shape 应直接拒绝。"
	)


func _test_runtime_dtos_reject_extra_fields() -> void:
	var party_payload := _build_minimal_party_state_payload()
	party_payload["legacy_party_cache"] = true
	_assert_true(
		PartyState.from_dict(party_payload) == null,
		"PartyState 顶层额外字段应直接拒绝。"
	)

	var member := _build_party_member_state(&"hero", "Hero")
	var progression_payload: Dictionary = member.progression.to_dict()
	progression_payload["legacy_progression_cache"] = true
	_assert_true(
		UnitProgress.from_dict(progression_payload) == null,
		"UnitProgress 顶层额外字段应直接拒绝。"
	)

	var attributes_payload := UnitBaseAttributes.new().to_dict()
	attributes_payload["legacy_attribute_cache"] = true
	_assert_true(
		UnitBaseAttributes.from_dict(attributes_payload) == null,
		"UnitBaseAttributes 额外字段应直接拒绝。"
	)

	var reputation_payload := UnitReputationState.new().to_dict()
	reputation_payload["legacy_reputation_cache"] = true
	_assert_true(
		UnitReputationState.from_dict(reputation_payload) == null,
		"UnitReputationState 额外字段应直接拒绝。"
	)

	var profession_progress := UnitProfessionProgress.new()
	profession_progress.profession_id = &"fighter"
	var profession_payload := profession_progress.to_dict()
	profession_payload["legacy_profession_cache"] = true
	_assert_true(
		UnitProfessionProgress.from_dict(profession_payload) == null,
		"UnitProfessionProgress 额外字段应直接拒绝。"
	)

	var achievement_progress := AchievementProgressState.new()
	achievement_progress.achievement_id = &"schema_probe"
	var achievement_payload := achievement_progress.to_dict()
	achievement_payload["legacy_achievement_cache"] = true
	_assert_true(
		AchievementProgressState.from_dict(achievement_payload) == null,
		"AchievementProgressState 额外字段应直接拒绝。"
	)

	var pending_choice := PendingProfessionChoice.new()
	var choice_payload := pending_choice.to_dict()
	choice_payload["legacy_choice_cache"] = true
	_assert_true(
		PendingProfessionChoice.from_dict(choice_payload) == null,
		"PendingProfessionChoice 额外字段应直接拒绝。"
	)

	var promotion_record := ProfessionPromotionRecord.new()
	var record_payload := promotion_record.to_dict()
	record_payload["legacy_promotion_cache"] = true
	_assert_true(
		ProfessionPromotionRecord.from_dict(record_payload) == null,
		"ProfessionPromotionRecord 额外字段应直接拒绝。"
	)

	var reward_entry := PendingCharacterRewardEntry.new()
	reward_entry.entry_type = PendingCharacterRewardEntry.SKILL_MASTERY_ENTRY_TYPE
	reward_entry.target_id = &"schema_probe_skill"
	reward_entry.amount = 1
	var reward_entry_payload := reward_entry.to_dict()
	reward_entry_payload["legacy_reward_entry_cache"] = true
	_assert_true(
		PendingCharacterRewardEntry.from_dict(reward_entry_payload) == null,
		"PendingCharacterRewardEntry 额外字段应直接拒绝。"
	)

	var reward := PendingCharacterReward.new()
	reward.reward_id = &"schema_probe_reward"
	reward.member_id = &"hero"
	reward.member_name = "Hero"
	reward.source_type = &"test"
	reward.source_id = &"schema_probe"
	reward.entries = [reward_entry]
	var reward_payload := reward.to_dict()
	reward_payload["legacy_reward_cache"] = true
	_assert_true(
		PendingCharacterReward.from_dict(reward_payload) == null,
		"PendingCharacterReward 额外字段应直接拒绝。"
	)


func _test_party_state_from_dict_rejects_bad_quest_state_schema() -> void:
	for field_name in [
		"quest_id",
		"status_id",
		"objective_progress",
		"accepted_at_world_step",
		"completed_at_world_step",
		"reward_claimed_at_world_step",
		"last_progress_context",
	]:
		var active_payload := _build_minimal_party_state_payload()
		var active_quest_payload := _build_active_quest_payload()
		active_quest_payload.erase(field_name)
		active_payload["active_quests"] = [active_quest_payload]
		_assert_true(
			PartyState.from_dict(active_payload) == null,
			"active_quests 内 QuestState.%s 缺失的 PartyState shape 应直接拒绝。" % field_name
		)

		var claimable_payload := _build_minimal_party_state_payload()
		var claimable_quest_payload := _build_claimable_quest_payload()
		claimable_quest_payload.erase(field_name)
		claimable_payload["claimable_quests"] = [claimable_quest_payload]
		_assert_true(
			PartyState.from_dict(claimable_payload) == null,
			"claimable_quests 内 QuestState.%s 缺失的 PartyState shape 应直接拒绝。" % field_name
		)

	for field_case in [
		{"field": "status_id", "value": "legacy_unknown"},
		{"field": "objective_progress", "value": []},
		{"field": "last_progress_context", "value": []},
	]:
		var payload := _build_minimal_party_state_payload()
		var quest_payload := _build_active_quest_payload()
		quest_payload[String(field_case.get("field", ""))] = field_case.get("value")
		payload["active_quests"] = [quest_payload]
		_assert_true(
			PartyState.from_dict(payload) == null,
			"active_quests 内 QuestState.%s 非法的 PartyState shape 应直接拒绝。" % String(field_case.get("field", ""))
		)

	var wrong_active_status_payload := _build_minimal_party_state_payload()
	wrong_active_status_payload["active_quests"] = [_build_claimable_quest_payload()]
	_assert_true(
		PartyState.from_dict(wrong_active_status_payload) == null,
		"active_quests 内部 status 不是 active 时应直接拒绝。"
	)

	var wrong_claimable_status_payload := _build_minimal_party_state_payload()
	wrong_claimable_status_payload["claimable_quests"] = [_build_active_quest_payload()]
	_assert_true(
		PartyState.from_dict(wrong_claimable_status_payload) == null,
		"claimable_quests 内部 status 不是 completed 时应直接拒绝。"
	)

	var duplicate_active_payload := _build_minimal_party_state_payload()
	duplicate_active_payload["active_quests"] = [
		_build_active_quest_payload(),
		_build_active_quest_payload(),
	]
	_assert_true(
		PartyState.from_dict(duplicate_active_payload) == null,
		"active_quests 内重复 quest_id 时应直接拒绝。"
	)


func _test_party_state_from_dict_rejects_bad_completed_quest_ids() -> void:
	for completed_quest_ids in [
		[""],
		["intro_contract", "intro_contract"],
	]:
		var payload := _build_minimal_party_state_payload()
		payload["completed_quest_ids"] = completed_quest_ids
		_assert_true(
			PartyState.from_dict(payload) == null,
			"completed_quest_ids 内空 id 或重复 id 应直接拒绝。"
		)


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


func _test_decode_payload_rejects_v5_version() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_true(create_error == OK, "V5 拒绝回归需要可创建的测试世界。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return

	var serializer = game_session._save_serializer
	_assert_true(serializer != null, "V5 拒绝回归需要已初始化的 SaveSerializer。")
	if serializer == null:
		_cleanup_test_session(game_session)
		return

	var payload: Dictionary = _build_save_payload_for_session(game_session, serializer)
	payload["version"] = 5

	var decode_result: Dictionary = serializer.decode_payload(
		payload,
		game_session.get_generation_config_path(),
		game_session.get_generation_config(),
		game_session.get_active_save_meta()
	)
	_assert_eq(int(decode_result.get("error", OK)), ERR_INVALID_DATA, "V5 payload 应被当前 target decoder 直接拒绝。")

	payload = _build_save_payload_for_session(game_session, serializer)
	payload["version"] = 6
	decode_result = serializer.decode_payload(
		payload,
		game_session.get_generation_config_path(),
		game_session.get_generation_config(),
		game_session.get_active_save_meta()
	)
	_assert_eq(int(decode_result.get("error", OK)), ERR_INVALID_DATA, "V6 payload 应被 V7 target decoder 直接拒绝。")

	_cleanup_test_session(game_session)


func _test_decode_payload_rejects_missing_main_character_member_id() -> void:
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

	var decode_result: Dictionary = serializer.decode_payload(
		payload,
		game_session.get_generation_config_path(),
		game_session.get_generation_config(),
		game_session.get_active_save_meta()
	)
	_assert_eq(int(decode_result.get("error", OK)), ERR_INVALID_DATA, "缺少 main_character_member_id 的旧存档应直接判为坏数据。")

	_cleanup_test_session(game_session)


func _test_decode_payload_rejects_missing_claimable_quests() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_true(create_error == OK, "缺 claimable_quests 字段的存档回归需要可创建的测试世界。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return

	var serializer = game_session._save_serializer
	_assert_true(serializer != null, "缺 claimable_quests 字段的存档回归需要已初始化的 SaveSerializer。")
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
	party_state_payload.erase("claimable_quests")
	payload["party_state"] = party_state_payload

	var decode_result: Dictionary = serializer.decode_payload(
		payload,
		game_session.get_generation_config_path(),
		game_session.get_generation_config(),
		game_session.get_active_save_meta()
	)
	_assert_eq(int(decode_result.get("error", OK)), ERR_INVALID_DATA, "缺少 claimable_quests 的存档应直接判为坏数据。")

	_cleanup_test_session(game_session)


func _test_decode_payload_rejects_missing_roster_header_fields() -> void:
	for field_name in ["version", "gold", "leader_member_id", "active_member_ids", "reserve_member_ids"]:
		var game_session = GAME_SESSION_SCRIPT.new()
		var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
		_assert_true(create_error == OK, "缺 %s 字段的存档回归需要可创建的测试世界。" % field_name)
		if create_error != OK:
			_cleanup_test_session(game_session)
			continue

		var serializer = game_session._save_serializer
		_assert_true(serializer != null, "缺 %s 字段的存档回归需要已初始化的 SaveSerializer。" % field_name)
		if serializer == null:
			_cleanup_test_session(game_session)
			continue

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
		party_state_payload.erase(field_name)
		payload["party_state"] = party_state_payload

		var decode_result: Dictionary = serializer.decode_payload(
			payload,
			game_session.get_generation_config_path(),
			game_session.get_generation_config(),
			game_session.get_active_save_meta()
		)
		_assert_eq(
			int(decode_result.get("error", OK)),
			ERR_INVALID_DATA,
			"缺少 %s 的存档应直接判为坏数据。" % field_name
		)

		_cleanup_test_session(game_session)


func _test_decode_payload_rejects_missing_member_schema_fields() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_true(create_error == OK, "缺成员 schema 字段的存档回归需要可创建的测试世界。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return

	var serializer = game_session._save_serializer
	_assert_true(serializer != null, "缺成员 schema 字段的存档回归需要已初始化的 SaveSerializer。")
	if serializer == null:
		_cleanup_test_session(game_session)
		return

	for field_case in [
		{"scope": "member", "field": "member_id"},
		{"scope": "member", "field": "control_mode"},
		{"scope": "progression", "field": "unit_id"},
	]:
		var payload := _build_save_payload_for_session(game_session, serializer)
		_erase_main_member_payload_field(
			payload,
			game_session,
			String(field_case.get("scope", "")),
			String(field_case.get("field", ""))
		)

		var decode_result: Dictionary = serializer.decode_payload(
			payload,
			game_session.get_generation_config_path(),
			game_session.get_generation_config(),
			game_session.get_active_save_meta()
		)
		_assert_eq(
			int(decode_result.get("error", OK)),
			ERR_INVALID_DATA,
			"缺少 %s.%s 的存档应直接判为坏数据。" % [String(field_case.get("scope", "")), String(field_case.get("field", ""))]
		)

	_cleanup_test_session(game_session)


func _build_save_payload_for_session(game_session, serializer) -> Dictionary:
	return serializer.build_save_payload(
		game_session.get_active_save_id(),
		game_session.get_generation_config_path(),
		game_session.get_active_save_meta(),
		game_session.get_world_data(),
		game_session.get_player_coord(),
		game_session.get_player_faction_id(),
		game_session.get_party_state(),
		int(Time.get_unix_time_from_system())
	)


func _build_minimal_party_state_payload() -> Dictionary:
	return {
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
		"active_quests": [],
		"claimable_quests": [],
		"completed_quest_ids": [],
		"warehouse_state": {"stacks": [], "equipment_instances": []},
	}


func _build_active_quest_payload() -> Dictionary:
	var quest_state := QuestState.new()
	quest_state.quest_id = &"contract_schema_strict"
	quest_state.mark_accepted(4)
	quest_state.record_objective_progress(
		&"report_back",
		1,
		2,
		{"settlement_id": "spring_village_01"}
	)
	return quest_state.to_dict()


func _build_claimable_quest_payload() -> Dictionary:
	var quest_state := QuestState.new()
	quest_state.quest_id = &"contract_schema_strict"
	quest_state.mark_accepted(4)
	quest_state.record_objective_progress(
		&"report_back",
		2,
		2,
		{"settlement_id": "spring_village_01"}
	)
	quest_state.mark_completed(7)
	return quest_state.to_dict()


func _build_party_payload_with_member_field_removed(field_name: String) -> Dictionary:
	var payload := _build_minimal_party_state_payload()
	var member_states: Dictionary = (payload.get("member_states", {}) as Dictionary).duplicate(true)
	var member_payload: Dictionary = (member_states.get("hero", {}) as Dictionary).duplicate(true)
	member_payload.erase(field_name)
	member_states["hero"] = member_payload
	payload["member_states"] = member_states
	return payload


func _build_party_payload_with_member_field_value(field_name: String, value) -> Dictionary:
	var payload := _build_minimal_party_state_payload()
	var member_states: Dictionary = (payload.get("member_states", {}) as Dictionary).duplicate(true)
	var member_payload: Dictionary = (member_states.get("hero", {}) as Dictionary).duplicate(true)
	member_payload[field_name] = value
	member_states["hero"] = member_payload
	payload["member_states"] = member_states
	return payload


func _build_party_payload_with_progression_field_removed(field_name: String) -> Dictionary:
	var payload := _build_minimal_party_state_payload()
	var member_states: Dictionary = (payload.get("member_states", {}) as Dictionary).duplicate(true)
	var member_payload: Dictionary = (member_states.get("hero", {}) as Dictionary).duplicate(true)
	var progression_payload: Dictionary = (member_payload.get("progression", {}) as Dictionary).duplicate(true)
	progression_payload.erase(field_name)
	member_payload["progression"] = progression_payload
	member_states["hero"] = member_payload
	payload["member_states"] = member_states
	return payload


func _build_party_payload_with_progression_field_value(field_name: String, value) -> Dictionary:
	var payload := _build_minimal_party_state_payload()
	var member_states: Dictionary = (payload.get("member_states", {}) as Dictionary).duplicate(true)
	var member_payload: Dictionary = (member_states.get("hero", {}) as Dictionary).duplicate(true)
	var progression_payload: Dictionary = (member_payload.get("progression", {}) as Dictionary).duplicate(true)
	progression_payload[field_name] = value
	member_payload["progression"] = progression_payload
	member_states["hero"] = member_payload
	payload["member_states"] = member_states
	return payload


func _erase_main_member_payload_field(payload: Dictionary, game_session, scope: String, field_name: String) -> void:
	var party_state_payload: Dictionary = (payload.get("party_state", {}) as Dictionary).duplicate(true)
	var member_states: Dictionary = (party_state_payload.get("member_states", {}) as Dictionary).duplicate(true)
	var member_id := String(game_session.get_party_state().main_character_member_id)
	var member_payload: Dictionary = (member_states.get(member_id, {}) as Dictionary).duplicate(true)
	if scope == "progression":
		var progression_payload: Dictionary = (member_payload.get("progression", {}) as Dictionary).duplicate(true)
		progression_payload.erase(field_name)
		member_payload["progression"] = progression_payload
	else:
		member_payload.erase(field_name)
	member_states[member_id] = member_payload
	party_state_payload["member_states"] = member_states
	payload["party_state"] = party_state_payload


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
		_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])


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
