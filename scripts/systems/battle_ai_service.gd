## 文件说明：该脚本属于战斗自动决策服务相关的服务脚本，主要封装当前领域所需的辅助逻辑。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name BattleAiService
extends RefCounted

const BATTLE_AI_DECISION_SCRIPT = preload("res://scripts/systems/battle_ai_decision.gd")
const BATTLE_COMMAND_SCRIPT = preload("res://scripts/systems/battle_command.gd")
const BattleAiDecision = preload("res://scripts/systems/battle_ai_decision.gd")
const BattleCommand = preload("res://scripts/systems/battle_command.gd")
const BattleUnitState = preload("res://scripts/systems/battle_unit_state.gd")
const BattleGridService = preload("res://scripts/systems/battle_grid_service.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")

var _enemy_ai_brains: Dictionary = {}


func setup(enemy_ai_brains: Dictionary = {}) -> void:
	_enemy_ai_brains = enemy_ai_brains if enemy_ai_brains != null else {}


func choose_command(context) -> BattleAiDecision:
	if context == null or context.state == null or context.unit_state == null or context.grid_service == null:
		return null

	var unit_state: BattleUnitState = context.unit_state
	var brain = _resolve_brain(unit_state.ai_brain_id)
	if brain == null:
		var legacy_decision = _choose_legacy_command(context)
		_commit_decision(unit_state, legacy_decision)
		return legacy_decision

	unit_state.ai_brain_id = brain.brain_id
	var next_state_id: StringName = _resolve_state_id(context, brain)
	unit_state.ai_state_id = next_state_id
	var state_def = brain.get_state(next_state_id)
	if state_def == null:
		var missing_state_decision = _build_wait_decision(
			context,
			brain.brain_id,
			next_state_id,
			&"wait_missing_state",
			"%s 找不到 AI 状态 %s，改为待机。" % [unit_state.display_name, String(next_state_id)]
		)
		_commit_decision(unit_state, missing_state_decision)
		return missing_state_decision

	for action in state_def.get_actions():
		if action == null or not action.has_method("decide"):
			continue
		var decision = action.decide(context) as BattleAiDecision
		if decision == null or decision.command == null:
			continue
		decision.brain_id = brain.brain_id
		decision.state_id = next_state_id
		if decision.action_id == &"":
			decision.action_id = &"anonymous_action"
		_commit_decision(unit_state, decision)
		return decision

	var wait_decision = _build_wait_decision(
		context,
		brain.brain_id,
		next_state_id,
		&"wait_fallback",
		"%s 在状态 %s 下没有找到合法指令，改为待机。" % [unit_state.display_name, String(next_state_id)]
	)
	_commit_decision(unit_state, wait_decision)
	return wait_decision


func _resolve_brain(brain_id: StringName):
	if brain_id == &"":
		return null
	return _enemy_ai_brains.get(brain_id)


func _resolve_state_id(context, brain) -> StringName:
	var current_state_id: StringName = brain.default_state_id
	if context.unit_state != null and brain.has_state(context.unit_state.ai_state_id):
		current_state_id = context.unit_state.ai_state_id

	var hp_ratio = _get_hp_ratio(context.unit_state)
	if brain.has_state(&"retreat") and hp_ratio <= float(brain.retreat_hp_ratio):
		return &"retreat"
	if brain.has_state(&"support") and _has_support_window(context, float(brain.support_hp_ratio)):
		return &"support"

	var nearest_enemy = _find_nearest_enemy(context)
	if nearest_enemy == null:
		return current_state_id
	var nearest_distance: int = context.grid_service.get_distance_between_units(context.unit_state, nearest_enemy)
	if brain.has_state(&"pressure") and nearest_distance <= int(brain.pressure_distance):
		return &"pressure"
	if current_state_id == &"pressure" and brain.has_state(&"pressure") and nearest_distance <= int(brain.pressure_distance) + 1:
		return &"pressure"
	if brain.has_state(&"engage"):
		return &"engage"
	return current_state_id


func _has_support_window(context, threshold: float) -> bool:
	if context == null or context.unit_state == null:
		return false
	if not _unit_has_support_skill(context):
		return false
	for unit_variant in context.state.units.values():
		var ally_unit = unit_variant as BattleUnitState
		if ally_unit == null or not ally_unit.is_alive:
			continue
		if ally_unit.faction_id != context.unit_state.faction_id:
			continue
		if _get_hp_ratio(ally_unit) <= threshold:
			return true
	return false


func _unit_has_support_skill(context) -> bool:
	if context == null or context.unit_state == null:
		return false
	for skill_id in context.unit_state.known_active_skill_ids:
		var skill_def = context.skill_defs.get(skill_id) as SkillDef
		if _is_support_skill(skill_def):
			return true
	return false


func _is_support_skill(skill_def: SkillDef) -> bool:
	if skill_def == null or skill_def.combat_profile == null:
		return false
	if skill_def.combat_profile.target_team_filter == &"ally":
		return true
	for effect_def in skill_def.combat_profile.effect_defs:
		if effect_def == null:
			continue
		if effect_def.effect_type == &"heal":
			return true
		if effect_def.effect_target_team_filter == &"ally":
			return true
	for cast_variant in skill_def.combat_profile.cast_variants:
		if cast_variant == null:
			continue
		for effect_def in cast_variant.effect_defs:
			if effect_def == null:
				continue
			if effect_def.effect_type == &"heal":
				return true
			if effect_def.effect_target_team_filter == &"ally":
				return true
	return false


func _build_wait_decision(
	context,
	brain_id: StringName,
	state_id: StringName,
	action_id: StringName,
	reason_text: String
) -> BattleAiDecision:
	var command = BATTLE_COMMAND_SCRIPT.new()
	command.command_type = BattleCommand.TYPE_WAIT
	command.unit_id = context.unit_state.unit_id
	var decision = BATTLE_AI_DECISION_SCRIPT.new()
	decision.command = command
	decision.brain_id = brain_id
	decision.state_id = state_id
	decision.action_id = action_id
	decision.reason_text = reason_text
	return decision


func _commit_decision(unit_state: BattleUnitState, decision: BattleAiDecision) -> void:
	if unit_state == null or decision == null:
		return
	unit_state.ai_blackboard["last_brain_id"] = String(decision.brain_id)
	unit_state.ai_blackboard["last_state_id"] = String(decision.state_id)
	unit_state.ai_blackboard["last_action_id"] = String(decision.action_id)
	unit_state.ai_blackboard["last_reason_text"] = decision.reason_text
	unit_state.ai_blackboard["turn_decision_count"] = int(unit_state.ai_blackboard.get("turn_decision_count", 0)) + 1


func _choose_legacy_command(context) -> BattleAiDecision:
	var unit_state: BattleUnitState = context.unit_state
	var target_unit = _find_nearest_enemy(context)
	if target_unit == null:
		return _build_wait_decision(
			context,
			&"legacy",
			&"legacy",
			&"legacy_wait",
			"%s 没有找到目标，按旧版逻辑待机。" % unit_state.display_name
		)

	for chosen_skill_id in unit_state.known_active_skill_ids:
		if chosen_skill_id == &"":
			continue
		var skill_def = context.skill_defs.get(chosen_skill_id) as SkillDef
		if skill_def == null or skill_def.combat_profile == null:
			continue
		if skill_def.combat_profile.target_mode != &"unit":
			continue
		if skill_def.combat_profile.target_team_filter != &"enemy":
			continue
		if unit_state.current_ap < skill_def.combat_profile.ap_cost:
			continue
		if context.grid_service.get_distance_between_units(unit_state, target_unit) > skill_def.combat_profile.range_value:
			continue
		var skill_command = BATTLE_COMMAND_SCRIPT.new()
		skill_command.command_type = BattleCommand.TYPE_SKILL
		skill_command.unit_id = unit_state.unit_id
		skill_command.skill_id = chosen_skill_id
		skill_command.target_unit_id = target_unit.unit_id
		skill_command.target_coord = target_unit.coord
		var skill_decision = BATTLE_AI_DECISION_SCRIPT.new()
		skill_decision.command = skill_command
		skill_decision.brain_id = &"legacy"
		skill_decision.state_id = &"legacy"
		skill_decision.action_id = &"legacy_skill"
		skill_decision.reason_text = "%s 按旧版逻辑对 %s 使用 %s。" % [
			unit_state.display_name,
			target_unit.display_name,
			skill_def.display_name,
		]
		return skill_decision

	var next_coord = _pick_step_toward(context.state, unit_state, target_unit.coord, context.grid_service)
	if next_coord != Vector2i(-1, -1):
		var target_cell = context.grid_service.get_cell(context.state, next_coord)
		var move_cost = int(target_cell.move_cost) if target_cell != null else 1
		if context.grid_service.can_traverse(context.state, unit_state.coord, next_coord, unit_state) and unit_state.current_ap >= move_cost:
			var move_command = BATTLE_COMMAND_SCRIPT.new()
			move_command.command_type = BattleCommand.TYPE_MOVE
			move_command.unit_id = unit_state.unit_id
			move_command.target_coord = next_coord
			var move_decision = BATTLE_AI_DECISION_SCRIPT.new()
			move_decision.command = move_command
			move_decision.brain_id = &"legacy"
			move_decision.state_id = &"legacy"
			move_decision.action_id = &"legacy_move"
			move_decision.reason_text = "%s 按旧版逻辑逼近 %s。" % [unit_state.display_name, target_unit.display_name]
			return move_decision

	return _build_wait_decision(
		context,
		&"legacy",
		&"legacy",
		&"legacy_wait",
		"%s 按旧版逻辑没有找到合法动作，待机。" % unit_state.display_name
	)


func _find_nearest_enemy(context) -> BattleUnitState:
	if context == null or context.state == null or context.unit_state == null:
		return null
	var candidate_ids = context.state.enemy_unit_ids if context.unit_state.faction_id == &"player" else context.state.ally_unit_ids
	var best_unit: BattleUnitState = null
	var best_distance := 999999
	for unit_id in candidate_ids:
		var candidate = context.state.units.get(unit_id) as BattleUnitState
		if candidate == null or not candidate.is_alive:
			continue
		var distance = context.grid_service.get_distance_between_units(context.unit_state, candidate)
		if distance < best_distance:
			best_distance = distance
			best_unit = candidate
	return best_unit


func _pick_step_toward(
	state,
	unit_state: BattleUnitState,
	target_coord: Vector2i,
	grid_service: BattleGridService
) -> Vector2i:
	if unit_state == null:
		return Vector2i(-1, -1)
	var from_coord := unit_state.coord
	var best_coord := Vector2i(-1, -1)
	var best_distance := grid_service.get_distance(from_coord, target_coord)
	for neighbor in grid_service.get_neighbors_4(state, from_coord):
		if not grid_service.can_traverse(state, from_coord, neighbor, unit_state):
			continue
		var distance := grid_service.get_distance(neighbor, target_coord)
		if distance < best_distance:
			best_distance = distance
			best_coord = neighbor
	return best_coord


func _get_hp_ratio(unit_state: BattleUnitState) -> float:
	if unit_state == null or unit_state.attribute_snapshot == null:
		return 1.0
	var hp_max := maxi(int(unit_state.attribute_snapshot.get_value(&"hp_max")), 1)
	return clampf(float(unit_state.current_hp) / float(hp_max), 0.0, 1.0)
