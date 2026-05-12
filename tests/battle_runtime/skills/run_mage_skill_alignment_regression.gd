extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const BATTLE_CELL_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BATTLE_COMMAND_SCRIPT = preload("res://scripts/systems/battle/core/battle_command.gd")
const BATTLE_EDGE_FEATURE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_edge_feature_state.gd")
const BATTLE_RUNTIME_MODULE_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_runtime_module.gd")
const BATTLE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_state.gd")
const BATTLE_STATUS_EFFECT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_status_effect_state.gd")
const BATTLE_TIMELINE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_timeline_state.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const PROGRESSION_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/player/progression/progression_content_registry.gd")
const SharedHitResolvers = preload("res://tests/shared/stub_hit_resolvers.gd")

const FIREBALL_ALIGNMENT_EXCEPTIONS: Array[StringName] = [
	&"mage_arcane_missile",
]

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_core_mage_skills_share_fireball_progression_shape()
	_test_chain_lightning_bounces_to_nearby_enemy()
	_test_blink_moves_to_ground_target()
	_test_cone_of_cold_uses_narrow_cone_shape()
	_test_gust_of_wind_uses_standard_cone_and_pushes_outward()
	_test_gust_of_wind_chain_pushes_near_targets_first()
	_test_fire_wall_leaves_timed_terrain_effects()
	_test_passwall_clears_adjacent_wall_edge()
	_test_dispel_magic_removes_enemy_buff_through_runtime()
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
	_assert_eq(mage_skill_ids.size(), 141, "法师技能目录应完整注册 141 个 mage_ 主动技能。")
	for skill_id in mage_skill_ids:
		if FIREBALL_ALIGNMENT_EXCEPTIONS.has(skill_id):
			continue
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

	_assert_arcane_missile_current_shape(skill_defs.get(&"mage_arcane_missile"))

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
	_assert_true(_has_forced_move_mode(blink, &"blink"), "闪现术应使用 blink 位移协议。")
	var cone_of_cold = skill_defs.get(&"mage_cone_of_cold")
	_assert_true(cone_of_cold != null and cone_of_cold.combat_profile != null, "寒冰锥应注册 combat_profile。")
	if cone_of_cold != null and cone_of_cold.combat_profile != null:
		_assert_eq(cone_of_cold.combat_profile.area_pattern, &"narrow_cone", "寒冰锥应使用 narrow_cone 范围。")
		_assert_eq(int(cone_of_cold.combat_profile.area_value), 5, "寒冰锥基础长度应为 5 格。")
	_assert_true(_has_status_effect_at_or_after(cone_of_cold, &"slow", 3), "寒冰锥 3 级后应附加 slow。")
	var gust_of_wind = skill_defs.get(&"mage_gust_of_wind")
	_assert_true(gust_of_wind != null and gust_of_wind.combat_profile != null, "强风术应注册 combat_profile。")
	if gust_of_wind != null and gust_of_wind.combat_profile != null:
		_assert_eq(gust_of_wind.combat_profile.area_pattern, &"cone", "强风术应使用标准 cone 范围。")
		_assert_eq(int(gust_of_wind.combat_profile.area_value), 2, "强风术基础锥形应向外扩张 2 列。")
	_assert_true(_has_forced_move_mode(gust_of_wind, &"wind_push"), "强风术应使用 wind_push 位移协议。")
	var passwall = skill_defs.get(&"mage_passwall")
	_assert_true(passwall != null and passwall.combat_profile != null, "穿墙术应注册 combat_profile。")
	if passwall != null and passwall.combat_profile != null:
		var open_passage = passwall.combat_profile.get_cast_variant(&"open_passage")
		_assert_true(open_passage != null, "穿墙术应提供 open_passage 施法形态。")
		if open_passage != null:
			_assert_eq(open_passage.footprint_pattern, &"line2", "穿墙术应要求两个正交相邻格。")
			_assert_eq(int(open_passage.required_coord_count), 2, "穿墙术应选择两个地格。")
	_assert_true(_has_effect_type(passwall, &"edge_clear"), "穿墙术应使用 edge_clear 地形效果。")
	var continual_light = skill_defs.get(&"mage_continual_light")
	_assert_true(continual_light != null and continual_light.combat_profile != null, "恒光术应注册 combat_profile。")
	if continual_light != null and continual_light.combat_profile != null:
		_assert_eq(continual_light.combat_profile.target_mode, &"unit", "恒光术应为单体目标控制。")
		_assert_eq(int(continual_light.combat_profile.range_value), 5, "恒光术基础射程应为 5 格。")
	_assert_true(_has_saved_status_effect(continual_light, &"blind"), "恒光术应以法术豁免施加 blind。")
	_assert_true(_has_effect_param_value(continual_light, &"breaks_barrier_layer", "indigo"), "恒光术效果应声明破解靛色层。")
	_assert_true(_has_effect_param_value(continual_light, &"attack_roll_penalty", 4), "恒光术 blind 应声明攻击检定惩罚。")
	var dispel_magic = skill_defs.get(&"mage_dispel_magic")
	_assert_true(dispel_magic != null and dispel_magic.combat_profile != null, "解除魔法应注册 combat_profile。")
	if dispel_magic != null and dispel_magic.combat_profile != null:
		_assert_eq(dispel_magic.combat_profile.target_mode, &"unit", "解除魔法应为单体目标技能。")
		_assert_eq(dispel_magic.combat_profile.target_team_filter, &"any", "解除魔法应允许选择友方或敌方目标。")
		_assert_eq(int(dispel_magic.combat_profile.range_value), 6, "解除魔法基础射程应为 6 格。")
	_assert_true(_has_effect_type(dispel_magic, &"dispel_magic"), "解除魔法应使用正式 dispel_magic 效果。")
	_assert_true(_has_effect_param_value(dispel_magic, &"breaks_barrier_layer", "violet"), "解除魔法效果应声明破解紫色层。")


func _test_chain_lightning_bounces_to_nearby_enemy() -> void:
	var registry := PROGRESSION_CONTENT_REGISTRY_SCRIPT.new()
	var runtime = BATTLE_RUNTIME_MODULE_SCRIPT.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})
	runtime.configure_hit_resolver_for_tests(SharedHitResolvers.FixedHitResolver.new(10))
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
	var chain_lightning = registry.get_skill_defs().get(&"mage_chain_lightning")
	_assert_true(_has_saved_status_effect(chain_lightning, &"shocked"), "链式闪击感电应配置豁免，成功豁免时可不附加。")


func _test_blink_moves_to_ground_target() -> void:
	var registry := PROGRESSION_CONTENT_REGISTRY_SCRIPT.new()
	var runtime = BATTLE_RUNTIME_MODULE_SCRIPT.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})
	var state = _build_state(Vector2i(5, 2))
	_set_cell_height(state, Vector2i(1, 0), 8)
	_set_cell_height(state, Vector2i(2, 0), 8)
	_set_cell_height(state, Vector2i(3, 0), 0)
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
	_assert_eq(caster.coord, Vector2i(3, 0), "闪现术应只按平面距离移动到选定地格。")
	_assert_true(not caster.has_status_effect(&"dodge_bonus_up"), "闪现术不应附加闪避状态。")


func _test_cone_of_cold_uses_narrow_cone_shape() -> void:
	var registry := PROGRESSION_CONTENT_REGISTRY_SCRIPT.new()
	var runtime = BATTLE_RUNTIME_MODULE_SCRIPT.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})
	var state = _build_state(Vector2i(13, 9))
	var caster = _build_unit(&"cone_caster", &"player", Vector2i(5, 5), 3)
	caster.current_mp = 200
	caster.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 100)
	caster.known_active_skill_ids.append(&"mage_cone_of_cold")
	caster.known_skill_level_map = {&"mage_cone_of_cold": 0}
	var close_left = _build_unit(&"cone_close_left", &"enemy", Vector2i(6, 4), 1)
	var close_right = _build_unit(&"cone_close_right", &"enemy", Vector2i(7, 6), 1)
	var tail = _build_unit(&"cone_tail", &"enemy", Vector2i(11, 5), 1)
	var outside = _build_unit(&"cone_outside", &"enemy", Vector2i(8, 4), 1)
	_add_unit(runtime, state, caster, false)
	_add_unit(runtime, state, close_left, true)
	_add_unit(runtime, state, close_right, true)
	_add_unit(runtime, state, tail, true)
	_add_unit(runtime, state, outside, true)
	state.active_unit_id = caster.unit_id
	runtime._state = state

	var command = BATTLE_COMMAND_SCRIPT.new()
	command.command_type = BATTLE_COMMAND_SCRIPT.TYPE_SKILL
	command.unit_id = caster.unit_id
	command.skill_id = &"mage_cone_of_cold"
	command.target_coord = Vector2i(6, 5)
	command.target_coords.append(Vector2i(6, 5))
	var batch = runtime.issue_command(command)

	_assert_true(batch.changed_unit_ids.has(close_left.unit_id), "寒冰锥应命中近端左侧格。")
	_assert_true(batch.changed_unit_ids.has(close_right.unit_id), "寒冰锥应命中近端右侧格。")
	_assert_true(batch.changed_unit_ids.has(tail.unit_id), "寒冰锥应命中远端中线长尾。")
	_assert_true(not batch.changed_unit_ids.has(outside.unit_id), "寒冰锥远端不应横向扩散到 3 格外。")
	_assert_true(close_left.current_hp < 30, "寒冰锥近端目标应受到冰冻伤害。")
	_assert_true(tail.current_hp < 30, "寒冰锥远端目标应受到冰冻伤害。")
	_assert_eq(outside.current_hp, 30, "寒冰锥范围外目标不应受伤。")


func _test_gust_of_wind_uses_standard_cone_and_pushes_outward() -> void:
	var registry := PROGRESSION_CONTENT_REGISTRY_SCRIPT.new()
	var runtime = BATTLE_RUNTIME_MODULE_SCRIPT.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})
	var state = _build_state(Vector2i(9, 8))
	var caster = _build_unit(&"gust_caster", &"player", Vector2i(3, 4), 3)
	caster.current_mp = 120
	caster.known_active_skill_ids.append(&"mage_gust_of_wind")
	caster.known_skill_level_map = {&"mage_gust_of_wind": 0}
	var pushed = _build_unit(&"gust_pushed", &"enemy", Vector2i(5, 3), 1)
	var outside = _build_unit(&"gust_outside", &"enemy", Vector2i(4, 3), 1)
	_add_unit(runtime, state, caster, false)
	_add_unit(runtime, state, pushed, true)
	_add_unit(runtime, state, outside, true)
	state.active_unit_id = caster.unit_id
	runtime._state = state

	var command = BATTLE_COMMAND_SCRIPT.new()
	command.command_type = BATTLE_COMMAND_SCRIPT.TYPE_SKILL
	command.unit_id = caster.unit_id
	command.skill_id = &"mage_gust_of_wind"
	command.target_coord = Vector2i(4, 4)
	command.target_coords.append(Vector2i(4, 4))
	var preview = runtime.preview_command(command)

	_assert_true(preview != null and preview.allowed, "强风术应允许点击施法者相邻格确定风向。")
	_assert_true(preview.target_coords.has(Vector2i(4, 4)), "强风术第一列应只有点击格。")
	_assert_true(preview.target_coords.has(Vector2i(5, 3)), "强风术第二列应向外扩为 3 格。")
	_assert_true(preview.target_coords.has(Vector2i(6, 2)), "强风术第三列应向外扩为 5 格。")
	_assert_true(preview.target_coords.has(Vector2i(6, 6)), "强风术第三列应包含另一侧边缘。")
	_assert_true(not preview.target_coords.has(Vector2i(4, 3)), "强风术第一列不应横向扩张。")
	_assert_true(not preview.target_coords.has(Vector2i(7, 1)), "强风术基础范围不应超过 area_value=2。")

	var batch = runtime.issue_command(command)

	_assert_true(batch.changed_unit_ids.has(pushed.unit_id), "强风术应记录被风推出的目标。")
	_assert_eq(pushed.coord, Vector2i(6, 3), "强风术应沿施法者到点击格的方向把目标往外推。")
	_assert_true(not batch.changed_unit_ids.has(outside.unit_id), "强风术第一列侧面目标不应受影响。")
	_assert_eq(outside.coord, Vector2i(4, 3), "强风术范围外目标不应移动。")


func _test_gust_of_wind_chain_pushes_near_targets_first() -> void:
	var registry := PROGRESSION_CONTENT_REGISTRY_SCRIPT.new()
	var runtime = BATTLE_RUNTIME_MODULE_SCRIPT.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})
	var state = _build_state(Vector2i(9, 8))
	var caster = _build_unit(&"gust_chain_caster", &"player", Vector2i(3, 4), 3)
	caster.current_mp = 120
	caster.known_active_skill_ids.append(&"mage_gust_of_wind")
	caster.known_skill_level_map = {&"mage_gust_of_wind": 0}
	var near = _build_unit(&"gust_chain_near", &"enemy", Vector2i(4, 4), 1)
	var far = _build_unit(&"gust_chain_far", &"enemy", Vector2i(5, 4), 1)
	_add_unit(runtime, state, caster, false)
	_add_unit(runtime, state, near, true)
	_add_unit(runtime, state, far, true)
	state.active_unit_id = caster.unit_id
	runtime._state = state

	var command = BATTLE_COMMAND_SCRIPT.new()
	command.command_type = BATTLE_COMMAND_SCRIPT.TYPE_SKILL
	command.unit_id = caster.unit_id
	command.skill_id = &"mage_gust_of_wind"
	command.target_coord = Vector2i(4, 4)
	command.target_coords.append(Vector2i(4, 4))
	var batch = runtime.issue_command(command)

	_assert_true(batch.changed_unit_ids.has(near.unit_id), "强风术连锁推人应记录近端目标。")
	_assert_true(batch.changed_unit_ids.has(far.unit_id), "强风术连锁推人应记录被近端目标推动的阻挡者。")
	_assert_eq(near.coord, Vector2i(5, 4), "强风术应先推远端阻挡者，再让近端目标进入原阻挡格。")
	_assert_eq(far.coord, Vector2i(6, 4), "强风术同一风步内不应让阻挡者被重复推进。")


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


func _test_passwall_clears_adjacent_wall_edge() -> void:
	var registry := PROGRESSION_CONTENT_REGISTRY_SCRIPT.new()
	var runtime = BATTLE_RUNTIME_MODULE_SCRIPT.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})
	var state = _build_state(Vector2i(4, 3))
	var caster = _build_unit(&"passwall_caster", &"player", Vector2i(0, 1), 3)
	caster.current_mp = 120
	caster.known_active_skill_ids.append(&"mage_passwall")
	caster.known_skill_level_map = {&"mage_passwall": 0}
	_add_unit(runtime, state, caster, false)
	state.active_unit_id = caster.unit_id
	runtime._state = state
	_assert_true(
		runtime._grid_service.set_edge_feature(state, Vector2i(1, 1), Vector2i.RIGHT, BATTLE_EDGE_FEATURE_STATE_SCRIPT.make_wall()),
		"测试前置：应能在相邻地格之间创建墙体边界。"
	)
	_assert_true(not runtime._grid_service.can_traverse(state, Vector2i(1, 1), Vector2i(2, 1)), "测试前置：墙体应阻挡通行。")

	var command = BATTLE_COMMAND_SCRIPT.new()
	command.command_type = BATTLE_COMMAND_SCRIPT.TYPE_SKILL
	command.unit_id = caster.unit_id
	command.skill_id = &"mage_passwall"
	command.skill_variant_id = &"open_passage"
	command.target_coord = Vector2i(1, 1)
	command.target_coords.append(Vector2i(1, 1))
	command.target_coords.append(Vector2i(2, 1))
	var preview = runtime.preview_command(command)
	_assert_true(preview != null and preview.allowed, "穿墙术应允许选择两个正交相邻格。")
	var batch = runtime.issue_command(command)

	_assert_true(batch.changed_coords.has(Vector2i(1, 1)), "穿墙术应标记通道一侧地格变更。")
	_assert_true(batch.changed_coords.has(Vector2i(2, 1)), "穿墙术应标记通道另一侧地格变更。")
	_assert_true(runtime._grid_service.can_traverse(state, Vector2i(1, 1), Vector2i(2, 1)), "穿墙术应移除相邻格之间的墙体阻挡。")


func _test_dispel_magic_removes_enemy_buff_through_runtime() -> void:
	var registry := PROGRESSION_CONTENT_REGISTRY_SCRIPT.new()
	var runtime = BATTLE_RUNTIME_MODULE_SCRIPT.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})
	var state = _build_state(Vector2i(4, 2))
	var caster = _build_unit(&"dispel_caster", &"player", Vector2i(0, 0), 3)
	caster.current_mp = 100
	caster.known_active_skill_ids.append(&"mage_dispel_magic")
	caster.known_skill_level_map = {&"mage_dispel_magic": 0}
	var enemy = _build_unit(&"dispel_enemy", &"enemy", Vector2i(2, 0), 1)
	_set_status(enemy, &"magic_shield")
	_set_status(enemy, &"marked")
	_add_unit(runtime, state, caster, false)
	_add_unit(runtime, state, enemy, true)
	state.active_unit_id = caster.unit_id
	runtime._state = state

	var command = BATTLE_COMMAND_SCRIPT.new()
	command.command_type = BATTLE_COMMAND_SCRIPT.TYPE_SKILL
	command.unit_id = caster.unit_id
	command.skill_id = &"mage_dispel_magic"
	command.target_unit_id = enemy.unit_id
	command.target_coord = enemy.coord
	var batch = runtime.issue_command(command)

	_assert_true(batch.changed_unit_ids.has(enemy.unit_id), "解除魔法应标记被驱散目标变化。")
	_assert_true(not enemy.has_status_effect(&"magic_shield"), "解除魔法应通过运行时命令移除敌方魔法护盾。")
	_assert_true(enemy.has_status_effect(&"marked"), "解除魔法不应移除敌方身上的负面状态。")


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


func _set_cell_height(state, coord: Vector2i, height: int) -> void:
	var cell = state.cells.get(coord)
	if cell == null:
		return
	cell.base_height = height
	cell.recalculate_runtime_values()


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


func _set_status(unit, status_id: StringName) -> void:
	var status = BATTLE_STATUS_EFFECT_STATE_SCRIPT.new()
	status.status_id = status_id
	status.source_unit_id = &"test"
	status.power = 1
	status.stacks = 1
	status.duration = 30
	unit.set_status_effect(status)


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
	if skill_def.combat_profile.special_resolution_profile_id != &"":
		return true
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
	if skill_def.combat_profile.special_resolution_profile_id != &"":
		return true
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
		&"damage",
		&"heal",
		&"shield",
	].has(effect_type)


func _assert_arcane_missile_current_shape(skill_def) -> void:
	_assert_true(skill_def != null, "mage_arcane_missile 应存在于技能注册表。")
	if skill_def == null:
		return
	_assert_eq(int(skill_def.max_level), 5, "mage_arcane_missile 当前基础上限应为 5。")
	_assert_eq(int(skill_def.non_core_max_level), 3, "mage_arcane_missile 当前非核心上限应为 3。")
	_assert_eq(skill_def.dynamic_max_level_stat_id, &"profession_rank:mage", "mage_arcane_missile 当前动态上限应读取法师职业等级。")
	_assert_eq(int(skill_def.dynamic_max_level_base), 5, "mage_arcane_missile 当前动态上限基础值应为 5。")
	_assert_eq(int(skill_def.dynamic_max_level_per_stat), -2, "mage_arcane_missile 当前应以法师等级除以 2 计算动态上限。")
	_assert_eq(skill_def.mastery_curve.size(), 10, "mage_arcane_missile 当前应保留 10 档熟练度曲线供动态等级使用。")
	_assert_eq(String(skill_def.growth_tier), "basic", "mage_arcane_missile 当前应使用 basic 成长预算。")
	_assert_eq(_sum_int_dict(skill_def.attribute_growth_progress), 60, "mage_arcane_missile 当前属性成长预算应合计 60。")
	_assert_true(skill_def.combat_profile != null, "mage_arcane_missile 应有 combat_profile。")
	if skill_def.combat_profile == null:
		return
	_assert_eq(int(skill_def.combat_profile.mp_cost), 20, "mage_arcane_missile 当前基础 MP 消耗应为 20。")
	_assert_true(bool(skill_def.combat_profile.allow_repeat_target), "mage_arcane_missile 当前应允许重复锁定同一目标。")
	_assert_eq(int(skill_def.combat_profile.max_target_count), 2, "mage_arcane_missile 当前基础飞弹目标数应为 2。")
	_assert_true(not skill_def.combat_profile.effect_defs.is_empty(), "mage_arcane_missile 当前应保留根级伤害效果。")
	if not skill_def.combat_profile.effect_defs.is_empty():
		var damage_effect = skill_def.combat_profile.effect_defs[0]
		_assert_eq(damage_effect.effect_type, &"damage", "mage_arcane_missile 当前根级效果应为 damage。")
		_assert_eq(damage_effect.damage_tag, &"force", "mage_arcane_missile 当前应造成 force 伤害。")
		_assert_eq(int(damage_effect.params.get("dice_count", 0)), 1, "mage_arcane_missile 当前每发应使用 1 个伤害骰。")
		_assert_eq(int(damage_effect.params.get("dice_sides", 0)), 4, "mage_arcane_missile 当前每发应使用 d4。")
		_assert_eq(int(damage_effect.params.get("dice_bonus", 0)), 1, "mage_arcane_missile 当前每发应有 +1 固定伤害。")
	for level in range(0, 11):
		_assert_true(_has_level_description_config(skill_def, level), "mage_arcane_missile 应提供 %d 级描述变量。" % level)
		_assert_true(_has_effect_available_at_level(skill_def, level), "mage_arcane_missile 在 %d 级应至少有一个可用效果。" % level)


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


func _has_saved_status_effect(skill_def, status_id: StringName) -> bool:
	for effect_def in _collect_effect_defs(skill_def):
		if effect_def == null:
			continue
		if effect_def.effect_type != &"status" or effect_def.status_id != status_id:
			continue
		var has_dc: bool = effect_def.save_dc_mode != &"" and effect_def.save_dc_mode != &"static"
		has_dc = has_dc or int(effect_def.save_dc) > 0
		if has_dc and effect_def.save_ability != &"":
			return true
	return false


func _has_forced_move_mode(skill_def, mode: StringName) -> bool:
	for effect_def in _collect_effect_defs(skill_def):
		if effect_def != null and effect_def.effect_type == &"forced_move" and effect_def.forced_move_mode == mode:
			return true
	return false


func _has_effect_param_value(skill_def, param_key: StringName, expected_value) -> bool:
	for effect_def in _collect_effect_defs(skill_def):
		if effect_def == null or effect_def.params == null:
			continue
		var key_string := String(param_key)
		if effect_def.params.has(param_key) and effect_def.params.get(param_key) == expected_value:
			return true
		if effect_def.params.has(key_string) and effect_def.params.get(key_string) == expected_value:
			return true
	return false


func _assert_true(value: bool, message: String) -> void:
	if not value:
		_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
