extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")
const BattleTestFixture = preload("res://tests/shared/battle_test_fixture.gd")

const BattleRuntimeModule = preload("res://scripts/systems/battle/runtime/battle_runtime_module.gd")
const BattleCommand = preload("res://scripts/systems/battle/core/battle_command.gd")
const BattleEventBatch = preload("res://scripts/systems/battle/core/battle_event_batch.gd")
const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const BattleStatusEffectState = preload("res://scripts/systems/battle/core/battle_status_effect_state.gd")
const BattleRuntimeSkillTurnResolver = preload("res://scripts/systems/battle/runtime/battle_skill_turn_resolver.gd")
const BattleStatusSemanticTable = preload("res://scripts/systems/battle/rules/battle_status_semantic_table.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")

var _test := TestRunner.new()
var _battle_fixture := BattleTestFixture.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_staggered_refreshes_without_stacking_and_expires_on_tu_progress()
	_test_meteor_concussed_shares_staggered_ap_penalty_group()
	_test_meteor_concussed_consumes_without_zero_ap_log()
	_test_burning_stacks_and_ticks_on_timeline_interval()
	_test_short_burning_can_expire_before_first_tick()
	_test_slow_increases_move_cost_and_expires_on_tu_progress()
	_test_refresh_timeline_statuses_keep_single_stack_and_max_duration()
	_test_taunted_uses_timeline_decay_without_turn_end_decay()
	_test_status_duration_is_not_backfilled_from_semantic_defaults()
	_test_status_params_duration_is_not_used_as_runtime_duration()
	_test_status_duration_tu_ignores_legacy_params_duration()
	_test_damage_resolver_reads_only_formal_damage_status_params()
	_test_skill_turn_status_params_require_formal_string_keys()
	_test_status_effect_from_dict_requires_explicit_status_id()
	_test_legacy_status_effect_map_keys_are_not_status_id_fallbacks()
	_test_non_dictionary_status_effect_entries_are_rejected()
	_test_status_effect_to_dict_from_dict_round_trip_still_restores()
	if _failures.is_empty():
		print("Status effect semantics regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Status effect semantics regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_staggered_refreshes_without_stacking_and_expires_on_tu_progress() -> void:
	var runtime := _build_runtime()
	var state := _build_state(Vector2i(4, 3))
	var striker := _build_unit(&"staggered_source", Vector2i(1, 1), 2)
	var target := _build_unit(&"staggered_target", Vector2i(2, 1), 2)
	target.faction_id = &"enemy"

	_add_unit(runtime, state, striker)
	_add_unit(runtime, state, target)
	state.ally_unit_ids = [striker.unit_id]
	state.enemy_unit_ids = [target.unit_id]
	runtime._state = state

	_apply_status(runtime, striker, target, &"staggered", 15)
	_apply_status(runtime, striker, target, &"staggered", 15)
	var stagger_entry = target.get_status_effect(&"staggered")
	_assert_true(stagger_entry != null, "重复施加 staggered 后应保留正式状态。")
	_assert_eq(int(stagger_entry.stacks) if stagger_entry != null else -1, 1, "staggered 应按 refresh 语义而不是累加层数。")
	_assert_eq(int(stagger_entry.duration) if stagger_entry != null else -1, 15, "staggered 应记录剩余 TU。")

	state.phase = &"timeline_running"
	state.active_unit_id = &""
	state.timeline.ready_unit_ids.clear()
	state.timeline.ready_unit_ids.append(target.unit_id)
	runtime.advance(0)
	_assert_eq(target.current_ap, 1, "staggered 刷新后仍只应在回合开始扣 1 点行动点。")

	var wait_command := BattleCommand.new()
	wait_command.command_type = BattleCommand.TYPE_WAIT
	wait_command.unit_id = target.unit_id
	runtime.issue_command(wait_command)
	_assert_true(target.has_status_effect(&"staggered"), "staggered 不应在目标回合结束后被立即移除。")
	_advance_timeline_tu(runtime, state, 15)
	_assert_true(not target.has_status_effect(&"staggered"), "staggered 应在 TU 走完后移除。")


func _test_meteor_concussed_shares_staggered_ap_penalty_group() -> void:
	var runtime := _build_runtime()
	var target := _build_unit(&"meteor_concussed_group_target", Vector2i(1, 1), 3)
	_set_status_params(target, &"staggered", {})
	_set_status_params(target, &"meteor_concussed", {})
	var meteor_entry := target.get_status_effect(&"meteor_concussed") as BattleStatusEffectState
	if meteor_entry != null:
		meteor_entry.power = 2
		target.set_status_effect(meteor_entry)

	var batch := BattleEventBatch.new()
	var result: Dictionary = runtime._apply_turn_start_statuses(target, batch)
	_assert_true(bool(result.get("changed", false)), "meteor_concussed 参与回合开始结算后应报告 changed。")
	_assert_eq(target.current_ap, 1, "meteor_concussed 与 staggered 同组时应只扣最高 AP 惩罚，而不是叠加扣 3。")
	_assert_true(not target.has_status_effect(&"meteor_concussed"), "meteor_concussed 应在参与回合开始 AP 惩罚后消耗。")
	_assert_true(target.has_status_effect(&"staggered"), "同组结算不应顺带消耗普通 staggered。")
	_assert_eq(batch.log_lines.size(), 1, "同组 AP 惩罚应只产生一条日志。")
	_assert_true(batch.log_lines.size() > 0 and String(batch.log_lines[0]).contains("少 2 点 AP"), "同组 AP 惩罚日志应记录实际扣除值。")
	_assert_eq(BattleStatusSemanticTable.get_attack_roll_penalty(meteor_entry), 2, "meteor_concussed 应提供 -2 攻击检定语义。")
	_assert_true(BattleStatusSemanticTable.is_harmful_status(&"meteor_concussed"), "meteor_concussed 应计为有害状态。")
	_assert_true(BattleStatusSemanticTable.is_dispellable_harmful_status(&"meteor_concussed"), "meteor_concussed 应允许按有害魔法驱散。")


func _test_meteor_concussed_consumes_without_zero_ap_log() -> void:
	var runtime := _build_runtime()
	var target := _build_unit(&"meteor_concussed_zero_ap_target", Vector2i(1, 1), 0)
	_set_status_params(target, &"meteor_concussed", {})
	var meteor_entry := target.get_status_effect(&"meteor_concussed") as BattleStatusEffectState
	if meteor_entry != null:
		meteor_entry.power = 2
		target.set_status_effect(meteor_entry)

	var batch := BattleEventBatch.new()
	var result: Dictionary = runtime._apply_turn_start_statuses(target, batch)
	_assert_true(bool(result.get("changed", false)), "meteor_concussed 即使目标 AP 为 0，也应因状态消耗报告 changed。")
	_assert_eq(target.current_ap, 0, "AP 为 0 时 meteor_concussed 不应产生负 AP。")
	_assert_true(not target.has_status_effect(&"meteor_concussed"), "AP 为 0 时 meteor_concussed 仍应完成一次性消耗。")
	_assert_eq(batch.log_lines.size(), 0, "AP 为 0 时不应记录“少 AP”的误导日志。")


func _test_burning_stacks_and_ticks_on_timeline_interval() -> void:
	var runtime := _build_runtime()
	var state := _build_state(Vector2i(4, 3))
	var caster := _build_unit(&"burning_source", Vector2i(0, 1), 2)
	var target := _build_unit(&"burning_target", Vector2i(2, 1), 2)
	target.faction_id = &"enemy"
	target.current_hp = 20
	target.attribute_snapshot.set_value(&"hp_max", 20)

	_add_unit(runtime, state, caster)
	_add_unit(runtime, state, target)
	state.ally_unit_ids = [caster.unit_id]
	state.enemy_unit_ids = [target.unit_id]
	runtime._state = state

	_apply_status(runtime, caster, target, &"burning", 20, 1, 10)
	_apply_status(runtime, caster, target, &"burning", 20, 1, 10)
	var burning_entry = target.get_status_effect(&"burning")
	_assert_true(burning_entry != null, "burning 应在重复施加后存在于正式状态字典中。")
	_assert_eq(int(burning_entry.stacks) if burning_entry != null else -1, 2, "burning 应按 add 语义累加层数。")
	_assert_eq(int(burning_entry.duration) if burning_entry != null else -1, 20, "burning 应沿用施加时给定的剩余 TU。")
	_assert_eq(int(burning_entry.tick_interval_tu) if burning_entry != null else -1, 10, "burning 应记录正式周期 tick 间隔。")

	state.phase = &"timeline_running"
	state.active_unit_id = &""
	state.timeline.ready_unit_ids.clear()
	state.timeline.ready_unit_ids.append(target.unit_id)
	runtime.advance(0)
	_assert_eq(target.current_hp, 20, "burning 不应在回合开始隐式结算伤害。")
	var first_wait := BattleCommand.new()
	first_wait.command_type = BattleCommand.TYPE_WAIT
	first_wait.unit_id = target.unit_id
	runtime.issue_command(first_wait)
	burning_entry = target.get_status_effect(&"burning")
	_assert_eq(int(burning_entry.duration) if burning_entry != null else -1, 20, "burning 不应在回合结束后递减 TU。")

	_advance_timeline_tu(runtime, state, 10)
	burning_entry = target.get_status_effect(&"burning")
	_assert_eq(int(burning_entry.duration) if burning_entry != null else -1, 10, "burning 应随时间轴推进递减剩余 TU。")
	_assert_eq(target.current_hp, 18, "2 层 burning 应在第一个周期 tick 结算 2 点灼烧伤害。")

	state.phase = &"timeline_running"
	state.active_unit_id = &""
	state.timeline.ready_unit_ids.clear()
	state.timeline.ready_unit_ids.append(target.unit_id)
	runtime.advance(0)
	_assert_eq(target.current_hp, 18, "burning 不应因进入第二个行动窗口额外结算伤害。")
	var second_wait := BattleCommand.new()
	second_wait.command_type = BattleCommand.TYPE_WAIT
	second_wait.unit_id = target.unit_id
	runtime.issue_command(second_wait)
	_assert_true(target.has_status_effect(&"burning"), "burning 不应在第二个回合结束时被 turn end 提前清除。")
	_advance_timeline_tu(runtime, state, 10)
	_assert_true(not target.has_status_effect(&"burning"), "burning 到期后应按 TU 正式移除。")
	_assert_eq(target.current_hp, 16, "2 层 burning 应在到期边界完成第二个周期 tick。")


func _test_short_burning_can_expire_before_first_tick() -> void:
	var runtime := _build_runtime()
	var state := _build_state(Vector2i(4, 3))
	var caster := _build_unit(&"short_burning_source", Vector2i(0, 1), 2)
	var target := _build_unit(&"short_burning_target", Vector2i(2, 1), 2)
	target.faction_id = &"enemy"
	target.current_hp = 20

	_add_unit(runtime, state, caster)
	_add_unit(runtime, state, target)
	state.ally_unit_ids = [caster.unit_id]
	state.enemy_unit_ids = [target.unit_id]
	runtime._state = state

	_apply_status(runtime, caster, target, &"burning", 5, 1, 10)
	_advance_timeline_tu(runtime, state, 5)
	_assert_true(not target.has_status_effect(&"burning"), "短于 tick 间隔的 burning 应按 TU 到期。")
	_assert_eq(target.current_hp, 20, "短于 tick 间隔的 burning 不应保证至少触发一次伤害。")


func _test_slow_increases_move_cost_and_expires_on_tu_progress() -> void:
	var runtime := _build_runtime()
	var state := _build_state(Vector2i(5, 3))
	var source := _build_unit(&"slow_source", Vector2i(0, 1), 2)
	var target := _build_unit(&"slow_target", Vector2i(1, 1), 3)
	var enemy := _build_unit(&"slow_enemy_anchor", Vector2i(4, 1), 1)
	enemy.faction_id = &"enemy"

	_add_unit(runtime, state, source)
	_add_unit(runtime, state, target)
	_add_unit(runtime, state, enemy)
	state.ally_unit_ids = [source.unit_id, target.unit_id]
	state.enemy_unit_ids = [enemy.unit_id]
	runtime._state = state

	_apply_status(runtime, source, target, &"slow", 15)
	state.phase = &"timeline_running"
	state.active_unit_id = &""
	state.timeline.ready_unit_ids.clear()
	state.timeline.ready_unit_ids.append(target.unit_id)
	runtime.advance(0)
	_assert_true(target.has_status_effect(&"slow"), "slow 应在受影响单位回合开始后仍保持生效。")

	var move_command := BattleCommand.new()
	move_command.command_type = BattleCommand.TYPE_MOVE
	move_command.unit_id = target.unit_id
	move_command.target_coord = Vector2i(2, 1)
	var preview = runtime.preview_command(move_command)
	_assert_true(preview != null and preview.allowed, "slow 状态下的相邻移动仍应合法。")
	_assert_true(
		preview != null and preview.log_lines.size() > 0 and String(preview.log_lines[0]).contains("距离消耗 2 点移动力"),
		"slow 应把基础 1 点移动力的平地移动提升为 2 点移动力。"
	)

	runtime.issue_command(move_command)
	_assert_eq(target.current_move_points, 0, "移动成功后应耗尽本回合移动力，即使只移动 1 格。")
	_assert_eq(target.current_ap, 3, "slow 只应抬高移动行动点消耗，不应继续扣除 AP。")
	var wait_command := BattleCommand.new()
	wait_command.command_type = BattleCommand.TYPE_WAIT
	wait_command.unit_id = target.unit_id
	runtime.issue_command(wait_command)
	_assert_true(
		target.has_status_effect(&"slow"),
		"slow 不应在目标回合结束后按 turn end 立刻移除。"
	)
	_advance_timeline_tu(runtime, state, 15)
	_assert_true(not target.has_status_effect(&"slow"), "slow 应在 TU 走完后移除。")


func _test_refresh_timeline_statuses_keep_single_stack_and_max_duration() -> void:
	var cases: Array[Dictionary] = [
		{"status_id": &"attack_up", "label": "attack_up"},
		{"status_id": &"archer_pre_aim", "label": "archer_pre_aim"},
		{"status_id": &"pinned", "label": "pinned"},
		{"status_id": &"taunted", "label": "taunted"},
	]
	for case_data in cases:
		var status_id: StringName = case_data.get("status_id", &"")
		var label := String(case_data.get("label", status_id))
		var first_effect := CombatEffectDef.new()
		first_effect.effect_type = &"status"
		first_effect.status_id = status_id
		first_effect.power = 1
		first_effect.duration_tu = 10

		var second_effect := CombatEffectDef.new()
		second_effect.effect_type = &"status"
		second_effect.status_id = status_id
		second_effect.power = 2
		second_effect.duration_tu = 15

		_assert_true(BattleStatusSemanticTable.has_semantic(status_id), "%s 应注册正式状态语义。" % label)
		var merged = BattleStatusSemanticTable.merge_status(first_effect, &"source_a")
		merged = BattleStatusSemanticTable.merge_status(second_effect, &"source_b", merged)
		_assert_true(merged != null, "%s 合并后应生成正式状态。" % label)
		_assert_eq(int(merged.stacks) if merged != null else -1, 1, "%s 应按 refresh 语义保持单层。" % label)
		_assert_eq(int(merged.power) if merged != null else -1, 2, "%s 应保留更高 power。" % label)
		_assert_eq(int(merged.duration) if merged != null else -1, 15, "%s 应保留更长的剩余 TU。" % label)
		_assert_eq(BattleStatusSemanticTable.get_turn_start_ap_penalty(merged), 0, "%s 不应附带 turn start AP penalty 语义。" % label)
		_assert_eq(BattleStatusSemanticTable.get_turn_start_damage(merged), 0, "%s 不应附带 turn start damage 语义。" % label)
		_assert_eq(BattleStatusSemanticTable.get_move_cost_delta(merged), 0, "%s 不应附带 move cost delta 语义。" % label)


func _test_taunted_uses_timeline_decay_without_turn_end_decay() -> void:
	var runtime := _build_runtime()
	var state := _build_state(Vector2i(4, 3))
	var source := _build_unit(&"taunted_source", Vector2i(0, 1), 2)
	var target := _build_unit(&"taunted_target", Vector2i(2, 1), 2)
	target.faction_id = &"enemy"

	_add_unit(runtime, state, source)
	_add_unit(runtime, state, target)
	state.ally_unit_ids = [source.unit_id]
	state.enemy_unit_ids = [target.unit_id]
	runtime._state = state

	_apply_status(runtime, source, target, &"taunted", 15)
	var taunted_entry = target.get_status_effect(&"taunted")
	_assert_true(taunted_entry != null, "taunted 应写入正式状态字典。")
	_assert_eq(int(taunted_entry.duration) if taunted_entry != null else -1, 15, "taunted 应记录施加时的剩余 TU。")

	state.phase = &"timeline_running"
	state.active_unit_id = &""
	state.timeline.ready_unit_ids.clear()
	state.timeline.ready_unit_ids.append(target.unit_id)
	runtime.advance(0)
	var wait_command := BattleCommand.new()
	wait_command.command_type = BattleCommand.TYPE_WAIT
	wait_command.unit_id = target.unit_id
	runtime.issue_command(wait_command)
	_assert_true(target.has_status_effect(&"taunted"), "taunted 不应在目标回合结束后被 turn end 提前移除。")

	_advance_timeline_tu(runtime, state, 15)
	_assert_true(not target.has_status_effect(&"taunted"), "taunted 应在 TU 走完后移除。")


func _test_status_duration_is_not_backfilled_from_semantic_defaults() -> void:
	var effect_def := CombatEffectDef.new()
	effect_def.effect_type = &"status"
	effect_def.status_id = &"pinned"
	effect_def.power = 1

	var merged = BattleStatusSemanticTable.merge_status(effect_def, &"source_unit")
	_assert_true(merged != null, "状态效果应能在缺少 duration_tu 时正常合并。")
	_assert_true(merged != null and not merged.has_duration(), "缺少来源时长时，状态不应再从语义表回填默认 TU。")


func _test_status_params_duration_is_not_used_as_runtime_duration() -> void:
	var effect_def := CombatEffectDef.new()
	effect_def.effect_type = &"status"
	effect_def.status_id = &"pinned"
	effect_def.power = 1
	effect_def.params = {
		"duration": 15,
	}

	var merged = BattleStatusSemanticTable.merge_status(effect_def, &"source_unit")
	_assert_true(merged != null, "旧 params.duration 不应阻止状态对象合并。")
	_assert_true(merged != null and not merged.has_duration(), "旧 params.duration 不应再恢复为状态剩余 TU。")


func _test_status_duration_tu_ignores_legacy_params_duration() -> void:
	var effect_def := CombatEffectDef.new()
	effect_def.effect_type = &"status"
	effect_def.status_id = &"pinned"
	effect_def.power = 1
	effect_def.duration_tu = 20
	effect_def.params = {
		"duration": 90,
	}

	var merged = BattleStatusSemanticTable.merge_status(effect_def, &"source_unit")
	_assert_true(merged != null, "正式 duration_tu 应继续生成状态对象。")
	_assert_eq(int(merged.duration) if merged != null else -1, 20, "正式 duration_tu 应生效，旧 params.duration 不应覆盖。")


func _test_damage_resolver_reads_only_formal_damage_status_params() -> void:
	var runtime := _build_runtime()
	var source := _build_unit(&"damage_alias_source", Vector2i.ZERO, 2)
	var physical_effect := _build_damage_effect(10, &"physical_slash")

	var formal_tag_target := _build_unit(&"formal_damage_tag_target", Vector2i.ZERO, 2)
	_set_status_params(formal_tag_target, &"formal_fire_barrier", {
		"damage_tag": "fire",
		"mitigation_tier": "half",
	})
	var formal_tag_result: Dictionary = runtime._damage_resolver.resolve_effects(source, formal_tag_target, [physical_effect])
	_assert_eq(int(formal_tag_result.get("damage", -1)), 10, "正式 damage_tag 不匹配时不应套用 mitigation_tier。")

	var legacy_tag_target := _build_unit(&"legacy_tag_target", Vector2i.ZERO, 2)
	_set_status_params(legacy_tag_target, &"legacy_fire_barrier", {
		"tag": "fire",
		"mitigation_tier": "half",
	})
	var legacy_tag_result: Dictionary = runtime._damage_resolver.resolve_effects(source, legacy_tag_target, [physical_effect])
	_assert_eq(int(legacy_tag_result.get("damage", -1)), 5, "旧 params.tag 不应再被当作 damage_tag 过滤。")

	var formal_bypass_effect := _build_damage_effect(10, &"physical_slash")
	formal_bypass_effect.params["dr_bypass_tag"] = "armor_pierce"
	var formal_bypass_target := _build_unit(&"formal_bypass_target", Vector2i.ZERO, 2)
	_set_status_params(formal_bypass_target, &"formal_content_dr", {
		"content_dr": 4,
		"dr_bypass_tag": "armor_pierce",
	})
	var formal_bypass_result: Dictionary = runtime._damage_resolver.resolve_effects(source, formal_bypass_target, [formal_bypass_effect])
	_assert_eq(int(formal_bypass_result.get("damage", -1)), 10, "正式 dr_bypass_tag 匹配时应绕过 content_dr。")

	var legacy_effect_bypass := _build_damage_effect(10, &"physical_slash")
	legacy_effect_bypass.params["bypass_tag"] = "armor_pierce"
	var legacy_effect_bypass_target := _build_unit(&"legacy_effect_bypass_target", Vector2i.ZERO, 2)
	_set_status_params(legacy_effect_bypass_target, &"formal_content_dr", {
		"content_dr": 4,
		"dr_bypass_tag": "armor_pierce",
	})
	var legacy_effect_bypass_result: Dictionary = runtime._damage_resolver.resolve_effects(source, legacy_effect_bypass_target, [legacy_effect_bypass])
	_assert_eq(int(legacy_effect_bypass_result.get("damage", -1)), 6, "旧 effect params.bypass_tag 不应再绕过 content_dr。")

	var legacy_status_bypass_target := _build_unit(&"legacy_status_bypass_target", Vector2i.ZERO, 2)
	_set_status_params(legacy_status_bypass_target, &"legacy_content_dr", {
		"content_dr": 4,
		"bypass_tag": "armor_pierce",
	})
	var legacy_status_bypass_result: Dictionary = runtime._damage_resolver.resolve_effects(source, legacy_status_bypass_target, [formal_bypass_effect])
	_assert_eq(int(legacy_status_bypass_result.get("damage", -1)), 6, "旧 status params.bypass_tag 不应再被当作 dr_bypass_tag。")

	var formal_low_hp_effect := _build_damage_effect(10, &"physical_slash")
	formal_low_hp_effect.bonus_condition = &"target_low_hp"
	formal_low_hp_effect.params["hp_ratio_threshold_percent"] = 70
	formal_low_hp_effect.params["bonus_damage_dice_count"] = 4
	formal_low_hp_effect.params["bonus_damage_dice_sides"] = 1
	var formal_low_hp_target := _build_unit(&"formal_low_hp_target", Vector2i.ZERO, 2)
	formal_low_hp_target.current_hp = 18
	var formal_low_hp_result: Dictionary = runtime._damage_resolver.resolve_effects(source, formal_low_hp_target, [formal_low_hp_effect])
	_assert_eq(int(formal_low_hp_result.get("damage", -1)), 14, "正式 hp_ratio_threshold_percent 应控制低血追加伤害骰阈值。")
	var formal_low_hp_crit_target := _build_unit(&"formal_low_hp_crit_target", Vector2i.ZERO, 2)
	formal_low_hp_crit_target.current_hp = 18
	var formal_low_hp_crit_result: Dictionary = runtime._damage_resolver.resolve_effects(
		source,
		formal_low_hp_crit_target,
		[formal_low_hp_effect],
		{"critical_hit": true}
	)
	_assert_eq(int(formal_low_hp_crit_result.get("damage", -1)), 18, "低血暴击应额外掷一组处决追加骰。")

	var legacy_low_hp_effect := _build_damage_effect(10, &"physical_slash")
	legacy_low_hp_effect.bonus_condition = &"target_low_hp"
	legacy_low_hp_effect.params["low_hp_ratio"] = 0.7
	legacy_low_hp_effect.params["bonus_damage_dice_count"] = 4
	legacy_low_hp_effect.params["bonus_damage_dice_sides"] = 1
	var legacy_low_hp_target := _build_unit(&"legacy_low_hp_target", Vector2i.ZERO, 2)
	legacy_low_hp_target.current_hp = 18
	var legacy_low_hp_result: Dictionary = runtime._damage_resolver.resolve_effects(source, legacy_low_hp_target, [legacy_low_hp_effect])
	_assert_eq(int(legacy_low_hp_result.get("damage", -1)), 10, "旧 params.low_hp_ratio 不应再覆盖默认低血阈值或触发追加骰。")


func _test_skill_turn_status_params_require_formal_string_keys() -> void:
	var resolver := BattleRuntimeSkillTurnResolver.new()

	var legacy_bool_unit := _build_unit(&"legacy_bool_param_unit", Vector2i.ZERO, 2)
	_set_status_params(legacy_bool_unit, &"legacy_counter_lock", {
		&"lock_counterattack": true,
	})
	_assert_true(
		not resolver.has_status_param_bool(legacy_bool_unit, &"lock_counterattack"),
		"StringName-only lock_counterattack params 不应再被 status bool helper 接受。"
	)

	var formal_bool_unit := _build_unit(&"formal_bool_param_unit", Vector2i.ZERO, 2)
	_set_status_params(formal_bool_unit, &"formal_counter_lock", {
		"lock_counterattack": true,
	})
	_assert_true(
		resolver.has_status_param_bool(formal_bool_unit, &"lock_counterattack"),
		"正式 String key 的 lock_counterattack params 应继续生效。"
	)

	var legacy_int_unit := _build_unit(&"legacy_int_param_unit", Vector2i.ZERO, 2)
	_set_status_params(legacy_int_unit, &"legacy_main_skill_lock", {
		&"main_skill_lock_other_debuff_count": 2,
	})
	_assert_eq(
		resolver.get_status_param_max_int(legacy_int_unit, &"main_skill_lock_other_debuff_count"),
		0,
		"StringName-only main_skill_lock_other_debuff_count params 不应再被 int helper 接受。"
	)

	var formal_int_unit := _build_unit(&"formal_int_param_unit", Vector2i.ZERO, 2)
	_set_status_params(formal_int_unit, &"formal_main_skill_lock", {
		"main_skill_lock_other_debuff_count": 2,
	})
	_assert_eq(
		resolver.get_status_param_max_int(formal_int_unit, &"main_skill_lock_other_debuff_count"),
		2,
		"正式 String key 的 main_skill_lock_other_debuff_count params 应继续生效。"
	)

	var legacy_counts_true_unit := _build_unit(&"legacy_counts_true_unit", Vector2i.ZERO, 2)
	_set_status_params(legacy_counts_true_unit, &"custom_bad_debuff", {
		&"counts_as_debuff": true,
	})
	_assert_eq(
		resolver.count_debuff_statuses(legacy_counts_true_unit),
		0,
		"StringName-only counts_as_debuff=true 不应再把自定义状态计为 debuff。"
	)

	var formal_counts_true_unit := _build_unit(&"formal_counts_true_unit", Vector2i.ZERO, 2)
	_set_status_params(formal_counts_true_unit, &"custom_formal_debuff", {
		"counts_as_debuff": true,
	})
	_assert_eq(
		resolver.count_debuff_statuses(formal_counts_true_unit),
		1,
		"正式 String key 的 counts_as_debuff=true 应继续把自定义状态计为 debuff。"
	)

	var legacy_counts_false_unit := _build_unit(&"legacy_counts_false_unit", Vector2i.ZERO, 2)
	_set_status_params(legacy_counts_false_unit, &"burning", {
		&"counts_as_debuff": false,
	})
	_assert_eq(
		resolver.count_debuff_statuses(legacy_counts_false_unit),
		1,
		"StringName-only counts_as_debuff=false 不应再覆盖内建 debuff 表。"
	)

	var formal_counts_false_unit := _build_unit(&"formal_counts_false_unit", Vector2i.ZERO, 2)
	_set_status_params(formal_counts_false_unit, &"burning", {
		"counts_as_debuff": false,
	})
	_assert_eq(
		resolver.count_debuff_statuses(formal_counts_false_unit),
		0,
		"正式 String key 的 counts_as_debuff=false 应继续覆盖内建 debuff 表。"
	)


func _test_status_effect_from_dict_requires_explicit_status_id() -> void:
	var missing_status_id_payload := _build_status_effect_payload()
	missing_status_id_payload.erase("status_id")
	var missing_status_id = BattleStatusEffectState.from_dict(missing_status_id_payload)
	_assert_true(missing_status_id == null, "状态效果反序列化应拒绝缺少 status_id 的字典。")

	var empty_status_id_payload := _build_status_effect_payload()
	empty_status_id_payload["status_id"] = ""
	var empty_status_id = BattleStatusEffectState.from_dict(empty_status_id_payload)
	_assert_true(empty_status_id == null, "状态效果反序列化应拒绝空 status_id。")

	var non_string_status_id_payload := _build_status_effect_payload()
	non_string_status_id_payload["status_id"] = 12
	var non_string_status_id = BattleStatusEffectState.from_dict(non_string_status_id_payload)
	_assert_true(non_string_status_id == null, "状态效果反序列化应拒绝非 String/StringName 的 status_id。")

	var non_dictionary_entry = BattleStatusEffectState.from_dict("burning")
	_assert_true(non_dictionary_entry == null, "状态效果反序列化应拒绝非 Dictionary entry。")

	var string_name_status_id_payload := _build_status_effect_payload()
	string_name_status_id_payload["status_id"] = &"slow"
	string_name_status_id_payload["source_unit_id"] = &"source"
	var string_name_status_id = BattleStatusEffectState.from_dict(string_name_status_id_payload)
	_assert_true(
		string_name_status_id != null and string_name_status_id.status_id == &"slow",
		"状态效果反序列化应接受显式 StringName status_id。"
	)


func _test_legacy_status_effect_map_keys_are_not_status_id_fallbacks() -> void:
	var unit := _build_unit(&"legacy_status_map_unit", Vector2i(1, 1), 2)
	var payload := unit.to_dict()
	payload["status_effects"] = {
		"burning": {
			"power": 2,
			"stacks": 1,
			"duration": 10,
		},
	}

	_assert_true(
		BattleUnitState.from_dict(payload) == null,
		"缺 status_id 的旧状态 map shape 应拒绝整份单位 payload。"
	)


func _test_non_dictionary_status_effect_entries_are_rejected() -> void:
	var unit := _build_unit(&"non_dict_status_entry_unit", Vector2i(1, 1), 2)
	var payload := unit.to_dict()
	payload["status_effects"] = {
		"burning": "legacy_entry",
	}

	_assert_true(
		BattleUnitState.from_dict(payload) == null,
		"非 Dictionary status effect entry 应拒绝整份单位 payload。"
	)


func _test_status_effect_to_dict_from_dict_round_trip_still_restores() -> void:
	var effect := BattleStatusEffectState.new()
	effect.status_id = &"burning"
	effect.source_unit_id = &"round_trip_source"
	effect.power = 3
	effect.params = {
		"damage_tag": "fire",
	}
	effect.stacks = 2
	effect.duration = 20
	effect.tick_interval_tu = 10
	effect.next_tick_at_tu = 15
	effect.skip_next_turn_end_decay = true

	var restored_effect = BattleStatusEffectState.from_dict(effect.to_dict())
	_assert_true(restored_effect != null, "正式状态 effect to_dict/from_dict 应继续恢复对象。")
	_assert_eq(restored_effect.status_id if restored_effect != null else &"", &"burning", "正式状态 effect round trip 应保留 status_id。")
	_assert_eq(restored_effect.source_unit_id if restored_effect != null else &"", &"round_trip_source", "正式状态 effect round trip 应保留来源单位。")
	_assert_eq(restored_effect.power if restored_effect != null else -1, 3, "正式状态 effect round trip 应保留 power。")
	_assert_eq(restored_effect.stacks if restored_effect != null else -1, 2, "正式状态 effect round trip 应保留 stacks。")
	_assert_eq(restored_effect.duration if restored_effect != null else -1, 20, "正式状态 effect round trip 应保留 duration。")
	_assert_eq(restored_effect.tick_interval_tu if restored_effect != null else -1, 10, "正式状态 effect round trip 应保留 tick interval。")
	_assert_eq(restored_effect.next_tick_at_tu if restored_effect != null else -1, 15, "正式状态 effect round trip 应保留 next tick。")
	_assert_true(restored_effect != null and restored_effect.skip_next_turn_end_decay, "正式状态 effect round trip 应保留 turn end decay 标记。")

	var unit := _build_unit(&"status_round_trip_unit", Vector2i(1, 1), 2)
	unit.set_status_effect(effect)
	var restored_unit = BattleUnitState.from_dict(unit.to_dict())
	var unit_effect = restored_unit.get_status_effect(&"burning") if restored_unit != null else null
	_assert_true(unit_effect != null, "正式 BattleUnitState 状态字典 round trip 应继续恢复状态。")
	_assert_eq(unit_effect.status_id if unit_effect != null else &"", &"burning", "正式 BattleUnitState 状态 round trip 应保留 status_id。")
	_assert_eq(unit_effect.stacks if unit_effect != null else -1, 2, "正式 BattleUnitState 状态 round trip 应保留 stacks。")


func _build_status_effect_payload() -> Dictionary:
	return {
		"status_id": "burning",
		"source_unit_id": "source",
		"power": 2,
		"params": {},
		"stacks": 1,
	}


func _apply_status(
	runtime: BattleRuntimeModule,
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	status_id: StringName,
	duration_tu: int,
	power: int = 1,
	tick_interval_tu: int = 0
) -> void:
	var effect_def := CombatEffectDef.new()
	effect_def.effect_type = &"status"
	effect_def.status_id = status_id
	effect_def.power = power
	if duration_tu > 0:
		effect_def.duration_tu = duration_tu
	if tick_interval_tu > 0:
		effect_def.tick_interval_tu = tick_interval_tu
	var result := runtime._damage_resolver.resolve_effects(source_unit, target_unit, [effect_def])
	runtime.mark_applied_statuses_for_turn_timing(target_unit, result.get("status_effect_ids", []))


func _set_status_params(unit: BattleUnitState, status_id: StringName, params: Dictionary) -> void:
	var status_effect := BattleStatusEffectState.new()
	status_effect.status_id = status_id
	status_effect.power = 1
	status_effect.stacks = 1
	status_effect.params = params.duplicate(true)
	unit.set_status_effect(status_effect)


func _build_damage_effect(power: int, damage_tag: StringName) -> CombatEffectDef:
	var effect_def := CombatEffectDef.new()
	effect_def.effect_type = &"damage"
	effect_def.power = power
	effect_def.damage_tag = damage_tag
	effect_def.params = {}
	return effect_def


func _build_runtime() -> BattleRuntimeModule:
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, {}, {}, {})
	return runtime


func _build_state(map_size: Vector2i) -> BattleState:
	return _battle_fixture.build_state({
		"battle_id": &"status_effect_semantics",
		"map_size": map_size,
		"base_height": 4,
		"height_offset": 0,
	})


func _advance_timeline_tu(runtime: BattleRuntimeModule, state: BattleState, total_tu: int) -> void:
	if runtime == null or state == null or total_tu <= 0:
		return
	state.phase = &"timeline_running"
	state.active_unit_id = &""
	state.timeline.ready_unit_ids.clear()
	state.timeline.tu_per_tick = 5
	for unit_variant in state.units.values():
		var unit_state := unit_variant as BattleUnitState
		if unit_state != null:
			unit_state.action_threshold = 1000000
	runtime.advance(int(total_tu / 5))


func _build_unit(unit_id: StringName, coord: Vector2i, current_ap: int) -> BattleUnitState:
	var unit := _battle_fixture.build_unit(unit_id, {
		"coord": coord,
		"current_ap": current_ap,
		"current_hp": 30,
		"current_mp": 4,
		"current_aura": 0,
	})
	unit.current_stamina = 4
	unit.attribute_snapshot.set_value(&"hp_max", 30)
	unit.attribute_snapshot.set_value(&"mp_max", 4)
	unit.attribute_snapshot.set_value(&"stamina_max", 4)
	unit.attribute_snapshot.set_value(&"action_points", maxi(current_ap, 1))
	return unit


func _add_unit(runtime: BattleRuntimeModule, state: BattleState, unit: BattleUnitState) -> void:
	state.units[unit.unit_id] = unit
	runtime._grid_service.place_unit(state, unit, unit.coord, true)


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s actual=%s expected=%s" % [message, str(actual), str(expected)])
