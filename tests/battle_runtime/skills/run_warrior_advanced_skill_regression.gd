extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")
const BattleRuntimeTestHelpers = preload("res://tests/shared/battle_runtime_test_helpers.gd")

const BattleCommand = preload("res://scripts/systems/battle/core/battle_command.gd")
const BattleEventBatch = preload("res://scripts/systems/battle/core/battle_event_batch.gd")
const BattleRuntimeModule = preload("res://scripts/systems/battle/runtime/battle_runtime_module.gd")
const BattleGridService = preload("res://scripts/systems/battle/terrain/battle_grid_service.gd")
const BattleCellState = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleTimelineState = preload("res://scripts/systems/battle/core/battle_timeline_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const BattleStatusEffectState = preload("res://scripts/systems/battle/core/battle_status_effect_state.gd")
const BattleRepeatAttackResolver = preload("res://scripts/systems/battle/runtime/battle_repeat_attack_resolver.gd")
const BattleSkillMasteryService = preload("res://scripts/systems/battle/runtime/battle_skill_mastery_service.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const CombatSkillDef = preload("res://scripts/player/progression/combat_skill_def.gd")
const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const DETERMINISTIC_BATTLE_HIT_RESOLVER_SCRIPT = preload("res://tests/battle_runtime/helpers/deterministic_battle_hit_resolver.gd")
const SharedHitResolvers = preload("res://tests/shared/stub_hit_resolvers.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


class FakeRepeatAttackDamageResolver:
	extends RefCounted

	var stage_successes: Array[bool] = []
	var call_count := 0

	func resolve_attack_effects(_source_unit, _target_unit, _stage_effects: Array, _attack_check: Dictionary, _attack_context: Dictionary = {}) -> Dictionary:
		var success := bool(stage_successes[call_count]) if call_count < stage_successes.size() else false
		call_count += 1
		return {
			"attack_success": success,
			"attack_resolution": &"hit" if success else &"miss",
			"hit_rate_percent": 100 if success else 0,
			"resolution_text": "100%（测试命中）" if success else "0%（测试未命中）",
			"applied": success,
			"damage": 0,
			"healing": 0,
			"status_effect_ids": [],
			"source_status_effect_ids": [],
		}


class StageOutcomeDamageResolver extends BattleDamageResolver:
	var stage_successes: Array[bool] = []
	var stage_damage: Array[int] = []
	var target_ids_seen: Array[StringName] = []
	var dead_target_ids_seen: Array[StringName] = []
	var hp_before_by_call: Array[int] = []
	var call_count := 0

	func resolve_attack_effects(_source_unit, target_unit, _stage_effects: Variant, _attack_check: Dictionary, _attack_context: Dictionary = {}) -> Dictionary:
		var success := bool(stage_successes[call_count]) if call_count < stage_successes.size() else false
		var damage := int(stage_damage[call_count]) if call_count < stage_damage.size() else 0
		call_count += 1
		if target_unit != null:
			target_ids_seen.append(target_unit.unit_id)
			hp_before_by_call.append(int(target_unit.current_hp))
			if not target_unit.is_alive:
				dead_target_ids_seen.append(target_unit.unit_id)
		if not success:
			return {
				"attack_success": false,
				"attack_resolution": &"miss",
				"hit_roll": 1,
				"applied": false,
				"damage": 0,
				"healing": 0,
				"status_effect_ids": [],
				"source_status_effect_ids": [],
			}
		if target_unit != null and damage > 0:
			target_unit.current_hp = maxi(int(target_unit.current_hp) - damage, 0)
			if target_unit.current_hp <= 0:
				target_unit.is_alive = false
		return {
			"attack_success": true,
			"attack_resolution": &"hit",
			"hit_roll": 10,
			"applied": true,
			"damage": damage,
			"healing": 0,
			"status_effect_ids": [],
			"source_status_effect_ids": [],
		}


class FixedSecondarySaveRollDamageResolver extends BattleDamageResolver:
	var save_roll := 1

	func _roll_attack_die(_die_size: int, _is_disadvantage: bool, _attack_context: Dictionary) -> int:
		return save_roll


class FakeRepeatAttackHitResolver:
	extends RefCounted

	func build_repeat_attack_stage_context(_battle_state, _active_unit, _target_unit, _skill_def, _stage_spec = null, _check_route: StringName = &"", _trace_source: StringName = &""):
		return null

	func build_fate_aware_repeat_attack_stage_hit_check(_context) -> Dictionary:
		return {
			"hit_rate_percent": 100,
			"success_rate_percent": 100,
			"preview_text": "100%（测试）",
		}


class FakeRepeatAttackRatingSystem:
	extends RefCounted

	func record_enemy_defeated_achievement(_active_unit, _target_unit) -> void:
		pass


class FakeRepeatAttackRuntime:
	extends RefCounted

	var damage_resolver = FakeRepeatAttackDamageResolver.new()
	var hit_resolver = FakeRepeatAttackHitResolver.new()
	var rating_system = FakeRepeatAttackRatingSystem.new()

	func is_unit_follow_up_locked(_unit) -> bool:
		return false

	func append_changed_unit_id(_batch, _unit_id: StringName) -> void:
		pass

	func append_result_report_entry(_batch, _stage_result: Dictionary) -> void:
		pass

	func mark_applied_statuses_for_turn_timing(_target_unit, _status_effect_ids) -> void:
		pass

	func append_result_source_status_effects(_batch, _active_unit, _stage_result: Dictionary) -> void:
		pass

	func append_changed_unit_coords(_batch, _target_unit) -> void:
		pass

	func append_damage_result_log_lines(_batch, _prefix: String, _target_name: String, _stage_result: Dictionary) -> void:
		pass

	func clear_defeated_unit(_target_unit, _batch) -> void:
		pass

	func get_battle_rating_system():
		return rating_system

	func record_skill_effect_result(_source_unit, _damage: int, _healing: int, _kill_count: int) -> void:
		pass

	func get_hit_resolver():
		return hit_resolver

	func get_attack_check_policy_service():
		return hit_resolver

	func get_state():
		return null

	func get_damage_resolver():
		return damage_resolver

	func is_unit_effect(effect_def: CombatEffectDef) -> bool:
		return effect_def != null and effect_def.effect_type == &"damage"


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_whirlwind_slash_path_aoe_can_repeat_hits_across_steps()
	_test_whirlwind_slash_runtime_repeats_hits_across_steps()
	_test_hundred_shadow_random_chain_is_unit_target_contract()
	_test_random_chain_without_legal_target_does_not_spend_costs()
	_test_random_chain_reselects_from_living_pool_and_pays_ap_once()
	_test_random_chain_stops_immediately_on_miss_and_respects_target_cap()
	_test_saint_blade_combo_contract_requires_hit_follow_up_and_single_cost_settlement()
	_test_saint_blade_combo_runtime_stops_on_insufficient_aura_after_successful_follow_up()
	_test_saint_blade_combo_runtime_consumes_follow_up_aura_on_miss()
	_test_repeat_attack_mastery_bonus_starts_on_fifth_stage_entry()
	_test_same_faction_support_mastery_counts_status_or_effect_applied()
	_test_control_save_bonus_status_modifies_secondary_hit()
	_test_skill_mastery_ignores_legacy_hp_damage_without_formal_damage()
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
	_assert_eq(
		String(path_step_aoe.params.get("repeat_hit_status_id", "")),
		"staggered",
		"旋风斩连续命中后的控制状态应由 path_step_aoe params 声明。"
	)
	_assert_eq(
		int(path_step_aoe.params.get("repeat_hit_status_min_skill_level", -1)),
		9,
		"旋风斩连续命中控制状态的解锁等级应由 params 声明。"
	)
	_assert_eq(
		int(path_step_aoe.params.get("repeat_hit_status_threshold", -1)),
		4,
		"旋风斩连续命中控制状态的命中次数阈值应由 params 声明。"
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
	var skill_def := runtime._skill_defs.get(&"warrior_whirlwind_slash") as SkillDef
	_assert_true(skill_def != null and skill_def.combat_profile != null, "旋风斩执行回归需要有效技能定义。")
	if skill_def == null or skill_def.combat_profile == null:
		return
	var cast_variant := skill_def.combat_profile.get_cast_variant(&"whirlwind_charge")
	var path_step_aoe := _get_effect_def(cast_variant.effect_defs if cast_variant != null else [], &"path_step_aoe")
	_assert_true(path_step_aoe != null, "旋风斩执行回归需要路径 AOE 效果。")
	if path_step_aoe == null:
		return
	path_step_aoe.params["step_radius"] = 3

	var state := _build_state(Vector2i(8, 5))
	state.timeline = BattleTimelineState.new()
	var warrior := _build_unit(&"whirlwind_user", Vector2i(0, 1), 2)
	warrior.current_stamina = 60
	warrior.current_aura = 100
	warrior.known_active_skill_ids = [&"warrior_whirlwind_slash"]
	warrior.known_skill_level_map = {&"warrior_whirlwind_slash": 9}
	var repeated_target := _build_unit(&"whirlwind_repeat_target", Vector2i(3, 2), 2)
	repeated_target.current_hp = 999
	repeated_target.attribute_snapshot.set_value(&"hp_max", 999)
	repeated_target.body_size = 3
	repeated_target.set_anchor_coord(Vector2i(3, 2))
	repeated_target.faction_id = &"enemy"
	var far_target := _build_unit(&"whirlwind_far_target", Vector2i(7, 1), 2)
	far_target.faction_id = &"enemy"
	var existing_stagger := BattleStatusEffectState.new()
	existing_stagger.status_id = &"staggered"
	existing_stagger.source_unit_id = &"older_stagger_source"
	existing_stagger.power = 1
	existing_stagger.stacks = 1
	existing_stagger.duration = 15
	repeated_target.set_status_effect(existing_stagger)

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
	command.target_coord = Vector2i(5, 1)

	var hp_before := repeated_target.current_hp
	var batch := runtime.issue_command(command)
	_assert_eq(warrior.coord, Vector2i(5, 1), "旋风斩执行后施法者应停在最终冲锋落点。")
	_assert_true(repeated_target.current_hp < hp_before, "旋风斩应让同一目标在不同步段被重复命中。 before=%d after=%d" % [hp_before, repeated_target.current_hp])
	var stagger_entry = repeated_target.get_status_effect(&"staggered")
	_assert_true(stagger_entry != null, "Lv9 旋风斩连续命中超过阈值后应刷新 staggered。")
	_assert_eq(int(stagger_entry.duration) if stagger_entry != null else -1, 60, "Lv9 旋风斩 staggered 应持续 60 TU。")
	_assert_eq(int(stagger_entry.stacks) if stagger_entry != null else -1, 1, "Lv9 旋风斩 staggered 应按 refresh 语义保持单层。")
	_assert_true(
		batch != null and batch.log_lines.any(func(line): return String(line).contains("沿途触发")),
		"旋风斩日志应汇总沿途旋斩触发次数。 log=%s" % [str(batch.log_lines)]
	)


func _test_saint_blade_combo_runtime_stops_on_insufficient_aura_after_successful_follow_up() -> void:
	var runtime := _build_runtime()
	BattleRuntimeTestHelpers.configure_fixed_combat(runtime)
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
	enemy.current_hp = 999
	enemy.attribute_snapshot.set_value(&"hp_max", 999)
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
	_assert_true(enemy.current_hp < hp_before, "圣剑连斩应至少完成两段伤害。 before=%d after=%d" % [hp_before, enemy.current_hp])
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
		[2, 2],
		"命中预览应按当前 Aura 上限把 100 命中/0 闪避夹具换算为 d20 required roll。"
	)
	var forced_resolver := StageOutcomeDamageResolver.new()
	forced_resolver.stage_successes.assign([true, false])
	forced_resolver.stage_damage.assign([12, 0])
	runtime.configure_damage_resolver_for_tests(forced_resolver)
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


func _test_hundred_shadow_random_chain_is_unit_target_contract() -> void:
	var skill_def := _get_skill_def(&"warrior_hundred_shadow_final_dance")
	_assert_true(skill_def != null and skill_def.combat_profile != null, "百影终舞技能定义应存在并声明 combat_profile。")
	if skill_def == null or skill_def.combat_profile == null:
		return
	_assert_eq(skill_def.combat_profile.target_selection_mode, &"random_chain", "百影终舞应声明 random_chain 随机连击。")
	_assert_eq(skill_def.combat_profile.target_mode, &"unit", "random_chain 是正式 unit-target 技能，不应按 ground action 路由。")
	_assert_eq(skill_def.combat_profile.target_team_filter, &"enemy", "百影终舞随机链目标池应只包含敌方单位。")
	_assert_eq(skill_def.combat_profile.max_hits_per_target, 2, "百影终舞仍应保留每目标最多 2 次命中的上限。")
	_assert_eq(skill_def.combat_profile.ap_cost, 1, "百影终舞整条连击链只应按一次技能 AP 成本结算。")


func _test_random_chain_without_legal_target_does_not_spend_costs() -> void:
	var skill_def := _build_random_chain_test_skill(&"random_chain_empty_pool_contract", 2)
	var runtime := _build_runtime()
	runtime._skill_defs[skill_def.skill_id] = skill_def
	var state := _build_state(Vector2i(4, 3))
	state.timeline = BattleTimelineState.new()
	var warrior := _build_unit(&"chain_no_target_user", Vector2i(1, 1), 2)
	warrior.known_active_skill_ids = [skill_def.skill_id]
	warrior.known_skill_level_map = {skill_def.skill_id: 1}
	var ally := _build_unit(&"chain_no_target_ally", Vector2i(2, 1), 2)
	_add_unit(runtime, state, warrior)
	_add_unit(runtime, state, ally)
	state.ally_unit_ids = [warrior.unit_id, ally.unit_id]
	state.enemy_unit_ids = []
	state.active_unit_id = warrior.unit_id
	runtime._state = state

	var command := _build_skill_command_without_target(warrior.unit_id, skill_def.skill_id)
	var preview := runtime.preview_command(command)
	_assert_true(preview != null and not preview.allowed, "random_chain 没有合法敌方目标时不应允许预览。")
	var batch := runtime.issue_command(command)

	_assert_true(batch != null, "random_chain 无合法目标执行仍应返回事件批次。")
	_assert_eq(warrior.current_ap, 2, "random_chain 无合法目标失败必须发生在成本扣除前。")
	_assert_true(not warrior.cooldowns.has(skill_def.skill_id), "random_chain 无合法目标不应进入冷却。")


func _test_random_chain_reselects_from_living_pool_and_pays_ap_once() -> void:
	var skill_def := _build_random_chain_test_skill(&"random_chain_living_pool_contract", 2)
	var runtime := _build_runtime()
	runtime._skill_defs[skill_def.skill_id] = skill_def
	var state := _build_state(Vector2i(6, 3))
	state.timeline = BattleTimelineState.new()
	var warrior := _build_unit(&"chain_user", Vector2i(1, 1), 2)
	warrior.known_active_skill_ids = [skill_def.skill_id]
	warrior.known_skill_level_map = {skill_def.skill_id: 1}
	var enemy_a := _build_unit(&"chain_enemy_a", Vector2i(2, 1), 1)
	enemy_a.faction_id = &"enemy"
	enemy_a.current_hp = 20
	var enemy_b := _build_unit(&"chain_enemy_b", Vector2i(3, 1), 1)
	enemy_b.faction_id = &"enemy"
	enemy_b.current_hp = 20
	_add_unit(runtime, state, warrior)
	_add_unit(runtime, state, enemy_a)
	_add_unit(runtime, state, enemy_b)
	state.ally_unit_ids = [warrior.unit_id]
	state.enemy_unit_ids = [enemy_a.unit_id, enemy_b.unit_id]
	state.active_unit_id = warrior.unit_id
	runtime._state = state
	var forced_resolver := StageOutcomeDamageResolver.new()
	forced_resolver.stage_successes.assign([true, true, true])
	forced_resolver.stage_damage.assign([999, 1, 1])
	runtime.configure_damage_resolver_for_tests(forced_resolver)

	var command := _build_skill_command_without_target(warrior.unit_id, skill_def.skill_id)
	var preview := runtime.preview_command(command)
	_assert_true(preview != null and preview.allowed, "random_chain unit-target 技能无需显式目标也应允许预览。")
	var batch := runtime.issue_command(command)

	_assert_true(batch != null, "random_chain 执行应返回事件批次。")
	_assert_true(forced_resolver.call_count >= 2, "首击击杀后，random_chain 应从当前存活合法目标池继续选择下一击。")
	_assert_eq(forced_resolver.dead_target_ids_seen.size(), 0, "random_chain 不应再次选择已经死亡的目标。")
	if forced_resolver.target_ids_seen.size() >= 2:
		_assert_true(
			forced_resolver.target_ids_seen[0] != forced_resolver.target_ids_seen[1],
			"首击击杀目标后，下一击必须改选仍存活的合法目标。 seen=%s" % [str(forced_resolver.target_ids_seen)]
		)
	_assert_eq(warrior.current_ap, 1, "random_chain 整条链只应扣一次 AP 成本。")


func _test_random_chain_stops_immediately_on_miss_and_respects_target_cap() -> void:
	var skill_def := _build_random_chain_test_skill(&"random_chain_miss_stop_contract", 2)
	var runtime := _build_runtime()
	runtime._skill_defs[skill_def.skill_id] = skill_def
	var state := _build_state(Vector2i(7, 3))
	state.timeline = BattleTimelineState.new()
	var warrior := _build_unit(&"chain_miss_user", Vector2i(1, 1), 2)
	warrior.known_active_skill_ids = [skill_def.skill_id]
	warrior.known_skill_level_map = {skill_def.skill_id: 1}
	var enemies: Array[BattleUnitState] = [
		_build_unit(&"chain_miss_enemy_a", Vector2i(2, 1), 1),
		_build_unit(&"chain_miss_enemy_b", Vector2i(3, 1), 1),
		_build_unit(&"chain_miss_enemy_c", Vector2i(4, 1), 1),
	]
	_add_unit(runtime, state, warrior)
	for enemy in enemies:
		enemy.faction_id = &"enemy"
		enemy.current_hp = 40
		_add_unit(runtime, state, enemy)
	state.ally_unit_ids = [warrior.unit_id]
	state.enemy_unit_ids = [&"chain_miss_enemy_a", &"chain_miss_enemy_b", &"chain_miss_enemy_c"]
	state.active_unit_id = warrior.unit_id
	runtime._state = state
	var forced_resolver := StageOutcomeDamageResolver.new()
	forced_resolver.stage_successes.assign([true, false, true])
	forced_resolver.stage_damage.assign([1, 0, 1])
	runtime.configure_damage_resolver_for_tests(forced_resolver)

	var command := _build_skill_command_without_target(warrior.unit_id, skill_def.skill_id)
	var preview := runtime.preview_command(command)
	_assert_true(preview != null and preview.allowed, "random_chain miss 停止用例前置：技能应可预览。")
	runtime.issue_command(command)

	_assert_eq(forced_resolver.call_count, 2, "random_chain 任意一击 miss 后应立即终止整条链。")
	var hit_counts := _target_hit_counts(forced_resolver.target_ids_seen)
	for target_id in hit_counts.keys():
		_assert_true(int(hit_counts.get(target_id, 0)) <= 2, "random_chain 应遵守 max_hits_per_target。 counts=%s" % [str(hit_counts)])
	_assert_eq(warrior.current_ap, 1, "random_chain miss 提前停止时也只应扣一次 AP 成本。")


func _test_repeat_attack_mastery_bonus_starts_on_fifth_stage_entry() -> void:
	var runtime := FakeRepeatAttackRuntime.new()
	runtime.damage_resolver.stage_successes.assign([true, true, true, true, false])
	var mastery_service := BattleSkillMasteryService.new()
	var resolver := BattleRepeatAttackResolver.new()
	resolver.setup(runtime, mastery_service)

	var active_unit := _build_unit(&"combo_mastery_user", Vector2i(1, 1), 2)
	active_unit.source_member_id = &"hero"
	active_unit.current_aura = 99
	active_unit.known_active_skill_ids = [&"combo_mastery_stage_test"]
	active_unit.known_skill_level_map = {&"combo_mastery_stage_test": 1}
	var target_unit := _build_unit(&"combo_mastery_target", Vector2i(2, 1), 2)
	target_unit.faction_id = &"enemy"
	target_unit.current_hp = 999
	target_unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX, 999)

	var damage_effect := CombatEffectDef.new()
	damage_effect.effect_type = &"damage"
	damage_effect.power = 0
	var repeat_effect := CombatEffectDef.new()
	repeat_effect.effect_type = &"repeat_attack_until_fail"
	repeat_effect.params = {
		"cost_resource": "aura",
		"follow_up_fixed_cost": 0,
		"follow_up_attack_penalty": 0,
		"stop_on_miss": true,
		"stop_on_target_down": true,
	}
	var skill_def := SkillDef.new()
	skill_def.skill_id = &"combo_mastery_stage_test"
	skill_def.display_name = "连击熟练度段数测试"
	var combat_profile := CombatSkillDef.new()
	combat_profile.skill_id = skill_def.skill_id
	combat_profile.mastery_amount_mode = &"per_target_rank"
	combat_profile.mastery_trigger_mode = &"damage_dealt"
	combat_profile.aura_cost = 0
	combat_profile.effect_defs = [damage_effect, repeat_effect]
	skill_def.combat_profile = combat_profile

	var batch := BattleEventBatch.new()
	var executed := resolver.apply_repeat_attack_skill_result(
		active_unit,
		target_unit,
		skill_def,
		combat_profile.effect_defs,
		repeat_effect,
		batch
	)
	_assert_true(executed, "连击段数熟练度回归前置：应至少执行到第五段。")
	_assert_eq(runtime.damage_resolver.call_count, 5, "连击段数熟练度回归应固定进入第五段后 miss。")
	_assert_eq(
		mastery_service.resolve_active_skill_mastery_amount(),
		0,
		"连击熟练度 bonus 必须在对应段命中后发放，第五段 miss 不应给 bonus。"
	)

	var hit_runtime := FakeRepeatAttackRuntime.new()
	hit_runtime.damage_resolver.stage_successes.assign([true, true, true, true, true, false])
	var hit_mastery_service := BattleSkillMasteryService.new()
	var hit_resolver := BattleRepeatAttackResolver.new()
	hit_resolver.setup(hit_runtime, hit_mastery_service)
	target_unit.current_hp = 999
	var hit_executed := hit_resolver.apply_repeat_attack_skill_result(
		active_unit,
		target_unit,
		skill_def,
		combat_profile.effect_defs,
		repeat_effect,
		BattleEventBatch.new()
	)
	_assert_true(hit_executed, "连击段数熟练度回归前置：命中夹具应执行。")
	_assert_eq(hit_runtime.damage_resolver.call_count, 6, "命中夹具应在第五段命中后继续进入第六段 miss。")
	_assert_eq(
		hit_mastery_service.resolve_active_skill_mastery_amount(),
		1,
		"第五段命中后应发放 1 点连击段数 bonus。"
	)


func _test_same_faction_support_mastery_counts_status_or_effect_applied() -> void:
	var mastery_service := BattleSkillMasteryService.new()
	var source_unit := _build_unit(&"support_mastery_source", Vector2i(1, 1), 2)
	source_unit.source_member_id = &"hero"
	var ally_unit := _build_unit(&"support_mastery_ally", Vector2i(1, 2), 2)
	ally_unit.faction_id = source_unit.faction_id
	var skill_def := SkillDef.new()
	skill_def.skill_id = &"support_mastery_contract"
	skill_def.display_name = "支援熟练度契约"
	var combat_profile := CombatSkillDef.new()
	combat_profile.skill_id = skill_def.skill_id
	combat_profile.target_team_filter = &"ally"
	combat_profile.mastery_amount_mode = &"per_target_rank"
	combat_profile.mastery_trigger_mode = &"status_applied"
	skill_def.combat_profile = combat_profile

	mastery_service.record_target_result(
		source_unit,
		ally_unit,
		skill_def,
		{
			"applied": true,
			"status_effect_ids": [&"attack_up"],
		}
	)
	_assert_eq(
		mastery_service.resolve_active_skill_mastery_amount(),
		1,
		"same-faction support 技能成功施加状态时，per_target_rank 不应直接归零。"
	)


func _test_control_save_bonus_status_modifies_secondary_hit() -> void:
	var resolver := FixedSecondarySaveRollDamageResolver.new()
	resolver.save_roll = 8
	resolver.set_hit_resolver(SharedHitResolvers.FixedHitResolver.new(8))
	var source_unit := _build_unit(&"secondary_hit_source", Vector2i(1, 1), 2)
	var target_unit := _build_unit(&"secondary_hit_target", Vector2i(2, 1), 2)
	source_unit.attribute_snapshot.set_value(&"strength", 10)
	target_unit.attribute_snapshot.set_value(&"constitution", 10)

	_assert_true(
		resolver._resolve_secondary_hit(source_unit, target_unit, {}, 10),
		"无控制豁免加值时，固定 d20=8 应低于 DC10 并触发 secondary_hit。"
	)

	var save_bonus_status := BattleStatusEffectState.new()
	save_bonus_status.status_id = &"test_control_save_bonus"
	save_bonus_status.power = 1
	save_bonus_status.stacks = 1
	save_bonus_status.params = {"control_save_bonus": 3}
	target_unit.set_status_effect(save_bonus_status)
	_assert_true(
		not resolver._resolve_secondary_hit(source_unit, target_unit, {}, 10),
		"状态 params.control_save_bonus 应提高目标二次豁免，阻止同一固定掷骰触发 secondary_hit。"
	)


func _test_skill_mastery_ignores_legacy_hp_damage_without_formal_damage() -> void:
	var mastery_service := BattleSkillMasteryService.new()
	var source_unit := _build_unit(&"legacy_hp_damage_mastery_source", Vector2i(1, 1), 2)
	source_unit.source_member_id = &"hero"
	var target_unit := _build_unit(&"legacy_hp_damage_mastery_target", Vector2i(2, 1), 2)
	target_unit.faction_id = &"enemy"
	var skill_def := SkillDef.new()
	skill_def.skill_id = &"legacy_hp_damage_mastery_contract"
	skill_def.display_name = "旧 hp_damage 熟练度契约"
	var combat_profile := CombatSkillDef.new()
	combat_profile.skill_id = skill_def.skill_id
	combat_profile.mastery_amount_mode = &"per_target_rank"
	combat_profile.mastery_trigger_mode = &"damage_dealt"
	skill_def.combat_profile = combat_profile

	mastery_service.record_target_result(
		source_unit,
		target_unit,
		skill_def,
		{
			"hp_damage": 9,
			"shield_absorbed": 0,
		}
	)
	_assert_eq(
		mastery_service.resolve_active_skill_mastery_amount(),
		0,
		"只带旧 hp_damage、缺正式 damage 的结果不应触发主动技能熟练度。"
	)

	mastery_service.record_target_result(
		source_unit,
		target_unit,
		skill_def,
		{
			"damage": 9,
			"hp_damage": 9,
			"shield_absorbed": 0,
		}
	)
	_assert_eq(
		mastery_service.resolve_active_skill_mastery_amount(),
		1,
		"正式 damage 结果仍应触发主动技能熟练度。"
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


func _build_random_chain_test_skill(skill_id: StringName, max_hits_per_target: int) -> SkillDef:
	var damage_effect := CombatEffectDef.new()
	damage_effect.effect_type = &"damage"
	damage_effect.damage_tag = &"physical_slash"
	damage_effect.power = 1
	damage_effect.params = {
		"dice_count": 1,
		"dice_sides": 4,
	}
	var combat_profile := CombatSkillDef.new()
	combat_profile.skill_id = skill_id
	combat_profile.target_mode = &"unit"
	combat_profile.target_team_filter = &"enemy"
	combat_profile.target_selection_mode = &"random_chain"
	combat_profile.max_hits_per_target = max_hits_per_target
	combat_profile.range_value = 4
	combat_profile.ap_cost = 1
	combat_profile.stamina_cost = 0
	combat_profile.mastery_trigger_mode = &"damage_dealt"
	combat_profile.effect_defs = [damage_effect]
	var skill_def := SkillDef.new()
	skill_def.skill_id = skill_id
	skill_def.display_name = String(skill_id)
	skill_def.combat_profile = combat_profile
	return skill_def


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
	runtime.configure_hit_resolver_for_tests(DETERMINISTIC_BATTLE_HIT_RESOLVER_SCRIPT.new())
	return runtime


func _build_unit(unit_id: StringName, coord: Vector2i, current_ap: int) -> BattleUnitState:
	var unit := BattleUnitState.new()
	unit.unit_id = unit_id
	unit.display_name = String(unit_id)
	unit.faction_id = &"player"
	unit.current_ap = current_ap
	unit.current_hp = 40
	unit.current_mp = 4
	unit.current_stamina = 60
	unit.current_aura = 0
	unit.is_alive = true
	unit.set_anchor_coord(coord)
	unit.attribute_snapshot.set_value(&"hp_max", 40)
	unit.attribute_snapshot.set_value(&"mp_max", 4)
	unit.attribute_snapshot.set_value(&"stamina_max", 60)
	unit.attribute_snapshot.set_value(&"aura_max", 8)
	unit.attribute_snapshot.set_value(&"action_points", maxi(current_ap, 1))
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 12)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 4)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 6)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 4)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 80)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 5)
	_apply_test_equipped_weapon(unit)
	return unit


func _apply_test_equipped_weapon(unit: BattleUnitState, attack_range: int = 4) -> void:
	if unit == null:
		return
	unit.apply_weapon_projection({
		"weapon_profile_kind": "equipped",
		"weapon_item_id": "warrior_advanced_test_blade",
		"weapon_profile_type_id": "test_blade",
		"weapon_current_grip": "one_handed",
		"weapon_attack_range": attack_range,
		"weapon_one_handed_dice": {"dice_count": 1, "dice_sides": 6, "flat_bonus": 0},
		"weapon_uses_two_hands": false,
		"weapon_physical_damage_tag": "physical_slash",
	})


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


func _build_skill_command_without_target(unit_id: StringName, skill_id: StringName) -> BattleCommand:
	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = unit_id
	command.skill_id = skill_id
	return command


func _target_hit_counts(target_ids: Array[StringName]) -> Dictionary:
	var counts: Dictionary = {}
	for target_id in target_ids:
		counts[target_id] = int(counts.get(target_id, 0)) + 1
	return counts


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
		_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s actual=%s expected=%s" % [message, str(actual), str(expected)])
