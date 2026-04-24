extends SceneTree

const BattleRuntimeModule = preload("res://scripts/systems/battle_runtime_module.gd")
const BattleCommand = preload("res://scripts/systems/battle_command.gd")
const BattleState = preload("res://scripts/systems/battle_state.gd")
const BattleTimelineState = preload("res://scripts/systems/battle_timeline_state.gd")
const BattleCellState = preload("res://scripts/systems/battle_cell_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle_unit_state.gd")
const BattleStatusSemanticTable = preload("res://scripts/systems/battle_status_semantic_table.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_staggered_refreshes_without_stacking_and_expires_on_tu_progress()
	_test_burning_stacks_and_ticks_each_turn()
	_test_slow_increases_move_cost_and_expires_on_tu_progress()
	_test_refresh_timeline_statuses_keep_single_stack_and_max_duration()
	_test_taunted_uses_timeline_decay_without_turn_end_decay()
	_test_status_duration_is_not_backfilled_from_semantic_defaults()
	if _failures.is_empty():
		print("Status effect semantics regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Status effect semantics regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_staggered_refreshes_without_stacking_and_expires_on_tu_progress() -> void:
	var runtime := _build_runtime()
	var state := _build_state(Vector2i(4, 3))
	var striker := _build_unit(&"staggered_source", Vector2i(1, 1), 2)
	var target := _build_unit(&"staggered_target", Vector2i(2, 1), 2)
	target.faction_id = &"enemy"

	_add_unit(runtime, state, striker)
	_add_unit(runtime, state, target)
	state.ally_unit_ids = [striker.unit_id]
	state.enemy_unit_ids = [target.unit_id]
	runtime._state = state

	_apply_status(runtime, striker, target, &"staggered", 15)
	_apply_status(runtime, striker, target, &"staggered", 15)
	var stagger_entry = target.get_status_effect(&"staggered")
	_assert_true(stagger_entry != null, "重复施加 staggered 后应保留正式状态。")
	_assert_eq(int(stagger_entry.stacks) if stagger_entry != null else -1, 1, "staggered 应按 refresh 语义而不是累加层数。")
	_assert_eq(int(stagger_entry.duration) if stagger_entry != null else -1, 15, "staggered 应记录剩余 TU。")

	state.phase = &"timeline_running"
	state.active_unit_id = &""
	state.timeline.ready_unit_ids.clear()
	state.timeline.ready_unit_ids.append(target.unit_id)
	runtime.advance(0.0)
	_assert_eq(target.current_ap, 1, "staggered 刷新后仍只应在回合开始扣 1 点行动点。")

	var wait_command := BattleCommand.new()
	wait_command.command_type = BattleCommand.TYPE_WAIT
	wait_command.unit_id = target.unit_id
	runtime.issue_command(wait_command)
	_assert_true(target.has_status_effect(&"staggered"), "staggered 不应在目标回合结束后被立即移除。")
	_advance_timeline_tu(runtime, state, 15)
	_assert_true(not target.has_status_effect(&"staggered"), "staggered 应在 TU 走完后移除。")


func _test_burning_stacks_and_ticks_each_turn() -> void:
	var runtime := _build_runtime()
	var state := _build_state(Vector2i(4, 3))
	var caster := _build_unit(&"burning_source", Vector2i(0, 1), 2)
	var target := _build_unit(&"burning_target", Vector2i(2, 1), 2)
	target.faction_id = &"enemy"
	target.current_hp = 20
	target.attribute_snapshot.set_value(&"hp_max", 20)

	_add_unit(runtime, state, caster)
	_add_unit(runtime, state, target)
	state.ally_unit_ids = [caster.unit_id]
	state.enemy_unit_ids = [target.unit_id]
	runtime._state = state

	_apply_status(runtime, caster, target, &"burning", 20)
	_apply_status(runtime, caster, target, &"burning", 20)
	var burning_entry = target.get_status_effect(&"burning")
	_assert_true(burning_entry != null, "burning 应在重复施加后存在于正式状态字典中。")
	_assert_eq(int(burning_entry.stacks) if burning_entry != null else -1, 2, "burning 应按 add 语义累加层数。")
	_assert_eq(int(burning_entry.duration) if burning_entry != null else -1, 20, "burning 应沿用施加时给定的剩余 TU。")

	state.phase = &"timeline_running"
	state.active_unit_id = &""
	state.timeline.ready_unit_ids.clear()
	state.timeline.ready_unit_ids.append(target.unit_id)
	runtime.advance(0.0)
	_assert_eq(target.current_hp, 18, "2 层 burning 应在回合开始稳定结算 2 点灼烧伤害。")
	var first_wait := BattleCommand.new()
	first_wait.command_type = BattleCommand.TYPE_WAIT
	first_wait.unit_id = target.unit_id
	runtime.issue_command(first_wait)
	burning_entry = target.get_status_effect(&"burning")
	_assert_eq(int(burning_entry.duration) if burning_entry != null else -1, 20, "burning 不应在回合结束后递减 TU。")

	_advance_timeline_tu(runtime, state, 10)
	burning_entry = target.get_status_effect(&"burning")
	_assert_eq(int(burning_entry.duration) if burning_entry != null else -1, 10, "burning 应随时间轴推进递减剩余 TU。")

	state.phase = &"timeline_running"
	state.active_unit_id = &""
	state.timeline.ready_unit_ids.clear()
	state.timeline.ready_unit_ids.append(target.unit_id)
	runtime.advance(0.0)
	_assert_eq(target.current_hp, 16, "burning 应在第二个受影响回合继续结算同层数伤害。")
	var second_wait := BattleCommand.new()
	second_wait.command_type = BattleCommand.TYPE_WAIT
	second_wait.unit_id = target.unit_id
	runtime.issue_command(second_wait)
	_assert_true(target.has_status_effect(&"burning"), "burning 不应在第二个回合结束时被 turn end 提前清除。")
	_advance_timeline_tu(runtime, state, 10)
	_assert_true(not target.has_status_effect(&"burning"), "burning 到期后应按 TU 正式移除。")


func _test_slow_increases_move_cost_and_expires_on_tu_progress() -> void:
	var runtime := _build_runtime()
	var state := _build_state(Vector2i(5, 3))
	var source := _build_unit(&"slow_source", Vector2i(0, 1), 2)
	var target := _build_unit(&"slow_target", Vector2i(1, 1), 3)
	var enemy := _build_unit(&"slow_enemy_anchor", Vector2i(4, 1), 1)
	enemy.faction_id = &"enemy"

	_add_unit(runtime, state, source)
	_add_unit(runtime, state, target)
	_add_unit(runtime, state, enemy)
	state.ally_unit_ids = [source.unit_id, target.unit_id]
	state.enemy_unit_ids = [enemy.unit_id]
	runtime._state = state

	_apply_status(runtime, source, target, &"slow", 15)
	state.phase = &"timeline_running"
	state.active_unit_id = &""
	state.timeline.ready_unit_ids.clear()
	state.timeline.ready_unit_ids.append(target.unit_id)
	runtime.advance(0.0)
	_assert_true(target.has_status_effect(&"slow"), "slow 应在受影响单位回合开始后仍保持生效。")

	var move_command := BattleCommand.new()
	move_command.command_type = BattleCommand.TYPE_MOVE
	move_command.unit_id = target.unit_id
	move_command.target_coord = Vector2i(2, 1)
	var preview = runtime.preview_command(move_command)
	_assert_true(preview != null and preview.allowed, "slow 状态下的相邻移动仍应合法。")
	_assert_true(
		preview != null and preview.log_lines.size() > 0 and String(preview.log_lines[0]).contains("消耗 2 点行动点"),
		"slow 应把基础 1 点行动点的平地移动提升为 2 点行动点。"
	)

	runtime.issue_command(move_command)
	_assert_eq(target.current_move_points, 0, "slow 提高移动消耗后，执行移动应实际多扣 1 点行动点。")
	_assert_eq(target.current_ap, 3, "slow 只应抬高移动行动点消耗，不应继续扣除 AP。")
	var wait_command := BattleCommand.new()
	wait_command.command_type = BattleCommand.TYPE_WAIT
	wait_command.unit_id = target.unit_id
	runtime.issue_command(wait_command)
	_assert_true(
		target.has_status_effect(&"slow"),
		"slow 不应在目标回合结束后按 turn end 立刻移除。"
	)
	_advance_timeline_tu(runtime, state, 15)
	_assert_true(not target.has_status_effect(&"slow"), "slow 应在 TU 走完后移除。")


func _test_refresh_timeline_statuses_keep_single_stack_and_max_duration() -> void:
	var cases: Array[Dictionary] = [
		{"status_id": &"attack_up", "label": "attack_up"},
		{"status_id": &"archer_pre_aim", "label": "archer_pre_aim"},
		{"status_id": &"pinned", "label": "pinned"},
		{"status_id": &"taunted", "label": "taunted"},
	]
	for case_data in cases:
		var status_id: StringName = case_data.get("status_id", &"")
		var label := String(case_data.get("label", status_id))
		var first_effect := CombatEffectDef.new()
		first_effect.effect_type = &"status"
		first_effect.status_id = status_id
		first_effect.power = 1
		first_effect.duration_tu = 10

		var second_effect := CombatEffectDef.new()
		second_effect.effect_type = &"status"
		second_effect.status_id = status_id
		second_effect.power = 2
		second_effect.duration_tu = 15

		_assert_true(BattleStatusSemanticTable.has_semantic(status_id), "%s 应注册正式状态语义。" % label)
		var merged = BattleStatusSemanticTable.merge_status(first_effect, &"source_a")
		merged = BattleStatusSemanticTable.merge_status(second_effect, &"source_b", merged)
		_assert_true(merged != null, "%s 合并后应生成正式状态。" % label)
		_assert_eq(int(merged.stacks) if merged != null else -1, 1, "%s 应按 refresh 语义保持单层。" % label)
		_assert_eq(int(merged.power) if merged != null else -1, 2, "%s 应保留更高 power。" % label)
		_assert_eq(int(merged.duration) if merged != null else -1, 15, "%s 应保留更长的剩余 TU。" % label)
		_assert_eq(BattleStatusSemanticTable.get_turn_start_ap_penalty(merged), 0, "%s 不应附带 turn start AP penalty 语义。" % label)
		_assert_eq(BattleStatusSemanticTable.get_turn_start_damage(merged), 0, "%s 不应附带 turn start damage 语义。" % label)
		_assert_eq(BattleStatusSemanticTable.get_move_cost_delta(merged), 0, "%s 不应附带 move cost delta 语义。" % label)


func _test_taunted_uses_timeline_decay_without_turn_end_decay() -> void:
	var runtime := _build_runtime()
	var state := _build_state(Vector2i(4, 3))
	var source := _build_unit(&"taunted_source", Vector2i(0, 1), 2)
	var target := _build_unit(&"taunted_target", Vector2i(2, 1), 2)
	target.faction_id = &"enemy"

	_add_unit(runtime, state, source)
	_add_unit(runtime, state, target)
	state.ally_unit_ids = [source.unit_id]
	state.enemy_unit_ids = [target.unit_id]
	runtime._state = state

	_apply_status(runtime, source, target, &"taunted", 15)
	var taunted_entry = target.get_status_effect(&"taunted")
	_assert_true(taunted_entry != null, "taunted 应写入正式状态字典。")
	_assert_eq(int(taunted_entry.duration) if taunted_entry != null else -1, 15, "taunted 应记录施加时的剩余 TU。")

	state.phase = &"timeline_running"
	state.active_unit_id = &""
	state.timeline.ready_unit_ids.clear()
	state.timeline.ready_unit_ids.append(target.unit_id)
	runtime.advance(0.0)
	var wait_command := BattleCommand.new()
	wait_command.command_type = BattleCommand.TYPE_WAIT
	wait_command.unit_id = target.unit_id
	runtime.issue_command(wait_command)
	_assert_true(target.has_status_effect(&"taunted"), "taunted 不应在目标回合结束后被 turn end 提前移除。")

	_advance_timeline_tu(runtime, state, 15)
	_assert_true(not target.has_status_effect(&"taunted"), "taunted 应在 TU 走完后移除。")


func _test_status_duration_is_not_backfilled_from_semantic_defaults() -> void:
	var effect_def := CombatEffectDef.new()
	effect_def.effect_type = &"status"
	effect_def.status_id = &"pinned"
	effect_def.power = 1

	var merged = BattleStatusSemanticTable.merge_status(effect_def, &"source_unit")
	_assert_true(merged != null, "状态效果应能在缺少 duration_tu 时正常合并。")
	_assert_true(merged != null and not merged.has_duration(), "缺少来源时长时，状态不应再从语义表回填默认 TU。")


func _apply_status(
	runtime: BattleRuntimeModule,
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	status_id: StringName,
	duration_tu: int,
	power: int = 1
) -> void:
	var effect_def := CombatEffectDef.new()
	effect_def.effect_type = &"status"
	effect_def.status_id = status_id
	effect_def.power = power
	if duration_tu > 0:
		effect_def.duration_tu = duration_tu
	runtime._damage_resolver.resolve_effects(source_unit, target_unit, [effect_def])


func _build_runtime() -> BattleRuntimeModule:
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, {}, {}, {})
	return runtime


func _build_state(map_size: Vector2i) -> BattleState:
	var state := BattleState.new()
	state.battle_id = &"status_effect_semantics"
	state.phase = &"unit_acting"
	state.map_size = map_size
	state.timeline = BattleTimelineState.new()
	state.cells = {}
	for y in range(map_size.y):
		for x in range(map_size.x):
			state.cells[Vector2i(x, y)] = _build_cell(Vector2i(x, y))
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)
	return state


func _advance_timeline_tu(runtime: BattleRuntimeModule, state: BattleState, total_tu: int) -> void:
	if runtime == null or state == null or total_tu <= 0:
		return
	state.phase = &"timeline_running"
	state.active_unit_id = &""
	state.timeline.ready_unit_ids.clear()
	state.timeline.tick_interval_seconds = 1.0
	state.timeline.tu_per_tick = 5
	for unit_variant in state.units.values():
		var unit_state := unit_variant as BattleUnitState
		if unit_state != null:
			unit_state.action_threshold = 1000000
	runtime.advance(float(total_tu) / 5.0)


func _build_cell(coord: Vector2i) -> BattleCellState:
	var cell := BattleCellState.new()
	cell.coord = coord
	cell.base_terrain = BattleCellState.TERRAIN_LAND
	cell.base_height = 4
	cell.height_offset = 0
	cell.recalculate_runtime_values()
	return cell


func _build_unit(unit_id: StringName, coord: Vector2i, current_ap: int) -> BattleUnitState:
	var unit := BattleUnitState.new()
	unit.unit_id = unit_id
	unit.display_name = String(unit_id)
	unit.faction_id = &"player"
	unit.current_ap = current_ap
	unit.current_move_points = BattleUnitState.DEFAULT_MOVE_POINTS_PER_TURN
	unit.current_hp = 30
	unit.current_mp = 4
	unit.current_stamina = 4
	unit.current_aura = 0
	unit.is_alive = true
	unit.set_anchor_coord(coord)
	unit.attribute_snapshot.set_value(&"hp_max", 30)
	unit.attribute_snapshot.set_value(&"mp_max", 4)
	unit.attribute_snapshot.set_value(&"stamina_max", 4)
	unit.attribute_snapshot.set_value(&"action_points", maxi(current_ap, 1))
	return unit


func _add_unit(runtime: BattleRuntimeModule, state: BattleState, unit: BattleUnitState) -> void:
	state.units[unit.unit_id] = unit
	runtime._grid_service.place_unit(state, unit, unit.coord, true)


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s actual=%s expected=%s" % [message, str(actual), str(expected)])
