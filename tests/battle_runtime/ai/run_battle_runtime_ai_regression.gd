extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/persistence/game_session.gd")
const GAME_RUNTIME_FACADE_SCRIPT = preload("res://scripts/systems/game_runtime/game_runtime_facade.gd")
const BATTLE_RUNTIME_MODULE_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_runtime_module.gd")
const BATTLE_AI_CONTEXT_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_context.gd")
const BATTLE_AI_SERVICE_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_service.gd")
const BATTLE_AI_SCORE_INPUT_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_score_input.gd")
const BATTLE_AI_SCORE_SERVICE_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_score_service.gd")
const BATTLE_AI_SCORE_PROFILE_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_score_profile.gd")
const BATTLE_COMMAND_SCRIPT = preload("res://scripts/systems/battle/core/battle_command.gd")
const BATTLE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_state.gd")
const BATTLE_TIMELINE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_timeline_state.gd")
const BATTLE_CELL_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const BATTLE_STATUS_EFFECT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_status_effect_state.gd")
const COMBAT_EFFECT_DEF_SCRIPT = preload("res://scripts/player/progression/combat_effect_def.gd")
const COMBAT_SKILL_DEF_SCRIPT = preload("res://scripts/player/progression/combat_skill_def.gd")
const COMBAT_CAST_VARIANT_DEF_SCRIPT = preload("res://scripts/player/progression/combat_cast_variant_def.gd")
const ENCOUNTER_ANCHOR_DATA_SCRIPT = preload("res://scripts/systems/world/encounter_anchor_data.gd")
const BATTLE_TERRAIN_GENERATOR_SCRIPT = preload("res://scripts/systems/battle/terrain/battle_terrain_generator.gd")
const ENEMY_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/enemies/enemy_content_registry.gd")
const ENEMY_AI_BRAIN_DEF_SCRIPT = preload("res://scripts/enemies/enemy_ai_brain_def.gd")
const ENEMY_AI_STATE_DEF_SCRIPT = preload("res://scripts/enemies/enemy_ai_state_def.gd")
const ENEMY_TEMPLATE_DEF_SCRIPT = preload("res://scripts/enemies/enemy_template_def.gd")
const SKILL_DEF_SCRIPT = preload("res://scripts/player/progression/skill_def.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")
const MOVE_TO_RANGE_ACTION_SCRIPT = preload("res://scripts/enemies/actions/move_to_range_action.gd")
const MOVE_TO_ADVANTAGE_POSITION_ACTION_SCRIPT = preload("res://scripts/enemies/actions/move_to_advantage_position_action.gd")
const RETREAT_ACTION_SCRIPT = preload("res://scripts/enemies/actions/retreat_action.gd")
const USE_CHARGE_ACTION_SCRIPT = preload("res://scripts/enemies/actions/use_charge_action.gd")
const USE_CHARGE_PATH_AOE_ACTION_SCRIPT = preload("res://scripts/enemies/actions/use_charge_path_aoe_action.gd")
const USE_GROUND_SKILL_ACTION_SCRIPT = preload("res://scripts/enemies/actions/use_ground_skill_action.gd")
const USE_GROUND_REPOSITION_SKILL_ACTION_SCRIPT = preload("res://scripts/enemies/actions/use_ground_reposition_skill_action.gd")
const MOVE_TO_MULTI_UNIT_SKILL_POSITION_ACTION_SCRIPT = preload("res://scripts/enemies/actions/move_to_multi_unit_skill_position_action.gd")
const USE_MULTI_UNIT_SKILL_ACTION_SCRIPT = preload("res://scripts/enemies/actions/use_multi_unit_skill_action.gd")
const USE_RANDOM_CHAIN_SKILL_ACTION_SCRIPT = preload("res://scripts/enemies/actions/use_random_chain_skill_action.gd")
const USE_UNIT_SKILL_ACTION_SCRIPT = preload("res://scripts/enemies/actions/use_unit_skill_action.gd")
const WAIT_ACTION_SCRIPT = preload("res://scripts/enemies/actions/wait_action.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const BattleRuntimeTestHelpers = preload("res://tests/shared/battle_runtime_test_helpers.gd")
const SharedDamageResolvers = preload("res://tests/shared/stub_damage_resolvers.gd")

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
	_test_enemy_template_resolves_stable_id()
	_test_frontline_template_resolves_stable_id()
	_test_suppressor_template_resolves_stable_id()
	_test_healer_template_resolves_stable_id()
	_test_wolf_templates_spawn_with_positive_stamina_pool()
	_test_battle_unit_factory_no_longer_builds_fallback_enemy()
	_test_formal_enemy_templates_have_real_pressure_skill_action()
	_test_depleted_ranged_templates_close_for_basic_attack_fallback()
	_test_enemy_template_uses_canonical_template_id()
	_test_natural_weapon_melee_aggressor_falls_back_to_basic_attack()
	_test_ai_charge_decision_logs_brain_state_action()
	_test_frontline_bulwark_charge_decision_logs_brain_state_action()
	_test_short_regular_move_prefers_close_in_over_charge()
	_test_melee_close_in_prefers_screening_ranged_ally_when_healthy()
	_test_melee_screening_scores_actual_path_cost_block()
	_test_melee_screening_ignores_geometric_line_without_pressure()
	_test_ai_wait_action_marks_active_rest_when_stamina_starved()
	_test_ai_wait_action_reports_rest_when_no_action_is_available()
	_test_active_rest_does_not_outrank_melee_screening_move()
	_test_charge_action_scores_with_resolved_stop_anchor()
	_test_ai_assembler_adds_whirlwind_charge_path_action()
	_test_ai_charge_path_aoe_scores_repeat_hits()
	_test_ai_runtime_plan_uses_auto_whirlwind_action()
	_test_ai_ground_skill_generates_legal_command()
	_test_ai_unit_skill_scores_ranged_role_threat_target()
	_test_nearest_role_threat_enemy_selector_prefers_reachable_ranged_output()
	_test_nearest_role_threat_enemy_selector_keeps_far_ranged_output_behind_frontline()
	_test_nearest_role_threat_enemy_selector_prefers_frontline_over_far_ranged()
	_test_ai_multi_unit_skill_generates_target_unit_ids()
	_test_ai_multi_unit_skill_scores_role_threat_target_groups()
	_test_ai_assembler_routes_random_chain_to_random_chain_action()
	_test_ai_random_chain_action_uses_candidate_pool_not_target_ids()
	_test_ai_ground_skill_scores_role_threat_area_targets()
	_test_ai_skill_score_prioritizes_lethal_threat_targets()
	_test_ai_score_comparison_promotes_lethal_kill_across_buckets()
	_test_ai_score_tiebreak_prefers_lower_nonfatal_post_threat()
	_test_ai_score_allows_higher_value_nonfatal_post_threat()
	_test_ai_ground_skill_minimum_hit_count_uses_effective_enemies()
	_test_ai_ground_control_score_input_keeps_empty_cells_separate_from_effective_targets()
	_test_ai_ground_control_requires_explicit_empty_control_opt_in()
	_test_ai_ground_control_opt_in_allows_empty_control_candidate()
	_test_ai_ground_control_opt_in_does_not_allow_empty_damage_only_skill()
	_test_ai_ground_control_opt_in_does_not_bypass_partial_hit_minimum()
	_test_ai_ground_control_supplement_can_allow_partial_hit_minimum_when_enabled()
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
	_test_ai_skill_score_input_exposes_ground_metrics()
	_test_ai_skill_score_input_uses_fate_aware_repeat_attack_success_rate()
	_test_ai_score_low_hp_threshold_uses_formal_param_only()
	_test_melee_aggressor_prefers_later_higher_score_skill_action()
	_test_ranged_controller_prefers_later_higher_score_skill_action()
	_test_ranged_suppressor_prefers_suppressive_fire_against_line_cluster()
	_test_ranged_suppressor_skips_stamina_blocked_suppressive_fire()
	_test_ranged_suppressor_skips_cooldown_blocked_suppressive_fire()
	_test_ai_unit_skill_action_skips_aura_blocked_primary_skill()
	_test_ai_unit_skill_scoring_prefers_higher_hit_payoff_target()
	_test_ai_unit_skill_action_selects_scoring_variant_id()
	_test_ai_unit_skill_action_ignores_locked_and_ground_variants()
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
	missing_action.distance_reference = USE_UNIT_SKILL_ACTION_SCRIPT.DISTANCE_REF_TARGET_UNIT

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
	var template_tags: Array[StringName] = [ENEMY_TEMPLATE_DEF_SCRIPT.TAG_BEAST]
	template.tags = template_tags
	var template_skill_ids: Array[StringName] = [&"missing_enemy_template_skill"]
	template.skill_ids = template_skill_ids
	for attribute_id in UNIT_BASE_ATTRIBUTES_SCRIPT.BASE_ATTRIBUTE_IDS:
		template.base_attribute_overrides[attribute_id] = 8
	var drop_entries: Array[Dictionary] = [{
		"drop_entry_id": "missing_drop_item",
		"drop_type": "item",
		"item_id": "missing_enemy_drop_item",
		"quantity": 1,
	}]
	template.drop_entries = drop_entries
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
	_assert_true(enemy_unit != null and enemy_unit.known_active_skill_ids.has(&"basic_attack"), "天生武器 wolf_pack 单位应自动获得基础攻击。")


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


func _test_wolf_templates_spawn_with_positive_stamina_pool() -> void:
	var cases := [
		{"template_id": "wolf_pack", "encounter_id": "encounter_wolf_pack_stamina", "display_name": "荒狼群"},
		{"template_id": "wolf_raider", "encounter_id": "encounter_wolf_raider_stamina", "display_name": "荒狼袭掠者"},
		{"template_id": "wolf_alpha", "encounter_id": "encounter_wolf_alpha_stamina", "display_name": "荒狼首领"},
		{"template_id": "wolf_vanguard", "encounter_id": "encounter_wolf_vanguard_stamina", "display_name": "荒狼先锋"},
	]
	for case_variant in cases:
		if case_variant is not Dictionary:
			continue
		var case_data: Dictionary = case_variant
		var template_id := ProgressionDataUtils.to_string_name(case_data.get("template_id", ""))
		var runtime = _build_runtime_with_enemy_content()
		var encounter_anchor = _build_encounter_anchor(
			ProgressionDataUtils.to_string_name(case_data.get("encounter_id", "")),
			template_id,
			String(case_data.get("display_name", String(template_id)))
		)
		var state = runtime.start_battle(encounter_anchor, 106, {
			"ally_member_ids": [&"ally_a", &"ally_b"],
			"default_active_skill_ids": [&"warrior_heavy_strike"],
		})
		_assert_true(state != null and not state.is_empty(), "%s 模板应能正式生成战斗状态。" % String(template_id))
		if state == null or state.is_empty():
			continue
		_assert_true(not state.enemy_unit_ids.is_empty(), "%s 模板应至少生成一个敌方单位。" % String(template_id))
		for enemy_unit_id in state.enemy_unit_ids:
			var enemy_unit = state.units.get(enemy_unit_id)
			_assert_true(enemy_unit != null, "%s 模板生成的敌方单位应存在于 battle state 中。" % String(template_id))
			if enemy_unit == null:
				continue
			_assert_true(
				int(enemy_unit.attribute_snapshot.get_value(&"stamina_max")) > 0,
				"%s 模板生成的敌方单位 stamina_max 应为正值。" % String(template_id)
			)
			_assert_true(
				int(enemy_unit.current_stamina) > 0,
				"%s 模板生成的敌方单位 current_stamina 应为正值，避免技能链因资源池为 0 直接失效。" % String(template_id)
			)


func _test_battle_unit_factory_no_longer_builds_fallback_enemy() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var runtime = BATTLE_RUNTIME_MODULE_SCRIPT.new()
	runtime.setup(
		null,
		game_session.get_skill_defs(),
		{},
		{},
		null
	)
	var encounter_anchor = _build_encounter_anchor(
		&"runtime_factory_fallback_affordability",
		&"missing_runtime_factory_template",
		"工厂 fallback 敌人"
	)
	var enemy_units: Array = runtime._unit_factory.build_enemy_units(encounter_anchor, {
		"default_enemy_stamina": 0,
		"enemy_unit_count": 1,
	})
	_assert_true(enemy_units.is_empty(), "BattleUnitFactory 不应再构建 fallback enemy；敌人必须来自显式 payload 或正式模板。")
	game_session.free()


func _test_formal_enemy_templates_have_real_pressure_skill_action() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var template_keys := ProgressionDataUtils.sorted_string_keys(game_session.get_enemy_templates())
	game_session.free()
	for template_key in template_keys:
		var template_id := StringName(template_key)
		var runtime = _build_runtime_with_enemy_content()
		var enemy_unit = _build_formal_template_probe_unit(runtime, template_id)
		_assert_true(enemy_unit != null, "%s 正式模板应能构建真实战斗单位。" % String(template_id))
		if enemy_unit == null:
			continue
		var target_distance := _resolve_probe_target_distance(runtime, enemy_unit)
		var state = _build_flat_state(Vector2i(10, 5))
		runtime._state = state
		enemy_unit.set_anchor_coord(Vector2i(1, 2))
		enemy_unit.ai_state_id = &"pressure"
		enemy_unit.current_move_points = 2
		var player = _build_manual_unit(&"pressure_probe_target", "压力目标", &"player", Vector2i(1 + target_distance, 2), [&"basic_attack"])
		var second_player = _build_manual_unit(&"pressure_probe_cluster", "压力副目标", &"player", Vector2i(1 + target_distance, 3), [&"basic_attack"])
		_add_unit_to_state(runtime, state, enemy_unit, true)
		_add_unit_to_state(runtime, state, player, false)
		_add_unit_to_state(runtime, state, second_player, false)
		var decision = runtime._ai_service.choose_command(_build_ai_context(runtime, enemy_unit))
		_assert_true(decision != null and decision.command != null, "%s pressure probe 应产出正式 AI 指令。" % String(template_id))
		if decision == null or decision.command == null:
			continue
		_assert_eq(
			decision.command.command_type,
			BATTLE_COMMAND_SCRIPT.TYPE_SKILL,
			"%s 在真实 stamina/weapon 投影下应至少有一个合法 pressure 技能动作，不应只 move/wait。" % String(template_id)
		)
		var preview = runtime.preview_command(decision.command)
		_assert_true(preview != null and preview.allowed, "%s pressure 技能动作必须通过 runtime preview。" % String(template_id))


func _test_depleted_ranged_templates_close_for_basic_attack_fallback() -> void:
	var cases := [
		&"mist_beast",
		&"mist_harrier",
		&"mist_weaver",
		&"wolf_shaman",
	]
	for template_id in cases:
		var runtime = _build_runtime_with_enemy_content()
		var enemy_unit = _build_formal_template_probe_unit(runtime, template_id)
		_assert_true(enemy_unit != null, "%s depleted fallback probe 应能构建真实敌方单位。" % String(template_id))
		if enemy_unit == null:
			continue
		var basic_stamina_cost := _resolve_basic_attack_stamina_cost(runtime)
		var target_distance := maxi(_resolve_probe_target_distance(runtime, enemy_unit), 3)
		var state = _build_flat_state(Vector2i(10, 5))
		runtime._state = state
		enemy_unit.set_anchor_coord(Vector2i(1, 2))
		enemy_unit.ai_state_id = &"pressure"
		enemy_unit.current_mp = 0
		enemy_unit.current_aura = 0
		enemy_unit.current_stamina = basic_stamina_cost
		enemy_unit.attribute_snapshot.set_value(&"stamina_max", basic_stamina_cost)
		enemy_unit.current_move_points = 2
		_block_non_basic_skills(enemy_unit)
		var player = _build_manual_unit(&"depleted_range_target", "耗竭远距目标", &"player", Vector2i(1 + target_distance, 2), [&"basic_attack"])
		_add_unit_to_state(runtime, state, enemy_unit, true)
		_add_unit_to_state(runtime, state, player, false)
		var move_decision = runtime._ai_service.choose_command(_build_ai_context(runtime, enemy_unit))
		_assert_true(move_decision != null and move_decision.command != null, "%s 法力耗尽时仍应产出 fallback 指令。" % String(template_id))
		if move_decision != null and move_decision.command != null:
			_assert_true(
				move_decision.command.command_type == BATTLE_COMMAND_SCRIPT.TYPE_MOVE \
					or (
						move_decision.command.command_type == BATTLE_COMMAND_SCRIPT.TYPE_SKILL \
						and move_decision.command.skill_id == &"basic_attack"
					),
				"%s 高阶动作不可用且处于远程距离带时，应推进到 basic_attack 距离或直接使用可达 basic_attack，而不是待机。" % String(template_id)
			)

		var adjacent_state = _build_flat_state(Vector2i(5, 3))
		runtime._state = adjacent_state
		enemy_unit.set_anchor_coord(Vector2i(1, 1))
		enemy_unit.ai_state_id = &"pressure"
		enemy_unit.current_mp = 0
		enemy_unit.current_aura = 0
		enemy_unit.current_stamina = basic_stamina_cost
		enemy_unit.current_move_points = 2
		_block_non_basic_skills(enemy_unit)
		var adjacent_player = _build_manual_unit(&"depleted_adjacent_target", "耗竭近距目标", &"player", Vector2i(2, 1), [&"basic_attack"])
		_add_unit_to_state(runtime, adjacent_state, enemy_unit, true)
		_add_unit_to_state(runtime, adjacent_state, adjacent_player, false)
		var attack_decision = runtime._ai_service.choose_command(_build_ai_context(runtime, enemy_unit))
		_assert_true(attack_decision != null and attack_decision.command != null, "%s 近身 depleted fallback 应产出基础攻击。" % String(template_id))
		if attack_decision != null and attack_decision.command != null:
			_assert_eq(
				attack_decision.command.skill_id,
				&"basic_attack",
				"%s 高阶资源耗尽且已近身时，应使用 basic_attack fallback。" % String(template_id)
			)


func _test_enemy_template_uses_canonical_template_id() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var encounter_anchor = _build_encounter_anchor(&"encounter_wolf_pack_canonical", &"wolf_pack", "荒狼群")
	var state = runtime.start_battle(encounter_anchor, 102, {
		"ally_member_ids": [&"ally_a"],
		"default_active_skill_ids": [&"warrior_heavy_strike"],
	})
	_assert_true(state != null and not state.is_empty(), "wolf_pack 正式 template_id 应能创建战斗状态。")
	if state == null or state.is_empty():
		return
	var enemy_unit = state.units.get(state.enemy_unit_ids[0])
	_assert_true(enemy_unit != null and enemy_unit.enemy_template_id == &"wolf_pack", "敌方单位应保留正式 wolf_pack template_id。")
	_assert_true(enemy_unit != null and enemy_unit.ai_brain_id == &"melee_aggressor", "wolf_pack 正式模板应解析到 melee_aggressor AI。")


func _test_natural_weapon_melee_aggressor_falls_back_to_basic_attack() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(3, 1))
	runtime._state = state
	var wolf = _build_ai_unit(
		&"natural_basic_wolf",
		"基础攻击荒狼",
		&"hostile",
		Vector2i(0, 0),
		&"melee_aggressor",
		&"pressure",
		[&"warrior_heavy_strike", &"basic_attack"],
		28,
		2
	)
	wolf.set_natural_weapon_projection(&"natural_weapon", &"physical_pierce", 1, {"dice_count": 1, "dice_sides": 6, "flat_bonus": 0})
	var player = _build_manual_unit(&"basic_attack_target", "玩家", &"player", Vector2i(1, 0), [&"basic_attack"])
	_add_unit_to_state(runtime, state, wolf, true)
	_add_unit_to_state(runtime, state, player, false)
	var ai_context = _build_ai_context(runtime, wolf)
	var decision = runtime._ai_service.choose_command(ai_context)
	_assert_true(decision != null and decision.command != null, "天生武器单位在近身 pressure 状态下应能产出攻击指令。")
	_assert_eq(decision.command.skill_id if decision != null and decision.command != null else &"", &"basic_attack", "重击被装备武器门槛阻断后，天生武器单位应回退到基础攻击。")
	var preview = runtime.preview_command(decision.command if decision != null else null)
	_assert_true(preview != null and preview.allowed, "天生武器基础攻击应通过 runtime preview。")


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
	wolf.current_move_points = 0
	wolf.current_stamina = 80
	wolf.attribute_snapshot.set_value(&"stamina_max", 80)
	var player = _build_manual_unit(&"player_01", "玩家", &"player", Vector2i(4, 1), [&"warrior_heavy_strike"])
	_add_unit_to_state(runtime, state, wolf, true)
	_add_unit_to_state(runtime, state, player, false)
	state.phase = &"unit_acting"
	state.active_unit_id = wolf.unit_id

	var batch = runtime.advance(0)
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
	vanguard.current_move_points = 0
	vanguard.current_stamina = 80
	vanguard.attribute_snapshot.set_value(&"stamina_max", 80)
	var player = _build_manual_unit(&"player_01", "玩家", &"player", Vector2i(4, 1), [&"warrior_heavy_strike"])
	_add_unit_to_state(runtime, state, vanguard, true)
	_add_unit_to_state(runtime, state, player, false)
	state.phase = &"unit_acting"
	state.active_unit_id = vanguard.unit_id

	var batch = runtime.advance(0)
	_assert_true(batch != null, "frontline_bulwark AI advance 应返回有效 batch。")
	_assert_true(
		batch != null and not batch.log_lines.is_empty() and String(batch.log_lines[0]).contains("AI[frontline_bulwark/engage/vanguard_charge_open]"),
		"frontline_bulwark 冲锋开场应带出明确的 brain/state/action 日志。"
	)
	_assert_true(vanguard.coord != Vector2i(0, 1), "frontline_bulwark 在 engage 状态下应优先用 charge 接敌。")


func _test_short_regular_move_prefers_close_in_over_charge() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(4, 3))
	runtime._state = state
	var wolf = _build_ai_unit(
		&"short_charge_wolf",
		"短距冲锋狼",
		&"hostile",
		Vector2i(0, 1),
		&"melee_aggressor",
		&"engage",
		[&"charge", &"warrior_heavy_strike"],
		36,
		2
	)
	wolf.current_move_points = 2
	wolf.current_stamina = 80
	wolf.attribute_snapshot.set_value(&"stamina_max", 80)
	var player = _build_manual_unit(&"short_charge_target", "短距目标", &"player", Vector2i(2, 1), [&"warrior_heavy_strike"])
	_add_unit_to_state(runtime, state, wolf, true)
	_add_unit_to_state(runtime, state, player, false)
	var ai_context = _build_ai_context(runtime, wolf)
	var decision = runtime._ai_service.choose_command(ai_context)
	_assert_true(decision != null and decision.command != null, "短距离接敌应产出合法 AI 指令。")
	_assert_eq(
		decision.action_id if decision != null else &"",
		&"wolf_close_in",
		"短距离且普通移动可达时应走 close_in，而不是 charge_open。"
	)
	_assert_eq(
		decision.command.command_type if decision != null and decision.command != null else &"",
		BATTLE_COMMAND_SCRIPT.TYPE_MOVE,
		"短距离接敌应生成移动指令。"
	)
	_assert_eq(
		decision.command.target_coord if decision != null and decision.command != null else Vector2i(-1, -1),
		Vector2i(1, 1),
		"短距离接敌应移动到贴身格。"
	)


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
		cell.base_terrain = BATTLE_CELL_STATE_SCRIPT.TERRAIN_DEEP_WATER
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


func _test_ai_wait_action_marks_active_rest_when_stamina_starved() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var brain = ENEMY_AI_BRAIN_DEF_SCRIPT.new()
	brain.brain_id = &"active_rest_probe_brain"
	brain.default_state_id = &"pressure"
	var pressure_state = ENEMY_AI_STATE_DEF_SCRIPT.new()
	pressure_state.state_id = &"pressure"
	var basic_action = USE_UNIT_SKILL_ACTION_SCRIPT.new()
	basic_action.action_id = &"active_rest_basic"
	var basic_skill_ids: Array[StringName] = [&"basic_attack"]
	basic_action.skill_ids = basic_skill_ids
	basic_action.target_selector = &"nearest_enemy"
	basic_action.desired_min_distance = 1
	basic_action.desired_max_distance = 1
	basic_action.distance_reference = &"target_unit"
	var wait_action = WAIT_ACTION_SCRIPT.new()
	wait_action.action_id = &"active_rest_wait"
	pressure_state.actions = [basic_action, wait_action]
	brain.states = [pressure_state]
	runtime._enemy_ai_brains[brain.brain_id] = brain
	runtime._ai_service.setup(runtime._enemy_ai_brains)

	var state = _build_flat_state(Vector2i(3, 1))
	runtime._state = state
	var wolf = _build_ai_unit(
		&"active_rest_wolf",
		"体力耗尽战士",
		&"hostile",
		Vector2i(0, 0),
		brain.brain_id,
		&"pressure",
		[&"basic_attack"],
		28,
		1
	)
	wolf.current_stamina = 0
	wolf.action_threshold = 30
	wolf.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX, 40)
	wolf.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.CONSTITUTION, 3)
	var player = _build_manual_unit(&"active_rest_target", "贴身目标", &"player", Vector2i(1, 0), [&"basic_attack"])
	_add_unit_to_state(runtime, state, wolf, true)
	_add_unit_to_state(runtime, state, player, false)
	var decision = runtime._ai_service.choose_command(_build_ai_context(runtime, wolf))
	_assert_eq(
		decision.action_id if decision != null else &"",
		&"active_rest_wait",
		"体力不足且无法支付基础攻击时，默认 wait action 应表达主动休息。"
	)
	_assert_true(
		decision != null and decision.reason_text.contains("主动休息"),
		"主动休息的 AI reason_text 应明确说明资源目的。"
	)
	_assert_true(
		decision != null and decision.score_input != null and int(decision.score_input.total_score) > -40,
		"主动休息应抬高 wait 评分，但仍保持低于有效移动/守线动作。"
	)


func _test_ai_wait_action_reports_rest_when_no_action_is_available() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var brain = ENEMY_AI_BRAIN_DEF_SCRIPT.new()
	brain.brain_id = &"fallback_rest_probe_brain"
	brain.default_state_id = &"engage"
	var engage_state = ENEMY_AI_STATE_DEF_SCRIPT.new()
	engage_state.state_id = &"engage"
	var wait_action = WAIT_ACTION_SCRIPT.new()
	wait_action.action_id = &"fallback_rest_wait"
	engage_state.actions = [wait_action]
	brain.states = [engage_state]
	runtime._enemy_ai_brains[brain.brain_id] = brain
	runtime._ai_service.setup(runtime._enemy_ai_brains)

	var state = _build_flat_state(Vector2i(3, 1))
	runtime._state = state
	var wolf = _build_ai_unit(
		&"fallback_rest_wolf",
		"无动作战士",
		&"hostile",
		Vector2i(0, 0),
		brain.brain_id,
		&"engage",
		[],
		28,
		1
	)
	wolf.current_stamina = 20
	wolf.action_threshold = 30
	wolf.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX, 40)
	var player = _build_manual_unit(&"fallback_rest_target", "目标", &"player", Vector2i(2, 0), [&"basic_attack"])
	_add_unit_to_state(runtime, state, wolf, true)
	_add_unit_to_state(runtime, state, player, false)
	var decision = runtime._ai_service.choose_command(_build_ai_context(runtime, wolf))
	_assert_eq(
		decision.action_id if decision != null else &"",
		&"fallback_rest_wait",
		"没有有效动作时仍应由默认 wait action 收束。"
	)
	_assert_true(
		decision != null and decision.reason_text.contains("休息"),
		"未消耗 AP 且体力未满时，默认 wait 的 AI 文案应明确表示会进入休息。"
	)
	_assert_eq(
		int(decision.score_input.total_score) if decision != null and decision.score_input != null else 999,
		-40,
		"普通无动作休息只改变语义说明，不应提高 wait 评分去抢移动或卡位。"
	)


func _test_active_rest_does_not_outrank_melee_screening_move() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(7, 8))
	runtime._state = state
	var wolf = _build_ai_unit(
		&"rest_screening_wolf",
		"体力不足占位战士",
		&"hostile",
		Vector2i(1, 4),
		&"melee_aggressor",
		&"engage",
		[&"basic_attack"],
		28,
		1
	)
	wolf.current_stamina = 0
	wolf.current_move_points = 2
	wolf.action_threshold = 30
	wolf.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX, 40)
	wolf.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.CONSTITUTION, 3)
	var archer = _build_ai_unit(
		&"rest_screening_archer",
		"被保护弓手",
		&"hostile",
		Vector2i(3, 6),
		&"ranged_archer",
		&"pressure",
		[&"archer_aimed_shot"],
		28,
		2
	)
	_apply_test_bow_weapon(archer, 6)
	var player = _build_manual_unit(&"rest_screening_threat", "近战威胁", &"player", Vector2i(3, 3), [&"basic_attack"])
	_add_unit_to_state(runtime, state, wolf, true)
	_add_unit_to_state(runtime, state, archer, true)
	_add_unit_to_state(runtime, state, player, false)
	var decision = runtime._ai_service.choose_command(_build_ai_context(runtime, wolf))
	_assert_eq(
		decision.action_id if decision != null else &"",
		&"wolf_close_in",
		"体力不足时，主动休息不能抢掉近战战士仍可执行的守线/接敌移动。"
	)
	_assert_eq(
		decision.command.target_coord if decision != null and decision.command != null else Vector2i(-1, -1),
		Vector2i(3, 4),
		"体力不足的近战仍应先走到实际增加敌方路径成本的守线格，之后再等待休息。"
	)


func _test_charge_action_scores_with_resolved_stop_anchor() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(6, 3))
	var blocked_cell = state.cells.get(Vector2i(2, 1))
	if blocked_cell != null:
		blocked_cell.base_terrain = BATTLE_CELL_STATE_SCRIPT.TERRAIN_DEEP_WATER
		blocked_cell.recalculate_runtime_values()
	state.cell_columns = BATTLE_CELL_STATE_SCRIPT.build_columns_from_surface_cells(state.cells)
	runtime._state = state
	var wolf = _build_ai_unit(
		&"charge_score_wolf",
		"冲锋评分狼",
		&"hostile",
		Vector2i(0, 1),
		&"melee_aggressor",
		&"engage",
		[&"charge"],
		36,
		2
	)
	wolf.current_stamina = 80
	wolf.attribute_snapshot.set_value(&"stamina_max", 80)
	var player = _build_manual_unit(&"charge_focus_target", "目标玩家", &"player", Vector2i(4, 1), [&"warrior_heavy_strike"])
	_add_unit_to_state(runtime, state, wolf, true)
	_add_unit_to_state(runtime, state, player, false)
	var ai_context = _build_ai_context(runtime, wolf)
	var action = USE_CHARGE_ACTION_SCRIPT.new()
	action.action_id = &"charge_resolved_stop_anchor"
	action.skill_id = &"charge"
	action.target_selector = &"nearest_enemy"
	action.minimum_charge_move_distance = 1
	var decision = action.decide(ai_context)
	_assert_true(decision != null and decision.command != null, "charge 评分回归应能产出合法冲锋指令。")
	if decision == null or decision.command == null:
		return
	_assert_eq(
		decision.command.target_coord,
		Vector2i(1, 1),
		"charge 评分应按 preview 解析出的真实停点取分，不再偏好会被中途阻断的更远目标格。"
	)
	var preview = runtime.preview_command(decision.command)
	_assert_true(preview != null and preview.allowed, "charge 评分回归中的正式 preview 必须允许该冲锋指令。")
	_assert_eq(
		preview.resolved_anchor_coord if preview != null else Vector2i(-1, -1),
		Vector2i(1, 1),
		"charge preview 应暴露与正式执行一致的 resolved_anchor_coord。"
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
	large_target.body_size = BATTLE_UNIT_STATE_SCRIPT.BODY_SIZE_LARGE
	large_target.sync_body_size_category_from_body_size()
	large_target.refresh_footprint()
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
	large_target.body_size = BATTLE_UNIT_STATE_SCRIPT.BODY_SIZE_LARGE
	large_target.sync_body_size_category_from_body_size()
	large_target.refresh_footprint()
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
	mist.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.INTELLIGENCE, 20)
	mist.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.SPELL_PROFICIENCY_BONUS, 4)
	var player_a = _build_manual_unit(&"player_a", "玩家A", &"player", Vector2i(4, 2), [&"warrior_heavy_strike"])
	var player_b = _build_manual_unit(&"player_b", "玩家B", &"player", Vector2i(4, 3), [&"warrior_heavy_strike"])
	player_a.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.AGILITY, 10)
	player_b.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.AGILITY, 10)
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


func _test_ai_multi_unit_skill_scores_role_threat_target_groups() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(7, 5))
	runtime._state = state
	var archer = _build_ai_unit(
		&"role_threat_multishot_archer",
		"威胁连珠弓手",
		&"hostile",
		Vector2i(1, 2),
		&"ranged_archer",
		&"pressure",
		[&"archer_multishot"],
		28,
		2
	)
	archer.current_stamina = 100
	archer.attribute_snapshot.set_value(&"stamina_max", 100)
	_apply_test_bow_weapon(archer, 6)
	var normal_a = _build_manual_unit(&"multi_role_normal_a", "普通靶A", &"player", Vector2i(4, 1), [&"warrior_heavy_strike"])
	var normal_b = _build_manual_unit(&"multi_role_normal_b", "普通靶B", &"player", Vector2i(4, 2), [&"warrior_heavy_strike"])
	var healer = _build_manual_unit(&"multi_role_healer", "治疗威胁靶", &"player", Vector2i(4, 3), [&"mage_temporal_rewind"])
	_add_unit_to_state(runtime, state, archer, true)
	_add_unit_to_state(runtime, state, normal_a, false)
	_add_unit_to_state(runtime, state, normal_b, false)
	_add_unit_to_state(runtime, state, healer, false)
	var ai_context = _build_ai_context(runtime, archer)
	var skill_def = runtime._skill_defs.get(&"archer_multishot")
	var cast_variants = skill_def.combat_profile.get_unlocked_cast_variants(1) if skill_def != null and skill_def.combat_profile != null else []
	var cast_variant = cast_variants[0] if not cast_variants.is_empty() else null
	_assert_true(cast_variant != null, "multi-unit 威胁评分回归应能读取 archer_multishot cast variant。")
	if cast_variant == null:
		return
	var normal_command = _build_test_multi_unit_skill_command(archer, &"archer_multishot", cast_variant.variant_id, [normal_a, normal_b])
	var threat_command = _build_test_multi_unit_skill_command(archer, &"archer_multishot", cast_variant.variant_id, [normal_a, healer])
	var normal_preview = runtime.preview_command(normal_command)
	var threat_preview = runtime.preview_command(threat_command)
	_assert_true(normal_preview != null and normal_preview.allowed, "普通 multi-unit 威胁评分命令必须通过 preview。")
	_assert_true(threat_preview != null and threat_preview.allowed, "包含治疗威胁的 multi-unit 命令必须通过 preview。")
	var normal_score = ai_context.build_skill_score_input(
		skill_def,
		normal_command,
		normal_preview,
		cast_variant.effect_defs,
		{"position_target_unit": normal_a, "desired_min_distance": 3, "desired_max_distance": 6}
	)
	var threat_score = ai_context.build_skill_score_input(
		skill_def,
		threat_command,
		threat_preview,
		cast_variant.effect_defs,
		{"position_target_unit": normal_a, "desired_min_distance": 3, "desired_max_distance": 6}
	)
	_assert_true(normal_score != null and threat_score != null, "multi-unit 威胁评分回归应拿到两个合法评分。")
	if normal_score == null or threat_score == null:
		return
	_assert_eq(normal_score.target_count, threat_score.target_count, "multi-unit 威胁评分前置：两个候选应命中相同目标数。")
	_assert_true(
		threat_score.target_priority_score > normal_score.target_priority_score,
		"multi-unit 技能应对包含治疗威胁目标的组合产生更高 target_priority_score。"
	)
	_assert_true(
		threat_score.total_score > normal_score.total_score,
		"multi-unit 技能在命中数相同时，应因目标威胁优先包含治疗单位的组合。"
	)


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
	action.distance_reference = USE_RANDOM_CHAIN_SKILL_ACTION_SCRIPT.DISTANCE_REF_CANDIDATE_POOL
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


func _test_ai_ground_skill_scores_role_threat_area_targets() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(7, 7))
	runtime._state = state
	var caster = _build_ai_unit(
		&"role_threat_fireballer",
		"威胁火球术士",
		&"hostile",
		Vector2i(1, 3),
		&"ranged_controller",
		&"pressure",
		[&"mage_fireball"],
		24,
		2
	)
	caster.current_mp = 120
	caster.attribute_snapshot.set_value(&"mp_max", 120)
	var normal_a = _build_manual_unit(&"ground_role_normal_a", "范围普通A", &"player", Vector2i(4, 1), [&"warrior_heavy_strike"])
	var normal_b = _build_manual_unit(&"ground_role_normal_b", "范围普通B", &"player", Vector2i(4, 2), [&"warrior_heavy_strike"])
	var healer = _build_manual_unit(&"ground_role_healer", "范围治疗威胁", &"player", Vector2i(4, 4), [&"mage_temporal_rewind"])
	var normal_c = _build_manual_unit(&"ground_role_normal_c", "范围普通C", &"player", Vector2i(4, 5), [&"warrior_heavy_strike"])
	_add_unit_to_state(runtime, state, caster, true)
	_add_unit_to_state(runtime, state, normal_a, false)
	_add_unit_to_state(runtime, state, normal_b, false)
	_add_unit_to_state(runtime, state, healer, false)
	_add_unit_to_state(runtime, state, normal_c, false)
	var ai_context = _build_ai_context(runtime, caster)
	var skill_def = runtime._skill_defs.get(&"mage_fireball")
	var normal_command = _build_test_ground_skill_command(caster, &"mage_fireball", Vector2i(4, 2))
	var threat_command = _build_test_ground_skill_command(caster, &"mage_fireball", Vector2i(4, 4))
	var normal_preview = runtime.preview_command(normal_command)
	var threat_preview = runtime.preview_command(threat_command)
	_assert_true(normal_preview != null and normal_preview.allowed, "普通范围威胁评分命令必须通过 preview。")
	_assert_true(threat_preview != null and threat_preview.allowed, "包含治疗威胁的范围命令必须通过 preview。")
	var normal_score = ai_context.build_skill_score_input(
		skill_def,
		normal_command,
		normal_preview,
		skill_def.combat_profile.effect_defs if skill_def != null and skill_def.combat_profile != null else [],
		{"desired_min_distance": 3, "desired_max_distance": 4}
	)
	var threat_score = ai_context.build_skill_score_input(
		skill_def,
		threat_command,
		threat_preview,
		skill_def.combat_profile.effect_defs if skill_def != null and skill_def.combat_profile != null else [],
		{"desired_min_distance": 3, "desired_max_distance": 4}
	)
	_assert_true(normal_score != null and threat_score != null, "范围威胁评分回归应拿到两个合法评分。")
	if normal_score == null or threat_score == null:
		return
	_assert_eq(normal_score.target_count, threat_score.target_count, "范围威胁评分前置：两个候选应命中相同目标数。")
	_assert_true(
		threat_score.target_priority_score > normal_score.target_priority_score,
		"范围技能应对覆盖治疗威胁目标的地格产生更高 target_priority_score。"
	)
	_assert_true(
		threat_score.total_score > normal_score.total_score,
		"范围技能在命中数相同时，应因目标威胁优先覆盖治疗单位。"
	)


func _test_ai_skill_score_prioritizes_lethal_threat_targets() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(8, 6))
	runtime._state = state
	var mage = _build_ai_unit(
		&"lethal_threat_mage",
		"击杀威胁法师",
		&"hostile",
		Vector2i(1, 2),
		&"mage_controller",
		&"pressure",
		[&"mage_fireball", &"mage_chain_lightning"],
		28,
		1
	)
	mage.current_mp = 1000
	mage.attribute_snapshot.set_value(&"mp_max", 1000)
	for skill_id in mage.known_active_skill_ids:
		mage.known_skill_level_map[skill_id] = 7
	var archer_a = _build_manual_unit(&"lethal_threat_archer_a", "低血远程威胁A", &"player", Vector2i(5, 2), [&"archer_aimed_shot", &"basic_attack"])
	var archer_b = _build_manual_unit(&"lethal_threat_archer_b", "低血远程威胁B", &"player", Vector2i(5, 4), [&"archer_aimed_shot", &"basic_attack"])
	for archer in [archer_a, archer_b]:
		archer.current_hp = 10
		archer.attribute_snapshot.set_value(&"hp_max", 30)
		_apply_test_bow_weapon(archer, 5)
	_add_unit_to_state(runtime, state, mage, true)
	_add_unit_to_state(runtime, state, archer_a, false)
	_add_unit_to_state(runtime, state, archer_b, false)
	var ai_context = _build_ai_context(runtime, mage)
	var fireball_def = runtime._skill_defs.get(&"mage_fireball")
	var chain_def = runtime._skill_defs.get(&"mage_chain_lightning")
	var fireball_command = _build_test_ground_skill_command(mage, &"mage_fireball", archer_a.coord)
	var chain_command = _build_test_unit_skill_command(mage, &"mage_chain_lightning", archer_a)
	var fireball_preview = runtime.preview_command(fireball_command)
	var chain_preview = runtime.preview_command(chain_command)
	_assert_true(fireball_preview != null and fireball_preview.allowed, "击杀威胁火球命令必须通过 preview。")
	_assert_true(chain_preview != null and chain_preview.allowed, "击杀威胁链闪命令必须通过 preview。")
	var fireball_score = ai_context.build_skill_score_input(
		fireball_def,
		fireball_command,
		fireball_preview,
		fireball_def.combat_profile.effect_defs if fireball_def != null and fireball_def.combat_profile != null else [],
		{"desired_min_distance": 4, "desired_max_distance": 5}
	)
	var chain_score = ai_context.build_skill_score_input(
		chain_def,
		chain_command,
		chain_preview,
		chain_def.combat_profile.effect_defs if chain_def != null and chain_def.combat_profile != null else [],
		{"desired_min_distance": 4, "desired_max_distance": 5, "position_target_unit": archer_a}
	)
	_assert_true(fireball_score != null and chain_score != null, "击杀威胁评分回归应拿到两个合法评分。")
	if fireball_score == null or chain_score == null:
		return
	_assert_true(
		fireball_score.estimated_lethal_threat_target_count >= 2,
		"范围技能应识别会死亡的多个威胁目标。"
	)
	_assert_eq(
		chain_score.estimated_lethal_threat_target_count,
		1,
		"单体链闪只应识别一个会死亡的威胁目标。"
	)
	_assert_true(
		fireball_score.total_score > chain_score.total_score,
		"当范围技能能击杀更多威胁单位时，应优先杀人而不是先打单体链闪。"
	)


func _test_ai_score_comparison_promotes_lethal_kill_across_buckets() -> void:
	var ai_service = BATTLE_AI_SERVICE_SCRIPT.new()
	var skill_action = USE_GROUND_SKILL_ACTION_SCRIPT.new()
	var lethal_threat_offense = _build_score_input_for_ordering_test(&"mist_offense", 100, 80, 1, 1, 1, 0)
	var lethal_enemy_offense = _build_score_input_for_ordering_test(&"mist_offense", 100, 80, 0, 1, 1, 0)
	var control = _build_score_input_for_ordering_test(&"mist_control", 110, 900, 0, 0, 2, 0)
	_assert_true(
		ai_service._is_better_score_input(lethal_threat_offense, control),
		"跨 action bucket 比较时，威胁击杀应压过普通控场 bucket。"
	)
	_assert_true(
		not ai_service._is_better_score_input(control, lethal_threat_offense),
		"普通控场不能因 bucket/total_score 更高反压威胁击杀。"
	)
	_assert_true(
		skill_action._is_better_skill_score_input(lethal_threat_offense, control),
		"单个 action 内部候选比较也应让威胁击杀压过普通控场。"
	)
	var emergency_escape = _build_score_input_for_ordering_test(&"archer_survival", 150, 220, 0, 0, 0, 120)
	emergency_escape.position_current_distance = 2
	emergency_escape.position_safe_distance = 4
	emergency_escape.distance_to_primary_coord = 4
	_assert_true(
		ai_service._is_better_score_input(lethal_threat_offense, emergency_escape),
		"能击杀威胁目标时，应压过纯逃生换位，避免法师浪费致命先手。"
	)
	_assert_true(
		not ai_service._is_better_score_input(emergency_escape, lethal_threat_offense),
		"纯逃生换位不能反压威胁击杀。"
	)
	_assert_true(
		ai_service._is_better_score_input(lethal_enemy_offense, emergency_escape),
		"普通击杀也应压过纯逃生换位，避免法师在可杀人时空耗先手。"
	)
	_assert_true(
		not ai_service._is_better_score_input(emergency_escape, lethal_enemy_offense),
		"纯逃生换位不能反压普通击杀。"
	)
	var mild_escape = _build_score_input_for_ordering_test(&"archer_survival", 150, 220, 0, 0, 0, 120)
	mild_escape.position_current_distance = 3
	mild_escape.position_safe_distance = 4
	mild_escape.distance_to_primary_coord = 4
	_assert_true(
		ai_service._is_better_score_input(lethal_threat_offense, mild_escape),
		"轻度不安全的换位不应压过可击杀目标。"
	)
	var short_escape = _build_score_input_for_ordering_test(&"archer_survival", 150, 260, 0, 0, 0, 160)
	short_escape.position_current_distance = 1
	short_escape.position_safe_distance = 4
	short_escape.distance_to_primary_coord = 3
	_assert_true(
		ai_service._is_better_score_input(lethal_enemy_offense, short_escape),
		"未真正脱离安全距离的换位不应以紧急生存身份压过击杀。"
	)
	var unsafe_lethal_enemy_offense = _build_score_input_for_ordering_test(&"mist_offense", 100, 180, 0, 1, 1, 0)
	var safe_escape = _build_score_input_for_ordering_test(&"archer_survival", 150, 120, 0, 0, 0, 120)
	_mark_survival_projection(unsafe_lethal_enemy_offense, true, true, 45, -21)
	_mark_survival_projection(safe_escape, true, false, 0, 24)
	_assert_true(
		ai_service._is_better_score_input(safe_escape, unsafe_lethal_enemy_offense),
		"若击杀后仍会被剩余威胁秒杀，安全换位应跨 bucket 压过普通击杀。"
	)
	_assert_true(
		not ai_service._is_better_score_input(unsafe_lethal_enemy_offense, safe_escape),
		"仍处于致死风险的击杀不能反压已经解除致死风险的生存动作。"
	)
	_assert_true(
		skill_action._is_better_skill_score_input(safe_escape, unsafe_lethal_enemy_offense),
		"单个 action 内部候选比较也应使用行动后致死风险。"
	)
	_mark_survival_projection(lethal_threat_offense, true, false, 0, 24)
	_mark_survival_projection(safe_escape, true, false, 0, 24)
	_assert_true(
		ai_service._is_better_score_input(lethal_threat_offense, safe_escape),
		"当击杀动作同样解除致死风险时，威胁击杀应继续压过纯生存动作。"
	)


func _test_ai_score_tiebreak_prefers_lower_nonfatal_post_threat() -> void:
	var ai_service = BATTLE_AI_SERVICE_SCRIPT.new()
	var reposition_action = USE_GROUND_REPOSITION_SKILL_ACTION_SCRIPT.new()
	var safe_escape = _build_score_input_for_ordering_test(&"archer_survival", 150, 220, 0, 0, 0, 120)
	var risky_escape = _build_score_input_for_ordering_test(&"archer_survival", 150, 220, 0, 0, 0, 120)
	_mark_nonfatal_survival_projection(safe_escape, 0, 0, 24)
	_mark_nonfatal_survival_projection(risky_escape, 1, 8, 16)
	_assert_true(
		ai_service._is_better_score_input(safe_escape, risky_escape),
		"跨 action 同等收益时，应优先选择行动后无剩余威胁的换位。"
	)
	_assert_true(
		not ai_service._is_better_score_input(risky_escape, safe_escape),
		"跨 action 同等收益时，新增非致死威胁的换位不应反压无威胁换位。"
	)
	_assert_true(
		reposition_action._is_better_skill_score_input(safe_escape, risky_escape),
		"闪现候选同等收益时，应优先选择行动后无剩余威胁的落点。"
	)
	_assert_true(
		not reposition_action._is_better_skill_score_input(risky_escape, safe_escape),
		"闪现候选同等收益时，新增非致死威胁的落点不应反压无威胁落点。"
	)
	var zero_damage_safe = _build_score_input_for_ordering_test(&"archer_survival", 150, 220, 0, 0, 0, 120)
	var zero_damage_risky = _build_score_input_for_ordering_test(&"archer_survival", 150, 220, 0, 0, 0, 120)
	_mark_nonfatal_survival_projection(zero_damage_safe, 0, 0, 24)
	_mark_nonfatal_survival_projection(zero_damage_risky, 1, 0, 24)
	_assert_true(
		reposition_action._is_better_skill_score_input(zero_damage_safe, zero_damage_risky),
		"闪现候选同等收益且预期伤害同为 0 时，仍应优先无威胁落点。"
	)
	var lethal_safe = _build_score_input_for_ordering_test(&"mist_offense", 100, 180, 0, 1, 1, 0)
	var lethal_risky = _build_score_input_for_ordering_test(&"mist_offense", 100, 180, 0, 1, 1, 0)
	_mark_nonfatal_survival_projection(lethal_safe, 0, 0, 24)
	_mark_nonfatal_survival_projection(lethal_risky, 1, 8, 16)
	_assert_true(
		ai_service._is_better_score_input(lethal_safe, lethal_risky),
		"同等击杀价值下，也应优先行动后威胁更低的候选。"
	)
	_assert_true(
		reposition_action._is_better_skill_score_input(lethal_safe, lethal_risky),
		"单个 action 内同等击杀价值下，也应优先行动后威胁更低的候选。"
	)


func _test_ai_score_allows_higher_value_nonfatal_post_threat() -> void:
	var ai_service = BATTLE_AI_SERVICE_SCRIPT.new()
	var reposition_action = USE_GROUND_REPOSITION_SKILL_ACTION_SCRIPT.new()
	var safe_escape = _build_score_input_for_ordering_test(&"archer_survival", 150, 220, 0, 0, 0, 120)
	var risky_escape = _build_score_input_for_ordering_test(&"archer_survival", 150, 240, 0, 0, 0, 120)
	_mark_nonfatal_survival_projection(safe_escape, 0, 0, 24)
	_mark_nonfatal_survival_projection(risky_escape, 1, 8, 16)
	_assert_true(
		ai_service._is_better_score_input(risky_escape, safe_escape),
		"跨 action 比较中，更高收益的非致死风险换位仍应允许胜出。"
	)
	_assert_true(
		reposition_action._is_better_skill_score_input(risky_escape, safe_escape),
		"闪现候选中，更高收益的非致死风险落点仍应允许胜出。"
	)
	var lethal_safe = _build_score_input_for_ordering_test(&"mist_offense", 100, 180, 0, 1, 1, 0)
	var lethal_risky = _build_score_input_for_ordering_test(&"mist_offense", 100, 200, 0, 1, 1, 0)
	_mark_nonfatal_survival_projection(lethal_safe, 0, 0, 24)
	_mark_nonfatal_survival_projection(lethal_risky, 1, 8, 16)
	_assert_true(
		ai_service._is_better_score_input(lethal_risky, lethal_safe),
		"击杀候选中，更高收益的非致死风险动作仍应允许胜出。"
	)
	_assert_true(
		reposition_action._is_better_skill_score_input(lethal_risky, lethal_safe),
		"单个 action 内击杀候选中，更高收益的非致死风险动作仍应允许胜出。"
	)


func _build_score_input_for_ordering_test(
	bucket_id: StringName,
	bucket_priority: int,
	total_score: int,
	lethal_threat_count: int,
	lethal_count: int,
	effective_target_count: int,
	position_score: int
):
	var score_input = BATTLE_AI_SCORE_INPUT_SCRIPT.new()
	score_input.score_bucket_id = bucket_id
	score_input.score_bucket_priority = bucket_priority
	score_input.total_score = total_score
	score_input.hit_payoff_score = total_score
	score_input.target_count = effective_target_count
	score_input.effective_target_count = effective_target_count
	score_input.enemy_target_count = effective_target_count
	score_input.estimated_damage = 10 if effective_target_count > 0 else 0
	score_input.estimated_control_count = effective_target_count if bucket_id == &"mist_control" else 0
	score_input.estimated_lethal_threat_target_count = lethal_threat_count
	score_input.estimated_lethal_target_count = lethal_count
	score_input.position_objective_score = position_score
	return score_input


func _mark_survival_projection(score_input, pre_fatal: bool, post_fatal: bool, post_damage: int, post_margin: int) -> void:
	if score_input == null:
		return
	score_input.has_post_action_threat_projection = true
	score_input.pre_action_threat_count = 1 if pre_fatal else 0
	score_input.pre_action_threat_expected_damage = 40 if pre_fatal else 0
	score_input.pre_action_survival_margin = -16 if pre_fatal else 24
	score_input.pre_action_is_lethal_survival_risk = pre_fatal
	score_input.post_action_remaining_threat_count = 1 if post_fatal else 0
	score_input.post_action_remaining_threat_expected_damage = post_damage
	score_input.post_action_survival_margin = post_margin
	score_input.post_action_is_lethal_survival_risk = post_fatal


func _mark_nonfatal_survival_projection(score_input, post_count: int, post_damage: int, post_margin: int) -> void:
	if score_input == null:
		return
	score_input.has_post_action_threat_projection = true
	score_input.pre_action_threat_count = 0
	score_input.pre_action_threat_expected_damage = 0
	score_input.pre_action_survival_margin = 24
	score_input.pre_action_is_lethal_survival_risk = false
	score_input.post_action_remaining_threat_count = post_count
	score_input.post_action_remaining_threat_expected_damage = post_damage
	score_input.post_action_survival_margin = post_margin
	score_input.post_action_is_lethal_survival_risk = false


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
	var skill_def = runtime._skill_defs.get(&"mage_fireball")
	var command = _build_test_ground_skill_command(mage, &"mage_fireball", target.coord)
	var preview = runtime.preview_command(command)
	_assert_true(preview != null and preview.allowed, "友伤火球命令必须通过 preview。")
	var score = ai_context.build_skill_score_input(
		skill_def,
		command,
		preview,
		skill_def.combat_profile.effect_defs if skill_def != null and skill_def.combat_profile != null else [],
		{"desired_min_distance": 4, "desired_max_distance": 5}
	)
	_assert_true(score != null, "友伤火球评分应可生成。")
	if score == null:
		return
	_assert_eq(score.enemy_target_count, 1, "minimum_hit_count 应只把敌方有效目标计入收益。")
	_assert_eq(score.effective_target_count, 1, "友军被火球覆盖不能贡献有效命中数。")
	_assert_true(score.estimated_friendly_fire_target_count >= 1, "评分应识别火球友伤目标。")
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


func _test_ai_ground_control_score_input_keeps_empty_cells_separate_from_effective_targets() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var ground_control_skill = _build_test_ground_control_skill(&"ai_empty_ground_control_score_test")
	runtime._skill_defs[ground_control_skill.skill_id] = ground_control_skill
	var state = _build_flat_state(Vector2i(6, 5))
	runtime._state = state
	var caster = _build_ai_unit(
		&"empty_ground_control_scorer",
		"空地控场评分者",
		&"hostile",
		Vector2i(1, 2),
		&"mage_controller",
		&"pressure",
		[ground_control_skill.skill_id],
		28,
		1
	)
	_add_unit_to_state(runtime, state, caster, true)
	var ai_context = _build_ai_context(runtime, caster)
	var command = _build_test_ground_skill_command(caster, ground_control_skill.skill_id, Vector2i(3, 2))
	var preview = runtime.preview_command(command)
	_assert_true(preview != null and preview.allowed, "空地控场命令必须通过 preview。")
	_assert_true(preview != null and preview.target_unit_ids.is_empty(), "空地控场 preview 不应伪造目标单位。")
	var score = ai_context.build_skill_score_input(
		ground_control_skill,
		command,
		preview,
		ground_control_skill.combat_profile.effect_defs,
		{"desired_min_distance": 0, "desired_max_distance": 5}
	)
	_assert_true(score != null, "空地控场评分应可生成。")
	if score == null:
		return
	_assert_eq(score.target_count, 0, "空地控场不应把地格计入 target_count。")
	_assert_eq(score.enemy_target_count, 0, "空地控场不应伪造敌方目标。")
	_assert_eq(score.effective_target_count, 0, "空地控场不应伪造有效命中数。")
	_assert_eq(score.estimated_ground_control_cell_count, 1, "空地控场应按 preview target_coords 暴露受控地格数。")
	_assert_true(score.ground_control_score > 0, "空地控场应产生独立地格控制评分。")


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
	action.distance_reference = USE_GROUND_SKILL_ACTION_SCRIPT.DISTANCE_REF_TARGET_COORD
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
	action.distance_reference = USE_GROUND_SKILL_ACTION_SCRIPT.DISTANCE_REF_TARGET_COORD
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
	action.distance_reference = USE_GROUND_SKILL_ACTION_SCRIPT.DISTANCE_REF_TARGET_COORD
	var decision = action.decide(_build_ai_context(runtime, caster))
	_assert_true(decision == null, "即使开启 allow_empty_ground_control，纯伤害地面技能也不能空放。")


func _test_ai_ground_control_opt_in_does_not_bypass_partial_hit_minimum() -> void:
	var action = USE_GROUND_SKILL_ACTION_SCRIPT.new()
	action.action_id = &"partial_hit_ground_control_probe"
	action.minimum_hit_count = 2
	action.allow_empty_ground_control = true
	action.minimum_ground_control_score = 1
	var score_input = BATTLE_AI_SCORE_INPUT_SCRIPT.new()
	score_input.effective_target_count = 1
	score_input.estimated_ground_control_cell_count = 3
	score_input.ground_control_score = 999
	_assert_true(
		not action._passes_minimum_effective_target_or_ground_control(score_input),
		"已有有效命中但未达到 minimum_hit_count 时，空地控场豁免不能绕过命中门槛。"
	)


func _test_ai_ground_control_supplement_can_allow_partial_hit_minimum_when_enabled() -> void:
	var action = USE_GROUND_SKILL_ACTION_SCRIPT.new()
	action.action_id = &"partial_hit_ground_control_supplement_probe"
	action.minimum_hit_count = 2
	action.allow_empty_ground_control = true
	action.allow_ground_control_supplement_partial_hits = true
	action.minimum_ground_control_score = 1
	var score_input = BATTLE_AI_SCORE_INPUT_SCRIPT.new()
	score_input.effective_target_count = 1
	score_input.estimated_ground_control_cell_count = 3
	score_input.ground_control_score = 999
	_assert_true(
		action._passes_minimum_effective_target_or_ground_control(score_input),
		"显式开启地格控制补足时，部分命中且地格控制分达标的候选应能通过。"
	)


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
	var skill_def = runtime._skill_defs.get(&"mage_chain_lightning")
	var command = _build_test_unit_skill_command(mage, &"mage_chain_lightning", target)
	var preview = runtime.preview_command(command)
	_assert_true(preview != null and preview.allowed, "友伤链闪命令必须通过 preview。")
	var score = ai_context.build_skill_score_input(
		skill_def,
		command,
		preview,
		skill_def.combat_profile.effect_defs if skill_def != null and skill_def.combat_profile != null else [],
		{"desired_min_distance": 4, "desired_max_distance": 5, "position_target_unit": target}
	)
	_assert_true(score != null, "友伤链闪评分应可生成。")
	if score == null:
		return
	_assert_true(score.estimated_chain_ally_target_count >= 1, "链闪评分应预估会弹射到友军。")
	_assert_true(score.estimated_friendly_fire_target_count >= 1, "链闪评分应把友军弹射计为友伤风险。")
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
		BATTLE_COMMAND_SCRIPT.TYPE_MOVE,
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
	action.distance_reference = USE_GROUND_SKILL_ACTION_SCRIPT.DISTANCE_REF_TARGET_COORD
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
	action.distance_reference = USE_GROUND_SKILL_ACTION_SCRIPT.DISTANCE_REF_TARGET_COORD
	var decision = action.decide(ai_context)
	_assert_true(decision != null and decision.command != null, "强风术应能通过外缘覆盖命中 4 格外目标。")
	_assert_eq(
		decision.score_input.desired_max_distance if decision != null and decision.score_input != null else -1,
		4,
		"强风术的 AI 距离合同应读取 range 1 + cone 外缘 3。"
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
		BATTLE_COMMAND_SCRIPT.TYPE_MOVE,
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
		BATTLE_COMMAND_SCRIPT.TYPE_MOVE,
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
	for target_variant in targets:
		var target = target_variant as BattleUnitState
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


func _test_ai_skill_score_input_exposes_ground_metrics() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var trap_damage_resolver := SharedDamageResolvers.TrapDamageResolver.new()
	runtime.configure_damage_resolver_for_tests(trap_damage_resolver)
	var state = _build_flat_state(Vector2i(7, 5))
	runtime._state = state
	var harrier = _build_ai_unit(
		&"mist_harrier_score",
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
	var action = USE_GROUND_SKILL_ACTION_SCRIPT.new()
	action.action_id = &"ground_score_probe"
	var ground_action_skill_ids: Array[StringName] = [&"archer_suppressive_fire"]
	action.skill_ids = ground_action_skill_ids
	action.minimum_hit_count = 2
	action.desired_min_distance = 0
	action.desired_max_distance = 6
	action.distance_reference = &"target_coord"
	var decision = action.decide(ai_context)
	_assert_true(decision != null and decision.command != null, "ground skill 评分回归应先拿到合法候选。")
	if decision == null or decision.command == null:
		return
	var skill_def = runtime._skill_defs.get(decision.command.skill_id)
	var preview = runtime.preview_command(decision.command)
	var score_input = ai_context.build_skill_score_input(
		skill_def,
		decision.command,
		preview,
		skill_def.combat_profile.effect_defs if skill_def != null and skill_def.combat_profile != null else [],
		{}
	)
	_assert_true(score_input != null, "AI skill score input 应由 BattleAiContext 正式构造。")
	if score_input == null:
		return
	_assert_true(score_input.hit_payoff_score > 0, "ground skill score input 应暴露正向命中收益。")
	_assert_eq(
		int(trap_damage_resolver.resolve_effects_calls),
		0,
		"AI 评分不应通过 BattleDamageResolver.resolve_effects() 偷取随机伤害结果。"
	)
	_assert_true(score_input.target_count >= 2, "ground skill score input 应暴露目标数量。")
	_assert_eq(score_input.ap_cost, 2, "ground skill score input 应暴露 AP 消耗。")
	_assert_eq(score_input.stamina_cost, 2, "ground skill score input 应暴露 ST 消耗。")
	_assert_eq(score_input.cooldown_tu, 15, "ground skill score input 应暴露 cooldown_tu。")
	_assert_true(score_input.resource_cost_score > 0, "ground skill score input 应暴露资源消耗评分。")
	_assert_eq(score_input.position_objective_kind, &"cast_distance", "ground skill score input 应记录默认站位目标类型。")
	_assert_true(score_input.distance_to_primary_coord >= 0, "ground skill score input 应记录站位目标距离。")
	_assert_true(score_input.position_objective_score >= 0, "ground skill score input 应暴露站位目标评分。")


func _test_ai_skill_score_input_uses_fate_aware_repeat_attack_success_rate() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var skill_def := SKILL_DEF_SCRIPT.new()
	skill_def.skill_id = &"ai_fate_preview_combo"
	skill_def.display_name = "评分命契连斩"
	skill_def.combat_profile = COMBAT_SKILL_DEF_SCRIPT.new()
	skill_def.combat_profile.skill_id = skill_def.skill_id
	skill_def.combat_profile.attack_roll_bonus = 0
	skill_def.combat_profile.aura_cost = 1
	var repeat_attack_effect := COMBAT_EFFECT_DEF_SCRIPT.new()
	repeat_attack_effect.effect_type = &"repeat_attack_until_fail"
	repeat_attack_effect.params = {
		"base_attack_bonus": 0,
		"follow_up_attack_penalty": 0,
		"follow_up_cost_multiplier": 2.0,
		"cost_resource": &"aura",
	}
	var damage_effect := COMBAT_EFFECT_DEF_SCRIPT.new()
	damage_effect.effect_type = &"damage"
	damage_effect.power = 10
	skill_def.combat_profile.effect_defs = [repeat_attack_effect, damage_effect]
	runtime._skill_defs[skill_def.skill_id] = skill_def

	var state = _build_flat_state(Vector2i(5, 3))
	runtime._state = state
	var scorer = _build_manual_unit(&"fate_score_user", "评分高运者", &"hostile", Vector2i(1, 1), [skill_def.skill_id])
	scorer.current_aura = 1
	scorer.unlock_combat_resource(BATTLE_UNIT_STATE_SCRIPT.COMBAT_RESOURCE_AURA)
	scorer.attribute_snapshot.set_value(&"aura_max", 1)
	scorer.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 80)
	scorer.attribute_snapshot.set_value(&"hidden_luck_at_birth", 2)
	var target = _build_manual_unit(&"fate_score_target", "高闪避目标", &"player", Vector2i(2, 1), [&"warrior_heavy_strike"])
	target.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 99)
	_add_unit_to_state(runtime, state, scorer, true)
	_add_unit_to_state(runtime, state, target, false)
	var ai_context = _build_ai_context(runtime, scorer)

	var command = BATTLE_COMMAND_SCRIPT.new()
	command.command_type = command.TYPE_SKILL
	command.unit_id = scorer.unit_id
	command.skill_id = skill_def.skill_id
	command.target_unit_id = target.unit_id
	command.target_coord = target.coord
	var preview = runtime.preview_command(command)
	var score_input = ai_context.build_skill_score_input(
		skill_def,
		command,
		preview,
		skill_def.combat_profile.effect_defs,
		{"position_target_unit": target, "desired_min_distance": 1, "desired_max_distance": 1}
	)
	_assert_true(score_input != null, "AI fate-aware 命中率回归应构造出合法 score input。")
	if score_input == null:
		return
	_assert_eq(preview.hit_preview.get("stage_base_hit_rates", []), [10], "AI 回归前置：preview 应保留 raw 命中率。")
	_assert_eq(preview.hit_preview.get("stage_success_rates", []), [15], "AI 回归前置：preview 应把高位大成功自动命中并入正式成功率。")
	_assert_eq(score_input.estimated_hit_rate_percent, 15, "AI 评分应消费 fate-aware repeat_attack 成功率，而不是 raw hit rate。")


func _test_ai_score_low_hp_threshold_uses_formal_param_only() -> void:
	var score_service = BATTLE_AI_SCORE_SERVICE_SCRIPT.new()
	var target = _build_manual_unit(&"ai_score_low_hp_target", "阈值目标", &"player", Vector2i(2, 1), [&"warrior_heavy_strike"])
	target.current_hp = 18
	target.attribute_snapshot.set_value(&"hp_max", 30)

	var formal_effect = COMBAT_EFFECT_DEF_SCRIPT.new()
	formal_effect.effect_type = &"damage"
	formal_effect.bonus_condition = &"target_low_hp"
	formal_effect.params = {"hp_ratio_threshold_percent": 70, "bonus_damage_dice_count": 2, "bonus_damage_dice_sides": 1}
	_assert_true(
		score_service._has_bonus_condition(formal_effect, target),
		"AI 评分应读取正式 hp_ratio_threshold_percent 判定低血追加骰。"
	)

	var legacy_effect = COMBAT_EFFECT_DEF_SCRIPT.new()
	legacy_effect.effect_type = &"damage"
	legacy_effect.bonus_condition = &"target_low_hp"
	legacy_effect.params = {"low_hp_ratio": 0.7}
	_assert_true(
		not score_service._has_bonus_condition(legacy_effect, target),
		"AI 评分不应再读取旧 low_hp_ratio alias。"
	)


func _test_melee_aggressor_prefers_later_higher_score_skill_action() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var source_brain = runtime._enemy_ai_brains.get(&"melee_aggressor")
	var brain = source_brain.duplicate(true) if source_brain != null else null
	if brain != null:
		runtime._enemy_ai_brains[brain.brain_id] = brain
		runtime._ai_service.setup(runtime._enemy_ai_brains)
	var pressure_state = brain.get_state(&"pressure") if brain != null else null
	_assert_true(pressure_state != null, "melee_aggressor 应暴露 pressure 状态供评分回归覆盖。")
	if pressure_state == null:
		return
	var lower_score_action = USE_UNIT_SKILL_ACTION_SCRIPT.new()
	lower_score_action.action_id = &"wolf_pressure_heavy_strike"
	var lower_score_skill_ids: Array[StringName] = [&"warrior_heavy_strike"]
	lower_score_action.skill_ids = lower_score_skill_ids
	lower_score_action.target_selector = &"nearest_enemy"
	lower_score_action.desired_min_distance = 1
	lower_score_action.desired_max_distance = 1
	lower_score_action.distance_reference = &"target_unit"
	lower_score_action.score_bucket_id = &"wolf_pressure_offense"
	var higher_score_action = USE_UNIT_SKILL_ACTION_SCRIPT.new()
	higher_score_action.action_id = &"wolf_pressure_execution"
	var higher_score_skill_ids: Array[StringName] = [&"warrior_execution_cleave"]
	higher_score_action.skill_ids = higher_score_skill_ids
	higher_score_action.target_selector = &"nearest_enemy"
	higher_score_action.desired_min_distance = 1
	higher_score_action.desired_max_distance = 1
	higher_score_action.distance_reference = &"target_unit"
	higher_score_action.score_bucket_id = &"wolf_pressure_offense"
	pressure_state.actions = [lower_score_action, higher_score_action]

	var state = _build_flat_state(Vector2i(5, 3))
	runtime._state = state
	var score_profile = BATTLE_AI_SCORE_PROFILE_SCRIPT.new()
	score_profile.stamina_cost_weight = 0
	score_profile.cooldown_weight = 0
	runtime.set_ai_score_profile(score_profile)
	var wolf = _build_ai_unit(
		&"wolf_score_melee",
		"荒狼评分手",
		&"hostile",
		Vector2i(1, 1),
		&"melee_aggressor",
		&"pressure",
		[&"warrior_heavy_strike", &"warrior_execution_cleave"],
		26,
		2
	)
	wolf.current_stamina = 80
	wolf.attribute_snapshot.set_value(&"stamina_max", 80)
	wolf.apply_weapon_projection({
		"weapon_profile_kind": "equipped",
		"weapon_item_id": "score_test_blade",
		"weapon_profile_type_id": "shortsword",
		"weapon_current_grip": "one_handed",
		"weapon_attack_range": 1,
		"weapon_one_handed_dice": {"dice_count": 1, "dice_sides": 6, "flat_bonus": 0},
		"weapon_physical_damage_tag": "physical_slash",
	})
	var player = _build_manual_unit(&"low_hp_target", "残血玩家", &"player", Vector2i(2, 1), [&"warrior_heavy_strike"])
	player.current_hp = 5
	_add_unit_to_state(runtime, state, wolf, true)
	_add_unit_to_state(runtime, state, player, false)
	var ai_context = _build_ai_context(runtime, wolf)

	var heavy_skill_def = runtime._skill_defs.get(&"warrior_heavy_strike")
	var heavy_command = BATTLE_COMMAND_SCRIPT.new()
	heavy_command.command_type = heavy_command.TYPE_SKILL
	heavy_command.unit_id = wolf.unit_id
	heavy_command.skill_id = &"warrior_heavy_strike"
	heavy_command.target_unit_id = player.unit_id
	heavy_command.target_coord = player.coord
	var heavy_preview = runtime.preview_command(heavy_command)
	_assert_true(heavy_preview != null and heavy_preview.allowed, "melee_aggressor 重击评分前置应满足 runtime preview。")
	var heavy_score = ai_context.build_skill_score_input(
		heavy_skill_def,
		heavy_command,
		heavy_preview,
		heavy_skill_def.combat_profile.effect_defs if heavy_skill_def != null and heavy_skill_def.combat_profile != null else [],
		{"position_target_unit": player, "desired_min_distance": 1, "desired_max_distance": 1}
	)

	var execute_skill_def = runtime._skill_defs.get(&"warrior_execution_cleave")
	var execute_command = BATTLE_COMMAND_SCRIPT.new()
	execute_command.command_type = execute_command.TYPE_SKILL
	execute_command.unit_id = wolf.unit_id
	execute_command.skill_id = &"warrior_execution_cleave"
	execute_command.target_unit_id = player.unit_id
	execute_command.target_coord = player.coord
	var execute_preview = runtime.preview_command(execute_command)
	_assert_true(execute_preview != null and execute_preview.allowed, "melee_aggressor 斩杀评分前置应满足 runtime preview。")
	var execute_score = ai_context.build_skill_score_input(
		execute_skill_def,
		execute_command,
		execute_preview,
		execute_skill_def.combat_profile.effect_defs if execute_skill_def != null and execute_skill_def.combat_profile != null else [],
		{"position_target_unit": player, "desired_min_distance": 1, "desired_max_distance": 1}
	)
	_assert_true(heavy_score != null and execute_score != null, "melee_aggressor 评分回归应拿到两个合法技能候选的评分。")
	if heavy_score == null or execute_score == null:
		return
	_assert_true(
		execute_score.total_score > heavy_score.total_score,
		"残血目标场景下，warrior_execution_cleave 的评分应高于 warrior_heavy_strike。"
	)

	var decision = runtime._ai_service.choose_command(ai_context)
	_assert_true(decision != null and decision.state_id == &"pressure", "melee_aggressor 评分选技回归应保持 pressure 状态。")
	_assert_eq(
		decision.command.skill_id if decision != null and decision.command != null else &"",
		&"warrior_execution_cleave",
		"melee_aggressor 不应再只按 action 顺序选择先声明的 warrior_heavy_strike。"
	)
	_assert_eq(
		decision.action_id if decision != null else &"",
		&"wolf_pressure_execution",
		"melee_aggressor 应能选中后声明但评分更高的技能 action。"
	)


func _test_ranged_controller_prefers_later_higher_score_skill_action() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var source_brain = runtime._enemy_ai_brains.get(&"ranged_controller")
	var brain = source_brain.duplicate(true) if source_brain != null else null
	if brain != null:
		runtime._enemy_ai_brains[brain.brain_id] = brain
		runtime._ai_service.setup(runtime._enemy_ai_brains)
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

	var fireball_skill_def = runtime._skill_defs.get(&"mage_fireball")
	var fireball_command = BATTLE_COMMAND_SCRIPT.new()
	fireball_command.command_type = fireball_command.TYPE_SKILL
	fireball_command.unit_id = mist.unit_id
	fireball_command.skill_id = &"mage_fireball"
	fireball_command.target_coord = player.coord
	var fireball_target_coords: Array[Vector2i] = [player.coord]
	fireball_command.target_coords = fireball_target_coords
	var fireball_preview = runtime.preview_command(fireball_command)
	var fireball_score = ai_context.build_skill_score_input(
		fireball_skill_def,
		fireball_command,
		fireball_preview,
		fireball_skill_def.combat_profile.effect_defs if fireball_skill_def != null and fireball_skill_def.combat_profile != null else [],
		{"desired_min_distance": 3, "desired_max_distance": 4}
	)

	var ice_lance_skill_def = runtime._skill_defs.get(&"mage_ice_lance")
	var ice_lance_command = BATTLE_COMMAND_SCRIPT.new()
	ice_lance_command.command_type = ice_lance_command.TYPE_SKILL
	ice_lance_command.unit_id = mist.unit_id
	ice_lance_command.skill_id = &"mage_ice_lance"
	ice_lance_command.target_unit_id = player.unit_id
	ice_lance_command.target_coord = player.coord
	var ice_lance_preview = runtime.preview_command(ice_lance_command)
	var ice_lance_score = ai_context.build_skill_score_input(
		ice_lance_skill_def,
		ice_lance_command,
		ice_lance_preview,
		ice_lance_skill_def.combat_profile.effect_defs if ice_lance_skill_def != null and ice_lance_skill_def.combat_profile != null else [],
		{"position_target_unit": player, "desired_min_distance": 3, "desired_max_distance": 4}
	)
	_assert_true(fireball_score != null and ice_lance_score != null, "ranged_controller 评分回归应拿到两个合法技能候选的评分。")
	if fireball_score == null or ice_lance_score == null:
		return
	_assert_true(
		ice_lance_score.total_score > fireball_score.total_score,
		"单体目标场景下，mage_ice_lance 的评分应高于 mage_fireball。"
	)

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
	brain.states = {&"pressure": state_def}
	runtime._enemy_ai_brains[brain.brain_id] = brain
	runtime._ai_service.setup(runtime._enemy_ai_brains)

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


func _test_ai_unit_skill_scoring_prefers_higher_hit_payoff_target() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var state = _build_flat_state(Vector2i(7, 5))
	runtime._state = state
	var archer = _build_ai_unit(
		&"score_archer",
		"评分猎手",
		&"hostile",
		Vector2i(1, 2),
		&"ranged_suppressor",
		&"pressure",
		[&"archer_pinning_shot"],
		26,
		2
	)
	var close_tank = _build_manual_unit(&"close_tank", "近处重甲", &"player", Vector2i(2, 2), [&"warrior_heavy_strike"])
	close_tank.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 45)
	var far_scout = _build_manual_unit(&"far_scout", "远处轻甲", &"player", Vector2i(4, 2), [&"warrior_heavy_strike"])
	far_scout.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 8)
	_add_unit_to_state(runtime, state, archer, true)
	_add_unit_to_state(runtime, state, close_tank, false)
	_add_unit_to_state(runtime, state, far_scout, false)
	var ai_context = _build_ai_context(runtime, archer)
	var action = USE_UNIT_SKILL_ACTION_SCRIPT.new()
	action.action_id = &"score_pick_best_target"
	var unit_action_skill_ids: Array[StringName] = [&"archer_pinning_shot"]
	action.skill_ids = unit_action_skill_ids
	action.target_selector = &"nearest_enemy"
	action.desired_min_distance = 0
	action.desired_max_distance = 6
	action.distance_reference = &"target_unit"
	var skill_def = runtime._skill_defs.get(&"archer_pinning_shot")
	var close_command = BATTLE_COMMAND_SCRIPT.new()
	close_command.command_type = close_command.TYPE_SKILL
	close_command.unit_id = archer.unit_id
	close_command.skill_id = &"archer_pinning_shot"
	close_command.target_unit_id = close_tank.unit_id
	close_command.target_coord = close_tank.coord
	var far_command = BATTLE_COMMAND_SCRIPT.new()
	far_command.command_type = far_command.TYPE_SKILL
	far_command.unit_id = archer.unit_id
	far_command.skill_id = &"archer_pinning_shot"
	far_command.target_unit_id = far_scout.unit_id
	far_command.target_coord = far_scout.coord
	var close_preview = runtime.preview_command(close_command)
	var far_preview = runtime.preview_command(far_command)
	var close_score = ai_context.build_skill_score_input(
		skill_def,
		close_command,
		close_preview,
		skill_def.combat_profile.effect_defs if skill_def != null and skill_def.combat_profile != null else [],
		{"position_target_unit": close_tank, "desired_min_distance": 0, "desired_max_distance": 6}
	)
	var far_score = ai_context.build_skill_score_input(
		skill_def,
		far_command,
		far_preview,
		skill_def.combat_profile.effect_defs if skill_def != null and skill_def.combat_profile != null else [],
		{"position_target_unit": far_scout, "desired_min_distance": 0, "desired_max_distance": 6}
	)
	_assert_true(close_score != null and far_score != null, "unit skill score input 应能为多个候选目标生成评分上下文。")
	if close_score == null or far_score == null:
		return
	_assert_true(
		far_score.hit_payoff_score > close_score.hit_payoff_score,
		"更脆弱的远处目标应提供更高的命中收益评分。"
	)
	_assert_true(
		far_score.total_score > close_score.total_score,
		"共享评分上下文应允许高收益目标压过默认最近目标。"
	)
	var decision = action.decide(ai_context)
	_assert_true(decision != null and decision.command != null, "共享 unit score input 后应仍能生成合法指令。")
	_assert_eq(
		decision.command.target_unit_id if decision != null and decision.command != null else &"",
		far_scout.unit_id,
		"UseUnitSkillAction 应根据共享评分上下文选择更高命中收益的目标。"
	)


func _test_ai_unit_skill_action_selects_scoring_variant_id() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var skill_id := &"ai_test_unit_variant_bolt"
	runtime._skill_defs[skill_id] = _build_test_unit_variant_skill(skill_id)
	var state = _build_flat_state(Vector2i(7, 5))
	runtime._state = state
	var caster = _build_ai_unit(
		&"variant_caster",
		"形态施法者",
		&"hostile",
		Vector2i(1, 2),
		&"ranged_suppressor",
		&"pressure",
		[skill_id],
		26,
		2
	)
	var target = _build_manual_unit(&"variant_target", "形态目标", &"player", Vector2i(3, 2), [&"basic_attack"])
	_add_unit_to_state(runtime, state, caster, true)
	_add_unit_to_state(runtime, state, target, false)
	var action = _build_test_unit_variant_action(skill_id)
	var decision = action.decide(_build_ai_context(runtime, caster))
	_assert_true(decision != null and decision.command != null, "unit variant action 应产出合法技能指令。")
	if decision == null or decision.command == null:
		return
	_assert_eq(decision.command.skill_variant_id, &"strong_bolt", "AI 应选择评分最高的 unit variant 并写入 command。")
	_assert_true(decision.score_input != null, "unit variant action 应使用带 variant effect 的评分上下文。")
	if decision.score_input != null:
		_assert_true(int(decision.score_input.estimated_damage) >= 30, "评分应消费所选 strong_bolt variant 的伤害效果。")
	var preview = runtime.preview_command(decision.command)
	_assert_true(preview != null and preview.allowed, "AI 产出的 explicit unit variant 命令必须通过 runtime preview。")


func _test_ai_unit_skill_action_ignores_locked_and_ground_variants() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var skill_id := &"ai_test_unit_variant_filter"
	runtime._skill_defs[skill_id] = _build_test_unit_variant_skill(skill_id, 5, true)
	var state = _build_flat_state(Vector2i(7, 5))
	runtime._state = state
	var caster = _build_ai_unit(
		&"variant_filter_caster",
		"形态过滤者",
		&"hostile",
		Vector2i(1, 2),
		&"ranged_suppressor",
		&"pressure",
		[skill_id],
		26,
		2
	)
	var target = _build_manual_unit(&"variant_filter_target", "形态过滤目标", &"player", Vector2i(3, 2), [&"basic_attack"])
	_add_unit_to_state(runtime, state, caster, true)
	_add_unit_to_state(runtime, state, target, false)
	var action = _build_test_unit_variant_action(skill_id)
	var decision = action.decide(_build_ai_context(runtime, caster))
	_assert_true(decision != null and decision.command != null, "unit variant filter action 应产出合法技能指令。")
	if decision == null or decision.command == null:
		return
	_assert_eq(decision.command.skill_variant_id, &"weak_touch", "AI 应忽略未解锁 unit variant 和 ground variant。")
	_assert_true(decision.score_input != null, "过滤后仍应构造评分上下文。")
	if decision.score_input != null:
		_assert_eq(int(decision.score_input.estimated_damage), 4, "过滤后评分只能来自已解锁 weak_touch unit variant。")
	var preview = runtime.preview_command(decision.command)
	_assert_true(preview != null and preview.allowed, "过滤后的 unit variant 命令必须通过 runtime preview。")


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
	_assert_true(decision != null and decision.command != null, "无 variant 的 unit skill 应继续产出合法指令。")
	if decision == null or decision.command == null:
		return
	_assert_eq(decision.command.skill_variant_id, &"", "无 cast_variants 的 unit skill 应保持空 skill_variant_id。")
	var preview = runtime.preview_command(decision.command)
	_assert_true(preview != null and preview.allowed, "无 variant 的旧 unit skill command 必须保持可 preview。")


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
	var invalid_command = _build_test_unit_skill_command(caster, skill_id, target, &"missing_variant")
	var ground_command = _build_test_unit_skill_command(caster, skill_id, target, &"ground_burst")
	var locked_command = _build_test_unit_skill_command(caster, locked_skill_id, target, &"strong_bolt")
	for command in [ambiguous_command, invalid_command, ground_command, locked_command]:
		var preview = runtime.preview_command(command)
		_assert_true(preview != null and not preview.allowed, "非法、歧义、未解锁或 target_mode 不匹配的 unit variant command 必须被 preview 拒绝。")

	var before_ap: int = int(caster.current_ap)
	var before_hp: int = int(target.current_hp)
	var batch = runtime.issue_command(invalid_command)
	_assert_eq(caster.current_ap, before_ap, "runtime 拒绝 invalid explicit variant 时不应消耗 AP。")
	_assert_eq(target.current_hp, before_hp, "runtime 拒绝 invalid explicit variant 时不应结算目标效果。")
	_assert_true(batch != null and not batch.log_lines.is_empty(), "runtime 拒绝 invalid explicit variant 时应返回阻断日志。")


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
	runtime._ai_service.setup(runtime._enemy_ai_brains)

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
		BATTLE_COMMAND_SCRIPT.TYPE_MOVE,
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


func _build_formal_template_probe_unit(runtime, template_id: StringName):
	if runtime == null or template_id == &"":
		return null
	var encounter_anchor = _build_encounter_anchor(
		StringName("probe_%s" % String(template_id)),
		template_id,
		String(template_id)
	)
	var state = runtime.start_battle(encounter_anchor, 1701, {
		"ally_member_ids": [&"ally_probe"],
		"default_active_skill_ids": [&"basic_attack"],
	})
	if state == null or state.is_empty() or state.enemy_unit_ids.is_empty():
		return null
	return state.units.get(state.enemy_unit_ids[0])


func _resolve_probe_target_distance(runtime, enemy_unit) -> int:
	if runtime == null or enemy_unit == null:
		return 1
	var brain = runtime._enemy_ai_brains.get(enemy_unit.ai_brain_id)
	if brain != null and brain.has_method("get_transition_rules"):
		for rule in brain.get_transition_rules():
			if rule == null or ProgressionDataUtils.to_string_name(rule.rule_id) != &"pressure_enter":
				continue
			for condition in rule.get_conditions():
				if condition == null:
					continue
				if ProgressionDataUtils.to_string_name(condition.predicate) == &"nearest_enemy_distance_at_or_below":
					return clampi(int(condition.max_distance), 1, 7)
	return 1


func _resolve_basic_attack_stamina_cost(runtime) -> int:
	if runtime == null:
		return 5
	var skill_def = runtime._skill_defs.get(&"basic_attack") as SKILL_DEF_SCRIPT
	if skill_def == null or skill_def.combat_profile == null:
		return 5
	var costs: Dictionary = skill_def.combat_profile.get_effective_resource_costs(1)
	return maxi(int(costs.get("stamina_cost", skill_def.combat_profile.stamina_cost)), 0)


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
	ai_context.skill_score_input_callback = Callable(runtime._ai_service, "build_skill_score_input")
	ai_context.action_score_input_callback = Callable(runtime._ai_service, "build_action_score_input")
	ai_context.move_cost_callback = Callable(runtime, "_get_move_cost_for_unit_target")
	ai_context.runtime_action_plan = _resolve_runtime_action_plan(runtime, unit_state)
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
	unit.unlock_combat_resource(BATTLE_UNIT_STATE_SCRIPT.COMBAT_RESOURCE_MP)
	unit.current_stamina = 8
	unit.current_ap = current_ap
	unit.is_alive = true
	unit.set_anchor_coord(coord)
	unit.attribute_snapshot.set_value(&"hp_max", maxi(current_hp, 24))
	unit.attribute_snapshot.set_value(&"mp_max", 120)
	unit.attribute_snapshot.set_value(&"stamina_max", 8)
	unit.attribute_snapshot.set_value(&"action_points", maxi(current_ap, 2))
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 12)
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
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 6)
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
	include_ground_variant: bool = false
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

	var weak_variant := COMBAT_CAST_VARIANT_DEF_SCRIPT.new()
	weak_variant.variant_id = &"weak_touch"
	weak_variant.display_name = "弱触"
	weak_variant.target_mode = &"unit"
	weak_variant.min_skill_level = 0
	weak_variant.effect_defs = [_build_test_damage_effect(4)]

	var strong_variant := COMBAT_CAST_VARIANT_DEF_SCRIPT.new()
	strong_variant.variant_id = &"strong_bolt"
	strong_variant.display_name = "强击"
	strong_variant.target_mode = &"unit"
	strong_variant.min_skill_level = strong_min_skill_level
	strong_variant.effect_defs = [_build_test_damage_effect(30)]

	skill_def.combat_profile.cast_variants = [weak_variant, strong_variant]
	if include_ground_variant:
		var ground_variant := COMBAT_CAST_VARIANT_DEF_SCRIPT.new()
		ground_variant.variant_id = &"ground_burst"
		ground_variant.display_name = "地爆"
		ground_variant.target_mode = &"ground"
		ground_variant.min_skill_level = 0
		ground_variant.footprint_pattern = &"single"
		ground_variant.required_coord_count = 1
		ground_variant.effect_defs = [_build_test_damage_effect(50)]
		skill_def.combat_profile.cast_variants.append(ground_variant)
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
	var command = BATTLE_COMMAND_SCRIPT.new()
	command.command_type = command.TYPE_SKILL
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
	var command = BATTLE_COMMAND_SCRIPT.new()
	command.command_type = command.TYPE_SKILL
	command.unit_id = source_unit.unit_id if source_unit != null else &""
	command.skill_id = skill_id
	command.target_coord = target_coord
	var target_coords: Array[Vector2i] = [target_coord]
	command.target_coords = target_coords
	return command


func _build_test_unit_skill_command(source_unit, skill_id: StringName, target_unit, skill_variant_id: StringName = &""):
	var command = BATTLE_COMMAND_SCRIPT.new()
	command.command_type = command.TYPE_SKILL
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
	unit.unlock_combat_resource(BATTLE_UNIT_STATE_SCRIPT.COMBAT_RESOURCE_AURA)
	unit.attribute_snapshot.set_value(&"stamina_max", 120)
	unit.attribute_snapshot.set_value(&"aura_max", 140)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 30)
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


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
