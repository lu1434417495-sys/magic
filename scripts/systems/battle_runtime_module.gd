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
const BATTLE_AI_SERVICE_SCRIPT = preload("res://scripts/systems/battle_ai_service.gd")
const BATTLE_AI_DECISION_SCRIPT = preload("res://scripts/systems/battle_ai_decision.gd")
const BATTLE_AI_CONTEXT_SCRIPT = preload("res://scripts/systems/battle_ai_context.gd")
const ENCOUNTER_ROSTER_BUILDER_SCRIPT = preload("res://scripts/systems/encounter_roster_builder.gd")
const BattleState = preload("res://scripts/systems/battle_state.gd")
const BattleTimelineState = preload("res://scripts/systems/battle_timeline_state.gd")
const BattleEventBatch = preload("res://scripts/systems/battle_event_batch.gd")
const BattlePreview = preload("res://scripts/systems/battle_preview.gd")
const BattleCellState = preload("res://scripts/systems/battle_cell_state.gd")
const BattleTerrainEffectState = preload("res://scripts/systems/battle_terrain_effect_state.gd")
const BattleGridService = preload("res://scripts/systems/battle_grid_service.gd")
const BattleDamageResolver = preload("res://scripts/systems/battle_damage_resolver.gd")
const BattleAiService = preload("res://scripts/systems/battle_ai_service.gd")
const BattleAiDecision = preload("res://scripts/systems/battle_ai_decision.gd")
const BattleAiContext = preload("res://scripts/systems/battle_ai_context.gd")
const BattleCommand = preload("res://scripts/systems/battle_command.gd")
const BattleUnitState = preload("res://scripts/systems/battle_unit_state.gd")
const CombatCastVariantDef = preload("res://scripts/player/progression/combat_cast_variant_def.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const CHARGE_EFFECT_TYPE: StringName = &"charge"
const TRAP_EFFECT_PREFIX := "trap_"
const BATTLE_RATING_SOURCE_TYPE: StringName = &"battle_rating"
const TERRAIN_EFFECT_DAMAGE: StringName = &"damage"
const TERRAIN_EFFECT_STATUS: StringName = &"status"
const STACK_BEHAVIOR_REFRESH: StringName = &"refresh"
const STACK_BEHAVIOR_STACK: StringName = &"stack"
const STACK_BEHAVIOR_IGNORE_EXISTING: StringName = &"ignore_existing"
const MIN_BATTLE_SURFACE_HEIGHT := 4
const STATUS_PINNED: StringName = &"pinned"
const STATUS_ROOTED: StringName = &"rooted"
const STATUS_TENDON_CUT: StringName = &"tendon_cut"
const STATUS_ARCHER_RANGE_UP: StringName = &"archer_range_up"
const STATUS_ARCHER_QUICKSTEP: StringName = &"archer_quickstep"

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
## 字段说明：记录自动决策服务，会参与运行时状态流转、系统协作和存档恢复。
var _ai_service: BattleAiService = BATTLE_AI_SERVICE_SCRIPT.new()
## 字段说明：缓存战斗评分统计字典，集中保存可按键查询的运行时数据。
var _battle_rating_stats: Dictionary = {}
## 字段说明：保存待处理后置战斗熟练度奖励列表，便于顺序遍历、批量展示、批量运算和整体重建。
var _pending_post_battle_mastery_rewards: Array = []
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
	_ai_service.setup(_enemy_ai_brains)


func start_battle(
	encounter_anchor,
	seed: int,
	context: Dictionary = {}
) -> BattleState:
	var party_state = _character_gateway.get_party_state() if _character_gateway != null and _character_gateway.has_method("get_party_state") else null
	var ally_units: Array = []
	if _character_gateway != null and _character_gateway.has_method("build_battle_party") and party_state != null:
		ally_units = _character_gateway.build_battle_party(party_state.active_member_ids)
	if ally_units.is_empty():
		ally_units = _build_ally_units(party_state, context)

	var enemy_units: Array = []
	var enemy_build_context := context.duplicate(true)
	enemy_build_context["skill_defs"] = _skill_defs
	enemy_build_context["enemy_templates"] = _enemy_templates
	enemy_build_context["enemy_ai_brains"] = _enemy_ai_brains
	if _encounter_builder != null and _encounter_builder.has_method("build_enemy_units"):
		enemy_units = _encounter_builder.build_enemy_units(encounter_anchor, enemy_build_context)
	if enemy_units.is_empty():
		enemy_units = _build_enemy_units(encounter_anchor, enemy_build_context)
	var terrain_data := _build_terrain_data(encounter_anchor, seed, context)

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
	_state.log_entries = ["战斗开始：%s" % encounter_anchor.display_name]
	_initialize_battle_rating_stats()
	_terrain_effect_nonce = 0
	return _state


func advance(delta_seconds: float) -> BattleEventBatch:
	var batch := _new_batch()
	if _state == null or _state.phase == &"battle_ended":
		return batch
	if _state.modal_state != &"":
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
		_state.timeline.current_tu += int(round(delta_seconds * float(_state.timeline.units_per_second)))
		for unit_id in _get_living_units_in_order():
			var unit_state := _state.units.get(unit_id) as BattleUnitState
			if unit_state == null or not unit_state.is_alive:
				continue
			var speed := 1
			if unit_state.attribute_snapshot != null:
				speed = maxi(unit_state.attribute_snapshot.get_value(&"speed"), 1)
			unit_state.action_progress += maxi(int(round(delta_seconds * float(speed) * 12.0)), 1)
			while unit_state.action_progress >= _state.timeline.action_threshold:
				unit_state.action_progress -= _state.timeline.action_threshold
				if not _state.timeline.ready_unit_ids.has(unit_id):
					_state.timeline.ready_unit_ids.append(unit_id)
		_process_timed_terrain_effects(batch)
		if _check_battle_end(batch):
			return batch

	if _state.phase == &"timeline_running":
		_activate_next_ready_unit(batch)

	return batch


func preview_command(command: BattleCommand) -> BattlePreview:
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
			var move_result := _grid_service.evaluate_move(_state, active_unit.coord, command.target_coord, active_unit)
			if bool(move_result.get("allowed", false)):
				var move_cost := _get_move_cost_for_unit_target(active_unit, command.target_coord)
				preview.allowed = active_unit.current_ap >= move_cost
				if preview.allowed:
					preview.log_lines.append("移动可执行，消耗 %d 点行动点。" % move_cost)
					for target_coord in _grid_service.get_unit_target_coords(active_unit, command.target_coord):
						preview.target_coords.append(target_coord)
				else:
					preview.log_lines.append("行动点不足，无法移动。")
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
	var batch := _new_batch()
	if _state == null or _character_gateway == null:
		return batch
	if _character_gateway.has_method("promote_profession"):
		var delta = _character_gateway.promote_profession(member_id, profession_id, selection)
		batch.progression_deltas.append(delta)
	var unit_state := _find_unit_by_member_id(member_id)
	if unit_state != null and _character_gateway.has_method("refresh_battle_unit"):
		_character_gateway.refresh_battle_unit(unit_state)
		batch.changed_unit_ids.append(unit_state.unit_id)
		batch.log_lines.append("%s 完成职业晋升。" % unit_state.display_name)
	_state.modal_state = &""
	_state.timeline.frozen = false
	return batch


func get_state() -> BattleState:
	return _state


func is_battle_active() -> bool:
	return _state != null and _state.phase != &"battle_ended"


func end_battle(result: Dictionary = {}) -> void:
	if _state == null:
		return
	if _character_gateway != null and bool(result.get("commit_progression", false)):
		for ally_unit_id in _state.ally_unit_ids:
			var unit_state := _state.units.get(ally_unit_id) as BattleUnitState
			if unit_state == null:
				continue
			if unit_state.is_alive:
				if _character_gateway.has_method("commit_battle_resources"):
					_character_gateway.commit_battle_resources(unit_state.source_member_id, unit_state.current_hp, unit_state.current_mp)
			else:
				if _character_gateway.has_method("commit_battle_ko"):
					_character_gateway.commit_battle_ko(unit_state.source_member_id)
		if _character_gateway.has_method("flush_after_battle"):
			_character_gateway.flush_after_battle()


func consume_pending_mastery_rewards() -> Array:
	var rewards := _pending_post_battle_mastery_rewards.duplicate()
	_pending_post_battle_mastery_rewards.clear()
	return rewards


func dispose() -> void:
	_battle_rating_stats.clear()
	_pending_post_battle_mastery_rewards.clear()
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


func _get_move_cost_for_unit_target(unit_state: BattleUnitState, target_coord: Vector2i) -> int:
	if _state == null or unit_state == null:
		return 1
	var move_cost := 1
	for occupied_coord in _grid_service.get_unit_target_coords(unit_state, target_coord):
		move_cost = maxi(move_cost, _grid_service.get_movement_cost(_state, occupied_coord))
	if _has_status(unit_state, STATUS_ARCHER_QUICKSTEP):
		move_cost = maxi(move_cost - 1, 0)
	return move_cost


func _handle_move_command(active_unit: BattleUnitState, command: BattleCommand, batch: BattleEventBatch) -> void:
	if _is_movement_blocked(active_unit):
		batch.log_lines.append("%s 当前被限制移动。" % active_unit.display_name)
		return
	var target_coord := command.target_coord
	var target_cell := _grid_service.get_cell(_state, target_coord)
	if target_cell == null:
		return
	if not _grid_service.can_traverse(_state, active_unit.coord, target_coord, active_unit):
		return
	var move_cost := _get_move_cost_for_unit_target(active_unit, target_coord)
	if active_unit.current_ap < move_cost:
		return

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
	if cast_variant != null:
		applied = _handle_ground_skill_command(active_unit, command, skill_def, cast_variant, batch)
	else:
		applied = _handle_unit_skill_command(active_unit, command, skill_def, batch)

	if applied:
		_grant_skill_mastery_if_needed(active_unit, command.skill_id, batch)


func _preview_skill_command(active_unit: BattleUnitState, command: BattleCommand, preview: BattlePreview) -> void:
	var skill_def := _skill_defs.get(command.skill_id) as SkillDef
	if skill_def == null or skill_def.combat_profile == null:
		preview.log_lines.append("技能或目标无效。")
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
	var target_unit := _state.units.get(command.target_unit_id) as BattleUnitState
	if target_unit == null or not target_unit.is_alive:
		preview.log_lines.append("技能或目标无效。")
		return

	var block_reason := _get_skill_cast_block_reason(active_unit, skill_def)
	if not block_reason.is_empty():
		preview.log_lines.append(block_reason)
		return

	active_unit.refresh_footprint()
	target_unit.refresh_footprint()
	preview.allowed = active_unit.current_ap >= skill_def.combat_profile.ap_cost \
		and _grid_service.get_distance_between_units(active_unit, target_unit) <= _get_effective_skill_range(active_unit, skill_def)
	if preview.allowed:
		preview.target_unit_ids.append(target_unit.unit_id)
		for occupied_coord in target_unit.occupied_coords:
			preview.target_coords.append(occupied_coord)
		preview.log_lines.append("%s 可对 %s 使用 %s。" % [active_unit.display_name, target_unit.display_name, skill_def.display_name])
	else:
		preview.log_lines.append("技能目标超出范围或行动点不足。")


func _preview_ground_skill_command(
	active_unit: BattleUnitState,
	command: BattleCommand,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	preview: BattlePreview
) -> void:
	var validation := _validate_ground_skill_command(active_unit, skill_def, cast_variant, command)
	preview.target_coords.clear()
	for target_coord in validation.get("preview_coords", _build_ground_effect_coords(skill_def, validation.get("target_coords", []))):
		preview.target_coords.append(target_coord)
	preview.target_unit_ids = _collect_ground_preview_unit_ids(
		active_unit,
		skill_def,
		_collect_ground_unit_effect_defs(skill_def, cast_variant),
		preview.target_coords
	)
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


func _handle_unit_skill_command(
	active_unit: BattleUnitState,
	command: BattleCommand,
	skill_def: SkillDef,
	batch: BattleEventBatch
) -> bool:
	var target_unit := _state.units.get(command.target_unit_id) as BattleUnitState
	if target_unit == null or not target_unit.is_alive:
		return false
	active_unit.refresh_footprint()
	target_unit.refresh_footprint()
	if _grid_service.get_distance_between_units(active_unit, target_unit) > _get_effective_skill_range(active_unit, skill_def):
		return false

	_consume_skill_costs(active_unit, skill_def)
	var result := _damage_resolver.resolve_skill(active_unit, target_unit, skill_def)
	_append_changed_unit_id(batch, active_unit.unit_id)
	_append_changed_unit_id(batch, target_unit.unit_id)
	_append_changed_unit_coords(batch, target_unit)
	if not bool(result.get("applied", false)):
		return false

	var damage := int(result.get("damage", 0))
	var healing := int(result.get("healing", 0))
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
		_record_enemy_defeated_achievement(active_unit, target_unit)
	_record_skill_effect_result(active_unit, damage, healing, 1 if not target_unit.is_alive else 0)
	return true


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
	if _is_charge_variant(cast_variant):
		return _handle_charge_skill_command(active_unit, skill_def, cast_variant, validation, batch)

	var effect_coords := _build_ground_effect_coords(skill_def, validation.get("target_coords", []))
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


func _build_ground_effect_coords(skill_def: SkillDef, target_coords: Array) -> Array[Vector2i]:
	var normalized_target_coords: Array[Vector2i] = []
	for target_coord in target_coords:
		normalized_target_coords.append(target_coord)
	if _state == null or skill_def == null or skill_def.combat_profile == null:
		return _sort_coords(normalized_target_coords)

	var area_pattern: StringName = skill_def.combat_profile.area_pattern if skill_def.combat_profile.area_pattern != &"" else &"single"
	var area_value := maxi(int(skill_def.combat_profile.area_value), 0)
	var coord_set: Dictionary = {}
	for target_coord in normalized_target_coords:
		for effect_coord in _grid_service.get_area_coords(_state, target_coord, area_pattern, area_value):
			coord_set[effect_coord] = true
	if coord_set.is_empty():
		for target_coord in normalized_target_coords:
			if _grid_service.is_inside(_state, target_coord):
				coord_set[target_coord] = true

	var effect_coords: Array[Vector2i] = []
	for coord_variant in coord_set.keys():
		effect_coords.append(coord_variant)
	return _sort_coords(effect_coords)


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
			_record_enemy_defeated_achievement(source_unit, target_unit)

	if applied and source_unit != null:
		_record_skill_effect_result(source_unit, total_damage, total_healing, total_kill_count)
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

	for effect_def in effect_defs:
		if effect_def == null:
			continue
		match effect_def.effect_type:
			&"terrain", &"terrain_replace", &"terrain_replace_to", &"height", &"height_delta":
				for effect_coord in effect_coords:
					if _apply_ground_cell_effect(effect_coord, effect_def, batch):
						applied = true
			&"terrain_effect":
				if effect_def.duration_tu > 0 and effect_def.tick_interval_tu > 0:
					var field_instance_id := _build_terrain_effect_instance_id(effect_def.terrain_effect_id)
					var applied_coord_count := 0
					for effect_coord in effect_coords:
						if _upsert_timed_terrain_effect(effect_coord, source_unit, skill_def, effect_def, field_instance_id):
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


func _upsert_timed_terrain_effect(
	target_coord: Vector2i,
	source_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_def: CombatEffectDef,
	field_instance_id: StringName
) -> bool:
	var cell := _grid_service.get_cell(_state, target_coord)
	if cell == null or effect_def == null or effect_def.terrain_effect_id == &"":
		return false

	var normalized_behavior := _normalize_stack_behavior(effect_def.stack_behavior)
	var existing_index := -1
	for index in range(cell.timed_terrain_effects.size()):
		var existing_effect := cell.timed_terrain_effects[index] as BattleTerrainEffectState
		if existing_effect != null and existing_effect.effect_id == effect_def.terrain_effect_id:
			existing_index = index
			break

	if existing_index >= 0:
		match normalized_behavior:
			STACK_BEHAVIOR_IGNORE_EXISTING:
				return false
			STACK_BEHAVIOR_REFRESH:
				cell.timed_terrain_effects[existing_index] = _build_timed_terrain_effect(source_unit, skill_def, effect_def, field_instance_id)
				return true
			_:
				pass

	cell.timed_terrain_effects.append(_build_timed_terrain_effect(source_unit, skill_def, effect_def, field_instance_id))
	return true


func _build_timed_terrain_effect(
	source_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_def: CombatEffectDef,
	field_instance_id: StringName
) -> BattleTerrainEffectState:
	var effect_state := BattleTerrainEffectState.new()
	effect_state.field_instance_id = field_instance_id
	effect_state.effect_id = effect_def.terrain_effect_id
	effect_state.effect_type = effect_def.tick_effect_type if effect_def.tick_effect_type != &"" else TERRAIN_EFFECT_DAMAGE
	effect_state.source_unit_id = source_unit.unit_id if source_unit != null else &""
	effect_state.source_skill_id = skill_def.skill_id if skill_def != null else &""
	effect_state.target_team_filter = _resolve_effect_target_filter(skill_def, effect_def)
	effect_state.power = int(effect_def.power)
	effect_state.scaling_attribute_id = effect_def.scaling_attribute_id
	effect_state.defense_attribute_id = effect_def.defense_attribute_id
	effect_state.resistance_attribute_id = effect_def.resistance_attribute_id
	effect_state.tick_interval_tu = maxi(int(effect_def.tick_interval_tu), 1)
	effect_state.remaining_tu = maxi(int(effect_def.duration_tu), effect_state.tick_interval_tu)
	effect_state.next_tick_at_tu = _state.timeline.current_tu + effect_state.tick_interval_tu
	effect_state.stack_behavior = _normalize_stack_behavior(effect_def.stack_behavior)
	effect_state.params = effect_def.params.duplicate(true)
	if effect_def.status_id != &"":
		effect_state.params["status_id"] = String(effect_def.status_id)
	return effect_state


func _process_timed_terrain_effects(batch: BattleEventBatch) -> void:
	if _state == null or _state.timeline == null:
		return

	var processed_tick_keys: Dictionary = {}
	for coord in _sort_coords(_state.cells.keys()):
		var cell := _state.cells.get(coord) as BattleCellState
		if cell == null or cell.timed_terrain_effects.is_empty():
			continue

		var retained_effects: Array[BattleTerrainEffectState] = []
		var cell_changed := false
		for effect_variant in cell.timed_terrain_effects:
			var effect_state := effect_variant as BattleTerrainEffectState
			if effect_state == null:
				cell_changed = true
				continue

			while effect_state.remaining_tu > 0 and effect_state.tick_interval_tu > 0 and _state.timeline.current_tu >= effect_state.next_tick_at_tu:
				_apply_timed_terrain_effect_tick(coord, effect_state, processed_tick_keys, batch)
				effect_state.remaining_tu = maxi(effect_state.remaining_tu - effect_state.tick_interval_tu, 0)
				effect_state.next_tick_at_tu += effect_state.tick_interval_tu
				cell_changed = true

			if effect_state.remaining_tu > 0:
				retained_effects.append(effect_state)
			else:
				cell_changed = true

		if cell_changed:
			cell.timed_terrain_effects = retained_effects
			_append_changed_coord(batch, coord)


func _apply_timed_terrain_effect_tick(
	target_coord: Vector2i,
	effect_state: BattleTerrainEffectState,
	processed_tick_keys: Dictionary,
	batch: BattleEventBatch
) -> void:
	if _state == null or effect_state == null:
		return

	var cell := _grid_service.get_cell(_state, target_coord)
	if cell == null or cell.occupant_unit_id == &"":
		return

	var target_unit := _state.units.get(cell.occupant_unit_id) as BattleUnitState
	if target_unit == null or not target_unit.is_alive:
		return

	var source_unit := _state.units.get(effect_state.source_unit_id) as BattleUnitState if effect_state.source_unit_id != &"" else null
	if not _is_unit_valid_for_effect(source_unit, target_unit, effect_state.target_team_filter):
		return

	var tick_key := "%s|%s|%d" % [String(effect_state.field_instance_id), String(target_unit.unit_id), int(effect_state.next_tick_at_tu)]
	if processed_tick_keys.has(tick_key):
		return
	processed_tick_keys[tick_key] = true

	var temp_effect := CombatEffectDef.new()
	temp_effect.effect_type = effect_state.effect_type
	temp_effect.power = int(effect_state.power)
	temp_effect.scaling_attribute_id = effect_state.scaling_attribute_id
	temp_effect.defense_attribute_id = effect_state.defense_attribute_id
	temp_effect.resistance_attribute_id = effect_state.resistance_attribute_id
	temp_effect.status_id = ProgressionDataUtils.to_string_name(effect_state.params.get("status_id", ""))
	temp_effect.params = effect_state.params.duplicate(true)

	var result := _damage_resolver.resolve_effects(source_unit, target_unit, [temp_effect])
	if not bool(result.get("applied", false)):
		return

	_append_changed_unit_id(batch, target_unit.unit_id)
	_append_changed_unit_coords(batch, target_unit)
	var damage := int(result.get("damage", 0))
	var healing := int(result.get("healing", 0))
	var kill_count := 0
	if damage > 0:
		_append_batch_log(batch, "%s 受到 %s 的 %d 点伤害。" % [
			target_unit.display_name,
			_get_timed_terrain_effect_display_name(effect_state),
			damage,
		])
	if healing > 0:
		_append_batch_log(batch, "%s 受到 %s 影响，恢复 %d 点生命。" % [
			target_unit.display_name,
			_get_timed_terrain_effect_display_name(effect_state),
			healing,
		])
	for status_id in result.get("status_effect_ids", []):
		_append_batch_log(batch, "%s 获得状态 %s。" % [target_unit.display_name, String(status_id)])

	if not target_unit.is_alive:
		kill_count = 1
		_clear_defeated_unit(target_unit, batch)
		_append_batch_log(batch, "%s 被击倒。" % target_unit.display_name)
		_record_enemy_defeated_achievement(source_unit, target_unit)

	if source_unit != null:
		_record_skill_effect_result(source_unit, damage, healing, kill_count)


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


func _normalize_stack_behavior(stack_behavior: StringName) -> StringName:
	match stack_behavior:
		STACK_BEHAVIOR_STACK, STACK_BEHAVIOR_IGNORE_EXISTING:
			return stack_behavior
		_:
			return STACK_BEHAVIOR_REFRESH


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


func _get_timed_terrain_effect_display_name(effect_state: BattleTerrainEffectState) -> String:
	if effect_state != null and effect_state.params.has("display_name"):
		return String(effect_state.params.get("display_name", ""))
	return String(effect_state.effect_id) if effect_state != null else "地格效果"


func _append_batch_log(batch: BattleEventBatch, message: String) -> void:
	if batch == null or message.is_empty():
		return
	batch.log_lines.append(message)
	if _state != null:
		_state.log_entries.append(message)


func _handle_charge_skill_command(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	validation: Dictionary,
	batch: BattleEventBatch
) -> bool:
	var direction: Vector2i = validation.get("direction", Vector2i.ZERO)
	var requested_distance := int(validation.get("distance", 0))
	if direction == Vector2i.ZERO or requested_distance <= 0:
		return false

	var snapshot := _capture_charge_snapshot()
	var charge_batch := _new_batch()
	var start_coord := active_unit.coord
	var moved_steps := 0
	var trap_result := {"triggered": false}
	var stop_reason := ""

	while moved_steps < requested_distance:
		var next_anchor := active_unit.coord + direction
		if not _can_charge_enter_anchor(active_unit, next_anchor):
			stop_reason = "terrain"
			break

		var blocker_result := _resolve_charge_step_blockers(active_unit, next_anchor, direction, charge_batch)
		match String(blocker_result.get("result", "continue")):
			"fail":
				_restore_charge_snapshot(snapshot)
				_append_changed_unit_id(batch, active_unit.unit_id)
				_append_changed_unit_coords(batch, active_unit)
				batch.log_lines.append("%s 发起 %s，但被更大体型单位挡回原位。" % [
					active_unit.display_name,
					_format_skill_variant_label(skill_def, cast_variant),
				])
				return false
			"stop":
				stop_reason = String(blocker_result.get("reason", "blocker"))
				break
			_:
				pass

		var previous_coords := active_unit.occupied_coords.duplicate()
		if not _grid_service.move_unit(_state, active_unit, next_anchor):
			stop_reason = "blocked"
			break
		moved_steps += 1
		_append_changed_unit_id(charge_batch, active_unit.unit_id)
		_append_changed_coords(charge_batch, previous_coords)
		_append_changed_unit_coords(charge_batch, active_unit)

		trap_result = _trigger_charge_trap(active_unit)
		if bool(trap_result.get("triggered", false)):
			var trap_coord: Vector2i = trap_result.get("coord", active_unit.coord)
			_append_changed_coord(charge_batch, trap_coord)
			charge_batch.log_lines.append("%s 在 (%d, %d) 触发陷阱，冲锋被中断。" % [
				active_unit.display_name,
				trap_coord.x,
				trap_coord.y,
			])
			stop_reason = "trap"
			break

	_merge_batch(batch, charge_batch)
	if moved_steps > 0:
		batch.log_lines.append("%s 使用 %s，向%s冲锋 %d 格。" % [
			active_unit.display_name,
			_format_skill_variant_label(skill_def, cast_variant),
			_format_charge_direction(direction),
			moved_steps,
		])
		return true
	if not charge_batch.log_lines.is_empty():
		batch.log_lines.append("%s 使用 %s，但在起步时被拦下。" % [
			active_unit.display_name,
			_format_skill_variant_label(skill_def, cast_variant),
		])
		return true
	if stop_reason == "terrain":
		batch.log_lines.append("%s 使用 %s，但前方地形无法通过。" % [
			active_unit.display_name,
			_format_skill_variant_label(skill_def, cast_variant),
		])
	return false


func _capture_charge_snapshot() -> Dictionary:
	var unit_snapshot := {}
	for unit_id_variant in _state.units.keys():
		var unit_id := StringName(String(unit_id_variant))
		var unit_state := _state.units.get(unit_id) as BattleUnitState
		if unit_state == null:
			continue
		unit_snapshot[unit_id] = {
			"coord": unit_state.coord,
			"current_hp": unit_state.current_hp,
			"is_alive": unit_state.is_alive,
		}

	var terrain_snapshot := {}
	for coord_variant in _state.cells.keys():
		var coord: Vector2i = coord_variant
		var cell := _state.cells.get(coord) as BattleCellState
		if cell == null:
			continue
		terrain_snapshot[coord] = {
			"terrain_effect_ids": cell.terrain_effect_ids.duplicate(),
			"timed_terrain_effects": BATTLE_TERRAIN_EFFECT_STATE_SCRIPT.to_dict_array(cell.timed_terrain_effects),
		}

	return {
		"units": unit_snapshot,
		"terrain_effects": terrain_snapshot,
	}


func _restore_charge_snapshot(snapshot: Dictionary) -> void:
	if _state == null:
		return
	for cell_variant in _state.cells.values():
		var cell = cell_variant as BattleCellState
		if cell == null:
			continue
		cell.occupant_unit_id = &""
		var terrain_data: Dictionary = snapshot.get("terrain_effects", {}).get(cell.coord, {})
		if not terrain_data.is_empty():
			cell.terrain_effect_ids = terrain_data.get("terrain_effect_ids", []).duplicate()
			cell.timed_terrain_effects = BATTLE_TERRAIN_EFFECT_STATE_SCRIPT.from_dict_array(
				terrain_data.get("timed_terrain_effects", [])
			)

	for unit_id_variant in _state.units.keys():
		var unit_id := StringName(String(unit_id_variant))
		var unit_state := _state.units.get(unit_id) as BattleUnitState
		var unit_data: Dictionary = snapshot.get("units", {}).get(unit_id, {})
		if unit_state == null or unit_data.is_empty():
			continue
		unit_state.coord = unit_data.get("coord", unit_state.coord)
		unit_state.current_hp = int(unit_data.get("current_hp", unit_state.current_hp))
		unit_state.is_alive = bool(unit_data.get("is_alive", unit_state.is_alive))
		unit_state.refresh_footprint()
		if unit_state.is_alive:
			_grid_service.set_occupants(_state, unit_state.occupied_coords, unit_state.unit_id)


func _can_charge_enter_anchor(active_unit: BattleUnitState, target_anchor: Vector2i) -> bool:
	if _state == null or active_unit == null:
		return false
	active_unit.refresh_footprint()
	var delta := target_anchor - active_unit.coord
	var current_coords: Dictionary = {}
	for occupied_coord in active_unit.occupied_coords:
		current_coords[occupied_coord] = true

	for footprint_coord in _grid_service.get_unit_target_coords(active_unit, target_anchor):
		if not _grid_service.is_inside(_state, footprint_coord):
			return false
		var target_cell := _grid_service.get_cell(_state, footprint_coord)
		if target_cell == null or not target_cell.passable:
			return false
		var reference_coord := footprint_coord - delta
		if not current_coords.has(reference_coord):
			reference_coord = active_unit.coord
		var reference_cell := _grid_service.get_cell(_state, reference_coord)
		if reference_cell == null:
			return false
		if absi(int(reference_cell.current_height) - int(target_cell.current_height)) > 1:
			return false
	return true


func _resolve_charge_step_blockers(
	active_unit: BattleUnitState,
	next_anchor: Vector2i,
	direction: Vector2i,
	batch: BattleEventBatch
) -> Dictionary:
	var reserved_coords := _grid_service.get_unit_target_coords(active_unit, next_anchor)
	var reserved_coord_set: Dictionary = {}
	for reserved_coord in reserved_coords:
		reserved_coord_set[reserved_coord] = true

	var seen_blockers: Dictionary = {}
	for frontier_coord in _get_charge_frontier_coords(active_unit, next_anchor):
		var blocker := _grid_service.get_unit_at_coord(_state, frontier_coord)
		if blocker == null or blocker.unit_id == active_unit.unit_id or not blocker.is_alive:
			continue
		if seen_blockers.has(blocker.unit_id):
			continue
		seen_blockers[blocker.unit_id] = true
		if active_unit.body_size < blocker.body_size:
			return {"result": "fail", "reason": "smaller_body"}
		if blocker.footprint_size != Vector2i.ONE:
			batch.log_lines.append("%s 被 %s 拦住，无法继续冲锋。" % [active_unit.display_name, blocker.display_name])
			return {"result": "stop", "reason": "large_blocker"}
		var blocker_result := _resolve_charge_blocker(active_unit, blocker, direction, reserved_coord_set, batch)
		if blocker_result != "continue":
			return {"result": blocker_result, "reason": blocker_result}
	return {"result": "continue"}


func _get_charge_frontier_coords(active_unit: BattleUnitState, next_anchor: Vector2i) -> Array[Vector2i]:
	var current_coords: Dictionary = {}
	for occupied_coord in active_unit.occupied_coords:
		current_coords[occupied_coord] = true
	var frontier_coords: Array[Vector2i] = []
	for target_coord in _grid_service.get_unit_target_coords(active_unit, next_anchor):
		if not current_coords.has(target_coord):
			frontier_coords.append(target_coord)
	return _sort_coords(frontier_coords)


func _resolve_charge_blocker(
	active_unit: BattleUnitState,
	blocker: BattleUnitState,
	direction: Vector2i,
	reserved_coord_set: Dictionary,
	batch: BattleEventBatch
) -> String:
	var side_push := _pick_charge_side_push(blocker, direction, reserved_coord_set)
	if bool(side_push.get("available", false)):
		var previous_coords := blocker.occupied_coords.duplicate()
		var side_coord: Vector2i = side_push.get("coord", blocker.coord)
		if _grid_service.move_unit_force(_state, blocker, side_coord):
			_append_changed_coords(batch, previous_coords)
			_append_changed_unit_coords(batch, blocker)
			_append_changed_unit_id(batch, blocker.unit_id)
			batch.log_lines.append("%s 将 %s 顶向侧面。" % [active_unit.display_name, blocker.display_name])
			var fall_layers := int(side_push.get("fall_layers", 0))
			if fall_layers > 0:
				var fall_damage := _damage_resolver.resolve_fall_damage(blocker, fall_layers)
				if fall_damage > 0:
					batch.log_lines.append("%s 因侧推跌落 %d 层，受到 %d 点坠落伤害。" % [
						blocker.display_name,
						fall_layers,
						fall_damage,
					])
					_append_changed_unit_id(batch, blocker.unit_id)
					if not blocker.is_alive:
						_clear_defeated_unit(blocker, batch)
						batch.log_lines.append("%s 被击倒。" % blocker.display_name)
			return "continue"

	var forward_coord := blocker.coord + direction
	if not reserved_coord_set.has(forward_coord):
		var previous_coords := blocker.occupied_coords.duplicate()
		if _grid_service.move_unit(_state, blocker, forward_coord):
			_append_changed_coords(batch, previous_coords)
			_append_changed_unit_coords(batch, blocker)
			_append_changed_unit_id(batch, blocker.unit_id)
			batch.log_lines.append("%s 将 %s 向前顶开。" % [active_unit.display_name, blocker.display_name])
			return "continue"

	var collision_damage := _damage_resolver.resolve_collision_damage(blocker, active_unit.body_size, blocker.body_size)
	_append_changed_unit_id(batch, blocker.unit_id)
	batch.log_lines.append("%s 撞上 %s，造成 %d 点碰撞伤害。" % [
		active_unit.display_name,
		blocker.display_name,
		collision_damage,
	])
	if not blocker.is_alive:
		_clear_defeated_unit(blocker, batch)
		batch.log_lines.append("%s 被击倒。" % blocker.display_name)
		return "continue"

	if not reserved_coord_set.has(forward_coord):
		var previous_coords := blocker.occupied_coords.duplicate()
		if _grid_service.move_unit_force(_state, blocker, forward_coord):
			_append_changed_coords(batch, previous_coords)
			_append_changed_unit_coords(batch, blocker)
			_append_changed_unit_id(batch, blocker.unit_id)
			batch.log_lines.append("%s 被强行撞退一格。" % blocker.display_name)
			return "continue"
	return "stop"


func _pick_charge_side_push(
	blocker: BattleUnitState,
	direction: Vector2i,
	reserved_coord_set: Dictionary
) -> Dictionary:
	if blocker == null:
		return {"available": false}
	var blocker_cell := _grid_service.get_cell(_state, blocker.coord)
	if blocker_cell == null:
		return {"available": false}
	var current_height := int(blocker_cell.current_height)
	var lower_candidates: Array[Dictionary] = []
	var level_candidates: Array[Dictionary] = []
	for side_direction in _get_side_directions_for_charge(direction):
		var side_coord := blocker.coord + side_direction
		if reserved_coord_set.has(side_coord):
			continue
		if not _grid_service.can_place_footprint(_state, side_coord, blocker.footprint_size, blocker.unit_id):
			continue
		var side_cell := _grid_service.get_cell(_state, side_coord)
		if side_cell == null:
			continue
		var side_height := int(side_cell.current_height)
		if side_height > current_height:
			continue
		var candidate := {
			"available": true,
			"coord": side_coord,
			"fall_layers": maxi(current_height - side_height, 0),
		}
		if side_height < current_height:
			lower_candidates.append(candidate)
		else:
			level_candidates.append(candidate)
	if not lower_candidates.is_empty():
		return lower_candidates[0]
	if not level_candidates.is_empty():
		return level_candidates[0]
	return {"available": false}


func _get_side_directions_for_charge(direction: Vector2i) -> Array[Vector2i]:
	if direction.x != 0:
		return [Vector2i.UP, Vector2i.DOWN]
	return [Vector2i.LEFT, Vector2i.RIGHT]


func _trigger_charge_trap(active_unit: BattleUnitState) -> Dictionary:
	for occupied_coord in _sort_coords(active_unit.occupied_coords):
		var cell := _grid_service.get_cell(_state, occupied_coord)
		if cell == null or cell.terrain_effect_ids.is_empty():
			continue
		var removed_ids: Array[StringName] = []
		for terrain_effect_id in cell.terrain_effect_ids.duplicate():
			if String(terrain_effect_id).begins_with(TRAP_EFFECT_PREFIX):
				cell.terrain_effect_ids.erase(terrain_effect_id)
				removed_ids.append(terrain_effect_id)
		if not removed_ids.is_empty():
			return {
				"triggered": true,
				"coord": occupied_coord,
				"terrain_effect_ids": removed_ids,
			}
	return {"triggered": false}


func _format_charge_direction(direction: Vector2i) -> String:
	if direction == Vector2i.LEFT:
		return "左"
	if direction == Vector2i.RIGHT:
		return "右"
	if direction == Vector2i.UP:
		return "上"
	if direction == Vector2i.DOWN:
		return "下"
	return "前"


func _grant_skill_mastery_if_needed(active_unit: BattleUnitState, skill_id: StringName, batch: BattleEventBatch) -> void:
	if active_unit.source_member_id == &"" or _character_gateway == null:
		return

	_record_skill_success(active_unit, skill_id)
	if _character_gateway.has_method("record_achievement_event"):
		_character_gateway.record_achievement_event(active_unit.source_member_id, &"skill_used", 1, skill_id)
	var delta = _character_gateway.grant_battle_mastery(active_unit.source_member_id, skill_id, 5)
	batch.progression_deltas.append(delta)
	_character_gateway.refresh_battle_unit(active_unit)
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
	if _is_charge_variant(cast_variant):
		return _validate_charge_command(active_unit, cast_variant, normalized_coords, result)

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
		if not cast_variant.allowed_base_terrains.is_empty() and not cast_variant.allowed_base_terrains.has(cell.base_terrain):
			result.message = "目标地格地形不符合该技能形态的要求。"
			return result

	if not _validate_target_coords_shape(cast_variant.footprint_pattern, normalized_coords):
		result.message = "目标地格排布不符合该技能形态。"
		return result

	result.target_coords = _sort_coords(normalized_coords)
	result.allowed = true
	result.message = "可施放。"
	return result


func _validate_charge_command(
	active_unit: BattleUnitState,
	cast_variant: CombatCastVariantDef,
	normalized_coords: Array[Vector2i],
	base_result: Dictionary
) -> Dictionary:
	var result := base_result.duplicate(true)
	var target_coord: Vector2i = normalized_coords[0]
	if not _grid_service.is_inside(_state, target_coord):
		result.message = "目标地格超出战场范围。"
		return result

	var target_info := _resolve_charge_target(active_unit, target_coord)
	if not bool(target_info.get("valid", false)):
		result.message = "冲锋只能选择当前单位同一行或同一列的目标地格。"
		return result

	var max_distance := _get_charge_max_distance(active_unit, cast_variant)
	var charge_distance := int(target_info.get("distance", 0))
	if charge_distance > max_distance:
		result.message = "目标地格超出当前冲锋距离 %d。" % max_distance
		return result

	result.allowed = true
	result.message = "可施放。"
	result.target_coords = [target_coord]
	result.preview_coords = _build_charge_preview_coords(active_unit, target_info.get("direction", Vector2i.ZERO), charge_distance)
	result.direction = target_info.get("direction", Vector2i.ZERO)
	result.distance = charge_distance
	return result


func _resolve_charge_target(active_unit: BattleUnitState, target_coord: Vector2i) -> Dictionary:
	if active_unit == null:
		return {"valid": false}
	active_unit.refresh_footprint()
	var footprint_size := active_unit.footprint_size
	var min_x := active_unit.coord.x
	var max_x := active_unit.coord.x + footprint_size.x - 1
	var min_y := active_unit.coord.y
	var max_y := active_unit.coord.y + footprint_size.y - 1

	if target_coord.y >= min_y and target_coord.y <= max_y:
		if target_coord.x < min_x:
			return {"valid": true, "direction": Vector2i.LEFT, "distance": min_x - target_coord.x}
		if target_coord.x > max_x:
			return {"valid": true, "direction": Vector2i.RIGHT, "distance": target_coord.x - max_x}
	if target_coord.x >= min_x and target_coord.x <= max_x:
		if target_coord.y < min_y:
			return {"valid": true, "direction": Vector2i.UP, "distance": min_y - target_coord.y}
		if target_coord.y > max_y:
			return {"valid": true, "direction": Vector2i.DOWN, "distance": target_coord.y - max_y}
	return {"valid": false}


func _build_charge_preview_coords(
	active_unit: BattleUnitState,
	direction: Vector2i,
	distance: int
) -> Array[Vector2i]:
	var preview_coords: Array[Vector2i] = []
	if active_unit == null or direction == Vector2i.ZERO or distance <= 0:
		return preview_coords
	var seen_coords: Dictionary = {}
	var preview_anchor := active_unit.coord
	for _step in range(distance):
		preview_anchor += direction
		for occupied_coord in _grid_service.get_unit_target_coords(active_unit, preview_anchor):
			if seen_coords.has(occupied_coord):
				continue
			seen_coords[occupied_coord] = true
			preview_coords.append(occupied_coord)
	return _sort_coords(preview_coords)


func _get_charge_max_distance(active_unit: BattleUnitState, cast_variant: CombatCastVariantDef) -> int:
	var charge_effect := _get_charge_effect_def(cast_variant)
	if charge_effect == null:
		return 0
	var skill_level := _get_unit_skill_level(active_unit, charge_effect.params.get("skill_id", &"charge"))
	var max_distance := int(charge_effect.params.get("base_distance", 3))
	var distance_by_level: Dictionary = charge_effect.params.get("distance_by_level", {})
	for breakpoint_key in distance_by_level.keys():
		var level_breakpoint: int = int(breakpoint_key)
		if skill_level >= level_breakpoint:
			max_distance = maxi(max_distance, int(distance_by_level.get(breakpoint_key, max_distance)))
	return max_distance


func _is_charge_variant(cast_variant: CombatCastVariantDef) -> bool:
	return _get_charge_effect_def(cast_variant) != null


func _get_charge_effect_def(cast_variant: CombatCastVariantDef) -> CombatEffectDef:
	if cast_variant == null:
		return null
	for effect_def in cast_variant.effect_defs:
		if effect_def != null and effect_def.effect_type == CHARGE_EFFECT_TYPE:
			return effect_def
	return null


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
	_record_battle_won_achievements()
	_finalize_battle_rating_rewards()
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
	return ""


func _consume_skill_costs(active_unit: BattleUnitState, skill_def: SkillDef) -> void:
	if active_unit == null or skill_def == null or skill_def.combat_profile == null:
		return
	var combat_profile = skill_def.combat_profile
	active_unit.current_ap = maxi(active_unit.current_ap - int(combat_profile.ap_cost), 0)
	active_unit.current_mp = maxi(active_unit.current_mp - int(combat_profile.mp_cost), 0)
	active_unit.current_stamina = maxi(active_unit.current_stamina - int(combat_profile.stamina_cost), 0)
	var cooldown := maxi(int(combat_profile.cooldown_tu), 0)
	if cooldown > 0:
		active_unit.cooldowns[skill_def.skill_id] = cooldown


func _advance_unit_turn_timers(unit_state: BattleUnitState, batch: BattleEventBatch) -> void:
	if unit_state == null:
		return
	var changed := false
	var retained_cooldowns: Dictionary = {}
	for skill_id_variant in unit_state.cooldowns.keys():
		var skill_id := ProgressionDataUtils.to_string_name(skill_id_variant)
		var previous_remaining := int(unit_state.cooldowns.get(skill_id_variant, 0))
		var remaining := maxi(previous_remaining - 1, 0)
		if remaining > 0:
			retained_cooldowns[skill_id] = remaining
		if remaining != previous_remaining:
			changed = true
	unit_state.cooldowns = retained_cooldowns

	var expired_status_ids: Array[StringName] = []
	for status_id_variant in unit_state.status_effects.keys():
		var status_id := ProgressionDataUtils.to_string_name(status_id_variant)
		var status_entry = unit_state.status_effects.get(status_id_variant, {})
		if status_entry is not Dictionary:
			expired_status_ids.append(status_id)
			changed = true
			continue
		if not status_entry.has("duration"):
			continue
		var previous_duration := int(status_entry.get("duration", 0))
		var remaining_duration := maxi(previous_duration - 1, 0)
		if remaining_duration <= 0:
			expired_status_ids.append(status_id)
			changed = true
			continue
		status_entry["duration"] = remaining_duration
		unit_state.status_effects[status_id] = status_entry
		if remaining_duration != previous_duration:
			changed = true
	for expired_status_id in expired_status_ids:
		unit_state.status_effects.erase(expired_status_id)

	if changed:
		_append_changed_unit_id(batch, unit_state.unit_id)


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
	return unit_state.status_effects.has(status_id)


func _consume_status_if_present(unit_state: BattleUnitState, status_id: StringName, batch: BattleEventBatch = null) -> void:
	if unit_state == null or status_id == &"" or not unit_state.status_effects.has(status_id):
		return
	unit_state.status_effects.erase(status_id)
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


func _build_ally_units(party_state, context: Dictionary) -> Array:
	var member_ids: Array = []
	if party_state != null and party_state.active_member_ids is Array:
		member_ids = party_state.active_member_ids
	if member_ids.is_empty():
		member_ids = context.get("ally_member_ids", [StringName("player_a"), StringName("player_b")])

	var units: Array = []
	for index in range(member_ids.size()):
		var member_id := StringName(String(member_ids[index]))
		var member_state = party_state.get_member_state(member_id) if party_state != null and party_state.has_method("get_member_state") else null
		var unit_state: BattleUnitState = _build_runtime_ally_unit(member_id, member_state, index, context)
		if unit_state != null:
			units.append(unit_state)
	return units


func _build_runtime_ally_unit(member_id: StringName, member_state, index: int, context: Dictionary):
	var unit_state = BattleUnitState.new()
	unit_state.unit_id = member_id if member_id != &"" else StringName("ally_%d" % [index + 1])
	unit_state.source_member_id = member_id
	if member_state != null and String(member_state.display_name) != "":
		unit_state.display_name = String(member_state.display_name)
	else:
		unit_state.display_name = "队员%d" % [index + 1]
	unit_state.faction_id = &"player"
	unit_state.control_mode = member_state.control_mode if member_state != null and member_state.control_mode != &"" else &"manual"
	unit_state.body_size = maxi(int(member_state.body_size), 1) if member_state != null else 1
	unit_state.refresh_footprint()
	var hp_max := int(context.get("default_ally_hp", member_state.current_hp if member_state != null else 24))
	var mp_max := int(context.get("default_ally_mp", member_state.current_mp if member_state != null else 0))
	var stamina_max := int(context.get("default_ally_stamina", 0))
	var action_points := int(context.get("default_ally_ap", 6))
	unit_state.attribute_snapshot.set_value(&"hp_max", maxi(hp_max, 1))
	unit_state.attribute_snapshot.set_value(&"mp_max", maxi(mp_max, 0))
	unit_state.attribute_snapshot.set_value(&"stamina_max", maxi(stamina_max, 0))
	unit_state.attribute_snapshot.set_value(&"action_points", maxi(action_points, 1))
	unit_state.attribute_snapshot.set_value(&"physical_attack", int(context.get("default_ally_attack", 8)))
	unit_state.attribute_snapshot.set_value(&"physical_defense", int(context.get("default_ally_defense", 4)))
	unit_state.attribute_snapshot.set_value(&"magic_attack", int(context.get("default_ally_magic_attack", 10)))
	unit_state.attribute_snapshot.set_value(&"magic_defense", int(context.get("default_ally_magic_defense", 4)))
	unit_state.attribute_snapshot.set_value(&"fire_resistance", int(context.get("default_ally_fire_resistance", 0)))
	unit_state.attribute_snapshot.set_value(&"speed", int(context.get("default_ally_speed", 100)))
	unit_state.current_hp = hp_max
	unit_state.current_mp = mp_max
	unit_state.current_stamina = stamina_max
	unit_state.current_ap = action_points
	unit_state.is_alive = unit_state.current_hp > 0
	var default_skills: Variant = context.get("default_active_skill_ids", [])
	if default_skills is Array:
		unit_state.known_active_skill_ids.clear()
		for skill_id in default_skills:
			var normalized_skill_id := StringName(String(skill_id))
			unit_state.known_active_skill_ids.append(normalized_skill_id)
			unit_state.known_skill_level_map[normalized_skill_id] = 1
	return unit_state


func _build_enemy_units(encounter_anchor, context: Dictionary) -> Array:
	var enemy_count := maxi(int(context.get("enemy_unit_count", 1)), 1)
	var monster_name := String(context.get("monster_display_name", encounter_anchor.display_name if encounter_anchor != null else "敌人"))
	var units: Array = []
	for index in range(enemy_count):
		units.append(_build_runtime_enemy_unit(encounter_anchor, monster_name, index, context))
	return units


func _build_runtime_enemy_unit(encounter_anchor, monster_name: String, index: int, context: Dictionary):
	var unit_state = BattleUnitState.new()
	var anchor_id := String(encounter_anchor.entity_id) if encounter_anchor != null else "wild"
	unit_state.unit_id = StringName("%s_%02d" % [anchor_id, index + 1])
	unit_state.source_member_id = &""
	unit_state.display_name = monster_name if index == 0 else "%s·从属%d" % [monster_name, index + 1]
	unit_state.faction_id = StringName(String(encounter_anchor.faction_id)) if encounter_anchor != null and String(encounter_anchor.faction_id) != "" else &"hostile"
	unit_state.control_mode = &"ai"
	unit_state.body_size = 1
	unit_state.refresh_footprint()
	var hp_max := int(context.get("default_enemy_hp", 12))
	var mp_max := int(context.get("default_enemy_mp", 0))
	var stamina_max := int(context.get("default_enemy_stamina", 0))
	var action_points := int(context.get("default_enemy_ap", 1))
	unit_state.attribute_snapshot.set_value(&"hp_max", maxi(hp_max, 1))
	unit_state.attribute_snapshot.set_value(&"mp_max", maxi(mp_max, 0))
	unit_state.attribute_snapshot.set_value(&"stamina_max", maxi(stamina_max, 0))
	unit_state.attribute_snapshot.set_value(&"action_points", maxi(action_points, 1))
	unit_state.attribute_snapshot.set_value(&"physical_attack", int(context.get("default_attack", 8)))
	unit_state.attribute_snapshot.set_value(&"physical_defense", int(context.get("default_defense", 4)))
	unit_state.attribute_snapshot.set_value(&"magic_attack", int(context.get("default_magic_attack", 6)))
	unit_state.attribute_snapshot.set_value(&"magic_defense", int(context.get("default_magic_defense", 3)))
	unit_state.attribute_snapshot.set_value(&"fire_resistance", int(context.get("default_fire_resistance", 0)))
	unit_state.attribute_snapshot.set_value(&"speed", int(context.get("default_speed", 100)))
	unit_state.current_hp = hp_max
	unit_state.current_mp = mp_max
	unit_state.current_stamina = stamina_max
	unit_state.current_ap = action_points
	unit_state.is_alive = unit_state.current_hp > 0
	var enemy_skills: Variant = context.get("enemy_skill_ids", [])
	if enemy_skills is Array:
		unit_state.known_active_skill_ids.clear()
		for skill_id in enemy_skills:
			var normalized_skill_id := StringName(String(skill_id))
			unit_state.known_active_skill_ids.append(normalized_skill_id)
			unit_state.known_skill_level_map[normalized_skill_id] = 1
	if unit_state.known_active_skill_ids.is_empty():
		unit_state.known_active_skill_ids = _pick_default_enemy_skill_ids()
		for skill_id in unit_state.known_active_skill_ids:
			unit_state.known_skill_level_map[skill_id] = 1
	return unit_state


func _build_terrain_data(encounter_anchor, seed: int, context: Dictionary) -> Dictionary:
	if _uses_manual_terrain_layout(context):
		return _build_fallback_terrain(context)
	var terrain_data := _terrain_generator.generate(encounter_anchor, seed, context)
	if not terrain_data.is_empty():
		return terrain_data
	return _build_fallback_terrain(context)


func _uses_manual_terrain_layout(context: Dictionary) -> bool:
	return (
		context.has("cells")
		or context.has("map_size")
		or context.has("ally_spawns")
		or context.has("enemy_spawns")
	)


func _build_fallback_terrain(context: Dictionary) -> Dictionary:
	var map_size: Vector2i = context.get("map_size", Vector2i(5, 5))
	var cells: Dictionary = {}
	for y in range(map_size.y):
		for x in range(map_size.x):
			var cell_state: BattleCellState = BATTLE_CELL_STATE_SCRIPT.new()
			cell_state.coord = Vector2i(x, y)
			cell_state.base_terrain = &"land"
			cell_state.base_height = MIN_BATTLE_SURFACE_HEIGHT
			cell_state.height_offset = 0
			cell_state.terrain_effect_ids.clear()
			cell_state.timed_terrain_effects.clear()
			_grid_service.recalculate_cell(cell_state)
			cells[cell_state.coord] = cell_state

	return {
		"map_size": map_size,
		"cells": cells,
		"cell_columns": BattleCellState.build_columns_from_surface_cells(cells),
		"ally_spawns": context.get("ally_spawns", [Vector2i(1, 1), Vector2i(1, 3)]),
		"enemy_spawns": context.get("enemy_spawns", [Vector2i(3, 1), Vector2i(3, 3)]),
	}


func _pick_default_enemy_skill_ids() -> Array[StringName]:
	var preferred_skill_ids: Array[StringName] = [
		&"warrior_heavy_strike",
		&"warrior_combo_strike",
		&"warrior_guard_break",
	]
	for preferred_skill_id in preferred_skill_ids:
		var preferred_skill := _skill_defs.get(preferred_skill_id) as SkillDef
		if _is_valid_enemy_skill(preferred_skill):
			return [preferred_skill_id]

	for skill_id_str in ProgressionDataUtils.sorted_string_keys(_skill_defs):
		var skill_id := StringName(skill_id_str)
		var skill_def := _skill_defs.get(skill_id) as SkillDef
		if _is_valid_enemy_skill(skill_def):
			return [skill_id]

	return []


func _is_valid_enemy_skill(skill_def: SkillDef) -> bool:
	if skill_def == null:
		return false
	if skill_def.skill_type != &"active":
		return false
	if not skill_def.can_use_in_combat():
		return false
	if skill_def.combat_profile == null:
		return false
	if skill_def.combat_profile.target_mode != &"unit":
		return false
	return skill_def.combat_profile.target_team_filter == &"enemy"


func _new_batch() -> BattleEventBatch:
	return BATTLE_EVENT_BATCH_SCRIPT.new()


func _initialize_battle_rating_stats() -> void:
	_battle_rating_stats.clear()
	_pending_post_battle_mastery_rewards.clear()
	if _state == null:
		return
	for ally_unit_id in _state.ally_unit_ids:
		var unit_state := _state.units.get(ally_unit_id) as BattleUnitState
		if unit_state == null:
			continue
		if unit_state.control_mode != &"manual":
			continue
		if unit_state.source_member_id == &"":
			continue
		_battle_rating_stats[unit_state.source_member_id] = {
			"member_id": unit_state.source_member_id,
			"member_name": unit_state.display_name if not unit_state.display_name.is_empty() else String(unit_state.source_member_id),
			"cast_counts": {},
			"successful_skill_count": 0,
			"total_damage_done": 0,
			"total_healing_done": 0,
			"kill_count": 0,
		}


func _record_skill_success(active_unit: BattleUnitState, skill_id: StringName) -> void:
	var stats := _get_battle_rating_stats(active_unit)
	if stats.is_empty() or skill_id == &"":
		return
	var cast_counts: Dictionary = stats.get("cast_counts", {})
	cast_counts[skill_id] = int(cast_counts.get(skill_id, 0)) + 1
	stats["cast_counts"] = cast_counts
	stats["successful_skill_count"] = int(stats.get("successful_skill_count", 0)) + 1
	_battle_rating_stats[active_unit.source_member_id] = stats


func _record_skill_effect_result(active_unit: BattleUnitState, damage: int, healing: int, kill_count: int) -> void:
	var stats := _get_battle_rating_stats(active_unit)
	if stats.is_empty():
		return
	stats["total_damage_done"] = int(stats.get("total_damage_done", 0)) + maxi(damage, 0)
	stats["total_healing_done"] = int(stats.get("total_healing_done", 0)) + maxi(healing, 0)
	stats["kill_count"] = int(stats.get("kill_count", 0)) + maxi(kill_count, 0)
	_battle_rating_stats[active_unit.source_member_id] = stats


func _record_enemy_defeated_achievement(source_unit: BattleUnitState, target_unit: BattleUnitState) -> void:
	if source_unit == null or target_unit == null or _character_gateway == null:
		return
	if source_unit.source_member_id == &"":
		return
	if String(target_unit.faction_id) == String(source_unit.faction_id):
		return
	if not _character_gateway.has_method("record_achievement_event"):
		return
	_character_gateway.record_achievement_event(source_unit.source_member_id, &"enemy_defeated", 1)


func _record_battle_won_achievements() -> void:
	if _state == null or _state.winner_faction_id != &"player" or _character_gateway == null:
		return
	if not _character_gateway.has_method("record_achievement_event"):
		return
	for ally_unit_id in _state.ally_unit_ids:
		var unit_state := _state.units.get(ally_unit_id) as BattleUnitState
		if unit_state == null or unit_state.source_member_id == &"":
			continue
		_character_gateway.record_achievement_event(unit_state.source_member_id, &"battle_won", 1)


func _get_battle_rating_stats(active_unit: BattleUnitState) -> Dictionary:
	if active_unit == null or active_unit.source_member_id == &"":
		return {}
	var stats_variant = _battle_rating_stats.get(active_unit.source_member_id, {})
	return stats_variant.duplicate(true) if stats_variant is Dictionary else {}


func _finalize_battle_rating_rewards() -> void:
	_pending_post_battle_mastery_rewards.clear()
	if _state == null or _character_gateway == null:
		return
	if not _character_gateway.has_method("build_pending_mastery_reward"):
		return

	var player_victory := _state.winner_faction_id == &"player"
	for stats_variant in _battle_rating_stats.values():
		if stats_variant is not Dictionary:
			continue
		var stats: Dictionary = stats_variant
		var score := _calculate_battle_rating_score(stats, player_victory)
		var mastery_amount := _resolve_battle_rating_mastery_amount(score)
		if mastery_amount <= 0:
			continue
		var cast_counts: Dictionary = stats.get("cast_counts", {})
		if cast_counts.is_empty():
			continue

		var member_id := ProgressionDataUtils.to_string_name(stats.get("member_id", ""))
		if member_id == &"":
			continue
		var member_name := String(stats.get("member_name", member_id))
		var rating_label := _resolve_battle_rating_label(score)
		var mastery_entries: Array[Dictionary] = []
		for skill_key in cast_counts.keys():
			var skill_id := ProgressionDataUtils.to_string_name(skill_key)
			if skill_id == &"" or int(cast_counts.get(skill_key, 0)) <= 0:
				continue
			mastery_entries.append({
				"skill_id": skill_id,
				"mastery_amount": mastery_amount,
				"reason_text": "战斗评分 %d · %s" % [score, rating_label],
			})
		if mastery_entries.is_empty():
			continue

		var reward = _character_gateway.build_pending_mastery_reward(
			member_id,
			BATTLE_RATING_SOURCE_TYPE,
			"战斗结算",
			mastery_entries,
			"在战斗中，%s%s。评分 %d。" % [
				member_name,
				_resolve_battle_rating_summary_suffix(score),
				score,
			]
		)
		if reward != null and not reward.is_empty():
			_pending_post_battle_mastery_rewards.append(reward)


func _calculate_battle_rating_score(stats: Dictionary, player_victory: bool) -> int:
	var successful_skill_count := int(stats.get("successful_skill_count", 0))
	var total_damage_done := int(stats.get("total_damage_done", 0))
	var total_healing_done := int(stats.get("total_healing_done", 0))
	var kill_count := int(stats.get("kill_count", 0))
	var member_id := ProgressionDataUtils.to_string_name(stats.get("member_id", ""))
	var survived := false
	if _state != null and member_id != &"":
		var unit_state := _find_unit_by_member_id(member_id)
		survived = unit_state != null and unit_state.is_alive

	var score := 0
	if successful_skill_count > 0:
		score += 1
	score += mini(successful_skill_count, 3)
	if total_damage_done > 0 or total_healing_done > 0:
		score += 1
	if kill_count > 0:
		score += 1
	if player_victory:
		score += 1
	if survived:
		score += 1
	return score


func _resolve_battle_rating_mastery_amount(score: int) -> int:
	if score >= 6:
		return 6
	if score >= 4:
		return 4
	if score >= 2:
		return 2
	return 0


func _resolve_battle_rating_label(score: int) -> String:
	if score >= 6:
		return "若有所悟"
	if score >= 4:
		return "渐入佳境"
	if score >= 2:
		return "有所体会"
	return "尚需磨炼"


func _resolve_battle_rating_summary_suffix(score: int) -> String:
	if score >= 6:
		return "若有所悟"
	if score >= 4:
		return "渐入佳境"
	if score >= 2:
		return "有所体会"
	return "尚需磨炼"
