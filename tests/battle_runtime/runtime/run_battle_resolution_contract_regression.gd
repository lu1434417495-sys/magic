## 文件说明：该脚本属于战斗结算 contract 回归相关的测试脚本，集中覆盖 canonical 结果对象、战斗结束生成时机和 battle session handoff。
## 审查重点：重点核对 battle end 时是否生成 BattleResolutionResult，以及 session facade 是否稳定传递 canonical reward queue。
## 备注：该回归只验证 battle-side contract，不触碰旧 pending_mastery_rewards 兼容链。

extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const BattleEventBatch = preload("res://scripts/systems/battle/core/battle_event_batch.gd")
const BattleResolutionResult = preload("res://scripts/systems/battle/core/battle_resolution_result.gd")
const BattleRuntimeModule = preload("res://scripts/systems/battle/runtime/battle_runtime_module.gd")
const GameRuntimeBattleLootCommitService = preload("res://scripts/systems/game_runtime/game_runtime_battle_loot_commit_service.gd")
const GameRuntimeFacade = preload("res://scripts/systems/game_runtime/game_runtime_facade.gd")
const BattleSessionFacade = preload("res://scripts/systems/game_runtime/battle_session_facade.gd")
const CharacterManagementModule = preload("res://scripts/systems/progression/character_management_module.gd")
const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleTimelineState = preload("res://scripts/systems/battle/core/battle_timeline_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const PartyWarehouseService = preload("res://scripts/systems/inventory/party_warehouse_service.gd")
const PendingCharacterReward = preload("res://scripts/systems/progression/pending_character_reward.gd")
const PendingCharacterRewardEntry = preload("res://scripts/systems/progression/pending_character_reward_entry.gd")
const PartyState = preload("res://scripts/player/progression/party_state.gd")
const PartyMemberState = preload("res://scripts/player/progression/party_member_state.gd")
const WarehouseState = preload("res://scripts/player/warehouse/warehouse_state.gd")
const EquipmentInstanceState = preload("res://scripts/player/warehouse/equipment_instance_state.gd")
const ItemDef = preload("res://scripts/player/warehouse/item_def.gd")
const AchievementDef = preload("res://scripts/player/progression/achievement_def.gd")
const AchievementRewardDef = preload("res://scripts/player/progression/achievement_reward_def.gd")
const UnitSkillProgress = preload("res://scripts/player/progression/unit_skill_progress.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


class _FakeBattleSelection extends RefCounted:
	func clear_battle_skill_selection(_keep_overlay: bool = false) -> void:
		pass


class _FakeBattleRuntimeWithResult extends RefCounted:
	var state: BattleState = null
	var resolution_result = null
	var consume_result_called := false

	func get_state() -> BattleState:
		return state

	func get_battle_resolution_result():
		return resolution_result

	func consume_battle_resolution_result():
		consume_result_called = true
		var result = resolution_result
		resolution_result = null
		return result


class _FakeBattleRuntimeWithoutResult extends RefCounted:
	var state: BattleState = null

	func get_state() -> BattleState:
		return state

	func get_battle_resolution_result():
		return null

	func consume_battle_resolution_result():
		return null


class _FakeLootCommitGameSession extends RefCounted:
	var item_defs: Dictionary = {}
	var _next_equipment_instance_serial := 1
	var allocation_call_count := 0

	func get_item_defs() -> Dictionary:
		return item_defs

	func allocate_equipment_instance_id() -> StringName:
		allocation_call_count += 1
		var allocated_id := StringName("eq_contract_%06d" % _next_equipment_instance_serial)
		_next_equipment_instance_serial += 1
		return allocated_id


class _FakeLootCommitRuntime extends RefCounted:
	var _party_state = null
	var _party_warehouse_service = null
	var _game_session = null

	func _setup_party_warehouse_service(service, party_state, item_defs: Dictionary = {}) -> void:
		if service == null or not service.has_method("setup"):
			return
		service.setup(party_state, item_defs, Callable(_game_session, "allocate_equipment_instance_id"))

	func _get_item_display_name(item_id: StringName) -> String:
		var item_def = _game_session.get_item_defs().get(item_id) if _game_session != null else null
		if item_def != null and not item_def.display_name.is_empty():
			return item_def.display_name
		return String(item_id)


class _FakeRuntimeBridge extends RefCounted:
	var battle_selection = _FakeBattleSelection.new()
	var battle_runtime = null
	var finalize_should_succeed := true
	var finalization_calls: Array[Dictionary] = []
	var status_updates: Array[String] = []

	func get_battle_selection():
		return battle_selection

	func get_battle_runtime():
		return battle_runtime

	func is_battle_active() -> bool:
		return true

	func get_battle_state() -> BattleState:
		return battle_runtime.get_state() if battle_runtime != null and battle_runtime.has_method("get_state") else null

	func finalize_battle_resolution(battle_resolution_result) -> bool:
		finalization_calls.append({
			"battle_resolution_result": battle_resolution_result,
			"winner_faction_id": String(battle_resolution_result.winner_faction_id) if battle_resolution_result != null else "",
			"pending_character_rewards": battle_resolution_result.get_pending_character_rewards_copy() if battle_resolution_result != null else [],
			"quest_progress_events": battle_resolution_result.quest_progress_events.duplicate(true) if battle_resolution_result != null else [],
		})
		return finalize_should_succeed

	func build_command_ok(message: String = "", battle_refresh_mode: String = "") -> Dictionary:
		return {
			"ok": true,
			"message": message,
			"battle_refresh_mode": battle_refresh_mode,
		}

	func build_command_error(message: String) -> Dictionary:
		return {
			"ok": false,
			"message": message,
		}

	func update_status(_message: String) -> void:
		status_updates.append(_message)

	func get_active_modal_id() -> String:
		return ""

	func is_modal_window_open() -> bool:
		return false


class _FakeBattleLockGameSession extends RefCounted:
	var lock_enabled := true
	var release_call_count := 0
	var set_party_state_call_count := 0
	var set_world_data_call_count := 0
	var flush_call_count := 0
	var discard_call_count := 0

	func set_battle_save_lock(enabled: bool) -> void:
		lock_enabled = enabled
		if not enabled:
			release_call_count += 1

	func set_party_state(_party_state) -> int:
		set_party_state_call_count += 1
		return OK

	func set_world_data(_world_data: Dictionary) -> int:
		set_world_data_call_count += 1
		return OK

	func flush_game_state() -> int:
		flush_call_count += 1
		return OK

	func discard_pending_save() -> void:
		discard_call_count += 1

	func get_item_defs() -> Dictionary:
		return {}

	func log_event(_level: String, _domain: String, _event_id: String, _message: String, _context: Dictionary = {}) -> Dictionary:
		return {}


class _FakeFinalizationCharacterManagement extends RefCounted:
	var party_state: PartyState = null
	var enqueue_call_count := 0
	var quest_progress_call_count := 0

	func get_party_state():
		return party_state

	func enqueue_pending_character_rewards(_rewards: Array) -> void:
		enqueue_call_count += 1

	func apply_quest_progress_events(_events: Array, _world_step: int = 0) -> Dictionary:
		quest_progress_call_count += 1
		return {}


class _FakeFinalizationBattleRuntime extends RefCounted:
	var end_battle_call_count := 0
	var fate_call_count := 0

	func handle_fate_battle_resolution(_battle_state, _battle_resolution_result) -> Dictionary:
		fate_call_count += 1
		return {}

	func end_battle(_options: Dictionary = {}) -> void:
		end_battle_call_count += 1


class _FakeBattleWritebackService extends RefCounted:
	var ok := true
	var commit_call_count := 0
	var report_call_count := 0

	func setup(_runtime) -> void:
		pass

	func commit_battle_local_views_to_party_state(_battle_state, _party_state) -> Dictionary:
		commit_call_count += 1
		return {"ok": ok, "error_code": "" if ok else "writeback_conflict"}

	func report_invariant_failure(_writeback_result: Dictionary, _battle_summary: Dictionary, _winner_faction_id: String) -> void:
		report_call_count += 1


class _FakeBattleLootCommitService extends RefCounted:
	var ok := true
	var commit_call_count := 0

	func setup(_runtime) -> void:
		pass

	func commit_battle_loot_to_shared_warehouse(_battle_resolution_result) -> Dictionary:
		commit_call_count += 1
		return {
			"ok": ok,
			"error_code": "" if ok else "battle_loot_item_missing_def",
			"blocked_item_id": "missing_item" if not ok else "",
			"committed_item_count": 0,
			"overflow_entries": [],
			"overflow_entry_count": 0,
		}

	func build_battle_resolution_status_message(
		battle_name: String,
		winner_faction_id: String,
		loot_commit_result: Dictionary,
		persisted_ok: bool
	) -> String:
		return "%s %s %s" % [battle_name, winner_faction_id, str(persisted_ok)]

	func build_last_battle_loot_snapshot(
		_battle_name: String,
		_winner_faction_id: String,
		_battle_resolution_result,
		_loot_commit_result: Dictionary
	) -> Dictionary:
		return {}

	func clear_regular_battle_calamity_shard_flags() -> void:
		pass


class _FakeBattleGateway extends RefCounted:
	var achievement_event_calls: Array[Dictionary] = []
	var _next_equipment_instance_serial := 1
	var equipment_instance_allocation_call_count := 0

	func allocate_equipment_instance_id() -> StringName:
		equipment_instance_allocation_call_count += 1
		var allocated_id := StringName("eq_contract_%06d" % _next_equipment_instance_serial)
		_next_equipment_instance_serial += 1
		return allocated_id

	func build_pending_skill_mastery_reward(
		member_id: StringName,
		source_type: StringName,
		source_label: String,
		entry_variants: Array,
		summary_text: String = ""
	) -> PendingCharacterReward:
		var reward := PendingCharacterReward.new()
		reward.reward_id = StringName("%s_%s" % [String(member_id), String(source_type)])
		reward.member_id = member_id
		reward.member_name = String(member_id)
		reward.source_type = source_type
		reward.source_id = source_type
		reward.source_label = source_label
		reward.summary_text = summary_text
		for entry_variant in entry_variants:
			if entry_variant is not Dictionary:
				continue
			var entry_data: Dictionary = entry_variant
			var entry := PendingCharacterRewardEntry.new()
			entry.entry_type = &"skill_mastery"
			entry.target_id = ProgressionDataUtils.to_string_name(entry_data.get("target_id", ""))
			entry.target_label = String(entry_data.get("target_label", String(entry.target_id)))
			entry.amount = int(entry_data.get("amount", 0))
			entry.reason_text = String(entry_data.get("reason_text", ""))
			if not entry.is_empty():
				reward.entries.append(entry)
		return reward

	func record_achievement_event(
		member_id: StringName,
		event_type: StringName,
		amount: int = 1,
		subject_id: StringName = &"",
		meta: Dictionary = {}
	) -> Array[StringName]:
		achievement_event_calls.append({
			"member_id": String(member_id),
			"event_type": String(event_type),
			"amount": amount,
			"subject_id": String(subject_id),
			"meta": meta.duplicate(true),
		})
		return []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_battle_resolution_result_round_trip()
	_test_battle_resolution_equipment_instance_ids_round_trip_and_overflow()
	_test_battle_resolution_rejects_bad_top_level_schema()
	_test_battle_resolution_rejects_bad_drop_entry_schema()
	_test_battle_resolution_rejects_bad_equipment_drop_schema()
	_test_battle_resolution_rejects_bad_nested_array_entries()
	_test_battle_resolution_rejects_loot_alias_only_payloads()
	_test_battle_loot_commit_rejects_equipment_instance_data_alias()
	_test_battle_runtime_assigns_equipment_instance_ids_on_player_victory()
	_test_battle_runtime_builds_resolution_result_on_battle_end()
	_test_battle_runtime_draws_when_both_sides_are_cleared()
	_test_battle_runtime_battle_end_integration_uses_real_character_gateway()
	_test_battle_session_facade_prefers_canonical_resolution_result()
	_test_battle_session_facade_keeps_result_when_finalize_fails()
	_test_battle_session_command_propagates_finalize_failure()
	_test_battle_session_facade_requires_canonical_resolution_result()
	_test_game_runtime_finalize_writeback_failure_keeps_battle_lock()
	_test_game_runtime_finalize_loot_failure_keeps_battle_lock()
	if _failures.is_empty():
		print("Battle resolution contract regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle resolution contract regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_battle_resolution_result_round_trip() -> void:
	var reward := _build_canonical_reward(&"hero", &"battle_skill")

	var result := BattleResolutionResult.new()
	result.battle_id = &"battle_contract"
	result.seed = 77
	result.world_coord = Vector2i(4, 9)
	result.encounter_anchor_id = &"encounter_contract"
	result.terrain_profile_id = &"canyon"
	result.winner_faction_id = &"player"
	result.encounter_resolution = &"player_victory"
	result.set_loot_entries([_build_raw_loot_entry()])
	result.set_overflow_entries([_build_raw_overflow_entry()])
	result.pending_character_rewards = [reward]
	result.quest_progress_events = [{"quest_id": "quest_contract", "amount": 1}]
	result.world_mutations = [{"kind": "clear_anchor"}]
	result.party_resource_commit = {"gold_delta": 12}

	_assert_true(not result.is_empty(), "填充字段后，BattleResolutionResult 不应为空。")
	_assert_true(result.has_pending_character_rewards(), "填充奖励后，BattleResolutionResult 应报告存在待处理奖励。")
	_assert_eq(result.get_pending_character_rewards_copy().size(), 1, "奖励访问器应返回同一批奖励。")
	_assert_eq(result.loot_entries.size(), 1, "BattleResolutionResult 应归一化 loot_entries。")
	if result.loot_entries.size() > 0 and result.loot_entries[0] is Dictionary:
		_assert_canonical_loot_entry(result.loot_entries[0], "BattleResolutionResult.set_loot_entries()")

	var round_tripped: BattleResolutionResult = BattleResolutionResult.from_dict(result.to_dict())
	_assert_true(round_tripped != null, "BattleResolutionResult.to_dict()/from_dict() 应支持 round trip。")
	_assert_eq(String(round_tripped.battle_id), "battle_contract", "battle_id 应保持稳定。")
	_assert_eq(String(round_tripped.winner_faction_id), "player", "winner_faction_id 应保持稳定。")
	_assert_eq(String(round_tripped.encounter_resolution), "player_victory", "encounter_resolution 应保持稳定。")
	_assert_eq(round_tripped.loot_entries.size(), 1, "loot_entries 应保持稳定。")
	if round_tripped.loot_entries.size() > 0 and round_tripped.loot_entries[0] is Dictionary:
		_assert_canonical_loot_entry(round_tripped.loot_entries[0], "BattleResolutionResult round trip")
	_assert_eq(round_tripped.overflow_entries.size(), 1, "overflow_entries 应保持稳定。")
	if round_tripped.overflow_entries.size() > 0 and round_tripped.overflow_entries[0] is Dictionary:
		_assert_canonical_overflow_entry(round_tripped.overflow_entries[0], "BattleResolutionResult overflow round trip")
	_assert_eq(round_tripped.pending_character_rewards.size(), 1, "pending_character_rewards 应保持稳定。")
	_assert_true(
		round_tripped.pending_character_rewards[0] is PendingCharacterReward,
		"pending_character_rewards 的元素在 round trip 后应仍是正式角色奖励。"
	)


func _test_battle_resolution_equipment_instance_ids_round_trip_and_overflow() -> void:
	var result := BattleResolutionResult.new()
	result.battle_id = &"battle_equipment_instance_contract"
	result.seed = 77
	result.world_coord = Vector2i(4, 9)
	result.encounter_anchor_id = &"encounter_equipment_instance_contract"
	result.terrain_profile_id = &"canyon"
	result.winner_faction_id = &"player"
	result.encounter_resolution = &"player_victory"
	var loot_entry := _build_persisted_equipment_instance_loot_entry()
	var overflow_entry := _build_persisted_equipment_instance_loot_entry()
	overflow_entry["drop_entry_id"] = &"wolf_unit_overflow_bronze_sword"
	var overflow_equipment_payload := _build_persisted_equipment_instance_payload()
	overflow_equipment_payload["instance_id"] = "eq_contract_000002"
	overflow_entry["equipment_instance"] = overflow_equipment_payload
	result.set_loot_entries([loot_entry])
	result.set_overflow_entries([overflow_entry])

	_assert_equipment_entry_instance_id(result.loot_entries, "eq_contract_000001", "equipment loot 正规化")
	_assert_equipment_entry_instance_id(result.overflow_entries, "eq_contract_000002", "equipment overflow 正规化")

	var round_tripped: BattleResolutionResult = BattleResolutionResult.from_dict(result.to_dict())
	_assert_true(round_tripped != null, "装备掉落 instance_id 应支持 BattleResolutionResult to_dict/from_dict 往返。")
	if round_tripped == null:
		return
	_assert_equipment_entry_instance_id(round_tripped.loot_entries, "eq_contract_000001", "equipment loot round trip")
	_assert_equipment_entry_instance_id(round_tripped.overflow_entries, "eq_contract_000002", "equipment overflow round trip")

	result.set_loot_entries([_build_formal_equipment_instance_loot_entry()])
	_assert_eq(result.loot_entries.size(), 0, "equipment loot 缺少非空 instance_id 时不应被正规化。")
	result.set_overflow_entries([_build_formal_equipment_instance_loot_entry()])
	_assert_eq(result.overflow_entries.size(), 0, "equipment overflow 缺少非空 instance_id 时不应被正规化。")


func _test_battle_resolution_rejects_bad_top_level_schema() -> void:
	var payload := _build_strict_battle_resolution_payload()

	var missing_terrain := payload.duplicate(true)
	missing_terrain.erase("terrain_profile_id")
	_assert_true(
		BattleResolutionResult.from_dict(missing_terrain) == null,
		"缺少 terrain_profile_id 时，BattleResolutionResult.from_dict() 不应回退到 default。"
	)

	for array_field_name in [
		"loot_entries",
		"overflow_entries",
		"pending_character_rewards",
		"quest_progress_events",
		"world_mutations",
	]:
		var missing_array := payload.duplicate(true)
		missing_array.erase(array_field_name)
		_assert_true(
			BattleResolutionResult.from_dict(missing_array) == null,
			"缺少数组字段 %s 时，BattleResolutionResult.from_dict() 不应按空数组恢复。" % array_field_name
		)

		var wrong_array_type := payload.duplicate(true)
		wrong_array_type[array_field_name] = {}
		_assert_true(
			BattleResolutionResult.from_dict(wrong_array_type) == null,
			"数组字段 %s 类型错误时，BattleResolutionResult.from_dict() 应拒绝 payload。" % array_field_name
		)

	var wrong_world_coord := payload.duplicate(true)
	wrong_world_coord["world_coord"] = {"x": 4, "y": 9}
	_assert_true(
		BattleResolutionResult.from_dict(wrong_world_coord) == null,
		"world_coord 类型错误时，BattleResolutionResult.from_dict() 应拒绝 payload。"
	)

	var extra_top_level_field := payload.duplicate(true)
	extra_top_level_field["legacy_resolution"] = "player"
	_assert_true(
		BattleResolutionResult.from_dict(extra_top_level_field) == null,
		"包含额外顶层字段时，BattleResolutionResult.from_dict() 应拒绝 payload。"
	)

	var empty_battle_id := payload.duplicate(true)
	empty_battle_id["battle_id"] = ""
	_assert_true(
		BattleResolutionResult.from_dict(empty_battle_id) == null,
		"battle_id 为空时，BattleResolutionResult.from_dict() 应拒绝 payload。"
	)

	var empty_encounter_anchor_id := payload.duplicate(true)
	empty_encounter_anchor_id["encounter_anchor_id"] = ""
	_assert_true(
		BattleResolutionResult.from_dict(empty_encounter_anchor_id) == null,
		"encounter_anchor_id 为空时，BattleResolutionResult.from_dict() 应拒绝 payload。"
	)

	var empty_terrain := payload.duplicate(true)
	empty_terrain["terrain_profile_id"] = ""
	_assert_true(
		BattleResolutionResult.from_dict(empty_terrain) == null,
		"terrain_profile_id 为空时，BattleResolutionResult.from_dict() 应拒绝 payload。"
	)

	var empty_winner := payload.duplicate(true)
	empty_winner["winner_faction_id"] = ""
	_assert_true(
		BattleResolutionResult.from_dict(empty_winner) == null,
		"winner_faction_id 为空时，BattleResolutionResult.from_dict() 应拒绝 payload。"
	)

	var empty_resolution := payload.duplicate(true)
	empty_resolution["encounter_resolution"] = ""
	_assert_true(
		BattleResolutionResult.from_dict(empty_resolution) == null,
		"encounter_resolution 为空时，BattleResolutionResult.from_dict() 应拒绝 payload。"
	)

	var missing_party_resource_commit := payload.duplicate(true)
	missing_party_resource_commit.erase("party_resource_commit")
	_assert_true(
		BattleResolutionResult.from_dict(missing_party_resource_commit) == null,
		"缺少 party_resource_commit 时，BattleResolutionResult.from_dict() 不应按空字典恢复。"
	)

	var wrong_party_resource_commit := payload.duplicate(true)
	wrong_party_resource_commit["party_resource_commit"] = []
	_assert_true(
		BattleResolutionResult.from_dict(wrong_party_resource_commit) == null,
		"party_resource_commit 类型错误时，BattleResolutionResult.from_dict() 应拒绝 payload。"
	)


func _test_battle_resolution_rejects_bad_drop_entry_schema() -> void:
	var non_dictionary_loot := _build_strict_battle_resolution_payload()
	non_dictionary_loot["loot_entries"] = [_build_raw_loot_entry(), "bad_loot"]
	_assert_true(
		BattleResolutionResult.from_dict(non_dictionary_loot) == null,
		"loot_entries 含非 Dictionary 元素时，BattleResolutionResult.from_dict() 应拒绝整个 payload。"
	)

	var non_dictionary_overflow := _build_strict_battle_resolution_payload()
	non_dictionary_overflow["overflow_entries"] = [_build_raw_overflow_entry(), "bad_overflow"]
	_assert_true(
		BattleResolutionResult.from_dict(non_dictionary_overflow) == null,
		"overflow_entries 含非 Dictionary 元素时，BattleResolutionResult.from_dict() 应拒绝整个 payload。"
	)

	var missing_item_id := _build_strict_battle_resolution_payload()
	var missing_item_entry := _build_raw_loot_entry()
	missing_item_entry.erase("item_id")
	missing_item_id["loot_entries"] = [missing_item_entry]
	_assert_true(
		BattleResolutionResult.from_dict(missing_item_id) == null,
		"loot entry 缺少正式必需字段时，BattleResolutionResult.from_dict() 应拒绝整个 payload。"
	)

	var string_quantity := _build_strict_battle_resolution_payload()
	var string_quantity_entry := _build_raw_loot_entry()
	string_quantity_entry["quantity"] = "2"
	string_quantity["loot_entries"] = [string_quantity_entry]
	_assert_true(
		BattleResolutionResult.from_dict(string_quantity) == null,
		"loot entry quantity 为字符串数字时，BattleResolutionResult.from_dict() 应拒绝 payload。"
	)

	var item_entry_with_equipment := _build_strict_battle_resolution_payload()
	var item_entry := _build_raw_loot_entry()
	item_entry["equipment_instance"] = _build_persisted_equipment_instance_payload()
	item_entry_with_equipment["loot_entries"] = [item_entry]
	_assert_true(
		BattleResolutionResult.from_dict(item_entry_with_equipment) == null,
		"普通 item loot entry 携带 equipment_instance 时，BattleResolutionResult.from_dict() 应拒绝 payload。"
	)


func _test_battle_resolution_rejects_bad_equipment_drop_schema() -> void:
	var missing_payload := _build_strict_battle_resolution_payload()
	var missing_payload_entry := _build_persisted_equipment_instance_loot_entry()
	missing_payload_entry.erase("equipment_instance")
	missing_payload["loot_entries"] = [missing_payload_entry]
	_assert_true(
		BattleResolutionResult.from_dict(missing_payload) == null,
		"equipment_instance 掉落缺少 equipment_instance payload 时，BattleResolutionResult.from_dict() 应拒绝 payload。"
	)

	var bad_payload := _build_strict_battle_resolution_payload()
	var bad_payload_entry := _build_persisted_equipment_instance_loot_entry()
	bad_payload_entry["equipment_instance"] = {"item_id": "bronze_sword"}
	bad_payload["loot_entries"] = [bad_payload_entry]
	_assert_true(
		BattleResolutionResult.from_dict(bad_payload) == null,
		"equipment_instance payload 不是有效 EquipmentInstanceState 字典时，BattleResolutionResult.from_dict() 应拒绝 payload。"
	)

	var empty_instance_id_payload := _build_strict_battle_resolution_payload()
	var empty_instance_id_entry := _build_persisted_equipment_instance_loot_entry()
	var empty_instance_equipment_payload: Dictionary = empty_instance_id_entry["equipment_instance"]
	empty_instance_equipment_payload["instance_id"] = ""
	empty_instance_id_entry["equipment_instance"] = empty_instance_equipment_payload
	empty_instance_id_payload["loot_entries"] = [empty_instance_id_entry]
	_assert_true(
		BattleResolutionResult.from_dict(empty_instance_id_payload) == null,
		"equipment_instance 掉落 instance_id 为空时，BattleResolutionResult.from_dict() 应拒绝 payload。"
	)

	var empty_overflow_instance_id_payload := _build_strict_battle_resolution_payload()
	var empty_overflow_entry := _build_persisted_equipment_instance_loot_entry()
	var empty_overflow_equipment_payload: Dictionary = empty_overflow_entry["equipment_instance"]
	empty_overflow_equipment_payload["instance_id"] = ""
	empty_overflow_entry["equipment_instance"] = empty_overflow_equipment_payload
	empty_overflow_instance_id_payload["overflow_entries"] = [empty_overflow_entry]
	_assert_true(
		BattleResolutionResult.from_dict(empty_overflow_instance_id_payload) == null,
		"equipment_instance overflow entry instance_id 为空时，BattleResolutionResult.from_dict() 应拒绝 payload。"
	)

	var mismatch_payload := _build_strict_battle_resolution_payload()
	var mismatch_entry := _build_persisted_equipment_instance_loot_entry()
	var equipment_payload: Dictionary = mismatch_entry["equipment_instance"]
	equipment_payload["item_id"] = "iron_sword"
	mismatch_entry["equipment_instance"] = equipment_payload
	mismatch_payload["loot_entries"] = [mismatch_entry]
	_assert_true(
		BattleResolutionResult.from_dict(mismatch_payload) == null,
		"loot entry item_id 与 equipment_instance.item_id 不一致时，BattleResolutionResult.from_dict() 应拒绝 payload。"
	)

	var wrong_quantity_payload := _build_strict_battle_resolution_payload()
	var wrong_quantity_entry := _build_persisted_equipment_instance_loot_entry()
	wrong_quantity_entry["quantity"] = 2
	wrong_quantity_payload["loot_entries"] = [wrong_quantity_entry]
	_assert_true(
		BattleResolutionResult.from_dict(wrong_quantity_payload) == null,
		"equipment_instance 掉落 quantity 不是 1 时，BattleResolutionResult.from_dict() 应拒绝 payload。"
	)


func _test_battle_resolution_rejects_bad_nested_array_entries() -> void:
	var bad_reward_entry := _build_strict_battle_resolution_payload()
	bad_reward_entry["pending_character_rewards"] = [_build_canonical_reward(&"hero", &"battle_skill").to_dict(), "bad_reward"]
	_assert_true(
		BattleResolutionResult.from_dict(bad_reward_entry) == null,
		"pending_character_rewards 含非 Dictionary 元素时，BattleResolutionResult.from_dict() 应拒绝整个 payload。"
	)

	var invalid_reward := _build_strict_battle_resolution_payload()
	var reward_payload := _build_canonical_reward(&"hero", &"battle_skill").to_dict()
	reward_payload.erase("entries")
	invalid_reward["pending_character_rewards"] = [reward_payload]
	_assert_true(
		BattleResolutionResult.from_dict(invalid_reward) == null,
		"pending_character_rewards 含无效奖励字典时，BattleResolutionResult.from_dict() 应拒绝整个 payload。"
	)

	var scalar_quest_event := _build_strict_battle_resolution_payload()
	scalar_quest_event["quest_progress_events"] = [{"quest_id": "quest_contract"}, "bad_event"]
	_assert_true(
		BattleResolutionResult.from_dict(scalar_quest_event) == null,
		"quest_progress_events 含标量元素时，BattleResolutionResult.from_dict() 应拒绝 payload。"
	)

	var scalar_world_mutation := _build_strict_battle_resolution_payload()
	scalar_world_mutation["world_mutations"] = [{"kind": "clear_anchor"}, 12]
	_assert_true(
		BattleResolutionResult.from_dict(scalar_world_mutation) == null,
		"world_mutations 含标量元素时，BattleResolutionResult.from_dict() 应拒绝 payload。"
	)


func _test_battle_resolution_rejects_loot_alias_only_payloads() -> void:
	var result := BattleResolutionResult.new()
	result.set_loot_entries([_build_drop_id_alias_only_loot_entry()])
	_assert_eq(result.loot_entries.size(), 0, "只提供旧 drop_id alias 的 battle loot payload 不应归一化为正式掉落。")

	result.set_loot_entries([_build_missing_source_label_loot_entry()])
	_assert_eq(result.loot_entries.size(), 0, "缺少正式 drop_source_label 的 battle loot payload 不应从 drop_source_id 回退。")

	result.set_loot_entries([_build_equipment_instance_data_alias_loot_entry()])
	_assert_eq(result.loot_entries.size(), 0, "只提供旧 equipment_instance_data alias 的装备掉落不应归一化为正式掉落。")

	result.set_loot_entries([_build_formal_equipment_instance_loot_entry()])
	_assert_eq(result.loot_entries.size(), 0, "equipment_instance 掉落缺少正式 instance_id 时不应归一化为正式掉落。")

	result.set_loot_entries([_build_persisted_equipment_instance_loot_entry()])
	_assert_eq(result.loot_entries.size(), 1, "带正式 instance_id 的 equipment_instance 掉落 payload 应继续通过归一化。")
	if result.loot_entries.size() > 0 and result.loot_entries[0] is Dictionary:
		var loot_entry := result.loot_entries[0] as Dictionary
		_assert_true(loot_entry.has("equipment_instance"), "正式装备掉落应保留 equipment_instance 字段。")
		_assert_true(not loot_entry.has("equipment_instance_data"), "正式装备掉落不应暴露 equipment_instance_data alias。")
		var equipment_payload: Dictionary = loot_entry.get("equipment_instance", {}) if loot_entry.get("equipment_instance", {}) is Dictionary else {}
		_assert_eq(String(equipment_payload.get("item_id", "")), "bronze_sword", "正式装备掉落应保留 equipment_instance.item_id。")
		_assert_eq(String(equipment_payload.get("instance_id", "")), "eq_contract_000001", "正式装备掉落应保留 equipment_instance.instance_id。")


func _test_battle_loot_commit_rejects_equipment_instance_data_alias() -> void:
	var runtime = _build_loot_commit_runtime()
	var service := GameRuntimeBattleLootCommitService.new()
	service.setup(runtime)
	var formal_result := BattleResolutionResult.new()
	formal_result.winner_faction_id = &"player"
	formal_result.set_loot_entries([_build_persisted_equipment_instance_loot_entry()])

	var commit_result: Dictionary = service.commit_battle_loot_to_shared_warehouse(formal_result)
	_assert_true(bool(commit_result.get("ok", false)), "正式 equipment_instance battle loot 应能提交到共享仓库。")
	_assert_eq(int(commit_result.get("committed_item_count", -1)), 1, "正式 equipment_instance battle loot 应提交 1 件装备。")
	_assert_eq(runtime._party_state.warehouse_state.equipment_instances.size(), 1, "正式 equipment_instance battle loot 应写入仓库装备实例。")
	if runtime._party_state.warehouse_state.equipment_instances.size() > 0:
		var committed_instance = runtime._party_state.warehouse_state.equipment_instances[0]
		_assert_eq(String(committed_instance.item_id), "bronze_sword", "提交后的装备实例应保留正式 item_id。")
		_assert_eq(String(committed_instance.instance_id), "eq_contract_000001", "提交后的装备实例应保留战斗结算已分配的正式 instance_id。")
	_assert_eq(runtime._game_session.allocation_call_count, 0, "提交 battle loot equipment_instance 时不应重新分配 instance_id。")

	runtime = _build_loot_commit_runtime()
	service = GameRuntimeBattleLootCommitService.new()
	service.setup(runtime)
	var alias_direct_commit_result: Dictionary = service._commit_equipment_instance_loot_entry(_build_equipment_instance_data_alias_loot_entry())
	_assert_true(not bool(alias_direct_commit_result.get("ok", true)), "提交服务不应直接接受 equipment_instance_data alias-only payload。")
	_assert_eq(
		String(alias_direct_commit_result.get("error_code", "")),
		"battle_loot_equipment_instance_missing_payload",
		"equipment_instance_data alias-only payload 应被报告为缺少正式 equipment_instance。"
	)
	_assert_eq(runtime._party_state.warehouse_state.equipment_instances.size(), 0, "被拒绝的 equipment_instance_data alias-only payload 不应写入仓库。")

	var alias_public_result := BattleResolutionResult.new()
	alias_public_result.winner_faction_id = &"player"
	alias_public_result.loot_entries = [_build_equipment_instance_data_alias_loot_entry()]
	var public_commit_result: Dictionary = service.commit_battle_loot_to_shared_warehouse(alias_public_result)
	_assert_true(bool(public_commit_result.get("ok", false)), "public commit 遇到 alias-only loot 时应按空掉落完成结算。")
	_assert_eq(int(public_commit_result.get("committed_item_count", -1)), 0, "public commit 不应提交 alias-only equipment_instance_data payload。")
	_assert_eq(alias_public_result.loot_entries.size(), 0, "public commit 应把 alias-only loot payload 归一化为空。")


func _test_battle_runtime_assigns_equipment_instance_ids_on_player_victory() -> void:
	var runtime := BattleRuntimeModule.new()
	var gateway := _FakeBattleGateway.new()
	runtime.setup(gateway, {}, {}, {}, null, null, {}, null, Callable(gateway, "allocate_equipment_instance_id"))
	runtime._state = _build_battle_state_for_end_test()
	runtime._battle_rating_stats = _build_battle_rating_stats()
	runtime._active_loot_entries = [_build_formal_equipment_instance_loot_entry()]

	var batch := BattleEventBatch.new()
	_assert_true(runtime._check_battle_end(batch), "战斗胜利结束时应生成正式结算结果。")
	var result: BattleResolutionResult = runtime.get_battle_resolution_result()
	_assert_true(result != null, "战斗胜利结束后应缓存 BattleResolutionResult。")
	if result == null:
		return
	_assert_equipment_entry_instance_id(result.loot_entries, "eq_contract_000001", "battle victory equipment loot")
	_assert_eq(
		gateway.equipment_instance_allocation_call_count,
		1,
		"battle victory canonical result 应在战斗胜利时通过正式接口分配一次 equipment instance_id。"
	)


func _test_battle_runtime_builds_resolution_result_on_battle_end() -> void:
	var runtime := BattleRuntimeModule.new()
	var gateway := _FakeBattleGateway.new()
	runtime.setup(gateway, {}, {}, {}, null)
	runtime._state = _build_battle_state_for_end_test()
	runtime._battle_rating_stats = _build_battle_rating_stats()
	runtime._active_loot_entries = [_build_raw_loot_entry()]

	var batch := BattleEventBatch.new()
	_assert_true(runtime._check_battle_end(batch), "_check_battle_end() 应在一方清场后结束战斗。")
	var result: BattleResolutionResult = runtime.get_battle_resolution_result()
	_assert_true(result != null, "战斗结束后应缓存 BattleResolutionResult。")
	_assert_eq(String(result.winner_faction_id), "player", "战斗胜利方应写入结算结果。")
	_assert_eq(String(result.encounter_resolution), "player_victory", "战斗结算类型应与胜利方一致。")
	_assert_eq(result.pending_character_rewards.size(), 1, "战斗结算结果应包含已构建的正式奖励。")
	_assert_true(
		result.pending_character_rewards[0] is PendingCharacterReward,
		"战斗结算结果中的奖励应为正式角色奖励对象。"
	)
	_assert_eq(result.loot_entries.size(), 1, "战斗结算结果应包含 canonical 掉落条目。")
	if result.loot_entries.size() > 0 and result.loot_entries[0] is Dictionary:
		_assert_canonical_loot_entry(result.loot_entries[0], "BattleRuntimeModule._build_battle_resolution_result()")
	_assert_true(
		_has_achievement_event_call(gateway.achievement_event_calls, "hero", "battle_won"),
		"battle end 结算应向 character gateway 记录 battle_won 成就事件。"
	)
	_assert_true(runtime.consume_battle_resolution_result() == result, "consume_battle_resolution_result() 应返回已构建的结果。")
	_assert_true(runtime.consume_battle_resolution_result() == null, "consume_battle_resolution_result() 第二次调用后应清空缓存。")


func _test_battle_runtime_draws_when_both_sides_are_cleared() -> void:
	var runtime := BattleRuntimeModule.new()
	var gateway := _FakeBattleGateway.new()
	runtime.setup(gateway, {}, {}, {}, null)
	runtime._state = _build_battle_state_for_end_test(false, false)
	runtime._battle_rating_stats = _build_battle_rating_stats()
	runtime._active_loot_entries = [_build_raw_loot_entry()]

	var batch := BattleEventBatch.new()
	_assert_true(runtime._check_battle_end(batch), "_check_battle_end() 应在双方同时清场后结束战斗。")
	var result: BattleResolutionResult = runtime.get_battle_resolution_result()
	_assert_true(result != null, "同归于尽后仍应生成 canonical BattleResolutionResult。")
	if result == null:
		return
	_assert_eq(String(runtime._state.winner_faction_id), "draw", "双方同时清场时胜利方应为 draw。")
	_assert_eq(String(result.winner_faction_id), "draw", "draw 应写入战斗结算结果。")
	_assert_eq(String(result.encounter_resolution), "draw", "draw 应写入 encounter_resolution。")
	_assert_eq(result.loot_entries.size(), 0, "draw 不应发放胜利掉落。")
	_assert_true(
		not _has_achievement_event_call(gateway.achievement_event_calls, "hero", "battle_won"),
		"draw 不应记录 battle_won 成就事件。"
	)


func _test_battle_runtime_battle_end_integration_uses_real_character_gateway() -> void:
	var runtime := BattleRuntimeModule.new()
	var character_gateway := _build_real_character_gateway_for_battle_end_test()
	runtime.setup(character_gateway, _build_skill_defs_for_battle_end_test(), {}, {}, null)
	runtime._state = _build_battle_state_for_end_test()
	runtime._battle_rating_stats = _build_battle_rating_stats()
	runtime._active_loot_entries = [_build_raw_loot_entry()]

	var batch := BattleEventBatch.new()
	_assert_true(runtime._check_battle_end(batch), "真实 character gateway 下，_check_battle_end() 应继续正常生成结算。")

	var result: BattleResolutionResult = runtime.get_battle_resolution_result()
	_assert_true(result != null, "真实 character gateway 下应继续生成 BattleResolutionResult。")
	_assert_eq(result.pending_character_rewards.size(), 1, "真实 character gateway 下 battle rating 奖励应仍写入结算结果。")

	var hero_state: PartyMemberState = character_gateway.get_party_state().get_member_state(&"hero")
	_assert_true(hero_state != null, "真实 character gateway 下应能读取 hero 成员状态。")
	if hero_state == null:
		return
	var achievement_progress = hero_state.progression.get_achievement_progress_state(&"battle_won_first")
	_assert_true(
		achievement_progress != null and achievement_progress.is_unlocked,
		"battle end 结算应通过真实 CharacterManagementModule 解锁 battle_won 成就。"
	)
	_assert_eq(
		character_gateway.get_party_state().pending_character_rewards.size(),
		1,
		"battle_won 成就奖励应进入 PartyState.pending_character_rewards。"
	)
	if character_gateway.get_party_state().pending_character_rewards.size() > 0:
		var reward: PendingCharacterReward = character_gateway.get_party_state().pending_character_rewards[0] as PendingCharacterReward
		_assert_true(reward != null and reward.source_id == &"battle_won_first", "成就奖励应携带稳定的 achievement source_id。")


func _test_battle_session_facade_prefers_canonical_resolution_result() -> void:
	var runtime_bridge := _FakeRuntimeBridge.new()
	var battle_runtime := _FakeBattleRuntimeWithResult.new()
	battle_runtime.state = _build_ended_battle_state()
	var expected_result := _build_resolution_result_with_reward(_build_canonical_reward(&"hero", &"battle_skill"))
	battle_runtime.resolution_result = expected_result
	runtime_bridge.battle_runtime = battle_runtime

	var facade := BattleSessionFacade.new()
	facade.setup(runtime_bridge)
	facade.resolve_active_battle()

	_assert_eq(runtime_bridge.finalization_calls.size(), 1, "resolve_active_battle() 应触发一次战后回写。")
	var call: Dictionary = runtime_bridge.finalization_calls[0]
	_assert_true(call.get("battle_resolution_result") is BattleResolutionResult, "resolve_active_battle() 应把 canonical result 整包传给运行时。")
	_assert_true(call.get("battle_resolution_result") == expected_result, "传给运行时的 canonical result 应保持原对象。")
	_assert_eq(String(call.get("winner_faction_id", "")), "player", "resolve_active_battle() 应使用 canonical result 的胜利方。")
	_assert_eq((call.get("pending_character_rewards", []) as Array).size(), 1, "resolve_active_battle() 应把 canonical result 的奖励传给运行时。")
	_assert_true(
		(call.get("pending_character_rewards", []) as Array)[0] is PendingCharacterReward,
		"resolve_active_battle() 应传递正式角色奖励对象。"
	)
	_assert_eq((call.get("quest_progress_events", []) as Array).size(), 1, "resolve_active_battle() 应把 canonical result 的 quest_progress_events 一并传给运行时。")
	_assert_true(battle_runtime.consume_result_called, "resolve_active_battle() 应在成功回写后消费 canonical battle result。")


func _test_battle_session_facade_keeps_result_when_finalize_fails() -> void:
	var runtime_bridge := _FakeRuntimeBridge.new()
	runtime_bridge.finalize_should_succeed = false
	var battle_runtime := _FakeBattleRuntimeWithResult.new()
	battle_runtime.state = _build_ended_battle_state()
	var expected_result := _build_resolution_result_with_reward(_build_canonical_reward(&"hero", &"battle_skill"))
	battle_runtime.resolution_result = expected_result
	runtime_bridge.battle_runtime = battle_runtime

	var facade := BattleSessionFacade.new()
	facade.setup(runtime_bridge)
	facade.resolve_active_battle()

	_assert_eq(runtime_bridge.finalization_calls.size(), 1, "finalize 失败前仍应尝试一次战后回写。")
	_assert_true(not battle_runtime.consume_result_called, "finalize 失败时不应消费 canonical battle result。")
	_assert_true(battle_runtime.resolution_result == expected_result, "finalize 失败时应保留原始 canonical result 供重试。")


func _test_battle_session_command_propagates_finalize_failure() -> void:
	var runtime_bridge := _FakeRuntimeBridge.new()
	runtime_bridge.finalize_should_succeed = false
	var battle_runtime := _FakeBattleRuntimeWithResult.new()
	battle_runtime.state = _build_ended_battle_state()
	var expected_result := _build_resolution_result_with_reward(_build_canonical_reward(&"hero", &"battle_skill"))
	battle_runtime.resolution_result = expected_result
	runtime_bridge.battle_runtime = battle_runtime

	var facade := BattleSessionFacade.new()
	facade.setup(runtime_bridge)
	var command_result := facade.command_battle_wait_or_resolve()

	_assert_true(not bool(command_result.get("ok", true)), "战后 finalize 失败时，battle.wait_or_resolve 应返回 ok=false。")
	_assert_true(not battle_runtime.consume_result_called, "命令级 finalize 失败时不应消费 canonical battle result。")
	_assert_true(battle_runtime.resolution_result == expected_result, "命令级 finalize 失败时应保留 canonical battle result 供重试。")


func _test_battle_session_facade_requires_canonical_resolution_result() -> void:
	var runtime_bridge := _FakeRuntimeBridge.new()
	var battle_runtime := _FakeBattleRuntimeWithoutResult.new()
	battle_runtime.state = _build_ended_battle_state()
	runtime_bridge.battle_runtime = battle_runtime

	var facade := BattleSessionFacade.new()
	facade.setup(runtime_bridge)
	facade.resolve_active_battle()

	_assert_eq(runtime_bridge.finalization_calls.size(), 0, "缺少 canonical result 时不应继续战后回写。")
	_assert_true(
		not runtime_bridge.status_updates.is_empty() and runtime_bridge.status_updates[-1].contains("缺少正式结算结果"),
		"缺少 canonical result 时应显式报告错误状态。"
	)


func _test_game_runtime_finalize_writeback_failure_keeps_battle_lock() -> void:
	var runtime := _build_runtime_for_finalize_failure_test()
	var game_session: _FakeBattleLockGameSession = runtime._game_session
	var battle_runtime: _FakeFinalizationBattleRuntime = runtime._battle_runtime
	var writeback_service: _FakeBattleWritebackService = runtime._battle_writeback_service
	writeback_service.ok = false

	var result := _build_resolution_result_with_reward(_build_canonical_reward(&"hero", &"battle_skill"))
	var finalized := bool(runtime.finalize_battle_resolution(result))

	_assert_true(not finalized, "battle-local writeback 失败时 finalize_battle_resolution() 应返回 false。")
	_assert_true(game_session.lock_enabled, "battle-local writeback 失败时不应释放 battle save lock。")
	_assert_eq(game_session.release_call_count, 0, "battle-local writeback 失败时不应调用 set_battle_save_lock(false)。")
	_assert_eq(battle_runtime.end_battle_call_count, 0, "battle-local writeback 失败时不应 end_battle。")
	_assert_true(runtime._battle_state != null, "battle-local writeback 失败时不应清理 battle context。")
	_assert_eq(game_session.flush_call_count, 0, "battle-local writeback 失败时不应 flush save。")


func _test_game_runtime_finalize_loot_failure_keeps_battle_lock() -> void:
	var runtime := _build_runtime_for_finalize_failure_test()
	var game_session: _FakeBattleLockGameSession = runtime._game_session
	var battle_runtime: _FakeFinalizationBattleRuntime = runtime._battle_runtime
	var loot_service: _FakeBattleLootCommitService = runtime._battle_loot_commit_service
	loot_service.ok = false

	var result := _build_resolution_result_with_reward(_build_canonical_reward(&"hero", &"battle_skill"))
	var finalized := bool(runtime.finalize_battle_resolution(result))

	_assert_true(not finalized, "loot commit hard failure 时 finalize_battle_resolution() 应返回 false。")
	_assert_true(game_session.lock_enabled, "loot commit hard failure 时不应释放 battle save lock。")
	_assert_eq(game_session.release_call_count, 0, "loot commit hard failure 时不应调用 set_battle_save_lock(false)。")
	_assert_eq(battle_runtime.end_battle_call_count, 0, "loot commit hard failure 时不应 end_battle。")
	_assert_true(runtime._battle_state != null, "loot commit hard failure 时不应清理 battle context。")
	_assert_eq(game_session.set_party_state_call_count, 0, "loot commit hard failure 时不应写回 party_state。")
	_assert_eq(game_session.set_world_data_call_count, 0, "loot commit hard failure 时不应移除 encounter 并写回 world_data。")
	_assert_eq(game_session.flush_call_count, 0, "loot commit hard failure 时不应 flush save。")


func _build_canonical_reward(member_id: StringName, skill_id: StringName) -> PendingCharacterReward:
	var reward := PendingCharacterReward.new()
	reward.reward_id = StringName("%s_reward" % String(member_id))
	reward.member_id = member_id
	reward.member_name = String(member_id)
	reward.source_type = &"battle_rating"
	reward.source_id = &"battle_rating"
	reward.source_label = "战斗结算"
	reward.summary_text = "战斗评分结算。"
	var entry := PendingCharacterRewardEntry.new()
	entry.entry_type = &"skill_mastery"
	entry.target_id = skill_id
	entry.target_label = String(skill_id)
	entry.amount = 4
	entry.reason_text = "战斗评分 4 · 渐入佳境"
	reward.entries = [entry]
	return reward


func _build_resolution_result_with_reward(reward: PendingCharacterReward) -> BattleResolutionResult:
	var result := BattleResolutionResult.new()
	result.battle_id = &"battle_session"
	result.seed = 99
	result.world_coord = Vector2i(6, 12)
	result.encounter_anchor_id = &"encounter_session"
	result.terrain_profile_id = &"default"
	result.winner_faction_id = &"player"
	result.encounter_resolution = &"player_victory"
	result.pending_character_rewards = [reward]
	result.quest_progress_events = [{"quest_id": "battle_contract", "objective_id": "defeat_enemy", "progress_delta": 1}]
	return result


func _build_runtime_for_finalize_failure_test() -> GameRuntimeFacade:
	var runtime := GameRuntimeFacade.new()
	var party_state := PartyState.new()
	var hero := PartyMemberState.new()
	hero.member_id = &"hero"
	hero.display_name = "Hero"
	hero.progression.unit_id = hero.member_id
	hero.progression.display_name = hero.display_name
	party_state.set_member_state(hero)
	party_state.active_member_ids = [&"hero"]
	party_state.leader_member_id = &"hero"
	party_state.main_character_member_id = &"hero"

	var character_management := _FakeFinalizationCharacterManagement.new()
	character_management.party_state = party_state
	runtime._game_session = _FakeBattleLockGameSession.new()
	runtime._character_management = character_management
	runtime._battle_runtime = _FakeFinalizationBattleRuntime.new()
	runtime._battle_writeback_service = _FakeBattleWritebackService.new()
	runtime._battle_loot_commit_service = _FakeBattleLootCommitService.new()
	runtime._party_state = party_state
	runtime._battle_state = _build_ended_battle_state()
	runtime._active_battle_encounter_id = &"encounter_session"
	runtime._active_battle_encounter_name = "Session Battle"
	return runtime


func _build_strict_battle_resolution_payload() -> Dictionary:
	var result := BattleResolutionResult.new()
	result.battle_id = &"battle_schema_contract"
	result.seed = 88
	result.world_coord = Vector2i(5, 10)
	result.encounter_anchor_id = &"encounter_schema_contract"
	result.terrain_profile_id = &"canyon"
	result.winner_faction_id = &"player"
	result.encounter_resolution = &"player_victory"
	result.set_loot_entries([_build_raw_loot_entry()])
	result.set_overflow_entries([_build_raw_overflow_entry()])
	result.pending_character_rewards = [_build_canonical_reward(&"hero", &"battle_skill")]
	result.quest_progress_events = [{"quest_id": "quest_contract", "amount": 1}]
	result.world_mutations = [{"kind": "clear_anchor"}]
	result.party_resource_commit = {"gold_delta": 3}
	return result.to_dict()


func _build_raw_loot_entry() -> Dictionary:
	return {
		"drop_type": &"item",
		"drop_source_kind": &"encounter_roster",
		"drop_source_id": &"wolf_den",
		"drop_source_label": "荒狼巢穴",
		"drop_entry_id": &"wolf_den_hide_bundle",
		"item_id": &"beast_hide",
		"quantity": 2,
		"debug_only_flag": true,
	}


func _build_raw_overflow_entry() -> Dictionary:
	return {
		"drop_type": &"item",
		"drop_source_kind": &"encounter_roster",
		"drop_source_id": &"wolf_den",
		"drop_source_label": "荒狼巢穴",
		"drop_entry_id": &"wolf_den_hide_bundle",
		"item_id": &"beast_hide",
		"quantity": 1,
		"debug_only_flag": true,
	}


func _build_drop_id_alias_only_loot_entry() -> Dictionary:
	var loot_entry := _build_raw_loot_entry()
	loot_entry.erase("drop_entry_id")
	loot_entry["drop_id"] = &"wolf_den_hide_bundle"
	return loot_entry


func _build_missing_source_label_loot_entry() -> Dictionary:
	var loot_entry := _build_raw_loot_entry()
	loot_entry.erase("drop_source_label")
	return loot_entry


func _build_formal_equipment_instance_loot_entry() -> Dictionary:
	return {
		"drop_type": &"equipment_instance",
		"drop_source_kind": &"enemy_unit",
		"drop_source_id": &"wolf_unit",
		"drop_source_label": "荒狼",
		"drop_entry_id": &"wolf_unit_bronze_sword",
		"item_id": &"bronze_sword",
		"quantity": 1,
		"equipment_instance": _build_transient_equipment_instance_payload(),
	}


func _build_persisted_equipment_instance_loot_entry() -> Dictionary:
	return {
		"drop_type": &"equipment_instance",
		"drop_source_kind": &"enemy_unit",
		"drop_source_id": &"wolf_unit",
		"drop_source_label": "荒狼",
		"drop_entry_id": &"wolf_unit_bronze_sword",
		"item_id": &"bronze_sword",
		"quantity": 1,
		"equipment_instance": _build_persisted_equipment_instance_payload(),
	}


func _build_equipment_instance_data_alias_loot_entry() -> Dictionary:
	var loot_entry := _build_formal_equipment_instance_loot_entry()
	loot_entry["equipment_instance_data"] = loot_entry.get("equipment_instance", {}).duplicate(true)
	loot_entry.erase("equipment_instance")
	return loot_entry


func _build_transient_equipment_instance_payload() -> Dictionary:
	return {
		"instance_id": "",
		"item_id": "bronze_sword",
		"rarity": EquipmentInstanceState.RarityTier.RARE,
		"current_durability": 120,
	}


func _build_persisted_equipment_instance_payload() -> Dictionary:
	var payload := _build_transient_equipment_instance_payload()
	payload["instance_id"] = "eq_contract_000001"
	return payload


func _assert_equipment_entry_instance_id(entries: Array, expected_id: String, context: String) -> void:
	_assert_eq(entries.size(), 1, "%s 应保留 1 条 equipment_instance entry。" % context)
	if entries.is_empty() or entries[0] is not Dictionary:
		return
	var entry := entries[0] as Dictionary
	_assert_true(entry.has("equipment_instance"), "%s 应保留 equipment_instance payload。" % context)
	var equipment_payload: Dictionary = entry.get("equipment_instance", {}) if entry.get("equipment_instance", {}) is Dictionary else {}
	_assert_eq(String(equipment_payload.get("instance_id", "")), expected_id, "%s 应保留非空 instance_id。" % context)


func _build_loot_commit_runtime():
	var game_session := _FakeLootCommitGameSession.new()
	game_session.item_defs = _build_loot_commit_item_defs()
	var runtime := _FakeLootCommitRuntime.new()
	runtime._party_state = PartyState.new()
	runtime._party_state.warehouse_state = WarehouseState.new()
	var hero := PartyMemberState.new()
	hero.member_id = &"hero"
	hero.display_name = "Hero"
	hero.progression.unit_id = hero.member_id
	hero.progression.display_name = hero.display_name
	hero.progression.unit_base_attributes.custom_stats[&"storage_space"] = 4
	runtime._party_state.set_member_state(hero)
	runtime._party_state.active_member_ids = [&"hero"]
	runtime._party_state.leader_member_id = &"hero"
	runtime._party_warehouse_service = PartyWarehouseService.new()
	runtime._game_session = game_session
	runtime._setup_party_warehouse_service(runtime._party_warehouse_service, runtime._party_state, game_session.get_item_defs())
	return runtime


func _build_loot_commit_item_defs() -> Dictionary:
	var hide := ItemDef.new()
	hide.item_id = &"beast_hide"
	hide.display_name = "Beast Hide"
	hide.is_stackable = true
	hide.max_stack = 30

	var sword := ItemDef.new()
	sword.item_id = &"bronze_sword"
	sword.display_name = "Bronze Sword"
	sword.is_stackable = false
	sword.item_category = ItemDef.ITEM_CATEGORY_EQUIPMENT
	sword.equipment_slot_ids = ["main_hand"]
	sword.equipment_type_id = ItemDef.EQUIPMENT_TYPE_WEAPON
	return {
		hide.item_id: hide,
		sword.item_id: sword,
	}


func _assert_canonical_loot_entry(loot_entry: Dictionary, message_scope: String) -> void:
	_assert_true(not loot_entry.has("drop_id"), "%s 应只暴露 canonical drop_entry_id 字段。" % message_scope)
	_assert_true(not loot_entry.has("debug_only_flag"), "%s 不应泄露原始调试字段。" % message_scope)
	_assert_eq(String(loot_entry.get("drop_type", "")), "item", "%s 应保留稳定 drop_type。" % message_scope)
	_assert_eq(String(loot_entry.get("drop_source_kind", "")), "encounter_roster", "%s 应保留稳定来源类型。" % message_scope)
	_assert_eq(String(loot_entry.get("drop_source_id", "")), "wolf_den", "%s 应保留稳定掉落来源标识。" % message_scope)
	_assert_eq(String(loot_entry.get("drop_entry_id", "")), "wolf_den_hide_bundle", "%s 应保留稳定掉落 entry 标识。" % message_scope)
	_assert_eq(String(loot_entry.get("item_id", "")), "beast_hide", "%s 应保留稳定物品标识。" % message_scope)
	_assert_eq(int(loot_entry.get("quantity", 0)), 2, "%s 应保留稳定数量。" % message_scope)


func _assert_canonical_overflow_entry(loot_entry: Dictionary, message_scope: String) -> void:
	_assert_true(not loot_entry.has("debug_only_flag"), "%s 不应泄露原始调试字段。" % message_scope)
	_assert_eq(String(loot_entry.get("drop_type", "")), "item", "%s 应保留稳定 drop_type。" % message_scope)
	_assert_eq(String(loot_entry.get("drop_source_kind", "")), "encounter_roster", "%s 应保留稳定来源类型。" % message_scope)
	_assert_eq(String(loot_entry.get("drop_source_id", "")), "wolf_den", "%s 应保留稳定掉落来源标识。" % message_scope)
	_assert_eq(String(loot_entry.get("drop_entry_id", "")), "wolf_den_hide_bundle", "%s 应保留稳定掉落 entry 标识。" % message_scope)
	_assert_eq(String(loot_entry.get("item_id", "")), "beast_hide", "%s 应保留稳定物品标识。" % message_scope)
	_assert_eq(int(loot_entry.get("quantity", 0)), 1, "%s 应保留稳定溢出数量。" % message_scope)


func _build_battle_state_for_end_test(ally_alive: bool = true, enemy_alive: bool = false) -> BattleState:
	var state := BattleState.new()
	state.battle_id = &"battle_end_contract"
	state.phase = &"timeline_running"
	state.timeline = BattleTimelineState.new()
	state.ally_unit_ids = [&"hero_unit"]
	state.enemy_unit_ids = [&"enemy_unit"]
	var ally_unit := _build_unit(&"hero_unit", &"hero", ally_alive)
	var enemy_unit := _build_unit(&"enemy_unit", &"enemy", enemy_alive)
	state.units[ally_unit.unit_id] = ally_unit
	state.units[enemy_unit.unit_id] = enemy_unit
	return state


func _build_battle_rating_stats() -> Dictionary:
	return {
		&"hero": {
			"member_id": &"hero",
			"member_name": "Hero",
			"cast_counts": {
				&"battle_skill": 1,
			},
			"successful_skill_count": 1,
			"total_damage_done": 1,
			"total_healing_done": 0,
			"kill_count": 0,
		},
	}


func _build_ended_battle_state() -> BattleState:
	var state := BattleState.new()
	state.battle_id = &"battle_session_end"
	state.phase = &"battle_ended"
	state.winner_faction_id = &"player"
	state.timeline = BattleTimelineState.new()
	return state


func _build_unit(unit_id: StringName, member_id: StringName, is_alive: bool) -> BattleUnitState:
	var unit := BattleUnitState.new()
	unit.unit_id = unit_id
	unit.source_member_id = member_id
	unit.display_name = String(unit_id)
	unit.faction_id = &"player" if String(member_id) == "hero" else &"hostile"
	unit.control_mode = &"manual"
	unit.is_alive = is_alive
	return unit


func _build_real_character_gateway_for_battle_end_test() -> CharacterManagementModule:
	var party_state := PartyState.new()
	var hero := PartyMemberState.new()
	hero.member_id = &"hero"
	hero.display_name = "Hero"
	hero.current_hp = 24
	hero.current_mp = 6
	hero.progression.unit_id = hero.member_id
	hero.progression.display_name = hero.display_name
	var skill_progress := UnitSkillProgress.new()
	skill_progress.skill_id = &"battle_skill"
	skill_progress.is_learned = true
	skill_progress.skill_level = 1
	hero.progression.set_skill_progress(skill_progress)
	party_state.set_member_state(hero)
	party_state.active_member_ids = [&"hero"]
	party_state.leader_member_id = &"hero"

	var gateway := CharacterManagementModule.new()
	gateway.setup(
		party_state,
		_build_skill_defs_for_battle_end_test(),
		{},
		_build_achievement_defs_for_battle_end_test()
	)
	return gateway


func _build_skill_defs_for_battle_end_test() -> Dictionary:
	var skill_def := SkillDef.new()
	skill_def.skill_id = &"battle_skill"
	skill_def.display_name = "战斗技能"
	skill_def.max_level = 5
	skill_def.mastery_curve = PackedInt32Array([0, 10, 20, 30, 40, 50])
	return {
		skill_def.skill_id: skill_def,
	}


func _build_achievement_defs_for_battle_end_test() -> Dictionary:
	var reward_def := AchievementRewardDef.new()
	reward_def.reward_type = AchievementRewardDef.TYPE_ATTRIBUTE_DELTA
	reward_def.target_id = &"hp_max"
	reward_def.target_label = "生命上限"
	reward_def.amount = 1
	reward_def.reason_text = "首胜奖励"

	var achievement_def := AchievementDef.new()
	achievement_def.achievement_id = &"battle_won_first"
	achievement_def.display_name = "首战告捷"
	achievement_def.description = "完成第一次战斗胜利。"
	achievement_def.event_type = &"battle_won"
	achievement_def.threshold = 1
	achievement_def.rewards = [reward_def]
	return {
		achievement_def.achievement_id: achievement_def,
	}


func _has_achievement_event_call(calls: Array[Dictionary], member_id: String, event_type: String) -> bool:
	for call in calls:
		if String(call.get("member_id", "")) != member_id:
			continue
		if String(call.get("event_type", "")) != event_type:
			continue
		return true
	return false


func _assert_true(condition: bool, message: String) -> void:
	if condition:
		return
	_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual == expected:
		return
	_test.fail("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
