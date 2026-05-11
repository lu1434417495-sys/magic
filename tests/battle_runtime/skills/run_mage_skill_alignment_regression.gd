extends SceneTree

const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const BATTLE_CELL_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BATTLE_COMMAND_SCRIPT = preload("res://scripts/systems/battle/core/battle_command.gd")
const BATTLE_RUNTIME_MODULE_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_runtime_module.gd")
const BATTLE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_state.gd")
const BATTLE_TIMELINE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_timeline_state.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const PROGRESSION_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/player/progression/progression_content_registry.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_core_mage_skills_share_fireball_progression_shape()
	_test_chain_lightning_bounces_to_nearby_enemy()
	_test_blink_moves_to_ground_target_and_grants_dodge()
	_test_fire_wall_leaves_timed_terrain_effects()
	if _failures.is_empty():
		print("Mage skill alignment regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Mage skill alignment regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_core_mage_skills_share_fireball_progression_shape() -> void:
	var skill_defs: Dictionary = PROGRESSION_CONTENT_REGISTRY_SCRIPT.new().get_skill_defs()
	var expected_mastery_curve := [400, 1000, 2200, 4000, 6500, 9500, 13000]
	var mage_skill_ids := _all_mage_skill_ids(skill_defs)
	_assert_eq(mage_skill_ids.size(), 135, "法师技能目录应完整注册 135 个 mage_ 主动技能。")
	for skill_id in mage_skill_ids:
		var skill_def = skill_defs.get(skill_id)
		_assert_true(skill_def != null, "%s 应存在于技能注册表。" % String(skill_id))
		if skill_def == null:
			continue
		_assert_eq(int(skill_def.max_level), 7, "%s 应对齐火球术 7 级上限。" % String(skill_id))
		_assert_eq(int(skill_def.non_core_max_level), 5, "%s 非核心上限应为 5。" % String(skill_id))
		_assert_eq(_packed_ints_to_array(skill_def.mastery_curve), expected_mastery_curve, "%s mastery_curve 应对齐火球术熟练度曲线。" % String(skill_id))
		_assert_eq(String(skill_def.growth_tier), "advanced", "%s 应使用 advanced 成长预算。" % String(skill_id))
		_assert_eq(_sum_int_dict(skill_def.attribute_growth_progress), 180, "%s 属性成长预算应合计 180。" % String(skill_id))
		_assert_true(skill_def.combat_profile != null, "%s 应有 combat_profile。" % String(skill_id))
		if skill_def.combat_profile == null:
			continue
		_assert_true(skill_def.tags.has(&"mage"), "%s 应保留 mage 标签。" % String(skill_id))
		_assert_true(skill_def.tags.has(&"magic"), "%s 应保留 magic 标签。" % String(skill_id))
		_assert_true(int(skill_def.combat_profile.ap_cost) >= 1, "%s 应至少消耗 1 AP。" % String(skill_id))
		_assert_true(int(skill_def.combat_profile.mp_cost) >= 60, "%s 应使用新版法师 MP 消耗下限。" % String(skill_id))
		_assert_true(int(skill_def.combat_profile.cooldown_tu) >= 5, "%s 应有新版冷却约束。" % String(skill_id))
		_assert_true(_has_usable_effect_surface(skill_def), "%s 应有可执行的效果面。" % String(skill_id))
		_assert_true(_all_scalable_effects_are_tiered(skill_def), "%s 的伤害/治疗/护盾/状态效果应按等级分档。" % String(skill_id))
		_assert_true(_damage_dice_params_use_formal_keys(skill_def), "%s 的技能骰应使用 dice_count / dice_sides 字段。" % String(skill_id))
		for level in range(0, 8):
			_assert_true(_has_level_description_config(skill_def, level), "%s 应提供 %d 级描述变量。" % [String(skill_id), level])
			_assert_true(_has_effect_available_at_level(skill_def, level), "%s 在 %d 级应至少有一个可用效果。" % [String(skill_id), level])

	var frost_bolt = skill_defs.get(&"mage_frost_bolt")
	_assert_true(_has_status_effect_at_or_after(frost_bolt, &"slow", 3), "霜击术 3 级后应附加 slow。")
	var spark_javelin = skill_defs.get(&"mage_spark_javelin")
	_assert_true(_has_status_effect_at_or_after(spark_javelin, &"shocked", 3), "电矛术 3 级后应附加 shocked。")
	var ice_lance = skill_defs.get(&"mage_ice_lance")
	_assert_true(_has_status_effect_at_or_after(ice_lance, &"frozen", 3), "冰枪术冻结应从中等级开始。")
	var burning_hands = skill_defs.get(&"mage_burning_hands")
	_assert_true(_has_status_effect_at_or_after(burning_hands, &"burning", 3), "焚掌喷流 3 级后应附加 burning。")
	var fire_wall = skill_defs.get(&"mage_fire_wall")
	_assert_true(_has_effect_type(fire_wall, &"terrain_effect"), "火墙术应留下 timed terrain effect。")
	var earthen_grasp = skill_defs.get(&"mage_earthen_grasp")
	_assert_true(_has_status_effect_at_or_after(earthen_grasp, &"rooted", 1), "地脉束缚应附加 rooted。")
	var chain_lightning = skill_defs.get(&"mage_chain_lightning")
	_assert_true(_has_effect_type(chain_lightning, &"chain_damage"), "链式闪击应声明 chain_damage 控制效果。")
	var magic_shield = skill_defs.get(&"mage_magic_shield")
	_assert_true(_has_effect_type(magic_shield, &"shield"), "魔力护盾高等级应附加 shield 效果。")
	var blink = skill_defs.get(&"mage_blink")
	_assert_true(_has_forced_move_mode(blink, &"jump"), "闪现术应使用现有 ground jump 位移协议。")


func _test_chain_lightning_bounces_to_nearby_enemy() -> void:
	var registry := PROGRESSION_CONTENT_REGISTRY_SCRIPT.new()
	var runtime = BATTLE_RUNTIME_MODULE_SCRIPT.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})
	var state = _build_state(Vector2i(6, 3))
	var caster = _build_unit(&"chain_caster", &"player", Vector2i(0, 1), 3)
	caster.current_mp = 200
	caster.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 100)
	caster.known_active_skill_ids.append(&"mage_chain_lightning")
	caster.known_skill_level_map = {&"mage_chain_lightning": 3}
	var primary = _build_unit(&"chain_primary", &"enemy", Vector2i(3, 1), 1)
	var bounce = _build_unit(&"chain_bounce", &"enemy", Vector2i(4, 1), 1)
	var far = _build_unit(&"chain_far", &"enemy", Vector2i(5, 2), 1)
	_add_unit(runtime, state, caster, false)
	_add_unit(runtime, state, primary, true)
	_add_unit(runtime, state, bounce, true)
	_add_unit(runtime, state, far, true)
	state.active_unit_id = caster.unit_id
	runtime._state = state

	var command = BATTLE_COMMAND_SCRIPT.new()
	command.command_type = BATTLE_COMMAND_SCRIPT.TYPE_SKILL
	command.unit_id = caster.unit_id
	command.skill_id = &"mage_chain_lightning"
	command.target_unit_id = primary.unit_id
	command.target_coord = primary.coord
	var batch = runtime.issue_command(command)

	_assert_true(batch.changed_unit_ids.has(primary.unit_id), "链式闪击应影响主目标。")
	_assert_true(batch.changed_unit_ids.has(bounce.unit_id), "链式闪击应弹射到相邻敌人。")
	_assert_true(not batch.changed_unit_ids.has(far.unit_id), "链式闪击不应弹射到半径外敌人。")
	_assert_true(primary.current_hp < 30, "主目标应受到雷电伤害。")
	_assert_true(bounce.current_hp < 30, "弹射目标应受到雷电伤害。")
	_assert_true(bounce.has_status_effect(&"shocked"), "弹射目标应继承感电状态。")


func _test_blink_moves_to_ground_target_and_grants_dodge() -> void:
	var registry := PROGRESSION_CONTENT_REGISTRY_SCRIPT.new()
	var runtime = BATTLE_RUNTIME_MODULE_SCRIPT.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})
	var state = _build_state(Vector2i(5, 2))
	var caster = _build_unit(&"blink_caster", &"player", Vector2i(0, 0), 3)
	caster.current_mp = 100
	caster.known_active_skill_ids.append(&"mage_blink")
	caster.known_skill_level_map = {&"mage_blink": 5}
	_add_unit(runtime, state, caster, false)
	state.active_unit_id = caster.unit_id
	runtime._state = state

	var command = BATTLE_COMMAND_SCRIPT.new()
	command.command_type = BATTLE_COMMAND_SCRIPT.TYPE_SKILL
	command.unit_id = caster.unit_id
	command.skill_id = &"mage_blink"
	command.target_coord = Vector2i(3, 0)
	command.target_coords.append(Vector2i(3, 0))
	var batch = runtime.issue_command(command)

	_assert_true(batch.changed_unit_ids.has(caster.unit_id), "闪现术应标记施法者变更。")
	_assert_eq(caster.coord, Vector2i(3, 0), "闪现术应移动到选定地格。")
	_assert_true(caster.has_status_effect(&"dodge_bonus_up"), "5 级闪现术应附加短暂闪避。")


func _test_fire_wall_leaves_timed_terrain_effects() -> void:
	var registry := PROGRESSION_CONTENT_REGISTRY_SCRIPT.new()
	var runtime = BATTLE_RUNTIME_MODULE_SCRIPT.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})
	var state = _build_state(Vector2i(6, 3))
	var caster = _build_unit(&"fire_wall_caster", &"player", Vector2i(0, 1), 3)
	caster.current_mp = 120
	caster.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 100)
	caster.known_active_skill_ids.append(&"mage_fire_wall")
	caster.known_skill_level_map = {&"mage_fire_wall": 3}
	var enemy = _build_unit(&"fire_wall_enemy", &"enemy", Vector2i(3, 1), 1)
	_add_unit(runtime, state, caster, false)
	_add_unit(runtime, state, enemy, true)
	state.active_unit_id = caster.unit_id
	runtime._state = state

	var command = BATTLE_COMMAND_SCRIPT.new()
	command.command_type = BATTLE_COMMAND_SCRIPT.TYPE_SKILL
	command.unit_id = caster.unit_id
	command.skill_id = &"mage_fire_wall"
	command.target_coord = Vector2i(2, 1)
	command.target_coords.append(Vector2i(2, 1))
	var batch = runtime.issue_command(command)
	var target_cell = runtime._grid_service.get_cell(state, Vector2i(2, 1))

	_assert_true(batch.changed_coords.has(Vector2i(2, 1)), "火墙术应标记目标地格变更。")
	_assert_true(target_cell != null and target_cell.timed_terrain_effects.size() >= 2, "火墙术应留下伤害与灼烧 timed terrain effects。")


func _build_state(map_size: Vector2i):
	var state = BATTLE_STATE_SCRIPT.new()
	state.battle_id = &"mage_skill_alignment"
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


func _build_unit(unit_id: StringName, faction_id: StringName, coord: Vector2i, current_ap: int):
	var unit = BATTLE_UNIT_STATE_SCRIPT.new()
	unit.unit_id = unit_id
	unit.display_name = String(unit_id)
	unit.faction_id = faction_id
	unit.current_ap = current_ap
	unit.current_move_points = BATTLE_UNIT_STATE_SCRIPT.DEFAULT_MOVE_POINTS_PER_TURN
	unit.current_hp = 30
	unit.current_mp = 0
	unit.current_stamina = 60
	unit.is_alive = true
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	unit.set_anchor_coord(coord)
	return unit


func _add_unit(runtime, state, unit, is_enemy: bool) -> void:
	state.units[unit.unit_id] = unit
	if is_enemy:
		state.enemy_unit_ids.append(unit.unit_id)
	else:
		state.ally_unit_ids.append(unit.unit_id)
	_assert_true(runtime._grid_service.place_unit(state, unit, unit.coord, true), "%s 应能放入战场。" % String(unit.unit_id))


func _sum_int_dict(values: Dictionary) -> int:
	var total := 0
	for value in values.values():
		total += int(value)
	return total


func _all_mage_skill_ids(skill_defs: Dictionary) -> Array[StringName]:
	var skill_ids: Array[StringName] = []
	for skill_id_variant in skill_defs.keys():
		var skill_id := skill_id_variant as StringName
		if not String(skill_id).begins_with("mage_"):
			continue
		var skill_def = skill_defs.get(skill_id)
		if skill_def == null or skill_def.skill_type != &"active":
			continue
		skill_ids.append(skill_id)
	skill_ids.sort()
	return skill_ids


func _packed_ints_to_array(values: PackedInt32Array) -> Array:
	var result: Array = []
	for value in values:
		result.append(int(value))
	return result


func _has_level_description_config(skill_def, level: int) -> bool:
	if skill_def == null:
		return false
	var configs: Dictionary = skill_def.level_description_configs
	return configs.has(str(level)) or configs.has(level)


func _has_usable_effect_surface(skill_def) -> bool:
	if skill_def == null or skill_def.combat_profile == null:
		return false
	if not skill_def.combat_profile.effect_defs.is_empty():
		return true
	for cast_variant in skill_def.combat_profile.cast_variants:
		if cast_variant != null and not cast_variant.effect_defs.is_empty():
			return true
	return false


func _collect_effect_defs(skill_def) -> Array:
	var effects: Array = []
	if skill_def == null or skill_def.combat_profile == null:
		return effects
	for effect_def in skill_def.combat_profile.effect_defs:
		if effect_def != null:
			effects.append(effect_def)
	for cast_variant in skill_def.combat_profile.cast_variants:
		if cast_variant == null:
			continue
		for effect_def in cast_variant.effect_defs:
			if effect_def != null:
				effects.append(effect_def)
	return effects


func _has_effect_available_at_level(skill_def, skill_level: int) -> bool:
	if skill_def == null or skill_def.combat_profile == null:
		return false
	for effect_def in skill_def.combat_profile.effect_defs:
		if effect_def != null and _effect_unlocked_at_level(effect_def, skill_level):
			return true
	for cast_variant in skill_def.combat_profile.cast_variants:
		if cast_variant == null or skill_level < int(cast_variant.min_skill_level):
			continue
		for effect_def in cast_variant.effect_defs:
			if effect_def != null and _effect_unlocked_at_level(effect_def, skill_level):
				return true
	return false


func _effect_unlocked_at_level(effect_def, skill_level: int) -> bool:
	if effect_def == null:
		return false
	if skill_level < int(effect_def.min_skill_level):
		return false
	var max_skill_level := int(effect_def.max_skill_level)
	return max_skill_level < 0 or skill_level <= max_skill_level


func _all_scalable_effects_are_tiered(skill_def) -> bool:
	for effect_def in _collect_effect_defs(skill_def):
		if not _is_scalable_effect_type(effect_def.effect_type):
			continue
		if int(effect_def.min_skill_level) <= 0 and int(effect_def.max_skill_level) < 0:
			return false
	return true


func _is_scalable_effect_type(effect_type: StringName) -> bool:
	return [
		&"apply_status",
		&"chain_damage",
		&"damage",
		&"heal",
		&"shield",
		&"status",
	].has(effect_type)


func _damage_dice_params_use_formal_keys(skill_def) -> bool:
	for effect_def in _collect_effect_defs(skill_def):
		if effect_def.effect_type != &"damage":
			continue
		var params: Dictionary = effect_def.params
		if params.has("damage_dice_count") or params.has("damage_dice_sides") or params.has("damage_dice_bonus"):
			return false
		if params.has("dice_count") or params.has("dice_sides") or params.has("dice_bonus"):
			if not params.has("dice_count") or not params.has("dice_sides"):
				return false
	return true


func _has_effect_type(skill_def, effect_type: StringName) -> bool:
	for effect_def in _collect_effect_defs(skill_def):
		if effect_def != null and effect_def.effect_type == effect_type:
			return true
	return false


func _has_status_effect_at_or_after(skill_def, status_id: StringName, min_level: int) -> bool:
	for effect_def in _collect_effect_defs(skill_def):
		if effect_def != null \
				and effect_def.effect_type == &"status" \
				and effect_def.status_id == status_id \
				and int(effect_def.min_skill_level) >= min_level:
			return true
	return false


func _has_forced_move_mode(skill_def, mode: StringName) -> bool:
	for effect_def in _collect_effect_defs(skill_def):
		if effect_def != null and effect_def.effect_type == &"forced_move" and effect_def.forced_move_mode == mode:
			return true
	return false


func _assert_true(value: bool, message: String) -> void:
	if not value:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
