extends SceneTree

const BattleCommand = preload("res://scripts/systems/battle_command.gd")
const BattleCellState = preload("res://scripts/systems/battle_cell_state.gd")
const BattleHudAdapter = preload("res://scripts/ui/battle_hud_adapter.gd")
const BattleRuntimeModule = preload("res://scripts/systems/battle_runtime_module.gd")
const BattleState = preload("res://scripts/systems/battle_state.gd")
const BattleTimelineState = preload("res://scripts/systems/battle_timeline_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle_unit_state.gd")
const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attribute_service.gd")

const BLACK_CONTRACT_PUSH_SKILL_ID: StringName = &"black_contract_push"
const ACTION_TITHE_VARIANT_ID: StringName = &"action_tithe"
const WARRIOR_HEAVY_STRIKE_SKILL_ID: StringName = &"warrior_heavy_strike"

var _failures: Array[String] = []


class MockGameSession:
	extends Node

	var skill_defs: Dictionary = {}

	func get_skill_defs() -> Dictionary:
		return skill_defs


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_force_hit_skill_runtime_preview_is_guaranteed()
	await _test_single_hit_skill_hud_surfaces_runtime_preview()
	if _failures.is_empty():
		print("Battle hit preview contract regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle hit preview contract regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_force_hit_skill_runtime_preview_is_guaranteed() -> void:
	var skill_defs := ProgressionContentRegistry.new().get_skill_defs()
	var skill_def := skill_defs.get(BLACK_CONTRACT_PUSH_SKILL_ID) as SkillDef
	_assert_true(skill_def != null and skill_def.combat_profile != null, "黑契推进预览前置：技能定义应存在。")
	if skill_def == null or skill_def.combat_profile == null:
		return

	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, skill_defs, {}, {}, null)
	var state := _build_state(&"preview_contract_force_hit")
	var caster := _build_unit(
		&"contract_caster",
		"黑契使徒",
		&"player",
		Vector2i(1, 1),
		[BLACK_CONTRACT_PUSH_SKILL_ID],
		2
	)
	var target := _build_unit(
		&"contract_target",
		"高闪避敌人",
		&"enemy",
		Vector2i(2, 1),
		[],
		2
	)
	target.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 999)
	_add_unit_to_runtime_state(runtime, state, caster, false)
	_add_unit_to_runtime_state(runtime, state, target, true)
	state.phase = &"unit_acting"
	state.active_unit_id = caster.unit_id
	runtime._state = state

	var preview := runtime.preview_command(_build_skill_command(
		caster.unit_id,
		BLACK_CONTRACT_PUSH_SKILL_ID,
		target,
		ACTION_TITHE_VARIANT_ID
	))
	_assert_true(preview != null and preview.allowed, "黑契推进应能对合法目标生成 preview。")
	var hit_preview: Dictionary = preview.hit_preview if preview != null else {}
	_assert_eq(int(hit_preview.get("hit_rate_percent", 0)), 100, "黑契推进 hit_rate_percent 应为 100。")
	_assert_eq(int(hit_preview.get("success_rate_percent", 0)), 100, "黑契推进 success_rate_percent 应为 100。")
	_assert_eq(hit_preview.get("stage_success_rates", []), [100], "黑契推进 stage_success_rates 应为 [100]。")
	_assert_true(bool(hit_preview.get("force_hit_no_crit", false)), "黑契推进 preview 应标记 force_hit_no_crit。")
	_assert_true(
		String(hit_preview.get("summary_text", "")).contains("必定命中")
			and String(hit_preview.get("summary_text", "")).contains("禁暴击"),
		"黑契推进 preview 文案应说明必定命中且禁暴击。"
	)
	runtime.dispose()


func _test_single_hit_skill_hud_surfaces_runtime_preview() -> void:
	var skill_defs := ProgressionContentRegistry.new().get_skill_defs()
	var skill_def := skill_defs.get(WARRIOR_HEAVY_STRIKE_SKILL_ID) as SkillDef
	_assert_true(skill_def != null and skill_def.combat_profile != null, "重击 HUD 预览前置：技能定义应存在。")
	if skill_def == null or skill_def.combat_profile == null:
		return

	var game_session := await _install_mock_game_session(skill_defs)
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, skill_defs, {}, {}, null)
	var state := _build_state(&"preview_contract_single_hit")
	var attacker := _build_unit(
		&"heavy_strike_user",
		"重击战士",
		&"player",
		Vector2i(1, 1),
		[WARRIOR_HEAVY_STRIKE_SKILL_ID],
		3
	)
	attacker.current_stamina = 30
	attacker.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 80)
	var target := _build_unit(
		&"heavy_strike_target",
		"高闪避木桩",
		&"enemy",
		Vector2i(2, 1),
		[],
		2
	)
	target.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 70)
	_add_unit_to_runtime_state(runtime, state, attacker, false)
	_add_unit_to_runtime_state(runtime, state, target, true)
	state.phase = &"unit_acting"
	state.active_unit_id = attacker.unit_id
	runtime._state = state

	var preview := runtime.preview_command(_build_skill_command(
		attacker.unit_id,
		WARRIOR_HEAVY_STRIKE_SKILL_ID,
		target
	))
	_assert_true(preview != null and not preview.hit_preview.is_empty(), "重击 runtime preview 应暴露命中摘要。")
	var hit_preview_text := String(preview.hit_preview.get("summary_text", "")) if preview != null else ""
	_assert_true(hit_preview_text.contains("预计命中率") and hit_preview_text.contains("需 "), "重击 runtime preview 应包含命中率与 required roll。")

	var adapter := BattleHudAdapter.new()
	var snapshot := adapter.build_snapshot(
		state,
		target.coord,
		WARRIOR_HEAVY_STRIKE_SKILL_ID,
		skill_def.display_name,
		"",
		[],
		1,
		[]
	)
	_assert_eq(
		String(snapshot.get("selected_skill_hit_preview_text", "")),
		hit_preview_text,
		"HUD snapshot 应保留普通单段技能的 runtime 命中摘要。"
	)
	_assert_eq(
		snapshot.get("selected_skill_hit_stage_rates", []),
		preview.hit_preview.get("stage_hit_rates", []),
		"HUD snapshot 应保留普通单段技能的阶段命中率。"
	)
	_assert_true(String(snapshot.get("skill_subtitle", "")).contains(hit_preview_text), "HUD 副标题应显示普通单段命中摘要。")

	runtime.dispose()
	game_session.queue_free()
	await process_frame


func _install_mock_game_session(skill_defs: Dictionary) -> MockGameSession:
	for child in root.get_children():
		if child.name == "GameSession":
			child.queue_free()
	await process_frame
	var game_session := MockGameSession.new()
	game_session.name = "GameSession"
	game_session.skill_defs = skill_defs
	root.add_child(game_session)
	await process_frame
	return game_session


func _build_state(battle_id: StringName) -> BattleState:
	var state := BattleState.new()
	state.battle_id = battle_id
	state.map_size = Vector2i(4, 3)
	state.terrain_profile_id = &"default"
	state.timeline = BattleTimelineState.new()
	state.cells = {}
	for y in range(3):
		for x in range(4):
			state.cells[Vector2i(x, y)] = _build_cell(Vector2i(x, y))
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)
	state.units = {}
	state.ally_unit_ids = []
	state.enemy_unit_ids = []
	return state


func _build_cell(coord: Vector2i) -> BattleCellState:
	var cell := BattleCellState.new()
	cell.coord = coord
	cell.stack_layer = 0
	cell.base_height = 0
	cell.base_terrain = BattleCellState.TERRAIN_LAND
	cell.recalculate_runtime_values()
	return cell


func _build_unit(
	unit_id: StringName,
	display_name: String,
	faction_id: StringName,
	coord: Vector2i,
	skill_ids: Array[StringName],
	current_ap: int
) -> BattleUnitState:
	var unit := BattleUnitState.new()
	unit.unit_id = unit_id
	unit.display_name = display_name
	unit.faction_id = faction_id
	unit.control_mode = &"manual"
	unit.current_hp = 40
	unit.current_mp = 4
	unit.current_ap = current_ap
	unit.current_stamina = 30
	unit.current_aura = 0
	unit.is_alive = true
	unit.set_anchor_coord(coord)
	unit.attribute_snapshot.set_value(&"hp_max", 40)
	unit.attribute_snapshot.set_value(&"mp_max", 4)
	unit.attribute_snapshot.set_value(&"stamina_max", 30)
	unit.attribute_snapshot.set_value(&"action_points", maxi(current_ap, 1))
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 12)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 10)
	unit.set_natural_weapon_projection(&"test_blade", &"physical_slash", 1)
	unit.known_active_skill_ids = skill_ids.duplicate()
	for skill_id in unit.known_active_skill_ids:
		unit.known_skill_level_map[skill_id] = 1
	return unit


func _add_unit_to_runtime_state(runtime: BattleRuntimeModule, state: BattleState, unit: BattleUnitState, is_enemy: bool) -> void:
	state.units[unit.unit_id] = unit
	if is_enemy:
		state.enemy_unit_ids.append(unit.unit_id)
	else:
		state.ally_unit_ids.append(unit.unit_id)
	_assert_true(bool(runtime._grid_service.place_unit(state, unit, unit.coord, true)), "preview contract 测试单位应成功放入战场。")


func _build_skill_command(
	unit_id: StringName,
	skill_id: StringName,
	target_unit: BattleUnitState,
	variant_id: StringName = &""
) -> BattleCommand:
	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = unit_id
	command.skill_id = skill_id
	command.skill_variant_id = variant_id
	command.target_unit_id = target_unit.unit_id if target_unit != null else &""
	command.target_coord = target_unit.coord if target_unit != null else Vector2i(-1, -1)
	return command


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
