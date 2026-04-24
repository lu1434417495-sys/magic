extends SceneTree

const BattleCommand = preload("res://scripts/systems/battle_command.gd")
const BattleRuntimeModule = preload("res://scripts/systems/battle_runtime_module.gd")
const BattleGridService = preload("res://scripts/systems/battle_grid_service.gd")
const BattleCellState = preload("res://scripts/systems/battle_cell_state.gd")
const BattleState = preload("res://scripts/systems/battle_state.gd")
const BattleTimelineState = preload("res://scripts/systems/battle_timeline_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle_unit_state.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attribute_service.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_whirlwind_slash_path_aoe_can_repeat_hits_across_steps()
	_test_whirlwind_slash_runtime_repeats_hits_across_steps()
	_test_saint_blade_combo_contract_requires_hit_follow_up_and_single_cost_settlement()
	_test_saint_blade_combo_runtime_stops_on_insufficient_aura_after_successful_follow_up()
	_test_saint_blade_combo_runtime_consumes_follow_up_aura_on_miss()
	if _failures.is_empty():
		print("Warrior advanced skill regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Warrior advanced skill regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_whirlwind_slash_path_aoe_can_repeat_hits_across_steps() -> void:
	var skill_def := _get_skill_def(&"warrior_whirlwind_slash")
	_assert_true(skill_def != null, "旋风斩技能定义应存在。")
	if skill_def == null:
		return

	var cast_variant := skill_def.combat_profile.get_cast_variant(&"whirlwind_charge")
	_assert_true(cast_variant != null, "旋风斩应保留 whirlwind_charge 施放变体。")
	if cast_variant == null:
		return

	var path_step_aoe := _get_effect_def(cast_variant.effect_defs, &"path_step_aoe")
	_assert_true(path_step_aoe != null, "旋风斩应声明路径 AOE 效果。")
	if path_step_aoe == null:
		return
	_assert_true(
		bool(path_step_aoe.params.get("allow_repeat_hits_across_steps", false)),
		"旋风斩路径 AOE 应允许同一目标在不同步段被重复命中。"
	)
	_assert_true(
		bool(path_step_aoe.params.get("apply_on_successful_step_only", false)),
		"旋风斩路径 AOE 应只在成功前进一步时触发。"
	)

	var state := _build_state(Vector2i(5, 3))
	var grid := BattleGridService.new()
	var step_centers: Array[Vector2i] = [Vector2i(1, 1), Vector2i(2, 1), Vector2i(3, 1)]
	var repeated_target := Vector2i(2, 1)
	var repeated_hit_steps := 0
	for step_center in step_centers:
		var step_coords := grid.get_area_coords(state, step_center, &"diamond", 1)
		if step_coords.has(repeated_target):
			repeated_hit_steps += 1
	_assert_true(
		repeated_hit_steps >= 2,
		"旋风斩的路径 AOE 应允许同一敌人在不同成功步段重复进入命中范围。 actual=%d" % repeated_hit_steps
	)


func _test_saint_blade_combo_contract_requires_hit_follow_up_and_single_cost_settlement() -> void:
	var skill_def := _get_skill_def(&"saint_blade_combo")
	_assert_true(skill_def != null, "圣剑连斩技能定义应存在。")
	if skill_def == null:
		return

	_assert_true(skill_def.combat_profile != null, "圣剑连斩应带有战斗配置。")
	if skill_def.combat_profile == null:
		return

	var repeat_effect := _get_effect_def(skill_def.combat_profile.effect_defs, &"repeat_attack_until_fail")
	_assert_true(repeat_effect != null, "圣剑连斩应声明 repeat_attack_until_fail 效果。")
	if repeat_effect == null:
		return

	_assert_true(
		bool(repeat_effect.params.get("same_target_only", false)),
		"圣剑连斩应只对同一目标继续追击。"
	)
	_assert_true(
		bool(repeat_effect.params.get("stop_on_miss", false)),
		"圣剑连斩应在未命中时停止追击。"
	)
	_assert_true(
		bool(repeat_effect.params.get("stop_on_insufficient_resource", false)),
		"圣剑连斩应在 Aura 不足时停止追击。"
	)
	_assert_true(
		not repeat_effect.params.has("consume_cost_on_attempt"),
		"圣剑连斩的追击扣费语义已固定为每次尝试扣费，不应再暴露 consume_cost_on_attempt 配置。"
	)
	_assert_true(
		bool(repeat_effect.params.get("stop_on_target_down", false)),
		"圣剑连斩应在目标倒下时停止追击。"
	)
	_assert_true(
		int(repeat_effect.params.get("follow_up_attack_penalty", 0)) > 0,
		"圣剑连斩的后续追击应带命中惩罚。"
	)
	_assert_true(skill_def.combat_profile.ap_cost > 0, "圣剑连斩应具备基础 AP 消耗。")
	_assert_true(skill_def.combat_profile.cooldown_tu > 0, "圣剑连斩应具备基础 CD。")
	_assert_true(skill_def.combat_profile.aura_cost > 0, "圣剑连斩应消耗 Aura。")
	_assert_eq(String(repeat_effect.params.get("cost_resource", "")), "aura", "圣剑连斩的追击资源应只走 Aura。")
	_assert_true(not repeat_effect.params.has("ap_cost"), "圣剑连斩的追击层不应重复结算 AP。")
	_assert_true(not repeat_effect.params.has("cooldown_tu"), "圣剑连斩的追击层不应重复结算 CD。")


func _test_whirlwind_slash_runtime_repeats_hits_across_steps() -> void:
	var runtime := _build_runtime()
	var state := _build_state(Vector2i(6, 3))
	state.timeline = BattleTimelineState.new()
	var warrior := _build_unit(&"whirlwind_user", Vector2i(0, 1), 2)
	warrior.current_stamina = 3
	warrior.current_aura = 2
	warrior.known_active_skill_ids = [&"warrior_whirlwind_slash"]
	warrior.known_skill_level_map = {&"warrior_whirlwind_slash": 1}
	var repeated_target := _build_unit(&"whirlwind_repeat_target", Vector2i(2, 1), 2)
	repeated_target.faction_id = &"enemy"
	var far_target := _build_unit(&"whirlwind_far_target", Vector2i(5, 1), 2)
	far_target.faction_id = &"enemy"

	_add_unit(runtime, state, warrior)
	_add_unit(runtime, state, repeated_target)
	_add_unit(runtime, state, far_target)
	state.ally_unit_ids = [warrior.unit_id]
	state.enemy_unit_ids = [repeated_target.unit_id, far_target.unit_id]
	state.active_unit_id = warrior.unit_id
	runtime._state = state

	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = warrior.unit_id
	command.skill_id = &"warrior_whirlwind_slash"
	command.skill_variant_id = &"whirlwind_charge"
	command.target_coord = Vector2i(3, 1)

	var hp_before := repeated_target.current_hp
	var batch := runtime.issue_command(command)
	_assert_eq(warrior.coord, Vector2i(3, 1), "旋风斩执行后施法者应停在最终冲锋落点。")
	_assert_true(repeated_target.current_hp <= hp_before - 20, "旋风斩应让同一目标在不同步段被重复命中。 before=%d after=%d" % [hp_before, repeated_target.current_hp])
	_assert_true(
		batch != null and batch.log_lines.any(func(line): return String(line).contains("沿途触发")),
		"旋风斩日志应汇总沿途旋斩触发次数。 log=%s" % [str(batch.log_lines)]
	)


func _test_saint_blade_combo_runtime_stops_on_insufficient_aura_after_successful_follow_up() -> void:
	var runtime := _build_runtime()
	var state := _build_state(Vector2i(5, 3))
	state.timeline = BattleTimelineState.new()
	var skill_def := _get_skill_def(&"saint_blade_combo")
	_assert_true(skill_def != null and skill_def.combat_profile != null, "圣剑连斩成功回归需要有效技能定义。")
	if skill_def == null or skill_def.combat_profile == null:
		return
	var repeat_effect := _get_effect_def(skill_def.combat_profile.effect_defs, &"repeat_attack_until_fail")
	_assert_true(repeat_effect != null, "圣剑连斩成功回归需要 repeat_attack_until_fail。")
	if repeat_effect == null:
		return
	var warrior := _build_unit(&"saint_blade_user", Vector2i(1, 1), 2)
	warrior.current_aura = 3
	warrior.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 100)
	warrior.known_active_skill_ids = [&"saint_blade_combo"]
	warrior.known_skill_level_map = {&"saint_blade_combo": 1}
	var enemy := _build_unit(&"saint_blade_target", Vector2i(2, 1), 2)
	enemy.faction_id = &"enemy"
	enemy.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, -10)

	_add_unit(runtime, state, warrior)
	_add_unit(runtime, state, enemy)
	state.ally_unit_ids = [warrior.unit_id]
	state.enemy_unit_ids = [enemy.unit_id]
	state.active_unit_id = warrior.unit_id
	runtime._state = state
	var success_seed := _find_repeat_attack_seed_for_stage_outcomes(
		runtime,
		state,
		warrior,
		enemy,
		skill_def,
		repeat_effect,
		[true, true]
	)
	_assert_true(success_seed >= 0, "应能为圣剑连斩找到稳定的前两段命中 seed。")
	if success_seed < 0:
		return
	state.seed = success_seed
	state.attack_roll_nonce = 0

	var hp_before := enemy.current_hp
	var command := _build_unit_skill_command(warrior.unit_id, &"saint_blade_combo", enemy)
	var batch := runtime.issue_command(command)
	_assert_eq(warrior.current_ap, 0, "圣剑连斩整次技能只应结算一次 AP。")
	_assert_eq(warrior.current_aura, 0, "圣剑连斩在前两段命中后应扣除 1 + 2 点 Aura。")
	_assert_true(enemy.current_hp <= hp_before - 36, "圣剑连斩应至少完成两段伤害。 before=%d after=%d" % [hp_before, enemy.current_hp])
	_assert_true(warrior.cooldowns.has(&"saint_blade_combo"), "圣剑连斩整次技能应只写入一次冷却。")
	_assert_eq(int(warrior.cooldowns.get(&"saint_blade_combo", 0)), 15, "圣剑连斩冷却值应保持基础配置。")
	_assert_true(
		batch != null and batch.log_lines.any(func(line): return String(line).contains("斗气不足")),
		"圣剑连斩 Aura 不足时应记录终止原因。 log=%s" % [str(batch.log_lines)]
	)


func _test_saint_blade_combo_runtime_consumes_follow_up_aura_on_miss() -> void:
	var runtime := _build_runtime()
	var state := _build_state(Vector2i(5, 3))
	state.timeline = BattleTimelineState.new()
	var skill_def := _get_skill_def(&"saint_blade_combo")
	_assert_true(skill_def != null and skill_def.combat_profile != null, "圣剑连斩未命中回归需要有效技能定义。")
	if skill_def == null or skill_def.combat_profile == null:
		return
	var repeat_effect := _get_effect_def(skill_def.combat_profile.effect_defs, &"repeat_attack_until_fail")
	_assert_true(repeat_effect != null, "圣剑连斩未命中回归需要 repeat_attack_until_fail。")
	if repeat_effect == null:
		return
	var warrior := _build_unit(&"saint_blade_miss_user", Vector2i(1, 1), 2)
	warrior.current_aura = 3
	warrior.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 100)
	warrior.known_active_skill_ids = [&"saint_blade_combo"]
	warrior.known_skill_level_map = {&"saint_blade_combo": 1}
	var enemy := _build_unit(&"saint_blade_miss_target", Vector2i(2, 1), 2)
	enemy.faction_id = &"enemy"
	enemy.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)

	_add_unit(runtime, state, warrior)
	_add_unit(runtime, state, enemy)
	state.ally_unit_ids = [warrior.unit_id]
	state.enemy_unit_ids = [enemy.unit_id]
	state.active_unit_id = warrior.unit_id
	runtime._state = state
	var forced_miss_seed := _find_repeat_attack_seed_for_stage_outcomes(
		runtime,
		state,
		warrior,
		enemy,
		skill_def,
		repeat_effect,
		[true, false]
	)
	_assert_true(forced_miss_seed >= 0, "应能为圣剑连斩找到首段命中、第二段 miss 的 battle seed。")
	if forced_miss_seed < 0:
		return
	state.seed = forced_miss_seed
	state.attack_roll_nonce = 0

	var hp_before := enemy.current_hp
	var command := _build_unit_skill_command(warrior.unit_id, &"saint_blade_combo", enemy)
	var preview := runtime.preview_command(command)
	var stage_preview_texts := preview.hit_preview.get("stage_preview_texts", []) as Array
	_assert_eq(stage_preview_texts.size(), 2, "圣剑连斩预览应按当前 Aura 暴露可支付的 shared resolver 文案。")
	_assert_eq(
		preview.hit_preview.get("stage_required_rolls", []),
		[2, 3],
		"命中预览应按当前 Aura 上限把 100 命中/0 闪避夹具换算为 d20 required roll。"
	)
	var batch := runtime.issue_command(command)
	_assert_eq(warrior.current_aura, 0, "圣剑连斩第二段即使未命中也应扣除尝试所需 Aura。")
	_assert_true(enemy.current_hp == hp_before - 12, "圣剑连斩第二段未命中时应只保留首段伤害。 before=%d after=%d" % [hp_before, enemy.current_hp])
	_assert_true(
		batch != null and batch.log_lines.any(func(line): return String(line).contains("未命中")),
		"圣剑连斩未命中时应写入失败日志。 log=%s" % [str(batch.log_lines)]
	)
	_assert_true(
		batch != null and batch.log_lines.any(func(line): return String(line).contains("d20=")),
		"圣剑连斩 battle log 应记录 d20 明细。 log=%s" % [str(batch.log_lines)]
	)
	if stage_preview_texts.size() >= 2:
		_assert_true(
			batch != null and batch.log_lines.any(func(line): return String(line).contains(String(stage_preview_texts[1]))),
			"圣剑连斩 battle log 应复用 preview 的第二段命中文案。 preview=%s log=%s" % [str(stage_preview_texts), str(batch.log_lines)]
		)


func _get_skill_def(skill_id: StringName) -> SkillDef:
	var registry := ProgressionContentRegistry.new()
	var skill_defs: Dictionary = registry.get_skill_defs()
	return skill_defs.get(skill_id) as SkillDef


func _get_effect_def(effect_defs: Array, effect_type: StringName) -> CombatEffectDef:
	for effect_def in effect_defs:
		var typed_effect := effect_def as CombatEffectDef
		if typed_effect != null and typed_effect.effect_type == effect_type:
			return typed_effect
	return null


func _build_state(map_size: Vector2i) -> BattleState:
	var state := BattleState.new()
	state.battle_id = &"warrior_advanced_skill_regression"
	state.phase = &"unit_acting"
	state.map_size = map_size
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


func _build_runtime() -> BattleRuntimeModule:
	var registry := ProgressionContentRegistry.new()
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})
	return runtime


func _build_unit(unit_id: StringName, coord: Vector2i, current_ap: int) -> BattleUnitState:
	var unit := BattleUnitState.new()
	unit.unit_id = unit_id
	unit.display_name = String(unit_id)
	unit.faction_id = &"player"
	unit.current_ap = current_ap
	unit.current_hp = 40
	unit.current_mp = 4
	unit.current_stamina = 0
	unit.current_aura = 0
	unit.is_alive = true
	unit.set_anchor_coord(coord)
	unit.attribute_snapshot.set_value(&"hp_max", 40)
	unit.attribute_snapshot.set_value(&"mp_max", 4)
	unit.attribute_snapshot.set_value(&"stamina_max", 4)
	unit.attribute_snapshot.set_value(&"aura_max", 8)
	unit.attribute_snapshot.set_value(&"action_points", maxi(current_ap, 1))
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 12)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 4)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 6)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 4)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 80)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 5)
	return unit


func _add_unit(runtime: BattleRuntimeModule, state: BattleState, unit: BattleUnitState) -> void:
	state.units[unit.unit_id] = unit
	runtime._grid_service.place_unit(state, unit, unit.coord, true)


func _build_unit_skill_command(unit_id: StringName, skill_id: StringName, target_unit: BattleUnitState) -> BattleCommand:
	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = unit_id
	command.skill_id = skill_id
	command.target_unit_id = target_unit.unit_id
	command.target_coord = target_unit.coord
	return command


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
			var roll_result: Dictionary = runtime._hit_resolver.resolve_repeat_attack_stage_hit(
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


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s actual=%s expected=%s" % [message, str(actual), str(expected)])
