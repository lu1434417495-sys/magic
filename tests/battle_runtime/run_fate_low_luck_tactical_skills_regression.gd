extends SceneTree

const BattleCommand = preload("res://scripts/systems/battle/core/battle_command.gd")
const BattleCellState = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BattleDamageResolver = preload("res://scripts/systems/battle/rules/battle_damage_resolver.gd")
const BattleEventBatch = preload("res://scripts/systems/battle/core/battle_event_batch.gd")
const BattleFateEventBus = preload("res://scripts/systems/battle/fate/battle_fate_event_bus.gd")
const BattleResolutionResult = preload("res://scripts/systems/battle/core/battle_resolution_result.gd")
const BattleRuntimeModule = preload("res://scripts/systems/battle/runtime/battle_runtime_module.gd")
const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleStatusEffectState = preload("res://scripts/systems/battle/core/battle_status_effect_state.gd")
const BattleTimelineState = preload("res://scripts/systems/battle/core/battle_timeline_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const CharacterManagementModule = preload("res://scripts/systems/progression/character_management_module.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const LowLuckEventService = preload("res://scripts/systems/battle/fate/low_luck_event_service.gd")
const PartyMemberState = preload("res://scripts/player/progression/party_member_state.gd")
const PartyState = preload("res://scripts/player/progression/party_state.gd")
const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")
const ProgressionDataUtils = preload("res://scripts/player/progression/progression_data_utils.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")
const DETERMINISTIC_BATTLE_DAMAGE_RESOLVER_SCRIPT = preload("res://tests/battle_runtime/helpers/deterministic_battle_damage_resolver.gd")
const DETERMINISTIC_BATTLE_HIT_RESOLVER_SCRIPT = preload("res://tests/battle_runtime/helpers/deterministic_battle_hit_resolver.gd")

const HERO_ID: StringName = &"hero"
const MISSTEP_TO_SCHEME_SKILL_ID: StringName = &"misstep_to_scheme"
const BLACK_CONTRACT_PUSH_SKILL_ID: StringName = &"black_contract_push"
const DOOM_SHIFT_SKILL_ID: StringName = &"doom_shift"
const BLACK_CROWN_SEAL_SKILL_ID: StringName = &"black_crown_seal"
const WARRIOR_HEAVY_STRIKE_SKILL_ID: StringName = &"warrior_heavy_strike"
const SAINT_BLADE_COMBO_SKILL_ID: StringName = &"saint_blade_combo"
const MAGE_ARCANE_MISSILE_SKILL_ID: StringName = &"mage_arcane_missile"
const ARCHER_MULTISHOT_SKILL_ID: StringName = &"archer_multishot"
const BLOOD_TITHE_VARIANT_ID: StringName = &"blood_tithe"
const GUARD_TITHE_VARIANT_ID: StringName = &"guard_tithe"
const ACTION_TITHE_VARIANT_ID: StringName = &"action_tithe"
const COUNTERATTACK_LOCK_VARIANT_ID: StringName = &"counterattack_lock"
const CRIT_LOCK_VARIANT_ID: StringName = &"crit_lock"
const ARCHER_MULTISHOT_VARIANT_ID: StringName = &"multishot_volley"
const STATUS_GUARDING: StringName = &"guarding"
const STATUS_STAGGERED: StringName = &"staggered"
const STATUS_MARKED: StringName = &"marked"
const STATUS_BLACK_CROWN_SEAL_COUNTERATTACK: StringName = &"black_crown_seal_counterattack"
const STATUS_BLACK_CROWN_SEAL_CRIT: StringName = &"black_crown_seal_crit"
const FORTUNE_MARK_TARGET_STAT_ID: StringName = &"fortune_mark_target"
const BOSS_TARGET_STAT_ID: StringName = &"boss_target"
const BLACK_CONTRACT_PUSH_HP_COST := 10

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_misstep_to_scheme_grants_bonus_calamity_without_duplicate_critical_fail_events()
	_test_black_contract_push_variants_pay_their_selected_cost_and_force_hit_without_crit()
	_test_doom_shift_marks_self_and_swaps_with_nearby_ally()
	_test_black_crown_seal_is_boss_only_once_per_battle_and_applies_both_lock_variants()

	if _failures.is_empty():
		print("FATE_25 regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("FATE_25 regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_misstep_to_scheme_grants_bonus_calamity_without_duplicate_critical_fail_events() -> void:
	var runtime := _build_runtime()
	var state := _build_skill_test_state(&"fate_25_misstep", Vector2i(5, 4))
	var hero := _build_unit(&"misstep_hero", "倒霉先锋", &"player", Vector2i(1, 1), 1, HERO_ID)
	hero.known_skill_level_map = {MISSTEP_TO_SCHEME_SKILL_ID: 1}
	_add_unit(runtime, state, hero)
	state.ally_unit_ids = [hero.unit_id]
	state.active_unit_id = hero.unit_id
	runtime._state = state
	_begin_runtime_battle(runtime)

	var low_luck_context := _build_low_luck_context(-5, runtime.get_fate_event_bus())
	var low_luck_service := low_luck_context.get("service") as LowLuckEventService
	if low_luck_service == null:
		_assert_true(false, "失手成筹前置失败：LowLuckEventService 未初始化。")
		return

	var seen_events: Array[StringName] = []
	runtime.get_fate_event_bus().event_dispatched.connect(func(event_type: StringName, _payload: Dictionary) -> void:
		seen_events.append(event_type)
	)
	runtime.get_fate_event_bus().dispatch(
		BattleFateEventBus.EVENT_CRITICAL_FAIL,
		_build_critical_fail_payload(state.battle_id, HERO_ID, hero.unit_id, -5)
	)

	_assert_eq(runtime.get_member_calamity(HERO_ID), 2, "失手成筹应让首次大失败额外获得 1 点 calamity。")
	_assert_eq(
		_seen_event_count(seen_events, BattleFateEventBus.EVENT_CRITICAL_FAIL),
		1,
		"失手成筹不应额外重复派发 critical_fail 事件。"
	)

	var low_luck_result := low_luck_service.handle_battle_resolution(state, _build_battle_resolution_result(state.battle_id))
	var triggered_event_ids := ProgressionDataUtils.to_string_name_array(low_luck_result.get("triggered_event_ids", []))
	_assert_true(
		triggered_event_ids.has(LowLuckEventService.EVENT_BORROWED_ROAD),
		"失手成筹不应冲掉 Borrowed Road 的大失败计数。"
	)
	runtime.dispose()


func _test_normal_attack_skill_critical_fail_keeps_fate_report_and_event() -> void:
	var runtime := _build_runtime()
	var skill_def := runtime.get_skill_defs().get(WARRIOR_HEAVY_STRIKE_SKILL_ID) as SkillDef
	_assert_true(skill_def != null and skill_def.combat_profile != null, "普通攻击 fate 路由前置：warrior_heavy_strike 定义应存在。")
	if skill_def == null or skill_def.combat_profile == null:
		runtime.dispose()
		return

	var state := _build_skill_test_state(&"fate_25_normal_attack_route", Vector2i(6, 4))
	var caster := _build_unit(&"route_caster", "倒霉战士", &"player", Vector2i(1, 1), 2, HERO_ID)
	caster.known_active_skill_ids = [WARRIOR_HEAVY_STRIKE_SKILL_ID]
	caster.known_skill_level_map = {WARRIOR_HEAVY_STRIKE_SKILL_ID: 1}
	caster.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.HIDDEN_LUCK_AT_BIRTH, -5)
	var enemy := _build_unit(&"route_enemy", "演习木桩", &"enemy", Vector2i(2, 1), 1)
	enemy.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	_add_unit(runtime, state, caster)
	_add_unit(runtime, state, enemy)
	state.ally_unit_ids = [caster.unit_id]
	state.enemy_unit_ids = [enemy.unit_id]
	state.active_unit_id = caster.unit_id
	runtime._state = state
	_begin_runtime_battle(runtime)

	var seed := _find_unit_skill_seed_for_resolution(
		runtime,
		state,
		caster,
		enemy,
		skill_def,
		[BattleFateEventBus.EVENT_CRITICAL_FAIL]
	)
	_assert_true(seed >= 0, "普通攻击 fate 路由前置：应能找到触发 critical_fail 的稳定 seed。")
	if seed < 0:
		runtime.dispose()
		return
	_begin_runtime_battle(runtime)
	state.seed = seed
	state.attack_roll_nonce = 0

	var seen_events: Array[StringName] = []
	runtime.get_fate_event_bus().event_dispatched.connect(func(event_type: StringName, _payload: Dictionary) -> void:
		seen_events.append(event_type)
	)
	var batch := runtime.issue_command(_build_unit_skill_command(caster.unit_id, WARRIOR_HEAVY_STRIKE_SKILL_ID, enemy))

	_assert_true(state.attack_roll_nonce > 0, "普通攻击走 fate 结算后应消耗真随机攻击骰。")
	_assert_eq(enemy.current_hp, 60, "普通攻击触发 critical_fail 时不应对目标造成伤害。")
	_assert_true(not enemy.has_status_effect(&"armor_break"), "普通攻击触发 critical_fail 时不应附加 on-hit debuff。")
	_assert_eq(
		_seen_event_count(seen_events, BattleFateEventBus.EVENT_CRITICAL_FAIL),
		1,
		"普通攻击触发 critical_fail 后应向 fate bus 派发事件。"
	)
	_assert_true(
		_has_fate_attack_report_entry_with_tag(batch.report_entries, BattleFateEventBus.EVENT_CRITICAL_FAIL),
		"普通攻击触发 critical_fail 后应保留 fate attack report entry。"
	)
	_assert_log_contains(batch.log_lines, "命运判定", "普通攻击触发 critical_fail 后应在 battle log 中写入命运判定文案。")
	runtime.dispose()


func _test_repeat_attack_skill_critical_fail_keeps_fate_report_and_event() -> void:
	var runtime := _build_runtime()
	var skill_def := runtime.get_skill_defs().get(SAINT_BLADE_COMBO_SKILL_ID) as SkillDef
	_assert_true(skill_def != null and skill_def.combat_profile != null, "连击 fate 路由前置：saint_blade_combo 定义应存在。")
	if skill_def == null or skill_def.combat_profile == null:
		runtime.dispose()
		return

	var repeat_effect: CombatEffectDef = _get_effect_def(skill_def.combat_profile.effect_defs, &"repeat_attack_until_fail")
	_assert_true(repeat_effect != null, "连击 fate 路由前置：saint_blade_combo 应声明 repeat_attack_until_fail。")
	if repeat_effect == null:
		runtime.dispose()
		return

	var state := _build_skill_test_state(&"fate_25_repeat_attack_route", Vector2i(6, 4))
	var caster := _build_unit(&"repeat_route_caster", "倒霉剑士", &"player", Vector2i(1, 1), 2, HERO_ID)
	caster.current_aura = 1
	caster.known_active_skill_ids = [SAINT_BLADE_COMBO_SKILL_ID]
	caster.known_skill_level_map = {SAINT_BLADE_COMBO_SKILL_ID: 1}
	caster.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.HIDDEN_LUCK_AT_BIRTH, -5)
	var enemy := _build_unit(&"repeat_route_enemy", "连击木桩", &"enemy", Vector2i(2, 1), 1)
	enemy.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	_add_unit(runtime, state, caster)
	_add_unit(runtime, state, enemy)
	state.ally_unit_ids = [caster.unit_id]
	state.enemy_unit_ids = [enemy.unit_id]
	state.active_unit_id = caster.unit_id
	runtime._state = state
	_begin_runtime_battle(runtime)

	var seed := _find_repeat_attack_stage_seed_for_resolution(
		runtime,
		state,
		caster,
		enemy,
		skill_def,
		repeat_effect,
		0,
		[BattleFateEventBus.EVENT_CRITICAL_FAIL]
	)
	_assert_true(seed >= 0, "连击 fate 路由前置：应能找到首段触发 critical_fail 的稳定 seed。")
	if seed < 0:
		runtime.dispose()
		return
	_begin_runtime_battle(runtime)
	state.seed = seed
	state.attack_roll_nonce = 0

	var seen_events: Array[StringName] = []
	runtime.get_fate_event_bus().event_dispatched.connect(func(event_type: StringName, _payload: Dictionary) -> void:
		seen_events.append(event_type)
	)
	var batch := runtime.issue_command(_build_unit_skill_command(caster.unit_id, SAINT_BLADE_COMBO_SKILL_ID, enemy))

	_assert_true(state.attack_roll_nonce > 0, "连击实际执行应走 fate 结算并消耗真随机攻击骰。")
	_assert_eq(enemy.current_hp, 60, "连击首段触发 critical_fail 时不应对目标造成伤害。")
	_assert_eq(
		_seen_event_count(seen_events, BattleFateEventBus.EVENT_CRITICAL_FAIL),
		1,
		"连击首段触发 critical_fail 后应向 fate bus 派发事件。"
	)
	_assert_true(
		_has_fate_attack_report_entry_with_tag(batch.report_entries, BattleFateEventBus.EVENT_CRITICAL_FAIL),
		"连击首段触发 critical_fail 后应保留 fate attack report entry。"
	)
	_assert_log_contains(batch.log_lines, "命运判定", "连击首段触发 critical_fail 后应在 battle log 中写入命运判定文案。")
	runtime.dispose()


func _test_multi_unit_damage_skill_routes_through_fate_attack_resolution() -> void:
	var runtime := _build_runtime()
	var skill_def := runtime.get_skill_defs().get(MAGE_ARCANE_MISSILE_SKILL_ID) as SkillDef
	_assert_true(skill_def != null and skill_def.combat_profile != null, "多目标 fate 路由前置：mage_arcane_missile 定义应存在。")
	if skill_def == null or skill_def.combat_profile == null:
		runtime.dispose()
		return

	var state := _build_skill_test_state(&"fate_25_multi_unit_route", Vector2i(7, 4))
	var caster := _build_unit(&"multi_route_caster", "厄运术士", &"player", Vector2i(1, 1), 1, HERO_ID)
	caster.known_active_skill_ids = [MAGE_ARCANE_MISSILE_SKILL_ID]
	caster.known_skill_level_map = {MAGE_ARCANE_MISSILE_SKILL_ID: 1}
	caster.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.HIDDEN_LUCK_AT_BIRTH, -5)
	var first_enemy := _build_unit(&"multi_route_enemy_a", "前排敌人", &"enemy", Vector2i(3, 1), 1)
	first_enemy.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	var second_enemy := _build_unit(&"multi_route_enemy_b", "后排敌人", &"enemy", Vector2i(4, 1), 1)
	second_enemy.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	_add_unit(runtime, state, caster)
	_add_unit(runtime, state, first_enemy)
	_add_unit(runtime, state, second_enemy)
	state.ally_unit_ids = [caster.unit_id]
	state.enemy_unit_ids = [first_enemy.unit_id, second_enemy.unit_id]
	state.active_unit_id = caster.unit_id
	runtime._state = state
	_begin_runtime_battle(runtime)

	var seed := _find_unit_skill_seed_for_resolution(
		runtime,
		state,
		caster,
		first_enemy,
		skill_def,
		[BattleFateEventBus.EVENT_CRITICAL_FAIL, BattleFateEventBus.EVENT_ORDINARY_MISS]
	)
	_assert_true(seed >= 0, "多目标 fate 路由前置：应能找到首目标触发 miss/fumble 的稳定 seed。")
	if seed < 0:
		runtime.dispose()
		return
	_begin_runtime_battle(runtime)
	state.seed = seed
	state.attack_roll_nonce = 0

	var seen_events: Array[StringName] = []
	runtime.get_fate_event_bus().event_dispatched.connect(func(event_type: StringName, _payload: Dictionary) -> void:
		seen_events.append(event_type)
	)
	var batch := runtime.issue_command(_build_multi_unit_skill_command(
		caster.unit_id,
		MAGE_ARCANE_MISSILE_SKILL_ID,
		[first_enemy, second_enemy]
	))

	_assert_true(state.attack_roll_nonce > 0, "多目标伤害技能走 fate 结算后应消耗真随机攻击骰。")
	_assert_eq(first_enemy.current_hp, 60, "多目标技能首目标触发 miss/fumble 时不应吃到伤害。")
	_assert_true(
		_seen_event_count(seen_events, BattleFateEventBus.EVENT_CRITICAL_FAIL) > 0
			or _seen_event_count(seen_events, BattleFateEventBus.EVENT_ORDINARY_MISS) > 0,
		"多目标伤害技能应为各目标派发命运攻击事件。"
	)
	_assert_true(
		_has_fate_attack_report_entry(batch.report_entries),
		"多目标伤害技能应为 miss/fumble 目标写入 fate attack report entry。"
	)
	runtime.dispose()


func _test_ground_variant_multi_unit_skill_routes_through_fate_attack_resolution() -> void:
	var runtime := _build_runtime()
	var skill_def := runtime.get_skill_defs().get(ARCHER_MULTISHOT_SKILL_ID) as SkillDef
	_assert_true(skill_def != null and skill_def.combat_profile != null, "混合多目标 fate 路由前置：archer_multishot 定义应存在。")
	if skill_def == null or skill_def.combat_profile == null:
		runtime.dispose()
		return
	var cast_variant = skill_def.combat_profile.get_cast_variant(ARCHER_MULTISHOT_VARIANT_ID)
	_assert_true(cast_variant != null, "混合多目标 fate 路由前置：multishot_volley 施法变体应存在。")
	if cast_variant == null:
		runtime.dispose()
		return

	var state := _build_skill_test_state(&"fate_25_ground_variant_multi_unit_route", Vector2i(8, 4))
	var caster := _build_unit(&"ground_route_caster", "厄运猎手", &"player", Vector2i(1, 1), 3, HERO_ID)
	caster.known_active_skill_ids = [ARCHER_MULTISHOT_SKILL_ID]
	caster.known_skill_level_map = {ARCHER_MULTISHOT_SKILL_ID: 1}
	caster.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.HIDDEN_LUCK_AT_BIRTH, -5)
	caster.current_stamina = 20
	var first_enemy := _build_unit(&"ground_route_enemy_a", "左翼敌人", &"enemy", Vector2i(2, 1), 1)
	first_enemy.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	var second_enemy := _build_unit(&"ground_route_enemy_b", "中路敌人", &"enemy", Vector2i(3, 1), 1)
	second_enemy.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	var third_enemy := _build_unit(&"ground_route_enemy_c", "右翼敌人", &"enemy", Vector2i(4, 1), 1)
	third_enemy.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	_add_unit(runtime, state, caster)
	_add_unit(runtime, state, first_enemy)
	_add_unit(runtime, state, second_enemy)
	_add_unit(runtime, state, third_enemy)
	state.ally_unit_ids = [caster.unit_id]
	state.enemy_unit_ids = [first_enemy.unit_id, second_enemy.unit_id, third_enemy.unit_id]
	state.active_unit_id = caster.unit_id
	runtime._state = state
	_begin_runtime_battle(runtime)

	var seed := _find_unit_skill_seed_for_resolution(
		runtime,
		state,
		caster,
		first_enemy,
		skill_def,
		[BattleFateEventBus.EVENT_CRITICAL_FAIL, BattleFateEventBus.EVENT_ORDINARY_MISS],
		cast_variant
	)
	_assert_true(seed >= 0, "混合多目标 fate 路由前置：应能找到首目标触发 miss/fumble 的稳定 seed。")
	if seed < 0:
		runtime.dispose()
		return
	_begin_runtime_battle(runtime)
	state.seed = seed
	state.attack_roll_nonce = 0

	var seen_events: Array[StringName] = []
	runtime.get_fate_event_bus().event_dispatched.connect(func(event_type: StringName, _payload: Dictionary) -> void:
		seen_events.append(event_type)
	)
	var batch := runtime.issue_command(_build_multi_unit_skill_command(
		caster.unit_id,
		ARCHER_MULTISHOT_SKILL_ID,
		[first_enemy, second_enemy, third_enemy],
		ARCHER_MULTISHOT_VARIANT_ID
	))

	_assert_true(state.attack_roll_nonce > 0, "ground 变体多目标点射走 fate 结算后应消耗真随机攻击骰。")
	_assert_eq(first_enemy.current_hp, 60, "ground 变体多目标点射首目标触发 miss/fumble 时不应吃到伤害。")
	_assert_true(
		_seen_event_count(seen_events, BattleFateEventBus.EVENT_CRITICAL_FAIL) > 0
			or _seen_event_count(seen_events, BattleFateEventBus.EVENT_ORDINARY_MISS) > 0,
		"ground 变体多目标点射应为各目标派发命运攻击事件。"
	)
	_assert_true(
		_has_fate_attack_report_entry(batch.report_entries),
		"ground 变体多目标点射应为 miss/fumble 目标写入 fate attack report entry。"
	)
	runtime.dispose()


func _test_black_contract_push_variants_pay_their_selected_cost_and_force_hit_without_crit() -> void:
	var blood_case := _issue_black_contract_push_case(BLOOD_TITHE_VARIANT_ID)
	_assert_force_hit_preview(
		blood_case.get("preview"),
		"黑契推进·血契 preview 应按必定命中暴露给指令与 AI 评分。"
	)
	_assert_forced_hit_no_crit(
		blood_case.get("simulated_result", {}),
		"黑契推进·血契应改为必定命中且不会暴击。"
	)
	_assert_true(
		int((blood_case.get("enemy") as BattleUnitState).current_hp) < 60,
		"黑契推进·血契命中后应对目标造成伤害。"
	)
	_assert_eq(
		int((blood_case.get("caster") as BattleUnitState).current_hp),
		28 - BLACK_CONTRACT_PUSH_HP_COST,
		"黑契推进·血契应先扣除固定生命代价。"
	)
	_assert_log_contains(
		(blood_case.get("batch") as BattleEventBatch).log_lines,
		"必定命中，且不会触发暴击",
		"黑契推进·血契应在 battle log 中回显强制命中语义。"
	)
	(blood_case.get("runtime") as BattleRuntimeModule).dispose()

	var guard_case := _issue_black_contract_push_case(GUARD_TITHE_VARIANT_ID)
	_assert_force_hit_preview(
		guard_case.get("preview"),
		"黑契推进·护契 preview 应按必定命中暴露给指令与 AI 评分。"
	)
	_assert_forced_hit_no_crit(
		guard_case.get("simulated_result", {}),
		"黑契推进·护契应改为必定命中且不会暴击。"
	)
	_assert_true(
		not (guard_case.get("caster") as BattleUnitState).has_status_effect(STATUS_GUARDING),
		"黑契推进·护契成功后应移除施法者的 Guard。"
	)
	_assert_true(
		int((guard_case.get("enemy") as BattleUnitState).current_hp) < 60,
		"黑契推进·护契命中后应对目标造成伤害。"
	)
	(guard_case.get("runtime") as BattleRuntimeModule).dispose()

	var action_case := _issue_black_contract_push_case(ACTION_TITHE_VARIANT_ID)
	_assert_force_hit_preview(
		action_case.get("preview"),
		"黑契推进·行契 preview 应按必定命中暴露给指令与 AI 评分。"
	)
	var action_runtime := action_case.get("runtime") as BattleRuntimeModule
	var action_caster := action_case.get("caster") as BattleUnitState
	_assert_forced_hit_no_crit(
		action_case.get("simulated_result", {}),
		"黑契推进·行契应改为必定命中且不会暴击。"
	)
	_assert_true(
		action_caster.has_status_effect(STATUS_STAGGERED),
		"黑契推进·行契成功后应为自己挂上 staggered。"
	)
	action_caster.current_ap = 2
	action_runtime._apply_turn_start_statuses(action_caster, BattleEventBatch.new())
	_assert_eq(action_caster.current_ap, 1, "黑契推进·行契应让施法者下一回合少 1 点行动点。")
	_assert_true(
		int((action_case.get("enemy") as BattleUnitState).current_hp) < 60,
		"黑契推进·行契命中后应对目标造成伤害。"
	)
	action_runtime.dispose()


func _test_doom_shift_marks_self_and_swaps_with_nearby_ally() -> void:
	var runtime := _build_runtime()
	var state := _build_skill_test_state(&"fate_25_doom_shift", Vector2i(6, 4))
	var caster := _build_unit(&"doom_shift_caster", "断命者", &"player", Vector2i(1, 1), 1, HERO_ID)
	caster.known_active_skill_ids = [DOOM_SHIFT_SKILL_ID]
	caster.known_skill_level_map = {DOOM_SHIFT_SKILL_ID: 1}
	var ally := _build_unit(&"doom_shift_ally", "护卫", &"player", Vector2i(3, 1), 1, &"ally")
	_add_unit(runtime, state, caster)
	_add_unit(runtime, state, ally)
	state.ally_unit_ids = [caster.unit_id, ally.unit_id]
	state.active_unit_id = caster.unit_id
	runtime._state = state
	_begin_runtime_battle(runtime)

	var illegal_preview := runtime.preview_command(_build_unit_skill_command(caster.unit_id, DOOM_SHIFT_SKILL_ID, caster))
	_assert_true(
		illegal_preview != null and not illegal_preview.allowed,
		"断命换位不应允许以自己为目标。"
	)

	var origin_coord := caster.coord
	var ally_coord := ally.coord
	var batch := runtime.issue_command(_build_unit_skill_command(caster.unit_id, DOOM_SHIFT_SKILL_ID, ally))
	_assert_true(caster.has_status_effect(STATUS_MARKED), "断命换位成功后应给施法者写入 marked。")
	_assert_eq(caster.coord, ally_coord, "断命换位应把施法者送到队友原位置。")
	_assert_eq(ally.coord, origin_coord, "断命换位应把队友换到施法者原位置。")
	_assert_log_contains(batch.log_lines, "交换位置", "断命换位应在 battle log 中说明换位结果。")
	runtime.dispose()


func _test_black_crown_seal_is_boss_only_once_per_battle_and_applies_both_lock_variants() -> void:
	var counter_case := _build_black_crown_seal_case(&"fate_25_black_crown_counter")
	var counter_runtime := counter_case.get("runtime") as BattleRuntimeModule
	var counter_caster := counter_case.get("caster") as BattleUnitState
	var boss := counter_case.get("boss") as BattleUnitState
	var elite := counter_case.get("elite") as BattleUnitState
	var skill_def := counter_runtime.get_skill_defs().get(BLACK_CROWN_SEAL_SKILL_ID) as SkillDef

	var illegal_preview := counter_runtime.preview_command(
		_build_unit_skill_command(counter_caster.unit_id, BLACK_CROWN_SEAL_SKILL_ID, elite, COUNTERATTACK_LOCK_VARIANT_ID)
	)
	_assert_true(
		illegal_preview != null and not illegal_preview.allowed,
		"黑冠封印应拒绝非 boss 的 elite 目标。"
	)

	counter_runtime.issue_command(
		_build_unit_skill_command(counter_caster.unit_id, BLACK_CROWN_SEAL_SKILL_ID, boss, COUNTERATTACK_LOCK_VARIANT_ID)
	)
	_assert_true(
		boss.has_status_effect(STATUS_BLACK_CROWN_SEAL_COUNTERATTACK),
		"黑冠封印·禁反击成功后应写入对应状态。"
	)
	_assert_true(counter_runtime.is_unit_counterattack_locked(boss), "黑冠封印·禁反击应封锁 boss 的反击。")
	counter_caster.current_ap = 1
	_assert_eq(
		counter_runtime.get_skill_cast_block_reason(counter_caster, skill_def),
		"黑冠封印每战只能施放 1 次。",
		"黑冠封印成功后应立刻进入每战 1 次的封锁状态。"
	)
	counter_runtime.dispose()

	var crit_case := _build_black_crown_seal_case(&"fate_25_black_crown_crit")
	var crit_runtime := crit_case.get("runtime") as BattleRuntimeModule
	var crit_caster := crit_case.get("caster") as BattleUnitState
	var crit_boss := crit_case.get("boss") as BattleUnitState
	var ally_target := crit_case.get("ally_target") as BattleUnitState
	crit_runtime.issue_command(
		_build_unit_skill_command(crit_caster.unit_id, BLACK_CROWN_SEAL_SKILL_ID, crit_boss, CRIT_LOCK_VARIANT_ID)
	)
	_assert_true(
		crit_boss.has_status_effect(STATUS_BLACK_CROWN_SEAL_CRIT),
		"黑冠封印·禁暴击成功后应写入对应状态。"
	)
	crit_runtime.dispose()


func _issue_black_contract_push_case(variant_id: StringName) -> Dictionary:
	var runtime := _build_runtime()
	var skill_def := runtime.get_skill_defs().get(BLACK_CONTRACT_PUSH_SKILL_ID) as SkillDef
	var cast_variant = skill_def.combat_profile.get_cast_variant(variant_id) if skill_def != null and skill_def.combat_profile != null else null
	var state := _build_skill_test_state(StringName("black_contract_%s" % String(variant_id)), Vector2i(6, 4))
	var caster := _build_unit(&"contract_caster", "契约战士", &"player", Vector2i(1, 1), 1, HERO_ID)
	caster.current_hp = 28
	caster.known_active_skill_ids = [BLACK_CONTRACT_PUSH_SKILL_ID]
	caster.known_skill_level_map = {BLACK_CONTRACT_PUSH_SKILL_ID: 1}
	if variant_id == GUARD_TITHE_VARIANT_ID:
		_set_status(caster, STATUS_GUARDING, 60)
	var enemy := _build_unit(&"contract_target", "高闪避敌人", &"enemy", Vector2i(2, 1), 1)
	enemy.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 999)
	_add_unit(runtime, state, caster)
	_add_unit(runtime, state, enemy)
	state.ally_unit_ids = [caster.unit_id]
	state.enemy_unit_ids = [enemy.unit_id]
	state.active_unit_id = caster.unit_id
	runtime._state = state
	_begin_runtime_battle(runtime)

	var preview := runtime.preview_command(_build_unit_skill_command(caster.unit_id, BLACK_CONTRACT_PUSH_SKILL_ID, enemy, variant_id))
	_assert_true(preview != null and preview.allowed, "黑契推进 %s 前置：目标应可预览。" % String(variant_id))

	var simulated_result := runtime._resolve_unit_skill_effect_result(
		BattleUnitState.from_dict(caster.to_dict()),
		BattleUnitState.from_dict(enemy.to_dict()),
		skill_def,
		runtime._collect_unit_skill_effect_defs(skill_def, cast_variant)
	)
	var batch := runtime.issue_command(_build_unit_skill_command(caster.unit_id, BLACK_CONTRACT_PUSH_SKILL_ID, enemy, variant_id))
	return {
		"runtime": runtime,
		"caster": caster,
		"enemy": enemy,
		"batch": batch,
		"preview": preview,
		"simulated_result": simulated_result,
	}


func _build_black_crown_seal_case(battle_id: StringName) -> Dictionary:
	var runtime := _build_runtime()
	var state := _build_skill_test_state(battle_id, Vector2i(7, 4))
	var caster := _build_unit(&"seal_caster", "黑冕使徒", &"player", Vector2i(1, 1), 1, HERO_ID)
	caster.known_active_skill_ids = [BLACK_CROWN_SEAL_SKILL_ID]
	caster.known_skill_level_map = {BLACK_CROWN_SEAL_SKILL_ID: 1}
	var boss := _build_unit(&"seal_boss", "章末 Boss", &"enemy", Vector2i(2, 1), 1, &"", false, true)
	var elite := _build_unit(&"seal_elite", "精英敌人", &"enemy", Vector2i(3, 1), 1, &"", true, false)
	var ally_target := _build_unit(&"seal_ally", "受击队友", &"player", Vector2i(1, 2), 1, &"ally")
	_add_unit(runtime, state, caster)
	_add_unit(runtime, state, boss)
	_add_unit(runtime, state, elite)
	_add_unit(runtime, state, ally_target)
	state.ally_unit_ids = [caster.unit_id, ally_target.unit_id]
	state.enemy_unit_ids = [boss.unit_id, elite.unit_id]
	state.active_unit_id = caster.unit_id
	runtime._state = state
	_begin_runtime_battle(runtime)
	return {
		"runtime": runtime,
		"caster": caster,
		"boss": boss,
		"elite": elite,
		"ally_target": ally_target,
	}


func _assert_forced_hit_no_crit(result: Dictionary, message: String) -> void:
	_assert_true(
		bool(result.get("attack_success", false))
			and bool(result.get("crit_locked", false))
			and not bool(result.get("critical_hit", false)),
		"%s result=%s" % [message, str(result)]
	)


func _assert_force_hit_preview(preview, message: String) -> void:
	var hit_preview: Dictionary = {}
	if preview != null:
		hit_preview = preview.hit_preview
	_assert_true(not hit_preview.is_empty(), "%s preview=%s" % [message, str(hit_preview)])
	_assert_eq(int(hit_preview.get("hit_rate_percent", 0)), 100, "%s hit_rate_percent 应为 100。" % message)
	_assert_eq(int(hit_preview.get("success_rate_percent", 0)), 100, "%s success_rate_percent 应为 100。" % message)
	_assert_eq(hit_preview.get("stage_success_rates", []), [100], "%s stage_success_rates 应为 [100]。" % message)
	_assert_true(bool(hit_preview.get("force_hit_no_crit", false)), "%s 应标记 force_hit_no_crit。" % message)
	_assert_true(
		String(hit_preview.get("summary_text", "")).contains("必定命中")
			and String(hit_preview.get("summary_text", "")).contains("禁暴击"),
		"%s 文案应说明必定命中且禁暴击。" % message
	)


func _build_runtime() -> BattleRuntimeModule:
	var registry := ProgressionContentRegistry.new()
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})
	runtime.configure_damage_resolver_for_tests(DETERMINISTIC_BATTLE_DAMAGE_RESOLVER_SCRIPT.new())
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
	is_elite := false,
	is_boss := false
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
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.AURA_MAX, 4)
	unit.attribute_snapshot.set_value(&"action_points", maxi(current_ap, 1))
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 12)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 4)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 6)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 4)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 60)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 10)
	unit.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.HIDDEN_LUCK_AT_BIRTH, 0)
	unit.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.FAITH_LUCK_BONUS, 0)
	unit.attribute_snapshot.set_value(FORTUNE_MARK_TARGET_STAT_ID, 2 if is_boss else (1 if is_elite else 0))
	unit.attribute_snapshot.set_value(BOSS_TARGET_STAT_ID, 1 if is_boss else 0)
	return unit


func _add_unit(runtime: BattleRuntimeModule, state: BattleState, unit: BattleUnitState) -> void:
	state.units[unit.unit_id] = unit
	runtime._grid_service.place_unit(state, unit, unit.coord, true)


func _set_status(
	unit_state: BattleUnitState,
	status_id: StringName,
	duration_tu: int,
	source_unit_id: StringName = &"",
	power: int = 1,
	params: Dictionary = {}
) -> void:
	if unit_state == null or status_id == &"":
		return
	var status_entry := BattleStatusEffectState.new()
	status_entry.status_id = status_id
	status_entry.source_unit_id = source_unit_id
	status_entry.power = maxi(power, 1)
	status_entry.stacks = 1
	status_entry.duration = duration_tu
	status_entry.params = params.duplicate(true)
	unit_state.set_status_effect(status_entry)


func _build_unit_skill_command(
	unit_id: StringName,
	skill_id: StringName,
	target_unit: BattleUnitState,
	variant_id: StringName = &""
) -> BattleCommand:
	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = unit_id
	command.skill_id = skill_id
	command.skill_variant_id = variant_id
	command.target_unit_id = target_unit.unit_id if target_unit != null else &""
	command.target_coord = target_unit.coord if target_unit != null else Vector2i(-1, -1)
	return command


func _build_multi_unit_skill_command(
	unit_id: StringName,
	skill_id: StringName,
	target_units: Array[BattleUnitState],
	variant_id: StringName = &""
) -> BattleCommand:
	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = unit_id
	command.skill_id = skill_id
	command.skill_variant_id = variant_id
	var first_target: BattleUnitState = null
	for target_unit in target_units:
		if target_unit == null:
			continue
		if first_target == null:
			first_target = target_unit
		command.target_unit_ids.append(target_unit.unit_id)
		command.target_coords.append(target_unit.coord)
	command.target_unit_id = first_target.unit_id if first_target != null else &""
	command.target_coord = first_target.coord if first_target != null else Vector2i(-1, -1)
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


func _build_low_luck_context(hidden_luck_at_birth: int, fate_event_bus: BattleFateEventBus) -> Dictionary:
	var party_state := PartyState.new()
	party_state.leader_member_id = HERO_ID
	party_state.main_character_member_id = HERO_ID
	party_state.active_member_ids = [HERO_ID]
	party_state.set_member_state(_build_member_state(hidden_luck_at_birth))
	var manager := CharacterManagementModule.new()
	manager.setup(party_state, {}, {}, {})
	var service := LowLuckEventService.new()
	service.setup(manager, fate_event_bus)
	return {
		"party_state": party_state,
		"manager": manager,
		"service": service,
	}


func _build_member_state(hidden_luck_at_birth: int) -> PartyMemberState:
	var member_state := PartyMemberState.new()
	member_state.member_id = HERO_ID
	member_state.display_name = "Hero"
	member_state.progression.unit_id = HERO_ID
	member_state.progression.display_name = "Hero"
	member_state.progression.character_level = 12
	member_state.progression.unit_base_attributes.set_attribute_value(&"hidden_luck_at_birth", hidden_luck_at_birth)
	return member_state


func _build_critical_fail_payload(
	battle_id: StringName,
	member_id: StringName,
	attacker_id: StringName,
	hidden_luck_at_birth: int
) -> Dictionary:
	return {
		"battle_id": battle_id,
		"attacker_id": attacker_id,
		"attacker_member_id": member_id,
		"luck_snapshot": {
			"hidden_luck_at_birth": hidden_luck_at_birth,
		},
	}


func _build_battle_resolution_result(battle_id: StringName) -> BattleResolutionResult:
	var result := BattleResolutionResult.new()
	result.battle_id = battle_id
	result.winner_faction_id = &"player"
	return result


func _seen_event_count(seen_events: Array[StringName], event_type: StringName) -> int:
	var count := 0
	for seen_event in seen_events:
		if seen_event == event_type:
			count += 1
	return count


func _find_unit_skill_seed_for_resolution(
	runtime: BattleRuntimeModule,
	state: BattleState,
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	expected_event_tags: Array[StringName],
	cast_variant = null
) -> int:
	if runtime == null or state == null or active_unit == null or target_unit == null or skill_def == null:
		return -1
	var resolver := DETERMINISTIC_BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	var effect_defs := runtime._collect_unit_skill_effect_defs(skill_def, cast_variant)
	if effect_defs.is_empty():
		return -1
	for candidate_seed in range(4096):
		state.seed = candidate_seed
		state.attack_roll_nonce = 0
		var simulated_source: BattleUnitState = BattleUnitState.from_dict(active_unit.to_dict())
		var simulated_target: BattleUnitState = BattleUnitState.from_dict(target_unit.to_dict())
		var attack_check: Dictionary = runtime.get_hit_resolver().build_skill_attack_check(
			simulated_source,
			simulated_target,
			skill_def
		)
		var simulated_result := resolver.resolve_attack_effects(
			simulated_source,
			simulated_target,
			effect_defs,
			attack_check,
			{
				"battle_state": state,
			}
		)
		var event_tags := ProgressionDataUtils.to_string_name_array(simulated_result.get("fate_event_tags", []))
		for expected_event_tag in expected_event_tags:
			if event_tags.has(expected_event_tag):
				state.attack_roll_nonce = 0
				return candidate_seed
	state.attack_roll_nonce = 0
	return -1


func _find_repeat_attack_stage_seed_for_resolution(
	runtime: BattleRuntimeModule,
	state: BattleState,
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	repeat_effect: CombatEffectDef,
	stage_index: int,
	expected_event_tags: Array[StringName]
) -> int:
	if runtime == null or state == null or active_unit == null or target_unit == null or skill_def == null or repeat_effect == null:
		return -1
	var resolver := DETERMINISTIC_BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	var staged_effects := runtime._repeat_attack_resolver.collect_repeat_attack_base_effects(skill_def.combat_profile.effect_defs)
	if staged_effects.is_empty():
		return -1
	for candidate_seed in range(4096):
		state.seed = candidate_seed
		state.attack_roll_nonce = 0
		var simulated_source: BattleUnitState = BattleUnitState.from_dict(active_unit.to_dict())
		var simulated_target: BattleUnitState = BattleUnitState.from_dict(target_unit.to_dict())
		var attack_check: Dictionary = runtime.get_hit_resolver().build_fate_aware_repeat_attack_stage_hit_check(
			state,
			simulated_source,
			simulated_target,
			skill_def,
			repeat_effect,
			stage_index
		)
		var simulated_result := resolver.resolve_attack_effects(
			simulated_source,
			simulated_target,
			staged_effects,
			attack_check,
			{
				"battle_state": state,
			}
		)
		var event_tags := ProgressionDataUtils.to_string_name_array(simulated_result.get("fate_event_tags", []))
		for expected_event_tag in expected_event_tags:
			if event_tags.has(expected_event_tag):
				state.attack_roll_nonce = 0
				return candidate_seed
	state.attack_roll_nonce = 0
	return -1


func _has_fate_attack_report_entry(report_entries_variant) -> bool:
	for report_entry_variant in report_entries_variant:
		if report_entry_variant is not Dictionary:
			continue
		var report_entry := report_entry_variant as Dictionary
		if String(report_entry.get("entry_type", "")) == "fate_attack_resolution":
			return true
	return false


func _has_fate_attack_report_entry_with_tag(report_entries_variant, event_tag: StringName) -> bool:
	for report_entry_variant in report_entries_variant:
		if report_entry_variant is not Dictionary:
			continue
		var report_entry := report_entry_variant as Dictionary
		if String(report_entry.get("entry_type", "")) != "fate_attack_resolution":
			continue
		var event_tags_variant = report_entry.get("event_tags", [])
		if event_tags_variant is Array and (event_tags_variant as Array).has(String(event_tag)):
			return true
	return false


func _assert_log_contains(lines: Array, needle: String, message: String) -> void:
	for line_variant in lines:
		if String(line_variant).contains(needle):
			return
	_failures.append("%s log=%s" % [message, str(lines)])


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s actual=%s expected=%s" % [message, str(actual), str(expected)])
