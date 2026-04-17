## 文件说明：该脚本属于战斗运行时模块相关的模块脚本，集中维护角色网关、技能定义集合、敌方模板集合等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name BattleRuntimeModule
extends RefCounted

const BATTLE_STATE_SCRIPT = preload("res://scripts/systems/battle_state.gd")
const BATTLE_TIMELINE_STATE_SCRIPT = preload("res://scripts/systems/battle_timeline_state.gd")
const BATTLE_EVENT_BATCH_SCRIPT = preload("res://scripts/systems/battle_event_batch.gd")
const BATTLE_PREVIEW_SCRIPT = preload("res://scripts/systems/battle_preview.gd")
const BATTLE_CELL_STATE_SCRIPT = preload("res://scripts/systems/battle_cell_state.gd")
const BATTLE_TERRAIN_EFFECT_STATE_SCRIPT = preload("res://scripts/systems/battle_terrain_effect_state.gd")
const BATTLE_GRID_SERVICE_SCRIPT = preload("res://scripts/systems/battle_grid_service.gd")
const BATTLE_TERRAIN_GENERATOR_SCRIPT = preload("res://scripts/systems/battle_terrain_generator.gd")
const BATTLE_DAMAGE_RESOLVER_SCRIPT = preload("res://scripts/systems/battle_damage_resolver.gd")
const BATTLE_HIT_RESOLVER_SCRIPT = preload("res://scripts/systems/battle_hit_resolver.gd")
const BATTLE_STATUS_SEMANTIC_TABLE_SCRIPT = preload("res://scripts/systems/battle_status_semantic_table.gd")
const BATTLE_AI_SERVICE_SCRIPT = preload("res://scripts/systems/battle_ai_service.gd")
const BATTLE_AI_DECISION_SCRIPT = preload("res://scripts/systems/battle_ai_decision.gd")
const BATTLE_AI_CONTEXT_SCRIPT = preload("res://scripts/systems/battle_ai_context.gd")
const BATTLE_TERRAIN_EFFECT_SYSTEM_SCRIPT = preload("res://scripts/systems/battle_terrain_effect_system.gd")
const BATTLE_RATING_SYSTEM_SCRIPT = preload("res://scripts/systems/battle_rating_system.gd")
const BATTLE_UNIT_FACTORY_SCRIPT = preload("res://scripts/systems/battle_unit_factory.gd")
const BATTLE_CHARGE_RESOLVER_SCRIPT = preload("res://scripts/systems/battle_charge_resolver.gd")
const BATTLE_REPEAT_ATTACK_RESOLVER_SCRIPT = preload("res://scripts/systems/battle_repeat_attack_resolver.gd")
const BATTLE_TERRAIN_TOPOLOGY_SERVICE_SCRIPT = preload("res://scripts/systems/battle_terrain_topology_service.gd")
const BATTLE_TARGET_COLLECTION_SERVICE_SCRIPT = preload("res://scripts/systems/battle_target_collection_service.gd")
const ENCOUNTER_ROSTER_BUILDER_SCRIPT = preload("res://scripts/systems/encounter_roster_builder.gd")
const BATTLE_RESOLUTION_RESULT_SCRIPT = preload("res://scripts/systems/battle_resolution_result.gd")
const BattleState = preload("res://scripts/systems/battle_state.gd")
const BattleTimelineState = preload("res://scripts/systems/battle_timeline_state.gd")
const BattleEventBatch = preload("res://scripts/systems/battle_event_batch.gd")
const BattlePreview = preload("res://scripts/systems/battle_preview.gd")
const BattleCellState = preload("res://scripts/systems/battle_cell_state.gd")
const BattleGridService = preload("res://scripts/systems/battle_grid_service.gd")
const BattleDamageResolver = preload("res://scripts/systems/battle_damage_resolver.gd")
const BattleHitResolver = preload("res://scripts/systems/battle_hit_resolver.gd")
const BattleStatusSemanticTable = preload("res://scripts/systems/battle_status_semantic_table.gd")
const BattleAiService = preload("res://scripts/systems/battle_ai_service.gd")
const BattleAiDecision = preload("res://scripts/systems/battle_ai_decision.gd")
const BattleAiContext = preload("res://scripts/systems/battle_ai_context.gd")
const BattleCommand = preload("res://scripts/systems/battle_command.gd")
const BattleUnitState = preload("res://scripts/systems/battle_unit_state.gd")
const BattleTerrainRules = preload("res://scripts/systems/battle_terrain_rules.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attribute_service.gd")
const CombatCastVariantDef = preload("res://scripts/player/progression/combat_cast_variant_def.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const REPEAT_ATTACK_EFFECT_TYPE: StringName = &"repeat_attack_until_fail"
const TERRAIN_EFFECT_STATUS: StringName = &"status"
const MIN_BATTLE_SURFACE_HEIGHT := 4
const STATUS_PINNED: StringName = &"pinned"
const STATUS_ROOTED: StringName = &"rooted"
const STATUS_TENDON_CUT: StringName = &"tendon_cut"
const STATUS_STAGGERED: StringName = &"staggered"
const STATUS_TAUNTED: StringName = &"taunted"
const STATUS_ARCHER_RANGE_UP: StringName = &"archer_range_up"
const STATUS_ARCHER_QUICKSTEP: StringName = &"archer_quickstep"
const REPEAT_ATTACK_STAGE_GUARD := 32

## 字段说明：缓存角色网关实例，会参与运行时状态流转、系统协作和存档恢复。
var _character_gateway: Object = null
## 字段说明：缓存技能定义集合字典，集中保存可按键查询的运行时数据。
var _skill_defs: Dictionary = {}
## 字段说明：缓存敌方模板集合字典，集中保存可按键查询的运行时数据。
var _enemy_templates: Dictionary = {}
## 字段说明：缓存敌方 AI brain 集合字典，集中保存可按键查询的运行时数据。
var _enemy_ai_brains: Dictionary = {}
## 字段说明：记录遭遇构建器，会参与运行时状态流转、系统协作和存档恢复。
var _encounter_builder = ENCOUNTER_ROSTER_BUILDER_SCRIPT.new()
## 字段说明：缓存状态对象实例，会参与运行时状态流转、系统协作和存档恢复。
var _state: BattleState = null
## 字段说明：记录网格服务，会参与运行时状态流转、系统协作和存档恢复。
var _grid_service := BATTLE_GRID_SERVICE_SCRIPT.new()
## 字段说明：记录地形生成器，会参与运行时状态流转、系统协作和存档恢复。
var _terrain_generator := BATTLE_TERRAIN_GENERATOR_SCRIPT.new()
## 字段说明：记录伤害解析器，会参与运行时状态流转、系统协作和存档恢复。
var _damage_resolver := BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
## 字段说明：记录命中解析器，会参与运行时状态流转、系统协作和 deterministic 掷骰。
var _hit_resolver: BattleHitResolver = BATTLE_HIT_RESOLVER_SCRIPT.new()
## 字段说明：记录自动决策服务，会参与运行时状态流转、系统协作和存档恢复。
var _ai_service: BattleAiService = BATTLE_AI_SERVICE_SCRIPT.new()
var _terrain_effect_system = BATTLE_TERRAIN_EFFECT_SYSTEM_SCRIPT.new()
var _battle_rating_system = BATTLE_RATING_SYSTEM_SCRIPT.new()
var _unit_factory = BATTLE_UNIT_FACTORY_SCRIPT.new()
var _charge_resolver = BATTLE_CHARGE_RESOLVER_SCRIPT.new()
var _repeat_attack_resolver = BATTLE_REPEAT_ATTACK_RESOLVER_SCRIPT.new()
var _terrain_topology_service = BATTLE_TERRAIN_TOPOLOGY_SERVICE_SCRIPT.new()
var _target_collection_service = BATTLE_TARGET_COLLECTION_SERVICE_SCRIPT.new()
## 字段说明：缓存战斗评分统计字典，集中保存可按键查询的运行时数据。
var _battle_rating_stats: Dictionary = {}
## 字段说明：保存待处理后置战斗角色奖励列表，便于顺序遍历、批量展示、批量运算和整体重建。
var _pending_post_battle_character_rewards: Array = []
## 字段说明：缓存当前战斗的正式掉落条目，供 canonical battle resolution result 直接消费。
var _active_loot_entries: Array = []
## 字段说明：缓存战斗结算结果，便于结算完成后由 session facade 统一消费。
var _battle_resolution_result = null
## 字段说明：记录战斗结算结果是否已经被消费，避免重复重建与重复提交。
var _battle_resolution_result_consumed := false
## 字段说明：记录地形效果序号，会参与运行时状态流转、系统协作和存档恢复。
var _terrain_effect_nonce := 0


func setup(
	character_gateway: Object = null,
	skill_defs: Dictionary = {},
	enemy_templates: Dictionary = {},
	enemy_ai_brains: Dictionary = {},
	encounter_builder: Object = null
) -> void:
	_character_gateway = character_gateway
	_skill_defs = skill_defs if skill_defs != null else {}
	_enemy_templates = enemy_templates if enemy_templates != null else {}
	_enemy_ai_brains = enemy_ai_brains if enemy_ai_brains != null else {}
	_encounter_builder = encounter_builder if encounter_builder != null else ENCOUNTER_ROSTER_BUILDER_SCRIPT.new()
	_ai_service.setup(_enemy_ai_brains, _damage_resolver)
	_terrain_effect_system.setup(self)
	_battle_rating_system.setup(self)
	_unit_factory.setup(self)
	_charge_resolver.setup(self)
	_repeat_attack_resolver.setup(self)


func start_battle(
	encounter_anchor,
	seed: int,
	context: Dictionary = {}
) -> BattleState:
	_ensure_sidecars_ready()
	var party_state = _character_gateway.get_party_state() if _character_gateway != null else null
	var ally_units: Array = _unit_factory.build_ally_units(party_state, context)
	if ally_units.is_empty():
		ally_units = _unit_factory.build_ally_units(null, context)

	var enemy_units: Array = []
	var enemy_build_context := context.duplicate(true)
	enemy_build_context["skill_defs"] = _skill_defs
	enemy_build_context["enemy_templates"] = _enemy_templates
	enemy_build_context["enemy_ai_brains"] = _enemy_ai_brains
	_active_loot_entries.clear()
	if _encounter_builder != null:
		enemy_units = _encounter_builder.build_enemy_units(encounter_anchor, enemy_build_context)
		_active_loot_entries = _encounter_builder.build_loot_entries(encounter_anchor, enemy_build_context)
	if enemy_units.is_empty():
		enemy_units = _unit_factory.build_enemy_units(encounter_anchor, enemy_build_context)
	var terrain_data := _unit_factory.build_terrain_data(encounter_anchor, seed, context)
	if terrain_data.is_empty():
		return BATTLE_STATE_SCRIPT.new()

	_state = BATTLE_STATE_SCRIPT.new()
	_state.battle_id = ProgressionDataUtils.to_string_name("%s_%d" % [String(encounter_anchor.entity_id), seed])
	_state.seed = seed
	_state.map_size = terrain_data.get("map_size", Vector2i.ZERO)
	_state.world_coord = context.get("world_coord", encounter_anchor.world_coord if encounter_anchor != null else Vector2i.ZERO)
	_state.encounter_anchor_id = ProgressionDataUtils.to_string_name(encounter_anchor.entity_id if encounter_anchor != null else "")
	_state.terrain_profile_id = ProgressionDataUtils.to_string_name(
		terrain_data.get("terrain_profile_id", context.get("battle_terrain_profile", "default"))
	)
	_state.cells = terrain_data.get("cells", {})
	_state.cell_columns = terrain_data.get("cell_columns", BattleCellState.build_columns_from_surface_cells(_state.cells))
	_state.timeline = BATTLE_TIMELINE_STATE_SCRIPT.new()
	_state.timeline.units_per_second = int(context.get("units_per_second", _state.timeline.units_per_second))
	_state.timeline.action_threshold = int(context.get("action_threshold", _state.timeline.action_threshold))

	_place_units(ally_units, terrain_data.get("ally_spawns", []), true)
	_place_units(enemy_units, terrain_data.get("enemy_spawns", []), false)
	_state.phase = &"timeline_running"
	_state.active_unit_id = &""
	_state.winner_faction_id = &""
	_state.modal_state = &""
	_state.attack_roll_nonce = 0
	_state.log_entries = ["战斗开始：%s" % encounter_anchor.display_name]
	_battle_rating_system.initialize_battle_rating_stats()
	_terrain_effect_nonce = 0
	_battle_resolution_result = null
	_battle_resolution_result_consumed = false
	return _state


func advance(delta_seconds: float) -> BattleEventBatch:
	_ensure_sidecars_ready()
	var batch := _new_batch()
	if _state == null or _state.phase == &"battle_ended":
		return batch
	if _state.modal_state != &"":
		return batch
	if _state.timeline != null and _state.timeline.frozen:
		return batch

	if _state.phase == &"unit_acting":
		var active_unit := _state.units.get(_state.active_unit_id) as BattleUnitState
		if active_unit != null and active_unit.is_alive and active_unit.control_mode != &"manual":
			var ai_context := BATTLE_AI_CONTEXT_SCRIPT.new()
			ai_context.state = _state
			ai_context.unit_state = active_unit
			ai_context.grid_service = _grid_service
			ai_context.skill_defs = _skill_defs
			ai_context.preview_callback = Callable(self, "preview_command")
			ai_context.skill_score_input_callback = Callable(_ai_service, "build_skill_score_input")
			var decision: BattleAiDecision = _ai_service.choose_command(ai_context)
			if decision != null and decision.command != null:
				var ai_line := "AI[%s/%s/%s] %s" % [
					String(decision.brain_id),
					String(decision.state_id),
					String(decision.action_id),
					decision.reason_text,
				]
				_state.log_entries.append(ai_line)
				var decision_command: BattleCommand = decision.command
				var decision_batch := issue_command(decision_command)
				if decision_batch != null:
					decision_batch.log_lines.insert(0, ai_line)
				return decision_batch
		return batch

	if delta_seconds > 0.0:
		if _use_discrete_timeline_ticks():
			_state.timeline.delta_remainder += delta_seconds
			while _state.timeline.delta_remainder >= _state.timeline.tick_interval_seconds:
				_state.timeline.delta_remainder -= _state.timeline.tick_interval_seconds
				_apply_timeline_step(batch, _state.timeline.tick_interval_seconds, _state.timeline.tu_per_tick)
				if _check_battle_end(batch):
					return batch
		else:
			_apply_timeline_step(batch, delta_seconds, int(round(delta_seconds * float(_state.timeline.units_per_second))))
			if _check_battle_end(batch):
				return batch

	if _state.phase == &"timeline_running":
		_activate_next_ready_unit(batch)

	return batch


func _use_discrete_timeline_ticks() -> bool:
	return _state != null \
		and _state.timeline != null \
		and _state.timeline.tick_interval_seconds > 0.0 \
		and _state.timeline.tu_per_tick > 0


func _apply_timeline_step(batch: BattleEventBatch, delta_seconds: float, tu_delta: int) -> void:
	if _state == null or _state.timeline == null:
		return
	if tu_delta > 0:
		_state.timeline.current_tu += tu_delta
	for unit_id in _get_living_units_in_order():
		var unit_state := _state.units.get(unit_id) as BattleUnitState
		if unit_state == null or not unit_state.is_alive:
			continue
		if tu_delta > 0 and _advance_unit_status_durations(unit_state, tu_delta):
			_append_changed_unit_id(batch, unit_state.unit_id)
		var speed := 1
		if unit_state.attribute_snapshot != null:
			speed = maxi(unit_state.attribute_snapshot.get_value(&"speed"), 1)
		unit_state.action_progress += maxi(int(round(delta_seconds * float(speed) * 12.0)), 1)
		while unit_state.action_progress >= _state.timeline.action_threshold:
			unit_state.action_progress -= _state.timeline.action_threshold
			if not _state.timeline.ready_unit_ids.has(unit_id):
				_state.timeline.ready_unit_ids.append(unit_id)
	_terrain_effect_system.process_timed_terrain_effects(batch)


func preview_command(command: BattleCommand) -> BattlePreview:
	_ensure_sidecars_ready()
	var preview := BATTLE_PREVIEW_SCRIPT.new()
	if _state == null or command == null or _state.phase == &"battle_ended":
		return preview

	var active_unit := _state.units.get(command.unit_id) as BattleUnitState
	if active_unit == null or not active_unit.is_alive:
		return preview

	match command.command_type:
		BattleCommand.TYPE_MOVE:
			if _is_movement_blocked(active_unit):
				preview.log_lines.append("%s 当前被限制移动。" % active_unit.display_name)
				return preview
			var move_result := _resolve_move_path_result(active_unit, command.target_coord)
			if bool(move_result.get("allowed", false)):
				preview.allowed = true
				var move_cost := int(move_result.get("cost", 0))
				preview.log_lines.append("移动可执行，消耗 %d 点行动点。" % move_cost)
				for target_coord in _grid_service.get_unit_target_coords(active_unit, command.target_coord):
					preview.target_coords.append(target_coord)
			else:
				preview.log_lines.append(String(move_result.get("message", "该移动不可执行。")))
		BattleCommand.TYPE_SKILL:
			_preview_skill_command(active_unit, command, preview)
		BattleCommand.TYPE_WAIT:
			preview.allowed = true
			preview.log_lines.append("%s 可以结束行动。" % active_unit.display_name)
		_:
			preview.log_lines.append("未知命令类型。")
	return preview


func issue_command(command: BattleCommand) -> BattleEventBatch:
	_ensure_sidecars_ready()
	var batch := _new_batch()
	if _state == null or command == null:
		return batch
	if _state.phase != &"unit_acting":
		return batch
	if _state.modal_state != &"":
		return batch

	var active_unit := _state.units.get(_state.active_unit_id) as BattleUnitState
	if active_unit == null or active_unit.unit_id != command.unit_id or not active_unit.is_alive:
		return batch
	_ensure_unit_turn_anchor(active_unit)

	match command.command_type:
		BattleCommand.TYPE_MOVE:
			_handle_move_command(active_unit, command, batch)
		BattleCommand.TYPE_SKILL:
			_handle_skill_command(active_unit, command, batch)
		BattleCommand.TYPE_WAIT:
			batch.log_lines.append("%s 结束行动。" % active_unit.display_name)
		_:
			return batch

	for line in batch.log_lines:
		_state.log_entries.append(line)

	if _state.modal_state != &"":
		batch.modal_requested = true
		return batch

	if _check_battle_end(batch):
		return batch

	if active_unit.current_ap <= 0 or not active_unit.is_alive or command.command_type == BattleCommand.TYPE_WAIT:
		_end_active_turn(batch)

	return batch


func submit_promotion_choice(
	member_id: StringName,
	profession_id: StringName,
	selection: Dictionary
) -> BattleEventBatch:
	_ensure_sidecars_ready()
	var batch := _new_batch()
	if _state == null or _character_gateway == null:
		return batch
	var delta = _character_gateway.promote_profession(member_id, profession_id, selection)
	batch.progression_deltas.append(delta)
	var unit_state := _find_unit_by_member_id(member_id)
	if unit_state != null:
		_unit_factory.refresh_battle_unit(unit_state)
		batch.changed_unit_ids.append(unit_state.unit_id)
		batch.log_lines.append("%s 完成职业晋升。" % unit_state.display_name)
	_state.modal_state = &""
	_state.timeline.frozen = false
	return batch


func get_state() -> BattleState:
	return _state


func _ensure_sidecars_ready() -> void:
	_terrain_effect_system.setup(self)
	_battle_rating_system.setup(self)
	_unit_factory.setup(self)
	_charge_resolver.setup(self)
	_repeat_attack_resolver.setup(self)


func is_battle_active() -> bool:
	return _state != null and _state.phase != &"battle_ended"


func get_unit_reachable_move_coords(unit_state: BattleUnitState) -> Array[Vector2i]:
	if _state == null or unit_state == null or not unit_state.is_alive:
		return []
	if _is_movement_blocked(unit_state):
		return []

	var origin := unit_state.coord
	var max_move_points := maxi(int(unit_state.current_ap), 0)
	var origin_has_quickstep_bonus := _has_status(unit_state, STATUS_ARCHER_QUICKSTEP)
	var best_state_costs := {
		_build_reachable_move_state_key(origin, origin_has_quickstep_bonus): 0,
	}
	var best_coord_costs := {
		origin: 0,
	}
	var buckets := _build_reachable_move_buckets(max_move_points)
	buckets[0].append({
		"coord": origin,
		"spent_cost": 0,
		"has_quickstep_bonus": origin_has_quickstep_bonus,
	})
	for current_cost in range(max_move_points + 1):
		var bucket_index := 0
		while bucket_index < buckets[current_cost].size():
			var frontier_entry: Dictionary = buckets[current_cost][bucket_index]
			bucket_index += 1
			var current_coord: Vector2i = frontier_entry.get("coord", origin)
			var spent_cost := int(frontier_entry.get("spent_cost", current_cost))
			var has_quickstep_bonus := bool(frontier_entry.get("has_quickstep_bonus", false))
			var current_state_key := _build_reachable_move_state_key(current_coord, has_quickstep_bonus)
			if spent_cost != current_cost:
				continue
			if spent_cost != int(best_state_costs.get(current_state_key, 2147483647)):
				continue
			for neighbor_coord in _grid_service.get_neighbors_4(_state, current_coord):
				if not _grid_service.can_unit_step_between_anchors(_state, unit_state, current_coord, neighbor_coord):
					continue
				var move_cost := _get_move_cost_for_unit_target(unit_state, neighbor_coord, has_quickstep_bonus)
				var next_cost := spent_cost + move_cost
				if next_cost > max_move_points:
					continue
				var next_has_quickstep_bonus := false
				var next_state_key := _build_reachable_move_state_key(neighbor_coord, next_has_quickstep_bonus)
				var best_state_cost := int(best_state_costs.get(next_state_key, 2147483647))
				if next_cost >= best_state_cost:
					continue
				best_state_costs[next_state_key] = next_cost
				best_coord_costs[neighbor_coord] = mini(int(best_coord_costs.get(neighbor_coord, 2147483647)), next_cost)
				buckets[next_cost].append({
					"coord": neighbor_coord,
					"spent_cost": next_cost,
					"has_quickstep_bonus": next_has_quickstep_bonus,
				})

	best_coord_costs.erase(origin)
	return _sort_coords(_collect_dict_vector2i_keys(best_coord_costs))


func end_battle(result: Dictionary = {}) -> void:
	if _state == null:
		return
	if _character_gateway != null and bool(result.get("commit_progression", false)):
		for ally_unit_id in _state.ally_unit_ids:
			var unit_state := _state.units.get(ally_unit_id) as BattleUnitState
			if unit_state == null:
				continue
			if unit_state.is_alive:
				_character_gateway.commit_battle_resources(unit_state.source_member_id, unit_state.current_hp, unit_state.current_mp)
			else:
				_character_gateway.commit_battle_ko(unit_state.source_member_id)
		_character_gateway.flush_after_battle()
	if _battle_resolution_result == null and not _battle_resolution_result_consumed and _state.phase == &"battle_ended":
		_battle_resolution_result = _build_battle_resolution_result()


func get_battle_resolution_result():
	return _battle_resolution_result if not _battle_resolution_result_consumed else null


func consume_battle_resolution_result():
	if _battle_resolution_result_consumed:
		return null
	var resolution_result = _battle_resolution_result
	if resolution_result == null and _state != null and _state.phase == &"battle_ended":
		resolution_result = _build_battle_resolution_result()
	if resolution_result != null:
		_pending_post_battle_character_rewards.clear()
		_active_loot_entries.clear()
	_battle_resolution_result = null
	_battle_resolution_result_consumed = true
	return resolution_result


func get_grid_service():
	return _grid_service


func get_character_gateway():
	return _character_gateway


func get_damage_resolver():
	return _damage_resolver


func get_hit_resolver():
	return _hit_resolver


func get_terrain_generator():
	return _terrain_generator


func get_skill_defs() -> Dictionary:
	return _skill_defs


func get_battle_rating_stats() -> Dictionary:
	return _battle_rating_stats


func get_battle_rating_system():
	return _battle_rating_system


func get_pending_post_battle_character_rewards() -> Array:
	return _pending_post_battle_character_rewards


func get_terrain_effect_nonce() -> int:
	return _terrain_effect_nonce


func increment_terrain_effect_nonce() -> int:
	_terrain_effect_nonce += 1
	return _terrain_effect_nonce


func new_batch() -> BattleEventBatch:
	return _new_batch()


func merge_batch(target_batch: BattleEventBatch, source_batch: BattleEventBatch) -> void:
	_merge_batch(target_batch, source_batch)


func append_changed_coord(batch: BattleEventBatch, coord: Vector2i) -> void:
	_append_changed_coord(batch, coord)


func append_changed_coords(batch: BattleEventBatch, coords: Array[Vector2i]) -> void:
	_append_changed_coords(batch, coords)


func append_changed_unit_id(batch: BattleEventBatch, unit_id: StringName) -> void:
	_append_changed_unit_id(batch, unit_id)


func append_changed_unit_coords(batch: BattleEventBatch, unit_state: BattleUnitState) -> void:
	_append_changed_unit_coords(batch, unit_state)


func append_batch_log(batch: BattleEventBatch, message: String) -> void:
	_append_batch_log(batch, message)


func clear_defeated_unit(unit_state: BattleUnitState, batch: BattleEventBatch = null) -> void:
	_clear_defeated_unit(unit_state, batch)


func sort_coords(target_coords: Variant) -> Array[Vector2i]:
	return _sort_coords(target_coords)


func format_skill_variant_label(skill_def: SkillDef, cast_variant: CombatCastVariantDef) -> String:
	return _format_skill_variant_label(skill_def, cast_variant)


func mark_applied_statuses_for_turn_timing(target_unit: BattleUnitState, status_effect_ids: Variant) -> void:
	# Legacy wrapper: status durations now decay on timeline TU progression instead of turn end.
	return


func resolve_effect_target_filter(skill_def: SkillDef, effect_def: CombatEffectDef) -> StringName:
	return _resolve_effect_target_filter(skill_def, effect_def)


func is_unit_valid_for_effect(source_unit: BattleUnitState, target_unit: BattleUnitState, target_filter: StringName) -> bool:
	return _is_unit_valid_for_effect(source_unit, target_unit, target_filter)


func is_unit_effect(effect_def: CombatEffectDef) -> bool:
	return _is_unit_effect(effect_def)


func collect_units_in_coords(effect_coords: Array[Vector2i]) -> Array[BattleUnitState]:
	return _collect_units_in_coords(effect_coords)


func get_unit_skill_level(unit_state: BattleUnitState, skill_id: StringName) -> int:
	return _get_unit_skill_level(unit_state, skill_id)


func record_enemy_defeated_achievement(active_unit: BattleUnitState, target_unit: BattleUnitState) -> void:
	_battle_rating_system.record_enemy_defeated_achievement(active_unit, target_unit)


func record_skill_effect_result(source_unit: BattleUnitState, damage: int, healing: int, kill_count: int) -> void:
	_battle_rating_system.record_skill_effect_result(source_unit, damage, healing, kill_count)


func dispose() -> void:
	if _terrain_effect_system != null:
		_terrain_effect_system.dispose()
	if _battle_rating_system != null:
		_battle_rating_system.dispose()
	if _unit_factory != null:
		_unit_factory.dispose()
	if _charge_resolver != null:
		_charge_resolver.dispose()
	if _repeat_attack_resolver != null:
		_repeat_attack_resolver.dispose()
	_battle_rating_stats.clear()
	_pending_post_battle_character_rewards.clear()
	_active_loot_entries.clear()
	_battle_resolution_result = null
	_battle_resolution_result_consumed = false
	_terrain_effect_nonce = 0
	_character_gateway = null
	_skill_defs = {}
	_enemy_templates = {}
	_enemy_ai_brains = {}
	_encounter_builder = null
	if _state != null:
		_state.cells.clear()
		_state.units.clear()
		_state.ally_unit_ids.clear()
		_state.enemy_unit_ids.clear()
		if _state.timeline != null:
			_state.timeline.ready_unit_ids.clear()
	_state = null


func _place_units(units: Array, spawn_coords: Array, is_ally: bool) -> void:
	for index in range(units.size()):
		var unit_state := units[index] as BattleUnitState
		if unit_state == null:
			continue
		unit_state.refresh_footprint()
		_state.units[unit_state.unit_id] = unit_state
		var preferred_coords: Array[Vector2i] = []
		if index < spawn_coords.size():
			preferred_coords.append(spawn_coords[index])
		for spawn_coord in spawn_coords:
			var coord: Vector2i = spawn_coord
			if not preferred_coords.has(coord):
				preferred_coords.append(coord)
		var placement_coord := _find_spawn_anchor(unit_state, preferred_coords)
		if placement_coord == Vector2i(-1, -1):
			_state.units.erase(unit_state.unit_id)
			continue
		_grid_service.place_unit(_state, unit_state, placement_coord, true)
		if is_ally:
			_state.ally_unit_ids.append(unit_state.unit_id)
		else:
			_state.enemy_unit_ids.append(unit_state.unit_id)


func _find_spawn_anchor(unit_state: BattleUnitState, preferred_coords: Array[Vector2i]) -> Vector2i:
	if _state == null or unit_state == null:
		return Vector2i(-1, -1)
	var best_coord := Vector2i(-1, -1)
	var best_score := -2147483647
	for preferred_index in range(preferred_coords.size()):
		var coord: Vector2i = preferred_coords[preferred_index]
		if not _grid_service.can_place_footprint(_state, coord, unit_state.footprint_size, unit_state.unit_id):
			continue
		var score := _score_spawn_anchor(unit_state, coord, preferred_index)
		if score > best_score:
			best_score = score
			best_coord = coord
	if best_coord != Vector2i(-1, -1):
		return best_coord
	for preferred_coord in preferred_coords:
		var coord: Vector2i = preferred_coord
		if _grid_service.can_place_footprint(_state, coord, unit_state.footprint_size, unit_state.unit_id):
			return coord
	for y in range(_state.map_size.y):
		for x in range(_state.map_size.x):
			var coord := Vector2i(x, y)
			if _grid_service.can_place_footprint(_state, coord, unit_state.footprint_size, unit_state.unit_id):
				return coord
	return Vector2i(-1, -1)


func _score_spawn_anchor(unit_state: BattleUnitState, coord: Vector2i, preferred_index: int) -> int:
	var mobility_score := _count_spawn_anchor_reachable_coords(unit_state, coord)
	var edge_clearance := _get_spawn_anchor_edge_clearance(unit_state, coord)
	var center_bias := _get_spawn_anchor_center_bias(unit_state, coord)
	return mobility_score * 100 + edge_clearance * 18 + center_bias * 4 - preferred_index


func _count_spawn_anchor_reachable_coords(unit_state: BattleUnitState, start_coord: Vector2i) -> int:
	if _state == null or unit_state == null:
		return 0
	var move_budget := mini(maxi(int(unit_state.current_ap), 0), 4)
	if move_budget <= 0:
		move_budget = 1
	var best_costs := {
		start_coord: 0,
	}
	var frontier: Array[Vector2i] = [start_coord]
	var frontier_index := 0
	while frontier_index < frontier.size():
		var current_coord := frontier[frontier_index]
		frontier_index += 1
		var spent_cost := int(best_costs.get(current_coord, 0))
		for neighbor_coord in _grid_service.get_neighbors_4(_state, current_coord):
			if not _grid_service.can_unit_step_between_anchors(_state, unit_state, current_coord, neighbor_coord):
				continue
			var move_cost := _grid_service.get_unit_move_cost(_state, unit_state, neighbor_coord)
			var next_cost := spent_cost + move_cost
			if next_cost > move_budget:
				continue
			if next_cost >= int(best_costs.get(neighbor_coord, 2147483647)):
				continue
			best_costs[neighbor_coord] = next_cost
			frontier.append(neighbor_coord)
	return best_costs.size() - 1


func _get_spawn_anchor_edge_clearance(unit_state: BattleUnitState, coord: Vector2i) -> int:
	if _state == null or unit_state == null:
		return 0
	var footprint := unit_state.footprint_size
	var left_clearance := coord.x
	var top_clearance := coord.y
	var right_clearance := _state.map_size.x - (coord.x + footprint.x)
	var bottom_clearance := _state.map_size.y - (coord.y + footprint.y)
	return mini(mini(left_clearance, right_clearance), mini(top_clearance, bottom_clearance))


func _get_spawn_anchor_center_bias(unit_state: BattleUnitState, coord: Vector2i) -> int:
	if _state == null or unit_state == null:
		return 0
	var footprint := unit_state.footprint_size
	var center_x := float(_state.map_size.x - footprint.x) * 0.5
	var center_y := float(_state.map_size.y - footprint.y) * 0.5
	var distance := absf(float(coord.x) - center_x) + absf(float(coord.y) - center_y)
	return -int(round(distance * 10.0))


func _get_move_cost_for_unit_target(
	unit_state: BattleUnitState,
	target_coord: Vector2i,
	allow_quickstep_bonus: bool = true
) -> int:
	if _state == null or unit_state == null:
		return 1
	var move_cost := _grid_service.get_unit_move_cost(_state, unit_state, target_coord)
	move_cost += _get_status_move_cost_delta(unit_state)
	if allow_quickstep_bonus and _has_status(unit_state, STATUS_ARCHER_QUICKSTEP):
		move_cost = maxi(move_cost - 1, 0)
	return move_cost


func _get_move_path_cost(unit_state: BattleUnitState, anchor_path: Array[Vector2i]) -> int:
	if unit_state == null or anchor_path.size() <= 1:
		return 0
	var total_cost := 0
	var allow_quickstep_bonus := _has_status(unit_state, STATUS_ARCHER_QUICKSTEP)
	for path_index in range(1, anchor_path.size()):
		total_cost += _get_move_cost_for_unit_target(unit_state, anchor_path[path_index], allow_quickstep_bonus)
		allow_quickstep_bonus = false
	return total_cost


func _get_status_move_cost_delta(unit_state: BattleUnitState) -> int:
	if unit_state == null:
		return 0
	var total_delta := 0
	for status_id_str in ProgressionDataUtils.sorted_string_keys(unit_state.status_effects):
		var status_entry = unit_state.get_status_effect(StringName(status_id_str))
		total_delta += BATTLE_STATUS_SEMANTIC_TABLE_SCRIPT.get_move_cost_delta(status_entry)
	return maxi(total_delta, 0)


func _resolve_move_path_result(active_unit: BattleUnitState, target_coord: Vector2i) -> Dictionary:
	if _state == null or active_unit == null:
		return {
			"allowed": false,
			"cost": 0,
			"path": [],
			"message": "当前单位数据不可用。",
		}
	var first_step_cost_discount := 1 if _has_status(active_unit, STATUS_ARCHER_QUICKSTEP) else 0
	var move_result := _grid_service.resolve_unit_move_path(
		_state,
		active_unit,
		active_unit.coord,
		target_coord,
		maxi(int(active_unit.current_ap), 0),
		first_step_cost_discount
	)
	var anchor_path: Array[Vector2i] = []
	var path_variant = move_result.get("path", [])
	if path_variant is Array:
		for coord_variant in path_variant:
			if coord_variant is Vector2i:
				anchor_path.append(coord_variant)
	if anchor_path.size() > 1:
		var semantic_cost := _get_move_path_cost(active_unit, anchor_path)
		move_result["cost"] = semantic_cost
		if semantic_cost > maxi(int(active_unit.current_ap), 0):
			move_result["allowed"] = false
			move_result["message"] = "行动点不足，无法移动。"
	return move_result


func _handle_move_command(active_unit: BattleUnitState, command: BattleCommand, batch: BattleEventBatch) -> void:
	if _is_movement_blocked(active_unit):
		batch.log_lines.append("%s 当前被限制移动。" % active_unit.display_name)
		return
	var target_coord := command.target_coord
	var move_result := _resolve_move_path_result(active_unit, target_coord)
	if not bool(move_result.get("allowed", false)):
		batch.log_lines.append(String(move_result.get("message", "该移动不可执行。")))
		return
	var target_cell := _grid_service.get_cell(_state, target_coord)
	if target_cell == null:
		return
	var move_cost := int(move_result.get("cost", 0))

	var previous_coords := active_unit.occupied_coords.duplicate()
	if _grid_service.move_unit(_state, active_unit, target_coord):
		active_unit.current_ap -= move_cost
		_consume_status_if_present(active_unit, STATUS_ARCHER_QUICKSTEP, batch)
		batch.changed_unit_ids.append(active_unit.unit_id)
		_append_changed_coords(batch, previous_coords)
		_append_changed_unit_coords(batch, active_unit)
		var terrain_name := _grid_service.get_terrain_display_name(String(target_cell.base_terrain)) if target_cell != null else "地格"
		batch.log_lines.append("%s 移动到 (%d, %d)，消耗 %d 点行动点。%s" % [
			active_unit.display_name,
			target_coord.x,
			target_coord.y,
			move_cost,
			terrain_name,
		])


func _handle_skill_command(active_unit: BattleUnitState, command: BattleCommand, batch: BattleEventBatch) -> void:
	var skill_def := _skill_defs.get(command.skill_id) as SkillDef
	if skill_def == null or skill_def.combat_profile == null:
		return
	var block_reason := _get_skill_cast_block_reason(active_unit, skill_def)
	if not block_reason.is_empty():
		batch.log_lines.append(block_reason)
		return

	var applied := false
	var cast_variant := _resolve_ground_cast_variant(skill_def, active_unit, command)
	if _should_route_skill_command_to_unit_targeting(skill_def, command):
		applied = _handle_unit_skill_command(active_unit, command, skill_def, cast_variant, batch)
	else:
		if cast_variant != null:
			applied = _handle_ground_skill_command(active_unit, command, skill_def, cast_variant, batch)
		else:
			applied = _handle_unit_skill_command(active_unit, command, skill_def, null, batch)

	if applied:
		_grant_skill_mastery_if_needed(active_unit, command.skill_id, batch)


func _preview_skill_command(active_unit: BattleUnitState, command: BattleCommand, preview: BattlePreview) -> void:
	var skill_def := _skill_defs.get(command.skill_id) as SkillDef
	if skill_def == null or skill_def.combat_profile == null:
		preview.log_lines.append("技能或目标无效。")
		return

	if _should_route_skill_command_to_unit_targeting(skill_def, command):
		_preview_unit_skill_command(active_unit, command, skill_def, preview)
		return

	var cast_variant := _resolve_ground_cast_variant(skill_def, active_unit, command)
	if cast_variant != null:
		_preview_ground_skill_command(active_unit, command, skill_def, cast_variant, preview)
		return

	_preview_unit_skill_command(active_unit, command, skill_def, preview)


func _preview_unit_skill_command(
	active_unit: BattleUnitState,
	command: BattleCommand,
	skill_def: SkillDef,
	preview: BattlePreview
) -> void:
	var block_reason := _get_skill_cast_block_reason(active_unit, skill_def)
	if not block_reason.is_empty():
		preview.log_lines.append(block_reason)
		return

	var validation := _validate_unit_skill_targets(active_unit, command, skill_def)
	preview.allowed = bool(validation.get("allowed", false))
	preview.target_unit_ids.clear()
	for target_unit_id_variant in validation.get("target_unit_ids", []):
		preview.target_unit_ids.append(ProgressionDataUtils.to_string_name(target_unit_id_variant))
	preview.target_coords.clear()
	for preview_coord_variant in validation.get("preview_coords", []):
		if preview_coord_variant is Vector2i:
			preview.target_coords.append(preview_coord_variant)
	if preview.allowed:
		var target_units := validation.get("target_units", []) as Array
		preview.hit_preview = _build_unit_skill_hit_preview(active_unit, target_units, skill_def)
		if target_units.size() == 1:
			var target_unit := target_units[0] as BattleUnitState
			if target_unit != null:
				preview.log_lines.append("%s 可对 %s 使用 %s。" % [active_unit.display_name, target_unit.display_name, skill_def.display_name])
				if not preview.hit_preview.is_empty():
					preview.log_lines.append(String(preview.hit_preview.get("summary_text", "")))
				return
		preview.log_lines.append("%s 可对 %d 个单位使用 %s。" % [
			active_unit.display_name,
			preview.target_unit_ids.size(),
			skill_def.display_name,
		])
		if not preview.hit_preview.is_empty():
			preview.log_lines.append(String(preview.hit_preview.get("summary_text", "")))
		return
	preview.log_lines.append(String(validation.get("message", "技能或目标无效。")))


func _preview_ground_skill_command(
	active_unit: BattleUnitState,
	command: BattleCommand,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	preview: BattlePreview
) -> void:
	var validation := _validate_ground_skill_command(active_unit, skill_def, cast_variant, command)
	preview.target_coords.clear()
	var preview_coords: Array[Vector2i] = validation.get(
		"preview_coords",
		_build_ground_effect_coords(skill_def, validation.get("target_coords", []), active_unit.coord if active_unit != null else Vector2i(-1, -1))
	)
	if bool(validation.get("allowed", false)):
		var path_step_aoe_effect := _charge_resolver.get_charge_path_step_aoe_effect_def(cast_variant)
		if path_step_aoe_effect != null:
			preview_coords = _charge_resolver.build_charge_step_aoe_preview_coords(
				active_unit,
				validation.get("direction", Vector2i.ZERO),
				int(validation.get("distance", 0)),
				path_step_aoe_effect
			)
	for target_coord in preview_coords:
		preview.target_coords.append(target_coord)
	preview.target_unit_ids = _collect_ground_preview_unit_ids(
		active_unit,
		skill_def,
		_collect_ground_unit_effect_defs(skill_def, cast_variant),
		preview.target_coords
	)
	if bool(validation.get("allowed", false)):
		var path_step_aoe_effect := _charge_resolver.get_charge_path_step_aoe_effect_def(cast_variant)
		if path_step_aoe_effect != null:
			var path_step_target_filter := _resolve_effect_target_filter(skill_def, path_step_aoe_effect)
			for target_unit in _collect_units_in_coords(preview.target_coords):
				if not _is_unit_valid_for_effect(active_unit, target_unit, path_step_target_filter):
					continue
				if preview.target_unit_ids.has(target_unit.unit_id):
					continue
				preview.target_unit_ids.append(target_unit.unit_id)
	preview.allowed = bool(validation.get("allowed", false))
	if preview.allowed:
		preview.log_lines.append("%s 可使用 %s，预计影响 %d 个地格、%d 个单位。" % [
			active_unit.display_name,
			_format_skill_variant_label(skill_def, cast_variant),
			preview.target_coords.size(),
			preview.target_unit_ids.size(),
		])
	else:
		preview.log_lines.append(String(validation.get("message", "地面技能目标无效。")))


func _build_unit_skill_hit_preview(
	active_unit: BattleUnitState,
	target_units: Array,
	skill_def: SkillDef
) -> Dictionary:
	if active_unit == null or skill_def == null or target_units.size() != 1:
		return {}
	var target_unit := target_units[0] as BattleUnitState
	if target_unit == null:
		return {}
	var repeat_attack_effect := _repeat_attack_resolver.get_repeat_attack_effect_def(
		_collect_unit_skill_effect_defs(skill_def, null)
	)
	if repeat_attack_effect == null:
		return {}
	return _hit_resolver.build_repeat_attack_preview(active_unit, target_unit, skill_def, repeat_attack_effect)


func _handle_unit_skill_command(
	active_unit: BattleUnitState,
	command: BattleCommand,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	batch: BattleEventBatch
) -> bool:
	var validation := _validate_unit_skill_targets(active_unit, command, skill_def)
	if not bool(validation.get("allowed", false)):
		return false

	var target_units := validation.get("target_units", []) as Array
	if target_units.is_empty():
		return false

	_consume_skill_costs(active_unit, skill_def)
	_append_changed_unit_id(batch, active_unit.unit_id)
	var applied := false
	var effect_defs := _collect_unit_skill_effect_defs(skill_def, cast_variant)
	var repeat_attack_effect := _repeat_attack_resolver.get_repeat_attack_effect_def(effect_defs)
	for target_unit_variant in target_units:
		var target_unit := target_unit_variant as BattleUnitState
		if target_unit == null:
			continue
		if repeat_attack_effect != null:
			if _repeat_attack_resolver.apply_repeat_attack_skill_result(active_unit, target_unit, skill_def, effect_defs, repeat_attack_effect, batch):
				applied = true
			continue
		if _apply_unit_skill_result(active_unit, target_unit, skill_def, effect_defs, batch):
			applied = true
	return applied


func _should_route_skill_command_to_unit_targeting(skill_def: SkillDef, command: BattleCommand) -> bool:
	if skill_def == null or skill_def.combat_profile == null or command == null:
		return false
	if not _normalize_target_unit_ids(command).is_empty():
		return true
	return skill_def.combat_profile.target_mode == &"unit"


func _validate_unit_skill_targets(active_unit: BattleUnitState, command: BattleCommand, skill_def: SkillDef) -> Dictionary:
	var result := {
		"allowed": false,
		"message": "技能或目标无效。",
		"target_unit_ids": [],
		"target_units": [],
		"preview_coords": [],
	}
	if _state == null or active_unit == null or command == null or skill_def == null or skill_def.combat_profile == null:
		return result

	var target_unit_ids := _normalize_target_unit_ids(command)
	var min_target_count := 1
	var max_target_count := 1
	if _is_multi_unit_skill(skill_def):
		min_target_count = maxi(int(skill_def.combat_profile.min_target_count), 1)
		max_target_count = maxi(int(skill_def.combat_profile.max_target_count), min_target_count)
	if target_unit_ids.is_empty():
		return result
	if target_unit_ids.size() < min_target_count:
		result.message = "至少需要选择 %d 个单位目标。" % min_target_count
		return result
	if target_unit_ids.size() > max_target_count:
		result.message = "最多只能选择 %d 个单位目标。" % max_target_count
		return result
	if not _is_multi_unit_skill(skill_def) and target_unit_ids.size() != 1:
		result.message = "当前技能只允许选择 1 个单位目标。"
		return result
	if StringName(skill_def.combat_profile.selection_order_mode) != &"manual":
		target_unit_ids = _sort_target_unit_ids_for_execution(target_unit_ids)

	var target_units: Array = []
	for target_unit_id in target_unit_ids:
		var target_unit := _state.units.get(target_unit_id) as BattleUnitState
		if target_unit == null or not _can_skill_target_unit(active_unit, target_unit, skill_def):
			result.message = "技能目标超出范围或不满足筛选条件。"
			return result
		target_units.append(target_unit)

	result.allowed = true
	result.message = ""
	result.target_unit_ids = target_unit_ids
	result.target_units = target_units
	var collected_target_coords := _target_collection_service.collect_combat_profile_target_coords(
		_state,
		_grid_service,
		active_unit.coord if active_unit != null else Vector2i(-1, -1),
		skill_def.combat_profile,
		[],
		active_unit,
		target_units
	)
	result.preview_coords = _sort_coords(collected_target_coords.get("target_coords", []))
	return result


func _normalize_target_unit_ids(command: BattleCommand) -> Array[StringName]:
	var target_unit_ids: Array[StringName] = []
	if command == null:
		return target_unit_ids
	var seen_ids: Dictionary = {}
	var single_target_id := ProgressionDataUtils.to_string_name(command.target_unit_id)
	if single_target_id != &"":
		seen_ids[single_target_id] = true
		target_unit_ids.append(single_target_id)
	for target_unit_id_variant in command.target_unit_ids:
		var target_unit_id := ProgressionDataUtils.to_string_name(target_unit_id_variant)
		if target_unit_id == &"" or seen_ids.has(target_unit_id):
			continue
		seen_ids[target_unit_id] = true
		target_unit_ids.append(target_unit_id)
	return target_unit_ids


func _sort_target_unit_ids_for_execution(target_unit_ids: Array[StringName]) -> Array[StringName]:
	var sorted_ids := target_unit_ids.duplicate()
	if _state == null:
		return sorted_ids
	sorted_ids.sort_custom(func(a: StringName, b: StringName) -> bool:
		var unit_a := _state.units.get(a) as BattleUnitState
		var unit_b := _state.units.get(b) as BattleUnitState
		if unit_a == null or unit_b == null:
			return String(a) < String(b)
		return unit_a.coord.y < unit_b.coord.y \
			or (unit_a.coord.y == unit_b.coord.y and (unit_a.coord.x < unit_b.coord.x \
			or (unit_a.coord.x == unit_b.coord.x and String(a) < String(b))))
	)
	return sorted_ids


func _is_multi_unit_skill(skill_def: SkillDef) -> bool:
	return skill_def != null \
		and skill_def.combat_profile != null \
		and StringName(skill_def.combat_profile.target_selection_mode) == &"multi_unit"


func _can_skill_target_unit(active_unit: BattleUnitState, target_unit: BattleUnitState, skill_def: SkillDef) -> bool:
	if active_unit == null or target_unit == null or skill_def == null or skill_def.combat_profile == null:
		return false
	if active_unit.current_ap < int(skill_def.combat_profile.ap_cost):
		return false
	if not _is_unit_valid_for_effect(active_unit, target_unit, skill_def.combat_profile.target_team_filter):
		return false
	active_unit.refresh_footprint()
	target_unit.refresh_footprint()
	return _grid_service.get_distance_between_units(active_unit, target_unit) <= _get_effective_skill_range(active_unit, skill_def)


func _apply_unit_skill_result(
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef],
	batch: BattleEventBatch
) -> bool:
	var result := _damage_resolver.resolve_effects(active_unit, target_unit, effect_defs) if not effect_defs.is_empty() else _damage_resolver.resolve_skill(active_unit, target_unit, skill_def)
	_append_changed_unit_id(batch, target_unit.unit_id)
	_append_changed_unit_coords(batch, target_unit)
	if not bool(result.get("applied", false)):
		return false

	var damage := int(result.get("damage", 0))
	var healing := int(result.get("healing", 0))
	var special_result := _apply_unit_skill_special_effects(active_unit, target_unit, skill_def, effect_defs, batch)
	var moved_steps := int(special_result.get("moved_steps", 0))
	if moved_steps > 0:
		batch.log_lines.append("%s 使用 %s，向更安全位置移动 %d 格。" % [
			active_unit.display_name,
			skill_def.display_name,
			moved_steps,
		])
	if damage > 0:
		batch.log_lines.append("%s 使用 %s，对 %s 造成 %d 伤害。" % [
			active_unit.display_name,
			skill_def.display_name,
			target_unit.display_name,
			damage,
		])
	if healing > 0:
		batch.log_lines.append("%s 使用 %s，为 %s 恢复 %d 点生命。" % [
			active_unit.display_name,
			skill_def.display_name,
			target_unit.display_name,
			healing,
		])
	for status_id in result.get("status_effect_ids", []):
		batch.log_lines.append("%s 获得状态 %s。" % [target_unit.display_name, String(status_id)])
	var terrain_effect_ids: Array = result.get("terrain_effect_ids", [])
	if not terrain_effect_ids.is_empty():
		for terrain_effect_id in terrain_effect_ids:
			var target_cell := _grid_service.get_cell(_state, target_unit.coord)
			if target_cell != null and not target_cell.terrain_effect_ids.has(terrain_effect_id):
				target_cell.terrain_effect_ids.append(terrain_effect_id)
				_append_changed_coord(batch, target_unit.coord)
				batch.log_lines.append("%s 的地格附加效果 %s。" % [
					target_unit.display_name,
					String(terrain_effect_id),
				])
	var height_delta := int(result.get("height_delta", 0))
	if height_delta != 0 and _grid_service.apply_height_delta(_state, target_unit.coord, height_delta):
		_append_changed_coord(batch, target_unit.coord)
		batch.log_lines.append("%s 所在地格高度变化 %d。" % [target_unit.display_name, height_delta])
	if not target_unit.is_alive:
		_clear_defeated_unit(target_unit, batch)
		batch.log_lines.append("%s 被击倒。" % target_unit.display_name)
		_battle_rating_system.record_enemy_defeated_achievement(active_unit, target_unit)
	_battle_rating_system.record_skill_effect_result(active_unit, damage, healing, 1 if not target_unit.is_alive else 0)
	return true


func _apply_unit_skill_special_effects(
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef],
	batch: BattleEventBatch
) -> Dictionary:
	var result := {
		"applied": false,
		"moved_steps": 0,
	}
	if active_unit == null or skill_def == null or effect_defs.is_empty():
		return result

	for effect_def in effect_defs:
		if effect_def == null:
			continue
		if effect_def.effect_type != &"forced_move":
			continue
		var move_target := target_unit if target_unit != null else active_unit
		var moved_steps := _apply_forced_move_effect(move_target, effect_def, batch)
		if moved_steps > 0:
			result["applied"] = true
			result["moved_steps"] = maxi(int(result.get("moved_steps", 0)), moved_steps)
	return result


func _apply_forced_move_effect(
	unit_state: BattleUnitState,
	effect_def: CombatEffectDef,
	batch: BattleEventBatch
) -> int:
	if _state == null or unit_state == null or effect_def == null:
		return 0
	var move_distance := maxi(int(effect_def.forced_move_distance), 0)
	if move_distance <= 0:
		move_distance = maxi(int(effect_def.params.get("distance", 0)), 0)
	if move_distance <= 0:
		return 0

	var mode := effect_def.forced_move_mode
	if mode == &"":
		mode = ProgressionDataUtils.to_string_name(effect_def.params.get("mode", "retreat"))

	var moved_steps := 0
	for _step in range(move_distance):
		var next_coord := _pick_forced_move_coord(unit_state, mode)
		if next_coord == Vector2i(-1, -1) or next_coord == unit_state.coord:
			break
		if not _grid_service.can_traverse(_state, unit_state.coord, next_coord, unit_state):
			break
		var previous_coords := unit_state.occupied_coords.duplicate()
		if not _grid_service.move_unit(_state, unit_state, next_coord):
			break
		moved_steps += 1
		_append_changed_coords(batch, previous_coords)
		_append_changed_unit_coords(batch, unit_state)
		_append_changed_unit_id(batch, unit_state.unit_id)
	return moved_steps


func _pick_forced_move_coord(unit_state: BattleUnitState, mode: StringName) -> Vector2i:
	if _state == null or unit_state == null:
		return Vector2i(-1, -1)
	unit_state.refresh_footprint()
	var best_coord := Vector2i(-1, -1)
	var best_score := -999999
	for direction in [Vector2i.UP, Vector2i.LEFT, Vector2i.RIGHT, Vector2i.DOWN]:
		var candidate_coord: Vector2i = unit_state.coord + direction
		if not _grid_service.can_traverse(_state, unit_state.coord, candidate_coord, unit_state):
			continue
		var candidate_score := _score_forced_move_coord(unit_state, candidate_coord, mode)
		if candidate_score > best_score or (candidate_score == best_score and (best_coord == Vector2i(-1, -1) or candidate_coord.y < best_coord.y or (candidate_coord.y == best_coord.y and candidate_coord.x < best_coord.x))):
			best_score = candidate_score
			best_coord = candidate_coord
	return best_coord


func _score_forced_move_coord(unit_state: BattleUnitState, candidate_coord: Vector2i, mode: StringName) -> int:
	if _state == null or unit_state == null:
		return -999999
	var hostile_units := _collect_hostile_units_for(unit_state)
	var closest_hostile_distance := 0
	if not hostile_units.is_empty():
		closest_hostile_distance = 999999
		for hostile_unit in hostile_units:
			closest_hostile_distance = mini(closest_hostile_distance, _grid_service.get_distance(candidate_coord, hostile_unit.coord))
	var score := closest_hostile_distance * 100
	score -= _grid_service.get_distance(unit_state.coord, candidate_coord) * 10
	score -= candidate_coord.y * 2 + candidate_coord.x
	if mode == &"evasive":
		score += 5
	return score


func _collect_hostile_units_for(unit_state: BattleUnitState) -> Array[BattleUnitState]:
	var hostile_units: Array[BattleUnitState] = []
	if _state == null or unit_state == null:
		return hostile_units
	for other_unit_variant in _state.units.values():
		var other_unit := other_unit_variant as BattleUnitState
		if other_unit == null or other_unit.unit_id == unit_state.unit_id or not other_unit.is_alive:
			continue
		if String(other_unit.faction_id) == String(unit_state.faction_id):
			continue
		hostile_units.append(other_unit)
	return hostile_units


func _collect_unit_skill_effect_defs(skill_def: SkillDef, cast_variant: CombatCastVariantDef) -> Array[CombatEffectDef]:
	var effect_defs: Array[CombatEffectDef] = []
	if skill_def != null and skill_def.combat_profile != null:
		for effect_def in skill_def.combat_profile.effect_defs:
			if effect_def != null:
				effect_defs.append(effect_def)
	if cast_variant != null:
		for effect_def in cast_variant.effect_defs:
			if effect_def != null:
				effect_defs.append(effect_def)
	return effect_defs


func _handle_ground_skill_command(
	active_unit: BattleUnitState,
	command: BattleCommand,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	batch: BattleEventBatch
) -> bool:
	var validation := _validate_ground_skill_command(active_unit, skill_def, cast_variant, command)
	if not bool(validation.get("allowed", false)):
		return false

	_consume_skill_costs(active_unit, skill_def)
	_append_changed_unit_id(batch, active_unit.unit_id)
	if _charge_resolver.is_charge_variant(cast_variant):
		return _charge_resolver.handle_charge_skill_command(active_unit, skill_def, cast_variant, validation, batch)

	var target_coords: Array[Vector2i] = []
	for target_coord_variant in validation.get("target_coords", []):
		if target_coord_variant is Vector2i:
			target_coords.append(target_coord_variant)
	if not _apply_ground_precast_special_effects(active_unit, skill_def, cast_variant, target_coords, batch):
		return false

	var effect_coords := _build_ground_effect_coords(skill_def, target_coords, active_unit.coord if active_unit != null else Vector2i(-1, -1))
	var unit_result := _apply_ground_unit_effects(
		active_unit,
		skill_def,
		_collect_ground_unit_effect_defs(skill_def, cast_variant),
		effect_coords,
		batch
	)
	var terrain_result := _apply_ground_terrain_effects(
		active_unit,
		skill_def,
		_collect_ground_terrain_effect_defs(skill_def, cast_variant),
		effect_coords,
		batch
	)
	var applied := bool(unit_result.get("applied", false)) or bool(terrain_result.get("applied", false))

	if applied:
		batch.log_lines.append("%s 使用 %s，影响了 %d 个地格、%d 个单位。" % [
			active_unit.display_name,
			_format_skill_variant_label(skill_def, cast_variant),
			effect_coords.size(),
			int(unit_result.get("affected_unit_count", 0)),
		])
	return applied


func _apply_ground_precast_special_effects(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	target_coords: Array[Vector2i],
	batch: BattleEventBatch
) -> bool:
	if _get_ground_jump_effect_def(skill_def, cast_variant) == null:
		return true
	return _apply_ground_jump_relocation(active_unit, target_coords, batch)


func _apply_ground_jump_relocation(
	active_unit: BattleUnitState,
	target_coords: Array[Vector2i],
	batch: BattleEventBatch
) -> bool:
	if _state == null or active_unit == null or target_coords.is_empty():
		return false

	var landing_coord := target_coords[0]
	if active_unit.coord == landing_coord:
		return true

	var previous_coords := active_unit.occupied_coords.duplicate()
	if not _grid_service.move_unit_force(_state, active_unit, landing_coord):
		return false

	_append_changed_coords(batch, previous_coords)
	_append_changed_unit_coords(batch, active_unit)
	_append_changed_unit_id(batch, active_unit.unit_id)
	batch.log_lines.append("%s 跳至 (%d, %d)。" % [
		active_unit.display_name,
		landing_coord.x,
		landing_coord.y,
	])
	return true


func _get_ground_jump_effect_def(skill_def: SkillDef, cast_variant: CombatCastVariantDef) -> CombatEffectDef:
	if cast_variant != null:
		for effect_def in cast_variant.effect_defs:
			if _is_ground_jump_effect(effect_def):
				return effect_def
	if skill_def != null and skill_def.combat_profile != null:
		for effect_def in skill_def.combat_profile.effect_defs:
			if _is_ground_jump_effect(effect_def):
				return effect_def
	return null


func _is_ground_jump_effect(effect_def: CombatEffectDef) -> bool:
	return effect_def != null \
		and effect_def.effect_type == &"forced_move" \
		and _get_effect_forced_move_mode(effect_def) == &"jump"


func _get_effect_forced_move_mode(effect_def: CombatEffectDef) -> StringName:
	if effect_def == null:
		return &""
	if effect_def.forced_move_mode != &"":
		return effect_def.forced_move_mode
	return ProgressionDataUtils.to_string_name(effect_def.params.get("mode", ""))


func _build_ground_effect_coords(
	skill_def: SkillDef,
	target_coords: Array,
	source_coord: Vector2i = Vector2i(-1, -1)
) -> Array[Vector2i]:
	var normalized_target_coords: Array[Vector2i] = []
	for target_coord in target_coords:
		normalized_target_coords.append(target_coord)
	if _state == null or skill_def == null or skill_def.combat_profile == null:
		return _sort_coords(normalized_target_coords)
	var collected_target_coords := _target_collection_service.collect_combat_profile_target_coords(
		_state,
		_grid_service,
		source_coord,
		skill_def.combat_profile,
		normalized_target_coords
	)
	if bool(collected_target_coords.get("handled", false)):
		return _sort_coords(collected_target_coords.get("target_coords", []))
	return _sort_coords(normalized_target_coords)

func _collect_ground_unit_effect_defs(skill_def: SkillDef, cast_variant: CombatCastVariantDef) -> Array[CombatEffectDef]:
	var effect_defs: Array[CombatEffectDef] = []
	for effect_def in _collect_ground_effect_defs(skill_def, cast_variant):
		if _is_unit_effect(effect_def):
			effect_defs.append(effect_def)
	return effect_defs


func _collect_ground_terrain_effect_defs(skill_def: SkillDef, cast_variant: CombatCastVariantDef) -> Array[CombatEffectDef]:
	var effect_defs: Array[CombatEffectDef] = []
	for effect_def in _collect_ground_effect_defs(skill_def, cast_variant):
		if _is_terrain_effect(effect_def):
			effect_defs.append(effect_def)
	return effect_defs


func _collect_ground_effect_defs(skill_def: SkillDef, cast_variant: CombatCastVariantDef) -> Array[CombatEffectDef]:
	var effect_defs: Array[CombatEffectDef] = []
	if skill_def != null and skill_def.combat_profile != null:
		for effect_def in skill_def.combat_profile.effect_defs:
			if effect_def != null:
				effect_defs.append(effect_def)
	if cast_variant != null:
		for effect_def in cast_variant.effect_defs:
			if effect_def != null:
				effect_defs.append(effect_def)
	return effect_defs


func _collect_ground_preview_unit_ids(
	source_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef],
	effect_coords: Array[Vector2i]
) -> Array[StringName]:
	var target_unit_ids: Array[StringName] = []
	for target_unit in _collect_units_in_coords(effect_coords):
		for effect_def in effect_defs:
			if _is_unit_valid_for_effect(source_unit, target_unit, _resolve_effect_target_filter(skill_def, effect_def)):
				target_unit_ids.append(target_unit.unit_id)
				break
	return target_unit_ids


func _apply_ground_unit_effects(
	source_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef],
	effect_coords: Array[Vector2i],
	batch: BattleEventBatch
) -> Dictionary:
	var applied := false
	var total_damage := 0
	var total_healing := 0
	var total_kill_count := 0
	var affected_unit_count := 0

	for target_unit in _collect_units_in_coords(effect_coords):
		var applicable_effects: Array[CombatEffectDef] = []
		for effect_def in effect_defs:
			if _is_unit_valid_for_effect(source_unit, target_unit, _resolve_effect_target_filter(skill_def, effect_def)):
				applicable_effects.append(effect_def)
		if applicable_effects.is_empty():
			continue

		var result := _damage_resolver.resolve_effects(source_unit, target_unit, applicable_effects)
		if not bool(result.get("applied", false)):
			continue

		applied = true
		affected_unit_count += 1
		_append_changed_unit_id(batch, source_unit.unit_id if source_unit != null else &"")
		_append_changed_unit_id(batch, target_unit.unit_id)
		_append_changed_unit_coords(batch, target_unit)

		var damage := int(result.get("damage", 0))
		var healing := int(result.get("healing", 0))
		total_damage += damage
		total_healing += healing
		if damage > 0:
			batch.log_lines.append("%s 的 %s 命中 %s，造成 %d 伤害。" % [
				source_unit.display_name if source_unit != null else "地格效果",
				skill_def.display_name if skill_def != null else "技能",
				target_unit.display_name,
				damage,
			])
		if healing > 0:
			batch.log_lines.append("%s 的 %s 为 %s 恢复 %d 点生命。" % [
				source_unit.display_name if source_unit != null else "地格效果",
				skill_def.display_name if skill_def != null else "技能",
				target_unit.display_name,
				healing,
			])
		for status_id in result.get("status_effect_ids", []):
			batch.log_lines.append("%s 获得状态 %s。" % [target_unit.display_name, String(status_id)])

		if not target_unit.is_alive:
			total_kill_count += 1
			_clear_defeated_unit(target_unit, batch)
			batch.log_lines.append("%s 被击倒。" % target_unit.display_name)
			_battle_rating_system.record_enemy_defeated_achievement(source_unit, target_unit)

	if applied and source_unit != null:
		_battle_rating_system.record_skill_effect_result(source_unit, total_damage, total_healing, total_kill_count)
	return {
		"applied": applied,
		"affected_unit_count": affected_unit_count,
		"damage": total_damage,
		"healing": total_healing,
		"kill_count": total_kill_count,
	}


func _apply_ground_terrain_effects(
	source_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef],
	effect_coords: Array[Vector2i],
	batch: BattleEventBatch
) -> Dictionary:
	var applied := false
	var requires_topology_reconcile := false

	for effect_def in effect_defs:
		if effect_def == null:
			continue
		match effect_def.effect_type:
			&"terrain", &"terrain_replace", &"terrain_replace_to", &"height", &"height_delta":
				requires_topology_reconcile = true
				for effect_coord in effect_coords:
					if _apply_ground_cell_effect(effect_coord, effect_def, batch):
						applied = true
			&"terrain_effect":
				if effect_def.duration_tu > 0 and effect_def.tick_interval_tu > 0:
					var field_instance_id := _build_terrain_effect_instance_id(effect_def.terrain_effect_id)
					var applied_coord_count := 0
					for effect_coord in effect_coords:
						if _terrain_effect_system.upsert_timed_terrain_effect(effect_coord, source_unit, skill_def, effect_def, field_instance_id):
							applied = true
							applied_coord_count += 1
							_append_changed_coord(batch, effect_coord)
					if applied_coord_count > 0:
						batch.log_lines.append("%s 在 %d 个地格留下 %s。" % [
							skill_def.display_name if skill_def != null else "技能",
							applied_coord_count,
							_get_terrain_effect_display_name(effect_def),
						])
				elif effect_def.terrain_effect_id != &"":
					var tagged_coord_count := 0
					for effect_coord in effect_coords:
						var cell := _grid_service.get_cell(_state, effect_coord)
						if cell == null or cell.terrain_effect_ids.has(effect_def.terrain_effect_id):
							continue
						cell.terrain_effect_ids.append(effect_def.terrain_effect_id)
						_append_changed_coord(batch, effect_coord)
						tagged_coord_count += 1
						applied = true
					if tagged_coord_count > 0:
						batch.log_lines.append("%s 在 %d 个地格附加效果 %s。" % [
							skill_def.display_name if skill_def != null else "技能",
							tagged_coord_count,
							_get_terrain_effect_display_name(effect_def),
						])
			_:
				pass

	if requires_topology_reconcile and _reconcile_water_topology(effect_coords, batch):
		applied = true
	return {"applied": applied}


func _apply_ground_cell_effect(target_coord: Vector2i, effect_def: CombatEffectDef, batch: BattleEventBatch) -> bool:
	var cell := _grid_service.get_cell(_state, target_coord)
	if cell == null:
		return false

	var cell_applied := false
	var before_terrain := cell.base_terrain
	var before_height := int(cell.current_height)
	var occupant_unit := _state.units.get(cell.occupant_unit_id) as BattleUnitState if cell.occupant_unit_id != &"" else null

	match effect_def.effect_type:
		&"terrain", &"terrain_replace", &"terrain_replace_to":
			if effect_def.terrain_replace_to != &"" and cell.base_terrain != effect_def.terrain_replace_to:
				if _grid_service.set_base_terrain(_state, target_coord, effect_def.terrain_replace_to):
					cell_applied = true
		&"height", &"height_delta":
			if effect_def.height_delta != 0:
				var height_result := _grid_service.apply_height_delta_result(_state, target_coord, int(effect_def.height_delta))
				if bool(height_result.get("changed", false)):
					cell_applied = true
		_:
			pass

	var after_height := int(cell.current_height)
	if before_terrain != cell.base_terrain or before_height != after_height:
		_append_changed_coord(batch, target_coord)
	if before_terrain != cell.base_terrain:
		batch.log_lines.append("地格 (%d, %d) 地形变为 %s。" % [
			target_coord.x,
			target_coord.y,
			_grid_service.get_terrain_display_name(String(cell.base_terrain)),
		])
	if before_height != after_height:
		batch.log_lines.append("地格 (%d, %d) 高度变为 %d。" % [target_coord.x, target_coord.y, after_height])

	if occupant_unit != null and occupant_unit.is_alive and after_height < before_height:
		var fall_layers := before_height - after_height
		var fall_damage := _damage_resolver.resolve_fall_damage(occupant_unit, fall_layers)
		if fall_damage > 0:
			cell_applied = true
			_append_changed_coord(batch, target_coord)
			_append_changed_unit_id(batch, occupant_unit.unit_id)
			batch.log_lines.append("%s 因地格下降 %d 层，受到 %d 点坠落伤害。" % [
				occupant_unit.display_name,
				fall_layers,
				fall_damage,
			])
			if not occupant_unit.is_alive:
				_clear_defeated_unit(occupant_unit, batch)
				batch.log_lines.append("%s 被击倒。" % occupant_unit.display_name)

	return cell_applied


func _reconcile_water_topology(effect_coords: Array[Vector2i], batch: BattleEventBatch) -> bool:
	if _state == null or _state.map_size == Vector2i.ZERO or effect_coords.is_empty():
		return false

	var changes: Array[Dictionary] = _terrain_topology_service.reclassify_water_terrain_near_coords(
		_state.cells,
		_state.map_size,
		effect_coords
	)
	var applied := false
	for change in changes:
		var coord: Vector2i = change.get("coord", Vector2i.ZERO)
		var cell: BattleCellState = _grid_service.get_cell(_state, coord)
		if cell == null:
			continue
		var before_terrain: StringName = cell.base_terrain
		var before_flow_direction: Vector2i = cell.flow_direction
		var after_terrain: StringName = change.get("after_terrain", before_terrain)
		var after_flow_direction: Vector2i = change.get("after_flow_direction", before_flow_direction)
		if before_terrain != after_terrain:
			_grid_service.set_base_terrain(_state, coord, after_terrain)
			cell = _grid_service.get_cell(_state, coord)
			if cell == null:
				continue
		if cell.flow_direction != after_flow_direction:
			cell.flow_direction = after_flow_direction
			_grid_service.recalculate_cell(cell)
			_grid_service.sync_column_from_surface_cell(_state, coord)
		if before_terrain != cell.base_terrain or before_flow_direction != cell.flow_direction:
			applied = true
			_append_changed_coord(batch, coord)
		if before_terrain != cell.base_terrain:
			batch.log_lines.append("相邻水域在 (%d, %d) 重分类为 %s。" % [
				coord.x,
				coord.y,
				_grid_service.get_terrain_display_name(String(cell.base_terrain)),
			])
	return applied


func _collect_units_in_coords(effect_coords: Array[Vector2i]) -> Array[BattleUnitState]:
	var units: Array[BattleUnitState] = []
	var seen_unit_ids: Dictionary = {}
	for effect_coord in effect_coords:
		var target_unit := _grid_service.get_unit_at_coord(_state, effect_coord)
		if target_unit == null or not target_unit.is_alive or seen_unit_ids.has(target_unit.unit_id):
			continue
		seen_unit_ids[target_unit.unit_id] = true
		units.append(target_unit)
	return units


func _is_unit_effect(effect_def: CombatEffectDef) -> bool:
	if effect_def == null:
		return false
	return effect_def.effect_type == &"damage" \
		or effect_def.effect_type == &"heal" \
		or effect_def.effect_type == &"status" \
		or effect_def.effect_type == &"apply_status"


func _is_terrain_effect(effect_def: CombatEffectDef) -> bool:
	if effect_def == null:
		return false
	return effect_def.effect_type == &"terrain" \
		or effect_def.effect_type == &"terrain_replace" \
		or effect_def.effect_type == &"terrain_replace_to" \
		or effect_def.effect_type == &"height" \
		or effect_def.effect_type == &"height_delta" \
		or effect_def.effect_type == &"terrain_effect"


func _resolve_effect_target_filter(skill_def: SkillDef, effect_def: CombatEffectDef) -> StringName:
	if effect_def != null and effect_def.effect_target_team_filter != &"":
		return effect_def.effect_target_team_filter
	if skill_def != null and skill_def.combat_profile != null:
		return skill_def.combat_profile.target_team_filter
	return &"any"


func _is_unit_valid_for_effect(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	target_team_filter: StringName
) -> bool:
	if target_unit == null or not target_unit.is_alive:
		return false
	match target_team_filter:
		&"", &"any":
			return true
		&"self":
			return source_unit != null and target_unit.unit_id == source_unit.unit_id
		&"ally", &"friendly":
			return source_unit != null and target_unit.faction_id == source_unit.faction_id
		&"enemy", &"hostile":
			return source_unit != null and target_unit.faction_id != source_unit.faction_id
		_:
			return true


func _build_terrain_effect_instance_id(effect_id: StringName) -> StringName:
	_terrain_effect_nonce += 1
	return StringName("%s_%d_%d" % [
		String(effect_id),
		int(_state.timeline.current_tu) if _state != null and _state.timeline != null else 0,
		_terrain_effect_nonce,
	])


func _get_terrain_effect_display_name(effect_def: CombatEffectDef) -> String:
	if effect_def != null and effect_def.params.has("display_name"):
		return String(effect_def.params.get("display_name", ""))
	return String(effect_def.terrain_effect_id) if effect_def != null else "地格效果"


func _append_batch_log(batch: BattleEventBatch, message: String) -> void:
	if batch == null or message.is_empty():
		return
	batch.log_lines.append(message)
	if _state != null:
		_state.log_entries.append(message)


func _grant_skill_mastery_if_needed(active_unit: BattleUnitState, skill_id: StringName, batch: BattleEventBatch) -> void:
	if active_unit.source_member_id == &"" or _character_gateway == null:
		return

	_battle_rating_system.record_skill_success(active_unit, skill_id)
	_character_gateway.record_achievement_event(active_unit.source_member_id, &"skill_used", 1, skill_id)
	var delta = _character_gateway.grant_battle_mastery(active_unit.source_member_id, skill_id, 5)
	batch.progression_deltas.append(delta)
	_unit_factory.refresh_battle_unit(active_unit)
	if delta.needs_promotion_modal:
		_state.modal_state = &"promotion_choice"
		_state.timeline.frozen = true
		batch.modal_requested = true
		batch.log_lines.append("%s 触发职业晋升选择。" % active_unit.display_name)


func _resolve_ground_cast_variant(
	skill_def: SkillDef,
	active_unit: BattleUnitState,
	command: BattleCommand
) -> CombatCastVariantDef:
	if skill_def == null or skill_def.combat_profile == null:
		return null
	if skill_def.combat_profile.cast_variants.is_empty():
		return _build_implicit_ground_cast_variant(skill_def) if skill_def.combat_profile.target_mode == &"ground" and command.skill_variant_id == &"" else null

	var skill_level := _get_unit_skill_level(active_unit, skill_def.skill_id)
	var unlocked_variants := skill_def.combat_profile.get_unlocked_cast_variants(skill_level)
	if unlocked_variants.is_empty():
		return null
	if command.skill_variant_id == &"":
		return unlocked_variants[0] if unlocked_variants.size() == 1 else null

	for cast_variant in unlocked_variants:
		if cast_variant != null and cast_variant.variant_id == command.skill_variant_id and cast_variant.target_mode == &"ground":
			return cast_variant
	return null


func _build_implicit_ground_cast_variant(skill_def: SkillDef) -> CombatCastVariantDef:
	var cast_variant := CombatCastVariantDef.new()
	cast_variant.variant_id = &""
	cast_variant.display_name = ""
	cast_variant.target_mode = &"ground"
	cast_variant.footprint_pattern = &"single"
	cast_variant.required_coord_count = 1
	cast_variant.effect_defs = skill_def.combat_profile.effect_defs.duplicate()
	return cast_variant


func _validate_ground_skill_command(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	command: BattleCommand
) -> Dictionary:
	var normalized_coords := _normalize_target_coords(command)
	var result := {
		"allowed": false,
		"message": "地面技能目标无效。",
		"target_coords": normalized_coords,
	}
	if _state == null or active_unit == null or skill_def == null or skill_def.combat_profile == null or cast_variant == null:
		return result
	if cast_variant.target_mode != &"ground":
		result.message = "该技能形态不是地面施法。"
		return result
	var block_reason := _get_skill_cast_block_reason(active_unit, skill_def)
	if not block_reason.is_empty():
		result.message = block_reason
		return result
	if normalized_coords.size() != int(cast_variant.required_coord_count):
		result.message = "该技能形态需要选择 %d 个地格。" % int(cast_variant.required_coord_count)
		return result
	if _charge_resolver.is_charge_variant(cast_variant):
		return _charge_resolver.validate_charge_command(active_unit, cast_variant, normalized_coords, result)

	var seen_coords: Dictionary = {}
	for target_coord in normalized_coords:
		var coord: Vector2i = target_coord
		if seen_coords.has(coord):
			result.message = "同一地格不能重复选择。"
			return result
		seen_coords[coord] = true
		if not _grid_service.is_inside(_state, coord):
			result.message = "存在超出战场范围的目标地格。"
			return result
		if _grid_service.get_distance_from_unit_to_coord(active_unit, coord) > _get_effective_skill_range(active_unit, skill_def):
			result.message = "目标地格超出技能施放距离。"
			return result
		var cell := _grid_service.get_cell(_state, coord)
		if cell == null:
			result.message = "目标地格数据不可用。"
			return result
		if not cast_variant.allowed_base_terrains.is_empty():
			var normalized_allowed := false
			var normalized_cell_terrain := BattleTerrainRules.normalize_terrain_id(cell.base_terrain)
			for allowed_terrain in cast_variant.allowed_base_terrains:
				if BattleTerrainRules.normalize_terrain_id(allowed_terrain) == normalized_cell_terrain:
					normalized_allowed = true
					break
			if not normalized_allowed:
				result.message = "目标地格地形不符合该技能形态的要求。"
				return result

	if not _validate_target_coords_shape(cast_variant.footprint_pattern, normalized_coords):
		result.message = "目标地格排布不符合该技能形态。"
		return result

	var sorted_target_coords := _sort_coords(normalized_coords)
	var special_validation_message := _get_ground_special_effect_validation_message(
		active_unit,
		skill_def,
		cast_variant,
		sorted_target_coords
	)
	if not special_validation_message.is_empty():
		result.message = special_validation_message
		return result

	result.target_coords = sorted_target_coords
	result.allowed = true
	result.message = "可施放。"
	return result


func _get_ground_special_effect_validation_message(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	target_coords: Array[Vector2i]
) -> String:
	if _get_ground_jump_effect_def(skill_def, cast_variant) == null:
		return ""
	if active_unit == null or _state == null:
		return "跳跃落点无效。"
	if _is_movement_blocked(active_unit):
		return "当前状态下无法跳跃移动。"
	if target_coords.is_empty():
		return "跳跃落点无效。"

	var landing_coord := target_coords[0]
	if not _grid_service.can_place_unit(_state, active_unit, landing_coord, true):
		return "目标地格无法作为跳跃落点。"
	return ""


func _validate_target_coords_shape(footprint_pattern: StringName, target_coords: Array[Vector2i]) -> bool:
	match footprint_pattern:
		&"single":
			return target_coords.size() == 1
		&"line2":
			if target_coords.size() != 2:
				return false
			var first := target_coords[0]
			var second := target_coords[1]
			return (first.x == second.x and absi(first.y - second.y) == 1) \
				or (first.y == second.y and absi(first.x - second.x) == 1)
		&"square2":
			if target_coords.size() != 4:
				return false
			var min_x := target_coords[0].x
			var max_x := target_coords[0].x
			var min_y := target_coords[0].y
			var max_y := target_coords[0].y
			var coord_set: Dictionary = {}
			for coord in target_coords:
				min_x = mini(min_x, coord.x)
				max_x = maxi(max_x, coord.x)
				min_y = mini(min_y, coord.y)
				max_y = maxi(max_y, coord.y)
				coord_set[coord] = true
			if max_x - min_x != 1 or max_y - min_y != 1:
				return false
			for x in range(min_x, max_x + 1):
				for y in range(min_y, max_y + 1):
					if not coord_set.has(Vector2i(x, y)):
						return false
			return true
		&"unordered":
			return not target_coords.is_empty()
		_:
			return false


func _normalize_target_coords(command: BattleCommand) -> Array[Vector2i]:
	var coords: Array[Vector2i] = []
	if command == null:
		return coords
	for target_coord in command.target_coords:
		coords.append(target_coord)
	if coords.is_empty() and command.target_coord != Vector2i(-1, -1):
		coords.append(command.target_coord)
	return coords


func _append_changed_coord(batch: BattleEventBatch, coord: Vector2i) -> void:
	if batch == null:
		return
	if batch.changed_coords.has(coord):
		return
	batch.changed_coords.append(coord)


func _append_changed_coords(batch: BattleEventBatch, coords: Array[Vector2i]) -> void:
	for coord in coords:
		_append_changed_coord(batch, coord)


func _append_changed_unit_id(batch: BattleEventBatch, unit_id: StringName) -> void:
	if batch == null or unit_id == &"":
		return
	if batch.changed_unit_ids.has(unit_id):
		return
	batch.changed_unit_ids.append(unit_id)


func _append_changed_unit_coords(batch: BattleEventBatch, unit_state: BattleUnitState) -> void:
	if unit_state == null:
		return
	unit_state.refresh_footprint()
	_append_changed_coords(batch, unit_state.occupied_coords)


func _clear_defeated_unit(unit_state: BattleUnitState, batch: BattleEventBatch = null) -> void:
	if _state == null or unit_state == null:
		return
	var previous_coords := unit_state.occupied_coords.duplicate()
	_grid_service.clear_unit_occupancy(_state, unit_state)
	_append_changed_coords(batch, previous_coords)
	_append_changed_unit_id(batch, unit_state.unit_id)


func _merge_batch(target_batch: BattleEventBatch, source_batch: BattleEventBatch) -> void:
	if target_batch == null or source_batch == null:
		return
	for coord in source_batch.changed_coords:
		_append_changed_coord(target_batch, coord)
	for unit_id in source_batch.changed_unit_ids:
		_append_changed_unit_id(target_batch, unit_id)
	for log_line in source_batch.log_lines:
		target_batch.log_lines.append(log_line)


func _sort_coords(target_coords: Variant) -> Array[Vector2i]:
	var sorted_coords: Array[Vector2i] = []
	if target_coords is Array:
		for coord_variant in target_coords:
			if coord_variant is Vector2i:
				sorted_coords.append(coord_variant)
	sorted_coords.sort_custom(func(a: Vector2i, b: Vector2i) -> bool:
		return a.y < b.y or (a.y == b.y and a.x < b.x)
	)
	return sorted_coords


func _collect_dict_vector2i_keys(values: Dictionary) -> Array[Vector2i]:
	var coords: Array[Vector2i] = []
	for coord_variant in values.keys():
		if coord_variant is Vector2i:
			coords.append(coord_variant)
	return coords


func _build_reachable_move_buckets(max_move_points: int) -> Array:
	var bucket_count := maxi(max_move_points, 0) + 1
	var buckets: Array = []
	buckets.resize(bucket_count)
	for bucket_index in range(bucket_count):
		buckets[bucket_index] = []
	return buckets


func _build_reachable_move_state_key(coord: Vector2i, has_quickstep_bonus: bool) -> String:
	return "%d:%d:%d" % [coord.x, coord.y, 1 if has_quickstep_bonus else 0]


func _get_unit_skill_level(unit_state: BattleUnitState, skill_id: StringName) -> int:
	if unit_state == null or skill_id == &"":
		return 0
	if unit_state.known_skill_level_map.has(skill_id):
		return int(unit_state.known_skill_level_map.get(skill_id, 0))
	return 1 if unit_state.known_active_skill_ids.has(skill_id) else 0


func _format_skill_variant_label(skill_def: SkillDef, cast_variant: CombatCastVariantDef) -> String:
	if skill_def == null:
		return ""
	if cast_variant == null or cast_variant.display_name.is_empty():
		return skill_def.display_name
	return "%s·%s" % [skill_def.display_name, cast_variant.display_name]


func _check_battle_end(batch: BattleEventBatch) -> bool:
	var living_allies := _count_living_units(_state.ally_unit_ids)
	var living_enemies := _count_living_units(_state.enemy_unit_ids)
	if living_allies > 0 and living_enemies > 0:
		return false

	_state.phase = &"battle_ended"
	_state.winner_faction_id = &"player" if living_allies > 0 else &"hostile"
	_state.active_unit_id = &""
	_state.timeline.ready_unit_ids.clear()
	_state.timeline.frozen = true
	_battle_rating_system.record_battle_won_achievements()
	_battle_rating_system.finalize_battle_rating_rewards()
	if _battle_resolution_result == null:
		_battle_resolution_result = _build_battle_resolution_result()
	_battle_resolution_result_consumed = false
	batch.phase_changed = true
	batch.battle_ended = true
	batch.log_lines.append("战斗结束，胜利方：%s。" % String(_state.winner_faction_id))
	return true


func _count_living_units(unit_ids: Array[StringName]) -> int:
	var count := 0
	for unit_id in unit_ids:
		var unit_state := _state.units.get(unit_id) as BattleUnitState
		if unit_state != null and unit_state.is_alive:
			count += 1
	return count


func _end_active_turn(batch: BattleEventBatch) -> void:
	var active_unit := _state.units.get(_state.active_unit_id) as BattleUnitState
	if active_unit != null and active_unit.control_mode != &"manual":
		_cleanup_ai_turn(active_unit)
	_state.phase = &"timeline_running"
	_state.active_unit_id = &""
	batch.phase_changed = true


func _activate_next_ready_unit(batch: BattleEventBatch) -> void:
	while not _state.timeline.ready_unit_ids.is_empty():
		var next_unit_id: StringName = _state.timeline.ready_unit_ids.pop_front()
		var unit_state := _state.units.get(next_unit_id) as BattleUnitState
		if unit_state == null or not unit_state.is_alive:
			continue
		_state.phase = &"unit_acting"
		_state.active_unit_id = next_unit_id
		_advance_unit_turn_timers(unit_state, batch)
		var action_points := 1
		if unit_state.attribute_snapshot != null:
			action_points = maxi(unit_state.attribute_snapshot.get_value(&"action_points"), 1)
		unit_state.current_ap = action_points
		_apply_turn_start_statuses(unit_state, batch)
		if not unit_state.is_alive:
			_clear_defeated_unit(unit_state, batch)
			_state.phase = &"timeline_running"
			_state.active_unit_id = &""
			batch.phase_changed = true
			batch.changed_unit_ids.append(next_unit_id)
			batch.log_lines.append("%s 因持续效果倒下。" % unit_state.display_name)
			_state.log_entries.append(batch.log_lines[-1])
			if _check_battle_end(batch):
				return
			continue
		if unit_state.control_mode != &"manual":
			_prepare_ai_turn(unit_state)
		batch.phase_changed = true
		batch.changed_unit_ids.append(next_unit_id)
		batch.log_lines.append("轮到 %s 行动。" % unit_state.display_name)
		_state.log_entries.append(batch.log_lines[-1])
		return


func _get_skill_cast_block_reason(active_unit: BattleUnitState, skill_def: SkillDef) -> String:
	if active_unit == null or skill_def == null or skill_def.combat_profile == null:
		return "技能或目标无效。"
	var combat_profile = skill_def.combat_profile
	var cooldown := int(active_unit.cooldowns.get(skill_def.skill_id, 0))
	if cooldown > 0:
		return "%s 仍在冷却中（%d）。" % [skill_def.display_name, cooldown]
	if active_unit.current_ap < int(combat_profile.ap_cost):
		return "行动点不足，无法施放该技能。"
	if active_unit.current_mp < int(combat_profile.mp_cost):
		return "法力不足，无法施放该技能。"
	if active_unit.current_stamina < int(combat_profile.stamina_cost):
		return "体力不足，无法施放该技能。"
	if active_unit.current_aura < int(combat_profile.aura_cost):
		return "斗气不足，无法施放该技能。"
	return ""


func _consume_skill_costs(active_unit: BattleUnitState, skill_def: SkillDef) -> void:
	if active_unit == null or skill_def == null or skill_def.combat_profile == null:
		return
	var combat_profile = skill_def.combat_profile
	active_unit.current_ap = maxi(active_unit.current_ap - int(combat_profile.ap_cost), 0)
	active_unit.current_mp = maxi(active_unit.current_mp - int(combat_profile.mp_cost), 0)
	active_unit.current_stamina = maxi(active_unit.current_stamina - int(combat_profile.stamina_cost), 0)
	active_unit.current_aura = maxi(active_unit.current_aura - int(combat_profile.aura_cost), 0)
	var cooldown := maxi(int(combat_profile.cooldown_tu), 0)
	if cooldown > 0:
		active_unit.cooldowns[skill_def.skill_id] = cooldown


func _ensure_unit_turn_anchor(unit_state: BattleUnitState) -> void:
	if unit_state == null or unit_state.last_turn_tu >= 0:
		return
	unit_state.last_turn_tu = int(_state.timeline.current_tu) if _state != null and _state.timeline != null else 0


func _advance_unit_cooldowns(unit_state: BattleUnitState, cooldown_delta: int) -> bool:
	if unit_state == null or cooldown_delta <= 0:
		return false
	var previous_cooldowns: Dictionary = unit_state.cooldowns.duplicate(true)
	var retained_cooldowns: Dictionary = {}
	for skill_id_variant in previous_cooldowns.keys():
		var skill_id := ProgressionDataUtils.to_string_name(skill_id_variant)
		var previous_remaining := int(previous_cooldowns.get(skill_id_variant, 0))
		var remaining := maxi(previous_remaining - cooldown_delta, 0)
		if remaining > 0:
			retained_cooldowns[skill_id] = remaining
	unit_state.cooldowns = retained_cooldowns
	return previous_cooldowns != retained_cooldowns


func _consume_turn_cooldown_delta(unit_state: BattleUnitState) -> bool:
	if unit_state == null:
		return false
	var current_tu := int(_state.timeline.current_tu) if _state != null and _state.timeline != null else 0
	if unit_state.last_turn_tu < 0:
		unit_state.last_turn_tu = current_tu
		return false
	var elapsed_tu := maxi(current_tu - unit_state.last_turn_tu, 0)
	unit_state.last_turn_tu = current_tu
	var cooldown_delta := elapsed_tu if elapsed_tu > 0 else 1
	return _advance_unit_cooldowns(unit_state, cooldown_delta)


func _advance_unit_turn_timers(unit_state: BattleUnitState, batch: BattleEventBatch) -> void:
	if unit_state == null:
		return
	var changed := _consume_turn_cooldown_delta(unit_state)
	for status_id_variant in unit_state.status_effects.keys():
		var status_id := ProgressionDataUtils.to_string_name(status_id_variant)
		if unit_state.get_status_effect(status_id) == null:
			changed = true

	if changed:
		_append_changed_unit_id(batch, unit_state.unit_id)


func _apply_turn_start_statuses(unit_state: BattleUnitState, batch: BattleEventBatch) -> void:
	if unit_state == null:
		return
	var changed := false
	for status_id_str in ProgressionDataUtils.sorted_string_keys(unit_state.status_effects):
		var status_entry = unit_state.get_status_effect(StringName(status_id_str))
		if status_entry == null:
			continue
		var ap_penalty := BattleStatusSemanticTable.get_turn_start_ap_penalty(status_entry)
		if ap_penalty > 0:
			var previous_ap: int = unit_state.current_ap
			unit_state.current_ap = maxi(unit_state.current_ap - ap_penalty, 0)
			if unit_state.current_ap != previous_ap:
				changed = true
				batch.log_lines.append("%s 受到踉跄影响，本回合少 %d 点行动点。" % [unit_state.display_name, previous_ap - unit_state.current_ap])
		var tick_damage := BattleStatusSemanticTable.get_turn_start_damage(status_entry)
		if tick_damage > 0 and unit_state.is_alive:
			var previous_hp := unit_state.current_hp
			unit_state.current_hp = maxi(unit_state.current_hp - tick_damage, 0)
			unit_state.is_alive = unit_state.current_hp > 0
			if unit_state.current_hp != previous_hp:
				changed = true
				batch.log_lines.append("%s 受到灼烧影响，损失 %d 点生命。" % [unit_state.display_name, previous_hp - unit_state.current_hp])
	if changed:
		_append_changed_unit_id(batch, unit_state.unit_id)


func _advance_unit_status_durations(unit_state: BattleUnitState, elapsed_tu: int) -> bool:
	if unit_state == null:
		return false
	var changed := false
	var expired_status_ids: Array[StringName] = []
	for status_id_str in ProgressionDataUtils.sorted_string_keys(unit_state.status_effects):
		var status_id := StringName(status_id_str)
		var status_entry = unit_state.get_status_effect(status_id)
		if status_entry == null:
			expired_status_ids.append(status_id)
			changed = true
			continue
		var duration_result: Dictionary = BattleStatusSemanticTable.advance_timeline_duration(status_entry, elapsed_tu)
		if bool(duration_result.get("expired", false)):
			expired_status_ids.append(status_id)
			changed = true
			continue
		if bool(duration_result.get("changed", false)):
			unit_state.set_status_effect(status_entry)
			changed = true
	for expired_status_id in expired_status_ids:
		unit_state.erase_status_effect(expired_status_id)
	return changed


func _get_effective_skill_range(active_unit: BattleUnitState, skill_def: SkillDef) -> int:
	if skill_def == null or skill_def.combat_profile == null:
		return 0
	var skill_range := int(skill_def.combat_profile.range_value)
	if _has_status(active_unit, STATUS_ARCHER_RANGE_UP):
		skill_range += 1
	return skill_range


func _is_movement_blocked(unit_state: BattleUnitState) -> bool:
	return _has_status(unit_state, STATUS_PINNED) or _has_status(unit_state, STATUS_ROOTED) or _has_status(unit_state, STATUS_TENDON_CUT)


func _has_status(unit_state: BattleUnitState, status_id: StringName) -> bool:
	if unit_state == null or status_id == &"":
		return false
	return unit_state.has_status_effect(status_id)


func _consume_status_if_present(unit_state: BattleUnitState, status_id: StringName, batch: BattleEventBatch = null) -> void:
	if unit_state == null or status_id == &"" or not unit_state.has_status_effect(status_id):
		return
	unit_state.erase_status_effect(status_id)
	if batch != null:
		_append_changed_unit_id(batch, unit_state.unit_id)


func _prepare_ai_turn(unit_state: BattleUnitState) -> void:
	if unit_state == null:
		return
	unit_state.ai_blackboard["turn_started_tu"] = int(_state.timeline.current_tu) if _state != null and _state.timeline != null else 0
	unit_state.ai_blackboard["turn_decision_count"] = 0
	var brain = _enemy_ai_brains.get(unit_state.ai_brain_id)
	if brain != null and not brain.has_state(unit_state.ai_state_id):
		unit_state.ai_state_id = brain.default_state_id


func _cleanup_ai_turn(unit_state: BattleUnitState) -> void:
	if unit_state == null:
		return
	unit_state.ai_blackboard.erase("turn_started_tu")
	unit_state.ai_blackboard.erase("turn_decision_count")


func _find_unit_by_member_id(member_id: StringName) -> BattleUnitState:
	for unit_state_data in _state.units.values():
		var unit_state := unit_state_data as BattleUnitState
		if unit_state != null and unit_state.source_member_id == member_id:
			return unit_state
	return null


func _get_living_units_in_order() -> Array[StringName]:
	var ordered_ids: Array[StringName] = []
	for unit_id_str in ProgressionDataUtils.sorted_string_keys(_state.units):
		ordered_ids.append(StringName(unit_id_str))
	return ordered_ids


func _new_batch() -> BattleEventBatch:
	return BATTLE_EVENT_BATCH_SCRIPT.new()


func _build_battle_resolution_result():
	var resolution_result = BATTLE_RESOLUTION_RESULT_SCRIPT.new()
	if _state == null:
		return resolution_result
	resolution_result.battle_id = _state.battle_id
	resolution_result.seed = int(_state.seed)
	resolution_result.world_coord = _state.world_coord
	resolution_result.encounter_anchor_id = _state.encounter_anchor_id
	resolution_result.terrain_profile_id = _state.terrain_profile_id
	resolution_result.winner_faction_id = _state.winner_faction_id
	resolution_result.encounter_resolution = _resolve_encounter_resolution()
	resolution_result.set_loot_entries(_active_loot_entries if resolution_result.winner_faction_id == &"player" else [])
	resolution_result.set_pending_character_rewards(_pending_post_battle_character_rewards)
	return resolution_result


func _resolve_encounter_resolution() -> StringName:
	if _state == null:
		return &""
	if _state.winner_faction_id == &"player":
		return &"player_victory"
	if _state.winner_faction_id == &"hostile":
		return &"hostile_victory"
	return &"resolved"


func _roll_hit_rate(hit_rate_percent: int) -> Dictionary:
	return _hit_resolver.roll_hit_rate(_state, hit_rate_percent)
