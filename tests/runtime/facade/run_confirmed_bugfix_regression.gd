extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const BATTLE_DAMAGE_RESOLVER_SCRIPT = preload("res://scripts/systems/battle/rules/battle_damage_resolver.gd")
const BATTLE_FATE_ATTACK_RULES_SCRIPT = preload("res://scripts/systems/battle/fate/battle_fate_attack_rules.gd")
const BATTLE_HIT_RESOLVER_SCRIPT = preload("res://scripts/systems/battle/rules/battle_hit_resolver.gd")
const EQUIPMENT_INSTANCE_STATE_SCRIPT = preload("res://scripts/player/warehouse/equipment_instance_state.gd")
const PARTY_EQUIPMENT_SERVICE_SCRIPT = preload("res://scripts/systems/inventory/party_equipment_service.gd")
const PARTY_MEMBER_STATE_SCRIPT = preload("res://scripts/player/progression/party_member_state.gd")
const PARTY_STATE_SCRIPT = preload("res://scripts/player/progression/party_state.gd")
const WORLD_MAP_GRID_SYSTEM_SCRIPT = preload("res://scripts/systems/world/world_map_grid_system.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_status_param_lookup_accepts_string_name_keys()
	_test_attack_disposition_respects_natural_roll_flags()
	_test_world_footprint_re_register_clears_old_cells()
	_test_missing_item_def_does_not_trap_equipped_instance()

	if _failures.is_empty():
		print("Confirmed bugfix regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Confirmed bugfix regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_status_param_lookup_accepts_string_name_keys() -> void:
	var params := {
		&"incoming_damage_multiplier": 1.5,
		&"lock_crit": true,
	}
	var damage_resolver = BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	var hit_resolver = BATTLE_HIT_RESOLVER_SCRIPT.new()
	var fate_rules = BATTLE_FATE_ATTACK_RULES_SCRIPT.new()

	_assert_eq(
		damage_resolver._get_status_param_string_key(params, &"incoming_damage_multiplier", 1.0),
		1.5,
		"伤害解析器应能读取 StringName key 的状态参数。"
	)
	_assert_eq(
		hit_resolver._get_status_param_string_key(params, &"lock_crit", false),
		true,
		"命中解析器应能读取 StringName key 的状态参数。"
	)
	_assert_eq(
		fate_rules._get_status_param_string_key(params, &"lock_crit", false),
		true,
		"命运攻击规则应能读取 StringName key 的状态参数。"
	)


func _test_attack_disposition_respects_natural_roll_flags() -> void:
	var hit_resolver = BATTLE_HIT_RESOLVER_SCRIPT.new()
	var forced_hit_check := {
		"required_roll": 1,
		"natural_one_auto_miss": false,
		"natural_twenty_auto_hit": false,
	}
	var disposition: StringName = hit_resolver._resolve_attack_roll_disposition_for_check(1, forced_hit_check)
	_assert_eq(
		disposition,
		hit_resolver.ROLL_DISPOSITION_THRESHOLD_HIT,
		"关闭 natural_one_auto_miss 后，d20=1 且 required_roll=1 应按普通命中处理。"
	)


func _test_world_footprint_re_register_clears_old_cells() -> void:
	var grid_system = WORLD_MAP_GRID_SYSTEM_SCRIPT.new()
	grid_system.setup(Vector2i(3, 3), Vector2i.ONE)

	_assert_true(
		grid_system.register_footprint("camp", Vector2i(0, 0), Vector2i(2, 1)),
		"初次注册 footprint 应成功。"
	)
	_assert_true(
		grid_system.register_footprint("camp", Vector2i(1, 1), Vector2i.ONE),
		"同 entity_id 重新注册 footprint 应成功。"
	)
	_assert_eq(grid_system.get_occupant_root(Vector2i(0, 0)), "", "重新注册后旧 footprint 占用应被清理。")
	_assert_eq(grid_system.get_occupant_root(Vector2i(1, 1)), "camp", "重新注册后新 footprint 应可读取。")
	_assert_true(
		not grid_system.register_footprint("camp", Vector2i(9, 9), Vector2i.ONE),
		"越界重新注册应失败。"
	)
	_assert_eq(grid_system.get_occupant_root(Vector2i(1, 1)), "camp", "越界注册失败后旧 footprint 应被恢复。")


func _test_missing_item_def_does_not_trap_equipped_instance() -> void:
	var party_state = PARTY_STATE_SCRIPT.new()
	var member_state = PARTY_MEMBER_STATE_SCRIPT.new()
	member_state.member_id = &"member_a"
	member_state.progression.unit_base_attributes.set_attribute_value(&"storage_space", 1)
	party_state.member_states[member_state.member_id] = member_state
	var occupied_slots: Array[StringName] = [&"main_hand"]
	var instance = EQUIPMENT_INSTANCE_STATE_SCRIPT.create(&"missing_sword", &"eq_missing_sword")
	_assert_true(
		member_state.equipment_state.set_equipped_entry(&"main_hand", &"missing_sword", occupied_slots, instance),
		"卸装回归前置：应能写入缺定义装备实例。"
	)

	var equipment_service = PARTY_EQUIPMENT_SERVICE_SCRIPT.new()
	equipment_service.setup(party_state, {})
	var result: Dictionary = equipment_service.unequip_item(&"member_a", &"main_hand")
	_assert_eq(bool(result.get("success", false)), true, "缺失 item_def 的已装备实例仍应可卸下。")
	_assert_eq(member_state.equipment_state.get_equipped_item_id(&"main_hand"), &"", "卸下后装备槽应为空。")
	_assert_eq(party_state.warehouse_state.get_non_empty_instances().size(), 1, "卸下的坏配置装备实例应回到仓库，不能丢失。")


func _assert_true(value: bool, message: String) -> void:
	if not value:
		_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s expected=%s actual=%s" % [message, str(expected), str(actual)])
