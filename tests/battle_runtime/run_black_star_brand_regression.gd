extends SceneTree

const BattleCommand = preload("res://scripts/systems/battle_command.gd")
const BattleCellState = preload("res://scripts/systems/battle_cell_state.gd")
const BattleRuntimeModule = preload("res://scripts/systems/battle_runtime_module.gd")
const BattleState = preload("res://scripts/systems/battle_state.gd")
const BattleTimelineState = preload("res://scripts/systems/battle_timeline_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle_unit_state.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attribute_service.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")

const BLACK_STAR_BRAND_SKILL_ID: StringName = &"black_star_brand"
const WARRIOR_GUARD_SKILL_ID: StringName = &"warrior_guard"
const WARRIOR_HEAVY_STRIKE_SKILL_ID: StringName = &"warrior_heavy_strike"
const STATUS_GUARDING: StringName = &"guarding"
const STATUS_BLACK_STAR_BRAND_NORMAL: StringName = &"black_star_brand_normal"
const STATUS_BLACK_STAR_BRAND_ELITE: StringName = &"black_star_brand_elite"
const STATUS_BLACK_STAR_BRAND_ELITE_GUARD_WINDOW: StringName = &"black_star_brand_elite_guard_window"
const FORTUNE_MARK_TARGET_STAT_ID: StringName = &"fortune_mark_target"

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_black_star_brand_first_cast_free_then_costs_calamity()
	_test_black_star_brand_normal_target_blocks_guard_and_counterattack()
	_test_black_star_brand_elite_target_uses_elite_only_debuffs()

	if _failures.is_empty():
		print("Black star brand regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Black star brand regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_black_star_brand_first_cast_free_then_costs_calamity() -> void:
	var runtime := _build_runtime()
	var black_star_brand = runtime.get_skill_defs().get(BLACK_STAR_BRAND_SKILL_ID)
	_assert_true(black_star_brand != null, "black_star_brand SkillDef 应能从内容注册表加载。")
	if black_star_brand == null:
		return

	var state := _build_skill_test_state(&"black_star_brand_costs", Vector2i(5, 3))
	var caster := _build_unit(&"brand_cost_caster", "施法者", &"player", Vector2i(1, 1), 3, &"hero")
	caster.known_active_skill_ids = [BLACK_STAR_BRAND_SKILL_ID]
	caster.known_skill_level_map = {BLACK_STAR_BRAND_SKILL_ID: 1}
	var enemy := _build_unit(&"brand_cost_enemy", "普通敌人", &"enemy", Vector2i(2, 1), 2)

	_add_unit(runtime, state, caster)
	_add_unit(runtime, state, enemy)
	state.ally_unit_ids = [caster.unit_id]
	state.enemy_unit_ids = [enemy.unit_id]
	state.active_unit_id = caster.unit_id
	runtime._state = state
	_begin_runtime_battle(runtime)

	_assert_eq(runtime.get_black_star_brand_cast_cost(&"hero"), 0, "黑星烙印首次施放应为免费。")
	var first_command := _build_skill_command(caster.unit_id, BLACK_STAR_BRAND_SKILL_ID, enemy)
	var first_preview := runtime.preview_command(first_command)
	_assert_true(first_preview.allowed, "首次免费施放时预览应允许黑星烙印。")
	var first_batch := runtime.issue_command(first_command)
	_assert_true(enemy.has_status_effect(STATUS_BLACK_STAR_BRAND_NORMAL), "首次施放后普通敌人应获得普通黑星烙印。")
	_assert_eq(runtime.get_member_calamity(&"hero"), 0, "首次免费施放后不应扣除 calamity。")
	_assert_eq(runtime.get_black_star_brand_cast_cost(&"hero"), 1, "首次施放后应切换为每次 1 点 calamity。")
	_assert_true(
		first_batch.log_lines.any(func(line): return String(line).contains("无法反击")),
		"首次施放成功后日志应说明普通黑星烙印效果。 log=%s" % [str(first_batch.log_lines)]
	)

	caster.current_ap = 3
	var ap_before_blocked := caster.current_ap
	var blocked_preview := runtime.preview_command(first_command)
	_assert_true(
		not blocked_preview.allowed and String(blocked_preview.log_lines[-1]).contains("calamity 不足"),
		"后续施放在 calamity 为 0 时应被正式拦截。 log=%s" % [str(blocked_preview.log_lines)]
	)
	var blocked_batch := runtime.issue_command(first_command)
	_assert_eq(runtime.get_member_calamity(&"hero"), 0, "因 calamity 不足而失败时不应扣减资源。")
	_assert_eq(caster.current_ap, ap_before_blocked, "因 calamity 不足而失败时不应继续扣除行动点。")
	_assert_true(
		not blocked_batch.changed_unit_ids.has(caster.unit_id),
		"因 calamity 不足而失败时不应把施法者记录为已执行变更。"
	)

	runtime.calamity_by_member_id[&"hero"] = 2
	caster.current_ap = 3
	var spend_preview := runtime.preview_command(first_command)
	_assert_true(spend_preview.allowed, "有足够 calamity 时后续施放应允许。")
	runtime.issue_command(first_command)
	_assert_eq(runtime.get_member_calamity(&"hero"), 1, "后续成功施放黑星烙印后应只扣除 1 点 calamity。")


func _test_black_star_brand_normal_target_blocks_guard_and_counterattack() -> void:
	var runtime := _build_runtime()
	var state := _build_skill_test_state(&"black_star_brand_normal", Vector2i(5, 3))
	var caster := _build_unit(&"brand_normal_caster", "施法者", &"player", Vector2i(1, 1), 3, &"hero")
	caster.known_active_skill_ids = [BLACK_STAR_BRAND_SKILL_ID]
	caster.known_skill_level_map = {BLACK_STAR_BRAND_SKILL_ID: 1}
	var enemy := _build_unit(&"brand_normal_enemy", "普通敌人", &"enemy", Vector2i(2, 1), 2)
	enemy.current_stamina = 2
	enemy.known_active_skill_ids = [WARRIOR_GUARD_SKILL_ID]
	enemy.known_skill_level_map = {WARRIOR_GUARD_SKILL_ID: 1}
	enemy.status_effects[STATUS_GUARDING] = {
		"status_id": STATUS_GUARDING,
		"power": 1,
		"duration": 60,
	}

	_add_unit(runtime, state, caster)
	_add_unit(runtime, state, enemy)
	state.ally_unit_ids = [caster.unit_id]
	state.enemy_unit_ids = [enemy.unit_id]
	state.active_unit_id = caster.unit_id
	runtime._state = state
	_begin_runtime_battle(runtime)

	var brand_command := _build_skill_command(caster.unit_id, BLACK_STAR_BRAND_SKILL_ID, enemy)
	runtime.issue_command(brand_command)

	_assert_true(enemy.has_status_effect(STATUS_BLACK_STAR_BRAND_NORMAL), "普通敌人应获得普通黑星烙印状态。")
	_assert_true(not enemy.has_status_effect(STATUS_GUARDING), "普通黑星烙印应立即打断已有 guarding。")
	_assert_true(runtime.is_unit_guard_locked(enemy), "普通黑星烙印应封锁后续格挡。")
	_assert_true(runtime.is_unit_counterattack_locked(enemy), "普通黑星烙印应标记为无法反击。")

	state.active_unit_id = enemy.unit_id
	state.phase = &"unit_acting"
	enemy.current_ap = 2
	enemy.current_stamina = 2
	var guard_command := _build_skill_command(enemy.unit_id, WARRIOR_GUARD_SKILL_ID, enemy)
	var guard_preview := runtime.preview_command(guard_command)
	_assert_true(
		not guard_preview.allowed and String(guard_preview.log_lines[-1]).contains("封锁了格挡"),
		"普通黑星烙印下预览 warrior_guard 应被阻断。 log=%s" % [str(guard_preview.log_lines)]
	)
	var ap_before_issue := enemy.current_ap
	runtime.issue_command(guard_command)
	_assert_eq(enemy.current_ap, ap_before_issue, "被普通黑星烙印封锁时不应继续扣除格挡技能的行动点。")
	_assert_true(not enemy.has_status_effect(STATUS_GUARDING), "被普通黑星烙印封锁时不应重新获得 guarding。")


func _test_black_star_brand_elite_target_uses_elite_only_debuffs() -> void:
	var runtime := _build_runtime()
	var heavy_strike = runtime.get_skill_defs().get(WARRIOR_HEAVY_STRIKE_SKILL_ID)
	_assert_true(heavy_strike != null, "elite case 前置：warrior_heavy_strike 定义应存在。")
	if heavy_strike == null:
		return

	var state := _build_skill_test_state(&"black_star_brand_elite", Vector2i(6, 3))
	var caster := _build_unit(&"brand_elite_caster", "施法者", &"player", Vector2i(1, 1), 3, &"hero")
	caster.known_active_skill_ids = [BLACK_STAR_BRAND_SKILL_ID]
	caster.known_skill_level_map = {BLACK_STAR_BRAND_SKILL_ID: 1}
	var elite := _build_unit(&"brand_elite_target", "精英敌人", &"enemy", Vector2i(2, 1), 2, &"", true)
	elite.current_stamina = 2
	elite.known_active_skill_ids = [WARRIOR_GUARD_SKILL_ID, WARRIOR_HEAVY_STRIKE_SKILL_ID]
	elite.known_skill_level_map = {WARRIOR_GUARD_SKILL_ID: 1, WARRIOR_HEAVY_STRIKE_SKILL_ID: 1}
	var ally_target := _build_unit(&"brand_elite_ally_target", "被打击者", &"player", Vector2i(3, 1), 2)

	_add_unit(runtime, state, caster)
	_add_unit(runtime, state, elite)
	_add_unit(runtime, state, ally_target)
	state.ally_unit_ids = [caster.unit_id, ally_target.unit_id]
	state.enemy_unit_ids = [elite.unit_id]
	state.active_unit_id = caster.unit_id
	runtime._state = state
	_begin_runtime_battle(runtime)

	var brand_command := _build_skill_command(caster.unit_id, BLACK_STAR_BRAND_SKILL_ID, elite)
	runtime.issue_command(brand_command)
	_assert_true(elite.has_status_effect(STATUS_BLACK_STAR_BRAND_ELITE), "elite 目标应获得专属黑星烙印状态。")
	_assert_true(elite.has_status_effect(STATUS_BLACK_STAR_BRAND_ELITE_GUARD_WINDOW), "elite 目标应持有首次受击穿透 guard 的窗口状态。")
	_assert_true(not runtime.is_unit_guard_locked(elite), "elite 黑星烙印不应沿用普通目标的格挡封锁。")
	_assert_true(not runtime.is_unit_counterattack_locked(elite), "elite 黑星烙印不应沿用普通目标的反击封锁。")

	state.active_unit_id = elite.unit_id
	state.phase = &"unit_acting"
	elite.current_ap = 2
	elite.current_stamina = 2
	var guard_command := _build_skill_command(elite.unit_id, WARRIOR_GUARD_SKILL_ID, elite)
	var guard_preview := runtime.preview_command(guard_command)
	_assert_true(guard_preview.allowed, "elite 黑星烙印下 warrior_guard 仍应允许施放。")
	runtime.issue_command(guard_command)
	_assert_true(elite.has_status_effect(STATUS_GUARDING), "elite 黑星烙印不应阻止目标进入 guarding。")

	var first_hit_result: Dictionary = runtime.get_damage_resolver().resolve_effects(caster, elite, [_build_damage_effect()])
	var first_event: Dictionary = _extract_first_damage_event(first_hit_result)
	var second_hit_result: Dictionary = runtime.get_damage_resolver().resolve_effects(caster, elite, [_build_damage_effect()])
	_assert_true(
		int(first_hit_result.get("damage", 0)) > int(second_hit_result.get("damage", 0)),
		"elite 黑星烙印的第一次受击应比后续同条件受击承受更高伤害。 first=%s second=%s" % [
			str(first_hit_result),
			str(second_hit_result),
		]
	)
	_assert_true(
		int(first_event.get("guard_ignore_applied", 0)) > 0,
		"elite 黑星烙印的首次受击应记录 guard_ignore_applied。 event=%s" % [str(first_event)]
	)
	_assert_true(
		not elite.has_status_effect(STATUS_BLACK_STAR_BRAND_ELITE_GUARD_WINDOW),
		"elite 黑星烙印的首次受击窗口应在第一下结算后被消耗。"
	)


func _extract_first_damage_event(result: Dictionary) -> Dictionary:
	var damage_events = result.get("damage_events", [])
	if damage_events is Array and not damage_events.is_empty() and damage_events[0] is Dictionary:
		return damage_events[0] as Dictionary
	return {}


func _build_runtime() -> BattleRuntimeModule:
	var registry := ProgressionContentRegistry.new()
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})
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
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.AURA_MAX, 2)
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


func _build_skill_command(unit_id: StringName, skill_id: StringName, target_unit: BattleUnitState) -> BattleCommand:
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


func _add_unit(runtime: BattleRuntimeModule, state: BattleState, unit: BattleUnitState) -> void:
	state.units[unit.unit_id] = unit
	runtime._grid_service.place_unit(state, unit, unit.coord, true)


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s actual=%s expected=%s" % [message, str(actual), str(expected)])
