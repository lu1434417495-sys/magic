extends SceneTree

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/persistence/game_session.gd")
const GAME_RUNTIME_FACADE_SCRIPT = preload("res://scripts/systems/game_runtime/game_runtime_facade.gd")
const BATTLE_RUNTIME_MODULE_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_runtime_module.gd")
const BATTLE_AI_CONTEXT_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_context.gd")
const BATTLE_AI_SCORE_SERVICE_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_score_service.gd")
const BATTLE_COMMAND_SCRIPT = preload("res://scripts/systems/battle/core/battle_command.gd")
const BATTLE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_state.gd")
const BATTLE_TIMELINE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_timeline_state.gd")
const BATTLE_CELL_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const BATTLE_STATUS_EFFECT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_status_effect_state.gd")
const COMBAT_EFFECT_DEF_SCRIPT = preload("res://scripts/player/progression/combat_effect_def.gd")
const COMBAT_SKILL_DEF_SCRIPT = preload("res://scripts/player/progression/combat_skill_def.gd")
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
const MOVE_TO_MULTI_UNIT_SKILL_POSITION_ACTION_SCRIPT = preload("res://scripts/enemies/actions/move_to_multi_unit_skill_position_action.gd")
const USE_MULTI_UNIT_SKILL_ACTION_SCRIPT = preload("res://scripts/enemies/actions/use_multi_unit_skill_action.gd")
const USE_UNIT_SKILL_ACTION_SCRIPT = preload("res://scripts/enemies/actions/use_unit_skill_action.gd")
const WAIT_ACTION_SCRIPT = preload("res://scripts/enemies/actions/wait_action.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const BattleDamageResolver = preload("res://scripts/systems/battle/rules/battle_damage_resolver.gd")

const TEST_WORLD_CONFIG := "res://data/configs/world_map/test_world_map_config.tres"

var _failures: Array[String] = []


class TrapDamageResolver:
	extends BattleDamageResolver

	var resolve_effects_calls := 0

	func resolve_effects(source_unit, target_unit, effect_defs: Array, damage_context: Dictionary = {}) -> Dictionary:
		resolve_effects_calls += 1
		return super.resolve_effects(source_unit, target_unit, effect_defs, damage_context)


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
	_test_battle_unit_factory_fallback_basic_attack_is_affordable()
	_test_formal_enemy_templates_have_real_pressure_skill_action()
	_test_depleted_ranged_templates_close_for_basic_attack_fallback()
	_test_enemy_template_does_not_resolve_display_name_alias()
	_test_natural_weapon_melee_aggressor_falls_back_to_basic_attack()
	_test_ai_charge_decision_logs_brain_state_action()
	_test_frontline_bulwark_charge_decision_logs_brain_state_action()
	_test_short_regular_move_prefers_close_in_over_charge()
	_test_melee_close_in_prefers_screening_ranged_ally_when_healthy()
	_test_melee_screening_scores_actual_path_cost_block()
	_test_melee_screening_ignores_geometric_line_without_pressure()
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
	_test_ai_ground_skill_scores_role_threat_area_targets()
	_test_ai_multi_unit_skill_prefers_max_targets_under_candidate_limit()
	_test_ai_multi_unit_positioning_moves_toward_max_targets()
	_test_ai_skill_distance_contract_uses_effective_weapon_range()
	_test_ai_move_to_range_uses_effective_weapon_range()
	_test_ranged_archer_survival_position_beats_shot_when_too_close()
	_test_ranged_archer_survival_position_uses_enemy_threat_range()
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


func _test_battle_unit_factory_fallback_basic_attack_is_affordable() -> void:
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
	_assert_true(not enemy_units.is_empty(), "BattleUnitFactory fallback 路径应能构建敌人单位。")
	if enemy_units.is_empty():
		game_session.free()
		return
	var enemy_unit = enemy_units[0]
	var basic_stamina_cost := _resolve_basic_attack_stamina_cost(runtime)
	_assert_true(enemy_unit.known_active_skill_ids.has(&"basic_attack"), "BattleUnitFactory fallback 敌人应持有 basic_attack。")
	_assert_true(
		int(enemy_unit.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX)) >= basic_stamina_cost,
		"BattleUnitFactory fallback 敌人的 stamina_max 应至少支付 basic_attack。"
	)
	_assert_true(
		int(enemy_unit.current_stamina) >= basic_stamina_cost,
		"BattleUnitFactory fallback 敌人的 current_stamina 应至少支付 basic_attack。"
	)
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
	vanguard.current_move_points = 0
	vanguard.current_stamina = 80
	vanguard.attribute_snapshot.set_value(&"stamina_max", 80)
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
	action.screening_min_hp_ratio = 0.4
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
	action.screening_min_hp_ratio = 0.4
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
	action.screening_min_hp_ratio = 0.4
	var screening_context: Dictionary = action._build_screening_context(ai_context)
	var metrics: Dictionary = action._build_screening_metrics(ai_context, Vector2i(3, 5), screening_context)
	_assert_true(bool(screening_context.get("enabled", false)), "敌方近战可威胁弓手时，screening context 应启用。")
	_assert_eq(
		int(metrics.get("bonus", 0)),
		0,
		"仅处于几何最短路但不增加路径成本、也不能贴身/反击的格子不应获得守线加分。"
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
	var plan: Dictionary = runtime._ai_action_assembler.build_unit_action_plan(spinner, brain, runtime._skill_defs)
	var engage_actions: Array = plan.get(&"engage", [])
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
	caster.current_mp = 20
	caster.attribute_snapshot.set_value(&"mp_max", 20)
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
	caster.current_mp = 20
	caster.attribute_snapshot.set_value(&"mp_max", 20)
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
	var trap_damage_resolver := TrapDamageResolver.new()
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
	formal_effect.damage_ratio_percent = 200
	formal_effect.params = {"hp_ratio_threshold": 0.7}
	var formal_multiplier := score_service._resolve_effect_damage_multiplier(formal_effect, target)
	_assert_eq(int(round(formal_multiplier * 100.0)), 200, "AI 评分应读取正式 hp_ratio_threshold 计算低血倍率。")

	var legacy_effect = COMBAT_EFFECT_DEF_SCRIPT.new()
	legacy_effect.effect_type = &"damage"
	legacy_effect.bonus_condition = &"target_low_hp"
	legacy_effect.damage_ratio_percent = 200
	legacy_effect.params = {"low_hp_ratio": 0.7}
	var legacy_multiplier := score_service._resolve_effect_damage_multiplier(legacy_effect, target)
	_assert_eq(int(round(legacy_multiplier * 100.0)), 100, "AI 评分不应再读取旧 low_hp_ratio alias。")


func _test_melee_aggressor_prefers_later_higher_score_skill_action() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var brain = runtime._enemy_ai_brains.get(&"melee_aggressor")
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
	var brain = runtime._enemy_ai_brains.get(&"ranged_controller")
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
	brain.pressure_distance = 99
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


func _test_move_to_range_prefers_closing_distance_over_wait_when_far_from_band() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var brain = ENEMY_AI_BRAIN_DEF_SCRIPT.new()
	brain.brain_id = &"far_gap_mover_brain"
	brain.default_state_id = &"engage"
	brain.pressure_distance = 0
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
	brain.pressure_distance = 99
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
	brain.pressure_distance = 99
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
	if brain != null and int(brain.pressure_distance) > 0:
		return clampi(int(brain.pressure_distance), 1, 7)
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
	ai_context.runtime_actions_by_state = runtime._ai_action_plans_by_unit_id.get(unit_state.unit_id, {}) if unit_state != null else {}
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
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 10)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 12)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 4)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 4)
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
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 10)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 6)
	unit.known_active_skill_ids = skill_ids.duplicate()
	for skill_id in unit.known_active_skill_ids:
		unit.known_skill_level_map[skill_id] = 1
	return unit


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
	state.units[unit.unit_id] = unit
	if is_enemy:
		state.enemy_unit_ids.append(unit.unit_id)
	else:
		state.ally_unit_ids.append(unit.unit_id)
	var placed = runtime._grid_service.place_unit(state, unit, unit.coord, true)
	_assert_true(placed, "测试单位 %s 应能放入测试战场。" % String(unit.unit_id))


func _errors_contain_fragment(errors: Array[String], fragment: String) -> bool:
	for error in errors:
		if String(error).contains(fragment):
			return true
	return false


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
