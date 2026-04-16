extends SceneTree

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/game_session.gd")
const GAME_RUNTIME_FACADE_SCRIPT = preload("res://scripts/systems/game_runtime_facade.gd")
const BATTLE_BOARD_SCENE = preload("res://scenes/ui/battle_board_2d.tscn")
const BattleBoard2D = preload("res://scripts/ui/battle_board_2d.gd")
const BATTLE_STATE_SCRIPT = preload("res://scripts/systems/battle_state.gd")
const BATTLE_TIMELINE_STATE_SCRIPT = preload("res://scripts/systems/battle_timeline_state.gd")
const BATTLE_CELL_STATE_SCRIPT = preload("res://scripts/systems/battle_cell_state.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle_unit_state.gd")
const BATTLE_COMMAND_SCRIPT = preload("res://scripts/systems/battle_command.gd")

const TEST_WORLD_CONFIG := "res://data/configs/world_map/test_world_map_config.tres"

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_battle_unit_state_serialization_exposes_aura()
	_test_facade_clicking_active_unit_casts_self_skill()
	_test_facade_multi_unit_selection_tracks_target_unit_ids()
	await _test_facade_ground_aoe_selection_highlight_preview_and_execution_share_range()
	_test_facade_stamina_skill_updates_battle_state_snapshot_and_logs()
	_test_facade_aura_skill_updates_battle_state_snapshot_and_logs()
	_test_facade_selected_aura_skill_returns_formal_error_after_aura_drops()
	_test_facade_cooldown_skill_reduces_after_battle_tick()
	if _failures.is_empty():
		print("Battle skill protocol regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle skill protocol regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_facade_clicking_active_unit_casts_self_skill() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)

	var state: BattleState = _build_flat_state(Vector2i(4, 2))
	var caster: BattleUnitState = _build_manual_unit(
		&"self_cast_user",
		"自施法者",
		&"player",
		Vector2i(1, 0),
		[&"mage_arcane_orbit"],
		2,
		6
	)
	var enemy: BattleUnitState = _build_manual_unit(
		&"self_cast_enemy",
		"敌人",
		&"enemy",
		Vector2i(3, 0),
		[],
		2,
		0
	)
	_add_unit_to_state(facade, state, caster, false)
	_add_unit_to_state(facade, state, enemy, true)
	state.phase = &"unit_acting"
	state.active_unit_id = caster.unit_id
	_apply_battle_state(facade, state)

	var before_hp: int = int(caster.current_hp)
	var before_mp: int = int(caster.current_mp)
	var select_result: Dictionary = facade.command_battle_select_skill(0)
	_assert_true(bool(select_result.get("ok", false)), "选择自施法技能应返回成功结果。")
	var cast_result: Dictionary = facade.command_battle_move_to(caster.coord)
	_assert_true(bool(cast_result.get("ok", false)), "点击自身坐标施法应返回成功结果。")
	var battle_snapshot: Dictionary = facade.build_headless_snapshot().get("battle", {})

	_assert_eq(caster.current_mp, before_mp - 2, "点击自身后应真正施放自施法技能并扣除法力。")
	_assert_true(caster.current_hp < before_hp, "当前自施法回归夹具应能观测到技能已真实结算。")
	_assert_eq(
		_extract_coord_pairs(battle_snapshot.get("selected_target_coords", [])),
		[],
		"自施法结算后不应残留已选目标坐标。"
	)

	_cleanup_test_session(game_session)


func _test_facade_multi_unit_selection_tracks_target_unit_ids() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return

	var skill_def = game_session.get_skill_defs().get(&"mage_arcane_missile")
	_assert_true(skill_def != null and skill_def.combat_profile != null, "多目标协议回归前置：mage_arcane_missile 定义应存在。")
	if skill_def == null or skill_def.combat_profile == null:
		_cleanup_test_session(game_session)
		return
	skill_def.combat_profile.min_target_count = 2
	skill_def.combat_profile.max_target_count = 2

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)

	var state: BattleState = _build_flat_state(Vector2i(5, 2))
	var caster: BattleUnitState = _build_manual_unit(
		&"multi_unit_user",
		"多目标施法者",
		&"player",
		Vector2i(0, 0),
		[&"mage_arcane_missile"],
		2,
		6
	)
	var enemy_a: BattleUnitState = _build_manual_unit(&"enemy_a", "敌人A", &"enemy", Vector2i(2, 0), [], 2, 0)
	var enemy_b: BattleUnitState = _build_manual_unit(&"enemy_b", "敌人B", &"enemy", Vector2i(3, 0), [], 2, 0)
	var enemy_c: BattleUnitState = _build_manual_unit(&"enemy_c", "敌人C", &"enemy", Vector2i(4, 0), [], 2, 0)
	_add_unit_to_state(facade, state, caster, false)
	_add_unit_to_state(facade, state, enemy_a, true)
	_add_unit_to_state(facade, state, enemy_b, true)
	_add_unit_to_state(facade, state, enemy_c, true)
	state.phase = &"unit_acting"
	state.active_unit_id = caster.unit_id
	_apply_battle_state(facade, state)

	var select_result: Dictionary = facade.command_battle_select_skill(0)
	_assert_true(bool(select_result.get("ok", false)), "选择多目标技能应返回成功结果。")
	facade.command_battle_move_to(enemy_b.coord)
	var queued_snapshot: Dictionary = facade.build_headless_snapshot().get("battle", {})
	_assert_eq(
		_extract_string_array(queued_snapshot.get("selected_target_unit_ids", [])),
		["enemy_b"],
		"首个单位目标应按点击顺序写入 battle snapshot。"
	)
	_assert_eq(
		_extract_coord_pairs(queued_snapshot.get("selected_target_coords", [])),
		[[enemy_b.coord.x, enemy_b.coord.y]],
		"单位多选阶段应把已选单位坐标同步暴露给界面快照。"
	)

	facade.command_battle_move_to(enemy_a.coord)
	var after_cast_snapshot: Dictionary = facade.build_headless_snapshot().get("battle", {})
	_assert_true(enemy_b.current_hp < 30, "多目标技能结算后应命中第一个已选单位。")
	_assert_true(enemy_a.current_hp < 30, "多目标技能结算后应命中第二个已选单位。")
	_assert_eq(enemy_c.current_hp, 30, "未被选中的单位不应受到多目标技能影响。")
	_assert_eq(
		_extract_string_array(after_cast_snapshot.get("selected_target_unit_ids", [])),
		[],
		"多目标技能结算后，battle snapshot 不应残留已选单位目标。"
	)

	_cleanup_test_session(game_session)


func _test_facade_ground_aoe_selection_highlight_preview_and_execution_share_range() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)

	var state: BattleState = _build_flat_state(Vector2i(5, 5))
	var caster: BattleUnitState = _build_manual_unit(
		&"radius_skill_user",
		"范围施法者",
		&"player",
		Vector2i(2, 2),
		[&"mage_cold_snap"],
		2,
		6
	)
	var enemy_top: BattleUnitState = _build_manual_unit(&"radius_enemy_top", "敌人上", &"enemy", Vector2i(2, 0), [], 2, 0)
	var enemy_left: BattleUnitState = _build_manual_unit(&"radius_enemy_left", "敌人左", &"enemy", Vector2i(1, 1), [], 2, 0)
	var enemy_center: BattleUnitState = _build_manual_unit(&"radius_enemy_center", "敌人中", &"enemy", Vector2i(2, 1), [], 2, 0)
	var enemy_right: BattleUnitState = _build_manual_unit(&"radius_enemy_right", "敌人右", &"enemy", Vector2i(3, 1), [], 2, 0)
	var enemy_far: BattleUnitState = _build_manual_unit(&"radius_enemy_far", "敌人远", &"enemy", Vector2i(4, 4), [], 2, 0)
	_add_unit_to_state(facade, state, caster, false)
	_add_unit_to_state(facade, state, enemy_top, true)
	_add_unit_to_state(facade, state, enemy_left, true)
	_add_unit_to_state(facade, state, enemy_center, true)
	_add_unit_to_state(facade, state, enemy_right, true)
	_add_unit_to_state(facade, state, enemy_far, true)
	state.phase = &"unit_acting"
	state.active_unit_id = caster.unit_id
	_apply_battle_state(facade, state)

	var select_result: Dictionary = facade.command_battle_select_skill(0)
	_assert_true(bool(select_result.get("ok", false)), "选择 radius 范围技能应返回成功结果。")
	var target_coord := Vector2i(2, 1)
	facade.set_runtime_battle_selected_coord(target_coord)
	facade.set_battle_selection_target_coords_state([target_coord])

	var selected_target_coords := facade.get_selected_battle_skill_target_coords()
	_assert_true(
		selected_target_coords.size() > 1 and selected_target_coords.has(target_coord),
		"radius 范围技能的 selection 读面应暴露包含目标中心在内的正式多格范围。"
	)

	var battle_snapshot: Dictionary = facade.build_headless_snapshot().get("battle", {})
	_assert_eq(
		_extract_coord_pairs(battle_snapshot.get("selected_target_coords", [])),
		_extract_vector2i_pairs(selected_target_coords),
		"battle snapshot 应把同一范围结果原样暴露给 HUD/棋盘高亮。"
	)

	var preview_command = BATTLE_COMMAND_SCRIPT.new()
	preview_command.command_type = BATTLE_COMMAND_SCRIPT.TYPE_SKILL
	preview_command.unit_id = caster.unit_id
	preview_command.skill_id = &"mage_cold_snap"
	preview_command.target_coord = target_coord
	var preview = facade.preview_battle_command(preview_command)
	_assert_true(preview != null and preview.allowed, "radius 范围技能前置：preview_command 应允许测试目标。")
	if preview != null:
		_assert_eq(
			_extract_vector2i_pairs(preview.target_coords),
			_extract_vector2i_pairs(selected_target_coords),
			"合法性校验 / preview 应复用与 selection 相同的范围结果。"
		)

	var board := await _instantiate_battle_board()
	board.configure(
		state,
		target_coord,
		selected_target_coords,
		facade.get_battle_overlay_target_coords()
	)
	await process_frame
	_assert_eq(
		_extract_vector2i_pairs(_collect_marker_used_coords(board)),
		_extract_vector2i_pairs(selected_target_coords),
		"棋盘高亮应使用与 selection 相同的范围结果。"
	)
	board.queue_free()
	await process_frame

	var caster_hp_before := caster.current_hp
	var enemy_top_hp_before := enemy_top.current_hp
	var enemy_left_hp_before := enemy_left.current_hp
	var enemy_center_hp_before := enemy_center.current_hp
	var enemy_right_hp_before := enemy_right.current_hp
	var enemy_far_hp_before := enemy_far.current_hp
	var execute_refresh := String(facade.issue_battle_command(preview_command))
	_assert_eq(execute_refresh, "full", "执行范围技能命令后应触发完整战斗刷新。")
	_assert_true(enemy_top.current_hp < enemy_top_hp_before, "范围内顶部敌人应受到实际结算影响。")
	_assert_true(enemy_left.current_hp < enemy_left_hp_before, "范围内左侧敌人应受到实际结算影响。")
	_assert_true(enemy_center.current_hp < enemy_center_hp_before, "范围内中心敌人应受到实际结算影响。")
	_assert_true(enemy_right.current_hp < enemy_right_hp_before, "范围内右侧敌人应受到实际结算影响。")
	_assert_eq(enemy_far.current_hp, enemy_far_hp_before, "范围外敌人不应被误伤。")
	_assert_eq(caster.current_hp, caster_hp_before, "敌对范围技能不应误伤施法者自身。")
	_assert_eq(
		_extract_coord_pairs(facade.build_headless_snapshot().get("battle", {}).get("selected_target_coords", [])),
		[],
		"范围技能结算后不应残留已选范围坐标。"
	)

	_cleanup_test_session(game_session)


func _test_battle_unit_state_serialization_exposes_aura() -> void:
	var unit := BATTLE_UNIT_STATE_SCRIPT.new()
	unit.unit_id = &"aura_state_user"
	unit.current_aura = 3
	unit.attribute_snapshot.set_value(&"aura_max", 5)

	var payload := unit.to_dict()
	var restored = BATTLE_UNIT_STATE_SCRIPT.from_dict(payload) as BattleUnitState

	_assert_eq(int(payload.get("current_aura", -1)), 3, "BattleUnitState.to_dict() 应稳定暴露 current_aura。")
	_assert_eq(int(payload.get("aura_max", -1)), 5, "BattleUnitState.to_dict() 应稳定暴露 aura_max。")
	_assert_true(restored != null, "BattleUnitState.from_dict() 应能恢复 Aura 字段。")
	_assert_eq(restored.current_aura if restored != null else -1, 3, "BattleUnitState.from_dict() 应恢复 current_aura。")
	_assert_eq(restored.get_aura_max() if restored != null else -1, 5, "BattleUnitState.from_dict() 应恢复 aura_max。")


func _test_facade_stamina_skill_updates_battle_state_snapshot_and_logs() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)

	var state: BattleState = _build_flat_state(Vector2i(3, 1))
	var caster: BattleUnitState = _build_manual_unit(
		&"stamina_cost_user",
		"耐力施法者",
		&"player",
		Vector2i(0, 0),
		[&"archer_long_draw"],
		2,
		0
	)
	caster.current_stamina = 12
	caster.attribute_snapshot.set_value(&"stamina_max", 12)
	var enemy: BattleUnitState = _build_manual_unit(
		&"stamina_cost_enemy",
		"敌人",
		&"enemy",
		Vector2i(1, 0),
		[],
		2,
		0
	)
	_add_unit_to_state(facade, state, caster, false)
	_add_unit_to_state(facade, state, enemy, true)
	state.phase = &"unit_acting"
	state.active_unit_id = caster.unit_id
	_apply_battle_state(facade, state)

	var select_result: Dictionary = facade.command_battle_select_skill(0)
	_assert_true(bool(select_result.get("ok", false)), "选择 stamina 技能应返回成功结果。")
	var cast_result: Dictionary = facade.command_battle_move_to(enemy.coord)
	_assert_true(bool(cast_result.get("ok", false)), "执行 stamina 技能应返回成功结果。")

	var runtime_state := facade.get_battle_state()
	var runtime_caster := runtime_state.units.get(caster.unit_id) as BattleUnitState if runtime_state != null else null
	var battle_snapshot: Dictionary = facade.build_headless_snapshot().get("battle", {})
	var caster_snapshot := _find_battle_unit_snapshot(battle_snapshot, String(caster.unit_id))
	var text_snapshot := facade.build_text_snapshot()
	var move_log := _find_log_entry(facade.get_log_snapshot(), "battle.move_to")
	var logged_units: Array = move_log.get("context", {}).get("after", {}).get("battle", {}).get("units", [])
	var logged_caster := _find_unit_entry(logged_units, String(caster.unit_id))

	_assert_true(runtime_caster != null, "stamina 回归中应能从 battle state 读取施法者单位。")
	_assert_eq(runtime_caster.current_stamina if runtime_caster != null else -1, 10, "技能释放后 battle state 应正式扣除 stamina。")
	_assert_eq(int(caster_snapshot.get("current_stamina", -1)), 10, "battle snapshot 应稳定暴露扣费后的 current_stamina。")
	_assert_eq(int(caster_snapshot.get("stamina_max", -1)), 12, "battle snapshot 应稳定暴露 stamina_max。")
	_assert_true(text_snapshot.contains("unit=stamina_cost_user |"), "battle 文本快照应渲染 stamina 施法者单位行。")
	_assert_true(text_snapshot.contains("st=10"), "battle 文本快照应渲染扣费后的 stamina。")
	_assert_eq(int(logged_caster.get("current_stamina", -1)), 10, "战斗命令日志后态也应暴露扣费后的 current_stamina。")

	_cleanup_test_session(game_session)


func _test_facade_aura_skill_updates_battle_state_snapshot_and_logs() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)

	var state: BattleState = _build_flat_state(Vector2i(3, 1))
	var caster: BattleUnitState = _build_manual_unit(
		&"aura_cost_user",
		"斗气施法者",
		&"player",
		Vector2i(0, 0),
		[&"warrior_aura_slash"],
		2,
		0
	)
	caster.current_aura = 2
	caster.attribute_snapshot.set_value(&"aura_max", 2)
	var enemy: BattleUnitState = _build_manual_unit(
		&"aura_cost_enemy",
		"敌人",
		&"enemy",
		Vector2i(1, 0),
		[],
		2,
		0
	)
	_add_unit_to_state(facade, state, caster, false)
	_add_unit_to_state(facade, state, enemy, true)
	state.phase = &"unit_acting"
	state.active_unit_id = caster.unit_id
	_apply_battle_state(facade, state)

	var select_result: Dictionary = facade.command_battle_select_skill(0)
	_assert_true(bool(select_result.get("ok", false)), "选择 aura 技能应返回成功结果。")
	var cast_result: Dictionary = facade.command_battle_move_to(enemy.coord)
	_assert_true(bool(cast_result.get("ok", false)), "执行 aura 技能应返回成功结果。")

	var runtime_state := facade.get_battle_state()
	var runtime_caster := runtime_state.units.get(caster.unit_id) as BattleUnitState if runtime_state != null else null
	var battle_snapshot: Dictionary = facade.build_headless_snapshot().get("battle", {})
	var caster_snapshot := _find_battle_unit_snapshot(battle_snapshot, String(caster.unit_id))
	var text_snapshot := facade.build_text_snapshot()
	var move_log := _find_log_entry(facade.get_log_snapshot(), "battle.move_to")
	var logged_units: Array = move_log.get("context", {}).get("after", {}).get("battle", {}).get("units", [])
	var logged_caster := _find_unit_entry(logged_units, String(caster.unit_id))

	_assert_true(runtime_caster != null, "aura 回归中应能从 battle state 读取施法者单位。")
	_assert_eq(runtime_caster.current_aura if runtime_caster != null else -1, 1, "技能释放后 battle state 应正式扣除 aura。")
	_assert_eq(int(caster_snapshot.get("current_aura", -1)), 1, "battle snapshot 应稳定暴露扣费后的 current_aura。")
	_assert_eq(int(caster_snapshot.get("aura_max", -1)), 2, "battle snapshot 应稳定暴露 aura_max。")
	_assert_true(text_snapshot.contains("unit=aura_cost_user |"), "battle 文本快照应渲染 aura 施法者单位行。")
	_assert_true(text_snapshot.contains("au=1/2"), "battle 文本快照应渲染扣费后的 aura。")
	_assert_eq(int(logged_caster.get("current_aura", -1)), 1, "战斗命令日志后态也应暴露扣费后的 current_aura。")

	_cleanup_test_session(game_session)


func _test_facade_selected_aura_skill_returns_formal_error_after_aura_drops() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)

	var state: BattleState = _build_flat_state(Vector2i(3, 1))
	var caster: BattleUnitState = _build_manual_unit(
		&"aura_runtime_block_user",
		"Aura 运行时阻断者",
		&"player",
		Vector2i(0, 0),
		[&"warrior_aura_slash"],
		2,
		0
	)
	caster.current_aura = 1
	caster.attribute_snapshot.set_value(&"aura_max", 1)
	var enemy: BattleUnitState = _build_manual_unit(
		&"aura_runtime_block_enemy",
		"敌人",
		&"enemy",
		Vector2i(1, 0),
		[],
		2,
		0
	)
	_add_unit_to_state(facade, state, caster, false)
	_add_unit_to_state(facade, state, enemy, true)
	state.phase = &"unit_acting"
	state.active_unit_id = caster.unit_id
	_apply_battle_state(facade, state)

	var select_result: Dictionary = facade.command_battle_select_skill(0)
	_assert_true(bool(select_result.get("ok", false)), "Aura 运行时阻断回归前置：选择技能应先成功。")
	var enemy_hp_before := enemy.current_hp
	caster.current_aura = 0
	facade.refresh_battle_selection_state()

	var cast_result: Dictionary = facade.command_battle_move_to(enemy.coord)
	_assert_true(not bool(cast_result.get("ok", true)), "Aura 在点击前耗尽时，battle.move_to 应返回正式失败。")
	_assert_true(String(cast_result.get("message", "")).contains("斗气不足"), "Aura 运行时阻断应沿正式命令结果返回明确原因。")
	_assert_eq(enemy.current_hp, enemy_hp_before, "Aura 不足导致施法失败时，不应继续结算伤害。")
	_assert_eq(caster.current_aura, 0, "Aura 不足导致施法失败时，不应继续扣费。")
	_assert_eq(String(facade.get_selected_battle_skill_id()), "warrior_aura_slash", "Aura 运行时阻断后应保留当前技能选择，等待资源恢复或手动清除。")

	_cleanup_test_session(game_session)


func _test_facade_cooldown_skill_reduces_after_battle_tick() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)

	var state: BattleState = _build_flat_state(Vector2i(3, 1))
	state.timeline.tick_interval_seconds = 1.0
	state.timeline.tu_per_tick = 1
	state.timeline.action_threshold = 100
	var caster: BattleUnitState = _build_manual_unit(
		&"aa_cooldown_tick_user",
		"冷却施法者",
		&"player",
		Vector2i(0, 0),
		[&"archer_long_draw"],
		2,
		0
	)
	caster.current_stamina = 12
	var enemy: BattleUnitState = _build_manual_unit(
		&"zz_cooldown_tick_enemy",
		"敌人",
		&"enemy",
		Vector2i(1, 0),
		[],
		2,
		0
	)
	_add_unit_to_state(facade, state, caster, false)
	_add_unit_to_state(facade, state, enemy, true)
	state.phase = &"unit_acting"
	state.active_unit_id = caster.unit_id
	_apply_battle_state(facade, state)

	var select_result: Dictionary = facade.command_battle_select_skill(0)
	_assert_true(bool(select_result.get("ok", false)), "选择 cooldown 技能应返回成功结果。")
	var cast_result: Dictionary = facade.command_battle_move_to(enemy.coord)
	_assert_true(bool(cast_result.get("ok", false)), "执行 cooldown 技能应返回成功结果。")
	_assert_eq(int(caster.cooldowns.get(&"archer_long_draw", 0)), 3, "技能释放后应写入基础 cooldown。")

	var tick_result: Dictionary = facade.command_battle_tick(1.0, 1.0)
	_assert_true(bool(tick_result.get("ok", false)), "battle tick 应能成功推进 cooldown。")

	var runtime_state := facade.get_battle_state()
	var runtime_caster := runtime_state.units.get(caster.unit_id) as BattleUnitState if runtime_state != null else null
	var battle_snapshot: Dictionary = facade.build_headless_snapshot().get("battle", {})
	var hud: Dictionary = battle_snapshot.get("hud", {})
	var skill_slots: Array = hud.get("skill_slots", [])
	var first_slot: Dictionary = skill_slots[0] if not skill_slots.is_empty() and skill_slots[0] is Dictionary else {}

	_assert_true(runtime_caster != null, "cooldown tick 回归中应能从 battle state 读取施法者单位。")
	_assert_eq(int(runtime_state.timeline.current_tu) if runtime_state != null and runtime_state.timeline != null else -1, 1, "battle tick 后 current_tu 应按配置推进 1。")
	_assert_eq(int(runtime_caster.cooldowns.get(&"archer_long_draw", 0)) if runtime_caster != null else -1, 2, "TU 推进后的下一行动窗口应把 cooldown 正式递减为 2。")
	_assert_eq(String(runtime_state.active_unit_id) if runtime_state != null else "", String(caster.unit_id), "冷却递减后应轮到施法者重新进入行动窗口。")
	_assert_eq(int(first_slot.get("cooldown", -1)), 2, "HUD skill slot 应展示递减后的 cooldown。")
	_assert_eq(String(first_slot.get("footer_text", "")), "CD 2", "HUD skill slot footer 应同步显示新的 cooldown 文案。")
	_assert_true(bool(first_slot.get("is_disabled", false)), "冷却未结束前 HUD skill slot 应保持禁用。")

	_cleanup_test_session(game_session)


func _create_test_session():
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_true(create_error == OK, "GameSession 应能基于测试世界配置创建新存档。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return null
	return game_session


func _cleanup_test_session(game_session) -> void:
	if game_session == null:
		return
	game_session.clear_persisted_game()
	game_session.free()


func _instantiate_battle_board() -> BattleBoard2D:
	var board := BATTLE_BOARD_SCENE.instantiate() as BattleBoard2D
	root.add_child(board)
	await process_frame
	board.set_viewport_size(Vector2(1280.0, 720.0))
	return board


func _build_flat_state(map_size: Vector2i) -> BattleState:
	var state: BattleState = BATTLE_STATE_SCRIPT.new()
	state.battle_id = &"battle_skill_protocol"
	state.phase = &"timeline_running"
	state.map_size = map_size
	state.timeline = BATTLE_TIMELINE_STATE_SCRIPT.new()
	for y in range(map_size.y):
		for x in range(map_size.x):
			var cell = BATTLE_CELL_STATE_SCRIPT.new()
			cell.coord = Vector2i(x, y)
			cell.base_terrain = BATTLE_CELL_STATE_SCRIPT.TERRAIN_LAND
			cell.base_height = 4
			cell.height_offset = 0
			cell.recalculate_runtime_values()
			state.cells[cell.coord] = cell
	state.cell_columns = BATTLE_CELL_STATE_SCRIPT.build_columns_from_surface_cells(state.cells)
	return state


func _build_manual_unit(
	unit_id: StringName,
	display_name: String,
	faction_id: StringName,
	coord: Vector2i,
	skill_ids: Array[StringName],
	current_ap: int,
	current_mp: int
) -> BattleUnitState:
	var unit: BattleUnitState = BATTLE_UNIT_STATE_SCRIPT.new()
	unit.unit_id = unit_id
	unit.display_name = display_name
	unit.faction_id = faction_id
	unit.control_mode = &"manual"
	unit.current_hp = 30
	unit.current_mp = current_mp
	unit.current_ap = current_ap
	unit.current_stamina = 20
	unit.is_alive = true
	unit.set_anchor_coord(coord)
	unit.attribute_snapshot.set_value(&"hp_max", 30)
	unit.attribute_snapshot.set_value(&"mp_max", maxi(current_mp, 6))
	unit.attribute_snapshot.set_value(&"action_points", maxi(current_ap, 2))
	unit.attribute_snapshot.set_value(&"physical_attack", 10)
	unit.attribute_snapshot.set_value(&"magic_attack", 12)
	unit.attribute_snapshot.set_value(&"physical_defense", 4)
	unit.attribute_snapshot.set_value(&"magic_defense", 4)
	unit.attribute_snapshot.set_value(&"speed", 10)
	unit.known_active_skill_ids = skill_ids.duplicate()
	for skill_id in unit.known_active_skill_ids:
		unit.known_skill_level_map[skill_id] = 1
	return unit


func _add_unit_to_state(facade, state: BattleState, unit: BattleUnitState, is_enemy: bool) -> void:
	state.units[unit.unit_id] = unit
	if is_enemy:
		state.enemy_unit_ids.append(unit.unit_id)
	else:
		state.ally_unit_ids.append(unit.unit_id)
	var placed: bool = bool(facade._battle_runtime._grid_service.place_unit(state, unit, unit.coord, true))
	_assert_true(placed, "测试单位 %s 应能成功放入战场。" % String(unit.unit_id))


func _apply_battle_state(facade, state: BattleState) -> void:
	facade._battle_runtime._state = state
	facade._battle_state = state
	facade._battle_selected_coord = Vector2i(-1, -1)
	facade._refresh_battle_runtime_state()


func _extract_string_array(values: Array) -> Array[String]:
	var result: Array[String] = []
	for value in values:
		result.append(String(value))
	return result


func _extract_coord_pairs(coord_dicts: Array) -> Array:
	var pairs: Array = []
	for coord_variant in coord_dicts:
		if coord_variant is not Dictionary:
			continue
		var coord: Dictionary = coord_variant
		pairs.append([int(coord.get("x", 0)), int(coord.get("y", 0))])
	return pairs


func _extract_vector2i_pairs(coords: Array) -> Array:
	var pairs: Array = []
	for coord_variant in coords:
		if coord_variant is not Vector2i:
			continue
		var coord: Vector2i = coord_variant
		pairs.append([coord.x, coord.y])
	return pairs


func _collect_marker_used_coords(board: BattleBoard2D) -> Array[Vector2i]:
	var coord_set: Dictionary = {}
	if board == null:
		return []
	for layer in board.marker_layers:
		if layer == null:
			continue
		for coord in layer.get_used_cells():
			coord_set[coord] = true
	var coords: Array[Vector2i] = []
	for coord_variant in coord_set.keys():
		if coord_variant is Vector2i:
			coords.append(coord_variant)
	coords.sort_custom(func(a: Vector2i, b: Vector2i) -> bool:
		if a.y == b.y:
			return a.x < b.x
		return a.y < b.y
	)
	return coords


func _find_battle_unit_snapshot(battle_snapshot: Dictionary, unit_id: String) -> Dictionary:
	return _find_unit_entry(battle_snapshot.get("units", []), unit_id)


func _find_unit_entry(unit_variants: Variant, unit_id: String) -> Dictionary:
	if unit_variants is not Array:
		return {}
	for unit_variant in unit_variants:
		if unit_variant is not Dictionary:
			continue
		var unit_entry: Dictionary = unit_variant
		if String(unit_entry.get("unit_id", "")) == unit_id:
			return unit_entry.duplicate(true)
	return {}


func _find_log_entry(log_snapshot: Dictionary, event_id: String) -> Dictionary:
	var entries: Array = log_snapshot.get("entries", [])
	for entry_variant in entries:
		if entry_variant is not Dictionary:
			continue
		var entry: Dictionary = entry_variant
		if String(entry.get("event_id", "")) == event_id:
			return entry.duplicate(true)
	return {}


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
