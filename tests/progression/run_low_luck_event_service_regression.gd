extends SceneTree

const BATTLE_FATE_EVENT_BUS_SCRIPT = preload("res://scripts/systems/battle/fate/battle_fate_event_bus.gd")
const BATTLE_RESOLUTION_RESULT_SCRIPT = preload("res://scripts/systems/battle/core/battle_resolution_result.gd")
const BATTLE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_state.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const CharacterManagementModule = preload("res://scripts/systems/progression/character_management_module.gd")
const LowLuckEventService = preload("res://scripts/systems/battle/fate/low_luck_event_service.gd")
const PartyMemberState = preload("res://scripts/player/progression/party_member_state.gd")
const PartyState = preload("res://scripts/player/progression/party_state.gd")
const BattleFateEventBus = BATTLE_FATE_EVENT_BUS_SCRIPT
const BattleResolutionResult = BATTLE_RESOLUTION_RESULT_SCRIPT
const BattleState = BATTLE_STATE_SCRIPT
const BattleUnitState = BATTLE_UNIT_STATE_SCRIPT

const HERO_ID: StringName = &"hero"

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_broken_bridge_survival_triggers_once_per_run()
	_test_lamp_without_witness_triggers_once_per_run()
	_test_borrowed_road_triggers_once_per_run()

	if _failures.is_empty():
		print("Low luck event service regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Low luck event service regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_broken_bridge_survival_triggers_once_per_run() -> void:
	var context := _build_context(-4)
	var service: LowLuckEventService = context.get("service") as LowLuckEventService
	var bus: BattleFateEventBus = context.get("bus") as BattleFateEventBus
	var party_state: PartyState = context.get("party_state") as PartyState
	if service == null or bus == null or party_state == null:
		_assert_true(false, "断桥生还测试前置构建失败。")
		return

	bus.dispatch(
		BATTLE_FATE_EVENT_BUS_SCRIPT.EVENT_HARDSHIP_SURVIVAL,
		_build_hardship_payload(&"bridge_battle_01", -4)
	)
	var first_result := service.handle_battle_resolution(
		_build_battle_state(&"bridge_battle_01", true),
		_build_battle_resolution_result(&"bridge_battle_01")
	)

	var first_triggered := ProgressionDataUtils.to_string_name_array(first_result.get("triggered_event_ids", []))
	_assert_true(first_triggered.has(LowLuckEventService.EVENT_BROKEN_BRIDGE_SURVIVAL), "低血 + 强 debuff 生还后应触发断桥生还。")
	var first_loot_entries: Array = first_result.get("loot_entries", [])
	_assert_eq(first_loot_entries.size(), 1, "断桥生还应固定产出 1 条 loot entry。")
	if first_loot_entries.size() == 1 and first_loot_entries[0] is Dictionary:
		var loot_entry: Dictionary = first_loot_entries[0]
		_assert_eq(String(loot_entry.get("item_id", "")), "calamity_shard", "断桥生还应走固定 calamity_shard。")
		_assert_eq(String(loot_entry.get("drop_source_kind", "")), "low_luck_event", "断桥生还应走 fixed low_luck_event 路径。")
	_assert_true(
		party_state.has_meta_flag(_build_member_flag_id(LowLuckEventService.EVENT_BROKEN_BRIDGE_SURVIVAL)),
		"断桥生还命中后应写入 PartyState.meta_flags 去重。"
	)

	var restored_party_state := _round_trip_party_state(party_state)
	if restored_party_state == null:
		return
	var restored_context := _build_context(-4, restored_party_state)
	var restored_service: LowLuckEventService = restored_context.get("service") as LowLuckEventService
	var restored_bus: BattleFateEventBus = restored_context.get("bus") as BattleFateEventBus
	if restored_service == null or restored_bus == null:
		_assert_true(false, "断桥生还 round-trip 后前置构建失败。")
		return

	restored_bus.dispatch(
		BATTLE_FATE_EVENT_BUS_SCRIPT.EVENT_HARDSHIP_SURVIVAL,
		_build_hardship_payload(&"bridge_battle_02", -4)
	)
	var second_result := restored_service.handle_battle_resolution(
		_build_battle_state(&"bridge_battle_02", true),
		_build_battle_resolution_result(&"bridge_battle_02")
	)
	_assert_true(
		(second_result.get("triggered_event_ids", []) as Array).is_empty(),
		"同周目重复命中断桥生还时不应再次发奖。"
	)
	_assert_true((second_result.get("loot_entries", []) as Array).is_empty(), "去重后断桥生还不应重复生成固定 loot。")


func _test_lamp_without_witness_triggers_once_per_run() -> void:
	var context := _build_context(-4)
	var service: LowLuckEventService = context.get("service") as LowLuckEventService
	var party_state: PartyState = context.get("party_state") as PartyState
	if service == null or party_state == null:
		_assert_true(false, "灯下无人测试前置构建失败。")
		return

	var first_result := service.handle_settlement_action({
		"action_id": "service:rest_full",
		"facility_name": "冷灯旅舍",
		"interaction_script_id": "service_rest_full",
		"service_type": "整备",
	})
	var first_triggered := ProgressionDataUtils.to_string_name_array(first_result.get("triggered_event_ids", []))
	_assert_true(first_triggered.has(LowLuckEventService.EVENT_LAMP_WITHOUT_WITNESS), "旅舍休整遇到 low luck 角色时应触发灯下无人。")
	var first_rewards: Array = first_result.get("pending_character_rewards", [])
	_assert_eq(first_rewards.size(), 1, "灯下无人应固定排入 1 条待领奖励。")
	if first_rewards.size() == 1 and first_rewards[0] is Dictionary:
		var reward_data: Dictionary = first_rewards[0]
		var reward_entries_variant: Variant = reward_data.get("entries", [])
		if reward_entries_variant is Array and not (reward_entries_variant as Array).is_empty() and (reward_entries_variant as Array)[0] is Dictionary:
			var first_entry: Dictionary = (reward_entries_variant as Array)[0]
			_assert_eq(String(first_entry.get("target_id", "")), "low_luck_black_market_hint", "灯下无人应固定发放黑市知识占位。")
	_assert_true(
		party_state.has_meta_flag(_build_party_flag_id(LowLuckEventService.EVENT_LAMP_WITHOUT_WITNESS)),
		"灯下无人命中后应写入 PartyState.meta_flags 去重。"
	)

	var restored_party_state := _round_trip_party_state(party_state)
	if restored_party_state == null:
		return
	var restored_context := _build_context(-4, restored_party_state)
	var restored_service: LowLuckEventService = restored_context.get("service") as LowLuckEventService
	if restored_service == null:
		_assert_true(false, "灯下无人 round-trip 后前置构建失败。")
		return

	var second_result := restored_service.handle_settlement_action({
		"action_id": "service:rest_full",
		"facility_name": "冷灯旅舍",
		"interaction_script_id": "service_rest_full",
		"service_type": "整备",
	})
	_assert_true(
		(second_result.get("triggered_event_ids", []) as Array).is_empty(),
		"同周目重复进入休整场景时不应再次触发灯下无人。"
	)
	_assert_true((second_result.get("pending_character_rewards", []) as Array).is_empty(), "去重后灯下无人不应重复排入奖励。")


func _test_borrowed_road_triggers_once_per_run() -> void:
	var context := _build_context(-5)
	var service: LowLuckEventService = context.get("service") as LowLuckEventService
	var bus: BattleFateEventBus = context.get("bus") as BattleFateEventBus
	var party_state: PartyState = context.get("party_state") as PartyState
	if service == null or bus == null or party_state == null:
		_assert_true(false, "死里借来的路测试前置构建失败。")
		return

	bus.dispatch(
		BATTLE_FATE_EVENT_BUS_SCRIPT.EVENT_CRITICAL_FAIL,
		_build_critical_fail_payload(&"borrowed_road_01", -5)
	)
	var first_result := service.handle_battle_resolution(
		_build_battle_state(&"borrowed_road_01", true),
		_build_battle_resolution_result(&"borrowed_road_01")
	)

	var first_triggered := ProgressionDataUtils.to_string_name_array(first_result.get("triggered_event_ids", []))
	_assert_true(first_triggered.has(LowLuckEventService.EVENT_BORROWED_ROAD), "low luck 角色大失败后仍赢下整场战斗时应触发死里借来的路。")
	var first_rewards: Array = first_result.get("pending_character_rewards", [])
	_assert_eq(first_rewards.size(), 1, "死里借来的路应固定排入 1 条待领奖励。")
	if first_rewards.size() == 1 and first_rewards[0] is Dictionary:
		var reward_data: Dictionary = first_rewards[0]
		var reward_entries_variant: Variant = reward_data.get("entries", [])
		if reward_entries_variant is Array and not (reward_entries_variant as Array).is_empty() and (reward_entries_variant as Array)[0] is Dictionary:
			var first_entry: Dictionary = (reward_entries_variant as Array)[0]
			_assert_eq(String(first_entry.get("target_id", "")), "low_luck_borrowed_road", "死里借来的路应固定发放借来的路占位知识。")
	_assert_true(
		party_state.has_meta_flag(_build_member_flag_id(LowLuckEventService.EVENT_BORROWED_ROAD)),
		"死里借来的路命中后应写入 PartyState.meta_flags 去重。"
	)

	var restored_party_state := _round_trip_party_state(party_state)
	if restored_party_state == null:
		return
	var restored_context := _build_context(-5, restored_party_state)
	var restored_service: LowLuckEventService = restored_context.get("service") as LowLuckEventService
	var restored_bus: BattleFateEventBus = restored_context.get("bus") as BattleFateEventBus
	if restored_service == null or restored_bus == null:
		_assert_true(false, "死里借来的路 round-trip 后前置构建失败。")
		return

	restored_bus.dispatch(
		BATTLE_FATE_EVENT_BUS_SCRIPT.EVENT_CRITICAL_FAIL,
		_build_critical_fail_payload(&"borrowed_road_02", -5)
	)
	var second_result := restored_service.handle_battle_resolution(
		_build_battle_state(&"borrowed_road_02", true),
		_build_battle_resolution_result(&"borrowed_road_02")
	)
	_assert_true(
		(second_result.get("triggered_event_ids", []) as Array).is_empty(),
		"同周目重复满足大失败获胜条件时不应再次触发死里借来的路。"
	)
	_assert_true((second_result.get("pending_character_rewards", []) as Array).is_empty(), "去重后死里借来的路不应重复排入奖励。")


func _build_context(hidden_luck_at_birth: int, party_state: PartyState = null) -> Dictionary:
	var resolved_party_state := party_state if party_state != null else _build_party_state(hidden_luck_at_birth)
	var manager := CharacterManagementModule.new()
	manager.setup(resolved_party_state, {}, {}, {})

	var bus := BattleFateEventBus.new()
	var service := LowLuckEventService.new()
	service.setup(manager, bus)

	return {
		"party_state": resolved_party_state,
		"manager": manager,
		"bus": bus,
		"service": service,
	}


func _build_party_state(hidden_luck_at_birth: int) -> PartyState:
	var party_state := PartyState.new()
	party_state.leader_member_id = HERO_ID
	party_state.main_character_member_id = HERO_ID
	party_state.active_member_ids = [HERO_ID]
	party_state.set_member_state(_build_member_state(hidden_luck_at_birth))
	return party_state


func _build_member_state(hidden_luck_at_birth: int) -> PartyMemberState:
	var member_state := PartyMemberState.new()
	member_state.member_id = HERO_ID
	member_state.display_name = "Hero"
	member_state.progression.unit_id = HERO_ID
	member_state.progression.display_name = "Hero"
	member_state.progression.character_level = 12
	member_state.progression.unit_base_attributes.set_attribute_value(&"hidden_luck_at_birth", hidden_luck_at_birth)
	return member_state


func _build_hardship_payload(battle_id: StringName, hidden_luck_at_birth: int) -> Dictionary:
	return {
		"battle_id": battle_id,
		"attacker_member_id": HERO_ID,
		"attacker_low_hp_hardship": true,
		"attacker_strong_attack_debuff_ids": [&"stunned"],
		"luck_snapshot": {
			"hidden_luck_at_birth": hidden_luck_at_birth,
		},
	}


func _build_critical_fail_payload(battle_id: StringName, hidden_luck_at_birth: int) -> Dictionary:
	return {
		"battle_id": battle_id,
		"attacker_member_id": HERO_ID,
		"luck_snapshot": {
			"hidden_luck_at_birth": hidden_luck_at_birth,
		},
	}


func _build_battle_state(battle_id: StringName, member_alive: bool) -> BattleState:
	var battle_state := BattleState.new()
	battle_state.battle_id = battle_id
	var unit_state := BattleUnitState.new()
	unit_state.unit_id = &"hero_unit"
	unit_state.source_member_id = HERO_ID
	unit_state.faction_id = &"player"
	unit_state.display_name = "Hero"
	unit_state.is_alive = member_alive
	battle_state.units[unit_state.unit_id] = unit_state
	battle_state.ally_unit_ids = [unit_state.unit_id]
	return battle_state


func _build_battle_resolution_result(battle_id: StringName) -> BattleResolutionResult:
	var result := BattleResolutionResult.new()
	result.battle_id = battle_id
	result.winner_faction_id = &"player"
	result.encounter_resolution = &"player_victory"
	return result


func _round_trip_party_state(party_state: PartyState) -> PartyState:
	var restored: PartyState = PartyState.from_dict(party_state.to_dict())
	_assert_true(restored != null, "PartyState 带 meta_flags 时应能完成 round-trip。")
	return restored


func _build_member_flag_id(event_id: StringName) -> StringName:
	return StringName("%s%s:%s" % [LowLuckEventService.META_FLAG_PREFIX, String(event_id), String(HERO_ID)])


func _build_party_flag_id(event_id: StringName) -> StringName:
	return StringName("%s%s" % [LowLuckEventService.META_FLAG_PREFIX, String(event_id)])


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
