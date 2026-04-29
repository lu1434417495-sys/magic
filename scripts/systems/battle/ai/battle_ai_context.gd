class_name BattleAiContext
extends RefCounted

const BATTLE_PREVIEW_SCRIPT = preload("res://scripts/systems/battle/core/battle_preview.gd")
const BATTLE_AI_SCORE_INPUT_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_score_input.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const BATTLE_COMMAND_SCRIPT = preload("res://scripts/systems/battle/core/battle_command.gd")
const BattlePreview = preload("res://scripts/systems/battle/core/battle_preview.gd")
const BattleAiScoreInput = preload("res://scripts/systems/battle/ai/battle_ai_score_input.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const BattleCommand = preload("res://scripts/systems/battle/core/battle_command.gd")
const STATUS_TAUNTED: StringName = &"taunted"

var state = null
var unit_state = null
var grid_service = null
var skill_defs: Dictionary = {}
var preview_callback: Callable = Callable()
var skill_score_input_callback: Callable = Callable()
var action_score_input_callback: Callable = Callable()
var trace_enabled := false
var action_traces: Array[Dictionary] = []
var _action_trace_nonce := 0


func preview_command(command) -> BattlePreview:
	if not preview_callback.is_valid():
		return BATTLE_PREVIEW_SCRIPT.new()
	var preview = preview_callback.call(command)
	return preview if preview is BattlePreview else BATTLE_PREVIEW_SCRIPT.new()


func build_skill_score_input(skill_def, command, preview: BattlePreview, effect_defs: Array = [], metadata: Dictionary = {}) -> BattleAiScoreInput:
	if not skill_score_input_callback.is_valid():
		return null
	var score_input = skill_score_input_callback.call(self, skill_def, command, preview, effect_defs, metadata)
	return score_input if score_input is BattleAiScoreInput else null


func build_action_score_input(
	action_kind: StringName,
	action_label: String,
	score_bucket_id: StringName,
	command,
	preview: BattlePreview,
	metadata: Dictionary = {}
) -> BattleAiScoreInput:
	if not action_score_input_callback.is_valid():
		return null
	var score_input = action_score_input_callback.call(
		self,
		action_kind,
		action_label,
		score_bucket_id,
		command,
		preview,
		metadata
	)
	return score_input if score_input is BattleAiScoreInput else null


func resolve_forced_target_unit(target_filter: StringName):
	if state == null or unit_state == null:
		return null
	if target_filter != &"enemy":
		return null
	var taunt_entry = unit_state.get_status_effect(STATUS_TAUNTED)
	if taunt_entry == null:
		return null
	var source_id := ProgressionDataUtils.to_string_name(taunt_entry.source_unit_id)
	if source_id == &"":
		return null
	var source_unit = state.units.get(source_id) as BattleUnitState
	if source_unit == null or not bool(source_unit.is_alive):
		return null
	if source_unit.faction_id == unit_state.faction_id:
		return null
	return source_unit


func next_action_trace_id(action_id: StringName) -> StringName:
	_action_trace_nonce += 1
	var normalized_action_id := action_id if action_id != &"" else &"anonymous_action"
	return StringName("%s_%d" % [String(normalized_action_id), _action_trace_nonce])


func record_action_trace(action_trace: Dictionary) -> void:
	if not trace_enabled or action_trace.is_empty():
		return
	action_traces.append(action_trace.duplicate(true))


func mark_action_trace_chosen(action_trace_id: StringName, decision = null) -> void:
	if action_trace_id == &"":
		return
	for trace_index in range(action_traces.size()):
		var action_trace := action_traces[trace_index]
		if action_trace.get("trace_id", &"") != action_trace_id:
			continue
		action_trace["chosen"] = true
		if decision != null:
			action_trace["chosen_reason_text"] = String(decision.reason_text)
			action_trace["chosen_command"] = _build_command_dict(decision.command)
			var score_input = decision.score_input if decision.score_input != null else decision.skill_score_input
			action_trace["chosen_score_input"] = score_input.to_dict() if score_input != null else {}
		action_traces[trace_index] = action_trace
		return


func build_turn_trace(decision = null) -> Dictionary:
	var resolved_brain_id := String(unit_state.ai_brain_id) if unit_state != null else ""
	var resolved_state_id := String(unit_state.ai_state_id) if unit_state != null else ""
	if decision != null:
		resolved_brain_id = String(decision.brain_id)
		resolved_state_id = String(decision.state_id)
	var turn_trace := {
		"battle_id": String(state.battle_id) if state != null else "",
		"turn_started_tu": int(unit_state.ai_blackboard.get("turn_started_tu", -1)) if unit_state != null else -1,
		"unit_id": String(unit_state.unit_id) if unit_state != null else "",
		"unit_name": unit_state.display_name if unit_state != null else "",
		"faction_id": String(unit_state.faction_id) if unit_state != null else "",
		"brain_id": resolved_brain_id,
		"state_id": resolved_state_id,
		"action_id": String(decision.action_id) if decision != null else "",
		"reason_text": decision.reason_text if decision != null else "",
		"command": _build_command_dict(decision.command) if decision != null else {},
		"score_input": {},
		"action_traces": action_traces.duplicate(true),
	}
	if decision != null:
		var score_input = decision.score_input if decision.score_input != null else decision.skill_score_input
		if score_input != null:
			turn_trace["score_input"] = score_input.to_dict()
	return turn_trace


func _build_command_dict(command: BattleCommand) -> Dictionary:
	if command == null:
		return {}
	return {
		"command_type": String(command.command_type),
		"unit_id": String(command.unit_id),
		"skill_id": String(command.skill_id),
		"skill_variant_id": String(command.skill_variant_id),
		"target_unit_id": String(command.target_unit_id),
		"target_unit_ids": command.target_unit_ids.duplicate(),
		"target_coord": command.target_coord,
		"target_coords": command.target_coords.duplicate(),
	}
