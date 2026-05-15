extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")
const BattleRuntimeTestHelpers = preload("res://tests/shared/battle_runtime_test_helpers.gd")

const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const BATTLE_CELL_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BATTLE_COMMAND_SCRIPT = preload("res://scripts/systems/battle/core/battle_command.gd")
const BATTLE_RUNTIME_MODULE_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_runtime_module.gd")
const BATTLE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_state.gd")
const BATTLE_TIMELINE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_timeline_state.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")
const SharedDamageResolvers = preload("res://tests/shared/stub_damage_resolvers.gd")
const SharedHitResolvers = preload("res://tests/shared/stub_hit_resolvers.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_fireball_normal_cast_hits_friend_at_full_damage_route()
	_test_fireball_burn_applies_to_every_team_in_area()
	_test_fireball_critical_refunds_mp_without_blocking_friendly_fire()
	_test_fireball_protected_fumble_consumes_extra_mp_and_skips_blast()
	_test_fireball_unprotected_fumble_drifts_ground_anchor()
	if _failures.is_empty():
		print("Magic backlash regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Magic backlash regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_fireball_normal_cast_hits_friend_at_full_damage_route() -> void:
	var runtime = _build_runtime_with_spell_control_roll(10)
	var state = _build_state(Vector2i(3, 1))
	var caster = _build_unit(&"normal_caster", &"player", Vector2i(0, 0), 1, 200, 0)
	var friend = _build_unit(&"normal_friend", &"player", Vector2i(1, 0), 0, 0, 0)
	var enemy = _build_unit(&"normal_enemy", &"enemy", Vector2i(2, 0), 0, 0, 0)
	_add_unit(runtime, state, caster, false)
	_add_unit(runtime, state, friend, false)
	_add_unit(runtime, state, enemy, true)
	_activate(runtime, state, caster)

	var command = _build_fireball_command(caster.unit_id, friend.coord)
	var preview = runtime.preview_command(command)
	var before_friend_hp := int(friend.current_hp)
	var batch = runtime.issue_command(command)

	_assert_true(preview.allowed, "火球术瞄准友军地格应通过地面目标预览。")
	_assert_true(preview.target_unit_ids.has(friend.unit_id), "火球术普通预览应把范围内友军列为受影响单位。")
	_assert_true(batch.changed_unit_ids.has(friend.unit_id), "普通施法应标记被火球波及的友军。")
	_assert_true(friend.current_hp < before_friend_hp, "普通施法时范围内友军应受到火球伤害。")
	_assert_eq(caster.current_mp, 100, "普通施法只应扣除火球本身 100 法力。")


func _test_fireball_burn_applies_to_every_team_in_area() -> void:
	var runtime = _build_runtime_with_spell_control_roll(10)
	var state = _build_state(Vector2i(3, 1))
	var caster = _build_unit(&"burn_caster", &"player", Vector2i(0, 0), 1, 200, 3)
	var friend = _build_unit(&"burn_friend", &"player", Vector2i(1, 0), 0, 0, 0)
	var enemy = _build_unit(&"burn_enemy", &"enemy", Vector2i(2, 0), 0, 0, 0)
	_add_unit(runtime, state, caster, false)
	_add_unit(runtime, state, friend, false)
	_add_unit(runtime, state, enemy, true)
	_activate(runtime, state, caster)

	var batch = runtime.issue_command(_build_fireball_command(caster.unit_id, friend.coord))

	var friend_burning = friend.get_status_effect(&"burning")
	var before_burn_tick_hp := int(friend.current_hp)
	_assert_true(caster.has_status_effect(&"burning"), "火球术灼烧不应保护范围内施法者。")
	_assert_true(friend_burning != null, "火球术灼烧应和伤害一样波及友军。")
	_assert_true(enemy.has_status_effect(&"burning"), "火球术灼烧仍应作用于范围内敌人。")
	_assert_eq(int(friend_burning.tick_interval_tu) if friend_burning != null else -1, 10, "火球术友军灼烧应保留正式 timeline tick。")
	_assert_true(batch.changed_unit_ids.has(friend.unit_id), "友军被灼烧时应标记单位变化。")
	_advance_timeline_tu(runtime, state, 10)
	_assert_true(friend.current_hp < before_burn_tick_hp, "火球术友军灼烧应按 timeline tick 造成伤害。")


func _test_fireball_critical_refunds_mp_without_blocking_friendly_fire() -> void:
	var runtime = _build_runtime_with_spell_control_roll(20)
	var state = _build_state(Vector2i(3, 1))
	var caster = _build_unit(&"crit_caster", &"player", Vector2i(0, 0), 1, 200, 0)
	var friend = _build_unit(&"crit_friend", &"player", Vector2i(1, 0), 0, 0, 0)
	var enemy = _build_unit(&"crit_enemy", &"enemy", Vector2i(2, 0), 0, 0, 0)
	_add_unit(runtime, state, caster, false)
	_add_unit(runtime, state, friend, false)
	_add_unit(runtime, state, enemy, true)
	_activate(runtime, state, caster)

	var before_friend_hp := int(friend.current_hp)
	var batch = runtime.issue_command(_build_fireball_command(caster.unit_id, friend.coord))

	_assert_true(friend.current_hp < before_friend_hp, "法术控制大成功不应取消范围内友军伤害。")
	_assert_eq(caster.current_mp, 150, "火球术大成功应返还本次实际法力消耗的 50%。")
	_assert_true(_logs_contain(batch.log_lines, "返还 50 点法力"), "火球术大成功应写入 MP 返还日志。")


func _test_fireball_protected_fumble_consumes_extra_mp_and_skips_blast() -> void:
	var runtime = _build_runtime_with_spell_control_roll(1)
	var state = _build_state(Vector2i(3, 1))
	var caster = _build_unit(&"protected_caster", &"player", Vector2i(0, 0), 1, 250, 3)
	var friend = _build_unit(&"protected_friend", &"player", Vector2i(1, 0), 0, 0, 0)
	var enemy = _build_unit(&"protected_enemy", &"enemy", Vector2i(2, 0), 0, 0, 0)
	_add_unit(runtime, state, caster, false)
	_add_unit(runtime, state, friend, false)
	_add_unit(runtime, state, enemy, true)
	_activate(runtime, state, caster)

	var before_friend_hp := int(friend.current_hp)
	var batch = runtime.issue_command(_build_fireball_command(caster.unit_id, friend.coord))

	_assert_eq(friend.current_hp, before_friend_hp, "受精通保护的大失败不应释放火球爆炸。")
	_assert_eq(caster.current_mp, 50, "受保护大失败应在原 100 法力外额外吞噬 100 法力。")
	_assert_eq(int(caster.fumble_protection_used.get(&"mage_fireball", 0)), 1, "受保护大失败应消耗一次火球术保护次数。")
	_assert_true(not batch.changed_unit_ids.has(friend.unit_id), "受保护大失败不应标记目标友军变化。")


func _test_fireball_unprotected_fumble_drifts_ground_anchor() -> void:
	var runtime = _build_runtime_with_spell_control_roll(1)
	var state = _build_state(Vector2i(2, 1))
	var caster = _build_unit(&"drift_caster", &"player", Vector2i(0, 0), 1, 200, 0)
	var friend = _build_unit(&"drift_friend", &"player", Vector2i(1, 0), 0, 0, 0)
	_add_unit(runtime, state, caster, false)
	_add_unit(runtime, state, friend, false)
	_activate(runtime, state, caster)

	var before_caster_hp := int(caster.current_hp)
	var before_friend_hp := int(friend.current_hp)
	var batch = runtime.issue_command(_build_fireball_command(caster.unit_id, caster.coord))

	_assert_eq(caster.current_hp, before_caster_hp, "无保护大失败偏移后不应继续结算原落点。")
	_assert_true(friend.current_hp < before_friend_hp, "无保护大失败应把火球偏移到唯一候选地格并伤到友军。")
	_assert_true(_logs_contain(batch.log_lines, "偏移到 (1, 0)"), "无保护大失败应写入明确的落点偏移日志。")


func _build_runtime_with_spell_control_roll(roll: int):
	var skill_def = load("res://data/configs/skills/mage_fireball.tres")
	var runtime = BATTLE_RUNTIME_MODULE_SCRIPT.new()
	runtime.setup(null, {skill_def.skill_id: skill_def}, {}, {})
	runtime.configure_damage_resolver_for_tests(SharedDamageResolvers.FixedFailedSaveDamageResolver.new([], [roll]))
	runtime.configure_hit_resolver_for_tests(SharedHitResolvers.FixedHitResolver.new(roll))
	return runtime


func _build_state(map_size: Vector2i):
	var state = BATTLE_STATE_SCRIPT.new()
	state.battle_id = &"magic_backlash_regression"
	state.phase = &"unit_acting"
	state.map_size = map_size
	state.timeline = BATTLE_TIMELINE_STATE_SCRIPT.new()
	state.cells = {}
	for y in range(map_size.y):
		for x in range(map_size.x):
			state.cells[Vector2i(x, y)] = _build_cell(Vector2i(x, y))
	state.cell_columns = BATTLE_CELL_STATE_SCRIPT.build_columns_from_surface_cells(state.cells)
	return state


func _build_cell(coord: Vector2i):
	var cell = BATTLE_CELL_STATE_SCRIPT.new()
	cell.coord = coord
	cell.base_terrain = BATTLE_CELL_STATE_SCRIPT.TERRAIN_LAND
	cell.base_height = 4
	cell.recalculate_runtime_values()
	return cell


func _build_unit(
	unit_id: StringName,
	faction_id: StringName,
	coord: Vector2i,
	current_ap: int,
	current_mp: int,
	fireball_level: int
):
	var unit = BATTLE_UNIT_STATE_SCRIPT.new()
	unit.unit_id = unit_id
	unit.source_member_id = unit_id
	unit.display_name = String(unit_id)
	unit.faction_id = faction_id
	unit.current_ap = current_ap
	unit.current_move_points = BATTLE_UNIT_STATE_SCRIPT.DEFAULT_MOVE_POINTS_PER_TURN
	unit.current_hp = 100
	unit.current_mp = current_mp
	unit.current_stamina = 60
	unit.is_alive = true
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX, 100)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.MP_MAX, maxi(current_mp, 200))
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.SPELL_PROFICIENCY_BONUS, 2)
	unit.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.AGILITY, 10)
	unit.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.INTELLIGENCE, 16)
	unit.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.HIDDEN_LUCK_AT_BIRTH, 0)
	unit.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.FAITH_LUCK_BONUS, 0)
	# 派生 AC=BASE_ARMOR_CLASS(8)+agility_mod(0)，BattleHitResolver 需要 ARMOR_CLASS 显式存在。
	unit.attribute_snapshot.set_value(
		ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS,
		ATTRIBUTE_SERVICE_SCRIPT.BASE_ARMOR_CLASS
	)
	# Fixture 显式给了 mp_max，就视作 MP 资源已解锁；否则技能 preview 在 get_locked_combat_resource_block_reason 直接拒。
	unit.unlock_combat_resource(BATTLE_UNIT_STATE_SCRIPT.COMBAT_RESOURCE_MP)
	unit.unlock_combat_resource(BATTLE_UNIT_STATE_SCRIPT.COMBAT_RESOURCE_AURA)
	if fireball_level >= 0:
		unit.known_active_skill_ids.append(&"mage_fireball")
		unit.known_skill_level_map[&"mage_fireball"] = fireball_level
	unit.set_anchor_coord(coord)
	return unit


func _add_unit(runtime, state, unit, is_enemy: bool) -> void:
	BattleRuntimeTestHelpers.register_unit_in_state(state, unit, is_enemy)
	_assert_true(runtime._grid_service.place_unit(state, unit, unit.coord, true), "%s 应能放入战场。" % String(unit.unit_id))


func _activate(runtime, state, caster) -> void:
	state.active_unit_id = caster.unit_id
	runtime._state = state


func _build_fireball_command(unit_id: StringName, target_coord: Vector2i):
	var command = BATTLE_COMMAND_SCRIPT.new()
	command.command_type = BATTLE_COMMAND_SCRIPT.TYPE_SKILL
	command.unit_id = unit_id
	command.skill_id = &"mage_fireball"
	command.target_coord = target_coord
	command.target_coords.append(target_coord)
	return command


func _logs_contain(log_lines: Array[String], needle: String) -> bool:
	for line in log_lines:
		if line.find(needle) >= 0:
			return true
	return false


func _advance_timeline_tu(runtime, state, total_tu: int) -> void:
	if runtime == null or state == null or total_tu <= 0:
		return
	state.phase = &"timeline_running"
	state.active_unit_id = &""
	state.timeline.ready_unit_ids.clear()
	state.timeline.tu_per_tick = 5
	for unit_variant in state.units.values():
		var unit_state = unit_variant
		if unit_state != null:
			unit_state.action_threshold = 1000000
	runtime.advance(int(total_tu / 5))


func _assert_true(value: bool, message: String) -> void:
	if value:
		return
	_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual == expected:
		return
	_test.fail("%s expected=%s actual=%s" % [message, str(expected), str(actual)])
