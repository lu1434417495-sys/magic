## 文件说明：该脚本属于战斗自动决策服务相关的服务脚本，主要封装当前领域所需的辅助逻辑。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name BattleAiService
extends RefCounted

const BATTLE_AI_DECISION_SCRIPT = preload("res://scripts/systems/battle_ai_decision.gd")
const BATTLE_COMMAND_SCRIPT = preload("res://scripts/systems/battle_command.gd")
const BATTLE_AI_SCORE_SERVICE_SCRIPT = preload("res://scripts/systems/battle_ai_score_service.gd")
const BattleAiDecision = preload("res://scripts/systems/battle_ai_decision.gd")
const BattleCommand = preload("res://scripts/systems/battle_command.gd")
const BattleUnitState = preload("res://scripts/systems/battle_unit_state.gd")
const BattleGridService = preload("res://scripts/systems/battle_grid_service.gd")
const BattleDamageResolver = preload("res://scripts/systems/battle_damage_resolver.gd")
const BattleAiScoreService = preload("res://scripts/systems/battle_ai_score_service.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const STATUS_TAUNTED: StringName = &"taunted"

var _enemy_ai_brains: Dictionary = {}
var _score_service: BattleAiScoreService = BATTLE_AI_SCORE_SERVICE_SCRIPT.new()


func setup(enemy_ai_brains: Dictionary = {}, damage_resolver: BattleDamageResolver = null) -> void:
	_enemy_ai_brains = enemy_ai_brains if enemy_ai_brains != null else {}
	_score_service.setup(damage_resolver)


func choose_command(context) -> BattleAiDecision:
	if context == null or context.state == null or context.unit_state == null or context.grid_service == null:
		return null
	if not context.skill_score_input_callback.is_valid():
		context.skill_score_input_callback = Callable(self, "build_skill_score_input")

	var unit_state: BattleUnitState = context.unit_state
	var brain = _resolve_brain(unit_state.ai_brain_id)
	if brain == null:
		var missing_brain_decision = _build_wait_decision(
			context,
			&"",
			&"",
			&"wait_missing_brain",
			"%s 缺少正式 AI brain，改为待机。" % unit_state.display_name
		)
		_commit_decision(unit_state, missing_brain_decision)
		return missing_brain_decision

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


func build_skill_score_input(context, skill_def: SkillDef, command, preview, effect_defs: Array = [], metadata: Dictionary = {}):
	return _score_service.build_skill_score_input(context, skill_def, command, preview, effect_defs, metadata)


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


func _find_nearest_enemy(context) -> BattleUnitState:
	if context == null or context.state == null or context.unit_state == null:
		return null
	var taunted_target = _resolve_taunted_target(context)
	if taunted_target != null:
		return taunted_target
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


func _resolve_taunted_target(context) -> BattleUnitState:
	if context == null or context.state == null or context.unit_state == null:
		return null
	var taunt_entry = context.unit_state.get_status_effect(STATUS_TAUNTED)
	if taunt_entry == null:
		return null
	var source_unit_id: StringName = taunt_entry.source_unit_id
	if source_unit_id == &"":
		return null
	var source_unit = context.state.units.get(source_unit_id) as BattleUnitState
	if source_unit == null or not source_unit.is_alive:
		return null
	if source_unit.faction_id == context.unit_state.faction_id:
		return null
	return source_unit


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
