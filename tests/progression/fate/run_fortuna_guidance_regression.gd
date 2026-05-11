extends SceneTree

const BATTLE_FATE_EVENT_BUS_SCRIPT = preload("res://scripts/systems/battle/fate/battle_fate_event_bus.gd")
const BATTLE_RESOLUTION_RESULT_SCRIPT = preload("res://scripts/systems/battle/core/battle_resolution_result.gd")
const BATTLE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_state.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const CharacterManagementModule = preload("res://scripts/systems/progression/character_management_module.gd")
const FaithService = preload("res://scripts/systems/progression/faith_service.gd")
const FortunaGuidanceService = preload("res://scripts/systems/battle/fate/fortuna_guidance_service.gd")
const FortuneService = preload("res://scripts/systems/battle/fate/fortune_service.gd")
const PartyMemberState = preload("res://scripts/player/progression/party_member_state.gd")
const PartyState = preload("res://scripts/player/progression/party_state.gd")
const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")
const BattleFateEventBus = preload("res://scripts/systems/battle/fate/battle_fate_event_bus.gd")
const BattleResolutionResult = BATTLE_RESOLUTION_RESULT_SCRIPT
const BattleState = BATTLE_STATE_SCRIPT
const BattleUnitState = BATTLE_UNIT_STATE_SCRIPT

const HERO_ID: StringName = &"hero"
const FORTUNA_DEITY_ID: StringName = &"fortuna"

var _failures: Array[String] = []


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


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_fortuna_guidance_unlock_chain_feeds_rank_2_to_5()

	if _failures.is_empty():
		print("Fortuna guidance regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Fortuna guidance regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_fortuna_guidance_unlock_chain_feeds_rank_2_to_5() -> void:
	var context := _build_context()
	var party_state: PartyState = context.get("party_state") as PartyState
	var manager: CharacterManagementModule = context.get("manager") as CharacterManagementModule
	var guidance: FortunaGuidanceService = context.get("guidance") as FortunaGuidanceService
	var fortune: FortuneService = context.get("fortune") as FortuneService
	var faith: FaithService = context.get("faith") as FaithService
	var bus: BattleFateEventBus = context.get("bus") as BattleFateEventBus
	if party_state == null or manager == null or guidance == null or fortune == null or faith == null or bus == null:
		_assert_true(false, "Fortuna guidance regression 前置构建失败。")
		return

	fortune.set_confirmation_rng_for_testing(StubRng.new([40, 40]))
	bus.dispatch(
		BATTLE_FATE_EVENT_BUS_SCRIPT.EVENT_CRITICAL_SUCCESS_UNDER_DISADVANTAGE,
		_build_disadvantage_crit_payload(&"battle_mark")
	)
	_assert_eq(_get_custom_stat(party_state, FortuneService.FORTUNE_MARKED_STAT_ID), 1, "第一次劣势大成功应授予 fortune_marked。")
	_assert_true(
		not _is_achievement_unlocked(party_state, FortunaGuidanceService.ACHIEVEMENT_GUIDANCE_TRUE),
		"用于授予 fortune_marked 的第一次事件不应顺带解锁 guidance_true。"
	)

	var rank_1_result := faith.execute_devotion(party_state, HERO_ID, FORTUNA_DEITY_ID)
	_assert_true(bool(rank_1_result.get("ok", false)), "fortune_marked 写入后应允许进入 Fortuna rank 1。")
	_apply_next_pending_reward(manager, party_state, 1)
	_assert_eq(_get_faith_luck_bonus(party_state), 1, "rank 1 结算后应写入 faith_luck_bonus=1。")

	var blocked_rank_2 := faith.execute_devotion(party_state, HERO_ID, FORTUNA_DEITY_ID)
	_assert_true(not bool(blocked_rank_2.get("ok", false)), "guidance_true 未解锁前不应进入 rank 2。")
	_assert_eq(String(blocked_rank_2.get("error_code", "")), "achievement_requirement_unmet", "rank 2 缺门票时应走 achievement gate。")
	_assert_eq(String(blocked_rank_2.get("missing_achievement_id", "")), "fortuna_guidance_true", "rank 2 应明确指出 guidance_true 缺失。")

	bus.dispatch(
		BATTLE_FATE_EVENT_BUS_SCRIPT.EVENT_CRITICAL_SUCCESS_UNDER_DISADVANTAGE,
		_build_disadvantage_crit_payload(&"battle_true")
	)
	_assert_true(_is_achievement_unlocked(party_state, FortunaGuidanceService.ACHIEVEMENT_GUIDANCE_TRUE), "再次命中条件后应解锁 guidance_true。")
	_assert_true(party_state.pending_character_rewards.is_empty(), "guidance 成就本身不应排入额外 reward 队列。")

	var rank_2_result := faith.execute_devotion(party_state, HERO_ID, FORTUNA_DEITY_ID)
	_assert_true(bool(rank_2_result.get("ok", false)), "guidance_true 达成后应允许进入 rank 2。")
	_apply_next_pending_reward(manager, party_state, 2)

	var blocked_rank_3 := faith.execute_devotion(party_state, HERO_ID, FORTUNA_DEITY_ID)
	_assert_true(not bool(blocked_rank_3.get("ok", false)), "guidance_devout 未解锁前不应进入 rank 3。")
	_assert_eq(String(blocked_rank_3.get("missing_achievement_id", "")), "fortuna_guidance_devout", "rank 3 应明确指出 guidance_devout 缺失。")

	bus.dispatch(
		BATTLE_FATE_EVENT_BUS_SCRIPT.EVENT_HARDSHIP_SURVIVAL,
		_build_devout_payload(&"battle_devout")
	)
	var devout_unlocks := guidance.handle_battle_resolution(
		_build_battle_state(&"battle_devout", true),
		_build_battle_resolution_result(&"battle_devout")
	)
	_assert_true(devout_unlocks.has(FortunaGuidanceService.ACHIEVEMENT_GUIDANCE_DEVOUT), "低血+强 debuff 活下来并赢战后应解锁 guidance_devout。")
	_assert_true(_is_achievement_unlocked(party_state, FortunaGuidanceService.ACHIEVEMENT_GUIDANCE_DEVOUT), "campaign achievement 记录应保留 guidance_devout。")

	var rank_3_result := faith.execute_devotion(party_state, HERO_ID, FORTUNA_DEITY_ID)
	_assert_true(bool(rank_3_result.get("ok", false)), "guidance_devout 达成后应允许进入 rank 3。")
	_apply_next_pending_reward(manager, party_state, 3)

	var blocked_rank_4 := faith.execute_devotion(party_state, HERO_ID, FORTUNA_DEITY_ID)
	_assert_true(not bool(blocked_rank_4.get("ok", false)), "guidance_exalted 未解锁前不应进入 rank 4。")
	_assert_eq(String(blocked_rank_4.get("missing_achievement_id", "")), "fortuna_guidance_exalted", "rank 4 应明确指出 guidance_exalted 缺失。")

	bus.dispatch(
		BATTLE_FATE_EVENT_BUS_SCRIPT.EVENT_HIGH_THREAT_CRITICAL_HIT,
		_build_exalted_payload(&"battle_exalted")
	)
	_assert_true(_is_achievement_unlocked(party_state, FortunaGuidanceService.ACHIEVEMENT_GUIDANCE_EXALTED), "高位威胁区大成功应解锁 guidance_exalted。")

	var rank_4_result := faith.execute_devotion(party_state, HERO_ID, FORTUNA_DEITY_ID)
	_assert_true(bool(rank_4_result.get("ok", false)), "guidance_exalted 达成后应允许进入 rank 4。")
	_apply_next_pending_reward(manager, party_state, 4)

	var blocked_rank_5 := faith.execute_devotion(party_state, HERO_ID, FORTUNA_DEITY_ID)
	_assert_true(not bool(blocked_rank_5.get("ok", false)), "guidance_blessed 未解锁前不应进入 rank 5。")
	_assert_eq(String(blocked_rank_5.get("missing_achievement_id", "")), "fortuna_guidance_blessed", "rank 5 应明确指出 guidance_blessed 缺失。")

	var chapter_unlocks := guidance.handle_chapter_completed({
		"chapter_id": &"chapter_01",
		"member_ids": [HERO_ID],
		"had_permanent_death": false,
	})
	_assert_true(chapter_unlocks.has(FortunaGuidanceService.ACHIEVEMENT_GUIDANCE_BLESSED), "章节无永久死亡且出现过 Fortuna 事件时应解锁 guidance_blessed。")
	_assert_true(_is_achievement_unlocked(party_state, FortunaGuidanceService.ACHIEVEMENT_GUIDANCE_BLESSED), "campaign achievement 记录应保留 guidance_blessed。")

	var rank_5_result := faith.execute_devotion(party_state, HERO_ID, FORTUNA_DEITY_ID)
	_assert_true(bool(rank_5_result.get("ok", false)), "guidance_blessed 达成后应允许进入 rank 5。")
	_apply_next_pending_reward(manager, party_state, 5)
	_assert_eq(_get_faith_luck_bonus(party_state), 5, "完整 guidance 链结算后 faith_luck_bonus 应到 rank 5。")

	guidance.dispose()
	fortune.dispose()


func _build_context() -> Dictionary:
	var party_state := PartyState.new()
	party_state.leader_member_id = HERO_ID
	party_state.main_character_member_id = HERO_ID
	party_state.active_member_ids = [HERO_ID]
	party_state.set_gold(50000)
	party_state.set_member_state(_build_member_state())

	var manager := CharacterManagementModule.new()
	manager.setup(
		party_state,
		{},
		{},
		ProgressionContentRegistry.new().get_achievement_defs()
	)

	var bus := BATTLE_FATE_EVENT_BUS_SCRIPT.new()
	var guidance := FortunaGuidanceService.new()
	guidance.setup(manager, bus)

	var fortune := FortuneService.new()
	fortune.setup(manager, bus)

	var faith := FaithService.new()
	return {
		"party_state": party_state,
		"manager": manager,
		"guidance": guidance,
		"fortune": fortune,
		"faith": faith,
		"bus": bus,
	}


func _build_member_state() -> PartyMemberState:
	var member_state := PartyMemberState.new()
	member_state.member_id = HERO_ID
	member_state.display_name = "Hero"
	member_state.progression.unit_id = HERO_ID
	member_state.progression.display_name = "Hero"
	member_state.progression.character_level = 30
	member_state.progression.unit_base_attributes.set_attribute_value(FortuneService.FORTUNE_MARKED_STAT_ID, 0)
	member_state.progression.unit_base_attributes.set_attribute_value(FaithService.FAITH_LUCK_BONUS_STAT_ID, 0)
	return member_state


func _build_disadvantage_crit_payload(battle_id: StringName) -> Dictionary:
	return {
		"battle_id": battle_id,
		"attacker_id": &"hero_unit",
		"attacker_member_id": HERO_ID,
		"attacker_low_hp_hardship": false,
		"attacker_strong_attack_debuff_ids": [],
		"defender_id": &"elite_target_01",
		"defender_member_id": &"",
		"defender_is_elite_or_boss": true,
		"attack_resolution": &"critical_hit",
		"critical_source": &"gate_die",
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


func _build_devout_payload(battle_id: StringName) -> Dictionary:
	return {
		"battle_id": battle_id,
		"attacker_id": &"hero_unit",
		"attacker_member_id": HERO_ID,
		"attacker_low_hp_hardship": true,
		"attacker_strong_attack_debuff_ids": [&"stunned"],
		"defender_id": &"elite_target_01",
		"defender_member_id": &"",
		"defender_is_elite_or_boss": true,
		"attack_resolution": &"hit",
		"critical_source": &"",
		"is_disadvantage": true,
		"crit_gate_die": 20,
		"crit_gate_roll": 20,
		"hit_roll": 12,
		"luck_snapshot": {
			"hidden_luck_at_birth": -4,
			"faith_luck_bonus": 2,
			"effective_luck": -2,
			"fumble_low_end": 1,
			"crit_threshold": 18,
		},
	}


func _build_exalted_payload(battle_id: StringName) -> Dictionary:
	return {
		"battle_id": battle_id,
		"attacker_id": &"hero_unit",
		"attacker_member_id": HERO_ID,
		"attacker_low_hp_hardship": false,
		"attacker_strong_attack_debuff_ids": [],
		"defender_id": &"elite_target_01",
		"defender_member_id": &"",
		"defender_is_elite_or_boss": true,
		"attack_resolution": &"critical_hit",
		"critical_source": &"high_threat",
		"is_disadvantage": false,
		"crit_gate_die": 20,
		"crit_gate_roll": 0,
		"hit_roll": 19,
		"luck_snapshot": {
			"hidden_luck_at_birth": -4,
			"faith_luck_bonus": 3,
			"effective_luck": -1,
			"fumble_low_end": 1,
			"crit_threshold": 19,
		},
	}


func _build_battle_state(battle_id: StringName, is_alive: bool) -> BattleState:
	var battle_state := BattleState.new()
	battle_state.battle_id = battle_id
	var unit := BattleUnitState.new()
	unit.unit_id = &"hero_unit"
	unit.source_member_id = HERO_ID
	unit.faction_id = &"player"
	unit.display_name = "Hero"
	unit.is_alive = is_alive
	battle_state.units[unit.unit_id] = unit
	battle_state.ally_unit_ids = [unit.unit_id]
	return battle_state


func _build_battle_resolution_result(battle_id: StringName) -> BattleResolutionResult:
	var result := BattleResolutionResult.new()
	result.battle_id = battle_id
	result.winner_faction_id = &"player"
	result.encounter_resolution = &"player_victory"
	return result


func _apply_next_pending_reward(manager: CharacterManagementModule, party_state: PartyState, expected_rank: int) -> void:
	var pending_reward = party_state.get_next_pending_character_reward()
	_assert_true(pending_reward != null, "Fortuna rank %d 应产生 pending reward。" % expected_rank)
	if pending_reward == null:
		return
	manager.apply_pending_character_reward(pending_reward)
	_assert_true(party_state.get_next_pending_character_reward() == null, "Fortuna rank %d 结算后应清空 pending reward。" % expected_rank)


func _is_achievement_unlocked(party_state: PartyState, achievement_id: StringName) -> bool:
	var member_state := party_state.get_member_state(HERO_ID) as PartyMemberState
	if member_state == null or member_state.progression == null:
		return false
	var progress_state = member_state.progression.get_achievement_progress_state(achievement_id)
	return progress_state != null and bool(progress_state.is_unlocked)


func _get_custom_stat(party_state: PartyState, stat_id: StringName) -> int:
	var member_state := party_state.get_member_state(HERO_ID) as PartyMemberState
	if member_state == null or member_state.progression == null or member_state.progression.unit_base_attributes == null:
		return 0
	return member_state.progression.unit_base_attributes.get_attribute_value(stat_id)


func _get_faith_luck_bonus(party_state: PartyState) -> int:
	return _get_custom_stat(party_state, FaithService.FAITH_LUCK_BONUS_STAT_ID)


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
