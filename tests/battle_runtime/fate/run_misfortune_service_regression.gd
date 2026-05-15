extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const BATTLE_RUNTIME_MODULE_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_runtime_module.gd")
const BATTLE_COMMAND_SCRIPT = preload("res://scripts/systems/battle/core/battle_command.gd")
const BATTLE_FATE_EVENT_BUS_SCRIPT = preload("res://scripts/systems/battle/fate/battle_fate_event_bus.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const ATTRIBUTE_SNAPSHOT_SCRIPT = preload("res://scripts/player/progression/attribute_snapshot.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const BattleRuntimeTestHelpers = preload("res://tests/shared/battle_runtime_test_helpers.gd")
const GAME_RUNTIME_SNAPSHOT_BUILDER_SCRIPT = preload("res://scripts/systems/game_runtime/game_runtime_snapshot_builder.gd")
const MISFORTUNE_SERVICE_SCRIPT = preload("res://scripts/systems/battle/fate/misfortune_service.gd")

const BattleRuntimeModule = BATTLE_RUNTIME_MODULE_SCRIPT
const BattleCommand = BATTLE_COMMAND_SCRIPT
const BattleFateEventBus = BATTLE_FATE_EVENT_BUS_SCRIPT
const BattleUnitState = BATTLE_UNIT_STATE_SCRIPT
const AttributeSnapshot = ATTRIBUTE_SNAPSHOT_SCRIPT
const GameRuntimeSnapshotBuilder = GAME_RUNTIME_SNAPSHOT_BUILDER_SCRIPT
const MisfortuneService = MISFORTUNE_SERVICE_SCRIPT

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


class TestEncounterAnchor:
	extends RefCounted

	var entity_id: StringName = &"misfortune_test_anchor"
	var display_name := "灾厄测试遭遇"
	var world_coord := Vector2i.ZERO
	var faction_id: StringName = &"hostile"
	var region_tag: StringName = &"test_region"
	var enemy_roster_template_id: StringName = &""
	var encounter_profile_id: StringName = &""
	var growth_stage := 0


class SnapshotRuntime:
	extends RefCounted

	var battle_state = null
	var battle_runtime = null
	var battle_selected_coord := Vector2i.ZERO
	var active_battle_encounter_id: StringName = &"misfortune_test_anchor"
	var active_battle_encounter_name := "灾厄测试遭遇"

	func is_battle_active() -> bool:
		return battle_state != null and not battle_state.is_empty()

	func get_status_text() -> String:
		return ""

	func get_active_modal_id() -> String:
		return ""

	func get_selected_settlement() -> Dictionary:
		return {}

	func get_selected_world_npc() -> Dictionary:
		return {}

	func get_selected_encounter_anchor():
		return null

	func get_selected_world_event() -> Dictionary:
		return {}

	func get_active_map_id() -> String:
		return ""

	func get_active_map_display_name() -> String:
		return ""

	func is_submap_active() -> bool:
		return false

	func get_world_step() -> int:
		return 0

	func get_player_coord() -> Vector2i:
		return Vector2i.ZERO

	func is_player_visible_on_world_map() -> bool:
		return false

	func get_selected_coord() -> Vector2i:
		return Vector2i.ZERO

	func get_pending_submap_prompt() -> Dictionary:
		return {}

	func get_submap_return_hint_text() -> String:
		return ""

	func get_game_over_context() -> Dictionary:
		return {}

	func get_party_state():
		return null

	func get_party_selected_member_id() -> StringName:
		return &""

	func get_pending_reward_count() -> int:
		return 0

	func get_member_achievement_summary(_member_id: StringName) -> Dictionary:
		return {}

	func get_member_attribute_snapshot(_member_id: StringName):
		return null

	func get_member_equipped_entries(_member_id: StringName) -> Array:
		return []

	func get_resolved_settlement_id() -> String:
		return ""

	func get_settlement_window_data(_settlement_id: String = "") -> Dictionary:
		return {}

	func get_settlement_feedback_text() -> String:
		return ""

	func get_shop_window_data() -> Dictionary:
		return {}

	func get_contract_board_window_data() -> Dictionary:
		return {}

	func get_active_contract_board_context() -> Dictionary:
		return {}

	func get_active_shop_context() -> Dictionary:
		return {}

	func get_forge_window_data() -> Dictionary:
		return {}

	func get_stagecoach_window_data() -> Dictionary:
		return {}

	func get_character_info_context() -> Dictionary:
		return {}

	func get_active_warehouse_entry_label() -> String:
		return ""

	func get_warehouse_window_data() -> Dictionary:
		return {}

	func get_battle_state():
		return battle_state

	func get_battle_runtime():
		return battle_runtime

	func get_battle_selected_coord() -> Vector2i:
		return battle_selected_coord

	func get_selected_battle_skill_id() -> StringName:
		return &""

	func get_selected_battle_skill_variant_id() -> StringName:
		return &""

	func get_selected_battle_skill_name() -> String:
		return ""

	func get_selected_battle_skill_variant_name() -> String:
		return ""

	func get_selected_battle_skill_target_coords() -> Array[Vector2i]:
		return []

	func get_selected_battle_skill_target_unit_ids() -> Array[StringName]:
		return []

	func get_selected_battle_skill_required_coord_count() -> int:
		return 0

	func get_active_battle_encounter_id() -> StringName:
		return active_battle_encounter_id

	func get_active_battle_encounter_name() -> String:
		return active_battle_encounter_name

	func get_battle_active_unit_name() -> String:
		if battle_state == null:
			return ""
		var active_unit := battle_state.units.get(battle_state.active_unit_id) as BattleUnitState
		return active_unit.display_name if active_unit != null else ""

	func get_pending_battle_start_prompt() -> Dictionary:
		return {}

	func get_battle_terrain_counts() -> Dictionary:
		return {}

	func get_snapshot_reward():
		return null

	func get_last_battle_loot_snapshot() -> Dictionary:
		return {}

	func get_current_promotion_prompt() -> Dictionary:
		return {}

	func get_log_snapshot(_limit: int = 30) -> Dictionary:
		return {}

	func get_nearby_encounter_entries(_limit: int = 8) -> Array[Dictionary]:
		return []

	func get_nearby_world_event_entries(_limit: int = 8) -> Array[Dictionary]:
		return []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_runtime_tracks_all_calamity_reasons_and_snapshot()
	_test_first_critical_fail_grants_reverse_fortune_and_cap_clamps()

	if _failures.is_empty():
		print("MisfortuneService regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("MisfortuneService regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_runtime_tracks_all_calamity_reasons_and_snapshot() -> void:
	var runtime := _build_runtime(-6, 2)
	var state = runtime.get_state()
	var hero := _get_runtime_unit(state, &"hero")
	var buddy := _get_runtime_unit(state, &"buddy")
	if hero == null or buddy == null:
		_assert_true(false, "全理由 case 前置构建失败。")
		return

	_dispatch_fate_event(runtime, BattleFateEventBus.EVENT_ORDINARY_MISS, &"hero")
	runtime.mark_applied_statuses_for_turn_timing(hero, [&"stunned"])
	buddy.current_hp = 0
	buddy.is_alive = false
	runtime.clear_defeated_unit(buddy)
	hero.current_hp = 20
	hero.current_ap = 1
	state.phase = &"unit_acting"
	state.active_unit_id = hero.unit_id
	runtime.issue_command(_build_wait_command(hero.unit_id))
	runtime.notify_member_boss_phase_changed(&"hero", &"phase_2")
	_dispatch_fate_event(runtime, BattleFateEventBus.EVENT_CRITICAL_FAIL, &"hero")
	_dispatch_fate_event(runtime, BattleFateEventBus.EVENT_ORDINARY_MISS, &"hero")

	_assert_eq(runtime.get_member_calamity_cap(&"hero"), 6, "rank 2/4 bonus 与极低 hidden luck 组合后 calamity cap 应为 6。")
	_assert_eq(runtime.get_member_calamity(&"hero"), 6, "六类首次坏运事件后 calamity 应累计到 6。")
	_assert_true(
		not hero.has_status_effect(MisfortuneService.REVERSE_FORTUNE_STATUS_ID),
		"若第一条 calamity 事件不是大失败，则不应补发 reverse_fortune。"
	)

	var snapshot_runtime := SnapshotRuntime.new()
	snapshot_runtime.battle_state = state
	snapshot_runtime.battle_runtime = runtime
	var builder := GameRuntimeSnapshotBuilder.new()
	builder.setup(snapshot_runtime)
	var snapshot: Dictionary = builder.build_headless_snapshot()
	var text_snapshot := builder.build_text_snapshot()

	_assert_eq(
		int(snapshot.get("battle", {}).get("calamity_by_member_id", {}).get("hero", -1)),
		6,
		"battle snapshot 应暴露 hero 的当前 calamity。"
	)
	_assert_true(text_snapshot.contains("calamity=hero=6"), "battle 文本快照应渲染 calamity 段。")

	builder.dispose()
	runtime.dispose()


func _test_first_critical_fail_grants_reverse_fortune_and_cap_clamps() -> void:
	var runtime := _build_runtime(0, 0)
	var state = runtime.get_state()
	var hero := _get_runtime_unit(state, &"hero")
	var buddy := _get_runtime_unit(state, &"buddy")
	if hero == null or buddy == null:
		_assert_true(false, "critical fail first case 前置构建失败。")
		return

	_dispatch_fate_event(runtime, BattleFateEventBus.EVENT_CRITICAL_FAIL, &"hero")
	_assert_eq(runtime.get_member_calamity_cap(&"hero"), 3, "默认角色 calamity cap 应为 3。")
	_assert_eq(runtime.get_member_calamity(&"hero"), 1, "第一次 critical_fail 应先授予 1 点 calamity。")
	_assert_true(hero.has_status_effect(MisfortuneService.REVERSE_FORTUNE_STATUS_ID), "第一次 calamity 事件就是大失败时应授予 reverse_fortune。")
	var reverse_fortune = hero.get_status_effect(MisfortuneService.REVERSE_FORTUNE_STATUS_ID)
	_assert_eq(int(reverse_fortune.duration) if reverse_fortune != null else -1, 60, "reverse_fortune 应维持 1 回合基准 duration。")

	_dispatch_fate_event(runtime, BattleFateEventBus.EVENT_ORDINARY_MISS, &"hero")
	runtime.mark_applied_statuses_for_turn_timing(hero, [&"fear"])
	runtime.notify_member_boss_phase_changed(&"hero", &"phase_2")
	buddy.current_hp = 0
	buddy.is_alive = false
	runtime.clear_defeated_unit(buddy)

	_assert_eq(runtime.get_member_calamity(&"hero"), 3, "超出默认上限后 calamity 不应继续增长。")
	_assert_true(
		runtime.get_calamity_by_member_id().get(&"hero", 0) == 3,
		"BattleRuntime.calamity_by_member_id 应与 MisfortuneService 计算结果保持同步。"
	)

	runtime.dispose()


func _build_runtime(hidden_luck_at_birth: int, calamity_capacity_bonus: int) -> BattleRuntimeModule:
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, {}, {}, {}, null)

	var hero := _build_member_unit(&"hero", "Hero", 100, hidden_luck_at_birth, calamity_capacity_bonus)
	var buddy := _build_member_unit(&"buddy", "Buddy", 80, 0, 0)
	var boss := _build_enemy_unit(&"boss_01", "Boss")
	var encounter_anchor := TestEncounterAnchor.new()
	var context := {
		"battle_map_size": Vector2i(6, 6),
		"ally_spawns": [Vector2i(1, 1), Vector2i(2, 1)],
		"enemy_spawns": [Vector2i(4, 4)],
		"battle_party": [hero.to_dict(), buddy.to_dict()],
		"enemy_units": [boss.to_dict()],
	}
	runtime.start_battle(encounter_anchor, 101, context)
	return runtime


func _build_member_unit(
	member_id: StringName,
	display_name: String,
	hp_max: int,
	hidden_luck_at_birth: int,
	calamity_capacity_bonus: int
) -> BattleUnitState:
	var unit_state := BattleUnitState.new()
	unit_state.unit_id = member_id
	unit_state.source_member_id = member_id
	unit_state.display_name = display_name
	unit_state.faction_id = &"player"
	unit_state.control_mode = &"manual"
	unit_state.attribute_snapshot = _build_attribute_snapshot(hp_max, hidden_luck_at_birth, calamity_capacity_bonus)
	unit_state.current_hp = hp_max
	unit_state.current_mp = 0
	unit_state.current_stamina = 0
	unit_state.current_aura = 0
	unit_state.current_ap = 1
	unit_state.is_alive = true
	return unit_state


func _build_enemy_unit(unit_id: StringName, display_name: String) -> BattleUnitState:
	var unit_state := BattleUnitState.new()
	unit_state.unit_id = unit_id
	unit_state.display_name = display_name
	unit_state.faction_id = &"hostile"
	unit_state.control_mode = &"ai"
	unit_state.attribute_snapshot = _build_attribute_snapshot(160, 0, 0)
	unit_state.current_hp = 160
	unit_state.current_ap = 1
	unit_state.is_alive = true
	return unit_state


func _build_attribute_snapshot(hp_max: int, hidden_luck_at_birth: int, calamity_capacity_bonus: int) -> AttributeSnapshot:
	var snapshot := AttributeSnapshot.new()
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX, hp_max)
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.MP_MAX, 0)
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX, 0)
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.AURA_MAX, 0)
	snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ACTION_POINTS, 1)
	snapshot.set_value(&"hidden_luck_at_birth", hidden_luck_at_birth)
	snapshot.set_value(&"calamity_capacity_bonus", calamity_capacity_bonus)
	BattleRuntimeTestHelpers.seed_attribute_snapshot_base_attributes_and_ac(snapshot)
	return snapshot


func _get_runtime_unit(state, member_id: StringName) -> BattleUnitState:
	if state == null:
		return null
	return state.units.get(member_id) as BattleUnitState


func _build_wait_command(unit_id: StringName) -> BattleCommand:
	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_WAIT
	command.unit_id = unit_id
	return command


func _dispatch_fate_event(runtime: BattleRuntimeModule, event_type: StringName, member_id: StringName) -> void:
	runtime.get_fate_event_bus().dispatch(event_type, {
		"battle_id": runtime.get_state().battle_id if runtime.get_state() != null else &"",
		"attacker_member_id": member_id,
	})


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
