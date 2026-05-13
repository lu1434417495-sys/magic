extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")
const BattleRuntimeTestHelpers = preload("res://tests/shared/battle_runtime_test_helpers.gd")

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/persistence/game_session.gd")
const GAME_RUNTIME_FACADE_SCRIPT = preload("res://scripts/systems/game_runtime/game_runtime_facade.gd")
const GAME_RUNTIME_BATTLE_SELECTION_SCRIPT = preload("res://scripts/systems/game_runtime/game_runtime_battle_selection.gd")
const BATTLE_COMMAND_SCRIPT = preload("res://scripts/systems/battle/core/battle_command.gd")
const BATTLE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_state.gd")
const BATTLE_TIMELINE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_timeline_state.gd")
const BATTLE_CELL_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")

const TEST_WORLD_CONFIG := "res://data/configs/world_map/test_world_map_config.tres"

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_selection_sidecar_tracks_multi_unit_targets()
	_test_selection_sidecar_reuses_shared_line_cone_radius_and_self_target_collection()
	_test_selection_sidecar_executes_multistep_reachable_movement()
	_test_selection_sidecar_hides_targets_for_stamina_blocked_skill()
	_test_selection_sidecar_hides_targets_for_cooldown_blocked_skill()
	_test_selection_sidecar_hides_targets_for_aura_blocked_skill()
	_test_selection_sidecar_focuses_caster_when_multi_unit_confirm_ready()
	_test_preview_command_rejects_when_battle_modal_blocks_interaction()
	_test_selection_read_faces_hide_targets_when_battle_modal_blocks_interaction()
	_test_battle_commands_and_proxy_reject_when_battle_modal_blocks_interaction()
	_test_promotion_modal_pauses_battle_timeline()
	_test_character_info_blocks_commands_without_pausing_battle_timeline()

	if _failures.is_empty():
		print("Game runtime battle selection regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Game runtime battle selection regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_selection_sidecar_tracks_multi_unit_targets() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return

	var skill_def = game_session.get_skill_defs().get(&"mage_arcane_missile")
	_assert_true(skill_def != null and skill_def.combat_profile != null, "多目标回归前置：mage_arcane_missile 定义应存在。")
	if skill_def == null or skill_def.combat_profile == null:
		_cleanup_test_session(game_session)
		return
	skill_def.combat_profile.min_target_count = 2
	skill_def.combat_profile.max_target_count = 2
	skill_def.combat_profile.level_overrides = {}

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)
	var selection = GAME_RUNTIME_BATTLE_SELECTION_SCRIPT.new()
	selection.setup(facade)

	var state: BattleState = _build_flat_state(Vector2i(5, 2))
	var caster: BattleUnitState = _build_manual_unit(
		&"multi_unit_user",
		"多目标施法者",
		&"player",
		Vector2i(0, 0),
		[&"mage_arcane_missile"],
		2,
		120
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

	selection.select_battle_skill_slot(0)
	_assert_eq(String(facade.get_selected_battle_skill_id()), "mage_arcane_missile", "选择技能后 facade 应同步记录选中的技能 ID。")

	var first_click_mode := String(selection.attempt_battle_move_to(enemy_b.coord))
	_assert_eq(first_click_mode, "overlay", "首个单位目标选择阶段应保持 overlay 刷新。")
	_assert_eq(
		_extract_string_array(selection.get_selected_battle_skill_target_unit_ids()),
		["enemy_b"],
		"Selection sidecar 应跟踪首个已选单位目标。"
	)
	_assert_eq(
		_extract_coord_pairs(selection.get_selected_battle_skill_target_coords()),
		[[enemy_b.coord.x, enemy_b.coord.y]],
		"Selection sidecar 应同步暴露首个已选单位的坐标。"
	)
	_assert_true(
		_extract_coord_pairs(selection.get_selected_battle_skill_valid_target_coords()).has([enemy_a.coord.x, enemy_a.coord.y]),
		"首个目标入队后，剩余合法目标中应仍包含第二个敌人。"
	)

	var second_click_mode := String(selection.attempt_battle_move_to(enemy_a.coord))
	_assert_eq(second_click_mode, "full", "达到最小目标数并完成施法后应返回 full 刷新。")
	_assert_true(enemy_b.current_hp < 30, "多目标技能结算后应命中第一个已选单位。")
	_assert_true(enemy_a.current_hp < 30, "多目标技能结算后应命中第二个已选单位。")
	_assert_eq(enemy_c.current_hp, 30, "未被选中的单位不应受到多目标技能影响。")
	_assert_eq(
		_extract_string_array(selection.get_selected_battle_skill_target_unit_ids()),
		[],
		"多目标技能结算后不应残留单位目标队列。"
	)

	_cleanup_test_session(game_session)


func _test_selection_sidecar_reuses_shared_line_cone_radius_and_self_target_collection() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return

	_assert_selection_sidecar_matches_ground_preview_target_collection(
		game_session,
		&"mage_flame_spear",
		120,
		20,
		Vector2i(2, 1),
		"line"
	)
	_assert_selection_sidecar_matches_ground_preview_target_collection(
		game_session,
		&"warrior_sweeping_slash",
		0,
		40,
		Vector2i(2, 1),
		"cone"
	)
	_assert_selection_sidecar_matches_ground_preview_target_collection(
		game_session,
		&"mage_cold_snap",
		120,
		20,
		Vector2i(2, 1),
		"radius"
	)
	_assert_selection_sidecar_matches_self_preview_target_collection(
		game_session,
		&"mage_arcane_orbit",
		120,
		20,
		"self"
	)

	_cleanup_test_session(game_session)


func _test_selection_sidecar_executes_multistep_reachable_movement() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)
	var selection = GAME_RUNTIME_BATTLE_SELECTION_SCRIPT.new()
	selection.setup(facade)

	var state: BattleState = _build_flat_state(Vector2i(4, 2))
	var mover: BattleUnitState = _build_manual_unit(
		&"multistep_move_user",
		"多步移动者",
		&"player",
		Vector2i(0, 0),
		[],
		2,
		0
	)
	_add_unit_to_state(facade, state, mover, false)
	state.phase = &"unit_acting"
	state.active_unit_id = mover.unit_id
	_apply_battle_state(facade, state)

	var click_mode := String(selection.attempt_battle_move_to(Vector2i(1, 1)))
	_assert_eq(click_mode, "full", "点击两步内可达蓝色地格后应触发完整战斗刷新。")
	_assert_eq(mover.coord, Vector2i(1, 1), "点击两步内可达蓝色地格后应真正移动到目标终点。")
	_assert_eq(mover.current_move_points, 0, "移动成功后应耗尽本回合移动力。")
	_assert_eq(mover.current_ap, 2, "普通移动改走行动点后，不应再扣除 AP。")

	_cleanup_test_session(game_session)


func _test_selection_sidecar_hides_targets_for_stamina_blocked_skill() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return

	var skill_def = game_session.get_skill_defs().get(&"archer_long_draw")
	_assert_true(skill_def != null and skill_def.combat_profile != null, "耐力阻断回归前置：archer_long_draw 定义应存在。")
	if skill_def == null or skill_def.combat_profile == null:
		_cleanup_test_session(game_session)
		return

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)
	var selection = GAME_RUNTIME_BATTLE_SELECTION_SCRIPT.new()
	selection.setup(facade)

	var state: BattleState = _build_flat_state(Vector2i(4, 2))
	var caster: BattleUnitState = _build_manual_unit(
		&"blocked_skill_user",
		"资源不足施法者",
		&"player",
		Vector2i(0, 0),
		[&"archer_long_draw"],
		2,
		0
	)
	caster.current_stamina = 1
	caster.attribute_snapshot.set_value(&"stamina_max", 2)
	var enemy_a: BattleUnitState = _build_manual_unit(&"blocked_enemy_a", "敌人A", &"enemy", Vector2i(2, 0), [], 2, 0)
	var enemy_b: BattleUnitState = _build_manual_unit(&"blocked_enemy_b", "敌人B", &"enemy", Vector2i(3, 0), [], 2, 0)
	_add_unit_to_state(facade, state, caster, false)
	_add_unit_to_state(facade, state, enemy_a, true)
	_add_unit_to_state(facade, state, enemy_b, true)
	state.phase = &"unit_acting"
	state.active_unit_id = caster.unit_id
	_apply_battle_state(facade, state)

	selection.select_battle_skill_slot(0)
	_assert_eq(String(facade.get_selected_battle_skill_id()), "", "耐力不足时不应把技能写入选中状态。")
	_assert_eq(
		_extract_coord_pairs(selection.get_selected_battle_skill_valid_target_coords()),
		[],
		"耐力不足时，不应继续高亮任何合法目标。"
	)
	_assert_true(
		String(facade.get_status_text()).contains("体力不足"),
		"耐力不足时，状态文案应直接说明阻断原因。"
	)

	_cleanup_test_session(game_session)


func _test_selection_sidecar_hides_targets_for_cooldown_blocked_skill() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return

	var skill_def = game_session.get_skill_defs().get(&"archer_long_draw")
	_assert_true(skill_def != null and skill_def.combat_profile != null, "冷却阻断回归前置：archer_long_draw 定义应存在。")
	if skill_def == null or skill_def.combat_profile == null:
		_cleanup_test_session(game_session)
		return

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)
	var selection = GAME_RUNTIME_BATTLE_SELECTION_SCRIPT.new()
	selection.setup(facade)

	var state: BattleState = _build_flat_state(Vector2i(4, 2))
	var caster: BattleUnitState = _build_manual_unit(
		&"cooldown_skill_user",
		"冷却施法者",
		&"player",
		Vector2i(0, 0),
		[&"archer_long_draw"],
		2,
		0
	)
	caster.current_stamina = 12
	caster.attribute_snapshot.set_value(&"stamina_max", 12)
	caster.cooldowns[&"archer_long_draw"] = 10
	var enemy_a: BattleUnitState = _build_manual_unit(&"cooldown_enemy_a", "敌人A", &"enemy", Vector2i(2, 0), [], 2, 0)
	_add_unit_to_state(facade, state, caster, false)
	_add_unit_to_state(facade, state, enemy_a, true)
	state.phase = &"unit_acting"
	state.active_unit_id = caster.unit_id
	_apply_battle_state(facade, state)

	selection.select_battle_skill_slot(0)
	_assert_eq(String(facade.get_selected_battle_skill_id()), "", "冷却未结束时不应把技能写入选中状态。")
	_assert_eq(
		_extract_coord_pairs(selection.get_selected_battle_skill_valid_target_coords()),
		[],
		"冷却未结束时，不应继续高亮任何合法目标。"
	)
	_assert_true(
		String(facade.get_status_text()).contains("冷却"),
		"冷却未结束时，状态文案应直接说明阻断原因。"
	)

	_cleanup_test_session(game_session)


func _assert_selection_sidecar_matches_ground_preview_target_collection(
	game_session,
	skill_id: StringName,
	current_mp: int,
	current_stamina: int,
	target_coord: Vector2i,
	label: String
) -> void:
	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)
	var selection = GAME_RUNTIME_BATTLE_SELECTION_SCRIPT.new()
	selection.setup(facade)

	var state: BattleState = _build_flat_state(Vector2i(5, 5))
	var caster: BattleUnitState = _build_manual_unit(
		StringName("%s_%s" % [label, String(skill_id)]),
		"%s 施法者" % label,
		&"player",
		Vector2i(2, 2),
		[skill_id],
		2,
		current_mp
	)
	caster.current_stamina = current_stamina
	caster.attribute_snapshot.set_value(&"stamina_max", maxi(current_stamina, 1))
	_apply_test_equipped_weapon(caster)
	_add_unit_to_state(facade, state, caster, false)
	state.phase = &"unit_acting"
	state.active_unit_id = caster.unit_id
	_apply_battle_state(facade, state)

	selection.select_battle_skill_slot(0)
	_assert_eq(String(facade.get_selected_battle_skill_id()), String(skill_id), "%s 范围技能选择后应写入 facade 选中状态。" % label)

	facade.set_battle_selection_target_coords_state([target_coord])
	var selected_target_coords := selection.get_selected_battle_skill_target_coords()

	var command = BATTLE_COMMAND_SCRIPT.new()
	command.command_type = BATTLE_COMMAND_SCRIPT.TYPE_SKILL
	command.unit_id = caster.unit_id
	command.skill_id = skill_id
	command.target_coord = target_coord
	var preview = facade.preview_battle_command(command)
	_assert_true(preview != null and preview.allowed, "%s 范围技能前置：preview_command 应允许测试目标地格。" % label)
	if preview == null:
		return
	_assert_true(preview.target_coords.size() > 1, "%s 范围技能前置：runtime preview 应返回正式范围坐标，而不是只保留锚点。" % label)
	_assert_eq(
		_extract_coord_pairs(selected_target_coords),
		_extract_coord_pairs(preview.target_coords),
		"%s 范围技能的 selection 读面应复用与 runtime preview 相同的范围收集结果。" % label
	)


func _assert_selection_sidecar_matches_self_preview_target_collection(
	game_session,
	skill_id: StringName,
	current_mp: int,
	current_stamina: int,
	label: String
) -> void:
	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)
	var selection = GAME_RUNTIME_BATTLE_SELECTION_SCRIPT.new()
	selection.setup(facade)

	var state: BattleState = _build_flat_state(Vector2i(5, 5))
	var caster: BattleUnitState = _build_manual_unit(
		StringName("%s_%s" % [label, String(skill_id)]),
		"%s 施法者" % label,
		&"player",
		Vector2i(2, 2),
		[skill_id],
		2,
		current_mp
	)
	caster.current_stamina = current_stamina
	caster.attribute_snapshot.set_value(&"stamina_max", maxi(current_stamina, 1))
	_add_unit_to_state(facade, state, caster, false)
	state.phase = &"unit_acting"
	state.active_unit_id = caster.unit_id
	_apply_battle_state(facade, state)

	selection.select_battle_skill_slot(0)
	_assert_eq(String(facade.get_selected_battle_skill_id()), String(skill_id), "%s 自身技能选择后应写入 facade 选中状态。" % label)

	var selected_target_coords := selection.get_selected_battle_skill_target_coords()
	var command = BATTLE_COMMAND_SCRIPT.new()
	command.command_type = BATTLE_COMMAND_SCRIPT.TYPE_SKILL
	command.unit_id = caster.unit_id
	command.skill_id = skill_id
	command.target_unit_id = caster.unit_id
	command.target_coord = caster.coord
	var preview = facade.preview_battle_command(command)
	_assert_true(preview != null and preview.allowed, "%s 自身技能前置：preview_command 应允许对施法者自身预览。" % label)
	if preview == null:
		return
	_assert_eq(
		_extract_coord_pairs(selected_target_coords),
		_extract_coord_pairs(preview.target_coords),
		"%s 自身技能的 selection 读面应复用与 runtime preview 相同的自身范围收集结果。" % label
	)


func _test_selection_sidecar_hides_targets_for_aura_blocked_skill() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return

	var skill_def = game_session.get_skill_defs().get(&"warrior_aura_slash")
	_assert_true(skill_def != null and skill_def.combat_profile != null, "Aura 阻断回归前置：warrior_aura_slash 定义应存在。")
	if skill_def == null or skill_def.combat_profile == null:
		_cleanup_test_session(game_session)
		return

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)
	var selection = GAME_RUNTIME_BATTLE_SELECTION_SCRIPT.new()
	selection.setup(facade)

	var state: BattleState = _build_flat_state(Vector2i(4, 2))
	var caster: BattleUnitState = _build_manual_unit(
		&"aura_skill_user",
		"Aura 施法者",
		&"player",
		Vector2i(0, 0),
		[&"warrior_aura_slash"],
		2,
		0
	)
	caster.current_aura = 0
	caster.attribute_snapshot.set_value(&"aura_max", 1)
	var enemy_a: BattleUnitState = _build_manual_unit(&"aura_enemy_a", "敌人A", &"enemy", Vector2i(2, 0), [], 2, 0)
	_add_unit_to_state(facade, state, caster, false)
	_add_unit_to_state(facade, state, enemy_a, true)
	state.phase = &"unit_acting"
	state.active_unit_id = caster.unit_id
	_apply_battle_state(facade, state)

	selection.select_battle_skill_slot(0)
	_assert_eq(String(facade.get_selected_battle_skill_id()), "", "Aura 未解锁时不应把技能写入选中状态。")
	_assert_eq(
		_extract_coord_pairs(selection.get_selected_battle_skill_valid_target_coords()),
		[],
		"Aura 未解锁时，不应继续高亮任何合法目标。"
	)
	_assert_true(
		String(facade.get_status_text()).contains("斗气尚未解锁"),
		"Aura 未解锁时，状态文案应直接说明阻断原因。"
	)

	_cleanup_test_session(game_session)


func _test_selection_sidecar_focuses_caster_when_multi_unit_confirm_ready() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return

	var skill_def = game_session.get_skill_defs().get(&"mage_arcane_missile")
	_assert_true(skill_def != null and skill_def.combat_profile != null, "确认焦点回归前置：mage_arcane_missile 定义应存在。")
	if skill_def == null or skill_def.combat_profile == null:
		_cleanup_test_session(game_session)
		return
	skill_def.combat_profile.min_target_count = 2
	skill_def.combat_profile.max_target_count = 3
	skill_def.combat_profile.range_value = 4
	skill_def.combat_profile.level_overrides = {}

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)
	var selection = GAME_RUNTIME_BATTLE_SELECTION_SCRIPT.new()
	selection.setup(facade)

	var state: BattleState = _build_flat_state(Vector2i(5, 2))
	var caster: BattleUnitState = _build_manual_unit(
		&"confirm_focus_user",
		"确认焦点施法者",
		&"player",
		Vector2i(0, 0),
		[&"mage_arcane_missile"],
		2,
		120
	)
	var enemy_a: BattleUnitState = _build_manual_unit(&"confirm_enemy_a", "敌人A", &"enemy", Vector2i(2, 0), [], 2, 0)
	var enemy_b: BattleUnitState = _build_manual_unit(&"confirm_enemy_b", "敌人B", &"enemy", Vector2i(3, 0), [], 2, 0)
	var enemy_c: BattleUnitState = _build_manual_unit(&"confirm_enemy_c", "敌人C", &"enemy", Vector2i(4, 0), [], 2, 0)
	_add_unit_to_state(facade, state, caster, false)
	_add_unit_to_state(facade, state, enemy_a, true)
	_add_unit_to_state(facade, state, enemy_b, true)
	_add_unit_to_state(facade, state, enemy_c, true)
	state.phase = &"unit_acting"
	state.active_unit_id = caster.unit_id
	_apply_battle_state(facade, state)

	selection.select_battle_skill_slot(0)
	selection.attempt_battle_move_to(enemy_a.coord)
	var second_click_mode := String(selection.attempt_battle_move_to(enemy_b.coord))
	_assert_eq(second_click_mode, "overlay", "达到最小目标数但未达上限时，应停留在确认态 overlay。")
	_assert_eq(
		facade.get_battle_selected_coord(),
		caster.coord,
		"进入 multi_unit 确认态后，棋盘焦点应回到施法者自身，而不是最后一个锁定目标。"
	)
	var remaining_target_coords := _extract_coord_pairs(selection.get_selected_battle_skill_valid_target_coords())
	var block_reason := facade.get_battle_skill_cast_block_reason(caster, skill_def)
	_assert_true(
		remaining_target_coords.has([enemy_c.coord.x, enemy_c.coord.y]),
		"进入确认态后，剩余合法目标仍应继续保留。 coords=%s selected=%s ap=%d mp=%d block=%s" % [
			str(remaining_target_coords),
			str(_extract_string_array(selection.get_selected_battle_skill_target_unit_ids())),
			int(caster.current_ap),
			int(caster.current_mp),
			block_reason,
		]
	)

	_cleanup_test_session(game_session)


func _test_preview_command_rejects_when_battle_modal_blocks_interaction() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)

	var state: BattleState = _build_flat_state(Vector2i(4, 2))
	var mover: BattleUnitState = _build_manual_unit(
		&"preview_blocked_user",
		"预览阻断者",
		&"player",
		Vector2i(0, 0),
		[],
		2,
		0
	)
	_add_unit_to_state(facade, state, mover, false)
	state.phase = &"unit_acting"
	state.active_unit_id = mover.unit_id
	state.modal_state = &"promotion_choice"
	_apply_battle_state(facade, state)

	var command = BATTLE_COMMAND_SCRIPT.new()
	command.command_type = BATTLE_COMMAND_SCRIPT.TYPE_MOVE
	command.unit_id = mover.unit_id
	command.target_coord = Vector2i(1, 0)
	var preview = facade.preview_battle_command(command)
	_assert_true(preview != null, "battle modal 阻断回归前置：preview_command 应返回 BattlePreview。")
	if preview == null:
		_cleanup_test_session(game_session)
		return
	_assert_true(not preview.allowed, "battle modal 打开时 preview_command 不应继续允许移动预览。")
	_assert_true(
		not preview.log_lines.is_empty() and String(preview.log_lines[-1]).contains("无法操作"),
		"battle modal 打开时 preview_command 应给出明确阻断文案。"
	)

	_cleanup_test_session(game_session)


func _test_selection_read_faces_hide_targets_when_battle_modal_blocks_interaction() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)

	var state: BattleState = _build_flat_state(Vector2i(4, 2))
	var caster: BattleUnitState = _build_manual_unit(
		&"read_face_blocked_user",
		"读面阻断者",
		&"player",
		Vector2i(0, 0),
		[&"archer_long_draw"],
		2,
		0
	)
	var enemy: BattleUnitState = _build_manual_unit(&"read_face_enemy", "敌人", &"enemy", Vector2i(2, 0), [], 2, 0)
	_add_unit_to_state(facade, state, caster, false)
	_add_unit_to_state(facade, state, enemy, true)
	state.phase = &"unit_acting"
	state.active_unit_id = caster.unit_id
	state.modal_state = &"promotion_choice"
	_apply_battle_state(facade, state)

	facade.set_battle_selection_skill_id(&"archer_long_draw")
	facade.set_battle_selection_target_unit_ids_state([enemy.unit_id])
	facade.set_runtime_active_modal_id("promotion")

	_assert_eq(String(facade.get_selected_battle_skill_id()), "archer_long_draw", "battle modal 阻断回归前置：内部 selected_skill_id 应保留，避免靠清状态掩盖问题。")
	_assert_eq(
		_extract_string_array(facade.get_selected_battle_skill_target_unit_ids()),
		[],
		"battle modal 打开时，目标单位读面应被隐藏。"
	)
	_assert_eq(
		_extract_coord_pairs(facade.get_selected_battle_skill_target_coords()),
		[],
		"battle modal 打开时，已选目标坐标读面应被隐藏。"
	)
	_assert_eq(
		_extract_coord_pairs(facade.get_selected_battle_skill_valid_target_coords()),
		[],
		"battle modal 打开时，不应继续暴露技能合法目标高亮。"
	)
	_assert_eq(
		_extract_coord_pairs(facade.get_battle_overlay_target_coords()),
		[],
		"battle modal 打开时，battle overlay 不应继续暴露可走/可打提示。"
	)

	_cleanup_test_session(game_session)


func _test_battle_commands_and_proxy_reject_when_battle_modal_blocks_interaction() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)

	var state: BattleState = _build_flat_state(Vector2i(4, 2))
	var caster: BattleUnitState = _build_manual_unit(
		&"command_blocked_user",
		"命令阻断者",
		&"player",
		Vector2i(0, 0),
		[&"archer_long_draw"],
		2,
		0
	)
	var enemy: BattleUnitState = _build_manual_unit(&"command_blocked_enemy", "敌人", &"enemy", Vector2i(2, 0), [], 2, 0)
	_add_unit_to_state(facade, state, caster, false)
	_add_unit_to_state(facade, state, enemy, true)
	state.phase = &"unit_acting"
	state.active_unit_id = caster.unit_id
	state.modal_state = &"promotion_choice"
	_apply_battle_state(facade, state)

	var select_result: Dictionary = facade.command_battle_select_skill(0)
	_assert_true(not bool(select_result.get("ok", false)), "battle modal 打开时 command_battle_select_skill 应返回明确失败。")
	_assert_true(String(select_result.get("message", "")).contains("无法操作"), "battle modal 打开时技能选择失败信息应说明无法操作。")
	_assert_eq(String(facade.get_selected_battle_skill_id()), "", "battle modal 打开时 command_battle_select_skill 不应污染 selection 状态。")

	var move_result: Dictionary = facade.command_battle_move_to(Vector2i(1, 0))
	_assert_true(not bool(move_result.get("ok", false)), "battle modal 打开时 command_battle_move_to 应返回明确失败。")
	_assert_true(String(move_result.get("message", "")).contains("无法操作"), "battle modal 打开时移动失败信息应说明无法操作。")
	_assert_eq(caster.coord, Vector2i(0, 0), "battle modal 打开时 command_battle_move_to 不应移动单位。")

	var proxy_result: Dictionary = facade.select_battle_cell(Vector2i(1, 0))
	_assert_true(not bool(proxy_result.get("ok", false)), "battle modal 打开时 select_battle_cell 应沿 proxy 路径返回失败。")
	_assert_true(String(proxy_result.get("message", "")).contains("无法操作"), "battle modal 打开时 select_battle_cell 的失败信息应说明无法操作。")
	_assert_eq(caster.coord, Vector2i(0, 0), "battle modal 打开时 select_battle_cell 不应绕过 session 守卫移动单位。")

	_cleanup_test_session(game_session)


func _test_promotion_modal_pauses_battle_timeline() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)

	var state: BattleState = _build_flat_state(Vector2i(4, 2))
	var caster: BattleUnitState = _build_manual_unit(
		&"promotion_timeline_user",
		"晋升选择者",
		&"player",
		Vector2i(0, 0),
		[&"archer_long_draw"],
		2,
		0
	)
	var enemy: BattleUnitState = _build_manual_unit(
		&"promotion_timeline_enemy",
		"晋升陪练",
		&"enemy",
		Vector2i(3, 0),
		[],
		2,
		0
	)
	_add_unit_to_state(facade, state, caster, false)
	_add_unit_to_state(facade, state, enemy, true)
	state.phase = &"timeline_running"
	state.modal_state = &"promotion_choice"
	_apply_battle_state(facade, state)
	facade.set_runtime_active_modal_id("promotion")

	var changed := facade.advance(1)
	_assert_true(not changed, "promotion_choice 打开时 facade.advance() 不应继续推进 battle timeline。")
	var battle_state := facade.get_battle_state()
	_assert_eq(
		int(battle_state.timeline.current_tu) if battle_state != null and battle_state.timeline != null else -1,
		0,
		"promotion_choice 打开时，battle timeline 1 秒后仍应保持原 TU。"
	)
	_assert_eq(facade.get_last_advance_battle_refresh_mode(), "", "promotion_choice 打开时不应产生 battle refresh 建议。")
	_assert_eq(String(battle_state.modal_state) if battle_state != null else "", "promotion_choice", "promotion_choice 打开时 battle modal_state 不应被 advance() 改写。")
	_assert_eq(facade.get_active_modal_id(), "promotion", "promotion_choice 打开时 runtime modal 不应被 advance() 改写。")

	_cleanup_test_session(game_session)


func _test_character_info_blocks_commands_without_pausing_battle_timeline() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)

	var state: BattleState = _build_flat_state(Vector2i(4, 2))
	var caster: BattleUnitState = _build_manual_unit(
		&"character_info_timeline_user",
		"信息查看者",
		&"player",
		Vector2i(0, 0),
		[&"archer_long_draw"],
		2,
		0
	)
	var enemy: BattleUnitState = _build_manual_unit(
		&"character_info_timeline_enemy",
		"信息陪练",
		&"enemy",
		Vector2i(3, 0),
		[],
		2,
		0
	)
	_add_unit_to_state(facade, state, caster, false)
	_add_unit_to_state(facade, state, enemy, true)
	state.phase = &"timeline_running"
	_apply_battle_state(facade, state)

	var opened := facade.try_open_character_info_at_battle_coord(caster.coord)
	_assert_true(opened, "battle character_info 回归前置：应能成功打开战斗人物信息窗。")
	_assert_eq(facade.get_active_modal_id(), "character_info", "打开战斗人物信息窗后应进入 character_info modal。")

	var select_result: Dictionary = facade.command_battle_select_skill(0)
	_assert_true(not bool(select_result.get("ok", false)), "character_info 打开时 battle command 仍应被阻断。")
	_assert_true(
		String(select_result.get("message", "")).contains("查看角色信息"),
		"character_info 打开时 battle command 的失败文案应明确指向角色信息窗。"
	)

	var changed := facade.advance(1)
	_assert_true(changed, "character_info 打开时 facade.advance() 仍应正式推进 battle TU。")
	var battle_state := facade.get_battle_state()
	_assert_eq(
		int(battle_state.timeline.current_tu) if battle_state != null and battle_state.timeline != null else -1,
		5,
		"character_info 打开时，battle timeline 1 秒仍应推进 5 TU。"
	)
	_assert_eq(facade.get_active_modal_id(), "character_info", "battle TU 推进不应自动关闭 character_info modal。")

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


func _build_flat_state(map_size: Vector2i) -> BattleState:
	var state: BattleState = BATTLE_STATE_SCRIPT.new()
	state.battle_id = &"battle_selection_regression"
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
	unit.current_move_points = BATTLE_UNIT_STATE_SCRIPT.DEFAULT_MOVE_POINTS_PER_TURN
	unit.current_stamina = 20
	unit.is_alive = true
	unit.set_anchor_coord(coord)
	unit.attribute_snapshot.set_value(&"hp_max", 30)
	unit.attribute_snapshot.set_value(&"mp_max", maxi(current_mp, 120))
	unit.attribute_snapshot.set_value(&"action_points", maxi(current_ap, 2))
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 10)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 12)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 4)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 4)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 100)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	unit.known_active_skill_ids = skill_ids.duplicate()
	for skill_id in unit.known_active_skill_ids:
		unit.known_skill_level_map[skill_id] = 1
	if current_mp > 0:
		unit.unlock_combat_resource(BATTLE_UNIT_STATE_SCRIPT.COMBAT_RESOURCE_MP)
	return unit


func _apply_test_equipped_weapon(unit: BattleUnitState, attack_range: int = 1) -> void:
	if unit == null:
		return
	unit.apply_weapon_projection({
		"weapon_profile_kind": "equipped",
		"weapon_item_id": "battle_selection_test_blade",
		"weapon_profile_type_id": "test_blade",
		"weapon_current_grip": "one_handed",
		"weapon_attack_range": attack_range,
		"weapon_one_handed_dice": {"dice_count": 1, "dice_sides": 6, "flat_bonus": 0},
		"weapon_uses_two_hands": false,
		"weapon_physical_damage_tag": "physical_slash",
	})


func _add_unit_to_state(facade, state: BattleState, unit: BattleUnitState, is_enemy: bool) -> void:
	state.units[unit.unit_id] = unit
	if is_enemy:
		state.enemy_unit_ids.append(unit.unit_id)
	else:
		state.ally_unit_ids.append(unit.unit_id)
	var placed: bool = bool(facade._battle_runtime._grid_service.place_unit(state, unit, unit.coord, true))
	_assert_true(placed, "测试单位 %s 应能成功放入战场。" % String(unit.unit_id))


func _apply_battle_state(facade, state: BattleState) -> void:
	BattleRuntimeTestHelpers.configure_fixed_combat_for_facade(facade)
	facade._battle_runtime._state = state
	facade._battle_state = state
	facade._battle_selected_coord = Vector2i(-1, -1)
	facade._refresh_battle_runtime_state()


func _extract_string_array(values: Array) -> Array[String]:
	var result: Array[String] = []
	for value in values:
		result.append(String(value))
	return result


func _extract_coord_pairs(coords: Array[Vector2i]) -> Array:
	var pairs: Array = []
	for coord in coords:
		pairs.append([coord.x, coord.y])
	return pairs


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
