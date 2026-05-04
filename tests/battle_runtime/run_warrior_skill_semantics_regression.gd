extends SceneTree

const BattleAiContext = preload("res://scripts/systems/battle/ai/battle_ai_context.gd")
const BattleCommand = preload("res://scripts/systems/battle/core/battle_command.gd")
const BattleRuntimeModule = preload("res://scripts/systems/battle/runtime/battle_runtime_module.gd")
const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleTimelineState = preload("res://scripts/systems/battle/core/battle_timeline_state.gd")
const BattleCellState = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const BattleDamageResolver = preload("res://scripts/systems/battle/rules/battle_damage_resolver.gd")
const EnemyContentRegistry = preload("res://scripts/enemies/enemy_content_registry.gd")
const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")


class DeterministicHitMaxDamageResolver extends BattleDamageResolver:
	func _roll_damage_die(dice_sides: int) -> int:
		return maxi(dice_sides, 1)

	func _roll_true_random_attack_range(min_value: int, max_value: int, battle_state) -> int:
		if battle_state != null:
			battle_state.attack_roll_nonce = maxi(int(battle_state.attack_roll_nonce), 0) + 1
		return clampi(10, mini(min_value, max_value), maxi(min_value, max_value))


var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_true_dragon_slash_hits_multiple_units_in_line()
	_test_shield_bash_reduces_target_ap_on_next_turn()
	_test_guard_applies_only_guarding_status()
	_test_guard_reduces_incoming_damage()
	_test_war_cry_applies_attack_roll_bonus()
	_test_heavy_strike_level_scaling_data_applies()
	_test_jump_slash_repositions_before_landing_burst()
	_test_jump_slash_ground_range_ignores_weapon_range()
	_test_jump_slash_diagonal_landing_uses_jump_distance()
	_test_execution_cleave_deals_more_damage_to_low_hp_targets()
	_test_taunt_redirects_ai_target()
	_test_aura_slash_requires_and_consumes_aura()
	if _failures.is_empty():
		print("Warrior skill semantics regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Warrior skill semantics regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_true_dragon_slash_hits_multiple_units_in_line() -> void:
	var runtime := _build_runtime()
	var state := _build_skill_test_state(Vector2i(7, 3))
	var warrior := _build_unit(&"warrior_line_user", Vector2i(0, 1), 3)
	warrior.current_stamina = 40
	warrior.current_aura = 2
	warrior.known_active_skill_ids = [&"warrior_true_dragon_slash"]
	warrior.known_skill_level_map = {&"warrior_true_dragon_slash": 1}
	var enemy_a := _build_unit(&"warrior_line_target_a", Vector2i(2, 1), 2)
	enemy_a.faction_id = &"enemy"
	var enemy_b := _build_unit(&"warrior_line_target_b", Vector2i(4, 1), 2)
	enemy_b.faction_id = &"enemy"
	var enemy_c := _build_unit(&"warrior_line_target_c", Vector2i(3, 2), 2)
	enemy_c.faction_id = &"enemy"

	_add_unit(runtime, state, warrior)
	_add_unit(runtime, state, enemy_a)
	_add_unit(runtime, state, enemy_b)
	_add_unit(runtime, state, enemy_c)
	state.ally_unit_ids = [warrior.unit_id]
	state.enemy_unit_ids = [enemy_a.unit_id, enemy_b.unit_id, enemy_c.unit_id]
	state.active_unit_id = warrior.unit_id
	runtime._state = state

	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = warrior.unit_id
	command.skill_id = &"warrior_true_dragon_slash"
	command.target_coord = Vector2i(3, 1)

	var preview := runtime.preview_command(command)
	_assert_true(preview != null and preview.allowed, "真龙斩应允许在同列目标线上施放。")
	_assert_true(preview != null and preview.target_unit_ids.has(enemy_a.unit_id), "真龙斩预览应命中直线上的第一个敌人。")
	_assert_true(preview != null and preview.target_unit_ids.has(enemy_b.unit_id), "真龙斩预览应命中直线上的第二个敌人。")
	_assert_true(preview != null and not preview.target_unit_ids.has(enemy_c.unit_id), "真龙斩不应命中不在直线上的目标。")

	var hp_a_before := enemy_a.current_hp
	var hp_b_before := enemy_b.current_hp
	var hp_c_before := enemy_c.current_hp
	var batch := runtime.issue_command(command)
	_assert_true(batch.changed_unit_ids.has(enemy_a.unit_id), "真龙斩应记录首个命中目标的变更。")
	_assert_true(batch.changed_unit_ids.has(enemy_b.unit_id), "真龙斩应记录第二个命中目标的变更。")
	_assert_true(enemy_a.current_hp < hp_a_before, "真龙斩应对直线上的首个敌人造成伤害。")
	_assert_true(enemy_b.current_hp < hp_b_before, "真龙斩应对直线上的第二个敌人造成伤害。")
	_assert_true(enemy_c.current_hp == hp_c_before, "真龙斩不应误伤直线外目标。")


func _test_shield_bash_reduces_target_ap_on_next_turn() -> void:
	var attempt := _run_shield_bash_until_staggered(100)
	if attempt.is_empty():
		_failures.append("盾击应在多次正式随机命中流程中至少成功施加一次 staggered。")
		return
	var runtime := attempt.get("runtime") as BattleRuntimeModule
	var state := attempt.get("state") as BattleState
	var enemy := attempt.get("enemy") as BattleUnitState
	var batch = attempt.get("batch")

	_assert_true(batch != null and batch.changed_unit_ids.has(enemy.unit_id), "盾击命中后应记录目标单位变更。")
	_assert_true(enemy.status_effects.has(&"staggered"), "盾击应为目标挂上 staggered。")

	state.phase = &"timeline_running"
	state.active_unit_id = &""
	state.timeline.ready_unit_ids.clear()
	state.timeline.ready_unit_ids.append(enemy.unit_id)
	var activate_batch := runtime.advance(0.0)
	_assert_true(activate_batch.log_lines.size() > 0, "目标回合激活时应产生行动日志。")
	_assert_eq(enemy.current_ap, 1, "staggered 应让目标在下一回合少 1 点行动点。")

	var wait_command := BattleCommand.new()
	wait_command.command_type = BattleCommand.TYPE_WAIT
	wait_command.unit_id = enemy.unit_id
	runtime.issue_command(wait_command)
	_assert_true(enemy.status_effects.has(&"staggered"), "目标回合结束后 staggered 不应再因 turn end 被消耗。")
	_advance_timeline_tu(runtime, state, 60)
	_assert_true(not enemy.status_effects.has(&"staggered"), "TU 走完后 staggered 应被移除。")


func _run_shield_bash_until_staggered(max_attempts: int) -> Dictionary:
	for attempt_index in range(maxi(max_attempts, 1)):
		var runtime := _build_runtime()
		var state := _build_skill_test_state(Vector2i(5, 3))
		var warrior := _build_unit(StringName("warrior_shield_bash_user_%d" % attempt_index), Vector2i(1, 1), 2)
		warrior.current_stamina = 25
		warrior.known_active_skill_ids = [&"warrior_shield_bash"]
		warrior.known_skill_level_map = {&"warrior_shield_bash": 1}
		warrior.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 100)
		var enemy := _build_unit(StringName("warrior_shield_bash_target_%d" % attempt_index), Vector2i(2, 1), 2)
		enemy.faction_id = &"enemy"
		enemy.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
		enemy.attribute_snapshot.set_value(&"action_points", 2)
		enemy.current_hp = 40

		_add_unit(runtime, state, warrior)
		_add_unit(runtime, state, enemy)
		state.ally_unit_ids = [warrior.unit_id]
		state.enemy_unit_ids = [enemy.unit_id]
		state.active_unit_id = warrior.unit_id
		runtime._state = state

		var command := BattleCommand.new()
		command.command_type = BattleCommand.TYPE_SKILL
		command.unit_id = warrior.unit_id
		command.skill_id = &"warrior_shield_bash"
		command.target_unit_id = enemy.unit_id
		command.target_coord = enemy.coord

		var batch := runtime.issue_command(command)
		if enemy.status_effects.has(&"staggered"):
			return {
				"runtime": runtime,
				"state": state,
				"enemy": enemy,
				"batch": batch,
			}
	return {}


func _test_guard_reduces_incoming_damage() -> void:
	var baseline_damage := _measure_enemy_heavy_strike_damage(false)
	var guarded_damage := _measure_enemy_heavy_strike_damage(true)
	_assert_true(guarded_damage < baseline_damage, "格挡后的承伤应低于未格挡。 baseline=%d guarded=%d" % [baseline_damage, guarded_damage])


func _test_guard_applies_only_guarding_status() -> void:
	var runtime := _build_runtime()
	var state := _build_skill_test_state(Vector2i(4, 3))
	var warrior := _build_unit(&"guard_status_user", Vector2i(1, 1), 2)
	warrior.current_stamina = 50
	warrior.known_active_skill_ids = [&"warrior_guard"]
	warrior.known_skill_level_map = {&"warrior_guard": 1}
	var enemy := _build_unit(&"guard_status_enemy", Vector2i(2, 1), 2)
	enemy.faction_id = &"enemy"

	_add_unit(runtime, state, warrior)
	_add_unit(runtime, state, enemy)
	state.ally_unit_ids = [warrior.unit_id]
	state.enemy_unit_ids = [enemy.unit_id]
	state.active_unit_id = warrior.unit_id
	runtime._state = state

	var guard_command := BattleCommand.new()
	guard_command.command_type = BattleCommand.TYPE_SKILL
	guard_command.unit_id = warrior.unit_id
	guard_command.skill_id = &"warrior_guard"
	guard_command.target_unit_id = warrior.unit_id
	guard_command.target_coord = warrior.coord
	runtime.issue_command(guard_command)

	_assert_true(warrior.has_status_effect(&"guarding"), "warrior_guard 应正式施加 guarding。")
	_assert_true(not warrior.has_status_effect(&"damage_reduction_up"), "warrior_guard 不应再顺带施加 damage_reduction_up。")


func _test_war_cry_applies_attack_roll_bonus() -> void:
	var baseline_damage := _measure_max_buffed_ally_strike_damage(false)
	var buffed_damage := _measure_max_buffed_ally_strike_damage(true)
	_assert_true(buffed_damage >= baseline_damage, "战吼不应降低友军输出。 baseline=%d buffed=%d" % [baseline_damage, buffed_damage])


func _test_heavy_strike_level_scaling_data_applies() -> void:
	var runtime := _build_runtime()
	var registry := ProgressionContentRegistry.new()
	var skill_def = registry.get_skill_defs().get(&"warrior_heavy_strike")
	_assert_true(skill_def != null and skill_def.combat_profile != null, "重击技能配置应可加载。")
	if skill_def == null or skill_def.combat_profile == null:
		return

	var level_zero := _build_heavy_strike_user(&"heavy_level_zero", 0)
	var level_zero_effects := runtime._collect_unit_skill_effect_defs(skill_def, null, level_zero)
	_assert_eq(level_zero_effects.size(), 1, "0级重击应只启用 1 条伤害效果。")
	_assert_eq(int(level_zero_effects[0].params.get("dice_sides", 0)), 4, "0级重击伤害应为 1d4。")
	_assert_eq(int(skill_def.combat_profile.get_effective_resource_costs(0).get("stamina_cost", 0)), 30, "0级重击体力消耗应为 30。")

	var level_one := _build_heavy_strike_user(&"heavy_level_one", 1)
	var level_one_effects := runtime._collect_unit_skill_effect_defs(skill_def, null, level_one)
	_assert_eq(level_one_effects.size(), 1, "1级重击应只启用 1 条伤害效果。")
	_assert_eq(int(level_one_effects[0].params.get("dice_sides", 0)), 6, "1级重击伤害应为 1d6。")

	var level_three := _build_heavy_strike_user(&"heavy_level_three", 3)
	var level_three_effects := runtime._collect_unit_skill_effect_defs(skill_def, null, level_three)
	_assert_eq(level_three_effects.size(), 2, "3级重击应启用伤害和破甲效果。")
	_assert_eq(int(level_three_effects[0].params.get("dice_sides", 0)), 8, "3级重击伤害应为 1d8。")
	_assert_eq(level_three_effects[1].status_id, &"armor_break", "3级重击应启用 armor_break。")
	_assert_eq(int(skill_def.combat_profile.attack_roll_bonus), -1, "重击所有等级应共享 -1 命中检定。")
	_assert_eq(int(skill_def.combat_profile.get_effective_resource_costs(2).get("stamina_cost", 0)), 20, "2级重击体力消耗应降为 20。")


func _test_jump_slash_repositions_before_landing_burst() -> void:
	var runtime := _build_runtime()
	var state := _build_skill_test_state(Vector2i(6, 4))
	var warrior := _build_unit(&"jump_slash_user", Vector2i(1, 1), 2)
	warrior.current_stamina = 35
	warrior.attribute_snapshot.set_value(&"strength", 12)
	warrior.known_active_skill_ids = [&"warrior_jump_slash"]
	warrior.known_skill_level_map = {&"warrior_jump_slash": 1}
	var enemy_a := _build_unit(&"jump_slash_target_a", Vector2i(3, 2), 2)
	enemy_a.faction_id = &"enemy"
	var enemy_b := _build_unit(&"jump_slash_target_b", Vector2i(4, 1), 2)
	enemy_b.faction_id = &"enemy"
	var enemy_c := _build_unit(&"jump_slash_target_c", Vector2i(5, 3), 2)
	enemy_c.faction_id = &"enemy"

	_add_unit(runtime, state, warrior)
	_add_unit(runtime, state, enemy_a)
	_add_unit(runtime, state, enemy_b)
	_add_unit(runtime, state, enemy_c)
	state.ally_unit_ids = [warrior.unit_id]
	state.enemy_unit_ids = [enemy_a.unit_id, enemy_b.unit_id, enemy_c.unit_id]
	state.active_unit_id = warrior.unit_id
	runtime._state = state

	var landing_coord := Vector2i(3, 1)
	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = warrior.unit_id
	command.skill_id = &"warrior_jump_slash"
	command.target_coord = landing_coord

	var preview := runtime.preview_command(command)
	_assert_true(preview != null and preview.allowed, "跳斩应允许选择空闲合法落点。")
	_assert_true(preview != null and preview.target_unit_ids.has(enemy_a.unit_id), "跳斩预览应包含落点半径内的敌人。")
	_assert_true(preview != null and preview.target_unit_ids.has(enemy_b.unit_id), "跳斩预览应包含另一名受波及的敌人。")
	_assert_true(preview != null and not preview.target_unit_ids.has(enemy_c.unit_id), "跳斩不应误标超出半径的敌人。")

	var hp_a_before := enemy_a.current_hp
	var hp_b_before := enemy_b.current_hp
	var hp_c_before := enemy_c.current_hp
	var batch := runtime.issue_command(command)
	_assert_eq(warrior.coord, landing_coord, "跳斩执行后施法者应落在指定地格。")
	_assert_true(batch.changed_unit_ids.has(warrior.unit_id), "跳斩应记录施法者位移。")
	_assert_true(batch.changed_unit_ids.has(enemy_a.unit_id), "跳斩应记录首个受击敌人的变更。")
	_assert_true(batch.changed_unit_ids.has(enemy_b.unit_id), "跳斩应记录第二个受击敌人的变更。")
	_assert_true(enemy_a.current_hp < hp_a_before, "跳斩应对落点范围内的首个敌人造成伤害。")
	_assert_true(enemy_b.current_hp < hp_b_before, "跳斩应对落点范围内的第二个敌人造成伤害。")
	_assert_true(enemy_c.current_hp == hp_c_before, "跳斩不应命中落点范围外的敌人。")

	var blocked_runtime := _build_runtime()
	var blocked_state := _build_skill_test_state(Vector2i(5, 4))
	var blocked_warrior := _build_unit(&"jump_slash_blocked_user", Vector2i(1, 1), 2)
	blocked_warrior.current_stamina = 35
	blocked_warrior.known_active_skill_ids = [&"warrior_jump_slash"]
	blocked_warrior.known_skill_level_map = {&"warrior_jump_slash": 1}
	var landing_blocker := _build_unit(&"jump_slash_blocker", Vector2i(2, 1), 2)

	_add_unit(blocked_runtime, blocked_state, blocked_warrior)
	_add_unit(blocked_runtime, blocked_state, landing_blocker)
	blocked_state.ally_unit_ids = [blocked_warrior.unit_id, landing_blocker.unit_id]
	blocked_state.enemy_unit_ids = []
	blocked_state.active_unit_id = blocked_warrior.unit_id
	blocked_runtime._state = blocked_state
	var blocked_ap_before := blocked_warrior.current_ap
	var blocked_stamina_before := blocked_warrior.current_stamina

	var blocked_command := BattleCommand.new()
	blocked_command.command_type = BattleCommand.TYPE_SKILL
	blocked_command.unit_id = blocked_warrior.unit_id
	blocked_command.skill_id = &"warrior_jump_slash"
	blocked_command.target_coord = landing_blocker.coord

	var blocked_preview := blocked_runtime.preview_command(blocked_command)
	_assert_true(blocked_preview != null and not blocked_preview.allowed, "跳斩落点被占据时应禁止施放。")
	var blocked_batch := blocked_runtime.issue_command(blocked_command)
	_assert_eq(blocked_warrior.coord, Vector2i(1, 1), "preview 拒绝的跳斩不应强行改变施法者坐标。")
	_assert_eq(blocked_warrior.current_ap, blocked_ap_before, "preview 拒绝的跳斩不应继续扣除行动点。")
	_assert_eq(blocked_warrior.current_stamina, blocked_stamina_before, "preview 拒绝的跳斩不应继续扣除体力。")
	_assert_true(not blocked_batch.changed_unit_ids.has(blocked_warrior.unit_id), "preview 拒绝的跳斩不应把施法者记为已执行变更。")
	_assert_true(
		blocked_batch.log_lines.any(func(line): return String(line).contains("跳跃落点")),
		"preview 拒绝的跳斩应把阻断原因带回 issue_command。 log=%s" % [str(blocked_batch.log_lines)]
	)


func _test_jump_slash_ground_range_ignores_weapon_range() -> void:
	var runtime := _build_runtime()
	var state := _build_skill_test_state(Vector2i(5, 4))
	var warrior := _build_unit(&"jump_slash_short_weapon_user", Vector2i(1, 1), 2)
	warrior.current_stamina = 35
	warrior.attribute_snapshot.set_value(&"strength", 12)
	warrior.known_active_skill_ids = [&"warrior_jump_slash"]
	warrior.known_skill_level_map = {&"warrior_jump_slash": 1}
	_apply_test_equipped_weapon(warrior, 1)

	_add_unit(runtime, state, warrior)
	state.ally_unit_ids = [warrior.unit_id]
	state.enemy_unit_ids = []
	state.active_unit_id = warrior.unit_id
	runtime._state = state

	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = warrior.unit_id
	command.skill_id = &"warrior_jump_slash"
	command.target_coord = Vector2i(3, 1)

	var preview := runtime.preview_command(command)
	_assert_true(
		preview != null and preview.allowed,
		"跳斩地面落点范围应使用技能配置，不应被 1 格短武器射程压缩。 log=%s" % [str(preview.log_lines if preview != null else [])]
	)


func _test_jump_slash_diagonal_landing_uses_jump_distance() -> void:
	var runtime := _build_runtime()
	var state := _build_skill_test_state(Vector2i(6, 6))
	var warrior := _build_unit(&"jump_slash_diagonal_user", Vector2i(1, 1), 2)
	warrior.current_stamina = 35
	warrior.attribute_snapshot.set_value(&"strength", 12)
	warrior.known_active_skill_ids = [&"warrior_jump_slash"]
	warrior.known_skill_level_map = {&"warrior_jump_slash": 1}

	_add_unit(runtime, state, warrior)
	state.ally_unit_ids = [warrior.unit_id]
	state.enemy_unit_ids = []
	state.active_unit_id = warrior.unit_id
	runtime._state = state

	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = warrior.unit_id
	command.skill_id = &"warrior_jump_slash"
	command.target_coord = Vector2i(3, 3)

	var preview := runtime.preview_command(command)
	_assert_true(
		preview != null and preview.allowed,
		"跳斩斜向落点应按跳跃 Chebyshev 距离校验，而不是被曼哈顿距离拒绝。 log=%s" % [str(preview.log_lines if preview != null else [])]
	)


func _test_execution_cleave_deals_more_damage_to_low_hp_targets() -> void:
	var healthy_damage := _measure_execution_cleave_damage(30)
	var low_hp_damage := _measure_execution_cleave_damage(19)
	_assert_true(
		low_hp_damage > healthy_damage,
		"断头斩应对低血目标造成更高伤害。 healthy=%d low_hp=%d" % [healthy_damage, low_hp_damage]
	)


func _test_taunt_redirects_ai_target() -> void:
	var runtime := _build_runtime()
	var state := _build_skill_test_state(Vector2i(4, 4))
	var enemy := _build_unit(&"taunted_enemy", Vector2i(1, 1), 2)
	enemy.faction_id = &"enemy"
	enemy.control_mode = &"ai"
	enemy.ai_brain_id = &"melee_aggressor"
	enemy.current_stamina = 30
	enemy.current_move_points = 0
	enemy.known_active_skill_ids = [&"warrior_heavy_strike"]
	enemy.known_skill_level_map = {&"warrior_heavy_strike": 1}
	enemy.status_effects[&"taunted"] = {
		"status_id": &"taunted",
		"source_unit_id": &"taunt_source",
		"power": 1,
		"duration": 90,
	}
	var taunt_source := _build_unit(&"taunt_source", Vector2i(2, 1), 2)
	var other_target := _build_unit(&"other_target", Vector2i(1, 2), 2)

	_add_unit(runtime, state, enemy)
	_add_unit(runtime, state, taunt_source)
	_add_unit(runtime, state, other_target)
	state.enemy_unit_ids = [enemy.unit_id]
	state.ally_unit_ids = [taunt_source.unit_id, other_target.unit_id]
	state.active_unit_id = enemy.unit_id
	runtime._state = state

	var context := BattleAiContext.new()
	context.state = state
	context.unit_state = enemy
	context.grid_service = runtime._grid_service
	context.skill_defs = runtime._skill_defs
	context.preview_callback = Callable(runtime, "preview_command")

	var decision = runtime._ai_service.choose_command(context)
	_assert_true(decision != null and decision.command != null, "被挑衅的敌人应仍能产出合法 AI 指令。")
	_assert_eq(decision.command.target_unit_id, taunt_source.unit_id, "被 taunted 的敌人应优先把来源单位作为目标。")


func _test_aura_slash_requires_and_consumes_aura() -> void:
	var runtime := _build_runtime()
	var state := _build_skill_test_state(Vector2i(5, 3))
	var warrior := _build_unit(&"warrior_aura_user", Vector2i(1, 1), 2)
	warrior.current_aura = 1
	warrior.known_active_skill_ids = [&"warrior_aura_slash"]
	warrior.known_skill_level_map = {&"warrior_aura_slash": 1}
	var enemy := _build_unit(&"warrior_aura_target", Vector2i(2, 1), 2)
	enemy.faction_id = &"enemy"

	_add_unit(runtime, state, warrior)
	_add_unit(runtime, state, enemy)
	state.ally_unit_ids = [warrior.unit_id]
	state.enemy_unit_ids = [enemy.unit_id]
	state.active_unit_id = warrior.unit_id
	runtime._state = state

	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = warrior.unit_id
	command.skill_id = &"warrior_aura_slash"
	command.target_unit_id = enemy.unit_id
	command.target_coord = enemy.coord

	var preview := runtime.preview_command(command)
	_assert_true(preview != null and preview.allowed, "斗气足够时斗气斩应允许施放。")

	var enemy_hp_before := enemy.current_hp
	runtime.issue_command(command)
	_assert_eq(warrior.current_aura, 0, "斗气斩施放后应正确扣除 Aura。")
	_assert_true(enemy.current_hp < enemy_hp_before, "斗气斩应对目标造成伤害。")

	warrior.current_ap = 2
	warrior.current_stamina = 2
	warrior.cooldowns.clear()
	var retry_preview := runtime.preview_command(command)
	_assert_true(
		retry_preview != null and not retry_preview.allowed and String(retry_preview.log_lines[-1]).contains("斗气不足"),
		"Aura 清空后斗气斩应被禁用。"
	)


func _measure_enemy_heavy_strike_damage(apply_guard: bool) -> int:
	var runtime := _build_runtime()
	runtime.configure_damage_resolver_for_tests(DeterministicHitMaxDamageResolver.new())
	var state := _build_skill_test_state(Vector2i(5, 3))
	var warrior := _build_unit(&"guard_target", Vector2i(1, 1), 2)
	warrior.current_stamina = 50
	warrior.known_active_skill_ids = [&"warrior_guard"]
	warrior.known_skill_level_map = {&"warrior_guard": 1}
	var enemy := _build_unit(&"guard_attacker", Vector2i(2, 1), 2)
	enemy.faction_id = &"enemy"
	enemy.current_stamina = 30
	enemy.known_active_skill_ids = [&"warrior_heavy_strike"]
	enemy.known_skill_level_map = {&"warrior_heavy_strike": 1}

	_add_unit(runtime, state, warrior)
	_add_unit(runtime, state, enemy)
	state.ally_unit_ids = [warrior.unit_id]
	state.enemy_unit_ids = [enemy.unit_id]
	runtime._state = state

	if apply_guard:
		state.phase = &"unit_acting"
		state.active_unit_id = warrior.unit_id
		var guard_command := BattleCommand.new()
		guard_command.command_type = BattleCommand.TYPE_SKILL
		guard_command.unit_id = warrior.unit_id
		guard_command.skill_id = &"warrior_guard"
		guard_command.target_unit_id = warrior.unit_id
		guard_command.target_coord = warrior.coord
		runtime.issue_command(guard_command)

	var hp_before := warrior.current_hp
	state.phase = &"unit_acting"
	state.active_unit_id = enemy.unit_id
	enemy.current_ap = 2
	var attack_command := BattleCommand.new()
	attack_command.command_type = BattleCommand.TYPE_SKILL
	attack_command.unit_id = enemy.unit_id
	attack_command.skill_id = &"warrior_heavy_strike"
	attack_command.target_unit_id = warrior.unit_id
	attack_command.target_coord = warrior.coord
	runtime.issue_command(attack_command)
	return hp_before - warrior.current_hp


func _measure_max_buffed_ally_strike_damage(apply_war_cry: bool) -> int:
	var best_damage := 0
	for attempt_index in range(80):
		best_damage = maxi(best_damage, _measure_buffed_ally_strike_damage_once(apply_war_cry, attempt_index))
	return best_damage


func _measure_buffed_ally_strike_damage_once(apply_war_cry: bool, attempt_index: int) -> int:
	var runtime := _build_runtime()
	runtime.configure_damage_resolver_for_tests(DeterministicHitMaxDamageResolver.new())
	var state := _build_skill_test_state(Vector2i(5, 4))
	var buffer := _build_unit(StringName("war_cry_user_%d" % attempt_index), Vector2i(1, 1), 2)
	buffer.current_stamina = 30
	buffer.known_active_skill_ids = [&"warrior_war_cry"]
	buffer.known_skill_level_map = {&"warrior_war_cry": 1}
	var striker := _build_unit(StringName("war_cry_striker_%d" % attempt_index), Vector2i(1, 2), 2)
	striker.current_stamina = 30
	striker.known_active_skill_ids = [&"warrior_heavy_strike"]
	striker.known_skill_level_map = {&"warrior_heavy_strike": 1}
	var enemy := _build_unit(StringName("war_cry_target_%d" % attempt_index), Vector2i(2, 2), 2)
	enemy.faction_id = &"enemy"

	_add_unit(runtime, state, buffer)
	_add_unit(runtime, state, striker)
	_add_unit(runtime, state, enemy)
	state.ally_unit_ids = [buffer.unit_id, striker.unit_id]
	state.enemy_unit_ids = [enemy.unit_id]
	runtime._state = state

	if apply_war_cry:
		state.phase = &"unit_acting"
		state.active_unit_id = buffer.unit_id
		var buff_command := BattleCommand.new()
		buff_command.command_type = BattleCommand.TYPE_SKILL
		buff_command.unit_id = buffer.unit_id
		buff_command.skill_id = &"warrior_war_cry"
		buff_command.target_coord = buffer.coord
		runtime.issue_command(buff_command)
		_assert_true(striker.status_effects.has(&"attack_roll_bonus_up"), "战吼应为半径内友军挂上 attack_roll_bonus_up。")

	var hp_before := enemy.current_hp
	state.phase = &"unit_acting"
	state.active_unit_id = striker.unit_id
	striker.current_ap = 2
	var attack_command := BattleCommand.new()
	attack_command.command_type = BattleCommand.TYPE_SKILL
	attack_command.unit_id = striker.unit_id
	attack_command.skill_id = &"warrior_heavy_strike"
	attack_command.target_unit_id = enemy.unit_id
	attack_command.target_coord = enemy.coord
	runtime.issue_command(attack_command)
	return hp_before - enemy.current_hp


func _measure_execution_cleave_damage(target_current_hp: int) -> int:
	for attempt_index in range(100):
		var runtime := _build_runtime()
		var state := _build_skill_test_state(Vector2i(5, 3))
		var warrior := _build_unit(StringName("execution_cleave_user_%d" % attempt_index), Vector2i(1, 1), 2)
		warrior.current_stamina = 35
		warrior.known_active_skill_ids = [&"warrior_execution_cleave"]
		warrior.known_skill_level_map = {&"warrior_execution_cleave": 1}
		warrior.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 100)
		var enemy := _build_unit(StringName("execution_cleave_target_%d" % attempt_index), Vector2i(2, 1), 2)
		enemy.faction_id = &"enemy"
		enemy.current_hp = target_current_hp
		enemy.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)

		_add_unit(runtime, state, warrior)
		_add_unit(runtime, state, enemy)
		state.ally_unit_ids = [warrior.unit_id]
		state.enemy_unit_ids = [enemy.unit_id]
		state.active_unit_id = warrior.unit_id
		runtime._state = state

		var command := BattleCommand.new()
		command.command_type = BattleCommand.TYPE_SKILL
		command.unit_id = warrior.unit_id
		command.skill_id = &"warrior_execution_cleave"
		command.target_unit_id = enemy.unit_id
		command.target_coord = enemy.coord

		var hp_before := enemy.current_hp
		runtime.issue_command(command)
		var damage := hp_before - enemy.current_hp
		if damage > 0:
			return damage
	return 0


func _build_runtime() -> BattleRuntimeModule:
	var registry := ProgressionContentRegistry.new()
	var enemy_content_registry := EnemyContentRegistry.new()
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, registry.get_skill_defs(), {}, enemy_content_registry.get_enemy_ai_brains())
	return runtime


func _build_skill_test_state(map_size: Vector2i) -> BattleState:
	var state := BattleState.new()
	state.battle_id = &"warrior_skill_semantics"
	state.phase = &"unit_acting"
	state.map_size = map_size
	state.timeline = BattleTimelineState.new()
	state.cells = {}
	for y in range(map_size.y):
		for x in range(map_size.x):
			state.cells[Vector2i(x, y)] = _build_cell(Vector2i(x, y))
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)
	return state


func _advance_timeline_tu(runtime: BattleRuntimeModule, state: BattleState, total_tu: int) -> void:
	if runtime == null or state == null or total_tu <= 0:
		return
	state.phase = &"timeline_running"
	state.active_unit_id = &""
	state.timeline.ready_unit_ids.clear()
	state.timeline.tick_interval_seconds = 1.0
	state.timeline.tu_per_tick = 5
	for unit_variant in state.units.values():
		var unit_state := unit_variant as BattleUnitState
		if unit_state != null:
			unit_state.action_threshold = 1000000
	runtime.advance(float(total_tu) / 5.0)


func _build_cell(coord: Vector2i) -> BattleCellState:
	var cell := BattleCellState.new()
	cell.coord = coord
	cell.base_terrain = BattleCellState.TERRAIN_LAND
	cell.base_height = 4
	cell.height_offset = 0
	cell.recalculate_runtime_values()
	return cell


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
	unit.attribute_snapshot.set_value(&"aura_max", 2)
	unit.attribute_snapshot.set_value(&"action_points", maxi(current_ap, 1))
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 12)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 4)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 6)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 4)
	_apply_test_equipped_weapon(unit)
	return unit


func _apply_test_equipped_weapon(unit: BattleUnitState, attack_range: int = 4) -> void:
	if unit == null:
		return
	unit.apply_weapon_projection({
		"weapon_profile_kind": "equipped",
		"weapon_item_id": "warrior_semantics_test_blade",
		"weapon_profile_type_id": "test_blade",
		"weapon_current_grip": "one_handed",
		"weapon_attack_range": attack_range,
		"weapon_one_handed_dice": {"dice_count": 1, "dice_sides": 6, "flat_bonus": 0},
		"weapon_uses_two_hands": false,
		"weapon_physical_damage_tag": "physical_slash",
	})


func _build_heavy_strike_user(unit_id: StringName, skill_level: int) -> BattleUnitState:
	var unit := _build_unit(unit_id, Vector2i(0, 0), 1)
	unit.current_stamina = 30
	unit.known_active_skill_ids = [&"warrior_heavy_strike"]
	unit.known_skill_level_map = {&"warrior_heavy_strike": skill_level}
	return unit


func _add_unit(runtime: BattleRuntimeModule, state: BattleState, unit: BattleUnitState) -> void:
	state.units[unit.unit_id] = unit
	runtime._grid_service.place_unit(state, unit, unit.coord, true)


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s actual=%s expected=%s" % [message, str(actual), str(expected)])
