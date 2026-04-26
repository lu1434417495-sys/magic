## 文件说明：该脚本属于战斗运行时冒烟测试相关的回归脚本，集中覆盖 timed terrain tick 等核心推进路径。
## 审查重点：重点核对最小状态夹具、关键推进调用以及类型收敛场景是否持续稳定。
## 备注：后续若 battle runtime 的最小启动前置发生变化，需要同步更新该脚本夹具。

extends SceneTree

const BattleRuntimeModule = preload("res://scripts/systems/battle_runtime_module.gd")
const BattleCommand = preload("res://scripts/systems/battle_command.gd")
const BattleDamageResolver = preload("res://scripts/systems/battle_damage_resolver.gd")
const BattleHitResolver = preload("res://scripts/systems/battle_hit_resolver.gd")
const BattleRangeService = preload("res://scripts/systems/battle_range_service.gd")
const BattleState = preload("res://scripts/systems/battle_state.gd")
const BattleTimelineState = preload("res://scripts/systems/battle_timeline_state.gd")
const BattleCellState = preload("res://scripts/systems/battle_cell_state.gd")
const BattleEdgeFeatureState = preload("res://scripts/systems/battle_edge_feature_state.gd")
const BattleBoardPropCatalog = preload("res://scripts/utils/battle_board_prop_catalog.gd")
const BattleGridService = preload("res://scripts/systems/battle_grid_service.gd")
const BattleTerrainEffectState = preload("res://scripts/systems/battle_terrain_effect_state.gd")
const BattleTerrainRules = preload("res://scripts/systems/battle_terrain_rules.gd")
const BattleUnitState = preload("res://scripts/systems/battle_unit_state.gd")
const BattleStatusEffectState = preload("res://scripts/systems/battle_status_effect_state.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const CombatSkillDef = preload("res://scripts/player/progression/combat_skill_def.gd")
const EncounterAnchorData = preload("res://scripts/systems/encounter_anchor_data.gd")
const ProgressionDataUtils = preload("res://scripts/player/progression/progression_data_utils.gd")
const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const CharacterProgressionDelta = preload("res://scripts/systems/character_progression_delta.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attribute_service.gd")

var _failures: Array[String] = []


class MasteryGatewayStub:
	extends RefCounted

	var grants: Array[Dictionary] = []
	var skill_used_events := 0

	func record_achievement_event(
		_member_id: StringName,
		event_type: StringName,
		_amount: int = 1,
		_subject_id: StringName = &"",
		_meta: Dictionary = {}
	) -> Array[StringName]:
		if event_type == &"skill_used":
			skill_used_events += 1
		return []

	func grant_battle_mastery(member_id: StringName, skill_id: StringName, amount: int) -> CharacterProgressionDelta:
		grants.append({
			"member_id": member_id,
			"skill_id": skill_id,
			"amount": amount,
		})
		var delta := CharacterProgressionDelta.new()
		delta.member_id = member_id
		delta.mastery_changes.append({
			"skill_id": skill_id,
			"mastery_amount": amount,
		})
		return delta

	func get_member_state(_member_id: StringName):
		return null


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_hit_resolver_boundary_natural_rules_are_explicit()
	_test_armor_break_lowers_target_ac_without_damage_vulnerability()
	_test_timed_terrain_processing_accepts_dictionary_keys()
	_test_start_battle_accepts_explicit_narrow_assault_profile()
	_test_start_battle_accepts_explicit_holdout_push_profile()
	_test_evaluate_move_rules_survive_stacked_columns()
	_test_move_command_executes_normally_on_stacked_columns()
	_test_runtime_reports_multistep_reachable_move_coords()
	_test_spawn_anchor_prefers_better_local_mobility_over_corner_slot()
	_test_spawn_anchor_rejects_water_start_cells()
	_test_movement_tags_override_water_traversal_rules()
	_test_height_delta_rebuilds_cell_columns()
	_test_height_delta_reclassifies_adjacent_water_component()
	_test_charge_preview_allows_impassable_first_step_and_resolves_as_stop()
	_test_charge_preview_allows_larger_first_step_blocker_and_resolves_as_stop()
	_test_charge_stops_at_larger_midpath_blocker_without_rollback()
	_test_large_unit_charge_respects_full_frontier_wall_blocking()
	_test_large_unit_charge_still_resolves_frontier_blockers()
	_test_large_unit_charge_stops_on_partial_frontier_terrain_in_all_directions()
	_test_large_unit_charge_stops_on_partial_frontier_height_in_all_directions()
	_test_large_unit_charge_stops_at_large_blockers_in_all_directions()
	_test_large_unit_charge_can_side_push_blocker()
	_test_large_unit_charge_prefers_lower_side_push_and_applies_fall_damage()
	_test_large_unit_charge_collision_kills_blocker()
	_test_large_unit_charge_force_pushes_surviving_blocker_across_height_step()
	_test_large_unit_charge_stops_when_collision_cannot_displace_blocker()
	_test_large_unit_charge_trap_stops_after_first_step()
	_test_ground_line_and_cone_skills_follow_caster_facing()
	_test_archer_multishot_uses_target_unit_ids_in_manual_order()
	_test_multi_unit_skill_uses_stable_target_order()
	_test_skill_costs_and_cooldowns_apply_in_runtime()
	_test_weapon_skill_range_uses_weapon_attack_range_not_skill_range()
	_test_battle_range_service_layers_modifiers_without_snapshot_truth()
	_test_weapon_skill_damage_tag_uses_current_weapon_type()
	_test_skill_mastery_requires_max_damage_die_or_critical_and_scales_by_enemy_rank()
	_test_ground_jump_precast_failure_does_not_consume_costs()
	_test_issue_command_flushes_battle_end_logs_to_state()
	_test_timeline_tick_uses_per_unit_action_threshold()
	_test_cooldowns_reduce_on_tu_progress_and_zero_tu_turn_switch()
	_test_status_duration_serialization_preserves_tu_window()
	_test_status_duration_blocks_target_turn_until_tu_expiry()
	if _failures.is_empty():
		print("Battle runtime smoke: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle runtime smoke: FAIL (%d)" % _failures.size())
	quit(1)


func _test_hit_resolver_boundary_natural_rules_are_explicit() -> void:
	var resolver := BattleHitResolver.new()

	var accurate_attacker := _build_unit(&"hit_boundary_accurate", Vector2i.ZERO, 1)
	accurate_attacker.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 100)
	var easy_target := _build_enemy_unit(&"hit_boundary_easy_target", Vector2i(1, 0))
	easy_target.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, -10)
	var easy_check := resolver.build_skill_attack_check(accurate_attacker, easy_target, null)
	_assert_true(int(easy_check.get("required_roll", 99)) <= 1, "低 required roll 夹具应进入天然 1 边界语义。")
	_assert_eq(int(easy_check.get("display_required_roll", 0)), 2, "低 required roll 预览应稳定显示为 2+。")
	_assert_eq(int(easy_check.get("hit_rate_percent", 0)), 95, "低 required roll 在天然 1 语义下应只保留 95% 命中。")
	_assert_true(String(easy_check.get("preview_text", "")).contains("天然 1 仍失手"), "低 required roll 预览应显式提示天然 1 失手语义。")

	var weak_attacker := _build_unit(&"hit_boundary_weak", Vector2i.ZERO, 1)
	weak_attacker.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 0)
	var evasive_target := _build_enemy_unit(&"hit_boundary_evasive_target", Vector2i(1, 0))
	evasive_target.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 100)
	var hard_check := resolver.build_skill_attack_check(weak_attacker, evasive_target, null)
	_assert_true(int(hard_check.get("required_roll", 0)) > 20, "高 required roll 夹具应进入仅天然 20 命中语义。")
	_assert_eq(int(hard_check.get("display_required_roll", 0)), 20, "高 required roll 预览应稳定显示为 20+。")
	_assert_eq(int(hard_check.get("hit_rate_percent", 0)), 5, "高 required roll 在天然 20 语义下应只保留 5% 命中。")
	_assert_true(String(hard_check.get("preview_text", "")).contains("仅天然 20"), "高 required roll 预览应显式提示天然 20 语义。")


func _test_armor_break_lowers_target_ac_without_damage_vulnerability() -> void:
	var hit_resolver := BattleHitResolver.new()
	var damage_resolver := BattleDamageResolver.new()
	var attacker := _build_unit(&"armor_break_attacker", Vector2i.ZERO, 1)
	attacker.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 4)
	var target := _build_enemy_unit(&"armor_break_target", Vector2i(1, 0))
	target.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 16)

	var baseline_check := hit_resolver.build_skill_attack_check(attacker, target, null)
	var armor_break_effect := CombatEffectDef.new()
	armor_break_effect.effect_type = &"status"
	armor_break_effect.status_id = &"armor_break"
	armor_break_effect.power = 1
	armor_break_effect.duration_tu = 90
	damage_resolver.resolve_effects(attacker, target, [armor_break_effect])
	var broken_check := hit_resolver.build_skill_attack_check(attacker, target, null)
	_assert_eq(int(broken_check.get("target_armor_class", 0)), int(baseline_check.get("target_armor_class", 0)) - 2, "armor_break power 1 应把有效 AC 降低 2。")
	_assert_eq(int(broken_check.get("hit_rate_percent", 0)), int(baseline_check.get("hit_rate_percent", 0)) + 10, "armor_break 降低 AC 后应提高 10 个百分点命中率。")

	var plain_target := _build_enemy_unit(&"plain_damage_target", Vector2i(1, 0))
	var broken_target := _build_enemy_unit(&"broken_damage_target", Vector2i(1, 0))
	damage_resolver.resolve_effects(attacker, broken_target, [armor_break_effect])
	var damage_effect := CombatEffectDef.new()
	damage_effect.effect_type = &"damage"
	damage_effect.power = 10
	var plain_result := damage_resolver.resolve_effects(attacker, plain_target, [damage_effect])
	var broken_result := damage_resolver.resolve_effects(attacker, broken_target, [damage_effect])
	_assert_eq(int(broken_result.get("damage", 0)), int(plain_result.get("damage", 0)), "armor_break 不应再提供承伤易伤倍率。")


func _test_timed_terrain_processing_accepts_dictionary_keys() -> void:
	var runtime := BattleRuntimeModule.new()
	var state := BattleState.new()
	state.battle_id = &"runtime_smoke"
	state.phase = &"timeline_running"
	state.map_size = Vector2i(2, 1)
	state.timeline = BattleTimelineState.new()
	state.timeline.current_tu = 0

	var lead_cell := _build_cell(Vector2i(1, 0))
	var trailing_cell := _build_cell(Vector2i(0, 0))
	var timed_effect := BattleTerrainEffectState.new()
	timed_effect.field_instance_id = &"smoke_field"
	timed_effect.effect_id = &"smoke_tick"
	timed_effect.tick_interval_tu = 5
	timed_effect.remaining_tu = 10
	timed_effect.next_tick_at_tu = 5
	trailing_cell.timed_terrain_effects.append(timed_effect)

	state.cells = {
		lead_cell.coord: lead_cell,
		trailing_cell.coord: trailing_cell,
	}

	runtime._state = state
	var batch = runtime.advance(1.0)
	_assert_true(batch != null, "advance() 应返回有效 batch。")
	_assert_true(
		trailing_cell.timed_terrain_effects.size() == 1,
		"timed terrain effect 在无占位单位时应稳定保留，且不应因坐标排序报错。"
	)
	_assert_true(
		trailing_cell.timed_terrain_effects[0].remaining_tu == 5,
		"timed terrain effect 应完成一次稳定 tick。"
	)


func _test_start_battle_accepts_explicit_narrow_assault_profile() -> void:
	var registry := ProgressionContentRegistry.new()
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})

	var encounter_anchor := EncounterAnchorData.new()
	encounter_anchor.entity_id = &"narrow_assault_smoke"
	encounter_anchor.display_name = "狭道突击测试"
	encounter_anchor.world_coord = Vector2i(8, 4)
	encounter_anchor.faction_id = &"hostile"
	encounter_anchor.region_tag = &"default"

	var ally_a := _build_unit(&"narrow_assault_ally_a", Vector2i.ZERO, 3)
	var ally_b := _build_unit(&"narrow_assault_ally_b", Vector2i.ZERO, 3)
	var state := runtime.start_battle(
		encounter_anchor,
		20260417,
		{
			"battle_terrain_profile": "narrow_assault",
			"battle_map_size": Vector2i(19, 11),
			"battle_party": [ally_a.to_dict(), ally_b.to_dict()],
			"enemy_unit_count": 2,
		}
	)
	_assert_true(state != null and not state.is_empty(), "BattleRuntimeModule.start_battle() 应能显式启动 narrow_assault 地形。")
	if state == null or state.is_empty():
		return

	_assert_eq(String(state.terrain_profile_id), "narrow_assault", "显式 battle_terrain_profile 应进入正式 narrow_assault battle state。")
	_assert_eq(state.map_size, Vector2i(19, 11), "显式 narrow_assault 入口应保留请求的 battle_map_size。")
	_assert_eq(state.ally_unit_ids.size(), 2, "显式 narrow_assault 入口应保留传入的 ally battle party。")
	_assert_eq(state.enemy_unit_ids.size(), 2, "显式 narrow_assault 入口应构建请求数量的敌方单位。")

	var center_x := int(state.map_size.x / 2)
	for ally_unit_id in state.ally_unit_ids:
		var ally_unit := state.units.get(ally_unit_id) as BattleUnitState
		_assert_true(ally_unit != null, "narrow_assault 入口构建后，友军单位应可从 state.units 读取。")
		if ally_unit == null:
			continue
		_assert_true(ally_unit.coord.x < center_x, "narrow_assault 入口应把友军部署在突破线左侧 staging 区。")
	for enemy_unit_id in state.enemy_unit_ids:
		var enemy_unit := state.units.get(enemy_unit_id) as BattleUnitState
		_assert_true(enemy_unit != null, "narrow_assault 入口构建后，敌军单位应可从 state.units 读取。")
		if enemy_unit == null:
			continue
		_assert_true(enemy_unit.coord.x >= center_x, "narrow_assault 入口应把敌军部署在突破线右侧 staging 区。")

	var explicit_prop_counts := _count_explicit_props(state)
	_assert_eq(
		int(explicit_prop_counts.get(BattleBoardPropCatalog.PROP_OBJECTIVE_MARKER, 0)),
		1,
		"narrow_assault 入口生成的 battle state 应保留唯一 objective marker。"
	)
	_assert_eq(
		int(explicit_prop_counts.get(BattleBoardPropCatalog.PROP_TENT, 0)),
		2,
		"narrow_assault 入口生成的 battle state 应保留双方 tent。"
	)
	_assert_eq(
		int(explicit_prop_counts.get(BattleBoardPropCatalog.PROP_TORCH, 0)),
		2,
		"narrow_assault 入口生成的 battle state 应保留左右 torch。"
	)
	_assert_true(
		_count_terrain_cells(state, BattleCellState.TERRAIN_SPIKE) >= 1,
		"narrow_assault 入口生成的 battle state 应保留突破口后的 spike kill-zone。"
	)


func _test_start_battle_accepts_explicit_holdout_push_profile() -> void:
	var registry := ProgressionContentRegistry.new()
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})

	var encounter_anchor := EncounterAnchorData.new()
	encounter_anchor.entity_id = &"holdout_push_smoke"
	encounter_anchor.display_name = "守点推进测试"
	encounter_anchor.world_coord = Vector2i(12, 6)
	encounter_anchor.faction_id = &"hostile"
	encounter_anchor.region_tag = &"default"

	var ally_a := _build_unit(&"holdout_push_ally_a", Vector2i.ZERO, 3)
	var ally_b := _build_unit(&"holdout_push_ally_b", Vector2i.ZERO, 3)
	var state := runtime.start_battle(
		encounter_anchor,
		20260417,
		{
			"battle_terrain_profile": "holdout_push",
			"battle_map_size": Vector2i(19, 11),
			"battle_party": [ally_a.to_dict(), ally_b.to_dict()],
			"enemy_unit_count": 2,
		}
	)
	_assert_true(state != null and not state.is_empty(), "BattleRuntimeModule.start_battle() 应能显式启动 holdout_push 地形。")
	if state == null or state.is_empty():
		return

	_assert_eq(String(state.terrain_profile_id), "holdout_push", "显式 battle_terrain_profile 应进入正式 holdout_push battle state。")
	_assert_eq(state.map_size, Vector2i(19, 11), "显式 holdout_push 入口应保留请求的 battle_map_size。")
	_assert_eq(state.ally_unit_ids.size(), 2, "显式 holdout_push 入口应保留传入的 ally battle party。")
	_assert_eq(state.enemy_unit_ids.size(), 2, "显式 holdout_push 入口应构建请求数量的敌方单位。")

	var objective_coords := _collect_explicit_prop_coords(state, BattleBoardPropCatalog.PROP_OBJECTIVE_MARKER)
	_assert_eq(objective_coords.size(), 1, "holdout_push 入口生成的 battle state 应保留唯一守点目标。")
	if objective_coords.size() != 1:
		return
	var objective_coord := objective_coords[0]
	_assert_true(objective_coord.x > int(state.map_size.x / 2), "holdout_push 的守点目标应位于战场右侧 holdout。")

	var ally_height_total := 0
	var enemy_height_total := 0
	for ally_unit_id in state.ally_unit_ids:
		var ally_unit := state.units.get(ally_unit_id) as BattleUnitState
		_assert_true(ally_unit != null, "holdout_push 入口构建后，友军单位应可从 state.units 读取。")
		if ally_unit == null:
			continue
		ally_height_total += _get_unit_anchor_height(state, ally_unit)
		_assert_true(ally_unit.coord.x < objective_coord.x, "holdout_push 入口应把友军部署在守点目标之前的推进侧。")
	for enemy_unit_id in state.enemy_unit_ids:
		var enemy_unit := state.units.get(enemy_unit_id) as BattleUnitState
		_assert_true(enemy_unit != null, "holdout_push 入口构建后，敌军单位应可从 state.units 读取。")
		if enemy_unit == null:
			continue
		enemy_height_total += _get_unit_anchor_height(state, enemy_unit)
		_assert_true(enemy_unit.coord.x >= objective_coord.x - 1, "holdout_push 入口应把敌军部署在守点目标附近的防守侧。")
	_assert_true(
		enemy_height_total >= ally_height_total + state.enemy_unit_ids.size(),
		"holdout_push 入口应让守军整体站在比推进方更高的 holdout 高地。"
	)

	var explicit_prop_counts := _count_explicit_props(state)
	_assert_eq(
		int(explicit_prop_counts.get(BattleBoardPropCatalog.PROP_TENT, 0)),
		2,
		"holdout_push 入口生成的 battle state 应保留双方 tent。"
	)
	_assert_eq(
		int(explicit_prop_counts.get(BattleBoardPropCatalog.PROP_TORCH, 0)),
		2,
		"holdout_push 入口生成的 battle state 应保留双方 torch。"
	)
	_assert_true(
		_count_terrain_cells(state, BattleCellState.TERRAIN_MUD) >= 2,
		"holdout_push 入口生成的 battle state 应保留推进侧泥地减速带。"
	)
	_assert_true(
		_count_terrain_cells(state, BattleCellState.TERRAIN_SPIKE) >= 2,
		"holdout_push 入口生成的 battle state 应保留守点正面的 spike barricade 区域。"
	)


func _build_cell(coord: Vector2i) -> BattleCellState:
	var cell := BattleCellState.new()
	cell.coord = coord
	cell.base_terrain = BattleCellState.TERRAIN_LAND
	cell.base_height = 4
	cell.height_offset = 0
	cell.recalculate_runtime_values()
	return cell


func _test_evaluate_move_rules_survive_stacked_columns() -> void:
	var grid_service := BattleGridService.new()
	var state := BattleState.new()
	state.battle_id = &"move_rules_smoke"
	state.phase = &"unit_acting"
	state.map_size = Vector2i(3, 1)
	state.timeline = BattleTimelineState.new()
	state.cells = {
		Vector2i(0, 0): _build_cell(Vector2i(0, 0)),
		Vector2i(1, 0): _build_cell(Vector2i(1, 0)),
		Vector2i(2, 0): _build_cell(Vector2i(2, 0)),
	}
	state.cells[Vector2i(2, 0)].base_height = 6
	state.cells[Vector2i(2, 0)].recalculate_runtime_values()
	state.cells[Vector2i(1, 0)].set_edge_feature(Vector2i.RIGHT, BattleEdgeFeatureState.make_wall())
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)

	var unit := _build_unit(&"move_smoke_unit", Vector2i(0, 0), 3)
	state.units[unit.unit_id] = unit
	state.ally_unit_ids = [unit.unit_id]
	state.active_unit_id = unit.unit_id
	_assert_true(grid_service.place_unit(state, unit, Vector2i(0, 0), true), "移动规则测试单位应成功放入起点。")

	var flat_move := grid_service.evaluate_move(state, Vector2i(0, 0), Vector2i(1, 0), unit)
	_assert_true(bool(flat_move.get("allowed", false)), "真堆叠列改造后，平地相邻移动仍应允许。")
	_assert_true(grid_service.move_unit_force(state, unit, Vector2i(1, 0)), "移动规则测试单位应能被重定位到中间格继续验证后续规则。")

	var blocked_by_wall := grid_service.evaluate_move(state, Vector2i(1, 0), Vector2i(2, 0), unit)
	_assert_true(not bool(blocked_by_wall.get("allowed", false)), "真堆叠列改造后，墙阻挡规则仍应生效。")

	state.cells[Vector2i(1, 0)].clear_edge_feature(Vector2i.RIGHT)
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)
	var blocked_by_height := grid_service.evaluate_move(state, Vector2i(1, 0), Vector2i(2, 0), unit)
	_assert_true(not bool(blocked_by_height.get("allowed", false)), "真堆叠列改造后，高差超过 1 的移动仍应被禁止。")


func _test_move_command_executes_normally_on_stacked_columns() -> void:
	var runtime := BattleRuntimeModule.new()
	var state := BattleState.new()
	state.battle_id = &"move_command_smoke"
	state.phase = &"unit_acting"
	state.map_size = Vector2i(2, 1)
	state.timeline = BattleTimelineState.new()
	state.cells = {
		Vector2i(0, 0): _build_cell(Vector2i(0, 0)),
		Vector2i(1, 0): _build_cell(Vector2i(1, 0)),
	}
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)

	var unit := _build_unit(&"runtime_move_unit", Vector2i(0, 0), 3)
	state.units[unit.unit_id] = unit
	state.ally_unit_ids = [unit.unit_id]
	state.active_unit_id = unit.unit_id
	_assert_true(runtime._grid_service.place_unit(state, unit, Vector2i(0, 0), true), "runtime move 测试单位应成功放入起点。")
	runtime._state = state

	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_MOVE
	command.unit_id = unit.unit_id
	command.target_coord = Vector2i(1, 0)
	var batch := runtime.issue_command(command)
	_assert_true(unit.coord == Vector2i(1, 0), "issue_command(move) 在真堆叠列地图上仍应更新单位坐标。")
	_assert_true(unit.current_move_points == 1, "issue_command(move) 在真堆叠列地图上仍应按移动消耗扣除行动点。")
	_assert_true(unit.current_ap == 3, "普通移动改走行动点后，不应再扣除 AP。")
	_assert_true(batch.changed_unit_ids.has(unit.unit_id), "移动批次仍应记录变更单位。")
	_assert_true(state.cells[Vector2i(1, 0)].occupant_unit_id == unit.unit_id, "目标地格占位应在移动后同步更新。")


func _test_runtime_reports_multistep_reachable_move_coords() -> void:
	var runtime := BattleRuntimeModule.new()
	var state := BattleState.new()
	state.battle_id = &"move_reachable_smoke"
	state.phase = &"unit_acting"
	state.map_size = Vector2i(4, 2)
	state.timeline = BattleTimelineState.new()
	state.cells = {}
	for y in range(state.map_size.y):
		for x in range(state.map_size.x):
			state.cells[Vector2i(x, y)] = _build_cell(Vector2i(x, y))
	var mud_cell := state.cells.get(Vector2i(1, 0)) as BattleCellState
	if mud_cell != null:
		mud_cell.base_terrain = BattleCellState.TERRAIN_MUD
		mud_cell.recalculate_runtime_values()
	var blocked_cell := state.cells.get(Vector2i(3, 1)) as BattleCellState
	if blocked_cell != null:
		blocked_cell.base_terrain = BattleCellState.TERRAIN_DEEP_WATER
		blocked_cell.recalculate_runtime_values()
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)

	var unit := _build_unit(&"move_reachable_unit", Vector2i(0, 0), 2)
	state.units[unit.unit_id] = unit
	state.ally_unit_ids = [unit.unit_id]
	state.active_unit_id = unit.unit_id
	_assert_true(runtime._grid_service.place_unit(state, unit, unit.coord, true), "移动范围测试单位应成功放入起点。")
	runtime._state = state

	var reachable_coords := runtime.get_unit_reachable_move_coords(unit)
	_assert_true(reachable_coords.has(Vector2i(0, 1)), "可达集应包含一步可达的 land 地格。")
	_assert_true(reachable_coords.has(Vector2i(1, 1)), "可达集应包含两步可达地格，而不只是相邻地格。")
	_assert_true(reachable_coords.has(Vector2i(1, 0)), "可达集应包含花费 2 点行动点的泥地。")
	_assert_true(not reachable_coords.has(Vector2i(2, 0)), "穿过泥地后超出行动点预算的地格不应进入可达集。")
	_assert_true(not reachable_coords.has(Vector2i(3, 1)), "不可通行的水域不应进入可达集。")

	var move_command := BattleCommand.new()
	move_command.command_type = BattleCommand.TYPE_MOVE
	move_command.unit_id = unit.unit_id
	move_command.target_coord = Vector2i(1, 1)
	var preview := runtime.preview_command(move_command)
	_assert_true(preview.allowed, "两步内蓝色可达格应允许普通移动预览。")

	var batch := runtime.issue_command(move_command)
	_assert_true(unit.coord == Vector2i(1, 1), "issue_command(move) 应允许直接移动到两步内可达终点。")
	_assert_true(unit.current_move_points == 0, "多步移动后应累计扣除整条路径消耗。")
	_assert_true(batch.changed_unit_ids.has(unit.unit_id), "多步移动批次应记录变更单位。")
	_assert_true(state.cells[Vector2i(1, 1)].occupant_unit_id == unit.unit_id, "多步移动后目标地格占位应同步更新。")


func _test_spawn_anchor_prefers_better_local_mobility_over_corner_slot() -> void:
	var runtime := BattleRuntimeModule.new()
	var state := BattleState.new()
	state.battle_id = &"spawn_anchor_mobility_smoke"
	state.phase = &"timeline_running"
	state.map_size = Vector2i(4, 4)
	state.timeline = BattleTimelineState.new()
	state.cells = {}
	for y in range(state.map_size.y):
		for x in range(state.map_size.x):
			state.cells[Vector2i(x, y)] = _build_cell(Vector2i(x, y))
	var blocked_corner_exit := state.cells.get(Vector2i(0, 2)) as BattleCellState
	if blocked_corner_exit != null:
		blocked_corner_exit.base_terrain = BattleCellState.TERRAIN_DEEP_WATER
		blocked_corner_exit.recalculate_runtime_values()
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)
	runtime._state = state

	var unit := _build_unit(&"spawn_anchor_unit", Vector2i.ZERO, 7)
	var preferred_coords: Array[Vector2i] = [
		Vector2i(0, 3),
		Vector2i(1, 3),
		Vector2i(0, 2),
	]
	var chosen_coord := runtime._find_spawn_anchor(unit, preferred_coords)
	_assert_eq(
		chosen_coord,
		Vector2i(1, 3),
		"spawn ring 含角落死角时，运行时应优先选择局部机动空间更大的出生格。"
	)


func _test_spawn_anchor_rejects_water_start_cells() -> void:
	var runtime := BattleRuntimeModule.new()
	var state := BattleState.new()
	state.battle_id = &"spawn_anchor_water_smoke"
	state.phase = &"timeline_running"
	state.map_size = Vector2i(3, 2)
	state.timeline = BattleTimelineState.new()
	state.cells = {}
	for y in range(state.map_size.y):
		for x in range(state.map_size.x):
			state.cells[Vector2i(x, y)] = _build_cell(Vector2i(x, y))
	var shallow_water_cell := state.cells.get(Vector2i(1, 0)) as BattleCellState
	if shallow_water_cell != null:
		shallow_water_cell.base_terrain = BattleCellState.TERRAIN_SHALLOW_WATER
		shallow_water_cell.recalculate_runtime_values()
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)
	runtime._state = state

	var unit := _build_unit(&"spawn_anchor_water_unit", Vector2i.ZERO, 4)
	var preferred_coords: Array[Vector2i] = [
		Vector2i(1, 0),
		Vector2i(0, 0),
		Vector2i(2, 0),
	]
	var chosen_coord := runtime._find_spawn_anchor(unit, preferred_coords)
	_assert_eq(
		chosen_coord,
		Vector2i(0, 0),
		"战斗起始锚点即使可通行，也不应落在浅水等水域地格上。"
	)


func _test_movement_tags_override_water_traversal_rules() -> void:
	var grid_service := BattleGridService.new()
	var lane_state := BattleState.new()
	lane_state.battle_id = &"water_tags_smoke"
	lane_state.phase = &"unit_acting"
	lane_state.map_size = Vector2i(4, 1)
	lane_state.timeline = BattleTimelineState.new()
	lane_state.cells = {
		Vector2i(0, 0): _build_cell(Vector2i(0, 0)),
		Vector2i(1, 0): _build_cell(Vector2i(1, 0)),
		Vector2i(2, 0): _build_cell(Vector2i(2, 0)),
		Vector2i(3, 0): _build_cell(Vector2i(3, 0)),
	}
	(lane_state.cells.get(Vector2i(1, 0)) as BattleCellState).base_terrain = BattleCellState.TERRAIN_SHALLOW_WATER
	(lane_state.cells.get(Vector2i(2, 0)) as BattleCellState).base_terrain = BattleCellState.TERRAIN_FLOWING_WATER
	(lane_state.cells.get(Vector2i(3, 0)) as BattleCellState).base_terrain = BattleCellState.TERRAIN_DEEP_WATER
	for cell_variant in lane_state.cells.values():
		var lane_cell := cell_variant as BattleCellState
		if lane_cell != null:
			lane_cell.recalculate_runtime_values()
	lane_state.cell_columns = BattleCellState.build_columns_from_surface_cells(lane_state.cells)

	var default_unit := _build_unit(&"default_water_unit", Vector2i.ZERO, 3)
	default_unit.movement_tags = []
	lane_state.units[default_unit.unit_id] = default_unit
	lane_state.ally_unit_ids = [default_unit.unit_id]
	lane_state.active_unit_id = default_unit.unit_id
	_assert_true(grid_service.place_unit(lane_state, default_unit, default_unit.coord, true), "默认地面单位应成功放入起点。")
	_assert_true(
		grid_service.evaluate_move(lane_state, Vector2i.ZERO, Vector2i(1, 0), default_unit).get("allowed", false),
		"默认地面单位应能进入浅水。"
	)
	_assert_true(
		not grid_service.can_unit_enter_coord(lane_state, Vector2i(3, 0), default_unit),
		"默认地面单位不应进入深水。"
	)

	var wade_unit := _build_unit(&"wade_water_unit", Vector2i.ZERO, 3)
	wade_unit.movement_tags = [BattleTerrainRules.TAG_WADE]
	_assert_eq(grid_service.get_unit_move_cost(lane_state, wade_unit, Vector2i(1, 0)), 1, "涉水单位进入浅水应只消耗 1 点行动点。")
	_assert_eq(grid_service.get_unit_move_cost(lane_state, wade_unit, Vector2i(2, 0)), 2, "涉水单位进入流水应消耗 2 点行动点。")

	var amphibious_state := BattleState.new()
	amphibious_state.battle_id = &"amphibious_water_unit"
	amphibious_state.phase = &"unit_acting"
	amphibious_state.map_size = Vector2i(2, 1)
	amphibious_state.timeline = BattleTimelineState.new()
	amphibious_state.cells = {
		Vector2i(0, 0): _build_cell(Vector2i(0, 0)),
		Vector2i(1, 0): _build_cell(Vector2i(1, 0)),
	}
	(amphibious_state.cells.get(Vector2i(1, 0)) as BattleCellState).base_terrain = BattleCellState.TERRAIN_DEEP_WATER
	for cell_variant in amphibious_state.cells.values():
		var amphibious_cell := cell_variant as BattleCellState
		if amphibious_cell != null:
			amphibious_cell.recalculate_runtime_values()
	amphibious_state.cell_columns = BattleCellState.build_columns_from_surface_cells(amphibious_state.cells)
	var amphibious_unit := _build_unit(&"amphibious_unit", Vector2i.ZERO, 2)
	amphibious_unit.movement_tags = [BattleTerrainRules.TAG_AMPHIBIOUS]
	amphibious_state.units[amphibious_unit.unit_id] = amphibious_unit
	amphibious_state.ally_unit_ids = [amphibious_unit.unit_id]
	amphibious_state.active_unit_id = amphibious_unit.unit_id
	_assert_true(grid_service.place_unit(amphibious_state, amphibious_unit, amphibious_unit.coord, true), "两栖单位应成功放入起点。")
	_assert_true(
		bool(grid_service.evaluate_move(amphibious_state, Vector2i.ZERO, Vector2i(1, 0), amphibious_unit).get("allowed", false)),
		"两栖单位应能进入深水。"
	)


func _test_height_delta_rebuilds_cell_columns() -> void:
	var grid_service := BattleGridService.new()
	var state := BattleState.new()
	state.battle_id = &"height_delta_smoke"
	state.phase = &"timeline_running"
	state.map_size = Vector2i(1, 1)
	state.timeline = BattleTimelineState.new()
	var cell := _build_cell(Vector2i.ZERO)
	state.cells = {Vector2i.ZERO: cell}
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)

	var before_column := state.cell_columns.get(Vector2i.ZERO, []) as Array
	_assert_true(before_column.size() == 5, "初始高度 4 的地格应展开成 5 层真实堆叠 cell。")
	var result := grid_service.apply_height_delta_result(state, Vector2i.ZERO, 1)
	_assert_true(bool(result.get("changed", false)), "高度变化在真堆叠列地图上应仍可生效。")
	var after_column := state.cell_columns.get(Vector2i.ZERO, []) as Array
	_assert_true(after_column.size() == 6, "高度增加 1 后，真实堆叠 cell 列数量应同步增加。")
	_assert_true(int(state.cells[Vector2i.ZERO].current_height) == 5, "surface cache 顶层高度应与真实堆叠列同步。")


func _test_height_delta_reclassifies_adjacent_water_component() -> void:
	var runtime := BattleRuntimeModule.new()
	var state := BattleState.new()
	state.battle_id = &"water_reclassify_smoke"
	state.phase = &"unit_acting"
	state.map_size = Vector2i(3, 3)
	state.timeline = BattleTimelineState.new()
	state.cells = {}
	for y in range(state.map_size.y):
		for x in range(state.map_size.x):
			var cell := _build_cell(Vector2i(x, y))
			cell.base_height = 5
			cell.recalculate_runtime_values()
			state.cells[cell.coord] = cell
	var center_water := state.cells.get(Vector2i(1, 1)) as BattleCellState
	center_water.base_terrain = BattleCellState.TERRAIN_DEEP_WATER
	center_water.base_height = 4
	center_water.recalculate_runtime_values()
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)
	runtime._state = state

	var effect_def := CombatEffectDef.new()
	effect_def.effect_type = &"height_delta"
	effect_def.height_delta = -1
	var batch := BattleEventBatch.new()
	var applied := runtime._apply_ground_terrain_effects(null, null, [effect_def], [Vector2i(1, 0)], batch)
	_assert_true(bool(applied.get("applied", false)), "降低堤岸后应触发邻近水域重分类。")
	_assert_eq(
		center_water.base_terrain,
		BattleCellState.TERRAIN_FLOWING_WATER,
		"当相邻地格降低到水面时，封闭水域应重分类为流水。"
	)
	_assert_eq(center_water.flow_direction, Vector2i.UP, "被击穿的流水应记录通向出口的流向。")


func _test_charge_preview_allows_impassable_first_step_and_resolves_as_stop() -> void:
	var registry := ProgressionContentRegistry.new()
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})

	var state := _build_skill_test_state(Vector2i(5, 1))
	var blocked_cell := state.cells.get(Vector2i(1, 0)) as BattleCellState
	if blocked_cell != null:
		blocked_cell.base_terrain = BattleCellState.TERRAIN_DEEP_WATER
		blocked_cell.recalculate_runtime_values()
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)

	var charger := _build_unit(&"charge_blocked_by_terrain", Vector2i.ZERO, 1)
	charger.known_active_skill_ids = [&"charge"]
	charger.known_skill_level_map = {&"charge": 1}
	state.units = {charger.unit_id: charger}
	state.ally_unit_ids = [charger.unit_id]
	state.active_unit_id = charger.unit_id
	_assert_true(runtime._grid_service.place_unit(state, charger, charger.coord, true), "冲锋测试单位应能成功放入起点。")
	runtime._state = state

	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = charger.unit_id
	command.skill_id = &"charge"
	command.target_coord = Vector2i(3, 0)

	var preview := runtime.preview_command(command)
	_assert_true(preview != null and preview.allowed, "首步被不可通行地形阻挡时，冲锋预览仍应允许尝试。")
	_assert_eq(
		preview.resolved_anchor_coord if preview != null else Vector2i(-1, -1),
		Vector2i.ZERO,
		"首步被地形阻挡时，charge preview 应暴露原地停下的 resolved_anchor_coord。"
	)

	var batch := runtime.issue_command(command)
	_assert_eq(charger.coord, Vector2i.ZERO, "首步被地形阻挡时，冲锋应原地停下。")
	_assert_eq(charger.current_ap, 0, "首步被地形阻挡时，冲锋仍应按 stop 流程消耗 AP。")
	_assert_true(
		batch.log_lines.any(func(line): return String(line).contains("起步时被拦下")),
		"首步被地形阻挡时，日志应明确记录这是一次起步即停止的冲锋。 log=%s" % [str(batch.log_lines)]
	)


func _test_charge_preview_allows_larger_first_step_blocker_and_resolves_as_stop() -> void:
	var registry := ProgressionContentRegistry.new()
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})

	var state := _build_skill_test_state(Vector2i(5, 3))
	var charger := _build_unit(&"charge_blocked_by_unit", Vector2i.ZERO, 1)
	charger.known_active_skill_ids = [&"charge"]
	charger.known_skill_level_map = {&"charge": 1}
	var blocker := _build_enemy_unit(&"charge_large_blocker", Vector2i(1, 0))
	blocker.body_size = 3
	blocker.refresh_footprint()

	state.units = {
		charger.unit_id: charger,
		blocker.unit_id: blocker,
	}
	state.ally_unit_ids = [charger.unit_id]
	state.enemy_unit_ids = [blocker.unit_id]
	state.active_unit_id = charger.unit_id
	_assert_true(runtime._grid_service.place_unit(state, charger, charger.coord, true), "冲锋测试单位应能成功放入起点。")
	_assert_true(runtime._grid_service.place_unit(state, blocker, blocker.coord, true), "大型阻挡单位应能成功放入测试战场。")
	runtime._state = state

	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = charger.unit_id
	command.skill_id = &"charge"
	command.target_coord = Vector2i(4, 0)

	var preview := runtime.preview_command(command)
	_assert_true(preview != null and preview.allowed, "首步被更大体型单位阻挡时，冲锋预览仍应允许尝试。")
	_assert_eq(
		preview.resolved_anchor_coord if preview != null else Vector2i(-1, -1),
		Vector2i.ZERO,
		"首步被更大体型单位阻挡时，charge preview 应暴露原地停下的 resolved_anchor_coord。"
	)

	var batch := runtime.issue_command(command)
	_assert_eq(charger.coord, Vector2i.ZERO, "首步被更大体型单位阻挡时，冲锋应原地停下。")
	_assert_eq(charger.current_ap, 0, "首步被更大体型单位阻挡时，冲锋仍应按 stop 流程消耗 AP。")
	_assert_true(
		batch.log_lines.any(func(line): return String(line).contains("起步时被拦下")),
		"首步被更大体型单位阻挡时，日志应明确记录这是一次起步即停止的冲锋。 log=%s" % [str(batch.log_lines)]
	)


func _test_charge_stops_at_larger_midpath_blocker_without_rollback() -> void:
	var registry := ProgressionContentRegistry.new()
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})

	var state := _build_skill_test_state(Vector2i(6, 3))
	var charger := _build_unit(&"charge_midpath_blocked", Vector2i.ZERO, 1)
	charger.known_active_skill_ids = [&"charge"]
	charger.known_skill_level_map = {&"charge": 1}
	var blocker := _build_enemy_unit(&"charge_midpath_large_blocker", Vector2i(2, 0))
	blocker.body_size = 3
	blocker.refresh_footprint()

	state.units = {
		charger.unit_id: charger,
		blocker.unit_id: blocker,
	}
	state.ally_unit_ids = [charger.unit_id]
	state.enemy_unit_ids = [blocker.unit_id]
	state.active_unit_id = charger.unit_id
	_assert_true(runtime._grid_service.place_unit(state, charger, charger.coord, true), "中途阻挡测试中的冲锋单位应能成功放入起点。")
	_assert_true(runtime._grid_service.place_unit(state, blocker, blocker.coord, true), "中途阻挡测试中的大型单位应能成功放入战场。")
	runtime._state = state

	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = charger.unit_id
	command.skill_id = &"charge"
	command.target_coord = Vector2i(4, 0)

	var preview := runtime.preview_command(command)
	_assert_true(preview != null and preview.allowed, "中途才遇到更大体型单位时，冲锋预览应允许尝试。")
	_assert_eq(
		preview.resolved_anchor_coord if preview != null else Vector2i(-1, -1),
		Vector2i(1, 0),
		"中途受阻时，charge preview 应暴露已完成位移后的 resolved_anchor_coord。"
	)

	var batch := runtime.issue_command(command)
	_assert_eq(charger.coord, Vector2i(1, 0), "中途被更大体型单位拦住时，应保留已完成的前进一步而不是回退。")
	_assert_eq(charger.current_ap, 0, "中途被更大体型单位拦住时，冲锋仍应消耗 AP。")
	_assert_true(
		batch.log_lines.any(func(line): return String(line).contains("更大体型")),
		"中途被更大体型单位拦住时，日志应给出明确原因。 log=%s" % [str(batch.log_lines)]
	)


func _test_large_unit_charge_respects_full_frontier_wall_blocking() -> void:
	var registry := ProgressionContentRegistry.new()
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})

	var state := _build_skill_test_state(Vector2i(5, 3))
	(state.cells.get(Vector2i(1, 0)) as BattleCellState).set_edge_feature(Vector2i.RIGHT, BattleEdgeFeatureState.make_wall())
	(state.cells.get(Vector2i(1, 1)) as BattleCellState).set_edge_feature(Vector2i.RIGHT, BattleEdgeFeatureState.make_wall())
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)

	var charger := _build_unit(&"charge_large_unit", Vector2i.ZERO, 1)
	charger.body_size = 3
	charger.refresh_footprint()
	charger.known_active_skill_ids = [&"charge"]
	charger.known_skill_level_map = {&"charge": 1}
	state.units = {charger.unit_id: charger}
	state.ally_unit_ids = [charger.unit_id]
	state.active_unit_id = charger.unit_id
	_assert_true(runtime._grid_service.place_unit(state, charger, charger.coord, true), "2x2 冲锋测试单位应能成功放入起点。")
	runtime._state = state

	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = charger.unit_id
	command.skill_id = &"charge"
	command.target_coord = Vector2i(4, 0)

	var preview := runtime.preview_command(command)
	_assert_true(preview != null and preview.allowed, "2x2 单位的冲锋预览在首步被整条前沿墙阻挡时仍应允许尝试。")

	var batch := runtime.issue_command(command)
	_assert_eq(charger.coord, Vector2i.ZERO, "2x2 单位冲锋时应检查整条前沿边，不能穿过只挡前沿列的墙。")
	_assert_eq(charger.current_ap, 0, "2x2 单位首步被墙挡住时，冲锋仍应按 stop 流程消耗 AP。")
	_assert_true(
		batch.log_lines.any(func(line): return String(line).contains("起步时被拦下")),
		"2x2 单位首步被墙挡住时，日志应记录起步即停止。 log=%s" % [str(batch.log_lines)]
	)


func _test_large_unit_charge_still_resolves_frontier_blockers() -> void:
	var registry := ProgressionContentRegistry.new()
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})

	var state := _build_skill_test_state(Vector2i(6, 3))
	var charger := _build_unit(&"charge_large_unit_vs_blocker", Vector2i.ZERO, 1)
	charger.body_size = 3
	charger.refresh_footprint()
	charger.known_active_skill_ids = [&"charge"]
	charger.known_skill_level_map = {&"charge": 1}
	var blocker := _build_enemy_unit(&"charge_frontier_blocker", Vector2i(2, 0))

	state.units = {
		charger.unit_id: charger,
		blocker.unit_id: blocker,
	}
	state.ally_unit_ids = [charger.unit_id]
	state.enemy_unit_ids = [blocker.unit_id]
	state.active_unit_id = charger.unit_id
	_assert_true(runtime._grid_service.place_unit(state, charger, charger.coord, true), "2x2 冲锋单位应能成功放入起点。")
	_assert_true(runtime._grid_service.place_unit(state, blocker, blocker.coord, true), "2x2 冲锋前沿的阻挡单位应能成功放入战场。")
	runtime._state = state

	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = charger.unit_id
	command.skill_id = &"charge"
	command.target_coord = Vector2i(4, 0)

	var preview := runtime.preview_command(command)
	_assert_true(preview != null and preview.allowed, "2x2 单位前沿有阻挡单位时，冲锋预览仍应允许尝试。")

	var batch := runtime.issue_command(command)
	_assert_eq(charger.coord, Vector2i(3, 0), "2x2 单位遇到前沿 1x1 阻挡时，仍应进入推挤分支并继续完成冲锋。")
	_assert_eq(blocker.coord, Vector2i(5, 0), "2x2 单位的前沿阻挡应被持续向前顶开，而不是被误判为地形阻挡。")
	_assert_true(
		batch.log_lines.any(func(line): return String(line).contains("向前顶开")),
		"2x2 单位冲锋遇到前沿阻挡时，日志应记录推挤而不是地形停步。 log=%s" % [str(batch.log_lines)]
	)


func _test_large_unit_charge_stops_on_partial_frontier_terrain_in_all_directions() -> void:
	for case_data in _get_large_charge_direction_cases():
		var fixture := _build_large_charge_fixture(case_data, true)
		var runtime := fixture.get("runtime") as BattleRuntimeModule
		var state := fixture.get("state") as BattleState
		var charger := fixture.get("charger") as BattleUnitState
		var command := fixture.get("command") as BattleCommand
		var blocked_coord: Vector2i = case_data.get("partial_frontier_coord", Vector2i.ZERO)
		var blocked_cell := state.cells.get(blocked_coord) as BattleCellState
		if blocked_cell != null:
			blocked_cell.base_terrain = BattleCellState.TERRAIN_DEEP_WATER
			blocked_cell.recalculate_runtime_values()
		state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)
		runtime._state = state

		var preview := runtime.preview_command(command)
		_assert_true(preview != null and preview.allowed, "2x2 单位在%s方向首步有单格不可通行地形时，冲锋预览仍应允许尝试。" % case_data.get("label", "未知"))

		var batch := runtime.issue_command(command)
		_assert_eq(charger.coord, case_data.get("start_coord", Vector2i.ZERO), "2x2 单位在%s方向首步只有半个前沿不可通行时，也应整段停下。" % case_data.get("label", "未知"))
		_assert_eq(charger.current_ap, 0, "2x2 单位在%s方向首步被单格不可通行地形拦住时，冲锋仍应消耗 AP。" % case_data.get("label", "未知"))
		_assert_true(
			batch.log_lines.any(func(line): return String(line).contains("起步时被拦下")),
			"2x2 单位在%s方向首步被单格不可通行地形拦住时，应记录起步即停止。 log=%s" % [case_data.get("label", "未知"), str(batch.log_lines)]
		)


func _test_large_unit_charge_stops_on_partial_frontier_height_in_all_directions() -> void:
	for case_data in _get_large_charge_direction_cases():
		var fixture := _build_large_charge_fixture(case_data, true)
		var runtime := fixture.get("runtime") as BattleRuntimeModule
		var state := fixture.get("state") as BattleState
		var charger := fixture.get("charger") as BattleUnitState
		var command := fixture.get("command") as BattleCommand
		_set_cell_height(state, case_data.get("partial_frontier_coord", Vector2i.ZERO), 7)
		state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)
		runtime._state = state

		var preview := runtime.preview_command(command)
		_assert_true(preview != null and preview.allowed, "2x2 单位在%s方向首步有单格高差过大时，冲锋预览仍应允许尝试。" % case_data.get("label", "未知"))

		var batch := runtime.issue_command(command)
		_assert_eq(charger.coord, case_data.get("start_coord", Vector2i.ZERO), "2x2 单位在%s方向首步只有半个前沿高差过大时，也应整段停下。" % case_data.get("label", "未知"))
		_assert_eq(charger.current_ap, 0, "2x2 单位在%s方向首步被单格高差拦住时，冲锋仍应消耗 AP。" % case_data.get("label", "未知"))
		_assert_true(
			batch.log_lines.any(func(line): return String(line).contains("起步时被拦下")),
			"2x2 单位在%s方向首步被单格高差拦住时，应记录起步即停止。 log=%s" % [case_data.get("label", "未知"), str(batch.log_lines)]
		)


func _test_large_unit_charge_stops_at_large_blockers_in_all_directions() -> void:
	for case_data in _get_large_charge_direction_cases():
		var fixture := _build_large_charge_fixture(case_data, true)
		var runtime := fixture.get("runtime") as BattleRuntimeModule
		var state := fixture.get("state") as BattleState
		var charger := fixture.get("charger") as BattleUnitState
		var command := fixture.get("command") as BattleCommand
		var blocker_id := StringName("charge_large_blocker_%s" % String(case_data.get("label", "dir")))
		var blocker := _build_enemy_unit(blocker_id, case_data.get("large_blocker_anchor", Vector2i.ZERO))
		blocker.body_size = 3
		blocker.refresh_footprint()
		state.units[blocker.unit_id] = blocker
		state.enemy_unit_ids.append(blocker.unit_id)
		_assert_true(runtime._grid_service.place_unit(state, blocker, blocker.coord, true), "2x2 冲锋在%s方向的大体型阻挡单位应能成功放入战场。" % case_data.get("label", "未知"))
		runtime._state = state

		var preview := runtime.preview_command(command)
		_assert_true(preview != null and preview.allowed, "2x2 单位在%s方向首步遇到 2x2 阻挡时，冲锋预览仍应允许尝试。" % case_data.get("label", "未知"))

		var batch := runtime.issue_command(command)
		_assert_eq(charger.coord, case_data.get("start_coord", Vector2i.ZERO), "2x2 单位在%s方向首步遇到另一名 2x2 单位时，应停在原地。" % case_data.get("label", "未知"))
		_assert_true(
			batch.log_lines.any(func(line): return String(line).contains("无法继续冲锋")),
			"2x2 单位在%s方向首步遇到另一名 2x2 单位时，应记录大体型阻挡原因。 log=%s" % [case_data.get("label", "未知"), str(batch.log_lines)]
		)


func _test_large_unit_charge_can_side_push_blocker() -> void:
	var case_data: Dictionary = _get_large_charge_direction_cases()[0]
	var fixture := _build_large_charge_fixture(case_data, true)
	var runtime := fixture.get("runtime") as BattleRuntimeModule
	var state := fixture.get("state") as BattleState
	var charger := fixture.get("charger") as BattleUnitState
	var command := fixture.get("command") as BattleCommand
	var blocker := _build_enemy_unit(&"charge_side_push_blocker", case_data.get("side_push_blocker_coord", Vector2i.ZERO))
	state.units[blocker.unit_id] = blocker
	state.enemy_unit_ids.append(blocker.unit_id)
	_assert_true(runtime._grid_service.place_unit(state, blocker, blocker.coord, true), "2x2 侧推分支的阻挡单位应能成功放入战场。")
	runtime._state = state

	var batch := runtime.issue_command(command)
	_assert_eq(charger.coord, case_data.get("first_anchor", Vector2i.ZERO), "2x2 单位在首步发生侧推后，应完成本次前进一步。")
	_assert_eq(blocker.coord, case_data.get("side_push_coord", Vector2i.ZERO), "2x2 单位在首步遇到偏置前沿阻挡时，应把阻挡单位顶向侧面。")
	_assert_true(
		batch.log_lines.any(func(line): return String(line).contains("顶向侧面")),
		"2x2 单位触发侧推时，应记录侧推日志。 log=%s" % [str(batch.log_lines)]
	)


func _test_large_unit_charge_prefers_lower_side_push_and_applies_fall_damage() -> void:
	var case_data: Dictionary = _get_large_charge_direction_cases()[0]
	var fixture := _build_large_charge_fixture(case_data, true)
	var runtime := fixture.get("runtime") as BattleRuntimeModule
	var state := fixture.get("state") as BattleState
	var command := fixture.get("command") as BattleCommand
	var blocker := _build_enemy_unit(&"charge_side_push_fall_blocker", case_data.get("side_push_blocker_coord", Vector2i.ZERO))
	state.units[blocker.unit_id] = blocker
	state.enemy_unit_ids.append(blocker.unit_id)
	_assert_true(runtime._grid_service.place_unit(state, blocker, blocker.coord, true), "2x2 侧推跌落分支的阻挡单位应能成功放入战场。")
	_set_cell_height(state, case_data.get("side_push_coord", Vector2i.ZERO), 2)
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)
	runtime._state = state

	var batch := runtime.issue_command(command)
	_assert_eq(blocker.coord, case_data.get("side_push_coord", Vector2i.ZERO), "2x2 单位在首步侧推时，应优先把阻挡单位顶向更低的侧向地格。")
	_assert_eq(blocker.current_hp, 26, "2x2 单位把阻挡单位侧推下两层时，应结算坠落伤害。")
	_assert_true(
		batch.log_lines.any(func(line): return String(line).contains("坠落伤害")),
		"2x2 单位触发侧推跌落时，应记录坠落伤害日志。 log=%s" % [str(batch.log_lines)]
	)


func _test_large_unit_charge_collision_kills_blocker() -> void:
	var case_data: Dictionary = _get_large_charge_direction_cases()[0]
	var fixture := _build_large_charge_fixture(case_data, true)
	var runtime := fixture.get("runtime") as BattleRuntimeModule
	var state := fixture.get("state") as BattleState
	var charger := fixture.get("charger") as BattleUnitState
	var command := fixture.get("command") as BattleCommand
	var blocker := _build_enemy_unit(&"charge_collision_kill_blocker", case_data.get("forward_blocker_coord", Vector2i.ZERO))
	var side_guard := _build_enemy_unit(&"charge_collision_side_guard", Vector2i(3, 1))
	state.units[blocker.unit_id] = blocker
	state.units[side_guard.unit_id] = side_guard
	state.enemy_unit_ids.append(blocker.unit_id)
	state.enemy_unit_ids.append(side_guard.unit_id)
	_assert_true(runtime._grid_service.place_unit(state, blocker, blocker.coord, true), "2x2 碰撞击倒分支的阻挡单位应能成功放入战场。")
	_assert_true(runtime._grid_service.place_unit(state, side_guard, side_guard.coord, true), "2x2 碰撞击倒分支的侧向阻挡单位应能成功放入战场。")
	_set_cell_height(state, case_data.get("forward_coord", Vector2i.ZERO), 7)
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)
	runtime._state = state

	var batch := runtime.issue_command(command)
	_assert_eq(charger.coord, case_data.get("first_anchor", Vector2i.ZERO), "2x2 单位撞倒阻挡后，应完成本次前进一步。")
	_assert_true(not blocker.is_alive and blocker.current_hp == 0, "2x2 单位发生碰撞击倒时，应清除阻挡单位的存活状态。")
	_assert_true(
		batch.log_lines.any(func(line): return String(line).contains("撞上")) and batch.log_lines.any(func(line): return String(line).contains("被击倒")),
		"2x2 单位撞倒阻挡时，应同时记录碰撞与击倒日志。 log=%s" % [str(batch.log_lines)]
	)


func _test_large_unit_charge_force_pushes_surviving_blocker_across_height_step() -> void:
	var case_data: Dictionary = _get_large_charge_direction_cases()[0]
	var fixture := _build_large_charge_fixture(case_data, true)
	var runtime := fixture.get("runtime") as BattleRuntimeModule
	var state := fixture.get("state") as BattleState
	var charger := fixture.get("charger") as BattleUnitState
	var command := fixture.get("command") as BattleCommand
	var blocker := _build_enemy_unit(&"charge_force_push_blocker", case_data.get("forward_blocker_coord", Vector2i.ZERO))
	var side_guard := _build_enemy_unit(&"charge_force_push_side_guard", Vector2i(3, 1))
	blocker.current_hp = 40
	state.units[blocker.unit_id] = blocker
	state.units[side_guard.unit_id] = side_guard
	state.enemy_unit_ids.append(blocker.unit_id)
	state.enemy_unit_ids.append(side_guard.unit_id)
	_assert_true(runtime._grid_service.place_unit(state, blocker, blocker.coord, true), "2x2 强制撞退分支的阻挡单位应能成功放入战场。")
	_assert_true(runtime._grid_service.place_unit(state, side_guard, side_guard.coord, true), "2x2 强制撞退分支的侧向阻挡单位应能成功放入战场。")
	_set_cell_height(state, case_data.get("forward_coord", Vector2i.ZERO), 7)
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)
	runtime._state = state

	var batch := runtime.issue_command(command)
	_assert_eq(charger.coord, case_data.get("first_anchor", Vector2i.ZERO), "2x2 单位把阻挡单位强行撞退后，应完成本次前进一步。")
	_assert_eq(blocker.coord, case_data.get("forward_coord", Vector2i.ZERO), "2x2 单位在普通前推失败但强制撞退可行时，应把阻挡单位撞退到前方高差地格。")
	_assert_eq(blocker.current_hp, 10, "2x2 单位强制撞退存活阻挡时，应先结算碰撞伤害。")
	_assert_true(
		batch.log_lines.any(func(line): return String(line).contains("强行撞退一格")),
		"2x2 单位触发强制撞退时，应记录强制位移日志。 log=%s" % [str(batch.log_lines)]
	)


func _test_large_unit_charge_stops_when_collision_cannot_displace_blocker() -> void:
	var case_data: Dictionary = _get_large_charge_direction_cases()[0]
	var fixture := _build_large_charge_fixture(case_data, true)
	var runtime := fixture.get("runtime") as BattleRuntimeModule
	var state := fixture.get("state") as BattleState
	var charger := fixture.get("charger") as BattleUnitState
	var command := fixture.get("command") as BattleCommand
	var blocker := _build_enemy_unit(&"charge_collision_stop_blocker", case_data.get("forward_blocker_coord", Vector2i.ZERO))
	var side_guard := _build_enemy_unit(&"charge_collision_stop_side_guard", Vector2i(3, 1))
	blocker.current_hp = 40
	var blocking_wall := _build_enemy_unit(&"charge_collision_stop_wall", case_data.get("forward_coord", Vector2i.ZERO))
	state.units[blocker.unit_id] = blocker
	state.units[side_guard.unit_id] = side_guard
	state.units[blocking_wall.unit_id] = blocking_wall
	state.enemy_unit_ids.append(blocker.unit_id)
	state.enemy_unit_ids.append(side_guard.unit_id)
	state.enemy_unit_ids.append(blocking_wall.unit_id)
	_assert_true(runtime._grid_service.place_unit(state, blocker, blocker.coord, true), "2x2 碰撞停步分支的首个阻挡单位应能成功放入战场。")
	_assert_true(runtime._grid_service.place_unit(state, side_guard, side_guard.coord, true), "2x2 碰撞停步分支的侧向阻挡单位应能成功放入战场。")
	_assert_true(runtime._grid_service.place_unit(state, blocking_wall, blocking_wall.coord, true), "2x2 碰撞停步分支的第二个阻挡单位应能成功放入战场。")
	runtime._state = state

	var batch := runtime.issue_command(command)
	_assert_eq(charger.coord, case_data.get("start_coord", Vector2i.ZERO), "2x2 单位碰撞后仍无法挪开阻挡时，应在原地 stop。")
	_assert_eq(blocker.coord, case_data.get("forward_blocker_coord", Vector2i.ZERO), "2x2 单位碰撞停步时，首个阻挡单位不应被错误位移。")
	_assert_eq(blocker.current_hp, 10, "2x2 单位碰撞停步时，仍应先结算碰撞伤害。")
	_assert_true(
		batch.log_lines.any(func(line): return String(line).contains("撞上")) and batch.log_lines.any(func(line): return String(line).contains("起步时被拦下")),
		"2x2 单位碰撞后仍无法挪开阻挡时，应同时记录碰撞与 stop 日志。 log=%s" % [str(batch.log_lines)]
	)


func _test_large_unit_charge_trap_stops_after_first_step() -> void:
	var case_data: Dictionary = _get_large_charge_direction_cases()[0]
	var fixture := _build_large_charge_fixture(case_data, false)
	var runtime := fixture.get("runtime") as BattleRuntimeModule
	var state := fixture.get("state") as BattleState
	var charger := fixture.get("charger") as BattleUnitState
	var command := fixture.get("command") as BattleCommand
	var trap_cell := state.cells.get(case_data.get("trap_coord", Vector2i.ZERO)) as BattleCellState
	if trap_cell != null:
		trap_cell.terrain_effect_ids.append(&"trap_large_unit_smoke")
	runtime._state = state

	var batch := runtime.issue_command(command)
	_assert_eq(charger.coord, case_data.get("first_anchor", Vector2i.ZERO), "2x2 单位首步踩中 trap 时，应保留首步位移并停止后续冲锋。")
	_assert_true(trap_cell != null and trap_cell.terrain_effect_ids.is_empty(), "2x2 单位触发 trap 后，应移除对应地格上的 trap 标记。")
	_assert_true(
		batch.log_lines.any(func(line): return String(line).contains("触发陷阱")),
		"2x2 单位踩中 trap 时，应记录 trap 中断日志。 log=%s" % [str(batch.log_lines)]
	)


func _test_ground_line_and_cone_skills_follow_caster_facing() -> void:
	var registry := ProgressionContentRegistry.new()
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})

	var line_state := _build_skill_test_state(Vector2i(5, 5))
	var line_user := _build_unit(&"line_skill_user", Vector2i(2, 2), 3)
	line_user.current_mp = 3
	line_user.known_active_skill_ids = [&"mage_flame_spear"]
	line_user.known_skill_level_map = {&"mage_flame_spear": 1}
	var line_enemy_front := _build_enemy_unit(&"line_enemy_front", Vector2i(2, 0))
	var line_enemy_side := _build_enemy_unit(&"line_enemy_side", Vector2i(3, 1))
	line_state.units = {
		line_user.unit_id: line_user,
		line_enemy_front.unit_id: line_enemy_front,
		line_enemy_side.unit_id: line_enemy_side,
	}
	line_state.ally_unit_ids = [line_user.unit_id]
	line_state.enemy_unit_ids = [line_enemy_front.unit_id, line_enemy_side.unit_id]
	line_state.active_unit_id = line_user.unit_id
	_assert_true(runtime._grid_service.place_unit(line_state, line_user, line_user.coord, true), "直线技能测试施法者应能成功放入战场。")
	_assert_true(runtime._grid_service.place_unit(line_state, line_enemy_front, line_enemy_front.coord, true), "直线技能前方敌人应能成功放入战场。")
	_assert_true(runtime._grid_service.place_unit(line_state, line_enemy_side, line_enemy_side.coord, true), "直线技能侧向敌人应能成功放入战场。")
	runtime._state = line_state

	var line_command := BattleCommand.new()
	line_command.command_type = BattleCommand.TYPE_SKILL
	line_command.unit_id = line_user.unit_id
	line_command.skill_id = &"mage_flame_spear"
	line_command.target_coord = Vector2i(2, 1)
	var line_preview := runtime.preview_command(line_command)
	_assert_true(line_preview.allowed, "炎枪术应允许指向施法者正前方的地格。")
	_assert_true(line_preview.target_coords.has(Vector2i(2, 0)), "炎枪术应沿施法者面向继续向前扩展。")
	_assert_true(not line_preview.target_coords.has(Vector2i(3, 1)), "炎枪术不应在正前方施放时横向偏转。")
	runtime.issue_command(line_command)
	_assert_true(line_enemy_front.current_hp < 30, "炎枪术应命中正前方敌人。")
	_assert_eq(line_enemy_side.current_hp, 30, "炎枪术不应误伤侧向敌人。")

	var cone_state := _build_skill_test_state(Vector2i(5, 5))
	var cone_user := _build_unit(&"cone_skill_user", Vector2i(2, 2), 3)
	cone_user.current_stamina = 12
	cone_user.known_active_skill_ids = [&"warrior_sweeping_slash"]
	cone_user.known_skill_level_map = {&"warrior_sweeping_slash": 1}
	var cone_enemy_front := _build_enemy_unit(&"cone_enemy_front", Vector2i(2, 0))
	var cone_enemy_side := _build_enemy_unit(&"cone_enemy_side", Vector2i(3, 1))
	cone_state.units = {
		cone_user.unit_id: cone_user,
		cone_enemy_front.unit_id: cone_enemy_front,
		cone_enemy_side.unit_id: cone_enemy_side,
	}
	cone_state.ally_unit_ids = [cone_user.unit_id]
	cone_state.enemy_unit_ids = [cone_enemy_front.unit_id, cone_enemy_side.unit_id]
	cone_state.active_unit_id = cone_user.unit_id
	_assert_true(runtime._grid_service.place_unit(cone_state, cone_user, cone_user.coord, true), "锥形技能测试施法者应能成功放入战场。")
	_assert_true(runtime._grid_service.place_unit(cone_state, cone_enemy_front, cone_enemy_front.coord, true), "锥形技能前方敌人应能成功放入战场。")
	_assert_true(runtime._grid_service.place_unit(cone_state, cone_enemy_side, cone_enemy_side.coord, true), "锥形技能侧向敌人应能成功放入战场。")
	runtime._state = cone_state

	var cone_command := BattleCommand.new()
	cone_command.command_type = BattleCommand.TYPE_SKILL
	cone_command.unit_id = cone_user.unit_id
	cone_command.skill_id = &"warrior_sweeping_slash"
	cone_command.target_coord = Vector2i(2, 1)
	var cone_preview := runtime.preview_command(cone_command)
	_assert_true(cone_preview.allowed, "横扫应允许指向施法者正前方的地格。")
	_assert_true(cone_preview.target_coords.has(Vector2i(2, 0)), "横扫应沿施法者面向向前展开扇形。")
	_assert_true(not cone_preview.target_coords.has(Vector2i(3, 1)), "横扫不应在正前方施放时改为向右扇出。")
	runtime.issue_command(cone_command)
	_assert_true(cone_enemy_front.current_hp < 30, "横扫应命中正前方敌人。")
	_assert_eq(cone_enemy_side.current_hp, 30, "横扫不应误伤右侧敌人。")


func _test_archer_multishot_uses_target_unit_ids_in_manual_order() -> void:
	var registry := ProgressionContentRegistry.new()
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})

	var state := _build_skill_test_state(Vector2i(4, 1))
	var archer := _build_unit(&"archer_multishot_user", Vector2i(0, 0), 3)
	archer.current_stamina = 20
	archer.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 100)
	archer.known_active_skill_ids = [&"archer_multishot"]
	archer.known_skill_level_map = {&"archer_multishot": 1}
	var enemy_a := _build_enemy_unit(&"enemy_a", Vector2i(1, 0))
	enemy_a.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	var enemy_b := _build_enemy_unit(&"enemy_b", Vector2i(2, 0))
	enemy_b.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	var enemy_c := _build_enemy_unit(&"enemy_c", Vector2i(3, 0))
	enemy_c.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)

	state.units = {
		archer.unit_id: archer,
		enemy_a.unit_id: enemy_a,
		enemy_b.unit_id: enemy_b,
		enemy_c.unit_id: enemy_c,
	}
	state.ally_unit_ids = [archer.unit_id]
	state.enemy_unit_ids = [enemy_a.unit_id, enemy_b.unit_id, enemy_c.unit_id]
	state.active_unit_id = archer.unit_id
	_assert_true(runtime._grid_service.place_unit(state, archer, archer.coord, true), "弓箭手测试单位应能成功放入战场。")
	_assert_true(runtime._grid_service.place_unit(state, enemy_a, enemy_a.coord, true), "敌人 A 应能成功放入战场。")
	_assert_true(runtime._grid_service.place_unit(state, enemy_b, enemy_b.coord, true), "敌人 B 应能成功放入战场。")
	_assert_true(runtime._grid_service.place_unit(state, enemy_c, enemy_c.coord, true), "敌人 C 应能成功放入战场。")
	runtime._state = state

	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = archer.unit_id
	command.skill_id = &"archer_multishot"
	command.skill_variant_id = &"multishot_volley"
	command.target_unit_id = enemy_c.unit_id
	command.target_coord = enemy_c.coord
	command.target_unit_ids = [enemy_c.unit_id, enemy_a.unit_id, enemy_b.unit_id]
	var preview := runtime.preview_command(command)
	_assert_true(preview.allowed, "连珠箭应允许一次锁定三个离散敌方单位。")
	_assert_eq(preview.target_unit_ids.size(), 3, "连珠箭预览应识别三个目标单位。")
	_assert_eq(preview.target_unit_ids, [enemy_c.unit_id, enemy_a.unit_id, enemy_b.unit_id], "连珠箭预览应保持玩家选择顺序。")

	var batch := runtime.issue_command(command)
	_assert_true(batch.changed_unit_ids.has(archer.unit_id), "连珠箭应记录施法者变更。")
	_assert_eq(archer.current_stamina, 18, "连珠箭应只按一次施放消耗体力。")
	_assert_eq(
		_extract_string_name_array(batch.changed_unit_ids),
		[String(archer.unit_id), String(enemy_c.unit_id), String(enemy_a.unit_id), String(enemy_b.unit_id)],
		"连珠箭应按玩家选择顺序依次解析目标；天然 1 未造成伤害时也应标记目标变更。"
	)


func _test_multi_unit_skill_uses_stable_target_order() -> void:
	var registry := ProgressionContentRegistry.new()
	var skill_defs := registry.get_skill_defs()
	var arcane_missile := skill_defs.get(&"mage_arcane_missile") as SkillDef
	if arcane_missile != null and arcane_missile.combat_profile != null:
		arcane_missile.combat_profile.selection_order_mode = &"stable"

	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, skill_defs, {}, {})

	var state := _build_skill_test_state(Vector2i(4, 2))
	var mage := _build_unit(&"mage_arcane_missile_user", Vector2i(0, 1), 3)
	mage.current_mp = 3
	mage.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 100)
	mage.known_active_skill_ids = [&"mage_arcane_missile"]
	mage.known_skill_level_map = {&"mage_arcane_missile": 1}
	var enemy_a := _build_enemy_unit(&"enemy_a", Vector2i(2, 0))
	enemy_a.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	var enemy_b := _build_enemy_unit(&"enemy_b", Vector2i(0, 0))
	enemy_b.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	var enemy_c := _build_enemy_unit(&"enemy_c", Vector2i(1, 0))
	enemy_c.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)

	state.units = {
		mage.unit_id: mage,
		enemy_a.unit_id: enemy_a,
		enemy_b.unit_id: enemy_b,
		enemy_c.unit_id: enemy_c,
	}
	state.ally_unit_ids = [mage.unit_id]
	state.enemy_unit_ids = [enemy_a.unit_id, enemy_b.unit_id, enemy_c.unit_id]
	state.active_unit_id = mage.unit_id
	_assert_true(runtime._grid_service.place_unit(state, mage, mage.coord, true), "奥术飞弹测试单位应能成功放入战场。")
	_assert_true(runtime._grid_service.place_unit(state, enemy_a, enemy_a.coord, true), "敌人 A 应能成功放入战场。")
	_assert_true(runtime._grid_service.place_unit(state, enemy_b, enemy_b.coord, true), "敌人 B 应能成功放入战场。")
	_assert_true(runtime._grid_service.place_unit(state, enemy_c, enemy_c.coord, true), "敌人 C 应能成功放入战场。")
	runtime._state = state

	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = mage.unit_id
	command.skill_id = &"mage_arcane_missile"
	command.target_unit_ids = [enemy_a.unit_id, enemy_b.unit_id, enemy_c.unit_id]
	var preview := runtime.preview_command(command)
	_assert_true(preview.allowed, "奥术飞弹应允许一次锁定三个离散敌方单位。")
	_assert_eq(preview.target_unit_ids, [enemy_b.unit_id, enemy_c.unit_id, enemy_a.unit_id], "稳定排序应按战场坐标归一化目标顺序。")

	var batch := runtime.issue_command(command)
	_assert_true(batch.changed_unit_ids.has(mage.unit_id), "奥术飞弹应记录施法者变更。")
	_assert_eq(mage.current_mp, 2, "奥术飞弹应只按一次施放消耗法力。")
	_assert_eq(
		_extract_string_name_array(batch.changed_unit_ids),
		[String(mage.unit_id), String(enemy_b.unit_id), String(enemy_c.unit_id), String(enemy_a.unit_id)],
		"奥术飞弹应按稳定排序后的顺序依次解析目标；天然 1 未造成伤害时也应标记目标变更。"
	)


func _test_skill_costs_and_cooldowns_apply_in_runtime() -> void:
	var registry := ProgressionContentRegistry.new()
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})

	var state := _build_skill_test_state(Vector2i(2, 1))
	var archer := _build_unit(&"archer_long_draw_user", Vector2i(0, 0), 3)
	archer.current_stamina = 12
	archer.current_mp = 0
	archer.known_active_skill_ids = [&"archer_long_draw"]
	archer.known_skill_level_map = {&"archer_long_draw": 1}
	var enemy := _build_enemy_unit(&"enemy_target", Vector2i(1, 0))

	state.units = {
		archer.unit_id: archer,
		enemy.unit_id: enemy,
	}
	state.ally_unit_ids = [archer.unit_id]
	state.enemy_unit_ids = [enemy.unit_id]
	state.active_unit_id = archer.unit_id
	_assert_true(runtime._grid_service.place_unit(state, archer, archer.coord, true), "长弓测试单位应能成功放入战场。")
	_assert_true(runtime._grid_service.place_unit(state, enemy, enemy.coord, true), "长弓测试目标应能成功放入战场。")
	runtime._state = state

	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = archer.unit_id
	command.skill_id = &"archer_long_draw"
	command.target_unit_id = enemy.unit_id
	command.target_coord = enemy.coord
	var batch := runtime.issue_command(command)
	_assert_true(batch.changed_unit_ids.has(archer.unit_id), "施放满弦狙击后应记录施法者变更。")
	_assert_eq(archer.current_stamina, 10, "满弦狙击应按文档配置扣除 2 点体力。")
	_assert_eq(int(archer.cooldowns.get(&"archer_long_draw", 0)), 15, "满弦狙击应按文档配置写入 15 TU 冷却。")

	var second_batch := runtime.issue_command(command)
	_assert_true(
		not second_batch.log_lines.is_empty() and String(second_batch.log_lines[-1]).contains("冷却"),
		"技能仍在冷却时，再次施放应给出明确提示。"
	)


func _test_weapon_skill_range_uses_weapon_attack_range_not_skill_range() -> void:
	var skill := _build_direct_damage_skill(&"weapon_range_contract", 1)
	skill.tags = [&"warrior", &"melee"]
	skill.combat_profile.range_value = 99
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, {skill.skill_id: skill}, {}, {})

	var state := _build_skill_test_state(Vector2i(3, 1))
	var warrior := _build_unit(&"weapon_range_user", Vector2i(0, 0), 2)
	warrior.set_natural_weapon_projection(&"test_blade", &"physical_slash", 1)
	warrior.known_active_skill_ids = [skill.skill_id]
	warrior.known_skill_level_map = {skill.skill_id: 1}
	var enemy := _build_enemy_unit(&"weapon_range_target", Vector2i(2, 0))
	state.units = {
		warrior.unit_id: warrior,
		enemy.unit_id: enemy,
	}
	state.ally_unit_ids = [warrior.unit_id]
	state.enemy_unit_ids = [enemy.unit_id]
	state.active_unit_id = warrior.unit_id
	_assert_true(runtime._grid_service.place_unit(state, warrior, warrior.coord, true), "武器射程回归中的战士应能成功放入战场。")
	_assert_true(runtime._grid_service.place_unit(state, enemy, enemy.coord, true), "武器射程回归中的目标应能成功放入战场。")
	runtime._state = state

	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = warrior.unit_id
	command.skill_id = skill.skill_id
	command.target_unit_id = enemy.unit_id
	command.target_coord = enemy.coord
	var blocked_batch := runtime.issue_command(command)
	_assert_true(
		not blocked_batch.log_lines.is_empty() and String(blocked_batch.log_lines[-1]).contains("目标"),
		"近战武器技能应使用武器攻击范围 1，而不是技能 range_value=99。 log=%s" % [str(blocked_batch.log_lines)]
	)
	_assert_eq(warrior.current_ap, 2, "武器攻击范围外的技能不应扣除 AP。")

	warrior.set_natural_weapon_projection(&"test_blade", &"physical_slash", 2)
	var allowed_batch := runtime.issue_command(command)
	_assert_true(allowed_batch.changed_unit_ids.has(warrior.unit_id), "武器攻击范围提高到 2 后，同一目标应允许结算。")
	_assert_eq(warrior.current_ap, 1, "武器攻击范围内的技能应正常扣除 AP。")


func _test_battle_range_service_layers_modifiers_without_snapshot_truth() -> void:
	var skill := _build_direct_damage_skill(&"range_layer_contract", 1)
	skill.tags = [&"archer", &"bow"]
	skill.combat_profile.range_value = 99

	var archer := _build_unit(&"range_layer_archer", Vector2i.ZERO, 2)
	archer.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.WEAPON_ATTACK_RANGE, 8)
	archer.set_natural_weapon_projection(&"test_bow", &"physical_pierce", 2)

	_assert_eq(
		BattleRangeService.get_effective_skill_range(archer, skill),
		2,
		"有效射程应读取 BattleUnitState.weapon_attack_range，而不是 attribute_snapshot 或技能 range_value。"
	)

	var range_status := BattleStatusEffectState.new()
	range_status.status_id = &"archer_range_up"
	range_status.source_unit_id = archer.unit_id
	range_status.power = 1
	range_status.stacks = 1
	range_status.duration = 60
	archer.set_status_effect(range_status)

	_assert_eq(
		BattleRangeService.get_effective_skill_range(archer, skill),
		3,
		"状态提供的射程修正应只在有效射程读取层叠加。"
	)
	_assert_eq(archer.weapon_attack_range, 2, "状态射程修正不应写回 BattleUnitState.weapon_attack_range 基础投影。")


func _test_weapon_skill_damage_tag_uses_current_weapon_type() -> void:
	var resolver := BattleDamageResolver.new()
	var effect := CombatEffectDef.new()
	effect.effect_type = &"damage"
	effect.power = 1
	effect.damage_tag = &"physical_blunt"
	effect.params = {
		"use_weapon_physical_damage_tag": true,
	}
	var expected_tags := {
		&"sword_user": &"physical_slash",
		&"mace_user": &"physical_blunt",
		&"dagger_user": &"physical_pierce",
	}
	for unit_id in expected_tags.keys():
		var source := _build_unit(unit_id, Vector2i.ZERO, 1)
		source.set_natural_weapon_projection(&"test_weapon", expected_tags.get(unit_id), 1)
		var target := _build_enemy_unit(StringName("%s_target" % String(unit_id)), Vector2i(1, 0))
		var result: Dictionary = resolver.resolve_effects(source, target, [effect])
		var events: Array = result.get("damage_events", [])
		_assert_true(not events.is_empty(), "武器伤害类型回归应产生伤害事件。")
		if events.is_empty():
			continue
		_assert_eq(
			ProgressionDataUtils.to_string_name(events[0].get("damage_tag", "")),
			expected_tags.get(unit_id),
			"武器近战技能应按当前武器类型实时覆盖物理伤害类型。"
		)


func _test_skill_mastery_requires_max_damage_die_or_critical_and_scales_by_enemy_rank() -> void:
	var gateway := MasteryGatewayStub.new()
	var skill := _build_ground_damage_dice_skill(&"mastery_rank_test", 0, 1, 1)
	var runtime := BattleRuntimeModule.new()
	runtime.setup(gateway, {skill.skill_id: skill}, {}, {})

	var state := _build_skill_test_state(Vector2i(5, 1))
	var caster := _build_unit(&"mastery_caster", Vector2i(0, 0), 3)
	caster.source_member_id = &"hero"
	caster.known_active_skill_ids = [skill.skill_id]
	caster.known_skill_level_map = {skill.skill_id: 1}
	var normal := _build_enemy_unit(&"mastery_normal", Vector2i(1, 0))
	var elite := _build_enemy_unit(&"mastery_elite", Vector2i(2, 0))
	elite.attribute_snapshot.set_value(&"fortune_mark_target", 1)
	var boss := _build_enemy_unit(&"mastery_boss", Vector2i(3, 0))
	boss.attribute_snapshot.set_value(&"fortune_mark_target", 2)
	boss.attribute_snapshot.set_value(&"boss_target", 1)

	state.units = {
		caster.unit_id: caster,
		normal.unit_id: normal,
		elite.unit_id: elite,
		boss.unit_id: boss,
	}
	state.ally_unit_ids = [caster.unit_id]
	state.enemy_unit_ids = [normal.unit_id, elite.unit_id, boss.unit_id]
	state.active_unit_id = caster.unit_id
	for unit in [caster, normal, elite, boss]:
		_assert_true(runtime._grid_service.place_unit(state, unit, unit.coord, true), "%s 应能放入熟练度回归战场。" % unit.display_name)
	runtime._state = state

	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = caster.unit_id
	command.skill_id = skill.skill_id
	command.target_coord = Vector2i(2, 0)
	var batch := runtime.issue_command(command)
	_assert_true(batch.changed_unit_ids.has(caster.unit_id), "满骰熟练度技能应正常执行。")
	_assert_eq(gateway.grants.size(), 1, "满伤害骰命中后应只提交一次聚合熟练度。")
	if not gateway.grants.is_empty():
		_assert_eq(int(gateway.grants[0].get("amount", 0)), 6, "普通/精英/BOSS 满骰命中应分别给 1/2/3 熟练度。")

	var non_dice_skill := _build_ground_damage_dice_skill(&"mastery_no_dice_test", 1, 0, 0)
	caster.current_ap = 3
	caster.known_active_skill_ids = [non_dice_skill.skill_id]
	caster.known_skill_level_map = {non_dice_skill.skill_id: 1}
	runtime.setup(gateway, {non_dice_skill.skill_id: non_dice_skill}, {}, {})
	runtime._state = state
	command.skill_id = non_dice_skill.skill_id
	runtime.issue_command(command)
	_assert_eq(gateway.grants.size(), 1, "非暴击且没有伤害骰满值时不应增加熟练度。")


func _test_ground_jump_precast_failure_does_not_consume_costs() -> void:
	var registry := ProgressionContentRegistry.new()
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})

	var state := _build_skill_test_state(Vector2i(3, 1))
	var warrior := _build_unit(&"jump_precast_user", Vector2i(0, 0), 3)
	warrior.current_stamina = 3
	warrior.known_active_skill_ids = [&"warrior_jump_slash"]
	warrior.known_skill_level_map = {&"warrior_jump_slash": 1}
	var blocker := _build_enemy_unit(&"jump_precast_blocker", Vector2i(1, 0))

	state.units = {
		warrior.unit_id: warrior,
		blocker.unit_id: blocker,
	}
	state.ally_unit_ids = [warrior.unit_id]
	state.enemy_unit_ids = [blocker.unit_id]
	state.active_unit_id = warrior.unit_id
	_assert_true(runtime._grid_service.place_unit(state, warrior, warrior.coord, true), "跳跃扣费回归中的战士应能成功放入战场。")
	_assert_true(runtime._grid_service.place_unit(state, blocker, blocker.coord, true), "跳跃扣费回归中的阻挡单位应能成功放入战场。")
	runtime._state = state

	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = warrior.unit_id
	command.skill_id = &"warrior_jump_slash"
	command.target_coord = blocker.coord
	var batch := runtime.issue_command(command)
	_assert_true(
		batch.log_lines.any(func(line): return String(line).contains("跳跃落点") or String(line).contains("落点")),
		"跳斩落点被占用时应给出明确日志。 log=%s" % [str(batch.log_lines)]
	)
	_assert_eq(warrior.current_ap, 3, "跳斩落点无效时不应扣除行动点。")
	_assert_eq(warrior.current_stamina, 3, "跳斩落点无效时不应扣除体力。")
	_assert_true(not warrior.cooldowns.has(&"warrior_jump_slash"), "跳斩落点无效时不应写入冷却。")
	_assert_eq(warrior.coord, Vector2i(0, 0), "跳斩落点无效时不应移动施法者。")


func _test_issue_command_flushes_battle_end_logs_to_state() -> void:
	var skill := _build_direct_damage_skill(&"test_direct_finisher", 20)
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, {skill.skill_id: skill}, {}, {})

	var state := _build_skill_test_state(Vector2i(2, 1))
	var caster := _build_unit(&"battle_end_log_caster", Vector2i(0, 0), 1)
	caster.known_active_skill_ids = [skill.skill_id]
	caster.known_skill_level_map = {skill.skill_id: 1}
	var target := _build_unit(&"battle_end_log_target", Vector2i(1, 0), 1)
	target.current_hp = 5

	state.units = {
		caster.unit_id: caster,
		target.unit_id: target,
	}
	state.ally_unit_ids = [caster.unit_id]
	state.enemy_unit_ids = [target.unit_id]
	state.active_unit_id = caster.unit_id
	_assert_true(runtime._grid_service.place_unit(state, caster, caster.coord, true), "战斗结束日志回归中的施法者应能成功放入战场。")
	_assert_true(runtime._grid_service.place_unit(state, target, target.coord, true), "战斗结束日志回归中的目标应能成功放入战场。")
	runtime._state = state

	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = caster.unit_id
	command.skill_id = skill.skill_id
	command.target_unit_id = target.unit_id
	command.target_coord = target.coord
	var batch := runtime.issue_command(command)
	_assert_true(batch.battle_ended, "终结最后一个敌方单位后 issue_command() 应返回 battle_ended。")
	_assert_eq(state.phase, &"battle_ended", "终结最后一个敌方单位后 state.phase 应进入 battle_ended。")
	_assert_true(
		state.log_entries.any(func(line): return String(line).contains("战斗结束")),
		"issue_command() 追加的战斗结束日志应同步写入 BattleState.log_entries。 logs=%s" % [str(state.log_entries)]
	)


func _test_cooldowns_reduce_on_tu_progress_and_zero_tu_turn_switch() -> void:
	var registry := ProgressionContentRegistry.new()
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})

	var state := _build_skill_test_state(Vector2i(2, 1))
	state.phase = &"timeline_running"
	state.timeline.tick_interval_seconds = 1.0
	state.timeline.tu_per_tick = 5
	var archer := _build_unit(&"aa_cooldown_turn_user", Vector2i(0, 0), 1)
	archer.action_threshold = 5
	archer.cooldowns = {&"archer_long_draw": 15}
	archer.last_turn_tu = 0
	var enemy := _build_enemy_unit(&"zz_cooldown_turn_enemy", Vector2i(1, 0))
	enemy.action_threshold = 5

	state.units = {
		archer.unit_id: archer,
		enemy.unit_id: enemy,
	}
	state.ally_unit_ids = [archer.unit_id]
	state.enemy_unit_ids = [enemy.unit_id]
	_assert_true(runtime._grid_service.place_unit(state, archer, archer.coord, true), "冷却递减回归中的施法者应能成功放入战场。")
	_assert_true(runtime._grid_service.place_unit(state, enemy, enemy.coord, true), "冷却递减回归中的目标应能成功放入战场。")
	runtime._state = state

	var tu_batch := runtime.advance(1.0)
	_assert_eq(state.timeline.current_tu, 5, "战斗时间轴推进后 current_tu 应增长 5。")
	_assert_true(tu_batch.changed_unit_ids.has(archer.unit_id), "TU 推进触发新回合时应记录 cooldown 单位变更。")
	_assert_eq(int(archer.cooldowns.get(&"archer_long_draw", 0)), 10, "经过 5 TU 后，技能冷却应正式递减 5。")
	_assert_eq(archer.last_turn_tu, 5, "进入新行动窗口后应记录最新的 turn TU 锚点。")

	state.phase = &"timeline_running"
	state.active_unit_id = &""
	state.timeline.ready_unit_ids.clear()
	state.timeline.ready_unit_ids.append(archer.unit_id)
	var turn_batch := runtime.advance(0.0)
	_assert_true(turn_batch.changed_unit_ids.has(archer.unit_id), "零 TU 的队列回合切换仍应记录行动单位变更。")
	_assert_eq(int(archer.cooldowns.get(&"archer_long_draw", 0)), 10, "零 TU 回合切换不应继续递减 cooldown。")
	_assert_eq(archer.last_turn_tu, 5, "零 TU 回合切换不应篡改当前的 timeline TU 锚点。")


func _test_timeline_tick_uses_per_unit_action_threshold() -> void:
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, {}, {}, {})

	var state := _build_skill_test_state(Vector2i(3, 1))
	state.phase = &"timeline_running"
	state.timeline.tick_interval_seconds = 1.0
	state.timeline.tu_per_tick = 5
	var first_unit := _build_unit(&"aa_timeline_threshold_10", Vector2i(0, 0), 1)
	first_unit.action_threshold = 10
	var second_unit := _build_enemy_unit(&"zz_timeline_threshold_15", Vector2i(1, 0))
	second_unit.action_threshold = 15

	state.units = {
		first_unit.unit_id: first_unit,
		second_unit.unit_id: second_unit,
	}
	state.ally_unit_ids = [first_unit.unit_id]
	state.enemy_unit_ids = [second_unit.unit_id]
	_assert_true(runtime._grid_service.place_unit(state, first_unit, first_unit.coord, true), "10 TU 阈值测试单位应能成功放入战场。")
	_assert_true(runtime._grid_service.place_unit(state, second_unit, second_unit.coord, true), "15 TU 阈值测试单位应能成功放入战场。")
	runtime._state = state

	runtime.advance(1.0)
	_assert_eq(state.timeline.current_tu, 5, "第一个离散 tick 应只推进 5 TU。")
	_assert_eq(first_unit.action_progress, 5, "10 TU 阈值单位的行动进度应按 tu_per_tick 累加。")
	_assert_eq(second_unit.action_progress, 5, "15 TU 阈值单位的行动进度也应按 tu_per_tick 累加。")
	_assert_true(state.timeline.ready_unit_ids.is_empty(), "所有单位未达到各自 action_threshold 前不应产生行动单位。")
	_assert_eq(state.phase, &"timeline_running", "未达到任一行动阈值前应保持时间轴推进阶段。")

	runtime.advance(1.0)
	_assert_eq(state.timeline.current_tu, 10, "第二个离散 tick 后 current_tu 应累计到 10。")
	_assert_eq(first_unit.action_progress, 0, "达到该单位 action_threshold 后应扣除一次阈值。")
	_assert_eq(second_unit.action_progress, 10, "未达到自己阈值的单位不应提前入队。")
	_assert_eq(state.active_unit_id, first_unit.unit_id, "达到阈值后应激活已满足自身阈值的单位。")
	_assert_true(not state.timeline.ready_unit_ids.has(second_unit.unit_id), "未达到自身阈值的单位不应留在 ready 队列。")


func _test_status_duration_serialization_preserves_tu_window() -> void:
	var registry := ProgressionContentRegistry.new()
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})

	var state := _build_skill_test_state(Vector2i(6, 4))
	var archer := _build_unit(&"status_skip_archer", Vector2i(3, 1), 2)
	archer.current_mp = 6
	archer.current_stamina = 6
	archer.current_aura = 6
	archer.known_active_skill_ids = [&"archer_skirmish_step"]
	archer.known_skill_level_map = {&"archer_skirmish_step": 1}
	var enemy := _build_enemy_unit(&"status_skip_enemy", Vector2i(5, 1))

	state.units = {
		archer.unit_id: archer,
		enemy.unit_id: enemy,
	}
	state.ally_unit_ids = [archer.unit_id]
	state.enemy_unit_ids = [enemy.unit_id]
	state.active_unit_id = archer.unit_id
	_assert_true(runtime._grid_service.place_unit(state, archer, archer.coord, true), "状态持续时间回归中的施法者应能成功放入战场。")
	_assert_true(runtime._grid_service.place_unit(state, enemy, enemy.coord, true), "状态持续时间回归中的敌人应能成功放入战场。")
	runtime._state = state

	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = archer.unit_id
	command.skill_id = &"archer_skirmish_step"
	command.target_unit_id = archer.unit_id
	var batch := runtime.issue_command(command)
	_assert_true(batch.changed_unit_ids.has(archer.unit_id), "游击步应记录施法者状态变更。")
	var pre_aim_entry = archer.get_status_effect(&"archer_pre_aim")
	_assert_true(
		pre_aim_entry != null and int(pre_aim_entry.duration) == 60,
		"自施放状态应写入正式 TU 持续时间。"
	)

	var payload := archer.to_dict()
	var payload_status_effects: Dictionary = payload.get("status_effects", {})
	var pre_aim_payload: Dictionary = payload_status_effects.get("archer_pre_aim", {})
	_assert_eq(int(pre_aim_payload.get("duration", -1)), 60, "BattleUnitState.to_dict() 应保留 TU 持续时间。")
	var restored := BattleUnitState.from_dict(payload) as BattleUnitState
	_assert_true(restored != null, "BattleUnitState.from_dict() 应能恢复带持续时间状态的单位。")
	if restored == null:
		return
	var restored_pre_aim = restored.get_status_effect(&"archer_pre_aim")
	_assert_true(
		restored_pre_aim != null and int(restored_pre_aim.duration) == 60,
		"BattleUnitState.from_dict() 应恢复 TU 持续时间。"
	)
	state.units[restored.unit_id] = restored
	archer = restored

	var wait_command := BattleCommand.new()
	wait_command.command_type = BattleCommand.TYPE_WAIT
	wait_command.unit_id = archer.unit_id
	runtime.issue_command(wait_command)
	var carried_pre_aim = archer.get_status_effect(&"archer_pre_aim")
	_assert_true(carried_pre_aim != null and int(carried_pre_aim.duration) == 60, "当前回合结束时，自施放状态不应因 turn end 被立即清除。")

	state.phase = &"timeline_running"
	state.active_unit_id = &""
	state.timeline.ready_unit_ids.clear()
	state.timeline.ready_unit_ids.append(archer.unit_id)
	runtime.advance(0.0)
	_assert_true(archer.has_status_effect(&"archer_pre_aim"), "自施放状态应在下一次行动窗口开始时仍然保留。")

	_advance_timeline_tu(runtime, state, 55)
	_assert_true(archer.has_status_effect(&"archer_pre_aim"), "TU 未走完前，自施放状态应继续保留。")
	_advance_timeline_tu(runtime, state, 5)
	_assert_true(not archer.has_status_effect(&"archer_pre_aim"), "TU 走完后，自施放状态应按时间轴移除。")


func _test_status_duration_blocks_target_turn_until_tu_expiry() -> void:
	var registry := ProgressionContentRegistry.new()
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})

	var state := _build_skill_test_state(Vector2i(6, 3))
	var archer := _build_unit(&"status_pinned_archer", Vector2i(1, 1), 2)
	archer.current_mp = 6
	archer.current_stamina = 6
	archer.current_aura = 6
	archer.known_active_skill_ids = [&"archer_arrow_rain"]
	archer.known_skill_level_map = {&"archer_arrow_rain": 1}
	var enemy := _build_enemy_unit(&"status_pinned_enemy", Vector2i(3, 1))
	enemy.current_ap = 1

	state.units = {
		archer.unit_id: archer,
		enemy.unit_id: enemy,
	}
	state.ally_unit_ids = [archer.unit_id]
	state.enemy_unit_ids = [enemy.unit_id]
	state.active_unit_id = archer.unit_id
	_assert_true(runtime._grid_service.place_unit(state, archer, archer.coord, true), "压制状态回归中的施法者应能成功放入战场。")
	_assert_true(runtime._grid_service.place_unit(state, enemy, enemy.coord, true), "压制状态回归中的敌人应能成功放入战场。")
	runtime._state = state

	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = archer.unit_id
	command.skill_id = &"archer_arrow_rain"
	command.target_coord = enemy.coord
	runtime.issue_command(command)
	_assert_true(enemy.has_status_effect(&"pinned"), "箭雨命中后应把 pinned 写入正式 battle unit 状态。")

	var enemy_payload := enemy.to_dict()
	var enemy_status_effects: Dictionary = enemy_payload.get("status_effects", {})
	var pinned_payload: Dictionary = enemy_status_effects.get("pinned", {})
	_assert_eq(int(pinned_payload.get("duration", -1)), 90, "被施加的 pinned 应带着正式 TU 持续时间进入序列化 payload。")
	var restored_enemy := BattleUnitState.from_dict(enemy_payload) as BattleUnitState
	_assert_true(restored_enemy != null and restored_enemy.has_status_effect(&"pinned"), "BattleUnitState.from_dict() 应恢复敌方 pinned 状态。")
	if restored_enemy == null:
		return
	state.units[restored_enemy.unit_id] = restored_enemy
	enemy = restored_enemy

	state.phase = &"timeline_running"
	state.active_unit_id = &""
	state.timeline.ready_unit_ids.clear()
	state.timeline.ready_unit_ids.append(enemy.unit_id)
	runtime.advance(0.0)
	_assert_true(enemy.has_status_effect(&"pinned"), "目标进入行动窗口时，pinned 不应在 turn start 前被提前清除。")

	var move_command := BattleCommand.new()
	move_command.command_type = BattleCommand.TYPE_MOVE
	move_command.unit_id = enemy.unit_id
	move_command.target_coord = Vector2i(4, 1)
	var move_preview := runtime.preview_command(move_command)
	_assert_true(
		move_preview != null and not move_preview.allowed and move_preview.log_lines.size() > 0 and String(move_preview.log_lines[-1]).contains("限制移动"),
		"pinned 应在目标回合内稳定阻止移动。"
	)

	var wait_command := BattleCommand.new()
	wait_command.command_type = BattleCommand.TYPE_WAIT
	wait_command.unit_id = enemy.unit_id
	runtime.issue_command(wait_command)
	_assert_true(enemy.has_status_effect(&"pinned"), "目标回合结束后，pinned 不应再因为 turn end 被移除。")
	_advance_timeline_tu(runtime, state, 85)
	_assert_true(enemy.has_status_effect(&"pinned"), "TU 未走完前，pinned 应保持生效。")
	_advance_timeline_tu(runtime, state, 5)
	_assert_true(not enemy.has_status_effect(&"pinned"), "TU 走完后，pinned 应按时间轴移除。")


func _count_explicit_props(state: BattleState) -> Dictionary:
	var counts := {
		BattleBoardPropCatalog.PROP_OBJECTIVE_MARKER: 0,
		BattleBoardPropCatalog.PROP_TENT: 0,
		BattleBoardPropCatalog.PROP_TORCH: 0,
	}
	if state == null:
		return counts
	for cell_variant in state.cells.values():
		var cell := cell_variant as BattleCellState
		if cell == null:
			continue
		for prop_id in cell.prop_ids:
			if counts.has(prop_id):
				counts[prop_id] = int(counts.get(prop_id, 0)) + 1
	return counts


func _collect_explicit_prop_coords(state: BattleState, prop_id: StringName) -> Array[Vector2i]:
	var coords: Array[Vector2i] = []
	if state == null:
		return coords
	for coord_variant in state.cells.keys():
		if coord_variant is not Vector2i:
			continue
		var coord: Vector2i = coord_variant
		var cell := state.cells.get(coord) as BattleCellState
		if cell == null or not cell.prop_ids.has(prop_id):
			continue
		coords.append(coord)
	return coords


func _count_terrain_cells(state: BattleState, terrain_id: StringName) -> int:
	if state == null:
		return 0
	var count := 0
	for cell_variant in state.cells.values():
		var cell := cell_variant as BattleCellState
		if cell == null:
			continue
		if cell.base_terrain == terrain_id:
			count += 1
	return count


func _get_unit_anchor_height(state: BattleState, unit: BattleUnitState) -> int:
	if state == null or unit == null:
		return 0
	var cell := state.cells.get(unit.coord) as BattleCellState
	if cell == null:
		return 0
	return int(cell.current_height)


func _build_unit(unit_id: StringName, coord: Vector2i, current_ap: int) -> BattleUnitState:
	var unit := BattleUnitState.new()
	unit.unit_id = unit_id
	unit.display_name = String(unit_id)
	unit.faction_id = &"player"
	unit.current_ap = current_ap
	unit.current_move_points = BattleUnitState.DEFAULT_MOVE_POINTS_PER_TURN
	unit.current_hp = 10
	unit.is_alive = true
	unit.set_anchor_coord(coord)
	return unit


func _build_enemy_unit(unit_id: StringName, coord: Vector2i) -> BattleUnitState:
	var unit := _build_unit(unit_id, coord, 1)
	unit.faction_id = &"enemy"
	unit.current_hp = 30
	return unit


func _build_direct_damage_skill(skill_id: StringName, power: int) -> SkillDef:
	var damage_effect := CombatEffectDef.new()
	damage_effect.effect_type = &"damage"
	damage_effect.power = power
	damage_effect.effect_target_team_filter = &"any"

	var combat_profile := CombatSkillDef.new()
	combat_profile.skill_id = skill_id
	combat_profile.target_mode = &"unit"
	combat_profile.target_team_filter = &"any"
	combat_profile.range_value = 1
	combat_profile.ap_cost = 1
	var effect_defs: Array[CombatEffectDef] = [damage_effect]
	combat_profile.effect_defs = effect_defs

	var skill := SkillDef.new()
	skill.skill_id = skill_id
	skill.display_name = String(skill_id)
	skill.combat_profile = combat_profile
	return skill


func _build_ground_damage_dice_skill(
	skill_id: StringName,
	power: int,
	dice_count: int,
	dice_sides: int
) -> SkillDef:
	var damage_effect := CombatEffectDef.new()
	damage_effect.effect_type = &"damage"
	damage_effect.power = power
	damage_effect.effect_target_team_filter = &"enemy"
	if dice_count > 0 and dice_sides > 0:
		damage_effect.params = {
			"dice_count": dice_count,
			"dice_sides": dice_sides,
		}

	var combat_profile := CombatSkillDef.new()
	combat_profile.skill_id = skill_id
	combat_profile.target_mode = &"ground"
	combat_profile.target_team_filter = &"enemy"
	combat_profile.range_value = 5
	combat_profile.area_pattern = &"diamond"
	combat_profile.area_value = 1
	combat_profile.ap_cost = 1
	var effect_defs: Array[CombatEffectDef] = [damage_effect]
	combat_profile.effect_defs = effect_defs

	var skill := SkillDef.new()
	skill.skill_id = skill_id
	skill.display_name = String(skill_id)
	skill.combat_profile = combat_profile
	return skill


func _collect_damage_resolution_lines(log_lines: Array, actor_name: String) -> Array[String]:
	var resolution_lines: Array[String] = []
	for log_line_variant in log_lines:
		var log_line := String(log_line_variant)
		if not log_line.contains(actor_name):
			continue
		if not log_line.contains("造成"):
			continue
		resolution_lines.append(log_line)
	return resolution_lines


func _extract_string_name_array(values: Array[StringName]) -> Array[String]:
	var result: Array[String] = []
	for value in values:
		result.append(String(value))
	return result


func _build_skill_test_state(map_size: Vector2i) -> BattleState:
	var state := BattleState.new()
	state.battle_id = &"skill_runtime_smoke"
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


func _get_large_charge_direction_cases() -> Array[Dictionary]:
	return [
		{
			"label": "向右",
			"direction": Vector2i.RIGHT,
			"map_size": Vector2i(7, 7),
			"start_coord": Vector2i(1, 2),
			"short_target_coord": Vector2i(3, 2),
			"target_coord": Vector2i(5, 2),
			"first_anchor": Vector2i(2, 2),
			"partial_frontier_coord": Vector2i(3, 2),
			"side_push_blocker_coord": Vector2i(3, 3),
			"side_push_coord": Vector2i(3, 4),
			"forward_blocker_coord": Vector2i(3, 2),
			"forward_coord": Vector2i(4, 2),
			"large_blocker_anchor": Vector2i(3, 2),
			"trap_coord": Vector2i(3, 3),
		},
		{
			"label": "向左",
			"direction": Vector2i.LEFT,
			"map_size": Vector2i(7, 7),
			"start_coord": Vector2i(4, 2),
			"short_target_coord": Vector2i(3, 2),
			"target_coord": Vector2i(1, 2),
			"first_anchor": Vector2i(3, 2),
			"partial_frontier_coord": Vector2i(3, 2),
			"side_push_blocker_coord": Vector2i(3, 3),
			"side_push_coord": Vector2i(3, 4),
			"forward_blocker_coord": Vector2i(3, 2),
			"forward_coord": Vector2i(2, 2),
			"large_blocker_anchor": Vector2i(2, 2),
			"trap_coord": Vector2i(3, 3),
		},
		{
			"label": "向下",
			"direction": Vector2i.DOWN,
			"map_size": Vector2i(7, 7),
			"start_coord": Vector2i(2, 1),
			"short_target_coord": Vector2i(2, 3),
			"target_coord": Vector2i(2, 5),
			"first_anchor": Vector2i(2, 2),
			"partial_frontier_coord": Vector2i(2, 3),
			"side_push_blocker_coord": Vector2i(3, 3),
			"side_push_coord": Vector2i(4, 3),
			"forward_blocker_coord": Vector2i(2, 3),
			"forward_coord": Vector2i(2, 4),
			"large_blocker_anchor": Vector2i(2, 3),
			"trap_coord": Vector2i(3, 3),
		},
		{
			"label": "向上",
			"direction": Vector2i.UP,
			"map_size": Vector2i(7, 7),
			"start_coord": Vector2i(2, 4),
			"short_target_coord": Vector2i(2, 3),
			"target_coord": Vector2i(2, 1),
			"first_anchor": Vector2i(2, 3),
			"partial_frontier_coord": Vector2i(2, 3),
			"side_push_blocker_coord": Vector2i(3, 3),
			"side_push_coord": Vector2i(4, 3),
			"forward_blocker_coord": Vector2i(2, 3),
			"forward_coord": Vector2i(2, 2),
			"large_blocker_anchor": Vector2i(2, 2),
			"trap_coord": Vector2i(3, 3),
		},
	]


func _build_large_charge_fixture(case_data: Dictionary, use_short_target: bool) -> Dictionary:
	var registry := ProgressionContentRegistry.new()
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})

	var state := _build_skill_test_state(case_data.get("map_size", Vector2i(7, 7)))
	var charger_id := StringName("large_charge_tester_%s" % String(case_data.get("label", "dir")))
	var charger := _build_unit(charger_id, case_data.get("start_coord", Vector2i.ZERO), 1)
	charger.body_size = 3
	charger.refresh_footprint()
	charger.known_active_skill_ids = [&"charge"]
	charger.known_skill_level_map = {&"charge": 1}
	state.units = {charger.unit_id: charger}
	state.ally_unit_ids = [charger.unit_id]
	state.active_unit_id = charger.unit_id
	_assert_true(runtime._grid_service.place_unit(state, charger, charger.coord, true), "2x2 冲锋夹具中的测试单位应能成功放入起点。")
	runtime._state = state

	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = charger.unit_id
	command.skill_id = &"charge"
	command.target_coord = case_data.get("short_target_coord" if use_short_target else "target_coord", Vector2i.ZERO)
	return {
		"runtime": runtime,
		"state": state,
		"charger": charger,
		"command": command,
	}


func _set_cell_height(state: BattleState, coord: Vector2i, height: int) -> void:
	var cell := state.cells.get(coord) as BattleCellState
	if cell == null:
		return
	cell.base_height = height
	cell.recalculate_runtime_values()
	state.mark_runtime_edges_dirty()


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
