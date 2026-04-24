extends SceneTree

const BATTLE_FATE_EVENT_BUS_SCRIPT = preload("res://scripts/systems/battle_fate_event_bus.gd")
const CharacterManagementModule = preload("res://scripts/systems/character_management_module.gd")
const FortuneService = preload("res://scripts/systems/fortune_service.gd")
const PartyMemberState = preload("res://scripts/player/progression/party_member_state.gd")
const PartyState = preload("res://scripts/player/progression/party_state.gd")
const BattleFateEventBus = preload("res://scripts/systems/battle_fate_event_bus.gd")


class StubRng:
	extends RefCounted

	var _rolls: Array[int] = []
	var call_count := 0


	func _init(rolls: Array[int] = []) -> void:
		_rolls = rolls.duplicate()


	func randi_range(min_value: int, max_value: int) -> int:
		if call_count >= _rolls.size():
			call_count += 1
			return min_value
		var roll := clampi(int(_rolls[call_count]), min_value, max_value)
		call_count += 1
		return roll


var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_grants_fortune_mark_after_confirmation_success()
	_test_failed_confirmation_does_not_grant_mark()
	_test_repeat_attempt_is_locked_per_member_per_run()
	_test_normal_enemy_event_can_grant_mark()

	if _failures.is_empty():
		print("FortuneService regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("FortuneService regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_grants_fortune_mark_after_confirmation_success() -> void:
	var context := _build_service_context()
	var service: FortuneService = context.get("service") as FortuneService
	var manager: CharacterManagementModule = context.get("manager") as CharacterManagementModule
	var party_state: PartyState = context.get("party_state") as PartyState
	if service == null or manager == null or party_state == null:
		_assert_true(false, "success case 前置构建失败。")
		return

	service.set_confirmation_rng_for_testing(StubRng.new([40, 40]))
	(context.get("bus") as BattleFateEventBus).dispatch(
		BATTLE_FATE_EVENT_BUS_SCRIPT.EVENT_CRITICAL_SUCCESS_UNDER_DISADVANTAGE,
		_build_payload(&"hero", true, &"battle_success")
	)

	_assert_eq(_get_fortune_marked_value(manager, &"hero"), 1, "二次确认成功后应写入 fortune_marked=1。")
	_assert_true(service.has_attempted_fortune_mark(&"hero"), "成功授予后应记录本周目已尝试。")
	_assert_true(
		party_state.has_fate_run_flag(_build_attempt_flag_id(&"hero")),
		"成功授予后 PartyState.fate_run_flags 应保留对应角色的尝试锁。"
	)
	service.dispose()


func _test_failed_confirmation_does_not_grant_mark() -> void:
	var context := _build_service_context()
	var service: FortuneService = context.get("service") as FortuneService
	var manager: CharacterManagementModule = context.get("manager") as CharacterManagementModule
	if service == null or manager == null:
		_assert_true(false, "confirm fail case 前置构建失败。")
		return

	service.set_confirmation_rng_for_testing(StubRng.new([1, 1]))
	(context.get("bus") as BattleFateEventBus).dispatch(
		BATTLE_FATE_EVENT_BUS_SCRIPT.EVENT_CRITICAL_SUCCESS_UNDER_DISADVANTAGE,
		_build_payload(&"hero", true, &"battle_confirm_fail")
	)

	_assert_eq(_get_fortune_marked_value(manager, &"hero"), 0, "二次确认失败时不应授予 fortune_marked。")
	_assert_true(service.has_attempted_fortune_mark(&"hero"), "二次确认失败后仍应保留 per-run 尝试锁。")
	service.dispose()


func _test_repeat_attempt_is_locked_per_member_per_run() -> void:
	var context := _build_service_context()
	var service: FortuneService = context.get("service") as FortuneService
	var manager: CharacterManagementModule = context.get("manager") as CharacterManagementModule
	var bus: BattleFateEventBus = context.get("bus") as BattleFateEventBus
	if service == null or manager == null or bus == null:
		_assert_true(false, "repeat lock case 前置构建失败。")
		return

	service.set_confirmation_rng_for_testing(StubRng.new([1, 1]))
	bus.dispatch(
		BATTLE_FATE_EVENT_BUS_SCRIPT.EVENT_CRITICAL_SUCCESS_UNDER_DISADVANTAGE,
		_build_payload(&"hero", true, &"battle_repeat_lock")
	)
	var blocked_rng := StubRng.new([40, 40])
	service.set_confirmation_rng_for_testing(blocked_rng)
	bus.dispatch(
		BATTLE_FATE_EVENT_BUS_SCRIPT.EVENT_CRITICAL_SUCCESS_UNDER_DISADVANTAGE,
		_build_payload(&"hero", true, &"battle_repeat_lock_second")
	)

	_assert_eq(_get_fortune_marked_value(manager, &"hero"), 0, "同一角色本周目第二次事件不应再次尝试授予。")
	_assert_eq(blocked_rng.call_count, 0, "重复尝试被锁后不应再消耗二次确认骰。")
	service.dispose()


func _test_normal_enemy_event_can_grant_mark() -> void:
	var context := _build_service_context()
	var service: FortuneService = context.get("service") as FortuneService
	var manager: CharacterManagementModule = context.get("manager") as CharacterManagementModule
	var bus: BattleFateEventBus = context.get("bus") as BattleFateEventBus
	var party_state: PartyState = context.get("party_state") as PartyState
	if service == null or manager == null or bus == null:
		_assert_true(false, "normal enemy case 前置构建失败。")
		return

	var confirm_rng := StubRng.new([40, 40])
	service.set_confirmation_rng_for_testing(confirm_rng)
	bus.dispatch(
		BATTLE_FATE_EVENT_BUS_SCRIPT.EVENT_CRITICAL_SUCCESS_UNDER_DISADVANTAGE,
		_build_payload(&"hero", false, &"battle_normal_enemy")
	)

	_assert_eq(_get_fortune_marked_value(manager, &"hero"), 1, "普通敌人事件也应允许授予 fortune_marked。")
	_assert_true(service.has_attempted_fortune_mark(&"hero"), "普通敌人事件成功后也应写入 per-run 尝试锁。")
	_assert_true(
		party_state.has_fate_run_flag(_build_attempt_flag_id(&"hero")),
		"普通敌人事件成功后 PartyState.fate_run_flags 也应保留尝试锁。"
	)
	_assert_eq(confirm_rng.call_count, 2, "普通敌人事件应按劣势确认规则消耗两次确认骰。")
	service.dispose()


func _build_service_context() -> Dictionary:
	var party_state := PartyState.new()
	party_state.leader_member_id = &"hero"
	party_state.main_character_member_id = &"hero"
	party_state.active_member_ids = [&"hero"]
	party_state.set_member_state(_build_member_state(&"hero", "Hero"))

	var manager := CharacterManagementModule.new()
	manager.setup(party_state, {}, {}, {})

	var bus := BATTLE_FATE_EVENT_BUS_SCRIPT.new()
	var service := FortuneService.new()
	service.setup(manager, bus)

	return {
		"party_state": party_state,
		"manager": manager,
		"bus": bus,
		"service": service,
	}


func _build_member_state(member_id: StringName, display_name: String) -> PartyMemberState:
	var member_state := PartyMemberState.new()
	member_state.member_id = member_id
	member_state.display_name = display_name
	member_state.progression.unit_id = member_id
	member_state.progression.display_name = display_name
	member_state.progression.unit_base_attributes.set_attribute_value(FortuneService.FORTUNE_MARKED_STAT_ID, 0)
	return member_state


func _build_payload(member_id: StringName, is_elite_or_boss: bool, battle_id: StringName) -> Dictionary:
	return {
		"battle_id": battle_id,
		"attacker_id": ProgressionDataUtils.to_string_name("%s_unit" % String(member_id)),
		"attacker_member_id": member_id,
		"defender_id": &"elite_target_01",
		"defender_member_id": &"",
		"defender_is_elite_or_boss": is_elite_or_boss,
		"attack_resolution": &"critical_hit",
		"is_disadvantage": true,
		"crit_gate_die": 40,
		"crit_gate_roll": 40,
		"hit_roll": 0,
		"luck_snapshot": {
			"hidden_luck_at_birth": -4,
			"faith_luck_bonus": 0,
			"effective_luck": -4,
			"fumble_low_end": 1,
			"crit_threshold": 20,
		},
	}


func _get_fortune_marked_value(manager: CharacterManagementModule, member_id: StringName) -> int:
	var member_state: PartyMemberState = manager.get_member_state(member_id)
	if member_state == null or member_state.progression == null or member_state.progression.unit_base_attributes == null:
		return 0
	return member_state.progression.unit_base_attributes.get_attribute_value(FortuneService.FORTUNE_MARKED_STAT_ID)


func _build_attempt_flag_id(member_id: StringName) -> StringName:
	return ProgressionDataUtils.to_string_name("%s%s" % [
		FortuneService.FORTUNE_MARK_ATTEMPT_FLAG_PREFIX,
		String(member_id),
	])


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
