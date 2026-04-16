extends SceneTree

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/game_session.gd")
const GAME_RUNTIME_FACADE_SCRIPT = preload("res://scripts/systems/game_runtime_facade.gd")
const BATTLE_RUNTIME_MODULE_SCRIPT = preload("res://scripts/systems/battle_runtime_module.gd")
const BATTLE_AI_CONTEXT_SCRIPT = preload("res://scripts/systems/battle_ai_context.gd")
const BATTLE_STATE_SCRIPT = preload("res://scripts/systems/battle_state.gd")
const BATTLE_TIMELINE_STATE_SCRIPT = preload("res://scripts/systems/battle_timeline_state.gd")
const BATTLE_CELL_STATE_SCRIPT = preload("res://scripts/systems/battle_cell_state.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle_unit_state.gd")
const ENCOUNTER_ANCHOR_DATA_SCRIPT = preload("res://scripts/systems/encounter_anchor_data.gd")

const TEST_WORLD_CONFIG := "res://data/configs/world_map/test_world_map_config.tres"

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_game_runtime_facade_injects_enemy_content()
	_test_enemy_template_resolves_stable_id()
	_test_frontline_template_resolves_stable_id()
	_test_suppressor_template_resolves_stable_id()
	_test_healer_template_resolves_stable_id()
	_test_enemy_template_does_not_resolve_display_name_alias()
	_test_ai_charge_decision_logs_brain_state_action()
	_test_frontline_bulwark_charge_decision_logs_brain_state_action()
	_test_ai_ground_skill_generates_legal_command()
	_test_ranged_suppressor_prefers_suppressive_fire_against_line_cluster()
	_test_healer_controller_uses_control_when_battle_is_stable()
	_test_frontline_bulwark_guards_when_low_hp()
	_test_ai_support_state_heals_low_hp_ally()
	_test_healer_controller_heals_low_hp_ally()
	if _failures.is_empty():
		print("Battle runtime AI regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle runtime AI regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_game_runtime_facade_injects_enemy_content() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error = int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_true(create_error == OK, "GameSession 应能加载测试世界配置并创建存档。")
	if create_error != OK:
		return
	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)
	_assert_true(
		facade._battle_runtime._enemy_templates.has(&"wolf_pack"),
		"GameRuntimeFacade.setup() 应向 BattleRuntimeModule 注入敌方模板。"
	)
	_assert_true(
		facade._battle_runtime._enemy_templates.size() >= 8,
		"正式 enemy template 数量应至少达到 8。"
	)
	_assert_true(
		facade._battle_runtime._enemy_templates.has(&"wolf_vanguard"),
		"GameRuntimeFacade.setup() 应注入新的前排狼先锋模板。"
	)
	_assert_true(
		facade._battle_runtime._enemy_templates.has(&"mist_harrier"),
		"GameRuntimeFacade.setup() 应注入新的远程压制模板。"
	)
	_assert_true(
		facade._battle_runtime._enemy_templates.has(&"mist_weaver"),
		"GameRuntimeFacade.setup() 应注入新的治疗控制模板。"
	)
	_assert_true(
		facade._battle_runtime._enemy_ai_brains.has(&"melee_aggressor"),
		"GameRuntimeFacade.setup() 应向 BattleRuntimeModule 注入敌方 AI brain。"
	)
	_assert_true(
		facade._battle_runtime._enemy_ai_brains.has(&"frontline_bulwark"),
		"GameRuntimeFacade.setup() 应注入新的前排承伤 AI brain。"
	)
	_assert_true(
		facade._battle_runtime._enemy_ai_brains.has(&"ranged_suppressor"),
		"GameRuntimeFacade.setup() 应注入新的远程压制 AI brain。"
	)
	_assert_true(
		facade._battle_runtime._enemy_ai_brains.has(&"healer_controller"),
		"GameRuntimeFacade.setup() 应注入新的治疗控制 AI brain。"
	)
	game_session.clear_persisted_game()
	game_session.free()


func _test_enemy_template_resolves_stable_id() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var encounter_anchor = _build_encounter_anchor(&"encounter_wolf", &"wolf_pack", "荒狼群")
	var state = runtime.start_battle(encounter_anchor, 101, {
		"ally_member_ids": [&"ally_a", &"ally_b"],
		"default_active_skill_ids": [&"warrior_heavy_strike"],
	})
	_assert_true(state != null and not state.is_empty(), "正式 battle start 应能创建基于敌方模板的战斗状态。")
	if state == null or state.is_empty():
		return
	_assert_true(state.enemy_unit_ids.size() == 2, "wolf_pack 模板应构建 2 个敌方单位。")
	var enemy_unit = state.units.get(state.enemy_unit_ids[0])
	_assert_true(enemy_unit != null and enemy_unit.ai_brain_id == &"melee_aggressor", "stable template id 应绑定 melee_aggressor brain。")
	_assert_true(enemy_unit != null and enemy_unit.ai_state_id == &"engage", "stable template id 应写入初始 AI 状态 engage。")
	_assert_true(enemy_unit != null and enemy_unit.known_active_skill_ids.has(&"charge"), "wolf_pack 模板应为敌人注入冲锋技能。")


func _test_frontline_template_resolves_stable_id() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var encounter_anchor = _build_encounter_anchor(&"encounter_vanguard", &"wolf_vanguard", "荒狼先锋")
	var state = runtime.start_battle(encounter_anchor, 103, {
		"ally_member_ids": [&"ally_a", &"ally_b"],
		"default_active_skill_ids": [&"warrior_heavy_strike"],
	})
	_assert_true(state != null and not state.is_empty(), "正式 battle start 应能创建基于前排模板的战斗状态。")
	if state == null or state.is_empty():
		return
	_assert_true(state.enemy_unit_ids.size() == 1, "wolf_vanguard 模板应构建 1 个敌方单位。")
	var enemy_unit = state.units.get(state.enemy_unit_ids[0])
	_assert_true(enemy_unit != null and enemy_unit.ai_brain_id == &"frontline_bulwark", "wolf_vanguard 应绑定 frontline_bulwark brain，而不是回落到默认敌人。")
	_assert_true(enemy_unit != null and enemy_unit.ai_state_id == &"engage", "wolf_vanguard 应写入 engage 初始状态。")
	_assert_true(enemy_unit != null and enemy_unit.known_active_skill_ids.has(&"charge"), "wolf_vanguard 应携带 charge。")
	_assert_true(enemy_unit != null and enemy_unit.known_active_skill_ids.has(&"warrior_guard"), "wolf_vanguard 应携带 warrior_guard 作为承伤技能。")


func _test_suppressor_template_resolves_stable_id() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var encounter_anchor = _build_encounter_anchor(&"encounter_harrier", &"mist_harrier", "雾沼猎压者")
	var state = runtime.start_battle(encounter_anchor, 104, {
		"ally_member_ids": [&"ally_a", &"ally_b"],
		"default_active_skill_ids": [&"warrior_heavy_strike"],
	})
	_assert_true(state != null and not state.is_empty(), "正式 battle start 应能创建基于远程压制模板的战斗状态。")
	if state == null or state.is_empty():
		return
	_assert_true(state.enemy_unit_ids.size() == 1, "mist_harrier 模板应构建 1 个敌方单位。")
	var enemy_unit = state.units.get(state.enemy_unit_ids[0])
	_assert_true(enemy_unit != null and enemy_unit.ai_brain_id == &"ranged_suppressor", "mist_harrier 应绑定 ranged_suppressor brain。")
	_assert_true(enemy_unit != null and enemy_unit.ai_state_id == &"pressure", "mist_harrier 应写入 pressure 初始状态。")
	_assert_true(enemy_unit != null and enemy_unit.known_active_skill_ids.has(&"archer_suppressive_fire"), "mist_harrier 应携带 archer_suppressive_fire。")
	_assert_true(enemy_unit != null and enemy_unit.known_active_skill_ids.has(&"archer_pinning_shot"), "mist_harrier 应携带 archer_pinning_shot。")


func _test_healer_template_resolves_stable_id() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var encounter_anchor = _build_encounter_anchor(&"encounter_weaver", &"mist_weaver", "雾沼织咒者")
	var state = runtime.start_battle(encounter_anchor, 105, {
		"ally_member_ids": [&"ally_a", &"ally_b"],
		"default_active_skill_ids": [&"warrior_heavy_strike"],
	})
	_assert_true(state != null and not state.is_empty(), "正式 battle start 应能创建基于治疗控制模板的战斗状态。")
	if state == null or state.is_empty():
		return
	_assert_true(state.enemy_unit_ids.size() == 1, "mist_weaver 模板应构建 1 个敌方单位。")
	var enemy_unit = state.units.get(state.enemy_unit_ids[0])
	_assert_true(enemy_unit != null and enemy_unit.ai_brain_id == &"healer_controller", "mist_weaver 应绑定 healer_controller brain。")
	_assert_true(enemy_unit != null and enemy_unit.ai_state_id == &"pressure", "mist_weaver 应写入 pressure 初始状态。")
	_assert_true(enemy_unit != null and enemy_unit.known_active_skill_ids.has(&"mage_temporal_rewind"), "mist_weaver 应携带 mage_temporal_rewind。")
	_assert_true(enemy_unit != null and enemy_unit.known_active_skill_ids.has(&"mage_glacial_prison"), "mist_weaver 应携带 mage_glacial_prison。")


func _test_enemy_template_does_not_resolve_display_name_alias() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var encounter_anchor = _build_encounter_anchor(&"encounter_legacy", &"荒狼群", "荒狼群")
	var state = runtime.start_battle(encounter_anchor, 102, {
		"ally_member_ids": [&"ally_a"],
		"default_active_skill_ids": [&"warrior_heavy_strike"],
	})
	_assert_true(state != null and not state.is_empty(), "旧显示名 alias 不应阻止战斗状态创建。")
	if state == null or state.is_empty():
		return
	var enemy_unit = state.units.get(state.enemy_unit_ids[0])
	_assert_true(enemy_unit != null and enemy_unit.ai_brain_id != &"melee_aggressor", "旧 display_name alias 不应再解析到正式 wolf_pack 模板。")


func _test_ai_charge_decision_logs_brain_state_action() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(6, 3))
	runtime._state = state
	var wolf = _build_ai_unit(
		&"wolf_01",
		"荒狼",
		&"hostile",
		Vector2i(0, 1),
		&"melee_aggressor",
		&"engage",
		[&"charge", &"warrior_heavy_strike"],
		36,
		2
	)
	var player = _build_manual_unit(&"player_01", "玩家", &"player", Vector2i(4, 1), [&"warrior_heavy_strike"])
	_add_unit_to_state(runtime, state, wolf, true)
	_add_unit_to_state(runtime, state, player, false)
	state.phase = &"unit_acting"
	state.active_unit_id = wolf.unit_id

	var batch = runtime.advance(0.0)
	_assert_true(batch != null, "AI advance 应返回有效 batch。")
	_assert_true(
		batch != null and not batch.log_lines.is_empty() and String(batch.log_lines[0]).contains("AI[melee_aggressor/engage/wolf_charge_open]"),
		"AI 行动日志应带出 brain/state/action 调试信息。"
	)
	_assert_true(wolf.coord != Vector2i(0, 1), "melee_aggressor 在 engage 状态下应优先用 charge 接敌。")


func _test_frontline_bulwark_charge_decision_logs_brain_state_action() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(7, 3))
	runtime._state = state
	var vanguard = _build_ai_unit(
		&"vanguard_01",
		"荒狼先锋",
		&"hostile",
		Vector2i(0, 1),
		&"frontline_bulwark",
		&"engage",
		[&"charge", &"warrior_shield_bash", &"warrior_taunt", &"warrior_guard"],
		42,
		2
	)
	var player = _build_manual_unit(&"player_01", "玩家", &"player", Vector2i(4, 1), [&"warrior_heavy_strike"])
	_add_unit_to_state(runtime, state, vanguard, true)
	_add_unit_to_state(runtime, state, player, false)
	state.phase = &"unit_acting"
	state.active_unit_id = vanguard.unit_id

	var batch = runtime.advance(0.0)
	_assert_true(batch != null, "frontline_bulwark AI advance 应返回有效 batch。")
	_assert_true(
		batch != null and not batch.log_lines.is_empty() and String(batch.log_lines[0]).contains("AI[frontline_bulwark/engage/vanguard_charge_open]"),
		"frontline_bulwark 冲锋开场应带出明确的 brain/state/action 日志。"
	)
	_assert_true(vanguard.coord != Vector2i(0, 1), "frontline_bulwark 在 engage 状态下应优先用 charge 接敌。")


func _test_ai_ground_skill_generates_legal_command() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(7, 5))
	runtime._state = state
	var mist = _build_ai_unit(
		&"mist_01",
		"雾沼异兽",
		&"hostile",
		Vector2i(1, 2),
		&"ranged_controller",
		&"pressure",
		[&"mage_fireball", &"mage_ice_lance", &"mage_temporal_rewind"],
		24,
		2
	)
	var player_a = _build_manual_unit(&"player_a", "玩家A", &"player", Vector2i(4, 2), [&"warrior_heavy_strike"])
	var player_b = _build_manual_unit(&"player_b", "玩家B", &"player", Vector2i(4, 3), [&"warrior_heavy_strike"])
	_add_unit_to_state(runtime, state, mist, true)
	_add_unit_to_state(runtime, state, player_a, false)
	_add_unit_to_state(runtime, state, player_b, false)
	var ai_context = _build_ai_context(runtime, mist)
	var decision = runtime._ai_service.choose_command(ai_context)
	_assert_true(decision != null and decision.command != null, "ranged_controller 应能选出有效 AI 指令。")
	_assert_true(
		decision != null and decision.command != null and decision.command.skill_id == &"mage_fireball",
		"雾沼异兽在 pressure 状态下应优先选择可命中多个目标的 ground skill。"
	)
	var preview = runtime.preview_command(decision.command)
	_assert_true(preview != null and preview.allowed, "AI 产出的 ground skill 命令必须能通过 preview_command。")
	_assert_true(preview != null and preview.target_unit_ids.size() >= 2, "ground skill 预览应至少命中 2 个单位。")


func _test_ranged_suppressor_prefers_suppressive_fire_against_line_cluster() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(7, 5))
	runtime._state = state
	var harrier = _build_ai_unit(
		&"mist_harrier_01",
		"雾沼猎压者",
		&"hostile",
		Vector2i(1, 2),
		&"ranged_suppressor",
		&"pressure",
		[&"archer_suppressive_fire", &"archer_pinning_shot"],
		26,
		2
	)
	var player_a = _build_manual_unit(&"player_a", "玩家A", &"player", Vector2i(4, 2), [&"warrior_heavy_strike"])
	var player_b = _build_manual_unit(&"player_b", "玩家B", &"player", Vector2i(5, 2), [&"warrior_heavy_strike"])
	_add_unit_to_state(runtime, state, harrier, true)
	_add_unit_to_state(runtime, state, player_a, false)
	_add_unit_to_state(runtime, state, player_b, false)
	var ai_context = _build_ai_context(runtime, harrier)
	var decision = runtime._ai_service.choose_command(ai_context)
	_assert_true(decision != null and decision.state_id == &"pressure", "ranged_suppressor 在有效射程内应保持 pressure 状态。")
	_assert_true(
		decision != null and decision.command != null and decision.command.skill_id == &"archer_suppressive_fire",
		"ranged_suppressor 面对成线目标时应优先生成 archer_suppressive_fire。"
	)
	var preview = runtime.preview_command(decision.command)
	_assert_true(preview != null and preview.allowed, "远程压制命令必须通过 preview_command。")
	_assert_true(preview != null and preview.target_unit_ids.size() >= 2, "远程压制命令应至少覆盖 2 个敌对目标。")


func _test_healer_controller_uses_control_when_battle_is_stable() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(7, 5))
	runtime._state = state
	var weaver = _build_ai_unit(
		&"mist_weaver_01",
		"雾沼织咒者",
		&"hostile",
		Vector2i(1, 1),
		&"healer_controller",
		&"pressure",
		[&"mage_temporal_rewind", &"mage_glacial_prison", &"mage_ice_lance"],
		24,
		2
	)
	var ally = _build_ai_unit(
		&"mist_weaver_ally",
		"雾沼盟友",
		&"hostile",
		Vector2i(2, 1),
		&"ranged_controller",
		&"pressure",
		[&"mage_ice_lance"],
		24,
		2
	)
	var player = _build_manual_unit(&"player_01", "玩家", &"player", Vector2i(5, 1), [&"warrior_heavy_strike"])
	_add_unit_to_state(runtime, state, weaver, true)
	_add_unit_to_state(runtime, state, ally, true)
	_add_unit_to_state(runtime, state, player, false)
	var ai_context = _build_ai_context(runtime, weaver)
	var decision = runtime._ai_service.choose_command(ai_context)
	_assert_true(decision != null and decision.state_id == &"pressure", "没有低血量友军时，healer_controller 应保持 pressure 状态。")
	_assert_true(
		decision != null and decision.command != null and decision.command.skill_id == &"mage_glacial_prison",
		"healer_controller 在稳定局面下应先用 mage_glacial_prison 做控制。"
	)
	var preview = runtime.preview_command(decision.command)
	_assert_true(preview != null and preview.allowed, "治疗控制模板的控场命令必须通过 preview_command。")


func _test_frontline_bulwark_guards_when_low_hp() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(6, 4))
	runtime._state = state
	var vanguard = _build_ai_unit(
		&"vanguard_guard",
		"荒狼先锋",
		&"hostile",
		Vector2i(1, 1),
		&"frontline_bulwark",
		&"pressure",
		[&"charge", &"warrior_shield_bash", &"warrior_taunt", &"warrior_guard"],
		12,
		2
	)
	var player = _build_manual_unit(&"player_01", "玩家", &"player", Vector2i(4, 1), [&"warrior_heavy_strike"])
	_add_unit_to_state(runtime, state, vanguard, true)
	_add_unit_to_state(runtime, state, player, false)
	var ai_context = _build_ai_context(runtime, vanguard)
	var decision = runtime._ai_service.choose_command(ai_context)
	_assert_true(decision != null and decision.state_id == &"support", "低血量时 frontline_bulwark 应切入 support 状态进行承伤准备。")
	_assert_true(
		decision != null and decision.command != null and decision.command.skill_id == &"warrior_guard",
		"frontline_bulwark 低血量时应优先使用 warrior_guard，而不是回落到普通近战动作。"
	)
	var preview = runtime.preview_command(decision.command)
	_assert_true(preview != null and preview.allowed, "frontline_bulwark 的 warrior_guard 命令必须通过 preview_command。")


func _test_ai_support_state_heals_low_hp_ally() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(7, 5))
	runtime._state = state
	var healer = _build_ai_unit(
		&"mist_healer",
		"雾沼异兽·主",
		&"hostile",
		Vector2i(1, 1),
		&"ranged_controller",
		&"pressure",
		[&"mage_fireball", &"mage_ice_lance", &"mage_temporal_rewind"],
		24,
		2
	)
	var ally = _build_ai_unit(
		&"mist_ally",
		"雾沼异兽·副",
		&"hostile",
		Vector2i(2, 1),
		&"ranged_controller",
		&"pressure",
		[&"mage_fireball", &"mage_ice_lance"],
		10,
		2
	)
	ally.attribute_snapshot.set_value(&"hp_max", 24)
	var player = _build_manual_unit(&"player_01", "玩家", &"player", Vector2i(5, 1), [&"warrior_heavy_strike"])
	_add_unit_to_state(runtime, state, healer, true)
	_add_unit_to_state(runtime, state, ally, true)
	_add_unit_to_state(runtime, state, player, false)
	var ai_context = _build_ai_context(runtime, healer)
	var decision = runtime._ai_service.choose_command(ai_context)
	_assert_true(decision != null and decision.state_id == &"support", "低血量友军存在时，ranged_controller 应切入 support 状态。")
	_assert_true(
		decision != null and decision.command != null and decision.command.skill_id == &"mage_temporal_rewind",
		"support 状态下应优先生成面向友军的合法支援命令。"
	)
	var preview = runtime.preview_command(decision.command)
	_assert_true(preview != null and preview.allowed, "support 支援命令必须通过 preview_command。")


func _test_healer_controller_heals_low_hp_ally() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(7, 5))
	runtime._state = state
	var weaver = _build_ai_unit(
		&"mist_weaver_healer",
		"雾沼织咒者",
		&"hostile",
		Vector2i(1, 1),
		&"healer_controller",
		&"pressure",
		[&"mage_temporal_rewind", &"mage_glacial_prison", &"mage_ice_lance"],
		24,
		2
	)
	var ally = _build_ai_unit(
		&"mist_weaver_target",
		"雾沼盟友",
		&"hostile",
		Vector2i(2, 1),
		&"ranged_controller",
		&"pressure",
		[&"mage_ice_lance"],
		10,
		2
	)
	ally.attribute_snapshot.set_value(&"hp_max", 24)
	var player = _build_manual_unit(&"player_01", "玩家", &"player", Vector2i(5, 1), [&"warrior_heavy_strike"])
	_add_unit_to_state(runtime, state, weaver, true)
	_add_unit_to_state(runtime, state, ally, true)
	_add_unit_to_state(runtime, state, player, false)
	var ai_context = _build_ai_context(runtime, weaver)
	var decision = runtime._ai_service.choose_command(ai_context)
	_assert_true(decision != null and decision.state_id == &"support", "低血量友军存在时，healer_controller 应切入 support 状态。")
	_assert_true(
		decision != null and decision.command != null and decision.command.skill_id == &"mage_temporal_rewind",
		"healer_controller 在 support 状态下应优先使用 mage_temporal_rewind。"
	)
	var preview = runtime.preview_command(decision.command)
	_assert_true(preview != null and preview.allowed, "healer_controller 的治疗命令必须通过 preview_command。")


func _build_runtime_with_enemy_content():
	var game_session = GAME_SESSION_SCRIPT.new()
	var runtime = BATTLE_RUNTIME_MODULE_SCRIPT.new()
	runtime.setup(
		null,
		game_session.get_skill_defs(),
		game_session.get_enemy_templates(),
		game_session.get_enemy_ai_brains(),
		null
	)
	game_session.free()
	return runtime


func _build_encounter_anchor(entity_id: StringName, template_id: StringName, display_name: String):
	var encounter_anchor = ENCOUNTER_ANCHOR_DATA_SCRIPT.new()
	encounter_anchor.entity_id = entity_id
	encounter_anchor.display_name = display_name
	encounter_anchor.enemy_roster_template_id = template_id
	encounter_anchor.faction_id = &"hostile"
	encounter_anchor.world_coord = Vector2i.ZERO
	encounter_anchor.region_tag = &"default"
	return encounter_anchor


func _build_flat_state(map_size: Vector2i):
	var state = BATTLE_STATE_SCRIPT.new()
	state.battle_id = &"ai_regression"
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


func _build_ai_context(runtime, unit_state):
	var ai_context = BATTLE_AI_CONTEXT_SCRIPT.new()
	ai_context.state = runtime._state
	ai_context.unit_state = unit_state
	ai_context.grid_service = runtime._grid_service
	ai_context.skill_defs = runtime._skill_defs
	ai_context.preview_callback = Callable(runtime, "preview_command")
	return ai_context


func _build_ai_unit(
	unit_id: StringName,
	display_name: String,
	faction_id: StringName,
	coord: Vector2i,
	brain_id: StringName,
	state_id: StringName,
	skill_ids: Array[StringName],
	current_hp: int,
	current_ap: int
):
	var unit = BATTLE_UNIT_STATE_SCRIPT.new()
	unit.unit_id = unit_id
	unit.display_name = display_name
	unit.faction_id = faction_id
	unit.control_mode = &"ai"
	unit.ai_brain_id = brain_id
	unit.ai_state_id = state_id
	unit.current_hp = current_hp
	unit.current_mp = 8
	unit.current_stamina = 8
	unit.current_ap = current_ap
	unit.is_alive = true
	unit.set_anchor_coord(coord)
	unit.attribute_snapshot.set_value(&"hp_max", maxi(current_hp, 24))
	unit.attribute_snapshot.set_value(&"mp_max", 8)
	unit.attribute_snapshot.set_value(&"stamina_max", 8)
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


func _build_manual_unit(
	unit_id: StringName,
	display_name: String,
	faction_id: StringName,
	coord: Vector2i,
	skill_ids: Array[StringName]
):
	var unit = BATTLE_UNIT_STATE_SCRIPT.new()
	unit.unit_id = unit_id
	unit.display_name = display_name
	unit.faction_id = faction_id
	unit.control_mode = &"manual"
	unit.current_hp = 30
	unit.current_ap = 2
	unit.is_alive = true
	unit.set_anchor_coord(coord)
	unit.attribute_snapshot.set_value(&"hp_max", 30)
	unit.attribute_snapshot.set_value(&"action_points", 2)
	unit.attribute_snapshot.set_value(&"physical_attack", 10)
	unit.attribute_snapshot.set_value(&"magic_attack", 6)
	unit.attribute_snapshot.set_value(&"speed", 10)
	unit.known_active_skill_ids = skill_ids.duplicate()
	for skill_id in unit.known_active_skill_ids:
		unit.known_skill_level_map[skill_id] = 1
	return unit


func _add_unit_to_state(runtime, state, unit, is_enemy: bool) -> void:
	state.units[unit.unit_id] = unit
	if is_enemy:
		state.enemy_unit_ids.append(unit.unit_id)
	else:
		state.ally_unit_ids.append(unit.unit_id)
	var placed = runtime._grid_service.place_unit(state, unit, unit.coord, true)
	_assert_true(placed, "测试单位 %s 应能放入测试战场。" % String(unit.unit_id))


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)
