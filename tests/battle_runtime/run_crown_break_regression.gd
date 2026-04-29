extends SceneTree

const BattleCommand = preload("res://scripts/systems/battle/core/battle_command.gd")
const BattleCellState = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BattleRuntimeModule = preload("res://scripts/systems/battle/runtime/battle_runtime_module.gd")
const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleStatusEffectState = preload("res://scripts/systems/battle/core/battle_status_effect_state.gd")
const BattleTimelineState = preload("res://scripts/systems/battle/core/battle_timeline_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const GameRuntimeBattleSelection = preload("res://scripts/systems/game_runtime/game_runtime_battle_selection.gd")
const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")
const DETERMINISTIC_BATTLE_HIT_RESOLVER_SCRIPT = preload("res://tests/battle_runtime/helpers/deterministic_battle_hit_resolver.gd")

const CROWN_BREAK_SKILL_ID: StringName = &"crown_break"
const SAINT_BLADE_COMBO_SKILL_ID: StringName = &"saint_blade_combo"
const WARRIOR_HEAVY_STRIKE_SKILL_ID: StringName = &"warrior_heavy_strike"
const VARIANT_BROKEN_FANG: StringName = &"broken_fang"
const VARIANT_BROKEN_HAND: StringName = &"broken_hand"
const VARIANT_BLINDED_EYE: StringName = &"blinded_eye"
const STATUS_BLACK_STAR_BRAND_NORMAL: StringName = &"black_star_brand_normal"
const STATUS_BLACK_STAR_BRAND_ELITE: StringName = &"black_star_brand_elite"
const STATUS_BLACK_STAR_BRAND_ELITE_GUARD_WINDOW: StringName = &"black_star_brand_elite_guard_window"
const STATUS_CROWN_BREAK_BROKEN_FANG: StringName = &"crown_break_broken_fang"
const STATUS_CROWN_BREAK_BROKEN_HAND: StringName = &"crown_break_broken_hand"
const STATUS_CROWN_BREAK_BLINDED_EYE: StringName = &"crown_break_blinded_eye"
const FORTUNE_MARK_TARGET_STAT_ID: StringName = &"fortune_mark_target"

var _failures: Array[String] = []


class SelectionRuntimeProxy:
	extends RefCounted

	var runtime: BattleRuntimeModule = null
	var skill_defs: Dictionary = {}
	var selected_skill_id: StringName = &""
	var selected_skill_variant_id: StringName = &""
	var last_manual_unit_id: StringName = &""
	var target_coords_state: Array[Vector2i] = []
	var target_unit_ids_state: Array[StringName] = []
	var selected_coord: Vector2i = Vector2i(-1, -1)
	var status_text := ""


	func _init(battle_runtime: BattleRuntimeModule, battle_skill_defs: Dictionary) -> void:
		runtime = battle_runtime
		skill_defs = battle_skill_defs if battle_skill_defs != null else {}


	func get_manual_battle_unit() -> BattleUnitState:
		var active_unit := get_runtime_battle_active_unit()
		return active_unit if active_unit != null and active_unit.control_mode == &"manual" else null


	func get_runtime_battle_active_unit() -> BattleUnitState:
		var state := get_battle_state()
		if state == null:
			return null
		return state.units.get(state.active_unit_id) as BattleUnitState


	func get_runtime_battle_unit_at_coord(coord: Vector2i) -> BattleUnitState:
		if runtime == null:
			return null
		return runtime.get_grid_service().get_unit_at_coord(runtime.get_state(), coord)


	func get_runtime_battle_unit_by_id(unit_id: StringName) -> BattleUnitState:
		var state := get_battle_state()
		if state == null:
			return null
		return state.units.get(unit_id) as BattleUnitState


	func get_battle_state() -> BattleState:
		return runtime.get_state() if runtime != null else null


	func get_battle_grid_service():
		return runtime.get_grid_service() if runtime != null else null


	func preview_battle_command(command):
		return runtime.preview_command(command) if runtime != null else null


	func issue_battle_command(command) -> StringName:
		if runtime != null:
			runtime.issue_command(command)
		return &"full"


	func refresh_battle_selection_state() -> void:
		pass


	func update_status(message: String) -> void:
		status_text = message


	func format_coord(coord: Vector2i) -> String:
		return "(%d,%d)" % [coord.x, coord.y]


	func is_battle_active() -> bool:
		return runtime != null and runtime.is_battle_active()


	func get_selected_battle_skill_id() -> StringName:
		return selected_skill_id


	func set_battle_selection_skill_id(skill_id: StringName) -> void:
		selected_skill_id = skill_id


	func get_selected_battle_skill_variant_id() -> StringName:
		return selected_skill_variant_id


	func set_battle_selection_skill_variant_id(variant_id: StringName) -> void:
		selected_skill_variant_id = variant_id


	func get_battle_selection_last_manual_unit_id() -> StringName:
		return last_manual_unit_id


	func set_battle_selection_last_manual_unit_id(unit_id: StringName) -> void:
		last_manual_unit_id = unit_id


	func get_battle_selection_target_coords_state() -> Array[Vector2i]:
		return target_coords_state.duplicate()


	func set_battle_selection_target_coords_state(target_coords: Array[Vector2i]) -> void:
		target_coords_state = target_coords.duplicate()


	func get_battle_selection_target_unit_ids_state() -> Array[StringName]:
		return target_unit_ids_state.duplicate()


	func set_battle_selection_target_unit_ids_state(target_unit_ids: Array[StringName]) -> void:
		target_unit_ids_state = target_unit_ids.duplicate()


	func set_runtime_battle_selected_coord(coord: Vector2i) -> void:
		selected_coord = coord


	func get_skill_defs() -> Dictionary:
		return skill_defs


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_crown_break_broken_fang_blocks_crit()
	_test_crown_break_broken_hand_blocks_counterattack_and_follow_up()
	_test_crown_break_blinded_eye_blocks_evasion()
	_test_crown_break_rejects_illegal_targets_in_selection_preview_and_issue()

	if _failures.is_empty():
		print("Crown break regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Crown break regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_crown_break_broken_fang_blocks_crit() -> void:
	var runtime := _build_runtime()
	var state := _build_skill_test_state(&"crown_break_broken_fang", Vector2i(6, 3))
	var caster := _build_unit(&"crown_break_fang_caster", "施法者", &"player", Vector2i(1, 1), 3, &"hero")
	caster.known_active_skill_ids = [CROWN_BREAK_SKILL_ID]
	caster.known_skill_level_map = {CROWN_BREAK_SKILL_ID: 1}
	var elite := _build_unit(&"crown_break_fang_target", "精英敌人", &"enemy", Vector2i(2, 1), 2, &"", true)
	elite.known_active_skill_ids = [WARRIOR_HEAVY_STRIKE_SKILL_ID]
	elite.known_skill_level_map = {WARRIOR_HEAVY_STRIKE_SKILL_ID: 1}
	var ally_target := _build_unit(&"crown_break_fang_ally", "被打击者", &"player", Vector2i(3, 1), 2)

	_add_unit(runtime, state, caster)
	_add_unit(runtime, state, elite)
	_add_unit(runtime, state, ally_target)
	state.ally_unit_ids = [caster.unit_id, ally_target.unit_id]
	state.enemy_unit_ids = [elite.unit_id]
	state.active_unit_id = caster.unit_id
	runtime._state = state
	_begin_runtime_battle(runtime)
	_apply_elite_brand(elite, caster.unit_id)
	runtime.calamity_by_member_id[&"hero"] = 2

	var command := _build_ground_skill_command(caster.unit_id, VARIANT_BROKEN_FANG, elite.coord)
	var preview := runtime.preview_command(command)
	_assert_true(preview != null and preview.allowed, "断牙分支前置：已被烙印的 elite 应允许预览折冠。")
	runtime.issue_command(command)
	_assert_eq(runtime.get_member_calamity(&"hero"), 0, "折冠成功施放后应固定扣除 2 点 calamity。")
	_assert_true(elite.has_status_effect(STATUS_CROWN_BREAK_BROKEN_FANG), "断牙分支应写入 broken_fang 状态。")
	_assert_true(not elite.has_status_effect(STATUS_CROWN_BREAK_BROKEN_HAND), "断牙分支不应混入折手状态。")
	_assert_true(not elite.has_status_effect(STATUS_CROWN_BREAK_BLINDED_EYE), "断牙分支不应混入遮目状态。")


func _test_crown_break_broken_hand_blocks_counterattack_and_follow_up() -> void:
	var runtime := _build_runtime()
	var skill_def := runtime.get_skill_defs().get(SAINT_BLADE_COMBO_SKILL_ID) as SkillDef
	_assert_true(skill_def != null and skill_def.combat_profile != null, "折手回归前置：saint_blade_combo 定义应存在。")
	if skill_def == null or skill_def.combat_profile == null:
		return
	var repeat_effect := _get_effect_def(skill_def.combat_profile.effect_defs, &"repeat_attack_until_fail")
	_assert_true(repeat_effect != null, "折手回归前置：saint_blade_combo 应声明 repeat_attack_until_fail。")
	if repeat_effect == null:
		return

	var state := _build_skill_test_state(&"crown_break_broken_hand", Vector2i(6, 3))
	var caster := _build_unit(&"crown_break_hand_caster", "施法者", &"player", Vector2i(1, 1), 3, &"hero")
	caster.known_active_skill_ids = [CROWN_BREAK_SKILL_ID]
	caster.known_skill_level_map = {CROWN_BREAK_SKILL_ID: 1}
	var elite := _build_unit(&"crown_break_hand_target", "精英敌人", &"enemy", Vector2i(2, 1), 2, &"", true)
	elite.current_aura = 4
	elite.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 100)
	elite.known_active_skill_ids = [SAINT_BLADE_COMBO_SKILL_ID]
	elite.known_skill_level_map = {SAINT_BLADE_COMBO_SKILL_ID: 1}
	var ally_target := _build_unit(&"crown_break_hand_ally", "被追击者", &"player", Vector2i(3, 1), 2)
	ally_target.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, -10)

	_add_unit(runtime, state, caster)
	_add_unit(runtime, state, elite)
	_add_unit(runtime, state, ally_target)
	state.ally_unit_ids = [caster.unit_id, ally_target.unit_id]
	state.enemy_unit_ids = [elite.unit_id]
	state.active_unit_id = caster.unit_id
	runtime._state = state
	_begin_runtime_battle(runtime)
	_apply_elite_brand(elite, caster.unit_id)
	runtime.calamity_by_member_id[&"hero"] = 2

	var seal_command := _build_ground_skill_command(caster.unit_id, VARIANT_BROKEN_HAND, elite.coord)
	runtime.issue_command(seal_command)
	_assert_true(elite.has_status_effect(STATUS_CROWN_BREAK_BROKEN_HAND), "折手分支应写入 broken_hand 状态。")
	_assert_true(runtime.is_unit_counterattack_locked(elite), "折手分支应封锁反击读面。")

	state.active_unit_id = elite.unit_id
	state.phase = &"unit_acting"
	elite.current_ap = 2
	var follow_up_seed := _find_repeat_attack_seed_for_stage_outcomes(
		runtime,
		state,
		elite,
		ally_target,
		skill_def,
		repeat_effect,
		[true, true]
	)
	_assert_true(follow_up_seed >= 0, "折手回归应能找到原本可连续命中的圣剑连斩 battle seed。")
	if follow_up_seed < 0:
		return
	state.seed = follow_up_seed
	state.attack_roll_nonce = 0

	var follow_up_command := _build_unit_skill_command(elite.unit_id, SAINT_BLADE_COMBO_SKILL_ID, ally_target)
	var follow_up_preview = runtime.preview_command(follow_up_command)
	var stage_preview_texts := follow_up_preview.hit_preview.get("stage_preview_texts", []) as Array
	_assert_eq(stage_preview_texts.size(), 1, "折手分支应把追击预览压成 1 段。")

	var aura_before := elite.current_aura
	var hp_before := ally_target.current_hp
	var batch := runtime.issue_command(follow_up_command)
	_assert_eq(
		elite.current_aura,
		aura_before - int(skill_def.combat_profile.aura_cost),
		"折手分支下的连斩应只结算首段 Aura 成本。"
	)
	_assert_true(
		batch != null and batch.log_lines.any(func(line): return String(line).contains("无法继续追击")),
		"折手分支应写出追击被封锁的 battle log。 log=%s" % [str(batch.log_lines)]
	)
	_assert_true(
		not (batch != null and batch.log_lines.any(func(line): return String(line).contains("第 2 段"))),
		"折手分支不应继续进入第二段追击日志。 log=%s" % [str(batch.log_lines)]
	)
	_assert_true(
		ally_target.current_hp < hp_before and ally_target.current_hp >= hp_before - 18,
		"折手分支应保留首段命中，但不应继续叠第二段伤害。 before=%d after=%d" % [hp_before, ally_target.current_hp]
	)


func _test_crown_break_blinded_eye_blocks_evasion() -> void:
	var runtime := _build_runtime()
	var state := _build_skill_test_state(&"crown_break_blinded_eye", Vector2i(6, 3))
	var caster := _build_unit(&"crown_break_eye_caster", "施法者", &"player", Vector2i(1, 1), 3, &"hero")
	caster.known_active_skill_ids = [CROWN_BREAK_SKILL_ID, WARRIOR_HEAVY_STRIKE_SKILL_ID]
	caster.known_skill_level_map = {
		CROWN_BREAK_SKILL_ID: 1,
		WARRIOR_HEAVY_STRIKE_SKILL_ID: 1,
	}
	var elite := _build_unit(&"crown_break_eye_target", "精英敌人", &"enemy", Vector2i(2, 1), 2, &"", true)
	elite.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 25)

	_add_unit(runtime, state, caster)
	_add_unit(runtime, state, elite)
	state.ally_unit_ids = [caster.unit_id]
	state.enemy_unit_ids = [elite.unit_id]
	state.active_unit_id = caster.unit_id
	runtime._state = state
	_begin_runtime_battle(runtime)
	_apply_elite_brand(elite, caster.unit_id)
	runtime.calamity_by_member_id[&"hero"] = 2

	var seal_command := _build_ground_skill_command(caster.unit_id, VARIANT_BLINDED_EYE, elite.coord)
	runtime.issue_command(seal_command)
	_assert_true(elite.has_status_effect(STATUS_CROWN_BREAK_BLINDED_EYE), "遮目分支应写入 blinded_eye 状态。")
	_assert_true(not elite.has_status_effect(STATUS_CROWN_BREAK_BROKEN_FANG), "遮目分支不应混入断牙状态。")
	_assert_true(not elite.has_status_effect(STATUS_CROWN_BREAK_BROKEN_HAND), "遮目分支不应混入折手状态。")


func _test_crown_break_rejects_illegal_targets_in_selection_preview_and_issue() -> void:
	var runtime := _build_runtime()
	var state := _build_skill_test_state(&"crown_break_illegal_target", Vector2i(7, 3))
	var caster := _build_unit(&"crown_break_illegal_caster", "施法者", &"player", Vector2i(1, 1), 3, &"hero")
	caster.control_mode = &"manual"
	caster.known_active_skill_ids = [CROWN_BREAK_SKILL_ID]
	caster.known_skill_level_map = {CROWN_BREAK_SKILL_ID: 1}
	var branded_elite := _build_unit(&"crown_break_valid_target", "已烙印精英", &"enemy", Vector2i(2, 1), 2, &"", true)
	var branded_normal := _build_unit(&"crown_break_normal_brand", "已烙印普通敌人", &"enemy", Vector2i(3, 1), 2)
	var unbranded_elite := _build_unit(&"crown_break_unbranded_elite", "未烙印精英", &"enemy", Vector2i(4, 1), 2, &"", true)

	_add_unit(runtime, state, caster)
	_add_unit(runtime, state, branded_elite)
	_add_unit(runtime, state, branded_normal)
	_add_unit(runtime, state, unbranded_elite)
	state.ally_unit_ids = [caster.unit_id]
	state.enemy_unit_ids = [branded_elite.unit_id, branded_normal.unit_id, unbranded_elite.unit_id]
	state.active_unit_id = caster.unit_id
	runtime._state = state
	_begin_runtime_battle(runtime)
	_apply_elite_brand(branded_elite, caster.unit_id)
	_set_status(branded_normal, STATUS_BLACK_STAR_BRAND_NORMAL, 60, caster.unit_id)
	runtime.calamity_by_member_id[&"hero"] = 4

	var proxy := SelectionRuntimeProxy.new(runtime, runtime.get_skill_defs())
	var selection := GameRuntimeBattleSelection.new()
	selection.setup(proxy)
	selection.select_battle_skill_slot(0)
	var valid_coords := _extract_coord_pairs(selection.get_selected_battle_skill_valid_target_coords())
	_assert_true(
		valid_coords.has([branded_elite.coord.x, branded_elite.coord.y]),
		"selection 读面应保留已烙印 elite 的合法目标格。 actual=%s" % [str(valid_coords)]
	)
	_assert_true(
		not valid_coords.has([branded_normal.coord.x, branded_normal.coord.y]),
		"selection 读面不应把普通黑星烙印目标误判成折冠可选。 actual=%s" % [str(valid_coords)]
	)
	_assert_true(
		not valid_coords.has([unbranded_elite.coord.x, unbranded_elite.coord.y]),
		"selection 读面不应把未烙印 elite 暴露成折冠合法目标。 actual=%s" % [str(valid_coords)]
	)

	var illegal_command := _build_ground_skill_command(caster.unit_id, VARIANT_BROKEN_HAND, unbranded_elite.coord)
	var illegal_preview := runtime.preview_command(illegal_command)
	_assert_true(illegal_preview != null and not illegal_preview.allowed, "未烙印的 elite 不应通过折冠 preview。")
	_assert_true(
		illegal_preview != null and illegal_preview.log_lines.any(func(line): return String(line).contains("黑星烙印")),
		"非法目标预览应明确指出需要黑星烙印。 log=%s" % [str(illegal_preview.log_lines if illegal_preview != null else [])]
	)

	var ap_before_issue := caster.current_ap
	var calamity_before_issue := runtime.get_member_calamity(&"hero")
	var illegal_batch := runtime.issue_command(illegal_command)
	_assert_eq(caster.current_ap, ap_before_issue, "非法目标被 issue 拒绝时不应扣除 AP。")
	_assert_eq(runtime.get_member_calamity(&"hero"), calamity_before_issue, "非法目标被 issue 拒绝时不应扣除 calamity。")
	_assert_true(
		not unbranded_elite.has_status_effect(STATUS_CROWN_BREAK_BROKEN_HAND),
		"非法目标被 issue 拒绝后不应获得折手状态。"
	)
	_assert_true(
		illegal_batch != null and illegal_batch.log_lines.any(func(line): return String(line).contains("黑星烙印")),
		"非法目标被 issue 拒绝时应回传 preview 阻断原因。 log=%s" % [str(illegal_batch.log_lines if illegal_batch != null else [])]
	)


func _build_runtime() -> BattleRuntimeModule:
	var registry := ProgressionContentRegistry.new()
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})
	runtime.configure_hit_resolver_for_tests(DETERMINISTIC_BATTLE_HIT_RESOLVER_SCRIPT.new())
	return runtime


func _begin_runtime_battle(runtime: BattleRuntimeModule) -> void:
	if runtime == null:
		return
	runtime.calamity_by_member_id.clear()
	runtime._misfortune_service.begin_battle(runtime.calamity_by_member_id)


func _build_skill_test_state(battle_id: StringName, map_size: Vector2i) -> BattleState:
	var state := BattleState.new()
	state.battle_id = battle_id
	state.phase = &"unit_acting"
	state.map_size = map_size
	state.timeline = BattleTimelineState.new()
	state.cells = {}
	for y in range(map_size.y):
		for x in range(map_size.x):
			state.cells[Vector2i(x, y)] = _build_cell(Vector2i(x, y))
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)
	return state


func _build_cell(coord: Vector2i) -> BattleCellState:
	var cell := BattleCellState.new()
	cell.coord = coord
	cell.base_terrain = BattleCellState.TERRAIN_LAND
	cell.base_height = 4
	cell.height_offset = 0
	cell.recalculate_runtime_values()
	return cell


func _build_unit(
	unit_id: StringName,
	display_name: String,
	faction_id: StringName,
	coord: Vector2i,
	current_ap: int,
	source_member_id: StringName = &"",
	is_elite_or_boss := false
) -> BattleUnitState:
	var unit := BattleUnitState.new()
	unit.unit_id = unit_id
	unit.source_member_id = source_member_id
	unit.display_name = display_name
	unit.faction_id = faction_id
	unit.control_mode = &"manual"
	unit.current_ap = current_ap
	unit.current_hp = 60
	unit.current_mp = 4
	unit.current_stamina = 4
	unit.current_aura = 0
	unit.is_alive = true
	unit.set_anchor_coord(coord)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX, 60)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.MP_MAX, 4)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX, 4)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.AURA_MAX, 6)
	unit.attribute_snapshot.set_value(&"action_points", maxi(current_ap, 1))
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 12)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 4)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 6)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 4)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 60)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 10)
	unit.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.HIDDEN_LUCK_AT_BIRTH, 0)
	unit.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.FAITH_LUCK_BONUS, 0)
	unit.attribute_snapshot.set_value(FORTUNE_MARK_TARGET_STAT_ID, 1 if is_elite_or_boss else 0)
	return unit


func _add_unit(runtime: BattleRuntimeModule, state: BattleState, unit: BattleUnitState) -> void:
	state.units[unit.unit_id] = unit
	runtime._grid_service.place_unit(state, unit, unit.coord, true)


func _apply_elite_brand(target_unit: BattleUnitState, source_unit_id: StringName = &"") -> void:
	_set_status(target_unit, STATUS_BLACK_STAR_BRAND_ELITE, 60, source_unit_id)
	_set_status(target_unit, STATUS_BLACK_STAR_BRAND_ELITE_GUARD_WINDOW, 60, source_unit_id)


func _set_status(
	unit_state: BattleUnitState,
	status_id: StringName,
	duration_tu: int,
	source_unit_id: StringName = &"",
	power: int = 1
) -> void:
	if unit_state == null or status_id == &"":
		return
	var status_entry := BattleStatusEffectState.new()
	status_entry.status_id = status_id
	status_entry.source_unit_id = source_unit_id
	status_entry.power = maxi(power, 1)
	status_entry.stacks = 1
	status_entry.duration = duration_tu
	unit_state.set_status_effect(status_entry)


func _build_ground_skill_command(unit_id: StringName, variant_id: StringName, target_coord: Vector2i) -> BattleCommand:
	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = unit_id
	command.skill_id = CROWN_BREAK_SKILL_ID
	command.skill_variant_id = variant_id
	command.target_coord = target_coord
	command.target_coords = [target_coord]
	return command


func _build_unit_skill_command(unit_id: StringName, skill_id: StringName, target_unit: BattleUnitState) -> BattleCommand:
	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = unit_id
	command.skill_id = skill_id
	command.target_unit_id = target_unit.unit_id if target_unit != null else &""
	command.target_coord = target_unit.coord if target_unit != null else Vector2i(-1, -1)
	return command


func _build_damage_effect() -> CombatEffectDef:
	var effect := CombatEffectDef.new()
	effect.effect_type = &"damage"
	effect.power = 12
	return effect


func _get_effect_def(effect_defs: Array, effect_type: StringName) -> CombatEffectDef:
	for effect_def in effect_defs:
		var typed_effect := effect_def as CombatEffectDef
		if typed_effect != null and typed_effect.effect_type == effect_type:
			return typed_effect
	return null


func _find_repeat_attack_seed_for_stage_outcomes(
	runtime: BattleRuntimeModule,
	state: BattleState,
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	repeat_effect: CombatEffectDef,
	expected_stage_outcomes: Array[bool]
) -> int:
	if runtime == null or state == null or active_unit == null or target_unit == null or skill_def == null or repeat_effect == null:
		return -1
	for candidate_seed in range(4096):
		state.seed = candidate_seed
		state.attack_roll_nonce = 0
		var matched := true
		for stage_index in range(expected_stage_outcomes.size()):
			var roll_result: Dictionary = runtime.get_hit_resolver().resolve_repeat_attack_stage_hit(
				state,
				active_unit,
				target_unit,
				skill_def,
				repeat_effect,
				stage_index
			)
			if bool(roll_result.get("success", false)) != expected_stage_outcomes[stage_index]:
				matched = false
				break
		if matched:
			state.attack_roll_nonce = 0
			return candidate_seed
	state.attack_roll_nonce = 0
	return -1


func _extract_coord_pairs(coords: Array[Vector2i]) -> Array:
	var pairs: Array = []
	for coord in coords:
		pairs.append([coord.x, coord.y])
	return pairs


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s actual=%s expected=%s" % [message, str(actual), str(expected)])
