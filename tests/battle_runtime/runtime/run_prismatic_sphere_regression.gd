extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const BattleRuntimeModule = preload("res://scripts/systems/battle/runtime/battle_runtime_module.gd")
const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleTimelineState = preload("res://scripts/systems/battle/core/battle_timeline_state.gd")
const BattleCellState = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BattleEventBatch = preload("res://scripts/systems/battle/core/battle_event_batch.gd")
const BattleStatusEffectState = preload("res://scripts/systems/battle/core/battle_status_effect_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_prismatic_sphere_creates_ordered_layers()
	_test_prismatic_sphere_blocks_deeper_breakers_until_outer_layer_breaks()
	_test_projected_effect_barrier_geometry_respects_boundary()
	_test_green_layer_instant_death_uses_fatal_damage_chain()
	_test_petrified_blocks_turn_until_self_save_succeeds()
	_test_violet_layer_teleports_non_summons_and_removes_summons()
	_test_cleanse_harmful_removes_madness_but_not_petrified()
	_test_dispel_magic_removes_magic_statuses_by_relation()
	if _failures.is_empty():
		print("Prismatic sphere regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Prismatic sphere regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_prismatic_sphere_creates_ordered_layers() -> void:
	var fixture := _build_runtime_with_sphere()
	var state: BattleState = fixture["state"]
	var barrier := _first_barrier(state)
	_assert_true(not barrier.is_empty(), "虹光法球应写入 battle_state.layered_barrier_fields。")
	_assert_eq(_get_active_layer_id(barrier), &"red", "新建虹光法球的第一活动层应为红色层。")
	_assert_eq((barrier.get("layers", []) as Array).size(), 7, "虹光法球应包含 7 层。")


func _test_prismatic_sphere_blocks_deeper_breakers_until_outer_layer_breaks() -> void:
	var fixture := _build_runtime_with_sphere()
	var runtime: BattleRuntimeModule = fixture["runtime"]
	var state: BattleState = fixture["state"]
	var caster: BattleUnitState = fixture["caster"]
	var enemy: BattleUnitState = fixture["enemy"]
	var batch := BattleEventBatch.new()

	var magic_missile := _build_skill(&"mage_arcane_missile", "奥术飞弹", [&"mage", &"magic"])
	var blocked_result: Dictionary = runtime._layered_barrier_service.resolve_skill_barrier_interaction(
		enemy,
		caster,
		magic_missile,
		[],
		batch
	)
	_assert_true(bool(blocked_result.get("blocked", false)), "外层仍在时，蓝色层破解法术应被阻挡。")
	_assert_eq(_get_active_layer_id(_first_barrier(state)), &"red", "错误顺序的破解不应破坏红色层。")

	var cone_of_cold := _build_skill(&"mage_cone_of_cold", "寒冰锥", [&"mage", &"magic", &"freeze"])
	var break_result: Dictionary = runtime._layered_barrier_service.resolve_skill_barrier_interaction(
		enemy,
		caster,
		cone_of_cold,
		[],
		batch
	)
	_assert_true(bool(break_result.get("blocked", false)), "正确破解法术应被法球消耗。")
	_assert_eq(_get_active_layer_id(_first_barrier(state)), &"orange", "寒冰锥应只破坏最外侧红色层。")

	var gust_of_wind := _build_skill(&"mage_gust_of_wind", "强风术", [&"mage", &"magic", &"air"])
	var orange_break_result: Dictionary = runtime._layered_barrier_service.resolve_skill_barrier_interaction(
		enemy,
		caster,
		gust_of_wind,
		[],
		batch
	)
	_assert_true(bool(orange_break_result.get("blocked", false)), "强风术应被虹光法球消耗以破解橙色层。")
	_assert_eq(_get_active_layer_id(_first_barrier(state)), &"yellow", "强风术应在红层破除后破解橙色层。")

	var disintegrate := _build_skill(&"mage_spell_disjunction", "裂解术", [&"mage", &"magic", &"arcane"])
	var yellow_break_result: Dictionary = runtime._layered_barrier_service.resolve_skill_barrier_interaction(
		enemy,
		caster,
		disintegrate,
		[],
		batch
	)
	_assert_true(bool(yellow_break_result.get("blocked", false)), "裂解术应被虹光法球消耗以破解黄色层。")
	_assert_eq(_get_active_layer_id(_first_barrier(state)), &"green", "裂解术应在橙层破除后破解黄色层。")

	var passwall := _build_skill(&"mage_passwall", "穿墙术", [&"mage", &"magic", &"earth"])
	var green_break_result: Dictionary = runtime._layered_barrier_service.resolve_skill_barrier_interaction(
		enemy,
		caster,
		passwall,
		[],
		batch
	)
	_assert_true(bool(green_break_result.get("blocked", false)), "穿墙术应被虹光法球消耗以破解绿色层。")
	_assert_eq(_get_active_layer_id(_first_barrier(state)), &"blue", "穿墙术应在黄层破除后破解绿色层。")

	var blue_break_result: Dictionary = runtime._layered_barrier_service.resolve_skill_barrier_interaction(
		enemy,
		caster,
		magic_missile,
		[],
		batch
	)
	_assert_true(bool(blue_break_result.get("blocked", false)), "奥术飞弹应被虹光法球消耗以破解蓝色层。")
	_assert_eq(_get_active_layer_id(_first_barrier(state)), &"indigo", "奥术飞弹应在绿层破除后破解蓝色层。")

	var continual_light := _build_skill(&"mage_continual_light", "恒光术", [&"mage", &"magic", &"radiant"])
	var indigo_break_result: Dictionary = runtime._layered_barrier_service.resolve_skill_barrier_interaction(
		enemy,
		caster,
		continual_light,
		[],
		batch
	)
	_assert_true(bool(indigo_break_result.get("blocked", false)), "恒光术应被虹光法球消耗以破解靛色层。")
	_assert_eq(_get_active_layer_id(_first_barrier(state)), &"violet", "恒光术应在蓝层破除后破解靛色层。")

	var dispel_magic := _build_skill(&"mage_dispel_magic", "解除魔法", [&"mage", &"magic", &"dispel"])
	var violet_break_result: Dictionary = runtime._layered_barrier_service.resolve_skill_barrier_interaction(
		enemy,
		caster,
		dispel_magic,
		[],
		batch
	)
	_assert_true(bool(violet_break_result.get("blocked", false)), "解除魔法应被虹光法球消耗以破解紫色层。")
	_assert_eq(_get_active_layer_id(_first_barrier(state)), &"", "解除魔法应在靛层破除后破解最后的紫色层。")


func _test_projected_effect_barrier_geometry_respects_boundary() -> void:
	var fixture := _build_runtime_with_sphere()
	var runtime: BattleRuntimeModule = fixture["runtime"]
	var state: BattleState = fixture["state"]
	var barrier := _first_barrier(state)

	_assert_true(
		not runtime._layered_barrier_service._projected_effect_crosses_barrier(Vector2i(2, 2), Vector2i(3, 2), barrier),
		"法球内部到内部的投射效果不应被屏障拦截。"
	)
	_assert_true(
		runtime._layered_barrier_service._projected_effect_crosses_barrier(Vector2i(2, 2), Vector2i(5, 2), barrier),
		"法球内部到外部的投射效果应被屏障拦截。"
	)
	_assert_true(
		runtime._layered_barrier_service._projected_effect_crosses_barrier(Vector2i(5, 2), Vector2i(2, 2), barrier),
		"法球外部到内部的投射效果应被屏障拦截。"
	)
	_assert_true(
		runtime._layered_barrier_service._projected_effect_crosses_barrier(Vector2i(5, 2), Vector2i(-1, 2), barrier),
		"法球外部到外部但线段穿过屏障时应被拦截。"
	)
	_assert_true(
		not runtime._layered_barrier_service._projected_effect_crosses_barrier(Vector2i(5, 4), Vector2i(6, 4), barrier),
		"法球外部到外部且未穿过屏障时不应被拦截。"
	)


func _test_green_layer_instant_death_uses_fatal_damage_chain() -> void:
	var fixture := _build_runtime_with_sphere()
	var runtime: BattleRuntimeModule = fixture["runtime"]
	var state: BattleState = fixture["state"]
	var enemy: BattleUnitState = fixture["enemy"]
	var last_stand_skill = load("res://data/configs/skills/warrior_last_stand.tres")
	_assert_true(last_stand_skill != null and last_stand_skill.combat_profile != null, "绿色层即死回归需要 warrior_last_stand 技能资源。")
	if last_stand_skill == null or last_stand_skill.combat_profile == null:
		return
	runtime.get_damage_resolver().set_skill_defs({&"warrior_last_stand": last_stand_skill})
	enemy.current_hp = 8
	_set_status(enemy, &"death_ward", {
		"source_skill_id": "warrior_last_stand",
		"skill_level": 7,
	})
	_set_status(enemy, &"staggered")
	_mark_layers_broken(state, [&"red", &"orange", &"yellow", &"blue", &"indigo", &"violet"])
	_set_layer_save_roll_override(state, &"green", 1)

	var result: Dictionary = runtime._layered_barrier_service.resolve_unit_boundary_crossing(
		enemy,
		Vector2i(5, 2),
		Vector2i(4, 2),
		BattleEventBatch.new()
	)
	_assert_true(not bool(result.get("blocked", false)), "不屈抵消绿色层即死后，穿越不应因死亡终止。")
	_assert_true(enemy.is_alive and enemy.current_hp > 0, "绿色层即死应触发现有免死链并把目标救回正 HP。")
	_assert_true(not enemy.has_status_effect(&"death_ward"), "绿色层即死触发不屈后应消耗 death_ward。")
	_assert_true(not enemy.has_status_effect(&"staggered"), "Lv5+ 不屈触发后仍应清理负面状态。")
	_assert_true(enemy.has_status_effect(&"last_stand_active"), "Lv7 不屈触发后应保留 last_stand_active。")


func _test_petrified_blocks_turn_until_self_save_succeeds() -> void:
	var fixture := _build_runtime_with_sphere()
	var runtime: BattleRuntimeModule = fixture["runtime"]
	var target: BattleUnitState = fixture["enemy"]
	var batch := BattleEventBatch.new()
	var petrified := BattleStatusEffectState.new()
	petrified.status_id = &"petrified"
	petrified.source_unit_id = &"caster"
	petrified.power = 1
	petrified.stacks = 1
	petrified.duration = -1
	petrified.params = {
		"self_save_ability": "constitution",
		"self_save_dc": 15,
		"self_save_roll_override": 1,
		"self_save_tag": "constitution"
	}
	target.set_status_effect(petrified)

	var fail_result: Dictionary = runtime._skill_turn_resolver.resolve_turn_control_status(target, batch)
	_assert_true(bool(fail_result.get("skip_turn", false)), "石化自检失败应跳过行动。")
	_assert_true(target.has_status_effect(&"petrified"), "石化失败后状态应保留。")

	var entry := target.get_status_effect(&"petrified") as BattleStatusEffectState
	entry.params["self_save_roll_override"] = 20
	target.set_status_effect(entry)
	var success_result: Dictionary = runtime._skill_turn_resolver.resolve_turn_control_status(target, batch)
	_assert_true(not bool(success_result.get("skip_turn", false)), "石化自检成功应允许本次行动继续。")
	_assert_true(not target.has_status_effect(&"petrified"), "石化自检成功应解除石化。")


func _test_violet_layer_teleports_non_summons_and_removes_summons() -> void:
	var fixture := _build_runtime_with_sphere()
	var runtime: BattleRuntimeModule = fixture["runtime"]
	var state: BattleState = fixture["state"]
	var enemy: BattleUnitState = fixture["enemy"]
	var batch := BattleEventBatch.new()
	_mark_layers_broken(state, [&"red", &"orange", &"yellow", &"green", &"blue", &"indigo"])
	_set_layer_save_roll_override(state, &"violet", 1)

	var result: Dictionary = runtime._layered_barrier_service.resolve_unit_boundary_crossing(
		enemy,
		Vector2i(5, 2),
		Vector2i(4, 2),
		batch
	)
	_assert_true(bool(result.get("blocked", false)), "紫色层放逐应终止本次穿越。")
	_assert_true(enemy.is_alive, "非召唤物被紫色层命中后应保留存活状态。")
	_assert_true(not runtime._layered_barrier_service._is_coord_inside_barrier(enemy.coord, _first_barrier(state)), "非召唤物应被传送到法球外合法坐标。")

	var summon := _build_unit(&"summon", "召唤物", &"enemy", Vector2i(6, 2))
	summon.ai_blackboard["summoned"] = true
	_add_unit(runtime, state, summon, true)
	var summon_result: Dictionary = runtime._layered_barrier_service.resolve_unit_boundary_crossing(
		summon,
		Vector2i(6, 2),
		Vector2i(4, 2),
		batch
	)
	_assert_true(bool(summon_result.get("blocked", false)), "召唤物被放逐也应终止穿越。")
	_assert_true(not summon.is_alive, "召唤物应被紫色层直接移除。")


func _test_cleanse_harmful_removes_madness_but_not_petrified() -> void:
	var source := _build_unit(&"source", "施法者", &"player", Vector2i.ZERO)
	var target := _build_unit(&"target", "目标", &"player", Vector2i(1, 0))
	_set_status(target, &"madness")
	_set_status(target, &"petrified")
	var cleanse := CombatEffectDef.new()
	cleanse.effect_type = &"cleanse_harmful"
	var resolver = preload("res://scripts/systems/battle/rules/battle_damage_resolver.gd").new()
	resolver.resolve_effects(source, target, [cleanse])
	_assert_true(not target.has_status_effect(&"madness"), "cleanse_harmful 应解除 madness。")
	_assert_true(target.has_status_effect(&"petrified"), "cleanse_harmful 不应解除 petrified。")


func _test_dispel_magic_removes_magic_statuses_by_relation() -> void:
	var source := _build_unit(&"source", "施法者", &"player", Vector2i.ZERO)
	var ally := _build_unit(&"ally", "友方", &"player", Vector2i(1, 0))
	var enemy := _build_unit(&"enemy", "敌方", &"enemy", Vector2i(2, 0))
	var dispel := CombatEffectDef.new()
	dispel.effect_type = &"dispel_magic"
	dispel.power = 1
	dispel.params = {
		"max_status_removed": 1,
		"remove_beneficial_from_enemies": true,
		"remove_harmful_from_allies": true
	}
	var resolver = preload("res://scripts/systems/battle/rules/battle_damage_resolver.gd").new()

	_set_status(ally, &"blind")
	_set_status(ally, &"petrified")
	var ally_result: Dictionary = resolver.resolve_effects(source, ally, [dispel])
	_assert_true(bool(ally_result.get("applied", false)), "解除魔法命中友方时应能移除可驱散减益。")
	_assert_true(not ally.has_status_effect(&"blind"), "解除魔法应移除友方 blind。")
	_assert_true(ally.has_status_effect(&"petrified"), "解除魔法不应移除 petrified。")
	_assert_true((ally_result.get("removed_status_effect_ids", []) as Array).has(&"blind"), "解除魔法结果应报告被移除的友方状态。")

	_set_status(enemy, &"magic_shield")
	_set_status(enemy, &"attack_up")
	_set_status(enemy, &"marked")
	var enemy_result: Dictionary = resolver.resolve_effects(source, enemy, [dispel])
	_assert_true(bool(enemy_result.get("applied", false)), "解除魔法命中敌方时应能移除可驱散增益。")
	_assert_true(not enemy.has_status_effect(&"magic_shield"), "解除魔法应优先移除敌方高优先级魔法增益。")
	_assert_true(enemy.has_status_effect(&"attack_up"), "单次解除魔法只应移除配置数量内的敌方增益。")
	_assert_true(enemy.has_status_effect(&"marked"), "解除魔法不应移除敌方身上的有害状态。")
	_assert_true((enemy_result.get("removed_status_effect_ids", []) as Array).has(&"magic_shield"), "解除魔法结果应报告被移除的敌方状态。")


func _build_runtime_with_sphere() -> Dictionary:
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, {}, {}, {})
	var state := _build_state(Vector2i(7, 5))
	runtime._state = state
	var caster := _build_unit(&"caster", "施法者", &"player", Vector2i(2, 2))
	var enemy := _build_unit(&"enemy", "敌人", &"enemy", Vector2i(5, 2))
	_add_unit(runtime, state, caster, false)
	_add_unit(runtime, state, enemy, true)
	var skill := _build_skill(&"mage_prismatic_sphere", "虹光法球", [&"mage", &"magic"])
	var effect := CombatEffectDef.new()
	effect.effect_type = &"layered_barrier"
	effect.duration_tu = 120
	effect.save_dc = 15
	effect.save_dc_mode = &"static"
	effect.save_ability = &"willpower"
	effect.save_tag = &"magic"
	effect.params = {
		"area_pattern": "diamond",
		"profile_id": "prismatic_sphere",
		"radius_cells": 2
	}
	runtime._layered_barrier_service.apply_layered_barrier_effect(caster, caster, skill, effect, BattleEventBatch.new())
	return {
		"runtime": runtime,
		"state": state,
		"caster": caster,
		"enemy": enemy,
	}


func _build_state(map_size: Vector2i) -> BattleState:
	var state := BattleState.new()
	state.battle_id = &"prismatic_sphere_regression"
	state.phase = &"unit_acting"
	state.map_size = map_size
	state.timeline = BattleTimelineState.new()
	state.cells = {}
	for y in range(map_size.y):
		for x in range(map_size.x):
			var cell := BattleCellState.new()
			cell.coord = Vector2i(x, y)
			cell.base_terrain = BattleCellState.TERRAIN_LAND
			cell.base_height = 4
			cell.recalculate_runtime_values()
			state.cells[cell.coord] = cell
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)
	return state


func _build_unit(unit_id: StringName, display_name: String, faction_id: StringName, coord: Vector2i) -> BattleUnitState:
	var unit := BattleUnitState.new()
	unit.unit_id = unit_id
	unit.display_name = display_name
	unit.faction_id = faction_id
	unit.control_mode = &"manual"
	unit.current_hp = 120
	unit.current_mp = 120
	unit.current_stamina = 40
	unit.current_ap = 2
	unit.is_alive = true
	unit.set_anchor_coord(coord)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX, 120)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.MP_MAX, 120)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX, 40)
	unit.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.CONSTITUTION, 10)
	unit.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.WILLPOWER, 10)
	unit.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.INTELLIGENCE, 14)
	unit.attribute_snapshot.set_value(&"constitution_modifier", 0)
	unit.attribute_snapshot.set_value(&"willpower_modifier", 0)
	unit.attribute_snapshot.set_value(&"intelligence_modifier", 2)
	return unit


func _build_skill(skill_id: StringName, display_name: String, tags: Array[StringName]) -> SkillDef:
	var skill := SkillDef.new()
	skill.skill_id = skill_id
	skill.display_name = display_name
	skill.icon_id = skill_id
	skill.tags = tags
	return skill


func _add_unit(runtime: BattleRuntimeModule, state: BattleState, unit: BattleUnitState, is_enemy: bool) -> void:
	state.units[unit.unit_id] = unit
	if is_enemy:
		state.enemy_unit_ids.append(unit.unit_id)
	else:
		state.ally_unit_ids.append(unit.unit_id)
	runtime._grid_service.place_unit(state, unit, unit.coord, true)


func _set_status(unit_state: BattleUnitState, status_id: StringName, params: Dictionary = {}) -> void:
	var status := BattleStatusEffectState.new()
	status.status_id = status_id
	status.source_unit_id = &"source"
	status.power = 1
	status.stacks = 1
	status.duration = -1
	status.params = params.duplicate(true)
	unit_state.set_status_effect(status)


func _first_barrier(state: BattleState) -> Dictionary:
	for key in state.layered_barrier_fields.keys():
		return state.layered_barrier_fields.get(key, {})
	return {}


func _first_barrier_key(state: BattleState) -> StringName:
	for key in state.layered_barrier_fields.keys():
		return key
	return &""


func _get_active_layer_id(barrier: Dictionary) -> StringName:
	for layer_variant in barrier.get("layers", []):
		var layer: Dictionary = layer_variant if layer_variant is Dictionary else {}
		if not layer.is_empty() and not bool(layer.get("broken", false)):
			return StringName(layer.get("layer_id", ""))
	return &""


func _mark_layers_broken(state: BattleState, layer_ids: Array[StringName]) -> void:
	var key := _first_barrier_key(state)
	var barrier := _first_barrier(state)
	var layers: Array = barrier.get("layers", [])
	for index in range(layers.size()):
		var layer: Dictionary = layers[index] if layers[index] is Dictionary else {}
		if layer_ids.has(StringName(layer.get("layer_id", ""))):
			layer["broken"] = true
			layers[index] = layer
	barrier["layers"] = layers
	state.layered_barrier_fields[key] = barrier


func _set_layer_save_roll_override(state: BattleState, layer_id: StringName, roll: int) -> void:
	var key := _first_barrier_key(state)
	var barrier := _first_barrier(state)
	var layers: Array = barrier.get("layers", [])
	for index in range(layers.size()):
		var layer: Dictionary = layers[index] if layers[index] is Dictionary else {}
		if StringName(layer.get("layer_id", "")) == layer_id:
			layer["save_roll_override"] = roll
			layers[index] = layer
			break
	barrier["layers"] = layers
	state.layered_barrier_fields[key] = barrier


func _assert_true(condition: bool, message: String) -> void:
	_test.assert_true(condition, message)


func _assert_eq(actual, expected, message: String) -> void:
	_test.assert_eq(actual, expected, message)
