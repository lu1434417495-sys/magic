extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/persistence/GameSession.cs")
const GAME_RUNTIME_FACADE_SCRIPT = preload("res://scripts/systems/game_runtime/GameRuntimeFacade.cs")
const BATTLE_RUNTIME_MODULE_SCRIPT = preload("res://scripts/systems/battle/runtime/BattleRuntimeModule.cs")
const BATTLE_AI_CONTEXT_SCRIPT = preload("res://scripts/systems/battle/ai/BattleAiContext.cs")
const BATTLE_AI_SERVICE_SCRIPT = preload("res://scripts/systems/battle/ai/BattleAiService.cs")
const BATTLE_AI_SCORE_PROFILE_SCRIPT = preload("res://scripts/systems/battle/ai/BattleAiScoreProfile.cs")
const BATTLE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/BattleState.cs")
const BATTLE_TIMELINE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/BattleTimelineState.cs")
const BATTLE_CELL_STATE_SCRIPT = preload("res://scripts/systems/battle/core/BattleCellState.cs")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/BattleUnitState.cs")
const BATTLE_STATUS_EFFECT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/BattleStatusEffectState.cs")
const COMBAT_EFFECT_DEF_SCRIPT = preload("res://scripts/player/progression/CombatEffectDef.cs")
const COMBAT_SKILL_DEF_SCRIPT = preload("res://scripts/player/progression/CombatSkillDef.cs")
const COMBAT_CAST_VARIANT_DEF_SCRIPT = preload("res://scripts/player/progression/CombatCastVariantDef.cs")
const ENCOUNTER_ANCHOR_DATA_SCRIPT = preload("res://scripts/systems/world/EncounterAnchorData.cs")
const BATTLE_TERRAIN_GENERATOR_SCRIPT = preload("res://scripts/systems/battle/terrain/BattleTerrainGenerator.cs")
const ENEMY_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/enemies/EnemyContentRegistry.cs")
const ENEMY_AI_BRAIN_DEF_SCRIPT = preload("res://scripts/enemies/EnemyAiBrainDef.cs")
const ENEMY_AI_STATE_DEF_SCRIPT = preload("res://scripts/enemies/EnemyAiStateDef.cs")
const ENEMY_TEMPLATE_DEF_SCRIPT = preload("res://scripts/enemies/EnemyTemplateDef.cs")
const SKILL_DEF_SCRIPT = preload("res://scripts/player/progression/SkillDef.cs")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/UnitBaseAttributes.cs")
const MOVE_TO_RANGE_ACTION_SCRIPT = preload("res://scripts/enemies/actions/MoveToRangeAction.cs")
const MOVE_TO_ADVANTAGE_POSITION_ACTION_SCRIPT = preload("res://scripts/enemies/actions/MoveToAdvantagePositionAction.cs")
const RETREAT_ACTION_SCRIPT = preload("res://scripts/enemies/actions/RetreatAction.cs")
const USE_CHARGE_ACTION_SCRIPT = preload("res://scripts/enemies/actions/UseChargeAction.cs")
const USE_CHARGE_PATH_AOE_ACTION_SCRIPT = preload("res://scripts/enemies/actions/UseChargePathAoeAction.cs")
const USE_GROUND_SKILL_ACTION_SCRIPT = preload("res://scripts/enemies/actions/UseGroundSkillAction.cs")
const USE_GROUND_REPOSITION_SKILL_ACTION_SCRIPT = preload("res://scripts/enemies/actions/UseGroundRepositionSkillAction.cs")
const MOVE_TO_MULTI_UNIT_SKILL_POSITION_ACTION_SCRIPT = preload("res://scripts/enemies/actions/MoveToMultiUnitSkillPositionAction.cs")
const USE_MULTI_UNIT_SKILL_ACTION_SCRIPT = preload("res://scripts/enemies/actions/UseMultiUnitSkillAction.cs")
const USE_RANDOM_CHAIN_SKILL_ACTION_SCRIPT = preload("res://scripts/enemies/actions/UseRandomChainSkillAction.cs")
const USE_UNIT_SKILL_ACTION_SCRIPT = preload("res://scripts/enemies/actions/UseUnitSkillAction.cs")
const WAIT_ACTION_SCRIPT = preload("res://scripts/enemies/actions/WaitAction.cs")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/AttributeService.cs")
const BattleRuntimeTestHelpers = preload("res://tests/shared/battle_runtime_test_helpers.gd")

const TEST_WORLD_CONFIG := "res://data/configs/world_map/test_world_map_config.tres"

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_enemy_content_registry_validates_loaded_skill_and_item_refs()
	_test_enemy_schema_validation_reports_missing_skill_and_drop_refs()
	_test_terrain_generator_prefers_anchor_region_tag_when_profile_empty()
	_test_game_runtime_facade_injects_enemy_content()
	_test_melee_close_in_prefers_screening_ranged_ally_when_healthy()
	_test_melee_screening_scores_actual_path_cost_block()
	_test_melee_screening_ignores_geometric_line_without_pressure()
	_test_ai_assembler_adds_whirlwind_charge_path_action()
	_test_ai_charge_path_aoe_scores_repeat_hits()
	_test_ai_runtime_plan_uses_auto_whirlwind_action()
	_test_ai_ground_skill_generates_legal_command()
	_test_ai_unit_skill_scores_ranged_role_threat_target()
	_test_nearest_role_threat_enemy_selector_prefers_reachable_ranged_output()
	_test_nearest_role_threat_enemy_selector_keeps_far_ranged_output_behind_frontline()
	_test_nearest_role_threat_enemy_selector_prefers_frontline_over_far_ranged()
	_test_ai_multi_unit_skill_generates_target_unit_ids()
	_test_ai_assembler_routes_random_chain_to_random_chain_action()
	_test_ai_random_chain_action_uses_candidate_pool_not_target_ids()
	_test_ai_ground_skill_minimum_hit_count_uses_effective_enemies()
	_test_ai_ground_control_requires_explicit_empty_control_opt_in()
	_test_ai_ground_control_opt_in_allows_empty_control_candidate()
	_test_ai_ground_control_opt_in_does_not_allow_empty_damage_only_skill()
	_test_ai_chain_skill_scores_friendly_bounce_risk()
	_test_ai_multi_unit_skill_prefers_max_targets_under_candidate_limit()
	_test_ai_multi_unit_positioning_moves_toward_max_targets()
	_test_ai_skill_distance_contract_uses_effective_weapon_range()
	_test_ai_move_to_range_uses_effective_weapon_range()
	_test_ai_ground_cone_distance_contract_uses_outer_reach()
	_test_ai_gust_of_wind_can_hit_from_outer_reach()
	_test_mage_controller_uses_gust_to_protect_threatened_ally()
	_test_ranged_archer_survival_position_beats_shot_when_too_close()
	_test_ranged_archer_survival_position_uses_enemy_threat_range()
	_test_mage_controller_uses_blink_escape_when_unsafe()
	_test_mage_controller_uses_lethal_fireball_before_blink_escape()
	_test_mage_retreat_state_still_uses_lethal_offense_when_safe()
	_test_retreat_action_uses_enemy_threat_range_progress()
	_test_ranged_archer_prefers_high_ground_position_before_shot()
	_test_ranged_controller_prefers_later_higher_score_skill_action()
	_test_ranged_suppressor_prefers_suppressive_fire_against_line_cluster()
	_test_ranged_suppressor_skips_stamina_blocked_suppressive_fire()
	_test_ranged_suppressor_skips_cooldown_blocked_suppressive_fire()
	_test_ai_unit_skill_action_skips_aura_blocked_primary_skill()
	_test_ai_unit_skill_action_selects_scoring_variant_id()
	_test_ai_unit_skill_action_ignores_locked_and_ground_options()
	_test_ai_unit_skill_action_preserves_empty_variant_for_base_skill()
	_test_runtime_rejects_invalid_unit_skill_variant_ids()
	_test_move_to_range_prefers_closing_distance_over_wait_when_far_from_band()
	_test_taunt_forces_nearest_enemy_selector_to_source_unit()
	_test_taunt_forces_lowest_hp_enemy_selector_to_source_unit()
	_test_taunt_forces_role_threat_enemy_selector_to_source_unit()
	_test_taunt_disadvantage_ignores_stale_dead_or_non_hostile_source()
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


func _test_enemy_content_registry_validates_loaded_skill_and_item_refs() -> void:
	var registry := ENEMY_CONTENT_REGISTRY_SCRIPT.new()
	var validation_errors := registry.validate()
	_assert_true(
		validation_errors.is_empty(),
		"EnemyContentRegistry 应校验正式敌方 skill/item 引用且不产生错误。 errors=%s" % [str(validation_errors)]
	)


func _test_enemy_schema_validation_reports_missing_skill_and_drop_refs() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var missing_action := USE_UNIT_SKILL_ACTION_SCRIPT.new()
	missing_action.action_id = &"missing_enemy_action_skill_ref"
	missing_action.skill_ids = [&"missing_enemy_action_skill"]
	missing_action.target_selector = &"nearest_enemy"
	missing_action.desired_min_distance = 1
	missing_action.desired_max_distance = 1
	missing_action.distance_reference = USE_UNIT_SKILL_ACTION_SCRIPT.DISTANCE_REF_TARGET_UNIT()

	var state_def = ENEMY_AI_STATE_DEF_SCRIPT.new()
	state_def.state_id = &"engage"
	state_def.actions = [missing_action]
	var brain = ENEMY_AI_BRAIN_DEF_SCRIPT.new()
	brain.brain_id = &"missing_enemy_skill_brain"
	brain.default_state_id = state_def.state_id
	brain.states = [state_def]
	var brain_errors: Array[String] = brain.validate_schema(game_session.get_skill_defs())
	_assert_true(
		_errors_contain_fragment(brain_errors, "references missing skill missing_enemy_action_skill"),
		"EnemyAiBrainDef schema 校验应报告 action skill_id 缺失。 errors=%s" % [str(brain_errors)]
	)

	var template = ENEMY_TEMPLATE_DEF_SCRIPT.new()
	template.template_id = &"missing_enemy_refs_template"
	template.display_name = "缺引用敌方模板"
	template.brain_id = brain.brain_id
	var template_tags: Array[StringName] = [ENEMY_TEMPLATE_DEF_SCRIPT.TAG_BEAST()]
	template.tags = template_tags
	var template_skill_ids: Array[StringName] = [&"missing_enemy_template_skill"]
	template.skill_ids = template_skill_ids
	for attribute_id in [&"strength", &"agility", &"constitution", &"perception", &"intelligence", &"willpower"]:
		template.base_attribute_overrides[attribute_id] = 8
	var drop_entry := DropEntryDef.new()
	drop_entry.drop_entry_id = &"missing_drop_item"
	drop_entry.drop_type = &"item"
	drop_entry.item_id = &"missing_enemy_drop_item"
	drop_entry.quantity = 1
	template.drop_entries.append(drop_entry)
	var template_errors: Array[String] = template.validate_schema(
		{brain.brain_id: brain},
		game_session.get_item_defs(),
		game_session.get_skill_defs()
	)
	_assert_true(
		_errors_contain_fragment(template_errors, "references missing skill missing_enemy_template_skill"),
		"EnemyTemplateDef schema 校验应报告 template skill_id 缺失。 errors=%s" % [str(template_errors)]
	)
	_assert_true(
		_errors_contain_fragment(template_errors, "references missing item_id missing_enemy_drop_item"),
		"EnemyTemplateDef schema 校验应报告掉落 item_id 缺失。 errors=%s" % [str(template_errors)]
	)
	game_session.free()


func _test_terrain_generator_prefers_anchor_region_tag_when_profile_empty() -> void:
	var generator = BATTLE_TERRAIN_GENERATOR_SCRIPT.new()
	var encounter_context := {
		"monster": {
			"region_tag": "canyon",
		},
		"battle_terrain_profile": "",
	}
	var terrain_profile_id := generator._resolve_terrain_profile_id(encounter_context, {})
	_assert_eq(
		terrain_profile_id,
		&"canyon",
		"battle_terrain_profile 为空时，anchor-only encounter 应回退使用 monster.region_tag。"
	)


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


func _test_melee_close_in_prefers_screening_ranged_ally_when_healthy() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(7, 8))
	runtime._state = state
	var wolf = _build_ai_unit(
		&"screening_wolf",
		"占位战士",
		&"hostile",
		Vector2i(1, 4),
		&"melee_aggressor",
		&"engage",
		[&"charge", &"basic_attack"],
		28,
		2
	)
	wolf.current_move_points = 2
	wolf.current_stamina = 80
	wolf.attribute_snapshot.set_value(&"stamina_max", 80)
	var archer = _build_ai_unit(
		&"screening_archer",
		"后排弓手",
		&"hostile",
		Vector2i(3, 6),
		&"ranged_archer",
		&"pressure",
		[&"archer_aimed_shot"],
		28,
		2
	)
	_apply_test_bow_weapon(archer, 6)
	var player = _build_manual_unit(&"screening_threat", "近战威胁", &"player", Vector2i(3, 3), [&"basic_attack"])
	_add_unit_to_state(runtime, state, wolf, true)
	_add_unit_to_state(runtime, state, archer, true)
	_add_unit_to_state(runtime, state, player, false)
	var ai_context = _build_ai_context(runtime, wolf)
	var action = MOVE_TO_RANGE_ACTION_SCRIPT.new()
	action.action_id = &"screening_close_in_probe"
	action.target_selector = &"nearest_enemy"
	action.desired_min_distance = 1
	action.desired_max_distance = 1
	action.screening_mode = &"ranged_ally"
	action.screening_min_hp_basis_points = 4000
	var decision = action.decide(ai_context)
	_assert_eq(
		decision.command.target_coord if decision != null and decision.command != null else Vector2i(-1, -1),
		Vector2i(3, 4),
		"健康近战接敌时，应优先选择仍能贴敌且位于敌方近战到己方弓手最短路上的占位格。"
	)
	wolf.current_hp = 8
	var low_hp_decision = action.decide(ai_context)
	_assert_eq(
		low_hp_decision.command.target_coord if low_hp_decision != null and low_hp_decision.command != null else Vector2i(-1, -1),
		Vector2i(2, 3),
		"低血且无防御技能时，接敌动作不应继续为了保护后排偏向占位格。"
	)


func _test_melee_screening_scores_actual_path_cost_block() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(6, 4))
	for blocked_coord in [
		Vector2i(2, 0),
		Vector2i(3, 0),
		Vector2i(4, 0),
		Vector2i(2, 1),
		Vector2i(3, 1),
		Vector2i(2, 2),
		Vector2i(3, 2),
	]:
		var cell = state.cells.get(blocked_coord)
		if cell == null:
			continue
		cell.base_terrain = BATTLE_CELL_STATE_SCRIPT.TERRAIN_DEEP_WATER()
		cell.recalculate_runtime_values()
	state.cell_columns = BATTLE_CELL_STATE_SCRIPT.build_columns_from_surface_cells(state.cells)
	runtime._state = state
	var wolf = _build_ai_unit(
		&"path_cost_screening_wolf",
		"占位战士",
		&"hostile",
		Vector2i(0, 3),
		&"melee_aggressor",
		&"engage",
		[&"charge", &"basic_attack"],
		28,
		2
	)
	wolf.current_move_points = 3
	var archer = _build_ai_unit(
		&"path_cost_screening_archer",
		"后排弓手",
		&"hostile",
		Vector2i(4, 1),
		&"ranged_archer",
		&"pressure",
		[&"archer_aimed_shot"],
		28,
		2
	)
	_apply_test_bow_weapon(archer, 6)
	var player = _build_manual_unit(&"path_cost_screening_threat", "近战威胁", &"player", Vector2i(1, 1), [&"basic_attack"])
	player.current_move_points = 5
	_add_unit_to_state(runtime, state, wolf, true)
	_add_unit_to_state(runtime, state, archer, true)
	_add_unit_to_state(runtime, state, player, false)
	var ai_context = _build_ai_context(runtime, wolf)
	var action = MOVE_TO_RANGE_ACTION_SCRIPT.new()
	action.action_id = &"path_cost_screening_probe"
	action.target_selector = &"nearest_enemy"
	action.desired_min_distance = 1
	action.desired_max_distance = 1
	action.screening_mode = &"ranged_ally"
	action.screening_min_hp_basis_points = 4000
	var screening_context: Dictionary = action._build_screening_context(ai_context)
	var metrics: Dictionary = action._build_screening_metrics(ai_context, Vector2i(3, 3), screening_context)
	_assert_true(bool(screening_context.get("enabled", false)), "敌方近战一两步能威胁弓手时，screening context 应启用。")
	_assert_true(int(metrics.get("bonus", 0)) > 0, "候选格实际增加敌方到弓手的路径成本时，应获得守线加分。")
	_assert_true(int(metrics.get("path_cost_delta", 0)) > 0, "守线 metrics 应记录实际路径成本增量。")
	_assert_true(not bool(metrics.get("on_shortest_path", true)), "该回归场景中的守线格不在几何最短路上，必须由实际路径成本命中。")
	_assert_eq(
		String(metrics.get("protected_unit_id", "")),
		"path_cost_screening_archer",
		"守线 metrics 应记录被保护的远程输出单位。"
	)


func _test_melee_screening_ignores_geometric_line_without_pressure() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(6, 8))
	runtime._state = state
	var wolf = _build_ai_unit(
		&"geometric_screening_wolf",
		"几何占位战士",
		&"hostile",
		Vector2i(0, 5),
		&"melee_aggressor",
		&"engage",
		[&"charge", &"basic_attack"],
		28,
		2
	)
	wolf.current_move_points = 3
	var archer = _build_ai_unit(
		&"geometric_screening_archer",
		"后排弓手",
		&"hostile",
		Vector2i(3, 6),
		&"ranged_archer",
		&"pressure",
		[&"archer_aimed_shot"],
		28,
		2
	)
	_apply_test_bow_weapon(archer, 6)
	var player = _build_manual_unit(&"geometric_screening_threat", "近战威胁", &"player", Vector2i(2, 3), [&"basic_attack"])
	player.current_move_points = 3
	_add_unit_to_state(runtime, state, wolf, true)
	_add_unit_to_state(runtime, state, archer, true)
	_add_unit_to_state(runtime, state, player, false)
	var ai_context = _build_ai_context(runtime, wolf)
	var action = MOVE_TO_RANGE_ACTION_SCRIPT.new()
	action.action_id = &"geometric_screening_probe"
	action.target_selector = &"nearest_enemy"
	action.desired_min_distance = 1
	action.desired_max_distance = 1
	action.screening_mode = &"ranged_ally"
	action.screening_min_hp_basis_points = 4000
	var screening_context: Dictionary = action._build_screening_context(ai_context)
	var metrics: Dictionary = action._build_screening_metrics(ai_context, Vector2i(3, 5), screening_context)
	_assert_true(bool(screening_context.get("enabled", false)), "敌方近战可威胁弓手时，screening context 应启用。")
	_assert_eq(
		int(metrics.get("bonus", 0)),
		0,
		"仅处于几何最短路但不增加路径成本、也不能贴身/反击的格子不应获得守线加分。"
	)


func _test_ai_assembler_adds_whirlwind_charge_path_action() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var brain = runtime._enemy_ai_brains.get(&"melee_aggressor")
	var spinner = _build_ai_unit(
		&"whirlwind_assembler",
		"自动旋风狼",
		&"hostile",
		Vector2i(1, 2),
		&"melee_aggressor",
		&"engage",
		[&"warrior_whirlwind_slash"],
		36,
		2
	)
	_prepare_test_whirlwind_user(spinner)
	var plan = runtime._ai_action_assembler.build_unit_action_plan(spinner, brain, runtime._skill_defs)
	var engage_actions: Array = plan.get_actions(&"engage")
	var found_path_action := false
	for action in engage_actions:
		if action == null or action.get_script() != USE_CHARGE_PATH_AOE_ACTION_SCRIPT:
			continue
		found_path_action = action.get_declared_skill_ids().has(&"warrior_whirlwind_slash")
		if found_path_action:
			break
	_assert_true(
		found_path_action,
		"AI 自动装配器应为 warrior_whirlwind_slash 生成 charge + path_step_aoe Action。"
	)


func _test_ai_charge_path_aoe_scores_repeat_hits() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(8, 5))
	runtime._state = state
	var spinner = _build_ai_unit(
		&"whirlwind_scorer",
		"旋风评分狼",
		&"hostile",
		Vector2i(1, 2),
		&"melee_aggressor",
		&"engage",
		[&"warrior_whirlwind_slash"],
		36,
		2
	)
	_prepare_test_whirlwind_user(spinner)
	var large_target = _build_manual_unit(&"whirlwind_large_target", "大型目标", &"player", Vector2i(2, 0), [&"warrior_heavy_strike"])
	large_target.set_body_size_category(&"large")
	_add_unit_to_state(runtime, state, spinner, true)
	_add_unit_to_state(runtime, state, large_target, false)
	var ai_context = _build_ai_context(runtime, spinner)
	var action = USE_CHARGE_PATH_AOE_ACTION_SCRIPT.new()
	action.action_id = &"whirlwind_path_aoe_probe"
	var action_skill_ids: Array[StringName] = [&"warrior_whirlwind_slash"]
	action.skill_ids = action_skill_ids
	action.target_selector = &"nearest_enemy"
	action.minimum_hit_count = 2
	var decision = action.decide(ai_context)
	_assert_true(decision != null and decision.command != null, "旋风斩路径 AOE Action 应能产出合法候选。")
	if decision == null or decision.command == null:
		return
	_assert_true(
		decision.score_input != null and decision.score_input.path_step_hit_count >= 2,
		"路径 AOE 评分应统计同一大型目标被沿途多次命中的收益。"
	)
	_assert_true(
		decision.score_input != null and decision.score_input.path_step_payoff_score > 0,
		"路径 AOE 评分应把沿途命中转成正向 hit payoff。"
	)
	var preview = runtime.preview_command(decision.command)
	_assert_true(preview != null and preview.allowed, "旋风斩路径 AOE Action 生成的命令必须通过 preview_command。")


func _test_ai_runtime_plan_uses_auto_whirlwind_action() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(8, 5))
	runtime._state = state
	var spinner = _build_ai_unit(
		&"whirlwind_auto_runtime",
		"自动旋风运行时",
		&"hostile",
		Vector2i(1, 2),
		&"melee_aggressor",
		&"engage",
		[&"warrior_whirlwind_slash"],
		36,
		2
	)
	_prepare_test_whirlwind_user(spinner)
	var large_target = _build_manual_unit(&"whirlwind_runtime_target", "运行时大型目标", &"player", Vector2i(2, 0), [&"warrior_heavy_strike"])
	large_target.set_body_size_category(&"large")
	_add_unit_to_state(runtime, state, spinner, true)
	_add_unit_to_state(runtime, state, large_target, false)
	runtime._build_ai_action_plans()
	var ai_context = _build_ai_context(runtime, spinner)
	var decision = runtime._ai_service.choose_command(ai_context)
	_assert_true(decision != null and decision.command != null, "运行时自动 Action plan 应能产出 AI 指令。")
	_assert_eq(
		decision.command.skill_id if decision != null and decision.command != null else &"",
		&"warrior_whirlwind_slash",
		"未在 brain .tres 手写列出的 warrior_whirlwind_slash 应通过自动装配参与决策。"
	)
	_assert_true(
		decision != null and decision.score_input != null and decision.score_input.path_step_hit_count >= 2,
		"运行时选择旋风斩时应携带路径 AOE 评分指标。"
	)


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
	mist.attribute_snapshot.set_value(&"intelligence", 20)
	mist.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.SPELL_PROFICIENCY_BONUS_ID(), 4)
	var player_a = _build_manual_unit(&"player_a", "玩家A", &"player", Vector2i(4, 2), [&"warrior_heavy_strike"])
	var player_b = _build_manual_unit(&"player_b", "玩家B", &"player", Vector2i(4, 3), [&"warrior_heavy_strike"])
	player_a.attribute_snapshot.set_value(&"agility", 10)
	player_b.attribute_snapshot.set_value(&"agility", 10)
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


func _test_ai_unit_skill_scores_ranged_role_threat_target() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(7, 5))
	runtime._state = state
	var caster = _build_ai_unit(
		&"role_threat_lancer",
		"威胁评分术士",
		&"hostile",
		Vector2i(1, 2),
		&"ranged_controller",
		&"pressure",
		[&"mage_ice_lance"],
		24,
		2
	)
	caster.current_mp = 120
	caster.attribute_snapshot.set_value(&"mp_max", 120)
	var normal_target = _build_manual_unit(&"role_threat_normal", "普通目标", &"player", Vector2i(4, 1), [&"warrior_heavy_strike"])
	var ranged_target = _build_manual_unit(&"role_threat_archer", "远程威胁目标", &"player", Vector2i(4, 3), [&"archer_aimed_shot", &"basic_attack"])
	_apply_test_bow_weapon(ranged_target, 6)
	_add_unit_to_state(runtime, state, caster, true)
	_add_unit_to_state(runtime, state, normal_target, false)
	_add_unit_to_state(runtime, state, ranged_target, false)
	var ai_context = _build_ai_context(runtime, caster)
	var action = USE_UNIT_SKILL_ACTION_SCRIPT.new()
	action.action_id = &"role_threat_unit_probe"
	var skill_ids: Array[StringName] = [&"mage_ice_lance"]
	action.skill_ids = skill_ids
	action.target_selector = &"nearest_enemy"
	action.desired_min_distance = 3
	action.desired_max_distance = 4
	action.distance_reference = &"target_unit"
	var decision = action.decide(ai_context)
	_assert_true(decision != null and decision.command != null, "单体技能威胁评分回归应产出合法指令。")
	_assert_eq(
		decision.command.target_unit_id if decision != null and decision.command != null else &"",
		ranged_target.unit_id,
		"单体技能在距离和伤害相同时，应因远程输出威胁优先选择远程攻击单位。"
	)
	_assert_true(
		decision != null and decision.score_input != null and decision.score_input.target_priority_score > 0,
		"单体技能评分应把目标角色威胁写入 target_priority_score。"
	)


func _test_nearest_role_threat_enemy_selector_prefers_reachable_ranged_output() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(9, 5))
	runtime._state = state
	var wolf = _build_ai_unit(
		&"role_selector_wolf",
		"威胁接敌狼",
		&"hostile",
		Vector2i(1, 2),
		&"melee_aggressor",
		&"engage",
		[&"basic_attack"],
		30,
		2
	)
	var melee_target = _build_manual_unit(&"role_selector_melee", "近处前排", &"player", Vector2i(3, 2), [&"warrior_heavy_strike"])
	var ranged_target = _build_manual_unit(&"role_selector_archer", "可压制远程", &"player", Vector2i(5, 2), [&"archer_aimed_shot", &"basic_attack"])
	_apply_test_melee_weapon(melee_target, 1)
	_apply_test_bow_weapon(ranged_target, 6)
	_add_unit_to_state(runtime, state, wolf, true)
	_add_unit_to_state(runtime, state, melee_target, false)
	_add_unit_to_state(runtime, state, ranged_target, false)
	var ai_context = _build_ai_context(runtime, wolf)
	var action = MOVE_TO_RANGE_ACTION_SCRIPT.new()
	var targets = action._sort_target_units(ai_context, &"enemy", &"nearest_role_threat_enemy")
	_assert_true(not targets.is_empty(), "nearest_role_threat_enemy 应返回敌方候选。")
	_assert_eq(
		targets[0].unit_id if not targets.is_empty() else &"",
		ranged_target.unit_id,
		"近战接敌选择器应在近距离窗口内优先压制远程输出，而不是永远锁最近前排。"
	)


func _test_nearest_role_threat_enemy_selector_keeps_far_ranged_output_behind_frontline() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(12, 5))
	runtime._state = state
	var wolf = _build_ai_unit(
		&"role_selector_far_wolf",
		"远程窗口狼",
		&"hostile",
		Vector2i(1, 2),
		&"melee_aggressor",
		&"engage",
		[&"basic_attack"],
		30,
		2
	)
	var melee_target = _build_manual_unit(&"role_selector_near_guard", "贴脸前排", &"player", Vector2i(2, 2), [&"warrior_heavy_strike"])
	var ranged_target = _build_manual_unit(&"role_selector_far_archer", "远处远程", &"player", Vector2i(9, 2), [&"archer_aimed_shot", &"basic_attack"])
	_apply_test_melee_weapon(melee_target, 1)
	_apply_test_bow_weapon(ranged_target, 6)
	_add_unit_to_state(runtime, state, wolf, true)
	_add_unit_to_state(runtime, state, melee_target, false)
	_add_unit_to_state(runtime, state, ranged_target, false)
	var ai_context = _build_ai_context(runtime, wolf)
	var action = MOVE_TO_RANGE_ACTION_SCRIPT.new()
	var targets = action._sort_target_units(ai_context, &"enemy", &"nearest_role_threat_enemy")
	_assert_true(not targets.is_empty(), "nearest_role_threat_enemy 远距回归应返回敌方候选。")
	_assert_eq(
		targets[0].unit_id if not targets.is_empty() else &"",
		melee_target.unit_id,
		"远程输出超出近距离窗口时，近战接敌选择器应先处理已经贴近的前排。"
	)


func _test_nearest_role_threat_enemy_selector_prefers_frontline_over_far_ranged() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(14, 5))
	runtime._state = state
	var wolf = _build_ai_unit(
		&"role_selector_frontline_wolf",
		"前排优先狼",
		&"hostile",
		Vector2i(1, 2),
		&"melee_aggressor",
		&"engage",
		[&"basic_attack"],
		30,
		2
	)
	var ranged_target = _build_manual_unit(&"role_selector_far_but_closer_archer", "稍近远程", &"player", Vector2i(9, 2), [&"archer_aimed_shot", &"basic_attack"])
	var melee_target = _build_manual_unit(&"role_selector_far_frontline", "更远前排", &"player", Vector2i(10, 2), [&"warrior_heavy_strike"])
	_apply_test_bow_weapon(ranged_target, 6)
	_apply_test_melee_weapon(melee_target, 1)
	_add_unit_to_state(runtime, state, wolf, true)
	_add_unit_to_state(runtime, state, ranged_target, false)
	_add_unit_to_state(runtime, state, melee_target, false)
	var ai_context = _build_ai_context(runtime, wolf)
	var action = MOVE_TO_RANGE_ACTION_SCRIPT.new()
	var targets = action._sort_target_units(ai_context, &"enemy", &"nearest_role_threat_enemy")
	_assert_true(not targets.is_empty(), "nearest_role_threat_enemy 前排回归应返回敌方候选。")
	_assert_eq(
		targets[0].unit_id if not targets.is_empty() else &"",
		melee_target.unit_id,
		"远程输出不在可争夺窗口内时，即使几何距离略近，近战接敌也应优先敌方接触威胁。"
	)


func _test_ai_multi_unit_skill_generates_target_unit_ids() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(7, 5))
	runtime._state = state
	var archer = _build_ai_unit(
		&"ranged_archer_multishot",
		"多目标弓手",
		&"hostile",
		Vector2i(1, 2),
		&"ranged_archer",
		&"pressure",
		[&"archer_aimed_shot", &"basic_attack", &"archer_multishot"],
		28,
		2
	)
	archer.current_stamina = 100
	archer.attribute_snapshot.set_value(&"stamina_max", 100)
	archer.apply_weapon_projection({
		"weapon_profile_kind": "equipped",
		"weapon_item_id": "ai_test_longbow",
		"weapon_profile_type_id": "longbow",
		"weapon_family": "bow",
		"weapon_current_grip": "two_handed",
		"weapon_attack_range": 6,
		"weapon_two_handed_dice": {"dice_count": 1, "dice_sides": 8, "flat_bonus": 0},
		"weapon_uses_two_hands": true,
		"weapon_physical_damage_tag": "physical_pierce",
	})
	var player_a = _build_manual_unit(&"multi_target_a", "目标A", &"player", Vector2i(4, 1), [&"warrior_heavy_strike"])
	var player_b = _build_manual_unit(&"multi_target_b", "目标B", &"player", Vector2i(4, 2), [&"warrior_heavy_strike"])
	var player_c = _build_manual_unit(&"multi_target_c", "目标C", &"player", Vector2i(4, 3), [&"warrior_heavy_strike"])
	_add_unit_to_state(runtime, state, archer, true)
	_add_unit_to_state(runtime, state, player_a, false)
	_add_unit_to_state(runtime, state, player_b, false)
	_add_unit_to_state(runtime, state, player_c, false)
	var ai_context = _build_ai_context(runtime, archer)
	var action = USE_MULTI_UNIT_SKILL_ACTION_SCRIPT.new()
	action.action_id = &"multi_unit_probe"
	var action_skill_ids: Array[StringName] = [&"archer_multishot"]
	action.skill_ids = action_skill_ids
	action.target_selector = &"nearest_enemy"
	action.desired_min_distance = 3
	action.desired_max_distance = 5
	action.distance_reference = &"target_unit"
	var action_decision = action.decide(ai_context)
	_assert_true(action_decision != null and action_decision.command != null, "multi-unit action 应能产出合法候选指令。")
	_assert_true(
		action_decision != null and action_decision.command != null and action_decision.command.target_unit_ids.size() >= 2,
		"multi-unit action 应通过 target_unit_ids 携带多个目标，而不是只写地格。"
	)
	var action_preview = runtime.preview_command(action_decision.command if action_decision != null else null)
	_assert_true(action_preview != null and action_preview.allowed, "multi-unit action 生成的命令必须通过 preview_command。")

	var decision = runtime._ai_service.choose_command(ai_context)
	_assert_true(decision != null and decision.state_id == &"pressure", "ranged_archer 多目标场景下应保持 pressure 状态。")
	_assert_eq(
		decision.command.skill_id if decision != null and decision.command != null else &"",
		&"archer_multishot",
		"ranged_archer 面对多个合法目标时应选择 archer_multishot。"
	)
	_assert_true(
		decision != null and decision.command != null and decision.command.target_unit_ids.size() >= 2,
		"ranged_archer 的 archer_multishot 命令必须写入 target_unit_ids。"
	)
	var preview = runtime.preview_command(decision.command if decision != null else null)
	_assert_true(preview != null and preview.allowed, "ranged_archer 产出的 archer_multishot 命令必须通过 preview_command。")
	_assert_true(preview != null and preview.target_unit_ids.size() >= 2, "archer_multishot 预览应命中多个单位。")


func _test_ai_assembler_routes_random_chain_to_random_chain_action() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var random_chain_skill = _build_test_random_chain_skill(&"ai_random_chain_route_test")
	runtime._skill_defs[random_chain_skill.skill_id] = random_chain_skill
	var brain = runtime._enemy_ai_brains.get(&"melee_aggressor")
	var chain_user = _build_ai_unit(
		&"random_chain_assembler_user",
		"随机链装配者",
		&"hostile",
		Vector2i(1, 2),
		&"melee_aggressor",
		&"engage",
		[random_chain_skill.skill_id],
		30,
		2
	)
	var plan = runtime._ai_action_assembler.build_unit_action_plan(chain_user, brain, runtime._skill_defs)
	var engage_actions: Array = plan.get_actions(&"engage")
	var found_random_chain_action := false
	var found_multi_unit_action := false
	var found_multi_unit_move_action := false
	var found_move_to_range_action := false
	for action in engage_actions:
		if action == null:
			continue
		if action.get_script() == USE_RANDOM_CHAIN_SKILL_ACTION_SCRIPT:
			found_random_chain_action = action.get_declared_skill_ids().has(random_chain_skill.skill_id)
		if action.get_script() == USE_MULTI_UNIT_SKILL_ACTION_SCRIPT:
			found_multi_unit_action = found_multi_unit_action or action.get_declared_skill_ids().has(random_chain_skill.skill_id)
		if action.get_script() == MOVE_TO_MULTI_UNIT_SKILL_POSITION_ACTION_SCRIPT:
			found_multi_unit_move_action = found_multi_unit_move_action or action.get_declared_skill_ids().has(random_chain_skill.skill_id)
		if action.get_script() == MOVE_TO_RANGE_ACTION_SCRIPT:
			var range_skill_ids = action.get("range_skill_ids")
			found_move_to_range_action = found_move_to_range_action \
				or (range_skill_ids is Array and (range_skill_ids as Array).has(random_chain_skill.skill_id))
	_assert_true(found_random_chain_action, "AI 自动装配器应为 random_chain 技能生成专用 UseRandomChainSkillAction。")
	_assert_true(not found_multi_unit_action, "random_chain 技能不应再生成 UseMultiUnitSkillAction。")
	_assert_true(not found_multi_unit_move_action, "random_chain 技能不应再生成 MoveToMultiUnitSkillPositionAction。")
	_assert_true(found_move_to_range_action, "random_chain 技能应使用 MoveToRangeAction 靠近可施放距离。")


func _test_ai_random_chain_action_uses_candidate_pool_not_target_ids() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var random_chain_skill = _build_test_random_chain_skill(&"ai_random_chain_score_test")
	runtime._skill_defs[random_chain_skill.skill_id] = random_chain_skill
	var state = _build_flat_state(Vector2i(6, 3))
	runtime._state = state
	var chain_user = _build_ai_unit(
		&"random_chain_action_user",
		"随机链行动者",
		&"hostile",
		Vector2i(1, 1),
		&"melee_aggressor",
		&"engage",
		[random_chain_skill.skill_id],
		30,
		2
	)
	var target_a = _build_manual_unit(&"random_chain_candidate_a", "随机候选A", &"player", Vector2i(2, 1), [&"warrior_heavy_strike"])
	var target_b = _build_manual_unit(&"random_chain_candidate_b", "随机候选B", &"player", Vector2i(3, 1), [&"warrior_heavy_strike"])
	_add_unit_to_state(runtime, state, chain_user, true)
	_add_unit_to_state(runtime, state, target_a, false)
	_add_unit_to_state(runtime, state, target_b, false)
	var ai_context = _build_ai_context(runtime, chain_user)
	ai_context.trace_enabled = true
	var action = USE_RANDOM_CHAIN_SKILL_ACTION_SCRIPT.new()
	action.action_id = &"random_chain_probe"
	var action_skill_ids: Array[StringName] = [random_chain_skill.skill_id]
	action.skill_ids = action_skill_ids
	action.target_selector = &"nearest_enemy"
	action.desired_min_distance = 1
	action.desired_max_distance = 3
	action.distance_reference = USE_RANDOM_CHAIN_SKILL_ACTION_SCRIPT.DISTANCE_REF_CANDIDATE_POOL()
	var decision = action.decide(ai_context)
	_assert_true(decision != null and decision.command != null, "random_chain action 应能产出合法候选指令。")
	if decision == null or decision.command == null:
		return
	_assert_eq(decision.command.target_unit_ids, [], "random_chain AI command 不应携带确定 target_unit_ids。")
	var preview = runtime.preview_command(decision.command)
	_assert_true(preview != null and preview.allowed, "random_chain AI command 应通过 preview。")
	_assert_eq(preview.target_unit_ids, [], "random_chain preview 不应伪造确定目标。")
	_assert_true(
		preview.random_chain_candidate_unit_ids.has(target_a.unit_id) and preview.random_chain_candidate_unit_ids.has(target_b.unit_id),
		"random_chain preview 应暴露候选池而不是写入 target_unit_ids。"
	)
	var score_input = decision.score_input
	_assert_true(score_input != null, "random_chain action 应携带专用评分输入。")
	if score_input == null:
		return
	_assert_eq(score_input.action_kind, &"random_chain_skill", "random_chain 评分应使用专用 action_kind。")
	_assert_eq(score_input.target_unit_ids, [], "random_chain 评分不应把候选池伪装成确定目标。")
	_assert_eq(score_input.target_count, 0, "random_chain 评分 target_count 应保持确定目标数量为 0。")
	_assert_eq(score_input.random_chain_candidate_pool_count, 2, "random_chain 评分应记录候选池大小。")
	_assert_true(
		score_input.random_chain_candidate_unit_ids.has(target_a.unit_id) and score_input.random_chain_candidate_unit_ids.has(target_b.unit_id),
		"random_chain 评分应记录候选池单位。"
	)
	_assert_true(score_input.effective_target_count > 0, "random_chain 评分应基于候选池产出正向期望收益。")
	_assert_true(score_input.total_score > 0, "random_chain 评分应产生正向总分。")
	_assert_eq(score_input.random_chain_selection_policy, &"random_from_living_pool", "random_chain 评分应说明执行期随机池策略。")
	_assert_eq(score_input.random_chain_score_estimate_policy, &"expected_value", "random_chain 评分应说明使用期望值估算。")
	_assert_true(not ai_context.action_traces.is_empty(), "random_chain action 应写入 AI trace。")
	if ai_context.action_traces.is_empty():
		return
	var trace = ai_context.action_traces[0]
	_assert_eq(String(trace.get("metadata", {}).get("action_kind", "")), "random_chain_skill", "random_chain trace 应标记专用 action kind。")
	_assert_true(
		(trace.get("metadata", {}).get("candidate_pool_unit_ids", []) as Array).has(String(target_a.unit_id)) \
			and (trace.get("metadata", {}).get("candidate_pool_unit_ids", []) as Array).has(String(target_b.unit_id)),
		"random_chain trace metadata 应记录候选池，而不是确定目标。"
	)


func _test_ai_ground_skill_minimum_hit_count_uses_effective_enemies() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(8, 6))
	runtime._state = state
	var mage = _build_ai_unit(
		&"friendly_fire_fireball_mage",
		"友伤火球法师",
		&"hostile",
		Vector2i(1, 2),
		&"mage_controller",
		&"pressure",
		[&"mage_fireball"],
		28,
		1
	)
	mage.current_mp = 1000
	mage.attribute_snapshot.set_value(&"mp_max", 1000)
	mage.known_skill_level_map[&"mage_fireball"] = 7
	var target = _build_manual_unit(&"friendly_fire_target", "有效敌人", &"player", Vector2i(5, 2), [&"warrior_heavy_strike"])
	var ally = _build_ai_unit(
		&"friendly_fire_ally",
		"误伤友军",
		&"hostile",
		Vector2i(5, 3),
		&"melee_aggressor",
		&"pressure",
		[&"warrior_heavy_strike"],
		30,
		1
	)
	_add_unit_to_state(runtime, state, mage, true)
	_add_unit_to_state(runtime, state, target, false)
	_add_unit_to_state(runtime, state, ally, true)
	var ai_context = _build_ai_context(runtime, mage)
	var command = _build_test_ground_skill_command(mage, &"mage_fireball", target.coord)
	var preview = runtime.preview_command(command)
	_assert_true(preview != null and preview.allowed, "友伤火球命令必须通过 preview。")
	var action = USE_GROUND_SKILL_ACTION_SCRIPT.new()
	action.action_id = &"friendly_fire_fireball_probe"
	var skill_ids: Array[StringName] = [&"mage_fireball"]
	action.skill_ids = skill_ids
	action.minimum_hit_count = 2
	action.desired_min_distance = 4
	action.desired_max_distance = 5
	action.distance_reference = &"target_coord"
	var decision = action.decide(ai_context)
	_assert_true(decision == null, "只有 1 个有效敌人加 1 个友军时，minimum_hit_count=2 的火球候选应被过滤。")


func _test_ai_ground_control_requires_explicit_empty_control_opt_in() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var ground_control_skill = _build_test_ground_control_skill(&"ai_empty_ground_control_default_reject_test")
	runtime._skill_defs[ground_control_skill.skill_id] = ground_control_skill
	var state = _build_flat_state(Vector2i(6, 5))
	runtime._state = state
	var caster = _build_ai_unit(
		&"empty_ground_control_default_caster",
		"默认拒绝控场者",
		&"hostile",
		Vector2i(1, 2),
		&"mage_controller",
		&"pressure",
		[ground_control_skill.skill_id],
		28,
		1
	)
	_add_unit_to_state(runtime, state, caster, true)
	var action = USE_GROUND_SKILL_ACTION_SCRIPT.new()
	action.action_id = &"empty_ground_control_default_probe"
	var skill_ids: Array[StringName] = [ground_control_skill.skill_id]
	action.skill_ids = skill_ids
	action.minimum_hit_count = 1
	action.desired_min_distance = 0
	action.desired_max_distance = 5
	action.distance_reference = USE_GROUND_SKILL_ACTION_SCRIPT.DISTANCE_REF_TARGET_COORD()
	var decision = action.decide(_build_ai_context(runtime, caster))
	_assert_true(decision == null, "默认不开启 allow_empty_ground_control 时，0 有效目标的地格控场应被拒绝。")


func _test_ai_ground_control_opt_in_allows_empty_control_candidate() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var ground_control_skill = _build_test_ground_control_skill(&"ai_empty_ground_control_accept_test")
	runtime._skill_defs[ground_control_skill.skill_id] = ground_control_skill
	var state = _build_flat_state(Vector2i(6, 5))
	runtime._state = state
	var caster = _build_ai_unit(
		&"empty_ground_control_accept_caster",
		"空地控场者",
		&"hostile",
		Vector2i(1, 2),
		&"mage_controller",
		&"pressure",
		[ground_control_skill.skill_id],
		28,
		1
	)
	_add_unit_to_state(runtime, state, caster, true)
	var action = USE_GROUND_SKILL_ACTION_SCRIPT.new()
	action.action_id = &"empty_ground_control_accept_probe"
	var skill_ids: Array[StringName] = [ground_control_skill.skill_id]
	action.skill_ids = skill_ids
	action.minimum_hit_count = 1
	action.allow_empty_ground_control = true
	action.minimum_ground_control_score = 1
	action.desired_min_distance = 0
	action.desired_max_distance = 5
	action.distance_reference = USE_GROUND_SKILL_ACTION_SCRIPT.DISTANCE_REF_TARGET_COORD()
	var decision = action.decide(_build_ai_context(runtime, caster))
	_assert_true(decision != null and decision.command != null, "显式开启空地控场后，0 有效目标的地格控制候选应可被选择。")
	if decision == null or decision.command == null:
		return
	var preview = runtime.preview_command(decision.command)
	_assert_true(preview != null and preview.allowed, "空地控场决策命令必须通过 runtime preview。")
	_assert_true(preview != null and preview.target_unit_ids.is_empty(), "空地控场决策不应依赖目标单位。")
	_assert_eq(decision.score_input.effective_target_count, 0, "空地控场决策不应伪造有效命中数。")
	_assert_true(decision.score_input.estimated_ground_control_cell_count > 0, "空地控场决策应暴露受控地格数。")
	_assert_true(decision.score_input.ground_control_score >= action.minimum_ground_control_score, "空地控场决策应满足地格控制评分门槛。")


func _test_ai_ground_control_opt_in_does_not_allow_empty_damage_only_skill() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var damage_skill = _build_test_ground_damage_skill(&"ai_empty_ground_damage_reject_test")
	runtime._skill_defs[damage_skill.skill_id] = damage_skill
	var state = _build_flat_state(Vector2i(6, 5))
	runtime._state = state
	var caster = _build_ai_unit(
		&"empty_ground_damage_caster",
		"空地伤害者",
		&"hostile",
		Vector2i(1, 2),
		&"mage_controller",
		&"pressure",
		[damage_skill.skill_id],
		28,
		1
	)
	_add_unit_to_state(runtime, state, caster, true)
	var action = USE_GROUND_SKILL_ACTION_SCRIPT.new()
	action.action_id = &"empty_ground_damage_probe"
	var skill_ids: Array[StringName] = [damage_skill.skill_id]
	action.skill_ids = skill_ids
	action.minimum_hit_count = 1
	action.allow_empty_ground_control = true
	action.minimum_ground_control_score = 1
	action.desired_min_distance = 0
	action.desired_max_distance = 5
	action.distance_reference = USE_GROUND_SKILL_ACTION_SCRIPT.DISTANCE_REF_TARGET_COORD()
	var decision = action.decide(_build_ai_context(runtime, caster))
	_assert_true(decision == null, "即使开启 allow_empty_ground_control，纯伤害地面技能也不能空放。")


func _test_ai_chain_skill_scores_friendly_bounce_risk() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(8, 6))
	runtime._state = state
	var mage = _build_ai_unit(
		&"friendly_chain_mage",
		"友伤链闪法师",
		&"hostile",
		Vector2i(1, 2),
		&"mage_controller",
		&"pressure",
		[&"mage_chain_lightning"],
		28,
		1
	)
	mage.current_mp = 1000
	mage.attribute_snapshot.set_value(&"mp_max", 1000)
	mage.known_skill_level_map[&"mage_chain_lightning"] = 7
	var target = _build_manual_unit(&"friendly_chain_target", "链闪敌人", &"player", Vector2i(5, 2), [&"warrior_heavy_strike"])
	var ally = _build_ai_unit(
		&"friendly_chain_ally",
		"链闪友军",
		&"hostile",
		Vector2i(5, 3),
		&"melee_aggressor",
		&"pressure",
		[&"warrior_heavy_strike"],
		30,
		1
	)
	_add_unit_to_state(runtime, state, mage, true)
	_add_unit_to_state(runtime, state, target, false)
	_add_unit_to_state(runtime, state, ally, true)
	var ai_context = _build_ai_context(runtime, mage)
	var command = _build_test_unit_skill_command(mage, &"mage_chain_lightning", target)
	var preview = runtime.preview_command(command)
	_assert_true(preview != null and preview.allowed, "友伤链闪命令必须通过 preview。")
	var action = USE_UNIT_SKILL_ACTION_SCRIPT.new()
	action.action_id = &"friendly_chain_probe"
	var skill_ids: Array[StringName] = [&"mage_chain_lightning"]
	action.skill_ids = skill_ids
	action.target_selector = &"nearest_enemy"
	action.desired_min_distance = 4
	action.desired_max_distance = 5
	action.distance_reference = &"target_unit"
	var decision = action.decide(ai_context)
	_assert_true(decision == null, "默认 unit skill action 应过滤会弹射友军的链闪候选。")


func _test_ai_multi_unit_skill_prefers_max_targets_under_candidate_limit() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(8, 8))
	runtime._state = state
	var archer = _build_ai_unit(
		&"ranged_archer_max_multishot",
		"满目标弓手",
		&"hostile",
		Vector2i(1, 3),
		&"ranged_archer",
		&"pressure",
		[&"archer_multishot"],
		28,
		2
	)
	archer.current_stamina = 100
	archer.attribute_snapshot.set_value(&"stamina_max", 100)
	archer.known_skill_level_map[&"archer_multishot"] = 7
	archer.apply_weapon_projection({
		"weapon_profile_kind": "equipped",
		"weapon_item_id": "ai_test_longbow",
		"weapon_profile_type_id": "longbow",
		"weapon_family": "bow",
		"weapon_current_grip": "two_handed",
		"weapon_attack_range": 6,
		"weapon_two_handed_dice": {"dice_count": 1, "dice_sides": 8, "flat_bonus": 0},
		"weapon_uses_two_hands": true,
		"weapon_physical_damage_tag": "physical_pierce",
	})
	_add_unit_to_state(runtime, state, archer, true)
	for index in range(6):
		var target = _build_manual_unit(
			StringName("max_multishot_target_%d" % index),
			"满目标靶%d" % index,
			&"player",
			Vector2i(4, index),
			[&"warrior_heavy_strike"]
		)
		_add_unit_to_state(runtime, state, target, false)
	var ai_context = _build_ai_context(runtime, archer)
	var action = USE_MULTI_UNIT_SKILL_ACTION_SCRIPT.new()
	action.action_id = &"max_multi_unit_probe"
	var action_skill_ids: Array[StringName] = [&"archer_multishot"]
	action.skill_ids = action_skill_ids
	action.target_selector = &"lowest_hp_enemy"
	action.desired_min_distance = 3
	action.desired_max_distance = 5
	action.distance_reference = &"target_unit"
	action.candidate_pool_limit = 6
	action.candidate_group_limit = 12
	var action_decision = action.decide(ai_context)
	_assert_true(action_decision != null and action_decision.command != null, "multi-unit action 应能产出满目标候选指令。")
	_assert_eq(
		action_decision.command.target_unit_ids.size() if action_decision != null and action_decision.command != null else 0,
		5,
		"candidate_group_limit=12 时，L7 连珠箭应优先评估并选择 5 目标组合。"
	)
	var action_preview = runtime.preview_command(action_decision.command if action_decision != null else null)
	_assert_true(action_preview != null and action_preview.allowed, "满目标 multi-unit action 命令必须通过 preview_command。")
	_assert_eq(
		action_preview.target_unit_ids.size() if action_preview != null else 0,
		5,
		"满目标连珠箭预览应保留 5 个单位目标。"
	)


func _test_ai_multi_unit_positioning_moves_toward_max_targets() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(8, 8))
	runtime._state = state
	var archer = _build_ai_unit(
		&"ranged_archer_multishot_position",
		"找点弓手",
		&"hostile",
		Vector2i(1, 3),
		&"ranged_archer",
		&"pressure",
		[&"archer_multishot"],
		28,
		2
	)
	archer.current_move_points = 2
	archer.current_stamina = 100
	archer.attribute_snapshot.set_value(&"stamina_max", 100)
	archer.known_skill_level_map[&"archer_multishot"] = 7
	archer.apply_weapon_projection({
		"weapon_profile_kind": "equipped",
		"weapon_item_id": "ai_test_short_bow",
		"weapon_profile_type_id": "shortbow",
		"weapon_family": "bow",
		"weapon_current_grip": "two_handed",
		"weapon_attack_range": 4,
		"weapon_two_handed_dice": {"dice_count": 1, "dice_sides": 6, "flat_bonus": 0},
		"weapon_uses_two_hands": true,
		"weapon_physical_damage_tag": "physical_pierce",
	})
	_add_unit_to_state(runtime, state, archer, true)
	for index in range(5):
		var target = _build_manual_unit(
			StringName("position_multishot_target_%d" % index),
			"找点靶%d" % index,
			&"player",
			Vector2i(5, index + 1),
			[&"warrior_heavy_strike"]
		)
		_add_unit_to_state(runtime, state, target, false)
	var ai_context = _build_ai_context(runtime, archer)
	var action = MOVE_TO_MULTI_UNIT_SKILL_POSITION_ACTION_SCRIPT.new()
	action.action_id = &"multishot_position_probe"
	var action_skill_ids: Array[StringName] = [&"archer_multishot"]
	action.skill_ids = action_skill_ids
	action.target_selector = &"lowest_hp_enemy"
	action.desired_min_distance = 3
	action.desired_max_distance = 5
	action.distance_reference = &"target_unit"
	action.candidate_pool_limit = 6
	action.candidate_group_limit = 12
	var action_decision = action.decide(ai_context)
	_assert_true(action_decision != null and action_decision.command != null, "multi-unit positioning action 应能产出移动指令。")
	_assert_eq(
		action_decision.command.command_type if action_decision != null and action_decision.command != null else &"",
		BattleCommand.TYPE_MOVE(),
		"multi-unit positioning action 应选择普通移动。"
	)
	_assert_eq(
		action_decision.command.target_coord if action_decision != null and action_decision.command != null else Vector2i(-1, -1),
		Vector2i(3, 3),
		"multi-unit positioning action 应移动到可覆盖 5 个目标的位置。"
	)
	_assert_eq(
		action_decision.score_input.target_count if action_decision != null and action_decision.score_input != null else 0,
		5,
		"multi-unit positioning action 的评分输入应暴露移动后可覆盖 5 个目标。"
	)


func _test_ai_skill_distance_contract_uses_effective_weapon_range() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(9, 3))
	runtime._state = state
	var archer = _build_ai_unit(
		&"effective_range_skill_archer",
		"真实射程弓手",
		&"hostile",
		Vector2i(1, 1),
		&"ranged_archer",
		&"pressure",
		[&"archer_aimed_shot"],
		28,
		2
	)
	archer.current_stamina = 100
	archer.attribute_snapshot.set_value(&"stamina_max", 100)
	_apply_test_bow_weapon(archer, 6)
	var target = _build_manual_unit(&"effective_range_target", "六格目标", &"player", Vector2i(7, 1), [&"warrior_heavy_strike"])
	_add_unit_to_state(runtime, state, archer, true)
	_add_unit_to_state(runtime, state, target, false)
	var ai_context = _build_ai_context(runtime, archer)
	var action = USE_UNIT_SKILL_ACTION_SCRIPT.new()
	action.action_id = &"effective_range_skill_probe"
	var action_skill_ids: Array[StringName] = [&"archer_aimed_shot"]
	action.skill_ids = action_skill_ids
	action.target_selector = &"nearest_enemy"
	action.desired_min_distance = 3
	action.desired_max_distance = 5
	action.distance_reference = &"target_unit"
	var decision = action.decide(ai_context)
	_assert_true(decision != null and decision.command != null, "unit skill action 应能用真实 6 格弓射程锁定目标。")
	_assert_eq(
		decision.score_input.desired_max_distance if decision != null and decision.score_input != null else -1,
		6,
		"unit skill action 评分距离上限应读取 BattleRangeService 的有效射程，而不是 ranged_archer.tres 的 5。"
	)
	var preview = runtime.preview_command(decision.command if decision != null else null)
	_assert_true(preview != null and preview.allowed, "真实 6 格射程生成的攻击命令必须通过 runtime preview。")


func _test_ai_move_to_range_uses_effective_weapon_range() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(10, 3))
	runtime._state = state
	var archer = _build_ai_unit(
		&"effective_range_move_archer",
		"真实射程走位弓手",
		&"hostile",
		Vector2i(1, 1),
		&"ranged_archer",
		&"pressure",
		[&"archer_aimed_shot"],
		28,
		2
	)
	archer.current_move_points = 2
	archer.current_stamina = 100
	archer.attribute_snapshot.set_value(&"stamina_max", 100)
	_apply_test_bow_weapon(archer, 6)
	var target = _build_manual_unit(&"effective_range_move_target", "七格目标", &"player", Vector2i(8, 1), [&"warrior_heavy_strike"])
	_add_unit_to_state(runtime, state, archer, true)
	_add_unit_to_state(runtime, state, target, false)
	var ai_context = _build_ai_context(runtime, archer)
	var action = MOVE_TO_RANGE_ACTION_SCRIPT.new()
	action.action_id = &"effective_range_move_probe"
	action.target_selector = &"nearest_enemy"
	action.desired_min_distance = 3
	action.desired_max_distance = 5
	var range_skill_ids: Array[StringName] = [&"archer_aimed_shot"]
	action.range_skill_ids = range_skill_ids
	var decision = action.decide(ai_context)
	_assert_true(decision != null and decision.command != null, "move_to_range 应能基于真实射程产出移动指令。")
	_assert_eq(
		decision.score_input.desired_max_distance if decision != null and decision.score_input != null else -1,
		6,
		"move_to_range 的距离带上限应读取当前弓有效射程。"
	)
	_assert_eq(
		decision.command.target_coord if decision != null and decision.command != null else Vector2i(-1, -1),
		Vector2i(2, 1),
		"真实射程为 6 时，AI 应只前进到 6 格攻击带边缘，而不是按硬编码 5 继续多走一格。"
	)


func _test_ai_ground_cone_distance_contract_uses_outer_reach() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(10, 5))
	runtime._state = state
	var mage = _build_ai_unit(
		&"outer_reach_cone_mage",
		"外缘寒冰锥法师",
		&"hostile",
		Vector2i(1, 2),
		&"mage_controller",
		&"pressure",
		[&"mage_cone_of_cold"],
		28,
		2
	)
	mage.current_mp = 200
	mage.attribute_snapshot.set_value(&"mp_max", 200)
	mage.known_skill_level_map[&"mage_cone_of_cold"] = 7
	var target = _build_manual_unit(&"outer_reach_cone_target", "寒冰锥外缘目标", &"player", Vector2i(8, 2), [&"warrior_heavy_strike"])
	_add_unit_to_state(runtime, state, mage, true)
	_add_unit_to_state(runtime, state, target, false)
	var ai_context = _build_ai_context(runtime, mage)
	var action = USE_GROUND_SKILL_ACTION_SCRIPT.new()
	action.action_id = &"outer_reach_cone_probe"
	var skill_ids: Array[StringName] = [&"mage_cone_of_cold"]
	action.skill_ids = skill_ids
	action.minimum_hit_count = 1
	action.desired_min_distance = 1
	action.desired_max_distance = 1
	action.distance_reference = USE_GROUND_SKILL_ACTION_SCRIPT.DISTANCE_REF_TARGET_COORD()
	var decision = action.decide(ai_context)
	_assert_true(decision != null and decision.command != null, "寒冰锥应能通过外缘覆盖命中 7 格外目标。")
	_assert_eq(
		decision.score_input.desired_max_distance if decision != null and decision.score_input != null else -1,
		7,
		"地面锥形技能的 AI 距离合同应读取施法范围 + 外缘范围。"
	)
	_assert_eq(
		decision.command.target_coord if decision != null and decision.command != null else Vector2i(-1, -1),
		Vector2i(2, 2),
		"寒冰锥外缘命中时，AI 应选择施法者前方相邻格作为锥尖。"
	)
	var preview = runtime.preview_command(decision.command if decision != null else null)
	_assert_true(preview != null and preview.allowed, "寒冰锥外缘命中指令必须通过 runtime preview。")
	_assert_true(
		preview != null and preview.target_unit_ids.has(target.unit_id),
		"寒冰锥 preview 应按实际覆盖格收集外缘目标。"
	)


func _test_ai_gust_of_wind_can_hit_from_outer_reach() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(7, 5))
	runtime._state = state
	var mage = _build_ai_unit(
		&"outer_reach_gust_mage",
		"外缘强风法师",
		&"hostile",
		Vector2i(1, 2),
		&"mage_controller",
		&"pressure",
		[&"mage_gust_of_wind"],
		28,
		2
	)
	mage.current_mp = 200
	mage.attribute_snapshot.set_value(&"mp_max", 200)
	mage.known_skill_level_map[&"mage_gust_of_wind"] = 7
	var target = _build_manual_unit(&"outer_reach_gust_target", "强风外缘目标", &"player", Vector2i(5, 2), [&"warrior_heavy_strike"])
	_add_unit_to_state(runtime, state, mage, true)
	_add_unit_to_state(runtime, state, target, false)
	var ai_context = _build_ai_context(runtime, mage)
	var action = USE_GROUND_SKILL_ACTION_SCRIPT.new()
	action.action_id = &"outer_reach_gust_probe"
	var skill_ids: Array[StringName] = [&"mage_gust_of_wind"]
	action.skill_ids = skill_ids
	action.minimum_hit_count = 1
	action.desired_min_distance = 1
	action.desired_max_distance = 1
	action.distance_reference = USE_GROUND_SKILL_ACTION_SCRIPT.DISTANCE_REF_TARGET_COORD()
	var decision = action.decide(ai_context)
	_assert_true(decision != null and decision.command != null, "强风术应能通过外缘覆盖命中 4 格外目标。")
	_assert_eq(
		decision.score_input.desired_max_distance if decision != null and decision.score_input != null else -1,
		4,
		"强风术的 AI 距离合同应读取 range 1 + cone 沿风向外缘 3。"
	)
	var preview = runtime.preview_command(decision.command if decision != null else null)
	_assert_true(preview != null and preview.allowed, "强风术外缘命中指令必须通过 runtime preview。")
	_assert_true(
		preview != null and preview.target_unit_ids.has(target.unit_id),
		"强风术 preview 应按实际覆盖格收集外缘目标。"
	)


func _test_mage_controller_uses_gust_to_protect_threatened_ally() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(7, 5))
	runtime._state = state
	var mage = _build_ai_unit(
		&"protective_gust_mage",
		"护卫强风法师",
		&"hostile",
		Vector2i(1, 2),
		&"mage_controller",
		&"pressure",
		[&"mage_blink", &"mage_fireball", &"mage_chain_lightning", &"mage_cone_of_cold", &"mage_gust_of_wind"],
		28,
		1
	)
	mage.current_mp = 1000
	mage.attribute_snapshot.set_value(&"mp_max", 1000)
	for skill_id in mage.known_active_skill_ids:
		mage.known_skill_level_map[skill_id] = 7
	var archer_ally = _build_manual_unit(&"protective_gust_archer", "被贴近弓手", &"hostile", Vector2i(4, 2), [&"archer_aimed_shot"])
	var threat = _build_manual_unit(&"protective_gust_threat", "贴身威胁", &"player", Vector2i(5, 2), [&"warrior_heavy_strike"])
	_apply_test_melee_weapon(threat, 1)
	_add_unit_to_state(runtime, state, mage, true)
	_add_unit_to_state(runtime, state, archer_ally, true)
	_add_unit_to_state(runtime, state, threat, false)
	var decision = runtime._ai_service.choose_command(_build_ai_context(runtime, mage))
	_assert_eq(
		decision.action_id if decision != null else &"",
		&"mage_protective_gust",
		"mage_controller 应在单个敌人威胁友方弓手时使用保护型强风，而不是被 minimum_hit_count=2 挡住。"
	)
	_assert_eq(
		decision.command.skill_id if decision != null and decision.command != null else &"",
		&"mage_gust_of_wind",
		"保护型动作应施放 mage_gust_of_wind。"
	)
	var preview = runtime.preview_command(decision.command if decision != null else null)
	_assert_true(preview != null and preview.allowed, "保护型强风指令必须通过 runtime preview。")
	_assert_true(
		preview != null and preview.target_unit_ids.has(threat.unit_id),
		"保护型强风应命中正在威胁友军的敌人。"
	)


func _test_ranged_archer_survival_position_beats_shot_when_too_close() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(7, 5))
	runtime._state = state
	var archer = _build_ai_unit(
		&"ranged_archer_survival",
		"保命弓手",
		&"hostile",
		Vector2i(3, 2),
		&"ranged_archer",
		&"pressure",
		[&"archer_aimed_shot", &"basic_attack", &"archer_multishot"],
		28,
		2
	)
	archer.current_move_points = 2
	archer.current_stamina = 100
	archer.attribute_snapshot.set_value(&"stamina_max", 100)
	archer.apply_weapon_projection({
		"weapon_profile_kind": "equipped",
		"weapon_item_id": "ai_test_longbow",
		"weapon_profile_type_id": "longbow",
		"weapon_family": "bow",
		"weapon_current_grip": "two_handed",
		"weapon_attack_range": 6,
		"weapon_two_handed_dice": {"dice_count": 1, "dice_sides": 8, "flat_bonus": 0},
		"weapon_uses_two_hands": true,
		"weapon_physical_damage_tag": "physical_pierce",
	})
	var target = _build_manual_unit(&"survival_target", "贴身目标", &"player", Vector2i(4, 2), [&"warrior_heavy_strike"])
	_add_unit_to_state(runtime, state, archer, true)
	_add_unit_to_state(runtime, state, target, false)
	var ai_context = _build_ai_context(runtime, archer)
	var decision = runtime._ai_service.choose_command(ai_context)
	_assert_eq(
		decision.action_id if decision != null else &"",
		&"archer_survival_position",
		"ranged_archer 被敌人贴近时应优先选择保命位移，而不是原地射击。"
	)
	_assert_eq(
		decision.command.command_type if decision != null and decision.command != null else &"",
		BattleCommand.TYPE_MOVE(),
		"保命站位应产出普通移动指令。"
	)
	var resolved_distance: int = int(runtime._grid_service.get_distance(decision.command.target_coord, target.coord)) if decision != null and decision.command != null else 0
	_assert_true(resolved_distance > 1, "保命位移应拉开最近敌人的距离。")


func _test_ranged_archer_survival_position_uses_enemy_threat_range() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(9, 5))
	runtime._state = state
	var archer = _build_ai_unit(
		&"ranged_archer_dynamic_survival",
		"动态保命弓手",
		&"hostile",
		Vector2i(2, 2),
		&"ranged_archer",
		&"pressure",
		[&"archer_aimed_shot", &"basic_attack", &"archer_multishot"],
		28,
		2
	)
	archer.current_move_points = 1
	archer.current_stamina = 100
	archer.attribute_snapshot.set_value(&"stamina_max", 100)
	_apply_test_bow_weapon(archer, 6)
	var target = _build_manual_unit(&"dynamic_survival_target", "长弓威胁", &"player", Vector2i(6, 2), [&"archer_aimed_shot"])
	_apply_test_bow_weapon(target, 6)
	_add_unit_to_state(runtime, state, archer, true)
	_add_unit_to_state(runtime, state, target, false)
	var ai_context = _build_ai_context(runtime, archer)
	var decision = runtime._ai_service.choose_command(ai_context)
	_assert_eq(
		decision.action_id if decision != null else &"",
		&"archer_survival_position",
		"ranged_archer 应把敌方长弓有效射程计入保命安全距离，而不是只按固定 3 格判定 already_safe。"
	)
	_assert_eq(
		decision.command.command_type if decision != null and decision.command != null else &"",
		BattleCommand.TYPE_MOVE(),
		"动态保命站位应产出移动指令。"
	)


func _test_mage_controller_uses_blink_escape_when_unsafe() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(8, 3))
	runtime._state = state
	var mage = _build_ai_unit(
		&"mage_escape",
		"保命法师",
		&"hostile",
		Vector2i(2, 1),
		&"mage_controller",
		&"pressure",
		[&"mage_blink", &"mage_fireball", &"mage_chain_lightning", &"mage_cone_of_cold", &"mage_gust_of_wind"],
		24,
		2
	)
	mage.current_mp = 1000
	mage.attribute_snapshot.set_value(&"mp_max", 1000)
	mage.known_skill_level_map[&"mage_blink"] = 7
	var target = _build_manual_unit(&"mage_escape_target", "贴近威胁", &"player", Vector2i(4, 1), [&"warrior_heavy_strike"])
	_apply_test_melee_weapon(target, 1)
	_add_unit_to_state(runtime, state, mage, true)
	_add_unit_to_state(runtime, state, target, false)
	var current_distance: int = int(runtime._grid_service.get_distance(mage.coord, target.coord))
	var ai_context = _build_ai_context(runtime, mage)
	var decision = runtime._ai_service.choose_command(ai_context)
	_assert_eq(
		decision.action_id if decision != null else &"",
		&"mage_blink_escape",
		"mage_controller 被敌人压近时应优先使用闪现保命。"
	)
	_assert_eq(
		decision.command.skill_id if decision != null and decision.command != null else &"",
		&"mage_blink",
		"法师保命 action 应产出 mage_blink 技能指令。"
	)
	var landing_distance: int = int(runtime._grid_service.get_distance(decision.command.target_coord, target.coord)) if decision != null and decision.command != null else 0
	_assert_true(landing_distance > current_distance, "闪现落点应拉开最近威胁距离。")


func _test_mage_controller_uses_lethal_fireball_before_blink_escape() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(8, 8))
	runtime._state = state
	var mage = _build_ai_unit(
		&"mage_escape_killer",
		"杀敌保命法师",
		&"hostile",
		Vector2i(2, 4),
		&"mage_controller",
		&"pressure",
		[&"mage_blink", &"mage_fireball", &"mage_chain_lightning", &"mage_cone_of_cold", &"mage_gust_of_wind"],
		24,
		2
	)
	mage.current_mp = 1000
	mage.attribute_snapshot.set_value(&"mp_max", 1000)
	for skill_id in mage.known_active_skill_ids:
		mage.known_skill_level_map[skill_id] = 7
	var targets: Array = [
		_build_manual_unit(&"mage_escape_kill_a", "可击杀目标A", &"player", Vector2i(4, 3), [&"warrior_heavy_strike"]),
		_build_manual_unit(&"mage_escape_kill_b", "可击杀目标B", &"player", Vector2i(4, 4), [&"warrior_heavy_strike"]),
		_build_manual_unit(&"mage_escape_kill_c", "可击杀目标C", &"player", Vector2i(4, 5), [&"warrior_heavy_strike"]),
	]
	_add_unit_to_state(runtime, state, mage, true)
	for target_option in targets:
		var target = target_option as BattleUnitState
		target.current_hp = 10
		target.attribute_snapshot.set_value(&"hp_max", 30)
		_apply_test_melee_weapon(target, 1)
		_add_unit_to_state(runtime, state, target, false)
	var decision = runtime._ai_service.choose_command(_build_ai_context(runtime, mage))
	_assert_eq(
		decision.action_id if decision != null else &"",
		&"mage_fireball_cluster",
		"法师被压近但能火球多杀时，应先杀人而不是先 blink。"
	)
	_assert_eq(
		decision.command.skill_id if decision != null and decision.command != null else &"",
		&"mage_fireball",
		"多杀保命决策应产出火球术。"
	)


func _test_mage_retreat_state_still_uses_lethal_offense_when_safe() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(8, 5))
	runtime._state = state
	var mage = _build_ai_unit(
		&"mage_retreat_killer",
		"低血杀敌法师",
		&"hostile",
		Vector2i(1, 2),
		&"mage_controller",
		&"pressure",
		[&"mage_blink", &"mage_fireball", &"mage_chain_lightning", &"mage_gust_of_wind"],
		28,
		1
	)
	mage.current_hp = 10
	mage.attribute_snapshot.set_value(&"hp_max", 40)
	mage.current_mp = 1000
	mage.attribute_snapshot.set_value(&"mp_max", 1000)
	for skill_id in mage.known_active_skill_ids:
		mage.known_skill_level_map[skill_id] = 7
	var target_a = _build_manual_unit(&"mage_retreat_kill_a", "低血威胁A", &"player", Vector2i(5, 2), [&"archer_aimed_shot"])
	var target_b = _build_manual_unit(&"mage_retreat_kill_b", "低血威胁B", &"player", Vector2i(5, 3), [&"archer_aimed_shot"])
	for target in [target_a, target_b]:
		target.current_hp = 10
		target.attribute_snapshot.set_value(&"hp_max", 30)
		_apply_test_bow_weapon(target, 5)
	_add_unit_to_state(runtime, state, mage, true)
	_add_unit_to_state(runtime, state, target_a, false)
	_add_unit_to_state(runtime, state, target_b, false)
	var decision = runtime._ai_service.choose_command(_build_ai_context(runtime, mage))
	_assert_true(decision != null and decision.command != null, "低血但安全的法师应能产生攻击决策。")
	if decision == null or decision.command == null:
		return
	_assert_true(
		decision.command.skill_id == &"mage_fireball" or decision.command.skill_id == &"mage_chain_lightning",
		"低血但不危急时，法师 retreat state 应继续使用可击杀输出，而不是只逃跑。"
	)


func _test_retreat_action_uses_enemy_threat_range_progress() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(8, 3))
	runtime._state = state
	var archer = _build_ai_unit(
		&"retreat_dynamic_archer",
		"动态撤退弓手",
		&"hostile",
		Vector2i(2, 1),
		&"ranged_archer",
		&"retreat",
		[&"archer_aimed_shot", &"basic_attack"],
		12,
		2
	)
	archer.current_move_points = 1
	archer.current_stamina = 100
	archer.attribute_snapshot.set_value(&"stamina_max", 100)
	_apply_test_bow_weapon(archer, 6)
	var target = _build_manual_unit(&"retreat_dynamic_target", "长弓追击者", &"player", Vector2i(4, 1), [&"archer_aimed_shot"])
	_apply_test_bow_weapon(target, 6)
	_add_unit_to_state(runtime, state, archer, true)
	_add_unit_to_state(runtime, state, target, false)
	var ai_context = _build_ai_context(runtime, archer)
	var action = RETREAT_ACTION_SCRIPT.new()
	action.action_id = &"retreat_dynamic_probe"
	action.score_bucket_id = &"archer_survival"
	action.minimum_safe_distance = 2
	action.use_dynamic_threat_safe_distance = true
	action.safe_distance_margin = 1
	var decision = action.decide(ai_context)
	_assert_true(decision != null and decision.command != null, "retreat 应在未达到动态安全线时仍能按安全缺口改善产出移动。")
	_assert_eq(
		decision.command.target_coord if decision != null and decision.command != null else Vector2i(-1, -1),
		Vector2i(1, 1),
		"retreat 应选择远离敌方长弓威胁的一步。"
	)
	_assert_eq(
		decision.score_input.desired_min_distance if decision != null and decision.score_input != null else -1,
		7,
		"retreat 安全距离应读取敌方有效射程 6 并叠加 1 格安全边距。"
	)
	_assert_eq(
		decision.score_input.position_objective_kind if decision != null and decision.score_input != null else &"",
		&"distance_band_progress",
		"retreat 应使用安全缺口改善评分，避免一步撤退因未达到安全线被 distance_floor 压成负收益。"
	)

	var fixed_action = RETREAT_ACTION_SCRIPT.new()
	fixed_action.action_id = &"retreat_fixed_probe"
	fixed_action.score_bucket_id = &"frontline_survival"
	fixed_action.minimum_safe_distance = 4
	var fixed_decision = fixed_action.decide(ai_context)
	_assert_eq(
		fixed_decision.score_input.desired_min_distance if fixed_decision != null and fixed_decision.score_input != null else -1,
		4,
		"retreat 默认应使用配置的固定安全距离，避免 melee_aggressor 低血时按敌方长弓射程后撤并拆掉前排。"
	)


func _test_ranged_archer_prefers_high_ground_position_before_shot() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(7, 5))
	runtime._state = state
	var high_cell = state.cells.get(Vector2i(2, 2))
	high_cell.base_height = 5
	high_cell.height_offset = 0
	high_cell.recalculate_runtime_values()
	state.cell_columns = BATTLE_CELL_STATE_SCRIPT.build_columns_from_surface_cells(state.cells)
	var archer = _build_ai_unit(
		&"ranged_archer_high_ground",
		"高地弓手",
		&"hostile",
		Vector2i(1, 2),
		&"ranged_archer",
		&"pressure",
		[&"archer_aimed_shot", &"basic_attack", &"archer_multishot"],
		28,
		2
	)
	archer.current_move_points = 1
	archer.current_stamina = 100
	archer.attribute_snapshot.set_value(&"stamina_max", 100)
	archer.apply_weapon_projection({
		"weapon_profile_kind": "equipped",
		"weapon_item_id": "ai_test_longbow",
		"weapon_profile_type_id": "longbow",
		"weapon_family": "bow",
		"weapon_current_grip": "two_handed",
		"weapon_attack_range": 6,
		"weapon_two_handed_dice": {"dice_count": 1, "dice_sides": 8, "flat_bonus": 0},
		"weapon_uses_two_hands": true,
		"weapon_physical_damage_tag": "physical_pierce",
	})
	var target = _build_manual_unit(&"high_ground_target", "远处目标", &"player", Vector2i(5, 2), [&"warrior_heavy_strike"])
	_add_unit_to_state(runtime, state, archer, true)
	_add_unit_to_state(runtime, state, target, false)
	var ai_context = _build_ai_context(runtime, archer)
	var decision = runtime._ai_service.choose_command(ai_context)
	_assert_eq(
		decision.action_id if decision != null else &"",
		&"archer_high_ground_position",
		"ranged_archer 有安全高地可用时应先抢高位。"
	)
	_assert_eq(
		decision.command.target_coord if decision != null and decision.command != null else Vector2i(-1, -1),
		Vector2i(2, 2),
		"高地站位应移动到可射击且高度更高的位置。"
	)


func _test_ranged_controller_prefers_later_higher_score_skill_action() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var source_brain = runtime._enemy_ai_brains.get(&"ranged_controller")
	var brain = source_brain.duplicate(true) if source_brain != null else null
	if brain != null:
		runtime._enemy_ai_brains[brain.brain_id] = brain
		runtime._ai_service.setup(runtime._enemy_ai_brains, null)
	var pressure_state = brain.get_state(&"pressure") if brain != null else null
	_assert_true(pressure_state != null, "ranged_controller 应暴露 pressure 状态供评分回归覆盖。")
	if pressure_state == null:
		return
	var lower_score_action = USE_GROUND_SKILL_ACTION_SCRIPT.new()
	lower_score_action.action_id = &"mist_pressure_fireball"
	var lower_score_ground_skill_ids: Array[StringName] = [&"mage_fireball"]
	lower_score_action.skill_ids = lower_score_ground_skill_ids
	lower_score_action.minimum_hit_count = 1
	lower_score_action.desired_min_distance = 3
	lower_score_action.desired_max_distance = 4
	lower_score_action.distance_reference = &"target_coord"
	lower_score_action.score_bucket_id = &"mist_pressure_offense"
	var higher_score_action = USE_UNIT_SKILL_ACTION_SCRIPT.new()
	higher_score_action.action_id = &"mist_pressure_ice_lance"
	var higher_score_unit_skill_ids: Array[StringName] = [&"mage_ice_lance"]
	higher_score_action.skill_ids = higher_score_unit_skill_ids
	higher_score_action.target_selector = &"lowest_hp_enemy"
	higher_score_action.desired_min_distance = 3
	higher_score_action.desired_max_distance = 4
	higher_score_action.distance_reference = &"target_unit"
	higher_score_action.score_bucket_id = &"mist_pressure_offense"
	pressure_state.actions = [lower_score_action, higher_score_action]

	var state = _build_flat_state(Vector2i(7, 5))
	runtime._state = state
	var mist = _build_ai_unit(
		&"mist_score_caster",
		"雾沼评分术士",
		&"hostile",
		Vector2i(1, 2),
		&"ranged_controller",
		&"pressure",
		[&"mage_fireball", &"mage_ice_lance"],
		24,
		2
	)
	var player = _build_manual_unit(&"single_target", "单体目标", &"player", Vector2i(4, 2), [&"warrior_heavy_strike"])
	_add_unit_to_state(runtime, state, mist, true)
	_add_unit_to_state(runtime, state, player, false)
	var ai_context = _build_ai_context(runtime, mist)

	var decision = runtime._ai_service.choose_command(ai_context)
	_assert_true(decision != null and decision.state_id == &"pressure", "ranged_controller 评分选技回归应保持 pressure 状态。")
	_assert_eq(
		decision.command.skill_id if decision != null and decision.command != null else &"",
		&"mage_ice_lance",
		"ranged_controller 不应再只按 action 顺序优先选到先声明的 mage_fireball。"
	)
	_assert_eq(
		decision.action_id if decision != null else &"",
		&"mist_pressure_ice_lance",
		"ranged_controller 应能选中后声明但评分更高的技能 action。"
	)


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
	var player_c = _build_manual_unit(&"player_c", "玩家C", &"player", Vector2i(6, 2), [&"warrior_heavy_strike"])
	_add_unit_to_state(runtime, state, harrier, true)
	_add_unit_to_state(runtime, state, player_a, false)
	_add_unit_to_state(runtime, state, player_b, false)
	_add_unit_to_state(runtime, state, player_c, false)
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


func _test_ranged_suppressor_skips_stamina_blocked_suppressive_fire() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(7, 5))
	runtime._state = state
	var harrier = _build_ai_unit(
		&"mist_harrier_stamina",
		"雾沼猎压者",
		&"hostile",
		Vector2i(1, 2),
		&"ranged_suppressor",
		&"pressure",
		[&"archer_suppressive_fire", &"archer_pinning_shot"],
		26,
		2
	)
	harrier.current_stamina = 1
	var player_a = _build_manual_unit(&"player_a", "玩家A", &"player", Vector2i(4, 2), [&"warrior_heavy_strike"])
	var player_b = _build_manual_unit(&"player_b", "玩家B", &"player", Vector2i(5, 2), [&"warrior_heavy_strike"])
	_add_unit_to_state(runtime, state, harrier, true)
	_add_unit_to_state(runtime, state, player_a, false)
	_add_unit_to_state(runtime, state, player_b, false)
	var ai_context = _build_ai_context(runtime, harrier)
	var decision = runtime._ai_service.choose_command(ai_context)
	_assert_true(decision != null and decision.command != null, "体力不足时 ranged_suppressor 仍应生成可执行的替代动作。")
	_assert_eq(
		decision.command.skill_id if decision != null and decision.command != null else &"",
		&"archer_pinning_shot",
		"体力不足时 ranged_suppressor 不应继续选择 archer_suppressive_fire。"
	)
	var preview = runtime.preview_command(decision.command)
	_assert_true(preview != null and preview.allowed, "体力阻断后的 AI 替代命令仍必须通过 preview_command。")


func _test_ranged_suppressor_skips_cooldown_blocked_suppressive_fire() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(7, 5))
	runtime._state = state
	var harrier = _build_ai_unit(
		&"mist_harrier_cooldown",
		"雾沼猎压者",
		&"hostile",
		Vector2i(1, 2),
		&"ranged_suppressor",
		&"pressure",
		[&"archer_suppressive_fire", &"archer_pinning_shot"],
		26,
		2
	)
	harrier.cooldowns[&"archer_suppressive_fire"] = 10
	var player_a = _build_manual_unit(&"player_a", "玩家A", &"player", Vector2i(4, 2), [&"warrior_heavy_strike"])
	var player_b = _build_manual_unit(&"player_b", "玩家B", &"player", Vector2i(5, 2), [&"warrior_heavy_strike"])
	_add_unit_to_state(runtime, state, harrier, true)
	_add_unit_to_state(runtime, state, player_a, false)
	_add_unit_to_state(runtime, state, player_b, false)
	var ai_context = _build_ai_context(runtime, harrier)
	var decision = runtime._ai_service.choose_command(ai_context)
	_assert_true(decision != null and decision.command != null, "冷却未结束时 ranged_suppressor 仍应生成可执行的替代动作。")
	_assert_eq(
		decision.command.skill_id if decision != null and decision.command != null else &"",
		&"archer_pinning_shot",
		"冷却未结束时 ranged_suppressor 不应继续选择 archer_suppressive_fire。"
	)
	var preview = runtime.preview_command(decision.command)
	_assert_true(preview != null and preview.allowed, "冷却阻断后的 AI 替代命令仍必须通过 preview_command。")


func _test_ai_unit_skill_action_skips_aura_blocked_primary_skill() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var brain = ENEMY_AI_BRAIN_DEF_SCRIPT.new()
	brain.brain_id = &"aura_archer_brain"
	brain.default_state_id = &"pressure"
	var state_def = ENEMY_AI_STATE_DEF_SCRIPT.new()
	state_def.state_id = &"pressure"
	var action = USE_UNIT_SKILL_ACTION_SCRIPT.new()
	action.action_id = &"aura_primary_then_fallback"
	var action_skill_ids: Array[StringName] = [&"archer_far_horizon", &"archer_pinning_shot"]
	action.skill_ids = action_skill_ids
	action.target_selector = &"lowest_hp_enemy"
	action.desired_min_distance = 0
	action.desired_max_distance = 6
	action.distance_reference = &"target_unit"
	state_def.actions = [action]
	brain.states = [state_def]
	runtime._enemy_ai_brains[brain.brain_id] = brain
	runtime._ai_service.setup(runtime._enemy_ai_brains, null)

	var state = _build_flat_state(Vector2i(7, 5))
	runtime._state = state
	var archer = _build_ai_unit(
		&"aura_archer",
		"Aura 猎手",
		&"hostile",
		Vector2i(1, 2),
		brain.brain_id,
		&"pressure",
		[&"archer_far_horizon", &"archer_pinning_shot"],
		26,
		2
	)
	archer.current_aura = 0
	archer.attribute_snapshot.set_value(&"aura_max", 1)
	var player = _build_manual_unit(&"aura_target", "玩家", &"player", Vector2i(4, 2), [&"warrior_heavy_strike"])
	_add_unit_to_state(runtime, state, archer, true)
	_add_unit_to_state(runtime, state, player, false)
	var ai_context = _build_ai_context(runtime, archer)
	var decision = runtime._ai_service.choose_command(ai_context)
	_assert_true(decision != null and decision.command != null, "Aura 不足时 AI 仍应生成可执行的替代动作。")
	_assert_eq(
		decision.command.skill_id if decision != null and decision.command != null else &"",
		&"archer_pinning_shot",
		"Aura 不足时 AI 不应继续选择需要 Aura 的 archer_far_horizon。"
	)
	var preview = runtime.preview_command(decision.command)
	_assert_true(preview != null and preview.allowed, "Aura 阻断后的 AI 替代命令仍必须通过 preview_command。")


func _test_ai_unit_skill_action_selects_scoring_variant_id() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var skill_id := &"ai_test_unit_variant_bolt"
	runtime._skill_defs[skill_id] = _build_test_unit_variant_skill(skill_id)
	var state = _build_flat_state(Vector2i(7, 5))
	runtime._state = state
	var caster = _build_ai_unit(
		&"option_caster",
		"形态施法者",
		&"hostile",
		Vector2i(1, 2),
		&"ranged_suppressor",
		&"pressure",
		[skill_id],
		26,
		2
	)
	var target = _build_manual_unit(&"option_target", "形态目标", &"player", Vector2i(3, 2), [&"basic_attack"])
	_add_unit_to_state(runtime, state, caster, true)
	_add_unit_to_state(runtime, state, target, false)
	var action = _build_test_unit_variant_action(skill_id)
	var decision = action.decide(_build_ai_context(runtime, caster))
	_assert_true(decision != null and decision.command != null, "unit option action 应产出合法技能指令。")
	if decision == null or decision.command == null:
		return
	_assert_eq(decision.command.skill_variant_id, &"strong_bolt", "AI 应选择评分最高的 unit option 并写入 command。")
	_assert_true(decision.score_input != null, "unit option action 应使用带 option effect 的评分上下文。")
	if decision.score_input != null:
		_assert_true(int(decision.score_input.estimated_damage) >= 30, "评分应消费所选 strong_bolt option 的伤害效果。")
	var preview = runtime.preview_command(decision.command)
	_assert_true(preview != null and preview.allowed, "AI 产出的 explicit unit option 命令必须通过 runtime preview。")


func _test_ai_unit_skill_action_ignores_locked_and_ground_options() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var skill_id := &"ai_test_unit_variant_filter"
	runtime._skill_defs[skill_id] = _build_test_unit_variant_skill(skill_id, 5, true)
	var state = _build_flat_state(Vector2i(7, 5))
	runtime._state = state
	var caster = _build_ai_unit(
		&"option_filter_caster",
		"形态过滤者",
		&"hostile",
		Vector2i(1, 2),
		&"ranged_suppressor",
		&"pressure",
		[skill_id],
		26,
		2
	)
	var target = _build_manual_unit(&"option_filter_target", "形态过滤目标", &"player", Vector2i(3, 2), [&"basic_attack"])
	_add_unit_to_state(runtime, state, caster, true)
	_add_unit_to_state(runtime, state, target, false)
	var action = _build_test_unit_variant_action(skill_id)
	var decision = action.decide(_build_ai_context(runtime, caster))
	_assert_true(decision != null and decision.command != null, "unit option filter action 应产出合法技能指令。")
	if decision == null or decision.command == null:
		return
	_assert_eq(decision.command.skill_variant_id, &"weak_touch", "AI 应忽略未解锁 unit option 和 ground option。")
	_assert_true(decision.score_input != null, "过滤后仍应构造评分上下文。")
	if decision.score_input != null:
		_assert_eq(int(decision.score_input.estimated_damage), 4, "过滤后评分只能来自已解锁 weak_touch unit option。")
	var preview = runtime.preview_command(decision.command)
	_assert_true(preview != null and preview.allowed, "过滤后的 unit option 命令必须通过 runtime preview。")


func _test_ai_unit_skill_action_preserves_empty_variant_for_base_skill() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var skill_id := &"ai_test_plain_unit_skill"
	runtime._skill_defs[skill_id] = _build_test_plain_unit_skill(skill_id)
	var state = _build_flat_state(Vector2i(7, 5))
	runtime._state = state
	var caster = _build_ai_unit(
		&"plain_skill_caster",
		"普通施法者",
		&"hostile",
		Vector2i(1, 2),
		&"ranged_suppressor",
		&"pressure",
		[skill_id],
		26,
		2
	)
	var target = _build_manual_unit(&"plain_skill_target", "普通目标", &"player", Vector2i(3, 2), [&"basic_attack"])
	_add_unit_to_state(runtime, state, caster, true)
	_add_unit_to_state(runtime, state, target, false)
	var action = _build_test_unit_variant_action(skill_id)
	var decision = action.decide(_build_ai_context(runtime, caster))
	_assert_true(decision != null and decision.command != null, "无 option 的 unit skill 应继续产出合法指令。")
	if decision == null or decision.command == null:
		return
	_assert_eq(decision.command.skill_variant_id, &"", "无 cast_variants 的 unit skill 应保持空 skill_variant_id。")
	var preview = runtime.preview_command(decision.command)
	_assert_true(preview != null and preview.allowed, "无 option 的旧 unit skill command 必须保持可 preview。")


func _test_runtime_rejects_invalid_unit_skill_variant_ids() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var skill_id := &"ai_test_unit_variant_runtime"
	var locked_skill_id := &"ai_test_unit_variant_locked_runtime"
	runtime._skill_defs[skill_id] = _build_test_unit_variant_skill(skill_id, 0, true)
	runtime._skill_defs[locked_skill_id] = _build_test_unit_variant_skill(locked_skill_id, 5, true)
	var state = _build_flat_state(Vector2i(7, 5))
	state.phase = &"unit_acting"
	runtime._state = state
	var caster = _build_ai_unit(
		&"runtime_variant_caster",
		"运行时形态施法者",
		&"hostile",
		Vector2i(1, 2),
		&"ranged_suppressor",
		&"pressure",
		[skill_id, locked_skill_id],
		26,
		2
	)
	var target = _build_manual_unit(&"runtime_variant_target", "运行时形态目标", &"player", Vector2i(3, 2), [&"basic_attack"])
	_add_unit_to_state(runtime, state, caster, true)
	_add_unit_to_state(runtime, state, target, false)
	state.active_unit_id = caster.unit_id

	var ambiguous_command = _build_test_unit_skill_command(caster, skill_id, target)
	var invalid_command = _build_test_unit_skill_command(caster, skill_id, target, &"missing_option")
	var ground_command = _build_test_unit_skill_command(caster, skill_id, target, &"ground_burst")
	var locked_command = _build_test_unit_skill_command(caster, locked_skill_id, target, &"strong_bolt")
	for command in [ambiguous_command, invalid_command, ground_command, locked_command]:
		var preview = runtime.preview_command(command)
		_assert_true(preview != null and not preview.allowed, "非法、歧义、未解锁或 target_mode 不匹配的 unit option command 必须被 preview 拒绝。")

	var before_ap: int = int(caster.current_ap)
	var before_hp: int = int(target.current_hp)
	var batch = runtime.issue_command(invalid_command)
	_assert_eq(caster.current_ap, before_ap, "runtime 拒绝 invalid explicit option 时不应消耗 AP。")
	_assert_eq(target.current_hp, before_hp, "runtime 拒绝 invalid explicit option 时不应结算目标效果。")
	_assert_true(batch != null and not batch.log_lines.is_empty(), "runtime 拒绝 invalid explicit option 时应返回阻断日志。")


func _test_move_to_range_prefers_closing_distance_over_wait_when_far_from_band() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var brain = ENEMY_AI_BRAIN_DEF_SCRIPT.new()
	brain.brain_id = &"far_gap_mover_brain"
	brain.default_state_id = &"engage"
	var engage_state = ENEMY_AI_STATE_DEF_SCRIPT.new()
	engage_state.state_id = &"engage"
	var move_action = MOVE_TO_RANGE_ACTION_SCRIPT.new()
	move_action.action_id = &"far_gap_close_in"
	move_action.target_selector = &"nearest_enemy"
	move_action.desired_min_distance = 4
	move_action.desired_max_distance = 5
	var wait_action = WAIT_ACTION_SCRIPT.new()
	wait_action.action_id = &"far_gap_wait"
	engage_state.actions = [move_action, wait_action]
	brain.states = [engage_state]
	runtime._enemy_ai_brains[brain.brain_id] = brain
	runtime._ai_service.setup(runtime._enemy_ai_brains, null)

	var state = _build_flat_state(Vector2i(31, 3))
	runtime._state = state
	var mover = _build_ai_unit(
		&"far_gap_enemy",
		"远距接敌者",
		&"hostile",
		Vector2i(1, 1),
		brain.brain_id,
		&"engage",
		[],
		26,
		2
	)
	var player = _build_manual_unit(&"far_gap_player", "远距目标", &"player", Vector2i(28, 1), [&"warrior_heavy_strike"])
	_add_unit_to_state(runtime, state, mover, true)
	_add_unit_to_state(runtime, state, player, false)
	var ai_context = _build_ai_context(runtime, mover)
	var decision = runtime._ai_service.choose_command(ai_context)
	_assert_true(decision != null and decision.command != null, "远距离 move_to_range 回归应产出合法指令。")
	_assert_eq(
		decision.command.command_type if decision != null and decision.command != null else &"",
		BattleCommand.TYPE_MOVE(),
		"当远远超出目标距离带时，AI 不应继续待机。"
	)
	_assert_eq(
		decision.command.target_coord if decision != null and decision.command != null else Vector2i(-1, -1),
		Vector2i(3, 1),
		"远距离 move_to_range 回归应优先选择本回合可达的最大有效逼近落点。"
	)


func _test_taunt_forces_nearest_enemy_selector_to_source_unit() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var brain = ENEMY_AI_BRAIN_DEF_SCRIPT.new()
	brain.brain_id = &"taunt_nearest_enemy_brain"
	brain.default_state_id = &"pressure"
	var pressure_state = ENEMY_AI_STATE_DEF_SCRIPT.new()
	pressure_state.state_id = &"pressure"
	var action = USE_UNIT_SKILL_ACTION_SCRIPT.new()
	action.action_id = &"taunt_force_nearest_enemy"
	var action_skill_ids: Array[StringName] = [&"archer_pinning_shot"]
	action.skill_ids = action_skill_ids
	action.target_selector = &"nearest_enemy"
	action.desired_min_distance = 0
	action.desired_max_distance = 6
	action.distance_reference = &"target_unit"
	pressure_state.actions = [action]
	brain.states = [pressure_state]
	runtime._enemy_ai_brains[brain.brain_id] = brain

	var state = _build_flat_state(Vector2i(7, 5))
	runtime._state = state
	var archer = _build_ai_unit(
		&"taunted_archer_nearest",
		"被嘲讽猎手",
		&"hostile",
		Vector2i(1, 2),
		brain.brain_id,
		&"pressure",
		[&"archer_pinning_shot"],
		26,
		2
	)
	_set_test_status(archer, &"taunted", &"taunt_source_far", 90)
	var taunt_source = _build_manual_unit(&"taunt_source_far", "远处嘲讽源", &"player", Vector2i(5, 2), [&"warrior_heavy_strike"])
	var closer_target = _build_manual_unit(&"closer_target", "近处诱饵", &"player", Vector2i(2, 2), [&"warrior_heavy_strike"])
	_add_unit_to_state(runtime, state, archer, true)
	_add_unit_to_state(runtime, state, taunt_source, false)
	_add_unit_to_state(runtime, state, closer_target, false)
	var ai_context = _build_ai_context(runtime, archer)
	var decision = runtime._ai_service.choose_command(ai_context)
	_assert_true(decision != null and decision.command != null, "nearest_enemy 选择器在 taunt 场景下应仍能产出合法 AI 指令。")
	_assert_eq(
		decision.command.skill_id if decision != null and decision.command != null else &"",
		&"archer_pinning_shot",
		"nearest_enemy taunt 回归应继续走正式技能施放路径，而不是回退到待机。"
	)
	_assert_eq(
		decision.command.target_unit_id if decision != null and decision.command != null else &"",
		taunt_source.unit_id,
		"被 taunted 时，nearest_enemy 不应继续命中更近的其它目标。"
	)


func _test_taunt_forces_lowest_hp_enemy_selector_to_source_unit() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var brain = ENEMY_AI_BRAIN_DEF_SCRIPT.new()
	brain.brain_id = &"taunt_lowest_hp_brain"
	brain.default_state_id = &"pressure"
	var pressure_state = ENEMY_AI_STATE_DEF_SCRIPT.new()
	pressure_state.state_id = &"pressure"
	var action = USE_UNIT_SKILL_ACTION_SCRIPT.new()
	action.action_id = &"taunt_force_lowest_hp_enemy"
	var action_skill_ids: Array[StringName] = [&"archer_pinning_shot"]
	action.skill_ids = action_skill_ids
	action.target_selector = &"lowest_hp_enemy"
	action.desired_min_distance = 0
	action.desired_max_distance = 6
	action.distance_reference = &"target_unit"
	pressure_state.actions = [action]
	brain.states = [pressure_state]
	runtime._enemy_ai_brains[brain.brain_id] = brain

	var state = _build_flat_state(Vector2i(7, 5))
	runtime._state = state
	var archer = _build_ai_unit(
		&"taunted_archer_low_hp",
		"被嘲讽评分手",
		&"hostile",
		Vector2i(1, 2),
		brain.brain_id,
		&"pressure",
		[&"archer_pinning_shot"],
		26,
		2
	)
	_set_test_status(archer, &"taunted", &"taunt_source_healthy", 90)
	var taunt_source = _build_manual_unit(&"taunt_source_healthy", "健康嘲讽源", &"player", Vector2i(5, 2), [&"warrior_heavy_strike"])
	var lowest_hp_target = _build_manual_unit(&"lowest_hp_target", "残血诱饵", &"player", Vector2i(2, 2), [&"warrior_heavy_strike"])
	lowest_hp_target.current_hp = 4
	_add_unit_to_state(runtime, state, archer, true)
	_add_unit_to_state(runtime, state, taunt_source, false)
	_add_unit_to_state(runtime, state, lowest_hp_target, false)
	var ai_context = _build_ai_context(runtime, archer)
	var decision = runtime._ai_service.choose_command(ai_context)
	_assert_true(decision != null and decision.command != null, "lowest_hp_enemy 选择器在 taunt 场景下应仍能产出合法 AI 指令。")
	_assert_eq(
		decision.command.skill_id if decision != null and decision.command != null else &"",
		&"archer_pinning_shot",
		"lowest_hp_enemy taunt 回归应继续走正式技能施放路径，而不是回退到待机。"
	)
	_assert_eq(
		decision.command.target_unit_id if decision != null and decision.command != null else &"",
		taunt_source.unit_id,
		"被 taunted 时，lowest_hp_enemy 不应继续命中更低血量的其它目标。"
	)


func _test_taunt_forces_role_threat_enemy_selector_to_source_unit() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(8, 5))
	runtime._state = state
	var wolf = _build_ai_unit(
		&"taunted_role_selector_wolf",
		"被嘲讽威胁狼",
		&"hostile",
		Vector2i(1, 2),
		&"melee_aggressor",
		&"engage",
		[&"basic_attack"],
		30,
		2
	)
	_set_test_status(wolf, &"taunted", &"taunt_source_role_selector", 90)
	var taunt_source = _build_manual_unit(&"taunt_source_role_selector", "嘲讽源", &"player", Vector2i(6, 2), [&"warrior_heavy_strike"])
	var ranged_target = _build_manual_unit(&"closer_role_threat_target", "近处远程威胁", &"player", Vector2i(3, 2), [&"archer_aimed_shot", &"basic_attack"])
	_apply_test_bow_weapon(ranged_target, 6)
	_add_unit_to_state(runtime, state, wolf, true)
	_add_unit_to_state(runtime, state, taunt_source, false)
	_add_unit_to_state(runtime, state, ranged_target, false)
	var ai_context = _build_ai_context(runtime, wolf)
	var action = MOVE_TO_RANGE_ACTION_SCRIPT.new()
	var targets = action._sort_target_units(ai_context, &"enemy", &"nearest_role_threat_enemy")
	_assert_true(not targets.is_empty(), "nearest_role_threat_enemy taunt 回归应返回强制目标。")
	_assert_eq(
		targets[0].unit_id if not targets.is_empty() else &"",
		taunt_source.unit_id,
		"被 taunted 时，nearest_role_threat_enemy 不应继续优先选择更近的远程威胁。"
	)


func _test_taunt_disadvantage_ignores_stale_dead_or_non_hostile_source() -> void:
	var state = _build_flat_state(Vector2i(7, 3))
	var attacker = _build_ai_unit(
		&"taunted_attacker",
		"被嘲讽攻击者",
		&"hostile",
		Vector2i(1, 1),
		&"melee_aggressor",
		&"pressure",
		[&"basic_attack"],
		30,
		2
	)
	var taunt_source = _build_manual_unit(&"valid_taunt_source", "有效嘲讽源", &"player", Vector2i(4, 1), [&"basic_attack"])
	var other_target = _build_manual_unit(&"other_target_for_disadvantage", "其它目标", &"player", Vector2i(5, 1), [&"basic_attack"])
	state.units[attacker.unit_id] = attacker
	state.units[taunt_source.unit_id] = taunt_source
	state.units[other_target.unit_id] = other_target
	_set_test_status(attacker, &"taunted", taunt_source.unit_id, 90)
	_assert_true(
		state.is_attack_disadvantage(attacker, other_target),
		"taunted 攻击非嘲讽源目标时应进入 disadvantage。"
	)
	_assert_true(
		not state.is_attack_disadvantage(attacker, taunt_source),
		"taunted 攻击仍存活且敌对的嘲讽源时不应吃 taunt disadvantage。"
	)
	taunt_source.is_alive = false
	_assert_true(
		not state.is_attack_disadvantage(attacker, other_target),
		"taunt source 死亡后不应继续给攻击者施加 disadvantage。"
	)
	taunt_source.is_alive = true
	taunt_source.faction_id = attacker.faction_id
	_assert_true(
		not state.is_attack_disadvantage(attacker, other_target),
		"taunt source 已非敌对阵营时不应继续给攻击者施加 disadvantage。"
	)
	_set_test_status(attacker, &"taunted", &"missing_taunt_source", 90)
	_assert_true(
		not state.is_attack_disadvantage(attacker, other_target),
		"taunt source 缺失时不应留下 stale disadvantage。"
	)


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
	vanguard.current_stamina = 50
	vanguard.attribute_snapshot.set_value(&"stamina_max", 50)
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
	BattleRuntimeTestHelpers.configure_fixed_combat(runtime)
	game_session.free()
	return runtime


func _block_non_basic_skills(unit_state) -> void:
	if unit_state == null:
		return
	for skill_id in unit_state.known_active_skill_ids:
		if skill_id == &"basic_attack":
			continue
		unit_state.cooldowns[skill_id] = 30


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
			cell.base_terrain = BATTLE_CELL_STATE_SCRIPT.TERRAIN_LAND()
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
	ai_context.runtime_action_plan = _resolve_runtime_action_plan(runtime, unit_state)
	if runtime != null and runtime.has_method("_bind_ai_helper_services_for_decision"):
		runtime._bind_ai_helper_services_for_decision(unit_state, ai_context)
	return ai_context


func _resolve_runtime_action_plan(runtime, unit_state):
	if runtime == null or unit_state == null:
		return null
	var existing_plan = runtime._ai_action_plans_by_unit_id.get(unit_state.unit_id, null)
	if existing_plan != null:
		return existing_plan
	var brain = runtime._enemy_ai_brains.get(unit_state.ai_brain_id)
	if brain == null or runtime._ai_action_assembler == null:
		return null
	var action_plan = runtime._ai_action_assembler.build_unit_action_plan(unit_state, brain, runtime._skill_defs)
	runtime._ai_action_plans_by_unit_id[unit_state.unit_id] = action_plan
	return action_plan


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
	unit.current_mp = 120
	unit.unlock_combat_resource(BATTLE_UNIT_STATE_SCRIPT.COMBAT_RESOURCE_MP())
	unit.current_stamina = 8
	unit.current_ap = current_ap
	unit.is_alive = true
	unit.set_anchor_coord(coord)
	unit.attribute_snapshot.set_value(&"hp_max", maxi(current_hp, 24))
	unit.attribute_snapshot.set_value(&"mp_max", 120)
	unit.attribute_snapshot.set_value(&"stamina_max", 8)
	unit.attribute_snapshot.set_value(&"action_points", maxi(current_ap, 2))
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS_ID(), 12)
	BattleRuntimeTestHelpers.seed_base_attributes_and_derive_ac(unit)
	unit.known_active_skill_ids = skill_ids.duplicate()
	for skill_id in unit.known_active_skill_ids:
		unit.known_skill_level_map[skill_id] = 3 if String(skill_id).begins_with("mage_") else 1
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
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS_ID(), 6)
	BattleRuntimeTestHelpers.seed_base_attributes_and_derive_ac(unit)
	unit.known_active_skill_ids = skill_ids.duplicate()
	for skill_id in unit.known_active_skill_ids:
		unit.known_skill_level_map[skill_id] = 3 if String(skill_id).begins_with("mage_") else 1
	return unit


func _build_test_unit_variant_action(skill_id: StringName):
	var action = USE_UNIT_SKILL_ACTION_SCRIPT.new()
	action.action_id = StringName("%s_action" % String(skill_id))
	var action_skill_ids: Array[StringName] = [skill_id]
	action.skill_ids = action_skill_ids
	action.target_selector = &"nearest_enemy"
	action.desired_min_distance = 0
	action.desired_max_distance = 6
	action.distance_reference = &"target_unit"
	return action


func _build_test_plain_unit_skill(skill_id: StringName):
	var skill_def := SKILL_DEF_SCRIPT.new()
	skill_def.skill_id = skill_id
	skill_def.display_name = "测试普通单体"
	skill_def.skill_type = &"active"
	skill_def.combat_profile = COMBAT_SKILL_DEF_SCRIPT.new()
	skill_def.combat_profile.skill_id = skill_id
	skill_def.combat_profile.target_mode = &"unit"
	skill_def.combat_profile.target_team_filter = &"enemy"
	skill_def.combat_profile.target_selection_mode = &"single_unit"
	skill_def.combat_profile.range_pattern = &"single"
	skill_def.combat_profile.range_value = 5
	skill_def.combat_profile.ap_cost = 1
	skill_def.combat_profile.effect_defs = [_build_test_damage_effect(8)]
	return skill_def


func _build_test_unit_variant_skill(
	skill_id: StringName,
	strong_min_skill_level: int = 0,
	include_ground_option: bool = false
):
	var skill_def := SKILL_DEF_SCRIPT.new()
	skill_def.skill_id = skill_id
	skill_def.display_name = "测试形态单体"
	skill_def.skill_type = &"active"
	skill_def.combat_profile = COMBAT_SKILL_DEF_SCRIPT.new()
	skill_def.combat_profile.skill_id = skill_id
	skill_def.combat_profile.target_mode = &"unit"
	skill_def.combat_profile.target_team_filter = &"enemy"
	skill_def.combat_profile.target_selection_mode = &"single_unit"
	skill_def.combat_profile.range_pattern = &"single"
	skill_def.combat_profile.range_value = 5
	skill_def.combat_profile.ap_cost = 1
	skill_def.combat_profile.effect_defs = []

	var weak_option := COMBAT_CAST_VARIANT_DEF_SCRIPT.new()
	weak_option.variant_id = &"weak_touch"
	weak_option.display_name = "弱触"
	weak_option.target_mode = &"unit"
	weak_option.min_skill_level = 0
	weak_option.effect_defs = [_build_test_damage_effect(4)]

	var strong_option := COMBAT_CAST_VARIANT_DEF_SCRIPT.new()
	strong_option.variant_id = &"strong_bolt"
	strong_option.display_name = "强击"
	strong_option.target_mode = &"unit"
	strong_option.min_skill_level = strong_min_skill_level
	strong_option.effect_defs = [_build_test_damage_effect(30)]

	skill_def.combat_profile.cast_variants = [weak_option, strong_option]
	if include_ground_option:
		var ground_option := COMBAT_CAST_VARIANT_DEF_SCRIPT.new()
		ground_option.variant_id = &"ground_burst"
		ground_option.display_name = "地爆"
		ground_option.target_mode = &"ground"
		ground_option.min_skill_level = 0
		ground_option.footprint_pattern = &"single"
		ground_option.required_coord_count = 1
		ground_option.effect_defs = [_build_test_damage_effect(50)]
		skill_def.combat_profile.cast_variants.append(ground_option)
	return skill_def


func _build_test_damage_effect(power: int):
	var damage_effect := COMBAT_EFFECT_DEF_SCRIPT.new()
	damage_effect.effect_type = &"damage"
	damage_effect.power = power
	damage_effect.damage_tag = &"force"
	return damage_effect


func _build_test_random_chain_skill(skill_id: StringName):
	var skill_def := SKILL_DEF_SCRIPT.new()
	skill_def.skill_id = skill_id
	skill_def.display_name = "测试随机连击"
	skill_def.skill_type = &"active"
	skill_def.combat_profile = COMBAT_SKILL_DEF_SCRIPT.new()
	skill_def.combat_profile.skill_id = skill_id
	skill_def.combat_profile.target_mode = &"unit"
	skill_def.combat_profile.target_team_filter = &"enemy"
	skill_def.combat_profile.target_selection_mode = &"random_chain"
	skill_def.combat_profile.range_pattern = &"single"
	skill_def.combat_profile.range_value = 3
	skill_def.combat_profile.ap_cost = 1
	skill_def.combat_profile.max_hits_per_target = 1
	var damage_effect := COMBAT_EFFECT_DEF_SCRIPT.new()
	damage_effect.effect_type = &"damage"
	damage_effect.power = 12
	damage_effect.damage_tag = &"physical_slash"
	skill_def.combat_profile.effect_defs = [damage_effect]
	return skill_def


func _build_test_ground_control_skill(skill_id: StringName):
	var skill_def := SKILL_DEF_SCRIPT.new()
	skill_def.skill_id = skill_id
	skill_def.display_name = "测试地格控制"
	skill_def.skill_type = &"active"
	skill_def.combat_profile = COMBAT_SKILL_DEF_SCRIPT.new()
	skill_def.combat_profile.skill_id = skill_id
	skill_def.combat_profile.target_mode = &"ground"
	skill_def.combat_profile.target_team_filter = &"enemy"
	skill_def.combat_profile.range_pattern = &"single"
	skill_def.combat_profile.range_value = 5
	skill_def.combat_profile.area_pattern = &"single"
	skill_def.combat_profile.area_value = 0
	skill_def.combat_profile.ap_cost = 1
	var terrain_effect := COMBAT_EFFECT_DEF_SCRIPT.new()
	terrain_effect.effect_type = &"terrain_effect"
	terrain_effect.terrain_effect_id = &"ai_test_snare_zone"
	skill_def.combat_profile.effect_defs = [terrain_effect]
	return skill_def


func _build_test_ground_damage_skill(skill_id: StringName):
	var skill_def := SKILL_DEF_SCRIPT.new()
	skill_def.skill_id = skill_id
	skill_def.display_name = "测试空地伤害"
	skill_def.skill_type = &"active"
	skill_def.combat_profile = COMBAT_SKILL_DEF_SCRIPT.new()
	skill_def.combat_profile.skill_id = skill_id
	skill_def.combat_profile.target_mode = &"ground"
	skill_def.combat_profile.target_team_filter = &"enemy"
	skill_def.combat_profile.range_pattern = &"single"
	skill_def.combat_profile.range_value = 5
	skill_def.combat_profile.area_pattern = &"single"
	skill_def.combat_profile.area_value = 0
	skill_def.combat_profile.ap_cost = 1
	var damage_effect := COMBAT_EFFECT_DEF_SCRIPT.new()
	damage_effect.effect_type = &"damage"
	damage_effect.power = 12
	damage_effect.damage_tag = &"force"
	skill_def.combat_profile.effect_defs = [damage_effect]
	return skill_def


func _build_test_multi_unit_skill_command(source_unit, skill_id: StringName, skill_variant_id: StringName, target_units: Array):
	var command = BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL()
	command.unit_id = source_unit.unit_id if source_unit != null else &""
	command.skill_id = skill_id
	command.skill_variant_id = skill_variant_id
	for target_unit in target_units:
		if target_unit == null:
			continue
		command.target_unit_ids.append(target_unit.unit_id)
		if command.target_coord == Vector2i(-1, -1):
			command.target_coord = target_unit.coord
	return command


func _build_test_ground_skill_command(source_unit, skill_id: StringName, target_coord: Vector2i):
	var command = BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL()
	command.unit_id = source_unit.unit_id if source_unit != null else &""
	command.skill_id = skill_id
	command.target_coord = target_coord
	var target_coords: Array[Vector2i] = [target_coord]
	command.target_coords = target_coords
	return command


func _build_test_unit_skill_command(source_unit, skill_id: StringName, target_unit, skill_variant_id: StringName = &""):
	var command = BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL()
	command.unit_id = source_unit.unit_id if source_unit != null else &""
	command.skill_id = skill_id
	command.skill_variant_id = skill_variant_id
	if target_unit != null:
		command.target_unit_id = target_unit.unit_id
		command.target_coord = target_unit.coord
	return command


func _apply_test_bow_weapon(unit, attack_range: int) -> void:
	if unit == null:
		return
	unit.apply_weapon_projection({
		"weapon_profile_kind": "equipped",
		"weapon_item_id": "ai_test_longbow",
		"weapon_profile_type_id": "longbow",
		"weapon_family": "bow",
		"weapon_current_grip": "two_handed",
		"weapon_attack_range": attack_range,
		"weapon_two_handed_dice": {"dice_count": 1, "dice_sides": 8, "flat_bonus": 0},
		"weapon_uses_two_hands": true,
		"weapon_physical_damage_tag": "physical_pierce",
	})


func _apply_test_melee_weapon(unit, attack_range: int) -> void:
	if unit == null:
		return
	unit.apply_weapon_projection({
		"weapon_profile_kind": "equipped",
		"weapon_item_id": "ai_test_sword",
		"weapon_profile_type_id": "shortsword",
		"weapon_family": "sword",
		"weapon_current_grip": "one_handed",
		"weapon_attack_range": attack_range,
		"weapon_one_handed_dice": {"dice_count": 1, "dice_sides": 6, "flat_bonus": 0},
		"weapon_uses_two_hands": false,
		"weapon_physical_damage_tag": "physical_slash",
	})


func _prepare_test_whirlwind_user(unit) -> void:
	if unit == null:
		return
	unit.current_stamina = 120
	unit.current_aura = 140
	unit.unlock_combat_resource(BATTLE_UNIT_STATE_SCRIPT.COMBAT_RESOURCE_AURA())
	unit.attribute_snapshot.set_value(&"stamina_max", 120)
	unit.attribute_snapshot.set_value(&"aura_max", 140)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS_ID(), 30)
	unit.known_skill_level_map[&"warrior_whirlwind_slash"] = 9
	unit.apply_weapon_projection({
		"weapon_profile_kind": "equipped",
		"weapon_item_id": "ai_test_whirlwind_blade",
		"weapon_profile_type_id": "shortsword",
		"weapon_family": "sword",
		"weapon_current_grip": "one_handed",
		"weapon_attack_range": 1,
		"weapon_one_handed_dice": {"dice_count": 1, "dice_sides": 6, "flat_bonus": 0},
		"weapon_uses_two_hands": false,
		"weapon_physical_damage_tag": "physical_slash",
	})


func _set_test_status(unit, status_id: StringName, source_unit_id: StringName, duration_tu: int = -1, params: Dictionary = {}, power: int = 1) -> void:
	var status_entry = BATTLE_STATUS_EFFECT_STATE_SCRIPT.new()
	status_entry.status_id = status_id
	status_entry.source_unit_id = source_unit_id
	status_entry.power = maxi(power, 1)
	status_entry.stacks = 1
	status_entry.duration = duration_tu
	status_entry.params = params.duplicate(true)
	unit.set_status_effect(status_entry)


func _add_unit_to_state(runtime, state, unit, is_enemy: bool) -> void:
	BattleRuntimeTestHelpers.register_unit_in_state(state, unit, is_enemy)
	var placed = runtime._grid_service.place_unit(state, unit, unit.coord, true)
	_assert_true(placed, "测试单位 %s 应能放入测试战场。" % String(unit.unit_id))


func _errors_contain_fragment(errors: Array[String], fragment: String) -> bool:
	for error in errors:
		if String(error).contains(fragment):
			return true
	return false


func _to_string_name(value) -> StringName:
	if value == null:
		return &""
	return StringName(String(value))


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
