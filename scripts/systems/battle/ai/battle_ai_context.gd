class_name BattleAiContext
extends RefCounted

const AI_TRACE_RECORDER = preload("res://scripts/dev_tools/ai_trace_recorder.gd")
const BATTLE_PREVIEW_SCRIPT = preload("res://scripts/systems/battle/core/battle_preview.gd")
const BATTLE_AI_SCORE_INPUT_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_score_input.gd")
const BATTLE_AI_SKILL_AFFORDANCE_CLASSIFIER_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_skill_affordance_classifier.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const BATTLE_COMMAND_SCRIPT = preload("res://scripts/systems/battle/core/battle_command.gd")
const BattlePreview = preload("res://scripts/systems/battle/core/battle_preview.gd")
const BattleAiScoreInput = preload("res://scripts/systems/battle/ai/battle_ai_score_input.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const BattleCommand = preload("res://scripts/systems/battle/core/battle_command.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const STATUS_TAUNTED: StringName = &"taunted"

var state = null
var unit_state = null
var grid_service = null
var skill_defs: Dictionary = {}
var preview_callback: Callable = Callable()
var skill_score_input_callback: Callable = Callable()
var action_score_input_callback: Callable = Callable()
# Mirrors BattleMovementService._get_move_cost_for_unit_target so AI pathing sees terrain
# effects and status move-cost deltas on the same terms as player movement.
var move_cost_callback: Callable = Callable()
var runtime_action_plan = null
var allow_authored_action_fallback_for_tests := false
var trace_enabled := false
var action_traces: Array[Dictionary] = []
var score_projection_cache: Dictionary = {}
var mutation_guard_violations: Array[String] = []
var _action_trace_nonce := 0
var _action_metadata_stack: Array[Dictionary] = []
var _skill_affordance_classifier = BATTLE_AI_SKILL_AFFORDANCE_CLASSIFIER_SCRIPT.new()
var _skill_affordance_cache: Dictionary = {}


func preview_command(command) -> BattlePreview:
	AI_TRACE_RECORDER.enter(&"preview_command")
	var result := _preview_command_impl(command)
	AI_TRACE_RECORDER.exit(&"preview_command")
	return result


func _preview_command_impl(command) -> BattlePreview:
	if not preview_callback.is_valid():
		return BATTLE_PREVIEW_SCRIPT.new()
	var preview = preview_callback.call(command)
	return preview if preview is BattlePreview else BATTLE_PREVIEW_SCRIPT.new()


func get_move_cost(target_unit_state, target_coord: Vector2i) -> int:
	if move_cost_callback.is_valid():
		return int(move_cost_callback.call(target_unit_state, target_coord))
	if grid_service != null and state != null:
		return int(grid_service.get_unit_move_cost(state, target_unit_state, target_coord))
	return 1


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


func get_runtime_actions(state_id: StringName) -> Array:
	if state_id == &"":
		return []
	if runtime_action_plan != null and runtime_action_plan.has_method("get_actions"):
		return runtime_action_plan.get_actions(state_id)
	return []


func has_runtime_action_state(state_id: StringName) -> bool:
	if state_id == &"":
		return false
	if runtime_action_plan != null and runtime_action_plan.has_method("has_state"):
		return runtime_action_plan.has_state(state_id)
	return false


func is_runtime_action_plan_stale(brain) -> bool:
	if runtime_action_plan == null or not runtime_action_plan.has_method("is_stale_for"):
		return false
	return runtime_action_plan.is_stale_for(unit_state, brain, skill_defs)


func get_runtime_action_metadata(action) -> Dictionary:
	if runtime_action_plan != null and runtime_action_plan.has_method("get_action_metadata"):
		return runtime_action_plan.get_action_metadata(action)
	return {}


func get_skill_affordance_record(skill_id: StringName) -> Dictionary:
	var normalized_skill_id := ProgressionDataUtils.to_string_name(skill_id)
	if normalized_skill_id == &"":
		return {}
	if runtime_action_plan != null and runtime_action_plan.has_method("get_skill_affordance_record"):
		var plan_record: Dictionary = runtime_action_plan.get_skill_affordance_record(normalized_skill_id)
		if not plan_record.is_empty():
			return plan_record
	if _skill_affordance_cache.has(normalized_skill_id):
		var cached_record = _skill_affordance_cache.get(normalized_skill_id, {})
		return cached_record.duplicate(true) if cached_record is Dictionary else {}
	var skill_def = skill_defs.get(normalized_skill_id) as SkillDef
	if skill_def == null:
		return {}
	var skill_level := 1
	if unit_state != null:
		skill_level = int(unit_state.known_skill_level_map.get(normalized_skill_id, 1))
	var record: Dictionary = _skill_affordance_classifier.classify_skill(skill_def, skill_level)
	_skill_affordance_cache[normalized_skill_id] = record.duplicate(true)
	return record


func has_skill_affordance(affordances: Array) -> bool:
	if unit_state == null or affordances.is_empty():
		return false
	var desired_lookup: Dictionary = {}
	for affordance_variant in affordances:
		var affordance := ProgressionDataUtils.to_string_name(affordance_variant)
		if affordance != &"":
			desired_lookup[affordance] = true
	if desired_lookup.is_empty():
		return false
	for raw_skill_id in unit_state.known_active_skill_ids:
		var skill_id := ProgressionDataUtils.to_string_name(raw_skill_id)
		if skill_id == &"":
			continue
		var record: Dictionary = get_skill_affordance_record(skill_id)
		var skill_affordances = record.get("affordances", [])
		if skill_affordances is not Array:
			continue
		for skill_affordance_variant in skill_affordances:
			var skill_affordance := ProgressionDataUtils.to_string_name(skill_affordance_variant)
			if desired_lookup.has(skill_affordance):
				return true
	return false


func push_action_metadata(metadata: Dictionary) -> void:
	_action_metadata_stack.append(_normalize_runtime_action_metadata(metadata))


func pop_action_metadata() -> Dictionary:
	if _action_metadata_stack.is_empty():
		return {}
	return _action_metadata_stack.pop_back()


func get_current_action_metadata() -> Dictionary:
	if _action_metadata_stack.is_empty():
		return {}
	return _action_metadata_stack[_action_metadata_stack.size() - 1].duplicate(true)


func merge_current_action_metadata(metadata: Dictionary = {}) -> Dictionary:
	var current := get_current_action_metadata()
	var merged := current.duplicate(true)
	for key in metadata.keys():
		if _is_runtime_fixed_metadata_key(key) and merged.has(key):
			continue
		merged[key] = metadata.get(key)
	var runtime_action_metadata: Dictionary = {}
	if merged.get("runtime_action_metadata", {}) is Dictionary:
		runtime_action_metadata = (merged.get("runtime_action_metadata", {}) as Dictionary).duplicate(true)
	for key in merged.keys():
		if _is_runtime_metadata_export_key(key):
			runtime_action_metadata[key] = merged.get(key)
	merged["runtime_action_metadata"] = runtime_action_metadata
	return merged


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
		"transition": decision.transition.duplicate(true) if decision != null and decision.transition is Dictionary else {},
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


func _normalize_runtime_action_metadata(metadata: Dictionary) -> Dictionary:
	var normalized := metadata.duplicate(true)
	for key in ["generated", "state_id", "slot_id", "skill_id", "variant_id", "action_family", "source_action_id", "identity_key", "score_bucket_id", "action_id"]:
		if normalized.has(key):
			normalized[key] = normalized.get(key)
	return normalized


func _is_runtime_fixed_metadata_key(key: Variant) -> bool:
	return String(key) in [
		"generated",
		"state_id",
		"slot_id",
		"skill_id",
		"variant_id",
		"action_family",
		"source_action_id",
		"identity_key",
		"score_bucket_id",
		"action_id",
	]


func _is_runtime_metadata_export_key(key: Variant) -> bool:
	return String(key) in [
		"generated",
		"state_id",
		"slot_id",
		"skill_id",
		"variant_id",
		"action_family",
		"source_action_id",
		"identity_key",
	]
